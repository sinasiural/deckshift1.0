using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

// The pause screen — Escape.
//
// ══ THE HANGING BOARD — built in SALVAGE ═════════════════════════════════════════════════════════
//
// A board of planks bound with iron straps, dropped in on two chains in front of the dungeon wall.
//
// ⚠️ IT WAS A CLOTH SHEET FIRST, AND THE MOTION IS THE PART THAT SURVIVED. The designer's verdict on
// the cloth: "i like the animation that plays when i press it, like the way it comes from the top,
// but i just dont like the panel itself too much. i mean its not bad, but i want something better."
// So the drop, the swing and the lift-away are unchanged — only the SURFACE was replaced. Why cloth
// failed is legible in the screenshots: a canvas sheet is one flat value with soft folds, so there
// is no structure to look at and no hard edge to make it feel built. Planks have seams, grain,
// straps and bolts; wood and iron are the dungeon's core material pair; and text sits far better on
// wood than on cloth. **Do not re-propose the sheet.** (`SalvageSurfaces.Sheet` is kept — it is a
// good cloth and something else may want it.)
//
// What the object is doing, and why it is this one:
//   · Pause is not a place you travel to, so it must not be a panel you opened. It is a thing that
//     DROPS IN FRONT OF YOU and stops the room, then is hauled back up when you carry on.
//   · The exit is the strongest beat: on resume it is LIFTED AWAY rather than faded out. A dissolve
//     says the screen was an image laid over the game; hauling the board up says the game was behind
//     it the whole time.
//   · The sound already fits. ProcSfx.PauseHalt is the one sound in the game defined by being CHOKED
//     — a damper clamping the ring away in 180ms — which is a heavy thing landing, not a chime.
//
// ⚠️ THE CONTENT IS PARENTED TO THE BOARD, so the text moves with the surface it is written on. Same
// lesson as the quest board's slips: the rotation PIVOT carries the metaphor, and here the pivot is
// the line it hangs from. Content that stayed level while the board swung would read as a texture
// behind a window.
//
// It is also the run's STATUS READOUT, which is the real reason it earns the whole frame. Several of
// these numbers — the exhausted count, the next Stagger price — are visible nowhere else in the game.
//
// House pattern: entirely procedural, self-bootstrapping, no prefab and no art files.
//
// ⚠️ THE ROOT STAYS ACTIVE; only its CONTENT child is toggled. Update() has to run to catch the
// Escape that OPENS the screen, and a deactivated GameObject does not get Update.
public class PauseScreen : MonoBehaviour
{
    private static PauseScreen instance;

    // ---- what is BEHIND the board ----------------------------------------------------------------
    //
    //   Wall — the dungeon's own masonry, tiled at world magnification and lit by an off-screen
    //          torch. The screen reads as somewhere in the building.
    //   Game — the frozen gameplay, dimmed to about a quarter, visible past the board's edges. Says
    //          "the world is still there, you have just stopped" — no other screen does this.
    //
    // Kept switchable because the two say genuinely different things and the choice is the
    // designer's; change the const and recompile.
    private enum Ground { Wall, Game }
    private const Ground GROUND = Ground.Wall;

    // ---- layout, in the canvas's 1920x1080 reference space, all centre-anchored -------------------

    // ⚠️ 1400 IS SET BY THE NARROWEST SUPPORTED ASPECT, NOT BY TASTE. Every CanvasScaler here matches
    // on HEIGHT, so the canvas is always 1080 tall and only WIDTH flexes: 1440 at 4:3, 1920 at 16:9,
    // 2560 at 21:9. A sheet wider than 1440 has its hanging edges cut off by the screen at 4:3, and
    // those edges are most of what makes it read as an object rather than a background. Widest
    // content is the menu hit plate at x -580, so 700 of half-width clears it by 120px.
    private const float SHEET_W = 1400f;
    private const float SHEET_TOP = 420f;
    // ⚠️ Sized to the CONTENT, not to the screen. The stat column ends at y -208, so this leaves
    // about 60px of margin below the last row. The first pass ran to -340 and the bottom fifth of the
    // board was bare planks — dead weight that made the panel read as oversized rather than as full.
    private const float SHEET_BOTTOM = -272f;

    private const float TITLE_Y = 318f;
    private const float SUB_Y = 266f;
    private const float TITLE_RULE_Y = 236f;
    private const float HEADER_Y = 194f;
    private const float HEADER_RULE_Y = 176f;
    private const float LIST_TOP = 128f;

