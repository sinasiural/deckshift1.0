using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The two pieces every Salvage screen is built out of: the dungeon wall behind it, and the board
/// it is written on.
///
/// ⚠️ THIS EXISTS SO THE SCREENS CANNOT DRIFT APART. The whole point of Salvage is that consistency
/// lives in the treatment — one scale, one light direction, one sampled palette — and the fastest way
/// to lose that is for eight screens to each grow their own copy of "tile the wall, put a torch on
/// it". This project has been bitten by hand-kept duplicates repeatedly (the shop's relic pool, the
/// chest's per-tier lists, the three copies of the card-cost rule). One builder, no copies.
///
/// It is deliberately NOT a MonoBehaviour base class. The screens have wildly different lifecycles —
/// PauseScreen self-bootstraps and stays resident, SettingsScreen is opened with a callback — and
/// forcing them into one inheritance chain is a bigger change than this problem needs.
/// </summary>
public static class SalvageScreen
{
    // The wall and everything on it share one tint, so decoration sits IN the masonry rather than on
    // top of it. Pack art is painted at full value and UI does not pass through the scene's
    // 0.5-intensity global light, so this is doing the lighting the world would have done.
    public static readonly Color WallTint = new Color(0.300f, 0.293f, 0.315f, 1f);

    // ⚠️ PROPS ARE BRIGHTER THAN THE WALL, AND THAT IS NOT A PREFERENCE. Wall decoration is part of
    // the backdrop and must stay behind everything; a prop is an OBJECT IN THE ROOM standing in
    // front of it. Dressing the forge at WallTint made the hearth vanish into the masonry — it was
    // there, and you could not see it. Roughly the scene light (0.5), which is what the same prop
    // would render at in a real room.
    public static readonly Color PropTint = new Color(0.470f, 0.455f, 0.450f, 1f);

    /// <summary>
    /// The dungeon's own masonry, tiled at world magnification, dressed with the pack's cracks and
    /// grime and biased by an off-screen torch from the upper left.
    /// </summary>
    public static void BuildWall(Transform parent)
    {
        Sprite wall = Salvage.Wall();
        if (wall == null)
        {
            Debug.LogWarning("SalvageScreen: SalvageArt.wall missing — run Deckshift → Bake Salvage Art.");
            return;
        }

        Image w = Img(parent, "Wall", wall, WallTint);
        w.type = Image.Type.Tiled;      // ⚠️ Simple stretches ONE 256px block across the screen
        Stretch(w.rectTransform);

        // ⚠️ LANDMARKS ARE WHAT STOP A TILED WALL READING AS WALLPAPER. A seamless texture repeated
        // across a screen has nothing to fix the eye on, so it stops being a wall and becomes a fill —
        // and whatever light gradient is left over then reads as an unfinished placeholder. That was
        // the designer's exact verdict on the first pass. Cainos draws cracks, dents, an outfall and
        // 15 grime patches for this job; they are the same art the rooms are dressed in.
        //
        // ⚠️ EVERY PIECE IS ANCHORED TO A SCREEN CORNER. The canvas matches on height, so width flexes
        // from 1440 at 4:3 to 2560 at 21:9 — anything placed by offset from the centre drifts out of
        // its corner as the aspect changes, and these are only visible in the margins to begin with.
        Deco(parent, "Break 01", new Vector2(0f, 0f), new Vector2(210f, 250f), false);
        Deco(parent, "Dent 04", new Vector2(1f, 0f), new Vector2(-190f, 190f), true);
        Deco(parent, "Outfall 01", new Vector2(1f, 1f), new Vector2(-150f, -170f), false);
        Deco(parent, "Dent 01 A", new Vector2(0f, 1f), new Vector2(240f, -120f), false);

        Dirt(parent, 0, new Vector2(0f, 0f), new Vector2(120f, 470f));
        Dirt(parent, 3, new Vector2(0f, 1f), new Vector2(150f, -330f));
        Dirt(parent, 6, new Vector2(1f, 0f), new Vector2(-140f, 430f));
        Dirt(parent, 9, new Vector2(1f, 1f), new Vector2(-260f, -300f));
        Dirt(parent, 12, new Vector2(0f, 0f), new Vector2(330f, 120f));

        // ⚠️ A TORCH BIASES A WALL; IT DOES NOT ERASE IT. The first pass ran amber 0.085 over
        // 1900x1500 against black 0.42 over 2600x1900 — a warm blob on the left falling to featureless
        // black on the right, a vignette so strong the masonry only existed in one band. Linear colour
        // space makes a saturated colour composite far brighter than its alpha suggests; measure by
        // screenshot, never by arithmetic.
        Image glow = Img(parent, "TorchGlow", SalvageSurfaces.Bloom(),
                         new Color(Salvage.Torch.r, Salvage.Torch.g, Salvage.Torch.b, 0.026f));
        glow.rectTransform.sizeDelta = new Vector2(1700f, 1350f);
        glow.rectTransform.anchoredPosition = new Vector2(-560f, 260f);

        Image dark = Img(parent, "FarCorner", SalvageSurfaces.Bloom(), new Color(0f, 0f, 0f, 0.20f));
        dark.rectTransform.sizeDelta = new Vector2(2800f, 2000f);
        dark.rectTransform.anchoredPosition = new Vector2(620f, -400f);
    }

