using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Deckshift -> Bake Gate Art
//
// "TX Dungeon Props - Gate 01" is NOT a portcullis. It is a stone archway with a pair of solid
// wooden double doors hung inside it. That is the whole reason the old gate looked wrong: it slid
// the ENTIRE archway down into the floor, and masonry does not do that. A double door opens.
//
// To open the leaves without moving the arch, the one sprite has to become four:
//
//   gate01_arch     the masonry only, with the door opening punched out (transparent)
//   gate01_leafL    the left door leaf,  pivoted on its LEFT edge  (its hinge)
//   gate01_leafR    the right door leaf, pivoted on its RIGHT edge (its hinge)
//   gate01_passage  a dark fill of the opening - what you see once the leaves are out of the way
//
// A leaf opens by scaling its X toward its hinge, which is exactly how this pack draws its own
// door animations: "Door Wood 01" runs 37px wide down to 11px at a constant 66px height.
//
// Baked to Assets/Resources/GateArt/ so Gate.cs can Resources.Load them with nothing to wire and
// no serialized reference on any room prefab (GenLevel7/8/9 carry hand edits and must never be
// re-imported - see CLAUDE.md).
//
// Re-run this if the gate art is ever swapped; GateSpriteName below is the single knob.
public static class GateArtBaker
{
    const string PropsTexturePath = "Assets/Cainos/Pixel Art Platformer - Dungeon/Texture/TX Dungeon Props.png";
    const string GateSpriteName = "TX Dungeon Props - Gate 01";
    const string OutDir = "Assets/Resources/GateArt";
    const float PPU = 32f;

    // Wood reads warm (red channel clearly above blue); the masonry is neutral-to-cool. Measured on
    // row 120: stone runs 0.29/0.29/0.30, wood 0.29/0.22/0.17.
    const float WoodWarmth = 0.06f;
    // ...but the stone carries warm HIGHLIGHTS, so a single warm pixel means nothing. Only a run of
    // this many consecutive warm pixels counts as "we have reached the door".
    const int WoodRun = 4;

    [MenuItem("Deckshift/Bake Gate Art")]
    public static void Bake()
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(PropsTexturePath);
        if (imp == null) { Debug.LogError("GateArtBaker: source texture not found at " + PropsTexturePath); return; }

        bool wasReadable = imp.isReadable;
        if (!wasReadable) { imp.isReadable = true; imp.SaveAndReimport(); }

