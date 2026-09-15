# Device checklist

A template the developer fills out on an iPhone 12 and an older Android phone, then pastes into
the PR that touches build/runtime code. Use a `-dev` build so the debug panel is available.

## Build

- Version + build number: _____ (from the debug panel of a `-dev` build, or the build's file name)
- Device model: _____
- OS version: _____
- Auto-selected quality tier and reason: _____

## Checks

| # | Check | How | iPhone 12 | Android | Notes |
|---|---|---|---|---|---|
| 1 | Install | Sideload the build | | | |
| 2 | Cold start time | Launch to first playable frame; stopwatch or debug panel | | | |
| 3 | First year with a thumb | Report the ring offset that feels right from the debug panel slider (default 0.80) | | | |
| 4 | Frost warning readable in sunlight | Play the last 10 s of Autumn outdoors | | | |
| 5 | Safe area | Nothing under notch / home indicator / status bar | | | |
| 6 | No ring input when a finger rests on the HUD | Rest a finger on the top band while dragging elsewhere | | | |
| 7 | Crow tap fires once | Tap a crow, confirm a single scare + payout | | | |
| 8 | Winter tree pinch/zoom | Pinch and drag the Almanac/Heritage tree | | | |
| 9 | Buy a node | Confirm haptic Medium | | | |
| 10 | Retire | Confirm haptic Heavy + shake | | | |
| 11 | Relaunch after > 1 min | Away card appears | | | |
| 12 | Incoming call or app switch under 60 s mid-year | Resumes exactly, no away card | | | |
| 13 | Over 60 s | Away card appears | | | |
| 14 | 20-minute play | Frame time at start and after 20 min, device warmth, quality tier | | | |
| 15 | Battery | Winter screen runs at 30 fps | | | |
| 16 | Haptics felt | Ring harvest (Light), golden (Medium), purchase (Medium), crow tap (Selection) | | | |
| 17 | Android back button | In Winter closes the topmost sheet, then does nothing; in-year does nothing | | | |
| 18 | Android haptics fallback | API 25 device: fixed-length fallback; API 26+: amplitude path | | | |
| 19 | iOS home-indicator swipe during a sweep | Needs a second swipe, doesn't interrupt the sweep | | | |
| 20 | iOS haptics plugin compiles | Build in Xcode, confirm no errors from `TillWinterHaptics.mm` | | | |
| 21 | Stripped release build loads an existing save | Install a release build over a save from a dev build | | | |

## How to report

Paste the filled table into the PR description. Note any crash with the time it happened and
what was on screen.
