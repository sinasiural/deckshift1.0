using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Tilemaps;

// Deckshift level-from-text importer.
//
// Reads an ASCII grid (.txt) and builds a room prefab that satisfies the room
// contract LevelManager expects:
//   - a child named exactly "CameraBounds" with a BoxCollider2D zone
//   - a child named exactly "GirisNoktasi" (player spawn)
//   - an ExitDoor instance somewhere in the room
//
// Visual language learned from EfeVrl7 (tile-by-tile audit, 2026-07-13):
//   - a full-room BACKDROP of "TX Tileable - Dungeon Wall" tiles (BGPalette base)
//   - a room FRAME painted with edge tiles: _153/_154 top, _156/_157 left,
//     _188/_189 right, _144 floor surface over _186/_185 fill, _96 ceiling face
//   - free-standing interior platforms use chunky "Ground Dirt" block tiles
// The importer reproduces that: a "BackWall" tilemap (no collider, order 0)
// plus a "Ground" tilemap (layer 3, TilemapCollider2D, order 1, at z=1).
//
// Marker legend and example: Assets/LevelTexts/TestRoom1.txt
public static class LevelTextImporter
{
    private const string OutputFolder = "Assets/LevelGenerated";
    private const string InputFolder = "Assets/LevelTexts";

    // Summary of the most recent Build(), shown by the interactive menu path only.
    private static string lastReport = "";

    // Level geometry layer index (matches PlayerController.groundLayer mask, see CLAUDE.md).
    private const int GroundLayer = 3;
    private const int GroundSortingOrder = 1;   // hand-built rooms use 1 for ground
    private const int BackWallSortingOrder = 0; // backdrop renders behind ground
    private const float GroundZ = 1f;           // hand-built rooms keep the tile grid at z=1

    private const string SpawnPrefabPath = "Assets/Prefabs/GirisNoktasi.prefab";
    private const string CameraBoundsPrefabPath = "Assets/Prefabs/CameraBounds.prefab";

    // ASCII marker -> prefab asset path. '.', ' ' = empty, '#' = ground tile,
    // 'S' = spawn (handled separately so we can enforce exactly one).
    private static readonly Dictionary<char, string> MarkerPrefabs = new Dictionary<char, string>
    {
        { 'X', "Assets/Prefabs/ExitDoor.prefab" },
        { 'm', "Assets/YeniLeveller/MeleeEnemy.prefab" },
        { 'r', "Assets/YeniLeveller/RangedEnemy.prefab" },
        { 'l', "Assets/YeniLeveller/SlimeEnemy.prefab" },
        { 'M', "Assets/YeniLeveller/Mimic.prefab" },
        // Zombie early-enemy tiers (Cainos rig + game AI). See CardAnchors.md §6.
        // 'z' Shambler: fodder (12 HP, one-shot), melee contact. Built from PF Zombie - A.
        // 'Z' Rotbrute: grunt (25 HP), bigger/slower, harder contact hit. PF Zombie - B skin.
        // 's' Spitter: weak ranged (18 HP), lobs a projectile via ZombieSpitterAI. PF Zombie - C skin.
        { 'z', "Assets/YeniLeveller/Shambler.prefab" },
        { 'Z', "Assets/YeniLeveller/Rotbrute.prefab" },
        { 's', "Assets/YeniLeveller/Spitter.prefab" },
        // NOTE: the flying bat is BatMan.prefab (has AeroBatAI). Assets/Prefabs/AeroBat.prefab
        // is a legacy husk without AI — do not point markers at it.
        { 'b', "Assets/YeniLeveller/BatMan.prefab" },
        { '^', "Assets/LevelEfeVrl/spikers.prefab" },
        { 'T', "Assets/YeniLeveller/Trapdoor.prefab" },
        { 'W', "Assets/YeniLeveller/BreakableWall_Bookshelf.prefab" },
        { '+', "Assets/Prefabs/ShiftCrystal.prefab" },
        { 'g', "Assets/YeniLeveller/Gold New.prefab" },
        { 'C', "Assets/YeniLeveller/RelicChest.prefab" },   // renamed from Chest.prefab now that card chests exist
        // Card chest — the ONLY source of cards in a level now that the between-rooms reward screen
        // is gone. Counts as loot exactly like 'C' does, so mind the per-tier chest budget.
        { 'D', "Assets/YeniLeveller/CardChest.prefab" },
        // mechanics (added for GenLevel3):
        // Village elevator (designer's pick 2026-08-07) — reads better than the dungeon one.
        { 'E', "Assets/Cainos/Pixel Art Platformer - Village Props/Prefab/PF Village Props - Elevator.prefab" }, // moving platform; tune travel in Inspector
        { 'F', "Assets/Prefabs/UpdraftFan.prefab" },        // updraft zone ~3 tall, liftForce 20 (~5-7 tiles of lift)
        { 'w', "Assets/Prefabs/AcidWater.prefab" },         // acid pool ~6 wide; damages + slows
        { 'K', "Assets/Prefabs/WreckingBall.prefab" },      // swinging hazard; placed at cell center, tune anchor
        { 'c', "Assets/Prefabs/CrumblingPlatform.prefab" }, // platform that crumbles underfoot
        { 't', "Assets/Prefabs/Taret.prefab" },             // stationary turret enemy
        { '$', "Assets/YeniLeveller/Shopkeeper_NPC.prefab" }, // shop NPC (its 'missing' scripts are TMP/UI package scripts — fine)
        { 'B', "Assets/Prefabs/Blompo.prefab" },              // Blompo — card-blessing NPC; a way to SPEND loot, so he counts as loot
        { 'L', "Assets/YeniLeveller/Lever.prefab" },          // lever; importer wires it to the NEAREST gate (On=Open, Off=Close)
        // Recharge-room furniture (2026-09-14). Each recharge room is SPECIALISED — one problem per
        // room — so these normally appear only in their own room: 'f'+'B' in the Foundry, '$'+'Q'
        // in the Market, 'H' in the Well. Use "!recharge: on" in the same file (see below).
        { 'f', "Assets/Prefabs/ScrapForge.prefab" },          // Scrap Forge — repair / salvage cards for scrap
        { 'Q', "Assets/YeniLeveller/QuestBoardPrefab.prefab" }, // Quest board — take a contract mid-run
        { 'H', "Assets/Prefabs/RestWell.prefab" },            // Rest Well — heal + Shift, once per visit
    };

    // 'A' Shift Altar. It is NOT in MarkerPrefabs because it needs post-processing the generic path
    // doesn't do — it gets collected and wired to the nearest gate. It IS a real prefab though, so
    // its look and collider live in one place instead of being re-declared here.
    private const string AltarPrefabPath = "Assets/YeniLeveller/ShiftAltar.prefab";

    // Non-prefab structural markers, built procedurally:
    //   '=' one-way platform tiles (jump up through, land on top)
    //   'G' gate cell — vertical runs of G become one Gate: a stone arch with double doors in it
    private const string PropsTexturePath = "Assets/Cainos/Pixel Art Platformer - Dungeon/Texture/TX Dungeon Props.png";
    // ⚠️ This sprite is an ARCH WITH DOUBLE DOORS IN IT, not a portcullis. The importer only lays
    // down the whole sprite; Gate.cs swaps it at runtime for the arch/leaf pieces cut by
    // Editor/GateArtBaker and opens the leaves in place. If this constant is ever changed, re-run
    // Deckshift → Bake Gate Art, which reads the same name.
    private const string GateSpriteName = "TX Dungeon Props - Gate 01";
    // (AltarSpriteName removed 2026-08-09 — the altar's sprite now lives on ShiftAltar.prefab.)
    private const int InteractableLayer = 12; // "Interactable" (PlayerController.interactableLayer)

    // Markers that stand ON the ground: after spawning, the instance is shifted
    // so the bottom of its measured visual bounds sits exactly on the cell floor.
    // (Enemies here don't fall — most have kinematic physics — so placement must
    // be exact.)
    //
    // ⚠️ GOLD ('g') IS GROUNDED, SHIFT CRYSTALS ('+') ARE NOT — designer 2026-08-13. A pile of coins
    // hovering at a cell centre reads as a bug; a Shift crystal hovering reads as magic. This is a
    // per-object judgement about what the thing IS, not a technical distinction, so it lives here
    // rather than being derived from anything.
    //
    // Flyers ('b') stay at cell centre. 'E' (elevator) and 'K' (wrecking ball) are NOT grounded
    // either: the elevator's rest position and the ball's hang point are tuned by hand.
    private static readonly HashSet<char> GroundedMarkers = new HashSet<char>
    {
        'X', 'm', 'r', 'l', 'M', 'z', 'Z', 's', 'C', 'D', 'g', '^', 'W', 'T', 'F', 'w', 'c', 't', '$', 'B', 'L',
        'f', 'Q', 'H',
    };

