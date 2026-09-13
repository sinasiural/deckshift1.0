using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The hand: a fan of cards held at the bottom edge of the screen.
///
/// ⚠️ THIS USED TO BE A HorizontalLayoutGroup, AND ALMOST EVERY COMPLAINT ABOUT THE HAND TRACED BACK
/// TO THAT ONE COMPONENT. Designer, 2026-09-07: "i dislike the placement, the way cards are seperated
/// in our hand, the draw animation". Measured before the rebuild:
///
///   THE CARD'S BOX WAS NOT THE CARD YOU SAW. The layout group rewrote each card's rect to 200x100
///   while its artwork drew at 200x300 centred inside it, and moved the anchors to top-left. Every
///   child authored against the real card drifted — the [1]/[2] key hints ended up floating in space
///   above the cards — and CardUI/CardHoverFlip/CardBack each carry a comment working around it.
///
///   THE CONTAINER WAS SCALED TO 0.55, which is the one thing the UI rules say never to do: cards
///   came out 110x165 on a 1080 canvas, too small to read, and the 50px spacing scaled down to a 27px
///   GAP. Cards with a gap between them read as loose tiles, not as a hand.
///
///   THE DEAL WAS A ONE-AT-A-TIME CONVEYOR. Each card waited for its own flying ghost (~0.3s) plus a
///   dealDelay before the next one started, and sat at alpha 0 until then, so a four-card recall took
///   ~1.4 seconds to become readable. It also ran on SCALED time and instantiated a second full copy
///   of the card prefab per card just to throw it away.
///
/// So the layout is computed here instead (see SlotFor) and pushed to each card, which is what a fan
/// needs anyway: ⚠️ A LAYOUT GROUP CAN NEVER OWN ROTATED CHILDREN — it relays them as axis-aligned
/// list items. The same rule already cost this project the quest board's slips.
///
/// ⚠️ AND THE CARDS ARE NO LONGER TORN DOWN ON EVERY CHANGE. UpdateHandDisplay used to Destroy and
/// re-Instantiate the whole hand for any event at all, including merely SELECTING a card. Beyond the
/// churn that had a bug in it nobody had named: a freshly built card under a stationary cursor never
/// receives OnPointerEnter, so after playing a card the card now under your mouse would not flip open
/// until you jiggled the mouse. Cards are now reconciled against the hand and only the difference is
/// created or destroyed.
/// </summary>
public class HandUI : MonoBehaviour
{
    [Header("UI Ayarları")]
    public GameObject cardUIPrefab;
    public Transform handContainer;

    [Header("Fan")]
    [Tooltip("Size of a card relative to the prefab's authored 200x300. 0.8 = 160x240 on the 1080 canvas.")]
    public float cardScale = 0.8f;
    [Tooltip("Centre-to-centre distance between neighbouring cards, measured at the card's BOTTOM. " +
             "Must be well under the card width or the cards stop overlapping and read as loose tiles.")]
    public float cardPitch = 100f;
    [Tooltip("The fan never grows wider than this; pitch tightens instead. Keeps a full hand inside " +
             "the narrowest canvas (1440 at 4:3).")]
    public float maxSpread = 860f;
    [Tooltip("Degrees of lean added per card away from the centre of the fan.")]
    public float tiltStep = 8f;
    public float maxTilt = 14f;
    [Tooltip("How far each step from the centre dips the card, so the fan curves. Cards rotate about " +
             "their BOTTOM edge (the prefab's pivot), so most of the fan shape already comes from tilt.")]
    public float arcDrop = 2f;

    [Header("Yükseklik")]
    [Tooltip("Height of the centre card's BOTTOM edge above the rail. Negative sinks the hand into the " +
             "screen edge.\n\n" +
             "⚠️ THIS IS A PLATFORMER, AND THE BOTTOM OF THE SCREEN IS WHERE THE FLOOR IS (designer, " +
             "2026-09-13: the hand 'uses up too much space in the game'). Everything a card tells you " +
             "at a glance — art, Shift cost, charges, key — is in its TOP HALF; the bottom half is name " +
             "plate and frame, which the hover already shows on the back. So the hand shows only the " +
             "top half at rest, and hover does the reveal. At -120 the hand occupies the bottom ~14% " +
             "of the screen instead of ~22%, and sits entirely below the floor line of most rooms.\n\n" +
             "Do not land the cut in the card's bottom 3%-11% (the name plate): a title sliced in half " +
             "reads as a rendering fault. Either clear it or, as now, go well past it.")]
    public float baselineY = -120f;
    [Tooltip("How far a card rises while you hover it. Must exceed the sink above by enough to bring " +
             "the back's footer (SHIFT / CHARGES, in its lowest 12%) onto the screen.")]
    public float hoverLift = 145f;

