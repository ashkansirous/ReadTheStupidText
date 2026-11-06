# Slice 278 — Dino: store books only (no own books, no scanning, no translation, no voice engine, no voice pickers)

*Part A built 2026-11-06 (wave 2026-11-06, merged as PR #859, 09e2e892), not yet verified on every platform (see Not yet verified below); parts B and C planned. Part B is unblocked: Slice 277 merged as PR #858 on 2026-11-06.* Dino Reader only (Android, iOS and the Windows head, the same in the same parts). **Read The
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

- [ ] B1. Reading plays only pre-made clips and recordings: remove the device-voice path from `StoryVoicing` /
  `ReadingClips` / `ReadingSession`, and every speech engine, voice pack download and voice consent from Dino
  (Android, iOS, Windows head).
- [ ] B2. Remove the cast page, narrator page, voice preview and their entries in the book sheet and settings; drop
  narrator recasting. A stored cast in a saved book is ignored on load (no crash on old files).
- [ ] B3. First run no longer waits for a voice download: language, first books, then the shelf (the books' audio
  packs still download with the books, as today).
- [ ] B4. Remove the sherpa-onnx and voice-pack references from the Dino project only (`DinoReader.App.csproj`);
  never from shared projects (same check and *Outstanding work* note as A4 if they still come in through them).
- [ ] B5. Tests updated; a test that reading a page with no clip and no recording never asks for a speech engine.

**Part C — design**

*Part C: In progress (wave 2026-11-06), draft PR, built alongside part B (it edits only `docs/design/dino-reader/**`). Open point taken as its default: the older `.dc.html` batch files stay verbatim (they are copies of the Claude Design project); the README screen map in `docs/design/dino-reader/` marks each removed frame range "retired by Slice 278" and points to Batch 48 frame 186.*

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

## Verification

- `dotnet test tests/DinoReader.Tests` passes; `dotnet test tests/ReadTheStupidText.Tests` passes unchanged and
  `git diff origin/main --stat` shows no file outside Dino's folders, tests and docs.
- Android emulator and iOS: a store book plays start to end with pre-made audio; no "add", "cast", "narrator" or
  "voice" control appears anywhere; an install that had an own book shows it gone after the update.
- Windows head: a store book plays with pre-made audio.
