# Slice 443 — Dino: a parent's part take plays in place of only its words
**Product:** Dino
**Touches:** `src/Dino/DinoReader.App/**` (`ReadingSession`), `src/Dino/DinoReader.Core/**` (`RecordSplitBook.cs`, `StarterUpdate.cs`, `PartTakes.cs`, `PartClipAdoption`, `ClipMove`, `FileStoryLibrary.cs`, `StoryRecordings.cs`, `CLAUDE.md`), `tests/Dino/DinoReader.Tests/**`, `.maestro/dino/**`

*Built 2026-11-27 (wave 2), merged as PR #1397 (7fcb4828); not yet verified on the phone, iOS simulator or Windows VM (see *Not yet verified* below).* Premise check at claim (2026-11-27): `RecordSplitBook.cs`, `StarterUpdate.cs` and `PartTakes.cs` are in `DinoReader.Core`, not App (Touches corrected); the line numbers `StarterUpdate.cs:944` and `RecordSplitBook.cs:658` in item 4 are from the old draft branch `fix/395-record-parts`, so item 4 re-finds those spots on main.

*Planned 2026-11-25 (plan-cleanup of that day; not built).*

**Starts after:** Slice 395 (part keys and per-character cards), merged as PR #1233 (2026-11-27).

Second half of Slice 395, split out by the plan-cleanup check 2026-11-25. Read Slice 395's Decisions 1-6, its park note and the faults below first; the slice file wins.

## Work
- [x] 1. Reading screen (`ReadingSession`): a part-keyed clip stands in for its segments only; segments before and after it play the book's premade per-segment track (settled 2026-11-18: no speech engine; a segment with no premade track is silent as today). A sentence-keyed clip plays as today.
- [x] 2. Store each clip's sentence cover and part range in the clip record when saved; never re-derive covers from story text at load, merge or starter-move time; the overlap rule compares stored covers only.
- [x] 3. "Listen and change" plays a part's take alone.
- [x] 4. Fix the known faults from `D:/ReadTheStupidText-worktrees/reviews/pr-1233-starter-medium3.md` (copy them into this file first): `StarterUpdate.cs:944` a part-only move drops the sentence clip; `RecordSplitBook.cs:658` an animal-sound-only take is ignored; an unknown cover must not drop valid recordings; load-time overlap must not drop a newer take. A sentence clip wins where it and a part clip cover the same words; a moved part clip whose page splits differently keeps its cover cleared.
- [x] 5. Tests on Freddy's pages 2 and 3; a Maestro record flow with the Debug record hooks reading the `[RecordedClip] playing` lines.
- [x] 6. Turn `PartTakes.Enabled` on (`src/Dino/DinoReader.Core/PartTakes.cs`) together with playback; run `.maestro/dino/record-only-the-words.yaml`; handle Slice 395's follow-ups (its *From review* list).
- Fresh review gate, both passes reading every file.

## Faults to fix (copied 2026-11-27 from `reviews/pr-1233-starter-medium3.md`; file:line are the old draft branch's, re-found on this tree by the builder)

The review was incomplete (it did not read `RecordPartsTests.cs`, `RecordSplitTests.cs` or the slice file in full). Its 10 findings:

1. `StoryRecordings.cs:1342`, medium: `PlayingClips` lost the overlap drop while `StoryJson.CleanPieces` keeps overlapping sentence clips; the Record screen can show or replace a clip the reader never plays.
2. `RecordSplitWalk.cs:699`, medium (performance): `ClipOf` re-runs `PartListOf` (segmenter plus attribution over the whole story) on every call when a part clip exists.
3. `RecordSplitBook.cs:658`, high: `RecordPartTakes.Apply` drops a take whose segments are all animal-sound markers: the parent's "Moo!" take is never placed, no `[RecordedClip]` line.
4. `ReadingSession.cs:72`, medium: a take is planned from its first segment without checking where the read started; tapping sentence 3 of a part spanning 2-4 plays the take from its beginning and the highlight jumps back.
5. `StoryJson.cs:1069`, medium: `MigratedFile` for a part clip accepts any recording file name, including another clip's.
6. `RecordSplit.cs:394`, medium: `PartRuns` ends a run when a part in no sentence breaks the index sequence, and merges narration with quotes of unknown speaker (both Storyteller).
7. `RecordSplitBook.cs:594`, low-medium: `PagesThatLineUp` rejects a whole page for a language when one source part with letters has empty translated text.
8. `StarterUpdate.cs:944`, high (data loss): moves are cut by `sentencesFit` for sentence clips and `partsFit` for part clips, but page `to` is marked taken once either kind moves, so a part-only move drops the sentence clip with its file.
9. `.maestro/dino/record-only-the-words.yaml:4`, low: comments disagree on the page (2 vs 3); the read-back waits optionally and asserts only `ReadingSurface`, so the only real playback check is the `[RecordedClip] playing part take` line.
10. `StoryRecordings.cs:1309`, medium: `PlayingParts` keeps a part clip with no stored cover unless a sentence clip's cover, recomputed from today's text, overlaps it; contradicts "cover is never derived from text".

