# Slice 464 — Dino: an older part-take file never plays after a new take is saved
**Product:** Dino

**Touches:** `src/Dino/DinoReader.App/RecordPage.cs`, `src/Dino/DinoReader.Core/StoryRecordings.cs`, `src/Dino/DinoReader.Core/PartClipAdoption.cs`, `src/Dino/DinoReader.Core/FileStoryLibrary.cs`, `src/Dino/DinoReader.Core/PartTakeCommit.cs`, `tests/Dino/DinoReader.Tests/**`

*In progress (wave 2026-11-27), draft PR.*

*Planned 2026-11-27 (index pass of night wave 2) from `reviews/pr-1397-check3-medium.md` finding #1 (Slice 443's third check pass).*

**Claim note (coordinator, 2026-11-27):** the commit step (`CommitAsync` / `SaveWithClipAsync`) is private in `RecordPage.cs` (about lines 880-925) and `tests/Dino/DinoReader.Tests/PartTakeCommitTests.cs` copies it by hand. Item 1 first moves that step into Core (`src/Dino/DinoReader.Core/PartTakeCommit.cs`, a pure step the page calls), so the test runs the real code. The adopting read is `FileStoryLibrary.cs` (about line 579; delete and marker code about 434-475), hence the wider Touches. The fix keeps Slice 443's rule: a save never deletes a recording of words the new take does not cover.

**Why:** a parent would hear an old recording after re-recording. The old file stays on disk as an orphan and is adopted again when the new take is later removed, or played in its place.

**The fault (from the review, `RecordPage.cs:912`, important, audible and not data loss):** a stale older overlapping part file can resurface after a new take is saved. Adoption keeps the first overlapping file by name; if the fresh file sorts first it is adopted, `WithoutFile` drops it, and the older overlapping file is never in `ReplacedFiles`, so it stays on disk as an orphan and is adopted when the new take is later removed.

**Scenario:** an orphan `rec-en-1-p2-3.m4a` exists; the user records part `p1-2` and it saves as `rec-en-1-p1-2-abcd1234.m4a` (sorts first). The old `p2-3` file survives and plays later.

**Fix options the review names (pick one, decided at build):**
- Delete every part file that overlaps the new key at commit; or
- pass `Adopt` the committing file name to ignore.

## Work

- [ ] 1. Reproduce first: a test in `tests/Dino/DinoReader.Tests/` (the commit step moved into Core so the test runs the real code, review #5 of the same file) with an older overlapping orphan (`rec-en-1-p2-3.m4a`) on disk and a new take `rec-en-1-p1-2-<id>.m4a` saved; it fails on `main` (the old file is still adopted or left on disk).
- [ ] 2. Fix by one of the two options above in `RecordPage.cs` / `StoryRecordings.cs` / `PartClipAdoption.cs`; the item 1 test passes.
- [ ] 3. A test that the new take, removed later, does not bring the older overlapping file back.
- [ ] 4. Android, iOS and Windows the same (shared page and Core code).

**Verification:** on an emulator, the record flows (`.maestro/dino/record-only-the-words.yaml` and the other record flows): record a part, re-record an overlapping part, delete the new take, and read the page; the older recording does not play. Dino tests pass.

#### Not yet verified

- [phone] The record flows above on the S21 or emulator after the fix.
