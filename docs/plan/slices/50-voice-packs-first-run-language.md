# Slice 50 — Voice packs, first-run language and a slimmer app (Android)

*Built (wave 2026-10-20), PR #173.*

**Batch:** 9
**Decisions:** 60, 61, 62, 68 (first-run choice only; UI translation is Slice 53)
**Depends on:** none to start (Android). If it merges before Slice 44, the `android` filter edit below is still correct. Pipeline edits depend on **PR #165** merging.
**Touches:** `docs/voice-packs.md`, `src/ReadTheStupidText.Mobile/**` (csproj, `MobileVoiceModelService.cs`, `MauiProgram.cs`, `App.xaml.cs`, new first-run page, `Platforms/Android/AndroidManifest.xml`), `src/ReadTheStupidText.Application/Reading/**`, `src/ReadTheStupidText.Application/Settings/ISettingsStore.cs`, `src/ReadTheStupidText.Mobile/MobilePreferencesSettingsStore.cs`, `src/ReadTheStupidText.Infrastructure/Settings/**`, `.github/workflows/build.yml`, `.github/workflows/packs.yml`, `PRIVACY.md`, `docs/privacy.html`, `STORE.md`, `CLAUDE.md`
**Story issue:** —

## Goal

The Android app stops carrying the ~145 MB voice. On first launch the user picks
their language; the matching voice pack downloads (with progress, resume and a
clear offline message), and until it lands the phone's own voice reads. The
install gets much smaller.

## Steps

- [x] **Pack format and manifest.** Define in `docs/` (or `STORE.md`) the pack
      as a zip with a top-level folder, a `pack.json` (id, kind, language,
      version, engine, file list) and a LICENSE file. `packs.json`: array of
      `{id, language, kind (voice|ocr), version, sizeBytes, sha256, license, url}`.
      The Supertonic multilingual pack is the only one this slice ships; the
      layout is the same `SupertonicFiles` set (`Domain/Reading/SupertonicFiles.cs`).
