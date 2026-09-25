using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Slot-constrained relic loadout bar (see RelicRedesign.md). A fixed top row of
// RelicManager.MaxSlots cells: filled cells reuse the procedural RelicIcon chip, empty
// cells show a dim rounded frame, and an "N/5" count sits at the end. Hovering a filled
// slot shows a shared tooltip (name / description / sell value).
//
// The bar self-positions in code (top-centre), so it needs no scene re-anchoring — the
// legacy left-column container is disabled on start. Bar-click / hotkey to open the
// Manage panel land in Stage 2.
public class RelicHUD : MonoBehaviour
{
    [Header("Legacy (disabled on start)")]
    [SerializeField] private GameObject iconPrefab;      // RelicIconPrefab — reused for filled slots if present
    [SerializeField] private Transform iconContainer;    // old vertical column — hidden

    [Header("Bar Layout")]
    [SerializeField] private float cellSize = 52f;
    [SerializeField] private float cellSpacing = 8f;
    [SerializeField] private float topMargin = 16f;
    [SerializeField] private float horizontalOffset = 0f;
    [SerializeField] private TMP_FontAsset uiFont;       // optional; auto-resolved if null

    [Header("Manage Panel")]
    [SerializeField] private KeyCode manageKey = KeyCode.I;  // opens the Manage panel (bar click also opens)

    private RectTransform[] cells;
    private RelicSlotHover[] hovers;
    private TMP_Text countText;
    private RelicTooltip tooltip;
    private bool built;

    private void Start()
    {
        BuildBar();

        if (RelicManager.instance == null) return;

        RebuildContents();
        RelicManager.instance.OnRelicAdded += OnRelicsChanged;
        RelicManager.instance.OnRelicRemoved += OnRelicsChanged;

        RelicManagePanel.SetToggleKey(manageKey);
    }

    private void Update()
    {
        // Opening only — this Update runs while the HUD is visible (i.e. the panel is closed).
        // The panel closes itself. The frame guard stops the closing keypress (which reactivates
        // the HUD the same frame) from being re-read here as an immediate reopen.
        if (Input.GetKeyDown(manageKey) && Time.frameCount != RelicManagePanel.LastToggleFrame)
            RelicManagePanel.Open();
    }

    private void OnDestroy()
    {
        if (RelicManager.instance != null)
        {
            RelicManager.instance.OnRelicAdded -= OnRelicsChanged;
            RelicManager.instance.OnRelicRemoved -= OnRelicsChanged;
        }
    }

    private void OnRelicsChanged(RelicData _) => RebuildContents();

    // --- one-time construction of the bar skeleton ---
    private void BuildBar()
    {
        // ⚠️ RE-ENTRANT ON PURPOSE. It used to bail on `built` and never run twice, which was fine
        // while MaxSlots was a compile-time constant. Estate Sale can now award a sixth slot
        // mid-run, and RebuildContents calls back in here to widen the bar — so the old children
        // have to go first, or the row stacks a second set of cells, a second count label and a
        // second tooltip on top of the first.
        if (built)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            cells = null; hovers = null; countText = null; tooltip = null;
        }
        built = true;

        // Retire a SEPARATE legacy column container if one exists. In SampleScene the old
        // design pointed iconContainer at THIS object's own transform (icons were stacked
        // directly on the RelicHUD via a VerticalLayoutGroup) — never disable self, that
        // would switch off the very object building the bar.
        if (iconContainer != null && iconContainer != transform)
            iconContainer.gameObject.SetActive(false);

        TMP_FontAsset font = ResolveFont();

        // Reposition this HUD object to a top-centre point; a horizontal layout + size
        // fitter make it hug its contents so the bar stays centred as slots are themed.
        RectTransform self = GetComponent<RectTransform>();
        if (self == null) self = gameObject.AddComponent<RectTransform>();
        self.anchorMin = self.anchorMax = new Vector2(0.5f, 1f);
        self.pivot = new Vector2(0.5f, 1f);
        self.anchoredPosition = new Vector2(horizontalOffset, -topMargin);

        // The old column stacked icons with a VerticalLayoutGroup on this same object.
        // Remove it synchronously so it doesn't coexist with the new horizontal one for a
        // frame (which would log a "multiple layout groups" warning).
        VerticalLayoutGroup oldVlg = gameObject.GetComponent<VerticalLayoutGroup>();
        if (oldVlg != null) DestroyImmediate(oldVlg);

        HorizontalLayoutGroup hlg = gameObject.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = cellSpacing;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter csf = gameObject.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        int slots = RelicManager.MaxSlots;
        cells = new RectTransform[slots];
        hovers = new RelicSlotHover[slots];
        for (int i = 0; i < slots; i++)
        {
            GameObject cell = new GameObject($"Slot{i}", typeof(RectTransform));
            RectTransform crt = cell.GetComponent<RectTransform>();
            crt.SetParent(transform, false);
            crt.sizeDelta = new Vector2(cellSize, cellSize);

            LayoutElement le = cell.AddComponent<LayoutElement>();
            le.preferredWidth = cellSize;
            le.preferredHeight = cellSize;

            // Transparent hit-target so hover works (RelicIcon's own graphics are non-raycast).
            Image hit = cell.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            hovers[i] = cell.AddComponent<RelicSlotHover>();
            cells[i] = crt;
        }

