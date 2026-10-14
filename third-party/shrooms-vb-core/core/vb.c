#ifndef VBAPI
#define VBAPI
#endif
#include <float.h>
#include <vb.h>



/*********************************** Types ***********************************/

/* VIP BG map cell */
typedef struct {
    uint8_t *palette; /* Points to a VB.vip.gplt */
    uint8_t *pixels;  /* Points to a VB.vip.characters */
} Cell;

/* VSU channel */
typedef struct {

    /* Envelope */
    struct {

        /* Register state */
        uint8_t enb;      /* Modifications enabled */
        uint8_t dir;      /* Modification direction */
        uint8_t interval; /* Modification interval */
        uint8_t rep;      /* Repeat modifications */
        uint8_t value;    /* Master output level */

        /* Other state */
        uint32_t clocks;  /* Clocks until modification */
        uint8_t  modmask; /* Modifications masked */
        uint8_t  reload;  /* Automatic reload value */
    } env;

    /* Frequency */
    struct {
        uint16_t current; /* Current value */
        uint16_t written; /* Last value written */
    } freq;

    /* Stereo levels */
    struct {
        uint8_t left;  /* Left output level */
        uint8_t right; /* Right output level */
    } lrv;

    /* Control */
    struct {

        /* Register state */
        uint8_t auto_;    /* Shutoff enabled */
        uint8_t enb;      /* Sound generation enabled */
        uint8_t interval; /* Shutoff interval */

        /* Other state */
        uint32_t clocks; /* Clocks until shutoff */
    } int_;

    /* Waveform */
    struct {

        /* Register state */
        uint8_t wave; /* Waveform index */

        /* Other state */
        int sample; /* Current sample index */
    } wave;

    /* Other state */
    uint32_t clocks;  /* Clocks until next wave or noise sample */
    uint8_t  freqmod; /* Frequency modifications are active */
    uint32_t until;   /* Clocks until channel component update */
} Channel;

/* VIP character (hflp << 1 | vflp) */
typedef uint8_t Character[4][64];

/* VIP object */
typedef struct {
    Cell    cell; /* BG map-like attributes */
    int16_t jp;   /* Parallax */
    int16_t jx;   /* Screen base X */
    int16_t jy;   /* Screen Y */
    uint8_t lron; /* Visible in left and right images */
} Object;

/* Output image */
typedef uint8_t Pixels[2][384*224];

/* VIP world */
typedef struct {

    /* Attributes */
    uint8_t  bgm;       /* Mode */
    uint8_t  end;       /* Control world */
    int16_t  gp;        /* World parallax */
    int16_t  gx;        /* World base X */
    int16_t  gy;        /* World Y */
    int16_t  mx;        /* Scroll base X */
    int16_t  mp;        /* Scroll parallax */
    int16_t  my;        /* Scroll Y */
    uint8_t  over;      /* Use overplane */
    Cell    *overplane; /* Overplane cell */

    /* Non-attributes */
    Cell    *bg[64];    /* Background arrangement */
    int32_t  bgHeight;  /* Height of background in pixels */
    uint32_t bgMaskX;   /* Horizontal background mask */
    uint32_t bgMaskY;   /* Vertical background mask */
    uint32_t bgShift;   /* Bits to shift when selecting a BG map */
    int32_t  bgWidth;   /* Width of background in pixels */
    uint32_t hAffine;   /* H for affine mode */
    uint32_t hNaffine;  /* H for not affine mode */
    uint32_t height;    /* Height of world in pixels */
    uint8_t  lron;      /* Visible in left and right images */
    uint32_t paramBase; /* H-bias and affine parameter address */
    uint32_t wAffine;   /* W for affine mode */
    uint32_t wNaffine;  /* W for not affine mode */
    uint32_t width;     /* Width of world in pixels */
} World;

/* Simulation state */
struct VB {

    /* Game Pak */
    struct {
        uint8_t *ram;     /* Save RAM */
        uint8_t *rom;     /* Program ROM */
        uint32_t ramMask; /* Size of SRAM - 1 */
        uint32_t romMask; /* Size of ROM - 1 */
    } cart;

    /* CPU */
    struct {

        /* Cache Control Word */
        struct {
            uint8_t ice; /* Instruction Cache Enable */
        } chcw;

        /* Exception Cause Register */
        struct {
            uint16_t eicc; /* Exception/Interrupt Cause Code */
            uint16_t fecc; /* Fatal Error Cause Code */
        } ecr;

