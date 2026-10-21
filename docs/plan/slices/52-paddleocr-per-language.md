# Slice 52 — PaddleOCR per language (Android)

*Built (wave 2026-10-21b), PR #203.*

**Batch:** 9
**Decisions:** 65 (also 60-62)
**Depends on:** Slice 50 (pack service); Slice 45 for the scanner that feeds it
**Touches:** `src/ReadTheStupidText.Application/Images/**`, `src/ReadTheStupidText.Mobile/**` (csproj, `MauiProgram.cs`, `Platforms/Android/**`), `src/ReadTheStupidText.Application/Reading/**`, `tests/ReadTheStupidText.Tests/**`, `.github/workflows/packs.yml`, `STORE.md`, `CLAUDE.md`
**Story issue:** —

## Goal

Scanned pages in Arabic, Farsi, Russian, Chinese, Japanese and the Latin
languages come out as correct text, not blanks. ML Kit's on-device recognizer has
no Arabic, Farsi or Cyrillic, so an OCR pack per language replaces it once
downloaded.

## Steps

- [x] **Research:** current PaddleOCR PP-OCRv5 ONNX det + rec models and
      per-language recognition dictionaries (Latin, Arabic/Farsi, Cyrillic, Chinese,
      Japanese); sizes; Apache-2.0 notices; whether an exported ONNX set is
      published or must be converted once in CI. Record in this file.
- [x] **Engine.** Run det + rec on onnxruntime. The Android app already carries
      onnxruntime inside sherpa-onnx's `libonnxruntime.so`
      (`ReadTheStupidText.Mobile.csproj` lines 114-117), but sherpa's C API does
      not expose it for arbitrary models. Decide at implementation between
      adding `Microsoft.ML.OnnxRuntime` (version confirmed via context7; check the
      two native `libonnxruntime.so` do not collide in the APK, unzip and
      verify) or P/Invoking the bundled one. Record the choice.
- [x] **Interface.** The existing contract is `IImageTextExtractor.ExtractTextAsync(string imagePath)`
      (`Application/Images/IImageTextExtractor.cs`). Add the language (new
      overload or a request record) so the extractor knows which pack to use.
      `MlKitImageTextExtractor` (`Platforms/Android/MlKitImageTextExtractor.cs`)
      keeps implementing it.
- [x] **`PaddleOcrImageTextExtractor`** in `Mobile/` (or a new portable
      `ReadTheStupidText.Ocr` project, shared with iOS/Windows in Slices 55/56;
      prefer the new project, `net10.0`): detection -> box ordering -> crop ->
      recognition -> CTC decode. Keep image decode/resize platform-specific
      behind a tiny interface (`SkiaSharp` or platform bitmap; confirm at
      implementation).
- [x] **Pure logic** (unit tested): CTC greedy decode with the language
      dictionary, detection post-processing (threshold, polygon -> box, unclip),
      box ordering into reading order; **RTL** (fa, ar): order lines top to
      bottom and boxes within a line right-to-left, and keep the recognizer's
      output as logical order (check bidi handling). Tests in
      `tests/ReadTheStupidText.Tests` (the project needs a reference to the new
      project if created).
- [~] **Packs (built, not live).** Extend `packs.yml` with `kind: ocr` packs per language
      (det model shared, rec model + dict per script). Manifest entries in
      `packs.json`; downloaded by the first-run flow alongside the voice pack.
      **Not live until the follow-up:** `PackConstants.CatalogUrl` still points at the
      current packs release, which has no OCR packs. After the merge: run the packs
      workflow with a new release number and `ocr_pack_version=1`, then open a
      one-line PR flipping `PackConstants.CatalogUrl` to that release.
- [x] **Wiring.** Register a composite in `MauiProgram.cs` (replaces
      `CreateImageTextExtractor`, lines 55-62): language-matching OCR pack
      installed -> PaddleOCR; otherwise ML Kit Latin as the fallback with a note
      that the OCR pack is still downloading when the language is non-Latin.
