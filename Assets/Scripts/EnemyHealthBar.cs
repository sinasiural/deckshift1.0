using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Prefab yapısı: boş GameObject + bu script. Canvas ve UI child'ları Awake'te otomatik oluşturulur.
// EnemyHealth.healthBarPrefab alanına sürükle.
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Font (Inspector'dan ver)")]
    public TMP_FontAsset healthFont;

    private Transform followTarget;
    private Vector3 worldOffset;
    private bool initialized;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Image fillImmediate;
    private Image fillDelayed;
    private TextMeshProUGUI healthText;

    private float targetFill = 1f;
    private float delayedFill = 1f;
    private float currentHP, maxHP;

    private static Sprite cachedWhiteSprite;

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

    const float CANVAS_SCALE = 0.01f;
    const float BAR_HEIGHT_PX = 24f;
    const float DELAYED_SPEED = 2f;

    void Awake()
    {
        BuildCanvas();
    }

    void Start()
    {
        GameSettings.OnChanged += ApplyVisibility;
        ApplyVisibility();
    }

    void OnDestroy()
    {
        GameSettings.OnChanged -= ApplyVisibility;
    }

    // ⚠️ THE BAR IS ALWAYS ON, OR ENTIRELY OFF — there is no in-between and no fade (designer
    // 2026-08-11). It used to start invisible, appear only when the enemy was damaged, and fade out
    // three seconds later; the only setting toggled the NUMBERS on top of it. That is the opposite
    // of what was wanted: the bar is meant to be readable at a glance the whole time, and the switch
    // is meant to remove it completely for players who find it cluttered.
    //
    // Disabling the Canvas rather than zeroing the CanvasGroup means a hidden bar costs no draw
    // calls at all, which matters with one of these per enemy in the room.
    void ApplyVisibility()
    {
        if (canvas != null) canvas.enabled = GameSettings.EnemyHealthBars && !concealed;
    }

    // ⚠️ THE ONE EXCEPTION to "always on": a DISGUISED enemy (the Mimic, while it is still a chest).
    // A health bar hanging over one chest of three is the disguise given away (2026-09-28, the
    // Ossuary's treasury). Cleared the moment it reveals itself, so a revealed Mimic's bar is
    // always on like every other enemy's.
    private bool concealed;
    public void SetConcealed(bool value)
    {
        concealed = value;
        ApplyVisibility();
    }

    void LateUpdate()
    {
        // ⚠️ THE BAR OUTLIVES ITS ENEMY UNLESS IT CLEANS ITSELF UP. It is instantiated with NO
        // PARENT (it has to be, so it can sit in front of the enemy in Z without inheriting its
        // scale or flips), which makes it a scene-root object. EnemyHealth.Die() destroys it — but
        // an enemy that vanishes any OTHER way never runs Die(), and the commonest of those is the
        // room itself being destroyed at a room change. The bars then hung around in world space
        // forever and turned up floating in the next room, and in the hub.
        //
        // Owning its own lifetime here fixes every such path at once, including ones that don't
        // exist yet, which a cleanup list in LevelManager could not.
        // The `initialized` guard matters: Initialize() is called from EnemyHealth.Start(), a beat
        // after this object is instantiated. Without it, a bar whose owner hasn't wired it up yet
        // would delete itself on its very first frame.
        if (initialized && followTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = followTarget.position + worldOffset;

        // Alt katman (delayed fill) mevcut HP'ye smooth lerp ile yaklaşır
        if (!Mathf.Approximately(delayedFill, targetFill))
        {
            delayedFill = Mathf.Lerp(delayedFill, targetFill, Time.deltaTime * DELAYED_SPEED);
            if (Mathf.Abs(delayedFill - targetFill) < 0.001f) delayedFill = targetFill;
            fillDelayed.fillAmount = delayedFill;
        }
    }

    public void Initialize(Transform enemy, Vector2 offset, float worldWidth)
    {
        followTarget = enemy;
        initialized = true;
        SetBarWidth(worldWidth);

        // Match the enemy's own renderer for sorting. Prefer a SpriteRenderer; Cainos monsters
        // have only a SkinnedMeshRenderer.
        Renderer enemyRenderer = enemy.GetComponentInChildren<SpriteRenderer>();
        if (enemyRenderer == null) enemyRenderer = enemy.GetComponentInChildren<SkinnedMeshRenderer>();

        // Sit the bar a solid margin IN FRONT of that renderer in Z. Cainos monsters render as an
        // OPAQUE SkinnedMeshRenderer pulled toward the camera; an opaque mesh writes depth and was
        // occluding the bar's Image fills (the TMP number, which draws on top, still showed — that's
        // why sprite-based enemies like AeroBat worked but mesh-based ones didn't). A fixed -0.1 was
        // too shallow to clear the mesh.
        float frontZ = -0.5f;
        if (enemyRenderer != null)
        {
            canvas.sortingLayerID = enemyRenderer.sortingLayerID;
            canvas.sortingOrder = enemyRenderer.sortingOrder + 10;
            frontZ = (enemyRenderer.transform.position.z - enemy.position.z) - 1f;
        }

        worldOffset = new Vector3(offset.x, offset.y, frontZ);

        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;
    }

    public void SetHealth(float current, float max)
    {
        currentHP = current;
        maxHP = max;
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        // Üst katman anında düşer
        targetFill = ratio;
        fillImmediate.fillAmount = ratio;

        UpdateText();
    }

    void SetBarWidth(float worldWidth)
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(worldWidth / CANVAS_SCALE, BAR_HEIGHT_PX);
    }

    void UpdateText()
    {
        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";
    }

    void BuildCanvas()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        gameObject.AddComponent<CanvasScaler>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 1f;   // always visible; the setting switches the whole Canvas instead

        RectTransform rootRT = GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(120f, BAR_HEIGHT_PX);
        transform.localScale = Vector3.one * CANVAS_SCALE;

        // 1px siyah border
        Image border = MakeChildImage("Border", rootRT, Color.black);
        FillRect(border.rectTransform, 0f);

        // Alt katman: açık kırmızı/turuncu (delayed — hasar boyutunu gösterir)
        fillDelayed = MakeChildImage("FillDelayed", rootRT, new Color(0.9f, 0.35f, 0.15f));
        SetFillImage(fillDelayed);
        FillRect(fillDelayed.rectTransform, 1f);

        // Üst katman: koyu kırmızı (immediate — gerçek HP)
        fillImmediate = MakeChildImage("FillImmediate", rootRT, new Color(0.8f, 0.133f, 0.133f));
        SetFillImage(fillImmediate);
        FillRect(fillImmediate.rectTransform, 1f);

        // Sayı metni — bar'ın üstünde ortalanmış
        GameObject textGO = MakeChild("HealthText", rootRT);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 1f);
        textRT.anchorMax = new Vector2(1f, 1f);
        textRT.pivot = new Vector2(0.5f, 0f);
        textRT.sizeDelta = new Vector2(0f, 24f);
        textRT.anchoredPosition = new Vector2(0f, 4f);

        healthText = textGO.AddComponent<TextMeshProUGUI>();
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.fontSize = 14f;
        healthText.color = Color.white;
        healthText.enableWordWrapping = false;
        if (healthFont != null) healthText.font = healthFont;
    }

    static Image MakeChildImage(string name, RectTransform parent, Color color)
    {
        GameObject go = MakeChild(name, parent);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = GetWhiteSprite();
        return img;
    }

    static GameObject MakeChild(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void FillRect(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void SetFillImage(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
    }
}
