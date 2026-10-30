### Slice 221 — Device-test tooling leftovers from the 2026-10-30-2100 night, round 1

*parked: feedback loop, waits for the user.* Planned 2026-10-30 by the round 1 feedback loop ([note](../notes/2026-10-30-2100-feedback-loop-w1.md)). Parked so no later wave tonight builds it; the user moves it to `planned`.

**Touches:** `src/DinoReader.App/Testing/**`, `tools/emulator/install-debug.ps1`, `tools/phone/run-checks.ps1`, `tools/phone/common.ps1`, `tools/phone/lease*.ps1`, `tools/CLAUDE.md`, `.github/workflows/release.yml` (item 4 only), `docs/plan/slices/221-device-test-tooling-leftovers-night-1.md`

#### Work

1. **Dino `read` test hook ignores the settings voice** (Slice 210 device runs, digest entries 871 and 1146): the run had to write `LastNeuralVoiceId.Kokoro` into prefs for each voice.
   - [ ] The hook takes the voice from the settings `VoiceId` (or a `voice=` argument). Verify: a scripted read with two different voices logs two different voice ids.
2. **`install-debug.ps1 -App dino` installs `uk.sirous.dinoreader`** while the phone scripts and a stale emulator package use `uk.sirous.dinoreader.dbg` (wave-1 report).
   - [ ] One package id for Dino dev installs across `tools/emulator` and `tools/phone`; the installer removes the other one if present. Verify: after an install, `pm list packages | grep dinoreader` shows exactly one.
3. **Phone builds by hand** (Slice 210 S21 run): the agent copied build-cache steps by hand, although `tools/phone/run-checks.ps1 -Build -App rtst|dino [-Variant debug|aot]` already builds into the shared cache and exits. The brief named the emulator's `-BuildOnly`, and `tools/CLAUDE.md` does not point phone builds at `-Build`.
   - [ ] `tools/CLAUDE.md` (and the device-test brief template) name `run-checks.ps1 -Build` as the phone build-only step. Verify: a device-test run log shows it used.
4. **A workflow_dispatch release can be replaced by a merge's push run** (entries 1000-1022): `release.yml` uses one concurrency group, `cancel-in-progress: false`, so a *pending* force_all run is replaced by the next pending push run. The force_all dispatch 41136299215 was cancelled with no jobs on 2026-10-30 (the #634 merge push queued after it). The night tooling now holds such runs to the end of the night (ClaudeSkills PR #95); this item is the repo-side choice, which waits for the user.
   - [ ] Decide: give `workflow_dispatch` runs their own concurrency group (and check the two can't release the same version at once), or keep the tooling rule only.
5. **Worker typecheck in fast checks** (Slice 219, entries 431-448): the builder's checks never ran `tsc` (no `node_modules`), so CI went red. The agents now install dependencies first (ClaudeSkills PR #95).
   - [x] `worker/CLAUDE.md` names `npm ci && npm run typecheck` as the worker's fast check (done in the round 1 feedback-loop PR).
6. **The phone lease does not stop a second run by the same owner** (Slice 210 S21 run, digest entry 1202): a device-test agent started a second copy of the S21 run by mistake and had to kill it.
   - [ ] `tools/phone` refuses to start while a run with the same lease owner is still alive (pid in the lease), unless `-Force`. Verify: start a run, start a second one, the second exits with a clear message.

#### Verification

- Items 1-3: the verify line under each, on the emulator.
- Item 4: a force_all dispatch followed by a merge still runs its jobs.
