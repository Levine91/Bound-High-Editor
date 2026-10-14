/* This file is included into vb.c and cannot be compiled on its own. */
#ifdef VBAPI



/********************************* Constants *********************************/

/* Pseudo-halt operations */
#define PH_NEVER    0
#define PH_DPSTTS   1
#define PH_GAME_PAD 2
#define PH_INTPND   3
#define PH_TCR      4
#define PH_TLHR     5
#define PH_XPSTTS   6



/***************************** Module Functions ******************************/

/* Activate pseudo-halt */
static void phActivate(VB *sim, uint32_t address, int type) {
    int range; /* Memory address range by component */

    /* Working variables */
    address &= 0x07FFFFFF;
    range    = address >> 24;

    /* Configure pseudo-halt */
    sim->ph.operation = PH_NEVER;

    /* VIP */
    if (range == 0) {
        address &= 0x0007FFFF;

        /* TODO: Frame buffer */

        /* I/O register */
        switch (address) {
            case 0x5F800: /* INTPND */
                if (
                    sim->vip.dp.step != 0                   ||
                    sim->vip.dp.enabled                     ||
                    (sim->vip.dp.disp && sim->vip.dp.synce) ||
                    sim->vip.xp.enabled                     ||
                    sim->vip.xp.xpen
                ) sim->ph.operation = PH_INTPND;
                break;
            case 0x5F820: /* DPSTTS */
                if (
                    sim->vip.dp.step != 0 ||
                    sim->vip.dp.enabled   ||
                    (sim->vip.dp.disp && sim->vip.dp.synce)
                ) sim->ph.operation = PH_DPSTTS;
                break;
            case 0x5F840: /* XPSTTS */
                if (sim->vip.xp.enabled || sim->vip.xp.xpen)
                    sim->ph.operation = PH_XPSTTS;
                break;
        }

    }

    /* Misc. I/O */
    else if (range == 2 && (type == VB_S8 || type == VB_U8)) {
        address &= 0x0000003F;
        switch (address) {

            /* TODO: Communication port */

            /* Game pad */
            case 0x10: /* SDLR */
            case 0x14: /* SDHR */
            case 0x28: /* SCR */
                if (sim->pad.si_stat != 0)
                    sim->ph.operation = PH_GAME_PAD;
                break;

            /* Timer */
            case 0x18: /* TLR */
            case 0x1C: /* THR */
                if (sim->tmr.t_enb)
                    sim->ph.operation = PH_TLHR;
                break;
            case 0x20: /* TCR */
                if (sim->tmr.t_enb)
                    sim->ph.operation = PH_TCR;
                break;
        }
    }

    /* Configure CPU */
    sim->cpu.clocks    = phUntil(sim);
    sim->cpu.operation = CPU_PHALT;
}

/* Test whether the current memory access matches the monitored access */
static int phMatches(VB *sim, uint32_t address, int type, int32_t value) {
    int match; /* Parameter match */
    int x;     /* Iterator */

    /* New memory access */
    if (sim->ph.step == 0)
        return 0;

    /* Check memory access parameters */
    match =
        address     == sim->ph.address &&
        sim->cpu.pc == sim->ph.pc      &&
        type        == sim->ph.type    &&
        value       == sim->ph.value
    ;
    if (!match || sim->ph.step != 3)
        return match;

    /* Check full CPU state */
    for (x = 1; x < 32; x++) {
        if (sim->ph.program[x - 1] != sim->cpu.program[x])
            return 0;
    }
    return
        sim->ph.adtre == sim->cpu.adtre                     &&
        sim->ph.chcw  == cpuGetSystemRegister(sim, VB_CHCW) &&
        sim->ph.ecr   == cpuGetSystemRegister(sim, VB_ECR)  &&
        sim->ph.eipc  == sim->cpu.eipc                      &&
        sim->ph.eipsw == sim->cpu.eipsw                     &&
        sim->ph.fepc  == sim->cpu.fepc                      &&
        sim->ph.fepsw == sim->cpu.fepsw                     &&
        sim->ph.psw   == cpuGetSystemRegister(sim, VB_PSW )
    ;
}



/****************************** Clock Measurers ******************************/

/* DPSTTS */
static uint32_t phDPSTTS(VB *sim) {
    switch (sim->vip.dp.step) {
        case 0: /* 0ms - FCLK rising edge */
        case 2: /* 3ms-8ms - Display left frame buffer */
        case 3: /* 8ms - L*BSY falling edge */
        case 6: /* 13ms-18ms - Display right frame buffer */
        case 7: /* 18ms - R*BSY falling edge */
        case 8: /* 20ms - Display interval complete */
            return sim->vip.dp.until;
    }
    /* case 1: 3ms - L*BSY rising edge */
    /* case 4: 10ms - FCLK falling edge */
    /* case 5: 13ms - R*BSY rising edge */
    return sim->vip.dp.clocks;
}

