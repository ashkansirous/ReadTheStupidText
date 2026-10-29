### Slice 191 — Dino: count restored and imported books as added books

*Planned 2026-10-29.* Dino Reader (Android, iOS and the Windows head share the code). Gap found in Slice 134.

**Touches:** `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.App/BackupPage.xaml.cs`, the user-facing `.dino` import path in `src/DinoReader.App/` (find it; store and starter imports through `StoryPackageReader.ImportAsync` are not user adds and stay uncounted), `worker/events.ts`, `worker/events.test.mjs`, `tools/dino-usage-report.ps1`, `tests/DinoReader.Tests/**`, `STORE.md`, `PRIVACY.md`, `site/dino/privacy.html`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/191-dino-count-restored-and-imported-books.md`.

#### Why

`own_book_added` is sent only from the add-book wizard (`TextStep.xaml.cs`). A backup restore and a `.dino` file import also put books on the shelf and are not counted, so the count of books families add is low.

#### Decisions (user, 2026-10-29)

- Count them, with two new own-book kinds, `restore` and `import`, so they never mix with books made in the app.
- The event keeps its closed-list fields only: kind plus the page-count bucket (Slice 178), no title or id.

#### Work

- [ ] `UsageDetail.KindRestore` / `KindImport`; unit tests for the detail strings.
- [ ] Backup restore sends one `own_book_added` per restored book with kind `restore` (not one per backup).
- [ ] A user-opened `.dino` file sends one with kind `import`; if the app has no user-facing `.dino` import, record that here and drop this item.
- [ ] `worker/events.ts` accepts the two kinds (closed list), with tests.
- [ ] `tools/dino-usage-report.ps1` shows them as their own rows.
- [ ] `STORE.md` and the privacy texts list restore and import among the add kinds.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`; `npm test` for the Worker.
- Emulator (Debug, usage switched off, Debug log marker): restore a backup with 2 books gives two `own_book_added` lines with `restore`.
- iOS: compile here; same check on TestFlight.
