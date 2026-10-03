# Deckshift — Audio Inventory & Licence Record

Created 2026-08-20 during the audio reorganisation. **Two jobs: say where every sound came
from (which a Steam release makes a legal question), and say which sounds are still missing.**

---

## 1. Where audio lives now

All game audio is under `Assets/Audio/`. It used to be split across three places, including
**24 audio files inside `Assets/LevelEfeVrl/Sprites/`** — a sprites folder.

```
Assets/Audio/
  Music/            the one music track
  SFX/
    Player/         footsteps, jump, dash, death, hurt, adrenaline
    Cards/          fireball, phase, portal, comet dive, platform, glass wail, bite
    Enemies/        melee, ranged, slime, and the boss's set
    Pickups/        gold, shift crystal, chest, purchase
    World/          crusher, lever
    UI/             card play, menu, level start
    _Unused/        kept but referenced by nothing — see §4
  Kenney/           three CC0 packs to audition and swap in — see §2
  Procedural/       baked placeholders — see §3
```

⚠️ **Files were moved with `AssetDatabase.MoveAsset`, which preserves the GUID**, so every
reference in every prefab and scene survived untouched. Verified after the move: all 13 of
`Player.prefab`'s assigned clips still resolve, and the 3-clip footstep array is intact.
**If you ever move audio again, do it inside Unity, never in Explorer** — moving the `.wav`
without its `.meta` orphans every reference in the project.

⚠️ **`Assets/ProcSfxPreview/` is NOT game audio.** Those nine `.wav`s are audition renders
written by the ProcSfx bake tool so procedural clips can be listened to outside play mode.
Nothing references them and nothing should. Left where the tool writes them.

---

## 2. Provenance — ⚠️ UNVERIFIED, NEEDS THE DESIGNER

**I inferred these from the original filenames. They are guesses and must be confirmed before
release.** Anything not confirmed CC0 or bought is a risk.

| Original filename | Inferred source | Licence | Status |
|---|---|---|---|
| `freesound_community-breaker-switch-45684` | Freesound | CC0 or CC-BY — **check the sound page** | ❓ |
| `freesound_community-short-success-…-6346` | Freesound | CC0 or CC-BY — **check the sound page** | ❓ |
| `dragon-studio-simple-whoosh-382724` | Pixabay | Pixabay licence (commercial OK) | ❓ |
| `Dark_fantasy_player__#4-1782900520715` | AI generator (prompt+ID naming) | depends on tier | ❓ |
| `Huge_dark_fantasy_bo_#1-1782940241745` | AI generator | depends on tier | ❓ |
| `video_game_huge_armo_#…` ×4 | AI generator | depends on tier | ❓ |
| `Huge_stone_crusher_t_#2-…` | AI generator | depends on tier | ❓ |
| `A_sharp,_quick_air_s_#4-…` | AI generator | depends on tier | ❓ |
| `Heavy_punch_impact_o_#1-…` | AI generator | depends on tier | ❓ |
| `card-sounds-35956`, `zoom-sound-effect-125029` | Pixabay-style | ❓ | ❓ |
| Everything else (`Fireball`, `dash2`, `Phase`, `ZIPLA AMK DECKSHOFT`, …) | unknown / designer-supplied | ❓ | ❓ |

**The AI-generated ones matter most.** Most services allow commercial use on paid tiers and
not on free ones. Confirm which tier produced these and write the answer here.

### ✅ Kenney packs — CC0, verified (added 2026-09-27)

`Assets/Audio/Kenney/` holds three packs downloaded from kenney.nl, each with its own
`License.txt`: **Impact Sounds** (130), **RPG Audio** (51), **Interface Sounds** (100).
All three are **Creative Commons Zero** — public domain, commercial use fine, no attribution
required. Nothing to confirm before a Steam release. Only clips a slot actually references go
into a build, so the unused ones cost nothing.

---

## 3. ⚠️ THE SHOPPING LIST — what is silent right now

✅ **NOTHING ON THE RUN'S PATH IS SILENT ANY MORE (re-audited 2026-09-24).** Every slot below is
filled with a **procedural placeholder** baked to `Assets/Audio/Procedural/*.wav` by
`Deckshift → Fill Silent Audio Slots`. These are placeholders, so the list is still the
**shopping list**: drop a real clip into the slot and it replaces the placeholder. The tool
only ever writes to empty slots, so it never overwrites a clip you picked.