    [Header("Animasyon Ayarları")]
    public Transform drawPilePosition;
    [Tooltip("Seconds for one card to travel from the draw pile to its place in the fan.")]
    public float dealFlyTime = 0.28f;
    [Tooltip("Seconds between one card leaving the pile and the next. Cards fly CONCURRENTLY — this " +
             "only offsets their starts, so a full hand deals in stagger*(n-1) + flyTime, not n*flyTime.")]
    public float dealStagger = 0.06f;
    [Tooltip("Settle time for a card that appears without travelling (a card handed straight to the hand).")]
    public float popInTime = 0.14f;

    [Header("Ses Efektleri")]
    public AudioClip drawSound;
    [Range(0f, 1f)] public float soundVolume = 0.5f;
    private AudioSource audioSource;

    private readonly List<CardUI> cards = new List<CardUI>();
    private RectTransform container;
    private Camera uiCamera;
    private Vector2 nativeCardSize = new Vector2(200f, 300f);
    private Coroutine dealSfxRoutine;
    private int frontCard = -1;
    private bool populated;

    private float CardWidth => nativeCardSize.x * cardScale;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        container = handContainer as RectTransform;
        if (container == null && handContainer != null)
            container = handContainer.GetComponent<RectTransform>();

        if (cardUIPrefab != null)
        {
            RectTransform prt = cardUIPrefab.GetComponent<RectTransform>();
            if (prt != null && prt.rect.width > 1f && prt.rect.height > 1f) nativeCardSize = prt.rect.size;
        }

