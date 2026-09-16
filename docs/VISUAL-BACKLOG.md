# Till Winter — Visual & UI/UX Backlog

A living list of visual, UI and UX improvements, worked through group by group. Each item states what exists today, then the change.

---

## 1. Highest impact (first five)

- **Done.** **Type scale.** Font sizes are hard-coded per call site (26..150). Define a named scale in the theme (Display/Title/Heading/Body/Label/Caption) with one global multiplier, and route every `UiKit.Label` through it. Shipped as `UiType` (Display 140 / Hero 84 / Big 64 / Title 56 / Heading 44 / Body 34 / Label 28 / Caption 24) with a global `UiType.Scale` multiplier applied inside `UiKit.Label`; 71 literal sizes across the HUD, Winter screen, pause sheets, title scene, away and generation cards, ending and onboarding now use it.
- **Done.** **One sheet shell.** Pause, settings, credits, stats, confirm dialogs, away card and the hint sheet all repeat "dim scrim + rounded box" and appear instantly. Build one shared sheet component with a 0.2 s rise+scale open/close. Shipped as `SheetTransition` (scrim fades, page rises 54 px and scales 0.96 to 1 over 0.2 s, unscaled time), attached to the four pause sheets, the new-game confirm, the away card and the Winter screen's confirm and first-retire sheets.
- **Done.** **Onboarding hand and arrow.** Primitives today (a circle plus a rectangle, and an 18x70 bar). Use real icons from the Kenney Game Icons atlas already in the project. The hand is now a beating dot with an expanding ripple (the Kenney icon set has no hand), and the arrow is the real arrowUp icon from the node atlas, turned over to point down.
- **Done.** **No bloom.** Ripe glow, golden harvest, the Golden Year and node purchases are all emissive, but the only post is colour adjustments and vignette. Add a mild bloom to the season Volume, off on the Low quality tier. A mild bloom (threshold 0.9, intensity 0.55, scatter 0.6, warm tint) was added to the season Volume; it is off on the Low quality tier, which disables post entirely.
- **Done.** **Skill tree branch identity.** Five branches differ only by node ring colour. Add a tinted region behind each branch and a branch name label. Each branch now has a tinted rounded region behind its nodes (branch colour at 10% alpha, padded by 0.85 node sizes) and its localized name above it.

Next: section 2 (world and visual language).

---

## 2. World and visual language

- **Done.** **No outline or rim light.** `TW_Toon` is a two-step ramp only. Add a rim light or an inverted-hull outline pass. A rim light was added to TW_Toon (rim colour, power 3, strength 0.25, scaled by how lit the surface is); no outline pass was needed.
- **Done.** **Sky is a plain gradient quad.** Add two or three slow stylised cloud layers that recolour per season. A SkyClouds view drifts three cloud prefabs high over the farm and the title scene, wrapping around, shadows off.
- **Done.** **The island ends in a hard box.** Hang roots, rocks and soil chunks under it (matters most on the title scene). DioramaView.BuildBlock now tapers the bottom to 55% about the block centre and the block is thicker (1.6), so the island hangs instead of ending in a flat box.
- **Done.** **Shadows are off entirely on the Low tier.** Give characters, trees and the house a soft blob shadow so objects sit on the ground. A TW_Shadow shader (hand-written, unlit, alpha-blended) plus a BlobShadow prefab; apprentices, the house, the trees and the bush get a soft contact disc. The art rule in CLAUDE.md now lists three project shaders.
- **Done.** **No water anywhere, although there is a well.** Add a pond, an irrigation channel or a millstream. A Pond prefab (water disc with a stone rim) sits at the front-left of the island.
- **Done.** **Season changes are colour-only (1.5 s lerp).** Drop leaves in Autumn, settle snow on trees in Winter (the shader already has a snow global), blossom in Spring. The default tree has an autumn twin (tree_default_fall); DioramaView spawns both and swaps them on SeasonChanged. Snow on trees already came from the shader's snow global.
- **Done.** **No life outside the field.** Butterflies, bees, a sparrow or hens, a couple of them changing per season. A CrittersView flies two butterflies (body plus flapping wings) over the field in Spring and Summer; they shrink away in Autumn and Winter.
- **Done.** **Crop middle stages repeat one green leaf model across tiers.** Give tomato, corn and pumpkin their own mid-stage silhouette, at least a different size and tone. Tomato now grows through plant_bush and pumpkin through plant_bushLarge instead of both reusing the same leaf model.
- **Done.** **The camera only refits when the field grows.** Add a slow breathing drift, a millimetric push-in on combo, a slight pull-back in Winter. A slow breath (0.6% over ~3 s), a 3% push-in that follows the combo, and a 5% pull-back in Winter, all damped.
- **Done.** **Every harvest looks the same except golden.** Scale particle count, colour temperature and ring brightness with the combo. The burst scales with the streak (up to +35% size) and its colour warms toward gold.

Next: section 3 (UI infrastructure).

