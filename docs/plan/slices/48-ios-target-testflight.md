# Slice 48 — iOS target and TestFlight from CI

*Built (wave 2026-10-20c), PR #186. Not yet verified: the first signed build and TestFlight upload, and runtime behaviour on an iPhone.*

**Batch:** 9
**Decisions:** 70, 69, 37
**Depends on:** Slice 44 (the `ios` filter output and platform-neutral Mobile root), which itself depends on **PR #165** merging.
**Touches:** `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `src/ReadTheStupidText.Mobile/Platforms/iOS/**`, `src/ReadTheStupidText.Mobile/Platforms/Android/AndroidSpeechReader.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/AndroidPlatformServices.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/IPlatformServices.cs`, `src/ReadTheStupidText.Mobile/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml`, `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ScanPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/OffsetSkip.cs`, `src/ReadTheStupidText.Mobile/OcrUnavailableException.cs`, `.github/workflows/build.yml`, `.github/scripts/ios-signing.sh`, `.github/scripts/testflight-upload.sh`, `.github/scripts/path-filters.test.mjs`, `STORE.md`, `CLAUDE.md`
**Story issue:** —

## Goal

Every merge to `main` that touches iOS (or shared MAUI/core code) lands a
build in TestFlight, signed with the Lets Call Mom Apple team but under this
app's own App ID. The first iOS build only needs to launch and show the shell;
feature parity is Slice 49.

## Steps

- [x] **Manual (user), before CI can sign:** in the Lets Call Mom Apple
      Developer team create App ID `uk.sirous.readthestupidtext`; create the App
      Store Connect app record (name "Read The Stupid Text", bundle id above,
      SKU `readthestupidtext`); create an *App Store* distribution provisioning
      profile for it; create a new App Store Connect API key (App Manager). Walk
      the user through it one step at a time (guide-step-by-step).
- [x] Store these via `manage-secrets` (`D:\secrets`) and set them as this
      repo's Actions secrets, named like Lets Call Mom's:
      `IOS_DIST_CERT_P12_BASE64`, `IOS_DIST_CERT_P12_PASSWORD` (the shared
      Apple Distribution certificate, exported once), `IOS_PROVISIONING_PROFILE_BASE64`,
      `ASC_API_KEY_P8_BASE64`, `ASC_API_KEY_ID`, `ASC_API_ISSUER_ID`. Same six
      gate `HAS_IOS_SIGNING` as in `D:\Lets-Call-Mom\.github\workflows\ios.yml`.
- [x] `ReadTheStupidText.Mobile.csproj` (currently `<TargetFrameworks>net10.0-android</TargetFrameworks>`,
      line 6): add `net10.0-ios`. Replace the Android-only comment (lines 4-5)
      with a pointer to Decision 70. Set an iOS `SupportedOSPlatformVersion`
      (confirm the minimum via context7 at implementation) beside line 37.
- [x] `Condition` every Android-only item on
      `'$(TargetFramework)' == 'net10.0-android'` (or the platform identifier
      check already used on line 37): the sherpa `AndroidNativeLibrary` group
      (lines 114-117), `Xamarin.Google.MLKit.TextRecognition` and
      `Xamarin.GooglePlayServices.Tasks` (lines 105-106), the seven
      `ExcludeAssets` runtime packages if they are Android-only, and the
      `MauiAsset` VoiceModel stand-in (lines 58-67, until Slice 50 deletes it).
- [ ] **sherpa-onnx on iOS (research + verify).** The `org.k2fsa.sherpa.onnx`
      package's runtime sub-packages cover win/linux/osx/android only (see the
      comment at csproj lines 74-85), so iOS likely needs the sherpa-onnx iOS
      xcframeworks (and onnxruntime's) linked as `NativeReference` items. Confirm
      the supported route via context7 `/k2-fsa/sherpa-onnx`, add it, then
      **unzip the built `.ipa`** and check `Frameworks/` for the real
      arm64 binaries (same lesson as the Android `.so` gotcha). If it cannot be
      linked within this slice, ship with the AVSpeech reader only and record the
      neural gap for Slice 49.
- [x] `Platforms/iOS/`: `AppDelegate.cs`, `Program.cs`, `Info.plist`
      (display name, bundle id, `UILaunchScreen`, `ITSAppUsesNonExemptEncryption`
      = false), `Entitlements.plist`; MAUI templates give the shape (confirm via
      context7 at implementation).
- [x] `Platforms/iOS/AvSpeechReader.cs`: an `ISpeechReader` over
      `AVSpeechSynthesizer`, mirroring `Platforms/Android/AndroidSpeechReader.cs`
      (state, progress, timing, chunk events; speed via `Rate`; skip best-effort
      or no-op). Register it, and an iOS image-text extractor stub that throws a
      clear message until Slice 49, through the factory from Slice 44.
- [x] `build.yml`: add `build-ios` (runs-on a macOS runner — current label as
      in `ios.yml`, `macos-26`; `needs: [changes, version, test, release-draft]`;
      `if: needs.changes.outputs.ios == 'true'`): `dotnet publish
      src/ReadTheStupidText.Mobile -f net10.0-ios -c Release` with
      `ApplicationDisplayVersion` = the shared version and `ApplicationVersion` =
      a build number that only goes up (e.g. a UTC timestamp, as Lets Call Mom
      does), keychain + profile install and cleanup steps adapted from
      `ios.yml` (lines 120-170 there), `CodesignKey`/`CodesignProvision` passed
      on the command line. Upload the `.ipa` to the draft release.
- [x] Add a `testflight-submit` job after `release-publish` (gated like
      `play-submit`, only when `ios` was built): upload the `.ipa` via the App
      Store Connect API (`xcrun altool --upload-app --apiKey ... --apiIssuer ...`
      or Lets Call Mom's `.github/scripts/testflight-distribute.mjs` adapted for
      this app id; decide at implementation which is simpler). Distributing to a
      tester group stays a manual step in App Store Connect (App Store release
      by hand, Decision 70).
- [x] Extend `release-draft` carry-forward and `release-publish` checks for the
      `.ipa` name chosen above (placeholder added in Slice 44).
- [x] `STORE.md`: a short *iOS / TestFlight* section (identity, secrets, the
      manual steps above). `CLAUDE.md`: Mobile bullet and CI note gain iOS.

## Acceptance

- `dotnet build src/ReadTheStupidText.Mobile -f net10.0-android` still passes.
- A merge touching `Platforms/iOS/**` triggers only `build-ios` (and a TestFlight
  upload), not Android.
- The build appears in App Store Connect -> TestFlight and installs on the
  user's iPhone; launch shows the Reader shell and AVSpeech reads typed text.
- Unzipped `.ipa` contains the intended native frameworks (or the gap is recorded).

## Risks / notes

- Apple processing takes up to ~30 minutes; do not hold the pipeline for it.
- macOS runner minutes cost ~10x Linux: the path filter is what keeps this affordable.
- The distribution certificate is team-wide, so reusing it from Lets Call Mom is
  fine; the profile and ASC key are per app (Decision 70).
- No `actions/upload-artifact` (Batch 8): the `.ipa` goes to the Release.

## Built

Apple side (done 2026-10-20 by the coordinator with the App Store Connect API key shared with Lets Call Mom): bundle id `uk.sirous.readthestupidtext` (ZZ9SG4TKRW) registered; App Store profile "RTST App Store" (7DLP3V6ACY, expires 2027-10-11) created and stored in the secrets store; the user created the App Store Connect app record (6825052929, SKU `readthestupidtext`); the six repo secrets are set (`IOS_DIST_CERT_P12_BASE64`, `IOS_DIST_CERT_P12_PASSWORD`, `IOS_PROVISIONING_PROFILE_BASE64`, `ASC_API_KEY_P8_BASE64`, `ASC_API_KEY_ID`, `ASC_API_ISSUER_ID`). Also prepared for Dino Reader: bundle id `uk.sirous.dinoreader` (4FC99W8D33), profile "Dino Reader App Store" (WV7MF55ZZ5), app record 6825053435, all in the secrets store.

Code:

- `net10.0-ios` is in the Mobile csproj only on macOS or with `-p:IncludeIos=true`; `-p:IncludeAndroid=false` drops Android for the CI iOS build; a clear error if no TFM remains. sherpa/ML Kit refs are Android-only.
- `Platforms/iOS`: `AppDelegate`, `Program`, `MauiProgram.iOS` (required `RegisterPlatformServices`), `IOSPlatformServices` (one shared `AvSpeechReader` for both slots; `SupportsNeuralVoice=false`, `SupportsTextScan=false`; stub OCR throwing `OcrUnavailableException`), `Info.plist` (camera, photo-library and microphone purpose strings, `ITSAppUsesNonExemptEncryption=false`, iOS 15+), `Entitlements`, `PrivacyInfo`.
- Shared `OffsetSkip` helper used by the Android and iOS system-voice readers. `VoicePackInstaller` skips the pack on platforms without neural voice; `VoicePickerPage` hides the Supertonic lists and `ReaderPage` hides Scan when unsupported; the `ReaderPage` file picker gains iOS UTIs.
- CI: `release-draft` computes `ios_signed` from the six secrets; `version` runs for windows/android or iOS on main; `test` (Windows) only for windows/android; `build-ios` on macos-26 (unsigned simulator compile unless a signed release; signed: temp keychain via `ios-signing.sh` with the identity's SHA-1, `dotnet publish -f net10.0-ios -p:RuntimeIdentifier=ios-arm64 -p:ArchiveOnBuild=true -p:IncludeAndroid=false`, `CFBundleVersion` = UTC `yyyymmdd.hhmmss`, `.ipa` uploaded to the draft and carried forward); `release-publish` waits for `build-ios` only when signed; `testflight-submit` uploads with altool after publish (fails loudly if the `.ipa` is missing; checks altool output because it can exit 0 on failure). An iOS-only change without secrets cuts no release.

*Changed while building:* sherpa-onnx on iOS was not attempted: iOS ships the AVSpeech reader only and the neural gap is left to Slice 49.

*From review:* high review: restore needed the Android workload on macOS (fixed via `IncludeAndroid=false`), iOS file picker crash, iOS downloading the useless 145 MB pack, missing photo-library purpose strings (ITMS-90683), OCR stub message unreachable, always-on ios-signing job removed, duplicated skip logic shared, dead voice lookup. Medium review: `IOS_SIGNED` never set (would have failed every iOS release), identity by SHA-1, TestFlight and cleanup failure modes, voice reset, pause+skip, audio session deactivation, single synthesizer, test/version gating, empty TFM error, Scan hidden up front.

*Verified (Windows):* Android build, iOS C# compile (with and without `IncludeAndroid=false`), 180 tests, actionlint, filter test. Emulator: Android still reads aloud with the neural voice and Scan still shows after the shared changes.

#### For CLAUDE.md

- Mobile: `Platforms/iOS` implements `IPlatformServices` with one `AvSpeechReader` for both slots (sherpa-onnx for iOS is Slice 49), `SupportsNeuralVoice`/`SupportsTextScan` false (the pack download, Supertonic voice list and Scan are hidden); `net10.0-ios` is in the csproj only on macOS or with `-p:IncludeIos=true` (`-p:IncludeAndroid=false` for an iOS-only build); compile-check iOS on Windows with `dotnet restore -p:IncludeIos=true` then `dotnet build -f net10.0-ios -p:IncludeIos=true --no-restore`; sherpa/ML Kit refs are Android-only. (Slice 48: docs/plan/slices/48-ios-target-testflight.md)
- CI: `build-ios` (macos-26) compiles unsigned on PRs; on main with the six iOS secrets it signs (keychain via `.github/scripts/ios-signing.sh`, identity by SHA-1), attaches the `.ipa` to the draft, and `testflight-submit` uploads it with altool after `release-publish`. iOS `CFBundleVersion` is a UTC timestamp. Signing scripts must run on macOS bash 3.2. App Store Connect setup and the shared signing assets are recorded in the secrets store (`readthestupidtext/`, `dinoreader/`); the ASC API script for bundle ids and profiles lives in the coordinator's scratchpad. (Slice 48: docs/plan/slices/48-ios-target-testflight.md)

#### For README.md

- iOS: Read The Stupid Text ships to TestFlight from CI (testers added in App Store Connect).

#### Not yet verified

- The first signed `build-ios` and TestFlight upload (happens on this PR's merge).
- Installing from TestFlight on an iPhone.
- The iOS app's runtime behaviour (AVSpeech reading, file picker); no Mac or iPhone here.
