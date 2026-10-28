### Slice 180 — Faster device tests: test only the touched apps, build once, two emulators

*Built* (PR #536, 2026-10-28).

**Touches:** `tools/test-targets.ps1` (new), `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `tools/emulator/*`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/desktop-vm/Build-TestPackage.ps1` (new), `tools/desktop-vm/README.md`, `tools/desktop-vm/CLAUDE.md`, `tools/phone/timing.ps1`, `tools/phone/audio-timing.ps1`, `.maestro/README.md`, `docs/plan/slices/180-faster-device-tests.md`. Outside this repo (one ClaudeSkills PR): `agents/device-test.md`, `skills/run-wave/SKILL.md`, `home/CLAUDE.md`.

Tooling only, no app behaviour change. Started after `feat/vm-update-only` (PR #532) merged: it edits `tools/desktop-vm/README.md`, `CLAUDE.md` and `_common.ps1` too.

#### Why

On 2026-10-28 a full device pass (both apps on the emulator and on the Hyper-V VM) took about 15-25 minutes when nothing was queued, and much longer in a wave. Measured from that day's agent runs:

- emulator cold boot about 45 s (once); Debug build and install 1.6-4 min; Maestro flows 3.5-6 min per app, about 10 min for both;
- VM checks 1.3-2 min per app, but each run builds its MSIX first: the Dino suite took about 7 min end to end, and one three-slice VM run 21 min;
- one emulator for the whole wave: slices waited 10-25 min for each other several times (177 for 174, 173 for 177 and back);
- agents looped over both apps for single-product changes (emulator flows for both apps, about 10 min; VM for both, 7.7 min);
- every fixer and tester rebuilt the same APK and MSIX for the same commit;
- inside one agent, Android and Windows checks ran one after the other, never side by side.

The machine: i9-11950H (8 cores, 16 threads), 32 GB RAM; `D:` is already a Dev Drive (ReFS) holding the worktrees and the NuGet cache (`D:\Caches\nuget\packages`), 42 GB free of 98; the AVD lives on `C:` (149 GB free). The VM uses 4 vCPUs and 6 GB.

#### Decisions locked with the user (2026-10-28)

- All four parts, in this order; the second emulator is last, after the cheaper parts are measured.
- Audio timing checks (Slices 165/177) run only on a quiet machine: they take an exclusive lock.
- Dev Drive: already in place; only its Defender trust is checked (the user runs `fsutil devdrv query D:` as admin).

#### Part A — test only the apps a change touches (smallest end-to-end first)

- [x] `ship-targets.mjs` gets a local mode: `node .github/scripts/ship-targets.mjs --local --base origin/main` diffs the working tree (committed and uncommitted) against the merge base and prints the targets, same mapping as CI. Unit tests in `ship-targets.test.mjs`. *Built; 100 unit tests pass. Also `--stdin` (file list from standard input) for test-targets.ps1.*
- [x] `tools/test-targets.ps1` turns those targets into device runs: `rtst-android`, `rtst-windows`, `dino-android`, `dino-windows` (iOS stays CI-only: no Mac). A `tools/` or `.maestro/` change runs the apps whose checks it edits; anything it can't place runs everything (fail safe means test more, never less). *Built. A Dino-UI-only diff prints `dino-android, dino-windows`; a Domain change prints all four. A Mobile.Shared root change prints rtst-android, dino-android, dino-windows (no rtst-windows: the RTST Windows app does not reference Mobile.Shared, same as CI), so only Domain/Application/Documents changes run all four.*
- [x] `run-flows.ps1` and `Invoke-DesktopTest.ps1` accept `-App auto` (calls `test-targets.ps1`) and print which apps were skipped and why. *Built (`-App auto` on both; Android takes `-Instance`).*
- [ ] ClaudeSkills `device-test` and `run-wave` briefs: device checks start from `-App auto`; a caller may add an app, never silently drop one. *Not done here: ClaudeSkills PR.*

#### Part B — build once per commit, reuse it

- [x] A build key = `HEAD` plus a hash of the uncommitted diff (`git stash create` or a temp index `write-tree`), per app and platform. *Built as a content hash instead: HEAD with the working tree laid over `src/` and the root build files (temp index, real index untouched), so docs, tools and tests never invalidate a build and the same inputs on another commit or worktree reuse it. ~1.4 s to compute.*
- [x] `install-debug.ps1` and the new `tools/desktop-vm/Build-TestPackage.ps1` (the README's MSIX commands, scripted) put outputs in `<worktrees>/_builds/<app>-<platform>-<key>/` and reuse a match instead of building. Old keys are pruned (keep the newest few per worktree) so `D:` doesn't fill. *Built (`build-cache.ps1`; newest 3 per app, platform and worktree kept). Not yet run end to end: no APK/MSIX build was timed (quiet-machine request).*
- [x] `Invoke-DesktopTest.ps1` finds the package by key rather than "newest under `pkgs`", so a stale MSIX is never tested by accident. *Built.*
- [x] Builds happen outside the emulator and VM locks: an agent holds a device only while installing and testing. *Built: the APK/MSIX builds take no emulator or VM lock; they register as an activity instead (`activity.ps1`).*

#### Part C — Android and Windows side by side; build speed

- [ ] `device-test` / `run-wave` (ClaudeSkills): when both platforms are due, the Android and Windows checks run as two agents at once (separate locks already allow it). *Not done here: ClaudeSkills PR.*
- [x] Measure a no-change rebuild of Dino Android in a worktree with and without `--disable-build-servers`. If keeping the servers saves real time, change the rule in `home/CLAUDE.md` and `device-test`: keep them during the slice, and at worktree removal stop only the build processes holding files in that worktree (never `dotnet build-server shutdown`, which stops every session's). If removal still hits locked files, keep the current rule and record why here. *Measured 2026-10-28 (docs/plan notes: `reviews/180-device-run.md`): no-change Dino Android rebuild with build servers on 116 s then 76 s; with `--disable-build-servers` 77 s and 77 s. Keeping the servers saves nothing: the `--disable-build-servers` rule stays.*
- [x] Dev Drive trust: record the `fsutil devdrv query D:` result here; if untrusted, the user runs `fsutil devdrv trust D:` (admin). *The user ran `fsutil devdrv query D:` on 2026-10-28: "This is a trusted developer volume. Developer volumes are protected by antivirus filter. Filters currently attached: WdFilter". Trusted, Defender in performance (async) mode; no action needed.*

#### Part D — a second emulator

- [x] `tools/emulator/*` takes an instance (1 or 2): AVD `RTST_Test` / `RTST_Test2` (on `C:`), ports 5554 / 5556, one state file and lock per instance. `start-warm.ps1 -Instance any` picks a free one; every script addresses its own serial. *Built (`-Instance 1|2|any` on start-warm; 1|2 on stop, install-debug, run-flows). Run on two emulators 2026-10-28. Bugs found there and fixed: `-Instance 2` was overwritten by common.ps1 (`$script:Instance` clashed with the parameter, now `$script:EmuInstance`), and the per-instance Maestro home was never applied.*
- [x] Creating the second AVD is scripted (same system image and flags as the first). *Built: `tools/emulator/new-avd.ps1` copies config.ini only (no download; fresh AVD starts small on C:, RTST_Test is 16 GB on disk, so docs never say the AVD is copied). Run: took 1 s; RTST_Test2 first boot 101 s under load.*
- [x] `tools/phone/timing.ps1` and `audio-timing.ps1` (and any emulator timing run) take an exclusive quiet-machine lock that waits for both emulators, the VM and running builds to be idle. *Built as `tools/emulator/activity.ps1` (quiet lock + activity registry), used by `audio-timing.ps1`; `timing.ps1` is pure parsing and needed no change. Lock logic checked across two processes.*
- [x] Measure: two flows runs plus a VM run plus one build at once: CPU, RAM, and whether any flow gets flaky. If it does, document the limit rather than ship flakiness. *Measured: two emulators with flows and no VM, both 2/2 PASS (133 s and 126 s), CPU peak 100%, RAM peak 30.7 of 32 GB. Two emulators + the VM: CPU 100%, instance 2 2/2, instance 1 1/2 FAIL, VM 0/3 FAIL. Limit, enforced in `activity.ps1` and documented in both READMEs: no VM run while two emulators run flows, and a second emulator's flows wait while the VM runs; builds now run at BelowNormal priority (flows failed 0/2 while an MSIX build held the CPU at 100%, with a System UI ANR that `run-flows.ps1` now detects and dismisses).*

#### Verification

- `ship-targets.test.mjs` covers the local mode; `test-targets.ps1` prints `dino-android, dino-windows` for a Dino-UI-only diff, `rtst-android, dino-android, dino-windows` for a `Mobile.Shared` core change (as CI) and all four for a Domain change.
- Timed on this machine, 2026-10-28 (tree a6d86396506b, Dino only):
  - [x] a Dino-only change, full device pass from a cold start, Android and VM as two background jobs: 6 min 45 s wall (target under 10 min: met). Android: build 179 s (emulator boot 53 s overlapped), flows 129 s, total 326 s; Windows: MSIX build 275.5 s (waited behind the Android build, same checkout), VM 3/3 PASS about 130 s, total 405 s. The Android flows failed 0/2 in this run because of the System UI ANR while the MSIX build ran (see Part D);
  - [x] the same pass rerun at the same commit (cache hit): APK "Reusing ... key" and MSIX "Using the cached", no build step; VM 128 s PASS; Android flows failed on the stale ANR dialog until the emulator restarted, then 2/2 PASS in 100 s (boot 36 s, total 171 s);
  - [x] two slices' emulator flows at once on two emulators, both passing: 2/2 each (133 s and 126 s).
- [x] The VM restores `clean` and passes its checks with a cached MSIX (confirmed 3 times; VM left Saved).
- [x] `test-targets.ps1 -Files <Dino UI xaml>` printed `dino-android, dino-windows` in 0.4 s.
- Re-check after the fixes (2026-10-28 22:02-22:20): `-Instance 2` boots emulator-5556 in 48 s and `-Instance any` picks the free one; two flows runs at once passed 2/2 each (122 s and 150 s) on the second try (first try: instance 2 1/2, GalleryTitle not visible on first run, passed alone); the VM run printed that it waited for the two emulators, then passed 3/3 with the cached MSIX; a `-Rebuild` during flows ran at BelowNormal and the flows passed 2/2.

**Not yet verified:** the System UI "isn't responding" dismissal in `run-flows.ps1` (no safe way to trigger one; read only); Part E's phone guards on a real phone; one flake of a first-run flow with two emulators at once (watch for repeats).

#### Part E — a real-phone run never waits in silence

Why: on 2026-10-28 a run sat silent about 20 minutes: the phone was on battery and dozed (`svc power stayon true` works only while charging), Android froze the app, the 124 MB `supertonic-3-4` pack download stalled at 702,966 bytes and the script waited out its 1200 s pack timeout (`reviews/phone-2026-10-28.md`).

- [x] Before a run print `dumpsys battery` power and `mWakefulness`; on battery warn "plug the phone in" and run a `KEYCODE_WAKEUP` loop every 15 s (no unlock, no taps), always stopped in `finally`. *Built in `common.ps1` and `audio-timing.ps1`. Not run on the phone (disconnected).*
- [x] After launch the app's first log line (read by pid) must come within 60 s, else fail with the pid, power state and last phone log lines. *Built (`-FirstLogSeconds`).*
- [x] Pack download: poll the `.partial` size; no growth for 120 s fails with "pack download stalled at N bytes"; progress at most every 30 s. *Built (`-PackStallSeconds`; reads `files/packs/*.partial` through `run-as`, Debug build).*
- [x] Mid-read: no new app log line for 120 s fails the read with the last line seen. *Built (`-ReadSilenceSeconds`, cold and warm reads).*
- [x] README: the phone may stay locked only while charging or with the keep-awake loop. *Done.*
