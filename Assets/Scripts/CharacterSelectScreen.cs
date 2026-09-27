using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The character select — theme **MARQUEE**.
//
// The billing before you go on. One character owns the frame, the rest of the roster stands back in
// the dark, the name is printed across the top at poster size, and the whole screen tears past in
// the character's own colour.
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════
// ⚠️ THIS REPLACES **VIGIL**, WHICH THE DESIGNER REJECTED TWICE. Do not rebuild it.
//
// Vigil was a cold hall of alcoves: the roster stood dormant as statues and one warm lamp travelled
// the row to whoever you were on. It was carefully made and it was wrong for this screen, for a
// reason worth keeping — **its whole vocabulary was DORMANCY.** Stillness, cold, low light, a row
// of equals waiting. That is a fine mood and it is the opposite of what a character select is FOR:
// it is the last beat before the run starts, the one screen in the game that is pure hype rather
// than decision support. A screen about to launch you should not feel like a mausoleum.
//
// Three things changed, and each is a deliberate inversion of what Vigil did:
//
//   1. **ONE HERO, NOT A ROW OF EQUALS.** Vigil gave every character the same small alcove, which
//      with a roster of two is two little figures in a lot of empty dark. Here the chosen one steps
//      forward and owns the frame at full size; the others recede, shrink and go cold. The choice
//      is legible from the silhouettes alone, before a word is read.
//   2. **VELOCITY, NOT STILLNESS.** Nothing here rests. Streaks tear across the backdrop, the
//      figures spring between positions and overshoot, the name slams in. This is a game whose
//      stated thesis is "movement is a resource" — the select screen may as well say so.
//   3. **FEWER WORDS** (designer, this pass). Vigil printed the title "WHO'S UP?", the name on
//      every alcove AND again below, a trait name, a trait line and a six-word key line. The name
//      appeared twice and the title said nothing the screen did not already say. What is left is
//      the name, the trait, the trait's one sentence, BEGIN and ESC.
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════
// ⚠️ THE THEME CLAIMS NO HUE OF ITS OWN — IT TAKES THE CHARACTER'S. That is the inversion, and it
// is the one axis nothing else in the game uses. Every other screen has ONE fixed accent that
// identifies the PLACE (Iron's orange, Arcane's violet, Apparatus's arc-cyan). This screen is not
// about a place, it is about an identity, so the accent belongs to the character and the entire
// frame cross-fades to their colour when the selection moves. Colour here is the SELECTION SIGNAL,
// not the theme signature.
//
// It also costs nothing from the hue budget, which was the practical problem: orange, violet, tan,
// amber, frost blue, arc-cyan and wax red are all claimed, and the skill's standing instruction
// when they run out is to stop reaching for a colour and invert a different axis instead.
//
// ⚠️ ACCENTS ARE PALETTE-BY-INDEX, NOT A FIELD ON `CharacterData`. A new character must never be
// able to arrive with an unset colour and render black; dropping an asset into Resources/Characters
// is still the whole job of adding one (see CharacterRoster), and this keeps it that way.
public class CharacterSelectScreen : MonoBehaviour
{
    private static CharacterSelectScreen instance;

    public static bool IsOpen { get { return instance != null && instance.isOpen; } }

    /// <summary>
    /// Whether this screen actually has its contents, as opposed to merely existing. See the husk
    /// note in <see cref="Open"/> — the two are not the same thing after a domain reload.
    /// </summary>
    private bool IsBuilt { get { return content != null && figures.Count > 0; } }

    private bool isOpen;
    private System.Action onConfirmed;

    private readonly List<CharacterData> roster = new List<CharacterData>();
    private readonly List<Figure> figures = new List<Figure>();
    private readonly List<RectTransform> deckCells = new List<RectTransform>();
    private readonly List<Streak> streaks = new List<Streak>();
    private int index;
    private int lastShown = -1;
    private int openedFrame = -999;

    private RectTransform content, stage, streakLayer, deckRow, beginBar, ring;
    private CanvasGroup group;
    private Image wall, barA, barB, heroGlow, groundRule, ringImg, beginPlate, beginEdge, beginChevron;
    private Image arrowL, arrowR, flashImg;
    private TextMeshProUGUI nameText, traitName, traitBody, beginText, escText;
    private AudioSource audioSrc;
    private int figureBaseSibling;      // where the figure block starts in Content's child order

    // Live, eased screen state.
    private Color accent = Color.white;      // cross-fades to the selection's colour
    private float nameT = 1f;                // 0 = mid slam, 1 = settled
    private float ringT = 2f;                // >1 = spent
    private float flare;                     // confirm burst
    private float launch;                    // confirm: streaks accelerate away
    private float wash;                      // confirm: the screen whites out and holds for the cut
    private float deckT = 1f;                // deck cards popping in

    // ── palette ────────────────────────────────────────────────────────────────────────────────
    // The only fixed colours are the ground and the ink. Everything with character in it is the
    // ACCENT, which is chosen per-character below.
    private static readonly Color Ground = new Color(0.032f, 0.031f, 0.040f, 1f);
    private static readonly Color Ink = new Color(0.965f, 0.945f, 0.930f, 1f);
    private static readonly Color InkDim = new Color(0.470f, 0.470f, 0.520f, 1f);

    // ⚠️ Ordered for MAXIMUM SEPARATION between neighbours, not for prettiness in isolation. With a
    // roster of two the player only ever compares slot 0 against slot 1, so those two are the
    // furthest apart on the wheel that the remaining budget allows (jade against magenta). Third
    // and fourth continue around rather than filling in between.
    private static readonly Color[] Accents =
    {
        new Color(0.42f, 0.94f, 0.60f, 1f),   // jade
        new Color(1.00f, 0.35f, 0.72f, 1f),   // magenta
        new Color(1.00f, 0.79f, 0.24f, 1f),   // gold
        new Color(0.44f, 0.72f, 1.00f, 1f),   // ice
    };

    private static Color AccentFor(int i)
    {
        return Accents[((i % Accents.Length) + Accents.Length) % Accents.Length];
    }

