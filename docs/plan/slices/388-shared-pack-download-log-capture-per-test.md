# Slice 388 — Shared tests: `PackDownloadLogTests` capture leaks another test's line
**Product:** Shared

*Built (wave 2026-11-15), merged as PR #1156 (417dc0d).*

**Touches:** `tests/Shared/Shared.Tests/PackDownloadLogTests.cs`, `src/Shared/Sirous.Reading.Application/Reading/PackDownloadLog.cs` (the capture sink, only if a per-test sink is needed; `tests/Shared/Shared.Tests/PausedNoticePolicyTests.cs` also sets it), `src/Shared/Sirous.Reading.Mobile.Shared/PackDownloadHost.cs` (LogService writes through PackDownloadLog.WriteText; review high #1), `tests/Shared/Shared.Tests/PackDownloadLogGlobalSinkTests.cs` (new, review medium #1), `docs/plan/slices/388-shared-pack-download-log-capture-per-test.md`

## Why

`PackDownloadLogTests.UserCancelIsNotLoggedAsAStall` failed once in CI (run 43653687137). It captured `PACKDL stall phase=body bytes=0`, a line with no `pack=` tag, so another test's line leaked into the shared capture.

## Work

- [x] 1. Find how the capture is shared between tests (a static sink) and make the capture per test, so a parallel test's line cannot enter it.
- [x] 2. Keep the assertions as strict as before.

*Changed while building:* the cause is `StallGuardedReadTests`' untagged `Wrap` calls (`StallGuardedRead.cs:59`, via `PackDownloadLog.Stall`) writing to the static `Sink` while `UserCancelIsNotLoggedAsAStall` had captured it. Fix: `PackDownloadLog.CaptureScope` (an `AsyncLocal` sink, called before the global `Sink`) and an internal `WriteText` that `PackDownloadHost.LogService` now uses; `PausedNoticePolicyTests` uses `CaptureScope`; the global-sink throw test moved to its own non-parallel collection (`PackDownloadLogGlobalSinkTests`); the `pack=p1` filter was removed (the scope makes it unnecessary). `Shared.Tests` green 3 times locally; the CI flake was not reproduced locally.

## Verification

- `dotnet test tests/Shared/Shared.Tests/Shared.Tests.csproj` green, run repeatedly (the failure was a one-off in CI).

## From review

- Applied: high #1 (`PausedNoticePolicyTests` and `PackDownloadHost.LogService` bypassed the scope), high #2 and medium #1 (the throwing global-sink test now lives in the non-parallel `PackDownloadLogGlobalSinkTests`).
- Skipped #3 (`reviews/s388-high.md`): "scoped and global sinks share one try/catch" - low, test-only scope behaviour, no production failure path.
- Skipped #4 (`reviews/s388-high.md`): "test-only seam is public production API with an AsyncLocal read on every Write" - low, API shape only.
- Skipped #5 (`reviews/s388-high.md`): "CaptureScope replaces the enclosing scope instead of chaining" - low, test-only scope behaviour.
- Skipped #6 (`reviews/s388-high.md`): "no test proves the new isolation guarantee" - low, test-only.
- Skipped #2 (`reviews/s388-medium.md`): "scoped and global sink share one try/catch" - low, test-only scope behaviour, no production failure path (production never sets a scope).
- Skipped #3 (`reviews/s388-medium.md`): "ScopeRestore.Dispose writes the AsyncLocal in the disposing context" - low, test-only scope behaviour, no production failure path (production never sets a scope).
- Skipped #4 (`reviews/s388-medium.md`): "removed pack=p1 filter and global-Sink capture weaken assertions" - low, test-only scope behaviour, no production failure path (production never sets a scope).

#### For CLAUDE.md (lifted 2026-11-15)

- Tests capture `PackDownloadLog` through `PackDownloadLog.CaptureScope`, never by setting the static `PackDownloadLog.Sink`: the sink is process-wide and parallel tests' lines leak into it. A test that must exercise the global `Sink` goes in the non-parallel collection (`tests/Shared/Shared.Tests/PackDownloadLogGlobalSinkTests.cs`). (Slice 388: docs/plan/slices/388-shared-pack-download-log-capture-per-test.md)

#### Not yet verified

- The shared tests job stays green over the next releases (no foreign PACKDL line); the CI flake was not reproduced locally.
