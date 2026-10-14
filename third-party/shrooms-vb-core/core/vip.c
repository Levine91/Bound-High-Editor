/* This file is included into vb.c and cannot be compiled on its own. */
#ifdef VBAPI



/********************************** Macros ***********************************/

/* Compute how many clocks are in some number of milliseconds */
#define vipClocksMs(x) x * 20000

/* Read a world line parameter value */
#define vipParam(x) busReadBuffer(&sim->vip.ram[x], VB_U16)



/******************************** Lookup Data ********************************/

/* BG map arrangement by world background dimensions */
static const uint8_t BG_TEMPLATES[][64] = {
    { 0 },                                              /* 1x1 */
    { 0, 1 },                                           /* 1x2 */
    { 0, 1, 2, 3 },                                     /* 1x4 */
    { 0, 1, 2, 3, 4, 5, 6, 7 },                         /* 1x8 */
    { 0, 1 },                                           /* 2x1 */
    { 0, 1, 2, 3 },                                     /* 2x2 */
    { 0, 1, 2, 3, 4, 5, 6, 7 },                         /* 2x4 */
    { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7 }, /* 2x8 */
    { 0, 1, 2, 3 },                                     /* 4x1 */
    { 0, 1, 2, 3, 4, 5, 6, 7 },                         /* 4x2 */
    { 0, 1, 0, 1, 2, 3, 2, 3, 4, 5, 4, 5, 6, 7, 6, 7 }, /* 4x4 */
    { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3,   /* 4x8 */
      4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7 },
    { 0, 1, 2, 3, 4, 5, 6, 7 },                         /* 8x1 */
    { 0, 1, 2, 3, 0, 1, 2, 3, 4, 5, 6, 7, 4, 5, 6, 7 }, /* 8x2 */
    { 0, 1, 0, 1, 0, 1, 0, 1, 2, 3, 2, 3, 2, 3, 2, 3,   /* 8x4 */
      4, 5, 4, 5, 4, 5, 4, 5, 6, 7, 6, 7, 6, 7, 6, 7 },
    { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1,   /* 8x8 */
      2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3,
      4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5,
      6, 6, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 7, 7 }
};

/* 8-bit color magnitude by brightness level */
/* Generated as BRIGHT8[n] = round((n / 132) ** 1.6 * 255) */
static const uint8_t BRIGHT8[] = {
      0, 12, 19, 24, 29, 33, 37, 41, 44, 48, 51, 54, 57, 60, 63, 65,
     68, 71, 73, 76, 78, 81, 83, 86, 88, 90, 92, 95, 97, 99,101,103,
    105,107,109,111,113,115,117,119,121,123,125,127,128,130,132,134,
    136,137,139,141,142,144,146,148,149,151,153,154,156,157,159,161,
    162,164,165,167,168,170,172,173,175,176,178,179,181,182,184,185,
    186,188,189,191,192,194,195,197,198,199,201,202,203,205,206,208,
    209,210,212,213,214,216,217,218,220,221,222,224,225,226,228,229,
    230,231,233,234,235,236,238,239,240,242,243,244,245,246,248,249,
    250,251,253,254,255
};



/*********************************** Types ***********************************/

/* Intersection of the screen and a world rectangle */
typedef struct {
    int32_t x1; /* Left edge inclusive */
    int32_t x2; /* Right edge exclusive */
    int32_t y1; /* Top edge inclusive */
    int32_t y2; /* Bottom edge exclusive */
} Window;



/***************************** Module Functions ******************************/

/* Read a palette */
static int32_t vipReadPalette(uint8_t *entries) {
    return entries[3] << 6 | entries[2] << 4 | entries[1] << 2;
}

/* Raise an interrupt request */
static void vipThrow(VB *sim, uint16_t cause) {
    sim->vip.intpnd |= cause;
    if (sim->vip.intenb & cause)
        sim->cpu.irq |= 0x0010;
}

/* Write a palette */
static void vipWritePalette(uint8_t *entries, int32_t mask, int32_t value) {
    if (mask & 0x00FF)
        return;
    entries[3] = value >> 6 & 3;
    entries[2] = value >> 4 & 3;
    entries[1] = value >> 2 & 3;
}

/* Read a typed value from an I/O register */
static int32_t vipReadIO(VB *sim, uint32_t address, int type) {
    int32_t mask = 0; /* Byte access mask */
    int32_t value;    /* Return value */

    /* Adjustments by type */
    switch (type) {

        case VB_S32: /* Word */
            return vipReadIO(sim, address, VB_U16) |
                (uint32_t) vipReadIO(sim, address + 2, VB_U16) << 16;

        case VB_S8: /* Byte */
        case VB_U8:
            mask  = 0x00FF << ((address & 1) << 3);
            break;

        case VB_S16: /* Halfword */
        case VB_U16:
            mask = 0xFFFF;
    }

    /* Access by register */
    switch (address >> 1) {

        case 0x5F800>>1: /* INTPND */
            value = sim->vip.intpnd;
            break;
        case 0x5F802>>1: /* INTENB */
            value = sim->vip.intenb;
            break;

        case 0x5F820>>1: /* DPSTTS */
            value =
                (int32_t) sim->vip.dp.lock    << 10 |
                (int32_t) sim->vip.dp.synce   <<  9 |
                (int32_t) sim->vip.dp.re      <<  8 |
                (int32_t) sim->vip.dp.fclk    <<  7 |
                (int32_t) sim->vip.dp.scanrdy <<  6 |
                (int32_t) sim->vip.dp.r1bsy   <<  5 |
                (int32_t) sim->vip.dp.l1bsy   <<  4 |
                (int32_t) sim->vip.dp.r0bsy   <<  3 |
                (int32_t) sim->vip.dp.l0bsy   <<  2 |
                (int32_t) sim->vip.dp.disp    <<  1
            ;
            break;

        case 0x5F824>>1: /* BRTA */
            value = sim->vip.brtRest[0];
            break;
        case 0x5F826>>1: /* BRTB */
            value = sim->vip.brtRest[1];
            break;
        case 0x5F828>>1: /* BRTC */
            value = sim->vip.brtRest[2];
            break;
        case 0x5F82A>>1: /* REST */
            value = sim->vip.brtRest[3];
            break;

        case 0x5F82E>>1: /* FRMCYC */
            value = sim->vip.frmcyc;
            break;

        case 0x5F830>>1: /* CTA */
            value = (int32_t) sim->vip.cta.cta_r << 8 | sim->vip.cta.cta_l;
            break;

        case 0x5F840>>1: /* XPSTTS */
            value =
                (int32_t) !!sim->vip.xp.sbout  << 15 |
                (int32_t) sim->vip.xp.sbcount  <<  8 |
                (int32_t) sim->vip.xp.overtime <<  4 |
                (int32_t) sim->vip.xp.f1bsy    <<  3 |
                (int32_t) sim->vip.xp.f0bsy    <<  2 |
                (int32_t) sim->vip.xp.xpen     <<  1
            ;
            break;

        case 0x5F844>>1: /* VER */
            value = 2;
            break;

        case 0x5F848>>1: /* SPT0 */
            value = sim->vip.spt[0];
            break;
        case 0x5F84A>>1: /* SPT1 */
            value = sim->vip.spt[1];
            break;
        case 0x5F84C>>1: /* SPT2 */
            value = sim->vip.spt[2];
            break;
        case 0x5F84E>>1: /* SPT3 */
            value = sim->vip.spt[3];
            break;

        case 0x5F860>>1: /* GPLT0 */
            value = vipReadPalette(sim->vip.gplt[0]);
            break;
        case 0x5F862>>1: /* GPLT1 */
            value = vipReadPalette(sim->vip.gplt[1]);
            break;
        case 0x5F864>>1: /* GPLT2 */
            value = vipReadPalette(sim->vip.gplt[2]);
            break;
        case 0x5F866>>1: /* GPLT3 */
            value = vipReadPalette(sim->vip.gplt[3]);
            break;
        case 0x5F868>>1: /* JPLT0 */
            value = vipReadPalette(sim->vip.jplt[0]);
            break;
        case 0x5F86A>>1: /* JPLT1 */
            value = vipReadPalette(sim->vip.jplt[1]);
            break;
        case 0x5F86C>>1: /* JPLT2 */
            value = vipReadPalette(sim->vip.jplt[2]);
            break;
        case 0x5F86E>>1: /* JPLT3 */
            value = vipReadPalette(sim->vip.jplt[3]);
            break;

        case 0x5F870>>1: /* BKCOL */
            value = sim->vip.bkcol;
            break;

        /* Unmapped */
        default: value = 0;
    }

    /* Select byte bits as necessary */
    return
        mask == 0x00FF ? value &  0x00FF :
        mask == 0xFF00 ? value >> 8      :
    value;
}

