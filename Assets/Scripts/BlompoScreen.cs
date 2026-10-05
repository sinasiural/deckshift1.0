using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Blompo's blessing screen.
//
// Flow (designer-set 2026-07-27): the player sees the three ENHANCEMENT OFFERS FIRST, then picks
// which card receives the one they chose. Choosing the card first made no sense — you'd be
// committing before knowing what you were committing to.
//
// Offers are rolled so each is valid for at least one card in the deck (CardEnhancements
// .RollOffersForDeck), so an offer is never a dead end. Free at the point of use, ONE card per
// visit; Blompo then leaves (BlompoNPC handles the vanish).
//
// Built procedurally in SALVAGE, self-instantiating under the main Canvas. Where the Scrap Forge is
// a workbench — PLANKS, fire from below, embers rising — this is an altar: dressed STONE, light
// descending from above, motes settling downward, and no wear, because his space is not a workshop
// that gets used. Same material family, opposite object, and not one new colour between them.
public class BlompoScreen : MonoBehaviour
{
    public static BlompoScreen instance;

    private CanvasGroup group;
    private RectTransform window;
    private Transform offerRow, cardRow;
    private RectTransform forgeStage;   // centred area the forging plays out in
    private TMP_Text titleText, promptText;
    private Image portraitImage;
    private TMP_FontAsset font;

    private Sprite portrait;
    private List<CardEnhancement> offers;
    private System.Action onBlessed;
    private CardEnhancement chosenOffer = CardEnhancement.None;
    private bool isOpen, blessingSpent;
    private float fitScale = 1f;   // <1 when the canvas is narrower than the panel — see FitScale

    private GameState prevState;
    private GameObject cachedHud;
    private bool hudWasActive;

    // ⚠️ STONE, LIT BY SHIFT. Blompo was the game's biggest consistency break — cold indigo panels
    // and arcane violet, a palette that exists nowhere in the world. It is also the screen with the
    // best excuse to be different, because it is the only one that is about MAGIC, and the trap is
    // to spend a new hue on that.
    //
    // Salvage's answer costs nothing: the surface is the dungeon's other structural material —
    // dressed STONE rather than the planks that pause, settings and the forge are built from, which
    // reads as a different object at a glance with no colour involved — and the light on it is
    // `Salvage.Shift`, the cyan of the altar orb and the gate seal. That is already the game's word
    // for "energised", and a blessing being put into a card is exactly that. Wood for things people
    // built; stone for things that were cut. An altar is cut.
    //
    // ⚠️ DO NOT REINTRODUCE VIOLET. Salvage has two accents for the entire game and this screen does
    // not get a third; if something here needs to stand out, use value or shape.
    private static readonly FlatUI.Theme T = new FlatUI.Theme
    {
        Backdrop = new Color(0.020f, 0.017f, 0.015f, 1f),
        Surface = new Color(0f, 0f, 0f, 0f),          // the slab supplies the surface
        SurfaceRaised = Salvage.Lit(Salvage.Ramp("stone").Sample(0.70f)),
        Border = Salvage.Lit(Salvage.Ramp("stone").Sample(0.05f)),
        BorderSoft = Salvage.Lit(Salvage.Ramp("stone").Sample(0.22f)),
        EdgeLight = Salvage.Lit(Salvage.Ramp("stone").Sample(1f), 1.8f),
        Accent = Salvage.Shift,
        TextBright = Salvage.TextBright,
        TextBody = Salvage.TextBody,
        TextMuted = Salvage.TextMuted,
        TextDisabled = Salvage.TextFaint,
    };

    // Sized against the project's 1920x1080 canvas. Height came down from 900 once the offer chips
    // shrank — at 900 there was ~200px of dead panel under them. Still tall enough for the forging
    // stage, which needs room for a 1.55x card struck at the centre.
    private const float WIN_W = 1600f, WIN_H = 762f;
    // 2:3, the card art's real aspect (1024x1536), so a chip IS a card face with nothing letterboxed.
    private const float CARD_W = 200f, CARD_H = 300f;
    // Shorter than the original 560: the ornate chrome used to fill the lower third, and without
    // it the chip was mostly empty space under the description.
    private const float OFFER_W = 380f, OFFER_H = 470f;

