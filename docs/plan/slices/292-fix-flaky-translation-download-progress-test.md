# Slice 292 — Fix the flaky `TranslationModelDownloadSessionTests.Progress_is_reported_while_downloading`

*Built 2026-11-07 (wave 2026-11-07, PR #902), not merged; the 200-run loop is not yet verified (25 runs passed).* Test only (unless the session's progress reporting needs a synchronous seam). Moved from plan.md's *Outstanding work*.

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
- High 1 and medium 1 (the `IsCompletedSuccessfully` assertion cannot fail on its own; a missing progress shows as a timeout or download-failed exception): skipped, the test is still correct and fails either way, only the message is weaker.
- High 2 (a regression that stops progress reports shows as "download failed" after the 5 s wait): skipped, same reason.
- High 3 and medium 3 (the 200-run loop is not recorded): handled by the 25-run result above and *Not yet verified* below; item 2 is marked not needed.
- High 4 (Touches line lists the unchanged production file): skipped, left as planned.
- Medium 2 (exact `== 0.4` in the `Changed` handler, `>= 0.4` would be sturdier): skipped, 0.4 is the exact value the test itself reports.

#### Not yet verified

- The 200-run loop of `Progress_is_reported_while_downloading` (only 25 runs were done).