    // ── layout ─────────────────────────────────────────────────────────────────────────────────
    // ⚠️ NOTHING HERE IS SCALED TO FIT, and that is deliberate. Every CanvasScaler in the project
    // matches on HEIGHT, so the canvas is ALWAYS 1080 tall and only its width flexes (1440 at 4:3
    // through 2560 at 21:9). Vertical layout is therefore fixed and safe, and the widest thing on
    // screen is ~1150px — comfortably inside even a 4:3 canvas. A uniform fit-scale would only
    // resample the portraits at a fractional factor for no benefit; the portrait textures are
    // Point-filtered pixel art and look best drawn at exactly 1:1.
    private const int FIG_W = 420, FIG_H = 614;
    private const float FEET_Y = -108f;       // the line the hero stands on
    private const float SIDE_X = 432f;        // first flanking slot
    private const float SIDE_STEP = 250f;     // and each one beyond it
    private const float SIDE_SCALE = 0.5f;    // exact half: a clean nearest-neighbour downscale
    private const float SIDE_LIFT = 16f;      // flankers stand slightly higher = further away

    private const float NAME_Y = 386f;
    private const float TRAIT_Y = -176f;
    private const float BODY_Y = -226f;
    private const float DECK_Y = -318f;
    private const float BEGIN_Y = -434f;

    private class Figure
    {
        public CharacterData data;
        public CharacterStagePortrait portrait;
        public RectTransform root;
        public RawImage img;
        public Image shadow;
        public float x, xv;                  // spring position
        public float scale, sv;              // spring scale
        public float lit;                    // 0..1 eased
    }

    private class Streak
    {
        public RectTransform rt;
        public Image img;
        public float x, y, speed, len, alpha;
    }

    // Real game art. ⚠️ The asset is still called VigilArt for the theme this screen used to be —
    // renaming a ScriptableObject class and its Resources asset together is a live risk to a file
    // the screen loads by name, and it buys nothing the player can see. Only two of its slots are
    // used now: the tiled wall and the floor strip. Every read is guarded; a missing asset costs
    // texture, never function.
    private VigilArt art;
    private static Sprite wallSprite;

    // ══════════════════════════════════════════════════════════════════════ open / close

    public static void Open(System.Action onConfirmed)
    {
        // ⚠️ ADOPT AN EXISTING SCREEN BEFORE BUILDING A NEW ONE. `instance` is a static and the
        // screen is a scene object, so anything that clears statics without destroying scene
        // objects — an editor domain reload is the everyday one — leaves the field null while the
        // old screen is still sitting in the Canvas running its Update. Building on top of that
        // stacks a second screen, a second copy of the roster, and a live camera per character.
        // Measured while testing this pass: THREE screens and SIX portrait plots.
        //
        // This is the same failure shape as the self-bootstrapping managers that vanished after a
        // death-restart (see the `RuntimeInitializeOnLoadMethod` note): the static and the object
        // disagree about whether the thing exists. Re-finding it costs one search per menu visit.
        if (instance == null)
            instance = FindFirstObjectByType<CharacterSelectScreen>(FindObjectsInactive.Include);

        // ⚠️ AND AN ADOPTED SCREEN MAY BE A HUSK — verify before trusting it. An editor domain
        // reload keeps the GameObject AND the component but resets every non-serialized field, so
        // `figures` and `roster` come back EMPTY while `content` and the built children survive.
        // The result renders the old hierarchy while driving none of it: the previous character
        // still standing there with the previous name over them, while the accent — the one value
        // computed from `index` alone — cross-fades to the new pick. That exact half-updated frame
        // is what caught this. `Show()` cannot repair it, because there is nothing left to show.
        if (instance != null && !instance.IsBuilt)
        {
            Destroy(instance.gameObject);
            instance = null;
        }

        if (instance == null)
        {
            // ⚠️ Never `FindFirstObjectByType<Canvas>()`. A gameplay scene carries a world-space
            // Canvas per enemy health bar (18 of them in SampleScene) and this screen once built
            // itself inside one at 0.01 scale — invisible, while `Open` still reported success and
            // armed its callback. `GameScreen` owns the correct lookup; keep using it.
            Canvas canvas = GameScreen.FindRootCanvas();
            if (canvas == null)
            {
                // Never strand the player on a dead menu button. Same rule the run map follows: if
                // the screen cannot be shown, do the thing it was going to do.
                Debug.LogWarning("CharacterSelectScreen: no Canvas — starting the run without a pick.");
                if (onConfirmed != null) onConfirmed();
                return;
            }

            var go = new GameObject("CharacterSelectScreen", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            instance = go.AddComponent<CharacterSelectScreen>();
            instance.Build();
        }
        instance.Show(onConfirmed);
    }

    private void Show(System.Action cb)
    {
        onConfirmed = cb;
        isOpen = true;
        openedFrame = Time.frameCount;
        content.gameObject.SetActive(true);
        SetStagesActive(true);
        transform.SetAsLastSibling();

        // Open on the last character played, so a repeat run is one keypress.
        index = 0;
        for (int i = 0; i < roster.Count; i++)
            if (roster[i] == CharacterSelection.Chosen) index = i;

        lastShown = -1;
        flare = 0f;
        launch = 0f;
        wash = 0f;
        if (flashImg != null) flashImg.color = new Color(1f, 1f, 1f, 0f);
        ringT = 2f;
        accent = AccentFor(index);

        // Snap the springs to rest, or the first frame plays a slam nobody asked for.
        for (int i = 0; i < figures.Count; i++)
        {
            Figure f = figures[i];
            f.x = SlotX(i - index);
            f.scale = i == index ? 1f : SIDE_SCALE;
            f.xv = f.sv = 0f;
            f.lit = i == index ? 1f : 0f;
        }
        Restack();
        RefreshInfo();
        nameT = 0f;
        deckT = 0f;

        Play(ProcSfx.UIOpen);
        StartCoroutine(FadeIn());
    }

    private void Hide()
    {
        isOpen = false;
        content.gameObject.SetActive(false);
        SetStagesActive(false);
    }

    /// <summary>
    /// The character plots are scene-root objects (see <see cref="CharacterStagePortrait"/>), so
    /// they do not follow this screen's own activation and have to be switched by hand.
    /// </summary>
    private void SetStagesActive(bool active)
    {
        foreach (Figure f in figures)
            if (f.portrait != null) f.portrait.SetStageActive(active);
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 3.2f;
            group.alpha = Mathf.Clamp01(t);
            yield return null;
        }
        group.alpha = 1f;
    }

