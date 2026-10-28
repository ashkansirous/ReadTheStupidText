### Slice 180 — Faster device tests: test only the touched apps, build once, two emulators

*In progress* (draft PR, 2026-10-28).

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

- [ ] `ship-targets.mjs` gets a local mode: `node .github/scripts/ship-targets.mjs --local --base origin/main` diffs the working tree (committed and uncommitted) against the merge base and prints the targets, same mapping as CI. Unit tests in `ship-targets.test.mjs`.
- [ ] `tools/test-targets.ps1` turns those targets into device runs: `rtst-android`, `rtst-windows`, `dino-android`, `dino-windows` (iOS stays CI-only: no Mac). A `tools/` or `.maestro/` change runs the apps whose checks it edits; anything it can't place runs everything (fail safe means test more, never less).
- [ ] `run-flows.ps1` and `Invoke-DesktopTest.ps1` accept `-App auto` (calls `test-targets.ps1`) and print which apps were skipped and why.
- [ ] ClaudeSkills `device-test` and `run-wave` briefs: device checks start from `-App auto`; a caller may add an app, never silently drop one.

#### Part B — build once per commit, reuse it

- [ ] A build key = `HEAD` plus a hash of the uncommitted diff (`git stash create` or a temp index `write-tree`), per app and platform.
- [ ] `install-debug.ps1` and the new `tools/desktop-vm/Build-TestPackage.ps1` (the README's MSIX commands, scripted) put outputs in `<worktrees>/_builds/<app>-<platform>-<key>/` and reuse a match instead of building. Old keys are pruned (keep the newest few per worktree) so `D:` doesn't fill.
- [ ] `Invoke-DesktopTest.ps1` finds the package by key rather than "newest under `pkgs`", so a stale MSIX is never tested by accident.
- [ ] Builds happen outside the emulator and VM locks: an agent holds a device only while installing and testing.

#### Part C — Android and Windows side by side; build speed

- [ ] `device-test` / `run-wave` (ClaudeSkills): when both platforms are due, the Android and Windows checks run as two agents at once (separate locks already allow it).
- [ ] Measure a no-change rebuild of Dino Android in a worktree with and without `--disable-build-servers`. If keeping the servers saves real time, change the rule in `home/CLAUDE.md` and `device-test`: keep them during the slice, and at worktree removal stop only the build processes holding files in that worktree (never `dotnet build-server shutdown`, which stops every session's). If removal still hits locked files, keep the current rule and record why here.
- [ ] Dev Drive trust: record the `fsutil devdrv query D:` result here; if untrusted, the user runs `fsutil devdrv trust D:` (admin).

#### Part D — a second emulator

- [ ] `tools/emulator/*` takes an instance (1 or 2): AVD `RTST_Test` / `RTST_Test2` (on `C:`), ports 5554 / 5556, one state file and lock per instance. `start-warm.ps1 -Instance any` picks a free one; every script addresses its own serial.
- [ ] Creating the second AVD is scripted (same system image and flags as the first).
- [ ] `tools/phone/timing.ps1` and `audio-timing.ps1` (and any emulator timing run) take an exclusive quiet-machine lock that waits for both emulators, the VM and running builds to be idle.
- [ ] Measure: two flows runs plus a VM run plus one build at once: CPU, RAM, and whether any flow gets flaky. If it does, document the limit (e.g. two emulators but no build alongside) rather than ship flakiness.

#### Verification

- `ship-targets.test.mjs` covers the local mode; `test-targets.ps1` prints `dino-android, dino-windows` for a Dino-UI-only diff and all four for a `Mobile.Shared` core change.
- Timed on this machine, recorded here against the 2026-10-28 numbers above:
  - a Dino-only change, full device pass from a cold start: target under 10 minutes;
  - the same pass rerun at the same commit (cache hit): no build step;
  - two slices' emulator flows at once on two emulators, both passing.
- The VM still restores `clean` and passes its checks with a cached MSIX.
