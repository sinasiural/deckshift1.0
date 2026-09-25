using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The boss's life, across the top of the screen. Built procedurally (the prefab is an empty
// GameObject carrying this script); spawned by the boss in its Start(), bound to an EnemyHealth, and
// removes itself when the boss dies.
//
// ⚠️ REBUILT 2026-09-07. The designer's verdict on the old one was "not good at all", and reading it
// back that was three separate faults, not a taste disagreement:
//
//   1. IT WAS THE MOSS KNIGHT'S BAR WITH A DIFFERENT NAME ON IT. Every default here is his —
//      verdigris fill, pale acid chunk, oxidized bronze frame, and `bossName` literally defaulting
//      to "The Moss Knight". Both bosses pointed at the SAME prefab asset, so the ninja fought under
//      a lime-green bar that meant nothing about him. With ~10 bosses planned this scales badly.
//   2. IT OVERLAPPED THE RELIC BAR. Measured on screen: the relic sockets end at canvas y −68 and
//      this started at −54, so the bar's top edge cut through the relic icons. Reported separately
//      by the designer and deferred; it is the same job as this one.
//   3. IT WAS A GLOSSY WEB WIDGET. A chunky bronze border, a white bevel strip across the top and a
//      shadow across the bottom — the exact "competent, safe, screams AI" look the whole FlatUI /
//      Salvage effort exists to kill, and the loudest thing on a dark dungeon screen.
//
// ⚠️ THE FIX FOR (3) IS NOT A NEW LOOK — IT IS THE ONE THE PLAYER'S OWN BARS ALREADY USE.
// `ResourceBarUI` (HP and Shift) was converted long ago and speaks a specific vocabulary: a soft
// shadow, a recessed track, discrete segment CELLS with real gaps the track shows through, fine pip
// ticks, and a chamfered `FlatUI.Outline` frame. The boss bar was the last readout in the game still
// speaking the old language. Consistency lives in the treatment (Salvage §1) — so this is the same
// object as your health bar, scaled up and handed to the enemy.
public class BossHealthBar : MonoBehaviour
{
    [Header("Identity")]
    public string bossName = "";
    [Tooltip("LEGACY — leave empty. The name routes through UIType.Display() like every other " +
             "label in the game; a per-bar font override is how a screen falls out of the type system.")]
    public TMP_FontAsset nameFont;

    [Header("Layout (reference 1920x1080)")]
    public float barWidth = 900f;
    public float barHeight = 26f;
    [Tooltip("⚠️ Distance from the top of the screen. MUST clear the relic bar, which occupies " +
             "canvas y −16 to −68 — the old 54 put this bar straight through the relic icons.")]
    public float topOffset = 84f;
    [Tooltip("How many cells the bar is divided into. ⚠️ This is IDENTITY, not decoration: a boss " +
             "made of armoured mass wants a few fat segments, a fragile one wants many fine ones.")]
    public int segmentCount = 10;
    [Tooltip("Gap between cells. The dark track shows through, so they read as separate plates.")]
    public float segmentGap = 3f;

    [Header("Colors")]
    public Color fillColor = new Color(0.42f, 0.74f, 0.33f);       // HP — verdigris green
    [Tooltip("The trailing chunk revealed behind a hit, before it drains away. This is the colour " +
             "of DAMAGE on this boss — the thing the player sees every time they connect.")]
    public Color delayedColor = new Color(0.83f, 0.95f, 0.55f);
    public Color warnColor = new Color(0.95f, 0.35f, 0.15f);       // low-HP / flash tint
    public Color backgroundColor = new Color(0.06f, 0.09f, 0.06f);
    public Color frameColor = new Color(0.34f, 0.3f, 0.18f);       // oxidized bronze
    public Color outlineColor = new Color(0.02f, 0.03f, 0.02f);
    public Color nameTopColor = new Color(0.96f, 0.98f, 0.88f);
    public Color nameBottomColor = new Color(0.55f, 0.76f, 0.45f);

    private const float INTRO_DURATION = 0.7f;
    private const float DELAYED_SPEED = 0.5f;
    private const float CHUNK_HOLD = 0.35f;
    private const float FLASH_DECAY = 7f;
    private const float SHAKE_DECAY = 26f;
    private const float FADE_SPEED = 3f;
    private const int OUTLINE = 2;
    private const int FRAME = 3;

    private EnemyHealth health;
    private bool subscribed;

    private CanvasGroup canvasGroup;
    private RectTransform panel;
    private Vector2 panelBasePos;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI nameShadow;

    private struct Cell { public Image delayed, immediate; }
    private Cell[] cells;

