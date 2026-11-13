# Slice 301 — Slice 247 follow-ups: emulator runner crash wait, crash log on retry, faster slow flows
**Product:** Shared

*Built 2026-11-07 (wave 3, night 2026-11-07-1412) for items 1 and 2, merged as PR #918 (70bb3a5a); item 3 not built (needs a re-plan). Device-checked on emulator-5554 for the happy path only, see Not yet verified.* *Planned 2026-11-07.* Three items left by [Slice 247](247-final-loop-leftovers-night-2026-11-01-0859.md), not built. Moved from plan.md's *Outstanding work*. Tooling only (the runner drives the Android emulator; iOS has no equivalent runner here).

**Touches:** `tools/emulator/run-flows.ps1`, `tools/emulator/common.ps1`, `.maestro/dino/**`, `tools/CLAUDE.md`, `docs/plan/slices/301-slice-247-follow-ups-emulator-runner.md`, `tools/emulator/test-*.ps1` (self-tests; widened at claim), `.maestro/rtst/**` (if the five flows include RTST ones)

## Work

- [x] 1. (built) The wait after the crash kill in `run-flows.ps1` has no time limit: if `taskkill /T /F` failed, the run would hang until the night watchdog. Add a limit (60 s), then report the run as `crash-kill-failed` and go on. Test with a fake kill that never ends.
- [x] 2. (built) A crash-retried flow drops the first run's crash log: keep it under a name with the attempt number and print both paths in the summary. Test: a flow that crashes once leaves two logs.
- [ ] 3. (open: not built, premise wrong, see Built; re-plan from a fresh `PHASES` run) Shorten the waits the `PHASES` lines name (Wait is the largest phase in 4 of 5 flows; `read-back-reread-background`'s single `repeat` action, 82 s, is the slowest step). Take the five flows' current times from one run, shorten the waits, and report the same five flows finishing faster, before and after numbers in the slice file. No flow may lose a check.

## Verification

- Self-tests for items 1 and 2; item 3 is open and moves to a re-plan (its before/after table is not part of this slice's build).

## Built

- **1.** `Wait-AfterCrashKill` (`common.ps1`): `Invoke-MaestroWatched` is the only launcher of a Maestro tree and every exit path (natural end or crash kill) goes through `Resolve-MaestroEnd`, which waits at most 60 s (`$script:CrashKillWaitSeconds`) and re-kills every 15 s. A tree still alive after that is `RunnerStuck` (its own state, not a crash): the row says `runner stuck: Maestro tree <pid> would not exit`; `Test-RunnerStuck` is checked before the crash retry, the guarded rerun and the next flow; the run ends with the remaining flows NOTRUN and the finally block kills the tree once more. An attempt log is written only from a flow log that attempt saved. Self-tested in `test-speed.ps1` with fake processes whose wait never ends and stubbed flow runs.
- **2.** `Save-AttemptLog` (`common.ps1`) writes `logcat-i<N>/<app>-<flow>.attempt1.log` and `.attempt2.log` (marker lines plus the crash buffer) around the retry in `Invoke-FlowCrashRetry`; the summary prints `crash-retried <flow>: logs <p1> and <p2>`. Self-tested in `test-speed.ps1`.
- **3. Not built: wrong premise.** Slice 278 part B deleted `first-run-pack-start`, `open-book-voice-id` and `read-back-reread-background` (see `.maestro/README.md`); only `first-run` and `dino-backup-restore-recordings` of the five exist, and no before-times can be taken without a device run. Needs a re-plan: pick the current slowest flows from a fresh `PHASES` run first.

*Changed while building:* item 1 was planned as a bounded wait after the crash kill and became a two-strikes design after check pass 1 (two failed fixes in the kill/retry area): `Test-RunnerStuck` runs before every further launch (crash retry, guarded rerun, dead-emulator rerun, next flow), a stuck tree ends the run with the rest NOTRUN, and the finally block kills it once more. Item 2 as planned. Item 3 not built (above).

*Device (2026-11-07, emulator-5554):* `run-flows -App dino` with `first-run` and `dino-backup-restore-recordings`, `-NoBuild`: PASS 2/2 in 125 s, no runner stuck, no 60 s waits. PHASES `first-run`: launch 7.5, wait 21.8, other 15.6 s. `dino-backup-restore-recordings`: launch 5.3, wait 17.1, other 9.5 s. Each normal flow logged `WATCH kill-wait done: the Maestro process ended after the kill`; on a natural end that wording may mislead (follow-up).

*From review:* files `D:/ReadTheStupidText-worktrees/reviews/s301-{high,medium,check1,check2}.md`.
- High: applied 1, 2, 3, 5, 6, 7.
- Skipped #4 (`reviews/s301-high.md`): "`Invoke-FlowBody` was already over the 3-30 line rule" (method length; refactor for its own sake in dev tooling, no failure path).
- Skipped #8 (`reviews/s301-high.md`): "Status header still says" (doc finding, handled in this record: status line, item 3 and Verification now say item 3 is open for a re-plan).
- Medium: applied 1, 2, 5, 6.
- Skipped #3 (`reviews/s301-medium.md`): "real wait is 65 s+" (low; the ~65 s worst case is known and written in `tools/CLAUDE.md`).
- Skipped #4 (`reviews/s301-medium.md`): "may hold no markers" (the reviewer marked it unconfirmed).
- Check 1: applied 1, 2, 3, 6 through the redesign.
- Skipped #4 (`reviews/s301-check1.md`): "wildcard-deletes `$AppName-$FlowName.attempt*.log` on attempt 1" (low; runs on one emulator hold a lease, one run per device, so two runs never share a flow's attempt logs at once).
- Skipped #5 (`reviews/s301-check1.md`): "a path with an apostrophe" (low, test spuriously fails; low, no repo or worktree path has an apostrophe).
- Check 2: #4 applied (it was already in the code).
- Skipped #1 (`reviews/s301-check2.md`): "Test-MaestroPreflight kills on timeout" (main's existing behaviour: the preflight has its own launcher; follow-up).
- Skipped #2 (`reviews/s301-check2.md`): "treats root process" (main's existing behaviour: only the root pid is tracked; follow-up).
- Skipped #3 (`reviews/s301-check2.md`): "no overall timeout" (`speed.ps1` is outside this slice's Touches; follow-up).
- Skipped #5 (`reviews/s301-check2.md`): "post-run steps" (the logcat and adb reads already run beside a live Maestro).
- Skipped #6 (`reviews/s301-check2.md`): "kills by raw PID" (low risk, follow-up: the finally block kills the stuck pid seconds after it was seen alive, so pid reuse in that window is unlikely).
- Skipped #7 (`reviews/s301-check2.md`): "comment says it reports" (the comment is in `speed.ps1`, outside Touches).
- Skipped #8 (`reviews/s301-check2.md`): "wildcard-deletes <app>-<flow>.attempt*.log in a shared StateDir" (low; one run per device under the lease, so two runs never share a flow's attempt logs at once).

#### Not yet verified

- A real crash-kill on a Mono SIGSEGV: the retry and the two attempt logs (only self-tests with stubs and fake processes, and a passing happy-path run).
- A real stuck Maestro tree (`RunnerStuck`, rest NOTRUN).

#### Follow-ups

- Item 3 re-plan from a fresh `PHASES` run (the three deleted flows are gone).
- Check 2 findings 1-3: the preflight's own Maestro launcher needs the same bound, a java child that outlives the root process, an overall timeout in `speed.ps1`'s `Wait-ProcessWithCrashWatch`.
- Reword the `WATCH kill-wait done` log line on a natural end.

#### For CLAUDE.md (lifted 2026-11-07)

- `tools/CLAUDE.md` already carries the rule in this slice's diff (only `Invoke-MaestroWatched` launches Maestro; every exit goes through `Resolve-MaestroEnd`; a live tree is `RunnerStuck`, checked by `Test-RunnerStuck` before any further launch) (Slice 301: docs/plan/slices/301-slice-247-follow-ups-emulator-runner.md).
