using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Draws a card the way the player sees it in hand: the painted face, with its Shift cost and charge
// count sitting in the two medallions.
//
// ⚠️ THE MEDALLION NUMBERS ARE NOT PAINTED INTO THE ART. The art carries the empty gold circles; the
// digits are TMP fields in CardUI_Template. Any screen that draws `cardData.cardArt` on its own gets
// a card with two blank sockets where the cost and charges should be — which is exactly what the
// Scrap Forge and Blompo produced when they first switched to the real face, and it is why they had
// been printing SHIFT/CHARGES as separate text underneath in the first place.
//
// So the placement lives here, once, measured off the prefab, and every screen that isn't the hand
// uses it. If the card art is ever re-cut and the medallions move, this is the single place to fix.
public static class CardFace
{
    // 1024x1536 card art.
    public const float ASPECT = 1.5f;

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // Where the medallions sit — AS FRACTIONS OF THE SPRITE.
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //
    // ⚠️ **THE FREEFALL BLADE FRAME IS THE CANONICAL CARD FRAME (designer, 2026-08-17).** All new
    // card art uses it: the red ball for charges, the blue crystal for Shift cost, an empty name
    // plate, and — on cards that deal damage — a heart container. **`Gem` is therefore the layout
    // to tune and trust; `Classic` is legacy** and exists only until the last 3 old cards (Floor is
    // Lava, Glass Wail, Create Platform — counted 2026-09-27) are re-cut.
    //
    // ⚠️ **THE TWO STYLES DO NOT PUT THEIR MEDALLIONS IN THE SAME PLACE.** This file used to carry
    // one position for both, on the assumption that "both styles put cost right / charges left, so
    // the positions hold". They are 0.045 of a card width apart, and on the gem cards the charge
    // number sat off the LEFT EDGE of the red ball entirely.
    //
    // ⚠️ **The generation is told apart by SPRITE ASPECT, and that is a STOPGAP.** Classic art is
    // 1024x1536 (0.667); the canonical frame is 118x200 (0.590). Aspect is at least a property of
    // the art FILE rather than of gameplay data — but it is still a proxy, and a new card cut at
    // 0.667 would silently take the legacy positions. **When the set is fully re-cut, delete
    // `Classic` and the chooser with it and keep `Gem` as the only layout.**
    public struct Medallions { public Vector2 Uses, Cost; }

    // ⚠️ **MEASURED ON THE RENDERED CARD, NOT ON THE SPRITE — that correction is the whole point
    // of these numbers.** The first pass scanned the sprite for strongly-coloured pixels, which is
    // fine for the ball (a saturated disc) and WRONG for the crystal: a diamond tapers to dark,
    // desaturated tips, the strict colour test missed the top one, and the resulting "centre" sat
    // 14.5px low on a 900px card — about 8% of the crystal's height, which is what the designer
    // saw. Rendering the real card and measuring the medallion AND the digit ink in the SAME image
    // removes every mapping assumption at once.
    //
    // Method that settled it: capture the frame, load the PNG back, and take a row-width profile
    // down each shape. A circle and a diamond both reach their widest row exactly at their vertical
    // centre, so the peak row IS the answer — and it is immune to the rim, highlights and facets
    // that drag a centroid or inflate a bounding box. (Bounding box put the ball's x at 814 and the
    // centroid at 811.1; the mode of the row midpoints is 811, and the profile is symmetric there.)
    private static readonly Medallions Gem = new Medallions
    {
        Uses = new Vector2(0.2188f, 0.8796f),   // red ball — verified: true centre (0.2194, 0.8794)
        Cost = new Vector2(0.8330f, 0.8767f),   // blue crystal — widest row peaks here
    };

    // ⚠️ Legacy keeps the ORIGINAL hand-authored values, so this stays a visual no-op on the 14 old
    // cards until their art is replaced.
    private static readonly Medallions Classic = new Medallions
    {
        Cost = new Vector2(0.5f + 69.5f / 200f, 0.5f + 121.4f / 300f),
        Uses = new Vector2(0.5f - 65.4f / 200f, 0.5f + 126.4f / 300f),
    };

    /// <summary>Which generation of card art this sprite belongs to. See the stopgap note above.</summary>
    public static Medallions LayoutFor(Sprite art)
    {
        if (art == null || art.rect.height <= 0f) return Classic;
        return (art.rect.width / art.rect.height) < 0.63f ? Gem : Classic;
    }

