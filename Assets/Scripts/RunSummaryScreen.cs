using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The end of a run — won or lost. Replaces the old GameOverScene ("You DIED!" + two buttons) and
// gives winning an ending at all: beating the final boss used to drop the player back in the hub
// with a fresh map, as if nothing had happened.
//
// ══ WHAT IT IS ═══════════════════════════════════════════════════════════════════════════════════
//
// The same hanging board as the pause screen (SalvageScreen.BuildBoard), dropped in over the room
// where the run ended. The frozen game stays visible, dimmed, past the board's edges: this is the
// one moment where WHERE it happened is part of what the screen is saying.
//
// It is the screen a player sees most in a roguelike, and the one they screenshot to show a friend,
// so it carries the whole run: the numbers, the deck they built and the relics they held.
//
// ⚠️ IT ARRIVES UNPROMPTED — mid-fight on a death, straight off an E-press on a victory — so it
// follows BossRewardScreen's three input rules: no input at all until the board has landed, Enter
// must be seen UP once before a press counts, and Space / E never confirm (they are jump and
// interact, which the player was mashing a second ago).
//
// ⚠️ IT IS NOT DISMISSIBLE. Both choices load a scene; there is no run to return to.
public class RunSummaryScreen : GameScreen
{
    private static RunSummaryScreen instance;

    public static bool IsShowing => instance != null && instance.isOpen;

    /// <summary>The run was won (the player left the final boss's room).</summary>
    public static void ShowVictory() { Show(true); }

    /// <summary>The player died. Returns false if the screen could not be built, so the caller can
    /// fall back to the old GameOverScene rather than leaving the player on a frozen corpse.</summary>
    public static bool ShowDefeat() { return Show(false); }