- [x] `STORE.md` third-party notices (PaddleOCR, Apache-2.0) added as licence rows. The
      `CLAUDE.md` OCR rule (Decision 40's ML Kit note) is lifted by the index pass from the
      block below.

## Acceptance

- Scan a printed page in each of Arabic, Farsi, Russian, Japanese, Chinese and
  French: recognized text is correct and in reading order (RTL pages read right
  to left); record a short accuracy note per language.
- Before the OCR pack is installed, a Latin page still works via ML Kit.
- Unit tests for decode/post-processing pass in CI.

## Risks / notes

- Two onnxruntime native libraries in one APK is the main packaging risk; verify by unzipping the `.apk`.
- OCR speed on mid-range phones: measure per page; show progress (Slice 45 does).
- Handwriting is out of scope beyond what PaddleOCR gives (plan Out of Scope).
- Batch 10 (Slice 63) needs the line boxes kept - the engine returns lines with boxes and the text-only call is derived from them, don't discard the geometry.

## Decisions taken during build

- **Engine:** `Microsoft.ML.OnnxRuntime.Managed` 1.24.3 only (no native package). It P/Invokes `libonnxruntime.so`, which is the sherpa-onnx one (also 1.24.3), so the APK has a single onnxruntime.
- **Models:** `monkt/paddleocr-onnx` (Apache-2.0) at a pinned revision, sha256-checked in `packs.yml`. Shared PP-OCRv3 mobile detector (2.4 MB) in every pack; recognizers: PP-OCRv5 Latin and Cyrillic (eslav), PP-OCRv3 Arabic (ar, fa), PP-OCRv5 Chinese server (zh, ja; 84 MB).
- **Packs:** `ocr-latin` (en, fr, es, pt), `ocr-cyrillic` (ru), `ocr-arabic` (ar, fa), `ocr-cjk` (zh, ja), each standalone, `kind: ocr`, one `ocr_pack_version` input. Downloaded silently once the voice pack settles (`OcrPackInstaller`, hooked in `App.xaml.cs`).
- **No new project:** the pure logic is in `Application/Images/Ocr/` and the onnxruntime/BitmapFactory code in `Mobile/Platforms/Android`, because a `ReadTheStupidText.Ocr` project needs slnx and CI path-filter edits outside Touches. Slices 55/56 can move it.
- **Geometry kept:** `OcrLine(Text, Box)`, `ImageText`, `IOcrLineExtractor`; the text-only `IImageTextExtractor` is derived. ML Kit lines carry boxes too.
- **No ReaderPage edit:** `LanguageImageTextExtractor` reads the language from settings. The "pack still downloading" note is exposed as `OcrSource.MlKitPackPending`, but no UI string was added: this branch has no `AppResources` (Slice 53 is not in it).
- **Axis-aligned boxes** only; empty or failed PaddleOCR reads fall back to ML Kit.
- **Superseded:** "Downloaded silently once the voice pack settles" is now: `OcrPackInstaller` runs at every start and on a language change, waits for the voice installer, runs off the UI thread with a cached catalogue, and upgrades packs (see below).

## Changed while building

Final shape, as built:

- **`Application/Images/Ocr` (pure):** geometry-keeping `OcrLine`/`OcrBox`/`ImageText` (for Slice 63); `IOcrPage` (detection image plus `CropLine`); `ChwNormalizer`, the detection and recognition preprocessors, `DbPostProcessor`; `CtcDecoder` with a cached table; `LineOrdering`, column-aware (two-column pages read column by column, RTL mirrored); `LineCrops` (wide lines split, tall crops rotated 90 degrees); `RightToLeftText` (PaddleOCR `pred_reverse` plus Arabic-Indic and Persian digits); `OcrPipeline`, `CropSampling`, `InstalledOcrCatalog`.
- **Shared `PackSelection`:** kind-parameterised, with an id tie-break.
- **`PackVersions` upgrades:** a newer catalogue version is downloaded, and the old pack is kept until the new one verifies. See `docs/voice-packs.md`.
- **Android:** `Microsoft.ML.OnnxRuntime.Managed` 1.24.3, bound to sherpa-onnx's bundled `libonnxruntime.so` 1.24.3 so only one native copy ships; `OnnxOcrInference` (sessions disposed on failure); `PaddleOcrEngine`; `AndroidPixelImageLoader`/`AndroidOcrPage` (detection on a decode of at most 1280 px, line crops from the full-resolution source via `BitmapRegionDecoder`, mapped through all 8 EXIF orientations); `LanguageImageTextExtractor`, which falls back to ML Kit and remembers broken packs.
- **`OcrPackInstaller`:** runs at every start and on a language change, waits for the voice installer, runs off the UI thread with a cached catalogue.
- **`packs.yml`:** builds four OCR packs (ocr-latin, ocr-cyrillic, ocr-arabic, ocr-cjk) from `monkt/paddleocr-onnx` (Apache-2.0), sha256-pinned.
- **`STORE.md`:** licence rows.

## From review

- High pass: 10 findings, 9 fixed. Skipped: one-line methods, as the repo convention. The "packs not live" finding is a follow-up (below).
- Medium pass: 10 findings, all fixed.

## Verified

- 378 tests pass, plus 80 Dino tests. Android builds pass.
- APK check: exactly one `libonnxruntime.so` in `lib/arm64-v8a`, and no models in the APK.
- Emulator smoke test (RTST_Test, 2026-10-21): the app starts with the OCR installer and stays running, no crash, neural voice ready. A real OCR scan cannot be tested until the OCR packs are published.

#### For CLAUDE.md

- **OCR on Android:** PaddleOCR models download as `kind: ocr` packs per language (ocr-latin, ocr-cyrillic, ocr-arabic, ocr-cjk); ML Kit Latin is the fallback. The pure pipeline lives in `Application/Images/Ocr` and keeps line boxes for Slice 63. (Slice 52: docs/plan/slices/52-paddleocr-per-language.md)
- **onnxruntime version lock:** `Microsoft.ML.OnnxRuntime.Managed` must stay version-matched with the `libonnxruntime.so` bundled by sherpa-onnx (both 1.24.3). Bump them together, and never add a native onnxruntime package. (Slice 52: docs/plan/slices/52-paddleocr-per-language.md)

#### For README.md

- On Android, scanned text in Arabic, Farsi, Russian, Chinese and Japanese is recognised on the phone with a downloaded recognition model, and Latin scripts improve too. Nothing leaves the device.

#### Not yet verified

- Run the packs workflow with a new release number, `ocr_pack_version=1`, and the voice pack versions unchanged. Then a one-line PR flips `PackConstants.CatalogUrl`. Until then the OCR packs are not live.
- Real scans in ar, fa, ru, zh, ja and fr: accuracy, reading order, RTL including numbers, and time per page on a mid-range phone.
- Memory with the 84 MB CJK pack.
- Follow-up: the voice installer does not check for newer voice-pack versions at start-up. Only the OCR installer does.