---

## 3. UI infrastructure

- **Done.** **No icon system beyond node icons.** Small icons for coins, seeds, years, harvests, crows and time, used by the stats, winter and away screens. Shipped as `UiIcons`, mapping generation, year, coin, harvest, ring, apprentice, tractor, crow, golden, combo and time to keys in the Kenney icon atlas the skill tree already uses; the statistics sheet now shows them per row.
- **Done.** **No shadow or gradient primitives in `UiKit`.** A soft drop shadow under cards, a thin gradient on top bands. Shipped as `UiKit.Shadow` (draws the same rounded shape behind a card, offset and darkened) and `UiKit.Gradient` plus `Prims.VerticalFadeSprite` for vertical shades; the title scene's own copy was deleted in favour of it.
- **Done.** **Weak button feedback (only a 0.06 s colour tint).** One shared press animation (0.96 scale, click sound, Selection haptic). Shipped as `ButtonFeedback`, added by `UiKit.Button` to every button; the hand-written `Play(SfxId.UiClick)` calls in the Winter screen and the away card were removed so a press clicks once.
- **Done.** **No toggle component.** Settings show on/off as button text. Build a real switch. Shipped as `UiSwitch`, a real switch (rounded track, sliding knob, animated at `UiMotion.Fast`); Vibration and Reduce motion in Settings use it instead of a button whose label said On or Off.
- **Done.** **No scroll view helper.** Stats and credits are fixed height and will overflow. Shipped as `UiKit.ScrollView`, a masked vertical scroller; the statistics and credits sheets are inside one, so longer content no longer overflows.
- **Done.** **Volume sliders show no value and play no sample sound while dragging.** Each row now shows the level as a percentage and plays a short sample sound while dragging (rate-limited to one every 0.12 s).
- **Done.** **Motion is ad hoc per view.** Define shared durations and curves (fast 0.12 s, normal 0.22 s, slow 0.4 s). Shipped as `UiMotion`: Fast 0.12 s, Normal 0.22 s and Slow 0.4 s plus EaseOut/EaseInOut and a damping helper; sheets, switches and button presses all read from it.

Next: section 4 (screen by screen).

---

## 4. Screen by screen

- **Done.** **HUD season bar** is four flat blocks. Add season glyphs, an icon on the progress marker, a hatched frost span; during the frost warning lean on the screen-edge ice rather than the bar. The frost span is now hatched (Prims.HatchSprite) and the progress marker carries a round knob; the season glyphs shipped with section 5.
- **Done.** **HUD coin counter** works; add a small earning-rate readout or a brief rising "+N". A "+N/s" earning rate under the counter, recomputed once a second and hidden outside the year.
- **Done.** **The retire chip appears abruptly.** Give it a flash and a haptic when it unlocks. It now arrives with a scale punch, a solid background and a Light haptic.
- **Done.** **Winter screen is plain text.** Add a small winter vista strip, a year summary (harvests, coins this year) and a hint of the next goal. A warm gradient strip and a year summary line ("This year: N harvests · M coins"). Core counts the year separately (save schema v5: CoinsThisYear, HarvestsThisYear, reset every Spring, v4 fixture migrates with both at zero).
- **Done.** **Skill tree nodes are flat circles.** A filling ring instead of pips, a stronger pulse on affordable nodes, a padlock glyph when locked, an outward wave on purchase. The level is a ring filling clockwise (pips are gone), locked nodes carry the atlas padlock instead of a cross, and a bought node sends a wave outward.
- **Done.** **Node card is text-heavy.** Show the effect as "now -> next" in two columns with a small increase bar. The effect reads "now » next" with a bar showing the step; hidden at max level or when the value does not change.
- **Done.** **Generation card.** Put the farm silhouette or a per-generation seal behind the counting seeds. A stamped seal with the generation's number behind the title, easing in with a slight rotation.
- **Done.** **Away card is fully static.** Fly the coins into the counter and reveal the source lines in sequence. The earned coins now fly into the counter from the card, and the source lines arrive one after another.
- **Done.** **Ending credits are a plain scroll.** Fade lines in groups, end on the game name and a seal. Three groups that fade in, hold and fade out, closing on the game's name inside a seal (the linear scroll is gone).
- **Done.** **Stats screen is two text columns.** Row icons, thin dividers, a highlight on a couple of values. Row icons and dividers shipped with section 3; coins and best combo are now larger and in the accent colour.
- **Done.** **Title scene.** A soft sun glow behind the name, a very slow island sway, snow gathering in Winter, and a "Generation 3, Year 12" line above Continue. A soft glow behind the name, a slowly swaying island, and a "Generation N · Year M" line above Continue read straight from the save file.

Next: section 5 (accessibility and polish).

---

## 5. Accessibility and polish