    // ---- Tile roles ---------------------------------------------------------------------------
    //
    // MEASURED, NOT GUESSED (2026-08-07). Every set below is the real frequency distribution from
    // the six hand-made rooms — efeslevel2/3, EfeVrl4/5/6/7 — counted by classifying each painted
    // cell by its neighbours. Entries are REPEATED to weight them, so the deterministic picker
    // reproduces roughly the same mix the designer paints by hand.
    //
    // WHY THIS WAS REWRITTEN. The previous table was a guess and it was wrong in three ways that
    // together made generated rooms read as flat grey wallpaper:
    //
    //   1. ⚠️ PLATFORMS WERE PAINTED WITH CEILING TILES. Extra_112/113/114 were used as the
    //      free-standing platform strip set. In the hand-made rooms those three tiles appear 18
    //      times each and are CEILING in 100% of cases — never once a platform. Every floating
    //      ledge in a generated room was wearing ceiling trim, complete with the toothy underside.
    //
    //   2. ⚠️ PLATFORMS COME FROM A DIFFERENT PALETTE ENTIRELY. The designer paints mid-air
    //      platforms from the Cainos "TP Dungeon Ground" palette (Dirt_* and Ground_*), NOT from
    //      the biseyler "Ground Extra_*" sheet that the masses use. Note Ground_* exists ONLY in
    //      the Cainos folder. This is the single biggest visual difference.
    //
    //   3. ⚠️ LEFT AND RIGHT WALL FACES WERE INVERTED. Extra_156 was used for "air to the right";
    //      it is the designer's most common tile for "air to the LEFT" (57 uses).
    //
    // Also: the hand-made rooms use 46-58 DISTINCT ground tiles each. Picking one fixed tile per
    // role is most of why generated rooms look like wallpaper, hence weighted sets everywhere.
    private const string TileDir = "Assets/LevelSinasi/biseyler/";
    private const string CainosDir = "Assets/Cainos/Pixel Art Platformer - Dungeon/Tileset Pallete/TP Dungeon Ground/";

    // Darkened copies of the interior tiles, used for rock more than 2 cells from air.
    // Generated by Deckshift -> Generate Tile Variants; see the note at the call site.
    // Indexed by recession step: [0] = depth 3, [1] = depth 4, [2] = depth 5+.
    private static readonly string[][] DeepFillTiles =
    {
        new[] { "Ground Extra_153 Deep1", "Ground Extra_185 Deep1", "Ground Extra_101 Deep1", "Ground Extra_49 Deep1" },
        new[] { "Ground Extra_153 Deep2", "Ground Extra_185 Deep2", "Ground Extra_101 Deep2", "Ground Extra_49 Deep2" },
        new[] { "Ground Extra_153 Deep3", "Ground Extra_185 Deep3", "Ground Extra_101 Deep3", "Ground Extra_49 Deep3" },
    };

    // Every tile name the importer can paint. TileVariantGenerator uses this to build a
    // Grid-collision copy of each — see the note on Resolve.
    public static IEnumerable<string> AllPaintedTileNames()
    {
        var seen = new HashSet<string>();
        foreach (var kv in MaskTiles) if (seen.Add(kv.Value)) yield return kv.Value;
        foreach (string n in Mask4Tiles) if (seen.Add(n)) yield return n;
        foreach (string p in PlatformSingleTiles) { string n = ShortNameOf(p); if (seen.Add(n)) yield return n; }
    }

    // "…/TX Tileset - Dungeon Ground Dirt_0.asset" -> "Ground Dirt_0"
    private static string ShortNameOf(string assetPath) =>
        Path.GetFileNameWithoutExtension(assetPath).Replace("TX Tileset - Dungeon ", "");

    private static string Bis(string n) => TileDir + "TX Tileset - Dungeon Ground Extra_" + n + ".asset";
    private static string Dirt(string n) => CainosDir + "TX Tileset - Dungeon Ground Dirt_" + n + ".asset";
    private static string Grnd(string n) => CainosDir + "TX Tileset - Dungeon Ground_" + n + ".asset";

    // ---- neighbour-mask tile selection ---------------------------------------------------------
    //
    // ⚠️ THIS TILESET IS DIRECTIONAL, NOT A BAG OF TEXTURES. Each tile means a POSITION — top edge,
    // inner corner, west face, ceiling underside. Picking one at random from a role bucket scatters
    // edge and corner art through the middle of a solid mass, which is exactly what made the walls
    // read as noise and made neighbouring platform cells fail to line up ("platforms inside one
    // another" — their surfaces are drawn at different heights, so mixing tiles along a run breaks
    // the silhouette).
    //
    // There are no Rule Tiles in this project (1225 plain Tile assets, zero rule-based), so nothing
    // does this automatically. The table below IS the auto-tiling: it was measured by classifying
    // every painted cell in the six hand-made rooms by its 8-neighbour configuration and taking the
    // tile the designer used most for that configuration. 55 configurations, 3482 cells.
    //
    // Bits: N=1 NE=2 E=4 SE=8 S=16 SW=32 W=64 NW=128, set when that neighbour is SOLID.
    //
    // THE RULE THAT MATTERS: one tile per configuration, deterministically. Variety is safe only on
    // ISOLATED tiles (mask 0), which have no neighbours to disagree with — see PlatformSingleTiles.
    // ⚠️ ONLY CONFIGURATIONS WITH n >= 10 SAMPLES ARE LISTED HERE (trimmed 2026-08-08).
    //
    // The table originally kept all 55 measured configurations, including ones decided by a
    // handful of cells. Those were not measurements, they were coin flips — and the coin flips are
    // exactly the OUTER-CORNER configurations, because a room has hundreds of buried and wall-face
    // cells and only a few of any given corner. The visible symptom the designer reported: mask 193
    // (a wall's bottom-right outer corner) resolved to "Ground Extra_205" on **2 votes out of 8**,
    // and Extra_205 is a brown interior-looking block, so every wall in every generated room had a
    // brown nub sticking out of that corner.
    //
    // Judge these entries by SAMPLE COUNT, never by winner share. Share is low almost everywhere
    // (mask 255 has n=1231 and its winner takes only 12%) because the designer deliberately varies
    // tiles across a mass — that is variety, not uncertainty, and trimming on share would gut the
    // table. Low n is the only honest signal of "we don't actually know".
    //
    // Dropped configurations now fall through to Mask4Tiles, which is hand-written and internally
    // consistent. Mask 193 collapses to cardinals N+W -> "Ground Extra_114" — corroborated by mask
    // 195 (the same corner plus one diagonal), which has n=27 and picks Extra_114 seventeen times.
    private static readonly Dictionary<int, string> MaskTiles = new Dictionary<int, string>
    {
        // ⚠️ MASKS 28 AND 112 USED TO RESOLVE TO "Ground Dirt_13", WHICH IS A 0.4 x 0.4 PEBBLE.
        // The measurement was not wrong about what the designer painted there — but they paint it as
        // DECORATION sitting on top of a surface tile, and this table places exactly one tile per
        // cell, so it came out as a speck of art on a cell with full Grid collision. That is an
        // invisible wall: a solid corner the player can bump into with almost nothing drawn on it.
        // Both masks have N air (28 = S+SE+E, 112 = W+SW+S), i.e. a corner with open sky above, so
        // they take the designer's normal floor-surface tile instead.
        // Rule worth keeping: never let a tile smaller than a cell into this table.
        {14,"Ground Dirt_11"}, {16,"Ground_11"}, {28,"Ground Extra_144"},
        {31,"Ground Extra_156"}, {60,"Ground Extra_148"}, {63,"Ground Extra_188"},
        {68,"Ground_11"}, {95,"Ground Dirt_11"}, {112,"Ground Extra_144"},
        {120,"Ground Extra_146"}, {124,"Ground Extra_162"}, {125,"Ground Extra_144"},
        {126,"Ground Extra_162"}, {127,"Ground Extra_164"}, {135,"Ground Extra_112"},
        {159,"Ground Extra_172"}, {195,"Ground Extra_114"}, {199,"Ground Extra_96"},
        {207,"Ground Extra_98"}, {223,"Ground Extra_96"}, {224,"Ground Dirt_11"},
        {231,"Ground Extra_96"}, {241,"Ground Extra_154"}, {243,"Ground Extra_138"},
        {245,"Ground Extra_186"}, {247,"Ground Extra_98"}, {249,"Ground Extra_170"},
        {252,"Ground Extra_162"}, {253,"Ground Extra_162"}, {255,"Ground Extra_153"},
    };

