using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The reverse face of a card — what you see when you hover one and it turns over.
//
// ══ REBUILT 2026-09-27 ON THE DESIGNER'S OWN FRAMES ══════════════════════════════════════════════
//
// The first back (2026-08-09) was a procedural gold-on-near-black plate, reasoned from the deck of the
// time, whose fronts were painted gold-on-black. The canonical frame has since replaced almost every
// front with a granite frame in the rarity's colour, so that plate had become the thing its own header
// warned against: a card that visibly stops being the same object halfway through its turn.
//
// The designer drew the fix (Assets/Art/bgcommon, bguncommon, bgrare, bgepic — CardBackArt): the same
// frame as the fronts, one per rarity, with the window and the name plate left empty. Brief: "when we
// hover over the cards we would see those pictures with words on the blank places", and "I also want
// the player to be able to see a bit of the card art when they hover over the card, as well as the
// description."
//
// So the back is printed on the frame of the card's rarity (CardData.rarity), and its window reads,
// top to bottom:
//     a strip of the card's OWN art     so the card you are reading is still the card you hovered
//     the description                   what it does, in the prose face (it is a sentence)
//     SHIFT n   CHARGES a / b           what it costs, how many are left — the front is hidden now
// with the name typed into the plate exactly like the front's.
//
// ⚠️ EVERYTHING IS PLACED IN FRACTIONS OF THE FRAME SPRITE, INSIDE A CHILD THAT KEEPS THE FRAME'S
// ASPECT (`face`, an AspectRatioFitter). The host rect is 200x300 in the hand while the frame is
// 0.597 — the letterbox lesson from CardFace: anything placed against the host lands off the art.
public class CardBack : MonoBehaviour
{
    // Measured on the recovered frames (all four share one geometry), as fractions of the sprite
    // rect (x2 y2 120x201 on the 124x204 canvas), y from the BOTTOM like Unity's anchors.
    private const float WIN_X0 = 0.100f, WIN_X1 = 0.900f;
    private const float WIN_Y0 = 0.1642f, WIN_Y1 = 0.9353f;
    private const float PLATE_X0 = 0.075f, PLATE_X1 = 0.925f;
    private const float PLATE_Y0 = 0.0448f, PLATE_Y1 = 0.1244f;

    // How much of the window the art strip takes, from the top. Enough to recognise the card; the
    // rest is for reading.
    private const float BAND_H = 0.33f;
    // The strip fades into the window over its lower part, so the art sinks into the text area
    // instead of ending on a hard edge that reads as a pasted-in photo.
    private const float FADE_H = 0.15f;

    // The frame's own window fill, sampled from the art: (41,41,41). Baked into the fade texture so
    // the gradient lands on exactly the window's value — a UI tint would go through linear colour
    // space and miss.
    private static readonly Color32 WINDOW = new Color32(41, 41, 41, 255);

    private static readonly Color BODY = new Color(0.90f, 0.88f, 0.84f, 1f);
    private static readonly Color LABEL = new Color(0.60f, 0.57f, 0.52f, 1f);
    private static readonly Color NAME_COLOR = new Color(0.85f, 0.72f, 0.36f, 1f);   // the front plate's gold
    // The resource bar's Shift blue, so a cost on a card back and a cost in the HUD are recognisably
    // the same currency (CardUI uses the same value).
    private static readonly Color SHIFT_BLUE = new Color(0.55f, 0.52f, 0.96f, 1f);
    private static readonly Color CHARGE_COLOR = new Color(0.96f, 0.94f, 0.90f, 1f);

    public TextMeshProUGUI title;
    public TextMeshProUGUI body;
    public TextMeshProUGUI keyHint;
    public TextMeshProUGUI costLabel, costValue;
    public TextMeshProUGUI chargeLabel, chargeValue;

    private RectTransform face;
    private AspectRatioFitter fitter;
    private Image frame;
    private Image artBand;
    private Image fade;

