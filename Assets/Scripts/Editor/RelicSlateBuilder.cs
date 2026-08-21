using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **Deckshift → Build Relic Slate** — creates the Stage 4 relic assets.
///
/// Idempotent: a relic whose asset already exists is left completely alone, so re-running this
/// after hand-tuning a description in the Inspector cannot overwrite the designer's edit.
///
/// ⚠️ ICONS ARE CHOSEN FIRST AND THE NAME BENDS TO FIT, not the other way round (designer,
/// 2026-08-21: "check the icon pack first, see if there are stuff that match the relics identity,
/// and use them. if there are none, you may consider changing the relics name to fit better with
/// something from the icon pack"). Two relics were renamed for exactly this reason:
///   Steel Toe -> Sharp Practice   (every boot icon was already taken; Iron Sword fits +2 damage)
///   Full Pockets -> Running on Fumes (redesigned anyway, and Empty Bottle is a perfect match)
///
/// ⚠️ NO ICON IS REUSED. All 19 existing relics already claim 17 of the pack's 107 sprites, and a
/// relic sharing another's art is indistinguishable on a 52px HUD socket — which is already true of
/// Gecko Gloves and Pogo Boots, and is a bug not a precedent.
/// </summary>
public static class RelicSlateBuilder
{
    private const string OutDir = "Assets/Relics";

    private struct Def
    {
        public string file, id, name, desc, icon;
        public Rarity rarity;
        public Def(string file, string id, string name, Rarity rarity, string desc, string icon)
        { this.file = file; this.id = id; this.name = name; this.rarity = rarity; this.desc = desc; this.icon = icon; }
    }

    private static readonly Def[] Slate =
    {
        // ---------------- COMMON — a small permanent number ----------------
        new Def("CoinPurse",      "CoinPurse",      "Coin Purse",       Rarity.Common,
                "Gold piles pay 20% more.", "Copper Coin"),
        new Def("Haggler",        "Haggler",        "Haggler",          Rarity.Common,
                "Everything in a shop costs 15% less.", "Silver Coin"),
        new Def("IronLung",       "IronLung",       "Iron Lung",        Rarity.Common,
                "Stagger's price climbs by 6 instead of 8.", "Bone"),
        new Def("Magpie",         "Magpie",         "Magpie",           Rarity.Common,
                "Enemies drop one extra scrap.", "Copper Nugget"),
        new Def("SecondWind",     "SecondWind",     "Second Wind",      Rarity.Common,
                "Heal 8 health when you enter a room.", "Red Potion"),
        new Def("SharpPractice",  "SharpPractice",  "Sharp Practice",   Rarity.Common,
                "All your damage +2.", "Iron Sword"),
        new Def("SecondNature",   "SecondNature",   "Second Nature",    Rarity.Common,
                "The first Recall in each room is free.", "Book"),

        // ---------------- RARE — a passive that changes how you move or trade ----------------
        new Def("AirBrake",       "AirBrake",       "Air Brake",        Rarity.Rare,
                "You fall 1.5x slower.", "Wool"),
        new Def("BounceHouse",    "BounceHouse",    "Bounce House",     Rarity.Rare,
                "Enemies you kill leave a bounce pad for 5 seconds.", "Slime Gel"),
        new Def("Crowbar",        "Crowbar",        "Crowbar",          Rarity.Rare,
                "Walk into a breakable wall to break it.", "Pickaxe"),
        new Def("GhostStep",      "GhostStep",      "Ghost Step",       Rarity.Rare,
                "Three jumps each room cost no Shift.", "Arrow"),
        new Def("MatchedSet",     "MatchedSet",     "Matched Set",      Rarity.Rare,
                "Each pair of relics sharing a rarity grants +8 max health and +2 damage.", "Cut Emerald"),
        new Def("Pawnbroker",     "Pawnbroker",     "Pawnbroker",       Rarity.Rare,
                "Relics sell for double.", "Silver Ingot"),
        new Def("QuickHands",     "QuickHands",     "Quick Hands",      Rarity.Rare,
                "Killing an enemy draws a card.", "Scroll"),

        // ---------------- EPIC — a rule change ----------------
        new Def("AceUpTheSleeve", "AceUpTheSleeve", "Ace Up the Sleeve", Rarity.Epic,
                "Once per run, hitting 0 Shift grants 20 Shift instead of dealing Stagger.", "Crystal"),
        new Def("BlankCheque",    "BlankCheque",    "Blank Cheque",     Rarity.Epic,
                "One free item at every shop you visit.", "Silver Key"),
        new Def("DebtCollector",  "DebtCollector",  "Debt Collector",   Rarity.Epic,
                "Stagger bills gold instead of health - 20 gold per point.", "Envolop"),
        new Def("EstateSale",     "EstateSale",     "Estate Sale",      Rarity.Epic,
                "Sell five relics in one run to gain a sixth slot, permanently.", "Golden Key"),
        new Def("Flywheel",       "Flywheel",       "Flywheel",         Rarity.Epic,
                "Recall deals damage to nearby enemies equal to 10x its Shift cost.", "Coal"),
        new Def("LongFuse",       "LongFuse",       "Long Fuse",        Rarity.Epic,
                "Exhausted cards return to your draw pile with one charge. Your hand is 1 smaller.", "String"),
        new Def("NestEgg",        "NestEgg",        "Nest Egg",         Rarity.Epic,
                "Finish a room having spent 5 Shift or less: +2 max Shift, permanently.", "Egg"),
        new Def("OddSocket",      "OddSocket",      "Odd Socket",       Rarity.Epic,
                "For each empty relic slot, you deal 15% more damage and take 15% less.", "Rune Stone"),
        new Def("PaperSkin",      "PaperSkin",      "Paper Skin",       Rarity.Epic,
                "Every card gets +1 charge. You take 50% more damage.", "Paper"),
        new Def("RunningOnFumes", "RunningOnFumes", "Running on Fumes", Rarity.Epic,
                "+1 damage for every 2 Shift you are missing.", "Empty Bottle"),
        new Def("TunnelVision",   "TunnelVision",   "Tunnel Vision",    Rarity.Epic,
                "Your hand is one card. Recall costs nothing and never escalates.", "Lantern"),
        new Def("Understudy",     "Understudy",     "Understudy",       Rarity.Epic,
                "Copies the relic in the slot to its left.", "Book 2"),
        new Def("WeightClass",    "WeightClass",    "Weight Class",     Rarity.Epic,
                "You fall faster and hit 40% harder. You jump 25% lower.", "Obsidian"),

        // ---------------- BOSS — a new verb, with a key to press ----------------
        new Def("DeadDrop",       "DeadDrop",       "Dead Drop",        Rarity.Boss,
                "Press down in mid-air to slam straight down.", "Hammer"),
        new Def("Grapnel",        "Grapnel",        "Grapnel",          Rarity.Boss,
                "Fire a hook at a surface and pull yourself to it. Costs 2 Shift.", "Rope"),
        new Def("Stopgap",        "Stopgap",        "Stopgap",          Rarity.Boss,
                "Freeze every enemy and projectile for 1.5 seconds. Once per room.", "Monster Eye"),
    };

