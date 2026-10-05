# Relic Redesign — Slot-Constrained Loadout

Paper design for replacing Deckshift's additive (Slay-the-Spire-style) relic system
with a **Balatro-style slot-constrained loadout**. Started 2026-07-09.

> ## ⚠️ STATUS 2026-08-11 — THE MECHANICS IN THIS DOC ARE BUILT. DO NOT RE-PLAN THEM.
>
> Shipped and live: **5 slots**, rarity-based sell values, `TryGrantRelic` with the forced
> full-slot swap screen, a manage/inspect panel, hover tooltips, and a `RelicCatalogue` /
> `RelicPool` so no offer list is ever hand-maintained again. 19 relics.
>
> **What actually remains is BALANCE, not code:** the relics were authored as small always-on
> bonuses, which is the wrong shape for an economy where every pick costs you another relic.
> Read the sections below as the *reasoning behind* the built system, not as a to-do list.
>
> Also note the **scrap system is built** (it was "planned" when this was written), which
> resolves the open questions here about exhaust recovery.

Companion context: see `CLAUDE.md` → "Relic System". Memory: [[project-deckshift]].

---

## Why

Deckshift's core philosophy is **"Movement is a Resource."** The old relic system
contradicted that: relics were a pile that only ever grew, every one a small passive
bonus, no decisions after acquiring. The redesign makes relics a **curated resource
pool the player manages** — every new relic is a real "what do I give up?" decision.

---

## Locked decisions (v1)

Agreed with the designer 2026-07-09:

1. **5 slots, fixed.** No slot expansion in v1 (may revisit — start simple).
2. **Loadout bar lives in a top row.** Always on screen: filled slots + empty frames
   + an `N/5` count. Reads as a loadout, not a growing column.
3. **Full-slot acquisition = a Swap Screen.** When a relic is granted and all 5 slots
   are full, the game pauses and shows the incoming relic vs. the current loadout;
   the player **sells one to take it, or leaves it.**
4. **Selling refunds a fixed amount by rarity** (tunable):
   - Common **25** · Rare **50** · Epic **90** · Legendary **150** gold.
5. **Manage panel opens from both a bar click and a hotkey.**
6. **Rarity colour language is unchanged** (Legendary gold / Epic purple / Rare blue /
   Common grey — the same palette already used by `RelicIcon` and the chest burst).

### The no-refund economics rule

Selling must never feel like a punishment for a purchase. The Swap Screen **decides
before anything is finalized**:

- **Paid source (shop):** clicking buy while full opens the Swap Screen *first*. Take
  it → pay for the new relic AND pocket the sell gold. Leave it → **no charge at all.**
- **Free source (chest, boss):** Swap Screen; leaving it simply declines
  the relic at no cost.

There is never a "I paid and lost it" moment.

---

## Three UI surfaces

### 1. Loadout bar (always on screen)
- Top row, part of `GameplayHUD` (so it hides behind full-screen panels like today).
- Renders exactly **5 slot cells**: filled cells reuse the existing procedural
  `RelicIcon` chip; empty cells show a dim rounded frame.
- An `N/5` count label.
- **Hover tooltip:** name + description + sell value.
- **Clickable** → opens the Manage panel. A hotkey opens it too.

### 2. Manage panel (calm, paused screen)
- Opened any time from the bar/hotkey. Pauses via `GameManager.RequestPause`, hides
  `GameplayHUD`, locks the hand drawer (`HandUIDrawer.SetLocked(true)`) — the standard
  full-screen-panel contract.
- Shows all 5 slots large with full descriptions. Click a relic → **Sell** (shows its
  gold value) → relic becomes gold, slot frees.

### 3. Swap Screen (acquire-when-full decision)
- Triggered by a grant while full. Shows the incoming relic + the loadout as
  click-to-sell targets. "Take it" (after selecting one to sell) / "Leave it".
- Wired into every grant path (shop, chest, boss death).

The Manage panel and Swap Screen share a clickable-loadout widget; build it once.

---

## Data model changes

