using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SALVAGE — the one material system for every screen in Deckshift.
///
/// ══ WHY THIS REPLACES THE NINE-THEME SYSTEM ══════════════════════════════════════════════════════
///
/// FlatUI's rule was "every screen gets its own material — screens share the ideology and NEVER the
/// same skin. Pick a material and invert something." Nine materials (Iron, Arcane, Loadout, Halt,
/// Apparatus, Bulletin, Cartograph, Marquee, Marketplace) and a documented "hue budget" that ran out.
/// It worked exactly as written, and what it was written to produce is screens that look unlike each
/// other — which is the opposite of what the game needs. Every settings screen built under that rule
/// was rejected, and the rule, not the execution, is why.
///
/// The fix is not "one substrate everywhere" — that was proposed and correctly rejected as
/// monotonous, and this project has the receipt: VIGIL was stone alcoves with real dungeon art and a
/// torch per alcove, and it was rejected TWICE.
///
/// Look instead at the Cainos dungeon pack itself. Crates, pots, bottles, banners, chains, skeletons,
/// candles, bookshelves, fireplaces — wildly different materials, and it reads as ONE world anyway.
/// Not because it is all stone. Because everything in it obeys the same handful of laws.
///
/// ⚠️ CONSISTENCY LIVES IN THE TREATMENT, NOT THE SUBSTRATE. That is the whole thesis. Screens may
/// be made of anything the dungeon contains; they may not disagree about these five things:
///
///   1. SCALE  — one magnification, and it is the world's own (see Scale).
///   2. LIGHT  — warm, from the UPPER LEFT, always. No screen relights itself.
///   3. COLOUR — sampled from the packs, never invented (see SalvageArt).
///   4. ACCENT — exactly two in the whole game: Torch (lit) and Shift (energised). No screen
///               spends a new hue, ever. The hue budget stops existing.
///   5. WEAR   — things here have been used and repaired. Not pristine, not derelict: the world's
///               repair currency is literally called scrap.
///
/// Variety then comes from WHAT THE OBJECT IS — a hung sheet, a notice board, a workbench, a
/// banner — which is a property of the screen's purpose rather than a colour someone picked.
/// </summary>
public static class Salvage
{
    // ══ LAW 1 — SCALE ════════════════════════════════════════════════════════════════════════════
    //
    // The world draws 32-pixels-per-unit art through a camera of orthographicSize 7 onto a canvas
    // that is ALWAYS 1080 tall (every CanvasScaler here matches on height). So:
    //
    //     14 world units  ->  1080 canvas px  ->  77.143 px/unit  ->  77.143 / 32 = 2.4107
    //
    // ⚠️ USE THIS NUMBER AND NOTHING ELSE. Pack art drawn at 2.41x is exactly the size the same art
    // is in the game — a peg in a menu is the size of a peg on a wall. It is deliberately NOT snapped
    // to an integer: the WORLD already displays its pixel art at this non-integer scale, so 2x or 3x
    // would make UI pixels visibly a different size from world pixels. Matching the world is the
    // consistency; a clean integer would break it.
    //
    // ⚠️ AND IT DOUBLES AS THE ENLARGEMENT LIMIT. Pixel art blown much past the size it was drawn to
    // be seen at stops reading as the thing it depicts — a 38x27 grime sprite at 420x420 became
    // abstract shapes floating in mid-air, and a 27x42 cloth stretched to a full-screen curtain would
    // do the same. 2.41x IS native here, so anything drawn at it is safe by construction.
    public const float Scale = 2.4107f;

    /// <summary>Texture pixels -> canvas pixels, at world magnification.</summary>
    public static float Px(float texturePixels) { return texturePixels * Scale; }

    /// <summary>Canvas pixels -> texture pixels, for sizing a generated surface.</summary>
    public static int Tex(float canvasPixels) { return Mathf.Max(1, Mathf.RoundToInt(canvasPixels / Scale)); }

