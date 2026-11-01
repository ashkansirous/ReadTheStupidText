### Slice 221 — Device-test tooling leftovers from the 2026-10-30-2100 night, round 1

*Built 2026-11-01, merged as PR #694 (f9208c1c); items 1, 2 and the lint run passed on emulator-5556, items 4 and 6 are not yet tried on GitHub / the real phone (see Not yet verified).* Written 2026-10-30 by the round 1 feedback loop ([note](../notes/2026-10-30-2100-feedback-loop-w1.md)) and parked; the user moved it to `planned` on 2026-11-01.

**Touches:** `src/DinoReader.App/Testing/**`, `tools/emulator/install-debug.ps1`, `tools/emulator/run-flows.ps1`, `tools/emulator/lint-flows.ps1`, `.maestro/dino/**`, `.maestro/README.md`, `tools/emulator/common.ps1`, `tools/emulator/fixtures/lint/**`, `tools/phone/run-checks.ps1`, `tools/phone/common.ps1`, `tools/phone/lease.ps1`, `tools/phone/audio-timing.ps1`, `tools/phone/reset-dev-app.ps1`, `tools/phone/test-*.ps1`, `tools/phone/README.md`, `tools/CLAUDE.md`, `.github/workflows/release.yml` (item 4 only), `docs/plan/slices/221-device-test-tooling-leftovers-night-1.md` (widened at the claim, 2026-11-01, from a premise check; `.maestro/` and `tools/emulator/common.ps1` added mid-build: the claim named a `tools/emulator/flows/` folder that does not exist)

#### Coordinator's design notes (claim, 2026-11-01)

Checked on origin/main before the claim:

- **Item 1.** The Dino hook is `src/DinoReader.App/Testing/DinoTestCommands.cs`. Its `voice=1` key (:30, :98) only starts the voice pack; nothing selects a voice. Add a `voiceId=<id>` launch key (in `AppLaunchKeys`, :55) that sets the voice through the existing `NeuralVoiceMemory` API (`src/ReadTheStupidText.Mobile.Shared/NeuralVoiceMemory.cs:12`, key `LastNeuralVoiceId.<engine>`) before the read. Call that API; don't edit Mobile.Shared. If it can't be called from the Dino app, stop and report.
- **Item 2.** There is one Dino dev id, `uk.sirous.dinoreader.dbg`: `tools/phone/common.ps1:132` already uses it. The emulator scripts and flows (`install-debug.ps1:24`, `run-flows.ps1:51`, `lint-flows.ps1:36`, the flow `appId`s) and `tools/phone/audio-timing.ps1:61` move to it, by the same `-p:ApplicationId` override the phone build uses. Keep the csproj id (`uk.sirous.dinoreader`), because it is the store id. **The installer removes plain `uk.sirous.dinoreader` only on an emulator serial (`emulator-*`), never on a real phone**, because on the user's phone it is their store install.
- **Item 3.** Only `tools/CLAUDE.md` changes here. The device-test brief template is in ClaudeSkills (`agents/device-test.md`), which is outside this repo, so it is left for the feedback loop.
- **Item 4 design (the user chose a separate group).** Today `release.yml:106-108` has one group, `release-${{ github.ref }}`. Under that:
  - **The race with two groups.** The version only moves when a tag is published (`gh release edit --draft=false`, :607). So a dispatch run and a push run in separate groups would compute the same version. They would also reuse the same draft, and its "retarget, delete assets" branch (:284-287) would wipe the other run's assets.
  - **Design.** `workflow_dispatch` runs use `release-dispatch-${{ github.ref }}` and push runs keep `release-${{ github.ref }}`, both with `cancel-in-progress: false`. Add a first step to the `version` job: wait while another `release.yml` run on the same branch with a smaller `run_number`, from the *other* event type, is `queued` or `in_progress` (`gh run list --workflow release.yml --branch <ref> --json databaseId,number,status,event`). Poll every 30 s, give up after 90 minutes, and fail with a clear message.
  - **Why it can't deadlock or race.** Each run waits only for older runs, so there is no cycle, and runs of the same type are already serialised by their group. The existing "tag already published" skip (:276-282) still decides what a later run releases.
  - **Preconditions.** The job needs `actions: read` permission for `gh run list`. Check whether it is set; if not, add it to that job only.
- **Item 6.** The phone lease has no pid today (owner and expiry only). Add `OwnerPid`, as `tools/emulator/test-lease.ps1:34` already does. `run-checks.ps1` refuses to start while the lease's owner and pid are alive, unless `-Force`. A dead pid counts as free.

