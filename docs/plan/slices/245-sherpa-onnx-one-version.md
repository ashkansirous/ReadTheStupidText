# Slice 245 — sherpa-onnx: one version on every platform (1.13.8)

*Built 2026-11-01, not merged; Android emulator Dino kokoro-en read and a host Python piper-fa read checked, the rest not yet verified (see below).* From the user: "the rule is that android and ios should work exactly the same, which means the
same fucking package versions as well", then "for the current platform, lets set it all to 1.13.8 for now".

**Depends on:** none.
**Touches:** `src/SherpaOnnxVersion.props` (new), `src/AndroidSherpaPackaging.props`,
`src/ReadTheStupidText.Mobile.Shared/SherpaIosVersion.props`, `src/ReadTheStupidText.Mobile.Shared/IosSherpaPackaging.props`,
`src/ReadTheStupidText.Mobile.Shared/ReadTheStupidText.Mobile.Shared.csproj`,
`src/ReadTheStupidText.Infrastructure/ReadTheStupidText.Infrastructure.csproj`, `src/ReadTheStupidText.App/ReadTheStupidText.App.csproj`,
`src/DinoReader.App/DinoReader.App.csproj`, `src/ReadTheStupidText.Domain/Reading/PiperTokens.cs`,
`.github/workflows/packs.yml`, `.github/scripts/**`, `tools/sound-words-test/requirements.txt`,
`src/ReadTheStupidText.Mobile/CLAUDE.md`, `.github/CLAUDE.md`, `CLAUDE.md`, `STORE.md`, `tests/ReadTheStupidText.Tests/**`

## Why (checked 2026-11-01)

- Android and Windows pin sherpa-onnx **1.13.3** (`src/AndroidSherpaPackaging.props:5`, `:145`;
  `ReadTheStupidText.Infrastructure.csproj:19`; `packs.yml:611` pip; `tools/sound-words-test/requirements.txt:2`).
- iOS pins **1.13.8** (`SherpaIosVersion.props:9`), because upstream has no 1.13.3 iOS xcframework (Slice 49:
  `docs/plan/slices/49-ios-parity.md:81`). Nobody moved Android to match.
- 1.13.8 exists for everything we fetch: NuGet `org.k2fsa.sherpa.onnx` and `runtime.android-arm64`/`win-x64` 1.13.8
  (HTTP 200), `sherpa-onnx-v1.13.8-android.tar.bz2` (HTTP 206 range read), PyPI `sherpa-onnx` 1.13.8.

## Decisions (the user, 2026-11-01)

- All platforms on **1.13.8** now.
- **2.0.x is deferred.** 2.0.0 removed espeak-ng and piper-phonemize from sherpa-onnx (PR #4055; `DataDir` ignored), so
  `piper-fa`, `piper-nb` and `kokoro-en` would stop reading. The ready-made phonemizer is GPL-3.0 (espeak-ng); the user
  asked whether changing the licence would allow it: only moving both apps to GPL-3 would, with full source to users
  and a known App Store conflict, so not now. Revisit with Slice 246 (pre-made audio for store books) and a non-GPL
  phonemizer.

## Work

- [x] 1. `library-docs`: the onnxruntime version bundled by sherpa-onnx 1.13.8 on Android (arm64, x86_64), Windows and
  iOS, and the 1.13.3 to 1.13.8 changes in the C# API and the Python TTS API that we call (configs, Generate,
  tokens.txt rules that `PiperTokens.cs` mirrors). Write the answers here.
- [x] 2. **One version property.** New `src/SherpaOnnxVersion.props` holds `SherpaOnnxVersion` (1.13.8) and the matching
  onnxruntime version; Android, iOS (replacing `SherpaIosVersion.props`) and Infrastructure import it. A build error if
  any project sets its own.
- [x] 3. Android: 1.13.8 NuGet packages, the x86_64 tarball pin and its SHA-256 (from the GitHub release digest).
- [x] 4. Windows (Infrastructure, RTST App, Dino Windows head): 1.13.8; the APPX onnxruntime dedupe still picks sherpa's.
- [x] 5. `Microsoft.ML.OnnxRuntime.Managed` (Mobile.Shared, Infrastructure) and iOS `Microsoft.ML.OnnxRuntime` move to the
  version item 1 found, per the "bump together" rule (Slice 52).
