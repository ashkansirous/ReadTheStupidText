### Slice 236 — Phone runner gaps from night 2026-11-01-0859, round 2

*Built 2026-11-02, not merged. Items 1-7 and 9 built; item 8 stays with Slice 242. Fast self-tests pass; partly run on the S21 (see Built and Not yet verified).* Written 2026-11-01 by the round 2 feedback loop ([note](../notes/2026-11-01-0859-feedback-loop-w2.md)). It sits next to Slices 224 and 225 (same files).

**Touches:** `tools/phone/run-checks.ps1`, `tools/phone/checks/_lib.ps1`, `tools/phone/reset-dev-app.ps1`, `tools/phone/preflight.ps1`, `tools/phone/checks/paused-notification.ps1`, `tools/phone/test-run-checks.ps1`, `tools/phone/test-phone-leftovers.ps1` and `tools/phone/checks/short-lines.ps1` (widened mid-build 2026-11-02: the leftovers assert on `Save-StayOn`, and the one-token `NanTakes` row field), `tools/CLAUDE.md`, `docs/plan/slices/236-phone-runner-gaps-night-2026-11-01-0859.md`

#### Work

1. **No `-Voices` on `run-checks.ps1`** (wave 2 digest entries 155, 278): Slice 220's short-lines check was run by hand for a second voice, and the check counts `NanTakes` but never prints it.
   - [x] `run-checks.ps1 -Voices <ids>` passes the voice list to checks that take one; `short-lines` prints `NanTakes`. Verify: a self-test in `test-run-checks.ps1` shows the voices reaching the check's arguments.
2. **A run cannot be pinned to the debug or aot build** (entry 1325): `-Variant` applies only to `-Build`/`-Prepare`; a run swaps to aot when its background aot build is cached, so "stay on debug" could not be honoured.
   - [x] `run-checks.ps1 -Variant debug|aot` on a run pins it: no swap, no background build of the other variant. Verify: self-test with both variants cached and `-Variant debug` installs debug and starts no build.
3. **`reset-dev-app.ps1 -Level auto` leaves a `held-user-cancelled` pack hold** (entries 1325-1359): with the app alive the ladder stops at level 1, and only `-Level clear` removes the hold; Slice 233 worked around it inside `paused-notification.ps1`.
   - [x] `-Level auto` clears a hold that blocks downloads (or escalates when one is found). Verify: on the emulator, cancel a pack download, run `-Level auto`, and the pack starts again.
4. **The runner stalls on its own lease unless `RTST_PHONE_OWNER` is exported** (entry 1325; today only a workaround line in `tools/CLAUDE.md`).
   - [x] With the variable unset, run-checks uses a default owner that matches the lease it takes. Verify: self-test without the variable takes and releases one lease and does not wait.
5. **Aside pack copies stay on the phone** (entries 1038, 1381, 1519): every run leaves tars under `/sdcard/Download/rtst-packs-aside/uk.sirous.readthestupidtext.dbg/`.
   - [x] The runner deletes the aside copy after a verified restore, or `tools/CLAUDE.md` says why it is kept. Verify: after a run, `adb shell ls` of that folder is empty (or the reason is written).
