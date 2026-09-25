---
name: deckshift-enemies
description: Deckshift's enemies — the AI pass of 2026-08-20 (mass, facing, line of sight), EnemyMelee hitboxes, the bat rebuild, the layer-convention mismatch, enemy health bars, head bounce and wall slide. Use when touching any enemy prefab, enemy AI, MonsterController, EnemyHealth, EnemyMelee, EnemySenses, a boss encounter, or anything about how enemies move, see, hit or die.
---

# Deckshift — Enemies

Split out of AGENTS.md 2026-08-21. Nothing was deleted; this is the same text, moved so
it loads only when the work is actually about enemies. Every ⚠️ here was paid for with a
real bug in this codebase — the reasons are the point, do not strip them.

## Enemy System

### Card & Enemy Numbers — see `CardAnchors.md`

All card and enemy numbers derive from the anchor table in **`CardAnchors.md`** (project root, 2026-07-15). Key facts: damage unit = **15** (one Fireball); **player starts with 40 Shift** (Player.prefab overrides the `maxShift = 3` script default — do NOT treat Shift as scarce at base; lowering the pool is the planned ascension difficulty knob); enemy HP is tiered so **fodder ≈ 12 HP dies to one Fireball**, up to Moss Knight 300. Early enemies are built from the Cainos zombie prefabs (recipe in `CardAnchors.md` §6). **Three zombie tiers built 2026-07-16**, importer markers live: **Shambler** `z` (12 HP fodder, melee), **Rotbrute** `Z` (25 HP grunt, 1.15× bigger, harder melee), **Spitter** `s` (18 HP ranged — `ZombieSpitterAI` lobs a projectile on a windup). **Enemy HP retuned 2026-07-16:** Melee 40, Ranged 25, Slime 10, Mimic 30 (untiered), Boss 300.

**Enemy move-speed retune (2026-07-17):** the AIs (`MeleeEnemyAI`/`ZombieSpitterAI`) leave `MonsterController.inputMoveModifier` false, so an enemy's effective ground speed is the max for its `defaultMovement` mode. Final values, all **Walk** mode: **all three zombies = 1.2** (`walkSpeedMax`, deliberately uniform per designer), **MeleeEnemy = 1.4** (buffed a hair above the zombies so it stays the stronger threat), **RangedEnemy = 1.2** (untouched). MeleeEnemy is a prefab **variant** sharing a base with RangedEnemy, so its 1.4 is a variant override and does NOT move RangedEnemy — verify with the effective-value dump (`GetComponentInChildren<MonsterController>()`) if you touch either. Caveat: the Cainos animator has NO speed-scaled playback (only a walk/run blend), so pushing these speeds much higher foot-slides badly — an earlier Run-mode ~3.x pass felt too fast and was reverted. Tune the per-prefab `walkSpeedMax` in the Inspector.

**Spitter projectile — green-goo `SpitGlob` (2026-07-17):** the spitter used to reuse the turret's red bolt `Mermi.prefab` (still the turret's), which read as ugly/placeholder. It now fires `Assets/Prefabs/SpitGlob.prefab` — a dedicated acid-glob whose visual is **procedural** (`Assets/Scripts/SpitGlob.cs`, house pattern: runtime-built goo sprite, squash-stretch wobble, tapering `TrailRenderer` goo streak; no art). SpitGlob sits on the **Projectile layer (8)** — REQUIRED for its trigger to hit the player; if you clone it, keep that layer. Movement/damage still come from the shared global `Projectile` component. NOTE: there are **three** `Projectile` types (global + two Cainos namespaces), so MCP component-add by short name is ambiguous and fails — add it via `execute_code` (`using`-scoped to the global one) or clone an existing prefab.
- **ShieldEnemy has no sprite** → it's unused in levels. Compose one from the Cainos packs (armored humanoid + shield prop) when convenient. The enemy *logic* works; it's purely missing art.
- ~~**Fireball sails over short enemies**~~ **FIXED 2026-07-16.** The Fireball prefab's tiny 0.137 `CircleCollider2D` is now a vertical `CapsuleCollider2D` reaching from wand height down to ~0.30 above the floor (world hitbox F+0.30→F+1.55), so it hits slimes/mimics without detonating on ground tiles. Launch height unchanged; sprite still casts from the wand. See `CardAnchors.md` §7.

