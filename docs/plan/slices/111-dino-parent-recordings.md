# Slice 111 — Dino: parents read in their own voice

*In progress (wave 2026-10-24), draft PR.* *Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
reading language, one narrator, parent recordings, store filters" (2026-10-24) —
https://drive.google.com/file/d/1b2XPenCW2D4Kx1yx98js_LK4CRBSnnLf/view?usp=drivesdk (part 5, milestone 5).

**Batch:** 22
**Depends on:** none in code; runs after 110 (both edit `ReadingSession` and `ReadingPage`)
**Touches:** `src/DinoReader.App/DinoReader.App.csproj`, `Directory.Packages.props` (if central versions), `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/RecordPage.cs` (new), `src/DinoReader.App/PageRecorder.cs` (new), `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/Platforms/Android/AndroidManifest.xml`, `src/DinoReader.App/Platforms/iOS/Info.plist`, `src/DinoReader.App/Platforms/Windows/Package.appxmanifest`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/StoryPackage*.cs`, `tests/DinoReader.Tests/StoreBuilder.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `site/dino/privacy.html`, `PRIVACY.md`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

Parents (or grandparents) want to read the bedtime story in their own voice, page by page, and choose to
use it instead of the generated voice. Today the reading pipeline only plays generated speech:
`ReadingSession.TryStartReadAsync` (`ReadingSession.cs:177-202`) voices the page and calls
`SpeakSegmentsAsync`. There is no audio library, no `RECORD_AUDIO`, no Windows microphone capability, and
the iOS `NSMicrophoneUsageDescription` (`Info.plist:63-66`) says "The app does not record audio".

## Decisions (2026-10-24, agreed with the user)

- **Decision 115: per-page parent recordings, on device, behind the grown-up gate.** In grown-up mode a
  book's **Record** action opens a page-by-page screen: see the page, record, stop, listen, re-record,
  delete, next. A per-book switch "Read with my voice". Recordings are AAC/M4A, `rec-<page>.m4a` in the
  story folder, with the recording's language in `story.json` (optional field, `schemaVersion` 1). They
  ride along in `.dino` backups and are never in store uploads.
- A recorded page **always plays the recording**, whatever the app language. Pages without one fall back
  to the generated voice (translated when translation applies). No sentence highlight on a recorded page
  (no timings): the whole page text is highlighted, in the recording's language (Slice 110).
- The microphone is requested only from the Record screen, behind the grown-up gate. Google Play Families
  treats microphone data as sensitive: the privacy page, the Data safety form and the manifest comments
  change in the same slice. If Play review rejects it, recording ships later (kill criterion).

#### Design (Dino Reader Batch 22-23 mock, frames 44, 45 and 46 (and 38, 43), added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 22-23.dc.html`, frames 44 (lines 186-201), 45 (204-216) and 46 (219-228); the tile mic is in frame 38 and the switch in frame 43. Three real-UI frames (360x760 phone frame, 6px `#2b2540` border, radius 34.); the page picture in 44 and 45 is the Good Enough starter's `2.jpg`. Icons in the mock are inline SVG strokes (2.2 wide, `#5B57E8`), not Phosphor names; the app uses its own `IconKind` set. The mock agrees with the plan.

- **Record, recording (44).** Page `#F3EFE6`. Header: back arrow, "Record" 20/600, "Good Enough · page 2 of 10" 12 `#8a8399`. The page is shown as the child sees it (image, radius 14) with a white script card below (radius 16, 17 text): "Read the words on this page aloud, then tap Stop." Footer: red dot and "Recording · 0:14" 15/600 `#E5484D`; a 104 circle `#E5484D`, shadow `0 8px 0 #B9363A` plus an 8px ring `rgba(229,72,77,.18)`, with a white 34 rounded square (stop); label "Stop" 13 `#5a5470`. Red mic to start, white square to stop. The mic permission is asked the first time this screen opens.
- **Record, page recorded (45).** A white card (radius 16): play circle 44 `#ECE7FF`, "Your recording" 15/600, "0:21 · English" 12.5 `#8a8399` (the saved recording language), "Re-record" 14/600 `#5B57E8` and a 44 delete button. Page dots turn green (`#4FC060`) for recorded pages (current 26x9). Footer: a 52 round button `#ECE7FF` (previous) and "Next page" (52 high, radius 26, `#FF7A59`, shadow `0 5px 0 #E2583A`, white 16/600).
- **Entry points.** The tile microphone (red, 44x44, frame 38) and the Record pill in the "This book" sheet (frame 43). **Read with my voice** is a switch in that sheet (on = `#5B57E8`), with the line "3 of 15 pages recorded. Other pages use Dino's voices." It appears only once a page is recorded.
- **Reading a recorded page (46).** Same reading page as 41 but the whole page text has one soft highlight `#FFF3C4` (radius 8, 4px spread) instead of a sentence highlight. The text is in the recording's language (English here, with the app in Farsi). Unrecorded pages use the normal sentence highlight (`#FFE27A`).

Settled by the mock: the recording screen shows the printed page, with no separate script box; any page can be skipped; recorded-page dots are green; delete sits beside Re-record.

New strings (all 14 locales): "Record", "Read the words on this page aloud, then tap Stop.", "Recording · {0}", "Stop", "Your recording", "{0} · {1}" (length and language), "Re-record", "Next page", "Read with my voice", "{0} of {1} pages recorded. Other pages use Dino's voices.", "Record {0} in your voice" (screen reader), delete label.

## Work

- [ ] 1. Library check via `library-docs` before any code: Plugin.Maui.Audio (recording on net10 MAUI,
  Android 9-11 format (minSdk 28), iOS, Windows), or the alternative it recommends.
- [ ] 2. Permissions: `RECORD_AUDIO`, the Windows `microphone` capability, rewrite the iOS usage string.
- [ ] 3. Core: recordings on `Story`, `StoryJson`, and the package/backup writer and reader carrying the
  files; the store builder excludes them; unit tests.
- [ ] 4. `PageRecorder` and `RecordPage` (`IGrownUpPage`), entered from `StoryActions`, the tile's red microphone (frame 38) and the "This book" sheet (frame 43, with the "Read with my voice" switch shown only once a page is recorded). Frames 44-45: page image plus script card, Stop circle, "Your recording" card with Re-record and delete, green page dots, Next page.
- [ ] 5. `ReadingSession`: a recorded page plays its file through a player (pause, skip, page turn), then
  moves on; fallback to generated voice. The whole page text gets one soft highlight `#FFF3C4` (frame 46).
- [ ] 6. Privacy page (`site/dino/privacy.html`, `PRIVACY.md` Dino section), Data safety answer recorded
  in the slice file for the user to submit, Families wording.
- [ ] 7. Rule in `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: record pages 1 and 3 of a starter, turn on "Read with my voice", read with the app in
  Farsi: pages 1 and 3 play the recordings, page 2 is the generated Farsi voice; back up, delete, restore:
  the recordings come back.
- A store story built after this slice contains no `rec-*` file.