### Built (wave 2026-11-27, builder's read of each fault against this tree)

Items 1-6 built and `PartTakes.Enabled` is on; the follow-ups marked left below were not changed. Fast checks at the first build: Dino tests 4936 before, 4950 after, 0 failed; Shared 1736; Android, iOS and Windows heads compile; flow lint clean. After the review fixes and the device fix: Dino tests 4967 pass, Shared 1736 pass, Android build OK.

*Changed while building / on the device (2026-11-27):*
- The record-only-the-words flow (`.maestro/dino/record-only-the-words.yaml`) now uses Three Little Pigs. Freddy is not a bundled test book, so the Slice 395 flow could never pass.
- Device bug found and fixed: read-time `PartClipAdoption` adopted a take's just-placed file, and the save then deleted it. Fixed by `StoryRecordings.WithoutFile` in `RecordPage.SaveWithClipAsync`; test `tests/Dino/DinoReader.Tests/PartTakeCommitTests.cs`.
- The unplayable-take catch now reports `FallbackArea.Speech` / `RecordingUnplayable`.
- Emulator, PASS: `record-per-character`, `record-per-language`, `record-only-the-words`, `record-page-shows-words`, `dino-backup-restore-recordings` (evidence `reviews/s443-device/run2/`, `run3/`).