    /// <summary>
    /// A board of planks bound with iron, hung on two chains, with a shadow behind it.
    /// </summary>
    /// <param name="board">the panel itself — animate THIS (drop, swing, lift away)</param>
    /// <param name="printed">parent for content; sits at canvas centre but moves with the board</param>
    public static void BuildBoard(Transform parent, float width, float top, float bottom,
                                  out RectTransform board, out RectTransform printed)
    {
        float h = top - bottom;

        // ⚠️ THE PIVOT IS WHERE IT HANGS FROM. Every bit of this screen's motion is a rotation about
        // that line. Pivoting at the centre makes a swinging board look like a spinning card — the
        // exact failure the quest board's tack pivot exists to avoid.
        board = Point(parent, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        board.pivot = new Vector2(0.5f, 1f);
        board.sizeDelta = new Vector2(width, h);
        board.anchoredPosition = new Vector2(0f, top);

        // ⚠️ Chains first, so they draw BEHIND the planks — a link crossing in front reads as a chain
        // lying on the board rather than one carrying it. They are children of the board so they
        // travel and swing with it, and their tops run off the screen so nothing gives away that the
        // ceiling end is moving too.
        Sprite chain = SalvageSurfaces.Chain(Salvage.Tex(520f));
        for (int i = 0; i < 2; i++)
        {
            Image ch = Img(board, "Chain" + i, chain, Color.white);
            ch.rectTransform.sizeDelta = new Vector2(Salvage.Px(chain.rect.width), Salvage.Px(chain.rect.height));
            // ⚠️ Aligned with the IRON STRAPS, not merely near the corners — a chain bolted to bare
            // planks looks like it would tear straight out. Must match PlankBoard's StrapA/StrapB.
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(i == 0 ? 0.055f : 0.945f, 1f);
            ch.rectTransform.pivot = new Vector2(0.5f, 0f);        // grows upward from the board
            ch.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        }

        Sprite planks = SalvageSurfaces.PlankBoard(Salvage.Tex(width), Salvage.Tex(h));
        Image face = Img(board, "Board", planks, Color.white);
        Stretch(face.rectTransform);

        // What the board casts on whatever is behind it. It is what stops the panel reading as
        // painted onto the backdrop, and it is the cheapest depth cue there is.
        Image shadow = Img(board, "BoardShadow", SalvageSurfaces.Bloom(), new Color(0f, 0f, 0f, 0.45f));
        shadow.rectTransform.anchorMin = Vector2.zero;
        shadow.rectTransform.anchorMax = Vector2.one;
        shadow.rectTransform.offsetMin = new Vector2(-70f, -90f);
        shadow.rectTransform.offsetMax = new Vector2(70f, 40f);
        shadow.transform.SetAsFirstSibling();

        // Content hangs off the board so it moves with the surface it is written on, but is placed in
        // ordinary canvas-centre coordinates so a screen's layout numbers need no adjusting.
        printed = Point(board, "OnBoard", new Vector2(0.5f, 1f), new Vector2(0f, -top), Vector2.zero);
        printed.sizeDelta = Vector2.zero;
    }

    /// <summary>
    /// Re-cut the board to a new height, for screens that size themselves to their content.
    ///
    /// ⚠️ THE PLANKS MUST BE REGENERATED, NOT STRETCHED. The board is a generated texture at world
    /// pixel scale; letting an Image stretch it to a different height smears the grain, thickens the
    /// seams and skews the bolts — the surface stops being 1:1 with the game's pixels, which is the
    /// one thing Law 1 exists to guarantee. `PlankBoard` caches by size, so a height the screen has
    /// used before costs nothing.
    ///
    /// ⚠️ AND THE CONTENT ANCHOR MOVES WITH THE TOP. `printed` is positioned by the board's top edge,
    /// so a resize that forgets it silently shifts every label on the screen.
    /// </summary>
    public static void ResizeBoard(RectTransform board, RectTransform printed,
                                   float width, float top, float bottom)
    {
        float h = top - bottom;
        board.sizeDelta = new Vector2(width, h);
        board.anchoredPosition = new Vector2(0f, top);

        Transform face = board.Find("Board");
        if (face != null)
        {
            Image img = face.GetComponent<Image>();
            if (img != null) img.sprite = SalvageSurfaces.PlankBoard(Salvage.Tex(width), Salvage.Tex(h));
        }

        if (printed != null) printed.anchoredPosition = new Vector2(0f, -top);
    }

    /// <summary>
    /// Place a pack prop at world magnification. The workhorse for dressing a screen as a PLACE.
    ///
    /// ⚠️ ANCHOR IT TO A SCREEN EDGE, not the centre. Props live in the margins beside a panel, and
    /// the canvas matches on height — width flexes 1440 → 2560 — so anything positioned from the
    /// middle slides out of frame or under the panel as the aspect changes.
    /// </summary>
    public static Image Prop(Transform parent, string suffix, Vector2 anchor, Vector2 offset,
                             float scale = 1f, bool flip = false, Color? tint = null)
    {
        Sprite s = Salvage.Prop(suffix);
        if (s == null) return null;

        Image img = Img(parent, "Prop_" + suffix, s, tint ?? PropTint);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = anchor;
        img.rectTransform.sizeDelta = new Vector2(Salvage.Px(s.rect.width) * scale,
                                                  Salvage.Px(s.rect.height) * scale);
        img.rectTransform.anchoredPosition = offset;
        if (flip) img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        return img;
    }

    /// <summary>
    /// A pool of firelight. Two blooms so the flicker has a core and a spill rather than one
    /// pulsing disc, driven by <see cref="FireGlow"/>.
    /// </summary>
    public static FireGlow Fire(Transform parent, Vector2 anchor, Vector2 offset, float size)
    {
        Image spill = Img(parent, "FireSpill", SalvageSurfaces.Bloom(),
                          new Color(Salvage.Torch.r, Salvage.Torch.g, Salvage.Torch.b, 0.055f));
        spill.rectTransform.anchorMin = spill.rectTransform.anchorMax = anchor;
        spill.rectTransform.sizeDelta = new Vector2(size * 2.6f, size * 2.0f);
        spill.rectTransform.anchoredPosition = offset;

        Image core = Img(parent, "FireCore", SalvageSurfaces.Bloom(),
                         new Color(1f, 0.80f, 0.46f, 0.20f));
        core.rectTransform.anchorMin = core.rectTransform.anchorMax = anchor;
        core.rectTransform.sizeDelta = new Vector2(size * 0.85f, size * 0.85f);
        core.rectTransform.anchoredPosition = offset;

        return new FireGlow { spill = spill, core = core, seed = Random.value * 10f };
    }

    /// <summary>
    /// ⚠️ FIRE FLICKERS ON TWO TIMESCALES, AND ONE SINE READS AS A PULSING LAMP. A slow swell plus a
    /// fast irregular jitter is the whole difference between "a fire" and "something animating".
    /// Unscaled, because these screens freeze the game.
    /// </summary>
    public struct FireGlow
    {
        public Image spill, core;
        public float seed;

        public void Tick()
        {
            if (spill == null || core == null) return;
            float t = Time.unscaledTime + seed;

            float slow = Mathf.Sin(t * 1.7f) * 0.5f + Mathf.Sin(t * 0.9f) * 0.3f;
            float fast = Mathf.Sin(t * 11.3f) * 0.12f + Mathf.Sin(t * 17.7f) * 0.08f;
            float k = 1f + slow * 0.18f + fast;

            Color c = spill.color; c.a = 0.055f * k; spill.color = c;
            Color d = core.color; d.a = 0.200f * k; core.color = d;
            core.rectTransform.localScale = Vector3.one * (1f + slow * 0.06f + fast * 0.5f);
        }
    }

    // ---- the hanging motion ----------------------------------------------------------------------

    /// <summary>
    /// A board on chains, integrated as the pendulum it is rather than lerped.
    ///
    /// ⚠️ THE NUMBERS CARRY THE MASS, NOT THE SPRITE. These were tuned down from a cloth sheet's
    /// (K 26, damp 3.1, idle 0.16° at ~2s), which on planks-and-chains read as tinny jitter.
    /// ⚠️ dt is clamped: an explicit integrator plus one very long frame (a domain reload, an editor
    /// stall) throws a spring clean off the screen.
    /// </summary>
    public struct Hang
    {
        public float angle, angleVel, drop, dropVel;

        private const float SwingK = 15f, SwingDamp = 2.5f;
        private const float DropK = 62f, DropDamp = 9.5f;
        private const float MaxStep = 1f / 30f;

        /// <summary>Throw it in from above. Call on open.</summary>
        public void Release(float fromHeight = 620f)
        {
            drop = fromHeight;
            dropVel = 0f;
            angle = Random.Range(0.7f, 1.5f) * (Random.value < 0.5f ? -1f : 1f);
            angleVel = 0f;
        }

        /// <summary>Nudge it, e.g. when the selection moves. Touching a hung thing moves it.</summary>
        public void Knock(float amount = 1.5f) { angleVel += Random.Range(-amount, amount); }

        /// <summary>Advance and apply. Unscaled, because these screens freeze the game.</summary>
        public void Tick(RectTransform board, float restY)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);

            dropVel += (-DropK * drop - DropDamp * dropVel) * dt;
            drop += dropVel * dt;
            if (Mathf.Abs(drop) < 0.05f && Mathf.Abs(dropVel) < 0.05f) { drop = 0f; dropVel = 0f; }

            angleVel += (-SwingK * angle - SwingDamp * angleVel) * dt;
            angle += angleVel * dt;

            // A slow settling drift, forever, so it never goes dead still and stops being a hung
            // object. Deliberately tiny — this sits over frozen gameplay.
            float idle = Mathf.Sin(Time.unscaledTime * 0.42f) * 0.055f
                       + Mathf.Sin(Time.unscaledTime * 0.17f) * 0.040f;

            board.anchoredPosition = new Vector2(0f, restY + drop);
            board.localRotation = Quaternion.Euler(0f, 0f, angle + idle);
        }
    }

    // ---- small builders --------------------------------------------------------------------------

    private static void Deco(Transform parent, string suffix, Vector2 anchor, Vector2 offset, bool flip)
    {
        Sprite s = Salvage.Deco(suffix);
        if (s == null) return;

        Image img = Img(parent, "Deco_" + suffix, s, WallTint);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = anchor;
        img.rectTransform.sizeDelta = new Vector2(Salvage.Px(s.rect.width), Salvage.Px(s.rect.height));
        img.rectTransform.anchoredPosition = offset;
        // Flipping doubles the vocabulary for free and stops two dents reading as a repeat.
        if (flip) img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
    }

    private static void Dirt(Transform parent, int index, Vector2 anchor, Vector2 offset)
    {
        Sprite s = Salvage.Dirt(index);
        if (s == null) return;

        Image img = Img(parent, "Dirt_" + index, s, WallTint);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = anchor;
        img.rectTransform.sizeDelta = new Vector2(Salvage.Px(s.rect.width), Salvage.Px(s.rect.height));
        img.rectTransform.anchoredPosition = offset;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static RectTransform Point(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        Image img = go.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }
}
