/* This file is included into vb.c and cannot be compiled on its own. */
#ifdef VBAPI



/********************************* Constants *********************************/

/* Analog output high-pass filter coefficient = 0.022 / (0.022 + 1 / 41700) */
#define RC_A (float) (1 - 1 / 918.4)



/******************************** Lookup Data ********************************/

/* Noise feedback bit positions by tap field */
static uint8_t NOISE_TAPS[] = { 14, 10, 13, 4, 8, 6, 9, 11 };

/* Clock flags per output sample -- 479 clocks plus bit value */
/* Ensures exactly 200,000 clocks every 417 samples = 41,700 Hz */
static uint32_t SAMPLE_CLOCKS[] = {
    0xD6B6B5B5, 0x5ADAD6D6, 0x6D6B6B5B, 0xB5B5ADAD,
    0xD6D6B6B5, 0x6B5B5ADA, 0xAD6D6D6B, 0xB6B5B5AD,
    0x5ADAD6D6, 0x6B6B5B5B, 0xB5ADAD6D, 0xD6D6B6B5,
    0x5B5ADAD6, 1
};



/***************************** Callback Handlers *****************************/

/* Samples buffer is full */
#ifndef VB_DIRECT_SAMPLES
    #define VB_ON_SAMPLES sim->onSamples
#else
    extern void VB_DIRECT_SAMPLES(VB *, void *, uint32_t);
    #define VB_ON_SAMPLES VB_DIRECT_SAMPLES
#endif
static void vsuOnSamples(VB *sim) {
    if (sim->onSamples != NULL)
        VB_ON_SAMPLES(sim, sim->vsu.out.samples, sim->vsu.out.capacity);
}
#undef VB_ON_SAMPLES



/***************************** Module Functions ******************************/

/* Determine while frequency modifications should be processed */
static uint8_t vsuCheckFreqMod(VB *sim) {
    return
        sim->vsu.freqmod.enb           &&
        sim->vsu.freqmod.interval != 0 && (
            sim->vsu.freqmod.modmask != 2 || (
                sim->vsu.freqmod.func == 1 &&
                sim->vsu.freqmod.rep
            )
        )
    ;
}

/* Stage the next frequency modification value */
static void vsuNextFreqMod(VB *sim, Channel *chan) {
    int32_t next;

    /* Modulation mask control */
    if (sim->vsu.freqmod.modmask == 1 && sim->vsu.freqmod.func == 1) {
        sim->vsu.freqmod.modmask = 2;
        chan->freqmod = vsuCheckFreqMod(sim);
    }

    /* Sweep */
    if (sim->vsu.freqmod.func == 0) {
        next = chan->freq.current >> sim->vsu.freqmod.shift;
        if (sim->vsu.freqmod.dir == 0)
             next = chan->freq.current - next;
        else next = chan->freq.current + next;
        if (next > 2047)
            chan->int_.enb = 0;
        if (next < 0)
            next = 0;
    }

    /* Modulation */
    else {
        next = ((int32_t) chan->freq.written +
            sim->vsu.modulation[sim->vsu.freqmod.sample]) & 0x7FF;
        if (sim->vsu.freqmod.freqmask == 1)
            next = (next & 0x700) | (chan->freq.written & 0x0FF);
        else if (sim->vsu.freqmod.freqmask == 2)
            next = (next & 0x0FF) | (chan->freq.written & 0x700);
    }

    /* Configure state */
    if (chan->freqmod)
        sim->vsu.freqmod.next = next;
}

/* Advance the noise channel's shift register */
static void vsuNextNoise(VB *sim) {
    uint32_t bit = ((
        sim->vsu.noise.register_ >> NOISE_TAPS[sim->vsu.noise.tap] ^
        sim->vsu.noise.register_ >> 7
    ) & 1) ^ 1;
    sim->vsu.noise.register_ = bit |
        (sim->vsu.noise.register_ << 1 & 0x7FFE);
}

