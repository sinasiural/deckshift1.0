---
name: deckshift-economy
description: Deckshift's currencies and progression — the scrap system and the gold/scrap split, the Scrap Forge, quests and the per-room oaths, and the relic system (slots, selling, the offer pool, the Boss tier and the one-Art cap). Use when touching scrap, gold, the forge, quests, oaths, relics, RelicPool/RelicCatalogue, or the shop's pricing.
---

# Deckshift — Economy, Quests and Relics

Split out of CLAUDE.md 2026-08-21. Nothing was deleted; this is the same text, moved so
it loads only when the work is actually about the economy. Every ⚠️ here was paid for
with a real bug — the reasons are the point, do not strip them.

## Scrap System (BUILT 2026-08-03)

**Scrap is the card-maintenance currency.** Earned from kills and from your own cards wearing out; spent at a **Scrap Forge** to put charges back on cards and to drag cards out of the exhaust pile.

### Why it exists

Before this, **killing an enemy paid literally nothing** — `EnemyHealth` had no drop logic at all (an earlier version of this file wrongly claimed it "handles drops"), and gold comes only from piles placed in levels, never from enemies. So a kill cost you HP and card charges and returned zero, making "skip every fight and platform to the exit" the optimal play in a game built around a deck of attack cards. Scrap is the payment for engaging with combat.

It is also load-bearing for the **planned difficulty tiers** (see Deferred Work → Run map): without a combat reward, a harder room is pure downside and no player would ever route into one.

### The gold / scrap split (designer-set, do not blur)

| | Gold | Scrap |
|---|---|---|
| **Comes from** | piles placed in levels (exploration; usually off the mandatory path, so reaching it costs Shift) | enemy kills + a small rebate when a card exhausts |
| **Buys** | NEW power — cards, relics at the shop | SUSTAIN — charges on cards you already own |

⚠️ **Never let these merge.** If the shop starts selling charges, or scrap starts buying cards, one of the two currencies is redundant and should be deleted. The reason recovery got its own currency at all is that **maintenance always loses to acquisition when they share a wallet** — given one pool, players buy the shiny relic over repairing a card every time, and the exhaust problem stays unsolved.

### Files

- **`ScrapEconomy.cs`** — **THE tuning file. Every scrap number lives here and nowhere else.** Drop tiers (derived from `maxHealth`, matching the `CardAnchors.md` §5 HP tiers), `RECHARGE_PER_CHARGE`, `SALVAGE_COST`, `EXHAUST_REBATE`, plus `ScrapColor` and `UIFont()`.
- **`ScrapPickup.cs`** — the collectible. **Built entirely in code (no prefab)**, so there is nothing to wire and nothing to lose from a scene. Deliberately has **no Rigidbody2D**: the pop-out arc is hand-integrated against a ground raycast, because a solid collider would shove the player's capsule and a trigger-only rigidbody would fall through the floor. Carries `TemporaryObject`, so uncollected shards are wiped on room change.
- **`ScrapForgeScreen.cs`** — the spend UI. Self-instantiating procedural screen, same pattern as `BlompoScreen`, but styled with **`FlatUI`, not `RelicUISprites`** (see UI System → Flat theme).
- **`ScrapForge.cs`** — the `IInteractable` station that opens it. **Unlike Blompo it does NOT vanish after use** — it's a workbench, and the scrap cost is the limiter, not the visit.
- **`ScrapHUD.cs`** — the counter. Self-bootstraps via `RuntimeInitializeOnLoadMethod` and **positions itself relative to the existing `ExhaustPile` button** rather than at fixed coordinates.

### Design rules baked in

- ⚠️ ~~**Scrap sits with the deck/exhaust pile UI, NOT in the resource panel.**~~ **OVERRULED by the designer 2026-08-09.** The reasoning (scrap is deck-maintenance, so it belongs with the deck UI) did not survive contact with play: bottom-right it read as a stray widget, and having the two CURRENCIES in opposite corners made neither easy to check. **Scrap now sits directly under the gold counter**, and both are built from `HudChip` so they are one piece of geometry rather than two that happen to agree. The resource panel is now two **bars** (health, Shift — bounded, so a fill is the honest shape) above two **chips** (gold, scrap — unbounded counts, so a number in a plate). ⚠️ `ScrapHUD` re-anchors in `LateUpdate`, not once in `Build()`: it is created from a `sceneLoaded` bootstrap that runs before any `Start()`, so at build time `ResourcePanelHUD` has not laid the gold row out yet and a one-shot read parks it in the wrong place.
- **Kills must out-earn the exhaust rebate by roughly 10:1.** Kills are the lever that changes behaviour; the rebate is only a consolation so losing a card isn't a total loss. If the rebate ever dominates, you've accidentally incentivised burning your own deck down.
- **Salvage returns a card only HALF charged**, so a full recovery is salvage + repair. Exhaust must stay a real loss.
- **Target: one act of income rescues ONE OR TWO cards, never the whole deck.** Scarcity is the point — charges depleting is what feeds Stagger, and Stagger's escalating HP price is the run's only real death pressure. Make repair comfortable and that pressure quietly disappears.
- **Scrap spending is NOT hub-exempt.** The umbrella "free in hub" rule covers resources the sandbox *drains* from you; a forge repair is a purchase that permanently improves the run, exactly like a shop buy (which the hub already charges for). Free repairs in the hub = infinite deck refills.
- Both `DeckManager.TryRechargeCard` / `TrySalvageCard` are **all-or-nothing** — verified by test that a refused operation never charges the player.

### The forge prop — `Assets/Prefabs/ScrapForge.prefab` (built 2026-08-09)

**There is now a real forge prefab; drop it into any room.** Before this the only forge in the game was the hub's `PF Dungeon Props - Chimney 01` — a wall chimney with hanging chains — with the `ScrapForge` script bolted onto it. It looked nothing like a forge because it wasn't one.

