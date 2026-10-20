# Slice 46 — Play button loading state

*Built (wave 2026-10-20c), PR #185.*

**Batch:** 9
**Decisions:** 66 (last bullet)
**Depends on:** none
**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.App/ControlPanelWindow.xaml`, `src/ReadTheStupidText.App/ControlPanelWindow.xaml.cs`, `src/ReadTheStupidText.Application/Reading/ReadAloudService.cs`
**Story issue:** —

## Goal

After tapping Play nothing visibly happens until the first chunk is
synthesized. Show a loading animation on the play button from the tap until the
first `Playing` transition, so the app never looks frozen. Same on the Windows
control-panel play/pause circle.

## Steps

- [x] **Android.** `ReaderPage.xaml` has the play circle with `PlayPauseGlyph`
      (a `Label`, `ReaderPage.xaml` line 142, tap -> `OnPlayPauseTapped`). Add a white
      `ActivityIndicator` overlaid on it, hidden by default.
- [x] `ReaderPage.xaml.cs`: in `OnPlayPauseTapped` (lines 136-160) show the
      indicator (hide the glyph) right before `await _reader.SpeakAsync(text)`
      (line 159), only on the fresh-read path, not on Pause/Resume.
- [x] Clear it in `OnReaderStateChanged` (lines 179-181) on the first
      `PlaybackState.Playing`, and on `Idle` (stop, error, a read with no
      audio). Wrap `SpeakAsync` in try/catch so an exception clears it too and
      shows an alert.
- [x] `OnDisappearing` (lines 65-71): clear it so a stale spinner never survives
      leaving the page.
- [x] **Windows.** The signal already exists: `ReadAloudService.ReadEntryAsync`
      (`Application/Reading/ReadAloudService.cs`, ~lines 548-560) sets
      `ActivityState.GeneratingAudio`, and the first `Playing` flips it to
      `Reading` in `OnReaderStateChanged` (line 574). Expose it as
      `IsGeneratingAudio` + a `GeneratingChanged` event, raised at those two
      points and on every exit: `Supersede` (line 539), the catch in
      `ReadEntryAsync`, natural completion.
- [x] `ControlPanelWindow.xaml`: the play circle is `PlayPauseButton` (40x40,
      lines ~398-410) holding `PlayPauseIcon`. Overlay a small `ProgressRing`.
      `ControlPanelWindow.xaml.cs`: subscribe next to `StateChanged` (line 111),
      update through `_dispatcher.TryEnqueue`, hide the icon while active.
- [x] Auto-read, clipboard and hotkey reads all pass through `ReadEntryAsync`,
      so the ring shows for them too; check it does not flash for ignored
      (debounced) selections, which never reach `GeneratingAudio`.

## Acceptance

- Android: Play on a long text -> spinner on the button until sound starts,
  then the pause glyph; Stop or an error mid-wait clears it.
- Windows: with the panel open, an auto-read selection shows the ring during
  synthesis and clears it at first audio; a failed read clears it.
- No spinner on Pause/Resume.

## Risks / notes

- Keep one source of truth on Windows (the service flag), not a second flag in
  the window.
- Slice 47 shortens the wait but cache misses and first runs still need this.

## Built

*Changed while building:*

- `ReadAloudService` exposes `IsGeneratingAudio` + `GeneratingChanged`. The event is deduped under a lock but ordering is not guaranteed, so handlers re-read `IsGeneratingAudio`.
- After `SpeakAsync` returns, a `FirstAudioGrace` (8 s, settable for tests) waits for the first `Playing` before a never-played read is marked Failed/Error. The WinRT fallback reader returns before playback starts.
- `PlayPauseOrReadAsync` is a no-op while generating.
- Windows: `ControlPanelWindow` overlays a 20px `ProgressRing` on the play circle (glyph hidden).
- Android `ReaderPage`: per-read token (`_readSequence`/`_pendingReadId`). The spinner runs from tap to first `Playing`. It clears on `SpeakAsync` return/throw (after an 8 s grace for the system TTS, which returns early), on `Idle` after return, or on grace expiry. Superseded reads never clear the current spinner. `OnAppearing` restores it. A same-text tap while pending is a no-op. Errors show a neutral alert, "Couldn't read that text / Something went wrong while reading."
- Tests: `ReadAloudServiceGeneratingTests` (never-played -> Failed after grace; early return then play within grace -> Reading, never Failed; deselect during generating raises false; `SpeakAsync` throwing clears generating; Play no-op while generating). 180 tests pass.

*From review:*

- High review: stuck spinners on Android (no reader-level generating signal), entry stuck in `GeneratingAudio` when `SpeakAsync` returns without playing, publish race, Play restarting synthesis, `OnAppearing` not restoring, dead `Task.Yield`, misleading alert, one-liners.
- A reader-level `IsGenerating` on `ISpeechReader` was deliberately not added: it would have broken Slice 48's iOS reader in the same wave. Follow-up.
- Medium review: failing immediately would have failed good reads on the WinRT fallback reader (fixed with the grace window), event ordering, Android system-TTS early return.

*Verified:* on the RTST_Test emulator (arm64 embedded build) with a neural voice the play button shows the spinner during first-chunk synthesis and switches to pause when audio starts; with the Phone voice it goes straight to pause.

#### For CLAUDE.md (lifted 2026-10-20)

- Play loading state: `ReadAloudService.IsGeneratingAudio` is true from `GeneratingAudio` until the first `Playing`; `GeneratingChanged` is deduped but not ordered, so handlers re-read `IsGeneratingAudio`. After `SpeakAsync` returns, a read gets `FirstAudioGrace` (8 s) to reach `Playing` before it is marked Failed, because the WinRT fallback reader returns before playback starts. Any new `ISpeechReader` must either await the whole read or raise `Playing` within that grace. Play does nothing while generating. The control panel overlays a `ProgressRing` on the play circle; Mobile `ReaderPage` tracks a per-read token and overlays an `ActivityIndicator` from tap to first `Playing`. (Slice 46: docs/plan/slices/46-play-loading-state.md)

#### For README.md (lifted 2026-10-20)

None.

#### Not yet verified

- The Windows tray panel's ring in the running app (auto-read, copy, hotkey; clears on supersede/failure/completion).
- Follow-up: reader-level generating signal on `ISpeechReader` for Android.