### ⚠️ Melee hits go through `EnemyMelee`, never through a distance check (rebuilt 2026-08-11)

Every melee enemy used to resolve its swing as `Vector2.Distance(transform.position, player.position) <= attackRange + 0.5f` — a **circle centred on the attacker's FEET tested against a single point at the player's FEET**. `MeleeEnemyAI`, `SlimeAI` and `MimicAI` all shared it, all with the same hidden `+0.5`. Three things were wrong, and together they are what made combat feel unfair:

- **It reached BEHIND the enemy.** No facing was involved, so standing behind something swinging the other way still hit you.
- **It largely ignored height.** Measured on MeleeEnemy: the player was hit with their feet up to **2 units** above the enemy's — on a ledge, or mid-jump clearly overhead.
- **The range was secretly 33% larger than authored.** The `+0.5` was applied at strike time while `OnDrawGizmos` drew `attackRange`, so tuning 1.5 shipped 2.0 and the editor said 1.5. That circle is **~8× the player's width**.

And the player's carefully-placed capsule was **never consulted** for any of it.

`EnemyMelee.TryHit(attacker, dirX, reach, damage, knockback, height)` replaces it with a box in FRONT of the attacker, tested against the player's real collider via `OverlapBox` on the Player layer. `EnemyMelee.DrawGizmo` draws that same box, so the editor now tells the truth. Per-enemy `attackHeight` is exposed (humanoid 1.8, slime 1.2, mimic 1.3).

- ⚠️ **`dirX` is the direction committed to when the swing STARTED**, not the facing at impact. A swing is a commitment, so a player who gets behind the enemy during the wind-up is missed — that is the fix, not a side effect.
- **Verified:** in front HIT · behind miss · 2.5 above miss · 0.6 above HIT · 2.2 away miss.
- **The Moss Knight is deliberately NOT converted.** Its slam is a radius AoE and its charge is a body-check, so circles are the honest shape there. Revisit only if being clipped by its back reads badly.

### Pattern

- **`EnemyHealth`** base script — handles damage, flash, death, and (since 2026-08-03) **scrap drops**. ⚠️ Before that date this file claimed it "handles drops" and it did not — there was no drop logic of any kind, which is exactly why kills paid nothing. Drops now go through `scrapDropOverride` (−1 = auto-tier from `maxHealth`); the override is the hook for shift-infused elites. **Currently the only callsite that reports KillEnemy/AirKill to QuestSystem.** `Die()` calls `RelicManager.OnEnemyKilled()`, `QuestSystem.ReportEvent(QuestType.KillEnemy, 1)`, and (if airborne) `QuestSystem.ReportEvent(QuestType.AirKill, 1)`. It now also exposes C# events: **`OnDamaged`**, **`OnDamagedAmount(float)`** (carries the hit size — the boss flinches on big hits), and **`OnDied`** (fired inside `Die()` right before the GameObject is destroyed — the boss uses it to hand music back and to spawn its death VFX). **CRITICAL: `Die()` fires `OnDied` and then `Destroy(gameObject)` in the SAME frame**, so an `OnDied` handler must NOT rely on the enemy surviving — anything that needs to outlive the death (VFX, loot) has to run on its own separate object (see `BossDeathVFX`). Non-event death consequences are still direct calls inside `Die()`.
- **AeroBat (BatMan)** — uses Cainos pack visual + custom `AeroBatAI`. Parent has Kinematic Rigidbody2D + Polygon trigger collider. Raycast LOS aimed at player chest (+0.5 Y), shortened by 0.3 to avoid hitting tile at player's feet. State machine: Idle → Preparing → Diving → Returning.
- **MeleeEnemy**, **RangedEnemy** — based on Cainos pack patterns.

**`TakeDamage(float damage, Transform damageSource = null)` does not currently track damage source.** Spike or hazard kills would credit the player's kill counter the same as direct kills. Minor concern; flag if it becomes design-relevant.

### ⚠️ The AI pass of 2026-08-20 — three things that were wrong for a long time

The designer reported that enemies "don't mix well with terrain", "work kind of bad", and that **"the
player can push the enemies around — this should not be able to happen."** All three were real.

**1. THE PLAYER WAS A BULLDOZER.** Player is Dynamic at **mass 1.0**; enemies were Dynamic at 3–10.
Two dynamic bodies resolve overlap by shoving *both*, and `PlayerController` **assigns
`rb.linearVelocity` directly every FixedUpdate**, so a collision can never slow the player — all the
resolution went into moving the enemy. Fixed by raising every enemy body to **mass 500**.

