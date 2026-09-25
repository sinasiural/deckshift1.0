# Boss Design — The Ninja (name TBD)

**Status:** Designed 2026-08-22. **Greybox partially built the same day** — decisions below are the
designer's unless marked PROVISIONAL or OPEN.

| | state |
|---|---|
| Arena `NinjaArena.prefab` + `RoomCamera` | ✅ built, validator PASS, camera locked at 16:9+ |
| The quiver (`CardActionType.SalvagedShuriken`, "Borrowed Steel") | ✅ built and **play-mode verified** |
| `BossShuriken` — fly / hurt / stick / collect / recall | ✅ built and verified |
| `NinjaBoss` — dormant, volley, recall | ✅ built and **play-mode verified** |
| Dash slash — lane telegraph, commit, wall overshoot, punish window | ✅ built and **play-mode verified** |
| Katana flight — leap, hang, throw, plant, blink, pursue | ✅ built and **play-mode verified** |
| `NinjaBoss` — airborne Shift drop | ✅ **verified** (grounded control passes; cap holds) |
| Boss prefab `Assets/Prefabs/NinjaBoss.prefab` | ✅ built and verified |
| `BossAwakenTrigger` placed in the arena | ❌ not done |
| Wall-run dive — the 4th attack | ✅ built and **play-mode verified** |
| Low-HP chained flight · name · relic | ❌ not built |
| In `bossRoomPrefabs` | ❌ deliberately not — an empty arena is worse than no node |

**Verified in play mode, outside the hub** (the hub's umbrella rule skips charge spending, so testing
there would have lied): the quiver appends past hand capacity (hand 3/2), two pickups stack into ONE
card, each throw spends exactly one star while the card stays in hand, it leaves at zero, it survives
a Recall, and it never entered the draw/discard/exhaust piles at any point. A `BossShuriken` fired
past the player embedded in the floor at the correct angle, left the player's HP untouched, and was
collected on contact.

He is a **normal floor boss** (`LevelManager.bossRoomPrefabs`, a mid-map optional `MapNodeType.Boss`
node), **and** he is the Ninja character's `FinalBoss`. See §2 — that is not a contradiction, it is
the structure.

---

## 1. The premise (designer + friend, 2026-08-22) — this is bigger than one boss

The old story was AI-written filler and is being replaced. The new premise:

> **The playable characters all used to own the castle.** Each of them went insane inside it over
> time, and they keep coming back to take it over again.

This is a *structural* premise, not decoration — it explains the run loop (you keep coming back
because that is what these people do), it explains the roster, and it explains the bosses.

**The consequences, all designer-stated:**

- ⚠️ **ALL BOSSES ARE CHARACTERS.** Target roster is **at least 10 characters**, each shipping twice —
  once playable, once as a boss. This is the main argument for the premise: in a project whose stated
  content gap is *1 boss built of ~10 wanted*, every character authored is now two pieces of content.
- ⚠️ **THE MOSS KNIGHT EVENTUALLY STOPS BEING A BOSS** and becomes an **Elite-tier enemy**. He stays
  in `bossRoomPrefabs` for now — with a 2-character roster, removing him leaves a boss pool of one
  that repeats within a run. Revisit at ~4 characters.
- **Mid-map bosses are the OTHER owners; your own mirror is held for the finale.** Play the Wizard
  and you may meet the Ninja mid-map, with the Wizard at the top. Play the Ninja and it inverts. The
  filter is one line — `bossRoomPrefabs` is already drawn without repeats.
- **The finale becomes per-character**, so `finalBossRoomPrefab` grows into a per-character lookup
  (a field on `CharacterData`, or a pick from `CharacterSelection.Chosen`). Small change; do it once,
  before there are three of them.

⚠️ **THE MIRROR MUST NOT LITERALLY BE THE PLAYER.** A boss that plays exactly like you is either
unthreatening (your kit is not scary when an AI holds it) or unfair (your mobility plus boss HP). The
mirror is **what you become**: same vocabulary, further gone. You throw one shuriken, he throws six.

**The line the finale is built on, and the reason the premise is worth having:**

> **He doesn't pay Shift.**

The whole game is that movement costs you, permanently. The thing at the top of the castle is what
you would be if it had stopped costing anything. It needs no dialogue and the player feels it in about
four seconds. *(As a mid-map boss he is a lesser version of himself and this may be dialled back —
see OPEN §11.)*

⚠️ **Keep the tone playful.** Per the tone rules in CLAUDE.md, "a pile of insane has-beens who refuse
to move out and keep fighting over the deed" is a comedy premise as much as a tragic one. It has to
stay compatible with card names like *Loot Goblin* and *Blood Money*. Sad **and** petty.

## 2. Why he needs no crusher

The Moss Knight's arena carries a `CrusherTrap` because a **room** had to satisfy the design law that
a boss must be damageable without cards. Once every boss is a character, **the boss answers that
himself**, in his own vocabulary — which is what the shuriken mechanic below does. This is a better
constraint and it should hold for every future boss: *"here is how you fight me with nothing"* is part
of the design, not a machine bolted to the wall.

## 3. Toolkit (all assets owned — no new art)

- **Boss visual: `PF Pixel Character - Ninja` — THE SAME PRESET AS THE PLAYABLE NINJA** (designer,
  2026-08-22).

  ⚠️ **AN EARLIER VERSION OF THIS DOC SAID THE OPPOSITE — "he must NOT look like the playable Ninja"
  — AND RECOMMENDED `Assassin` OR `Xiake`. That is superseded, and the reason is the whole point of
  the premise.** The distinctness rule was written before the mirror finale existed. Once the run
  ends with you fighting *yourself*, an identical model is not a collision to avoid, it is the
  payload. A Wizard player meeting him mid-map reads him as another former owner; a Ninja player
  meets him at the top and the premise lands with no dialogue at all. **Do not re-propose a
  different preset for the sake of visual separation.**

- ⚠️ **A BOSS ON THE PLAYER RIG CAN STILL FAIL TO READ AS A BOSS** — that concern survives the change
  above, it just can no longer be solved by picking different clothes. Three things carry it:

  1. **Scale 1.0, against the player's 0.8.** The player's `visualModel` is 0.8, so the pack's NATIVE
     scale is already 25% bigger — and it is the art's true pixel grid, so he comes out *crisper*
     than the player rather than mushier. The usual penalty for enlarging pixel art is dodged by
     returning to native rather than going past it. **Do not scale beyond 1.0 chasing size.**
  2. **The katana, not the shuriken.** The playable Ninja holds `PF Weapon - Shuriken`; the boss
     holds `PF Weapon - Katana`. Free silhouette separation the design already called for.
  3. **A constant effect** (designer-requested). See the constraint below — this is the dangerous part.

- ⚠️ **`_Color` DOES NOT EXIST ON 15 OF THE 16 RENDERERS, SO ANY AURA THAT TINTS HIS BODY WILL
  SILENTLY DO NOTHING.** Verified on this exact preset: `Alpha Cut` and `Body` expose **only
  `_Alpha`**; only `Hair` carries a colour. Setting a missing shader property is a no-op with no
  warning — the bug shape that kept the gravity-reversal flash invisible for months across two
  separate "fixes". The boss effect must therefore be built from things that are **not the rig**:
  - **Baked-mesh afterimages.** `CardAimIndicator` already does this correctly via
    `SkinnedMeshRenderer.BakeMesh`. ⚠️ A SpriteRenderer-only pass yields a **katana-only ghost**,
    because the body is 16 SkinnedMeshRenderers and only the weapon is a SpriteRenderer.
  - **Separate sprite objects** (ground smoke, a contact shadow) — ordinary sprites the pack's
    shaders never touch, so they can be coloured freely.
  - `_Alpha` modulation on the rig, the one handle that genuinely works on it.

- ⚠️ **THE EFFECT HAS A SECOND JOB, AND IT MATTERS MORE THAN LOOKING IMPRESSIVE: two identical
  figures can be confused for one another in a fast fight** — especially once he teleports and both
  of you are airborne. The effect is what keeps *"which one am I?"* instant. Judge it on that, not on
  spectacle.
