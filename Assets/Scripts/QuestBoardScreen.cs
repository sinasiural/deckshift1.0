using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// The quest board — the contracts pinned up in the hub.
//
// THEME: Bulletin (FlatUI.Bulletin). See that theme for the full rationale; the short version is
// that this is the only screen in the game whose value structure is INVERTED. Everywhere else is a
// dark plate with light text on it. Here the board is dark and the CONTRACTS ARE PALE PAPER pinned
// to it, so the bright and dark areas have swapped places and the screen is unmistakable at a
// glance without borrowing a hue from anything.
//
// Three things carry it, and each is a deliberate inversion of something already in the project:
//   · LIGHT RAKES IN FROM THE LEFT, so every slip throws its shadow to the RIGHT and the brass
//     tacks are shaded to match. No other screen is side-lit.
//   · THE MOTION IS IN THE CONTENT. There is no particle field at all — the slips sway a fraction
//     of a degree ON THEIR PINS, which is why each one's rotation pivot sits exactly under its tack
//     rather than at its centre. That detail is most of why the board reads as paper on a wall.
//   · THE BOARD IS PERFORATED with old pin holes. It is the only wear in the game that says
//     something about the WORLD (other people took contracts here) rather than about the object.
//
// House pattern: entirely procedural, self-instantiating, no prefab and no art files — same shape
// as ScrapForgeScreen and BlompoScreen. It replaced a painted board sprite that the designer
// disliked; QuestSystem no longer owns any UI references at all.
public class QuestBoardScreen : GameScreen
{
    private static QuestBoardScreen instance;

    private static readonly FlatUI.Theme T = FlatUI.Bulletin;

    // The wax sits a little under the accent. The accent is tuned to read as TEXT on pale paper;
    // the same value poured into a 100px blob is a much larger area of saturated red and tips over
    // into looking like a sticker. (Linear colour space — a big flat area of a bright colour always
    // lands heavier on screen than the number suggests.)
    private static readonly Color WAX = new Color(0.596f, 0.135f, 0.125f, 1f);
    private static readonly Color WAX_SPENT = new Color(0.380f, 0.145f, 0.135f, 0.92f);

    // The board WIDENS with the number of contracts pinned to it, so raising QuestSystem.BoardSlots
    // is genuinely a one-number change rather than a layout job. At the current 3 slots this comes
    // out to exactly the 1280 the board was hand-tuned at, so nothing moved.
    private static float BoardW
    {
        get
        {
            int slots = Mathf.Max(1, QuestSystem.BoardSlots);
            return Mathf.Max(1280f, slots * (SLIP_W + SLIP_GAP) + 110f);
        }
    }

    private const float BOARD_H = 740f;

    private const float SLIP_W = 356f;
    private const float SLIP_H = 440f;
    private const float SLIP_GAP = 34f;
    private const float SLIP_CY = 26f;          // centre of the slip row
    private const float SLIP_PIVOT_Y = 0.94f;   // the tack: what the paper swings from

    private const float TITLE_Y = 316f;
    private const float RULE_Y = 272f;
    private const float HINT_Y = -262f;
    private const float LEAVE_Y = -316f;

    private RectTransform board;
    private CanvasGroup group;
    private TMP_FontAsset font;
    private AudioSource audioSource;

    private TextMeshProUGUI takenLabel;
    private TextMeshProUGUI hintLabel;

    private readonly List<Slip> slips = new List<Slip>();
    private float fitScale = 1f;
    // isOpen, and the pause / game-state / HUD / drawer bookkeeping, now live in GameScreen.

    // ⚠️ THE BOARD OWNS ALL THREE OF ITS SOUNDS, and they are a set rather than three picks:
    // BoardOpen is paper and wood, BoardNail adds IRON, BoardDud takes the iron away again. Playing
    // the generic UI pair over the top would put a fourth material on the screen and bury the one
    // relationship the set is built on. See ProcSfx → NOTICE BOARD.
    protected override bool PlaysDefaultOpenCloseSound { get { return false; } }

    // One pinned contract.
    private class Slip
    {
        public QuestData data;
        public RectTransform rt;
        public RectTransform shadow;
        public CanvasGroup cg;
        public Image paper;
        public Image seal;
        public RectTransform sealRT;
        public TextMeshProUGUI status, progressText;
        public Image progressFill;

        public float baseAngle;
        public float swaySpeed, swayPhase, swayAmp;
        public bool hovered;
        public float lift;           // 0..1, eased hover
        public bool interactable;
    }

    // ---- entry points -----------------------------------------------------------------------------

    public static bool IsOpen { get { return instance != null && instance.isOpen; } }

    public static void Toggle()
    {
        if (IsOpen) Close(); else Open();
    }

    public static void Open()
    {
        EnsureInstance();
        if (instance == null || instance.isOpen) return;
        instance.Show();
    }

    public static void Close()
    {
        if (instance != null) instance.Hide();
    }