#### Work

1. **Dino `read` test hook ignores the settings voice** (Slice 210 device runs, digest entries 871 and 1146): the run had to write `LastNeuralVoiceId.Kokoro` into prefs for each voice.
   - [x] The hook takes the voice from the settings `VoiceId` (or a `voice=` argument). Verify: a scripted read with two different voices logs two different voice ids.
   - *Built differently from the design note:* the `open-book ... voiceId=<id>` launch key in `DinoTestCommands.cs` does not go through `NeuralVoiceMemory` (review: Dino reads it only at app start, so a `stopApp:false` launch would ignore it). It validates the id against the installed pack voices, the model and the Supertonic table, calls `ISpeechReader.SetVoice` in-process and, when a pack language lists the id, `StoryVoicing.SetNarrator` for that language (this persists, so a dev install keeps the last narrator set). An unknown id logs `failed unknown voice <id>` and the book is not opened. Flow: `.maestro/dino/open-book-voice-id.yaml` (supertonic:F1, F2, not-a-voice).
2. **`install-debug.ps1 -App dino` installs `uk.sirous.dinoreader`** while the phone scripts and a stale emulator package use `uk.sirous.dinoreader.dbg` (wave-1 report).
   - [x] One package id for Dino dev installs across `tools/emulator` and `tools/phone`; the installer removes the other one if present. Verify: after an install, `pm list packages | grep dinoreader` shows exactly one.
   - *Built:* the dev id is `uk.sirous.dinoreader.dbg` everywhere: all `.maestro/dino` flows, the lint fixtures (new `plain-dino-appid.yaml`), the `lint-flows.ps1` dev list, `install-debug.ps1` / `run-flows.ps1` (via `-p:ApplicationId`) and the emulator cache name `android-dbg`; `audio-timing.ps1` already used it. `Get-StalePackagesToRemove` in `tools/emulator/common.ps1` removes plain `uk.sirous.dinoreader` only on `^emulator-\d+$` serials; the install fails unless exactly one `.dbg` package is there afterwards (on a non-emulator serial the store id is ignored); a failed uninstall prints a warning.
3. **Phone builds by hand** (Slice 210 S21 run): the agent copied build-cache steps by hand, although `tools/phone/run-checks.ps1 -Build -App rtst|dino [-Variant debug|aot]` already builds into the shared cache and exits. The brief named the emulator's `-BuildOnly`, and `tools/CLAUDE.md` does not point phone builds at `-Build`.
   - [x] `tools/CLAUDE.md` names `run-checks.ps1 -Build -App rtst|dino` as the phone build-only step. The device-test brief template (ClaudeSkills `agents/device-test.md`) is outside this repo and is left for the feedback loop. Verify: a device-test run log shows it used (not yet seen).
4. **A workflow_dispatch release can be replaced by a merge's push run** (entries 1000-1022): `release.yml` uses one concurrency group, `cancel-in-progress: false`, so a *pending* force_all run is replaced by the next pending push run. The force_all dispatch 41136299215 was cancelled with no jobs on 2026-10-30 (the #634 merge push queued after it). The night tooling now holds such runs to the end of the night (ClaudeSkills PR #95); this item is the repo-side choice, which waits for the user.

**User answers (2026-11-01):** the user chose to give `workflow_dispatch` release runs their own concurrency group, so a later merge's push run cannot cancel them. The tooling rule may stay as well, but item 4 is now to be built.
   - [x] Decide: give `workflow_dispatch` runs their own concurrency group (and check the two can't release the same version at once), or keep the tooling rule only. Built: `release.yml` runs `workflow_dispatch` in group `release-dispatch-<ref>` and push in `release-<ref>`. The `version` job has `actions: read`, `timeout-minutes: 130` and a first step that waits (60 s poll, 120 min ceiling, retries `gh` errors, fails closed after 3) while an older run (lower run number) of the other event type is queued or in progress. The ceiling is 120 min because a whole run took up to ~60 min on 2026-11-01 (an earlier brief said 30 min, which was one job's timeout).
5. **Worker typecheck in fast checks** (Slice 219, entries 431-448): the builder's checks never ran `tsc` (no `node_modules`), so CI went red. The agents now install dependencies first (ClaudeSkills PR #95).
   - [x] `worker/CLAUDE.md` names `npm ci && npm run typecheck` as the worker's fast check (done in the round 1 feedback-loop PR).