- **Weapons:** `PF Weapon - Katana`, `PF Weapon - Shuriken`.
- **Animation:** the same `AC Character.controller` the player uses. ⚠️ **`AttackAction = 13` is
  THROW** (wind-up / hold / release, driven by `IsAttacking`); **14 is CAST**. Use 13.
- ⚠️ **`AttackSpeedMul` is a GLOBAL animator parameter.** The player's Shuriken sets it to 2.2 and
  restores it to 1. If the boss drives it, it must restore it too.
- **Reuse:** `EnemyHealth` (so cards and everything else damage him for free), `BossHealthBar`,
  `BossFightTrigger`, `CameraShake`, `HitStop`, `SfxManager`, `ShiftCrystal.prefab`,
  `EnemyMelee.TryHit`, the procedural VFX house pattern.

## 4. Core loop — THE BOSS ARMS YOU

**He throws shuriken. They stick where they land. You pick them up and throw them back.**

That is the card-free damage avenue, and it is inseparable from the boss rather than bolted to the
room. A player who walks in with a zero-damage deck can still win.

**The card you get is a quiver, not a card:**

- ⚠️ **IT DOES NOT DISCARD.** It stays in hand until its charges are spent, then evaporates without
  entering any pile. This is the whole reason it works — a discarding 20-charge card is **20 Recalls**,
  each costing Shift and escalating. That is a toll booth, not a fight.
  Machinery already exists: `StaysInHand` / `RetainsThroughRecall` (the Clingy and Teacher's Pet
  hooks), and **Stagger** is the worked example of a conjured card that is retained through Recall and
  routes to no pile.
- **Pickups stack CHARGES onto the one card**, never spawn new cards. Hand capacity is **3**, and
  **2** when playing the Ninja. Six pickups cannot be six cards.
- ⚠️ **It bypasses hand capacity, like Stagger.** A player whose hand is full of junk must never be
  locked out of their only damage source — that is the exact death spiral this exists to prevent.
- **Holding a hand slot IS the cost.** Same trade Stagger makes, so the game has already taught it.
- **Damage stays 8.** See §5.

⚠️ **HE RECALLS THEM.** Uncollected shuriken snap back to him after ~8s. Three jobs at once: it stops
the degenerate line where the player ignores the fight and farms the floor, it creates the
volley → scramble → recall rhythm, and a ninja yanking his blades home is free coolness.

**OPEN:** does the quiver survive the fight? Evaporating at room end is clean and Stagger-like.
Persisting as part of the reward is a lovely payoff — the boss permanently changes your deck. Not
decided; see §11.

## 5. The damage maths — do NOT inflate the shuriken

The two obvious fixes were **considered and rejected by the designer**: making shuriken hit the boss
harder, and minting boss-only cards. Both are the same fix in different hats — *lie to the player
about what a shuriken is*. `CardAnchors.md` says 8 is 8, and a boss that secretly makes it 24 corrodes
that number everywhere it is used.

**The boss changes instead. He is glass.**

The Moss Knight is 300 HP because he stands still and you have a press that does 80. This boss's
difficulty is **hitting him at all** — evasion is his real health. Volume, not damage, is the
mechanic:

- **HP ~150–180** (start at 160).
- **Volleys of 4–6**, thrown often. The floor is littered, not sprinkled.
- ~**20 landed shuriken** kills him from a zero-damage deck, across a ~2 minute fight in which he
  throws sixty-plus. Never a grind, and 8 is still 8.

⚠️ **Where they land needs no arena support** — he throws *at the player*, so they embed wherever the
player is fighting. Self-balancing. Do not design collection zones.

## 6. Moveset — four attacks

| Attack | Built from | Telegraph | Effect |
|---|---|---|---|
| **Shuriken volley** | Throw (`AttackAction 13`) | throw gesture | 4–6 shuriken fanned at the player; they **stick** in whatever they hit and become collectible. Recalled after ~8s. |
| **Dash slash** | Run + Attack | ⚠️ **the LANE, not the wind-up** | stops, charges briefly, then dashes; damages **the whole path**, origin to end. |
| **Katana flight** | Jump → Air → Throw → Attack | **the katana itself** | leaps, hangs at apex, throws the katana — it **sticks where it lands**; he vanishes, reappears at it, then dashes at the player. |
| **Wall-run dive** | Ladder Climb + Dash + Swipe | **the cling** — he hangs on the wall aiming, and the diagonal lane is drawn | crosses to the nearest wall, runs UP it, clings and aims, then dives diagonally at the player. Body-slam damage along the path. |

**The wall-run dive exists to DELETE THE REFUGE (built 2026-08-22).** Every other attack he has is
grounded and horizontal — the dash flatly refuses a target more than 2.5 units above him — so before
this, the ledges and the centre island were a free safe zone. That is the exact opposite of the
arena's stated thesis that no tier is correct for long. It is **prioritised whenever the player is
above him**, which is what makes the choice of tier a real one.

- ⚠️ **The wall run is a BORROWED animation, not an authored one.** The pack has no wall-run, but
  `Ladder Climb` is already a character pressed flat against a vertical surface. Climbing uses
  `ClimbingSpeedMul = 1`; the cling freezes the same clip with **`ClimbingSpeedMul = 0`**, turning a
  climb into a grip. Both halves of that trick come from the Gecko Gloves wall-slide.
- ⚠️ **The dive hit is a CIRCLE, not `EnemyMelee.TryHit`.** That helper takes a `dirX` and reasons in
  ±X only, so it would miss on a steep diagonal. A dive is a body, not a swing.
- ⚠️ **`gravityScale`, the ladder flags, `IsDashing` and the player-collision ignore are ALL restored
  in one `finally`.** Every one of them is a latch: a `StopCoroutine` landing mid-move would leave him
  weightless forever, stuck to a wall, or permanently intangible to the player.
- **The lane telegraph was generalised to take a direction** rather than a sign, so the diagonal reuses
  the horizontal dash's strip — one lane, one alpha calibration, nothing to keep in sync.

*(The Shift-theft idea was rejected for this boss and reserved for a future thief-identity boss. The
counter-stance survives as the candidate FIFTH — see §11.)*

**Built 2026-08-22. Three bugs it cost, all worth keeping:**

- ⚠️ **THE FIRST VERSION DASHED A FLAT 18 UNITS, WHICH BROKE THE ATTACK'S WHOLE POINT.** In a 30-wide
  arena that put him on the far side every time, so he never met a wall — meaning the long
  wall-recovery, the punish window the attack exists for, could not fire at all. He also just
  ping-ponged end to end. The lane is now **distance-to-player + a fixed overshoot**, clipped by any
  wall. That is what makes backing up to a wall a real play.
- ⚠️ **THE TELEGRAPH WAS INVISIBLE, AND THE ALPHA WAS NOT WHY.** Measured renderer bounds:
  **0.08 × 0.02 world units.** `FlatUI.Pixel()` is a ONE-PIXEL sprite, so its native world size is a
  fraction of a unit — setting `localScale = (length, height)` scaled *by* it instead of *to* it and
  produced a speck, while every value in code read correct. **Two rounds of "the alpha must be too
  low" were spent before anything measured the bounds.** Divide by `sprite.bounds.size`, as
  `Shuriken.Spawn` already does.
- ⚠️ **The alpha ramp was also genuinely wrong**, just secondary: it started at 0.05 and eased on
  `k²`, so the lane only became readable in the last instant of the wind-up — the one moment it is
  too late to help. It is a linear ramp from a **readable floor** now (0.15 → 0.34, both picked by
  screenshot). A telegraph's job is done in its first frame; the ramp only builds pressure.

**Dash slash — the two rules that make it fair:**
- ⚠️ **Telegraph the lane he is about to occupy, not the wind-up.** Drawing the danger makes leaving
  it a decision instead of a reaction. This is the difference between "fast" and "annoying".
- ⚠️ **The punish is at the END.** He overshoots; ending against a wall costs him a longer recovery.
  That recovery is where a cardless player does real damage, and it is why the arena has **no open
  ends** (§8).
