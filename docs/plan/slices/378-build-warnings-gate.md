# Slice 378 — Build warnings gate: current GitHub Actions, warnings that fail the build, and the Android 9-11 scan crop bug
**Product:** Shared

*Built wave 4 (night 2026-11-14-1547), PR #1146, not merged. Device run 2026-11-14 on emulator-5554 passed the two checks that touch this slice; the API 28-30 scan check and the first release run are not yet verified.*

**Touches:** `src/**/*.cs` and `tests/**/*.cs` (only to fix warnings on the gate list, item 4), `.github/actionlint.yaml`, `.github/workflows/*.yml`, `.github/dependabot.yml` (new), `.github/CLAUDE.md`, `Directory.Build.props`, `src/Shared/Sirous.Reading.Mobile.Shared/Platforms/Android/AndroidPixelImageLoader.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/Platforms/Android/AndroidSpeechReader.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/Platforms/Android/Crashes/AndroidCrashHooks.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/IDocumentScanner.cs`, `src/Shared/Sirous.Reading.Mobile.Shared/Ocr/IBuiltInTextRecognizer.cs`, `tests/Shared/Shared.Tests/VoicePackSetupTests.cs`, `tests/Shared/Shared.Tests/KokoroNanRescueTests.cs`, `tests/Dino/DinoReader.Tests/AudioPackStoreTests.cs`, `tests/Dino/DinoReader.Tests/BackCoverPictureTests.cs`, `tests/Dino/DinoReader.Tests/BookBuilderTests.cs`, `tests/Dino/DinoReader.Tests/LanguageAttributionTests.cs`, `tests/Dino/DinoReader.Tests/RecordSplitTests.cs`, `tests/Dino/DinoReader.Tests/UsagePlaybackTests.cs`, `docs/plan/slices/378-build-warnings-gate.md`

## Why

The user, 2026-11-14, looking at the release runs' warnings: "code that is not awaited ... how come no code review has caught it?", "node 20 is very old ... how come it is not updated?", "how come I have a warning for the version of android? dont we gate it?"

What was found that day (read from `origin/main` 55086c6d and the check-run annotations of release-dino 43621807490 and release-rtst 43611996617, 47 distinct warnings):

- **Nothing gates warnings.** No project or props file sets `TreatWarningsAsErrors` or `WarningsAsErrors`, and the review gate reads the diff, not the build's warning list. So warnings pile up unseen.
- **Node 20 actions:** every workflow is on `actions/checkout@v4` (27 uses), `setup-dotnet@v4` (11), `setup-node@v4` (7), `cache@v4`, `cache/restore@v4`, `cache/save@v4`, `setup-msbuild@v2`. Latest on 2026-11-14 (`gh api repos/<r>/releases/latest`): checkout v7.0.1, setup-dotnet v6.1.0, setup-node v7.1.0, cache v6.1.0, setup-msbuild v3, upload-google-play v1.1.5. No Dependabot config exists, so nothing proposes action bumps. No earlier update round touched the workflow action versions (only the GitVersion v4.5.0 pin, 2026-06-25).
- **Other workflow warnings:** `gitversion/execute` gets an input it doesn't have, `useConfigFile` (`windows.yml:83`, `release-shared.yml:228`, `dinoreader-windows.yml:88`; `configFilePath` is what it reads). `r0adkll/upload-google-play` warns that `track` is deprecated in favour of `tracks` (`release-rtst.yml:341`, `release-dino.yml:341`).
- **Android 9-11 scan crop bug (likely):** `AndroidPixelImageLoader.cs:45` `BitmapRegionDecoder.NewInstance(string)` and `:49` `NewInstance(Stream)` exist only from API 31; the apps support API 28 (`SupportedOSPlatformVersion` 28.0). On Android 9, 10 and 11 the call fails, the `catch` swallows it and every OCR line crop comes from the lower-resolution detection image. Present since Slice 52 (c230d278, 2026-10-21). Analytics on 2026-11-14: zero `ocr_crop_degraded` fallback events in `rtst_usage` and `dino_usage` over all retained data (100 days), so no user evidence either way; there is no scan usage event to size it. Fix: the `NewInstance(path, false)` / `NewInstance(stream, false)` overloads, which exist on every supported version.
- **False positives with the same warning code:** `AndroidCrashHooks.cs:51-65` uses API 30 `ApplicationExitInfo` members in `ToNative`/`KindOf`; the caller already returns early below API 30 (`:27`), but the analyzer can't see that across methods.
- **Obsolete API:** `AndroidSpeechReader.cs:191,208` `new Locale(code)` is obsoleted on Android 36; `Locale.ForLanguageTag` replaces it.
- **Tests:** `VoicePackSetupTests.cs` lines 106, 654, 834, 854, 903, 921, 955, 980 (8 annotations; 980 found at claim) call `packs.BlockFirstDownload()` without using its `Task<bool>` (whether the blocked download was later cancelled). The tests do run to the end (they await `FirstDownloadStarted` and the run); the discard just isn't written. Plus xUnit1051 (no `TestContext.Current.CancellationToken`, 7), xUnit2013 (1), nullability CS86xx (4) in DinoReader.Tests, and two cref warnings in Mobile.Shared.
- GitHub shows at most 10 annotations per step, so the list above may be short; work item 4 takes the full list from the build logs.

