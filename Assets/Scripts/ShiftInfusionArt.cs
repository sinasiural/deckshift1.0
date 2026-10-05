using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The pixel art behind a Shift-infused enemy, shared by <see cref="ShiftInfused"/> (the living
/// look) and <see cref="InfusedDeathVFX"/> (the death). All of it is generated once, at the world's
/// 32 px per unit, and cached for the session; nothing here is per enemy.
///
/// ⚠️ THE SMALL SPRITES SHARE ONE ATLAS TEXTURE AND ONE MATERIAL. SpriteRenderers sharing a custom
/// material but carrying DIFFERENT textures can be merged into one draw with the first one's
/// texture (the BraceVFX bug in CLAUDE.md). One texture for all of them makes that impossible. The
/// rings are too big to atlas, so each ring size gets its own material.
///
/// Colours follow Salvage: Shift cyan is the accent, white-hot is the same cyan pushed to white, and
/// the dark side of a shard is the same cyan in shadow. Light comes from the upper left.
/// </summary>
public static class ShiftInfusionArt
{
    public const float PPU = 32f;

    /// <summary>Shift pushed toward white: the core of a band, a flash, a bolt.</summary>
    public static readonly Color Hot = Color.Lerp(Salvage.Shift, Color.white, 0.65f);
    private static readonly Color Deep = new Color(0.14f, 0.42f, 0.62f, 1f);

    public const int ArcCount = 8;
    public const int ShardFrames = 6;

    private static bool built;
    private static Material atlasMat;
    private static Sprite mote, dotBig, dotSmall;
    private static readonly Sprite[] shard = new Sprite[ShardFrames];
    private static readonly Sprite[] arcs = new Sprite[ArcCount];
    private static readonly Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, Material> ringMats = new Dictionary<int, Material>();
    private static Material silhouette;
    private static bool silhouetteLoaded;

    public static Material AtlasMaterial { get { Build(); return atlasMat; } }
    public static Sprite Mote { get { Build(); return mote; } }
    public static Sprite DotBig { get { Build(); return dotBig; } }
    public static Sprite DotSmall { get { Build(); return dotSmall; } }
    public static Sprite Shard(int frame) { Build(); return shard[((frame % ShardFrames) + ShardFrames) % ShardFrames]; }
    public static Sprite Arc(int i) { Build(); return arcs[((i % ArcCount) + ArcCount) % ArcCount]; }

    /// <summary>
    /// The flat-colour silhouette material (Deckshift/Shift Silhouette). Loaded from Resources so
    /// a build includes the shader; null only if both the asset and the shader are missing.
    /// </summary>
    public static Material Silhouette
    {
        get
        {
            if (silhouetteLoaded && !Stale(silhouette)) return silhouette;
            silhouetteLoaded = true;
            silhouette = Resources.Load<Material>("ShiftSilhouette");
            if (silhouette == null)
            {
                Shader s = Shader.Find("Deckshift/Shift Silhouette");
                if (s != null) silhouette = new Material(s) { name = "ShiftSilhouette (runtime)" };
                Debug.LogWarning("[ShiftInfusionArt] Resources/ShiftSilhouette.mat is missing; " +
                                 (silhouette != null ? "built one from the shader, which a BUILD may not include." : "the shader is missing too, so infused enemies have no outline."));
            }
            return silhouette;
        }
    }

    /// <summary>A pixel ring of the given radius in pixels, white (tint it), with its own material.</summary>
    public static Sprite Ring(int radiusPx, out Material mat)
    {
        radiusPx = Mathf.Clamp(radiusPx, 2, 96);
        Build();   // also clears a stale ring cache
        if (!rings.TryGetValue(radiusPx, out Sprite s) || Stale(s))
        {
            int size = radiusPx * 2 + 3;
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    // Two texels thick: the outer one full, the inner one half, so it reads as a
                    // wave front travelling outward rather than a drawn circle.
                    float a = d >= radiusPx - 0.5f && d < radiusPx + 0.5f ? 1f
                            : d >= radiusPx - 1.5f && d < radiusPx - 0.5f ? 0.45f : 0f;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
            s.name = "ShiftRing" + radiusPx;
            rings[radiusPx] = s;
            Shader unlit = Shader.Find("Sprites/Default");
            ringMats[radiusPx] = unlit != null ? new Material(unlit) { name = s.name, mainTexture = tex } : null;
        }
        mat = ringMats[radiusPx];
        return s;
    }

    private static Texture2D NewTex(int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
    }

    // ---------------------------------------------------------------------------------------------
    // The atlas: one 128x64 texture holding the mote, two dots, six shard frames and eight bolts.
    // ---------------------------------------------------------------------------------------------
    private const int AW = 128, AH = 64;
    private static Color[] atlas;

    private static void Put(int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= AW || y >= AH) return;
        atlas[y * AW + x] = c;
    }

    private static Color Get(int x, int y)
    {
        if (x < 0 || y < 0 || x >= AW || y >= AH) return Color.clear;
        return atlas[y * AW + x];
    }

    // ⚠️ THE CACHE CHECKS ITS OBJECTS ARE STILL ALIVE, not just a flag. Leaving Play mode destroys
    // everything created at runtime but keeps static fields when the domain is not reloaded (the
    // editor's "Enter Play Mode Options"), which would leave every infused enemy drawing with
    // destroyed sprites. `x is null` is a true C# null; `x == null` is also true for a destroyed one.
    private static bool Stale(Object o) => !(o is null) && o == null;

    private static void Build()
    {
        if (built && !Stale(mote)) return;
        built = true;
        rings.Clear();
        ringMats.Clear();
        atlas = new Color[AW * AH];
        var tex = NewTex(AW, AH);
        var rects = new List<KeyValuePair<string, RectInt>>();

        // Mote: a 3x3 plus of light (the same mote the infusion has always used).
        Color e = new Color(1f, 1f, 1f, 0.55f);
        Put(1, 0, e); Put(0, 1, e); Put(1, 1, Color.white); Put(2, 1, e); Put(1, 2, e);
        RectInt moteR = new RectInt(0, 0, 3, 3);

        // Dots for the shards' comet tails.
        Put(4, 0, Color.white); Put(5, 0, Color.white); Put(4, 1, Color.white); Put(5, 1, Color.white);
        RectInt dotBigR = new RectInt(4, 0, 2, 2);
        Put(7, 0, Color.white);
        RectInt dotSmallR = new RectInt(7, 0, 1, 1);

        // Shard frames: a crystal turning on its axis, 7 -> 5 -> 3 -> 1 -> 3 -> 5 texels wide (the
        // last two are the back face, so light and dark swap sides). 13 tall: half the size of the
        // Shift crystal pickup, the thing it turns into. At 9 tall it read as a glint, not a crystal.
        int[] widths = { 7, 5, 3, 1, 3, 5 };
        var shardR = new RectInt[ShardFrames];
        for (int f = 0; f < ShardFrames; f++)
        {
            int ox = 10 + f * 8, oy = 0;
            int w = widths[f];
            bool back = f >= 4;
            float half = (w - 1) * 0.5f;
            for (int row = 0; row < 13; row++)
            {
                int hw = Mathf.RoundToInt(half * (1f - Mathf.Abs(row - 6) / 6.5f));
                for (int dx = -hw; dx <= hw; dx++)
                {
                    Color c;
                    if (w == 1) c = Hot;                                   // edge-on: a glint
                    else if (row >= 10) c = dx <= 0 ? Hot : Salvage.Shift; // the tip catches light
                    else if (dx == 0) c = Salvage.Shift;
                    else c = (dx < 0) != back ? Hot : Deep;                // lit from the upper left
                    Put(ox + 3 + dx, oy + row, c);
                }
            }
            Put(ox + 3, oy + 12, Color.white);                             // the top point
            shardR[f] = new RectInt(ox, oy, 7, 13);
        }

        // Bolts: eight jagged pixel lightning strokes, a white-hot core with a cyan glow either side.
        var rng = new System.Random(4711);
        var arcR = new RectInt[ArcCount];
        for (int i = 0; i < ArcCount; i++)
        {
            int ox = 1 + i * 15, oy = 16;
            int w = 13, h = 12 + rng.Next(0, 11);
            var core = new List<Vector2Int>();
            int x = w / 2 + rng.Next(-2, 3);
            for (int y = 0; y < h; y++)
            {
                core.Add(new Vector2Int(x, y));
                int step = rng.Next(-1, 2);
                if (rng.NextDouble() < 0.3) step *= 2;
                int nx = Mathf.Clamp(x + step, 1, w - 2);
                for (int fx = Mathf.Min(x, nx); fx <= Mathf.Max(x, nx); fx++) core.Add(new Vector2Int(fx, y));
                x = nx;
                // A short fork off the main stroke, now and then.
                if (y > 2 && y < h - 4 && rng.NextDouble() < 0.14)
                {
                    int dir = rng.Next(0, 2) == 0 ? -1 : 1, bx = x;
                    for (int k = 1; k <= 3; k++) { bx = Mathf.Clamp(bx + dir, 1, w - 2); core.Add(new Vector2Int(bx, y + k)); }
                }
            }
            Color glow = Salvage.Shift; glow.a = 0.55f;
            foreach (Vector2Int p in core)
                foreach (Vector2Int n in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    Vector2Int q = p + n;
                    if (q.x < 0 || q.y < 0 || q.x >= w || q.y >= h) continue;
                    if (Get(ox + q.x, oy + q.y).a < 0.5f) Put(ox + q.x, oy + q.y, glow);
                }
            foreach (Vector2Int p in core) Put(ox + p.x, oy + p.y, Hot);
            arcR[i] = new RectInt(ox, oy, w, h);
        }

        tex.SetPixels(atlas);
        tex.Apply();
        tex.name = "ShiftInfusionAtlas";
        atlas = null;

        Shader unlit = Shader.Find("Sprites/Default");
        if (unlit != null) atlasMat = new Material(unlit) { name = "ShiftInfusionAtlas", mainTexture = tex };

        mote = Cut(tex, moteR, "ShiftMote");
        dotBig = Cut(tex, dotBigR, "ShiftDotBig");
        dotSmall = Cut(tex, dotSmallR, "ShiftDotSmall");
        for (int f = 0; f < ShardFrames; f++) shard[f] = Cut(tex, shardR[f], "ShiftShard" + f);
        for (int i = 0; i < ArcCount; i++) arcs[i] = Cut(tex, arcR[i], "ShiftArc" + i);
    }

    private static Sprite Cut(Texture2D tex, RectInt r, string name)
    {
        var s = Sprite.Create(tex, new Rect(r.x, r.y, r.width, r.height), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
        s.name = name;
        return s;
    }
}