- Implement the hit with **`EnemyMelee.TryHit`** — a box in front of the attacker tested against the
  player's real capsule. The dash is a lane, so a box is the honest shape. (The Moss Knight is
  deliberately *not* converted because its slam is a radius and its charge is a body-check; this one
  is not that.)
- Like the Moss Knight's charge, he should **pass through the player** rather than bulldoze them.

**Built 2026-08-22 (`BossKatana.cs` + `KatanaFlightRoutine`). Two failures worth keeping:**

- ⚠️ **A "SPENT" FLAG THAT GATED THE TERRAIN CHECK SENT THE BOSS OUT OF THE ARENA.** `OnTriggerEnter2D`
  set `spent = true` when the blade clipped the player, and `FixedUpdate` early-returned on `spent` —
  so a blade that passed through the player **stopped looking for walls and flew forever**. Measured
  at **x = −40**, far outside a room whose interior ends at 39. The boss then blinks *to the blade*,
  so this was one step from teleporting him out of the world. Two fixes, both needed: the flag now
  means only "has already hurt the player" and never gates the flight, **and the blink refuses a
  blade that is not `Stuck`.** Passing through someone is not the end of a thrown sword's flight.
- ⚠️ **`gravityScale` is zeroed for the apex hang and restored in a `finally`.** He is a dynamic body;
  a `StopCoroutine` from death or a room change would otherwise leave a boss hanging weightless in
  mid-air forever.
- ⚠️ **`IsGrounded` had to stop reading `collider.bounds`** before this move could work at all. With
  `autoSyncTransforms` off, bounds lag a step — and this boss now *teleports*, so straight after a
  blink he was ground-checking wherever he used to be. It derives from transform + collider offset
  now, like `ChestPoint`. **The airborne Shift drop could not have been verified without this.**

**Katana flight — what makes it cool, beyond the mechanic:**
- ⚠️ **THE KATANA IS THE TELEGRAPH.** It sticks and sits there for a beat before he blinks. The player
  gets to see where he will be **before he is there**, so the fight becomes readable at a glance —
  *look at the sword, not the man*. Land near the player and they know to move; a player already
  moving can end up behind him when he arrives.
- ⚠️ **While it is in flight he is UNARMED** — no dash slash until he reaches it. The coolest-looking
  move is also the one that costs him something.
- **At low HP, let him chain it**: throw, blink, throw again, *then* dash. Same move, escalated, no new
  animation.

## 7. The Shift valve — hit him in the air, he drops a crystal

Designer-specified. Without a valve, a dodge-heavy fight is a run-ender three rooms later.

- Hook it on **`EnemyHealth.OnDamaged`**, with the boss checking its own grounded state — exactly how
  the Moss Knight's flinch works. Hooking it there means it works for **every** damage source,
  including ones that do not exist yet.
- ⚠️ **Cap per AIRTIME, not per hit.** Precedent: Pogo Boots' `_bouncedThisAirtime`. Without a cap, one
  fast card during a single leap prints six crystals. Start at **2 per airborne period**, tunable.
- Drops `Assets/Prefabs/ShiftCrystal.prefab` (+1 Shift each), collectible mid-air.

**⚠️ THIS IS THE FIGHT'S SKILL CEILING AND IT WAS NOT DESIGNED — IT EMERGED.** The shuriken is
**aimed** (it flies at the cursor), so a player with nothing but floor scraps has a real loop to climb:
don't just throw at him, throw at him *while he is airborne*. Damage **and** the Shift to keep dodging.
**His scariest attack is also your payday.** Do not break this by making the airborne windows
untargetable or by capping the drop at 1.

## 7b. Playtest fixes, 2026-08-22

Four things the designer hit in the first real play. All fixed and verified.

- ⚠️ **THE DASH COULD HANG FOREVER — the worst bug so far.** The travel loop ended on
  distance-travelled or `WallAhead`, and `WallAhead` cast a SINGLE ray from chest height. A platform
  that blocks his body above or below that line satisfies neither condition: he is pinned by physics,
  x stops accumulating, and the dash never ends. Symptoms were a latched dash animation, `IsDashing`
  stuck on, player-collision permanently ignored, and — because the once-per-dash strike flag was
  never spent — **walking into a long-finished dash still dealt damage**.
  **The fix is not a better ray.** It is to notice he has stopped moving: whatever the geometry, no
  progress means the dash is over (`DASH_STALL` 0.10s), with a hard `DASH_TIMEOUT` of 1.6s beneath
  it. `WallAhead` also probes three heights now instead of one.
  **Verified by driving a dash at `dashSpeed = 0`**, which exercises the stall path exactly.
- **He stood still between attacks.** A flat 4.5s `WaitForSeconds` after every attack, which read as
  a statue that occasionally lunged. Replaced by `RepositionRoutine` — he holds a jittered preferred
  distance on whichever side of the player he is already on, driving the pack's run blend. Gap is
  now `betweenAttacks` (1.1s) and he spends it MOVING.
- ⚠️ **THE EXIT WAS USABLE MID-FIGHT** — you could walk to the door and leave, making the encounter
  optional in the room that exists for it. `ExitDoor.SetLocked` now gates `Update`, `PerformExit` and
  the interact prompt (no "press E" on a door that refuses — that reads as a broken key).
  ⚠️ **`IsLocked` is runtime-only and defaults to UNLOCKED.** Never serialize a room shut: if whatever
  unlocks it fails or is absent, an open door costs nothing and a locked one ends the run. The boss
  also unlocks it in `OnDestroy`, not just on death, for the same reason.
- **The sealed door wears `Door Iron Fence 01`.** ⚠️ The levels doc records this asset as REJECTED for
  the ordinary exit because *"bars read as blocked"* — which was a defect there and is exactly the
  meaning wanted here. **A rejected asset is rejected for a reason, and the reason can invert.** The
  pack animates it via one `IsOpened` bool, so the portcullis raises itself when he dies. The bars are
  auto-fitted to the archway rather than hardcoded (verified: both bottoms at y=5.00, both 3.18 tall),
  so a future exit-art swap — there have been two — does not strand them.
  Its bright cyan `Sky` panel is the "new hue in a spent palette" the doc warns about; kept on for now
  because loud is correct for a sealed exit, and switchable via `barsShowSky`.

## 7c. Second playtest, 2026-08-22 — the wall burial, and the silence

### ⚠️ HE BLINKED INTO THE ARENA WALL AND STAYED THERE

Designer's report: *"it threw the katana top right of the map, and got stuck there, only able to
throw shurikens. he basically got stuck in the wall."*

`ResolveStandingSpot` was one raycast down from the planted blade, standing him on whatever it hit.
But **the blade is deliberately embedded 0.22 units INTO the surface it struck**, so that ray starts
inside a collider — and `Physics2D.queriesStartInColliders` is ON by default, which means it returns
a hit **at distance 0.000**. Measured against the real arena rock: `hit 'Ground' at distance 0.000,
point (46.00, 22.00)` — the "floor beneath the blade" it found was the blade's own position. He
teleported into the rock, and from inside it his ground check also passed (same reason), so he was
grounded, walled in, and every attack except the volley was blocked.

**Two fixes, and the second one matters more than the first.**

1. **The blink resolves a spot his CAPSULE ACTUALLY FITS IN** — a ring search outward from the blade,
   nearest first, angles walked from straight-down out, then a short floor snap. Identical in shape
   to the Phase card's `EjectFromGeometry`, and for the identical reason: a single probe that looks
   authoritative is answering a different question. If nothing within 6 units works, **he does not
   blink at all** — he keeps his sword and the move simply ends.
2. **`EnsureNotStuck()` — a general watchdog, not a katana fix.** Fixing the blink fixes the one path
   somebody reported. A boss that teleports, dashes with collision disabled, dives at 30 u/s and
   clings to walls with gravity off has other ways to end up embedded, including ones nobody has
   written yet. Same reasoning as `EnemyHealthBar` owning its own lifetime. ⚠️ **It has NO "I am
   mid-move" exemption flag, deliberately** — that flag is a latch, and one `StopCoroutine` on death
   leaves the watchdog silently dead for the session. It does not need one: the solver never lets a
   dynamic body rest inside terrain, and the detection box (0.8× capsule) is smaller than the fit
   test (0.95×), so being pressed against a wall at speed does not trip it.