    private const float MENU_X = -330f;
    private const float MENU_W = 380f;
    private const float MENU_STEP = 54f;
    private const float CHALK_X = -556f;

    private const float STAT_LABEL_X = 214f;
    private const float STAT_LABEL_W = 200f;
    private const float STAT_VALUE_X = 432f;
    private const float STAT_VALUE_W = 200f;
    private const float STAT_STEP = 36f;
    private const float BAR_X = 318f;
    private const float BAR_W = 110f;
    private const float BAR_VALUE_W = 108f;

    // Below the board, on the dark. Keeps the board.s bottom edge as the last thing ON the board.
    // ⚠️ Not parented to it either — a hint that swung with the panel would be hard to read.
    private const float FOOTER_Y = -336f;

    private class Entry
    {
        public string label;
        public string confirmLabel;
        public System.Action action;
        public TextMeshProUGUI text;
        public bool armed;
        public float armedUntil;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private int selected;

    private RectTransform content, sheet, cloth, chalk;
    private Image underline;
    private RectTransform printed;   // parent for everything drawn ON the board, so it moves with it
    private CanvasGroup group;
    private TMP_FontAsset font;
    private AudioSource audioSource;

    private TextMeshProUGUI subtitle;
    private TextMeshProUGUI vFloor, vGold, vScrap, vRelics, vDeck, vExhaust, vStagger, vRecall;
    private TextMeshProUGUI vHealth, vShift;
    private Image barHealth, barShift;

    private bool isOpen;
    private bool wasUIPaused;
    private float markY, markTargetY;

    // The hanging motion lives in SalvageScreen.Hang so every board in the game swings alike.
    private SalvageScreen.Hang hang;

    private bool subScreenOpen;

    // The right-hand column shows the run's status, or the key list while CONTROLS is selected.
    // `column` is where the row builders put things; it is `printed` except while building a layer.
    private RectTransform statusLayer, controlsLayer, column;
    private int controlsIndex = -1;

    private GameObject cachedHud;
    private bool hudWasActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneBootstrap.Register(Create);
    }

    private static void Create()
    {
        if (instance != null) return;
        if (FindFirstObjectByType<PauseScreen>() != null) return;

        // Gameplay scenes only. The bootstrap re-runs on every scene load, and Escape must not
        // raise a run-status screen over the main menu or the game-over screen.
        if (FindFirstObjectByType<GameManager>() == null) return;

        Canvas canvas = FindRootCanvas();
        if (canvas == null) return;

        GameObject go = new GameObject("PauseScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<PauseScreen>();
        instance.Build();
    }

    private static Canvas FindRootCanvas()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas fallback = null;
        foreach (Canvas c in all)
        {
            if (c == null) continue;
            if (fallback == null) fallback = c;
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        }
        return fallback;
    }

    public static bool IsOpen => instance != null && instance.isOpen;

    // ---- construction ----------------------------------------------------------------------------