    // Fallback when an 8-bit configuration wasn't seen in the hand-made rooms: collapse to the four
    // cardinals only. Complete for all 16, so selection can never fall through to nothing.
    private static readonly string[] Mask4Tiles =
    {
        /* 0 ----  */ "Ground Dirt_0",     /* 1  N--- */ "Ground Dirt_4",
        /* 2 -E--  */ "Ground_11",         /* 3  NE-- */ "Ground Extra_112",
        /* 4 --S-  */ "Ground_11",         /* 5  N-S- */ "Ground_11",
        /* 6 -ES-  */ "Ground_11",         /* 7  NES- */ "Ground Extra_156",   // air to the WEST
        /* 8 ---W  */ "Ground_11",         /* 9  N--W */ "Ground Extra_114",
        /*10 -E-W  */ "Ground_11",         /*11  NE-W */ "Ground Extra_96",    // air BELOW: ceiling
        /*12 --SW  */ "Ground_11",         /*13  N-SW */ "Ground Extra_154",   // air to the EAST
        /*14 -ESW  */ "Ground Extra_162",  /*15  NESW */ "Ground Extra_153",   // air ABOVE: surface
    };

    // LONE free-standing block — a single stepping stone with air on all four sides (mask 0). This
    // is the dominant mid-air idiom: 104 of the 217 platform cells in the hand-made rooms are
    // 1-wide singles. Variety is SAFE here precisely because an isolated tile has no neighbour to
    // misalign with.
    private static readonly string[] PlatformSingleTiles =
    {
        Dirt("0"), Dirt("0"), Dirt("0"), Dirt("0"),
        Dirt("14"), Dirt("14"), Dirt("14"), Dirt("14"),
        Dirt("4"), Dirt("4"), Dirt("4"),
        Grnd("3"), Grnd("3"),
        Grnd("11"), Grnd("11"),
        Dirt("12"), Dirt("3"), Grnd("1"), Grnd("0"),
    };

    // ---- FREE-STANDING PLATFORMS ARE STAMPED AS WHOLE SHAPES, NOT TILED PER CELL --------------
    //
    // ⚠️ THE CAINOS GROUND PALETTE IS A SET OF PRE-DRAWN PLATFORMS, NOT A SET OF 1x1 BRICKS.
    // Ground_1 is a 2x2 BOX, Ground_8 is a 3-tall PILLAR, Ground_11 is a 3-wide LEDGE, Ground_0 is a
    // 3x3 BLOCK. Painting them per-cell from the mask table stamps a whole 3-wide platform into
    // EVERY cell of a run, so a 3-cell ledge drew three overlapping copies of the same art.
    //
    // Worse, it always drew the SAME one. Every horizontal-run configuration in the mask tables
    // resolves to Ground_11 (masks 4, 68 and 64 all land on it), so every mid-air platform in every
    // generated room was the identical 3-wide ledge repeated — the designer's "the only mid-air
    // platforms you use are the same ones, over and over again". A PlatformRunTiles table existed
    // but was NEVER REFERENCED by the painter, so its variety never reached a room.
    //
    // Now a free-standing platform is measured and stamped as ONE tile matching its footprint,
    // which is how the art was drawn to be used, and gives the whole vocabulary back: single blocks,
    // 2-tall pillars, boxes, wide ledges.
    //
    // ⚠️ EVEN-SIZED PIECES STRADDLE THE GRID AND MUST BE NUDGED. A tilemap centres a sprite's pivot
    // at cell+(0.5,0.5), so odd sizes (1, 3) land on cell boundaries but even ones (2) sit half a
    // cell off. OffX/OffY push those back via Tilemap.SetTransformMatrix — verified that
    // TilemapCollider2D honours the matrix, so collision moves with the art rather than staying
    // where the art used to be.
    private struct PlatformShape
    {
        public int W, H;              // footprint in CELLS
        public int AnchorX, AnchorY;  // which cell of the footprint carries the tile
        public float OffX, OffY;      // half-cell correction for even sizes
        public string[] Tiles;        // interchangeable variants
    }

    private static readonly PlatformShape[] PlatformShapes =
    {
        // one block
        new PlatformShape { W=1, H=1, AnchorX=0, AnchorY=0,
            Tiles = new[]{ "Ground_9", "Ground_10", "Ground_9", "Ground_10", "Ground Dirt_11", "Ground Dirt_7" } },
        // 2 wide
        new PlatformShape { W=2, H=1, AnchorX=0, AnchorY=0, OffX=0.5f,
            Tiles = new[]{ "Ground_3", "Ground Dirt_3" } },
        // 3 wide ledge
        new PlatformShape { W=3, H=1, AnchorX=1, AnchorY=0,
            Tiles = new[]{ "Ground_11", "Ground_12", "Ground Dirt_14", "Ground Dirt_12" } },
        // 2 block vertical
        // ⚠️ AnchorY IS 1, NOT 0. Text rows count DOWNWARD but the tilemap counts UPWARD, and a
        // +0.5 Y nudge grows the tile UP from its stamped cell — so it has to be stamped on the
        // BOTTOM row of the footprint, which is the LAST text row. Anchored at 0 the piece landed
        // one full cell high: measured 1 of 2 cells solid with 1 cell of collision hanging in the
        // air above. Only the vertically-even shapes are affected; odd ones stamp at their middle,
        // which is the middle either way round.
        new PlatformShape { W=1, H=2, AnchorX=0, AnchorY=1, OffY=0.5f,
            Tiles = new[]{ "Ground_2", "Ground_4", "Ground_5", "Ground Dirt_5", "Ground Dirt_2", "Ground Dirt_4" } },
        // 3 tall pillar
        new PlatformShape { W=1, H=3, AnchorX=0, AnchorY=1,
            Tiles = new[]{ "Ground_8" } },
        // the box — AnchorY=1 for the same reason as the 2-tall piece above
        new PlatformShape { W=2, H=2, AnchorX=0, AnchorY=1, OffX=0.5f, OffY=0.5f,
            Tiles = new[]{ "Ground_1", "Ground Dirt_1" } },
        // big block
        new PlatformShape { W=3, H=3, AnchorX=1, AnchorY=1,
            Tiles = new[]{ "Ground_0", "Ground Dirt_0" } },
    };

    // ⚠️ THREE VARIANTS WERE REMOVED FROM THE LISTS ABOVE BECAUSE THEIR COLLISION DOES NOT FILL
    // THEIR DECLARED FOOTPRINT (measured 2026-08-14, by stamping each one on a bare tilemap and
    // probing every cell with Physics2D.OverlapPoint):
    //
    //     Ground Dirt_10   2x2   only 1 of 4 cells solid
    //     Ground_6         3x3         8 of 9
    //     Ground Dirt_6    3x3         8 of 9
    //
    // These are oversized tiles, so they keep colliderType = Sprite (TileVariantGenerator refuses
    // to give an oversized tile full-cell Grid collision — see the note there). Sprite collision
    // traces the ALPHA OUTLINE, and these three are drawn as irregular rounded rocks rather than
    // filled blocks, so the corners of the footprint are simply not there. A platform stamped with
    // one of them looks solid and is not: the player falls through part of it.
    //
    // Found when a 2x2 box added to GenLevel8 came out with 3 of its 4 cells empty. The other 17
    // variants all measured complete, so the shapes themselves are sound — only these three.
    //
    // ⚠️ ANY NEW VARIANT ADDED TO PlatformShapes MUST BE MEASURED THE SAME WAY. "It is the right
    // pixel size" is NOT enough: Ground Dirt_10 measures 2.06 x 2.03, which looks perfect.

    // Platforms bigger than this are rock masses, not ledges — they keep the mask painting.
    private const int MaxPlatformCells = 12;

    // Walkable top of a thick mass. Extra_162 is the designer's primary (147 uses); the old table
    // led with Extra_144, which is only their third choice.
    private static readonly string[] SurfaceTiles =
    {
        Bis("162"), Bis("162"), Bis("162"), Bis("162"), Bis("162"), Bis("162"),
        Bis("101"), Bis("101"), Bis("101"),
        Bis("144"), Bis("144"), Bis("144"),
        Grnd("13"), Grnd("13"),
        Bis("154"), Bis("154"),
        Bis("166"), Bis("166"),
        Bis("97"), Bis("49"),
    };

