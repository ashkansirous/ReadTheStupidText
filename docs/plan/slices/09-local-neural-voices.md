# Slice 9 — Local neural voices (sherpa-onnx + Supertonic-3)

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 9 — Local neural voices (sherpa-onnx + Supertonic-3).** Replaces the
      built-in voices with a local neural engine (see Decision 14). Added the
      **sherpa-onnx** runtime (Apache-2.0) + **Supertonic-3** model (Apache-2.0)
      via the `org.k2fsa.sherpa.onnx` NuGet package; Kokoro (Chinese-focused,
      no English male voice in the latest) and Piper (GPL) were rejected.
      `IVoiceModelService` (Application) + `SupertonicModelService` (Infrastructure)
      locate `sherpa-onnx-supertonic-3-tts-int8-2026-05-11`, which is **bundled in
      the package** under `VoiceModel/` (committed to the repo, ~145 MB) and read
      from `AppContext.BaseDirectory` — so it's ready offline at first launch, no
      download. `VoiceModelPaths` is just the model root dir, `SupertonicFiles`
      holds the layout. `SupertonicSpeechReader` builds an `OfflineTts` lazily
      (Supertonic config: duration_predictor / text_encoder / vector_estimator /
      vocoder + tts.json + unicode_indexer + voice.bin — no espeak, no lexicon),
      synthesizes PCM at 1×, wraps it as an in-memory WAV stream, and plays it
      through the existing `MediaPlayer` so the 0.5–2.0× slider stays live and
      pitch-corrected. `CompositeSpeechReader` routes to Supertonic (with the WinRT
      voice as a safety-net fallback only if the bundled files are missing);
      `NeuralVoiceCatalog` exposes **only** the Supertonic voices
      (`SupertonicVoiceTable`, the fixed 10-style set F1–F5/M1–M5 in sorted sid
      order, default *Male 1*); the panel/menu rebuild on
      `ReadAloudService.VoicesChanged`. A build-time target drops the duplicate
      `onnxruntime.dll` that WinML (`Microsoft.Windows.AI.MachineLearning`) and
      sherpa both ship, keeping sherpa's version-matched copy; the unused
      `systemAIModels` capability was removed (no `internetClient` is needed since
      the model ships in the package). sherpa-onnx C# API, model id, voice sid
      order (`generate_voices_bin.py` uses `sorted(*.json)`), and licensing all
      confirmed via context7 + NuGet + HF + the sherpa docs before coding.
      *Auto-read debounce (fix):* UIA fires a `SelectionChanged` per character
      while the user drag-selects, which previously triggered a burst of
      overlapping reads (the play/pause state bounced). `ReadAloudService` now
      debounces selections (500 ms quiet period, superseded reads cancelled via a
      swapped `CancellationTokenSource`), so a drag collapses into one read.
      *Unverified at build time (needs a real run):* neural audio output and the
      native runtime loading under package identity.

## Verification (moved from plan.md)

- **Slice 9:** on first launch (offline is fine — the model ships in the
  package) → the picker immediately lists the Supertonic neural voices (default
  Male 1; 5 male + 5 female) and the tray Voice submenu is present; a read uses
  the selected neural voice and sounds natural; the speed slider still changes
  the rate live; pick a different voice → the next read uses it; restart → the
  chosen voice is restored. **Auto-read debounce:** drag-select a sentence → it
  is read **once** after the selection settles (no burst of play/pause and no
  overlapping reads). Confirm the packaged build runs under package identity and
  that audio plays (the build-time ORT dedupe didn't break the native engine).
