# Slice 299 — Slice 261 leftovers (dead `LaunchedFromIntro`, Cancel on list load, starter names)

*In progress (wave 2026-11-07), draft PR.* (Planned 2026-11-07.) Three leftovers from [Slice 261](261-dino-no-starters-first-run-book-picker.md), not built. Moved from plan.md's *Outstanding work*. App code is shared MAUI (Android and iOS together).

**Touches:** `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/FirstBooksPage.cs`, `src/DinoReader.App/CLAUDE.md`, `.github/scripts/dino-starters.json`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books*.test.mjs`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets*.test.mjs`, `.github/workflows/dino-store.yml`, `.github/CLAUDE.md`, `tests/DinoReader.Tests/BookBuilderTests.cs`, `tests/DinoReader.Tests/FirstBooks*Tests.cs`, `docs/plan/slices/299-slice-261-leftovers.md`

**Claim notes (wave 2026-11-07, run-all-waves):** premise check: item 1, nothing sets `StorePage.LaunchedFromIntro`; `IntroWizardPage.cs:637` sets the RecordPage/RecordIntroPage property, which stays. Item 2: `StorePage.LoadAsync` already treats a cancel as non-offline (`StorePage.xaml.cs:326-352`, `StoreClient.cs:91,119`); the first-run picker `FirstBooksPage.cs:191` is the likely place a cancelled load shows offline: reproduce there with a test first, fix only if it fails. Item 3: the json is read by `publish-store-books.mjs` (`--check-content`), `dino-store.yml:61` and its paths filter; remove it and those reads (or rename to what it does now if the check still has a job), keep CI green.

## Work

- [ ] 1. `StorePage` `LaunchedFromIntro` (`StorePage.xaml.cs:562`) is dead code: remove it, and the intro-side setters that only fed it (check `IntroWizardPage.cs`, `RecordIntroPage.cs`, `RecordPage.cs`, which share the property name; touch those only if they set this one).
- [ ] 2. Cancel during the store list load shows the offline copy: Cancel must leave the page as before the load (or close it), not show "offline". Test with a fake client whose list call is cancelled.
- [ ] 3. `dino-starters.json` and `BookBuilderTests` still name starters, though there are none (Slice 261): remove the file and the tests' references, or rename them to what they do now. Check `.github/` workflows that read the json before deleting.

## Verification

- Dino tests green; emulator and iOS: first-run picker, Cancel during the list load, back to the shelf.