/* Write into BG map cell memory */
static void vipWriteCell(VB *sim, uint32_t offset, int type, int32_t value) {
    Cell *cell;

    /* Adjustments for byte and word writes */
    switch (type) {
        case VB_S8:
        case VB_U8:
            value = offset & 1 ?
                value << 8 |           sim->vip.ram[0x20001 ^ offset] :
                value      | (int32_t) sim->vip.ram[0x20001 | offset] << 8
            ;
            break;
        case VB_S32:
            vipWriteCell(sim, offset + 2, VB_U16, value >> 16);
            break;
    }

    /* Update pixel pointer */
    cell          = &sim->vip.cells[offset >> 1];
    cell->palette = sim->vip.gplt[value >> 14 & 3];
    cell->pixels  = sim->vip.characters[value & 0x07FF][value >> 12 & 3];
}

/* Write into character memory */
static void vipWriteChr(VB *sim, uint32_t offset, int type, int32_t value) {
    Character *chr;        /* Pixel state */
    int        b, l, r, t; /* Pixel bounds */
    int        x, y, X, Y; /* Iterators */

    /* Byte advance by data type */
    static const uint8_t HORZ[] = { 4, 4, 8, 8,  8 };
    static const uint8_t VERT[] = { 8, 8, 8, 8, 16 };

    /* Working variables */
    chr = &sim->vip.characters[offset >> 4];
    l   = (offset & 0x1) << 2;
    t   = (offset & 0xE) << 2;
    r   = l + HORZ[type];
    b   = t + VERT[type];

    /* Process all pixels */
    for (y = t, Y = 56 - y; y < b; y += 8, Y -= 8)
    for (x = l, X =  7 - x; x < r; x += 1, X -= 1, value >>= 2) {
        (*chr)[0][y | x] = (*chr)[1][Y | x] =
        (*chr)[2][y | X] = (*chr)[3][Y | X] = value & 3;
    }

}

/* Write into object attribute memory */
static void vipWriteObject(VB *sim, uint32_t offset, int type, int32_t value) {
    Object *obj; /* Object state */

    /* Adjustments for byte and word writes */
    switch (type) {
        case VB_S8:
        case VB_U8:
            value = offset & 1 ?
                value << 8 |           sim->vip.ram[0x3E001 ^ offset] :
                value      | (int32_t) sim->vip.ram[0x3E001 | offset] << 8
            ;
            break;
        case VB_S32:
            vipWriteObject(sim, offset + 2, VB_U16, value >> 16);
            break;
    }

    /* Processing by offset */
    obj = &sim->vip.objects[offset >> 3];
    switch (offset >> 1 & 3) {
        case 0: obj->jx = SignExtend(value, 10); break;
        case 1:
            obj->jp   = SignExtend(value, 10);
            obj->lron = value >> 14 & 3;
            break;
        case 2:
            value = (int8_t) value;
            obj->jy = value > -8 ? value : value & 0xFF;
            break;
        case 3:
            obj->cell.palette = sim->vip.jplt[value >> 14 & 3];
            obj->cell.pixels  =
                sim->vip.characters[value & 0x07FF][value >> 12 & 3];
    }

}

/* Write into world attribute memory */
static void vipWriteWorld(VB *sim, uint32_t offset, int type, int32_t value) {
    uint32_t base;  /* Base BG map index */
    uint8_t *bg;    /* Background template */
    uint32_t count; /* Number of BG maps in background */
    World   *world; /* World state */
    uint32_t z;     /* Iterator */

    /* Adjustments for byte and word writes */
    switch (type) {
        case VB_S8:
        case VB_U8:
            value = offset & 1 ?
                value << 8 |           sim->vip.ram[0x3D801 ^ offset] :
                value      | (int32_t) sim->vip.ram[0x3D801 | offset] << 8
            ;
            break;
        case VB_S32:
            vipWriteWorld(sim, offset + 2, VB_U16, value >> 16);
            break;
    }

    /* Processing by offset */
    world = &sim->vip.worlds[offset >> 5];
    switch (offset >> 1 & 15) {
        case 0:

            /* Parse attributes */
            world->lron = value >> 14 & 3;
            world->bgm  = value >> 12 & 3;
            world->over = value >>  7 & 1;
            world->end  = value >>  6 & 1;

            /* Update background fields */
            z               = value >> 10 & 3;
            world->bgMaskX  =  (1 << z) - 1;
            world->bgWidth  = 512 << z;
            world->bgShift  = z;
            z               = value >>  8 & 3;
            world->bgMaskY  =  (1 << z) - 1;
            world->bgHeight = 512 << z;

            /* Update world dimensions */
            if (world->bgm != 2) {
                world->height = world->hNaffine;
                world->width  = world->wNaffine;
            } else {
                world->height = world->hAffine;
                world->width  = world->wAffine;
            }

            /* Update background arrangement */
            count = (world->bgMaskX + 1) * (world->bgMaskY + 1);
            base  = value & 15 & ~(count > 8 ? 7 : count - 1);
            bg    = (uint8_t *) BG_TEMPLATES[value >> 8 & 15];
            for (z = 0; z < count; z++)
                world->bg[z] = &sim->vip.cells[(base + bg[z]) << 12];
            break;

        case 1: world->gx = SignExtend(value, 10); break;
        case 2: world->gp = SignExtend(value, 10); break;
        case 3: world->gy = (int16_t) value;       break;
        case 4: world->mx = SignExtend(value, 13); break;
        case 5: world->mp = SignExtend(value, 15); break;
        case 6: world->my = SignExtend(value, 13); break;
        case 7: /* W */
            value = SignExtend(value, 13) + 1;
            world->wNaffine = value < 0 ? 0 : value;
            value = SignExtend(value, 10) + 1;
            world->wAffine  = value < 0 ? 0 : value;
            world->width = world->bgm != 2 ? world->wNaffine : world->wAffine;
            break;
        case 8: /* H */
            value = (int16_t) value + 1;
            world->hNaffine = value < 8 ? 8 : value;
            world->hAffine  = value < 0 ? 0 : value;
            world->height = world->bgm != 2 ? world->hNaffine : world->hAffine;
            break;
        case  9: world->paramBase = 0x20000|(uint32_t)(uint16_t)value<<1;break;
        case 10: world->overplane = &sim->vip.cells[(uint16_t) value];   break;
    }

}

