using System.Collections.Generic;

// The run map: a Slay-the-Spire style branching graph for one act.
//
// DESIGN (settled 2026-08-03, built 2026-08-06 — see CLAUDE.md "Run map system"):
//
//   · The WHOLE act is visible at once, opened with M, so the player plans a ROUTE rather than
//     picking one door at a time. It is viewable in the hub too, for quest planning.
//
//   · DIFFICULTY IS THE NODE TYPE, not a second axis layered on top of it. Three combat nodes —
//     Skirmish / Fight / Elite — of ascending cost and reward. Layering easy/medium/hard ONTO
//     Fight/Shop/Event would give ~15 icon combinations and an unreadable map. One node, one icon,
//     one promise.
//
//   · RECHARGE ROOMS ARE NOT FLOORS. They hang off a route as an ATTACHMENT to a combat node
//     (MapNode.recharge), so travelling to that node means clearing the fight and then getting the
//     recharge room. Modelling them as their own nodes would make them floors, which is exactly
//     what the design says they must not be.
//
//   · A recharge room can ONLY hang off a Fight or an Elite, NEVER a Skirmish. That restriction is
//     the entire economy: Skirmish routes are cheap to survive but never resupply, Elite routes
//     cost you but are the only path to recharge. Enforced in RunMapGenerator, and re-asserted by
//     RunMap.Validate() so it can't rot.
//
//   · Each recharge room is SPECIALISED, never a do-everything room — one room that fixes every
//     problem is never a decision. The player must be able to see WHICH one is on which branch
//     before committing, so the type is part of the map data, not rolled on arrival.
//
// This file is pure data and pure queries: no Unity types beyond none at all, so the generator can
// be exercised and printed in a test without a scene. Generation lives in RunMapGenerator, run
// state and persistence in RunMapManager.

public enum MapNodeType
{
    Start,      // the hub — always floor 0, always exactly one
    Skirmish,   // simple layouts, low-HP enemies, thin loot, no NPCs at all
    Fight,      // harder layout, mid-tier enemies, at least one chest
    Elite,      // hardest layouts, buffed and shift-infused enemies

    // ⚠️ BOSS IS NO LONGER THE FINALE, AND ACTS NO LONGER EXIST (designer, 2026-08-21).
    //
    // A Boss is an OPTIONAL mid-map node standing in a column like any other. Route into it for a
    // boss relic and a heavy payout; route around it and keep your resources. A run can take as few
    // as none of them or as many as the map offers, and that choice is the strategy the map exists
    // for. Every Boss node is REQUIRED to be avoidable — see RunMap.IsAvoidable, enforced in
    // Validate — or "choose whether to fight it" would be a lie.
    Boss,

    // ⚠️ AND A BOSS REPLACES A FLOOR, IT DOES NOT ADD ONE (designer's call). The map is always the
    // same depth, so a five-boss route and a one-boss route take the same TIME and differ in
    // intensity and reward. That is what keeps a 45–50 minute run from swinging to 75.
    FinalBoss,  // the run's terminus — always the top floor, always exactly one, never optional
}

// What a recharge room attached to a node actually solves for the player. Never combine these:
// the specialisation IS the decision.
public enum RechargeType
{
    None,
    Foundry,   // scrap: repair and salvage cards, Blompo
    Market,    // gold: the shop
    Well,      // Shift and healing
}

public class MapNode
{
    public int id;
    public int floor;     // 0 = Start, floors-1 = Boss
    public int column;    // horizontal slot, for layout and for path carving
    public MapNodeType type;
    public RechargeType recharge = RechargeType.None;

    // Graph edges, by node id. `next` always points at floor+1.
    public List<int> next = new List<int>();
    public List<int> prev = new List<int>();

    public bool IsCombat => type == MapNodeType.Skirmish || type == MapNodeType.Fight || type == MapNodeType.Elite;

    /// <summary>Either kind of boss — for anything that just needs "is this a boss fight".</summary>
    public bool IsBoss => type == MapNodeType.Boss || type == MapNodeType.FinalBoss;

    // Only Fight and Elite may carry a recharge room. See the header — this is the economy, not a
    // balance tweak.
    //
    // ⚠️ A BOSS DELIBERATELY CANNOT. A boss node already pays out heavily (its relic, its loot), and
    // hanging a Well off it would mean the hardest route also refills you — which would delete the
    // attrition that makes routing into bosses a decision at all.
    public bool CanCarryRecharge => type == MapNodeType.Fight || type == MapNodeType.Elite;

    public override string ToString()
    {
        string r = recharge == RechargeType.None ? "" : "+" + recharge;
        return $"#{id} f{floor}c{column} {type}{r}";
    }
}

public class RunMap
{
    public List<MapNode> nodes = new List<MapNode>();
    public int floors;
    public int seed;

    // -1 until the run starts. The Start node is entered explicitly rather than assumed, so the
    // "where am I" question has exactly one answer at all times.
    public int currentNodeId = -1;