/* Process one channel */
static void vsuEmulateChannel(VB *sim, Channel *chan) {

    /* Automatic shutoff */
    if (chan->int_.auto_ && chan->int_.clocks == 0) {
        chan->int_.enb = 0;
        return;
    }

    /* Next input sample */
    if (chan->clocks == 0) {

        /* Wave */
        if (chan != &sim->vsu.channels[5]) {
            chan->clocks =  4 * (2048 - (uint32_t) chan->freq.current);
            chan->wave.sample = (chan->wave.sample + 1) & 31;
        }

        /* Noise */
        else {
            chan->clocks = 40 * (2048 - (uint32_t) chan->freq.current);
            vsuNextNoise(sim);
        }

    }

    /* Envelope modification */
    if (chan->env.clocks == 0) {
        uint8_t new_envelope = chan->env.value;
        if (chan->env.dir == 0 && chan->env.value != 0)
            new_envelope--;
        else if (chan->env.dir == 1 && chan->env.value != 15)
            new_envelope++;
        else if (chan->env.rep && chan->env.modmask != 2) {
            new_envelope = chan->env.reload;
            chan->env.modmask = 0;
        } else if (!chan->env.modmask)
            chan->env.modmask = 1;
        if (chan->env.enb && !chan->env.modmask)
            chan->env.value = new_envelope;
        chan->env.clocks = ((uint32_t) chan->env.interval + 1) * 307220;
    }

    /* Frequency modification */
    if (chan == &sim->vsu.channels[4] && sim->vsu.freqmod.clocks == 0) {

        /* Apply previously computed frequency modification */
        if (chan->freqmod)
            chan->freq.current = sim->vsu.freqmod.next;

        /* Channel itself is disabled */
        if (!chan->int_.enb)
            return;

        /* Frequency modifications are enabled */
        if (chan->freqmod) {

            /* Not the final modulation sample */
            if (sim->vsu.freqmod.sample != 31)
                sim->vsu.freqmod.sample++;

            /* Last modulation sample processed */
            else {
                sim->vsu.freqmod.modmask = 1;
                if (sim->vsu.freqmod.rep)
                    sim->vsu.freqmod.sample = 0;
            }

            /* Schedule next modification */
            sim->vsu.freqmod.clocks = (uint32_t) sim->vsu.freqmod.interval *
                (sim->vsu.freqmod.clk == 0 ? 19200 : 153600);
        }

        /* Always process and validate next frequency value */
        vsuNextFreqMod(sim, chan);
    }

}

/* Produce output for a channel */
static void vsuOutputChannel(VB *sim, int index, uint16_t *output) {
    Channel *chan;   /* Channel handle */
    uint32_t level;  /* Stereo output level */
    uint32_t sample; /* Input sample */
    int      e;      /* Iterator */

    /* Select channel */
    chan = &sim->vsu.channels[index];

    /* Select input sample */
    sample =
        !chan->int_.enb     ? 0 :                              /* Disabled */
        index == 5 ? (sim->vsu.noise.register_ & 1) ? 63 : 0 : /* Noise */
        chan->wave.wave > 4 ? 0 :                              /* Wave range */
        sim->vsu.waves[chan->wave.wave][chan->wave.sample]     /* Wave */
    ;

    /* Compute output samples */
    for (e = 0; e < 2; e++) {
        level = e == 0 ? chan->lrv.left : chan->lrv.right;
        level = (level * chan->env.value >> 3) + (level && chan->env.value);
        output[e] += level * sample;
    }

}

/* Write a value to S*EV0 */
static void vsuWriteEV0(VB *sim, int index, uint8_t value) {
    Channel *chan = &sim->vsu.channels[index];

    /* Parse fields */
    chan->env.dir      = value >> 3 &  1;
    chan->env.interval = value      &  7;
    chan->env.value    = value >> 4 & 15;
    chan->env.reload   = chan->env.value;
    if (chan->env.modmask == 1) chan->env.modmask = 2;
    if (index == 4) {
        chan->freqmod = vsuCheckFreqMod(sim);
        vsuNextFreqMod(sim, chan);
    }

    /* Configure state */
    chan->env.clocks = 307220 * ((uint32_t) chan->env.interval + 1);
}

