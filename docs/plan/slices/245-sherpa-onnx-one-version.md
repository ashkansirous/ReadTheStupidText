# Slice 245 — sherpa-onnx: one version on every platform (1.13.8)

*Planned 2026-11-01.* From the user: "the rule is that android and ios should work exactly the same, which means the
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

- [ ] 1. `library-docs`: the onnxruntime version bundled by sherpa-onnx 1.13.8 on Android (arm64, x86_64), Windows and
  iOS, and the 1.13.3 to 1.13.8 changes in the C# API and the Python TTS API that we call (configs, Generate,
  tokens.txt rules that `PiperTokens.cs` mirrors). Write the answers here.
- [ ] 2. **One version property.** New `src/SherpaOnnxVersion.props` holds `SherpaOnnxVersion` (1.13.8) and the matching
  onnxruntime version; Android, iOS (replacing `SherpaIosVersion.props`) and Infrastructure import it. A build error if
  any project sets its own.
- [ ] 3. Android: 1.13.8 NuGet packages, the x86_64 tarball pin and its SHA-256 (from the GitHub release digest).
- [ ] 4. Windows (Infrastructure, RTST App, Dino Windows head): 1.13.8; the APPX onnxruntime dedupe still picks sherpa's.
- [ ] 5. `Microsoft.ML.OnnxRuntime.Managed` (Mobile.Shared, Infrastructure) and iOS `Microsoft.ML.OnnxRuntime` move to the
  version item 1 found, per the "bump together" rule (Slice 52).
- [ ] 6. `packs.yml` pip pin and `tools/sound-words-test/requirements.txt` to 1.13.8 (pack building and tests read
  with the same engine as the apps).
- [ ] 7. **A check that keeps them equal:** a CI script (in `tests.yml` or `.github/scripts`) fails when any sherpa-onnx
  or onnxruntime pin in the repo differs from `SherpaOnnxVersion.props`. Rule line in the root `CLAUDE.md`.
- [ ] 8. Update the version mentions in `src/ReadTheStupidText.Mobile/CLAUDE.md`, `STORE.md`, `PiperTokens.cs` comments.

## Verification

- Fast checks: both test suites, the pin-equality check.
- Android emulator (x86_64 Debug) and iPhone/simulator: one sentence each with `supertonic-3`, `piper-fa` (mana),
  `piper-nb`, `kokoro-en` and `melo` reads aloud. Windows VM: one Supertonic and one Piper read.
- The same Farsi sentence with sukun reads the same on Android and iOS (now the same engine).
