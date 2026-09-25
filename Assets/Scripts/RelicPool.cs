using System.Collections.Generic;
using UnityEngine;

// The single place that answers "which relics may be offered to the player right now".
//
// Two rules, both of which used to be everyone's problem and are now nobody's:
//
//   1. THE POOL IS EVERY RELIC IN THE PROJECT, not a list somebody remembered to update.
//      Sourced from RelicCatalogue, which regenerates itself from the assets.
//
//   2. A RELIC YOU ALREADY OWN IS NEVER OFFERED. Chests could hand you the same relic over and
//      over, which is a dead reward — you pay the room's cost and get nothing. Filtering on the
//      live loadout also gives the designer's other requirement for free: SELLING a relic puts it
//      straight back in the pool, because ownership is read at the moment of the offer rather than
//      recorded once.
//
// Callers should treat an empty result as "no relic to give" and degrade gracefully — with 18
// relics and 5 slots that cannot happen today, but a pool can always run dry.
public static class RelicPool
{
    private static RelicCatalogue cached;

    private static RelicCatalogue Catalogue
    {
        get
        {
            if (cached == null) cached = Resources.Load<RelicCatalogue>(RelicCatalogue.ResourcePath);
            return cached;
        }
    }

    // Every relic in the project, owned or not.
    public static IReadOnlyList<RelicData> All
    {
        get
        {
            RelicCatalogue c = Catalogue;
            return c != null ? (IReadOnlyList<RelicData>)c.all : System.Array.Empty<RelicData>();
        }
    }

    /// <summary>Does the player already carry a boss relic that binds a key?</summary>
    public static bool HoldsAnArt()
    {
        if (RelicManager.instance == null) return false;
        foreach (RelicData owned in RelicManager.instance.OwnedRelics)
            if (owned != null && owned.IsArt) return true;
        return false;
    }

    public static bool IsOwned(RelicData relic)
    {
        if (relic == null || RelicManager.instance == null) return false;
        // Compare by relicID, not by asset reference: IDs are the identity everything else polls
        // (HasRelic), and two assets sharing an ID would otherwise both be offerable.
        foreach (RelicData owned in RelicManager.instance.OwnedRelics)
            if (owned != null && owned.relicID == relic.relicID) return true;
        return false;
    }

    // Everything the player does not currently hold. Optionally restricted to one rarity, and
    // optionally restricted to a curated subset (a shop or chest may still narrow the pool).
    public static List<RelicData> Offerable(Rarity? rarity = null, IReadOnlyList<RelicData> restrictTo = null)
    {
        IReadOnlyList<RelicData> source = (restrictTo != null && restrictTo.Count > 0) ? restrictTo : All;
        List<RelicData> result = new List<RelicData>();
        HashSet<string> seen = new HashSet<string>();

        foreach (RelicData r in source)
        {
            if (r == null) continue;
            // ⚠️ BOSS RELICS ARE NEVER OFFERED BY THE ORDINARY CHANNELS. They are the reward for
            // killing a boss and nothing else, so a chest or a shop must not be able to hand one
            // over. Asking for Rarity.Boss explicitly is the ONE way through — that is the boss
            // reward's own call, not a stray draw. Guarded here rather than at each call site
            // because the whole point of this class is that no caller keeps its own list.
            if (r.rarity == Rarity.Boss && (!rarity.HasValue || rarity.Value != Rarity.Boss)) continue;
            if (rarity.HasValue && r.rarity != rarity.Value) continue;
            if (IsOwned(r)) continue;

            // ⚠️ ONE ART, EVER. An Art is a boss relic that adds a KEY, and a run can now visit up
            // to five bosses out of a pool of ten — if each handed over an input the player would
            // be carrying five new keys by the end. Refusing to OFFER a second is the cheapest
            // possible cap: it needs no attunement UI, and it can never leave an inert relic
            // occupying a slot, which every "hold many, bind one" scheme does.
            if (r.IsArt && HoldsAnArt()) continue;
            // Guard against a pool listing the same relic twice, which would skew the draw.
            if (!string.IsNullOrEmpty(r.relicID) && !seen.Add(r.relicID)) continue;
            result.Add(r);
        }
        return result;
    }

    // One random un-owned relic of the given rarity, stepping DOWN through rarities if that tier is
    // exhausted. Returns null only when the player owns everything the pool can offer.
    public static RelicData PickOfferable(Rarity rarity, IReadOnlyList<RelicData> restrictTo = null)
    {
        for (int tier = (int)rarity; tier >= 0; tier--)
        {
            List<RelicData> options = Offerable((Rarity)tier, restrictTo);
            if (options.Count > 0) return options[Random.Range(0, options.Count)];
        }
        // The requested tier and everything below it is exhausted; try upward before giving up, so
        // a player who owns all the commons still gets something rather than an empty chest.
        for (int tier = (int)rarity + 1; tier <= (int)Rarity.Legendary; tier++)
        {
            List<RelicData> options = Offerable((Rarity)tier, restrictTo);
            if (options.Count > 0) return options[Random.Range(0, options.Count)];
        }
        return null;
    }

    // Draw up to `count` DISTINCT un-owned relics — for a shop stocking its shelf.
    public static List<RelicData> DrawDistinct(int count, IReadOnlyList<RelicData> restrictTo = null)
    {
        List<RelicData> pool = Offerable(null, restrictTo);
        List<RelicData> picked = new List<RelicData>();
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
