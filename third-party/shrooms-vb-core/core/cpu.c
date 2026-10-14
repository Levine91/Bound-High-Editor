/* This file is included into vb.c and cannot be compiled on its own. */
#ifdef VBAPI



/********************************* Constants *********************************/

/* Operation IDs */
#define CPU_EXCEPTION 0
#define CPU_HALTING   1
#define CPU_FATAL     2
#define CPU_FETCH     3
#define CPU_PHALT     4
#define CPU_ILLEGAL   5
#define CPU_BITSTRING 6
#define CPU_FLOATENDO 7
#define CPU_ADD_IMM   8
#define CPU_ADD_REG   9
#define CPU_ADDF_S   10
#define CPU_ADDI     11
#define CPU_AND      12
#define CPU_ANDI     13
#define CPU_BCOND    14
#define CPU_CAXI     15
#define CPU_CLI      16
#define CPU_CMP_IMM  17
#define CPU_CMP_REG  18
#define CPU_CMPF_S   19
#define CPU_CVT_SW   20
#define CPU_CVT_WS   21
#define CPU_DIV      22
#define CPU_DIVF_S   23
#define CPU_DIVU     24
#define CPU_HALT     25
#define CPU_IN_B     26
#define CPU_IN_H     27
#define CPU_IN_W     28
#define CPU_JAL      29
#define CPU_JMP      30
#define CPU_JR       31
#define CPU_LD_B     32
#define CPU_LD_H     33
#define CPU_LD_W     34
#define CPU_LDSR     35
#define CPU_MOV_IMM  36
#define CPU_MOV_REG  37
#define CPU_MOVEA    38
#define CPU_MOVHI    39
#define CPU_MPYHW    40
#define CPU_MUL      41
#define CPU_MULF_S   42
#define CPU_MULU     43
#define CPU_NOT      44
#define CPU_OR       45
#define CPU_ORI      46
#define CPU_OUT_B    47
#define CPU_OUT_H    48
#define CPU_OUT_W    49
#define CPU_RETI     50
#define CPU_REV      51
#define CPU_SAR_IMM  52
#define CPU_SAR_REG  53
#define CPU_SCH0BSD  54
#define CPU_SCH0BSU  55
#define CPU_SCH1BSD  56
#define CPU_SCH1BSU  57
#define CPU_SEI      58
#define CPU_SETF     59
#define CPU_SHL_IMM  60
#define CPU_SHL_REG  61
#define CPU_SHR_IMM  62
#define CPU_SHR_REG  63
#define CPU_ST_B     64
#define CPU_ST_H     65
#define CPU_ST_W     66
#define CPU_STSR     67
#define CPU_SUB      68
#define CPU_SUBF_S   69
#define CPU_TRAP     70
#define CPU_TRNC_SW  71
#define CPU_XB       72
#define CPU_XH       73
#define CPU_XOR      74
#define CPU_XORI     75
#define CPU_ANDBSU   76 /* Keep bit string ALU IDs consecutive */
#define CPU_ANDNBSU  77
#define CPU_MOVBSU   78
#define CPU_NOTBSU   79
#define CPU_ORBSU    80
#define CPU_ORNBSU   81
#define CPU_XORBSU   82
#define CPU_XORNBSU  83

/* Bit string operations */
#define CPU_AND_BS  0
#define CPU_ANDN_BS 1
#define CPU_MOV_BS  2
#define CPU_NOT_BS  3
#define CPU_OR_BS   4
#define CPU_ORN_BS  5
#define CPU_XOR_BS  6
#define CPU_XORN_BS 7



/********************************** Macros ***********************************/

/* Master clocks per CPU cycles */
#define cpuClocks(x) x

/* Shorthands */
#define auxBS      sim->cpu.aux.bs
#define auxData    sim->cpu.aux.data
#define bsDestAddr sim->cpu.program[29]
#define bsDestBit  sim->cpu.program[26]
#define bsLength   sim->cpu.program[28]
#define bsSkipped  sim->cpu.program[29]
#define bsSrcAddr  sim->cpu.program[30]
#define bsSrcBit   sim->cpu.program[27]



/******************************** Lookup Data ********************************/

/* Instruction code lengths by opcode */
static const uint8_t INST_LENGTHS[] = {
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
    1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2,
    2, 2, 1, 2, 2, 2, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2
};

/* Highest interrupt level by IRQ bit mask value */
static const int8_t IRQ_LEVELS[] = {
    -1, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3,
     4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4
};

/* Opdefs by opcode */
static const uint8_t OPDEFS[] = {
    CPU_MOV_REG, CPU_ADD_REG, CPU_SUB      , CPU_CMP_REG  , /* 000000 */
    CPU_SHL_REG, CPU_SHR_REG, CPU_JMP      , CPU_SAR_REG  ,
    CPU_MUL    , CPU_DIV    , CPU_MULU     , CPU_DIVU     ,
    CPU_OR     , CPU_AND    , CPU_XOR      , CPU_NOT      ,
    CPU_MOV_IMM, CPU_ADD_IMM, CPU_SETF     , CPU_CMP_IMM  , /* 010000 */
    CPU_SHL_IMM, CPU_SHR_IMM, CPU_CLI      , CPU_SAR_IMM  ,
    CPU_TRAP   , CPU_RETI   , CPU_HALT     , CPU_ILLEGAL  ,
    CPU_LDSR   , CPU_STSR   , CPU_SEI      , CPU_BITSTRING,
    CPU_BCOND  , CPU_BCOND  , CPU_BCOND    , CPU_BCOND    , /* 100000 */
    CPU_BCOND  , CPU_BCOND  , CPU_BCOND    , CPU_BCOND    ,
    CPU_MOVEA  , CPU_ADDI   , CPU_JR       , CPU_JAL      ,
    CPU_ORI    , CPU_ANDI   , CPU_XORI     , CPU_MOVHI    ,
    CPU_LD_B   , CPU_LD_H   , CPU_ILLEGAL  , CPU_LD_W     , /* 110000 */
    CPU_ST_B   , CPU_ST_H   , CPU_ILLEGAL  , CPU_ST_W     ,
    CPU_IN_B   , CPU_IN_H   , CPU_CAXI     , CPU_IN_W     ,
    CPU_OUT_B  , CPU_OUT_H  , CPU_FLOATENDO, CPU_OUT_W
};

/* Opdefs by bit string sub-opcode */
static const uint8_t OPDEFS_BITSTRING[] = {
    CPU_SCH0BSU, CPU_SCH0BSD, CPU_SCH1BSU, CPU_SCH1BSD, /* 00000 */
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ORBSU  , CPU_ANDBSU , CPU_XORBSU , CPU_MOVBSU , /* 01000 */
    CPU_ORNBSU , CPU_ANDNBSU, CPU_XORNBSU, CPU_NOTBSU ,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, /* 10000 */
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, /* 11000 */
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL
};

/* Opdefs by floating-point/Nintendo sub-opcodes */
static const uint8_t OPDEFS_FLOATENDO[] = {
    CPU_CMPF_S , CPU_ILLEGAL, CPU_CVT_WS , CPU_CVT_SW , /* 000000 */
    CPU_ADDF_S , CPU_SUBF_S , CPU_MULF_S , CPU_DIVF_S ,
    CPU_XB     , CPU_XH     , CPU_REV    , CPU_TRNC_SW,
    CPU_MPYHW  , CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, /* 010000 */
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, /* 100000 */
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, /* 110000 */
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL,
    CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL, CPU_ILLEGAL
};



/**************************** Forward references *****************************/

static int      phAssess (VB *, uint32_t, int, int32_t);
static uint32_t phUntil  (VB *);



/***************************** Callback Handlers *****************************/

/* Prepare to handle an exception */
#ifndef VB_DIRECT_EXCEPTION
    #define VB_ON_EXCEPTION sim->onException
#else
    extern int VB_DIRECT_EXCEPTION(VB *, uint16_t *);
    #define VB_ON_EXCEPTION VB_DIRECT_EXCEPTION
#endif
static int cpuOnException(VB *sim, uint16_t *cause) {
    return sim->onException != NULL && VB_ON_EXCEPTION(sim, cause);
}
#undef VB_ON_EXCEPTION

/* Prepare to execute an instruction */
#ifndef VB_DIRECT_EXECUTE
    #define VB_ON_EXECUTE sim->onExecute
