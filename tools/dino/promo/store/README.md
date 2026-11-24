# Dino Reader store media (Slice 433)

HyperFrames project for the App Store preview videos and the Google Play feature graphic. Palette and font are the app's own
(`src/Dino/DinoReader.App/App.xaml` Brand, Sand, Ink; `Resources/Fonts/Fredoka.ttf`). Real app footage only.

| File | Output |
| --- | --- |
| `index.html` | iPhone preview, 886 x 1920 portrait, 20 s (the note's video timeline: `docs/plan/notes/2026-11-23-dino-store-screenshots-and-video.md`) |
| `variants/ipad.html` | iPad preview, 1600 x 1200 landscape, the same timeline |
| `variants/feature-graphic.html` | Play feature graphic, 1024 x 500 |
| `footage-cuts.json` | per clip, the planned length (`seconds`) and the fixed lead-in (`leadIn`, the recorder and Maestro start-up) trimmed off its own recording |
| `store-media.mjs` | `cut` (ffmpeg, recordings to `assets/footage/<device>/`) and `encode` (Apple spec) |
| `flatten-still.ps1` | feature graphic frame to 24-bit PNG, then `store-size-check.ps1 -Target play-feature` |

## Steps

1. Take the footage: `store-screenshots.yml` (Dino, device `iphone-6.9` and `ipad-13`) records one clip at a time: a setup flow `.maestro/dino/store-shot-ios-videoprep-<clip>.yaml` runs unrecorded, then `simctl recordVideo` starts and `store-shot-ios-video-<clip>.yaml` runs alone, so each mp4 holds exactly one clip (never one recording cut by time from its end). They land in the `store-shots-dino-<device>-<run id>` draft release as `store-dino-<device>-video-<clip>.mp4`. Download them (`gh release download`).
2. `node store-media.mjs cut iphone <folder>` and `... cut ipad <folder>` trim `leadIn` off each recording and fail when a recording is shorter than `leadIn + seconds`. Look at the cuts on a contact sheet and tune `leadIn` in `footage-cuts.json` until each clip shows what its caption says (no shot over about 4 s).
3. Check, then render: `npm run check`, `npm run render:iphone`, `npm run render:ipad` (run the CLI through the HyperFrames plugin launcher, not directly).
4. Encode to Apple's spec: `node store-media.mjs encode iphone renders/iphone.mp4 out/iphone-preview.mp4`, then `pwsh -NoProfile -File ../../../emulator/store-size-check.ps1 -Target preview-iphone -Path out/iphone-preview.mp4` (and `ipad`). The 5 s poster frame lands on the character-voices shot.
5. Feature graphic: `npm run render:feature`, then `pwsh -NoProfile -File flatten-still.ps1 -Frames renders/feature-frames -Out <file>.png`.

`npm run check` reports `missing_local_asset` until the footage of step 2 is in `assets/footage/`; the two warnings about a dense root timeline and a nested end frame are kept: the preview is one composition on purpose.