/* Write a value to S*EV1 */
static void vsuWriteEV1(VB *sim, int index, uint8_t value) {
    Channel *chan = &sim->vsu.channels[index];

    /* Parse fields */
    chan->env.enb = value      & 1;
    chan->env.rep = value >> 1 & 1;
    if (
        !chan->env.rep && (
            (chan->env.reload ==  0 && chan->env.dir == 0) ||
            (chan->env.reload == 15 && chan->env.dir == 1)
        )
    ) chan->env.modmask = 1;

    /* Processing by channel */
    switch (index) {

        case 4: /* Channel 5 */
            sim->vsu.freqmod.enb  = value >> 6 & 1;
            sim->vsu.freqmod.func = value >> 4 & 1;
            sim->vsu.freqmod.rep  = value >> 5 & 1;
            chan->freqmod         = vsuCheckFreqMod(sim);
            vsuNextFreqMod(sim, chan);
            break;

        case 5: /* Channel 6 */
            sim->vsu.noise.tap       = value >> 4 & 7;
            sim->vsu.noise.register_ = 0x0000;
            vsuNextNoise(sim);
    }

}

/* Write a value to S*FQH */
static void vsuWriteFQH(VB *sim, int index, uint16_t value) {
    Channel *chan = &sim->vsu.channels[index];
    value = value << 8 & 0x0700;
    chan->freq.current = (chan->freq.current & 0x00FF) | value;
    chan->freq.written = (chan->freq.written & 0x00FF) | value;
    if (index != 4)
        return;
    sim->vsu.freqmod.freqmask = 2;
    vsuNextFreqMod(sim, chan);
}

/* Write a value to S*FQL */
static void vsuWriteFQL(VB *sim, int index, uint8_t value) {
    Channel *chan = &sim->vsu.channels[index];
    chan->freq.current = (chan->freq.current & 0x0700) | value;
    chan->freq.written = (chan->freq.written & 0x0700) | value;
    if (index != 4)
        return;
    sim->vsu.freqmod.freqmask = 1;
    vsuNextFreqMod(sim, chan);
}

/* Write a value to S*INT */
static void vsuWriteINT(VB *sim, int index, uint8_t value) {
    Channel *chan = &sim->vsu.channels[index];

    /* Parse fields */
    chan->int_.auto_    = value >> 5 &  1;
    chan->int_.enb      = value >> 7 &  1;
    chan->int_.interval = value      & 31;

    /* Update state */
    chan->int_.clocks = 76805 * ((uint32_t) chan->int_.interval + 1);
    chan->env.modmask = 0;
    if (
        !chan->env.rep && (
            (chan->env.reload ==  0 && chan->env.dir == 0) ||
            (chan->env.reload == 15 && chan->env.dir == 1)
        )
    ) chan->env.modmask = 1;
    chan->env.clocks = 307220 * ((uint32_t) chan->env.interval + 1);
    if (index != 5) {
        chan->wave.sample = 0;
        chan->clocks =  4 * (2048 - (uint32_t) chan->freq.current);
    } else {
        sim->vsu.noise.register_ = 0x0000;
        vsuNextNoise(sim);
        chan->clocks = 40 * (2048 - (uint32_t) chan->freq.current);
    }
    if (index == 4) {
        sim->vsu.freqmod.clocks = (uint32_t) sim->vsu.freqmod.interval *
            (sim->vsu.freqmod.clk == 0 ? 19200 : 153600);
        sim->vsu.freqmod.modmask = 0;
        sim->vsu.freqmod.sample  = 0;
        chan->freqmod = vsuCheckFreqMod(sim);
        vsuNextFreqMod(sim, chan);
    }

}

/* Write a value to S*LRV */
static void vsuWriteLRV(VB *sim, int index, uint8_t value) {
    Channel *chan = &sim->vsu.channels[index];
    chan->lrv.left  = value >> 4 & 15;
    chan->lrv.right = value      & 15;
}

/* Write a value to S*RAM */
static void vsuWriteRAM(VB *sim, int index, uint8_t value) {
    sim->vsu.channels[index].wave.wave = value & 7;
}

