# Slice 450 — Faster slow emulator flows (re-plan of Slice 301 item 3)
**Product:** Shared
**Touches:** `.maestro/dino/**`, `tools/emulator/**`

*Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 301; not built).*

Why: Slice 301 item 3 asked to shorten the waits the `PHASES` lines name, but 3 of its 5 flows were deleted by Slice 278, so the premise is wrong.

Work:
- [ ] 1. Run the emulator runner and read a fresh `PHASES` summary for the flows that exist on origin/main.
- [ ] 2. Pick the largest Wait phases (previously `read-back-reread-background`'s single `repeat` action, 82 s) and shorten each without losing the check.
- [ ] 3. Re-run those flows and compare phase times.