        /* Program Status Word */
        struct {
            uint8_t ae;  /* Address Trap Enable */
            uint8_t cy;  /* Carry */
            uint8_t ep;  /* Exception Pending */
            uint8_t fiv; /* Floating Invalid */
            uint8_t fov; /* Floating Overflow */
            uint8_t fpr; /* Floading Precision */
            uint8_t fro; /* Floating Reserved Operand */
            uint8_t fud; /* Floading Underflow */
            uint8_t fzd; /* Floating Zero Divide */
            uint8_t i;   /* Interrupt Level */
            uint8_t id;  /* Interrupt Disable */
            uint8_t np;  /* NMI Pending */
            uint8_t ov;  /* Overflow */
            uint8_t s;   /* Sign */
            uint8_t z;   /* Zero */
        } psw;

        /* Other registers */
        uint32_t adtre;       /* Address Trap Register for Execution */
        uint32_t eipc;        /* Exception/Interrupt PC */
        uint32_t eipsw;       /* Exception/Interrupt PSW */
        uint32_t fepc;        /* Fatal Error PC */
        uint32_t fepsw;       /* Fatal Error PSW */
        uint32_t pc;          /* Program Counter */
        int32_t  program[32]; /* Program registers */
        uint32_t sr29;        /* System register 29 */
        uint32_t sr31;        /* System register 31 */

        /* Working data */
        union {
            struct {
                uint32_t dest;
                uint64_t src;
            } bs;   /* Arithmetic bit strings */
            struct {
                uint32_t address;
                int32_t  value;
            } data; /* Data accesses */
        } aux;

        /* Other state */
        uint32_t clocks;    /* Master clocks to wait */
        uint16_t code[2];   /* Instruction code units */
        uint16_t exception; /* Exception cause code */
        int      halt;      /* CPU is halting */
        uint16_t irq;       /* Interrupt request lines */
        int      length;    /* Instruction code length */
        uint32_t nextPC;    /* Address of next instruction */
        int      operation; /* Current operation ID */
        int      step;      /* Operation sub-task ID */
    } cpu;

    /* Communication port */
    struct {

        /* Register state */
        uint8_t c_clk_sel;  /* Transmission clock source */
        uint8_t c_int_inh;  /* Interrupt acknowledge/disable */
        uint8_t c_stat;     /* Communication is underway */
        uint8_t cc_int_inh; /* Interrupt acknowledge/disable */
        uint8_t cc_int_lev; /* Interrupt condition */
        uint8_t cc_rd;      /* Manual read */
        uint8_t cc_sig;     /* Automatic write */
        uint8_t cc_smp;     /* Automatic read */
        uint8_t cc_wr;      /* Manual write */
        uint8_t cdrr;       /* Data received */
        uint8_t cdtr;       /* Data to transmit */

        /* Other state */
        int8_t   c_irq;  /* COM interrupt request */
        int8_t   cc_irq; /* COMCNT interrupt request */
        uint32_t clocks; /* Master clocks to wait */
    } ext;

    /* Game pad */
    struct {

        /* Register state */
        uint8_t  k_int_inh; /* Interrupt acknowledge/disable */
        uint8_t  para_si;   /* Read reset signal */
        uint8_t  s_abt_dis; /* Abort hardware read */
        uint8_t  sdhr;      /* High key bits */
        uint8_t  sdlr;      /* Low key bits */
        uint32_t si_stat;   /* Hardware read in progress */
        uint8_t  soft_ck;   /* Controller communication signal */

        /* Other state */
        uint32_t clocks; /* Master clocks to wait */
        uint16_t keys;   /* Next input bits */
        int      step;   /* Software read processing phase */
    } pad;

    /* Timer */
    struct {

        /* Register state */
        uint8_t t_clk_sel; /* Counter tick duration */
        uint8_t t_enb;     /* Enable timer */
        uint8_t tim_z_int; /* Enable interrupt */
        uint8_t z_stat;    /* Zero status */

        /* Other state */
        uint32_t clocks;  /* Master clocks to wait */
        uint16_t counter; /* Current counter value */
        uint16_t reload;  /* Reload counter value */
        uint8_t  tick20;  /* Current 20-microsecond tick */
        uint32_t until;   /* Clocks until interrupt condition */
    } tmr;

    /* VIP */
    struct {

