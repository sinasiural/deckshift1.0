using UnityEngine;
using UnityEngine.UI;

// Shared procedural UGUI sprites for Deckshift's crafted HUD chrome — the slot-relic UI (loadout
// bar, tooltip, Manage / Swap panels, shop) and the health / Shift resource bars. House style —
// generated in code, cached statically so every panel draws from one source.
//
// Everything here is built to match Deckshift's OWN hand-painted HUD art (Assets/Art/panel 1.png,
// the top-left stat panel): a dark MOTTLED-STONE interior inside an ornate GOLD border studded with
// GEM BOSSES at the corners. Rarity is carried by the gem colour, not by recolouring the frame — so
// every relic reads as the same crafted gold-on-stone object the rest of the HUD is made of.
public static class RelicUISprites
{
    private static Sprite panelSprite;   // solid rounded fill (plates / panels)
    private static Sprite frameSprite;   // rounded border ring (slot frames)
    private static Sprite glowSprite;    // soft radial glow
    private static Sprite whiteSprite;   // 1x1 white (bars / dividers)
    private static Sprite goldSprite;    // ornate gold beveled border
    private static Sprite stoneSprite;   // dark mottled-stone fill
    private static Sprite settingSprite; // gold diamond gem-setting (frame w/ hole)
    private static Sprite gemSprite;     // faceted gem cabochon (grayscale, tinted)
    private static Sprite barFrameSprite; // thin gold bevel frame for HUD bars
    private static Sprite barTrackSprite; // dark recessed channel behind a bar's fill
    private static Sprite barFillSprite;  // glossy vertical gradient (grayscale, tinted)
    private static Sprite shadowSprite;   // feathered rounded drop shadow

    // --- Deckshift chrome palette (sampled from Assets/Art/panel 1.png) ---
    static readonly Color GoldHi   = new Color(0.97f, 0.85f, 0.50f);
    static readonly Color GoldMid  = new Color(0.80f, 0.60f, 0.26f);
    static readonly Color GoldShad = new Color(0.47f, 0.33f, 0.14f);
    static readonly Color GoldDark = new Color(0.16f, 0.11f, 0.05f);
    static readonly Color StoneBase= new Color(0.205f, 0.155f, 0.115f);
    static readonly Color StoneLo  = new Color(0.115f, 0.088f, 0.066f);
    static readonly Color StoneHi  = new Color(0.275f, 0.215f, 0.160f);