/* Write a typed value to an I/O register */
static void vipWriteIO(
    VB *sim, uint32_t address, int type, int32_t value, int debug) {
    int32_t mask; /* Byte access mask */

    /* Adjustments by type */
    switch (type) {

        case VB_S32: /* Word */
            vipWriteIO(sim, address    , VB_U16, value      , debug);
            vipWriteIO(sim, address + 2, VB_U16, value >> 16, debug);
            return;

        case VB_S8: /* Byte */
        case VB_U8:

            /* Select which half of the register to access */
            if (debug) {
                mask  = (address & 1) << 3;
                value = (value & 0x00FF) << mask;
                mask  = 0xFF00 >> mask;
                break;
            }

            /* Convert to a halfword access */
            if ((address & 1) != 0)
                value = (value & 0x00FF) << 8;

            /* Fallthrough */
        default: /* Halfword */
            mask = 0x0000;
    }

    /* Access by register */
    switch (address >> 1) {

        case 0x5F802>>1: /* INTENB */
            sim->vip.intenb = (sim->vip.intenb & mask) | (value & 0xE01F);
            if (sim->vip.intenb & sim->vip.intpnd)
                sim->cpu.irq  |=  0x0010;
            else sim->cpu.irq &= ~0x0010;
            break;
        case 0x5F804>>1: /* INTCLR */
            sim->vip.intpnd &= ~value;
            if ((sim->vip.intpnd & sim->vip.intenb) == 0)
                sim->cpu.irq &= ~0x0010;
            break;

        case 0x5F822>>1: /* DPCTRL */
            if ((mask & 0xFF00) == 0) {
                sim->vip.dp.lock  = value >> 10 & 1;
                sim->vip.dp.synce = value >>  9 & 1;
                sim->vip.dp.re    = value >>  8 & 1;
            }
            if ((mask & 0x00FF) == 0) {
                sim->vip.dp.disp  = value >>  1 & 1;

                /* DPRST */
                if (value & 1) {
                    sim->vip.intenb &= ~0x801F;
                    sim->vip.intpnd &= ~0x801F;
                    if ((sim->vip.intpnd & sim->vip.intenb) == 0)
                        sim->cpu.irq &= ~0x0010;
                    /* TODO: Research exact operation */
                }

            }
            break;

        case 0x5F824>>1: /* BRTA */
            sim->vip.brtRest[0] = (sim->vip.brtRest[0] & mask) | value;
            break;
        case 0x5F826>>1: /* BRTB */
            sim->vip.brtRest[1] = (sim->vip.brtRest[1] & mask) | value;
            break;
        case 0x5F828>>1: /* BRTC */
            sim->vip.brtRest[2] = (sim->vip.brtRest[2] & mask) | value;
            break;
        case 0x5F82A>>1: /* REST */
            sim->vip.brtRest[3] = (sim->vip.brtRest[3] & mask) | value;
            break;

        case 0x5F82E>>1: /* FRMCYC */
            sim->vip.frmcyc = (sim->vip.frmcyc & mask) | (value & 15);
            break;

        case 0x5F830>>1: /* CTA */
            if (debug) {
                if ((mask & 0xFF00) == 0)
                    sim->vip.cta.cta_r = value >> 8;
                if ((mask & 0x00FF) == 0)
                    sim->vip.cta.cta_l = value;
            }
            break;

        case 0x5F842>>1: /* XPCTRL */
            if ((mask & 0xFF00) == 0)
                sim->vip.xp.sbcmp = value >> 8 & 31;
            if ((mask & 0x00FF) == 0) {
                sim->vip.xp.xpen  = value >> 1 &  1;

                /* XPRST */
                if (value & 1) {
                    sim->vip.intenb &= ~0xE000;
                    sim->vip.intpnd &= ~0xE000;
                    if ((sim->vip.intpnd & sim->vip.intenb) == 0)
                        sim->cpu.irq &= ~0x0010;
                    /* TODO: Research exact operation */
                }

            }
            break;

        case 0x5F848>>1: /* SPT0 */
            sim->vip.spt[0] = (sim->vip.spt[0]&mask) | (value&0x03FF&~mask);
            break;
        case 0x5F84A>>1: /* SPT1 */
            sim->vip.spt[1] = (sim->vip.spt[1]&mask) | (value&0x03FF&~mask);
            break;
        case 0x5F84C>>1: /* SPT2 */
            sim->vip.spt[2] = (sim->vip.spt[2]&mask) | (value&0x03FF&~mask);
            break;
        case 0x5F84E>>1: /* SPT3 */
            sim->vip.spt[3] = (sim->vip.spt[3]&mask) | (value&0x03FF&~mask);
            break;

        case 0x5F860>>1: /* GPLT0 */
            vipWritePalette(sim->vip.gplt[0], mask, value);
            break;
        case 0x5F862>>1: /* GPLT1 */
            vipWritePalette(sim->vip.gplt[1], mask, value);
            break;
        case 0x5F864>>1: /* GPLT2 */
            vipWritePalette(sim->vip.gplt[2], mask, value);
            break;
        case 0x5F866>>1: /* GPLT3 */
            vipWritePalette(sim->vip.gplt[3], mask, value);
            break;
        case 0x5F868>>1: /* JPLT0 */
            vipWritePalette(sim->vip.jplt[0], mask, value);
            break;
        case 0x5F86A>>1: /* JPLT1 */
            vipWritePalette(sim->vip.jplt[1], mask, value);
            break;
        case 0x5F86C>>1: /* JPLT2 */
            vipWritePalette(sim->vip.jplt[2], mask, value);
            break;
        case 0x5F86E>>1: /* JPLT3 */
            vipWritePalette(sim->vip.jplt[3], mask, value);
            break;

        case 0x5F870>>1: /* BKCOL */
            sim->vip.bkcol = (sim->vip.bkcol & mask) | (value & 3 & ~mask);
            break;
    }

}