    // Underside of an overhang.
    private static readonly string[] CeilingTiles =
    {
        Bis("96"), Bis("96"), Bis("96"), Bis("96"),
        Bis("189"), Bis("189"), Bis("97"), Bis("97"),
        Bis("3"), Bis("3"), Bis("186"), Bis("186"),
        Bis("98"), Bis("98"), Bis("160"), Bis("160"), Bis("49"),
    };

    // Vertical face with open air to the LEFT (a west-facing wall).
    private static readonly string[] FaceWestTiles =
    {
        Bis("156"), Bis("156"), Bis("156"), Bis("156"),
        Bis("172"), Bis("172"), Bis("172"),
        Bis("188"), Bis("188"), Bis("137"), Bis("137"),
        Bis("153"), Bis("140"),
    };

    // Vertical face with open air to the RIGHT (an east-facing wall).
    private static readonly string[] FaceEastTiles =
    {
        Bis("154"), Bis("154"), Bis("154"),
        Bis("170"), Bis("170"), Bis("170"),
        Bis("157"), Bis("157"), Bis("173"), Bis("173"),
        Bis("189"), Bis("189"), Bis("188"), Bis("186"),
    };

    // Buried rock with solid neighbours all round — the bulk of any thick mass.
    private static readonly string[] InteriorTiles =
    {
        Bis("153"), Bis("153"), Bis("153"), Bis("153"), Bis("153"),
        Bis("185"), Bis("185"), Bis("185"), Bis("185"),
        Bis("101"), Bis("101"), Bis("101"), Bis("101"),
        Bis("49"), Bis("49"), Bis("49"),
        Bis("157"), Bis("157"), Bis("157"),
        Bis("169"), Bis("169"), Bis("169"),
        Bis("97"), Bis("97"), Bis("189"), Bis("189"), Bis("173"), Bis("173"), Bis("100"),
    };

    // Backdrop.
    //
    // ⚠️ "TX Tileable - Dungeon Wall" IS ONE SEAMLESS 8x8 PICTURE, NOT A BAG OF INTERCHANGEABLE
    // TILES. Tile N is the piece at row N/8, column N%8 of a single wall texture, and it only looks
    // like a wall when the 64 pieces are laid out in that order and repeated. The importer used to
    // hold SIX of them and scatter those six at random, which is why generated rooms never looked
    // like the hand-made ones — it was shuffling six fragments of a jigsaw instead of assembling it.
    //
    // The designer's Assets/LevelSinasi/BGPalette.prefab is the reference: measured off it, the
    // pattern repeats every 8 cells on BOTH axes with the row running downward. BackWallIndex
    // reproduces exactly that, so any room of any size gets a continuous wall.
    private const string WallDir = "Assets/Cainos/Pixel Art Platformer - Dungeon/Tileset Pallete/TP Dungeon Wall/";
    private const int BackWallPeriod = 8;

    private static readonly string[] BackWallTiles = BuildBackWallTiles();

    private static string[] BuildBackWallTiles()
    {
        var a = new string[BackWallPeriod * BackWallPeriod];
        for (int i = 0; i < a.Length; i++)
            a[i] = WallDir + "TX Tileable - Dungeon Wall_" + i + ".asset";
        return a;
    }

    // Index into BackWallTiles for a cell, reproducing the palette's tiling. Row counts DOWNWARD
    // (index 0 is the top-left piece), which is why y is negated.
    private static int BackWallIndex(int x, int y)
    {
        int col = ((x % BackWallPeriod) + BackWallPeriod) % BackWallPeriod;
        int row = (((-y) % BackWallPeriod) + BackWallPeriod) % BackWallPeriod;
        return row * BackWallPeriod + col;
    }

    // Finds every free-standing platform and stamps it as whole pre-drawn pieces (see
    // PlatformShapes). Fills `consumed[col,row]` so the per-cell mask painter skips those cells.
    //
    // "Free-standing" means a connected group of '#' that does NOT touch the grid border — i.e. a
    // mid-air ledge rather than part of the room's rock shell — and that exactly fills its bounding
    // box. Anything else falls through to the mask painter, which handles masses correctly.
    //
    // Long runs are DECOMPOSED rather than skipped: a 7-wide ledge becomes 3+3+1 or 3+2+2, chosen
    // from the piece list, so even oversized platforms stop being one tile repeated.
    private static int StampPlatformShapes(Tilemap tilemap, int width, int height,
                                           Func<int, int, char> At, Func<string, TileBase> Resolve,
                                           bool[,] consumed)
    {
        Func<int, int, bool> Solid = (c, r) =>
            c >= 0 && c < width && r >= 0 && r < height && At(c, r) == '#';

        int stamped = 0;
        var visited = new bool[width, height];
        var cells = new List<Vector2Int>();

        for (int row = 0; row < height; row++)
        for (int col = 0; col < width; col++)
        {
            if (visited[col, row] || !Solid(col, row)) continue;

            // Flood-fill this component (4-connected).
            cells.Clear();
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(col, row));
            visited[col, row] = true;
            bool touchesBorder = false;

            while (stack.Count > 0)
            {
                Vector2Int p = stack.Pop();
                cells.Add(p);
                if (p.x == 0 || p.y == 0 || p.x == width - 1 || p.y == height - 1) touchesBorder = true;

                var n = new[] { new Vector2Int(p.x+1,p.y), new Vector2Int(p.x-1,p.y),
                                new Vector2Int(p.x,p.y+1), new Vector2Int(p.x,p.y-1) };
                foreach (var q in n)
                {
                    if (q.x < 0 || q.y < 0 || q.x >= width || q.y >= height) continue;
                    if (visited[q.x, q.y] || !Solid(q.x, q.y)) continue;
                    visited[q.x, q.y] = true;
                    stack.Push(q);
                }
            }

            if (touchesBorder || cells.Count > MaxPlatformCells) continue;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var p in cells)
            {
                minX = Math.Min(minX, p.x); maxX = Math.Max(maxX, p.x);
                minY = Math.Min(minY, p.y); maxY = Math.Max(maxY, p.y);
            }
            int w = maxX - minX + 1, h = maxY - minY + 1;
            if (cells.Count != w * h) continue;          // not a solid rectangle — leave to the mask painter

            // Split into pieces the palette actually has art for.
            var pieces = new List<Vector3Int>();          // x, y (text row), packed shape index
            if (!Decompose(minX, minY, w, h, pieces)) continue;