/* INTPND */
static uint32_t phINTPND(VB *sim) {
    if (!(
        sim->vip.dp.step != 0 ||
        sim->vip.dp.enabled   ||
        (sim->vip.dp.disp && sim->vip.dp.synce)
    )) return sim->vip.xp.until;
    if (!(sim->vip.xp.enabled || sim->vip.xp.xpen))
        return sim->vip.dp.until;
    return sim->vip.dp.until < sim->vip.xp.until ?
        sim->vip.dp.until : sim->vip.xp.until;
}

/* XPSTTS */
static uint32_t phXPSTTS(VB *sim) {
    uint32_t  clocks;    /* Return value */
    uint16_t *halfwords; /* Drawing clocks this SBCOUNT */
    uint32_t  x;         /* Iterator */

    /* Drawing is underway */
    if (sim->vip.xp.f0bsy || sim->vip.xp.f1bsy) {
        clocks    = 0;
        halfwords = &sim->vip.halfwords[
            (uint32_t) sim->vip.xp.sbcount * 384 + sim->vip.xp.column];
        for (x = sim->vip.xp.column; x < 384; x++, halfwords++)
            clocks += *halfwords;
        return clocks;
    }

    /* Drawing is idle */
    switch (sim->vip.dp.step) {
        case 0: /* 0ms - FCLK rising edge */
            return sim->vip.dp.until;
        case 1: /* 3ms - L*BSY rising edge */
        case 2: /* 3ms-8ms - Display left frame buffer */
        case 3: /* 8ms - L*BSY falling edge */
            return sim->vip.dp.until + vipClocksMs(12);
    }
    /* case 4: 10ms - FCLK falling edge */
    /* case 5: 13ms - R*BSY rising edge */
    /* case 6: 13ms-18ms - Display right frame buffer */
    /* case 7: 18ms - R*BSY falling edge */
    return sim->vip.dp.until + vipClocksMs(2);
}



/***************************** Library Functions *****************************/

/* Test whether to activate pseudo-halt */
static int phAssess(VB *sim, uint32_t address, int type, int32_t value) {
    int x; /* Iterator */

    /* Memory access does not match last time */
    address &= TYPE_MASKS[type];
    if (!phMatches(sim, address, type, value))
        sim->ph.step = 0;

    /* New memory access */
    if (sim->ph.step == 0) {
        sim->ph.address = address;
        sim->ph.pc      = sim->cpu.pc;
        sim->ph.step    = 1;
        sim->ph.type    = type;
        sim->ph.value   = value;
        return 0;
    }

    /* Repeated memory access, not checking full CPU state */
    if (sim->ph.step < 2) {
        sim->ph.step++;
        return 0;
    }

    /* Take a snapshot of the full CPU state */
    if (sim->ph.step == 2) {
        for (x = 1; x < 32; x++)
            sim->ph.program[x - 1] = sim->cpu.program[x];
        sim->ph.adtre = sim->cpu.adtre;
        sim->ph.chcw  = cpuGetSystemRegister(sim, VB_CHCW);
        sim->ph.ecr   = cpuGetSystemRegister(sim, VB_ECR);
        sim->ph.eipc  = sim->cpu.eipc;
        sim->ph.eipsw = sim->cpu.eipsw;
        sim->ph.fepc  = sim->cpu.fepc;
        sim->ph.fepsw = sim->cpu.fepsw;
        sim->ph.psw   = cpuGetSystemRegister(sim, VB_PSW);
        sim->ph.step  = 3;
        return 0;
    }

    /* Activate pseudo-halt */
    phActivate(sim, address, type);
    return 1;
}

/* Determine how long the CPU should wait before checking the monitor value */
static uint32_t phUntil(VB *sim) {
    switch (sim->ph.operation) {
        case PH_DPSTTS  : return phDPSTTS(sim);
        case PH_GAME_PAD: return sim->pad.si_stat;
        case PH_INTPND  : return phINTPND(sim);
        case PH_TCR     : return sim->tmr.until;
        case PH_TLHR    : return sim->tmr.clocks;
        case PH_XPSTTS  : return phXPSTTS(sim);
    }
    return 0; /* PH_NEVER */
}



#endif /* VBAPI */
