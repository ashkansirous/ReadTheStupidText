# Slice 301 — Slice 247 follow-ups: emulator runner crash wait, crash log on retry, faster slow flows

*In progress (wave 2026-11-07), draft PR.* *Planned 2026-11-07.* Three items left by [Slice 247](247-final-loop-leftovers-night-2026-11-01-0859.md), not built. Moved from plan.md's *Outstanding work*. Tooling only (the runner drives the Android emulator; iOS has no equivalent runner here).

**Touches:** `tools/emulator/run-flows.ps1`, `tools/emulator/common.ps1`, `.maestro/dino/**`, `tools/CLAUDE.md`, `docs/plan/slices/301-slice-247-follow-ups-emulator-runner.md`, `tools/emulator/test-*.ps1` (self-tests; widened at claim), `.maestro/rtst/**` (if the five flows include RTST ones)

## Work

- [ ] 1. The wait after the crash kill in `run-flows.ps1` has no time limit: if `taskkill /T /F` failed, the run would hang until the night watchdog. Add a limit (60 s), then report the run as `crash-kill-failed` and go on. Test with a fake kill that never ends.
- [ ] 2. A crash-retried flow drops the first run's crash log: keep it under a name with the attempt number and print both paths in the summary. Test: a flow that crashes once leaves two logs.
- [ ] 3. Shorten the waits the `PHASES` lines name (Wait is the largest phase in 4 of 5 flows; `read-back-reread-background`'s single `repeat` action, 82 s, is the slowest step). Take the five flows' current times from one run, shorten the waits, and report the same five flows finishing faster, before and after numbers in the slice file. No flow may lose a check.

## Verification

- Self-tests for items 1 and 2; item 3 with a before/after table of the five flows' times (same emulator, same build), all five still passing.