`RelicManager` (owned relics stay a `List<RelicData>`; list index = slot index):
- `public const int MaxSlots = 5;`
- `public bool IsFull => ownedRelics.Count >= MaxSlots;`
- `public int SellValueFor(RelicData)` — switch on rarity (numbers above).
- `public void SellRelic(RelicData)` — remove, credit gold, fire `OnRelicRemoved`.
- `public event Action<RelicData> OnRelicRemoved;` (HUD/panels subscribe alongside
  the existing `OnRelicAdded`).

`RelicData` unchanged for v1 (relicID / relicName / description / relicArt / rarity).
Sell value is derived from rarity, not stored per-asset.

---

## Grant-path integration (Stage 3)

Current grant paths call `RelicManager.AddRelic(relic)` directly:
`ShopItemUI`, `DebugTools` (F1). Boss death loot drops real pickups,
not relics (no relic grant there yet).

Stage 3 routes grants through a single **`TryGrantRelic(relic, context)`** entry point:
- Not full → add as today.
- Full → open the Swap Screen; the `context` carries the "paid" info so the shop can
  honour the no-refund rule (charge only on "Take it").

---

## Staged build plan

- **Stage 1 — Foundation + loadout bar.** Data model (slots, sell values, `SellRelic`,
  `OnRelicRemoved`) + reworked top-row HUD with empty slots, `N/5` count, hover tooltip.
  *(Bar click / hotkey wired in Stage 2 once the panel exists.)*
