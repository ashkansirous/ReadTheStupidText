### Slice 188 — Dino Settings behind the grown-up gate as a permanent VM check

*Planned 2026-10-29.* Follow-up of Slice 172 ("make `dino-settings.ps1` a permanent VM check").

**Touches:** `tools/desktop-vm/checks/dino-settings.ps1` (new), `tools/desktop-vm/checks/_lib.ps1`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/desktop-vm/README.md`, `tools/desktop-vm/CLAUDE.md`, `docs/plan/slices/188-dino-settings-vm-check.md`.

#### Why

The 2026-10-28 VM run passed the Dino Windows Settings flow (touch injection and Narrator, Slice 172 file), but its script only exists in an ad-hoc worktree outside the repo (`D:\ReadTheStupidText-worktrees\_desktop-vm\adhoc-s172\checks\dino-settings.ps1`), so it can be lost and never runs again. The Dino profile's checks today are `dino-launch`, `dino-open-book`, `dino-page-turn` (`Invoke-DesktopTest.ps1:44`).

The gate (`GatePage`, `HoldTracker.GateHold` = 3 s, then a sum) has no test bypass, and it should stay that way: the check is worth more if it passes the real gate.

#### Decisions (Claude, 2026-10-29)

- Pass the real gate: a touch-injection press held for 3.5 s on the "Press and hold for 3 seconds" ring, then read the sum and type the answer. No Debug bypass of the gate.
- If touch injection proves flaky in three runs, fall back to the gate's screen-reader alternative (`ShowHoldAlternative`), which the run already used with Narrator; never add a skip.

#### Work

- [ ] Copy the ad-hoc script into `tools/desktop-vm/checks/dino-settings.ps1`, rewritten on the `_lib.ps1` helpers (move the press-and-hold injection into `_lib.ps1` if it is generic).
- [ ] Assert: the gear opens the gate; the hold and sum open Settings; the Settings page shows its sections; Back returns to the shelf.
- [ ] Add `dino-settings` to `$Profiles.dino.Checks` after `dino-page-turn`.
- [ ] README and `tools/desktop-vm/CLAUDE.md`: the new check and the injection gotcha, if any.

#### Verification

- Three runs in a row of `Invoke-DesktopTest.ps1` for the Dino profile on the VM from the existing snapshot (no rebuild), all checks pass.