        /* CTA */
        struct {
            uint8_t cta_l; /* Left column table index */
            uint8_t cta_r; /* Right column table index */
        } cta;

        /* Display processor */
        struct {

            /* Register state */
            uint8_t  disp;    /* Display enabled */
            uint8_t  fclk;    /* Frame clock signal high */
            uint8_t  l0bsy;   /* Displaying left frame buffer 0 */
            uint8_t  l1bsy;   /* Displaying left frame buffer 1 */
            uint8_t  lock;    /* Lock CTA */
            uint8_t  r0bsy;   /* Displaying right frame buffer 0 */
            uint8_t  r1bsy;   /* Displaying right frame buffer 1*/
            uint8_t  re;      /* Memory refresh enabled */
            uint8_t  scanrdy; /* Mirrors are stable */
            uint8_t  synce;   /* Servo enabled */

            /* Other state */
            uint8_t  brt[4];  /* Precomputed brightness */
            int      buffer;  /* Index of frame buffer to display */
            uint32_t clocks;  /* Master clocks to wait */
            int      column;  /* Index of column to display */
            uint32_t cta;     /* Column table pointer in memory */
            uint8_t  enabled; /* Was enabled at FCLK */
            uint32_t fbDest;  /* Output frame pixel address */
            uint32_t fbSrc;   /* Source frame buffer address */
            int32_t  repeat;  /* Current column table repeat value */
            int      step;    /* Processing phase */
            uint32_t until;   /* Clocks until interrupt condition */
        } dp;

        /* Pixel processor */
        struct {

            /* Register state */
            uint8_t  f0bsy;    /* Drawing into frame buffer 0 */
            uint8_t  f1bsy;    /* Drawing into frame buffer 1 */
            uint8_t  overtime; /* Drawing extends into display interval */
            uint8_t  sbcmp;    /* Vertical output position compare */
            uint8_t  sbcount;  /* Current vertical output position */
            uint32_t sbout;    /* Drawing specified vertical output position */
            uint8_t  xpen;     /* Drawing enabled */

            /* Other state */
            uint32_t clocks;   /* Master clocks to wait */
            int      column;   /* Current horizontal output position */
            uint8_t  enabled;  /* Was enabled at FCLK */
            int      frame;    /* FRMCYC counter */
            int32_t  halfword; /* Current output halfword offset */
            int      step;     /* Processing phase */
            uint8_t  toggle;   /* Switch frame buffers next frame */
            uint32_t until;    /* Clocks until interrupt condition */
        } xp;

        /* Control state */
        uint8_t  bkcol;      /* Backdrop color */
        uint8_t  brtRest[4]; /* Brightness and REST */
        uint8_t  frmcyc;     /* Game frame control */
        uint8_t  gplt[4][4]; /* Background palettes */
        uint16_t intenb;     /* Interrupts enabled */
        uint16_t intpnd;     /* Interrupts pending */
        uint8_t  jplt[4][4]; /* Object palettes */
        uint16_t spt[4];     /* Object control */

        /* Rendering shadow memory */
        uint8_t  buffer;            /* Output buffer */
        uint16_t halfwords[384*28]; /* Output timing by 1x8 halfword */
        Pixels   output[2];         /* Output images, row-major */
        Pixels   shadow;            /* Drawing shadow image, column-major */

        /* Other state */
        Cell      cells[0x10000];   /* Pre-computed BG map attributes */
        Character characters[2048]; /* Pre-computed character pixels */
        Object    objects[1024];    /* Pre-computed object attributes */
        uint8_t   ram[0x40000];     /* Video memory */
        World     worlds[32];       /* Pre-computed world attributes */
    } vip;

    /* VSU */
    struct {

        /* Audio sources */
        Channel channels[6];

        /* Channel 5 frequency modification */
        struct {

            /* Register state */
            uint8_t clk;      /* Base modification clock */
            uint8_t dir;      /* Sweep direction */
            uint8_t enb;      /* Modifications enabled */
            uint8_t func;     /* Modification function */
            uint8_t interval; /* Modification interval */
            uint8_t rep;      /* Repeat modulation */
            uint8_t shift;    /* Sweep shift amount */

            /* Other state */
            uint32_t clocks;   /* Clocks until modification */
            uint8_t  freqmask; /* Frequency mask */
            uint8_t  modmask;  /* Modifications masked */
            uint16_t next;     /* Next frequency value */
            int      sample;   /* Current sample index */
        } freqmod;

