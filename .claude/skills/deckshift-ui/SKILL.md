---
name: deckshift-ui
description: Deckshift's UI design system — the Salvage material system every screen now follows (it replaced the old one-material-per-screen rule), linear-colour-space calibration, uGUI layout traps, pause/HUD wiring, and a pre-delivery checklist. Use when building, restyling, reviewing or debugging ANY screen, panel, HUD element, card face, world-space marker or UI VFX in this project.
---

# Deckshift UI

Everything here was paid for with a real mistake in this codebase. Rules without
reasons get "improved" back into bugs — so every rule carries its why. Do not
strip them.

**Read §1 before choosing a look. Read §2–3 before writing pixels. Run §7 before
calling a screen done.**

---

## 1. The house style — highest priority

### The failure mode has a name

The first FlatUI pass delivered the literal brief ("soothing, simple,
understandable, but also cool") as flat slate-blue panels, uniform rounded
corners, neutral greys, one accent. The designer's verdict: **"it screams AI."**

That was correct. It was the house style of every dev dashboard, and it had no
*place* in it. **Simple and generic are not the same thing.** Competent, safe,
professional flat design is the failure state, not the goal.

What fixed it was pointing every choice at the world:

- **Warm charcoal, not slate-blue** — the district is the *Oxidation District*. Rust,
  not brushed steel. This single palette shift did most of the work. ⚠️ **Superseded
  for colour by Salvage law 3:** measured dungeon stone is `#444548` cool-neutral, and
  "warm charcoal" was reasoned from the district's name. The lesson that survives is
  "point the palette at the world", not the specific hue.
- **Chamfered corners, not rounded** — cut plate reads as a made object; a
  uniform corner radius reads as a web card. Biggest silhouette cue.
- **Directional light** — a lit top lip plus ember glow rising off the *bottom*
  edge (firelight under the bench). Uneven light = physical object in a place.
- **Rivets and faint scuffs** — fasteners, not jewels. Imperfection is what
  kills the generated feel.
- **Rules score across and fade at the ends**, never edge-to-edge like a CSS
  border.

### ⛔ SUPERSEDED 2026-08-20 — SALVAGE REPLACES EVERYTHING IN THIS SUBSECTION

**Do not build a new screen from the table below.** It is kept only so you can
read a screen that has not been migrated yet, and so nobody re-derives the rule
it encodes. That rule was:

> Every screen gets its own material. Screens share the ideology and **never the
> same skin**. Pick a material and invert something.

It did exactly what it says, and what it says is *make the screens look unlike
each other* — nine invented materials (smoked glass, brass, frost…) and a hue
budget that ran out. **Every settings screen built under it was rejected, and
the rule, not the execution, is why.** Designer, 2026-08-20:

> "i want a settings menu, a pause menu, a blompo UI/VFX, the shop, the forge,
> the map, and every other UI asset … to feel like they would have been in the
> cainos packs. i want consistency in the visuals overall, not seperated to
> menus and the actual gameplay, but everything."

**The replacement is `Salvage.cs` — read it before any UI work.** Its thesis, and
the thing both the old rule and the obvious fix get wrong:

⚠️ **CONSISTENCY LIVES IN THE TREATMENT, NOT THE SUBSTRATE.** One substrate
everywhere is *not* the answer — that reads as monotony, and this project has the
receipt: **Vigil** was stone alcoves with real dungeon art and a torch per alcove,
and it was rejected **twice**. But look at the Cainos dungeon pack itself: crates,
pots, bottles, banners, chains, skeletons, candles, fireplaces — wildly different
materials, reading as one world. Not because it is all stone. Because everything
in it obeys the same handful of laws.

So: screens may be made of anything the dungeon contains; they may not disagree
about these five.

| | law |
|---|---|
| **1 · Scale** | `Salvage.Scale` = **2.4107** — 14 world units over a 1080 canvas at 32 PPU. UI art is the exact size the same art is in the game. `Salvage.SpritePPU` enforces it, so no screen has to remember. Deliberately non-integer: the *world* already displays at this scale, so 2× or 3× would make UI pixels visibly a different size from world pixels. |
| **2 · Light** | Warm, from the **upper left**, always. The old system had Iron lit from below, Arcane from above, Halt edges-inward and Bulletin from the left — four screens, four suns. |
| **3 · Colour** | **Sampled from the pack PNGs, never chosen.** `SalvageArtBaker` → `SalvageArt` ramps. Measured dungeon stone is **`#444548` cool-neutral**; the old palette reasoned "warm charcoal, rust not brushed steel" from the *district's name* and was simply wrong against the art. The warmth in this game comes from wood (`#401D13`) and torchlight, never from the walls. |
| **4 · Accent** | **Exactly two in the whole game.** `Salvage.Torch` (lit / present) and `Salvage.Shift` (energised / live — the altar orb's exact cyan, the same colour that seals the gate). **The hue budget stops existing; no screen ever spends a colour again.** `Salvage.Wound` red is a *warning*, not an accent, and is the only permitted third. |
| **5 · Wear** | Used **and repaired** — not pristine, not derelict. The world's repair currency is literally called scrap. |

**Variety then comes from WHAT THE OBJECT IS**, which is a property of the
screen's purpose rather than a colour someone picked: a hung sheet, a notice
board, a workbench, a banner, paper pinned across a grate.

⚠️ **THE ONE FREE RESOURCE NOBODY WAS USING:** `Assets/Cainos/Pixel Art Icon
Pack - RPG` holds **107 icons, 89 of them referenced nowhere in the project** —
Heart, Gear, Scroll, Map, Chest, three Keys, Coins, Rune Stone, Book, Lantern,
gems, ingots. Same artist, same 32 PPU, same palette. Reach for these before
drawing another procedural sigil.

**Migration status (measured 2026-09-24 by which screens build from `Salvage` /
`SalvageSurfaces`):** on Salvage are `PauseScreen` + `SettingsScreen` (one hanging board),
`ScrapForgeScreen`, `QuestBoardScreen` and `BossRewardScreen`; `BlompoScreen` borrows the
Forge's Salvage surfaces and colours. Still on the old table below: `ShopScreenUI`,
`RunMapScreen`, `CharacterSelectScreen`, `CardChestScreen`, `RelicSwapScreen` and
`RelicManagePanel`.

---

<details>
<summary>The superseded per-screen material table (reference only)</summary>

Screens share the *ideology* — flat procedural plates, restraint, directional
light, a subtle particle drift, one meaningful accent — and **never the same
skin**. The material should say what the place DOES.

| | **Iron** (ScrapForge) | **Arcane** (Blompo) | **Loadout** (relics) | **Halt** (pause) | **Apparatus** (settings) | **Bulletin** (quests) | **Cartograph** (run map) | **Marquee** (character select) |
|---|---|---|---|---|---|---|---|---|
| What it is | a workbench | a blessing granted | what you're carrying | the moment you stopped | the machine's own panel | contracts you promise | a folded map you just opened | the billing before you go on |
| Palette | warm charcoal | cold indigo | near-**colourless** | cold blue-black | smoked glass + arc-cyan | dark wood + **pale paper** | **tan paper** + oxblood | near-black + **the CHARACTER's colour** |
| Light | fire from **below** | descends from **above** | none | from **edges inward** | **emitted by the content** | rakes in from the **left** | even, with aged corners | one key light on the hero |
| Particles | embers **rising** | motes **settling** | none | **suspended**, shivering | none — one scan sweep | none — the content sways | none — paper doesn't move | **speed streaks** tearing past |
| Corner marks | rivets | four-point stars | none | none | calibration crosshairs | brass tacks | **compass rose** | none — raked livery bars |
| Surface | scuffed | pristine | plain sockets | **crazed** | unblemished glass | **perforated** | **stained, foxed, folded** | none — no plate at all |

The Marketplace (`ShopScreenUI`) keeps its own: warm wood, striped awning,
lamplight.

⚠️ **A MATERIAL ALONE IS NOT ENOUGH — the map proved it twice.** It was given flat
slate, then acid-etched copper, both carefully lit, and both were rejected as
still reading like a diagram. What fixed it was making it a **DOCUMENT**: paper
instead of a panel (the sheet IS the window, torn edge, no frame), fold creases,
**dashed** trails instead of solid lines, and the player's own progress drawn
over the print in red pen by a visibly different hand. When a screen depicts a
THING that exists in the world, ask what that thing has been *through*, not just
what it is made of.

</details>

### The inversions are the point

⚠️ **Under Salvage the inversions still matter, but they may no longer spend a
HUE.** Light direction is fixed and the palette is sampled, so what separates two
screens is the OBJECT, its silhouette and its motion. Worked example: the pause
screen is the only one that **drops in from above and is hauled back up** — every
other screen opens in place — and it is the only one that can show you the world
behind it. Two separations, no colour spent.

⚠️ **AND MOTION CAN SURVIVE A MATERIAL IT WAS DESIGNED FOR.** Pause was cloth
first, and the designer's verdict was *"i like the animation … the way it comes
from the top, but i just dont like the panel itself"*. The drop, the swing and
the lift-away were kept verbatim and only the SURFACE was swapped for planks.
When a screen is half-right, find out which half before rebuilding either.

Warm/cold. Below/above. Rising/falling. Worn/pristine. Still/moving.
Inside/outside the fiction.

**When adding a screen, pick a material and invert something. Do not retint
Iron.**

⚠️ **The strongest available inversion is VALUE, not hue.** Every screen except
Bulletin is a dark plate with light text. Bulletin is a dark board with **pale
paper pinned to it** — its `TextBright` is nearly black, and bright and dark have
swapped places. That one structural choice makes it unmistakable while claiming
almost no colour. **Reach for this before reaching for another hue.**

⚠️ **MARQUEE (character select) is the other proof, and the cheapest inversion
yet: THE THEME OWNS NO ACCENT.** Every other screen has one fixed accent that
identifies a PLACE. Marquee is about an IDENTITY, so it takes the *character's*
colour and the whole frame cross-fades when the selection moves — colour there is
the **selection signal**, not the theme signature. It costs nothing from the
budget below, and it makes the choice feel consequential before a word is read.

⚠️ **AND A SCREEN REJECTED TWICE HAS A MEANING PROBLEM, NOT A DRESSING PROBLEM.**
Marquee replaced **Vigil**, a cold hall of stone alcoves where the roster stood
dormant and a travelling lamp woke only the chosen one. Vigil's second pass gave
it real dungeon art, a torch per alcove and a diegetic flame — better dressing,
same rejection. The fault was that its whole vocabulary was **DORMANCY** on the
one screen in the game that is pure hype: the last beat before the run starts.
**Ask what a screen is SAYING before you improve how it looks.**

⚠️ **The hue budget is nearly spent.** Claimed: orange (Iron), violet (Arcane),
no-hue (Loadout), **tan paper + oxblood (map — Cartograph)**, warm wood/amber
(shop), frost blue (Halt), arc-cyan (Apparatus), deep wax red (Bulletin), and
**no fixed hue (Marquee** — but its ROSTER palette spends jade, magenta, gold and
ice). Roughly yellow remains for a *place*. After that, **stop reaching for a
colour and invert a different axis** — light direction, motion vocabulary,
surface treatment and value structure separate these screens at least as much as
hue does. Loadout and Marquee both prove a theme can carry no fixed hue at all.

⚠️ **The map is CARTOGRAPH — paper, not metal.** An earlier verdigris-and-copper
etched-plate version was built and REJECTED: a material is not enough, because a
map reads as a map by being a DOCUMENT (torn deckle edge, fold creases, dashed
routes, progress annotated in red pen over printed brown ink). If a doc still
describes the map as verdigris, it is stale.

### Motion has a vocabulary too, and it must not contradict itself

Blompo's blessing animation was originally a hammer-and-anvil forging: strikes,
sparks, screen shake. Once his screen went arcane that fought everything on the
panel — he grants a charm, he isn't a blacksmith. The rebuild inverted it:

> forging → strikes, impacts, gravity, sparks flying **out**, the window rattling
> binding → orbit, convergence, weightlessness, motes drawn **in**, nothing hit

**When a beat feels weak, check whether it contradicts the sequence's own
vocabulary before reaching for more particles.** The settle beat was called bland
because it *expanded* while everything else converged; pressing a seal inward
fixed it.

⚠️ **AN EFFECT MUST COME FROM WHAT THE THING MEANS, NOT FROM A SHAPE.** Standing
instruction from the designer (2026-08-20), after a chalk ring that circled the
exit door on first sight was cut as "too basic … i don't think it's even a good
idea to have them at all", with: **be more creative with animations and effects.**

The reflex to avoid is the *generic reveal* — a ring that expands, an outline that
pulses, a glow that pops in, a shape drawn around the thing you want noticed. They
are interchangeable, they carry no information, and they would fit any game.

The test that replaced it: **what does this object already MEAN in this game, and
what does the thing that acts on it mean?** Worked example from the same day — the
gate. It was animated as a realistic medieval door (groan, strain, brown dust,
1.6s) while the `ShiftAltar` was firing a glowing **cyan orb of Shift** across the
room that burst on it. Cause and effect were in two different genres. The rebuild
made the gate **sealed with Shift**: a cyan hairline breathing in the join, which
flares and shatters when the orb lands, in the altar's exact colour. Same event,
but now it says something — *this is locked, Shift is what locks it, and Shift is
what just broke it* — and it doubles as gameplay information that sends the player
looking for the switch.

**Ask what is causing the effect and answer in that thing's own language.** If the
effect would work equally well on a chest, a door and a menu button, it is the
generic reveal wearing a costume.

### A permanent overlay must not compete with the game behind it

Loadout is the quietest theme by weight because the relic bar sits over gameplay
forever and the relic art is colourful pixel work. **Do not add a hue to the
relic bar.** Same reason the quest tracker slips are a third of the board's sway,
with no grain, fold or perforation.

### A screen with a person in it needs the person to talk back

The shop's brief was "make the player feel like they are talking to a person who
is trying to sell them stuff." Barks split by EVENT (greet / browse-card /
browse-relic / too-poor / bought / farewell), fired from hover, purchase,
refusal. **Affordability outranks item type** — being told you can't afford it is
more useful than a joke about what it does. Speech **types out**; a line that
snaps in whole reads as a label changing. Body language stays tiny (a portrait
that lurches pulls focus off the prices). **No line repeats back-to-back** —
with pools this small, plain randomness repeats constantly and that's what makes
barks feel canned.

### Sound is part of the screen

Four families, separated by physics, not by taste:

- **Magic** — harmonic (bell partials 1, 2, 3, 4, 5.1)
- **Metal** — inharmonic (bar modes 1, 2.76, 5.40, 8.93)
- **Paper** — no pitched component at all
- **Halt** — defined by ENVELOPE: the only sound that gets **choked**, a damper
  clamping the ring away in 180ms. A sound that fades says "ending"; a sound cut
  short says "held".

Keep them distinct so a blessing and a scrap pickup are never confusable.

---

## 2. Calibration — you cannot compute a colour here, you must measure it

⚠️ **The project renders in LINEAR colour space.** A small alpha of a bright
saturated colour over a dark panel lands **much** higher in sRGB than the
arithmetic says. Measured: a selection plate at alpha **0.065** arc-cyan came out
near **0.36 sRGB** and filled the whole row with a teal slab. It had to drop to
0.03.

⚠️ **World sprites also render through a 0.5-intensity global `Light2D`.** The
scene halves your value before you see it. A 0.42 deep-rock tint measured ~0.21
on screen and the mass read as a pit. **Multiply by the light, then pick.**

Consequences, all load-bearing:

- **Pick every subtle alpha by screenshot, never by reasoning about the value.**
- **The brighter and more saturated the colour, the worse the gap.** Halt's frost
  blue at 0.07 is a restrained plate; Apparatus's cyan at 0.065 was a slab.
  **Do not copy an alpha across themes.**
- **Atmosphere wants roughly half the alpha you first reach for.** Embers at
  0.085/140px were an orange wash owning the bottom third; ~0.05 over 120px is
  firelight. Scuffs at 0.045 read as *rendering glitches*; 0.022 reads as wear.
- **Hairlines need to be brighter than theory says.** The quest board's header
  rule was invisible at `T.Border` and had to move to `EdgeLight`.
- **Rules are 2px, not 1.** Cards render at ~0.8 scale in the hand, so a 1px rule
  is 0.8 device pixels and visibility is subpixel luck. Two identical rules were
  drawn by identical code and only one appeared.
- **A watermark is an OUTLINE, small and faint.** A filled diamond at 56% card
  width and 0.055 alpha measured `#231E12` against a `#0D0D0D` ground — three
  times the ground's value — and read as an olive blob the text sat on. A
  watermark has to survive being ignored.
- **A dark dot cannot mark anything on a dark surface.** The quest board's pin
  holes read because of a pale crescent on the side away from the lamp, drawn
  OVER a wider dark smudge. Rim-only reads as dust; dark-only is invisible.

### ⚠️ A LIGHT GROUND INVERTS EVERY RULE ABOVE

Everything above assumes a dark plate. `Cartograph` is paper, and on paper the
same instincts are wrong in the opposite direction. Measured while building it:

- **A subtle bright mark has almost no headroom.** The fold highlight had to drop
  from 0.20 to **0.055** — at 0.20 the sheet came out with three glowing lines
  ruled across it, reading as laser guides rather than creases. On a dark plate a
  low bright alpha blooms; on a light one it just becomes a drawn line.
- **A subtle DARK mark washes out instead of reading as subtle.** The compass was
  completely invisible as a large 0.115 watermark. It needed to be **smaller and
  roughly three times stronger** to register at all. This is the exact inverse of
  "atmosphere wants half the alpha you reach for".
- **The masking trick flips too, and gets better.** On a dark screen, hiding a
  line behind a label needs a visible plate. On paper the mask can be the GROUND
  ITSELF — a paper-coloured blob is invisible except for what it hides.

The rule underneath all three: **contrast against the ground is what matters, not
the alpha.** Never carry a value across from a dark theme to a light one.

### Generating a Salvage surface — five traps, all paid for on the pause screen

⚠️ **EVERY CAINOS SPRITE HAS A 1PX DARK OUTLINE, AND IT IS A DIFFERENT MATERIAL
FROM THE THING IT OUTLINES.** Sampling a sprite rect swallows it. `Cloth 08` is
grey linen (`#97918A`) inside a solid brown (`#563B25`) border on all four sides,
so the first linen ramp had a brown bottom third and every shadowed part of the
sheet came out blotched. `SalvageArtBaker` insets **2px** (the corners are
stepped, so the outline is two pixels thick diagonally). If a baked ramp ever
looks contaminated, this is why.

⚠️ **A RAMP CARRIES THE MATERIAL; A MULTIPLIER CARRIES THE FORM.** Do not shade
by walking the ramp. Measured, linen spans `#8A8179`..`#A29B91` — about **22
luminance levels out of 255** — so driving folds, key light and drape through
`Sample()` produced a sheet as flat as poured concrete. `Sample()` picks *which*
linen; a `shade` float decides how lit it is.

⚠️ **SMOOTH GRADIENTS READ AS SHEET METAL.** Carrying the form in wide soft
gradients made cloth look like brushed steel. Matte surfaces need a fine crumple
broken into the shade itself so the surface never resolves into a clean gradient,
plus a few **hard** creases — the sharp lines are what the eye reads as fabric.

⚠️ **A RADIAL FALLOFF STRETCHED TO A MENU ROW BECOMES A STREAK, NOT A BAND.**
A 64×64 radial blob at 600×62 rendered as a horizontal smear with a hot core and
read as a lens flare lying across the menu. Any soft shape that will be stretched
to a very different aspect must fall off on each axis **independently**, with a
flat plateau (`SalvageSurfaces.Edge`).

⚠️ **A DASHED RECTANGLE ENCLOSING NOTHING IS MARCHING ANTS.** The stitched patch
was a perfect rect whose interior was 8% brighter than the sheet, so all that was
visible was its dashed border — it read as a UI selection box left on screen.
Wear has to be a visibly **different** piece of material, with a boundary that
wobbles and stitches spaced irregularly.

⚠️ **AND WEAR GOES WHERE THE LAYOUT IS EMPTY AT EVERY CONTENT LENGTH.** The mend
sits bottom-left because the menu bottoms out around v 0.67 and the stat column
around v 0.70. A stain behind a column of numbers reads as a rendering fault.

### Rarity must separate on three channels at once

The first palette was amber / violet / azure / cool-slate and the tiers were
indistinguishable — three sat in the blue-violet quadrant at near-identical
**luminance**, so the only cue was a ~40° hue step. Invisible on a small sigil,
and gone entirely for a colour-blind player. `FlatUI.RarityColor` now separates
on:

1. **Hue** spread right around the wheel (neutral → **green** → violet → amber)
2. **Luminance** strictly ascending (0.42 → 0.56 → 0.66 → 0.82) so a better item
   is literally brighter and the order survives greyscale
3. **Saturation** climbing from near-zero

Plus **shape**: `FlatUI.RaritySigil` progresses bare ring → ring + 4 rays → ring
+ 6 rays + inner ring → the full `ArcaneSigil`. Shape reads faster than hue and
survives greyscale, colour-blindness and a 40px icon.

**Common is deliberately muted** — at a lighter slate it rendered near-white and
made the *weakest* offer the brightest thing on screen.

On the relic bar, rarity is a **solid coloured strip** along the socket bottom,
not a tinted hairline: at 52px over moving gameplay a hairline is not reliably
readable. **Only Epic/Legendary animate** — that's what makes a Legendary catch
your eye in a row of five.

⚠️ **A STATUS COLOUR MUST BE MEASURED AGAINST THE SURFACE IT APPEARS ON, not
chosen for its meaning.** Two digits on the canonical card frame were invisible
for exactly this reason, and both would have shipped: the Shift cost was
`(0.307, 0.304, 0.934)` on a crystal sampling `(0.377, 0.398, 0.920)` — the same
blue — and the *last charge* warning was painted `Color.red` on a medallion that
**is a red ball**. Red means danger, and it is the one colour that cannot say so
there. Sample the artwork, then pick.

⚠️ **Card rarity is the ART's job, not the UI's.** Card art bakes rarity in as
colour (dark grey Common, light grey Uncommon, yellow Rare, purple Epic; no
Legendary cards). **Never invent a second rarity colour system on a card** — two
colour codes that disagree is worse than one. The blessing mark is therefore
**one fixed teal on every blessing**, and blessing hierarchy moved to a channel
the art doesn't use: only Epic/Legendary blessings pulse.

---

## 3. Composition

- **Detail placed exactly on another element disappears.** `ArcaneSeal`'s four
  diamond glyphs sat at the inner ring's radius and merged into it invisibly;
  they now punctuate the outer ring on the diagonals, clear of the twelve ticks.
- **Keep wear out of content columns.** The first scuff pass ran a streak through
  the title. Grain belongs in bands the layout leaves **empty at any content
  count** — one placed between the hint and the LEAVE button instantly read as a
  divider rule nobody asked for.
- ⚠️ **A glow that doesn't reach its container's edge must fade on that axis
  too, or it draws its own border.** The bottom glow reused `VerticalFade` (Y
  falloff only) inset 14px from the sides, and its hard left/right ends produced
  a visible seam down both edges. That's what `BottomGlow()` exists for.
- **An emblem needs STRUCTURE or it reads as a lens flare.** A four-point sparkle
  behind a big soft glow looks cheap. `ArcaneSigil` works because of a containing
  ring, rays of two lengths, and ticks outside the ring — plus a much tighter,
  dimmer glow, since the haze was doing most of the damage.
- **Small icons inside dense text don't work.** A 17px scrap shard beside each
  cost read as a smudge fused to the first digit. The accent colour alone
  carries it.
- **Empty states must collapse.** `LayoutSections` lays out top-down and resizes
  the window to its content, so an empty section shrinks to one explanatory line.
  The fixed-height version had two large voids and looked broken — and that state
  is *common* (early in a run nothing is damaged or exhausted).
- **Show the numbers a decision depends on.** Blompo's card picker listed a bare
  charge count — no Shift cost, no maximum — so you permanently altered a card
  without seeing what it cost or how much life it had.
- **Labels must be narrower than they look like they need.** Map nodes sit about
  one column apart minus jitter; 150px labels collided on any full floor.
- **Text pivots.** A label positioned by offset with a centred pivot places its
  BOX centre, so a 34px-tall label put its first line back over the glyph it was
  labelling. Pivot to top or bottom whenever the offset is meant to clear
  something.
- **The rotation pivot carries the metaphor.** Quest slips rotate about a point
  under their TACK (`SLIP_PIVOT_Y = 0.94`), which is the entire reason the sway
  reads as paper hanging from a pin rather than a card wobbling in space. One
  line.
- **Stillness can be the selection signal.** Hovering a quest slip *stops* its
  sway and lifts it — it becomes the only motionless thing on the board, which is
  clearer than any highlight. (Safe from flip-flop because the slip grows on
  hover, keeping the cursor inside it.)
- **Put the decoration where the content isn't.** The wax seal goes **top-right**
  of a slip: the bottom holds the payout block and progress bar, and a 100px blob
  covers both. The top corner is the only region empty at every content length.

---

## 4. uGUI mechanics — the traps

- ⚠️ **NEVER SCALE UI CONTAINERS — RESIZE THEM.** Changing Scale cascades to
  children and fights Layout Groups, producing wildly wrong sizes. The honest fix
  is always Width/Height, sometimes anchor/pivot. Leave Scale at (1,1,1).
- ⚠️ **`Image.Type` defaults to `Simple`.** A 26px 9-sliced outline left at
  Simple was stretched across a 1040×780 window and rendered as an enormous soft
  octagon hanging outside the panel. Any FlatUI `Panel`/`Outline` at panel scale
  **must** be set to `Image.Type.Sliced` explicitly.
- ⚠️ **Every CanvasScaler is `ScaleWithScreenSize`, ref 1920×1080,
  `matchWidthOrHeight = 1` (HEIGHT). Do not change the match value.** The camera
  is height-anchored (`orthographicSize = 7` ⇒ 14 world units tall at every
  aspect), so matching width makes the UI do the opposite of the camera — at 21:9
  the canvas became 810 logical px tall instead of 1080 and clipped 170px off the
  run map.
- **An element at a screen EDGE must be ANCHORED to that edge.** A centre-anchored
  element at a large offset drifts as canvas width varies. `RecallButton` was
  anchored to centre at x = −859.2 and got cut in half on a 1728-wide canvas.
- **Two fit strategies, not interchangeable.** *Resize* the window only when its
  content reflows (RunMapScreen's chart is anchored to the window corners).
  *Uniform scale, never above 1* when content sits at fixed offsets from centre
  (Blompo, Settings, Shop) — resizing those overlaps their own columns.
- ⚠️ **Two objects that track each other by position must agree on their ANCHORS
  first.** Copying `anchoredPosition` is not enough: a slip anchored TOP and its
  shadow anchored CENTRE shared a position measured from origins 300px apart, and
  every shadow rendered as a free-floating black rectangle mid-screen.
- ⚠️ **`anchoredPosition` places the PIVOT.** With a pivot at the pin in the
  top-left corner, positioning at x = 0 hangs 90% of the strip to the right of
  the anchor. Back the offset out explicitly.
- ⚠️ **A Layout Group can never own rotated or custom-pivoted children.** A
  `VerticalLayoutGroup` relaid every quest slip *and every slip's shadow* as
  separate list items, spacing rows at 176 instead of 94. Build such rows into a
  dedicated child layer with no layout components, and disable any inherited
  group in `Start()`.
- ⚠️ **UI children are NOT clipped, so FX geometry is bounded by the WINDOW, not
  the stage.** A 520px ring scattered runes outside the panel onto the backdrop.
  Check available room in both axes; squash in Y if the stage is off-centre.
- ⚠️ **`preserveAspect` MEANS THE ART IS NOT THE HOST.** Anything stamped at a
  fraction of the host rect lands off the artwork the moment the sprite's aspect
  differs from the box. The card set has two generations — 1024×1536 (0.667) and
  118×200 (0.590) — so the newer art letterboxes to **88.5%** of the host width
  and every medallion number drifted outward off its socket. Measure against the
  DRAWN size (`CardFace.DrawnArtSize`), never the host.
- ⚠️ **TO CENTRE SOMETHING ON A SHAPE, MEASURE THE RENDERED FRAME — not the
  source art.** Scanning the sprite for strongly-coloured pixels finds a
  saturated disc fine, but a shape that tapers to dark, desaturated tips (a
  diamond, a gem, a flame) loses its ends to the colour test, and the "centre"
  comes out shifted. The card's Shift digit ended up 14.5px low on a 900px card
  — 8% of the medallion's height — from exactly this. Render it, capture, and
  measure the SHAPE and the THING YOU ARE CENTRING in the same image; that
  answers "is it on it?" directly instead of inferring it through a chain of
  rect maths.
- ⚠️ **Use a ROW-WIDTH PROFILE, not a bounding box or a centroid.** Circles and
  diamonds both reach their widest row exactly at their vertical centre, so the
  peak row is the answer — and unlike a bbox it is not inflated by a rim, and
  unlike a centroid it is not dragged by interior highlights. On one ball the
  three methods gave x = 814 (bbox), 811.1 (centroid) and 811 (profile mode);
  the profile was the symmetric, correct one. Read a capture back with
  `File.ReadAllBytes` + `Texture2D.LoadImage` to sample pixels.
- ⚠️ **Know when the residual is the GLYPH and stop.** After correcting, ~2px on
  a 900px card remained — that is each digit's own bearing (worst case 0.63px
  vertical at hand size, measured from the font asset's glyph metrics), it
  differs per digit, and "fixing" it over-fits to whichever number you tested.
- ⚠️ **A TWO-DIGIT NUMBER IS ~1.93× THE WIDTH OF ONE DIGIT.** Measured in the
  display face at 100pt: widest digit `0` = 58.2px, `10` = 110.8, `99` = 112.2,
  `100` = 176.0, `∞` = 70.9. Any socket, badge or medallion drawn for one digit
  will be overflowed by two — and it will not be noticed until one value in the
  whole game reaches 10. **Fit the string, don't trust the authored size.**
- ⚠️ **Fit it DETERMINISTICALLY, not with `enableAutoSizing`.** Auto-size settles
  over several frames (§4 above), so a label rebuilt every refresh can render at
  the wrong size on the frame that matters. Scale from a measured glyph-width
  constant instead and it is correct immediately.
- **Textures need `FilterMode.Bilinear`** — Point aliases chamfer edges badly.
- **Get the SDF right.** Rounded box is `inside + outside − radius`; the chamfer
  is that box distance `max`'d with a normalised diagonal half-plane. Naive
  versions pinch the outline at corners.
- **All FlatUI shapes are WHITE and tinted via `Image.color`**, so one cached
  sprite serves every panel.
- ⚠️ **TMP auto-size does not settle within one frame** — you cannot batch-measure
  it. Setting `text` + `ForceMeshUpdate` in a loop gives sticky, wrong numbers
  (one pass reported `textBounds.size.y` of −4294967000). Measure ONE string per
  frame, read `textInfo.lineInfo[]` ascender/descender rather than `textBounds`,
  or drive a real card through `Setup` and read it the following frame.
- **The auto-size CEILING is the design; the floor is a safety net.** Do not
  widen the ceiling to give short strings bigger text — one card rendering at
  twice the size of a wordy one reads as broken, not as emphasis. Shorten the
  copy instead.
- **`UIEmberField` must use `Time.unscaledDeltaTime`** (every screen it belongs
  on pauses the game) **and re-read the parent rect every frame** (window heights
  are dynamic; a bounds snapshot leaves embers outside a collapsed panel).

---

## 5. Wiring — a screen that doesn't integrate is broken

> ### ⚠️ Start here: extend `GameScreen`, and set text through `UIType`
>
> **`GameScreen` (`Assets/Scripts/GameScreen.cs`) already implements most of this section.**
> A new screen calls `AcquireDisplay()` from its Show and `ReleaseDisplay()` from its Hide, and
> gets the pause, game-state, HUD-hide and drawer-lock handover correct by construction — plus
> `FindRootCanvas()`, both aspect-fit modes, the one-frame Escape memory (`UIHeldPauseLastFrame`)
> and an unscaled-time `FadeGroup`. It does **not** own activation, because `PauseScreen`'s root
> must stay active to catch its own Escape; screens keep their own Show/Hide.
>
> **`UIType` decides the face.** `UIType.Apply(text, role)` for the display face — titles,
> headings, buttons, stat labels, numbers. `UIType.ApplyProse(text, role)` for **real sentences
> only**; the display face has essentially no lowercase and renders prose as a wall of capitals.
> Sizes are roles (Hero/Title/Heading/Label/Body/Caption), never magic numbers, and prose is
> auto-compensated ×1.18 for Pixie's smaller cap height — **never hand-tune a size to compensate.**
>
> ⚠️ **Judge any type decision on a screen with SENTENCES in it.** The pause screen looks like the
> obvious test as the densest screen and is nearly useless for it — 30 labels and numbers, almost
> no prose. The quest board decided the current split.
>
> ⚠️ **A thin face on a LIGHT ground needs darker ink than the number suggests** — §2's calibration
> rule applies to type too. The quest slip's body ink had to go 0.189 → 0.141 against paper at 0.75
> when it moved to the prose face, because thinner strokes cover less area at the same value.
>
> Existing screens migrate when already being touched — **do not retrofit them all at once.**
> `QuestBoardScreen` is the worked example for both.

- **Pause through the counter, never `Time.timeScale` directly.**
  `GameManager.instance.RequestPause()` / `ReleasePause()`. Documented exceptions:
  `HitStop`, Adrenaline slow-mo, and hard resets before a scene transition.
- **`GameManager.IsUIPaused` is the single honest "is another screen up?" test.**
  Every modal routes through `RequestPause`, so one property covers all of them
  and cannot fall behind when a screen is added. **Prefer it over a hand-kept
  list of `SomeScreen.IsOpen` flags** — that pattern has rotted twice here.
- ⚠️ **And it needs a ONE-FRAME MEMORY, not just a live read.** Script execution
  order is undefined, so on the frame a shop closes on Escape it may release its
  pause *before* the pause screen's `Update` runs — opening the pause screen
  instantly behind the screen just dismissed. Refuse to open if any UI held the
  pause on the **previous** frame.
- **Hide `GameplayHUD` when a full-screen panel opens**, and call
  `HandUIDrawer.instance.SetLocked(true)` / `(false)`. The drawer's Image has
  `raycastTarget` on to detect hover, so it absorbs clicks in its rect until
  locked.
- **Self-bootstrapping singletons must register with `SceneBootstrap.Register`**,
  and `Create` must be idempotent. `[RuntimeInitializeOnLoadMethod]` runs **once
  per session, not once per scene** — a scene-local self-bootstrapped manager is
  destroyed by the first scene load and never returns.
- **A screen's root GameObject stays ACTIVE; only its `Content` child toggles.**
  `Update` has to run to catch the key that *opens* it, and a deactivated
  GameObject gets no `Update`. When a sub-panel borrows the display, drop the
  CanvasGroup's alpha rather than deactivating anything.
- ⚠️ **Anything a screen creates OUTSIDE its own hierarchy will NOT be hidden with
  it.** The character select's live rigs are world objects 3000 units out, so
  hiding the screen left one camera per character rendering a 420×614 target
  every frame for the rest of the session. Switch them off explicitly in `Hide`.
- ⚠️ **A static `instance` and a scene object can DISAGREE, so adopt-then-verify.**
  Anything that clears statics without destroying scene objects — an editor domain
  reload is the everyday one — leaves the field null while the old screen sits in
  the Canvas still running. Building on top of that stacks screens (measured:
  **three screens, six character plots**). But re-finding it is only half the fix:
  a domain reload also **resets every non-serialized field**, so the adopted
  component comes back with its collections EMPTY while its built children survive.
  That renders the old hierarchy while driving none of it. **Check the screen is
  actually built before trusting it, and rebuild if not.**
- **Restore only what you hid.** Turning every child back on is not the inverse
  of hiding them — some children are supposed to be off. Record the visible set
  when hiding and restore exactly that. (One card flip resurrected three
  deliberately-disabled children and came back wearing a grey overlay reading
  "New Text".)
- **Read the set of children on each change, never cache it in `Awake`.** Other
  systems parent things onto UI afterwards; an `Awake` snapshot left a bonus
  badge rendering mirrored as "+1 TFIHS".
- ⚠️ **A SETTING MUST DO SOMETHING.** Never add a row without a consumer — a
  slider that moves and changes nothing is worse than an absent feature, because
  the player stops trusting the ones that work. Name the consumer in a comment.
- **Scale a global effect at its ONE chokepoint**, not at its 23 call sites, so a
  later addition cannot forget to respect the setting. A zero setting must
  **return before touching state** — a zero-length freeze that still sets
  `timeScale = 0` for a frame is a visible hitch.
- **Re-read values from source on every refresh** rather than mirroring them in
  widget state — rows affect each other, and RESET changes everything at once.
- **Keyboard navigation must skip disabled rows**, and a click anywhere on a
  slider track should jump the value there (grabbing a 3px handle is miserable).
- **One shared hint line** describing the selected row beats N permanent captions
  burying the controls.
- **Destructive entries are two-step**: first activation arms and relabels,
  second commits, and moving away or ~4s of silence disarms.

---

## 6. Verification — this project's #1 bug shape is the silent no-op

**Code that looks correct, runs without error, and does nothing.** The gravity
warning flash was "fixed" twice this way and stayed invisible for months.

- ⚠️ **Setting a shader property that doesn't exist fails SILENTLY.** Dump the
  property list before writing any material effect. On the PLAYER rig tint via
  **`_Alpha`** (the only handle every Cainos rig shader shares — "Alpha Cut"
  exposes no colour property at all); on ENEMIES `_Color` is fine. Prefer
  `HasProperty` + an explicit fallback over `HasProperty` + silently skipping — a
  guarded skip still produces "nothing happens", which *is* the bug.
- ⚠️ **A UI raycast test must let a FRAME PASS after building the UI.**
  `GraphicRaycaster` skips any graphic whose `Graphic.depth == -1`, and `depth`
  is only assigned on a render pass — so a UI built inside one tool call is
  invisible to `EventSystem.RaycastAll` in that same call. Open the screen in one
  call, raycast in the NEXT. This produced five convincing false MISSes.
- ⚠️ **POINTER BEHAVIOUR CANNOT BE VERIFIED BY CALLING `OnPointerEnter`
  YOURSELF.** Invoking the handler never produces the *exit* that breaks things,
  so every test passes while real hovering is unusable. Verify **geometrically**:
  build a `PointerEventData` at the cursor position and run
  `EventSystem.current.RaycastAll` at each animation angle.
- ⚠️ **Deferred `Destroy` survives to end of frame.**
  `GetComponentsInChildren<Button>(true)` still returns the previous chart's
  buttons, whose listeners point at unreachable nodes — a convincing false
  "callback never fired". Filter on `activeInHierarchy`, or check in the NEXT
  tool call.
- **Screenshot recipe (Unity MCP, 2026-09-24):** `editor_play` → `capture_game_view`
  with **`source: "screen"`** and a `save_path` → `Read` the PNG straight away (the
  file exists when the call returns) → `editor_stop`. ⚠️ **`save_path` is relative to
  `Assets/`**, so it creates imported files there; save into a scratch folder and
  `delete_asset` it afterwards. ⚠️ The default `source: "camera"` **omits every Screen
  Space Overlay canvas**, which is every screen in this project; it is useless for UI.
  Full recipe and traps: CLAUDE.md → Workflow Notes → Unity MCP.
- ⚠️ **`Texture2D.ReadPixels` and `CaptureScreenshotAsTexture()` do NOT read the game
  framebuffer from a one-shot code call (`eval`, formerly `execute_code`)**. It returned
  a uniform flat grey for a screen that was demonstrably on display. To sample exact
  pixel values, capture to a PNG and load that back as a texture.
- ⚠️ **NEVER WRITE A MEASURED CLAIM INTO A COMMENT YOU HAVE NOT MEASURED.** A
  header in `RunMapScreen` asserted that a layout change cut edge crossings from
  ~9 per act to under 1. Measured afterwards over 300 generated acts: crossings
  were **zero before and zero after** — the change did something else entirely
  (sideways travel per edge, 214px → 86px). A confident false number in a comment
  is worse than no comment, because the next person plans around it.
- ⚠️ **Verify a fix against the FAILURE, not the mechanism.** A soft radial was
  added behind labels to hide lines crossing them, and it looked like a fix — but
  a radial is an ELLIPSE, so it was nearly transparent at exactly the left and
  right ends where the lines actually cross. Test the thing the user complained
  about, not the thing you built.
- ⚠️ **Trust only the real framebuffer.** A manual `camera.Render()` into a
  RenderTexture can sort differently from the URP pipeline and has produced a
  false "this is fixed" image.
- **When a field mysteriously stops working, check for a scene-instance prefab
  override before touching the prefab or the code.** Run **Deckshift → Audit
  Prefab Overrides**. Fix findings with
  `PrefabUtility.RevertPropertyOverride`, never by re-typing the value (that
  creates a new PINNED override).
- **Verify at more than one aspect.** 4:3 (1440×1080), 16:10, 16:9, 21:9
  (2560×1080). Every change should be a no-op at 1920×1080.

---

## 7. Pre-delivery checklist

Run this before saying a screen is done.

**Identity**
- [ ] Does it have a MATERIAL, and does that material say what the place does?
- [ ] What did I invert relative to the nearest existing screen? (name it)
- [ ] Did I reach for a new hue when a value/light/motion inversion would do?
- [ ] Would a stranger call this generic? If unsure, it is.

**Calibration** *(by screenshot, not arithmetic)*
- [ ] Every subtle alpha eyeballed on screen, not computed
- [ ] No alpha copied across themes
- [ ] Hairlines and rules actually visible (2px, brighter than theory)
- [ ] Rarity separates on hue + luminance + saturation + shape
- [ ] Nothing dark placed on a dark surface without a pale rim

**Composition**
- [ ] Empty state collapses rather than leaving voids
- [ ] Every number a decision depends on is on screen
- [ ] Wear/grain only in bands empty at any content count
- [ ] No detail sitting exactly on another element
- [ ] Glows fade on both axes if inset from the edge

**Mechanics**
- [ ] All Scale values (1,1,1); sizing done with Width/Height
- [ ] Every 9-sliced Image explicitly `Image.Type.Sliced`
- [ ] Edge-hugging elements anchored to that edge
- [ ] Paired objects share anchors, not just positions
- [ ] Window fits a narrow aspect (resize *or* uniform scale — the right one)
- [ ] FX geometry inside the window bounds

**Wiring**
- [ ] Pause via `RequestPause`/`ReleasePause`
- [ ] `GameplayHUD` hidden, `HandUIDrawer.SetLocked(true)`
- [ ] Escape checked against `IsUIPaused` with a one-frame memory
- [ ] All motion on `unscaledDeltaTime`
- [ ] Any new setting has a named consumer
- [ ] Root stays active; only `Content` toggles

**Verification**
- [ ] Screenshotted in Play mode from the real framebuffer
- [ ] Pointer behaviour tested geometrically, one frame after building
- [ ] Checked at 4:3, 16:9 and 21:9; no-op at 1920×1080
- [ ] Any material property confirmed to exist on that shader

---


---

## 8. The screens that already exist

**Moved to its own skill: `/deckshift-screens`.** It was 63% of this file (75KB of
120KB) and it is a CATALOGUE, not method — so every session that invoked this skill
to learn *how to build a screen* was paying for the full per-screen history of every
screen already built.

Invoke it only when you are touching a NAMED existing screen and need to know what it
is made of and which traps it already paid for. Everything above is what you need to
build a new one.
