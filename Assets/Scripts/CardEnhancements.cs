using System.Collections.Generic;
using UnityEngine;

// Blompo's card enhancements. An enhancement is a permanent (for the run) upgrade attached to
// ONE COPY of a card in the player's deck.
//
// CRITICAL: enhancements live on RuntimeCard, never on CardData. CardData is a shared
// ScriptableObject asset — writing to it would upgrade that card in every future run AND dirty
// the asset on disk. RuntimeCard is per-copy, per-run, which is also what makes deck identity
// work: with two Fireballs you can bless one and leave the other alone.
//
// Design rules (designer-set 2026-07-27, revised 2026-08-14):
//   - ONE enhancement per card, with exactly one deliberate exception (Twin — see below).
//   - Blompo is free at the point of use; reaching him is the cost.
//   - One card blessed per visit.
//   - The player picks 1 of 3 randomly offered (valid) enhancements, then picks the card.
//
// ⚠️ THE SET WAS REBUILT 2026-08-14, FROM 7 TO 24. The designer's verdict on the original seven
// was that they were "too simple and not very fun", and re-reading them that was right: six of the
// seven were "cheaper, or more of the same", and only one changed how a card BEHAVES. A blessing
// that just discounts a card reads as a coupon, not a discovery. The replacements are weighted
// toward changing behaviour, taking on risk, or paying out over the length of a run.
//
// Three were CUT and should not come back as-is:
//   · On the House (costs no Shift) — measured, nine of fifteen cards already cost 0 Shift and the
//     dearest costs 2, so the whole cost-reduction axis is nearly a no-op. Ritual replaces it by
//     going the other way: pay MORE, hit harder. That is a decision; a discount is not.
//   · Extra Spicy (+50% damage) — the most boring possible upgrade, and it asked nothing of the
//     player. Finisher / Opener / Momentum / Grudge cover the same axis conditionally.
//   · Double Dip (plays twice) — it could only ever be offered on the five cards holding no
//     ConflictFlags, so most of the deck never saw it. Echo is the same idea done properly: its
//     2-second delay lets the first cast's flags expire, so it works on cards Double Dip could not.
public enum CardEnhancement
{
    None = 0,

    // charges & longevity
    Overstuffed = 1,
    NeverSayDie = 2,
    LastCall = 3,
    SleightOfHand = 4,
    Inheritance = 5,
    SlowBurn = 6,
    TimeWillCome = 7,
    OnlyChild = 8,

    // cost & Shift
    Ritual = 10,
    TollBooth = 11,
    CompoundInterest = 12,
    DonorCard = 13,

    // damage
    Grudge = 20,
    Momentum = 21,
    Finisher = 22,
    Opener = 23,
    HeavyHitter = 24,
    Glass = 25,
    LoadedDice = 26,

    // hand & timing
    Clingy = 30,
    TeachersPet = 31,
    Understudy = 32,
    Echo = 33,

    // deck
    Twin = 40,
}

public static class CardEnhancements
{
    // ---- tuning ----------------------------------------------------------------------------------
    // Every number the blessings use lives here. The designer asked for Blompo to be OVERALL MORE
    // POWERFUL, so these are deliberately generous — this is a private beta and the point is to find
    // the ceiling by hitting it, not to guess where it is.

    public const int OVERSTUFFED_CHARGES = 3;
    public const int RITUAL_EXTRA_COST = 3;
    public const float RITUAL_MULT = 2f;
    public const int INHERITANCE_CHARGES = 2;
    public const float SLEIGHT_CHANCE = 0.5f;
    public const int SLOW_BURN_REQUIRED = 2;
    public const int GRUDGE_PER_KILL = 2;
    public const float MOMENTUM_MAX = 2.6f;      // at dash speed; 1.0 standing still
    public const float FINISHER_MULT = 2f;
    public const float OPENER_MULT = 2f;
    public const float HEAVY_MULT = 3f;
    public const float GLASS_MULT = 2f;
    public const float LOADED_DICE_CHANCE = 0.75f;
    public const float LOADED_DICE_MULT = 2f;
    public const int DONOR_HEALTH_COST = 3;
    public const float ECHO_DELAY = 2f;
    public const int ONLY_CHILD_DECK_SIZE = 10;

