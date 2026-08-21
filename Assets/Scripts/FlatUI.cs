using UnityEngine;

// Deckshift's procedural UI kit — the alternative to RelicUISprites' ornate stone-and-gold chrome.
//
// THE BRIEF (designer, 2026-08-03): the first pass at this was "soothing, simple" but read as
// generic — flat slate-blue panels, uniform rounded corners, one accent. That is the house style of
// every dev dashboard on earth and has no PLACE in it. It needed to feel like Deckshift's world
// without going back to ornament.
//
// THE ANSWER: a sheet of iron on a workbench, lit by the forge. Same restraint, but every choice
// now points at Act 1's Oxidation District:
//   · WARM charcoal, not slate-blue. The district is rust and corrosion, not brushed steel. This
//     single palette shift does most of the work.
//   · CHAMFERED corners, not rounded. Cut plate reads as a made object; a uniform radius reads as
//     a web card. This is the biggest silhouette cue.
//   · Light on the TOP LIP only, plus an EMBER GLOW rising from the bottom edge — the forge fire
//     below the bench. Uneven, directional light reads as a physical thing in a place; a uniform
//     glowing border reads as a UI widget.
//   · RIVETS and faint SCUFFS. Small, dark, functional — fasteners, not jewels. Imperfection is
//     what kills the "generated" feel, and it costs almost nothing.
//   · Rules score across and FADE AT THE ENDS instead of running edge to edge like a CSS border.
//
// Everything here is a WHITE shape meant to be tinted by Image.color, so one cached sprite serves
// every panel in any colour, and all of it is 9-sliced where it needs to stretch.
public static class FlatUI
{
    private static Sprite plateLarge, plateSmall, outlineLarge, outlineSmall;
    private static Sprite softGlow, verticalFade, horizontalFade, bottomGlow, fadedRule, rivet, pixel;
    private static Sprite emberDot, fourPointStar, arcaneSigil, arcaneSeal;
    private static Sprite calibrationMark, sweepLine;
    private static Sprite pinTack, waxSeal;
    private static Sprite[] raritySigils;   // one glyph per Rarity — see RaritySigil

    // Solid chamfered plate. chamfer 10 = windows, 5 = cards and buttons.
    public static Sprite Panel(int chamfer = 10)
    {
        if (chamfer <= 6)
        {
            if (plateSmall == null) plateSmall = BuildPlate(5, 0);
            return plateSmall;
        }
        if (plateLarge == null) plateLarge = BuildPlate(10, 0);
        return plateLarge;
    }

    // Chamfered OUTLINE only, stacked over a Panel so the edge tints independently of the fill.
    public static Sprite Outline(int chamfer = 10, int thickness = 2)
    {
        if (chamfer <= 6)
        {
            if (outlineSmall == null) outlineSmall = BuildPlate(5, Mathf.Max(1, thickness));
            return outlineSmall;
        }
        if (outlineLarge == null) outlineLarge = BuildPlate(10, Mathf.Max(1, thickness));
        return outlineLarge;
    }

