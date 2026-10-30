### Slice 199 — Unit tests for the test-hook launch keys, BookMatch and the phone timing parser

*Built (wave 2026-10-30), PR #586.* *Planned 2026-10-29.* Tooling only, no app behaviour change. Gap left in Slice 175 (*Not yet verified*).

**Touches:** `.github/workflows/tests.yml` (one step), `src/DinoReader.Core/Testing/BookMatch.cs` (new), `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookDispatcher.cs` (only if `LaunchKeys` needs a seam to be testable), `src/DinoReader.App/Testing/DinoTestCommands.cs` (move `BookMatch` out to its own file, e.g. `src/DinoReader.Core/Testing/BookMatch.cs` or a linked file, so a test project can reach it), `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`, `tools/phone/timing.ps1`, `tools/phone/test-timing.ps1` (new, same shape as `test-quoting.ps1`), `docs/plan/slices/199-test-hook-unit-tests.md`.

#### Why

`TestHookDispatcher.LaunchKeys` was fixed twice in the same spot (Slice 175), `BookMatch` picks the book and page sentences for the Dino open-book hook, and `timing.ps1` turns a phone log into the timing table. None has a test, so a regression shows up only on a device run.

#### Claim notes (2026-10-30, grounded)

- `tools/phone/timing.ps1` already has `ConvertFrom-TimingLog` (:8): the new `test-timing.ps1` calls it, no split needed.
- `test-quoting.ps1` runs nowhere today. Decision: run both `tools/phone/test-quoting.ps1` and the new `test-timing.ps1` as one step in `.github/workflows/tests.yml` (pwsh, seconds), so neither rots.
- `TestHookDispatcher.LaunchKeys` is public static (:50); test it without linking the whole dispatcher if that pulls in MAUI (extract the key list if needed).
- `BookMatch` (`DinoTestCommands.cs:308`, internal static) moves to a new `src/DinoReader.Core/Testing/BookMatch.cs`.

#### Work

- [x] `LaunchKeys`: tests for cmd first, own keys, app keys, no duplicates, and the iOS order (link the file into `tests/ReadTheStupidText.Tests` the way `TestHookCommand.cs` is linked).
- [x] `BookMatch`: move to a file a test project can link; tests for `Find` (by id, missing id, several books) and `SentencesOfPage` (first page, last page, out of range).
- [x] `timing.ps1`: split the log parsing into a function; `test-timing.ps1` feeds it a saved log (commit a small fixture) and checks the engine, synthesis and first-audio columns.
- [x] Wire `test-timing.ps1` into the same place `test-quoting.ps1` runs.

#### Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` and `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- `pwsh tools/phone/test-timing.ps1` passes.

#### Built (2026-10-30)
- New `TestHookLaunchKeys` (Mobile.Shared/Testing, compiles in every configuration; only the Debug-only dispatcher calls it) with 4 tests that run in CI's Release build; `BookMatch` moved to `src/DinoReader.Core/Testing/BookMatch.cs` (internal) with 11 tests; `tools/phone/test-timing.ps1` + fixture; both phone self-tests run in `tests.yml` (5-minute limit; the paths filter is generated from ship-targets GROUPS, so `tools/phone` is not added there).
- Reviews: high 3 of 5 applied (2 needed no change); medium 1 applied (timeout, exit codes), skipped the full-rebuild note (shared code changes rebuild both apps anyway).
- Checks: RTST 1662 (Debug) / 1656 (Release), Dino 1789, both Android builds, both iOS compiles, both pwsh self-tests (a broken fixture exits 1).