#else
    extern int VB_DIRECT_EXECUTE(VB *, uint32_t, const uint16_t *, int);
    #define VB_ON_EXECUTE VB_DIRECT_EXECUTE
#endif
static int cpuOnExecute(VB *sim) {
    return
        sim->onExecute != NULL &&
        VB_ON_EXECUTE(sim, sim->cpu.pc, sim->cpu.code, sim->cpu.length)
    ;
}
#undef VB_ON_EXECUTE

/* Read a value from the memory bus */
#ifndef VB_DIRECT_READ
    #define VB_ON_READ sim->onRead
#else
    extern int VB_DIRECT_READ(VB *, uint32_t, int, int32_t *, uint32_t *);
    #define VB_ON_READ VB_DIRECT_READ
#endif
static int cpuRead(VB *sim, uint32_t address, int type, int32_t *value) {
    uint32_t cycles = 0; /* TODO: Research this */

    /* Retrieve the value from the simulation state directly */
    busRead(sim, address, type, value);

    /* Check for pseudo-halt */
    if (sim->ph.enabled && phAssess(sim, address, type, *value))
        return 0;

    /* Invoke the callback if available */
    if (
        sim->onRead != NULL &&
        VB_ON_READ(sim, address, type, value, &cycles)
    ) return 1;

    /* Update state */
    sim->cpu.clocks += cpuClocks(cycles);
    return 0;
}
#undef VB_ON_READ

/* Fetch a code unit from the memory bus */
#ifndef VB_DIRECT_FETCH
    #define VB_ON_FETCH sim->onFetch
#else
    extern int VB_DIRECT_FETCH(VB *, int, uint32_t, int32_t *, uint32_t *);
    #define VB_ON_FETCH VB_DIRECT_FETCH
#endif
static int cpuReadFetch(VB *sim, int fetch, uint32_t address, int32_t *value) {
    uint32_t cycles = 0;

    /* Retrieve the value from the simulation state directly */
    busRead(sim, address, VB_U16, value);

    /* Invoke the callback if available */
    if (
        sim->onFetch != NULL &&
        VB_ON_FETCH(sim, fetch, address, value, &cycles)
    ) return 1;

    /* Update state */
    sim->cpu.clocks += cpuClocks(cycles);
    return 0;
}
#undef VB_ON_FETCH

/* Write a value to the memory bus */
#ifndef VB_DIRECT_WRITE
    #define VB_ON_WRITE sim->onWrite
#else
    extern int VB_DIRECT_WRITE(VB *,uint32_t,int,int32_t *,uint32_t *,int *);
    #define VB_ON_WRITE VB_DIRECT_WRITE
#endif
static int cpuWrite(VB *sim, uint32_t address, int type, int32_t value) {
    int      cancel = 0;
    uint32_t cycles = 0; /* TODO: Research this */

    /* Reset pseudo-halt */
    if (sim->ph.enabled)
        sim->ph.step = 0;

    /* Invoke the callback if available */
    if (
        sim->onWrite != NULL &&
        VB_ON_WRITE(sim, address, type, &value, &cycles, &cancel)
    ) return 1;

    /* Write the value to the simulation state directly */
    if (!cancel)
        busWrite(sim, address, type, value, 0);

    /* Update state */
    sim->cpu.clocks += cpuClocks(cycles);
    return 0;
}
#undef VB_ON_WRITE



/**************************** Instruction Helpers ****************************/

/* Retrieve the value of a system register */
static uint32_t cpuGetSystemRegister(VB *sim, int index) {
    switch (index) {
        case VB_ADTRE: return sim->cpu.adtre;
        case VB_CHCW : return sim->cpu.chcw.ice << 1;
        case VB_ECR  : return
            (uint32_t) sim->cpu.ecr.fecc << 16 |
            (uint32_t) sim->cpu.ecr.eicc;
        case VB_EIPC : return sim->cpu.eipc;
        case VB_EIPSW: return sim->cpu.eipsw;
        case VB_FEPC : return sim->cpu.fepc;
        case VB_FEPSW: return sim->cpu.fepsw;
        case VB_PIR  : return 0x00005346;
        case VB_PSW  : return
            (uint32_t) sim->cpu.psw.i   << 16 |
            (uint32_t) sim->cpu.psw.np  << 15 |
            (uint32_t) sim->cpu.psw.ep  << 14 |
            (uint32_t) sim->cpu.psw.ae  << 13 |
            (uint32_t) sim->cpu.psw.id  << 12 |
            (uint32_t) sim->cpu.psw.fro <<  9 |
            (uint32_t) sim->cpu.psw.fiv <<  8 |
            (uint32_t) sim->cpu.psw.fzd <<  7 |
            (uint32_t) sim->cpu.psw.fov <<  6 |
            (uint32_t) sim->cpu.psw.fud <<  5 |
            (uint32_t) sim->cpu.psw.fpr <<  4 |
            (uint32_t) sim->cpu.psw.cy  <<  3 |
            (uint32_t) sim->cpu.psw.ov  <<  2 |
            (uint32_t) sim->cpu.psw.s   <<  1 |
            (uint32_t) sim->cpu.psw.z;
        case VB_TKCW : return 0x000000E0;
        case 29      : return sim->cpu.sr29;
        case 30      : return 0x00000004;
        case 31      : return sim->cpu.sr31;
    }
    return 0x00000000; /* All others */
}

/* Specify a new value for a system register */
static uint32_t cpuSetSystemRegister(VB*sim,
    int index, uint32_t value, int debug) {
    switch (index) {
        case VB_ADTRE: return sim->cpu.adtre = value & 0xFFFFFFFE;
        case VB_CHCW :
            /* TODO: Configure instruction cache */
            sim->cpu.chcw.ice = value >> 1 & 1;
            return value & 0x00000002;
        case VB_ECR  :
            if (debug) {
                sim->cpu.ecr.fecc = value >> 16;
                sim->cpu.ecr.eicc = value;
            }
            return
                (uint32_t) sim->cpu.ecr.fecc << 16 |
                (uint32_t) sim->cpu.ecr.eicc;
        case VB_EIPC : return sim->cpu.eipc  = value & 0xFFFFFFFE;
        case VB_EIPSW: return sim->cpu.eipsw = value & 0x000FF3FF;
        case VB_FEPC : return sim->cpu.fepc  = value & 0xFFFFFFFE;
        case VB_FEPSW: return sim->cpu.fepsw = value & 0x000FF3FF;
        case VB_PIR  : return 0x00005346;
        case VB_PSW  :
            sim->cpu.psw.i   = value >> 16 & 15;
            sim->cpu.psw.np  = value >> 15 & 1;
            sim->cpu.psw.ep  = value >> 14 & 1;
            sim->cpu.psw.ae  = value >> 13 & 1;
            sim->cpu.psw.id  = value >> 12 & 1;
            sim->cpu.psw.fro = value >>  9 & 1;
            sim->cpu.psw.fiv = value >>  8 & 1;
            sim->cpu.psw.fzd = value >>  7 & 1;
            sim->cpu.psw.fov = value >>  6 & 1;
            sim->cpu.psw.fud = value >>  5 & 1;
            sim->cpu.psw.fpr = value >>  4 & 1;
            sim->cpu.psw.cy  = value >>  3 & 1;
            sim->cpu.psw.ov  = value >>  2 & 1;
            sim->cpu.psw.s   = value >>  1 & 1;
            sim->cpu.psw.z   = value       & 1;
            return value & 0x000FF3FF;
        case VB_TKCW : return 0x000000E0;
        case 29      : return sim->cpu.sr29 = value;
        case 30      : return 0x00000004;
        case 31      : return sim->cpu.sr31 = debug ? value :
            *(int32_t *) &value < 0 ? (uint32_t) -*(int32_t *) &value : value;
    }
    return 0x00000000; /* All others */
}

/* Raise an exception */
static void cpuThrow(VB *sim, uint16_t cause) {
    sim->cpu.exception = cause;
    sim->cpu.operation = CPU_EXCEPTION;
    sim->cpu.step      = 0;
}

/* Check for an interrupt request */
static int cpuIRQ(VB *sim) {
    int level;
    if (sim->cpu.psw.id | sim->cpu.psw.ep | sim->cpu.psw.np)
        return 0;
    level = IRQ_LEVELS[sim->cpu.irq];
    if (level < sim->cpu.psw.i)
        return 0;
    cpuThrow(sim, 0xFE00 | level << 4);
    return 1;
}



