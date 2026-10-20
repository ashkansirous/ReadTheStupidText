# Slice 31 — Neural voice on Android (sherpa-onnx + Supertonic-3)

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 31 — Neural voice on Android (sherpa-onnx + Supertonic-3).**
      (Decision 39, 43 — Screen 3) Bundle the same Supertonic-3 model used on
      Windows via Play Asset Delivery; port `SupertonicSpeechReader`'s synthesis
      logic to the sherpa-onnx Android binding; `CompositeSpeechReader`-style
      routing with Android `TextToSpeech` as the safety-net fallback only. Builds
      Screen 3 (voice picker): grouped `MALE`/`FEMALE` cards listing the same ten
      Overlord-named voices (Decision 19, `SupertonicVoiceTable` reused unchanged
      from Domain) with a preview-play affordance; a change applies at the next
      chunk, matching Slice 23's Windows behavior. Settings persistence (voice
      id, playback rate) via a MAUI `Preferences`-backed `ISettingsStore`
      implementation (Decision 41).
      **Built:** *Layering fix (same pattern as Slice 28's), enabling this whole
      slice:* `SupertonicVoiceTable`, `SupertonicFiles`, `ReadTimingTracker`, and
      `SpeechTextChunker` were misplaced in Infrastructure (net10.0-windows-only,
      unreachable from Mobile) despite having zero framework dependencies —
      Domain and Application weren't actually "unchanged," they were made
      correct so the reuse the plan promises is real. `SupertonicVoiceTable`
      (now `public`, plus a new `IsFemale(voiceId)` for the MALE/FEMALE
      grouping, unit-tested) and `SupertonicFiles` moved to
      `Domain.Reading`; `ReadTimingTracker` and `SpeechTextChunker` moved to
      `Application.Reading` (both still `internal` — `SpeechTextChunker` needed
      a new `InternalsVisibleTo` for `ReadTheStupidText.Mobile` alongside the
      existing `Tests`/now-added-back `Infrastructure` ones). All four now have
      exactly one implementation shared by both platforms instead of a second
      copy waiting to drift.
      *Native packaging (the real blocker):* `org.k2fsa.sherpa.onnx`'s nuspec
      unconditionally depends on all 8 platform `runtime.*` sub-packages; a
      plain `PackageReference` on Android silently bundled the **linux-x64**
      `libonnxruntime.so`/`libsherpa-onnx-c-api.so` into `lib/arm64-v8a/`
      instead of the real Android ones — same failure as
      [microsoft/onnxruntime#29270](https://github.com/microsoft/onnxruntime/issues/29270),
      confirmed by inspecting the built APK, not just trusting the build log.
      Fix: `ExcludeAssets="all"` on the seven non-Android `runtime.*` packages
      (kept as pinned-but-inert references) plus `GeneratePathProperty="true"`
      on `org.k2fsa.sherpa.onnx.runtime.android-arm64`, then two explicit
      `AndroidNativeLibrary` items (`Abi="arm64-v8a"`) pointing at its
      `$(Pkgorg_k2fsa_sherpa_onnx_runtime_android-arm64)\runtimes\...\native\*.so`
      — confirmed by unzipping the built APK a second time. No android-x64
      native package exists upstream, so the neural engine is unreachable on a
      plain x86_64 emulator; `CompositeSpeechReader`-style fallback to
      `AndroidSpeechReader` (Slice 30) covers that by design, the same as a
      missing/broken model on Windows. `SupportedOSPlatformVersion` bumped
      21→23 for `MediaPlayer.PlaybackParams` (pitch-preserving speed change).
      Since raised to 28 by Decision 82.
      *`AndroidSupertonicSpeechReader`* (`Platforms/Android`) ports the Windows
      reader's chunking/ordered-playback/generation-counter/mid-read-voice-swap
      design essentially unchanged (it's the same pure logic, now shared via
      the layering fix above); only the playback primitive differs —
      `Android.Media.MediaPlayer` has no in-memory-stream play path, so each
      synthesized chunk is written to a temp WAV file
      (`FileSystem.Current.CacheDirectory`) and played via `SetDataSource`,
      and there's no position-changed event, so progress/timing are driven by
      a 200ms poll loop instead of a push event. `MobileVoiceModelService`
      extracts the model (packaged as a `MauiAsset`, referencing the *same*
      committed `App/VoiceModel/*` files — no second 139 MB copy in the repo)
      from the package to app-local storage once on first launch, since
      sherpa-onnx's native loader needs real file paths, not an
      asset-package stream; this is a **plain-bundle stand-in for the real
      Play Asset Delivery** — untestable here without a live Play Console
      listing to deliver from (Slice 34 revisits it once one exists), fine for
      local sideload testing meanwhile. `VoicePickerPage` (Screen 3, pushed via
      a registered Shell route from `TypePage`'s mic button) builds its
      MALE/FEMALE rows in code rather than a `CollectionView` (10 static
      items); tapping a row selects **and** previews it by speaking its own
      display name — hearing the voice *is* the preview, so there's no
      separate preview affordance to wire up (a disclosed simplification of
      the design mock's separate per-row play icon). `App.xaml.cs` now takes
      `ISpeechReader`/`ISettingsStore`/`IVoiceModelService` via constructor
      injection (`UseMauiApp<App>` resolves `App` from the DI container) and
      applies the persisted speed/voice immediately, then awaits model
      location + engine warm-up. Verified: `dotnet build` (Mobile alone and
      the full `.slnx`), the 108-test suite (6 new `IsFemale` cases), and
      unzipping the built debug APK to confirm both the native libraries and
      `assets/VoiceModel/*` land where expected. **Not yet verified: running
      on a device/emulator** — same environment gap as Slice 30.

## Verification (moved from plan.md)

- **Slice 31:** open the voice picker → the same ten Overlord-named voices appear;
  pick one → the next read uses it and sounds like the Windows Supertonic voice,
  not the Android system voice; restart the app → the chosen voice and speed are
  restored (`Preferences`-backed settings). Force the model to fail to load (test
  hook) → falls back to Android `TextToSpeech` without crashing.