/***************************** Callback Handlers *****************************/

/* Prepare to handle an exception */
#ifndef VB_DIRECT_FRAME
    #define VB_ON_FRAME sim->onFrame
#else
    extern int VB_DIRECT_FRAME(VB *);
    #define VB_ON_FRAME VB_DIRECT_FRAME
#endif
static int vipOnFrame(VB *sim) {
    return sim->onFrame != NULL && VB_ON_FRAME(sim);
}
#undef VB_ON_FRAME



/***************************** Display Processor *****************************/

/* Precompute brightness values for image output */
static void vipComputeBrightness(VB *sim) {
    int32_t brt[4]; /* Working output */
    int32_t repeat; /* Multiplier from column table */
    int     x;      /* Iterator */

    /* Compute brightness from hardware state */
    repeat = (int32_t) sim->vip.ram[sim->vip.dp.cta + 1] + 1;
    brt[1] = repeat * sim->vip.brtRest[0];
    brt[2] = repeat * sim->vip.brtRest[1];
    brt[3] = repeat * sim->vip.brtRest[2] + brt[2] + brt[1];

    /* Transform brightness values to 0..255 */
    for (x = 1; x < 4; x++)
        sim->vip.dp.brt[x] = brt[x] > 132 ? 255 : BRIGHT8[brt[x]];
}

/* Transfer one column of frame buffer pixels to output */
static void vipTransferColumn(VB *sim, int32_t eye) {
    int32_t  bits; /* Pixel bits from frame buffer */
    uint8_t *dest; /* Host frame image output */
    uint8_t *src;  /* Simulation frame buffer input */
    int      y, z; /* Iterators */

    /* Select destination address in host memory */
    dest = &sim->vip.output
        [sim->vip.buffer ^ 1][eye][sim->vip.dp.column];

    /* Output is disabled */
    if (!sim->vip.dp.enabled) {
        for (y = 0; y < 224; y += 16)
        for (z = 0; z < 16; z++, dest += 384)
            *dest = 0;
        return;
    }

    /* Select source address in host memory */
    src = &sim->vip.ram[
        eye                << 16 |
        sim->vip.dp.buffer << 15 |
        sim->vip.dp.column <<  6
    ];

    /* Transfer all rows as words */
    for (y = 0; y < 224; y += 16, src += 4) {
        bits = busReadBuffer(src, VB_S32);
        for (z = 0; z < 16; bits >>= 2, z++, dest += 384)
            *dest = sim->vip.dp.brt[bits & 3];
    }

}

/* Process sub-component */
static int vipEmulateDisplay(VB *sim, uint32_t clocks) {

    /* Process all clocks */
    for (;;) {

        /* The next event is after the time remaining */
        if (sim->vip.dp.clocks > clocks) {
            sim->vip.dp.clocks -= clocks;
            sim->vip.dp.until  -= clocks;
            return 0;
        }

        /* Advance forward the component's number of clocks */
        clocks            -= sim->vip.dp.clocks;
        sim->vip.dp.until -= sim->vip.dp.clocks;
        sim->vip.dp.clocks = 0;

        /* Processing by operation phase */
        switch (sim->vip.dp.step) {

            case 0: /* 0ms - FCLK rising edge */
                sim->vip.dp.clocks  = vipClocksMs(3);
                sim->vip.dp.enabled = sim->vip.dp.disp & sim->vip.dp.synce;
                sim->vip.dp.fclk    = 1;
                sim->vip.dp.step    = 1;
                sim->vip.dp.until   = vipClocksMs(8);
                vipThrow(sim, 0x0010); /* FRAMESTART */

                /* Game frame */
                if (sim->vip.xp.frame >= sim->vip.frmcyc) {
                    sim->vip.xp.frame = 0;

                    vipThrow(sim, 0x0008); /* GAMESTART */
                    if (sim->vip.xp.step != 0) {
                        sim->vip.xp.overtime = 1;
                        vipThrow(sim, 0x8000); /* TIMEERR */
                    }

                    /* Initiate drawing procedure */
                    else if (sim->vip.xp.xpen) {
                        sim->vip.dp.buffer  ^= sim->vip.xp.toggle;
                        sim->vip.xp.enabled  = 1;
                        sim->vip.xp.overtime = 0;
                        sim->vip.xp.step     = 1;
                        sim->vip.xp.toggle   = 0;
                    }

                }

                /* Not a game frame */
                else sim->vip.xp.frame++;

                break;

            /* 0ms-3ms - Idle */

            case 1: /* 3ms - L*BSY rising edge */
                if (sim->vip.dp.enabled) {
                    if (sim->vip.dp.buffer == 0)
                         sim->vip.dp.l0bsy = 1;
                    else sim->vip.dp.l1bsy = 1;
                }
                sim->vip.dp.column = 0;
                sim->vip.dp.cta    = 0x3DC00 | (uint32_t)sim->vip.cta.cta_l<<1;
                sim->vip.dp.step   = 2;
                vipComputeBrightness(sim);

                /* Fallthrough */
            case 2: /* 3ms-8ms - Display left frame buffer */
                if ((sim->vip.dp.column & 3) == 0)
                    vipComputeBrightness(sim);
                vipTransferColumn(sim, 0);
                sim->vip.dp.clocks =
                    260 + (0xA94 >> sim->vip.dp.column % 12 & 1);
                sim->vip.dp.column++;
                if ((sim->vip.dp.column & 3) == 0)
                    sim->vip.dp.cta -= 2;
                if (sim->vip.dp.column == 384)
                    sim->vip.dp.step = 3;
                break;

            case 3: /* 8ms - L*BSY falling edge */
                if (sim->vip.dp.enabled) {
                    if (sim->vip.dp.buffer == 0)
                         sim->vip.dp.l0bsy = 0;
                    else sim->vip.dp.l1bsy = 0;
                    vipThrow(sim, 0x0002); /* LFBEND */
                }
                sim->vip.dp.clocks = vipClocksMs(2);
                sim->vip.dp.step   = 4;
                sim->vip.dp.until  = vipClocksMs(10);
                break;

            /* 8ms-10ms - Idle */

            case 4: /* 10ms - FCLK falling edge */
                sim->vip.dp.clocks = vipClocksMs(3);
                sim->vip.dp.fclk   = 0;
                sim->vip.dp.step   = 5;
                break;

            /* 10ms-13ms - Idle */

            case 5: /* 13ms - R*BSY rising edge */
                if (sim->vip.dp.enabled) {
                    if (sim->vip.dp.buffer == 0)
                         sim->vip.dp.r0bsy = 1;
                    else sim->vip.dp.r1bsy = 1;
                }
                sim->vip.dp.column = 0;
                sim->vip.dp.cta    = 0x3DE00 | (uint32_t)sim->vip.cta.cta_r<<1;
                sim->vip.dp.step   = 6;

                /* Fallthrough */
            case 6: /* 13ms-18ms - Display right frame buffer */
                if ((sim->vip.dp.column & 3) == 0)
                    vipComputeBrightness(sim);
                vipTransferColumn(sim, 1);
                sim->vip.dp.clocks =
                    260 + (0xA94 >> sim->vip.dp.column % 12 & 1);
                sim->vip.dp.column++;
                if ((sim->vip.dp.column & 3) == 0)
                    sim->vip.dp.cta -= 2;
                if (sim->vip.dp.column == 384)
                    sim->vip.dp.step = 7;
                break;

            case 7: /* 18ms - R*BSY falling edge */
                if (sim->vip.dp.enabled) {
                    if (sim->vip.dp.buffer == 0)
                         sim->vip.dp.r0bsy = 0;
                    else sim->vip.dp.r1bsy = 0;
                    vipThrow(sim, 0x0004); /* RFBEND */
                }
                sim->vip.dp.clocks  = vipClocksMs(2);
                sim->vip.dp.enabled = 0;
                sim->vip.dp.step    = 8;
                sim->vip.dp.until   = vipClocksMs(2);
                break;

            /* 18ms-20ms - Idle */

            case 8: /* 20ms - Display interval complete */
                sim->vip.buffer ^= 1;
                sim->vip.dp.step = 0;
                if (vipOnFrame(sim))
                    return 1;
                break;

        }
    }

    return 0;
}



