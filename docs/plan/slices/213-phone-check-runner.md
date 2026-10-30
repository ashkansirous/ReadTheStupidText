### Slice 213 — One phone check runner with a fixed time budget

*Built 2026-10-30; fast self-tests and S21 runs passed, see Not yet verified.* Follow-up to Slice 206. Tooling only.

**Touches:** `tools/phone/run-checks.ps1` (new), `tools/phone/checks/*.ps1` (new), `tools/phone/common.ps1`, `tools/phone/audio-timing.ps1` (only to reuse its build-cache lookup), `tools/phone/README.md`, `tools/CLAUDE.md`, `docs/plan/slices/213-phone-check-runner.md`

#### Why

The 2026-10-30 leftovers run took over 20 minutes and finished one check of four. The brief was free text, so it asked for "10 minutes per item", an AOT build nobody had cached (about 7 min), and a non-AOT comparison that reinstalled the app and re-downloaded a voice pack. Repeat timing runs read a warm cache (240-578 ms) and were not cold starts. The notification check was blocked because the dev app's notifications were off. None of that was the agent's choice; the brief allowed it.

#### Decisions (2026-10-30)

- `run-checks.ps1 -App rtst|dino -Checks <names> [-Budget 10]`: takes the lease, runs preflight (charging and lock only fail it), then each named check as a fixed script, and stops at the budget, naming every check it skipped.
- **Builds (2026-10-30, the user's design, revised after the build tests): at the start the runner starts BOTH builds in the background, in parallel: plain Debug and AOT, each skipped when cached for the current key. Each is its own `dotnet build --artifacts-path <worktree>\.artifacts\<app>-jit|aot` (restore included, `-nodeReuse:false`, no shared compilation), so nothing under `src\*\obj|bin` is touched (13,531 files compared); the lock is per checkout and variant. (The first design ran them one after the other because they shared obj folders; separate artifacts paths removed that need.) Each writes `<worktrees>\.phone\build-<app>-<variant>.json` (state, start, heartbeat every 30 s, last log line, log path); a log not growing for 5 minutes is a stall and fails the build. Every catalogue entry declares its variant: `cold-start` (and any timing) needs `aot` and waits for it, printing what it waits for every 30 s; the others start on whichever is ready, AOT preferred. At most one swap on the phone, Debug to AOT, never back (`adb install -r` keeps data; after every install `Test-PackInstalled` and a restore from the aside copy). A failed or stalled build FAILs the checks that need it with its last 20 log lines; nothing is skipped for a missing build and nothing falls back silently. The 10-minute budget runs from the runner start; time spent only waiting for a build is added to its end (lease and quiet-machine waits capped at 3 minutes each and count). `<worktrees>\.phone\run-status.json` (builds, checks, installed variant, updatedUtc) is rewritten every 30 s. Measured: Debug 2:49 and AOT 4:58 in parallel, about 5 minutes in all. Every table row and timing carries its variant. `-Variant` (default `debug`) only chooses what `-Build`/`-Prepare` act on.**
- **Setup it does itself** on the dev install only: grant `POST_NOTIFICATIONS` (`pm grant`), skip first run, make sure the needed pack is present (restore from the aside copy if moved, verified). The timed run never downloads.
- **Checks (each its own script under `checks/`):** `cold-start` (fresh process plus cleared sentence cache each run, N runs, prints first audio, engine and first synthesis), `highlight` (Slice 190 `SENTENCE` marker moves while chunk 1 plays), `notification-text` (start a throttled download, read `dumpsys notification --noredact`), `book-end` (Dino play to the end, time to doze), `paused-notification` (Slice 208, once built). New checks are added as files, not briefs.
- **Voice packs (redesign after two S21 failures, 2026-10-30):** an installed pack is `files/packs/<id>/active` plus `files/packs/<id>/v<n>/pack.json` (`PackVersionFolders`); a download is `files/packs/<id>-<version>.zip.partial`, never a pack. The first runner looked for pack.json one level up, never saw the installed pack, watched a stale `.partial`, and a reset saved the `.partial` as the aside copy. Now: one pure `Test-PackInstalled` (common.ps1, self-tested with installed / partial only / active without pack.json / two versions listings); `run-checks.ps1 -Prepare` downloads outside the budget (stale `.partial` deleted, `PACKDL start` within 60 s or FAIL quoting the last 10 `RTST-TEST` lines, stall 2 min, verified aside copy of installed folders only, newest 3); the timed run restores from an aside copy or SKIPs `no pack: run -Prepare first`; `reset-dev-app.ps1 -Level clear` keeps only installed pack folders aside and says "packs restored" only after the install check passes. All elapsed time counts from the runner start (the wait budget varied 128-463 s only because the in-budget download was sized from what was left).
- Device briefs then only name checks: "run-checks -App rtst -Checks cold-start,highlight".

#### Work

- [x] The runner (lease, preflight, budget, skip reasons, one summary table at the end).
- [x] `-Build` mode using the existing build cache.
- [x] Setup steps above.
- [x] The four checks above.
- [x] No-device self-tests for the budget and skip logic.
- [x] README and `tools/CLAUDE.md`: device briefs name checks from the runner; free-text phone briefs only for something no check covers.

#### Verification

- Fast: the self-tests in `pwsh` and Windows PowerShell 5.1.
- S21: `run-checks -App rtst -Checks cold-start,highlight,notification-text` finishes inside 10 minutes with a cached AOT build; `-App dino -Checks book-end`.

#### Changed while building

- Builds moved from "never builds" to two parallel background builds with separate `--artifacts-path` folders; the budget counts from the runner start.
- Pack handling redesigned after two failed S21 rounds (wrong layout, stale `.partial` saved as a pack): `Test-PackInstalled`, `-Prepare`, aside copies of installed folders only.
- `notification-text` moves the current pack aside, relaunches, requires `PACKDL start`, reads the dev app's newest progress notification, restores and verifies; `ExpectPackNameInNotification` flag for Slice 208. `book-end` counts only a live wake-lock block (by uid or WorkSource).

- AOT trade-off (2026-10-30): an AOT build costs about 5-7 minutes and saves about 1.8 s per cold start (S21 fresh install: 8.8 s Debug JIT, 7.0 s AOT), so it never pays off for speed; it is built only so timing checks measure what store users get. Plain Debug serves every other check.

#### From review

- High pass 20 findings, medium pass 5, all fixed. S21 rounds also found: missing-pack give-up, appId vs userId, wake-lock history counted as live, pack listing exit code, status variant, notification trigger picking a pack the app does not want.

#### S21 results (2026-10-30)

- RTST `highlight,notification-text,cold-start` in 85 s; Dino `book-end` 112 s. Notification title `Downloading natural voices... 3%` (no language or size: Slice 208).
- Cold start AOT 3.1-4.2 s after force-stop (first synthesis about 2.9-4.0 s), 7.0 s after a fresh install.
- Slice 190 highlight: PASS on a phone. Slice 126 book end: PASS (released 0 s after `READ_END`).

#### For CLAUDE.md (lifted 2026-10-30)

- Phone briefs name checks from the runner (`tools/phone/run-checks.ps1 -App <app> -Checks <names>`); builds start in parallel on their own `--artifacts-path`; timings carry `aot`/`debug`; packs only via `-Prepare`. Already in `tools/CLAUDE.md`. (Slice 213: docs/plan/slices/213-phone-check-runner.md)

#### Not yet verified

- A cold-cache run where the AOT build fails or stalls (the FAIL path).
- A run while another session holds the lease.
- The Debug-to-AOT swap mid-run.
- iOS (no Mac).