/****************************** Pipeline Stages ******************************/

/* Handle an exception */
static int cpuException(VB *sim) {
    uint16_t cause = sim->cpu.exception;

    /* Invoke the exception callback */
    if (sim->cpu.step == 0 && cpuOnException(sim, &cause))
        return 1;

    /* Fatal exception */
    if (sim->cpu.psw.np) {
        switch (sim->cpu.step) {
            case 0:
                auxData.value = 0xFFFF0000 | cause;
                /* Fallthrough */
            case 1:
                if (cpuWrite(sim, 0x00000000, VB_S32, auxData.value)) {
                    sim->cpu.step = 1;
                    return 1;
                }
                auxData.value = cpuGetSystemRegister(sim, VB_PSW);
                /* Fallthrough */
            case 2:
                if (cpuWrite(sim, 0x00000004, VB_S32, auxData.value)) {
                    sim->cpu.step = 2;
                    return 1;
                }
                /* Fallthrough */
            case 3:
                if (cpuWrite(sim, 0x00000008, VB_S32, sim->cpu.pc)) {
                    sim->cpu.step = 3;
                    return 1;
                }
        }

        /* Enter fatal halting state */
        sim->cpu.halt      = 1;
        sim->cpu.operation = CPU_FATAL;
        return 0;
    }

    /* Duplexed exception */
    if (sim->cpu.psw.ep) {
        sim->cpu.ecr.fecc = cause;
        sim->cpu.fepsw    = cpuGetSystemRegister(sim, VB_PSW);
        sim->cpu.fepc     = sim->cpu.pc;
        sim->cpu.psw.np   = 1;
        sim->cpu.nextPC   = 0xFFFFFFD0;
    }

    /* Regular exception */
    else {

        /* All exceptions */
        sim->cpu.eipsw = cpuGetSystemRegister(sim, VB_PSW);

        /* Interrupts only */
        if ((cause & 0xFF00) == 0xFE00) {
            sim->cpu.psw.i = (cause >> 4 & 7) + 1;

            /* HALT instruction */
            if (sim->cpu.halt) {
                sim->cpu.halt = 0;
                sim->cpu.pc  += 2;
            }

        }

        /* All exceptions */
        sim->cpu.ecr.eicc = cause;
        sim->cpu.eipc     = sim->cpu.pc;
        sim->cpu.psw.ep   = 1;
        sim->cpu.nextPC   = 0xFFFF0000 |
            ((cause & 0xFFF0) == 0xFF70 ? 0xFF60 : cause & 0xFFF0);
    }

    /* All exceptions */
    sim->cpu.psw.ae    = 0;
    sim->cpu.psw.id    = 1;
    sim->cpu.operation = CPU_FETCH;
    /* TODO: Research clocks */
    return 0;
}

/* Fetch the code bits for the next instruction */
static int cpuFetch(VB *sim) {
    int32_t value;
    switch (sim->cpu.step) {

        case 0:
            sim->cpu.pc = sim->cpu.nextPC;
            if (cpuIRQ(sim))
                return 0;

            /* Fallthrough */
        case 1:

            /* Retrieve the first code unit */
            if (cpuReadFetch(sim, 0, sim->cpu.pc, &value)) {
                sim->cpu.step = 1;
                return 1;
            }

            /* Update state */
            sim->cpu.code[0] = value;
            sim->cpu.length  = INST_LENGTHS[value >> 10 & 63];
            sim->cpu.step    = 3 - sim->cpu.length;

            /* Wait any clocks taken */
            if (sim->cpu.clocks != 0)
                return 0;

            /* Skip fetching a second code unit */
            if (sim->cpu.length == 1)
                goto Step3;

            /* Fallthrough */
        case 2:

            /* Retrieve the second code unit */
            if (cpuReadFetch(sim, 1, sim->cpu.pc + 2, &value))
                return 1;

            /* Update state */
            sim->cpu.code[1] = value;
            sim->cpu.step    = 3;

            /* Wait any clocks taken */
            if (sim->cpu.clocks != 0)
                return 0;

            /* Fallthrough */
        case 3: Step3:

            /* Prepare to execute the instruction */
            if (cpuOnExecute(sim))
                return 1;

            /* Select operation definition */
            sim->cpu.operation = OPDEFS[sim->cpu.code[0] >> 10];
            switch (sim->cpu.operation) {
                case CPU_BITSTRING:
                    sim->cpu.operation =
                        OPDEFS_BITSTRING[sim->cpu.code[0] & 31];
                    break;
                case CPU_FLOATENDO:
                    sim->cpu.operation =
                        OPDEFS_FLOATENDO[sim->cpu.code[1] >> 10];
            }

            /* Update state */
            sim->cpu.step = 0;

    }
    return 0;
}

/* HALT instruction is pending */
static int cpuHalt(VB *sim) {
    /* TODO: Research clocks */
    return !cpuIRQ(sim);
}

/* Pseudo-halt is pending */
static int cpuPHalt(VB *sim) {
    int32_t value; /* Value read from memory */

    /* An interrupt will be accepted */
    if (cpuIRQ(sim)) {
        sim->ph.step = 0;
        return 0;
    }

    /* Monitor value has not changed */
    busRead(sim, sim->ph.address, sim->ph.type, &value);
    if (value == sim->ph.value) {
        sim->cpu.clocks = phUntil(sim);
        return 1;
    }

    /* Release pseudo-halt */
    sim->cpu.operation = CPU_FETCH;
    sim->cpu.step      = 0;
    sim->ph.step       = 0;
    return 0;
}



/**************************** Instruction Helpers ****************************/

/* Parse the immediate 4-bit condition value */
static int32_t cpuGetCond(VB *sim) {
    return sim->cpu.code[0] >> 9 & 15;
}

/* Parse the 9-bit displacement value */
static int32_t cpuGetDisp9(VB *sim) {
    return SignExtend(sim->cpu.code[0], 9);
}

/* Parse the 26-bit displacement value */
static int32_t cpuGetDisp26(VB *sim) {
    return SignExtend((int32_t) sim->cpu.code[0] << 16 | sim->cpu.code[1], 26);
}

/* Parse the immediate 5-bit sign-extended value */
static int32_t cpuGetImm5S(VB *sim) {
    return SignExtend(sim->cpu.code[0], 5);
}

/* Parse the immediate 5-bit zero-filled value */
static int32_t cpuGetImm5U(VB *sim) {
    return sim->cpu.code[0] & 31;
}

/* Parse the immediate 16-bit sign-extended value */
static int32_t cpuGetImm16S(VB *sim) {
    return SignExtend(sim->cpu.code[1], 16);
}

/* Parse the immediate 16-bit zero-filled value */
static int32_t cpuGetImm16U(VB *sim) {
    return sim->cpu.code[1];
}

/* Resolve the operand value for reg1 */
static int32_t cpuGetReg1(VB *sim) {
    return sim->cpu.program[sim->cpu.code[0] & 31];
}

/* Resolve the operand value for reg2 */
static int32_t cpuGetReg2(VB *sim) {
    return sim->cpu.program[sim->cpu.code[0] >> 5 & 31];
}

/* Supply an operand value for reg2 */
static void cpuSetReg2(VB *sim, int32_t value) {
    int reg2 = sim->cpu.code[0] >> 5 & 31;
    if (reg2 != 0)
        sim->cpu.program[reg2] = value;
}

/* Advance to the next instruction */
static void cpuAdvance(VB *sim, uint32_t clocks) {
    sim->cpu.clocks   += clocks;
    sim->cpu.operation = CPU_FETCH;
    sim->cpu.nextPC    = sim->cpu.pc + (sim->cpu.length << 1);
    sim->cpu.step      = 0;
}

/* Interpret floating short bits as word integer bits */
static int32_t cpuFloatToWord(float x) {
    return *(int32_t *) &x;
}

/* Interpret word integer bits as floating short bits */
static float cpuWordToFloat(int32_t x) {
    return *(float *) &x;
}

/* Addition common processing */
static int32_t cpuAdd(VB *sim, int32_t a, int32_t b) {
    int32_t c = a + b;
    sim->cpu.psw.cy = (uint32_t) c < (uint32_t) a;
    sim->cpu.psw.ov = (~(a ^ b) & (a ^ c)) < 0;
    sim->cpu.psw.s  = c <  0;
    sim->cpu.psw.z  = c == 0;
    return c;
}

