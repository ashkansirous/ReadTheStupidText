# Slice 261 — Dino: no starter books in the app; a first-run book picker (choose at least 2)

*Planned 2026-11-04.* Dino Reader only; Android and iOS alike (and the Windows head, which shares these pages). After Slice 260 (the picker shows its tags). Slice 265 removes the starters' special handling from CI and the book builder.

**Touches:** `src/DinoReader.App/StarterStories.cs`, `src/DinoReader.App/Resources/Raw/starters/**`, `src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/FirstBooksPage*.cs` (new), `src/DinoReader.App/IntroWizardPage*.cs`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/StagedBookUpdates.cs`, `src/DinoReader.App/BookOrigins.cs`, `src/DinoReader.App/UsageCounter.cs`, `src/DinoReader.App/DinoTestCommands.cs`, `src/DinoReader.Core/FirstBooks*.cs` (new), `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/**`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/261-dino-no-starters-first-run-book-picker.md`.

## Why

Bundling the starter books' pre-made audio would have made the app about 92 MB bigger (Android APK 57 to about 149 MB, iPhone IPA 90 to about 182 MB, measured 2026-11-04 from dino-v1.100.0 and the 60 built packs). The user chose instead to take the starter books out of the app (about 13 MB today) and let every family pick their first books from the store.

## Decisions (user, 2026-11-04, RefineScope)

- First run order: **main language, then choose books (at least 2), then the rest of the intro wizard**. The book page is mandatory; there is no skip.
- The wizard's "store" step goes (the book picker replaces it). Its other steps stay in their order.
- Offline on the book page: it says "Connect to the internet to choose your first books" with a Try again button, and the app goes no further until two books are downloaded.
- The picker lists the store's books with their tags (Slice 260), tagged first; each card can be ticked; Continue is enabled from two ticks and downloads them with progress (`WaitController`, Cancel), then the wizard goes on.
- People who already have the app keep the four starter books on their shelf: they are the same ids as the store copies, so they update and get audio like any store book. Only a fresh install starts empty with the picker.

## Work

- [ ] Core: `FirstBooks` rules (minimum 2, tagged first, which picked books are still downloading), with unit tests.
- [ ] `FirstBooksPage`: after the language page; store list with tick boxes and tag badges; Continue from two ticks; download with progress and Cancel; offline message with Try again; every word from `AppResources` (Slice 159), layout never mirrors (Slice 96).
- [ ] `FirstRunPage`: no starter install, no cover strip of starters; the "ready" text no longer names them; the intro art (`IntroWizardPage.Art.cs`) uses art from the app, not a starter book.
- [ ] Intro wizard: drop the store step (`IntroSteps`, `UsageEvent.cs`), keep the rest; a replay from Settings has no store step either.
- [ ] Remove `Resources/Raw/starters/` and its `MauiAsset` lines; `StarterStories` keeps only what existing installs need (the installed flag and `IsStarterId` for books already on a shelf); `BookUpdater`, `StagedBookUpdates`, `BookOrigins`, `UsageCounter`, `StoreClient` treat those four ids as store books.
- [ ] Device-test hook: a Debug-only command that imports a test book from the test folder, so the Maestro flows that opened a starter book still have a book (they cannot download from the live store). Move every flow that used a starter to it.
- [ ] Tests that read the committed starter `.dino` files read the books built from `content/dino/` instead.
- [ ] Android task: first run on the emulator (language, picker, two books, wizard). iOS task: the same pages compile and run on the simulator build in CI (shared code); nothing Android-only.
- [ ] `src/DinoReader.App/CLAUDE.md`: replace the starter-stories rule with the first-books rule.

## Verification

- `dotnet test tests/DinoReader.Tests`; the Dino APK no longer contains `starters/` (list the APK's assets) and is about 13 MB smaller.
- Emulator flows: `first-run` (language, picker with a test catalogue, two ticks, wizard without a store step), offline picker message, and every moved flow passes.
- An upgrade from the current release keeps its four books on the shelf (emulator: install the release APK, then the new one).
