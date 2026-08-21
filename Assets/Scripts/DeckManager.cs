using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;

public class DeckManager : MonoBehaviour
{
    public static DeckManager instance;
    public static event Action<bool> OnHandChanged;
    public static event Action<int> OnCardPlayed;
    public bool isNextCardFree = false;
    [Header("Recall Settings")]
    public int baseRecallCost = 1; // Başlangıç maliyeti
    public int currentRecallCost;  // Şu anki maliyet

    public static System.Action<int> OnRecallCostChanged;

    [Header("Referanslar")]
    public PlayerController player;

    [Header("Deste Ayarlarý")]
    // FALLBACK ONLY once a character is assigned on the Player — the character's own startingDeck
    // wins. Kept because it is also the designer's testing tool: clear the character field and this
    // list is the deck again, exactly as before characters existed.
    public List<CardData> startingDeck;

    [Header("Special Cards")]
    public CardData staggerCardData;

    private List<RuntimeCard> drawPile = new List<RuntimeCard>();
    private List<RuntimeCard> hand = new List<RuntimeCard>();
    private List<RuntimeCard> discardPile = new List<RuntimeCard>();
    private List<RuntimeCard> exhaustPile = new List<RuntimeCard>();

    // BASE capacity. Read HandCapacity, never this — a character's trait can raise it.
    public int handCapacity = 4;

    // ⚠️ THE ONE PLACE HAND SIZE IS DECIDED. Every full-hand check goes through here, so a trait
    // that grants a slot cannot be honoured by the draw and then forgotten by, say, the Teacher's
    // Pet pull or the Stagger check. Same reasoning as CardEnhancements.EffectiveCost.
    public int HandCapacity
    {
        get
        {
            // Tunnel Vision REPLACES the hand size rather than adjusting it — one card, whatever
            // else you are carrying. Checked first so it wins over every other modifier, which is
            // the point of a rule change: nothing negotiates with it.
            if (RelicManager.instance != null && RelicManager.instance.HasRelic("TunnelVision")) return 1;

            int bonus = (player != null && player.character != null)
                ? player.character.handCapacityBonus : 0;

            // Long Fuse buys its exhaust rescue with a hand slot — the Ninja's currency.
            if (RelicManager.instance != null && RelicManager.instance.HasRelic("LongFuse")) bonus -= 1;

            return Mathf.Max(1, handCapacity + bonus);
        }
    }

    // Character trait hook. Like HandCapacity, it is read rather than mirrored into a field, so it
    // can never fall out of step with the character actually being played.
    //
    // Tunnel Vision locks it too: a one-card hand means constant Recalls, so an escalating price
    // would make the relic unplayable within a single room rather than merely different.
    public bool RecallCostIsLocked =>
        (player != null && player.character != null && player.character.recallCostNeverRises)
        || (RelicManager.instance != null && RelicManager.instance.HasRelic("TunnelVision"));

    // Second Nature: the first Recall of each room is free. Reset by OnNewRoom, like the Clamp.
    private bool freeRecallUsedThisRoom = false;

    private int selectedIndex = -1;
    private bool isReloading = false;

    // Reclaimer's Clamp salvages one exhausting card per room; reset on each new room.
    private bool clampUsedThisRoom = false;

    // The RuntimeCard currently mid-play — set around ExecuteAction in PlayCard so actions
    // that need a handle on their own card (Glass Parry's refund) can capture it. Only
    // valid during that call; null at all other times.
    public RuntimeCard CardBeingPlayed { get; private set; }

    // The card CREDITED with the damage currently being resolved. Read by
    // RelicManager.ModifyPlayerDamage (Finisher / Opener / Grudge / Momentum / Heavy Hitter) and by
    // EnemyHealth.Die (Grudge / Toll Booth).
    //
    // ⚠️ IT IS NOT THE SAME THING AS CardBeingPlayed, and the difference is projectiles. Most card
    // damage resolves synchronously inside ExecuteAction, where CardBeingPlayed is live — but a
    // Fireball lands whole seconds later, long after that has been cleared. So the projectile
    // carries its own source card and sets this around its hit. Anything else that spawns a delayed
    // damage source must do the same, or its blessings silently do nothing.
    //
    // It must be nulled again afterwards. Leaving it set would credit the NEXT damage in the game —
    // a spike, a pogo bounce — to a card that had nothing to do with it.
    public RuntimeCard AttributedCard { get; set; }