        // ⚠️ SELF-HEALING, DELIBERATELY. The fan positions cards itself, so a layout group here would
        // fight it every frame and a container scale would shrink the result. Both are switched off in
        // the scene as well — this is here so that a scene revert (or someone re-adding a layout group
        // because the hierarchy "looks like it needs one") cannot silently crush the cards back to
        // 200x100 the way it did for months.
        if (container != null)
        {
            container.localScale = Vector3.one;
            LayoutGroup group = container.GetComponent<LayoutGroup>();
            if (group != null) group.enabled = false;
            ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
        }
    }

    private void OnEnable()
    {
        DeckManager.OnHandChanged += UpdateHandDisplay;
        DeckManager.OnCardPlayed += HandleCardPlayed;
    }

    private void OnDisable()
    {
        DeckManager.OnHandChanged -= UpdateHandDisplay;
        DeckManager.OnCardPlayed -= HandleCardPlayed;
    }

    private void Start()
    {
        StartCoroutine(StartWithDelay());
    }

    private IEnumerator StartWithDelay()
    {
        yield return new WaitForSeconds(0.2f);
        UpdateHandDisplay(true);
    }

    // --- Layout ---------------------------------------------------------------------------------

    private struct Slot
    {
        public Vector2 pos;
        public float tilt;
    }

    /// <summary>
    /// Where card <paramref name="i"/> of <paramref name="n"/> sits. Pure — no side effects, so it can
    /// be reasoned about and tested without a hand on screen.
    ///
    /// Positions are the card's BOTTOM-CENTRE (the prefab pivots there), which is what makes the fan
    /// work: tilt swings each card about the point where a real hand would hold it, and the hover
    /// zoom grows the card UPWARD out of the rail instead of pushing it through the screen edge.
    /// </summary>
    private Slot SlotFor(int i, int n)
    {
        float pitch = cardPitch;
        if (n > 1)
        {
            float span = (n - 1) * pitch + CardWidth;
            if (span > maxSpread) pitch = Mathf.Max(24f, (maxSpread - CardWidth) / (n - 1));
        }

        float d = i - (n - 1) * 0.5f;      // steps from the centre of the fan; 0 for a lone card

        Slot s;
        s.pos = new Vector2(d * pitch, baselineY - arcDrop * d * d);
        s.tilt = Mathf.Clamp(-d * tiltStep, -maxTilt, maxTilt);
        return s;
    }

    // --- Reconciliation -------------------------------------------------------------------------

    public void UpdateHandDisplay(bool animate)
    {
        if (DeckManager.instance == null || container == null || cardUIPrefab == null) return;

        List<RuntimeCard> hand = DeckManager.instance.GetCurrentHand();

        // Release the cards that left the hand. Anything the player just PLAYED has already been
        // detached by HandleCardPlayed and is mid-pop, so it is not in this list to begin with.
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            if (cards[i] == null) { cards.RemoveAt(i); continue; }
            if (!hand.Contains(cards[i].GetCard()))
            {
                Destroy(cards[i].gameObject);
                cards.RemoveAt(i);
            }
        }

        // Rebuild the list in hand order, reusing every card that survived.
        List<CardUI> ordered = new List<CardUI>(hand.Count);
        List<bool> isNew = new List<bool>(hand.Count);

        for (int i = 0; i < hand.Count; i++)
        {
            CardUI ui = TakeExisting(hand[i]);
            bool fresh = ui == null;
            if (fresh) ui = CreateCard();
            if (ui == null) continue;

            ui.Setup(hand[i], i);
            ordered.Add(ui);
            isNew.Add(fresh);
        }

        cards.Clear();
        cards.AddRange(ordered);
        frontCard = -1;

        Vector2 from = DrawPileLocal();
        bool travel = animate && drawPilePosition != null;
        int dealt = 0;

        for (int i = 0; i < cards.Count; i++)
        {
            Slot s = SlotFor(i, cards.Count);
            cards[i].transform.SetSiblingIndex(Depth(i, cards.Count));

            // The first population of the run snaps: a hand sliding in from nowhere at scene load
            // reads as a notification rather than as furniture.
            bool snap = !populated;
            cards[i].SetSlot(s.pos, s.tilt, cardScale, hoverLift, snap);

            if (snap || !isNew[i]) continue;

            // A NEW card either flies in from the draw pile, or — when the caller asked for no
            // animation — settles in place. Both go through BeginDeal so there is one entry path.
            if (travel) cards[i].BeginDeal(from, dealt++ * dealStagger, dealFlyTime);
            else cards[i].BeginDeal(s.pos, 0f, popInTime);
        }

        populated = true;

        if (dealSfxRoutine != null) { StopCoroutine(dealSfxRoutine); dealSfxRoutine = null; }
        if (travel && dealt > 0) dealSfxRoutine = StartCoroutine(DealSfxRoutine(dealt));
    }

    private CardUI TakeExisting(RuntimeCard card)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;
            if (!ReferenceEquals(cards[i].GetCard(), card)) continue;
            CardUI found = cards[i];
            cards.RemoveAt(i);
            return found;
        }
        return null;
    }

    private CardUI CreateCard()
    {
        GameObject go = Instantiate(cardUIPrefab, container);
        RectTransform rt = go.GetComponent<RectTransform>();

        // ⚠️ RESTORE THE PREFAB'S OWN GEOMETRY. The old layout group overwrote anchors and sizeDelta
        // at runtime, and an object that has ever been laid out keeps whatever the group last wrote.
        // Anchoring to the container's PIVOT (its bottom-centre) makes a card's anchoredPosition and
        // the rail's local space the same coordinates, so DrawPileLocal needs no correction term.
        rt.anchorMin = rt.anchorMax = container.pivot;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = nativeCardSize;

        return go.GetComponent<CardUI>();
    }

    /// <summary>The draw pile button, expressed in the rail's own coordinates.</summary>
    private Vector2 DrawPileLocal()
    {
        if (drawPilePosition == null || container == null) return Vector2.zero;

        if (uiCamera == null)
        {
            Canvas canvas = container.GetComponentInParent<Canvas>();
            // Screen Space Overlay canvases take a NULL camera; passing one silently mis-projects.
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = canvas.worldCamera;
        }

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, drawPilePosition.position);
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screen, uiCamera, out local))
            return Vector2.zero;
        return local;
    }

    private IEnumerator DealSfxRoutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (drawSound != null && audioSource != null)
            {
                // A little pitch scatter so a four-card recall isn't the same click four times.
                audioSource.pitch = Random.Range(0.92f, 1.08f);
                SfxManager.PlayOn(audioSource, drawSound, soundVolume);
            }
            // Realtime: a recall can land on a frame the game is frozen (HitStop) and the deal must
            // not stall halfway through.
            yield return new WaitForSecondsRealtime(dealStagger);
        }
        dealSfxRoutine = null;
    }

    // --- Depth ----------------------------------------------------------------------------------

    /// <summary>
    /// Sibling index for card <paramref name="i"/>. Later siblings draw on top, and the fan is stacked
    /// LEFT OVER RIGHT so each card covers its right-hand neighbour's LEFT edge.
    ///
    /// ⚠️ THE DIRECTION IS DECIDED BY THE ARTWORK, NOT BY TASTE. On the canonical card frame the
    /// charge ball sits top-left and the Shift crystal top-RIGHT, and an overlapping fan always eats
    /// one of the two. Stacking right-over-left (the obvious order, and what this did first) buried
    /// the Shift cost — the number that decides whether a card is playable at all, in a game whose
    /// whole subject is Shift. Charges are a planning number and survive being covered; a cost you
    /// cannot see is a card you cannot evaluate.
    /// </summary>
    private static int Depth(int i, int n) => n - 1 - i;

    // The card you are reading has to draw over its neighbours, and in an overlapping fan sibling
    // order IS depth. Recomputed only when the front card changes — SetSiblingIndex dirties the
    // canvas, so doing this every frame would rebuild the hand's geometry for nothing.
    private void LateUpdate()
    {
        int front = -1;
        float best = 0.02f;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;
            float p = cards[i].Prominence;
            if (p > best) { best = p; front = i; }
        }

        if (front == frontCard) return;
        frontCard = front;

        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null) cards[i].transform.SetSiblingIndex(Depth(i, cards.Count));

        if (front >= 0 && cards[front] != null) cards[front].transform.SetAsLastSibling();
    }

    // --- Playing a card -------------------------------------------------------------------------

    public void AnimateCardFromHand(int index) { }

    /// <summary>
    /// Fires from DeckManager BEFORE the card leaves the hand list, so this index still addresses it.
    /// </summary>
    private void HandleCardPlayed(int index)
    {
        if (index < 0 || index >= cards.Count) return;

        CardUI ui = cards[index];
        cards.RemoveAt(index);
        frontCard = -1;
        if (ui == null) return;

        // ⚠️ THE CARD YOU PLAYED IS THE CARD THAT FLIES OFF. This used to instantiate a whole second
        // copy of the prefab as a "ghost" and leave the real one to be destroyed by the rebuild —
        // which meant building a card (procedural blessing mark, card back, hover rig and all) purely
        // to throw it away one frame later. Detaching the real one is cheaper AND correct: the thing
        // that leaves is visibly the thing that was in your hand.
        ui.DetachForPlay();
        ui.transform.SetParent(container.parent, true);
        StartCoroutine(CardPlayPopRoutine(ui.gameObject));
    }

    private IEnumerator CardPlayPopRoutine(GameObject card)
    {
        if (card == null) yield break;

        RectTransform rt = card.GetComponent<RectTransform>();
        CanvasGroup cg = card.GetComponent<CanvasGroup>();
        if (cg == null) cg = card.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        Vector3 startScale = card.transform.localScale;
        Vector2 startPos = rt.anchoredPosition;

        float t = 0f;
        while (t < 0.07f)
        {
            if (card == null) yield break;
            t += Time.unscaledDeltaTime;
            card.transform.localScale = startScale * Mathf.Lerp(1f, 1.25f, t / 0.07f);
            yield return null;
        }

        t = 0f;
        while (t < 0.18f)
        {
            if (card == null) yield break;
            t += Time.unscaledDeltaTime;
            float p = t / 0.18f;
            card.transform.localScale = startScale * Mathf.Lerp(1.25f, 0.85f, p);
            cg.alpha = Mathf.Lerp(1f, 0f, p);
            rt.anchoredPosition = startPos + Vector2.up * (56f * p);
            yield return null;
        }

        if (card != null) Destroy(card);
    }
}
