# Slice 270 — Dino iPhone crash: the audio player is thrown away inside its own "finished playing" call

*In progress (wave 2026-11-05), draft PR.* Bug fix, Dino Reader, iOS (Android checked for the same pattern). Reported by the user on 2026-11-05: "I just tried the app on my iphone and it crashed multiple times. I sent the crash report."

**Touches:** `src/DinoReader.App/Platforms/iOS/RecordingAudio.ios.cs`, `src/DinoReader.App/RecordingAudio.cs`, `src/DinoReader.App/Platforms/Android/RecordingAudio.android.cs`, `src/DinoReader.App/Platforms/Windows/RecordingAudio.windows.cs`, `src/DinoReader.App/ReadingSession.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/270-dino-ios-player-disposed-in-finished-callback.md`.

## What the crash reports say (read 2026-11-05 with `node tools/crashes/pull-crashes.mjs --app dino --from 2026-11-05`)

- **4 crashes, one group:** iOS, TestFlight, Dino 1.106.0, one iPhone (iOS 16, under 3 GB memory, session `0fb31c66…`), the newest at 08:25 UTC.
- `System.ObjectDisposedException`: "the player object was Dispose()d during the callback, this has corrupted the state of the program", thrown in `AVFoundation.InternalAVAudioPlayerDelegate.FinishedPlaying`. No frame of our code is on the stack: the throw comes from the .NET binding once our handler returns.
- The log tail of the newest report ends at 08:23:30 with the shelf books' pre-made audio packs downloading in fa and en (`shelf audio fa: 4 of 6 done`, Slice 267's cold-start fetch). The lines between that and the crash were not flushed.

## Cause (very likely, read from the code; not yet shown by a run)

1. Pre-made clips, a parent's recording and a recorded clip all play through `RecordingPlayer` (`RecordingAudio.cs:86`). On iOS it is one `AVAudioPlayer` (`Platforms/iOS/RecordingAudio.ios.cs`).
2. When a clip finishes, Apple calls `FinishedPlaying` on the main thread, and our handler calls `ended()` directly (`RecordingAudio.ios.cs:81-95`).
3. `ReadingSession` passes `ended` as `() => MainThread.BeginInvokeOnMainThread(() => ApplyPremadeEnded(...))` (`ReadingSession.cs:769`; also `ApplyClipEnded` at 731 and `ApplyRecordingEnded` at 452). MAUI's `BeginInvokeOnMainThread` **runs the action at once when already on the main thread** (checked in dotnet/maui `src/Essentials/src/MainThread/MainThread.shared.cs`: `if (IsMainThread) action();`).
4. `ApplyPremadeEnded` calls `AdvanceStep` (`ReadingSession.cs:818`), which plays the next clip. `RecordingPlayer.Play` calls `Stop()` first, and the iOS `PlatformStop` does `player.Stop(); player.Dispose();`, on the very player whose `FinishedPlaying` is still running.
5. The binding sees its player disposed during the callback and throws, and the app dies.

The voice reader (`IosSupertonicSpeechReader.Playback.cs`) does not have this bug: its `ended` task uses `RunContinuationsAsynchronously`, so the next chunk starts after Apple's call returns.

**Why it shows now:** pre-made playback chains clip after clip only when a book has its pack. Slice 267 (in Dino 1.105.0) made the shelf books fetch their packs on every cold start, and the user's log shows them downloading a minute before the crash. So the phone probably played several pre-made clips in a row for the first time. Not proven.

**Why the tests did not catch it:** pre-made playback is tested only on the Android emulator (`premade-audio-plays`, `shelf-audio-on-start`); Android's player does not throw on this. No iOS run plays two clips in a row (iOS gets a CI compile only; no simulator or device flow covers reading).

## Work

- [ ] iOS: never dispose an `AVAudioPlayer` inside its own delegate call. `FinishedPlaying` and `DecoderError` hand `ended` and `failed` to the main queue asynchronously (`DispatchQueue.MainQueue.DispatchAsync`), so the next clip starts after Apple's call returns. Also `PlatformStop` releases a player that is mid-callback only after the call returns (keep a "finishing" flag, dispose on the next main-queue turn). Check the dispatch API with `library-docs` (Microsoft.iOS bindings) first.
- [ ] Android: check `RecordingAudio.android.cs` for the same pattern (releasing a `MediaPlayer` from its own `Completion` handler); if present, same async hand-off. Android and iOS behave the same afterwards.
- [ ] Shared: `RecordingPlayer`'s contract says `ended` and `failed` always run after the platform callback has returned; a unit test with a fake platform player that fails if it is disposed while its callback runs (play clip 1, end it, the session plays clip 2 from inside the end).
- [ ] A Debug and release log line when a clip ends and the next one starts (`[AudioPack] clip N ended, next`), so the next crash report shows how far a book got.
- [ ] Prove it: an iOS run plays a book with pre-made audio for at least 10 clips in a row on the current build (expect the crash), then on the fix (no crash). Where no iOS simulator or device run exists for the wave, the TestFlight build goes to the user's iPhone and the crash store is read the next morning (`pull-crashes.mjs --app dino --kind managed`); the slice is `built`, not `verified`, until that read shows no new `FinishedPlaying` crash.
- [ ] `src/DinoReader.App/CLAUDE.md`: one rule line: a native player's "finished" handler never starts, stops or disposes a player directly; it hands off to the next main-loop turn.

## Verification

- `dotnet test tests/DinoReader.Tests` (the hand-off test); the iOS build compiles in CI; Android `premade-audio-plays` and `shelf-audio-on-start` still pass on the emulator.
- The user's iPhone on the fix build: a book with pre-made audio reads to the end with no crash, and `pull-crashes.mjs` shows no new `ObjectDisposedException` in `FinishedPlaying` for that version.
