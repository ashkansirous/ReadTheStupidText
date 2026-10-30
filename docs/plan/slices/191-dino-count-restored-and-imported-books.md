### Slice 191 — Dino: count restored and imported books as added books

*Built (wave 2026-10-30), PR #581.* *Planned 2026-10-29.* Dino Reader (Android, iOS and the Windows head share the code). Gap found in Slice 134.

**Touches:** `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.App/BackupPage.xaml.cs`, the user-facing `.dino` import path in `src/DinoReader.App/` (find it; store and starter imports through `StoryPackageReader.ImportAsync` are not user adds and stay uncounted), `worker/events.ts`, `worker/events.test.mjs`, `tools/dino-usage-report.ps1`, `tests/DinoReader.Tests/**`, `STORE.md`, `PRIVACY.md`, `site/dino/privacy.html`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/UsageCounter.cs`, `src/ReadTheStupidText.Mobile.Shared/Usage/UsageQueueSender.cs` (additive `EnqueueMany`), `docs/plan/slices/191-dino-count-restored-and-imported-books.md`.

#### Why

`own_book_added` is sent only from the add-book wizard (`TextStep.xaml.cs`). A backup restore and a `.dino` file import also put books on the shelf and are not counted, so the count of books families add is low.

#### Decisions (user, 2026-10-29)

- Count them, with two new own-book kinds, `restore` and `import`, so they never mix with books made in the app.
- The event keeps its closed-list fields only: kind plus the page-count bucket (Slice 178), no title or id.

#### Claim notes (2026-10-30, grounded)

- No user-facing `.dino` import exists (the only pickers are `BackupPage.xaml.cs:172` restore and `PagesStep.xaml.cs:734` wizard files; `StoryPackageReader.ImportAsync` is used only by starters and the store). Drop the `import` kind and its item: count `restore` only.
- Check `UsageDetail` for an existing kind first (Slice 178 touched own-book kinds) before adding `KindRestore`.

#### Work

- [x] `UsageDetail.KindRestore` (no `KindImport`, see Claim notes); unit tests for the detail strings.
- [x] Backup restore sends one `own_book_added` per restored book with kind `restore` (not one per backup).
- [x] No user-facing `.dino` import exists (Claim notes): `import` kind dropped, nothing built.
- [x] `worker/events.ts` accepts `restore` (closed list), with tests.
- [x] `tools/dino-usage-report.ps1`: rows group by `blob9` detail, so `restore:<bucket>` is its own row; heading says so.
- [x] `STORE.md` and `site/dino/privacy.html` list restore among the add kinds (`PRIVACY.md` has no Dino usage text).

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`; `npm test` for the Worker.
- Emulator (Debug, usage switched off, Debug log marker): restore a backup with 2 own books gives two `own_book_added` lines with `restore` (not run, see Not yet verified).
- iOS: compile here; same check on TestFlight.

#### Built (2026-10-30)

`StoryBackupReader.RestoreAsync` takes an optional `onAdded` callback; `UsageDetail.RestoredBook` gives `restore:<pages bucket>` for a restored own book (no `SourceId`); restored starter and store books are not counted. `BackupPage` collects the added books and queues them all in one write off the UI thread (`UsageCounter.RecordMany` -> `UsageQueueSender.EnqueueMany`). A throwing callback cannot abort the restore (`StoryBackupReader` guards it). The existing dry-run log (`usage batch of N not sent: ...: <json>`) is the Debug marker.

#### Not yet verified
- Emulator run 2026-10-30 (inconclusive): wizard book, Backup save and the picker restore were driven end to end ("Putting the books back..." finished), but no `own_book_added`/usage-batch line showed in logcat at add or restore (Debug sending is off; the batch log may not reach logcat), and whether the own book came back was not seen.
- Odd sighting on that run, not explained: after the grown-up gate the new own book was missing from the library (4 starters, Backup page "All 4 stories") with no delete. Re-check on a device before trusting the restore count; may be unrelated to this slice.

- Emulator restore check not run: restore a backup holding 2 own books and expect two `own_book_added` ... `restore:` entries in the dry-run log (and none for a starter or store book). Scripted check not added (`tools/emulator` is outside this slice's Touches).
