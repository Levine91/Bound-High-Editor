/* This file is included into vb.c and cannot be compiled on its own. */
#ifdef VBAPI



/***************************** Callback Handlers *****************************/

/* Prepare to handle an exception */
#ifndef VB_DIRECT_LINK
    #define VB_ON_LINK sim1->onLink
#else
    extern int VB_DIRECT_LINK(VB *, VB *, uint8_t *, uint8_t *);
    #define VB_ON_LINK VB_DIRECT_LINK
#endif
static int extOnLink(VB *sim1, VB *sim2, uint8_t *value1, uint8_t *value2) {
    return sim1->onLink != NULL && VB_ON_LINK(sim1, sim2, value1, value2);
}
#undef VB_ON_LINK



/***************************** Library Functions *****************************/

/* Process component */
static int extEmulate(VB *sim, uint32_t clocks) {
    VB     *peer;      /* Communication peer */
    VB     *sims[2];   /* Communication peers */
    uint8_t values[2]; /* Transmission values */
    int     x;         /* Iterator */

    /* Communication will not occur */
    if (!sim->ext.c_stat || sim->ext.c_clk_sel)
        return 0;

    /* Communication completes after time to process */
    if (sim->ext.clocks > clocks) {
        sim->ext.clocks -= clocks;
        return 0;
    }

    /* Exchange transmission data */
    sims  [0] = sim;
    sims  [1] = sim->peer;
    values[0] = sim->ext.cdtr;
    values[1] = sim->peer == NULL ? 0 : sim->peer->ext.cdtr;
    if (extOnLink(sim, sim->peer, &values[0], &values[1]))
        return 1;

    /* Update state */
    for (x = 0; x < 2; x++) {
        sim  = sims[x];
        peer = sims[x ^ 1];
        if (sim  == NULL)
            break;
        if (peer == NULL)
            peer = sim;

        /* Registers */
        sim->ext.cdrr    = values[x ^ 1];
        sim->ext.c_irq  |= !sim->ext.c_int_inh;
        sim->ext.c_stat  = 0;
        sim->ext.cc_rd   = sim->ext.cc_wr  & peer->ext.cc_wr;
        sim->ext.cc_smp  = sim->ext.cc_sig & peer->ext.cc_sig & sim->ext.cc_rd;
        sim->ext.cc_irq |= !sim->ext.cc_int_inh &&
            sim->ext.cc_smp == sim->ext.cc_int_lev;

        /* Interrupt */
        if (sim->ext.c_irq | sim->ext.cc_irq)
            sim->cpu.irq |= 0x0008;
    }
    return 0;
}

/* Read a value from CCR */
static int32_t extReadCCR(VB *sim) {
    return 0x6D |
        sim->ext.c_int_inh << 7 |
        sim->ext.c_clk_sel << 4 |
        sim->ext.c_stat    << 1
    ;
}

/* Read a value from CCSR */
static int32_t extReadCCSR(VB *sim) {
    sim->ext.cc_rd = sim->ext.cc_wr &
        (sim->peer == NULL ? 1 : sim->peer->ext.cc_wr);
    return 0x60 |
        sim->ext.cc_int_inh << 7 |
        sim->ext.cc_int_lev << 4 |
        sim->ext.cc_sig     << 3 |
        sim->ext.cc_smp     << 2 |
        sim->ext.cc_wr      << 1 |
        sim->ext.cc_rd
    ;
}

/* Simulate a hardware reset */
static void extReset(VB *sim) {

    /* Normal */
    sim->ext.c_clk_sel  = 0;
    sim->ext.c_int_inh  = 0;
    sim->ext.c_stat     = 0;
    sim->ext.cc_int_inh = 1;
    sim->ext.cc_int_lev = 1;
    sim->ext.cc_sig     = 1;
    sim->ext.cc_wr      = 1;
    sim->ext.cdrr       = 0x00;
    sim->ext.cdtr       = 0x00;

    /* Other */
    sim->ext.c_irq  = 0;
    sim->ext.cc_irq = 0;
}

/* Determine how many clocks are guaranteed to process */
static uint32_t extUntil(VB *sim, uint32_t clocks) {
    return !sim->ext.c_stat || sim->ext.c_clk_sel || clocks < sim->ext.clocks ?
        clocks : sim->ext.clocks;
}

/* Write a value to CCR */
static void extWriteCCR(VB *sim, uint8_t value) {

    /* Configure state */
    sim->ext.c_int_inh = value >> 7 & 1;
    sim->ext.c_clk_sel = value >> 4 & 1;

    /* Acknowledge interrupt */
    if (sim->ext.c_int_inh) {
        sim->ext.c_irq = 0;
        if (!sim->ext.cc_irq)
            sim->cpu.irq &= ~0x0008;
    }

    /* Do not initiate a communication */
    if (sim->ext.c_stat || (value & 0x04) == 0) /* C-Start */
        return;

    /* Initiate a communication */
    sim->ext.c_stat = 1;
    sim->ext.clocks = 3200; /* 160us */
}

/* Write a value to CCSR */
static void extWriteCCSR(VB *sim, uint8_t value) {

    /* Configure state */
    sim->ext.cc_int_inh = value >> 7 & 1;
    sim->ext.cc_int_lev = value >> 4 & 1;
    sim->ext.cc_sig     = value >> 3 & 1;
    sim->ext.cc_wr      = value >> 1 & 1;

    /* Acknowledge interrupt */
    if (!sim->ext.cc_int_inh)
        return;
    sim->ext.cc_irq = 0;
    if (!sim->ext.c_irq)
        sim->cpu.irq &= ~0x0008;
}



#endif /* VBAPI */
