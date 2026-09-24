---
name: deckshift-levels
description: Deckshift's rooms — the eight Level Design Laws, the ASCII level importer and its tile-painting rules, the level validator and movement budget, doors/gates, the room pool and its contract, and the run map. Use when authoring, importing, validating, debugging or placing anything in a room prefab, or when touching LevelManager, the run map, tiles, gates or the exit door.
---

# Deckshift Levels

> ⚠️ **ACTS ARE GONE (designer, 2026-08-21). This file still says "act" in ~40 places and every one
> of them now means RUN.** A run is one map of 20 floors with 2-5 OPTIONAL boss nodes the player
> routes into or around, plus one unique FinalBoss at the top. `MapNodeType.Boss` is a mid-map node
> in a real column, NOT the finale; `MapNodeType.FinalBoss` is. Every optional boss is proven
> avoidable. The map SCROLLS now (fixed floor pitch, RectMask2D viewport) rather than dividing the
> visible height by the floor count. Read the run-map section below with that substitution in mind.

Split out of CLAUDE.md 2026-08-20. Everything here was paid for with a real mistake in
this project — the rules carry their reasons, so do not strip them.

**Read the Level Design Laws before drawing a room. Read the room contract before adding
one to the pool.**

---


### LEVEL DESIGN LAWS (designer-stated 2026-07-14 — absolute)