    // ⚠️ The cost was authored at 30 and is now 34. Colour was the reported fault — a blue digit on
    // a blue crystal — but it was also the SMALLER of the two numbers while sitting on the larger,
    // taller medallion: a single digit filled only ~40% of the crystal's width. Recolouring alone
    // fixed legibility without fixing presence, and the cost is the number a player checks most
    // often (can I afford this?).
    private const float COST_SIZE = 34f / 200f;   // font size as a fraction of the DRAWN card width
    private const float USES_SIZE = 38f / 200f;

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // The digits' colours — chosen against the canonical frame, MEASURED not guessed.
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //
    // ⚠️ **THE SHIFT COST USED TO BE BLUE ON A BLUE CRYSTAL.** Sampled off the sprite, the crystal
    // averages (0.377, 0.398, 0.920) and the digit was (0.307, 0.304, 0.934) — the same colour. The
    // designer reported it as blending into the background, and it did, exactly.
    //
    // Pale ice rather than pure white: it keeps the Shift-blue identity the whole game uses for this
    // resource while carrying a luminance of ~0.93 against the crystal's ~0.43.
    public static readonly Color CostColor = new Color(0.90f, 0.95f, 1.00f, 1f);

    /// <summary>Charges, normal. White on the red ball.</summary>
    public static readonly Color ChargeColor = Color.white;

    // ⚠️ **THE LAST-CHARGE WARNING WAS RED ON A RED BALL** — the same fault as the cost, on the same
    // frame, and it would have shipped invisible. Amber reads as a warning without competing with
    // the ball, and still reads inside the legacy frame's dark gold ring.
    public static readonly Color ChargeLowColor = new Color(1.00f, 0.82f, 0.25f, 1f);

    // ── Fitting a number to its socket ─────────────────────────────────────────────────────────
    //
    // ⚠️ **A TWO-DIGIT NUMBER IS 1.93x THE WIDTH OF A ONE-DIGIT NUMBER, and the sockets are drawn
    // for one digit.** Measured in the display face at 100pt: widest single digit "0" = 58.2px,
    // "10" = 110.8, "99" = 112.2, "100" = 176.0, "∞" = 70.9. Shuriken is the only card in the set
    // with maxUses 10, so it was the one that showed it — but nothing stops a future card, or a
    // recharged one, going higher.
    //
    // Deterministic scaling, NOT `enableAutoSizing`: auto-size settles over several frames and is
    // documented in this project as unreliable to measure, and these labels are rebuilt constantly.
    private const float GLYPH_W = 0.582f;    // widest digit, as a multiple of font size
    private const float INF_W = 0.709f;      // the infinity glyph

    // ⚠️ **PER MEDALLION, because the two sockets are not the same size.** Measured on the canonical
    // frame: the ball is 0.357 of the card wide, the crystal only 0.219 — and the crystal is a
    // DIAMOND, so a number near its full width would run into the tapering facets. One shared budget
    // either wasted the ball or overran the gem.
    private const float USES_MAX_W = 0.165f;   // of the drawn card width — inside the ball
    private const float COST_MAX_W = 0.130f;   // inside the crystal's waist

