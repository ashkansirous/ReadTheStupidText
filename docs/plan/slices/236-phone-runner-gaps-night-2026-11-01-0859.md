### Slice 236 — Phone runner gaps from night 2026-11-01-0859, round 2

*Parked: feedback loop, waits for the user.* Written 2026-11-01 by the round 2 feedback loop ([note](../notes/2026-11-01-0859-feedback-loop-w2.md)). Nothing here is built until the user moves it to `planned`. It sits next to Slices 224 and 225 (same files); merge it into one of them, or sequence it after them, when it is planned.

**Touches:** `tools/phone/run-checks.ps1`, `tools/phone/checks/_lib.ps1`, `tools/phone/reset-dev-app.ps1`, `tools/phone/preflight.ps1`, `tools/phone/checks/paused-notification.ps1`, `tools/phone/test-run-checks.ps1`, `tools/CLAUDE.md`, `docs/plan/slices/236-phone-runner-gaps-night-2026-11-01-0859.md`

#### Work

1. **No `-Voices` on `run-checks.ps1`** (wave 2 digest entries 155, 278): Slice 220's short-lines check was run by hand for a second voice, and the check counts `NanTakes` but never prints it.
   - [ ] `run-checks.ps1 -Voices <ids>` passes the voice list to checks that take one; `short-lines` prints `NanTakes`. Verify: a self-test in `test-run-checks.ps1` shows the voices reaching the check's arguments.
2. **A run cannot be pinned to the debug or aot build** (entry 1325): `-Variant` applies only to `-Build`/`-Prepare`; a run swaps to aot when its background aot build is cached, so "stay on debug" could not be honoured.
   - [ ] `run-checks.ps1 -Variant debug|aot` on a run pins it: no swap, no background build of the other variant. Verify: self-test with both variants cached and `-Variant debug` installs debug and starts no build.
3. **`reset-dev-app.ps1 -Level auto` leaves a `held-user-cancelled` pack hold** (entries 1325-1359): with the app alive the ladder stops at level 1, and only `-Level clear` removes the hold; Slice 233 worked around it inside `paused-notification.ps1`.
   - [ ] `-Level auto` clears a hold that blocks downloads (or escalates when one is found). Verify: on the emulator, cancel a pack download, run `-Level auto`, and the pack starts again.
4. **The runner stalls on its own lease unless `RTST_PHONE_OWNER` is exported** (entry 1325; today only a workaround line in `tools/CLAUDE.md`).
   - [ ] With the variable unset, run-checks uses a default owner that matches the lease it takes. Verify: self-test without the variable takes and releases one lease and does not wait.
5. **Aside pack copies stay on the phone** (entries 1038, 1381, 1519): every run leaves tars under `/sdcard/Download/rtst-packs-aside/uk.sirous.readthestupidtext.dbg/`.
   - [ ] The runner deletes the aside copy after a verified restore, or `tools/CLAUDE.md` says why it is kept. Verify: after a run, `adb shell ls` of that folder is empty (or the reason is written).
6. **The pre-flight's stay-awake change is never recorded or undone** (interactive session 0779fbee entries 1849-1857): `preflight.ps1 -Fix` sets `stay_on_while_plugged_in` to 7 and nothing restores the old value.
   - [ ] `-Fix` writes the old value to the night folder; a `-Restore` (or the end-of-run step) sets it back. Verify: set 0, run `-Fix`, then `-Restore`, and read 0 again.
7. **An app kill 0.7 s after launch was never explained** (wave 2 entries 1454, 1461-1473): system_server logged "Compatibility: Killing package ... PlatformCompat overrides" during a `paused-notification-network` run. Likely a leftover `am compat` reset from the time-limit variant; not proven.
   - [ ] `paused-notification.ps1` logs the package's compat state at start and resets only what it set. Verify: two runs in a row (time-limit, then network) with no PlatformCompat kill in logcat.
8. **A raw CR byte in a `run-checks.ps1` comment** (round 2 `control-chars.mjs --all`): line 17 reads `.phone<CR>un-status.json`, a `\r` from `.phone\run-status.json` turned into a CR; same family as Slice 225 item 22 (the backspace bytes on lines 13-14 and in `common.ps1:570`). Do both together.
   - [ ] Rewrite the line with the Edit tool. Verify: `node ~/.claude/skills/feedback-loop/control-chars.mjs <worktree> origin/main --all` lists no hit in `tools/phone/`.
   - Round 3 feedback loop: the `run-checks.ps1` bytes were fixed by PR #682 (Slice 224); one backspace byte is still in `tools/phone/common.ps1` (counted on origin/main 2026-11-01), so only that file remains.
9. **The phone dozed on AC in the middle of a run and nobody read why** (round 3, wave 3 digest entries 860 and 928): at 16:15 the S21 read `mWakefulness=Dozing` with `AC powered: true`, while a pack download sat still; the 08:56 pre-flight had read stay-awake 7. Nothing re-runs the pre-flight's read-only checks before a timed run. (The `device-test` agent now reads `dumpsys power` before each timed run, ClaudeSkills #138; this item is the runner side.)
   - [ ] `run-checks.ps1` runs `preflight.ps1`'s read-only checks before each timed run and also compares the installed dev APK (`dumpsys package`: versionCode, lastUpdateTime) with the cached build it would install, reinstalling an older one before the budget starts (wave 3 entry 928: the first Slice 220 run measured an APK on the packs-10 catalogue). Verify: install an older dev APK, start a run, and it reinstalls before the first check. Same item: and stops with the changed setting named (or restores it where `-Fix` may). Verify: set stay-awake to 0 on the phone, start a run, and it reports the change before the first check.