    // A small domed fastener: dark body, lit along its top edge so it reads as raised metal.
    public static Sprite Rivet()
    {
        if (rivet != null) return rivet;

        const int S = 16;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                // Light from above-left: brighten the upper edge, darken the lower.
                float lit = Mathf.Clamp01(0.5f + (dy * 0.55f - dx * 0.25f));
                float v = Mathf.Lerp(0.35f, 1f, lit);
                float a = Mathf.Clamp01((1f - d) * 6f);   // soft 1px rim
                tex.SetPixel(x, y, new Color(v, v, v, a));
            }
        tex.Apply();
        rivet = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return rivet;
    }

    // A scored rule: solid in the middle, fading out at both ends. Reads as a scratch across metal
    // rather than a border drawn to the edges of a box.
    public static Sprite FadedRule()
    {
        if (fadedRule != null) return fadedRule;

        const int W = 128;
        Texture2D tex = NewTex(W, 1);
        for (int x = 0; x < W; x++)
        {
            float t = (float)x / (W - 1);
            // Fade over the outer ~22% at each end.
            float a = Mathf.Clamp01(Mathf.Min(t, 1f - t) / 0.22f);
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        fadedRule = Sprite.Create(tex, new Rect(0, 0, W, 1), new Vector2(0.5f, 0.5f), 100f);
        return fadedRule;
    }

    // Radial falloff, used behind a selected card so the highlight bleeds softly outward.
    public static Sprite SoftGlow()
    {
        if (softGlow != null) return softGlow;

        const int S = 64;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        softGlow = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return softGlow;
    }

    // Forge-fire wash: strongest along the BOTTOM edge, fading upward AND toward the left/right
    // ends.
    //
    // The horizontal falloff is the whole point. The first version reused VerticalFade, which only
    // falls off in Y — inset from the panel sides, it hard-cut vertically and left a visible seam
    // down both edges of the window. Any glow that doesn't reach its container's edge must fade on
    // that axis too, or it draws its own border.
    public static Sprite BottomGlow()
    {
        if (bottomGlow != null) return bottomGlow;

        const int W = 96, H = 48;
        Texture2D tex = NewTex(W, H);
        for (int y = 0; y < H; y++)
        {
            float v = 1f - (float)y / (H - 1);       // 1 at the bottom row
            float vy = v * v;                         // squared: hugs the edge, no hard stop
            for (int x = 0; x < W; x++)
            {
                float t = (float)x / (W - 1);
                float hx = Mathf.Clamp01(Mathf.Min(t, 1f - t) / 0.28f);
                hx = hx * hx * (3f - 2f * hx);        // smoothstep the ends
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, vy * hx));
            }
        }
        tex.Apply();
        bottomGlow = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        return bottomGlow;
    }

    // Opaque at the LEFT fading to nothing rightward — VerticalFade's other axis.
    //
    // Exists so an effect can hug a vertical edge. You cannot get there by rotating VerticalFade:
    // rotating a stretched RectTransform turns the whole strip out of the screen. Mirror this one
    // with localScale.x = -1 (on a centred pivot) for the right-hand edge.
    public static Sprite HorizontalFade()
    {
        if (horizontalFade != null) return horizontalFade;

        const int W = 64;
        Texture2D tex = NewTex(W, 1);
        for (int x = 0; x < W; x++)
        {
            float t = 1f - (float)x / (W - 1);
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, t * t));
        }
        tex.Apply();
        horizontalFade = Sprite.Create(tex, new Rect(0, 0, W, 1), new Vector2(0.5f, 0.5f), 100f);
        return horizontalFade;
    }

    // Opaque at the top fading to nothing downward. Used for the top-lip sheen.
    public static Sprite VerticalFade()
    {
        if (verticalFade != null) return verticalFade;

        const int H = 64;
        Texture2D tex = NewTex(1, H);
        for (int y = 0; y < H; y++)
        {
            float t = (float)y / (H - 1);
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, t * t));
        }
        tex.Apply();
        verticalFade = Sprite.Create(tex, new Rect(0, 0, 1, H), new Vector2(0.5f, 0.5f), 100f);
        return verticalFade;
    }

    // A four-point sparkle. Blompo's answer to the Forge's rivet: where the workbench is held
    // together by fasteners, the mythic panel is pinned by points of light.
    public static Sprite FourPointStar()
    {
        if (fourPointStar != null) return fourPointStar;

        const int S = 32;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);

                // Two crossed tapered spikes, plus a bright core where they meet.
                float horiz = Mathf.Clamp01(1f - ax) * Mathf.Clamp01(1f - ay / 0.16f);
                float vert = Mathf.Clamp01(1f - ay) * Mathf.Clamp01(1f - ax / 0.16f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Clamp01(1f - d * 3.4f);

                float a = Mathf.Clamp01(Mathf.Max(horiz, vert) * Mathf.Max(horiz, vert) + core);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        fourPointStar = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return fourPointStar;
    }

    // A single floating ember: a hot core with a soft halo. Deliberately not a hard dot — at the
    // 2-4px these are drawn at, a hard-edged square reads as a dead pixel rather than a spark.
    public static Sprite EmberDot()
    {
        if (emberDot != null) return emberDot;

        const int S = 16;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float halo = Mathf.Clamp01(1f - d);
                float core = Mathf.Clamp01(1f - d * 2.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(halo * halo * 0.55f + core)));
            }
        tex.Apply();
        emberDot = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return emberDot;
    }

    // An arcane MARK — the emblem above each of Blompo's offers.
    //
    // Replaced a plain four-point sparkle, which read as a lens flare rather than as a symbol
    // somebody drew. The difference is structure: a containing ring, rays of two different
    // lengths, and tick marks outside the ring all say "this was inscribed". A soft blob with
    // four points says "bloom effect".
    public static Sprite ArcaneSigil()
    {
        if (arcaneSigil != null) return arcaneSigil;

        const int S = 192;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { tex.SetPixel(x, y, Clear); continue; }
                float ang = Mathf.Atan2(dy, dx);

                // Containing ring.
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.70f) / 0.030f);

                // Eight rays: long ones on the axes, shorter on the diagonals.
                float axis = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang)), 26f) * Falloff(d, 0.66f);
                float diag = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * (ang - Mathf.PI * 0.25f))), 44f) * Falloff(d, 0.42f);

                // Four ticks just outside the ring, on the diagonals.
                float tickBand = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.075f);
                float tick = tickBand * Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * (ang - Mathf.PI * 0.25f))), 90f);

                float core = Mathf.Pow(Mathf.Clamp01(1f - d * 7f), 2f);

                float a = Mathf.Clamp01(ring * 0.85f + axis + diag * 0.8f + tick * 0.9f + core);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        arcaneSigil = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return arcaneSigil;
    }

    // A binding circle: two concentric rings, radial ticks between them, and four diamond glyphs
    // on the cardinals. Used for the moment a blessing is pressed into a card.
    public static Sprite ArcaneSeal()
    {
        if (arcaneSeal != null) return arcaneSeal;

        const int S = 256;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { tex.SetPixel(x, y, Clear); continue; }
                float ang = Mathf.Atan2(dy, dx);

                float outer = Mathf.Clamp01(1f - Mathf.Abs(d - 0.94f) / 0.016f);
                float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 0.62f) / 0.030f);
                float faint = Mathf.Clamp01(1f - Mathf.Abs(d - 0.32f) / 0.012f);

                // Twelve radial ticks spanning the gap between the two main rings.
                float gap = (d > 0.66f && d < 0.90f) ? 1f : 0f;
                float ticks = gap * Mathf.Pow(Mathf.Abs(Mathf.Cos(6f * ang)), 60f);

                // Four diamond glyphs punctuating the OUTER ring, on the diagonals.
                //
                // They were originally on the inner ring at the cardinals — at exactly that
                // radius they merged into the ring itself and disappeared. On the outer ring they
                // read, and the diagonals keep them clear of the twelve ticks (which land on
                // multiples of 30 degrees).
                float glyph = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float ga = Mathf.PI * 0.25f + k * Mathf.PI * 0.5f;
                    float gx = dx - Mathf.Cos(ga) * 0.94f;
                    float gy = dy - Mathf.Sin(ga) * 0.94f;
                    float diamond = Mathf.Abs(gx) + Mathf.Abs(gy);
                    glyph = Mathf.Max(glyph, Mathf.Clamp01(1f - diamond / 0.105f));
                }

                float a = Mathf.Clamp01(outer * 0.9f + inner + faint * 0.45f + ticks * 0.8f + glyph);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        arcaneSeal = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return arcaneSeal;
    }

    // A registration/calibration mark: a crosshair with a GAP at the centre, ringed. The Apparatus
    // theme's answer to the Forge's rivet and Blompo's star — where those are fasteners and points
    // of light, this is a measuring mark, which is what a control panel is covered in.
    //
    // The centre gap is the whole reason it reads as an instrument rather than as a plus sign: a
    // solid cross is a symbol, an interrupted one is an alignment target.
    public static Sprite CalibrationMark()
    {
        if (calibrationMark != null) return calibrationMark;

        const int S = 64;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { tex.SetPixel(x, y, Clear); continue; }

                // Crosshair arms, interrupted between 0.22 and 0.52 of the radius.
                float armH = Mathf.Clamp01(1f - Mathf.Abs(dy) / 0.045f);
                float armV = Mathf.Clamp01(1f - Mathf.Abs(dx) / 0.045f);
                float band = (d > 0.22f && d < 0.95f) ? 1f : 0f;
                float arms = Mathf.Max(armH, armV) * band;

                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.52f) / 0.045f);
                float dot = Mathf.Clamp01(1f - d / 0.09f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(arms + ring * 0.8f + dot)));
            }
        tex.Apply();
        calibrationMark = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return calibrationMark;
    }

    // A horizontal line of light with a SYMMETRIC vertical falloff and faded ends — the travelling
    // scan sweep on the Apparatus panel.
    //
    // Not BottomGlow: that one is anchored at its bottom edge, so a sweep built from it would look
    // like light welling up from a floor rather than a line passing across glass.
    public static Sprite SweepLine()
    {
        if (sweepLine != null) return sweepLine;

        const int W = 96, H = 32;
        Texture2D tex = NewTex(W, H);
        float cy = (H - 1) * 0.5f;

        for (int y = 0; y < H; y++)
        {
            float dy = Mathf.Abs(y - cy) / cy;
            float core = Mathf.Clamp01(1f - dy / 0.09f);       // the hairline itself
            float halo = Mathf.Pow(Mathf.Clamp01(1f - dy), 2.4f) * 0.42f;
            for (int x = 0; x < W; x++)
            {
                float t = (float)x / (W - 1);
                float hx = Mathf.Clamp01(Mathf.Min(t, 1f - t) / 0.18f);
                hx = hx * hx * (3f - 2f * hx);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(core + halo) * hx));
            }
        }
        tex.Apply();
        sweepLine = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        return sweepLine;
    }

    // A brass tack head — the Bulletin theme's fastener, the counterpart to Iron's rivet.
    //
    // It is lit from the upper LEFT, because that theme's light rakes across the board from the
    // left; a rivet lit from the other side on the same screen would read as a mistake before the
    // player could say why. The shading also runs a wider value range than the rivet's, so a tack
    // reads as a rounded dome catching a lamp rather than as a flush fastener.
    //
    // NOTE it carries no cast shadow. The sprite is tinted brass by Image.color, and tinting can
    // only darken toward the tint — never toward black — so a baked shadow would come out as dark
    // brass. Shadows are drawn separately with SoftGlow (see QuestBoardScreen).
    public static Sprite PinTack()
    {
        if (pinTack != null) return pinTack;

        const int S = 48;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { tex.SetPixel(x, y, Clear); continue; }

                // Dome shading: bright toward the upper-left, falling away to the lower-right.
                float lit = Mathf.Clamp01(0.52f + (-dx * 0.62f + dy * 0.42f));
                float v = Mathf.Lerp(0.26f, 1f, lit * lit);

                // Specular pip, offset toward the light.
                float sx = dx + 0.34f, sy = dy - 0.34f;
                v += Mathf.Clamp01(1f - Mathf.Sqrt(sx * sx + sy * sy) / 0.26f) * 0.55f;

                // Darkened rim so the head has an edge instead of dissolving into the paper.
                v *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((1f - d) / 0.22f));

                float a = Mathf.Clamp01((1f - d) * 7f);
                tex.SetPixel(x, y, new Color(Mathf.Clamp01(v), Mathf.Clamp01(v), Mathf.Clamp01(v), a));
            }
        tex.Apply();
        pinTack = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return pinTack;
    }

    // A blob of sealing wax with a sigil pressed into it — the mark on an accepted contract.
    //
    // The edge is deliberately IRREGULAR (three sine harmonics of the angle). A circle reads as a
    // button; only the uneven rim says "this was poured and then squashed". The pressed impression
    // is rendered DARKER rather than brighter, which is what a recess in a glossy material actually
    // does, and it survives tinting: a darker white stays a darker red.
    public static Sprite WaxSeal()
    {
        if (waxSeal != null) return waxSeal;

        const int S = 160;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);

                // Wobbly outline.
                float R = 0.86f
                        + 0.055f * Mathf.Sin(3f * ang + 0.7f)
                        + 0.038f * Mathf.Sin(5f * ang + 2.1f)
                        + 0.022f * Mathf.Sin(7f * ang + 4.4f);

                float a = Mathf.Clamp01((R - d) * 12f);
                if (a <= 0f) { tex.SetPixel(x, y, Clear); continue; }

                // Domed wax, lit from the upper left like everything else on this board.
                float lit = Mathf.Clamp01(0.55f + (-dx * 0.42f + dy * 0.30f));
                float v = Mathf.Lerp(0.62f, 1.12f, lit);

                // The pressed sigil: a ring, four short spokes on the DIAGONALS, and a centre pip.
                //
                // ⚠️ Four, not six, and they must not reach the ring. Six evenly spaced spokes
                // running from the centre out to a ring is a citrus slice — that is genuinely what
                // the first version looked like. Keeping the count low and leaving a clear gap
                // between the spoke tips and the ring is what makes it read as something pressed
                // into wax rather than as a wheel.
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.54f) / 0.055f);
                float spokeBand = (d > 0.17f && d < 0.42f) ? 1f : 0f;
                float spokes = spokeBand * Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * (ang - Mathf.PI * 0.25f))), 34f);
                float pip = Mathf.Clamp01(1f - d / 0.11f);
                v -= (ring * 0.40f + spokes * 0.26f + pip * 0.28f);

                // Squashed-out lip: slightly darker right at the rim.
                v *= Mathf.Lerp(0.68f, 1f, Mathf.Clamp01((R - d) / 0.16f));

                tex.SetPixel(x, y, new Color(Mathf.Clamp01(v), Mathf.Clamp01(v), Mathf.Clamp01(v), a));
            }
        tex.Apply();
        waxSeal = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return waxSeal;
    }

    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

    // Ray brightness: full near the centre, tapering to nothing at `reach`.
    private static float Falloff(float d, float reach)
    {
        return Mathf.Pow(Mathf.Clamp01(1f - d / reach), 1.4f);
    }

    // 1x1 white — flat fills and hard edges.
    public static Sprite Pixel()
    {
        if (pixel != null) return pixel;
        Texture2D tex = NewTex(1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        return pixel;
    }

    // Builds a chamfered rectangle. thickness 0 = filled, >0 = hollow outline of that thickness.
    private static Sprite BuildPlate(int chamfer, int thickness)
    {
        int pad = chamfer + 3;
        int S = pad * 2 + 2;

        Texture2D tex = NewTex(S);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = ChamferDistance(x + 0.5f, y + 0.5f, S, chamfer);

                float outer = Mathf.Clamp01(0.5f - d);
                float a = outer;
                if (thickness > 0)
                {
                    float inner = Mathf.Clamp01(0.5f - (d + thickness));
                    a = Mathf.Clamp01(outer - inner);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(pad, pad, pad, pad));
    }

    // Signed distance to a rectangle with its corners cut off at 45°: the box distance, then
    // intersected (max) with a diagonal half-plane that slices each corner.
    private static float ChamferDistance(float px, float py, int size, int chamfer)
    {
        float half = size * 0.5f;
        float ax = Mathf.Abs(px - half);
        float ay = Mathf.Abs(py - half);

        // Box SDF.
        float qx = ax - half, qy = ay - half;
        float box = Mathf.Min(Mathf.Max(qx, qy), 0f) +
                    Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                               Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));

        // Diagonal cut: |x| + |y| <= (half + half - chamfer), normalised to a true distance.
        float diag = (ax + ay - (half * 2f - chamfer)) * 0.70710678f;

        return Mathf.Max(box, diag);
    }

    // Renders a 9-sliced FlatUI sprite's border at an exact on-screen thickness, whatever the
    // sprite was baked at. Needed wherever a plate is small — a HUD bar is ~26px tall, and the
    // sprite's native 8px border would eat two thirds of it.
    public static void ApplySliceThickness(UnityEngine.UI.Image img, float pixels)
    {
        if (img == null || img.sprite == null || pixels <= 0f) return;
        float border = img.sprite.border.x;      // uniform on every shape here
        img.pixelsPerUnitMultiplier = border > 0f ? Mathf.Max(0.01f, border / pixels) : 1f;
    }

    private static Texture2D NewTex(int w, int h = -1)
    {
        if (h < 0) h = w;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;   // Point would alias the chamfer edges
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // ---- palette ------------------------------------------------------------------------------
    // Warm charcoal iron, not slate-blue. Act 1 is the Oxidation District — everything here should
    // sit on the rust side of neutral.

    public static readonly Color Backdrop = new Color(0.020f, 0.017f, 0.015f, 0.92f);
    public static readonly Color Surface = new Color(0.086f, 0.076f, 0.068f, 0.99f);
    public static readonly Color SurfaceRaised = new Color(0.133f, 0.117f, 0.104f, 1f);
    public static readonly Color Border = new Color(0.278f, 0.243f, 0.204f, 1f);
    public static readonly Color BorderSoft = new Color(0.239f, 0.208f, 0.176f, 1f);
    // The lit top lip of the plate — brighter than the border, applied to the top edge only.
    public static readonly Color EdgeLight = new Color(0.420f, 0.369f, 0.310f, 1f);
    // Forge fire under the bench, washing up from the bottom edge.
    public static readonly Color Ember = new Color(0.85f, 0.42f, 0.16f, 1f);

    public static readonly Color TextBright = new Color(0.945f, 0.925f, 0.886f, 1f);
    public static readonly Color TextBody = new Color(0.800f, 0.769f, 0.722f, 1f);
    public static readonly Color TextMuted = new Color(0.549f, 0.510f, 0.455f, 1f);
    public static readonly Color TextDisabled = new Color(0.361f, 0.329f, 0.290f, 1f);

    // Charges are Shift-blue on purpose: with scrap costs in rust-orange, the only two colours on
    // the screen are the game's own two resources.
    public static readonly Color Charges = new Color(0.478f, 0.706f, 0.929f, 1f);

    // ---- themes -------------------------------------------------------------------------------
    // Screens share the IDEOLOGY (flat procedural plates, restraint, directional light, a subtle
    // particle drift, one meaningful accent) but must NOT share a skin. Each place gets its own
    // material, and the material should say what the place DOES:
    //
    //   IRON   — the Scrap Forge. A workbench. Warm charcoal, fire from BELOW, rivets, scuffs,
    //            embers rising. Industrial, used, hot.
    //   ARCANE — Blompo. A mythic creature granting a blessing. Cold indigo, light from ABOVE
    //            (the blessing descending), four-point stars instead of rivets, motes settling
    //            downward, and no wear at all — his space isn't a workshop that gets used, so
    //            leaving it pristine is itself the contrast.
    //
    // The inversions are deliberate: warm/cold, lit from below/above, rising/falling, worn/clean.
    // When adding a screen, pick a material and invert something — don't just retint Iron.
    public struct Theme
    {
        public Color Backdrop, Surface, SurfaceRaised, Border, BorderSoft, EdgeLight, Accent;
        public Color TextBright, TextBody, TextMuted, TextDisabled;
    }

    public static readonly Theme Iron = new Theme
    {
        Backdrop = Backdrop,
        Surface = Surface,
        SurfaceRaised = SurfaceRaised,
        Border = Border,
        BorderSoft = BorderSoft,
        EdgeLight = EdgeLight,
        Accent = Ember,
        TextBright = TextBright,
        TextBody = TextBody,
        TextMuted = TextMuted,
        TextDisabled = TextDisabled,
    };

    public static readonly Theme Arcane = new Theme
    {
        Backdrop = new Color(0.016f, 0.014f, 0.026f, 0.92f),
        Surface = new Color(0.069f, 0.064f, 0.098f, 0.99f),
        SurfaceRaised = new Color(0.110f, 0.102f, 0.149f, 1f),
        Border = new Color(0.263f, 0.239f, 0.361f, 1f),
        BorderSoft = new Color(0.216f, 0.196f, 0.298f, 1f),
        EdgeLight = new Color(0.451f, 0.412f, 0.596f, 1f),
        Accent = new Color(0.686f, 0.522f, 0.965f, 1f),   // arcane violet
        TextBright = new Color(0.929f, 0.925f, 0.961f, 1f),
        TextBody = new Color(0.769f, 0.761f, 0.831f, 1f),
        TextMuted = new Color(0.522f, 0.510f, 0.612f, 1f),
        TextDisabled = new Color(0.337f, 0.325f, 0.412f, 1f),
    };

    // The DISPLAY font procedural UI should use — titles, headings, buttons, labels, numbers.
    //
    // ⚠️ This used to BE the font decision, made by census: it counted every `TMP_Text` in the scene
    // and returned the most common one. That was already an improvement on the older
    // `FindAnyObjectByType<TMP_Text>().font` (which returned an arbitrary object, so the same screen
    // came back in different typefaces between runs) — but it is still not a decision. It costs a
    // full scene scan per call, it can answer differently in MainMenu than in SampleScene, and any
    // screen that forgot to call it silently fell out of the system.
    //
    // The faces are now STATED in `UIType`. This stays as the display-face accessor because 18
    // screens already call it, and it returns exactly what the census was resolving to
    // (CCBattleScarred) — so the switch is a visual no-op.
    //
    // **For running sentences use `UIType.Prose()`, not this.** See `UIType` for the split and the
    // migration policy.
    public static TMPro.TMP_FontAsset UIFont()
    {
        return UIType.Display();
    }

    // LOADOUT — the relic bar and its tooltip.
    //
    // The other two themes dress a PLACE, where the material is the subject. This one dresses
    // what you're CARRYING, and it lives over gameplay permanently rather than taking the screen.
    // So its defining choice is the inverse: the chrome recedes to near-colourless, because the
    // relic icons are the subject and they're the only colour that should be in the row. It's also
    // the quietest theme by weight — a permanent HUD element must not compete with the game behind
    // it the way a modal panel can afford to.
    public static readonly Theme Loadout = new Theme
    {
        Backdrop = new Color(0.02f, 0.019f, 0.018f, 0.86f),
        Surface = new Color(0.078f, 0.075f, 0.071f, 0.94f),
        SurfaceRaised = new Color(0.110f, 0.106f, 0.100f, 0.96f),
        Border = new Color(0.239f, 0.231f, 0.216f, 1f),
        BorderSoft = new Color(0.180f, 0.173f, 0.161f, 1f),
        EdgeLight = new Color(0.361f, 0.349f, 0.325f, 1f),
        Accent = new Color(0.847f, 0.804f, 0.706f, 1f),   // pale bone, not a hue
        TextBright = new Color(0.937f, 0.929f, 0.910f, 1f),
        TextBody = new Color(0.788f, 0.776f, 0.749f, 1f),
        TextMuted = new Color(0.529f, 0.518f, 0.494f, 1f),
        TextDisabled = new Color(0.341f, 0.333f, 0.318f, 1f),
    };

    // VERDIGRIS — the run map.
    //
    // The other themes dress a PLACE and render it with light on material. A map is not a place:
    // you are not standing in it, you are READING it. So this one inverts the lighting model
    // itself — it is flat, matte and unlit, with no glow source anywhere. Motion lives in the
    // INFORMATION (branches you can take pulse, the route you've walked shimmers) rather than in
    // the air, because a chart has no air. That is why it carries no particle field while every
    // other screen does.
    //
    // The material is oxidised copper, which is Act 1's own chemistry — the Oxidation District is
    // rust and corrosion, and verdigris is what copper does there. It also pays off the accent:
    // the route you have ALREADY TRAVELLED is drawn in warm bare copper, as though the patina were
    // worn back to metal by walking it. Every other theme's accent is light being ADDED (forge
    // fire, arcane glow); this one's is surface being WORN AWAY.
    public static readonly Theme Verdigris = new Theme
    {
        Backdrop = new Color(0.012f, 0.020f, 0.019f, 0.93f),
        Surface = new Color(0.055f, 0.082f, 0.078f, 0.99f),
        SurfaceRaised = new Color(0.082f, 0.115f, 0.108f, 1f),
        Border = new Color(0.180f, 0.255f, 0.235f, 1f),
        BorderSoft = new Color(0.130f, 0.190f, 0.176f, 1f),
        EdgeLight = new Color(0.290f, 0.400f, 0.360f, 1f),
        Accent = new Color(0.855f, 0.545f, 0.290f, 1f),   // bare copper, worn through the patina
        TextBright = new Color(0.878f, 0.925f, 0.905f, 1f),
        TextBody = new Color(0.706f, 0.780f, 0.755f, 1f),
        TextMuted = new Color(0.478f, 0.545f, 0.522f, 1f),
        TextDisabled = new Color(0.310f, 0.365f, 0.349f, 1f),
    };

    // HALT — the pause screen.
    //
    // Every other theme dresses a PLACE (a workbench, a grove, a stall) or a THING (your loadout,
    // the chart). This one dresses a MOMENT: the one the player just stopped. In a game whose whole
    // thesis is "Movement is a Resource", pause is the total absence of movement, and that is what
    // the material has to say.
    //
    // The inversions, against everything already here:
    //   LIGHT      comes from the EDGES INWARD — frost creeping in from the borders of the screen.
    //              Iron is lit from below, Arcane from above, Verdigris not at all. This is a
    //              fourth direction, and an enclosing one: the picture is being closed in on.
    //   PARTICLES  are SUSPENDED. Not rising, not settling, not absent — hanging dead still, each
    //              still dragging the motion streak it had when the clock stopped. They only
    //              shiver, sub-pixel, straining against it. That single detail says "time stopped"
    //              faster than any amount of text.
    //   SURFACE    is CRAZED — a few hairline fractures across the frame. Iron is worn by use and
    //              Arcane is pristine; this stopped hard enough to crack.
    //
    // The accent is Shift-blue EXACTLY (FlatUI.Charges). Shift is the movement resource, so lighting
    // the screen where movement has stopped with the colour of movement itself is the point, and
    // there are no charge counts on this screen for it to collide with.
    public static readonly Theme Halt = new Theme
    {
        Backdrop = new Color(0.010f, 0.014f, 0.022f, 0.94f),
        Surface = new Color(0.043f, 0.055f, 0.075f, 0.99f),
        SurfaceRaised = new Color(0.070f, 0.086f, 0.114f, 1f),
        Border = new Color(0.180f, 0.220f, 0.280f, 1f),
        BorderSoft = new Color(0.130f, 0.163f, 0.212f, 1f),
        EdgeLight = new Color(0.560f, 0.680f, 0.800f, 1f),   // pale frost rim — here it IS the light
        Accent = Charges,                                    // Shift-blue, deliberately
        TextBright = new Color(0.902f, 0.933f, 0.965f, 1f),
        TextBody = new Color(0.729f, 0.784f, 0.843f, 1f),
        TextMuted = new Color(0.451f, 0.510f, 0.580f, 1f),
        TextDisabled = new Color(0.290f, 0.337f, 0.396f, 1f),
    };

    // APPARATUS — the settings screen.
    //
    // Every other theme dresses something INSIDE the fiction: a workbench, a grove, a stall, a
    // chart, the moment you stopped. Settings is the one screen that reaches back OUT of the game
    // and changes how it feels to the person holding the mouse. It is the machine's own control
    // panel, so it should not pretend to be a room in the Oxidation District — it should look like
    // an instrument.
    //
    // The inversions:
    //   LIGHT      is EMITTED BY THE CONTENT. Iron is lit from below, Arcane from above, Halt from
    //              the edges, Verdigris not at all — here the linework and the values glow, and the
    //              plate around them is unlit smoked glass. The information is the light source.
    //   MOTION     is a single scan SWEEP travelling down the plate. Not rising, settling,
    //              suspended, or absent: a measuring pass.
    //   MARKS      are calibration crosshairs (FlatUI.CalibrationMark) rather than rivets or stars.
    //              Fasteners hold a workbench together; measuring marks are what a control panel is
    //              covered in.
    //
    // Cyan-teal, at high saturation on a NEUTRAL dark surface, is the last clearly unclaimed hue —
    // and it is deliberately clinical, because being slightly outside the world's palette is itself
    // the signal that this screen is not part of the world. It is kept well clear of Halt's frost
    // blue on all three channels (hue ~175 vs ~210, far higher saturation, and a neutral rather
    // than blue-tinted surface), for the same reason the rarity colours had to separate on more
    // than hue.
    public static readonly Theme Apparatus = new Theme
    {
        Backdrop = new Color(0.008f, 0.012f, 0.013f, 0.94f),
        Surface = new Color(0.043f, 0.058f, 0.060f, 0.99f),
        SurfaceRaised = new Color(0.071f, 0.092f, 0.094f, 1f),
        Border = new Color(0.145f, 0.235f, 0.235f, 1f),
        BorderSoft = new Color(0.102f, 0.169f, 0.169f, 1f),
        EdgeLight = new Color(0.290f, 0.470f, 0.463f, 1f),
        Accent = new Color(0.204f, 0.898f, 0.831f, 1f),   // arc-cyan, the readout's own light
        TextBright = new Color(0.878f, 0.945f, 0.941f, 1f),
        TextBody = new Color(0.690f, 0.784f, 0.780f, 1f),
        TextMuted = new Color(0.420f, 0.514f, 0.510f, 1f),
        TextDisabled = new Color(0.267f, 0.337f, 0.333f, 1f),
    };

    // INSTRUMENT — the settings screen, rebuilt 2026-08-20.
    //
    // ⚠️ IT REPLACES *APPARATUS*, WHICH THE DESIGNER REJECTED: "a modern type of cool UI, in a game
    // that happens in a dungeon, where you fight zombies and orcs and slimes, using shift and cards.
    // does not read well." That was right, and the fault was not the craft — Apparatus was smoked
    // glass and arc-cyan, lit by its own content, with a travelling scan sweep and an iOS-style
    // toggle pill. Every one of those signals SCI-FI INTERFACE. It was the only screen in the game
    // made of a material that exists nowhere in the world.
    //
    // The standing note in the UI skill already diagnosed why this screen in particular drifted:
    // Halt, Apparatus and Marquee are **the three screens that depict no place in the game**, so
    // each had to invent its material from nothing — and inventing is exactly where "competent but
    // generic" creeps back in.
    //
    // ⚠️ AND A MATERIAL ALONE IS NOT ENOUGH — the map proved that twice. So this is not "Apparatus
    // in brass". Settings genuinely IS calibration, which was Apparatus's one good idea and is kept;
    // what changes is that the calibrating thing is now a REAL OBJECT WITH A HISTORY: a brass
    // surveying instrument that has been carried down into a dungeon. Tarnished, thumbed at the
    // controls, dust settled in the engraving, lit from BEHIND through its own lens glass.
    //
    // ⚠️ THE VERDIGRIS HERE IS NOT THE VERDIGRIS THAT WAS REJECTED. The run map was built once as a
    // verdigris-and-copper etched plate and thrown out — but that failed because a MAP must read as
    // a document, not because aged brass is wrong. Here it is a crevice accent on an instrument,
    // which is where tarnish actually collects, and brass is the dominant field rather than patina.
    //
    // INVERSIONS, against everything already claimed:
    //   LIGHT      FROM BEHIND, through lens glass. Iron lights from below, Arcane from above, Halt
    //              from the edges inward, Bulletin rakes from the left, Apparatus was lit by its own
    //              content. Backlit is the one direction nobody had.
    //   MOTION     NEEDLES SETTLE. No particle field and no sweep — the only movement is an index
    //              coming to rest after you move it, which is what an instrument does and what a
    //              scanline never did.
    //   CONTROLS   THROWN, NOT SLID. The toggle is a lever standing proud of a slotted plate, not a
    //              pill with a knob in it. That single widget was doing more to make this screen
    //              read as an app than the palette was.
    //   HUE        warm brass. The shop also owns warm/amber, so this is pushed GREEN-gold (aged
    //              brass) against the shop's orange lamplight, and the shop is wood-and-canvas with
    //              a person in it while this is metal and glass.
    public static readonly Theme Instrument = new Theme
    {
        Backdrop = new Color(0.020f, 0.017f, 0.012f, 0.94f),
        Surface = new Color(0.086f, 0.072f, 0.048f, 0.99f),   // tarnished brass plate
        SurfaceRaised = new Color(0.128f, 0.107f, 0.070f, 1f),
        Border = new Color(0.310f, 0.253f, 0.140f, 1f),
        BorderSoft = new Color(0.196f, 0.163f, 0.098f, 1f),
        EdgeLight = new Color(0.620f, 0.520f, 0.310f, 1f),    // polished where a thumb rests
        Accent = new Color(0.949f, 0.769f, 0.365f, 1f),       // lamplight through the lens
        TextBright = new Color(0.960f, 0.925f, 0.845f, 1f),
        TextBody = new Color(0.792f, 0.741f, 0.620f, 1f),
        TextMuted = new Color(0.510f, 0.463f, 0.365f, 1f),
        TextDisabled = new Color(0.310f, 0.282f, 0.220f, 1f),
    };

    // The green that collects in an instrument's engraving. Used ONLY in crevices — see Instrument.
    // ⚠️ Named Patina, not Verdigris: `FlatUI.Verdigris` is already taken by the run map's REJECTED
    // etched-copper THEME, which still sits in this file. Two different kinds of thing sharing a
    // name is how someone ends up assigning a whole Theme where a Color was meant.
    public static readonly Color Patina = new Color(0.298f, 0.443f, 0.365f, 1f);

    // BULLETIN — the quest board.
    //
    // ⚠️ THIS THEME'S VALUE STRUCTURE IS INVERTED, AND THAT IS THE POINT. Every other screen here is
    // a dark plate with light text on it. This one is a dark board with PALE PAPER pinned to it, and
    // its text ramp is therefore INK — dark, reading on the paper rather than on the surface. It is
    // the only theme where TextBright is nearly black. A label that has to sit on the BOARD itself
    // (a heading, a footer hint) uses EdgeLight instead.
    //
    // That single inversion does more than any hue could: at a glance the board has a completely
    // different silhouette from the forge, the map or the pause screen, because the bright areas and
    // the dark areas have swapped places. It is also the honest material — every other screen is
    // fabricated (iron, glass, patina, copper); the board is the one place the game hands you PAPER.
    //
    // The rest of the inversions, against everything already here:
    //   LIGHT      RAKES IN FROM THE LEFT. Iron is lit from below, Arcane from above, Halt from the
    //              edges inward, Apparatus from its own content, Verdigris not at all. Sideways is
    //              the unclaimed direction, and it is what a board on a wall beside a lamp looks
    //              like. Every slip's shadow therefore falls to the RIGHT — see PinTack, which is
    //              shaded to match, and get this backwards and the screen reads as wrong before
    //              anyone can say why.
    //   MOTION     lives in the CONTENT, not the air. There is no particle field at all: the slips
    //              themselves sway a fraction of a degree on their pins, as if there were a draught
    //              in the room. Verdigris also has no particles, but its motion is in the
    //              INFORMATION (routes pulsing); this one's is physical, objects moving.
    //   SURFACE    is PERFORATED — hundreds of old pin holes from contracts taken long before yours.
    //              Iron is scuffed by use and Halt is crazed by force; this one is worn by OTHER
    //              PEOPLE, which is the only wear on any screen that tells you something about the
    //              world rather than about the object.
    //   MARKS      are brass tacks, and unlike every other theme's corner marks they are on the
    //              CONTENT (one per slip) rather than on the frame — because a fastener here is
    //              holding something up, not holding a panel together.
    //
    // The accent is deep sealing-wax red, taken from the last of the unclaimed hue budget. It is
    // reserved almost entirely for the wax seal on an accepted contract, so red on this screen means
    // exactly one thing: you have promised to do that.
    public static readonly Theme Bulletin = new Theme
    {
        Backdrop = new Color(0.014f, 0.012f, 0.010f, 0.94f),
        Surface = new Color(0.072f, 0.060f, 0.049f, 0.99f),      // the board: dark oiled wood
        SurfaceRaised = new Color(0.780f, 0.741f, 0.659f, 1f),   // AGED PAPER — the inversion
        Border = new Color(0.196f, 0.161f, 0.129f, 1f),
        BorderSoft = new Color(0.145f, 0.118f, 0.094f, 1f),
        EdgeLight = new Color(0.510f, 0.435f, 0.345f, 1f),       // raking lamplight; also board text
        Accent = new Color(0.694f, 0.157f, 0.145f, 1f),          // sealing wax
        TextBright = new Color(0.129f, 0.106f, 0.086f, 1f),      // ink
        TextBody = new Color(0.220f, 0.184f, 0.149f, 1f),
        TextMuted = new Color(0.404f, 0.353f, 0.294f, 1f),
        TextDisabled = new Color(0.565f, 0.518f, 0.451f, 1f),
    };

    // Rarity colours tuned to read on a DARK surface. The old chrome carried rarity on a gem set
    // in gold; without that frame the colour has to stand on its own, so these are brighter and
    // more separated than jewel tones would be.
    // ⚠️ RARITY MUST SEPARATE ON MORE THAN HUE (reworked 2026-08-09). The previous set was amber /
    // violet / azure / cool-slate, and the designer could not tell the tiers apart at a glance. The
    // reason: three of the four sat in the blue-violet quadrant, and all four had near-identical
    // LUMINANCE, so the only cue was a hue step of ~40° — which is invisible on a small sigil over a
    // dark panel, and gone entirely for a red-green colour-blind player.
    //
    // This set separates on THREE channels at once, so any one of them is enough to read it:
    //   HUE        neutral -> green -> violet -> amber   (spread right around the wheel, not
    //              clustered; green is the biggest possible jump away from violet and amber)
    //   LUMINANCE  0.42 -> 0.56 -> 0.66 -> 0.82, strictly ascending, so the tiers are still ordered
    //              in greyscale and a better blessing is literally brighter
    //   SATURATION near-zero for Common, climbing with rarity, so Common reads as "no colour at all"
    //
    // Common stays the DIMMEST — it was already established that a bright Common makes the weakest
    // offer the loudest thing on screen.
    public static Color RarityColor(Rarity r)
    {
        switch (r)
        {
            // Boss sits OUTSIDE the ascending ladder on purpose — it is a different acquisition
            // channel, not a higher Legendary, so it takes the one hue nothing else uses.
            case Rarity.Boss: return new Color(1.000f, 0.290f, 0.560f, 1f);        // crimson-magenta
            case Rarity.Legendary: return new Color(1.000f, 0.780f, 0.290f, 1f);   // amber, brightest
            case Rarity.Epic: return new Color(0.760f, 0.420f, 1.000f, 1f);        // violet, pushed off blue
            case Rarity.Rare: return new Color(0.290f, 0.850f, 0.520f, 1f);        // green — far from both
            default: return new Color(0.470f, 0.490f, 0.520f, 1f);                 // neutral grey, dim
        }
    }

    // A DIFFERENT GLYPH PER RARITY, so the tier is readable without relying on colour at all.
    //
    // Shape is the strongest at-a-glance signal there is: the eye counts points long before it
    // judges a hue, and unlike colour it survives greyscale, colour-blindness, and a 40px icon.
    // The progression is deliberately "more elaborate = rarer", which needs no legend to read:
    //
    //   Common     a bare ring — plainly nothing special
    //   Rare       ring + four axial rays (a compass mark)
    //   Epic       ring + six rays + a second inner ring
    //   Legendary  double ring + eight rays of two lengths + outer ticks (the full ArcaneSigil)
    //
    // Legendary deliberately reuses the existing ArcaneSigil so the most ornate mark is the one
    // already established as "the arcane emblem", and the lesser tiers read as reduced versions of
    // it rather than as unrelated symbols.
    public static Sprite RaritySigil(Rarity r)
    {
        // ⚠️ Boss shares Legendary's sigil AND must be caught before the array index below — the
        // cache is sized to the tiers that generate a sigil, and Boss's enum value sits past it.
        if (r == Rarity.Legendary || r == Rarity.Boss) return ArcaneSigil();

        int idx = (int)r;
        if (raritySigils == null) raritySigils = new Sprite[4];
        if (raritySigils[idx] != null) return raritySigils[idx];

        const int S = 192;
        Texture2D tex = NewTex(S);
        float c = (S - 1) * 0.5f;

        // rays: 0 = none, 4 = cardinals, 6 = six-fold
        int rays = r == Rarity.Epic ? 6 : (r == Rarity.Rare ? 4 : 0);
        bool innerRing = r == Rarity.Epic;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { tex.SetPixel(x, y, Clear); continue; }
                float ang = Mathf.Atan2(dy, dx);

                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.70f) / 0.030f);
                float inner = innerRing ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.40f) / 0.026f) : 0f;

                float ray = 0f;
                if (rays > 0)
                {
                    // cos(n/2 * ang) gives n lobes; the high power sharpens them into rays.
                    float lobe = Mathf.Abs(Mathf.Cos(rays * 0.5f * ang));
                    ray = Mathf.Pow(lobe, 30f) * Falloff(d, 0.66f);
                }

                float core = Mathf.Pow(Mathf.Clamp01(1f - d * 8f), 2f);

                float a = Mathf.Clamp01(ring * 0.9f + inner * 0.75f + ray + core * 0.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        raritySigils[idx] = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return raritySigils[idx];
    }
}
