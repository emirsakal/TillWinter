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

- **Partly done.** **HUD season bar** is four flat blocks. Add season glyphs, an icon on the progress marker, a hatched frost span; during the frost warning lean on the screen-edge ice rather than the bar. The frost span is now hatched (Prims.HatchSprite) and the progress marker carries a round knob; the season glyphs move to section 5, because the Kenney icon set has no season icons and they will be drawn there with the other generated glyphs.
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

- **Seasons and branches are colour-only.** Branch icons exist; add season glyphs so nothing depends on colour.
- **Large-text option in settings**, once the type scale has one multiplier.
- **Safe area** is applied to the HUD and sheets; verify on notched devices.
- **A language change reloads the scene with no warning.** Show a short "changing language" transition.

---

## Order

1. **Foundations** — type scale, sheet shell, button feedback, motion constants.
2. **Cheap and visible** — onboarding icons, bloom, blob shadows, icon set, season glyphs.
3. **Depth** — tree branches and nodes, winter summary, away/generation card motion.
4. **World enrichment** — clouds, island underside, pond, seasonal trees, small life, camera breathing.
