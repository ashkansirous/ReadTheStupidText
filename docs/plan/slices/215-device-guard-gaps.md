# Slice 215: close the gaps left by Slices 207 and 212

*Planned 2026-10-30.* Follow-up to Slices 207 and 212. Tooling, plus one app id.

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

## Not in scope (decided)

- The flow lint checks every flow on every run, not only changed ones: a broken flow anywhere should block, and it takes about a second (Slice 207 review, finding 6, declined 2026-10-30).
- `tests.yml` is not edited to add self-tests: it is in `CORE_PATHS`, so editing it ships every app (Slice 207).

## Verification

- VM: `rtst-panel-screenshot -WithPack supertonic-3` passes preflight and the shot shows the panel ready (no "Preparing the voice"); the site image is replaced.

- Self-tests (lint, activity, lease, test-targets) pass in pwsh and PowerShell 5.1, the way CI runs them (`-command ". 'file'"`).
- Emulator: the two Farsi flows and the Norsk flow pass with the id; killing `system_server`
  (`adb shell su 0 kill <pid>` on the emulator) before a flow makes run-flows print `EMULATOR UNHEALTHY`, restart and pass.
