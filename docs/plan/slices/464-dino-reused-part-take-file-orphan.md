# Slice 464 — Dino: an older part-take file never plays after a new take is saved
**Product:** Dino

**Touches:** `src/Dino/DinoReader.App/RecordPage.cs`, `src/Dino/DinoReader.Core/StoryRecordings.cs`, `src/Dino/DinoReader.Core/PartClipAdoption.cs`, `src/Dino/DinoReader.Core/FileStoryLibrary.cs`, `src/Dino/DinoReader.Core/PartTakeCommit.cs`, `src/Dino/DinoReader.Core/IStoryLibrary.cs`, `tests/Dino/DinoReader.Tests/**`

*Built 2026-11-27 (wave 3 of night 2026-11-27-0154), PR #1402; not merged.*

*Planned 2026-11-27 (index pass of night wave 2) from `reviews/pr-1397-check3-medium.md` finding #1 (Slice 443's third check pass).*

**Claim note (coordinator, 2026-11-27):** the commit step (`CommitAsync` / `SaveWithClipAsync`) is private in `RecordPage.cs` (about lines 880-925) and `tests/Dino/DinoReader.Tests/PartTakeCommitTests.cs` copies it by hand. Item 1 first moves that step into Core (`src/Dino/DinoReader.Core/PartTakeCommit.cs`, a pure step the page calls), so the test runs the real code. The adopting read is `FileStoryLibrary.cs` (about line 579; delete and marker code about 434-475), hence the wider Touches. The fix treats an orphan that overlaps the new key exactly as Slice 443 already treats an adopted overlapping clip (replaced, with the card warning when it reaches past the take); a file that does not overlap the key is kept. (An earlier draft kept uncovered words; review changed that.)

**Why:** a parent would hear an old recording after re-recording. The old file stays on disk as an orphan and is adopted again when the new take is later removed, or played in its place.

**The fault (from the review, `RecordPage.cs:912`, important, audible and not data loss):** a stale older overlapping part file can resurface after a new take is saved. Adoption keeps the first overlapping file by name; if the fresh file sorts first it is adopted, `WithoutFile` drops it, and the older overlapping file is never in `ReplacedFiles`, so it stays on disk as an orphan and is adopted when the new take is later removed.

**Scenario:** an orphan `rec-en-1-p2-3.m4a` exists; the user records part `p1-2` and it saves as `rec-en-1-p1-2-abcd1234.m4a` (sorts first). The old `p2-3` file survives and plays later.

**Fix options the review names (pick one, decided at build):**
- Delete every part file that overlaps the new key at commit; or
- pass `Adopt` the committing file name to ignore.

## Work