- **Done.** **Seasons and branches are colour-only.** Branch icons exist; add season glyphs so nothing depends on colour. Four season glyphs (sprout, sun, leaf, snowflake) are drawn in code by Prims.SeasonGlyphSprite and sit above the season bar's segments, so the season reads without colour; branches already had icons.
- **Done.** **Large-text option in settings**, once the type scale has one multiplier. A Large text switch in Settings sets SettingsData.LargeText, which drives the single UiType.Scale multiplier (1.18) applied before any label is built; changing it reloads the scene like a language change.
- **Done.** **Safe area** is applied to the HUD and sheets; verify on notched devices. Sheets now build their page inside a SafeArea rect (the HUD, the title scene and the Winter screen already did), so a notch cannot cut a sheet's buttons.
- **Done.** **A language change reloads the scene with no warning.** Show a short "changing language" transition. The language and text-size switches now cover the screen with a short "Applying…" panel for 0.4 s before the scene reloads.

All five sections are done; new items go at the bottom as they come up.

---

## 6. Follow-up: developer screenshot feedback

- **Done.** **Ripe crop model timing.** The ripe model (orange carrot) could appear on a plot that
  was only 80% through Wet, so an almost-grown plot showed the same model as one ready to harvest
  and the colour read backwards. It now only appears once a plot is actually Ripe; an almost-grown
  plot shows green foliage instead.
- **Done.** **Plot spacing.** A 3x3 field had visible gaps left and right but none above and below,
  because the camera's downward tilt foreshortens gaps in z to nothing. The soil tile is now shorter
  in z than in x (0.94 x 0.86).
- **Done.** **Pond placement.** The pond used to hang over the island's front lip; it now sits fully
  on the island.
- **Done.** **Fence length.** A full-width fence read as off-centre where it crossed the island
  block's taper. The fence is now shortened so it stops clear of the taper.
- **Done.** **Season name legibility.** Season colours sat close to the sky colour behind them,
  relying on an outline to read. The season name now sits on a dark chip instead.
- **Done.** **Bottom of the play screen.** The bottom of the screen sat empty. The retire chip moved
  to the bottom left and the pause button to the bottom right, both in thumb reach.
- **Done.** **Skill tree layout.** Five side-by-side lanes overlapped. The tree is rebuilt as a
  star: the five branches leave one centre 72 degrees apart and grow outward. Each branch is marked
  by a name plate past its outermost node plus a hub in the middle; the old tinted boxes behind
  branches, which overlapped in the centre where every branch begins, are gone.
- **Done.** **Skill tree contrast.** Muted ink and dim edges were hard to read on the dark page;
  both were lightened, and the initial zoom was lowered since the star reaches further out than the
  old row of lanes did.
- **Done.** **Safe area.** Confirmed on every scene: menu, HUD, pause and sheets, winter screen and
  the skill trees inside it.
- **Done.** **Scene transitions.** Menu to game now shows a real loading screen (cover fades in, the
  scene loads asynchronously, cover fades out); opening a skill tree gets a short fade, skipped when
  Reduce motion is on.

---

## 7. Follow-up: developer playtest feedback, round two

- **Done.** **Credits entry path.** Opening credits straight from the main menu and closing it used
  to show Settings, because both entry points set the same flag. Credits now records whether it was
  reached via Settings and retraces that path.
- **Done.** **Ripe plots reading green.** The stage-to-model mapping was already correct; Kenney's
  carrot is a green leafy top with the orange root at soil level, so at the game's camera angle a
  ripe plant looked like a growing one. The ripe model's foliage now warms toward the crop's own
  colour through PaletteBinder.
- **Done.** **Field expansion scenery.** Expanding the field to 4x4/5x5/6x6 scrambled the scenery:
  Rebuild() destroyed the old scenery with Destroy (which only takes effect at end of frame), placed
  the new scenery under the same root, then ran static batching over a root that was already batched
  and still held dying children. Each rebuild now builds into a fresh root.
- **Done.** **Season bar placement.** The season bar and season name moved from the top band into
  their own band below the island, next to the farm they measure; coins and the earning rate stay at
  the top.
- **Done.** **Season name legibility.** The dark chip behind the season name is gone; a new
  UiKit.OutlineStrong (outline plus a soft TMP underlay shadow) keeps it legible over any sky.
- **Done.** **Pond and island size.** The pond moved onto the back strip beside the house instead of
  sitting in front of the field, the island grew (Margin 1.1 -> 1.6), and the camera's ExtraWidth
  grew with it so the wider island is not cropped.
- **Done.** **Plot soil depth.** Plot soil depth reduced further in z so the gap above and below a
  plot matches the gap left and right under the camera's tilt.
- **Done.** **Plot crack variation.** Each plot now rotates and mirrors its baked crack detail
  deterministically from its grid position, so a field no longer reads as one stamped tile repeated.
- **Done.** **End year early.** Players can end a year early from the HUD instead of waiting the
  clock out; it costs the standing crop exactly as frost would.
- **Done.** **Developer panel entry point.** The developer panel's toggle moved out of Settings into
  its own button in the play scene.