    // `fixedOffers` are THIS Blompo's three blessings, rolled once and owned by the NPC — the
    // screen must never re-roll, or leaving and re-entering would let the player reroll for free.
    public static void Open(Sprite blompoPortrait, List<CardEnhancement> fixedOffers, System.Action blessedCallback = null)
    {
        EnsureInstance();
        if (instance == null || instance.isOpen) return;
        instance.portrait = blompoPortrait;
        instance.offers = fixedOffers;
        instance.onBlessed = blessedCallback;
        instance.Show();
    }

    // Lets BlompoNPC roll its offers against the same deck view the screen uses.
    public static List<RuntimeCard> CollectDeckStatic()
    {
        List<RuntimeCard> all = new List<RuntimeCard>();
        DeckManager d = DeckManager.instance;
        if (d == null) return all;
        all.AddRange(d.GetCurrentHand());
        all.AddRange(d.GetDrawPile());
        all.AddRange(d.GetDiscardPile());
        return all;
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        Canvas canvas = FindRootCanvas();
        if (canvas == null) { Debug.LogWarning("BlompoScreen: no Canvas found in scene."); return; }
        GameObject go = new GameObject("BlompoScreen", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<BlompoScreen>();
        instance.Build();
    }

    private static Canvas FindRootCanvas()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas fallback = null;
        foreach (Canvas c in all)
        {
            if (c == null) continue;
            if (fallback == null) fallback = c;
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        }
        return fallback;
    }

    // ---- construction ----
    private void Build()
    {
        font = ResolveFont();
        Stretch(GetComponent<RectTransform>());
        group = gameObject.AddComponent<CanvasGroup>();

        Image backdrop = AddImage(transform, "Backdrop", null, T.Backdrop, true);
        Stretch(backdrop.rectTransform);
        Button backBtn = backdrop.gameObject.AddComponent<Button>();
        backBtn.transition = Selectable.Transition.None;
        backBtn.onClick.AddListener(Hide);

        SalvageScreen.BuildWall(transform);

        // A cut stone slab, standing. Not on chains — an altar is not furniture.
        window = AddPoint(transform, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIN_W, WIN_H));
        Image winBg = window.gameObject.AddComponent<Image>();
        winBg.sprite = SalvageSurfaces.StoneSlab(Salvage.Tex(WIN_W), Salvage.Tex(WIN_H));
        winBg.type = Image.Type.Simple;
        winBg.color = Color.white;
        winBg.raycastTarget = true;

        // The Forge is lit by fire from BELOW; Blompo is lit from ABOVE — the blessing descending.
        // Same BottomGlow sprite (it falls off on both axes, so no seam at the sides), flipped in
        // place by scaling Y negative about a centred pivot.
        Image halo = AddImage(window, "Halo", FlatUI.BottomGlow(),
            new Color(T.Accent.r, T.Accent.g, T.Accent.b, 0.075f), false);
        halo.rectTransform.anchorMin = new Vector2(0f, 1f);
        halo.rectTransform.anchorMax = new Vector2(1f, 1f);
        halo.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        halo.rectTransform.anchoredPosition = new Vector2(0f, -90f);
        halo.rectTransform.sizeDelta = new Vector2(-6f, 180f);
        halo.rectTransform.localScale = new Vector3(1f, -1f, 1f);

        // Motes settling downward — magic coming to rest, the inverse of the Forge's rising heat.
        UIEmberField.Attach(window, 22, Salvage.Shift, UIEmberField.Settings.Motes);


        // Four-point stars where the Forge has rivets: his panel is pinned by light, not fasteners.
        AddCornerStars();

