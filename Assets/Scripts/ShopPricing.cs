using UnityEngine;

/// <summary>
/// Every price the shop quotes, in one place.
///
/// ⚠️ WHY THIS EXISTS. Before it, Shopkeeper priced EVERY card at Random.Range(40, 70) and EVERY
/// relic at Random.Range(100, 150) — rarity was never consulted at all. Set against the sell table
/// (Legendary 150 / Epic 90 / Rare 50 / Common 25) that produced a live gold exploit: a Legendary
/// bought at 100–149 sold back for a flat 150, so every one a shop offered was free money and the
/// slot stayed empty. The mirror was just as wrong — a Common cost up to six times what it returned.
///
/// The rule now: PRICE FROM RARITY, ALWAYS COMFORTABLY ABOVE THE SELL VALUE. Selling recovers value
/// and can never generate it. Sell lands at 42–47% of buy across every tier.
/// </summary>
public static class ShopPricing
{
    // ±15% so a shelf still feels like a shop rather than a price list.
    private const float Jitter = 0.15f;

    /// <summary>Haggler's discount. Read at the point of sale, not baked into the sticker.</summary>
    public const float HagglerDiscount = 0.15f;

    public static int ForRelic(RelicData relic)
    {
        if (relic == null) return 0;
        int b;
        switch (relic.rarity)
        {
            case Rarity.Boss:      b = 400; break;   // unreachable in a shop today; priced for safety
            case Rarity.Legendary: b = 320; break;
            case Rarity.Epic:      b = 190; break;
            case Rarity.Rare:      b = 110; break;
            default:               b = 60;  break;
        }
        return Jittered(b);
    }

    /// <summary>
    /// ⚠️ CardData HAS NO RARITY FIELD — card rarity exists only as paint on the artwork, so nothing
    /// in code knows a card is Epic. Until that field is added, price off what the game DOES know:
    /// charges and Shift cost. Fewer charges means a more premium card, which tracks the real
    /// power curve in CardAnchors.md (a 1-charge finisher is worth ~2.6x a 6-charge staple).
    /// </summary>
    public static int ForCard(CardData card)
    {
        if (card == null) return 0;

        // maxUses 6 -> ~45, 3 -> ~75, 1 -> ~135. Clamped so an odd asset cannot price at zero.
        int uses = Mathf.Max(1, card.maxUses);
        float b = 45f * (6f / uses);
        b += card.shiftCost * 12f;              // premium utility costs Shift AND gold
        return Jittered(Mathf.RoundToInt(Mathf.Clamp(b, 30f, 200f)));
    }

    /// <summary>The price actually charged: the sticker, less Haggler.</summary>
    public static int Effective(int basePrice)
    {
        if (RelicManager.instance != null && RelicManager.instance.HasRelic("Haggler"))
            return Mathf.Max(1, Mathf.RoundToInt(basePrice * (1f - HagglerDiscount)));
        return basePrice;
    }

    private static int Jittered(int b)
    {
        return Mathf.Max(1, Mathf.RoundToInt(b * Random.Range(1f - Jitter, 1f + Jitter)));
    }
}
