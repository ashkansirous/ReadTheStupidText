# Slice 32 — Camera capture → OCR → read

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 32 — Camera capture → OCR → read.** (Decision 40, 43 — Screen 2)
      Add the camera screen (MAUI `MediaPicker`/`CameraView`-equivalent, confirm
      current API via context7): dark viewfinder with a detection frame + "TEXT
      FOUND" chip, a capture bar (gallery / shutter / flash), and a result bar
      with a **Read** button. `MlKitImageTextExtractor` implements the new
      `IImageTextExtractor` (Application); a captured photo's extracted text
      flows through the existing `SpeechTextChunker`/`ReadAloudService` pipeline
      exactly like typed text. Single-shot only, per Decision 40. This is the
      headline new capability the user asked for: "take a picture of the stuff,
      then read it for them."
      **Built:** Confirmed via context7 that MAUI has no embedded live-preview
      camera control (`CameraView` is a Community Toolkit/third-party thing, not
      MAUI itself) — `MediaPicker.Default.CapturePhotoAsync()` launches the
      **system** camera app instead, so Screen 2 is a plain-native-default
      capture flow (idle "take photo" card → processing spinner → extracted-text
      result card with Retake/Read) rather than the design mock's in-app dark
      viewfinder/detection-frame/shutter-bar chrome, same disclosed-simplification
      allowance Slice 30 used for Screen 1's first pass. `IImageTextExtractor`
      (Application/Images, mirroring `IDocumentTextExtractor`'s shape but with
      no `CanHandle` — a capture is always an image, nothing to route) is
      implemented by `MlKitImageTextExtractor` over **Google ML Kit's on-device
      Latin text recognizer** (`Xamarin.Google.MLKit.TextRecognition`), per
      Decision 40. Unlike sherpa-onnx's packaging bug, ML Kit's native `.so` and
      model assets are correctly per-ABI out of the box — confirmed the same
      way, by unzipping the built APK: `lib/arm64-v8a/` +
      `lib/x86_64/libmlkit_google_ocr_pipeline.so` (~11 MB each, both real,
      no wrong-OS collision) and the recognizer's `.tflite`/`.binarypb` model
      files under `assets/mlkit-google-ocr-models/` — meaning recognition is
      **fully on-device from first launch, zero network**, stronger than the
      Play-services-download story originally assumed for Decision 40's "we
      collect nothing" framing. Awaiting the Java `Task<Text>` from
      `ITextRecognizer.Process()` needed the `Android.Gms.Extensions` package
      (`Xamarin.GooglePlayServices.Tasks`) for its `GetAwaiter()`/`AsAsync<T>()`
      extensions — not obvious from the compiler's first error (a bare
      "namespace not found"); found by loading the referenced DLL into a scratch
      console project and enumerating its public strings for the real namespace,
      the same "trust the built artifact, not the guess" instinct that caught
      the sherpa-onnx bug. `CameraPage` reads through the exact same
      `ISpeechReader` **singleton** `TypePage` uses (registered once in
      `MauiProgram`) — tapping **Read** calls `SpeakAsync` on that shared
      instance directly, so it is genuinely "the same pipeline," not a
      look-alike second one; switching to the Type tab mid-read shows the same
      in-progress read on its transport controls, for free. `CAMERA` permission
      + a `<queries>` package-visibility entry for `IMAGE_CAPTURE` added to
      `AndroidManifest.xml`; `Permissions.Camera` itself is requested
      internally by `MediaPicker`, not requested by app code. Verified:
      `dotnet build` (Mobile alone and the full `.slnx`, 0 errors), the
      108-test suite (no regressions — this slice added no new pure logic to
      test), and unzipping the built debug APK to confirm the OCR native
      libraries/model assets as above. **Not yet verified: running on a
      device/emulator** (camera capture doubly so, since most emulators have
      no usable camera) — same environment gap as Slices 30-31.

## Verification (moved from plan.md)

- **Slice 32:** tap the camera button, photograph a printed page → the extracted
  text begins reading aloud shortly after capture, through the same chunked
  pipeline as every other input path.