- **Done.** **Farm dog and kennel.** A farm dog and kennel beside the house, built from primitives
  because no kit has an animal; tapping the dog pops a heart for two seconds and swallows the tap so
  petting it never waters the plot behind it.
- **Done.** **Skill tree distribution.** The skill tree became an organic distribution rather than a
  rigid five-spoke star: branches curve further round with each layer, siblings fan along an arc, and
  each node carries a small deterministic offset.
- **Done.** **UI font.** UI font swapped from Nunito to Figtree.
- **Done.** **Button styling.** Buttons restyled: a face sitting on a darker lip, so they have a near
  edge and read as pressable.
- **Done.** **Title scene sky.** The title scene now uses the same sky gradient the farm already had;
  it had been clearing to a single flat colour.
- **Done.** **Winter snow amount.** Winter no longer washes everything white (snow amount 1.0 -> 0.55).
- **Done.** **Loading screen.** The loading screen grows a seed while it waits, and an EFS Games
  studio mark plays once per app run.

---

## 8. Follow-up: developer playtest feedback, round three

- **Done.** **Settings switch knob missing.** The knob was created 10 px wide and positioned once,
  before the switch had its size, and never again. It now sizes to the track and re-places itself
  whenever the switch is resized or shown.
- **Done.** **Settings switch row spacing.** Switch rows sat 34 px below their labels and ran into
  the next row; they are centred on their labels like the sliders.
- **Done.** **Button label wrapping.** Button labels wrapped mid-word ("Otoma / tik"); every button
  label now stays on its lines and shrinks to fit instead. Callers that want a smaller label lower
  the maximum size, not the size.
- **Done.** **Slider knob stretch.** Slider knobs were stretched into tall ellipses by the Slider
  component; the knob now lives inside an invisible holder and keeps its size.
- **Done.** **Reset-save and quality buttons.** The reset-save button matches the other buttons;
  quality buttons are aligned with the rest of the column.
- **Done.** **Main menu Generation/Year line.** It was hidden behind the Continue button; it is now
  placed above the primary button whatever the button count.
- **Done.** **Credits font name.** Credits named the old font (Nunito); it now says Figtree, the
  lines are grouped, and the sheet is shorter.
- **Done.** **EFS Games splash never showed.** The first frames after a scene load are long enough
  to finish the fade in one step. Splash and loading-screen fades now advance at most a thirtieth of
  a second per frame; the logo also loads if it was imported as a plain texture.
- **Done.** **Loading screen leaves.** They now grow from the tip of the stem.
- **Done.** **HUD.** The season name stays legible (light text, no longer fading to 35%), the End
  year button sits below the season bar, and the onboarding hint moved below the season band where
  it had covered it.
- **Done.** **Winter screen.** The pause button moved to the top right while Winter is open (it sat
  on top of Next Year); the developer toggle moved clear of the title and hint; the hint caption is
  light text at the top of the tree (it was dark on dark and under the node sheet).
- **Done.** **Node sheet.** The current value grew out of the sheet's left edge (its right edge was
  pinned at x = 24); the coin sits against the price; the description stays above the effect row.
- **Done.** **Retire confirm and stats.** The retire confirm body text no longer starts inside the
  title. Stats: the Continue button no longer touches the last row.
- **Done.** **Skill trees open fully framed.** The view fits and centres the tree's bounds including
  the branch name plates (whose far edge, not centre, sets the padding); the Field branch now starts
  down-left so its curve carries it down the portrait screen instead of flat to the right edge; name
  plates are placed clear of their nodes; the onboarding focus no longer pans branches off screen;
  the zoom floor was lowered to 0.3 so the whole Almanac fits.
- **Done.** **Developer panel.** Slider knobs and the status block no longer overlap.

---

## 9. Visual review, round three

A full-game review from the play-mode screenshot tour, worked through group by group; the developer reviews between groups.

### 9.0 Bugs seen during the review (do these first)

- **Done.** [H] **Coin counter overlap.** The coin counter overlaps the "Year · Generation" line
  when the number grows (e.g. "4,1K"). The line started 30 units inside the coin row; it now sits
  below it, and the earning-rate label moved under the Year line since a long number ran into it
  beside the counter.
- **Done.** [H] **Snow keeps falling after Next Year.** It carries on into Spring. The first fix
  only cleared flakes when a new year began, but the season blend kept emitting snow for the whole
  Winter-to-Spring blend; snow now follows the Winter phase directly (plus a few flakes during the
  frost warning).
- **Done.** [M] **Kennel reads as a second house.** Red roof, too big; the dog disappears into its
  doorway. It is smaller, dark wood with a green roof and a dark doorway; the dog has a light coat
  and stands in front of it.
- **Done.** [M] **Apprentices too big for the plots.** One covers a whole plot. They are 0.52 tall
  instead of 0.72, hats scaled to match.
