# Slice 111 — Dino: parents read in their own voice

*Built 2026-10-25, not yet verified on a device (wave 2026-10-24), draft PR.* *Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
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

- [x] 1. Library check via `library-docs` before any code: Plugin.Maui.Audio (recording on net10 MAUI,
  Android 9-11 format (minSdk 28), iOS, Windows), or the alternative it recommends.
- [x] 2. Permissions: `RECORD_AUDIO`, the Windows `microphone` capability, rewrite the iOS usage string.
- [x] 3. Core: recordings on `Story`, `StoryJson`, and the package/backup writer and reader carrying the
  files; the store builder excludes them; unit tests.
- [x] 4. `PageRecorder` and `RecordPage` (`IGrownUpPage`), entered from `StoryActions`, the tile's red microphone (frame 38) and the "This book" sheet (frame 43, with the "Read with my voice" switch shown only once a page is recorded). Frames 44-45: page image plus script card, Stop circle, "Your recording" card with Re-record and delete, green page dots, Next page.
- [x] 5. `ReadingSession`: a recorded page plays its file through a player (pause, skip, page turn), then
  moves on; fallback to generated voice. The whole page text gets one soft highlight `#FFF3C4` (frame 46).
- [x] 6. Privacy page (`site/dino/privacy.html`, `PRIVACY.md` Dino section), Data safety answer recorded
  in the slice file for the user to submit, Families wording.
- [ ] 7. Rule in `src/DinoReader.App/CLAUDE.md` (written in "For CLAUDE.md" below; update-docs lifts it).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: record pages 1 and 3 of a starter, turn on "Read with my voice", read with the app in
  Farsi: pages 1 and 3 play the recordings, page 2 is the generated Farsi voice; back up, delete, restore:
  the recordings come back.
- A store story built after this slice contains no `rec-*` file.

## Built (2026-10-25)