⚠️ **Mass is free here, and that is the whole reason it is the right tool.** `MonsterController`
assigns `rb.linearVelocity` directly and gravity is `gravityScale`-based, so **neither locomotion nor
falling depends on mass** — it affects collision response and nothing else. Verified that nothing in
the project ever pushes an enemy body with a force (the Moss Knight assigns velocity, which is
mass-independent). Measured with a coasting player: **old mass shoved a Shambler 0.536 units, new
mass 0.003.**

⚠️ **SIDE EFFECT, AND IT IS A REAL GAMEPLAY CHANGE: the player is now BLOCKED by enemy bodies**
(measured: the player stops at x 5.69 where it used to barge through to 6.23). Dash does not disable
collision (only Phase does), so dashing into an enemy now stops you. If that reads badly, the
alternative is to stop Player↔Enemy colliding at all — but that needs the layer split below fixed
first, and it changes head-bounce/Pogo Boots.

**2. MELEE ENEMIES ATTACKED BACKWARDS.** `MonsterController.cs:226` only writes `pm.Facing` while
`inputMove.x != 0`, and every melee AI sets it to **zero** to stand and swing — so an enemy kept
whatever facing it arrived with while `EnemyMelee` still resolved the hit on the player's real side.
`RangedEnemyAI` and `ZombieSpitterAI` already carried an explicit per-frame facing line (with a
comment naming the bug); **`MeleeEnemyAI`, `SlimeAI` and `MimicAI` never got it — 49 of the pool's 77
enemies.** All three now face every frame. Verified: the enemy turns while `inputMove.x` is still
0.00, proving the turn comes from the new line and not from the controller.

⚠️ Partly masked in play, which is why it survived: knockback usually shoves the player back out of
attack range, which restarts walking and re-faces the enemy. It bites when you get behind a *stopped*
enemy — i.e. after a dash.

**3. NOTHING COULD SEE.** No ground AI checked line of sight at all: spitters lobbed acid through
solid rock, melee walked into the wall between them and the player, and **`Turret` was a bare
`while(true)` with no range check and no LOS**, firing every `fireRate` seconds from room spawn.
Projectiles travel `speed × lifeTime` = 10 × 3 = **30 units** against a ~25-unit screen, so turrets
shot the player from off screen, out of walls, the whole time the room was loaded. `EnemySenses` is
now the one place that answers "can it see me", and Turret gained `range` (13) + LOS.

⚠️ **Sight ACQUIRES, memory KEEPS** (`EnemySenses.Memory` 2.5s). Gating the chase on "can see right
now" makes a *worse* enemy — it freezes the instant you step behind a pillar and unfreezes after,
which reads as a stutter rather than as awareness.

⚠️ **CAST FROM THE CHEST, NOT THE FEET.** Enemy transforms are grounded at floor level by the level
importer, so a ray from the origin starts inside the floor tile. Measured on a completely clear line:
`eyeHeight 1.0 → CanSee true`, `eyeHeight 0.0 → CanSee FALSE`. Same class of bug as the player's old
`wallCheck` returning true on flat ground.

⚠️ **An unset `LayerMask` serializes as 0, which as a raycast mask means "hit nothing" — i.e. it would
silently disable line of sight entirely.** `EnemySenses.ResolveBlockers` therefore falls back to
Ground, so a forgotten Inspector slot degrades to CORRECT behaviour rather than to no behaviour.

⚠️ **TESTING TRAP that produced two false results in a row.** `Physics2D.simulationMode` is
`FixedUpdate`, so **`Physics2D.Simulate()` called from `execute_code` does nothing** — a push test
using it reported 0.0000 for both the old and new mass and looked like proof. And placing the test
player by offsetting from an enemy buries them inside terrain, so every LOS check returns false and
looks like the feature is broken. **Stand the test player on a real floor found by raycast, and
always run the control to confirm the test can still detect the bug.**

**Still open, deliberately not done:** enemies still never jump (`inputJump` is written exactly once
in the whole AI codebase, in `SlimeAI.cs`, as `false`), so they still stop dead at ledges; and
`MeleeEnemyAI` still does not patrol, so 27 enemies stand frozen until aggroed. Both change
difficulty and were left for the designer to call.

