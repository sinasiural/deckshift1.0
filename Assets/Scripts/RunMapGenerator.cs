using System.Collections.Generic;
using UnityEngine;

// Builds a RunMap for one act.
//
// The shape is Slay-the-Spire's: a fixed number of routes are CARVED from the bottom of the act to
// the top, one column-step at a time, and wherever two routes land on the same slot they share a
// node and the paths merge. That is what produces a graph that genuinely branches and rejoins,
// rather than N parallel lanes that never interact.
//
// Deterministic for a given seed (System.Random, not UnityEngine.Random — generating a map must not
// disturb the global random sequence the rest of the game is drawing from, and a reproducible seed
// is what makes the generator testable at all).
[System.Serializable]
public class RunMapSettings
{
    // ⚠️ WAS 8, WHICH WAS ONE ACT. Acts are gone (designer, 2026-08-21) and the whole run is now a
    // single map, so the depth has to cover a whole run rather than a third of one.
    //
    // 20 (designer's call, 2026-08-21 — 15 "does not sit very well"). The map screen scrolls now, so
    // depth is no longer bounded by what fits on one sheet.
    //
    // It also widens the boss maths comfortably: optional bosses start at floor 3 and need 2 floors
    // between them, so the placeable slots run 3, 5, 7 … 18 — nine of them, against a 2–5 range, so
    // the generator has spare legal spots even after avoidability rejections.
    [Tooltip("Total rows INCLUDING the hub row and the final boss row. 20 = hub + 18 combat floors " +
             "+ the final boss. The map screen scrolls, so this is not limited by screen height.")]
    public int floors = 20;

    [Tooltip("Widest the act can get, in columns.")]
    public int width = 5;

    [Tooltip("How many routes are carved bottom-to-top. More routes = a denser, more connected act. " +
             "Below 3 the act stops feeling like a choice; above width it just re-treads slots. " +
             "Matching this to width puts one route in every column, which keeps the middle of the " +
             "act populated — at 4-on-5 no route ever starts in the centre column and the act draws " +
             "as two arcs around an empty middle.")]
    public int pathCount = 5;

    [Range(0f, 1f)]
    [Tooltip("Chance a Fight or Elite carries a recharge room. Skirmishes NEVER can — that " +
             "restriction is the run economy, not a tuning value.")]
    public float rechargeChance = 0.35f;

    [Tooltip("Guarantee at least one Foundry and one Market exist somewhere in the act. Note this " +
             "guarantees they EXIST, not that any single route reaches them — that tension is the point.")]
    public bool guaranteeCoreRecharges = true;

    // ---- optional bosses ------------------------------------------------------------------------
    // ⚠️ These place bosses ON the map, not at the end of it. The FinalBoss is separate and always
    // exists; these are the ones the player chooses whether to fight.

    [Tooltip("How many OPTIONAL boss nodes to place. The player still chooses how many to actually " +
             "fight — this is how many are on offer. Fewer are placed if the map cannot fit them " +
             "while keeping every one avoidable.")]
    public int bossesMin = 2;
    public int bossesMax = 5;

    [Tooltip("Earliest floor an optional boss may appear on. Floor 1 is the run's first room and is " +
             "far too early — the player has four cards and no relics.")]
    public int bossEarliestFloor = 3;

    [Tooltip("Minimum floors between two optional bosses. Back-to-back bosses are not a route " +
             "choice, they are a wall: there is no chance to recover between them.")]
    public int bossFloorSpacing = 2;

    // Which recharge types this run is allowed to place. RunMapManager narrows this to the ones
    // LevelManager actually has a room prefab for.
    //
    // THE MAP MUST NEVER PROMISE SOMETHING IT CANNOT DELIVER. A Foundry icon on a branch the player
    // routes three floors to reach, which then spawns nothing because the prefab slot is empty, is
    // worse than no Foundry at all — it spends the player's Shift on a lie. None of the three
    // recharge rooms are built yet, so today this list resolves to empty and no recharge icons are
    // drawn; each one starts appearing on its own the moment its prefab is assigned.
    [HideInInspector]
    public List<RechargeType> allowedRecharges = new List<RechargeType>
    {
        RechargeType.Foundry, RechargeType.Market, RechargeType.Well
    };
}

