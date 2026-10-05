using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **Deckshift → Bake Salvage Art** — lifts the real colours out of the Cainos pack PNGs into
/// <see cref="SalvageArt"/>, so UI surfaces can be generated in a palette nobody invented.
///
/// ⚠️ IT READS THE PNG BYTES DIRECTLY rather than reading the imported Texture2D. Pack textures are
/// imported non-readable, and the two ways round that are both bad: flipping `isReadable` on a
/// shared pack asset is a project-settings change that would quietly ride along in a commit, and
/// re-importing a 2048 atlas to read five rectangles is slow. `File.ReadAllBytes` + `LoadImage`
/// touches nothing and is exact.
///
/// ⚠️ RECTS COME FROM THE IMPORTED SUB-SPRITES, NOT FROM HARDCODED NUMBERS. Sprite rects live in the
/// .meta and the packs get re-imported (the Customizable Character pack ships as a whole project and
/// has overwritten things before). Looking them up by NAME means a re-slice moves the sample with
/// the art instead of silently sampling whatever now sits at those coordinates.
///
/// ⚠️ THE RAMP IS PERCENTILE-TRIMMED. Raw min/max is contamination: a sprite rect touches its
/// neighbours on the atlas and its own anti-aliased rim, so the darkest pixels in `Cloth 08` are
/// actually the brown of an adjacent peg (measured: p2 = #563B25 against a p50 of #97918A). Trimming
/// to p8..p92 throws the bleed away and keeps the material.
/// </summary>
public static class SalvageArtBaker
{
    private const string AssetPath = "Assets/Resources/SalvageArt.asset";
    private const int Steps = 16;          // ramp resolution; 16 is plenty for mottling
    private const float TrimLow = 0.08f;
    private const float TrimHigh = 0.92f;

    // ⚠️ EVERY CAINOS SPRITE IS DRAWN WITH A 1PX DARK OUTLINE, AND IT IS A DIFFERENT MATERIAL FROM
    // THE THING IT OUTLINES. Sampling a sprite rect therefore poisons the DARK END of the ramp with
    // outline colour — measured on Cloth 08, which is grey linen (#97918A) inside a solid brown
    // (#563B25) border on all four sides. The first bake produced a ramp whose bottom third was that
    // brown, and every shadowed part of the pause screen's sheet came out as brown blotches.
    //
    // 2px rather than 1: the corners are stepped, so the outline is two pixels thick diagonally.
    private const int RectInset = 2;

    private const string Village = "Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";
    private const string Props = "Assets/Cainos/Pixel Art Platformer - Dungeon/Texture/TX Dungeon Props.png";
    private const string Wall = "Assets/Cainos/Pixel Art Platformer - Dungeon/Texture/TX Tileable - Dungeon Wall.png";
    private const string WallDeco = "Assets/Cainos/Pixel Art Platformer - Dungeon/Texture/TX Dungeon Wall Deco.png";
    private const string WallDirt = "Assets/Cainos/Pixel Art Platformer - Dungeon/Texture/TX Dungeon Wall Dirt.png";
    private const string VillageProps = "Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";

