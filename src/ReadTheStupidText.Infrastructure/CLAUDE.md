# CLAUDE.md — Infrastructure (Windows speech engines, packs, logging)

Loaded when you work under `src/ReadTheStupidText.Infrastructure`. The tray UI and read orchestration
rules are in `src/ReadTheStupidText.App/CLAUDE.md`. Tray-only code (hotkey, UIA selection monitor, clipboard listener, selection copier, foreground probe, startup task) lives in `src/ReadTheStupidText.Windows.Tray`, not here: Dino Reader's Windows head shares this project, so keep tray code out (Slice 128: docs/plan/slices/128-tray-code-out-of-shared-infrastructure.md). `Input/NativeMethods.cs` here is only the clipboard-read subset `ClipboardReader` uses.

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
- **Norwegian translation packs (Slice 106).** The pack code is `nb`; `TranslationFlow.NormalizeTag` maps `no`/`no-NO` to it. Both legs come from TigreGotico's
  gmq exports (no Xenova conversion exists); `en-nb` needs `targetToken` `>>nob<<`; the `nb` merged decoders are
  ~337 MB (fp32 despite the `int8/` folder). `packs.yml` falls back to `max_position_embeddings` when `max_length`
  is null (the job fails if both are), and each table row names its repo layout and licence. `NormalizeTag` maps `no`
  to `nb`; `nn` is out of scope. (Slice 106: docs/plan/slices/106-windows-nordic-dutch-translation-packs.md)
- Pack voices show as "Voice N (woman/man)" in every language, gender from the speaker code's first letter via `NorwegianVoices`; `SupertonicVoiceTable.Serves` must keep Supertonic off `nb`. (Slice 104: docs/plan/slices/104-piper-norwegian-pack.md)

- Voice flavours are Dino-only and live in `DinoReader.Core` (Slice 129, Decision 136): the engine knows only `IVoicedAudioTransform` (Application: cache tag, pace, sentence gap, `Apply` on the samples), carried on `VoicedSegment.Transform`/`SpeechChunkPlan.Transform`; null is a plain read (RTST, head start). Never name a flavour, pitch or animal effect in this project, Application or the readers. Head start (`NeuralSentenceSource.AreCached/PrepareAsync`) takes the transform in its key; `PrepareChunksAsync` passes null. The synthesize/transform/join logic is copied in the Windows `NeuralSentenceSource`, Android and iOS readers: fix it in all three. (Slice 113: docs/plan/slices/113-dino-character-flavours.md; Slice 129: docs/plan/slices/129-dino-voice-features-out-of-shared-engine.md)
- Kokoro (`VoiceEngineKind.Kokoro`) is Dino-only storyteller voices (en, es, pt-br, zh): one model, speaker id picks the voice, config via `ConfigureKokoro` (model, voices, tokens, lexicon, espeak data dir) in `SherpaEngineFactory`, the Android reader and `IosTtsEngine` (keep the three in step). Its pack `kokoro-en` has catalog kind `storyteller`; the text reader never resolves it. Kokoro is a per-language engine (`lang` "" for en, `es`, `pt-br`, `cmn`) and only one stays resident (`EngineCache`, `ModelKey` IsKokoro plus language): keep the three engine builders (`SherpaEngineFactory`, Android and iOS `KokoroEngineConfig`) and the three readers' model keys in step. (Slice 114: docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md; Slice 118: docs/plan/slices/118-dino-storyteller-more-languages.md)