- **Library check (item 1):** context7 (`/jfversluis/plugin.maui.audio`, `/dotnet/maui`): Plugin.Maui.Audio records AAC only on Android 12+ (`Encoding.Aac`), and the app's floor is Android 9 (minSdk 28), so **no audio package was added**. Each platform has a small native recorder and player behind `IMicRecorder` / `IRecordingPlayer` (`RecordingAudio.cs`, `Platforms/<OS>/RecordingAudio.*.cs`): Android `MediaRecorder` (MPEG-4/AAC) and `MediaPlayer`, iOS `AVAudioRecorder` (MPEG4AAC) and `AVAudioPlayer`, Windows `MediaCapture` + `MediaEncodingProfile.CreateM4a` and `MediaPlayer`. The microphone permission is `Permissions.Microphone` (Android, iOS), asked only from `RecordPage.StartRecordingAsync`; Windows asks when the recorder opens the device.
- **Core:** `Story.Recordings` (page index to `PageRecording(Language, Seconds)`) and `Story.UseRecordings`; `story.json` `Recordings` list (`Page`, `Language`, `Seconds`) and `UseRecordings`, both null when unused, `schemaVersion` stays 1; `StoryRecordings` (file names `rec-<page number>.m4a`, `With`/`Without`, `Plays`, `Kept`/`Dropped` for page edits, `PageStarts`/`PageOfPosition`, `Clock`, `WholeSeconds`, `PagesShownInOwnLanguage`). `IStoryLibrary.StoreRecordingAsync` (temp file then move over, so a failed write keeps the old one); `DeleteFileAsync` also deletes `rec-*.m4a` names. `StoryPackageWriter.WriteAsync(..., includeRecordings)` (store builder passes false; packages, exports and backups carry them); the reader imports the `rec-*` entries and drops a recording whose file is missing.
- **App:** `RecordPage` + `RecordViews` (frames 44-45; entered from the tile's red microphone via `TileOverlay`/`GalleryPage`/`StoryActions.RecordAsync`, and the Record pill of the "This book" sheet). Recording is always in the story's own language (what the page shows on the Record screen); it saves the moment it stops. A recording never outlives the screen (disappearing cancels it). `ReadingSession` plays a recorded page's file (pause, resume, stop, skip, page turn all go through the session state), then goes on with the next sentence; a generated read is cut at the next recorded page (`_continueRead`) and continues there. `ReadingSurface.MarkPage` / `IPageRenderer.MarkPage`: one soft `#FFF3C4` highlight over the page's sentences (night `#1FFFD678`), implemented in `TextPages` and `PictureTextPages`; the line-box renderers (`ScanPages`, `PicturePages`) show no sentence mark on a recorded page. Text shown with a recording in the story's own language is the source text even while the rest of the book shows the translation (`TranslatedStoryBuilder.Build(..., keepSource)`).
- **"This book" sheet:** now also opens for a book with a recorded page (not only a translated one); "Show original text" hides when the book is not translated. A book that is neither still goes straight to grown-up mode. The wizard (`StorySaver`) keeps a recording only for a page that is still there with the same text and deletes the files of the rest.
- **Files outside Touches:** `src/DinoReader.App/RecordViews.cs` (new), `RecordingAudio.cs` and `Platforms/*/RecordingAudio.*.cs` (new), `StoryVoicing.cs`, `StoryImport.cs` (`StorySaver`), `PageRendering.cs`, `TextPages.cs`, `PictureTextPages.cs`, `ReadingTheme.cs`, `DinoTokens.cs`, `App.xaml`, `IconView.cs` (`Mic`), `StoryTile.cs`, `TileOverlay.xaml(.cs)`, `GalleryPage.xaml(.cs)`, `GalleryText.cs`, `BookSheet.cs`, `Resources/Strings/AppResources.cs`, `src/DinoReader.Core/StoryRecordings.cs` (new), `FileStoryLibrary.cs`, `IStoryLibrary.cs`, `TranslatedStory.cs`, `StoryPackageReader.cs`.
- Strings `Rec_*` (20 keys, Slice 111 block, all 14 locales, machine-drafted). Privacy page updated (`site/dino/privacy.html`, effective 25 October 2026). `PRIVACY.md` has no Dino section (it is the Read The Stupid Text policy), so it is unchanged. `PrivacyInfo.xcprivacy` needs no change (nothing is collected).
- Tests: `tests/DinoReader.Tests/ParentRecordingTests.cs` (file names, switch rules, JSON round trip and damage, page edits, package and backup round trips, store-style export, atomic store/delete, kept-source translation, clock). 629 pass (count taken before the review fixes).

**Changed while building (review):** shared or exported `.dino` packages never include recordings and a `.dino` import strips them: only a backup restore keeps them (supersedes "ride along in `.dino` backups" above and the "packages, exports and backups carry them" line in Core). Recordings play only when their language matches the story language (supersedes "always plays the recording, whatever the app language"); otherwise the generated voice reads.

**From review:** high 1-10 applied: re-record crash fixed; a playback failure falls back to the generated voice; a recorded page spanning several display pages turns over after the recording's length; mic guard plus recording stops on backgrounding; the iOS audio session is released; the label reads "Recording · 0:14". Medium 1-9 applied: a late `Completed` is ignored; a page text edit drops its recording; `story.json` is updated before the audio is moved in; a paused failure does not restart playback. Mock fix: the tile mic button is white.

*Not built / differs from the plan:* "Read with my voice" is not offered from the reading page of a book that is neither translated nor recorded (Record is on the tile). The recording's language is the story's language, never chosen per recording. A recording whose language differs from the story's is not played (generated voice instead).

## Data safety answer (for the user to submit in Play Console)

- **Data collected or shared: no change.** Voice recordings are made only on the Record screen behind the grown-up gate, are stored only in the app's private folder on the device and are never transmitted off the device (no upload, no analytics, no SDK reads them), so under Google's definition they are **not "collected"**. Leave *Audio > Voice or sound recordings* **not declared as collected or shared**; the other answers stay as they are today.
- **Permissions:** `RECORD_AUDIO` is now in the manifest. Families policy: say in the store listing's app-access notes that the microphone is used only by a grown-up to record their own reading of a page, behind a gate, with recordings kept on the device. It is not a restricted permission, so there is no extra declaration form.
- **Kill criterion:** if Play review rejects the permission for a Families app, remove `RECORD_AUDIO` and the Record entry points and ship the rest later.

## For CLAUDE.md (lifted 2026-10-25)

- (`src/DinoReader.App/CLAUDE.md`) Parent recordings (Decision 115): `Story.Recordings` maps a page index to `PageRecording`; the file is `rec-<page number>.m4a` in the story folder, written only through `IStoryLibrary.StoreRecordingAsync` and deleted through `DeleteFileAsync` (`StoryRecordings` holds every rule: names, `Plays`, `Kept` for page edits). They are in backups only: shared or exported `.dino` files, `.dino` imports and store uploads never carry them (`StoryPackageWriter.WriteAsync(..., includeRecordings: false)`). The microphone is asked for only in `RecordPage`; no audio package is used (Plugin.Maui.Audio records AAC only on Android 12+): `IMicRecorder` / `IRecordingPlayer` have a native implementation in each `Platforms/<OS>/RecordingAudio.*.cs`. A recorded page plays its file when `Story.UseRecordings` is on and the recording's language matches the story language (otherwise the generated voice), and shows its text in the recording's language (the source text when that is the story's language); `ReadingSession` cuts a generated read at the next recorded page and continues there; `ReadingSurface.MarkPage` draws the one soft highlight (`TextPages`, `PictureTextPages`; the line-box renderers draw none). The privacy page, `AndroidManifest.xml`, `Info.plist` and `Package.appxmanifest` comments must stay in step with the microphone use. (Slice 111: docs/plan/slices/111-dino-parent-recordings.md)
- (amend the Slice 110 sheet rule) The "This book" sheet opens for a translated book or a book with a recorded page; any other book still goes straight to grown-up mode.

#### Not yet verified

- Human: Play Console Data safety needs the "audio recordings, on-device only" answer (text in "Data safety answer" above).
- Device: record and replay a page on Android, iOS and Windows; microphone permission denial; a backup round trip keeps the recordings.

- Emulator (Android): record pages 1 and 3 of a starter, turn on "Read with my voice", read with the app in Farsi: pages 1 and 3 play the recordings with the soft page highlight and the English text, page 2 is the generated Farsi voice; back up, delete, restore: the recordings come back. Also Android 9-11 (MediaRecorder AAC) on a device, and the microphone permission prompt and the "denied" alert.
- Windows head: the microphone prompt, recording and playback (MediaCapture M4A) on a packaged build; iOS compile only so far (no device).
- Pause, skip and page turn while a recording plays; a book with a recorded page between generated pages reads on correctly.
- Native-speaker check of the machine-drafted `Rec_*` strings in all 14 locales.
- A store story built with `DINO_BUILD_STORE=1` contains no `rec-*` file (covered by a test of the writer; the build itself was not run).

*Emulator check 2026-10-25 (Android emulator, Debug arm64, main 7846b1c): PASS. Record/replay and highlight work.*
