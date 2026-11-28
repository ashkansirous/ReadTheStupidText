# Slice 450 — Faster slow emulator flows (re-plan of Slice 301 item 3)
**Product:** Shared
**Touches:** `.maestro/dino/**`, `tools/emulator/**`

*Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 301; not built).*

*Plan-cleanup check 2026-11-28: keep open, not started; all three items are own work and nothing waits on an outside event. Checked on origin/main: `PHASES` is still printed by `tools/emulator/run-flows.ps1` and `speed.ps1`. The Android `dino/read-back-reread-background` flow was deleted (`.maestro/README.md` line 36, Slice 278 part B), so item 2's named example no longer exists; only the iOS simulator variants (`ios-read-back-reread-background`, `ios-read-audio`) remain, and the emulator runner does not run them. Item 2 therefore picks the largest Wait phases from item 1's fresh run, not from that flow.*

Why: Slice 301 item 3 asked to shorten the waits the `PHASES` lines name, but 3 of its 5 flows were deleted by Slice 278, so the premise is wrong.

Work:
- [ ] 1. Run the emulator runner and read a fresh `PHASES` summary for the flows that exist on origin/main.
- [ ] 2. Pick the largest Wait phases (previously `read-back-reread-background`'s single `repeat` action, 82 s) and shorten each without losing the check.
- [ ] 3. Re-run those flows and compare phase times.
