# Slice 451 — A Debug hook that makes the user-report endpoint refuse
**Product:** Shared
**Touches:** `src/Dino/DinoReader.App/Testing/**` (a new partial file; never `Testing/DinoTestCommands.cs`, which parked draft PR #1430's worktree edits), `.maestro/dino/**`, `docs/plan/unverified.md` (the Slice 300 line this closes), this slice file

*In progress (wave 2026-11-27), draft PR. Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 300).*

**Why.** Slice 300's emulator check (usage switch off, a failed user report shows the not-kept text and logs `not kept, usage switch off`) cannot run: no Debug hook makes the report endpoint refuse (checked 2026-11-25, none in `src/Dino/DinoReader.App/Testing/` on origin/main). The `test-store status*` modes only cover book downloads.

**Work.**
- [ ] Add a Debug-only hook (in `src/Dino/DinoReader.App/Testing/**`, documented in its CLAUDE.md) that makes the user-report POST fail with a chosen status.
- [ ] A Maestro flow runs the Slice 300 check on the emulator; the Slice 300 line leaves unverified.md when it passes.
