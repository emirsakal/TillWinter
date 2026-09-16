# Third-party art

All kits below are by Kenney (www.kenney.nl), licensed **CC0 1.0** (public domain). Each kit folder keeps its original `License.txt`. Only the files a prefab references were imported; the rest of each kit was stripped (counts in `DECISIONS.md`, Session 6).

| Kit | Folder | Used for |
|---|---|---|
| Nature Kit 2.1 | `Kenney/nature-kit/` | crops (carrot, corn, pumpkin, wheat, sprouts), trees, bushes, rocks, fences, path, flowers, logs, mushrooms, stump |
| Food Kit 2.0 | `Kenney/food-kit/` | tomato, grapes, barrel (+ `colormap.png`) |
| Mini Characters 1.x | `Kenney/mini-characters/` | apprentices (six characters, + `colormap.png`) |
| Game Icons | `Kenney/game-icons/` | Almanac / Heritage node icons (white 2x PNGs, packed into `NodeIcons` atlas) |

Everything else (house, well, windmill, greenhouse, tractor, crow, cloud, plot, soil block) is built from primitives inside prefabs by `ArtSetup` and needs no credit.

# Fonts

Both are Google Fonts under the **SIL Open Font License 1.1**, which permits embedding in a published app on Google Play and the App Store. Each keeps its licence text next to it.

| Font | File | Licence text |
|---|---|---|
| Nunito | `../Fonts/Nunito-Variable.ttf` | `../Fonts/OFL.txt` |
| Figtree | `../Fonts/Figtree-SemiBold.ttf` | `../Fonts/OFL-Figtree.txt` |

A candidate font must cover the Turkish alphabet (`Ç ç Ğ ğ İ ı Ö ö Ş ş Ü ü`) before it can be used — the UI ships in EN and TR, and a face missing them sets Turkish words half in the fallback font.

# First-party

The EFS Games studio mark (`../TillWinter/Unity/Resources/efs-logo.png`) is the developer's own work and needs no third-party credit.