    public readonly HashSet<int> visited = new HashSet<int>();

    public MapNode Get(int id)
    {
        // Ids are assigned as list indices at build time, so this is a direct hit in the normal
        // case; the scan is a guard for maps built any other way.
        if (id >= 0 && id < nodes.Count && nodes[id].id == id) return nodes[id];
        foreach (MapNode n in nodes) if (n.id == id) return n;
        return null;
    }

    public MapNode Current => Get(currentNodeId);

    public List<MapNode> NodesOnFloor(int floor)
    {
        List<MapNode> result = new List<MapNode>();
        foreach (MapNode n in nodes) if (n.floor == floor) result.Add(n);
        result.Sort((a, b) => a.column.CompareTo(b.column));
        return result;
    }

    public MapNode StartNode
    {
        get { List<MapNode> f = NodesOnFloor(0); return f.Count > 0 ? f[0] : null; }
    }

    public MapNode FinalBossNode
    {
        get { List<MapNode> f = NodesOnFloor(floors - 1); return f.Count > 0 ? f[0] : null; }
    }

    /// <summary>Every OPTIONAL boss on the map, in floor order. The final boss is not one of them.</summary>
    public List<MapNode> BossNodes
    {
        get
        {
            List<MapNode> result = new List<MapNode>();
            foreach (MapNode n in nodes) if (n.type == MapNodeType.Boss) result.Add(n);
            result.Sort((a, b) => a.floor != b.floor ? a.floor.CompareTo(b.floor) : a.column.CompareTo(b.column));
            return result;
        }
    }

    /// <summary>How many optional bosses this run has actually fought so far.</summary>
    public int BossesDefeated
    {
        get
        {
            int n = 0;
            foreach (int id in visited)
            {
                MapNode m = Get(id);
                if (m != null && m.type == MapNodeType.Boss) n++;
            }
            return n;
        }
    }

    /// <summary>
    /// Can the run still be finished WITHOUT passing through this node? Exact rather than
    /// heuristic: a breadth-first search from Start to the top floor with the node removed.
    ///
    /// ⚠️ THIS IS WHAT MAKES AN OPTIONAL BOSS OPTIONAL. "Route around it" has to be true of every
    /// single boss placement, and a cheap proxy — "its floor has more than one node" — is not the
    /// same thing: the alternatives on that floor may all feed through it later, or be unreachable
    /// from where the player actually is. Validate() runs this on every Boss node.
    /// </summary>
    public bool IsAvoidable(int nodeId)
    {
        MapNode target = Get(nodeId);
        if (target == null) return true;

        MapNode start = StartNode;
        MapNode finish = FinalBossNode;
        if (start == null || finish == null) return false;
        if (target == start || target == finish) return false;   // neither is ever optional

        var seen = new HashSet<int> { start.id };
        var queue = new Queue<int>();
        queue.Enqueue(start.id);

        while (queue.Count > 0)
        {
            MapNode cur = Get(queue.Dequeue());
            if (cur == null) continue;
            if (cur.id == finish.id) return true;

            foreach (int id in cur.next)
            {
                if (id == nodeId) continue;          // the removal
                if (!seen.Add(id)) continue;
                queue.Enqueue(id);
            }
        }
        return false;
    }

    // The nodes the player may pick RIGHT NOW. Before the run starts that is the Start node alone,
    // so the caller never needs a special case for "beginning of the act".
    public List<MapNode> AvailableNext()
    {
        List<MapNode> result = new List<MapNode>();

        if (currentNodeId < 0)
        {
            MapNode s = StartNode;
            if (s != null) result.Add(s);
            return result;
        }

        MapNode cur = Current;
        if (cur == null) return result;

        foreach (int id in cur.next)
        {
            MapNode n = Get(id);
            if (n != null) result.Add(n);
        }
        result.Sort((a, b) => a.column.CompareTo(b.column));
        return result;
    }

    public bool CanTravelTo(int id)
    {
        foreach (MapNode n in AvailableNext()) if (n.id == id) return true;
        return false;
    }

    // Returns false rather than throwing on an illegal move, so a mis-wired UI button can't corrupt
    // the run's position.
    public bool TravelTo(int id)
    {
        if (!CanTravelTo(id)) return false;
        currentNodeId = id;
        visited.Add(id);
        return true;
    }

    // ⚠️ ONLY the FinalBoss ends a run. Reaching an optional Boss mid-map used to end it, because
    // Boss meant "the act finale" — that assumption is exactly what the act system baked in.
    public bool IsFinished => Current != null && Current.type == MapNodeType.FinalBoss;