    public static readonly CardEnhancement[] All =
    {
        CardEnhancement.Overstuffed, CardEnhancement.NeverSayDie, CardEnhancement.LastCall,
        CardEnhancement.SleightOfHand, CardEnhancement.Inheritance, CardEnhancement.SlowBurn,
        CardEnhancement.TimeWillCome, CardEnhancement.OnlyChild,
        CardEnhancement.Ritual, CardEnhancement.TollBooth, CardEnhancement.CompoundInterest,
        CardEnhancement.DonorCard,
        CardEnhancement.Grudge, CardEnhancement.Momentum, CardEnhancement.Finisher,
        CardEnhancement.Opener, CardEnhancement.HeavyHitter, CardEnhancement.Glass,
        CardEnhancement.LoadedDice,
        CardEnhancement.Clingy, CardEnhancement.TeachersPet, CardEnhancement.Understudy,
        CardEnhancement.Echo,
        CardEnhancement.Twin,
    };

    public static string Name(CardEnhancement e)
    {
        switch (e)
        {
            case CardEnhancement.Overstuffed:      return "Overstuffed";
            case CardEnhancement.NeverSayDie:      return "Never Say Die";
            case CardEnhancement.LastCall:         return "Last Call";
            case CardEnhancement.SleightOfHand:    return "Sleight of Hand";
            case CardEnhancement.Inheritance:      return "Inheritance";
            case CardEnhancement.SlowBurn:         return "Slow Burn";
            case CardEnhancement.TimeWillCome:     return "Time Will Come";
            case CardEnhancement.OnlyChild:        return "Only Child";
            case CardEnhancement.Ritual:           return "Ritual";
            case CardEnhancement.TollBooth:        return "Toll Booth";
            case CardEnhancement.CompoundInterest: return "Compound Interest";
            case CardEnhancement.DonorCard:        return "Donor Card";
            case CardEnhancement.Grudge:           return "Grudge";
            case CardEnhancement.Momentum:         return "Momentum";
            case CardEnhancement.Finisher:         return "Finisher";
            case CardEnhancement.Opener:           return "Opener";
            case CardEnhancement.HeavyHitter:      return "Heavy Hitter";
            case CardEnhancement.Glass:            return "Glass";
            case CardEnhancement.LoadedDice:       return "Loaded Dice";
            case CardEnhancement.Clingy:           return "Clingy";
            case CardEnhancement.TeachersPet:      return "Teacher's Pet";
            case CardEnhancement.Understudy:       return "Understudy";
            case CardEnhancement.Echo:             return "Echo";
            case CardEnhancement.Twin:             return "Twin";
            default: return "";
        }
    }

    // Player-facing effect text. Keep these literal — per the tone brief the humour lives in the
    // NAME, never at the cost of the player understanding what the thing does.
    public static string Description(CardEnhancement e)
    {
        switch (e)
        {
            case CardEnhancement.Overstuffed:      return $"+{OVERSTUFFED_CHARGES} charges.";
            case CardEnhancement.NeverSayDie:      return "Never runs out of charges.";
            case CardEnhancement.LastCall:         return "The first time this would burn out, it comes back fully charged instead.";
            case CardEnhancement.SleightOfHand:    return "Half the time, playing this spends no charge.";
            case CardEnhancement.Inheritance:      return $"When this burns out, another card in your deck gains {INHERITANCE_CHARGES} charges.";
            case CardEnhancement.SlowBurn:         return $"Spends no charge if you've already played {SLOW_BURN_REQUIRED} other cards this room.";
            case CardEnhancement.TimeWillCome:     return "Gains a charge for every room you finish without playing it.";
            case CardEnhancement.OnlyChild:        return $"Gains a charge each room while your deck holds fewer than {ONLY_CHILD_DECK_SIZE} cards.";
            case CardEnhancement.Ritual:           return $"Costs {RITUAL_EXTRA_COST} more Shift. Deals double damage.";
            case CardEnhancement.TollBooth:        return "Refunds its Shift if it kills something.";
            case CardEnhancement.CompoundInterest: return "Gain 1 Shift for every room since you last played it.";
            case CardEnhancement.DonorCard:        return $"Costs no Shift. Costs {DONOR_HEALTH_COST} health.";
            case CardEnhancement.Grudge:           return $"Permanently deals +{GRUDGE_PER_KILL} damage every time it kills.";
            case CardEnhancement.Momentum:         return "Deals more damage the faster you are moving.";
            case CardEnhancement.Finisher:         return "Deals double damage to enemies below half health.";
            case CardEnhancement.Opener:           return "Deals double damage to enemies at full health.";
            case CardEnhancement.HeavyHitter:      return "Half the charges. Triple the damage.";
            case CardEnhancement.Glass:            return "Double damage, but only one charge.";
            case CardEnhancement.LoadedDice:       return "Usually deals double damage. Sometimes does nothing at all.";
            case CardEnhancement.Clingy:           return "Never leaves your hand.";
            case CardEnhancement.TeachersPet:      return "Always in your opening hand, and its first play each room spends no charge.";
            case CardEnhancement.Understudy:       return "Playing this draws the card it is bound to.";
            case CardEnhancement.Echo:             return "Casts itself again two seconds later.";
            case CardEnhancement.Twin:             return "Adds a second copy of this card to your deck. Both can be blessed again.";
            default: return "";
        }
    }

