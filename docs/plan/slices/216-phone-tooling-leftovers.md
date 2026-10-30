### Slice 216 — Phone tooling leftovers from the Slice 213 runs

*Planned 2026-10-30.* Findings from the 2026-10-30 S21 runs that no slice owned. Tooling only, except item 1 if it turns out to be an app bug.

**Touches:** `tools/phone/audio-timing.ps1`, `tools/phone/run-checks.ps1`, `tools/phone/common.ps1`, `tools/emulator/build-cache.ps1`, `tools/phone/README.md`, `tools/CLAUDE.md`, `src/ReadTheStupidText.Mobile.Shared/**` (only if item 1 is an app bug), `docs/plan/slices/216-phone-tooling-leftovers.md`

#### Work

1. **A build swap re-downloaded the voice pack.** On the S21 (11:19-11:20), installing the plain Debug APK over the AOT one was followed by `PACKDL start pack=supertonic-3 offset=0`: the pack was gone. Cause not proven; likely `audio-timing.ps1` ran with `-FreshInstall` or the two APKs are treated as different installs. The runner avoids it (`adb install -r`, packs checked after every install), but a user-side cause (an app update losing packs) would be serious.
   - [ ] Reproduce on the S21: AOT installed with packs, `adb install -r` the Debug APK, list `files/packs`. If the packs survive, the cause was the script's flags: fix or document them. If they don't, it is an app bug: find why the packs folder is cleared on an update and plan the fix (Android and iOS).
2. **`audio-timing.ps1` repeat runs are not cold.** Its second and later runs read 240-578 ms because the sentence cache was warm, and it labels them as cold starts.
   - [ ] Clear the sentence cache (as Slice 213's `cold-start` does) before every run that is reported as cold, or label the run "warm".
3. **Build path length.** The first parallel build failed with APT2264 (path too long) until the `--artifacts-path` folder names were shortened. A deeper worktree path can hit it again.
   - [ ] Before a build, compute the longest expected intermediate path (worktree path + artifacts folder + the deepest known MAUI intermediate) and fail early with "path too long: move the worktree or shorten <x>" instead of a build error minutes later. Self-test the check.
4. **`audio-timing.ps1` writes logs next to the repo** (`<repo parent>\.phone`), not in the shared worktrees folder (`Get-WorktreesRoot`) like the runner.
   - [ ] Use `Get-WorktreesRoot` for its output folder, like the runner.

#### Verification

- Fast: self-tests for items 2-4 in `pwsh` and Windows PowerShell 5.1.
- S21: item 1's reproduction; one `audio-timing.ps1 -Rates 1` run with two repeats labelled correctly.