/* Bitwise common processing */
static int32_t cpuBitwise(VB *sim, int32_t c) {
    sim->cpu.psw.ov = 0;
    sim->cpu.psw.s  = c <  0;
    sim->cpu.psw.z  = c == 0;
    return c;
}

/* Bit string arithmetic common processing */
static int cpuBSArithmetic(VB *sim) {
    uint32_t dest;
    uint32_t src;
    switch (sim->cpu.step) {
        case 0:
            bsDestAddr      &= 0xFFFFFFFC;
            bsDestBit       &= 0x0000001F;
            bsSrcAddr       &= 0xFFFFFFFC;
            bsSrcBit        &= 0x0000001F;
            sim->cpu.clocks += cpuClocks(35); /* TODO: Research */

            /* Nothing to do */
            if (bsLength == 0)
                break;

            /* Fallthrough */
        case 1:

            /* Read the low source word */
            if (cpuRead(sim, bsSrcAddr, VB_S32, (int32_t *) &src)) {
                sim->cpu.step = 1;
                return 1;
            }
            auxBS.src = src;

            /* Fallthrough */
        case 2:

            /* Read the high source word */
            if (cpuRead(sim, bsSrcAddr + 4, VB_S32, (int32_t *) &src)) {
                sim->cpu.step = 2;
                return 1;
            }
            auxBS.src = (uint32_t) auxBS.src | (uint64_t) src << 32;

            /* Fallthrough */
        case 3:

            /* Read the destination word */
            if (cpuRead(sim, bsDestAddr, VB_S32, (int32_t *) &auxBS.dest)) {
                sim->cpu.step = 3;
                return 1;
            }

            /* Perform the ALU operation */
            dest = auxBS.dest;
            src  = bsSrcBit < bsDestBit ?
                auxBS.src << (bsDestBit - bsSrcBit ) :
                auxBS.src >> (bsSrcBit  - bsDestBit)
            ;
            switch (sim->cpu.operation) {
                case CPU_ANDBSU : dest &=  src; break;
                case CPU_ANDNBSU: dest &= ~src; break;
                case CPU_MOVBSU : dest  =  src; break;
                case CPU_NOTBSU : dest  = ~src; break;
                case CPU_ORBSU  : dest |=  src; break;
                case CPU_ORNBSU : dest |= ~src; break;
                case CPU_XORBSU : dest ^=  src; break;
                case CPU_XORNBSU: dest ^= ~src; break;
            }

            /* Change only the bits that are part of the string */
            src = 0xFFFFFFFF << bsDestBit;
            if ((uint32_t) 32 - bsDestBit > (uint32_t) bsLength)
                src &= ~(0xFFFFFFFF << (bsDestBit + bsLength));
            auxBS.dest = (auxBS.dest & ~src) | (dest & src);

            /* Fallthrough */
        case 4:

            /* Write the destination word */
            if (cpuWrite(sim, bsDestAddr, VB_S32, auxBS.dest)) {
                sim->cpu.step = 4;
                return 1;
            }

            /* Select number of bits to advance */
            src = 32 - bsDestBit;
            if (src > (uint32_t) bsLength)
                src = bsLength;

            /* Advance to next output word */
            if (bsSrcBit + src >= 32) {
                bsSrcAddr    += 4;
                auxBS.src   >>= 32;
                sim->cpu.step = 2; /* Read high source word */
            } else sim->cpu.step = 3; /* Read destination word */
            if (bsDestBit + src >= 32)
                bsDestAddr += 4;
            bsSrcBit    = (bsSrcBit  + src) & 31;
            bsDestBit   = (bsDestBit + src) & 31;
            bsLength   -= src;

            /* Update state */
            sim->cpu.clocks += cpuClocks(12); /* TODO: Research */
    }

    /* Exit condition */
    if (bsLength == 0)
        cpuAdvance(sim, 0);
    return 0;
}

/* Bit string search common processing */
static int cpuBSSearch(VB *sim, int bit, int dir) {
    switch (sim->cpu.step) {
        case 0:
            bsSrcAddr       &= 0xFFFFFFFC;
            bsSrcBit        &= 0x0000001F;
            sim->cpu.clocks += cpuClocks(50); /* TODO: Research */
            sim->cpu.psw.z   = 1;
            sim->cpu.step    = 1;

            /* Nothing to do */
            if (bsLength == 0)
                break;

            /* Fallthrough */
        case 1:

            /* Read the source word */
            if (cpuRead(sim, bsSrcAddr, VB_S32, &auxData.value))
                return 1;
            sim->cpu.clocks += cpuClocks(5); /* TODO: Research */

            /* Process all remaining bits in source word */
            do {

                /* Match was found */
                if ((auxData.value >> bsSrcBit & 1) == bit)
                    sim->cpu.psw.z = 0;

                /* Match was not found */
                else bsSkipped++;

                /* Advance to next bit */
                bsSrcBit = (bsSrcBit + dir) & 31;
                bsLength--;
                if (bsSrcBit != (dir == 1 ? 0 : 31))
                    continue;

                /* Advance to next word */
                bsSrcAddr += dir << 2;
                break;
            } while (sim->cpu.psw.z && bsLength != 0);
    }

    /* Exit condition */
    if (!sim->cpu.psw.z || bsLength == 0)
        cpuAdvance(sim, 0);
    return 0;
}

/* Test a condition */
static int cpuCondition(VB *sim, int id) {
    switch (id) {
        case 0: return sim->cpu.psw.ov;
        case 1: return sim->cpu.psw.cy;
        case 2: return sim->cpu.psw.z;
        case 3: return sim->cpu.psw.cy | sim->cpu.psw.z;
        case 4: return sim->cpu.psw.s;
        case 5: return 1;
        case 6: return sim->cpu.psw.ov ^ sim->cpu.psw.s;
        case 7: return (sim->cpu.psw.ov ^ sim->cpu.psw.s) | sim->cpu.psw.z;
    }
    return !cpuCondition(sim, id & 7);
}

/* Floating-point common processing */
static int cpuFloatCommon(VB *sim, double value, int32_t *bits) {
    float floatVal;

    /* Overflow */
    if (value < -FLT_MAX || value > FLT_MAX) {
        /* TODO: CPU does give reg2 some value */
        sim->cpu.psw.fov = 1;
        cpuThrow(sim, 0xFF64);
        return 1;
    }

    /* Underflow */
    if (value != 0 && value > -FLT_MIN && value < FLT_MIN) {
        sim->cpu.psw.fud = 1;
        floatVal         = 0;
        /* TODO: Can this produce negative zero? */
    }

    /* Regular conversion */
    else floatVal = value;

    /* Precision degradation */
    if (floatVal != value)
        sim->cpu.psw.fpr = 1;

    /* Update state */
    *bits = cpuFloatToWord(floatVal);
    sim->cpu.psw.cy = floatVal <  0;
    sim->cpu.psw.ov = 0;
    sim->cpu.psw.s  = floatVal <  0;
    sim->cpu.psw.z  = floatVal == 0;
    return 0;
}

/* Test whether a floating-point register value is a reserved operand */
static int cpuFloatReservedOne(int32_t bits) {
    int32_t e = bits >> 23 & 0xFF;
    return e == 0 ? (bits & 0x7FFFFFFF) != 0 : e == 0xFF;
}

/* Test whether floating-point register values are reserved operands */
static int cpuFloatReserved(VB *sim, int32_t a, int32_t b) {
    if (!(cpuFloatReservedOne(a) || cpuFloatReservedOne(b)))
        return 0;
    sim->cpu.psw.fro = 1;
    cpuThrow(sim, 0xFF60);
    return 1;
}

/* Jump and branch common processing */
static void cpuJump(VB *sim, uint32_t address) {
    sim->cpu.clocks   += cpuClocks(3);
    sim->cpu.operation = CPU_FETCH;
    sim->cpu.nextPC    = address & 0xFFFFFFFE;
    sim->cpu.step      = 0;
    /* TODO: Clear prefetch buffer */
}