/****************************** Pixel Processor ******************************/

/* Draw a cell into shadow memory */
static void vipDrawCell(uint8_t *shadow, Window *wnd,
    Cell *cell, int32_t x, int32_t y) {
    uint8_t *col, *row;          /* Output pixel */
    uint8_t  pixel;              /* Character pixel value */
    int32_t  px1, px2, py1, py2; /* Pixel bounds in cell */
    int32_t  px, py;             /* Pixel origin in cell */

    /* Identify the visible pixels */
    py1 = y     > wnd->y1 ? y     : wnd->y1;
    py2 = y + 8 < wnd->y2 ? y + 8 : wnd->y2;
    px1 = x     > wnd->x1 ? x     : wnd->x1;
    px2 = x + 8 < wnd->x2 ? x + 8 : wnd->x2;

    /* Process all columns of pixels */
    col = &shadow[px1 * 224 + py1];
    for (px = px1; px < px2; px++, col += 224)
    for (py = py1, row = col; py < py2; py++, row++) {
        pixel = cell->pixels[(py - y) << 3 | (px - x)];
        if (pixel != 0)
            *row = cell->palette[pixel];
    }

}

/* Draw a BG map into shadow memory */
static void vipDrawBGMap(uint8_t *shadow, Window *wnd,
    uint8_t over, Cell *map, int32_t x, int32_t y) {
    Cell   *col, *row;          /* Source cell */
    int32_t cx1, cx2, cy1, cy2; /* Cell bounds in BG map pixels */
    int32_t cx, cy;             /* Cell origin in BG map pixels */

    /* Identify the visible cells */
    cy1 = (wnd->y1-y  ) & ~7; if (cy1 < 0) cy1 = 0; if (cy1 > 512) cy1 = 512;
    cy2 = (wnd->y2-y+7) & ~7; if (cy2 < 0) cy2 = 0; if (cy2 > 512) cy2 = 512;
    cx1 = (wnd->x1-x  ) & ~7; if (cx1 < 0) cx1 = 0; if (cx1 > 512) cx1 = 512;
    cx2 = (wnd->x2-x+7) & ~7; if (cx2 < 0) cx2 = 0; if (cx2 > 512) cx2 = 512;

    /* Process all cells as overplane */
    if (over) {
        for (cy = cy1; cy < cy2; cy += 8)
        for (cx = cx1; cx < cx2; cx += 8)
            vipDrawCell(shadow, wnd, map, x + cx, y + cy);
        return;
    }

    /* Process all cells normally */
    row = &map[cy1 << 3 | cx1 >> 3];
    for (cy = cy1           ; cy < cy2; cy += 8, row += 64)
    for (cx = cx1, col = row; cx < cx2; cx += 8, col++)
        vipDrawCell(shadow, wnd, col, x + cx, y + cy);
}

/* Draw a background into shadow memory */
static void vipDrawBackground(uint8_t *shadow, Window *wnd,
    World *world, int32_t x, int32_t y) {
    int32_t bx1, bx2, by1, by2; /* BG map bounds in background pixels */
    int32_t bx, by;             /* BG map origin in background pixels */

    /* Identify the visible BG maps */
    by1 = (wnd->y1 - y    ) & ~511;
    by2 = (wnd->y2 - y - 1) & ~511;
    bx1 = (wnd->x1 - x    ) & ~511;
    bx2 = (wnd->x2 - x - 1) & ~511;

    /* Process all rows of BG maps */
    for (by = by1; by <= by2; by += 512) {

        /* Row is out of bounds */
        if (world->over && (by < 0 || by >= world->bgHeight)) {
            for (bx = bx1; bx <= bx2; bx++)
                vipDrawBGMap(shadow, wnd, 1, world->overplane, x + bx, y + by);
            continue;
        }

        /* Process all columns of BG maps */
        for (bx = bx1; bx <= bx2; bx += 512) {

            /* Column is out of bounds */
            if (world->over && (bx < 0 || bx >= world->bgWidth)) {
                vipDrawBGMap(shadow, wnd, 1, world->overplane, x + bx, y + by);
                continue;
            }

            /* Column is in bounds */
            vipDrawBGMap(shadow, wnd, 0, world->bg[
                (by >> 9 & world->bgMaskY) << world->bgShift |
                (bx >> 9 & world->bgMaskX)
            ], x + bx, y + by);
        }

    }

}

/* Draw a Normal world into shadow memory */
static void vipDrawNormal(VB *sim, World *world) {
    int32_t  mx, my; /* Background origin */
    Window   wnd;    /* Window bounds in pixels */
    int      i;      /* Iterator */

    /* No visible content */
    if (world->width == 0)
        return;

    /* Vertical window bounds */
    wnd.y1 = (int32_t) sim->vip.xp.sbcount << 3;
    if (world->gy > wnd.y1)
        wnd.y1 = world->gy;
    if (wnd.y1 >= 224)
        return;
    wnd.y2 = world->gy + world->height;
    if (wnd.y2 <= 0)
        return;
    my = world->gy - world->my;
    if (wnd.y1 < 0)
        wnd.y1 = 0;
    if (wnd.y2 > 224)
        wnd.y2 = 224;

    /* Process both eyes */
    for (i = 0; i < 2; i++) {

        /* Check visibility */
        if (!(world->lron >> (i ^ 1) & 1))
            continue;

        /* Horizontal window bounds */
        wnd.x1 = world->gx + (i == 0 ? -world->gp : world->gp);
        if (wnd.x1 >= 384)
            continue;
        wnd.x2 = wnd.x1 + world->width;
        if (wnd.x2 <= 0)
            continue;
        mx = wnd.x1 - world->mx - (i == 0 ? -world->mp : world->mp);
        if (wnd.x1 < 0)
            wnd.x1 = 0;
        if (wnd.x2 > 384)
            wnd.x2 = 384;

        /* Draw the background into the window */
        vipDrawBackground(sim->vip.shadow[i], &wnd, world, mx, my);
    }

}