⚠️ **"NEAREST CLEAR SPOT" IS NOT "SOMEWHERE HE SHOULD BE".** Burying him deep in the rock frame and
searching outward first surfaced him **on top of the room** — free, upright, standing outside the
level. The search now runs twice: pass one only accepts spots inside the room's `CameraBounds`, pass
two takes anything clear. Out of the fight is bad; stuck in a wall is worse, and the two passes are
that ordering written down.

⚠️ **AND THE TWO SEARCH RADII ARE DIFFERENT ON PURPOSE.** The blink is a *promise* — the planted
blade says "this is where he will be", which is the only reason a teleporting boss is legible rather
than unfair. Letting it hunt a long way would break that promise. Six units, or he stays put. The
rescue is a *repair*, the telegraph is already moot, so it looks 16. The floor snap was also cut from
9 units to 3 for the same reason: at 9 it quietly became a second teleport, measured worst case
**8.2 units away from the sword the player was told to watch**. He is a dynamic body — gravity can do
a long drop, and a ninja falling out of the ceiling looks better than one who was never up there.

**Verified: 65,740 simulated katana throws** — every from/to pair of standable positions in the
arena, raycast and embedded exactly as `BossKatana` does it. Zero landed inside terrain, zero landed
outside the arena, zero aborted, and he arrives an average of **0.81 units** from his blade (worst
2.40). The root cause was confirmed separately by measuring the old raycast, and the watchdog was
confirmed by burying him at (46, 22) and watching him come back out.

### The fight was almost entirely SILENT

⚠️ **All six of the boss's `AudioClip` slots were NULL**, and `SfxManager.PlayOn` with a null clip is
a silent no-op — so the volley, the recall, the dash, the katana and the blink made no sound at all.
This is the same 75-empty-slots pattern `AudioInventory.md` documents, in a brand-new encounter: the
slots exist, so the wiring *looks* done.

**Every sound is now procedural by default and the Inspector fields are OVERRIDES.** A boss must not
be able to ship mute because someone forgot to drag a file in.

`ProcSfx` gains a **NINJA family — the sixth**, and it is built on the one axis none of the others
use. Every existing family is separated by material (magic = harmonic bells, metal = inharmonic bar
modes, stone = noise and sub, paper = no pitch at all) or by envelope or by pitch motion. These
sounds are all the *same* two materials — steel and air — so material cannot tell them apart.
**Flutter rate can:**

| clip | flutter | why |
|---|---|---|
| `ShurikenThrow` | **62 Hz** | a small star spinning too fast to count |
| `KatanaThrow` | **11 Hz** | a long blade going end over end, slowly |
| `KatanaPlant` | 7 Hz tremolo | buried in stone and quivering |
| `NinjaDash` | **none** | a body, not a blade — the absence is the message |

All four are literally the same `Whip` call, the way the six UI sounds share `WoodTap`: a family
assembled from hand-tuned one-offs drifts apart the moment anyone retunes one.

⚠️ **THE FLUTTER IS THE WHOLE IDENTITY OF A THROWN SHURIKEN**, and it is what the old whoosh was
missing (designer: *"i dont like the current shuriken throw whoosh"*). A plain swept-noise whip is a
generic swing — `FreefallBlade` and `ZombieSwing` already are one, and a third would just be a
quieter copy. A star chops the air four times per revolution, and that chopping is the only reason
the sound says *spinning object* rather than *something moved fast*. The player's `AirSwish` override
was cleared so this is what plays.

Also added: `ShurikenStick` (which deliberately quivers on after the impact — it is the cue that
there is now ammo on the floor), `ShurikenCatch` (two ticks, no tail, because you collect several in
a couple of seconds), `ShurikenRecall` (seven whips staggered but **converging on one arrival**, over
a rising sub — the pull), and `NinjaBlink` (⚠️ **not electric**: the jump-SFX hunt already settled
that a swept sci-fi teleport is wrong for a candlelit dungeon, so it is air collapsing *inward*, the
inverse of every other whip in the family).

### The stars were invisible

Designer: *"they kind of blend in with the background and their traces are almost unseeable"*, and
separately, that the picked-up card had no trail.

**On the trail: the salvaged card was never the problem.** `Borrowed Steel` and `Shuriken` both run
`PlayerController.ThrowShuriken` → `Shuriken.Spawn`, so it always had exactly the same streak the
other card had. That streak was just close to invisible for both of them — one flat grey
`(0.34, 0.36, 0.43)` at alpha 0.40, against dungeon stone measured at `#444548`. **The trail was
drawing itself in the wall's colour.** The reasoning behind it ("a pale streak would be the loudest
thing on screen") was sound and the result was not.

The fix is a **gradient**, not "brighter": hot for the first 35% and gone by the end. A short hot head
reads as speed; a long even streak reads as an object.

And a **keyline** — a copy of the star one order behind it, 30% larger, in a bright colour.
⚠️ **It is the same silhouette, not a soft halo.** A feathered ring at the blade radius was built here
once and rejected because it read as a UI selection circle; a smooth gradient behind hard pixel art
is a different drawing language. Scaling the sprite keeps one language, and it spins for free.

**The star is the only object in the game that is an ATTACK and then becomes AMMO**, and the player
has to read which it currently is at a glance, mid-fight, across an arena. So it carries three states
and no more — all from the game's existing accents, spending no new hue:

| | colour | rim | meaning |
|---|---|---|---|
| his, in flight | **Wound red** | 1.30× | dodge it |
| planted in the arena | **Torch gold** | **1.55×** | take it |
| yours, in flight | **Shift cyan** | 1.30× | yours |

⚠️ **The player's star was WHITE first, and photographed as a solid glowing lozenge** with the
four-bladed silhouette washed clean out of it — legible, and no longer legible *as a shuriken*. Cyan
carries far less luminance at the same alpha, so the dark blade survives on top of it. It also does a
second job: in this fight his stars and yours are in the air simultaneously all the time, and the one
thing the player must never misread is which of them will hurt them.

⚠️ **The planted rim GROWS as well as changing colour.** Side by side, red-in-flight and gold-on-the-
floor are both "a dark star with a warm edge" and are only as different as two warm hues ever are at
thirty pixels across. Widening the lit edge makes a planted star read as something *glowing* rather
than something *outlined* — two signals instead of one.

## 7d. Third playtest, 2026-08-22 — the HUD, the gate, and a telegraph that lied

### ⚠️ THE HAND WAS COVERING THE FIGHT, AND THE ARENA IS WHY IT SHOWED UP HERE FIRST

Designer: *"the HUD does not mix well with the full room screen … the hover panel always gets in the
middle of the screen, so much so that sometimes i cant see my character or the boss."*

**Measured, and the numbers are the argument.** This arena runs at `orthographicSize` **10** instead
of the usual 7 — the boss teleports, so the whole room has to be on screen — which means the world
draws smaller while the HUD does not. In here the player is **91 canvas px tall** and a hovered card
was **288 px**: over three times the height of the character it was covering, dead centre, rising
into the play area from a 1000×200 hover zone.

**Two structural faults, not one cosmetic one:**

1. **THE MOUSE WAS ALREADY BUSY.** Aimed cards — Shuriken, Borrowed Steel — fly at the CURSOR. A hand
   that must be *hovered* before it can be read makes "see my options" and "aim my shot" the same
   input, fighting each other, in a boss fight. Cards are playable on 1/2/3 already, so the hover
   requirement bought nothing and cost that.
2. **IT ROSE INTO THE PLAY AREA.** A drawer that hides is only tidy if the thing it uncovers matters
   more than the thing it covers. Here it uncovered three cards and covered the boss.

Fixed by making the hand a **rail**: permanently visible, resting at the bottom edge, no pointer
involved, cards at 0.55 instead of 0.80. `SetLocked` still tucks it fully away, which is the one case
where hiding is right — a full-screen panel is up and the hand is not playable anyway.