- [x] 6. `packs.yml` pip pin and `tools/sound-words-test/requirements.txt` to 1.13.8 (pack building and tests read
  with the same engine as the apps).
- [x] 7. **A check that keeps them equal:** a CI script (in `tests.yml` or `.github/scripts`) fails when any sherpa-onnx
  or onnxruntime pin in the repo differs from `SherpaOnnxVersion.props`. Rule line in the root `CLAUDE.md`.
- [x] 8. Update the version mentions in `src/ReadTheStupidText.Mobile/CLAUDE.md`, `STORE.md`, `PiperTokens.cs` comments.

## Item 1 findings (checked 2026-11-01, sherpa-onnx tag v1.13.8)

- **Bundled onnxruntime is 1.28.2 everywhere** (it was 1.24.3 Android, 1.24.4 Windows with 1.13.3). cmake files at the tag:
  `onnxruntime-win-x64.cmake` (1.28.2), `onnxruntime-android-aarch64/-x86_64.cmake` (`onnxruntime-android-1.28.2.zip`),
  `build-ios.sh` (`SHERPA_ONNX_ONNXRUNTIME_VERSION` default 1.28.2). Confirmed in the real binaries: the strings in
  `org.k2fsa.sherpa.onnx.runtime.win-x64` 1.13.8 `onnxruntime.dll`, `runtime.android-arm64` `libonnxruntime.so`, the built RTST
  `onnxruntime.dll` and the extracted x86_64 `libonnxruntime.so` all say 1.28.2 and no other 1.2x/1.3x version.
- **1.28.2 is not on NuGet.** `Microsoft.ML.OnnxRuntime(.Managed)` has 1.28.0-rc.1, 1.28.0, 1.29.0 and later (no 1.28.1 or 1.28.2),
  so the managed binding is **1.28.0** (`OnnxRuntimeVersion`). Same 1.28 API level as the native library, so the "bump
  together" rule holds at minor level; a patch difference on the managed side was not exercised on a device yet.
- **SHA-256 (GitHub release asset digests, API):** Android tarball `sherpa-onnx-v1.13.8-android.tar.bz2`
  `2ff63469a71cb6009aa2e3ed5f4a670f8abdcbe4bb9ffd23776afc792a6b4f44` (the build fetched it and the pin matched);
  iOS `sherpa-onnx-v1.13.8-ios-shared-onnxruntime-static.xcframework.zip`
  `e259a7d3b38ad7dec49bb078252a30bb42ede8355e2bb130cf8c1c78ed131f75` (unchanged, already 1.13.8).
- **C# API 1.13.3 to 1.13.8:** `scripts/dotnet/*.cs` differ only in FastClustering, OfflineSpeakerDiarization (+Segment,
  PyannoteModelConfig), OfflineSpeechDenoiserDpdfNetModelConfig and VersionInfo. Nothing we call (OfflineTts, OfflineTtsConfig,
  OfflineTtsGenerationConfig, OfflineTtsGeneratedAudio, the model configs) changed; all builds compile unchanged.
- **Piper/tokens.txt:** `symbol-table.cc` only swaps `std::ifstream` for `OpenInputFile`; `piper-phonemize-lexicon.cc`
  `ReadTokens` rules are unchanged, so `PiperTokens.cs` still mirrors them. Behaviour changes in the same file that a device read
  must hear: Piper phoneme ids now get a pad token after bos (upstream issue 3721, `is_inflect` flag), and an espeak-ng
  exception is caught and returns no phonemes instead of aborting. Python TTS API: not diffed; the same 1.13.8 wheel
  is pinned in `packs.yml` (Kokoro sample) and the sound-words test.
- Not changed on purpose: `packs.yml` quantiser pip pins `onnxruntime==1.23.2` (a Python quantiser, no wheel for newer; not an app pin).

## Built (2026-11-01)

