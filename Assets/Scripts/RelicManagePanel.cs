using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A calm, paused screen to inspect the loadout and SELL a relic for gold. Self-instantiated under
// the main Canvas — no scene/prefab wiring. Opens from a loadout-bar click (RelicSlotHover) or the
// toggle hotkey (default I, set by RelicHUD).
//
// Runs the FlatUI LOADOUT theme, deliberately the SAME theme as the bar rather than its own: this
// panel is the bar opened up, not a different place. It reuses RelicIcon for filled slots, so a
// relic looks identical here and in the HUD, and rarity reads the same way (coloured strip under
// the socket) in both.
public class RelicManagePanel : GameScreen
{
    public static RelicManagePanel instance;
    private static KeyCode toggleKey = KeyCode.I;

    // Frame of the last open/close. RelicHUD checks this so the keypress that closes the panel
    // (which reactivates the HUD mid-frame) can't be re-read by the HUD to instantly reopen.
    public static int LastToggleFrame { get; private set; }

    private CanvasGroup group;
    private RectTransform window;
    private Transform slotRow;
    private TMP_Text countText, detailName, detailDesc;
    private GameObject sellButtonGo;
    private TMP_Text sellLabel;
    private TMP_FontAsset font;

    private readonly List<Image> selectRings = new List<Image>();
    private readonly List<RelicData> cellRelics = new List<RelicData>();
    private RelicData selected;
    // isOpen, and the pause / game-state / HUD / drawer bookkeeping, now live in GameScreen.

    // Height came down from 470: with the ornate border gone the detail area was mostly empty
    // panel, and a relic description is only ever a line or two.
    private const float WIN_W = 680f, WIN_H = 418f;
    private const float CELL = 104f;
    private const float PAD = 32f;

    public static void SetToggleKey(KeyCode k) => toggleKey = k;

    public static void Open()
    {
        EnsureInstance();
        if (instance != null) instance.Show();
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        Canvas canvas = FindRootCanvas();
        if (canvas == null) { Debug.LogWarning("RelicManagePanel: no Canvas found in scene."); return; }
        GameObject go = new GameObject("RelicManagePanel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<RelicManagePanel>();
        instance.Build();
    }

    // FindRootCanvas now lives in GameScreen.

    private void Update()
    {
        // Close via the same toggle key while open (opening is handled by RelicHUD, which is
        // inactive while this panel is up).
        if (isOpen && Input.GetKeyDown(toggleKey)) Hide();
    }

    // ---- construction ----
    private void Build()
    {
        font = ResolveFont();

        RectTransform root = GetComponent<RectTransform>();
        Stretch(root);
        group = gameObject.AddComponent<CanvasGroup>();

        FlatUI.Theme T = FlatUI.Loadout;

        // Full-screen dim; clicking it (outside the window) closes.
        Image backdrop = AddImage(transform, "Backdrop", null, T.Backdrop, true);
        Stretch(backdrop.rectTransform);
        Button backBtn = backdrop.gameObject.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(Hide);

        // Window
        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, WIN_H));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = FlatUI.Panel(8);
        winBg.type = Image.Type.Sliced;
        winBg.color = new Color(0.063f, 0.061f, 0.058f, 0.99f);
        winBg.raycastTarget = true;   // blocks the backdrop-close behind it
        Image winFrame = AddImage(window, "Frame", FlatUI.Outline(8, 2), T.Border, false);
        winFrame.type = Image.Type.Sliced;
        Stretch(winFrame.rectTransform);

        // Header. Insets are much tighter than the old ones, which existed only to clear the
        // ornate border and its gem studs — with a 2px outline that padding is just dead space.
        AddText(window, "Title", new Vector2(0f, 1f), new Vector2(PAD, -26f), new Vector2(300f, 40f),
            "RELICS", 27f, FontStyles.Bold, T.TextBright, TextAlignmentOptions.TopLeft)
            .characterSpacing = 6f;
        countText = AddText(window, "Count", new Vector2(1f, 1f), new Vector2(-PAD - 34f, -28f), new Vector2(120f, 30f),
            "0 / 5", 19f, FontStyles.Bold, T.TextMuted, TextAlignmentOptions.TopRight);

        BuildCloseButton(T);

        AddRule(-70f);

        // Slot row (horizontal, centred under the header)
        RectTransform rowRt = AddPoint(window, "SlotRow", new Vector2(0.5f, 1f), new Vector2(0f, -86f),
            new Vector2(WIN_W - 40f, CELL));
        rowRt.pivot = new Vector2(0.5f, 1f);
        HorizontalLayoutGroup hlg = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        slotRow = rowRt;

