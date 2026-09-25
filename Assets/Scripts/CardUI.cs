using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CardUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Görseller")]
    public Image cardArtImage;
    public TextMeshProUGUI keyHintText;
    public GameObject selectionFrame;
    public TextMeshProUGUI usesText;
    public Transform shiftCostContainer;
    public GameObject shiftPointPrefab;
    public TextMeshProUGUI costText; // Maliyet sayısı (büyük sol-üst daire)

    [Header("Hizalama Ayarları")]
    public float pointSpacing = 20f; // Noktalar arası boşluk (Bunu Inspector'dan değiştirebilirsin)

    [Header("Hover")]
    [Tooltip("LEGACY. The old flat grey overlay — permanently switched off; the card turns over instead. Kept assigned so the change is one line to back out.")]
    public GameObject descriptionPanel;
    [Tooltip("LEGACY. Text of the old overlay. The description now goes on the card's back.")]
    public TextMeshProUGUI descriptionText;
    public float selectionLiftAmount = 50f;

    [Header("Flip")]
    [Tooltip("How much the card grows while turned over. The back is a page of text on a small card, so the one you are reading comes forward. Kept under the hand's card spacing so it never overlaps a neighbour.")]
    [SerializeField] private float flipZoom = 1.2f;
    [Tooltip("How far the card rises while turned over, so the bottom of the back clears the screen edge. See the note in UpdateSelectionVisual for the measurement.")]
    [SerializeField] private float flipLift = 40f;

    private RuntimeCard myCard;
    private int myIndex;
    private Vector3 originalScale;
    private RectTransform rectTransform;

    // --- Hover flip ---------------------------------------------------------------------------
    // The mechanics live in CardHoverFlip, shared with the Scrap Forge and Blompo so all three
    // screens turn a card over the same way. This class only adds what is specific to a card in the
    // HAND: the zoom and lift, and Stagger's bespoke footer.
    private CardHoverFlip flip;
    private string bodyText = "";     // what the back's description reads; composed in Setup

    private CardBack back => flip != null ? flip.Back : null;
    private float flipT => flip != null ? flip.Progress : 0f;

    // --- Slot-driven motion -----------------------------------------------------------------------
    // Used ONLY by the hand. HandUI computes the fan and pushes each card its pose; this class adds
    // the hover and selection on top of it.
    //
    // ⚠️ EVERY OTHER SCREEN THAT USES THIS PREFAB NEVER CALLS SetSlot — the deck view and the card
    // chest lay their cards out themselves. `hasSlot` is what keeps them on the original code path;
    // without it they would all be dragged to whatever pose the hand last wrote.
    private const float DEAL_START_SCALE = 0.62f;

    private bool hasSlot;
    private Vector2 slotPos;
    private float slotTilt;
    private float slotScale = 1f;
    private float slotHoverLift;

    private Vector2 livePos;
    private float liveScale = 1f;
    private float liveTilt;

    private bool dealing;
    private Vector2 dealFrom;
    private float dealDelay, dealTime, dealElapsed;
    private CanvasGroup group;

    /// <summary>How much this card is asking to be looked at — hovered or selected. The hand uses it
    /// to decide which card draws over its neighbours.</summary>
    public float Prominence => Mathf.Max(flipT, myCard != null && myCard.isSelected ? 1f : 0f);

    /// <summary>Where the fan wants this card to sit. Snap only on the first placement.</summary>
    public void SetSlot(Vector2 pos, float tilt, float scale, float hoverLiftAmount, bool snap)
    {
        hasSlot = true;
        slotPos = pos;
        slotTilt = tilt;
        slotScale = scale;
        slotHoverLift = hoverLiftAmount;

        if (!snap) return;

        dealing = false;
        livePos = pos;
        liveTilt = tilt;
        liveScale = scale;
        if (group != null) group.alpha = 1f;
        ApplySlotPose();
    }

    /// <summary>Fly in from <paramref name="from"/> (rail-local) after <paramref name="delay"/>s.
    /// Passing the card's own slot as `from` turns this into a settle-in-place instead.</summary>
    public void BeginDeal(Vector2 from, float delay, float flyTime)
    {
        EnsureGroup();

        dealing = true;
        dealFrom = from;
        dealDelay = Mathf.Max(0f, delay);
        dealTime = Mathf.Max(0.01f, flyTime);
        dealElapsed = 0f;

        livePos = from;
        liveScale = slotScale * DEAL_START_SCALE;
        liveTilt = 0f;
        group.alpha = 0f;
        ApplySlotPose();
    }

    /// <summary>Hand this card over to whoever is animating it off the screen. Everything that writes
    /// the transform stops, so the play animation is the only thing moving it.</summary>
    public void DetachForPlay()
    {
        if (flip != null)
        {
            flip.ExtraRoll = 0f;
            flip.ResetFace();      // face-up and straight: you should see the card you just played
            flip.enabled = false;
        }
        Button button = GetComponent<Button>();
        if (button != null) button.interactable = false;
        enabled = false;
    }

    private void EnsureGroup()
    {
        if (group != null) return;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    private void ApplySlotPose()
    {
        if (rectTransform != null) rectTransform.anchoredPosition = livePos;
        transform.localScale = originalScale * liveScale;

        // ⚠️ ONE WRITER FOR ROTATION. CardHoverFlip writes localRotation every frame to drive the
        // turn, so the fan's lean is handed to IT rather than written here. Two components writing
        // the same transform would be settled by script execution order, which is undefined.
        if (flip != null) flip.ExtraRoll = liveTilt;
    }

    public RuntimeCard GetCard()
    {
        return myCard;
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = transform.localScale;

        // The old hover overlay is retired: a flat grey rectangle over the artwork with a 140x50
        // text box that every real description overflowed. Switched off rather than deleted.
        if (descriptionPanel != null) descriptionPanel.SetActive(false);

        // ⚠️ The geometry source is cardArtImage, NOT this root. The root carries a LayoutElement
        // and the hand's layout group rewrites it to 200x100 at runtime — nothing like the 200x300
        // the prefab shows. Sizing the back off it put it over the card's bottom third.
        flip = CardHoverFlip.Attach(rectTransform,
                                    cardArtImage != null ? cardArtImage.rectTransform : rectTransform);
    }

    public void Setup(RuntimeCard card, int index)
    {
        myCard = card;
        myIndex = index;

        if (cardArtImage != null) cardArtImage.sprite = card.cardData.cardArt;
        // A NEGATIVE index means the card isn't in the hand — the deck view, the play/discard ghosts
        // and the card-chest offer all pass -1. There is no key to press, and "[0]" is a lie. (The
        // card's BACK already guarded this; the front did not, so every chest offer read "[0]".)
        if (keyHintText != null) keyHintText.text = index >= 0 ? $"[{index + 1}]" : "";

        // The description lives on the card's BACK now; the title is up there as a header, so the
        // body is just the effect text. RefreshBlessingBadge/TickCardFace may extend it below.
        bodyText = card.cardData.description;

        // Uses
        if (usesText != null)
        {
            if (card.isInfinite) { usesText.text = "∞"; usesText.color = CardFace.ChargeColor; }
            else
            {
                usesText.text = card.currentUses.ToString();
                // ⚠️ NOT red. The canonical frame's charge medallion IS a red ball, so the
                // last-charge warning was red on red — invisible exactly when it matters most.
                // Colours come from CardFace so the hand and every screen agree.
                usesText.color = (card.currentUses == 1) ? CardFace.ChargeLowColor : CardFace.ChargeColor;
            }
        }

        // --- MALİYET: tek sayı (büyük sol-üst daire) ---
        // Eski nokta (dot) sistemi kaldırıldı; maliyet artık costText'te sayı olarak gösteriliyor.
        if (costText != null) costText.text = card.cardData.shiftCost.ToString();
        // -----------------------------

        // ⚠️ The two medallion numbers are POSITIONED AND SIZED IN CODE, not left at their authored
        // prefab values — through the same `CardFace` maths every other screen uses, so the hand and
        // the forge can never disagree about where a card's numbers go. Two things make this
        // necessary, and neither is visible in the prefab:
        //   • the art is drawn with `preserveAspect`, so a sprite of a different aspect letterboxes
        //     inside the 200x300 box and every number authored against 200 drifts outward;
        //   • the sockets are drawn for ONE digit, and "10" is 1.93x the width of "9" — it spilled
        //     straight off the medallion, which is what the designer reported.
        CardFace.PlaceMedallion(usesText, cardArtImage, false);
        CardFace.PlaceMedallion(costText, cardArtImage, true);

        RefreshBlessingBadge(card);
        RefreshCardFace(card);
        RefreshBack(card, index);

        UpdateSelectionVisual();
    }

    // Pushes the finished text and the stat footer onto the back. Called after the badge and face
    // passes, because both can add lines to bodyText.
    private void RefreshBack(RuntimeCard card, int index)
    {
        if (flip == null || back == null || card == null || card.cardData == null) return;

        // DeckViewUI passes -1: those cards are not in the hand, so there is no key to press and a
        // literal "[0]" would be a lie.
        string key = index >= 0 ? $"[{index + 1}]" : "";

        // Re-bound on every Setup rather than once at build: the hand's layout group has not run
        // when Awake fires, and DeckViewUI reuses this prefab at a different size.
        flip.Bind(card, key);

        // Blompo's blessing line is appended by RefreshBlessingBadge into bodyText; BindStandard
        // composes the same thing, so the normal case is already correct by here. Only Stagger
        // needs a different footer.
        if (!isStaggerCard) return;

        // Stagger's price is HP and it rises every play, so its footer says something entirely
        // different from every other card's. TickCardFace keeps the number live.
        PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
        float cost = p != null ? p.NextStaggerCost : 0f;
        bool lethal = p != null && cost >= p.CurrentHealth;
        back.SetContent(card, bodyText, key,
                        "HEALTH", Mathf.RoundToInt(cost).ToString(),
                        lethal ? STAGGER_LETHAL_COLOR : STAGGER_COST_COLOR,
                        "GIVES", $"+{(p != null ? p.staggerShiftGain : 2)}");
    }

    // --- Procedural card face -------------------------------------------------------------------
    // Stagger's art is not built like the rest of the set. Every other card carries two painted
    // medallions in its top corners (Shift cost left, charges right); Stagger's has NEITHER, and
    // instead a single HEART centred along the top edge. That heart is the cost slot, because what
    // Stagger costs is HP — an escalating price that goes up every play, for the whole run
    // (PlayerController.PerformStagger).
    //
    // So the two normal readouts are switched OFF for this card rather than left to float over
    // artwork that has no sockets for them, and the price is drawn into the heart instead.
    //
    // ⚠️ THE NUMBER IS PLACED AGAINST THE DISPLAYED IMAGE, NOT THE RECT. CardArt is 200x300 with
    // preserveAspect ON, and the Stagger art is 124x210 — a narrower aspect — so it letterboxes
    // with a bar down each side and the artwork is ~177px wide, not 200. Measuring the heart as a
    // fraction of the RECT would put the number off-centre and too wide. It is measured as a
    // fraction of the SPRITE and mapped through the letterbox each time the rect resizes.
    //
    // Fractions below are measured off Assets/Art/stagger.png. The heart's outline spans x 38-86,
    // y 2-44 in the FILE, and its widest legible band — where a number actually fits, above the
    // taper — is centred on y 19. Interior fill is a flat #550A0F, so pale text reads cleanly.
    //
    // ⚠️ They are expressed against the SPRITE RECT, not the file. Unity's auto-slice trimmed the
    // transparent margin, so the sprite is 118x205 at offset (3,3) inside the 124x210 png — a
    // fraction taken against the file would sit the number low and slightly small. art.rect below
    // is the sprite rect, which is why the two agree.
    private const float HEART_CX = 59f / 118f;      // dead-centre, as it happens
    private const float HEART_CY = 17f / 205f;      // from the TOP of the sprite
    private const float HEART_W = 44f / 118f;
    private const float HEART_H = 28f / 205f;

    // --- The name plate, drawn for EVERY card whose art doesn't already carry its title ---------
    //
    // Standing convention (designer, 2026-08-09): new card art ships with an EMPTY plate and the UI
    // types the name into it. That decouples a card's name from its texture — renaming a card stops
    // being a repaint — and it is why CardData.nameIsPaintedIntoArt defaults to FALSE.
    //
    // ⚠️ The 14 pre-2026-08-09 cards have their titles painted in and set that flag, so nothing
    // about them changes today. Clear it on each as its art is replaced. Getting it backwards is
    // visible instantly: set-when-blank leaves an empty plate, clear-when-painted double-prints.
    //
    // The plate geometry below was measured on Stagger's art but is expressed as fractions of the
    // sprite, and the old 1024x1536 cards put their plate within ~1% of the same place — so one set
    // of constants serves both layouts. Re-measure only if a new art moves the plate.
    private const float PLATE_CY = 190.5f / 205f;
    private const float PLATE_W = 96f / 118f;
    private const float PLATE_H = 16f / 205f;
    // The set's title gold, matching the plates painted into the legacy art.
    private static readonly Color NAME_COLOR = new Color(0.85f, 0.72f, 0.36f, 1f);

    // The Shift blue used by the resource bar, so a cost on a card back and a cost in the HUD are
    // recognisably the same currency.
    private static readonly Color SHIFT_COST_COLOR = new Color(0.55f, 0.52f, 0.96f, 1f);

    private static readonly Color STAGGER_COST_COLOR = new Color(0.98f, 0.90f, 0.87f, 1f);
    // Shown when the next Stagger costs at least as much HP as the player has left. This is the
    // only place the run's actual fail state is visible before it happens, so it must be loud.
    private static readonly Color STAGGER_LETHAL_COLOR = new Color(1f, 0.30f, 0.28f, 1f);

    private TextMeshProUGUI staggerCostText;
    private TextMeshProUGUI nameText;
    private Vector2 faceHostSize = Vector2.zero;
    private bool isStaggerCard;

    private void RefreshCardFace(RuntimeCard card)
    {
        CardData data = card != null ? card.cardData : null;
        if (data == null) return;

        isStaggerCard = data.actionType == CardActionType.Stagger;

        // The two painted medallions belong to the normal card frame, which Stagger's art lacks —
        // it carries a single heart on the top edge instead (see below). Leaving them on would
        // float a Shift cost and a charge count over artwork with no sockets for them.
        if (costText != null) costText.gameObject.SetActive(!isStaggerCard);
        if (usesText != null) usesText.gameObject.SetActive(!isStaggerCard);

        bool wantName = !data.nameIsPaintedIntoArt && !string.IsNullOrEmpty(data.cardName);
        if (wantName)
        {
            if (nameText == null) nameText = MakeFaceLabel("CardName", 8f, 15f, NAME_COLOR);
            nameText.gameObject.SetActive(true);
            nameText.text = data.cardName.ToUpperInvariant();
        }
        else if (nameText != null) nameText.gameObject.SetActive(false);

        if (isStaggerCard)
        {
            if (staggerCostText == null)
                staggerCostText = MakeFaceLabel("StaggerCost", 12f, 30f, STAGGER_COST_COLOR);
            staggerCostText.gameObject.SetActive(true);
        }
        else if (staggerCostText != null) staggerCostText.gameObject.SetActive(false);

        faceHostSize = Vector2.zero;   // force a re-place against the current rect
        TickCardFace();
    }

    private TextMeshProUGUI MakeFaceLabel(string name, float minSize, float maxSize, Color color)
    {
        RectTransform host = cardArtImage != null ? cardArtImage.rectTransform : (RectTransform)transform;

        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(host, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // top-left; placed by pixel offset below
        rt.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (costText != null) t.font = costText.font;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true;   // 3-digit costs arrive fast: 8, 16, 24... 104
        t.fontSizeMin = minSize;
        t.fontSizeMax = maxSize;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    // Placement and value are refreshed from Update rather than set once: Stagger's price is read
    // off the live player, and the lethal warning depends on current HP, which changes while the
    // card just sits in the hand. All of it is a couple of compares — nothing is written unless it
    // changed, and the layout block only runs when the rect actually resizes.
    private void TickCardFace()
    {
        bool haveName = nameText != null && nameText.gameObject.activeSelf;
        bool haveCost = staggerCostText != null && staggerCostText.gameObject.activeSelf;
        if (!haveName && !haveCost) return;

        RectTransform host = cardArtImage != null ? cardArtImage.rectTransform : (RectTransform)transform;
        Vector2 size = host.rect.size;

        if (size != faceHostSize && size.x > 0f && size.y > 0f)
        {
            faceHostSize = size;

            // Map the sprite-space fractions through preserveAspect's letterbox.
            Sprite art = cardArtImage != null ? cardArtImage.sprite : null;
            float aspect = art != null && art.rect.height > 0f ? art.rect.width / art.rect.height
                                                               : size.x / size.y;
            float dh = Mathf.Min(size.y, size.x / aspect);
            float dw = dh * aspect;
            float padX = (size.x - dw) * 0.5f;
            float padY = (size.y - dh) * 0.5f;

            if (haveCost)
            {
                RectTransform rt = staggerCostText.rectTransform;
                rt.sizeDelta = new Vector2(HEART_W * dw, HEART_H * dh);
                rt.anchoredPosition = new Vector2(padX + HEART_CX * dw, -(padY + HEART_CY * dh));
            }
            if (haveName)
            {
                RectTransform nrt = nameText.rectTransform;
                nrt.sizeDelta = new Vector2(PLATE_W * dw, PLATE_H * dh);
                nrt.anchoredPosition = new Vector2(padX + 0.5f * dw, -(padY + PLATE_CY * dh));
            }
        }

        if (!haveCost) return;

        PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
        if (p == null) return;

        float cost = p.NextStaggerCost;
        string label = Mathf.RoundToInt(cost).ToString();
        if (staggerCostText.text != label) staggerCostText.text = label;

        Color want = cost >= p.CurrentHealth ? STAGGER_LETHAL_COLOR : STAGGER_COST_COLOR;
        if (staggerCostText.color != want) staggerCostText.color = want;

        // The back's footer shows the same price, so it has to track the same way — the card can be
        // flipped when the player takes a hit and crosses the lethal threshold.
        if (back != null && back.costValue != null)
        {
            if (back.costValue.text != label) back.costValue.text = label;
            if (back.costValue.color != want) back.costValue.color = want;
        }
    }

    // --- Blompo blessing mark ------------------------------------------------------------
    // A blessed card must be identifiable at a glance in the hand. Built procedurally here (house
    // style) rather than in the CardTemplate prefab: that prefab has known scale corruption and is
    // blocked on new art, so we deliberately don't touch it.
    //
    // IT IS PARENTED TO THE ART, NOT THE CARD ROOT. The root's RectTransform is much taller than
    // the visible card, so the old badge — anchored to the root's top-right corner — floated off
    // the card's right EDGE at mid-height instead of sitting on the card at all. cardArtImage is
    // the frame the player actually sees, so anchoring there lands the mark correctly no matter
    // what the corrupt root geometry is doing.
    //
    // THE LOOK inverts the gem it replaces. The old badge was an OBJECT: a jewel set in an ornate
    // gold ring — the chrome this UI pass exists to remove. This is LIGHT INSCRIBED ON THE CARD:
    // Blompo's own sigil glowing over a soft dark halo, which lifts it off busy artwork without
    // drawing a frame.
    //
    // ⚠️ IT IS ONE COLOUR ON EVERY BLESSING, AND MUST STAY THAT WAY (designer, 2026-08-06). The
    // incoming card art telegraphs each CARD's rarity in colour — dark grey Common, light grey
    // Uncommon, yellow Rare, purple Epic (no Legendary cards yet). An earlier pass here tinted the
    // mark by the BLESSING's rarity, which is a different thing but would not survive contact with
    // a player: two colour-coded rarity systems on one card, disagreeing with each other (this one
    // called Rare azure where the art calls it yellow). So the mark carries no rarity at all, and
    // its colour is chosen to sit OUTSIDE that palette — teal is nowhere near grey, yellow or
    // purple, and is pushed green of Shift-blue so it can't be misread as a Shift cost either.
    //
    // Blessing hierarchy moved to a channel the art doesn't use: only Epic and Legendary blessings
    // PULSE. If the rarity palette ever changes, re-pick this colour against it — do not reach for
    // FlatUI.RarityColor here.
    //
    // The mark deliberately does NOT say WHICH blessing this is; all seven share one sigil and the
    // hover text names it. Seven legible glyphs at this size is a bespoke-art job, not a procedural
    // one.
    private static readonly Color BLESSED_COLOR = new Color(0.42f, 0.90f, 0.82f, 1f);
    private const float MARK_SIZE = 34f;    // the sigil itself
    private const float MARK_GLOW = 46f;    // rarity halo
    private const float MARK_SHADE = 58f;   // dark halo that separates it from the artwork
    // Offsets from the art rect's bottom-left, in its own 200x300 units.
    //
    // These are NOT arbitrary padding. cardArtImage's sprite is the WHOLE card face — painted
    // frame, cost medallions and name plate included — not just the inner picture. Measured on the
    // real cards, the inner picture panel occupies roughly 10%-80% of the card's height, so an
    // inset of 25 put the mark down inside the painted NAME PLATE, where it clipped the card's
    // title. 62 sits it just inside the picture's bottom-left corner.
    private const float MARK_INSET_X = 34f;
    private const float MARK_INSET_Y = 62f;

    private GameObject blessMark;
    private Image blessShade, blessGlow, blessSigil;
    private bool blessPulses;
    private float blessGlowAlpha;

    private void RefreshBlessingBadge(RuntimeCard card)
    {
        bool blessed = card != null && card.enhancement != CardEnhancement.None;

        if (!blessed)
        {
            if (blessMark != null) blessMark.SetActive(false);
            return;
        }

        Rarity rarity = CardEnhancements.RarityOf(card.enhancement);

        // Motion, not colour, is the hierarchy — see the note above. Only the two blessings worth
        // noticing animate, which is what makes one catch your eye in a full hand.
        blessPulses = rarity == Rarity.Epic || rarity == Rarity.Legendary;
        blessGlowAlpha = 0.30f;

        EnsureBlessMark();
        blessMark.SetActive(true);

        blessShade.color = new Color(0.02f, 0.02f, 0.03f, 0.72f);
        blessGlow.color = new Color(BLESSED_COLOR.r, BLESSED_COLOR.g, BLESSED_COLOR.b, blessGlowAlpha);
        blessSigil.color = BLESSED_COLOR;

        // Name the blessing on the back, under the card's own text. The mark says a card is
        // blessed; only this says which one.
        // Single break, not a blank line: a blessed card carries two extra lines of text on a face
        // that is already full, and the spare line was enough to push the longest combinations past
        // the box. The colour change is what separates the sections; it doesn't need whitespace too.
        bodyText = $"{myCard.cardData.description}\n" +
                   $"<color=#6BE6D1><b>{CardEnhancements.Name(card.enhancement)}</b></color>\n" +
                   $"{CardEnhancements.Description(card.enhancement)}";
    }

    private void EnsureBlessMark()
    {
        if (blessMark != null) return;

        RectTransform host = cardArtImage != null ? cardArtImage.rectTransform : (RectTransform)transform;

        blessMark = new GameObject("BlessMark", typeof(RectTransform));
        RectTransform rt = blessMark.GetComponent<RectTransform>();
        rt.SetParent(host, false);
        // Bottom-left of the picture. The cost medallion owns the top-left and the card's own
        // rarity tag owns the top-right, so this is the one corner free on every card in the set.
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(MARK_INSET_X, MARK_INSET_Y);
        rt.sizeDelta = new Vector2(MARK_SIZE, MARK_SIZE);

        blessShade = AddMarkLayer("Shade", FlatUI.SoftGlow(), MARK_SHADE);
        blessGlow = AddMarkLayer("Glow", FlatUI.SoftGlow(), MARK_GLOW);
        blessSigil = AddMarkLayer("Sigil", FlatUI.ArcaneSigil(), MARK_SIZE);
    }

    private Image AddMarkLayer(string name, Sprite sprite, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(blessMark.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    // Unscaled on purpose: blessed cards are on screen during the reward screen, which holds the
    // game paused at timeScale 0.
    private void TickBlessMark()
    {
        if (!blessPulses || blessGlow == null || blessMark == null || !blessMark.activeSelf) return;

        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.1f);
        Color c = blessGlow.color;
        c.a = blessGlowAlpha * Mathf.Lerp(0.55f, 1.25f, t);
        blessGlow.color = c;
    }

    private void Update()
    {
        if (myCard == null) return;
        UpdateSelectionVisual();
        TickBlessMark();
        TickCardFace();
    }

    private void UpdateSelectionVisual()
    {
        bool isSelected = myCard.isSelected;
        if (selectionFrame != null) selectionFrame.SetActive(isSelected);

        // The flip zoom composes with the selection bump rather than replacing it, so a selected
        // card you hover does both instead of one silently winning.
        float zoom = Mathf.Lerp(1f, flipZoom, flipT);

        // ⚠️ UNSCALED. This used to lerp on Time.deltaTime, which is ZERO on every screen that
        // pauses the game — so a reward card, the one place reading a description matters most,
        // would flip over without ever growing.
        float dt = Time.unscaledDeltaTime;

        if (!hasSlot) { LegacyMotion(isSelected, zoom, dt); return; }

        // --- In the hand: the fan owns the base pose, this adds hover and selection on top --------
        //
        // ⚠️ THE FLIP MUST LIFT AS WELL AS GROW, OR THE BACK'S FOOTER FALLS OFF THE SCREEN. The hand
        // is deliberately sunk into the bottom edge, so a card read in place would be read through
        // the screen edge. HandUI owns the lift amount because HandUI owns the sink — the two numbers
        // are the same measurement from opposite ends, and splitting them across two files is how
        // they drift apart.
        float lift = (isSelected ? selectionLiftAmount : 0f) + slotHoverLift * flipT;
        Vector2 targetPos = slotPos + Vector2.up * lift;
        float targetScale = slotScale * (isSelected ? 1.1f : 1f) * zoom;

        // A card turning towards you straightens out of the fan. The lean is charm while you are
        // glancing at the hand and friction the moment you are reading a page of text off one.
        float targetTilt = Mathf.Lerp(slotTilt, 0f, Mathf.Max(flipT, isSelected ? 1f : 0f));

        if (dealing)
        {
            dealElapsed += dt;
            if (dealElapsed >= dealDelay)
            {
                float p = Mathf.Clamp01((dealElapsed - dealDelay) / dealTime);
                // Ease-out cubic: leaves the pile fast and settles into the fan, which reads as a
                // card being DEALT. A linear slide reads as a panel opening.
                float e = 1f - Mathf.Pow(1f - p, 3f);
                livePos = Vector2.Lerp(dealFrom, targetPos, e);
                liveScale = Mathf.Lerp(slotScale * DEAL_START_SCALE, targetScale, e);
                liveTilt = Mathf.Lerp(0f, targetTilt, e);
                if (group != null) group.alpha = Mathf.Clamp01(p * 3f);
                if (p >= 1f) dealing = false;
            }
            ApplySlotPose();
            return;
        }

        // ⚠️ Frame-rate independent approach. Lerp(a, b, speed * dt) is NOT — it settles about three
        // times faster at 144fps than at 48, so the hand's feel would depend on the machine.
        float k = 1f - Mathf.Exp(-16f * dt);
        livePos = Vector2.Lerp(livePos, targetPos, k);
        liveScale = Mathf.Lerp(liveScale, targetScale, k);
        liveTilt = Mathf.LerpAngle(liveTilt, targetTilt, k);
        ApplySlotPose();
    }

    // The original path, for every screen that positions its own cards (the deck view, the card
    // chest). Untouched on purpose: those screens are not part of this change.
    private void LegacyMotion(bool isSelected, float zoom, float dt)
    {
        Vector3 targetScale = originalScale * (isSelected ? 1.1f : 1f) * zoom;
        float targetY = (isSelected ? selectionLiftAmount : 0f) + flipLift * flipT;
        float speed = dt * 15f;

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, speed);

        if (rectTransform != null)
        {
            Vector2 currentPos = rectTransform.anchoredPosition;
            Vector2 targetPos = new Vector2(currentPos.x, targetY);
            rectTransform.anchoredPosition = Vector2.Lerp(currentPos, targetPos, speed);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (DeckManager.instance != null) DeckManager.instance.SelectCard(myIndex);
        }
    }

    // Hover itself is handled by CardHoverFlip on this same GameObject — it owns the flip, the
    // counter-rotating hit target and the face swap. Nothing to do here.
}