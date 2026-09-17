# Character + Boss Design — The Samurai / Kagemusha

**Status:** Designed 2026-09-16. **Nothing built.** Everything below is a PROPOSAL for the designer to
tear up — numbers are derived from `CardAnchors.md` but are not approved until they are assets.
Decisions marked **DESIGNER** are theirs already; everything else is Claude's draft.

| | state |
|---|---|
| Playable Samurai (`Assets/Resources/Characters/Samurai.asset`, preset + katana wired) | ✅ **built and play-verified 2026-09-17** |
| Signature card *Through and Through* (enum 22 · action · `LungeRoutine` · asset · aim preview) | ✅ **verified** — placeholder art (see below) |
| **Armour system** (`PlayerHealth` pool + damage order + HUD bar) — new to the game | ✅ **verified** |
| Trait *Full Plate* (+5 Armour on combat-room entry) | ✅ **verified** |

**Verified in play mode, 2026-09-17** (zero console errors across compile and the session):
- Samurai spawns with the katana in hand and the deck `Through and Through ×2 · Glass Parry · Leap`.
- **0 armour in the hub, 5 on entering the first combat room** — the sandbox gate holds.
- 3 damage on 5 armour → **2 armour, 100 HP, and `TookDamageThisRoom` flipped true** (a hit is a
  hit). 10 damage on 2 armour → 0 armour, 92 HP. **Stagger's `PayHealthCost(8)` on 20 armour → 20
  armour, HP −8** — the bypass holds.
- Through and Through against a **MeleeEnemy (Enemy layer)**: 40 → 20 HP, exactly one cut; the
  player started 2.5 units short of him and finished 1.4 units PAST him — it went through the body.
  **Zero collision pairs left ignored** afterwards. Shift charged 1.
- The armour bar draws as a thin steel plate directly above the health bar with its count. ⚠️ It
  sits tight against the top screen edge (about 5px clear at 1080). Not clipped, but worth an
  eyeball; the lever is `ResourcePanelHUD.armourGap` / `armourBarHeight`, or lowering the whole
  panel.

⚠️ **Not yet tested: the lunge against a Default-layer enemy** (a zombie, bat, Mimic). The
per-collider approach should not care about layer — that is the whole reason it was chosen over
the matrix — but it has only been measured against the Enemy-layer case.

⚠️ **The card's ART IS A PLACEHOLDER** — Freefall Blade's illustration, the same borrow Shuriken
shipped with. Its name plate is empty so the title types in correctly; only the picture is wrong.
The two references that could not be hand-written (a Cainos preset is a prefab VARIANT, so its root
fileID is Unity-computed) were assigned through the editor and are now in the asset.
| Per-character finale plumbing (`CharacterData.bossRoom`, `IMirrorBoss`) | ✅ built 2026-09-17 |
| Arena `KagemushaHall.txt` → prefab, `RoomCamera` 10, bounds bound to interior, trigger | ✅ built, in `bossRoomPrefabs` |
| `KagemushaBoss` — kneel, awaken, Draw, Overhead, Split, Sheathe, death | ✅ **built and smoke-tested** |
| `ShadowDouble` — mirror, strike, shatter, crystal | ✅ **built and verified** |
| `LaneTelegraph` — standalone lane warning (cut / streaks / notch / premonition) | ✅ |
| `BossHealthBar_Kagemusha.prefab` (12 steel plates, Wound-red chunk, lacquer frame) | ✅ |
| Death: loot, VFX, `BossRewardCue`, exit unsealed | ✅ verified |
| Relic *Body Double* | ❌ — the banner offers the existing boss relics |
| `ProcSfx` SAMURAI family | ❌ — sounds are BORROWED clips (see §8 "as built") |
| Arena dressing (props) | ❌ — the Hall is undressed rock |

**Smoke-tested in play mode 2026-09-17, zero console errors or warnings** (the designer is doing the
real ability testing): three identical figures kneel at the far end → crossing the trigger dissolves
two and he rises with his bar → a Split put two doubles out with three lanes drawn and a premonition
ghost → **touching an ARMED double took him 220 → 208 and paid +1 Shift** → hitting him during a
Sheathe swapped him with a Standing double (the double ended up exactly where he had been) → killing
him unbarred the exit, dropped 14 gold + 5 crystals, cleared the bar and the doubles, and raised the
Spoils banner with `Time.timeScale` held by the banner alone. Also: an unshielded player parked on
the trigger was killed by the first Split — the fight is not shy.

**Written standalone, NOT on the Ninja skeleton** (designer 2026-09-17). `KagemushaBoss.cs` shares
no code with `NinjaBoss.cs`; only the generic lessons were carried (gravity captured once, latches
released in `finally` + death + destroy, positions from transform not bounds, a stuck watchdog with
no exemption flag, exit fails passable). The lane telegraph was extracted as `LaneTelegraph` rather
than copied as a nested class.

**Deliberate simplifications in this build (§5 as designed vs as built):**
- **Draw is HORIZONTAL only**, at his height, hit-tested with `EnemyMelee.TryHit`. The doc proposed
  aiming it at the player; a ledge is therefore a Draw refuge and the Overhead is what answers it.
- **Split always resolves into a unison DRAW.** "Doubles perform his NEXT attack" (three Overheads)
  is not built — one unison shape is enough to judge the loop.
