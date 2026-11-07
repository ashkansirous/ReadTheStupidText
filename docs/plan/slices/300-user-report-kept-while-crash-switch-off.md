# Slice 300 — A kept problem report must not be wiped at next start when the remote crash switch is off

*Planned 2026-11-07.* Known gap from [Slice 249](249-dino-report-a-problem.md): a report kept while the remote crash switch is off can be wiped at the next start although the page said "kept" (`src/ReadTheStupidText.Mobile.Shared/Crashes/CrashReporter.UserReport.cs:135`, which logs "kept for retry" and returns `FailedKept`). Moved from plan.md's *Outstanding work*. Shared code: Android and iOS (and the Windows head) behave the same.

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Crashes/CrashReporter.UserReport.cs`, `src/ReadTheStupidText.Mobile.Shared/Crashes/CrashReporter*.cs`, `src/ReadTheStupidText.Application/Crashes/CrashSendPolicy.cs`, `tests/DinoReader.Tests/**`, `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/300-user-report-kept-while-crash-switch-off.md`

## Work

- [ ] 1. Find the start-up path that deletes queued reports when `crashReports.enabled` is false (read `CrashReporter` start and `CrashSendPolicy.DeleteAfter`), and write down the order in the slice file.
- [ ] 2. Fix one of two ways, chosen from that read: keep user-written reports (they were asked for by the user) while the switch is off and send them when it turns on, or change the page's text so it never says "kept" when they will be wiped. Prefer keeping them.
- [ ] 3. Test: save a report with the switch off, restart the reporter, the report is still queued; with the switch on it is sent.
- [ ] 4. Android and iOS: the same shared code; confirm by log on the emulator and one iOS build.

## Verification

- The tests above pass; the emulator run: switch off in the remote config, send a report, restart, switch on, the report arrives.
