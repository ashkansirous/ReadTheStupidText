# Slice 163 — Windows desktop tests in a headless Hyper-V VM

*Built 2026-10-28, not yet fully verified (see Not yet verified).* Batch 36 (fast device tests), RTST tray app and the Dino Reader Windows head. Decision 165.

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

- [x] **Smallest end-to-end first:** `New-TestVm.ps1` creates a Windows 11 VM (the Microsoft evaluation VHD, or an
  ISO with an unattended answer file), 4 vCPU and 6 GB, Developer Mode on, a `clean` checkpoint.
  `Invoke-DesktopTest.ps1 -App rtst -Package <msix>` restores `clean`, copies the MSIX in with `Copy-VMFile` /
  `Copy-Item -ToSession`, signs a copy with a throw-away certificate (subject = the manifest publisher, trusted inside the VM only; the CI MSIX is
  unsigned and `Add-AppxPackage -AllowUnsigned` refuses a Store publisher, Decision 18), installs it, launches
  it, and checks the process is up and the activity log has its start line. Nothing appears on the host screen.
- [x] Checks under `checks/`, each one a script returning pass/fail plus one line: tray icon present (UIA on the
  taskbar notification area inside the VM), hotkey reads the clipboard, control panel opens and the speed changes,
  startup task registered. Dino Windows head: launch, open a starter book, page turn.
- [x] The VM user is signed in automatically (UIA and the tray need an interactive session). The eval image expires
  after 90 days, so `New-TestVm.ps1` rebuilds it from scratch; its README says when.
- [x] Credentials for the VM's local account go through `manage-secrets` (generated, stored in `D:\secrets`).
- [x] Rules in the App and Dino `CLAUDE.md`: desktop acceptance runs in the VM, never on the host desktop.
- [ ] Point `device-test` at `Invoke-DesktopTest.ps1` (ClaudeSkills PR; follow-up, not part of this PR).

## Verification

- With the user working on the host, `Invoke-DesktopTest.ps1 -App rtst` and `-App dino` pass, and nothing appears on
  the host screen, tray or clipboard.
- After a run, the VM is back at `clean` and the host has no dev package installed (`Get-AppxPackage *ReadTheStupid*`
  shows only the user's own install).
- A deliberately broken build (e.g. no tray icon) fails the matching check.

*Changed while building:*
- The Win11 taskbar is invisible to UIA inside the VM, so the tray checks read the `NotifyIconSettings` registry, promote the icon and find a blue pixel in a screenshot of the tray strip.
- The CI MSIX is unsigned, so it is signed in the run with a throw-away certificate trusted only in the VM; the certificate and key are deleted on the host in the same call.
- The VM has no NIC. The VM user is the built-in Administrator (the answer file's LocalAccount came out as a standard user); the password is `DESKTOP_VM_PASSWORD` in `D:\secrets\readthestupidtext`.
- The hotkey check asserts the read starts (the VM has no audio device).
- VM files live under `D:\ReadTheStupidText-worktrees\_desktop-vm\` (`-VmRoot`).

*From review (reviews/163-review1.md, 163-review2.md):* host-wide mutex `Global\rtst-desktop-test-vm` across all three scripts; `Wait-Desktop` needs a new boot time plus 60 s stable and a fresh session per poll; ISO download validation; check timeouts of 240 s with task stop; answer ISO and Panther plaintext cleaned up.

*Verified in the VM:* rtst 5/5 and dino 3/3 pass; `-Sabotage KillApp` fails 4 checks with exit 1; nothing appeared on the host screen, tray or clipboard; the VM ends `Saved` at `clean`.

#### For CLAUDE.md

- Scripts written with the Write tool can get a backslash-r sequence (the two characters backslash and r inside a string) turned into a literal CR byte; check the bytes of any `tools/desktop-vm/*.ps1` you write. (Slice 163: docs/plan/slices/163-desktop-tests-in-hyperv-vm.md)

#### Not yet verified

- Selection plus hotkey path (only the clipboard path is checked).
- Full `New-TestVm -Force` rebuild end to end after the `Wait-Desktop` patch (the tail was run separately).
- The eval licence already reports expired with this ISO; it may shut down hourly, so a newer ISO may be needed.
- ClaudeSkills `device-test` pointer PR (follow-up).