6. **The phone lease does not stop a second run by the same owner** (Slice 210 S21 run, digest entry 1202): a device-test agent started a second copy of the S21 run by mistake and had to kill it.
   - [x] `tools/phone` refuses to start while a run with the same lease owner is still alive (pid in the lease), unless `-Force`. Verify: start a run, start a second one, the second exits with a clear message. (Self-test only; not tried on the real phone.)
   - *Built differently from the design note (2026-11-01):* the pid is not in the lease but in a per-owner run marker `<worktrees>/.phone/run-<phone>-<owner>.json` written by `run-checks.ps1` (default owner `run-checks-<phone key>`, so the guard also fires without an owner variable). `lease.ps1 -Take` exits at once, so its own pid is short-lived and a lease that tracked it would read as free; the lease helpers are also in `tools/lease-common.ps1`, outside Touches. A second timed run of the same live owner exits 4; a dead or reused pid is free; `-Force` takes over; the marker is removed in a `finally` that also covers builds. Only timed runs are guarded (not `-Prepare`/`-Build`). Self-test: `tools/phone/test-phone-leftovers.ps1`.

#### Verification

- Items 1-3: the verify line under each, on the emulator.
- Item 4: a force_all dispatch followed by a merge still runs its jobs.
- Emulator emulator-5556, 2026-11-01: item 2 PASS (stale plain id removed, exactly one `.dbg`), `open-book-voice-id` PASS (75 s), `open-book` PASS. Both `run-flows.ps1` calls exited 137 although they printed PASS (not investigated).

#### Changed while building

- Touches: the claim named a `tools/emulator/flows/` folder that does not exist; widened mid-build to `.maestro/dino/**`, `.maestro/README.md` and `tools/emulator/common.ps1`.
- Item 1 sets the voice through the reader and `StoryVoicing`, not `NeuralVoiceMemory`; item 6 uses a per-owner run marker, not a lease pid (both described under the items).
- **Slice 225 item 22, `common.ps1` part (drop-cache marker):** main's `Clear-PageCache` pattern ends `...0<0x08>`, so it most likely never matched. Here it ends `...0`, which matches `RTST_DROP_RC=0` and no other exit code, so the check can now return true. The control-chars check found it at commit time. The coordinator removed the byte and first said the line "now matches origin/main"; that was wrong, because main has the byte too. 225 item 22 still owns the two `run-checks.ps1` comments (already clean on main) and the device check.

#### From review

High (`reviews/s221-high.md`): all 9 findings applied.

Medium (`reviews/s221-medium.md`): applied 1, 4, 5, 7, 8. Skipped:
- 2: a queued push run's own wait step still sees the older dispatch run (same-type runs rely on the concurrency group; no change).
- 3: the 120 min ceiling is deliberate (a whole run took up to ~60 min); only a stuck older run burns the minutes.
- 6: the `.dbg` id is defined in three places (style, left as is).

Check pass (`reviews/s221-check-medium.md`, 2 low findings): none applied.
- 1: skipped, the emulator run installed and ran the `.dbg` build, so the manifest id was right (the stale-obj worry did not happen).
- 2: skipped as low and unproven: `Remove-StaleDevPackages` runs `adb ... 2>&1` under `$ErrorActionPreference='Stop'` (`tools/emulator/common.ps1:151`) and on Windows PowerShell 5.1 adb's stderr could abort the install instead of warning.

#### For CLAUDE.md (lifted 2026-11-01)

- `tools/CLAUDE.md`: Dino dev installs are `uk.sirous.dinoreader.dbg` on emulator and phone; the plain `uk.sirous.dinoreader` is the store app and is removed only on `emulator-N` serials. (Slice 221: docs/plan/slices/221-device-test-tooling-leftovers-night-1.md)
- `.github/CLAUDE.md`: dispatch and push release runs have separate concurrency groups (`release-dispatch-<ref>`, `release-<ref>`), ordered by the `version` job's wait step; do not drop that step. (Slice 221: docs/plan/slices/221-device-test-tooling-leftovers-night-1.md)

#### Not yet verified

- Item 6 on the real phone: two timed runs with the same owner, the second exits 4.
- Item 4 on GitHub: a force_all dispatch followed by a merge still runs its jobs; the wait step's `jq`/`gh` output on a real run.
- `run-flows.ps1` exits 137 after printing PASS (not investigated).
- Item 3 seen in a device-test run log.
- `adb uninstall` stderr under Windows PowerShell 5.1 with `ErrorActionPreference` Stop (`Remove-StaleDevPackages`, `tools/emulator/common.ps1:151`) could abort the install instead of warning.
