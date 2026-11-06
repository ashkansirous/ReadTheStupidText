# Slice 273 — Every fallback sends an analytics event (both apps, every platform)

*In progress (wave 2026-11-06), draft PR: part A.* Planned 2026-11-05. Part A's own Touches line (below, under Work) is this PR's scope; parallel with Slice 274, which owns the Dino lifecycle and grown-ups pages. User, 2026-11-05: "all fallback cases in the whole code should add an analytics event", after a Farsi Dino read fell back to the device voice from page 5 on and our data showed nothing (Slice 272).

**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `worker/CLAUDE.md`, `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.Core/AudioPackUsage.cs`, `src/DinoReader.Core/FallbackUsage.cs` (new), `src/DinoReader.App/**`, `src/ReadTheStupidText.Application/Usage/**`, `src/ReadTheStupidText.Application/Usage/FallbackKind.cs` (new), `src/ReadTheStupidText.Application/Reading/**`, `src/ReadTheStupidText.Application/Translation/ReadTranslator.cs`, `src/ReadTheStupidText.Infrastructure/Reading/**`, `src/ReadTheStupidText.Infrastructure/Usage/**`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/ReadTheStupidText.Mobile/**`, `src/ReadTheStupidText.App/**`, `src/ReadTheStupidText.Windows.Tray/**`, `tests/DinoReader.Tests/**`, `tests/ReadTheStupidText.Tests/**`, `.maestro/dino/**`, `docs/plan/slices/273-fallback-events-everywhere.md`. Parts A-D below narrow this.

## What we have today

The inventory (read from origin/main, 2026-11-05): [notes/2026-11-05-fallback-inventory.md](../notes/2026-11-05-fallback-inventory.md). About 40 fallback rows in 9 areas; only two send a full event (Dino `read_voice device:<reason>`, `audio_pack_download failed:<kind>`), a few are partly visible (RTST `read_started` engine, `timing ... failed`). RTST has no reason field at all. The inventory read the main paths and grouped other catches as a class: part D reads those line by line.

Two known faults in the existing event, both from Slice 272: `read_voice` says `premade` when any piece had a clip, so a read that falls back from page 5 on looks good; and a pack that is installed but out of line with the text is reported as `clip_missing`, not `text_mismatch`.

## Decisions (taken while planning, user may override)

- **One event, `fallback`**, in both apps: `area` and `reason` from closed lists (the inventory's proposed names), plus the existing columns (app, version, platform, book and language where the fallback has them). No free text, file names or book text (worker/CLAUDE.md). The Worker drops values not on its list, so the client enums and `worker/events.ts` lists must match, with a test that compares them.
- **At most once per (area, reason, book, language) per session**, with a count of how many times it happened in that session, so a fallback inside a loop (every piece of a book) is one row, not hundreds.
- **Production only** (`production-only-analytics`): the same gate as every usage event; tests block the endpoint.
- **Not instrumented:** catches inside crash reporting, the rolling log and the usage sender (CrashReporter, RollingLogFile, PendingCrashStore, UsageService, DinoLog): an event there could loop. Every other fallback is instrumented, cosmetic ones (window placement, DPI, font) included, because the user asked for all.
- **Errors that are not fallbacks** (file too large, user input rejected) stay as they are.

## Work

Part A is the smallest end-to-end piece and goes first; B, C and D can run in parallel after A merges (their Touches do not overlap beyond the Worker list, which A creates and each later part appends to inside its own block).

- [ ] **A. The event, the Worker, and Dino pre-made audio.** Touches: `worker/events.ts`, `worker/events.test.mjs`, `worker/CLAUDE.md`, `src/DinoReader.Core/{UsageEvent,AudioPackUsage,FallbackUsage}.cs`, `src/ReadTheStupidText.Application/Usage/**`, `src/DinoReader.App/{StoryVoicing,ReadingPage.xaml,ReadingSession,UsageCounter}.cs`, `tests/**/Usage*Tests.cs`, `tests/DinoReader.Tests/AudioPackUsageTests.cs`, `.maestro/dino/usage-playback-events.yaml`.
  - The `fallback` event shape, client side in both apps, Worker list and test; once-per-session-with-count.
  - `read_voice` becomes `premade`, `partial` (some pieces fell back) or `device:<reason>`, with the number of pieces read by clips and by the device voice; an installed pack whose hashes miss is `text_mismatch`.
  - Verify: a unit test per reason; the emulator flow `usage-playback-events` sees a `partial` read on a book whose pack is made to miss (a test fixture, never a live pack).
- [ ] **B. Speech, voices and translation** (inventory sections 1, 2 row 2, 4). Touches: `src/ReadTheStupidText.Application/Reading/**`, `src/ReadTheStupidText.Application/Translation/ReadTranslator.cs`, `src/ReadTheStupidText.Infrastructure/Reading/**`, `src/ReadTheStupidText.Mobile.Shared/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/*/…SpeechReader*.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/MlKitTranslator.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/DinoReader.App/{StoryTranslator,ReadingSession}.cs`, `src/DinoReader.Core/{CharacterTraits,CharacterRosterPackVoices}.cs`, their tests. Android and iOS rows each get their event in this part.
- [ ] **C. Packs, OCR, documents, store and config** (sections 3, 5, 6, 7). Touches: `src/DinoReader.App/{AudioPackStore,BookUpdater,StorytellerDefault,StoryLanguages,StoreClient,PagesStep.xaml}.cs`, `src/ReadTheStupidText.Mobile.Shared/{Ocr,Usage,Packs}/**`, `src/ReadTheStupidText.Mobile/{MobileVoicePackService,ScanPackFlow,OcrPackInstaller,UiCulture}.cs`, Windows voice model service, their tests.
- [ ] **D. Platform gaps and the swallowed-error class** (sections 8, 9). Read every catch the inventory grouped (Dino GalleryPage, ReadingPage, CastPage, RecordPage, StoryPackageReader, FileStoryLibrary, App.xaml.cs; RTST ReviewPage, CapturePage; ReadingSession best-effort paths) line by line; each one that swaps to a default gets an event, each that does not gets a one-line reason in this file. Font, window placement and selection-via-copy fallbacks. Touches: those files, `src/DinoReader.App/Platforms/**`, `src/ReadTheStupidText.App/**`, `src/ReadTheStupidText.Windows.Tray/**`.
- [ ] **E. A guard for new code**: a test (or analyzer) that fails when a new `catch` or fallback branch in app code has neither a `fallback` event nor an allow-list entry with its reason, so the rule holds after this slice.

## Verification

- Unit tests per reason in both test suites; Worker test that every client reason is on its list and nothing else is.
- Emulator (Dino and RTST Android): one scripted flow per part that forces a fallback (Debug test hook) and reads the sent batch; Windows VM for the Windows-only rows (Part B neural to WinRT, Part D selection).
- After release: `analytics-query` shows `fallback` rows from production builds within a day, and none from tests or CI.
