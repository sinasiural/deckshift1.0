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
