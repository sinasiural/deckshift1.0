# Deckshift — Codex Context

This file is loaded automatically into Codex at the start of every session. Read it carefully before suggesting changes. It documents architectural decisions, known pitfalls, and conventions specific to this project.

---

## Project Overview

**Deckshift** is a 2D pixel-art roguelike deckbuilder platformer for PC (Steam target). Built in **Unity 6.0+** with URP (2D renderer) enabled.

**Core concept:** "Movement is a Resource." Jumping consumes **Shift**, which does not regenerate on its own — and **Shift CARRIES OVER between rooms** (designer-confirmed 2026-07-13: it is a run-long resource, and this persistence is "the whole identity of the game" — spending Shift now means having less for the rest of the run). Do NOT describe or implement Shift as a per-room resource. Most other actions (attacks, special movement, utility) are delivered via cards. Cards have **charges**; when charges deplete the card moves to the exhaust pile and must be recovered via scrap.

**Current state:** Act 1 (Oxidation District) prototype. **11 combat levels in the run pool** (+ hub + boss room; ~15 more contract-valid rooms exist unused — see Room Pool), **18 CardData assets in `Assets/Cards/`, 19 relics, and 8 quest assets** (cards re-counted 2026-08-16; note 2 of the 18 are not normal reward cards — `Stagger` is the fail-state card and `AnaKartVeritabanı` is the card *database* asset, so the real playable pool is **16**). **2 playable characters** in `Assets/Resources/Characters/` (see Characters). Two acts (Vapor Stratum, Final Forge) planned but not started. Target: 45-50 minute run length, 60+ cards total at content-complete. **24 Blompo blessings** as of 2026-08-14 — the card *pool* is still the bottleneck, but the enhancement multiplier on it is now built.

⚠️ **`DeckManager.startingDeck` is currently 3 cards (Phase, Freefall Blade, Second Thoughts) and that is the designer using it as a TESTING TOOL, not the intended starting deck.** Do not balance against it, and do not "fix" it. It does have one live side effect worth knowing: the `Only Child` blessing keys off a deck under 10 cards, so it currently fires for the whole run.

📐 **TWO LOADABLE SKILLS HOLD THE DETAIL. This file is the always-on summary; they are the working references.**

- **`/deckshift-ui`** — the **Salvage** material system (see below), linear-colour-space calibration, uGUI traps, the wiring contract, a pre-delivery checklist, and a catalogue of every screen that exists. Invoke before building, restyling or debugging **any** screen, panel, HUD element, card face, world-space marker or UI VFX.
- **`/deckshift-levels`** — the Level Design Laws with their reasoning, the ASCII importer and its tile-painting rules, the validator's measured movement budget, doors and gates, the room pool inventory, and the run map. Invoke before authoring, importing, validating or debugging a room, or before touching `LevelManager`, tiles, gates or the exit door.

⚠️ **These were split OUT of this file on 2026-08-20 because it had grown to ~75k tokens and was being loaded in full for every session, including ones that never touched a screen or a room.** Between them the two sections were 41% of the file. **When you learn something new about UI or levels, write it into the SKILL, not back into here** — otherwise this grows again and the split buys nothing. Only add here what must be true even when you are working on something else entirely.

⚠️ **PRIORITY, RESET BY THE DESIGNER 2026-08-20: POLISH AND FEEL COME BEFORE MORE CONTENT.** This file said for months that "content is the project's real bottleneck" — that was written when the run map and Blompo were unbuilt and explicitly gated on room and card count. **Both shipped.** The claim outlived its reason and kept steering sessions toward authoring.

**The framing that replaced it: polish debt blocks JUDGING the game; content blocks FINISHING it.** You cannot tell whether the game is fun through placeholder audio, enemies that swing backwards and a bat that never attacks — all three were real and all three were found in a single day. Feel, visuals and audio come first, because everything else is unevaluable until they do.

⚠️ **Audio is the designer's stated top priority, and the real problem is not what it looked like.** It is not that the procedural clips are weak (they are, and `ProcSfx` should be read as a **sound-design brief** rather than a source — see `AudioInventory.md`). It is that **a large fraction of the game is literally SILENT**: an audit found **103 empty `AudioClip` slots**, including every zombie's swing, every spitter, every Shift Altar, every breakable wall, and the boss's death. Some were silent *while the correct file already sat in the project unreferenced*. **Read `AudioInventory.md` before touching audio** — it holds the layout, the licence record, and the eleven-sound shopping list.

**The content gap is still real, just no longer first.** 11 combat rooms and 16 playable cards remain thin for a 45–50 minute run, the two named archetypes are the thinnest lines in the deck (Glass has 2 cards, Vampiric 1), and Acts 2–3 do not exist. Do not read "polish first" as "content is solved".

The player character was recently swapped from the skeleton rig (`PF Skeleton - Mage`) to `PF Pixel Character - Mage M` from the Cainos Customizable Pixel Character pack. The wizard identity is now the canonical character. The skeleton remains in the Player prefab disabled, intended for future use as an enemy. **Renderer facts (verified in-editor 2026-07-17): the Mage M body is 16 `SkinnedMeshRenderer` parts (Body, Hair, Hat, Cloth… — Cainos "Alpha Cut"/Body/Hair shaders); only the magic staff is a `SpriteRenderer`.** Any code that snapshots/copies the player's look must handle SkinnedMeshRenderers (e.g. `SkinnedMeshRenderer.BakeMesh`, as `CardAimIndicator`'s dash trail does) — a SpriteRenderer-only pass silently produces a staff-only ghost.

**Active scene:** `Assets/Scenes/SampleScene.unity` (build index 2). Other scene files exist (`GameScene`, `MasterLevel`, `Hub`) but are inactive/legacy. When debugging "is this in the scene?" issues, always check SampleScene first.

---

## Tone & Voice (designer-stated 2026-07-15 — applies to ALL player-facing text)

**Deckshift does not take itself too seriously.** Player-facing names and flavor — relics, cards, items, enemies, quests, UI — should have **personality and a wink**, not dry functional labels. The goal is that players get *attached* to specific things partly because the name is fun ("I love running Loot Goblin"). The world-building is currently thin, so this is where character comes from.

