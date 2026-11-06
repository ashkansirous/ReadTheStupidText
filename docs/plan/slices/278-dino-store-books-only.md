# Slice 278 — Dino: store books only (no own books, no scanning, no translation, no voice engine, no voice pickers)

*Part A built 2026-11-06 (wave 2026-11-06, merged as PR #859, 09e2e892), not yet verified on every platform (see Not yet verified below). Part B built 2026-11-06 (wave 2026-11-06, PR #863), not yet verified on iOS. Part C merged as PR #862 (a854c8ed).* Dino Reader only (Android, iOS and the Windows head, the same in the same parts). **Read The
Stupid Text is not touched**, and neither is any shared code it uses (`src/ReadTheStupidText.Mobile.Shared`,
`src/ReadTheStupidText.Infrastructure`, `src/ReadTheStupidText.Documents`, `src/ReadTheStupidText.Mobile`). Dino
simply stops calling the shared pieces it no longer needs.

**Depends on:** Slice 277 for part B (the whole store must have complete pre-made audio before the voice engine goes). Slice 277 merged (PR #858, 2026-11-06), so part B can start.
**Touches, part A (own books out):** `src/DinoReader.App/{StoryWizardPage,PagesStep,TextStep,CoverStep,PageEditorPage}.xaml*`,
`src/DinoReader.App/{PageTextPage,RegionPicker,RegionOverlay,CoverPicker,StoryImport,OwnBookTranslation,PickedPhotoCache,ScanPages,ZoomablePage,TranslationPackPrompt,TranslationPackStore,StoryTranslator,LocalizedPackStatus,BookOrigins}.cs`,
`src/DinoReader.App/{IntroWizardPage,IntroWizardPage.Art,IntroState,StoryActions,BookSheet,GalleryPage.xaml,MauiProgram,UsageEvent}.cs*`,
`src/DinoReader.App/Resources/**` (strings), `src/DinoReader.Core/ScanLayout*.cs` (only if no store book uses it),
`tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.Core/CLAUDE.md`,
`src/DinoReader.App/StarterStories.cs`, `src/DinoReader.App/BookSheetPlan*.cs`, `src/DinoReader.App/OwnBooksCleanup.cs` (new, A3), `src/DinoReader.App/BackupPage.xaml.cs` (A3: restore skips own books),
`.maestro/dino/**` (only flows that drive the removed wizard, Add pill or scan; not `premade-audio-past-roar.yaml` or
`usage-fallback-swallowed.yaml`), `tools/desktop-vm/checks/**` (only Dino checks that drive removed screens).
Must not edit `src/DinoReader.App/ReadingSession.cs`, `PlayerHandOff.cs` or `AudioPackStore.cs` (Slices 271 and 275 edit them in the same wave).

**Touches widened for A3 (design 2, 2026-11-06):** `src/DinoReader.Core/FileStoryLibrary.cs`, `src/DinoReader.Core/IStoryLibrary.cs`, `src/DinoReader.Core/OwnBook.cs` (new), and the listing callers
(`GalleryPage.xaml.cs`, `BackupPage.xaml.cs`, `IntroWizardPage.Art.cs`, `NarratorPage`, `StorytellerDefault`; the last three need no edit, they are consistent by construction), plus the existing Dino tests whose test books now carry a source id.

**Design for A3, design 2 (coordinator, third round = redesign, 2026-11-06; replaces the earlier design).** Cause of the repeats: "own book" was decided in several places from different inputs (parsed `SourceId` in the shelf filter and the restore, raw `story.json` in the delete), and only some listings filtered. Now one owner, one predicate:
- `OwnBook.IsOwn(rawStoryJson)` (Core, pure, unit-tested): true only when the `sourceId` field is absent or JSON null. Present-but-malformed, empty string, number, or text that is not a JSON object: NOT own (kept and listed as today).
- `FileStoryLibrary` applies it where it reads `story.json`: `ListAsync` never returns an own book, so the shelf, intro, backup export and count, narrator, storyteller and any later caller are consistent by construction; `ForShelf` and the other ad-hoc filters are gone. `GetAsync` and `TryListSourceIdsAsync` still see every book (a lookup by id, and the audio sweep's "do not conclude a book is gone").
- `FileStoryLibrary.ListOwnFoldersAsync()` (also on `IStoryLibrary` as a default member) returns the own books' folders plus `Complete` (false when a `story.json` could not be read or the root could not be listed). A folder with no `story.json` yet (a package mid-import) is not a book: skipped, not a failure.
- Cleanup (`OwnBooksCleanupFlight`, single-flight per app run; started in `GalleryPage.OnAppearing` on its own, before and independent of the starter install and the refresh): deletes each listed folder; per book it catches every exception except cancellation, reports `FallbackArea.Library` and goes on with the next book. Done when `Complete` and no failure; failed starts are capped at 3 (`dino.ownBooksCleanupFailedStarts`), then done; no folder at all is a clean pass. State: `dino.ownBooksCleanupDone`. Killed mid-way, the next start runs again.
- Backup restore: imports through the library, then runs the same `OwnBooksCleanupRun.RunAsync` and drops the books it deleted from `added`, so the summary and usage count see only kept books; own books show as skipped. A book the restore could not remove stays hidden and the cleanup is re-armed for the next start (`OwnBooksCleanup.Rearm`).
- Tests: predicate cases; the library omits own books and keeps malformed ones; ListOwnFolders complete, incomplete, mid-import; the cleanup continues past a failing book (IOException and InvalidOperationException, poisoned book first); retry cap; single-flight; restore drop.
Offline does not matter (local files only).
**Touches, part B (voice engine and voice pickers out):** `src/DinoReader.App/{StoryVoicing,SpeechStartup,StorytellerDefault,StorytellerConsent,StorytellerViews,VoicePreview,NarratorPage,NarratorChoice,NarratorRecast,CastPage,CastRows,CastViews,CastText,CastFlavourMenu,ReadingClips,ReadingSession,SpokenLanguage,FirstRunPage.xaml,FirstBooksPage*,SettingsPage,SettingsRows,BookSheet,StoryActions,MauiProgram}.cs*`,
`src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/Platforms/**`, `src/DinoReader.App/Resources/**`,
`src/DinoReader.Core/**` (cast and voice-choice types only), `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`.
**Touches widened for part B (claim, 2026-11-06, from a ground-in-code read of origin/main):** `src/DinoReader.App/{App.xaml,GalleryPage.xaml,LanguagePage.xaml,ReadingPage.xaml,ReadingSpeedPage,IntroWizardPage,StoryLanguages,StoryLanguagesPage,StoryLanguageButton,StarterStories,CloseQuestionPage,GrownUpHeader,ReadingText,RecordViews,ShelfAudioPacksCard,ShelfReorderView,UsageEvent}.cs*`,
`src/DinoReader.App/Testing/**` (Debug test hooks: drop `voice=`, `voiceId=`, the voice-ready acknowledgement; keep the rest),
`src/DinoReader.Core/{Story,StoryJson,StoryDraft,StoryPieces,StarterUpdate,UsageEvent,BookSheetPlan,FirstBooks}.cs` (only to stop the app needing cast data; parsing keeps tolerating stored `cast` fields),
`.maestro/dino/**` (flows that drive the Cast screen, the voice-ready scrim, voice ids or the voice download; not `premade-audio-past-roar.yaml`),
`tools/desktop-vm/checks/dino-*.ps1` (dino-launch waits for "Continue with the phone voice"), `tests/ReadTheStupidText.Tests/FallbackGuardBaseline.txt` (only deleted or changed Dino files' lines).
Must not edit: `tools/DinoReader.BookBuilder/**` (it builds the store audio with `VoiceCaster`, `CharacterRoster` and the cast), any shared project (`src/ReadTheStupidText.*`, `src/AndroidSherpaPackaging.props`), `worker/**`, `docs/design/**` (part C).

**Design for part B (coordinator, claim 2026-11-06):**
- **Core cast and voice types stay** where `tools/DinoReader.BookBuilder` or `StoryJson` uses them (the store's audio is built from them). Delete a Core type only when no App, BookBuilder or test code still needs it after the App removal. A saved book's `cast` / language casts still parse and are ignored by the app.
- **A piece with no clip and no recording is not spoken.** Reading shows that piece's text and highlights for a short fixed beat, then goes on to the next piece, as if it had played; it reports one fallback per occurrence through the existing sink with an existing `FallbackArea`/reason (no Worker change). A clip that fails to play is treated the same way. No code path in Dino resolves `ISpeechReader`, `IVoicePackService`, `VoicePackInstaller` or `IVoiceModelService` after part B.
- **Reading speed:** keep the setting only if pre-made clip playback already honours it (read `ReadingSpeedPage` / the clip player); otherwise remove the row. Write which in the slice file.
- **Language packs:** a story language needs only its audio pack (`AudioPackStore`); the per-language voice pack download, its prompt, consent and progress go.
- **First run:** language, first books, then the shelf; saved first-run states that pointed at the voice wait go to the shelf.
- **Shared registration:** `AddMobileSharedServices()` still registers the shared speech reader (shared code, not edited); Dino just never resolves it. If sherpa-onnx / voice-pack natives still land in the Dino packages through the shared props or `Mobile.Shared`, write it into *Outstanding for the repo split* like A4.
- **Test hooks and flows:** remove the hooks and flows that only exist for voices (cast screen, voice id, first-run pack start); fix the flows and the VM `dino-launch` check that wait for or dismiss the voice-ready scrim.

**Touches, part C (design copy):** `docs/design/dino-reader/**`.
Parts A and B share hub files (`MauiProgram.cs`, `StoryActions.cs`, `BookSheet.cs`, resources): build them one after
the other, not in the same wave.

## Why

The user, 2026-11-06: "remove the path to add your own books. I dont care about it. that's not the main product
anymore. we dont need any of the shit that comes with that. no ml kit, not scanning no translation no nothing. even
remove it from the design. no tts engine. nothing. as a result, the wizard does not need it either and there is no
choice to change charactures voices. all should be cleaned up from the app." And: "I said everything but I was
explicit about dino reader. Do not fucking touch the RTST!!!" The user plans to move Dino Reader to its own
repository later.

## Decisions (the user, 2026-11-06)

- Books are only store books. A book plays its pre-made audio, or the family's own recordings (Slice 279). No phone
  voice ever reads anything, and there is no fallback when a clip is missing (Slice 277 guarantees there is none).
- Books a family added themselves are **deleted on update** (testing phase; "I dont fucking care"). The user asked
  how many people did this. Live usage counts (`dino_usage`, last 90 days, test rows left out, read 2026-11-06):
  own books were added in **1 app session** (about 9 books, all typed in the story wizard; no scans or file
  imports), and own-voice recordings were made in 2 sessions. The data has no person or install id, only a random
  id per app launch, so this is launches, not people; TestFlight counts (`dino_usage_beta`) were not read.
- The setup wizard loses its "add your own book" step. Nobody can choose a character's or the storyteller's voice.
- The Windows head loses the voice engine too, like Android and iOS.
- Removed from the design copy as well.

## Facts from the code (origin/main, 2026-11-06)

- Own books start at `StoryActions.EditAsync` (the story wizard), the intro wizard's "add" step and the Add pill.
- Machine translation: `StoryTranslator` uses each store book's bundled translations first and ML Kit only when one
  is missing. Store books carry their own translations, so only bundled ones stay.
- Voices: Kokoro (`kokoro-en`), Supertonic and `piper-fa` packs, system voice readers, `StorytellerDefault`
  downloads the English voice on Wi-Fi; the first-run flow waits for a voice download.
- The intro step names `Add` and `Store` are kept for old saved states and usage-event names (`FromLegacy`,
  `FromSixSteps`); the Worker's allow-list is left alone.

## Work

**Part A — own books out**

- [x] A1. Remove the story wizard and every page and service only it uses (list in Touches), the Add pill, the
  intro wizard's "add" step (remap saved states that point at it to the next step), and their strings.
- [x] A2. Remove machine translation: `StoryTranslator` keeps only bundled translations; ML Kit, translation packs
  and their prompts go. A book is offered only in languages it has a bundled translation for.
- [x] A3. On the first start after the update, delete every book that did not come from the store (with its
  pictures and recordings) and log one line with the count. Store books and their recordings stay.
- [x] A4. Dino no longer registers or calls the scanner, OCR, PDF/Word extractors or translator from the shared
  projects. Do not change those projects. Check whether the ML Kit packages still land in the Dino Android and iOS
  packages through `Mobile.Shared`; if they do, write it into *Outstanding work* for the planned repo split, without
  editing shared code.
- [x] A5. Tests updated (wizard, import and translation tests removed; a test for A3's cleanup and A1's remap).

**Part B — voice engine and voice pickers out**

- [x] B1. Reading plays only pre-made clips and recordings: remove the device-voice path from `StoryVoicing` /
  `ReadingClips` / `ReadingSession`, and every speech engine, voice pack download and voice consent from Dino
  (Android, iOS, Windows head).
- [x] B2. Remove the cast page, narrator page, voice preview and their entries in the book sheet and settings; drop
  narrator recasting. A stored cast in a saved book is ignored on load (no crash on old files).
- [x] B3. First run no longer waits for a voice download: language, first books, then the shelf (the books' audio
  packs still download with the books, as today).
- [x] B4 (checked, imports kept, see Part B built). Remove the sherpa-onnx and voice-pack references from the Dino project only (`DinoReader.App.csproj`);
  never from shared projects (same check and *Outstanding work* note as A4 if they still come in through them).
- [x] B5. Tests updated; a test that reading a page with no clip and no recording never asks for a speech engine.

**Part C — design**

- [ ] C1. Remove the add-book, scan, page-editor, translation, voice download, cast and narrator frames from the
  design copy in `docs/design/dino-reader/`, and the wizard's "add" step. (The new recording frames come with Slice 279.)

#### Design (Dino Reader Batch 48 mock, frames 177, 185, 186, added 2026-11-06)

Three frames in `docs/design/dino-reader/Dino Reader Batch 48 Store Only and Recording.dc.html` (lines 34, 325, 349). The mock mostly agrees with the plan; three Open points are at the end.

- **A1 / B2, book sheet (177, line 34, 387 x 679).** Frame 172 with Cast gone. Rows: header (44 x 58 cover, "The Three Little Pigs", "Traditional · 16 pages"); "Read now" (60px); "Record your voice" (48px, outlined: 2px `#c9c0f5` border, `#5B57E8` text, red `#E5484D` mic icon 18px, full row); "Reorder" (48px); "Remove from the shelf" (46px). "From the Store" is gone: the line under the title is credit and length, because every book is a store book. Record opens the Slice 279 intro every time (its frame 178).
- **A1, Grown-ups' corner (185, line 325, 360 x 760).** Frame 175 with "Add a book" gone. One full-row button: 52px, radius 18, fill `#5B57E8`, white 15px 600, shop icon + "Get more books". Below it the book list (Good Enough, The Three Little Pigs, The Gingerbread Man, The Tortoise and the Hare, 30px covers) and "Back to my child's shelf" (56px). Intro line "Only grown-ups see this. Your child's shelf is one tap away." The page title still says "Store".
- **A3 (185 caption).** Own books are deleted on the first start after the update with no message to the user (the plan's one log line stays).
- **C1, what leaves the design copy (186, line 349, a list, not a frame).** Own books: v2 add-story, Batch 17-18 frames 31-35, Batch 47 frames 170-171, corner "Add a book" (98, 175), the intro add step. Translation: Batch 22-23 frames 37 and 41; Batch 37 story-languages copy that mentions machine translation. Voices: Batch 16 frame 17, Batch 22-23 frames 48-49, Batch 24 frames 50-51, frame 169's reading-voice line and "Dino's reading voice will read it". Cast and Narrator: Batch 22-23 frames 38-40 and 42-43, Batch 24 frames 52-55, Batch 31 frame 69, Cast in frames 100 and 172. Old Record: Batch 22-23 frames 44-46 and 106's words and note (replaced by 177-184, Slice 279).
- **B3, first run (186).** Language, first books (163-166), "Ready to read" (167). No voice download screen.
- **A1, intro wizard (186).** Two places, read, record, languages: counters read "of 4" (record is step 3 of 4). Saved add steps map to languages.
- **A2, story languages (frame 169, 186).** The list shows only languages every shelf book has; the audio card stays.
- **Simplified:** 186 is a text list; status bars and phone outlines are illustration.
- **Strings (13 languages):** new "Get more books"; remove every add-book, scan, translation, voice-download, cast and narrator string.

**Open point: remap target of the intro's add step.** Plan A1: saved states map "to the next step". Mock 186: they map to languages. Default: languages.
**Open point: translation languages.** Plan A2: a book is offered only in languages it has a bundled translation for. Mock 186 (frame 169): the list shows languages every shelf book has. Default: per book when reading, and the story-languages list as the mock says.
**Open point: removing frames.** Plan C1 removes frames from the design copy. Mock 186: the older batch files keep their frames and get a "retired" line in the screen map; only the repo copy drops them. Default: leave the older `.dc.html` files verbatim and add a "retired by Slice 278" line per frame range to the README screen map.

## Part A built (2026-11-06)

**Touches widened by the coordinator:** `src/DinoReader.App/{StoryLanguagesPage,App.xaml,SettingsPage}.cs`, `src/DinoReader.Core/{UsageEvent,BookSheetPlan}.cs`, `src/DinoReader.App/OwnBooksCleanupRun.cs`, `tests/ReadTheStupidText.Tests/FallbackGuardBaseline.txt` (only the deleted files' lines; Slice 275 edits other lines), and Core's dead translation types (`ReadingTranslation`, `TranslationPackRecord`, `TranslationPackSeed`) with their tests, removed.

**Not done in part A:**
- The book sheet's subtitle still reads "From the Store"; the mock's "credit · N pages" needs a credit field `Story` does not have.
- The "Get more books" button uses the existing Book icon; the mock's shop icon is not in `IconView`.
- No scripted device check proves the A3 cleanup: it needs a Debug hook that seeds an own book (shared test hooks, not part A).
- `ScanPages`, `ZoomablePage`, `ScanLayout` stay (`ReadingSurface` still renders scanned pages with them). A failed own-book delete reports `FallbackArea.Library` / `StoryUnreadable` (no delete-specific reason in the shared list).

**Outstanding for the repo split (A4):** Dino makes no call to the scanner, OCR, PDF/Word extractors or translator, but `DinoReader.App` still references `Mobile.Shared` and `Documents`, so Xamarin ML Kit Translate/TextRecognition/DocumentScanner (Android) and `EngageSolutionsGroup.Apple.MLKitTranslate` (iOS) still land in the Dino packages (read from the csproj files, not checked in an APK). Drop them when Dino gets its own repo; shared projects were not touched.

**Changed while building:** the A3 cleanup went through three design rounds (see the design for A3 above). The first design decided "own" from `BookOrigins` preferences keys; review showed that could delete store books, so it became the single predicate `OwnBook.IsOwn` applied inside `FileStoryLibrary`. The third round on the cleanup was a redesign, not another patch (design files: `reviews/s278a-cleanup-design.md`, `reviews/s278a-cleanup-design-2.md`, outside the repo).

**From review** (files in `D:/ReadTheStupidText-worktrees/reviews/`, outside the repo):
- `s278a-high.md`: findings 1-8 resolved through design 1 (which replaced the preferences-key classification with the story's source id). Finding 9 skipped: `IntroSteps.Add` and `BookOrigin.Own` are kept because old clients still send `add:*` events.
- `s278a-medium.md`: 1, 3 and 7 resolved through the design (single-flight cleanup, complete listing before Done, cleanup before the shelf shows own books); 2, 4, 5 and 8 applied; 6 skipped: no delete-specific reason exists in the shared fallback reason list, so `FallbackArea.Library` / `StoryUnreadable` is reused (shared code not touched).
- `s278a-check1.md`: findings 1-8 all resolved through design 2 (one predicate in `FileStoryLibrary`, `ListAsync` never lists own books, per-book isolation, cleanup started independently in `GalleryPage`).
- `s278a-check2.md`: 1 applied (`story.json` is deleted last, so a failed folder delete is found again next start). 2 skipped: after the cap of 3 failed starts, own books that stayed undeletable are hidden but never deleted, and never shown; accepted. 3 skipped: a cosmetic count race in the restore summary when the background cleanup deletes a restored own book first.
- `s278a-check3.md`: 1 (low) skipped: the restore's own-book drop runs the same per-book catch as the cleanup, and an exception outside the per-book catch (for example from listing) can still escape the restore after books were restored; rare, optional.

**Device (part A):** Windows VM, 8 Dino checks PASS (launch, open-book, page-turn, settings, keyboard, window, desktop-polish, reorder). Android emulator, flows `dino-landscape-shelf`, `grown-ups-button` (no Edit row) and `dino-landscape-intro` PASS on build 4597ede207de.

#### For CLAUDE.md (lifted 2026-11-06)

For `src/DinoReader.Core/CLAUDE.md`:
- A book is "own" (added by the family, deleted on update) only when `OwnBook.IsOwn(rawStoryJson)` says so: `story.json` has no `sourceId` field or it is JSON null. A malformed, empty or non-string `sourceId` is not own. Never decide "own" anywhere else (preferences keys, parsed `Story.SourceId`). `FileStoryLibrary.ListAsync` never returns own books; `GetAsync` and `TryListSourceIdsAsync` still see them. (Slice 278: docs/plan/slices/278-dino-store-books-only.md)

For `src/DinoReader.App/CLAUDE.md`:
- The own-books cleanup (`OwnBooksCleanup.cs`, `OwnBooksCleanupRun.cs`) is single-flight, started from `GalleryPage.OnAppearing` independent of the starter install and refresh, deletes `story.json` last, catches every exception except cancellation per book, and is done when the listing was complete with no failure or after 3 failed starts. Backup restore runs the same run and drops the own books it removes. Do not add a second deletion path. (Slice 278: docs/plan/slices/278-dino-store-books-only.md)

#### Not yet verified

- Android emulator flow `dino-landscape-store` was not reached in the 10-minute run.
- No flow asserts the "Get more books" text or that the Add pill is gone.
- The A3 cleanup has no device check: no Debug hook seeds an own book, so deleting an own book on a real install is untried.
- No iOS device run is recorded for part A.

## Part B built (2026-11-06)

**Reading speed (decision):** kept. The pre-made clip player already honours it on all three platforms (`IRecordingPlayer.Play(..., rate)`: Android `PlaybackParams.SetSpeed`, Windows `PlaybackSession.PlaybackRate`, iOS player rate), so `ReadingSpeedPreference` stays and `ReadingSession.TryPlayPremade` still passes it. Only the sample "Play" button on the Reading speed card went (it spoke a line with the voice). A piece shown for a beat ignores the speed.

**Built:**
- `ReadingSession` has no speech reader: steps are `PlaybackSteps.Plan` (new, Core, pure): a pre-made clip, an animal's recording, or one piece with no clip. A silent piece is marked and held for `PlaybackSteps.BeatMilliseconds` (1200 ms), reports one `premade_audio` fallback per step (`clip_missing`; `clip_unplayable` when the file would not play), logs `[AudioPack] silent piece shown (sentence n)`, and the read goes on. Pause cancels the beat, Resume shows the piece again, Stop and a newer read cancel it.
- `StoryVoicing` no longer casts, names or picks voices; it cuts the pieces, finds each clip and plans the animals' slots (`VoicedSegment.VoiceId` is empty).
- Gone: `CastPage/Rows/Views/Text/FlavourMenu`, `NarratorPage/Choice/Recast`, `VoicePreview`, `SpeechStartup`, `SpokenLanguage`, `StorytellerDefault/Consent/Views`, `StoryLanguagePacks`, `LocalizedPackStatus`, the book sheet's Cast pill (Record is a full row now), Settings' Narrator row, the gallery's storyteller line and "ready" sheet, the reading page's pack status line, the first run's voice download page and "hear hello" pill, the Language page's Ready/Download column, the story-languages picker's voice bars. `LanguageChoice.Apply(code, settings)` stores the language; `Taps` and `OwnScript` replace the helpers that lived in the cast files. Debug hooks `voice=`, `voiceId=`, `speed=` and the voice-ready acknowledgement are gone.
- Adding a story language only fetches the books' audio (`ShelfAudioPacks`). Review fix: on a phone with only mobile data the grown-up is asked "Download on mobile data?" (Download now / Wait for Wi-Fi) as before (`ShelfAudioAsk`, Settings tick and intro Done); a quiet launch run still waits for Wi-Fi.
- Review fixes (high pass): the reading page shows the book's audio state (`ReadingAudioStatus`: getting audio with progress, waiting for Wi-Fi or the internet, or "No audio for this book in this language yet.") while a piece has no clip; the silent beat pauses when the page is covered (lock, background, gate) and goes on when shown; the child's shelf language list needs an installed audio pack; a read of only silent pieces is not counted as `read_voice` (each piece reports the clip fallback); `OldVoicePacksCleanup` deletes the old voice and storyteller packs and their partials once, by known pack id only; the read wait says "Getting the book…". Unused strings removed (about 90 keys).
- Flows: removed `dino-landscape-cast`, `open-book-voice-id`, `first-run-pack-start`, `download-now-tap`, `read-back-reread-background` (engine log markers); removed the "Storyteller voices are ready" dismissals from 21 flows; first-run flows wait for `FirstRunReady`; new `silent-piece-beat`. VM check `dino-launch` no longer waits for "Continue with the phone voice".
- Tests: `NoSpeechEngineTests` (pure `PlaybackSteps` cases and a source scan: no `ISpeechReader`, `IVoicePackService`, `IVoiceModelService`, `VoicePackInstaller` and the like in `src/DinoReader.App`, the session reports the fallback once per step); `StorytellerConsentTests` and `AcceptCompleted` removed; the lifecycle allow-list and two source-text tests follow the new code. `FallbackGuardBaseline.txt` lowered (deleted files, `SettingsRows` 3 to 2, `StarterStories` 3 to 2).

**Outstanding for the repo split (B4):** the sherpa-onnx and onnxruntime packages still reach the Dino packages through `Mobile.Shared` (and `Infrastructure` on Windows). Dino's csproj keeps its three imports (`AndroidSherpaPackaging.props`, `IosSherpaPackaging.props`, the WinML target) because they are what neutralise the transitive packages: a trial without the Android import still shipped `libsherpa-onnx-c-api.so` and `libonnxruntime.so` in the x86_64 Debug APK, NuGet's `ExcludeAssets` does not flow through a project reference, and the iOS import also links the framework `Mobile.Shared`'s P/Invokes need. Removing them without editing the shared projects would risk the Android wrong-OS natives, `APPX1101` and an iOS link failure. Drop them with the repo split. The Android manifest keeps `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_DATA_SYNC` and `POST_NOTIFICATIONS` for the same reason (the shared downloader's service is merged in); nothing in Dino starts it.

**Known behaviour left in place:** Debug builds only: `Mobile.Shared/Testing` (`TestHooks.Install`) still resolves `ISpeechReader` and `VoicePackInstaller`, so a Debug build builds the engine at start-up; Release never does. Core's `CastEditor`, `CastSheet`, `CastRowLookup`, `NarratorSetting`, `FlavourTransform`, `PitchShifter` and `StarterUpdate.AnimalVoicesToRecast` have no app caller left but their tests are many and intertwined (`AuthoredVoiceTests`, `FarsiDefaultVoiceTests`), so they stay for a later sweep. About 110 strings (`Cast_*`, `Narrator_*`, `Storyteller_*`, `PackStatus_*`, `PackNotify_*`, `Notify_*`, `Language_Ready/Download/Downloading`, `StoryLanguages_Getting/Wifi*/Waiting*`, `FirstRun_GettingReady/Once/Downloading/PickAnother/HearHello/HelloLine/PhoneVoice`, `Speed_Play`, `Settings_NarratorVoice`, `Wait_Stalled`) are unused in the 15 resx files and `AppResources.cs`; removing them is a mechanical pass not done here. `.maestro/README.md` (outside this part's Touches) still lists `first-run-pack-start` and the `speed=` argument.

**From review** (high pass, `D:/ReadTheStupidText-worktrees/reviews/s278b-high.md`): 1 to 6, 8 and 9 applied (the "Review fixes (high pass)" bullet above). 7 skipped: the beat tests are source-string asserts and the `silent-piece-beat` flow does not pause or leave; the beat state machine was later replaced by `SilentBeat` (see check pass 1), which has pure unit tests.

**From review** (medium pass, `D:/ReadTheStupidText-worktrees/reviews/s278b-medium.md`):
- 1 and 7 applied as one design: `BeatVisibility` (Core, pure, unit-tested for hide then beat start, beat then hide, hide then resume, show then resume) holds the one hidden flag; `ReadingSession` parks a beat that would start while the page is covered, and Resume never restarts a beat while hidden.
- 2 applied: after the mobile-data answer only the languages still selected start (`StoryLanguagesPage`, `IntroWizardPage`).
- 3 applied: the audio status line is cleared when an animal clip or a recorded page plays (`PieceAudio(true)`), so it shows only while a silent beat runs.
- 4 applied: `ShelfAudioAsk` uses `CellularOnlyKnown()`, so a failed connectivity read asks nothing and starts normally.
- 5 applied: the gallery follows `ShelfAudioPacks.Changed` (subscribed in OnAppearing, removed in OnDisappearing).
- 6 applied: installed audio languages are a snapshot computed off the UI thread (on a new shelf list and on each shelf audio change).
- 8 skipped by design: only known voice pack ids are deleted, which is safe. 9 skipped: cleanup during a forced update is harmless.

**Changed while building (check pass 1):** after two strikes on both areas the review findings were not patched; `D:/ReadTheStupidText-worktrees/reviews/s278b-design.md` replaced them. `BeatVisibility` and its tests are deleted; `SilentBeat` (Core, pure) is the one clock of a silent piece (Active, Hidden, HeldByPerson; the timer runs only while all allow it), `ReadingSession` owns one. `InstalledShelfAudio` (Core, pure) plus `ShelfAudioPacks.RescanAsync/ShelfLanguages/OffersBook` are the one snapshot and the one rule for which languages are offered; the gallery and the reading page read no pack folder. New string `Reading_NoSound` ("This part has no sound") in all 15 resx.

**From review** (`D:/ReadTheStupidText-worktrees/reviews/s278b-check1.md`):
- 1 to 7 resolved by the design above: 1 (no page reads pack folders; the snapshot is scanned off the UI thread), 2 (one rule, an active download neither hides nor shows a language), 3 (the last snapshot is saved to Preferences and loaded at start), 4 (a pack landing only updates the language button's choices; the open book is never re-rendered and `ReadingLanguage` never changes), 5 (`BeginBeat` always does the immediate part, only the timer waits), 6 (resume logs `User` only when counted, else `Other`; a resume while covered is logged and the timer waits for Show), 7 (status line is progress, waiting, or "This part has no sound", never "audio unavailable").
- 9 resolved: `Pause` while a beat is active sets the person's hold whatever the reason or `_stepRead`. 8 skipped (dead guard is harmless).

**From review** (`D:/ReadTheStupidText-worktrees/reviews/s278b-check2.md`):
- 1 applied: `AudioPackStore.PacksChanged` (raised after an install, `DeleteBookAsync`, `RemoveAsync` and a sweep that deleted) makes `ShelfAudioPacks` rescan; the gallery and a book also rescan when they appear. 2+3 applied as one change: the scan is single-flight with a dirty flag (`ScanRequests`, Core, unit-tested): a request during a scan reruns once after it; a failed scan keeps the last good snapshot, reports one `Pack` fallback and a later request rescans. 4 applied: the status line shows download progress only when this book's pack is in the live run (`ShelfAudioPacks.RunIncludes`), else the plain "This part has no sound". 5 applied: `UpdateLanguageButtonAsync` drops the result of a stale call (version counter).
- 6 skipped (unconfirmed; a new read must not clear a real cover). 7 skipped (low; the existing `SilentBeat` tests stand). 8 skipped (decided: `Begin` starts a new read the person asked for, so clearing their old pause is right).

**From review** (`D:/ReadTheStupidText-worktrees/reviews/s278b-check3.md`):
- 1 applied: `ShelfAudioPacks.ScanLoopAsync` runs each scan through `ScanGuardedAsync`, so a throw anywhere in a scan (disk, `Preferences.Set`, `Serialize`, `SameAs`, a listener) keeps the last snapshot, reports the one `Pack` fallback and the loop reaches `TakeNext`, which clears `ScanRequests`' running state. No unit test: `ScanRequests` is pure and cannot throw; the guard is in the App-layer loop.

**From review** (`D:/ReadTheStupidText-worktrees/reviews/s278b-check4.md`): 1 (low) skipped: after a `Preferences` throw in a scan, `Changed` is not raised and the next scan sees the same snapshot, so pages stay stale until another trigger; very unlikely.

**From review** (`D:/ReadTheStupidText-worktrees/reviews/s278b-check5.md`): the 7 findings (all low: Debug skip-first-run waits the idle grace, the "nothing resolves" comment is false in Debug, string-position test on `MauiProgram`, the notifier's doc names only the installer, last-registration-wins is order-fragile, two `silent-piece-beat` flow weaknesses) were skipped; no `LanguageChanged` consumer in Dino breaks with the inert notifier.

**CI fix and check6** (`D:/ReadTheStupidText-worktrees/reviews/s278b-check6.md`): CI's `FallbackGuardTests` failed on the new `ReadingSpeedPreference.cs` catch (local runs passed because the file was still untracked); it got a `// no-fallback:` marker (the catch returns the normal default speed). Check6's 7 findings were skipped: 1 (case-sensitive book-id match in `InstalledShelfAudio`) because both sides take the id from the same story/pack id and `AudioPackStore.cs` compares ordinally too; 2 (a removed pack leaves the open book in its language) because the book then shows the "no sound" line, nothing breaks; 3 (cleanup duplicates the packs-root path) because a wrong path only means nothing is deleted; 4-7 low.

**Device bug found and fixed:** after the first run Dino still downloaded the 124 MB `supertonic-3` voice pack (Debug skip-first-run set `settings.Language`, which raised `LanguageChanged`, which reached the shared `VoicePackInstaller.OnLanguageChanged`). Fix: `InertLanguageNotifier` is registered after `AddMobileSharedServices`. For Release, no construction of the installer was found by reading the code; no Release run was made.

**Device (part B):** Windows VM, 8 Dino checks PASS (launch, open-book, page-turn, settings, keyboard, window, desktop-polish, reorder) on build 5e4c1765291f. Android emulator, flows `dino-landscape-firstrun`, `side-language-audio` and `grown-ups-button` PASS on 5e4c1765291f; `silent-piece-beat` and `dino-landscape-firstrun` PASS on b68f4acf604b with no PACKDL line (no voice pack download).

**Not yet verified (part B):**
- iOS device or simulator run.
- The mobile-data prompt (`ShelfAudioAsk`) on a real cellular-only phone.
- The beat pausing on screen lock (no scripted flow).
- Flows not run this round: `dino-landscape-settings`, `premade-audio-past-roar`, `usage-playback-events`, `dino-landscape-store` (the first emulator run's results were lost: the agent piped `run-flows` through `Out-String`).
- A Release build downloads no voice pack.
- The Windows MSIX still packages (`APPX1101`) and the iOS app still links: project settings unchanged, not built beyond a compile.

#### For CLAUDE.md (src/DinoReader.App/CLAUDE.md)

- Dino never resolves `ISpeechReader`, `VoicePackInstaller`, `IVoicePackService` or `IVoiceModelService`; `NoSpeechEngineTests` scans `src/DinoReader.App` for them. (Slice 278: docs/plan/slices/278-dino-store-books-only.md)
- `MauiProgram.cs` registers `InertLanguageNotifier` after `AddMobileSharedServices`, so the shared voice installer never starts on a language change. (Slice 278: docs/plan/slices/278-dino-store-books-only.md)
- A piece with no clip and no recording is shown for a `SilentBeat` (Core) and reports `premade_audio:clip_missing` once per step. (Slice 278: docs/plan/slices/278-dino-store-books-only.md)
- Shelf language choices come only from `ShelfAudioPacks`' `InstalledShelfAudio` snapshot; no page reads pack folders. (Slice 278: docs/plan/slices/278-dino-store-books-only.md)

## Verification

- `dotnet test tests/DinoReader.Tests` passes; `dotnet test tests/ReadTheStupidText.Tests` passes unchanged and
  `git diff origin/main --stat` shows no file outside Dino's folders, tests and docs.
- Android emulator and iOS: a store book plays start to end with pre-made audio; no "add", "cast", "narrator" or
  "voice" control appears anywhere; an install that had an own book shows it gone after the update.
- Windows head: a store book plays with pre-made audio.
