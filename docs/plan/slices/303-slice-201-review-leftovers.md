# Slice 303 — Slice 201 review leftovers that change behaviour

*In progress (wave 2026-11-07), draft PR.* *Planned 2026-11-07.* Four items from [Slice 201](201-norwegian-pack-crash.md)'s review, listed in plan.md's *Outstanding work* and in the slice file. Moved here.

**Touches:** `.github/workflows/packs.yml`, `src/ReadTheStupidText.Application/Reading/**`, `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/303-slice-201-review-leftovers.md`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosTtsEngine.cs` (item 4 parity), `src/ReadTheStupidText.Application/**` (FallbackArea, item 3 constant), `src/ReadTheStupidText.Domain/**` (widened at claim)

## Work

- [ ] 1. Android and iOS (and Windows, shared code): a previously installed bad `piper-nb` now reads as "not installed" with no release log. Add a log line (a fallback event, not text) when an installed pack fails the shape check, so the cause is visible.
- [ ] 2. `packs.yml` splits a file list with Python `splitlines()` (line 406) while the C# side splits on `\n` only: a `\r` or Unicode line separator in a list would differ. Make both split on `\n` (strip `\r`), with a test on the C# side and a fixture check in the workflow.
- [ ] 3. The pack-shape check hard-codes 5 files: derive the expected files from the pack's own manifest (or one named constant used by the workflow and the app).
- [ ] 4. `RequirePiperTokens` (`SherpaEngineFactory.cs:103`) gates on VITS, not on the Piper id list: gate on the Piper pack ids so a non-Piper VITS model does not need a tokens file. Test with both.

## Verification

- Unit tests per item; a corrupted `piper-nb` fixture on the emulator logs the new line; `packs.yml` passes `actionlint`.