Composed from stock Cainos props, so it costs no art:

| piece | source |
|---|---|
| **Hearth** | `PF Dungeon Props - Fireplace 01`, nested so it keeps its own **fire, sparks and glow particles** |
| **Anvil** | `PF Village Props - Anvil 01` |
| Hammer / Bucket / ScrapBin | raw sprites (`TX Village Props - Hammer`, `Dungeon Bucket 01`, `Dungeon Metal Basket 01`) |
| **ForgeGlow** | a `Light2D` **added by us** |

⚠️ **The Cainos fireplace's own `Light` is a 3D `Light`, which the URP 2D renderer ignores** — its fire throws no light at all. The warm `Light2D` point light is what actually makes it read as lit, and it spills onto the surrounding tiles.

⚠️ **Pivots are NOT consistent across the Cainos props.** Hearth and Anvil are bottom-centre (`pivot.y = 0`), so local y=0 puts them on the floor — but **`Bucket 01` and `Metal Basket 01` are CENTRE-pivoted** and sink half their height into the floor at y=0. Check `sprite.pivot` before placing a new piece.

⚠️ **The trigger collider must be on the SAME GameObject as the `ScrapForge` component** — `PlayerController` does `OverlapCircleAll(...)` then `hit.GetComponent<IInteractable>()`, so a collider on a child is invisible to it. Layer **Interactable (12)**. Verified reachable from both sides.

It carries an `InteractPrompt` child (the "press E" keycap) at local **(0, 3.45)**, ~0.3 above the chimney — the same relationship the chests use to their lids. ⚠️ **The trigger box must track the ART.** When the designer's pass removed the bucket and bin, the collider still had the original wide layout's `size 5.6 / offset 0.85`, so the zone — and therefore the prompt — reached ~3 units out into bare floor. Resized to `3.6 x 2.8` at offset `(-0.3, 1.2)`. **Re-check the trigger whenever the prop's contents change.**

### The "press E" prompt: one-shot interactables must guard `OnTriggerEnter2D` (2026-08-09)

`InteractPrompt` is driven purely by the interactable's own `SetActive(true/false)` on trigger enter/exit. That is fine for **repeatable** stations (`ScrapForge`, `Lever`, `SimpleInteract`/QuestBoard — always offer the prompt), but a **one-shot** interactable needs two extra lines or the keycap lies:

- **hide it in `Interact()`**, because the player is still standing inside the trigger when it fires and nothing else would take it down;
- **guard `OnTriggerEnter2D` with the spent flag**, or it returns every time the player walks back past a thing they already used.

`Chest` (the golden relic chest) had neither, so its prompt sat over an opened chest and came back on re-entry, inviting an `Interact()` that returns immediately. `CardChest` and `BlompoNPC` were already correct; `Chest` now matches them.

⚠️ **A wired `InteractPrompt` child does not mean a working prompt.** `CardChest` (the **silver** chest — same `Chest Golden` sprite tinted blue `0.66, 0.78, 1.0`) shipped with an `InteractPrompt` child sitting in the prefab and its `prompt` **field left null**, so it silently never showed one. Check the field, not just the hierarchy.

`ShiftAltar` deliberately has no prompt — its floating "N SHIFT" cost label is its affordance.

**Placing it needs THREE clearances, not one** — the assembly is ~5.6 wide and the hearth is **3.13 tall**:
1. floor to stand it on,
2. ~5.6 units of horizontal room,
3. **3.2 units of headroom** — this is the one that bites. In the hub, ray-casting up from the floor showed open headroom only at x 15–19; a stone shelf overhangs x 19.5–21.25 at y 11.65. The forge sits at hub-local **(18.40, 10.65)** with the chimney in the open slot and the anvil/bucket/bin under the shelf.

⚠️ **The hub's floor is at y = 10.65; the old chimney hung on the WALL at 12.59.** Reusing the old prop's position put the whole forge in mid-air. Measure the floor with a downward raycast on the Ground layer — don't inherit a decorative prop's transform.

**Still true: this is the only forge in the game, and it is in the hub** — the first room of every run, visited once, before you have any scrap or any damaged cards. Scrap therefore still has nowhere to be spent mid-run. Now that it's a prefab, fixing that is a drag-and-drop into combat rooms, or into the unbuilt Foundry recharge room (`LevelManager.foundryRoomPrefab`, still empty).

### Card Effect Conflict Class of Bug (KNOWN)

Discovered when hub mode allowed free card spamming: playing multiple state-modifying cards in close succession (e.g., Floor is Lava + Adrenaline + Phase) can leave the player in a permanently broken state (flying, frozen gravity, etc.). Each card's effect captures "original" state at start and restores it at end, but **none of them know about each other**. Card A captures the current state (already modified by still-active Card B), then later restores to that mid-effect snapshot — corrupting baseline.

**Current state (updated 2026-07-06): RESOLVED.** The CardActionExecutor extraction is done AND conflict-flag enforcement is live. Each `CardAction` declares a `ModifiedState` (`ConflictFlags`); the executor accumulates flags in `activeFlags` (via `ManagedCoroutine` for coroutine actions, via `SetManualFlag` for the manual-lifecycle ones) and **`TryExecute` now checks them: if an action's `ModifiedState` overlaps `activeFlags`, it is refused up front with `CardExecuteResult.Blocked` and none of its code runs.** A blocked play costs no Shift and no charge, and the card stays in hand (`DeckManager.PlayCard` only spends/consumes on `Success`). The state-corruption bug class (Floor is Lava + Adrenaline + Phase leaving the player flying/frozen) can no longer occur — the conflicting second card is refused instead of corrupting the baseline snapshot.

