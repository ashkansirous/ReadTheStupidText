# Slice 378 — Build warnings gate: current GitHub Actions, warnings that fail the build, and the Android 9-11 scan crop bug
**Product:** Shared

*Planned.*

**Touches:** `.github/workflows/*.yml`, `.github/dependabot.yml` (new), `.github/CLAUDE.md`, `Directory.Build.props`, `src/Shared/Sirous.Reading.Mobile.Shared/Platforms/Android/AndroidPixelImageLoader.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/Platforms/Android/AndroidSpeechReader.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/Platforms/Android/Crashes/AndroidCrashHooks.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/IDocumentScanner.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/Ocr/IBuiltInTextRecognizer.cs`, `tests/Shared/Shared.Tests/VoicePackSetupTests.cs`, `tests/Shared/Shared.Tests/KokoroNanRescueTests.cs`, `tests/Dino/DinoReader.Tests/AudioPackStoreTests.cs`, `tests/Dino/DinoReader.Tests/BackCoverPictureTests.cs`, `tests/Dino/DinoReader.Tests/BookBuilderTests.cs`, `tests/Dino/DinoReader.Tests/LanguageAttributionTests.cs`, `tests/Dino/DinoReader.Tests/RecordSplitTests.cs`, `tests/Dino/DinoReader.Tests/UsagePlaybackTests.cs`, `docs/plan/slices/378-build-warnings-gate.md`

## Why

The user, 2026-11-14, looking at the release runs' warnings: "code that is not awaited ... how come no code review has caught it?", "node 20 is very old ... how come it is not updated?", "how come I have a warning for the version of android? dont we gate it?"

What was found that day (read from `origin/main` 55086c6d and the check-run annotations of release-dino 43621807490 and release-rtst 43611996617, 47 distinct warnings):

- **Nothing gates warnings.** No project or props file sets `TreatWarningsAsErrors` or `WarningsAsErrors`, and the review gate reads the diff, not the build's warning list. So warnings pile up unseen.
- **Node 20 actions:** every workflow is on `actions/checkout@v4` (27 uses), `setup-dotnet@v4` (11), `setup-node@v4` (7), `cache@v4`, `cache/restore@v4`, `cache/save@v4`, `setup-msbuild@v2`. Latest on 2026-11-14 (`gh api repos/<r>/releases/latest`): checkout v7.0.1, setup-dotnet v6.1.0, setup-node v7.1.0, cache v6.1.0, setup-msbuild v3, upload-google-play v1.1.5. No Dependabot config exists, so nothing proposes action bumps. No earlier update round touched the workflow action versions (only the GitVersion v4.5.0 pin, 2026-06-25).
- **Other workflow warnings:** `gitversion/execute` gets an input it doesn't have, `useConfigFile` (`windows.yml:83`, `release-shared.yml:228`, `dinoreader-windows.yml:88`; `configFilePath` is what it reads). `r0adkll/upload-google-play` warns that `track` is deprecated in favour of `tracks` (`release-rtst.yml:341`, `release-dino.yml:341`).
- **Android 9-11 scan crop bug (likely):** `AndroidPixelImageLoader.cs:45` `BitmapRegionDecoder.NewInstance(string)` and `:49` `NewInstance(Stream)` exist only from API 31; the apps support API 28 (`SupportedOSPlatformVersion` 28.0). On Android 9, 10 and 11 the call fails, the `catch` swallows it and every OCR line crop comes from the lower-resolution detection image. Present since Slice 52 (c230d278, 2026-10-21). Analytics on 2026-11-14: zero `ocr_crop_degraded` fallback events in `rtst_usage` and `dino_usage` over all retained data (100 days), so no user evidence either way; there is no scan usage event to size it. Fix: the `NewInstance(path, false)` / `NewInstance(stream, false)` overloads, which exist on every supported version.
- **False positives with the same warning code:** `AndroidCrashHooks.cs:51-65` uses API 30 `ApplicationExitInfo` members in `ToNative`/`KindOf`; the caller already returns early below API 30 (`:27`), but the analyzer can't see that across methods.
- **Obsolete API:** `AndroidSpeechReader.cs:191,208` `new Locale(code)` is obsoleted on Android 36; `Locale.ForLanguageTag` replaces it.
- **Tests:** `VoicePackSetupTests.cs` lines 106, 654, 834, 854, 903, 921, 955 (8 annotations) call `packs.BlockFirstDownload()` without using its `Task<bool>` (whether the blocked download was later cancelled). The tests do run to the end (they await `FirstDownloadStarted` and the run); the discard just isn't written. Plus xUnit1051 (no `TestContext.Current.CancellationToken`, 7), xUnit2013 (1), nullability CS86xx (4) in DinoReader.Tests, and two cref warnings in Mobile.Shared.
- GitHub shows at most 10 annotations per step, so the list above may be short; work item 4 takes the full list from the build logs.

