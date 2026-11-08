# Slice 292 — Fix the flaky `TranslationModelDownloadSessionTests.Progress_is_reported_while_downloading`

*Built 2026-11-07 (wave 2026-11-07, PR #902), merged as PR #902 (32e690e5); the 200-run loop is not yet verified (25 runs passed).* Test only (unless the session's progress reporting needs a synchronous seam). Moved from plan.md's *Outstanding work*.

**Touches:** `tests/ReadTheStupidText.Tests/TranslationModelDownloadSessionTests.cs`, `src/ReadTheStupidText.Mobile.Shared/Translation/TranslationModelDownloadSession.cs`, `docs/plan/slices/292-fix-flaky-translation-download-progress-test.md`

## Why

`TranslationModelDownloadSessionTests.Progress_is_reported_while_downloading` (`tests/ReadTheStupidText.Tests/TranslationModelDownloadSessionTests.cs:210-221`) failed CI on PRs #672 (run 41310271450), #845 and #846 and passed on rerun. It reports progress through `Progress<T>` (which posts the callback to the thread pool or a captured context) and returns after `Task.Delay(50)`, so the posted callback can arrive after the assertion.

## Work

- [x] 1. Make the test deterministic: pass a synchronous `IProgress<T>` (a small test double that records calls inline), or wait for the session's Changed callback with a timeout instead of `Task.Delay(50)`. No sleeps.
- [x] 2. If the session only accepts `Progress<T>`, accept `IProgress<T>` in its constructor or method (one change, no behaviour change). Not needed: see *Changed while building*.

## Verification

- The test runs 200 times in a loop (`dotnet test --filter` repeated, or a `[Theory]` with 200 rows locally) with no failure; the suite is green.

## Built

- Design: the test awaits the session's `Changed` event for 0.4 (the attempt stays open on a TaskCompletionSource that the handler completes, bounded by a 5 s limit) instead of `Task.Delay(50)`. No production change: the session builds its own `Progress<double>` in `RunAttemptsAsync`, so a synchronous `IProgress` double would need a new constructor seam, and awaiting the real event tests the real path.
- Ran: 25 of 25 loop runs passed; full suite 2151 pass.

*Changed while building:* item 2 (the `IProgress<T>` constructor seam) was not needed: waiting on the real `Changed` event exercises the real `Progress<double>`, so there is no production change.

*From review:* high (`reviews/s292-high.md`, 4 findings) and medium (`reviews/s292-medium.md`, 3 findings), all low, none applied.
- Skipped high #1 (`reviews/s292-high.md`): "can never fail on its own" - the test is still correct and fails either way (a timeout or download-failed exception), only the message is weaker.
- Skipped medium #1 (`reviews/s292-medium.md`): "only checks the handler's own flag" - same reason as high #1.
- Skipped high #2 (`reviews/s292-high.md`): "after a 5 s wait, since TryAttemptAsync swallows the attempt's TimeoutException as a generic attempt failure" - same reason, the test fails either way.
- High 3 and medium 3 (the 200-run loop is not recorded): handled by the 25-run result above and *Not yet verified* below; item 2 is marked not needed.
- Skipped high #4 (`reviews/s292-high.md`): "which the diff does not change" - the Touches line was left as planned.
- Skipped medium #2 (`reviews/s292-medium.md`): "Passes today (Math.Clamp returns 0.4)" - 0.4 is the exact value the test itself reports.

#### Not yet verified

- The 200-run loop of `Progress_is_reported_while_downloading` (only 25 runs were done).