    // Getterlar
    public List<RuntimeCard> GetDrawPile() { return drawPile; }
    public List<RuntimeCard> GetDiscardPile() { return discardPile; }
    public List<RuntimeCard> GetExhaustPile() { return exhaustPile; }
    public List<RuntimeCard> GetCurrentHand() { return hand; }
    public int GetSelectedIndex() { return selectedIndex; }

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        foreach (CardData data in ResolveStartingDeck())
        {
            if (data != null) drawPile.Add(new RuntimeCard(data));
        }
        ShuffleDeck();
        ReloadHand(); // Baþlangýçta animasyon olsun
        ResetRecallCost();
    }

    // The character's deck is the run's deck; `startingDeck` is the fallback. An EMPTY character
    // deck falls back rather than starting the run with no cards at all — a half-authored character
    // asset should look wrong in the Inspector, not softlock the run.
    private List<CardData> ResolveStartingDeck()
    {
        CharacterData c = player != null ? player.character : null;
        if (c != null && c.startingDeck != null && c.startingDeck.Count > 0) return c.startingDeck;
        return startingDeck;
    }

    public void SelectCard(int index)
    {
        if (isReloading || index < 0 || index >= hand.Count) return;

        if (selectedIndex != -1 && selectedIndex < hand.Count)
            hand[selectedIndex].isSelected = false;

        selectedIndex = index;
        hand[selectedIndex].isSelected = true;

        // Seçim deðiþikliðinde animasyona gerek yok (false)
        OnHandChanged?.Invoke(false);
    }

    public void DeselectCard()
    {
        if (selectedIndex != -1 && selectedIndex < hand.Count)
            hand[selectedIndex].isSelected = false;

        selectedIndex = -1;

        // Seçim iptalinde animasyona gerek yok (false)
        OnHandChanged?.Invoke(false);
    }

    public void TryCastSelectedCard()
    {
        if (isReloading || selectedIndex == -1) return;
        PlayCard(selectedIndex);
    }

    private void PlayCard(int index)
    {
        if (index >= hand.Count) return;


        RuntimeCard playedCard = hand[index];
        CardData data = playedCard.cardData;

        int cost = data.shiftCost;
        if (SkillManager.instance != null && SkillManager.instance.HasSkill(SkillType.KineticDiscount))
            cost = Mathf.Max(0, cost - 1);
        // Blompo's blessings that change what a card costs. CardAimIndicator and BlompoScreen call
        // the SAME function, so the affordability dimming and the readouts can no longer disagree
        // with what is actually charged.
        cost = Mathf.Max(0, CardEnhancements.EffectiveCost(playedCard, cost));
        if (isNextCardFree)
        {
            cost = 0;
        }
        if (player.GetCurrentShift() < cost) return;
        if (!playedCard.isInfinite && playedCard.currentUses <= 0) return;

        // Blompo: cast-time damage multipliers (Ritual, Glass, Loaded Dice). Applied to the value
        // handed to the action, so they scale every target of an AoE and not just the first.
        float actionValue = CardEnhancements.ModifyActionValue(playedCard, data.actionValue);

        CardBeingPlayed = playedCard;
        AttributedCard = playedCard;
        bool success = player.ExecuteAction(data.actionType, actionValue, out bool keepInHand);
        CardBeingPlayed = null;
        AttributedCard = null;

        // Oath tracking. Noted on `success` alone rather than inside the !keepInHand branch below,
        // because a card that stays in hand (Portal's first placement) has still been PLAYED as far
        // as "clear a room without playing a card" is concerned. Blocked and failed plays don't
        // count — nothing happened, and refusing a card shouldn't break an oath.
        if (success && QuestSystem.instance != null)
            QuestSystem.instance.NoteCardPlayed(IsStagger(playedCard));

        if (success && !keepInHand)
        {
            // Shift is deducted only when the action actually executed AND the card is actually
            // leaving the hand — Blocked plays (conflict refusal) and Failed plays (e.g. Comet Dive
            // while grounded) cost nothing. The affordability check above still gates up front.
            //
            // ⚠️ THIS LIVES INSIDE THE !keepInHand BLOCK ON PURPOSE. Every card except Portal
            // returns keepInHand = false, so for all of them this is identical to charging on
            // `success` alone. Portal is the one card that reports success while STAYING in hand
            // (its first placement), and putting the spend here is what lets it be charged once, on
            // the second placement, by the same code path as everything else. It used to charge
            // itself inside TryPlacePortal, which is why "On the House" and First One's Free did
            // nothing on it. Do not hoist this back out.
            if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub())
                player.SpendShift(cost);

            OnCardPlayed?.Invoke(index);
            player.FlashCardPlay();

            hand.RemoveAt(index);
            selectedIndex = -1;
            if (isNextCardFree)
            {
                isNextCardFree = false;
            }

            // Blompo: payouts and prices that land on play (Compound Interest, Donor Card), plus
            // recording what this play actually cost so Toll Booth can refund the real number.
            CardEnhancements.NotePlayed(playedCard, cost);
            cardsPlayedThisRoom++;

            // Blompo: "Understudy" pulls its bound partner into hand.
            if (playedCard.enhancement == CardEnhancement.Understudy)
                DrawSpecificCard(playedCard.understudyPartner);

            // Blompo: "Echo" recasts after a delay. ⚠️ THE DELAY IS THE MECHANISM, not flavour —
            // an immediate second cast is refused by CardActionExecutor.TryExecute whenever the
            // first is still holding ConflictFlags, which is exactly why the old Double Dip could
            // only ever be offered on the five flagless cards. Two seconds is comfortably past
            // every card's effect window, so Echo works on the whole deck.
            if (playedCard.enhancement == CardEnhancement.Echo)
                StartCoroutine(EchoRoutine(data.actionType, actionValue));

            // Stagger is exempt: a coin flip that secretly doubles the blood price of the one card
            // you play when you're already out of resources reads as a bug, not as a skill paying
            // off. Echo Chamber is a bonus, and it should never be able to cost you the run.
            if (SkillManager.instance != null &&
                !IsStagger(playedCard) &&
                SkillManager.instance.HasSkill(SkillType.EchoChamber) &&
                UnityEngine.Random.value < 0.5f) // <--- BURASI DÜZELDÝ
            {
                Debug.Log("ECHO CHAMBER: Çift Etki!");
                // Ýkinci kez çalýþtýr
                player.ExecuteAction(data.actionType, actionValue, out bool _);
            }
            bool inHub = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub();
            // Blompo: several blessings can skip the charge (Sleight of Hand, Slow Burn, the first
            // Teacher's Pet play each room). `- 1` because this card's own play was just counted.
            bool spendCharge = CardEnhancements.ShouldSpendCharge(playedCard, cardsPlayedThisRoom - 1);
            if (!playedCard.isInfinite && !inHub && spendCharge) playedCard.currentUses--;

            // ⚠️ STAGGER ENTERS NO PILE. It is not a card the player owns — it is conjured into the
            // hand whenever Shift hits zero and evaporates when spent. Letting it fall through to
            // the discard below (which is what used to happen) quietly enrolled it in the DECK, so
            // it came back around on later draws as a free-to-play card that costs HP, in hands
            // where the player had plenty of Shift and had never asked for it.
            if (IsStagger(playedCard))
            {
                OnHandChanged?.Invoke(false);
                return;
            }

            // Blompo: "Clingy" never leaves the hand at all — it goes straight back, so it costs a
            // hand slot forever in exchange for always being available. Once it runs dry it falls
            // through to the normal routing below and burns out like anything else.
            if (CardEnhancements.StaysInHand(playedCard))
            {
                hand.Add(playedCard);
            }
            else if (inHub || (playedCard.isInfinite || playedCard.currentUses > 0) && (!data.singleUse || playedCard.isInfinite))
            {
                discardPile.Add(playedCard);
            }
            // Blompo: "Last Call" — the first burnout of the run refills the card instead. Checked
            // ahead of Reclaimer's Clamp on purpose: this is once per RUN and card-specific, the
            // Clamp is once per ROOM and applies to anything, so spending the narrower one first
            // leaves the broader one available for a different card.
            else if (CardEnhancements.RescueFromExhaust(playedCard))
            {
                discardPile.Add(playedCard);
            }
            else if (!clampUsedThisRoom && RelicManager.instance != null
                     && RelicManager.instance.HasRelic("ReclaimersClamp"))
            {
                // Reclaimer's Clamp: the first card that would exhaust each room is salvaged —
                // it returns to hand with a single charge instead of going to the exhaust pile.
                clampUsedThisRoom = true;
                playedCard.currentUses = 1;
                hand.Add(playedCard);
            }
            // Long Fuse: a burnt-out card goes back into the DRAW pile with a single charge instead
            // of the exhaust pile. Checked last of the rescues on purpose — Last Call is once per
            // run and Reclaimer's Clamp once per room, so the narrower ones spend first and this
            // unlimited one catches whatever is left.
            //
            // It softens exhaust rather than deleting it: one charge at a time still burns down,
            // and it costs a hand slot (see HandCapacity). No scrap rebate — the card did not die.
            else if (RelicManager.instance != null && RelicManager.instance.HasRelic("LongFuse"))
            {
                playedCard.currentUses = 1;
                drawPile.Add(playedCard);
            }
            else
            {
                exhaustPile.Add(playedCard);

                // Blompo: death benefits ("Inheritance" passes its remaining life to another card).
                CardEnhancements.OnExhausted(playedCard);

                // A card burning out leaves scrap behind — a small consolation so losing a card
                // isn't a total loss, deliberately far below what it costs to salvage one back
                // (see ScrapEconomy). No hub guard needed: charges don't decrement in the hub,
                // so this branch is unreachable there.
                if (player != null) player.AddScrap(ScrapEconomy.EXHAUST_REBATE);
            }
            OnHandChanged?.Invoke(false);
        }
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            TryRecall();
        }
        // Her karede kontrol etmek yerine sadece oyun akarken bak
        // (null guard: a recompile during Play mode resets the singleton's static instance.)
        if (GameManager.instance != null && GameManager.instance.currentState == GameState.Playing)
        {
            CheckForStaggerCondition();
        }
    }
    // ---- Blompo support ------------------------------------------------------------------------

    // How many cards have been played in the current room. Slow Burn reads it; PlayerController's
    // room hook resets it. Deliberately NOT reused from QuestSystem's identical counter — that one
    // is per-OATH bookkeeping and is reset by oath logic, so borrowing it would couple a blessing
    // to whether the player happens to be carrying a contract.
    private int cardsPlayedThisRoom;

    // Called once per room, from PlayerController.OnNewRoomEnter.
    public void BeginRoomForEnhancements()
    {
        cardsPlayedThisRoom = 0;

        List<RuntimeCard> all = new List<RuntimeCard>();
        all.AddRange(drawPile); all.AddRange(hand); all.AddRange(discardPile);
        CardEnhancements.BeginRoom(all);

        PullTeachersPets();
    }

    // "Teacher's Pet" is always in the opening hand. Pulled AFTER the normal draw rather than by
    // rigging the shuffle, so it cannot break the draw's own accounting; if the hand is already
    // full the pet displaces the last ordinary card, which goes back to the draw pile.
    private void PullTeachersPets()
    {
        for (int i = drawPile.Count - 1; i >= 0; i--)
        {
            RuntimeCard c = drawPile[i];
            if (!CardEnhancements.WantsOpeningHand(c)) continue;
            if (hand.Contains(c)) continue;
            if (hand.Count >= HandCapacity)
            {
                RuntimeCard bumped = null;
                for (int h = hand.Count - 1; h >= 0; h--)
                    if (!CardEnhancements.WantsOpeningHand(hand[h]) && !IsStagger(hand[h])) { bumped = hand[h]; break; }
                if (bumped == null) return;          // hand is all pets; leave it alone
                hand.Remove(bumped);
                drawPile.Add(bumped);
            }
            drawPile.RemoveAt(i);
            hand.Add(c);
        }
        OnHandChanged?.Invoke(false);
    }

    // "Understudy": pull the bound partner out of wherever it is and into hand.
    private void DrawSpecificCard(RuntimeCard target)
    {
        if (target == null || hand.Contains(target) || hand.Count >= HandCapacity) return;

        if (drawPile.Remove(target) || discardPile.Remove(target))
        {
            hand.Add(target);
            OnHandChanged?.Invoke(true);
        }
        // Not found means it is exhausted, or it is the card that was just played. Silently doing
        // nothing is right: the bond is a bonus, and failing it must never block the play.
    }

    // "Echo": recast after a delay, so the first cast's ConflictFlags have expired.
    private System.Collections.IEnumerator EchoRoutine(CardActionType type, float value)
    {
        yield return new WaitForSeconds(CardEnhancements.ECHO_DELAY);
        if (player == null || GameManager.instance == null) yield break;
        if (GameManager.instance.currentState != GameState.Playing) yield break;
        player.ExecuteAction(type, value, out bool _);
    }

    // Lets systems that mutate cards IN PLACE ask the hand UI to redraw. Blompo needs this: a
    // blessing changes an existing RuntimeCard without adding/removing/playing anything, so no
    // normal hand event fires and the new badge would not appear until the next redraw.
    // OnHandChanged is an event, so only DeckManager can raise it — hence this wrapper.
    public void RefreshHandUI()
    {
        OnHandChanged?.Invoke(false);
    }

    public void ResetRecallCost()
    {
        currentRecallCost = baseRecallCost;
        OnRecallCostChanged?.Invoke(currentRecallCost);
    }

    // Clears per-room relic state (currently Reclaimer's Clamp's once-per-room salvage).
    public void ResetRoomRelicState()
    {
        clampUsedThisRoom = false;
        freeRecallUsedThisRoom = false;   // Second Nature
    }

    // Glass Parry's mastery refund: gives one charge back to a card that was already
    // played this frame. If spending that charge exhausted the card, pull it back out
    // of the exhaust pile — perfect play means the card never really left.
    public void RefundCharge(RuntimeCard card)
    {
        if (card == null) return;
        if (!card.isInfinite)
            card.currentUses = Mathf.Min(card.currentUses + 1, card.MaxUses);
        if (exhaustPile.Remove(card))
            discardPile.Add(card);
        OnHandChanged?.Invoke(false);
    }

    // --- Scrap forge operations ----------------------------------------------------------------
    // Both are all-or-nothing: the scrap is only spent if the operation actually applies, so a
    // failed call leaves the player's wallet and the piles untouched. The UI (ScrapForgeScreen)
    // gates on the same conditions, but these re-check independently — never trust the caller.

    // Tops a card the player still owns back up to full charges.
    public bool TryRechargeCard(RuntimeCard card)
    {
        if (card == null || player == null) return false;
        if (exhaustPile.Contains(card)) return false;   // must be Salvaged first, not recharged

        int missing = ScrapEconomy.MissingCharges(card);
        if (missing <= 0) return false;                 // already full, or infinite

        int cost = ScrapEconomy.RechargeCost(card);
        if (!player.TrySpendScrap(cost)) return false;

        card.currentUses = card.MaxUses;
        OnHandChanged?.Invoke(false);
        return true;
    }

    // Pulls a card back out of the exhaust pile. It returns to the DISCARD pile (not the hand) so
    // it re-enters the deck through the normal shuffle, and comes back only half charged — exhaust
    // is meant to stay a real loss, so a full recovery costs the salvage plus a recharge on top.
    public bool TrySalvageCard(RuntimeCard card)
    {
        if (card == null || player == null) return false;
        if (!exhaustPile.Contains(card)) return false;

        if (!player.TrySpendScrap(ScrapEconomy.SALVAGE_COST)) return false;

        exhaustPile.Remove(card);
        card.currentUses = ScrapEconomy.SalvageCharges(card);
        card.isSelected = false;
        discardPile.Add(card);
        OnHandChanged?.Invoke(false);
        return true;
    }

    // Called by LevelManager.SpawnNextRoom at the moment a COMBAT room ends, while the
    // ending room's hand still exists (the reload that discards it comes right after).
    // Held payoff cards trigger here — Dead Weight: +actionValue Shift per copy still
    // in hand. Recalling earlier discarded it and forfeited this.
    public void OnRoomEnd()
    {
        foreach (RuntimeCard card in hand)
        {
            if (card.cardData != null && card.cardData.actionType == CardActionType.DeadWeight)
            {
                int payout = Mathf.RoundToInt(card.cardData.actionValue);
                player.AddShift(payout);
                Debug.Log($"DEAD WEIGHT held to room end: +{payout} Shift.");
            }
        }
    }
    // Stagger is identified by ACTION TYPE, not by asset reference, so every rule below holds for
    // any card that staggers — and can't be broken by renaming or duplicating the asset.
    public static bool IsStagger(RuntimeCard card)
    {
        return card != null && card.cardData != null
            && card.cardData.actionType == CardActionType.Stagger;
    }

    // Stagger appears the moment you hit ZERO SHIFT — that alone, nothing else.
    //
    // It used to also require an otherwise unplayable hand, because Stagger was a death sentence
    // and handing it over early would have been handing over a loss. It isn't one any more: it buys
    // Shift with HP at an escalating price (PlayerController.PerformStagger), so it is the pump you
    // reach for when you're out, and gating it behind "and every card is spent too" would hide the
    // option at exactly the moment it's the decision the player should be making.
    private void CheckForStaggerCondition()
    {
        if (LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub()) return;
        if (player.GetCurrentShift() > 0) return;

        foreach (RuntimeCard card in hand)
            if (IsStagger(card)) return;   // already holding one — never stack them

        // Ace Up the Sleeve: once per run, running dry pays out instead of billing you. Checked
        // BEFORE the card is conjured, so the player never sees the Stagger at all — a card that
        // appeared and then vanished would read as a glitch rather than as a rescue.
        if (RelicManager.instance != null && RelicManager.instance.TryConsumeAceUpTheSleeve())
        {
            player.AddShift(AceUpTheSleeveShift);
            Debug.Log($"🃏 Ace Up the Sleeve: +{AceUpTheSleeveShift} Shift instead of a Stagger. Once per run.");
            return;
        }

        AddStaggerCardToHand();
    }

    public const int AceUpTheSleeveShift = 20;

    private void AddStaggerCardToHand()
    {
        if (staggerCardData == null) return;

        // Deliberately appended past handCapacity rather than displacing a card: Stagger is an extra
        // option, not a punishment that eats the hand you were dealt. A Recall trims back to
        // capacity naturally, with Stagger retained (see ReloadRoutine).
        RuntimeCard staggerInstance = new RuntimeCard(staggerCardData);

        // Stagger has no charges. Its cost is the HP price, which rises forever, so a charge count
        // would be a second limiter doing nothing — and it keeps Stagger out of the Scrap Forge's
        // repair list, where a card that can never be damaged has no business appearing.
        staggerInstance.isInfinite = true;

        hand.Add(staggerInstance);

        OnHandChanged?.Invoke(true);
    }
    public void TryRecall()
    {
        // 1. Zaten el yenileniyorsa dur
        if (isReloading) return;

        bool inHub = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub();
        bool overclocked = RelicManager.instance != null && RelicManager.instance.HasRelic("OverclockedRecall");

        if (overclocked)
        {
            // Overclocked Recall: no Shift cost — paid in blood instead (5 HP per Recall,
            // never in the sandbox hub). Cost escalation is irrelevant when Shift is free.
            if (!inHub) player.TakeDamage(5);
        }
        else
        {
            // Tunnel Vision pays nothing, ever. Second Nature waives only the first of each room.
            // Both are resolved BEFORE the affordability check, or a player at 0 Shift would be
            // refused a Recall they were never going to be charged for.
            bool relicFree = RelicManager.instance != null
                             && RelicManager.instance.HasRelic("TunnelVision");
            if (!relicFree && !freeRecallUsedThisRoom && RelicManager.instance != null
                && RelicManager.instance.HasRelic("SecondNature"))
            {
                relicFree = true;
                if (!inHub) freeRecallUsedThisRoom = true;   // the hub must not burn the freebie
            }

            // 2. Maliyet kontrolü
            if (!relicFree && player.GetCurrentShift() < currentRecallCost)
            {
                Debug.Log("Yetersiz Shift! Recall yapılamıyor.");
                // Buraya "Yetersiz Enerji" sesi veya görseli eklenebilir
                return;
            }

            // 3. Shift Harca + 4. Maliyeti Artır (Level bitene kadar)
            if (!inHub && !relicFree)
            {
                player.SpendShift(currentRecallCost);
                // The Ninja's "Fast Hands": the price never climbs, so cycling the hand is a real
                // strategy instead of something the escalation quietly teaches you not to do. The
                // recall still COSTS — it just stops getting worse.
                if (!RecallCostIsLocked) currentRecallCost++;
                OnRecallCostChanged?.Invoke(currentRecallCost);
            }
        }

        // Flywheel: the refresh detonates, and it hits harder the deeper into the room you are —
        // the escalating price becomes the payoff instead of a tax.
        //
        // ⚠️ 10x the cost, not the cost. At 1x this dealt 1-4 damage against enemies with 12-40 HP,
        // which is indistinguishable from nothing (designer, 2026-08-21).
        //
        // Read AFTER the block above, so it uses the price actually standing at this moment. Routed
        // through ModifyPlayerDamage like every other player damage source, so relics and blessings
        // apply and a future source cannot forget them.
        if (RelicManager.instance != null && RelicManager.instance.HasRelic("Flywheel"))
        {
            float blast = currentRecallCost * 10f;
            // Dedup by component: an enemy with several colliders would otherwise be hit once per
            // collider. Same guard MeteorGreaves uses.
            HashSet<EnemyHealth> struck = new HashSet<EnemyHealth>();
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(player.transform.position, 4.5f))
            {
                EnemyHealth eh = hit.GetComponentInParent<EnemyHealth>();
                if (eh == null || !struck.Add(eh)) continue;
                eh.TakeDamage(RelicManager.instance.ModifyPlayerDamage(blast, eh));
            }
            if (CameraShake.instance != null) CameraShake.instance.Shake(0.15f, 0.3f);
        }

        // Oath tracking, placed after every early-return above so a REFUSED recall (not enough
        // Shift) doesn't break the No Take-Backs oath — the player didn't get one.
        if (QuestSystem.instance != null) QuestSystem.instance.NoteRecall();

        // Recall discards the hand, so a Portal that placed its first half and never its second
        // would leave that half orphaned in the room with firstPortalInstance still pointing at it.
        // The next Portal drawn in the same room would then place the SECOND portal on its first
        // click and charge for it.
        player.CancelPendingPortal();
        player.ClearReturnAnchor();   // same reasoning for Second Thoughts' marker

        // 5. Asıl işlemi başlat
        ReloadHand();
    }
    public void ReloadHand()
    {
        if (isReloading) return;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        DeselectCard();
        yield return new WaitForSeconds(0.2f);

        // Blompo: "Clingy" cards are held back instead of being discarded, so they survive the
        // Recall and are still in hand afterwards. They occupy their slot, so a hand full of
        // Clingy cards simply doesn't refresh — that's the intended trade-off.
        //
        // ⚠️ STAGGER IS RETAINED THE SAME WAY, AND CAN NEVER BE DISCARDED. Recalling it away would
        // be the obvious dodge — spend a Shift you don't have to make the bill disappear — and the
        // less obvious problem is that discarding it puts it in the deck. Once it is in your hand
        // the only way out is to play it. It costs a slot until you do, which is the pressure.
        List<RuntimeCard> retained = new List<RuntimeCard>();
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] != null && (CardEnhancements.RetainsThroughRecall(hand[i]) || IsStagger(hand[i])))
                retained.Add(hand[i]);
            else
                discardPile.Add(hand[i]);
        }
        hand.Clear();
        hand.AddRange(retained);

        for (int i = hand.Count; i < HandCapacity; i++)
        {
            if (drawPile.Count == 0 && discardPile.Count > 0)
            {
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                ShuffleDeck();
            }

            if (drawPile.Count > 0)
            {
                RuntimeCard c = drawPile[0];
                drawPile.RemoveAt(0);
                c.isSelected = false;
                hand.Add(c);
            }
        }

        // EL YENÝLENDÝ: Animasyon ÝSTÝYORUZ (true)
        OnHandChanged?.Invoke(true);
        isReloading = false;
    }

    public void DrawCard()
    {
        if (hand.Count >= HandCapacity) return;

        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0) return;
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            ShuffleDeck();
        }

        RuntimeCard c = drawPile[0];
        drawPile.RemoveAt(0);
        c.isSelected = false;
        hand.Add(c);

        // Tek kart çekme: Ýsteðe baðlý. Þimdilik animasyonsuz olsun ki hýzlý aksýn.
        // Ýstersen bunu 'true' yapabilirsin.
        OnHandChanged?.Invoke(false);
    }

    public void AddCardToDeck(CardData newCardData)
    {
        RuntimeCard newCardInstance = new RuntimeCard(newCardData);
        discardPile.Add(newCardInstance);
    }

    private void ShuffleDeck()
    {
        for (int i = 0; i < drawPile.Count; i++)
        {
            RuntimeCard temp = drawPile[i];
            int rnd = UnityEngine.Random.Range(i, drawPile.Count);
            drawPile[i] = drawPile[rnd];
            drawPile[rnd] = temp;
        }
    }
}