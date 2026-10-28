# CLAUDE.md — tools

Rules for the device-test tooling (`tools/emulator`, `tools/phone`, `tools/desktop-vm`; the VM scripts have their own file).

- Start device checks from `run-flows.ps1 -App auto` / `Invoke-DesktopTest.ps1 -App auto` (they test only the apps the diff touches); never drop an app it names.
- Builds are cached by input hash in `<worktrees>/_builds` and run outside the device locks; the enforced limit is one emulator plus the VM, or two emulators.
- Phone runs need the phone charging; for network it must be unlocked once with the `.dbg` app in the foreground.

(Slice 180: docs/plan/slices/180-faster-device-tests.md)
