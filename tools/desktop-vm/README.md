# Desktop acceptance tests in a headless Hyper-V VM (Slice 163)

Windows desktop checks (the RTST tray app, the Dino Reader Windows head) run inside a Windows 11 VM that has no
window and no network adapter, are reset to a `clean` checkpoint before and after every run, and never touch the host
desktop, tray, clipboard or installed packages. Decision 165, `docs/plan/slices/163-desktop-tests-in-hyperv-vm.md`.

## Use

```powershell
# 1. Build the unsigned dev MSIX (same commands as CI; output outside the repo)
msbuild src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -restore -p:Configuration=Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 `
  -p:GenerateAppxPackageOnBuild=true "-p:AppxPackageDir=D:\ReadTheStupidText-worktrees\_desktop-vm\pkgs\rtst\\" `
  -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxBundle=Never -p:AppxPackageSigningEnabled=false
dotnet publish src/DinoReader.App -f net10.0-windows10.0.19041.0 -c Release -p:IncludeAndroid=false -p:Platform=x64 -p:AppxPackageSigningEnabled=false
#    (Dino writes to src/DinoReader.App/AppPackages/; copy that folder to ...\_desktop-vm\pkgs\dino\)

# 2. Run (exit code 0 = every check passed; prints one PASS/FAIL line per check)
tools\desktop-vm\Invoke-DesktopTest.ps1 -App rtst          # or  -App dino   [-Package <msix>]
tools\desktop-vm\Invoke-DesktopTest.ps1 -App rtst -Sabotage KillApp   # must FAIL: proves a check can fail
```

`Invoke-DesktopTest.ps1` finds the newest MSIX under `<VmRoot>\pkgs\<app>` unless `-Package` is given. It restores
`clean`, starts the VM, signs a **copy** of the MSIX with a throw-away certificate (subject = the manifest publisher; the
CI MSIX is unsigned and a Store publisher is not in the "unsigned namespace", so `-AllowUnsigned` refuses it), trusts
that certificate inside the VM only, installs the package with its `Dependencies\x64` framework MSIX, runs the checks,
and restores `clean` again (the VM is left in the `Saved` state; nothing keeps running). On the host the throw-away
certificate lives in `Cert:\CurrentUser\My` for a moment and is removed with its private key in the same call; the
signed copy sits in `<VmRoot>\sign` and is deleted in a `finally`. Runs take a host-wide lock (a second run prints
"VM busy" and waits). Every long wait prints a progress line; Ctrl+C stops any script.

## Files

- `New-TestVm.ps1` builds the VM from nothing (`-Force` rebuilds). `Reset-TestVm.ps1 [-Start]` restores `clean`.
- `Invoke-DesktopTest.ps1` is the runner; `_common.ps1` holds the shared code; `autounattend.template.xml` is the answer file.
- `checks/*.ps1` run inside the VM in the signed-in desktop session (a PowerShell Direct session is a separate,
  non-interactive logon, so each check runs as a one-shot scheduled task). Each writes `{pass,message}`; `_lib.ps1` has
  the helpers (UIA lookups, tray icon, mouse and keys, log readers). Order matters: later checks use the running app.
  RTST: `rtst-launch`, `rtst-tray-icon`, `rtst-hotkey-read` (clipboard path), `rtst-selection-hotkey` (Notepad selection beats a clipboard decoy), `rtst-panel-speed`, `rtst-startup-task`.
  Dino: `dino-launch`, `dino-open-book`, `dino-page-turn`. Add a check = add the file and its name to `$Apps` in the runner.

## Where things live

Everything big is under `D:\ReadTheStupidText-worktrees\_desktop-vm\` (`-VmRoot`): `media\` (evaluation ISO, about 8 GB;
delete it after a build if space is short, the next build downloads it again), `vm\` (VHDX and checkpoint), `pkgs\`
(built MSIX). The VM account is `Administrator` (signed in automatically); its password is generated and stored as
`DESKTOP_VM_PASSWORD` in `D:\secrets\readthestupidtext\secrets.env` (manage-secrets), or set `RTST_VM_PASSWORD`.
Needs Hyper-V on and your user in *Hyper-V Administrators*; no elevation, and no `vmconnect` or Manager window is ever opened.

## Rebuild: when and how

The VM runs the Windows 11 Enterprise **evaluation** (90 days; the host fetches the current ISO from the Microsoft Evaluation
Center page). `Invoke-DesktopTest.ps1` prints the days left and warns under 14 days, or when the licence is not counting, with the rebuild command. Rebuild with `New-TestVm.ps1 -Force` (15-30 minutes, unattended) when: the evaluation lapses (Windows shows
"license expired"; short runs still work, but it begins shutting the VM down hourly), Windows asks for something the
checks cannot answer, or the VM is damaged. The image is never updated (no network), so UI positions stay stable.

## Gotchas found while building it

- The new built-in-user route is `Administrator`: a `LocalAccount` created by the answer file ended up a standard user,
  so the first-logon commands (Developer Mode, policies) could not write HKLM.
- The Win11 taskbar is invisible to UI Automation here. The tray check therefore asks Windows for the notification-icon
  record (`HKCU\Control Panel\NotifyIconSettings`), promotes the icon onto the taskbar, and finds it by its own blue in a
  screenshot of the tray strip; the panel check clicks that spot. A tray icon that moves or changes colour fails the check.
- No audio device exists, so the hotkey check asserts that a Hotkey row carrying the sentinel text reaches the input log (and reports the states seen), not that the read ends.
- First run of each app shows a language page; the launch checks answer it (English, no network, phone/Windows voice).
- A killed app leaves a ghost tray icon, so each tray check also requires the process to be alive.