    private void Build()
    {
        font = FlatUI.UIFont();

        RectTransform root = GetComponent<RectTransform>();
        Stretch(root);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        content = AddPoint(transform, "Content", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(content);
        group = content.gameObject.AddComponent<CanvasGroup>();

        // ⚠️ Raycast ON so nothing behind can be clicked through, and deliberately NOT a dismiss
        // button — two of these entries are destructive and a click-anywhere-to-close would put them
        // one stray click from being lost.
        //
        // In Sheet mode this is NOT opaque: it dims the frozen game to about a quarter and lets it
        // show past the cloth's edges, because "the world is still there, you have just stopped" is
        // that version's whole premise. The wall modes cover it instead.
        Image backdrop = AddImage(content, "Backdrop", null,
                                  GROUND == Ground.Game ? new Color(0.030f, 0.025f, 0.021f, 0.78f)
                                                        : new Color(0.020f, 0.017f, 0.015f, 1f), true);
        Stretch(backdrop.rectTransform);

        if (GROUND == Ground.Wall) SalvageScreen.BuildWall(content);
        SalvageScreen.BuildBoard(content, SHEET_W, SHEET_TOP, SHEET_BOTTOM, out sheet, out printed);
        column = printed;

        BuildTitle();
        BuildMenu();
        BuildStatus();
        BuildFooter();

        content.gameObject.SetActive(false);
    }


    private void BuildTitle()
    {
        TextMeshProUGUI t = AddText(printed, "Title", "PAUSED", 62f, Salvage.TextBright,
                                    TextAlignmentOptions.Center);
        t.rectTransform.sizeDelta = new Vector2(900f, 76f);
        t.rectTransform.anchoredPosition = new Vector2(0f, TITLE_Y);
        t.characterSpacing = 16f;

        subtitle = AddText(printed, "Subtitle", "", 17f, Salvage.TextMuted, TextAlignmentOptions.Center);
        subtitle.rectTransform.sizeDelta = new Vector2(1100f, 26f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f, SUB_Y);
        subtitle.characterSpacing = 6f;

        // A chalk rule, scored across and fading at the ends. Chalk rather than a drawn line because
        // chalk is already this game's "someone wrote this here" — the exit marker uses the same
        // stroke sprite and the same colour.
        Image rule = AddImage(printed, "TitleRule", Parchment.Stroke(),
                              new Color(Salvage.Chalk.r, Salvage.Chalk.g, Salvage.Chalk.b, 0.30f), false);
        rule.rectTransform.sizeDelta = new Vector2(760f, 3f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, TITLE_RULE_Y);
    }

    private void BuildMenu()
    {
        AddColumnHeader("OPTIONS", MENU_X, MENU_W, TextAlignmentOptions.Left);

        // ⚠️ THE SELECTION IS MARKED IN CHALK, NOT LIT. Two earlier attempts lit the row instead —
        // a translucent accent plate (which needs a hue Salvage does not have to spend) and then a
        // "rubbed brighter" patch of cloth. The rubbed version was the better idea and still failed
        // on screen: a soft bright blob over a soft grey field has nothing to read as an EDGE, so it
        // came out looking like a lens flare or a blur artefact lying across the menu rather than
        // like wear. Chalk has a hard edge, it is already this game's "someone marked this" — the
        // exit arrow in the world is drawn with the same stroke sprite in the same colour — and it
        // costs no colour at all.
        underline = AddImage(printed, "Underline", Parchment.Stroke(), Salvage.Chalk, false);
        underline.rectTransform.sizeDelta = new Vector2(MENU_W - 40f, 3.4f);

        chalk = BuildChalkMark();

        AddEntry("RESUME", null, Close);
        AddEntry("SETTINGS", null, OpenSettings);
        // ⚠️ REPLACED "HOW TO PLAY", which opened the prototype-era TutorialPanel ("For this version,
        // we have 6 rooms…") — wrong about the game and in a superseded style. Selecting CONTROLS
        // swaps the board's right column to the key list; there is nothing to open or close.
        controlsIndex = entries.Count;
        AddEntry("CONTROLS", null, () => hang.Knock(0.8f));
        AddEntry("ABANDON RUN", "ABANDON RUN?  CONFIRM", AbandonRun);
        AddEntry("QUIT TO DESKTOP", "QUIT TO DESKTOP?  CONFIRM", QuitGame);

        SetSelected(0, false);
    }

    // Two chalk strokes making a chevron. ⚠️ DRAWN, never typed as ">" — the display face is
    // CCBattleScarred and a glyph it happens not to carry renders as a blank or a box, which is the
    // same trap the character-select arrow hints hit.
    private RectTransform BuildChalkMark()
    {
        RectTransform mark = AddPoint(printed, "ChalkMark", new Vector2(0.5f, 0.5f),
                                      new Vector2(CHALK_X, 0f), new Vector2(28f, 30f));

        AddChalkStroke(mark, new Vector2(-3f, 7f), -38f, 22f, 3.4f);
        AddChalkStroke(mark, new Vector2(-3f, -7f), 38f, 22f, 3.4f);
        return mark;
    }

    private void AddChalkStroke(RectTransform parent, Vector2 pos, float angle, float len, float thick)
    {
        Image img = AddImage(parent, "Stroke", Parchment.Stroke(), Salvage.Chalk, false);
        img.rectTransform.sizeDelta = new Vector2(len, thick);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void AddEntry(string label, string confirmLabel, System.Action action)
    {
        int index = entries.Count;
        float y = LIST_TOP - index * MENU_STEP;

        Entry e = new Entry { label = label, confirmLabel = confirmLabel, action = action };

        e.text = AddText(printed, "Entry_" + label, label, 29f, Salvage.TextMuted,
                         TextAlignmentOptions.Left);
        e.text.rectTransform.sizeDelta = new Vector2(MENU_W, 44f);
        e.text.rectTransform.anchoredPosition = new Vector2(MENU_X, y);
        e.text.characterSpacing = 4f;

        // A transparent hit plate rather than a Button on the label: the label's own rect is only as
        // tall as its text, and a row you have to hit exactly feels broken next to keyboard nav.
        Image hit = AddImage(printed, "Hit_" + label, null, new Color(0f, 0f, 0f, 0f), true);
        hit.rectTransform.sizeDelta = new Vector2(MENU_W + 120f, 48f);
        hit.rectTransform.anchoredPosition = new Vector2(MENU_X, y);

        PauseEntryHover hov = hit.gameObject.AddComponent<PauseEntryHover>();
        hov.onEnter = () => SetSelected(index, true);
        hov.onClick = () => Activate(index);

        entries.Add(e);
    }

    private void BuildStatus()
    {
        // Built into its own layer so CONTROLS can swap the column out. A zero-size point at the
        // board's centre, so every coordinate below means exactly what it did on `printed`.
        statusLayer = AddPoint(printed, "Status", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        column = statusLayer;

        AddColumnHeader("THIS RUN", STAT_LABEL_X, STAT_LABEL_W, TextAlignmentOptions.Left);

        int row = 0;
        vFloor = AddStatRow("FLOOR", row++);
        vHealth = AddBarRow("HEALTH", row++, Salvage.Wound, out barHealth);
        vShift = AddBarRow("SHIFT", row++, Salvage.Shift, out barShift);
        vGold = AddStatRow("GOLD", row++);
        vScrap = AddStatRow("SCRAP", row++);
        vRelics = AddStatRow("RELICS", row++);
        vDeck = AddStatRow("DECK", row++);
        vExhaust = AddStatRow("EXHAUSTED", row++);
        vRecall = AddStatRow("RECALL COST", row++);
        vStagger = AddStatRow("NEXT STAGGER", row++);

        BuildControls();
        column = printed;
    }

    // The key list, in the same row format as the stats so it reads as the same board. Only what a
    // player can press at any time — relic keys (Q, F, dropping with S) are taught by their relic.
    // ⚠️ Keys are written with plain ASCII: the display face is not guaranteed to carry arrows or
    // dashes, and a missing glyph renders as a blank.
    private static readonly string[,] Controls =
    {
        { "MOVE",                  "A / D" },
        { "JUMP",                  "SPACE" },
        { "PICK A CARD",           "1 - 9" },
        { "PLAY IT (AIM WITH MOUSE)", "LEFT CLICK" },
        { "PUT IT BACK",           "RIGHT CLICK" },
        { "RECALL A NEW HAND",     "R" },
        { "DOORS, CHESTS, SHOPS",  "E" },
        { "MAP",                   "M" },
        { "RELICS",                "I" },
        { "LOOK AHEAD",            "HOLD L-CTRL" },
        { "SAVE A BUG REPORT",     "F8" },
    };

    private void BuildControls()
    {
        controlsLayer = AddPoint(printed, "Controls", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        column = controlsLayer;

        AddColumnHeader("CONTROLS", STAT_LABEL_X, STAT_LABEL_W, TextAlignmentOptions.Left);

        // Eleven rows at the stat pitch would run past the board's bottom edge; 32 keeps a margin.
        const float step = 32f;
        for (int i = 0; i < Controls.GetLength(0); i++)
        {
            float y = LIST_TOP - i * step;

            TextMeshProUGUI l = AddText(column, "L_" + Controls[i, 0], Controls[i, 0], 15f, Salvage.TextMuted,
                                        TextAlignmentOptions.Left);
            l.rectTransform.sizeDelta = new Vector2(STAT_LABEL_W + 120f, 24f);
            l.rectTransform.anchoredPosition = new Vector2(STAT_LABEL_X + 60f, y);
            l.characterSpacing = 4f;

            TextMeshProUGUI v = AddText(column, "V_" + Controls[i, 0], Controls[i, 1], 17f, Salvage.TextBright,
                                        TextAlignmentOptions.Right);
            v.rectTransform.sizeDelta = new Vector2(STAT_VALUE_W, 24f);
            v.rectTransform.anchoredPosition = new Vector2(STAT_VALUE_X, y);
        }

        controlsLayer.gameObject.SetActive(false);
    }

    // Derived from the selection every time it changes — never toggled from anywhere else.
    private void RefreshColumn()
    {
        bool showControls = selected == controlsIndex;
        if (statusLayer != null) statusLayer.gameObject.SetActive(!showControls);
        if (controlsLayer != null) controlsLayer.gameObject.SetActive(showControls);
    }

    private void AddColumnHeader(string label, float x, float w, TextAlignmentOptions align)
    {
        TextMeshProUGUI h = AddText(column, "Header_" + label, label, 14f, Salvage.TextMuted, align);
        h.rectTransform.sizeDelta = new Vector2(w, 20f);
        h.rectTransform.anchoredPosition = new Vector2(x, HEADER_Y);
        h.characterSpacing = 10f;

        Image rule = AddImage(column, "HeaderRule_" + label, Parchment.Stroke(),
                              new Color(Salvage.Chalk.r, Salvage.Chalk.g, Salvage.Chalk.b, 0.16f), false);
        rule.rectTransform.sizeDelta = new Vector2(w + 220f, 2f);
        rule.rectTransform.anchoredPosition = new Vector2(x + 110f, HEADER_RULE_Y);
    }

    private TextMeshProUGUI AddStatRow(string label, int row)
    {
        float y = LIST_TOP - row * STAT_STEP;

        TextMeshProUGUI l = AddText(column, "L_" + label, label, 15f, Salvage.TextMuted,
                                    TextAlignmentOptions.Left);
        l.rectTransform.sizeDelta = new Vector2(STAT_LABEL_W, 24f);
        l.rectTransform.anchoredPosition = new Vector2(STAT_LABEL_X, y);
        l.characterSpacing = 4f;

        TextMeshProUGUI v = AddText(column, "V_" + label, "-", 17f, Salvage.TextBody,
                                    TextAlignmentOptions.Right);
        v.rectTransform.sizeDelta = new Vector2(STAT_VALUE_W, 24f);
        v.rectTransform.anchoredPosition = new Vector2(STAT_VALUE_X, y);
        return v;
    }

    // Health and Shift are BOUNDED, so they get a fill; everything else is an unbounded count and
    // stays a number. Same rule the resource panel settled on.
    private TextMeshProUGUI AddBarRow(string label, int row, Color fill, out Image bar)
    {
        float y = LIST_TOP - row * STAT_STEP;

        TextMeshProUGUI l = AddText(column, "L_" + label, label, 15f, Salvage.TextMuted,
                                    TextAlignmentOptions.Left);
        l.rectTransform.sizeDelta = new Vector2(STAT_LABEL_W, 24f);
        l.rectTransform.anchoredPosition = new Vector2(STAT_LABEL_X, y);
        l.characterSpacing = 4f;

        Image track = AddImage(column, "Track_" + label, Salvage.Pixel(),
                               new Color(0f, 0f, 0f, 0.30f), false);
        track.rectTransform.sizeDelta = new Vector2(BAR_W, 7f);
        track.rectTransform.anchoredPosition = new Vector2(BAR_X + BAR_W * 0.5f, y);

        bar = AddImage(track.rectTransform, "Fill_" + label, Salvage.Pixel(), fill, false);
        bar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.anchoredPosition = Vector2.zero;
        bar.rectTransform.sizeDelta = new Vector2(BAR_W, 0f);

        // ⚠️ Wide enough for "100 / 100" at 17pt. An earlier pass gave this 70px and TMP wrapped the
        // health readout onto two lines, which pushed it out of its row.
        TextMeshProUGUI v = AddText(column, "V_" + label, "-", 17f, Salvage.TextBody,
                                    TextAlignmentOptions.Right);
        v.rectTransform.sizeDelta = new Vector2(BAR_VALUE_W, 24f);
        v.rectTransform.anchoredPosition =
            new Vector2(STAT_VALUE_X + STAT_VALUE_W * 0.5f - BAR_VALUE_W * 0.5f, y);
        return v;
    }

    // ⚠️ NOT on the cloth. The footer hangs below the torn hem, on the dark, which is what makes you
    // notice the hem is torn — and it keeps the sheet's bottom edge as the last thing on the sheet.
    private void BuildFooter()
    {
        TextMeshProUGUI f = AddText(content, "Footer",
            "W / S  NAVIGATE          ENTER  SELECT          ESC  RESUME",
            13f, Salvage.TextFaint, TextAlignmentOptions.Center);
        f.rectTransform.sizeDelta = new Vector2(1400f, 22f);
        f.rectTransform.anchoredPosition = new Vector2(0f, FOOTER_Y);
        f.characterSpacing = 6f;
    }

    // ---- open / close ----------------------------------------------------------------------------

    private static readonly string[] Subtitles =
    {
        "TAKE YOUR TIME. IT ISN'T GOING ANYWHERE.",
        "EVERYTHING IS HOLDING VERY STILL.",
        "THE DISTRICT WILL WAIT.",
        "NOBODY MOVES.",
        "BREATHE. THE RUST CAN WAIT.",
    };
    private static int lastSubtitle = -1;

    private void Open()
    {
        if (isOpen) return;
        isOpen = true;

        content.gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // ⚠️ Re-arm the group by hand. Opening can interrupt CloseAnim mid-yank, and that coroutine
        // drops raycasts on its first frame and only restores them at its end — so a screen opened
        // during a close would come up looking perfect and ignoring every click.
        group.blocksRaycasts = true;
        group.interactable = true;

        if (GameManager.instance != null)
        {
            GameManager.instance.RequestPause();
            GameManager.instance.SetGameState(GameState.Paused);
        }

        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");
        hudWasActive = cachedHud != null && cachedHud.activeSelf;
        if (cachedHud != null) cachedHud.SetActive(false);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        // Never the same line twice running — with a pool this small, plain randomness repeats
        // constantly, and a repeat is what makes a line feel canned.
        int pick = Random.Range(0, Subtitles.Length);
        if (Subtitles.Length > 1 && pick == lastSubtitle) pick = (pick + 1) % Subtitles.Length;
        lastSubtitle = pick;
        subtitle.text = Subtitles[pick];

        Refresh();
        SetSelected(0, false);

        // The throw. It comes in from above the rope, overshoots, and swings itself out.
        hang.Release();

        if (audioSource != null) SfxManager.PlayOn(audioSource, ProcSfx.PauseHalt, 0.85f);

        StopAllCoroutines();
        StartCoroutine(OpenAnim());
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (GameManager.instance != null)
        {
            GameManager.instance.ReleasePause();
            GameManager.instance.SetGameState(GameState.Playing);
        }

        if (cachedHud != null) cachedHud.SetActive(hudWasActive);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);

        if (audioSource != null) SfxManager.PlayOn(audioSource, ProcSfx.PauseRelease, 0.7f);

        StopAllCoroutines();
        StartCoroutine(CloseAnim());
    }

    // ⚠️ THE SHEET IS PULLED AWAY, NOT FADED OUT, and the pause is released BEFORE it finishes.
    // A dissolve says the screen was an image laid over the game; whipping the cloth up off the rope
    // says the game was behind it the whole time, which is the one thing this screen exists to say.
    // The game is therefore already running for the ~0.2s the sheet takes to clear — that is the
    // point, not a compromise — so raycasts are dropped on the first frame or the player's first
    // click after resuming would be eaten by a sheet halfway off the screen.
    private IEnumerator CloseAnim()
    {
        const float dur = 0.19f;

        group.blocksRaycasts = false;
        group.interactable = false;

        float startY = sheet.anchoredPosition.y;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            sheet.anchoredPosition = new Vector2(0f, startY + 780f * k * k);   // hauled up and away
            sheet.localRotation = Quaternion.Euler(0f, 0f, hang.angle * (1f - k) + k * 3.5f);
            group.alpha = 1f - k * k;
            yield return null;
        }

        // ⚠️ Open() may have run during the yank (Escape mashed). Deactivating unconditionally would
        // switch off a screen that is supposed to be up, leaving a paused game with no menu on it.
        if (!isOpen) content.gameObject.SetActive(false);

        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;
    }

    private IEnumerator OpenAnim()
    {
        const float dur = 0.16f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // the screen pauses the game; scaled time is frozen
            group.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        group.alpha = 1f;
    }

    // ---- input -----------------------------------------------------------------------------------

    private void Update()
    {
        if (!isOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) && CanOpen()) Open();
            return;
        }

        // SettingsScreen owns the display and will call us back; it also handles its own Escape,
        // so we must not act on that key while it is up.
        if (subScreenOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

        HandleNavigation();
        if (!isOpen) return;   // RESUME closes us mid-frame; don't tick a screen that is gone

        TickArmTimeout();
        TickCloth();
        TickSelectionVisual();
    }

    // Sampled every frame, whatever state this screen is in — hence LateUpdate rather than the tail
    // of Update, which several branches return before reaching.
    private void LateUpdate()
    {
        wasUIPaused = GameManager.instance != null && GameManager.instance.IsUIPaused;
    }

    private bool CanOpen()
    {
        GameManager gm = GameManager.instance;
        if (gm == null) return false;

        // Something else already owns the screen (shop, map, forge, Blompo, chest, quest board...).
        // One check instead of a list of IsOpen flags that would fall behind — see IsUIPaused.
        if (gm.IsUIPaused) return false;

        // ⚠️ AND it must not have owned the screen LAST frame either. Script execution order is
        // undefined, so on the frame the shop closes on Escape it may release its pause before this
        // Update runs — leaving Escape still down, no UI paused, and the pause screen opening
        // instantly behind the screen the player just dismissed. A one-frame memory covers every
        // Escape-handling screen and needs nothing from any of them.
        if (wasUIPaused) return false;

        // No pausing your way out of a death. The game-over flow owns the moment.
        if (gm.player != null)
        {
            PlayerHealth ph = gm.player.GetComponent<PlayerHealth>();
            if (ph != null && ph.IsDead) return false;
        }
        return true;
    }

    private void HandleNavigation()
    {
        int dir = 0;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dir = 1;
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dir = -1;

        if (dir != 0)
        {
            int next = (selected + dir + entries.Count) % entries.Count;
            SetSelected(next, true);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
            Activate(selected);
    }

    private void SetSelected(int index, bool playSound)
    {
        if (entries.Count == 0) return;
        index = Mathf.Clamp(index, 0, entries.Count - 1);

        bool changed = index != selected;

        // Moving off an armed entry disarms it. Arming has to be a deliberate, held state — an
        // "are you sure" that survives you looking elsewhere is not a confirmation.
        if (changed) Disarm(entries[selected]);

        selected = index;
        markTargetY = LIST_TOP - index * MENU_STEP;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e.armed) continue;                        // armed entries keep the warning colour
            e.text.color = i == selected ? Salvage.TextBright : Salvage.TextMuted;
        }

        RefreshColumn();

        if (!playSound) markY = markTargetY;
        else if (changed && audioSource != null)
        {
            SfxManager.PlayOn(audioSource, ProcSfx.PauseTick, 0.5f);
            // Nudging the row nudges the sheet. Tiny, but it is the difference between cloth and a
            // picture of cloth: touching a hung thing moves it.

            hang.Knock();
        }
    }