public static class RunMapGenerator
{
    private struct Edge { public int from, to; }

    public static RunMap Generate(RunMapSettings s, int seed)
    {
        if (s == null) s = new RunMapSettings();

        int floors = Mathf.Max(3, s.floors);
        int width = Mathf.Max(1, s.width);
        int paths = Mathf.Clamp(s.pathCount, 1, Mathf.Max(1, width));

        System.Random rng = new System.Random(seed);

        RunMap map = new RunMap { floors = floors, seed = seed };

        // Slot grid: which node id occupies (floor, column), or -1. Sharing a slot is how two
        // carved routes merge into one node.
        int[,] slot = new int[floors, width];
        for (int f = 0; f < floors; f++)
            for (int c = 0; c < width; c++)
                slot[f, c] = -1;

        int mid = width / 2;
        MapNode start = NewNode(map, slot, 0, mid, MapNodeType.Start);
        MapNode boss = NewNode(map, slot, floors - 1, mid, MapNodeType.FinalBoss);

        // Edges already carved between each pair of floors, used for the anti-crossing rule.
        List<Edge>[] carved = new List<Edge>[floors];
        for (int f = 0; f < floors; f++) carved[f] = new List<Edge>();

        int topCombatFloor = floors - 2;

        // Carve each route across the combat floors. Starting columns are spread so the act opens
        // wide instead of every route beginning in the same slot.
        for (int p = 0; p < paths; p++)
        {
            int col = paths == 1 ? mid : Mathf.RoundToInt((float)p * (width - 1) / (paths - 1));
            col = Mathf.Clamp(col, 0, width - 1);

            EnsureNode(map, slot, 1, col, MapNodeType.Skirmish);

            for (int f = 1; f < topCombatFloor; f++)
            {
                int nextCol = PickNextColumn(rng, carved[f], col, width);
                EnsureNode(map, slot, f + 1, nextCol, MapNodeType.Skirmish);
                Connect(map, slot, f, col, f + 1, nextCol, carved[f]);
                col = nextCol;
            }
        }

        // The hub feeds every opening node, and every top combat node feeds the boss, so the act
        // always has exactly one entrance and one exit.
        foreach (MapNode n in map.NodesOnFloor(1)) Link(start, n);
        foreach (MapNode n in map.NodesOnFloor(topCombatFloor)) Link(n, boss);

        AssignCombatTypes(map, rng, topCombatFloor);

        // ⚠️ BOSSES BEFORE RECHARGE ROOMS, deliberately. Promoting a node to Boss strips any
        // recharge it carries (a boss may not carry one — see MapNode.CanCarryRecharge), so doing
        // this after would silently delete a guaranteed Foundry and leave the map short of one.
        PlaceOptionalBosses(map, rng, s, topCombatFloor);

        AttachRechargeRooms(map, rng, s, topCombatFloor);

        return map;
    }

