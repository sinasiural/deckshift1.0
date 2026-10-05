using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The name plaque riding on top of each card in the HAND (2026-10-02).
//
// WHY: a playtester "could not understand which cards they have in their hand without having to hover
// them and read". The hand is sunk to show only the top half of each card (the floor lives at the
// bottom of the screen in a platformer), and the card's name plate is on the bottom half. So the name
// moves to the top, onto a plaque above the card, where the eye already goes to read the two numbers.
// It sits in the row the "[1]" key hint used to occupy, and carries the key number too.
//
// ⚠️ THE PLAQUE IS BUILT FROM THE CARD'S OWN FRAME, NOT DRAWN AS UI. The first version was a flat dark
// rectangle with gold text; the designer's verdict was that the idea was right and the look was off —
// it was a UI box pasted onto pixel art. The card frame (CardBackArt, one per rarity) already has
// exactly the parts of a name plaque: an outline, a granite band, a bracket at each corner and a
// black plate with a rim in the rarity's colour. This cuts the frame's TOP EDGE and its NAME PLATE
// and stacks them, so the plaque is literally made of the card it labels, in its rarity's colours,
// at the same pixel size.
//
// The row ranges below are measured on the canonical 124x204 frame (bgcommon/bguncommon/bgrare/bgepic
// share one layout) and expressed in the SPRITE's own rows, top-down: the sprite is cut at x2 y2
// 120x201 inside the file. If the frame art is redrawn, re-measure them.
public class CardNamePlate : MonoBehaviour
{
    // Source rows (top-down, inclusive) copied from the frame into the plaque, in this order:
    //   the frame's top edge (outline, corner brackets, granite band) ...
    //   ... then the whole name plate: outline, rim, black interior, rim, granite, bottom edge.
    // ⚠️ The plate keeps ALL 16 interior rows. A first pass cut it to 12 to keep the plaque short,
    // and the names came out too small to read at a glance, which is the plaque's only job.
    private static readonly Vector2Int[] SourceRows =
    {
        new Vector2Int(3, 9),
        new Vector2Int(174, 199),
    };

    // The plate's black interior in the plaque, in plaque pixels: columns, and rows from the TOP.
    private const int INTERIOR_X0 = 10, INTERIOR_X1 = 110;
    private const int INTERIOR_TOP = 9, INTERIOR_ROWS = 16;

    // The key number's cell at the left of the plate, closed off by a one-pixel rule in the rim's
    // colour (drawn into the plaque itself, so it is pixel art like everything around it).
    private const int KEY_CELL = 13;

    // Room left above the card's frame: the charge ball and the Shift crystal stand ~3 px proud of it.
    private const float GAP_PX = 4f;

    private static readonly Color NameGold = new Color(0.85f, 0.72f, 0.36f, 1f);   // the card's title gold
    private static readonly Color KeySilver = new Color(0.72f, 0.74f, 0.78f, 1f);

    private static readonly Dictionary<Sprite, Sprite> plaques = new Dictionary<Sprite, Sprite>();

    private RectTransform host;      // the card art, whose drawn size the plaque follows
    private Image cardArt;
    private RectTransform rect;
    private Image plate;
    private TextMeshProUGUI keyText;
    private TextMeshProUGUI nameText;
    private Vector2 laidOutFor;

    public static CardNamePlate For(Image cardArt)
    {
        if (cardArt == null) return null;
        Transform existing = cardArt.rectTransform.Find("NamePlate");
        if (existing != null) return existing.GetComponent<CardNamePlate>();

        var go = new GameObject("NamePlate", typeof(RectTransform));
        var p = go.AddComponent<CardNamePlate>();
        p.Build(cardArt);
        return p;
    }

    private void Build(Image art)
    {
        cardArt = art;
        host = art.rectTransform;
        rect = (RectTransform)transform;
        rect.SetParent(host, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);

        plate = gameObject.AddComponent<Image>();
        plate.raycastTarget = false;

        keyText = MakeLabel("Key", KeySilver);
        nameText = MakeLabel("Name", NameGold);
    }

