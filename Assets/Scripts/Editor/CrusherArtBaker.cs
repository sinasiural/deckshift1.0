using UnityEditor;
using UnityEngine;

// Deckshift -> Art -> Bake Crusher Head
//
// The level crusher's ram (Assets/Art/Traps/CrusherHead.png), composed from Cainos pack PIXELS at
// native scale. Nothing is resampled and no colour is chosen by hand: the body's texture is copied
// from the pack's rusted iron beams and the teeth from its steel spike plate; the seams, straps,
// rivets and chain lugs are drawn in colours sampled from those same sprites.
//
// Why it exists: the press it replaces was a 3-tile stone platform sprite scaled 3x to 9 tiles wide,
// with three small spike plates under the middle third. A 3x horizontal pixel smear reads as a
// floating ledge, not as a weight, and only its middle third could hurt anything.
//
// The ram is TWO tiles wide (the Moss Knight's arena keeps its own, separate press). Layout, from
// the bottom up (y = pixel row; row 0 holds the tips of the teeth, which is the sprite's pivot):
//
//   0..10   the steel shoe and its teeth, one continuous plate with 11 teeth, 2px wider than the
//           body on each side
//   11..38  the body: three iron plates, each the textured INSIDE of a pack beam, with straight
//           seams drawn between them (the beams' own outlines are worn and stepped, and stacking
//           them edge to edge left lumps and gaps)
//   39..44  two lugs, one on each strap, where the chains hook in
//
// Re-run after changing anything here; CrusherTrap.prefab references the PNG, so nothing else
// needs rewiring.
public static class CrusherArtBaker
{
    const string DungeonProps = "Assets/Cainos/Pixel Art Platformer - Dungeon/Texture/TX Dungeon Props.png";
    const string VillageProps = "Assets/Cainos/Pixel Art Platformer - Village Props/Texture/TX Village Props.png";
    const string OutDir = "Assets/Art/Traps";
    public const string OutPath = OutDir + "/CrusherHead.png";
    const float PPU = 32f;

    // Odd width, so the ram has a centre COLUMN (34) and the pivot runs down the middle of it.
    public const int Width = 69, Height = 45;
    const int Centre = 34;
    const int BodyL = 2, BodyR = 66;          // inclusive: 65px, a hair over 2 tiles
    const int BodyBottom = 11, BodyTop = 38;
    const int PlateRows = 8;                  // each plate's interior; seams at 20 and 29
    const int StrapHalf = 16;                 // strap centres sit a quarter of the way in

    // Where the chains hook in, in head-local units: the lug's hole, over each strap's centre.
    public const float ChainHookY = 41.5f / PPU;
    public const float ChainX = StrapHalf / PPU;
    // The body's width in units, for the collider.
    public const float BodyWidth = (BodyR - BodyL + 1) / PPU;

