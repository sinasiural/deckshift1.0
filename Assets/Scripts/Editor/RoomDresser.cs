using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Automatic set dressing for rooms built by LevelTextImporter. Designer, 2026-09-25: generated
// rooms "look bare".
//
// MEASURED FIRST. The seven hand-made pool rooms carry 50-100 Cainos props each; every generated
// room carried zero, and GenLevel10 has since been dressed by hand, which is the complaint in
// action. Three habits of the hand-made rooms are reproduced here, and they matter more than the
// prop count:
//
//   1. VIGNETTES, NOT SCATTER. Props come in small scenes that say what the room WAS: a table with
//      chairs and a candle on it, a shrine flanked by candlestands and banners, a row of coffins.
//      The same props scattered one at a time read as clutter, not as a place.
//   2. ONE THEME PER ROOM. efeslevel2 is a library, EfeVrl5 a prison, EfeVrl4 a barracks.
//   3. STRUCTURE. EfeVrl7's ledges hang from chains and stand on pillars; nothing floats for no
//      reason. Free-standing platforms here get chains up to the ceiling.
//
// ⚠️ READABILITY LAW — most of the exclusions below exist for it: DECORATION MUST NEVER LOOK LIKE
// SOMETHING YOU CAN USE. No props that look like platforms (Platform, Beam, Stairs, Ladder),
// hazards (Spear Trap, Spike Ball, Swinging Blade), interactables (Switch, Door, Trapdoor, Chest,
// Elevator) or pickups (Bag is a gold pickup's silhouette; Coin Pile is gold). A player who jumps
// at painted-on stairs, or walks around a harmless spike ball, has been lied to by the room.
//
// Decoration has NO gameplay effect: no prop in the pack carries a collider (verified across all
// of it, 2026-09-25), this pass never edits the grid, and it runs after every gameplay object is
// placed, keeping clear of all of them. Everything lands under one "Dressing" child, so the
// designer can edit or delete it by hand, and it is seeded by the room's name, so re-importing
// reproduces the same dressing.
//
// Light from the upper left is a house law (Salvage), so no prop is ever mirrored to add variety:
// a flipped prop has its shading on the wrong side.
public static class RoomDresser
{
    public enum Density { Off, Light, Normal, Heavy }

    // Everything the dresser needs to know about the room. Rows are TEXT rows (row 0 = top line),
    // after any grid transform the importer applied, so dressing matches the built geometry.
    public class Room
    {
        public char[][] rows;
        public int width, height;
        public string name;
        public string theme;          // null/empty = picked from the name
        public Density density = Density.Normal;
        public Transform root;
    }

    public static readonly string[] Themes = { "crypt", "cellar", "library", "barracks", "prison" };

    // ---- prop library -------------------------------------------------------------------------

    private const string PropDir = "Assets/Cainos/Pixel Art Platformer - Dungeon/Prefab";

    // Short key ("Table 01") -> prefab. Keys are file names with the "PF Dungeon " prefix removed,
    // and "Props - " removed too for the common props, so recipes stay readable.
    private static Dictionary<string, GameObject> prefabs;
    private static readonly Dictionary<GameObject, Bounds> measured = new Dictionary<GameObject, Bounds>();

    private static GameObject Prefab(string key)
    {
        if (prefabs == null)
        {
            prefabs = new Dictionary<string, GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PropDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("PF Dungeon ")) continue;
                string k = file.Substring("PF Dungeon ".Length);
                if (k.StartsWith("Props - ")) k = k.Substring("Props - ".Length);
                prefabs[k] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
        prefabs.TryGetValue(key, out GameObject go);
        return go;
    }

