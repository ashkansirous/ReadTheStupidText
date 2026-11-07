# Slice 299 — Slice 261 leftovers (dead `LaunchedFromIntro`, Cancel on list load, starter names)

*Built 2026-11-07, merged as PR #912 (08f65b2f); items 1 and 3 built, item 2 not built (see Decisions), not yet verified on a device.* (Planned 2026-11-07.) Three leftovers from [Slice 261](261-dino-no-starters-first-run-book-picker.md), not built. Moved from plan.md's *Outstanding work*. App code is shared MAUI (Android and iOS together).

**Touches:** `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/FirstBooksPage.cs`, `src/DinoReader.App/CLAUDE.md`, `.github/scripts/dino-starters.json`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books*.test.mjs`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets*.test.mjs`, `.github/workflows/dino-store.yml`, `.github/CLAUDE.md`, `tests/DinoReader.Tests/BookBuilderTests.cs`, `tests/DinoReader.Tests/FirstBooks*Tests.cs`, `docs/plan/slices/299-slice-261-leftovers.md`, `docs/plan/unverified.md`

**Claim notes (wave 2026-11-07, run-all-waves):** premise check: item 1, nothing sets `StorePage.LaunchedFromIntro`; `IntroWizardPage.cs:637` sets the RecordPage/RecordIntroPage property, which stays. Item 2: `StorePage.LoadAsync` already treats a cancel as non-offline (`StorePage.xaml.cs:326-352`, `StoreClient.cs:91,119`); the first-run picker `FirstBooksPage.cs:191` is the likely place a cancelled load shows offline: reproduce there with a test first, fix only if it fails. Item 3: the json is read by `publish-store-books.mjs` (`--check-content`), `dino-store.yml:61` and its paths filter; remove it and those reads (or rename to what it does now if the check still has a job), keep CI green.

## Work

- [x] 1. `StorePage` `LaunchedFromIntro` (`StorePage.xaml.cs:562`) is dead code: remove it, and the intro-side setters that only fed it (check `IntroWizardPage.cs`, `RecordIntroPage.cs`, `RecordPage.cs`, which share the property name; touch those only if they set this one).
- [ ] 2. (not built, see Decisions) Cancel during the store list load shows the offline copy: Cancel must leave the page as before the load (or close it), not show "offline". Test with a fake client whose list call is cancelled.
- [x] 3. `dino-starters.json` and `BookBuilderTests` still name starters, though there are none (Slice 261): remove the file and the tests' references, or rename them to what they do now. Check `.github/` workflows that read the json before deleting.

## Decisions (wave 2026-11-07)

- **Item 2 not built.** Main's behaviour is kept: `FirstBooksPage.cs:199-203` shows the load-failed layer with Try again after a Cancel, by design per its comment. That layer's copy says "Connect to the internet" (`FirstBooks_OfflineTitle`/`Body`), so a Cancel reads as offline; a distinct cancelled/failed copy needs new strings in every language, outside this slice: follow-up.
- **Item 3, `--check-content` dropped.** Checked what kind Starter still does: no book is bundled in the app (`DinoReader.App.csproj:94`, Slice 261; the four former starters are Debug-only test books), `ship-targets.mjs` routes every `content/dino/**` path to `dinostore` only, and `BookKind`/`BookKindConverter` in `Books.cs` already reject an unknown kind in BookBuilder. `Starter` is only a label now, so the check (reject a kind that is neither Starter nor Store) guarded nothing: the script flag, `unknownKinds` and the `dino-store.yml` step are removed. The ship-targets tests read the book stems from the real `content/dino/*/book.json`.

- **Bad `kind` still fails before any upload (review 2).** An unknown `kind` in a `book.json` throws in `Books.ReadInfo` (`tools/DinoReader.BookBuilder/Books.cs:164-166`, `BookKindConverter`, `Books.cs:18`) while `Books.All` loads (`Books.cs:117,133`), which `BookCommands.BuildAsync` triggers first via `Choose` (`BookCommands.cs:20`); `Program.cs:81-84` turns it into a failing exit code. The same load happens in the earlier `check-audio` step. All of this runs before the upload step, so no partial upload; no up-front check needed.
- **`Books.Starters` still has callers (review 6), not removed here:** `tools/DinoReader.BookBuilder/StoreBuilder.cs:33`, `StarterBuilder.cs:37,41,44`, `BookCommands.cs:68,128,196-214`; tests `BookBuilderTests.cs:26`, `StoreStoryTests.cs:30,121,435,462`, `GentleTextTests.cs:22`, `AuthoredVoiceTests.cs:428,432`, `AudioPackTests.cs:151,162`, `AnimalFromBookTests.cs:131`, `StarterUpdateTests.cs:629`, `StarterStoryTests.cs:22`.

*Changed while building:* item 1 removed `StorePage.LaunchedFromIntro` (nothing set it). Item 3 removed `dino-starters.json`, `--check-content` and its `dino-store.yml` step; `Starter` is a label only (no book bundled, `DinoReader.App.csproj:94`), and an unknown kind fails in `build --live` before upload (`Books.cs:164-166`). Item 2 was not built (see Decisions).

*From review:* `reviews/s299-high.md` (all 7 applied); `reviews/s299-medium.md` (1, 2, 3 applied); `reviews/s299-check1.md` (none applied).
- Skipped #4 (`reviews/s299-medium.md`): "item 2 (Cancel on list load shows offline copy) left unbuilt, "follow-up" has no slice or Outstanding-work entry". The follow-up line goes to plan.md's *Outstanding work* in the index pass.
- Skipped #5 (`reviews/s299-medium.md`): "BOOK_STEMS read at import from content/dino; missing folder throws at module load, empty list makes loops vacuous" — the test runs only in this repo, where `content/dino` exists, so the module-load throw has no failure path; the empty-list case is covered by the non-empty assertion at `.github/scripts/ship-targets.test.mjs:625`.
- Skipped #6 (`reviews/s299-medium.md`): "Books.Starters (Books.cs:135) and the Starter-only contentHash rule (Books.cs:190) may now be dead". `Books.Starters` still has callers (Decisions, review 6).
- Skipped #1 (`reviews/s299-check1.md`): "a typo'd kind (e.g. "Strore") is now silently skipped by the storeIds filter and the job stays green". A bad kind fails earlier, in `Books.ReadInfo` (Decisions, review 2).
- Skipped #2 (`reviews/s299-check1.md`): "the dino-store.yml loop over BOOK_STEMS has no non-empty assertion, so it passes vacuously" — `BOOK_STEMS` is one list shared by both tests (`.github/scripts/ship-targets.test.mjs:13`), and `:625` already asserts it is non-empty in the same file and run.

## Verification

- Dino tests green; emulator and iOS: first-run picker, Cancel during the list load, back to the shelf.
- Run: `node --test .github/scripts/*.test.mjs` 266 pass; DinoReader.Tests 3833 pass; the Dino app compiles for Android, iOS and Windows. No device run (no runtime behaviour change).

#### For CLAUDE.md (lifted 2026-11-07)

None.

#### Not yet verified

- First-run picker and Cancel during the list load on emulator and iOS (item 2 follow-up: a cancelled load shows the "Connect to the internet" copy; a distinct copy needs new strings in every language).