        /* Channel 6 noise generator */
        struct {

            /* Register state */
            uint8_t tap; /* LSFR feedback bit position */

            /* Other state */
            uint16_t register_; /* Pseudorandom bits */
        } noise;

        /* Sample output */
        struct {
            float    i1[2];    /* Previous analog input sample */
            float    o1[2];    /* Previous analog output sample */
            unsigned capacity; /* Number of audio frames in output buffer */
            unsigned offset;   /* Position in output buffer */
            void    *samples;  /* Output buffer */
            int      type;     /* Output data type */
        } out;

        /* Memory */
        int8_t  modulation[32]; /* Modulation amounts */
        uint8_t waves[5][32];   /* Wafeform samples */

        /* Other state */
        uint32_t clocks; /* Clocks until next output sample */
        int      sample; /* Output sample index, period 417 */
    } vsu;

    /* Wait controller */
    struct {
        uint8_t exp1w; /* Cartridge expansion 1-wait */
        uint8_t rom1w; /* Cartridge ROM 1-wait */
    } wcr;

    /* Pseudo-halt */
    struct {
        uint32_t address;   /* Monitor address */
        uint8_t  enabled;   /* Pseudo-halt function is enabled */
        uint8_t  operation; /* Monitoring operation */
        uint8_t  step;      /* Number of consecutive matching reads */
        int      type;      /* Memory access type */
        int32_t  value;     /* Value read from monitor address */

        /* CPU snapshot */
        uint32_t adtre;
        uint32_t chcw;
        uint32_t ecr;
        uint32_t eipc;
        uint32_t eipsw;
        uint32_t fepc;
        uint32_t fepsw;
        uint32_t pc;
        int32_t  program[31];
        uint32_t psw;
        uint32_t sr29;
        uint32_t sr31;
    } ph;

    /* Other state */
    uint8_t wram[0x10000]; /* System RAM */

    /* Application data */
    vbOnException onException; /* CPU exception */
    vbOnExecute   onExecute;   /* CPU instruction execute */
    vbOnFetch     onFetch;     /* CPU instruction fetch */
    vbOnFrame     onFrame;     /* VIP frame ready */
    vbOnLink      onLink;      /* Communication transfer */
    vbOnRead      onRead;      /* CPU instruction read */
    vbOnSamples   onSamples;   /* VSU samples full */
    vbOnWrite     onWrite;     /* CPU instruction write */
    VB           *peer;        /* Communication peer */
    void         *tag;         /* User data */
};



/***************************** Library Functions *****************************/

/* Sign-extend an integer of variable width */
static int32_t SignExtend(int32_t value, int32_t bits) {
    #ifndef VB_SIGNED_PROPAGATE
        value &= ~((uint32_t) 0xFFFFFFFF << bits);
        bits   = (int32_t) 1 << (bits - (int32_t) 1);
        return (value ^ bits) - bits;
    #else
        return value << (32 - bits) >> (32 - bits);
    #endif
}



/******************************** Sub-Modules ********************************/

#include "ext.c"
#include "game-pad.c"
#include "timer.c"
#include "bus.c"
#include "cpu.c"
#include "vip.c"
#include "vsu.c"
#include "pseudo-halt.c"



/***************************** Library Functions *****************************/

/* Process a simulation for a given number of clocks */
static int sysEmulate(VB *sim, uint32_t clocks) {
    int ret = 0;

    /* CPU is in a pseudo-halt state that requires an interrupt to exit */
    int never =
        sim->cpu.operation == CPU_PHALT &&
        sim->ph.operation  == PH_NEVER
    ;

    /* Process all components */
    if (!never){ret  = cpuEmulate(sim, clocks);}
                ret |= extEmulate(sim, clocks);
                       padEmulate(sim, clocks);
                       tmrEmulate(sim, clocks);
                       vsuEmulate(sim, clocks);
                ret |= vipEmulate(sim, clocks);
    if ( never) ret |= cpuEmulate(sim, clocks);
    return ret;
}

/* Determine how many clocks are guaranteed to process */
static uint32_t sysUntil(VB *sim, uint32_t clocks) {
    clocks = cpuUntil(sim, clocks);
    clocks = extUntil(sim, clocks);
    clocks = padUntil(sim, clocks);
    clocks = tmrUntil(sim, clocks);
    clocks = vipUntil(sim, clocks);
    return clocks;
}