    // Builds the whole face under `parent` (the card ROOT, so it turns with the card) and returns
    // it inactive.
    //
    // ⚠️ The returned object is pre-rotated 180 degrees on Y. CardHoverFlip turns the card by
    // rotating the ROOT, and past 90 degrees every child renders MIRRORED — text included. Cancelling
    // it here means the back reads correctly exactly when it is the face you're looking at.
    public static CardBack Build(RectTransform parent)
    {
        GameObject go = new GameObject("CardBack", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localRotation = Quaternion.Euler(0f, 180f, 0f);

        CardBack back = go.AddComponent<CardBack>();
        back.BuildFace(rt);
        go.SetActive(false);
        return back;
    }

    // ⚠️ THE BACK IS SIZED OFF THE ART, NEVER OFF THE CARD ROOT.
    //
    // A hand card's root is not the card the player sees (it has been rewritten by layout code more
    // than once in this project's history); cardArtImage is the rect that matches the visible card.
    // The back still has to PARENT to the root (that's what turns it), so it copies the art's
    // geometry instead of nesting inside it.
    public void MatchTo(RectTransform art)
    {
        if (art == null) return;

        RectTransform rt = (RectTransform)transform;

        // When the geometry IS our parent — the forge and Blompo build their chips at the size the
        // player sees — copying the parent's own anchors onto a child is meaningless. Fill it.
        if (rt.parent == art)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return;
        }

        rt.anchorMin = art.anchorMin;
        rt.anchorMax = art.anchorMax;
        rt.pivot = art.pivot;
        rt.sizeDelta = art.sizeDelta;
        rt.anchoredPosition = art.anchoredPosition;
    }

    private void BuildFace(RectTransform root)
    {
        // The frame's aspect is set per card in SetContent (all four frames share it today, but a
        // re-drawn frame must not be able to stretch).
        GameObject fgo = new GameObject("Face", typeof(RectTransform));
        face = fgo.GetComponent<RectTransform>();
        face.SetParent(root, false);
        face.anchorMin = Vector2.zero;
        face.anchorMax = Vector2.one;
        face.offsetMin = face.offsetMax = Vector2.zero;
        fitter = fgo.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 120f / 201f;

        frame = AddImage(face, "Frame", null, Color.white);
        Anchor(frame.rectTransform, 0f, 0f, 1f, 1f);

        // The art strip, then the fade that sinks it into the window. Both sit inside the window.
        artBand = AddImage(face, "ArtBand", null, Color.white);
        Anchor(artBand.rectTransform, WIN_X0, WIN_Y1 - BAND_H, WIN_X1, WIN_Y1);

        fade = AddImage(face, "ArtFade", FadeSprite(), Color.white);
        Anchor(fade.rectTransform, WIN_X0, WIN_Y1 - BAND_H, WIN_X1, WIN_Y1 - BAND_H + FADE_H);

        // Key hint on the frame's top band: the lifted card covers its own [n] label in the hand.
        keyHint = AddText(face, "KeyHint", Color.white, 6f, 11f, TextAlignmentOptions.Center, UIType.Display());
        CardFace.ApplyNumberOutline(keyHint);
        Anchor(keyHint.rectTransform, 0.30f, WIN_Y1 + 0.004f, 0.70f, 0.996f);

        // --- The description, which is what the player actually hovered for. ---
        //
        // ⚠️ THE CEILING IS THE DESIGN; the floor is a safety net. A one-line card rendering at
        // twice the size of a wordy one reads as broken, not as emphasis — if a card can't reach the
        // ceiling, shorten the card's text. The floor exists for BLESSED cards, which append two
        // lines of blessing text to an already-full face.
        //
        // Prose face (Pixie), not the display face: card rules are sentences, and the display face
        // has essentially no lowercase — the old back printed every description in capitals.
        //
        // 18 is MEASURED (2026-09-27, GetPreferredValues in the 134x119 box): all 19 descriptions fit
        // at 19; 18 keeps a step of headroom so every unblessed card renders at one identical size.
        body = AddText(face, "Body", BODY, 8f, 18f, TextAlignmentOptions.Center, UIType.Prose());
        body.enableWordWrapping = true;
        Anchor(body.rectTransform, WIN_X0 + 0.025f, 0.245f, WIN_X1 - 0.025f, WIN_Y1 - BAND_H + 0.035f);

        // --- Footer: what it costs, how many are left. One row inside the window's foot. ---
        // Repeats the front on purpose: while the card is turned the front's numbers are hidden,
        // and "show the numbers a decision depends on" has been paid for once already (Blompo).
        costLabel = AddText(face, "CostLabel", LABEL, 6f, 10f, TextAlignmentOptions.Right, UIType.Display());
        Anchor(costLabel.rectTransform, WIN_X0 + 0.02f, WIN_Y0 + 0.012f, 0.34f, 0.232f);
        costValue = AddText(face, "CostValue", SHIFT_BLUE, 8f, 14f, TextAlignmentOptions.Left, UIType.Display());
        Anchor(costValue.rectTransform, 0.36f, WIN_Y0 + 0.012f, 0.47f, 0.232f);

        chargeLabel = AddText(face, "ChargeLabel", LABEL, 6f, 10f, TextAlignmentOptions.Right, UIType.Display());
        Anchor(chargeLabel.rectTransform, 0.49f, WIN_Y0 + 0.012f, 0.68f, 0.232f);
        chargeValue = AddText(face, "ChargeValue", CHARGE_COLOR, 8f, 14f, TextAlignmentOptions.Left, UIType.Display());
        Anchor(chargeValue.rectTransform, 0.70f, WIN_Y0 + 0.012f, WIN_X1 - 0.01f, 0.232f);

        // Tracked out: at this size the display face's letterforms crowd — "SHIFT" read as "SKIFT".
        costLabel.characterSpacing = 6f;
        chargeLabel.characterSpacing = 6f;

        // --- The name, in the plate, set like the front's plate. ---
        title = AddText(face, "Title", NAME_COLOR, 6f, 15f, TextAlignmentOptions.Center, UIType.Display());
        title.fontStyle = FontStyles.Bold;
        Anchor(title.rectTransform, PLATE_X0 + 0.03f, PLATE_Y0, PLATE_X1 - 0.03f, PLATE_Y1);
    }