- **Done.** [M] **End year and pause buttons out of style.** Translucent grey, does not match the
  new button style. Solid like the other buttons; a translucent face on a translucent lip had read
  as a grey smudge.

### 9.1 Visual language

- **Done.** [H] **One documented colour palette.** Menu grey, cream sheets, brown winter and green
  heritage should read as one family (warm earth, cream, sage green, honey). Palette documented in
  `docs/STYLE.md` and applied through a `HudTheme` style bump and a `TreeTheme` style bump: menu
  secondary buttons became earth-coloured instead of translucent near-black, and the away card lost
  its off-palette navy.
- **Done.** [H] **Surface language for UI.** Light texture (paper, wood, cloth) or a thin border and
  inner shadow, a "farm notebook" feel. `UiKit.Card` gives every sheet and dialog a border, shadow
  and paper grain.
- **Done.** [M] **One standard for corner radius, shadow and border** across cards, chips and
  buttons. Card border, shadow and radius are shared constants in `UiKit`.
- **Done.** [M] **Bridge the low-poly 3D and flat 2D UI** (subtle facets or soft shadows in UI). The
  soft card shadow and the lipped buttons are the bridge between the flat UI and the low-poly world.

### 9.2 Play screen (HUD)

- **Done.** [H] **Empty bottom third of the screen.** Today's goal, next crop, or a small "this
  year" panel; or move the island down and enlarge it. A "this year" card at the bottom shows harvests and coins this year plus a progress bar and percentage toward the next generation, written into char buffers so nothing allocates per frame.
- **Done.** [H] **Coin area.** Tighten icon-to-number spacing, glow on the coin, counting animation
  when the number changes. The icon and number are now centred as one unit with the icon riding the number's left edge, a soft glow behind the coin, and the counter counts up instead of jumping; coins in flight land on the icon wherever it is.
- **Done.** [M] **Season bar thin, glyphs small.** A thicker, illustrated timeline. The bar is thicker (HudTheme style 2) with larger season glyphs.
- **Done.** [M] **Frost warning.** Pulse on the bar, a slight blue tint, frost on the field. Already shipped before this round (frost creeping in from the screen edges, the elapsed bar breathing toward frost blue, colder light, and frost spreading over the field through the season snow amount); no change was needed.
- **Done.** [M] **Retire chip.** A clearer badge with a seed icon that glows when ready. It is now a lipped badge with a larger seed and a glow that breathes while the farm can be passed on.
- **Done.** [L] **Combo counter.** Fiery/sparkling number that grows with the streak. It grows and warms from yellow toward a hot red as the streak lengthens (new HudTheme.ComboHot).

### 9.3 Island and 3D world

- **Done.** [H] **Island is a flat green slab.** Grass tufts, flowers and stones on the edges; soil
  layers and roots on the side profile. Grass tufts, flowers and pebbles now line the rim from the
  first generation, placed from a seed so a farm looks the same every load and kept clear of the
  field, fence, path and props. The side profile's soil layers are not done; the torn earth hanging
  under the front edge covers the side view for now.
- **Done in part.** [H] **Empty grass left and right of the field.** Set dressing (hay bale,
  wheelbarrow, watering can, scarecrow, well, coop). Low clusters of tufts and flowers fill it;
  larger set dressing stays with the generation decor so the two cannot collide.
- **Done.** [M] **Underside of the floating island.** Rocks, hanging roots, light mist. Rock and
  roots hang under the tapered front edge, kept small so they stay clear of the season band.
- **Open.** [M] **Fence.** Posts at the corners, a path to the gate, maybe a low fence around the
  field.
- **Done.** [M] **Path tile under the field looks detached.** A path running to the house. The path
  now runs from the field to the rim; with the wider margin it had stopped after one tile.
- **Done, already present.** [M] **Lasting visual rewards as generations pass** (barn, greenhouse,
  windmill), beyond the growing house. `FarmDecorSet` unlocks decor by generation (from generation
  2), which is also why a first-generation island looked empty.
- **Done.** [L] **Background layer.** Hills, tree silhouettes, other islands. Two small floating
  islands with a tree and torn earth under them sit far behind the farm.

### 9.4 Field and crops

- **Done.** [H] **Crops small for their plots.** Scale up or plant 2-4 per plot. Every crop stage
  is planted as a bed: `ArtSetup.Clump` copies one plant onto 4 spots on the two soil ridges
  (`Bed4`), or 2 spots for bushy tomato/pumpkin growing stages (`Bed2`), with per-copy turn and
  ±10% size; all copies share the root PaletteBinder so ripe glow and golden tint still cover the
  whole bed. Ripe carrot is 0.56, corn 1.1, wheat 0.82. Flat Kenney bushes (plant_bush,
  plant_bushLarge) are fitted by width so they stay inside the plot.
