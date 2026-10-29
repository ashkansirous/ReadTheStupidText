# Slice 178 — Dino: count how big an own book is and how it really came in

*In progress (wave 2026-10-28), draft PR.* Dino Reader (Android, iOS and Windows: the code is shared in `DinoReader.App`, so all heads get it in one change).
Asked for by the user on 2026-10-28 after an analytics check.

**Touches:** `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.App/PagesStep.xaml.cs`,
`src/DinoReader.App/StoryWizardPage.xaml.cs`, `src/DinoReader.App/TextStep.xaml.cs`, `worker/events.ts`,
`worker/events.test.mjs`, `tests/DinoReader.Tests/**`, `docs/plan/slices/178-dino-own-book-usage-detail.md`

**Sequencing:** #484 has merged; the open Farsi PR #435 also edits `UsageEvent.cs` and `worker/events.ts` (#435 also `TextStep.xaml.cs`).
Small overlap; build after they merge, or rebase on them.

## Why

On 2026-10-28 the user asked how many pages the books people added had, and whether they came from a PDF, photos or a scan.
The analytics can't answer either question:

- `own_book_added` (Slice 134) carries only a kind: `photo`, `pdf`, `scan` or `typed`. A `.docx` or `.txt` file import never
  notes a kind (`PagesStep.xaml.cs` notes only scan, photo and pdf), so it falls back to `typed` (`TextStep.xaml.cs:316`,
  `session.OwnBookKind ?? KindTyped`). All 9 own books added on 27-28 Oct counted as `typed`, which may hide file imports.
- `NoteKind` keeps the first kind only (`StoryWizardPage.xaml.cs:49`), so a book made from photos and then typed pages counts
  as the first one.
- Nothing records an own book's page count. The only page bucket is on `recording_made`.

## Decisions (obvious defaults, no open questions)

- **Detail shape:** `own_book_added` detail becomes `<kind>:<pages>`, e.g. `pdf:6-10`, the same `:` pattern as the wait counts
  (Slice 148). Pages reuse `UsageDetail.PagesBucket` (`1`, `2-5`, `6-10`, `11+`), counted from the saved book's pages.
- **Kinds:** `photo`, `pdf`, `scan`, `typed` stay; add `docx`, `txt` (file imports by extension) and `mixed` (more than one way
  in the same book). `typed` then means typed or pasted only.
- **Worker:** accepts the new `<kind>:<pages>` shape and still accepts the old plain kinds, so older app versions keep counting.
- **Privacy:** still closed-list values only, nothing the grown-up wrote or picked; no new data category, so the store
  privacy rows don't change.

## Work

- [ ] `UsageDetail`: add `KindDocx`, `KindTxt`, `KindMixed` and an `OwnBookAdded(kind, pageCount)` helper that returns `<kind>:<bucket>`.
- [ ] `WizardSession.NoteKind`: remember every kind noted; report `mixed` when typed pages or a second kind join the first.
- [ ] `PagesStep`: note `docx` / `txt` on a file import (by extension); keep scan/photo/pdf.
- [ ] `TextStep.CountNewOwnBook`: send `OwnBookAdded(kind, saved page count)`.
- [ ] `worker/events.ts`: `own_book_added` detail accepts `kind:bucket` (new kinds included) or an old plain kind; tests in `events.test.mjs`.
- [ ] Unit tests in `tests/DinoReader.Tests` for the helper (each bucket edge, each kind, mixed).
- [ ] After release: add the new field to the `analytics-query` notes (blob9 for Dino `own_book_added` is `kind:pages`).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and the Worker tests (`npm test`) pass.
- Android emulator and iOS simulator Debug runs: add a book from a PDF, a `.docx`, photos and typed text; a debug log line
  (the gate drops the send in Debug, Slice 167) shows the detail each would send, e.g. `pdf:2-5`, `docx:1`, `mixed:6-10`.
- After the first release with it: an `analytics-query` run shows `own_book_added` rows with the new shape from that version on.
