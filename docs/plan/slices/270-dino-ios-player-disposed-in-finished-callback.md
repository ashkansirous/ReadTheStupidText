# Slice 270 — Dino iPhone crash: the audio player is thrown away inside its own "finished playing" call

*Built 2026-11-05 (wave 2026-11-05), merged as PR #834 (75a3a7c8). Verified 2026-11-06 by the user on iPhone (TestFlight): tested, no crashes (also 2026-11-06 ~00:10: "no crash on reading stories").* Bug fix, Dino Reader, iOS (Android checked for the same pattern). Reported by the user on 2026-11-05: "I just tried the app on my iphone and it crashed multiple times. I sent the crash report."

**Touches:** `src/DinoReader.App/Platforms/iOS/RecordingAudio.ios.cs`, `src/DinoReader.App/RecordingAudio.cs`, `src/DinoReader.App/Platforms/Android/RecordingAudio.android.cs`, `src/DinoReader.App/Platforms/Windows/RecordingAudio.windows.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/PlayerHandOff.cs` (new, added during the build), `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/270-dino-ios-player-disposed-in-finished-callback.md`.

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

- [x] iOS: never dispose an `AVAudioPlayer` inside its own delegate call. `FinishedPlaying` and `DecoderError` hand `ended` and `failed` to the main queue asynchronously (`DispatchQueue.MainQueue.DispatchAsync`), so the next clip starts after Apple's call returns. The shared `PlayerHandOff` (a generation counter, bumped by every `Play` and `Stop`) queues the callbacks through the platform's `PlatformPost`, frees the player (`PlatformStop`) on that later turn and drops a callback whose play was stopped or replaced meanwhile; a `finishing` flag, set in the native call, makes `Pause`/`Resume` no-ops until then. Check the dispatch API with `library-docs` (Microsoft.iOS bindings) first.
- [x] Android: `RecordingAudio.android.cs` had the same pattern (releasing a `MediaPlayer` from its own `Completion` handler); same hand-off, posted to the main looper. Android and iOS behave the same afterwards.
- [x] Windows head: `RecordingAudio.windows.cs` uses the same hand-off, posted to the MAUI UI dispatcher (never a pool thread, so the check, the dispose and a new `Play` cannot interleave).
- [x] Shared: `RecordingPlayer`'s contract says `ended` and `failed` always run after the platform callback has returned; a unit test with a fake platform player that fails if it is disposed while its callback runs (play clip 1, end it, the session plays clip 2 from inside the end). The `RecordingPlayer` wiring itself is platform code and not unit-tested.
- [x] A Debug and release log line when a clip ends and the next one starts (`[AudioPack] clip N ended, next`), so the next crash report shows how far a book got.
- [ ] Prove it: an iOS run plays a book with pre-made audio for at least 10 clips in a row on the current build (expect the crash), then on the fix (no crash). Where no iOS simulator or device run exists for the wave, the TestFlight build goes to the user's iPhone and the crash store is read the next morning (`pull-crashes.mjs --app dino --kind managed`); the slice is `built`, not `verified`, until that read shows no new `FinishedPlaying` crash.
- [x] `src/DinoReader.App/CLAUDE.md`: one rule line: a native player's "finished" handler never starts, stops or disposes a player directly; it hands off to the next main-loop turn.

## Verification

- `dotnet test tests/DinoReader.Tests` (the hand-off test); the iOS build compiles in CI; Android `premade-audio-plays` and `shelf-audio-on-start` still pass on the emulator.
- The user's iPhone on the fix build: a book with pre-made audio reads to the end with no crash, and `pull-crashes.mjs` shows no new `ObjectDisposedException` in `FinishedPlaying` for that version.

## Hand-off design (after two review rounds)

The first design (a lock inside `PlayerHandOff`) was replaced: every platform now delivers the native ended/failed call to the UI thread before it reaches `PlayerHandOff` (Windows marshals `MediaEnded`/`MediaFailed` to the dispatcher at the source; no inline fallback, a refused dispatch is logged and dropped), so `PlayerHandOff` is single-threaded with no lock. Design file: `D:/ReadTheStupidText-worktrees/reviews/s270-handoff-design.md`. `ReadingSession.ApplyRecordingEnded` also holds while the session is paused (Resume goes on); the pre-made and animal-clip paths already hold through `AdvanceStep`.

