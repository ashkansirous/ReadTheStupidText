# Desktop acceptance tests in a headless Hyper-V VM (Slice 163)

Windows desktop checks (the RTST tray app, the Dino Reader Windows head) run inside a Windows 11 VM that has no
window and no network adapter (once built), are reset to a `clean` checkpoint before and after every run, and never touch the host
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

- `New-TestVm.ps1` builds the VM from nothing (`-Force` rebuilds, `-UpdateOnly` refreshes). `Reset-TestVm.ps1 [-Start]` restores `clean`.
- `Invoke-DesktopTest.ps1` is the runner; `_common.ps1` holds the shared code; `autounattend.template.xml` is the answer file.
- `checks/*.ps1` run inside the VM in the signed-in desktop session (a PowerShell Direct session is a separate,
  non-interactive logon, so each check runs as a one-shot scheduled task). Each writes `{pass,message}`; `_lib.ps1` has
  the helpers (UIA lookups, tray icon, mouse and keys, log readers). Order matters: later checks use the running app.
  RTST: `rtst-launch`, `rtst-tray-icon`, `rtst-hotkey-read` (clipboard path), `rtst-selection-hotkey` (Notepad selection beats a clipboard decoy), `rtst-panel-speed`, `rtst-startup-task`.
  Dino: `dino-launch`, `dino-open-book`, `dino-page-turn`. Add a check = add the file and its name to `$Apps` in the runner.

## Where things live

Everything big is under `D:\ReadTheStupidText-worktrees\_desktop-vm\` (`-VmRoot`): `media\` (evaluation ISO, about 8 GB,
deleted after a build unless `-KeepIso`; the next build downloads it again), `screens\` (stall and failure screenshots), `vm\` (VHDX and checkpoint), `pkgs\`
(built MSIX). The VM account is `Administrator` (signed in automatically); its password is generated and stored as
`DESKTOP_VM_PASSWORD` in `D:\secrets\readthestupidtext\secrets.env` (manage-secrets), or set `RTST_VM_PASSWORD`.
Needs Hyper-V on and your user in *Hyper-V Administrators*; no elevation, and no `vmconnect` or Manager window is ever opened.

## Rebuild: when and how

**Monthly refresh: `New-TestVm.ps1 -UpdateOnly`** (about 15 minutes, no ISO, VM kept). It restores `clean`, lifts the Windows
Update freeze for that run, installs the pending updates exactly like a full build, freezes again, takes `clean-new` and
only then swaps the names (`clean` to `clean-prev`, `clean-new` to `clean`, both instant) and deletes `clean-prev` last, so a usable
`clean` always exists; any failure or Ctrl+C before the swap restores the old `clean`, and it refuses to publish when the guest
got no network or `-UpdateTimeoutMin` ran out. A failed update or exhausted rounds still publish (the image is better than the old one) but print a warning in the final summary. An interrupted swap is repaired by the next run of `-UpdateOnly`; until then `Reset-TestVm.ps1` and the tests say so when `clean` is missing.
It does not extend the 90-day evaluation (the licence days are printed at the end), and cannot be combined with `-Force`, `-IsoPath`
or `-SkipUpdates`; `-KeepIso` and `-InstallTimeoutMin` have no effect with it. Use `-Force` only when the licence runs out or the VM is damaged.

The VM runs the Windows 11 Enterprise **evaluation** (90 days; the host fetches the current ISO from the Microsoft Evaluation
Center page). `Invoke-DesktopTest.ps1` prints the days left and warns under 14 days, or when the licence is not counting, with the rebuild command. Rebuild with `New-TestVm.ps1 -Force` (about 55 minutes, unattended) only when: the evaluation lapses (Windows shows
"license expired"; short runs still work, but it begins shutting the VM down hourly), Windows asks for something the
checks cannot answer, or the VM is damaged.

The build has one online step (a network adapter named `BuildOnly` on `Default Switch`, removed before the checkpoint): it
activates the evaluation, then installs **every pending Windows update on purpose** with the Windows Update Agent API (run in
the guest as a SYSTEM task, rebooting and searching again, at most 6 rounds, `-UpdateTimeoutMin` 120 in all; `-SkipUpdates`
skips it), then **turns Windows Update off for good** (policies, services, scheduled tasks, the "finish setting up your
device" screen and tips). So the image is current as of its build date and frozen afterwards: UI positions stay stable and no
"install updates" prompt can appear. Before the checkpoint the build **reads the freeze back** from the rebooted guest (update
policies set, `wuauserv` disabled, no network adapter; `UsoSvc` still enabled is only a warning; 3 tries, 20 s apart) and
refuses to checkpoint otherwise. The update search is software only (no drivers; feature updates are included). If
`-UpdateTimeoutMin` runs out the build warns and carries on to the freeze. The checkpoint and every run stay offline. The script prints the guest's Windows version
after the updates and in its final "Done." line. A rebuild with updates took about 55 minutes on 2026-10-28 (Setup about
13, activation and 3 update rounds about 40). After the checkpoint the downloaded ISO is deleted (`-KeepIso` keeps it; an
`-IsoPath` file is never deleted).

Every reboot during the build is a **Windows restart from inside the guest** (`Restart-Computer`), never `Restart-VM`: a
cumulative update finishes while Windows shuts down, and a hard reset skipped that, so the update reported "installed" twice
and the build number never moved. The "Press any key to boot from CD or DVD" prompt is answered with **X**, not Space: late
keys reach Setup's window, where Space pressed the focused Cancel button and Setup sat on "Are you sure you want to quit?".

**Stalls fail fast with a picture.** Every wait (first-logon provisioning, the rebooted desktop, the update rounds, opening a
session) watches for progress (power state, heartbeat, the disk growing, the update status, a new boot). No change for
`-StallMin` minutes (default 10) saves the VM screen as `<VmRoot>\screens\<time>-<reason>.png` from the host (no guest session
needed) and throws `stalled: <what it waited for>; screen: <png path>`. `Invoke-DesktopTest.ps1` does the same when a check
returns no result or the harness throws: the screenshot path is printed in the FAIL line, before `clean` is restored.

## Gotchas found while building it

- The new built-in-user route is `Administrator`: a `LocalAccount` created by the answer file ended up a standard user,
  so the first-logon commands (Developer Mode, policies) could not write HKLM.
- The Win11 taskbar is invisible to UI Automation here. The tray check therefore asks Windows for the notification-icon
  record (`HKCU\Control Panel\NotifyIconSettings`), promotes the icon onto the taskbar, and finds it by its own blue in a
  screenshot of the tray strip; the panel check clicks that spot. A tray icon that moves or changes colour fails the check.
- No audio device exists, so the hotkey check asserts that a Hotkey row carrying the sentinel text reaches the input log (and reports the states seen), not that the read ends.
- First run of each app shows a language page; the launch checks answer it (English, no network, phone/Windows voice).
- A killed app leaves a ghost tray icon, so each tray check also requires the process to be alive.
