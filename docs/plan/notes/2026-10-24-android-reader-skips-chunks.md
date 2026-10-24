# 2026-10-24: Android read-aloud skips text (MediaPlayer state error)

*Fix: `fix/android-reader-skips-chunks`.*

## Symptom

On a Pixel 10 Pro (v1.37.0) Dino Book Reader "jumps over" text while reading a book: part of a
sentence is cut off and reading carries on with the next one. The same reader
(`AndroidSupertonicSpeechReader`, Mobile.Shared) serves Read The Stupid Text on Android.

## Evidence (logcat, Pixel 10 Pro)

```
22:33:43.328 NuPlayerDriver created                      <- next chunk: Reset() + PrepareAsync()
22:33:43.344 E/MediaPlayerNative: Attempt to call getDuration in wrong state: mCurrentState=4
22:33:43.344 E/MediaPlayer: Error (-38,0)
22:33:43.464 NuPlayerDriver created                      <- the chunk after, ~120 ms later
```

State 4 is PREPARING. The chunk prepared at 43.328 was played for ~0.1 s and abandoned.

## Root cause

`AndroidSupertonicSpeechReader.cs`:

1. The progress poll loop (`StartPolling`/`PollLoopAsync`/`ReportProgress`) is linked to the
   *read's* token, not the chunk's, so it keeps running across chunks. `PlayChunkAsync` calls
   `player.Reset()` + `PrepareAsync()` on the same player while the old loop still calls
   `player.Duration`/`CurrentPosition` (`ReportProgress` does not check `_playerReady`).
   Calling `getDuration` while PREPARING is an illegal-state call: MediaPlayer goes to its Error
   state (-38, INVALID_OPERATION).
2. `EnsurePlayer` registers only `Completion`. With no error listener (or one that returns
   unhandled), Android calls `OnCompletion` after an error, so `_chunkEnded` is set to `true`
   ("ended naturally") and the loop moves on: that chunk's audio is skipped.

## Fix

- One `_playerLock` serialises every MediaPlayer call together with the `_playerReady` check that
  guards it: the poll tick, Reset/SetDataSource/PrepareAsync, Start (including the
  `_state == Paused` check), Pause, Resume, SetSpeed, Stop's Reset and Dispose. It is never held
  across an await; the player's event handlers only set flags/TCS (and `MarkPlayerNotReady`).
- Skip and timing read the live position under that lock (no cached position); it is zero when the
  player is not ready or the reader is Idle.
- Completion, Prepared and Error handlers are attached per chunk and carry the chunk's sequence
  number (`_chunkSequence`); events from a superseded chunk are ignored, so a stale chunk never acts.
- Error listener: logs `what`/`extra`, marks the error handled (no spurious `Completion`) and
  completes a pending prepare so it cannot hang the read. A mid-play error still ends the chunk and
  the read moves on (logged as a warning).
- Pause while a chunk is still preparing is honoured: the chunk stays prepared, not playing. Resume
  only acts when the reader is Paused; if the chunk is not ready it just sets Playing and
  `StartPreparedChunk` (which checks the state under the lock) starts it.

Not yet verified on the phone.