    // The shared rarity palette — used for glow, tooltip name, panel accents.
    public static Color RarityColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Boss:      return new Color(1f, 0.29f, 0.56f);
            case Rarity.Legendary: return new Color(1f, 0.80f, 0.25f);
            case Rarity.Epic:      return new Color(0.72f, 0.38f, 1f);
            case Rarity.Rare:      return new Color(0.35f, 0.62f, 1f);
            default:               return new Color(0.75f, 0.78f, 0.85f);
        }
    }

    // Gem-boss colour by rarity — jewel tones so the corner stones read like real gems set in gold.
    public static Color GemColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Boss:      return new Color(1f, 0.22f, 0.50f);    // garnet — nothing else uses it
            case Rarity.Legendary: return new Color(1f, 0.62f, 0.14f);   // amber
            case Rarity.Epic:      return new Color(0.66f, 0.30f, 0.98f); // amethyst
            case Rarity.Rare:      return new Color(0.28f, 0.55f, 1f);    // sapphire
            default:               return new Color(0.86f, 0.24f, 0.26f); // ruby (like the HUD panel)
        }
    }

    // ---- ornate gold beveled border. Simple for the square medallion; also carries a 9-slice
    // border so wide panels (Manage / Swap / tooltip) can use it as a Sliced frame. ----
    public static Sprite GoldBorder()
    {
        if (goldSprite != null) return goldSprite;
        int s = 112; float radius = s * 0.20f, border = s * 0.135f, half = s / 2f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);
                float aO = Mathf.Clamp01(d / 1.25f), aI = Mathf.Clamp01((border - d) / 1.25f);
                float a = d < 0f ? 0f : Mathf.Min(aO, aI);
                if (a <= 0f) { px[y * s + x] = new Color32(0, 0, 0, 0); continue; }

                float t = Mathf.Clamp01(d / border);       // 0 outer .. 1 inner
                float ny = ((y + 0.5f) - half) / half;     // -1 bottom .. +1 top
                Color c;
                const float rim = 0.16f, chanA = 0.78f, chanB = 0.97f;
                if (t < rim) c = Color.Lerp(GoldDark, GoldShad, t / rim);        // dark outer rim
                else if (t > chanB) c = GoldDark;                                // dark inner edge
                else
                {
                    float body = (t - rim) / (chanB - rim);
                    float lit = 1f - body * 0.62f + 0.10f * ny;                  // bright outer + slight top-lit (mild so it 9-slices)
                    if (t > chanA && t < chanB)                                  // engraved inner groove
                    {
                        float g = 1f - Mathf.Abs(t - (chanA + chanB) * 0.5f) / ((chanB - chanA) * 0.5f);
                        lit -= 0.55f * g;
                    }
                    c = Color.Lerp(GoldShad, GoldHi, Mathf.Clamp01(lit));
                }
                float n = (Mathf.PerlinNoise(x * 0.19f, y * 0.19f) - 0.5f) * 0.10f;
                c = new Color(Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n));
                px[y * s + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        // 9-slice border (26px) keeps the rounded gold corners intact when used as a panel frame;
        // the transparent centre lets the stone fill show through. Ignored when drawn as Simple.
        goldSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0, SpriteMeshType.FullRect, new Vector4(26, 26, 26, 26));
        return goldSprite;
    }

    // ---- dark mottled-stone fill (baked; Simple for sockets, 9-slice for panels) ----
    public static Sprite StonePanel()
    {
        if (stoneSprite != null) return stoneSprite;
        int s = 96; float radius = s * 0.15f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);
                float a = Mathf.Clamp01(d / 1.25f);
                if (a <= 0f) { px[y * s + x] = new Color32(0, 0, 0, 0); continue; }

                float n1 = Mathf.PerlinNoise(x * 0.085f, y * 0.085f);
                float n2 = Mathf.PerlinNoise(x * 0.23f + 5f, y * 0.23f + 5f);
                float n = n1 * 0.66f + n2 * 0.34f;
                Color c = Color.Lerp(StoneLo, StoneHi, n);
                float crack = Mathf.PerlinNoise(x * 0.13f + 11f, y * 0.13f + 11f);  // cobble seams
                if (crack > 0.60f && crack < 0.645f) c *= 0.55f;
                float edge = Mathf.Clamp01(d / (s * 0.16f));                        // darken toward rim
                c *= 0.72f + 0.28f * edge;
                px[y * s + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        stoneSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return stoneSprite;
    }

    // ======================= HUD resource bars (health / Shift) =======================
    // These three are pinned to pixelsPerUnit 100 (the UGUI reference PPU) and carry small 9-slice
    // borders, so a caller can render an EXACT pixel thickness on a bar of any size via
    // ApplySliceThickness. GoldBorder above is the wrong tool here — its 26px border would swamp a
    // 24px-tall bar.

    private const float BAR_SLICE = 6f;  // 9-slice border of BarFrame / BarTrack, in sprite px

    // ---- thin gold bevel frame: dark outer line, top-lit gold bevel, dark inner line, hollow
    // centre so the track shows through. ----
    public static Sprite BarFrame()
    {
        if (barFrameSprite != null) return barFrameSprite;
        int s = 32; float half = s / 2f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                // Distance (px) to the nearest edge; the frame lives in the outer BAR_SLICE band.
                float d = Mathf.Min(Mathf.Min(x + 0.5f, s - x - 0.5f), Mathf.Min(y + 0.5f, s - y - 0.5f));
                if (d >= BAR_SLICE) { px[y * s + x] = new Color32(0, 0, 0, 0); continue; }

                Color c;
                if (d < 1f) c = GoldDark;                                   // crisp outer outline
                else if (d > BAR_SLICE - 1f) c = GoldDark;                  // inner lip into the recess
                else
                {
                    float t = (d - 1f) / (BAR_SLICE - 2f);                  // 0 outer .. 1 inner
                    float ny = ((y + 0.5f) - half) / half;                  // +1 top .. -1 bottom
                    float lit = 1f - t * 0.55f + 0.26f * ny;                // top-lit bevel
                    c = Color.Lerp(GoldShad, GoldHi, Mathf.Clamp01(lit));
                }
                px[y * s + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
            }
        tex.SetPixels32(px); tex.Apply();
        barFrameSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(BAR_SLICE, BAR_SLICE, BAR_SLICE, BAR_SLICE));
        return barFrameSprite;
    }

    // ---- dark recessed channel the fill sits in. Near-black stone with an inner shadow along the
    // top edge so the bar reads as carved INTO the panel rather than sitting on it. ----
    public static Sprite BarTrack()
    {
        if (barTrackSprite != null) return barTrackSprite;
        int s = 32;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        Color baseCol = new Color(0.072f, 0.062f, 0.088f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float fromTop = s - y - 0.5f;                               // 0 at top edge
                Color c = baseCol;
                c *= 1f - 0.55f * Mathf.Clamp01(1f - fromTop / 5f);         // inner shadow under the top lip
                float n = (Mathf.PerlinNoise(x * 0.31f, y * 0.31f) - 0.5f) * 0.035f;
                c = new Color(Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n));
                px[y * s + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
            }
        tex.SetPixels32(px); tex.Apply();
        barTrackSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(BAR_SLICE, BAR_SLICE, BAR_SLICE, BAR_SLICE));
        return barTrackSprite;
    }

    // ---- glossy fill: grayscale vertical gradient (tint it with the resource colour). Uniform
    // horizontally, so stretching it across a segment of any width stays clean. ----
    public static Sprite BarFill()
    {
        if (barFillSprite != null) return barFillSprite;
        int w = 4, h = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float ny = y / (h - 1f);                                        // 0 bottom .. 1 top
            float v = Mathf.Lerp(0.58f, 1f, ny);
            if (ny > 0.60f && ny < 0.84f) v += 0.20f;                       // gloss band
            if (ny > 0.94f) v *= 0.78f;                                     // top rim
            if (ny < 0.07f) v *= 0.66f;                                     // bottom rim
            byte b = (byte)(Mathf.Clamp01(v) * 255f);
            for (int x = 0; x < w; x++) px[y * w + x] = new Color32(b, b, b, 255);
        }
        tex.SetPixels32(px); tex.Apply();
        barFillSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return barFillSprite;
    }

    // ---- soft drop shadow: a rounded rect with a feathered edge, 9-sliced. The HUD bars sit
    // directly on the game world with no panel behind them, so they need their own separation from
    // whatever is rendering underneath. ----
    public static Sprite SoftShadow()
    {
        if (shadowSprite != null) return shadowSprite;
        int s = 48; float pad = 14f, radius = 7f, feather = 12f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                // signed distance to a rounded rect inset by `pad`; >0 inside
                float half = s / 2f;
                float ax = Mathf.Abs(x + 0.5f - half) - (half - pad - radius);
                float ay = Mathf.Abs(y + 0.5f - half) - (half - pad - radius);
                float outside = Mathf.Sqrt(Mathf.Max(ax, 0f) * Mathf.Max(ax, 0f) + Mathf.Max(ay, 0f) * Mathf.Max(ay, 0f));
                float d = radius - (outside + Mathf.Min(Mathf.Max(ax, ay), 0f));
                float a = Mathf.Clamp01(d / feather + 0.5f);
                a = a * a * (3f - 2f * a);                                  // smoothstep for a soft falloff
                px[y * s + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        shadowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(22, 22, 22, 22));
        return shadowSprite;
    }

    // Makes a 9-sliced BarFrame / BarTrack render its border at EXACTLY `thicknessPx` canvas pixels.
    // The rendered border is spriteBorder / (spritePPU / canvasReferencePPU) / pixelsPerUnitMultiplier,
    // so we solve for the multiplier instead of assuming the canvas uses the default reference PPU.
    public static void ApplySliceThickness(Image img, float thicknessPx)
    {
        if (img == null || img.sprite == null || thicknessPx <= 0f) return;
        // Graphic.canvas is null until the graphic registers, which hasn't happened yet if the HUD
        // was built while hidden — walk the hierarchy (including inactive parents) as a fallback.
        Canvas canvas = img.canvas != null ? img.canvas : img.GetComponentInParent<Canvas>(true);
        float refPPU = canvas != null ? canvas.referencePixelsPerUnit : 100f;
        float ppuRatio = img.sprite.pixelsPerUnit / Mathf.Max(1f, refPPU);
        img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, (BAR_SLICE / ppuRatio) / thicknessPx);
    }

    // ---- gold diamond gem-setting: a beveled gold frame with a hole the gem shows through ----
    public static Sprite GemSetting()
    {
        if (settingSprite != null) return settingSprite;
        int s = 44; float half = s / 2f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        float outer = half * 0.96f, inner = half * 0.46f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float dia = Mathf.Abs(dx) + Mathf.Abs(dy);                // diamond (L1) distance
                float aO = Mathf.Clamp01((outer - dia) / 1.2f);
                float aI = Mathf.Clamp01((dia - inner) / 1.2f);
                float a = Mathf.Min(aO, aI);
                if (a <= 0f) { px[y * s + x] = new Color32(0, 0, 0, 0); continue; }
                float t = Mathf.Clamp01((dia - inner) / (outer - inner));  // 0 inner .. 1 outer
                float ny = dy / half;                                     // top-lit
                float lit = 0.9f - Mathf.Abs(t - 0.45f) * 1.1f + 0.22f * ny;
                Color c = Color.Lerp(GoldShad, GoldHi, Mathf.Clamp01(lit));
                px[y * s + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        settingSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return settingSprite;
    }

    // ---- faceted gem cabochon (grayscale; tint with GemColor) ----
    public static Sprite Gem()
    {
        if (gemSprite != null) return gemSprite;
        int s = 40; float half = s / 2f, r = half * 0.62f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x + 0.5f - half, dy = y + 0.5f - half;
                float dia = Mathf.Abs(dx) + Mathf.Abs(dy);               // diamond gem outline
                float a = Mathf.Clamp01((r - dia) / 1.2f);
                if (a <= 0f) { px[y * s + x] = new Color32(0, 0, 0, 0); continue; }
                float v = 0.40f + 0.58f * (1f - dia / r);                // domed: bright centre
                // four table facets: a faint seam along the diagonals
                float seam = Mathf.Min(Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)), Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy)));
                if (seam < 1.1f) v *= 0.85f;
                // specular glint up-left
                float sx = dx + r * 0.34f, sy = dy - r * 0.34f;
                if (Mathf.Sqrt(sx * sx + sy * sy) < r * 0.22f) v = Mathf.Min(1.15f, v + 0.5f);
                if (dia > r * 0.85f) v *= 0.55f;                          // dark rim
                v = Mathf.Clamp01(v);
                byte b = (byte)(v * 255f);
                px[y * s + x] = new Color32(b, b, b, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        gemSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return gemSprite;
    }

    public static Sprite Panel()
    {
        if (panelSprite != null) return panelSprite;
        int s = 64; float radius = s * 0.22f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);
                float a = Mathf.Clamp01(d);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        panelSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return panelSprite;
    }

    public static Sprite Frame()
    {
        if (frameSprite != null) return frameSprite;
        int s = 64; float radius = s * 0.22f, border = s * 0.09f, feather = 1.5f;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedRectEdge(x, y, s, radius);
                float outer = Mathf.Clamp01(d / feather);
                float inner = Mathf.Clamp01((border - d) / feather);
                float a = d < 0f ? 0f : Mathf.Min(outer, inner);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        frameSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return frameSprite;
    }

    public static Sprite Glow()
    {
        if (glowSprite != null) return glowSprite;
        int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f, rad = c;
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / rad;
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return glowSprite;
    }

    public static Sprite White()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white); tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        return whiteSprite;
    }

    // Studs a window's border with gem bosses (gold setting + coloured gem) — corners always, plus
    // edge midpoints when edgeMids is true. Mirrors Assets/Art/panel 1.png. Adds children to
    // `window`; call after the gold frame so the studs sit on top of it.
    // topRight=false leaves the top-right corner open (e.g. for a close button that takes that stud's place).
    public static void AddGemStuds(RectTransform window, float w, float h, Color gem, float studSize = 46f, bool edgeMids = true, bool topRight = true)
    {
        float gx = w * 0.5f - studSize * 0.45f;
        float gy = h * 0.5f - studSize * 0.45f;
        var pts = new System.Collections.Generic.List<Vector2>
        {
            new Vector2(-gx, gy), new Vector2(-gx, -gy), new Vector2(gx, -gy)
        };
        if (topRight) pts.Add(new Vector2(gx, gy));
        if (edgeMids)
        {
            pts.Add(new Vector2(0f, gy)); pts.Add(new Vector2(0f, -gy));
            pts.Add(new Vector2(-gx, 0f)); pts.Add(new Vector2(gx, 0f));
        }
        foreach (var p in pts)
        {
            MakeStud(window, "Stud", GemSetting(), Color.white, studSize, p);
            MakeStud(window, "StudGem", Gem(), gem, studSize * 0.62f, p);
        }
    }

    private static void MakeStud(Transform parent, string name, Sprite sprite, Color color, float size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
    }

    // Signed distance (px) to the nearest edge of a rounded square; >0 inside.
    private static float RoundedRectEdge(int x, int y, int s, float radius)
    {
        float half = s / 2f;
        float px = x + 0.5f - half;
        float py = y + 0.5f - half;
        float ax = Mathf.Abs(px) - (half - radius);
        float ay = Mathf.Abs(py) - (half - radius);
        float outside = Mathf.Sqrt(Mathf.Max(ax, 0f) * Mathf.Max(ax, 0f) + Mathf.Max(ay, 0f) * Mathf.Max(ay, 0f));
        float inside = Mathf.Min(Mathf.Max(ax, ay), 0f);
        return radius - (outside + inside);
    }
}