    // Rarity drives the offer weighting and the badge colour.
    public static Rarity RarityOf(CardEnhancement e)
    {
        switch (e)
        {
            case CardEnhancement.NeverSayDie:
            case CardEnhancement.Twin:
                return Rarity.Legendary;

            case CardEnhancement.Ritual:
            case CardEnhancement.LastCall:
            case CardEnhancement.TimeWillCome:
            case CardEnhancement.HeavyHitter:
            case CardEnhancement.Glass:
            case CardEnhancement.Understudy:
            case CardEnhancement.Echo:
                return Rarity.Epic;

            case CardEnhancement.Clingy:
            case CardEnhancement.TollBooth:
            case CardEnhancement.Grudge:
            case CardEnhancement.Momentum:
            case CardEnhancement.SlowBurn:
            case CardEnhancement.Inheritance:
            case CardEnhancement.DonorCard:
            case CardEnhancement.LoadedDice:
            case CardEnhancement.OnlyChild:
                return Rarity.Rare;

            default:
                return Rarity.Common;
        }
    }

    // ---- card categories -------------------------------------------------------------------------

    // Cards whose actionValue is a damage number, so a damage multiplier means something.
    public static bool IsDamageCard(CardActionType t)
    {
        switch (t)
        {
            case CardActionType.Fireball:
            case CardActionType.VampiricBite:
            case CardActionType.GlassWail:
            case CardActionType.CometDive:
            case CardActionType.FreefallBlade:
                return true;
            default:
                return false;
        }
    }

    // Blessings that need a SECOND pick in the Blompo screen (the card to bind to).
    public static bool NeedsPartner(CardEnhancement e) => e == CardEnhancement.Understudy;

    // ---- eligibility -----------------------------------------------------------------------------

    public static bool CanApplyTo(CardEnhancement e, RuntimeCard card)
    {
        if (card == null || card.cardData == null) return false;
        if (e == CardEnhancement.None) return false;

        // ⚠️ ONE PER CARD, WITH TWIN AS THE ONE DELIBERATE EXCEPTION. A Twinned card counts as
        // still-unblessed and may take another blessing on top — which also means Twin can be
        // applied to a Twin, doubling again. That is intentional and was asked for by name: the
        // designer wants the ceiling found in beta rather than guessed at, and this is the only
        // route in the whole system to an exponential deck. If it turns out to be miserable rather
        // than fun, the fix is one line here, not a redesign.
        if (card.enhancement != CardEnhancement.None && card.enhancement != CardEnhancement.Twin)
            return false;

        CardData d = card.cardData;

        // Stagger is the fail-state card — never blessable. It is conjured on 0 Shift and evaporates
        // when spent, so a blessing on it would be attached to something the player never owns.
        if (d.actionType == CardActionType.Stagger) return false;

        switch (e)
        {
            // Damage multipliers are meaningless on a card with no damage number.
            case CardEnhancement.Ritual:
            case CardEnhancement.Grudge:
            case CardEnhancement.Momentum:
            case CardEnhancement.Finisher:
            case CardEnhancement.Opener:
            case CardEnhancement.LoadedDice:
                return IsDamageCard(d.actionType);

            // Both trade charges for damage, so they need a damage number AND charges to trade.
            case CardEnhancement.HeavyHitter:
            case CardEnhancement.Glass:
                return IsDamageCard(d.actionType) && !card.isInfinite && card.currentUses >= 2;

            // Meaningless on a card that cannot run out.
            case CardEnhancement.Overstuffed:
            case CardEnhancement.NeverSayDie:
            case CardEnhancement.LastCall:
            case CardEnhancement.SleightOfHand:
            case CardEnhancement.Inheritance:
            case CardEnhancement.SlowBurn:
            case CardEnhancement.TimeWillCome:
            case CardEnhancement.OnlyChild:
            case CardEnhancement.TeachersPet:
                return !card.isInfinite;

            // Pure downside on a card that already costs nothing.
            case CardEnhancement.TollBooth:
            case CardEnhancement.DonorCard:
                return d.shiftCost > 0;

            // Needs a second card in the deck to bind to.
            case CardEnhancement.Understudy:
                return CountBlessableDeck() >= 2;

            default:
                return true;
        }
    }