| Sound (placeholder in use) | Slot | Affects |
|---|---|---|
| Zombie melee swing (`ZombieSwing`) | `MeleeEnemyAI.attackSound` | ~27 enemies |
| Spit / retch (`SpitterSpit`) | `ZombieSpitterAI.spitSound` | 7 enemies |
| Altar pays / refuses (`AltarPay`, `AltarRefuse`) | `ShiftAltar.paySound` / `refuseSound` | every altar |
| Wall shattering (`WallBreak`) | `BreakableWall.breakSound` | every breakable |
| Glass Parry, Freefall Blade | `PlayerController.glassParrySound` / `freefallBladeSound` | two cards |
| **Boss death (`BossDeath`)** | `deathSound` on **MossKnightBoss, NinjaBoss, KagemushaBoss** | all three bosses; **every boss death was silent until 2026-09-24** |
| **Moss Knight stomp / leap (`BossStomp`, `BossLeap`)** | `MossKnightBoss.poundSound` / `leapSound` | the awakening and the leap |
| Drinking from the Well | `RestWell.drinkSound` (runtime fallback `ProcSfx.WellDraw`/`WellSurge`) | every Well |
| **Portal opening** | `Portal.openSound` = the existing **`Portal.mp3`** (a real file, not a placeholder) | Portal card |
| **Walking through a portal** | `Portal.traverseSound` (empty; runtime fallback `ProcSfx.PortalPass`) | Portal card |

### Swapping a placeholder: `Deckshift → Replace Sound Everywhere` (2026-09-27)

Drag the placeholder into **From**, the real clip into **To**, press the button: every prefab slot
playing From now plays To. It only writes into each prefab's own components (never a nested
instance, same rule as the filler), and only touches slots holding exactly From, so a hand-picked
clip is never overwritten. **It is also the undo** — swap back the other way.

**Swapped so far (picked by NAME against the ProcSfx brief, not by ear — listen before shipping):**

| Slot | Was | Now |
|---|---|---|
| `PlayerController.glassParrySound` | `GlassParry.wav` | Kenney `impactGlass_heavy_001` |
| `PlayerController.freefallBladeSound` | `FreefallBlade.wav` | Kenney `knifeSlice` |

**Brace (new card, 2026-09-27) went straight to Kenney, also picked by name:**
`PlayerController.braceStartSound` = `impactPlate_heavy_000` (planting into the stance),
`braceHitSound` = `impactMetal_heavy_000` (a hit on the block), `braceEndSound` =
`impactPlate_light_001` (letting go). `PlayerBrace` falls back to `ProcSfx` (`BossStomp`,
`WallBreak`, `PauseRelease`) if a slot is ever emptied, so it can't go silent.

**Shortlist for the rest** — what in the Kenney packs is worth auditioning against each brief.
The rest were deliberately NOT swapped: their briefs are LAYERED (see §5), and one sample from
these packs covers only one layer, which is not obviously better than the placeholder.

| Placeholder | Brief (ProcSfx) | Try in Kenney | Honest verdict |
|---|---|---|---|
| `ZombieSwing` (~27 enemies, **the most-heard placeholder**) | heavy swing through air + wet grunt | `cloth1`–`4` for the air only | **Not in these packs.** Source a whoosh + a grunt (Sonniss GDC bundle) |
| `SpitterSpit` | wet gather + release | — | Not in these packs |
| `WallBreak` | a crack, then rubble | `impactMining_000`–`004`, `impactPlate_heavy_*` | Crack only; rubble needs a second layer |
| `BossStomp` | armoured foot on stone, room-shaking | `impactPunch_heavy_*`, `impactPlate_heavy_*` | Missing the sub-bass the brief asks for |
| `BossDeath` | blow, armour collapse, toll, dust | `impactBell_heavy_000` (1.5s) for the toll | One layer of four — keep the placeholder until layered |
| `AltarPay` / `AltarRefuse` | harmonic, rising / falling | `confirmation_*` / `error_*` | These are UI blips; the altar is the Shift (magic) family — probably keep |
| `BossLeap`, Well, portal traversal | — | — | Not in these packs |

⚠️ **`BossDeathVFX` falls back to `ProcSfx.BossDeath` in code** when a boss passes it an empty
slot. Every boss death goes through it, so a new boss can never be silent at death, even
before anyone runs the filler.

⚠️ **`Portal.mp3` is on OPENING, not traversal, on purpose.** It takes 0.5s to peak and fades out
by 1.5s. Traversal is instant, so a sound that is still rising after you arrive feels laggy.
Traversal wants something short and punchy.

⚠️ **The filler's boss entries are keyed `Type.field`** (`MossKnightBoss.deathSound`), not the
bare field name, so a future small enemy with an empty `deathSound` doesn't get a boss-sized death.

**Still empty, not on the run's path:** `sinasiBigLevel` (Chest, RangedEnemyAI, CrusherTrap —
this room is not in `roomPrefabs`), and the Moss Knight's roar/cleave/charge/slam/lob/hurt
on the unused `YeniLeveller/PF Knight - Moss` copy only (the real encounter has them).
The portal **placement** (first click) plays no sound.

**Already fixed during this pass:** `SlimeAI.attackSound` was empty on every slime *while
`SlimeAttack.wav` sat in the project unreferenced* — the right file had been downloaded and
never wired. Now assigned on the `SlimeEnemy` source prefab, which fills all 14.

---

## 3b. ⚠️ Combat was silent and the world was at 1/8 volume (fixed 2026-10-03)

Two problems the silent-slot audit above could not see, because neither is an empty slot.

**1. Landing a hit, an ordinary enemy dying and a shield block made no sound at all.** Those
events never HAD a slot, so "count the empty slots" reported them as fine. They are now SoundBank
events played from `EnemyHealth` (every enemy has them, nothing to wire), all Kenney CC0:

