using UnityEngine;

// House-style PROCEDURAL sound effects (no audio files — clips are synthesized in code from
// oscillators, filtered noise + a tiny reverb, the same "generate the data ourselves" idea as the
// procedural sprites in CardAimIndicator / SpitGlob). Clips are baked once on first access and
// cached, so playback is a normal AudioClip through SfxManager.PlayOn.
//
// Usage:  SfxManager.PlayOn(audioSource, ProcSfx.Jump);
//
// DESIGN NOTE (2026-07-17): the "jump" action SPENDS Shift — the arcane kinetic resource — so it
// is a SHIFT (arcane displacement), NOT a physical jump (no thump). Rejected directions, in order:
// a jump/thump (too physical), a high shimmer (annoying), a warm dark thump (still a jump), and a
// flanger+doppler teleport (too ELECTRIC/sci-fi for a candlelit alchemist's den). This pass is
// ORGANIC + ARCANE: filtered air displacement ("fwsh") + a warm struck-glass/resonant bloom (a
// nod to the potion bottles in the room) — sine-based and noise-based only, no synthy flanger or
// laser sweep. Three styles are provided to A/B (JumpSfxPreviewBaker). Live sound = DefaultStyle.
public static class ProcSfx
{
    private const int SampleRate = 44100;

    public enum ShiftStyle { Glass, Breath, Bloom }
    // Bloom was the closest of the three to the designer's target (2026-07-17) — the resume point
    // when the "shift" SFX hunt continues. (Currently the live jump uses the original mp3, not this.)
    public static readonly ShiftStyle DefaultStyle = ShiftStyle.Bloom;

    private static AudioClip jump;
    public static AudioClip Jump
    {
        get
        {
            if (jump == null) jump = BuildShift(DefaultStyle);
            return jump;
        }
    }

    public static AudioClip BuildShift(ShiftStyle style)
    {
        switch (style)
        {
            case ShiftStyle.Breath: return BuildBreath();
            case ShiftStyle.Bloom:  return BuildBloom();
            default:                return BuildGlass();
        }
    }

    // A — GLASS: a soft air "fwsh" of displacement + a warm struck-glass resonance (potion-bottle
    // nod). Organic and magical, a little bright but warm — "displace + soft enchanted ring".
    private static AudioClip BuildGlass()
    {
        const float dur = 0.45f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(9021);

        float svfLow = 0f, svfBand = 0f;   // band-pass on the air
        float f0 = 430f;                   // glass fundamental
        float[] ratio = { 1f, 2.38f, 3.94f };   // struck-glass-ish partials (kept low/warm)
        float[] pAmp  = { 1f, 0.42f, 0.18f };
        float[] pDec  = { 6f, 10f, 15f };        // higher partials die faster (natural)

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            // air displacement: band-pass noise whose center falls (a settling "fwsh").
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(1700f, 620f, ts / dur);
            float f = 2f * Mathf.Sin(Mathf.PI * cutoff / SampleRate);
            svfLow += f * svfBand;
            float high = noise - svfLow - 0.42f * svfBand;
            svfBand += f * high;
            float airEnv = Mathf.Clamp01(ts / 0.006f) * Mathf.Exp(-16f * ts);

            // struck-glass bloom.
            float bell = 0f;
            for (int k = 0; k < ratio.Length; k++)
                bell += pAmp[k] * Mathf.Sin(2f * Mathf.PI * f0 * ratio[k] * ts) * Mathf.Exp(-pDec[k] * ts);
            float bellAtk = Mathf.Clamp01(ts / 0.003f);

            float sub = Mathf.Sin(2f * Mathf.PI * 92f * ts) * Mathf.Exp(-28f * ts);

            dry[i] = svfBand * airEnv * 0.16f
                   + bell * bellAtk * 0.13f
                   + sub * 0.08f;
        }