/* Draw an H-bias world into shadow memory */
static void vipDrawHBias(VB *sim, World *world) {
    int32_t  mx, my; /* Background origin */
    uint8_t *param;  /* World parameter memory */
    Window   wnd;    /* Window bounds in pixels */
    int32_t  y1, y2; /* Vertical window bounds */
    int      i, y;   /* Iterators */

    /* No visible content */
    if (world->width == 0)
        return;

    /* Vertical window bounds */
    y1 = (int32_t) sim->vip.xp.sbcount << 3;
    if (world->gy > y1)
        y1 = world->gy;
    if (y1 >= 224)
        return;
    y2 = world->gy + world->height;
    if (y2 <= 0)
        return;
    my = world->gy - world->my;
    if (y1 < 0)
        y1 = 0;
    if (y2 > 224)
        y2 = 224;

    /* Process both eyes */
    for (i = 0; i < 2; i++) {

        /* Check visibility */
        if (!(world->lron >> (i ^ 1) & 1))
            continue;

        /* Horizontal window bounds */
        wnd.x1 = world->gx + (i == 0 ? -world->gp : world->gp);
        if (wnd.x1 >= 384)
            continue;
        wnd.x2 = wnd.x1 + world->width;
        if (wnd.x2 <= 0)
            continue;
        mx = wnd.x1 - world->mx - (i == 0 ? -world->mp : world->mp);
        if (wnd.x1 < 0)
            wnd.x1 = 0;
        if (wnd.x2 > 384)
            wnd.x2 = 384;

        /* Process all visible lines */
        param = &sim->vip.ram[world->paramBase +
            ((y1 - world->gy) << 2) + (i << 1)];
        for (y = y1; y < y2; y++, param += 4) {

            /* Configure window bounds */
            wnd.y1 = y;
            wnd.y2 = y + 1;

            /* Draw the background into the window */
            vipDrawBackground(sim->vip.shadow[i], &wnd, world,
                mx - busReadBuffer(param, VB_S16), my);
        }

    }

}

/* Draw an Affine world into shadow memory */
static void vipDrawAffine(VB *sim, World *world) {
    Cell    *cell;               /* Source cell */
    uint8_t *col, *row;          /* Output pixel */
    uint8_t  pixel;              /* Character pixel value */
    uint32_t param;              /* World parameter memory */
    int32_t  px, py;             /* Pixel source coordinates */
    int32_t  dx, dy, mp, mx, my; /* Affine parameters */
    int32_t  wx;                 /* Base world X coordinate */
    int32_t  x1, x2, y1, y2;     /* Window bounds */
    int      i, x, y;            /* Iterators */

    /* No visible content */
    if (world->width == 0 || world->height == 0)
        return;

    /* Vertical window bounds */
    y1 = (int32_t) sim->vip.xp.sbcount << 3;
    if (world->gy > y1)
        y1 = world->gy;
    if (y1 >= 224)
        return;
    y2 = world->gy + world->height;
    if (y2 <= 0)
        return;
    if (y1 < 0)
        y1 = 0;
    if (y2 > 224)
        y2 = 224;

    /* Process both eyes */
    for (i = 0; i < 2; i++) {

        /* Check visibility */
        if (!(world->lron >> (i ^ 1) & 1))
            continue;

        /* Horizontal window bounds */
        x1 = world->gx + (i == 0 ? -world->gp : world->gp);
        if (x1 >= 384)
            continue;
        x2 = x1 + world->width;
        if (x2 <= 0)
            continue;
        wx = x1;
        if (x1 < 0)
            x1 = 0;
        if (x2 > 384)
            x2 = 384;
        wx = x1 - wx;

        /* Process all visible rows */
        param = world->paramBase + ((y1 - world->gy) << 4);
        row   = &sim->vip.shadow[i][x1 * 224 + y1];
        for (y = y1; y < y2; y++, param += 16, row++) {

            /* Parse line parameters */
            mx = (int32_t) busReadBuffer(&sim->vip.ram[param | 0], VB_S16) << 6;
            mp = (int32_t) busReadBuffer(&sim->vip.ram[param | 2], VB_S16);
            my = (int32_t) busReadBuffer(&sim->vip.ram[param | 4], VB_S16) << 6;
            dx = (int32_t) busReadBuffer(&sim->vip.ram[param | 6], VB_S16);
            dy = (int32_t) busReadBuffer(&sim->vip.ram[param | 8], VB_S16);

            /* Adjust left-edge parameters */
            if ((mp < 0) ^ i) {
                if (mp < 0) {
                    mx += dx * (wx - mp);
                    my += dy * (wx - mp);
                } else {
                    mx += dx * (wx + mp);
                    my += dy * (wx + mp);
                }
            } else {
                mx += dx * wx;
                my += dy * wx;
            }

            /* Process all visible columns */
            col = row;
            for (x = x1; x < x2; x++, mx += dx, my += dy, col += 224) {
                px = (int16_t) (mx >> 9);
                py = (int16_t) (my >> 9);

                /* Overplane */
                if (world->over && (
                    px < 0 || px >= world->bgWidth ||
                    py < 0 || py >= world->bgHeight
                )) cell = world->overplane;

                /* Regular */
                else cell = &world->bg[
                    (py >> 9 & world->bgMaskY) << world->bgShift |
                    (px >> 9 & world->bgMaskX)
                ][(py << 3 & 0xFC0) | (px >> 3 & 63)];

                /* Sample the pixel */
                pixel = cell->pixels[(py & 7) << 3 | (px & 7)];
                if (pixel != 0)
                    *col = cell->palette[pixel];

            } /* x */

        } /* y */

    } /* i */

}

/* Draw an object group into shadow memory */
static void vipDrawObjects(VB *sim, int group) {
    Object *obj;   /* Object state */
    int     start; /* First object in the group */
    int     stop;  /* Last object in the group */
    Window  wnd;   /* Window bounds in pixels */
    int     i, o;  /* Iterators */

    /* Establish the viewing window */
    wnd.x1 = 0;
    wnd.y1 = (int32_t) sim->vip.xp.sbcount << 3;
    wnd.x2 = 384;
    wnd.y2 = 224;

    /* Process all objects in the group */
    start = group == 0 ? 0 : (sim->vip.spt[group - 1] + 1) & 1023;
    stop  = sim->vip.spt[group];
    for (o = stop; o != -1; o = o == start ? -1 : (o - 1) & 1023) {
        obj = &sim->vip.objects[o];
        for (i = 0; i < 2; i++) {
            if (!(obj->lron >> (i ^ 1) & 1))
                continue;
            vipDrawCell(sim->vip.shadow[i], &wnd, &obj->cell,
                obj->jx + (i == 0 ? -obj->jp : obj->jp), obj->jy);
        }
    }

}

