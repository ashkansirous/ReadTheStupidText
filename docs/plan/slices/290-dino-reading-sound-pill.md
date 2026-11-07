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

## Part B — exactly as designed (built 2026-11-07, branch feat/s290b-dino-sound-pill-as-designed)

User, 2026-11-07: "The design is always the right choice! do everything as designed. fix the small issues as well".
Part A kept two things that differ from the mock and left three small issues. Part B builds the mock exactly.

**Touches:** `src/DinoReader.App/SoundStatusPill*.cs`, `src/DinoReader.App/SoundPillText.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.App/Resources/Fonts/**`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/DinoFonts.cs`, `src/DinoReader.App/DinoReader.App.csproj`, `tests/DinoReader.Tests/**`, `.maestro/dino/**`, `docs/plan/slices/290-dino-reading-sound-pill.md`

- [x] **Words (frame 197):** the pill says "Getting {0} sound for your books" as the mock does. `ShelfAudio_Getting`
      ("… audio …") is also used by `ShelfAudioPacksCard.cs` on another screen, so add a new string
      `Reading_GettingSound` for the pill only, in English and all 14 locale files (word for "sound", not "audio",
      in each language; same wording style as `Reading_NoSound` in that file).
- [x] **Weight:** the pill text is Fredoka weight 500 as the mock says. The app ships one `Fredoka.ttf`
      (`MauiProgram.cs` line 39). Add a Medium (500) face (OFL 1.1, like the existing file; make a static instance from
      the variable font if `Fredoka.ttf` is variable, else the upstream static Medium), register it in
      `DinoFonts`, use it for the pill label.
- [x] **Language name in its own script:** frame 197 sets "فارسی" in Vazirmatn inside the Fredoka line. Use a span
      in Vazirmatn (or the font the app already uses for that script) for the native language name.
- [x] **Ring colours after a theme switch** (check-pass 1): refresh and `Invalidate` the ring on every `Show`, even
      when state and text are unchanged.
- [x] **Null checks** (check-pass 2): one check of the story in `ShowAudioStatus`, passed down.
- [x] **No wasted first render** (high-review 9): the icon is not built with a default `Lock` kind.
- [ ] **Device:** screenshots of every pill state against frames 197 (lilac, ring part-filled, "· N of M"), 198
      (Wi-Fi) and 199, plus a Farsi pill. Screen capture must run during the flow (start it before the flow).
      Not reached, see *Part B: not yet verified*.

### Part B built (2026-11-07)

- New string `Reading_GettingSound` ("Getting {0} sound for your books") in all 15 resx files, used only by the pill;
  `ShelfAudio_Getting` unchanged for `ShelfAudioPacksCard`. Non-English texts are machine drafts (marked in the files);
  de, nl, fi, ar, zh, ja rewritten after review.
- `Resources/Fonts/Fredoka-Medium.ttf`: a static wght=500 instance of the variable `Fredoka.ttf` (fontTools), family
  renamed "Fredoka Medium", OS/2 weight 500; OFL 1.1, no Reserved Font Name. Registered as `DinoFonts.UiMedium`, used
  only by the pill label; a right-to-left pill line switches to Vazirmatn (Fredoka has no Arabic letters).
- Label is a FormattedString: a right-to-left native language name is a Vazirmatn span (pure `SoundPillText.Split`
  and `FontFor`, tested); the label also carries a `SemanticProperties.Description` with the plain text.
- Small fixes: ring and icon colours refresh on every `Show`; one story null check; the icon is never drawn as Lock.
- Reviews: high `D:\ReadTheStupidText-worktrees\reviews\290b-high.md` (all 7 applied); medium
  `D:\ReadTheStupidText-worktrees\reviews\290b-medium.md` (6 low, not applied: listed below).
- Fast checks: DinoReader.Tests 3776 passed, 1 skipped, 0 failed; Dino Android build 0 errors.
- Emulator-5554, 2026-11-07: `language-change-fetches-audio` PASS (2nd try; 1st died on a Maestro "Device server
  died" crash). `silent-piece-beat` did not run: the Maestro driver crashed twice (DeviceServerDiedException, harness,
  empty app logcat).

### Part B: not yet verified

- Every pill state on a device against frames 197-199 (lilac Getting pill with "فارسی" in Vazirmatn and "· N of M",
  Wi-Fi, Preparing, no sound), the medium weight and the new translations. No flow stops on the reading page while a
  pill shows: the language flow ends on the Grown-ups shelf and the fetch can finish before a frame is taken.
- The pill's accessibility text: no `ReadingAudioStatus` node appeared in uiautomator dumps during the language flow.
- iPhone and the Windows head (incl. the UIA name).

### Part B: left from the medium review (all low)

- `SoundStatusPill.cs` ~92: with FormattedText, `Label.Text` is null; anything reading `.Text` sees nothing.
- `SoundPillText.cs` ~12: `IndexOf` marks the first occurrence of the name; a translation repeating it could mark the
  wrong one.
- The FormattedString is rebuilt on each changed call; the ring redraws even while hidden.
- ja/zh pill text uses the system font (as elsewhere in the app).

### Follow-up needed for the device check

- A Debug-only test hook (`src/DinoReader.App/Testing/**`) that opens a store book and holds the pill in each state
  (Getting with a count, Wi-Fi, Preparing, no sound), and a Maestro flow that screenshots each state. Without it, no
  flow can show the pill long enough to compare with the mock.
- The Maestro driver crash after boot (`silent-piece-beat`): restart the emulator cold before the next run.
