### Slice 221 — Device-test tooling leftovers from the 2026-10-30-2100 night, round 1

*In progress (wave 2026-11-01), draft PR.* Written 2026-10-30 by the round 1 feedback loop ([note](../notes/2026-10-30-2100-feedback-loop-w1.md)) and parked; the user moved it to `planned` on 2026-11-01.

**Touches:** `src/DinoReader.App/Testing/**`, `tools/emulator/install-debug.ps1`, `tools/emulator/run-flows.ps1`, `tools/emulator/lint-flows.ps1`, `tools/emulator/flows/**`, `tools/emulator/fixtures/lint/**`, `tools/phone/run-checks.ps1`, `tools/phone/common.ps1`, `tools/phone/lease.ps1`, `tools/phone/audio-timing.ps1`, `tools/phone/reset-dev-app.ps1`, `tools/phone/test-*.ps1`, `tools/phone/README.md`, `tools/CLAUDE.md`, `.github/workflows/release.yml` (item 4 only), `docs/plan/slices/221-device-test-tooling-leftovers-night-1.md` (widened at the claim, 2026-11-01, from a premise check)

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
   - [ ] The hook takes the voice from the settings `VoiceId` (or a `voice=` argument). Verify: a scripted read with two different voices logs two different voice ids.
2. **`install-debug.ps1 -App dino` installs `uk.sirous.dinoreader`** while the phone scripts and a stale emulator package use `uk.sirous.dinoreader.dbg` (wave-1 report).
   - [ ] One package id for Dino dev installs across `tools/emulator` and `tools/phone`; the installer removes the other one if present. Verify: after an install, `pm list packages | grep dinoreader` shows exactly one.
3. **Phone builds by hand** (Slice 210 S21 run): the agent copied build-cache steps by hand, although `tools/phone/run-checks.ps1 -Build -App rtst|dino [-Variant debug|aot]` already builds into the shared cache and exits. The brief named the emulator's `-BuildOnly`, and `tools/CLAUDE.md` does not point phone builds at `-Build`.
   - [ ] `tools/CLAUDE.md` (and the device-test brief template) name `run-checks.ps1 -Build` as the phone build-only step. Verify: a device-test run log shows it used.
4. **A workflow_dispatch release can be replaced by a merge's push run** (entries 1000-1022): `release.yml` uses one concurrency group, `cancel-in-progress: false`, so a *pending* force_all run is replaced by the next pending push run. The force_all dispatch 41136299215 was cancelled with no jobs on 2026-10-30 (the #634 merge push queued after it). The night tooling now holds such runs to the end of the night (ClaudeSkills PR #95); this item is the repo-side choice, which waits for the user.

**User answers (2026-11-01):** the user chose to give `workflow_dispatch` release runs their own concurrency group, so a later merge's push run cannot cancel them. The tooling rule may stay as well, but item 4 is now to be built.
   - [ ] Decide: give `workflow_dispatch` runs their own concurrency group (and check the two can't release the same version at once), or keep the tooling rule only.
5. **Worker typecheck in fast checks** (Slice 219, entries 431-448): the builder's checks never ran `tsc` (no `node_modules`), so CI went red. The agents now install dependencies first (ClaudeSkills PR #95).
   - [x] `worker/CLAUDE.md` names `npm ci && npm run typecheck` as the worker's fast check (done in the round 1 feedback-loop PR).
6. **The phone lease does not stop a second run by the same owner** (Slice 210 S21 run, digest entry 1202): a device-test agent started a second copy of the S21 run by mistake and had to kill it.
   - [ ] `tools/phone` refuses to start while a run with the same lease owner is still alive (pid in the lease), unless `-Force`. Verify: start a run, start a second one, the second exits with a clear message.

#### Verification

- Items 1-3: the verify line under each, on the emulator.
- Item 4: a force_all dispatch followed by a merge still runs its jobs.