        try
        {
            Sprite src = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(PropsTexturePath))
            {
                var sp = o as Sprite;
                if (sp != null && sp.name == GateSpriteName) src = sp;
            }
            if (src == null) { Debug.LogError("GateArtBaker: sprite not found: " + GateSpriteName); return; }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PropsTexturePath);
            var r = src.rect;
            int w = (int)r.width, h = (int)r.height;
            Color[] px = tex.GetPixels((int)r.x, (int)r.y, w, h);

            int[] L, R;
            FindOpening(px, w, h, out L, out R);

            if (!AssetDatabase.IsValidFolder(OutDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "GateArt");
            }

            // ---- arch: everything EXCEPT the opening ----
            var arch = new Color[w * h];
            for (int t = 0; t < h; t++)
                for (int x = 0; x < w; x++)
                {
                    int i = Idx(x, t, w, h);
                    bool inside = L[t] >= 0 && x >= L[t] && x <= R[t];
                    arch[i] = inside ? Color.clear : px[i];
                }
            WritePng(arch, w, h, OutDir + "/gate01_arch.png", src.pivot.x / w, 0f);

            // ---- passage: the opening, as darkness beyond ----
            // Deliberately not black: a pure black hole reads as a rendering error. This is the
            // gate's own stone pushed far down in value, so it reads as depth rather than a void.
            var passage = new Color[w * h];
            for (int t = 0; t < h; t++)
                for (int x = 0; x < w; x++)
                {
                    int i = Idx(x, t, w, h);
                    bool inside = L[t] >= 0 && x >= L[t] && x <= R[t];
                    if (!inside) { passage[i] = Color.clear; continue; }
                    // a little darker toward the top of the arch, where least light reaches.
                    //
                    // These values look far too light sitting in this file, and that is on purpose.
                    // The gate renders through the scene's 0.5-intensity global Light2D, which
                    // roughly halves them again, and the project is in LINEAR colour space where
                    // dark values composite darker than the arithmetic suggests. A first pass at
                    // 0.085/0.045 measured as a pure black hole on screen, which reads as a
                    // rendering error rather than as depth. See CLAUDE.md, "tint darker than you
                    // think and you'll get a black hole".
                    float depth = 1f - (float)t / h;                  // 0 bottom .. 1 top
                    float v = Mathf.Lerp(0.24f, 0.12f, depth);
                    passage[i] = new Color(v, v * 0.94f, v * 0.86f, 1f);
                }
            WritePng(passage, w, h, OutDir + "/gate01_passage.png", src.pivot.x / w, 0f);

            // ---- the two leaves ----
            // Split down the opening's centreline, measured across the straight section so the
            // seam sits where the art's own centre join is.
            int mid = CentreLine(L, R, h);
            Vector2 offL = BakeLeaf(px, w, h, L, R, true, mid, OutDir + "/gate01_leafL.png", src.pivot.x);
            Vector2 offR = BakeLeaf(px, w, h, L, R, false, mid, OutDir + "/gate01_leafR.png", src.pivot.x);

            AssetDatabase.Refresh();

            // ---- geometry asset: the offsets travel WITH the sprites, never hardcoded in Gate.cs ----
            int oTop = -1, oBot = -1, oL = int.MaxValue, oR = int.MinValue;
            for (int t = 0; t < h; t++)
            {
                if (L[t] < 0) continue;
                if (oTop < 0) oTop = t;
                oBot = t;
                if (L[t] < oL) oL = L[t];
                if (R[t] > oR) oR = R[t];
            }

            string assetPath = OutDir + "/gate01.asset";
            var art = AssetDatabase.LoadAssetAtPath<GateArt>(assetPath);
            bool fresh = art == null;
            if (fresh) art = ScriptableObject.CreateInstance<GateArt>();

            art.arch = AssetDatabase.LoadAssetAtPath<Sprite>(OutDir + "/gate01_arch.png");
            art.passage = AssetDatabase.LoadAssetAtPath<Sprite>(OutDir + "/gate01_passage.png");
            art.leafL = AssetDatabase.LoadAssetAtPath<Sprite>(OutDir + "/gate01_leafL.png");
            art.leafR = AssetDatabase.LoadAssetAtPath<Sprite>(OutDir + "/gate01_leafR.png");
            art.leafLOffset = offL;
            art.leafROffset = offR;
            // rowTop counts DOWN from the top of the sprite; local Y counts UP from the pivot (y=0
            // is the sprite's bottom edge), so the two are inverted through (h - 1 - t).
            art.openingBottom = (h - 1 - oBot) / PPU;
            art.openingTop = (h - 1 - oTop) / PPU;
            art.openingLeft = (oL - src.pivot.x) / PPU;
            art.openingRight = (oR + 1 - src.pivot.x) / PPU;
            art.archWidth = w / PPU;
            art.archHeight = h / PPU;

            if (fresh) AssetDatabase.CreateAsset(art, assetPath);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();

            Debug.Log("GateArtBaker: baked 4 sprites + gate01.asset to " + OutDir
                      + " | centreline x=" + mid
                      + " | opening rows " + oTop + ".." + oBot + " cols " + oL + ".." + oR
                      + " | leafLOffset=" + offL + " leafROffset=" + offR);
        }
        finally
        {
            if (!wasReadable) { imp.isReadable = false; imp.SaveAndReimport(); }
        }
    }

    // px is bottom-up; every row index in this file is measured from the TOP.
    static int Idx(int x, int rowTop, int w, int h) { return (h - 1 - rowTop) * w + x; }

    // Walk inward from the sprite's silhouette, through the masonry, and stop at the door.
    // Then reject outliers, interpolate the gaps and median-filter, so the arch curve comes out
    // smooth instead of following every highlight in the stonework.
    static void FindOpening(Color[] px, int w, int h, out int[] L, out int[] R)
    {
        var rawL = new int[h]; var rawR = new int[h];
        for (int t = 0; t < h; t++)
        {
            rawL[t] = -1; rawR[t] = -1;
            int first = -1, last = -1;
            for (int x = 0; x < w; x++) if (px[Idx(x, t, w, h)].a > 0.5f) { if (first < 0) first = x; last = x; }
            if (first < 0) continue;

            int l = -1;
            for (int x = first; x <= last - (WoodRun - 1); x++) { if (IsWoodRun(px, x, t, w, h, 1)) { l = x; break; } }
            int rr = -1;
            for (int x = last; x >= first + (WoodRun - 1); x--) { if (IsWoodRun(px, x, t, w, h, -1)) { rr = x; break; } }

            // Reject anything that clearly ran into the masonry rather than the door.
            if (l >= 14 && rr <= 102 && rr - l + 1 >= 8) { rawL[t] = l; rawR[t] = rr; }
        }

        // The door is ONE contiguous opening. The crown of the arch is warm-toned stone and gets
        // misread as wood on a handful of rows right at the top, which - left alone - punches a
        // hole through the keystone and hands the leaves a slice of masonry. Keep only the largest
        // vertically-connected block of rows, allowing small internal gaps for the outlier rows
        // that the reject rule above already dropped.
        const int MaxGap = 6;
        int top = -1, bot = -1, bestCount = 0;
        {
            int t2 = 0;
            while (t2 < h)
            {
                if (rawL[t2] < 0) { t2++; continue; }
                int start = t2, last = t2, count = 0;
                while (t2 < h && t2 - last <= MaxGap)
                {
                    if (rawL[t2] >= 0) { last = t2; count++; }
                    t2++;
                }
                if (count > bestCount) { bestCount = count; top = start; bot = last; }
            }
        }
        L = new int[h]; R = new int[h];
        for (int t = 0; t < h; t++) { L[t] = -1; R[t] = -1; }
        if (top < 0) return;
        // anything outside the winning block is masonry, not door
        for (int t = 0; t < h; t++) if (t < top || t > bot) rawL[t] = -1;

        // interpolate rejected rows from their nearest surviving neighbours
        var fl = new int[h]; var fr = new int[h];
        for (int t = top; t <= bot; t++)
        {
            if (rawL[t] >= 0) { fl[t] = rawL[t]; fr[t] = rawR[t]; continue; }
            int a = t, b = t;
            while (a >= top && rawL[a] < 0) a--;
            while (b <= bot && rawL[b] < 0) b++;
            if (a < top) { fl[t] = rawL[b]; fr[t] = rawR[b]; }
            else if (b > bot) { fl[t] = rawL[a]; fr[t] = rawR[a]; }
            else
            {
                float k = (float)(t - a) / (b - a);
                fl[t] = Mathf.RoundToInt(Mathf.Lerp(rawL[a], rawL[b], k));
                fr[t] = Mathf.RoundToInt(Mathf.Lerp(rawR[a], rawR[b], k));
            }
        }

        for (int t = top; t <= bot; t++)
        {
            L[t] = Median(fl, t, top, bot, 3);
            R[t] = Median(fr, t, top, bot, 3);
        }

        // The arch can only ever widen on the way down. Enforcing that kills the last of the
        // wobble in the curve without touching the straight sides below the springline.
        for (int t = top + 1; t <= bot; t++)
        {
            if (L[t] > L[t - 1] && R[t] - L[t] < 74) L[t] = L[t - 1];
            if (R[t] < R[t - 1] && R[t] - L[t] < 74) R[t] = R[t - 1];
        }
    }

    static bool IsWoodRun(Color[] px, int x, int t, int w, int h, int dir)
    {
        for (int k = 0; k < WoodRun; k++)
        {
            int xi = x + dir * k;
            if (xi < 0 || xi >= w) return false;
            var c = px[Idx(xi, t, w, h)];
            if (c.a <= 0.5f || (c.r - c.b) <= WoodWarmth) return false;
        }
        return true;
    }

    static int Median(int[] v, int t, int lo, int hi, int rad)
    {
        var list = new List<int>();
        for (int i = t - rad; i <= t + rad; i++) if (i >= lo && i <= hi) list.Add(v[i]);
        list.Sort();
        return list[list.Count / 2];
    }

    // Centre of the opening across the straight section only - the arch rows would drag it.
    static int CentreLine(int[] L, int[] R, int h)
    {
        var mids = new List<int>();
        for (int t = 0; t < h; t++) if (L[t] >= 0 && R[t] - L[t] + 1 >= 70) mids.Add((L[t] + R[t]) / 2);
        if (mids.Count == 0) return 58;
        mids.Sort();
        return mids[mids.Count / 2];
    }

    // One leaf, cropped to its own bounds, pivoted on its hinge (outer edge) so that scaling X
    // narrows it toward the jamb the way a door swings away from you.
    static Vector2 BakeLeaf(Color[] px, int w, int h, int[] L, int[] R, bool left, int mid, string path, float pivotX)
    {
        int x0 = int.MaxValue, x1 = int.MinValue, t0 = int.MaxValue, t1 = int.MinValue;
        for (int t = 0; t < h; t++)
        {
            if (L[t] < 0) continue;
            int a = left ? L[t] : mid + 1;
            int b = left ? mid : R[t];
            if (b < a) continue;
            if (a < x0) x0 = a;
            if (b > x1) x1 = b;
            if (t < t0) t0 = t;
            if (t > t1) t1 = t;
        }
        if (x0 > x1) { Debug.LogError("GateArtBaker: empty leaf for " + path); return Vector2.zero; }

        int cw = x1 - x0 + 1, ch = t1 - t0 + 1;
        var buf = new Color[cw * ch];
        for (int t = t0; t <= t1; t++)
            for (int x = x0; x <= x1; x++)
            {
                int a = left ? L[t] : mid + 1;
                int b = left ? mid : R[t];
                bool inside = L[t] >= 0 && x >= a && x <= b && x >= L[t] && x <= R[t];
                buf[(ch - 1 - (t - t0)) * cw + (x - x0)] = inside ? px[Idx(x, t, w, h)] : Color.clear;
            }
        // hinge = outer edge. Left leaf pivots on its left, right leaf on its right.
        WritePng(buf, cw, ch, path, left ? 0f : 1f, 0f);

        // Where this crop's PIVOT sits, in local units relative to the arch's pivot. The left leaf
        // pivots on pixel column x0; the right leaf on the far edge of column x1, hence x1 + 1.
        // Y: the crop's bottom row is (h - 1 - t1) pixels above the sprite's bottom, and the arch
        // pivot's y is 0 = that bottom edge.
        var offset = new Vector2((left ? x0 : x1 + 1) - pivotX, h - 1 - t1) / PPU;

        Debug.Log("GateArtBaker: " + System.IO.Path.GetFileNameWithoutExtension(path)
                  + " crop x[" + x0 + ".." + x1 + "] rowTop[" + t0 + ".." + t1 + "] size " + cw + "x" + ch
                  + " offset=" + offset);
        return offset;
    }

    static void WritePng(Color[] buf, int w, int h, string path, float pivotX01, float pivotY01)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(buf);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;          // pixel art
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.spritePixelsPerUnit = PPU;
        var st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        st.spriteAlignment = (int)SpriteAlignment.Custom;
        st.spritePivot = new Vector2(pivotX01, pivotY01);
        ti.SetTextureSettings(st);
        ti.SaveAndReimport();
    }
}