/******************************* API Commands ********************************/

/* Process one simulation */
VBAPI int vbEmulate(VB *sim, uint32_t *clocks) {
    int      brk;   /* A callback requested a break */
    uint32_t until; /* Clocks guaranteed to process */
    while (*clocks != 0) {
        until    = sysUntil(sim, *clocks);
        brk      = sysEmulate(sim, until);
        *clocks -= until;
        if (brk)
            return brk; /* TODO: return 1 */
    }
    return 0;
}

/* Process multiple simulations */
VBAPI int vbEmulateEx(VB **sims, unsigned count, uint32_t *clocks) {
    int      brk;   /* A callback requested a break */
    uint32_t until; /* Clocks guaranteed to process */
    unsigned x;     /* Iterator */

    /* Process one simulation */
    if (count == 1)
        return vbEmulate(sims[0], clocks);

    /* Process multiple simulations */
    while (*clocks != 0) {
        until = *clocks;
        for (x = count; x > 0; x--)
            until = sysUntil(sims[x - 1], until);
        brk = 0;
        for (x = count; x > 0; x--)
            brk |= sysEmulate(sims[x - 1], until);
        *clocks -= until;
        if (brk)
            return brk; /* TODO: return 1 */
    }
    return 0;
}

/* Retrieve the game pack RAM buffer */
VBAPI void* vbGetCartRAM(VB *sim, uint32_t *size) {
    if (size != NULL)
        *size = sim->cart.ram == NULL ? 0 : sim->cart.ramMask + 1;
    return sim->cart.ram;
}

/* Retrieve the game pack ROM buffer */
VBAPI void* vbGetCartROM(VB *sim, uint32_t *size) {
    if (size != NULL)
        *size = sim->cart.rom == NULL ? 0 : sim->cart.romMask + 1;
    return sim->cart.rom;
}

/* Retrieve the exception callback handler */
VBAPI vbOnException vbGetExceptionCallback(VB *sim) {
    return sim->onException;
}

/* Retrieve the execute callback handler */
VBAPI vbOnExecute vbGetExecuteCallback(VB *sim) {
    return sim->onExecute;
}

/* Retrieve the fetch callback handler */
VBAPI vbOnFetch vbGetFetchCallback(VB *sim) {
    return sim->onFetch;
}

/* Retrieve the frame callback handler */
VBAPI vbOnFrame vbGetFrameCallback(VB *sim) {
    return sim->onFrame;
}

/* Retrieve the current game pad key state */
VBAPI uint16_t vbGetKeys(VB *sim) {
    return sim->pad.keys;
}

/* Retrieve the current link callback handler */
VBAPI vbOnLink vbGetLinkCallback(VB *sim) {
    return sim->onLink;
}

/* Retrieve a core option value */
VBAPI int vbGetOption(VB *sim, int key) {
    switch (key) {
        case VB_PSEUDO_HALT: return sim->ph.enabled;
    }
    return 0;
}

/* Retrieve the communication peer */
VBAPI VB* vbGetPeer(VB *sim) {
    return sim->peer;
}

/* Retrieve the most recent frame image pixels */
VBAPI void vbGetPixels(VB *sim, void *left, int leftStrideX, int leftStrideY,
    void *right, int rightStrideX, int rightStrideY) {
    uint8_t *dest;    /* Output data */
    int      offset;  /* Horizontal offset of output pixel */
    uint8_t *src;     /* Source data */
    int      xStride; /* Bytes between output pixels */
    int      yStride; /* Bytes between output lines */
    int      i, x, y; /* Iterators */

    /* Process both eyes */
    for (i = 0; i < 2; i++) {

        /* Working variables for left image */
        if (i == 0) {
            dest    = left;
            xStride = leftStrideX;
            yStride = leftStrideY;
        }

        /* Working variables for right image */
        else {
            dest    = right;
            xStride = rightStrideX;
            yStride = rightStrideY;
        }

        /* Nothing to do */
        if (dest == NULL)
            continue;

        /* Transfer pixels to the destination */
        src = sim->vip.output[sim->vip.buffer][i];
        for (y = 0; y < 224; y++, dest += yStride)
        for (x = offset = 0; x < 384; x++, offset += xStride)
            dest[offset] = *src++;
    }

}

/* Retrieve the value of the program counter */
VBAPI uint32_t vbGetProgramCounter(VB *sim) {
    return sim->cpu.pc;
}

