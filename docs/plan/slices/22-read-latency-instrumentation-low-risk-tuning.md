# Slice 22 — Read-latency instrumentation + low-risk tuning

*Built in part (legacy entry marked `[~]`; moved verbatim by archive-docs 2026-10-20). See the Deferred note below.*

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