        // A soft aura behind the portrait so Blompo reads as the source of the light in the room.
        RectTransform auraRt = AddPoint(window, "Aura", new Vector2(0f, 1f), new Vector2(150f, -140f), new Vector2(340f, 340f));
        auraRt.pivot = new Vector2(0.5f, 0.5f);
        Image aura = auraRt.gameObject.AddComponent<Image>();
        aura.sprite = FlatUI.SoftGlow();
        aura.color = new Color(T.Accent.r, T.Accent.g, T.Accent.b, 0.14f);
        aura.raycastTarget = false;

        RectTransform portraitRt = AddPoint(window, "Portrait", new Vector2(0f, 1f), new Vector2(70f, -48f), new Vector2(190f, 190f));
        portraitRt.pivot = new Vector2(0f, 1f);
        portraitImage = portraitRt.gameObject.AddComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;

        titleText = AddText(window, "Title", new Vector2(0f, 1f), new Vector2(290f, -52f), new Vector2(900f, 72f),
            "BLOMPO", 46f, FontStyles.Bold, T.TextBright, TextAlignmentOptions.TopLeft);
        titleText.characterSpacing = 8f;

        promptText = AddText(window, "Prompt", new Vector2(0f, 1f), new Vector2(292f, -124f), new Vector2(1100f, 44f),
            "", 24f, FontStyles.Normal, T.TextBody, TextAlignmentOptions.TopLeft);

        BuildCloseButton();

        offerRow = BuildRow("OfferRow", -230f, 44f);
        cardRow = BuildRow("CardRow", -250f, 26f);