/* Write a value to S5SWP */
static void vsuWriteSWP(VB *sim, uint8_t value) {
    uint32_t clocks;

    /* Parse fields */
    sim->vsu.freqmod.clk      = value >> 7 & 1;
    sim->vsu.freqmod.dir      = value >> 3 & 1;
    sim->vsu.freqmod.interval = value >> 4 & 7;
    sim->vsu.freqmod.shift    = value      & 7;

    /* Configure state */
    clocks = (uint32_t) sim->vsu.freqmod.interval *
        (sim->vsu.freqmod.clk == 0 ? 19200 : 153600);
    if (clocks < sim->vsu.freqmod.clocks)
        sim->vsu.freqmod.clocks = clocks;
    sim->vsu.channels[4].freqmod = vsuCheckFreqMod(sim);
    vsuNextFreqMod(sim, &sim->vsu.channels[4]);
}



/***************************** Module Functions ******************************/

/* Process component */
static void vsuEmulate(VB *sim, uint32_t clocks) {
    Channel *chan;      /* Input channel */
    uint32_t chantil;   /* Clocks until next channel state update */
    float    i0;        /* Current analog input sample */
    float    o0;        /* Current analog output sample */
    uint16_t output[2]; /* Digital output samples */
    uint32_t until;     /* Clocks to process channels */
    int      e, x;      /* Iterators */

    /* Process all clocks */
    do {

        /* Clocks until next sample */
        until = clocks;
        if (sim->vsu.clocks < until)
            until = sim->vsu.clocks;

        /* Manage clocks */
        clocks          -= until;
        sim->vsu.clocks -= until;

        /* Process all channels */
        for (x = 0; x < 6; x++) {
            chan    = &sim->vsu.channels[x];
            chantil = until;

            /* Process all clocks */
            while (chan->int_.enb && chantil != 0) {

                /* Determine when the next state change will occur */
                if (chan->until == 0) {

                    /* Clocks until next state change */
                    chan->until = chan->clocks;
                    if (chan->env.enb && chan->env.modmask != 2 &&
                        chan->env.clocks < chan->until)
                        chan->until = chan->env.clocks;
                    if (chan->int_.auto_ && chan->int_.clocks < chan->until)
                        chan->until = chan->int_.clocks;
                    if (chan->freqmod && sim->vsu.freqmod.clocks < chan->until)
                        chan->until = sim->vsu.freqmod.clocks;

                    /* Manage clocks */
                    chan->clocks -= chan->until;
                    if (chan->env.enb && chan->env.modmask != 2)
                        chan->env.clocks -= chan->until;
                    if (chan->int_.auto_)
                        chan->int_.clocks -= chan->until;
                    if (chan->freqmod)
                        sim->vsu.freqmod.clocks -= chan->until;
                }

                /* Manage clocks */
                if (chan->until > chantil) {
                    chan->until -= chantil;
                    chantil      = 0;
                } else {
                    chantil     -= chan->until;
                    chan->until  = 0;
                }

                /* Update channel state */
                if (chan->until == 0)
                    vsuEmulateChannel(sim, chan);
            }

        }

        /* Wait for the current sample to finish */
        if (sim->vsu.clocks != 0)
            return;

        /* Compute the output sample */
        output[0] = output[1] = 0;
        for (x = 0; x < 6; x++)
            vsuOutputChannel(sim, x, output);

        /* Output sample */
        for (e = 0; e < 2; e++) {

            /* Compute the output sample for this stereo channel */
            i0 = (output[e] >> 4) / 685.0f;
            o0 = RC_A * (sim->vsu.out.o1[e] + i0 - sim->vsu.out.i1[e]);
            if (o0 < -1.0f) o0 = -1.0f;
            if (o0 > +1.0f) o0 = +1.0f;
            sim->vsu.out.i1[e] = i0;
            sim->vsu.out.o1[e] = o0;

            /* Output the sample to caller memory */
            if (
                sim->vsu.out.samples != NULL &&
                sim->vsu.out.offset >> 1 < sim->vsu.out.capacity
            ) {

                /* Processing by data type */
                switch (sim->vsu.out.type) {
                    case VB_S16:
                        ((int16_t *) sim->vsu.out.samples)
                        [sim->vsu.out.offset++] = (int16_t)
                        (o0 * 32767.0f + (o0 < 0.0f ? -0.5f : +0.5f));
                        break;
                    case VB_F32:
                        ((float *) sim->vsu.out.samples)
                        [sim->vsu.out.offset++] = o0;
                        break;
                }

                /* Invoke the samples callback */
                if (
                    e == 1 &&
                    sim->vsu.out.offset >> 1 == sim->vsu.out.capacity
                ) vsuOnSamples(sim);
            }

        }

        /* Advance to the next sample */
        sim->vsu.sample += sim->vsu.sample == 416 ? -416 : 1;
        sim->vsu.clocks  = 479 + (1 &
            SAMPLE_CLOCKS[sim->vsu.sample >> 5] >> (sim->vsu.sample & 31));

    } while (clocks != 0);

}