Measured after, in canvas px with y up from the screen bottom:

| | before | after |
|---|---|---|
| resting card | hidden | 165 px, y 10–175 |
| hovered card | 288 px | 183 px, y 16–199 |
| arena floor line | y 135 | y 135 |
| player | y 135–226 | y 135–226 |

⚠️ **The number that matters is the last one.** The hovered card used to extend **139 px above the
player's head**; it now stops **27 px below it**. It still overlaps his legs, which is unavoidable for
anything anchored to the bottom — but it no longer hides him or the boss.

⚠️ **The drawer's raycast target is OFF now.** It existed to detect hover, and it was a 1000×200
invisible input-eating rectangle lying across the bottom centre of the screen for the entire run.
With the rail always open there is nothing to detect. (Gameplay never consulted the EventSystem, so
this was not blocking casts — but it was exactly the shape of thing we were trying to remove.)

**The knobs, if this wants tuning by eye:** `HandUIDrawer.restY` (−9) is how low the rail sits, and
the `HandController` scale (0.55) is card size. Going below ~0.5 starts to cost the readability of the
rules text on a flipped card, which is the whole reason the flip exists.

### The boss gate: only the bars, on the door that was already there

Designer: *"the exit door is very ugly. its smaller than the exit door behind it, which is still
there … i meant that we could perhaps use the bars on that prefab only. not the background lighting,
not the frame or anything."*

Correct on every count. The first version instantiated the pack's **whole**
`PF Dungeon Props - Door Iron Fence 01` over the exit — which brings its own stone frame, its own
shadow, a bright cyan sky panel, a light shaft, a Light2D and a dust emitter. Dropped in front of a
door that already has an arch, that is two archways, one smaller than the other, lit differently.

Now it is **one sprite** (`TX Dungeon Props - Door Fence 01 A`) owned by `ExitDoor` and driven by the
lock the door already tracks. Any room that seals its exit gets the visual for free, including bosses
nobody has written yet. `NinjaBoss` no longer has gate fields at all — sealing is one call.

⚠️ **Three traps, all paid for in one sitting:**

- **`localScale` is RELATIVE.** The fit ratio is computed from WORLD bounds, but the door's `Visual`
  is itself scaled 1.32, so assigning a world ratio as a local one squared it and the bars came out
  ~1.7× oversize, hanging through the floor.
- **THE SPRITE'S PIVOT IS TOP-CENTRE (0.50, 1.00)**, because a portcullis hangs from its top. So
  `transform.position` places the top EDGE, and setting it to the arch's centre put the whole grille
  exactly one half-height low. Backed out arithmetically from the sprite's own local bounds, never by
  re-reading renderer bounds after moving.
- **`StartCoroutine` on an inactive GameObject THROWS**, and the unlock deliberately runs from the
  boss's `OnDestroy` — which on a room change fires while the room is being torn down and the door is
  already inactive. An exception there aborts the rest of `SetLocked`, i.e. the one call whose entire
  job is to leave the room passable. It now destroys the bars outright when it cannot animate them.

⚠️ **The bars FADE as they rise, and that is the mask, not decoration.** These are world sprites with
nothing to clip them; bars that simply slid up would emerge above the arch and travel up the wall in
plain sight. The sound is the `ProcSfx` gate family, which its own header notes was written for a
portcullis and has only ever been used on a double door since.

⚠️ **A SpriteRenderer tint MULTIPLIES — it can only ever darken.** There is no tint that makes the
pack's dark iron brighter, so if the bars ever need to read harder the contrast has to come from what
is BEHIND them. That is precisely why the pack's own fence ships with a lit sky panel. Left at white
and judged on screen.

### The dash telegraph was lying, and it was lying in two directions

Designer: *"the indicator is sometimes mistaken. it shows in one spot, but the ninja boss sometimes
goes lower than what that indication showed 1 second before … also the trail is really ugly, its just
red and pretty bland."*

**VERTICAL.** `rb.linearVelocity` was written as `(dir * speed, keep Y)`, so gravity ran through the
coil AND through the dash while the strip stayed at the height it was drawn. Worse,
`KatanaFlightRoutine` chains straight into a dash after the blink **with no grounded check**, so the
whole move could begin airborne.

**HORIZONTAL.** He drifts BACKWARD by `leanBack` while coiling, and the loop then ran a full `lane`
from that retreated position — overshooting the drawn end every single time.

⚠️ **Fixing the drawing would have been the wrong repair. THE TELEGRAPH IS THE CONTRACT**, so the
attack is what got corrected: gravity off for the whole move, Y velocity zeroed, and the loop runs to
a **world-space terminus fixed before the wind-up** rather than a distance from wherever he ends up.

Measured on an airborne dash, tracked from the coil through the end of travel:

| | before | after |
|---|---|---|
| y error at the end | −1.99 (and −7.1 once gravity resumed) | **0.000** |
| y drift across the move | ~2.0 through the coil alone | **0.000** |
| x error | overshoot by `leanBack` | 0.131 |

⚠️ **AND GRAVITY IS NOW CAPTURED ONCE, IN `Awake`.** Three of his moves switch it off, and each used
to read "the current value" at the top of its own coroutine to put back later. That is a latch: a move
beginning while gravity is already 0 faithfully restores it to 0, and the boss floats for the rest of
the fight with nothing in the log. Every restore goes through `RestoreGravity()`, including from
`OnBossDied` and `OnDestroy`.

**And the look.** The old lane was a flat red rectangle; the first rebuild kept a faint filled band
behind a cut line, and photographed, that was *still* a red BOX with two crisp horizontal edges ruled
across the room — the same fault, quieter. A rectangle drawn around danger is the **generic reveal**
the UI doc warns about, wearing a costume.

The test that replaced it: *what does this object already mean in this game?* He is a blade travelling
in a straight line, and his visual identity is **afterimages**. So:

| | |
|---|---|
| **CUT** | a thin bright hairline at blade height — the path the EDGE takes. It reads because it is thin and bright, not because it is big and dim. |
| **STREAKS** | four hairline outriggers, thinning toward the edges and inset at the ends so the shape tapers. They state the same hit volume the band did, but read as SPEED LINES. |
| **NOTCH** | a tick at the terminus. The slab never said where he STOPS — which is the single most useful thing to know about a charge, and what makes baiting him into a wall a real play. |
| **GHOST** | ⚠️ a premonition of him standing at the end, resolving as he loads. Blue ghosts are where he WAS; this red one is where he WILL BE. Same mechanism, opposite meaning, no new art. |

Nothing GROWS — the geometry is full-length on frame one, because a telegraph's job is done in its
first frame; only the intensity and the premonition change as he commits.

### Noted, not actioned

**The boss health bar sits on top of the relic bar.** The designer flagged it and explicitly said to
wait — both are top-centre `GameplayHUD` elements and it is a HUD layout question rather than a boss
one.

## 7e. Setup finished, 2026-09-06 — he is a real boss node now

### ⚠️ `roomPrefabs` HAD BEEN WIPED A FIFTH TIME, AND IT BLOCKED EVERYTHING ELSE

Found while wiring the arena in: `LevelManager.roomPrefabs` was down to **ONE** entry —
`herangibisi`, a scratch room, **not the hub**. HEAD has twelve. Every symptom CLAUDE.md predicts was
live and none of them names the cause: no sandbox first room, so no quest board and no forge; the
same room forever; and a run that can never reach a boss node the way a player would.

Restored by resolving the twelve `(guid, fileID)` pairs out of `git show HEAD:…SampleScene.unity`,
**never by re-picking prefabs by filename** — a prefab reference is a pair, and re-picking a
same-named asset is how a room silently reads as `null` in the Inspector while looking assigned. All
twelve resolved, hub at index 0, and all twelve still satisfy the room contract (CameraBounds +
GirisNoktasi + ExitDoor), checked on the way past.

**This is the fifth wipe. It has never once announced itself.** If anything about a run feels wrong,
read that list before debugging the map or the camera.

### The arena joined the boss pool