    // ══════════════════════════════════════════════════════════════════════ build

    private void Build()
    {
        Stretch(GetComponent<RectTransform>());

        content = NewRect(transform, "Content");
        Stretch(content);
        group = content.gameObject.AddComponent<CanvasGroup>();

        art = Resources.Load<VigilArt>("VigilArt");

        BuildBackdrop();

        // The stage holds everything at fixed offsets from screen centre. It is a plain centred
        // rect, never resized and never scaled — see the layout note above.
        stage = NewRect(content, "Stage");
        stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
        stage.sizeDelta = new Vector2(1600f, 1080f);

        BuildGround();
        BuildName();          // before the figures: the headline sits BEHIND them
        BuildFigures();
        BuildRing();
        BuildInfo();
        BuildBegin();
        BuildCorner();

        // The launch wash. Last child of Content so it covers everything, transparent until confirm.
        flashImg = AddImage(content, "Flash", FlatUI.Pixel(), new Color(1f, 1f, 1f, 0f), false);
        Stretch(flashImg.rectTransform);
        flashImg.transform.SetAsLastSibling();
    }

    private void BuildBackdrop()
    {
        Image baseCoat = AddImage(content, "Ground", FlatUI.Pixel(), Ground, false);
        Stretch(baseCoat.rectTransform);

        // ⚠️ THE WALL IS ONE SEAMLESS 8x8 PICTURE, NOT A REPEATING TILE. `TX Tileable - Dungeon
        // Wall` is a 256x256 block drawn to tile as a WHOLE; its 64 sub-sprites are pieces of that
        // picture and tiling any single one repeats a fragment and reads as a checkerboard. So the
        // entire texture is used as one tiled sprite: correct by construction, one Image instead of
        // ~500 cells. (This is the same mistake that made generated rooms never look hand-made.)
        if (art != null && art.wallTexture != null)
        {
            Texture2D tex = art.wallTexture;
            if (wallSprite == null)
                // FullRect: the default Tight mesh needs to read the pixels to trace an outline,
                // and this pack texture is not readable in a build ("Sprite outline generation
                // failed"). A tiled rectangle has no outline to trace anyway.
                wallSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                           new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

            // ⚠️ Measured on screen, not computed. At 0.15 the stone was invisible — the accent and
            // the vignette buried it and the backdrop was a flat void, which threw away the one
            // piece of real game art on the screen. At full value it is a bright grey field that
            // flattens everything. 0.25 is where the masonry is legible and still clearly unlit.
            wall = AddImage(content, "Wall", wallSprite, new Color(0.25f, 0.245f, 0.29f, 1f), false);
            wall.type = Image.Type.Tiled;      // ⚠️ never Simple — Simple stretches one block over the screen
            Stretch(wall.rectTransform);
        }

        // Two raked accent bars — the poster's livery. ⚠️ These are FIXED-SIZE rects that are
        // ROTATED, never stretched strips. Rotating a rect that is anchor-stretched to the canvas
        // swings the whole strip out of the screen; a fixed rect just turns.
        barA = AddImage(content, "BarA", TaperBar(), Color.white, false);
        barA.rectTransform.sizeDelta = new Vector2(300f, 1900f);
        barA.rectTransform.anchoredPosition = new Vector2(-150f, 0f);
        barA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 19f);

