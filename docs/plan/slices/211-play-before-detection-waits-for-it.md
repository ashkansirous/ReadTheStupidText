### Slice 211 — Play right after typing reads like a later Play would

*Planned 2026-10-30.* From the S21 night runs (Slice 139 race check, docs/plan/notes/2026-10-29-s21-night-runs.md).

**Touches:** `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReviewDecisions.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Wait.cs` (only if the wait overlay needs a new stage), `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx` (only if a new wait line is needed), `tests/ReadTheStupidText.Tests/**`, `.maestro/rtst/**`, `docs/plan/slices/211-play-before-detection-waits-for-it.md`

#### Why

On the S21, with Translate on, a Play tap about 0.4 s after typing (before language detection's idle delay ran) read the text **untranslated** (1456 ms). The code (2026-10-30, ground-in-code):

- `OnReadTapped` (`ReviewPage.xaml.cs` ~408-426) translates only when `ReviewDecisions.TranslatesOnRead(_optionShown, _ticked)` (`ReviewDecisions.cs:17`) is true, and `_optionShown` needs a detection result (`ReviewDecisions.cs:13`). Detection runs only after the idle delay (`DetectAfterDelayAsync`, ~268-276) and Play never waits for it, so an early tap falls through to the plain read.
- The voice for that plain read then comes from the script alone (`VoiceRouting.ReadingLanguage`, `VoiceRouting.cs:54-63`). Likely result: foreign text in the app's own script (Spanish on an English phone) is read by the app language's voice.
- No slice decided this; Slice 139 only records "a tap before detection starts no early work".

So the same text and the same settings give a different read depending on how fast you tap. That is a bug, not a design.

#### Decision (2026-10-30)

When Play is tapped and detection has not run for the current text, **run detection at once** (cancel the idle delay, keep the same detector), then decide exactly as if it had finished: offer/translate per the existing rules and pick the voice from the detected language. Detection is normally well under 500 ms, so usually no wait shows. If detection plus the start goes over 500 ms, the existing Slice 138 wait overlay shows ("Getting ready", with Cancel), never a silent wait. A detection failure falls back to today's plain read, as now. iOS and Android share this page, so both get it.

#### Work

- [ ] `ReviewDecisions`: a pure step "detection pending at Play -> detect now" (unit-tested: pending, done, failed, text changed during detection).
- [ ] `ReviewPage.OnReadTapped`: await an immediate detection when none exists for the current text, under the read's cancellation; then the existing paths.
- [ ] Wait overlay covers the case when it passes 500 ms (reuse a stage; new string only if none fits).
- [ ] Debug marker `READ detect-now <ms>`.
- [ ] Maestro: type foreign text and Play at once with Translate on; assert the translated read (marker) on the emulator.

#### Verification

- Fast: the `ReviewDecisions` tests.
- Emulator: the Maestro flow.
- S21: rerun the Slice 139 race (tap ~0.4 s after typing): translated read, start time recorded against the 1456 ms baseline.
- iOS: compile in CI; TestFlight hand check.
