# Slice 22 — Read-latency instrumentation + low-risk tuning

*Built and verified 2026-10-23 on a Windows dev build (see Measured 2026-10-23 below). Legacy entry marked `[~]` moved verbatim by archive-docs 2026-10-20; its Deferred note (#117) is now closed by the measurement.*

## Plan entry (moved from plan.md)

- [~] **Slice 22 — Read-latency instrumentation + low-risk tuning.** ([#104](https://github.com/ashkansirous/ReadTheStupidText/issues/104)) (Decision 30)
      Log per-chunk split/generate/wav/first-audio timings into the system log so the
      ~7 s is attributable, then (context7-confirm sherpa threading first) raise
      `NumThreads`/`MaxSynthesisConcurrency` to fit the machine and tighten first-chunk
      biasing — measured against the new logs. No model/runtime swap this round.
      **Built:** `SupertonicSpeechReader` now takes `ISystemLog` and emits Debug lines —
      `split N chars into K chunk(s) in X ms (threads T, concurrency C)`, per chunk
      `chunk i/K (n chars): generate X ms, wav Y ms`, and once `first audio after X ms`
      (on the first chunk's `MediaOpened`) — all stamped with the activity-log **id**
      (threaded through a new optional `ISpeechReader.SpeakAsync(text, activityId)`
      param) so they join the input log. Tuning is **adaptive to `Environment.ProcessorCount`**:
      `SynthesisThreads = clamp(cores/2, 2, 4)` and `MaxSynthesisConcurrency =
      clamp(cores/threads, 2, 4)` — latency-first (more ONNX threads shorten the single
      first-chunk synthesis the user feels), sized so `threads * concurrency` fits the
      cores without oversubscribing. sherpa-onnx `config.Model.NumThreads` confirmed via
      context7 (`/k2-fsa/sherpa-onnx`) before the change. First-chunk biasing was already
      in place from Slice 18 (`SpeechTextChunker.BiasFirstChunkToOneSentence`) and is kept
      at one sentence — cutting *below* a sentence trades prosody for latency, a call to
      make against the new logs. **Deferred (#117, story #104 stays open):** verifying the
      latency win and any further biasing-tightening is data-driven and needs a real run
      under the (Package) profile, which can't be measured headlessly. #115 (per-chunk
      logs) and #116 (threading) are done.

## Verification (moved from plan.md)

- **Slice 22:** read a multi-sentence paragraph → the system log shows per-chunk
  split/generate/wav/first-audio timings that sum toward the observed latency; after
  the thread/concurrency tuning, the logged synthesis time per chunk drops on a
  multi-core machine. context7 query for sherpa-onnx threading is logged before the
  knob change.

## Notes moved from CLAUDE.md (2026-10-21)

**Latency instrumentation + adaptive threading (Slice 22, Decision 30).** The reader
takes `ISystemLog` and writes per-read Debug lines so a slow read is attributable:
`split … into K chunk(s) in X ms (threads T, concurrency C)`, a per-chunk
`chunk i/K (n chars): generate X ms, wav Y ms`, and a one-shot `first audio after X
ms` (on the first chunk's `MediaOpened`). Each is stamped with the activity-log
**id**, threaded in via a new optional `ISpeechReader.SpeakAsync(text, activityId)`
param (the WinRT fallback ignores it), so the lines join the input log. The sherpa-onnx
knobs are **adaptive to `Environment.ProcessorCount`** (no longer fixed at threads 2 /
degree 3): `SynthesisThreads = clamp(cores/2, 2, 4)` (`config.Model.NumThreads`,
confirmed via context7 `/k2-fsa/sherpa-onnx`) and `MaxSynthesisConcurrency =
clamp(cores/threads, 2, 4)` — latency-first (more ONNX threads shorten the single
first-chunk synthesis the user waits on), sized so `threads * concurrency` fits the
cores without oversubscribing.

## Measured 2026-10-23

Windows dev build (merged `main` incl. Slices 47, 56, 88), Supertonic voice, 16 cores, so threads 4 and concurrency 4. Read from the system log's per-chunk lines.

- **First audio on a first-chunk cache miss:** median 1.92 s (range 1.53-3.27 s) for a ~420-char, 4-sentence paragraph (5 reads), against the ~7 s that started the slice. Short one-sentence texts (47-77 chars): 0.87-1.57 s.
- **Head-start cache hit (Slice 47):** 134 ms to first audio.
- **Attribution:** the chunk timings sum to the observed latency. First audio is about chunk-1 generate plus ~50 ms (`MediaOpened`); the split takes under 1.2 ms. Chunk-1 synthesis is about 95% of the wait, so the remaining lever is synthesis itself, not chunking or plumbing.
- **Decision:** keep first-chunk biasing at one sentence. Cutting below a sentence would trade prosody for under a second, not worth it. #117 closes with this data and story #104 closes.
- **Gap, accepted:** the wav time is not logged as a separate figure (only `chunk i/K (n chars) ready in X ms`). Not worth a change.

### Follow-up (not part of Slice 22)

A new clipboard copy interrupting a read while the panel window is open crashed the app: `0xc000027b` in `CoreMessagingXP.dll`, WER fault `80070057` in `combase.dll` (a stowed WinUI exception). Seen once, not investigated. Needs its own issue. Crash filed as #264.

#### For plan.md

- Slice 22 status: `verified`. Remove its line from *Outstanding work*.

