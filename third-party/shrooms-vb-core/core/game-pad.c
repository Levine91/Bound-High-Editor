/* This file is included into vb.c and cannot be compiled on its own. */
#ifdef VBAPI



/***************************** Library Functions *****************************/

/* Process component */
static void padEmulate(VB *sim, uint32_t clocks) {

    /* No hardware read in progress */
    if (!sim->pad.si_stat)
        return;

    /* Hardware read completes after time to process */
    if (sim->pad.clocks > clocks) {
        sim->pad.clocks -= clocks;
        return;
    }

    /* Complete hardware read */
    sim->pad.si_stat = 0;
    sim->pad.sdlr    = sim->pad.keys;
    sim->pad.sdhr    = sim->pad.keys >> 8;

    /* Raise game pad interrupt */
    if (
        !sim->pad.k_int_inh           &&
        (sim->pad.keys & 0xFFF0) != 0 &&
        (sim->pad.keys & 0x000E) == 0
    ) sim->cpu.irq |= 0x0001;

}

/* Read a value from SCR */
static int32_t padReadControl(VB *sim) {
    return 0x4C |
        sim->pad.k_int_inh << 7 |
        sim->pad.para_si   << 5 |
        sim->pad.soft_ck   << 4 |
        sim->pad.si_stat   << 1 |
        sim->pad.s_abt_dis
    ;
}

/* Simulate a hardware reset */
static void padReset(VB *sim) {

    /* Normal */
    sim->pad.k_int_inh = 0;
    sim->pad.para_si   = 0;
    sim->pad.s_abt_dis = 0;
    sim->pad.sdhr      = 0x00;
    sim->pad.sdlr      = 0x00;
    sim->pad.si_stat   = 0;
    sim->pad.soft_ck   = 0;

    /* Other */
    sim->pad.step = 0;
}

/* Write a value to SCR */
static void padWriteControl(VB *sim, uint8_t value) {

    /* Acknowledge and disable interrupt */
    sim->pad.k_int_inh = value >> 7 & 1;
    if (sim->pad.k_int_inh)
        sim->cpu.irq &= ~0x0001;

    /* Enable or disable hardware reads */
    sim->pad.s_abt_dis = value & 1;
    if (sim->pad.s_abt_dis)
        sim->pad.si_stat = 0; /* Abort hardware read */

    /* Initiate a hardware read */
    if (
        (value & 0x04)        && /* HW-SI */
        sim->pad.si_stat == 0 && /* No hardware read underway */
        !sim->pad.s_abt_dis      /* Hardware reads enabled */
    ) {
        sim->pad.clocks  = 10240; /* 512us */
        sim->pad.si_stat = 1;
        sim->pad.step    = 0;     /* Abort software read */
    }

    /* Reset the read operation */
    sim->pad.para_si = value >> 5 & 1;
    if (
        sim->pad.para_si &&
        !sim->pad.si_stat /* No hardware read underway */
    ) sim->pad.step = 32;

    /* Signal a bit for a software read */
    sim->pad.soft_ck = value >> 4 & 1;
    if (
        sim->pad.step != 0 && /* Software read is underway */
        !sim->pad.para_si  && /* Not resetting software read */
        ((sim->pad.soft_ck ^ sim->pad.step) & 1) == 1 /* Advance */
    ) {
        sim->pad.step--;
        if (sim->pad.step == 0) {
            sim->pad.sdlr = sim->pad.keys;
            sim->pad.sdhr = sim->pad.keys >> 8;
        }
    }

}

/* Determine how many clocks are guaranteed to process */
static uint32_t padUntil(VB *sim, uint32_t clocks) {
    return sim->pad.si_stat && sim->pad.clocks < clocks ?
        sim->pad.clocks : clocks;
}



#endif /* VBAPI */