/* Simulate a hardware reset */
static void vsuReset(VB *sim) {
    Channel *chan; /* Channel handle */
    int     x, y;  /* Iterators */

    /* Memory */
    for (x = 0; x < 32; x++) {
        sim->vsu.modulation[x] = 0;
        for (y = 0; y < 5; y++)
            sim->vsu.waves[y][x] = 0;
    }

    /* Channel state */
    for (x = 0; x < 6; x++) {
        chan = &sim->vsu.channels[x];
        chan->clocks        = 0;
        chan->freqmod       = 0;
        chan->until         = 0;
        chan->env.clocks    = 0;
        chan->env.enb       = 0;
        chan->env.dir       = 0;
        chan->env.interval  = 0;
        chan->env.modmask   = 0;
        chan->env.reload    = 0;
        chan->env.rep       = 0;
        chan->env.value     = 0;
        chan->freq.current  = 0x000;
        chan->freq.written  = 0x000;
        chan->lrv.left      = 0;
        chan->lrv.right     = 0;
        chan->int_.auto_    = 0;
        chan->int_.clocks   = 0;
        chan->int_.enb      = 0;
        chan->int_.interval = 0;
        chan->wave.sample   = 0;
        chan->wave.wave     = 0;
    }

    /* Other channel state */
    sim->vsu.freqmod.clk      = 0;
    sim->vsu.freqmod.clocks   = 0;
    sim->vsu.freqmod.dir      = 0;
    sim->vsu.freqmod.enb      = 0;
    sim->vsu.freqmod.freqmask = 0;
    sim->vsu.freqmod.func     = 0;
    sim->vsu.freqmod.interval = 0;
    sim->vsu.freqmod.modmask  = 0;
    sim->vsu.freqmod.next     = 0;
    sim->vsu.freqmod.rep      = 0;
    sim->vsu.freqmod.sample   = 0;
    sim->vsu.freqmod.shift    = 0;
    sim->vsu.noise.register_  = 0;
    sim->vsu.noise.tap        = 0;

    /* Other */
    sim->vsu.clocks = 479 + (SAMPLE_CLOCKS[0] & 1);
    sim->vsu.sample = 0;
    for (x = 0; x < 2; x++)
        sim->vsu.out.i1[x] = sim->vsu.out.o1[x] = 0;
}

