# CLAUDE.md — Infrastructure (Windows speech engines, packs, logging)

Loaded when you work under `src/ReadTheStupidText.Infrastructure`. The tray UI and read orchestration
rules are in `src/ReadTheStupidText.App/CLAUDE.md`.

Note (Windows; Android/iOS use downloaded packs): the narrator voice is a **local neural voice** (Slice 9, Decision 14) — the
**sherpa-onnx** runtime (Apache-2.0) running the **Supertonic-3** model
(Apache-2.0, `sherpa-onnx-supertonic-3-tts-int8-2026-05-11`), via
`org.k2fsa.sherpa.onnx`. The model (~145 MB) is a **downloaded pack** (Slice 56,
see the Windows packs rule below; `VoiceModel/` stays in the repo only as the source for `packs.yml`); the picker offers the pack's
voices plus a labelled "Windows voice". The Supertonic voices (`SupertonicVoiceTable`, the fixed 10-style F1–F5/M1–M5 set in
sorted sid order, modelled as `VoiceInfo` records). The built-in WinRT
`SpeechSynthesis` voice is the first-run voice until the pack lands and the fallback
if the pack is missing; it is selectable as "Windows voice" (Slice 56). Narrator's
"Natural"/neural voices are unreachable by a Store app, so we bring our own
engine. **Piper is GPL — do not use it; Kokoro was rejected** (Chinese-focused,
no English male voice in the latest, ships GPL-adjacent espeak data); Supertonic
is English-first, Apache-2.0, espeak-free, and Store-safe. (The one scoped GPL
exception, espeak-ng data inside the downloaded `piper-fa` and `piper-nb` packs, is in the Mobile rules.)

- Windows packs (Slice 56): the MSIX holds no voice model and declares `internetClient` (STORE.md, PRIVACY.md and `site/privacy.html` say so). Packs live in `LocalCacheFolder\packs\<id>`, never `LocalFolder` (cloud-backed). `PackVoiceModelService` and `WindowsVoicePackService` are Windows copies of the Mobile ones, `VoicePackSetup` (Application) the MAUI-free twin of `VoicePackInstaller`: change both sides together. `windows.yml` has no LFS; keep `RemoveWinMlOnnxRuntimeDuplicate`. (Slice 56: docs/plan/slices/56-windows-packs-language-headstart.md)
- Pack versions and engine cache (Slice 89): packs install to versioned folders with an `active` pointer, and engines come from the shared `EngineCache`; the full rule is in `src/ReadTheStupidText.Mobile/CLAUDE.md` (Voice packs) and applies to Windows too. (Slice 89: docs/plan/slices/89-four-farsi-voices.md)
- Windows head start (Slice 56): `SupertonicSpeechReader` implements `IHeadStart`, backed by `SentenceAudioCache` in `LocalCacheFolder\sentences`; chunks are joined into the per-read play file, so `DeleteRead` and the startup sweep never touch cached clips. `ReadAloudService.BeginAutoRead` prepares after an 80 ms stable wait (`HeadStartStableMs`); hotkey and Play reads do not. Reads are owned by `ActiveRead`/`ReadOwnership`. (Slice 56: docs/plan/slices/56-windows-packs-language-headstart.md)

Neural reader (Slices 17, 18, 22, 23; docs/plan/slices/17-warm-the-neural-engine-at-startup.md, 18-adaptive-settle-delay-smaller-first-chunk.md, 22-read-latency-instrumentation-low-risk-tuning.md, 23-voice-swap-continues-the-current-read.md): `SupertonicSpeechReader` chunks text (`SpeechTextChunker`, paragraph then sentence then word, ~200 chars; the first chunk biased to one sentence) and synthesizes chunks concurrently, playing them in order. A stale read is torn down by a generation counter + `CancellationToken` (`ISpeechReader.Stop()`) so stale synthesis never reaches the shared `MediaPlayer`; `read` is marked only on `Completed`, never on a stop-induced idle. Per-read Debug lines (split, per-chunk generate/wav ms, first audio) carry the activity id via the optional `SpeakAsync(text, activityId)` param. Sherpa knobs adapt to `Environment.ProcessorCount`: `SynthesisThreads = clamp(cores/2, 2, 4)`, `MaxSynthesisConcurrency = clamp(cores/threads, 2, 4)`. A mid-read voice change applies at the next chunk boundary: `SpeakChunksAsync` takes the speaker as a parameter, `SetVoice` records `_speakerId`, the loop restarts at the next chunk (`BeginGeneration` cancels old-voice synthesis); nothing is repeated or skipped. Startup warm-up: `ReadAloudService` calls `ISpeechReader.WarmUpAsync()` once the model is located (builds `OfflineTts`, runs one discarded synthesis); `EnsureTts()` is double-checked under a lock as the safety net; WinRT's is a no-op. Auto-read settle is adaptive: 150 ms for a lone select, extended to 500 ms while events arrive within `BurstGapMs`; the swapped `_selectionCts` makes late reads harmless; both auto-read paths share it in `ReadAloudService`.

- Windows translation is Opus-MT ONNX in `translation-<from>-<to>` packs, pivoting through English. `PackTranslator`
  loads legs lazily and must dispose under its gate. Translated reads stream: the head of 1-2 sentences speaks while later
  blocks are appended through `ISpeechReader.AppendText`; extend `ReadTimingTracker` inside `ChunkFeed`'s lock. Each block
  has a 20 s budget, and failure falls back to the original from that block on. fa and ja targets are unsupported, so the
  toggle stays hidden. `PackConstants.CatalogUrl` is shared by every platform: a new `packs-N` release must re-ship every
  existing pack at the same versions. (Slice 75: docs/plan/slices/75-windows-translation-engine.md)