## Decisions

- Warnings that can hide a real bug fail the build in every project (src and tests): CS4014 (not awaited), CA1416 (platform version), CA1422 (obsoleted on a platform version), CS8600-CS8634 nullability family already reported, xUnit1051, CS1574/CS0419 (bad cref). Use `WarningsAsErrors` for that list in `Directory.Build.props`, not a blanket `TreatWarningsAsErrors`, so a new SDK's new warning doesn't break every release overnight; Slice 379 widens the list once everything is current.
- Platform-version false positives are fixed with `[SupportedOSPlatform("android30.0")]` on the guarded method, never with `#pragma warning disable` or `NoWarn`.
- Dependabot for `github-actions` only (weekly, one grouped PR). NuGet and npm updates are Slice 379's.
- Action bumps keep every existing pin rule in `.github/CLAUDE.md` (SDK band, workload sets, Xcode, GitVersion 6.x); a new major whose inputs changed gets context7/`library-docs` first.

## Work

- [ ] 1. **Android scan crop fix (first, smallest end-to-end):** `AndroidPixelImageLoader.cs:45,49` use `BitmapRegionDecoder.NewInstance(local, false)` and `NewInstance(stream, false)` (confirm the binding's overloads with `library-docs` first). iOS: no change needed (no region decoder; it crops the same way by design, Slice 275), state that in the review notes.
- [ ] 2. **Analyzer false positives and obsolete calls:** `[SupportedOSPlatform("android30.0")]` on `AndroidCrashHooks.ToNative` and `KindOf`; `Locale.ForLanguageTag` at `AndroidSpeechReader.cs:191,208`; fix the two crefs (`IDocumentScanner.cs:28`, `IBuiltInTextRecognizer.cs:6`).
- [ ] 3. **Tests:** `_ = packs.BlockFirstDownload();` at the 8 `VoicePackSetupTests.cs` sites; `TestContext.Current.CancellationToken` at the xUnit1051 sites; `Assert.Single` at `UsagePlaybackTests.cs:96`; the nullability warnings in `BookBuilderTests.cs:315`, `LanguageAttributionTests.cs:206`, `RecordSplitTests.cs:291,293`.
- [ ] 4. **Full list:** build every project and head locally (Windows tray, Dino Windows, Dino and RTST `net10.0-android` and `net10.0-ios`, all three test projects) and collect every warning from the build output, not the annotations. Fix each remaining one on the decision list; list anything left (other codes) in this file for Slice 379.
- [ ] 5. **Gate:** add the `WarningsAsErrors` list to `Directory.Build.props`. Every build in item 4 must pass with it.
- [ ] 6. **Actions:** bump to the current majors (checkout, setup-dotnet, setup-node, cache/restore/save, setup-msbuild, setup-python if behind, upload-google-play to `tracks:`, `microsoft-store-apppublisher` and `gittools/actions` checked for newer releases while keeping GitVersion 6.x). Remove `useConfigFile`. actionlint passes (`release-scripts.yml`). No Node 20 warning is left in a release run.
- [ ] 7. **Dependabot:** `.github/dependabot.yml` with `package-ecosystem: github-actions`, weekly, one group.
- [ ] 8. Rule in `.github/CLAUDE.md` (one line): warnings on the gate list fail the build; fix the code or add the platform attribute, never suppress.

## Verification

- All builds in item 4 succeed with the gate on, and the three test suites pass.
- An emulator run on an **API 28-30 image** (Android 9-11) scans a page with the RTST Scan flow and shows no `No region decoder` debug line / `ocr_crop_degraded` fallback; the same flow on the usual API 3x emulator still passes.
- iOS: `net10.0-ios` builds of both apps compile with the gate on; TestFlight build of the release succeeds.
- The first release run after merge shows no Node 20 and no compiler warning annotations (read through `ci-status`).

## Not yet verified

_(filled in when built)_
