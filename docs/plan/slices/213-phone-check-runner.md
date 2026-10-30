### Slice 213 — One phone check runner with a fixed time budget

*Planned 2026-10-30.* Follow-up to Slice 206. Tooling only.

**Touches:** `tools/phone/run-checks.ps1` (new), `tools/phone/checks/*.ps1` (new), `tools/phone/common.ps1`, `tools/phone/audio-timing.ps1` (only to reuse its build-cache lookup), `tools/phone/README.md`, `tools/CLAUDE.md`, `docs/plan/slices/213-phone-check-runner.md`

#### Why

The 2026-10-30 leftovers run took over 20 minutes and finished one check of four. The brief was free text, so it asked for "10 minutes per item", an AOT build nobody had cached (about 7 min), and a non-AOT comparison that reinstalled the app and re-downloaded a voice pack. Repeat timing runs read a warm cache (240-578 ms) and were not cold starts. The notification check was blocked because the dev app's notifications were off. None of that was the agent's choice; the brief allowed it.

#### Decisions (2026-10-30)

- `run-checks.ps1 -App rtst|dino -Checks <names> [-Budget 10]`: takes the lease, runs preflight (charging and lock only fail it), then each named check as a fixed script, and stops at the budget, naming every check it skipped.
- **Builds:** it never builds. A check whose APK is not in the build cache for the current key is skipped as `needs build (<app> <variant>, ~7 min): run build first`; `run-checks.ps1 -Build` builds the needed variants and exits, for use before a run. One variant (AOT or not) per run, never swapped mid-run.
- **Setup it does itself** on the dev install only: grant `POST_NOTIFICATIONS` (`pm grant`), skip first run, make sure the needed pack is present (restore from the aside copy if moved).
- **Checks (each its own script under `checks/`):** `cold-start` (fresh process plus cleared sentence cache each run, N runs, prints first audio, engine and first synthesis), `highlight` (Slice 190 `SENTENCE` marker moves while chunk 1 plays), `notification-text` (start a throttled download, read `dumpsys notification --noredact`), `book-end` (Dino play to the end, time to doze), `paused-notification` (Slice 208, once built). New checks are added as files, not briefs.
- Device briefs then only name checks: "run-checks -App rtst -Checks cold-start,highlight".

#### Work

- [ ] The runner (lease, preflight, budget, skip reasons, one summary table at the end).
- [ ] `-Build` mode using the existing build cache.
- [ ] Setup steps above.
- [ ] The four checks above.
- [ ] No-device self-tests for the budget and skip logic.
- [ ] README and `tools/CLAUDE.md`: device briefs name checks from the runner; free-text phone briefs only for something no check covers.

#### Verification

- Fast: the self-tests in `pwsh` and Windows PowerShell 5.1.
- S21: `run-checks -App rtst -Checks cold-start,highlight,notification-text` finishes inside 10 minutes with a cached AOT build; `-App dino -Checks book-end`.
