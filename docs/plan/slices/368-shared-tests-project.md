# Slice 368 — Tests for the shared code in their own test project

*Planned 2026-11-13.* Part of the tidy-by-product slices (366-371). User's choice, 2026-11-13 (item 4 of 5).

**Touches:** `tests/ReadTheStupidText.Tests/**`, `tests/Shared.Tests/**` (new; final name from Slice 370), `ReadTheStupidText.slnx`, `.github/workflows/tests.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `.github/workflows/release.yml` (test job only), `tests/ReadTheStupidText.Tests/FallbackGuardBaseline.txt`, `src/*/Properties/AssemblyInfo.cs` or csproj `InternalsVisibleTo` lines

## Why

`tests/ReadTheStupidText.Tests` holds both RTST-only tests (tray, activity log, settings migration) and tests of the shared engine both apps ship (`PlaybackRate`, `SpeechTextChunker`, voice tables, packs, Documents extractors). A failure there reads as "RTST is broken" even when Dino is the one affected. Repo-wide guards (`FallbackGuardTests`, `SherpaVersionPinTests`) scan all of `src/`.

## Work

- [ ] 1. New xUnit v3 project `tests/Shared.Tests` (name follows Slice 370 if it has merged first). Target `net10.0-windows` only for the files that need Windows types (Infrastructure); plain `net10.0` if every moved test allows it, so it runs on Linux CI too.
- [ ] 2. Move every test whose subject lives in Domain, Application, Documents, Infrastructure (non-tray) or Mobile.Shared into it with `git mv` (history kept). RTST-only subjects (Windows.Tray, App, tray parts of Application such as Input/Startup/Activity, `LocalSettingsStore`) stay in `ReadTheStupidText.Tests`.
- [ ] 3. Move the repo-wide guards (`FallbackGuardTests` + `FallbackGuardBaseline.txt`, `SherpaVersionPinTests`) to the shared project.
- [ ] 4. `InternalsVisibleTo`: add the new assembly wherever the moved tests need internals; remove the old one only where nothing in `ReadTheStupidText.Tests` still needs it.
- [ ] 5. `tests.yml` and `ship-targets.mjs`: shared tests run when shared code changes and gate both products' builds; RTST tests gate only RTST; Dino tests gate only Dino. Update `ship-targets.test.mjs`.
- [ ] 6. Fix the known flaky pair while moving them: `PackKeepAlivePolicyTests` and `VoicePackSetupTests` share the static `PackDownloadHost.CancelRequested` (plan.md *Outstanding work*, release run 42410230793); put them in one non-parallel collection.

## Verification

- Test count before = test count after (sum of all three projects), each test name present once.
- `dotnet test` of all three projects passes locally; `tests.yml` passes on the PR.
- A change to a shared file selects the shared tests; a tray-only change selects only RTST tests (`ship-targets.test.mjs` cases).
