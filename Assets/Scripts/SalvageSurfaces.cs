using UnityEngine;

/// <summary>
/// The things Salvage screens are physically made of. Every surface here is GENERATED — the form is
/// authored, the colour is only ever sampled (see <see cref="Salvage.Ramp"/>), and everything comes
/// out at world magnification because it goes through <see cref="Salvage.MakeSprite"/>.
///
/// ⚠️ WHY GENERATE RATHER THAN STRETCH A PACK SPRITE. The pack's cloth pieces are 15x21 to 27x42 —
/// laundry on a line, not curtains. A 27x42 sprite blown up to a 1500px sheet is a 55x enlargement,
/// and pixel art past roughly 2x its native size stops reading as the thing it depicts (a 38x27
/// grime sprite at 420x420 came out as abstract shapes floating in mid-air). So the pack supplies the
/// PALETTE and the SHADING CONVENTION — dark base, one shadow, one bright highlight, exactly how
/// Cainos paints every cloth and rope in the packs — and the shape is built at the size it is needed.
/// </summary>
public static class SalvageSurfaces
{
    // ---- cloth -----------------------------------------------------------------------------------

    /// <summary>
    /// A sheet of canvas hung from pegs: gathered and creased at each pin, bowing and catching the
    /// light between them, relaxing as it falls, with a torn hem at the bottom.
    /// </summary>
    /// <param name="w">width in TEXTURE pixels (canvas px / Salvage.Scale)</param>
    /// <param name="h">height in TEXTURE pixels</param>
    /// <param name="pegs">where it is pinned, as 0..1 fractions across the top</param>
    public static Sprite Sheet(int w, int h, float[] pegs, int seed = 7)
    {
        string key = "sheet_" + w + "x" + h + "_" + pegs.Length + "_" + seed;
        Sprite cached;
        if (Salvage.TryCached(key, out cached) && cached != null) return cached;

        SalvageArt.Ramp linen = Salvage.Ramp("linen");
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];

        // Hem depth: how far up the tearing reaches. ~7 texture px is one good bite out of the weave
        // at this scale; much more and it stops reading as a hem and starts reading as damage.
        const int HemMax = 7;

        for (int y = 0; y < h; y++)
        {
            // v: 0 at the TOP (the rope), 1 at the hem. Texture y is bottom-up.
            float v = 1f - y / (float)(h - 1);

            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);

                // ---- fold field ------------------------------------------------------------------
                // Pinned cloth creases AT each peg and bulges toward the viewer between them, so the
                // pegs are shadow and the middle of each span is highlight. Folds are tight at the
                // top where the load is and relax as the sheet falls.
                float fold = SpanFold(u, pegs);
                float relax = Mathf.Lerp(1f, 0.34f, v * v);
                fold *= relax;

                // A few wandering creases that ignore the pegs, so it does not read as corrugation.
                fold += (Salvage.Grain(x * 0.6f, y * 0.12f, seed + 11, 0.09f, 2) - 0.5f) * 0.55f * relax;

                // ---- key light -------------------------------------------------------------------
                // Law 2: warm, upper-left, on every screen.
                float key2 = (-(u - 0.5f) * Salvage.LightDir.x + (0.5f - v) * Salvage.LightDir.y);
                float lightTerm = 0.5f + key2 * 0.42f;

