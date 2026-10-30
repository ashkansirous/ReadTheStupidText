# Slice 212: hooks reach the running app, and the tool self-tests run in CI

*In progress (wave 2026-10-30), draft PR.*

**Why:** `.maestro/rtst/cancelled-voice-pack-stays-cancelled.yaml` (Slice 204) has never passed on the emulator. Its
`launchApp` with `arguments: {cmd: cancel-pack-downloads}` stops the running app and cold-starts a new one (maestro.log:
"Stopping app during launch"); the cold start takes ~9 s, and the flow's `stopApp` two seconds later kills it before
`HOOKS installed`. No `HOOK cancel-pack-downloads` line is ever logged, no cancel is saved, the download resumes by itself
and "Download stopped" never shows. The app is not at fault (Slice 204 passed on the S21 by script). Separately, the
Slice 207 self-tests (`test-lint-flows.ps1`, `test-lease.ps1`, `test-test-targets.ps1`) run only by hand: editing
`tests.yml` would ship every app (it is in `CORE_PATHS`, `.github/scripts/ship-targets.mjs`), but a new workflow
file is in no build group.

**Touches:** `.maestro/**`, `tools/emulator/lint-flows.ps1`, `tools/emulator/test-lint-flows.ps1`,
`tools/emulator/fixtures/**`, `tools/emulator/README.md`, `.maestro/README.md`, `.github/workflows/tools-selftests.yml`,
`.github/CLAUDE.md`, `tools/CLAUDE.md`

**Platforms:** test tooling and CI only, no app code.

## Design (settled; build exactly this)

1. **A hook sent to a running app must not restart it.** Confirm with `library-docs` (Maestro) that
   `launchApp: {stopApp: false, arguments: {...}}` delivers the arguments to the running activity without a restart
   (Android: a new intent, handled by `OnNewIntent` in `TestHooks.Android.cs`). If it does, every `launchApp` that sends a
   hook after the app is already running uses `stopApp: false`. If it doesn't, use the hook's adb broadcast
   (`uk.sirous.rtst-test.TEST_HOOK`, `Get-HookAction` in `tools/phone/common.ps1`) through Maestro's `runScript`/
   another documented way, and say which in the PR.
2. **Wait for the hook's effect before the next step.** In `cancelled-voice-pack-stays-cancelled.yaml`, after the cancel,
   `extendedWaitUntil` "Download stopped.*" is visible (proves the cancel was saved and Stopped published) before `stopApp`.
   Replace the 2 s `waitForAnimationToEnd` there. Also make sure the download has actually started before the cancel
   (wait for "Downloading natural voices.*").
3. **Lint rule** in `lint-flows.ps1`: a `launchApp` that carries `arguments` with a `cmd` and is not the flow's first
   `launchApp` must have `stopApp: false` (or a `# launchApp-restart:` comment above it saying why a restart is wanted).
   Fixture and test case for both. Fix every existing flow the rule flags (each change judged: a deliberate restart gets
   the comment, a hook to the running app gets `stopApp: false`).
4. **`.github/workflows/tools-selftests.yml`**: `on: pull_request` only, `paths: ['tools/**', '.maestro/**',
   '.github/workflows/tools-selftests.yml']`; one `windows-latest` job (the scripts are PowerShell and some need
   Windows paths) running `test-lint-flows.ps1`, `lint-flows.ps1`, `test-lease.ps1`, `tools/test-test-targets.ps1` (it
   needs node: set it up), and the phone `test-phone-lib.ps1`/`test-tap.ps1` if they need no device. No
   `upload-artifact`, a 10-minute timeout, no `on: push` (`release.yml` is the only push workflow). Don't add it to
   `ship-targets.mjs` `GROUPS`/`PR_TARGETS`. Note the workflow in `.github/CLAUDE.md` and remove "not in CI" from
   `tools/CLAUDE.md` / READMEs.

## Verification

- Self-tests pass in pwsh and PowerShell 5.1; lint clean on `.maestro`.
- The new workflow runs green on this PR (it touches `tools/**`), and `ship-targets` reports no app targets for it.
- Emulator: `cancelled-voice-pack-stays-cancelled.yaml` passes, with `HOOK cancel-pack-downloads ok` and
  `VOICEPACK held-user-cancelled fa` in the log.
