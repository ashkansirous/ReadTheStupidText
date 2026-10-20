# Slice 35 — Fix the stuck elapsed/total read-through timer

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 35 — Fix the stuck elapsed/total read-through timer.** (Decision
      51) Root-caused from the packaged app's own `system-*.log`/`input-*.log`
      (Decision 27) left over from a prior session: a file-upload read's chunks
      8-29 were resynthesized in overlapping bursts for two minutes and the
      read never reached `Read`. Cause: `SkipForward`/`SkipBackward` cancels
      the reader's in-flight generation (`BeginGeneration`), but sherpa-onnx's
      `OfflineTts.Generate` is a synchronous native call — passing a
      `CancellationToken` to `Task.Run` only stops it from *starting*, it can't
      interrupt one already running. So a second Skip pressed before the first
      one's target chunk finished synthesizing didn't replace that work, it
      left it running as a "zombie" generation that (a) still wrote its chunk's
      duration into the shared `ReadTimingTracker` after the fact, and (b) kept
      competing for the same thread pool as the generation that superseded it —
      starving the *current* read's own synthesis and making the timer (and
      the read) look permanently stuck. Fix:
      `SupertonicSpeechReader.GenerateChunkAsync` now calls
      `token.ThrowIfCancellationRequested()` right after `Generate` returns,
      before touching `_timing` or building the WAV stream, so a superseded
      generation's already-sunk synthesis time can no longer corrupt the
      active read's state; `ReadAloudService.SkipForwardAsync`/
      `SkipBackwardAsync` also serialize on a single in-flight skip so
      back-to-back presses can't start the cascade in the first place.
      `ReadTimingFormatter`/`ReadTimingTracker` themselves had no bug — the
      108 existing unit tests still pass unchanged; the WinRT/native
      synthesis path isn't unit-testable (needs package identity), so this
      needs a live-run confirmation per the project's existing convention.

## Verification (moved from plan.md)

- **Slice 35:** trigger a read that used to leave the timer stuck → `Total`
  now resolves and the `mm:ss/mm:ss` display keeps ticking for the whole read.
  `dotnet test` covers the regression case for whatever the root cause was.
