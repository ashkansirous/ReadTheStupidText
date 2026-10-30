# Slice 212: hooks reach the running app, and the tool self-tests run in CI

*Built 2026-10-30. Verified on the emulator and in CI.*

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
`.github/CLAUDE.md`, `tools/CLAUDE.md`, `tools/emulator/run-flows.ps1`

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

## Result

**Changed while building:**

- Hooks into a running app use `launchApp` with `stopApp: false` (Maestro docs: the default restarts the app). Deliberate restarts carry a `# launchApp-restart:` comment; `lint-flows.ps1` enforces it in plain, inline and block forms.
- `tools-selftests.yml` is PR-only with two jobs (pwsh and Windows PowerShell 5.1), one step per script, and is not in the `ship-targets` groups.
- Found on the emulator and fixed: the two `fa` flows asserted English text under a Farsi UI (now a `fa` regex; `PackStatusLabel` has no AutomationId); the Slice 183 locked-download dialog is dismissed via `android:id/button2`; the scan-pack `OCRPACK` marker is written at app start, so its `state` hook keeps a restart; doubled `\r\r\n` line endings from a fixer were removed.
- `run-flows.ps1` now applies `# needs: cellular-only` (Wi-Fi off, restored in `finally`; lint rejects unknown needs) and wakes and dismisses the keyguard before each flow.

**From review:** high pass, 7 applied (CI masked exit codes, lint forms, comment on `stopApp`, scan-pack race, speed restart kept, docs, 5.1 job), 2 skipped: finding 7 (the first-run restart reasons are asserted, not shown; kept, and both first-run flows pass with the restart) and finding 8 (the slice file did not record the Maestro check; recorded under Changed while building). High finding 3 (`stopApp: false # why`) was fixed. Medium pass, no blockers; 1-3 were checked by the emulator runs, 4 is Slice 215 item 8, 5 is item 11, 6-8 are item 4, 9 (CRLF in the working copy) is normalised by git on commit.

**Emulator:** `cancelled-voice-pack-stays-cancelled` PASS (`VOICEPACK held-user-cancelled fa`), `voice-cancel-holds-scan-pack` PASS (`OCRPACK held-voice-cancelled fa`), and `speed`, `store-credits-language`, `open-book`, `download-now-tap` (Wi-Fi off, `wifi_on=1` after), `grown-ups-button`, `default-view-per-book` PASS. The emulator's system_server died once mid-run and was restarted (not app related).

#### For CLAUDE.md (lifted 2026-10-30)

Already written in `tools/CLAUDE.md` and `.github/CLAUDE.md` in this slice's diff; nothing further to lift.

#### Not yet verified

- ~~The new `tools-selftests.yml` workflow's first run (this PR).~~ Green on PR #600 in both shells, after the self-tests got an explicit `exit 0`.
- ~~The first-run flows (rtst and dino) and `norsk-pack-reads` were not rerun.~~ First-run flows pass; `norsk-pack-reads` was broken (no scroll, English text on a Norwegian screen), fixed and passing in PR #603.
