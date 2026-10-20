# Slice 46 — Play button loading state

*Planned.*

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

- [ ] **Android.** `ReaderPage.xaml` has the play circle with `PlayPauseGlyph`
      (a `Label`, `ReaderPage.xaml` line 142, tap -> `OnPlayPauseTapped`). Add a white
      `ActivityIndicator` overlaid on it, hidden by default.
- [ ] `ReaderPage.xaml.cs`: in `OnPlayPauseTapped` (lines 136-160) show the
      indicator (hide the glyph) right before `await _reader.SpeakAsync(text)`
      (line 159), only on the fresh-read path, not on Pause/Resume.
- [ ] Clear it in `OnReaderStateChanged` (lines 179-181) on the first
      `PlaybackState.Playing`, and on `Idle` (stop, error, a read with no
      audio). Wrap `SpeakAsync` in try/catch so an exception clears it too and
      shows an alert.
- [ ] `OnDisappearing` (lines 65-71): clear it so a stale spinner never survives
      leaving the page.
- [ ] **Windows.** The signal already exists: `ReadAloudService.ReadEntryAsync`
      (`Application/Reading/ReadAloudService.cs`, ~lines 548-560) sets
      `ActivityState.GeneratingAudio`, and the first `Playing` flips it to
      `Reading` in `OnReaderStateChanged` (line 574). Expose it as
      `IsGeneratingAudio` + a `GeneratingChanged` event, raised at those two
      points and on every exit: `Supersede` (line 539), the catch in
      `ReadEntryAsync`, natural completion.
- [ ] `ControlPanelWindow.xaml`: the play circle is `PlayPauseButton` (40x40,
      lines ~398-410) holding `PlayPauseIcon`. Overlay a small `ProgressRing`.
      `ControlPanelWindow.xaml.cs`: subscribe next to `StateChanged` (line 111),
      update through `_dispatcher.TryEnqueue`, hide the icon while active.
- [ ] Auto-read, clipboard and hotkey reads all pass through `ReadEntryAsync`,
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