Per-effect conversion status:
- **Dash** ✅ converted — managed coroutine; flags `PlayerVelocity | Invincibility` held for the whole dash. **Reworked 2026-07-06 into a driven dash** (`PlayerController.DashRoutine`): enters `PlayerState.Dashing` and holds a flat horizontal velocity for `dashDuration` (re-asserted each FixedUpdate with y forced to 0), so it works on the ground too — the old one-shot `AddForce` impulse was erased the next frame by the grounded movement line (`rb.linearVelocity = moveInput * moveSpeed`). Never touches `gravityScale` (composes cleanly with Floor is Lava). Procedural afterimages via `DashAfterimage.cs`; tunables `dashSpeed`/`dashDuration`/`dashEndSpeed`/`dashIFrameDuration`/`dashAfterimages` on PlayerController. **Live Player.prefab values (verified 2026-07-18): dashSpeed 26, dashDuration 0.16, dashEndSpeed 9, dashIFrameDuration `0.15`.** ⚠️ Note `dashIFrameDuration (0.15) < dashDuration (0.16)`, which violates the field's own inline invariant ("keep >= dashDuration to stay safe through the dash") — the player is damageable for the last ~0.01s of the dash. The script default is 0.22; the prefab overrides it to 0.15. Harmless in practice but unintended; raise it to ≥ 0.16 if i-frames should truly cover the whole dash.
- **Phase** ✅ converted — managed coroutine; flags `GravityScale | LayerCollisionMatrix | PlayerVelocity`.
- **Adrenaline** ✅ converted (manual-flag pattern) — `UseAdrenaline`'s two sub-coroutines are mutually exclusive (`if/else` on health %), and each calls `SetManualFlag(TimeScale | MoveSpeed, …)` at start/end. The old "not refcounted / overlapping plays clear flags early" caveat is now moot: a second Adrenaline play while one is active is Blocked (its flags overlap), so concurrent same-flag effects can't happen.
- **Fireball** ✅ converted — managed coroutine; `AnimatorAttackState`.
- **ReverseGravity** ✅ converted (manual-flag pattern) — `StartGravityReversal`/`GravityReversalRoutine` now call `SetManualFlag(GravityScale | VisualTransform, …)` with a restart-safe lifecycle: flags are cleared BEFORE `StopCoroutine` and re-set synchronously inside the new `StartCoroutine`, so there is never a flags-set-but-no-routine window and the clear can't stomp the new set. The same-card timer-refresh branch is now unreachable (a replay while active is Blocked because its flags overlap `activeFlags`); it's kept deliberately in case the policy later allows same-card refresh.

**Known interaction (found 2026-07-06):** enforcement makes the **Echo Chamber** skill's instant double-cast (`DeckManager.PlayCard` re-calls `ExecuteAction` immediately after the first play) silently no-op for *stateful* cards — the second cast's `ModifiedState` overlaps the first's still-live flags and is Blocked. It still works on instant cards (Jump, Glass Wail, etc.). Fix options if this becomes design-relevant: defer the echo cast until the first effect ends, or let a same-card replay bypass the block. Not yet done — flagged, not urgent.

**Enforcement applies everywhere, including the hub** (where free card spamming used to make this bug trivially reproducible). The class is now handled centrally in `TryExecute`, so there is no need to patch individual cards.

### Card Aim Indicator System (2026-07-17)

`Assets/Scripts/CardAimIndicator.cs`, on the **Player prefab root**. Watches `DeckManager`'s selected card every frame and shows an honest world-space preview of what the card will do when cast. All visuals are procedural (house pattern: no prefabs, no art — like `DashAfterimage`/`EnemyHealthBar`). Hidden while paused, dead, or when nothing/a non-indicator card is selected; everything dims when the player can't afford the card's **effective** Shift cost (mirrors `PlayCard`'s gate exactly: KineticDiscount and `isNextCardFree` included — note the affordability GATE applies even in the hub; only the spend is hub-exempt).

Per-card previews (each mirrors the real mechanic's math — **if you change a card's range/center/cost, update the matching `Update*` method or the indicator becomes a lie**):