    // Structural self-check. Cheap, and it turns a silently broken generator into a named failure —
    // an unreachable node means a run that cannot be completed, which is the worst possible bug to
    // find at playtest instead of at build time.
    public bool Validate(out string error)
    {
        error = null;

        if (nodes.Count == 0) { error = "map has no nodes"; return false; }
        if (floors < 3) { error = $"map needs at least 3 floors (start, one combat, boss); has {floors}"; return false; }

        if (NodesOnFloor(0).Count != 1) { error = $"floor 0 must hold exactly one Start node, has {NodesOnFloor(0).Count}"; return false; }
        if (NodesOnFloor(floors - 1).Count != 1) { error = $"top floor must hold exactly one FinalBoss node, has {NodesOnFloor(floors - 1).Count}"; return false; }

        MapNode top = FinalBossNode;
        if (top == null || top.type != MapNodeType.FinalBoss)
            { error = "the top floor's node is not a FinalBoss"; return false; }

        foreach (MapNode n in nodes)
        {
            if (n.recharge != RechargeType.None && !n.CanCarryRecharge)
                { error = $"{n} carries a recharge room but is not a Fight or Elite"; return false; }

            // ⚠️ EVERY OPTIONAL BOSS MUST BE ROUTABLE AROUND. This is the one rule that makes the
            // whole system a choice instead of a difficulty spike, so it is checked structurally
            // rather than trusted to the generator's placement rules.
            if (n.type == MapNodeType.Boss && !IsAvoidable(n.id))
                { error = $"{n} is an OPTIONAL boss but every route to the end passes through it"; return false; }

            if (n.type == MapNodeType.FinalBoss && n.floor != floors - 1)
                { error = $"{n} is a FinalBoss but is not on the top floor"; return false; }

            // Every node must be both reachable and able to reach onward, or a route dead-ends.
            if (n.floor > 0 && n.prev.Count == 0) { error = $"{n} is unreachable (no incoming edges)"; return false; }
            if (n.floor < floors - 1 && n.next.Count == 0) { error = $"{n} is a dead end (no outgoing edges)"; return false; }

            foreach (int id in n.next)
            {
                MapNode m = Get(id);
                if (m == null) { error = $"{n} points at missing node {id}"; return false; }
                if (m.floor != n.floor + 1) { error = $"{n} -> {m} skips or reverses a floor"; return false; }
                if (!m.prev.Contains(n.id)) { error = $"{n} -> {m} edge is not mirrored in prev"; return false; }
            }
        }

        return true;
    }

    // An ASCII rendering of the graph, for tests and for the console. Being able to eyeball a
    // generated act in one line of output is how the generator gets tuned without any UI.
    //
    // ⚠️ The edge rows show DIRECTION FROM THE SOURCE COLUMN (\ = goes left, | = straight,
    // / = goes right), not lines drawn to scale. A node that fans out several columns — the hub
    // does exactly this — renders as a single "\|/" over the source, NOT as long diagonals reaching
    // each target. Read the edge row as "what leaves this node", and use Validate() or the raw
    // next/prev lists if you need to confirm a specific connection.
    public string ToAscii()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        int maxCol = 0;
        foreach (MapNode n in nodes) if (n.column > maxCol) maxCol = n.column;

        for (int f = floors - 1; f >= 0; f--)
        {
            List<MapNode> row = NodesOnFloor(f);

            // The node row.
            sb.Append($"f{f,-2} ");
            for (int c = 0; c <= maxCol; c++)
            {
                MapNode at = null;
                foreach (MapNode n in row) if (n.column == c) { at = n; break; }
                sb.Append(at == null ? "    " : $" {Glyph(at)}{RechargeGlyph(at)} ");
            }
            sb.AppendLine();

            // The edge row, drawn between this floor and the one below it.
            if (f > 0)
            {
                sb.Append("    ");
                for (int c = 0; c <= maxCol; c++)
                {
                    string cell = "    ";
                    foreach (MapNode below in NodesOnFloor(f - 1))
                    {
                        if (below.column != c) continue;
                        bool l = false, s = false, r = false;
                        foreach (int id in below.next)
                        {
                            MapNode up = Get(id);
                            if (up == null) continue;
                            if (up.column < c) l = true;
                            else if (up.column > c) r = true;
                            else s = true;
                        }
                        cell = $"{(l ? "\\" : " ")}{(s ? "|" : " ")}{(r ? "/" : " ")} ";
                    }
                    sb.Append(cell);
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("    S=Skirmish  F=Fight  E=Elite  B=Boss  H=hub/start");
        sb.AppendLine("    attached recharge: f=Foundry  m=Market  w=Well");
        return sb.ToString();
    }

    private static string Glyph(MapNode n)
    {
        switch (n.type)
        {
            case MapNodeType.Start: return "H";
            case MapNodeType.Skirmish: return "S";
            case MapNodeType.Fight: return "F";
            case MapNodeType.Elite: return "E";
            case MapNodeType.Boss: return "B";
            case MapNodeType.FinalBoss: return "X";
            default: return "?";
        }
    }

    private static string RechargeGlyph(MapNode n)
    {
        switch (n.recharge)
        {
            case RechargeType.Foundry: return "f";
            case RechargeType.Market: return "m";
            case RechargeType.Well: return "w";
            default: return " ";
        }
    }
}