### ⚠️ The bat (`BatMan.prefab` + `AeroBatAI`), rebuilt 2026-08-20

Reported as "really bad… maybe doing it from scratch might be easier". It did not need rebuilding —
the Cainos rig and `AC Bat` controller render well. It needed five things fixed:

**1. IT NEVER ATTACKED WHEN NEAR TERRAIN — the big one.** `CheckForPlayer` raycast from
`transform.position`, and `Physics2D.queriesStartInColliders` is ON, so a bat hovering against a
ceiling or ledge — *which is where bats hang* — started its sight ray INSIDE that tile and got
**"blocked by Ground at distance 0.00"**. Measured live. Such a bat is inert for its whole life.
Now routed through `EnemySenses`, which skips `StartSkip` (0.35) units of ray before testing. 0.35 is
safe against seeing *through* anything, because level geometry is on a 1-unit grid.

**2. THE TELEGRAPH WAS AN UNREADABLE RED BOX.** The wind-up showed a plain red `Square` sprite above
the bat — at almost exactly the height of the enemy health bar, which is **also a red bar**. They
overlapped and were indistinguishable, so the dive effectively had no warning. ⚠️ **Deleted, and the
bat itself is the telegraph now: it REARS BACK away from you and flushes hot (`windUpTint`) before it
commits.** Anticipation is the oldest and most readable tell there is, it needs no icon or new art,
it cannot be confused with a health bar, and it shows *which way* the dive is coming because the
recoil runs along the same line. The Cainos monster shader exposes `_Color` (unlike the PLAYER rig's
"Alpha Cut", which exposes no colour property at all), so the flush is a `MaterialPropertyBlock`.

⚠️ **The recoil is applied in `FixedUpdate`, not in the coroutine that times it.** The body is
Kinematic, so `MovePosition` belongs on the physics step; driven from a coroutine it stutters. The
coroutine publishes `prepK` and FixedUpdate consumes it.

**3. NO COOLDOWN BETWEEN DIVES.** It re-acquired on the frame it arrived home. Measured: it killed a
full-health player in a few seconds *while the test was still being set up*. `diveCooldown` 1.1s.

**4. COMPENSATING SCALES.** Root was **0.40** with the visual child at **2.88** to cancel it out —
the same corruption shape as the old CardTemplate. Root is now (1,1,1) with the factor pushed down
into the child, **and the PolygonCollider2D's 150 points scaled by the same 0.40**, since points are
local and would otherwise have grown 2.5×. Verified a no-op: drawn bounds 1.753 × 1.761 and collider
1.627 × 1.623 **before and after, to three decimals**.

**5. Root position was `(-66.88, 19.79)`** (left over from being dragged out of a scene) and the
**layer was Default(0)**; now origin and Enemy(11). Health bar offset 1.00 → 0.62, which was a full
unit above a bat only 1.75 tall.

⚠️ **`Collider2D.bounds` on a PREFAB ASSET reads (0,0,0)** — it is only real on an instance. This
looked exactly like a degenerate collider and nearly got "fixed"; instantiate before believing it.

⚠️ **`AeroBatAI.startPos` is captured once in `Start()`**, so teleporting a bat to test it does not
stick — `IdleBehavior` flies it back. Respawn at the position you want instead.

Still true: `Assets/Prefabs/AeroBat.prefab` remains a legacy husk (a SpriteRenderer with concept art
and **no AI at all**). `BatMan` is the real one and the importer's `b` marker uses it.

### Layer Convention Mismatch (Known Issue)

**Verified against every enemy prefab 2026-07-18** (an earlier version of this file wrongly claimed MeleeEnemy was on Default — it is on Enemy):

- **Default layer (0):** AeroBat, BatMan, ShieldEnemy, Mimic, **Shambler, Rotbrute, Spitter** (all three zombies).
- **Enemy layer (11):** **MeleeEnemy**, RangedEnemy, SlimeEnemy, Taret, PatrolEnemy, MossKnightBoss.

Two consequences, both load-bearing:
1. Many systems check via the `enemyLayer` mask, which **misses every Default-layer enemy** (including all three zombies). The workaround in PlayerController is to use `GetComponentInParent<EnemyHealth>()` instead of relying on layer masks for head-bounce detection.
2. **`groundLayer` (2056) includes layer 11**, so the player can **stand on** MeleeEnemy / RangedEnemy / SlimeEnemy / Taret / PatrolEnemy / MossKnightBoss — but **not** on the zombies, bats, Mimic or ShieldEnemy. That asymmetry is accidental, not designed.