    // Created on demand rather than from a bootstrap: the board is opened by an interactable in the
    // world, so there is no key to catch and nothing to be listening while it is closed. That also
    // sidesteps the death-restart trap entirely — a fresh scene simply builds a fresh screen the
    // next time somebody walks up to the board.
    private static void EnsureInstance()
    {
        if (instance != null) return;
        Canvas canvas = FindRootCanvas();
        if (canvas == null) { Debug.LogWarning("QuestBoardScreen: no Canvas found in scene."); return; }
        GameObject go = new GameObject("QuestBoardScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<QuestBoardScreen>();
        instance.Build();
    }

    // FindRootCanvas now lives in GameScreen — every screen carried its own copy, and the one screen
    // that did NOT (the character select, which used FindFirstObjectByType<Canvas>) built itself
    // inside a world-space enemy health bar and rendered invisibly.

    // ---- construction -----------------------------------------------------------------------------

    private void Build()
    {
        font = FlatUI.UIFont();
        Stretch(GetComponent<RectTransform>());
        group = gameObject.AddComponent<CanvasGroup>();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // Backdrop. Clicking it leaves — a notice board is something you step away from, and there
        // is nothing here that a stray click could destroy.
        //
        // ⚠️ OPAQUE, unlike the 0.94 it inherited. This screen is opened in the HUB, which is the
        // best-lit room in the game — a shopkeeper, a forge, lit barrels, a torch — and at 0.94 all
        // of it read straight through and competed with the slips. It also broke the one thing this
        // screen is built on: the value inversion only works while there are TWO surfaces, pale
        // paper on dark board, and a busy lit room behind it is a third.
        Image backdrop = AddImage(transform, "Backdrop", null,
                                  new Color(0.016f, 0.013f, 0.011f, 1f), true);
        Stretch(backdrop.rectTransform);
        AddClick(backdrop.gameObject, Hide);

        board = AddPoint(transform, "Board", Vector2.zero, new Vector2(BoardW, BOARD_H));

        BuildBoardSurface();
        BuildHeader();
        BuildFooter();

        gameObject.SetActive(false);
    }

    // The board itself: oiled wood, raked by a lamp somewhere off to the left.
    private void BuildBoardSurface()
    {
        // ⚠️ REAL TIMBER, GENERATED AT WORLD PIXEL SCALE — not a flat plate. This was
        // FlatUI.Panel(10) in T.Surface, a single chamfered colour, and it is the last thing on the
        // screen that was a "panel" rather than an object.
        //
        // ⚠️ NO WALL BEHIND IT. Every other Salvage screen tiles the dungeon masonry; this one
        // deliberately does not (designer, 2026-08-21: "the background, i didnt really like
        // honestly. maybe this does not need a background image. the panel might just be fine for
        // it."). It is also the right call on its own terms — this screen's whole idea is the
        // VALUE INVERSION, pale paper on dark board, and a lit brick wall behind it puts a third
        // mid-value surface into a composition that works precisely because it only has two.
        Image plate = AddImage(board, "Plate",
                               SalvageSurfaces.PlankBoard(Salvage.Tex(BoardW), Salvage.Tex(BOARD_H), 9),
                               Color.white, true);
        plate.type = Image.Type.Simple;

        // The frame: real Beam 01 timbers laid end to end across the top and bottom edges, which is
        // what makes it a MOUNTED notice board rather than a floating rectangle. Beam 01 is 128x10,
        // so at world scale each length is ~309px and the run tiles without stretching anything.
        Sprite beam = Salvage.Prop("Beam 01");
        if (beam != null)
        {
            // ⚠️ THE FRAME IS MASKED TO THE BOARD. Beams are tiled at their NATIVE width so the
            // timber is never stretched (Law 1), which means the last one in each run inevitably
            // straddles the edge — and an unmasked run overhung by up to a full beam length, 309px
            // of rail hanging in the black past the corner. Trimming the loop early instead would
            // leave a gap at the corner, which is worse; clipping keeps the joinery flush.
            RectTransform frameLayer = AddPoint(board, "Frame", Vector2.zero,
                                                new Vector2(BoardW, BOARD_H));
            frameLayer.gameObject.AddComponent<RectMask2D>();

            float bw = Salvage.Px(beam.rect.width);
            float bh = Salvage.Px(beam.rect.height) * 1.6f;
            int count = Mathf.CeilToInt(BoardW / bw) + 1;
            for (int i = 0; i < count; i++)
            {
                float x = -BoardW * 0.5f + bw * (i + 0.5f);
                if (x - bw * 0.5f > BoardW * 0.5f) break;
                for (int edgeIdx = 0; edgeIdx < 2; edgeIdx++)
                {
                    Image b = AddImage(frameLayer, "Frame" + edgeIdx + "_" + i, beam,
                                       SalvageScreen.PropTint, false);
                    b.rectTransform.sizeDelta = new Vector2(bw, bh);
                    b.rectTransform.anchoredPosition =
                        new Vector2(x, edgeIdx == 0 ? BOARD_H * 0.5f - bh * 0.4f
                                                    : -BOARD_H * 0.5f + bh * 0.4f);
                }
            }

            // ⚠️ THE UPRIGHTS ARE WHAT MAKE IT A FRAME. With only the top and bottom rails the board
            // has no left or right edge, so a dark timber field against a dark backdrop has no
            // silhouette at all — it read as the slips floating in front of nothing. Same beam
            // rotated 90°, drawn LAST so the corners lap over the rails like real joinery.
            int vCount = Mathf.CeilToInt(BOARD_H / bw) + 1;
            for (int i = 0; i < vCount; i++)
            {
                float y = -BOARD_H * 0.5f + bw * (i + 0.5f);
                if (y - bw * 0.5f > BOARD_H * 0.5f) break;
                for (int side = 0; side < 2; side++)
                {
                    Image b = AddImage(frameLayer, "Upright" + side + "_" + i, beam,
                                       SalvageScreen.PropTint, false);
                    b.rectTransform.sizeDelta = new Vector2(bw, bh);
                    b.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    b.rectTransform.anchoredPosition =
                        new Vector2(side == 0 ? -BoardW * 0.5f + bh * 0.4f
                                              : BoardW * 0.5f - bh * 0.4f, y);
                }
            }
        }

        // Wood grain: a few long, very faint scores. They live in the margins the slips leave
        // exposed, since a streak crossing the content is the mistake the forge screen already made.
        // ⚠️ These heights are chosen to land in the bands the slips and the text LEAVE EMPTY. A
        // grain line that happens to sit between two pieces of text stops reading as wear and
        // starts reading as a divider rule the layout didn't ask for — which is the same mistake
        // the forge screen's first scuff pass made by running a streak through its title.
        float[] grainY = { 356f, -226f, -352f };
        float[] grainA = { 0.13f, 0.22f, 0.16f };
        for (int i = 0; i < grainY.Length; i++)
        {
            Color g = T.EdgeLight;
            g.a = grainA[i];
            Image line = AddImage(board, "Grain" + i, FlatUI.FadedRule(), g, false);
            line.rectTransform.sizeDelta = new Vector2(BoardW * Random.Range(0.55f, 0.86f), 2f);
            line.rectTransform.anchoredPosition = new Vector2(Random.Range(-120f, 120f), grainY[i]);
        }

        BuildPinHoles();

        // THE LIGHT. The dark falloff on the right does more work than the highlight on the left —
        // a lit edge alone reads as a glow effect, but one side being in shade reads as a direction.
        Color lamp = T.EdgeLight; lamp.a = 0.055f;
        Image lit = AddImage(board, "RakingLight", FlatUI.HorizontalFade(), lamp, false);
        lit.rectTransform.anchorMin = new Vector2(0f, 0f);
        lit.rectTransform.anchorMax = new Vector2(0f, 1f);
        lit.rectTransform.sizeDelta = new Vector2(460f, -26f);
        lit.rectTransform.anchoredPosition = new Vector2(230f, 0f);

        Color shade = new Color(0f, 0f, 0f, 0.24f);
        Image dark = AddImage(board, "FarSide", FlatUI.HorizontalFade(), shade, false);
        dark.rectTransform.anchorMin = new Vector2(1f, 0f);
        dark.rectTransform.anchorMax = new Vector2(1f, 1f);
        dark.rectTransform.sizeDelta = new Vector2(520f, -26f);
        dark.rectTransform.anchoredPosition = new Vector2(-260f, 0f);
        dark.rectTransform.localScale = new Vector3(-1f, 1f, 1f);   // mirror, don't rotate

        Color lip = T.EdgeLight; lip.a = 0.07f;
        Image top = AddImage(board, "TopLip", FlatUI.VerticalFade(), lip, false);
        top.rectTransform.anchorMin = new Vector2(0f, 1f);
        top.rectTransform.anchorMax = new Vector2(1f, 1f);
        top.rectTransform.sizeDelta = new Vector2(-26f, 40f);
        top.rectTransform.anchoredPosition = new Vector2(0f, -20f);
    }

    // Every contract taken here before this one. Scattered across the whole board, so the ones that
    // land under a slip are simply hidden — which is exactly what happens to a real board, and gives
    // the visible clusters an uneven spacing no deliberate layout would produce.
    private void BuildPinHoles()
    {
        const int HOLES = 130;
        RectTransform layer = AddPoint(board, "PinHoles", Vector2.zero, new Vector2(BoardW, BOARD_H));

        for (int i = 0; i < HOLES; i++)
        {
            float sz = Random.Range(4.5f, 8.5f);
            Vector2 at = new Vector2(
                Random.Range(-BoardW * 0.5f + 34f, BoardW * 0.5f - 34f),
                Random.Range(-BOARD_H * 0.5f + 34f, BOARD_H * 0.5f - 34f));

            // ⚠️ The LIT RIM is what makes this read, not the dark centre. A dark dot on a surface
            // this dark (0.072) is nearly invisible however far the alpha is pushed — there is
            // simply no room below it. The hole becomes visible because of the pale crescent on the
            // side away from the lamp, which is a value the board has plenty of headroom for.
            // The DARK smudge goes down first and is drawn WIDER than the pip. With only the bright
            // crescent showing, each hole read as a speck of dust or a star — a lone bright dot on a
            // dark ground is a highlight, not a puncture. It takes a dark surround for the pip to
            // read as light catching the far wall of something recessed.
            Image h = AddImage(layer, "Hole", FlatUI.SoftGlow(),
                               new Color(0f, 0f, 0f, Random.Range(0.65f, 0.90f)), false);
            h.rectTransform.sizeDelta = new Vector2(sz * 1.7f, sz * 1.7f);
            h.rectTransform.anchoredPosition = at;

            Image rim = AddImage(layer, "HoleRim", FlatUI.SoftGlow(),
                                 new Color(T.EdgeLight.r, T.EdgeLight.g, T.EdgeLight.b,
                                           Random.Range(0.22f, 0.38f)), false);
            rim.rectTransform.sizeDelta = new Vector2(sz * 0.72f, sz * 0.72f);
            rim.rectTransform.anchoredPosition = at + new Vector2(sz * 0.26f, -sz * 0.20f);
        }
    }

    private void BuildHeader()
    {
        TextMeshProUGUI title = AddText(board, "Title", "CONTRACTS", 40f, T.EdgeLight, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 14f;
        title.rectTransform.sizeDelta = new Vector2(700f, 54f);
        title.rectTransform.anchoredPosition = new Vector2(-BoardW * 0.5f + 56f + 350f, TITLE_Y);

        takenLabel = AddText(board, "Taken", "", 20f, T.TextMuted, TextAlignmentOptions.Right);
        takenLabel.characterSpacing = 8f;
        takenLabel.rectTransform.sizeDelta = new Vector2(460f, 40f);
        takenLabel.rectTransform.anchoredPosition = new Vector2(BoardW * 0.5f - 56f - 230f, TITLE_Y - 2f);

        // Hairlines have to be brighter than the palette suggests or they don't register at all on a
        // dark surface — T.Border, which is the "correct" colour for a rule here, was invisible.
        Color r = T.EdgeLight; r.a = 0.55f;
        Image rule = AddImage(board, "HeaderRule", FlatUI.FadedRule(), r, false);
        rule.rectTransform.sizeDelta = new Vector2(BoardW - 112f, 2f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, RULE_Y);
    }

    private void BuildFooter()
    {
        hintLabel = AddText(board, "Hint", "", 17f, T.TextMuted, TextAlignmentOptions.Center);
        hintLabel.characterSpacing = 6f;
        hintLabel.rectTransform.sizeDelta = new Vector2(BoardW - 140f, 30f);
        hintLabel.rectTransform.anchoredPosition = new Vector2(0f, HINT_Y);

        // LEAVE reads as a nailed-up sign rather than a button: the board has no chrome anywhere
        // else, and a rounded UI button would be the one thing on screen that isn't an object.
        RectTransform btn = AddPoint(board, "Leave", new Vector2(0f, LEAVE_Y), new Vector2(232f, 46f));

        Image bg = AddImage(btn, "Bg", FlatUI.Panel(5), new Color(0f, 0f, 0f, 0.30f), true);
        bg.type = Image.Type.Sliced;
        Stretch(bg.rectTransform);

        Image ol = AddImage(btn, "Outline", FlatUI.Outline(5, 2), T.Border, false);
        ol.type = Image.Type.Sliced;
        Stretch(ol.rectTransform);

        TextMeshProUGUI label = AddText(btn, "Label", "LEAVE", 20f, T.EdgeLight, TextAlignmentOptions.Center);
        label.characterSpacing = 10f;
        Stretch(label.rectTransform);

        AddClick(bg.gameObject, Hide,
                 onEnter: () => { label.color = T.Accent; ol.color = T.Accent; },
                 onExit: () => { label.color = T.EdgeLight; ol.color = T.Border; });
    }

    // ---- contents ---------------------------------------------------------------------------------

    private void Refresh()
    {
        for (int i = 0; i < slips.Count; i++)
            if (slips[i].rt != null) Destroy(slips[i].rt.gameObject);
        slips.Clear();

        QuestSystem qs = QuestSystem.instance;
        if (qs == null) return;

        IReadOnlyList<QuestData> offer = qs.Offer;
        int n = offer.Count;

        if (n == 0)
        {
            takenLabel.text = "";
            hintLabel.text = "THE BOARD IS BARE.";
            return;
        }

        // Centre the row on however many contracts there are, so a two-quest board isn't
        // left-aligned with a hole in it.
        float step = SLIP_W + SLIP_GAP;
        float x0 = -(n - 1) * step * 0.5f;
        for (int i = 0; i < n; i++)
            slips.Add(BuildSlip(offer[i], x0 + i * step, i));

        RefreshHeaderAndHint();
    }

    private void RefreshHeaderAndHint()
    {
        QuestSystem qs = QuestSystem.instance;
        if (qs == null) return;

        int taken = qs.ActiveCount;
        takenLabel.text = taken + " OF " + QuestSystem.MaxActiveQuests + " TAKEN";
        takenLabel.color = taken >= QuestSystem.MaxActiveQuests ? T.Accent : T.TextMuted;

        hintLabel.text = taken >= QuestSystem.MaxActiveQuests
            ? "YOUR HANDS ARE FULL  ·  ESC TO LEAVE"
            : "CLICK A CONTRACT TO TAKE IT  ·  ESC TO LEAVE";
    }

    private Slip BuildSlip(QuestData data, float x, int index)
    {
        Slip s = new Slip();
        s.data = data;

        // A slight, FIXED tilt per position — hand-pinned, not laid out. Randomising it every open
        // would make the same board look different each visit, which reads as instability.
        float[] tilt = { -1.6f, 0.9f, -0.7f, 1.4f, -1.1f };
        s.baseAngle = tilt[index % tilt.Length];

        s.swaySpeed = Random.Range(0.32f, 0.55f);
        s.swayPhase = Random.Range(0f, Mathf.PI * 2f);
        s.swayAmp = Random.Range(0.22f, 0.42f);

        // ⚠️ The pivot sits under the TACK, not at the centre. That is what makes the sway read as
        // paper hanging from a pin instead of a card wobbling in space, and it costs nothing.
        float pivotOffset = SLIP_H * (SLIP_PIVOT_Y - 0.5f);

        // Shadow first, so it sits behind. Light comes from the left, so it falls right and down.
        Image sh = AddImage(board, "SlipShadow", FlatUI.Panel(5), new Color(0f, 0f, 0f, 0.42f), false);
        sh.type = Image.Type.Sliced;
        sh.rectTransform.pivot = new Vector2(0.5f, SLIP_PIVOT_Y);
        sh.rectTransform.sizeDelta = new Vector2(SLIP_W, SLIP_H);
        sh.rectTransform.anchoredPosition = new Vector2(x + 9f, SLIP_CY + pivotOffset - 7f);
        sh.rectTransform.localEulerAngles = new Vector3(0f, 0f, s.baseAngle);
        s.shadow = sh.rectTransform;

        RectTransform rt = AddPoint(board, "Slip_" + data.name, new Vector2(x, SLIP_CY + pivotOffset),
                                    new Vector2(SLIP_W, SLIP_H));
        rt.pivot = new Vector2(0.5f, SLIP_PIVOT_Y);
        rt.localEulerAngles = new Vector3(0f, 0f, s.baseAngle);
        s.rt = rt;
        s.cg = rt.gameObject.AddComponent<CanvasGroup>();

        s.paper = AddImage(rt, "Paper", FlatUI.Panel(5), T.SurfaceRaised, true);
        s.paper.type = Image.Type.Sliced;
        Stretch(s.paper.rectTransform);

        // Paper isn't flat: a touch of light along the top where it curls off the board, and a
        // little grime gathered at the foot.
        Image sheen = AddImage(rt, "Sheen", FlatUI.VerticalFade(), new Color(1f, 1f, 1f, 0.16f), false);
        sheen.rectTransform.anchorMin = new Vector2(0f, 1f);
        sheen.rectTransform.anchorMax = new Vector2(1f, 1f);
        sheen.rectTransform.sizeDelta = new Vector2(-8f, 120f);
        sheen.rectTransform.anchoredPosition = new Vector2(0f, -60f);

        Image grime = AddImage(rt, "Grime", FlatUI.VerticalFade(), new Color(0.35f, 0.28f, 0.20f, 0.16f), false);
        grime.rectTransform.anchorMin = new Vector2(0f, 0f);
        grime.rectTransform.anchorMax = new Vector2(1f, 0f);
        grime.rectTransform.sizeDelta = new Vector2(-8f, 90f);
        grime.rectTransform.anchoredPosition = new Vector2(0f, 45f);
        grime.rectTransform.localScale = new Vector3(1f, -1f, 1f);

        BuildSlipContent(s);

        // The tack goes on last so it sits over everything, and its own little shadow goes under it.
        Image tackShadow = AddImage(rt, "TackShadow", FlatUI.SoftGlow(), new Color(0f, 0f, 0f, 0.40f), false);
        tackShadow.rectTransform.sizeDelta = new Vector2(40f, 40f);
        tackShadow.rectTransform.anchoredPosition = new Vector2(6f, pivotOffset - 5f);

        Image tack = AddImage(rt, "Tack", FlatUI.PinTack(), new Color(0.82f, 0.63f, 0.30f, 1f), false);
        tack.rectTransform.sizeDelta = new Vector2(26f, 26f);
        tack.rectTransform.anchoredPosition = new Vector2(0f, pivotOffset);

        AddClick(s.paper.gameObject,
                 () => TryAccept(s),
                 onEnter: () => s.hovered = true,
                 onExit: () => s.hovered = false);

        ApplyState(s);
        return s;
    }

    private void BuildSlipContent(Slip s)
    {
        RectTransform rt = s.rt;

        // A fold across the sheet — it was carried here in somebody's pocket. Placed off-centre and
        // varied per slip so three of them don't line up into a stripe across the board.
        Color fold = new Color(0.30f, 0.24f, 0.17f, 0.10f);
        Image crease = AddImage(rt, "Fold", FlatUI.FadedRule(), fold, false);
        crease.rectTransform.sizeDelta = new Vector2(SLIP_W - 10f, 2f);
        crease.rectTransform.anchoredPosition = new Vector2(0f, Random.Range(-40f, 40f));

        TextMeshProUGUI kind = AddText(rt, "Kind", ContractKind(s.data.type), 15f, T.TextMuted,
                                       TextAlignmentOptions.Center);
        kind.characterSpacing = 12f;
        kind.rectTransform.sizeDelta = new Vector2(300f, 22f);
        kind.rectTransform.anchoredPosition = new Vector2(0f, 156f);

        TextMeshProUGUI title = AddText(rt, "Title", s.data.questName, 30f, T.TextBright,
                                        TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = true;
        title.fontSizeMin = 20f;
        title.fontSizeMax = 30f;
        title.rectTransform.sizeDelta = new Vector2(300f, 74f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 112f);

        s.status = AddText(rt, "Status", "", 15f, T.Accent, TextAlignmentOptions.Center);
        s.status.fontStyle = FontStyles.Bold;
        s.status.characterSpacing = 10f;
        s.status.rectTransform.sizeDelta = new Vector2(300f, 22f);
        s.status.rectTransform.anchoredPosition = new Vector2(0f, 66f);

        Color r = T.TextMuted; r.a = 0.55f;
        Image rule = AddImage(rt, "Rule", FlatUI.FadedRule(), r, false);
        rule.rectTransform.sizeDelta = new Vector2(250f, 2f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, 46f);

        // ⚠️ Vertically CENTRED in its box, not top-aligned. The box has to be tall enough for a
        // three-line objective, and every quest written so far is one line — top-aligning dumped all
        // of that reserve into a single hole above the progress bar and the slip read as unfinished.
        TextMeshProUGUI desc = AddText(rt, "Desc", s.data.description, 21f, T.TextBody,
                                       TextAlignmentOptions.Center);

        // ⚠️ THE ONE PIECE OF REAL PROSE ON A SLIP, so it takes the PROSE face (see `UIType`). The
        // display face has no lowercase, which rendered every contract as
        // `CLEAR 4 ROOMS IN A ROW WITHOUT PLAYING STAGGER.` — a board of shouting. Sentence case is
        // what makes a slip read as something a person wrote and pinned up, which is the whole
        // Bulletin conceit. The title, tag, payout and progress stay in the display face: they are
        // labels, not sentences.
        TMP_FontAsset prose = UIType.Prose();
        if (prose != null)
        {
            desc.font = prose;

            // ⚠️ A THIN FACE ON A LIGHT GROUND NEEDS DARKER INK THAN THE NUMBER SUGGESTS. `T.TextBody`
            // was picked for the display face, which is heavy enough to hold its value; Pixie's
            // strokes cover far less area, so the identical colour reads visibly weaker. Measured on
            // the slip: paper luminance 0.75, title ink 0.109, this ink 0.189 — it sat nearly twice
            // as light as the title while carrying the sentence you actually have to read. Pulled
            // most of the way toward the title without reaching it, since the title still outranks it.
            desc.color = new Color(0.165f, 0.137f, 0.110f, desc.color.a);
        }

        desc.enableAutoSizing = true;
        desc.fontSizeMin = UIType.SizeFor(TextRole.Caption, true);   // 19 — was 15 in the display face
        desc.fontSizeMax = UIType.SizeFor(TextRole.Body, true);      // 22 — was 21
        desc.rectTransform.sizeDelta = new Vector2(292f, 86f);
        desc.rectTransform.anchoredPosition = new Vector2(0f, -12f);

        // Progress is drawn for UNTAKEN contracts too, showing 0 of the target. It keeps the layout
        // from jumping when one is accepted, and it tells you the size of the job before you agree
        // to it — which the description doesn't always spell out.
        s.progressText = AddText(rt, "Progress", "", 18f, T.TextMuted, TextAlignmentOptions.Center);
        s.progressText.rectTransform.sizeDelta = new Vector2(260f, 26f);
        s.progressText.rectTransform.anchoredPosition = new Vector2(0f, -78f);

        Image track = AddImage(rt, "Track", FlatUI.Panel(2), new Color(0.42f, 0.37f, 0.30f, 0.45f), false);
        track.type = Image.Type.Sliced;
        track.rectTransform.sizeDelta = new Vector2(258f, 7f);
        track.rectTransform.anchoredPosition = new Vector2(0f, -100f);

        // Left-anchored so a fill of 0 has no width at all rather than collapsing about its centre.
        s.progressFill = AddImage(track.rectTransform, "Fill", FlatUI.Panel(2), T.TextBright, false);
        s.progressFill.type = Image.Type.Sliced;
        s.progressFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        s.progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        s.progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        s.progressFill.rectTransform.anchoredPosition = Vector2.zero;
        s.progressFill.rectTransform.sizeDelta = new Vector2(0f, 0f);

        // The payout, printed as an INK BLOCK. On a screen made of paper the strongest emphasis
        // available is a solid stamp of ink — and it needs no second colour, which keeps red
        // meaning exactly one thing here.
        RectTransform pay = AddPoint(rt, "Payment", new Vector2(0f, -168f), new Vector2(304f, 62f));
        Image payBg = AddImage(pay, "Bg", FlatUI.Panel(4), new Color(0.106f, 0.086f, 0.067f, 0.94f), false);
        payBg.type = Image.Type.Sliced;
        Stretch(payBg.rectTransform);

        TextMeshProUGUI payLabel = AddText(pay, "Label", "PAYS", 12f,
                                           new Color(0.66f, 0.60f, 0.50f, 1f), TextAlignmentOptions.Center);
        payLabel.characterSpacing = 12f;
        payLabel.rectTransform.sizeDelta = new Vector2(280f, 16f);
        payLabel.rectTransform.anchoredPosition = new Vector2(0f, 17f);

        TextMeshProUGUI payValue = AddText(pay, "Value", RewardLine(s.data), 22f,
                                           T.SurfaceRaised, TextAlignmentOptions.Center);
        payValue.fontStyle = FontStyles.Bold;
        payValue.enableAutoSizing = true;
        payValue.fontSizeMin = 15f;
        payValue.fontSizeMax = 22f;
        payValue.rectTransform.sizeDelta = new Vector2(284f, 30f);
        payValue.rectTransform.anchoredPosition = new Vector2(0f, -8f);

        // The seal, built hidden.
        //
        // It goes in the TOP-RIGHT corner, beside the tack, and that is a placement decision rather
        // than an aesthetic one: the bottom of the sheet is where the payout and the progress bar
        // live, and a 100px wax blob dropped there covers the two numbers the player most needs
        // once a contract is actually taken. The top corner is the only region of the slip that is
        // empty at every content length.
        Image seal = AddImage(rt, "Seal", FlatUI.WaxSeal(), WAX, false);
        seal.rectTransform.sizeDelta = new Vector2(100f, 100f);
        seal.rectTransform.anchoredPosition = new Vector2(110f, 168f);
        seal.rectTransform.localEulerAngles = new Vector3(0f, 0f, -13f);
        seal.gameObject.SetActive(false);
        s.seal = seal;
        s.sealRT = seal.rectTransform;
    }

    // Reflects the quest's live state onto the slip. Called on build and after every accept, so the
    // board is never showing a contract as available that the player is already carrying — which is
    // what the old board did, silently swallowing the click.
    private void ApplyState(Slip s)
    {
        QuestSystem qs = QuestSystem.instance;
        if (qs == null) return;

        QuestSystem.ActiveQuest active = qs.FindActive(s.data);
        int current = active != null ? Mathf.Min(active.currentAmount, s.data.targetAmount) : 0;
        int target = Mathf.Max(1, s.data.targetAmount);

        s.progressText.text = current + " / " + s.data.targetAmount;
        s.progressText.color = active != null ? T.TextBody : T.TextDisabled;
        s.progressFill.rectTransform.sizeDelta =
            new Vector2(258f * Mathf.Clamp01((float)current / target), 0f);

        if (active != null && active.isCompleted)
        {
            s.status.text = "· COMPLETE ·";
            s.status.color = T.TextMuted;
            s.progressFill.color = T.TextMuted;
            s.cg.alpha = 0.72f;
            s.seal.gameObject.SetActive(true);
            s.seal.color = WAX_SPENT;
            s.interactable = false;
        }
        else if (active != null)
        {
            s.status.text = "· ACCEPTED ·";
            s.status.color = T.Accent;
            s.progressFill.color = T.Accent;
            s.cg.alpha = 0.94f;
            s.seal.gameObject.SetActive(true);
            s.seal.color = WAX;
            s.interactable = false;
        }
        else
        {
            s.status.text = "";
            s.progressFill.color = T.TextBright;
            // A contract you have no room for is greyed rather than hidden: knowing what you turned
            // down is part of deciding whether to finish something first.
            bool full = qs.ActiveCount >= QuestSystem.MaxActiveQuests;
            s.cg.alpha = full ? 0.55f : 1f;
            s.seal.gameObject.SetActive(false);
            s.interactable = !full;
        }
    }

    private void PlayBoardDud() { SfxManager.PlayOn(audioSource, ProcSfx.BoardDud, 0.85f); }

    private void TryAccept(Slip s)
    {
        QuestSystem qs = QuestSystem.instance;

        // ⚠️ A refused click used to return in SILENCE, so clicking a contract while the board was
        // full — or one already taken — did nothing at all and looked broken rather than refused.
        //
        // ⚠️ AND IT IS THE BOARD'S OWN REFUSAL, NOT THE GENERIC UI ONE. BoardDud is BoardNail with
        // the iron layer removed — the hammer meets the timber and nothing fastens. That
        // relationship is what makes it read instantly, and a shared beep throws it away.
        if (qs == null || !s.interactable) { PlayBoardDud(); return; }
        if (!qs.AcceptQuest(s.data)) { PlayBoardDud(); return; }

        s.hovered = false;
        s.interactable = false;

        // Keeps its own confirm: a seal pressed into wax is this screen's signature and beats the
        // generic one, exactly like the paper rustle beats the generic open.
        SfxManager.PlayOn(audioSource, ProcSfx.BoardNail, 0.95f);

        StartCoroutine(SealRoutine(s));
    }

    // The accept: a seal PRESSED into the paper.
    //
    // The vocabulary is deliberately the opposite of Blompo's blessing, which converges and orbits
    // and never touches anything. This is a physical impact — it comes down, it squashes, the paper
    // recoils and settles. Same reasoning as the forge/arcane split: the motion should say what the
    // material is.
    private IEnumerator SealRoutine(Slip s)
    {
        s.status.text = "· ACCEPTED ·";
        s.status.color = T.Accent;

        s.seal.gameObject.SetActive(true);
        s.seal.color = WAX;

        const float DROP = 0.16f;
        float t = 0f;
        while (t < DROP)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / DROP);
            float k = Mathf.Lerp(2.4f, 1f, n * n);            // accelerating downward
            s.sealRT.localScale = new Vector3(k, k, 1f);
            s.seal.color = new Color(WAX.r, WAX.g, WAX.b, n);
            yield return null;
        }

        // Contact: the wax spreads sideways for an instant, and the whole slip takes the blow.
        const float SQUASH = 0.20f;
        t = 0f;
        while (t < SQUASH)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / SQUASH);
            float e = 1f - (1f - n) * (1f - n);
            s.sealRT.localScale = new Vector3(Mathf.Lerp(1.22f, 1f, e), Mathf.Lerp(0.80f, 1f, e), 1f);
            s.rt.localScale = Vector3.one * Mathf.Lerp(0.975f, 1f, e);
            yield return null;
        }
        s.sealRT.localScale = Vector3.one;
        s.rt.localScale = Vector3.one;

