# Slice 303 — Slice 201 review leftovers that change behaviour
**Product:** RTST

*Built 2026-11-07 (wave 3, night 2026-11-07-1412), merged as PR #921 (6c487973); Windows read path, iOS device and a real packs.yml run not yet verified.* *Planned 2026-11-07.* Four items from [Slice 201](201-norwegian-pack-crash.md)'s review, listed in plan.md's *Outstanding work* and in the slice file. Moved here.

**Touches:** `.github/workflows/packs.yml`, `src/ReadTheStupidText.Application/Reading/**`, `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/303-slice-201-review-leftovers.md`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosTtsEngine.cs` (item 4 parity), `src/ReadTheStupidText.Application/**` (FallbackArea, item 3 constant), `src/ReadTheStupidText.Domain/**` (widened at claim), `src/ReadTheStupidText.Infrastructure/Reading/WindowsVoicePackService.cs`, `.maestro/rtst/needs-file/**` (widened mid-build: item 1 Windows half and its scripted check; no other live slice touches them)

## Work

- [x] 1. Android and iOS (and Windows, shared code): a previously installed bad `piper-nb` now reads as "not installed" with no release log. Add a log line (a fallback event, not text) when an installed pack fails the shape check, so the cause is visible.
- [x] 2. `packs.yml` splits a file list with Python `splitlines()` (line 406) while the C# side splits on `\n` only: a `\r` or Unicode line separator in a list would differ. Make both split on `\n` (strip `\r`), with a test on the C# side and a fixture check in the workflow.
- [x] 3. The pack-shape check hard-codes 5 files: derive the expected files from the pack's own manifest (or one named constant used by the workflow and the app).
- [x] 4. `RequirePiperTokens` (`SherpaEngineFactory.cs:103`) gates on VITS, not on the Piper id list: gate on the Piper pack ids so a non-Piper VITS model does not need a tokens file. Test with both.

## Verification

- Unit tests per item; a corrupted `piper-nb` fixture on the emulator logs the new line; `packs.yml` passes `actionlint`.

## Built

*Changed while building:*
- Item 1: an installed Piper pack whose tokens fail the check reports `FallbackArea.Pack` / `pack_failed` once per folder per run, on Windows (`WindowsVoicePackService`) and Mobile (`MobileVoicePackService`). It has its own dedupe key, `PackVersionFolders.FirstTokensReportFor`, so it does not share a set with the Slice 275 manifest reports. The Debug marker stays; tokens.txt is read once. It reuses `pack_failed`, because a new reason would need the Worker's `FALLBACK_REASONS`.
- Item 2: `packs.yml` splits on `\n`, strips `\r` and trims like `PiperTokens.TrySplit` (CR, space, tab), and rejects ids outside int32. The fixture self-tests; C# tests cover U+2028, U+2029, U+0085, `\r`, U+00A0 and int32.
- Item 3: the tokens check derives the expected files from each pack's `pack.json` (no hard-coded 5). A voice without `model` fails with a named message.
- Item 4: `Application/Reading/PiperTokenGuard` gates on `PackConstants.PiperPackIds`; a VITS voice with an empty `PackId` is still checked, as on main. `VoiceModelPaths` gained `PackId`, filled by `InstalledVoiceCatalog`. The three `RequirePiperTokens` copies (Windows `SherpaEngineFactory`, Android, iOS) are replaced by it.
- Touches widened mid-build: `WindowsVoicePackService.cs`, `.maestro/rtst/needs-file/**`.

*From review:* files `reviews/s303-high.md`, `reviews/s303-medium.md`, `reviews/s303-check1.md` (gate: high, medium, check pass 1).
- High: applied #1, #2, #7.
- Skipped #3 (`reviews/s303-high.md`): "guard rebuilds tokens path itself" (low; latent, RequireModelFiles runs first on all platforms).
- Skipped #4 (`reviews/s303-high.md`): "parity of the guard call rests on three hand-written call sites" (low; all three present).
- Skipped #5 (`reviews/s303-high.md`): "a defaulted trailing parameter" (low; the only production construction site, InstalledVoiceCatalog, sets PackId).
- Skipped #6 (`reviews/s303-high.md`): "from inside a bool predicate" (low).
- Medium: applied #1, #2, #3.
- Skipped #4 (`reviews/s303-medium.md`): "Real pack.json files not opened; unconfirmed" (the high review found every voice in both manifests sets model).
- Check pass 1: none applied, #1-#6 below:
- Skipped #1 (`reviews/s303-check1.md`): "guard skips VITS voices whose PackId is set but not in PiperPackIds" (this is the slice's own item 4 design: gate on the Piper ids; a known non-Piper VITS skips).
- Skipped #2 (`reviews/s303-check1.md`): "fails on empty pack.json model" (low; fail-loud only).
- Skipped #3 (`reviews/s303-check1.md`): "reported without language argument" (low).
- Skipped #4 (`reviews/s303-check1.md`): "HasBrokenTokens duplicated in both" (low).
- Skipped #5 (`reviews/s303-check1.md`): "collection-expression Assert.Equal" (info; compiles and passes).
- Skipped #6 (`reviews/s303-check1.md`): "Android-only, never run" (low; the flow has since run on the emulator and passes).

*Checks:* RTST tests 2168 pass, Dino tests 3859 pass, actionlint `packs.yml` 0 findings, RTST App x64, Mobile Android and Mobile iOS compile with 0 errors.

*Device:* emulator-5554, cached rtst `b1bb6fd542a3`. `corrupt-piper-pack-logged` PASS (19 s, `[Usage] fallback pack:pack_failed`; logcat `RTST_PACK_TOKENS_REJECTED pack=piper-nb version=v1`). `paste-and-read` PASS (a good supertonic pack reads). The flow asserts area:reason only, because the line carries no pack id.

#### For CLAUDE.md (lifted 2026-11-07)

- A new Piper-style pack id must be added to `PackConstants.PiperPackIds`: `PiperTokenGuard` (`src/ReadTheStupidText.Application/Reading/`) checks tokens.txt only for those ids (and for a VITS voice with an empty `PackId`) before sherpa builds the engine, and a bad file aborts the process natively. Build `VoiceModelPaths` with `PackId` set. (Slice 303: docs/plan/slices/303-slice-201-review-leftovers.md)
- Every `new OfflineTts(` site (Windows `SherpaEngineFactory`, Android, iOS) calls `PiperTokenGuard.Require` first; keep the three in step. (Slice 303: docs/plan/slices/303-slice-201-review-leftovers.md)

#### Not yet verified

- Windows read path with a bad pack (VM not run: free memory 6.5 GB, under the 8 GB needed).
- iOS device: bad Piper pack reports `pack_failed` and the guard stops the engine build.
- A real `packs.yml` run (next packs release): the new tokens check and fixture self-test.