    private void Activate(int index)
    {
        if (index < 0 || index >= entries.Count) return;
        SetSelected(index, false);

        Entry e = entries[index];

        if (e.confirmLabel != null && !e.armed)
        {
            e.armed = true;
            e.armedUntil = Time.unscaledTime + 4f;
            e.text.text = e.confirmLabel;
            // ⚠️ Wound, not an accent. Salvage has exactly two accents and neither of them can say
            // "this will end your run" — danger is the one place a third colour is permitted.
            e.text.color = Salvage.Wound;
            if (audioSource != null) SfxManager.PlayOn(audioSource, ProcSfx.PauseTick, 0.9f);
            return;
        }

        Disarm(e);
        e.action?.Invoke();
    }

    private void Disarm(Entry e)
    {
        if (e == null || !e.armed) return;
        e.armed = false;
        e.text.text = e.label;
        e.text.color = entries.IndexOf(e) == selected ? Salvage.TextBright : Salvage.TextMuted;
    }

    private void TickArmTimeout()
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].armed && Time.unscaledTime > entries[i].armedUntil) Disarm(entries[i]);
    }

    private void TickCloth() { hang.Tick(sheet, SHEET_TOP); }

    private void TickSelectionVisual()
    {
        // Framerate-independent ease, on unscaled time because the game is frozen.
        float k = 1f - Mathf.Exp(-24f * Time.unscaledDeltaTime);
        markY = Mathf.Lerp(markY, markTargetY, k);

        chalk.anchoredPosition = new Vector2(CHALK_X, markY);
        // The underline sits just under the label's baseline, indented like a hand-drawn rule.
        underline.rectTransform.anchoredPosition = new Vector2(MENU_X - 14f, markY - 23f);
    }

    // ---- content ---------------------------------------------------------------------------------

    private void Refresh()
    {
        PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
        PlayerHealth ph = p != null ? p.GetComponent<PlayerHealth>() : null;
        DeckManager dm = DeckManager.instance;
        RelicManager rm = RelicManager.instance;
        RunMapManager map = RunMapManager.instance;

        if (map != null && map.HasMap && map.CurrentNode != null)
            vFloor.text = map.CurrentNode.floor + " / " + (map.Map.floors - 1);
        else
            vFloor.text = "-";

        if (ph != null)
        {
            float frac = ph.MaxHealth > 0f ? Mathf.Clamp01(ph.CurrentHealth / ph.MaxHealth) : 0f;
            barHealth.rectTransform.sizeDelta = new Vector2(BAR_W * frac, 0f);
            vHealth.text = Mathf.CeilToInt(ph.CurrentHealth) + " / " + Mathf.CeilToInt(ph.MaxHealth);
        }

        if (p != null)
        {
            float frac = p.maxShift > 0 ? Mathf.Clamp01((float)p.GetCurrentShift() / p.maxShift) : 0f;
            barShift.rectTransform.sizeDelta = new Vector2(BAR_W * frac, 0f);
            vShift.text = p.GetCurrentShift() + " / " + p.maxShift;

            vGold.text = p.currentGold.ToString();
            vScrap.text = p.currentScrap.ToString();

            // Mirrors the Stagger card's own rule: the price turns red once it is more than you
            // have left, because that is the run's actual death condition and this is the only
            // place outside the card itself it can be read.
            float cost = p.NextStaggerCost;
            bool lethal = ph != null && cost >= ph.CurrentHealth;
            vStagger.text = Mathf.CeilToInt(cost) + " HP";
            vStagger.color = lethal ? Salvage.Wound : Salvage.TextBody;
        }

        vRelics.text = rm != null
            ? rm.OwnedRelics.Count + " / " + RelicManager.MaxSlots
            : "-";

        if (dm != null)
        {
            int deck = dm.GetDrawPile().Count + dm.GetCurrentHand().Count + dm.GetDiscardPile().Count;
            int exhausted = dm.GetExhaustPile().Count;
            vDeck.text = deck.ToString();
            vExhaust.text = exhausted.ToString();
            vExhaust.color = exhausted > 0 ? Salvage.Torch : Salvage.TextBody;
            // Denominated in Shift, so it carries Shift's colour. That is the whole discipline:
            // cyan means "this is Shift", everywhere in the game, and nothing else ever borrows it.
            vRecall.text = dm.currentRecallCost + " SHIFT";
            vRecall.color = Salvage.Shift;
        }
    }

    // ---- sub-panels ------------------------------------------------------------------------------

    // SettingsScreen is a proper procedural screen with its own callback, so it needs none of the
    // activeSelf polling the legacy panel below does.
    private void OpenSettings()
    {
        subScreenOpen = true;
        SetContentVisible(false);
        SettingsScreen.Open(() =>
        {
            subScreenOpen = false;
            SetContentVisible(true);
        });
    }

    // Hides the pause screen's own furniture WITHOUT releasing the pause or deactivating the root —
    // Update has to keep running to notice the sub-panel closing.
    private void SetContentVisible(bool visible)
    {
        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }

    // ---- actions ---------------------------------------------------------------------------------

    private void AbandonRun()
    {
        if (GameManager.instance != null) GameManager.instance.ReleasePause();
        // Belt and braces before a scene load: an unbalanced pause anywhere would leave the menu
        // frozen. This is the same deliberate bypass the old PauseMenu.LoadMenu did.
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ---- small builders --------------------------------------------------------------------------

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private RectTransform AddPoint(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
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
}

// Pointer relay for a menu row. Hover SELECTS rather than merely highlighting, so the mouse and the
// keyboard drive the same single selection instead of disagreeing about which row is live.
public class PauseEntryHover : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public System.Action onEnter;
    public System.Action onClick;

    public void OnPointerEnter(PointerEventData e) { onEnter?.Invoke(); }
    public void OnPointerClick(PointerEventData e) { onClick?.Invoke(); }
}