    // The ordinary fill: card text, Shift cost, charges. Used by every screen that shows a card —
    // the hand, the Scrap Forge and Blompo — so they all read identically. CardUI overrides it for
    // Stagger, whose footer says something else entirely.
    //
    // A blessing is named here too. The mark on the front says a card is blessed; only this says
    // which one, so a forge or Blompo chip that omitted it would be hiding what the player came for.
    public void BindStandard(RuntimeCard card, string keyHint = "")
    {
        if (card == null || card.cardData == null) return;

        string body = card.cardData.description;
        if (card.enhancement != CardEnhancement.None)
            body += $"\n<color=#6BE6D1><b>{CardEnhancements.Name(card.enhancement)}</b></color>\n" +
                    CardEnhancements.Description(card.enhancement);

        string charges = card.isInfinite ? "∞" : $"{card.currentUses}/{card.MaxUses}";

        SetContent(card, body, keyHint,
                   "SHIFT", card.cardData.shiftCost.ToString(), SHIFT_BLUE,
                   "CHARGES", charges);
    }

    // Fills the face. `costOverride` exists for Stagger, whose price is HP and changes every play.
    public void SetContent(RuntimeCard card, string bodyText, string key,
                           string costLabelText, string costText, Color costColor,
                           string chargeLabelText, string chargeText)
    {
        if (card == null || card.cardData == null) return;

        // The frame of the card's rarity. Without the asset the back still reads — a plain dark
        // plate in the same layout — rather than disappearing.
        CardBackArt art = CardBackArt.Get();
        Sprite frameSprite = art != null ? art.For(card.cardData.rarity) : null;
        frame.sprite = frameSprite != null ? frameSprite : FlatUI.Panel(5);
        frame.type = frameSprite != null ? Image.Type.Simple : Image.Type.Sliced;
        frame.color = frameSprite != null ? Color.white : new Color(WINDOW.r / 255f, WINDOW.g / 255f, WINDOW.b / 255f, 1f);
        if (frameSprite != null && frameSprite.rect.height > 0f)
            fitter.aspectRatio = frameSprite.rect.width / frameSprite.rect.height;

        Sprite band = ArtBand(card.cardData.cardArt, BandAspect());
        artBand.sprite = band;
        artBand.enabled = band != null;
        fade.enabled = band != null;

        title.text = card.cardData.cardName.ToUpperInvariant();
        // ⚠️ Pixie has no em dash or middle dot; TMP draws them from its fallback font, visibly
        // wider and heavier ("portals —— the first"). Shown as plain punctuation, data untouched.
        body.text = bodyText != null ? bodyText.Replace('—', '-').Replace('·', '-') : "";
        keyHint.text = key;

        costLabel.text = costLabelText;
        costValue.text = costText;
        costValue.color = costColor;

        chargeLabel.text = chargeLabelText;
        chargeValue.text = chargeText;
    }