    private TextMeshProUGUI MakeLabel(string name, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(rect, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var t = go.AddComponent<TextMeshProUGUI>();
        UIType.Apply(t, TextRole.Label);
        t.enableAutoSizing = false;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.alignment = TextAlignmentOptions.Center;
        t.color = colour;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Shows the plaque for this card in hand slot <paramref name="index"/> (0-based).</summary>
    public void Show(RuntimeCard card, int index)
    {
        if (card == null || card.cardData == null) { Hide(); return; }

        CardBackArt frames = CardBackArt.Get();
        Sprite frame = frames != null ? frames.For(card.cardData.rarity) : null;
        Sprite built = frame != null ? PlaqueFrom(frame) : null;
        if (built == null) { Hide(); return; }

        plate.sprite = built;
        keyText.text = (index + 1).ToString();
        nameText.text = card.cardData.cardName.ToUpperInvariant();
        gameObject.SetActive(true);
        laidOutFor = Vector2.zero;   // force a layout pass for the new text
        Layout();
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (host != null && host.rect.size != laidOutFor) Layout();
    }

    // Everything is placed in plaque PIXELS converted through the card's drawn width, so the plaque's
    // pixels are exactly the size of the card's pixels. ⚠️ Measured against the DRAWN art, not the
    // host rect: the art is preserveAspect and letterboxes inside its 200x300 box (CardFace.DrawnArtSize).
    private void Layout()
    {
        if (host == null || plate.sprite == null) return;
        Vector2 size = host.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;
        laidOutFor = size;

        Sprite art = cardArt.sprite;
        float aspect = art != null && art.rect.height > 0f ? art.rect.width / art.rect.height : size.x / size.y;
        float drawnH = Mathf.Min(size.y, size.x / aspect);
        float drawnW = drawnH * aspect;

        Rect pr = plate.sprite.rect;
        float px = drawnW / pr.width;              // one plaque pixel, in the card's local units

        rect.sizeDelta = new Vector2(drawnW, pr.height * px);
        rect.anchoredPosition = new Vector2(0f, drawnH * 0.5f + GAP_PX * px);

        // The interior, in local units from the plaque's bottom-left corner.
        float x0 = INTERIOR_X0 * px, x1 = INTERIOR_X1 * px;
        float y1 = (pr.height - INTERIOR_TOP) * px;
        float y0 = y1 - INTERIOR_ROWS * px;
        float midY = (y0 + y1) * 0.5f;
        // Measured on screen: at 0.78 the display face's capitals filled only ~60% of the plate and
        // short names looked lost in it. Long names are fitted to the width below either way.
        float textH = (y1 - y0) * 0.92f;

        // The key number in its cell at the left; the name centred in the rest, past the rule.
        float keyW = KEY_CELL * px;
        float nameX0 = x0 + keyW + px;
        Place(keyText, new Vector2(x0 + keyW * 0.5f, midY), new Vector2(keyW, textH));
        Place(nameText, new Vector2((nameX0 + x1) * 0.5f, midY), new Vector2(x1 - nameX0, textH));

        keyText.fontSize = textH * 0.85f;
        FitWidth(nameText, textH, x1 - nameX0 - 4f * px);
    }

    private static void Place(TMP_Text t, Vector2 centre, Vector2 size)
    {
        var rt = t.rectTransform;
        rt.sizeDelta = size;
        rt.anchoredPosition = centre;
    }

    // ⚠️ DETERMINISTIC, not enableAutoSizing (which settles over several frames — see the UI skill).
    // Every name gets the same size; only one too long to fit at it is shrunk, until it does.
    private static void FitWidth(TMP_Text t, float size, float maxWidth)
    {
        t.fontSize = size;
        float w = t.GetPreferredValues(t.text, 10000f, 10000f).x;
        if (w > maxWidth && w > 0f) t.fontSize = size * maxWidth / w;
    }

    // ---- building the plaque out of the frame --------------------------------------------------

    private static Sprite PlaqueFrom(Sprite frame)
    {
        if (plaques.TryGetValue(frame, out Sprite cached) && cached != null) return cached;

        Color32[] src = StarArt.ReadPixels(frame, out int w, out int h);
        if (src == null) return null;

        var rows = new List<int>();
        foreach (Vector2Int r in SourceRows)
            for (int t = r.x; t <= r.y; t++)
                if (t >= 0 && t < h) rows.Add(t);

        int H = rows.Count;
        var px = new Color32[w * H];
        for (int i = 0; i < H; i++)
        {
            int srcY = h - 1 - rows[i];   // source is bottom-up
            int dstY = H - 1 - i;         // so is the plaque
            System.Array.Copy(src, srcY * w, px, dstY * w, w);
        }

        // The frame's top edge carries two small bumps (where the card's medallions sit). On the plaque
        // they are just stray pixels, so the top row keeps only its corners.
        int top = H - 1;
        for (int x = 12; x < w - 12; x++) px[top * w + x] = new Color32(0, 0, 0, 0);

        // The rule closing off the key number's cell, in the plate rim's own colour (sampled from the
        // rim row, mid-width, so it follows the rarity: grey, white, gold or magenta).
        int rimRow = H - 1 - (INTERIOR_TOP - 1);
        Color32 rim = px[rimRow * w + w / 2];
        int ruleX = INTERIOR_X0 + KEY_CELL;
        for (int t = INTERIOR_TOP + 2; t < INTERIOR_TOP + INTERIOR_ROWS - 2; t++)
            px[(H - 1 - t) * w + ruleX] = rim;

        var tex = new Texture2D(w, H, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = frame.name + " (name plaque)" };
        tex.SetPixels32(px);
        tex.Apply();

        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, H), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
        plaques[frame] = s;
        return s;
    }
}
