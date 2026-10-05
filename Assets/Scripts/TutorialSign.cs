using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A tutorial instruction chalked on the dungeon wall: a row of hand-drawn key boxes over a line or
// two of writing.
//
// THE MATERIAL is the one ExitMarker already established for wayfinding: chalk on stone, somebody's
// scratched directions, a real dungeon-crawling habit. It claims no colour (Salvage.Chalk is a value,
// not a hue) and sits BEHIND the actors, so it reads as part of the room rather than a HUD label
// bolted over it. The key boxes are drawn in the same chalk, deliberately not InteractPrompt's solid
// keycap: that cap means "press this NOW, here", and these are notes about the whole game.
//
// Each sign also doubles as a CHECKPOINT: walking past it moves the respawn point here (see
// TutorialRoom), so a fall or a death costs one section rather than the whole tutorial.
//
// Built procedurally at runtime, like InteractPrompt — edit `keys` and `caption` in the Inspector and
// they take effect on the next Play. In the Scene view a gizmo label shows the text.
public class TutorialSign : MonoBehaviour
{
    [Tooltip("Keys to draw as chalk boxes, separated by spaces: \"A D\", \"1 2 3 4 CLICK\". Empty = " +
             "no key row.")]
    public string keys = "";

    [TextArea(2, 5)]
    [Tooltip("The writing under the keys. Keep it to two short lines — a player reads it mid-stride.")]
    public string caption = "";

    [Tooltip("Height of the sign's BOTTOM edge above the floor, in world units. 2.3 clears the " +
             "player's head (1.7) with room to spare.")]
    public float heightAboveFloor = 2.3f;

    [Tooltip("Widest the writing may run before it wraps, in world units.")]
    public float maxWidth = 6.5f;

    // 1 world unit = 100 canvas px on this canvas.
    private const float CanvasScale = 0.01f;

    // UIType's sizes are SCREEN-canvas pixels (1080 tall over the 14-unit view). Multiplying by this
    // makes a role render at the same on-screen size in the world as it does on a menu, so the signs
    // obey the same type scale as every screen instead of inventing numbers of their own.
    private const float WorldPxPerScreenPx = 100f / (1080f / 14f);

    private const float KeyHeight = 64f;        // canvas px (0.64 world units)
    private const float KeyPadX = 30f;
    private const float KeyGap = 14f;
    private const float RowGap = 16f;

    // The writing is faint from across the room and full strength when you arrive at it. Faint, not
    // hidden: seeing the NEXT sign waiting ahead is part of what makes the room read as a course.
    // Calibrate by screenshot — UI does not pass through the scene's 0.5 light, so chalk here reads
    // brighter than the same value would on a lit sprite.
    private const float FarAlpha = 0.30f;
    private const float NearAlpha = 0.92f;
    private const float NearDist = 4f;
    private const float FarDist = 11f;

    private CanvasGroup group;
    private TutorialRoom room;
    private Transform player;
    private bool passed;
    private float alpha = FarAlpha;

    private static Sprite keyBoxSprite;

    private void Awake()
    {
        room = GetComponentInParent<TutorialRoom>();
        Build();
    }

    private void Start()
    {
        if (GameManager.instance != null && GameManager.instance.player != null)
            player = GameManager.instance.player.transform;
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 here = transform.position;
        Vector3 p = player.position;
        bool sameLevel = Mathf.Abs(p.y - here.y) < 3.5f;

        float dist = sameLevel ? Mathf.Abs(p.x - here.x) : FarDist;
        float target = Mathf.Lerp(NearAlpha, FarAlpha, Mathf.InverseLerp(NearDist, FarDist, dist));
        alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * 2.2f);
        if (group != null) group.alpha = alpha;