- **Done in part.** [H] **Soil is a flat box.** Furrows, a dark sheen when wet, crack texture when
  dry. The Plot prefab now has two raised ridges with lighter crests (same SoilDry slot, so
  Dry/Wet colours follow; tint 1.07/1.13). Cracks and droplets moved onto the ridge height, and the
  crop anchor and the stand height of apprentices, crows and event visitors were raised 0.16 -> 0.2
  to sit on it. A separate wet sheen beyond the existing darker wet colour is still open.
- **Done.** [M] **Each crop with its own identity** (tomato on a stake, tall corn, pumpkin on
  trailing leaves). Tomato is tied to a stake, corn grows as a tall bed, and pumpkin is one large
  fruit on a trailing vine with five leaves (`PumpkinPatch`); the grape vine spans the plot and
  carries two bunches, their binders folded into the vine's so the plot lights them.
- **Done.** [M] **Ripeness signal.** A small sparkle, light ring or bouncing icon in addition to the
  wobble and warm tint. The Plot prefab carries a `RipeMark` gem (two cones, its Crop0 slot
  overridden to the tier colour by PlotView). PlotView measures the ripe bed's height when the crop
  is built and floats the gem just above it; it pops in with EaseOutBack (new
  `Prims.EaseOutBack`), bobs and turns, and hides while the ring is over the plot.
- **Done in part.** [M] **Player's ring.** Rotating dots on the edge, water-drop and sun-ray effects
  under it. A new `RingDots` prefab (10 beads, `VisualCatalog.RingDots`) turns around the ring edge,
  driven by RingView, faster with the combo and shrinking in as the ring fades. Water-drop and
  sun-ray effects under the ring are still open.
- **Open.** [L] **Golden crop.** A clearer light shaft and particle shower.

### 9.5 Characters and creatures

- **Done in part.** [M] **Apprentices.** Smaller, with dig/water/pick animations and small status
  icons. Already smaller (0.52, from 9.3); `ApprenticeView` now bends down to the bed while
  harvesting (28° pitch over the harvest, a small hop as it finishes) and, when standing still,
  breathes and looks about. Status icons still open.
- **Done.** [M] **Dog.** Leaves the kennel, wanders, follows apprentices, chases crows. `DogView`
  rewritten: `DioramaView` gives it a home and a grass strip just behind the fence (`SetArea`); it
  naps, trots between random spots and home (legs swing, body bobs), goes home in Winter, and when
  any crow is on the field it runs to the fence opposite the crow, faces the field and bounces, tail
  wagging fast. Stops while being patted. It barks silently (no bark clip; no generated audio). It
  cannot cross the fence, so it does not physically chase crows.
- **Done in part.** [M] **Crows.** Landing, pecking and flight animations; feathers when scared.
  Landing, pecking, flight and scare feathers already existed; `CrowView` now glances about in
  quick jerks before it settles to eat.
- **Done in part.** [L] **Ambient creatures.** Bees, chickens, a cat, flocks of birds, a frog at the
  pond. A new Bee prefab (two bees in `CrittersView` hop from bed to bed, preferring ripe beds,
  wings buzzing; Spring/Summer only) and a pond Frog (`FrogView`, placed by `DioramaView` beside
  the pond: throat puffs, short hops away and back, hidden in Autumn/Winter). Chickens, cat and
  bird flocks still open (flocks skipped so they are not confused with gameplay crows).

### 9.6 Sky, light, weather and seasons