- **The Overhead leap runs at 2.5x gravity** for the flight only (`overheadGravityMul`), restored in
  a `finally`. At base gravity a 0.9s flight peaked under a unit — a hop, not a leap.
- ⚠️ **`finale` is set by `LevelManager` through `IMirrorBoss.SetFinale`** when the room spawned is
  the played character's own `bossRoom`. Forced through the testing hook while playing the Samurai
  it therefore reads `finale = true`; play the Wizard or Ninja to see the mid-map cut (one double,
  no twist).

He follows the rule the story pivot set (see `BossDesign_Ninja.md` §1 and the `bosses-are-characters`
memory): **one identity, shipped twice** — a playable character and the boss made from them. He is a
normal mid-map boss for the Wizard and the Ninja, and the Samurai's own **finale**.

---

## 1. Who he is

**DESIGNER:** the name **Kagemusha** was reserved for a samurai, explicitly *not* for the ninja
(`BossDesign_Ninja.md` §11). A *kagemusha* is a lord's body double — the man who stands in the
lord's armour so an assassin kills the wrong one. Under the premise that every boss is a former
owner of the castle who went mad inside it, that word does a lot of work for free:

> The samurai who owned the castle kept a double. Somewhere in the years since, one of them died.
> The one still here insists he is the original. So does his shadow. **Neither of them is sure any
> more, and the deed is in a name they both answer to.**

That is the whole character, and it is the mirror finale without needing a line of dialogue: you
play the Samurai, you climb to the top, and the thing waiting there is a man who cannot tell which
of you is real either.

**Tone (per CLAUDE.md):** sad *and* petty. He is not a tragic ghost; he is a has-been who kept a
stunt double and now argues with it about who gets the house. Keep the winks in the names (§9) and
the mechanics literal.

## 2. The playable Samurai

**A character is a starting deck and one passive trait, nothing more** (`CharacterData.cs` — the
active ability was built, playtested and cut; do not re-propose one).

**The fantasy:** commitment. The Wizard stands back and throws; the Ninja skips and redraws; the
Samurai **places one cut and does not flinch**. He is the roster's melee slot, and the deck should
make "stand your ground" a real option in a game whose currency is moving.

### Starting deck — 4 cards, like the other two

| card | why it is here |
|---|---|
| **Through and Through** ×2 | the signature — see below |
| **Glass Parry** | the samurai parry; also puts a Glass card in a starting deck, and Glass is the thinnest archetype (2 cards) |
| **Leap** | the one plain movement card, because his attack is also his dash (below) |

⚠️ Note what is *missing*: no Dash. Both existing decks carry Dash. His horizontal movement is
**inside his attack card** — that is the identity, and it is the first card in the pool where the
movement half and the attack half of the deck are the same card (`CardIdeas.md` lists that as an
empty shape: *"movement cards and attack cards never talk to each other"*).

### Signature card — "Through and Through"