    private static int CountBlessableDeck()
    {
        List<RuntimeCard> deck = BlompoScreen.CollectDeckStatic();
        return deck != null ? deck.Count : 0;
    }

    public static bool CanEnhance(RuntimeCard card)
    {
        foreach (CardEnhancement e in All)
            if (CanApplyTo(e, card)) return true;
        return false;
    }

    // Every card in `deck` that could receive `e`.
    public static List<RuntimeCard> CardsFor(CardEnhancement e, List<RuntimeCard> deck)
    {
        List<RuntimeCard> valid = new List<RuntimeCard>();
        if (deck == null) return valid;
        foreach (RuntimeCard c in deck)
            if (CanApplyTo(e, c)) valid.Add(c);
        return valid;
    }

    // Rolls the offers shown BEFORE the player picks a card. Only includes enhancements that at
    // least one card in the deck can actually receive, so an offer is never a dead end.
    public static List<CardEnhancement> RollOffersForDeck(List<RuntimeCard> deck, int count = 3)
    {
        List<CardEnhancement> pool = new List<CardEnhancement>();
        foreach (CardEnhancement e in All)
            if (CardsFor(e, deck).Count > 0) pool.Add(e);
        return PickWeighted(pool, count);
    }

    public static List<CardEnhancement> RollOffers(RuntimeCard card, int count = 3)
    {
        List<CardEnhancement> pool = new List<CardEnhancement>();
        foreach (CardEnhancement e in All)
            if (CanApplyTo(e, card)) pool.Add(e);
        return PickWeighted(pool, count);
    }

    private static List<CardEnhancement> PickWeighted(List<CardEnhancement> pool, int count)
    {
        List<CardEnhancement> picked = new List<CardEnhancement>();
        while (picked.Count < count && pool.Count > 0)
        {
            int total = 0;
            foreach (CardEnhancement e in pool) total += Weight(e);

            int roll = Random.Range(0, total);
            int idx = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= Weight(pool[i]);
                if (roll < 0) { idx = i; break; }
            }

            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return picked;
    }

    // ⚠️ FLATTER THAN IT LOOKS, ON PURPOSE. The old table was 10/6/3/1, which with 24 blessings in
    // the pool made a Legendary offer genuinely rare across a 45-minute run — and Blompo is already
    // gated by having to FIND him. The designer asked for the whole system to hit harder, and the
    // cheapest honest lever is the frequency of the good stuff, not the strength of each blessing.
    private static int Weight(CardEnhancement e)
    {
        switch (RarityOf(e))
        {
            case Rarity.Legendary: return 2;
            case Rarity.Epic:      return 5;
            case Rarity.Rare:      return 7;
            default:               return 10;
        }
    }

    // ---- application -----------------------------------------------------------------------------

    // Attaches the enhancement and applies any one-shot effect. Returns false if not legal.
    public static bool Apply(RuntimeCard card, CardEnhancement e, RuntimeCard partner = null)
    {
        if (!CanApplyTo(e, card)) return false;

        card.enhancement = e;

        switch (e)
        {
            case CardEnhancement.Overstuffed:
                card.currentUses += OVERSTUFFED_CHARGES;
                break;

            // Reuses the existing isInfinite plumbing (charge checks, decrement and exhaust routing
            // all already honour it) rather than adding a parallel code path.
            case CardEnhancement.NeverSayDie:
                card.isInfinite = true;
                break;

            case CardEnhancement.HeavyHitter:
                card.currentUses = Mathf.Max(1, card.currentUses / 2);
                break;

            case CardEnhancement.Glass:
                card.currentUses = 1;
                break;

            case CardEnhancement.Understudy:
                card.understudyPartner = partner;
                break;

            case CardEnhancement.Twin:
                AddTwin(card);
                break;
        }

        Debug.Log($"BLOMPO: {card.cardData.cardName} is now {Name(e)}");
        return true;
    }

