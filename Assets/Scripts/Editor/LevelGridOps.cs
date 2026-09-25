using System;
using System.Collections.Generic;

// Grid transforms shared by LevelTextImporter and LevelValidator.
//
// ⚠️ ANYTHING THAT CHANGES THE ROOM'S SHAPE LIVES HERE AND BOTH TOOLS CALL IT. If the importer
// builds one shape and the validator checks another, the validator is vouching for a room that does
// not exist. That is the one way this file can go wrong, so keep every geometry edit in it.
public static class LevelGridOps
{
    // ---- stepped ceiling corners ("the rooms feel blocky", designer 2026-09-25) ----------------
    //
    // The hand-made rooms cut their upper corners into small staircases (EfeVrl5's top chamber
    // has one in every corner); every generated room met ceiling and wall at a perfect right
    // angle, which is a large part of why they read as drawn on graph paper. This fills 1-3 air
    // cells in each qualifying UPPER inner corner to make that step.
    //
    // UPPER CORNERS ONLY, on purpose. A step in a FLOOR corner is a 1-tile ledge the player has
    // to jump over, and changing where the player can walk is a design decision, not a cosmetic
    // one. A ceiling corner is dead space: the rules below keep it at least MinAirBelow cells above
    // anything, so no jump that worked before can hit it.
    //
    // A corner qualifies only when:
    //   - both arms are long (ceiling and wall each run on for ArmLength cells), so the step
    //     softens a real corner rather than chewing a notch out of a short ledge;
    //   - every filled cell has MinAirBelow clear cells beneath it;
    //   - there is no marker within 2 cells (never crowd a door, a chest or an enemy).
    // Deterministic per room (seeded), so the importer and the validator fill the same cells.
    private const int ArmLength = 4;
    private const int MinAirBelow = 6;

    public static int ChamferCeilingCorners(char[][] g, int seed)
    {
        int h = g.Length;
        if (h == 0) return 0;
        int w = 0;
        foreach (var row in g) w = Math.Max(w, row.Length);

        bool Solid(int c, int r) => c < 0 || r < 0 || c >= w || r >= h || c >= g[r].Length || g[r][c] == '#';
        bool Empty(int c, int r) => c >= 0 && r >= 0 && c < w && r < h && c < g[r].Length && (g[r][c] == '.' || g[r][c] == ' ');

        bool NoMarkersNear(int c, int r)
        {
            for (int rr = r - 2; rr <= r + 2; rr++)
                for (int cc = c - 2; cc <= c + 2; cc++)
                {
                    if (cc < 0 || rr < 0 || cc >= w || rr >= h || cc >= g[rr].Length) continue;
                    char ch = g[rr][cc];
                    if (ch != '.' && ch != ' ' && ch != '#') return false;
                }
            return true;
        }

        bool AirBelow(int c, int r)
        {
            for (int k = 1; k <= MinAirBelow; k++)
                if (!Empty(c, r + k)) return false;
            return true;
        }

        // Collect first, fill after: filling while scanning would let one step create the
        // "corner" the next cell reads, and chamfers would crawl along the ceiling.
        var fills = new List<(int c, int r)>();
        for (int r = 1; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                if (!Empty(c, r) || !Solid(c, r - 1)) continue;

                for (int side = -1; side <= 1; side += 2)       // -1: wall on the left, +1: on the right
                {
                    if (!Solid(c + side, r)) continue;

                    int run = -side;                            // the direction the ceiling runs away from the wall
                    bool arms = true;
                    for (int k = 0; k < ArmLength && arms; k++)
                    {
                        if (!Solid(c + run * k, r - 1) || !Empty(c + run * k, r)) arms = false;   // ceiling arm
                        if (!Solid(c + side, r + k) || !Empty(c, r + k)) arms = false;           // wall arm
                    }
                    if (!arms) continue;

                    int hash = Math.Abs((c * 73856093) ^ (r * 19349663) ^ seed);
                    int size = 1 + hash % 3;                   // 1, 2 or 3 cells: varied, never uniform
                    var cells = new List<(int, int)> { (c, r) };
                    if (size >= 2) { cells.Add((c + run, r)); cells.Add((c, r + 1)); }
                    if (size >= 3) { cells.Add((c + run * 2, r)); }

                    bool ok = true;
                    foreach (var (cc, rr) in cells)
                        if (!Empty(cc, rr) || !AirBelow(cc, rr) || !NoMarkersNear(cc, rr)) { ok = false; break; }
                    if (ok) fills.AddRange(cells);
                }
            }

        int n = 0;
        foreach (var (c, r) in fills)
        {
            if (!Empty(c, r)) continue;
            g[r][c] = '#';
            n++;
        }
        return n;
    }

    // Reads a directive the way the importer does: absent = the default, "off"/"false" = off.
    public static bool DirectiveOn(Dictionary<string, string> directives, string key, bool defaultOn)
    {
        if (!directives.TryGetValue(key, out string v)) return defaultOn;
        v = v.Trim().ToLowerInvariant();
        if (v == "off" || v == "false" || v == "no") return false;
        if (v == "on" || v == "true" || v == "yes") return true;
        return defaultOn;
    }

    // Stable string hash (string.GetHashCode is not guaranteed stable across runs).
    public static int StableHash(string s)
    {
        unchecked
        {
            int hsh = (int)2166136261;
            foreach (char ch in s) hsh = (hsh ^ ch) * 16777619;
            return hsh & 0x7fffffff;
        }
    }
}