- 1 (`PlayingClips` overlap drop): not on this tree. `PlayingClips` is sentence clips only; part clips play through the new `StoryRecordings.PlayingParts`, and load keeps every clip.
- 2 (`ClipOf` re-cuts parts): not on this tree. `RecordPage` cuts the part list once per story; the reading session caches it per pages and cast (`PartListCache`) and cuts it only when a playable part take exists.
- 3 (animal-sound-only take ignored): `PartTakes.Plan` covers segments by source range, so a take whose segments are all animal markers still replaces them (test `A_take_of_only_an_animal_sound_part_still_plays`).
- 4 (read starting inside a take): the whole take plays from its start and the highlight stays on the first segment in the read, as a sentence clip does; a take with no segment in the read plays nothing (test). Known behaviour, left in place: tapping sentence 3 of a part spanning 2-4 plays the take from its beginning.
- 5 (`MigratedFile` accepts another clip's file): not on this tree; part clips keep only a name `FileToKeep` matches to their own key.
- 6 and 7 (PartRuns with unknown speaker, `PagesThatLineUp`): not changed, not in item 4 and Slice 395's Decisions 3 and 6 cover them; left for the review gate to judge.
- 8 (part-only move drops the sentence clip): not on this tree. `PieceMoves` moves sentence and part clips with their own taken pages and count checks, so one kind never blocks the other (test). What remains is the older skipped follow-up of Slice 395: a clip whose page has a different count is not moved (a sentence clip on a page that splits differently is dropped); it needs its own slice.
- 9 (flow): the flow now reads the book back from page 2 with `open-book page=2 play=1` and expects `[RecordedClip] playing part take` with `parts=1-1`, `0-0`, `2-2`; comments say page 2 holds line 1 and page 3 lines 2 and 3.
- 10 (cover re-derived from text): the cover is stored with the clip (`PageRecording.Cover`, from the card when saved, written in `PartRecordings` as `CoverFirst`/`CoverLast`); the overlap rule reads stored covers only. A take with no cover (adopted file, damaged number) overlaps nothing and plays, so an unknown cover never drops a recording. A moved part clip whose page has the same parts but other sentences keeps playing with its cover cleared (`ClipMove.DropCover`).
- Unknown cover plus a sentence clip over the same words: superseded by the review fix (Decisions below): the reader hides a take with no stored cover where a playing sentence clip covers any sentence its parts sit in; nothing is deleted.
- Slice 395 follow-ups: adoption now works out the named files once per read (done); a part-take delete failure, `PagesThatLineUp` on keepSource pages, `WithClip(keepOverlappingParts)` orphaning an exact-key part file (latent: callers filter the same key) and the older-app empty `UseRecordings` toggle are left, none changed by this slice.

## Decisions

- **A read that starts inside a take (coordinator, review pass 1 finding 4, 2026-11-27):** the take plays from its start and the highlight moves to the take's first segment, so audio and highlight agree (this replaces the Built note of fault 4, "the highlight stays on the first segment in the read"). Next and Back treat the take's sentences as one unit, as they do a recorded page: Next goes to the sentence after the take's range, so the take is never restarted by Next; a read that plays on after the take continues at the segment after the take's range.
- **A sentence clip wins over a take, also with no stored cover (finding 5):** a take with no stored cover does not play where a playing sentence clip on the same page covers any sentence its parts sit in; nothing is deleted. Saving a take over words a sentence clip covers replaces that clip and its file (finding 2), so the new recording is the one that plays.
- A take plays only while the screen shows the reading language's words and it did not fail this session (finding 3, 8); the voice count leaves out the words a take stands in for (finding 8).

- **Saving a take never deletes words it does not cover (coordinator design, review pass 2, 2026-11-27; second strike in the recording-files area, so one design, not patches; `reviews/pr-1397-medium.md` #3, #5):** when a part take is saved, a sentence clip whose stored cover lies wholly inside the take's segment range is replaced (record entry and file removed). A sentence clip that also covers words outside the take is kept, record and file. By the rule above it still wins on those words, so the Record card for that part says so; it uses the card's existing warning, and that warning lists exactly the files the save will remove. No other file is removed. The part's Record card shows a new note, "Your recording of the whole sentence plays here. Remove it to hear this part." (`Record_WiderClipPlays`), whenever a kept wider sentence clip covers its words, before and after saving such a take (coordinator, 2026-11-27). This undoes the fix for high #2 only for wider clips: there the new take is saved and kept, never lost, and it plays once the wider sentence clip is removed.
- **Failed deletes (coordinator design, `reviews/pr-1397-medium.md` #1, #2, #4):** a `<file>.deleted` marker is written only for a part-take file, and only under the library's `_gate`. Writing, restoring or importing a file with that name removes its marker first, under the same gate. A listing removes a marker whose file is already gone. Backup, restore and package code never copies a marker.

#### From review

Files read to the last finding: `reviews/pr-1397-high.md` (9/9), `reviews/pr-1397-medium.md` (5/5), `reviews/pr-1397-check.md` (5/5), `reviews/pr-1397-check2-medium.md` (2/2), `reviews/pr-1397-check3-medium.md` (7/7). Every finding of the high and medium passes was fixed (their outcome lines say Fixed); the notes below name the design choices. Skips and follow-ups from the three check passes follow.

- High findings 1-5 and 9 (`reviews/pr-1397-high.md`): fixed (`Refresh` applies `DropCover`; a take saved over a card a sentence clip covers replaces it; a take plays only while the screen shows the reading language; a read starting inside a take plays it from its start with the highlight on its first segment; a cover-less take yields to a playing sentence clip; the long methods are split). Findings 1-5 of `reviews/pr-1397-medium.md`: fixed (the marker, gate and wider-clip designs are in Decisions).
- Check pass 1 (`reviews/pr-1397-check.md`): #1 (whole-sentence clip deleted by a take over part of it), #2 (stale `_takes` after an await) and #5 (`ListPartFiles` leftover check outside the gate) fixed. #1 became the "saving a take never deletes words it does not cover" Decision.
- Skipped #3 (`reviews/pr-1397-check.md`): "ignores story.UseRecordings; with the switch off nothing plays" - left as it is: the note can show while the switch is off, a display nit; no recording is lost or deleted by it.
- Skipped #4 (`reviews/pr-1397-check.md`): "keyed only on Pages and Cast references" - left as it is: Pages and Cast references are replaced on every edit, so a stale key needs a case the review did not give; no failing input was named.
- Check pass 2 (`reviews/pr-1397-check2-medium.md`): both lows are follow-ups, not built:
  - "ClearMarker(target) runs before the CreateNew open; if a leftover file exists, CreateNew throws after the marker is gone and the deleted take is adopted again on next load" (`FileStoryLibrary.cs:672`, low).
  - "Between UpdateStory clearing _takes and the next read, Next/Back can land inside a take's range and replay it" (`ReadingSession.cs:248`, low).
- Check pass 3 (`reviews/pr-1397-check3-medium.md`, after the device fix): #1 to #7 are follow-ups, not built:
  - #1 (important, audible not data loss, `RecordPage.cs:912`): "A stale older overlapping part file can resurface after a new take is saved" (an older overlapping file stays on disk as an orphan and is adopted when the new take is later removed).
  - #2 (nit, `FileStoryLibrary.cs:672`): "ClearMarker runs before CreateNew / File.Move; if those throw, the .deleted marker is gone and a locked leftover is adopted again".
  - #3 (pre-existing, low, `PartClipAdoption.cs:33`): "Adopted clips have Cover = null and length 0 (memory only); a stale writer overwriting story.json re-adopts the take without its cover".
  - #4 (nit, `StoryRecordings.cs:167`): "WithoutFile removes any clip with the given file name; safe only because the name is always fresh. Document/assert."
  - #5 (nit, `PartTakeCommitTests.cs:29`): "CommitAsync is a hand copy of SaveWithClipAsync, so removing WithoutFile from the page leaves the test green".
  - #6 (nit, `PartTakeCommitTests.cs:24`): "Depends on content/dino/freddy-and-the-slipper/story.md shape and speaker; use an inline fixture."
  - #7 (nit, `FileStoryLibrary.cs:597`): "Every book read now takes the library-wide _gate and scans for markers, so shelf listing can serialise behind a commit."
  - #1 goes to its own follow-up slice (Slice 464, written in the wave's index pass), because a parent would hear an old take after re-recording.
- Check pass 4 (`reviews/pr-1397-check4-medium.md`, after the fallback report): no crash, data-loss or privacy fault. Its 5 lows are follow-ups, not built: #1, the catch skips UpdateAudible; #2, a direct ReportFallback where the other paths use NoteTakeUnplayable; #3, TryPlayTake does not clear `_pauseState`; #4, the wait after the GalleryLock tap proves nothing; #5, the 30 s optional wait always runs.

- Finding 6 (`reviews/pr-1397-high.md`): a recording whose delete fails now leaves an empty `<file>.deleted` marker; adoption skips marked files and each listing retries the delete. The listing itself is not cached (a folder listing of one book's files; no per-pass cache built).
- Finding 7 (`reviews/pr-1397-high.md`): no saved-at stamp is stored; the reader and the Record screen already pick the earlier take by the same order, and the reader now logs the hidden take (`[RecordedClip] part take ... hidden by the earlier take ...`).
- Finding 8 (`reviews/pr-1397-high.md`): a take is not counted as the first clip of the read's voice (it is not the book's voice); the count is taken on the words the plan plays from the book's own audio, at the first book clip.

#### For CLAUDE.md (lifted 2026-11-27)

- `tools/CLAUDE.md`: an `# expect-log:` marker in a Maestro flow must end at a word boundary. `Test-LogMarker` (`tools/emulator/common.ps1:436`) refuses a match followed by a word character, so a prefix such as `rec-en-2-p1-1-` never matches; `lint-flows` does not catch it yet. (Slice 443: docs/plan/slices/443-dino-parent-part-take-plays-its-words-only.md)
- `tools/CLAUDE.md` (flows): a record flow runs on a bundled test book (Three Little Pigs); Freddy is not bundled, so a flow naming it can never pass. (Slice 443: docs/plan/slices/443-dino-parent-part-take-plays-its-words-only.md)

Follow-up (not built, needs a slice): make `lint-flows` flag an `# expect-log:` marker that ends mid-word.

#### Not yet verified

- [phone] Hear "said Mummy" in the book's voice between two part takes on the S21 (the parent's ear).
- [cloud] iOS simulator: record a part take and read it back.
- [session] Windows VM: record a part take and read it back (the VM was not started, for memory).