    // Visual bounds of a prefab relative to its own origin (particles excluded), measured once.
    private static Bounds Measure(GameObject prefab)
    {
        if (measured.TryGetValue(prefab, out Bounds b)) return b;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            inst.transform.position = Vector3.zero;
            Bounds? acc = null;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (acc == null) acc = r.bounds; else { var bb = acc.Value; bb.Encapsulate(r.bounds); acc = bb; }
            }
            b = acc ?? new Bounds(Vector3.zero, Vector3.one);
        }
        finally { UnityEngine.Object.DestroyImmediate(inst); }
        measured[prefab] = b;
        return b;
    }

    // ---- recipes ------------------------------------------------------------------------------

    private enum Mount { Floor, OnTop, Wall }

    private struct Part
    {
        public string Key; public float Dx; public Mount Mount; public float Y; public int On;
    }

    private class Recipe
    {
        public string Name; public float Width; public Part[] Parts;
    }

    // Floor: centred at Dx, standing on the floor. OnTop: centred at Dx, standing on the top of
    // part #on (a candle on a table). Wall: centred at (Dx, floor + y) on the back wall.
    private static Part F(string k, float dx) => new Part { Key = k, Dx = dx, Mount = Mount.Floor };
    private static Part T(string k, float dx, int on) => new Part { Key = k, Dx = dx, Mount = Mount.OnTop, On = on };
    private static Part W(string k, float dx, float y) => new Part { Key = k, Dx = dx, Mount = Mount.Wall, Y = y };
    private static Recipe R(string name, float width, params Part[] parts) => new Recipe { Name = name, Width = width, Parts = parts };

    private static readonly Dictionary<string, Recipe> Recipes = new Dictionary<string, Recipe>
    {
        { "shrine",     R("shrine", 4.2f, F("Wall Altar 01", 2.1f), F("Candlestand 01", 0.6f), F("Candlestand 01", 3.6f),
                                          W("Banner 01 A", 0.6f, 2.95f), W("Banner 01 B", 3.6f, 2.95f)) },
        { "coffins",    R("coffins", 4.0f, F("Stone Coffin 01 A", 0.75f), F("Stone Coffin 01 C", 2.1f), F("Candle 01", 3.1f), F("Skull 02", 3.6f)) },
        { "upright",    R("upright", 3.2f, F("Wooden Coffin 02 A", 0.6f), F("Wooden Coffin 02 B", 1.8f), F("Skull 01", 2.8f)) },
        { "statues",    R("statues", 3.2f, F("Church Statue 01", 0.4f), F("Candlestand 01", 1.6f), F("Church Statue 01", 2.8f)) },
        { "statue",     R("statue", 2.4f, F("Statue 01 A", 1.2f)) },
        { "bones",      R("bones", 1.8f, F("Skull 03", 0.4f), F("Bone 02", 1.2f)) },
        { "debris",     R("debris", 2.4f, F("Debris Brick 02", 0.8f), F("Debris Wood 01 A", 1.8f)) },
        { "table",      R("table", 3.6f, F("Table 01", 1.8f), F("Chair 02 Side A", 0.4f), F("Chair 02 Side B", 3.2f),
                                         T("Candlestick 01", 1.4f, 0), T("Cup 01", 2.05f, 0), T("Silver Jug 01", 2.4f, 0)) },
        { "sidetable",  R("sidetable", 1.6f, F("Small Table 01", 0.8f), T("Candle 02", 0.6f, 0), T("Book Group 01", 1.0f, 0)) },
        { "storage",    R("storage", 3.0f, F("Barrel 01 A", 0.5f), F("Barrel 01 B", 1.4f), F("Crate 04", 2.45f), T("Jar 01 A", 2.45f, 2)) },
        { "bigbarrel",  R("bigbarrel", 3.2f, F("Barrel Large 01", 1.1f), F("Barrel 02 A", 2.65f)) },
        { "crates",     R("crates", 2.6f, F("Crate 02", 0.7f), F("Crate 03", 1.95f), T("Crate 03", 0.7f, 0)) },
        { "pots",       R("pots", 2.0f, F("Pot 01 A", 0.35f), F("Pot 02 B", 1.0f), F("Pot 03 A", 1.65f)) },
        { "cupboard",   R("cupboard", 1.6f, F("Cupboard 01", 0.6f), T("Bottle 01 A", 0.35f, 0), T("Bottle 02 B", 0.75f, 0), F("Jar 01 C", 1.35f)) },
        { "kitchen",    R("kitchen", 4.4f, F("Stove 01", 2.1f), T("Cooking Pot 01", 1.4f, 0), T("Kettle 01", 2.7f, 0)) },
        { "cauldron",   R("cauldron", 2.4f, F("Caudron 01", 0.8f), F("Caudron Tool 01", 1.9f)) },
        { "bookcase",   R("bookcase", 3.2f, F("Bookshelf 01 A", 0.8f), F("Bookshelf 02 B", 2.3f)) },
        { "stoneshelf", R("stoneshelf", 2.6f, F("Stone Bookshelf 01", 1.1f), F("Book Group 05", 2.35f)) },
        { "reading",    R("reading", 2.6f, F("Lectern", 0.6f), F("Candlestand 01", 1.6f), F("Book Group 12", 2.25f)) },
        { "bed",        R("bed", 3.6f, F("Bed 01", 1.2f), F("Small Table 02", 3.0f), T("Candlestick 01", 3.0f, 1)) },
        { "bunk",       R("bunk", 2.8f, F("Bed 02 A", 1.05f), F("Stool 01 A", 2.4f)) },
        { "bench",      R("bench", 2.4f, F("Bench 01 A", 1.15f)) },
        { "armory",     R("armory", 3.0f, F("Hanger 01", 0.6f), F("Rack 01", 2.1f)) },
        // ⚠️ NOT "Wall Deco - Prison 01": its wooden double doors read as an EXIT (measured by
        // rendering it against the wall, 2026-09-25). Same reason "Wall Deco - Outfall 01" is unused.
        { "cell",       R("cell", 3.6f, W("Manacle 01 A", 0.5f, 2.35f), W("Manacle 01 B", 1.2f, 2.35f), F("Skull 02", 0.7f),
                                        F("Bone 01", 1.5f), F("Rotten Food 01", 2.3f), F("Bucket 01", 3.2f)) },
        { "torture",    R("torture", 3.0f, F("Torture Chair 01", 0.6f), W("Manacle 01 A", 1.7f, 2.35f), W("Manacle 01 B", 2.35f, 2.35f), F("Bucket 01", 2.7f)) },
    };

    // Single small props for leftover floor, where no vignette fits.
    private static readonly Dictionary<string, string[]> Fillers = new Dictionary<string, string[]>
    {
        { "crypt",    new[] { "Skull 01", "Skull 04", "Bone 03", "Candle 03", "Pot 04 A", "Debris Brick 04 A" } },
        { "cellar",   new[] { "Jar 01 B", "Jar 01 E", "Bottle 01 A", "Basket 01", "Pot 04 B", "Pot 05 A" } },
        { "library",  new[] { "Book Group 02", "Book Group 09", "Book Group 16", "Bookend 01", "Candle 01" } },
        { "barracks", new[] { "Stool 01 B", "Basket 02", "Cup 01", "Pot 03 B", "Bucket 01" } },
        { "prison",   new[] { "Skull 02", "Bone 01", "Bone 04", "Bucket 01", "Debris Brick 01 A", "Rotten Food 01" } },
    };

    // Which vignettes each theme uses, with weights. Built from what the matching hand-made room
    // actually contains (see the header).
    private static readonly Dictionary<string, (string, int)[]> ThemeFloor = new Dictionary<string, (string, int)[]>
    {
        { "crypt",    new[] { ("shrine", 3), ("coffins", 3), ("upright", 2), ("statues", 2), ("bones", 3), ("statue", 1), ("cauldron", 1), ("pots", 1), ("debris", 1) } },
        { "cellar",   new[] { ("storage", 4), ("bigbarrel", 2), ("crates", 3), ("pots", 3), ("cupboard", 2), ("kitchen", 1), ("cauldron", 1), ("table", 1), ("debris", 2) } },
        { "library",  new[] { ("bookcase", 4), ("reading", 3), ("stoneshelf", 2), ("sidetable", 2), ("table", 1), ("statue", 1), ("statues", 1) } },
        { "barracks", new[] { ("table", 3), ("bed", 3), ("bunk", 2), ("bench", 2), ("armory", 2), ("storage", 2), ("kitchen", 1), ("cupboard", 1) } },
        { "prison",   new[] { ("cell", 3), ("torture", 2), ("bones", 3), ("upright", 1), ("storage", 1), ("cauldron", 1), ("debris", 2) } },
    };

    // Hung on the back wall above floors, between vignettes.
    private static readonly Dictionary<string, string[]> ThemeWall = new Dictionary<string, string[]>
    {
        { "crypt",    new[] { "Banner 01 A", "Banner 01 B", "Banner 01 C", "Painting 01 A", "Painting 03 B", "Lamp 01", "Lamp 01" } },
        { "cellar",   new[] { "Shelf 01 A", "Shelf 01 B", "Banner 01 C", "Painting 02 A", "Lamp 01", "Lamp 01", "Tool Hanger 01 A" } },
        { "library",  new[] { "Painting 01 B", "Painting 02 B", "Painting 03 A", "Painting 04 A", "Painting 05 A", "Banner 01 A", "Lamp 01", "Lamp 01" } },
        { "barracks", new[] { "Banner 01 A", "Banner 01 B", "Painting 04 B", "Painting 05 B", "Key Holder 01", "Lamp 01", "Lamp 01" } },
        { "prison",   new[] { "Manacle 01 A", "Manacle 01 B", "Key Holder 01", "Banner 01 C", "Lamp 01", "Lamp 01" } },
    };

    // Hung from ceilings over open space.
    private static readonly Dictionary<string, string[]> ThemeCeiling = new Dictionary<string, string[]>
    {
        { "crypt",    new[] { "Chandelier 01", "Cage 01 A", "Lamp 01", "Ceiling Chain" } },
        { "cellar",   new[] { "Lamp 01", "Lamp 01", "Ceiling Chain" } },
        { "library",  new[] { "Chandelier 01", "Chandelier 01", "Lamp 01" } },
        { "barracks", new[] { "Chandelier 01", "Lamp 01", "Ceiling Chain" } },
        { "prison",   new[] { "Cage 01 A", "Cage 01 B", "Cage 01 C", "Lamp 01", "Ceiling Chain" } },
    };

    // Set pieces on large empty stretches of back wall: arched niches, and small windows.
    //
    // ⚠️ THE "Wall Deco - *" SERIES IS NOT USED, and each exclusion was judged on screen, not
    // guessed (2026-09-25). The Dents are pale grey brick patches that read as SOLID BLOCKS
    // floating on the wall (a readability-law violation), the wall-deco windows carry the same
    // pale brick surround, and Outfall / Prison 01 are wooden doors a player would walk up to
    // expecting an exit. The Wall Cave niches and the small arched Windows sit IN the wall
    // instead of on top of it.
    private static readonly Dictionary<string, string[]> ThemeFeature = new Dictionary<string, string[]>
    {
        { "crypt",    new[] { "Wall Cave 01 A", "Wall Cave 01 B", "Wall Cave 02 A", "Wall Cave 02 B" } },
        { "cellar",   new[] { "Wall Cave 01 B", "Wall Cave 02 A", "Wall Cave 02 B" } },
        { "library",  new[] { "Window 02 B", "Window 03 C", "Window 01 A", "Wall Cave 02 A" } },
        { "barracks", new[] { "Window 01 B", "Window 03 A", "Wall Cave 01 A" } },
        { "prison",   new[] { "Wall Cave 01 A", "Wall Cave 02 B", "Window 01 C" } },
    };

    private static readonly string[] Grime =
        { "Wall Dirt - 01", "Wall Dirt - 03", "Wall Dirt - 05", "Wall Dirt - 07", "Wall Dirt - 09", "Wall Dirt - 11", "Wall Dirt - 13" };
    private static readonly string[] Webs = { "Spider Web - 03", "Spider Web - 04", "Spider Web - 06" };

    // ---- the pass -----------------------------------------------------------------------------

    private const float DressZ = 0.6f;          // behind actors (PlayPlane.Z = -2), in front of tiles (z = 1)
    private const int MaxLights = 16;           // warm Light2Ds per room; small point lights are cheap, not free

    private class Ctx
    {
        public Room room;
        public System.Random rng;
        public string theme;
        public float placeChance, gapScale;
        public bool[,] reserved;               // [col,row] no decoration may overlap these cells
        public List<Rect> used = new List<Rect>();   // world rects already taken by wall / ceiling decoration
        public Transform floorGroup, wallGroup, ceilingGroup, chainGroup, grimeGroup;
        public List<GameObject> placed = new List<GameObject>();
        public Dictionary<string, int> counts = new Dictionary<string, int>();
        public int missing;
    }

    // Returns a one-line summary for the importer's report.
    public static string Dress(Room room)
    {
        if (room.density == Density.Off) return "dressing off";

        var ctx = new Ctx { room = room };
        int seed = LevelGridOps.StableHash(room.name ?? "room");
        ctx.rng = new System.Random(seed);
        ctx.theme = PickTheme(room, seed);
        ctx.placeChance = room.density == Density.Light ? 0.45f : room.density == Density.Heavy ? 0.92f : 0.72f;
        ctx.gapScale = room.density == Density.Light ? 1.6f : room.density == Density.Heavy ? 0.7f : 1f;

        var dressing = new GameObject("Dressing").transform;
        dressing.SetParent(room.root, false);
        ctx.floorGroup = Group(dressing, "Floor");
        ctx.wallGroup = Group(dressing, "Wall");
        ctx.ceilingGroup = Group(dressing, "Ceiling");
        ctx.chainGroup = Group(dressing, "Chains");
        ctx.grimeGroup = Group(dressing, "Grime");

        BuildReservations(ctx);

        // Order matters: structure first (chains belong to the geometry), then the vignettes that
        // tell the story, then what fills the walls around them, then grime last into the gaps.
        HangPlatforms(ctx);
        DressFloors(ctx);
        DressWalls(ctx);
        DressCeilings(ctx);
        PlaceFeatures(ctx);
        PlaceWebs(ctx);
        PlaceGrime(ctx);
        int lights = AddLights(ctx);

        int total = ctx.placed.Count;
        return $"dressing: theme '{ctx.theme}', {total} props, {lights} lights" +
               (ctx.missing > 0 ? $" ({ctx.missing} prop keys not found — check RoomDresser tables)" : "");
    }

    private static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static string PickTheme(Room room, int seed)
    {
        if (!string.IsNullOrEmpty(room.theme))
        {
            string t = room.theme.Trim().ToLowerInvariant();
            if (Array.IndexOf(Themes, t) >= 0) return t;
            Debug.LogWarning($"[RoomDresser] unknown theme '{room.theme}', picking one from the room name. Themes: {string.Join(", ", Themes)}");
        }
        return Themes[seed % Themes.Length];
    }

    // ---- grid helpers (text rows: row 0 = top) --------------------------------------------------

    private static char At(Ctx x, int c, int r)
    {
        var rows = x.room.rows;
        if (r < 0 || r >= rows.Length || c < 0 || c >= rows[r].Length) return '#';
        return rows[r][c];
    }

    // Gates and crusher heads count as wall for dressing: nothing is painted over them.
    private static bool Solid(Ctx x, int c, int r)
    {
        if (c < 0 || r < 0 || c >= x.room.width || r >= x.room.height) return true;
        char ch = At(x, c, r);
        return ch == '#' || ch == 'G';
    }

    private static bool Reserved(Ctx x, int c, int r) =>
        c < 0 || r < 0 || c >= x.room.width || r >= x.room.height || x.reserved[c, r];

    // Bottom edge (world y) of a text row.
    private static float RowBottom(Ctx x, int r) => x.room.height - 1 - r;

    private static int RowOfWorldY(Ctx x, float y) => x.room.height - 1 - Mathf.FloorToInt(y);

    // Every marker keeps a clear zone so decoration never crowds what the player must read.
    private static void BuildReservations(Ctx x)
    {
        int w = x.room.width, h = x.room.height;
        x.reserved = new bool[w, h];

        void Box(int c, int r, int left, int right, int up, int down)
        {
            for (int rr = r - up; rr <= r + down; rr++)
                for (int cc = c - left; cc <= c + right; cc++)
                    if (cc >= 0 && rr >= 0 && cc < w && rr < h) x.reserved[cc, rr] = true;
        }

        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                char ch = At(x, c, r);
                switch (ch)
                {
                    case '.': case ' ': case '#': break;

                    // Doors, loot, NPCs and switches: generous, they are what the player looks for.
                    case 'S': case 'X': case 'C': case 'D': case 'A': case 'L':
                    case '$': case 'B': case 'f': case 'Q': case 'H': case 'M':
                        Box(c, r, 2, 2, 4, 0); break;

                    // Gates: the arch is wider than its cell.
                    case 'G': Box(c, r, 2, 2, 1, 1); break;

                    // Crusher: its whole drop column, three wide, down to the floor.
                    case 'P':
                        for (int rr = r; rr < h && !Solid(x, c, rr + 0); rr++) Box(c, rr, 2, 2, 0, 0);
                        Box(c, r, 2, 2, 1, 1);
                        break;

                    // Water and hazards: the cell and a margin.
                    case '~': case 'w': case '^': case 'T': case 'W': case 'F': case 'E': case 'K':
                        Box(c, r, 1, 1, 2, 1); break;

                    // Enemies and pickups: a small clear zone.
                    default: Box(c, r, 1, 1, 2, 0); break;
                }
            }
    }

    private static bool AreaClear(Ctx x, Rect wr, bool checkUsed)
    {
        int c0 = Mathf.FloorToInt(wr.xMin + 0.05f), c1 = Mathf.FloorToInt(wr.xMax - 0.05f);
        int r0 = RowOfWorldY(x, wr.yMax - 0.05f), r1 = RowOfWorldY(x, wr.yMin + 0.05f);
        for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
                if (Solid(x, c, r) || Reserved(x, c, r)) return false;
        if (checkUsed)
            foreach (var u in x.used)
                if (u.Overlaps(wr)) return false;
        return true;
    }

    // ---- placing one prop -----------------------------------------------------------------------

    // Places `key` so its VISUAL bounds sit where asked (pivots in this pack vary wildly, from the
    // base of a barrel to the top of a hanging lamp, so measuring beats trusting the origin).
    // anchor: 0 = bottom edge at y, 1 = top edge at y, 0.5 = centred on y.
    private static GameObject Place(Ctx x, string key, Transform parent, float cx, float y, float anchor)
    {
        GameObject prefab = Prefab(key);
        if (prefab == null) { x.missing++; Debug.LogWarning("[RoomDresser] no prop '" + key + "'"); return null; }

        Bounds b = Measure(prefab);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        float targetMinY = y - b.size.y * anchor;
        go.transform.position = new Vector3(cx - b.center.x, targetMinY - b.min.y, 0f);

        // Some pack props carry their sprite on a child pushed ~3 units deep (the webs and wall
        // dirt sit at local z +2.95), which puts them BEHIND the tilemap. Shift the whole prop so
        // its deepest renderer lands on the dressing plane.
        float deepest = float.MinValue;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) deepest = Mathf.Max(deepest, r.transform.position.z);
        if (deepest > float.MinValue)
        {
            Vector3 p = go.transform.position;
            p.z += DressZ - deepest;
            go.transform.position = p;
        }

        x.placed.Add(go);
        x.counts.TryGetValue(key, out int n);
        x.counts[key] = n + 1;
        return go;
    }

    private static Rect WorldRect(string key, float cx, float y, float anchor)
    {
        GameObject prefab = Prefab(key);
        if (prefab == null) return new Rect(cx, y, 0f, 0f);
        Bounds b = Measure(prefab);
        float minY = y - b.size.y * anchor;
        return new Rect(cx - b.size.x * 0.5f, minY, b.size.x, b.size.y);
    }

    private static float Height(string key) { var p = Prefab(key); return p == null ? 0f : Measure(p).size.y; }
    private static float Width(string key) { var p = Prefab(key); return p == null ? 0f : Measure(p).size.x; }

    private static T Pick<T>(Ctx x, T[] items) => items[x.rng.Next(items.Length)];

    private static string PickWeighted(Ctx x, (string, int)[] items, Func<string, bool> allowed)
    {
        int total = 0;
        foreach (var (n, wgt) in items) if (allowed(n)) total += wgt;
        if (total == 0) return null;
        int roll = x.rng.Next(total);
        foreach (var (n, wgt) in items)
        {
            if (!allowed(n)) continue;
            if (roll < wgt) return n;
            roll -= wgt;
        }
        return null;
    }

    // ---- structure: chains up from free-standing ledges -----------------------------------------

    // A mid-air ledge with nothing holding it up is the single most "generated" thing in these
    // rooms. Hand-made rooms hang them from chains. Each free-standing platform whose ceiling is
    // within reach gets a chain from each end up to the rock above.
    private static void HangPlatforms(Ctx x)
    {
        int w = x.room.width, h = x.room.height;
        var seen = new bool[w, h];
        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                if (seen[c, r] || At(x, c, r) != '#') continue;

                var cells = new List<Vector2Int>();
                var stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(c, r)); seen[c, r] = true;
                bool border = false;
                while (stack.Count > 0)
                {
                    var p = stack.Pop(); cells.Add(p);
                    if (p.x == 0 || p.y == 0 || p.x == w - 1 || p.y == h - 1) border = true;
                    foreach (var q in new[] { new Vector2Int(p.x + 1, p.y), new Vector2Int(p.x - 1, p.y), new Vector2Int(p.x, p.y + 1), new Vector2Int(p.x, p.y - 1) })
                    {
                        if (q.x < 0 || q.y < 0 || q.x >= w || q.y >= h || seen[q.x, q.y] || At(x, q.x, q.y) != '#') continue;
                        seen[q.x, q.y] = true; stack.Push(q);
                    }
                }
                if (border || cells.Count > 40) continue;

                int minX = int.MaxValue, maxX = int.MinValue, topRow = int.MaxValue;
                foreach (var p in cells) { minX = Math.Min(minX, p.x); maxX = Math.Max(maxX, p.x); topRow = Math.Min(topRow, p.y); }

                // Chains only on ledges at least 2 wide; a single stepping stone on two chains
                // reads as a lamp, not a platform.
                if (maxX - minX < 1) continue;

                foreach (int col in new[] { minX, maxX })
                {
                    float cx = col == minX ? minX + 0.3f : maxX + 0.7f;
                    // The ledge's top row at this column.
                    int top = topRow;
                    while (top < h && At(x, col, top) != '#') top++;

                    // Reservations are NOT checked here: they keep clutter off what the player must
                    // read, and a chain holding up the very ledge a chest sits on is structure,
                    // not clutter. (Checking them left every loot stone floating, 2026-09-25.)
                    int up = top - 1, dist = 0;
                    while (up >= 0 && !Solid(x, col, up)) { up--; dist++; }
                    if (up < 0 || dist < 2 || dist > 14) continue;

                    float ceilY = RowBottom(x, up);         // underside of the rock above
                    float ledgeY = RowBottom(x, top) + 1f;  // walking surface of the ledge
                    MakeChain(x, cx, ceilY, ledgeY);
                }
            }
    }

    // The pack's Ceiling Chain is a mount plus a TILED chain sprite, so it stretches cleanly to any
    // length instead of stacking copies with a mount repeated every 1.2 units.
    private static void MakeChain(Ctx x, float cx, float topY, float bottomY)
    {
        var go = Place(x, "Ceiling Chain", x.chainGroup, cx, topY, 1f);
        if (go == null) return;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.drawMode != SpriteDrawMode.Tiled) continue;
            float chainTop = sr.bounds.max.y;
            sr.size = new Vector2(sr.size.x, Mathf.Max(0.3f, chainTop - bottomY));
            // Tiled sprites grow about their pivot, so re-seat the top where the mount is.
            sr.transform.position += new Vector3(0f, chainTop - sr.bounds.max.y, 0f);
        }
        x.used.Add(new Rect(cx - 0.3f, bottomY, 0.6f, topY - bottomY));
    }

    // ---- floors: the vignettes ------------------------------------------------------------------

    private struct Run { public int row, c0, c1, headroom; }

    private static List<Run> FloorRuns(Ctx x)
    {
        var runs = new List<Run>();
        int w = x.room.width, h = x.room.height;
        for (int r = 0; r < h; r++)
        {
            int start = -1, head = int.MaxValue;
            for (int c = 0; c <= w; c++)
            {
                bool floor = c < w && !Solid(x, c, r) && Solid(x, c, r + 1) && !Reserved(x, c, r)
                             && At(x, c, r + 1) == '#';     // on rock, not on a gate
                int hr = 0;
                if (floor) { while (hr < 8 && !Solid(x, c, r - hr) && !Reserved(x, c, r - hr)) hr++; if (hr < 2) floor = false; }

                if (floor)
                {
                    if (start < 0) { start = c; head = hr; }
                    else head = Math.Min(head, hr);
                }
                else if (start >= 0)
                {
                    runs.Add(new Run { row = r, c0 = start, c1 = c - 1, headroom = head });
                    start = -1; head = int.MaxValue;
                }
            }
        }
        return runs;
    }

    private static float RecipeHeight(Recipe rc)
    {
        float top = 0f;
        foreach (var p in rc.Parts)
        {
            float hgt = Height(p.Key);
            if (p.Mount == Mount.Wall) top = Mathf.Max(top, p.Y + hgt * 0.5f);
            else if (p.Mount == Mount.Floor) top = Mathf.Max(top, hgt);
            else top = Mathf.Max(top, 1.2f + hgt);
        }
        return top;
    }

    private static void DressFloors(Ctx x)
    {
        var options = ThemeFloor[x.theme];
        string[] fill = Fillers[x.theme];
        var lastUsed = new Dictionary<string, int>();
        int placedVignettes = 0;

        foreach (var run in FloorRuns(x))
        {
            float floorY = RowBottom(x, run.row);
            float end = run.c1 + 1f;
            float pos = run.c0 + (float)x.rng.NextDouble() * 1.2f;

            while (pos < end - 0.8f)
            {
                float room = end - pos;
                // Never the same vignette twice in a row, and never one that doesn't fit.
                string pick = PickWeighted(x, options, n =>
                {
                    Recipe rc = Recipes[n];
                    if (rc.Width > room) return false;
                    if (RecipeHeight(rc) > run.headroom - 0.3f) return false;
                    return !lastUsed.TryGetValue(n, out int at) || placedVignettes - at > 1;
                });

                if (pick == null || x.rng.NextDouble() > x.placeChance)
                {
                    // A lone small prop where a vignette doesn't fit or wasn't wanted, sparingly.
                    if (room >= 1f && x.rng.NextDouble() < x.placeChance * 0.35)
                    {
                        string k = Pick(x, fill);
                        Place(x, k, x.floorGroup, pos + 0.5f, floorY, 0f);
                    }
                    pos += (1.5f + (float)x.rng.NextDouble() * 2f) * x.gapScale;
                    continue;
                }

                PlaceRecipe(x, Recipes[pick], pos, floorY);
                lastUsed[pick] = placedVignettes++;
                pos += Recipes[pick].Width + (1.5f + (float)x.rng.NextDouble() * 3f) * x.gapScale;
            }
        }
    }

    private static void PlaceRecipe(Ctx x, Recipe rc, float left, float floorY)
    {
        var made = new GameObject[rc.Parts.Length];
        for (int i = 0; i < rc.Parts.Length; i++)
        {
            Part p = rc.Parts[i];
            float cx = left + p.Dx;
            switch (p.Mount)
            {
                case Mount.Floor:
                    made[i] = Place(x, p.Key, x.floorGroup, cx, floorY, 0f);
                    break;
                case Mount.OnTop:
                    if (p.On < 0 || p.On >= i || made[p.On] == null) break;
                    float top = float.MinValue;
                    foreach (var r in made[p.On].GetComponentsInChildren<Renderer>(true))
                        if (!(r is ParticleSystemRenderer)) top = Mathf.Max(top, r.bounds.max.y);
                    made[i] = Place(x, p.Key, x.floorGroup, cx, top - 0.06f, 0f);
                    break;
                case Mount.Wall:
                    made[i] = Place(x, p.Key, x.wallGroup, cx, floorY + p.Y, 0.5f);
                    if (made[i] != null) x.used.Add(WorldRect(p.Key, cx, floorY + p.Y, 0.5f));
                    break;
            }
        }
        // The vignette's own footprint on the wall, so wall pieces don't land on top of it.
        x.used.Add(new Rect(left, floorY, rc.Width, RecipeHeight(rc)));
    }

    // ---- walls ----------------------------------------------------------------------------------

    private static void DressWalls(Ctx x)
    {
        string[] pieces = ThemeWall[x.theme];
        foreach (var run in FloorRuns(x))
        {
            if (run.headroom < 4) continue;
            float floorY = RowBottom(x, run.row);
            for (float cx = run.c0 + 1.5f; cx < run.c1 - 0.5f; cx += (3.5f + (float)x.rng.NextDouble() * 3f) * x.gapScale)
            {
                if (x.rng.NextDouble() > x.placeChance * 0.8f) continue;
                string key = Pick(x, pieces);
                float y = floorY + 2.3f + (float)x.rng.NextDouble() * 0.9f;
                Rect wr = WorldRect(key, cx, y, 0.5f);
                Rect padded = new Rect(wr.x - 0.3f, wr.y - 0.3f, wr.width + 0.6f, wr.height + 0.6f);
                if (wr.yMax > floorY + run.headroom - 0.2f || !AreaClear(x, padded, true)) continue;

                var go = Place(x, key, x.wallGroup, cx, y, 0.5f);
                if (go == null) continue;
                x.used.Add(padded);

                // A shelf gets a few things on it, like the hand-made cellars do.
                if (key.StartsWith("Shelf"))
                {
                    float top = float.MinValue;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) top = Mathf.Max(top, r.bounds.max.y);
                    string[] onShelf = { "Jar 01 A", "Bottle 02 A", "Jar 01 D", "Bottle 04 B", "Pot 04 A" };
                    for (float sx = wr.xMin + 0.35f; sx < wr.xMax - 0.2f; sx += 0.45f + (float)x.rng.NextDouble() * 0.3f)
                        Place(x, Pick(x, onShelf), x.wallGroup, sx, top - 0.12f, 0f);
                }
            }
        }
    }

    // ---- ceilings -------------------------------------------------------------------------------

    private static void DressCeilings(Ctx x)
    {
        string[] pieces = ThemeCeiling[x.theme];
        int w = x.room.width, h = x.room.height;
        float nextAllowed = float.MinValue;
        int lastRow = -1;

        for (int r = 1; r < h; r++)
        {
            for (int c = 1; c < w - 1; c++)
            {
                if (Solid(x, c, r) || !Solid(x, c, r - 1) || Reserved(x, c, r)) continue;
                if (r != lastRow) { lastRow = r; nextAllowed = float.MinValue; }
                if (c < nextAllowed) continue;
                if (x.rng.NextDouble() > x.placeChance * 0.3f) continue;

                string key = Pick(x, pieces);
                float hgt = Height(key);
                int needAir = Mathf.CeilToInt(hgt) + 4;       // the player passes underneath untouched
                int air = 0;
                while (air < needAir && !Solid(x, c, r + air) && !Reserved(x, c, r + air)) air++;
                if (air < needAir) continue;

                float ceilY = RowBottom(x, r) + 1f;
                Rect wr = WorldRect(key, c + 0.5f, ceilY, 1f);
                if (!AreaClear(x, new Rect(wr.x - 0.4f, wr.y, wr.width + 0.8f, wr.height), true)) continue;

                if (Place(x, key, x.ceilingGroup, c + 0.5f, ceilY, 1f) == null) continue;
                x.used.Add(wr);
                nextAllowed = c + (6f + (float)x.rng.NextDouble() * 6f) * x.gapScale;
            }
        }
    }

    // ---- big set pieces on empty wall -----------------------------------------------------------

    private static void PlaceFeatures(Ctx x)
    {
        string[] pieces = ThemeFeature[x.theme];
        int area = x.room.width * x.room.height;
        int want = Mathf.Clamp(area / 450, 1, 6);
        int tries = want * 40;
        for (int i = 0; i < tries && want > 0; i++)
        {
            string key = Pick(x, pieces);
            float cx = 2f + (float)x.rng.NextDouble() * (x.room.width - 4);
            float cy = 2f + (float)x.rng.NextDouble() * (x.room.height - 4);
            Rect wr = WorldRect(key, cx, cy, 0.5f);
            Rect padded = new Rect(wr.x - 1f, wr.y - 1f, wr.width + 2f, wr.height + 2f);
            if (!AreaClear(x, padded, true)) continue;
            if (Place(x, key, x.grimeGroup, cx, cy, 0.5f) == null) continue;
            x.used.Add(padded);
            want--;
        }
    }

    // ---- spider webs in upper corners -----------------------------------------------------------

    private static void PlaceWebs(Ctx x)
    {
        int w = x.room.width, h = x.room.height;
        for (int r = 1; r < h; r++)
            for (int c = 1; c < w - 1; c++)
            {
                if (Solid(x, c, r) || !Solid(x, c, r - 1) || Reserved(x, c, r)) continue;
                bool wallL = Solid(x, c - 1, r), wallR = Solid(x, c + 1, r);
                if (!wallL && !wallR) continue;
                if (x.rng.NextDouble() > 0.45) continue;

                string key = Pick(x, Webs);
                float bw = Width(key), bh = Height(key);
                float ceilY = RowBottom(x, r) + 1f;
                float cx = wallL ? c + bw * 0.5f : c + 1f - bw * 0.5f;
                Rect wr = new Rect(cx - bw * 0.5f, ceilY - bh, bw, bh);
                if (!AreaClear(x, wr, true)) continue;
                if (Place(x, key, x.grimeGroup, cx, ceilY, 1f) != null) x.used.Add(wr);
            }
    }

    // ---- grime on open wall ---------------------------------------------------------------------

    private static void PlaceGrime(Ctx x)
    {
        int want = Mathf.Clamp(x.room.width * x.room.height / 140, 3, 18);
        for (int i = 0; i < want * 12 && want > 0; i++)
        {
            string key = Pick(x, Grime);
            float cx = 1f + (float)x.rng.NextDouble() * (x.room.width - 2);
            float cy = 1f + (float)x.rng.NextDouble() * (x.room.height - 2);
            Rect wr = WorldRect(key, cx, cy, 0.5f);
            if (!AreaClear(x, wr, true)) continue;
            if (Place(x, key, x.grimeGroup, cx, cy, 0.5f) == null) continue;
            x.used.Add(wr);
            want--;
        }
    }

    // ---- light ----------------------------------------------------------------------------------

    // The pack's candles, lamps and chandeliers already carry a 3D Light at the flame, with the
    // colour and range Cainos chose. The URP 2D renderer ignores 3D lights entirely, so those do
    // nothing in this game. Each one becomes a Light2D at the same spot: the tiles and back wall
    // (Sprite-Lit-Default) catch it, so a candle really lights the stone around it.
    //
    // ⚠️ Calibrated by screenshot, not arithmetic (linear colour space, 0.5 global light). The
    // rig's shaders ignore 2D light, so the player is never lit by these; that is expected.
    private static int AddLights(Ctx x)
    {
        var sources = new List<(Light l, GameObject prop)>();
        foreach (var go in x.placed)
        {
            if (go == null) continue;
            foreach (var l in go.GetComponentsInChildren<Light>(true)) sources.Add((l, go));
        }
        // Biggest first, so if the budget runs out the chandeliers keep theirs and a candle doesn't.
        sources.Sort((a, b) => b.l.range.CompareTo(a.l.range));

        int made = 0;
        var litSpots = new List<Vector2>();
        foreach (var (l, prop) in sources)
        {
            if (made >= MaxLights) break;
            Vector2 at = l.transform.position;
            // Two flames on one table are one pool of light.
            bool near = false;
            foreach (var s in litSpots) if ((s - at).sqrMagnitude < 2.5f * 2.5f) { near = true; break; }
            if (near) continue;

            var lgo = new GameObject("Light2D");
            lgo.transform.SetParent(prop.transform, false);
            lgo.transform.position = new Vector3(at.x, at.y, prop.transform.position.z);
            var l2 = lgo.AddComponent<Light2D>();
            l2.lightType = Light2D.LightType.Point;
            l2.color = l.color;
            bool big = l.range >= 4f;
            l2.intensity = big ? 0.85f : 0.7f;
            l2.pointLightInnerRadius = 0.25f;
            l2.pointLightOuterRadius = big ? 4.2f : 2.6f;
            l2.falloffIntensity = 0.7f;
            litSpots.Add(at);
            made++;
        }
        return made;
    }
}
