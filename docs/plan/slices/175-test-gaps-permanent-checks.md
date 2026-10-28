# Slice 175 — Close the test gaps: permanent checks, a Dino page hook, finer timing, the VM image

*In progress (wave 2026-10-28b), draft PR.* Batch 38 (test gaps found 2026-10-28), tooling for both apps; Debug-only hooks on Android and iOS.
After Slices 163 and 165.

**Touches:** `tools/desktop-vm/**`, `tools/phone/**`, `src/ReadTheStupidText.Mobile.Shared/Testing/**`,
`src/ReadTheStupidText.Mobile.Shared/Platforms/*/Testing/**`, `src/DinoReader.App/Testing/**` (new, the Dino
page hook handler, if the dispatcher can't open a book on its own),
`src/ReadTheStupidText.Application/Reading/ReadTimingTracker.cs` (Debug markers only), `.maestro/dino/**`

## Why

Checking every item Slices 163 and 165 left "unverified" (2026-10-28) showed that each one could be checked with
the VM and the phone. The checks ran as one-off scripts. This slice keeps them, so the next run doesn't depend on
someone rewriting them:

- Selection + hotkey passed in the VM through an ad-hoc check
  (`D:\ReadTheStupidText-worktrees\_desktop-vm\adhoc-selection-hotkey.ps1`): Notepad, select all, a decoy on the
  clipboard, Ctrl+Win+R, assert the `Hotkey` row holds the selection and not the decoy.
- No Debug hook opens a Dino book page, so phone timing reads a fixed passage, not a real page, and the emulator's
  `open-book` flow taps through the UI.
- The phone markers can't tell engine start-up from synthesis, so the Dino cold-start delay (Slice 177) can't be
  located.
- The 8 GB Windows ISO stays in `_desktop-vm\media` after the VM is built (a third of D:'s free space), although
  it's only needed for a rebuild.
- The first VM's evaluation licence reported "grace time expired" (0xC004F009) on its first day. The VM has no
  network adapter, so the evaluation may never activate; the rebuilt VM says 90 days but wasn't checked with `slmgr`.

## Work

- [ ] **Smallest first:** `checks/rtst-selection-hotkey.ps1` from the ad-hoc script, added to `$Apps.rtst.Checks`;
  the hotkey check stays for the clipboard path.
- [ ] Debug hook `open-book id=<starter id> [page=<n>]` in `TestHookDispatcher` (Android and iOS, compiled out of
  Release like the others), which opens the book on that page and can be followed by `read` / play. Dino's
  `audio-timing.ps1` reads page 1 of a starter book with it, and the Maestro `open-book` flow uses it instead of
  taps.
- [ ] Debug markers `ENGINE_READY <ms>` (engine created and model loaded) and `CHUNK_SYNTH n start|end <ms>`;
  `timing.ps1` adds an engine column and a synthesis column.
- [ ] `New-TestVm.ps1` deletes the ISO once the `clean` checkpoint is taken (`-KeepIso` keeps it) and downloads it
  again on the next rebuild; the README says so.
- [ ] Licence: read `slmgr /dlv` on the rebuilt VM. If an offline evaluation can't stay licensed, find out whether
  activating it needs a short-lived internal-only network, or whether a rebuild every N days is the answer;
  `Invoke-DesktopTest.ps1` prints the remaining days and warns under 14 (or when expired) with the rebuild command.
- [ ] `HOOK read fault` logs the exception's top stack frame, not only its type, so a fault is findable from the
  log (the whitespace NullReferenceException of Slice 176 had no location).

## Verification

- VM: `Invoke-DesktopTest.ps1 -App rtst` runs 6 checks, all pass; a build with the hotkey handler disabled fails
  the selection check. `New-TestVm.ps1 -Force` leaves no ISO behind and ends at `clean`.
- Phone: `audio-timing.ps1 -App dino` reads a real starter page at 1x and 2x and prints engine and synthesis
  columns; the emulator `open-book` flow passes using the hook.
- iOS: the hook and markers compile in a Debug iOS build (`-p:IncludeIos=true`); running them needs a Mac and is
  the only item allowed to stay unrun.
</content>