Changed while building: the hand-off lives in a new `src/DinoReader.App/PlayerHandOff.cs` (`RecordingPlayer` wraps ended/failed; release, then the callback, on the next main-loop turn; a generation counter drops stale callbacks; `Pause`/`Resume` are ignored while finishing). The first design (a lock) failed two review rounds and was replaced by the single-threaded hand-off above. `ReadingSession` keeps `_recordingEndedWhilePaused` (a recording that ends while paused waits for Resume; `StopRecording` clears it).

From review (files in `D:/ReadTheStupidText-worktrees/reviews/`, all read to the last finding):
- `s270-review-high.md` (9 findings): applied 1, 2, 3, 4, 8, 9. Skipped 5 (Android: new `Handler` per clip end, cache a static one), 6 (`AudioPackLog.Write` on the per-clip path), 7 (counter arithmetic in `RecordingPlayer.Play`/`Defer`; a play token would be simpler).
- `s270-review-medium.md` (9 findings): applied 1, 2, 3, 4, 5, 9 (written against the lock design, which was later replaced). Skipped 6 (tests exercise `PlayerHandOff` directly, not the `RecordingPlayer` wiring), 7 (Android `Handler` per call), 8 (note only: `ReferenceEquals` pre-filter on iOS).
- `s270-review-check1.md` (6 findings): 1, 2, 3 handled by the redesign (no lock, Windows marshalled at source); 4 (Pause tapped between native end and the queued turn) handled by the `ReadingSession` pause hold; skipped 5 (Android `Handler` per call), 6 (`AudioPackLog.Write` on the hot path).
- `s270-review-check2.md` (8 findings): applied 3 (clear `_recordingEndedWhilePaused` in Stop/StartAt). Skipped 1 (Windows: `IsFinishing` is set after the worker-to-UI dispatch, so Pause then Resume in that window can restart the ended clip; origin/main had the same window), 2 (`IsFinishing` stays true after Run until the next Begin), 4 (`_recorded` stays Playing if `ApplyRecordingEnded` returns early on a stale id), 5 (Android `Handler`), 6 (iOS audio session Deactivate relies on Stop's `PlatformStop` when Run is stale), 7 (`AudioPackLog.Write` I/O), 8 (no unit test for the `ReadingSession` paused-then-ended/Resume branch).
- `s270-review-check3.md` (4 findings, all low): none applied (Windows MediaEnded dropped with no dispatcher at shutdown; Android `Handler`; `AudioPackLog.Write` on the UI thread; no `ReadingSession` paused/Resume test). It found no correctness defects in the final diff.

Device: Android emulator `premade-audio-plays` and `shelf-audio-on-start` PASS (92 s); logcat shows `[AudioPack] clip 0 ended, next` then `premade clip plays (sentence 1)` 81 ms later, no ObjectDisposed, no crash. Windows VM `dino-launch` PASS.

#### For CLAUDE.md (lifted 2026-11-05)

Already added in `src/DinoReader.App/CLAUDE.md` (a native player's "finished" handler never starts, stops or disposes a player directly; it hands off to the next main-loop turn). (Slice 270: docs/plan/slices/270-dino-ios-player-disposed-in-finished-callback.md)

#### Not yet verified

- iOS: the TestFlight build on the user's iPhone, then `node tools/crashes/pull-crashes.mjs --app dino --kind managed` shows no new `FinishedPlaying` ObjectDisposedException.
- Three or more pre-made clips in a row on a device (the emulator flow ends after clip 1).
- Windows clip hand-off: no VM check plays audio clips (a harness gap).
- `IDispatcher.Dispatch` always queues on Windows (seen only as a string match in Microsoft.Maui.dll 10.0.120, not confirmed from source).
- A parent's recording that ends while paused waits for Resume (`_recordingEndedWhilePaused` in `ReadingSession.cs`): no unit test and no device flow; the skip reason "covered by device runs" was wrong, no run covers it (feedback loop 2026-11-05-1823; test work in Slice 271).
- Review check2 #4 (a stale id's early return in `ApplyRecordingEnded` may leave the recording Playing with nothing playing) and #6 (iOS audio-session Deactivate only in the queued step on a stale play): both unconfirmed.
- Review check2 #1 kept because "origin/main had the same window" (Windows Pause then Resume in the hop replays the clip): that premise was not checked.