## Decisions

- Warnings that can hide a real bug fail the build in every project (src and tests): CS4014 (not awaited), CA1416 (platform version), CA1422 (obsoleted on a platform version), CS8600-CS8634 nullability family already reported, xUnit1051, CS1574/CS0419 (bad cref). Use `WarningsAsErrors` for that list in `Directory.Build.props`, not a blanket `TreatWarningsAsErrors`, so a new SDK's new warning doesn't break every release overnight; Slice 379 widens the list once everything is current.
- Platform-version false positives are fixed with `[SupportedOSPlatform("android30.0")]` on the guarded method, never with `#pragma warning disable` or `NoWarn`.
- Exception (coordinator, wave 4): `IosPurchases.cs` keeps one file-scoped CA1422 suppression for StoreKit 1 (about 35 sites, obsoleted from iOS 18) until a StoreKit 2 migration; no other suppression.
- Dependabot for `github-actions` only (weekly, one grouped PR). NuGet and npm updates are Slice 379's.
- Action bumps keep every existing pin rule in `.github/CLAUDE.md` (SDK band, workload sets, Xcode, GitVersion 6.x); a new major whose inputs changed gets context7/`library-docs` first.

## Work

- [x] 1. **Android scan crop fix (first, smallest end-to-end):** `AndroidPixelImageLoader.cs:45,49` use `BitmapRegionDecoder.NewInstance(local, false)` and `NewInstance(stream, false)` (confirm the binding's overloads with `library-docs` first). iOS: no change needed (no region decoder; it crops the same way by design, Slice 275), state that in the review notes.
- [x] 2. **Analyzer false positives and obsolete calls:** `[SupportedOSPlatform("android30.0")]` on `AndroidCrashHooks.ToNative` and `KindOf`; `Locale.ForLanguageTag` at `AndroidSpeechReader.cs:191,208`; fix the two crefs (`IDocumentScanner.cs:28`, `IBuiltInTextRecognizer.cs:6`).
- [x] 3. **Tests:** `_ = packs.BlockFirstDownload();` at the 8 `VoicePackSetupTests.cs` sites; `TestContext.Current.CancellationToken` at the xUnit1051 sites; `Assert.Single` at `UsagePlaybackTests.cs:96`; the nullability warnings in `BookBuilderTests.cs:315`, `LanguageAttributionTests.cs:206`, `RecordSplitTests.cs:291,293`.
- [x] 4. **Full list:** build every project and head locally (Windows tray, Dino Windows, Dino and RTST `net10.0-android` and `net10.0-ios`, all three test projects) and collect every warning from the build output, not the annotations. Fix each remaining one on the decision list; list anything left (other codes) in this file for Slice 379.
- [x] 5. **Gate:** add the `WarningsAsErrors` list to `Directory.Build.props`. Every build in item 4 must pass with it.
- [x] 6. **Actions:** bump to the current majors (checkout, setup-dotnet, setup-node, cache/restore/save, setup-msbuild, setup-python if behind, upload-google-play to `tracks:`, `microsoft-store-apppublisher` and `gittools/actions` checked for newer releases while keeping GitVersion 6.x). Remove `useConfigFile`. actionlint passes (`release-scripts.yml`). No Node 20 warning is left in a release run.
- [x] 7. **Dependabot:** `.github/dependabot.yml` with `package-ecosystem: github-actions`, weekly, one group.
- [x] 8. Rule in `.github/CLAUDE.md` (one line): warnings on the gate list fail the build; fix the code or add the platform attribute, never suppress.

## Verification

- All builds in item 4 succeed with the gate on, and the three test suites pass.
- An emulator run on an **API 28-30 image** (Android 9-11) scans a page with the RTST Scan flow and shows no `No region decoder` debug line / `ocr_crop_degraded` fallback; the same flow on the usual API 3x emulator still passes.
- iOS: `net10.0-ios` builds of both apps compile with the gate on; TestFlight build of the release succeeds.
- The first release run after merge shows no Node 20 and no compiler warning annotations (read through `ci-status`).

## Not yet verified

- The scan-crop fix on an API 28-30 emulator image: no Android 9-11 system image is installed, and no flow scans a real page, so a scan flow is needed as well.
- The first release run after merge: no Node 20 warnings, `tracks:` accepted by upload-google-play v1.1.5, GitTools v4.7.0 `configFilePath` (including `windows.yml`'s restored config), setup-node v7 caching.
- A TestFlight build with the gate on.
- Android 36 `ToggleSwitch` `CheckedState` (`ToggleSwitchAccessibility.cs`).

## Device run (2026-11-14, emulator-5554, build key a11d6be4235e; logs `reviews/s378-device/`)

- PASS: usage-fallback-packs-ocr (32 s), dino-landscape-settings (46 s).
- Environment: the machine `JAVA_HOME` points at a removed JDK (`C:\Program Files\Eclipse Adoptium\jdk-21.0.11.10-hotspot`); device agents set it per process. (Same note as Slice 291; plan-not-needed: 291.)

### Found by the device run (already on main)

Both fail the same way on main's build (79f15e56616b; `reviews/s378-device/baseline/`), so this slice did not cause them. They become planned follow-up slices in the index pass.

- FAIL rtst voice-cancel-holds-scan-pack: `Assertion is false: "در حال دانلود صداهای طبیعی.*", id: PackStatusLabel is visible`.
- FAIL dino default-view-per-book: `Assertion is false: "Picture only.*Selected" is visible`.

## Changed while building

- **Item 1 differs from the slice text.** `BitmapRegionDecoder.NewInstance(path)` / `(stream)` exist from API 31 and the `(x, false)` overloads are Java-deprecated at API 31 (library-docs, 2026-11-14), so `AndroidPixelImageLoader` now has `OpenFromPath` / `OpenFromStream`: `OperatingSystem.IsAndroidVersionAtLeast(31)` takes the new overload, else the `(x, false)` one. Neither CA1416 nor CA1422 fires (the gated builds pass, no pragma). iOS needs no change (no region decoder; Slice 275).
- **The xUnit1051 list was about 90 sites, not 7** (the annotations were capped): `Shared.Tests` (SilentOutputGuard, KokoroNanRescue, FallbackSink, StallGuardedRead, PackDownloadGate, PackDownloadLog, SingleModelCall, StallReporter, TranslationModelDownloadSession, VoicePackSetup), `ReadTheStupidText.Tests` (EarlyStart, EarlyStartWindows, TranslationStreaming, UsageService) and `DinoReader.Tests` (AudioPackStore, BackCoverPicture, BusyGuard, LatestWinsWorker, ParentRecording, PictureTextBook, RecordCommit, RecordingLanguage, ShelfOrder, StarterUpdate, StoreStory, StoryBackup, StoryPackage). All fixed with `TestContext.Current.CancellationToken`; `SilentOutputGuardTests.Rescue` now takes `CancellationToken? token = null` and passes the test token when none is given.
- **Gate hits found only by the Android, Windows and iOS builds, all fixed:** `AndroidCrashHooks.ReadTrace` (CA1416, `[SupportedOSPlatform("android30.0")]`), `MlKitTranslator`/`MlKitLanguageDetector` (CS8602, `m?.`/`OfType<>()`), `ReaderPage` PlanKey voice id (CS8604, `?? string.Empty`), Dino `Shadow = ... : null!` in `ModeSwitch`, `ReadingPage`, `StoryLanguageButton` (CS8601), `ToggleSwitchAccessibility` (CS8604 and CA1422: `CheckedState.True/False` from API 36, else `Checked`), crefs in `IBuiltInTextRecognizer`, `RecordingAudio`, `ScreenAwake`, `TestStore` (CS1574/CS0419), iOS `NlLanguageDetector` and `IosAttributes` (nullability).
- **StoreKit 1 in `IosPurchases` (about 35 CA1422 hits, iOS 18 obsoletion):** `IosPurchases.cs` has one file-scoped `#pragma warning disable CA1422` after its usings (the gate stays on for everything else). This is the one suppression, named as an exception in `.github/CLAUDE.md` and in Decisions. `RedeemCodeAsync` is `[UnsupportedOSPlatform("maccatalyst")]` (CA1416).
- `useConfigFile` removed from all three GitVersion steps (`release-shared.yml` and `dinoreader-windows.yml` keep `configFilePath`; `windows.yml` is RTST's and reads the default `GitVersion.yml`). Actions now: checkout v7, setup-dotnet v6, setup-node v7, cache v6 (+ restore/save), setup-msbuild v3, GitTools v4.7.0 (GitVersion stays `6.x`), upload-google-play v1.1.5 with `tracks: internal`. setup-python v7 and setup-java v6 were already current; `microsoft-store-apppublisher` v1.4 is the latest release. `rerun-no-runner.yml` uses `workflow_run` but has no checkout, so checkout v7's fork block does not touch it.

## From review

- Applied #1+#2 as one design (one file-scoped CA1422 suppression in `IosPurchases.cs`, see Decisions) and #4 (null-safe API 36 branch).
- Skipped #3 (`reviews/s378-high.md`): "means an SDK, MAUI workload, Android API or Xcode binding bump can fail the release pipeline." - the slice Decision says "Use `WarningsAsErrors` for that list in `Directory.Build.props`, not a blanket `TreatWarningsAsErrors`" and names these codes; a platform bump is fixed by the code change the gate exists to force.
- Skipped #5 (`reviews/s378-high.md`): "for input that is not well-formed BCP 47" (applied in the medium pass as #3: codes with an underscore are normalised first). - equivalent for today's plain two-letter codes; slice work item 2 asks for `ForLanguageTag`.
- Skipped #6 (`reviews/s378-high.md`): "`?? string.Empty` for the plan-cache key voice id satisfies the nullable gate but conflates" default and empty - coordinator triage, not applied in this pass.
- Skipped #7 (`reviews/s378-high.md`): "Run the API 28-30 emulator image before merge" - a device check, already under Not yet verified; no code change.
- Skipped #8 (`reviews/s378-high.md`): "and one major can sink the whole group" - slice Decision says "weekly, one grouped PR".
- Skipped #9 (`reviews/s378-high.md`): "silently drop null or wrong-type elements" - coordinator triage, not applied in this pass.
- Skipped #10 (`reviews/s378-high.md`): "treat this as a finding" if the builder's report names no context7 id - coordinator triage, not applied in this pass.

- Skipped #1 (`reviews/s378-medium.md`): "Any later obsolete call in the file passes silently." - the Decisions bullet "Exception (coordinator, wave 4)" records this one file-scoped suppression for StoreKit 1 until a StoreKit 2 migration.
- Skipped #2 (`reviews/s378-medium.md`): "If the input name is wrong the release goes to the default track instead of internal, or fails." - `tracks` is confirmed in the upload-google-play v1.1.5 README and action.yml (library-docs, 2026-11-14); the first release run reads it back (Not yet verified).
- Skipped #5 (`reviews/s378-medium.md`): "Check PlanKey.VoiceId's declared type." - low severity; same coordinator triage as #6 of the high pass.
- Skipped #6 (`reviews/s378-medium.md`): "The node can be half-initialised, and screen readers or UI tests that read the legacy path may miss the state." - as high #4 (applied), nothing to set up without a host; the rest is low.
- Skipped #7 (`reviews/s378-medium.md`): "only affects projects that enable" - low severity; comment wording only, no behaviour change.
- Skipped #9 (`reviews/s378-medium.md`): "near-duplicates with a duplicated API-level guard" - low severity; refactor beyond the warnings gate.
- Skipped #10 (`reviews/s378-medium.md`): "silently drop null entries" - low severity; same coordinator triage as #9 of the high pass.

- Skipped #1 (`reviews/s378-check.md`): "with no restore; slice rule says never suppress" - the Decisions bullet "Exception (coordinator, wave 4)" records this one file-scoped CA1422 suppression for StoreKit 1.
- Skipped #2 (`reviews/s378-check.md`): "suppresses the nullable warning" - null clears a Shadow on purpose (checked at each site in the medium pass, #4).
- Skipped #3 (`reviews/s378-check.md`): "still use the raw code" - every caller passes two-letter codes (`AppLanguages.Code`, `StoryLanguages`).
- Skipped #4 (`reviews/s378-check.md`): "yields root locale" - low severity, not applied.
- Skipped #5 (`reviews/s378-check.md`): "returns null silently" - null means not detected, which callers already handle.
- Skipped #6 (`reviews/s378-check.md`): "were not confirmed against context7 or a workflow run" - versions read from `gh api` on 2026-11-14 and each step's config checked by the medium fixer; the first release run is under Not yet verified.
- Skipped #7 (`reviews/s378-check.md`): "share one plan-cache key" - low severity, not applied.
- Skipped #8 (`reviews/s378-check.md`): "duplicate the API 31 split" - low severity, not applied.
- Skipped #9 (`reviews/s378-check.md`): "Base OnInitializeAccessibilityNodeInfo now skipped" - low severity, not applied.

## Left for Slice 379 (warnings with other codes, seen in the item 4 builds)

- Tests: xUnit2029 (`LifecycleSubscriptionTests.cs:82`), xUnit2000 (`ListenTrackTests.cs:30`, `RecordingLanguageTests.cs:270`), xUnit1026 (`TabletCardLayoutTests.cs:25`), CA2022 (`StallGuardedReadTests`, `PackDownloadLogTests:77`: inexact `ReadAsync`), xUnit1031 (`ReadAloudServiceGeneratingTests.cs:843`).
- Builds: MSB3277 (30, Windows head: `Microsoft.ML.OnnxRuntime` 0.0.0.0 vs 1.28.0.0 conflict in Dino Windows), MSB4011 (`SherpaOnnxVersion.props` imported twice, every head), NU1608 (20, `Xamarin.AndroidX.Lifecycle.LiveData.Core` 2.11.0.1 vs a `< 2.9.3` constraint, both Android heads), XA4301 (4, RTST Android), CS0108 (Dino, Windows and iOS), CS1734 (6, Dino Android), CS0414 and CS9113 (Dino iOS), CS0618 (2, RTST iOS).
- Move Dino iOS purchases off StoreKit 1 (the `IosPurchases` obsoletion and the one pragma).
