using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The reverse face of a card — what you see when you hover one and it turns over.
//
// THE BRIEF (designer, 2026-08-09): "the hover displays should be cooler and nicer-looking. maybe
// they could be the cards' backs turnt." The old hover was a flat grey rectangle laid over the card
// with the art faded to 12% behind it and a 140x50 text box that every real description overflowed.
// It read as a tooltip that had landed on the card, not as part of the card.
//
// So the card TURNS OVER (CardUI drives the flip) and this is what's on the other side.
//
// ⚠️ IT IS NOT DRESSED IN FlatUI's IRON. FlatUI is the material for SCREENS — the forge, Blompo,
// the shop — and its whole point is that each screen picks a material and inverts something. A card
// back is not a screen: it belongs to the DECK, and the deck's fronts are painted gold-on-near-black
// with an ornate border. Re-skinning the back as a charcoal workbench plate would make the card
// visibly stop being a card halfway through its own flip. It borrows FlatUI's SHAPES (they're just
// white sprites) and none of its palette.
//
// The layout, top to bottom, is deliberately the reading order of a decision:
//     NAME          what is this
//     ------
//     body          what does it do
//     ------
//     COST  CHARGES what does it cost me, and how many are left
//
// The cost/charges footer repeats what the front already shows, on purpose: while the card is
// flipped you cannot see the front, and "show the numbers a decision depends on" is a lesson this
// project has already paid for once on Blompo's card picker.
public class CardBack : MonoBehaviour
{
    // The deck's own palette, read off the painted card fronts — NOT FlatUI's charcoal.
    private static readonly Color GROUND = new Color(0.088f, 0.070f, 0.062f, 1f);
    private static readonly Color GROUND_EDGE = new Color(0.145f, 0.115f, 0.098f, 1f);
    private static readonly Color GOLD = new Color(0.85f, 0.72f, 0.36f, 1f);
    private static readonly Color GOLD_DIM = new Color(0.85f, 0.72f, 0.36f, 0.30f);
    private static readonly Color BODY = new Color(0.88f, 0.85f, 0.80f, 1f);
    private static readonly Color LABEL = new Color(0.52f, 0.46f, 0.38f, 1f);
    private static readonly Color SHIFT_BLUE = new Color(0.55f, 0.52f, 0.96f, 1f);
    private static readonly Color CHARGE_BLUE = new Color(0.478f, 0.706f, 0.929f, 1f);

    // Fractions of the card rect, so this works at hand scale and in the deck view's 0.72 alike.
    private const float MARGIN = 0.055f;

    public TextMeshProUGUI title;
    public TextMeshProUGUI body;
    public TextMeshProUGUI keyHint;
    public TextMeshProUGUI costLabel, costValue;
    public TextMeshProUGUI chargeLabel, chargeValue;

    private static Sprite crystal;

    // Builds the whole face under `parent` (the card ROOT, so it turns with the card) and returns
    // it inactive.
    //
    // ⚠️ The returned object is pre-rotated 180 degrees on Y. CardUI flips the card by rotating the
    // ROOT, and past 90 degrees every child renders MIRRORED — text included. Cancelling it here
    // means the back reads correctly exactly when it is the face you're looking at.
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
    // The root carries a LayoutElement and lives inside the hand's layout group, which OVERWRITES
    // its RectTransform at runtime — it is nothing like the 200x300 the prefab shows in the editor.
    // Stretching to the root produced a back a third of the card's height sitting over its bottom
    // edge. cardArtImage is the only rect on this prefab that matches what the player sees, which
    // is exactly why the blessing mark is anchored to it too.
    //
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
        // --- The card stock: warm near-black ground, a lit edge, then the gold rule. ---
        AddStretched(root, "Ground", FlatUI.Panel(5), GROUND, Vector2.zero, Vector2.zero);

        // Light from above. FlatUI's ideology in one line: a uniformly lit surface reads as a UI
        // widget, an unevenly lit one reads as a physical object catching light from somewhere.
        Image sheen = AddStretched(root, "Sheen", FlatUI.VerticalFade(),
                                   new Color(1f, 0.86f, 0.66f, 0.020f), Vector2.zero, Vector2.zero);
        sheen.type = Image.Type.Simple;

