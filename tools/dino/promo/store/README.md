# Dino Reader store media (Slice 433)

HyperFrames project for the App Store preview videos and the Google Play feature graphic. Palette and font are the app's own
(`src/Dino/DinoReader.App/App.xaml` Brand, Sand, Ink; `Resources/Fonts/Fredoka.ttf`). Real app footage only.

| File | Output |
| --- | --- |
| `index.html` | iPhone preview, 886 x 1920 portrait, 20 s (the note's video timeline: `docs/plan/notes/2026-11-23-dino-store-screenshots-and-video.md`) |
| `variants/ipad.html` | iPad preview, 1600 x 1200 landscape, the same timeline |
| `variants/feature-graphic.html` | Play feature graphic, 1024 x 500 |
| `footage-cuts.json` | per clip, `markStep` (which recorded step's end is the mark), `beforeMark` (seconds kept before the mark: the cut starts at `mark - beforeMark`) and the planned length (`seconds`) |
| `store-media.mjs` | `mark` and `check` (run on CI by `ios-sim-store-shots.sh`), `cut` (ffmpeg, recordings to `assets/footage/<device>/`) and `encode` (Apple spec) |
| `flatten-still.ps1` | feature graphic frame to 24-bit PNG, then `store-size-check.ps1 -Target play-feature` |

## Steps

1. Take the footage: `store-screenshots.yml -f media=clips` (Dino, device `iphone-6.9` and `ipad-13`) runs every clip in one Maestro invocation; each clip is a generated flow: its setup `.maestro/dino/store-shot-ios-videoprep-<clip>.yaml` (unrecorded), Maestro's `startRecording`, the steps of `store-shot-ios-video-<clip>.yaml`, `stopRecording`, so each mp4 holds exactly one clip. They land in the `store-clips-dino-<device>-<run id>` draft release as `store-dino-<device>-video-<clip>.mp4`, each with a `.mark` file (two lines: the mark, the end of the clip's `markStep`-th recorded step, and the span, how long the recorder ran; both in seconds from the end of `startRecording`, from the flow's `commands.json`). Download both (`gh release download`).
2. `node store-media.mjs cut iphone <folder>` and `... cut ipad <folder>` cut each recording from `mark - beforeMark` (never below 0) for `seconds` at 30 fps, holding the still tail the file does not hold (simctl ends the file at the last screen change), and fail when a recording has no two-line `.mark` or its recorder ran less than the cut start + `seconds`. Look at the cuts on a contact sheet and tune `markStep` and `beforeMark` in `footage-cuts.json` until each clip shows what its caption says (no shot over about 4 s).
3. Check, then render: `npm run check`, `npm run render:iphone`, `npm run render:ipad` (run the CLI through the HyperFrames plugin launcher, not directly).
4. Encode to Apple's spec: `node store-media.mjs encode iphone renders/iphone.mp4 out/iphone-preview.mp4`, then `pwsh -NoProfile -File ../../../emulator/store-size-check.ps1 -Target preview-iphone -Path out/iphone-preview.mp4` (and `ipad`). The 5 s poster frame lands on the character-voices shot.
5. Feature graphic: `npm run render:feature`, then `pwsh -NoProfile -File flatten-still.ps1 -Frames renders/feature-frames -Out <file>.png`.

`npm run check` reports `missing_local_asset` until the footage of step 2 is in `assets/footage/`; the two warnings about a dense root timeline and a nested end frame are kept: the preview is one composition on purpose.
