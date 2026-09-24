# Deckshift — Card Ideas Backlog

Designs that have been thought through but not built. Numbers, when they appear, are not approved —
run them through `CardAnchors.md` before they become an asset.

Started 2026-09-07.

---

## The pool as it stands (2026-09-07)

16 playable cards: **6 movement** (Leap, Dash, Create Platform, Phase, Portal, Second Thoughts) ·
**5 attack** (Fireball, Shuriken, Comet Dive, Freefall Blade, Vampiric Bite) · **5 utility**
(Adrenaline, Floor is Lava, Glass Wail, Glass Parry, Dead Weight). Plus Stagger (fail-state) and
Borrowed Steel (the Ninja boss's quiver, not a reward card).

**Shapes the pool does not contain yet** — worth checking a new card against this list, because a
card that fills one of these is worth more than another well-tuned instance of a shape you already
have:

- **Nothing gives Shift back.** The only Shift income inside the deck is Stagger, paid in blood.
- **No line or pierce attack.** Every attack hits one thing or one blob.
- **Nothing works for you while you keep moving.** Every card is instant or a self-buff.
- **No delayed payload.** Nothing telegraphs and lands later.
- **Movement cards and attack cards never talk to each other.** They are two separate populations
  with zero synergy between them.
- **Nothing is bounded by TIME.** ⚠️ This one is structural: the game charges nothing for time — no
  timer, no reinforcements, Shift does not decay. It is why the cut character active ability broke
  the game (an infinite-but-slow attack always beats a finite-but-fast one). A card whose value is
  bounded by a clock is doing something almost nothing else in the game does.

---

## Shelved: "Skies Parted"

**Status: name reserved, mechanic undecided (designer, 2026-09-07).** The designer likes the name and
wants it to be an **attack or a buff**. Four readings were rejected as wrong *for this name* — they
are recorded below under their own headings because two of them are good cards that need different
names.

Three attack/buff readings reached before the card was shelved:

- **The cleave.** Cursor-aimed (the aim path already exists for Shuriken) — the sky splits along that
  angle clean across the room, through walls, damaging everything on the line. The pool's first
  line/pierce. Rewards reading where enemies stand relative to each other, which a vertical room
  makes into a real puzzle. Most literal reading of the name.
- **The storm gets in** *(the recommendation at the time)*. ~6s buff: **your movement is the
  weapon** — jumps crack, dashes cut, landings shock. Inverts the game's central rule for a window,
  so for six seconds *spending* Shift is the point. It is also the only idea on the list that makes
  the movement half of the deck and the attack half synergise.
- **The telegraph.** Mark a spot; ~1.5s later something enormous lands there. The wind-up is the
  card — you must herd enemies into the mark. Best new verb, but ⚠️ **its supporting systems are not
  ready**: enemies never jump and stop dead at ledges, and herding is only fun if enemies follow
  convincingly.

---

## The proposed set (2026-09-07) — 8 cards, none built

Numbers derive from `CardAnchors.md`: budget ≈ `U(15) × ChargeMult × ShapeMult × RarityMult`, plus a
light Shift premium. Charges 6:×1.0 · 5:×1.1 · 4:×1.25 · 3:×1.45 · 2:×1.8 · 1:×2.6. Shape ranged
×1.0 · melee ×1.3 · AoE ×1.8. Rarity Common ×1.0 · Rare ×1.3 · Epic ×1.6.

⚠️ **Name collisions checked against the 49 relics and the 24 blessings.** *Hot Streak* and *Second
Wind* were both discarded during this pass — they are **relics already**. Check any new name against
`Assets/Relics/*.asset` before it becomes an asset.

| # | Card | Lane | Fills | **Designer verdict 2026-09-08** |
|---|---|---|---|---|
| 1 | Granted Wishes | utility | Shift income in the deck | good idea, **execution looks difficult** |
| 2 | Expense Account | movement | time-bounded value | **card yes, NAME didn't land** — needs a new one |
| 3 | **Judgment** | attack | delayed payload | **wanted, and it keeps the name "Judgment"** (not Act of God) |
| 4 | Right of Way | buff | movement × attack synergy | ❌ rejected |
| 5 | Killing Time | buff | time-bounded value | ❌ rejected |
| 6 | Break Glass | Glass | archetype depth (2 → 3) | ✅ liked |
| 7 | Open Bar | Vampiric | archetype depth (1 → 2) | ✅ liked |
| 8 | Hornet's Nest | deployable | works for you while you move | ✅ **some version of it** |

⚠️ **The two I recommended hardest (Right of Way, Killing Time) were both rejected.** Worth
remembering before leaning on the same reasoning again: both were argued from *structural gaps in the
pool* rather than from how they would feel to play. The three that landed (Break Glass, Open Bar,
Hornet's Nest) were argued from a fantasy first. Gap analysis picks good problems; it does not pick
good cards.

---

### 1. Granted Wishes — it rains Shift

*(designer named this one during the session)*

The sky splits and pours Shift crystals across the room. **Not** "gain 10 Shift" — they land
scattered, biased to high ledges and the far half of the room, and they persist until the room ends.
**Collecting them costs jumps.** A Shift card that charges you movement to bank movement.

**Numbers:** ~10 crystals at 1 Shift each · **2 charges · 0 Shift**. Rare.
A greedy full sweep nets maybe +5 after the jumps it costs; taking only the safe three nets +3 for
nothing. Priced against the shop, which sells 3 Shift as a service.

**The decision it creates:** sweep the room or take the easy ones — greed against safety, in a game
where the currency *is* your ability to move.

⚠️ **Charge no Shift to play it.** The collection cost is the cost; a Shift price on top double-taxes
the same idea. ⚠️ **The tuning lever is WHERE the crystals land, not how many there are.**

**Build note:** the boss death celebration already drops real collectible gold and Shift crystals —
that scatter path is reusable, so this is much cheaper than it looks.

### 2. Expense Account — the sky takes your weight

*(was "Thermal"/"Updraft" — the new name hints at the effect and sits in the tone doc's
cool-with-a-wink register: jumps are free because someone else is paying)*

~5s window: jumps go higher (×1.3), falls are slower (gravity ×0.55), and **jumps cost 0 Shift**.

**Numbers:** 5s · **3 charges · 1 Shift**. Rare. Roughly 6 free jumps in the window, so it pays for
itself at 2 and is worth a card slot at 5+.

**The decision it creates:** not "should I play this" but "**where**". Burning it in a corridor is a
waste; the value is in reading the room ahead and saving it for the shaft.

⚠️ **The floatiness is not decoration.** An economy effect is invisible — "your jumps were free" is
not something the player can see, and this project's own rule is that a buffed thing must *look*
different. The visible physical change is what makes the free jumps legible. Ships together or the
card feels like nothing happened.

⚠️ Carries `ConflictFlags.GravityScale`, so it and Floor is Lava block each other — correct, and
`CardActionExecutor` enforces it for free.

### 3. Judgment — the parting is the telegraph

⚠️ **The name is "Judgment", settled by the designer 2026-09-08.** It was briefly renamed "Act of
God" during the design pass; that was reverted. Do not re-propose it.

Aim at a spot. The sky opens there — visibly, loudly. **~1.2s later** a pillar comes down.

**Numbers:** **50 damage**, radius 3 · **2 charges · 1 Shift**. Epic.
Budget: 15 ×1.8(2ch) ×1.8(AoE) = **48** ✓. One-shots a Soldier (40), leaves an Elite (70) at 20.

⚠️ **The delay is drama, NOT a herding puzzle — and that distinction is the whole design.** The
earlier version of this idea asked the player to bait enemies into the mark, which fails on systems
this game does not have: enemies never jump and stop dead at ledges. Sizing fixes it instead of
AI — enemies move 1.2–1.4 u/s, so in 1.2s one walks ~1.7 units, and **a radius-3 circle forgives that
entirely**. The telegraph keeps the spectacle and drops the dependency.

### 4. Right of Way — movement is the weapon

For **8 seconds you do not stop for enemies**: their bodies stop blocking you, and anything you pass
through takes damage. **Their attacks still land.**

**Numbers:** 12 damage per pass-through, once per enemy per second · 8s · **2 charges · 1 Shift**.
Epic. Budget 15 ×1.8(2ch) = 27; three pass-throughs = 36, and the risk attached keeps it honest.

**Why this one matters most:** movement cards and attack cards are currently two separate populations
with **zero** synergy. This is the first card where Leap, Dash and Comet Dive become attacks. It also
quietly fixes a friction the player already feels — enemy bodies are mass 500 and *block you*, and
dash does not disable collision.

⚠️ **Not invulnerability.** You pass through their bodies and still eat their hits, so threading a
pack of three for 36 damage costs you a hit or two. That trade is the card; making it safe makes it
boring.

⚠️ Carries `ConflictFlags.LayerCollisionMatrix`, so it and Phase block each other — correct.

### 5. Killing Time — the clock card

*(name is the mechanic: your kills buy the time)*

**8 seconds during which playing a card does not spend a charge. Every kill adds 3 seconds.**

**Numbers:** **1 charge (single use) · 2 Shift**. Epic.
Budget: 15 ×2.6(1ch) ×1.6(Epic) = **62** ≈ four free Fireballs ✓.

**Why it's the strongest idea here:** the game charges **nothing** for time — no timer, no
reinforcements, Shift does not decay — which is exactly why the cut character active ability broke
it. This is the one card that makes the player care about the clock, and it does it by making speed
*pay*: burn Fireball, kill, buy more seconds, keep going. Being single-use turns it into a "when do I
pull the trigger" card, which is the best kind.

**Build note:** cheap — `ShouldSpendCharge` already exists as a hook (Sleight of Hand, Slow Burn,
Teacher's Pet all use it).

### 6. Break Glass — Glass archetype (2 cards → 3)

*"In case of emergency."* **Can only be played below 30 HP.** Above that it sits in your hand,
unplayable, as a promise.

**Numbers:** **60 damage** in a wide arc + knockback · **2 charges · 0 Shift**. Rare.
Budget 15 ×1.8(2ch) ×1.8(AoE) = 48; the conditional lock is a real cost, so 60.

**Why it's Glass:** the archetype's identity is that being nearly dead is where your power is. This is
the purest statement of it in the pool — a card that is dead weight while you're winning and your
best card at the worst moment of the run.

**Precedent:** conditional-on-HP is established — Adrenaline already branches on above/below half
health, and Dead Weight is already a card that cannot be played.

### 7. Open Bar — Vampiric archetype (1 card → 2)

For **6 seconds, 40% of all damage you deal heals you.**

**Numbers:** 6s · **3 charges · 1 Shift**. Rare.
Budget 15 ×1.45(3ch) = 22; three Fireballs in the window = 45 damage → 18 HP ✓ (heal converts 1:1).

**The decision it creates:** it has to be played *before* you commit to the fight, not after you're
hurt — which is the opposite of how players instinctively use healing, and therefore a skill.

**Why it's needed:** Vampiric is a named archetype with exactly one card in it. This turns the whole
attack half of a hand vampiric for a window, so the archetype becomes something you *build toward*
rather than a single asset.

### 8. Hornet's Nest — it works while you move

Throw it. It sticks where it lands and, for 8 seconds, stings anything that comes near.

**Numbers:** 5 damage every 0.7s within 6 units, 8s · **3 charges · 1 Shift**. Rare.
Ceiling ~55, realistic ~25–35 because enemies do not politely stand in range.

**Why:** every card in the pool is instant or a self-buff. **Nothing works for you while you keep
platforming.** Placing this on a ledge and leaving is a verb the deck does not have, and it suits a
platformer specifically — position is the whole game.

⚠️ **Runtime spawn — it must not outlive its room.** `Instantiate` with no parent makes a
**scene-root** object that survives the room change (this shipped twice already: floating enemy health
bars, and the Moss Knight's slimes following the player into the hub). `LevelManager.ClearRuntimeSpawns()`
sweeps by type; give the nest its own self-destruct as well.

---

## Art pass, 2026-09-08

Five new faces in `Assets/Art/`, all cut on the **canonical Freefall Blade frame** with an **empty
name plate**, so all five carry `nameIsPaintedIntoArt: 0` and the title is typed in code.

| art | assigned to | replaced |
|---|---|---|
| `shuriken.png` | Shuriken | `newcards/freefallblade.png` (placeholder) |
| `shuriken.png` | **Borrowed Steel** — same art, distinguished by VFX (below) | `newcards/freefallblade.png` |
| `takeback.png` | Second Thoughts | `newcards/parry.png` (Glass Parry's art) |
| `adrenaline.png` | Adrenaline | older `newcards/adrenaline.png` |
| `cometdive.png` | Comet Dive | older `newcards/cometdive.png` |
| `redpact.png` | **nothing yet** — see below | — |

**Frame colour is the rarity tell** (dark grey Common · light grey Uncommon · gold Rare · purple
Epic), and the new art states it: Shuriken grey, Second Thoughts + Comet Dive gold, Adrenaline +
Red Pact purple.

⚠️ **The new art passes `CardFace.LayoutFor`'s aspect check by 0.03.** That chooser picks the `Gem`
medallion positions when `width/height < 0.63`; the new cuts are 0.590–0.597. Had they come in at the
legacy 0.667 they would have silently taken the `Classic` positions and put the charge number off the
left edge of the red ball. **Anything re-cut later must stay under 0.63** until `Classic` is deleted.

⚠️ **A doc correction found here: CLAUDE.md claims `stagger.png` is the only card art imported with
Bilinear filtering while "every other card art uses Point". That is wrong.** Every PNG in
`Assets/Art/` is `filterMode: 1` (Bilinear) except three UI bits (`panel`, `recallbutton`,
`recallcounter`). The new five match the existing set, so nothing was changed — but do not "fix"
stagger on the strength of that claim.

### Borrowed Steel — same art, distinct read (designer request, UNBUILT)

Borrowed Steel is the Ninja boss's own stars picked up off his arena floor. It already behaves
unlike a card — stays in hand, enters no pile, ignores hand capacity, 20 charges — so a visual mark
is earning its place rather than decorating.

The mark should say **"this isn't yours"**, not "this is better". `CardFace.Build` already
special-cases by `actionType` (Stagger returns early before the medallions), so
`CardActionType.SalvagedShuriken` is the hook, and it is the ONE place every screen shares.

⚠️ **BLOCKED ON VERIFICATION, not on code.** This project's hardest-won rule is that subtle alphas
and tints are picked **by screenshot, never by arithmetic** — the render is linear, so a low alpha of
a saturated colour composites far brighter than the number reads. The Unity MCP bridge was refusing
connections during this session, so no capture was possible. **Do not ship a tint value that has not
been looked at.**

## Red Pact — art exists, card does not

`Assets/Art/redpact.png`. A blood-red scroll covered in runes, on a **purple (Epic) frame**. It reads
as a contract: ancient, arcane, binding.

⚠️ **A pact is TWO-SIDED. A card that only gives is not a pact** — whatever this becomes, the art is
promising the player that something is owed.

**The strongest lead: the game already has contracts.** `QuestSystem`'s oaths are per-room
commitments ("clear this room without playing a card / without Recall / without spending Shift"),
they are **streaks that reset to zero when broken**, and nothing is judged until the room is left.
A card that signs one on the spot would turn oaths from a quest-board decision into a tactical one.

## Rejected during the set pass

- **A line/cleave attack** — still a good card and still an empty shape, but the mechanic is
  currently reserved under **Skies Parted** (above). Don't spend it on another name until that card
  is settled or abandoned.

---

## Rejected, with reasons worth keeping

- **A truce** ("the storm stops") — ~6s where nothing in the room can hurt you. Enemies still move,
  projectiles still fly, they pass through. Rejected as too well-worn; every deckbuilder has brief
  invulnerability and it says nothing about *this* game.
- **Revelation** — the whole room revealed through walls (enemies, gold, chests, exit). Probably a
  **relic, not a card**: you would want it always-on, and spending a hand slot and a charge on
  information feels bad in a platformer.
- **The sky leaves** — the room's ceiling briefly stops existing; you jump out the top of the world
  and come down anywhere along it. ⚠️ Rejected on **Level Design Law #7**: it is a level-skipper by
  construction.