        AddStretched(root, "GroundLip", FlatUI.Outline(5, 2), GROUND_EDGE, Vector2.zero, Vector2.zero);

        // The gold frame is INSET, the way the painted fronts carry theirs — a border drawn hard to
        // the silhouette reads as a UI panel, one floating inside the card reads as printing on it.
        float inset = 7f;
        AddStretched(root, "GoldFrame", FlatUI.Outline(5, 1), GOLD_DIM,
                     new Vector2(inset, inset), new Vector2(-inset, -inset));

        // Watermark: the Shift crystal, the game's own mark, printed faintly behind the text.
        //
        // ⚠️ It is an OUTLINE with a barely-there fill, not a solid shape, and it is small. The
        // first pass was a filled diamond at 56% of the card width and 0.055 alpha, which measured
        // #231E12 against a #0D0D0D ground — nearly three times the ground's value — and read as a
        // big olive blob the body text sat on top of. A watermark has to survive being ignored.
        Image mark = AddImage(root, "Watermark", Crystal(), new Color(GOLD.r, GOLD.g, GOLD.b, 0.105f));
        mark.preserveAspect = true;
        RectTransform mrt = mark.rectTransform;
        mrt.pivot = new Vector2(0.5f, 0.5f);
        mrt.anchorMin = new Vector2(0.33f, 0.40f);
        mrt.anchorMax = new Vector2(0.67f, 0.66f);
        mrt.offsetMin = mrt.offsetMax = Vector2.zero;

        // --- Header. The key hint sits ABOVE the title, inside the frame. ---
        // It used to be anchored to the very top edge, where it straddled the gold rule and was
        // half cut off by the card's own silhouette.
        keyHint = AddText(root, "KeyHint", LABEL, 6f, 11f, TextAlignmentOptions.Center);
        Anchor(keyHint.rectTransform, MARGIN, 0.895f, 1f - MARGIN, 0.955f);

        title = AddText(root, "Title", GOLD, 12f, 22f, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, MARGIN, 0.805f, 1f - MARGIN, 0.895f);

        AddRule(root, "RuleTop", 0.792f);

        // --- Body: the description, which is what the player actually hovered for. ---
        //
        // ⚠️ THE RANGE IS NARROW ON PURPOSE, AND THE CEILING IS THE POINT. The first pass capped it
        // at 13pt while the box was two-thirds empty, so the descriptions came out unreadably small
        // for no reason — nothing was constraining them except the cap. The box is ~170x168 units,
        // which fits every current description at 21pt with room to spare.
        //
        // The CEILING is the design; the floor is only a safety net. 14 of the 15 cards settle at
        // exactly 21pt so they look identical, the longest (Glass Parry) steps to 19, and the floor
        // exists for the rare BLESSED long card, which carries two extra lines of blessing text on
        // an already-full face — at a 16pt floor three blessings clipped straight out of the box.
        //
        // Do NOT widen the ceiling to give short cards bigger text: a one-line card rendering at
        // twice the size of a wordy one reads as broken, not as emphasis. If a card can't reach 21,
        // shorten the card's text.
        body = AddText(root, "Body", BODY, 12f, 21f, TextAlignmentOptions.Center);
        body.enableWordWrapping = true;
        body.lineSpacing = -12f;   // CCBattleScarred sets a loose default line height for a card
        Anchor(body.rectTransform, MARGIN + 0.01f, 0.215f, 1f - MARGIN - 0.01f, 0.775f);

        // --- Footer: two stat columns. ---
        AddRule(root, "RuleBottom", 0.195f);

        costLabel = AddText(root, "CostLabel", LABEL, 9f, 12f, TextAlignmentOptions.Center);
        Anchor(costLabel.rectTransform, MARGIN, 0.120f, 0.5f, 0.180f);
        costValue = AddText(root, "CostValue", SHIFT_BLUE, 13f, 20f, TextAlignmentOptions.Center);
        Anchor(costValue.rectTransform, MARGIN, 0.042f, 0.5f, 0.122f);