- **Done in part.** [H] **Sky still plain.** Sun/moon disc, layered clouds, colour through the
  day. `TW_Sky.shader` now draws, besides the gradient: a sun disc with a soft glow
  (`_SunColor`/`_SunPos`/`_SunSize`/`_Aspect`), soft procedural clouds (rotated value-noise fbm,
  drifting, confined to the strip of sky between the island and the HUD so the coin counter stays
  readable; `_Clouds`/`_CloudColor`) and a horizon haze (`_Haze`). All extras default off, so the
  menu's sky is unchanged. `SeasonPresenter` sets them per season: sun tinted from the season
  light, lower in autumn/winter (from the light's elevation), dimmer in frost/winter; cloud cover
  per season (summer 0.3, spring 0.45, autumn 0.6, winter 0.8). Note: the material asset keeps its
  own serialized shader values, so `SeasonPresenter` sets `_SunSize` from code. Moon and colour
  through the day still open.
- **Done in part.** [H] **Season transitions.** Spring blossom, summer haze, autumn leaf wind,
  snow cover arriving gradually. Snow now settles slowly (damped, ~several seconds) and melts
  quickly instead of switching with the 1.5 s light blend; summer gets a light heat haze. Spring
  petals and autumn leaves already existed. Autumn leaf wind still open.
- **Done in part.** [M] **Weather.** Light rain, rainbow, autumn wind, morning mist. Morning mist
  now plays at the start of every year (sky haze plus fog pulled closer and paler, burning off
  over 7 s). Rain cloud event already existed. Rainbow and autumn wind still open.
- **Done in part.** [M] **Light.** Stronger shadow direction, warm rim light, lit house windows on
  winter evenings. Sun shadow strength 0.6 -> 0.72. Farmhouse windows now have their own
  PaletteBinder (child "Windows" in `ArtSetup.BuildHouse`) and `DioramaView` lights them warm
  (Golden colour + GoldenGlow emission): a little in Autumn, fully during the frost warning and
  Winter. Warm rim light still open.
- **Open.** [L] **Starry winter night** behind the Winter screen.

### 9.7 Animation and game feel

- **Done in part.** [H] **Purchases lack a visible change on the farm** (a new plot landing, an
  irrigation pipe appearing). Plots bought in Winter (field expansion) used to pop in behind the
  Winter overlay; `FieldView` now holds the staggered reveal until the Year phase, so they land in
  view when the year starts. New apprentices pop in (EaseOutBack over 0.4 s with a PlotPop puff)
  once the field is in view. Irrigation/sun/other upgrades still have no farm-side visual.
- **Done in part.** [H] **Screen transitions.** Freezing into Winter, thawing out of it.
  `WinterScreen` fades in over `UiMotion.Slow` (0.4 s, was 0.3) and now fades out ("thaw") over the
  same time instead of vanishing; raycasts are released during the fade so the first taps of the
  year are not swallowed. The hard hide stays when the generation card takes over. A real freeze
  effect is still open.
- **Done in part.** [M] **UI micro-interactions.** Selected/hover states, a light bounce when
  sheets open. `SheetTransition` (every sheet) now lands with a small overshoot
  (`Prims.EaseOutBack`) and skips the movement when Reduce motion is on. Selected/hover states
  still open.
- **Done.** [M] **Coin flight.** A pop and colour flash on the counter when coins land. Landing
  coins flash the counter number toward the coin colour along with the existing punch.
- **Open.** [M] **Passing on the farm is the biggest moment.** Island darkens and is reborn; a more
  cinematic generation card.
- **Done in part.** [L] **Idle motion.** Trees swaying, a flag, ripples on the pond.
  `TW_Toon.shader` has a `_Wind` property and a world-space sway (TWSway, grows with height above
  the island top, applied in all four passes so shadows/depth match, works on statically batched
  scenery). `ArtSetup` sets `_Wind` 0.06 on Leaf, LeafDark, Sprout, Flower, Crop0–5 and Golden
  materials, so trees, bushes, grass tufts and crops lean in a slow breeze. Pond ripples and a flag
  still open.

### 9.8 Winter screen and skill tree

- **Done.** [H] **Tree unreadable at overview.** Node icons and "0/3" are tiny; show only state
  colour far away and detail up close. `SkillTreeLayout.RootRadius` 3.2 -> 2.6 (layout tests still
  pass), so the canopy is tighter and fits at a larger zoom. Below `TreeTheme.DetailZoom` (0.55)
  nodes show only their colour and icon; the "0/3" level text and padlocks appear when zoomed in.
- **Done.** [H] **Hub is a plain grey circle.** A trunk, seed or farm emblem, with branches growing
  from it. The grey disc is now an emblem: trunk-coloured ring (new `TreeTheme.Trunk`), a paler
  growth ring, a page-coloured heart with the farmhouse ("home") icon in the accent colour, and a
  soft halo.
- **Done.** [H] **Connection lines thin and straight.** Thick organic branches that grow as nodes
  are bought. `UILines` draws curved (quadratic, per-edge stable bend from a hash of the ids) and
  tapered strips (`Line.WidthB`, `Line.Bend`, `AddCurve`, `SetWidth`, `PointAt`; one continuous
  strip per edge). Thick roots now grow from the hub to every branch's first node (trunk colour,
  branch colour once bought). A bought path thickens (`EdgeWidth` x2 tapering to x1.3); flow sparks
  follow the curve.
- **Done.** [M] **Branch plates too small.** Readable size and a branch icon. 380x80 with a branch
  icon (Hand target, Soil contrast, Field menuGrid, Helpers multiplayer, Calendar scrollHorizontal)
  and Heading-size text; content padding follows the plate size.
- **Done.** [M] **No winter atmosphere.** Light snowfall, window condensation, warm lamp light
  instead of a flat dark page. A warm lamp glow (`TreeTheme.Lamp`, radial gradient) over the top of
  the page and 36 small, faint snowflakes (`TreeTheme.Snowfall`) drifting down behind the tree, off
  with Reduce motion. Window condensation not done.
- **Done.** [M] **Empty space above and below.** A light zoom-in on first open. On the first open
  (no remembered view) the tree settles in from 86% to its fitted zoom over 0.7 s; any touch
  cancels it; skipped with Reduce motion. Remembered tree views saved before this change keep their
  old pan/zoom; the pan clamp keeps the canopy on screen.
- **Done in part.** [M] **Node sheet.** A large node icon and a small preview of what it unlocks. A
  large node badge (branch-coloured ring, node icon) sits top-right under the level. A preview of
  what it unlocks is still open.