**Be aware of this when adding new enemies — pick a layer and stick with it, or use the EnemyHealth-component approach.** (Note: `PF Knight - Moss` is the raw Cainos prefab at 600 HP and is not the encounter; the real boss is `MossKnightBoss` at 300.)

### Wall Slide — a RELIC, not a base ability (built 2026-08-11)

**`PlayerState.WallSliding` was dead code for the whole project's life.** It was handled in three places (jump input, fall-speed clamp, state exit), had a `wallCheck` transform and `wallSlideSpeed` / `wallJumpForce` tuned on the prefab — and **nothing anywhere ever entered the state**, so wall-jumping had never existed in the game. That made it free to hand out as a pickup instead of a base move.

**Relic: `GeckoGloves` — "Gecko Gloves", Rare** (`Assets/Relics/GeckoGloves.asset`). Gated via `PlayerController.WallSlideRelicID`; the state can neither be entered nor sustained without it.

⚠️ **THE SLIDE IS FREE, THE WALL JUMP COSTS SHIFT (1, hub-exempt).** Sliding only ever slows a fall, so it's pure utility. A *free* wall jump is an unlimited climb — exactly the hole Pogo Boots' Shift refund opened, and a wall is far easier to find than an enemy to bounce on. Refused outright at 0 Shift rather than granted free.

Entry needs: the relic · airborne · **falling** (you catch a wall on the way down, never on the way up) · **pushing into it**. Exit on `!pushingIntoWall` — ⚠️ not `moveInput == 0` as the original code had it, or actively steering *away* from a wall left you stuck to it and walls behaved like flypaper.

**The animation is borrowed, not authored.** The Cainos pack has no wall-slide clip, but its **Ladder Climb** layer is already a character pressed flat against a vertical surface with both arms up. `IsClimbingLadder = true` plus **`ClimbingSpeedMul = 0`** freezes it on one frame, turning a climb cycle into a grip. That one parameter is the whole difference between "climbing an invisible ladder" and "holding a wall". Facing already points into the wall, since the slide can only start while pushing toward the wall the sensor found.

`WallScrapeVFX` supplies the motion cue — a frozen pose alone reads as being *stuck* to the wall, with nothing saying which way you're travelling. Procedural grit at the contact point, drifting up because the player is going down. ⚠️ Pitched much brighter than "dust" suggests: these render through the scene's 0.5-intensity global `Light2D` like every world sprite, and a plausible dust value came back at half strength against dark rock and read as dirt on the lens.

**Still open:** the relic borrows Pogo Boots' boot icon, because a relic with no art draws as an empty socket. Swap it when there's an icon to swap in.

### Head Bounce (Pogo Boots Relic) — REBALANCED 2026-08-10

⚠️ **It used to grant `AddShift(1)` on every bounce, which this file never recorded.** With a 0.3s cooldown that made Pogo Boots **the only free Shift regeneration in the game** — in a game whose stated identity is that Shift does not regenerate on its own and carries over for the whole run. It quietly turned any room with enemies into a refuelling station: a 40 HP melee enemy is five bounces at 8 damage, so a room of six was worth roughly half a full Shift bar for nothing. The designer flagged the relic as overpowered; this was the mechanism.

Three changes, meant to work together (see `PlayerController.TriggerHeadBounce`):
- **No Shift refund at all.** The boots are a movement toy; movement is what they pay in.
- **One bounce per enemy per airtime** (`_bouncedThisAirtime`, cleared the moment `isGrounded`). Camping a single slime until it died was both the degenerate line and the boring one; chaining ACROSS several enemies is the trick worth rewarding, and it's the only thing still allowed.
- **Decaying chain height** — `pogoChainFalloff` (0.70 / 0.55 / 0.42 / 0.32, Inspector-tunable on the Player), so a chain can't sustain itself across a dense room.

Verified: two bounces on the same enemy in one airtime deal 8 damage total (not 16), a second distinct enemy is still accepted, and Shift is unchanged across both.

