# CLAUDE.md — Mobile.Shared

Shared platform code for every MAUI app here (RTST Mobile and Dino Reader). The namespace is
still `ReadTheStupidText.Mobile`, so the namespace, bare-`Application` and sherpa-onnx gotchas in
`src/ReadTheStupidText.Mobile/CLAUDE.md` apply to the `Platforms/` folders here too.

- Scanner/OCR stack (ML Kit, PaddleOCR, document scanner, PDF renderer, page-image keeper) lives in `Mobile.Shared/Platforms/Android`, registered once in `MobileSharedServices.Android.cs`; apps must not register their own and must forward `OnActivityResult` to `AndroidDocumentScanner.HandleActivityResult`. Positioned text uses `NormalizedRect` (0..1, top-left); `RangeToBoxMapper` maps story ranges to lines. Translation stays in the RTST app. (Slice 63: docs/plan/slices/63-positioned-text-extraction.md)
- OCR shared by Android and iOS is `Mobile.Shared/Ocr/` behind `IBuiltInTextRecognizer`. iOS ships its own `Microsoft.ML.OnnxRuntime` (sherpa's xcframework links it statically, no `OrtGetApiBase`); no translator on iOS, so the translate card stays hidden. (Slice 55: docs/plan/slices/55-ios-batch9-features.md)