- **Open.** [L] **Heritage tree.** A distinct golden/aged texture.

Verified with `ui-tour` (tours 28-30), 204 tests passing.

### 9.9 Menu, splash and loading

- **Done.** [M] **Main menu gap between island and buttons.** Enlarge the island or join logo and
  island. `IslandWidth` 6.4 -> 5.5 (larger island) and `IslandScreenY` 0.6 -> 0.575 in
  `MenuBootstrap`, so the island reaches the buttons.
- **Done in part.** [M] **"Till Winter" title.** A custom logotype (crop letters, snowflake i). A
  two-tone logotype (the last word of `menu.title` in `HudTheme.MenuTitleFrost` via a TMP colour
  tag; rich text enabled on that label only) and an ornament under it: two thin rules with a sprout
  (stem, two leaves, seed) in `HudTheme.MenuOrnament` (Sage). Subtitle moved down to make room.
  Custom letterforms (crop letters, snowflake i) not done.
- **Done.** [M] **Menu buttons dark and heavy.** Tie them to the palette with cream/wood tones.
  Buttons were already Sage/Earth from 9.1; the heavy grey bottom shade is now a light warm veil
  (`MenuShade` = Night at 38%), `HudTheme` style 3 (`UiSetup` restyles the asset).
- **Done.** [L] **Splash.** A light scale/glow animation on the logo. A faint warm radial glow
  behind the EFS Games mark, which settles from 106% scale during the fade-in; the glow breathes
  during the hold.
- **Done in part.** [L] **Loading screen.** Seedling tinted by season, tip text below. A random tip
  under "Loading" from six new strings `tip.0`-`tip.5` (en/tr): ring harvest, scaring crows, combo,
  offline irrigation/helpers, passing the farm on, rain cloud. Season-tinted seedling not done (the
  loader does not know the season).

Verified with `ui-tour` (tours 31-32), tests passing.

### 9.10 Sheets and buttons

- **Done.** [H] **Sheets are plain cream cards.** A title band, thin frame and corner ornaments
  ("notebook/signboard" identity). New `UiKit.SheetDecor(card, bandHeight)` adds a tinted title
  band (colour derived from the card face: toward Earth on light faces, toward Cream on dark
  ones), a thin rule under it with a diamond at each end, and a small diamond in each lower
  corner. Used by every pause sheet (pause, settings, credits, stats; titles moved up into the
  band), the title scene's new-game confirm, the Winter screen's retire confirm and the away card.
- **Done.** [M] **Icons on buttons** (Continue, Settings, Statistics, Main menu). New
  `UiKit.ButtonIcon(button, key)` puts an atlas icon at the left of the face (label stays
  centred): Resume forward, Settings gear, Statistics leaderboardsSimple, Main menu home.
- **Done.** [M] **Pause sheet has empty space.** A summary of year, coins and generation. A
  summary line under the title, "Year {year} · Generation {gen} · {coins} coins" (new string
  `pause.summary`, en/tr), filled when the sheet opens; sheet height 620 -> 700.
- **Open.** [M] **Confirmation dialogs.** Two iconed columns for what is lost and what is gained.
- **Done.** [L] **Settings sections** (Sound, Display, Account) with headings. Headings General
  (language, vibration), Sound (effects, ambience), Display (reduce motion, large text,
  graphics), Farm data (credits, reset) with a rule under each; small bold caps with letter
  spacing. Turkish capitals are made by hand (i -> İ before upper-casing) because culture data
  is not guaranteed in IL2CPP builds. New strings `settings.section.general/sound/display/data`
  (en/tr). Vibration moved from the display block to General. Settings sheet 1480 -> 1720.

Verified with `ui-tour` (tours 33-34), tests passing.

### 9.11 Typography, icons, colour

- **Open.** [M] **Clearer type hierarchy** between titles, body and captions.
- **Open.** [M] **One icon set in a single style** (filled, rounded); the Kenney icons vary in
  weight.
- **Open.** [M] **Large numbers (4,1K)** presented more legibly and with colour.
- **Open.** [L] **A characterful display face for titles**; Figtree stays for body text.

### 9.12 Accessibility and screen sizes

- **Open.** [M] **Colour blindness.** Seasons and ripeness must not rely on colour alone.
- **Open.** [M] **Tablets and short (16:9) screens.** Check the new layouts; extend the UI tour with
  device settings.
- **Open.** [L] **Reduce motion must cover every new animation.**

### 9.13 Storefront

- **Open.** [M] **App icon** that shows the island and winter at a glance.
- **Open.** [L] **Store screenshots**, using the UI tour infrastructure.

---

## Order

1. **Foundations** — type scale, sheet shell, button feedback, motion constants.
2. **Cheap and visible** — onboarding icons, bloom, blob shadows, icon set, season glyphs.
3. **Depth** — tree branches and nodes, winter summary, away/generation card motion.
4. **World enrichment** — clouds, island underside, pond, seasonal trees, small life, camera breathing.