        // Forging stage: centred, no layout group (the FX drives positions directly). Sits a little
        // below centre so the struck card doesn't collide with the title.
        forgeStage = AddPoint(window, "ForgeStage", new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(WIN_W - 200f, 560f));
        forgeStage.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    private void BuildCloseButton()
    {
        const float sz = 34f;
        RectTransform rt = AddPoint(window, "Close", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(sz, sz));
        rt.pivot = new Vector2(1f, 1f);

        Image hit = AddImage(rt, "Hit", FlatUI.Panel(5), new Color(1f, 1f, 1f, 0.05f), true);
        hit.type = Image.Type.Sliced;
        Stretch(hit.rectTransform);

        Button btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = hit;
        btn.onClick.AddListener(Hide);

        AddText(rt, "X", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sz, sz),
            "X", 20f, FontStyles.Bold, T.TextMuted, TextAlignmentOptions.Center);
    }

    // Corner stars, offset inward like the Forge's rivets so the two screens share a rhythm even
    // though the marks themselves are opposites (light vs hardware).
    private void AddCornerStars()
    {
        Vector2[] anchors = { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
        Vector2[] offsets = { new Vector2(22f, -22f), new Vector2(-22f, -22f), new Vector2(22f, 22f), new Vector2(-22f, 22f) };

        for (int i = 0; i < 4; i++)
        {
            RectTransform rt = AddPoint(window, "Star", anchors[i], offsets[i], new Vector2(18f, 18f));
            rt.pivot = new Vector2(0.5f, 0.5f);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = FlatUI.FourPointStar();
            img.color = new Color(T.Accent.r, T.Accent.g, T.Accent.b, 0.55f);
            img.raycastTarget = false;
        }
    }

    private Transform BuildRow(string name, float y, float spacing)
    {
        RectTransform rt = AddPoint(window, name, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(WIN_W - 160f, 460f));
        rt.pivot = new Vector2(0.5f, 1f);
        HorizontalLayoutGroup hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = spacing;
        hlg.childAlignment = TextAnchor.UpperCenter;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        return rt;
    }

    // ---- open / close ----
    private void Show()
    {
        isOpen = true;
        blessingSpent = false;
        chosenOffer = CardEnhancement.None;
        pendingCard = null;
        pickingPartner = false;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        prevState = GameManager.instance != null ? GameManager.instance.currentState : GameState.Playing;
        if (GameManager.instance != null)
        {
            GameManager.instance.RequestPause();
            GameManager.instance.SetGameState(GameState.Paused);
        }
        if (cachedHud == null) cachedHud = GameObject.Find("GameplayHUD");
        hudWasActive = cachedHud != null && cachedHud.activeSelf;
        if (cachedHud != null) cachedHud.SetActive(false);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(true);

        fitScale = FitScale();

        ShowOfferStep();

        StopAllCoroutines();
        StartCoroutine(OpenAnim());
    }

    private void Hide()
    {
        if (!isOpen) return;
        isOpen = false;

        if (GameManager.instance != null)
        {
            GameManager.instance.ReleasePause();
            GameManager.instance.SetGameState(prevState);
        }
        if (cachedHud != null) cachedHud.SetActive(hudWasActive);
        if (hudWasActive && HandUIDrawer.instance != null) HandUIDrawer.instance.SetLocked(false);

        gameObject.SetActive(false);

        // Only now does Blompo leave — closing without blessing keeps him around.
        if (blessingSpent)
        {
            System.Action cb = onBlessed;
            onBlessed = null;
            cb?.Invoke();
        }
    }

    // Shrinks the whole panel if the canvas is smaller than its design size. At 1600 wide this is
    // the second-widest window in the game, so it is the first after the map to run off the edge on
    // a narrow display: the canvas is 1080 * aspect logical px wide, which is 1920 at 16:9 but only
    // 1440 at 4:3.
    //
    // A UNIFORM SCALE, not a resize — unlike the map, this panel places its card and offer rows by
    // hand at absolute offsets (see the "PLACED BY HAND, NOT BY A LAYOUT GROUP" note below), so a
    // narrower window would overlap its own content rather than reflow. Scaling keeps the tuned
    // layout exactly and just makes it smaller. Same trick ShopScreenUI uses.
    //
    // Never scales UP: the panel was tuned at this size, and blowing it up on an ultrawide would
    // make it the only screen in the game that grows with the monitor.
    private float FitScale()
    {
        RectTransform parent = transform as RectTransform;
        if (parent == null) return 1f;
        Rect r = parent.rect;
        if (r.width <= 1f || r.height <= 1f) return 1f;
        return Mathf.Clamp(Mathf.Min(r.width * 0.97f / WIN_W, r.height * 0.97f / WIN_H), 0.4f, 1f);
    }

    private IEnumerator OpenAnim()
    {
        float t = 0f; const float dur = 0.22f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            group.alpha = n;
            window.localScale = Vector3.one * fitScale * (0.92f + 0.08f * EaseOutBack(n));
            yield return null;
        }
        group.alpha = 1f;
        window.localScale = Vector3.one * fitScale;
    }

    // ---- step 1: pick a blessing ----
    private void ShowOfferStep()
    {
        ClearRow(cardRow); ClearRow(offerRow);
        offerRow.gameObject.SetActive(true);
        cardRow.gameObject.SetActive(false);
        if (forgeStage != null) forgeStage.gameObject.SetActive(false);

        List<RuntimeCard> deck = CollectDeck();

        if (offers == null || offers.Count == 0)
        {
            promptText.text = deck.Count == 0
                ? "Your deck is empty. Blompo shrugs."
                : "Every card you own is already blessed. Blompo is out of ideas.";
            return;
        }

        promptText.text = "Blompo offers three blessings. Take one.";
        foreach (CardEnhancement e in offers)
        {
            // The offers are fixed, but the DECK can change between visits (cards played, exhausted,
            // already blessed). An offer with no legal target is shown dimmed and inert rather than
            // being swapped out — swapping would be a reroll by the back door.
            bool playable = CardEnhancements.CardsFor(e, deck).Count > 0;
            GameObject chip = BuildOfferChip(offerRow, e, playable);
            if (!playable) continue;

            Button b = chip.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            CardEnhancement captured = e;
            b.onClick.AddListener(() => ChooseOffer(captured));
        }
    }

    private void ChooseOffer(CardEnhancement e)
    {
        if (blessingSpent) return;
        chosenOffer = e;
        ShowCardStep();
    }

    // ---- step 2: pick the card that receives it ----
    private void ShowCardStep()
    {
        ClearRow(offerRow); ClearRow(cardRow);
        offerRow.gameObject.SetActive(false);
        cardRow.gameObject.SetActive(true);
        if (forgeStage != null) forgeStage.gameObject.SetActive(false);

        // "Understudy" is the one blessing that needs TWO picks — the card that receives it, then
        // the card it is bound to. Everything else falls through the first branch untouched.
        List<RuntimeCard> valid;
        if (pickingPartner)
        {
            valid = new List<RuntimeCard>();
            foreach (RuntimeCard c in CollectDeck())
                if (c != null && c != pendingCard) valid.Add(c);

            string recipient = pendingCard != null && pendingCard.cardData != null
                ? pendingCard.cardData.cardName : "It";
            promptText.text = $"<b>{recipient}</b> will draw… choose the card to bind it to.";
        }
        else
        {
            valid = CardEnhancements.CardsFor(chosenOffer, CollectDeck());
            promptText.text = $"<b>{CardEnhancements.Name(chosenOffer)}</b> — {CardEnhancements.Description(chosenOffer)}  Choose a card.";
        }

        // ⚠️ THIS USED TO CAP AT EIGHT CARDS AND SILENTLY DROP THE REST. With a deck of fifteen you
        // simply could not bless anything after the eighth valid card, and nothing on screen said
        // so — the same "content hidden with no indication" shape as the forge overflowing.
        //
        // Cards now WRAP onto rows at full size, exactly like the forge, and only shrink once the
        // spread would need more than MAX_ROWS. The row's height is set from the result so the
        // window still frames it.
        const float ROW_SPACING = 26f;
        const int MAX_ROWS = 2;
        float rowW = WIN_W - 160f;

        int columns = Mathf.Max(1, Mathf.FloorToInt((rowW + ROW_SPACING) / (CARD_W + ROW_SPACING)));
        float fit = 1f;
        if (Mathf.CeilToInt((float)valid.Count / columns) > MAX_ROWS)
        {
            int needed = Mathf.CeilToInt((float)valid.Count / MAX_ROWS);
            float w = Mathf.Max(96f, (rowW - ROW_SPACING * (needed - 1)) / needed);
            fit = w / CARD_W;
            columns = Mathf.Max(1, Mathf.FloorToInt((rowW + ROW_SPACING) / (w + ROW_SPACING)));
        }

        // ⚠️ PLACED BY HAND, NOT BY A LAYOUT GROUP. BuildCardChip shrinks a chip with localScale
        // (its children sit at fixed offsets derived from CARD_W, so scaling the rect alone would
        // scatter them). A GridLayoutGroup drives the child's RECT, which would then fight that
        // scale and shrink everything twice. The row's own HorizontalLayoutGroup is switched off
        // here for the same reason.
        HorizontalLayoutGroup hlg = cardRow.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        float cellW = CARD_W * fit + ROW_SPACING;
        float cellH = CARD_H * fit + ROW_SPACING;
        int rows = Mathf.CeilToInt((float)valid.Count / columns);
        ((RectTransform)cardRow).sizeDelta = new Vector2(rowW, rows * cellH - ROW_SPACING);

        for (int i = 0; i < valid.Count; i++)
        {
            RuntimeCard card = valid[i];
            GameObject chip = BuildCardChip(cardRow, card, fit);

            int col = i % columns, row = i / columns;
            // Last row is centred on whatever it holds rather than left-aligned under a full row.
            int inThisRow = Mathf.Min(columns, valid.Count - row * columns);
            RectTransform crt = (RectTransform)chip.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2((col - (inThisRow - 1) * 0.5f) * cellW, -row * cellH);

            Button b = chip.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            RuntimeCard captured = card;
            b.onClick.AddListener(() => ChooseCard(captured));
        }
    }

    // Understudy's two-step state. Reset alongside chosenOffer whenever the screen resets.
    private RuntimeCard pendingCard;
    private bool pickingPartner;

    private void ChooseCard(RuntimeCard card)
    {
        if (blessingSpent || chosenOffer == CardEnhancement.None || card == null) return;

        // Second pick: the partner. Any card in the deck qualifies except the one receiving the
        // blessing — a card that draws itself would do nothing.
        if (pickingPartner)
        {
            if (card == pendingCard) return;
            RuntimeCard recipient = pendingCard;
            pendingCard = null;
            pickingPartner = false;

            blessingSpent = true;
            StartCoroutine(ForgeRoutine(recipient, card));
            return;
        }

        if (!CardEnhancements.CanApplyTo(chosenOffer, card)) return;

        if (CardEnhancements.NeedsPartner(chosenOffer))
        {
            pendingCard = card;
            pickingPartner = true;
            ShowCardStep();
            return;
        }

        blessingSpent = true;   // lock immediately so a double-click can't forge twice
        StartCoroutine(ForgeRoutine(card, null));
    }

    // Blompo binds the blessing to the card: it takes centre stage while a ring of runes gathers
    // and closes around it, and the enhancement lands at the moment the charm sets.
    private IEnumerator ForgeRoutine(RuntimeCard card, RuntimeCard partner)
    {
        ClearRow(cardRow);
        cardRow.gameObject.SetActive(false);
        offerRow.gameObject.SetActive(false);
        forgeStage.gameObject.SetActive(true);
        for (int i = forgeStage.childCount - 1; i >= 0; i--) Destroy(forgeStage.GetChild(i).gameObject);

        promptText.text = "Blompo begins the binding.";

        // A single big copy of the card, centred on the stage.
        GameObject chipGo = BuildCardChip(forgeStage, card, 1.55f);
        RectTransform chip = chipGo.GetComponent<RectTransform>();
        chip.anchorMin = chip.anchorMax = chip.pivot = new Vector2(0.5f, 0.5f);
        chip.anchoredPosition = Vector2.zero;

        Color gem = FlatUI.RarityColor(CardEnhancements.RarityOf(chosenOffer));

        // The enhancement lands on the LAST hammer blow, and the chip is updated on that same
        // frame so the badge appears exactly when the metal is struck.
        //
        // CRITICAL: this must MUTATE the existing chip, never destroy and rebuild it. The FX
        // coroutine holds a reference to `chip` and keeps animating it after this callback — if
        // the object were destroyed here, the next line of the FX would throw on a dead
        // RectTransform, abort the coroutine, and the screen would hang on the last frame with
        // only the X button to escape. That was exactly the "gets stuck" bug.
        yield return StartCoroutine(BlompoForgeFX.Play(this, forgeStage, chip, gem, () =>
        {
            CardEnhancements.Apply(card, chosenOffer, partner);
            if (DeckManager.instance != null) DeckManager.instance.RefreshHandUI();
            StampChip(chip, card);
        }));

        string cardName = card.cardData != null ? card.cardData.cardName : "It";
        if (partner != null && partner.cardData != null)
            promptText.text = $"<b>{cardName}</b> is bound to <b>{partner.cardData.cardName}</b>. Blompo is pleased.";
        else
            promptText.text = $"<b>{cardName}</b> is now <b>{CardEnhancements.Name(chosenOffer)}</b>. Blompo is pleased.";
        yield return StartCoroutine(CloseAfter(1.4f));
    }

    // Updates an already-built card chip in place to show its new blessing. Fires on the exact
    // frame the charm binds, so BOTH stats are refreshed here — several blessings visibly change
    // them (Overstuffed adds charges, On the House zeroes the Shift cost), and seeing the number
    // move at the moment of the bind is most of the payoff.
    private void StampChip(RectTransform chip, RuntimeCard card)
    {
        if (chip == null || card == null) return;

        int maxUses = card.cardData != null ? card.MaxUses : 0;
        SetChipText(chip, "Uses", card.isInfinite ? "∞" : $"{card.currentUses}/{maxUses}");

        int cost = card.cardData != null ? card.cardData.shiftCost : 0;
        // Through the shared cost function, so a blessing that makes a card DEARER (Ritual) shows
        // its real price here too — not just the ones that make it cheaper.
        SetChipText(chip, "Cost", CardEnhancements.EffectiveCost(card, cost).ToString());

        if (card.enhancement == CardEnhancement.None || chip.Find("Badge") != null) return;

        Color gem = FlatUI.RarityColor(CardEnhancements.RarityOf(card.enhancement));
        AddText(chip, "Badge", new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(CARD_W - 16f, 28f),
            CardEnhancements.Name(card.enhancement), 16f, FontStyles.Bold, gem, TextAlignmentOptions.Center);
    }

    private void SetChipText(RectTransform chip, string childName, string value)
    {
        Transform t = chip.Find(childName);
        if (t == null) return;
        TMP_Text txt = t.GetComponent<TMP_Text>();
        if (txt != null) txt.text = value;
    }

    private IEnumerator CloseAfter(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        Hide();
    }

    // ---- chip builders ----
    private GameObject BuildCardChip(Transform parent, RuntimeCard card, float scale = 1f)
    {
        RectTransform rt = AddPoint(parent, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CARD_W, CARD_H));
        rt.localScale = Vector3.one * scale;
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        // Scaled, so a shrunk card actually reserves less room in the layout group.
        le.preferredWidth = CARD_W * scale; le.preferredHeight = CARD_H * scale;

        // ⚠️ THE CHIP IS THE CARD. It used to be a FlatUI plate with the art squeezed into a SQUARE
        // box — which letterboxed the whole 2:3 painted face down small enough that its own cost and
        // charge medallions were unreadable, which is why this screen re-printed the name, SHIFT and
        // CHARGES underneath. They are all on the card already. The face is drawn at true aspect and
        // the duplicates are gone; hovering turns it over for the effect text.
        //
        // A transparent hit plate keeps the whole chip clickable — an Image only raycasts within its
        // RECT, and the card art carries its own transparent margins.
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = true;

        // Face plus its medallion numbers — drawing cardArt alone leaves the cost and charge circles
        // empty, because those digits are TMP fields on the card prefab rather than painted art.
        CardFace.Build(rt, card);

        if (card.enhancement != CardEnhancement.None)
        {
            Color gem = FlatUI.RarityColor(CardEnhancements.RarityOf(card.enhancement));
            AddText(rt, "Badge", new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(CARD_W - 16f, 26f),
                CardEnhancements.Name(card.enhancement), 15f, FontStyles.Bold, gem, TextAlignmentOptions.Center);
        }

        // Hovering turns the chip over, exactly as in the hand. You are choosing which card to
        // PERMANENTLY alter, so being able to read what it does first is the whole point.
        CardHoverFlip.Attach(rt).Bind(card);

        return rt.gameObject;
    }

    private GameObject BuildOfferChip(Transform parent, CardEnhancement e, bool playable = true)
    {
        RectTransform rt = AddPoint(parent, "Offer", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(OFFER_W, OFFER_H));
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = OFFER_W; le.preferredHeight = OFFER_H;

        // Rarity used to be a gem set in gold. Without that frame the COLOUR has to carry rarity
        // by itself, so it now tints the sigil, the border, the name and the label together —
        // four quiet signals instead of one loud jewel.
        Color gem = FlatUI.RarityColor(CardEnhancements.RarityOf(e));
        if (!playable)
        {
            // Drained of colour so it reads as "offered, but nothing here can take it".
            float grey = (gem.r + gem.g + gem.b) / 3f;
            gem = Color.Lerp(new Color(grey, grey, grey), gem, 0.22f);
        }

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = FlatUI.Panel(5);
        bg.type = Image.Type.Sliced;
        bg.color = T.SurfaceRaised;
        bg.raycastTarget = true;

        Image frame = AddImage(rt, "Frame", FlatUI.Outline(5, 1), new Color(gem.r, gem.g, gem.b, 0.45f), false);
        frame.type = Image.Type.Sliced;
        Stretch(frame.rectTransform);

        // Glow kept TIGHT and dim. The first version was a 220px soft blob at 0.20 alpha, and that
        // haze — not the mark itself — was most of why the emblem read as a generic lens flare.
        Image glow = AddImage(rt, "Glow", FlatUI.SoftGlow(), new Color(gem.r, gem.g, gem.b, 0.13f), false);
        glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        glow.rectTransform.anchoredPosition = new Vector2(0f, -112f);
        glow.rectTransform.sizeDelta = new Vector2(150f, 150f);

        // The sigil: an inscribed arcane mark where the gem used to sit.
        //
        // ⚠️ A DIFFERENT GLYPH PER RARITY, not one shared mark recoloured. Every offer used the
        // same sigil, so colour was carrying the tier alone — and the designer reported being
        // unable to tell the tiers apart at a glance (2026-08-09). Shape is read faster than hue,
        // and it survives greyscale and colour-blindness: bare ring (Common) -> 4 rays (Rare) ->
        // 6 rays + inner ring (Epic) -> the full ornate sigil (Legendary).
        Image star = AddImage(rt, "Sigil", FlatUI.RaritySigil(CardEnhancements.RarityOf(e)), gem, false);
        star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        star.rectTransform.anchoredPosition = new Vector2(0f, -112f);
        star.rectTransform.sizeDelta = new Vector2(112f, 112f);

        AddText(rt, "Name", new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(OFFER_W - 34f, 52f),
            CardEnhancements.Name(e), 30f, FontStyles.Bold, gem, TextAlignmentOptions.Top);

        TMP_Text desc = AddText(rt, "Desc", new Vector2(0.5f, 1f), new Vector2(0f, -254f), new Vector2(OFFER_W - 60f, 130f),
            CardEnhancements.Description(e), 22f, FontStyles.Normal,
            T.TextBody, TextAlignmentOptions.Top);
        desc.enableWordWrapping = true;

        AddText(rt, "Rarity", new Vector2(0.5f, 0f), new Vector2(0f, 30f),  new Vector2(OFFER_W - 40f, 34f),
            playable ? CardEnhancements.RarityOf(e).ToString().ToUpper() : "NO VALID CARD", 18f, FontStyles.Bold,
            new Color(gem.r, gem.g, gem.b, 0.80f), TextAlignmentOptions.Center);

        if (!playable) rt.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;

        return rt.gameObject;
    }

    // Every card the player still owns. Exhausted cards are excluded — they're out of the run, so
    // blessing one would be a dead pick.
    private List<RuntimeCard> CollectDeck()
    {
        List<RuntimeCard> all = new List<RuntimeCard>();
        DeckManager d = DeckManager.instance;
        if (d == null) return all;
        all.AddRange(d.GetCurrentHand());
        all.AddRange(d.GetDrawPile());
        all.AddRange(d.GetDiscardPile());
        return all;
    }

    private void ClearRow(Transform row)
    {
        for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
    }

    // ---- small UGUI builders (house style) ----
    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private RectTransform AddPoint(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = raycast;
        return img;
    }

    private TMP_Text AddText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        RectTransform rt = AddPoint(parent, name, anchor, pos, size);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = fontSize; t.fontStyle = style; t.color = color; t.alignment = align;
        t.enableWordWrapping = false; t.raycastTarget = false; t.richText = true;
        return t;
    }

    private TMP_FontAsset ResolveFont() => FlatUI.UIFont();

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 2.70158f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