6. **The pre-flight's stay-awake change is never recorded or undone** (interactive session 0779fbee entries 1849-1857): `preflight.ps1 -Fix` sets `stay_on_while_plugged_in` to 7 and nothing restores the old value.
   - [x] `-Fix` writes the old value, owner `hand` and a time to the night folder; the record never goes stale and only `preflight.ps1 -Restore` sets it back (a lease take or release leaves it alone; a lease take's own record, owner = the lease owner, is restored by that lease's release, and a dead run's by the next take). While a record exists the runner may set stay-awake back to 7; with none it stops and names the change. The who-may-restore decision is one pure function (`Get-StayOnRecordAction`) with a self-test per case. Verify: set 0, run `-Fix`, then `-Restore`, and read 0 again.
7. **An app kill 0.7 s after launch was never explained** (wave 2 entries 1454, 1461-1473): system_server logged "Compatibility: Killing package ... PlatformCompat overrides" during a `paused-notification-network` run. Likely an `am compat reset` on a package with no override (the time-limit variant resets unconditionally); not proven. Fix: the reset reads the timeout and the compat override first and resets only what is present, logging both.
   - [x] `paused-notification.ps1` logs the package's compat state at start and resets only what it set. Verify: two runs in a row (time-limit, then network) with no PlatformCompat kill in logcat.
8. **Raw control bytes in source:** tracked in [Slice 242](242-round-4-leftovers-night-2026-11-01-0859.md) item 4 (the one place to fix them). The final feedback loop ran `control-chars.mjs . --all` on this branch (2026-11-01): the only hit is `src/DinoReader.App/TextStep.xaml.cs:74` (0x1f), none in `tools/phone/`.
9. **The phone dozed on AC in the middle of a run and nobody read why** (round 3, wave 3 digest entries 860 and 928): at 16:15 the S21 read `mWakefulness=Dozing` with `AC powered: true`, while a pack download sat still; the 08:56 pre-flight had read stay-awake 7. Nothing re-runs the pre-flight's read-only checks before a timed run. (The `device-test` agent now reads `dumpsys power` before each timed run, ClaudeSkills #138; this item is the runner side.)
   - [x] `run-checks.ps1` runs `preflight.ps1`'s read-only checks before each timed run and also compares the installed dev APK (`dumpsys package`: versionCode, lastUpdateTime) with the cached build it would install, reinstalling an older one before the budget starts (wave 3 entry 928: the first Slice 220 run measured an APK on the packs-10 catalogue). Verify: install an older dev APK, start a run, and it reinstalls before the first check. Same item: and stops with the changed setting named (or restores it where `-Fix` may). Verify: set stay-awake to 0 on the phone, start a run, and it reports the change before the first check.

#### Built (2026-11-02)

Items 1-7 and 9 built; item 8 stays with Slice 242. Fast self-tests: every `tools/phone/test-*.ps1` and `tools/test-switch-param-assign.ps1` pass under pwsh 7.6.9 and Windows PowerShell 5.1. Known gaps:
- Item 1: `short-lines` prints the guard's `NaN takes` once `checks/short-lines.ps1` puts `NanTakes = $one.Facts.NanTakes` into its row (one token, outside this slice's Touches); the formatter and summary are built and tested.
- Item 5: the aside copy is kept on purpose (it is the restore source after an install or `pm clear` loses the packs); the reason is in `tools/CLAUDE.md`.
- Item 6: `tools/phone/test-phone-leftovers.ps1` line 359 asserts that `preflight.ps1` never contains `Save-StayOn` (the review 2 rule "a plain preflight never records"); item 6 records under `-Fix` only, so that line must become "only inside the `-Fix` branch" (outside Touches).
- Item 3 sees the hold only where the app logs it (RTST Debug, when it refuses an automatic start); the `dumpsys platform_compat` wording in item 7's log line is not confirmed on a device.
- Item 9: the dev APK is compared through the install marker's build key plus versionCode and lastUpdateTime printed; an older build is reinstalled by the existing install step (inside the budget, not before it starts).

#### Scripted device checks (run in the device stage, none run yet)