    // Width over height of the art strip's rect, in the frame's own proportions.
    private float BandAspect()
    {
        float frameAspect = fitter != null ? fitter.aspectRatio : 120f / 201f;
        return (WIN_X1 - WIN_X0) * frameAspect / BAND_H;
    }

    // --- the art strip --------------------------------------------------------------------------

    // Where the PICTURE sits on a card front, as fractions of the sprite rect (y from the bottom).
    // Measured 2026-09-27: every canonical-frame card (both frame widths) puts it at x 0.10-0.90,
    // y 0.162-0.787; the two remaining legacy 1024x1536 cards at roughly x 0.08-0.92, y 0.13-0.79.
    private const float GEM_X0 = 0.100f, GEM_X1 = 0.900f, GEM_Y0 = 0.164f, GEM_Y1 = 0.786f;
    private const float OLD_X0 = 0.080f, OLD_X1 = 0.920f, OLD_Y0 = 0.130f, OLD_Y1 = 0.790f;

    private static readonly Dictionary<Sprite, Sprite> bands = new Dictionary<Sprite, Sprite>();

    // A sprite cut from the middle of the card's picture at the strip's aspect — the same texture,
    // no copy. ⚠️ FullRect: the default Tight mesh traces an outline from the pixels, and card art is
    // not readable in a build ("Sprite outline generation failed").
    private static Sprite ArtBand(Sprite art, float bandAspect)
    {
        if (art == null || art.texture == null || bandAspect <= 0f) return null;
        Sprite cached;
        if (bands.TryGetValue(art, out cached) && cached != null) return cached;

        Rect r = art.rect;
        bool gem = r.height > 0f && r.width / r.height < 0.63f;   // CardFace.LayoutFor's rule
        float x0 = gem ? GEM_X0 : OLD_X0, x1 = gem ? GEM_X1 : OLD_X1;
        float y0 = gem ? GEM_Y0 : OLD_Y0, y1 = gem ? GEM_Y1 : OLD_Y1;

        float pw = (x1 - x0) * r.width, ph = (y1 - y0) * r.height;
        float cw = pw, ch = pw / bandAspect;
        if (ch > ph) { ch = ph; cw = ch * bandAspect; }

        // Centred on the picture: the subject of every card sits in the middle of its window.
        float cx = r.x + (x0 + x1) * 0.5f * r.width;
        float cy = r.y + (y0 + y1) * 0.5f * r.height;
        Rect crop = new Rect(Mathf.Round(cx - cw * 0.5f), Mathf.Round(cy - ch * 0.5f),
                             Mathf.Round(cw), Mathf.Round(ch));

        Sprite s = Sprite.Create(art.texture, crop, new Vector2(0.5f, 0.5f), art.pixelsPerUnit,
                                 0, SpriteMeshType.FullRect);
        s.name = art.name + " (back strip)";
        bands[art] = s;
        return s;
    }

    private static Sprite fadeSprite;

    // Transparent at the top, the window's own grey at the bottom.
    private static Sprite FadeSprite()
    {
        if (fadeSprite != null) return fadeSprite;
        const int H = 64;
        Texture2D tex = new Texture2D(1, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < H; y++)
        {
            // y = 0 is the BOTTOM row. Eased, so the art stays clear most of the way down.
            float t = 1f - y / (float)(H - 1);
            float a = t * t * (3f - 2f * t);
            tex.SetPixel(0, y, new Color32(WINDOW.r, WINDOW.g, WINDOW.b, (byte)Mathf.RoundToInt(a * 255f)));
        }
        tex.Apply();
        fadeSprite = Sprite.Create(tex, new Rect(0, 0, 1, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return fadeSprite;
    }

    // --- construction helpers -------------------------------------------------------------------

    private static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Image AddImage(RectTransform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI AddText(RectTransform parent, string name, Color color,
                                    float minSize, float maxSize, TextAlignmentOptions align, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.color = color;
        t.alignment = align;
        t.enableAutoSizing = true;
        t.fontSizeMin = minSize;
        t.fontSizeMax = maxSize;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }
}