        if (!passed && sameLevel && p.x >= here.x - 0.6f)
        {
            passed = true;
            if (room != null) room.ReachCheckpoint(here);
        }
    }

    // ---- construction ----------------------------------------------------------------------------

    private void Build()
    {
        GameObject go = new GameObject("Chalk", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, heightAboveFloor, 0f);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // On the Default layer at order 0: above the backdrop tilemap (Background layer), below the
        // Ground tilemap (order 1). The actors are opaque and sort by depth, and this sits at the
        // room's Z, behind PlayPlane.Z — so the player and enemies always walk IN FRONT of the chalk.
        canvas.sortingLayerName = "Default";
        canvas.sortingOrder = 0;

        group = go.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = alpha;

        RectTransform rootRT = (RectTransform)go.transform;
        rootRT.pivot = new Vector2(0.5f, 0f);
        go.transform.localScale = Vector3.one * CanvasScale;

        // Hand-written, so never quite level. Seeded by position: the same sign always leans the same
        // way, and two signs never lean identically.
        float lean = (Mathf.PerlinNoise(transform.position.x * 0.37f, 0.5f) - 0.5f) * 3f;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, lean);

        float widthPx = maxWidth / CanvasScale;
        float y = 0f;

        // Writing first (bottom), then the keys above it — read top to bottom: keys, then what they do.
        if (!string.IsNullOrEmpty(caption))
        {
            TextMeshProUGUI text = MakeText(rootRT, "Caption", caption.Replace("\\n", "\n"), prose: true);
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.Normal;
            Vector2 pref = text.GetPreferredValues(text.text, widthPx, 0f);
            RectTransform rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(widthPx, pref.y);
            rt.anchoredPosition = new Vector2(0f, y);
            y += pref.y + RowGap;
        }

        string[] tokens = string.IsNullOrWhiteSpace(keys)
            ? new string[0]
            : keys.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0) BuildKeyRow(rootRT, tokens, y);
    }

    private void BuildKeyRow(RectTransform parent, string[] tokens, float y)
    {
        // Measure every box first so the row can be centred as a whole.
        var labels = new List<TextMeshProUGUI>();
        var widths = new List<float>();
        float total = 0f;
        foreach (string token in tokens)
        {
            TextMeshProUGUI label = MakeText(parent, "Key " + token, token.ToUpperInvariant(), prose: false);
            float w = Mathf.Max(KeyHeight, label.GetPreferredValues(label.text).x + KeyPadX);
            labels.Add(label);
            widths.Add(w);
            total += w;
        }
        total += KeyGap * (tokens.Length - 1);

        float x = -total / 2f;
        for (int i = 0; i < tokens.Length; i++)
        {
            float w = widths[i];

            GameObject box = new GameObject("Box " + tokens[i], typeof(RectTransform));
            box.transform.SetParent(parent, false);
            RectTransform brt = (RectTransform)box.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(w, KeyHeight);
            brt.anchoredPosition = new Vector2(x + w / 2f, y + KeyHeight / 2f);
            // Each box drawn by the same unsteady hand.
            float tilt = (Mathf.PerlinNoise(transform.position.x * 0.9f + i * 1.7f, 3.1f) - 0.5f) * 5f;
            brt.localRotation = Quaternion.Euler(0f, 0f, tilt);

            Image img = box.AddComponent<Image>();
            img.sprite = KeyBoxSprite();
            img.type = Image.Type.Sliced;   // ⚠️ Simple would stretch the corners on the wide boxes
            img.color = Salvage.Chalk;
            img.raycastTarget = false;

            RectTransform lrt = labels[i].rectTransform;
            lrt.SetParent(brt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            labels[i].alignment = TextAlignmentOptions.Midline;

            x += w + KeyGap;
        }
    }

    private static TextMeshProUGUI MakeText(RectTransform parent, string name, string content, bool prose)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (prose) UIType.ApplyProse(t, TextRole.Label);
        else UIType.Apply(t, TextRole.Label);
        t.fontSize *= WorldPxPerScreenPx;
        t.color = Salvage.Chalk;
        t.text = content;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    // A chalk-drawn box: a slightly rounded outline whose stroke is broken up by grain, the way chalk
    // skips over stone. White, tinted by Image.color; 9-sliced so wide keys (SPACE, CLICK) keep their
    // corners.
    private static Sprite KeyBoxSprite()
    {
        if (keyBoxSprite != null) return keyBoxSprite;

        const int S = 48;
        const float inset = 4f, stroke = 3.4f, radius = 8f;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float half = S / 2f - inset;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float px = x + 0.5f - S / 2f, py = y + 0.5f - S / 2f;
                float ax = Mathf.Abs(px) - (half - radius);
                float ay = Mathf.Abs(py) - (half - radius);
                float outside = Mathf.Sqrt(Mathf.Max(ax, 0f) * Mathf.Max(ax, 0f) + Mathf.Max(ay, 0f) * Mathf.Max(ay, 0f));
                float inside = Mathf.Min(Mathf.Max(ax, ay), 0f);
                float sd = outside + inside - radius;                  // <0 inside the outline

                float band = Mathf.Clamp01(stroke / 2f - Mathf.Abs(sd) + 0.5f);
                float grain = Mathf.Lerp(0.45f, 1f, Mathf.PerlinNoise(x * 0.42f, y * 0.42f));
                float skip = Mathf.PerlinNoise(x * 0.11f + 7.3f, y * 0.11f + 2.9f) < 0.28f ? 0.55f : 1f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, band * grain * skip));
            }
        tex.Apply();

        keyBoxSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                                     SpriteMeshType.FullRect, new Vector4(16f, 16f, 16f, 16f));
        keyBoxSprite.name = "TutorialChalkKeyBox";
        return keyBoxSprite;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 top = transform.position + Vector3.up * heightAboveFloor;
        Gizmos.color = new Color(0.93f, 0.90f, 0.82f, 0.6f);
        Gizmos.DrawLine(transform.position, top);
        string preview = (string.IsNullOrEmpty(keys) ? "" : "[" + keys + "] ") +
                         (caption.Length > 40 ? caption.Substring(0, 40) + "…" : caption);
        UnityEditor.Handles.Label(top, preview);
    }
#endif
}