/* Draw the current graphics configuration into shadow memory */
static void vipRender(VB *sim) {
    int       group;  /* Next object group index */
    uint16_t *timing; /* Column timing in host memory */
    World    *world;  /* World attribute source in host memory */
    int32_t   x, y;   /* Iterators */

    /* Erase all pixels */
    for (x = 0; x < 384*224; x += 224)
    for (y = (int32_t) sim->vip.xp.sbcount << 3; y < 224; y++)
        sim->vip.shadow[0][x + y] = sim->vip.shadow[1][x + y] = sim->vip.bkcol;

    /* Process all worlds */
    group = 3;
    for (x = 31; x >= 0; x--) {
        world = &sim->vip.worlds[x];

        /* Non-graphical world */
        if (world->end)
            break;    /* Control world */
        if (world->lron == 0)
            continue; /* Dummy world */

        /* Processing by world type */
        switch (world->bgm) {
            case 0: vipDrawNormal(sim, world); break;
            case 1: vipDrawHBias (sim, world); break;
            case 2: vipDrawAffine(sim, world); break;
            case 3:
                vipDrawObjects(sim, group);
                group = (group - 1) & 3;
        }

    }

    /*
      Supply filler timings for each group of 8 rows of pixels
        Using 2.8ms experimental measurement for empty frame
        = 125 clocks every 24 halfwords
    */
    timing = &sim->vip.halfwords[(uint32_t) sim->vip.xp.sbcount * 384];
    x      = sim->vip.xp.column;
    for (y = sim->vip.xp.sbcount; y < 28; y++, x = 0)
    for (; x < 384; x++, timing++)
        *timing = 5 + ((uint32_t) 0x210884 >> x % 24 & 1);
}

/* Transfer one halfword from shadow pixel memory to the frame buffer */
static void vipTransferHalfword(VB *sim) {
    uint32_t bits0;  /* Upper 4 pixels from shadow memory */
    uint32_t bits1;  /* Lower 4 pixels from shadow memory */
    uint8_t *dest;   /* Pointer to frame buffer data in host memory */
    uint32_t offset; /* Position of halfword in shadow memory */
    uint8_t *src;    /* Pointer to pixel data in host memory */
    int      i;      /* Iterator */

    /* Determine the output address in frame buffer memory */
    dest = &sim->vip.ram[
        (uint32_t) (sim->vip.dp.buffer ^ 1) << 15 |
        (uint32_t)  sim->vip.xp.column      <<  6 |
        (uint32_t)  sim->vip.xp.sbcount     <<  1
    ];

    /* Transfer halfwords to each eye's frame buffer */
    offset = (uint32_t) sim->vip.xp.column * 224 +
        ((uint32_t) sim->vip.xp.sbcount << 3);
    for (i = 0; i < 2; i++, dest += 0x10000) {
        src   = &sim->vip.shadow[i][offset];
        bits0 = busReadBuffer(src    , VB_S32);
        bits1 = busReadBuffer(src + 4, VB_S32);
        busWriteBuffer(dest, VB_U16,
            (bits0       & 0x0003) |
            (bits0 >>  6 & 0x000C) |
            (bits0 >> 12 & 0x0030) |
            (bits0 >> 18 & 0x00C0) |
            (bits1 <<  8 & 0x0300) |
            (bits1 <<  2 & 0x0C00) |
            (bits1 >>  4 & 0x3000) |
            (bits1 >> 10 & 0xC000)
        );
    }

}

/* Process sub-component */
static void vipEmulateDrawing(VB *sim, uint32_t clocks) {

    /* Process all clocks */
    for (;;) {

        /* The next event is after the time remaining */
        if (sim->vip.xp.clocks > clocks) {
            sim->vip.xp.clocks -= clocks;
            sim->vip.xp.until  -= clocks;
            return;
        }

        /* Advance forward the component's number of clocks */
        clocks            -= sim->vip.xp.clocks;
        sim->vip.xp.until -= sim->vip.xp.clocks;
        sim->vip.xp.clocks = 0;

        /* Processing by operation phase */
        switch (sim->vip.xp.step) {

            case 1: /* Initialization */
                sim->vip.xp.halfword = 0;
                sim->vip.xp.sbcount  = 0;
                if (sim->vip.dp.buffer == 0)
                     sim->vip.xp.f1bsy = 1;
                else sim->vip.xp.f0bsy = 1;
                vipRender(sim);

                /* Fallthrough */
            case 2: /* Begin drawing halfwords */
                sim->vip.xp.column = 0;
                sim->vip.xp.sbout  = 2240; /* 112us */
                sim->vip.xp.step   = 3;
                sim->vip.xp.until  = 2000; /* 100us */
                if (sim->vip.xp.sbcount == sim->vip.xp.sbcmp)
                    vipThrow(sim, 0x2000); /* SBHIT */

                /* Fallthrough */
            case 3: /* Draw one halfword */
                vipTransferHalfword(sim);
                sim->vip.xp.clocks = sim->vip.halfwords[sim->vip.xp.halfword];
                sim->vip.xp.column  ++;
                sim->vip.xp.halfword++;
                if (sim->vip.xp.column < 384)
                    break;

                /* Transition to a new line of halfwords */
                sim->vip.xp.sbcount++;
                sim->vip.xp.step = sim->vip.xp.sbcount == 28 ? 4 : 2;
                break;

            case 4: /* Drawing complete */
                sim->vip.xp.enabled = 0;
                sim->vip.xp.step    = 0;
                sim->vip.xp.toggle  = 1;
                if (sim->vip.dp.buffer == 0)
                     sim->vip.xp.f1bsy = 0;
                else sim->vip.xp.f0bsy = 0;
                vipThrow(sim, 0x4000); /* XPEND */
                return;
        }

    }

    return; /* Unreachable */
}



/***************************** Library Functions *****************************/

/* Process component */
static int vipEmulate(VB *sim, uint32_t clocks) {

    /* Process sub-components */
    int brk = vipEmulateDisplay(sim, clocks);
    if (sim->vip.xp.step != 0)
        vipEmulateDrawing(sim, clocks);

    /* Process SBOUT */
    if (sim->vip.xp.sbout != 0) {
        if (sim->vip.xp.sbout <= clocks) {
            sim->vip.xp.sbout = 0;
        } else sim->vip.xp.sbout -= clocks;
    }

    return brk;
}

