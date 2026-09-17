# Till Winter — Visual Style Reference

The colour family every screen draws from: warm earth, cream paper, sage green and honey, with brick for danger and plum for heritage seeds. Source values live in `Assets/TillWinter/Unity/UiPalette.cs`.

## Palette

| Name | RGB (0–1, as in code) | Hex (approx.) | Role |
|---|---|---|---|
| Ink | 0.18, 0.14, 0.105 | `#2E241B` | Body text and line work |
| InkMuted | Ink at 65% alpha | `#2E241B` @ 65% | Secondary text |
| Paper | 0.965, 0.92, 0.84 | `#F6EBD6` | Sheets and dialogs |
| Cream | 1, 0.97, 0.9 | `#FFF7E6` | Text on dark or coloured surfaces |
| Sage | 0.42, 0.6, 0.35 | `#6B9959` | Primary actions |
| Honey | 0.91, 0.66, 0.24 | `#E8A83D` | Highlights, progress, coins |
| Earth | 0.6, 0.48, 0.36 | `#997A5C` | Secondary actions |
| Brick | 0.74, 0.32, 0.24 | `#BD523D` | Destructive actions |
| Plum | 0.55, 0.42, 0.74 | `#8C6BBD` | Heritage and seeds |
| Night | 0.16, 0.135, 0.12 | `#29221F` | Dark surfaces, e.g. the Winter page and cards over the field |
| Dim | 0.1, 0.08, 0.06 at 72% alpha | `#1A140F` @ 72% | Behind a sheet |

## How colours flow

Views never read `UiPalette` directly. `HudTheme` and `TreeTheme` are ScriptableObject assets; `UiPalette` feeds them through `HudTheme.ApplyPalette` and `TreeTheme`'s restyle path, which `ui-setup.bat` applies when an asset's `StyleVersion` is behind `CurrentStyle`. Serialized assets ignore changed field defaults, so a colour change means a style bump, and new theme fields are safe because they take their default.

## Cards

Every sheet and dialog is built with `UiKit.Card`: a rounded border (`UiKit.CardBorder` = 5 reference px) a shade darker than a light face or lighter than a dark one, a soft shadow (`UiKit.CardShadow` = 10 px) below, and a faint tiled paper grain on the face — 5% on light faces, 1.2% on dark ones, where more reads as static.

## Buttons

A face sits on a darker lip (`UiKit.LipColor`). The label stays on one line and shrinks to fit rather than wrapping; callers that want a smaller label lower `fontSizeMax`, not `fontSize`. Faces and lips are solid colours only — a translucent face over a translucent lip reads as a grey smudge.

`UiKit.Label` tightens letter spacing (-1.5) at Title size and above, opens it slightly (+1) at Caption size and below, and adds line spacing (+6) at Body size and below so wrapped paragraphs breathe; large numbers use `RichNumber` to shrink and colour the unit suffix (e.g. the "K" in "4,1K") without any per-frame string allocation.

## Type

Figtree is the body face everywhere. Titles and big numbers — anything `UiKit.Label` sets at
Title size and above — use the display face, Rammetto One, through `UiKit.DisplayFont`. A
candidate display font must pass the Turkish cmap check (Ğ ğ İ ı Ş ş Ö ö Ü ü Ç ç) before it is
considered; several were rejected for missing glyphs before Rammetto One was picked.

## Checking layout

Both canvas scalers (game/menu and loading) use CanvasScaler Expand, and `CameraRig.FitSize` /
the title scene camera never show less height than `CameraRig.ReferenceAspect` (1080/2340), so
every screen keeps the reference layout on wider or taller aspects instead of cropping or
stretching.

`ui-tour.bat <folder>` captures every screen and sheet; pass a preset as a second argument to
shoot other screen shapes (16:9, tablet).