    private float displayRatio = 1f;
    private float delayedFill = 1f;
    private float chunkHoldTimer;
    private float flash;
    private float shake;
    private bool intro = true;
    private float introT;
    private bool dying;

    private static Sprite cachedWhiteSprite;

    void Awake()
    {
        BuildUI();
    }

    // Called by the boss right after it wakes. Binds the bar to the boss's health.
    public void Initialize(EnemyHealth bossHealth, string displayName)
    {
        health = bossHealth;
        if (!string.IsNullOrEmpty(displayName)) bossName = displayName;
        SetName(bossName);

        if (health != null && !subscribed)
        {
            health.OnDamaged += HandleDamaged;
            subscribed = true;
        }
        intro = true;
        introT = 0f;
    }

    void OnDestroy()
    {
        if (health != null && subscribed) health.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged()
    {
        flash = 1f;
        shake = 7f;
        chunkHoldTimer = CHUNK_HOLD;
    }

    void Update()
    {
        if (dying)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.deltaTime * FADE_SPEED);
            if (canvasGroup.alpha <= 0f) Destroy(gameObject);
            return;
        }

        // health == null means the boss GameObject was destroyed (it died) — bow out.
        if (health == null)
        {
            dying = true;
            return;
        }

        float max = health.maxHealth;
        float ratio = max > 0f ? Mathf.Clamp01(health.CurrentHealth / max) : 0f;

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, Time.deltaTime * FADE_SPEED);

        if (intro)
        {
            // Dramatic fill-up as the boss appears.
            introT += Time.deltaTime / INTRO_DURATION;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(introT), 3f);   // ease-out cubic
            displayRatio = Mathf.Lerp(0f, ratio, e);
            delayedFill = displayRatio;
            if (introT >= 1f) { intro = false; displayRatio = ratio; }
        }
        else
        {
            displayRatio = ratio;   // real HP snaps instantly
            if (delayedFill > displayRatio)
            {
                if (chunkHoldTimer > 0f) chunkHoldTimer -= Time.deltaTime;
                else delayedFill = Mathf.MoveTowards(delayedFill, displayRatio, Time.deltaTime * DELAYED_SPEED);
            }
            else delayedFill = displayRatio;
        }

        // Low-HP danger pulse + white hit flash, composited onto the fill color.
        float danger = displayRatio < 0.3f ? (1f - displayRatio / 0.3f) : 0f;
        float pulse = danger > 0f ? (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f)) * danger : 0f;
        if (flash > 0f) flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * FLASH_DECAY);

        Color c = Color.Lerp(fillColor, warnColor, pulse * 0.6f);
        c = Color.Lerp(c, Color.white, flash);

        ApplyCells(displayRatio, Mathf.Max(delayedFill, displayRatio), c);

        if (nameText != null)
        {
            Color nc = Color.Lerp(Color.white, warnColor, pulse * 0.6f);
            nameText.color = nc;
        }

        // Hit shake on the bar.
        if (shake > 0f)
        {
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * SHAKE_DECAY);
            panel.anchoredPosition = panelBasePos + new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * shake;
        }
        else if (panel.anchoredPosition != panelBasePos)
        {
            panel.anchoredPosition = panelBasePos;
        }
    }

    /// <summary>
    /// Spreads one 0..1 ratio across the cells. Each cell owns an equal slice, so a cell is full,
    /// empty, or the one currently draining — which is what makes the bar empty plate by plate
    /// instead of sliding a single edge across a ruled rectangle.
    /// </summary>
    private void ApplyCells(float ratio, float delayedRatio, Color fillCol)
    {
        if (cells == null || cells.Length == 0) return;
        int n = cells.Length;
        for (int i = 0; i < n; i++)
        {
            float lo = (float)i / n;
            float span = 1f / n;
            // Clamp01 of "how far into MY slice the level is" — 0 below me, 1 above me.
            float mine = Mathf.Clamp01((ratio - lo) / span);
            float mineDelayed = Mathf.Clamp01((delayedRatio - lo) / span);

            cells[i].immediate.fillAmount = mine;
            cells[i].delayed.fillAmount = Mathf.Max(mine, mineDelayed);
            cells[i].immediate.color = fillCol;
        }
    }

    private void SetName(string n)
    {
        string up = string.IsNullOrEmpty(n) ? "" : n.ToUpperInvariant();
        if (nameText != null) nameText.text = up;
        if (nameShadow != null) nameShadow.text = up;
    }

    void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // ⚠️ MATCH ON HEIGHT (1), NOT 0.5. Every other CanvasScaler in the project is 1, because the
        // camera is height-anchored (orthographicSize 7 ⇒ 14 world units tall at every aspect). This
        // was the only canvas in the game disagreeing, so at 21:9 the boss bar scaled differently
        // from the relic bar it sits under and from the HUD around it.
        scaler.matchWidthOrHeight = 1f;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;

        RectTransform rootRT = GetComponent<RectTransform>();

        // The panel is now just a transparent frame of reference — the cells below carry the surface.
        GameObject panelGO = MakeChild("BarPanel", rootRT);
        panel = panelGO.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.sizeDelta = new Vector2(barWidth, barHeight);
        panel.anchoredPosition = new Vector2(0f, -topOffset);
        panelBasePos = panel.anchoredPosition;

        // Drop shadow, so the bar sits ON the scene rather than being printed onto it — the same
        // first layer every player resource bar starts with.
        Image drop = MakeChildImage("Shadow", panel, new Color(0f, 0f, 0f, 0.55f));
        drop.sprite = RelicUISprites.SoftShadow();
        RectTransform dr = drop.rectTransform;
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
        dr.offsetMin = new Vector2(-10f, -14f);
        dr.offsetMax = new Vector2(10f, 6f);

        // The recessed track. It runs the FULL width and is what shows through the gaps between
        // cells — which is what makes them read as separate plates rather than a ruled rectangle.
        Image track = MakeChildImage("Track", panel, outlineColor);
        FillRect(track.rectTransform, 0f);

        // ⚠️ THE FILL IS SPLIT ACROSS CELLS, NOT ONE BAR WITH LINES DRAWN ON IT. The old version was a
        // single Filled image with notch ticks laid over it, so the notches were decoration that the
        // fill slid underneath. Real cells mean the bar empties plate by plate, and the gaps stay
        // dark at every fill level.
        int segs = Mathf.Max(1, segmentCount);
        cells = new Cell[segs];
        float gap = Mathf.Max(0f, segmentGap);
        float cellW = (barWidth - gap * (segs - 1)) / segs;

        for (int i = 0; i < segs; i++)
        {
            GameObject holder = MakeChild("Cell" + i, panel);
            RectTransform hr = holder.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(0f, 1f);
            hr.pivot = new Vector2(0f, 0.5f);
            hr.sizeDelta = new Vector2(cellW, -FRAME * 2f);
            hr.anchoredPosition = new Vector2(i * (cellW + gap), 0f);

            FillRect(MakeChildImage("Socket", hr, backgroundColor).rectTransform, 0f);

            var del = MakeChildImage("Delayed", hr, delayedColor);
            SetFillImage(del); FillRect(del.rectTransform, 0f);

            var imm = MakeChildImage("Fill", hr, fillColor);
            SetFillImage(imm); FillRect(imm.rectTransform, 0f);

            cells[i] = new Cell { delayed = del, immediate = imm };
        }

        // Chamfered frame over the whole run, in the boss's own metal. Cut plate, not a CSS border.
        Image frame = MakeChildImage("Frame", panel, frameColor);
        frame.sprite = FlatUI.Outline(5, 2);
        frame.type = Image.Type.Sliced;
        FillRect(frame.rectTransform, -2f);

        // Boss name (shadow clone behind, gradient face in front), centered below the bar.
        nameShadow = MakeName("BossNameShadow", panel, new Vector2(2f, -2f));
        nameShadow.color = new Color(0f, 0f, 0f, 0.8f);
        nameShadow.enableVertexGradient = false;

        nameText = MakeName("BossName", panel, Vector2.zero);
        nameText.enableVertexGradient = true;
        nameText.colorGradient = new VertexGradient(nameTopColor, nameTopColor, nameBottomColor, nameBottomColor);

        SetName(bossName);
    }

    private TextMeshProUGUI MakeName(string name, RectTransform parent, Vector2 pixelOffset)
    {
        GameObject go = MakeChild(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 30f);
        rt.anchoredPosition = new Vector2(pixelOffset.x, -5f + pixelOffset.y);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;

        // ⚠️ THROUGH UIType, NOT A LOCAL FONT FIELD. A per-screen font reference is exactly how the
        // character select shipped in Liberation Sans — anything that opts out of the type system
        // opts out silently. `nameFont` is honoured only as a deliberate override.
        UIType.Apply(t, TextRole.Heading);
        t.characterSpacing = 6f;                       // a boss name is set wide; that is the flourish
        if (nameFont != null) t.font = nameFont;
        return t;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = GetWhiteSprite();
        img.raycastTarget = false;
        return img;
    }

    private static Image MakeChildImage(string name, RectTransform parent, Color color)
    {
        return AddImage(MakeChild(name, parent), color);
    }

    private static GameObject MakeChild(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void FillRect(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetFillImage(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
    }

    private static Sprite GetWhiteSprite()
    {
        if (cachedWhiteSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        }
        return cachedWhiteSprite;
    }
}
