using System.Collections.Generic;
using UnityEngine;

// The single place that answers "which cards may be offered to the player".
//
// ⚠️ CARD AVAILABILITY IS NO LONGER GATED BY ACHIEVEMENTS (designer 2026-08-09: the achievement
// system went in far too early and is regretted; a proper one for cards/relics comes near release).
// RewardManager used to pull its pool from AchievementManager.GetAvailableCardPool(), which
// returned only `defaultUnlockedCards` plus the reward cards of COMPLETED challenges — with one
// challenge authored, that meant most of the roster was unreachable and nothing said so.
//
// AchievementManager still tracks and saves challenges; it simply no longer decides what exists.
public static class CardPool
{
    private static CardCatalogue cached;

    private static CardCatalogue Catalogue
    {
        get
        {
            if (cached == null) cached = Resources.Load<CardCatalogue>(CardCatalogue.ResourcePath);
            return cached;
        }
    }

    // Every card asset in the project, including ones that must never be offered.
    public static IReadOnlyList<CardData> All
    {
        get
        {
            CardCatalogue c = Catalogue;
            return c != null ? (IReadOnlyList<CardData>)c.all : System.Array.Empty<CardData>();
        }
    }

    // Stagger is not a card the player OWNS: DeckManager conjures it into the hand the moment Shift
    // hits zero and it evaporates when spent, entering no pile. Offering it as a reward or stocking
    // it in a shop would put a permanent copy in the deck that arrives on ordinary draws — a card
    // that only charges HP, handed to a player who never asked for it. Identified by DeckManager's
    // own reference rather than by name, so renaming the asset cannot silently reintroduce it.
    //
    // Borrowed Steel is the same kind of card (designer, 2026-09-27: "that card should only be
    // available in the boss fight"): the Ninja's stars, picked up off his arena floor and conjured
    // into the hand as a quiver. It was reachable from chests and shops because this test only knew
    // about Stagger. Identified by ACTION TYPE, the same handle DeckManager's quiver rules use, so a
    // renamed or duplicated asset stays excluded and the test works with no DeckManager in the scene.
    public static bool IsRewardable(CardData card)
    {
        if (card == null) return false;
        if (DeckManager.instance != null && card == DeckManager.instance.staggerCardData) return false;
        if (card.actionType == CardActionType.SalvagedShuriken) return false;
        return true;
    }

    // Everything that may legitimately be offered, optionally narrowed to a curated subset.
    // An empty/absent `restrictTo` means the whole roster, which is the normal case.
    public static List<CardData> Offerable(IReadOnlyList<CardData> restrictTo = null)
    {
        IReadOnlyList<CardData> source = (restrictTo != null && restrictTo.Count > 0) ? restrictTo : All;
        List<CardData> result = new List<CardData>();
        HashSet<CardData> seen = new HashSet<CardData>();

        foreach (CardData c in source)
        {
            if (!IsRewardable(c)) continue;
            if (!seen.Add(c)) continue;      // a pool listing the same card twice would skew the draw
            result.Add(c);
        }
        return result;
    }

    // One card, at random, from whatever may legitimately be offered. Null only if the project has
    // no offerable cards at all.
    public static CardData PickRewardable(IReadOnlyList<CardData> restrictTo = null)
    {
        List<CardData> pool = Offerable(restrictTo);
        if (pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    // Draw up to `count` DISTINCT cards — for a reward screen or a shop shelf.
    public static List<CardData> DrawDistinct(int count, IReadOnlyList<CardData> restrictTo = null)
    {
        List<CardData> pool = Offerable(restrictTo);
        List<CardData> picked = new List<CardData>();
        int n = Mathf.Min(count, pool.Count);
        for (int i = 0; i < n; i++)
        {
            int idx = Random.Range(0, pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return picked;
    }
}
