### Slice 216 — Phone tooling leftovers from the Slice 213 runs

*Built 2026-10-31 (night run), merged as PR #637 (1a444ac); S21 checks for items 1, 2 and notification-text passed, item 11 not yet re-run.* Findings from the 2026-10-30 S21 runs that no slice owned. Tooling only, except item 1 if it turns out to be an app bug.

**Touches:** `tools/phone/**`, `tools/lease-common.ps1` (corrupt-lease wording), `tools/CLAUDE.md`, `tools/emulator/build-cache.ps1` (widened by the coordinator, 2026-10-30; was: `tools/phone/audio-timing.ps1`, `tools/phone/run-checks.ps1`, `tools/phone/common.ps1`, `tools/emulator/build-cache.ps1`, `tools/phone/README.md`, `tools/CLAUDE.md`, `src/ReadTheStupidText.Mobile.Shared/**` (only if item 1 is an app bug), `docs/plan/slices/216-phone-tooling-leftovers.md`), plus `tools/lease-common.ps1` and `tools/CLAUDE.md` as named by the coordinator

#### Work

1. **A build swap re-downloaded the voice pack.** On the S21 (11:19-11:20), installing the plain Debug APK over the AOT one was followed by `PACKDL start pack=supertonic-3 offset=0`: the pack was gone. Cause not proven; likely `audio-timing.ps1` ran with `-FreshInstall` or the two APKs are treated as different installs. The runner avoids it (`adb install -r`, packs checked after every install), but a user-side cause (an app update losing packs) would be serious.
   - [x] (RESOLVED 2026-10-31: packs survive `adb install -r`; the Slice 213 re-download came from `-FreshInstall`. Script: `tools/phone/repro-pack-survives-install.ps1 -App rtst`; src/ untouched until it runs) Reproduce on the S21: AOT installed with packs, `adb install -r` the Debug APK, list `files/packs`. If the packs survive, the cause was the script's flags: fix or document them. If they don't, it is an app bug: find why the packs folder is cleared on an update and plan the fix (Android and iOS).