        return Finalize(dry, 0.10f, 2800f);
    }

    // B — BREATH: mostly warm air — a soft robe/air swish of displacement with only a whisper of
    // magical tone. The most organic/ethereal option, darker and breathier.
    private static AudioClip BuildBreath()
    {
        const float dur = 0.42f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5540);

        float lowLp = 0f, airLp = 0f;
        float lowCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 650f / SampleRate);
        float airCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 1500f / SampleRate);

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lowLp += lowCoef * (noise - lowLp);   // body of the breath
            airLp += airCoef * (noise - airLp);   // airy top

            // Soft swell-in then fade (a passing breath).
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(ts / dur), 0.45f));

            // Whisper of warm magic underneath.
            float tone = Mathf.Sin(2f * Mathf.PI * 320f * ts) * (1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 6f * ts));
            float toneEnv = Mathf.Clamp01(ts / 0.02f) * Mathf.Exp(-7f * ts);

            dry[i] = lowLp * env * 0.20f
                   + airLp * env * 0.12f
                   + tone * toneEnv * 0.06f;
        }

        return Finalize(dry, 0.13f, 2400f);
    }

    // C — BLOOM: a warm resonant magical swell — high-resonance band-pass noise "whooom" that
    // blooms in and fades, dreamy and enchanted, with a little air. More "spell energy" than air.
    private static AudioClip BuildBloom()
    {
        const float dur = 0.5f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(3312);

        float svfLow = 0f, svfBand = 0f;   // resonant band-pass = a pitched airy bloom
        float airLp = 0f;
        float airCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 1600f / SampleRate);

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // Pitched resonant bloom around ~440 Hz (high resonance = low q).
            float cutoff = Mathf.Lerp(360f, 520f, Mathf.Clamp01(ts / dur));
            float f = 2f * Mathf.Sin(Mathf.PI * cutoff / SampleRate);
            svfLow += f * svfBand;
            float high = noise - svfLow - 0.10f * svfBand;
            svfBand += f * high;
            // Swell IN over ~90ms, then a gentle fall — the magic gathering and releasing.
            float bloomEnv = Mathf.Clamp01(ts / 0.09f) * Mathf.Exp(-4.5f * ts);

            airLp += airCoef * (noise - airLp);
            float airEnv = Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(ts / dur), 0.5f));

            dry[i] = svfBand * bloomEnv * 0.16f
                   + airLp * airEnv * 0.09f;
        }

        return Finalize(dry, 0.14f, 2600f);
    }

    private static AudioClip scrapPickup;
    // Collecting a scrap shard. Procedural because ScrapPickup builds its GameObject entirely in
    // code (no prefab), so there is nowhere to hang an AudioClip in the Inspector.
    public static AudioClip ScrapPickup
    {
        get
        {
            if (scrapPickup == null) scrapPickup = BuildScrapPickup();
            return scrapPickup;
        }
    }

    // A small bright metal "tink" — a struck iron offcut, not a coin. Built from INHARMONIC
    // partials (the 1 : 2.76 : 5.40 : 8.93 ideal-bar mode ratios), which is what makes metal read
    // as metal rather than as a pitched chime; harmonic ratios here would sound like a bell and
    // collide with the gold pickup. Short and dry so a five-shard burst layers into a satisfying
    // rattle instead of a wash.
    private static AudioClip BuildScrapPickup()
    {
        const float dur = 0.22f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(4471);

        float f0 = 920f;                                    // small shard = high fundamental
        float[] ratio = { 1f, 2.76f, 5.40f, 8.93f };        // ideal free-bar modes
        float[] pAmp = { 1f, 0.55f, 0.28f, 0.12f };
        float[] pDec = { 22f, 30f, 42f, 58f };              // brighter modes die faster

        float clickLp = 0f;
        float clickCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 5200f / SampleRate);

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            // Struck partials.
            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * f0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            // Contact transient: a very short filtered noise tick that sells the "hit".
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            clickLp += clickCoef * (noise - clickLp);
            float clickEnv = Mathf.Exp(-190f * ts);

            dry[i] = body * 0.13f + clickLp * clickEnv * 0.20f;
        }

        return Finalize(dry, 0.07f, 9000f);   // dry and bright — metal, close-miked
    }

    private static AudioClip meteorImpact;

    // Meteor Greaves landing. A heavy stone impact, and deliberately a THIRD sound family from the
    // two already here: magic is harmonic (bell partials), metal is inharmonic-but-pitched (bar
    // modes), and this is barely pitched at all — mostly noise and sub. Stone shatters; it does not
    // ring. Keeping the families distinct is what stops a boulder landing and a scrap pickup
    // reading as the same event.
    public static AudioClip MeteorImpact
    {
        get { if (meteorImpact == null) meteorImpact = BuildMeteorImpact(); return meteorImpact; }
    }

    // Four layers, because a big impact is a sequence and not a single hit:
    //   CRACK   ~8 ms of bright noise — the fracture. Without this the hit has no attack and
    //           reads as distant rather than underfoot.
    //   SUB     a sine swept 110 -> 34 Hz. The downward sweep is what makes it feel like MASS
    //           arriving; a static low sine just sounds like a hum.
    //   BODY    three inharmonic low partials for the stone slab itself.
    //   DEBRIS  band-passed noise with a slow decay and a little flutter — rubble settling after,
    //           which is what sells the scale and stops the sound ending abruptly.
    private static AudioClip BuildMeteorImpact()
    {
        const float dur = 1.15f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(90210);

        // Body: inharmonic, low, and fast-decaying — a struck slab, not a bell.
        float b0 = 132f;
        float[] ratio = { 1f, 1.71f, 2.43f };
        float[] pAmp = { 1f, 0.42f, 0.20f };
        float[] pDec = { 11f, 17f, 26f };

        float crackLp = 0f;
        float crackCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 7000f / SampleRate);

        // Two one-pole stages in series make a crude band-pass for the debris.
        float debLp = 0f, debHp = 0f;
        float debLpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 1800f / SampleRate);
        float debHpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 260f / SampleRate);

        float subPhase = 0f;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // CRACK
            crackLp += crackCoef * (noise - crackLp);
            float crack = crackLp * Mathf.Exp(-320f * ts);

            // SUB — sweep the frequency, integrating phase so there are no discontinuities.
            float subF = Mathf.Lerp(110f, 34f, Mathf.Clamp01(ts / 0.30f));
            subPhase += 2f * Mathf.PI * subF / SampleRate;
            float sub = Mathf.Sin(subPhase) * Mathf.Exp(-5.5f * ts);

            // BODY
            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * b0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            // DEBRIS — rubble skittering, gated so it starts just after the hit.
            debLp += debLpCoef * (noise - debLp);
            debHp += debHpCoef * (debLp - debHp);
            float band = debLp - debHp;
            float flutter = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 27f * ts);
            float debrisEnv = Mathf.Exp(-4.2f * ts) * Mathf.Clamp01(ts / 0.02f);
            float debris = band * debrisEnv * flutter;

            dry[i] = crack * 0.30f + sub * 0.52f + body * 0.16f + debris * 0.22f;
        }

        return Finalize(dry, 0.26f, 5200f);   // wet and dark — a big room, heard from inside it
    }

    private static AudioClip arcaneGather, arcaneBind;

    // Blompo's blessing, part one: power gathering. A rising shimmer under the ring-and-motes
    // sequence, ending just as the blessing sets.
    public static AudioClip ArcaneGather
    {
        get { if (arcaneGather == null) arcaneGather = BuildArcaneGather(); return arcaneGather; }
    }

    // Blompo's blessing, part two: the bind. A struck chime that blooms.
    public static AudioClip ArcaneBind
    {
        get { if (arcaneBind == null) arcaneBind = BuildArcaneBind(); return arcaneBind; }
    }

    // Rising shimmer. Band-passed noise whose cutoff GLIDES upward, plus two sine partials sliding
    // up a fifth — the pitch rise is what makes it read as "something is building" rather than as
    // ambience. Swells to ~85% of its length so it peaks into the bind rather than after it.
    private static AudioClip BuildArcaneGather()
    {
        const float dur = 1.5f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(7717);

        float svfLow = 0f, svfBand = 0f;
        double p1 = 0.0, p2 = 0.0;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float t01 = ts / dur;

            // Resonant band-pass climbing 400 -> 2600 Hz.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(400f, 2600f, t01 * t01);
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(cutoff, SampleRate * 0.45f) / SampleRate);
            svfLow += f * svfBand;
            float high = noise - svfLow - 0.22f * svfBand;
            svfBand += f * high;

            // Two partials gliding up a fifth. Phase is integrated (not sin(2*pi*f*t)) so the
            // frequency can change without the waveform tearing.
            float g1 = Mathf.Lerp(392f, 587f, t01);
            float g2 = Mathf.Lerp(587f, 880f, t01);
            p1 += 2.0 * Mathf.PI * g1 / SampleRate;
            p2 += 2.0 * Mathf.PI * g2 / SampleRate;

            float swell = Mathf.Pow(Mathf.Clamp01(t01 / 0.85f), 1.6f);
            float tail = 1f - Mathf.Clamp01((t01 - 0.85f) / 0.15f) * 0.4f;
            float env = swell * tail;

            dry[i] = svfBand * env * 0.13f
                   + (float)System.Math.Sin(p1) * env * 0.045f
                   + (float)System.Math.Sin(p2) * env * 0.030f;
        }

        return Finalize(dry, 0.22f, 6000f);
    }

    // The bind: a struck chime that blooms open.
    //
    // HARMONIC partials on purpose (1, 2, 3, 4, 5.1) — that's what makes it read as a bell, i.e.
    // as magic. The scrap pickup deliberately uses INHARMONIC bar modes so it reads as metal; the
    // two sounds should never be confusable, and the partial ratios are the whole difference.
    private static AudioClip BuildArcaneBind()
    {
        const float dur = 1.8f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(2286);

        float f0 = 587.33f;                                    // D5
        float[] ratio = { 1f, 2f, 3f, 4f, 5.1f };              // 5.1 is stretched for shimmer
        float[] pAmp = { 1f, 0.50f, 0.30f, 0.16f, 0.09f };
        float[] pDec = { 2.6f, 3.6f, 5.0f, 7.0f, 9.0f };

        // A few high sparkles scattered through the first 250ms.
        int sparkCount = 7;
        float[] sparkAt = new float[sparkCount];
        float[] sparkHz = new float[sparkCount];
        for (int k = 0; k < sparkCount; k++)
        {
            sparkAt[k] = (float)rng.NextDouble() * 0.25f;
            sparkHz[k] = 1800f + (float)rng.NextDouble() * 2600f;
        }

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * f0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            // Sub bloom an octave down, swelling in rather than striking — the "opening" feeling.
            float sub = Mathf.Sin(2f * Mathf.PI * (f0 * 0.5f) * ts)
                      * Mathf.Clamp01(ts / 0.12f) * Mathf.Exp(-2.2f * ts);

            float spark = 0f;
            for (int k = 0; k < sparkCount; k++)
            {
                float st = ts - sparkAt[k];
                if (st <= 0f) continue;
                spark += Mathf.Sin(2f * Mathf.PI * sparkHz[k] * st) * Mathf.Exp(-26f * st);
            }

            dry[i] = body * 0.115f + sub * 0.055f + spark * 0.020f;
        }

        return Finalize(dry, 0.30f, 9000f);   // wetter than the forge sounds — this one has space
    }

    // Shared tail: small warm reverb + master low-pass + anti-click fades -> AudioClip.
    private static AudioClip pauseHalt, pauseRelease, pauseTick;

    // Opening the pause screen: the moment everything stops.
    public static AudioClip PauseHalt
    {
        get { if (pauseHalt == null) pauseHalt = BuildPauseHalt(); return pauseHalt; }
    }

    // Closing it: the clock let go.
    public static AudioClip PauseRelease
    {
        get { if (pauseRelease == null) pauseRelease = BuildPauseRelease(); return pauseRelease; }
    }

    // Moving the selection. Tiny, dry, and quiet enough to hold a key down through.
    public static AudioClip PauseTick
    {
        get { if (pauseTick == null) pauseTick = BuildPauseTick(); return pauseTick; }
    }

    // A FOURTH sound family, kept distinct from the three already here on purpose: magic is
    // harmonic (bell partials), metal is inharmonic-but-pitched (bar modes), stone is barely
    // pitched at all. This one is defined by what happens to its ENVELOPE rather than its
    // spectrum — it is the only sound in the game that gets CHOKED.
    //
    // Three beats, and the middle one is the whole idea:
    //   INHALE  ~130ms of noise whose band-pass climbs. Reads as something winding up.
    //   STOP    a struck glass cluster plus a low sub, on the beat the inhale cuts dead.
    //   CHOKE   the ring is damped away over ~180ms instead of being allowed to decay naturally.
    //           A sound that fades out says "ending"; a sound that is cut short says "held".
    private static AudioClip BuildPauseHalt()
    {
        const float dur = 1.10f;
        const float hit = 0.13f;                 // when the inhale stops and the strike lands
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(9042);

        float svfLow = 0f, svfBand = 0f;

        // Cold glass, not a warm bell: a stretched, slightly inharmonic cluster.
        float f0 = 494f;                                    // B4
        float[] ratio = { 1f, 2.02f, 3.09f, 4.21f, 6.05f };
        float[] pAmp = { 1f, 0.46f, 0.26f, 0.14f, 0.07f };

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            // INHALE — band-passed noise climbing 300 -> 3400 Hz, silenced the instant the hit lands.
            float inhale = 0f;
            if (ts < hit)
            {
                float t01 = ts / hit;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float cutoff = Mathf.Lerp(300f, 3400f, t01 * t01);
                float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(cutoff, SampleRate * 0.45f) / SampleRate);
                svfLow += f * svfBand;
                float high = noise - svfLow - 0.30f * svfBand;
                svfBand += f * high;
                inhale = svfBand * Mathf.Pow(t01, 1.8f) * 0.16f;
            }

            float st = ts - hit;
            if (st < 0f) { dry[i] = inhale; continue; }

            // THE CHOKE. Two envelopes multiplied: the partials' own decay, and a damper that
            // clamps down over 180ms and holds everything at a trickle afterwards. Without the
            // damper this is just a chime and reads as a menu ping.
            float damp = Mathf.Lerp(1f, 0.06f, Mathf.Clamp01(st / 0.18f));

            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * f0 * ratio[p] * st) * pAmp[p] * Mathf.Exp(-6f * st);

            // Sub thump under the strike — the weight of the thing coming to rest.
            float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(96f, 58f, Mathf.Clamp01(st / 0.25f)) * st)
                      * Mathf.Exp(-9f * st);

            dry[i] = inhale + body * damp * 0.105f + sub * 0.075f;
        }

        return Finalize(dry, 0.20f, 7000f);
    }

    // The inverse: short, and it OPENS. The halt's ring is choked; this one is released and allowed
    // to run out on its own, which is what makes the pair read as a lid closing and lifting.
    private static AudioClip BuildPauseRelease()
    {
        const float dur = 0.55f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(3311);

        float lp = 0f;
        double ph = 0.0;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float t01 = ts / dur;

            // A breath opening outward: noise whose low-pass sweeps UP, brief.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(700f, 5200f, Mathf.Clamp01(t01 / 0.35f));
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate)) * (noise - lp);
            float air = lp * Mathf.Exp(-11f * ts) * 0.13f;

            // A glass partial rising a fourth. Phase is integrated so the glide doesn't tear.
            float g = Mathf.Lerp(494f, 659f, Mathf.Clamp01(t01 / 0.4f));
            ph += 2.0 * Mathf.PI * g / SampleRate;
            float tone = (float)System.Math.Sin(ph) * Mathf.Exp(-7f * ts) * 0.075f;

            dry[i] = air + tone;
        }

        return Finalize(dry, 0.14f, 8000f);
    }

    // Selection tick. 35ms, no reverb, no pitch to speak of — a fingernail on glass. Anything with
    // a discernible NOTE turns a held arrow key into a melody.
    private static AudioClip BuildPauseTick()
    {
        const float dur = 0.035f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(1187);

        float hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // One-pole high-pass, so the click has no body at all.
            hp = 0.92f * (hp + noise - prev);
            prev = noise;

            dry[i] = (hp * 0.5f + Mathf.Sin(2f * Mathf.PI * 2300f * ts) * 0.35f) * Mathf.Exp(-180f * ts) * 0.22f;
        }

        return Finalize(dry, 0f, 11000f);
    }

    private static AudioClip paperRustle, waxStamp;

    // Opening the quest board: a sheaf of pinned paper disturbed by the door.
    public static AudioClip PaperRustle
    {
        get { if (paperRustle == null) paperRustle = BuildPaperRustle(); return paperRustle; }
    }

    // Accepting a contract: a seal pressed into wax.
    public static AudioClip WaxStamp
    {
        get { if (waxStamp == null) waxStamp = BuildWaxStamp(); return waxStamp; }
    }

    // These two are the only sounds in the game with NO PITCHED COMPONENT AT ALL, and that is what
    // makes them a distinct family rather than a variation on the stone hits. Magic is harmonic,
    // metal is inharmonic-but-pitched, stone is barely pitched, the pause pair is defined by its
    // envelope — paper simply has no note in it. Give either of these a tone and it stops being
    // paper immediately.
    //
    // A rustle is a cluster of short noise bursts, not one long one: a continuous shaped hiss reads
    // as wind, and only the granularity says "many separate sheets".
    private static AudioClip BuildPaperRustle()
    {
        const float dur = 0.62f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5531);

        const int bursts = 14;
        var at = new float[bursts];
        var decay = new float[bursts];
        var amp = new float[bursts];
        var hz = new float[bursts];
        for (int k = 0; k < bursts; k++)
        {
            // Front-loaded: the disturbance settles rather than building.
            float u = (float)rng.NextDouble();
            at[k] = u * u * dur * 0.85f;
            decay[k] = 34f + (float)rng.NextDouble() * 46f;
            amp[k] = 0.5f + (float)rng.NextDouble() * 0.5f;
            hz[k] = 2200f + (float)rng.NextDouble() * 4200f;
        }

        float svfLow = 0f, svfBand = 0f;
        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            float env = 0f;
            float centre = 0f, wsum = 0f;
            for (int k = 0; k < bursts; k++)
            {
                float bt = ts - at[k];
                if (bt <= 0f) continue;
                float e = Mathf.Exp(-decay[k] * bt) * amp[k];
                env += e;
                centre += hz[k] * e; wsum += e;
            }
            if (env <= 0.0001f) { dry[i] = 0f; continue; }
            centre = wsum > 0f ? centre / wsum : 3600f;

            // Resonant band-pass following whichever burst is loudest right now.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(centre, SampleRate * 0.45f) / SampleRate);
            svfLow += f * svfBand;
            float high = noise - svfLow - 0.55f * svfBand;   // low Q: paper is broad, not whistly
            svfBand += f * high;

            // Global taper so the last bursts don't end abruptly.
            dry[i] = svfBand * Mathf.Min(env, 1.4f) * (1f - Mathf.Clamp01(ts / dur)) * 0.085f;
        }

        return Finalize(dry, 0.08f, 12000f);   // nearly dry — a board is against a wall, not in a hall
    }

    // The press. Three layers, and the ORDER of their decays is what sells it as one physical
    // action rather than three sounds: the wax gives way first (a fast dull squash), the seal
    // bottoms out on the paper underneath (a low thock), and the sheet itself creases last.
    private static AudioClip BuildWaxStamp()
    {
        const float dur = 0.42f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(8264);

        // Crease ticks: a handful of tiny snaps in the first 140ms.
        const int ticks = 6;
        var tickAt = new float[ticks];
        var tickHz = new float[ticks];
        for (int k = 0; k < ticks; k++)
        {
            tickAt[k] = 0.012f + (float)rng.NextDouble() * 0.13f;
            tickHz[k] = 3400f + (float)rng.NextDouble() * 3800f;
        }

        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            // SQUASH — noise through a low-pass that closes fast. Soft material displacing.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(2600f, 380f, Mathf.Clamp01(ts / 0.05f));
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate)) * (noise - lp);
            float squash = lp * Mathf.Exp(-28f * ts) * 0.30f;

            // THOCK — the seal reaching the desk through the paper. Pitch drops as it settles, but
            // it stays under 90 Hz where it reads as weight rather than as a note.
            float thock = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(88f, 52f, Mathf.Clamp01(ts / 0.12f)) * ts)
                        * Mathf.Exp(-19f * ts) * 0.16f;

            // CREASE — paper snapping under the pressure.
            float crease = 0f;
            for (int k = 0; k < ticks; k++)
            {
                float t2 = ts - tickAt[k];
                if (t2 <= 0f) continue;
                crease += Mathf.Sin(2f * Mathf.PI * tickHz[k] * t2) * Mathf.Exp(-150f * t2);
            }

            dry[i] = squash + thock + crease * 0.022f;
        }

        return Finalize(dry, 0.12f, 9000f);
    }

    // =============================================================================================
    // THE SILENT SLOTS — sounds for things that currently make none.
    //
    // ⚠️ THIS IS WHERE THE GAME'S SILENCE ACTUALLY LIVES, and it is NOT the SoundBank. Measured
    // 2026-08-21: 75 AudioClip fields across our 172 prefabs are NULL, and `SfxManager.PlayOn` with
    // a null clip is a silent no-op — so every melee enemy swings without a sound (15 prefabs), no
    // breakable wall makes a noise (13), no spitter spits (8), no Shift Altar responds (12).
    // Meanwhile `Sfx.Play` has ONE call site, so filling the SoundBank would have built a library
    // nothing reads. Fill the slots first; migrate the architecture after.
    //
    // Six clips here cover 54 of the 75 slots. They are baked to real .wav assets and assigned by
    // `Deckshift → Fill Silent Audio Slots`.

    private static AudioClip zombieSwing, spitterSpit, wallBreak, altarPay, altarRefuse, crystalCollect;

    /// <summary>A heavy, unskilled swing: mass moving through air, and a wet grunt behind it.</summary>
    public static AudioClip ZombieSwing
    { get { if (zombieSwing == null) zombieSwing = BuildZombieSwing(); return zombieSwing; } }

    /// <summary>The spitter's launch — a wet gather and a release.</summary>
    public static AudioClip SpitterSpit
    { get { if (spitterSpit == null) spitterSpit = BuildSpitterSpit(); return spitterSpit; } }

    /// <summary>Masonry giving way: a crack, then rubble.</summary>
    public static AudioClip WallBreak
    { get { if (wallBreak == null) wallBreak = BuildWallBreak(); return wallBreak; } }

    /// <summary>The altar takes your Shift. Harmonic, rising, and it RESOLVES.</summary>
    public static AudioClip AltarPay
    { get { if (altarPay == null) altarPay = BuildAltarPay(); return altarPay; } }

    /// <summary>The altar refuses. The same voice, falling, and it does not resolve.</summary>
    public static AudioClip AltarRefuse
    { get { if (altarRefuse == null) altarRefuse = BuildAltarRefuse(); return altarRefuse; } }

    /// <summary>A Shift crystal collected — small, bright, over fast.</summary>
    public static AudioClip CrystalCollect
    { get { if (crystalCollect == null) crystalCollect = BuildCrystalCollect(); return crystalCollect; } }

    // ⚠️ THE SWING IS AIR, NOT AN IMPACT. It plays when the attack STARTS, before anything is hit —
    // the hit has its own sound. Giving this a transient makes every enemy sound like it connected
    // even when it whiffed, which is actively misleading in a game where dodging is the whole point.
    private static AudioClip BuildZombieSwing()
    {
        const float dur = 0.34f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5501);

        float lp = 0f, hp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            float k = t / dur;

            // A band of noise swept UP then down — the doppler of something heavy passing you.
            float centre = Mathf.Lerp(320f, 1500f, Mathf.Sin(k * Mathf.PI));
            float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * (centre * 2f) / SampleRate)) * (nz - lp);
            hp += (1f - Mathf.Exp(-2f * Mathf.PI * (centre * 0.5f) / SampleRate)) * (lp - hp);
            float air = (lp - hp) * Mathf.Sin(k * Mathf.PI) * 0.55f;

            // The grunt: low, breathy, no clear pitch. It arrives slightly AFTER the swing peaks,
            // because the effort comes out of you a beat behind the movement.
            float g = 0f;
            if (t > 0.06f)
            {
                float gt = t - 0.06f;
                float wob = 74f + Mathf.Sin(gt * 38f) * 9f;
                g = Mathf.Sin(2f * Mathf.PI * wob * gt) * Mathf.Exp(-11f * gt) * 0.22f;
                g += (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-15f * gt) * 0.07f;
            }
            dry[i] = air + g;
        }
        return Finalize(dry, 0.08f, 6000f);
    }

    private static AudioClip BuildSpitterSpit()
    {
        const float dur = 0.40f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5502);

        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;

            // GATHER — a rising wet rattle for the first 180ms. This is the windup the AI already
            // animates and which currently has no sound at all, so the player gets no warning.
            float gather = 0f;
            if (t < 0.20f)
            {
                float k = t / 0.20f;
                float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Lerp(260f, 900f, k) / SampleRate)) * (nz - lp);
                float chop = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(22f, 46f, k) * t);
                gather = lp * chop * k * 0.5f;
            }

            // RELEASE — a short pressurised burst, low-passed hard so it stays wet rather than hissy.
            float rel = 0f;
            if (t >= 0.20f)
            {
                float rt = t - 0.20f;
                float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Lerp(2400f, 400f, Mathf.Clamp01(rt / 0.10f)) / SampleRate)) * (nz - lp);
                rel = lp * Mathf.Exp(-16f * rt) * 0.85f;
            }
            dry[i] = gather + rel;
        }
        return Finalize(dry, 0.10f, 5200f);
    }

    private static AudioClip BuildWallBreak()
    {
        const float dur = 0.95f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5503);

        // THE CRACK — the wall failing. One hard, bright, very short event.
        Thud(dry, 0f, 210f, 0.85f, 26f, rng, 0.30f);
        Latch(dry, 0.002f, 0.30f, rng, 1900f);

        // THE COLLAPSE — a body of low noise under it, so it has weight.
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * 700f / SampleRate)) * (nz - lp);
            dry[i] += lp * Mathf.Exp(-7f * t) * 0.42f;
        }

        // ⚠️ AND THEN RUBBLE — 14 small stone hits scattered over the next 700ms. The tail is the
        // whole difference between "a wall broke" and "something was hit". A single impact with a
        // decay reads as a drum; discrete pieces landing at irregular times reads as debris.
        for (int k = 0; k < 14; k++)
        {
            float at = 0.10f + (float)rng.NextDouble() * 0.62f;
            float hz = 150f + (float)rng.NextDouble() * 460f;
            float amp = 0.16f * (1f - at / 0.80f) * (0.5f + (float)rng.NextDouble() * 0.5f);
            Thud(dry, at, hz, Mathf.Max(0.03f, amp), 46f + (float)rng.NextDouble() * 40f, rng, 0.18f);
        }
        return Finalize(dry, 0.18f, 8000f);
    }

    // ⚠️ PAY AND REFUSE ARE THE SAME VOICE, INVERTED. Both are Shift, so both are harmonic bell
    // partials in the magic family — what separates them is DIRECTION and RESOLUTION: pay rises and
    // lands on its root, refuse falls and stops on an unresolved interval. That is far clearer than
    // making refuse a buzzer, and it keeps the altar sounding like one object.
    private static AudioClip BuildAltarPay()
    {
        const float dur = 0.85f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5504);

        float[] partial = { 1f, 2f, 3f, 4f, 5.1f };
        float[] gain = { 1f, 0.52f, 0.30f, 0.16f, 0.09f };

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            // Root rises a fifth and settles — the altar accepting.
            float root = Mathf.Lerp(196f, 294f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.30f)));
            float s = 0f;
            for (int p = 0; p < partial.Length; p++)
                s += Mathf.Sin(2f * Mathf.PI * root * partial[p] * t) * gain[p] * Mathf.Exp(-(2.2f + p * 1.5f) * t);

            // A breath of noise on the attack only, so it starts as an event rather than a tone.
            float air = t < 0.03f ? (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - t / 0.03f) * 0.10f : 0f;
            dry[i] = s * 0.24f + air;
        }
        return Finalize(dry, 0.30f, 11000f);
    }

    private static AudioClip BuildAltarRefuse()
    {
        const float dur = 0.60f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5505);

        float[] partial = { 1f, 2f, 3f, 4f, 5.1f };
        float[] gain = { 1f, 0.52f, 0.30f, 0.16f, 0.09f };

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            // Falls, and stops on a tritone below the root — deliberately unresolved.
            float root = Mathf.Lerp(294f, 208f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f)));
            float s = 0f;
            for (int p = 0; p < partial.Length; p++)
                s += Mathf.Sin(2f * Mathf.PI * root * partial[p] * t) * gain[p] * Mathf.Exp(-(4.5f + p * 2.2f) * t);

            float air = t < 0.03f ? (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - t / 0.03f) * 0.10f : 0f;
            dry[i] = s * 0.20f + air;
        }
        return Finalize(dry, 0.16f, 9000f);
    }

    // ⚠️ SHORT. This fires on every crystal picked up, often several in a row, and anything with a
    // tail turns a handful of pickups into a chord. 180ms and out.
    private static AudioClip BuildCrystalCollect()
    {
        const float dur = 0.22f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];

        float[] partial = { 1f, 2.01f, 3.02f };
        float[] gain = { 1f, 0.44f, 0.20f };

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            float root = Mathf.Lerp(660f, 880f, Mathf.Clamp01(t / 0.05f));
            float s = 0f;
            for (int p = 0; p < partial.Length; p++)
                s += Mathf.Sin(2f * Mathf.PI * root * partial[p] * t) * gain[p] * Mathf.Exp(-(16f + p * 10f) * t);
            dry[i] = s * 0.24f;
        }
        return Finalize(dry, 0.14f, 13000f);
    }

    // ---- WELL (recharge room, 2026-09-14) ------------------------------------------------------
    // The Well is the altar's opposite: the altar DRINKS your Shift, the Well gives it back. So the
    // two clips share a voice — the same harmonic partials as AltarPay — and invert the shape:
    // the altar rises and resolves upward; the well DRAWS (a held, falling intake) and then SURGES
    // (water and a rising bloom). Read the pair as a sound-design brief, like everything here.
    private static AudioClip wellDraw, wellSurge;

    /// <summary>The well holding its breath: water pulled inward, a low tone sinking. ~0.4s.</summary>
    public static AudioClip WellDraw
    { get { if (wellDraw == null) wellDraw = BuildWellDraw(); return wellDraw; } }

    /// <summary>The water erupts and the light blooms: a splash, then a rising resolved chime. ~1.3s.</summary>
    public static AudioClip WellSurge
    { get { if (wellSurge == null) wellSurge = BuildWellSurge(); return wellSurge; } }

    private static AudioClip BuildWellDraw()
    {
        const float dur = 0.42f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5601);

        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            float k = t / dur;

            // Water being drawn INWARD: filtered noise whose band closes as it goes — the sound of
            // a surface being pulled taut. Reverse-enveloped so it swells rather than decays.
            float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Lerp(1400f, 380f, k) / SampleRate)) * (nz - lp);
            float water = lp * Mathf.SmoothStep(0f, 1f, k) * 0.55f;

            // Under it a tone that SINKS a fourth — the intake. The altar's voice, going the other way.
            float root = Mathf.Lerp(294f, 220f, Mathf.SmoothStep(0f, 1f, k));
            float tone = (Mathf.Sin(2f * Mathf.PI * root * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * root * 2f * t))
                         * Mathf.Sin(k * Mathf.PI) * 0.16f;
            dry[i] = water + tone;
        }
        return Finalize(dry, 0.22f, 7000f);
    }

    private static AudioClip BuildWellSurge()
    {
        const float dur = 1.30f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5602);

        float[] partial = { 1f, 2f, 3f, 4f, 5.1f };
        float[] gain = { 1f, 0.52f, 0.30f, 0.16f, 0.09f };

        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;

            // THE SPLASH — a burst of bright water in the first 120ms, then droplets: the noise band
            // opens wide and slams shut, with a slow gurgle underneath so it stays liquid, not hiss.
            float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
            float band = t < 0.12f ? Mathf.Lerp(3200f, 900f, t / 0.12f) : 700f;
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * band / SampleRate)) * (nz - lp);
            float splash = lp * Mathf.Exp(-9f * t) * 0.9f;
            lp2 += (1f - Mathf.Exp(-2f * Mathf.PI * 260f / SampleRate)) * (nz - lp2);
            float gurgle = lp2 * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 7.5f * t)) * Mathf.Exp(-2.6f * t) * 0.35f;

            // THE BLOOM — the altar's chord, but arriving late (the light follows the water), rising
            // a fifth and RESOLVING. Slower decay than the altar: this is a gift, let it ring.
            float bloom = 0f;
            if (t > 0.10f)
            {
                float bt = t - 0.10f;
                float root = Mathf.Lerp(220f, 330f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(bt / 0.35f)));
                for (int p = 0; p < partial.Length; p++)
                    bloom += Mathf.Sin(2f * Mathf.PI * root * partial[p] * bt) * gain[p] * Mathf.Exp(-(1.6f + p * 1.2f) * bt);
                bloom *= 0.22f * Mathf.Clamp01(bt / 0.05f);
            }

            // DROPLETS — a scatter of tiny high pings falling back into the water over the tail.
            float drops = 0f;
            for (int d = 0; d < 9; d++)
            {
                float at = 0.18f + d * 0.09f + (float)((d * 7919) % 50) * 0.001f;
                if (t > at && t < at + 0.06f)
                {
                    float dt = t - at;
                    float hz = 1800f + (d * 613) % 900;
                    drops += Mathf.Sin(2f * Mathf.PI * hz * dt) * Mathf.Exp(-70f * dt) * 0.08f;
                }
            }
            dry[i] = splash + gurgle + bloom + drops;
        }
        return Finalize(dry, 0.34f, 11000f);
    }

    private static AudioClip glassParry, freefallBlade;

    /// <summary>A blow stopped on glass: struck, an instant of ring, then shards.</summary>
    public static AudioClip GlassParry
    { get { if (glassParry == null) glassParry = BuildGlassParry(); return glassParry; } }

    /// <summary>The blade's arc — fast, high, and the edge singing behind it.</summary>
    public static AudioClip FreefallBlade
    { get { if (freefallBlade == null) freefallBlade = BuildFreefallBlade(); return freefallBlade; } }

    // ⚠️ THE RING IS CUT OFF BY THE SHARDS, not faded under them. That is the whole difference
    // between glass that RANG and glass that BROKE — a decaying tone reads as a chime no matter how
    // many ticks you scatter over it. Inverted against WallBreak on purpose: masonry is low rubble
    // falling and settling, glass is high shards thrown outward and over fast.
    private static AudioClip BuildGlassParry()
    {
        const float dur = 0.70f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5506);

        // Glass plate modes: inharmonic like metal, but far higher and with almost no sustain.
        float[] ratio = { 1f, 2.66f, 4.83f, 7.19f };
        float[] gain = { 1f, 0.58f, 0.31f, 0.14f };
        const float root = 1180f;
        const float choke = 0.085f;              // when the shatter takes it

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            if (t > choke + 0.012f) break;

            float s = 0f;
            for (int p = 0; p < ratio.Length; p++)
                s += Mathf.Sin(2f * Mathf.PI * root * ratio[p] * t) * gain[p] * Mathf.Exp(-(14f + p * 9f) * t);

            // The blow landing. Bright and very short — this is contact, not a hit on flesh.
            float strike = t < 0.006f ? (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - t / 0.006f) * 0.62f : 0f;

            // 12ms taper into the break, so the cut is abrupt without clicking.
            float gate = t < choke ? 1f : 1f - (t - choke) / 0.012f;
            dry[i] = (s * 0.30f + strike) * gate;
        }

        // 24 shards over the next half second: each a tiny high mode pair with a steep decay,
        // amplitude thinning with time so the spray falls away rather than stopping.
        for (int k = 0; k < 24; k++)
        {
            float at = choke + (float)rng.NextDouble() * 0.46f;
            float hz = 2300f + (float)rng.NextDouble() * 3900f;
            float amp = 0.20f * (1f - (at - choke) / 0.52f) * (0.35f + (float)rng.NextDouble() * 0.65f);
            int start = Mathf.RoundToInt(at * SampleRate);
            for (int i = start; i < n; i++)
            {
                float t = (float)(i - start) / SampleRate;
                float env = Mathf.Exp(-95f * t);
                if (env < 0.001f) break;
                float s = Mathf.Sin(2f * Mathf.PI * hz * t)
                        + Mathf.Sin(2f * Mathf.PI * hz * 2.44f * t) * 0.4f;
                float tick = t < 0.0015f ? (float)(rng.NextDouble() * 2.0 - 1.0) * 0.45f : 0f;
                dry[i] += (s * 0.5f + tick) * env * amp;
            }
        }
        return Finalize(dry, 0.22f, 14000f);
    }

    // ⚠️ SAME FAMILY AS ZombieSwing (both are air) AND IT MUST NOT BE CONFUSABLE WITH IT. The axis
    // that separates them is speed and pitch, not volume: the zombie is slow, low (320-1500Hz) and
    // grunts; this is half the length, four times higher, and rings. It also fires on EVERY cast
    // including into empty air, so it carries no impact and no tail.
    private static AudioClip BuildFreefallBlade()
    {
        const float dur = 0.26f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(5507);

        const float swipe = 0.13f;
        float lp = 0f, hp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            float air = 0f;
            if (t < swipe)
            {
                // Narrow band climbing hard and dropping — an edge, not a mass.
                float k = t / swipe;
                float centre = Mathf.Lerp(1800f, 6400f, Mathf.Sin(k * Mathf.PI * 0.72f));
                float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (1f - Mathf.Exp(-2f * Mathf.PI * (centre * 1.6f) / SampleRate)) * (nz - lp);
                hp += (1f - Mathf.Exp(-2f * Mathf.PI * (centre * 0.7f) / SampleRate)) * (lp - hp);
                air = (lp - hp) * Mathf.Sin(k * Mathf.PI) * 0.85f;
            }

            // The edge sings from the peak of the arc onward — metal bar modes, thin and quick.
            float ring = 0f;
            if (t > swipe * 0.55f)
            {
                float rt = t - swipe * 0.55f;
                float[] ratio = { 1f, 2.76f, 5.40f };
                float[] gain = { 1f, 0.34f, 0.12f };
                for (int p = 0; p < ratio.Length; p++)
                    ring += Mathf.Sin(2f * Mathf.PI * 1420f * ratio[p] * rt) * gain[p] * Mathf.Exp(-(26f + p * 16f) * rt);
                ring *= 0.16f;
            }
            dry[i] = air + ring;
        }
        return Finalize(dry, 0.12f, 15000f);
    }

    // =============================================================================================
    // NOTICE BOARD — timber, iron nails, paper.
    //
    // ⚠️ THESE REPLACE PaperRustle / WaxStamp ON THE QUEST BOARD (designer, 2026-08-21: "change the
    // sound effects that the quest board uses currently as well. the sound should fit the theme").
    // The two old clips are pure PAPER, and paper was the whole board when the board was a painted
    // sheet. It is now a timber notice board with contracts NAILED to it, so two thirds of the
    // material was inaudible: no wood, no iron.
    //
    // The set is built as a three-way where the MATERIAL carries the meaning, not the volume:
    //
    //   BoardOpen  — paper and wood.            you disturbed the sheaf
    //   BoardNail  — paper, wood AND IRON.      it went in
    //   BoardDud   — paper and wood, NO IRON.   it did not
    //
    // ⚠️ THE ABSENCE OF THE IRON IS THE REFUSAL. Refuse is not the accept played quieter or lower —
    // it is the accept with the one layer that means "fastened" removed. That is why it reads
    // instantly even at the same loudness, and it is the same trick the pause pair uses (choked
    // envelope) rather than a pitch drop.
    //
    // ⚠️ AND THIS IS WHERE THE PAPER FAMILY'S "NO PITCHED COMPONENT" RULE STOPS APPLYING — carefully.
    // BoardOpen still obeys it (its wood knock sits under 80Hz, where it reads as weight, not a
    // note). BoardNail deliberately breaks it, because a nail IS metal, and it is the only sound on
    // the screen with a bar mode in it. That exclusivity is what makes the accept land.

    private static AudioClip boardOpen, boardNail, boardDud;

    /// <summary>Stepping up to the board: several pinned sheets stirring against timber.</summary>
    public static AudioClip BoardOpen
    { get { if (boardOpen == null) boardOpen = BuildBoardOpen(); return boardOpen; } }

    /// <summary>Taking a contract: a nail driven through paper into the board, two strikes.</summary>
    public static AudioClip BoardNail
    { get { if (boardNail == null) boardNail = BuildBoardNail(); return boardNail; } }

    /// <summary>Refused: the hammer meets the board and nothing goes in.</summary>
    public static AudioClip BoardDud
    { get { if (boardDud == null) boardDud = BuildBoardDud(); return boardDud; } }

    private static AudioClip BuildBoardOpen()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.58f)];
        var rng = new System.Random(4471);

        // ⚠️ SEPARATE BURSTS, NOT ONE LONG HISS. A continuous shaped noise reads as wind; only the
        // granularity says "several distinct sheets". Same rule the original rustle was built on.
        // Staggered irregularly — evenly spaced bursts read as a machine.
        Rustle(dry, 0.000f, 0.14f, 0.30f, rng, 5200f, 0.4f);
        Rustle(dry, 0.055f, 0.11f, 0.22f, rng, 6400f, -0.3f);
        Rustle(dry, 0.140f, 0.16f, 0.26f, rng, 4600f, 0.2f);
        Rustle(dry, 0.255f, 0.13f, 0.16f, rng, 5800f, -0.6f);
        Rustle(dry, 0.370f, 0.15f, 0.10f, rng, 4200f, -0.9f);

        // The board itself taking the disturbance. Under 80Hz and almost no ring, so it is felt as
        // the paper being attached to SOMETHING rather than heard as a note.
        Thud(dry, 0.020f, 74f, 0.13f, 26f, rng, 0.10f);

        return Finalize(dry, 0.06f, 10000f);
    }

    private static AudioClip BuildBoardNail()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.50f)];
        var rng = new System.Random(4472);

        // STRIKE ONE — the nail bites. Bright iron, the board still ringing under it, paper
        // compressing where the head lands.
        Latch(dry, 0.000f, 0.46f, rng, 2950f);
        Thud(dry, 0.002f, 126f, 0.40f, 32f, rng, 0.55f);
        Rustle(dry, 0.000f, 0.05f, 0.16f, rng, 5600f, -1f);

        // STRIKE TWO — and it SEATS. Harder, but the wood goes deeper and the ring is choked,
        // because the nail is now home and the board is no longer free to sound.
        //
        // ⚠️ THIS IS THE WHOLE SOUND. One strike is a generic impact and could be any screen's
        // confirm; two strikes where the SECOND IS DEADER is unmistakably something being driven in.
        // If this ever needs retuning, keep that relationship — do not make strike two louder and
        // brighter, which is the instinct and which turns it back into a generic double-click.
        Latch(dry, 0.094f, 0.60f, rng, 3150f);
        Thud(dry, 0.096f, 101f, 0.52f, 47f, rng, 0.16f);

        // The sheet settling against the timber afterwards.
        Rustle(dry, 0.135f, 0.13f, 0.09f, rng, 4800f, -0.8f);

        return Finalize(dry, 0.10f, 9500f);
    }

    private static AudioClip BuildBoardDud()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.32f)];
        var rng = new System.Random(4473);

        // Wood, struck once, and NOTHING FASTENS. ring 0.05 is almost pure transient — the sound of
        // a hammer meeting a board rather than driving anything through it.
        Thud(dry, 0.000f, 92f, 0.42f, 55f, rng, 0.05f);
        Rustle(dry, 0.004f, 0.07f, 0.13f, rng, 5000f, -1f);

        // ⚠️ NO Latch(). Deliberately. See the family note above: the missing iron IS the message,
        // and adding "just a little" metal here would make refuse and accept the same event at
        // different volumes.

        return Finalize(dry, 0.05f, 7200f);
    }

    // =============================================================================================
    // GATE — heavy banded doors hung in a stone arch.
    //
    // A FIFTH family, and deliberately the only one built from TWO materials at once. Every family
    // above commits to one (magic = harmonic bell partials, metal = inharmonic bar modes, stone =
    // noise + sub, paper = no pitched component at all, UI = pitch motion). A barred door in a stone
    // opening is iron working against masonry, so these layer bar modes OVER grit — which is what
    // stops a hinge creak reading as a scrap pickup and the stop reading as a Meteor Greaves landing.
    //
    // FOUR clips, because a gate opening is a SEQUENCE and not a hit. The old gate played nothing at
    // all, and that silence was most of why it felt like nothing was happening.
    //
    // ⚠️ These were written for a PORTCULLIS (strain → catch → ratchet down → seat) and are now
    // sequenced by Gate.cs as a DOUBLE DOOR (bolt → strain → swing → stop, and a slam on the way
    // back). The clips still fit — the materials did not change, only the choreography — so the
    // names below describe the sound rather than the beat it happens to serve. Do not assume
    // "Ratchet" means the gate ratchets; it is the dry repeatable tick, used as a hinge creak.

    private static AudioClip gateGroan, gateRelease, gateRatchet, gateSeat;

    // The mechanism taking the weight before anything moves. Deliberately has NO attack transient —
    // it swells. A sound that arrives gradually is what makes the release that follows land.
    public static AudioClip GateGroan
    { get { if (gateGroan == null) gateGroan = BuildGateGroan(); return gateGroan; } }

    // The catch letting go: the one sharp event in the whole sequence, so nothing else may compete.
    public static AudioClip GateRelease
    { get { if (gateRelease == null) gateRelease = BuildGateRelease(); return gateRelease; } }

    // One pawl catch during the descent. Played a dozen times per open, so it is deliberately the
    // quietest and driest clip in the game — give this a tail and the drop turns to mush.
    public static AudioClip GateRatchet
    { get { if (gateRatchet == null) gateRatchet = BuildGateRatchet(); return gateRatchet; } }

    // The slab arriving. Lower and longer than MeteorImpact on purpose: that is a body hitting the
    // floor, this is the floor taking a tonne of rock.
    public static AudioClip GateSeat
    { get { if (gateSeat == null) gateSeat = BuildGateSeat(); return gateSeat; } }

    private static AudioClip BuildGateGroan()
    {
        const float dur = 0.55f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(20819);

        float lp = 0f, hp = 0f;
        float lpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 520f / SampleRate);
        float hpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 150f / SampleRate);
        float subPhase = 0f;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;

            // Swell in and ease out. No transient anywhere — this is load ARRIVING, not an impact.
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(ts / dur));
            env *= env;

            // Stone bearing on stone: a narrow noise band with a slow wobble, as the faces bind.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += lpCoef * (noise - lp);
            hp += hpCoef * (lp - hp);
            float grit = (lp - hp) * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 11f * ts));

            subPhase += 2f * Mathf.PI * 52f / SampleRate;   // the weight itself
            float sub = Mathf.Sin(subPhase);

            dry[i] = (grit * 0.42f + sub * 0.30f) * env;
        }
        return Finalize(dry, 0.20f, 3200f);   // dark: heard through rock, not through air
    }

    private static AudioClip BuildGateRelease()
    {
        const float dur = 0.34f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(60313);

        float f0 = 168f;                                   // heavy iron => LOW fundamental
        float[] ratio = { 1f, 2.76f, 5.40f, 8.93f };       // ideal free-bar modes = metal
        float[] pAmp  = { 1f, 0.48f, 0.22f, 0.09f };
        float[] pDec  = { 15f, 24f, 36f, 52f };

        float lp = 0f;
        float lpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 4200f / SampleRate);
        float subPhase = 0f;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * f0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            lp += lpCoef * (noise - lp);
            float grit = lp * Mathf.Exp(-70f * ts);

            subPhase += 2f * Mathf.PI * 64f / SampleRate;
            float sub = Mathf.Sin(subPhase) * Mathf.Exp(-13f * ts);

            dry[i] = body * 0.20f + grit * 0.26f + sub * 0.30f;
        }
        return Finalize(dry, 0.18f, 6000f);
    }

    private static AudioClip BuildGateRatchet()
    {
        const float dur = 0.13f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(11279);

        float f0 = 315f;
        float[] ratio = { 1f, 2.76f, 5.40f };
        float[] pAmp  = { 1f, 0.40f, 0.16f };
        float[] pDec  = { 46f, 62f, 84f };                 // very fast: a tick, never a ring

        float lp = 0f;
        float lpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 3400f / SampleRate);

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * f0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            lp += lpCoef * (noise - lp);
            float grit = lp * Mathf.Exp(-150f * ts);

            dry[i] = body * 0.11f + grit * 0.16f;
        }
        return Finalize(dry, 0.05f, 7000f);   // almost dry — a dozen of these must not smear
    }

    private static AudioClip BuildGateSeat()
    {
        const float dur = 1.30f;
        int n = Mathf.CeilToInt(SampleRate * dur);
        var dry = new float[n];
        var rng = new System.Random(77404);

        float b0 = 96f;                                    // below MeteorImpact 132 = more mass
        float[] ratio = { 1f, 1.71f, 2.43f };
        float[] pAmp  = { 1f, 0.38f, 0.17f };
        float[] pDec  = { 8f, 13f, 20f };

        float crackLp = 0f;
        float crackCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 6200f / SampleRate);
        float debLp = 0f, debHp = 0f;
        float debLpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 1500f / SampleRate);
        float debHpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 220f / SampleRate);
        float subPhase = 0f;

        for (int i = 0; i < n; i++)
        {
            float ts = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            crackLp += crackCoef * (noise - crackLp);
            float crack = crackLp * Mathf.Exp(-260f * ts);

            float subF = Mathf.Lerp(92f, 30f, Mathf.Clamp01(ts / 0.36f));
            subPhase += 2f * Mathf.PI * subF / SampleRate;
            float sub = Mathf.Sin(subPhase) * Mathf.Exp(-4.2f * ts);

            float body = 0f;
            for (int p = 0; p < ratio.Length; p++)
                body += Mathf.Sin(2f * Mathf.PI * b0 * ratio[p] * ts) * pAmp[p] * Mathf.Exp(-pDec[p] * ts);

            debLp += debLpCoef * (noise - debLp);
            debHp += debHpCoef * (debLp - debHp);
            float band = debLp - debHp;
            float debris = band * Mathf.Exp(-3.4f * ts) * Mathf.Clamp01(ts / 0.025f)
                         * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 19f * ts));

            dry[i] = crack * 0.22f + sub * 0.58f + body * 0.20f + debris * 0.18f;
        }
        return Finalize(dry, 0.30f, 4600f);   // wet and dark — a big room, heard from inside it
    }

    private static AudioClip Finalize(float[] dry, float reverbWet, float masterLpHz)
    {
        float[] s = ApplyReverbAndWarmth(dry, reverbWet, masterLpHz);

        int n = s.Length;
        int fade = Mathf.Min(n / 2, SampleRate / 400);   // ~2.5ms
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            s[i] *= k;
            s[n - 1 - i] *= k;
        }

        var clip = AudioClip.Create("ProcSfx_Shift", n, 1, SampleRate, false);
        clip.SetData(s, 0);
        return clip;
    }

    // Small Schroeder reverb (3 damped combs -> 1 allpass) for a tight, warm stone room, then a
    // one-pole master low-pass. Damping keeps the tail warm/candlelit.
    // =============================================================================================
    // UI — the interface's own voice.
    //
    // THE FAMILY RULE, and it is a different KIND of rule from the others. Every family above is
    // defined by a MATERIAL: magic by harmonic bell partials, metal by inharmonic bar modes, stone
    // by noise and sub, paper by having no pitched component at all, the pause pair by a choked
    // envelope. A UI sound has no material — it is not a thing in the world, it is the interface.
    //
    // So this family is defined by PITCH MOTION instead. All six share ONE voice, literally the
    // same `WoodTap` call, and differ only in which way the pitch moves and by how much. That is
    // what makes them a learnable language rather than six noises — and it is the right mechanism
    // for the job, because these are the only sounds in the game that must be told apart FROM EACH
    // OTHER. A world sound only has to be distinguishable from other materials.
    //
    // ⚠️ THE VOICE IS SOFT STRUCK WOOD, and that is a deliberate claim on the one material the
    // world does not already use. Metal is the forge, glass and bells are magic, stone is the
    // rooms, paper is the quest board. Wood is unclaimed, warm, and belongs in a candlelit
    // dungeon — where a clean synth blip would sound like it came from a different game.
    //
    // ⚠️ THEY MUST BE SMALL AND DRY. These play hundreds of times a session. Anything with shimmer
    // or a long tail becomes torture by minute ten, which is why the reverb here is the driest in
    // the file and the decays are the shortest.
    //
    // ⚠️ CANCEL AND REFUSE ARE NOT THE SAME SOUND, and conflating them is the usual mistake.
    // CANCEL is the player choosing to back out — consonant, unremarkable, no fault implied.
    // REFUSE is the game saying no — the only DISSONANT sound in the family. And refuse must not
    // read as damage or failure either; it means "you can't do that", not "you got hurt".
    //
    // ⚠️ OPEN AND CLOSE ARE THE SAME FIGURE INVERTED — the identical three notes, backwards. Two
    // unrelated sounds would not read as a pair, and the pairing is what tells the player that the
    // thing that arrived is the thing that just left.

    private static AudioClip uiMove, uiConfirm, uiCancel, uiRefuse, uiOpen, uiClose;

    /// <summary>Moving a selection. Neutral, no pitch motion, quiet enough to hold a key through.</summary>
    public static AudioClip UIMove
    {
        get { if (uiMove == null) uiMove = BuildUIMove(); return uiMove; }
    }

    /// <summary>Committing. Rising perfect fifth — the most consonant way up, so it reads as resolution.</summary>
    public static AudioClip UIConfirm
    {
        get { if (uiConfirm == null) uiConfirm = BuildUIConfirm(); return uiConfirm; }
    }

    /// <summary>Backing out by choice. Falling fourth: downward, but consonant — this is not an error.</summary>
    public static AudioClip UICancel
    {
        get { if (uiCancel == null) uiCancel = BuildUICancel(); return uiCancel; }
    }

    /// <summary>The game saying no. A minor second sounded together, damped hard. The only dissonance here.</summary>
    public static AudioClip UIRefuse
    {
        get { if (uiRefuse == null) uiRefuse = BuildUIRefuse(); return uiRefuse; }
    }

    /// <summary>A panel arriving. Three notes up.</summary>
    public static AudioClip UIOpen
    {
        get { if (uiOpen == null) uiOpen = BuildUIOpen(); return uiOpen; }
    }

    /// <summary>The same three notes, backwards.</summary>
    public static AudioClip UIClose
    {
        get { if (uiClose == null) uiClose = BuildUIClose(); return uiClose; }
    }

    // The one voice. Every UI sound in the family is this function and nothing else, so the family
    // physically cannot drift apart the way a set of hand-tuned one-offs would.
    //
    // Struck wood is INHARMONIC but only mildly so — far less than metal's bar modes, which is what
    // keeps it reading as a soft tap rather than a clank. The tiny noise transient is the mallet
    // contact; without it the tone starts from nothing and sounds synthesised rather than struck.
    private static void WoodTap(float[] buf, float atSeconds, float hz, float amp, float decay,
                                System.Random rng)
    {
        int start = Mathf.RoundToInt(atSeconds * SampleRate);
        if (start >= buf.Length) return;

        // Mild inharmonic partials — a struck wooden bar, not a metal one.
        float[] ratio = { 1f, 2.83f, 4.94f };
        float[] gain  = { 1f, 0.26f, 0.09f };

        for (int i = start; i < buf.Length; i++)
        {
            float t = (float)(i - start) / SampleRate;
            float env = Mathf.Exp(-decay * t);
            if (env < 0.0008f) break;

            float body = 0f;
            for (int k = 0; k < ratio.Length; k++)
            {
                // Higher partials die faster, which is most of what makes a tap sound wooden.
                body += Mathf.Sin(2f * Mathf.PI * hz * ratio[k] * t) * gain[k] * Mathf.Exp(-decay * 1.9f * k * t);
            }

            // Mallet contact: a couple of milliseconds of noise, gone almost immediately.
            float tap = 0f;
            if (t < 0.004f)
                tap = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.35f * (1f - t / 0.004f);

            buf[i] += (body * 0.42f + tap) * env * amp;
        }
    }

    // =============================================================================================
    // ⚠️ REBUILT 2026-08-20 — MECHANISM, NOT MELODY.
    //
    // The version this replaces was built from musical intervals and the designer's verdict was
    // that it "does not read well" in a dungeon. Reading the numbers back, that was exactly right:
    //
    //     Confirm = 620 -> 930 Hz   a perfect fifth, up
    //     Cancel  = 780 -> 585 Hz   a perfect fourth, down
    //     Refuse  = 600 +  636 Hz   a minor second
    //     Open    = 520 / 693 / 780 a three-note melody, and Close was it backwards
    //
    // The interface was playing little TUNES at the player. Every one was a clean tuned-percussion
    // note, and clean tuned percussion is a musical signal — which is why the family read as a
    // modern app rather than as a dungeon, no matter that the comment above says "wood". It is the
    // same fault as the settings screen being smoked glass and neon: well made, coherent, and from
    // a different game.
    //
    // THE INVERSION: real interfaces in this world are OBJECTS BEING OPERATED — a latch dropping, a
    // card sliding, a bolt that will not move. Objects differ by MATERIAL and ACTION, never by
    // pitch interval. So pitch is no longer the thing that distinguishes these six sounds; what
    // they are MADE OF is:
    //
    //     Move    paper only, no pitched component at all — a card edge brushing past
    //     Confirm wood seating, then iron catching     — two materials, one action
    //     Cancel  wood alone, lower and damped         — it came back down, nothing caught
    //     Refuse  wood with the resonance stripped     — the sound of something NOT moving
    //     Open    a slide, then iron releasing
    //     Close   a slide, ending in the wood arriving
    //
    // Open and Close are still the same gesture inverted, which was the old family's best idea —
    // but the inversion is now physical (where the impact falls) rather than melodic.
    //
    // ⚠️ Refuse is deliberately NOT dissonant any more. Dissonance is a musical idea; a real thing
    // refusing to move is UNRESONANT. Killing the ring says "it didn't budge" far better than a
    // beating interval, and it cannot be mistaken for a tune.
    // =============================================================================================

    // A struck wooden object with a BODY, as opposed to WoodTap's three bare sines. The differences
    // are all the ones that separate "an object was hit" from "a note was played": many more modes,
    // a transient with spectral shape instead of 4ms of white noise, and a tail that is not a
    // perfect exponential.
    private static void Thud(float[] buf, float atSeconds, float hz, float amp, float decay,
                             System.Random rng, float ring = 1f)
    {
        int start = Mathf.RoundToInt(atSeconds * SampleRate);
        if (start >= buf.Length) return;

        // Irregular ratios: a real wooden body has no tidy harmonic series.
        float[] ratio = { 1f, 1.87f, 2.71f, 4.16f, 5.93f, 8.05f };
        float[] gain = { 1f, 0.42f, 0.28f, 0.15f, 0.08f, 0.04f };

        float lp = 0f, hp = 0f;
        float lpC = 1f - Mathf.Exp(-2f * Mathf.PI * 2600f / SampleRate);
        float hpC = 1f - Mathf.Exp(-2f * Mathf.PI * 420f / SampleRate);

        for (int i = start; i < buf.Length; i++)
        {
            float t = (float)(i - start) / SampleRate;
            float env = Mathf.Exp(-decay * t);
            if (env < 0.0006f) break;

            float body = 0f;
            for (int k = 0; k < ratio.Length; k++)
                body += Mathf.Sin(2f * Mathf.PI * hz * ratio[k] * t) * gain[k]
                        * Mathf.Exp(-decay * (1f + 1.15f * k) * t);
            body *= ring;                     // ring 0 = a dead thud, all transient and no tone

            // Contact: ~14ms of BAND-LIMITED noise, not a click. This is most of what makes it read
            // as a material being struck rather than a tone being started.
            float contact = 0f;
            if (t < 0.014f)
            {
                float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += lpC * (nz - lp);
                hp += hpC * (lp - hp);
                contact = (lp - hp) * 1.6f * (1f - t / 0.014f);
            }

            // A little noise riding the tail so the decay isn't a mathematically perfect curve.
            float grit = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.02f * env;

            buf[i] += (body * 0.34f + contact + grit) * env * amp;
        }
    }

    // Paper, cloth, leather — a sound with NO pitched component whatsoever. The whole point of the
    // paper family, borrowed here because a cursor moving over a card should sound like a card.
    private static void Rustle(float[] buf, float atSeconds, float dur, float amp,
                               System.Random rng, float centreHz, float swell)
    {
        int start = Mathf.RoundToInt(atSeconds * SampleRate);
        int n = Mathf.RoundToInt(dur * SampleRate);
        float lp = 0f, hp = 0f;
        float lpC = 1f - Mathf.Exp(-2f * Mathf.PI * (centreHz * 2.1f) / SampleRate);
        float hpC = 1f - Mathf.Exp(-2f * Mathf.PI * (centreHz * 0.55f) / SampleRate);

        for (int i = 0; i < n; i++)
        {
            int j = start + i;
            if (j < 0 || j >= buf.Length) continue;
            float k = (float)i / n;

            // swell > 0 opens toward the end (a drawer coming out), < 0 closes (settling shut)
            float env = swell >= 0f
                ? Mathf.Sin(Mathf.PI * k) * Mathf.Lerp(1f, k, Mathf.Clamp01(swell))
                : Mathf.Sin(Mathf.PI * k) * Mathf.Lerp(1f, 1f - k, Mathf.Clamp01(-swell));

            float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += lpC * (nz - lp);
            hp += hpC * (lp - hp);
            buf[j] += (lp - hp) * env * amp;
        }
    }

    // A small iron catch. Very short, very inharmonic, no warmth — the counterpoint to the wood.
    private static void Latch(float[] buf, float atSeconds, float amp, System.Random rng, float hz = 2400f)
    {
        int start = Mathf.RoundToInt(atSeconds * SampleRate);
        float[] ratio = { 1f, 2.41f, 3.83f };
        float[] gain = { 1f, 0.5f, 0.22f };
        for (int i = start; i < buf.Length; i++)
        {
            float t = (float)(i - start) / SampleRate;
            float env = Mathf.Exp(-78f * t);
            if (env < 0.0008f) break;
            float s = 0f;
            for (int k = 0; k < ratio.Length; k++)
                s += Mathf.Sin(2f * Mathf.PI * hz * ratio[k] * t) * gain[k] * Mathf.Exp(-120f * k * t);
            float click = t < 0.002f ? (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f : 0f;
            buf[i] += (s * 0.22f + click) * env * amp;
        }
    }

    private static AudioClip BuildUIMove()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.10f)];
        var rng = new System.Random(9101);
        // A card edge brushing past. The quietest sound in the game — it fires on every arrow key,
        // and anything with a pitch in it becomes a melody once you hold the key down.
        Rustle(dry, 0f, 0.045f, 0.085f, rng, 3200f, 0f);
        return Finalize(dry, 0.04f, 7200f);
    }

    private static AudioClip BuildUIConfirm()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.28f)];
        var rng = new System.Random(9102);
        Thud(dry, 0f, 300f, 0.130f, 30f, rng);        // it seats
        Latch(dry, 0.028f, 0.075f, rng, 2600f);       // and catches
        return Finalize(dry, 0.075f, 6400f);
    }

    private static AudioClip BuildUICancel()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.26f)];
        var rng = new System.Random(9103);
        // The same wood, lower and shorter, and crucially NO latch — nothing engaged. That absence
        // is the whole difference from Confirm, and absence reads faster than a different pitch.
        Thud(dry, 0f, 232f, 0.115f, 40f, rng, 0.7f);
        return Finalize(dry, 0.06f, 4800f);
    }

    private static AudioClip BuildUIRefuse()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.20f)];
        var rng = new System.Random(9104);
        // ring = 0.12: almost all contact, almost no tone. A dead knock is what something immovable
        // sounds like. Followed by a short scrape — the thing was pushed and did not give.
        Thud(dry, 0f, 190f, 0.235f, 62f, rng, 0.12f);
        Rustle(dry, 0.020f, 0.070f, 0.045f, rng, 900f, -0.8f);
        return Finalize(dry, 0.03f, 3400f);           // driest and dullest in the family
    }

    private static AudioClip BuildUIOpen()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.38f)];
        var rng = new System.Random(9105);
        Latch(dry, 0f, 0.105f, rng, 2200f);          // the catch lets go...
        Rustle(dry, 0.020f, 0.185f, 0.155f, rng, 1500f, 0.85f);  // ...and it slides out, opening up
        return Finalize(dry, 0.10f, 6600f);
    }

    private static AudioClip BuildUIClose()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.38f)];
        var rng = new System.Random(9106);
        // The same gesture inverted — but physically, not melodically: the slide comes FIRST and
        // the impact lands at the END, which is what closing a thing actually sounds like.
        Rustle(dry, 0f, 0.165f, 0.120f, rng, 1500f, -0.85f);
        Thud(dry, 0.150f, 250f, 0.115f, 38f, rng, 0.6f);
        return Finalize(dry, 0.085f, 5400f);
    }

    // =============================================================================================
    // THE NINJA — thrown steel, cloth, and displaced air.
    //
    // A SIXTH family, and it is built on the one axis none of the others use: FLUTTER RATE. Every
    // family above is separated by material (magic = harmonic bells, metal = inharmonic bar modes,
    // stone = noise and sub, paper = no pitch at all) or by envelope (the pause pair) or by pitch
    // motion (UI). These sounds are all the SAME two materials — steel and air — so material cannot
    // tell them apart. What can is how fast the thing is turning:
    //
    //     ShurikenThrow   flutter 62 Hz   a small star spinning far too fast to count
    //     KatanaThrow     flutter 11 Hz   a long blade going end over end, slowly
    //     KatanaPlant     tremolo 7 Hz    buried in stone and quivering
    //     NinjaDash       NO flutter      a body, not a blade — the absence IS the message
    //
    // ⚠️ THE FLUTTER IS THE WHOLE IDENTITY OF A THROWN SHURIKEN, and it is the thing the old whoosh
    // was missing (designer, 2026-08-22: "i dont like the current shuriken throw whoosh"). A plain
    // swept-noise whip is a generic swing — it is what `FreefallBlade` and `ZombieSwing` already are,
    // and adding a third would just be a quieter version of one of them. A star chops the air four
    // times per revolution; that chopping is audible, and it is the only reason the sound says
    // "spinning object" rather than "something moved fast".
    //
    // ⚠️ AND ALL FOUR SHARE ONE VOICE — literally the same `Whip` call — for the same reason the six
    // UI sounds share `WoodTap`: a family assembled from hand-tuned one-offs drifts apart the moment
    // anyone retunes one of them.

    private static AudioClip shurikenThrow, shurikenStick, shurikenCatch, shurikenRecall;
    private static AudioClip ninjaDash, katanaThrow, katanaPlant, ninjaBlink;

    /// <summary>A star leaving the hand. Fast air, chopped by the spin.</summary>
    public static AudioClip ShurikenThrow
    { get { if (shurikenThrow == null) shurikenThrow = BuildShurikenThrow(); return shurikenThrow; } }

    /// <summary>A star biting stone — and therefore becoming ammo lying on the floor.</summary>
    public static AudioClip ShurikenStick
    { get { if (shurikenStick == null) shurikenStick = BuildShurikenStick(); return shurikenStick; } }

    /// <summary>Picking one up.</summary>
    public static AudioClip ShurikenCatch
    { get { if (shurikenCatch == null) shurikenCatch = BuildShurikenCatch(); return shurikenCatch; } }

    /// <summary>The boss calling every loose star home at once.</summary>
    public static AudioClip ShurikenRecall
    { get { if (shurikenRecall == null) shurikenRecall = BuildShurikenRecall(); return shurikenRecall; } }

    /// <summary>The dash launch: cloth cracking, air shoved aside, and weight leaving the floor.</summary>
    public static AudioClip NinjaDash
    { get { if (ninjaDash == null) ninjaDash = BuildNinjaDash(); return ninjaDash; } }

    /// <summary>The katana thrown — a long blade tumbling and singing.</summary>
    public static AudioClip KatanaThrow
    { get { if (katanaThrow == null) katanaThrow = BuildKatanaThrow(); return katanaThrow; } }

    /// <summary>The katana landing. ⚠️ THIS IS A TELEGRAPH, so it RINGS ON — see the builder.</summary>
    public static AudioClip KatanaPlant
    { get { if (katanaPlant == null) katanaPlant = BuildKatanaPlant(); return katanaPlant; } }

    /// <summary>He arrives. Air collapsing inward, a dull pop, and the room breathing back.</summary>
    public static AudioClip NinjaBlink
    { get { if (ninjaBlink == null) ninjaBlink = BuildNinjaBlink(); return ninjaBlink; } }

    // The blade-through-air voice, shared by every sound in this family.
    //
    // The band centre RISES to the pass and FALLS away after it, which is the doppler of something
    // going by rather than something being switched on — a static band reads as a hiss no matter how
    // it is enveloped. `flutterDepth` 0 leaves it as a plain whip (used by the dash, where the moving
    // object has no blades).
    private static void Whip(float[] buf, float atSeconds, float dur, float amp, System.Random rng,
                             float startHz, float peakHz, float endHz,
                             float flutterHz, float flutterDepth)
    {
        int start = Mathf.RoundToInt(atSeconds * SampleRate);
        int n = Mathf.RoundToInt(dur * SampleRate);
        if (n <= 0) return;

        float lp = 0f, hp = 0f;
        for (int i = 0; i < n; i++)
        {
            int j = start + i;
            if (j < 0 || j >= buf.Length) continue;

            float k = (float)i / n;
            float t = (float)i / SampleRate;

            float centre = k < 0.5f
                ? Mathf.Lerp(startHz, peakHz, k * 2f)
                : Mathf.Lerp(peakHz, endHz, (k - 0.5f) * 2f);
            centre = Mathf.Min(centre, SampleRate * 0.40f);

            float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * (centre * 1.7f) / SampleRate)) * (nz - lp);
            hp += (1f - Mathf.Exp(-2f * Mathf.PI * (centre * 0.62f) / SampleRate)) * (lp - hp);

            // The chop. Never allowed to reach zero — a fully gated stream reads as a broken speaker
            // rather than as a spinning object.
            float flutter = flutterDepth > 0f
                ? 1f - flutterDepth * 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * flutterHz * t))
                : 1f;

            // Fast in, slow out: the approach is shorter than the departure.
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.62f));

            buf[j] += (lp - hp) * env * flutter * amp;
        }
    }

    // Steel ringing: the metal family's inharmonic free-bar modes, plus an optional TREMOLO. A blade
    // in the air tumbles and one buried in stone quivers, and in both cases the ring is amplitude
    // modulated by that movement. Without the wobble this is a struck bar sitting on a bench.
    private static void Blade(float[] buf, float atSeconds, float hz, float amp, float decay,
                              System.Random rng, float tremHz = 0f, float tremDepth = 0f)
    {
        int start = Mathf.RoundToInt(atSeconds * SampleRate);
        if (start >= buf.Length) return;

        float[] ratio = { 1f, 2.76f, 5.40f, 8.93f };
        float[] gain = { 1f, 0.44f, 0.19f, 0.08f };

        for (int i = start; i < buf.Length; i++)
        {
            float t = (float)(i - start) / SampleRate;
            float env = Mathf.Exp(-decay * t);
            if (env < 0.0006f) break;

            float s = 0f;
            for (int k = 0; k < ratio.Length; k++)
                s += Mathf.Sin(2f * Mathf.PI * hz * ratio[k] * t) * gain[k]
                     * Mathf.Exp(-decay * (1f + 0.9f * k) * t);

            float trem = tremHz > 0f
                ? 1f - tremDepth * 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * tremHz * t))
                : 1f;

            // The edge catching. A couple of milliseconds, so the tone starts as a strike rather
            // than as an oscillator being switched on.
            float edge = t < 0.003f ? (float)(rng.NextDouble() * 2.0 - 1.0) * 0.30f * (1f - t / 0.003f) : 0f;

            buf[i] += (s * 0.30f + edge) * env * trem * amp;
        }
    }

    // A gliding low sine. Phase is integrated rather than sin(2*pi*f*t) so the pitch can move without
    // the waveform tearing — the same reason MeteorImpact's sub does it.
    private static void Sub(float[] buf, float atSeconds, float fromHz, float toHz,
                            float amp, float decay, float glide)
    {
        int start = Mathf.RoundToInt(atSeconds * SampleRate);
        if (start >= buf.Length) return;

        float phase = 0f;
        for (int i = start; i < buf.Length; i++)
        {
            float t = (float)(i - start) / SampleRate;
            float env = Mathf.Exp(-decay * t);
            if (env < 0.0006f) break;

            float f = Mathf.Lerp(fromHz, toHz, Mathf.Clamp01(t / Mathf.Max(0.001f, glide)));
            phase += 2f * Mathf.PI * f / SampleRate;
            buf[i] += Mathf.Sin(phase) * env * amp;
        }
    }

    private static AudioClip BuildShurikenThrow()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.30f)];
        var rng = new System.Random(6601);

        // The release: fingers and cloth letting go. Short enough to be an attack, not a layer.
        Rustle(dry, 0f, 0.032f, 0.26f, rng, 2600f, -1f);

        // ⚠️ 62 Hz FLUTTER. This is the sound. See the family note above.
        Whip(dry, 0.004f, 0.235f, 0.80f, rng, 2600f, 7200f, 3000f, 62f, 0.55f);

        // A thin high edge, well above KatanaThrow's 820 — a small piece of steel has a small voice.
        Blade(dry, 0.008f, 2450f, 0.090f, 34f, rng);

        return Finalize(dry, 0.09f, 15000f);   // dry and bright: this happens right next to you
    }

    private static AudioClip BuildShurikenStick()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.26f)];
        var rng = new System.Random(6602);

        Latch(dry, 0f, 0.44f, rng, 2900f);                 // iron biting
        Thud(dry, 0.001f, 190f, 0.26f, 72f, rng, 0.10f);   // ring 0.10: stone chips, it does not sing

        // ⚠️ AND THEN IT QUIVERS. A star driven into rock is still vibrating, and that half-second of
        // wobble is what makes this read as "it stuck" rather than "it hit". It is also the cue that
        // there is now ammo on the floor, which is the whole reason this fight is winnable with an
        // empty deck — so it deliberately outlasts the impact.
        Blade(dry, 0.004f, 2300f, 0.085f, 24f, rng, 9f, 0.5f);

        return Finalize(dry, 0.10f, 12000f);
    }

    private static AudioClip BuildShurikenCatch()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.17f)];
        var rng = new System.Random(6603);

        // ⚠️ TWO TICKS, NOT ONE, and no tail at all. Two pieces of steel meeting is what picking a
        // star out of a wall and putting it with the others sounds like; a single chime would be a
        // menu confirm. And a handful get collected in a couple of seconds, so anything that rings
        // stacks into a chord — the same rule CrystalCollect is built on.
        Latch(dry, 0f, 0.44f, rng, 3400f);
        Latch(dry, 0.026f, 0.28f, rng, 2650f);
        Rustle(dry, 0.002f, 0.048f, 0.095f, rng, 2000f, -1f);

        return Finalize(dry, 0.05f, 13000f);
    }

    private static AudioClip BuildShurikenRecall()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.90f)];
        var rng = new System.Random(6604);

        // ⚠️ THEY CONVERGE. The starts are staggered but every whip PEAKS at roughly the same moment,
        // so a scatter of separate objects becomes one arrival — which is exactly what the recall is,
        // and it is why this reads as a summons rather than as six stars being thrown.
        float[] at = { 0.00f, 0.035f, 0.075f, 0.105f, 0.150f, 0.185f, 0.225f };
        for (int i = 0; i < at.Length; i++)
        {
            float dur = 0.62f - at[i];
            Whip(dry, at[i], dur, 0.24f, rng, 700f, 4600f + i * 320f, 3200f, 48f + i * 6f, 0.5f);
        }

        // The pull itself: a low tone rising under the whole thing. Rising, because gravity falls and
        // this is the opposite of gravity.
        Sub(dry, 0.0f, 58f, 104f, 0.16f, 3.4f, 0.42f);

        // ...and they arrive. A tight cluster of steel, not one hit.
        Latch(dry, 0.470f, 0.30f, rng, 2700f);
        Latch(dry, 0.492f, 0.26f, rng, 3100f);
        Latch(dry, 0.508f, 0.22f, rng, 2300f);
        Blade(dry, 0.478f, 1900f, 0.09f, 15f, rng, 12f, 0.4f);

        return Finalize(dry, 0.20f, 11000f);
    }

    private static AudioClip BuildNinjaDash()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.38f)];
        var rng = new System.Random(6605);

        // Cloth cracking taut. This is the transient, and it is the only part of the dash that is
        // allowed to be sharp — the movement itself is a shove of air, not an impact.
        Rustle(dry, 0f, 0.042f, 0.44f, rng, 1400f, -1f);

        // ⚠️ NO FLUTTER, AND LOW. A body is not a blade. Against ShurikenThrow (2600-7200 Hz, chopped)
        // this sits an octave and a half down and runs smooth, so the two can never be confused even
        // though they are made of the same ingredients. That contrast is the whole reason the family
        // shares one function.
        Whip(dry, 0.004f, 0.215f, 0.62f, rng, 480f, 2500f, 680f, 0f, 0f);

        // The weight leaving the floor.
        Sub(dry, 0.006f, 78f, 42f, 0.28f, 13f, 0.14f);

        // Cloth settling behind him.
        Rustle(dry, 0.150f, 0.130f, 0.095f, rng, 900f, -0.9f);

        return Finalize(dry, 0.12f, 6000f);   // darker than anything else in the family
    }

    private static AudioClip BuildKatanaThrow()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.52f)];
        var rng = new System.Random(6606);

        Rustle(dry, 0f, 0.030f, 0.16f, rng, 1800f, -1f);

        // ⚠️ 11 Hz — SIX TIMES SLOWER THAN THE STAR. A katana goes end over end a handful of times
        // across a room; you can hear each pass. Same function, same materials, and it is instantly a
        // different object. Do not "fix" this toward the shuriken's rate.
        Whip(dry, 0.004f, 0.400f, 0.68f, rng, 900f, 3600f, 1200f, 11f, 0.62f);

        // The blade singing, tremolo locked to the tumble so the two halves are one object.
        Blade(dry, 0.006f, 820f, 0.17f, 9f, rng, 11f, 0.35f);

        return Finalize(dry, 0.16f, 9000f);
    }

    private static AudioClip BuildKatanaPlant()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.80f)];
        var rng = new System.Random(6607);

        Latch(dry, 0f, 0.82f, rng, 2100f);                 // deep iron, lower than the star's bite
        Thud(dry, 0.002f, 150f, 0.56f, 55f, rng, 0.12f);   // the stone taking it

        // ⚠️ AND IT RINGS ON FOR HALF A SECOND. This clip is a TELEGRAPH, not an impact: the planted
        // blade is where he is about to be, and it sits there while the player reads it. A sound that
        // stops dead says the event is over; a blade still humming says something has not happened
        // yet. Longest tail in the family, and that is deliberate.
        Blade(dry, 0.006f, 640f, 0.19f, 5.5f, rng, 7f, 0.55f);

        return Finalize(dry, 0.22f, 7000f);
    }

    private static AudioClip BuildNinjaBlink()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.42f)];
        var rng = new System.Random(6608);

        // ⚠️ NOT ELECTRIC. The jump SFX hunt already settled this for the whole game: a flanged,
        // swept, sci-fi teleport is wrong for a candlelit dungeon, and a ninja vanishing is a cloth
        // and smoke trick rather than a machine. So the arrival is AIR COLLAPSING INWARD — a band
        // sweeping DOWN, which is the inverse of every other whip in this family.
        Whip(dry, 0f, 0.098f, 0.58f, rng, 4600f, 2200f, 380f, 0f, 0f);

        // The pop. ring 0.05 = almost pure contact: displaced air closing, with nothing to resonate.
        Thud(dry, 0.095f, 118f, 0.36f, 62f, rng, 0.05f);

        // ...and the room breathing back around him.
        Rustle(dry, 0.104f, 0.210f, 0.150f, rng, 1900f, 0.6f);

        return Finalize(dry, 0.18f, 6500f);
    }

    // =============================================================================================
    // BOSSES + PORTAL (2026-09-24) — the last silent events on the run's critical path.
    //
    // Measured before writing these: every boss death in the game was SILENT. All three bosses hand
    // an empty `deathSound` to BossDeathVFX, and `SfxManager.PlayOn` with a null clip is a no-op —
    // so the single biggest moment of a run played to a freeze-frame, a screen flash and nothing.
    // The Moss Knight's awakening stomps and his leap were silent the same way, and the Portal
    // component had no AudioClip field at all.
    //
    // ⚠️ THE BOSS CLIPS ARE ARMOURED STONE, NOT MAGIC. A boss is a body with weight; the Shift
    // family's bell partials belong to the player's resource. The one exception is the death TOLL
    // below, and it is there on purpose: it is the only sound in the game that says "the fight is
    // over", so it borrows the one voice that is never used for an impact.

    private static AudioClip bossDeath, bossStomp, bossLeap, portalPass;

    /// <summary>A champion falling: the killing blow, armour collapsing, a low toll, and dust.</summary>
    public static AudioClip BossDeath
    { get { if (bossDeath == null) bossDeath = BuildBossDeath(); return bossDeath; } }

    /// <summary>An armoured foot hitting stone hard enough to shake the room.</summary>
    public static AudioClip BossStomp
    { get { if (bossStomp == null) bossStomp = BuildBossStomp(); return bossStomp; } }

    /// <summary>A heavy body leaving the floor: push-off, armour, and a lot of air moved.</summary>
    public static AudioClip BossLeap
    { get { if (bossLeap == null) bossLeap = BuildBossLeap(); return bossLeap; } }

    /// <summary>Stepping through a portal. Short, magic-family, and gone before you have landed.</summary>
    public static AudioClip PortalPass
    { get { if (portalPass == null) portalPass = BuildPortalPass(); return portalPass; } }

    // Harmonic partials — the magic family's voice, the same series AltarPay uses — with a pitch
    // glide. Phase is integrated per partial so the glide cannot tear the waveform.
    private static void Bell(float[] buf, float atSeconds, float fromHz, float toHz, float glide,
                             float amp, float decay)
    {
        int start = Mathf.RoundToInt(atSeconds * SampleRate);
        if (start >= buf.Length) return;

        float[] partial = { 1f, 2f, 3f, 4f, 5.1f };
        float[] gain = { 1f, 0.52f, 0.30f, 0.16f, 0.09f };
        var phase = new float[partial.Length];

        for (int i = start; i < buf.Length; i++)
        {
            float t = (float)(i - start) / SampleRate;
            if (Mathf.Exp(-decay * t) < 0.0006f) break;

            float root = Mathf.Lerp(fromHz, toHz, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.001f, glide))));
            float s = 0f;
            for (int p = 0; p < partial.Length; p++)
            {
                phase[p] += 2f * Mathf.PI * root * partial[p] / SampleRate;
                s += Mathf.Sin(phase[p]) * gain[p] * Mathf.Exp(-decay * (1f + 0.7f * p) * t);
            }
            buf[i] += s * amp;
        }
    }

    // ⚠️ IT IS A SEQUENCE, NOT A HIT — and the gap in the middle is the point. The killing blow and
    // the body landing are half a second apart, which is the stagger BossDeathVFX's slow-mo plays
    // over. A single big impact would say "he was hit hard"; blow, beat, collapse says "he fell".
    private static AudioClip BuildBossDeath()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 2.9f)];
        var rng = new System.Random(7701);

        // 1. THE BLOW — a hard strike with an armoured edge on it.
        Thud(dry, 0f, 94f, 0.80f, 13f, rng, 0.35f);
        Latch(dry, 0.001f, 0.55f, rng, 1650f);
        Sub(dry, 0.0f, 104f, 30f, 0.50f, 1.7f, 0.85f);

        // 2. THE COLLAPSE — the body landing, then plate and weapon clattering down after it. The
        // pieces are scattered and quieten as they go, the same rule WallBreak's rubble follows.
        Thud(dry, 0.52f, 68f, 0.80f, 11f, rng, 0.25f);
        Sub(dry, 0.52f, 82f, 34f, 0.34f, 3.2f, 0.30f);
        for (int k = 0; k < 7; k++)
        {
            float at = 0.56f + k * 0.055f + (float)rng.NextDouble() * 0.04f;
            float hz = 1500f + (float)rng.NextDouble() * 1100f;
            Latch(dry, at, 0.34f * (1f - k / 8f), rng, hz);
        }

        // 3. THE TOLL — one low bell, settling a whole step down and ringing out under the dust.
        // Nothing else in the game is a low bell, so this is unmistakably the end of something.
        Bell(dry, 0.60f, 110f, 98f, 0.9f, 0.20f, 1.8f);

        // 4. DUST — low noise swelling in after the collapse and thinning out over two seconds.
        float lp = 0f;
        for (int i = Mathf.RoundToInt(0.55f * SampleRate); i < dry.Length; i++)
        {
            float t = (float)i / SampleRate - 0.55f;
            float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * 620f / SampleRate)) * (nz - lp);
            dry[i] += lp * Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-1.9f * t) * 0.30f;
        }

        return Finalize(dry, 0.32f, 5000f);   // a big room, heard from inside it
    }

    // ⚠️ THIS REPEATS. The awakening stomps several times in a row, so the tail is short and the
    // weight lives in the first 150ms — a long debris tail would smear consecutive stomps together.
    private static AudioClip BuildBossStomp()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.75f)];
        var rng = new System.Random(7702);

        Thud(dry, 0f, 72f, 0.82f, 19f, rng, 0.25f);          // the foot, dead and heavy
        Sub(dry, 0.0f, 90f, 34f, 0.56f, 6.0f, 0.18f);        // the floor taking it
        Latch(dry, 0.004f, 0.22f, rng, 1800f);               // armour jolting
        Latch(dry, 0.048f, 0.12f, rng, 2350f);

        float lp = 0f;
        for (int i = 0; i < dry.Length; i++)
        {
            float t = (float)i / SampleRate;
            float nz = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (1f - Mathf.Exp(-2f * Mathf.PI * 900f / SampleRate)) * (nz - lp);
            dry[i] += lp * Mathf.Clamp01((t - 0.012f) / 0.02f) * Mathf.Exp(-9f * t) * 0.26f;
        }

        return Finalize(dry, 0.22f, 5200f);
    }

    // The NinjaDash recipe at twice the mass: the same ingredients, an octave lower, with armour on.
    // Keeping the two related is deliberate — "a body launching" should sound like one family.
    private static AudioClip BuildBossLeap()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.58f)];
        var rng = new System.Random(7703);

        Rustle(dry, 0f, 0.050f, 0.36f, rng, 1000f, -1f);                  // moss and cloth pulled taut
        Thud(dry, 0.002f, 96f, 0.50f, 30f, rng, 0.15f);                   // pushing off the stone
        Latch(dry, 0.010f, 0.18f, rng, 2000f);                            // plate shifting
        Whip(dry, 0.018f, 0.400f, 0.72f, rng, 220f, 1500f, 360f, 0f, 0f); // a LOT of air
        Sub(dry, 0.005f, 70f, 40f, 0.30f, 9f, 0.20f);                     // weight leaving the floor

        return Finalize(dry, 0.14f, 5500f);
    }

    // ⚠️ SHORT AND DRY, BECAUSE IT IS INSTANT. Traversal teleports you on the frame you touch the
    // portal, so anything that swells arrives after you have. That is also why Portal.mp3 is NOT
    // used here: it takes half a second to peak, which fits the portal OPENING and lags stepping
    // through. This one is all attack — air sucked inward, then a small bell bloom as you land.
    private static AudioClip BuildPortalPass()
    {
        var dry = new float[Mathf.CeilToInt(SampleRate * 0.40f)];
        var rng = new System.Random(7704);

        Whip(dry, 0f, 0.110f, 0.52f, rng, 5200f, 2600f, 520f, 0f, 0f);   // collapsing inward, like the blink
        Bell(dry, 0.060f, 523f, 392f, 0.08f, 0.13f, 9f);                  // falls a fourth: you arrived

        return Finalize(dry, 0.16f, 10000f);
    }

    private static float[] ApplyReverbAndWarmth(float[] dry, float wet, float masterLpHz)
    {
        int n = dry.Length;

        int[] combLen = { 811, 971, 1123 };
        const float combFb = 0.66f;
        const float damp = 0.34f;
        var combBuf = new float[combLen.Length][];
        var combPos = new int[combLen.Length];
        var combStore = new float[combLen.Length];
        for (int k = 0; k < combLen.Length; k++) combBuf[k] = new float[combLen[k]];

        const int apLen = 421;
        const float apG = 0.5f;
        var apBuf = new float[apLen];
        int apPos = 0;

        float masterLp = 0f;
        float masterCoef = 1f - Mathf.Exp(-2f * Mathf.PI * masterLpHz / SampleRate);

        var outp = new float[n];
        for (int i = 0; i < n; i++)
        {
            float input = dry[i];

            float combSum = 0f;
            for (int k = 0; k < combLen.Length; k++)
            {
                float y = combBuf[k][combPos[k]];
                combStore[k] += damp * (y - combStore[k]);
                combBuf[k][combPos[k]] = input + combStore[k] * combFb;
                combPos[k] = (combPos[k] + 1) % combLen[k];
                combSum += y;
            }
            combSum /= combLen.Length;

            float bufout = apBuf[apPos];
            float apOut = -combSum + bufout;
            apBuf[apPos] = combSum + bufout * apG;
            apPos = (apPos + 1) % apLen;

            float mixed = dry[i] + apOut * wet;

            masterLp += masterCoef * (mixed - masterLp);
            outp[i] = Mathf.Clamp(masterLp, -0.95f, 0.95f);
        }
        return outp;
    }
}
