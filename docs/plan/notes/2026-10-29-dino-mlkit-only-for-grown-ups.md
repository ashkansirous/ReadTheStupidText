# 2026-10-29 Dino: ML Kit only for grown-ups

Branch `fix/dino-mlkit-lazy`. A follow-up fix, not a slice.

## Why

Google ML Kit (translation, OCR, the document scanner) sends Google usage data that can't be switched off: a per-installation id, device details, timings, the language pair, plus Firebase Installations and Remote Config calls ([Android](https://developers.google.com/ml-kit/android-data-disclosure), [iOS](https://developers.google.com/ml-kit/ios-data-disclosure)). Replacing it in Dino was ruled out: our Opus-MT packs have no Persian model, and blocking the endpoints is fragile. The user's decision: keep ML Kit, but make sure a child never causes a Google call. ML Kit may only load on grown-up paths.

## What changed

- Dino's Android manifest removes ML Kit's `MlKitInitProvider` (it ran at process start). `Mobile.Shared/Platforms/Android/MlKitInit.cs` starts ML Kit by hand (`MlKitContext.InitializeIfNeeded`) before every use: translator, text recognizer, document scanner (start and warm-up; a failure surfaces as `DocumentScannerStartFailedException`). RTST keeps the provider, so the call does nothing there.
- `StoryTranslator` resolves the ML Kit engine lazily, off the UI thread, only on grown-up paths; its language list comes from the plain table `Application/Translation/MlKitLanguages.cs`. `AutoDownload = false` is set at Dino's DI registration.
- Dino keeps its own record of downloaded translation packs (`DinoReader.Core/TranslationPackRecord.cs`, `DinoReader.App/TranslationPackStore.cs`, locked writes). The reading screen decides from that record and the book's tables (`DinoReader.Core/ReadingTranslation.cs`), never from ML Kit. During reading the engine is used only when the record holds every pack of the pair and a table has gaps.
- Reconcile with ML Kit's model list is additive and single-flight, and runs only on grown-up paths (Settings behind the gate, Story languages, the pack prompt, own-book translate/save). On "model missing" at translate time, a background re-check forgets only the packs ML Kit says are gone.
- Upgrades: the first run after the update seeds the record without ML Kit from the stored story languages (`TranslationPackSeed`); a fresh install is never seeded.
- Behaviour change (intended): a book with a partial table offers a language only when its packs are recorded.

## Checks

- DinoReader.Tests 1756 passed; ReadTheStupidText.Tests 1577 passed, 2 skipped. Android builds of Dino and RTST and the iOS compile of both: 0 errors.
- Two review passes (`reviews/mlkit-lazy-review1.md`, `-review2.md`); all findings applied except review 2 #5 (checking `MlKitLanguages` against the real engine needs the engine).
- Emulator, logcat by app pid (`mlkit|datatransport|firebase|TransportRuntime|LibraryVersion`): a fresh install in Farsi through the wizard, shelf, a starter book and a typed own book gave 0 ML Kit lines; the first ML Kit lines appear on Settings after the grown-up gate. An upgrade from origin/main with side language English gave 0 lines on start, shelf and a starter book, and the record seeded `en,fa`. The scanner on the emulator shows Play services' error with no crash. RTST starts with no crash.

## Not yet verified

- Own-book translation and pack download on a device after the change.
- RTST Review page translate and language detection at runtime.
- iOS at runtime (compile only; iOS has no start-up provider).
- A Maestro flow asserting no `MLKIT_INIT` after the shelf and a book (the Debug markers `RTST-TEST: MLKIT_INIT` and `MLKIT_ENGINE_RESOLVED` exist for it).

## For CLAUDE.md

In `src/DinoReader.App/CLAUDE.md`:

- Dino never touches ML Kit (translation, OCR, scanner) on a child path: shelf, reading, intro wizard. The reading screen asks Dino's pack record (`TranslationPackRecord`), never ML Kit; ML Kit loads only behind the grown-up gate. Dino's Android manifest removes `MlKitInitProvider`, so call `MlKitInit.EnsureInitialized()` before any new ML Kit use. (Note: docs/plan/notes/2026-10-29-dino-mlkit-only-for-grown-ups.md)