2. **`audio-timing.ps1` repeat runs are not cold.** Its second and later runs read 240-578 ms because the sentence cache was warm, and it labels them as cold starts.
   - [x] Clear the sentence cache (as Slice 213's `cold-start` does) before every run that is reported as cold, or label the run "warm".
3. **Build path length.** The first parallel build failed with APT2264 (path too long) until the `--artifacts-path` folder names were shortened. A deeper worktree path can hit it again.
   - [x] Before a build, compute the longest expected intermediate path (worktree path + artifacts folder + the deepest known MAUI intermediate) and fail early with "path too long: move the worktree or shorten <x>" instead of a build error minutes later. Self-test the check.
4. **`audio-timing.ps1` writes logs next to the repo** (`<repo parent>\.phone`), not in the shared worktrees folder (`Get-WorktreesRoot`) like the runner.
   - [x] Use `Get-WorktreesRoot` for its output folder, like the runner.

5. **The build command exists twice** (`Build-PhoneApk` in `audio-timing.ps1`, `Build-VariantApk` in `run-checks.ps1`; `audio-timing.ps1` cannot be dot-sourced). A change to one can silently miss the other.
   - [x] Move it into `common.ps1` and call it from both; a self-test that both produce the same command line for each variant.
6. **The lease expiry moved between runs** (preflight printed 13:29/13:30 on the `-Prepare` runs and 12:55/12:56 on the timed runs, 2026-10-30). Not looked into: likely `-Prepare`'s 45-minute lease against the timed run's budget-sized one, but not proven.
   - [x] Confirm from the code which lease length each mode takes and print it with its reason; fix if a mode shortens or lengthens someone else's lease.
7. **A corrupt lease file blocks the phone for 24 hours** (fails closed by design, `tools/phone/README.md`) and only a person deleting it frees the phone.
   - [x] `lease.ps1 -Show` and preflight name a corrupt lease, its age and when it frees; `lease.ps1 -Clear -Corrupt` removes only a corrupt file (never a valid one).
8. **The `highlight` check prints very long uiautomator diff lines** into the run output.
   - [x] Print only the changed texts, cut to 120 characters, full dump to the log file.

9. **Half of PR #610 was never reviewed.** Both review passes ran before the parallel builds, the `-Prepare` pack restore, the notification-text redesign and the deadline plumbing were written; the second pass was also the reviewer's own read, because the forked review had not finished (feedback loop 2026-10-30).
   - [x] Run `/code-review` (high, then medium) on `tools/phone/` as merged, and fix what it finds. (High pass done 2026-10-31, findings 1-7 fixed below; the medium pass done 2026-10-31, see "Item 9 medium pass notes".)
10. **A leftover aside pack archive after a failed restore is only reported, not restored.**
   - [x] On the next run, restore a leftover aside archive (verified copy) before anything else, or say plainly why it can't.

11. **Night cold-start numbers may be warm.** Dino AOT 624 ms (97 ms synthesis) and Kokoro 960 ms vs Supertonic 6229 ms came from `audio-timing.ps1` (item 2). "Kokoro stays" and Slice 185's result rest on them.
   - [ ] Rerun them with `run-checks.ps1 -Checks cold-start` (sentence cache cleared) and update Slices 185 and 214 with the real numbers.

#### Verification

- Fast: self-tests for items 2-4 in `pwsh` and Windows PowerShell 5.1.
- S21: item 1's reproduction; one `audio-timing.ps1 -Rates 1` run with two repeats labelled correctly.

#### Built (night run 2026-10-30, not yet run on a device)

- Items 2-7 built in `tools/phone/` (self-test `test-phone-leftovers.ps1`, pwsh and Windows PowerShell 5.1). Item 1: the repro script is written, not run. Item 6 finding: no mode shortens a lease; `-Prepare` writes 45 + 15 min, a timed run budget + 15, a longer own lease is kept (the differing expiry was by design); the length and reason are now printed.
- Item 3 limit is an estimate and was never measured (`DeepestMauiIntermediateChars` = 150 (raised from 190 in the medium review: at 190 the warning fired on nearly every worktree path; now it sits just below the 160 that fails), from the ~230-character ML Kit paths minus the project folder). Review fix: past the estimate it only WARNS; the build is stopped only when the artifacts path plus a 100-character floor (`KnownMinimumIntermediateChars`, also an estimate) passes 260. Tune both after the first APT2264.
- Review fixes (high pass): the repro script says the install marker no longer matches (next run needs `-Reinstall`); `reset-dev-app.ps1` restores a leftover archive only for levels other than `setting` and only when it is under 24 h old; the corrupt-lease advice is per kind (emulator: delete by hand); `lease.ps1 -Clear -Corrupt` works with no phone connected (checks every phone lease file); `audio-timing.ps1` no longer dies when the sentence cache cannot be emptied (warns, labels the table "cache state unknown").
- Item 7: the corrupt-lease wording lives in `tools/lease-common.ps1` `Format-LeaseHolder` (age, when it frees), so `lease.ps1 -Show`, preflight and `Assert-PhoneLease` all use it.
- Item 8: `tap.ps1` prints only the changed texts, cut to 120 characters (`Format-ScreenChangeLine`); the full signatures go to `<worktrees>/.phone/tap-changes.log`. Item 10: `reset-dev-app.ps1` restores the newest leftover aside archive first (only when the app has no installed pack, verified by `Restore-PacksAside`; a failure is said and the archive stays). `Clear-SentenceCache` now exists once, in `common.ps1`. Item 9 and the device re-measure of item 11 left to the coordinator (no script change needed for 11: `cold-start` already empties the cache).
- CI owed: the coordinator adds `./tools/phone/test-phone-leftovers.ps1` (pwsh and powershell) to `.github/workflows/tools-selftests.yml` after Slice 215 merges.

#### Device run 2026-10-31 (S21, wave1-s216, uncommitted)
- Item 1 `repro-pack-survives-install.ps1 -App rtst`: PACKS SURVIVED. Packs before (files/packs) `ocr-latin/v1`, `supertonic-3/v4`; after AOT install and after Debug `adb install -r` over it: the same two packs (`ocr-latin, supertonic-3`), files intact. lastUpdateTime 10:30:26 (AOT) then 10:30:33 (Debug). So the Slice 213 re-download came from the script flags (`-FreshInstall` clears data), not from the install. Not an app bug.
- Item 2 `audio-timing.ps1 -App rtst -Aot -Rates 1,1 -Warm`: labels correct. Repeat 1: `1x COLD (sentence cache emptied)` first audio 3856 ms, `1x WARM` 138 ms; repeat 2: COLD 3853 ms, WARM 108 ms; no gaps flagged; total read 26.2-26.3 s vs 25.85 s audio. Cache state is emptied before each cold read, so cold repeats agree.
- Script bug found: `pwsh -File audio-timing.ps1 -Rates 1,1` passes the string "1,1", which became rate 11 (`== rtst at 11x`, ran a 2350 ms-audio read). Rates as an array only works via `-Command "& ... -Rates 1,1"`. Fix: parse a string into doubles, or document it (`[double[]]$Rates` in `tools/phone/audio-timing.ps1`).
- Fix (2026-10-31): `audio-timing.ps1 -Rates` is now a string list split on commas (`ConvertTo-RateList` in `common.ps1`, invariant culture, 0.5-2.0 only); self-tested in `test-phone-leftovers.ps1`, which now runs in `tools-selftests.yml`.

#### Item 9 review notes (high pass on `tools/phone/` as merged, 2026-10-31)
- 1: `Wait-ForBuildsOnce` now moves the deadline (`Set-CheckDeadline (Get-BudgetEnd)`) after every poll, so install, preflight and the quiet wait keep the time the build wait gave back.
- 2: the quiet-machine wait starts from `Get-BudgetStart` (run start plus waited seconds); self-test cases in `test-run-checks.ps1`.
- 3: `-Prepare` resets `RunStart` after its build, so the lease wait and install get their own clock.
- 4: `Get-BuildView` reads the status file before the cache, so an `ok` status always finds the APK.
- 5: a lease lost during a build wait now exits 4 (`Get-StageErrorExitCode`, self-tested).
- 6: a notification record with no readable post time no longer counts as posted after the download started (self-tested); if the S21 dump has no time field, notification-text FAILs with the "no notification" detail instead of passing on a stale one.
- 7: `notification-text.ps1` rethrows a `BUDGET:` error after the pack restore, so it is a TIMEOUT, not a FAIL.
- 8 (corrupt-lease wording with no date) skipped by the coordinator.
- Runtime change: 1, 2, 3, 5, 6, 7 change run-time behaviour (waits, exit codes, PASS rule); not device-run.

#### Item 9 medium pass notes (2026-10-31)
- 1 and 6: one budget model. The budget start is the single value `Get-BudgetStart` (run start plus waited seconds); `New-BudgetClock`/`Reset-BudgetClock` restart the pair together (-Prepare), and the lease wait, quiet wait and elapsed time all read it (`Get-BudgetPreWaitMinutes`, `Get-BudgetElapsedSecondsAt`). Self-tested in `test-run-checks.ps1`.
- 2: a dump with no post time now FAILs with "no post time in the dump" (not "no notification"), so it is not mistaken for an app bug.
- 5: aside archives are stamped and age-checked with the phone's own clock (`Get-PhoneStamp`, `date` on the phone), not the PC's. Runtime change (reset and notification-text paths), not device-run.
- 3 (dead `-Clear` block in lease.ps1) skipped: dead code, no effect. 4 (false WARNING from a no-match `ls`) skipped: wording only. 8 (path-length numbers) skipped: known estimates, tune after the first APT2264. 9 (tap.ps1 truncated output, unrotated log) skipped. 10 (`-Rates "1,5"` message) skipped: wording.

- 2026-10-31 S21 re-run (wave1-s216b, AOT cache ea0d0370b0e3, no rebuild): `run-checks -Checks notification-text` PASS (title 'Downloading natural voices… 0%', 11 s; no "no post time" wording, so the S21 dump was readable). `audio-timing -Aot -Rates 1,1 -Warm`: two repeats, labels COLD/WARM/COLD/WARM correct, no rate 11, 0 gaps (cold first audio 4000/4069 ms, warm 80/115 ms).

#### Final reviews and changed while building (night run 2026-10-31)
- Review passes run: `216-high`, `216-medium`, the item 9 folder review (`216-phone-folder-high`, 7 findings fixed), and a final medium pass (`216-final-medium`, 5 fixed, 5 skipped). Budget handling is now one clock in run-checks (`Get-BudgetStart` / `New-BudgetClock`).
- Item 1 result: the S21 keeps packs across `adb install -r` of the Debug build over AOT. The Slice 213 re-download came from `-FreshInstall`, not the install.
- `audio-timing.ps1 -Rates "1,1"` was read as 11 under `pwsh -File`; fixed with `ConvertTo-RateList`. After the fixes on the S21: notification-text PASS; `audio-timing -Aot -Rates 1,1 -Warm` PASS (cold first audio about 4.0 s, warm 80-115 ms, labels right).
- `tools-selftests.yml` now runs `test-phone-leftovers.ps1`.
- Seen twice: `run-checks` skips with "installed variant unknown" until `-Reinstall` on a fresh night.

#### For CLAUDE.md (lifted 2026-10-31)
- `tools/CLAUDE.md`: pass `audio-timing.ps1 -Rates` as one comma list (`-Rates 1,1.5`), which works in any shell; the script splits it itself. (Slice 216: docs/plan/slices/216-phone-tooling-leftovers.md)
- `tools/CLAUDE.md`: run-checks has one budget clock (`Get-BudgetStart` / `New-BudgetClock`); lease wait, quiet wait and elapsed time all read it, so never add a second start time. (Slice 216: docs/plan/slices/216-phone-tooling-leftovers.md)

#### Not yet verified
- Item 11: the cold-start rerun and the numbers in Slices 185 and 214 (not edited from this branch).
- The path-length estimates (`DeepestMauiIntermediateChars`, `KnownMinimumIntermediateChars`) are still unmeasured.
- The aside-archive phone-clock stamp (`Get-PhoneStamp`) was not device-run.