    /// <summary>
    /// Promotes some combat nodes to optional Boss nodes.
    ///
    /// ⚠️ EVERY PLACEMENT IS TESTED FOR AVOIDABILITY BEFORE IT IS KEPT, and reverted if it fails.
    /// The obvious cheap rule — "only promote on a floor with more than one node" — is not
    /// sufficient: the other nodes on that floor can all funnel back through this one further up,
    /// and then the boss is mandatory while looking optional on the map. RunMap.IsAvoidable runs the
    /// real reachability search, so this asks it rather than guessing.
    ///
    /// ⚠️ IT ALSO NEVER PROMOTES A NODE THAT IS THE ONLY WAY ONWARD FROM ITS PREDECESSOR. That is
    /// avoidable by the global test (the player could have gone a different way three floors back),
    /// but it reads as a trap: you commit to a branch and the boss appears with no way out.
    /// </summary>
    private static void PlaceOptionalBosses(RunMap map, System.Random rng, RunMapSettings s, int topCombatFloor)
    {
        int want = rng.Next(Mathf.Max(0, s.bossesMin), Mathf.Max(0, s.bossesMax) + 1);
        if (want <= 0) return;

        int earliest = Mathf.Max(1, s.bossEarliestFloor);
        int spacing = Mathf.Max(1, s.bossFloorSpacing);

        // Candidates: every combat node from `earliest` up to the top combat floor. The top combat
        // floor is allowed — a boss immediately before the finale is a real and interesting choice.
        List<MapNode> candidates = new List<MapNode>();
        foreach (MapNode n in map.nodes)
            if (n.IsCombat && n.floor >= earliest && n.floor <= topCombatFloor)
                candidates.Add(n);

        // Which floors will host a boss: spaced, ascending, chosen from the eligible set.
        List<int> eligibleFloors = new List<int>();
        for (int f = earliest; f <= topCombatFloor; f++)
            if (map.NodesOnFloor(f).Count > 0) eligibleFloors.Add(f);

        List<int> bossFloors = new List<int>();
        for (int i = eligibleFloors.Count - 1; i > 0; i--)   // shuffle, then take spaced ones
        {
            int j = rng.Next(i + 1);
            int t = eligibleFloors[i]; eligibleFloors[i] = eligibleFloors[j]; eligibleFloors[j] = t;
        }
        // ⚠️ TAKE MORE SPACED FLOORS THAN NEEDED — SPARES, NOT EXACTLY `want`.
        //
        // A chosen floor can turn out to host no legal boss at all: every node on it may be some
        // predecessor's only exit, or promoting any of them would make the boss unavoidable. With
        // exactly `want` floors selected, each such failure silently costs a boss — measured, that
        // left 3.9% of maps with NO optional boss and 19.8% with only one, against a stated range
        // of 2–5. Every floor here is already mutually spaced, so placing on any subset is safe.
        foreach (int f in eligibleFloors)
        {
            if (bossFloors.Count >= want + 3) break;
            bool tooClose = false;
            foreach (int g in bossFloors) if (Mathf.Abs(g - f) < spacing) { tooClose = true; break; }
            if (!tooClose) bossFloors.Add(f);
        }
        bossFloors.Sort();

        // ⚠️ EACH BOSS IS PLACED WITHIN REACH OF THE ONE BELOW IT, AND THAT IS THE WHOLE POINT.
        //
        // Measured before this rule existed: bosses landed in independent random columns, so
        // whether a single route could chain them all was decided by the SEED — only 46% of maps
        // allowed it, and 6% let a boss-hungry player reach just one no matter how well they
        // routed. The designer's brief is the opposite: "they will have to navigate the way pretty
        // good to be able to" — the player's routing should decide, not the roll.
        //
        // A route steps at most one column per floor, so from column `c` at floor `f` the columns
        // in reach at floor `g` are within (g - f). Preferring candidates inside that window makes
        // the full chain a HARD ROUTE rather than a lucky map. It stays a preference, not a
        // requirement: when no reachable column has a legal node the boss still gets placed, which
        // is what keeps maps varied instead of every boss sitting in one drifting line.
        int prevFloor = -1, prevCol = -1;
        int placed = 0;

        foreach (int f in bossFloors)
        {
            List<MapNode> row = map.NodesOnFloor(f);

            // Order this floor's nodes by how reachable they are from the previous boss.
            row.Sort((a, b) =>
            {
                if (prevCol < 0) return rng.Next(3) - 1;                    // first boss: free choice
                int reach = Mathf.Max(1, f - prevFloor);
                int da = Mathf.Max(0, Mathf.Abs(a.column - prevCol) - reach);
                int db = Mathf.Max(0, Mathf.Abs(b.column - prevCol) - reach);
                return da != db ? da.CompareTo(db) : rng.Next(3) - 1;
            });

            foreach (MapNode c in row)
            {
                if (!c.IsCombat) continue;

                // Never the sole exit from any predecessor — see the header.
                bool soleExit = false;
                foreach (int pid in c.prev)
                {
                    MapNode p = map.Get(pid);
                    if (p != null && p.next.Count <= 1) { soleExit = true; break; }
                }
                if (soleExit) continue;

                MapNodeType was = c.type;
                RechargeType hadRecharge = c.recharge;
                c.type = MapNodeType.Boss;
                c.recharge = RechargeType.None;

                if (map.IsAvoidable(c.id))
                {
                    prevFloor = f; prevCol = c.column;
                    placed++;
                    break;                       // one boss per floor
                }

                c.type = was;                    // put it back exactly as it was
                c.recharge = hadRecharge;
            }

            if (placed >= want) break;
        }
    }

