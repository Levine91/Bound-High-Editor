/* This file is included into vb.c and cannot be compiled on its own. */
#ifdef VBAPI



/******************************** Lookup Data ********************************/

/* Memory access address masks by data type */
static const uint32_t TYPE_MASKS[] = {
    0x07FFFFFF, /* S8  */
    0x07FFFFFF, /* U8  */
    0x07FFFFFE, /* S16 */
    0x07FFFFFE, /* U16 */
    0x07FFFFFC  /* S32 */
};



/**************************** Forward references *****************************/

static void vipRead (VB *, uint32_t, int, int32_t *);
static void vipWrite(VB *, uint32_t, int, int32_t, int);
static void vsuWrite(VB *, uint32_t, int, int32_t, int);



/*************************** Sub-Module Functions ****************************/

/* Read a typed value from a buffer in host memory */
static int32_t busReadBuffer(uint8_t *data, int type) {

    /* Processing by data type */
    switch (type) {

        /* Generic implementation */
        #ifndef VB_LITTLE_ENDIAN
            case VB_S8 : return ((int8_t *)data)[0];
            case VB_U8 : return            data [0];
            case VB_S16: return (int32_t) ((int8_t *)data)[1] << 8 | data[0];
            case VB_U16: return (int32_t)            data [1] << 8 | data[0];
            case VB_S32: return
                (int32_t) data[3] << 24 | (int32_t) data[2] << 16 |
                (int32_t) data[1] <<  8 |           data[0];

        /* Little-endian host */
        #else
            case VB_S8 : return *(int8_t   *) data;
            case VB_U8 : return *             data;
            case VB_S16: return *(int16_t  *) data;
            case VB_U16: return *(uint16_t *) data;
            case VB_S32: return *(int32_t  *) data;
        #endif

    }

    return 0; /* Unreachable */
}

/* Write a typed value to a buffer in host memory */
static void busWriteBuffer(uint8_t *data, int type, int32_t value) {

    /* Processing by data type */
    switch (type) {

        /* Generic implementation */
        #ifndef VB_LITTLE_ENDIAN
            case VB_S32: data[3] = value >> 24;
                         data[2] = value >> 16; /* Fallthrough */
            case VB_S16:                        /* Fallthrough */
            case VB_U16: data[1] = value >>  8; /* Fallthrough */
            case VB_S8 :                        /* Fallthrough */
            case VB_U8 : data[0] = value;

        /* Little-endian host */
        #else
            case VB_S8 : /* Fallthrough */
            case VB_U8 : *             data = value; return;
            case VB_S16: /* Fallthrough */
            case VB_U16: *(uint16_t *) data = value; return;
            case VB_S32: *(int32_t  *) data = value; return;
        #endif

    }

}

/* Read a value from miscellaneous hardware I/O */
static int32_t busReadMisc(VB *sim, uint8_t address, int type) {

    /* Unmapped */
    switch (type) {
        case VB_S8 : case VB_U8 : if (address & 3) return 0; break;
        case VB_S16: case VB_U16: if (address & 2) return 0;
    }

    /* Access by register */
    switch (address >> 2 & 15) {
        case 0x00>>2: return extReadCCR(sim);                      /* CCR */
        case 0x04>>2: return extReadCCSR(sim);                     /* CCSR */
        case 0x08>>2: return sim->ext.cdtr;                        /* CDTR */
        case 0x0C>>2: return sim->ext.cdrr;                        /* CDRR */
        case 0x10>>2: return sim->pad.sdlr;                        /* SDLR */
        case 0x14>>2: return sim->pad.sdhr;                        /* SDHR */
        case 0x18>>2: return sim->tmr.counter      & 0xFF;         /* TLR */
        case 0x1C>>2: return sim->tmr.counter >> 8 & 0xFF;         /* THR */
        case 0x20>>2: return tmrReadControl(sim);                  /* TCR */
        case 0x24>>2: return sim->wcr.exp1w << 1 | sim->wcr.rom1w; /* WCR */
        case 0x28>>2: return padReadControl(sim);                  /* SCR */
    }

    /* Unmapped */
    return 0;
}

/* Write a value to miscellaneous hardware I/O */
static void busWriteMisc(VB *sim, uint8_t address, int type, int32_t value) {

    /* Unmapped */
    switch (type) {
        case VB_S8 : case VB_U8 : if (address & 3) return; break;
        case VB_S16: case VB_U16: if (address & 2) return;
    }

    /* Access by register */
    switch (address >> 2 & 15) {
        case 0x00>>2: extWriteCCR (sim, value);    break; /* CCR */
        case 0x04>>2: extWriteCCSR(sim, value);    break; /* CCSR */
        case 0x08>>2: sim->ext.cdtr = value;       break; /* CDTR */
        case 0x18>>2: tmrWriteLow    (sim, value); break; /* TLR */
        case 0x1C>>2: tmrWriteHigh   (sim, value); break; /* THR */
        case 0x20>>2: tmrWriteControl(sim, value); break; /* TCR */
        case 0x28>>2: padWriteControl(sim, value); break; /* SCR */
        case 0x24>>2:                                     /* WCR */
            sim->wcr.exp1w = value >> 1 & 1;
            sim->wcr.rom1w = value      & 1;
    }

}



/***************************** Library Functions *****************************/

/* Read a typed value from the simulation bus */
static void busRead(VB *sim, uint32_t address, int type, int32_t *value) {

    /* Working variables */
    address &= TYPE_MASKS[type];
    *value   = 0;

    /* Process by address range */
    switch (address >> 24) {

        case 0: /* VIP */
            vipRead(sim, address, type, value);
            break;

        case 1: break; /* VSU */

        case 2: /* Misc. I/O */
            *value = busReadMisc(sim, address, type);
            if (type == VB_S8)
                *value = (int8_t) *value;
            break;

        case 3: break; /* Unmapped */
        case 4: break; /* Game Pak expansion */

        case 5: /* WRAM */
            *value = busReadBuffer(&sim->wram[address & 0x0000FFFF], type);
            break;

        case 6: /* Game Pak RAM */
            if (sim->cart.ram != NULL) {
                *value = busReadBuffer(
                    &sim->cart.ram[address & sim->cart.ramMask], type);
            }
            break;

        case 7: /* Game Pak ROM */
            if (sim->cart.rom != NULL) {
                *value = busReadBuffer(
                    &sim->cart.rom[address & sim->cart.romMask], type);
            }
            break;

    }

}

/* Write a typed value to the simulation bus */
static void busWrite(VB*sim,uint32_t address,int type,int32_t value,int debug){

    /* Working variables */
    address &= TYPE_MASKS[type];

    /* Process by address range */
    switch (address >> 24) {

        case 0: /* VIP */
            vipWrite(sim, address, type, value, debug);
            break;

        case 1: /* VSU */
            vsuWrite(sim, address, type, value, debug);
            break;

        case 2: /* Misc. I/O */
            busWriteMisc(sim, address, type, value);
            break;

        case 3: break; /* Unmapped */
        case 4: break; /* Game Pak expansion */

        case 5: /* WRAM */
            busWriteBuffer(&sim->wram[address & 0x0000FFFF], type, value);
            break;

        case 6: /* Game Pak RAM */
            if (sim->cart.ram != NULL) {
                busWriteBuffer(
                    &sim->cart.ram[address & sim->cart.ramMask], type, value);
            }
            break;

        case 7: /* Game Pak ROM */
            if (debug && sim->cart.rom != NULL) {
                busWriteBuffer(
                    &sim->cart.rom[address & sim->cart.romMask], type, value);
            }
            break;

    }

}

#endif /* VBAPI */
