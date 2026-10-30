### Slice 188 — Dino Settings behind the grown-up gate as a permanent VM check

*Built (wave 2026-10-30), PR #585.* *Planned 2026-10-29.* Follow-up of Slice 172 ("make `dino-settings.ps1` a permanent VM check").

**Touches:** `tools/desktop-vm/checks/dino-settings.ps1` (new), `tools/desktop-vm/checks/_lib.ps1`, `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/desktop-vm/README.md`, `tools/desktop-vm/CLAUDE.md`, `docs/plan/slices/188-dino-settings-vm-check.md`.

#### Why

The 2026-10-28 VM run passed the Dino Windows Settings flow (touch injection and Narrator, Slice 172 file), but its script only exists in an ad-hoc worktree outside the repo (`D:\ReadTheStupidText-worktrees\_desktop-vm\adhoc-s172\checks\dino-settings.ps1`), so it can be lost and never runs again. The Dino profile's checks today are `dino-launch`, `dino-open-book`, `dino-page-turn` (`Invoke-DesktopTest.ps1:44`).

The gate (`GatePage`, `HoldTracker.GateHold` = 3 s, then a sum) has no test bypass, and it should stay that way: the check is worth more if it passes the real gate.

#### Decisions (Claude, 2026-10-29)

- Pass the real gate: a mouse press (`Invoke-PressAndHold`, `mouse_event`; chosen over touch injection, which Slice 172's run used, and not yet shown on the VM) held for 3.5 s on the "Press and hold for 3 seconds" ring, then read the sum and type the answer. No Debug bypass of the gate.
- If touch injection proves flaky in three runs, fall back to the gate's screen-reader alternative (`ShowHoldAlternative`), which the run already used with Narrator; never add a skip.

#### Note

The Why and Decisions above predate Slice 189 (they describe the shelf gear opening the gate). The script follows 189's flow: shelf "For grown-ups" button -> gate -> grown-up mode -> gear -> Settings (no second gate). Review fix pass 2026-10-30: exact-name lookups, one `Win32.Mouse` type, return-path assertions.

#### Work

- [x] Copy the ad-hoc script into `tools/desktop-vm/checks/dino-settings.ps1`, rewritten on the `_lib.ps1` helpers (move the press-and-hold injection into `_lib.ps1` if it is generic).
- [x] Assert (Slice 189 flow): the "For grown-ups" button opens the gate; hold and sum open grown-up mode; its gear opens Settings; Settings shows its groups; Back returns to grown-up mode; Done returns to the kids' shelf. Written, not yet run on the VM.
- [x] Add `dino-settings` to `$Profiles.dino.Checks` after `dino-page-turn`.
- [x] README and `tools/desktop-vm/CLAUDE.md`: the new check and the injection gotcha, if any.

#### Verification

- Three runs in a row of `Invoke-DesktopTest.ps1` for the Dino profile on the VM from the existing snapshot (no rebuild), all checks pass.

#### Not yet verified

- dino-page-turn (existing check) failed in all 3 VM runs: no StoryText element with a name — pre-existing, follow-up.
- Reading speed row navigation on the Windows head by UIA (the row is a Grid with a tap gesture; neither a label click nor a pane-centre click opened it on the VM)
- dino-page-turn existing check broken

#### Verified (2026-10-30)
- VM (cached MSIX 77dc56dc4cec, snapshot restored each run): `dino-settings` PASS in 3 of 3 runs in a row. Gate passed by a real mouse hold plus the sum (answers 14, 22, 35); Settings shows READING and THIS PHONE; Back returns to grown-up mode; Done returns to the kids' shelf. "For grown-ups" is pressed through UIA with one retry after 5 s (one earlier run saw only the tooltip).