`src/SherpaOnnxVersion.props` holds both versions; Android, iOS (the old `SherpaIosVersion.props` is gone), Mobile.Shared and
Infrastructure read it. The x86_64 tarball pin is 1.13.8; `packs.yml` and `tools/sound-words-test` are on 1.13.8.
`.github/scripts/check-sherpa-pins.mjs` (+ `.test.mjs`) runs in CI (`release-scripts.yml`) and in the C# suite
(`SherpaVersionPinTests`); it fails on any other pin. `SherpaVersionGuard` (Android/Mobile.Shared) and `CheckSherpaIosPin` (iOS)
are build errors when a project sets its own version or the pinned version differs. `ship-targets.mjs` `CORE_PATHS` names
`src/SherpaOnnxVersion.props`, so a bump ships all six apps.

Changed while building: the native onnxruntime in sherpa-onnx 1.13.8 is 1.28.2 on every platform, NuGet has no 1.28.2 managed
package, so managed is 1.28.0 (same C API level 28; reasoning only, not run).

From review (read: `D:\ReadTheStupidText-worktrees\reviews\s245-high.md` 9/9, `s245-medium.md` 8/8):
- High 1 (version props in no ship group or path filter), 2 (pip rule missed a line-start pin), 3 (single-line pin rules), 4
  (pin check not a CI step), 6 (iOS hash had no version guard), 7 (scanned extensions narrower than the rule), 8 (preset
  `SherpaOnnxVersion` silently accepted): applied. 9: applied for the timeout (the test no longer hangs); a missing `node` is
  not covered by this note.
- High 5 / medium 1 (managed 1.28.0 against native 1.28.2 never run): not a code fix; it is a device check, listed under *Not yet verified*.
- Medium 3 (duplicate version literals in the guard), 6 (iOS pin Error only on a cache miss), 7 (stale 1.13.3 clause in
  `PiperTokens.cs`): applied.
- Medium 4 (pin check does not cover `onnxruntime==` pip pins): skipped on purpose; the `packs.yml` quantiser `onnxruntime`
  pins are a Python tool, separate from the app pins.
- Medium 5 (unit test spawns node and git) and 8 (`release-scripts.yml` runs on every project-file change): low, not applied.
- Medium 2 (sound-words baselines on 1.13.8): the brief does not say it was applied; only the host piper-fa read below was run.

## Device results (2026-11-01)

- Android emulator: Dino `kokoro-en` read PASS (CHUNK_AUDIO lines).
- Host Python sherpa-onnx 1.13.8, `piper-fa` mana, the sukun sentence from three-little-pigs: non-silent 6.15 s wav PASS.

## Verification

- Fast checks: both test suites, the pin-equality check.
- Android emulator (x86_64 Debug) and iPhone/simulator: one sentence each with `supertonic-3`, `piper-fa` (mana),
  `piper-nb`, `kokoro-en` and `melo` reads aloud. Windows VM: one Supertonic and one Piper read.
- The same Farsi sentence with sukun reads the same on Android and iOS (now the same engine).

#### Not yet verified

- Dino `supertonic-3`, `piper-fa`, `piper-nb` and `melo` reads on a device; RTST Android Supertonic read.
- Scan/OCR (PaddleOCR) and translation with managed onnxruntime 1.28.0 against native 1.28.2 on Android, iOS and Windows.
- Windows VM reads: the VM did not start (host had 6.9 GB free, gate is 8 GB, because the emulator ran at the same time).
- iOS everything (needs the TestFlight build).
- The same Farsi sukun sentence sounding the same on Android and iOS.

#### Tooling gaps found (for a tools slice)

- No scripted way to put `piper-fa`, `piper-nb` or `melo` packs on the emulator.
- The open-book hook rejects voice id `supertonic:F1` after the pack installs.
- The desktop VM has no audio device, so its read check proves only that a read started; no VM check reads with a Piper pack or exercises onnxruntime managed.
- Never start the VM and the emulator together (memory).

#### For CLAUDE.md

- sherpa-onnx and onnxruntime versions are set only in `src/SherpaOnnxVersion.props`; never pin either elsewhere in build, workflow or requirements files. `.github/scripts/check-sherpa-pins.mjs` (CI and the C# suite) fails on a stray pin. A bump ships all six apps through `ship-targets.mjs` `CORE_PATHS`. (Slice 245: docs/plan/slices/245-sherpa-onnx-one-version.md)
- The managed onnxruntime may lag the native one when NuGet has no matching patch (1.28.0 managed, 1.28.2 native); keep the minor equal. (Slice 245: docs/plan/slices/245-sherpa-onnx-one-version.md)
