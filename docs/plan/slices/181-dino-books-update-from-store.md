### Slice 181 — Dino: books on the device update themselves from the store

*Built 2026-10-29 (PR #543); emulator-verified, see Not yet verified.*

**Touches:** `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StarterStories.cs`, `src/DinoReader.App/BookUpdater.cs` (new), `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.Core/StarterUpdate.cs`, `src/DinoReader.Core/BookUpdatePlan.cs` (new), `tests/DinoReader.Tests/BookUpdatePlanTests.cs` (new), `tests/DinoReader.Tests/StarterUpdateTests.cs`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/181-dino-books-update-from-store.md`.

#### Why

A book fixed in the store never reaches the phones that already have it. On 2026-10-28 the Gingerbread Man got its Farsi readings (PR #484, v10). Phones get starter books from the app (Decision 130: a newer bundled starter replaces an unedited copy on the next app update). A book a parent downloaded from the store stays at the version they downloaded, forever. Example: a parent added 4 books, 1 of them now has a newer version, and the app should fetch it by itself.

What exists (checked on `origin/main` 2026-10-28):
- `GET /api/stories` returns each book's `version`, starters included (they are built as store packages too, Decision 128; `worker/stories.ts:69,88`).
- The installed version of every book, starter or downloaded, is the preference `dino.starter.version.<id>` (`StarterStories.RecordStoreInstallAsync`, `StarterStories.cs:324`, called from `StoreClient.cs:178`).
- Nothing compares it with the store. `StoreClient.RunDownloadAsync` returns at once when the id is already on the shelf (`StoreClient.cs:147`); the Store page has no "update" state.
- The in-place swap already exists for starters: `StarterStories.ReplaceAsync` (`StarterStories.cs:199`) imports the new copy, carries over cast, per-language casts, translations and their switches, recordings and the cover photo (`StarterUpdate.Refresh`), then deletes the old copy. It is private to starters.
- Edit detection (`StarterUpdate.IsEdited`) compares page text with the bundled copy plus a hard-coded table of earlier starter texts (`EarlierPageTextHashes`). There is no record of a downloaded book's original text.

#### Decisions (user, 2026-10-28)

1. **When:** only when the app opens (a cold start, `DinoApp.CountLaunchAsync` in `App.xaml.cs`, after the app-config fetch), in the background, and **at most once every 7 days**. The app saves the time of its last finished check (preference `dino.books.lastupdatecheck`); an open less than 7 days after it makes no store call at all. Never on resume or return to the screen. (Corrected 2026-10-28: the first version of this slice added a check on resume, which the user had not asked for.)
2. **Which books:** every book on the shelf with a store id (starters and downloaded books) whose recorded version is below the store's. Own books (no store id) are never touched. A deleted book is not brought back (Decision 130 rule kept).
3. **Edited books are left alone:** if the parent changed a book's page text, it is never overwritten (same rule as starters). Unedited copies are replaced in place, keeping cast, voices, translations, recordings and cover photo.
4. **Any network:** Wi-Fi or mobile data (a book is about 3 MB). No download when offline; an offline or failed check does not save the time, so the next open tries again.
5. **Silent:** no notice, no badge. The book just has the new version the next time it is opened.

Decided without asking (my choice, not the user's): a book the child opens while the check runs is not swapped under them. A failed download leaves the old copy as it is. In both cases the time is not saved, so the next app open checks again instead of waiting 7 days. Books update one after another, not in parallel.

#### Design

- **Original text record.** Edit detection needs the page text the book had when it arrived. From this slice on, every store install and every update records `dino.book.texthash.<id>` = `StarterUpdate.PageTextHash` of the imported copy. A downloaded book installed before this slice has no record: the updater fetches its installed version once (`/api/stories/<id>/download?v=<installed>`; versions are never overwritten in R2), records its hash, then decides. Starters keep using the bundled copy and `EarlierPageTextHashes`, so nothing changes for them.
- **`BookUpdatePlan` (Core, pure, unit-tested):** given the store list (id, version), the installed records (id, version, text-hash record), the shelf (id, current page-text hash, open or not), it returns the books to update and, for each skipped one, why (`UpToDate`, `Edited`, `Open`, `Deleted`, `NotInStore`).
- **`BookUpdater` (App), redesigned after review 2:** fetches `/api/stories`, plans with `BookUpdatePlan` and only DOWNLOADS each due package into `StagedBookUpdates` (`book-updates/<id>/v<n>.dino`, version in the file name; copied under `.part` then renamed). It never touches a stored book. A 404 on a baseline or package skips that book (time still saved); a failed starter install, offline or failed download stops without saving; one static run gate.
- **`StagedBookUpdates` (App):** `ApplyOnceAsync` runs from `GalleryPage.OnAppearing` after `InstallOnceAsync` and before the first shelf read, once per process, so no screen can be open (the earlier "shelf is idle" checks failed twice and are gone; `ShelfBook.IsOpen` and `BookUpdateSkip.Open` removed). Each staged package is re-planned against the library as it is then (edited, deleted, already newer: entry deleted); `StarterStories.SwapFromStoreAsync` swaps it and the entry is deleted. A crash mid-apply keeps the entry; the next launch retries (the swap clears duplicates and re-replaces equal text). No visible loading state covers the apply (the gallery shows none while starters install); a swap is a local import, normally well under 500 ms.
- **Review 3 fixes:** (1) `ApplyOnceAsync` never throws: one shared task per process, every failure logged, a staged entry that fails for a non-storage reason is dropped (never the stored book), so no crash loop. (2) "Edited" covers pictures: the recorded hash is now `c2:` + SHA-256 of the page text and each page picture's bytes (`StarterUpdate.ContentHash`, `StoryPackageReader.ContentHashOfPackageAsync`); an old text-only hash whose text still matches is `NeedsBaseline` (the installed version is fetched again and recorded in the new form), never trusted for a store swap. The bundled-starter launch refresh keeps its text-only rule while the record is old. (3) The swap is journaled (`SwapMarker`, pref `dino.book.swapping`): written before the import, rewritten with the new copy's id once it holds the carried-over choices, cleared after the old copy is deleted and hash+version recorded; `InstallOnceAsync` finishes a leftover marker first (new copy complete: delete old, record; incomplete: delete the half-made copy, keep old). Carry-over also keeps the legacy `NarratorVoiceId` (`StarterUpdate.Refresh`; unit-tested with the other per-book choices). (4) `GalleryPage.RefreshAsync` awaits the shared apply task first, from every caller. Debug `[BookUpdate]` lines log apply start/end with elapsed ms and how long the gallery waited, to measure the 500 ms loading-state rule.
- **Edited books:** an edited copy records `dino.book.seenversion.<id>`; `BookUpdatePlan` skips it as Edited until the store version is above that, so there is no weekly re-download. Bundled starters installed fresh now record their text hash too.
- **`StoreClient.RunDownloadAsync`** keeps its "already on the shelf" shortcut for the Get button; the updater does not go through it.
- **Usage counts:** none added (Decision 145 rows would be needed first).

#### Work

- [x] Core: `BookUpdatePlan` + `BookUpdatePlanTests` (version below / equal / above, edited, open, deleted, not in store, no hash record).
- [x] Record `dino.book.texthash.<id>` on store install (`StoreClient`) and after every swap (`StarterStories`).
- [x] Share the swap: lift `ReplaceUnlessEditedAsync` / `ReplaceAsync` so downloaded books use the same path, with edit detection against the recorded hash for downloaded books.
- [x] `BookUpdater`: list, plan, download, swap, record; one run per app open; cancels cleanly when the app goes to the background.
- [x] Wire into `CountLaunchAsync` only (`App.xaml.cs`), after the config refresh; skip unless 7 days have passed since `dino.books.lastupdatecheck`; save the time only after a finished check (pure due-check in Core, with tests: never checked, 6 days, 7 days, clock moved back).
- [x] Legacy downloaded books with no hash record: fetch the installed version once and record its hash.
- [x] Android and iOS: the code is shared MAUI; both run the same updater (no platform code expected). Windows head runs it too.
- [x] Rule into `src/DinoReader.App/CLAUDE.md`: every store install and swap records the text hash; never swap an open book.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (new `BookUpdatePlanTests`, `StarterUpdateTests` still green).
- Emulator (Android): install a store book at an older version (download `?v=<n-1>` via a test hook or an old build), open the app online, confirm the book shows the new text and keeps its cast and a recording; edit another book's text, confirm it is not replaced; open a book, send the app to the background and back, confirm it is not swapped while open; airplane mode, confirm nothing breaks.
- iOS: the same on the simulator or TestFlight (no Mac locally; Outstanding work if not run).
- Windows VM: one update on the Windows head.

#### Changed while building

- The background run only stages packages (`book-updates/<id>/v<n>.dino`). The next cold start applies them before the shelf's first read (`StagedBookUpdates.ApplyOnceAsync`, awaited by `GalleryPage.RefreshAsync`). A swap journal (`SwapMarker`) is finished at launch, capped at 3 launches.
- The text hash (c2) covers text plus picture bytes. Edited books remember the version they last saw. A 404 from the store is `NotInStore` and is skipped.
- "Updating your books…" overlay after 500 ms (new string `Gallery_UpdatingBooks`; machine drafts, need native review).
- The narrator voice is global per language (by design); the legacy per-book `NarratorVoiceId` is rewritten from it on save.

#### From review

4 passes (`reviews/181-review1..4.md`). Two strikes on "book open during swap" led to a redesign: apply at next start instead of swapping live.

#### Checks

Dino 1694 tests, RTST suite, Android and iOS builds green. Emulator: not due, stage then swap, cast voice kept, edited text kept, force-stop after stage recovers, overlay shown during the 1.2-1.5 s apply and not on a normal start (5-13 ms), no FATAL.

#### For CLAUDE.md

- Dino book updates are staged in the background and applied only at the next cold start, before the shelf's first read (`StagedBookUpdates.ApplyOnceAsync`); never swap a book that may be open. Every install and swap records the text+picture hash. (Slice 181: docs/plan/slices/181-dino-books-update-from-store.md)

#### Not yet verified

- A recording carried over a swap, on a device.
- A store-downloaded book with edited text getting an update.
- Crash during the apply itself (kill mid-swap).
- iOS simulator and Windows VM runs.
- Apply time on a real phone (1.2-1.5 s on emulator Debug).