`bossRoomPrefabs` is now `[BossRoom, NinjaArena]`. `PickBossRoom` draws without repeating within a
run, so verified over **200 simulated runs of two boss nodes each: every single run met both bosses,
neither repeated.** `finalBossRoomPrefab` is untouched — the finale is meant to be unique and is not
built, so it still falls back to the Moss Knight.

### ⚠️ KILLING HIM PAID NOTHING

`OnBossDied` opened the exit and stopped. No loot, no relic, no celebration — the biggest fight on
the map had a quieter death than a zombie. Now:

- **14 gold + 5 shift crystals**, the same `Gold New` / `ShiftCrystal` prefabs the Moss Knight drops
  (read off his prefab rather than guessed, so the spoils are literally the same substance).
- **`BossRewardCue.Schedule(2, 2.6s)`** — the boss relic banner.
- Both reuse the Moss Knight's classes **unchanged**. Measured live: 12–14 gold and 5 crystals hit
  the floor, the exit unlocked, the bars raised, and `Time.timeScale` returned to 1 with the banner
  holding the only pause.

⚠️ **He was fighting to the LEVEL's music.** `StopBossMusic()` was already being called on his death —
stopping something that had never started. That reads as correct in a diff and is silent in play;
`PlayBossMusic()` now runs from `StartFight`.

⚠️ **THE DEATH BURST WAS HARDCODED ACID GREEN**, which is the Moss Knight's entire identity. It is a
parameter now (defaulting to the green, so he is untouched) and the Ninja comes apart into the **cold
blue of his own afterimages** — the colour this fight spends three minutes teaching you means *where
he was*. The gold stays gold in both: it is the LOOT, and recolouring it per boss would make the
reward read as a different substance.

### Two test artifacts worth not re-learning

⚠️ **KILLING A BOSS IN THE SAME CODE CALL (`eval`, formerly `execute_code`) THAT SPAWNS THE ROOM PROVES NOTHING.** `Start()`
has not run, so he has not subscribed to `OnDied` and has not sealed the exit — the death sequence
simply never fires and the result looks exactly like broken wiring. The first run of this test
reported zero loot with everything correctly assigned. Spawn in one call, kill in the NEXT.

⚠️ **A live, enabled `LevelManager` whose static `instance` is NULL is a domain reload**, not a bug —
compiling and then entering play mode clears statics while scene objects survive. Stop and restart
play mode rather than debugging the manager.

## 7f. Tuning pass, 2026-09-07 — the volley, and his own health bar

### ⚠️ HE THREW ZERO SHURIKENS ON FLAT GROUND

Designer: *"the boss throws the shurikens not very frequently, which makes it hard for the player to
kill the boss."* Measured over 2000 attacks with the loop's own branches, on flat ground: **the volley
fired 0 times.** Not rarely — never.

`FightLoop` had it as the final `else`. He repositions to `preferredRange ± repositionJitter` = **4.5
to 11.5 units**, and every one of those is inside `dashRange` (**15**), so the dash branch accepted on
every single pass and the fallback was unreachable. Stars only ever appeared when the player stood on
a ledge (`dy > 2.5` fails `level`) or ran past 15 units.

⚠️ **THIS IS THE FIGHT'S ECONOMY, NOT A TUNING NUMBER.** §4's whole premise is that HE ARMS YOU — a
player who walked in with no damage cards has no other source of damage — and §4 calls the recall
"the clock the fight runs on". **A clock that depends on losing a range check is not a clock.** The
volley is now a guaranteed periodic event (`volleyInterval`, 5.5s) with first refusal, and the rest of
the kit fills the gaps. Re-measured: **800 volleys in 2000 attacks, one every 6.7s, ~45 stars/min.**

`volleyInterval` is a floor, not a cap — the volley still also fires as the fallback when nothing else
applies, which is what keeps him throwing at a player camped on a ledge.

### The health bar was the Moss Knight's, worn by a ninja

Designer: *"the boss health bar is not good at all. i want to customize it to represent the ninja."*
Three separate faults, and only one of them was colour:

1. **BOTH BOSSES POINTED AT THE SAME PREFAB ASSET**, whose every default is his — verdigris fill,
   pale acid chunk, oxidized bronze frame, and `bossName` literally defaulting to "The Moss Knight".
   With ~10 bosses planned that scales badly. There is now `BossHealthBar_Ninja.prefab`; the shared
   `bossName` default is cleared, because a baked-in default is a lie waiting for the next boss.
2. **IT OVERLAPPED THE RELIC BAR** — the thing the designer had flagged earlier and told me to defer.
   The relic sockets occupy canvas y −16…−68 and the bar started at −54. `topOffset` is now **84**.
3. **IT WAS A GLOSSY WEB WIDGET** — chunky bronze border, a white bevel strip across the top, a shadow
   across the bottom. The exact "competent, safe, screams AI" look the FlatUI/Salvage work exists to
   kill, and the loudest thing on a dark dungeon screen.

⚠️ **THE FIX FOR (3) WAS NOT A NEW LOOK — IT WAS THE ONE THE PLAYER'S OWN BARS ALREADY USE.**
`ResourceBarUI` was converted long ago and speaks a specific vocabulary: soft shadow, recessed track,
discrete segment **cells** with real gaps the track shows through, and a chamfered `FlatUI.Outline`
frame. The boss bar was the last readout in the game still speaking the old language. Consistency
lives in the treatment, so this is now the same object as your health bar, handed to the enemy.

The fill is also **real cells now, not one Filled image with notch ticks drawn over it** — so the bar
empties plate by plate and the gaps stay dark at every level, instead of a single edge sliding under
decorative lines.

**His identity, spending no hue** (the budget is spent — §1 of the UI skill):

| | Moss Knight | Ninja |
|---|---|---|
| segments | 10 fat plates | **22 fine ones** |
| height | 26 | 22 |
| fill | verdigris | **cold steel** `(0.74, 0.80, 0.88)` |
| damage chunk | pale acid | **Wound red** |
| frame | bronze | dark iron |

⚠️ **SEGMENT COUNT IS IDENTITY, NOT DECORATION.** He is the glass boss — 160 HP against high output —
and many fine plates read as a fragile thing where few fat ones read as armour. It is the cheapest
inversion available and it costs no colour at all.

⚠️ **The damage chunk is the one place his palette belongs on the HUD.** It is what the player sees
every time they connect, and the fight has spent three minutes teaching them that red means he is
cutting them. Turning that around — *you* cut *him*, and it shows red — is the payoff. The fill is
his blade, not his blood.

Also fixed: the canvas was the **only one in the game** with `matchWidthOrHeight = 0.5` (every other
is 1/HEIGHT, because the camera is height-anchored), so at 21:9 it scaled differently from the relic
bar sitting under it. And the name now routes through `UIType` instead of a local font field — a
per-screen font reference is exactly how the character select once shipped in Liberation Sans.

### ⚠️ TWO MEASUREMENT TRAPS THAT COST MOST OF THIS SESSION

**`Time.timeScale = 0` FREEZES ANY `deltaTime`-DRIVEN FADE-IN.** The bar fades up via
`CanvasGroup.alpha` on `Time.deltaTime`. Freezing time to photograph it caught the fade at **42%**, and
every colour measured off that capture was 42% of its authored value — which read exactly like a
rendering bug and sent me hunting a non-existent overlay. Proved by putting identical white squares on
the bar's canvas and the main canvas in the same frame: main `(1.00,1.00,1.00)`, bar `(0.42,0.41,0.41)`.
**When freezing time to photograph UI, force the fade to completion first.**

**The damage chunk cannot survive a tool-call boundary.** It holds 0.35s and drains at 0.5/s, so every
screenshot taken "just after damaging" is really a screenshot of it already gone — which reads as the
chunk being broken. Drive the fields to an exact state and photograph that.

**And, for the third time this project: killing a boss in the same `eval` (formerly `execute_code`) call that spawns the
room proves nothing.** `Start()` has not run, `CurrentHealth` is still 0, and `TakeDamage` kills it
instantly. Spawn in one call, act in the next.

## 8. Arena