    // Sampled from "Beam 01" and "Trap Swinging Blade" (the pack's own iron).
    static readonly Color32 Outline = new Color32(0x40, 0x20, 0x16, 255);
    static readonly Color32 IronLight = new Color32(0x63, 0x5D, 0x59, 255);
    static readonly Color32 IronMid = new Color32(0x53, 0x47, 0x41, 255);
    static readonly Color32 IronDark = new Color32(0x46, 0x34, 0x2C, 255);
    static readonly Color32 Rust = new Color32(0x5C, 0x37, 0x27, 255);
    static readonly Color32 RustLight = new Color32(0x66, 0x41, 0x30, 255);
    static readonly Color32 Hole = new Color32(0x2C, 0x16, 0x10, 255);
    // a rivet's glint: from the steel of "Spike Plate", so it catches light the plates do not
    static readonly Color32 Glint = new Color32(0x77, 0x6F, 0x63, 255);
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    [MenuItem("Deckshift/Art/Bake Crusher Head")]
    public static void Bake()
    {
        var dungeon = LoadPixels(DungeonProps);
        var village = LoadPixels(VillageProps);
        if (dungeon == null || village == null) return;

        RectInt beam1 = SpriteRect(DungeonProps, "TX Dungeon Props - Beam 01");
        RectInt beam2 = SpriteRect(DungeonProps, "TX Dungeon Props - Beam 02");
        RectInt plate = SpriteRect(VillageProps, "TX Village Props - Spike Plate");
        if (beam1.width == 0 || beam2.width == 0 || plate.width == 0) return;

        var buf = new Color32[Width * Height];

        // ---- the shoe: one continuous spike plate, flipped so the teeth point down ----
        // The pack's plate is 33px with 5 teeth on a 6px pitch (tips at columns 4, 10 .. 28). Its two
        // ENDS are kept and its middle is repeated on the same pitch, so the joins are invisible:
        // 11 teeth, tips at 4, 10 .. 64, with the plate's own rounded end at each side.
        for (int x = 0; x < Width; x++)
        {
            int sx = x <= 6 ? x : x >= Width - 7 ? x - (Width - plate.width) : 7 + (x - 7) % 6;
            for (int y = 0; y < plate.height; y++)
            {
                var c = Pixel(village, plate, sx, plate.height - 1 - y);
                if (c.a >= 128) Set(buf, x, y, c);
            }
        }

        // ---- the body: three plates cut from stretches of beam with no joint in them ----
        // (Beam 01's left half has a diagonal joint and Beam 03 a broken end; these windows avoid both.)
        int w = BodyR - BodyL + 1;
        PlateFrom(dungeon, beam2, 63, buf, BodyBottom + 1, w);
        PlateFrom(dungeon, beam1, 60, buf, BodyBottom + 1 + PlateRows + 1, w);
        PlateFrom(dungeon, beam2, 0, buf, BodyBottom + 1 + 2 * (PlateRows + 1), w);

        // straight outlines round the body. The two seams between plates are only DARKENED, not
        // outlined: full-strength seams made three separate planks of it, and it read as a chest of
        // drawers. A lap joint between plates of one casting is what it should be.
        foreach (int y in new[] { BodyBottom, BodyTop })
            for (int x = BodyL; x <= BodyR; x++) Set(buf, x, y, Outline);
        foreach (int y in new[] { BodyBottom + PlateRows + 1, BodyBottom + 2 * (PlateRows + 1) })
            for (int x = BodyL; x <= BodyR; x++)
            {
                Set(buf, x, y, Pixel(dungeon, beam2, 20 + x, 4));
                Tint(buf, x, y, Outline, 0.6f);
            }
        for (int y = BodyBottom; y <= BodyTop; y++) { Set(buf, BodyL, y, Outline); Set(buf, BodyR, y, Outline); }

        // Light comes from the upper left (Salvage law 2): each plate's top row and the left edge
        // catch it, each plate's bottom row and the right edge fall into shadow. This is what makes
        // three strips read as one solid block.
        for (int p = 0; p < 3; p++)
        {
            int bottom = BodyBottom + 1 + p * (PlateRows + 1), top = bottom + PlateRows - 1;
            for (int x = BodyL + 1; x < BodyR; x++)
            {
                Tint(buf, x, top, IronLight, 0.40f);
                Tint(buf, x, bottom, Outline, 0.30f);
            }
        }
        for (int y = BodyBottom + 1; y < BodyTop; y++)
        {
            Tint(buf, BodyL + 1, y, IronLight, 0.40f);
            Tint(buf, BodyR - 1, y, Outline, 0.35f);
        }
        // Rows of rivet heads along the top and bottom edges. Riveted plate is the thing that says
        // IRON rather than wood at this size. One lit pixel, its shadow below and to the right.
        foreach (int ry in new[] { BodyTop - 2, BodyBottom + 2 })
            for (int x = BodyL + 4; x <= BodyR - 4; x += 6)
            {
                if (Mathf.Abs(x - (Centre - StrapHalf)) <= 3 || Mathf.Abs(x - (Centre + StrapHalf)) <= 3) continue;
                Set(buf, x, ry, Glint);
                Tint(buf, x + 1, ry - 1, Outline, 0.7f);
            }

        // chamfered top corners
        Set(buf, BodyL, BodyTop, Clear);
        Set(buf, BodyR, BodyTop, Clear);
        Set(buf, BodyL + 1, BodyTop - 1, Outline);
        Set(buf, BodyR - 1, BodyTop - 1, Outline);

        // ---- straps, rivets and lugs ----
        foreach (int side in new[] { -1, 1 })
        {
            int c = Centre + side * StrapHalf;          // 5px strap: c-2 .. c+2
            for (int y = BodyBottom; y <= BodyTop; y++)
            {
                Set(buf, c - 2, y, Outline);
                Blend(buf, c - 1, y, IronLight, 0.75f);
                Blend(buf, c, y, IronMid, 0.75f);
                Blend(buf, c + 1, y, IronDark, 0.75f);
                Set(buf, c + 2, y, Outline);
            }

            // a rivet on each plate, with a rust streak running down from it
            for (int p = 0; p < 3; p++)
            {
                int ry = BodyBottom + 1 + p * (PlateRows + 1) + PlateRows / 2;
                Set(buf, c, ry, Glint);
                Set(buf, c + 1, ry, IronMid);
                Set(buf, c, ry - 1, IronMid);
                Set(buf, c + 1, ry - 1, Outline);
                Tint(buf, c, ry - 2, Rust, 0.6f);
                Tint(buf, c, ry - 3, Rust, 0.3f);
            }

            // the lug: a ring standing up off the strap, with a hole the chain passes through
            for (int y = BodyTop + 1; y <= BodyTop + 6; y++)
            {
                if (y == BodyTop + 6) { for (int x = c - 1; x <= c + 1; x++) Set(buf, x, y, Outline); continue; }
                bool hole = y == BodyTop + 3 || y == BodyTop + 4;
                Set(buf, c - 2, y, Outline);
                Set(buf, c - 1, y, IronLight);
                Set(buf, c, y, hole ? Hole : IronMid);
                Set(buf, c + 1, y, IronDark);
                Set(buf, c + 2, y, Outline);
            }
        }

        // ---- wear: rust bleeding down the face from the seams, seeded so every bake matches ----
        var rng = new System.Random(20260928);
        for (int n = 0; n < 10; n++)
        {
            int x = rng.Next(BodyL + 3, BodyR - 2);
            if (Mathf.Abs(x - (Centre - StrapHalf)) <= 3 || Mathf.Abs(x - (Centre + StrapHalf)) <= 3) continue;
            int seam = BodyBottom + (1 + rng.Next(3)) * (PlateRows + 1);
            if (seam > BodyTop) seam = BodyTop;
            int len = rng.Next(2, 5);
            for (int k = 1; k <= len; k++)
                Tint(buf, x, seam - k, k == 1 ? RustLight : Rust, 0.55f - 0.1f * k);
        }

        WritePng(buf);
        Debug.Log("CrusherArtBaker: baked " + OutPath + " (" + Width + "x" + Height + ")");
    }