**The line to walk:** playful, NOT a complete joke. Mix registers so it feels like a real world with a sense of humor, not a parody:
- **Cool-with-personality** (the default): evocative names with a slight grin — "Pocket Lightning", "Blood Money", "Pay in Blood", "Glass Heart". These carry the world.
- **Straight-up fun** (sprinkle, don't flood): the occasional pure wink — "Bubble Wrap", "Do Not Pet", "Loot Goblin". These are the ones players quote.
- **Keep the genuinely cool ones cool:** if a name already lands (Phoenix Cog, Executioner's Seal, Meteor Greaves, Glass Heart), leave it — don't jokify everything, or nothing stands out.

Names should still *hint at what the thing does* where possible (First One's Free = first card free; Do Not Pet = touch it and get hurt). Keep mechanical **descriptions** clear and literal — the humor lives in the NAME and any short flavor line, never at the cost of the player understanding the effect. **`relicID` / enum / code identifiers NEVER change for flavor** — only the display `relicName` / `cardName` / description text.

---

## User Profile and Workflow

The user is **designer-first, not a developer**. Strong design intuition, limited coding background. They cannot evaluate code quality directly. All implementation flows through Codex; conversational Codex reviews plans before they're sent to Codex.

**Never ask the user to read or edit code directly.** Never instruct them to "tweak this value in the script" or "change line X yourself." Either:
- Give them an Inspector step in Unity (drag this, click that, change this number),
- Or give them a prompt to send to Codex that does the change.

**Code-level explanations should be in plain language with concrete examples.** When proposing a refactor or technical change, explain the consequence in plain English BEFORE the technical detail.

The user works with a **separate conversational Codex instance** (Codex.ai) for design discussion and prompt drafting, then sends prompts to Codex for execution. When the user references "what Codex said" or "the other Codex," that's the source. The user is fluent enough to course-correct, but defer to user intent when their explanation differs from a previous prompt.

---

## Critical Architecture Rules

These are absolute. Do not suggest alternatives without explicit user approval.

1. **No Cinemachine currently in use.** It was removed early due to confiner issues with multi-shape rooms. The custom system is in `CameraFollow.cs` plus per-level `LevelBounds` zones. The policy is "currently removed, can be revisited if a clean approach is found" — not absolute prohibition. `CameraPeek.cs` has since been rebuilt without Cinemachine and works (see Camera System); the Cinemachine package itself is still installed and two dead `using Unity.Cinemachine;` directives remain (`PlayerController.cs`, `LevelManager.cs`) — cleanup pending.

2. **Manager-singleton pattern.** All major systems are singleton MonoBehaviour managers (GameManager, DeckManager, LevelManager, etc.). This pattern has known issues (cyclic dependencies, flat global state) but is the architecture. Do not propose dependency injection, ECS, or other paradigms.

3. **Game runs in a single scene currently.** Most managers do not have `DontDestroyOnLoad`. **QuestSystem's `DontDestroyOnLoad` was REMOVED (2026-06-10): quests are per-run by design and reset on death/restart; each scene uses its own QuestSystem instance, whose serialized UI references match that scene.** If quest meta-progression is ever wanted, persist it through the save system (PlayerPrefs, like AchievementManager) — do not re-add `DontDestroyOnLoad`. If scene transitions are added later, this must be revisited. Do not add `DontDestroyOnLoad` to existing managers without discussing the implications.

4. **Comment language convention.** Older code has Turkish comments (Gemini-era). Going forward, **new comments should be in English** for clarity. Do not retranslate existing Turkish comments unless they're factually misleading.

5. **No assets the user can't afford.** Solo developer with limited budget, no freelance artist. Work with existing Cainos asset packs and pixel-art conventions. Don't propose solutions that require commissioning new art.

6. **Asset pack imports require extreme care.** The Cainos Customizable Pixel Character pack ships as a "complete project" that wants to overwrite `ProjectSettings/`. Always uncheck `ProjectSettings/` in the import dialog and uncheck any duplicate packs you already have. See "Common Pitfalls" for the full story.

---

## Characters (built 2026-08-14)

**A character is TWO things and no more: the deck you start the run with, and one passive trait.**
`CharacterData` assets live in **`Assets/Resources/Characters/`** and are found by `Resources.LoadAll`,
not a hand-kept list — adding a character is dropping in an asset, with nothing to rebuild.

| | deck | trait |
|---|---|---|
| **Wizard** | Fireball ×2, Create Platform, Dash | *Big Sleeves* — +1 hand |
| **Ninja** | Shuriken ×2, Dash, Leap | *Fast Hands* — Recall never escalates, **−1 hand** |

⚠️ **A character's innate ACTIVE ability was built and then CUT. Do not re-propose it.** The first
version gave the Wizard a free, unlimited, aimed attack on right mouse. It worked, and the designer
rejected it after one playtest. The reasoning generalises, which is why it is recorded here:

- A free attack doesn't out-damage your attack cards — it **prevents the situations those cards exist
  for**. Comet Dive is for being surrounded, Glass Wail for being overwhelmed. Delete everything at
  range for nothing and none of those moments happen.
- Underneath that: **the game charges nothing for TIME.** No timer, no reinforcements, Shift does not
  decay. So an infinite-but-slow attack always beats a finite-but-fast one, and every attack card in
  the game is fundamentally selling speed.
- It also made breakable walls free to open, and voided the design law that a boss room must provide
  a way to damage the boss.

Measured at the time: of the playable cards, 7 are attacks but only **Fireball** and **Dead Weight**
were *directly* outclassed. **The felt problem was far broader than the measured one, and that gap is
the whole lesson** — don't re-litigate this with a damage table.

**Traits are read, never mirrored into a field**, so a character swap can't leave a stale copy:
- **`DeckManager.HandCapacity`** — the ONE place hand size is decided. Never read `handCapacity`
  directly. (Base is **3** in the scene, not the script default of 4.) `handCapacityBonus` may be
  **negative** — traits are allowed real downsides, and the designer wants that.
- **`DeckManager.RecallCostIsLocked`** — read at the escalation site.
- **`PlayerController.Awake`** — `CharacterSelection.Chosen` (static + PlayerPrefs) overrides the
  prefab's `character`.

### `CharacterAppearance` — it re-dresses the rig, it does not swap the model

⚠️ **It copies the preset's MATERIALS onto the existing rig.** The player's visual model carries a lot
of fragile hand setup (Cainos controller scripts stripped, `AnimationEventReceiver` removed,
`PlayerAnimEventSink` on the Animator child, 0.8 scale) and every one of those steps fails silently
when missed. Every preset is the same rig in different clothes.

⚠️ **Weapons go through the pack's `PixelCharacter.AddWeapon(prefab, true)`.** `PixelCharacter.Weapon`
is read-only, and the pack syncs the weapon to the rig bone and pushes sorting layer + alpha onto the
new renderers. Hand-parenting looks right standing still and then sorts or fades wrong.

⚠️ **Reading an unassigned object reference on a Cainos preset THROWS** `UnassignedReferenceException`
instead of returning null, so every slot read goes through a `Safe()` guard. `HairRampTexture` is
deliberately not copied.

⚠️ **`Apply` catches everything, and it must.** Unity **DISABLES a MonoBehaviour whose `Awake`
throws** — when this threw inside `PlayerController.Awake` it switched off the entire player: no
movement, no jump, no card hotkeys, while Recall and clicking cards still worked (those are
DeckManager and UI). **A cosmetic pass must never be able to brick the character.**

### ⚠️ Starting a run: the load is 1.04s and must be OVERLAPPED, not queued (2026-08-17)

**Measured, not estimated: `SceneManager.LoadScene` into SampleScene takes 1.04 seconds**, and the
synchronous call spends every one of them frozen on the last rendered frame — no animation, no
feedback, indistinguishable from a hang. On top of that the character select used to play its exit
animation and *then* hand over, so a player clicking BEGIN waited **1.46s**: 0.42s of animation
followed by a full second of dead image. The designer reported it as "it takes too long for the next
screen to load".

Two changes, and the order matters more than either one:
- **`MainMenuController.StartRun` uses `LoadSceneAsync`.** The load costs the same second; it just
  stops freezing the frame while it happens.
- **`CharacterSelectScreen.ConfirmRoutine` fires its callback FIRST, not last.** The exit burst now
  plays *through* the load instead of before it. **This is where the saving comes from** — the load
  did not get faster, it stopped being dead time.

Now **1.085s, all of it animated.** The residual is the scene load itself; making that shorter means
restructuring SampleScene, which is out of bounds.

⚠️ **The screen washes to near-white and HOLDS.** The swap lands whenever the load happens to
finish, which is not a moment anything can predict — cutting out of a detailed frame reads as a
glitch, cutting out of a white one is invisible.

⚠️ **The hold loop must still be able to END.** The scene swap normally destroys the screen mid-hold,
so the loop usually never exits — but `ScreenGallery` opens the screen with a **null callback**, and
a failed or absent load must not strand the player on a white rectangle. There is a give-up timeout.

⚠️ **`SceneManager.GetActiveScene().buildIndex + 1` resolves to SampleScene ONLY because `Hub` is
DISABLED in Build Settings** and disabled scenes are not counted. Verified: [0] MainMenu,
[1] SampleScene, [2] GameOverScene, [3] GameScene. **Enabling Hub would silently send PLAY to the
wrong scene** — it looks like a build-settings tick, and it re-routes the game.

### Character select — theme **Marquee** (rebuilt 2026-08-17; see UI System → Themes)

Opens on PLAY from the main menu; the designer chose "always ask" over "remember last pick". **The
billing before you go on:** one character owns the frame at full size, the rest of the roster stands
back in the dark, the name is printed across the top at poster size, and the whole screen tears past
in that character's colour.

⚠️ **THIS REPLACED *VIGIL*, WHICH THE DESIGNER REJECTED TWICE. DO NOT REBUILD IT.** Vigil was a cold
hall of stone alcoves: the roster stood dormant as statues and one warm lamp travelled the row to
whoever you were on. It was carefully made — real dungeon art, a torch per alcove, a diegetic flame
— and it was wrong, for a reason that generalises: **its entire vocabulary was DORMANCY.** Stillness,
cold, low light, a row of equals waiting their turn. That is a fine mood and it is the opposite of
what this screen is FOR. The character select is the last beat before the run starts — the one screen
in the game that is pure hype rather than decision support. A screen about to launch you should not
feel like a mausoleum. **When a screen is rejected twice, question what it is SAYING before dressing
it better; the second Vigil pass improved the art and changed nothing about the problem.**

Three inversions of what Vigil did, and all three are the design:

1. **ONE HERO, NOT A ROW OF EQUALS.** Vigil gave every character the same small alcove, which with a
   roster of two is two little figures in a lot of empty dark. The chosen one now steps forward at
   full size; the others recede, shrink and go cold. Readable from silhouettes alone.
2. **VELOCITY, NOT STILLNESS.** Nothing rests: streaks tear across the backdrop, figures spring
   between slots and overshoot, the name slams in. The game's stated thesis is "movement is a
   resource" — the select screen may as well say so.
3. **FEWER WORDS** (designer-requested). Vigil printed a title ("WHO'S UP?"), the name on every
   alcove AND again below, a trait name, a trait line and a six-word key line. What is left is the
   **name, the trait, the trait's one sentence, BEGIN and ESC** — the name appeared twice and the
   title said nothing the screen did not already say.

⚠️ **THE THEME CLAIMS NO HUE — IT TAKES THE CHARACTER'S.** Every other screen has ONE fixed accent
identifying a PLACE. This screen is about an IDENTITY, so the accent belongs to the character and the
whole frame cross-fades when the selection moves: **colour here is the selection signal, not the
theme signature.** It therefore costs nothing from the nearly-spent hue budget, which is the standing
instruction when hues run out (invert a different axis instead of picking a new colour).

⚠️ **Accents are PALETTE-BY-INDEX, not a field on `CharacterData`.** A new character must never be
able to arrive with an unset colour and render black — dropping an asset into `Resources/Characters`
is still the whole job of adding one. Ordered for maximum separation between *neighbours* (jade →
magenta → gold → ice), because with a roster of two the player only ever compares slot 0 against 1.

**Still true from the old screen, do not re-learn:**
- **Live rigs render to one RenderTexture each** (`CharacterStagePortrait`, stage at world
  (3000, −3000) on its own `CharacterStage` layer). The menu Canvas is Screen Space Overlay, so a
  world character would sit *behind* the backdrop; and one texture each makes dimming a plain
  `RawImage.color` tint instead of reaching into Cainos shaders.
- ⚠️ **A `RawImage` with no texture draws a solid WHITE quad** — tinted, that was a large coloured
  slab where a character should have been. A half-authored character must leave an empty slot.
- ⚠️ **A key that activated the button which OPENED this screen is still down on its first `Update`.**
  It confirmed instantly from the Enter that pressed PLAY; guarded with `openedFrame`. Same family as
  `PauseScreen`'s Escape memory, from the other direction.
- ⚠️ **`RefreshInfo` is called every frame and self-guards on `lastShown`** — the panel is DERIVED
  from `index`, never pushed at it. It had already rotted once when the click handler set `index`
  without refreshing. Requiring every mutation site to remember is the losing pattern.
- It uses `GameScreen.FindRootCanvas()`, never `FindFirstObjectByType<Canvas>()` (which in a gameplay
  scene returns a world-space **enemy health bar** — 18 canvases in SampleScene), and all text routes
  through `UIType` (skipping it is how the screen once shipped in Liberation Sans).

**New traps paid for in the rebuild:**

⚠️ **AN ADOPTED SCREEN MAY BE A HUSK.** `Open` now re-finds an existing screen when the static
`instance` is null (a domain reload clears statics while scene objects survive — otherwise you stack
a second screen, a second roster and a live camera per character; measured **three screens and six
portrait plots**). But a domain reload also **resets every non-serialized field**, so the component
comes back with `figures` and `roster` EMPTY while `content` and the built children survive. The
symptom is a beautifully half-updated frame: the previous character still standing there under the
previous name, while the accent — the one value computed from `index` alone — cross-fades to the new
pick. `Open` therefore checks `IsBuilt` and rebuilds rather than adopting a husk.

⚠️ **THE CHARACTER PLOTS ARE SCENE-ROOT OBJECTS, SO HIDING THE SCREEN DOES NOT STOP THEM.** They have
to be — world space, 3000 units out, while the screen is a RectTransform. Without
`CharacterStagePortrait.SetStageActive`, one camera per character keeps rendering a 420×614 target
every frame for the rest of the session, through the entire run. `Build` also sweeps any orphaned
plot before creating its own.

⚠️ **The bars were calibrated DOWN by two-thirds.** At alpha 0.055/0.035 the two raked accent bars
stopped being livery and became enormous slabs owning the composition — they read as the subject
rather than as light behind it, and flattened the character they crossed. 0.020/0.012. Linear colour
space again: measure by screenshot, never by arithmetic.

⚠️ **The unlit floor is 0.42, not 0.30.** Below ~0.4 a flanking character stops reading as "someone
standing in the dark" and starts reading as a rendering artefact — and showing you the roster is this
screen's job, so the ones you did *not* pick still have to be recognisable.

⚠️ **The contact mark under each figure is LIGHT, not a shadow.** A black ellipse on a near-black
backdrop is invisible by definition. Inverting it grounds the figure AND carries the accent.

⚠️ **Arrow hints are DRAWN AS SPRITES, never typed as "← →".** CCBattleScarred is a display face with
no guarantee of carrying arrow glyphs, and a missing glyph renders as a blank or a box.

⚠️ **Springs, not lerps — and they were measured, not eyeballed.** Simulated at 144/60/30 fps: peak
overshoot 52–69px, settles ~0.65s, converges exactly, and the scale spring never goes negative
(min 0.51, max 1.08). `dt` is clamped to 1/30 because an explicit integrator plus one long frame (a
domain reload, an editor stall) throws a figure off the screen.

**Nothing is fit-scaled**, deliberately: every CanvasScaler matches on HEIGHT, so the canvas is always
1080 tall and only width flexes (1440 at 4:3 → 2560 at 21:9). The widest element is ~1150px, so the
fixed layout is safe everywhere and the Point-filtered portraits stay at exactly 1:1. Verified by
screenshot at 4:3, 16:9 and 21:9.

⚠️ **`Assets/Resources/VigilArt.asset` survives but ONLY `wallTexture` is still consumed.** The name
is historical — the alcove/plinth/torch/flame/banner/beam/floor/grime slots are Vigil's and are now
unused. They are kept because the class name and the asset path are loaded by name at runtime
(renaming is a live risk to a lookup for no visible benefit) and the references are already resolved.
**Do not read that file as a description of what the screen looks like.**

⚠️ **The wall is ONE seamless 8×8 picture, so the WHOLE 256×256 texture is tiled as a single sprite** —
tiling any one of its 64 sub-sprites repeats a fragment of a larger image and reads as a
checkerboard. This is the same mistake that made generated rooms never look hand-made. Tint measured
on screen: at 0.15 the masonry was invisible and the backdrop was a flat void, throwing away the one
piece of real game art on the screen; near full value it is a bright grey field. **0.25.**

**Two lessons from the Vigil art pass that still hold, and cost real time:**
- ⚠️ **PIXEL ART ENLARGED PAST ~2× STOPS READING AS THE THING IT DEPICTS.** A 38×27 grime sprite blown
  to 420×420 (15×) became angular shapes that looked like broken masonry floating in mid-air.
- ⚠️ **`Image.Type.Tiled` on an ATLASED sprite repeats the wrong part.** `Pillar 01 A` tiled up a
  shaft repeated the pillar's *capital*, so both alcoves grew brackets at chest height; stretched, it
  gave a 3× vertical smear. A real column needs a purpose-drawn shaft sprite.

## Player System

### PlayerController.cs

This is a large script (~1,200 lines). It currently handles movement, jumping, card action execution, gravity reversal, VFX spawning, audio, gold, shift, portal state, cannon enter/exit, and respawn. **Health/damage/knockback/parry were extracted into `PlayerHealth.cs`** (same GameObject); `PlayerController.TakeDamage` is a one-line delegate to it, and RelicManager recomputes HP passives from `PlayerHealth.BaseMaxHealth`.

**Refactor status: the `CardActionExecutor` extraction is DONE.** `ExecuteAction()` is now a one-line delegate to `CardActionExecutor.TryExecute()`. All card actions live in `Assets/Scripts/CardActions/Actions/` as `CardAction` subclasses, registered in a dictionary in `CardActionExecutor.Awake()`. There is no switch statement anymore — do not look for one. The conflict-flag half of the system is only partially built; see "Card Effect Conflict Class of Bug" below for the audited current state.

### Player Prefab Specifics

- **Active visual model:** `PF Pixel Character - Mage M` at `Assets/Cainos/Customizable Pixel Character/Prefab/Character Preset/PF Pixel Character - Mage M.prefab`. This is a child of the Player root and is assigned to `PlayerController.visualModel`.
- **Disabled fallback:** `PF Skeleton - Mage` is still parented under Player but disabled (checkbox off). Kept as backup and for future reuse as an enemy.
- **Physics collider:** `CapsuleCollider2D` on the Player root with **Offset (-0.0053, 0.8423) and Size (0.5075, 1.6848)**. Direction: Vertical. A `BoxCollider2D` was previously present but disabled and has been removed. Do not re-add it. **This capsule is the player's only ACTIVE solid collider (2026-07-16):** the Cainos rig's leftover bone colliders (capsules on `Rig Spine1`/`Rig Spine2`, circle on `Rig Head`) and the magic staff's `Rigidbody2D` + trigger `PolygonCollider2D` were removed from the prefab — they made the hitbox animation-dependent and cost physics rebakes every frame. Do not re-add them. **The capsule is now genuinely the only solid collider in the prefab, active or not (2026-08-11):** the `PF Skeleton - Mage` child's leftover solid `BoxCollider2D` — inert only because that GameObject was disabled, and a live landmine the moment anyone re-enabled the skeleton as an enemy — has been deleted. The root Rigidbody2D is confirmed the only Rigidbody2D in the prefab.
- **Rigidbody2D:** Dynamic. Gravity scale flips sign during gravity reversal — do NOT modify `Physics2D.gravity` globally.
- **Player root Transform:** Position (0, 0, 0), Rotation (0, 0, 0), **Scale (1, 1, 1)**. This is now a hard rule again — the prior non-(1,1,1) scale was an accidental drift that compounded into a real bug. Do not modify the root scale to adjust character size; scale `visualModel` instead.

### Visual Model Internals (PF Pixel Character - Mage M)

- The visualModel itself is scaled to **(0.8, 0.8, 0.8)** to fit the collider. If the character ever needs to appear larger or smaller, change this value, not the root.
- The prefab has its own root-level scripts (`PixelCharacter`, `PixelCharacterController`, `PixelCharacterInputMouseAndKeyboard`, plus its own Rigidbody2D and BoxCollider2D). When the visualModel was integrated, the controller scripts and physics components were removed; only the `PixelCharacter` (customization) script remains. Do not re-add the removed components.
- The Animator component lives on the child GameObject named `Animator`, found via `GetComponentInChildren<Animator>()`. There is only one Animator in the hierarchy.
- The Animator Controller is `Assets/Cainos/Customizable Pixel Character/Animation/AC Character.controller`.
- **`Cainos.CustomizablePixelCharacter.AnimationEventReceiver` has been REMOVED from the Mage M Animator GameObject entirely** (it used to be merely disabled). It throws NullReferenceExceptions on the built-in footstep animation events (the pack expects a footstep audio system we don't use). Do not re-add it; if a pack reimport resurrects it, remove it again.
- **`PlayerAnimEventSink` component must stay on that same Animator GameObject.** With no Cainos receiver, the pack's ~20 animation events (`OnFootstep`, `OnAttackCast`, etc.) would have no receiver, spamming `"'OnFootstep' has no receiver!"` every step. `Assets/Scripts/PlayerAnimEventSink.cs` is a sink with a method for every event name (including the pack's `OnLedgeClimbFinised` typo) so the events land harmlessly. Do NOT delete it or the spam returns. Its `OnFootstep(AnimationEvent)` relays to `PlayerController.PlayFootstep()` (footstep SFX). The sink MUST stay on this Animator child (that's where Unity delivers the events); the footstep *fields* (`footstepClips[]` — the three `Walk` mp3s from `Assets/LevelEfeVrl/Sprites/`, `footstepVolume`, `footstepPitchRange`) live on `PlayerController` (the player root) per the designer's request. Other event methods remain empty; hook new anim-driven SFX here. **History (2026-07-16): the sink + clip wiring were once scene-only, never committed, and silently lost — they are now serialized in `Player.prefab` itself. Keep it that way: player changes must be applied to the PREFAB, not left as scene overrides.**

### Animator Parameter Map

PlayerController writes to these parameters on the Animator:

| Parameter        | Type    | Driven by                                                | Purpose                                            |
|------------------|---------|----------------------------------------------------------|----------------------------------------------------|
| `MoveBlendX`     | Float   | `UpdateAnimations()` — 0 idle / `locomotionPose` moving  | Locomotion pose blend: idle(0)/walk(1)/run(3)      |
| `MoveSpeedMul`   | Float   | `UpdateAnimations()` — `speed * animCadenceScale` clamped | Scales walk-cycle PLAYBACK to real ground speed (kills foot-slide) |
| `VelocityY`      | Float   | `UpdateAnimations()` — `rb.linearVelocity.y`             | Jump/fall vertical state                           |
| `IsGrounded`     | Bool    | `UpdateAnimations()`                                     | Land/airborne distinction                          |
| `InjuredFront`   | Trigger | `TakeDamage()` on every damage hit                       | Hurt reaction                                      |
| `IsDead`         | Bool    | `Die()` set to true                                      | Death state                                        |
| `AttackAction`   | **Int** | `FireballCastRoutine` sets to **14**                     | Dispatch value selecting which attack animation    |
| `IsAttacking`    | Bool    | `FireballCastRoutine` toggles                            | Gate for AttackAction transitions                  |

**Critical:** `AttackAction` is an **Int**, not a Float. Calling `SetFloat("AttackAction", ...)` throws a runtime type mismatch. Use `SetInteger("AttackAction", value)`.

### Cast Animation (Fireball Card)

The Cainos Animator Controller has a "Cast" animation at `AttackAction == 14`, playing on both the "Attack Action - Arm" and "Attack Action - Body" layers simultaneously. The clip is 1.0 seconds long and self-exits at ~80% via unconditional ExitTime.

`PlayerController.FireballCastRoutine` (~line 800-826):
1. Sets `IsAttacking = true` and `AttackAction = 14`.
2. Waits **0.36 seconds** — this is the `OnAttackCast` animation event timestamp authored by Cainos themselves, the designer's intended projectile release frame.
3. Calls `PerformFireball(value)` to spawn the projectile.
4. Waits an additional 0.15 seconds, then sets `IsAttacking = false`.

The animation will self-exit even if the bool isn't released, but releasing it explicitly prevents an Empty→Cast re-trigger loop.

**Cainos's own attack system (idle, unused):** The pack prefab has a `CharacterBehaviour` script on its root with an `attackAction` field (set to 14 in Mage M preset) and UnityEvent callbacks `onAttackCast`, `onAttackStart`, `onAttackEnd`. These exist but are not currently wired. If perfect frame-accurate cast spawn timing becomes a priority, hooking into `onAttackCast` via an Animation Event is the right path — but not today.

### Ground / Wall / Ceiling Detection

The player has check Transforms parented to the player root (NOT to visualModel). Post-refactor honest values:

- **`groundCheck`** at local (0, 0.015, 0). Used for normal grounded detection via `Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)`.
- **`wallCheck`** at local (0, 0.8423, 0) — mid-body, on the capsule's centre. `wallCheckDistance` 0.32. See the warning below for why it is NOT down at the feet.
- **`ceilingCheck`** at local (0, 1.725, 0) — added during gravity reversal work. Used when `isGravityReversed` is true.
- **`firepoint`** at local Position (0.499, 1.263, 0), Scale (1, 1, 1). Fireball/bite origin point.

`IsGroundedCheck()` switches probe based on `isGravityReversed`. The original implementation used a mirror-math formula (`2 * pivot - groundCheck.position`) but that was fragile (only 0.16 units of overlap margin); the dedicated `ceilingCheck` Transform replaced it.

⚠️ **`wallCheck` MOVED TO MID-BODY (0, 0.8423) and `wallCheckDistance` cut 0.5 → 0.32 (2026-08-11).** It used to sit at local y **−0.0098**, i.e. just *below* the capsule's bottom — and `Physics2D.queriesStartInColliders` is ON by default, so the ray started INSIDE the floor tile the player was standing on and returned a hit at distance 0.000. Measured: `WallCheck()` returned **true while standing on open, flat ground**. The old 0.5 length also reached a quarter of a unit past the body; 0.32 is the body half-width (0.254) plus a small margin.

⚠️ **The wall check uses a NEW `terrainLayer` (Ground only), NOT `groundLayer`.** `groundLayer` deliberately contains Enemy so the player can land on heads (Pogo Boots), but that made every Enemy-layer enemy count as a *wall* — you could wall-slide off some enemies and not others purely by which layer that prefab happened to be authored on.

⚠️ **The disabled `PF Skeleton - Mage` child's leftover `BoxCollider2D` has been DELETED (2026-08-11).** It was solid, enabled, and inert only because the GameObject was off — re-enabling that skeleton as an enemy would have silently given the player a second solid collider. The caveat that used to live here is resolved; don't re-add it.

`groundLayer` mask is **`2056` = layer 3 (`Ground`) + layer 11 (`Enemy`)** — verified against Player.prefab 2026-07-18. (An earlier version of this file claimed `2057` including layer 0 `Default`; that was WRONG — Default is NOT in the mask.) Consequences worth knowing: level geometry is on layer 3, and because layer 11 `Enemy` IS in the mask, the player can **stand on / ground-check against every Enemy-layer enemy**. See "Layer Convention Mismatch" under Enemy System for the verified per-enemy layer split — it is inconsistent and decides which enemies are walkable. This is a known issue but currently load-bearing.

### Jump Forgiveness — coyote time + jump buffering (added 2026-08-14)

Neither existed before this. `coyoteTime` 0.10s and `jumpBufferTime` 0.12s, both serialized on the Player.

⚠️ **THEY MATTER MORE HERE THAN IN AN ORDINARY PLATFORMER.** Elsewhere a jump that fails on a timing gap costs a retry. Here it costs **Shift** — a run-long resource — twice: once if the input fired at all, and again for the re-attempt. It is the only fix to the Shift economy that *removes waste* rather than adding income.

- **Coyote time** is refreshed while grounded and bleeds away once you step off. **Consumed on use** (`coyoteTimer = 0` on a successful jump) — without that, the leftover window hands out a second free jump immediately after the first.
- **Jump buffering** remembers the press and retries it each frame. `PerformJump`, `PerformWallJump` and `HandleJumpInput` all **return bool**, and the buffer clears ONLY on a jump that really happened — so a press that can't be paid for at 0 Shift expires instead of firing later.

⚠️ **Testing this across MCP tool calls is misleading.** The editor stalls for seconds between calls, and Unity caps the resulting first-frame `Time.deltaTime` at `Time.maximumDeltaTime` (0.333) — enough to drain a 0.12s buffer before it is ever checked. A buffer test that "fails" this way is a test artifact. Verify with a deliberately long buffer and check it fires **exactly once** (Shift 40 → 39, not 40 → 0).

### Gravity Reversal System

Triggered by the "Floor is Lava" card (`CardActionType.ReverseGravity`). Lasts 5 seconds with a 0.5s warning flash + audio cue before expiration.

Key fields on PlayerController:
- `isGravityReversed` — runtime flag
- `originalGravityScale` — cached at effect start, restored at end
- `gravityReversalCoroutine` — reference for stop-and-restart on re-play
- **`visualFlipYOffset`** — serialized field, current value **1.6875** (tuned in Inspector after the scale refactor). Translates visualModel up so the 180° rotation pivots around the collider center instead of the feet.
- `originalVisualLocalPos`, `originalVisualScaleX` — cached for restoration
- `warningSoundClip` — AudioClip Inspector field, played at t=4.5s (assigned 2026-07-16: the breaker-switch clip in `Assets/Audio/SFX/` — designer may swap; it had been null in the scene, making the warning fully silent). ⚠️ **This regressed once and was re-fixed 2026-07-22:** the SampleScene Player instance had re-acquired a `prefabOverride` setting this field back to NULL, silencing the warning again even though the prefab was correct. Reverted via `PrefabUtility.RevertPropertyOverride`. **If a player field mysteriously "stops working," check for a scene-instance override before touching the prefab or the code** — the prefab being right does not mean the scene is using it.

`GravityReversalRoutine()` handles the full timeline. `LerpVisualTransform` uses a tracked Z-angle float (never reads back from `localEulerAngles`, which Unity normalizes unpredictably).

The 0.5s **warning flash** is `WarningFlashRoutine` (3 rapid on/off cycles ≈ 0.5s) — **FIXED and screenshot-verified 2026-07-26.** History: it originally tinted `SkinnedMeshRenderer._Color`, which silently no-ops because the Cainos **"Alpha Cut"** shader (most outfit parts) exposes no color property at all (only `_MainTex` + `_Alpha`); a later "fix" switched it to `GetComponentsInChildren<SpriteRenderer>()`, but the Mage M rig is **16 SkinnedMeshRenderers (body/outfit) + 1 SpriteRenderer (the staff ONLY)**, so that flashed just the staff and the body never reacted. Current implementation strobes **`_Alpha`** (the one handle EVERY Cainos rig shader shares — the same one Phase uses) across all SkinnedMeshRenderers via a `MaterialPropertyBlock`, blinking the WHOLE character (a blink reads as "effect expiring"), and additionally red-tints the staff (which does support color). `_Alpha == 1` is the confirmed "normal" value. **Audio cue also plays** (clip re-assigned 2026-07-16). If you ever swap the character rig, re-check that its shaders still expose `_Alpha`.

### Facing System

**Critical:** `transform.localScale` of the player root is ALWAYS `(1, 1, 1)`. Never modify it directly for facing.

Use the `isFacingRight` private bool instead. The `ApplyVisualFacing()` method writes to `visualModel.localScale.x` with this formula:

```
sign = (isFacingRight ? 1 : -1) * (isGravityReversed ? -1 : 1)
visualModel.localScale.x = originalVisualScaleX * sign
```

The gravity reversal factor compensates for the 180° Z rotation inverting the visual X axis.

**Every system that needs world-space facing direction (dash, wall jump, wall check raycast, fireball, etc.) must read `isFacingRight`**, never `transform.localScale.x`.

---

## Card System

### Data Architecture

- **`CardData`** (ScriptableObject) — card templates. Created as assets via Unity menu.
- **`RuntimeCard`** — instance, tracks `currentUses`, `isInfinite`, etc.
- **`CardActionType`** (enum in `GameEnums.cs`) — dispatch identifier.

### Adding a New Card

1. Add a new value to `CardActionType` enum if no existing action covers it.
2. Create a `CardAction` subclass in `Assets/Scripts/CardActions/Actions/` and register it in the dictionary in `CardActionExecutor.Awake()`. Declare an honest `ModifiedState` (ConflictFlags) for any state the action touches.
3. Create a `CardData` asset in Unity (right-click in Project view → Create → Card Data).
4. Set the asset's `actionType`, `maxUses`, `shiftCost`, sprite, etc. in the Inspector.
5. Add the card to the relevant reward pools / starter deck as needed.
6. If the card has a "where/how" (aim, range, placement, area), add a matching preview to `CardAimIndicator` (see "Card Aim Indicator System" below).

### Deck Structure

`DeckManager` maintains four piles: `drawPile`, `hand`, `discardPile`, `exhaustPile`. **Recall** (R key) is the player's manual refresh action — costs Shift, redraws the hand, cost increases each use within a level.

### Card Enhancements — Blompo's blessings (24 of them, rebuilt 2026-08-14)

**`CardEnhancements.cs` is the whole system: the enum, the metadata, the eligibility rules AND every runtime hook.** Adding a blessing is one file, not eight.

It went 7 → 24 because the designer's verdict on the original seven was *"too simple and not very fun"*, and that was right: six of the seven were "cheaper, or more of the same" and only one changed how a card BEHAVES. Three were **cut** and should not come back as-is:

- **On the House** (costs no Shift) — measured, **nine of fifteen cards already cost 0 Shift and the dearest costs 2**, so the whole cost-reduction axis is nearly a no-op. `Ritual` replaces it by going the other way: pay MORE, hit harder. That's a decision; a discount isn't.
- **Extra Spicy** (+50% damage) — asked nothing of the player.
- **Double Dip** (plays twice) — could only ever be offered on the five cards holding no ConflictFlags, so most of the deck never saw it. **`Echo` is the same idea done properly:** its 2-second delay lets the first cast's flags expire, so it works on the whole deck. The delay is the mechanism, not flavour.

**The hooks, and where they're consumed:**

| hook | called from | blessings |
|---|---|---|
| `EffectiveCost` | `DeckManager.PlayCard`, `CardAimIndicator`, `BlompoScreen` | Ritual, Donor Card |
| `ModifyActionValue` | `PlayCard` (cast time) | Ritual, Glass, Loaded Dice |
| `ModifyDamage` | `RelicManager.ModifyPlayerDamage` | Grudge, Momentum, Finisher, Opener, Heavy Hitter |
| `ShouldSpendCharge` | `PlayCard` | Sleight of Hand, Slow Burn, Teacher's Pet |
| `NotePlayed` / `NoteKill` | `PlayCard` / `EnemyHealth.Die` | Compound Interest, Donor Card, Toll Booth, Grudge |
| `RescueFromExhaust` / `OnExhausted` | `PlayCard` routing | Last Call, Inheritance |
| `BeginRoom` | `PlayerController.OnNewRoomEnter` | Time Will Come, Only Child, Teacher's Pet |
| `StaysInHand` / `RetainsThroughRecall` | `PlayCard`, `ReloadRoutine` | Clingy, Teacher's Pet |

⚠️ **`EffectiveCost` is the ONE place a card's cost is computed.** DeckManager, CardAimIndicator and BlompoScreen all call it. They used to each carry a copy of the rule with a comment begging whoever edited one to remember the others.

⚠️ **Damage-time blessings live at `RelicManager.ModifyPlayerDamage` for two reasons**: it's the single chokepoint every point of player damage passes through (so a damage source added later can't forget them), and it's **the only place the TARGET is known** — which is what lets Finisher and Opener read the enemy's real health instead of guessing at cast time.

⚠️ **`DeckManager.AttributedCard` is NOT the same as `CardBeingPlayed`, and the difference is projectiles.** `CardBeingPlayed` is live only during `ExecuteAction`. Most card damage resolves synchronously inside that, but **a Fireball lands seconds later**, so `Fireball.sourceCard` is stamped at spawn and re-installed around its hit. **Without that, every damage blessing silently does nothing on the main attack card.** Any future delayed damage source (a lingering pool, a summon) must do the same. It must also be nulled again, or the next spike or pogo bounce inherits a Grudge bonus it never earned.

Kills are credited in `EnemyHealth.Die`, which runs *inside* `TakeDamage` while the attribution is still live — so Grudge and Toll Booth are exact, not a time window.

⚠️ **TWIN IS DELIBERATELY BROKEN, BY REQUEST.** A Twinned card counts as still-unblessed, so it can take another blessing **and be Twinned again** — the only route in the system to an exponential deck. The designer wants the ceiling found in beta rather than guessed. If it's miserable rather than fun, the fix is one line in `CanApplyTo`.

**Offer weights were flattened 10/6/3/1 → 10/7/5/2** ("make Blompo more OP overall"). Measured over 4000 visits: **8.6% offer a Legendary, 59.6% an Epic-or-better.**

⚠️ **`Understudy` needs a THIRD pick step** in `BlompoScreen` (blessing → card → partner). It's the only blessing that does; `NeedsPartner` is the flag.

⚠️ **Per-card blessing state lives on `RuntimeCard`**, not CardData — `grudgeBonus`, `roomsSincePlayed`, `lastCallUsed`, `playedThisRoom`, `lastCostPaid`, `understudyPartner`. Same reason the enhancement itself does: it's per-copy and per-run.

### Shuriken (built 2026-08-14) — and the Animator traps it exposed

`CardActionType.Shuriken = 20`. **8 damage, 10 charges, 0 Shift, and AIMED** — it flies at the cursor
in any direction, which is the thing Fireball structurally cannot do. It is in the shared card pool,
so any character can find it (verified surfacing in 107 of 2000 reward draws); the procedural star is
the fallback for a character not holding one.

Aiming needed no new input mode: cards are cast with a left click, so **the cursor at click time IS
the aim**, and `CardAimIndicator` draws the same line beforehand.

⚠️ **`AttackAction = 13` is THROW; `AttackAction = 14` is CAST.** 13 is a wind-up / hold / release
driven by `IsAttacking`; 14 is the wizard's spell pose, a 1.0s clip that self-exits at 80%. Using 14
for the throw is why it first looked wrong and ran long. (`AttackAction` is an **Int** — see the
Animator Parameter Map.)

⚠️ **`AttackSpeedMul` IS A GLOBAL ANIMATOR PARAMETER.** The throw sets it to **2.2** and must restore
it to **1** afterwards, or **any character carrying a Shuriken gets a double-speed Fireball cast**.

⚠️ **The star leaves on the arm's snap, not the keypress** (`THROW_RELEASE = 0.13s`). Spawned
immediately it outran the animation. The aim is captured at the press; only the release waits.

The projectile uses the pack's own shuriken sprite, sized from `sprite.bounds` rather than a fixed
scale, and the held star is hidden for 0.28s during the throw so the thing that flies is the thing
that was in his hand.

**Still open: the card ART is a placeholder** borrowed from Freefall Blade. `nameIsPaintedIntoArt` is
correctly `false`, so the title draws in code and reads right — only the illustration is wrong.

### Stagger Mechanic (REDESIGNED 2026-08-09 — it is no longer a three-strikes death sentence)

**Stagger buys Shift with blood at a price that rises forever.** It appears in the hand the moment Shift hits **0**, and playing it pays **+2 Shift** and charges **HP: 8, then 16, 24, 32, 40…** — `staggerHealthStep × (staggerCount + 1)`, escalating for the whole RUN with no cap and no per-room reset. There is no "three plays and you die" rule any more; the run ends when the next bill is bigger than your health bar. Tunables live on `PlayerController` (`staggerHealthStep`, `staggerShiftGain`) and are serialized in `Player.prefab`.

Four rules make it work, and each exists because the obvious alternative breaks something:

- ⚠️ **It appears on 0 Shift ALONE.** It used to also require an otherwise unplayable hand, because handing over a death sentence early would have been handing over a loss. It isn't one now — it's the pump you reach for when you're out — so gating it hides the option at exactly the moment it's the decision the player should be making.
- ⚠️ **It can never be discarded.** `ReloadRoutine` retains it exactly like a Clingy card. Recall would otherwise be the dodge (spend Shift you don't have to make the bill vanish), and discarding it would put it in the DECK. It costs a hand slot until you play it — that's the pressure.
- ⚠️ **IT ENTERS NO PILE.** `PlayCard` drops it on the floor instead of routing it to discard/exhaust. It is not a card the player owns: it is conjured on empty and evaporates when spent. The old code let it fall through to the discard, quietly enrolling it in the deck so it came back on later draws — a free-to-play card that costs HP, in hands where the player had plenty of Shift. It is also created `isInfinite`, so it has no charges and can never appear in the Scrap Forge's repair list.
- ⚠️ **The HP cost goes through `PlayerHealth.PayHealthCost`, NOT `TakeDamage`.** `TakeDamage` returns early on `isInvincible` and on an open parry window, and the +2 Shift is granted by the caller — so routing it through `TakeDamage` would hand out free Shift any time the player was mid-dash, holding a parry, or inside a Phoenix Cog mercy window. "Sometimes free" is worse than either. Verified: with `isInvincible = true` AND a live parry window, the 16 HP still landed. Phoenix Cog can still save you from a lethal Stagger — paying more than you have is exactly the fail state.

Echo Chamber is exempt from double-casting it (a coin flip that secretly doubles the blood price reads as a bug), and the whole trade — payout, charge and the escalation counter — is skipped in the hub under the umbrella rule.

**The card face is bespoke** — see `CardUI.RefreshCardFace`. Stagger's art (`Assets/Art/stagger.png`) has **no corner medallions**, so `costText` and `usesText` are switched OFF for it; the cost is drawn into the **heart centred on the top edge**, and turns **red when the price is ≥ current HP** (the only place the fail state is visible before it happens).

⚠️ **The heart/plate fractions in `CardUI` are measured against the SPRITE RECT, not the png.** Unity's auto-slice trimmed the transparent margin, so the sprite is **118×205 at offset (3,3)** inside the 124×210 file; fractions taken against the file sit low and small. `CardArt` is 200×300 with `preserveAspect` ON and the art is a narrower aspect, so it **letterboxes** — the placement maps through that letterbox each time the rect resizes. If new art arrives at a different size, re-measure and update the four `HEART_*` / three `PLATE_*` constants; nothing else needs touching.

**Note for the designer:** `stagger.png` imported with **Bilinear** filtering while every other card art uses **Point**. It is upscaled ~1.4× in the hand, so Bilinear reads slightly softer than its neighbours — worth eyeballing and flipping to Point if it bothers you. Left as imported; it's a look judgement, not a bug.

---

## Scrap System · Quest System · Relic System

📐 **All three live in `/deckshift-economy`.** Invoke it before touching scrap, gold, the forge,
quests, oaths, relics, `RelicPool`/`RelicCatalogue`, or shop pricing.

**What must be true even when you are working elsewhere:**

- **Gold and scrap must never merge.** Gold comes from piles placed in levels and buys NEW power
  (cards, relics). Scrap comes from kills and buys SUSTAIN (charges on cards you own). If the shop
  ever sells charges, or scrap ever buys cards, one of the two is redundant and should be deleted —
  maintenance always loses to acquisition when they share a wallet.
- **Relics are slot-constrained: `RelicManager.MaxSlots` (5, +1 if Estate Sale is earned).** Every
  grant routes through `TryGrantRelic`, which raises the swap screen when full. ⚠️ `MaxSlots` is a
  static PROPERTY, not a const — a const gets inlined at every call site and would never grow.
- ⚠️ **Never add or subtract stats incrementally.** `RecomputePassives()` rebuilds from BASE stats
  every time the loadout changes, so selling reverses exactly. Incremental maths breaks the moment
  relics stack or are sold out of order.
- ⚠️ **Poll relics by `relicID`, never by asset filename.** Two traps: the asset named `Kinetic` has
  ID `KineticCapacitor`, and `SpikedCarapac` (no "e") has ID `SpikedCarapace` (with "e").
- ⚠️ **`Rarity.Boss` is an acquisition channel, not a keybind.** It is filtered out of every ordinary
  offer (`RelicPool.Offerable`); `RelicData.isArt` marks the few that bind a key, and only one Art
  may be held at a time.
- **Quests are per-run and reset on death.** `QuestSystem` is deliberately scene-local — do NOT
  re-add `DontDestroyOnLoad`.
- **Quest payout rule:** quests pay in things the shop does not sell.

## Hub Mode (Sandbox)

The hub is a sandbox room where the player tests cards, jumps freely, and experiments without consequence. The hub prefab is at `Assets/LevelEfeS/hub.prefab`. **It is currently the always-first room in every run** (see "First-Room Logic" under Level System).

### HubMarker Component

`Assets/Scripts/HubMarker.cs` is a marker MonoBehaviour with no fields or methods — its presence on a room prefab's root signals "this is a hub" to the rest of the codebase. Currently attached to the hub prefab's root.

`LevelManager.IsCurrentRoomHub()` returns true if the currently spawned room has a HubMarker. This is the single source of truth.

### Umbrella Rule: No Consumption In Hub

The hub gates every player-resource consumption call. The umbrella principle: **no resource is consumed and no permanent state changes** while the player is in a hub.

Specifically gated:
- Shift consumption from jumping (`PerformJump`)
- Shift consumption from playing cards (`DeckManager.PlayCard` → `player.SpendShift(cost)`)
- Shift consumption from portal second-placement (`TryPlacePortal`)
- Card charge decrement (`playedCard.currentUses--`)
- Card exhaust routing (cards play but don't go to exhaust pile when depleted)
- Recall shift cost (`TryRecall` → `SpendShift`)
- Recall cost escalation (`currentRecallCost++`)
- Stagger card injection (`CheckForStaggerCondition`)
- ~~Fall damage~~ — no longer applicable: fall damage has been removed from the game entirely (`FallAndRespawn` only teleports; see Resolved bugs)

All guards use the pattern: `if (LevelManager.instance == null || !LevelManager.instance.IsCurrentRoomHub()) { ... do the consumption ... }`.

**When adding new player-resource consumption code,** check whether it should also be gated by `IsCurrentRoomHub()`. The pattern is: at every consumption site, ask "should this be free in a sandbox?" — almost always yes.

### What Hub Does NOT Hide

UI is intentionally unchanged in hub. The shift counter, card hand, recall button — all visible and operate normally. Only the underlying mechanics are gated. This is so the hub can act as a tutorial space where the player sees the UI react.

---

## Manager Layer

There are 13+ singleton managers. This is a known architectural smell flagged in audit but currently load-bearing. Do not propose merging or restructuring without explicit user approval.

### List of Managers

- **GameManager** — top-level state, player reference, centralized pause counter
- **DeckManager** — card piles, draw/discard logic
- **LevelManager** — room spawning, transitions, zone/camera setup, first-room-is-hub logic
- ⚠️ **RewardManager** — the end-of-level card selection screen. **ORPHANED (verified 2026-08-11): the script and its `RewardScreenFX` still exist, but NOTHING CALLS IT.** The screen was removed 2026-08-09 and its route-choice hook moved to `LevelManager.AdvanceToNextRoom`. Cards now come from chests, the shop and quest payouts. Either delete it or re-wire it deliberately — don't assume it runs.
- **RelicManager** — owned relics, `HasRelic(string id)` polling pattern, `OnRelicAdded` event
- **SkillManager**, **SkillRewardManager** — skill tree / skill selection
- **QuestSystem** — quest tracking, board UI, accept/progress/complete events
- **ShopManager** — in-game shop UI and purchases
- ⚠️ **`SlotMachineUI` IS DELETED (verified 2026-08-11)** — the script is gone and nothing in any scene or prefab references it. The gambling system no longer exists in the game at all. The "Dice Broker" idea in Deferred Work is now a from-scratch build, not a reskin. (There was never a `SlotMachineManager` type either.)
- **AchievementManager** — achievement tracking
- **MainMenuController** — the main-menu scene. ⚠️ **`PauseMenu` was DELETED 2026-08-09** along with its `MenuManager` GameObject and the `PauseMenuPanel` hierarchy; the pause screen is now `PauseScreen`, a self-bootstrapping procedural screen (see UI System). Do not re-create a scene-placed pause panel. There is also no `MenuManager` *type* and never was.
- ⚠️ **`EffectManager` DOES NOT EXIST as a type** (verified 2026-07-18) — this entry was a phantom. Confusingly there IS a GameObject *named* "EffectManager" in SampleScene, but it carries **HitStop**, not an EffectManager component. VFX are spawned ad-hoc by the callers (e.g. `Instantiate` of a VFX prefab) and by house-pattern procedural classes (`DashAfterimage`, `ShockwaveVFX`, `SpitGlob`, `CardAimIndicator`).
- **MusicManager** — background music
- **CameraShake**, **HitStop** — game-feel singletons (camera shake + freeze frames)

#### Scene presence in SampleScene — verified 2026-07-18

Because "the system exists in code but isn't in the scene" is this project's #1 recurring bug (see Common Pitfalls), here is the audited truth:

- **Present, component-enabled, active:** GameManager, DeckManager, LevelManager, RelicManager, SkillManager, SkillRewardManager, QuestSystem, ShopManager, AchievementManager, RewardManager, **CameraShake (on Main Camera — the 9-month "no shake" bug is genuinely fixed)**, CameraFollow (Main Camera), **CameraPeek (Main Camera ONLY — the Player duplicate is confirmed gone)**, HitStop (on the GameObject named "EffectManager").
- **NOT in SampleScene:** `MusicManager` and `SfxManager`. Both exist as MonoBehaviours but live elsewhere (MainMenu boot flow). Consequence: **entering Play mode directly in SampleScene gives you no MusicManager**, so no BGM — that's expected, not a bug. `SfxManager`'s entry points are `static` and work fine without a scene instance, which is why SFX still play.
- ⚠️ **Leftover `CinemachineCamera` GameObject still sits in SampleScene, INACTIVE, carrying a second `CameraShake` component.** Harmless while inactive (Awake never runs, so it can't hijack the singleton), but it is Cinemachine-era cruft and a trap if anyone activates it — that would create a duplicate CameraShake singleton. Part of the same pending Cinemachine cleanup as the dead `using Unity.Cinemachine;` directives.

**Build settings (verified):** `MainMenu`(0), `Hub`(1, disabled), **`SampleScene`(2)**, `GameOverScene`(3), `GameScene`(4).

### Pause Counter System

`GameManager` has a centralized pause counter that any UI/menu system uses instead of writing `Time.timeScale` directly.

```csharp
GameManager.instance.RequestPause();   // increments depth, sets timeScale=0 if depth becomes 1
GameManager.instance.ReleasePause();   // decrements depth, sets timeScale=1 if depth becomes 0
```

**Use this for any new UI that should pause the game.** Do not write `Time.timeScale = 0` directly in new code.

Exceptions that intentionally bypass the counter:
- `HitStop.Stop()` — sets timeScale=0 briefly for hit freezes. Not a "pause" semantically.
- `PlayerController.AdrenalineSlowMoRoutine` — slow motion at timeScale=0.4f. Not a pause.
- `PauseScreen.AbandonRun()` / `QuitGame()` — hard reset before a scene transition.

**`GameManager.IsUIPaused` (added 2026-08-09) is the single honest "is another screen already up?" test.** Every modal in the project routes through `RequestPause` — shop, map, forge, Blompo, chests, quest board, relic panels — so one property covers all of them and cannot fall behind when a screen is added. `PauseScreen` uses it to decide whether Escape belongs to it. **Prefer this over a hand-kept list of `SomeScreen.IsOpen` flags**, which is the exact pattern that has rotted twice in this project (`ShopManager.allRelicsPool`, `Chest`'s per-tier lists).

⚠️ **And it needs a ONE-FRAME MEMORY, not just a live read.** Script execution order is undefined, so on the frame the shop closes on Escape it may release its pause *before* the pause screen's `Update` runs — leaving Escape still down, nothing paused, and the pause screen opening instantly behind the screen the player just dismissed. `ShopManager` used to carry an `escapeConsumedFrame` stamp for precisely this (now deleted), but a stamp only ever protected the one screen that remembered to set it. `PauseScreen` instead refuses to open if any UI held the pause on the **previous** frame, which covers every Escape-handling screen and requires nothing from any of them.

### Known Manager Issues

- **Cyclic dependencies:** PlayerController → DeckManager → PlayerController. Don't add more cycles.
- **Most managers lack `DontDestroyOnLoad`**, intentional for single-scene operation.
- **QuestSystem is now scene-local too** — its `DontDestroyOnLoad` was removed 2026-06-10 (quests are per-run by design; the survivor's dead UI references broke the quest board after the first death).
- **`GameManager.instance.player` is accessed from many UI scripts** with inconsistent null guarding. Add null guards when touching these sites.

---

## UI System

📐 **UI work has its own loadable skill: `.Codex/skills/deckshift-ui/SKILL.md`.** House style and the
inversion rule, the theme table, linear-colour-space calibration, uGUI traps, the wiring contract, a
pre-delivery checklist, **and (§8) the full catalogue of every screen that exists** and the traps each
one paid for. **Invoke it (`/deckshift-ui`) before building, restyling, reviewing or debugging any
screen, panel, HUD element, card face, world-space marker or UI VFX.** What follows is only what must
be true even when you are not doing UI work.

### ⚠️ SALVAGE — the one material system (2026-08-20). It replaced the nine-theme rule.

The designer's brief: *"i want a settings menu, a pause menu, a blompo UI/VFX, the shop, the forge,
the map, and every other UI asset … to feel like they would have been in the cainos packs. i want
consistency in the visuals overall, not seperated to menus and the actual gameplay, but everything."*

The old rule — **"every screen gets its own material, never the same skin, pick one and invert
something"** — did exactly what it said, which is make screens look *unlike* each other: nine invented
materials (smoked glass, brass, frost) and a hue budget that ran out. **Every settings screen built
under it was rejected, and the rule was why.**

⚠️ **THE FIX IS NOT ONE SUBSTRATE EVERYWHERE — that is monotony, and `Vigil` (stone alcoves, real
dungeon art, a torch each) was rejected TWICE proving it.** The Cainos pack holds crates, pots,
banners, chains and fireplaces and still reads as one world. **Consistency lives in the TREATMENT.**

`Assets/Scripts/Salvage.cs` is the file; five laws no screen may disagree with:

1. **Scale** — `Salvage.Scale` = **2.4107** (14 world units / 1080 canvas / 32 PPU). UI art is the
   exact size the same art is in the game. `Salvage.SpritePPU` enforces it automatically.
2. **Light** — warm, **upper left**, always.
3. **Colour** — **sampled from the pack PNGs, never chosen** (`SalvageArt` + its baker). Measured
   dungeon stone is **`#444548` cool-neutral**; the old "warm charcoal" was reasoned from the
   district's *name* and is wrong against the art. Warmth comes from wood and torchlight only.
4. **Accent** — **exactly two in the entire game**: `Salvage.Torch` (lit) and `Salvage.Shift`
   (energised — the altar orb's cyan, the same that seals the gate). `Salvage.Wound` red is a
   warning, not an accent. **No screen ever spends a new hue again.**
5. **Wear** — used **and repaired**. The repair currency is literally called scrap.

Variety comes from **what the object is**, not from a colour: a hung sheet, a notice board, a
workbench, a banner. **Migrated so far: `PauseScreen` only** (the Hanging Board). Everything else still
renders in the superseded themes until converted.

⚠️ **`Assets/Cainos/Pixel Art Icon Pack - RPG` has 107 icons and 89 are referenced NOWHERE.** Same
artist, same PPU, same palette. Use them before drawing another procedural sigil.

### Canvas hierarchy

SampleScene's main Canvas contains **`GameplayHUD`** (gold, health, shift, recall, pile buttons, hand
drawer, RelicHUD, QuestTracker, ExitMarker) — toggle it off to hide the HUD during full-screen UI.

⚠️ **Procedural screens create themselves under the Canvas at RUNTIME and are NOT in the scene file** —
`PauseScreen`, `RunMapScreen`, `ScrapForgeScreen`, `BlompoScreen`, `QuestBoardScreen`,
`SettingsScreen`, `ShopScreenUI`, `CharacterSelectScreen`. **Do not go looking for them in the
hierarchy at edit time**, and do not "fix" their absence by placing one in the scene. Only
`TutorialPanel` remains scene-placed.

### Rules that bind any code touching a RectTransform

⚠️ **NEVER SCALE UI CONTAINERS — RESIZE THEM.** Changing Scale cascades to children and fights Layout
Groups, producing wildly wrong sizes. The honest fix is always Width/Height, sometimes anchor/pivot.
Leave Scale at (1,1,1). This has bitten twice.

⚠️ **Every CanvasScaler is `ScaleWithScreenSize`, ref 1920x1080, `matchWidthOrHeight = 1` (HEIGHT).
Do not change the match value.** The camera is height-anchored (`orthographicSize = 7` ⇒ 14 world
units tall at every aspect), so matching width makes the UI do the opposite of the camera. The canvas
is therefore always 1080 tall and only width flexes (1440 at 4:3 → 2560 at 21:9).

⚠️ **An element at a screen EDGE must be ANCHORED to that edge**, or it drifts as canvas width varies.

### Wiring any new screen

- **Extend `GameScreen`** (`Assets/Scripts/GameScreen.cs`) — it owns pause, game state, HUD hide,
  drawer lock, the one-frame Escape memory and both aspect-fit modes. Do NOT retrofit existing
  screens all at once; `QuestBoardScreen` is the migrated worked example.
- **Pause through the counter, never `Time.timeScale` directly:** `GameManager.instance.RequestPause()`
  / `ReleasePause()`. Documented exceptions: `HitStop`, Adrenaline slow-mo, hard resets before a scene
  transition.
- **`GameManager.IsUIPaused` is the single honest "is another screen up?" test** — prefer it over a
  hand-kept list of `SomeScreen.IsOpen` flags, a pattern that has rotted twice here.
- **Hide `GameplayHUD` and call `HandUIDrawer.instance.SetLocked(true)`** when a full-screen panel
  opens (and `false` on close). The drawer's Image has `raycastTarget` on to detect hover, so it
  absorbs clicks in its rect until locked.
- **Set text through `UIType`** — `Apply` for the display face (CCBattleScarred: titles, labels,
  numbers), `ApplyProse` for real sentences only (Pixie). The display face has essentially no
  lowercase and renders prose as a wall of capitals.
- **Self-bootstrapping singletons must register with `SceneBootstrap.Register`**, and `Create` must be
  idempotent — `[RuntimeInitializeOnLoadMethod]` fires once per SESSION, not once per scene.

### Cards: rarity colour is the ART's job, not the UI's

**Card rarity is telegraphed in the card ARTWORK** (dark grey Common, light grey Uncommon, yellow
Rare, purple Epic; there are no Legendary cards). **UI code must never invent a second rarity colour
system on a card** — two colour codes that disagree is worse than one. The blessing mark is therefore
one fixed teal, and blessing hierarchy moved to a channel the art does not use: only Epic/Legendary
pulse.

⚠️ **The Freefall Blade frame is the canonical card frame** (designer, 2026-08-17): red ball = charges
left, blue crystal = Shift cost right, an **empty name plate** drawn in code, and a heart container on
cards that deal damage. New card art uses it, and ships with the name plate EMPTY —
`CardData.nameIsPaintedIntoArt` defaults to `false`; the 14 older cards have their titles painted in
and set that flag. `CardFace` is the single source for every screen **including the hand**.
## Camera System

### CameraFollow.cs (custom)

Replaces Cinemachine for the main follow camera. Each level prefab contains a **`CameraBounds`** child GameObject with `BoxCollider2D` zone children (the shared `Assets/Prefabs/CameraBounds.prefab` carries one zone collider on its root). `LevelManager` finds it via `transform.Find("CameraBounds")` on spawn and passes the zones to `CameraFollow`.

- Camera clamps to the zone the player is currently in.
- Zone transitions use hysteresis (zone doesn't change until player leaves current zone).
- No lerp on zone transition — direct follow (lerp was tried, caused jitter).

**Naming is case-sensitive:** the child must be named exactly **`CameraBounds`** — verified against `LevelManager.cs` (`Find("CameraBounds")`) and the real level prefabs on 2026-07-13. (An earlier version of this file claimed the name was `LevelBounds`; that was stale/backwards — `LevelBounds` appears nowhere in the codebase.)

### CameraShake.cs

Rewritten to work without Cinemachine. Uses a `shakeOffset` Vector2 that `CameraFollow.LateUpdate` adds to the final clamped position (so shake can briefly push past zone bounds, which feels correct).

- Uses `unscaledDeltaTime` so shake still plays during HitStop freezes.
- Call sites: `CameraShake.instance.Shake(duration, intensity)`. Always null-guard `instance`.

**The CameraShake component must be present in the active scene** (on the Main Camera) and **enabled**. If it's missing or disabled, every Shake call silently no-ops. This caused a 9-month "no shake anywhere" bug that wasn't discovered until the audit.

### CameraPeek.cs (REBUILT — working, verified by code audit 2026-06-10)

Rebuilt without Cinemachine, along the planned CameraShake-style design: holding Left Ctrl computes a mouse-direction `peekOffset` (clamped to `maxOffset`, smoothed with unscaled time) that `CameraFollow.LateUpdate` adds after zone clamping. **The component lives on Main Camera ONLY** — a duplicate copy on the Player prefab was removed 2026-07-16 (unguarded `instance = this` singleton; two copies made the winner a coin flip). Do not re-add it to the Player. Input is blocked while paused, while the hand drawer is locked, or when the player is dead. If peek "doesn't seem to work," verify scene presence and enabled state of the component first (per Common Pitfalls) — the code is fine. Note: the rebuilt CameraPeek does NOT set `PlayerController.isPeeking`; that flag is dead code.

Related: a missing-script warning for `CameraBoundsController` appears in the console at scene load — this is part of the same Cinemachine-era cleanup that's pending. Cosmetic; doesn't affect gameplay.

---

## Level System

📐 **Rooms have their own loadable skill: `.Codex/skills/deckshift-levels/SKILL.md`.** The full
Level Design Laws with their reasoning, the ASCII importer and every tile-painting rule, the
validator's measured movement budget, doors/gates, the room pool inventory, and the run map all live
there. **Invoke it (`/deckshift-levels`) before authoring, importing, validating or debugging a
room, or before touching `LevelManager`, tiles, gates or the exit door.** What follows is only what
must be true even when you are nowhere near a room.

### The Level Design Laws — titles only (full text and reasoning in the skill)

1. **Every level must be completable with ONLY jumping and moving.** Cards, fans, elevators and
   trapdoors may gate *optional* things — never the exit. This one constrains card and mechanic
   design too, which is why it is here and not only in the skill.
2. Mandatory rises at **4** tiles (the character jumps ~5-6); flat gaps ≤ 5-6.
3. Hazard pits on the mandatory path must be escapable and crossable unaided.
4. **No one-way (`=`) platforms.**
5. Turrets (`t`) only on walls/ceilings — so never in generated rooms.
6. The player has **no wall-breaking attack**; never design a secret that needs terrain destroyed.
7. **Entry and exit must be far apart**, separated by whole chambers — a Phase/Portal must never skip
   the level.
8. **The spawn is a safe beach.** No enemy on the spawn's ground run, and nothing with line of sight
   to it. Enforced by `LevelValidator`.

### The room contract — three parts, all required

A room prefab must have a **`CameraBounds`** child (exact name, case-sensitive — zone
`BoxCollider2D`s), a **`GirisNoktasi`** entry point, and an **`ExitDoor`**. Miss `CameraBounds` and
the camera never clamps, so at wide aspects you see straight past the art into undressed space; miss
`ExitDoor` and the room is unfinishable. **Always re-check all three before adding a room to
`LevelManager.roomPrefabs`** — ~15 contract-valid rooms exist unused, and several near-misses do not
satisfy it.

⚠️ **`LevelManager.roomPrefabs` HAS BEEN WIPED FIVE TIMES, and it does not announce itself.** (The
fifth was found 2026-09-06, down to a single scratch room that was not even the hub.) Element
0 must be the hub; 1..n are the combat rooms; the boss has its own `bossRoomPrefab` slot. When the
list is short or holds a scratch room, there is **no hub** (so no sandbox first room, no quest board,
no forge), every room in the run is the same room, and if the stand-in lacks `CameraBounds` the camera
stops clamping. The only console clue is one Turkish line, `CameraBounds objesi bulunamadı!`, which
reads like ordinary noise. **When anything about a run feels wrong — no hub, repeated rooms, a camera
showing the void — read this list before debugging the map or the camera.** Restore it by resolving
the GUIDs recorded in the last good commit, never by re-picking prefabs by filename.

⚠️ **The `.txt` is NOT the source of truth for `GenLevel7/8/9/10`.** The designer has hand-edited the
built prefabs. Re-importing destroys that work **and** renumbers every fileID, which silently drops
the room out of `roomPrefabs` (the reference keeps a valid guid pointing at a fileID that no longer
exists, so it reads as `null` in the Inspector while looking fine in YAML). Edit these in the editor.

⚠️ **Actors live at `PlayPlane.Z`; everything else is behind it.** `PlayPlane.Apply(room)` runs on
every spawn and is why props no longer render over the player. Do not hand-tune prop Z.

**Active scene is `Assets/Scenes/SampleScene.unity`.** Other scene files exist but are inactive or
legacy — check SampleScene first when debugging "is this in the scene?".
## Enemy System

📐 **The detail lives in `/deckshift-enemies`.** Invoke it before touching any enemy prefab, enemy
AI, `MonsterController`, `EnemyHealth`, `EnemyMelee`, `EnemySenses`, a boss encounter, or anything
about how enemies move, see, hit or die.

**What must be true even when you are working elsewhere:**

- **Card and enemy numbers derive from `CardAnchors.md`.** Damage unit = **15** (one Fireball);
  player has **100 HP** and **40 Shift**; fodder ≈ 12 HP dies to one Fireball.
- ⚠️ **Enemy layers are INCONSISTENT and it is load-bearing.** Zombies, bats, Mimic and ShieldEnemy
  are on Default(0); MeleeEnemy, RangedEnemy, Slime, Turret, Patrol and MossKnightBoss are on
  Enemy(11). `groundLayer` includes layer 11, so the player can stand on the second group and not
  the first. Prefer `GetComponentInParent<EnemyHealth>()` over a layer mask.
- ⚠️ **Enemy bodies are mass 500** so the player cannot shove them. Mass is free here — locomotion
  and gravity do not depend on it. Side effect: the player is BLOCKED by enemy bodies, and dash does
  not disable collision.
- ⚠️ **Melee hits go through `EnemyMelee.TryHit`** — a box in FRONT of the attacker tested against
  the player's real capsule — never a distance check between transform origins.
- ⚠️ **Sight is `EnemySenses`, cast from the CHEST not the feet**, because a ray from a grounded
  transform starts inside the floor tile. Sight ACQUIRES, memory KEEPS (2.5s).
- ⚠️ **`EnemyHealth.Die()` fires `OnDied` and destroys the GameObject in the SAME frame.** Anything
  that must outlive the death needs its own object (see `BossDeathVFX`, `BossRewardCue`).
- **Still open, deliberately:** enemies never jump (so they stop at ledges) and `MeleeEnemyAI` never
  patrols. Both change difficulty and are the designer's call.

## Audio System

`MusicManager` handles background music (incl. `PlayBossMusic()`/`StopBossMusic()`).

**There IS a central SFX helper now: `SfxManager` (singleton).** Two static entry points, both multiplying a per-call `localVolume` by a global `SfxManager.Volume`:
- **`SfxManager.PlayOn(AudioSource source, AudioClip clip, float localVolume = 1f)`** — a `PlayOneShot` on a **2D** source you own. Use this for sounds that must be clearly audible regardless of distance (boss abilities, player footsteps, the crusher slam). Because it's a one-shot on a 2D source, `localVolume` can go **past 1** for headroom, and the source can be `.Stop()`ped for looping/sustained sounds (e.g. the boss charge).
- **`SfxManager.PlayAtPoint(AudioClip clip, Vector3 pos, float localVolume = 1f)`** — positional/3D (`PlayClipAtPoint`), **distance-attenuated and clamped to [0,1]**. Fine for small local pickups (gold), but it goes quiet in a big arena and can't be boosted — that's exactly why the crusher slam was switched to a 2D `PlayOn` source with a `[0,2]` slider.

When adding audio cues: expose a `[SerializeField] AudioClip` (+ optional `[Range]` volume) field, and route it through `SfxManager` with a null guard. For a runtime-built source, add an `AudioSource` in code with `playOnAwake=false` and `spatialBlend=0` (2D). Animation-driven SFX (footsteps) come in via `PlayerAnimEventSink` relaying to `PlayerController.PlayFootstep()`; boss ability SFX are frame-synced via the Cainos `AnimationEventReceiver.onAttack` event.

---

## Common Pitfalls (Hard-Won Lessons)

### "Importing an asset pack can overwrite ProjectSettings and break everything"

The Cainos Customizable Pixel Character pack is distributed as a "complete project." Its first import dialog warns about overwriting project settings; the second dialog ("Step 2 of 2: Import Settings Overrides") lists 15+ ProjectSettings files marked **Override**. Accepting these overwrites your URP renderer config (shaders go pink), tags (custom tags vanish), physics (gravity/layer matrix changes), and input bindings.

**Always click "None" on the Step-2 overrides screen.** Always uncheck duplicate Cainos packs you already have in the Step-1 file tree (overwriting shared files in `Common/` can break other Cainos packs too). The pack itself is fine to import once these are excluded.

### "The system exists in code but doesn't work"

Check whether the component is **actually in the scene and enabled**. Multiple times during development, scripts were perfect but the GameObject was missing or the component was disabled. Examples:
- CameraShake was in the scene but disabled for 9 months.
- HitStop was missing from some scenes entirely.
- QuestSystem was missing from SampleScene after the old hub scene was deleted — the script existed and was complete, the manager just wasn't instantiated anywhere.

When a system "doesn't seem to work," verify scene presence and enabled state BEFORE assuming the code is wrong.

### "[RuntimeInitializeOnLoadMethod] runs ONCE PER SESSION, not once per scene" (2026-08-09)

**`RuntimeInitializeLoadType.AfterSceneLoad` says WHEN in the startup sequence the method runs. It does NOT mean "after each scene load".** The name reads exactly like it does, which is why this survived.

Consequence, reported by the designer as *"after restarting from the death screen I can no longer use the map"*: dying is `SampleScene → GameOverScene` and RESTART is `GameOverScene → SampleScene`. Both are scene loads, and a **scene-local self-bootstrapping singleton is destroyed by the first one and never re-created**. So from the player's first death onward, for the rest of the session:

- **`RunMapManager` gone** — `M` did nothing, and `LevelManager` silently fell back to `PickNextRoomPrefabWithoutMap()`, i.e. random room order. Exactly the "looks almost right" failure that class's own header warns about.
- **`ScrapHUD` gone** — no scrap counter.
- `SfxManager` was fine only because it happens to call `DontDestroyOnLoad`.

⚠️ **The irony worth remembering: self-bootstrapping was adopted to stop systems going missing from scenes, and it introduced a new way for systems to go missing.** Managers *placed* in the scene never had this problem — reloading the scene brings them back.

**Fix: `SceneBootstrap.Register(Create)`** — runs the creator now and on every `sceneLoaded`. Any new self-bootstrapping singleton must use it, and its `Create` must be idempotent. Verified across two full death→restart cycles: both managers return, the hub is the first room again, the map regenerates, and `M` opens a freshly drawn chart.

**When touching anything per-run, test the SECOND run, not the first.** A scan for dangling statics found none, so the damage was confined to these two — but the first run is not evidence about the second.

### "Idempotent operations hide bugs"

Setting `Time.timeScale = 0` is idempotent — calling it twice does the same as once. This hid the ExitDoor double-fire bug for months. Now that the pause counter is in place, redundant calls become visible (pauseDepth goes to 2). **If you find redundant-but-harmless calls, audit whether they should be redundant.**

### "Reading back transform.localEulerAngles is unreliable"

Unity normalizes Euler angles and may return unexpected combinations after 180° rotations. **For rotation tracking, store the angle in a float field and write it via `Quaternion.Euler(0, 0, currentZ)`.** Never read back from `localEulerAngles`.

### "Camera.main is slow and can be null"

`Camera.main` does a tag-based lookup every call. **Cache it in Awake.** Add null guards at call sites.

### "Visual flip ≠ Physics flip"

When rotating a sprite 180° around Z to simulate gravity reversal, the collider does NOT rotate. The capsule remains upright. Don't try to rotate the collider — translate the visual instead (this is what `visualFlipYOffset` does).

### "Cinemachine values don't translate to direct camera offsets"

A Cinemachine `AmplitudeGain` of 0.15 looks very different from a direct `transform.position` offset of 0.15 world units. When porting away from Cinemachine, expect to retune all magnitudes.

### "Animator parameter type errors are silent until used"

`AC Character.controller` lists `AttackAction` as `m_Type: 3` in YAML, which is **Int**, not Float. The mapping is: 1=Float, 3=Int, 4=Bool, 9=Trigger. When in doubt about an Animator parameter type, read the .controller YAML directly rather than guessing from the parameter's appearance in the Animator window.

### "Setting a shader property that doesn't exist fails SILENTLY" (2026-07-26)

**This is the project's most expensive bug shape: code that looks correct, runs without error, and does nothing.** `material.SetColor("_Color", …)` / `MaterialPropertyBlock.SetFloat(…)` on a shader that lacks that property is a **no-op with no warning**. The gravity-reversal warning flash was "fixed" TWICE this way and stayed invisible for months — first tinting `_Color` (the Cainos **"Alpha Cut"** shader exposes no colour property at all), then switching to `GetComponentsInChildren<SpriteRenderer>()` (the Mage M rig is 16 SkinnedMeshRenderers + ONE SpriteRenderer, the staff — so only the staff flashed).

Known property support (verified by dumping `ShaderUtil.GetPropertyCount`):
| Shader | Has `_Color`? | Has `_Alpha`? |
|---|---|---|
| Cainos `Customizable Pixel Character/Alpha Cut` (most player outfit parts) | ❌ **no colour property** | ✅ |
| Cainos `Customizable Pixel Character/Body` | ❌ (has `_SkinTint`) | ✅ |
| Cainos `Customizable Pixel Character/Hair` | ✅ | ✅ |
| Cainos `Pixel Art Monster - Dungeon/Transparent` (**all enemies**) | ✅ | ✅ |

**Rules:** on the PLAYER rig, tint via **`_Alpha`** — the one handle every Cainos rig shader shares. On ENEMIES, `_Color` is fine (verified across every enemy prefab). When writing any new material-property effect, **dump the shader's property list first** rather than assuming, and prefer `HasProperty` + an explicit fallback over `HasProperty` + silently skipping (a guarded skip still produces "nothing happens", which is the bug).

Also beware the inverse: `BreakableWall.cs` checks `HasProperty("_Color")` when *caching* the original colour but not when *setting* it — an asymmetry worth copying nowhere.

### "The project renders in LINEAR colour space, so low alphas composite far brighter than the number reads" (2026-08-09)

Verified: `QualitySettings.activeColorSpace == Linear`. A small alpha of a bright, saturated colour over a dark panel therefore lands **much** higher in sRGB than the arithmetic suggests — the settings screen's selection plate at **alpha 0.065** of arc-cyan measured out near **0.36 sRGB** on screen and filled the whole selected row with a solid teal slab. It had to drop to 0.03.

This is the mechanism behind two rules already in this file — "atmosphere effects want roughly half the alpha you first reach for" and "tint darker than you think and you'll get a black hole" — and it explains why they keep being right. **Consequences:**

- **A tint that looks correct in the numbers is not correct.** Pick every subtle alpha by screenshot, never by reasoning about the value.
- **The brighter and more saturated the colour, the worse the gap.** Halt's frost blue at 0.07 reads as a restrained plate; Apparatus's cyan at 0.065 read as a slab. The same alpha is not the same weight in a different theme, so do not copy an alpha across themes.
- The inverse of the deep-rock lesson: there, a 0.5-intensity Light2D *halved* the value and made a dark tint a pit. Same underlying point — **measure the pixels, don't compute them**.

### "A UI raycast test must let a FRAME PASS after building the UI" (2026-08-09)

`GraphicRaycaster` skips any graphic whose **`Graphic.depth == -1`**, and `depth` is only assigned when the canvas performs a render pass. So a UI built (or shown) inside an `execute_code` call is **invisible to `EventSystem.RaycastAll` in that same call** — every hit test returns `<nothing>`, including against a full-screen backdrop that plainly covers the point.

This produced a completely convincing false negative while verifying the pause menu's rows: five MISSes with correct geometry, correct `blocksRaycasts`, correct sibling order, nothing culled. The tell was `depth == -1` on a graphic that was demonstrably on screen.

**Open the screen in one tool call and raycast in the NEXT** — the same split already required for `ScreenCapture.CaptureScreenshot`, and for the same underlying reason. Re-run after the split: all five rows hit.

(This does NOT retire the standing rule that pointer behaviour must be verified geometrically rather than by invoking `OnPointerEnter` yourself — see the card-flip entry. Both traps are live; this one is about *when* you measure, that one about *what* you measure.)

### "First diagnostics can be wrong; always verify"

During the character swap session, Codex's first diagnostic incorrectly described `AttackAction` as a Float. The error only surfaced at runtime as a type mismatch. **For Animator parameter types specifically, the YAML `m_Type` integer is the source of truth.**

More generally: this file itself drifts. A 2026-07-26 pass found the entire Relic System section describing a slot redesign as an unbuilt "future direction" when it had already shipped, three "deferred bugs" that were already fixed, and a HUD described as a left-side vertical column when it is a top-centre bar. **Verify against the code before planning from this document** — and when you find drift, fix the doc in the same session.

### "Transform.Find is strict and silent"

The QuestTrackerHUD looks for children named exactly `Title` and `Progress` (case-sensitive). A typo, trailing space, or different capitalization causes Transform.Find to return null, and the defensive code skips text assignment silently. When a tracker, popup, or instantiated UI element appears blank, the first thing to check is child naming inside the prefab.

### "GetComponentInChildren can return null"

`acceptButton.GetComponentInChildren<TextMeshProUGUI>().text = "ACCEPTED"` crashes if the button has no TMP descendant. This caused a silent quest-acceptance failure: the exception fired BEFORE the actual AcceptQuest logic ran, so the system looked like "nothing happened on click." Always null-guard before dereferencing GetComponentInChildren results.

### "Cainos '3D Lit' props sort by Z-DEPTH, not sortingOrder" (2026-07-18)

**A prop drawing on top of the player is almost always a Z-position problem, NOT a sorting-layer/order problem.** The Cainos "Pixel Art Platformer - Dungeon" props (doors, frames, etc.) and the Cainos player character both render with **opaque shaders in render queue 2000** (`Sprite 3D Lit …`, `Customizable Pixel Character/Body`, `.../Alpha Cut`). Opaque geometry sorts by **camera depth (Z distance)** — `SpriteRenderer.sortingOrder` is essentially ignored for them. Two opaque things at the same Z sort ambiguously and one arbitrarily wins.

The fix is **Z position**, not sorting order: push the prop farther from the camera than the player. The camera looks along **+Z** (camera at negative Z), so "behind the player" = **larger Z**. Because the camera is orthographic, changing Z does NOT move the prop on screen — it only changes depth sort. (Setting the door's `sortingOrder` to -1 first did NOTHING — that was a misdiagnosis.) Note: a prop may mix queues (the door's `Door`/`Frame` are opaque 2000, its `Inside`/`Shadow` are transparent 3000); transparent parts do honor sortingOrder and always draw after all opaque.

✅ **SOLVED GENERALLY BY `PlayPlane.cs` (2026-08-08) — you should not need to hand-tune prop Z any more.** Fixing individual props only ever fixes the one somebody noticed, and the designer reported the player and enemies still rendering behind props. Measuring all 11 pool rooms showed why: **there was no play plane at all.** Every room had invented its own depth —

| room | spawn Z | enemies Z | frontmost prop Z |
|---|---|---|---|
| `efeslevel1` | 0.00 | 0.00 | **−0.01** (prop in front of every enemy) |
| `EfeVrl4` | 0.00 | 0.00 | **0.00** (exactly coplanar → arbitrary sort) |
| `EfeVrl7` | 2.00 | 2.00 | **0.00** (props in front of player *and* enemies) |
| `efeslevel3` | 2.56 | 0.00 | 3.01 |
| `hub` | −1.06 | — | −0.61 |

— because `LevelManager` copied the entry point's **full Vector3** onto the player, so the player's depth was whatever that room's `GirisNoktasi` happened to sit at, while enemies sat wherever they were dropped and props ranged from −1.12 to +3.56. Sorting was luck, per room. That "sometimes" is the signature of two opaque things at the *same* Z.

**The rule now: actors live at `PlayPlane.Z` (−2), everything else is behind it.** `PlayPlane.Apply(room)` runs on every spawn — it snaps every `EnemyHealth` onto the plane and pushes any opaque non-actor renderer found at or in front of it behind, moving the prop's top-level ancestor so multi-part props stay together. `LevelManager` now takes only X/Y from the entry point. Verified: all 11 rooms satisfy the invariant (every enemy on the plane, zero props in front), and the fix holds for rooms nobody has authored yet. **Z is free to move** — the camera is orthographic so depth changes cost zero pixels on screen, and Physics2D ignores Z entirely.

The historical per-prop fix below is now redundant but harmless; keep it as the explanation of *why* opaque sprites behave this way.

**The entry-door case is FIXED PROJECT-WIDE (2026-07-22) — at the source, in `Assets/Prefabs/GirisNoktasi.prefab`.** It was never a hub-only bug: `LevelManager` spawns the player with `playerTransform.position = entryPoint.position` (full Vector3, **Z included**), so the player always lands exactly coplanar with the entry door — and a scan found **38 of 39 rooms** with the door on or in front of the spawn plane, because every room nests this one shared prefab. Fix: the `PF Dungeon Props - Door Wood 01` child's local Z is now **0.5**, putting all four door sprites 0.45–0.51 **behind** the spawn plane. All 39 rooms inherited it from the single source change; the hub's earlier per-instance override (and 4 no-op `sortingOrder` overrides) were reverted so the hub tracks the source. **If you add a new room, do not override that door prop's local Z** — inherit it. If you ever place another prop near the entry point, remember the spawn plane is the Z the player occupies.

### "camera.Render() to a RenderTexture can sort DIFFERENTLY than the real game view"

When capturing a frame to inspect it (see Workflow Notes → Visual inspection), a throwaway `Camera.Render()` into a RenderTexture does NOT necessarily match what the URP pipeline actually draws — it gave a *false* "the door is behind the player" image while the real game still showed the door on top. **Trust only the real framebuffer** (`ScreenCapture.CaptureScreenshot(path)`), never a manual `camera.Render()`, when verifying sorting/lighting/pipeline-dependent visuals.

---

## Workflow Notes

### Two-Codex Collaboration

The user often consults a separate Codex instance (the conversational one in Codex.ai) for design discussion and prompt drafting, then sends prompts to Codex for execution. When the user references "what Codex said" or "the other Codex," that's the source. Defer to user intent when their explanation differs from a previous prompt.

### Confirmation Patterns

- Default to small, targeted changes. Refactors require explicit approval.
- When a plan changes scope mid-task ("while I'm in there..."), STOP and confirm with the user.
- For multi-file changes, show the affected file list before making edits.
- Diagnostic-only prompts ("don't fix yet, report") must be respected — never make changes when asked to diagnose only.
- **Commit between meaningful steps.** A working state is worth checkpointing even if more work remains. The discipline of "commit per logical change" has saved the project from cascading errors multiple times.

### Language

- New code comments: **English**.
- Older code comments: often Turkish — leave alone unless misleading.
- User communicates in English now (was Turkish in earlier sessions).

### Don't Save Before Discarding

If the user is about to discard uncommitted Unity changes via GitHub Desktop, **Unity should be closed first with "Don't Save"** on the unsaved-changes prompt. Saving the broken state right before throwing it away is pointless and can interfere with the discard.

### Prefab override auditor (2026-07-22) — run this when something "should work but doesn't"

`Assets/Scripts/Editor/PrefabOverrideAuditor.cs`, menu **Deckshift → Audit Prefab Overrides**.
Scans the active scene + every prefab asset (~2,000 prefab instances, ~9s) and reports **prefab-instance
overrides that have silently diverged from their source prefab** — this project's most recurrent
invisible bug class. Two categories, both deliberately high-signal (it finds ~1 hit in 31,000 overrides):

- **NULLED** — the instance blanks an object reference the source prefab HAS. Almost always a bug,
  and a nasty one: the prefab looks correct, so you debug the code instead. It further distinguishes
  *"reference cleared"* (revert the property) from *"the instance DELETED the object it pointed at"*
  (revert won't help — restore the child or remove the leftover).
- **PINNED** — an override that merely repeats the source's CURRENT value. Harmless today, but the
  instance is frozen and will not follow future prefab edits. Restricted to **our own scripts**:
  Unity's built-ins (especially `RectTransform`) emit value-identical overrides constantly, which
  buried the real findings ~500:1 before the filter.

**Implementation caveat worth preserving:** the NULLED check does NOT read
`PrefabUtility.GetPropertyModifications`. That record can contain **stale entries Unity no longer
applies** — GenLevel3's AcidWater carries an `m_Materials.Array.data[0] = null` record while every
material is in fact assigned, which produced a confident false positive. The auditor instead compares
the **effective instance value** against `PrefabUtility.GetCorrespondingObjectFromSource(...)`. If you
extend this tool, keep that principle: *trust live values, not modification records.*

Verified by regression test: temporarily re-introducing the `warningSoundClip` null override made the
auditor flag it immediately. Note that restoring a value by **assigning** it creates a PINNED override —
always fix these with `PrefabUtility.RevertPropertyOverride`, not by re-typing the value.

### Screen gallery (2026-08-16) — photograph every screen at every aspect

`Assets/Scripts/Editor/ScreenGallery.cs`, menu **Deckshift → Screen Gallery**. Walks all 13 full-screen
UIs, captures each at **4:3 / 16:9 / 21:9**, and writes an HTML contact sheet to
`<project>/ScreenGallery/<timestamp>/` (gitignored). It is the **baseline** for the planned typography
and `GameScreen` work, and the **regression net** afterwards — every screen here is procedural, so
nothing else catches one that silently stopped opening, dropped out of the font system, or overflows a
narrow aspect.

⚠️ **It requires Play mode and deliberately will not start one.** Entering play mode domain-reloads and
would wipe the run's state mid-flight.

⚠️ **Waits are WALL CLOCK, never `Time.deltaTime`.** Every screen pauses the game (`timeScale = 0`) and
animates on unscaled time, so a scaled wait hangs forever on the first modal. It also shoots after a
settle delay rather than on the build frame — a screenshot taken on the frame a UI is built shows the
AUTHORED colours, not the animated ones.

⚠️ **Aspect is changed by driving the Game View's own size dropdown** (reflection into
`UnityEditor.GameView.selectedSizeIndex`), so the capture is genuinely 2560x1080 rather than a
letterboxed 16:9. That matters because the canvas matches on HEIGHT, so **width is what flexes and
width is what breaks screens**. The original size is restored on finish or abort.

⚠️ **It stages the run first** (gold, scrap, relics, a damaged card, a blessed card, an exhausted card,
a shopkeeper) because several screens deliberately collapse to one explanatory line when empty — an
empty forge is a misleading thing to photograph as "the forge". All of it is play-mode state, discarded
on Stop.

⚠️ **A screen that fails to open records the failure and the run continues.** A regression net that
aborts on the first broken screen reports one problem per run.

⚠️ **The close path is reflection onto a private `Hide()`/`Close()`.** Every screen owns its own
dismissal and none expose a public close, so the tool reaches in. `ScreenDef.DestroyOnClose` exists for
screens whose `Hide()` is not a full teardown. **If a shared screen base class ever lands, this whole
section collapses into one virtual call** — that is the strongest argument for building it.

**A leaking screen is the failure mode to fear, so it is checked explicitly.** `CameraCensus` records
every enabled camera drawing to the screen before the run and warns if a screen leaves a new one behind.
A screen that leaks something *rendering* does not fail loudly — it composites itself into every capture
that follows and the run finishes "successfully" with wrong pictures.

### Visual inspection via MCP screenshots (2026-07-18)

Codex CAN see the running game — this is the fix for "I can't judge how it looks." The reliable recipe (via `execute_code`):
1. Enter Play mode (`manage_editor play`) so levels/entities actually spawn; edit mode is sparse (rooms instantiate at runtime).
2. `ScreenCapture.CaptureScreenshot("<abs path>")` — **async**, captures the REAL framebuffer (full URP render + all Screen-Space-Overlay UI/HUD) after the next frame renders.
3. In a LATER tool call (a frame has passed), `Read` the PNG. Reading it in the SAME call fails — the file isn't written yet.
4. Stop Play mode (`manage_editor stop`) to leave the editor clean.

Gotchas learned the hard way:
- `ScreenCapture.CaptureScreenshotAsTexture()` returns null/invalid from `execute_code` (it must run at end-of-frame, which `execute_code` can't hit). Use the async file method.
- A manual `Camera.Render()` into a RenderTexture is synchronous and handy, but **can sort differently than the real pipeline** — do NOT trust it for sorting/lighting checks (see Common Pitfalls). It's fine only for a rough world grab.
- To zoom on something (e.g. the spawn), move the REAL `Camera.main` onto the target and shrink `orthographicSize` **after disabling `CameraFollow.enabled`** (it re-clamps every LateUpdate), then use the async framebuffer capture. Play-mode changes revert on stop, so no restore needed.
- `execute_code` safety checks block `System.IO.File.Delete` and `AssetDatabase.DeleteAsset` (pass `safety_checks:false` when a delete is truly intended); `using` directives are illegal in its method body (fully-qualify types); and there are **three `Projectile` types** so component-add by short name is ambiguous (see Enemy System).

Use this liberally to verify visual changes, diagnose "it looks wrong" reports, and fact-check the docs against reality — it caught a wrong sorting fix this session before it shipped.

---

## Known Issues / Deferred Work

### Architecture (planned, highest priority)

- ~~CardActionExecutor conflict-flag enforcement~~ — **DONE (2026-07-06).** The ExecuteAction() extraction, all per-effect flag registration (incl. ReverseGravity via `SetManualFlag`), AND enforcement in `TryExecute` (Blocked on flag overlap) are complete. The card-effect-conflict bug class is resolved. Only remaining nuance: the Echo Chamber double-cast no-ops on stateful cards (see Card System → Known interaction) — flagged, not urgent.
- ~~CameraPeek rebuild~~ — **done**; rebuilt without Cinemachine (see Camera System).
- **Manager dependency graph** — undocumented. Long-term docs task.
- ~~QuestSystem DontDestroyOnLoad inconsistency~~ — **resolved 2026-06-10**: removed; QuestSystem is scene-local like every other manager, and quests are per-run by design. Quest meta-progression, if ever wanted, should go through the save system (PlayerPrefs, like AchievementManager), not DontDestroyOnLoad.

### Future: Slot-Constrained Relic Redesign (MAJOR DESIGN DIRECTION)

✅ **THE MECHANICAL REDESIGN IS DONE (corrected 2026-07-26).** This section spent months describing a "future direction" that had in fact already shipped. What actually exists now is documented under **Relic System** above: 5 slots, rarity-based sell values, `TryGrantRelic` + the forced full-slot swap screen, a manage panel, and hover tooltips. **Do not re-plan or re-build any of that.**

**What genuinely remains is BALANCE, not code:**
- **Rebalance the 19 relics for a slot economy.** They were authored as small always-on Slay-the-Spire bonuses (+5 HP on kill, +2 Shift on kill). In a 5-slot loadout where every pick costs you another relic, small passive trickles are the wrong shape — slot-constrained systems want **bigger, more interactive, more build-defining** effects that change how you play, not just numbers that tick up. This is the real outstanding work and it is a **design pass, not an engineering one**.
- **Economy tuning** — sell refunds are currently flat by rarity (150/90/50/25) and untuned against a 45-50 min run and the actual rate relics are offered.
- **Possibly** distinguish acquisition sources (shop vs. pack vs. voucher).

**Why this fits the game's DNA:** Deckshift's core philosophy is "Movement is a Resource" — resources matter. Slot-constrained relics extend that principle: a curated pool the player manages, not a pile that grows passively.

⚠️ **The old "don't invest in relic UX / don't add relics" freeze is LIFTED** — it was guarding against a rework that has now happened. Tooltips and the manage/swap UI exist. Adding relics is fine and in fact *needed* (a 5-slot system wants a deep pool to choose from); just author them at slot-worthy power, not as another +2 trickle.

**Approach for the balance pass:** paper design first, code second.

### Quest System Expansion (deferred)

- Wire `NoDamageRoom` quest type — needs an event fired from PlayerController's damage path that resets a per-room "no damage" flag; on level end, if flag is true, fire `ReportEvent(QuestType.NoDamageRoom, 1)`.
- Wire `GoldAccumulate` and `UseCardCount` quest types similarly.
- Add card-reward type. Currently only Gold/Heal/ShiftCharge are supported.
- **Rich Man's Dagger card** — a card that deals damage based on current player gold. Was discussed as a quest reward. Needs design pass: damage formula, balance against scaling gold pools, mid-fight gold loss interaction.
- Defer reward delivery to **level-end** instead of firing immediately on quest completion (see also Quest banking, below).
- ~~Randomize the offer~~ · ~~enforce the 3-quest cap~~ · ~~visual feedback on accept~~ — **all done 2026-08-10** with the board rebuild.
- **AUTHOR MORE QUESTS.** Only 4 assets exist, one of them (`Scrooge`) is unfinished — it pays `rewardAmount` 0 and isn't in `allQuests`. The board is built to offer more contracts than you can carry, which is what makes taking one a decision; with three assets it can't. This is now the quest system's binding constraint, not the UI.
- Wire the "press E" prompt GameObject on the QuestBoard's `SimpleInteract.prompt` field (currently null — no hover hint appears).

### Scene Flow (deferred)

- Player should start in hub from main menu, transition to run levels, and return to hub after death/run completion.
- Currently a hack: hub is `LevelManager.roomPrefabs[0]` and first-room logic forces it. Works for testing/demo but isn't proper scene flow.
- When implemented: review every manager for `DontDestroyOnLoad` needs. Most currently lack it; that becomes a real concern with scene transitions.

### Bugs (deferred)

- ~~Card effect conflict class of bug~~ — **RESOLVED (2026-07-06).** `TryExecute` now refuses (Blocked) any card whose `ModifiedState` overlaps a live effect's flags; blocked plays cost nothing and stay in hand. Stacking Floor is Lava + Adrenaline + Phase can no longer corrupt player state. See Card System for detail.
- ~~**Phase card wall-stuck**~~ — **RESOLVED 2026-08-11** (it recurred; the designer got stuck inside rock and could not move). `PhaseRoutine` still extends Phase up to 1 extra second while embedded, but the old fallback — nudge 0.5 units along the gravity axis and hope — is replaced by `EjectFromGeometry()`: a **ring search outward for a position the capsule actually FITS in**, nearest first, directions ordered from straight-up outward so the player surfaces on top of geometry. Falls back to the room entry point if nothing is free within 7 units, so a run can never be lost to this. Velocity is zeroed on eject, or a fast downward fall tunnels straight back in.
  - **Measured:** 368 of 368 stuck positions across a room recovered, zero failures; deepest burial found was **6 units**, against the old fix's 0.5 — so the old nudge was failing on nearly every real case.
  - ⚠️ **AND THE REASON THE FIRST ATTEMPT SILENTLY FAILED IS WORTH KEEPING:** it tested candidates using `capsuleCollider.bounds`. **`Physics2D.autoSyncTransforms` is OFF by default**, so a collider's `bounds` still report the player's PREVIOUS position until the next physics step — and this code tests positions the player hasn't moved to yet, then moves and re-checks. The search "found" a clear spot, teleported there, and left the player just as embedded. Both `IsPositionClear` and `IsCollidingWithGround` now derive the box from `transform.position + capsuleCollider.offset` (exact, because the player root is guaranteed scale (1,1,1)). **Never read a collider's `bounds` in the same frame you moved its transform.**
- ~~**Comet Dive identity loss**~~ — **RESOLVED (verified 2026-07-26).** Comet Dive was redesigned into an AoE **dive-blast** (`StartCometDive`/`LandCometDive`: fast downward slam → `Physics2D.OverlapCircleAll` damage at `cometRadius`/`cometDamage`, with a `CometDiveVFX` telegraph while falling). It is no longer the single-target head-bounce; the two are distinct.
- ~~**Head bounce + gravity reversal**~~ — **RESOLVED (verified 2026-07-26).** All head-bounce branches now flip on `isGravityReversed` (see Head Bounce section). Head-bouncing works upside-down.
- ~~**Duplicate ExitDoor possible in some room prefabs**~~ — **RESOLVED 2026-08-19.** It was never "some rooms": the duplicate was baked into `Assets/Prefabs/ExitDoor.prefab` itself, so **37 of 39 rooms had it**, and one keypress ran `PerformExit()` twice. See Level System → Doors.

### ⚠️ Runtime spawns must not outlive their room (fixed 2026-08-11)

**`Destroy(currentRoom)` only destroys what is PARENTED UNDER IT.** Anything created at runtime with `Instantiate(prefab)` and no parent becomes a **scene-root** object and simply survives the room change — it then turns up in the next room, and in the hub. The designer reported this twice: enemy health bars floating in later rooms, and the Moss Knight's summoned slimes following the player into the hub when the fight was abandoned.

`TemporaryObject` was the existing answer and is **the wrong shape of answer on its own**: it only cleans up objects whose author remembered to stamp them, so every future runtime spawn is one forgotten line away from the same bug. `LevelManager.ClearRuntimeSpawns()` now also sweeps by **TYPE** — every `EnemyHealth`, `EnemyHealthBar` and `Projectile` — which covers the spawns nobody remembered, including ones not written yet. Sweeping every enemy is safe because the room is destroyed on the next line; note they are *destroyed*, not killed, so `Die()` never runs and leaving a room pays no scrap and completes no bounty.

**`EnemyHealthBar` also owns its own lifetime** (`followTarget == null` → self-destruct), which is the more important half: the bar is parentless by design, and that one line covers every way an enemy can vanish, including paths that don't exist yet. It's guarded by an `initialized` flag because `Initialize()` arrives a beat after instantiation.

⚠️ **Testing this needs a frame boundary.** Unity's `Destroy` is deferred to end of frame, so `FindObjectsByType` in the *same* `execute_code` call still returns everything you just destroyed — it reads as a total failure of the fix. Check in the NEXT tool call. Same family as the deferred-`Destroy` trap already documented for `RunMapScreen`'s buttons.
- **AnimationEventReceiver may resurrect on prefab reimport.** It is now fully REMOVED from the Mage M Animator child (was previously just disabled). If OnFootstep NullRefs reappear in the console, a pack reimport probably restored it — remove it again. (The "'OnFootstep' has no receiver!" *warning* spam is absorbed by `PlayerAnimEventSink` on that same GameObject, now serialized in Player.prefab; see Visual Model Internals.)
- ~~**Gravity reversal warning flash may be invisible**~~ — **RESOLVED (screenshot-verified 2026-07-26).** `WarningFlashRoutine` now strobes `_Alpha` across all 16 SkinnedMeshRenderers (whole-body blink) + red-tints the staff. The prior versions no-op'd (`_Color` unsupported by the Alpha Cut shader) or flashed only the staff. See Gravity Reversal System.

### Resolved bugs (verified by code audit 2026-06-10 — do NOT re-fix)

These were previously listed as open in this file; the audit (`audit_report.md`) confirmed they are already fixed in code:

- ✅ **Shield-block damage leak** — `EnemyHealth.TakeDamage` checks the shield BEFORE deducting health; blocked hits no longer lose HP.
- ✅ **Spike knockback always sends right-up** — `Spike.cs` now reflects incoming velocity off `transform.up` with a minimum-force floor (correct for floor, wall, and ceiling spikes).
- ✅ **Fall damage** — removed entirely; `FallAndRespawn` teleports to the room entry point and fires `OnFallRespawn`, no damage is applied.
- ✅ **CameraPeek** — fully rebuilt without Cinemachine as a `peekOffset` consumed by `CameraFollow.LateUpdate` (see Camera System).

### CardTemplate prefab rebuild (BLOCKED on art)

The `CardTemplate` prefab has fundamental scale corruption: root scale is non-uniform (0.119, 0.568, 0.92) and ShiftCostContainer compensates with inverse scale (7.40, 1.55, 0.96). On-screen layout works only because the scales partially cancel; any position/spacing change looks broken because the cancellation is non-uniform.

**Measurements taken from a 1024×1536 sample card art** (deckshift_card_03):
- Shift slot painted centers: PNG pixels (411, 138), (511, 138), (610, 138) — 99px horizontal spacing.
- Charge slot painted center: PNG pixels (245, 150) — slightly lower and left.
- Honest Point Spacing in a 120×180 card rect: ~11.72 units (current Inspector value is 20, also wrong).

**Plan:** rebuild from scratch with all scales at (1, 1, 1), Width 120 / Height 180, all positioning via RectTransform Width/Height/Position only. **Blocked: user is hiring an artist for new card art. Not all current cards are the same exact size. Rebuilding now means rebuilding again once consistent art is back.** Hold until then.

### Resolved this session (Player prefab audit, 2026-07-16)

(Kept for short-term reference; can be deleted once stale.)
- ✅ **Scene→prefab tuning drift eliminated.** The scene Player carried 12 uncommitted PlayerController overrides (moveSpeed 8 vs prefab's stale 5, jump 11 vs 10, run pose, real jump SFX, aura VFX, dash tint). All applied into `Player.prefab`; the prefab is now the source of truth. The only scene overrides left are root position/rotation + name (correct). **Rule going forward: tune the PREFAB (or apply overrides after tuning in scene), never leave player tuning scene-only.**
- ✅ Removed three leftover Cainos bone colliders (Rig Spine1/Spine2 capsules, Rig Head circle) — solid, animation-driven, attached to the player's Rigidbody2D. The root capsule is now the only solid player collider.
- ✅ Removed the magic staff's Kinematic Rigidbody2D + trigger PolygonCollider2D (Cainos leftovers; its `Weapon` script is a passive visual helper and stays).
- ✅ Restored `PlayerAnimEventSink` + `footstepClips` (3 Walk mp3s) — this time saved into the prefab, not scene-only (the old wiring had never been committed and was silently lost, breaking footstep SFX and re-triggering "no receiver" spam).
- ✅ Removed duplicate `CameraPeek` from the Player root (lives on Main Camera only).
- ✅ Assigned `warningSoundClip` (breaker-switch SFX, designer may swap) — the gravity-reversal warning had become fully silent.
- ✅ AudioSource `playOnAwake` disabled; prefab root transform reset to identity; 17 stale skeleton-receiver overrides cleaned from the scene instance.

### Started but not finished (2026-08-21) — read before picking a thread

- **Live relic readouts.** The designer asked for a "currently +14 damage" line on every relic that
  has a moving number. The DATA exists (`EstateSaleProgress`, `StandInTarget`, `nestEggRoomsBanked`,
  `AceUpTheSleeveReady`, `Stacks`, `MatchedPairs`); nothing renders it yet. ⚠️ It must be a SECOND
  LINE, never a `+{X}` hole in the description — `RelicSwapScreen`, chests and the shop all show
  relics the player does not own, where there is no value to substitute.
- **Cards cannot be priced by rarity** because `CardData` has no rarity field — it exists only as
  paint on the artwork. `ShopPricing.ForCard` prices off charges and Shift cost meanwhile. Adding
  the field also unblocks icon-only card faces (below).
- **The card-art unblock (designer's constraint, 2026-08-21):** new card art is stalled on an
  unavailable artist, and that is blocking card DESIGN, which does not need art. The intended fix is
  a systematic icon-only face using the Cainos RPG icon pack on the canonical Freefall Blade frame
  (whose name plate is already drawn in code), so cards can be built and balanced now and
  illustrated later, one field each.
- **Relic reordering is unbuilt**, so Stand-In works but the player cannot aim it.
- **`Known Issues / Deferred Work` carries ~10-15KB of RESOLVED entries** with no transferable
  lesson. Deleting those is worth doing, but per-entry: several resolved items carry the most
  expensive ⚠️ in the file (the `Physics2D.autoSyncTransforms` one especially). Keep every trap.

### Content (TODO)

- Scale to 60+ cards (currently **18 assets in `Assets/Cards/`, 16 genuinely playable** — `Stagger` is the fail-state card, `AnaKartVeritabanı` is the database asset). **This is the single biggest content gap and it gates both the map system and card enhancements.** The two archetypes the GDD names are the thinnest lines in the deck: **Glass has 2 cards** (Glass Wail, Glass Parry) and **Vampiric has 1** (Vampiric Bite), against 6 movement / 4 attack / 3 utility.
- Glass archetype: cards exist in theory, not implemented.
- Expand Vampiric archetype.
- ⚠️ **ACTS ARE GONE (designer, 2026-08-21). Do not plan around them.** A run is now ONE map of
  **20 floors** with **2–5 OPTIONAL boss nodes** the player routes into or around, ending at a
  single unique **FinalBoss**. `MapNodeType.Boss` is a mid-map node standing in a column like any
  other; `MapNodeType.FinalBoss` is the terminus. Every optional boss is *proven* avoidable
  (`RunMap.IsAvoidable`, enforced in `Validate`). A boss REPLACES a floor rather than adding one, so
  run length stays put while intensity and reward vary. **Content gap: 1 boss room exists of ~10
  wanted, and the unique finale is not built.**
- ~~**Run map system**~~ — **BUILT 2026-08-06, working end to end.** See "Run Map — BUILT AND WORKING END TO END" under Level System for the implementation and its traps. What remains is CONTENT and TUNING, not engineering: the three recharge room prefabs (Foundry / Market / Well) don't exist, so no recharge rooms appear yet; rooms are untagged so every room still serves every tier; and the shift-infused / buffed-enemy half of Elite tiers is not built. The settled design, kept for reference:
  - **Shape: a Slay-the-Spire branching graph, the whole RUN visible**, so the player plans a route rather than picking one door at a time. **Opened with the `M` key** — meaning it's also viewable in the hub, for quest planning. ⚠️ At 20 floors the chart no longer fits the sheet: it has a fixed floor pitch and **scrolls** inside a `RectMask2D` viewport (wheel / drag / held W-S), opening centred on the player.
  - **Difficulty IS the node type, not a second axis on top of it.** Three combat nodes — **Skirmish / Fight / Elite** — ascending cost and reward. Layering easy/med/hard *onto* Fight/Shop/Event would give ~15 icon combinations and an unreadable map; one node = one icon = one promise.
  - **Per-tier content rules (designer-specified):**
    - **Skirmish** — simple layouts, low-HP enemies, thin loot. At most 1 chest. **No shop, no Blompo, no NPCs at all.** Gold and Shift crystals scaled to how much the layout drains.
    - **Fight** — harder layout, mid-tier enemies with some fodder. **At least 1 chest.** Shop appears sometimes; Blompo rarely (shop more common than Blompo).
    - **Elite** — genuinely uncomfortable to pick. Hardest layouts. Some enemies carry **more HP than the same enemy in a Fight room**, plus **shift-infused enemies** (faster, hit harder, drop Shift on death).
  - **The governing law: a room's loot scales to the Shift it costs to cross.** Drainy layout ⇒ bigger payout. Self-balancing; write new rooms to it.
  - **Two axes, two sources:** platforming difficulty is **authored into the room prefab** (geometry can't change at runtime without violating Level Design Law #1); combat difficulty is a **runtime spawn table**. Cheapest extra lever: author *optional* enemy/hazard groups in a prefab and have the tier switch them on.
  - **No Shift cost on map paths** (decided against, for now). It isn't needed: **the danger is the cost.** Skirmish routes are cheap to survive but never resupply; Elite routes are expensive but are the only path to recharge rooms. That loop is the economy.
  - **Recharge rooms** — extra rooms hanging off a route, **not counted as floors**, and **only ever reachable from Fight/Elite nodes, never Skirmish** (that restriction IS the economy above). **Each is specialised, never a do-everything room** — one room that fixes every problem is never a decision. Design them by *which player problem they solve*: a Foundry (scrap → repair/salvage, Blompo), a Market (shop), a Well (Shift + healing). **The map must show which one is on which branch before the player commits**, or it's a coin flip instead of a choice.
  - ⚠️ **Two things that must be visible, not silent:** (1) if an enemy is buffed, **it must LOOK different** — a Shambler that quietly has 20 HP instead of 12 reads to the player as "my Fireball is broken", and corrodes the `CardAnchors.md` anchor that fodder dies to one Fireball. (2) The single most sensitive number in the system is how much Shift a shift-infused enemy drops: too generous and Elite is always correct, too stingy and it's never taken. **Target: an Elite room should be net-negative Shift for an average player and net-positive only for a good one.** Keep it a single tunable value, not baked across prefabs.
  - **Dependency status:** the old "BLOCKED on level count" framing is softer than it looked. Shop/Blompo/quest board are **NPCs placed in rooms**, not dedicated room prefabs, so those node types are near-free. ~15 contract-valid rooms already exist unused (see Room Pool) and need correction passes, not authoring from scratch. Still, tiers are baked into layout, so each room serves ONE tier — roughly 4 Skirmish / 4 Fight / 3 Elite are needed for one repeat-free act.

- **Quest banking — designed 2026-08-03, not built.** Quest rewards should stop paying out instantly and instead **accumulate**, to be collected at a quest board **at the start of the next act** (post-boss). Quests are taken at run start, so they act as *route-shaping objectives* — "kill 3 elites" pushes you onto dangerous paths, "collect 500 gold" into exploration detours. The existing run loop already does this shape (`LevelManager` goes hub → levels → boss → back to hub, and the hub already has the board), so the structural work is small. **The board does NOT need its own map node yet** — only four quest assets exist (one pays zero), which is too thin to carry a node; put it inside the Market or Well for now. When the map exists, show it *while* the player picks quests, so quest selection isn't a blind bet.
- ~~Card enhancements via "Blompo"~~ — **BUILT. 24 blessings as of 2026-08-14** (see Card System → Card Enhancements). This entry described it as "NOT started" for weeks after it shipped with seven; do not plan from that.
- **Bosses: ~10 wanted, 1 built.** The Moss Knight is a complete encounter (moveset, gated fight
  start, awaken cinematic, SFX, boss health bar, a death celebration dropping real collectible gold
  and shift crystals, and now the reward banner). Full doc: `BossDesign_MossKnight.md`; still open
  there is the acid arena.
  - ⚠️ **`LevelManager.bossRoomPrefab` NO LONGER EXISTS.** It is `bossRoomPrefabs` (a List, drawn
    without repeating within a run) plus a separate `finalBossRoomPrefab`.
  - ⚠️ **A boss relic does NOT imply a keybind.** `Rarity.Boss` is an acquisition channel;
    `RelicData.isArt` marks the few that bind a key, and `RelicPool` refuses to offer a second Art
    while one is held — so the game is capped at ONE extra key however many bosses are killed. With
    ~10 bosses that means roughly **seven of the ten relics must be passives at boss power**, which
    is a different design job from the three verb-shaped ones already built.
- Chunk-based level system (currently hand-crafted levels).
- **Starting relic system** + **Fireball relic** for the wizard identity (auto-fires fireball every 10s). Deferred when the broader relic redesign was prioritized — may be revisited as a small early demo polish.

### Replace SlotMachine with "Dice Broker"

A character-driven gambling NPC. ⚠️ The slot machine it was meant to replace is now DELETED, so this is a from-scratch build, not a reskin. Same intended outcome (random relic from a dice roll):
- A grimy character (sprite needed) who shakes a dice cup
- Should route through `RelicManager.TryGrantRelic` (RewardManager is orphaned; see Manager Layer)
- Implementation note: **roll the result in code first, then play an animation that ends on the correct face**. Don't depend on physics simulation.
- Dice animation: sprite-sheet of 6-12 tumble frames ending on each face (cheaper and more readable than physics dice).
- Voice/banter potential — give the broker personality.

### Documentation Tasks

- Eventually: a proper GDD (Game Design Document). Currently the design is fluid enough that a GDD would be obsolete fast. Worth doing once: the relic system is finalized, the act structure is locked, the card list is more complete.

---

## File / Path Reference

- Active scene: `Assets/Scenes/SampleScene.unity`
- Player prefab: `Assets/Prefabs/Player.prefab`
- Scripts: `Assets/Scripts/` (75+ files, flat structure)
- Level prefabs: `Assets/LevelSinasi/*.prefab` and `Assets/LevelEfeS/*.prefab` (hub)
- Quest assets: `Assets/Quests/`
- Relic assets: `Assets/Relics/`
- Card assets: `Assets/Cards/`
- Hub prefab: `Assets/LevelEfeS/hub.prefab`
- Customizable Pixel Character pack: `Assets/Cainos/Customizable Pixel Character/`
- Icon pack (relic art, and the answer to "we have no art"): `Assets/Cainos/Pixel Art Icon Pack - RPG/`

### The five loadable skills

⚠️ **This file is the always-on summary. The skills hold the detail and load ONLY when invoked**,
which is the whole point — a session about audio should not pay for the enemy AI history. When you
learn something new about one of these areas, write it into the SKILL, not back into here, or this
file re-grows and the split buys nothing. It has already happened once: AGENTS.md was split in
August, re-grew to 194KB, and had to be split again.

| skill | invoke before |
|---|---|
| `/deckshift-ui` | building, restyling or debugging ANY screen, HUD element, card face or UI VFX |
| `/deckshift-screens` | touching a NAMED existing screen — the catalogue of what each is made of |
| `/deckshift-levels` | authoring, importing or validating a room; `LevelManager`, tiles, gates, the run map |
| `/deckshift-enemies` | any enemy prefab or AI, `EnemyHealth`/`EnemyMelee`/`EnemySenses`, a boss |
| `/deckshift-economy` | scrap, gold, the forge, quests, oaths, relics, shop pricing |

---

## When in Doubt

- Ask the user for clarification before making sweeping changes.
- Verify scene presence of components before assuming code is broken.
- Read related scripts before refactoring shared systems.
- For visual/UI work, confirm the canvas hierarchy and parenting before moving GameObjects.
- For Animator parameter types, read the `.controller` YAML directly (m_Type integer).
- The user wants quality over speed. "Make this one of the greats" is the stated goal — push back gently on quick-fix patterns when a slightly larger correct fix is appropriate.
