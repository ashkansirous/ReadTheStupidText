# Slice 163 — Windows desktop tests in a headless Hyper-V VM

*Planned.* Batch 36 (fast device tests), RTST tray app and the Dino Reader Windows head. Decision 165.

**Touches:** `tools/desktop-vm/**` (new: `New-TestVm.ps1`, `Reset-TestVm.ps1`, `Invoke-DesktopTest.ps1`,
`checks/*.ps1`, `README.md`), `src/ReadTheStupidText.App/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`

## Why

Desktop acceptance tests run on the user's own screen: they install the dev package, take over the tray, the
hotkey and the clipboard, and leave apps running (2026-10-26). Decided with the user: **a Hyper-V VM** with no
window, driven through PowerShell Direct, reset to a clean checkpoint before every run. Windows Sandbox was
rejected because it opens a visible window and can only be scripted through a shared folder.

## Human step (only the user can do it)

- Turning on Hyper-V needs admin rights and a reboot. The guide comes one step at a time when the slice starts
  (`guide-step-by-step`). Everything after that is scripted.

## Work

- [ ] **Smallest end-to-end first:** `New-TestVm.ps1` creates a Windows 11 VM (the Microsoft evaluation VHD, or an
  ISO with an unattended answer file), 4 vCPU and 6 GB, Developer Mode on, a `clean` checkpoint.
  `Invoke-DesktopTest.ps1 -App rtst -Package <msix>` restores `clean`, copies the MSIX in with `Copy-VMFile` /
  `Copy-Item -ToSession`, installs it (`Add-AppxPackage -AllowUnsigned`; CI MSIX is unsigned, Decision 18), launches
  it, and checks the process is up and the activity log has its start line. Nothing appears on the host screen.
- [ ] Checks under `checks/`, each one a script returning pass/fail plus one line: tray icon present (UIA on the
  taskbar notification area inside the VM), hotkey reads the clipboard, control panel opens and the speed changes,
  startup task registered. Dino Windows head: launch, open a starter book, page turn.
- [ ] The VM user is signed in automatically (UIA and the tray need an interactive session). The eval image expires
  after 90 days, so `New-TestVm.ps1` rebuilds it from scratch; its README says when.
- [ ] Credentials for the VM's local account go through `manage-secrets` (generated, stored in `D:\secrets`).
- [ ] Rules in the App and Dino `CLAUDE.md`: desktop acceptance runs in the VM, never on the host desktop. Point
  `device-test` at `Invoke-DesktopTest.ps1` (ClaudeSkills PR).

## Verification

- With the user working on the host, `Invoke-DesktopTest.ps1 -App rtst` and `-App dino` pass, and nothing appears on
  the host screen, tray or clipboard.
- After a run, the VM is back at `clean` and the host has no dev package installed (`Get-AppxPackage *ReadTheStupid*`
  shows only the user's own install).
- A deliberately broken build (e.g. no tray icon) fails the matching check.
