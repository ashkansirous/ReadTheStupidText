# Slice 361 — RTST `sentence-highlight` Maestro flow fails on the emulator

*Planned 2026-11-13 (found by the 2026-11-13 emulator run).*

**Status:** planned.

**Touches:** `.maestro/rtst/sentence-highlight.yaml` and the RTST read path (`ReaderPage`, the `RTST-TEST: SENTENCE` marker).

## Evidence

- Device and build: emulator-5554, origin/main ef8c1838, 2026-11-13.
- Error: `Missing log marker(s) under tag RTST-TEST: SENTENCE 0 1`. The slowest step was `inputText`, 43.4 s.
- Logs: `D:/ReadTheStupidText-worktrees/reviews/dvt-sentence-highlight-fail.log` and `D:/ReadTheStupidText-worktrees/.emulator/logcat-i1/rtst-sentence-highlight.log`.
- Earlier: the same flow passed on the S21 (`RTST-TEST: SENTENCE 2 1`, twice; Slice 319, PR #957). See [Slice 319](319-device-run-2026-11-08-fixes.md) and [Slice 320](320-device-check-tooling-from-2026-11-08-run.md).

## Expected

The `SENTENCE 0` and `SENTENCE 1` markers appear under `RTST-TEST`, as on the S21.

## Likely cause

Unknown. Either the highlight never reached sentence 0/1 on the emulator (app bug), or text entry was too slow (`inputText` took 43.4 s; flow bug).

## Work items

- [ ] 1. Read both logs (the logcat especially) and tell an app bug from a flow bug: did the reader start, and did any `SENTENCE` marker print?
- [ ] 2. Fix the cause (app or flow), then re-run `sentence-highlight` on emulator-5554 until the markers appear.
- [ ] 3. Remove the line from `docs/plan/unverified.md` when it passes.
