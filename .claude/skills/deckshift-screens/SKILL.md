---
name: deckshift-screens
description: Catalogue of every screen, panel and HUD element that already EXISTS in Deckshift — what each one is made of, how it is wired, and the specific traps each one paid for. Use only when touching, debugging or restyling a NAMED existing screen; the method for building any screen is in deckshift-ui.
---

# Deckshift — the screens that exist

Split out of `deckshift-ui` 2026-08-21 because it was **63% of that skill** (75KB of
120KB) and is REFERENCE rather than METHOD. Invoking the UI skill to learn how to
build a screen was dragging the entire per-screen history along with it, on every
UI session.

**The method lives in `/deckshift-ui`.** This file is the catalogue: skim for the
screen you are about to touch, and do not read it end to end.


Moved here from CLAUDE.md 2026-08-20, where it was costing ~10k tokens on every session
including ones that never touched a pixel. §1–7 above are the *method*; this part is the
*catalogue* — what is already built, what each screen is made of, and the traps each one
paid for. Skim for the screen you are about to touch.


### Canvas Hierarchy

SampleScene's main Canvas contains:
- **`GameplayHUD`** — contains all in-game HUD elements (gold, health, shift counter, recall button, deck/discard/exhaust pile buttons, hand drawer trigger zone, **RelicHUD**, **QuestTracker**). Toggle with `SetActive(false)` to hide HUD during full-screen UI.
- Various menu panels (ShopUI, TutorialPanel, etc.) as direct children of Canvas. **Procedural screens (`PauseScreen`, `RunMapScreen`, `ScrapForgeScreen`, `BlompoScreen`, `QuestBoardScreen`, `SettingsScreen`…) create themselves under this Canvas at runtime and are NOT in the scene file** — do not go looking for them in the hierarchy at edit time. (`QuestBoardOverlay` and both `SettingsPanel`s were deleted; only `TutorialPanel` remains as a scene-placed panel.)

**When adding new full-screen UI panels**, hide GameplayHUD when they open by adding a `[SerializeField] GameObject gameplayHUD;` reference and toggling SetActive. ShopManager and QuestBoardScreen already follow this pattern.

### `FlatUI.cs` — the new UI direction (2026-08-03)

**The designer has disliked the ornate stone-and-gold chrome "since the beginning."** `FlatUI.cs` is the replacement, prototyped on the Scrap Forge screen.

**It took two passes, and the first one's failure is the useful part.** Pass 1 delivered the literal brief ("soothing, simple, understandable, but also cool") as flat slate-blue panels, uniform rounded corners, neutral greys, one accent. The designer's verdict: **"it screams AI."** That was right — it was the house style of every dev dashboard, and crucially it had no *place* in it. Simple and generic are not the same thing.

**Pass 2 keeps the restraint but points every choice at the world: a sheet of iron on a workbench, lit by the forge.**
- **Warm charcoal, not slate-blue.** Act 1 is the *Oxidation District* — rust, not brushed steel. This single palette shift did most of the work.
- **Chamfered corners, not rounded.** Cut plate reads as a made object; a uniform corner radius reads as a web card. Biggest silhouette cue.
- **Directional light.** A lit top lip plus an ember glow rising off the *bottom* edge (firelight under the bench), instead of a uniform glowing border. Uneven light = physical object in a place.
- **Rivets and faint scuffs.** Small, dark, functional — fasteners, not jewels. Imperfection is what kills the "generated" feel.
- **Rules score across and fade at the ends** rather than running edge to edge like a CSS border.
- **The only two colours on screen are the game's own two resources:** charges in Shift-blue, costs in scrap-orange.

API: `Panel(chamfer)` / `Outline(chamfer, thickness)` (9-sliced chamfered plates), `Rivet()`, `FadedRule()`, `SoftGlow()`, `BottomGlow()`, `VerticalFade()`, `EmberDot()`, `Pixel()`. **All shapes are WHITE and tinted via `Image.color`**, so one cached sprite serves every panel. Shared palette at the bottom of the file.