    [MenuItem("Deckshift/Build Relic Slate")]
    public static void Build()
    {
        // Resolve icons by FILE NAME, not by path — the pack sorts its 107 sprites into themed
        // subfolders (Material/, Monster Part/, ...) and a hardcoded path breaks on a reimport.
        var icons = new Dictionary<string, Sprite>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Cainos/Pixel Art Icon Pack - RPG" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) icons[System.IO.Path.GetFileNameWithoutExtension(path)] = s;
        }

        int made = 0, skipped = 0;
        var missing = new List<string>();

        foreach (Def d in Slate)
        {
            string path = OutDir + "/" + d.file + ".asset";
            if (AssetDatabase.LoadAssetAtPath<RelicData>(path) != null) { skipped++; continue; }

            RelicData r = ScriptableObject.CreateInstance<RelicData>();
            r.relicID = d.id;
            r.relicName = d.name;
            r.description = d.desc;
            r.rarity = d.rarity;

            Sprite icon;
            if (icons.TryGetValue(d.icon, out icon)) r.relicArt = icon;
            else missing.Add(d.name + " wanted '" + d.icon + "'");

            AssetDatabase.CreateAsset(r, path);
            made++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        // The catalogue's AssetPostprocessor already fires on these imports; this is belt-and-braces
        // so the roster is certainly current before anything reads RelicPool.
        RelicCatalogueBuilder.RebuildMenu();

        var sb = new System.Text.StringBuilder();
        sb.Append("[RelicSlate] created ").Append(made).Append(", already present ").Append(skipped);
        if (missing.Count > 0)
        {
            sb.Append("\nICON NOT FOUND (relic created with an empty art slot, which renders as a bare socket):");
            foreach (string m in missing) sb.Append("\n  ").Append(m);
        }
        Debug.Log(sb.ToString());
    }
}