            foreach (var piece in pieces)
            {
                PlatformShape s = PlatformShapes[piece.z];
                string name = s.Tiles[Math.Abs((piece.x * 73856093) ^ (piece.y * 19349663)) % s.Tiles.Length];
                TileBase tb = Resolve(name);
                if (tb == null) continue;

                int cx = piece.x + s.AnchorX;
                int textRow = piece.y + s.AnchorY;
                var cell = new Vector3Int(cx, height - 1 - textRow, 0);

                tilemap.SetTile(cell, tb);
                if (s.OffX != 0f || s.OffY != 0f)
                {
                    tilemap.SetTileFlags(cell, TileFlags.None);
                    tilemap.SetTransformMatrix(cell, Matrix4x4.Translate(new Vector3(s.OffX, s.OffY, 0f)));
                }
                stamped++;

                for (int dy = 0; dy < s.H; dy++)
                for (int dx = 0; dx < s.W; dx++)
                    consumed[piece.x + dx, piece.y + dy] = true;
            }
        }
        return stamped;
    }

    // Greedy split of a w x h rectangle into available pieces. Exact matches win outright; 1-wide
    // and 1-tall runs are chopped into 3s/2s/1s; anything else is refused so the mask painter keeps it.
    private static bool Decompose(int x0, int y0, int w, int h, List<Vector3Int> outPieces)
    {
        for (int i = 0; i < PlatformShapes.Length; i++)
            if (PlatformShapes[i].W == w && PlatformShapes[i].H == h)
            { outPieces.Add(new Vector3Int(x0, y0, i)); return true; }

        if (h == 1) return Chop(w, true, x0, y0, outPieces);
        if (w == 1) return Chop(h, false, x0, y0, outPieces);
        return false;
    }

    private static bool Chop(int len, bool horizontal, int x0, int y0, List<Vector3Int> outPieces)
    {
        int at = 0;
        while (at < len)
        {
            int remaining = len - at;
            // Take 3 where it fits, but never leave a remainder of exactly 1 after a 3 when a 2+2
            // split is available — long ledges then read as varied pieces instead of 3,3,3,1.
            int take = remaining >= 3 ? (remaining == 4 ? 2 : 3) : remaining;
            int idx = ShapeIndex(horizontal ? take : 1, horizontal ? 1 : take);
            if (idx < 0) return false;
            outPieces.Add(horizontal ? new Vector3Int(x0 + at, y0, idx) : new Vector3Int(x0, y0 + at, idx));
            at += take;
        }
        return true;
    }

    private static int ShapeIndex(int w, int h)
    {
        for (int i = 0; i < PlatformShapes.Length; i++)
            if (PlatformShapes[i].W == w && PlatformShapes[i].H == h) return i;
        return -1;
    }

    // Chebyshev distance from a solid cell to the nearest open one, capped at `cap`.
    // Used to leave deep interiors unpainted — see the note at the call site.
    private static int DepthFromAir(int col, int row, int cap, Func<int, int, bool> IsSolid)
    {
        for (int d = 1; d <= cap; d++)
            for (int dx = -d; dx <= d; dx++)
                for (int dy = -d; dy <= d; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != d) continue;
                    if (!IsSolid(col + dx, row + dy)) return d;
                }
        return cap;
    }

    // Scales and positions an acid pool so it exactly fills the hole it was placed in.
    //
    // The pit is measured from the GRID, not from the prefab: walk left/right along the marker's
    // row while the cells are open, and down while they are open, to get the hole's true extent.
    // The pool is then scaled to that size and seated so its TOP sits flush with the pit's rim
    // rather than floating at a cell centre.
    private static void FitAcidToPit(GameObject go, int col, int row, int cellY, int width, int height,
                                     Func<int, int, char> At, Func<int, int, bool> IsSolid)
    {
        int left = col;
        while (left - 1 >= 0 && !IsSolid(left - 1, row)) left--;
        int right = col;
        while (right + 1 < width && !IsSolid(right + 1, row)) right++;

        int bottom = row;
        while (bottom + 1 < height && !IsSolid(col, bottom + 1)) bottom++;

        // ⚠️ The pit's top is where its SIDE WALLS run out — NOT the first solid cell above.
        // A pit opens upward into the room, so walking up until something is solid measures the
        // whole chamber (9 tiles here instead of 2) and launches the pool into the air. The hole
        // only exists while there is rock beside it, so climb while either flank is still solid.
        int top = bottom;
        while (top - 1 >= 0
               && !IsSolid(col, top - 1)
               && (IsSolid(left - 1, top - 1) || IsSolid(right + 1, top - 1)))
            top--;

        float w = right - left + 1;
        float h = bottom - top + 1;
        if (w <= 0f || h <= 0f) return;

        var box = go.GetComponentInChildren<BoxCollider2D>();
        Vector2 native = box != null ? box.size : new Vector2(5.98f, 2.53f);
        if (native.x <= 0.01f || native.y <= 0.01f) return;

        // ⚠️ The pool is NOT centred on its own transform. AcidWater's BoxCollider2D carries an
        // offset of (0, 1.27), so positioning the transform at the pit centre floats the water a
        // scaled 1.27 units too high - half the pool ends up hanging above the floor it should be
        // sunk into. Back the offset out, scaled, or the fit is silently off by it.
        Vector2 nativeOffset = box != null ? box.offset : Vector2.zero;

        Vector3 scale = new Vector3(w / native.x, h / native.y, 1f);
        go.transform.localScale = scale;

        // Grid rows run DOWNWARD while world Y runs up, so a row above the marker is (row - top)
        // cells higher. The rim is the top edge of that cell; the pool centres half its height below.
        float rimY = cellY + (row - top) + 1f;
        go.transform.position = new Vector3(left + w * 0.5f - nativeOffset.x * scale.x,
                                            rimY - h * 0.5f - nativeOffset.y * scale.y,
                                            0f);
    }

    [MenuItem("Deckshift/Import Level From Text...")]
    public static void ImportFromText()
    {
        string startDir = Directory.Exists(InputFolder) ? Path.GetFullPath(InputFolder) : Application.dataPath;
        string filePath = EditorUtility.OpenFilePanel("Select level text file", startDir, "txt");
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            string savedPath = Build(filePath);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(savedPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            // Only the interactive menu path shows the modal summary — see the note in Build().
            EditorUtility.DisplayDialog("Level imported", lastReport, "OK");
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Level import failed", e.Message, "OK");
            Debug.LogError("[LevelTextImporter] " + e);
        }
    }

    private static string Build(string filePath)
    {
        // ---------- Parse ----------
        var directives = new Dictionary<string, string>();
        var gridLines = new List<string>();

        foreach (string raw in File.ReadAllLines(filePath))
        {
            string line = raw.TrimEnd('\r', '\n');
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("//"))
                continue; // comment

            if (trimmed.StartsWith("!"))
            {
                int colon = trimmed.IndexOf(':');
                if (colon > 1)
                {
                    string key = trimmed.Substring(1, colon - 1).Trim().ToLowerInvariant();
                    string value = trimmed.Substring(colon + 1).Trim();
                    directives[key] = value;
                }
                continue;
            }

            gridLines.Add(line);
        }

        // Trim leading/trailing fully-empty lines (interior empty lines are valid rows).
        while (gridLines.Count > 0 && gridLines[0].Trim().Length == 0) gridLines.RemoveAt(0);
        while (gridLines.Count > 0 && gridLines[gridLines.Count - 1].Trim().Length == 0) gridLines.RemoveAt(gridLines.Count - 1);

        if (gridLines.Count == 0)
            throw new Exception("No grid found in file. Add rows of '#' and '.' below the '!' directives.");

        int height = gridLines.Count;
        int width = 0;
        foreach (string l in gridLines)
            width = Mathf.Max(width, l.Length);

        string levelName = directives.TryGetValue("name", out string dirName) && dirName.Length > 0
            ? dirName
            : Path.GetFileNameWithoutExtension(filePath);

        // char at (col, rowFromTop); short lines count as empty
        char At(int col, int row)
        {
            string l = gridLines[row];
            return col < l.Length ? l[col] : '.';
        }
        bool InGrid(int col, int row) => col >= 0 && col < width && row >= 0 && row < height;
        // outside the grid counts as SOLID so border outer faces don't read as exposed
        bool IsSolid(int col, int row)
        {
            if (!InGrid(col, row)) return true;
            return At(col, row) == '#';
        }

        // ---------- Validate ----------
        var warnings = new List<string>();
        var unknownChars = new HashSet<char>();
        int spawnCount = 0, exitCount = 0;

        bool hasOneWay = false, hasGate = false, hasAltar = false;
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                char c = At(col, row);
                if (c == '.' || c == ' ' || c == '#') continue;
                if (c == '=') { hasOneWay = true; continue; }
                if (c == 'G') { hasGate = true; continue; }
                if (c == 'A') { hasAltar = true; continue; }
                if (c == 'S') { spawnCount++; continue; }
                if (c == 'X') exitCount++;
                if (!MarkerPrefabs.ContainsKey(c)) unknownChars.Add(c);
            }
        }

        if (spawnCount != 1)
            throw new Exception($"A room needs exactly one 'S' (player spawn). Found {spawnCount}.");
        if (exitCount == 0)
            warnings.Add("No 'X' (ExitDoor) — the room will have no way to leave.");
        foreach (char c in unknownChars)
            warnings.Add($"Unknown marker '{c}' ignored.");

        // ---------- Preload assets ----------
        TileBase[] platSingle = LoadTileSet(PlatformSingleTiles);
        TileBase[] backWall = LoadTileSet(BackWallTiles);

        // Resolve a measured tile NAME to an asset. Extra_* live in the biseyler copies; the
        // Ground_*/Dirt_* platform tiles exist ONLY in the Cainos palette folder, so try both.
        var tileByName = new Dictionary<string, TileBase>();
        Func<string, TileBase> Resolve = shortName =>
        {
            TileBase cached;
            if (tileByName.TryGetValue(shortName, out cached)) return cached;
            string file = "TX Tileset - Dungeon " + shortName + ".asset";
            // ⚠️ PREFER THE " Solid" VARIANT. The pack's tiles use colliderType = Sprite, so
            // collision traces the sprite's ALPHA OUTLINE — including the little protruding brick
            // nubs on the wall-face tiles. The player catches on them, and the collision gizmo
            // shows a bumpy edge instead of a clean wall. The Solid variants are identical art
            // with colliderType = Grid, which is what a solid cell in a platformer wants.
            string solid = "TX Tileset - Dungeon " + shortName + " Solid.asset";
            TileBase tb = AssetDatabase.LoadAssetAtPath<TileBase>(TileVariantGenerator.VariantFolder + "/" + solid)
                       ?? AssetDatabase.LoadAssetAtPath<TileBase>(TileVariantGenerator.VariantFolder + "/" + file)
                       ?? AssetDatabase.LoadAssetAtPath<TileBase>(TileDir + file)
                       ?? AssetDatabase.LoadAssetAtPath<TileBase>(CainosDir + file);
            tileByName[shortName] = tb;
            return tb;
        };

        // Warm the cache and fail loudly rather than silently painting holes.
        //
        // ⚠️ THE SPRITE CHECK IS THE IMPORTANT HALF. `TX Tileset - Dungeon Ground_13` exists as an
        // asset but ships with a NULL SPRITE — the pack's valid range stops at Ground_12, so it is
        // one entry past the end. It was the tile used for every platform run, so 30 of 499 cells
        // in a generated room were painted with nothing and the ledges were literally invisible;
        // the room looked like it had no mid-air platforms at all because it didn't. A
        // resolve-only check passes that happily, so both conditions are enforced here.
        var broken = new List<string>();
        Action<string> Check = n =>
        {
            TileBase tb = Resolve(n);
            if (tb == null) { broken.Add(n + "  (asset missing)"); return; }
            Tile asTile = tb as Tile;
            if (asTile == null) return;
            if (asTile.sprite == null) { broken.Add(n + "  (SPRITE IS NULL — would paint an invisible cell)"); return; }

            // ⚠️ AND IT MUST FILL ITS CELL. "Ground Dirt_13" is a 0.4 x 0.4 pebble that the designer
            // paints ON TOP of a surface tile as decoration; this table places one tile per cell, so
            // using it as a solid cell drew a speck over full collision — an invisible wall. A null
            // sprite and a too-small sprite fail the same way (solid where nothing is drawn), so
            // both are caught here.
            float w = asTile.sprite.rect.width / asTile.sprite.pixelsPerUnit;
            float h = asTile.sprite.rect.height / asTile.sprite.pixelsPerUnit;
            if (w < 0.95f || h < 0.95f)
                broken.Add(n + $"  (sprite is only {w:F2}x{h:F2} cells — too small to be a solid cell)");
        };
        foreach (var kv in MaskTiles) Check(kv.Value);
        foreach (string n in Mask4Tiles) Check(n);
        foreach (string n in PlatformSingleTiles) Check(ShortNameOf(n));
        if (broken.Count > 0)
            throw new Exception("LevelTextImporter: unusable tiles in the mask table:\n  "
                                + string.Join("\n  ", broken.ToArray()));

        var prefabCache = new Dictionary<char, GameObject>();
        var missing = new List<string>();
        foreach (var kv in MarkerPrefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value);
            if (prefab == null) missing.Add($"'{kv.Key}' -> {kv.Value}");
            else prefabCache[kv.Key] = prefab;
        }
        var spawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnPrefabPath);
        if (spawnPrefab == null) missing.Add($"'S' -> {SpawnPrefabPath}");
        var camBoundsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraBoundsPrefabPath);
        if (camBoundsPrefab == null) missing.Add($"CameraBounds -> {CameraBoundsPrefabPath}");

        Sprite gateSprite = LoadPropSprite(GateSpriteName);
        if (hasGate && gateSprite == null) missing.Add($"'G' gate sprite '{GateSpriteName}' in {PropsTexturePath}");

        var altarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AltarPrefabPath);
        if (hasAltar && altarPrefab == null) missing.Add($"'A' -> {AltarPrefabPath}");

        if (missing.Count > 0)
            throw new Exception("Prefab paths in LevelTextImporter are stale, fix them:\n" + string.Join("\n", missing));

        // deterministic per-cell variety, independent of iteration order
        TileBase Pick(TileBase[] set, int col, int cellY)
        {
            int h = (col * 73856093) ^ (cellY * 19349663);
            return set[Math.Abs(h) % set.Length];
        }

        // ---------- Build ----------
        var root = new GameObject(levelName);
        try
        {
            // "!recharge: on" marks a Foundry / Market / Well. The marker is what stops the exit
            // door paying a free flawless clear, oath step and Nest Egg for a room with no enemies
            // in it — forgetting it makes the room a silent exploit, so it lives in the text file
            // beside the layout rather than being a component someone must remember to add.
            if (directives.TryGetValue("recharge", out string rechargeV)
                && (rechargeV.Equals("on", StringComparison.OrdinalIgnoreCase)
                    || rechargeV.Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                root.AddComponent<RechargeRoomMarker>();
            }

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform);
            gridGo.transform.localPosition = new Vector3(0f, 0f, GroundZ);
            gridGo.AddComponent<Grid>(); // default 1x1 cells, like BGPalette

            // Backdrop: OPT-IN via "!backwall: on" (the designer usually places the
            // backdrop/decoration by hand). When on, it must live on the "Background"
            // sorting LAYER (like BGPalette's backdrop): several sprites (e.g.
            // ExitDoor at Default order -1) get swallowed by a Default-layer backdrop.
            // DEFAULT ON since 2026-08-13. It used to be opt-in because the generated backdrop was
            // six wall fragments scattered at random and looked wrong, so the designer preferred to
            // paint backdrops by hand. Now that it assembles the real seamless wall (BackWallIndex),
            // a generated room should have one by default. "!backwall: off" still turns it off.
            string bwv;
            bool backwallOn = !directives.TryGetValue("backwall", out bwv)
                || !(bwv.Trim().ToLowerInvariant() == "off" || bwv.Trim().ToLowerInvariant() == "false");
            Tilemap backMap = null;
            if (backwallOn)
            {
                var backGo = new GameObject("BackWall");
                backGo.transform.SetParent(gridGo.transform);
                backGo.transform.localPosition = Vector3.zero;
                backMap = backGo.AddComponent<Tilemap>();
                var backRenderer = backGo.AddComponent<TilemapRenderer>();
                backRenderer.sortingLayerName = "Background";
                backRenderer.sortingOrder = BackWallSortingOrder;
            }

            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(gridGo.transform);
            groundGo.transform.localPosition = Vector3.zero;
            groundGo.layer = GroundLayer;
            var tilemap = groundGo.AddComponent<Tilemap>();
            groundGo.AddComponent<TilemapRenderer>().sortingOrder = GroundSortingOrder;
            groundGo.AddComponent<TilemapCollider2D>();

            // One-way platforms ('='): their own tilemap with a one-way
            // PlatformEffector2D — jump up through them, land on top.
            Tilemap oneWayMap = null;
            if (hasOneWay)
            {
                var oneWayGo = new GameObject("OneWay");
                oneWayGo.transform.SetParent(gridGo.transform);
                oneWayGo.transform.localPosition = Vector3.zero;
                oneWayGo.layer = GroundLayer; // ground checks must see it
                oneWayMap = oneWayGo.AddComponent<Tilemap>();
                oneWayGo.AddComponent<TilemapRenderer>().sortingOrder = GroundSortingOrder;
                var twc = oneWayGo.AddComponent<TilemapCollider2D>();
                var composite = oneWayGo.AddComponent<CompositeCollider2D>(); // auto-adds a Rigidbody2D
                oneWayGo.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                twc.compositeOperation = Collider2D.CompositeOperation.Merge;
                composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
                composite.usedByEffector = true;
                var effector = oneWayGo.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 170f;
            }

            // Whole-shape pass FIRST: it claims the free-standing platforms so the per-cell mask
            // painter below leaves them alone.
            var consumed = new bool[width, height];
            int tileCount = StampPlatformShapes(tilemap, width, height, At, Resolve, consumed);
            var spikeDone = new bool[width, height];   // a '^' run is spawned once, at its left end

            var entityCounts = new Dictionary<string, int>();
            var gateColumns = new Dictionary<int, List<int>>(); // col -> rows with 'G'
            var levers = new List<Lever>();
            var altars = new List<ShiftAltar>();

            for (int row = 0; row < height; row++)
            {
                int cellY = height - 1 - row; // row 0 is the TOP line of the file
                for (int col = 0; col < width; col++)
                {
                    // Assembled, not shuffled — see BackWallIndex.
                    if (backMap != null)
                        backMap.SetTile(new Vector3Int(col, cellY, 0), backWall[BackWallIndex(col, cellY)]);

                    char c = At(col, row);
                    if (c == '.' || c == ' ')
                        continue;

                    if (c == '#')
                    {
                        // Already drawn as a whole pre-drawn platform piece.
                        if (consumed[col, row]) continue;

                        bool airUp = !IsSolid(col, row - 1);
                        bool airDown = !IsSolid(col, row + 1);
                        bool airLeft = !IsSolid(col - 1, row);
                        bool airRight = !IsSolid(col + 1, row);

                        // A "strip" cell is 1 tile thick (air above AND below):
                        // floating platforms and wall-attached shelves alike.
                        bool IsStrip(int cc) => cc >= 0 && cc < width && At(cc, row) == '#'
                            && !IsSolid(cc, row - 1) && !IsSolid(cc, row + 1);

                        // ---- DEEP INTERIORS ARE NOT PAINTED --------------------------------
                        //
                        // Measured across the six hand-made rooms: 2355 solid cells sit 1 away
                        // from open air, 1074 at 2, 134 at 3, 23 at 4, and NOTHING deeper. The
                        // designer never builds a deep solid interior, so this tileset has never
                        // had to be a fill texture — it is a FACING set, all edges and near-edge
                        // detail. Painted across a 10-deep mass it reads as ugly repeating
                        // wallpaper, which is exactly what generated rooms were doing.
                        //
                        // ⚠️ An earlier version left these cells UNPAINTED so the backdrop showed
                        // through. That was worse: solid rock then reads as open background, which
                        // misleads the player while moving and while peeking with Ctrl. Deep rock
                        // must still look SOLID — just recessed.
                        //
                        // So deep cells are painted with DARKENED COPIES of the interior tiles
                        // (TileVariantGenerator). Same art, same collision, pushed dark and
                        // slightly cool so a mass reads as receding shadow instead of another lit
                        // surface — and the brick detail stays faintly legible rather than going
                        // to flat black.
                        // ⚠️ DO NOT JITTER THIS THRESHOLD. A hashed -1/0/+1 nudge was added on
                        // 2026-08-08 to break up the Chebyshev metric's rectangular contours, and
                        // the designer rejected it — the interiors read better with the hard step.
                        // It also let a +1 push DEEP tiles out to depth 2, one cell from the face,
                        // which is exactly the dark block sticking out of a wall edge that the
                        // change was supposed to help. Deep fill starts at a hard depth of 3.
                        int depth = DepthFromAir(col, row, 5, IsSolid);
                        if (depth >= 3)
                        {
                            string[] step = DeepFillTiles[Mathf.Min(depth - 3, DeepFillTiles.Length - 1)];
                            TileBase deep = Resolve(step[Mathf.Abs((col * 73856093) ^ (cellY * 19349663)) % step.Length]);
                            if (deep != null)
                            {
                                tilemap.SetTile(new Vector3Int(col, cellY, 0), deep);
                                tileCount++;
                                continue;
                            }
                        }

                        // Tile chosen by the cell's 8-neighbour configuration — this IS the
                        // auto-tiling (see MaskTiles). One tile per configuration, deterministic,
                        // so edges land on edges and a run of platform cells shares one silhouette.
                        int mask = 0;
                        if (IsSolid(col, row - 1)) mask |= 1;     // N
                        if (IsSolid(col + 1, row - 1)) mask |= 2; // NE
                        if (IsSolid(col + 1, row)) mask |= 4;     // E
                        if (IsSolid(col + 1, row + 1)) mask |= 8; // SE
                        if (IsSolid(col, row + 1)) mask |= 16;    // S
                        if (IsSolid(col - 1, row + 1)) mask |= 32;// SW
                        if (IsSolid(col - 1, row)) mask |= 64;    // W
                        if (IsSolid(col - 1, row - 1)) mask |= 128;// NW

                        TileBase tile;
                        if (mask == 0)
                        {
                            // Isolated stepping stone. The ONLY place variety is safe, because
                            // there is no neighbour for it to disagree with.
                            tile = Pick(platSingle, col, cellY);
                        }
                        else
                        {
                            string name;
                            if (!MaskTiles.TryGetValue(mask, out name))
                            {
                                // Configuration never seen in the hand-made rooms: collapse to the
                                // four cardinals, which is complete for all 16 cases.
                                int m4 = ((mask & 1) != 0 ? 1 : 0) | ((mask & 4) != 0 ? 2 : 0)
                                       | ((mask & 16) != 0 ? 4 : 0) | ((mask & 64) != 0 ? 8 : 0);
                                name = Mask4Tiles[m4];
                            }
                            tile = Resolve(name);
                        }

                        tilemap.SetTile(new Vector3Int(col, cellY, 0), tile);
                        tileCount++;
                        continue;
                    }

                    if (c == '=')
                    {
                        // one-way lip: thin brick-top tile, visually distinct from solid strips.
                        // (Level Design Law 4 bans these in new rooms; kept so old texts import.)
                        oneWayMap.SetTile(new Vector3Int(col, cellY, 0), Resolve("Ground Extra_162"));
                        tileCount++;
                        continue;
                    }

                    if (c == 'G')
                    {
                        if (!gateColumns.TryGetValue(col, out var rows)) gateColumns[col] = rows = new List<int>();
                        rows.Add(row);
                        continue;
                    }

                    Vector3 worldPos = new Vector3(col + 0.5f, cellY + 0.5f, 0f);

                    if (c == 'A')
                    {
                        // Instantiated from the prefab rather than assembled here, so the altar's
                        // sprite, sorting order, layer and trigger box are defined once.
                        var altarGo = (GameObject)PrefabUtility.InstantiatePrefab(altarPrefab);
                        altarGo.name = "ShiftAltar";
                        altarGo.transform.SetParent(root.transform);
                        altarGo.transform.position = worldPos;
                        altars.Add(altarGo.GetComponent<ShiftAltar>());
                        GroundToSurface(altarGo, cellY);
                        entityCounts.TryGetValue("ShiftAltar", out int na);
                        entityCounts["ShiftAltar"] = na + 1;
                        continue;
                    }

                    if (c == 'S')
                    {
                        var spawn = (GameObject)PrefabUtility.InstantiatePrefab(spawnPrefab);
                        spawn.name = "GirisNoktasi"; // LevelManager finds it by this exact name
                        spawn.transform.SetParent(root.transform); // must be a DIRECT child of the room root
                        spawn.transform.position = worldPos;
                        GroundToSurface(spawn, cellY); // player pivot is at the feet; spawn at floor level
                        continue;
                    }

                    // ⚠️ SPIKES ARE WIDER THAN A CELL, SO ONE PER CELL MAKES THEM OVERLAP.
                    // The spikers prefab measures 1.55 units across; dropping one on every '^' cell
                    // spaced them 1.0 apart, so each sat a third of the way inside its neighbour and
                    // the bed read as a mangled pile rather than a row of spikes. A run of '^' is now
                    // laid end-to-end: as many as fit at the prefab's real width, spread evenly so
                    // the run is covered edge to edge without any two intersecting.
                    if (c == '^' && prefabCache.ContainsKey('^'))
                    {
                        if (spikeDone[col, row]) continue;

                        int cEnd = col;
                        while (cEnd + 1 < width && At(cEnd + 1, row) == '^') cEnd++;
                        for (int cc = col; cc <= cEnd; cc++) spikeDone[cc, row] = true;

                        GameObject spikePrefab = prefabCache['^'];
                        float runW = cEnd - col + 1;
                        float spikeW = Mathf.Max(0.05f, MeasuredSize(spikePrefab).x);
                        int count = Mathf.Max(1, Mathf.FloorToInt(runW / spikeW + 0.001f));
                        float pitch = runW / count;

                        for (int i = 0; i < count; i++)
                        {
                            var sp = (GameObject)PrefabUtility.InstantiatePrefab(spikePrefab);
                            sp.transform.SetParent(root.transform);
                            sp.transform.position = new Vector3(col + pitch * (i + 0.5f), cellY + 0.5f, 0f);
                            GroundToSurface(sp, cellY);
                            entityCounts.TryGetValue(spikePrefab.name, out int sn);
                            entityCounts[spikePrefab.name] = sn + 1;
                        }
                        continue;
                    }

                    if (prefabCache.TryGetValue(c, out GameObject prefab))
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        go.transform.SetParent(root.transform);
                        go.transform.position = worldPos;

                        // Acid pools must FIT THEIR PIT, not sit at a cell centre at a fixed size.
                        //
                        // AcidWater is a fixed ~5.98 x 2.53 box whose water is a shader/mesh with NO
                        // SpriteRenderer, so the usual auto-grounding (which measures renderer
                        // bounds) measures nothing and leaves the pool floating, overflowing the
                        // hole it is supposed to be filling. Measure the actual hole from the grid
                        // and scale to it instead.
                        if (c == 'w')
                        {
                            FitAcidToPit(go, col, row, cellY, width, height, At, IsSolid);
                        }
                        else if (GroundedMarkers.Contains(c))
                            GroundToSurface(go, cellY);
                        if (c == 'L')
                        {
                            var lever = go.GetComponent<Lever>();
                            if (lever != null) levers.Add(lever);
                            else warnings.Add("Lever prefab has no Lever component?");
                        }
                        entityCounts.TryGetValue(prefab.name, out int n);
                        entityCounts[prefab.name] = n + 1;
                    }
                }
            }

            // ---------- Gates: vertical runs of 'G' become one sliding portcullis ----------
            var gates = new List<Gate>();
            foreach (var kv in gateColumns)
            {
                int col = kv.Key;
                var rows = kv.Value;
                rows.Sort();
                int runStart = 0;
                for (int i = 1; i <= rows.Count; i++)
                {
                    if (i < rows.Count && rows[i] == rows[i - 1] + 1) continue;
                    int rowTop = rows[runStart], rowBottom = rows[i - 1];
                    float h = rowBottom - rowTop + 1;
                    float bottomY = height - 1 - rowBottom; // cellY of the lowest gate cell

                    var gateGo = new GameObject("Gate");
                    gateGo.transform.SetParent(root.transform);
                    gateGo.transform.position = new Vector3(col + 0.5f, bottomY + h / 2f, 0f);
                    gateGo.layer = GroundLayer; // solid: blocks the player while closed
                    var solid = gateGo.AddComponent<BoxCollider2D>();
                    solid.size = new Vector2(1f, h);

                    var visual = new GameObject("Visual");
                    visual.transform.SetParent(gateGo.transform, false);
                    var vsr = visual.AddComponent<SpriteRenderer>();
                    vsr.sprite = gateSprite;
                    vsr.sortingOrder = 2;
                    float spriteH = gateSprite.bounds.size.y;
                    if (spriteH > 0.01f)
                    {
                        float scale = h / spriteH;
                        visual.transform.localScale = Vector3.one * scale;
                        // Cainos props pivot at their BASE — recenter so the sprite's
                        // visual middle sits on the gate's (collider) middle.
                        visual.transform.localPosition = -(Vector3)gateSprite.bounds.center * scale;
                    }

                    // Gate.cs re-dresses this single sprite into arch + passage + two leaves at
                    // runtime and opens the leaves in place, so there is no travel to configure.
                    var gate = gateGo.AddComponent<Gate>();
                    gates.Add(gate);
                    runStart = i;
                }
            }
            if (gates.Count > 0) entityCounts["Gate"] = gates.Count;

            // ---------- Auto-wiring: levers and altars drive their NEAREST gate ----------
            Gate Nearest(Vector3 from)
            {
                Gate best = null; float bestD = float.MaxValue;
                foreach (var g2 in gates)
                {
                    float d = (g2.transform.position - from).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = g2; }
                }
                return best;
            }
            foreach (var lever in levers)
            {
                var g2 = Nearest(lever.transform.position);
                if (g2 == null) { warnings.Add("Lever placed but no 'G' gate to wire it to."); continue; }
                UnityEventTools.AddPersistentListener(lever.OnFlippedOn, g2.Open);
                UnityEventTools.AddPersistentListener(lever.OnFlippedOff, g2.Close);
            }
            foreach (var altar in altars)
            {
                var g2 = Nearest(altar.transform.position);
                if (g2 == null) { warnings.Add("Shift Altar placed but no 'G' gate to wire it to."); continue; }
                UnityEventTools.AddPersistentListener(altar.OnPaid, g2.Open);
                altar.signalTarget = g2.transform; // the signal orb flies here on payment
            }

            // Camera zone: one BoxCollider2D covering the whole grid (with margin).
            var camBounds = (GameObject)PrefabUtility.InstantiatePrefab(camBoundsPrefab);
            camBounds.name = "CameraBounds"; // LevelManager finds it by this exact name
            camBounds.transform.SetParent(root.transform);
            camBounds.transform.position = new Vector3(width / 2f, height / 2f, 0f);
            var zone = camBounds.GetComponent<BoxCollider2D>();
            if (zone != null)
            {
                zone.offset = Vector2.zero;
                zone.size = new Vector2(Mathf.Max(width + 4f, 32f), Mathf.Max(height + 4f, 20f));
            }
            else
            {
                warnings.Add("CameraBounds prefab has no BoxCollider2D on its root — zone not resized.");
            }

            // ---------- Save ----------
            // Unity refuses to save a prefab whose hierarchy contains missing
            // scripts (this is how the dead AeroBat.prefab broke GenLevel1's
            // import). Detect it up front and name the culprit.
            var withMissing = new List<string>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                    withMissing.Add(t.name);
            }
            if (withMissing.Count > 0)
                throw new Exception(
                    "These objects contain MISSING SCRIPTS, and Unity refuses to save a prefab containing them.\n" +
                    "Fix (or stop using) the source prefab(s) of:\n  " + string.Join("\n  ", withMissing));

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(OutputFolder));

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{levelName}.prefab");
            PrefabUtility.SaveAsPrefabAsset(root, assetPath, out bool success);
            if (!success)
                throw new Exception("Unity failed to save the prefab (see Console).");

            // ---------- Report ----------
            var report = new StringBuilder();
            report.AppendLine($"Saved: {assetPath}");
            report.AppendLine($"Size: {width} x {height} cells, {tileCount} ground tiles");
            foreach (var kv in entityCounts)
                report.AppendLine($"  {kv.Value} x {kv.Key}");
            if (warnings.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Warnings:");
                foreach (string w in warnings)
                    report.AppendLine("  - " + w);
            }
            report.AppendLine();
            report.AppendLine("Remember: add the prefab to LevelManager's Room Prefabs list to put it in the run.");

            // NOTE: the summary dialog belongs to the MENU path only (see ImportFromText).
            // EditorUtility.DisplayDialog is modal and blocks Unity's main thread until someone
            // clicks OK, which makes Build() impossible to call from a script — it hangs the
            // editor, and any batch import or automated re-import with it.
            Debug.Log("[LevelTextImporter] " + report);
            lastReport = report.ToString();
            return assetPath;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // Shift an instantiated object so the bottom of its visual bounds sits
    // exactly at surfaceY. Uses renderers (ignoring particles/trails), falling
    // back to 2D colliders. Prefab pivots vary wildly; measuring beats guessing.
    private static void GroundToSurface(GameObject go, float surfaceY)
    {
        Bounds? b = null;
        void Add(Bounds nb)
        {
            if (b == null) { b = nb; return; }
            var bb = b.Value; bb.Encapsulate(nb); b = bb;
        }

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || !r.enabled) continue;
            Add(r.bounds);
        }
        if (b == null)
        {
            foreach (var c in go.GetComponentsInChildren<Collider2D>())
                Add(c.bounds);
        }
        if (b == null) return;

        float dy = surfaceY - b.Value.min.y;
        go.transform.position += new Vector3(0f, dy, 0f);
    }

    // Combined visual size of a prefab, ignoring particles/trails — the same measurement
    // GroundToSurface uses, exposed so placement can space objects by how wide they really are.
    private static Vector2 MeasuredSize(GameObject go)
    {
        Bounds? b = null;
        void Add(Bounds nb)
        {
            if (b == null) { b = nb; return; }
            var bb = b.Value; bb.Encapsulate(nb); b = bb;
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            Add(r.bounds);
        }
        if (b == null)
            foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) Add(c.bounds);
        return b == null ? Vector2.one : new Vector2(b.Value.size.x, b.Value.size.y);
    }


    private static Sprite LoadPropSprite(string spriteName)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(PropsTexturePath))
            if (o is Sprite s && s.name == spriteName)
                return s;
        return null;
    }

    private static TileBase LoadTile(string path)
    {
        var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
        if (tile == null)
            throw new Exception($"Tile asset not found: {path}");
        return tile;
    }

    private static TileBase[] LoadTileSet(string[] paths)
    {
        var tiles = new TileBase[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            tiles[i] = LoadTile(paths[i]);
        return tiles;
    }
}
