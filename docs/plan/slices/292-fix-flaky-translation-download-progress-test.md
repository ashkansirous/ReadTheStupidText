# Slice 292 — Fix the flaky `TranslationModelDownloadSessionTests.Progress_is_reported_while_downloading`

*Planned 2026-11-07.* Test only (unless the session's progress reporting needs a synchronous seam). Moved from plan.md's *Outstanding work*.

**Touches:** `tests/ReadTheStupidText.Tests/TranslationModelDownloadSessionTests.cs`, `src/ReadTheStupidText.Mobile.Shared/Translation/TranslationModelDownloadSession.cs`, `docs/plan/slices/292-fix-flaky-translation-download-progress-test.md`

## Why

`TranslationModelDownloadSessionTests.Progress_is_reported_while_downloading` (`tests/ReadTheStupidText.Tests/TranslationModelDownloadSessionTests.cs:210-221`) failed CI on PRs #672 (run 41310271450), #845 and #846 and passed on rerun. It reports progress through `Progress<T>` (which posts the callback to the thread pool or a captured context) and returns after `Task.Delay(50)`, so the posted callback can arrive after the assertion.

## Work

- [ ] 1. Make the test deterministic: pass a synchronous `IProgress<T>` (a small test double that records calls inline), or wait for the session's Changed callback with a timeout instead of `Task.Delay(50)`. No sleeps.
- [ ] 2. If the session only accepts `Progress<T>`, accept `IProgress<T>` in its constructor or method (one change, no behaviour change).

## Verification

- The test runs 200 times in a loop (`dotnet test --filter` repeated, or a `[Theory]` with 200 rows locally) with no failure; the suite is green.