    // A route steps at most one column sideways per floor. The candidate is rejected if it would
    // CROSS an edge already carved between the same two floors: crossed lines are unreadable on the
    // map and imply a connection that isn't there.
    private static int PickNextColumn(System.Random rng, List<Edge> carvedHere, int col, int width)
    {
        List<int> candidates = new List<int>();
        for (int d = -1; d <= 1; d++)
        {
            int c = col + d;
            if (c < 0 || c >= width) continue;
            if (Crosses(carvedHere, col, c)) continue;
            candidates.Add(c);
        }

        // Every sideways option crossed something; going straight up never crosses anything.
        if (candidates.Count == 0) return col;
        return candidates[rng.Next(candidates.Count)];
    }

    private static bool Crosses(List<Edge> edges, int from, int to)
    {
        foreach (Edge e in edges)
        {
            if (e.from < from && e.to > to) return true;
            if (e.from > from && e.to < to) return true;
        }
        return false;
    }

    private static MapNode NewNode(RunMap map, int[,] slot, int floor, int col, MapNodeType type)
    {
        MapNode n = new MapNode { id = map.nodes.Count, floor = floor, column = col, type = type };
        map.nodes.Add(n);
        slot[floor, col] = n.id;
        return n;
    }

    private static MapNode EnsureNode(RunMap map, int[,] slot, int floor, int col, MapNodeType type)
    {
        int id = slot[floor, col];
        if (id >= 0) return map.Get(id);
        return NewNode(map, slot, floor, col, type);
    }

    private static void Connect(RunMap map, int[,] slot, int f0, int c0, int f1, int c1, List<Edge> carvedHere)
    {
        MapNode a = map.Get(slot[f0, c0]);
        MapNode b = map.Get(slot[f1, c1]);
        if (a == null || b == null) return;

        if (Link(a, b)) carvedHere.Add(new Edge { from = c0, to = c1 });
    }

    // Returns true only if this edge was new, so callers don't record a duplicate.
    private static bool Link(MapNode a, MapNode b)
    {
        if (a.next.Contains(b.id)) return false;
        a.next.Add(b.id);
        b.prev.Add(a.id);
        return true;
    }

    // Difficulty ramps with depth: the act opens on Skirmishes and Elites only become likely
    // later. Elite weight is zero on the first combat floor by construction (t = 0), so the player
    // is never asked to take an Elite before they have had a chance to build anything.
    private static void AssignCombatTypes(RunMap map, System.Random rng, int topCombatFloor)
    {
        int combatFloors = topCombatFloor;   // floors 1..topCombatFloor inclusive

        for (int f = 1; f <= topCombatFloor; f++)
        {
            float t = combatFloors <= 1 ? 0f : (float)(f - 1) / (combatFloors - 1);

            float wSkirmish = Mathf.Lerp(0.70f, 0.10f, t);
            float wFight = Mathf.Lerp(0.30f, 0.45f, t);
            float wElite = Mathf.Lerp(0.00f, 0.45f, t);

            List<MapNode> row = map.NodesOnFloor(f);
            foreach (MapNode n in row)
                n.type = WeightedType(rng, wSkirmish, wFight, wElite);

            BreakUniformFloor(rng, row);
        }
    }

