/* ROM-driven audio adapter. No game code, samples, or save states are embedded. */
#include <stdlib.h>
#include <string.h>
#include "vb.h"

#define API __declspec(dllexport)
#define RETURN_PC 0x0500F000
typedef struct {
    VB *sim;
    void *baseline;
    unsigned char *rom;
    unsigned stop;
    unsigned base;
    unsigned proto;
    unsigned loops[6];
    unsigned repeatDepth[6];
} Audio;

static int executed(VB *sim, uint32_t address, const uint16_t *code, int length) {
    Audio *a = (Audio *)vbGetUserData(sim);
    (void)code; (void)length;
    if (address == a->stop) return 1;
    unsigned loop = a->proto ? 0x0703D314 : 0x07054F74;
    unsigned repeat = a->proto ? 0x0703D21A : 0x0705500A;
    unsigned endRepeat = a->proto ? 0x0703D254 : 0x07054FC8;
    if (address == loop || address == repeat || address == endRepeat) {
        unsigned channel = (unsigned)vbGetProgramRegister(sim, a->proto ? 20 : 6) - a->base;
        if (channel < 6 * 132 && channel % 132 == 0) {
            unsigned voice = a->base + channel, index = channel / 132;
            unsigned depth = vbRead(sim, voice + 0x38, VB_U16);
            /* The ROM also uses F1 7F / F2 for whole-song repetition.
               Count that enclosing repeat, never ordinary short nested repeats. */
            if (address == repeat && !a->repeatDepth[index] &&
                vbRead(sim, (a->proto ? vbRead(sim, voice, VB_S32) : vbGetProgramRegister(sim, 10) + 1), VB_U8) == 127)
                a->repeatDepth[index] = depth + 1;
            if (address == loop || (address == endRepeat &&
                a->repeatDepth[index] && depth == a->repeatDepth[index])) a->loops[index]++;
        }
    }
    return 0;
}

static int call(Audio *a, unsigned pc, int argument) {
    uint32_t clocks = 2000000, psw = vbGetSystemRegister(a->sim, VB_PSW);
    vbSetSystemRegister(a->sim, VB_PSW, psw | 0x1000);
    vbSetProgramRegister(a->sim, 6, argument);
    vbSetProgramRegister(a->sim, 7, 15);
    vbSetProgramRegister(a->sim, 8, 15);
    vbSetProgramRegister(a->sim, 31, RETURN_PC);
    vbSetProgramCounter(a->sim, pc);
    a->stop = RETURN_PC;
    vbEmulate(a->sim, &clocks);
    a->stop = 0;
    vbSetSystemRegister(a->sim, VB_PSW, psw);
    return vbGetProgramCounter(a->sim) == RETURN_PC;
}

API void bhAudioFree(Audio *a) {
    if (!a) return;
    free(a->sim); free(a->baseline); free(a->rom); free(a);
}

API Audio *bhAudioCreate(const unsigned char *rom, unsigned length) {
    Audio *a;
    unsigned i;
    uint32_t clocks;
    if (!rom || length != 2097152) return NULL;
    a = (Audio *)calloc(1, sizeof(Audio));
    if (!a) return NULL;
    a->sim = (VB *)malloc(vbSizeOf());
    a->baseline = malloc(vbSizeOf());
    a->rom = (unsigned char *)malloc(length);
    if (!a->sim || !a->baseline || !a->rom) { bhAudioFree(a); return NULL; }
    memcpy(a->rom, rom, length);
    a->proto = rom[0] == 0x80 && rom[1] == 0xBC;
    vbInit(a->sim);
    if (vbSetCartROM(a->sim, a->rom, length)) { bhAudioFree(a); return NULL; }
    vbReset(a->sim);
    /* Fresh boot from this user's ROM. No development save-state dependency. */
    for (i = 0; i < 150; i++) { clocks = 400000; vbEmulate(a->sim, &clocks); }
    if ((!a->proto && (vbRead(a->sim, 0x0500118C, VB_S32) != 0x07079180 ||
        vbRead(a->sim, 0x0500FFC4, VB_S32) != 0x0705549C)) ||
        (a->proto && vbRead(a->sim, 0x0500003C, VB_S32) != 6)) { bhAudioFree(a); return NULL; }
    vbSetUserData(a->sim, a);
    vbSetExecuteCallback(a->sim, executed);
    vbWrite(a->sim, 0x0005F802, VB_U16, 0); /* Disable video IRQ; retain sound timer. */
    for (i = a->proto ? 0x05000040 : 0x05006568; i < (a->proto ? 0x05000880 : 0x05006DA8); i += 4) vbWrite(a->sim, i, VB_S32, 0);
    if (!call(a, a->proto ? 0x0703CAEC : 0x0705436C, 0)) { bhAudioFree(a); return NULL; }
    vbWrite(a->sim, RETURN_PC, VB_U16, 0x6800);     /* HALT */
    vbWrite(a->sim, RETURN_PC + 2, VB_U16, 0x8BFE); /* BR -2 */
    vbSetProgramCounter(a->sim, RETURN_PC);
    /* Drain the boot sound's analog filter before taking the silent baseline. */
    for (i = 0; i < 50; i++) { clocks = 400000; vbEmulate(a->sim, &clocks); }
    memcpy(a->baseline, a->sim, vbSizeOf());
    return a;
}

API int bhAudioStart(Audio *a, int effect, int id) {
    if (!a || (effect != 0 && effect != 1) || id < (effect ? 0 : 1) || id > (effect ? 56 : 24)) return 0;
    /* Same allocation: all internal baseline pointers remain valid. */
    memcpy(a->sim, a->baseline, vbSizeOf());
    memset(a->loops, 0, sizeof(a->loops));
    memset(a->repeatDepth, 0, sizeof(a->repeatDepth));
    a->base = a->proto ? (effect ? 0x05000460 : 0x05000040) : (effect ? 0x05006988 : 0x05006568);
    return call(a, a->proto ? (effect ? 0x0703D7F8 : 0x0703D87A) : (effect ? 0x070555BC : 0x07055678), id);
}

/* Exactly 20 ms: 834 stereo frames at the core's native 41,700 Hz. */
API int bhAudioStep(Audio *a, short *samples) {
    uint32_t clocks = 400000;
    unsigned position;
    if (!a || !samples || vbSetSamples(a->sim, samples, VB_S16, 834)) return 0;
    vbEmulate(a->sim, &clocks);
    vbGetSamples(a->sim, NULL, NULL, &position);
    return position == 834;
}

/* 0 playing, 1 naturally ended, 2 all remaining voices completed two loops. */
API int bhAudioStatus(Audio *a) {
    unsigned i, active = 0, pending = 0;
    for (i = 0; i < 6; i++) if (vbRead(a->sim, a->base + i * 132, VB_S32)) {
        active++;
        if (a->loops[i] < 2) pending++;
    }
    return !active ? 1 : !pending ? 2 : 0;
}