- **Stage 2 — Manage panel.** Inspect + sell. Wire bar-click + hotkey to open it.
- **Stage 3 — Swap Screen** + route all grant paths through `TryGrantRelic`.
- **Stage 4 (later) — Content.** Bigger, more interactive relics + rebalance the
  current 5 (small passives don't shine in a slot system). Target ~15–25 relics so
  slot decisions matter.

---

## Stage 4 content — agreed relic slate (2026-07-09)

Designed with the user. **Corrections learned:** every jump costs exactly **1 Shift**; **Shift carries
between rooms and never resets** (a few wasted early jumps hurt later); enemies **don't drop gold** yet
(scrap system planned). These invalidated some first-draft ideas.

**Build (functional standalone) — proposed numbers, tunable:**
- **Reclaimer's Clamp** (Rare) — first card exhausted each room returns to hand with 1 charge.
- **Flux Regulator** (Common) — first card each room is free (0 Shift). Reuses `DeckManager.isNextCardFree`.
- **Overclocked Recall** (Epic) — Recall costs 0 Shift but each Recall deals 5 self-damage.
- **Meteor Greaves** (Epic) — landing from a fall ≥~4u makes a shockwave (radius ~3.5), damage scales with
  fall height (~3/unit, cap ~45) + knockback.
- **Executioner's Seal** (Epic) — hitting a **non-boss** enemy at ≤20% max HP instakills it (bosses immune).
- **Midas Recoil** (Rare) — +1 damage per 25 gold held, on all player damage (needs a central outgoing-damage hook).
- **Phoenix Cog** (Legendary) — once/run, lethal damage leaves you at 1 HP + screen-clear blast (~60).

**Commons batch (agreed, all but one):** Flux Regulator (above) · **Pocket Battery** (start each room +1 Shift)
· **Scrap Magnet** (pickups pulled from farther) · **Whetstone** (first hit on each enemy +5) ·
**Reinforced Plating** (+15 max HP). **Kinetic Primer** (idle→free jump) — user: too strong, redesign later.

**Needs the sell UI (Stage 2) before it's meaningful:**
- **Glass Heart** (Epic) — +100% damage, −50% max HP. A punishing relic is only fair if sellable.
- **Foundry Rights** (Epic) — each relic sold → +1 max Shift this run (start +1; user flagged it strong).
  Literally can't fire until selling exists.

**Still designing (numbers/feel):** Deadweight Governor (ground-drain vs kill payout — +3/kill too low) ·
Overpressure Regulator (Shift regen + halved max — spicy) · Flywheel Core (airborne kill → +1 Shift — cheap, defined).

**Shelved (revisit when systems exist):** Surge Cell (dead — Shift already carries) · Vulture's Instinct
(needs scrap drops) · Rust Catalyst (needs supporting cards + status system) · Gecko Grip (no wall-cling yet
— maybe the relic that GRANTS it) · Hollow Socket (design pass).

**Note on rarity curve:** a slot system still needs Commons — they're the *cycle fuel* you slot early and
**sell** later to fund Epics. Don't make everything build-defining.

## Deferred / open

- Slot **expansion** (buy a 6th/7th slot) — out of scope for v1.
- Relic **reordering** (drag) — nice-to-have; not required for the decision loop.
- Existing relics keep their current effects until Stage 4 rebalance.
- Sell numbers are first-pass; tune once the run economy is felt end to end.

---

# Stage 4 — the balance pass (measured 2026-08-21)

All 19 relics read out of the assets and their wiring read out of the code. Nothing below is
estimated from the descriptions.

**Run assumptions used throughout:** ~10 combat rooms, ~5 enemies each, **~50 kills per run**.
Anchors from `CardAnchors.md`: player **100 HP**, **40 Shift**, damage unit **U = 15** (one
Fireball), fodder **12 HP**, melee enemy **40 HP**, boss **300 HP**.

## 1. What each relic actually pays over one run

| Relic | Tier | Real effect | Per-run value |
|---|---|---|---|
| **Hot Streak** | Common | +2 Shift per kill | **+100 Shift — 2.5× the whole starting pool** |
| **Snack Fangs** | Common | +5 HP per kill | **+250 HP — 2.5 full health bars** |
| **Whetstone** | Common | +5 on first hit per enemy | **+250 damage — ~17 Fireballs** |
| Bubble Wrap | Common | +15 max HP | +15 HP, once |
| Pocket Lightning | Common | +1 Shift per room | +10 Shift |
| **First One's Free** | Common | first card each room costs 0 Shift | **~5 Shift** (see §3) |
| Loot Goblin | Common | 3u pickup pull | convenience only (deliberate) |
| Hot Steppers | Common | walk on hazard floors | binary: huge or zero |
| Gecko Gloves | Rare | grants wall slide + wall jump | a movement verb |
| Blood Money | Rare | +1 dmg per 25 gold | +20/hit at 500 gold, uncapped |
| Pogo Boots | Rare | head bounce, 8 dmg, decaying chain | a movement verb |
| Sticky Fingers | Rare | first exhaust each room returns w/ 1 charge | see §3 |
| Do Not Pet | Rare | 20 dmg reflect in 3u when hit | see §3 |
| **Executioner's Seal** | Epic | instakill non-boss under 20% HP | **saves ~1 hit; nothing on bosses** |
| **Melt It Down** | Epic | +1 max Shift per relic sold | **+4 max Shift (+10%)** |
| Glass Heart | Epic | ×2 damage, ½ max HP | build-defining |
| Meteor Greaves | Epic | fall ≥6.5u → 12–55 dmg AoE, r 2.5–5.5, 1.5s cd | up to ~3.7 Fireballs, free, repeatable |
| Offering | Epic | Recall free, 5 self-damage | scales with Recall escalation |
| Phoenix Cog | Legendary | once/run survive lethal + 60 dmg blast | saves a lost run |

## 2. The headline problem: rarity does not track power

Inside the **Common** tier alone the spread is **20×** — Hot Streak pays +100 Shift, Pocket
Lightning +10, First One's Free ~5. Meanwhile two **Epics** pay less than the average Common:
Melt It Down is +4 max Shift, and Executioner's Seal saves roughly one hit per large enemy.

> ⚠️ **A Common is currently the strongest economy relic in the game, and an Epic is one of the
> weakest.** That is not a tuning drift, it is the tier ordering being inverted.

## 3. Four relics fight the game's own design

**⚠️ Hot Streak deletes the Shift economy, and CLAUDE.md is factually wrong about this.**
The file states Shift "does not regenerate on its own" and calls that persistence "the whole
identity of the game." It also records that Pogo Boots' `+1 Shift` per bounce was removed on
2026-08-10 for being *"the only free Shift regeneration in the game."*

It was not the only one, and it was not the biggest. **Hot Streak grants +2 per kill, is still
live, and is a Common** — a 5-enemy room refunds +10 Shift, a quarter of the starting pool, per
room, forever. Verified: `RelicManager.cs:258` is now the only unconditional Shift income from
combat in the project. Whatever the fix, this is the one that matters.

**First One's Free is close to a no-op, and this is already known.** Nine of the fifteen playable
cards cost 0 Shift and the dearest costs 2. The *blessing* version of this idea ("On the House")
was cut during the Blompo pass for exactly this measurement. The relic version shipped anyway.

**Do Not Pet pays you for being hit.** August's melee rebuild made enemy swings a box in front of
the attacker specifically so that getting behind something works — the game is asking you to dodge.
This relic is a 20-damage reward (more than a Fireball) for not dodging. It is a real *build* if
paired with Bubble Wrap, so it is not a delete — but it is not a Rare.

**Sticky Fingers taxes the only clock in the game.** Exhaust → depleted deck → Stagger → an
escalating HP bill is the run's sole death pressure. A permanent per-room exhaust refund is a
strong effect priced as a Rare.

## 4. The framework — what each rarity should BE

Derived from the relics that already work, not invented. Every relic on this roster that feels
good is a **verb or a rule**. Every one that feels flat is a **number**.

| Tier | Shape | The test | Model |
|---|---|---|---|
| **Common** | a small permanent number | "are my numbers slightly better?" | Bubble Wrap |
| **Rare** | **a new verb** — something you can *do* | "did I gain an input or an option?" | Gecko Gloves, Pogo Boots |
| **Epic** | **a rule change** — inverts an assumption | "does one of the game's rules now read differently?" | Glass Heart |
| **Legendary** | **a once-per-run miracle** | "does this save a run that was over?" | Phoenix Cog |

> ⚠️ **This corrects the note at the end of Stage 4's original slate.** That note says small
> passives are "the wrong shape for a slot system." They are the *right* shape — for Common.
> The doc is correct that Commons are cycle fuel you slot early and sell to fund Epics.
>
> **The real bug is not that Commons are boring. It is that Commons are boring AND stronger than
> the Epics.** A Common should be a small number; three of them are large numbers wearing a
> Common price tag.

## 5. Fixes, cheapest first

### Tier 1 — retune only (one number each, no new design)

| Relic | Change | Why |
|---|---|---|
| **Hot Streak** | +2/kill → **+1 on airborne kills only, max 3 per room** | Caps the leak, keeps the kinetic fantasy, ties it to the game's verb |
| **Snack Fangs** | 5 HP → **2 HP** per kill | 100 HP a run instead of 250 |
| **First One's Free** | → **first card each room spends no charge** | Same fantasy, aimed at the resource that is actually scarce |
| **Melt It Down** | +1 → **+4 max Shift** per sale | Must be worth a slot at Epic |
| **Executioner's Seal** | include bosses at a lower threshold (~8%) | Currently excluded from the one fight where the last 20% is a grind |

### Tier 2 — re-tier (free, changes only the rarity field)

- **Gecko Gloves** → **Epic.** It grants an entire movement ability; that is a rule change, not a verb.
- **Sticky Fingers** → **Epic.** It is a permanent discount on the run's only death pressure.
- **Do Not Pet** → **Epic**, and lean into it as the tank build's keystone.
- **Melt It Down** → **Common** if it is not buffed per Tier 1.
- **Executioner's Seal** → **Rare** if bosses stay excluded.

### Tier 3 — new design (the actual content gap)

Current spread is **8 / 5 / 5 / 1**. A 5-slot game wants the top end deep, because that is where
"what do I give up" lives. Target roughly **8 / 6 / 8 / 3**.

⚠️ **And nothing on the roster references the loadout itself.** That is the trick the slot system
was modelled on — pieces that care about your *other* pieces. Three seeds, designer's call:

- **Odd Socket** (Epic) — everything else is stronger while a slot stays empty. Makes *not*
  filling the loadout a build.
- **Understudy** (Epic) — copies the relic to its left. Mirrors the card blessing of the same name.
- **Pawnbroker** (Rare) — double sell value; makes cycling relics a strategy rather than a loss.

## 6. Leave alone

**Glass Heart · Phoenix Cog · Meteor Greaves · Pogo Boots · Loot Goblin · Bubble Wrap ·
Pocket Lightning · Hot Steppers.** Eight of nineteen are correctly shaped *and* correctly priced.
The pass is smaller than it looks.