/* Arithmetic right shift common processing */
static int32_t cpuShiftArithmetic(VB *sim, int32_t b) {
    int32_t a = cpuGetReg2(sim);
    #ifndef VB_SIGNED_PROPAGATE
        int32_t c = b == 0 ? a : SignExtend(a >> b, 32 - b);
    #else
        int32_t c = a >> b;
    #endif
    sim->cpu.psw.cy = b == 0 ? 0 : a >> (b - 1) & 1;
    sim->cpu.psw.ov = 0;
    sim->cpu.psw.s  = c <  0;
    sim->cpu.psw.z  = c == 0;
    return c;
}

/* Left shift common processing */
static int32_t cpuShiftLeft(VB *sim, int32_t b) {
    int32_t a = cpuGetReg2(sim);
    int32_t c = (uint32_t) a << b;
    sim->cpu.psw.cy = b == 0 ? 0 : a >> (32 - b) & 1;
    sim->cpu.psw.ov = 0;
    sim->cpu.psw.s  = c <  0;
    sim->cpu.psw.z  = c == 0;
    return c;
}

/* Logical right shift common processing */
static int32_t cpuShiftRight(VB *sim, int32_t b) {
    int32_t a = cpuGetReg2(sim);
    int32_t c = b == 0 ? (uint32_t) a :
        a >> b & ~((uint32_t) 0xFFFFFFFF << (32 - b));
    sim->cpu.psw.cy = b == 0 ? 0 : a >> (b - 1) & 1;
    sim->cpu.psw.ov = 0;
    sim->cpu.psw.s  = c <  0;
    sim->cpu.psw.z  = c == 0;
    return c;
}

/* Subtraction common processing */
static int32_t cpuSubtract(VB *sim, int32_t a, int32_t b) {
    int32_t c = a - b;
    sim->cpu.psw.cy = (uint32_t) a < (uint32_t) b;
    sim->cpu.psw.ov = ((a ^ b) & (a ^ c)) < 0;
    sim->cpu.psw.s  = c <  0;
    sim->cpu.psw.z  = c == 0;
    return c;
}

/* Memory load/input instruction */
static int cpuLD_IN(VB *sim, int type) {
    switch (sim->cpu.step) {

        case 0:
            auxData.address = cpuGetReg1(sim) + cpuGetImm16S(sim);
            /* Fallthrough */

        case 1:

            /* Read the value from memory */
            if (cpuRead(sim, auxData.address, type, &auxData.value)) {
                sim->cpu.step = 1;
                return 1;
            }

            /* Update state */
            sim->cpu.clocks += cpuClocks(1);

            /* Wait for clocks taken */
            sim->cpu.step = 2;
            return 0;

        case 2:
            cpuSetReg2(sim, auxData.value);
            cpuAdvance(sim, cpuClocks(4)); /* TODO: Research */
    }
    return 0;
}

/* Memory store/output instruction */
static int cpuST_OUT(VB *sim, int type) {
    switch (sim->cpu.step) {

        case 0:
            auxData.address = cpuGetReg1(sim) + cpuGetImm16S(sim);
            /* Fallthrough */

        case 1:

            /* Write the value to memory */
            if (cpuWrite(sim, auxData.address, type, cpuGetReg2(sim))) {
                sim->cpu.step = 1;
                return 1;
            }

            /* Update state */
            cpuAdvance(sim, cpuClocks(4)); /* TODO: Research */
    }
    return 0;
}



/************************** Instruction Operations ***************************/

