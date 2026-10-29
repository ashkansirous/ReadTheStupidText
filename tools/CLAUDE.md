# CLAUDE.md — tools

Rules for the device-test tooling (`tools/emulator`, `tools/phone`, `tools/desktop-vm`; the VM scripts have their own file).

- Start device checks from `run-flows.ps1 -App auto` / `Invoke-DesktopTest.ps1 -App auto` (they test only the apps the diff touches); never drop an app it names.
- Builds are cached by input hash in `<worktrees>/_builds` and run outside the device locks; the enforced limit is one emulator plus the VM, or two emulators.
- Phone runs need the phone charging; for network it must be unlocked once with the `.dbg` app in the foreground.
- Unattended phone runs (nobody to unlock) need the screen lock set to None or Swipe: starting a pack download, opening a book or a Settings page needs the app visible. Ask the user before they leave. Language, voice and Translate switch after first run through the app's Settings by uiautomator taps (no hook exists yet); never report them "no hook". Force a download by moving the pack aside and restoring it, never deleting it; one harness script per device at a time. (2026-10-29 S21 night: docs/plan/notes/2026-10-29-s21-night-runs.md)
- Maestro flows that need a pushed file live under `.maestro/<app>/needs-file/`, so `run-flows.ps1` skips them; run them by hand after `adb push`.

(Slice 185: docs/plan/slices/185-close-three-old-gaps.md)
(Slice 180: docs/plan/slices/180-faster-device-tests.md)
