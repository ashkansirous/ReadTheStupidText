# Slice 26 — Skip forward / backward (±10s)

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 26 — Skip forward / backward (±10s).** (Decision 32, Batch 5)
      `ISpeechReader.SkipForward()` / `SkipBackward()`: using the per-chunk duration
      index from Slice 25, compute the chunk whose cumulative start time is nearest
      at/after `elapsed ± 10s`, tear down current playback via the existing
      generation-counter mechanism, and resume `MediaPlayer` at that chunk (waiting on
      it if still synthesizing). Clamp backward at 0; clamp forward at the furthest
      point reached so far (can't skip into audio not yet synthesized). Two new icon
      buttons flank the play/pause circle in the transport row, wired to
      `ReadAloudService.SkipForward/SkipBackward`. Unit-test the pure
      target-chunk-selection and clamping logic.
      **Built:** a new pure `SkipTarget(int ChunkIndex, TimeSpan ChunkStart)` record
      (`Application.Reading`) and `ReadTimingTracker.ComputeSkipTarget(elapsed, delta)`
      (Infrastructure, unit-tested — `ReadTimingTrackerTests`) do the target-chunk
      selection: forward rounds up to the nearest known chunk start at/after the
      target, clamped to the furthest reachable boundary (the start of the next chunk
      is knowable, and selectable, even before that chunk itself has finished
      synthesizing); backward rounds down to the nearest known chunk start at/before
      the target, clamped to zero. A new `ReadTimingTracker.SeekTo(SkipTarget)`
      repositions the elapsed baseline without touching already-recorded chunk
      durations, so replaying a chunk after a skip folds its duration back in exactly
      like a first playthrough. `SupertonicSpeechReader.SkipForward/SkipBackward` keep
      the read's chunk list (`_chunks`, set in `SpeakAsync`) so a skip can restart the
      ordered playback loop (`SpeakChunksAsync`) at the target chunk — reusing the same
      generation-counter teardown as `Stop()`/the Slice 23 voice-swap. The WinRT
      fallback (`SpeechReader`) is a single fully-synthesized stream, so instead of
      chunk-snapping it seeks `MediaPlaybackSession.Position` directly (exact, not
      best-effort — confirmed via context7 against
      `/websites/learn_microsoft_en-us_windows_apps`), clamped to
      `[0, NaturalDuration]`. `CompositeSpeechReader` and `ReadAloudService.
      SkipForwardAsync/SkipBackwardAsync` just forward to the active engine. UI: two
      28px ghost icon buttons (`SkipBackButton`/`SkipForwardButton`, Segoe Fluent
      Icons `Rewind`/`FastForward` glyphs `EB9E`/`EB9D` — the font's only true ±10s
      pair is `SkipBack10`/`SkipForward30`, mismatched at 10s/30s, so the plain
      direction glyphs were used instead) flank the play/pause circle in the transport
      row. Both engines gate the skip on `State != Idle` rather than on `_chunkCount`/
      `NaturalDuration` (a code-review catch): neither reader clears those on natural
      completion — only `Stop()` does — so gating on them let a skip after a read
      finished silently resurrect/replay it; `State` is the field completion always
      updates first, on both engines. UI/native code isn't unit-tested per the
      project's test story; runtime check under the (Package) profile remains.

## Verification (moved from plan.md)

- **Slice 26:** during a read, click skip-forward → elapsed jumps ~10s to the
  nearest chunk boundary and audio resumes there; skip-backward similarly; clamps
  at 0 and at the furthest synthesized point. `dotnet test` covers the pure
  target-selection and clamping logic.