                // The hem hangs away from the light and goes dim; the top catches the rope's light.
                float drop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, v));

                // ---- weave -----------------------------------------------------------------------
                float weave = Salvage.Grain(x, y, seed, 0.62f, 3);

                // ⚠️ THE RAMP CARRIES THE MATERIAL; A MULTIPLIER CARRIES THE FORM. Do not try to
                // shade by walking the ramp — measured, linen spans only #8A8179..#A29B91, which is
                // about 22 luminance levels out of 255. The first version drove folds, key light and
                // drape all through Sample() and the sheet came out perfectly flat, like poured
                // concrete, because a full swing across the ramp is almost no change in value.
                // Sample() now decides only WHICH linen this is; `shade` decides how lit it is.
                Color c = linen.Sample(0.35f + weave * 0.65f);

                // ⚠️ CLOTH IS MATTE, AND SMOOTH GRADIENTS ARE WHAT MAKE IT LOOK LIKE SHEET METAL.
                // The first version carried the form almost entirely in wide soft gradients, and the
                // sheet came out reading as brushed steel. Two fixes, and the second matters more:
                // less weight on the smooth key light, and a fine crumple broken into the shade
                // itself so the surface never resolves into a clean gradient anywhere.
                float crumple = (Salvage.Grain(x * 1.7f, y * 1.7f, seed + 71, 0.42f, 2) - 0.5f);

                // Hard creases. Real hung cloth has a few sharp lines in it, not only soft swells —
                // they are what the eye reads as "fabric" rather than "surface".
                float creaseNoise = Salvage.Grain(x * 1.15f, y * 0.09f, seed + 83, 0.30f, 1);
                float crease = Mathf.SmoothStep(0.80f, 0.92f, creaseNoise) * relax;

                float shade = 1f
                            + (lightTerm - 0.5f) * 0.44f      // key light, upper-left
                            + fold * 0.30f                     // creases at the pins, bulges between
                            + crumple * 0.17f                  // the matte break-up
                            - crease * 0.22f                   // sharp folds, in shadow
                            - drop * 0.34f;                    // the hem hangs out of the light

                // ---- wear (Law 5) ----------------------------------------------------------------
                // Used and repaired, not pristine and not derelict. Damp stains where it has been on
                // the floor, and one patch stitched over a hole — the world's repair currency is
                // literally called scrap, so a mended sheet is the honest object.
                float stain = Salvage.Grain(x * 0.5f, y * 0.5f, seed + 101, 0.055f, 2);
                shade -= Mathf.SmoothStep(0.44f, 0.20f, stain) * 0.17f;

                if (InPatch(u, v, x, y))
                {
                    shade *= 0.82f;                            // a darker, older scrap of cloth
                    c = linen.Sample(0.05f + weave * 0.30f);   // and from the dirty end of the ramp
                }
                if (OnPatchStitch(u, v, x, y)) shade *= 1.55f; // thread catching the light

                shade = Mathf.Clamp(shade, 0.42f, 1.34f);
                c = Salvage.Lit(c, shade);

                // ---- torn hem --------------------------------------------------------------------
                float a = 1f;
                int fromBottom = y;
                if (fromBottom < HemMax)
                {
                    // A per-column tear depth from low-frequency noise, plus the odd loose thread
                    // hanging below the line. Threads are what stop a noisy edge reading as dither.
                    float tear = Salvage.Grain(x * 1.0f, 0f, seed + 31, 0.16f, 2);
                    float depth = tear * HemMax;
                    bool thread = Salvage.Grain(x * 1f, 90f, seed + 47, 0.9f, 1) > 0.90f;
                    if (thread) depth *= 0.25f;
                    if (fromBottom < depth) a = 0f;
                }

                // ---- scalloped top ---------------------------------------------------------------
                // Cloth droops between its pins; a dead-straight top edge is the tell that a "sheet"
                // is really a rectangle. Only a couple of pixels, but it is the silhouette.
                int fromTop = (h - 1) - y;
                float droop = TopDroop(u, pegs) * 5f;
                if (fromTop < droop) a = 0f;

                px[y * w + x] = new Color(c.r, c.g, c.b, a);
            }
        }

        tex.SetPixels(px);
        return Salvage.MakeSprite(tex, key);
    }

    // ---- the mend --------------------------------------------------------------------------------
    //
    // ⚠️ ITS POSITION IS NOT DECORATIVE — IT IS THE ONE REGION THE LAYOUT LEAVES EMPTY AT EVERY
    // CONTENT LENGTH. Wear placed anywhere else immediately reads as UI: a stain behind a column of
    // numbers looks like a rendering fault, and a line between two elements reads as a divider rule
    // nobody asked for. The menu bottoms out around v 0.67 and the stat column around v 0.70, so the
    // bottom-left corner is dead space no matter how many rows either column grows to.
    private const float PatchU0 = 0.105f, PatchU1 = 0.245f;
    private const float PatchV0 = 0.760f, PatchV1 = 0.915f;

    // ⚠️ THE EDGE IS CUT BY HAND, NOT BY A RECT. The first version was a perfect rectangle whose
    // interior was only 8% brighter than the sheet, so the only thing visible was its dashed border —
    // and a dashed rectangle enclosing nothing is marching ants. It read as a UI selection box
    // somebody had left on the screen. Two fixes: the boundary wobbles, and the patch is a visibly
    // DIFFERENT piece of cloth rather than the same cloth very slightly lighter.
    private static bool InPatch(float u, float v, int x, int y)
    {
        float wobbleU = (Salvage.Grain(y * 1.3f, 0f, 211, 0.22f, 2) - 0.5f) * 0.016f;
        float wobbleV = (Salvage.Grain(x * 1.3f, 40f, 223, 0.22f, 2) - 0.5f) * 0.020f;
        return u >= PatchU0 + wobbleU && u <= PatchU1 + wobbleU
            && v >= PatchV0 + wobbleV && v <= PatchV1 + wobbleV;
    }

    /// <summary>Bright thread crossing the seam — short irregular stitches, not a drawn border.</summary>
    private static bool OnPatchStitch(float u, float v, int x, int y)
    {
        if (!InPatch(u, v, x, y)) return false;

        float du = Mathf.Min(u - PatchU0, PatchU1 - u) / (PatchU1 - PatchU0);
        float dv = Mathf.Min(v - PatchV0, PatchV1 - v) / (PatchV1 - PatchV0);
        bool nearEdge = du < 0.055f || dv < 0.070f;
        if (!nearEdge) return false;

        // Irregular spacing. A fixed modulus gives perfectly even dashes, which is the machine-made
        // look the wobble above is trying to get away from.
        float jitter = Salvage.Grain(x * 2.1f, y * 2.1f, 233, 0.75f, 1);
        return ((x + y + Mathf.RoundToInt(jitter * 3f)) % 6) < 2;
    }

    // -1 at a peg (crease, in shadow), +1 midway between two pegs (bulge, in light).
    private static float SpanFold(float u, float[] pegs)
    {
        if (pegs == null || pegs.Length < 2) return 0f;

        for (int i = 0; i < pegs.Length - 1; i++)
        {
            float a = pegs[i], b = pegs[i + 1];
            if (u < a || u > b) continue;
            float k = Mathf.InverseLerp(a, b, u);
            return -Mathf.Cos(k * Mathf.PI * 2f);
        }
        // Outside the pinned span the cloth just falls away from the last peg.
        float edge = u < pegs[0] ? (pegs[0] - u) : (u - pegs[pegs.Length - 1]);
        return -1f + Mathf.Clamp01(edge * 6f) * 0.5f;
    }

    // How far the top edge sags below the rope at u, 0..1.
    private static float TopDroop(float u, float[] pegs)
    {
        if (pegs == null || pegs.Length < 2) return 0f;
        for (int i = 0; i < pegs.Length - 1; i++)
        {
            float a = pegs[i], b = pegs[i + 1];
            if (u < a || u > b) continue;
            float k = Mathf.InverseLerp(a, b, u);
            return Mathf.Sin(k * Mathf.PI);      // 0 at each pin, 1 midway
        }
        return 1f;
    }

    // ---- plank board -----------------------------------------------------------------------------

    /// <summary>
    /// A board of horizontal planks bound with two iron straps — the surface a Salvage screen's
    /// content is written on when it needs to be RIGID.
    ///
    /// ⚠️ WHY THIS REPLACED THE CLOTH SHEET. Cloth was chosen because pause is stillness and a sheet
    /// hangs in front of the world rather than replacing it. The metaphor was right and the SURFACE
    /// was wrong: the designer's verdict on it was "its not bad, but i want something better", and
    /// the reason is legible in the screenshots — a canvas sheet is one flat value with soft folds,
    /// so it has no structure to look at and no edge to make it feel built. Planks have seams, grain,
    /// straps and bolts; wood and iron are the dungeon's core material pair; and text on wood reads
    /// far better than text on cloth. The drop-from-above motion is kept exactly — that is the part
    /// that was working.
    /// </summary>
    public static Sprite PlankBoard(int w, int h, int planks = 7, int seed = 5)
    {
        string key = "board_" + w + "x" + h + "_" + planks + "_" + seed;
        Sprite cached;
        if (Salvage.TryCached(key, out cached) && cached != null) return cached;

        SalvageArt.Ramp wood = Salvage.Ramp("wood");
        SalvageArt.Ramp iron = Salvage.Ramp("iron");

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];

        float plankH = h / (float)planks;

        // The straps. Kept off the content columns: the menu occupies roughly u 0.12..0.62 and the
        // stat block u 0.62..0.92, so a strap in the middle would run straight through the numbers.
        const float StrapA = 0.055f, StrapB = 0.945f;
        float strapHalf = Salvage.Tex(26f) * 0.5f / w;

        for (int y = 0; y < h; y++)
        {
            float v = 1f - y / (float)(h - 1);           // 0 at top

            int plank = Mathf.Clamp((int)(y / plankH), 0, planks - 1);
            float inPlank = (y - plank * plankH) / plankH;      // 0 at plank bottom, 1 at its top

            // Each board is a different piece of timber, and that variation is most of what stops
            // this reading as a flat brown rectangle.
            float plankTone = Salvage.Grain(plank * 13.7f, 0f, seed + 5, 0.9f, 1);

            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);

                // Grain runs ALONG the plank, so the noise is stretched hard in x.
                float grain = Salvage.Grain(x * 0.30f, y * 2.6f, seed, 0.55f, 3);

                float shade = 0.94f
                            + (grain - 0.5f) * 0.30f
                            + (plankTone - 0.5f) * 0.26f;

                // Key light, upper-left (Law 2).
                shade += (-(u - 0.5f) * Salvage.LightDir.x + (0.5f - v) * Salvage.LightDir.y) * 0.30f;

                // Seams. The shadow sits UNDER the plank above it, so each plank is dark along its
                // top edge and catches a thin highlight along its bottom — that asymmetry is what
                // makes them read as overlapping boards instead of as drawn stripes.
                float seamPx = 2.2f / plankH;
                if (inPlank > 1f - seamPx) shade *= 0.42f;
                else if (inPlank < seamPx * 0.9f) shade *= 1.16f;

                Color c = wood.Sample(0.30f + grain * 0.55f);
                bool isIron = false;

                // ---- iron straps ---------------------------------------------------------------
                float dStrap = Mathf.Min(Mathf.Abs(u - StrapA), Mathf.Abs(u - StrapB));
                if (dStrap < strapHalf)
                {
                    isIron = true;
                    float across = dStrap / strapHalf;                 // 0 centre, 1 edge
                    c = iron.Sample(0.30f + (1f - across) * 0.45f);
                    shade = 1f + (0.5f - across) * 0.34f;
                    if (across > 0.86f) shade *= 0.45f;                // the strap's own dark edge

                    // Bolts, one per plank, alternating side to side so the row is not a column.
                    float bolt = Mathf.Abs(inPlank - 0.5f);
                    if (bolt < 0.17f && across < 0.55f)
                    {
                        float bx = across / 0.55f, by = bolt / 0.17f;
                        float d = Mathf.Sqrt(bx * bx + by * by);
                        if (d < 1f) { c = iron.Sample(d < 0.55f ? 0.95f : 0.10f); shade = d < 0.55f ? 1.25f : 0.55f; }
                    }
                }

                // ---- board edge ------------------------------------------------------------------
                int ex = Mathf.Min(x, w - 1 - x);
                int ey = Mathf.Min(y, h - 1 - y);
                if (ex < 2 || ey < 2)
                {
                    // Lit on the top and left, dark on the bottom and right: one rule, and it is what
                    // gives a flat rectangle thickness.
                    bool litEdge = (x < 2) || (y > h - 3);
                    c = isIron ? iron.Sample(litEdge ? 0.9f : 0.05f) : wood.Sample(litEdge ? 0.85f : 0.05f);
                    shade = litEdge ? 1.15f : 0.50f;
                }

                px[y * w + x] = Salvage.Lit(c, Mathf.Clamp(shade, 0.35f, 1.45f));
            }
        }

        tex.SetPixels(px);
        return Salvage.MakeSprite(tex, key);
    }

    /// <summary>
    /// A slab of dressed stone with a chiselled bevel — the surface for screens that are not
    /// furniture. Same five laws, different material.
    ///
    /// ⚠️ THIS IS HOW SALVAGE AVOIDS MONOTONY WITHOUT SPENDING A COLOUR. The designer's objection to
    /// a single substrate was that everything would look the same, and they were right: eight wooden
    /// boards is a set of eight wooden boards. Stone is the dungeon's other structural material, it
    /// is already in the sampled palette, and it reads as instantly different from planks at a
    /// glance — no hue involved. Use wood for things people BUILT and stone for things that were
    /// CUT: an altar, a plinth, a threshold.
    /// </summary>
    public static Sprite StoneSlab(int w, int h, int seed = 9)
    {
        string key = "slab_" + w + "x" + h + "_" + seed;
        Sprite cached;
        if (Salvage.TryCached(key, out cached) && cached != null) return cached;

        SalvageArt.Ramp stone = Salvage.Ramp("stone");
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];

        const int Bevel = 5;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1), v = 1f - y / (float)(h - 1);

                // Coarse mottle plus a fine tooth — dressed stone is pitted, not smooth.
                float coarse = Salvage.Grain(x * 0.22f, y * 0.22f, seed, 0.30f, 3);
                float tooth = Salvage.Grain(x * 1.6f, y * 1.6f, seed + 17, 0.85f, 1);

                float shade = 0.95f
                            + (coarse - 0.5f) * 0.34f
                            + (tooth - 0.5f) * 0.13f
                            + (-(u - 0.5f) * Salvage.LightDir.x + (0.5f - v) * Salvage.LightDir.y) * 0.34f;

                Color c = stone.Sample(0.25f + coarse * 0.62f);

                // The chiselled edge: lit on the top and left, in shadow on the bottom and right.
                // One rule, and it is the whole reason a flat rectangle reads as a cut block.
                int ex = Mathf.Min(x, w - 1 - x);
                int ey = Mathf.Min(y, h - 1 - y);
                int edge = Mathf.Min(ex, ey);
                if (edge < Bevel)
                {
                    bool lit = (x < Bevel) || (y > h - 1 - Bevel);
                    float k = 1f - edge / (float)Bevel;
                    shade *= lit ? (1f + 0.42f * k) : (1f - 0.46f * k);
                }

                px[y * w + x] = Salvage.Lit(c, Mathf.Clamp(shade, 0.38f, 1.40f));
            }

        tex.SetPixels(px);
        return Salvage.MakeSprite(tex, key);
    }

    /// <summary>
    /// A hanging chain, 5 texture pixels wide, links repeating every 8. Alternating upright and
    /// crosswise links — a chain drawn as a plain dashed line reads as a zip, and the alternation is
    /// the only thing that sells it at this size.
    /// </summary>
    public static Sprite Chain(int lengthTexPx, int seed = 2)
    {
        string key = "chain_" + lengthTexPx + "_" + seed;
        Sprite cached;
        if (Salvage.TryCached(key, out cached) && cached != null) return cached;

        SalvageArt.Ramp iron = Salvage.Ramp("iron");
        const int W = 5, Cycle = 8;

        var tex = new Texture2D(W, lengthTexPx, TextureFormat.RGBA32, false);
        var px = new Color[W * lengthTexPx];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);

        Color lit = Salvage.Lit(iron.Sample(0.92f), 1.1f);
        Color body = Salvage.Lit(iron.Sample(0.50f));
        Color dark = Salvage.Lit(iron.Sample(0.06f));

        for (int y = 0; y < lengthTexPx; y++)
        {
            int phase = y % Cycle;
            if (phase < 5)
            {
                // Upright link: two sides, seen edge-on.
                px[y * W + 1] = phase == 0 || phase == 4 ? dark : lit;   // Law 2: left side is lit
                px[y * W + 3] = phase == 0 || phase == 4 ? dark : body;
            }
            else
            {
                // Crosswise link: a bar across.
                for (int x = 0; x < W; x++)
                    px[y * W + x] = x == 0 || x == W - 1 ? dark : (phase == 5 ? lit : body);
            }
        }

        tex.SetPixels(px);
        return Salvage.MakeSprite(tex, key);
    }

    // ---- rope ------------------------------------------------------------------------------------

    /// <summary>
    /// A rope strung across a span and sagging under a load. Three pixels thick, painted the way the
    /// pack paints rope: dark underside, mid body, one bright strand catching the light on top.
    /// </summary>
    public static Sprite RopeSpan(int w, int sag, int seed = 3)
    {
        string key = "rope_" + w + "_" + sag + "_" + seed;
        Sprite cached;
        if (Salvage.TryCached(key, out cached) && cached != null) return cached;

        SalvageArt.Ramp rope = Salvage.Ramp("rope");
        const int Thick = 3;
        int h = sag + Thick + 2;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);

        Color dark = Salvage.Lit(rope.Sample(0.05f));
        Color body = Salvage.Lit(rope.Sample(0.45f));
        Color lit = Salvage.Lit(rope.Sample(0.97f));

        for (int x = 0; x < w; x++)
        {
            float u = x / (float)(w - 1);
            // Catenary, near enough: a parabola is indistinguishable at this sag and costs nothing.
            float dip = 4f * u * (1f - u);
            int top = h - 2 - Mathf.RoundToInt(dip * sag);

            // The twist. Without it a rope is a tube; the pack's ropes all show a fibre cadence.
            bool twist = ((x + Mathf.RoundToInt(Salvage.Grain(x, 0f, seed, 0.5f, 1) * 2f)) % 4) < 2;

            for (int k = 0; k < Thick; k++)
            {
                int y = top - k;
                if (y < 0 || y >= h) continue;
                Color c = k == 0 ? (twist ? lit : body)       // top strand catches the light
                        : k == 1 ? body
                                 : dark;                      // underside
                px[y * w + x] = c;
            }
        }

        tex.SetPixels(px);
        return Salvage.MakeSprite(tex, key);
    }

    // ---- peg -------------------------------------------------------------------------------------

    /// <summary>
    /// A wooden clothes peg, 5x11 texture pixels — near enough the pack's own 3x9 Clother Hanger
    /// Clip, drawn a touch larger so it survives being the thing that explains the whole metaphor.
    /// </summary>
    public static Sprite Peg(int seed = 1)
    {
        string key = "peg_" + seed;
        Sprite cached;
        if (Salvage.TryCached(key, out cached) && cached != null) return cached;

        SalvageArt.Ramp wood = Salvage.Ramp("wood");
        const int W = 5, H = 11;

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color[W * H];

        Color dark = Salvage.Lit(wood.Sample(0.12f));
        Color body = Salvage.Lit(wood.Sample(0.55f));
        Color lit = Salvage.Lit(wood.Sample(0.95f));

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // The slot down the middle of the lower half is what makes it a PEG and not a stick.
                bool slot = x == 2 && y < H - 5;
                bool leftFace = x <= 1;                     // Law 2: the light is upper-left
                Color c = slot ? dark : (leftFace ? lit : body);
                if (y == 0 || y == H - 1) c = dark;         // ends read as cut wood
                px[y * W + x] = c;
            }

        tex.SetPixels(px);
        return Salvage.MakeSprite(tex, key);
    }

    // ---- wear ------------------------------------------------------------------------------------

    /// <summary>
    /// A patch rubbed brighter by being touched. ⚠️ BRIGHT, NOT DARK — the surfaces here are already
    /// dark, so a dark mark on them is invisible by definition, and what a hand actually does to a
    /// worn surface is polish it. This is the same inversion the pin-holes on the quest board needed.
    /// Returned as a soft radial falloff to be tinted and stretched by the caller.
    /// </summary>
    public static Sprite Rubbed()
    {
        Sprite cached;
        if (Salvage.TryCached("rubbed", out cached) && cached != null) return cached;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color[S * S];

        // ⚠️ SEPARABLE FALLOFF WITH A FLAT PLATEAU, NOT A RADIAL BLOB. The first version was radial,
        // and a radial falloff stretched to a 600x62 row does not become a band — it becomes a
        // horizontal streak with a hot core, which on screen read as a lens flare lying across the
        // menu. Any soft shape that will be stretched to a very different aspect has to fall off on
        // each axis independently.
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = Mathf.Abs(x - (S - 1) * 0.5f) / ((S - 1) * 0.5f);
                float dy = Mathf.Abs(y - (S - 1) * 0.5f) / ((S - 1) * 0.5f);
                px[y * S + x] = new Color(1f, 1f, 1f, Edge(dx, 0.55f) * Edge(dy, 0.30f));
            }

        tex.SetPixels(px);
        Sprite s = Salvage.MakeSprite(tex, "rubbed");
        s.texture.filterMode = FilterMode.Bilinear;         // a falloff is the ONE thing not on the grid
        return s;
    }

    /// <summary>
    /// A soft radial falloff, for LIGHT — a torch throwing warmth onto a surface, or a broad shadow
    /// where a surface falls away from it. ⚠️ Light is the one thing in Salvage that is not on the
    /// pixel grid: a dithered gradient reads as a rendering fault, not as a lamp.
    /// </summary>
    public static Sprite Bloom()
    {
        Sprite cached;
        if (Salvage.TryCached("bloom", out cached) && cached != null) return cached;

        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color[S * S];

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - (S - 1) * 0.5f) / ((S - 1) * 0.5f);
                float dy = (y - (S - 1) * 0.5f) / ((S - 1) * 0.5f);
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = 1f - d;
                a = a * a * a * (3f - 2f * a);          // steeper than smoothstep: a core, then falloff
                px[y * S + x] = new Color(1f, 1f, 1f, a);
            }

        tex.SetPixels(px);
        Sprite s = Salvage.MakeSprite(tex, "bloom");
        s.texture.filterMode = FilterMode.Bilinear;
        return s;
    }

    /// <summary>1 inside the plateau, smoothly 0 at the edge. d and plateau are both 0..1.</summary>
    private static float Edge(float d, float plateau)
    {
        if (d <= plateau) return 1f;
        float k = Mathf.Clamp01(1f - (d - plateau) / (1f - plateau));
        return k * k * (3f - 2f * k);
    }
}