        barB = AddImage(content, "BarB", TaperBar(), Color.white, false);
        barB.rectTransform.sizeDelta = new Vector2(150f, 1900f);
        barB.rectTransform.anchoredPosition = new Vector2(330f, 0f);
        barB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 19f);

        // The speed streaks. This screen's particle system, and its whole motion vocabulary — a
        // character select for a game about movement should not sit still.
        streakLayer = NewRect(content, "Streaks");
        Stretch(streakLayer);
        for (int i = 0; i < 30; i++)
        {
            var s = new Streak();
            s.img = AddImage(streakLayer, "Streak" + i, FlatUI.HorizontalFade(), Color.white, false);
            s.rt = s.img.rectTransform;
            s.rt.anchorMin = s.rt.anchorMax = s.rt.pivot = new Vector2(0.5f, 0.5f);
            ResetStreak(s, true);
            streaks.Add(s);
        }

        // Vignette, so the streaks and the wall die into the frame instead of hitting a hard edge.
        // Built BEFORE the stage, so it never dims the content that has to be read.
        //
        // ⚠️ THE SIDE PIECES ARE DELIBERATELY NARROW (220, not the 360 the top and bottom take).
        // They are anchored to the screen edge while the flanking figures sit at a fixed ±432 from
        // CENTRE, so how much of a figure the fade covers depends entirely on canvas width: at 4:3
        // a 360px side fade swallows most of the flanker, and at 21:9 it does not touch them at all.
        // The same screen would then read differently per aspect. 220 clears the figures everywhere.
        EdgeFade("VigTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -260f), false, false);
        EdgeFade("VigBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 300f), false, true);
        EdgeFade("VigLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(220f, 0f), true, false);
        EdgeFade("VigRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-220f, 0f), true, true);
    }

    /// <summary>
    /// One edge of the vignette. ⚠️ Anchored to the EDGE IT DARKENS, never centred at an offset —
    /// the canvas width varies from 1440 to 2560 and a centre-anchored edge piece drifts inward or
    /// off-screen with it.
    /// </summary>
    private void EdgeFade(string name, Vector2 aMin, Vector2 aMax, Vector2 size, bool horizontal, bool mirror)
    {
        Image img = AddImage(content, name,
                             horizontal ? FlatUI.HorizontalFade() : FlatUI.VerticalFade(),
                             new Color(Ground.r, Ground.g, Ground.b, 0.92f), false);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        if (horizontal) { rt.sizeDelta = new Vector2(Mathf.Abs(size.x), 0f); rt.anchoredPosition = new Vector2(size.x * 0.5f, 0f); }
        else            { rt.sizeDelta = new Vector2(0f, Mathf.Abs(size.y)); rt.anchoredPosition = new Vector2(0f, size.y * 0.5f); }

        // Both fade sprites run opaque→clear in one direction only; mirroring is how the opposite
        // edge is served without a second texture.
        if (mirror) rt.localScale = new Vector3(horizontal ? -1f : 1f, horizontal ? 1f : -1f, 1f);
    }

    private void BuildGround()
    {
        // The line the roster stands on. Brightest under the hero and fading out both ways, so it
        // reads as light pooling around them rather than as a drawn rule across the screen.
        groundRule = AddImage(stage, "GroundRule", FlatUI.FadedRule(), Color.white, false);
        groundRule.rectTransform.sizeDelta = new Vector2(1240f, 3f);
        groundRule.rectTransform.anchoredPosition = new Vector2(0f, FEET_Y);

        // A wide, very soft accent wash behind the hero. This is the screen's key light and the
        // reason the hero separates from the wall at all.
        heroGlow = AddImage(stage, "HeroGlow", FlatUI.SoftGlow(), Color.white, false);
        heroGlow.rectTransform.sizeDelta = new Vector2(900f, 780f);
        heroGlow.rectTransform.anchoredPosition = new Vector2(0f, FEET_Y + 250f);
    }

    private void BuildName()
    {
        nameText = AddText(stage, "Name", "", 150f, Ink, TextAlignmentOptions.Center);
        nameText.rectTransform.sizeDelta = new Vector2(1320f, 210f);
        nameText.rectTransform.anchoredPosition = new Vector2(0f, NAME_Y);
        nameText.characterSpacing = 10f;

        // ⚠️ Auto-sized DOWN only. 150 is the design; the floor exists so a future character with a
        // long name shrinks to fit instead of overflowing the frame. Never raise the ceiling to make
        // short names bigger — one name rendering at twice another's reads as broken, not as
        // emphasis (the same rule the card descriptions had to learn).
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 70f;
        nameText.fontSizeMax = 150f;
        nameText.enableWordWrapping = false;
        nameText.overflowMode = TextOverflowModes.Overflow;
    }

    private void BuildFigures()
    {
        roster.Clear();
        foreach (CharacterData c in CharacterRoster.All) if (c != null) roster.Add(c);

        // ⚠️ Sweep orphaned plots first. `Build` runs exactly once per screen and creates every
        // plot itself, so anything already standing out at (3000, -3000) belonged to a screen that
        // is gone — a husk cleared above, whose `OnDestroy` iterated an emptied `figures` list and
        // therefore took none of them with it. Each orphan is a live camera rendering forever.
        foreach (CharacterStagePortrait stray in
                 FindObjectsByType<CharacterStagePortrait>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (stray != null) Destroy(stray.gameObject);

        figureBaseSibling = stage.childCount;

        for (int i = 0; i < roster.Count; i++)
        {
            var f = new Figure { data = roster[i] };

            f.root = NewRect(stage, "Figure_" + f.data.characterName);
            f.root.anchorMin = f.root.anchorMax = new Vector2(0.5f, 0.5f);
            f.root.pivot = new Vector2(0.5f, 0f);
            f.root.sizeDelta = new Vector2(FIG_W, FIG_H);

            // A pool of light at the feet, so a figure is standing on the line rather than hovering
            // over it. Drawn inside the figure's own root, so it scales and travels with them free.
            //
            // ⚠️ IT IS LIGHT, NOT A SHADOW. The first version was a black ellipse — which on a
            // near-black backdrop is invisible by definition, exactly the "a dark mark cannot mark
            // anything on a dark surface" trap. Inverting it also earns its keep twice: it grounds
            // the figure AND it is one more thing carrying the character's colour.
            f.shadow = AddImage(f.root, "Contact", FlatUI.SoftGlow(), new Color(1f, 1f, 1f, 0f), false);
            f.shadow.rectTransform.anchorMin = f.shadow.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            f.shadow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            f.shadow.rectTransform.sizeDelta = new Vector2(300f, 86f);
            f.shadow.rectTransform.anchoredPosition = new Vector2(0f, FIG_H * CharacterStagePortrait.FeetFraction);

            f.portrait = CharacterStagePortrait.Create(f.data, i, FIG_W, FIG_H);
            f.img = AddRaw(f.root, "Rig", f.portrait != null ? f.portrait.Texture : null);
            // ⚠️ A RawImage with NO texture draws a SOLID WHITE QUAD — tinted, that is a large
            // coloured slab where a character should be. A half-authored character must leave an
            // empty slot, never a box.
            if (f.portrait == null) { f.img.enabled = false; f.shadow.enabled = false; }
            f.img.rectTransform.anchorMin = f.img.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            f.img.rectTransform.pivot = new Vector2(0.5f, 0f);
            f.img.rectTransform.anchoredPosition = Vector2.zero;
            f.img.rectTransform.sizeDelta = new Vector2(FIG_W, FIG_H);

            int captured = i;
            var hit = f.root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);      // invisible, but a Button needs a graphic
            hit.raycastTarget = true;
            var btn = f.root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            btn.onClick.AddListener(() =>
            {
                // Clicking whoever is already centre confirms them, so picking and starting is one
                // gesture — the second click of a double-click lands on the same figure.
                if (index == captured) Confirm();
                else Select(captured);
            });

            figures.Add(f);
        }
    }

    private void BuildRing()
    {
        ring = NewRect(stage, "Ring");
        ring.anchorMin = ring.anchorMax = ring.pivot = new Vector2(0.5f, 0.5f);
        ring.sizeDelta = new Vector2(420f, 130f);      // flattened: it is a ring on the FLOOR
        ring.anchoredPosition = new Vector2(0f, FEET_Y);
        ringImg = ring.gameObject.AddComponent<Image>();
        ringImg.sprite = RingSprite();
        ringImg.color = new Color(1f, 1f, 1f, 0f);
        ringImg.raycastTarget = false;
    }

    private void BuildInfo()
    {
        traitName = AddText(stage, "TraitName", "", 30f, Ink, TextAlignmentOptions.Center);
        traitName.rectTransform.sizeDelta = new Vector2(1000f, 42f);
        traitName.rectTransform.anchoredPosition = new Vector2(0f, TRAIT_Y);
        traitName.characterSpacing = 9f;

        // ⚠️ The trait blurb is the one real SENTENCE on this screen, so it takes the prose face.
        // Everything else here — name, trait name, BEGIN, ESC — is a label and stays in display.
        // ⚠️ Lighter than InkDim, because Pixie is a THIN face and covers far less area per glyph
        // than the display face beside it. At the same value it reads washed out — the same lesson
        // the quest slip's body ink had to learn, in the opposite direction (dark ground here).
        traitBody = AddText(stage, "TraitBody", "", 0f, new Color(0.66f, 0.66f, 0.71f, 1f),
                            TextAlignmentOptions.Center);
        UIType.ApplyProse(traitBody, TextRole.Body);
        traitBody.rectTransform.sizeDelta = new Vector2(1000f, 40f);
        traitBody.rectTransform.anchoredPosition = new Vector2(0f, BODY_Y);

        // ⚠️ The starting deck is drawn with the REAL card faces and NO labels. It is the single
        // biggest difference between two characters, and a returning player recognises those cards
        // on sight — a list of names would turn the screen into a spec sheet, which is exactly the
        // kind of words this pass was asked to remove.
        deckRow = NewRect(stage, "Deck");
        deckRow.anchorMin = deckRow.anchorMax = deckRow.pivot = new Vector2(0.5f, 0.5f);
        deckRow.sizeDelta = new Vector2(1000f, 150f);
        deckRow.anchoredPosition = new Vector2(0f, DECK_Y);
    }

    private void BuildBegin()
    {
        beginBar = NewRect(stage, "Begin");
        beginBar.anchorMin = beginBar.anchorMax = beginBar.pivot = new Vector2(0.5f, 0.5f);
        beginBar.sizeDelta = new Vector2(360f, 62f);
        beginBar.anchoredPosition = new Vector2(0f, BEGIN_Y);

        beginPlate = AddImage(beginBar, "Plate", FlatUI.Panel(5), Color.white, true);
        beginPlate.type = Image.Type.Sliced;     // ⚠️ Image.Type defaults to Simple; a 9-slice left
        Stretch(beginPlate.rectTransform);       //     at Simple stretches into a huge soft blob.

        beginEdge = AddImage(beginBar, "Edge", FlatUI.Outline(5, 2), Color.white, false);
        beginEdge.type = Image.Type.Sliced;
        Stretch(beginEdge.rectTransform);

        beginChevron = AddImage(beginBar, "Chevron", ChevronSprite(), Color.white, false);
        beginChevron.rectTransform.sizeDelta = new Vector2(20f, 24f);
        beginChevron.rectTransform.anchoredPosition = new Vector2(-96f, 0f);

        beginText = AddText(beginBar, "Label", "BEGIN", 30f, Ink, TextAlignmentOptions.Center);
        beginText.rectTransform.sizeDelta = new Vector2(320f, 40f);
        beginText.rectTransform.anchoredPosition = new Vector2(12f, 0f);
        beginText.characterSpacing = 12f;

        var btn = beginBar.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = beginPlate;
        btn.onClick.AddListener(Confirm);

        // The only movement hint left, and it is pure symbol — the dim flanking figures already say
        // there is something to move between, so this only has to say "sideways".
        //
        // ⚠️ DRAWN AS SPRITES, NOT TYPED AS "← →". CCBattleScarred is a display face with a small
        // glyph set and no guarantee of carrying arrows; a missing glyph in TMP renders as a blank
        // or a box, and a hint that renders as two boxes is worse than no hint. The chevron is
        // already procedural for the BEGIN bar, so this costs one mirrored copy.
        arrowL = AddImage(stage, "ArrowL", ChevronSprite(), InkDim, false);
        arrowL.rectTransform.sizeDelta = new Vector2(13f, 17f);
        arrowL.rectTransform.anchoredPosition = new Vector2(-26f, BEGIN_Y - 54f);
        arrowL.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        arrowR = AddImage(stage, "ArrowR", ChevronSprite(), InkDim, false);
        arrowR.rectTransform.sizeDelta = new Vector2(13f, 17f);
        arrowR.rectTransform.anchoredPosition = new Vector2(26f, BEGIN_Y - 54f);
    }

    private void BuildCorner()
    {
        // ⚠️ Anchored to the CORNER, not placed at an offset from centre — the canvas width varies
        // by over a thousand pixels between 4:3 and 21:9 and a centred corner label walks with it.
        escText = AddText(content, "Esc", "ESC", 18f,
                          new Color(InkDim.r, InkDim.g, InkDim.b, 0.7f), TextAlignmentOptions.Left);
        RectTransform rt = escText.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(160f, 26f);
        rt.anchoredPosition = new Vector2(44f, 34f);
        escText.characterSpacing = 8f;
    }

    // ══════════════════════════════════════════════════════════════════════ per-frame

    private void Update()
    {
        // Closed AND finished flaring: nothing on screen, nothing to drive. The confirm burst keeps
        // ticking after `isOpen` goes false, which is what lets the screen play its own exit.
        if (!isOpen && flare <= 0f) return;

        // ⚠️ Unscaled throughout. This screen can be reached with the game paused behind it, and on
        // scaled time every animation here would sit frozen at its resting value.
        // ⚠️ And CLAMPED — the springs below integrate explicitly, and one long frame (a domain
        // reload, an editor stall) would otherwise throw a figure off the screen.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        if (isOpen) HandleKeys();

        // ⚠️ THE PANEL IS DERIVED FROM `index`, NOT PUSHED AT IT. `RefreshInfo` self-guards, so
        // calling it every frame is free — and it is the only arrangement in which the name, trait
        // and deck cannot fall out of step with the selection. They already had once: the click
        // handler set `index` directly and never refreshed, so clicking a character moved the
        // highlight onto them while the panel kept describing the previous one.
        RefreshInfo();

        accent = Color.Lerp(accent, AccentFor(index), 1f - Mathf.Exp(-7f * dt));

        if (flare > 0f) flare = Mathf.Max(0f, flare - dt * 1.7f);
        if (launch > 0f) launch = Mathf.Max(0f, launch - dt * 0.5f);
        nameT = Mathf.Min(1f, nameT + dt * 4.2f);
        deckT = Mathf.Min(1f, deckT + dt * 2.6f);
        if (ringT < 2f) ringT += dt * 1.9f;

        TickStreaks(dt);
        TickBackdrop();
        TickFigures(dt);
        TickName();
        TickDeck();
        TickBegin();
    }

    private void TickStreaks(float dt)
    {
        if (streakLayer == null) return;

        // ⚠️ Re-read the layer's width EVERY frame. It is anchor-stretched to the canvas, so it is
        // 1440 wide at 4:3 and 2560 at 21:9 — a width snapshotted at build time would either recycle
        // streaks in the middle of a wide screen or let them run far off a narrow one.
        float halfW = streakLayer.rect.width * 0.5f + 240f;
        float rush = 1f + launch * 7f;

        for (int i = 0; i < streaks.Count; i++)
        {
            Streak s = streaks[i];
            s.x += s.speed * rush * dt;
            if (s.x > halfW) { ResetStreak(s, false); continue; }

            s.rt.anchoredPosition = new Vector2(s.x, s.y);
            s.img.color = new Color(accent.r, accent.g, accent.b, s.alpha * (1f + flare * 2.5f));
        }
    }

    private void ResetStreak(Streak s, bool anywhere)
    {
        float halfW = streakLayer != null && streakLayer.rect.width > 1f
                    ? streakLayer.rect.width * 0.5f : 960f;

        s.len = Random.Range(110f, 380f);
        s.speed = Random.Range(620f, 2100f);
        // ⚠️ Alpha is low on purpose, and lower than the number feels. The project renders in LINEAR
        // colour space, so a small alpha of a bright saturated colour composites far higher in sRGB
        // than the arithmetic suggests — measured elsewhere, 0.065 of arc-cyan came out near 0.36.
        // Thirty of these overlap, so the ceiling here is a third of what a single streak could take.
        s.alpha = Random.Range(0.025f, 0.075f);
        s.y = Random.Range(-540f, 540f);
        s.x = anywhere ? Random.Range(-halfW, halfW) : -halfW - s.len - Random.Range(0f, 700f);

        s.rt.sizeDelta = new Vector2(s.len, Random.Range(2f, 5f));
        // The fade sprite runs opaque→clear left to right; mirrored, the bright HEAD leads the
        // travel and the tail smears behind it, which is what makes it read as speed and not as a
        // floating dash.
        s.rt.localScale = new Vector3(-1f, 1f, 1f);
        s.rt.anchoredPosition = new Vector2(s.x, s.y);
    }

    private void TickBackdrop()
    {
        // The wall is deliberately NOT driven here — it is fixed, cold and unlit, and it is what the
        // accent has to read against. Tinting it with the accent too would leave nothing for the
        // character's colour to stand out from.
        // ⚠️ FAR LOWER THAN THEY LOOK ON PAPER. At 0.055 and 0.035 these two raked bars stopped
        // being livery and became enormous green slabs owning the whole composition — they read as
        // the subject rather than as light behind it, and they flattened the character they crossed.
        // Linear colour space is why: a small alpha of a bright saturated hue over near-black
        // composites much higher in sRGB than the arithmetic says. Measured by screenshot.
        if (barA != null) barA.color = new Color(accent.r, accent.g, accent.b, 0.020f + flare * 0.10f);
        if (barB != null) barB.color = new Color(accent.r, accent.g, accent.b, 0.012f + flare * 0.07f);

        if (heroGlow != null)
        {
            heroGlow.color = new Color(accent.r, accent.g, accent.b, 0.145f + flare * 0.45f);
            heroGlow.rectTransform.localScale = Vector3.one * (1f + flare * 0.35f);
        }
        if (groundRule != null)
            groundRule.color = new Color(accent.r, accent.g, accent.b, 0.55f + flare * 0.45f);

        if (ringImg != null)
        {
            float t = Mathf.Clamp01(ringT);
            float a = ringT >= 1f ? 0f : (1f - t) * (1f - t) * 0.85f;
            ringImg.color = new Color(accent.r, accent.g, accent.b, a);
            ring.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.45f, t);
        }
    }

    private void TickFigures(float dt)
    {
        for (int i = 0; i < figures.Count; i++)
        {
            Figure f = figures[i];
            bool chosen = i == index;

            // ⚠️ A SPRING, NOT A LERP, and that is the whole difference in feel. A lerp glides to a
            // stop; this overshoots and settles, so the chosen character ARRIVES rather than slides.
            // Stiffness and damping are tuned to overshoot once, visibly, and be done in ~0.3s.
            Spring(ref f.x, ref f.xv, SlotX(i - index), 300f, 17f, dt);
            Spring(ref f.scale, ref f.sv, chosen ? 1f : SIDE_SCALE, 260f, 16f, dt);

            f.lit = Mathf.Lerp(f.lit, chosen ? 1f : 0f, 1f - Mathf.Exp(-9f * dt));

            float lift = Mathf.Lerp(SIDE_LIFT, 0f, f.lit);
            f.root.anchoredPosition = new Vector2(f.x, FEET_Y + lift - FIG_H * CharacterStagePortrait.FeetFraction * f.scale);
            f.root.localScale = Vector3.one * f.scale;

            if (f.img != null && f.img.enabled)
            {
                // Unchosen figures go COLD as well as dark. Pure darkening reads as a turned-down
                // brightness slider; the blue shift reads as "out of the light".
                // ⚠️ The unlit floor is 0.42, not the 0.30 that felt right in the editor. Below
                // about 0.4 a flanking character stops reading as "someone standing in the dark"
                // and starts reading as a rendering artefact — and this screen's whole job is to
                // show you the roster, so the ones you did NOT pick still have to be recognisable.
                float k = f.lit * f.lit;
                float v = Mathf.Lerp(0.42f, 1f, k);
                Color tint = new Color(v * 0.86f, v * 0.90f, Mathf.Lerp(0.56f, 1f, k), 1f);
                // On confirm the hero blows out toward their own colour rather than merely
                // brightening — the screen is becoming the character, not turning up a lamp.
                if (chosen && flare > 0f)
                    tint = Color.Lerp(tint, new Color(accent.r + 0.6f, accent.g + 0.6f, accent.b + 0.6f, 1f), flare * 0.55f);
                f.img.color = tint;
            }
            if (f.shadow != null && f.shadow.enabled)
                f.shadow.color = new Color(accent.r, accent.g, accent.b,
                                           Mathf.Lerp(0.04f, 0.26f, f.lit) + flare * 0.4f);

            // Only the chosen one BREATHES; the rest are stopped mid-pose. Motion is the second
            // selection signal and it costs nothing.
            if (f.portrait != null) f.portrait.SetAwake(chosen);
        }
    }

    private void TickName()
    {
        if (nameText == null) return;

        float e = 1f - Mathf.Pow(1f - nameT, 3f);          // ease-out cubic
        nameText.rectTransform.localScale = new Vector3(Mathf.Lerp(1.16f, 1f, e), Mathf.Lerp(1.16f, 1f, e), 1f);
        nameText.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(46f, 0f, e), NAME_Y);
        // ⚠️ The headline has to BRIGHTEN with the confirm burst, not merely sit through it. The
        // burst floods the ground behind it with the accent, so a fixed ink value loses contrast at
        // exactly the moment the screen is loudest and the name — the thing being confirmed — is
        // the first thing to go unreadable.
        float lift = flare * 0.35f;
        nameText.color = new Color(
            Mathf.Lerp(Ink.r, accent.r, 0.30f) + lift,
            Mathf.Lerp(Ink.g, accent.g, 0.30f) + lift,
            Mathf.Lerp(Ink.b, accent.b, 0.30f) + lift,
            Mathf.Lerp(0f, 1f, e));

        if (traitName != null)
            traitName.color = new Color(accent.r, accent.g, accent.b, Mathf.Lerp(0f, 1f, e));
    }

    private void TickDeck()
    {
        float e = 1f - Mathf.Pow(1f - deckT, 3f);
        for (int i = 0; i < deckCells.Count; i++)
        {
            RectTransform c = deckCells[i];
            if (c == null) continue;
            // Staggered, so the deck deals in rather than appearing. 0.13 per card is enough to
            // read as a sequence and short enough that four cards are done before you look down.
            float k = Mathf.Clamp01((e - i * 0.13f) / 0.7f);
            float s = Mathf.Lerp(0.72f, 1f, 1f - Mathf.Pow(1f - k, 3f));
            c.localScale = new Vector3(s, s, 1f);
        }
    }

    private void TickBegin()
    {
        if (beginPlate == null) return;

        // A slow breath, so the one thing you are meant to press is the one thing inviting you.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.6f);

        beginPlate.color = new Color(accent.r, accent.g, accent.b, 0.10f + pulse * 0.06f + flare * 0.5f);
        beginEdge.color = new Color(accent.r, accent.g, accent.b, 0.55f + pulse * 0.25f);
        beginChevron.color = new Color(accent.r, accent.g, accent.b, 0.75f + pulse * 0.25f);
        beginText.color = Color.Lerp(Ink, accent, 0.25f);
        beginBar.localScale = Vector3.one * (1f + pulse * 0.012f + flare * 0.10f);

        Color arrow = new Color(InkDim.r, InkDim.g, InkDim.b, 0.60f);
        if (arrowL != null) arrowL.color = arrow;
        if (arrowR != null) arrowR.color = arrow;
    }

    private static void Spring(ref float cur, ref float vel, float target, float stiff, float damp, float dt)
    {
        vel += (target - cur) * stiff * dt;
        vel *= Mathf.Exp(-damp * dt);
        cur += vel * dt;
    }

    private float SlotX(int offset)
    {
        if (offset == 0) return 0f;
        int n = Mathf.Abs(offset);
        return Mathf.Sign(offset) * (SIDE_X + (n - 1) * SIDE_STEP);
    }

    // ══════════════════════════════════════════════════════════════════════ input

    private void HandleKeys()
    {
        // ⚠️ IGNORE KEYS ON THE FRAME THIS OPENED. The main menu's PLAY button can be activated with
        // Enter or Space, and that same press is still down when this screen's first Update runs —
        // so the screen would confirm the default character instantly and flash past. Exactly the
        // trap PauseScreen guards against with Escape, arriving from the other direction.
        if (Time.frameCount <= openedFrame + 1) return;

        int move = 0;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) move = 1;
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) move = -1;

        if (move != 0) Select(index + move);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space)) Confirm();

        if (Input.GetKeyDown(KeyCode.Escape)) { Play(ProcSfx.UICancel); Hide(); }
    }

    private void Select(int wanted)
    {
        if (figures.Count == 0) return;

        int clamped = Mathf.Clamp(wanted, 0, figures.Count - 1);
        // At either end there is nothing to move to. Say so — silence reads as a dropped input.
        if (clamped == index) { if (wanted != index) Play(ProcSfx.UIRefuse); return; }

        index = clamped;
        nameT = 0f;
        deckT = 0f;
        ringT = 0f;
        Restack();
        Play(ProcSfx.UIMove);
    }

    /// <summary>
    /// Put the chosen figure in front of the others.
    ///
    /// ⚠️ Called only when the selection CHANGES, never per frame. Reordering a transform's children
    /// dirties the canvas, and doing it every frame would rebuild this screen's geometry sixty times
    /// a second for a result that changes about twice a visit.
    ///
    /// ⚠️ Ordered against `figureBaseSibling`, a slot recorded at BUILD time — never against a
    /// figure's live `GetSiblingIndex()`. Reading one figure's index while moving another is a
    /// self-referential measurement: the first move changes the answer the second one depends on.
    /// The whole block is rewritten instead, so the result is the same however many times it runs.
    /// </summary>
    private void Restack()
    {
        if (index < 0 || index >= figures.Count) return;

        int slot = figureBaseSibling;
        for (int i = 0; i < figures.Count; i++)
            if (i != index) figures[i].root.SetSiblingIndex(slot++);
        figures[index].root.SetSiblingIndex(slot);
    }

    private void RefreshInfo()
    {
        if (index == lastShown || index < 0 || index >= roster.Count) return;
        lastShown = index;

        CharacterData c = roster[index];
        nameText.text = c.characterName.ToUpperInvariant();
        traitName.text = c.traitName.ToUpperInvariant();
        traitBody.text = c.traitDescription;

        BuildDeckRow(c);
    }

    private void BuildDeckRow(CharacterData c)
    {
        for (int i = deckRow.childCount - 1; i >= 0; i--)
        {
            GameObject child = deckRow.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        deckCells.Clear();

        const float H = 138f;
        float w = H / CardFace.ASPECT;
        int n = c.startingDeck != null ? c.startingDeck.Count : 0;
        const float gap = 12f;
        float first = -(n - 1) * 0.5f * (w + gap);

        for (int i = 0; i < n; i++)
        {
            CardData card = c.startingDeck[i];
            if (card == null) continue;

            RectTransform cell = NewRect(deckRow, "Card" + i);
            cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(0.5f, 0.5f);
            cell.anchoredPosition = new Vector2(first + i * (w + gap), 0f);
            cell.sizeDelta = new Vector2(w, H);
            CardFace.Build(cell, new RuntimeCard(card));
            deckCells.Add(cell);
        }
    }

    private void Confirm()
    {
        if (!isOpen || index < 0 || index >= roster.Count) return;

        CharacterSelection.Chosen = roster[index];
        flare = 1f;
        launch = 1f;
        Play(ProcSfx.UIConfirm);
        // Layered under the confirm chime: the chime says "taken", the impact says "go". Quiet
        // enough to read as weight behind the click rather than as a second, competing sound.
        Play(ProcSfx.MeteorImpact, 0.45f);
        StartCoroutine(ConfirmRoutine());
    }

    // The accent floods, the streaks tear away, and the run begins.
    //
    // ⚠️ **THE CALLBACK FIRES FIRST, NOT LAST — that is the "it takes too long" fix.** Loading
    // SampleScene is a measured **1.04 seconds**. This routine used to play a 0.42s exit and *then*
    // hand over, so the player waited 1.46s: 0.42 of animation followed by a full second of frozen
    // image, because `SceneManager.LoadScene` is synchronous and nothing renders during it. Starting
    // the load on the first frame of the burst overlaps the two — the exit now plays THROUGH the
    // load (which is async, see `MainMenuController.StartRun`) and the wait drops to the load alone,
    // with something moving the whole time.
    //
    // ⚠️ And the screen washes out to near-white and HOLDS there. The scene swap lands at an
    // unpredictable moment — whenever the load happens to finish — and cutting out of a detailed
    // frame reads as a glitch, where cutting out of a white one is invisible.
    private IEnumerator ConfirmRoutine()
    {
        isOpen = false;

        System.Action cb = onConfirmed;
        onConfirmed = null;
        if (cb != null) cb();

        // ⚠️ The scene swap normally destroys this object mid-hold, so this loop usually never
        // ends — but it MUST be able to. `ScreenGallery` opens the screen with a null callback, and
        // a failed or absent load must not strand the player on a white rectangle forever.
        const float GIVE_UP = 6f;
        float t = 0f;
        while (t < GIVE_UP)
        {
            t += Time.unscaledDeltaTime;
            // Burst first, wash second: the character you just picked is the payoff, so let it blow
            // out for a beat before the screen takes the frame.
            wash = Mathf.Clamp01((t - 0.30f) / 0.32f) * 0.93f;
            if (flashImg != null) flashImg.color = new Color(1f, 1f, 1f, wash);
            if (cb == null && t > 0.75f) break;      // nothing is coming; return to the menu
            yield return null;
        }

        wash = 0f;
        if (flashImg != null) flashImg.color = new Color(1f, 1f, 1f, 0f);
        Hide();
        group.alpha = 1f;
    }

    private void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (audioSrc == null)
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;     // 2D: a UI sound is not in the room
        }
        SfxManager.PlayOn(audioSrc, clip, volume);
    }

    // ══════════════════════════════════════════════════════════════════════ procedural art

    private static Sprite taperBar, ringSprite, chevron;

    // A bar that fades to nothing at BOTH ends, so a raked stripe dies into the frame instead of
    // stopping dead at the rect's edge. VerticalFade only falls off one way, which is why this
    // exists rather than reusing it.
    private static Sprite TaperBar()
    {
        if (taperBar != null) return taperBar;

        const int H = 128;
        var tex = new Texture2D(1, H, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)(H - 1);
            float a = Mathf.Sin(v * Mathf.PI);      // 0 at both ends, 1 in the middle
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();
        taperBar = Sprite.Create(tex, new Rect(0, 0, 1, H), new Vector2(0.5f, 0.5f), 100f);
        return taperBar;
    }

    // The impact ring, drawn flat on the floor. Soft on both sides of a thin band — a hard-edged
    // circle reads as a drawn shape, a soft one as a pressure wave.
    private static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;

        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.20f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

    // A play arrow for the BEGIN bar. One glyph instead of a word — the cheapest possible way to
    // say "forwards" without spending a line of text on it.
    private static Sprite ChevronSprite()
    {
        if (chevron != null) return chevron;

        const int W = 32, H = 40;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float v = Mathf.Abs(y - (H - 1) * 0.5f) / ((H - 1) * 0.5f);   // 0 centre, 1 tip
                float edge = (1f - v) * (W - 1);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(edge - x + 1f)));
            }
        tex.Apply();
        chevron = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        return chevron;
    }

    // ══════════════════════════════════════════════════════════════════════ small builders

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image AddImage(Transform parent, string name, Sprite s, Color c, bool raycast)
    {
        RectTransform rt = NewRect(parent, name);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var img = rt.gameObject.AddComponent<Image>();
        if (s != null) img.sprite = s;
        img.color = c;
        img.raycastTarget = raycast;
        return img;
    }

    private static RawImage AddRaw(Transform parent, string name, Texture tex)
    {
        RectTransform rt = NewRect(parent, name);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI AddText(Transform parent, string name, string text, float size,
                                           Color c, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(parent, name);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.color = c;
        t.alignment = align;
        t.raycastTarget = false;

        // ⚠️ Every label on this screen goes through UIType. Skipping it is exactly how the previous
        // version shipped rendering in TMP's default Liberation Sans while the rest of the game was
        // in the pixel face — an opt-in convention silently drops any screen that forgets it.
        TMP_FontAsset font = UIType.Display();
        if (font != null) t.font = font;
        if (size > 0f) t.fontSize = size;

        return t;
    }

    private void OnDestroy()
    {
        foreach (Figure f in figures)
            if (f.portrait != null) Destroy(f.portrait.gameObject);
        if (instance == this) instance = null;
    }
}
