using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The player's resource readout: health, Shift and gold, top-left of the screen.
// Attach to the ResourcePanel GameObject under Canvas/GameplayHUD — use
// Deckshift -> Rebuild Resource Panel HUD to install and wire it.
//
// LAYOUT: three left-aligned rows on one grid — icon | bar | number.
//
//     [heart]   [=========== health bar ===========]   70 / 100
//     [crystal] [==== segmented Shift bar =========]   34 / 40
//     [coin]    67
//
// The two bars are the SAME length and height so they read as siblings and you can compare them at
// a glance. Numbers sit in their own column OUTSIDE the bars — the old build centred the health
// number on the fill, which put half the text on red and half on dark and made it hard to read.
//
// NO BACKGROUND PANEL. The hand-painted panel art (Assets/Art/panel 1.png) is switched off: it was
// far larger than its own content, and its gold border wrapped a second gold border on every bar,
// so the HUD read as frames inside frames with a lot of dead stone between them. Each bar carries
// its own soft drop shadow instead, which is what separates it from the level behind.
//
// House pattern: the bars are generated in code by ResourceBarUI from RelicUISprites. The legacy
// vertical-filling heart and the "SHIFT: n" text are switched off rather than deleted, so the old
// display is one checkbox away if this needs backing out.
public class ResourcePanelHUD : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Leave empty to find the player through GameManager at runtime.")]
    public PlayerController playerController;
    [Tooltip("Leave empty to borrow the font from the legacy Shift text so the HUD stays consistent.")]
    public TMP_FontAsset font;

    [Header("Icons")]
    public Sprite heartIcon;
    public Sprite shiftIcon;

    [Header("Replaced display")]
    [Tooltip("The painted stat panel behind the HUD. Off by default — it dwarfed its own content and double-framed the bars.")]
    public bool hidePanelBackground = true;
    [Tooltip("The old vertical-filling heart (Health_Background). Hidden on Start.")]
    public GameObject legacyHealthDisplay;
    [Tooltip("The old \"SHIFT: n\" text (JumpText). Hidden on Start.")]
    public GameObject legacyShiftText;
    [Tooltip("The existing gold readout (GoldDisplay). Kept and moved into row 3 — GoldUI still drives its number.")]
    public GameObject goldDisplay;

    [Header("Grid (panel-local px, origin = top-left)")]
    [Tooltip("Centre line every icon is centred on. All three sit on this axis whatever size they are.")]
    public float iconCenterX = 17f;
    [Tooltip("Left edge of both bars, and of the gold number.")]
    public float barX = 44f;
    [Tooltip("Top edge of each row.")]
    public float healthRowY = 2f;
    public float shiftRowY = 42f;
    public float goldRowY = 82f;
    [Tooltip("Font size of the gold number, so row 3 matches the two bar numbers.")]
    public float goldNumberSize = 23f;
    [Tooltip("Colour of the gold number. Matches the palette gold used for sell values elsewhere.")]
    public Color goldNumberColor = new Color(0.85f, 0.72f, 0.36f);

    // The three sprites are very different shapes — the heart is square while the crystal is a
    // narrow spike — so a single box size makes them centred but visibly uneven (in a 30px box the
    // heart's artwork renders 28px wide against the crystal's 16px, which reads as the heart
    // hanging out to the right). These are tuned per shape for equal optical weight, not equal box.
    [Header("Icon sizes (tuned per shape, not equal by accident)")]
    public float heartIconSize = 27f;
    public float shiftIconSize = 33f;
    public float goldIconSize = 30f;
    [Tooltip("Offset of each icon's drop shadow.")]
    public Vector2 iconShadowOffset = new Vector2(2f, 3f);

    [Header("Bar shape — SHARED by health and Shift")]
    [Tooltip("One size for both bars. They are the same object in two colours; change this and both follow.")]
    public BarGeometry barGeometry = new BarGeometry();

    [Header("Health bar colour")]
    public BarStyle healthStyle = new BarStyle
    {
        segmented = false,
        pipMode = BarPipMode.None,
        fill = new Color(0.86f, 0.17f, 0.21f),
        empty = new Color(0.24f, 0.09f, 0.11f),
        chip = new Color(1f, 0.66f, 0.30f),
        low = new Color(1f, 0.32f, 0.32f),
        lowThreshold = 30f,
        warnWhenLow = true,
        showMax = true,
        numberColor = new Color(1f, 0.88f, 0.86f),
        maxNumberColor = new Color(0.70f, 0.56f, 0.56f)
    };

    [Header("Shift bar colour")]
    public BarStyle shiftStyle = new BarStyle
    {
        segmented = true,
        unitsPerSegment = 10,
        segmentGap = 4f,
        pipMode = BarPipMode.ActiveSegment,
        fill = new Color(0.47f, 0.42f, 0.94f),
        empty = new Color(0.19f, 0.17f, 0.31f),
        chip = new Color(0.82f, 0.79f, 1f),
        low = new Color(0.99f, 0.44f, 0.88f),
        lowThreshold = 10f,
        warnWhenLow = true,
        showMax = true,
        numberColor = new Color(0.88f, 0.86f, 1f),
        maxNumberColor = new Color(0.60f, 0.58f, 0.78f)
    };

    // ARMOUR — a second pool that empties before health (PlayerHealth.Armour). Drawn as a short,
    // plated bar sitting directly ABOVE the health bar, because that is the order damage goes
    // through them and the HUD should say so.
    //
    // ⚠️ IT HIDES ENTIRELY AT 0, rather than showing an empty track. Only the Samurai has any
    // armour today; the Wizard and the Ninja would otherwise carry a permanently dead bar for the
    // whole run. An empty readout for a resource you can never have is clutter, not information.
    //
    // ⚠️ Its max is not a real maximum — armour has no cap. The bar scales against the biggest
    // value seen this run (floored at armourBarReference) so it always reads as "how much have I
    // got", never as "how full is a bar that cannot fill".
    [Header("Armour bar")]
    [Tooltip("Gap above the health row. The armour bar is thinner than the other two — it is an " +
             "extra, not a third sibling.")]
    public float armourGap = 4f;
    public float armourBarHeight = 12f;
    [Tooltip("The bar scales against the most armour held this run, never dropping below this.")]
    public float armourBarReference = 10f;
    public BarStyle armourStyle = new BarStyle
    {
        segmented = true,
        unitsPerSegment = 5,
        segmentGap = 3f,
        pipMode = BarPipMode.None,
        fill = new Color(0.74f, 0.80f, 0.88f),      // cold steel — it is plate
        empty = new Color(0.16f, 0.17f, 0.20f),
        chip = new Color(1f, 0.66f, 0.30f),
        low = new Color(0.74f, 0.80f, 0.88f),
        lowThreshold = 0f,
        warnWhenLow = false,                         // armour running out is not an emergency; HP is
        showMax = false,
        numberColor = new Color(0.88f, 0.92f, 1f),
        maxNumberColor = new Color(0.60f, 0.64f, 0.72f)
    };

    private ResourceBarUI healthBar, shiftBar, armourBar;
    private float armourPeak;
    private PlayerHealth playerHealthCache;
    private RectTransform panelRT;

    void Start()
    {
        panelRT = GetComponent<RectTransform>();

        if (font == null && legacyShiftText != null)
        {
            var legacy = legacyShiftText.GetComponent<TextMeshProUGUI>();
            if (legacy != null) font = legacy.font;
        }

        if (hidePanelBackground)
        {
            var bg = GetComponent<Image>();
            if (bg != null) bg.enabled = false;
        }
        if (legacyHealthDisplay != null) legacyHealthDisplay.SetActive(false);
        if (legacyShiftText != null) legacyShiftText.SetActive(false);

        // Both bars are constructed from the SAME BarGeometry instance, so they cannot end up
        // different lengths no matter what is tweaked.
        BuildIcon("HealthIcon", heartIcon, healthRowY, heartIconSize);
        healthBar = new ResourceBarUI(panelRT, "HealthBar", barGeometry, healthStyle, font);
        healthBar.SetPosition(new Vector2(barX, healthRowY));

        BuildIcon("ShiftIcon", shiftIcon, shiftRowY, shiftIconSize);
        shiftBar = new ResourceBarUI(panelRT, "ShiftBar", barGeometry, shiftStyle, font);
        shiftBar.SetPosition(new Vector2(barX, shiftRowY));

        // Its own geometry — same length so it lines up with the bars below it, but thinner and
        // with no number column, since the count is short and the plates already read as a count.
        BarGeometry armourGeo = new BarGeometry
        {
            width = barGeometry.width,
            height = armourBarHeight,
            frameThickness = barGeometry.frameThickness,
            numberPlacement = barGeometry.numberPlacement,
            numberSize = 15f,
            maxNumberSize = 11f,
            numberGap = barGeometry.numberGap
        };
        armourBar = new ResourceBarUI(panelRT, "ArmourBar", armourGeo, armourStyle, font);
        armourBar.SetPosition(new Vector2(barX, healthRowY - armourBarHeight - armourGap));
        armourBar.Root.gameObject.SetActive(false);   // nobody starts with armour

        LayOutGoldRow();
    }

    void Update()
    {
        if (healthBar == null || shiftBar == null) return;

        var player = ResolvePlayer();
        if (player == null) return;

        float dt = Time.unscaledDeltaTime;

        healthBar.SetValue(player.CurrentHealth, player.MaxHealth);
        healthBar.Tick(dt);

        shiftBar.SetValue(player.GetCurrentShift(), player.maxShift);
        shiftBar.Tick(dt);

        UpdateArmourBar(player, dt);
    }

    // Shown only while the player actually has armour. The peak is remembered so the bar DRAINS
    // when armour is spent instead of re-scaling itself to whatever is left — a bar whose maximum
    // follows its value never appears to move, which is the one thing this readout has to do.
    private void UpdateArmourBar(PlayerController player, float dt)
    {
        if (armourBar == null) return;

        if (playerHealthCache == null) playerHealthCache = player.GetComponent<PlayerHealth>();
        float value = playerHealthCache != null ? playerHealthCache.Armour : 0f;

        bool show = value > 0f;
        if (armourBar.Root.gameObject.activeSelf != show)
            armourBar.Root.gameObject.SetActive(show);

        if (!show) { armourPeak = 0f; return; }

        armourPeak = Mathf.Max(armourPeak, value, armourBarReference);
        armourBar.SetValue(value, armourPeak);
        armourBar.Tick(dt);
    }

    private PlayerController ResolvePlayer()
    {
        if (playerController != null) return playerController;
        if (GameManager.instance != null) playerController = GameManager.instance.player;
        return playerController;
    }

    // Gold keeps its existing GoldDisplay object (GoldUI drives the number through an event) — it
    // just gets moved onto the third row and its coin / number pulled tight, since the old spacing
    // was built to fill a painted cell that no longer exists.
    private void LayOutGoldRow()
    {
        if (goldDisplay == null) return;

        var rt = goldDisplay.GetComponent<RectTransform>();
        if (rt == null) return;

        // Span the row from the panel's own origin so coordinates inside GoldDisplay match the
        // other two rows exactly — the coin can then use the same iconCenterX as the other icons.
        //
        // ⚠️ The row is now exactly one HudChip, because gold and scrap must read as the same kind
        // of readout (designer 2026-08-09: "they should not look too different from each other").
        // Health and Shift stay BARS — they have a maximum and a fill is the honest shape for that.
        // Gold and scrap are unbounded counts, so both are plates with a number.
        float h = HudChip.Height;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(HudChip.Width, h);
        rt.anchoredPosition = new Vector2(0f, -goldRowY);

        HudChip.Build(rt);

        // Search children only — a stray Image on GoldDisplay itself would otherwise be mistaken
        // for the coin and the real icon would never move.
        Image coin = null;
        foreach (var img in goldDisplay.GetComponentsInChildren<Image>(true))
            if (img.gameObject != goldDisplay) { coin = img; break; }

        if (coin != null)
        {
            var crt = coin.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(goldIconSize, goldIconSize);
            crt.anchoredPosition = new Vector2(iconCenterX, -h * 0.5f);
            coin.preserveAspect = true;

            // The coin is an existing scene object, so it never got the drop shadow the other two
            // icons build for themselves — without it row 3 read as flatter than rows 1 and 2.
            var shadow = MakeIconImage("GoldIconShadow", coin.sprite, goldIconSize, h * 0.5f,
                                       iconShadowOffset, new Color(0f, 0f, 0f, 0.5f), rt);
            shadow.transform.SetAsFirstSibling();
        }

        var text = goldDisplay.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            var trt = text.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.sizeDelta = new Vector2(160f, h);
            trt.anchoredPosition = new Vector2(barX, -h * 0.5f);
            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = goldNumberSize;
            // Match the palette gold used elsewhere (sell button, sell values). The scene object
            // carried a saturated yellow that read as a different UI from the two bar numbers
            // sitting directly above it.
            text.color = goldNumberColor;
        }
    }

    // Icon + its own drop shadow, centred on the icon axis and on the row's centre line.
    private void BuildIcon(string name, Sprite sprite, float rowY, float size)
    {
        if (sprite == null) return;

        float centerY = rowY + barGeometry.height * 0.5f;
        MakeIconImage(name + "Shadow", sprite, size, centerY, iconShadowOffset, new Color(0f, 0f, 0f, 0.5f), panelRT);
        MakeIconImage(name, sprite, size, centerY, Vector2.zero, Color.white, panelRT);
    }

    // Centre pivot is what makes this work: the icon's BOX is centred on iconCenterX, so changing an
    // icon's size grows it evenly about the axis instead of pushing it sideways.
    private Image MakeIconImage(string name, Sprite sprite, float size, float centerY, Vector2 nudge,
                                Color color, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(iconCenterX + nudge.x, -(centerY + nudge.y));

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }
}