    // One plate: the textured inside of a 10px beam (its rows 1..8), from column sx onward. Where the
    // beam's worn outline steps into those rows, the pixel is taken from the next row inward instead.
    static void PlateFrom(Sheet src, RectInt beam, int sx, Color32[] dst, int dy, int w)
    {
        for (int x = 0; x < w; x++)
            for (int r = 0; r < PlateRows; r++)
            {
                int row = 1 + r;
                var c = Pixel(src, beam, sx + x, row);
                if (IsEdge(c)) c = Pixel(src, beam, sx + x, r < PlateRows / 2 ? row + 1 : row - 1);
                if (IsEdge(c)) c = Pixel(src, beam, sx + x, r < PlateRows / 2 ? row + 2 : row - 2);
                Set(dst, BodyL + x, dy + r, c);
            }
    }

    // The pack's outline is a dark red-brown; its iron fill is lighter and greyer.
    static bool IsEdge(Color32 c) => c.a < 128 || (c.r < 0x48 && c.g < 0x2C);

    // ---------------------------------------------------------------- pixel helpers

    class Sheet { public Color32[] px; public int width; }

    static Sheet LoadPixels(string path)
    {
        if (!System.IO.File.Exists(path)) { Debug.LogError("CrusherArtBaker: missing " + path); return null; }
        // LoadImage always yields a readable copy, so the pack's own import settings are never touched.
        var t = new Texture2D(2, 2);
        t.LoadImage(System.IO.File.ReadAllBytes(path));
        var sheet = new Sheet { px = t.GetPixels32(), width = t.width };
        Object.DestroyImmediate(t);
        return sheet;
    }

    static RectInt SpriteRect(string sheet, string name)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(sheet))
            if (o is Sprite s && s.name == name)
                return new RectInt((int)s.rect.x, (int)s.rect.y, (int)s.rect.width, (int)s.rect.height);
        Debug.LogError("CrusherArtBaker: sprite not found: " + name);
        return new RectInt();
    }

    // A pixel of sprite rect r, at (x, y) inside it (y = 0 is its bottom row).
    static Color32 Pixel(Sheet src, RectInt r, int x, int y)
    {
        x = Mathf.Clamp(x, 0, r.width - 1);
        y = Mathf.Clamp(y, 0, r.height - 1);
        return src.px[(r.y + y) * src.width + (r.x + x)];
    }

    static void Set(Color32[] buf, int x, int y, Color32 c)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        buf[y * Width + x] = c;
    }

    // Moves an existing opaque pixel toward c; leaves transparent pixels alone.
    static void Tint(Color32[] buf, int x, int y, Color32 c, float k)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        var p = buf[y * Width + x];
        if (p.a == 0) return;
        buf[y * Width + x] = Color32.Lerp(p, c, k);
    }

    // Like Tint, but paints c outright where there is nothing yet.
    static void Blend(Color32[] buf, int x, int y, Color32 c, float k)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        var p = buf[y * Width + x];
        buf[y * Width + x] = p.a == 0 ? c : Color32.Lerp(p, c, k);
    }

    static void WritePng(Color32[] buf)
    {
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Art", "Traps");

        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        tex.SetPixels32(buf);
        tex.Apply();
        System.IO.File.WriteAllBytes(OutPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(OutPath, ImportAssetOptions.ForceUpdate);

        var ti = (TextureImporter)AssetImporter.GetAtPath(OutPath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.spritePixelsPerUnit = PPU;
        var st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        st.spriteAlignment = (int)SpriteAlignment.Custom;
        // tips of the teeth, down the middle of the centre column
        st.spritePivot = new Vector2((Centre + 0.5f) / Width, 0f);
        st.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(st);
        ti.SaveAndReimport();
    }
}