    [MenuItem("Deckshift/Bake Salvage Art")]
    public static void Bake()
    {
        SalvageArt art = AssetDatabase.LoadAssetAtPath<SalvageArt>(AssetPath);
        bool fresh = art == null;
        if (fresh) art = ScriptableObject.CreateInstance<SalvageArt>();

        var log = new List<string>();

        art.linen = FromSprites("linen", Village, log, "TX Village Props - Cloth 08");
        art.rope = FromSprites("rope", Village, log, "TX Village Props - Clother Hanger Rope 01",
                                                      "TX Village Props - Clother Hanger Rope 02");
        art.wood = FromSprites("wood", Props, log, "TX Dungeon Props - Beam 01",
                                                   "TX Dungeon Props - Shelf 01 A",
                                                   "TX Dungeon Props - Crate 01");
        art.iron = FromSprites("iron", Props, log, "TX Dungeon Props - Beam Metal 01 A",
                                                   "TX Dungeon Props - Beam Metal 01 B",
                                                   "TX Dungeon Props - Door Frame Iron Side 01");
        art.stone = WholeTexture("stone", Wall, log);

        // The wall goes in whole, as a Texture2D reference — it is the one piece of real pack art a
        // Salvage screen uses directly rather than sampling, because it is genuinely tileable.
        art.wall = AssetDatabase.LoadAssetAtPath<Texture2D>(Wall);
        log.Add("  wall   " + (art.wall != null ? art.wall.width + "x" + art.wall.height : "MISSING"));

        art.wallDeco = AllSprites(WallDeco, log, "wallDeco");
        art.wallDirt = AllSprites(WallDirt, log, "wallDirt");

        var allProps = new List<Sprite>();
        allProps.AddRange(AllSprites(Props, log, "props(dungeon)"));
        allProps.AddRange(AllSprites(VillageProps, log, "props(village)"));
        art.props = allProps.ToArray();

        if (fresh)
        {
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.CreateAsset(art, AssetPath);
        }
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SalvageArt] baked to " + AssetPath + "\n" + string.Join("\n", log));
        Selection.activeObject = art;
    }

    // ⚠️ SORTED BY NAME, ALWAYS. AssetDatabase returns sub-assets in an order that is not guaranteed
    // stable across reimports, and a screen that indexes into this array would silently redecorate
    // itself — a different crack in a different corner — every time somebody touched the pack.
    private static Sprite[] AllSprites(string sheet, System.Collections.Generic.List<string> log, string label)
    {
        var found = new List<Sprite>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(sheet))
        {
            Sprite s = o as Sprite;
            if (s != null) found.Add(s);
        }
        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        log.Add("  " + label + "  " + found.Count + " sprites   (" + Path.GetFileName(sheet) + ")");
        return found.ToArray();
    }

    // ---- sampling --------------------------------------------------------------------------------

    private static SalvageArt.Ramp FromSprites(string id, string sheet, List<string> log, params string[] spriteNames)
    {
        Texture2D tex = LoadRaw(sheet);
        if (tex == null) { log.Add("  " + id + ": MISSING SHEET " + sheet); return null; }

        var rects = new List<Rect>();
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(sheet))
        {
            Sprite s = o as Sprite;
            if (s == null) continue;
            foreach (string want in spriteNames)
                if (s.name == want)
                {
                    Rect r = s.rect;
                    // Peel the outline off. A sprite too small to survive the inset is sampled whole
                    // rather than dropped — a contaminated ramp beats no ramp at all.
                    if (r.width > RectInset * 2 + 2 && r.height > RectInset * 2 + 2)
                        r = new Rect(r.x + RectInset, r.y + RectInset,
                                     r.width - RectInset * 2, r.height - RectInset * 2);
                    rects.Add(r);
                }
        }

        if (rects.Count == 0)
        {
            log.Add("  " + id + ": no sprite matched (" + string.Join(", ", spriteNames) + ")");
            Object.DestroyImmediate(tex);
            return null;
        }

        var lums = new List<Color>();
        foreach (Rect r in rects) Collect(tex, r, lums);
        Object.DestroyImmediate(tex);

        return Build(id, Path.GetFileName(sheet) + " :: " + string.Join(" + ", spriteNames), lums, log);
    }

    private static SalvageArt.Ramp WholeTexture(string id, string path, List<string> log)
    {
        Texture2D tex = LoadRaw(path);
        if (tex == null) { log.Add("  " + id + ": MISSING " + path); return null; }

        var lums = new List<Color>();
        Collect(tex, new Rect(0, 0, tex.width, tex.height), lums);
        Object.DestroyImmediate(tex);
        return Build(id, Path.GetFileName(path), lums, log);
    }

    // Unity's Texture2D and Sprite.rect share a bottom-left origin, so the rect indexes directly —
    // no vertical flip. (System.Drawing is top-left; do not copy sampling code between the two.)
    private static void Collect(Texture2D tex, Rect r, List<Color> into)
    {
        int x0 = Mathf.Clamp(Mathf.RoundToInt(r.x), 0, tex.width - 1);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(r.y), 0, tex.height - 1);
        int x1 = Mathf.Clamp(Mathf.RoundToInt(r.x + r.width), 0, tex.width);
        int y1 = Mathf.Clamp(Mathf.RoundToInt(r.y + r.height), 0, tex.height);

        // Step through big sources (the wall is 2048 wide) rather than reading every pixel.
        int step = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt((x1 - x0) * (float)(y1 - y0) / 20000f)));

        for (int y = y0; y < y1; y += step)
            for (int x = x0; x < x1; x += step)
            {
                Color c = tex.GetPixel(x, y);
                if (c.a < 0.78f) continue;
                c.a = 1f;
                into.Add(c);
            }
    }

    private static SalvageArt.Ramp Build(string id, string source, List<Color> samples, List<string> log)
    {
        if (samples.Count < Steps)
        {
            log.Add("  " + id + ": only " + samples.Count + " opaque samples — SKIPPED");
            return null;
        }

        samples.Sort((a, b) => Lum(a).CompareTo(Lum(b)));

        int lo = Mathf.FloorToInt(samples.Count * TrimLow);
        int hi = Mathf.FloorToInt(samples.Count * TrimHigh);
        int span = Mathf.Max(1, hi - lo - 1);

        var ramp = new SalvageArt.Ramp { id = id, source = source, steps = new Color[Steps] };
        for (int i = 0; i < Steps; i++)
            ramp.steps[i] = samples[lo + Mathf.RoundToInt(span * (i / (float)(Steps - 1)))];

        log.Add(string.Format("  {0,-6} {1,6} px   {2}  ->  {3}   ({4})",
            id, samples.Count, Hex(ramp.steps[0]), Hex(ramp.steps[Steps - 1]), source));
        return ramp;
    }

    private static float Lum(Color c) { return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b; }

    private static string Hex(Color c)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    private static Texture2D LoadRaw(string projectPath)
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), projectPath);
        if (!File.Exists(full)) return null;
        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!t.LoadImage(File.ReadAllBytes(full))) { Object.DestroyImmediate(t); return null; }
        return t;
    }
}