*Cool-with-a-wink, and it says what it does: the blade goes through them, and so do you.*
(Alternative in the straight-up-fun register: **"Excuse Me"** — you pass through the enemy. Funnier;
reads worse on a card that deals damage. Designer's call.)

> **Lunge 5 units in the direction you face. Nothing can hurt you while you lunge. You pass THROUGH
> enemies, and every one you pass is cut for 20.** Stops at walls.

- **20 damage per enemy · 4 charges · 1 Shift · Rare.** Budget: 15 × 1.25 (4 charges) × 1.3 (melee)
  = 24 per cast; it can hit several, so the per-target number sits under that. Two enemies in a line
  = 40 for one Shift, which is the reward for reading the room.
- **DESIGNER (2026-09-16): invulnerable while lunging.** The first draft made it vulnerable to keep
  Dash relevant; the designer's ruling is that Dash is a basic starter card and its usefulness is not
  worth protecting at the cost of the signature feeling bad. So it is a Dash that cuts. ⚠️ **This
  means it also has to share Dash's i-frame rules exactly** — same invincibility window, same "can't
  be hit by a lingering hitbox on the exit frame" mercy — so a player who learned Dash is not
  surprised by the samurai version. Build it on `DashAction`'s routine, not beside it.
- Passing through bodies means **`ConflictFlags.LayerCollisionMatrix`**, so it and Phase block each
  other — correct, and `CardActionExecutor` enforces it for free. It also carries whatever flag
  Dash declares, so it and Dash cannot stack either.
- Grounded or airborne; horizontal only; gravity suspended for the ~0.2s of travel, restored in a
  `finally` (the pattern every dash in this codebase uses).
- Facing-driven, not aimed. The Ninja's star is aimed because the cursor is the point of a ranged
  weapon; a lunge in the cursor's direction would be a Dash that ignores facing. Read `isFacingRight`,
  never `localScale`.
- **Card face:** the canonical Freefall Blade frame with an empty name plate, `nameIsPaintedIntoArt`
  false. Until there is art, the icon-only face on the RPG icon pack (a sword icon) is the plan on
  record for exactly this situation.

### Trait — "Full Plate" (DESIGNER, 2026-09-16: armour, not knockback immunity)

> **Every room you enter, you put on 5 Armour. Armour you still have when you leave comes with you.**

The first draft's trait (no knockback, ever) was rejected as simple and boring. The designer's
replacement brings in a mechanic they have wanted for some time: **Armour — a second health bar that
sits on top of HP and empties first.** The Samurai is the character that introduces it.

**The trait, plainly:**
- Walk into a room → **+5 Armour**.
- Get hit → the hit comes off Armour first. Only what is left over touches HP.
- Leave the room with Armour still on → it **carries over**, and the next room adds another 5 on top.
- So a player who is never touched walks into room five wearing **25 Armour**. One who gets clipped
  every room lives at 5-ish. **The streak is the trait.** It rewards the thing the samurai fantasy is
  about — not getting hit — and it pays in the samurai's own material.

⚠️ **ONE RULE TO SETTLE — does a hit CHIP the armour or SHATTER it?** (§12.2)
- **Chip** (recommended): Armour is literally an HP bar on top, as the designer described it. A
  3-damage hit on 10 Armour leaves 7; next room you have 12. Simple, no special case, and it already
  produces "stacks if you never take damage" without a second rule.
- **Shatter**: any hit at all drops Armour to 0 — a streak that resets when broken, like the quest
  oaths. More samurai (posture broken = armour gone), much harsher, and a 3-damage slime touch
  wiping 25 Armour will *read as a bug* to a player who was told it is a health bar.

### The Armour system — what has to be true wherever it is built

Armour is **new to the whole game**, not to this character, so these are project rules from the
day it lands:

- **It is a pool with no regeneration and no cap.** Sources add to it; damage takes from it. Nothing
  refills it on its own — same philosophy as Shift.
- **Damage order is Armour → HP, always.** One place: `PlayerHealth.ApplyDamage`. Nothing else
  subtracts HP directly.
- ⚠️ **A hit absorbed by Armour is STILL A HIT.** The hurt animation plays, `OnDamaged` fires,
  knockback applies, the flawless-clear payout is lost, oaths break. Armour changes *what the hit
  costs*, not *whether it happened*. Otherwise "no damage" quests and Glass cards silently change
  meaning the moment anyone has 1 Armour.
- ⚠️ **Stagger's blood price BYPASSES Armour.** `PayHealthCost` already bypasses invincibility and
  the parry window for the same reason ("sometimes free" is worse than either); paying the Shift
  bill out of armour would make Stagger free for exactly the character who stacks it.
- ⚠️ **The trait grants on entering a COMBAT room only** — `LevelManager.IsCurrentRoomCombat()`,
  not every room. Hub, Foundry, Market and Well are sandboxes; pumping +5 on each visit is the
  umbrella rule broken from the income side.
- **Phoenix Cog** watches HP, not Armour. A lethal hit that Armour fully absorbs was never lethal.
- **HUD:** a second bar on the `ResourceBarUI` vocabulary, **above** the health bar, drawn as plates
  (it is armour) in steel, that **collapses to nothing at 0** rather than showing an empty track —
  the Wizard and Ninja never have any and should not carry a dead bar all run.
- ⚠️ **Once it exists it is a resource the rest of the game will want to spend and grant.** Relics
  (+Armour on kill), cards (*Break Glass* could trade Armour for damage), shop items, a Blompo
  blessing. That is the point of building it as a system rather than a Samurai-only counter — but
  none of that ships with him. He ships the bar and the rule.

**Name:** *Full Plate* hints at the effect and has the grin (it is a full plate of armour and it keeps
piling on). Alternatives in the same register: *Layered Up*, *Dress Code*. **Not** "Unbowed" — that
name belongs to the knockback idea and would mislead.

### As built (2026-09-17) — where each piece actually lives

| piece | file |
|---|---|
| the pool, `AddArmour`, damage order, chip/shatter flag | `PlayerHealth.cs` (Armour header + `ApplyDamage`) |
| the trait field | `CharacterData.armourPerRoom` |
| the grant | `PlayerController.OnNewRoomEnter`, gated on `IsCurrentRoomCombat()` |
| the bar | `ResourcePanelHUD.UpdateArmourBar` + `armourStyle` |
| the card's motion | `PlayerController.LungeRoutine` |
| the card's wiring | `ThroughAndThroughAction.cs`, registered in `CardActionExecutor.Awake` |
| the card's preview | `CardAimIndicator.UpdateDash(dim, lunge: true)` — reuses the dash ghost trail |

⚠️ **`LungeRoutine` deliberately does NOT touch the global layer-collision matrix, unlike Phase.**
Two reasons, both in the code comment: the matrix survives scene loads, so a coroutine killed
mid-flight leaves the player permanently intangible; and it would not have worked anyway, because
enemy layers here are inconsistent (zombies, bats, Mimic and ShieldEnemy on Default(0); MeleeEnemy,
RangedEnemy, Slime, Turret, Patrol on Enemy(11)), so ignoring Player↔Enemy would phase through some
enemies and bounce off the most common ones. It uses per-collider `Physics2D.IgnoreCollision`
against the bodies actually in the lane, restored in a `finally`.

⚠️ **`armourShatters` defaults to FALSE (chip).** Flip it on the Player prefab's `PlayerHealth` to
play the harsh version — that is §12.2, and it is a checkbox precisely so it can be judged by
playing rather than by arguing.

**What to check first, once the bridge is up:**
1. Does the armour bar appear above health on entering the first combat room, and is it in a sane
   place? Its Y is `healthRowY - armourBarHeight - armourGap`, which was **never looked at** — the
   whole HUD is calibrated by screenshot in this project and this one could not be.
2. Does armour actually eat a hit (take 3 damage on 5 armour → 2 armour, 100 HP), and does the hit
   still register as a hit (hurt animation, camera shake, flawless clear lost)?
3. Does Through and Through pass through a **zombie** (Default layer) as well as a MeleeEnemy
   (Enemy layer)? The zombie is the case the layer-matrix approach would have failed, so it is the
   one worth testing.

### Look

`PF Pixel Character - Samurai` preset + `PF Weapon - Katana`. Same rig, re-dressed by
`CharacterAppearance` as every character is. Accent is **palette-by-index**: slot 2 is **gold**
(`CharacterSelectScreen.AccentFor`), which happens to suit lacquered armour — no change needed.
⚠️ Verify the preset's actual outfit colours in-editor before assuming anything about them; nothing
in the code describes them.

## 3. The Kagemusha — the thesis, and why he is not the Ninja again

**The Ninja is evasion. The Kagemusha is COMMITMENT — and he makes YOUR commitment dangerous.**

| | The Quiet Hand (Ninja) | Kagemusha (Samurai) |
|---|---|---|
| his health is | evasion — 160 HP, glass | posture — ~220 HP, he stands and takes it |
| he moves by | blinking to a thrown blade | **never teleporting — he SWAPS with a double** |
| his range is | ranged volleys | melee lanes |
| he arms you by | throwing stars you pick up | **splitting — every double is a piece of him you can break** |
| he punishes | standing still | **playing a card at the wrong moment** |
| the fight's clock | the star recall | the split cadence |

⚠️ **He does not teleport. This is a hard rule** — teleport-to-marker is the Ninja's whole identity
and a second boss with it is a reskin. Every time the Kagemusha is "suddenly somewhere else" it is
because **he and a double changed places**. The double was already standing there in plain view. It is
the body-double trick, performed on you, in the open.

**Finale thesis, same as every mirror:** *he doesn't pay Shift.* Your lunge costs 1 Shift and is one
cut; his are free, chained, and there are three of him. The split is literally the thing you cannot do
— be in more than one place — and it costs him nothing.

## 4. How the fight works — in plain terms

Read this section first; §5 is the same fight in build detail.

### The one idea

**He makes copies of himself. The copies are hollow. If you run into a copy before it swings, it
bursts, and that hurts HIM — and it leaves a Shift crystal behind.**

*(The build sections call the copies "doubles" — same thing.)*

Everything else in the fight is arranged around that.

### How you hurt him — three ways

| | what you do | what happens |
|---|---|---|
| **1. Cards** | play any damage card at the real one (Fireball, Shuriken, Through and Through…) | normal damage. Works like any enemy. **Except** while his blade is lowered — see "the trap" below. |
| **2. Break a fake** | when copies appear, run into one **before it swings** (you have about a second) | the copy bursts. **12 damage goes to the real one.** A **Shift crystal** drops where the copy stood. |
| **3. Punish the counter** | after he answers a hit with his swap-and-cut, he is open for a moment | free hits with anything you have |

Way 2 is the reason a player with a deck full of movement cards and nothing else can still win. It
is his version of the Ninja handing you his stars: **every time he attacks with copies, he hands you
a way to hurt him.**

### How you get Shift

- **Break a fake → 1 Shift crystal.** This is the main supply during the fight.
- **Two crystals on the floor** at the start, reachable with no jumping (the lifeline for a player
  who arrives at 0).

### What costs you Shift

- **His Overhead** (he jumps onto you and sends a shockwave along the floor) — you jump it, and the
  jump costs Shift. That is deliberate: it is the tax his copies pay back.
- Moving between ledges to get out of his Draw lane.

**So the loop the player feels is:** dodge the Draw → jump the Overhead (spend Shift) → copies appear
→ break one (hurt him, get Shift back) → repeat. A good player ends the fight Shift-positive; an
average one slightly down.

### The trap — the only time attacking him is wrong

Sometimes he **lowers his blade and just stands there** for about a second and a half. It is loud
and obvious. **If anything hurts him while he is like that**, he instantly trades places with his
nearest copy and cuts you. If you already threw a Fireball a second ago and it lands during the
stance, that counts.

Nothing bad happens if you just wait it out. The stance exists so that "play a card now" is
sometimes the wrong answer — which is a real decision in a deckbuilder and nothing else in the game
creates it. Players with no damage cards never even notice it.

### A fight, walked through (first ~30 seconds)

1. You walk in. Three of him kneel at the far end, identical. Cross the trigger line → two dissolve.
   The one left stands up. *(That was him showing you the trick.)*
2. He walks toward you, stops at range, goes still with his hand on the hilt. A thin line draws from
   him to you across the floor — **the lane**. You step off it (or jump to a ledge). He cuts along
   the line. Miss.
3. He walks again. Then he **leaps** — straight at where you are standing — and comes down with a
   vertical cut. A shockwave runs along the floor both ways. You **jump** it. −1 Shift.
4. Two copies peel off him left and right. All three raise their swords. You **sprint into the
   nearest copy** — it bursts. His health bar drops 12. A crystal is lying there. You grab it. +1 Shift.
   The other copy swings and misses you.
5. He **lowers his blade** and waits. You have a Fireball in hand. You **don't** play it. He gives up
   the stance.
6. He goes still with his hand on the hilt again — lane — and now you have the tempo: you're off the
   line, you've got Shift, and you play Through and Through *through* him as he commits.

### The thing that changes at the top of the castle

Mid-map (when the Wizard or Ninja meets him) he makes **one** copy at a time and the copies are
see-through, so you can always tell which is real. In the **finale** (when the Samurai meets himself)
he makes **two**, and at 40% health **the copies become solid and identical to him**. Now you can't
tell. Breaking one still works — you just can't pick in advance which one is safe to run at.

⚠️ Numbers that hold this together — **12 is 12.** The damage unit is 15 (one Fireball); breaking a
copy is a body-check worth slightly less. Do not inflate it to shorten the fight; shorten the fight
with fewer HP or more frequent copies. **One crystal per copy** is the single most sensitive number
in the encounter — it decides whether Elite-tier risk is worth it — and it must stay ONE tunable.

**Cardless maths, provisional:** 220 HP ÷ 12 ≈ 19 broken copies. He splits every ~9s, so a perfect
no-cards player clears him in ~90s and a realistic one (breaking ~60%) in ~2.5 min. Comparable to the
Ninja's ~2 min.

## 5. Moveset — four attacks, build detail

Built from the pack's own clips. ⚠️ **The sword-swing `AttackAction` integers are NOT known** — 13 is
Throw and 14 is Cast; the build session must read `AC Character.controller` for the one- and
two-handed swing values rather than guess (the Animator-Int trap in CLAUDE.md).

| Attack | what you SEE | what it DOES | what you DO |
|---|---|---|---|
| **Draw** | he goes still, hand on hilt; a thin line draws from him toward you | ~0.7s later he cuts along that line, ~9 units, hurting everything on it. Ends at walls. | step off the line. Ledges are safe from it. |
| **Overhead** | he jumps, high, toward your position | lands with a vertical cut; a shockwave runs along the floor 6 units each way | jump the shockwave (costs Shift), or be on a ledge |
| **Split** | two copies peel off him to either side; all three raise their swords together | ~0.9s later all three perform the same attack (three Draw lanes, or three Overheads) | run into a copy BEFORE it swings → it bursts, 12 to him, a crystal drops. Touch it late → it cuts you (12). |
| **Sheathe** | he lowers the blade and stands still, ~1.6s, loud | if ANY damage lands on him during it, he swaps places with his nearest copy and cuts a wide arc (18). No copy up → he lunges from where he is. Short recovery after. | don't hit him. Wait. Then punish the recovery if you baited it. |

**Draw — the lane rules are the Ninja's, verbatim, because they were paid for:**
- Telegraph the LANE, not the wind-up. Draw it full-length on frame one; only intensity ramps.
- A world-space terminus fixed *before* the wind-up, gravity off for the move, `RestoreGravity()` in
  a `finally`. The telegraph is the contract — the attack conforms to the drawing, never the reverse.
- End on **no progress** (`DASH_STALL`), never on a single ray. Hard timeout underneath.
- Hit with `EnemyMelee.TryHit` — a box in front, against the player's real capsule.
- Aimed at the player rather than flat, so a ledge is not a permanent refuge (same reason the Ninja
  got a wall-run dive). The lane generalisation already takes a direction.

**Sheathe — the one that makes playing a card dangerous:**
- ⚠️ **This is the "counter-stance" recorded as the Ninja's runner-up fourth attack** (§11.2 there:
  *"the only idea so far that makes playing a card dangerous — a genuinely nasty proposition in a
  deckbuilder"*). It was set aside there as "cheap to build, less identity". On a samurai it *is* the
  identity — iaijutsu is waiting — so it is claimed here. Do not also give it to the Ninja.
- ⚠️ **A Fireball launched before the stance and landing during it WILL trigger the counter.** That
  is fair only because the stance is long (1.6s) and loud. Do not shorten it below the Fireball's
  flight time across the arena without re-checking this.
- **He answers ALL damage during the stance**, including a shattered double's shard. Simpler to build
  and it teaches one clean rule: *when the blade is low, touch nothing.* Split and Sheathe are
  mutually exclusive in the fight loop so this rarely matters, but the rule must hold when it does.
- Cardless players are unaffected by it — which is elegant: the counter punishes exactly the players
  who brought the most damage.
- The swap-and-cut has a short **recovery** at the end, like the Ninja's wall overshoot. That is the
  punish window for a player who baited the stance on purpose with a cheap card.

**Split — the identity, and the cheapest attack to build because it is not an attack:**
- Two doubles at `_Alpha` ~0.5 (see §8 — the rig cannot be tinted, only faded), no contact mark
  under them (a shadow casts no shadow). The real one is opaque with a warm mark.
- ⚠️ **A double that strikes hurts for real.** Otherwise the split is decoration and the "touch it
  before it draws" decision has no risk side.
- They mirror the real one's next attack — Draw lanes from three origins, or three Overheads. The
  telegraph rule applies to each: three lanes drawn, not one.

**Overhead — the Shift tax, and his answer to a camped floor:**
- Reuse the Comet-Dive-shaped landing (`OverlapCircle` at impact) plus a ground shockwave that is a
  travelling box, not a radius — it runs along the floor and stops at walls, so ledges beat it and
  the floor does not. The opposite of Draw, which ledges do not beat. **No tier is correct for long**,
  which is the arena thesis.
- Jumping it costs Shift. That is the point (§4).

**Movement between attacks:** he walks (pack run blend) to a jittered preferred range like the Ninja's
`RepositionRoutine` — never a statue. **He does not jump to reach you**; Overhead and the doubles are
how he gets height.

## 6. Mid-map vs finale (the "same vocabulary, further gone" dial)

| | mid-map (Wizard / Ninja meet him) | finale (the Samurai meets himself) |
|---|---|---|
| doubles per split | 1 | **2** |
| doubles look | translucent, unmarked | **translucent — until 40% HP, then OPAQUE and marked** |
| Draw | one cut, recovery | **chained ×3, no recovery** — he doesn't pay Shift |
| Sheathe | swaps with nearest double | swaps with a double on **every** attack, not only the counter |
| HP | 220 | 220 (the escalation is his behaviour, not his bar) |

⚠️ **The 40% twist is the whole finale and costs one number.** Below 40% the doubles go to `_Alpha`
1 and gain the same contact mark he has — now *nobody in the room can tell which one is real*,
including, per §1, him. Everything the fight taught for two minutes ("the faded ones are fake") is
revoked at once, with no new art. The cardless route survives: touching *any* of them before the
strike still shatters a fake; you just cannot pre-select.

## 7. Arena — the Long Hall

Rules inherited whole from the Ninja arena (§8 there), because they were measured, not guessed:

1. ⚠️ **EXIT ON THE FLOOR, floor one unbroken flat run.** A player at 0 Shift cannot jump; this is
   the room where finishing at 0 is likely.
2. **No pits, acid or spikes.** The danger is the man (and his shadows).
3. **Both side walls solid floor-to-ceiling.** Draw must terminate against something.
4. **Every ledge has two ways off.** A Draw lane aimed at a cornered player is an unfair death.
5. **Camera LOCKED** — `RoomCamera` at `orthographicSize` 10, ONE `CameraBounds` zone bound to the
   interior, thick rock frame sized for 21:9. He swaps positions across the whole room, so the whole
   room must be on screen. ⚠️ Re-import the text file and the zone resize is lost; redo it.
6. **Three standing levels, no more** — the lock plus Law 2's five clear rows caps it there.

**What differs from the Ninja's — the shape says "hall":** wider and lower. A long floor makes Draw
lanes long (his best attack gets room) and Overhead shockwaves matter (a long floor is where they
travel). Two low ledges at the ends, one **central shelf** rather than an island — the shelf is the
Draw refuge and the Overhead trap, and the split fans doubles onto it when you are up there.

```
##############################################
##############################################
##############################################
########..............................########
########..............................########
########..............................########
########..............................########
########..............................########
########..............................########
########............########..........########   central shelf  — Draw refuge, Overhead trap
########..............................########
########..............................########
########..######..............######..########   low ledges — float clear of the walls (rule 4)
########..............................########
########..............................########
########..S.....+..........+.......X..########   spawn · crystals · exit — all on the floor
##############################################
##############################################
##############################################
```

Sizes are a starting point; `LevelValidator` decides. Rises of 4, jump apex 4.9. Two floor crystals
as the no-jump lifeline, same as the Ninja arena. **Dressing is the designer's pass** — the Ninja's
crates-as-steps trick is load-bearing there and would be here too. Rows of candles along the floor
would suit a hall; check what the Dungeon pack actually holds before promising anything.

## 8. Look and sound — where the doubles come from, and the trap already known

⚠️ **THE RIG CANNOT BE TINTED. `_Color` does not exist on 15 of the 16 renderers** (Alpha Cut and
Body expose only `_Alpha`; only Hair has a colour). Setting it is a silent no-op — the bug shape that
kept the gravity-reversal flash invisible for months. So "shadow" doubles cannot be dark rigs. They
are **translucent rigs**: two extra stripped Samurai rigs, pre-built in the boss prefab (strip once
in the editor, never at runtime — every strip step fails silently when missed), driven by the boss
broadcasting its animator parameters to theirs, `_Alpha` via `MaterialPropertyBlock` across all 16
`SkinnedMeshRenderer`s. The `_Alpha` handle is the one every Cainos rig shader shares, and it is the
same handle the finale twist flips.

- **Which one am I / which one is real:** the real one carries a **warm contact mark** (Torch — the
  lit accent) under his feet; doubles carry none. The player's own contact mark is already a
  different object. Judge the effect on *that* question, not on spectacle (Ninja §3).
- ⚠️ Baked-mesh afterimages (`SkinnedMeshRenderer.BakeMesh`, as `CardAimIndicator` does it) are the
  Ninja's language — cold blue, "where he was". **Do not use them for the doubles**: a double is
  where he *is*. If the Kagemusha wants motion trails at all, they are ink-dark, not blue.
- **Scale 1.0**, against the player's 0.8 — native pixel grid, crisper not mushier. Do not go past it.
- **Death burst** through `BossDeathVFX`'s colour parameter: the Ninja comes apart into his blue;
  the Kagemusha comes apart into **two of himself** — the death VFX spawns the last two doubles
  fading out either side of the burst, and the gold stays gold (it is the loot).
- **Health bar:** its own prefab (`BossHealthBar_Kagemusha`), on the converted `ResourceBarUI`
  vocabulary. Identity without a hue: the Ninja is 22 fine plates (glass); the Moss Knight 10 fat
  ones (armour). The Kagemusha: **two bars' worth of plates drawn as one — 12 plates, each with a
  faint seam down its middle**, so the bar itself is a thing pretending to be two things. Fill
  steel like the Ninja's, chunk in Wound red, frame lacquer-dark. Verify by screenshot; the
  linear-space rule applies.

**Sound — `ProcSfx` SAMURAI family.** The Ninja family is separated by *flutter rate* (steel and air
spinning). The samurai's axis is **the ring**: a katana drawn from a scabbard is a long resonant steel
tail with a hard onset, which nothing in the six existing families has (magic bells are harmonic and
soft-onset; metal bars are inharmonic and short).

| clip | character |
|---|---|
| `KatanaDraw` | hard click, then a long ring decaying ~0.8s — the Draw's strike |
| `Sheathe` | the ring in REVERSE — a short scrape into a click, then **silence**. The silence is the telegraph. |
| `Split` | three `KatanaDraw` onsets a few cents apart, no tail — a chorus of him |
| `Shatter` | a dull hollow knock, not glass (glass is the Glass archetype's sound) — the fake was empty |
| `Overhead` | the stone-slam family's sub plus a ring on top |

⚠️ **All Inspector clip slots are OVERRIDES; the procedural clip is the default.** The Ninja shipped
with six null slots and fought in silence. A boss must not be able to ship mute.

## 9. Names, reward, relic

- **Boss name: "Kagemusha"** — DESIGNER. The playable is **"Samurai"**, matching "Wizard"/"Ninja".
- **Card: "Through and Through"** (alt. "Excuse Me"). ⚠️ Checked against the 49 relics and 24
  blessings — no collision. *Stand-In* (relic: copies the relic to its left) and *Understudy*
  (blessing) already live in the "double" neighbourhood; neither is a mechanical overlap with
  anything here, but the *naming* space is getting crowded — do not add a third "stand-in" word.
- **Trait: "Full Plate"** (alt. "Layered Up", "Dress Code").
- **On death:** the same `Gold New` / `ShiftCrystal` spoils as the other two bosses (read off the
  prefab, not guessed), `BossRewardCue.Schedule`, exit unsealed from `OnDestroy` as well as death.
  Boss music from `StartFight`, not only stopped on death (the Ninja shipped that backwards).
- **Relic — "Body Double"**, `Rarity.Boss`, **a PASSIVE, not an Art.** *"Once per room, the first hit
  that would hurt you hits a shadow instead."* You flicker translucent for a beat; no damage, no
  knockback. On theme, at boss power, and it does not spend the one-Art-at-a-time cap — the Ninja doc
  points out that with ~10 bosses roughly seven relics *must* be passives, and this is one. Check
  against Phoenix Cog (a mercy on lethal, per run) — different trigger, different scale, both can
  coexist in a loadout.

## 10. The opening beat

**Three of him kneel at the far end of the hall, identical, still.** The player walks in and reads the
room (`startDormant` + `BossAwakenTrigger` a few tiles past spawn, like both other bosses).

Cross the line and **two of them dissolve.** The one left stands, and draws.

That is the awaken, and it tells the player the trick *before he uses it* — which is exactly what the
Ninja's katana-pull does for the blink. The fight explains itself before it starts, and the first
Split lands on a player who already knows what a fake looks like. (And the joke is right there: he
brought two decoys to an empty room and has to send them home.)

## 11. Implementation map

- ⚠️ **PREREQUISITE — the per-character finale plumbing, and it is due NOW.** The Ninja doc said
  `finalBossRoomPrefab` "grows into a per-character lookup — do it once, before there are three of
  them." This is the third. Two fields on `CharacterData`: **`bossRoom`** (the arena where *this*
  character is the boss) and the finale becomes `Chosen.bossRoom ?? finalBossRoomPrefab`; and
  `PickBossRoom` **filters the chosen character's own `bossRoom` out of the mid-map pool**, so a
  Samurai never meets the Kagemusha before the top. Wizard has no arena yet → falls back to today's
  behaviour unchanged. ⚠️ Read it via `CharacterSelection.Chosen` at pick time, never cached — same
  no-mirrored-field rule as every other trait.
- **`KagemushaBoss`** on the `NinjaBoss` skeleton: dormant → awaken → `FightLoop` picking by range
  and cooldown, `RepositionRoutine` between attacks, `EnsureNotStuck()` watchdog (⚠️ no mid-move
  exemption flag — it is a latch), gravity captured ONCE in `Awake` and every restore through
  `RestoreGravity()`, `AttackSpeedMul` restored in `OnBossDied` AND `OnDestroy` if driven at all.
  `EnemyHealth` for HP so every card works for free; clear `healthBarPrefab` so the small bar does
  not also draw.
- ⚠️ **Split is GUARANTEED PERIODIC, not a fallback branch.** The Ninja's volley sat as the final
  `else` and fired **zero times in 2000 attacks** because a range check always won first. Split is
  the fight's economy (§4); give it an interval with first refusal (`splitInterval` ~9s) exactly
  like `volleyInterval`.
- **`ShadowDouble`** (house pattern, built in code): a stripped rig child, a trigger collider, an
  `_Alpha` setter, a `Mirror(animatorParams)` call, `Shatter()` → damage to the owner via
  `EnemyHealth.TakeDamage`, spawn one `ShiftCrystal`, VFX, `Destroy`. Carries `TemporaryObject` and
  is a child of the boss, so it dies with the room either way.
- ⚠️ **The swap moves TWO bodies.** Real ↔ double positions exchange; the double inherits the real
  one's velocity of zero. The double was standing somewhere valid, so no ring search is needed — but
  run the 0.95×-capsule fit test anyway, because a double fanned onto a ledge edge may be
  half-overhanging. If it fails, the counter falls back to the in-place lunge.
- **Preset stripping, dependency order** (Ninja §10): input → controller → Rigidbody2D/BoxCollider2D
  on the root; three solid bone colliders (Spine1/Spine2 capsules, Head circle); stock weapon's
  Rigidbody2D + trigger; `AnimationEventReceiver` off the Animator child, `PlayerAnimEventSink` on.
  **End state: exactly 1 Rigidbody2D, 1 Collider2D, 1 Animator per rig** — and the two double rigs
  have NO Rigidbody2D at all (they are moved by hand).
- **Layer:** Enemy(11), mass 500, like every enemy since the AI pass. Standable via `groundLayer` —
  decide deliberately (Ninja §11.6 is still open on this); detect him via
  `GetComponentInParent<EnemyHealth>()` regardless.
- **`Through and Through`:** `CardActionType` value next after `SalvagedShuriken = 21`; action
  class in `Actions/`, registered in `CardActionExecutor.Awake()`, honest `ModifiedState`
  (`LayerCollisionMatrix` + whatever the dash flag is called); `CardAimIndicator` preview = a 5-unit
  lane in facing direction; `PlayerController.PerformLunge` ignoring Player↔Enemy collision for the
  travel, restored in `finally`; `EnemyMelee`-style box sweep per fixed step with a `HashSet` so each
  enemy is cut once.
- **Armour (system):** `PlayerHealth.Armour` (float, no cap) + `AddArmour(float)`; `ApplyDamage`
  drains Armour before HP in the ONE place damage is subtracted; `PayHealthCost` untouched (bypasses
  it by construction). Fires `OnArmourChanged` for the HUD. ⚠️ Every "did the player take damage"
  consumer (flawless clear, oaths, `NoDamageRoom`, Glass) keys off the HIT, not off HP moving —
  audit them when this lands, because today the two are the same thing and tomorrow they are not.
  HUD: a second `ResourceBarUI` above health, hidden entirely at 0.
- **Full Plate (trait):** `CharacterData.armourPerRoom` (5); granted from
  `PlayerController.OnNewRoomEnter` only when `LevelManager.IsCurrentRoomCombat()`, read via the
  live `character` (never copied). Whether a hit chips or shatters is one branch in `ApplyDamage`
  behind a flag, so §12.2 can be flipped by the designer in the Inspector during testing.
- **Testing traps already paid for, all apply:** spawn the room in one `execute_code` call and act
  in the NEXT (`Start()` has not run; `CurrentHealth` is 0); a live `LevelManager` with a null
  static `instance` is a domain reload, not a bug; `Time.timeScale = 0` freezes `deltaTime` fades
  before you photograph them; never read `collider.bounds` in the frame you moved the transform.

## 12. OPEN — for the designer

1. **"Through and Through" vs "Excuse Me".** Cool-with-a-grin, or the pure wink. The deck can only
   afford one straight-up-fun name per character or nothing stands out.
2. **Armour: chip or shatter?** (§2) Recommended chip — it is what "an HP bar on top" means and it
   already produces the streak. Shatter is harsher and more samurai. Built as a flag so both can be
   played.
   Also: **is 5 per room right, and should it start at 0 or 5 in the hub?** Proposed: 0 in the hub
   (sandbox), first 5 on the first combat room.
3. **Does the shard damage answer the Sheathe?** Proposed YES (one rule: blade low, touch nothing).
   The alternative — his own pieces cannot provoke him — is more forgiving and one extra branch.
4. **Standable or not** — same open question as the Ninja's.
5. **Does the finale's 40% twist apply mid-map?** Proposed NO — mid-map he is a lesser cut of himself
   (Ninja §11.4 asks the same), and the twist is the finale's payload.
6. **Wizard's arena does not exist**, so a Wizard player still gets the Moss Knight at the top. That is
   the fourth identity, not this one — but the plumbing in §11 should be built so it is one asset
   away.

## 13. Rejected — do not re-propose

- **A blink / teleport.** Ninja's. He swaps with a double, in view, or he does not move. §3.
- **Shift theft.** Reserved for a future Thief boss (Ninja §11.2). Not spent here.
- **A room-wide cleave through walls for the signature card.** That mechanic is reserved under the
  shelved card *Skies Parted* (`CardIdeas.md`). The lunge is 5 units and stops at walls — a
  different card.
- **A VULNERABLE lunge** (the first draft, to protect Dash). DESIGNER: Dash is a starter card and
  does not need protecting; the signature is invulnerable. §2.
- **"Unbowed" — knockback immunity as the trait.** DESIGNER: simple and boring. Replaced by Armour.
  §2. (The knockback-immunity idea itself is fine for a *relic* someday — it is two-sided and cheap.)
- **Tinting the doubles dark.** Impossible on the rig without a warning ever being logged. §8.
- **Shatter damage above the anchor.** 12 is 12. §4.
- **A crusher or other room-owned damage machine.** He owns that job. §4.
- **An active ability for the playable Samurai.** Built, tested, cut, documented in `CharacterData.cs`.