    // ══ LAW 2 — LIGHT ════════════════════════════════════════════════════════════════════════════
    //
    // Warm, from the upper left, on every screen. A hung sheet, a plank and a notice board disagreeing
    // about where the light is, is the single fastest way to make a set of screens look assembled from
    // different games — and it is a mistake the old system made deliberately (Iron lit from BELOW,
    // Arcane from ABOVE, Halt from the EDGES INWARD, Bulletin from the LEFT).
    public static readonly Vector2 LightDir = new Vector2(-0.72f, 0.69f);

    // ⚠️ WORLD SPRITES COMPOSITE THROUGH A 0.5-INTENSITY GLOBAL Light2D. UI DOES NOT.
    // A pack colour pasted raw into a Screen Space Overlay canvas therefore renders at TWICE the
    // value the same material has in the game, and every surface built that way reads as if it is
    // glowing next to the world it is supposed to belong to. Multiply by this, then calibrate.
    public const float SceneLight = 0.5f;

    /// <summary>A pack colour as it actually appears in the lit world. <paramref name="key"/> &gt; 1 puts it in the light.</summary>
    public static Color Lit(Color packColor, float key = 1f)
    {
        float k = SceneLight * key;
        return new Color(packColor.r * k, packColor.g * k, packColor.b * k, packColor.a);
    }

    // ══ LAW 4 — THE ONLY TWO ACCENTS ═════════════════════════════════════════════════════════════
    //
    // TORCH  = lit, warm, present.  "this exists"
    // SHIFT  = energised, live.     "this is on"
    //
    // Both are already established in the fiction and neither is invented: torches light every room,
    // and the ShiftAltar fires a cyan orb whose exact colour seals the gate. The gate rebuild is the
    // proof this works — it landed because it spoke in the game's own mechanic instead of a new hue.
    //
    // ⚠️ NO SCREEN MAY INTRODUCE A THIRD. If something needs to stand out and neither of these is
    // right, the answer is a different VALUE, a different SHAPE or a chalk mark — not a new colour.
    public static readonly Color Torch = new Color(0.980f, 0.706f, 0.365f, 1f);
    public static readonly Color Shift = new Color(0.450f, 0.900f, 1.000f, 1f);   // == Gate.Seal, == the altar orb

    // Chalk. Someone wrote this here. ⚠️ IDENTICAL to ExitMarker's chalk, on purpose — the mark in
    // the menu is the same mark the world uses to point at the exit, which is a consistency win that
    // costs nothing. Do not "improve" one of the two without the other.
    public static readonly Color Chalk = new Color(0.93f, 0.90f, 0.82f, 1f);

    // Blood/danger. Not an accent — a warning, and the one place a third hue is permitted, because
    // "you will die if you do this" cannot be said in torch amber. Matches the Stagger card's red.
    public static readonly Color Wound = new Color(0.902f, 0.290f, 0.290f, 1f);

    // ══ TEXT ═════════════════════════════════════════════════════════════════════════════════════
    // Warm off-white, not white — everything in the packs is warm, and pure white belongs to nothing.
    public static readonly Color TextBright = new Color(0.937f, 0.914f, 0.867f, 1f);
    public static readonly Color TextBody = new Color(0.784f, 0.757f, 0.706f, 1f);
    public static readonly Color TextMuted = new Color(0.573f, 0.541f, 0.494f, 1f);
    public static readonly Color TextFaint = new Color(0.404f, 0.380f, 0.345f, 1f);
    public static readonly Color Ink = new Color(0.118f, 0.098f, 0.078f, 1f);   // for use ON pale cloth

    // ---- sprite construction ---------------------------------------------------------------------