/* Retrieve the value in a program register */
VBAPI int32_t vbGetProgramRegister(VB *sim, unsigned index) {
    return index < 1 || index > 31 ? 0 : sim->cpu.program[index];
}

/* Retrieve the read callback handler */
VBAPI vbOnRead vbGetReadCallback(VB *sim) {
    return sim->onRead;
}

/* Retrieve the audio samples buffer */
VBAPI void* vbGetSamples(VB *sim, int *type,
    unsigned *capacity, unsigned *position) {
    if (sim->vsu.out.samples == NULL) {
        if (type     != NULL)
            *type     = 0;
        if (capacity != NULL)
            *capacity = 0;
        if (position != NULL)
            *position = 0;
    } else {
        if (type     != NULL)
            *type     = sim->vsu.out.type;
        if (capacity != NULL)
            *capacity = sim->vsu.out.capacity;
        if (position != NULL)
            *position = sim->vsu.out.offset >> 1;
    }
    return sim->vsu.out.samples;
}

/* Retrieve the samples callback handler */
VBAPI vbOnSamples vbGetSamplesCallback(VB *sim) {
    return sim->onSamples;
}

/* Retrieve the value in a system register */
VBAPI uint32_t vbGetSystemRegister(VB *sim, unsigned index) {
    return index > 31 ? 0 : cpuGetSystemRegister(sim, index);
}

/* Retrieve a simulation's userdata pointer */
VBAPI void* vbGetUserData(VB *sim) {
    return sim->tag;
}

/* Retrieve the write callback handler */
VBAPI vbOnWrite vbGetWriteCallback(VB *sim) {
    return sim->onWrite;
}

/* Initialize a simulation instance */
VBAPI VB* vbInit(VB *sim) {
    sim->cart.ram        = NULL;
    sim->cart.rom        = NULL;
    sim->ph.enabled      = 0;
    sim->vsu.out.samples = NULL;
    sim->onExecute       = NULL;
    sim->onFetch         = NULL;
    sim->onFrame         = NULL;
    sim->onLink          = NULL;
    sim->onRead          = NULL;
    sim->onSamples       = NULL;
    sim->onWrite         = NULL;
    sim->peer            = NULL;
    sim->tag             = NULL;
    vbReset(sim);
    return sim;
}

/* Read a value from the memory bus */
VBAPI int32_t vbRead(VB *sim, uint32_t address, int type) {
    int32_t value;
    if (type < 0 || type > 4)
        return 0;
    busRead(sim, address, type, &value);
    return value;
}

/* Simulate a hardware reset */
VBAPI VB* vbReset(VB *sim) {
    int x; /* Iterator */

    /* Wait controller */
    sim->wcr.exp1w = 0;
    sim->wcr.rom1w = 0;

    /* WRAM (the hardware does not do this) */
    for (x = 0; x < 0x10000; x++)
        sim->wram[x] = 0x00;

    /* Components */
    cpuReset(sim);
    extReset(sim);
    padReset(sim);
    tmrReset(sim);
    vipReset(sim);
    vsuReset(sim);

    /* Pseudo-halt */
    sim->ph.step = 0;
    return sim;
}

/* Specify a game pak RAM buffer */
VBAPI int vbSetCartRAM(VB *sim, void *sram, uint32_t size) {
    if (sram != NULL) {
        if (size < 16 || size > 0x1000000 || (size & (size - 1)) != 0)
            return 1;
        sim->cart.ramMask = size - 1;
    }
    sim->cart.ram = sram;
    return 0;
}

/* Specify a game pak ROM buffer */
VBAPI int vbSetCartROM(VB *sim, void *rom, uint32_t size) {
    if (rom != NULL) {
        if (size < 16 || size > 0x1000000 || (size & (size - 1)) != 0)
            return 1;
        sim->cart.romMask = size - 1;
    }
    sim->cart.rom = rom;
    return 0;
}

/* Specify a new exception callback handler */
VBAPI vbOnException vbSetExceptionCallback(VB *sim, vbOnException callback) {
    vbOnException prev = sim->onException;
    sim->onException   = callback;
    return prev;
}

/* Specify a new execute callback handler */
VBAPI vbOnExecute vbSetExecuteCallback(VB *sim, vbOnExecute callback) {
    vbOnExecute prev = sim->onExecute;
    sim->onExecute   = callback;
    return prev;
}

