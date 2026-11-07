# Slice 300 — A kept problem report must not be wiped at next start when the remote crash switch is off

*Built 2026-11-07 (wave 3, night 2026-11-07-1412), merged as PR #919 (234b6290); emulator and iOS device checks not yet run.* *Planned 2026-11-07.* Known gap from [Slice 249](249-dino-report-a-problem.md): a report kept while the remote crash switch is off can be wiped at the next start although the page said "kept" (`src/ReadTheStupidText.Mobile.Shared/Crashes/CrashReporter.UserReport.cs:135`, which logs "kept for retry" and returns `FailedKept`). Moved from plan.md's *Outstanding work*. Shared code: Android and iOS (and the Windows head) behave the same.

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Crashes/CrashReporter.UserReport.cs`, `src/ReadTheStupidText.Mobile.Shared/Crashes/CrashReporter*.cs`, `src/ReadTheStupidText.Application/Crashes/CrashSendPolicy.cs`, `tests/DinoReader.Tests/**`, `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/300-user-report-kept-while-crash-switch-off.md`, `src/ReadTheStupidText.Mobile.Shared/Crashes/**` (CrashSender.cs, the report store; widened at claim)

**Decision (wave 3, night 2026-11-07-1412, revised after the high review, `reviews/s300-high.md` findings 1-3):** the first choice (keep user reports through the wipe, send when the switch turns on) was built and reverted: it broke the published privacy text (PRIVACY.md:156 and site/privacy.html:173 say anything waiting is deleted when the switch is off) and leaked the old log tail. Built instead, the slice's second option with no new strings: a failed user report is kept for retry only while the person's anonymous-usage switch is on and the build may report (`MayReport()`); otherwise it is not saved and the page shows the existing `ReportProblem_FailedNotKept` text. So the page never says "kept" for a report the next start would wipe, and the wipe and the privacy text stay as on main. **Test rule:** the emulator check never changes the live remote config; it uses a Debug-only override if one exists, else it goes to *Not yet verified*.

## Work

- [x] 1. Find the start-up path that deletes queued reports when `crashReports.enabled` is false (read `CrashReporter` start and `CrashSendPolicy.DeleteAfter`), and write down the order in the slice file.
- [x] 2. Fix (built the not-kept way, see the Decision): a failed user report is saved for retry only while the usage switch is on and the Worker did not refuse it; with the switch off it is not saved and the page shows the existing not-kept text.
- [x] 3. Test: `CrashSendPolicy.TryKeepFailedUserReport` over a real `PendingCrashStore`: switch off saves nothing, switch on saves one copy, a Worker refusal saves nothing; the keep policy by status (network failure, 503, 404, 401, 403, 301 kept; 200, 400, 413, 422 not).
- [ ] 4. Android and iOS: the same shared code; confirm by log on the emulator and one iOS build.

## Verification

- The tests above pass. *Not yet verified* (emulator): the person's usage switch off, send a report against the Debug stub that refuses, the page shows the not-kept text and the log shows `not kept, usage switch off`. Checked (review finding 7): no Debug-only way to make the user-report endpoint refuse exists (the Debug test hooks in `src/DinoReader.App/Testing` have none), so this emulator check stays unverified until one is added.

## Start-up order (item 1, read from the code)

1. `CrashReporter.Start` builds the store and calls `StartReporting`: handlers installed; if `!BuildEligible()` then `DiscardAll`; then `RefreshAllowed()` (switch on and build eligible) and, if closed, `DiscardAll` again; then `RecordWhatTheLastRunLeft`.
2. `BeginSending` starts `CrashSender.RunAfterStartAsync` (12 s later). `SendPendingAsync` first checks the person's usage switch: off means `discardAll()` and return (the same again between reports). Only then does it read the remote config: `ReadyAddressAsync` returns nothing while `crashReports.enabled` is off or unknown, so the reports are kept (the remote switch never deletes).
3. Finding: the premise "remote crash switch off wipes" is not what the code does; the wipe is the **person's anonymous-usage switch** being off (`StartReporting`, `RefreshAllowed` when the gate closes, `CrashSender` `switchOn()`), all through `DiscardAll` -> `store.Clear()`. A user report (`SendUserReportAsync` goes even with that switch off) that failed was saved ("kept") and then wiped at the next start. A remote-off report was already kept and is sent when the remote config turns on.

## Built (Decision applied)

- The wipe paths (`DiscardAll`, `store.Clear()`, the sender's note texts) are exactly as on main; the earlier keep-through-the-wipe code was reverted.
- `CrashSendPolicy.KeepFailedUserReport(status, mayReport)` (pure) is `mayReport && !DeleteAfter(status)`. `CrashReporter.PostUserReportAsync` (`CrashReporter.UserReport.cs`) saves a failed report only when it is true, so with the usage switch off (or a build that may not report) nothing is saved and the outcome is `Failed` (the page's existing not-kept text); with it on, `FailedKept` and one saved copy. The Debug note says `not kept, usage switch off`.
- Tests: `PendingCrashStoreTests` (policy by status and switch; the Worker's refusals are never kept; switch off leaves no saved copy, on leaves one). Fast checks green: RTST and Dino test suites, Mobile.Shared Android and iOS compiles.
- Item 4: shared code, one implementation for Android and iOS. The emulator run stays in *Not yet verified*: switch the person's usage switch off, send a report against the Debug stub that refuses, the page shows the not-kept text and the log shows `not kept, usage switch off`.

## Changed while building

- The first design (keep user reports through the wipe, send when the switch turns on) was built and reverted after the high review: it clashed with the published privacy text and leaked the old log tail (`reviews/s300-high.md` findings 1-3). The built design is the not-kept one in the Decision; the wipe paths and `PRIVACY.md` are unchanged. Findings 4-9 of that file were written against the reverted code.
- A failed user report is kept only when `MayReport()` and not `DeleteAfter` and the save succeeds (`CrashSendPolicy.TryKeepFailedUserReport`, safe with a null store); otherwise the outcome is `Failed` and the page shows `ReportProblem_FailedNotKept`.
- Coordinator mistake: `CrashSendPolicy.cs` was reset to main by accident; a fixer re-added it and it was re-reviewed (check1, check2).

*Checks:* RTST tests 2165 pass, Dino tests 3860 pass, Mobile.Shared Android compile 0 errors, Mobile.Shared iOS compile 0 errors (the iOS compile ran before the final policy edit).

*From review:* gate files in `D:/ReadTheStupidText-worktrees/reviews/`: `s300-high.md` (first design, reverted), `s300-high-2.md`, `s300-medium.md`, `s300-check1.md`, `s300-check2.md`.
- Applied: high-2 #1, #2, #3, #6; medium #3 (line-ending churn restored) and #7 (checked: no Debug refuse hook exists, so the emulator check is unverified); check1 #1, #2.
- check1 (third and fourth findings): the brief does not say what was done; check2 raised the same two points again (its first and fifth) and those are listed below.
- Skipped #4 (`reviews/s300-high-2.md`): "If the person turns the usage switch off between the two" - a narrow race, accepted.
- Skipped #5 (`reviews/s300-high-2.md`): "Not a wrong result; it hides that the real condition is the person's switch" - a naming nit, result is correct.
- Skipped #1 (`reviews/s300-medium.md`): "with switch off a failed user report is now dropped" - follows the slice Decision; the person taps Try again.
- Skipped #2 (`reviews/s300-medium.md`): "switch can flip before Save, saving a copy that the next start wipes" - the same narrow race as high-2 #4.
- Skipped #4 (`reviews/s300-medium.md`): "only pure policy and Save are tested" - the caller wiring goes to the emulator check.
- Skipped #5 (`reviews/s300-medium.md`): "two one-line public helpers" - low, left as is.
- Skipped #6 (`reviews/s300-medium.md`): "nested ternary of magic strings" - low, left as is.
- Skipped #1 (`reviews/s300-check2.md`): "DropQueuedCopies runs before keep decision" - low; the queue is wiped at the next start anyway.
- Skipped #2 (`reviews/s300-check2.md`): "switch toggled mid-flight changes keep decision and log verdict" - low, narrow race.
- Skipped #3 (`reviews/s300-check2.md`): "Save() false (disk write failure) is logged as policy refusal" - low.
- Skipped #4 (`reviews/s300-check2.md`): "BuildEligible already true here" - low, a wording nit in the log line.
- Skipped #5 (`reviews/s300-check2.md`): "No test of caller (PostUserReportAsync outcome Failed vs FailedKept with switch off)" - low; goes to the emulator check.

#### For CLAUDE.md (lifted 2026-11-07)

None.

#### Not yet verified

- Emulator: with the usage switch off, a failed user-report send shows the not-kept text and logs `not kept, usage switch off`. Needs a Debug hook that makes the report endpoint refuse; none exists.
- iOS device: the same shared code, not run on a device.

*Follow-up candidate:* a Debug hook that makes the user-report endpoint refuse.