    private static bool Show(bool won)
    {
        if (IsShowing) return true;

        Canvas canvas = FindRootCanvas();
        if (canvas == null) return false;

        GameObject go = new GameObject("RunSummaryScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<RunSummaryScreen>();
        instance.won = won;
        instance.Build();
        instance.Open();
        return true;
    }

    // The board lands with the pause screen's own halt, not the generic UI open.
    protected override bool PlaysDefaultOpenCloseSound => false;

    // ---- layout, canvas-centre coordinates (1920x1080 reference, width flexes) --------------------

    // ⚠️ 1400 wide for the same reason as the pause board: the canvas is 1440 wide at 4:3.
    private const float BOARD_W = 1400f;
    private const float BOARD_TOP = 440f;
    private const float BOARD_BOTTOM = -352f;

    private const float TITLE_Y = 372f;
    private const float SUB_Y = 318f;
    private const float TITLE_RULE_Y = 288f;
    private const float HEADER_Y = 250f;
    private const float HEADER_RULE_Y = 232f;

    // Left column: the numbers.
    private const float STAT_LABEL_X = -520f;
    private const float STAT_LABEL_W = 260f;
    private const float STAT_VALUE_X = -262f;
    private const float STAT_VALUE_W = 180f;
    private const float STAT_TOP = 198f;
    private const float STAT_STEP = 33f;

    // Right column: the deck, then the relics.
    private const float DECK_LEFT = -90f;
    private const float DECK_RIGHT = 640f;
    private const float DECK_TOP = 212f;
    private const float DECK_BOTTOM = -40f;
    private const float RELIC_HEADER_Y = -70f;
    private const float RELIC_Y = -128f;
    private const float RELIC_SIZE = 56f;

    private const float INSPECT_Y = -178f;
    private const float BUTTON_Y = -250f;
    private const float BUTTON_W = 300f;
    private const float BUTTON_GAP = 170f;       // each button's centre sits this far from x = 0
    private const float HINT_Y = -312f;
    private const float FOOTER_Y = -394f;        // below the board, on the dark

    // ⚠️ Input stays dead until the board has dropped and swung — see the header.
    private const float SETTLE = 0.9f;

    private bool won;
    private RectTransform content, board, printed;
    private CanvasGroup group;
    private SalvageScreen.Hang hang;

    private readonly List<TextMeshProUGUI> buttons = new List<TextMeshProUGUI>();
    private RectTransform chevron;
    private Image underline;
    private int selected;
    private float markX, markTargetX;

    private TextMeshProUGUI inspect;
    private string inspectDefault;

    private float inputLiveAt;
    private int openedFrame;
    private bool confirmReleased;
    private bool leaving;

    // ---- construction ----------------------------------------------------------------------------

    private void Build()
    {
        RectTransform root = (RectTransform)transform;
        SalvageScreen.Stretch(root);

        content = SalvageScreen.Point(transform, "Content", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        SalvageScreen.Stretch(content);
        group = content.gameObject.AddComponent<CanvasGroup>();

        // The frozen room, dimmed — not covered. Raycast ON so nothing behind can be clicked.
        Image backdrop = SalvageScreen.Img(content, "Backdrop", null, new Color(0.030f, 0.025f, 0.021f, 0.80f), true);
        SalvageScreen.Stretch(backdrop.rectTransform);

        SalvageScreen.BuildBoard(content, BOARD_W, BOARD_TOP, BOARD_BOTTOM, out board, out printed);

        BuildTitle();
        BuildStats();
        BuildDeck();
        BuildRelics();
        BuildButtons();
        BuildHints();
    }

    private void BuildTitle()
    {
        // Victory is the one place the title is LIT: Torch is Salvage's "lit / present" accent, and
        // a defeat has nothing to light.
        TextMeshProUGUI t = AddLabel(printed, "Title", won ? "YOU MADE IT OUT" : "YOU FELL",
                                     TextRole.Hero, won ? Salvage.Torch : Salvage.TextBright);
        Place(t, new Vector2(0f, TITLE_Y), new Vector2(1200f, 80f));
        t.characterSpacing = 14f;

        TextMeshProUGUI sub = AddLabel(printed, "Subtitle", Subtitle(), TextRole.Body, Salvage.TextBody);
        UIType.ApplyProse(sub, TextRole.Body);
        Place(sub, new Vector2(0f, SUB_Y), new Vector2(1200f, 34f));

        Rule(new Vector2(0f, TITLE_RULE_Y), 820f, 0.30f);
    }

    // One sentence saying how it ended. Kept literal where it matters (the floor, who did it) and
    // allowed a grin everywhere else — see Tone & Voice in CLAUDE.md.
    private string Subtitle()
    {
        RunMapManager map = RunMapManager.instance;
        MapNode node = map != null ? map.CurrentNode : null;
        string floor = node != null && map.Map != null
            ? $"Floor {node.floor} of {map.Map.floors - 1}"
            : "Somewhere in the district";

        if (won)
        {
            string boss = RunStats.BossesKilled.Count > 0 ? RunStats.BossesKilled[RunStats.BossesKilled.Count - 1] : null;
            return boss != null
                ? $"{boss} is down, and the Oxidation District is behind you."
                : "The top of the castle is yours. The district is behind you.";
        }

        if (RunStats.LastLossWasStagger)
            return $"{floor}. Stagger sent the bill, and you couldn't cover it.";
        if (!string.IsNullOrEmpty(RunStats.CurrentBoss))
            return $"{floor}. {RunStats.CurrentBoss} had the last word.";
        if (node != null && node.type != MapNodeType.Start)
            return $"{floor}, in a {MapGlyphs.LabelFor(node.type).ToLowerInvariant()} room. The district keeps this one.";
        return $"{floor}. The district keeps this one.";
    }

    private void BuildStats()
    {
        ColumnHeader("THIS RUN", STAT_LABEL_X - STAT_LABEL_W * 0.5f, STAT_VALUE_X + STAT_VALUE_W * 0.5f, HEADER_Y);

        RunMapManager map = RunMapManager.instance;
        MapNode node = map != null ? map.CurrentNode : null;

        int row = 0;
        StatRow(row++, "CHARACTER", CharacterSelection.Chosen != null ? CharacterSelection.Chosen.characterName.ToUpperInvariant() : "-");
        StatRow(row++, "FLOOR REACHED", node != null && map.Map != null ? node.floor + " / " + (map.Map.floors - 1) : "-");
        StatRow(row++, "PLAY TIME", RunStats.FormatTime(RunStats.PlaySeconds));
        StatRow(row++, "ROOMS CLEARED", RunStats.RoomsCleared.ToString());
        StatRow(row++, "ENEMIES DEFEATED", RunStats.Kills.ToString());
        StatRow(row++, "BOSSES DEFEATED", RunStats.BossesKilled.Count.ToString());
        // Denominated in Shift, so it carries Shift's colour — the one rule cyan obeys everywhere.
        StatRow(row++, "SHIFT SPENT", RunStats.ShiftSpent.ToString(), Salvage.Shift);
        StatRow(row++, "GOLD COLLECTED", RunStats.GoldCollected.ToString());
        StatRow(row++, "CARDS PLAYED", RunStats.CardsPlayed.ToString());
        StatRow(row++, "DAMAGE TAKEN", Mathf.CeilToInt(RunStats.DamageTaken).ToString());
    }

    private void StatRow(int row, string label, string value, Color? valueColor = null)
    {
        float y = STAT_TOP - row * STAT_STEP;

        TextMeshProUGUI l = AddLabel(printed, "L_" + label, label, TextRole.Caption, Salvage.TextMuted,
                                     TextAlignmentOptions.Left);
        Place(l, new Vector2(STAT_LABEL_X, y), new Vector2(STAT_LABEL_W, 26f));
        l.characterSpacing = 4f;

        TextMeshProUGUI v = AddLabel(printed, "V_" + label, value, TextRole.Body, valueColor ?? Salvage.TextBright,
                                     TextAlignmentOptions.Right);
        Place(v, new Vector2(STAT_VALUE_X, y), new Vector2(STAT_VALUE_W, 26f));
    }

    // Every card the player owns — hand, draw, discard and exhaust — at the true card face. Exhausted
    // cards are shown dimmed rather than hidden: they are still part of the deck they built.
    private void BuildDeck()
    {
        List<RuntimeCard> cards = new List<RuntimeCard>();
        List<RuntimeCard> exhausted = new List<RuntimeCard>();
        DeckManager dm = DeckManager.instance;
        if (dm != null)
        {
            AddOwned(cards, dm.GetCurrentHand());
            AddOwned(cards, dm.GetDrawPile());
            AddOwned(cards, dm.GetDiscardPile());
            AddOwned(exhausted, dm.GetExhaustPile());
        }
        cards.Sort((a, b) => string.CompareOrdinal(a.cardData.cardName, b.cardData.cardName));
        exhausted.Sort((a, b) => string.CompareOrdinal(a.cardData.cardName, b.cardData.cardName));
        int liveCount = cards.Count;
        cards.AddRange(exhausted);

        float areaW = DECK_RIGHT - DECK_LEFT;
        ColumnHeader("YOUR DECK  ·  " + cards.Count, DECK_LEFT, DECK_RIGHT, HEADER_Y);

        if (cards.Count == 0)
        {
            TextMeshProUGUI none = AddLabel(printed, "NoCards", "No cards left to your name.", TextRole.Caption,
                                            Salvage.TextMuted, TextAlignmentOptions.Left);
            UIType.ApplyProse(none, TextRole.Caption);
            Place(none, new Vector2(DECK_LEFT + areaW * 0.5f, DECK_TOP - 20f), new Vector2(areaW, 30f));
            return;
        }

        // The largest card that fits the area, at the card's true aspect. A 4-card starter deck gets
        // big readable faces; a 30-card deck still fits in three rows.
        const float gap = 8f;
        float areaH = DECK_TOP - DECK_BOTTOM;
        float w = 30f;
        int cols = 1;
        for (float tryW = 104f; tryW >= 30f; tryW -= 2f)
        {
            int c = Mathf.Max(1, Mathf.FloorToInt((areaW + gap) / (tryW + gap)));
            int r = Mathf.CeilToInt(cards.Count / (float)c);
            if (r * tryW * CardFace.ASPECT + (r - 1) * gap <= areaH) { w = tryW; cols = c; break; }
        }
        float h = w * CardFace.ASPECT;

        for (int i = 0; i < cards.Count; i++)
        {
            int col = i % cols, row = i / cols;
            Vector2 pos = new Vector2(DECK_LEFT + w * 0.5f + col * (w + gap),
                                      DECK_TOP - h * 0.5f - row * (h + gap));

            RectTransform host = SalvageScreen.Point(printed, "Card_" + i, new Vector2(0.5f, 0.5f), pos, new Vector2(w, h));
            CardFace.Build(host, cards[i]);

            bool isExhausted = i >= liveCount;
            if (isExhausted) host.gameObject.AddComponent<CanvasGroup>().alpha = 0.38f;

            RuntimeCard card = cards[i];
            string line = card.cardData.cardName.ToUpperInvariant() + (isExhausted ? "  (EXHAUSTED)" : "")
                          + "  —  " + card.cardData.description;
            AddHover(host, line);
        }
    }

    private static void AddOwned(List<RuntimeCard> into, List<RuntimeCard> pile)
    {
        if (pile == null) return;
        foreach (RuntimeCard c in pile)
            if (c != null && c.cardData != null && !DeckManager.IsStagger(c)) into.Add(c);   // Stagger is conjured, never owned
    }

    private void BuildRelics()
    {
        RelicManager rm = RelicManager.instance;
        int count = rm != null ? rm.OwnedRelics.Count : 0;

        ColumnHeader("RELICS  ·  " + count, DECK_LEFT, DECK_RIGHT, RELIC_HEADER_Y);

        if (count == 0)
        {
            TextMeshProUGUI none = AddLabel(printed, "NoRelics", "Travelled light. No relics this run.",
                                            TextRole.Caption, Salvage.TextMuted, TextAlignmentOptions.Left);
            UIType.ApplyProse(none, TextRole.Caption);
            Place(none, new Vector2(DECK_LEFT + 365f, RELIC_Y), new Vector2(730f, 30f));
            return;
        }

        for (int i = 0; i < count; i++)
        {
            RelicData r = rm.OwnedRelics[i];
            if (r == null) continue;
            Vector2 pos = new Vector2(DECK_LEFT + RELIC_SIZE * 0.5f + i * (RELIC_SIZE + 16f), RELIC_Y);
            Image icon = SalvageScreen.Img(printed, "Relic_" + i, r.relicArt, Color.white, true);
            icon.preserveAspect = true;
            Place(icon, pos, new Vector2(RELIC_SIZE, RELIC_SIZE));
            AddHover(icon.rectTransform, r.relicName.ToUpperInvariant() + "  —  " + r.description);
        }
    }

    private void BuildButtons()
    {
        // Chalk marks the selection, exactly as on the pause board — a chevron and an underline in
        // the colour and stroke the world's exit marker uses. No lit plate, no new colour.
        underline = SalvageScreen.Img(printed, "Underline", Parchment.Stroke(), Salvage.Chalk);
        underline.rectTransform.sizeDelta = new Vector2(BUTTON_W - 80f, 3.4f);

        chevron = SalvageScreen.Point(printed, "Chevron", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28f, 30f));
        ChalkStroke(chevron, new Vector2(-3f, 7f), -38f);
        ChalkStroke(chevron, new Vector2(-3f, -7f), 38f);

        AddButton("PLAY AGAIN", -BUTTON_GAP, PlayAgain);
        AddButton("MAIN MENU", BUTTON_GAP, MainMenu);
        SetSelected(0, false);
    }

    private void AddButton(string label, float x, System.Action action)
    {
        int index = buttons.Count;
        TextMeshProUGUI t = AddLabel(printed, "Button_" + label, label, TextRole.Heading, Salvage.TextMuted);
        Place(t, new Vector2(x, BUTTON_Y), new Vector2(BUTTON_W, 48f));
        t.characterSpacing = 5f;
        buttons.Add(t);

        // A transparent hit plate wider than the label: a row you must hit exactly feels broken.
        Image hit = SalvageScreen.Img(printed, "Hit_" + label, null, new Color(0f, 0f, 0f, 0f), true);
        Place(hit, new Vector2(x, BUTTON_Y), new Vector2(BUTTON_W + 40f, 64f));
        PauseEntryHover hov = hit.gameObject.AddComponent<PauseEntryHover>();
        hov.onEnter = () => { if (InputLive) SetSelected(index, true); };
        hov.onClick = () => { if (InputLive) { SetSelected(index, false); action(); } };
    }

    private void BuildHints()
    {
        // One shared line for whatever is under the pointer; otherwise, the bug-report reminder. It
        // is the moment a playtester is most likely to have something to say.
        inspectDefault = "Hover a card or relic to read it.";
        inspect = AddLabel(printed, "Inspect", inspectDefault, TextRole.Caption, Salvage.TextMuted);
        UIType.ApplyProse(inspect, TextRole.Caption);
        Place(inspect, new Vector2(0f, INSPECT_Y), new Vector2(1240f, 30f));
        inspect.textWrappingMode = TextWrappingModes.NoWrap;
        inspect.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI hint = AddLabel(printed, "Hint",
            "Spotted a bug? Press F8 at any time to save a report with a screenshot.",
            TextRole.Caption, Salvage.TextFaint);
        UIType.ApplyProse(hint, TextRole.Caption);
        Place(hint, new Vector2(0f, HINT_Y), new Vector2(1200f, 28f));

        TextMeshProUGUI f = AddLabel(content, "Footer", "A / D  CHOOSE          ENTER  SELECT",
                                     TextRole.Caption, Salvage.TextFaint);
        Place(f, new Vector2(0f, FOOTER_Y), new Vector2(1400f, 24f));
        f.characterSpacing = 6f;
    }

    private void AddHover(RectTransform target, string line)
    {
        // One line by construction: some descriptions carry hand-typed line breaks.
        line = line.Replace("\r", "").Replace("\n", " ");

        Image img = target.GetComponent<Image>();
        if (img == null)
        {
            // A card host has no graphic of its own (the face is a child), so give it an invisible
            // one to catch the pointer over the whole card.
            img = target.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
        }
        img.raycastTarget = true;

        PauseEntryHover hov = target.gameObject.AddComponent<PauseEntryHover>();
        hov.onEnter = () => { if (inspect != null) { inspect.text = line; inspect.color = Salvage.TextBody; } };
        // Nothing to do on click; the line is the whole interaction.
        hov.onClick = () => { };
        target.gameObject.AddComponent<SummaryHoverExit>().onExit = () =>
        {
            if (inspect != null && inspect.text == line) { inspect.text = inspectDefault; inspect.color = Salvage.TextMuted; }
        };
    }

    // ---- open ------------------------------------------------------------------------------------

    private void Open()
    {
        isOpen = true;
        transform.SetAsLastSibling();
        AcquireDisplay();

        openedFrame = Time.frameCount;
        inputLiveAt = Time.unscaledTime + SETTLE;
        confirmReleased = false;

        hang.Release();
        SfxManager.PlayOn(EnsureAudio(), ProcSfx.PauseHalt, 0.85f);
        StartCoroutine(FadeGroup(group, 0f, 1f, 0.25f));
    }

    private AudioSource audioSource;
    private AudioSource EnsureAudio()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        return audioSource;
    }