/* ADD immediate */
static void cpuADDImm(VB *sim) {
    cpuSetReg2(sim, cpuAdd(sim, cpuGetReg2(sim), cpuGetImm5S(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* ADD register */
static void cpuADDReg(VB *sim) {
    cpuSetReg2(sim, cpuAdd(sim, cpuGetReg2(sim), cpuGetReg1(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* ADDF.S */
static void cpuADDF_S(VB *sim) {
    int32_t a = cpuGetReg2(sim);
    int32_t b = cpuGetReg1(sim);
    double  c; /* Result */
    int32_t d; /* Float bits for result */
    if (cpuFloatReserved(sim, a, b))
        return;
    c = (double) cpuWordToFloat(a) + cpuWordToFloat(b);
    if (cpuFloatCommon(sim, c, &d))
        return;
    cpuSetReg2(sim, d);
    cpuAdvance(sim, cpuClocks(28)); /* TODO: Research */
}

/* ADDI */
static void cpuADDI(VB *sim) {
    cpuSetReg2(sim, cpuAdd(sim, cpuGetReg1(sim), cpuGetImm16S(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* AND */
static void cpuAND(VB *sim) {
    cpuSetReg2(sim, cpuBitwise(sim, cpuGetReg2(sim) & cpuGetReg1(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* ANDBSU */
static int cpuANDBSU(VB *sim) {
    return cpuBSArithmetic(sim);
}

/* ANDI */
static void cpuANDI(VB *sim) {
    cpuSetReg2(sim, cpuBitwise(sim, cpuGetReg1(sim) & cpuGetImm16U(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* ANDNBSU */
static int cpuANDNBSU(VB *sim) {
    return cpuBSArithmetic(sim);
}

/* BCOND */
static void cpuBCOND(VB *sim) {
    if (cpuCondition(sim, cpuGetCond(sim)))
        cpuJump(sim, sim->cpu.pc + cpuGetDisp9(sim));
    else cpuAdvance(sim, cpuClocks(1));
}

/* CAXI */
static int cpuCAXI(VB *sim) {
    switch (sim->cpu.step) {

        case 0:
            auxData.address = cpuGetReg1(sim) + cpuGetImm16S(sim);
            /* Fallthrough */

        case 1:

            /* Read the compare value */
            if (cpuRead(sim, auxData.address, VB_S32, &auxData.value)) {
                sim->cpu.step = 1;
                return 1;
            }

            /* Update state */
            sim->cpu.clocks += cpuClocks(1); /* TODO: Research */
            cpuSubtract(sim, cpuGetReg2(sim), auxData.value);
            if (sim->cpu.psw.z)
                auxData.value = sim->cpu.program[30];

            /* Wait for clocks taken */
            sim->cpu.step = 2;
            return 0;

        case 2:

            /* Write the exchange value */
            if (cpuWrite(sim, auxData.address, VB_S32, auxData.value))
                return 1;

            /* Update state */
            cpuSetReg2(sim, auxData.value);
            cpuAdvance(sim, cpuClocks(25)); /* TODO: Research */
    }
    return 0;
}

/* CLI */
static void cpuCLI(VB *sim) {
    sim->cpu.psw.id = 0;
    cpuAdvance(sim, cpuClocks(12));
}

/* CMP immediate */
static void cpuCMPImm(VB *sim) {
    cpuSubtract(sim, cpuGetReg2(sim), cpuGetImm5S(sim));
    cpuAdvance(sim, cpuClocks(1));
}

/* CMP register */
static void cpuCMPReg(VB *sim) {
    cpuSubtract(sim, cpuGetReg2(sim), cpuGetReg1(sim));
    cpuAdvance(sim, cpuClocks(1));
}

/* CMPF.S */
static void cpuCMPF_S(VB *sim) {
    int32_t a = cpuGetReg2(sim);
    int32_t b = cpuGetReg1(sim);
    double  c; /* Result */
    int32_t d; /* Float bits for result */
    if (cpuFloatReserved(sim, a, b))
        return;
    c = (double) cpuWordToFloat(a) - cpuWordToFloat(b);
    if (cpuFloatCommon(sim, c, &d))
        return;
    cpuAdvance(sim, cpuClocks(10)); /* TODO: Research */
}

/* CVT.SW */
static void cpuCVT_SW(VB *sim) {
    double  f;
    int32_t w = cpuGetReg1(sim);
    if (cpuFloatReservedOne(w)) {
        cpuThrow(sim, 0xFF60);
        return;
    }
    f = cpuWordToFloat(w);
    if (f <= -2147483648.5 || f >= 2147483647.5) {
        sim->cpu.psw.fiv = 1;
        cpuThrow(sim, 0xFF70);
        return;
    }
    w = f < 0 ? f - 0.5 : f + 0.5;
    cpuSetReg2(sim, w);
    if (w != f)
        sim->cpu.psw.fpr = 1;
    sim->cpu.psw.ov = 0;
    sim->cpu.psw.s  = w <  0;
    sim->cpu.psw.z  = w == 0;
    cpuAdvance(sim, cpuClocks(14)); /* TODO: Research */
}

/* CVT.WS */
static void cpuCVT_WS(VB *sim) {
    int32_t w = cpuGetReg1(sim);
    float   f = w;
    if (f != w)
        sim->cpu.psw.fpr = 1;
    w = cpuFloatToWord(f);
    cpuSetReg2(sim, w);
    sim->cpu.psw.cy = w <  0;
    sim->cpu.psw.ov = 0;
    sim->cpu.psw.s  = w <  0;
    sim->cpu.psw.z  = w == 0;
    cpuAdvance(sim, cpuClocks(16)); /* TODO: Research */
}

/* DIV */
static void cpuDIV(VB *sim) {
    int32_t a;  /* Dividend */
    int32_t b;  /* Divisor */
    int32_t c;  /* Remainder */
    int32_t d;  /* Quotient */
    #ifdef VB_DIV_GENERIC
        int sa; /* Sign of a */
        int sb; /* Sign of b */
    #endif

    /* Zero division */
    b = cpuGetReg1(sim);
    if (b == 0) {
        cpuThrow(sim, 0xFF80);
        return;
    }

    /* Compute results */
    a = cpuGetReg2(sim);
    sim->cpu.psw.ov = (uint32_t) a == (uint32_t) 0x80000000 && b == -1;
    #ifndef VB_DIV_GENERIC
        c = a % b;
        d = a / b;
    #else

        /* Do not divide */
        if (sim->cpu.psw.ov) {
            c = 0;
            d = a;
        }

        /* Perform division */
        else {

            /* Take signs and absolute values of operands */
            sa = a < 0;
            if (sa)
                a = -a;
            sb = b < 0;
            if (sb)
                b = -b;

            /* Compute results */
            c = (uint32_t) a % (uint32_t) b;
            if (sa)
                c = -c;
            d = (uint32_t) a / (uint32_t) b;
            if (sa ^ sb)
                d = -d;
        }

    #endif
    sim->cpu.program[30] = c;
    cpuSetReg2(sim, d);

    /* Flags */
    sim->cpu.psw.s  = d <  0;
    sim->cpu.psw.z  = d == 0;
    cpuAdvance(sim, cpuClocks(38));
}

/* DIVF.S */
static void cpuDIVF_S(VB *sim) {
    int32_t a = cpuGetReg2(sim);
    int32_t b = cpuGetReg1(sim);
    double  c; /* Result */
    int32_t d; /* Float bits for result */
    if (cpuFloatReserved(sim, a, b))
        return;
    if ((b & 0x7FFFFFFF) == 0) {
        cpuThrow(sim, (a & 0x7FFFFFFF) == 0 ? 0xFF70 : 0xFF68);
        return;
    }
    c = (double) cpuWordToFloat(a) / cpuWordToFloat(b);
    if (cpuFloatCommon(sim, c, &d))
        return;
    cpuSetReg2(sim, d);
    cpuAdvance(sim, cpuClocks(44));
}

/* DIVU */
static void cpuDIVU(VB *sim) {
    uint32_t a;  /* Dividend */
    uint32_t b;  /* Divisor */
    int32_t  c;  /* Remainder */
    int32_t  d;  /* Quotient */

    /* Zero division */
    b = cpuGetReg1(sim);
    if (b == 0) {
        cpuThrow(sim, 0xFF80);
        return;
    }

    /* Compute results */
    a = cpuGetReg2(sim);
    c = a % b;
    d = a / b;
    sim->cpu.program[30] = c;
    cpuSetReg2(sim, d);

    /* Flags */
    sim->cpu.psw.ov = 0;
    sim->cpu.psw.s  = d <  0;
    sim->cpu.psw.z  = d == 0;
    cpuAdvance(sim, cpuClocks(36));
}

/* HALT */
static void cpuHALT(VB *sim) {
    sim->cpu.halt      = 1;
    sim->cpu.operation = CPU_HALTING;
    /* TODO: Research clocks */
}

/* IN.B */
static int cpuIN_B(VB *sim) {
    return cpuLD_IN(sim, VB_U8);
}

/* IN.H */
static int cpuIN_H(VB *sim) {
    return cpuLD_IN(sim, VB_U16);
}

/* IN.W */
static int cpuIN_W(VB *sim) {
    return cpuLD_IN(sim, VB_S32);
}

/* JAL */
static void cpuJAL(VB *sim) {
    sim->cpu.program[31] = sim->cpu.pc + 4;
    cpuJump(sim, sim->cpu.pc + cpuGetDisp26(sim));
}

/* JMP */
static void cpuJMP(VB *sim) {
    cpuJump(sim, cpuGetReg1(sim));
}

/* JR */
static void cpuJR(VB *sim) {
    cpuJump(sim, sim->cpu.pc + cpuGetDisp26(sim));
}

/* LD.B */
static int cpuLD_B(VB *sim) {
    return cpuLD_IN(sim, VB_S8);
}

/* LD.H */
static int cpuLD_H(VB *sim) {
    return cpuLD_IN(sim, VB_S16);
}

/* LD.W */
static int cpuLD_W(VB *sim) {
    return cpuLD_IN(sim, VB_S32);
}

/* LDSR */
static void cpuLDSR(VB *sim) {
    cpuSetSystemRegister(sim, cpuGetImm5U(sim), cpuGetReg2(sim), 0);
    cpuAdvance(sim, cpuClocks(8));
}

/* MOV immediate */
static void cpuMOVImm(VB *sim) {
    cpuSetReg2(sim, cpuGetImm5S(sim));
    cpuAdvance(sim, cpuClocks(1));
}

/* MOV register */
static void cpuMOVReg(VB *sim) {
    cpuSetReg2(sim, cpuGetReg1(sim));
    cpuAdvance(sim, cpuClocks(1));
}

/* MOVBSU */
static int cpuMOVBSU(VB *sim) {
    return cpuBSArithmetic(sim);
}

/* MOVEA */
static void cpuMOVEA(VB *sim) {
    cpuSetReg2(sim, cpuGetReg1(sim) + cpuGetImm16S(sim));
    cpuAdvance(sim, cpuClocks(1));
}

/* MOVHI */
static void cpuMOVHI(VB *sim) {
    cpuSetReg2(sim, cpuGetReg1(sim) + (cpuGetImm16U(sim) << 16));
    cpuAdvance(sim, cpuClocks(1));
}

/* MPYHW */
static void cpuMPYHW(VB *sim) {
    cpuSetReg2(sim, cpuGetReg2(sim) * SignExtend(cpuGetReg1(sim), 17));
    cpuAdvance(sim, cpuClocks(9));
}

/* MUL */
static void cpuMUL(VB *sim) {
    int64_t a = cpuGetReg2(sim);
    int64_t b = cpuGetReg1(sim);
    int64_t c = a * b;
    int32_t d = c;
    sim->cpu.program[30] = c >> 32;
    cpuSetReg2(sim, d);
    sim->cpu.psw.ov = d != c;
    sim->cpu.psw.s  = d <  0;
    sim->cpu.psw.z  = d == 0;
    cpuAdvance(sim, 13);
}

/* MULF.S */
static void cpuMULF_S(VB *sim) {
    int32_t a = cpuGetReg2(sim);
    int32_t b = cpuGetReg1(sim);
    double  c; /* Result */
    int32_t d; /* Float bits for result */
    if (cpuFloatReserved(sim, a, b))
        return;
    c = (double) cpuWordToFloat(a) * cpuWordToFloat(b);
    if (cpuFloatCommon(sim, c, &d))
        return;
    cpuSetReg2(sim, d);
    cpuAdvance(sim, cpuClocks(30)); /* TODO: Research */
}

/* MULU */
static void cpuMULU(VB *sim) {
    uint64_t a = (uint32_t) cpuGetReg2(sim);
    uint64_t b = (uint32_t) cpuGetReg1(sim);
    uint64_t c = a * b;
    uint32_t d = c;
    sim->cpu.program[30] = c >> 32;
    cpuSetReg2(sim, d);
    sim->cpu.psw.ov = d != c;
    sim->cpu.psw.s  = (int32_t) d < 0;
    sim->cpu.psw.z  = d == 0;
    cpuAdvance(sim, 13);
}

/* NOT */
static void cpuNOT(VB *sim) {
    cpuSetReg2(sim, cpuBitwise(sim, ~cpuGetReg1(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* NOTBSU */
static int cpuNOTBSU(VB *sim) {
    return cpuBSArithmetic(sim);
}

/* OR */
static void cpuOR(VB *sim) {
    cpuSetReg2(sim, cpuBitwise(sim, cpuGetReg2(sim) | cpuGetReg1(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* ORBSU */
static int cpuORBSU(VB *sim) {
    return cpuBSArithmetic(sim);
}

/* ORI */
static void cpuORI(VB *sim) {
    cpuSetReg2(sim, cpuBitwise(sim, cpuGetReg1(sim) | cpuGetImm16U(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* ORNBSU */
static int cpuORNBSU(VB *sim) {
    return cpuBSArithmetic(sim);
}

/* OUT.B */
static int cpuOUT_B(VB *sim) {
    return cpuST_OUT(sim, VB_U8);
}

/* OUT.H */
static int cpuOUT_H(VB *sim) {
    return cpuST_OUT(sim, VB_U16);
}

/* OUT.W */
static int cpuOUT_W(VB *sim) {
    return cpuST_OUT(sim, VB_S32);
}

/* RETI */
static void cpuRETI(VB *sim) {
    if (sim->cpu.psw.np) {
        sim->cpu.nextPC = sim->cpu.fepc;
        cpuSetSystemRegister(sim, VB_PSW, sim->cpu.fepsw, 0);
    } else {
        sim->cpu.nextPC = sim->cpu.eipc;
        cpuSetSystemRegister(sim, VB_PSW, sim->cpu.eipsw, 0);
    }
    sim->cpu.clocks   += cpuClocks(10);
    sim->cpu.operation = CPU_FETCH;
}

/* REV */
static void cpuREV(VB *sim) {
    uint32_t x = cpuGetReg1(sim);
    x = (x << 16 & 0xFFFF0000) | (x >> 16 & 0x0000FFFF);
    x = (x <<  8 & 0xFF00FF00) | (x >>  8 & 0x00FF00FF);
    x = (x <<  4 & 0xF0F0F0F0) | (x >>  4 & 0x0F0F0F0F);
    x = (x <<  2 & 0xCCCCCCCC) | (x >>  2 & 0x33333333);
    x = (x <<  1 & 0xAAAAAAAA) | (x >>  1 & 0x55555555);
    cpuSetReg2(sim, x);
    cpuAdvance(sim, cpuClocks(22));
}

/* SAR immediate */
static void cpuSARImm(VB *sim) {
    cpuSetReg2(sim, cpuShiftArithmetic(sim, cpuGetImm5U(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* SAR register */
static void cpuSARReg(VB *sim) {
    cpuSetReg2(sim, cpuShiftArithmetic(sim, cpuGetReg1(sim) & 31));
    cpuAdvance(sim, cpuClocks(1));
}

/* SCH0BSD */
static int cpuSCH0BSD(VB *sim) {
    return cpuBSSearch(sim, 0, -1);
}

/* SCH0BSU */
static int cpuSCH0BSU(VB *sim) {
    return cpuBSSearch(sim, 0, +1);
}

/* SCH1BSD */
static int cpuSCH1BSD(VB *sim) {
    return cpuBSSearch(sim, 1, -1);
}

/* SCH1BSU */
static int cpuSCH1BSU(VB *sim) {
    return cpuBSSearch(sim, 1, +1);
}

/* SEI */
static void cpuSEI(VB *sim) {
    sim->cpu.psw.id = 1;
    cpuAdvance(sim, cpuClocks(12));
}

/* SETF */
static void cpuSETF(VB *sim) {
    cpuSetReg2(sim, cpuCondition(sim, cpuGetImm5U(sim) & 15));
    cpuAdvance(sim, cpuClocks(1));
}

/* SHL immediate */
static void cpuSHLImm(VB *sim) {
    cpuSetReg2(sim, cpuShiftLeft(sim, cpuGetImm5U(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* SHL register */
static void cpuSHLReg(VB *sim) {
    cpuSetReg2(sim, cpuShiftLeft(sim, cpuGetReg1(sim) & 31));
    cpuAdvance(sim, cpuClocks(1));
}

/* SHR immediate */
static void cpuSHRImm(VB *sim) {
    cpuSetReg2(sim, cpuShiftRight(sim, cpuGetImm5U(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* SHR register */
static void cpuSHRReg(VB *sim) {
    cpuSetReg2(sim, cpuShiftRight(sim, cpuGetReg1(sim) & 31));
    cpuAdvance(sim, cpuClocks(1));
}

/* ST.B */
static int cpuST_B(VB *sim) {
    return cpuST_OUT(sim, VB_S8);
}

/* ST.H */
static int cpuST_H(VB *sim) {
    return cpuST_OUT(sim, VB_S16);
}

/* ST.W */
static int cpuST_W(VB *sim) {
    return cpuST_OUT(sim, VB_S32);
}

/* STSR */
static void cpuSTSR(VB *sim) {
    cpuSetReg2(sim, cpuGetSystemRegister(sim, cpuGetImm5U(sim)));
    cpuAdvance(sim, cpuClocks(8));
}

/* SUB */
static void cpuSUB(VB *sim) {
    cpuSetReg2(sim, cpuSubtract(sim, cpuGetReg2(sim), cpuGetReg1(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* SUBF.S */
static void cpuSUBF_S(VB *sim) {
    int32_t a = cpuGetReg2(sim);
    int32_t b = cpuGetReg1(sim);
    double  c; /* Result */
    int32_t d; /* Float bits for result */
    if (cpuFloatReserved(sim, a, b))
        return;
    c = (double) cpuWordToFloat(a) - cpuWordToFloat(b);
    if (cpuFloatCommon(sim, c, &d))
        return;
    cpuSetReg2(sim, d);
    cpuAdvance(sim, cpuClocks(28)); /* TODO: Research */
}

/* TRAP */
static void cpuTRAP(VB *sim) {
    sim->cpu.clocks += cpuClocks(15);
    sim->cpu.pc     += 2;
    cpuThrow(sim, 0xFFA0 + cpuGetImm5U(sim));
}

/* TRNC.SW */
static void cpuTRNC_SW(VB *sim) {
    double  f;
    int32_t w = cpuGetReg1(sim);
    if (cpuFloatReservedOne(w)) {
        cpuThrow(sim, 0xFF60);
        return;
    }
    f = cpuWordToFloat(w);
    if (f <= -2147483649.0 || f >= 2147483648.0) {
        sim->cpu.psw.fiv = 1;
        cpuThrow(sim, 0xFF70);
        return;
    }
    w = f;
    cpuSetReg2(sim, w);
    if (w != f)
        sim->cpu.psw.fpr = 1;
    sim->cpu.psw.ov = 0;
    sim->cpu.psw.s  = w <  0;
    sim->cpu.psw.z  = w == 0;
    cpuAdvance(sim, cpuClocks(14)); /* TODO: Research */
}

/* XB */
static void cpuXB(VB *sim) {
    uint32_t x = cpuGetReg2(sim);
    x = (x & 0xFFFF0000) | (x << 8 & 0x0000FF00) | (x >> 8 & 0x000000FF);
    cpuSetReg2(sim, x);
    cpuAdvance(sim, cpuClocks(6));
}

/* XH */
static void cpuXH(VB *sim) {
    uint32_t x = cpuGetReg2(sim);
    x = (x << 16 & 0xFFFF0000) | (x >> 16 & 0x0000FFFF);
    cpuSetReg2(sim, x);
    cpuAdvance(sim, cpuClocks(1));
}

/* XOR */
static void cpuXOR(VB *sim) {
    cpuSetReg2(sim, cpuBitwise(sim, cpuGetReg2(sim) ^ cpuGetReg1(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* XORBSU */
static int cpuXORBSU(VB *sim) {
    return cpuBSArithmetic(sim);
}

/* XORI */
static void cpuXORI(VB *sim) {
    cpuSetReg2(sim, cpuBitwise(sim, cpuGetReg1(sim) ^ cpuGetImm16U(sim)));
    cpuAdvance(sim, cpuClocks(1));
}

/* XORNBSU */
static int cpuXORNBSU(VB *sim) {
    return cpuBSArithmetic(sim);
}



/***************************** Library Functions *****************************/

/* Process component */
static int cpuEmulate(VB *sim, uint32_t clocks) {
    int brk = 0;

    /* Process until there are clocks to wait */
    for (;;) {

        /* The next event is after the time remaining */
        if (sim->cpu.clocks > clocks) {
            sim->cpu.clocks -= clocks;
            return 0;
        }

        /* Advance forward the CPU's number of clocks */
        clocks         -= sim->cpu.clocks;
        sim->cpu.clocks = 0;

        /* Processing by operation ID */
        switch (sim->cpu.operation) {
            case CPU_EXCEPTION: brk = cpuException(sim);     break;
            case CPU_FATAL    : return 0;
            case CPU_FETCH    : brk = cpuFetch    (sim);     break;
            case CPU_HALTING  : if (cpuHalt (sim)) return 0; break;
            case CPU_PHALT    : if (cpuPHalt(sim)) return 0; break;

            case CPU_ADD_IMM:       cpuADDImm (sim); break;
            case CPU_ADD_REG:       cpuADDReg (sim); break;
            case CPU_ADDF_S :       cpuADDF_S (sim); break;
            case CPU_ADDI   :       cpuADDI   (sim); break;
            case CPU_AND    :       cpuAND    (sim); break;
            case CPU_ANDBSU :       cpuANDBSU (sim); break;
            case CPU_ANDI   :       cpuANDI   (sim); break;
            case CPU_ANDNBSU:       cpuANDNBSU(sim); break;
            case CPU_BCOND  :       cpuBCOND  (sim); break;
            case CPU_CAXI   : brk = cpuCAXI   (sim); break;
            case CPU_CLI    :       cpuCLI    (sim); break;
            case CPU_CMP_IMM:       cpuCMPImm (sim); break;
            case CPU_CMP_REG:       cpuCMPReg (sim); break;
            case CPU_CMPF_S :       cpuCMPF_S (sim); break;
            case CPU_CVT_SW :       cpuCVT_SW (sim); break;
            case CPU_CVT_WS :       cpuCVT_WS (sim); break;
            case CPU_DIV    :       cpuDIV    (sim); break;
            case CPU_DIVF_S :       cpuDIVF_S (sim); break;
            case CPU_DIVU   :       cpuDIVU   (sim); break;
            case CPU_HALT   :       cpuHALT   (sim); break;
            case CPU_IN_B   : brk = cpuIN_B   (sim); break;
            case CPU_IN_H   : brk = cpuIN_H   (sim); break;
            case CPU_IN_W   : brk = cpuIN_W   (sim); break;
            case CPU_JAL    :       cpuJAL    (sim); break;
            case CPU_JMP    :       cpuJMP    (sim); break;
            case CPU_JR     :       cpuJR     (sim); break;
            case CPU_LD_B   : brk = cpuLD_B   (sim); break;
            case CPU_LD_H   : brk = cpuLD_H   (sim); break;
            case CPU_LD_W   : brk = cpuLD_W   (sim); break;
            case CPU_LDSR   :       cpuLDSR   (sim); break;
            case CPU_MOV_IMM:       cpuMOVImm (sim); break;
            case CPU_MOV_REG:       cpuMOVReg (sim); break;
            case CPU_MOVBSU :       cpuMOVBSU (sim); break;
            case CPU_MOVEA  :       cpuMOVEA  (sim); break;
            case CPU_MOVHI  :       cpuMOVHI  (sim); break;
            case CPU_MPYHW  :       cpuMPYHW  (sim); break;
            case CPU_MUL    :       cpuMUL    (sim); break;
            case CPU_MULF_S :       cpuMULF_S (sim); break;
            case CPU_MULU   :       cpuMULU   (sim); break;
            case CPU_NOT    :       cpuNOT    (sim); break;
            case CPU_NOTBSU :       cpuNOTBSU (sim); break;
            case CPU_OR     :       cpuOR     (sim); break;
            case CPU_ORBSU  :       cpuORBSU  (sim); break;
            case CPU_ORI    :       cpuORI    (sim); break;
            case CPU_ORNBSU :       cpuORNBSU (sim); break;
            case CPU_OUT_B  : brk = cpuOUT_B  (sim); break;
            case CPU_OUT_H  : brk = cpuOUT_H  (sim); break;
            case CPU_OUT_W  : brk = cpuOUT_W  (sim); break;
            case CPU_RETI   :       cpuRETI   (sim); break;
            case CPU_REV    :       cpuREV    (sim); break;
            case CPU_SAR_IMM:       cpuSARImm (sim); break;
            case CPU_SAR_REG:       cpuSARReg (sim); break;
            case CPU_SCH0BSD:       cpuSCH0BSD(sim); break;
            case CPU_SCH0BSU:       cpuSCH0BSU(sim); break;
            case CPU_SCH1BSD:       cpuSCH1BSD(sim); break;
            case CPU_SCH1BSU:       cpuSCH1BSU(sim); break;
            case CPU_SEI    :       cpuSEI    (sim); break;
            case CPU_SETF   :       cpuSETF   (sim); break;
            case CPU_SHL_IMM:       cpuSHLImm (sim); break;
            case CPU_SHL_REG:       cpuSHLReg (sim); break;
            case CPU_SHR_IMM:       cpuSHRImm (sim); break;
            case CPU_SHR_REG:       cpuSHRReg (sim); break;
            case CPU_ST_B   : brk = cpuST_B   (sim); break;
            case CPU_ST_H   : brk = cpuST_H   (sim); break;
            case CPU_ST_W   : brk = cpuST_W   (sim); break;
            case CPU_STSR   :       cpuSTSR   (sim); break;
            case CPU_SUB    :       cpuSUB    (sim); break;
            case CPU_SUBF_S :       cpuSUBF_S (sim); break;
            case CPU_TRAP   :       cpuTRAP   (sim); break;
            case CPU_TRNC_SW:       cpuTRNC_SW(sim); break;
            case CPU_XB     :       cpuXB     (sim); break;
            case CPU_XH     :       cpuXH     (sim); break;
            case CPU_XOR    :       cpuXOR    (sim); break;
            case CPU_XORBSU :       cpuXORBSU (sim); break;
            case CPU_XORI   :       cpuXORI   (sim); break;
            case CPU_XORNBSU:       cpuXORNBSU(sim); break;

            default: return -1; /* TODO: Temporary for debugging */
        }

        /* A callback requested a break */
        if (brk)
            return 1;
    }

    return 0;
}

/* Simulate a hardware reset */
static void cpuReset(VB *sim) {
    uint32_t x; /* Iterator */

    /* Normal */
    sim->cpu.exception = 0;
    sim->cpu.halt      = 0;
    sim->cpu.irq       = 0;
    sim->cpu.pc        = 0xFFFFFFF0;
    cpuSetSystemRegister(sim, VB_ECR, 0x0000FFF0, 1);
    cpuSetSystemRegister(sim, VB_PSW, 0x00008000, 1);

    /* Extra (the hardware does not do this) */
    sim->cpu.adtre = 0x00000000;
    sim->cpu.eipc  = 0x00000000;
    sim->cpu.eipsw = 0x00000000;
    sim->cpu.fepc  = 0x00000000;
    sim->cpu.fepsw = 0x00000000;
    sim->cpu.sr29  = 0x00000000;
    sim->cpu.sr31  = 0x00000000;
    cpuSetSystemRegister(sim, VB_CHCW, 0x00000000, 1);
    for (x = 0; x < 32; x++)
        sim->cpu.program[x] = 0x00000000;

    /* Other */
    sim->cpu.clocks    = 0;
    sim->cpu.nextPC    = 0xFFFFFFF0;
    sim->cpu.operation = CPU_FETCH;
    sim->cpu.step      = 0;
}

/* Determine how many clocks are guaranteed to process */
static uint32_t cpuUntil(VB *sim, uint32_t clocks) {

    /* Pseudo-halting */
    if (sim->cpu.operation == CPU_PHALT) {
        if (sim->cpu.clocks == 0)
            return clocks;
    }

    /* Halting */
    else if (sim->cpu.halt) {
        return
            sim->cpu.operation == CPU_HALTING                      &&
            !(sim->cpu.psw.id | sim->cpu.psw.ep | sim->cpu.psw.np) &&
            IRQ_LEVELS[sim->cpu.irq] >= sim->cpu.psw.i
        ? 0 : clocks;
    }

    /* Not halting */
    return sim->cpu.clocks > clocks ? clocks : sim->cpu.clocks;
}

#endif /* VBAPI */