⚠️ **THE CAMERA DECIDES THE SIZE BEFORE ANYTHING ELSE DOES.** This boss can teleport off screen and no
other boss in the game can. The camera is height-anchored at `orthographicSize` **7** — 14 world units
tall, ~25 wide at 16:9 — and the Moss Knight's 58×23 arena is simply panned around. Fine for something
that walks; fatal for something that blinks.

**The whole room fits one camera view, and the camera never moves.**

- ⚠️ **ONE `CameraBounds` zone, never two.** A zone transition mid-blink loses the boss entirely.
- ⚠️ **PREREQUISITE, DOES NOT EXIST YET: a per-room camera size.** `CameraFollow` has no lever for it.
  At `orthographicSize` **10** the view is 20 tall × ~35 wide at 16:9, which holds the **34 × 18** grid
  with margin. Build this before the arena is worth testing.
- At 4:3 the view is only 26.6 wide, so the camera still travels horizontally there. 16:9 and wider is
  locked, vertically locked at every aspect.

⚠️ **THE CAMERA LOCK AND THE JUMP BUDGET TOGETHER CAP THE ROOM AT THREE STANDING LEVELS.** The first
draft had four tiers plus plinths. Four rises of 4 with the **5 tiles of clear air above a launch
surface** Law 2 requires needs ~24 rows — well past the 20 units the locked camera can show. Anything
that adds a tier has to give up the camera lock, and the camera lock is the more important of the two.

**Thesis: safety and space are inverted.** The floor is where you can run — and it is his longest dash
lane. The ledges are out of that lane — and they are corners. Every tier is a trade and none is
correct for long, which is what keeps you moving, which is what spends Shift.

**Built as `Assets/LevelTexts/NinjaArena.txt` → `Assets/LevelGenerated/NinjaArena.prefab`.**
48 × 24 grid, of which the **playable interior is only 30 × 15** (cols 9-38, rows 4-18). Everything
else is a deliberately thick rock frame.

⚠️ **THE FRAME IS THICK BECAUSE A LOCKED CAMERA SHOWS A FIXED AMOUNT OF WORLD REGARDLESS OF THE
ROOM.** At `orthographicSize` 10 the view is 26.7 wide at 4:3, 35.6 at 16:9 and **46.7 at 21:9**. The
first draft was 34 wide and showed **six units of undressed void** past the outer wall at ultrawide —
the exact "you can see straight past the art" failure the levels doc warns about, arrived at from the
opposite direction (usually it is a missing `CameraBounds`; here the camera was working perfectly and
the room was too small for it). The frame is sized to cover the widest view.

⚠️ **THE `CameraBounds` ZONE IS BOUND TO THE INTERIOR, NOT THE GRID — and the importer will not do
this for you.** It auto-sizes the zone to the whole grid; it is resized after import to **30 × 15
centred on (24, 12.5)**. That is what makes `CameraFollow` *centre* (and therefore lock) rather than
clamp. **If this room is ever re-imported, redo it or the camera silently starts roaming again.**

Measured on the built prefab:

| aspect | view | horizontal | shows X | outside the room? |
|---|---|---|---|---|
| 4:3 | 26.7 × 20 | clamped ±1.67 | 9.0 – 39.0 | none — rock |
| 16:9 | 35.6 × 20 | **LOCKED** | 6.2 – 41.8 | none — rock |
| 21:9 | 46.7 × 20 | **LOCKED** | 0.7 – 47.3 | none — rock |

Vertically locked at every aspect (shows Y 2.5 – 22.5). **Validator: PASS, 52 standable / 52 reachable
(100%), rock 63%.**

```
################################################
################################################
################################################
################################################
#########..............................#########
#########..............................#########
#########..............................#########
#########..............................#########
#########..............................#########
#########..............................#########
#########..............................#########
#########............######............#########  centre island  x21-26
#########..............................#########
#########..............................#########
#########..............................#########
#########..########..........########..#########  low ledges    x11-18 · x29-36
#########..............................#########
#########..............................#########
#########..S......+..........+.....X...#########  spawn · crystals · exit — all on the floor
################################################
################################################
################################################  floor — one unbroken flat run
################################################
################################################
```

**The climb, and why it is only three levels.** Floor → low ledge is a **4** tile rise; low ledge →
centre island is another **4**. Measured jump apex is **4.9**, so both are inside budget with room to
spare, and the island keeps **6** clear rows overhead. Horizontal gaps to the island are 2 tiles
against a flat reach of ~12 — deliberately generous, because this is a dodging arena and the climb
must never be the thing that kills you. Descent is free off any inner edge.

⚠️ **THE LOW LEDGES DELIBERATELY DO NOT TOUCH THE SIDE WALLS.** The 2-tile chutes at x9-10 and x37-38
are escape hatches. Without them a dash along a ledge pins the player against the wall with no way off
but running *through* him — see rule 5.

Boss and katana are **not** in the text file (no markers exist for them): he starts dormant on a beam
or the right ledge, his katana embedded in the floor at centre, ~x24.

**The arena rules, each with its reason:**

1. ⚠️ **THE EXITDOOR IS ON THE FLOOR. NON-NEGOTIABLE.** Jumping costs Shift, so **a player at 0 Shift
   cannot jump at all** — and this is the one room in the game where finishing at 0 is likely. An exit
   up a ledge is a softlock wearing a victory screen. For the same reason **the floor is one unbroken
   flat run end to end**: a broke player must be able to *walk* out.
2. **No pits, no acid, no spikes.** The deliberate inverse of the Moss Knight's arena. The danger here
   is entirely the man. A cardless player is already fighting with scraps; terrain that also wants
   them dead makes the fight unreadable rather than hard.
3. **Both side walls solid floor-to-ceiling — no open ends.** The dash must terminate against
   something; that impact is his overcommit and the player's punish window. An open end means the
   attack has no downside.
4. **Keep the central volume empty.** That column of air is where he flies and where you shoot him for
   crystals. No pillars, no foreground occluders, nothing that can hide him mid-leap.
5. **No ledge may be a dead end — every one needs at least two ways off.** Being cornered on a ledge
   with a dash incoming and no escape but running *through* him is an unfair death, and it will happen
   constantly if any ledge lacks one. This is why the low ledges float clear of the side walls: the
   inner end drops to the floor or jumps to the island, and the outer end drops down a chute.
6. **Stick points everywhere** — walls, ceiling, beams, ledge tops, floor. The katana lands where it
   lands; an arena that cannot accept it anywhere gives the teleport a "sometimes it doesn't work"
   feel.
7. **Two Shift crystals on the floor.** Area puts the room at ~4 by the usual ~7-per-1000-tiles band,
   but the airborne drops carry the real supply. The floor pair is a lifeline for a player circling
   the drain — reachable without a single jump.

**Room contract, as always:** `CameraBounds` + `GirisNoktasi` + `ExitDoor`.