    // Drops a duplicate into the draw pile so it can turn up immediately, rather than the discard
    // where it would wait for a reshuffle.
    private static void AddTwin(RuntimeCard card)
    {
        if (DeckManager.instance == null) return;
        RuntimeCard copy = card.Clone();
        copy.enhancement = CardEnhancement.Twin;
        DeckManager.instance.GetDrawPile().Add(copy);
    }

    // ---- runtime hooks ---------------------------------------------------------------------------
    // Everything below is called from DeckManager / RelicManager / PlayerController. It lives here
    // so that adding a blessing is one file, not eight.

    // Shift cost after blessings. ⚠️ THE ONE PLACE effective cost is computed — DeckManager,
    // CardAimIndicator and BlompoScreen all call this. They used to each carry their own copy of the
    // "On the House zeroes it" rule with a comment begging whoever edited one to remember the others.
    public static int EffectiveCost(RuntimeCard card, int baseCost)
    {
        if (card == null) return baseCost;
        switch (card.enhancement)
        {
            case CardEnhancement.Ritual:    return baseCost + RITUAL_EXTRA_COST;
            case CardEnhancement.DonorCard: return 0;
            default:                        return baseCost;
        }
    }

    // Cast-time multipliers — applied to the value handed to ExecuteAction, so they scale the whole
    // action (every target of an AoE, the heal on a Bite, and so on).
    public static float ModifyActionValue(RuntimeCard card, float value)
    {
        if (card == null) return value;
        switch (card.enhancement)
        {
            case CardEnhancement.Ritual: return value * RITUAL_MULT;
            case CardEnhancement.Glass:  return value * GLASS_MULT;
            case CardEnhancement.LoadedDice:
                return Random.value < LOADED_DICE_CHANCE ? value * LOADED_DICE_MULT : 0f;
            default: return value;
        }
    }

    // Damage-time modifiers, called from RelicManager.ModifyPlayerDamage — the single chokepoint
    // every point of player damage passes through, and the only place the TARGET is known. That is
    // what lets Finisher and Opener be exact rather than guessed at cast time.
    public static float ModifyDamage(RuntimeCard card, float dmg, EnemyHealth target)
    {
        if (card == null) return dmg;

        switch (card.enhancement)
        {
            case CardEnhancement.Grudge:
                return dmg + card.grudgeBonus;

            case CardEnhancement.HeavyHitter:
                return dmg * HEAVY_MULT;

            case CardEnhancement.Momentum:
            {
                PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
                Rigidbody2D rb = p != null ? p.GetComponent<Rigidbody2D>() : null;
                if (rb == null) return dmg;
                // Normalised against the dash speed, which is the fastest the player ever moves.
                float k = Mathf.Clamp01(rb.linearVelocity.magnitude / 26f);
                return dmg * Mathf.Lerp(1f, MOMENTUM_MAX, k);
            }

            // ⚠️ Both read the target's health BEFORE this hit lands, which is correct and is only
            // true because ModifyPlayerDamage runs ahead of TakeDamage at every call site. Opener
            // means "this enemy was untouched", not "this hit left it full".
            case CardEnhancement.Finisher:
                if (target != null && target.maxHealth > 0f
                    && target.CurrentHealth / target.maxHealth < 0.5f) return dmg * FINISHER_MULT;
                return dmg;

            case CardEnhancement.Opener:
                if (target != null && target.CurrentHealth >= target.maxHealth - 0.01f)
                    return dmg * OPENER_MULT;
                return dmg;

            default:
                return dmg;
        }
    }

    // Should this play consume a charge? Called after a successful play.
    public static bool ShouldSpendCharge(RuntimeCard card, int otherCardsPlayedThisRoom)
    {
        if (card == null) return true;
        switch (card.enhancement)
        {
            case CardEnhancement.SleightOfHand:
                return Random.value >= SLEIGHT_CHANCE;
            case CardEnhancement.SlowBurn:
                return otherCardsPlayedThisRoom < SLOW_BURN_REQUIRED;
            case CardEnhancement.TeachersPet:
                return card.playedThisRoom;      // the FIRST play each room is free
            default:
                return true;
        }
    }

