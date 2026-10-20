# Slice 48 — iOS target and TestFlight from CI

*Planned.*

**Batch:** 9
**Decisions:** 70, 69, 37
**Depends on:** Slice 44 (the `ios` filter output and platform-neutral Mobile root), which itself depends on **PR #165** merging.
**Touches:** `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`, `src/ReadTheStupidText.Mobile/Platforms/iOS/**`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `.github/workflows/build.yml`, `.github/scripts/**`, `STORE.md`, `CLAUDE.md`
**Story issue:** —

## Goal

Every merge to `main` that touches iOS (or shared MAUI/core code) lands a
build in TestFlight, signed with the Lets Call Mom Apple team but under this
app's own App ID. The first iOS build only needs to launch and show the shell;
feature parity is Slice 49.

## Steps

- [ ] **Manual (user), before CI can sign:** in the Lets Call Mom Apple
      Developer team create App ID `uk.sirous.readthestupidtext`; create the App
      Store Connect app record (name "Read The Stupid Text", bundle id above,
      SKU `readthestupidtext`); create an *App Store* distribution provisioning
      profile for it; create a new App Store Connect API key (App Manager). Walk
      the user through it one step at a time (guide-step-by-step).
- [ ] Store these via `manage-secrets` (`D:\secrets`) and set them as this
      repo's Actions secrets, named like Lets Call Mom's:
      `IOS_DIST_CERT_P12_BASE64`, `IOS_DIST_CERT_P12_PASSWORD` (the shared
      Apple Distribution certificate, exported once), `IOS_PROVISIONING_PROFILE_BASE64`,
      `ASC_API_KEY_P8_BASE64`, `ASC_API_KEY_ID`, `ASC_API_ISSUER_ID`. Same six
      gate `HAS_IOS_SIGNING` as in `D:\Lets-Call-Mom\.github\workflows\ios.yml`.
- [ ] `ReadTheStupidText.Mobile.csproj` (currently `<TargetFrameworks>net10.0-android</TargetFrameworks>`,
      line 6): add `net10.0-ios`. Replace the Android-only comment (lines 4-5)
      with a pointer to Decision 70. Set an iOS `SupportedOSPlatformVersion`
      (confirm the minimum via context7 at implementation) beside line 37.
- [ ] `Condition` every Android-only item on
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
- [ ] `Platforms/iOS/`: `AppDelegate.cs`, `Program.cs`, `Info.plist`
      (display name, bundle id, `UILaunchScreen`, `ITSAppUsesNonExemptEncryption`
      = false), `Entitlements.plist`; MAUI templates give the shape (confirm via
      context7 at implementation).
- [ ] `Platforms/iOS/AvSpeechReader.cs`: an `ISpeechReader` over
      `AVSpeechSynthesizer`, mirroring `Platforms/Android/AndroidSpeechReader.cs`
      (state, progress, timing, chunk events; speed via `Rate`; skip best-effort
      or no-op). Register it, and an iOS image-text extractor stub that throws a
      clear message until Slice 49, through the factory from Slice 44.
- [ ] `build.yml`: add `build-ios` (runs-on a macOS runner — current label as
      in `ios.yml`, `macos-26`; `needs: [changes, version, test, release-draft]`;
      `if: needs.changes.outputs.ios == 'true'`): `dotnet publish
      src/ReadTheStupidText.Mobile -f net10.0-ios -c Release` with
      `ApplicationDisplayVersion` = the shared version and `ApplicationVersion` =
      a build number that only goes up (e.g. a UTC timestamp, as Lets Call Mom
      does), keychain + profile install and cleanup steps adapted from
      `ios.yml` (lines 120-170 there), `CodesignKey`/`CodesignProvision` passed
      on the command line. Upload the `.ipa` to the draft release.
- [ ] Add a `testflight-submit` job after `release-publish` (gated like
      `play-submit`, only when `ios` was built): upload the `.ipa` via the App
      Store Connect API (`xcrun altool --upload-app --apiKey ... --apiIssuer ...`
      or Lets Call Mom's `.github/scripts/testflight-distribute.mjs` adapted for
      this app id; decide at implementation which is simpler). Distributing to a
      tester group stays a manual step in App Store Connect (App Store release
      by hand, Decision 70).
- [ ] Extend `release-draft` carry-forward and `release-publish` checks for the
      `.ipa` name chosen above (placeholder added in Slice 44).
- [ ] `STORE.md`: a short *iOS / TestFlight* section (identity, secrets, the
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