/* Write a typed value to the VSU bus */
static void vsuWrite(VB*sim,uint32_t address,int type,int32_t value,int debug){
    (void) type;

    /* Unmapped */
    if (address & 1)
        return;

    /* Working variables */
    address &= 0x7FC;

    /* Clear frequency mask */
    sim->vsu.freqmod.freqmask = 0;

    /* Wave memory */
    if (address < 0x280) {
        if (debug || !(
            sim->vsu.channels[0].int_.enb |
            sim->vsu.channels[1].int_.enb |
            sim->vsu.channels[2].int_.enb |
            sim->vsu.channels[3].int_.enb |
            sim->vsu.channels[4].int_.enb |
            sim->vsu.channels[5].int_.enb
        )) sim->vsu.waves[address >> 7][address >> 2 & 31] = value & 63;
    }

    /* Modulation memory */
    else if (address < 0x300) {
        if (debug || !sim->vsu.channels[4].int_.enb)
            sim->vsu.modulation[address >> 2 & 31] = value;
    }

    /* I/O register */
    else switch (address >> 2) {

        case 0x400>>2: vsuWriteINT(sim, 0, value); break; /* S1INT */
        case 0x404>>2: vsuWriteLRV(sim, 0, value); break; /* S1LRV */
        case 0x408>>2: vsuWriteFQL(sim, 0, value); break; /* S1FQL */
        case 0x40C>>2: vsuWriteFQH(sim, 0, value); break; /* S1FQH */
        case 0x410>>2: vsuWriteEV0(sim, 0, value); break; /* S1EV0 */
        case 0x414>>2: vsuWriteEV1(sim, 0, value); break; /* S1EV1 */
        case 0x418>>2: vsuWriteRAM(sim, 0, value); break; /* S1RAM */

        case 0x440>>2: vsuWriteINT(sim, 1, value); break; /* S2INT */
        case 0x444>>2: vsuWriteLRV(sim, 1, value); break; /* S2LRV */
        case 0x448>>2: vsuWriteFQL(sim, 1, value); break; /* S2FQL */
        case 0x44C>>2: vsuWriteFQH(sim, 1, value); break; /* S2FQH */
        case 0x450>>2: vsuWriteEV0(sim, 1, value); break; /* S2EV0 */
        case 0x454>>2: vsuWriteEV1(sim, 1, value); break; /* S2EV1 */
        case 0x458>>2: vsuWriteRAM(sim, 1, value); break; /* S2RAM */

        case 0x480>>2: vsuWriteINT(sim, 2, value); break; /* S3INT */
        case 0x484>>2: vsuWriteLRV(sim, 2, value); break; /* S3LRV */
        case 0x488>>2: vsuWriteFQL(sim, 2, value); break; /* S3FQL */
        case 0x48C>>2: vsuWriteFQH(sim, 2, value); break; /* S3FQH */
        case 0x490>>2: vsuWriteEV0(sim, 2, value); break; /* S3EV0 */
        case 0x494>>2: vsuWriteEV1(sim, 2, value); break; /* S3EV1 */
        case 0x498>>2: vsuWriteRAM(sim, 2, value); break; /* S3RAM */

        case 0x4C0>>2: vsuWriteINT(sim, 3, value); break; /* S4INT */
        case 0x4C4>>2: vsuWriteLRV(sim, 3, value); break; /* S4LRV */
        case 0x4C8>>2: vsuWriteFQL(sim, 3, value); break; /* S4FQL */
        case 0x4CC>>2: vsuWriteFQH(sim, 3, value); break; /* S4FQH */
        case 0x4D0>>2: vsuWriteEV0(sim, 3, value); break; /* S4EV0 */
        case 0x4D4>>2: vsuWriteEV1(sim, 3, value); break; /* S4EV1 */
        case 0x4D8>>2: vsuWriteRAM(sim, 3, value); break; /* S4RAM */

        case 0x500>>2: vsuWriteINT(sim, 4, value); break; /* S5INT */
        case 0x504>>2: vsuWriteLRV(sim, 4, value); break; /* S5LRV */
        case 0x508>>2: vsuWriteFQL(sim, 4, value); break; /* S5FQL */
        case 0x50C>>2: vsuWriteFQH(sim, 4, value); break; /* S5FQH */
        case 0x510>>2: vsuWriteEV0(sim, 4, value); break; /* S5EV0 */
        case 0x514>>2: vsuWriteEV1(sim, 4, value); break; /* S5EV1 */
        case 0x518>>2: vsuWriteRAM(sim, 4, value); break; /* S5RAM */
        case 0x51C>>2: vsuWriteSWP(sim,    value); break; /* S5SWP */

        case 0x540>>2: vsuWriteINT(sim, 5, value); break; /* S6INT */
        case 0x544>>2: vsuWriteLRV(sim, 5, value); break; /* S6LRV */
        case 0x548>>2: vsuWriteFQL(sim, 5, value); break; /* S6FQL */
        case 0x54C>>2: vsuWriteFQH(sim, 5, value); break; /* S6FQH */
        case 0x550>>2: vsuWriteEV0(sim, 5, value); break; /* S6EV0 */
        case 0x554>>2: vsuWriteEV1(sim, 5, value); break; /* S6EV1 */

        case 0x580>>2: /* SSTOP */
            if ((value & 1) == 0)
                break;
            sim->vsu.channels[0].int_.enb =
            sim->vsu.channels[1].int_.enb =
            sim->vsu.channels[2].int_.enb =
            sim->vsu.channels[3].int_.enb =
            sim->vsu.channels[4].int_.enb =
            sim->vsu.channels[5].int_.enb = 0;
    }

}



#endif /* VBAPI */