    // Does the card stay in hand after a successful play instead of going to a pile?
    public static bool StaysInHand(RuntimeCard card)
        => card != null && card.enhancement == CardEnhancement.Clingy
                        && (card.isInfinite || card.currentUses > 0);

    public static bool RetainsThroughRecall(RuntimeCard card)
        => card != null && (card.enhancement == CardEnhancement.Clingy
                         || card.enhancement == CardEnhancement.TeachersPet);

    public static bool WantsOpeningHand(RuntimeCard card)
        => card != null && card.enhancement == CardEnhancement.TeachersPet;

    // Fired after a card resolves and leaves the hand. `costPaid` is what it actually cost after
    // every discount, which is what Toll Booth refunds.
    public static void NotePlayed(RuntimeCard card, int costPaid)
    {
        if (card == null) return;

        card.lastCostPaid = costPaid;
        bool inHub = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub();

        if (card.enhancement == CardEnhancement.CompoundInterest && card.roomsSincePlayed > 0)
        {
            PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
            if (p != null && !inHub) p.AddShift(card.roomsSincePlayed);
        }

        if (card.enhancement == CardEnhancement.DonorCard && !inHub)
        {
            PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
            PlayerHealth h = p != null ? p.GetComponent<PlayerHealth>() : null;
            // ⚠️ PayHealthCost, NOT TakeDamage — exactly as Stagger does it. TakeDamage returns early
            // while invincible or mid-parry, so routing a deliberate price through it would make the
            // card sometimes free, which is worse than either always or never.
            if (h != null) h.PayHealthCost(DONOR_HEALTH_COST);
        }

        card.roomsSincePlayed = 0;
        card.playedThisRoom = true;
    }

    // An enemy died while this card's damage was being resolved. Called from EnemyHealth.Die, where
    // DeckManager's attributed card is still the one that dealt the killing blow.
    public static void NoteKill(RuntimeCard card)
    {
        if (card == null) return;

        if (card.enhancement == CardEnhancement.Grudge)
            card.grudgeBonus += GRUDGE_PER_KILL;

        if (card.enhancement == CardEnhancement.TollBooth && card.lastCostPaid > 0)
        {
            PlayerController p = GameManager.instance != null ? GameManager.instance.player : null;
            bool inHub = LevelManager.instance != null && LevelManager.instance.IsCurrentRoomHub();
            if (p != null && !inHub) p.AddShift(card.lastCostPaid);
            card.lastCostPaid = 0;      // one refund per play, however many things it kills
        }
    }

    // The card has run out and is about to be exhausted. Return true to rescue it instead.
    public static bool RescueFromExhaust(RuntimeCard card)
    {
        if (card == null || card.enhancement != CardEnhancement.LastCall || card.lastCallUsed)
            return false;

        card.lastCallUsed = true;
        card.currentUses = Mathf.Max(1, card.MaxUses);
        return true;
    }

    // The card really is exhausting. Death benefits fire here.
    public static void OnExhausted(RuntimeCard card)
    {
        if (card == null || card.enhancement != CardEnhancement.Inheritance) return;
        if (DeckManager.instance == null) return;

        // A random other card that can still hold charges. Random rather than "the weakest" because
        // the player cannot see which one it would pick, so a rule they cannot read is just noise.
        List<RuntimeCard> pool = new List<RuntimeCard>();
        foreach (RuntimeCard c in BlompoScreen.CollectDeckStatic())
            if (c != null && c != card && !c.isInfinite) pool.Add(c);
        if (pool.Count == 0) return;

        pool[Random.Range(0, pool.Count)].currentUses += INHERITANCE_CHARGES;
    }

    // Called once per room from PlayerController.OnNewRoomEnter, for every card in the deck.
    public static void BeginRoom(List<RuntimeCard> deck)
    {
        if (deck == null) return;
        foreach (RuntimeCard c in deck)
        {
            if (c == null) continue;

            if (!c.playedThisRoom) c.roomsSincePlayed++;
            c.playedThisRoom = false;

            if (c.isInfinite) continue;

            // Both of these pay out for NOT playing the card, so they are settled at room start,
            // after roomsSincePlayed has ticked.
            if (c.enhancement == CardEnhancement.TimeWillCome && c.roomsSincePlayed > 0)
                c.currentUses++;

            if (c.enhancement == CardEnhancement.OnlyChild && deck.Count < ONLY_CHILD_DECK_SIZE)
                c.currentUses++;
        }
    }
}