    /// <summary>The font size at which <paramref name="text"/> still fits inside its medallion.</summary>
    public static float FitNumberSize(string text, float authoredSize, float drawnCardWidth, bool cost)
    {
        if (string.IsNullOrEmpty(text) || drawnCardWidth <= 0f) return authoredSize;
        float ratio = text == "∞" ? INF_W : GLYPH_W * text.Length;
        float want = ratio * authoredSize;
        float max = (cost ? COST_MAX_W : USES_MAX_W) * drawnCardWidth;
        return want <= max ? authoredSize : authoredSize * (max / want);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // ⚠️ ONE SHARED OUTLINED MATERIAL for every medallion digit, in the hand AND on every screen.
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //
    // The digits sit directly on saturated artwork — a white number on a red ball, a pale one on a
    // blue crystal — and without a dark edge they smear into it at hand size. `CardFace` used to
    // fake this with FOUR offset copies of every number (a "keyline"), which the hand never had at
    // all, so the same card read differently in your hand than in the forge.
    //
    // ⚠️ **It must be `fontSharedMaterial`, and it must be ONE material.** Writing `outlineWidth` on
    // a TMP_Text auto-instances a material PER LABEL, which breaks batching and leaks one material
    // per card drawn. A single cached variant of the display font's material keeps every digit in
    // one draw call and replaces 8 extra TMP objects per card with zero.
    private static Material outlineMat;

    private static Material OutlineMaterial()
    {
        if (outlineMat != null) return outlineMat;
        TMP_FontAsset f = UIType.Display();
        if (f == null || f.material == null) return null;

        outlineMat = new Material(f.material) { name = "CardNumber (outlined)" };
        outlineMat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        outlineMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.05f, 0.04f, 0.06f, 1f));
        outlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
        return outlineMat;
    }

    /// <summary>Give a medallion digit the shared dark edge. Safe to call every refresh.</summary>
    public static void ApplyNumberOutline(TMP_Text t)
    {
        if (t == null) return;
        Material m = OutlineMaterial();
        if (m != null) t.fontSharedMaterial = m;
    }

    /// <summary>
    /// The size the art is actually DRAWN at inside a host rect.
    ///
    /// ⚠️ **`preserveAspect` means the art is not the host.** The gem art is 0.590 where the card
    /// box is 0.667, so it letterboxes to 88.5% of the host width with bars either side — and every
    /// number stamped at a fraction of the HOST then lands outside the artwork it belongs to. This
    /// is the same letterbox `CardUI` already maps Stagger's heart and name plate through.
    /// </summary>
    public static Vector2 DrawnArtSize(Vector2 hostSize, Sprite art)
    {
        if (art == null || art.rect.height <= 0f || hostSize.x <= 0f || hostSize.y <= 0f) return hostSize;
        float artAspect = art.rect.width / art.rect.height;
        float hostAspect = hostSize.x / hostSize.y;
        return artAspect > hostAspect
             ? new Vector2(hostSize.x, hostSize.x / artAspect)      // width-limited
             : new Vector2(hostSize.y * artAspect, hostSize.y);     // height-limited
    }

    /// <summary>Offset from the host's CENTRE to a medallion, in host-local pixels.</summary>
    public static Vector2 MedallionOffset(Vector2 hostSize, Sprite art, bool cost)
    {
        Medallions m = LayoutFor(art);
        Vector2 f = cost ? m.Cost : m.Uses;
        Vector2 drawn = DrawnArtSize(hostSize, art);
        return new Vector2((f.x - 0.5f) * drawn.x, (f.y - 0.5f) * drawn.y);
    }

    /// <summary>
    /// Position and size an EXISTING medallion label against a card art image — the hand's path.
    /// `CardUI`'s `Uses_Text` / `Cost_Text` are prefab objects at authored positions, so they get
    /// moved rather than rebuilt, but through the same maths every other screen uses.
    ///
    /// ⚠️ The label must share an origin with the art image. In `CardUI_Template` both are anchored
    /// to the ROOT's centre, and the art keeps a fixed 200x300 even though the hand's layout group
    /// rewrites the root to 200x100 — so they do.
    /// </summary>
    public static void PlaceMedallion(TMP_Text label, Image artImage, bool cost)
    {
        if (label == null || artImage == null) return;

        Vector2 host = artImage.rectTransform.rect.size;
        if (host.x <= 0f || host.y <= 0f) host = artImage.rectTransform.sizeDelta;
        Vector2 drawn = DrawnArtSize(host, artImage.sprite);

        RectTransform rt = label.rectTransform;
        // ⚠️ The PIVOT is set here too, not just the anchors. `anchoredPosition` places the
        // pivot, so a label whose pivot is not centred lands half its own rect away from the
        // medallion — and these are prefab objects whose pivot this code does not own.
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = MedallionOffset(host, artImage.sprite, cost);

        label.enableWordWrapping = false;   // "10" has no break opportunity, but do not tempt it
        label.fontSize = FitNumberSize(label.text, (cost ? COST_SIZE : USES_SIZE) * drawn.x, drawn.x, cost);
        ApplyNumberOutline(label);
        if (cost) label.color = CostColor;   // the charge colour carries the low-charge warning
    }

    private static readonly Color COST_COLOR = new Color(0.307f, 0.304f, 0.934f, 1f);

    // The bottom name plate, same fractions CardUI uses (measured on the 118x205 Stagger sprite,
    // and the 1024x1536 cards put their plate within ~1% of the same place). CY is from the BOTTOM.
    private const float PLATE_CY = 190.5f / 205f;
    private const float PLATE_W = 96f / 118f;
    private const float PLATE_H = 16f / 118f;
    private const float NAME_SIZE = 15f / 200f;
    private static readonly Color NAME_COLOR = new Color(0.85f, 0.72f, 0.36f, 1f);

    // Fills `host` with the card. The host should already be at the card's aspect — pass a rect of
    // (w, w * CardFace.ASPECT) — or the art will letterbox inside it.
    public static void Build(RectTransform host, RuntimeCard card)
    {
        if (host == null || card == null || card.cardData == null) return;

        Vector2 hostSize = host.rect.size;
        if (hostSize.x <= 0f || hostSize.y <= 0f) hostSize = host.sizeDelta;

        // ⚠️ Everything below measures against the DRAWN art, not the host — see DrawnArtSize.
        Vector2 drawn = DrawnArtSize(hostSize, card.cardData.cardArt);
        float w = drawn.x;

        if (card.cardData.cardArt != null)
        {
            Image art = AddImage(host, "Face", card.cardData.cardArt);
            art.preserveAspect = true;
            Stretch(art.rectTransform);
        }

        // The title, for art that ships with an empty plate — the standing convention for new cards
        // (see CardData.nameIsPaintedIntoArt). Without this those cards render with a blank bar,
        // which is what they were doing here and in the hand.
        if (!card.cardData.nameIsPaintedIntoArt && !string.IsNullOrEmpty(card.cardData.cardName))
        {
            TextMeshProUGUI n = AddNumberLayer(host, "Name",
                                               new Vector2(0f, (0.5f - PLATE_CY) * drawn.y),
                                               NAME_SIZE * w, card.cardData.cardName.ToUpperInvariant(),
                                               NAME_COLOR);
            n.rectTransform.sizeDelta = new Vector2(w * PLATE_W, w * PLATE_H);
            n.enableAutoSizing = true;
            n.fontSizeMin = NAME_SIZE * w * 0.5f;
            n.fontSizeMax = NAME_SIZE * w;
        }

        // Stagger's art has NO corner medallions — it carries a single heart on the top edge — so
        // stamping numbers into those positions would print them onto bare artwork. CardUI hides
        // them for the same reason.
        if (card.cardData.actionType == CardActionType.Stagger) return;

        string cost = card.cardData.shiftCost.ToString();
        AddNumber(host, "Cost", MedallionOffset(hostSize, card.cardData.cardArt, true),
                  FitNumberSize(cost, COST_SIZE * w, w, true), cost, CostColor);

        string charges = card.isInfinite ? "∞" : card.currentUses.ToString();
        Color chargeCol = (!card.isInfinite && card.currentUses <= 1) ? ChargeLowColor : ChargeColor;
        AddNumber(host, "Charges", MedallionOffset(hostSize, card.cardData.cardArt, false),
                  FitNumberSize(charges, USES_SIZE * w, w, false), charges, chargeCol);
    }

    // ⚠️ THE FOUR-COPY KEYLINE IS GONE. Every number used to be drawn five times — the digit plus
    // four offset black copies ringing it — because the digits sit on saturated artwork and smear
    // into it without a dark edge. It worked, but the HAND never had it (its labels are prefab
    // objects, not built here), so the same card read differently in your hand than in the forge.
    // `ApplyNumberOutline` puts a real SDF outline on both through one shared material: same look
    // everywhere, one draw call, and 8 fewer TMP objects per card.
    private static TextMeshProUGUI AddNumber(RectTransform parent, string name, Vector2 offset, float size,
                                             string text, Color color)
    {
        TextMeshProUGUI t = AddNumberLayer(parent, name, offset, size, text, color);
        ApplyNumberOutline(t);
        return t;
    }

    private static TextMeshProUGUI AddNumberLayer(RectTransform parent, string name, Vector2 offset,
                                                  float size, string text, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(size * 3.2f, size * 1.6f);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = UIType.Display();
        if (f != null) t.font = f;
        t.text = text;
        t.color = color;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    private static Image AddImage(RectTransform parent, string name, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
