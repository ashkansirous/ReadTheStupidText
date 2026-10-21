# Slice 52 — PaddleOCR per language (Android)

*In progress (wave 2026-10-21b), draft PR.*

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

- [ ] **Research:** current PaddleOCR PP-OCRv5 ONNX det + rec models and
      per-language recognition dictionaries (Latin, Arabic/Farsi, Cyrillic, Chinese,
      Japanese); sizes; Apache-2.0 notices; whether an exported ONNX set is
      published or must be converted once in CI. Record in this file.
- [ ] **Engine.** Run det + rec on onnxruntime. The Android app already carries
      onnxruntime inside sherpa-onnx's `libonnxruntime.so`
      (`ReadTheStupidText.Mobile.csproj` lines 114-117), but sherpa's C API does
      not expose it for arbitrary models. Decide at implementation between
      adding `Microsoft.ML.OnnxRuntime` (version confirmed via context7; check the
      two native `libonnxruntime.so` do not collide in the APK, unzip and
      verify) or P/Invoking the bundled one. Record the choice.
- [ ] **Interface.** The existing contract is `IImageTextExtractor.ExtractTextAsync(string imagePath)`
      (`Application/Images/IImageTextExtractor.cs`). Add the language (new
      overload or a request record) so the extractor knows which pack to use.
      `MlKitImageTextExtractor` (`Platforms/Android/MlKitImageTextExtractor.cs`)
      keeps implementing it.
- [ ] **`PaddleOcrImageTextExtractor`** in `Mobile/` (or a new portable
      `ReadTheStupidText.Ocr` project, shared with iOS/Windows in Slices 55/56;
      prefer the new project, `net10.0`): detection -> box ordering -> crop ->
      recognition -> CTC decode. Keep image decode/resize platform-specific
      behind a tiny interface (`SkiaSharp` or platform bitmap; confirm at
      implementation).
- [ ] **Pure logic** (unit tested): CTC greedy decode with the language
      dictionary, detection post-processing (threshold, polygon -> box, unclip),
      box ordering into reading order; **RTL** (fa, ar): order lines top to
      bottom and boxes within a line right-to-left, and keep the recognizer's
      output as logical order (check bidi handling). Tests in
      `tests/ReadTheStupidText.Tests` (the project needs a reference to the new
      project if created).
- [ ] **Packs.** Extend `packs.yml` with `kind: ocr` packs per language
      (det model shared, rec model + dict per script). Manifest entries in
      `packs.json`; downloaded by the first-run flow alongside the voice pack.
- [ ] **Wiring.** Register a composite in `MauiProgram.cs` (replaces
      `CreateImageTextExtractor`, lines 55-62): language-matching OCR pack
      installed -> PaddleOCR; otherwise ML Kit Latin as the fallback with a note
      that the OCR pack is still downloading when the language is non-Latin.
- [ ] Update `CLAUDE.md` (OCR rule, Decision 40's ML Kit note) and `STORE.md`
      third-party notices (PaddleOCR, Apache-2.0).

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