- **Fireball** — ember dots flowing along the true flight line (capsule-cast with the real fireball collider, so short targets register) + pulsing impact ring; ring is orange on walls, **hot red when the impact would be an enemy**.
- **Dash** — afterimage trail: 4 translucent silhouettes along the wall-clamped true path, strongest at the destination. **The Cainos body is SkinnedMeshRenderers, so parts are baked per frame via `SkinnedMeshRenderer.BakeMesh`** and drawn as tinted MeshRenderer copies (Sprites/Default material carrying each part's texture); the staff is the only SpriteRenderer.
- **Vampiric Bite** — ring + soft fill at the true radius; **green when an enemy is inside (play lands), dim red when it would be refused**. Validity re-scanned on a 0.08s timer with the exact same filter as `PerformVampiricBite`.
- **Portal** — ghost portal follows the cursor from selection; neutral gray before the first placement, **cyan in-range / red out-of-range** while the second is pending (reads `PlayerController.FirstPortalInstance`, an accessor added for this).
- **PlatformCreate** — ghost of the platform prefab's actual sprites at true size on the cursor. (The card itself has NO range limit or placement rules — the ghost shows that honestly.)
- **FreefallBlade** — the true ")" slash circle (forward-and-low, same offset math as `PerformFreefallBlade`); **grows while falling** (the empowered arc) and colors pale-blue neutral / orange falling / green enemy-inside.
- **GlassWail** — two expanding ripples from the body + a pulsing glint over every `EnemyHealth` in the scene (the wail is scene-wide; enemy list refreshed on a 0.25s timer).

**Adding an indicator for a new card:** add a `Kind`, an `Ensure*Visuals()` builder + `Update*(dim)` method, and a case in both the `LateUpdate` switch and `SetKind`. Read the real mechanic's code first and mirror its numbers exactly.

Related: `Assets/Scripts/PortalRangeRing.cs` — the first portal's range border is now a procedural rotating dashed ring + traveling wave (spawned by `Portal.ShowRangeCircle` at the EXACT gameplay radius, parent-scale-compensated). The old flat `rangeIndicator` sprite on the Portal prefab is kept assigned but permanently hidden — do not re-enable it.

---

## Quest System

The quest system is **functional**: data model, accept/progress/complete events, board UI, and live tracker HUD all working as of the most recent session.

### Data

- **`QuestData`** (ScriptableObject) — quest templates. Fields: `questName`, `description`, `type` (QuestType enum), `targetAmount`, `rewardText`, `rewardType` (RewardType enum), `rewardAmount`.
- **`QuestType` enum:** `GoldAccumulate`, `KillEnemy`, `AirKill`, `NoDamageRoom`, `UseCardCount`, plus the four **oaths** added 2026-08-10 (`NoCardsRoom`, `NoRecallRoom`, `LowShiftRoom`, `NoStaggerRoom`). **Eight of nine fire; only `UseCardCount` is still unwired.**
- **`RewardType` enum:** `Gold`, `ShiftCharge`, `Heal`, plus `Card`, `Scrap`, `MaxHealth` (2026-08-10). All six are wired in `QuestSystem.GiveReward`. ⚠️ **`MaxHealth` goes through `PlayerHealth.IncreaseBaseMaxHealth`, which raises `baseMaxHealth` and re-runs `RelicManager.RecomputePassives()`** — writing to `maxHealth` directly would be silently erased the next time the player gained or sold any relic, since passives are always rebuilt from the base.
- **`QuestData.objectiveParam`** — a second number for objectives whose target is a count. Only `LowShiftRoom` reads it (the per-room Shift ceiling). **`QuestData.rewardCard`** — only read when `rewardType` is `Card`; empty draws at random from `CardPool`.

### Oaths — the per-room streak contracts (2026-08-10)

Four quest types share one recorder in `QuestSystem` (`BeginRoom` / `NoteCardPlayed` / `NoteRecall` / `NoteShiftSpent` / `EndRoom`), so **adding a fifth oath is a switch case, not a system**. `BeginRoom` is hooked in `PlayerController.OnNewRoomEnter`; `EndRoom` in `ExitDoor.PerformExit`.

⚠️ **They are STREAKS, not tallies.** Clearing a room inside the oath adds one; breaking it resets the count to **zero**. That is what makes them read as a commitment rather than a checklist, and it is also why a break can never dead-end a run — the next room starts clean, so the contract stays winnable. Verified: 2/3 → break → 0/3 → next clean room → 1/3.

Design rules baked in, each because the obvious alternative is wrong:
- ⚠️ **`EndRoom` is called OUTSIDE the flawless-clear block in `ExitDoor`.** Nested inside it (where the `NoDamageRoom` report lives) an oath would only ever be scored on rooms that also happened to be damage-free.
- ⚠️ **Hub-excluded.** Nothing is spent in the sandbox, so every oath would pass there for free.
- **Nothing is judged until the room is LEFT.** A violation isn't final until then. The tracker HUD shows the break live so it isn't sprung on the player at the door, but the reset happens once, at the exit.
- **`NoteCardPlayed` fires on `success` alone**, not inside the `!keepInHand` branch — a card that stays in hand (Portal's first placement) has still been played. Blocked/failed plays don't count; refusing a card must not break an oath.
- **`NoteRecall` sits after every early return in `TryRecall`**, so a refused recall (not enough Shift) doesn't break No Take-Backs — the player didn't get one.
- **`NoteShiftSpent` hangs off `PlayerController.SpendShift`**, the single funnel every Shift cost in the game passes through, so Featherweight gets a complete per-room total from one hook.

⚠️ **COMPLETED CONTRACTS DO NOT OCCUPY A SLOT** (fixed 2026-08-10). `ActiveCount` counts only *incomplete* quests. Counting the whole `activeQuests` list capped the player at three contracts for the **entire run** — finish three and the board silently refuses every further offer. `activeQuests` remains the full record so the board can still draw finished contracts as COMPLETE.

**The four authored oath assets** (`Assets/Quests/Oath_*.asset`): Deck's Closed (3 rooms, no cards → a card) · No Take-Backs (3 rooms, no Recall → +4 max Shift) · Featherweight (3 rooms, ≤8 Shift each → +6 max Shift) · Sober Streak (4 rooms, no Stagger → +10 max HP). ⚠️ **The permanent-stat numbers are deliberately set about a third below what the design pitched** — permanent max Shift is the strongest thing in the game and these want to be felt in a real run before being raised. One Inspector field each.

⚠️ **`Scrooge` is deliberately NOT in `allQuests`.** Its `rewardAmount` is 0, so it would appear on the board as a contract that pays nothing. It is waiting on **Rich Man's Dagger** (the card whose damage scales with held gold) and on `GoldAccumulate` becoming a peak/hold check rather than a running total.

**Payout rule (designer-set):** *quests pay in things the shop doesn't sell.* Gold is the buying currency, so paying gold is just handing out a discount — it's reserved for the lightest contracts, if at all. The tighter form is **pay in the thing the oath was made of**: gave up cards → paid a card; gave up Recall and spending → paid Shift capacity; avoided Stagger (which charges HP) → paid HP.
- **Four quest assets exist** at `Assets/Quests/` (re-verified 2026-07-18 — an earlier version of this file said three):
  - `New Quest 1` — "Invincible" — NoDamageRoom (1) → 300 Gold. **Objective type not wired, won't progress yet.**
  - `New Quest 2` — "Hit a Clip" — AirKill (3) → +10 ShiftCharge. Fully functional.
  - `New Quest 3` — "Bounty Hunter" — KillEnemy (3) → 100 Gold. Fully functional.
  - `Scrooge` — "Scrooge" — GoldAccumulate (800) → Gold **0**. ⚠️ Two problems: `GoldAccumulate` is still an unwired objective type (won't progress), AND its `rewardAmount` is 0, so it would pay nothing even if completed. Looks unfinished.

### QuestSystem Singleton

Located on a `QuestSystem` GameObject in SampleScene. Holds:
- `allQuests` — list of QuestData assets the board can pull from (⚠️ **3 of the 4 assets are wired in** — `Scrooge` is not in the list).
- `activeQuests` — `List<ActiveQuest>` (inner serializable class). Each `ActiveQuest` has `data` (QuestData), `currentAmount` (int), `isCompleted` (bool).
- **No serialized UI fields any more** — see Quest Board UI.

Key methods:
- `ToggleBoard()` / `CloseBoard()` — one-liners onto `QuestBoardScreen`, which owns the pause and the HUD hide.
- `Offer` / `EnsureOffer()` — the pinned contracts, shuffled and rolled **once per run**.
- `AcceptQuest(QuestData)` — **returns bool**; refuses duplicates and refuses past `MaxActiveQuests`. Fires `OnQuestAccepted` only on a real acceptance.
- `FindActive(QuestData)` / `ActiveCount` — what the board reads to draw each slip's state.
- `ReportEvent(QuestType, int)` — iterates activeQuests, increments `currentAmount` on matching quests, fires `OnQuestProgress`, then calls `CheckCompletion`.
- `CheckCompletion(ActiveQuest)` — if `currentAmount >= targetAmount`, sets `isCompleted = true`, fires `OnQuestCompleted`, calls `GiveReward`.
- `GiveReward(QuestData)` — delivers reward immediately. **Not deferred to level-end yet** (on the deferred list).

### Events (for HUDs and other listeners)

```csharp
public event System.Action<ActiveQuest> OnQuestAccepted;   // fired after successful add (not on duplicate-accept)
public event System.Action<ActiveQuest> OnQuestProgress;   // fired after currentAmount++, before CheckCompletion
public event System.Action<ActiveQuest> OnQuestCompleted;  // fired after isCompleted=true, before GiveReward
```

### Quest Board UI — `QuestBoardScreen.cs` (rebuilt from scratch 2026-08-10)

⚠️ **THE PAINTED BOARD IS GONE.** The designer disliked the artwork, so `QuestBoardOverlay` (and its `Panel`/`QuestContainer`/`LeaveButton` hierarchy), `QuestItemTemplate.prefab`, `QuestPaper.cs` and `QuestBoardFX.cs` were all **deleted**, along with QuestSystem's `overlayPanel` / `container` / `paperPrefab` fields. **QuestSystem now holds no UI references at all** — `ToggleBoard()`/`CloseBoard()` are one-liners onto the procedural screen, which builds itself on demand under the Canvas. Do not re-create a scene-placed quest board. (The old background sprite `Slide_16_9_-_5_0` still exists as an asset; nothing points at it.)

The theme is **Bulletin** — see UI System → Themes for why it's the one screen whose value structure is inverted. Mechanically:

- **The rotation pivot of each slip sits under its TACK** (`SLIP_PIVOT_Y = 0.94`), not at its centre. That is the whole reason the sway reads as paper hanging from a pin rather than a card wobbling in space, and it costs one line.
- **Hovering a slip STOPS its sway** and lifts it off the board. Stillness is the selection signal — it's the only motionless thing on the board, which is clearer than any highlight. (No flip-flop risk: the slip grows on hover, so the cursor stays inside it.)
- ⚠️ **The wax seal goes in the TOP-RIGHT corner of a slip, not the bottom.** The bottom holds the payout block and the progress bar — the two numbers that only start mattering once a contract is actually taken — and a 100px blob dropped there covers both. The top corner is the only region empty at every content length.
- ⚠️ **A dark dot cannot mark anything on a surface this dark.** The pin holes are readable because of the pale crescent on the side away from the lamp, drawn OVER a wider dark smudge. Rim-only reads as dust or stars; dark-only is invisible. Same family of lesson as "hairlines need to be brighter than theory says" — the header rule was invisible at `T.Border` and had to move to `EdgeLight`.
- ⚠️ **Grain/wear lines must land in bands the layout leaves EMPTY.** One placed between the hint and the LEAVE button instantly reads as a divider rule nobody asked for.
- `FlatUI.WaxSeal()`'s impression is **four diagonal spokes that stop short of the ring**. Six evenly spaced spokes running out to a ring is a citrus slice — that is genuinely what the first version looked like.
- Two new sounds, `ProcSfx.PaperRustle` and `ProcSfx.WaxStamp`. They are the only sounds in the game with **no pitched component at all**, which is what makes paper a distinct family rather than a variant of the stone hits. The stamp's three layers decay in the order the physical action happens (wax squashes, seal bottoms out, sheet creases last).

**Behaviour fixed in the same pass, because the old board was dishonest about its own state:**

- **The offer is rolled ONCE per run** (`QuestSystem.Offer`), not regenerated on every open. The old board rebuilt its slips each time it was opened, so with a pool bigger than three, closing and reopening would have been a free reroll.
- It **shuffles** instead of always taking `allQuests[0..2]`, so quests past the third could never be offered before.
- **`AcceptQuest` returns bool and enforces `MaxActiveQuests`.** It used to silently no-op on a duplicate and had no cap at all.
- Accepted / completed contracts now **show as accepted** (seal, status line, live progress bar) instead of looking fresh and swallowing the click.
- ⚠️ **`BoardSlots` and `MaxActiveQuests` are deliberately SEPARATE numbers.** A board offering exactly as many jobs as you can carry is a checklist, not a decision. Both are 3 today only because the offer would otherwise exceed what you can carry; the "no room, greyed out" state is already drawn and becomes reachable the moment `BoardSlots` is raised (which also needs `BOARD_W` widened — the slips are one row).

The QuestBoard in `Assets/LevelEfeS/hub.prefab` has a `SimpleInteract` component on it (implements `IInteractable`) that calls `QuestSystem.ToggleBoard()` on player interact (press E within `interactionRange`). The board's Layer must be in PlayerController's `interactableLayer` mask. ✅ **VERIFIED 2026-07-18** (this was previously an open "someone please check" item): `interactableLayer` = **4096 = layer 12**, layer 12 **is** named `Interactable`, and the hub's `QuestBoard` object is on layer 12 with a `SimpleInteract` component. The wiring is correct — no action needed.

### Live Tracker HUD — `QuestTrackerHUD.cs` (rebuilt 2026-08-10)

On the `QuestTracker` GameObject under `Canvas/GameplayHUD/`, so it still inherits the HUD auto-hide when a full-screen panel opens. **`QuestRowPrefab.prefab` and `QuestTrackerRow.cs` were DELETED** — it is fully procedural now and needs no prefab.

The rows are **slips off the quest board**: same Bulletin material, pale paper, brass tack, ink text, wax seal on completion. Nothing else in the HUD is made of paper, so the corner of the screen identifies itself before a word is read. Deliberately quieter than the board (narrow strips, a third of the sway, no grain/fold/perforation) per the Loadout rule that a permanent overlay must not compete with the game behind it.

- Each slip carries **the contract's requirement**, read straight from `QuestData.description` rather than derived from the type — so it can never drift from what the board says, and editing a quest's text updates the HUD for free.
- **The live break warning is the point.** `QuestSystem.IsOathBroken` drives a wax-red edge flag and a red title, and **replaces the requirement line** with "BROKEN THIS ROOM" in wax red. This is the only place in the game that tells you an oath is already lost for the room you are **standing in** — the board can only ever say so afterwards. It swaps rather than stacking: once the oath is broken the requirement is no longer the thing you need to read, and it keeps the strip a line shorter.
- The progress fill **eases** toward its target, so a collapsing streak visibly *drains* instead of snapping to zero.
- Completion stamps the seal, holds, then the slip comes **off the pin and falls away** — a contract that merely faded would read as the tracker forgetting it.

⚠️ **The scene object still carries a `VerticalLayoutGroup` + `ContentSizeFitter` from the old prefab tracker, and `Start()` disables both.** They relaid every slip *and every slip's shadow* as separate list items, spacing rows at 84+4+84+4 = **176 instead of 94** and shoving them sideways. Rows here are pivoted at their pin and rotated every frame, so a layout group can never own them. Slips are also built into a dedicated `Slips` child layer with no layout components, so this stays correct if anyone re-adds one.

⚠️ **`anchoredPosition` places the PIVOT, and this pivot is the pin in the top-left corner.** Positioning rows at x = 0 hung 90% of each strip to the right of the anchor and pushed them 74px off the screen, cutting the counts in half. `RowRest()` backs that offset out. Same reason the title is indented — a full-width title box runs its first two characters under the tack.

⚠️ **A slip's drop shadow must be ANCHORED exactly like the slip**, not merely positioned to match it. The local `AddImage` helper anchors to the parent's CENTRE while `AddPoint` anchors to its TOP, so the two shared an `anchoredPosition` but measured it from origins 300px apart — every shadow rendered as a free-floating black rectangle in the middle of the screen, well away from the slip it belonged to. Reported by the designer as "a black overlay completely not in the right place". **Two objects that track each other by position must agree on their anchors first**; copying the position is not enough.

### Quest content — the system's binding constraint

**8 quest assets exist; 7 are offered.** Three originals (`Invincible` NoDamageRoom, `Hit a Clip` AirKill, `Bounty Hunter` KillEnemy) plus the four oaths. `Scrooge` is authored but deliberately **not** in `allQuests` — see the oath section for why.

The board is built to offer more contracts than you can carry, which is what makes taking one a decision — but `BoardSlots` and `MaxActiveQuests` are both 3, so today you can still take everything offered. **Raising `BoardSlots` is now a one-number change**: the board widens itself from the slot count and scales down if it would overflow a narrow aspect. Whether to raise it (and whether to lower the carry cap to 1–2) is an open DESIGN decision the designer had not settled — see the fifteen-quest list discussed 2026-08-11, of which only the four oaths were built.

---

## Relic System

**The Balatro-style slot-constrained system is BUILT and live (corrected 2026-07-26 — this section previously claimed it was an unbuilt "future direction", which was badly stale).** The player owns at most **`RelicManager.MaxSlots` = 5** relics; acquiring one while full forces a sell-or-decline decision. It is no longer an unlimited additive pile.

What exists today:
- **Slots + selling** — `MaxSlots`, `IsFull`, `SellValueFor(relic)` (fixed refund by rarity: Legendary 150 / Epic 90 / Rare 50 / Common 25), `SellRelic(relic)` which removes, credits gold, fires `OnRelicRemoved` and calls `RecomputePassives()`.
- **Central grant entry point** — `TryGrantRelic(relic, onAcquired)`. Slot free → add immediately and run `onAcquired`. Slots full → open `RelicSwapScreen`; TAKE sells the chosen relic then adds the new one and runs `onAcquired`, LEAVE runs nothing. **`onAcquired` is where callers finalize side effects (e.g. the shop charges gold ONLY when the relic is actually taken), so a declined full-slot grant costs nothing.** New grant sources should route through `TryGrantRelic`, not `AddRelic`.
- **UI** — `RelicHUD` (top-centre loadout bar), `RelicSlotHover` + `RelicTooltip` (hover info), `RelicManagePanel` (inspect/sell, `I` key), `RelicSwapScreen` (the forced full-slot decision). All procedural, all sharing `RelicUISprites`.

**Passive recomputation rule (important):** `RecomputePassives()` recalculates stat relics from the player's BASE stats every time the loadout changes, so selling reverses exactly. **Never add/subtract stats incrementally** — that breaks the moment relics stack (Reinforced Plating + Glass Heart) or are sold out of order.

Still open (see deferred work): rebalancing the 19 relics *for* a slot economy — they were authored as small always-on Slay-the-Spire bonuses, which is the wrong shape for a 5-slot loadout where each pick should be a real decision.

### Card offer pool — `CardCatalogue` + `CardPool` (2026-08-09)

⚠️ **CARD AVAILABILITY IS NO LONGER GATED BY ACHIEVEMENTS.** `RewardManager` used to pull its pool from `AchievementManager.GetAvailableCardPool()`, which returned only `defaultUnlockedCards` (11 of 15) plus the reward cards of **completed** challenges — and exactly one challenge is authored. The shop drew from a separate hand-kept `ShopManager.allCardsPool` (10 of 15). Between them, **`DeadWeight`, `FreefallBlade` and `GlassParry` could never be obtained by any means**, silently.

The designer regrets putting the achievement system in this early and wants a proper one for cards/relics near release. `AchievementManager` still tracks and saves challenges — **it just no longer decides what exists**. Same machinery as the relics: `CardCatalogue` (auto-rebuilt asset) + `CardPool`.

⚠️ **`Stagger` must never be offered.** It is not a card the player owns — it is conjured into the hand on 0 Shift and evaporates when spent (see Stagger Mechanic). Rewarding or selling it would put a *permanent* copy in the deck that arrives on ordinary draws: a card that only charges HP, handed to a player who never asked for it. `CardPool.IsRewardable` excludes it by comparing against `DeckManager.staggerCardData`, **not by name**, so renaming the asset can't reintroduce it. Verified: 3000 reward draws surfaced all 14 legitimate cards including the three formerly unreachable ones, and Stagger zero times.

### Relic offer pool — `RelicCatalogue` + `RelicPool` (2026-08-08)

**Never hand-maintain a list of relics again.** The shop and the chests each carried their own Inspector list and both had silently fallen behind the roster: **18 relics existed, `ShopManager.allRelicsPool` held 3 and `Chest.prefab` held 5 across its four tiers**. Nothing was broken in code — the lists were simply never updated when relics were added, and there is no way to notice that from inside the game.

- **`RelicCatalogue`** — a ScriptableObject at `Assets/Resources/RelicCatalogue.asset` listing every `RelicData`. Rebuilt automatically by `Editor/RelicCatalogueBuilder` (an `AssetPostprocessor`) whenever a relic asset is added, removed, moved or renamed, plus a **Deckshift → Rebuild Relic Catalogue** menu item. It also warns about empty or duplicated `relicID`s, which silently break `HasRelic()`.
- **`RelicPool`** — the only thing that answers "what may be offered right now". `All`, `Offerable(rarity, restrictTo)`, `PickOfferable(rarity, …)` (steps down tiers, then up), `DrawDistinct(n, …)` for stocking a shelf.

⚠️ **An owned relic is never offered, and ownership is read AT THE MOMENT OF THE OFFER.** Chests used to hand back a relic you were already wearing — a dead reward for a room you paid to cross. Reading the live loadout also gives the sell-behaviour for free: **selling a relic puts it straight back in the pool**, with no bookkeeping. Comparison is by `relicID`, not asset reference.

⚠️ **`Chest`'s four per-tier relic lists were DELETED (2026-08-08) — do not reintroduce them.** They held 5 relics across four tiers, so a chest could only ever hand out those five. Once the player owned enough of them the chest had **nothing left to offer**, `PickRandomRelic` returned null, and the swap screen never appeared — the designer reported chests as broken after 5 relics, and this was why. Keeping them as an *optional* curated override did not help: they were populated, so the override was always on. A chest now draws the whole roster. If per-chest curation is ever wanted, add **one** list, not one per tier — a per-tier list also breaks the rarity fallback, because stepping to another tier re-searches the same single-tier list and finds nothing.

`Shopkeeper.specificRelicPool` survives as a genuine per-shop restriction (**empty = whole roster**, the normal case). `ShopManager.allRelicsPool` is deliberately no longer consulted; copying it into the shopkeeper is precisely what capped the stock at 3.

**A chest is never empty.** If the loadout is full the swap screen opens; if the player declines — or the screen cannot open at all — `onDeclined` pays the relic's **sell value** in gold, so the payout still scales with the rarity that was rolled. Verified: 6 consecutive chests at a full loadout all raised the swap screen, DECLINE paid the sell value, and TAKE swapped the loadout without double-paying.

Verified: 500 chest rolls returned zero owned relics; 200 shop restocks produced zero worn or duplicate offers; with a full 5-slot loadout the pool correctly reports 13 of 18 offerable, and selling restores the sold relic.

### RelicManager

Singleton. Holds:
- `ownedRelics` — private list of owned `RelicData`; **list index == slot index**.
- Public `OwnedRelics` — `IReadOnlyList<RelicData>` accessor.
- Public events `OnRelicAdded` / `OnRelicRemoved` — `System.Action<RelicData>`, fired after a successful add/sell (not on duplicate-add). HUD and panels rebuild on both.

Grant paths:
- `TryGrantRelic(relic, onAcquired)` — **the entry point everything should use** (handles the full-slot swap flow).
- `ShopItemUI` — buying a shop item with a relic reference.
- (`SlotMachineUI` used to grant relics here; it has been deleted.)
- `DebugTools.cs` F1 key — debug only.

**No starting-relic infrastructure exists yet.** Every run begins with zero relics. Adding a starting relic system (e.g., a wizard who begins with a Fireball relic) is on the deferred list.

### RelicData ScriptableObject

Fields: `relicID` (string, used for `HasRelic` polling), `relicName`, `description`, `relicArt` (Sprite, used by the HUD), `rarity` (enum).

**Relic roster — 19 relics as of 2026-08-11 (GeckoGloves added), all wired.** (An earlier version of this file listed only 7, including `New Relic 1` / "Oops! All 7's" and `Helly` — those are gone, and the "only 5 are functional" claim was badly stale.) The roster was renamed to the playful house voice (see Tone & Voice), so **asset filename ≠ display name ≠ `relicID`** — always poll by `relicID`:

| Asset file | `relicID` | Display name | Rarity |
|---|---|---|---|
| ExecutionersSeal | `ExecutionersSeal` | Executioner's Seal | Epic |
| FluxRegulator | `FluxRegulator` | First One's Free | Common |
| FoundryRights | `FoundryRights` | Melt It Down | Epic |
| GlassHeart | `GlassHeart` | Glass Heart | Epic |
| **Kinetic** | **`KineticCapacitor`** ⚠️ | Hot Streak | Common |
| LavaBoots | `LavaBoots` | Hot Steppers | Common |
| MeteorGreaves | `MeteorGreaves` | Meteor Greaves | Epic |
| MidasRecoil | `MidasRecoil` | Blood Money | Rare |
| OverclockedRecall | `OverclockedRecall` | Offering | Epic |
| PhoenixCog | `PhoenixCog` | Phoenix Cog | Legendary |
| PocketBattery | `PocketBattery` | Pocket Lightning | Common |
| Pogo Boots | `PogoBoots` | Pogo Boots | Rare |
| ReclaimersClamp | `ReclaimersClamp` | Sticky Fingers | Rare |
| ReinforcedPlating | `ReinforcedPlating` | Bubble Wrap | Common |
| ScrapMagnet | `ScrapMagnet` | Loot Goblin | Common |
| **SpikedCarapac** | **`SpikedCarapace`** ⚠️ | Do Not Pet | Rare |
| VampireTooth | `VampireTooth` | Snack Fangs | Common |
| Whetstone | `Whetstone` | Whetstone | Common |
| GeckoGloves | `GeckoGloves` | Gecko Gloves | Rare |

⚠️ **Two filename/ID traps:** the asset named `Kinetic` has `relicID` **`KineticCapacitor`**, and `SpikedCarapac` (no trailing "e") has `relicID` **`SpikedCarapace`** (with "e"). Using the filename in `HasRelic()` will silently never match.

**How each is wired** (verified): most via `RelicManager.HasRelic("<id>")` — including a damage-modifier path `RelicManager.ModifyPlayerDamage(...)` used by Fireball / Bite / Freefall that reads **Whetstone, MidasRecoil, GlassHeart**. Two are wired differently and will NOT show up if you grep for `HasRelic`: **LavaBoots** via `HazardZone.requiredRelicID` (default `"LavaBoots"`, also set by `AcidBlobProjectile`), and **ScrapMagnet** via the static `ScrapMagnet` class (`ScrapMagnet.Attract`, called from `GoldPickUp` and `Shift Crystal`).

### Relic HUD (RelicHUD.cs)

`Assets/Scripts/RelicHUD.cs`, attached to a `RelicHUD` GameObject under `Canvas/GameplayHUD/`. **It is a fixed TOP-CENTRE loadout bar of `MaxSlots` cells + an "N/5" count** (corrected 2026-07-26; it was previously a middle-left vertical column). The bar **self-positions in code**, so it needs no scene re-anchoring — the legacy left-column container is disabled on `Start()`. Note `iconContainer` in SampleScene points at the HUD's OWN transform, so `BuildBar()` deliberately never disables it when `iconContainer == transform` (that would switch off the object building the bar).

- Subscribes to both `OnRelicAdded` and `OnRelicRemoved`; rebuilds all cells on either.
- Filled cells instantiate `RelicIconPrefab.prefab` and call `RelicIcon.Build(relic)`; empty cells draw a dim, gemless stone socket so full and empty read as one crafted row.
- Each cell carries a transparent `RelicSlotHover` hit-target (RelicIcon's own graphics are non-raycast) which drives the shared `RelicTooltip` and opens `RelicManagePanel` on click. `I` also opens it.

**Relic chip visual language (rebuilt 2026-07-26):** `RelicIcon.cs` disables the prefab's root Image and builds the chip procedurally to match the game's OWN hand-painted HUD chrome (`Assets/Art/panel 1.png`, the top-left stat panel), rather than generic UI: rarity **glow** → mottled-**stone** socket → **icon** art (`relicArt`, + drop shadow) → ornate **gold border** → four corner **gem bosses**. **Rarity is carried by the GEM colour, not by recolouring the frame** (amber Legendary / amethyst Epic / sapphire Rare / ruby Common — ruby matches the HUD panel's own studs), so every relic reads as the same gold-on-stone object as the rest of the HUD. Pop-in is **Update-driven** (EaseOutBack, unscaled) so it survives being built while GameplayHUD is inactive (relics granted from a hidden shop/slot pop in when the HUD reshows); Epic/Legendary get an idle glow pulse.

All the shared sprites live in **`RelicUISprites`** (`GoldBorder()`, `StonePanel()`, `GemSetting()`, `Gem()`, `GemColor(rarity)`, plus `AddGemStuds(...)` which studs a panel's border). Procedural + statically cached, no art files. `GoldBorder` carries a 9-slice border so panels use it too; the medallion draws it as **Simple** (a 9-sliced bevel would stretch). The Manage/Swap/tooltip panels all use this same chrome.

⚠️ **When editing these panels, keep content inset clear of the border AND the gem studs** (~52px on the Manage panel). The ornate border is much thicker than the old flat frame, and the original insets left text visibly crowding it.

---

