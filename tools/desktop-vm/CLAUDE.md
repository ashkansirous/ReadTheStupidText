# CLAUDE.md — tools/desktop-vm

Rules for the Hyper-V VM desktop test scripts.

- Scripts written with the Write tool can get a backslash-r sequence (the two characters backslash and r inside a string) turned into a literal CR byte; check the bytes of any `tools/desktop-vm/*.ps1` you write. (Slice 163: docs/plan/slices/163-desktop-tests-in-hyperv-vm.md)
- Never leave Windows Update on in `clean` (its "install updates" prompt stalls every run), and never wait on a VM screen nobody can answer: a wait with no progress for `-StallMin` takes a screenshot (`Save-VmScreenshot`) and throws `stalled: ...; screen: <png>`. (docs/plan/notes/2026-10-28-desktop-vm-updated-image.md)
- The MSIX under test is found by build key (`Build-TestPackage.ps1`, `<worktrees>/_builds/`), never as "the newest file under `pkgs`"; the build takes no VM lock (only install and checks hold the VM). `-App auto` runs only the touched apps (`tools/test-targets.ps1`). (Slice 180: docs/plan/slices/180-faster-device-tests.md)
- Monthly refresh is `New-TestVm.ps1 -UpdateOnly` (restores `clean`, updates, swaps the checkpoint, never leaves the VM without a `clean`); `-Force` only when the licence runs out or the VM is damaged. (docs/plan/notes/2026-10-28-desktop-vm-updated-image.md)
