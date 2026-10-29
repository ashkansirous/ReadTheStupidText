### Slice 199 — Unit tests for the test-hook launch keys, BookMatch and the phone timing parser

*Planned 2026-10-29.* Tooling only, no app behaviour change. Gap left in Slice 175 (*Not yet verified*).

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookDispatcher.cs` (only if `LaunchKeys` needs a seam to be testable), `src/DinoReader.App/Testing/DinoTestCommands.cs` (move `BookMatch` out to its own file, e.g. `src/DinoReader.Core/Testing/BookMatch.cs` or a linked file, so a test project can reach it), `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`, `tools/phone/timing.ps1`, `tools/phone/test-timing.ps1` (new, same shape as `test-quoting.ps1`), `docs/plan/slices/199-test-hook-unit-tests.md`.

#### Why

`TestHookDispatcher.LaunchKeys` was fixed twice in the same spot (Slice 175), `BookMatch` picks the book and page sentences for the Dino open-book hook, and `timing.ps1` turns a phone log into the timing table. None has a test, so a regression shows up only on a device run.

#### Work

- [ ] `LaunchKeys`: tests for cmd first, own keys, app keys, no duplicates, and the iOS order (link the file into `tests/ReadTheStupidText.Tests` the way `TestHookCommand.cs` is linked).
- [ ] `BookMatch`: move to a file a test project can link; tests for `Find` (by id, missing id, several books) and `SentencesOfPage` (first page, last page, out of range).
- [ ] `timing.ps1`: split the log parsing into a function; `test-timing.ps1` feeds it a saved log (commit a small fixture) and checks the engine, synthesis and first-audio columns.
- [ ] Wire `test-timing.ps1` into the same place `test-quoting.ps1` runs.

#### Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` and `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- `pwsh tools/phone/test-timing.ps1` passes.
