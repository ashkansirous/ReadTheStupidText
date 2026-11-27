# Slice 451 — A Debug hook that makes the user-report endpoint refuse
**Product:** Shared
**Touches:** `src/Dino/DinoReader.App/Testing/**` (a new partial file; never `Testing/DinoTestCommands.cs`, which parked draft PR #1430's worktree edits), `.maestro/dino/**`, `src/Dino/DinoReader.App/MauiProgram.cs` (one `#if DEBUG` call that registers the hooks; widened by the coordinator after review 1 #1), `docs/plan/unverified.md` (the Slice 300 line this closes), this slice file

*Built (wave 2026-11-27), PR #1450. Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 300).*

**Why.** Slice 300's emulator check (usage switch off, a failed user report shows the not-kept text and logs `not kept, usage switch off`) cannot run: no Debug hook makes the report endpoint refuse (checked 2026-11-25, none in `src/Dino/DinoReader.App/Testing/` on origin/main). The `test-store status*` modes only cover book downloads.

**Work.**
- [x] Add a Debug-only hook (in `src/Dino/DinoReader.App/Testing/**`, documented in its CLAUDE.md) that makes the user-report POST fail with a chosen status.
- [x] A Maestro flow runs the Slice 300 check on the emulator; the Slice 300 line leaves unverified.md when it passes.

**Built.** Debug hooks `user-report-refuse status=400-599|off` (an in-app loopback listener on 127.0.0.1:8788) and `usage-switch state=on|off`, in `Testing/DinoTestCommands.UserReport.cs` and `Testing/UserReportRefusePlan.cs`. One `#if DEBUG` call, `ChainUserReportHooks()`, in `MauiProgram.cs` registers them just before `CrashTestHook.Install()`. Flow `.maestro/dino/user-report-not-kept-usage-off.yaml` points `crash-stub` at `http://127.0.0.1:8788`; its `onFlowComplete` resets the switch, the listener and the stub. `Testing/CLAUDE.md` documents the hooks.

**Device run.** emulator-5554, flow `user-report-not-kept-usage-off`: PASS in 55.9 s (2026-11-27, build `0750b2d397ed`). Markers seen: `HOOK user-report-refuse answered 503` and `CRASH_REPORT user report failed (status 503), not kept, this build or the usage switch may not report`. Logs: `D:/ReadTheStupidText-worktrees/reviews/w10-device/`. That passes the Slice 300 emulator check, so its line is removed from `docs/plan/unverified.md`.

*Changed while building:*
- The app's marker reads `not kept, this build or the usage switch may not report`, not the `not kept, usage switch off` the plan text quoted; the flow matches the app's real text.
- `MauiProgram.cs` got one `#if DEBUG` call (the coordinator widened Touches after review 1 #1) because a module initializer raced `CrashTestHook.Install()`.
- No unit tests for `UserReportRefusePlan`: a `Compile` link in `DinoReader.Tests.csproj` would touch what parked draft PR #1437's worktree edits. Listed in `unverified.md`.

#### From review
- Both findings files were read in full (high 6 of 6, medium 5 of 5). Applied: high findings 1, 2, 5 and 6 (6 by correcting the header comment, guard kept); medium findings 1, 2 and 3. The four others are the skips below.
- Skipped #3 (`reviews/pr-1450-high.md`): "`ReadWholeRequestAsync` has no read timeout or cancellation and only handles Content-Length bodies." - Debug-only loopback listener; the app's own report sender sends a Content-Length body, so chunked and slow-read cases never happen in the flow.
- Skipped #4 (`reviews/pr-1450-high.md`): "`SetUsageSwitch` dereferences `IPlatformApplication.Current!` and `GetRequiredService<UsageCounter>()` with no guard, and its order dependency on `crash-stub` is not documented." - Debug-only hook; a null there fails the flow loudly, which is the wanted result.
- Skipped #4 (`reviews/pr-1450-medium.md`): "`ChainUserReportHooks()` is not idempotent; a second call stacks the wrapper." - it is called once from MauiProgram at startup and has no second caller; the Testing/CLAUDE.md entry now says to call it once.
- Skipped #5 (`reviews/pr-1450-medium.md`): "Work items still unticked and no `## Review` section listing the findings files." - the slice record is written by update-docs after the device run.