- [x] 1. Reproduce first: a test in `tests/Dino/DinoReader.Tests/` (the commit step moved into Core so the test runs the real code, review #5 of the same file) with an older overlapping orphan (`rec-en-1-p2-3.m4a`) on disk and a new take `rec-en-1-p1-2-<id>.m4a` saved; it fails on `main` (the old file is still adopted or left on disk).
- [x] 2. Fix by one of the two options above in `RecordPage.cs` / `StoryRecordings.cs` / `PartClipAdoption.cs`; the item 1 test passes.
- [x] 3. A test that the new take, removed later, does not bring the older overlapping file back.
- [x] 4. Android, iOS and Windows the same (shared page and Core code).

**Verification:** on an emulator, the record flows (`.maestro/dino/record-only-the-words.yaml` and the other record flows): record a part, re-record an overlapping part, delete the new take, and read the page; the older recording does not play. Dino tests pass.

#### Not yet verified

- [phone] The Verification walk-through by hand on the S21 or emulator (record a part, re-record an overlapping part, delete the new take, and the older recording does not play). The emulator flows `record-only-the-words`, `record-per-character` and `record-per-language` pass but log `replacedFiles=0`, and a scripted flow cannot make a stale orphan, so the orphan path is covered by unit tests only.

#### Built (implement-slice, 2026-11-27)

- **Fix chosen: delete every overlapping part file at commit** (option 1), in Core `PartTakeCommit.SaveAsync`. At save, orphan part files that overlap the new key are put in as clips (`PartClipAdoption.WithOrphansAdopted`) and then go exactly as adopted overlapping clips do in Slice 443's path: replaced, with `ReplacesBeyond` computed from the commit-time set and logged as `replacesBeyond=`. A non-overlapping file, or one for another page or language, is kept. Why not option 2 (ignore the committing file in `Adopt`): it lives in `GetAsync` inside the library and would still leave a second orphan (p3-4 beside p2-3) on disk.
- The commit step moved from `RecordPage.SaveWithClipAsync` into Core `PartTakeCommit.SaveAsync`; `PartTakeCommitTests` now runs it (review #5). The Record log line gained `replacedFiles=<n>` and `replacesBeyond=`. Listing goes through the new `IStoryLibrary.PartFileNamesAsync` (under the library gate, honours `PartTakes.Enabled`, every failure reported as a fallback so the fresh take is never deleted). Touches widened to `FileStoryLibrary.cs`, `IStoryLibrary.cs` and the new `PartTakeCommit.cs`.
- Tests in `PartTakeCommitTests`: orphan `rec-en-1-p2-3.m4a` is replaced when `p1-2-abcd1234` saves; the new take removed later brings nothing back; a non-overlapping `p3-3` file is kept. Dino tests: 5076 pass. Windows Release build (`Build-TestPackage.ps1 -App dino`) OK.
- Emulator: `record-only-the-words`, `record-per-character` and `record-per-language` all PASS. The logs show `replacedFiles=0`: the flows make no stale orphan, so the orphan path is covered by unit tests only.

#### From review

- Fixed (`reviews/pr-1402-high.md` 1-4, 6): an orphan overlapping the key is put in as a clip (`PartClipAdoption.WithOrphansAdopted`) and then goes exactly as an adopted clip does under Slice 443 (any overlap is replaced, the card warns when it reaches past the take); `first`, the replaced files and the switch come from that one set; the listing is `IStoryLibrary.PartFileNamesAsync` (under the gate, honours the part-take switch, any exception reported as `FallbackArea.Library`/`StoryUnreadable` and nothing listed). The "never deletes words the take does not cover" wording is gone: only a file that does not overlap the key is kept.
- Skipped #5 (`reviews/pr-1402-high.md`): "The rollback `DeleteFileAsync` inside the catch can throw and replace the original save failure." - pre-existing: `git show origin/main:src/Dino/DinoReader.App/RecordPage.cs` (lines 917-924) has the same catch and `DeleteFileAsync`; this diff only moved it into Core.
- Fixed (`reviews/pr-1402-medium.md` 1, 2, 4): the delete comment now says an orphan can be adopted again and its name is logged; `PartTakeSaved.ReplacesBeyond` is the warning worked out from the commit-time set and is logged as `replacesBeyond=` in `[Record] saved` (the card itself still warns from the page's earlier story); the switch-off test commits and checks nothing is deleted.
- Skipped #3 (`reviews/pr-1402-medium.md`): "PartFileNamesAsync catches all exceptions and reports FallbackReason.StoryUnreadable; mislabels IO/cancel errors and silently skips orphan cleanup." - a FallbackReason is a closed list the Worker also holds, so a new one is outside this slice; StoryUnreadable names the library read that failed.
- Fixed (`reviews/pr-1402-medium.md` 5): the Claim note and Built section were rewritten for the replace-overlap rule and 5076 tests.
- Skipped #6 (`reviews/pr-1402-medium.md`): "Rollback DeleteFileAsync in the catch can throw and mask the original save failure; catch now covers the new listing/adoption calls too." - pre-existing: the rollback catch was moved unchanged from main's RecordPage.cs.
- Check pass (`reviews/pr-1402-check1-medium.md`), 7 findings read, none applied:
- Skipped #1 (`reviews/pr-1402-check1-medium.md`): "Orphan reaching past the take is deleted; ReplacesBeyond only logged in RecordPage" - the orphan it deletes was not playing, because adoption kept another overlapping file; an orphan the page had adopted is in the page's story, so the card warns. The late-orphan case is logged.
- Skipped #2 (`reviews/pr-1402-check1-medium.md`): "Story read and folder listing not atomic; a concurrent commit's just-placed file is deleted as an orphan." - saves on one page run one at a time, and no path for a second save has been shown; speculative.
- Skipped #3 (`reviews/pr-1402-check1-medium.md`): "Two orphans sharing a key: loser deleted but not in latest/first/ReplacesBeyond." - a nit after the check pass; follow-up.
- Skipped #4 (`reviews/pr-1402-check1-medium.md`): "PartFileNamesAsync default returns empty; fakes/wrappers silently disable cleanup." - a nit after the check pass; follow-up.
- Skipped #5 (`reviews/pr-1402-check1-medium.md`): "catch(Exception) swallows cancel, mislabels StoryUnreadable." - the earlier medium #3 skip stands, since FallbackReason is a closed list the Worker holds.
- Skipped #6 (`reviews/pr-1402-check1-medium.md`): "Overlapping orphans kept as clips (keepOverlappingParts) in latest; behaviour unpinned by a test." - a nit after the check pass; follow-up.
- Skipped #7 (`reviews/pr-1402-check1-medium.md`): "Rollback DeleteFileAsync can mask original save failure." - pre-existing, moved unchanged from main.
