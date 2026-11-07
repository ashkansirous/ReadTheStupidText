### Slice 290 — Dino: the reading page's sound line as a pill (Batch 50 design)

*Built 2026-11-07, merged as PR #889 (ca6b039f).*

**Touches:** `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/IconView.cs`, `src/DinoReader.App/App.xaml`, `src/DinoReader.App/SoundStatusPill*.cs`, `tests/DinoReader.Tests/**`, `.maestro/dino/**`, `docs/plan/slices/290-dino-reading-sound-pill.md`

## Why

Slice 285 C (merged 2026-11-07, #886) shipped the reading page's sound line with no mock: a plain 14 px `Body`
`Label` (`StatusLabel`, `ReadingPage.xaml` line 58) between the picture and the transport row. The Batch 50 design
arrived after it (user, 2026-11-07: "last night we implemented some parts. the design was late. add this to the
plan/assets/design files, and update anything that was implemented differently"). The design keeps the line's place,
its words and when it shows, and gives it a shape: a pill with an icon, so it reads as a state, not stray text.

## Design

Batch 50 mock, `docs/design/dino-reader/Dino Reader Batch 50 Sound States and Sonos.dc.html`, frames 197-199
(lines 28-121; index in `docs/design/dino-reader/README.md`). Read the values from the copy; never guess them.

| State (`AudioStatusText` today) | Pill | Icon | Text |
|---|---|---|---|
| `Running` with a count (197) | `BrandTint` `#ECE7FF` background, text `BrandEdge` `#3f3bc4` | 24 px progress ring: track `#d6cffc`, arc `Brand` `#5B57E8`, stroke 3, round cap, starts at 12 o'clock, filled `Position / Total`; a small down arrow inside (stroke 1.8, `Brand`) | shipped `ShelfAudio_Getting`, then ` · ` and `Intro_Counter` |
| `Running` without a count | same as 197 | ring with no arc (track only) and the arrow | shipped `ShelfAudio_Getting` |
| `WaitingForWifi` (198) | sand `#F0E8D8` background, text `Body` `#5a5470` | 20 px Wi-Fi (three arcs and a dot, stroke 2.2, `Body`) | shipped `ShelfAudio_WaitingWifi` |
| `WaitingForInternet` | sand, as 198 | the same Wi-Fi icon | shipped `ShelfAudio_WaitingInternet` |
| `Preparing` | sand, as 198 | none | shipped `ShelfAudio_Preparing` |
| no sound (199) | sand, as 198 | 20 px muted speaker (speaker and an x, stroke 2.2, `Body`) | shipped `Reading_NoSound` |

Shape, all states: centred, `min-height` 36, corner radius 18, padding 6/14/6/8 with the ring and 6/14/6/10 with a
20 px icon or none, gap 8 between icon and text, 14 px Fredoka weight 500, line height 1.3, max width the page minus
14 px each side, text wraps inside the pill. The row above it keeps today's place: 12 px above the pill, 14 px each
side, transport row below. No red, no button: nothing on this child's page asks the child to act. The language name in
197 is shown in its own script (the mock sets Farsi in Vazirmatn); use whatever the app already does for a native
language name in a Fredoka line.

## What differs from what Slice 285 C built

- **Shape:** a plain grey `Label` → the pill above (two colour styles, three icons, a progress ring).
- **Count:** built `"<Getting line> 3 of 15"` (a space) → mock `"<Getting line> · 3 of 15"` (a middle dot with spaces).
- **Words:** unchanged. The mock caption quotes "Getting {0} *sound* for your books"; the shipped English string is
  "Getting {0} *audio* for your books" and other screens use it, so the shipped words stay (design note in
  `docs/design/dino-reader/README.md`, Batch 50).
- **When it shows:** unchanged; it already matches frames 197-199 (shown only while a read runs and the current piece
  has no clip, gone on `PieceAudio(true)`; "no sound" only when no shelf run covers the book).

## Work

- [x] Android and iOS (shared MAUI code; the Windows head gets it too): replace `StatusLabel` with a pill view
      (`Border` + icon + `Label`), keep `AutomationId="ReadingAudioStatus"` on the element whose text the flows read.
- [x] `AudioStatusText` returns the state as data (kind + text + progress), not only a string; the view picks colours
      and icon from the kind. Backend stays copy-free; the text still comes from `AppResources`.
- [x] Icons: add `Wifi` and `SoundOff` to `IconKind` (paths from the mock), and a progress ring (reuse
      `HoldRingDrawable` if it fits, else a small drawable) that redraws when the run's position changes.
- Why the ring is its own drawable (`SoundStatusPillRing`), not `HoldRingDrawable`: the mock's ring is a 24-unit ring with a down arrow inside and a track, unlike `HoldRingDrawable`'s hold ring, so reuse did not fit.
- [x] Colour token for the sand pill `#F0E8D8` in `App.xaml` if none exists; no hex in code.
- [x] The count joins with ` · ` (a named constant or a resource, not a literal in the method).
- [x] Unit test for the state-to-kind mapping (pure).
- [x] Emulator: the existing `.maestro/dino/language-change-fetches-audio` and `silent-piece-beat` flows still pass
      (they read `ReadingAudioStatus`); a screenshot of 199 compared with the mock (197 not captured, see below).

## Built (2026-11-07)

- New `SoundStatusPill.cs` (Border, ring, IconView, Label; `ReadingAudioStatus` on the label), pure
  `SoundStatusPillState.cs` (kind, count, `CountSeparator` " · "), `SoundStatusPillRing.cs`; `IconKind.Wifi` and
  `SoundOff` (filled r=1 Wi-Fi dot); tokens `SoundPillSand` and `RingTrack` in `App.xaml`/`DinoTokens.cs`. Row pinned
  left-to-right (icon leading), label text follows its own language direction; icons hidden from the screen reader.
- Reviews: high `D:\ReadTheStupidText-worktrees\reviews\290-high.md` (1, 2, 3, 5, 6, 8, 9, 10 applied; 4 skipped: the
  app ships no medium Fredoka weight; 7 skipped, reason below); medium `D:\ReadTheStupidText-worktrees\reviews\290-medium.md`
  (1 high, construction order, and 2, 4, 5 applied; 3 skipped, the view is a fixed 24 units; 6 skipped, the count
  matches the shipped counter).
- The ring is its own drawable, not `HoldRingDrawable`: the mock's ring is a 24-unit ring with a download arrow inside.
- Fast checks: DinoReader.Tests 3777 passed, 1 skipped; Dino Android build 0 errors.
- Emulator-5554, 2026-11-07: `language-change-fetches-audio` PASS, `silent-piece-beat` PASS. Screenshot of the
  no-sound pill (`reviews/290-device/silent-piece-no-sound.png`) matches frame 199 by eye: sand pill, muted speaker
  left, text hugging, centred between picture and transport row.

## Not yet verified

- The "Getting … audio · N of M" pill (frame 197): lilac colour, the ring filling, the " · " count. No screenshot was
  taken while it showed (the run's screen capture started after that flow; the emulator was then held by another run).
- The Wi-Fi pill (frame 198) and the Preparing pill on a device.
- A Farsi or Arabic pill whose text wraps (label right-aligned, icon still left).
- iPhone and the Windows head: not built here; shared code with no platform branches.

## Left from the check pass (`D:\ReadTheStupidText-worktrees\reviews\290-check.md`, both low, not applied)

- A light/bedtime colour switch while the Getting pill shows and its text does not change leaves the ring on the old
  colours until the next count (`SoundStatusPill.cs` early return skips the ring's `Invalidate`).
- `ReadingPage.xaml.cs` ~1111: two null checks on the story that could drift apart (cleanup).

## Verification

- Fast checks: `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`, the Dino Android build, FallbackGuard.
- Emulator, after both reviews: the two flows above, plus screenshots of the getting and no-sound pills.
- iOS: compile only from Windows; an iPhone look goes to *Not yet verified*.