/* Specify a new fetch callback handler */
VBAPI vbOnFetch vbSetFetchCallback(VB *sim, vbOnFetch callback) {
    vbOnFetch prev = sim->onFetch;
    sim->onFetch   = callback;
    return prev;
}

/* Specify a new frame callback handler */
VBAPI vbOnFrame vbSetFrameCallback(VB *sim, vbOnFrame callback) {
    vbOnFrame prev = sim->onFrame;
    sim->onFrame   = callback;
    return prev;
}

/* Specify new game pad keys */
VBAPI uint16_t vbSetKeys(VB *sim, uint16_t keys) {
    return sim->pad.keys = keys;
}

/* Specify a new link callback handler */
VBAPI vbOnLink vbSetLinkCallback(VB *sim, vbOnLink callback) {
    vbOnLink prev = sim->onLink;
    sim->onLink   = callback;
    return prev;
}

/* Specify a new core option value */
VBAPI int vbSetOption(VB *sim, int key, int value) {
    switch (key) {
        case VB_PSEUDO_HALT:
            sim->ph.enabled = value = !!value;
            if (!value) {
                sim->ph.step = 0;
                if (sim->cpu.operation == CPU_PHALT) {
                    sim->cpu.operation = CPU_FETCH;
                    sim->cpu.step      = 0;
                }
            }
            break;
    }
    return value;
}

/* Specify a new communication peer */
VBAPI void vbSetPeer(VB *sim, VB *peer) {
    if (sim->peer == peer)
        return;
    if (sim->peer != NULL)
        sim->peer->peer = NULL;
    sim->peer = peer;
    if (peer == NULL)
        return;
    if (peer->peer != NULL)
        peer->peer->peer = NULL;
    peer->peer = sim;
}

/* Specify a new value for the program counter */
VBAPI uint32_t vbSetProgramCounter(VB *sim, uint32_t value) {
    sim->cpu.clocks    = 0;
    sim->cpu.operation = CPU_FETCH;
    sim->cpu.pc        = sim->cpu.nextPC = value & 0xFFFFFFFE;
    sim->cpu.step      = 0;
    return sim->cpu.pc;
}

/* Specify a new value for a program register */
VBAPI int32_t vbSetProgramRegister(VB *sim, unsigned index, int32_t value) {
    return index < 1 || index > 31 ? 0 : (sim->cpu.program[index] = value);
}

/* Specify a new read callback handler */
VBAPI vbOnRead vbSetReadCallback(VB *sim, vbOnRead callback) {
    vbOnRead prev = sim->onRead;
    sim->onRead   = callback;
    return prev;
}

/* Specify a new audio samples buffer */
VBAPI int vbSetSamples(VB *sim, void *samples, int type, unsigned capacity) {
    if (samples != NULL &&
        (capacity == 0 || (type != VB_S16 && type != VB_F32)))
        return 1;
    sim->vsu.out.capacity = capacity;
    sim->vsu.out.offset   = 0;
    sim->vsu.out.samples  = samples;
    sim->vsu.out.type     = type;
    return 0;
}

/* Specify a new samples callback handler */
VBAPI vbOnSamples vbSetSamplesCallback(VB *sim, vbOnSamples callback) {
    vbOnSamples prev = sim->onSamples;
    sim->onSamples = callback;
    return prev;
}

/* Specify a new value for a system register */
VBAPI uint32_t vbSetSystemRegister(VB *sim, unsigned index, uint32_t value) {
    return index > 31 ? 0 : cpuSetSystemRegister(sim, index, value, 1);
}

/* Specify a new write callback handler */
VBAPI vbOnWrite vbSetWriteCallback(VB *sim, vbOnWrite callback) {
    vbOnWrite prev = sim->onWrite;
    sim->onWrite   = callback;
    return prev;
}

/* Determine the size of a simulation instance */
VBAPI size_t vbSizeOf(void) {
    return sizeof (VB);
}

/* Specify a simulation's userdata pointer */
VBAPI void* vbSetUserData(VB *sim, void *tag) {
    void *prev = sim->tag;
    sim->tag   = tag;
    return prev;
}

/* Write a value to the memory bus */
VBAPI int32_t vbWrite(VB *sim, uint32_t address, int type, int32_t value) {
    if (type < 0 || type > 4)
        return 0;
    busWrite(sim, address, type, value, 1);
    return vbRead(sim, address, type);
}