1. **Every level must be completable with ONLY jumping and moving.** Cards, fans, elevators, trapdoors, and any other mechanic may only gate OPTIONAL things: loot, shortcuts, Shift savings. If a mechanic fails or the player has no cards, the exit must still be reachable. (Violation that prompted this rule: GenLevel3's first draft made a fan relay the only way over a tall wall.)
2. Mandatory-path geometry (**recalibrated from designer playtest 2026-07-14: the character jumps ~5-6 tiles**, not the 4 the old physics math said): design mandatory rises at **4** (comfortable), 5 only for optional challenge, card-gated pockets need rises ≥ 8. Flat gaps ≤ 5-6 tiles. ≥ 5 tiles of clear air above launch surfaces. **Don't crowd platforms** — same-column vertical spacing between floating ledges ≥ 7 tiles; GenLevel5's 3-tile ladder spacing read as clutter.
3. Hazard pits on the mandatory path must be escapable (shallow enough to jump out) and crossable without aid platforms.
4. **NO one-way (`=`) platforms in levels** (designer 2026-07-14: "they feel wrong and also work bad and buggy, and there is no visual clearance for them"). The importer still supports `=` but don't place it — use solid 1-thick `#` strips (the `Extra_112/113/114` platform-strip look) and route jumps AROUND them, zig-zag ladder style on alternating shaft walls.
5. **Turrets (`t`) only on walls or ceilings** — that's how the hand-made levels use them, so they're hard to kill. The importer can only floor-ground them, so generated levels must NOT use `t` at all; use a melee (`m`) or ranged (`r`) enemy instead. (Designer 2026-07-14, after GenLevel5's exposed floor turret.)
6. The player has **no wall-breaking attack** (fireballs don't break walls) — never design a secret that requires destroying terrain. Card-gated secrets = Phase through a 1-thick wall, Portal, or an 8+ tile rise.
7. **Entry and exit must be far apart in the map** (designer 2026-07-14, after GenLevel6 v1 put the exit directly above the spawn behind a 2-thick slab): a Phase/Portal card must never be able to skip the level. Keep the spawn and the ExitDoor in different regions — roughly 20+ tiles apart, separated by whole chambers of solid rock, never by a thin wall or single floor slab.
8. **THE SPAWN IS A SAFE BEACH** (designer 2026-08-07). The player must be able to arrive, look around, read their deck and decide *before* anything can touch them. **No enemies on the platform the player spawns on, and nothing able to target them there** — ranged/flying enemies must not have line of sight to the spawn. Enforced by `LevelValidator` (LAW 8): it finds the spawn's contiguous ground run and fails on any enemy standing on it, then ray-checks ranged (`r s t`) and flying (`b`) enemies within 26 tiles and melee within 10. Line of sight, not raw distance — a spitter 20 tiles down a clear corridor is aiming at you; one 6 tiles away behind rock is not.

9. ⚠️ **PROVISIONAL — the designer said this was written up wrong and will restate it ("we can see about that later on", 2026-08-08). Do not treat it as settled; ask before designing to it.** The rough shape, from GenLevel8 where they placed a Blompo on such a platform themselves: a ledge reachable only by dropping onto it, or only along one narrow guarded approach, wants **something on it to claim** — loot, a shop, Blompo, an NPC — which is what makes the player accept the narrow path with an enemy in it. What is NOT yet confirmed is how far that generalises.

### RECHARGE ROOMS — Foundry / Market / Well (BUILT 2026-09-14)

**All three exist now and are assigned on `LevelManager` in SampleScene**, so the map draws
recharge icons (measured on one seed: 12 recharge attachments across 62 nodes). Verified in play
mode end to end: hub → Fight (floor 1) → Well (floor STILL 1, map not advanced) → Fight (floor 2).

| room | prefab | source | furniture | fixes the problem of |
|---|---|---|---|---|
| **Well** | `LevelGenerated/Well.prefab` | `LevelTexts/Well.txt` | `H` RestWell | being worn down — **+40 HP, +8 Shift, once per visit** |
| **Foundry** | `LevelGenerated/Foundry.prefab` | `LevelTexts/Foundry.txt` | `f` ScrapForge + `B` Blompo | a damaged deck — scrap and blessings |
| **Market** | `LevelGenerated/Market.prefab` | `LevelTexts/Market.txt` | `$` Shopkeeper + `Q` QuestBoard | gold with nothing to buy; taking on work |

**Rules every recharge room follows — and why:**

- **NO ENEMIES.** Nothing is at stake. It is a rest stop.
- ⚠️ **FLAT FROM SPAWN TO EXIT: ZERO MANDATORY JUMPS.** The room gives Shift back; taxing a jump
  to reach the service or the door works against the room. The first Well draft had a 2+2 rise to
  the exit — that is 2 Shift charged against an 8-Shift payout. Elevation comes only from a spawn
  ledge you **drop** off (drops are free).
- **ONE JOB PER ROOM.** The Well is the well and nothing else — no crystals, no chests, no NPCs.
  A room that fixes every problem is never a decision. ⚠️ **The shop still sells a `+3 Shift` and
  a heal service** (`ShopManager.shiftAmount` / `healAmount`), which overlaps the Well's whole
  identity. Left in place deliberately this session; removing them is the obvious next step once
  the Well has been playtested.
- **`!recharge: on` in the `.txt` is what makes it a recharge room.** The importer stamps
  `RechargeRoomMarker` on the root (same shape as `HubMarker`), and `LevelManager` exposes
  `IsCurrentRoomRecharge()`, `IsCurrentRoomCombat()` (= not hub, not recharge) and
  **`IsCurrentRoomSandbox()`** (= hub OR recharge).
- ⚠️ **THEY ARE SANDBOXES, LIKE THE HUB (designer, same day: "they should not waste anything").**
  Every umbrella-rule consumption site — jump Shift, card Shift and charges, Recall, Grapnel,
  Stagger, altars, blessing payouts, Dead Weight's room-end payout — now gates on
  `IsCurrentRoomSandbox()`. Verified: a jump in the Well costs 0 Shift. `RelicManager.OnRoomStart`
  additionally skips recharge rooms (its per-room gifts already paid on the combat room).
- ⚠️ **THE MAP DOES NOT OPEN BETWEEN A COMBAT ROOM AND ITS RECHARGE ROOM.** `AdvanceToNextRoom`
  spawns straight through when `pendingRecharge` is set and its prefab exists: the recharge room
  is next whatever the player picks, so asking was a lie (reported as "pick a node, then walk into
  a Well instead"). The map opens on the recharge room's exit, where the choice is real. Verified
  through the real `ExitDoor` path both ways.

⚠️ **WITHOUT THE MARKER, EVERY RECHARGE ROOM WAS A SILENT EXPLOIT.** `ExitDoor.PerformExit` pays
three things on leaving "a room", all previously gated only on `IsCurrentRoomHub()`: a **flawless
clear** (achievement + `NoDamageRoom` quest), a **step on every per-room oath** (`EndRoom`), and
**Nest Egg's +2 max Shift** (`ScoreRoomRelics`). A room with no enemies passes all three for free.
All three now gate on `IsCurrentRoomCombat()`. `RelicManager.OnRoomStart` (Pocket Battery / Flux
Regulator / Second Wind) also skips recharge rooms — the node's combat room already paid them.
Verified: `QuestSystem.roomActive` stays `true` through a recharge-room exit (EndRoom not called).

**`RestWell` (`Assets/Scripts/RestWell.cs`, `Assets/Prefabs/RestWell.prefab`)** — IInteractable on
layer 12 with a trigger, `InteractPrompt` child, the Cainos `PF Village Props - Well` as the
visual. `healAmount` / `shiftAmount` are Inspector fields. The mouth sits at **y 1.0** (the stone
rim; the first placement at 1.94 was rope height).

⚠️ **THE FIRST DRINK WAS REJECTED AS "BLAND AND NOT VERY IMMERSIVE", and the reason generalises:
nothing HAPPENED.** A label changed and sixteen dots floated. The rebuild stages it — DRAW (the
light dims, the glow contracts, loose motes are sucked back IN) → SURGE (a column of light, ripple
rings across the surface, the light flares room-wide, a small shake) → RECEIVE (a fountain of motes
arcs into the player; HP and Shift land in **8 ticks** so the bars visibly FILL; an aura blooms
behind the rig) → SETTLE (light sinks to a spent ember). Two procedural clips (`ProcSfx.WellDraw`
/ `WellSurge`, the altar's voice inverted) so it is never silent; `drinkSound` is an override.

⚠️ **A REAL `Light2D` IS THE BIGGEST LEVER, AND THE PLAYER RIG IGNORES IT.** Verified: a point
Light2D lights the well sprite, the floor tiles and the back wall (`Sprite-Lit-Default`), but the
Cainos Customizable Pixel Character shaders take no 2D light at all. So the WORLD reacts through
the light and the PLAYER is reached with sprites (motes, rings, an aura parented behind the rig at
local z +0.15, which the opaque rig occludes so it reads as light around them). Nothing else in
the project casts a local light yet — the Cainos props carry 3D `Light`s, which the URP 2D
renderer ignores. This is the first, and it is cheap: reach for it before another particle.

⚠️ **The camera in a 14-tall generated room always shows ~2 tiles of black under the floor**: the
importer's zone is `grid + 4` with a 20-tall minimum, so the 14-unit view is always clamped 2
past the frame. Pre-existing for every generated room; noted here because a flat rest stop makes
it more visible than a tall shaft does. Not fixed this session.

### Level Text Importer (NEW 2026-07-13 — Stage 1)

`Assets/Scripts/Editor/LevelTextImporter.cs` adds menu **Deckshift → Import Level From Text…**: it reads an ASCII grid `.txt` (legend + example: `Assets/LevelTexts/TestRoom1.txt`) and builds a room prefab into `Assets/LevelGenerated/` satisfying the room contract (`CameraBounds` zone auto-sized to the grid, `GirisNoktasi` spawn, ExitDoor). Markers: `#` ground, `S` spawn (exactly one), `X` exit, `m/r/l/M/b` enemies plus the zombie tiers `z` Shambler / `Z` Rotbrute / `s` Spitter (added 2026-07-16; `b` = `YeniLeveller/BatMan.prefab` — the real flying bat with AeroBatAI; **`Assets/Prefabs/AeroBat.prefab` is a legacy husk with NO AI**, its dead missing-script component was removed 2026-07-13 because Unity refuses to save any new prefab containing missing scripts, which broke level import), `^/T/W` hazards, `+/g/C` pickups, and mechanics (added 2026-07-13): `E` Elevator (Cainos prop, floats at cell center — tune travel in Inspector), `F` UpdraftFan (draft zone ~3 tall, liftForce 20 ≈ 5-7 tiles of lift — chain fans as relays for taller climbs), `w` AcidWater (~6 wide pool, damage+slow), `K` WreckingBall (floats at cell center, tune anchor/swing), `c` CrumblingPlatform (**do NOT use in levels — its sprites are outdated; use `T` Trapdoor instead, designer 2026-07-14**), `t` Taret turret, `$` Shopkeeper_NPC, `B` Blompo (`Assets/Prefabs/Blompo.prefab`, added 2026-08-08 — NPCs are loot, see below), and the recharge-room furniture added 2026-09-14: `f` ScrapForge, `Q` QuestBoardPrefab, `H` RestWell (all grounded; normally only in their own recharge room — see "Recharge Rooms") (its TMP/UI scripts live in Library/PackageCache — an Assets-only guid scan wrongly flags them "missing").

**Interactive structure markers (2026-07-14):** `=` one-way platform tiles (own tilemap: TilemapCollider2D via CompositeCollider2D + one-way PlatformEffector2D on Ground layer; painted with the thin `_144` lip so they read differently from solid strips) · `G` gate cells (vertical G-runs become one sliding **Gate** — `Assets/Scripts/Gate.cs`, solid Ground-layer collider, slides down + fades on Open, Cainos Gate 01 sprite scaled to height — see Doors below) · `L` Lever (`YeniLeveller/Lever.prefab`; its `OnFlippedOn/Off` UnityEvents are now public) · `A` **Shift Altar** (**`Assets/YeniLeveller/ShiftAltar.prefab`** since 2026-08-09 — it used to be assembled inline by the importer, so its sprite/layer/collider were declared in editor code and existed nowhere you could look at or tweak. `Assets/Scripts/ShiftAltar.cs`: IInteractable on the Interactable layer (12), pays `shiftCost` Shift via `player.SpendShift`, free in hub per the umbrella rule, procedural floating TMP cost label, fires public `OnPaid`). ⚠️ **It is deliberately NOT in `MarkerPrefabs`** — the `'A'` branch still needs its own code path because it collects altars for the gate wiring below; it just instantiates the prefab now instead of building one. **The importer auto-wires each `L` and `A` to its NEAREST `G` gate** (lever On→Open/Off→Close, altar OnPaid→Open) via `UnityEventTools.AddPersistentListener` — rewire in Inspector if a level needs different pairing. Header directives are `!name`, `!backwall` and `!recharge: on` (stamps `RechargeRoomMarker`). The importer pre-checks for missing scripts before saving and names the culprit object.

**Tile painting reproduces the hand-built visual language** (learned by auditing EfeVrl7's 546 painted tiles, 2026-07-13): a "BackWall" backdrop tilemap (⚠️ **it now defaults to ON and `!backwall: off` turns it off** — this file said "opt-in via `!backwall: on`" until 2026-08-22 and that was stale: the default flipped once the importer started assembling the real seamless 64-piece wall via `BackWallIndex`, since before that a generated backdrop was six scattered fragments and worse than nothing. Verified in `LevelTextImporter.cs` ~L842. It must be on the **"Background" sorting LAYER**, NOT Default: ExitDoor's sprite is Default order -1 and gets swallowed by a Default-layer backdrop), plus a "Ground" tilemap (layer 3, TilemapCollider2D, Default sortingOrder 1, z=1). Any 1-tile-thick run (air above AND below, wall-attached or floating) gets the `_112/_113/_114` strip treatment with caps on open ends; the gappy `_186` fill goes in exactly ONE row under a surface, deeper cells get dark `_185` (repeating `_186` looks like a broken colonnade). Frame cells (`#` connected to the grid edge) get role tiles from `Assets/LevelSinasi/biseyler/`: air-above → floor surface `_144`, air-below → ceiling face `_96`, wall faces → inner accent tiles `_188`/`_157` ONLY when backed by a real solid tile (2-thick walls), else the clean outer tiles `_189`/`_156` (the inner tiles have protruding brick nubs + bumpy collision — wrong for 1-thick walls), buried → `_153/_154` top rows, `_156/_189` outer walls, `_186/_185` floor fill. Free-standing `#` platforms: horizontal runs of 2+ get the **platform strip set `Extra_112/_113/_114`** (left cap / middle / right cap — learned from EfeVrl6's interior platforms); lone blocks and 1-wide pillars get chunky `Ground Dirt` block tiles (`#..#..#` = the hand-made stepping-stone style); buried rows of thick platforms get floor fill. NOTE: the edge-strip tiles look like sparse floating crumbs if painted in mid-air, and adjacent Dirt blocks melt into dark blobs — never tile either as strips.

### Sprite-less tiles are the designer's ERASER — do not "fix" them (2026-08-09)

Several tiles in the pack have a **null sprite** (`Ground_13`, `Ground Dirt_31`, `Ground_25`, `Ground Dirt_15` — indices past the end of the sheet). **The designer paints these deliberately to blank a cell**, because it is quicker than switching to the erase tool. Verified by clean-room physics test: `colliderType = Sprite` + null sprite ⇒ no outline to trace ⇒ **draws nothing and collides with nothing**. The technique is safe.

⚠️ **Do not report these as broken tiles.** A scan of the hand-made rooms found 218 such cells (98 in `efeslevel2`, 116 in `efeslevel3`) and they were briefly misdiagnosed as invisible platforms. They are intentional erasures.

⚠️ **But the safety rests entirely on them never getting Grid collision.** A Grid copy of a sprite-less tile is a full-cell collider that renders nothing — an **invisible wall everywhere the designer erased**. Not hypothetical: `Ground_13` was in `MaskTiles` until 2026-08-08 and did exactly that in generated rooms. `TileVariantGenerator` now refuses to build a `Solid` variant of any null-sprite tile, and `LevelTextImporter` already throws if a table tile has no sprite. **Keep both guards.**

⚠️ **Tooling reads an eraser cell as SOLID**, because `Tilemap.GetTile()` returns non-null for it. This contaminated the 8-neighbour mask measurement taken from the hand-made rooms — some configurations counted visually empty cells as solid neighbours, which is part of why the rare corner masks looked erratic. Any future measurement over their tilemaps must skip tiles whose `sprite == null`.

⚠️ **TEN OF THE GROUND TILES ARE BIGGER THAN THEIR CELL — NEVER FORCE GRID COLLISION ON THEM (2026-08-08).** The Cainos ground palette is **not a set of 1×1 blocks**; it is a set of whole pre-drawn **platforms**, each centred on its cell: `Ground_11` **3×1**, `Ground Dirt_0` and `Ground_0` **3×3**, `Ground_1` **2×2**, `Ground Dirt_12` 3×1.6, `Ground Dirt_14` 3×1.3, `Ground Dirt_4` 1.4×2, `Ground_3`/`Ground Dirt_3` 2×1, `Ground Dirt_11` 1.3×1.3.

Grid collision is exactly one cell, so a Grid copy of a 3×1 platform strip **keeps drawing the outer two thirds while deleting their collision** — the player sees platform, steps on it, and drops straight through. That was the "the edges of the mid-air platforms have no colliders" report, and it was self-inflicted: the earlier protruding-brick-nub fix generated Grid `… Solid` variants for *every* painted tile. **The hand-made rooms use `Sprite` collision throughout, which is exactly why their platforms have always felt right — what is drawn is what is solid.**

`TileVariantGenerator` now **skips oversized tiles** when building `Solid` variants (`IsOversized`), so they keep native Sprite collision while cell-sized tiles still get the nub fix. Verified by probing `Physics2D.OverlapPoint` across a platform's drawn width: solid over the full 3-unit art, empty outside. **Before adding any tile to the painting tables, check its sprite size against the cell.**

⚠️ **JUDGE THE MASK TABLE BY SAMPLE COUNT, NEVER BY WINNER SHARE (trimmed 2026-08-08).** `MaskTiles` was measured by taking the tile the designer used most for each 8-neighbour configuration — but some configurations appear only a handful of times across all six hand-made rooms, so their "winner" is a coin flip. And the coin flips are precisely the **outer-corner** configurations: a room has hundreds of buried and wall-face cells and only a few of any given corner. Symptom: mask 193 (a wall's bottom-right outer corner) resolved to `Ground Extra_205` on **2 votes out of 8**, and Extra_205 is a brown interior-looking block — so every generated wall had a brown nub sticking out of that corner. Entries with **n < 10 are now dropped**, falling through to the hand-written, internally consistent `Mask4Tiles`. **Do not trim on winner share:** it is low almost everywhere (mask 255 has n=1231 and its winner takes 12%) because the designer deliberately varies tiles across a mass — that is variety, not uncertainty, and trimming on share would gut the table.

⚠️ **NPCs COUNT AS LOOT when populating a room (designer 2026-08-08).** A Blompo (`B`) or a shopkeeper (`$`) is a place to *spend* what the player picked up, so it pays a room out just as a chest does — and arguably better, since it converts carried gold into something kept. **Chests are the expensive way to reward a room and a pile of them reads as filler**; ~3 is a sensible ceiling even for an Elite. Shift crystals are the exception — the designer considers those genuinely needed, so don't thin them out.

⚠️ **A fitted prefab's COLLIDER is often not centred on its transform (2026-08-08).** `FitAcidToPit`
scaled the pool by its `BoxCollider2D.size` but then positioned the *transform* at the pit centre, as
if the collider sat on the origin. `AcidWater`'s collider carries `offset (0, 1.27)`, so every pool in
every generated room floated a scaled 1.27 units too high — the water's surface sat a full tile above
the floor it was supposed to be sunk into. It imported without a single warning and looked *almost*
right, which is why it survived two rooms. Fixed by backing the scaled offset out of the position.
**Whenever you size or place a prefab from its collider, read `offset` as well as `size`** — and
verify the result by asking for the instance's world bounds, not by eyeballing the transform value.

⚠️ **`T` Trapdoor grounds to the BOTTOM of its cell, like an enemy standing there.** Used as a bridge
across a pit that is what you want one row *above* the pit mouth: place the marker on the standing row
(the row the surrounding floor's occupants use), not in the gap itself, or the planks end up a tile
down inside the hole — under the acid, invisible.

**Entity placement:** most enemies have kinematic physics and do NOT fall, so the importer auto-grounds standing markers (`X m r l M C ^ W T` + the spawn): after instantiating, it measures the instance's combined renderer bounds (ignoring particles/trails, collider fallback) and shifts it so bounds-bottom sits exactly on the cell floor. Floaty pickups (`+ g`) and flyers (`b`) stay at cell center. Decoration (props) stays a manual pass by design. Planned next stages: movement-metrics doc (jump/dash distances in tiles) then batch room drafting.

### ⚠️ SHIFT SUPPLY IS A MEASURABLE ROOM PROPERTY: ~7 Shift per 1000 tiles (2026-08-14)

The designer reported the generated rooms as far harsher than the hand-made ones and punishing on a missed jump. Measured, it was not a feel problem — it was a **4.5× supply gap that compounds with room size**:

| | shift | area (tiles) | per 1000 |
|---|---|---|---|
| hand-made average | 6.9 | 1090 | **6.3** |
| GenLevel7 | 3 | 2584 | 1.2 |
| GenLevel8 | 3 | 2880 | 1.0 |
| GenLevel9 | 7 | 2772 | 2.5 |
| GenLevel10 | 2 | 2520 | **0.8** |

The generated rooms are **~2.5× larger AND paid half as much**, so per unit of traversal GenLevel10 was **nine times stingier** than efeslevel1. All four are now stocked to **6.9–7.1 per 1000 tiles** (targeting the most generous hand-made rooms, not the average, because the bigger layouts demand more traversal). Total Shift across a 10-room run went **63 → 123**.

**Room size is staying big — the designer likes the large layouts, so the lever is supply, not size.** When authoring a new room, check crystals against area; the hand-made band is 5.1–7.7 per 1000.

⚠️ **The gold/crystal split still holds:** gold piles must be GROUNDED, Shift crystals floating is correct and wanted.

### ⚠️ THREE PLATFORM VARIANTS DO NOT FILL THEIR FOOTPRINT — removed 2026-08-14

Measured by stamping each multi-cell variant on a bare tilemap and probing every cell with `Physics2D.OverlapPoint`:

```
Ground Dirt_10   2x2   1 of 4 cells solid
Ground_6         3x3   8 of 9
Ground Dirt_6    3x3   8 of 9
```

They are oversized, so `TileVariantGenerator` correctly refuses them full-cell Grid collision and they keep `colliderType = Sprite` — which traces the **alpha outline**. These three are drawn as irregular rounded rocks rather than filled blocks, so the corners simply are not there. **A platform stamped with one looks solid and is not.** All three are gone from `PlatformShapes`; the other 17 variants measured complete.

⚠️ **Being the right pixel size is NOT evidence.** `Ground Dirt_10` measures **2.06 × 2.03**, which looks perfect. Any new variant added to `PlatformShapes` must be probed, not eyeballed.

⚠️ **`Ground Dirt_13 Solid` was the same class of bug and is also gone** — a 0.44 × 0.38 pebble carrying FULL-CELL Grid collision, so the player stood **0.62 units above a pebble**. Every instance sat at the outer edge of a floor run, i.e. exactly the surface you walk onto. This is very likely the designer's "2-3 tiles where the colliders are off and the player seems to float".

### Doors: the gate's LEAVES OPEN, the exit is an OPEN ARCHWAY (gate rebuilt again 2026-08-20)

The mid-level **gate** was a closed thing wearing the grandest door in the pack, while the **exit** —
the single most important thing in a room — was a murky sprite from a different art pack you could
barely find on the wall. Both were wrong, and in opposite directions.

| | before | after |
|---|---|---|
| **Gate** (`G`) | `Gate 01` sprite, slid the whole archway into the floor and faded | `Gate 01` art kept, but cut into **arch + two leaves**: the arch stays put and the **doors open** |
| **ExitDoor** | `main_lev_build_110` (PlatformerSet1), blurry and squashed | **open stone archway**: `Door Frame 01 A` + `Door Wood Inside 01` |

⚠️ **`TX Dungeon Props - Gate 01` IS NOT A PORTCULLIS.** It is a stone archway with a pair of solid
wooden **double doors** hung inside it, ring handles and all. Every earlier design here treated it as
a slab and slid it somewhere, which is why it kept looking wrong — you were watching an entire
masonry arch sink into the ground. **A double door opens.** See "The gate opens like a door" below.

⚠️ **THE EXIT HAS NO DOOR IN IT, AND THAT IS THE DESIGN.** You walk *through* an exit, so anything
that reads as "closed" is lying about what it does. `Door Wood Inside 01` is the passage texture that
normally sits BEHIND a wooden door — dark brick with a short flight of **steps rising into it** — so
the exit reads as a way onward rather than a barrier.

⚠️ **`Door Wood Inside 01` EXISTS ONLY AS A SPRITE. There is no prefab for it** (unlike almost every
other prop in the pack), which is why it does not turn up when you search the Prefab/Props folder. It
is in `TX Dungeon Props.png`, 36×64px, base-pivoted.

⚠️ **Two candidates were built, shown and REJECTED before this one — do not re-propose them.**
`Gate 01`'s grand double doors: still a *closed door*, and its own `Sky`/`Light Shaft` children are
invisible behind its opaque leaf, so the prefab buys nothing over the bare sprite. `Door Iron Fence
01`: bars over a bright cyan sky — loud and eye-catching, but bars read as *blocked*, and the cyan is
a brand-new hue in a palette that is already nearly spent (see UI System). The exit deliberately
introduces **no new colour at all**.

**It is deliberately unlit.** A warm `Light2D` in the archway was tried and left out: the designer
picked this texture on its own merits, and the exit's job is to be a believable part of the room. If
it proves hard to find in a big room, a low warm point light in the passage is the one-line change —
but that is a play-test call, not an assumption.

⚠️ **THE GATE ENDED UP EXACTLY WHERE IT STARTED, AND THAT IS THE POINT.** It was rebuilt as a hinged
`PF Dungeon Props - Door Wood 01` that swung open on the pack's own Animator, and the designer then
reverted it the same day — because the reason to move the gate off `Gate 01` was to free that sprite
for the exit, and the exit does not want it either (it wants an OPEN archway). With the exit sorted,
the gate's original art was never the problem. **Do not "fix" the gate again without a reason that is
about the gate itself.** All 13 gates were restored to byte-identical values (h=3 ⇒ scale 0.623,
localPosition (-0.010, -1.500)) rather than re-picked by eye.

⚠️ **THE SWING BRANCH IS GONE.** An older `Gate.cs` carried two movements and chose between them by
looking for a Cainos `Door` component among its children. The 2026-08-19 rebuild deleted that, and the
2026-08-20 rebuild replaced the survivor. There is now **one** movement (the leaves opening) and no
`Door` component anywhere. Earlier versions of this file described the two-branch design as current;
they were stale.

If a hinged Cainos door prefab is ever tried again, three findings from that attempt still cost a
session each: size a door by its **combined** renderer bounds, not the leaf (the Frame is the tallest
piece at 2.41 units); **exclude particle Light Shafts** from that measurement (`Door Iron Fence 01`'s
glow reaches below the frame and lifts the door ~1 unit off the floor); and the pack's
`AM Door Wood 01 - Closed` clip keys a sprite *named* `Door Wood Side 01 - 0` — a Cainos naming slip,
the art is correct, **do not chase it.**

#### The gate opens like a door (rebuilt 2026-08-20)

The designer reported that the gate "goes under the floor after it is opened, which makes it so the
barricade is still below — does not make sense", and asked for it to be **completely gone** when the
lever or altar fires. Two separate faults, and the second was never documented before:

⚠️ **1. IT SANK THE MASONRY.** The art is an arch with double doors in it (above). Now the arch stays
bolted in the wall and the two **leaves** open, each narrowing toward its own hinge — which is exactly
how this pack draws its own doors: `Door Wood 01` runs **37px wide down to 11px at a constant height**.

⚠️ **2. THE COLLIDER NEVER MOVED — this was the actual bug.** Nothing in the old file ever touched the
`BoxCollider2D`. Opening only translated the transform, so the solid box travelled with it and came to
rest *below the floor*. Measured in GenLevel8: closed it spans y 21→24, open y 18→21, while the floor
tile is only y 20→21 thick. **That left an invisible 1×2 wall standing in playable space under the
floor, in every room with a gate.** The collider is now disabled on open.

⚠️ **Opening drops the collider FIRST; closing restores it LAST.** The passage must never be solid at a
moment the doors visibly are not. The reverse ordering lets a player be stopped by an open doorway, or
sealed inside a door still swinging shut.

**The art is cut by `Editor/GateArtBaker` (Deckshift → Bake Gate Art)** into four pieces —
`gate01_arch` (masonry with the opening punched out), `gate01_passage` (the dark beyond),
`gate01_leafL` / `gate01_leafR` (pivoted on their hinges) — plus a **`GateArt` asset carrying the
placement offsets**, all in `Assets/Resources/GateArt/`.

⚠️ **The offsets are baked BESIDE the sprites, never hardcoded in `Gate.cs`.** The leaves are cropped
to their own bounds so they can pivot on their hinges, so their placement depends on where the cut
landed — which the baker decides by reading the artwork. Hardcoding it fails silently as a door
hanging a few pixels out of its frame the next time the art is re-cut.

⚠️ **`Gate.cs` re-dresses the importer's single sprite AT RUNTIME**, so **no room prefab needed
re-importing.** That is deliberate: GenLevel7/8/9 carry hand edits a re-import would destroy, and a
re-import also renumbers every fileID out of `LevelManager.roomPrefabs`.

⚠️ **The opening is found by walking inward from the silhouette through the masonry until it hits
wood** (warm red-vs-blue), with a run threshold — the stone carries warm *highlights*, so a single
warm pixel means nothing. Two traps: a "longest wood run" test breaks on the iron bands crossing the
doors, and the arch's **keystone is warm-toned stone**, so without a vertical-contiguity filter the
baker punches a hole through the crown and hands the leaves a slice of masonry.

#### ⚠️ The animation: THE GATE IS SEALED WITH SHIFT (rebuilt again 2026-08-20)

**The first swing animation was competent and the designer rejected it: "not great … it just doesn't
fit in."** It didn't, and the fault was not the timing curve — it was that the gate was animated as a
REALISTIC MEDIEVAL DOOR (groan, strain, brown dust, ~1.6 seconds) **in a game where doors are opened
by magic**. The `ShiftAltar` rips motes of Shift out of the air, absorbs them, flashes, and fires a
glowing **cyan orb that flies across the room and bursts on the gate** — and the gate answered that
with carpentry. Cause and effect were in two different genres, and it spent 1.6 seconds of a game
whose whole thesis is momentum.

**The fix was to make the gate speak the game's own language:**

1. **A CLOSED GATE IS VISIBLY SEALED.** A hairline of Shift-cyan light breathes in the seam between
   the two leaves — the *same* colour as the altar's orb, deliberately. This is information the gate
   never gave before: it says "locked, and Shift is what locks it", which is what sends a player
   looking for the altar or lever. `RestGlow` 0.12, breathing ±30% at 1.5 rad/s.
2. **OPENING IS AN EVENT, NOT A PROCESS.** BREAK (the seal flares past full and shatters) → a held
   BEAT of stillness → THROW (the doors are flung, ease-OUT because they were *released* not pushed,
   overshooting to 1.06) → REBOUND off the jambs. **~0.52s, down from ~1.6s.**
3. **THE PARTICLES INVERT.** Breaking sheds cyan motes OUTWARD from the seam; re-sealing draws them
   back INWARD and the hairline re-ignites. Stone grit survives only where a leaf actually strikes
   stone (the jambs on opening, the seam on the slam). The out/in inversion is the same trick that
   fixed Blompo's forging→binding rebuild.

⚠️ **DO NOT re-add the groaning strain sequence, brown dust as the primary particle, or a
multi-second open.** Each was tried and each is what made it read as the wrong game.

⚠️ **Closing is deliberately NOT a mirror** — it accelerates the whole way into a single slam as the
leaves meet, then the seal knits back. Opening ends softly at the jambs, closing ends loudly in the
middle, so the two are distinguishable with your eyes shut.

⚠️ **SCALE A PROCEDURAL SPRITE *TO* A SIZE, NEVER *BY* IT.** The seam sprite is 32×128px at PPU 32,
i.e. natively **1 × 4 world units** — multiplying its scale by the 4-unit opening height made it 16
units tall and the seal rendered as a line running off the top and bottom of the screen. Always divide
the desired size by `sprite.bounds.size`; never assume a generated sprite is 1×1.

⚠️ **Spawn particles on a TIME ACCUMULATOR, not a per-frame probability.** `if (Random.value < 0.5f)`
inside the throw loop is framerate-dependent — twice as many at 120fps as at 60 — and in slow motion
it runs away completely: **measured 109 live sparks at `timeScale` 0.05 where normal speed makes ~8.**

⚠️ **The layer stack goes UP from the sprite's original sorting order, never down.** The Ground tilemap
draws at Default order 1 and the gate art is wider than the 1-tile gap it stands in, so in a room where
geometry flanks the opening a passage at order 0 is swallowed by the floor tiles either side.

⚠️ **A gate may carry MORE THAN ONE visual.** GenLevel9 shipped with two identical `Visual` children
stacked exactly (same sprite, position, scale, order) — the same duplicate-prop shape as the nested
`ExitDoor`. Only the first is re-dressed, so the survivor draws a **closed** gate over the open one and
the lever looks broken. The prefab is fixed and `Gate.cs` now disables and warns about any future
duplicate rather than failing silently.

⚠️ **The shudder must anchor to a position cached ONCE, not read the live transform.**
`StopCoroutine` can cut it off mid-jitter, so each interruption adopted the leftover offset as its new
rest pose: measured **0.002 units of permanent drift per interruption**, accumulating silently.

Two calibration values, both measured on screen rather than computed (linear colour space, and world
sprites render through the scene's 0.5-intensity global `Light2D`): the **passage** started at
0.085/0.045 and read as a pure black hole, and the **leaf shading** at 0.52 fell to roughly the value
of that passage so the doors stopped reading as wood. Now 0.24/0.12 and 0.74.

If the gate's look is ever revisited, `Door Iron Fence 01` (a barred portcullis) was built and compared
and is the strongest alternative — bars are honest for a thing you cannot pass, and it is the only
candidate you can see through. Its cost is a bright cyan sky panel, a new hue in an almost-spent
palette.

#### The gate's movement, rebuilt from scratch (2026-08-19) — SUPERSEDED, kept for its lessons

⚠️ **The gate no longer slides, so THE `SpriteMask` MACHINERY BELOW IS GONE** — there is no mask, no
slot, and no alpha fade in `Gate.cs` any more. Read this section for *why* those choices were made,
not as a description of the code. What still holds and is still live: the silence diagnosis, the
one-row-of-floor measurement (it is why sinking could never work), "a constant rate reads as a lift",
and the `CameraShake.Shake(INTENSITY, DURATION)` argument order.

The designer called the old animation "really lackluster and quite honestly bad". Diagnosed rather
than guessed at, it had **three** separate faults:

⚠️ **1. IT WAS SILENT. All 13 gates had `moveSound` unassigned**, so a three-tonne slab dropped into
the floor and made no noise at all. That was most of the problem, and no amount of motion tuning
would have fixed it. There are now four procedural clips — see ProcSfx → GATE.

⚠️ **2. IT FADED OUT, because it had to.** The gate sprite draws at Default order **2** while the
Ground tilemap is order **1**, so it renders *over* the floor; without the fade you would watch a
stone slab slide down across the floor tiles. But a fade reads as *dissolving*, which is the exact
opposite of heavy. It is now **clipped by a `SpriteMask` at the floor line** and stays fully opaque
(`alpha == 1` throughout, verified) — it genuinely disappears into the floor.

  ⚠️ **Masking by the FLOOR TILEMAP was tried first and does not work: there is only ONE row of
  ground tile under the gate.** Measured in GenLevel8 — y=20 is solid, y=19/18/17 are empty backdrop.
  A 3-tall gate sinking 3 units would hang in open air below the floor, which is precisely why the
  original fade existed. The mask is the fix; re-ordering the sprite is not.

  ⚠️ **The mask is a SIBLING (parented to the room), never a child.** It is the *slot* — it belongs
  to the floor and must not travel with the gate. Parenting it to the room also means the room
  destroys it, so it cannot outlive the level (the class of bug `ClearRuntimeSpawns` exists for).

  ⚠️ **It is only as WIDE as the gate, and that is load-bearing. Sprite masks ACCUMULATE** — a
  renderer draws wherever *any* mask covers it, so one screen-wide mask would un-hide a second gate
  sunk in its own slot elsewhere in the room. Measured across every multi-gate room, the closest two
  gates are **8 units** apart, so a 3.48-wide local mask can never reach a neighbour.

⚠️ **3. IT MOVED AT A CONSTANT RATE.** Five equal steps at equal spacing reads as a lift, not as a
falling weight. The descent now accelerates on `k*k` (what gravity actually does) and the ratchet
catches are spaced by **distance**, so they arrive faster and faster as it picks up speed.

**The sequence is STRAIN → CATCH → DROP → SEAT, and `CatchHold` — a beat of complete stillness
before it gives — is doing more work than any other single value in the file.** Weight is
communicated by the pause *before* the movement, not by the movement. Closing is the inverse and
deliberately slower (`HeaveTime` 1.05s vs `DropTime` 0.72s), easing *out* because it is being winched
against its own weight, with the ratchet pitch falling as it slows where the drop's rises.

⚠️ **`CameraShake.Shake` is `(INTENSITY, DURATION)` and the old gate passed them REVERSED.** Every
other caller in the project has it right (boss death is `0.6, 1.6`). The old gate's hardest hit asked
for 0.12 intensity over 0.14s while the Moss Knight's slam gets 0.28 over 0.8s — an order of
magnitude under every other impact in the game, which is its own reason a falling slab registered as
nothing. The seat is now `0.34, 0.60`.

**Verified in play mode:** settles to exactly y=22.500 closed and 19.500 open; alpha stays 1.00
throughout; when open the sprite's top edge lands at exactly the floor line (21.00) so it is entirely
clipped; a hammered Open/Close/Open/Close settles correctly with the collider back on; dust motes
drain to 0 rather than growing unbounded; and exactly one `SpriteMask` exists per gate.

⚠️ **Testing this needs the clock slowed.** The whole sequence is ~1.1s, which is shorter than the
round-trip of a single MCP call — at `Time.timeScale = 0.12` it still finished between two calls.
0.02 is what actually lets you photograph the middle of it.

#### ⚠️ `ExitDoor.prefab` CONTAINED A NESTED COPY OF ITSELF — in 37 of 39 rooms

Found while swapping the sprite. This is the deferred "duplicate ExitDoor possible in some room
prefabs" item, which badly understated it: the duplicate was baked into the shared prefab, so nearly
every room had it. The root had a **child also called `ExitDoor`** carrying its own `BoxCollider2D`
(trigger, enabled), its own `SpriteRenderer` (same sprite, same sorting order — so the door z-fought
with itself and its transparent parts double-composited, a large part of why it looked so murky), and
its own **`ExitDoor` script**.

Both scripts polled `E` and both had the player in range, so one keypress ran `PerformExit()` twice:
`ReportEvent(NoDamageRoom)` twice, `QuestSystem.EndRoom()` twice (double-counting oath streaks) and
**`LevelManager.AdvanceToNextRoom()` twice**. Each instance has its own `hasBeenTriggered`, so that
guard did not help. The two even pointed at different popups — root at `InteractPrompt`, child at a
legacy `Canvas` — which is the fingerprint of an old version left parented under the new one.

Deleted. Verified first that **zero** room instances carried any override on the nested child, and
that all 38 room ExitDoors are linked instances of the shared prefab, so one edit propagated — the
same mechanism as the `GirisNoktasi` door-Z fix.

⚠️ **Every Cainos prop is BOTTOM-pivoted; the old exit sprite was CENTRE-pivoted.** Dropping a new
sprite straight onto the root would have raised the door half its height in all 38 rooms. The art
therefore lives on a `Visual` child offset to `-DRAWN_H/2`, and the root's own SpriteRenderer was
removed. **Check `sprite.pivot` before swapping any sprite onto an existing transform.**

The prefab also carried a **non-uniform root scale (5.92, 7.75, 3.59)** applied to a **32×41 sprite at
PPU 100** — so the exit door was blown up ~2.5× (hence blurry next to crisp brick) *and* squashed 24%
horizontally. Root scale is now `(1,1,1)` with the collider expressed directly in world units
(2.00 × 3.37, unchanged), and the art sized so its **drawn height is identical to the old door's
3.18** — which is what let all 38 rooms keep their placement with nothing to reposition. Verified in
GenLevel8: the frame bottom still lands at exactly y = 37.00.

#### ⚠️ That root-scale change left TWO numbers behind, and both shipped (fixed 2026-08-20)

Reported as "the door prefab is kind of bugged … they are much bigger than they used to be, and the
prompt is much smaller". Both are the same leftover: two values had been tuned to cancel out the old
(5.92, 7.75, 3.59) root scale, and neither was reset when the root went back to (1,1,1).

- ⚠️ **`InteractPrompt.size` was `0.155` world units** — against **0.7** on ScrapForge/Blompo and 0.5
  on the Lever. The keycap rendered at **9% of the player's height**. It only ever looked right in the
  hub, because the leftover scale below happened to multiply it back up. Now **0.7**, and lifted to
  `y + 2.10` so it clears the 3.18-tall arch.
- ⚠️ **`hub.prefab`'s ExitDoor instance still carried scale (3.879, 5.009, 2.318).** Applied to the new
  archway art that made the hub's door **9.77 × 15.93** where every other room's is 2.52 × 3.18 —
  **9.5× the player's height instead of 1.9×**, with a 7.76 × 16.88 trigger. The hub is the first room
  of every run, so this was the door the player saw most.

**Reverted, not reassigned** (`PrefabUtility.RevertPropertyOverride`), so the instance tracks the
source prefab again — reassigning creates a PINNED override that silently stops following the prefab.
Position IS legitimately per-room, so that one is assigned: the hub floor measures **y = 10.651** by
raycast and the art sits 1.59 above its root, giving **12.241**.

⚠️ **Measure the hub floor by RAYCAST, not from the tilemap cells.** The hand-made rooms use *sprite*
collision, so the cell boundary is not the surface — the cells at x=40 suggest y=12, the real surface
is 10.651. And cast from *below* the mid-level platforms: a ray from y=34 hits a ledge at 28.651 and
never reaches the floor.

⚠️ **WORKFLOW TRAP, cost several wrong screenshots: a scratch scene can end up with TWO room
instances.** `GameObject.Find("ROOM")` returns only the first, so hiding "the" exit door hid one and
left an identical second one rendering — which showed up as mystery iron bars over compositions that
contained no bars, and made three comparison shots quietly worthless. When staging a visual
comparison, **destroy every matching root first and assert the count**, and if something appears on
screen that your code cannot draw, enumerate the live renderers near that position before theorising.

### Level Validator (2026-08-07) — run this BEFORE importing a level

`Assets/Scripts/Editor/LevelValidator.cs`, menu **Deckshift → Validate Level Text(s)**.

`LevelTextImporter`'s own validation only counts markers (one `S`, an `X`, unknown chars). Every one of the seven Level Design Laws was enforced by prose in a comment header, which demonstrably does not work. This makes them executable: it simulates the real player and flood-fills reachability from the spawn.

**`LevelValidator.Overlay(path)` is the tool to reach for when authoring** — it prints the room with `o` = reachable standing cell, `x` = standable but ORPHANED. It answers "where does the route actually stop?" directly, and it's how the validator itself gets checked.

⚠️ **The movement model constants are read from `PlayerController` + `Player.prefab`, not estimated. If jump/gravity/speed change in the game, change them here or the validator quietly starts lying.**

**Measured from the code 2026-08-07 (tile = 1 world unit), designer-confirmed by playtest:**
- **Jump apex ≈ 4.9 tiles.** Confirms Law #2 ("mandatory rises at 4, 5 is the edge").
- **Airtime ≈ 1.5s** (0.90s up at −12.26, 0.60s down at −26.98 thanks to `fallMultiplier`).
- **Flat jump reach ≈ 12 tiles** — simply `moveSpeed × airtime`. Still about **2× the "flat gaps ≤ 5-6 tiles"** the design laws assume, which is worth knowing when rooms play flat.

✅ **`PerformJump`'s horizontal impulse is GONE (2026-08-14), and the reason it had to go is worth keeping.** It used to do `AddForce(moveInput * jumpForce, jumpForce)`, which looked like a running jump should launch at 8 + 11 = 19 u/s. It didn't: `isGrounded` is assigned only in `Update()` and nothing clears it on jumping, so the very next `FixedUpdate` saw `isGrounded == true`, ran the grounded branch (`rb.linearVelocity = (moveInput * moveSpeed, y)`) and overwrote it back to 8 about 20ms later. Dead code — **on a grounded jump.**

⚠️ **COYOTE TIME REACHED THAT LANDMINE FROM THE OTHER SIDE.** A coyote jump fires while `isGrounded` is **false**, so FixedUpdate takes the AIR branch instead, which only lerps toward moveSpeed at ~7% per step — the impulse would have survived most of a second. Coyote jumps would have flown noticeably further than the ordinary jumps they're meant to be indistinguishable from, and **every gap in the game would have been clearable by deliberately stepping off the edge first.** The old warning here was about "fixing" the stale `isGrounded` read; that was only one of the two routes in.

Deleted outright rather than special-cased, which is safe because **`maxAirJumps` is 0** so the ground branch is `PerformJump`'s only caller. Verified: a coyote jump while running leaves horizontal velocity at **8.00, not 19**.

(An earlier version of this section claimed a 15-tile reach and a 3× discrepancy, from modelling that impulse as if it survived. It does not.)

**Modelling notes:** the player occupies 1 column × 2 rows. `Solid` (blocks) and `Support` (can land on) are deliberately split — one-way `=` platforms support from above but pass through from below, and treating them as non-support produced a false "exit unreachable" on GenLevel5.

### Tile appearance — what we CAN change (2026-08-07)

Verified, not assumed. The tilemaps render with **`Sprite-Lit-Default` (URP 2D lit)** and the scene has a global `Light2D` at **0.5 intensity**, so:

- **2D lights affect tiles.** Glowing platform edges are achievable with a Light2D, no art needed.
- **Tiles have a `color` field, but every pack tile ships with `TileFlags.LockColor`**, which makes `Tilemap.color` / `SetColor` no-ops on them. `TileVariantGenerator` (menu **Deckshift → Generate Tile Variants**) sidesteps this by writing DUPLICATE `Tile` assets pointing at the same sprite with their own colour — no shader work, no texture edits, no risk to hand-made rooms.
- **The textures are editable** — real 512×512 sheets (`TX Tileset - Dungeon Ground Extra.png`); `readable=False` is just an import setting to flip if pixel edits are ever wanted.

⚠️ **Tint darker than you think and you'll get a black hole.** These render through a 0.5-intensity light, so the scene already halves your value. A 0.42 deep-rock tint measured ~0.21 on screen and the mass read as a pit. Multiply by the light, *then* pick.

⚠️ **THE DEEP-ROCK INTERIOR IS SIGNED OFF. DO NOT "IMPROVE" IT (2026-08-08).** Two changes were made to it and both were reverted the same day at the designer's request: brightening `FlattenSprite` to pull toward the **mean** instead of the 25th percentile, and **jittering** the depth threshold to break up the Chebyshev metric's rectangular contours. Both are measurably more "correct" and the designer rejected both on sight — the interiors read better dark and hard-edged. The low percentile and the hard depth-3 step are deliberate. The jitter was actively harmful besides: a +1 nudge pushed **deep tiles out to depth 2**, one cell from the face, producing dark blocks stuck to wall edges.

⚠️ **DISTINGUISH "THE INTERIOR OF THE MASS" FROM "ONE TILE ON ITS OUTER EDGE".** The designer's complaint that "the corner tiles look really bad" was about a **single mask entry** picking a brown interior-looking tile at wall corners — not about the fill behind it. Reading it as the fill cost a full revert. When feedback points at a tile, find *that tile's* mask before changing anything global.

**Deep interiors are painted, not skipped.** An earlier pass left cells >2 from air unpainted so the backdrop showed through — that was worse, because solid rock then reads as open background and misleads the player, especially when peeking with Ctrl. They now get **darkened copies** of the interior tiles: same art, same collision, recessed value.

⚠️ **`TX Tileset - Dungeon Ground_13` is BROKEN — it has a NULL SPRITE.** The pack's valid range stops at `Ground_12`; `_13` is one past the end. It was the most-used tile in the measured platform-run data, so the importer painted every ledge with nothing and generated rooms genuinely had **invisible mid-air platforms** (30 of 499 cells). Replaced with `Ground_11`. **`LevelTextImporter` now fails the import if any table tile is missing OR has a null sprite** — a resolve-only check passes this happily, which is how it survived.

**First run found:** GenLevel3 is **unfinishable** — its header advertises a "zigzag staircase" that was never drawn into the ASCII, so the only route up is the fan relay, violating Law #1. GenLevel4/5 + ToyboxTest carry banned turrets/one-ways. GenLevel1–4 sit at 14–18% rock density (mostly empty void); the two that read as real rooms, GenLevel5 and GenLevel6, are both 67%.

### Room Pool

`LevelManager.roomPrefabs` holds the pool of room prefabs. **Element 0 must be the hub;** elements 1..n are the run's combat levels. Boss and recharge rooms are NOT in this list. Bosses live in **`bossRoomPrefabs`** (a list, drawn without repeats within a run) plus **`finalBossRoomPrefab`**, and the recharge rooms in `foundryRoomPrefab` / `marketRoomPrefab` / `wellRoomPrefab`. (The old single `bossRoomPrefab` slot no longer exists.) The played character's `CharacterData.bossRoom` overrides the finale and is filtered out of that run's mid-map boss draws.

**Verified pool contents (re-verified 2026-09-24):** `[0] hub, [1] efeslevel1, [2] efeslevel2, [3] efeslevel3, [4] EfeVrl4, [5] EfeVrl5, [6] EfeVrl6, [7] EfeVrl7, [8] GenLevel7, [9] GenLevel8, [10] GenLevel9, [11] GenLevel10`. Bosses: `bossRoomPrefabs = [BossRoom, NinjaArena, KagemushaHall]`, `finalBossRoomPrefab = BossRoom`. Recharge: `Foundry`, `Market`, `Well` (all in `Assets/LevelGenerated/`). So the run is **11 combat levels**. All satisfy the room contract (CameraBounds / GirisNoktasi / ExitDoor), and only `hub` has a `HubMarker`.

⚠️ **THIS LIST HAS BEEN WIPED FIVE TIMES (the fifth found 2026-09-06; see CLAUDE.md). THIS ENTRY RECORDS THE THIRD, WHICH SURVIVED A WHOLE SESSION.** On
2026-08-16 it was found holding a **single** entry — `herangibisi`, a scratch room saved into
`Assets/Cainos/Pixel Art Monster - Dungeon/Prefab/`, with **no `CameraBounds`** and no `HubMarker`.
Consequences, none of which announce themselves as a pool problem: there is **no hub** (so no sandbox
first room, no quest board, no forge), every room in the run is the same room, and because
`CameraBounds` is missing the camera **never clamps** — at 21:9 you see straight past the room's art
into undressed space. The only clue in the console is one Turkish line, `CameraBounds objesi
bulunamadı!`, which reads like ordinary noise.

It was introduced by commit `477c8b7` ("osbir", 2026-08-14) — the pool was 12 as recently as `4d80c8f`
— and the entire "characters" session ran on top of it without noticing. **Restored by resolving the
GUIDs recorded in `4d80c8f`**, so the list is byte-identical rather than re-picked by filename; the
`herangibisi` prefab was left on disk untouched. **When anything about the run feels wrong — no hub,
repeated rooms, a camera that shows the void — read this list before debugging the map or the camera.**

**GenLevel7/8/9 were brought up to the current rules IN PLACE (2026-08-14)** — never by re-import, for the reason immediately below. Four things had drifted, all found by auditing against GenLevel10 (the only generated room built under current rules):

1. **The backdrop was six tiles of a sixty-four piece wall.** `TX Tileable - Dungeon Wall` is one seamless 8×8 picture; the old importer held six pieces and scattered them randomly. **That is why generated rooms never looked like the hand-made ones.** Now 64/64, assembled via `BackWallIndex`. ⚠️ Only cells that ALREADY held a tile were rewritten — the designer erased backdrop tiles by hand in these rooms and filling every empty cell would silently undo that.
2. **`Ground Dirt_13 Solid`** floating-collider cells (14 of them). See the tile section above.
3. **Overlapping spikes** — 1.55 wide placed 1.00 apart. Re-spaced to GenLevel10's 1.67 pitch about each run's original centre. No spikes removed; the floor runs had room for their existing count all along.
4. **Every mid-air platform was `Ground_11` repeated per cell** (GenLevel8 had 86 cells of it and nothing else) — these rooms predate `StampPlatformShapes`. Re-stamped as decomposed whole shapes, plus 11 new **vertical** pieces (pillars, boxes, blocks) added additively so they cannot make an exit unreachable.

⚠️ **RE-STAMPING NARROWED THE PLATFORMS, and this is a real gameplay change.** `Ground_11` is 3 units of art on a 1-cell stamp with Sprite collision, so painting it per cell overlapped it three deep AND spilled past both ends. Measured on a 7-wide run: collision ran x=5.0–15.0 for cells 6..12 — a cell too far left, two too far right. It is now exactly 6.0–13.0, the run as drawn. The old width was a bug, but it is a bug those rooms were playtested with.

⚠️ **THE `.txt` IS NO LONGER THE SOURCE OF TRUTH FOR `GenLevel7/8/9` (2026-08-09).** The designer has hand-edited the built prefabs — moved loot, placed a Blompo, erased tiles. **Re-importing any of them from its text file DESTROYS that work**, and also renumbers every fileID so `LevelManager.roomPrefabs` loses its reference. Edit these rooms in the Unity editor, or if a text re-import is genuinely needed, diff the prefab first and re-apply the hand edits afterwards. `GenLevel8` has carried hand-tuning since 2026-08-08; 7 and 9 now do too.

**Tier tags (2026-08-08):** the three importer-built rooms carry `RoomTier` — `GenLevel7` **Fight** (horizontal corridor), `GenLevel8` **Fight** (vertical shaft), `GenLevel9` **Elite** (loop; the pool's first Elite room). The seven originals stay untagged and therefore serve every tier, so eligibility is **7 Skirmish / 9 Fight / 8 Elite**. Verified by driving the real `PickNextRoomPrefab`.

#### Room inventory — relevant to the planned map system (audited 2026-07-18)

**24 prefabs in the project satisfy the FULL room contract, but only 9 are wired into LevelManager.** That means ~15 contract-valid rooms are sitting unused:
- **`Assets/LevelGenerated/`** — `GenLevel1..9`, `TestRoom1`, `ToyboxTest`, `ToyboxTest 1` (12 rooms, importer output). **`GenLevel7` (Fight, horizontal corridor), `GenLevel8` (Fight, vertical shaft) and `GenLevel9` (Elite, loop) are the three built to the corrected movement budget and passing `LevelValidator`** — the earlier six predate it and several fail. All three still need a `RoomTier` component and a slot in `LevelManager.roomPrefabs` before they enter the run.
- **`Assets/LevelSinasi/CainosLeveller/`** — `kuzeymap`, `Room_Easy_01`, `sinasiBigLevel` (3 rooms).
- **Legacy/retired** — `Assets/LevelEfeS/old_levels/` (`-1`, `0`) and `Assets/LevelEfeVrl/Old Levels/EfeVrl2`. (`Old Levels/` also holds the six contract-INCOMPLETE retirees listed below; the folder was consolidated from a stray `Assets/LevelEfeVrl 1/` copy — don't be surprised by the git rename.)

**Why this matters:** the map system's blocker was framed as "we need to build many more levels." The truer statement is **"a dozen contract-valid rooms already exist and need quality/correction passes, not creation from scratch."** That is a much cheaper path to the ~15-30 rooms a map needs.

**Rooms that would BREAK if naively added to the pool** (incomplete contract — verified): `efeslevel4` has CameraBounds + GirisNoktasi but **no ExitDoor** (unfinishable). `EfeVrl1`, `EfeVrl3`, `EfeVrlLevel1..4` (all six now under `Assets/LevelEfeVrl/Old Levels/`), plus `kuzeymap2`, `kuzeymapv1`, `CainosLevel`, are **missing `CameraBounds`** (camera would not clamp). Always re-check the three-part contract before adding a room to `roomPrefabs`. (Note: `CameraBounds.prefab`, `GirisNoktasi.prefab`, `MainMenu`, `GameOverScreen` also match the scan but are shared components/UI, not rooms.)

### Run Map — BUILT AND WORKING END TO END (2026-08-06)

**The whole system is done and verified in play mode: graph, generator, room routing, and the `M` screen.** Run order is driven by the graph, not by a shuffled pool — the section below describes the pre-map order, which survives only as a fallback.

| File | Role |
|---|---|
| `RunMap.cs` | `MapNodeType` / `RechargeType` enums, `MapNode`, `RunMap`. Pure data + queries, no Unity types. `Validate()` and `ToAscii()` live here. |
| `RunMapGenerator.cs` | `RunMapSettings` (Inspector-tunable) + the carving generator. |
| `RunMapManager.cs` | Singleton owning the act and the player's position. **Self-bootstraps** via `RuntimeInitializeOnLoadMethod` — no scene wiring. Also owns the `M` key. |
| `RunMapScreen.cs` | The map UI. Procedural, self-instantiating, Verdigris theme. |
| `MapGlyphs.cs` | Procedural node + recharge symbols. |
| `RoomTier.cs` | Marker on a room prefab root declaring which tier it serves. |

**Where the choice happens:** `LevelManager.AdvanceToNextRoom()`, called by `ExitDoor`. (It used to be `RewardManager.FinishReward()`; that screen was deleted 2026-08-09 and the hook moved with it.)

⚠️ **THE MAP OPENS ON EVERY ROOM CHANGE — the "skip it when there's only one branch" rule was WRONG and is reverted (2026-08-09).** The original reasoning was that a screen with a single button is ceremony, not a decision. Measured over 200 generated acts, **62% of room transitions offer exactly one option**, and planning with `M` suppressed the screen for another — so the player crossed several rooms without the map ever appearing and reported it as *"I open the map and I'm 2-3 floors ahead of where I should be."* Nothing was corrupt: the same 200 acts gave **0 invalid maps, 0 dead ends, and every step advanced exactly one floor**. The bug was that the run's only sense of PLACE was hidden whenever it had nothing to ask. Orientation beats the saved click.

⚠️ **The zero-options guard in `AdvanceToNextRoom` is load-bearing.** On the boss node `AvailableNext()` is empty, and a map opened for a required choice refuses Escape and the backdrop — so opening it with nothing clickable is an unescapable screen. Verified: leaving the boss skips the map and starts the next act. `M` opens the same screen in planning mode: clicking marks a branch and stays open. In forced mode Escape, `M` and the backdrop all refuse to dismiss it, and clicking commits and continues the run.

⚠️ If `RunMapScreen` can't find a Canvas, `OpenForChoice` **invokes its callback anyway**. A missing Canvas must never strand the run in a room with no way forward.

**Things that will bite you if you forget them:**

- ⚠️ **Recharge rooms are an ATTACHMENT to a node (`MapNode.recharge`), NOT a node.** Modelling them as nodes would make them floors, which the design forbids. `LevelManager` spawns the combat room first, then the recharge room, *without advancing the map* (`pendingRecharge`).
- ⚠️ **Only Fight and Elite may carry a recharge room, never Skirmish.** That is the entire run economy, not a tuning value — `Validate()` re-asserts it so it can't rot.
- ⚠️ **The map never promises a room it cannot spawn.** Recharge types are generated only for the prefab slots assigned on `LevelManager` (`foundryRoomPrefab` / `marketRoomPrefab` / `wellRoomPrefab`). **All three are ASSIGNED as of 2026-09-14** (see "Recharge Rooms" above); if a type stops appearing on the map, check that its slot is still filled before debugging the generator — the scene has lost prefab references before.
- **Untagged rooms serve every tier.** The 7 existing rooms predate `RoomTier`, so requiring tags would have meant a broken map until a chore was finished. Tagging narrows a room; not tagging costs nothing.
- `ToAscii()`'s edge rows show **direction from the source column**, not lines to scale — a wide fan-out (the hub does this) renders as one `\|/`. Use `Validate()` or the raw `next`/`prev` lists to confirm a specific connection.
- **`RunMapManager` is scene-local, no `DontDestroyOnLoad`** — a map is per-run and must reset on death, exactly like QuestSystem's quests.

**Measured behaviour** (500 seeds, 2000 random routes, default settings — 8 floors, width 5, 4 paths): 0 invalid acts, 0 uniform floors. Per route: **2.34 Skirmish / 2.39 Fight / 1.28 Elite**, **1.44 recharge rooms** (min 0, max 5), and **55% of random routes never pass a Market**. That last number is the one to watch — it is fine if the player can *see* the Market and route to it, and bad if they can't; re-measure it once the map screen exists.

`BreakUniformFloor` exists because the late-floor weights produced all-Elite rows often. A floor where every branch is the same type is a toll, not a choice, which defeats the reason difficulty is the node type at all.

**Traps hit while building the screen — don't re-learn them:**

- ⚠️ **`Image.Type` defaults to `Simple`.** The window outline is a 26px 9-sliced sprite; left at Simple it was stretched across the whole 1040×780 window and rendered as an enormous soft octagon hanging outside the panel. Any FlatUI `Panel`/`Outline` used at panel scale **must** be set to `Image.Type.Sliced` explicitly — the local `AddImage` helper does not do it for you.
- **Text pivots.** A label positioned by offset with a centred pivot places its BOX centre, so a 34px-tall label put its first line back on top of the glyph it was labelling. Pivot to top (or bottom) whenever the offset is meant to clear something.
- **Node labels must be narrower than they look like they need to be** — neighbours on a floor sit about one column apart minus jitter, and 150px labels collided on any floor that filled up. Horizontal jitter is deliberately tighter than vertical for the same reason.
- **`pathCount` must match `width`**, or no route ever starts in the centre column and the act draws as two arcs around an empty middle.
- ⚠️ **Deferred `Destroy` will bite any test that clicks a map button.** `Refresh()` deactivates old nodes before destroying them, but they survive until end of frame, so `GetComponentsInChildren<Button>(true)` still returns the PREVIOUS chart's buttons — whose listeners point at nodes that are no longer reachable, so the click silently does nothing. Filter on `activeInHierarchy`. This produced a convincing false "callback never fired" failure.

### `roomPrefabs` emptied for testing — RESTORED 2026-08-06

Kept as a diagnostic pointer, because the symptom is confusing. Commit `2f236ad` ("h") left `LevelManager.roomPrefabs` holding only `[0] hub` — the designer had emptied it for a test. The map still generated fine, but every combat node failed to spawn, logging *"no combat room available"*, and the player never left the hub.

Restored to the full 8 (hub + `efeslevel1-3` + `EfeVrl4-7`) by resolving the GUIDs recorded in `b2760be`, so the list is byte-identical to what it was rather than rebuilt by filename.

**If the run stops advancing past the hub, check this list first** — an empty or short `roomPrefabs` looks like a map bug and isn't one. It happened again on 2026-08-08 (found as `[hub, <NULL>]`) and was restored, again by resolving the GUIDs from the scene's last commit rather than re-picking by filename.

⚠️ **`Build` DOES NOT OVERWRITE — IT WRITES `Name 1.prefab` BESIDE THE ORIGINAL (2026-08-22).** Run
the importer twice on the same `.txt` and the second run silently produces a *new* asset at a unique
path, returning that path. The failure this produces is nasty and quiet: you believe you re-imported,
so you carry on editing **the stale prefab**, and every check you run against it passes — against the
old geometry. Caught on `NinjaArena` when the re-imported room's spawn read `(4.50, 2.00)`, an
old-grid coordinate, after a re-import that had reported success. **Always read the path `Build`
returns**, and delete the previous prefab first if you mean to replace it (see the reference-nulling
warning immediately below for why deleting is not free once anything points at the room).

⚠️ **DELETING AND RE-IMPORTING A PREFAB SILENTLY NULLS EVERY REFERENCE INTO IT.** The `<NULL>` above was a room the designer had slotted for testing. Re-importing a level (`delete the .prefab`, then `Build` again) **keeps the asset GUID** — the `.meta` survives — but **renumbers every fileID inside the prefab**. A scene reference is `{fileID, guid}`, so the guid still resolves while the fileID matches nothing: the link looks valid in YAML and reads as `null` in the Inspector. Before deleting a generated room prefab, check whether anything points at it, and re-assign afterwards. This is why `GenLevel8` is re-tagged via `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset` rather than a rebuild.

### Run Order — the pre-map order, now a FALLBACK ONLY (reworked 2026-07-02, superseded 2026-08-06)

⚠️ **This is no longer how the run is ordered.** `PickNextRoomPrefab()` routes through the map (above); the logic below now lives in `PickNextRoomPrefabWithoutMap()` and runs only if `RunMapManager.instance` is somehow null. It is kept as a *named, obvious* fallback because a missing manager silently reverting to random rooms would look almost right.

`LevelManager` was changed from an endless-refill pool (which repeated the same level forever) into a **finite, structured run**:

1. **First room is always the hub** (`roomPrefabs[0]`), and `BuildLevelQueue()` fills `availableRoomIndices` with indices `1..n`.
2. **Then every other pool level, once each, in random order (no repeats)** — pulled from `availableRoomIndices` until empty.
3. **Pool exhausted → the finale** (`PickFinaleRoom()`: the character's `bossRoom`, else `finalBossRoomPrefab`, else a draw from `bossRoomPrefabs`; gated by a `bossSpawned` flag so it only happens once).
4. **After the boss (or if no boss is assigned) → reset the flags and loop back to the hub** for a fresh run.

So a run is: **hub → each combat level once (random) → boss → (loop to hub)**. The old `RefillRoomPool()` and index-stripping logic are gone; `hasSpawnedFirstRoom` + `bossSpawned` are the state.

**Inspector requirements:** boss arenas go in **`Boss Room Prefabs`** / **`Final Boss Room Prefab`**, never in `roomPrefabs`. A boss room must satisfy the same room contract as every other room — a **`CameraBounds`** child (zone `BoxCollider2D`s) and a **`GirisNoktasi`** entry-point child — or the camera/spawn won't set up. With no boss rooms assigned at all, the fallback just loops hub→levels→hub.

### ⚠️ The hand rail is part of the frame — a locked arena is centred ABOVE it (2026-09-17)

The cards stand permanently across the bottom ~170 canvas px of the screen. Both boss arenas
(`NinjaArena`, `KagemushaHall`: zone 15 tall, **zone min = floor = 5**, five units of art below the
floor unused) had their zone centred in the 20-tall view at `RoomCamera` size 10, which put the floor
at 88% of the frame height — **directly under the cards, every foot in the fight hidden**. Designer,
while recording trailer footage: *"the hand is basically covering up the whole ground area."*

`CameraFollow.handRailPx` (170) fixes it in code: when the vertical zone is **locked** (zone + rail ≤
view), the zone is treated as extending down by the rail's height in world units and centred in the
space above the rail. Kagemusha Hall now shows y 0.9–20.9 instead of 2.5–22.5; the floor sits at 80%
and the cards fall entirely into the wall strip below it. Screenshot-verified.

**Scrolling rooms are deliberately untouched.** Extending the clamp too was tried and every generated
room grew a wider black band along the bottom (the importer pads the zone 2 units past the art on all
sides, so a lower camera shows more of nothing). There the margin is the author's job: **pad the zone
below the lowest floor by at least the rail's height** (≈2.2 units at size 7, 3.2 at size 10). The
importer's 2 already clears it at size 7. **A new boss arena needs no special handling** — the code
composes it — but its art must extend at least the rail's height below the floor, or the strip
under the cards is void.

If/when proper scene flow gets built (player starts in hub from main menu, returns after death/run completion), this loop-back should be revisited.

---