        // Every other slip may have just become unavailable (the third acceptance fills the hands),
        // so the whole board is re-read rather than just this one.
        foreach (Slip other in slips) if (other != s) ApplyState(other);
        ApplyState(s);
        RefreshHeaderAndHint();
    }

    // ---- show / hide ------------------------------------------------------------------------------

    private void Show()
    {
        isOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // The pause / game-state / HUD / hand-drawer handover lives in GameScreen — it was twelve
        // identical lines in every screen, and three of its details are load-bearing and non-obvious
        // (the HUD state is recorded rather than assumed, the drawer lock is gated on it, and the
        // previous game state is restored rather than hardcoded to Playing).
        AcquireDisplay();

        Refresh();
        FitScale();

        SfxManager.PlayOn(audioSource, ProcSfx.BoardOpen, 0.75f);

        StopAllCoroutines();
        StartCoroutine(OpenAnim());
    }

    private void Hide()
    {
        if (!isOpen) return;
        isOpen = false;

        ReleaseDisplay();

        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    // The board doesn't zoom in like a menu — it settles, as though it had just been hung. The
    // slips arrive slightly after it and each on its own beat.
    private IEnumerator OpenAnim()
    {
        group.alpha = 0f;
        const float DUR = 0.24f;
        float t = 0f;
        while (t < DUR)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / DUR);
            group.alpha = n;
            float e = 1f - Mathf.Pow(1f - n, 3f);
            board.localScale = Vector3.one * (fitScale * Mathf.Lerp(0.965f, 1f, e));
            board.anchoredPosition = new Vector2(0f, Mathf.Lerp(26f, 0f, e));
            yield return null;
        }
        group.alpha = 1f;
        board.localScale = Vector3.one * fitScale;
        board.anchoredPosition = Vector2.zero;
    }

    // A board too wide for the canvas is SCALED down uniformly, never reflowed: its header, footer
    // and slip row all sit at fixed offsets from the centre, so resizing the plate would leave the
    // contents overlapping. Same choice BlompoScreen and SettingsScreen make, and for the same
    // reason. Never scales above 1 — a small board on a big screen keeps its authored size.
    //
    // A no-op today (1280x740 fits the narrowest supported canvas, 1440x1080 at 4:3). It exists so
    // that raising QuestSystem.BoardSlots, which widens the board, can't quietly push the frame off
    // the side of the screen on anything narrower than 16:9.
    private void FitScale()
    {
        RectTransform canvasRect = transform.parent as RectTransform;
        if (canvasRect == null || board == null) { fitScale = 1f; return; }

        float sx = canvasRect.rect.width / (BoardW + 80f);
        float sy = canvasRect.rect.height / (BOARD_H + 80f);
        fitScale = Mathf.Min(1f, sx, sy);
        board.localScale = Vector3.one * fitScale;
    }

    // ---- life -------------------------------------------------------------------------------------

    private void Update()
    {
        if (!isOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Hide();
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        for (int i = 0; i < slips.Count; i++)
        {
            Slip s = slips[i];
            if (s.rt == null) continue;

            bool wantLift = s.hovered && s.interactable;
            s.lift = Mathf.MoveTowards(s.lift, wantLift ? 1f : 0f, dt * 6f);

            // The draught. A hovered slip is being held, so it stops swinging — which is a clearer
            // "this one is selected" signal than any highlight, because it is the only still thing
            // on the board.
            float sway = Mathf.Sin(now * s.swaySpeed + s.swayPhase) * s.swayAmp * (1f - s.lift);
            float angle = s.baseAngle + sway;

            float pivotOffset = SLIP_H * (SLIP_PIVOT_Y - 0.5f);
            float x = s.rt.anchoredPosition.x;
            float baseY = SLIP_CY + pivotOffset;

            s.rt.localEulerAngles = new Vector3(0f, 0f, angle);
            s.rt.anchoredPosition = new Vector2(x, baseY + s.lift * 10f);
            s.rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.035f, s.lift);

            // The shadow drops further away as the slip lifts off the board.
            if (s.shadow != null)
            {
                s.shadow.localEulerAngles = new Vector3(0f, 0f, angle);
                s.shadow.anchoredPosition = new Vector2(x + 9f + s.lift * 8f,
                                                        baseY - 7f + s.lift * 2f);
                s.shadow.localScale = Vector3.one * Mathf.Lerp(1f, 1.045f, s.lift);
            }
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ---- text -------------------------------------------------------------------------------------

    // A short caption naming the KIND of job, so a slip is identifiable before its title is read.
    private static string ContractKind(QuestType t)
    {
        switch (t)
        {
            case QuestType.KillEnemy: return "BOUNTY";
            case QuestType.AirKill: return "TRICK SHOT";
            case QuestType.NoDamageRoom: return "TRIAL";
            case QuestType.GoldAccumulate: return "HAUL";
            case QuestType.UseCardCount: return "DRILL";

            // The four streak contracts all read OATH, and that caption is load-bearing: it is the
            // one word telling the player this is a thing they must keep doing rather than a total
            // that only climbs. The progress bar collapsing on a break confirms it afterwards.
            case QuestType.NoCardsRoom:
            case QuestType.NoRecallRoom:
            case QuestType.LowShiftRoom:
            case QuestType.NoStaggerRoom: return "OATH";

            default: return "CONTRACT";
        }
    }

    // ⚠️ Composed from rewardType/rewardAmount, NOT from the `rewardText` string. The string is
    // hand-written per asset and has already drifted from what the code pays (one quest's text says
    // "+10 Shifts" where QuestSystem calls IncreaseMaxShift, i.e. it raises the MAXIMUM). Deriving
    // the line from the fields the reward is actually paid from means the board cannot lie, which is
    // the same rule the card aim indicator follows.
    private static string RewardLine(QuestData d)
    {
        // A card is the one payout with no amount attached, so it is checked before the
        // "pays nothing" guard — otherwise every card contract would advertise NOTHING.
        if (d.rewardType == RewardType.Card)
            return d.rewardCard != null ? d.rewardCard.cardName.ToUpperInvariant() : "A CARD";

        if (d.rewardAmount <= 0) return "NOTHING";
        switch (d.rewardType)
        {
            case RewardType.Gold: return d.rewardAmount + " GOLD";
            case RewardType.ShiftCharge: return "+" + d.rewardAmount + " MAX SHIFT";
            case RewardType.Heal: return d.rewardAmount + " HEALTH";
            case RewardType.Scrap: return d.rewardAmount + " SCRAP";
            case RewardType.MaxHealth: return "+" + d.rewardAmount + " MAX HP";
            default: return d.rewardAmount.ToString();
        }
    }

    // ---- helpers ----------------------------------------------------------------------------------

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private RectTransform AddPoint(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool raycast)
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

    private TextMeshProUGUI AddText(Transform parent, string name, string text, float size, Color color,
                                    TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private static void AddClick(GameObject go, System.Action onClick,
                                 System.Action onEnter = null, System.Action onExit = null)
    {
        QuestBoardHover h = go.AddComponent<QuestBoardHover>();
        h.onClick = onClick;
        h.onEnter = onEnter;
        h.onExit = onExit;
    }
}

// Pointer relay. The board has no Buttons: a Unity Button brings its own transition/navigation
// behaviour and would fight the sway, which drives the slip's own scale and rotation every frame.
public class QuestBoardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public System.Action onClick;
    public System.Action onEnter;
    public System.Action onExit;

    public void OnPointerEnter(PointerEventData e) { if (onEnter != null) onEnter(); }
    public void OnPointerExit(PointerEventData e) { if (onExit != null) onExit(); }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (onClick != null) onClick();
    }
}