    // ⚠️ THIS IS WHAT ENFORCES LAW 1, and it is the reason no screen has to remember it.
    // uGUI draws a sprite at  rect.size / pixelsPerUnit * canvas.referencePixelsPerUnit.  With
    // referencePixelsPerUnit 100 (the default, and what every canvas here uses), setting ppu to
    // 100/Scale makes native size and Image.Type.Tiled land at exactly world magnification for free.
    public static readonly float SpritePPU = 100f / Scale;

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite MakeSprite(Texture2D tex, string cacheKey = null)
    {
        tex.filterMode = FilterMode.Point;      // Law 1 is meaningless through a bilinear filter
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply(false, false);

        Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f), SpritePPU, 0, SpriteMeshType.FullRect);
        s.name = cacheKey ?? "Salvage";
        if (cacheKey != null) cache[cacheKey] = s;
        return s;
    }

    public static bool TryCached(string key, out Sprite s) { return cache.TryGetValue(key, out s); }

    /// <summary>
    /// The dungeon wall, as one tiled sprite at world magnification.
    ///
    /// ⚠️ THE SPRITE IS THE WHOLE 256x256 TEXTURE AND THE IMAGE MUST BE `Image.Type.Tiled`.
    /// Simple stretches one block across the screen; a sub-sprite tiles a fragment of a larger
    /// picture and reads as a checkerboard.
    ///
    /// ⚠️ AND IT IS BUILT AT SpritePPU, NOT AT 100. The character select builds the same wall at
    /// ppu 100, which draws each block at 256 canvas px — 2.4x SMALLER than the identical bricks in
    /// the game behind it. At SpritePPU a block is 617px and the masonry is exactly the size the
    /// player sees in a room, which is the entire point of Law 1.
    /// </summary>
    public static Sprite Wall()
    {
        Sprite s;
        if (TryCached("wall", out s) && s != null) return s;

        SalvageArt art = SalvageArt.Get();
        if (art == null || art.wall == null) return null;

        Texture2D t = art.wall;
        s = Sprite.Create(t, new Rect(0f, 0f, t.width, t.height),
                          new Vector2(0.5f, 0.5f), SpritePPU, 0, SpriteMeshType.FullRect);
        s.name = "wall";
        cache["wall"] = s;
        return s;
    }

    /// <summary>
    /// A piece of wall decoration by name — pass the bit after "TX Dungeon Wall Deco - ",
    /// e.g. "Break 01", "Dent 04", "Outfall 01".
    ///
    /// ⚠️ BY NAME, NOT BY INDEX. Indexing an array of sub-sprites means a pack reimport can quietly
    /// put a different crack in a different corner, and nothing would ever report it.
    /// </summary>
    public static Sprite Deco(string suffix)
    {
        SalvageArt art = SalvageArt.Get();
        if (art == null || art.wallDeco == null) return null;
        foreach (Sprite s in art.wallDeco)
            if (s != null && s.name.EndsWith(suffix)) return s;
        return null;
    }

    /// <summary>
    /// A prop from either pack, by the bit after "TX Dungeon Props - " / "TX Village Props - "
    /// (e.g. "Anvil", "Hammer", "Fireplace 01", "Barrel 01 A").
    ///
    /// ⚠️ THIS IS HOW EACH SCREEN BECOMES ITS OWN PLACE. Standing instruction from the designer:
    /// do NOT give every screen the same visuals. A screen dressed with the props its location would
    /// actually contain — an anvil and a lit hearth at the forge, notices on a board, goods on a
    /// stall — is a place; the same panel eight times is a template. All of it is already drawn.
    ///
    /// Returns null if nothing matches, so a caller must guard. Silent absence beats a magenta box
    /// on a shipped screen.
    /// </summary>
    public static Sprite Prop(string suffix)
    {
        SalvageArt art = SalvageArt.Get();
        if (art == null || art.props == null) return null;
        foreach (Sprite s in art.props)
            if (s != null && s.name.EndsWith(" - " + suffix)) return s;
        return null;
    }

    /// <summary>One of the grime patches. Index wraps, so a caller can never pick a missing one.</summary>
    public static Sprite Dirt(int index)
    {
        SalvageArt art = SalvageArt.Get();
        if (art == null || art.wallDirt == null || art.wallDirt.Length == 0) return null;
        return art.wallDirt[((index % art.wallDirt.Length) + art.wallDirt.Length) % art.wallDirt.Length];
    }

    /// <summary>1x1 white. The flat-fill workhorse.</summary>
    public static Sprite Pixel()
    {
        Sprite s;
        if (TryCached("pixel", out s) && s != null) return s;
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, Color.white);
        return MakeSprite(t, "pixel");
    }

    // ---- ramps -----------------------------------------------------------------------------------

    /// <summary>
    /// A material's sampled palette. Falls back to a hardcoded measurement of the same pack region
    /// if the baked asset is missing, so a screen can never render magenta because someone has not
    /// run the baker yet. ⚠️ The fallbacks ARE measurements (see the hex in each), not guesses.
    /// </summary>
    public static SalvageArt.Ramp Ramp(string id)
    {
        SalvageArt art = SalvageArt.Get();
        SalvageArt.Ramp r = art != null ? art.ById(id) : null;
        if (r != null && r.steps != null && r.steps.Length > 0) return r;
        return Fallback(id);
    }

    private static readonly Dictionary<string, SalvageArt.Ramp> fallbacks =
        new Dictionary<string, SalvageArt.Ramp>();

    private static SalvageArt.Ramp Fallback(string id)
    {
        SalvageArt.Ramp r;
        if (fallbacks.TryGetValue(id, out r)) return r;

        string[] hex;
        switch (id)
        {
            // TX Village Props - Cloth 08, measured p15/p50/p85/p98
            case "linen": hex = new[] { "6E6862", "8A8179", "97918A", "9D978F", "A29B91" }; break;
            // TX Village Props - Clother Hanger Rope 01, measured p2/p15/p50/p85/p98
            case "rope": hex = new[] { "503220", "593B27", "62452F", "907E67", "9C8B74" }; break;
            // TX Dungeon Props, dominant wood family
            case "wood": hex = new[] { "2E140C", "401D13", "431F14", "5F402A", "8A7358" }; break;
            // TX Tileable - Dungeon Wall, dominant #444548 with the cut-stone blues
            case "stone": hex = new[] { "35363A", "434446", "444548", "4A5054", "53595E" }; break;
            case "iron": hex = new[] { "24262A", "34373C", "424344", "53575C", "6E747A" }; break;
            default: hex = new[] { "444548" }; break;
        }

        var steps = new Color[hex.Length];
        for (int i = 0; i < hex.Length; i++)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + hex[i], out c);
            steps[i] = c;
        }
        r = new SalvageArt.Ramp { id = id, source = "fallback (measured)", steps = steps };
        fallbacks[id] = r;
        return r;
    }

    // ---- noise -----------------------------------------------------------------------------------

    private static float Hash(int x, int y, int seed)
    {
        int h = x * 374761393 + y * 668265263 + seed * 1274126177;
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
    }

    private static float ValueNoise(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3f - 2f * xf);      // smoothstep, so octaves don't show the lattice
        float v = yf * yf * (3f - 2f * yf);
        float a = Mathf.Lerp(Hash(xi, yi, seed), Hash(xi + 1, yi, seed), u);
        float b = Mathf.Lerp(Hash(xi, yi + 1, seed), Hash(xi + 1, yi + 1, seed), u);
        return Mathf.Lerp(a, b, v);
    }

    /// <summary>Multi-octave value noise in 0..1. The grain under every Salvage surface.</summary>
    public static float Grain(float x, float y, int seed, float frequency = 0.25f, int octaves = 3)
    {
        float sum = 0f, amp = 1f, norm = 0f, f = frequency;
        for (int o = 0; o < octaves; o++)
        {
            sum += ValueNoise(x * f, y * f, seed + o * 97) * amp;
            norm += amp;
            amp *= 0.5f;
            f *= 2.13f;                           // not exactly 2, or octaves align into visible bands
        }
        return sum / norm;
    }
}
