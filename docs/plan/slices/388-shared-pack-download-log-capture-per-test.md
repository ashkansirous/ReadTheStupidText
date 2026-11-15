# Slice 388 — Shared tests: `PackDownloadLogTests` capture leaks another test's line
**Product:** Shared

*In progress (wave 2026-11-15), draft PR.*

**Touches:** `tests/Shared/Shared.Tests/PackDownloadLogTests.cs`, `src/Shared/Sirous.Reading.Application/Reading/PackDownloadLog.cs` (the capture sink, only if a per-test sink is needed; `tests/Shared/Shared.Tests/PausedNoticePolicyTests.cs` also sets it), `docs/plan/slices/388-shared-pack-download-log-capture-per-test.md`

## Why

`PackDownloadLogTests.UserCancelIsNotLoggedAsAStall` failed once in CI (run 43653687137). It captured `PACKDL stall phase=body bytes=0`, a line with no `pack=` tag, so another test's line leaked into the shared capture.

## Work

- [ ] 1. Find how the capture is shared between tests (a static sink) and make the capture per test, so a parallel test's line cannot enter it.
- [ ] 2. Keep the assertions as strict as before.

## Verification

- `dotnet test tests/Shared/Shared.Tests/Shared.Tests.csproj` green, run repeatedly (the failure was a one-off in CI).