        AddRule(-206f);

        // Detail area (lower).
        detailName = AddText(window, "DetailName", new Vector2(0f, 1f), new Vector2(PAD, -224f), new Vector2(WIN_W - PAD * 2f, 32f),
            "", 22f, FontStyles.Bold, T.TextBright, TextAlignmentOptions.TopLeft);
        // Narrow column on the left so a long description never runs under the sell button.
        detailDesc = AddText(window, "DetailDesc", new Vector2(0f, 1f), new Vector2(PAD, -256f), new Vector2(WIN_W - 300f, 84f),
            "", 16f, FontStyles.Normal, T.TextBody, TextAlignmentOptions.TopLeft);
        detailDesc.enableWordWrapping = true;

        // Sell button (bottom-right).
        sellButtonGo = BuildButton(window, "SellButton", new Vector2(1f, 0f), new Vector2(-PAD, 26f),
            new Vector2(224f, 46f), out sellLabel, DoSell);

        gameObject.SetActive(false);
    }

    // A plain X, matching the Forge and Blompo. The old one was a red gem in a gold setting,
    // which drew more attention than the relics the panel exists to show.
    private void BuildCloseButton(FlatUI.Theme T)
    {
        const float sz = 30f;
        RectTransform rt = AddPoint(window, "Close", new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(sz, sz));
        rt.pivot = new Vector2(1f, 1f);

        Image hit = AddImage(rt, "Hit", FlatUI.Panel(5), new Color(1f, 1f, 1f, 0.05f), true);
        hit.type = Image.Type.Sliced;
        Stretch(hit.rectTransform);

        Button btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = hit;
        btn.onClick.AddListener(Hide);

        AddText(rt, "X", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sz, sz),
            "X", 17f, FontStyles.Bold, T.TextMuted, TextAlignmentOptions.Center);
    }

    private void AddRule(float y)
    {
        Image div = AddImage(window, "Rule", FlatUI.FadedRule(), FlatUI.Loadout.BorderSoft, false);
        RectTransform drt = div.rectTransform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 1f);
        drt.pivot = new Vector2(0.5f, 1f);
        drt.sizeDelta = new Vector2(WIN_W - PAD * 2f, 1f);
        drt.anchoredPosition = new Vector2(0f, y);
    }

    // ---- open / close ----
    private void Show()
    {
        if (isOpen) return;
        isOpen = true;
        selected = null;
        LastToggleFrame = Time.frameCount;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        AcquireDisplay();

        RebuildSlots();
        UpdateDetail();
        StopAllCoroutines();
        StartCoroutine(OpenAnim());
    }

    private void Hide()
    {
        if (!isOpen) return;
        isOpen = false;
        LastToggleFrame = Time.frameCount;

        // ⚠️ This screen used to hardcode all three of these — SetGameState(Playing), SetActive(true)
        // and an ungated SetLocked(false) — rather than restoring what was actually there. Opened on
        // top of another screen it would have handed the HUD and the drawer back while the outer
        // screen still needed them hidden and locked. GameScreen restores the recorded values.
        ReleaseDisplay();

        gameObject.SetActive(false);
    }

    private IEnumerator OpenAnim()
    {
        float t = 0f; const float dur = 0.2f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            group.alpha = n;
            window.localScale = Vector3.one * (0.9f + 0.1f * EaseOutBack(n));
            yield return null;
        }
        group.alpha = 1f;
        window.localScale = Vector3.one;
    }

    // ---- slot grid ----
    private void RebuildSlots()
    {
        for (int c = slotRow.childCount - 1; c >= 0; c--)
            Destroy(slotRow.GetChild(c).gameObject);
        selectRings.Clear();
        cellRelics.Clear();

        var owned = RelicManager.instance != null ? RelicManager.instance.OwnedRelics : null;
        int count = owned != null ? owned.Count : 0;
        int slots = RelicManager.MaxSlots;

        for (int i = 0; i < slots; i++)
        {
            RectTransform cell = AddPoint(slotRow, $"Slot{i}", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CELL, CELL));
            LayoutElement le = cell.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = CELL;

            // Selection ring (hidden until selected).
            Image ring = AddImage(cell, "Ring", FlatUI.Outline(6, 2), Color.white, false);
            ring.type = Image.Type.Sliced;
            RectTransform ringRt = ring.rectTransform;
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(CELL + 6f, CELL + 6f);
            ring.enabled = false;
            selectRings.Add(ring);

            RelicData relic = i < count ? owned[i] : null;
            cellRelics.Add(relic);

            if (relic != null)
            {
                GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                RectTransform irt = icon.GetComponent<RectTransform>();
                irt.SetParent(cell, false);
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(CELL - 8f, CELL - 8f);
                icon.AddComponent<RelicIcon>().Build(relic);

                // Transparent hit target + button to select this relic.
                Image hit = AddImage(cell, "Hit", null, new Color(0, 0, 0, 0), true);
                hit.rectTransform.anchorMin = hit.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                hit.rectTransform.sizeDelta = new Vector2(CELL, CELL);
                Button b = hit.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                RelicData captured = relic;
                b.onClick.AddListener(() => Select(captured));
            }
            else
            {
                // Same recessed socket as an empty slot in the HUD bar, just larger.
                Image fill = AddImage(cell, "EmptyFill", FlatUI.Panel(5), FlatUI.Loadout.Surface, false);
                fill.type = Image.Type.Sliced;
                fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                fill.rectTransform.sizeDelta = new Vector2(CELL - 8f, CELL - 8f);
                Image frame = AddImage(cell, "EmptyFrame", FlatUI.Outline(5, 1), FlatUI.Loadout.BorderSoft, false);
                frame.type = Image.Type.Sliced;
                frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                frame.rectTransform.sizeDelta = new Vector2(CELL - 8f, CELL - 8f);
            }
        }

        if (countText != null) countText.text = $"{count} / {slots}";
        RefreshRings();
    }

    private void Select(RelicData relic)
    {
        selected = relic;
        UpdateDetail();
        RefreshRings();
    }

    private void RefreshRings()
    {
        for (int i = 0; i < selectRings.Count; i++)
        {
            bool on = selected != null && cellRelics[i] == selected;
            selectRings[i].enabled = on;
            if (on) selectRings[i].color = FlatUI.RarityColor(selected.rarity);
        }
    }

    private void UpdateDetail()
    {
        if (selected == null)
        {
            detailName.text = "";
            detailDesc.text = "Select a relic to inspect it, or sell it for gold.";
            if (sellButtonGo != null) sellButtonGo.SetActive(false);
            return;
        }

        detailName.text = string.IsNullOrEmpty(selected.relicName) ? selected.relicID : selected.relicName;
        detailName.color = FlatUI.RarityColor(selected.rarity);
        detailDesc.text = string.IsNullOrEmpty(selected.description) ? "-" : selected.description;

        // The relic's live reading, as its own line under the rule — same source as the bar tooltip.
        string live = RelicManager.instance != null ? RelicManager.instance.LiveReadout(selected) : null;
        if (!string.IsNullOrEmpty(live))
            detailDesc.text += "\n<color=#" + ColorUtility.ToHtmlStringRGB(FlatUI.Loadout.TextBright) + "><b>" + live + "</b></color>";

        int value = RelicManager.instance != null ? RelicManager.instance.SellValueFor(selected) : 0;
        if (sellLabel != null) sellLabel.text = $"SELL: {value} GOLD";
        if (sellButtonGo != null) sellButtonGo.SetActive(true);
    }

    private void DoSell()
    {
        if (selected == null || RelicManager.instance == null) return;
        RelicManager.instance.SellRelic(selected);
        selected = null;
        RebuildSlots();
        UpdateDetail();
    }

    // ---- small UGUI builders ----
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private RectTransform AddPoint(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private TMP_Text AddText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = AddPoint(parent, name, anchor, pos, size);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    // Selling is destructive and irreversible, so the button is the one place on this panel that
    // carries colour — a gold-tinted outline and label over the flat plate, matching the "sell for
    // gold" it performs.
    private GameObject BuildButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        out TMP_Text label, UnityEngine.Events.UnityAction onClick)
    {
        Color gold = new Color(0.85f, 0.72f, 0.36f);

        RectTransform rt = AddPoint(parent, name, anchor, pos, size);
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = FlatUI.Panel(5);
        bg.type = Image.Type.Sliced;
        bg.color = new Color(gold.r * 0.20f, gold.g * 0.17f, gold.b * 0.10f, 1f);

        Image outline = AddImage(rt, "Outline", FlatUI.Outline(5, 2), gold, false);
        outline.type = Image.Type.Sliced;
        Stretch(outline.rectTransform);

        Button b = rt.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = bg;
        b.onClick.AddListener(onClick);

        label = AddText(rt, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, size,
            name, 18f, FontStyles.Bold, gold, TextAlignmentOptions.Center);
        return rt.gameObject;
    }

    private TMP_FontAsset ResolveFont() => FlatUI.UIFont();

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