**`UIEmberField.cs`** — drifting embers for a panel background (`UIEmberField.Attach(rect, count, colour)`); builds and animates its own Image dots, no particle system. Two things that would break it: it must use **`Time.unscaledDeltaTime`** (every screen it belongs on pauses the game, so scaled time freezes the embers solid), and it must **re-read the parent rect every frame** (the forge window's height is dynamic, so a bounds snapshot would leave embers outside a collapsed panel).

Lessons already paid for, don't re-learn them:
- **Get the SDF right.** Rounded box is `inside + outside - radius`; the chamfer is that box distance `max`'d with a normalised diagonal half-plane. Naive versions pinch the outline at corners.
- Textures need `FilterMode.Bilinear` — Point aliases the chamfer edges badly.
- **Hairlines need to be brighter than theory says**, or they don't register on a dark surface.
- **Atmosphere effects want roughly half the alpha you first reach for.** The ember at 0.085/140px was an orange wash owning the bottom third; ~0.05 over 120px is firelight. Scuffs at 0.045 read as *rendering glitches*; 0.022 reads as wear.
- ⚠️ **A glow that doesn't reach its container's edge must fade on that axis too, or it draws its own border.** The bottom glow originally reused `VerticalFade` (which only falls off in Y) inset 14px from the window sides — the sprite's hard left/right ends produced a visible vertical seam down BOTH edges of the panel. That's what `BottomGlow()` exists for: falloff in both axes.
- **Keep wear out of content columns.** The first scuff pass ran a streak straight through the title. They belong in margins that are empty at any content count.
- **Small icons inside dense text don't work.** A 17px scrap shard beside each cost read as a smudge fused to the first digit; the accent colour alone carries it.
- **An emblem needs STRUCTURE, or it reads as a lens flare.** Blompo's offer marks were a plain four-point sparkle behind a big soft glow and looked cheap. `ArcaneSigil` fixed it with a containing ring, rays of two lengths, and ticks outside the ring — plus a much tighter, dimmer glow, since the haze was doing most of the damage.
- **Detail placed exactly on another element disappears.** `ArcaneSeal`'s four diamond glyphs originally sat at the inner ring's radius and merged into it invisibly; they now punctuate the outer ring on the diagonals, clear of the twelve ticks.
- **Show the numbers a decision depends on.** Blompo's card-pick step listed only a bare charge count — no Shift cost, no maximum — so you chose which card to permanently alter without seeing what it cost or how much life it had. Chips now carry labelled SHIFT / CHARGES stats, and `StampChip` refreshes *both* on the bind frame because several blessings visibly change them.
- **Empty states must collapse.** `LayoutSections` lays the screen out top-down and resizes the window to its content, so an empty section shrinks to one explanatory line. The fixed-height version had two large voids and looked broken — and that state is *common*, since early in a run nothing is damaged or exhausted.

### ⚠️ DESIGNER NOTE: the menu screens don't feel like the WORLD yet (2026-08-17)

> **ANSWERED 2026-08-20 by SALVAGE (§1).** This note called it three days before the cause was
> found, and it named the two screens — pause and settings — that then failed twice more before the
> RULE, rather than either screen, turned out to be the problem. **`PauseScreen` is converted; every
> other screen here is still the thing this note is complaining about.** Keep the note until the
> migration is done; it is the standing brief for the rest of it.

**Standing feedback, not a bug.** On accepting Marquee the designer said it is
"a better screen, doesn't really fit the theme of the game and the world", and named **the pause
menu and the settings menu** as feeling the same way — "they are fine for now, I would like to
change them in the future for sure".

Why this is worth recording rather than fixing on the spot: **the three named screens are the three
that depict no place in the game.** Iron is a workbench you stand at, Bulletin is a board in the hub,
Cartograph is a document you carry, the Marketplace is a stall with a person in it — all of them
borrow their material from something the player has actually seen. Halt, Apparatus and Marquee are
abstractions (a moment, a control panel, a billing), so each had to invent its material from nothing,
and inventing is exactly where "competent but generic" creeps back in — the failure the FlatUI pass
was created to kill.

**Do not start a redesign of these unprompted**, and do not treat their themes as settled either.
When it is picked up, the lead to follow is the one the run map already proved: **a material is not
enough; ask what the thing has been THROUGH.** The map stopped reading as a diagram only when it
became a document that had been folded, carried and scribbled on.

### Themes — same ideology, never the same skin (2026-08-03)

**Screens must NOT all look alike.** Designer's rule: share the ideology (flat procedural plates, restraint, directional light, a subtle particle drift, one meaningful accent), but each place gets its own material, and **the material should say what the place DOES**.

`FlatUI.Theme` is the mechanism — a colour set (`Surface`, `Border`, `EdgeLight`, `Accent`, text ramp) picked per screen:

| | **Iron** (`ScrapForgeScreen`) | **Arcane** (`BlompoScreen`) | **Loadout** (`RelicHUD`, `RelicIcon`, `RelicTooltip`) | **Halt** (`PauseScreen`) | **Apparatus** (`SettingsScreen`) | **Bulletin** (`QuestBoardScreen`) |
|---|---|---|---|---|---|---|
| What it is | a workbench you repair cards at | a mythic creature granting a blessing | what you're **carrying** | the **moment** you stopped | the **machine's own control panel** | a board of **contracts** you promise to do |
| Palette | warm charcoal (rust district) | cold indigo | near-**colourless** | cold blue-black (frost) | smoked glass + arc-**cyan** | dark wood + **pale paper** + wax red |
| Light | fire from **below** | descends from **above** | none — it's not a place | from the **edges inward** | **emitted by the content itself** | **rakes in from the LEFT** |
| Particles | embers **rising**, fast | motes **settling**, slow, twinkling | none | **suspended**, shivering in place | none — one **scan sweep** instead | none — **the content itself sways** |
| Corner marks | **rivets** (fasteners) | **four-point stars** (light) | none | none — it has no corners | **calibration crosshairs** | **brass tacks**, on the content not the frame |
| Surface | scuffed and worn | pristine | plain, recessed sockets | **crazed** (hairline fractures) | unblemished glass | **perforated** (old pin holes) |

**The inversions are the point.** Warm/cold, below/above, rising/falling, worn/clean, still/moving, and — with Apparatus — inside/outside the fiction. When adding a screen, pick a material and invert something — **do not just retint Iron**.

⚠️ **Bulletin proves the strongest available inversion is VALUE, not hue.** Every other screen is a dark plate with light text on it; the quest board is a dark board with **pale paper pinned to it**, so its text ramp is INK (`TextBright` is nearly black) and the bright/dark areas have swapped places. That single structural choice makes it unmistakable at a glance while claiming almost no colour. Its wear is also the only wear in the game that says something about the **world** (other people took contracts here) rather than about the object. **Reach for this before reaching for another hue.**

### `ExitMarker` — chalk on the wall, pointing at the way out (2026-08-20)

**`Assets/Scripts/ExitMarker.cs`.** The generated rooms are ~2.5× the area of the hand-made ones and
Level Design Law 7 deliberately puts the exit in a different region from the spawn, so the exit is
usually off screen with nothing saying which way. The designer asked for "an arrow pointing towards it
so the player knows where the level ends".

**The material is CHALK ON STONE** — a wayfinding mark somebody scratched on the wall. It is drawn
with `Parchment`'s pen (the same hand that annotates the run map) but with **the ground inverted**:
Cartograph is dark ink on pale paper, this is pale chalk on dark rock.

⚠️ **That inversion is a VALUE one, not a hue one, and it is why this costs nothing from the nearly
spent hue budget.** It claims no colour at all — which is also the correct weight for something
sitting over gameplay permanently, the same reason the relic bar is near-colourless.

⚠️ **NOT RED, even though the map's annotations are.** On paper oxblood reads as *pen*; over gameplay
red is already **damage** (health bar, damage numbers, hurt flashes), so a red arrow at the screen edge
reads as "you are being hurt". Same lesson as the card's last-charge warning that could not be red
because it sat on a red medallion: **pick a status colour against what it will appear on and mean.**

**It shows an arrow riding the inset frame while the door is off screen, and nothing at all once the
door is in view** — once you can see the archway there is nothing left to say.

⚠️ **IT ALSO SETTLES BACK, AND FAR FURTHER THAN FEELS RIGHT.** After **3s** in a room the arrow eases
from alpha 0.92 down to **0.15** over 1.6s and stays there; a new room restarts the clock. The first
pass at 5s / 0.42 was corrected by the designer to *"way more transparent, almost kind of invisible"*
and *"5 seconds might be a little bit too long"*. **Verified on real dungeon art, not against black**
— at 0.15 it is present if you look for it and completely ignorable if you are not, which is the
target. The nudge motion is doing most of the work of keeping it findable at that value, so do not
remove the motion to "tidy" the settled state.

**This is the general shape for any persistent guidance mark: loud while it is teaching, nearly gone
once it is only confirming.** The instinct is to settle at something still comfortably readable;
the designer's correction says go lower than that.

⚠️ **A CHALK RING THAT CIRCLED THE DOOR ON FIRST SIGHT WAS BUILT AND CUT** (designer, 2026-08-20):
*"too basic … i don't think it's even a good idea to have them at all"*, delivered with a general
instruction to **be more creative with animations and effects**. Do not re-propose it. The lesson
generalises past this one mark: **a shape that simply appears around a thing is the most obvious
effect available**, and reaching for it is a failure of imagination rather than a design. Expanding
rings, pulsing outlines and pop-in glows are all the same reflex. If something must say "look here",
the answer has to come from what the game already MEANS — the way the gate's seal became Shift-cyan
because Shift is literally what opens it — not from a shape drawn around it.

- ⚠️ Bootstraps through **`SceneBootstrap.Register`**, never a bare `RuntimeInitializeOnLoadMethod`.
- Parented under **`GameplayHUD`**, so it inherits the HUD auto-hide for free.
- ⚠️ **The on/off-screen test uses `WorldToViewportPoint`, NOT `screenPoint / Screen.width`.**
  `Screen` reports the Game View *window* rather than the render target for at least a frame after a
  resolution change — measured **2269×334 while the canvas was correctly 1440×1080** — so dividing by
  it can be a whole aspect ratio out.
- **Hysteresis** on that test (0.10 to notice, 0.02 to lose), or it flickers while the player walks
  along the boundary, which is exactly where they spend their time.
- The exit is **re-found whenever the cached one dies with its room** — derived, never pushed at it, so
  a room spawned by any path is picked up with no wiring in `LevelManager`.
- The one piece of motion is a slow nudge **along the pointing direction**, not a pulse: drift in the
  direction of travel says "that way", a pulse only says "look at me".
- Arrow geometry is **fractions of `ArrowLen`**, so resizing keeps the barbs on the head.

Verified by screenshot at 4:3, 16:9 and 21:9. **Still open:** no `GameSettings` toggle — if it should
be switchable off, that is a row in `SettingsScreen` plus a consumer in `LateUpdate`.

### Cartograph — the run map, rebuilt on paper (2026-08-14)

⚠️ **THE MAP TOOK THREE ATTEMPTS AND THE TWO FAILURES ARE THE LESSON.** It was a flat slate panel, then an acid-etched copper plate. Both were given a MATERIAL, both were carefully lit, and the designer rejected both as still reading like a diagram. **A material is not enough.** A map feels like a map because it is a **DOCUMENT** — printed, folded, carried, then scribbled on. Four things carry that, and stripping any one slides it back to a node graph:

1. **PAPER, NOT A PANEL.** The sheet IS the window — no frame, and its edge is a torn deckle rather than a chamfer. Every other screen is a plate you look AT; this is an object you're holding.
2. **FOLDS.** Two vertical creases and one horizontal. The cheapest possible signal the thing was in a pocket a second ago.
3. **DASHED TRAILS.** A solid line between two points is a graph edge; a dashed line is a ROUTE. Biggest change to the read after the paper itself.
4. **PROGRESS IS ANNOTATION.** The chart is printed in brown ink; where you've been and what you may take next is marked over it in **red pen**. Printed trails are mechanically tiled and neat; the player's are individual strokes with per-stroke wobble — **two different hands, deliberately**. Every state is signalled by that fiction with no colour key.

`Parchment.cs` holds the procedural paper, grain, ink strokes, hand-drawn rings and compass rose. It claims **tan/paper + oxblood** and gives back verdigris.

⚠️ **LIGHT GROUND INVERTS THE CALIBRATION RULES.** Everything in §2 of the `deckshift-ui` skill assumes a dark plate. On paper:
- The fold **highlight** had to drop 0.20 → **0.055**. A bright line has almost no headroom above bright paper, so any visible value instantly reads as a drawn rule — the sheet came out with three glowing lines across it.
- The compass was **invisible** as a large 0.115 watermark. A dark mark on a light ground **washes out** rather than reading as subtle. It needed to be *smaller and three times stronger*.
- The player's pen needed a **shorter stroke period and more overlap** than felt right: a trail between adjacent floors is only ~60px after trimming, so at the printed spacing the player's own route came out fainter than the chart it overlays.

⚠️ **NODE LAYOUT IS A FIXED COLUMN LATTICE. DO NOT REINTRODUCE BARYCENTRIC RELAXATION.** It was tried and reverted the same day. Pulling nodes toward their neighbours' mean X does straighten the trails — measured, sideways travel per edge falls 214px → 86px — but it computes a **different spread for every row**, so a floor with three nodes shares no column with a floor that has five. The designer read the result instantly as *"the nodes are off, they are not where they are meant to be"*. A grid you can scan beats trails that lean less. Edge crossings are **zero either way** (measured over 300 acts), so nothing is lost.

**Marquee — the character select (rebuilt 2026-08-17).** The billing before you go on: one character
owns the frame, the rest of the roster stands back in the dark, the name is printed across the top at
poster size, and everything tears past in that character's colour. ⚠️ **Its inversion is that the
theme claims NO ACCENT OF ITS OWN — it takes the character's, and the whole frame cross-fades when
the selection moves.** Every other screen has one fixed accent identifying a PLACE; this screen is
about an IDENTITY, so colour here is the *selection signal* rather than the theme signature. Its
motion vocabulary is the second inversion: everything else in the game is restrained and settled, and
this one never rests. **It replaced *Vigil*, which the designer rejected twice — see Characters for
what Vigil got wrong and for the rebuild's traps. Do not rebuild Vigil.**

⚠️ **The hue budget is nearly spent.** Claimed: orange (Iron), violet (Arcane), no-hue (Loadout), **tan paper + oxblood (map — Cartograph)**, warm wood/amber (shop), frost blue (Halt), arc-cyan (Apparatus), deep wax red (Bulletin), and **no fixed hue at all (Marquee**, which borrows the character's — jade / magenta / gold / ice are spent on the ROSTER, not on the screen). Roughly magenta and yellow remain for a *place*, but note Marquee is already using magenta for a character. **Cartograph and Bulletin are the two light-ground themes** and stay separable because Bulletin is small pale slips on a DARK board — its dominant field is dark, where the map's whole field is paper. When those run out, **stop reaching for a new colour and invert a different axis instead** — light direction, motion vocabulary, surface treatment and now value structure separate these screens at least as much as hue does, and Loadout and Marquee both prove a theme can carry no fixed hue at all.

**The Marketplace (`ShopScreenUI`) keeps its own material** — warm wood, striped canvas awning, lamplight — and was already bespoke rather than old chrome. What it needed wasn't a reskin but a PERSON; see "The keeper talks back" below.

**Loadout inverts a different axis: it's the only theme where the chrome is NOT the subject.** The other two dress a place, so the material carries the character. The relic bar dresses your inventory, sits over gameplay permanently, and the relic art is colourful pixel work — so the sockets are deliberately near-colourless and the theme is the quietest by weight. **Do not add a hue to the relic bar.** A permanent HUD element cannot compete with the game behind it the way a modal panel can.

`UIEmberField.Settings` carries the motion half (`Settings.Embers` / `Settings.Motes`): rise speed (negative = falling), lateral spread, size, life, sway, twinkle.

⚠️ **RARITY MUST SEPARATE ON MORE THAN HUE (reworked 2026-08-09).** The first palette was amber / violet / azure / cool-slate and the designer could not tell the tiers apart at a glance. Three of the four sat in the blue-violet quadrant with near-identical **luminance**, so the only cue was a ~40° hue step — invisible on a small sigil over a dark panel, and gone entirely for a colour-blind player. `FlatUI.RarityColor` now separates on **three channels at once**: hue spread right around the wheel (neutral → **green** → violet → amber; green is the biggest possible jump from both violet and amber), strictly ascending luminance (0.42 → 0.56 → 0.66 → 0.82, so a better blessing is literally brighter and the order survives greyscale), and saturation climbing from near-zero. Common stays the dimmest, for the reason already established below.

⚠️ **Rarity also has its own GLYPH now — `FlatUI.RaritySigil(rarity)`.** Every Blompo offer used one shared sigil, so colour carried the tier alone. Shape is read faster than hue and survives greyscale, colour-blindness and a 40px icon, so the marks progress **bare ring** (Common) → **ring + 4 axial rays** (Rare) → **ring + 6 rays + inner ring** (Epic) → **the full ornate `ArcaneSigil`** (Legendary). Legendary deliberately reuses the established emblem so the lesser tiers read as reduced versions of it rather than unrelated symbols.

Rarity note: the old chrome carried rarity as a gem set in gold. Without that frame **colour has to carry rarity alone**, so `FlatUI.RarityColor` is brighter and more separated than jewel tones, and Blompo tints the sigil, border, name and label together — four quiet signals instead of one loud jewel. **Common is deliberately muted**: at a lighter slate it rendered near-white and made the *weakest* offer the brightest thing on screen.

**On the relic bar, rarity is a coloured STRIP along the bottom of each socket**, plus a muted tint on the socket outline and (Epic/Legendary only) a slow glow pulse. The strip is the load-bearing signal: at 52px over moving gameplay a tinted hairline is not reliably readable, but a solid bar is legible at a glance. The tooltip repeats the rarity in its border and name, confirming what the strip meant. **Only the two rarities worth noticing animate** — that's what makes a Legendary catch your eye in a row of five.

**Blompo's blessing animation (`BlompoForgeFX`) was rebuilt to match (2026-08-03).** It used to be a hammer-and-anvil forging: three blows, sparks, screen shake. Once his screen went arcane, a smithy sequence fought everything else on the panel — he grants a charm, he isn't a blacksmith. The motion vocabulary is inverted the same way the palette was:

> forging → strikes, impacts, gravity, sparks flying **out**, the window rattling
> binding → orbit, convergence, weightlessness, motes drawn **in**, nothing ever hit

Four beats: GATHER (rune ring forms, motes stream in) → DRAW (ring contracts, everything accelerates) → BIND (`onSet` fires here) → SETTLE, where an `ArcaneSeal` contracts **into** the card and snuffs out. Two procedural sounds accompany it (`ProcSfx.ArcaneGather`, `ArcaneBind`).

The settle originally used an *expanding* ring, which the designer called bland — and re-reading it, that was the one beat in the sequence pushing **outward** while everything else converged. Pressing a seal inward finishes the idea the rest of the animation sets up. **When a beat feels weak, check whether it contradicts the sequence's own vocabulary before reaching for more particles.**

⚠️ **UI children are NOT clipped, so FX geometry is bounded by the WINDOW, not the stage.** A first pass used a 520px ring radius and scattered runes across the whole screen, outside the panel, onto the backdrop. The stage sits 60px below centre in a 762-tall window, so there is only ~321px of room downward — anything that must travel further does so on an ellipse squashed in Y (`VERT_SQUASH`). Check this whenever you add UI FX.

**Sound design note:** magic is **harmonic** (bell/chime partials 1,2,3,4,5.1), metal is **inharmonic** (bar modes 1,2.76,5.40,8.93 — see `ProcSfx.ScrapPickup`). The **gate** family (2026-08-19) is the only one that is deliberately TWO materials at once — bar modes layered over stone grit, because a portcullis is iron running in a stone slot. That ratio choice is the whole difference between "charm" and "clank"; keep the two families distinct so a blessing and a scrap pickup are never confusable.

### The keeper talks back (`ShopScreenUI`, 2026-08-03)

The designer's brief for the shop was **"make the player feel like they are talking to a person who is trying to sell them stuff."** The stall already looked like a stall; what was missing was a shopkeeper.

- **He has a face.** `Shopkeeper.ResolvePortrait()` returns an assignable `portrait` sprite, falling back to the shopkeeper's own world sprite — so a placed stall gets a face with zero wiring. ⚠️ The fallback grabs the whole stall prop, not a head; **assign `portrait` for a proper close-up.**
- **He reacts to what you do.** Barks used to be one array with a single line picked at open — decoration that never changed. They're now split by EVENT (`Greetings` / `BrowseCard` / `BrowseRelic` / `BrowseService` / `TooPoor` / `Bought` / `AlreadySold` / `Farewells`) and fired from hover, purchase, refusal and the Leave button. **Affordability outranks item type** on hover: being told you can't afford it is more useful than a joke about what it does, and it's what a real trader would say to you eyeing something out of your league.
- **Speech is typed out a character at a time.** A line that snaps in whole reads as a label changing; typed, it reads as *said*.
- **Small body language** — `Mood.Lean` on browse, `Nod` on a sale, `Slump` on a refusal, plus a constant idle bob. Deliberately tiny: a portrait that lurches around pulls focus off the prices, which is what the player is there to read.
- **No line repeats back-to-back** (`lastLine`), because with pools this small plain randomness repeats constantly and repetition is what makes barks feel canned.
- Lamplit **dust** drifts through the stall (`UIEmberField.Settings.Dust` — warm, very slow, no twinkle). A shop is a place with air in it; stillness is what made the panel feel like a menu.

⚠️ `ShopScreenUI` already had an `Update()`. The keeper's idle bob is a `TickKeeperIdle()` called from it, **not a second `Update`** — and it skips while a mood coroutine owns the transform, or the two fight over `anchoredPosition`.

**Status: converted —** `ScrapForgeScreen`, `ScrapHUD`, `BlompoScreen`, `RelicHUD`, `RelicIcon`, `RelicTooltip`, `RelicManagePanel`, `RelicSwapScreen`, `ResourceBarUI`/`ResourcePanelHUD`, `ShopScreenUI`, `CardUI`, `PauseScreen`, `SettingsScreen`. **The pass is complete.** (`PixelUI` remains and is fine as-is — the shop uses it for grain/frames.)

### The pause screen (`PauseScreen.cs`, rebuilt from scratch 2026-08-09)

Escape. **The old `PauseMenu` + `PauseMenuPanel` + `MenuManager` are DELETED** at the designer's word — do not resurrect a scene-placed pause panel. (For the record, the old one also had a wiring bug nobody had noticed: its `settingsPanel` field pointed at **TutorialPanel**, so the Settings button opened the how-to-play text, and `CloseSettings` then closed a different object than the one it had opened.)

**It is the only screen with NO window plate, and that is structural, not decorative.** Every other screen is a place you walked to inside the world, so each is a panel sitting on top of the game. Pause is not somewhere you go — it is the world being stopped — so it takes the whole frame. That choice separates it from every other screen before a single colour is picked.

#### ⚠️ REBUILT 2026-08-20 — it is now **THE HANGING BOARD**, the first Salvage screen

A board of planks bound with iron straps, dropped in on two chains in front of the dungeon wall. **The Halt theme, the frost edges, the hairline fractures and the suspended mote field are GONE** — do not restore them; they belonged to the superseded per-screen-material system. What survives from the old screen is everything below this box: the structural no-plate choice, the status readout, the two-step destructive entries, and every wiring rule.

⚠️ **IT WAS A CLOTH SHEET FOR ONE ITERATION. DO NOT RE-PROPOSE THE SHEET.** Designer: *"i like the animation that plays when i press it, like the way it comes from the top, but i just dont like the panel itself too much. i mean its not bad, but i want something better."* So the motion was kept verbatim and only the surface changed. Why cloth lost, and it is a general point about generated surfaces: **a canvas sheet is one flat value with soft folds — no structure to look at and no hard edge to make it feel built.** Planks have seams, grain, straps and bolts; wood and iron are the dungeon's core material pair; and text sits far better on wood than on cloth. (`SalvageSurfaces.Sheet` is kept — it is good cloth and something else may want it.)

Why this object:

- **Pause is not a place you travel to**, so it must not be a panel you opened. It is a thing that DROPS IN FRONT OF YOU and stops the room, then gets hauled back up. It is the only screen in the game that arrives from outside the frame.
- **It leaves by being LIFTED AWAY, not faded.** A dissolve says the screen was an image laid over the game; hauling the board up says the game was behind it the whole time.
- **The backdrop is switchable** (`PauseScreen.Ground`): `Wall` tiles the dungeon's masonry at world scale under an off-screen torch; `Game` dims the frozen gameplay to a quarter and lets it show past the board's edges — the only screen in the game that does that.

⚠️ **THE PIVOT IS WHERE IT HANGS FROM, and the content is parented to the board** so the text moves with the surface it is written on. Same lesson as the quest board's tack pivot: the rotation pivot carries the metaphor. Content that stayed level while the board swung would read as a texture behind a window.

⚠️ **A HEAVY OBJECT NEEDS DIFFERENT SPRING NUMBERS FROM A LIGHT ONE.** Carrying the cloth's values (swing K 26, damp 3.1, idle 0.16° at ~2s) straight onto planks-and-chains read as tinny jitter. The board runs K 15 / damp 2.5 and idles at a third of the amplitude at half the rate. **Mass is expressed in the numbers, not in the sprite.**

⚠️ **The chains anchor at the IRON STRAPS (u 0.055 / 0.945), not merely near the corners** — a chain bolted to bare planks looks like it would tear straight out — and they are built before the board so they draw *behind* it.

⚠️ **The board is sized to its CONTENT, not to the screen.** The first pass ran 130px past the last stat row and that bare strip made the panel read as oversized rather than full.

⚠️ **The pause is released on the FIRST frame of the yank**, not at the end — so the game is already running for the ~0.2s the cloth takes to clear. That is the point, not a compromise. Two consequences that are easy to miss: `CloseAnim` must drop `blocksRaycasts` immediately (or the player's first click after resuming is eaten by a sheet halfway off screen), and `Open` must **re-arm** the group (or a screen opened during a yank comes up looking perfect and ignoring every click). `CloseAnim` also checks `isOpen` before deactivating, since Escape-mashing can re-open mid-flight.

⚠️ **The sheet is 1400 wide because 4:3 is 1440.** Canvases match on HEIGHT, so width is what flexes and width is what breaks screens. A sheet wider than 1440 loses its hanging edges at 4:3 — and those edges are most of what makes it read as an object rather than a background.

**Selection is marked in CHALK** — a chevron plus an underline, in `Salvage.Chalk`, the exact colour and stroke sprite the world's exit marker uses. Two earlier attempts *lit* the row instead (an accent plate, then a "rubbed brighter" patch of cloth) and both failed: see §2's radial-falloff trap for why the rubbed version read as a lens flare.

The old screen's signature was a **suspended** mote field saying "time is held". Its replacement says something more physical: **dust knocked off the sheet when it dropped**, falling and thinning to almost nothing over a few seconds, so the screen calms down instead of fidgeting for as long as you leave it open.

⚠️ **The root GameObject stays ACTIVE; only its `Content` child toggles.** `Update` has to run to catch the Escape that *opens* the screen, and a deactivated GameObject gets no `Update`. Same reason `SetContentVisible` (used while a sub-panel borrows the display) drops the CanvasGroup's alpha rather than deactivating anything.

**It doubles as the run's status readout** — floor, HP, Shift, gold, scrap, relics, deck, exhausted, recall cost, and the next Stagger price (red once it exceeds current HP, mirroring the card's own rule). Several of those numbers are visible **nowhere else in the game**, and it is the one screen that can afford to show everything at once. That is what makes it worth its space; four buttons on a dark rectangle is not.

Destructive entries (**ABANDON RUN**, **QUIT**) are two-step: the first activation arms and relabels, the second commits, and moving the selection away or 4s of silence disarms. Sitting one keypress below RESUME, they need it.

**Settings and How To Play still open the OLD panels** (`SettingsPanel` / `TutorialPanel` under the Canvas). `PauseScreen` hides its own furniture, keeps its pause held, and **polls the panel's `activeSelf`** to know when it closed — both panels dismiss via their own buttons, so this needed no rewiring of either. They are next to be rebuilt; this handover exists so the pause rebuild wasn't blocked on theirs.

### Settings — `GameSettings.cs` + `SettingsScreen.cs` (rebuilt 2026-08-09)

**`GameSettings` is THE single source of truth for every player setting**, PlayerPrefs-backed, loaded through `SceneBootstrap` so it re-applies on every scene load. `SettingsMenu.cs` and both `SettingsPanel` objects (SampleScene *and* MainMenu) plus `Assets/LevelSinasi/SettingsPanel.prefab` are **DELETED**.

⚠️ **THE MAIN MENU AND THE PAUSE MENU NOW OPEN THE SAME SCREEN.** There used to be two settings panels, one per scene; with two copies every new setting has to be added twice and they drift apart the first time one is missed. `MainMenuController.OpenSettings()` calls `SettingsScreen.Open()` and its `settingsPanel` field is gone.

⚠️ **A SETTING MUST DO SOMETHING.** Never add a row without a consumer — a slider that moves and changes nothing is worse than an absent feature, because the player then stops trusting the ones that work. Every property in `GameSettings` names its consumer in a comment. The eleven live settings and where they land:

| Setting | Consumer |
|---|---|
| Master / Music / SFX volume | `AudioListener.volume`, `MusicManager.SetVolume`, `SfxManager.SetVolume` |
| **Screen Shake** | `CameraShake.Shake` scales intensity; 0 refuses the call outright |
| **Freeze Frames** | `HitStop.Stop` scales duration; **0 must return BEFORE touching `timeScale`**, or a zero-length freeze still sets it to 0 for a frame — a visible hitch |
| Damage Numbers | `EnemyHealth`'s popup spawn |
| **Enemy Health Bars** | `EnemyHealthBar` — switches its whole Canvas |
| Card Aim Preview | `CardAimIndicator.LateUpdate` |
| Display Mode / VSync / Frame Cap | `Screen.fullScreenMode`, `QualitySettings.vSyncCount`, `Application.targetFrameRate` |

**Screen Shake and Freeze Frames are scaled at the ONE chokepoint each**, not at the 23 and 8 call sites — so a shake added later cannot forget to respect the setting.

`ApplyDisplayMode` is deliberately `#if !UNITY_EDITOR`: `Screen.fullScreenMode` in the editor resizes the actual **editor window**, which is alarming and has to be undone by hand.

Screen details worth keeping: the value is re-read from `GameSettings` on every `RefreshAll` rather than mirrored in widget state (rows affect each other — VSync greys out Frame Cap — and RESET changes all eleven at once); keyboard navigation **skips disabled rows** so it never parks on a control that ignores input; a slider click anywhere on the track jumps the value there (grabbing a 3px handle would be miserable); and there is **one shared hint line** describing the selected row rather than eleven permanent captions burying the controls.

Three procedural sounds in `ProcSfx`: `PauseHalt`, `PauseRelease`, `PauseTick`. They are a **fourth sound family**, defined by their ENVELOPE rather than their spectrum (magic = harmonic bell partials, metal = inharmonic bar modes, stone = noise + sub). The halt is the only sound in the game that gets **choked** — a damper clamps the ring away over 180ms instead of letting it decay. A sound that fades out says "ending"; a sound cut short says "held". Release is its inverse and is allowed to run out naturally.

### `GameScreen.cs` (2026-08-16) — the shared screen contract

**Every new full-screen panel should extend `GameScreen`.** It owns taking over the display and
handing it back: pause, game state, HUD hide, hand-drawer lock, the one-frame Escape memory, both
aspect-fit modes, and finding the right Canvas.

⚠️ **It is deliberately NOT a lifecycle that owns activation.** Screens genuinely differ there —
`PauseScreen`'s root must stay ACTIVE so its `Update` can catch the Escape that *opens* it, while
every other screen deactivates its own GameObject. Screens keep their own Show/Hide and call
`AcquireDisplay()` / `ReleaseDisplay()` from inside it. A base class that insisted on `SetActive`
would have to be fought by the one screen that matters most.

**Why it exists:** those twelve lines were copy-pasted *identically* into ten screens — same fields,
same order, same guards. Three details are load-bearing and none are obvious, so every new screen was
one forgotten line from a bug that only appears when screens open on top of each other:

- **`hudWasActive` is RECORDED, not assumed.** A screen opened over another (a chest's relic swap,
  Blompo from the forge) must restore the HUD to what it *was*, not switch it on.
- **The drawer lock is GATED on `hudWasActive`**, or an inner screen unlocks a drawer the outer
  screen still needs locked.
- **`prevState` is SAVED, not hardcoded to `Playing`.**

`AcquireDisplay`/`ReleaseDisplay` are **idempotent** (a double Show can't stack two pauses), and
`OnDestroy` releases — a screen destroyed while open would otherwise leave the game paused forever
with no HUD.

⚠️ **The two aspect-fit modes are NOT interchangeable.** `FitWindowToCanvas` RESIZES and is only safe
when content is anchored to the window's corners with insets (the run map's chart). `FitScaleFor`
returns a uniform scale and is required when content sits at fixed offsets from the window centre
(Blompo, Settings, the shop) — *resizing* those overlaps their own columns.

`UIHeldPauseLastFrame` + `TickUIPauseMemory()` generalise the guard that used to live only in
`PauseScreen`: any screen opening on a keypress must check it, because script execution order is
undefined and the screen closing this frame may release its pause before yours runs.

⚠️ **Do NOT retrofit every screen at once.** New screens use it immediately; existing ones migrate
when already being touched. **`QuestBoardScreen` is the migrated worked example.** Verified after
migrating: open/close balanced, no pause leak over three cycles, double-open and double-close safe,
and — the load-bearing case — opening the board *on top of* the relic panel and closing it leaves the
HUD hidden and the outer screen's pause intact.

### The UI sound family (2026-08-16) — defined by PITCH MOTION, not by material

Six sounds in `ProcSfx`: `UIMove` / `UIConfirm` / `UICancel` / `UIRefuse` / `UIOpen` / `UIClose`,
fired from `GameScreen`.

⚠️ **This family's rule is a different KIND of rule from the others.** Every existing family is
defined by a MATERIAL — magic by harmonic bell partials, metal by inharmonic bar modes, stone by
noise + sub, paper by having no pitched component at all, the pause pair by a choked envelope. **A UI
sound has no material**: it is not a thing in the world, it is the interface. So this family is
defined by **pitch motion** instead — all six share one voice (literally the same `WoodTap` call) and
differ only in which way the pitch moves. That is what makes them a learnable *language*, and it is
the right mechanism because these are the only sounds in the game that must be told apart **from each
other**; a world sound only has to be distinguishable from other materials.

⚠️ **The voice is soft struck WOOD**, deliberately claiming the one material the world does not use
(metal = forge, glass/bell = magic, stone = rooms, paper = quest board). A clean synth blip would
sound like it came from a different game.

⚠️ **CANCEL AND REFUSE ARE NOT THE SAME SOUND.** Cancel is the player choosing to back out —
consonant, no fault implied. Refuse is the *game* saying no, and is the only dissonant sound in the
family. Refuse must also not read as damage: it means "you can't", not "you got hurt".

⚠️ **Open and Close are the same three notes inverted**, not two unrelated sounds — the pairing is
what says the thing that arrived is the thing that left.

⚠️ **A screen with a BESPOKE open sound must override `PlaysDefaultOpenCloseSound` to false**, or it
plays two. The quest board's paper rustle and the pause screen's halt/release are signatures and beat
the generic pair; the generic pair exists for screens that would otherwise be silent.

**Audition without Play mode:** **Deckshift → Bake UI SFX Previews** writes the six to
`Assets/ProcSfxPreview/*.wav` (throwaway folder). Verified by measurement rather than ear —
Move is the quietest (peak 0.038 vs 0.072–0.088), Confirm's 930Hz overtakes its 620Hz while Cancel's
585Hz overtakes its 780Hz, Open/Close are 520→780 and 780→520, and Refuse carries both 600Hz and
636Hz simultaneously (a beating minor second, not a melody).

### Typography — `UIType.cs` (2026-08-16), two stated faces and a size scale

**`UIType` is the single source of truth for what the UI is set in.** Before it, the font was decided
by **census**: `FlatUI.UIFont()` counted every `TMP_Text` in the scene and returned the most common
one. That is an emergent property, not a decision — a full `FindObjectsByType` per call, capable of
answering differently in MainMenu than in SampleScene, and **any screen that forgot to call it fell
silently out of the system** (the character select shipped in Liberation Sans exactly that way).

**The split (designer-chosen 2026-08-16, from screenshots):**

| | face | takes |
|---|---|---|
| **Display** | `CCBattleScarred` | titles, headings, menu items, buttons, stat labels, numbers — the game's voice |
| **Prose** | `Pixie` | running sentences ONLY — contract text, card rules, barks, trait blurbs |

⚠️ **CCBattleScarred has essentially no lowercase**, so used for prose it renders every sentence as
capitals. That is fine for labels and terrible for paragraphs.

⚠️ **JUDGE A TYPE DECISION ON A SCREEN WITH SENTENCES IN IT.** The obvious candidate — the pause
screen, "the densest screen" — turned out to barely discriminate: it is 30 labels and numbers with
almost no prose, and the all-display version looks *best* there. The quest board decided it, because
a contract reads `CLEAR 4 ROOMS IN A ROW WITHOUT PLAYING STAGGER.` in the display face and
`Clear 4 rooms in a row without playing Stagger.` in the prose face — and the Bulletin theme's whole
conceit is that a person wrote these and pinned them up.

⚠️ **Prose size is auto-compensated (`ProseScale` 1.18).** Pixie has a smaller cap height, so at equal
nominal pt it renders visibly smaller. `UIType.SizeFor(role, prose: true)` applies it — **never
hand-tune a size to compensate**, or the two faces drift apart again.

⚠️ **A THIN FACE ON A LIGHT GROUND NEEDS DARKER INK THAN THE NUMBER SUGGESTS.** The quest slip's body
colour was chosen for the heavy display face; Pixie's strokes cover far less area, so the same value
read washed out. Measured on the slip: paper luminance 0.75, title ink 0.109, body ink **0.189** —
nearly twice as light as the title while carrying the sentence you actually have to read. Pulled to
0.141. Same family as the linear-colour-space rule: **measure the pixels, don't compute them.**

⚠️ **`Assets/Resources/UIType.asset` carries the two font references** because neither font lives in a
`Resources/` folder (Pixie ships inside the Cainos pack, CCBattleScarred sits in `LevelEfeVrl/
Sprites/`), and moving either risks a pack reimport undoing it. Rebuilt by **Deckshift → Rebuild UI
Type**, same pattern as `RelicCatalogue`. If the asset goes missing, `UIType` **falls back to the old
census** rather than breaking — degrading to today's look, not to Liberation Sans.

**Migration policy: do NOT retrofit every screen at once.** `FlatUI.UIFont()` now delegates to
`UIType.Display()` and returns exactly what the census was already resolving to, so wiring it in was
a visual no-op across all 18 screens that call it. New screens use `UIType` immediately; existing ones
move their prose to `UIType.Prose()` when they are already being touched. **`QuestBoardScreen` is the
one migrated so far** — use it as the worked example.

### Cards: rarity colour is the ART's job, not the UI's (designer 2026-08-06)

**Card rarity is telegraphed in the card ARTWORK, in colour: dark grey Common, light grey Uncommon, yellow Rare, purple Epic. There are no Legendary cards.** The incoming art has this baked in, so **UI code must not invent a second rarity colour system on a card** — two colour codes on one object that disagree is worse than one.

This is a live constraint, not a preference: `CardUI`'s blessing mark originally tinted itself by the *blessing's* rarity via `FlatUI.RarityColor`. That's a different axis, but no player would read it as one — and it contradicted the art (calling Rare azure where the art calls it yellow). It is now **one fixed teal on every blessing**, chosen to sit outside the grey/grey/yellow/purple palette and pushed green of Shift-blue so it can't read as a cost either. Blessing hierarchy moved to a channel the art doesn't use: **only Epic/Legendary blessings pulse.**

### Hovering a card TURNS IT OVER (`CardBack.cs` + `CardHoverFlip.cs`, 2026-08-09)

⚠️ **`CardHoverFlip` IS THE ONE IMPLEMENTATION — never hand-roll a second.** The hand (`CardUI`), the Scrap Forge's repair chips and Blompo's card picker all attach it. It exists as a component because the mechanism has three non-obvious requirements that have each already caused a shipped bug: the back must be **pre-rotated 180°** or it renders mirrored; the hit target must **counter-rotate** or the card flaps edge-on under the cursor; and showing the front must **restore only what it hid** or deliberately-inactive children get resurrected. `CardBack.BindStandard(card)` fills the normal SHIFT/CHARGES footer (CardUI overrides it only for Stagger), so every screen reads identically.

⚠️ **`CardHoverFlip.Attach` takes a GEOMETRY SOURCE.** Pass `cardArtImage` for a card whose root is rewritten by a layout group — the deck view's and the card chest's grids still do this (the HAND no longer does; see "The hand" below). Pass nothing for the forge and Blompo, whose chips are built at the size the player sees; `CardBack.MatchTo` detects "the source is my parent" and fills it.


The old hover was a flat grey rectangle laid over the card, the art faded to 12% behind it, and a **140×50** text box that every real description overflowed. It read as a tooltip that had landed on the card. The designer asked for something nicer and suggested the card's back — so the card now flips.

**The flip is free.** Screen Space Overlay is an orthographic projection, so rotating the card on Y renders as a horizontal squash to nothing and back out — exactly what turning a card over looks like, for one `Quaternion` per frame. No perspective canvas, no shader. Unscaled time throughout (the reward screen and deck view both hold `timeScale` at 0). Faces swap at the halfway point, where the card is edge-on.

⚠️ **THE HOVER IS DETECTED BY A COUNTER-ROTATING CHILD, NOT BY THE CARD.** A rotating card's raycast rect narrows exactly as its picture does, so halfway through the flip the pointer is inside nothing, `OnPointerExit` fires, the card turns back, widens, `OnPointerEnter` fires — and it sits edge-on flapping, a vertical sliver under the cursor. That is the shipped-broken state the designer reported. `CardUI.hoverTarget` is an invisible, full-card-size child that cancels the root's turn each frame, holding a stable axis-aligned rect for the whole animation; pointer events bubble from it to `CardUI` and clicks bubble to the root's `Button`. Its centre sits on the root's rotation axis, which is what makes the cancellation exact. It must never be disabled by `SetFrontVisible`.

⚠️ **POINTER BEHAVIOUR CANNOT BE VERIFIED BY CALLING `OnPointerEnter` YOURSELF.** That is precisely how this shipped: invoking the handler directly never produces the *exit* that breaks it, so every test passed while real hovering was unusable. Verify geometrically instead — build a `PointerEventData` at the cursor's would-be position and run `EventSystem.current.RaycastAll` at each flip angle. Measured, with the counter-rotation the card is HIT at all of 0/22.5/…/180°; without it, MISS from 90° onward (past 90° the graphics are also back-face-culled, so it can't be re-entered at all).

⚠️ **`CardBack` is pre-rotated 180° on Y.** Past 90° every child of the rotating root renders MIRRORED, text included; the pre-rotation cancels it exactly when the back is the face you're looking at.

⚠️ **The back is SIZED OFF `cardArtImage`, never off the card root.** The root carries a `LayoutElement`, and inside a layout group that overwrites its RectTransform at runtime it measures **200×100**, not the 200×300 the prefab shows. (True of the deck view and the card chest; the hand stopped using a layout group on 2026-09-07 and restores the prefab's geometry itself.) Stretching to it produced a back a third of the card's height over its bottom edge. Same reason the blessing mark anchors to the art. The back still *parents* to the root (that's what turns it) and copies the art's geometry instead.

⚠️ **The front is "every child that isn't the back", re-read on each face change — never a list cached in `Awake`.** Other systems parent things onto a card afterwards: `RewardScreenFX` hangs a "+1 SHIFT" bonus badge on the offered card, and an `Awake` snapshot left it showing straight through the flip, rendered mirrored as "+1 TFIHS".

⚠️ **AND THE FLIP ONLY RE-SHOWS WHAT IT ITSELF HID.** Turning every child back on is *not* the inverse of hiding them — three of `CardUI_Template`'s children are supposed to be off. `Image` and `ShiftCostContainer` ship disabled in the prefab (dead leftovers) and `Awake` retires the legacy `Hover_Panel`, so one flip out and back **resurrected all three** and the card came back wearing a grey overlay reading "New Text". `SetFrontVisible` records what was actually visible when it hid the face and restores exactly that set.

**It is NOT dressed in FlatUI's iron.** FlatUI is the material for *screens*, and each screen picks a material and inverts something. A card back is not a screen — it belongs to the deck, whose fronts are painted gold-on-near-black. Re-skinning it as a charcoal workbench plate would make the card visibly stop being a card halfway through its own flip. It borrows FlatUI's *shapes* (they're just white sprites) and none of its palette.

**Sizing the description text (2026-08-09).** The card is only ~160×240 screen px, which is small for a paragraph, so two things carry it:
- **A flip zoom of 1.2×, plus a 40px LIFT.** Hand cards sit 200px apart and are 160px wide, so 1.2× (=192px) is the largest zoom that cannot overlap a neighbour — measured, not guessed. It composes with the selection bump rather than replacing it, and it lerps on **unscaled** time because `Time.deltaTime` is 0 on every screen that pauses, which would have left reward cards flipping without ever growing.
  ⚠️ **The zoom is useless without the lift.** The hand sits on the screen's bottom edge and a card's art already overhangs it — measured, the card bottom is **6px below the screen at rest**, and because the zoom grows about the root's pivot that becomes **22px** at 1.2×. The Shift/charges row lives in the lowest 12% of the back, so it was exactly the part that got cut off. 40px clears it with ~18px to spare. If the zoom or the drawer's resting position ever changes, re-measure the back's bottom corner against y=0.
- ⚠️ **The body's auto-size CEILING is the design; the floor is a safety net.** The first pass capped it at 13pt while the box was two-thirds empty — nothing was constraining the text except the cap. 14 of 15 cards now settle at exactly **21pt**, so they look identical; the longest steps to 19. **Do not widen the ceiling to give short cards bigger text** — a one-line card rendering at twice the size of a wordy one reads as broken, not as emphasis. If a card can't reach 21, shorten the card's text (Glass Parry was trimmed from 173 to 142 chars for exactly this reason). The floor is 12 for the rare **blessed** long card, which carries two extra lines on an already-full face; at a 16pt floor three blessings clipped straight out of the box.

⚠️ **TMP auto-size does not settle within one frame, so you cannot batch-measure it.** Setting `text` and calling `ForceMeshUpdate` in a loop gives sticky, wrong numbers — one pass reported 36pt with `textBounds.size.y` of −4294967000, another reported 12pt for a string that really renders at 21. Measure ONE string per frame, read line metrics (`textInfo.lineInfo[0].ascender − lineInfo[last].descender`) rather than `textBounds`, or better, drive a real card through `Setup` and read it on the following frame.

Two calibration lessons, both re-learned the hard way:
- ⚠️ **Rules are 2px, not 1.** Cards render at ~0.8 scale in the hand, so a 1px rule is 0.8 device pixels and visibility comes down to subpixel luck. Both rules were drawn by identical code and only the lower one appeared — measured at `#9D8541`, full strength, while the upper sampled as bare card.
- ⚠️ **The watermark is an OUTLINE, small and faint.** First pass was a filled diamond at 56% of the card width and 0.055 alpha: it measured `#231E12` against a `#0D0D0D` ground — three times the ground's value — and read as an olive blob the body text sat on. A watermark has to survive being ignored.

**The deck view does not flip.** `DeckViewUI` sets `ui.enabled = false` after `Setup` (so `CardUI.Update` stops resetting the scale it needs for grid cells), which also stops `Update` and pointer events. That's unchanged behaviour — the deck view never had hover text — but it's the obvious follow-up if browsing your deck should read descriptions too.

### Card descriptions are written for a player, not a spec (2026-08-09)

Rewritten across all 15 cards: lead with the verb, state the number, one or two short sentences, no restating the cost (the card face and the back's footer both show it). Two were also **factually wrong** and are fixed — Comet Dive said 20 damage when `cometDamage` is **40** (radius 5), and Dash never mentioned that it grants **i-frames**, which is most of why you'd play it.

### Every screen draws the REAL card face — `CardFace.cs` (2026-08-09)

**All three non-hand screens use it: the Scrap Forge, Blompo and the shop.** The shop was the worst of them — it drew the card into a **68×68 square icon**, letterboxing a 2:3 card down to ~45×68, then re-printed the name and `N SHIFT  N CHARGES` underneath. Its card tiles are now card-shaped (`TILE_H / CardFace.ASPECT` wide; relics and services keep the square shelf tile) and carry no grain plate or PixelUI frame — the card has its own painted border, and a second frame around it read as a card inside a card. ⚠️ The price plaque is lifted clear of the card's **name plate** (bottom ~10% of the face); at the normal height it covered the title, leaving a row of unlabelled pictures.

The Scrap Forge and Blompo used to build their own card chips: a FlatUI plate with `cardArt` squeezed into a **square** box, which letterboxed the whole 2:3 painted face down small enough that its own medallions were unreadable — which is exactly why those screens re-printed the name, SHIFT and CHARGES as separate text underneath. Both now draw the card at its true aspect via `CardFace.Build`, and the duplicate readouts are gone. There was never a design reason for the divergence; it was history.

⚠️ **THE MEDALLION NUMBERS ARE NOT PAINTED INTO THE ART.** The art carries the empty gold circles; the digits are TMP fields in `CardUI_Template`. Any screen that draws `cardData.cardArt` on its own gets a card with two **blank sockets**. `CardFace` stamps them at fractions measured off the prefab (`Cost_Text` at (69.5, 121.4), `Uses_Text` at (-65.4, 126.4) in a 200×300 rect), so there is one place to fix if the art is re-cut.

⚠️ **THE SET CURRENTLY HAS TWO ART STYLES AND THEY FIGHT.** The older cards socket their medallions in dark gold circles; **Dead Weight, Freefall Blade, Glass Parry and Shuriken** are newer art with a red ball and a **blue crystal**, and no painted name. Consequences already hit: a blue Shift digit on a blue crystal was *invisible* at ~10px (fixed with a 4-way dark **keyline**, not a one-sided drop shadow — that leaves most of the glyph edge unlit), and those three had `nameIsPaintedIntoArt` wrongly set true in the bulk pass, so they rendered a blank name plate **in the hand as well**.

### ⚠️ THE FREEFALL BLADE FRAME IS THE CANONICAL CARD FRAME (designer, 2026-08-17)

**All new card art uses it**, and the layout is fixed for every card: the **red ball** (charges, left), the **blue crystal** (Shift cost, right), an **empty name plate** (drawn in code — see below), and on cards that deal damage a **heart container**. `CardFace.Gem` is therefore the layout to tune and trust; **`CardFace.Classic` is legacy** and exists only until the 14 old cards are re-cut. When they are, delete `Classic` and the chooser with it.

⚠️ **The heart container is NOT BUILT — it is the designer's stated plan, not a request.** Do not invent a different mechanism for "does this card deal damage" in the meantime. When it lands, the machinery already exists: `CardUI.RefreshCardFace` draws a number into a heart for **Stagger** today (the `HEART_*` fraction constants), which is the same problem in the same place.

⚠️ ~~Both styles put cost right / charges left, so the positions do hold.~~ **THAT WAS WRONG AND IS NOW FIXED (2026-08-17).** The two generations put their medallions **0.045 of a card width apart**, and on the gem cards the charge number sat off the LEFT EDGE of the red ball entirely:

| | charges | cost | sprite |
|---|---|---|---|
| **gem (canonical)** | **(0.2188, 0.8796)** | **(0.8330, 0.8767)** | `freefallblade_0`, 118×200, aspect **0.590** |
| classic (legacy) | (0.173, 0.921) | (0.848, 0.905) | `fireball_0`, 1024×1536, aspect **0.667** |

⚠️ **MEASURE ON THE RENDERED CARD, NOT ON THE SPRITE.** The first pass scanned the sprite for strongly-coloured pixels. That is fine for the ball (a saturated disc, and its value was confirmed correct to 0.3px) and **wrong for the crystal**: a diamond tapers to dark, desaturated tips, the strict colour test missed the top one, and the resulting "centre" put the Shift digit **14.5px low on a 900px card — about 8% of the crystal's height.** That is what the designer reported as the numbers not being centred. Rendering the real card and measuring the medallion **and** the digit ink in the SAME image removes every mapping assumption at once — it answers "is the number on the medallion?" directly instead of inferring it.

⚠️ **The tool that settled it: a ROW-WIDTH PROFILE, not a bounding box or a centroid.** A circle and a diamond both reach their widest row exactly at their vertical centre, so the peak row *is* the answer, and it is immune to the rim, highlights and facets that drag a centroid or inflate a bbox. On the ball the three methods disagreed — bbox said x=814, centroid said 811.1, and the mode of the row midpoints said 811 with a symmetric profile, which is the truth. Capture with `ScreenCapture.CaptureScreenshot`, then read the PNG back with `File.ReadAllBytes` + `Texture2D.LoadImage` to sample it.

⚠️ **A residual of ~2px on a 900px card is the GLYPH, not the placement, and must not be "corrected".** Both medallions now land within 1.5px, and the leftover is each digit's own bearing — measured from the font asset, the worst digit is 0.63px vertical and 0.19px horizontal at hand size. It also differs per digit, so tuning it against one number over-fits.

⚠️ **`CardFace` is the single source for every screen INCLUDING the hand.** `CardUI.Setup` calls `CardFace.PlaceMedallion` on its two prefab labels rather than trusting their authored positions, so the hand and the forge cannot drift apart.

⚠️ **The generation is told apart by SPRITE ASPECT, and that is a STOPGAP.** Aspect is at least a property of the art FILE rather than of gameplay data, but it is still a proxy — a new card cut at 0.667 would silently take the legacy positions.

### ⚠️ Two digits were invisible against the medallions they sat on (2026-08-17)

Both are the same mistake and both were on the **canonical** frame, so both would have shipped:

- **Shift cost: blue on a blue crystal.** Sampled off the sprite, the crystal averages **(0.377, 0.398, 0.920)** and the digit was **(0.307, 0.304, 0.934)** — the same colour. The designer reported it as blending into the background, and it did, exactly. Now pale ice **(0.90, 0.95, 1.00)**: keeps the Shift-blue identity the whole game uses for this resource, at luminance ~0.93 against the crystal's ~0.43.
- **Last-charge warning: red on a red ball.** `currentUses == 1` painted the number `Color.red`, and the canonical charge medallion *is* a red ball — the warning was invisible exactly when it mattered most. Now amber **(1.00, 0.82, 0.25)**, which still reads inside the legacy frame's dark gold ring.

**The general rule: a status colour must be measured against the SURFACE it appears on, not chosen for its meaning.** Red means danger, and it is the one colour that cannot say so on a red ball.

⚠️ **The cost also grew 30 → 34.** Colour was the reported fault, but the cost was also the *smaller* of the two numbers while sitting on the *larger* medallion — a single digit filled ~40% of the crystal's width. Recolouring fixed legibility without fixing presence, and the cost is the number a player checks most often ("can I afford this?").

⚠️ **The four-copy keyline is GONE — there is now ONE shared outlined material.** Every number used to be drawn five times (the digit plus four offset black copies) because the digits sit on saturated artwork. It worked, but **the hand never had it** — its labels are prefab objects, not built by `CardFace` — so the same card read differently in your hand than in the forge. `CardFace.ApplyNumberOutline` puts a real SDF outline on both. It must be `fontSharedMaterial` and it must be ONE cached material: writing `outlineWidth` on a `TMP_Text` auto-instances a material **per label**, which breaks batching and leaks one material per card drawn. Same look everywhere, one draw call, 8 fewer TMP objects per card.

⚠️ **Max number width is PER MEDALLION** (`USES_MAX_W` 0.165 / `COST_MAX_W` 0.130), because the sockets are not the same size: the ball is 0.357 of the card wide, the crystal only 0.219 — and the crystal is a diamond, so a number near its full width runs into the tapering facets. One shared budget either wasted the ball or overran the gem.

⚠️ **`preserveAspect` MEANS THE ART IS NOT THE HOST — measure against the DRAWN art.** The gem sprite is 0.590 where the card box is 0.667, so it letterboxes to **88.5%** of the host width with bars either side, and every number stamped at a fraction of the HOST lands outside the artwork it belongs to. This is half of the misplacement above, and it is the same letterbox `CardUI` already maps Stagger's heart and name plate through. `CardFace.DrawnArtSize` is the shared helper.

⚠️ **A TWO-DIGIT CHARGE COUNT IS 1.93× THE WIDTH OF ONE DIGIT, AND THE SOCKETS ARE DRAWN FOR ONE.** Measured in the display face at 100pt: widest digit `0` = 58.2px, `10` = 110.8, `99` = 112.2, `100` = 176.0, `∞` = 70.9. **Shuriken is the only card in the set with `maxUses` 10**, so it was the one that showed it — the designer reported the charges as "weird and bad over 10", and on both styles the number simply spilled off its medallion. `CardFace.FitNumberSize` shrinks any number to `NUMBER_MAX_W` (0.135 of the drawn card width, which is inside both sockets — verified on both styles at 1/9/10/99).

⚠️ **Deterministic scaling, NOT `enableAutoSizing`.** TMP auto-size settles over several frames and is documented in this file as unreliable to measure; these labels are rebuilt on every hand refresh. The scale comes from a measured glyph-width constant instead, so it is correct on the frame it is set.

⚠️ **Verified as a NO-OP on the 14 classic cards**: a single-digit classic card still resolves to font size 38.0 at exactly (-65.40, 126.40) — byte-identical to the authored prefab values.

⚠️ **`CardUI_Template` is NOT scale-corrupted** — measured 2026-08-09: root scale (1,1,1), 200×300, `ShiftCostContainer` scale (1,1,1) and inactive. The "non-uniform (0.119, 0.568, 0.92)" warning below refers to an older prefab and does not apply to the card the game actually uses.

### Card name plates are drawn in CODE from now on (designer 2026-08-09)

**New card art must ship with an EMPTY name plate.** `CardUI` types `cardName` into it. This decouples a card's name from its texture — renaming a card stops being a repaint — and it is why **`CardData.nameIsPaintedIntoArt` defaults to `false`**.

⚠️ **The 14 pre-2026-08-09 cards have their titles painted in and all set that flag**, so nothing about them changed. **Clear it on each card as its art is replaced.** Getting it backwards is visible instantly: set-when-blank leaves an empty plate, clear-when-painted prints the name on top of itself.

Plate geometry (`PLATE_CY/W/H` in `CardUI`) was measured on Stagger's art but is expressed as fractions of the **sprite rect**, and the legacy 1024×1536 cards put their plate within ~1% of the same place — so one set of constants serves both layouts, letterboxing included. Re-measure only if new art moves the plate. Colour is the set's title gold, matching the painted plates.

### `CardUI` — the blessing mark (2026-08-06)

`CardUI`'s only procedural chrome was the blessing badge; the card frame, cost medallions, rarity tag and name plate are all **painted into the card art sprite**, so "converting CardUI" meant converting that one mark. Three things were wrong with it and all three are fixed:

- **It wasn't on the card.** It was anchored to the card ROOT, whose RectTransform is a **200×100 stub** — while `cardArtImage` is the real 200×300 card face. The mark floated off the card's right *edge* at mid-height. It is now parented to `cardArtImage.rectTransform`, the only honest geometry on the prefab.
- ⚠️ **`cardArtImage`'s sprite is the WHOLE CARD FACE** (1024×1536), not the inner picture — frame, medallions and name plate included. Measured on the real cards, the inner picture occupies roughly **10%–80% of the card height**, so a naive small inset lands the mark inside the painted *name plate*, on top of the card's title. `MARK_INSET_Y = 62` (of 300) clears it.
- **The look** was a jewel in an ornate gold ring — the chrome this pass exists to remove, and its bright gold setting drowned the gem so different rarities read identically. It is now Blompo's own `ArcaneSigil` glowing over a soft dark halo: light *inscribed on* the card rather than an object stuck to it, tying the mark to the screen that grants it. The dark halo (not a frame) is what keeps it legible over busy artwork.

The mark deliberately does **not** say which of the seven blessings it is — the hover text names it. Seven legible glyphs at ~24 screen px is a bespoke-art job, not a procedural one.

Verified in play mode across the hand and the deck view: blessed cards mark, unblessed cards build no mark at all.

### Resolution independence (2026-08-09) — the game is NOT 1920x1080-only

The project was believed to be locked to 1920x1080. It never was: `defaultIsNativeResolution` is **on**, so a build launches at the player's native resolution and the 1920x1080 in ProjectSettings is only the *windowed fallback* size. What was actually wrong was three settings.

⚠️ **EVERY CanvasScaler IS `ScaleWithScreenSize`, ref 1920x1080, `matchWidthOrHeight = 1` (HEIGHT). Do not change the match value.**

**Match HEIGHT because the camera is height-anchored.** `Camera.main.orthographicSize = 7` means the view is exactly **14 world units tall at every aspect**, with the width flexing (`halfW = orthoSize * aspect`, which `CameraFollow` already computes correctly). Matching *width* made the UI do the opposite of the camera: on a 21:9 display the canvas became only **810** logical px tall instead of 1080, which clipped 170px off the run map (980 tall) and 130px off settings (940 tall). With match=height the canvas is always 1080 tall and its width is `1080 * aspect` — 1440 at 4:3, 1728 at 16:10, 1920 at 16:9, 2560 at 21:9.

Also fixed: **MainMenu and GameOverScene were `ConstantPixelSize`**, so their UI did not scale at all (measured: at 2560x1440 the menu rendered at its authored pixel size and looked shrunken). And `resizableWindow` was off, so windowed mode could not be dragged.

⚠️ **AN ACTIVE BUILD PROFILE OVERRIDES ProjectSettings, AND `PlayerSettings.*` WRITES TO THE PROFILE.** Setting `PlayerSettings.resizableWindow = true` changed `Assets/Settings/Build Profiles/New Windows Profile.asset` and left `ProjectSettings/ProjectSettings.asset` still reading `resizableWindow: 0`. Both are now set. **When changing a player setting, check which of the two actually moved** — a value set only in the profile silently reverts for any build made without it, and reading `PlayerSettings.x` back gives you the profile's value, so it looks correct either way.

⚠️ **A UI element that sits at a screen EDGE must be anchored to that edge.** With the canvas width now varying, a centre-anchored element at a large offset drifts. Audited every `GameplayHUD` child; exactly one was wrong — **`RecallButton`** was anchored to centre `(0.5, 0.5)` at `x = -859.2`, which put it 5px from the left edge on a 1728-wide canvas and cut it in half. Re-anchored to `(0, 0.5)` at `x = 100.8`, which is the identical position at 1920 and correct everywhere else. Everything else was already edge-anchored.

**Oversized windows now fit themselves**, and the two mechanisms are NOT interchangeable:
- **`RunMapScreen.FitWindowToCanvas()` RESIZES** the window (1560x980, the widest in the game). Its chart lives in `area`, anchored to the window corners with insets, so it genuinely reflows into a smaller box.
- **`BlompoScreen` (1600) and `SettingsScreen` (1240) SCALE** uniformly instead, via `FitScale()`, never above 1. Their content sits at fixed offsets from the window centre, so *resizing* them would overlap their own columns — shrinking is only safe as a uniform scale. `ShopScreenUI` already did this.

**Verified by screenshot at 4:3 (1440x1080), 16:10 (1920x1200), 16:9 (1920x1080, 2560x1440) and 21:9 (2560x1080):** zero visible graphics off-screen in SampleScene or GameOverScene at any of them, and every change is a **no-op at 1920x1080** (canvas scaleFactor 1, RecallButton on the same pixels, both windows at full size).

**Camera vs room width — measured, no action needed up to 21:9.** A room's CameraBounds zone must be at least `14 * aspect` wide or the clamp inverts. Need is 24.9 at 16:9 and **33.2 at 21:9**; the pool's rooms are 42.8–68 wide, so all clear it. Only `EfeVrl5`'s narrow sub-zone (25.9) inverts at 21:9, and its art still covers the overshoot, so nothing is visibly wrong. **32:9 super-ultrawide needs 49.8 and most rooms fail it** — that's the line to draw.

Known cosmetic nit at 21:9: `GameOverScene`'s background art doesn't reach the edges, leaving plain grey strips. Scene art, not UI.

### Never Scale UI Containers — Resize Them

When a UI element needs to be bigger or smaller, **change Width and Height in the RectTransform, not Scale.** Scaling a UI container cascades to children and fights with Layout Groups, producing wildly incorrect sizes (twice during the last session we hit this — once with the RelicHUD container scaled 5.44× on Y, once nearly happened with the QuestBoardOverlay). The honest fix is always Width/Height, sometimes anchor/pivot. Leave Scale at (1, 1, 1) on UI elements.

### The hand — `HandUI` (the row) + `HandUIDrawer` (the rail)

⚠️ **THE OVERLAPPING FAN WAS BUILT (2026-09-07) AND REJECTED (2026-09-13). DO NOT REBUILD IT.**
Designer: *"it covers up the charge of the cards"*. The reason is the artwork and no tuning fixes
it: the canonical frame puts charges top-left and the Shift crystal top-right, so overlap hides one
corner on every card but the front one. Right-over-left hid the cost, left-over-right hid the
charges; there is no third order. What shipped instead: **a flat row, card width + 6px pitch, no
tilt, sunk so only the top half shows** (`baselineY` −120), hover lifts the card clear. Both
medallions visible on every card, always. The fan machinery (`tiltStep`, `arcDrop`, `Depth`) is
still in the file at zero — it is the overlap that is forbidden, not the code.

⚠️ **AND IT IS SUNK TO ITS TOP HALF BECAUSE THIS IS A PLATFORMER.** Designer, 2026-09-13: the hand
"uses up too much space in the game". Everything a card says at a glance — art, cost, charges,
key — is in its top half; the bottom half is name plate and frame, which the hover already shows on
the back. Measured: bottom ~14% of the screen instead of ~22%, entirely below the hub's floor line.
**New card art must keep its identifying silhouette in the upper half of the card**, or it is a
black strip at rest.

⚠️ **IT IS NO LONGER A HOVER DRAWER AND NO LONGER A LAYOUT GROUP.** Two rebuilds, and every doc written before them is wrong about this screen:

- **2026-08-22 — it stopped hiding.** It used to slide out of sight and rise only when the pointer entered a 1000×200 zone. Aimed cards fly at the CURSOR, so reading your hand and aiming a shot were the same input, fighting each other mid-fight; and the raised panel covered the character. `SetLocked` is now the only thing that hides it (a full-screen panel is up and the hand is unplayable anyway), and the drawer's Image has `raycastTarget` **permanently off** — an input-eating rectangle across the bottom of the play area was the thing being removed.
- **2026-09-07 — the cards became a fan.** Designer: *"i dislike the placement, the way cards are seperated in our hand, the draw animation"*. All three traced to one `HorizontalLayoutGroup`.

**What the layout group was actually doing**, measured before it was deleted, because each of these looked like a separate bug:

| symptom | cause |
|---|---|
| `[1]`/`[2]` key hints floating in space above the cards | the group rewrote each card's rect to **200×100** and moved its anchors to top-left, while the art drew 200×300 centred — so the root was nothing like the card you saw, and every child authored against the real card drifted |
| cards unreadably small | the container carried **`localScale` 0.55**, the one move the rules forbid outright. Cards came out 110×165 on a 1080 canvas |
| cards read as loose tiles, not a hand | spacing 50 × 0.55 = a **27px GAP**. Any gap at all reads as separate objects; a hand needs overlap |
| a 4-card recall took ~1.4s to become readable | the deal was **sequential** — each card waited for its own flying ghost plus a delay, sitting at alpha 0 until then — on **scaled** time, instantiating a second full copy of the card prefab per card to throw away |

⚠️ **A LAYOUT GROUP COULD NEVER HAVE OWNED THIS ANYWAY** — it relays rotated children as axis-aligned list items. Same rule that already cost the quest board its slips. `HandUI.SlotFor(i, n)` is now a pure function returning position + tilt, pushed to each card via `CardUI.SetSlot`.

⚠️ **CARDS PIVOT AT THEIR BOTTOM EDGE (0.5, 0), AND THAT IS LOAD-BEARING.** Tilt swings each card about the point a real hand would hold it, and the hover zoom grows the card **upward out of the rail** instead of pushing it through the screen edge. Cards anchor to the rail's own pivot, which makes `anchoredPosition` and the rail's local space the same coordinates — so the draw-pile conversion needs no correction term.

⚠️ **THE SINK MUST NOT LAND IN THE NAME PLATE.** The plate occupies 3.2%–11% of a card's height from the bottom — a band 8–28px above the bottom edge at hand size. A cut inside that band slices the title in half and reads as a rendering fault. The current sink goes well past it; titles read on the card's back.

⚠️ **ONE WRITER FOR ROTATION.** `CardHoverFlip` writes `localRotation` every frame for the turn, so the fan's lean goes through its `ExtraRoll` field rather than being written by `CardUI` — two components writing one transform would be settled by script execution order, which Unity does not define. The hover target counter-rotates the **Y** only; a Z roll is inherited deliberately (a rolled rect still covers its own area, whereas the Y turn narrows it to nothing and drops the pointer off the card). Verified by raycast at 49° and 150° through the turn.

⚠️ **THE HAND IS RECONCILED, NOT REBUILT.** `UpdateHandDisplay` used to Destroy and re-Instantiate everything for any event at all, *including merely selecting a card*. That hid a bug nobody had named: **a freshly built card under a stationary cursor never receives `OnPointerEnter`**, so after playing a card the card now under your mouse would not open until you jiggled the mouse. Only the difference is created or destroyed now, and the card you played is **detached and animated off** rather than duplicated as a ghost.

⚠️ **`CardUI.hasSlot` is what keeps the other screens working.** `DeckViewUI` and `CardChestScreen` use the same prefab and lay their own cards out; they never call `SetSlot` and stay on `LegacyMotion`. Their cards ARE still crushed by their own grid, so the "root measures 200×100" warnings elsewhere in this file remain true **for them** — just not for the hand.

**When opening any full-screen UI panel, call `HandUIDrawer.instance.SetLocked(true)`** and `SetLocked(false)` when closing. ShopManager, QuestBoardScreen and DeckViewUI already do this.

---

