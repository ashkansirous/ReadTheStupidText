# Slice 448 — tools/phone runs the .maestro/dino flows on a real phone
**Product:** Shared
**Touches:** `tools/phone/**`, `tools/CLAUDE.md`

*Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 396; not built).*

## Why
`tools/phone` has only scripted checks (`checks/*.ps1`, `run-checks.ps1`) and no runner for the Maestro UI flows in `.maestro/dino`. Slice 396's S21 run was 0/4: all four flows stopped at FirstRunLanguage (evidence `reviews/s396-device/`, night 2026-11-17-2105). Phone runs therefore cannot check any flow-based "Not yet verified" line. Moved out of Slice 396 (its own work, items 1-7, is built).

## Work
- [ ] 1. A runner in `tools/phone` that runs a `.maestro/dino` (and `.maestro/rtst`) flow on the leased S21 (USB first), with the lease, the JDK pick (`Set-ProcessJavaHome`, `tools/emulator/common.ps1`) and the 10-minute stop of `tools/CLAUDE.md`.
- [ ] 2. Get past first run (FirstRunLanguage) the way the emulator's `run-flows.ps1` does, or by the app's Debug test hook.
- [ ] 3. Run Slice 396's four flows (`store-restore`, `paid-books`, `subscription`, `store-payment-requ...`) through it and report pass/fail.
- [ ] 4. Self-test script and a line in `tools/CLAUDE.md`.