- 8 damage, `defaultJumpForce * pogoChainFalloff[n]` upward force, 0.1s camera shake, 0.3s cooldown.
- Gated behind `RelicManager.HasRelic("PogoBoots")`.
- Uses both `OnCollisionEnter2D` and `OnTriggerEnter2D` (AeroBat has trigger collider, others have solid).
- Contact normal check: `contact.normal.y > 0.7`.

**Gravity reversal — HANDLED (verified in code 2026-07-26):** every branch of the head-bounce path now flips on `isGravityReversed` — the falling-direction check (`OnTriggerEnter2D`: `isGravityReversed ? velocity.y > 0.1f : velocity.y < -0.1f`), the position-vs-enemy check (top vs bottom), the collision-normal check (`normal.y < -0.7f` vs `> 0.7f`), and the bounce impulse direction. The old "velocity sign check doesn't account for gravity reversal" gap is closed; head-bouncing works upside-down.

### Enemy Healthbars (EnemyHealthBar.cs + EnemyHealthBar.prefab)

Wired and working across all six enemy types (AeroBat, MeleeEnemy, RangedEnemy, ShieldEnemy, Turret, PatrolEnemy).

**Architecture:** `EnemyHealth` instantiates `healthBarPrefab` (assigned per-enemy in Inspector) in `Start()`, calls `Initialize(transform, headBarOffset, computedWidth)`. The bar parents itself to nothing (free in world space), follows the enemy via its own `LateUpdate`, and is destroyed in `Die()` before the enemy GameObject. Width is computed from `Collider2D.bounds.size.x * 1.2`. `EnemyHealth.headBarOffset` is the per-enemy Y offset; tune in Inspector if the bar sits in the middle of the model instead of above its head.

**The prefab itself is intentionally near-empty:** `Assets/Prefabs/UI/EnemyHealthBar.prefab` has only a `RectTransform` + the `EnemyHealthBar` MonoBehaviour. `BuildCanvas()` in Awake constructs the Canvas, CanvasGroup, border Image, FillImmediate (dark red, snaps), FillDelayed (orange, lerps), and HealthText (TMP) procedurally. WorldSpace canvas at `CANVAS_SCALE = 0.01f`.

**Two pitfalls already hit and fixed — do not regress:**

1. **`UnityEngine.Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd")` does NOT work at runtime.** Returns null with logged errors. The current solution: `EnemyHealthBar` builds a 1×1 white sprite procedurally in a static `GetWhiteSprite()` helper (cached in `cachedWhiteSprite`), assigned to every Image's `sprite` field in `MakeChildImage`. **Required for `fillAmount` to render** — Filled-mode Images with no sprite silently ignore fillAmount and just render as flat colored rectangles.
2. **Sorting fallback for SkinnedMeshRenderer enemies.** `Initialize` first checks for SpriteRenderer (for any future sprite-based enemies), then falls back to SkinnedMeshRenderer for Cainos-based rigs. Without this fallback, AeroBat/MeleeEnemy/RangedEnemy/etc. would stay at default `sortingOrder = 100` regardless of their actual rendering layer.

**Visibility (reworked 2026-08-11 — designer):** ⚠️ **The bar is ALWAYS ON, or entirely OFF. There is no fade and no damage-triggered reveal.** It used to start at alpha 0, appear only once the enemy was damaged, and fade out 3 seconds later, while the only setting toggled the *numbers* on top of it. That was backwards on both counts: the bar is meant to be readable at a glance the whole time an enemy is alive, and the switch is meant to remove it entirely for players who find it cluttered. `FADE_DELAY` / `FADE_SPEED` / `isDamaged` are gone; `SetHealth` no longer touches alpha.

`GameSettings.EnemyHealthBars` drives it by toggling the **Canvas**, not the CanvasGroup alpha — a hidden bar then costs no draw calls at all, which matters with one per enemy. It applies live to already-spawned bars via `GameSettings.OnChanged`.

⚠️ **It uses a NEW PlayerPrefs key, `ShowEnemyHealthBars`** — deliberately not the inherited `ShowEnemyNumbers`. That key meant "show the HP text"; this one means "show the bar at all". Reusing it would have silently turned the bars OFF for any existing player who had only switched the numbers off. **When a setting's MEANING changes, take a new key.**

**Shield-block damage leak (RESOLVED — verified by code audit 2026-06-10):** `EnemyHealth.TakeDamage` now runs the `shield.IsBlocking()` check and returns BEFORE deducting health. Blocked hits no longer lose HP. Do not re-fix.

---