**Props (designer's pass):** crates and barrels at the wall bases — these are the step up to the low
ledges, so they are load-bearing, not decoration. Chains and beams at the ceiling as katana anchors.
Everything else sits behind the play plane (`PlayPlane.Apply` handles depth on spawn — do **not**
hand-tune prop Z). Nothing tall in the middle.

## 9. The opening beat

**His katana starts embedded in the floor at centre.** He is perched on a ceiling beam or the high
right ledge, dormant — `startDormant` + a `BossFightTrigger` a few tiles past the spawn, same pattern
as the Moss Knight, so the player walks in and reads the room first.

Cross the line and **he blinks to the katana and pulls it out of the floor.**

That is the awaken, and it teaches the teleport-to-blade mechanic in a moment that costs the player
nothing — so the first time he throws it at a wall, they already know what is about to happen. The
fight explains itself before it starts.

## 10. Implementation map

- **`NinjaBoss`** (new) — state machine picking between the four attacks by range + cooldown, on the
  `MossKnightBoss` pattern. Uses `EnemyHealth` for HP/damage/death so cards work for free.

- ⚠️ **THE PRESET SHIPS WITH LEFTOVERS THAT MUST BE STRIPPED — the same ones the Player prefab was
  purged of.** `PF Pixel Character - Ninja` arrives carrying **three SOLID bone colliders** (capsules
  on `Rig Spine1`/`Rig Spine2`, a circle on `Rig Head`) parented under the boss's Rigidbody2D, which
  make his hitbox **animation-dependent** and cost a physics rebake every frame; a stock **shuriken in
  the Weapon Slot** with its own Kinematic Rigidbody2D and trigger collider; and the pack's
  `PixelCharacterController` / `PixelCharacterInputMouseAndKeyboard` / `Rigidbody2D` / `BoxCollider2D`
  on the root. All removed. **Verified end state: exactly 1 Rigidbody2D, 1 Collider2D, 1 Animator.**
  - ⚠️ **Strip in dependency order** — input requires controller, controller requires Rigidbody2D.
    Removing the body first is simply refused, silently leaving the pack's physics on the boss.
  - ⚠️ **`AnimationEventReceiver` must come off the Animator child** (it NullRefs on the pack's
    footstep events) and `PlayerAnimEventSink` goes on in its place, or ~20 events per second log
    "has no receiver". The sink is misnamed for this use but is genuinely safe on a non-player: it
    null-guards its `GetComponentInParent<PlayerController>()` and every other method is empty.

- ⚠️ **`AttackSpeedMul` IS RESTORED TO 1 IN THREE PLACES** — `EndThrowPose`, `OnBossDied` and
  `OnDestroy`. It is a GLOBAL animator parameter on a controller the PLAYER also uses, so a boss that
  dies mid-throw would otherwise leave every character's attack running at 2.2×. Verified reading 1
  after a completed volley.

- **Observed in testing, not yet a problem but worth knowing:** a boss standing on a ledge throwing
  steeply DOWN at a player below buries most of the volley in his own floor within ~1.5 units of his
  feet — collectible in theory, but you would have to stand on him. It did not matter in the arena
  as designed (three broad tiers, lots of open air) and his movement does not exist yet. If it bites
  once the dash and leap land, the fix is to refuse a stick within ~2 units of the thrower.
- **Shuriken pickup** — `BossShuriken.cs`, house pattern (built entirely in code, no prefab).
  ⚠️ **It must not outlive the room** — it carries `TemporaryObject` for exactly that reason.

- ⚠️ **TERRAIN IS DETECTED BY RAYCAST, NOT BY TRIGGER, and that is load-bearing.** At 15 u/s a fixed
  step covers ~0.3 units, so a trigger test can straddle a 1-tile wall entirely — a star that tunnels
  out of the arena is ammo the player can never reach. Sweeping from the previous position also
  yields the exact impact point and normal, which is what lets it embed instead of stopping in
  mid-air near a wall. It also means sticking does not depend on how `Projectile` is configured
  against `Ground` in the layer collision matrix.

- ⚠️ **ASSIGNING A `Vector2` TO `transform.position` SILENTLY SETS Z TO ZERO.** The raycast returns a
  `Vector2` impact point, so embedding a star yanked it off the play plane (−2) to 0 and it rendered
  *behind* the arena's props. The implicit conversion is perfectly legal, so nothing warned — it was
  caught only by reading the transform back in play mode. Stars are now snapped to `PlayPlane.Z` on
  spawn and carry Z through by hand on stick. **Physics2D ignores Z entirely, so this is purely a
  sorting bug — which is exactly why it is invisible until you look at it.**
- **The quiver card** — a `RuntimeCard` conjured like Stagger: retained through Recall, over hand
  capacity, routed to no pile, evaporating at 0 charges.
- **Layer decision (OPEN):** `groundLayer` (2056) includes **Enemy(11)**, so a boss on layer 11 can be
  **stood on**. That is either a nice Pogo Boots interaction or an absurdity on a ninja. Decide
  deliberately — do not inherit it by accident. Whichever is picked, detect him via
  `GetComponentInParent<EnemyHealth>()` rather than a layer mask.
- **Body mass 500**, like every other enemy, so the player cannot shove him. The dash should ignore
  solid collision with the player.
- ⚠️ **Untargetable rule (PROVISIONAL, designer unsure of timing):** *untargetable because he is
  ABSENT, never untargetable because he is invulnerable.* Mid-blink he is not there, which lasts a
  fraction of a second and reads as the move. An armour phase on a boss you can already barely catch
  is just a longer fight — that is what the Moss Knight's "always damageable" rule ruled out.
- **Boss health bar:** `BossHealthBar.cs` + prefab, assigned to the boss; clear `EnemyHealth.
  healthBarPrefab` so the small floating bar does not also draw.
- **SFX:** a runtime 2D `AudioSource` + `SfxManager.PlayOn`, same as the Moss Knight (2D so it carries
  across the arena and the sliders can exceed 1 for headroom). Clip slots for throw, recall, dash,
  blink, katana-stick, hurt, death.
- ⚠️ **`CameraShake.Shake` is `(INTENSITY, DURATION)`** — the old Gate code had it reversed for months.
- ⚠️ **`EnemyHealth.Die()` fires `OnDied` and destroys the GameObject in the SAME frame.** Anything
  that must outlive the death (VFX, loot) runs on its own object — see `BossDeathVFX`.

## 11. OPEN — not decided

1. ~~**His name.**~~ — **"The Quiet Hand"** (designer, 2026-09-06), explicitly *for now*: a working
   name they expect to revisit, not a locked decision. It fits the roster logic — bosses are the
   castle's insane former owners, so a name that reads as a castle OFFICE is the shape to aim at, the
   way the Moss Knight is a knight. The rejected shortlist and why is worth keeping: **The Armourer**
   (explains the fight diegetically — he still hands you weapons and wants them back), **The Lender**
   (pairs with Borrowed Steel), **Nobody** (a ninja's identity is being no one; striking, but says
   nothing about the fight).
   *(The CARD is named — **"Borrowed Steel"**, cool-with-a-grin and it says what it is: not yours.
   "Returned to Sender" was the funnier alternative and reads worse as a held quantity.)*

   ⚠️ **"KAGEMUSHA" IS RESERVED, AND NOT FOR THIS BOSS.** The designer likes the name and has
   deliberately held it back: *"its not for a ninja, but more like a samurai … we will probably use
   it for the samurai sprite and add that one as a character and a boss later on."* So it is earmarked
   for a future **samurai**, which under the §1 premise means a playable character AND the boss made
   from them. Do not spend it on the ninja, and do not treat the two as interchangeable — the
   distinction is the designer's, and it is what makes the future samurai a separate roster slot
   rather than a reskin.
2. **The fourth attack.** Shift theft was rejected for this boss and reserved for a future
   thief-identity boss. The runner-up on the table is a **counter-stance** — hit him during it and he
   catches the attack and punishes — which is the only idea so far that makes *playing a card*
   dangerous, a genuinely nasty proposition in a deckbuilder. Cheap to build, less identity.
3. **Does the quiver survive the fight?** (§4) — **provisionally NO.** `DeckManager.OnRoomEnd` drops
   it, which is the safe default: it is conjured, not owned, and letting it ride into the run puts a
   card the player can never repair or bless into their deck (Stagger's "enters no pile" lesson).
   Keeping a real Shuriken as part of the reward is the nicer payoff and is **one line to flip**.
4. **Does the mid-map version differ from the finale version?** "He doesn't pay Shift" is the finale's
   thesis; the mid-map appearance may want to be a lesser cut of him.
5. **His relic.** `Rarity.Boss`. Not every boss gets one, but this one may. ⚠️ A blink-on-a-key is the
   obvious ninja drop and would have to be `isArt` — and **only one Art may be held at a time, ever**,
   across ~10 planned bosses. Spending it here is a decision about all of them.
6. **Standable or not** (§10).

## 12. Rejected — do not re-propose

- **Shuriken that hit the boss harder**, and **boss-only shuriken cards.** Both lie to the player about
  what a shuriken is and corrode the `CardAnchors.md` damage unit. §5.
- **A single stacking card that discards on play.** 20 charges = 20 Recalls. §4.
- **Shift theft on this boss.** Good idea, wrong identity — saved for a thief. §11.
- **A crusher or any other room-owned damage machine.** The boss owns that job now. §2.
- **A literal mirror of the player's kit for the finale.** §1.
