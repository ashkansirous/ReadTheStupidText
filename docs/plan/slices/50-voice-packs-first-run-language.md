# Slice 50 — Voice packs, first-run language and a slimmer app (Android)

*In progress (wave 2026-10-20), draft PR.*

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

- [ ] **Pack format and manifest.** Define in `docs/` (or `STORE.md`) the pack
      as a zip with a top-level folder, a `pack.json` (id, kind, language,
      version, engine, file list) and a LICENSE file. `packs.json`: array of
      `{id, language, kind (voice|ocr), version, sizeBytes, sha256, license, url}`.
      The Supertonic multilingual pack is the only one this slice ships; the
      layout is the same `SupertonicFiles` set (`Domain/Reading/SupertonicFiles.cs`).
- [ ] `.github/workflows/packs.yml`: `workflow_dispatch` only. Checks out with
      LFS (model lives in `src/ReadTheStupidText.App/VoiceModel/`), zips it,
      computes sha256, publishes zip(s) and `packs.json` to a release tagged
      `packs-<n>` (input), marked not-latest. **Verify GitVersion ignores it:**
      `GitVersion.yml` sets no `tag-prefix` (default `[vV]?<semver>`), so
      `packs-1` should not parse; confirm with a local `gitversion` run after
      creating the tag, and check `build.yml`'s base-tag lookup only matches
      `refs/tags/v*` (it does on PR #165's branch). Also confirm `releases/latest`
      stays the app release (`--latest=false`).
- [ ] `IVoicePackService` in `Application/Reading/` (replaces the single-model
      assumption in `IVoiceModelService`, lines 103-120): catalog fetch,
      `DownloadAsync(packId, IProgress<double>, CancellationToken)`, `IsInstalled`,
      resume via HTTP Range into a `.partial` file, retry with backoff, sha256
      verify before the atomic move, a typed `PackDownloadResult`
      (Ok / Offline / HashMismatch / Failed) so the UI composes the message.
      Keep `IVoiceModelService` as the readiness signal the readers use, now fed
      by the pack folder.
- [ ] Android implementation in `Mobile/` (`HttpClient`, files under
      `FileSystem.AppDataDirectory/packs/<id>/`). Remove the `INTERNET`-less
      comment in `AndroidManifest.xml` (lines 3-5) and add
      `<uses-permission android:name="android.permission.INTERNET" />` and
      `ACCESS_NETWORK_STATE`.
- [ ] **First-run language page** (9 languages: en, fa, fr, es, pt, zh, ar, ru, ja),
      shown from `App.xaml.cs` when no language is stored; persists through
      `ISettingsStore` (new `Language` property + key in
      `MobilePreferencesSettingsStore.cs`, ~line 17-23, and the Windows store).
      The page starts the pack download and shows progress; the user can already
      enter the Reader.
- [ ] **System voice as a real first-run voice.** `Mobile/CompositeSpeechReader.cs`
      already falls back to the Android `TextToSpeech` reader while
      `IVoiceModelService.IsReady` is false (`SpeakAsync`, line 49). Promote it:
      label it in the voice picker ("Phone voice") instead of hiding it, and
      show a small "Downloading natural voices" status in the Reader.
- [ ] **Remove the bundle.** In `ReadTheStupidText.Mobile.csproj` delete the
      `MauiAsset` VoiceModel item and its comment (lines 58-67). Rewrite
      `MobileVoiceModelService.cs` (the asset extraction, lines 22-36, goes away)
      to locate the installed pack folder and verify the required files.
- [ ] `build.yml` android filter group: remove the line
      `src/ReadTheStupidText.App/VoiceModel/**` (PR #165's `changes` job, with
      its "The Mobile csproj bundles the Windows project's voice model" comment).
      The Android job's LFS checkout is no longer needed for the AAB; drop it
      if the tests/other steps don't need it.
- [ ] Privacy and store text: `PRIVACY.md` (*Network use*, lines 43-52 and
      *Permissions*, 53-64), `docs/privacy.html` (same), and `STORE.md` Play data
      safety notes: the app downloads language packs (no user data sent, no
      telemetry); text, images and scans stay on the device.
- [ ] Update the listing text from Slice 43 with the first-run download
      disclosure (about 145 MB, Wi-Fi advised).
- [ ] `CLAUDE.md`: replace "model ships in the package" for Mobile, document the
      pack service and the `packs-*` releases.

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
