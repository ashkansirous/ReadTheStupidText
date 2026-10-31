# Slice 215: close the gaps left by Slices 207 and 212

*Built (night run 2026-10-31), merged as PR #639 (a28f339). Both review passes done; emulator checks pass; the VM site-image retake is not done (see Not yet verified).* Follow-up to Slices 207 and 212. Tooling, plus one app id.

**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml` (one `AutomationId`), `.maestro/**`,
`tools/emulator/run-flows.ps1`, `tools/emulator/common.ps1`, `tools/emulator/activity.ps1`,
`tools/emulator/lint-flows.ps1`, `tools/emulator/test-lint-flows.ps1`, `tools/emulator/test-activity.ps1` (new),
`tools/emulator/fixtures/**`, `tools/emulator/README.md`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/desktop-vm/_common.ps1`, `site/img/control-panel.webp`,
`.github/workflows/tools-selftests.yml`, `docs/plan/slices/215-device-guard-gaps.md`

**Platforms:** the `AutomationId` is in shared MAUI XAML, so Android and iOS get it together; the rest is Windows-host tooling.

## Why

Slices 207 and 212 left these open without a plan (user, 2026-10-30: "have you planned all the findings?"):

1. The pack status label (`PackStatusLabel`, `ReaderPage.xaml:73`) has no `AutomationId`, so the Farsi and Norwegian
   flows match translated text, which breaks whenever a translation changes.
2. The emulator's Android system died once mid-run (`DeadSystemException`, `pm` gone, `sys.boot_completed` still 1).
   `run-flows.ps1` didn't notice: the flow failed at its first step and an agent read it as an app failure.
3. The "one emulator running flows at a time" default (`RTST_ALLOW_TWO_EMULATORS`) was never tested.
4. Lint gaps the reviews found: a `launchApp` inside a `runFlow` sub-flow escapes the restart rule; the inline
   `launchApp: {...}` form is split on commas inside values; a `clearState` comment check gives false positives
   (comment above the enclosing item, quoted text); no fixture for an explicit `stopApp: true`.
5. `Invoke-DesktopTest.ps1` reads the `# needs:` headers twice and prints the skip line twice.
6. The Slice 194 control-panel shot was never retaken: the VM is offline by design and the clean checkpoint has no voice pack, so the panel shows "Preparing the voice" (and now the Slice 207 preflight says NO-GO).
7. `test-quoting.ps1` and `test-timing.ps1` run in both `tests.yml` and `tools-selftests.yml`.
8. The Farsi flows wait for "Downloading natural voices" text; with the pack already installed that wait fails. `clearState` prevents it today, nothing says so.
9. The Slice 183 "keep downloading when locked?" dialog is dismissed by an optional tap on `android:id/button2`; a passing flow doesn't prove the tap dismissed it.
10. The emulator's `system_server` died once (2026-10-30 11:32) after hours of runs; item 2 detects it, nobody looked for the cause.

## Design (settled; build exactly this)

1. **Status by id.** Add `AutomationId="PackStatusLabel"` to the label. Switch the two Farsi flows
   (`cancelled-voice-pack-stays-cancelled`, `voice-cancel-holds-scan-pack`) and the English ones that wait on the
   status text to `id: PackStatusLabel` plus the state's text where the state matters (keep the fa regex for the
   state; the id only pins which element). Note the rule in `.maestro/README.md`.
2. **Emulator health before every flow.** In `run-flows.ps1`, before each flow: `adb shell pm path android` must
   answer and `getprop sys.boot_completed` must be 1. If not, print `EMULATOR UNHEALTHY: <what>`, restart it
   (stop + start-warm from a neutral working folder, never from inside a worktree, so the emulator never holds a
   worktree open), and run the flow once more. A second unhealthy check stops the run with exit 5 naming it. After a
   failed flow, run the same check, so an emulator death is reported as that, never as a flow failure.
3. **Test the limit.** `test-activity.ps1`: `Get-ConcurrencyConflict` with two fake emulator activities refuses the
   second by default and allows it with `RTST_ALLOW_TWO_EMULATORS=1`; VM plus one emulator allowed. Add it to
   `tools-selftests.yml` (both shells).
4. **Lint.** Follow `runFlow` file references (relative paths) and apply the restart rule as if inlined; parse the
   inline form with a small tokenizer that respects quotes and nested braces; check the `# clearState:` comment
   against the line directly above the `clearState` key or its enclosing `- launchApp:` item, and ignore text in quotes;
   fixtures for each plus `stopApp: true` (flagged like a missing one, unless it has a restart comment).
5. Read the `# needs:` headers once in `Invoke-DesktopTest.ps1`.
6. **Voice pack into the VM.** `Invoke-DesktopTest.ps1 -WithPack <id>` copies a host copy of that pack (the packs release asset, cached under `<worktrees>/_builds/packs/`, downloaded once) into the guest app's `LocalCache\packs\<id>` after the install, before the preflight; the guest stays offline. Then run `rtst-panel-screenshot` with `-WithPack supertonic-3`, crop the build watermark, convert to WebP at 362x525 and replace `site/img/control-panel.webp` (closes the Slice 194 Outstanding line).

7. **Duplicate self-tests stay.** `tools-selftests.yml` keeps running `test-quoting`/`test-timing` in both shells and `tests.yml` keeps its pwsh run: removing them from `tests.yml` would edit a `CORE_PATHS` file and ship every app. Nothing to build; recorded here.
8. **Say why the Downloading wait is safe.** A comment above each such wait: the flow's `clearState` removes the pack, so the download always starts; if a flow ever keeps the pack, wait for Downloading OR the ready state.
9. **Prove the dialog step.** Make the dismissal assert-visible-then-tap (not optional) in a flow where the dialog is known to show (first pack download on a fresh install), and keep it optional elsewhere; if `button2` is the wrong id, the flow fails and says so.
10. **Emulator crash cause.** When item 2's health check fires, `run-flows.ps1` saves `adb logcat -b crash -d` and `dumpsys meminfo` (host side: free RAM, the qemu process's working set) to the run's log folder, so the next death comes with evidence. Record what the 11:32 logs already show, if they still exist, in this file.
11. **One job, two shells.** `tools-selftests.yml` uses a `matrix: shell: [pwsh, powershell]` instead of two copies of the same eight steps (Slice 212 medium review, finding 5); add `test-activity.ps1` once.
12. **Line endings.** `lint-flows.ps1` rejects a flow with `\r\r` (doubled CRs) or mixed endings (2026-10-30: a fixer left doubled CRs in two flows, found only by reading them); fixture and test case.

## Not in scope (decided)

- The flow lint checks every flow on every run, not only changed ones: a broken flow anywhere should block, and it takes about a second (Slice 207 review, finding 6, declined 2026-10-30).
- `tests.yml` is not edited to add self-tests: it is in `CORE_PATHS`, so editing it ships every app (Slice 207).

## Verification

- The fast checks for this slice come from `tools/test-targets.ps1 -Checks` (its first use on a real slice; Slice 207 built it): `run-checks` runs exactly what it prints, and the report says whether it named every touched app and test project.

- VM: `rtst-panel-screenshot -WithPack supertonic-3` passes preflight and the shot shows the panel ready (no "Preparing the voice"); the site image is replaced.
  - **VM run 2026-10-31 (key ea0d0370b0e3):** preflight ok (pack copied, guest offline), rtst-launch and rtst-panel-screenshot PASS, but the shot (tmp/shots/control-panel.png) shows "Preparing the voice..." (read states Pending > GeneratingAudio), the Windows build watermark at the bottom, and no online state. Not ready: `site/img/control-panel.webp` NOT replaced. Likely the pack is present but the voice isn't loaded when the shot is taken (not proven); the check passes on the read state alone.
  - **VM run 2 2026-10-31 (same key, the check now waits up to 180 s):** FAIL, "the read never left GeneratingAudio" (waited 180 s), no PNG copied out, `site/img/control-panel.webp` NOT replaced. The offline guest with the copied supertonic-3 pack does not finish generating audio; cause not proven (needs the guest app log).

- Self-tests (lint, activity, lease, test-targets) pass in pwsh and PowerShell 5.1, the way CI runs them (`-command ". 'file'"`).
- Emulator: the two Farsi flows and the Norsk flow pass with the id; killing `system_server`
  (`adb shell su 0 kill <pid>` on the emulator) before a flow makes run-flows print `EMULATOR UNHEALTHY`, restart and pass.
  - **Emulator kill run 2026-10-31 (key ea0d0370b0e3):** killed `system_server` on emulator-5554, then `run-flows.ps1 -App rtst -Flow norsk-pack-reads`: printed `EMULATOR UNHEALTHY (before the install check): pm path android did not answer`, saved evidence, restarted (51.8 s), flow `norsk-pack-reads` PASS (84 s). Item verified.
## Built (night run 2026-10-30, not yet reviewed or run on a device)

All 12 items are built except the VM run of item 6 and the site image replacement, which need the VM and the coordinator.

- 1: `AutomationId="PackStatusLabel"` on the label; the two Farsi flows match `id: PackStatusLabel` plus the fa state text. No English flow and not the Norsk flow waits on the status text (Norsk waits on `LanguageContinue`), so there was nothing to switch there. Rule noted in `.maestro/README.md`.
- 2 and 10: `run-flows.ps1` checks health before every flow and after a failed one (`Get-EmulatorHealthProblem` in `common.ps1`), saves evidence under `maestro-out/i<n>/unhealthy-*`, restarts from the shared `.emulator` folder and reruns the flow once; the second unhealthy check exits 5. No logs of the 2026-10-30 11:32 death still exist (`maestro-out/i1` is empty), so nothing could be recorded from them.
- 3: `test-activity.ps1` (limit, override, VM plus one emulator, health parser), in the workflow. `-DeviceKillCheck` is the scripted emulator check for the `system_server` kill; it has not been run.
- 4 and 12: lint follows `runFlow` files, tokenizes the inline `launchApp`, accepts the `# clearState:` comment above the key or its item and ignores quoted text, treats `stopApp: true` as a restart, and rejects doubled CRs and mixed endings. Fixtures under `fixtures/lint`; the line-ending fixtures are written at test time because git would normalize checked-in ones.
- 5: `Invoke-DesktopTest.ps1` reads the `# needs:` headers once and prints the skip line once.
- 6 (tooling only): `Invoke-DesktopTest.ps1 -WithPack <id>` downloads the pack zip once to `<worktrees>/_builds/packs` (sha256 checked against the pinned `packs.json`, whose URL is read from `PackConstants.cs`), copies it into the guest and unpacks it as the unversioned layout in `LocalCache\packs\<id>`, after the install and before the preflight. Also defined the missing `$script:PackManifestFile` in `desktop-vm/_common.ps1` (the pack preflight read it but only `tools/phone/common.ps1` set it). **Still to do by the coordinator (VM, not run):** `tools\desktop-vm\Invoke-DesktopTest.ps1 -App rtst -Check rtst-panel-screenshot -WithPack supertonic-3 -CopyOut <folder>`, crop the build watermark, convert to WebP 362x525 and replace `site/img/control-panel.webp`.
- 7: nothing to build (recorded above). 8: a comment above each Downloading wait in the two Farsi flows. 9: `cancelled-voice-pack-stays-cancelled` asserts the `android:id/button2` sheet and its dismissal; `voice-cancel-holds-scan-pack` keeps it optional.
- 11: `tools-selftests.yml` is one `selftests` job with `matrix.shell: [pwsh, powershell]`; actionlint was not available here (NOT RUN).

## Review notes (medium pass)

- Fixed: unhealthy counter reset after a healthy rerun; bounded adb health probes (15 s); `button2` sheet wait kept optional; lint strips a trailing comment from `runFlow:` paths; `Restart-Emulator` reinstalls when the installed build key is stale. `PackStatusLabel` selectors rely on MAUI AutomationId reaching Maestro as resource-id (same as `SettingsButton`); not yet run on the emulator.
- Checked, no change: `tools-selftests.yml` matrix check names; the repo's only ruleset ("Protect") requires deletion/non_fast_forward only and main has no branch protection, so no required check names.
- Not fixed (outside Touches): `lint-flows.ps1` `Test-LineEndings` depends on autocrlf with no `.gitattributes` pin (`*.yaml text eol=...`); a `.gitattributes` line would pin it.

## Emulator run 2026-10-31 (key ea0d0370b0e3, emulator-5554, uncommitted)

- PASS `voice-cancel-holds-scan-pack` (Farsi, PackStatusLabel id), PASS `norsk-pack-reads`, PASS `cancelled-voice-pack-stays-cancelled` (with the optional Not now sheet).
- FAIL `sentence-highlight` (first flow after a reinstall; "Missing log marker SENTENCE 0 1"); not a PackStatusLabel flow, not rerun, cause not investigated.
- FAIL item 3 (kill check): after `su 0 kill <system_server pid>`, the next `run-flows.ps1 -App rtst -Flow voice-cancel-holds-scan-pack` threw "uk.sirous.readthestupidtext is not installed" (`cmd: Can't find service: package`) at run-flows.ps1:285. No `EMULATOR UNHEALTHY`, no restart: the install check (run-flows.ps1:~275-285) runs before the per-flow health check, so a dead system_server is reported as "not installed". Fix: run the health check (and restart) before the install/key check. Emulator left with package service down 1+ min after the kill.
- Not reached: `test-activity.ps1 -DeviceKillCheck`; recovery of the flow after restart.

## Changed while building (night run 2026-10-31)

- Device fix for the kill-check failure above: `run-flows.ps1` now runs the emulator health check before the install check; a package-service failure counts as unhealthy (`Resolve-InstallProblem`, with a self-test). Re-run: after killing `system_server`, run-flows printed `EMULATOR UNHEALTHY`, restarted in 52 s and the flow passed.
- `rtst-panel-screenshot` waits up to 180 s for the read to reach Reading/Read and fails with a clear message otherwise.
- Emulator: Farsi flows, Norsk flow and `cancelled-voice-pack-stays-cancelled` PASS. `sentence-highlight` failed once as the first flow after a fresh install ("Missing log marker SENTENCE 0 1"); not this slice's change, not rerun.
- VM: two runs, the sample read never leaves GeneratingAudio in the offline guest with the copied supertonic-3 pack (layout matches `PackVersionFolders`), so `site/img/control-panel.webp` was NOT replaced. Two strikes, patching stopped. Cause unproven; next step is to read the guest app log.

## From review

- High pass (`reviews\215-high.md`) and medium pass (`reviews\215-medium.md`) done and fixed as listed in the medium notes above.
- Medium finding 3 skipped: renaming the `tools-selftests.yml` matrix check names breaks nothing, since no ruleset requires the old check names.
- Medium finding 8 skipped: the `.gitattributes` line pin is outside this slice's Touches.

#### For CLAUDE.md (lifted 2026-10-31)

- `tools/CLAUDE.md`: `run-flows.ps1` checks emulator health (and restarts) before the install check, so a dead `system_server` is never reported as "not installed". `Invoke-DesktopTest.ps1 -WithPack <id>` copies a voice pack into the VM guest for checks that need one. (Slice 215: docs/plan/slices/215-device-guard-gaps.md)

#### Not yet verified

- Site image retake: the VM sample read never leaves GeneratingAudio with the copied pack; `site/img/control-panel.webp` not replaced. Read the guest app log first.
- `sentence-highlight` first-run failure after a fresh install ("Missing log marker SENTENCE 0 1"), not rerun.
- `test-activity.ps1 -DeviceKillCheck` script path not run (the kill was done by hand).
- Watermark crop of the site image not done.
