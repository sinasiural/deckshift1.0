using UnityEngine;

// THE tuning file for the scrap economy. Every scrap number in the game is here — if a value
// needs to change, it changes here and nowhere else.
//
// WHY SCRAP EXISTS (designer, 2026-08-03): killing an enemy used to pay literally nothing.
// EnemyHealth had no drop logic at all, gold only comes from piles placed in levels, so the
// rational play was to skip every fight and platform to the exit — in a game built around a deck
// of attack cards. Scrap is the reward for engaging with combat, and it's what makes the planned
// Skirmish/Fight/Elite difficulty tiers coherent: without it, a harder room is pure downside and
// no one would ever route into one.
//
// THE TARGET THE NUMBERS BELOW AIM AT: one act of scrap income should let the player rescue
// ONE OR TWO cards they really care about — never enough to maintain the whole deck. Scarcity is
// the entire point. Charges depleting is what pushes cards into exhaust, which is what eventually
// leaves the player buying Shift off Stagger at an escalating HP price until they can't afford the
// next one. Make recharging comfortable and that pressure quietly disappears.
//
// Rough maths behind the current values: a combat room holds ~6 mixed enemies ≈ 8-12 scrap, and
// an act runs ~8 combat rooms ≈ 70-95 scrap. A fully depleted 6-charge card costs 36 to refill,
// so an act buys about two. Salvaging a card out of exhaust and refilling it costs ~48 — over
// half an act's income for one rescue, which is the intended sting.
public static class ScrapEconomy
{
    // ---- income -------------------------------------------------------------------------------

    // Kills are the MAIN source. This is the lever that changes behaviour — it's what makes
    // fighting worth doing at all, so it should always dominate total income.
    //
    // Derived from the enemy's maxHealth so new enemies tier themselves automatically, matching
    // the HP tiers in CardAnchors.md §5 (fodder 12 / grunt 25 / soldier 40 / boss 300). Any
    // enemy can override this per-prefab via EnemyHealth.scrapDropOverride — that's the hook for
    // "shift-infused" elites, which should drop noticeably more than their base version.
    public static int ScrapForEnemy(float maxHealth)
    {
        if (maxHealth <= 20f) return 1;    // fodder   — Shambler 12, Slime 10, Spitter 18
        if (maxHealth <= 30f) return 2;    // grunt    — Rotbrute 25, RangedEnemy 25, Mimic 30
        if (maxHealth <= 60f) return 3;    // soldier  — MeleeEnemy 40
        if (maxHealth <= 150f) return 6;   // heavy    — nothing here yet; reserved for elites
        return 20;                         // boss     — MossKnight 300
    }

    // A card burning its last charge leaves scrap behind. Deliberately a SMALL consolation, not an
    // income stream: it must stay far below SalvageCost so "let cards die on purpose" is never a
    // profitable strategy. Kills should out-earn this by roughly 10:1 over an act.
    public const int EXHAUST_REBATE = 2;

    // ---- costs --------------------------------------------------------------------------------

    // Putting one charge back on a card you still own.
    public const int RECHARGE_PER_CHARGE = 6;

    // Pulling a card back out of the exhaust pile. A rescue, not a refill — the card returns with
    // only half its charges (see SalvageCharges), so a full recovery is SALVAGE_COST plus the
    // recharge on top. Exhaust should always feel like a real loss.
    public const int SALVAGE_COST = 30;

    // ---- helpers ------------------------------------------------------------------------------

    // Cost to top a card back up to full. Zero when the card is already full, infinite, or null —
    // callers should treat 0 as "nothing to buy" rather than "free".
    public static int RechargeCost(RuntimeCard card)
    {
        return MissingCharges(card) * RECHARGE_PER_CHARGE;
    }

    public static int MissingCharges(RuntimeCard card)
    {
        if (card == null || card.cardData == null || card.isInfinite) return 0;
        return Mathf.Max(0, card.MaxUses - card.currentUses);
    }

    // Charges a salvaged card comes back with: half its maximum, rounded up, and always at
    // least 1 so a salvage is never a dud.
    public static int SalvageCharges(RuntimeCard card)
    {
        if (card == null || card.cardData == null) return 1;
        return Mathf.Max(1, Mathf.CeilToInt(card.MaxUses * 0.5f));
    }

    // House colour for anything scrap-flavoured (HUD, pickups, forge UI) so the currency reads as
    // one consistent thing. Warm oxidised iron — deliberately distinct from gold's yellow.
    public static readonly Color ScrapColor = new Color(0.80f, 0.49f, 0.29f);

    // NOTE: the shared UI font resolver used to live here. It moved to FlatUI.UIFont() once more
    // than one screen needed it — the economy file is the wrong home for a UI concern, and two
    // copies would eventually drift.
}
