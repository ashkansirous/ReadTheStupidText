# Slice 36 — Disk-backed audio chunks

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 36 — Disk-backed audio chunks.** (Decisions 49, 50) Replaced the
      in-memory per-chunk WAV buffer (`InMemoryRandomAccessStream`)
      `SupertonicSpeechReader` fed to `MediaPlayer` with a temp WAV file per
      chunk under `…\TemporaryFolder\audio\<activity-id>\chunk-<index>.wav`
      (new `AudioChunkPaths`, a sibling of `LogPaths`); `MediaPlayer` now plays
      each chunk via `StorageFile.GetFileFromPathAsync` +
      `MediaSource.CreateFromStorageFile` instead of a stream. A read's folder
      is deleted on natural completion or `Stop()` (both already know the
      read is genuinely done, unlike Skip/mid-read voice-change, which reuse
      the same activity id and folder); a startup sweep
      (`AudioChunkPaths.PurgeOrphaned`, 1 day) catches one orphaned by a read
      that never reached a terminal state. The pure sweep-eligibility rule
      (`AudioChunkSweep.IsEligible`) is unit-tested (`AudioChunkSweepTests`,
      4 cases). `CompositeSpeechReader` needed no change — it already just
      routes to the active engine. Confirmed
      `Windows.Storage.StorageFile`/`Windows.Media.Core.MediaSource` usage via
      context7 first (Decision 9).

## Verification (moved from plan.md)

- **Slice 36:** read a large file, watching Task Manager → memory no longer
  grows unbounded with file size; `…\TemporaryFolder\audio\<id>\` contains a
  WAV per chunk during the read and is gone after it finishes; skip
  forward/backward still works unchanged. Restart with a >1-day-old orphaned
  `audio\` folder present → it's swept on startup.
