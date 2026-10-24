# Slice 111 — Dino: parents read in their own voice

*Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
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

## Work

- [ ] 1. Library check via `library-docs` before any code: Plugin.Maui.Audio (recording on net10 MAUI,
  Android 9-11 format (minSdk 28), iOS, Windows), or the alternative it recommends.
- [ ] 2. Permissions: `RECORD_AUDIO`, the Windows `microphone` capability, rewrite the iOS usage string.
- [ ] 3. Core: recordings on `Story`, `StoryJson`, and the package/backup writer and reader carrying the
  files; the store builder excludes them; unit tests.
- [ ] 4. `PageRecorder` and `RecordPage` (`IGrownUpPage`), entered from `StoryActions`.
- [ ] 5. `ReadingSession`: a recorded page plays its file through a player (pause, skip, page turn), then
  moves on; fallback to generated voice.
- [ ] 6. Privacy page (`site/dino/privacy.html`, `PRIVACY.md` Dino section), Data safety answer recorded
  in the slice file for the user to submit, Families wording.
- [ ] 7. Rule in `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: record pages 1 and 3 of a starter, turn on "Read with my voice", read with the app in
  Farsi: pages 1 and 3 play the recordings, page 2 is the generated Farsi voice; back up, delete, restore:
  the recordings come back.
- A store story built after this slice contains no `rec-*` file.
