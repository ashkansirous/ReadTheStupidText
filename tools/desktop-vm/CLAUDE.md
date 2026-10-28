# CLAUDE.md — tools/desktop-vm

Rules for the Hyper-V VM desktop test scripts.

- Scripts written with the Write tool can get a backslash-r sequence (the two characters backslash and r inside a string) turned into a literal CR byte; check the bytes of any `tools/desktop-vm/*.ps1` you write. (Slice 163: docs/plan/slices/163-desktop-tests-in-hyperv-vm.md)