        // Count label ("3/5") at the end of the row.
        GameObject countGo = new GameObject("Count", typeof(RectTransform));
        RectTransform countRt = countGo.GetComponent<RectTransform>();
        countRt.SetParent(transform, false);
        countRt.sizeDelta = new Vector2(56f, cellSize);
        LayoutElement countLe = countGo.AddComponent<LayoutElement>();
        countLe.preferredWidth = 56f;
        countLe.preferredHeight = cellSize;
        countText = countGo.AddComponent<TextMeshProUGUI>();
        if (font != null) countText.font = font;
        countText.fontSize = 20f;
        countText.fontStyle = FontStyles.Bold;
        countText.alignment = TextAlignmentOptions.Left;
        countText.color = FlatUI.Loadout.TextMuted;   // a label, not a stat — stays quiet
        countText.raycastTarget = false;

        // Shared tooltip (child of the bar so it hides with the HUD; positioned in world space).
        GameObject tipGo = new GameObject("RelicTooltip", typeof(RectTransform));
        tipGo.transform.SetParent(transform, false);
        tipGo.AddComponent<LayoutElement>().ignoreLayout = true;
        tooltip = tipGo.AddComponent<RelicTooltip>();
        tooltip.Build(font);
    }

    // --- fill/empty the slots from the current owned list ---
    private void RebuildContents()
    {
        if (!built || RelicManager.instance == null) return;

        var owned = RelicManager.instance.OwnedRelics;
        int slots = RelicManager.MaxSlots;

        // ⚠️ MaxSlots CAN GROW MID-RUN (Estate Sale earns a sixth). The cell array was built once at
        // the count that was current then, so a grown loadout indexed straight off the end and threw
        // IndexOutOfRange the moment the slot was awarded — the HUD died on the frame the reward
        // landed. Rebuild the bar instead of trusting the cached size.
        if (cells == null || cells.Length != slots)
        {
            BuildBar();
            if (cells == null || cells.Length != slots) return;   // build refused; nothing to fill
            owned = RelicManager.instance.OwnedRelics;
        }

        for (int i = 0; i < slots; i++)
        {
            RectTransform cell = cells[i];

            // Clear whatever the cell held last time.
            for (int c = cell.childCount - 1; c >= 0; c--)
                Destroy(cell.GetChild(c).gameObject);

            if (i < owned.Count)
            {
                BuildFilledSlot(cell, owned[i]);
                hovers[i].Set(owned[i], tooltip);
            }
            else
            {
                BuildEmptySlot(cell);
                hovers[i].Set(null, tooltip);
            }
        }

        if (countText != null)
            countText.text = $"{owned.Count}/{slots}";
    }

    private void BuildFilledSlot(RectTransform cell, RelicData relic)
    {
        GameObject icon = iconPrefab != null
            ? Instantiate(iconPrefab, cell)
            : new GameObject("RelicIcon", typeof(RectTransform), typeof(Image));

        RectTransform irt = icon.GetComponent<RectTransform>();
        if (irt == null) irt = icon.AddComponent<RectTransform>();
        irt.SetParent(cell, false);
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = Vector2.zero;
        irt.sizeDelta = new Vector2(cellSize - 6f, cellSize - 6f);

        RelicIcon styler = icon.GetComponent<RelicIcon>();
        if (styler == null) styler = icon.AddComponent<RelicIcon>();
        styler.Build(relic);
    }

    // An empty socket: the same chamfered plate as a filled slot but recessed and unlit, with no
    // rarity strip. Read against a filled neighbour the difference is immediate — something is
    // seated here, or nothing is — without the row losing its shape.
    private void BuildEmptySlot(RectTransform cell)
    {
        float inner = cellSize - 6f;
        FlatUI.Theme T = FlatUI.Loadout;

        GameObject fill = new GameObject("EmptyFill", typeof(RectTransform));
        RectTransform frt = fill.GetComponent<RectTransform>();
        frt.SetParent(cell, false);
        frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = new Vector2(inner, inner);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.sprite = FlatUI.Panel(5);
        fillImg.type = Image.Type.Sliced;
        fillImg.color = T.Surface;
        fillImg.raycastTarget = false;

        GameObject frame = new GameObject("EmptyFrame", typeof(RectTransform));
        RectTransform fr2 = frame.GetComponent<RectTransform>();
        fr2.SetParent(cell, false);
        fr2.anchorMin = fr2.anchorMax = fr2.pivot = new Vector2(0.5f, 0.5f);
        fr2.sizeDelta = new Vector2(inner, inner);
        Image frameImg = frame.AddComponent<Image>();
        frameImg.sprite = FlatUI.Outline(5, 1);
        frameImg.type = Image.Type.Sliced;
        frameImg.color = T.BorderSoft;
        frameImg.raycastTarget = false;
    }

    private TMP_FontAsset ResolveFont()
    {
        if (uiFont != null) return uiFont;      // Inspector override wins
        return FlatUI.UIFont();
    }
}