| Event | Variants | Measured at the listener |
|---|---|---|
| `Enemy.Hit` | `impactPunch_medium_000`–`004`, pitch 0.92–1.08 | −14.1 dB |
| `Enemy.Death` | `impactSoft_heavy_000`–`004`, pitch 0.82–0.94 (deeper) | −10.8 dB (not on bosses: `BossDeathVFX` owns theirs) |
| `Enemy.Block` | `impactMetal_light_000`–`004` | −13.7 dB |

Two new cards went straight into the bank the same day (Kenney CC0, picked by measurement):
- **Glass Moon**, timed to its effect: `Card.GlassMoon.Rise` (`maximize_005`, a sweep that peaks at
  0.46s, on the moon's 0.42s climb), `Card.GlassMoon.Crack` (`glass_002/005/006`, one tick per crack
  stage), then at the burst `Card.GlassMoon` (`impactGlass_heavy_000/002/003/004`; Glass Parry's slot
  owns `_001`) + `Card.GlassMoon.Body` (`impactPunch_heavy_*`) + `Card.GlassMoon.Tail` (`glass_004`,
  the long ring-out), and four `Card.GlassMoon.Tinkle` (`impactGlass_light_*`, pitched up) as shards
  land. One glass take alone measured about −18 dB, thin for the moment, hence the layers. A locked
  Glass Moon plays `ProcSfx.UIRefuse`.
- **Bloodlust**: `Card.Bloodlust` (`impactSoft_medium_*`, pitched down to 0.6–0.68) played twice
  0.17s apart, a heartbeat.

For scale: the player's jump measures −13.8 dB and the hurt sound −9.2 dB. Picked by measurement
(length, onset, loudness), **not by ear — listen in play.** Tune them in `Resources/SoundBank.asset`.
A hit reads as a punch whatever landed it (Fireball included); a card-specific impact layered on
top is the next step, and **a Fireball hitting a wall is still silent.**

**2. `SfxManager.PlayAtPoint` played everything at 1/8 volume.** It used
`AudioSource.PlayClipAtPoint`, whose default falloff is full volume only within 1 unit of the
listener. The listener is on the camera at z = −10 and the play plane is z = −2, so nothing is ever
closer than 8 units. **Measured −18.1 dB dead centre on screen.** Every enemy attack, archer shot,
spit, gold/scrap/crystal pickup, altar and breakable wall went through it. It now plays on the `Sfx`
voice pool (full volume to 6 units, silent by 34, so on-screen sounds land at 70–93%), with ±5%
pitch variation and the same burst guard as bank events.

Un-muting them restored the levels the prefabs already asked for, with two trims where that level
would have beaten the player's own hurt sound: **`MeleeEnemy` `attackVolume` 0.706 → 0.40** (sword
−24.0 → −11.1 dB) and **`SlimeEnemy` `attackVolume` 1.0 → 0.35** (−13.2 dB). The zombie swing went
from about −42 to −23.7 dB and is already at its maximum (1.0), so it is the quietest enemy attack.
Gold stays at its configured 0.15 (−31.9 dB), deliberately small.

⚠️ **Measure loudness at the listener, not in the clip.** `AudioListener.GetOutputData` in Play mode
is the ground truth; a clip's own level says nothing about falloff, and falloff was the whole bug.

## 4. `_Unused/` — kept, not deleted

Nothing references these. Kept rather than deleted because several are plausible candidates
for the shopping list above, and deleting someone's sourced audio is not mine to do.

`Boss_Armor_4_unused` · `Dark_fantasy_player_4` · `PlayerDeath_alt` (the player uses
`Player/Death.mp3` instead) · `stone_crusher_alt` · `VampiricBite_v1` (superseded by v2) ·
`whoosh` · `zoom` · `unknown_yogurt`

---

## 5. Sourcing notes

- **Sonniss GDC Game Audio Bundle** — free, annual, professionally recorded, royalty-free, no
  attribution. Tens of GB. Best single source for a solo dev and most people don't know it exists.
- **Humble Bundle audio bundles** — a few times a year, large libraries for ~$25.
- **AI generation** — already in use here. Good for one-off specifics. **Record the tier and terms.**
- **Freesound** — free, mixed licences. Every file needs its licence noted in §2.

⚠️ **What actually separates good game SFX from bad is LAYERING and VARIATION, not fidelity.**
One sample rarely works — a gate slam is a low thud plus a wood creak plus an iron rattle plus a
tail. And 3–4 variants with randomised pitch beats one perfect sample every time. The project
already does variation correctly in exactly one place: the three footstep clips.

⚠️ **`ProcSfx.cs` should be read as a SOUND DESIGN BRIEF, not deleted.** Its families are
separated by physics — magic = harmonic bell partials, metal = inharmonic bar modes, stone =
noise + sub, paper = no pitched component at all, Halt = defined by being *choked* rather than
faded. That is a better articulation of an audio identity than most indie games ever write down.
When auditioning a real sample, the question is already written for you.