    // A floor where every branch is the same type offers no decision — it is a toll, not a choice,
    // and on the late floors the weights produce all-Elite rows often. Since difficulty IS the node
    // type here, a uniform row defeats the whole reason the map exists.
    //
    // Nudging ONE node one step EASIER is the right correction rather than re-rolling the row: it
    // guarantees a way through that isn't the hardest option, without flattening the ramp or
    // touching the floors that are legitimately uniform because they only have one node.
    private static void BreakUniformFloor(System.Random rng, List<MapNode> row)
    {
        if (row.Count < 2) return;

        MapNodeType first = row[0].type;
        foreach (MapNode n in row) if (n.type != first) return;

        MapNode victim = row[rng.Next(row.Count)];
        switch (first)
        {
            case MapNodeType.Elite: victim.type = MapNodeType.Fight; break;
            case MapNodeType.Fight: victim.type = MapNodeType.Skirmish; break;
            // An all-Skirmish row is the one case where the outlier goes the other way: there is
            // nothing easier than a Skirmish, so the choice has to be an opt-IN to danger.
            default: victim.type = MapNodeType.Fight; break;
        }
    }

    private static MapNodeType WeightedType(System.Random rng, float wS, float wF, float wE)
    {
        float total = wS + wF + wE;
        if (total <= 0f) return MapNodeType.Skirmish;

        double roll = rng.NextDouble() * total;
        if (roll < wS) return MapNodeType.Skirmish;
        if (roll < wS + wF) return MapNodeType.Fight;
        return MapNodeType.Elite;
    }

    // Recharge rooms hang off Fight and Elite nodes only. Within a floor the generator avoids
    // handing out the same type twice, so a floor offers a CHOICE between different problems being
    // solved rather than the same one on two branches.
    private static void AttachRechargeRooms(RunMap map, System.Random rng, RunMapSettings s, int topCombatFloor)
    {
        // Only types this run can actually spawn — see RunMapSettings.allowedRecharges. With none
        // available the act simply carries no recharge rooms, which is honest, rather than drawing
        // icons that lead nowhere.
        List<RechargeType> all = new List<RechargeType>();
        if (s.allowedRecharges != null)
            foreach (RechargeType t in s.allowedRecharges)
                if (t != RechargeType.None && !all.Contains(t)) all.Add(t);

        if (all.Count == 0) return;

        for (int f = 1; f <= topCombatFloor; f++)
        {
            List<RechargeType> unusedThisFloor = new List<RechargeType>(all);

            foreach (MapNode n in map.NodesOnFloor(f))
            {
                if (!n.CanCarryRecharge) continue;
                if (rng.NextDouble() > s.rechargeChance) continue;

                if (unusedThisFloor.Count == 0) unusedThisFloor.AddRange(all);
                int pick = rng.Next(unusedThisFloor.Count);
                n.recharge = unusedThisFloor[pick];
                unusedThisFloor.RemoveAt(pick);
            }
        }

        if (!s.guaranteeCoreRecharges) return;

        // A run with nowhere to spend gold or repair a card is a dead run, so force those two in if
        // the rolls did not produce them — but only if they're spawnable at all. Deliberately NOT a
        // guarantee that any single route reaches one: choosing whether to detour for it is the
        // decision the map exists to pose.
        if (all.Contains(RechargeType.Foundry)) EnsureExists(map, rng, RechargeType.Foundry);
        if (all.Contains(RechargeType.Market)) EnsureExists(map, rng, RechargeType.Market);
    }

    private static void EnsureExists(RunMap map, System.Random rng, RechargeType want)
    {
        List<MapNode> eligible = new List<MapNode>();

        foreach (MapNode n in map.nodes)
        {
            if (!n.CanCarryRecharge) continue;
            if (n.recharge == want) return;                 // already present, nothing to do
            if (n.recharge == RechargeType.None) eligible.Add(n);
        }

        if (eligible.Count == 0) return;   // no Fight/Elite free to carry it; not worth forcing
        eligible[rng.Next(eligible.Count)].recharge = want;
    }
}