- [x] `.github/workflows/packs.yml`: `workflow_dispatch` only. Checks out with
      LFS (model lives in `src/ReadTheStupidText.App/VoiceModel/`), zips it,
      computes sha256, publishes zip(s) and `packs.json` to a release tagged
      `packs-<n>` (input), marked not-latest. **Verify GitVersion ignores it:**
      `GitVersion.yml` sets no `tag-prefix` (default `[vV]?<semver>`), so
      `packs-1` should not parse; confirm with a local `gitversion` run after
      creating the tag, and check `build.yml`'s base-tag lookup only matches
      `refs/tags/v*` (it does on PR #165's branch). Also confirm `releases/latest`
      stays the app release (`--latest=false`).
- [x] `IVoicePackService` in `Application/Reading/` (replaces the single-model
      assumption in `IVoiceModelService`, lines 103-120): catalog fetch,
      `DownloadAsync(packId, IProgress<double>, CancellationToken)`, `IsInstalled`,
      resume via HTTP Range into a `.partial` file, retry with backoff, sha256
      verify before the atomic move, a typed `PackDownloadResult`
      (Ok / Offline / HashMismatch / Failed) so the UI composes the message.
      Keep `IVoiceModelService` as the readiness signal the readers use, now fed
      by the pack folder.
- [x] Android implementation in `Mobile/` (`HttpClient`, files under
      `FileSystem.AppDataDirectory/packs/<id>/`). Remove the `INTERNET`-less
      comment in `AndroidManifest.xml` (lines 3-5) and add
      `<uses-permission android:name="android.permission.INTERNET" />` and
      `ACCESS_NETWORK_STATE`.
- [x] **First-run language page** (9 languages: en, fa, fr, es, pt, zh, ar, ru, ja),
      shown from `App.xaml.cs` when no language is stored; persists through
      `ISettingsStore` (new `Language` property + key in
      `MobilePreferencesSettingsStore.cs`, ~line 17-23, and the Windows store).
      The page starts the pack download and shows progress; the user can already
      enter the Reader.
- [x] **System voice as a real first-run voice.** `Mobile/CompositeSpeechReader.cs`
      already falls back to the Android `TextToSpeech` reader while
      `IVoiceModelService.IsReady` is false (`SpeakAsync`, line 49). Promote it:
      label it in the voice picker ("Phone voice") instead of hiding it, and
      show a small "Downloading natural voices" status in the Reader.
- [x] **Remove the bundle.** In `ReadTheStupidText.Mobile.csproj` delete the
      `MauiAsset` VoiceModel item and its comment (lines 58-67). Rewrite
      `MobileVoiceModelService.cs` (the asset extraction, lines 22-36, goes away)
      to locate the installed pack folder and verify the required files.
- [x] `build.yml` android filter group: remove the line
      `src/ReadTheStupidText.App/VoiceModel/**` (PR #165's `changes` job, with
      its "The Mobile csproj bundles the Windows project's voice model" comment).
      The Android job's LFS checkout is no longer needed for the AAB; drop it
      if the tests/other steps don't need it.
- [x] Privacy and store text: `PRIVACY.md` (*Network use*, lines 43-52 and
      *Permissions*, 53-64), `docs/privacy.html` (same), and `STORE.md` Play data
      safety notes: the app downloads language packs (no user data sent, no
      telemetry); text, images and scans stay on the device.
- [x] Update the listing text from Slice 43 with the first-run download
      disclosure (about 145 MB, Wi-Fi advised). *Play listing text updated in docs/store-listings/*.md; Microsoft Store waits for Slice 56.*
- [x] `CLAUDE.md`: replace "model ships in the package" for Mobile, document the
      pack service and the `packs-*` releases. *Lifted by the index pass from the For CLAUDE.md block below.*

## Acceptance

- Unzip the built `.aab` / `.apk`: no `assets/VoiceModel/` and no `.onnx` voice
  files; the size is down by roughly the model size (record before/after).
- Fresh install, airplane mode: language page works, the Reader reads with the
  phone voice, and a clear offline message with Retry appears for the pack.
- Fresh install, online: pack downloads with progress; killing the app mid-way
  and reopening resumes; once ready, the next read uses the neural voice.
- A corrupted pack (wrong sha256) is rejected and re-downloaded.
- `v*` release versions are unchanged by creating a `packs-*` release.

## Risks / notes

- First network use in the app: the privacy text must ship in the same release.
- Play Console data-safety form needs a manual update by the user.
- Only the Supertonic (multilingual) pack exists here; per-language voices are Slice 51.
- The Windows `VoiceModel/` stays in the repo and MSIX until Slice 56.

## Changed while building

- Android `minSdk` raised 23 to 24: Play rejected the #165 release ("Play automatic protection requires a minimum SDK version of 24"). Later raised to 28 by Decision 82.
- Touches widened to `.github/scripts/find-release.sh`: `packs-*` tags would have made release-publish delete every future app draft and could misdirect asset carry-forward. Every app-release tag scan (build.yml release-publish, find-release.sh) now filters to `v<x.y.z>` tags only.
- `packs.yml` copies only the `SupertonicFiles` set plus LICENSE and fails on missing or LFS-pointer files; the release is `packs-<n>` with `--latest=false`.
- Pack model in `Application/Reading`: `PackKind`, `PackDownloadResult` (Ok / Offline / HashMismatch / InvalidPack / Failed), `PackInfo`/`PackCatalogParser` (comma-separated `language` list; invalid ids or versions skipped via `PackNaming`), `PackManifest`, `PackRetryPolicy` (resume offset, 2/4/8 s backoff capped at 30 s, Offline and InvalidPack not retried, one clean re-download on HashMismatch), `PackErrorClassifier` (Java exception type names, `WebException`, `NetworkAccess.None` map to Offline), `VoicePackSelector`, `AppLanguage` (9 languages), `SystemVoice`, `IVoicePackService`. `ISettingsStore.Language` is stored in Mobile Preferences and the Windows `LocalSettingsStore` (unused on Windows until Slice 56).
- Mobile: `MobileVoicePackService` runs off the UI thread with a Range-resumed `<id>-<version>.zip.partial`, a stall guard, sha256 then extraction to an `.extracting` scratch folder, manifest validation, an atomic move, reuse of a verified zip after a kill, and exact orphan cleanup. `VoicePackInstaller` is a singleton with Resolving / Downloading / Ready / Offline / Failed / InvalidPack states marshalled to the main thread; an installed pack activates with no network request at all, whatever the stored language. `LanguagePage` is the first-run page; "Phone voice" appears in the picker and the Reader status line; `CompositeSpeechReader` routes the switch to the next read and keeps the last neural voice. The `MauiAsset` VoiceModel is removed; `INTERNET` and `ACCESS_NETWORK_STATE` added.
- Docs: `docs/voice-packs.md`, `PRIVACY.md`/`docs/privacy.html` (network only for pack downloads, none once installed), `STORE.md` Play data-safety note. `build.yml` android filter drops `App/VoiceModel` and the Android job's LFS checkout.
- Measured (debug build): APK 164.8 MB to 38.4 MB; no `assets/VoiceModel`; the real arm64 sherpa/onnxruntime `.so` files are still present; `minSdkVersion` 24 confirmed with aapt2.

## From review

- High pass: 10 findings, all fixed (UI-thread pipeline, verified-zip loss, catalog fetch before the installed check, AndroidMessageHandler offline detection, page leak, blank Resolving state, progress flood, catalog-null to Offline, duplicated activation, one-line methods).
- Medium pass: 10 findings, 9 fixed. The pack version-update check was skipped and deferred to Slice 51, because an installed pack must make no network request.

#### For CLAUDE.md (lifted 2026-10-20)

- Mobile no longer bundles the voice model. Supertonic-3 is a downloaded pack `supertonic-3` (~145 MB) under `AppDataDirectory/packs/`; `MobileVoiceModelService` only locates and manifest-checks it. The APK/AAB has no `assets/VoiceModel` (unzip and check). The Windows `VoiceModel/` stays until Slice 56. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- Voice packs (`docs/voice-packs.md`): `IVoicePackService` (Application) + `MobileVoicePackService` (Mobile); the pure decisions live in `PackRetryPolicy`/`PackCatalogParser`/`PackManifest`/`PackNaming`/`VoicePackSelector` (unit-tested). The pipeline runs off the UI thread and `VoicePackInstaller` marshals state to the main thread. An installed pack means zero network requests. `InvalidPack` is never retried. Offline detection on Android = `NetworkAccess.None` + Java exception type names. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- Until the pack is Ready, `CompositeSpeechReader` reads with the Android `TextToSpeech` "Phone voice" (`SystemVoice.Id`), also user-selectable. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- `packs-<n>` releases hold packs + `packs.json`, published by the manual `packs.yml` with `--latest=false`; the app pins one in `PackConstants.CatalogUrl`. Every app-release tag scan (`build.yml` release-publish, `.github/scripts/find-release.sh`) must filter to `^v[0-9]+\.[0-9]+\.[0-9]+$`. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)
- First-run language: `ISettingsStore.Language`; the app shows `LanguagePage` when it is null. Android now declares `INTERNET` + `ACCESS_NETWORK_STATE`, used only for pack downloads; user data stays on the device. Android `minSdk` is 28 (Decision 82; Google Play rejects below 24). (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)

#### For README.md (lifted 2026-10-20)

- Android first launch asks for a language and downloads the ~145 MB voice pack (Wi-Fi advised); the phone voice reads until it lands; works offline after that.

#### Not yet verified

- Run `packs.yml` with release_number=1 pack_version=1; confirm `releases/latest` and `v*` versions are unchanged.
- Fresh install in airplane mode: language page, phone voice reads, offline message + Retry.
- Online download with progress; kill mid-way and resume.
- Corrupted pack rejected and re-downloaded once.
- Next read after install uses the neural voice.
- Play Console data-safety form updated by hand.
- Play accepts the minSdk 28 bundle (Decision 82).
