# Slice 58 — Dino Reader skeleton: shared Mobile library, new app, pipeline

*Planned.*

**Batch:** 10
**Decisions:** 72, 73, 81
**Depends on:** PR #165 (Batch 8 CI, still open) for the pipeline steps; Slice 44 (platform-neutral Mobile root) for the move; Slice 48 for the iOS target of the new library
**Touches:** `src/ReadTheStupidText.Mobile/**`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/DinoReader.App/**`, `ReadTheStupidText.slnx`, `.github/workflows/build.yml`, `.github/scripts/**`, `STORE.md`, `CLAUDE.md`
**Story issue:** —

## Goal

A second MAUI app, **Dino Reader** (`src/DinoReader.App`, id
`uk.sirous.dinoreader`), builds from this repo next to Read The Stupid Text
Mobile, with one page: type text, read it with one voice. The platform code both
apps need moves into a shared MAUI class library, and Read The Stupid Text Mobile
behaves exactly as before.

## Steps

- [ ] Create `src/ReadTheStupidText.Mobile.Shared` (MAUI class library,
      `net10.0-android` now; `net10.0-ios` is added when Slice 48 lands, reusing
      its `Platforms/iOS` layout). References Domain, Application, Documents.
- [ ] Move out of `src/ReadTheStupidText.Mobile` into it (keep the
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
- [ ] Move the Android packaging block (`ReadTheStupidText.Mobile.csproj` lines
      86-106 and 115-116: sherpa-onnx, the seven `runtime.*` packages with
      `ExcludeAssets="all"`, `GeneratePathProperty`, the explicit
      `AndroidNativeLibrary Abi="arm64-v8a"` items, the ML Kit packages) into the
      shared library csproj. **The CLAUDE.md gotcha applies:** after the build,
      unzip **Dino Reader's** `.apk` under `obj/Debug/net10.0-android/android/bin/`
      and check `lib/arm64-v8a/` holds the Android `libonnxruntime.so` and
      `libsherpa-onnx-c-api.so`; repeat for the RTST APK. A clean build is not proof.
- [ ] The voice-model `MauiAsset` link (`..\ReadTheStupidText.App\VoiceModel\*`,
      csproj line 67) stays in each app project (assets are app-level). Dino
      Reader links the same files until Slice 50 removes bundling.
- [ ] New `src/DinoReader.App` (MAUI, `net10.0-android`, `ApplicationId`
      `uk.sirous.dinoreader`, display name "Dino Reader", the real icon (below),
      `AppShell` + one page: editor, Play/Stop, `ISpeechReader` with the default
      voice). References the shared library, Domain, Application.
- [ ] **App icon (Android):** use the ready-made set in
      `docs/design/dino-reader/icons/android/` ("Book hug" design): copy
      `mipmap-*/` and `mipmap-anydpi-v26/` into
      `src/DinoReader.App/Platforms/Android/Resources/` and point the manifest's
      `android:icon`/`android:roundIcon` at `@mipmap/ic_launcher(_round)` instead
      of a generated `MauiIcon` (exact MAUI wiring confirmed via context7 at
      implementation; don't let `MauiIcon` overwrite the adaptive icon).
      `play-store-512.png` is the Play Console listing icon (manual upload).
- [ ] `ReadTheStupidText.slnx`: add both new projects (line 23 is the Mobile entry to copy).
- [ ] **Pipeline** (`build.yml`, after PR #165 merges): new path-filter group
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