    private bool InputLive => !leaving && Time.frameCount > openedFrame && Time.unscaledTime >= inputLiveAt;

    // ---- input -----------------------------------------------------------------------------------

    private void Update()
    {
        TickUIPauseMemory();
        if (!isOpen || board == null) return;

        hang.Tick(board, BOARD_TOP);
        TickSelectionVisual();

        if (!InputLive) return;

        int dir = 0;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) dir = 1;
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) ||
                 Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) dir = -1;
        if (dir != 0) SetSelected((selected + dir + buttons.Count) % buttons.Count, true);

        // A FRESH press only: Enter must have been seen up since the board arrived.
        bool held = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
        if (!held) confirmReleased = true;
        if (confirmReleased && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            if (selected == 0) PlayAgain(); else MainMenu();
        }
    }

    private void SetSelected(int index, bool playSound)
    {
        if (buttons.Count == 0) return;
        bool changed = index != selected;
        selected = Mathf.Clamp(index, 0, buttons.Count - 1);
        markTargetX = selected == 0 ? -BUTTON_GAP : BUTTON_GAP;

        for (int i = 0; i < buttons.Count; i++)
            buttons[i].color = i == selected ? Salvage.TextBright : Salvage.TextMuted;

        if (!playSound) markX = markTargetX;
        else if (changed)
        {
            SfxManager.PlayOn(EnsureAudio(), ProcSfx.PauseTick, 0.5f);
            hang.Knock();
        }
    }

    private void TickSelectionVisual()
    {
        float k = 1f - Mathf.Exp(-24f * Time.unscaledDeltaTime);
        markX = Mathf.Lerp(markX, markTargetX, k);
        underline.rectTransform.anchoredPosition = new Vector2(markX, BUTTON_Y - 26f);
        chevron.anchoredPosition = new Vector2(markX - BUTTON_W * 0.5f - 6f, BUTTON_Y);
    }

    // ---- actions ---------------------------------------------------------------------------------

    private void PlayAgain() { Leave("SampleScene"); }
    private void MainMenu() { Leave("MainMenu"); }

    // ⚠️ Same character, straight into a new run: CharacterSelection.Chosen survives the reload.
    // MAIN MENU is the way to pick someone else.
    private void Leave(string scene)
    {
        if (leaving) return;
        leaving = true;
        PlayConfirm();
        StartCoroutine(LoadRoutine(scene));
    }

    // Async so the board keeps swinging through the ~1s load (Update keeps ticking it) instead of
    // freezing on one frame, which reads as a hang — measured on the character select at 1.04s.
    //
    // ⚠️ THE GAME STAYS PAUSED UNTIL THE LAST FRAME. Releasing first would un-freeze the room behind
    // the board for the whole load: enemies walking, the player movable. Loading does not depend on
    // timeScale, so the pause is only handed back once the new scene is ready to switch in.
    private IEnumerator LoadRoutine(string scene)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(scene);
        if (op == null)
        {
            ReleaseForSceneChange();
            SceneManager.LoadScene(scene);
            yield break;
        }

        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;

        ReleaseForSceneChange();
        op.allowSceneActivation = true;
    }

    private void ReleaseForSceneChange()
    {
        // Hand the pause back BEFORE the swap, so OnDestroy during the unload has nothing left to
        // release against whichever GameManager happens to be current at that moment.
        ReleaseDisplay();
        // Documented exception to the pause counter: a hard reset before a scene transition.
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    // ---- small builders --------------------------------------------------------------------------

    // A column heading at the column's left edge, with a faint chalk rule scored across the column.
    private void ColumnHeader(string label, float left, float right, float y)
    {
        TextMeshProUGUI h = AddLabel(printed, "Header_" + label, label, TextRole.Caption, Salvage.TextMuted,
                                     TextAlignmentOptions.Left);
        Place(h, new Vector2(left + 200f, y), new Vector2(400f, 22f));
        h.characterSpacing = 10f;
        Rule(new Vector2((left + right) * 0.5f, y - 18f), right - left, 0.16f, 2f);
    }

    private void Rule(Vector2 pos, float width, float alpha, float thickness = 3f)
    {
        Image rule = SalvageScreen.Img(printed, "Rule", Parchment.Stroke(),
                                       new Color(Salvage.Chalk.r, Salvage.Chalk.g, Salvage.Chalk.b, alpha));
        rule.rectTransform.sizeDelta = new Vector2(width, thickness);
        rule.rectTransform.anchoredPosition = pos;
    }

    private void ChalkStroke(RectTransform parent, Vector2 pos, float angle)
    {
        Image img = SalvageScreen.Img(parent, "Stroke", Parchment.Stroke(), Salvage.Chalk);
        img.rectTransform.sizeDelta = new Vector2(22f, 3.4f);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private static void Place(Graphic g, Vector2 pos, Vector2 size) { Place(g.rectTransform, pos, size); }

    private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}

// Pointer-exit relay, so the inspect line can fall back to its default when the pointer leaves.
public class SummaryHoverExit : MonoBehaviour, UnityEngine.EventSystems.IPointerExitHandler
{
    public System.Action onExit;
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { onExit?.Invoke(); }
}
