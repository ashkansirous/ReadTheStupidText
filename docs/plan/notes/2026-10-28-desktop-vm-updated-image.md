# Desktop VM: fully updated image, no update prompts, fail fast (2026-10-28)

*Built.* First real rebuild 2026-10-28: activated, 3 update rounds (7 updates, 0 failed), Windows Update read back off, `clean` taken; it stayed on 26300.9457 because Restart-VM hard resets skipped the cumulative update (fixed after that run; not yet re-run). Boot key Space hit Setup's Cancel (fixed to X; not yet re-run). Fix to the Slice 163 harness (`tools/desktop-vm/`), taking over Slice 175's `New-TestVm.ps1` items.

**Touches:** `tools/desktop-vm/New-TestVm.ps1`, `tools/desktop-vm/_common.ps1`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/desktop-vm/README.md`, `tools/desktop-vm/CLAUDE.md`, this note.

## What went wrong

Slice 175 attached a network adapter during the rebuild so `slmgr /ato` could start the evaluation's 90 days. With a
network, Windows Update downloaded updates and put an "install updates" prompt on the VM screen. The scripts never saw
it: the rebuild sat 30 minutes with no `clean` checkpoint and the wave coordinator kept waiting. The user found the
prompt in Hyper-V Manager (it happened once before, too: clicking Install by hand never reached the checkpoint).

## What to build

1. **Build step "online", between `Complete-Guest` and `Complete-Build`** (one adapter on `Default Switch`, named
   `BuildOnly`, removed in a `finally` before the checkpoint; the checkpoint and every run stay offline):
   - wait for an IPv4 address (as Slice 175's `Invoke-EvaluationActivation` did: `git show origin/feat/test-gaps-permanent-checks:tools/desktop-vm/New-TestVm.ps1` if pushed, else read `D:\ReadTheStupidText-worktrees\s175\tools\desktop-vm\New-TestVm.ps1`, read-only);
   - activate the evaluation (`slmgr /ato`); failure is a warning;
   - **install every pending Windows update on purpose, with no prompt**: Windows Update Agent COM API
     (`Microsoft.Update.Session`: search `IsInstalled=0 and IsHidden=0`, accept EULAs, download, install). The WUA API
     refuses remote callers, so run it inside the guest as a one-shot SYSTEM scheduled task that writes a JSON result
     (count found, installed, failed, reboot required), like `Invoke-InteractiveScript` does for checks. Loop: install,
     reboot when required (`Restart-VM`, then `Wait-Desktop` with the boot-time check), search again; stop when nothing
     is left or after 6 rounds; cap the whole step at `-UpdateTimeoutMin` (default 120). A progress line every minute
     (round, phase, updates count). `-SkipUpdates` skips the install (activation still runs).
   - then **turn Windows Update off for good** inside the guest: policies under
     `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate` (`DoNotConnectToWindowsUpdateInternetLocations=1`,
     `SetDisableUXWUAccess=1`, `DisableOSUpgrade=1`) and `...\WindowsUpdate\AU` (`NoAutoUpdate=1`, `AUOptions=1`);
     `wuauserv` and `UsoSvc` stopped and disabled; the `\Microsoft\Windows\UpdateOrchestrator\` and
     `\Microsoft\Windows\WindowsUpdate\` scheduled tasks disabled (best effort, each failure a warning);
     the "finish setting up your device" screen off (`HKCU\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement`
     `ScoobeSystemSettingEnabled=0`) and the Windows tips/suggestion toasts off.
   - **Read-back before the checkpoint** (after the settle reboot): the guest must show the update policies set and `wuauserv`
     Disabled and the VM no network adapter, else the build refuses to checkpoint. `UsoSvc` not Disabled is a warning only
     (it can be protected). Up to 3 tries, 20 s apart. If `Set-Service` fails the freeze sets the service's `Start` registry value to 4.
   - Search is `IsInstalled=0 and IsHidden=0 and Type='Software'` (no drivers; feature updates stay in). A dead guest task
     (not Running, no result for 2 polls) fails the round fast with a screenshot; the overall deadline ends the loop with a warning.
   - Print the guest's Windows version after updates (`DisplayVersion`, build and `UBR`), and again in the final "Done." line.
2. **Slice 175's ISO item:** after `clean` is taken, delete the ISO the script downloaded
   (`media\win11-ent-eval-en-us.iso`); `-KeepIso` keeps it; an `-IsoPath` file is never deleted.
3. **Fail fast with a picture, never wait on a screen nobody can answer** (`_common.ps1`):
   - `Save-VmScreenshot` from the host with no guest session (`Msvm_VirtualSystemManagementService.GetVirtualSystemThumbnailImage`,
     RGB565 at e.g. 1024x768, saved as PNG under `<VmRoot>\screens\<time>-<reason>.png`); returns the path.
   - Every wait (`Wait-Provisioned`, `Wait-Desktop`, the update loop, `New-VmSession`) gets a **stall limit**: no
     progress (same phase, no new boot, no new update count) for `-StallMin` (default 10) takes a screenshot and throws
     `"stalled: <what it waited for>; screen: <png path>"`. Total deadlines stay as today.
   - `Invoke-DesktopTest.ps1`: when a check returns no result or the harness throws, take a screenshot before restoring
     `clean` and print its path in the FAIL line.
4. **Docs:** README "Rebuild" section (online build step, updates installed then switched off, so the image is current
   as of its build date and frozen afterwards; the stall rule and where screenshots go; rebuild takes longer now, give the
   measured time). The README sentence "The image is never updated (no network)" is now wrong: replace it.
   `tools/desktop-vm/CLAUDE.md`: one rule line: never leave Windows Update on in `clean`, and never wait on a VM screen:
   stall = screenshot + throw.

## Constraints

- PowerShell 7, no elevation (Hyper-V Administrators only), no `vmconnect` or Manager window ever.
- Write-tool CR-byte gotcha in `tools/desktop-vm/CLAUDE.md`: check the bytes of every `.ps1` you write.
- Methods 3-30 lines, named constants, the existing style of these scripts.
- **Do not run `New-TestVm.ps1`** and do not touch the VM `rtst-desktop-test`: the coordinator runs the rebuild after review.
  Parse-check every script (`[System.Management.Automation.Language.Parser]::ParseFile`) and dot-source `New-TestVm.ps1`
  (it returns early when dot-sourced) to prove it loads.
- Confirm the WUA COM API and `GetVirtualSystemThumbnailImage` shapes from Microsoft Learn before coding (library-docs / web).

## Follow-up: update-only mode (`New-TestVm.ps1 -UpdateOnly`)

*Built 2026-10-28.* A monthly refresh must not rebuild from scratch (~55 min). `-UpdateOnly` brings the existing `clean` image up to date in ~15 minutes; a full rebuild (`-Force`) is needed only when the 90-day evaluation runs out.

1. Under the VM lock. Fail before touching anything when the VM or its `clean` checkpoint is missing ("run without -UpdateOnly").
2. Restore `clean`, start the VM, wait until the guest answers and the desktop is up.
3. Unfreeze for this step only: remove the `WindowsUpdate` policy values the freeze sets, set `wuauserv` (and `UsoSvc`) to Manual and start `wuauserv`.
4. `Invoke-OnlineBuild` as in a full build: attach `BuildOnly`, activate, update rounds with graceful restarts, adapter removed in `finally`.
5. `Disable-GuestWindowsUpdate`, the settling reboot and `Assert-WindowsUpdateOff`, exactly as a full build.
6. Replace the snapshot without ever losing the last good one: checkpoint as `clean-new`; only after that exists swap the names (rename `clean` to `clean-prev`, `clean-new` to `clean`) and remove `clean-prev` last, so a usable `clean` always exists. On any failure or Ctrl+C before the swap, the old `clean` is untouched (the VM is restored to it); a swap interrupted halfway is repaired by the next run (no `clean` with `clean-prev` and `clean-new`: finish the swap, the new image is complete; with only `clean-prev`: rename it back), and `Restore-CleanCheckpoint` names `-UpdateOnly` as the repair when `clean` is missing. Update failures or exhausted rounds do not stop the publish (a partly updated, verified image beats the old one) but are repeated as warnings in the final summary. `-UpdateOnly` also rejects `-SkipUpdates`; `-KeepIso` and `-InstallTimeoutMin` are ignored. `-UpdateOnly` refuses to publish when the guest got no network, the Default Switch is missing, or the update deadline passed.
7. Print the Windows version and the licence days (`Get-GuestLicense` / `Write-LicenseNote`); updating does not extend the evaluation.

Found while testing it: a search with exactly one pending update reported "found ," and installed nothing, because returning the one-item COM collection unrolled it to a bare update with no `Count`. `Search-Updates` now returns the search result. The full build of 2026-10-28 hit this in round 3; the update it missed was a Defender definitions update (KB2267602), installed by the first `-UpdateOnly` run.
