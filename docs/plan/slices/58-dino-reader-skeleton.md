# Slice 58 — Dino Reader skeleton: shared Mobile library, new app, pipeline

*Built (wave 2026-10-21), PR #191. Not yet verified: the Play Console and signing steps below.*

**Batch:** 10
**Decisions:** 72, 73, 81
**Depends on:** PR #165 (Batch 8 CI, still open) for the pipeline steps; Slice 44 (platform-neutral Mobile root) for the move; Slice 48 for the iOS target of the new library
**Touches:** `src/ReadTheStupidText.Mobile/**`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/DinoReader.App/**`, `src/AndroidSherpaPackaging.props`, `src/ReadTheStupidText.Application/ReadTheStupidText.Application.csproj`, `ReadTheStupidText.slnx`, `.github/workflows/**` (`android.yml`, `ios.yml`, `release.yml`, `tests.yml`, `dinoreader-android.yml`), `.github/scripts/**` (`ship-targets.mjs` and test, `stage-android-package.sh`, `check-android-natives.sh`), `STORE.md`, `CLAUDE.md`
**Story issue:** —

## Goal

A second MAUI app, **Dino Reader** (`src/DinoReader.App`, id
`uk.sirous.dinoreader`), builds from this repo next to Read The Stupid Text
Mobile, with one page: type text, read it with one voice. The platform code both
apps need moves into a shared MAUI class library, and Read The Stupid Text Mobile
behaves exactly as before.

## Steps

- [x] Create `src/ReadTheStupidText.Mobile.Shared` (MAUI class library,
      `net10.0-android` now; `net10.0-ios` is added when Slice 48 lands, reusing
      its `Platforms/iOS` layout). References Domain, Application, Documents.
- [x] Move out of `src/ReadTheStupidText.Mobile` into it (keep the
      `ReadTheStupidText.Mobile` namespace, so the Android namespace gotcha in
      CLAUDE.md stays true): `CompositeSpeechReader.cs`,
      `MobileVoiceModelService.cs`, `MobilePreferencesSettingsStore.cs`,
      `Platforms/Android/AndroidSpeechReader.cs`,
      `Platforms/Android/AndroidSupertonicSpeechReader.cs`,
      `Platforms/Android/MlKitImageTextExtractor.cs`. Stay in the RTST app:
      pages, `AppShell`, `MauiProgram.cs`, `MainActivity.cs`, `MainApplication.cs`,
      manifest, resources. Add an `AddMobileSharedServices(this MauiAppBuilder)`
      extension in the library that registers readers/OCR/voice/settings, so both
      `MauiProgram.cs` files stay thin. If Slice 44 already moved
      `CompositeSpeechReader` into Application, drop it from this list.
- [x] Move the Android packaging block (`ReadTheStupidText.Mobile.csproj` lines
      86-106 and 115-116: sherpa-onnx, the seven `runtime.*` packages with
      `ExcludeAssets="all"`, `GeneratePathProperty`, the explicit
      `AndroidNativeLibrary Abi="arm64-v8a"` items, the ML Kit packages) into the
      shared library csproj. **The CLAUDE.md gotcha applies:** after the build,
      unzip **Dino Reader's** `.apk` under `obj/Debug/net10.0-android/android/bin/`
      and check `lib/arm64-v8a/` holds the Android `libonnxruntime.so` and
      `libsherpa-onnx-c-api.so`; repeat for the RTST APK. A clean build is not proof.
- [x] The voice-model `MauiAsset` link (`..\ReadTheStupidText.App\VoiceModel\*`,
      csproj line 67) stays in each app project (assets are app-level). Dino
      Reader links the same files until Slice 50 removes bundling.
- [x] New `src/DinoReader.App` (MAUI, `net10.0-android`, `ApplicationId`
      `uk.sirous.dinoreader`, display name "Dino Reader", Android
      `SupportedOSPlatformVersion` 28.0 (Decision 82, not the template's 21),
      the real icon (below),
      `AppShell` + one page: editor, Play/Stop, `ISpeechReader` with the default
      voice). References the shared library, Domain, Application.
- [x] **App icon (Android):** use the ready-made set in
      `docs/design/dino-reader/icons/android/` ("Book hug" design): copy
      `mipmap-*/` and `mipmap-anydpi-v26/` into
      `src/DinoReader.App/Platforms/Android/Resources/` and point the manifest's
      `android:icon`/`android:roundIcon` at `@mipmap/ic_launcher(_round)` instead
      of a generated `MauiIcon` (exact MAUI wiring confirmed via context7 at
      implementation; don't let `MauiIcon` overwrite the adaptive icon).
      `play-store-512.png` is the Play Console listing icon (manual upload).
- [x] `ReadTheStupidText.slnx`: add both new projects (line 23 is the Mobile entry to copy).
- [x] **Pipeline** (`build.yml`, after PR #165 merges): new path-filter group
      `dinoreader` = `src/DinoReader.App/**` (+ `src/DinoReader.Core/**` from
      Slice 59). `src/ReadTheStupidText.Mobile.Shared/**` is a shared path and
      rebuilds both Android apps; core paths rebuild everything. Add a Dino Reader
      Android build job that uploads to the draft Release (no `upload-artifact`,
      Decision 52) and a Play **internal track** submit step. An untouched product's
      assets carry forward.
- [ ] **Manual (user):** create the Dino Reader app in Play Console
      (`uk.sirous.dinoreader`). A **separate upload key and Play service-account
      secrets** for it, generated and stored with `manage-secrets`, never the RTST ones.
- [ ] `CLAUDE.md`: update the layered-structure block (`Mobile.Shared`,
      `DinoReader.App`, `DinoReader.Core`), the Mobile bullet (platform code now
      in `Mobile.Shared`), and the Batch 8 path-filter note (the `dinoreader` group).
      Lifted by the index pass after the merge (see `#### For CLAUDE.md` below).

#### Design (Dino Reader v2 mock, shared tokens, added 2026-10-20)

Source: `docs/design/dino-reader/Dino Reader v2.dc.html`, intro and fonts note at lines 18-21, tokens read from every screen. The mock has 17 frames; this slice owns no screen, only the tokens every later slice reuses. The frames are 360x760 phone mocks (tablet 1000x640), drawn as HTML with a fake status bar ("9:41", "100%") that is not UI. Fonts load from Google Fonts in the mock; the app must **bundle** them (everything stays on the device). The mock's intro says to list the licences in `STORE.md`.

- **Fonts.** UI text `Fredoka` (400/500/600/700). Reading text `Andika` (400/700). Farsi and Arabic text `Vazirmatn` (the RTL frames set `font-family:'Vazirmatn',system-ui,sans-serif`; weights seen 600/700). All three are SIL OFL 1.1. Chinese and Japanese use the system font (no bundled file). The mock’s Georgia is only a stand-in for a scanned page, not UI. Put the font files in `src/DinoReader.App/Resources/Fonts/` (already inside the Touches glob).
- **Colours (child).** Page `#FFF6E6`; ink `#2b2540`; muted `#8a8399`; body-muted `#5a5470`; brand `#5B57E8` (pressed/under-edge `#3f3bc4`); brand tint `#ECE7FF`; play coral `#FF7A59` (under-edge `#E2583A`); card `#fff`; inactive dot `#d9d2c4`.
- **Colours (grown-up screens).** Page `#F3EFE6` (gallery manage mode, add, cast, merge); cards `#fff`; dividers `#efe9dd`; danger `#E5484D`; success tick `#7BE495`.
- **Bedtime theme** (screen 12, lines 35-36): page `#1E1B2E`; reading card `#2A2640`; text `#F3E9D2`; chrome icons/text `#cfc6e6`; muted `#8e86a8`; accent `#A9A2FF` (under-edge `#7c74d9`); play `#C65A43` (under-edge `#9E4332`, glyph `#FFE9D9`); dots `#4a4463`; highlight fill `rgba(255,214,120,.22)` with inset 2px `#C9A54A`.
- **Touch sizes.** Top-bar buttons 44x44, radius 14. Transport: play 104 circle, skip 72 circle (tablet 120 and 80).
- **App icon** in the gallery header is `icons/ios/AppIcon-1024-light.png` at 40x40, radius 11.

**Settled by the mock: bundled fonts.** The intro (line 21) names Fredoka, Andika and Vazirmatn, all OFL 1.1, bundled in the app with licences listed in `STORE.md` (already in Touches). Register them in `MauiProgram`; CJK falls back to the system font. Pages are never tilted (see Slice 64).

## Acceptance

- `dotnet build` of `ReadTheStupidText.Mobile.csproj` and of `DinoReader.App`
  (`-f net10.0-android`) succeed; both APKs hold the Android sherpa libraries and
  no linux/win/osx `.so`.
- Read The Stupid Text Mobile: type, scan/file, voice pick and read behave as before (device check).
- Dino Reader on a device: type text, Play reads it with one voice.
- A change only under `src/DinoReader.App/**` builds and submits Dino Reader and
  nothing else; a change under `Mobile.Shared/**` builds both; the Play internal
  track shows the new app.

## Risks / notes

- The move is the risky part: do it as one commit with no behaviour change, before any new code.
- Keep the `ReadTheStupidText.Mobile` namespace in moved Android files (the `Android`-segment shadowing gotcha).
- Slices 45 and 52 edit `MlKitImageTextExtractor` and `Mobile/**`; sequence this slice before them or rebase them onto the new paths.

## Built notes

*Changed while building:*

- `Mobile.Shared` targets `net10.0-android` plus a conditional `net10.0-ios` (same `IncludeIos`/`IncludeAndroid` logic as the RTST app), so the iOS target did not wait for Slice 48. It also holds the voice pack services, `VoicePackInstaller`, `PackStatusText`, `OffsetSkip`, `IPlatformServices` and its Android/iOS implementations (registered only by `MobileSharedServices.RegisterPlatformServices`), not just the files listed in the steps. `Application` grants `InternalsVisibleTo` to `Mobile.Shared`.
- OCR (ML Kit text recognition, `MlKitImageTextExtractor`, the iOS stub) and the document scanner stayed in the RTST app, so the Dino APK carries no OCR library.
- The sherpa-onnx packaging moved to `src/AndroidSherpaPackaging.props` (sherpa-onnx, the seven `ExcludeAssets` runtime packages, `GeneratePathProperty`), imported by `Mobile.Shared`, RTST and `DinoReader.App`. The `AndroidNativeLibrary` arm64-v8a items are staged only in app projects (`AndroidApplication=true`) so the `.so` files land once.
- `DinoReader.App` (`uk.sirous.dinoreader`, minSdk 28) has one page: editor, Play/Stop (per-read pending guard, stop during generation, no alert on cancel), pack status with tap-to-retry, and a startup that forces English and installs the pack, reporting failures. "Book hug" mipmap icons are wired in the manifest; Fredoka/Andika/Vazirmatn (OFL) are bundled with licences; `allowBackup=false`.
- Pipeline: the path-filter group is the `ship-targets` target `dinoapp` (`src/DinoReader.App/**`), not a `dinoreader` group in `build.yml`. `Mobile.Shared` root changes go to android+ios+dinoapp; `Mobile.Shared/Platforms/Android`, the sherpa props and the stage script go to android+dinoapp; `Mobile.Shared/Platforms/iOS` goes to ios; core goes to all. New `dinoreader-android.yml` (unsigned compile and Release AAB native check on PRs; signed `DinoReader-<v>.aab`/`.apk` uploaded to the draft on releases), `release.yml` `dino_signed` (needs all five `DINOREADER_*` secrets), `build-dinoapp`, `release-publish` gating, `play-submit-dino` (internal track), and carry-forward by exact name prefixes (`ReadTheStupidText-*`, `DinoReader-*`).
- New `.github/scripts/check-android-natives.sh` runs after every Android/Dino package build in CI and fails if `libonnxruntime.so` or `libsherpa-onnx-c-api.so` is missing from `lib/arm64-v8a`.
- `DinoReader.Core` does not exist yet (Slice 59), so the `dinoapp` target does not list it.

*From review:*

- High: Dino retry tap, double-staged natives, duplicate `IPlatformServices` registration, fire-and-forget startup, carry-forward regex keyed on the first letter, duplicate `Fragment.Ktx` pin, stale doc comment, OCR library in the Dino APK, one-liners.
- Medium: Dino play re-entrancy and stop during generation, alert on cancel, event leaks, Release-packaging natives unverified (now verified locally for both apps' Release AAB+APK and guarded in CI), redundant Debug build on releases, `dino_signed` without the Play secret, `allowBackup` on the kids' app.

*Verified:* RTST Android and iOS compile, Dino Android compile, slnx, both test suites, 69 script tests, actionlint. Release AAB+APK of both apps contain the real AArch64 `libonnxruntime.so` and `libsherpa-onnx-c-api.so`. Emulator (RTST_Test): Dino Reader launched, downloaded the voice pack and read typed text with the neural voice; RTST after the move went language page, pack, read with the neural voice, and Scan is still shown.

#### For CLAUDE.md (lifted 2026-10-21)

- Layered structure gets `Mobile.Shared` (shared platform code for both MAUI apps, namespace `ReadTheStupidText.Mobile`, registered with `AddMobileSharedServices`; `Application` grants it `InternalsVisibleTo`) and `DinoReader.App` (`net10.0-android`, `uk.sirous.dinoreader`). Arrows: `Mobile → Application/Domain/Documents/Mobile.Shared`, `DinoReader.App → Application/Domain/Mobile.Shared`. (Slice 58: docs/plan/slices/58-dino-reader-skeleton.md)
- Native-library gotcha: the sherpa packaging lives in `src/AndroidSherpaPackaging.props` and every project that links the engine must import it (`ExcludeAssets` does not flow through `ProjectReference`). Natives are staged only in app projects. CI's `.github/scripts/check-android-natives.sh` fails a build whose AAB/APK lacks them. OCR stays RTST-only. (Slice 58: docs/plan/slices/58-dino-reader-skeleton.md)
- Pipeline: the `dinoapp` ship target plus `dinoreader-android.yml`; assets are `DinoReader-<v>.aab`/`.apk`; it uses the `DINOREADER_*` secrets, never the RTST ones; a Dino-only change without all five secrets cuts no release. (Slice 58: docs/plan/slices/58-dino-reader-skeleton.md)

#### For README.md (lifted 2026-10-21)

- Dino Reader (Android, in development): a kids' story reader that reads with a voice per character, builds from `src/DinoReader.App`.

#### Not yet verified

- Play Console app creation and the first manual internal-track upload for `uk.sirous.dinoreader`.
- Dino upload key and Play service-account secrets (`DINOREADER_*`) generated and stored.
- A signed Dino release cut by CI.