        chargeLabel = AddText(root, "ChargeLabel", LABEL, 9f, 12f, TextAlignmentOptions.Center);
        Anchor(chargeLabel.rectTransform, 0.5f, 0.120f, 1f - MARGIN, 0.180f);
        chargeValue = AddText(root, "ChargeValue", CHARGE_BLUE, 13f, 20f, TextAlignmentOptions.Center);
        Anchor(chargeValue.rectTransform, 0.5f, 0.042f, 1f - MARGIN, 0.122f);

        // Track the two small labels out. At this size the card font's letterforms crowd into each
        // other — "SHIFT" was reading as "SKIFT" — and spacing them is what makes them read as
        // labels rather than as a smudge.
        costLabel.characterSpacing = 8f;
        chargeLabel.characterSpacing = 8f;
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

        string charges = card.isInfinite ? "∞" : $"{card.currentUses} / {card.MaxUses}";

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

        title.text = card.cardData.cardName.ToUpperInvariant();
        body.text = bodyText;
        keyHint.text = key;

        costLabel.text = costLabelText;
        costValue.text = costText;
        costValue.color = costColor;

        chargeLabel.text = chargeLabelText;
        chargeValue.text = chargeText;
    }

    // --- construction helpers -------------------------------------------------------------------

    private static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Image AddStretched(RectTransform parent, string name, Sprite sprite, Color color,
                                      Vector2 offMin, Vector2 offMax)
    {
        Image img = AddImage(parent, name, sprite, color);
        img.type = Image.Type.Sliced;   // chamfered plates stretch as 9-slices, never Simple
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        return img;
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

    // ⚠️ TWO PIXELS, NOT ONE, AND BRIGHTER THAN THEORY SAYS.
    //
    // Cards render at roughly 0.8 scale in the hand, so a 1px rule is 0.8 device pixels and whether
    // you see it comes down to which subpixel it lands on. That is not hypothetical: the first pass
    // drew both rules with identical code and only the LOWER one appeared — measured at #9D8541,
    // full strength, while the upper one sampled as bare card. Nothing was wrong with the upper
    // rule except its luck. 2px always survives the rounding.
    private static void AddRule(RectTransform parent, string name, float y)
    {
        Image r = AddImage(parent, name, FlatUI.FadedRule(), new Color(GOLD.r, GOLD.g, GOLD.b, 0.48f));
        Anchor(r.rectTransform, MARGIN + 0.04f, y, 1f - MARGIN - 0.04f, y);
        r.rectTransform.sizeDelta = new Vector2(0f, 2f);
    }

    private TextMeshProUGUI AddText(RectTransform parent, string name, Color color,
                                    float minSize, float maxSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = FlatUI.UIFont();
        if (f != null) t.font = f;
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

    // Deckshift's mark: the Shift crystal, same silhouette as the pickup and the HUD icon, drawn
    // rather than referenced so the back has no asset to lose. White, tinted by the caller.
    //
    // ⚠️ DRAWN AS AN OUTLINE, not a filled shape. A solid diamond behind body text is a blob the
    // text has to fight; a struck outline with a whisper of fill reads as something PRINTED on the
    // card and lets the words sit on top of it. Same lesson as Blompo's sigil: an emblem needs
    // structure, or it is just a smear of light. The centre seam is what makes it a crystal rather
    // than a rhombus.
    private static Sprite Crystal()
    {
        if (crystal != null) return crystal;

        const int W = 96, H = 160;
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float cx = (W - 1) * 0.5f, cy = (H - 1) * 0.5f;
        const float STROKE = 0.045f;   // in normalised diamond units
        const float FILL = 0.22f;      // the interior wash, relative to the stroke

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // |dx| + |dy| == 1 is the diamond's edge; d is the signed distance to it.
                float dx = Mathf.Abs(x - cx) / cx;
                float dy = Mathf.Abs(y - cy) / cy;
                float d = dx + dy;

                if (d > 1f + STROKE) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                // Outline: full value in a band either side of the edge, feathered.
                float edge = Mathf.Clamp01(1f - Mathf.Abs(1f - d) / STROKE);

                // Centre seam, so the facets read.
                float seam = Mathf.Clamp01(1f - Mathf.Abs(dx) / 0.055f) * (d < 0.95f ? 1f : 0f);

                float a = Mathf.Max(Mathf.Max(edge, seam * 0.55f), d < 1f ? FILL : 0f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();

        crystal = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        return crystal;
    }
}