/* Read a typed value from the VIP bus */
static void vipRead(VB *sim, uint32_t address, int type, int32_t *value) {

    /* Working variables */
    address &= 0x0007FFFF;

    /* RAM */
    if (address < 0x40000)
        *value = busReadBuffer(&sim->vip.ram[address], type);

    /* Unmapped */
    else if (address < 0x5E000)
        *value = 0;

    /* I/O register */
    else if (address < 0x60000) {
        *value = vipReadIO(sim, address, type);
        switch (type) {
            case VB_S8 : *value = (int8_t ) *value; break;
            case VB_S16: *value = (int16_t) *value; break;
        }
    }

    /* Unmapped */
    else if (address < 0x78000)
        *value = 0;

    /* Mirrors of character memory */
    else {
        address = 0x06000 | (address << 2 & 0x18000) | (address & 0x01FFF);
        *value  = busReadBuffer(&sim->vip.ram[address], type);
    }

}

/* Simulate a hardware reset */
static void vipReset(VB *sim) {
    Cell      *cell;    /* BG map cell state */
    Character *chr;     /* Character state */
    Object    *obj;     /* Object state */
    World     *world;   /* World state */
    int        x, y, z; /* Iterators */

    /* Normal */
    sim->vip.intenb   = 0x0000;
    sim->vip.dp.disp  = 0;
    sim->vip.dp.re    = 0;
    sim->vip.dp.synce = 0;
    sim->vip.xp.xpen  = 0;

    /* Extra (the hardware does not do this) */
    sim->vip.bkcol     = 0;
    sim->vip.cta.cta_l = 0xFA;
    sim->vip.cta.cta_r = 0xFA;
    sim->vip.frmcyc    = 0;
    sim->vip.intpnd    = 0x0000;
    for (x = 0; x < 0x40000; x++)
        sim->vip.ram[x] = 0;
    for (x = 0; x < 4; x++) {
        sim->vip.brtRest[x] = 0;
        sim->vip.spt    [x] = 0;
        for (y = 0; y < 4; y++) {
            sim->vip.gplt[x][y] = 0;
            sim->vip.jplt[x][y] = 0;
        }
    }
    for (x = 0; x < 0x10000; x++) {
        cell = &sim->vip.cells[x];
        cell->palette = sim->vip.gplt[0];
        cell->pixels  = sim->vip.characters[0][0];
    }
    for (x = 0; x < 2048; x++) {
        chr = &sim->vip.characters[x];
        for (y = 0; y < 64; y++)
            (*chr)[0][y] = (*chr)[1][y] = (*chr)[2][y] = (*chr)[3][y] = 0;
    }
    for (x = 0; x < 32; x++) {
        world = &sim->vip.worlds[x];
        world->bgm       = 0;
        world->end       = 0;
        world->gp        = 0;
        world->gx        = 0;
        world->gy        = 0;
        world->mx        = 0;
        world->mp        = 0;
        world->my        = 0;
        world->over      = 0;
        world->overplane = &sim->vip.cells[0];
        for (y = 0; y < 64; y++)
            world->bg[0] = world->overplane;
        world->bgHeight  = 512;
        world->bgMaskX   = 0;
        world->bgMaskY   = 0;
        world->bgShift   = 0;
        world->bgWidth   = 512;
        world->hAffine   = 0;
        world->hNaffine  = 0;
        world->height    = 8;
        world->lron      = 0;
        world->paramBase = 0;
        world->wAffine   = 0;
        world->wNaffine  = 0;
        world->width     = 0;
    }
    for (x = 0; x < 1024; x++) {
        obj = &sim->vip.objects[x];
        obj->cell.palette = sim->vip.jplt[0];
        obj->cell.pixels  = sim->vip.characters[0][0];
        obj->jp           = 0;
        obj->jx           = 0;
        obj->jy           = 0;
        obj->lron         = 0;
    }

    /* Display processor extra (the hardware does not do this) */
    sim->vip.dp.fclk    = 0;
    sim->vip.dp.l0bsy   = 0;
    sim->vip.dp.l1bsy   = 0;
    sim->vip.dp.lock    = 0;
    sim->vip.dp.r0bsy   = 0;
    sim->vip.dp.r1bsy   = 0;
    sim->vip.dp.scanrdy = 1;

    /* Pixel processor extra (the hardware does not do this) */
    sim->vip.xp.f0bsy    = 0;
    sim->vip.xp.f1bsy    = 0;
    sim->vip.xp.overtime = 0;
    sim->vip.xp.sbcmp    = 0;
    sim->vip.xp.sbcount  = 0;
    sim->vip.xp.sbout    = 0;

    /* Other */
    sim->vip.buffer = 0;
    for (x = 0; x < 2; x++)
    for (y = 0; y < 2; y++)
    for (z = 0; z < 384*224; z++)
        sim->vip.output[x][y][z] = 0;

    /* Display processor other */
    sim->vip.dp.brt[0] = 0;
    sim->vip.dp.buffer = 0; /* TODO: Hardware might actually do this */
    sim->vip.dp.clocks = 0;
    sim->vip.dp.step   = 0;
    sim->vip.dp.until  = 0;

    /* Pixel processor other */
    sim->vip.xp.clocks = 0;
    sim->vip.xp.step   = 0;
    sim->vip.xp.toggle = 0;
    sim->vip.xp.until  = 0;
}

/* Determine how many clocks are guaranteed to process */
static uint32_t vipUntil(VB *sim, uint32_t clocks) {
    if (clocks > sim->vip.dp.until)
        clocks = sim->vip.dp.until;
    if (sim->vip.xp.step != 0 && clocks > sim->vip.xp.until)
        clocks = sim->vip.xp.until;
    return clocks;
}

/* Write a typed value to the VIP bus */
static void vipWrite(VB*sim,uint32_t address,int type,int32_t value,int debug){

    /* Working variables */
    address &= 0x0007FFFF;

    /* RAM */
    if (address < 0x40000) {
        busWriteBuffer(&sim->vip.ram[address], type, value);

        /* Character memory */
        if ((address & 0x26000) == 0x06000) {
            address = (address & 0x18000) >> 2 | (address & 0x01FFF);
            vipWriteChr(sim, address, type, value);
        }

        /* BG map memory */
        if (address >= 0x20000)
            vipWriteCell(sim, address & 0x1FFFF, type, value);

        /* World memory */
        if ((address & 0x3FC00) == 0x3D800)
            vipWriteWorld(sim, address &0x003FF, type, value);

        /* Object memory */
        if (address >= 0x3E000)
            vipWriteObject(sim, address & 0x01FFF, type, value);
    }

    /* Unmapped */
    else if (address < 0x5E000)
        ;

    /* I/O register */
    else if (address < 0x60000)
        vipWriteIO(sim, address, type, value, debug);

    /* Unmapped */
    else if (address < 0x78000)
        ;

    /* Mirrors of character memory */
    else {
        vipWriteChr(sim, address & 0x07FFF, type, value);
        address = 0x06000 | (address << 2 & 0x18000) | (address & 0x01FFF);
        busWriteBuffer(&sim->vip.ram[address], type, value);
    }

}



#endif /* VBAPI */
