/* This file is included into vb.c and cannot be compiled on its own. */
#ifdef VBAPI



/***************************** Module Functions ******************************/

/* Compute clocks until the next decrement to zero */
static uint32_t tmrGetUntil(VB *sim) {
    uint32_t fullTick = sim->tmr.t_clk_sel ? 400 : 2000;
    uint32_t thisTick = sim->tmr.clocks +
        (sim->tmr.t_clk_sel ? 0 : 400 * (4 - sim->tmr.tick20));
    return thisTick + fullTick *
        (sim->tmr.counter == 0 ? sim->tmr.reload : sim->tmr.counter - 1);
}

/* Update the counter to a new value */
static void tmrUpdate(VB *sim, uint16_t value) {
    if (value == 0 && sim->tmr.counter != 0) {
        if (sim->tmr.t_enb)
            sim->tmr.z_stat = 1;
        if (sim->tmr.tim_z_int)
            sim->cpu.irq |= 0x0002;
    }
    sim->tmr.counter = value;
}



/***************************** Library Functions *****************************/

/* Process component */
static void tmrEmulate(VB *sim, uint32_t clocks) {

    /* Process all clocks */
    for (;;) {

        /* Next tick is after time to process */
        if (sim->tmr.clocks > clocks) {
            sim->tmr.clocks -= clocks;
            sim->tmr.until  -= clocks;
            return;
        }

        /* Advance forward the component's number of clocks */
        clocks          -= sim->tmr.clocks;
        sim->tmr.until  -= sim->tmr.clocks;
        sim->tmr.clocks  = 400;
        sim->tmr.tick20 += sim->tmr.tick20 == 4 ? -4 : 1;

        /* Do not decrement counter */
        if (
            !sim->tmr.t_enb ||
            (!sim->tmr.t_clk_sel && sim->tmr.tick20 != 0)
        ) continue;

        /* Advance to the next counter value */
        tmrUpdate(sim, sim->tmr.counter == 0 ?
            sim->tmr.reload : sim->tmr.counter - 1);
        if (sim->tmr.counter == 0)
            sim->tmr.until = tmrGetUntil(sim);
    }

}

/* Read a value from TCR */
static int32_t tmrReadControl(VB *sim) {
    return 0xE4 |
        sim->tmr.t_clk_sel << 4 |
        sim->tmr.tim_z_int << 3 |
        sim->tmr.z_stat    << 1 |
        sim->tmr.t_enb
    ;
}

/* Simulate a hardware reset */
static void tmrReset(VB *sim) {

    /* Normal */
    sim->tmr.t_enb     = 0;
    sim->tmr.t_clk_sel = 0;
    sim->tmr.tim_z_int = 0;
    sim->tmr.z_stat    = 0;

    /* Other */
    sim->tmr.clocks  = 400;
    sim->tmr.counter = 0xFFFF;
    sim->tmr.reload  = 0x0000;
    sim->tmr.tick20  = 0;
}

/* Determine how many clocks are guaranteed to process */
static uint32_t tmrUntil(VB *sim, uint32_t clocks) {
    return sim->tmr.t_enb && sim->tmr.until < clocks ?
        sim->tmr.until : clocks;
}

/* Write a value to TCR */
static void tmrWriteControl(VB *sim, uint8_t value) {

    /* Clear interrupt status */
    if (
        (value & 0x04) && /* Z-Stat-Clr */
        (
            !sim->tmr.t_enb ||
            (
                sim->tmr.counter != 0 &&
                !(value & 0x01) /* T-Enb */
            )
        )
    ) {
        sim->tmr.z_stat = sim->tmr.counter != 0;
        sim->cpu.irq   &= ~0x0002;
    }

    /* Hardware bug: decrement on switch to 20us mode */
    if (
        !sim->tmr.t_clk_sel &&
        (value & 0x10)      && /* T-Clk-Sel */
        sim->tmr.tick20 != 0
    ) {
        tmrUpdate(sim, sim->tmr.counter == 0 ?
            sim->tmr.reload : sim->tmr.counter - 1);
    }

    /* Parse fields */
    sim->tmr.t_clk_sel = value >> 4 & 1;
    sim->tmr.tim_z_int = value >> 3 & 1;
    sim->tmr.t_enb     = value      & 1;

    /* The interrupt is disabled */
    if (!sim->tmr.tim_z_int)
        sim->cpu.irq &= ~0x0002;

    /* Configure state */
    sim->tmr.until = tmrGetUntil(sim);

    /* TODO: Will Z-Stat raise an interrupt when Tim-Z-Int is set? */
}

/* Write to the high data register */
static void tmrWriteHigh(VB *sim, uint8_t value) {
    sim->tmr.reload = (uint16_t) value << 8 | (sim->tmr.reload & 0x00FF);
    tmrUpdate(sim, sim->tmr.reload);
    sim->tmr.until  = tmrGetUntil(sim);
}

/* Write to the low data register */
static void tmrWriteLow(VB *sim, uint8_t value) {
    sim->tmr.reload = (sim->tmr.reload & 0xFF00) | value;
    tmrUpdate(sim, sim->tmr.reload);
    sim->tmr.until  = tmrGetUntil(sim);
}



#endif /* VBAPI */