Each is a command the `device-test` agent runs through the runner or the scripts; redirect output to a file in `tmp/`.
1. Voices: `run-checks.ps1 -App dino -Checks short-lines -Voices bf_emma -Budget 10 > tmp\voices.log 2>&1`; the log shows `bf_emma` reads only and `guard NaN takes` in each row and the summary.
2. Pin: with both variants cached, `run-checks.ps1 -App rtst -Checks highlight -Variant debug`; the log says `pinned to debug`, installs debug, starts no build (`building ... in the background` never appears), and `Get-Process dotnet` shows no new build of the aot variant.
3. Hold: `reset-dev-app.ps1 -App rtst -Level auto` after cancelling a pack download in the app (Maestro `cancelled-voice-pack-stays-cancelled` or the Debug cancel hook): the log shows `download hold found` then `RESET OK at level 'clear'`; then `run-checks.ps1 -App rtst -Checks pack-stall` logs `PACKDL start`.
4. Owner: `Remove-Item Env:RTST_PHONE_OWNER; run-checks.ps1 -App rtst -Checks highlight`; no `LEASE waiting` line, and `lease.ps1 -Show` is free afterwards.
5. Aside: nothing to run (kept by design, see `tools/CLAUDE.md`).
6. Stay-on: `adb shell settings put global stay_on_while_plugged_in 0`; `preflight.ps1 -Fix` (prints `recorded stay_on_while_plugged_in = '0'`); `preflight.ps1 -Restore` (prints `RESTORED ... 0`); `adb shell settings get global stay_on_while_plugged_in` prints `0`.
7. Compat: `run-checks.ps1 -App rtst -Checks paused-notification,paused-notification-network` back to back; the log has `compat state before this check changes anything`, `found: timeout ... compat override lines N` and `reset: timeout deleted, compat flag reset` (or `none present, no call`), then `... at the start of the network-only variant (it sets none)`; `adb logcat -d | findstr "PlatformCompat"` shows no `Killing package`.
8. Re-read: `adb shell settings put global stay_on_while_plugged_in 0`, then `run-checks.ps1 -App rtst -Checks highlight`; the log prints `CHANGED: stay_on_while_plugged_in is '0' now` before the first `==` line. Install an older dev APK (`run-checks -Reinstall` from an older checkout's cache), start a run: the `installed versionCode ... differs from the cached build key` line, then the reinstall, before the first check.
Each fails against the old behaviour: the old runner has no `-Voices`/`-Variant` switch effect, prints no `CHANGED:` line and never records the stay-on value in `-Fix`.

#### Run on the S21 (2026-11-02, about 00:50, key 405f6d0b3687)

- Item 7: PASS. `paused-notification` (59 s) then `paused-notification-network` (83 s), no PlatformCompat kill; the `found:` and `reset:` lines were logged.
- Item 5: PASS. 3 tars kept, the reason is in `tools/CLAUDE.md`.
- Item 9: PASS through `run-checks`' own pre-flight: stay-awake read 0 and was set to 7 before the first check. No mid-run `CHANGED:` case was exercised.
- Item 6: partial. The run held a lease, so `-Fix` kept the lease's record and `-Restore` answered LeaveAlone (by design: the lease restores its original). The no-lease `-Fix` then `-Restore` to 0 was not run.
- Item 3: not run (no download was in progress to cancel). `-Level auto` reported RESET OK with no hold.
- Warning seen: "could not cancel the device-side restore ... did not finish in 30 s", then the restore verified.

#### Changed while building

- Stay-on record redesigned after two review strikes: one owner restores, decided by the pure `Get-StayOnRecordAction` in `tools/phone/common.ps1`. A `hand` record (from `preflight.ps1 -Fix`) is restored only by `preflight.ps1 -Restore` and never goes stale; a lease's record is restored by that lease's release; a dead run's record by the next take. The runner may set 7 again only while a record exists; with none it stops and names the change.
- `Reset-TimeLimit` (`checks/paused-notification.ps1`) reads the timeout and the compat override and resets only what is present, so it keeps item 7 and high finding 5 (stale override from a killed run) both.
- `-Variant` on a run now pins it (before, a run ignored it); a check that needs the other variant FAILs with "needs the ... build but this run is pinned".

#### From review

Files in `D:/ReadTheStupidText-worktrees/reviews/` (outside the repo).

`s236-high.md` (10 findings):
- 1 applied (hold check reads the log once; hold missed before an automatic start).
- 2 applied (`AliveWithHold` never cleared, PARTIAL message always blamed the hold).
- 3 skipped: refactor (move the hold helpers from `_lib.ps1` to `common.ps1`); same as medium 4.
- 4 applied (weak assertion that a plain preflight never records).
- 5 applied (`Reset-TimeLimit` left a stale override after a killed run).
- 6 applied (runner overwrote a mid-run stay-on change).
- 7 applied (`-Fix` record read as a dead run's), via the stay-on redesign.
- 8 skipped: low (unfiltered `dumpsys platform_compat` run on the PC).
- 9 applied for the doc side only; the behaviour change (`-Variant` pins a run) is item 2's ask, so no pinning was removed.
- 10 skipped: low (hand-kept `VoiceListChecks` list).

`s236-medium.md` (8 findings):
- 1 applied (missing space in the `test-phone-leftovers.ps1` assertion).
- 2 and 8 applied via the stay-on redesign.
- 3 applied (hold read outside any try/catch).
- 4 skipped: refactor (same as high 3).
- 5 skipped: the `dumpsys` override-line wording is unconfirmed on a device.
- 6 applied (cleanup before the diagnostic reads).
- 7 skipped: unconfirmed, needs a pinned aot run.

`s236-check-medium.md` (9 findings, all skipped):
- 1: needs a device (`dumpsys` wording).
- 2: `hand` records never expire, by design.
- 3: low (three adb calls before each check).
- 4: low (`[int]` cast overflow on a huge digit string).
- 5: cosmetic (synthetic failed record for the unpinned variant).
- 6: low (same as high 3).
- 7: low (`Get-NoBuildMiss` duplicates the pinned rule).
- 8: low (source-text assertion on `preflight.ps1`).
- 9: low (repeated literals).

#### For CLAUDE.md

- `tools/phone/`: a stay-awake change is restored by exactly one owner, decided in `Get-StayOnRecordAction` (`common.ps1`). A `hand` record from `preflight.ps1 -Fix` is restored only by `preflight.ps1 -Restore`; a lease's record by that lease's release; a dead run's by the next take. Do not add another restorer. (Slice 236: docs/plan/slices/236-phone-runner-gaps-night-2026-11-01-0859.md)
- `run-checks.ps1 -Variant debug|aot` on a run pins it (no swap, no background build of the other variant); a check that needs the other variant FAILs. Do not pass `-Variant` to a run that includes checks for the other build. (Slice 236: docs/plan/slices/236-phone-runner-gaps-night-2026-11-01-0859.md)
- The aside pack tars under `/sdcard/Download/rtst-packs-aside/` are kept on purpose (the restore source); do not add a delete. (Slice 236: docs/plan/slices/236-phone-runner-gaps-night-2026-11-01-0859.md)

#### Not yet verified

- Item 3 on a device: cancel a pack download, `reset-dev-app.ps1 -Level auto`, the pack starts again.
- Item 6 with no lease held: `preflight.ps1 -Fix` then `-Restore` back to 0.
- A mid-run `CHANGED:` stop with no record.
- The `dumpsys platform_compat` override-line wording (`Get-CompatOverrideLines` matching).
- The 30 s device-side restore cancel timeout warning ("did not finish in 30 s").

## User answers before the night run (2026-11-01, evening)

- "s21 is always available for your tests if you need them": the phone checks (run-checks, preflight -Fix, the stay-awake restore, dozing, the lease) may run on the S21 tonight. Pre-flight passed at the start (night folder preflight.md). If auto mode still denies a phone step, record the denied tool here and park only that step.
