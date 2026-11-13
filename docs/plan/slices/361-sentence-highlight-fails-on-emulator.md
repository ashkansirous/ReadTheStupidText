# Slice 361 — RTST `sentence-highlight` Maestro flow fails on the emulator

*Planned 2026-11-13 (found by the 2026-11-13 emulator run).*

**Status:** built 2026-11-13 and verified on emulator-5554 (merged as PR #1051, 3fc78850).

**Touches:** `.maestro/rtst/sentence-highlight.yaml`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs` (the `RTST-TEST: SENTENCE_MOVED` marker), `docs/plan/slices/361-sentence-highlight-fails-on-emulator.md`.

## Evidence

- Device and build: emulator-5554, origin/main ef8c1838, 2026-11-13.
- Error: `Missing log marker(s) under tag RTST-TEST: SENTENCE 0 1`. The slowest step was `inputText`, 43.4 s.
- Logs: `D:/ReadTheStupidText-worktrees/reviews/dvt-sentence-highlight-fail.log` and `D:/ReadTheStupidText-worktrees/.emulator/logcat-i1/rtst-sentence-highlight.log`.
- Earlier: the same flow passed on the S21 (`RTST-TEST: SENTENCE 2 1`, twice; Slice 319, PR #957). See [Slice 319](319-device-run-2026-11-08-fixes.md) and [Slice 320](320-device-check-tooling-from-2026-11-08-run.md).

## Expected

The chunk-agnostic marker `SENTENCE_MOVED` appears under `RTST-TEST` before `READ_END`, as the `SENTENCE 2 1` marker did on the S21.

## Cause

A flow bug, confirmed by the emulator re-run on 2026-11-13 (item 3): head-start chunks hold one sentence each, so `SENTENCE 0 1` never appears and the highlight first moves inside chunk 2. The flow expected `SENTENCE 0 1`, but the head-start chunks make chunk 2 the first multi-sentence chunk. The flow now expects `SENTENCE_MOVED`, a Debug marker written once per read when the highlight first moves past a chunk's first sentence, so it no longer depends on the chunk index.

## Work items

- [x] 1. Cause: flow bug, not app bug. The logcat shows READ_START, CHUNK_AUDIO 0 1598ms (chunk 0 = "The sun came up."), chunk 1 one sentence, then `SENTENCE 2 0` and `SENTENCE 2 1` before READ_END. The reader starts with short head-start chunks, so chunk 0 never holds two sentences; the flow wrongly expected `SENTENCE 0 1`. inputText 43.4 s was slow but the read still started.
- [x] 2. Flow now expects `SENTENCE_MOVED` (done); the Debug marker is written in `ReaderPage.ShowSentence`.
- [x] 3. Re-run `sentence-highlight` on emulator-5554 (2026-11-13 12:15Z, cached build b0748749aa54): PASS. Log showed `SENTENCE 2 0`, `2 1`, `SENTENCE_MOVED`, `2 2`; the supertonic-3 pack voice spoke; 0 fallback lines. Logs: `D:/ReadTheStupidText-worktrees/reviews/w52-device/rtst-sentence-highlight.log` and `run.log`.
- [x] 4. The sentence-highlight entry in `docs/plan/unverified.md` (Slice 361 line) is closed by this pass; the index pass removes it.

## From review

- Skipped #2 (`reviews/s361-high.md`): "Not ruled out; say whether the pack was ready in the failing run" - the device re-run shows whether speech came from the pack voice or the fallback.
- Skipped #3 (`reviews/s361-high.md`): "Index readers get the opposite of the slice file" - those lines change only after the device pass, in the index pass.
- Skipped #6 (`reviews/s361-high.md`): "Fixtures only test the n>=1 parser so stay green" - the phone check's regex accepts any chunk, and that fixture is outside this slice's Touches.
- Skipped #2 (`reviews/s361-medium.md`): "the new SENTENCE_MOVED marker is undocumented there, so two pass markers can drift" - the phone check's regex already passes on any moved sentence, and phone tooling is outside this slice's Touches.
- Skipped #3 (`reviews/s361-medium.md`): "A second #if DEBUG field/method plus a second log line per move; could reuse the SENTENCE marker with a regex expect in the flow instead" - `tools/emulator/run-flows.ps1:113` expect-log is a literal match with no regex, so the SENTENCE marker cannot be matched chunk-agnostically.
- Applied #1, #4, #5 of `reviews/s361-high.md` and #1, #4 of `reviews/s361-medium.md`.
- Skipped #1 (`reviews/s361-check.md`): "a late Idle from the previous read could clear it mid-read and SENTENCE_MOVED would log twice" - harmless to the flow, which needs the marker only once.
- Skipped #2 (`reviews/s361-check.md`): "SENTENCE_MOVED passes on any chunk, so a regression narrowing only the first multi-sentence chunk is not caught" - the trade-off is deliberate, and the chunk-0 two-sentence case cannot occur with head-start chunks.

#### For CLAUDE.md (lifted 2026-11-13)

- `tools/CLAUDE.md`: an expect-log marker is a literal match (`tools/emulator/run-flows.ps1:113`); never expect a chunk-indexed `SENTENCE <c> <i>` marker in a Maestro flow, use the chunk-agnostic `SENTENCE_MOVED`. (Slice 361: docs/plan/slices/361-sentence-highlight-fails-on-emulator.md)

#### Not yet verified

None: the emulator re-run passed. The `unverified.md` sentence-highlight entry for this slice is closed by it.
