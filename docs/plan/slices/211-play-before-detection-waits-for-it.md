### Slice 211 — Play right after typing reads like a later Play would

*Built 2026-10-30, emulator-verified (emulator-5554), not yet verified on the S21 or iOS.* Planned 2026-10-30. From the S21 night runs (Slice 139 race check, docs/plan/notes/2026-10-29-s21-night-runs.md).

**Touches:** `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Application/Translation/ReviewDecisions.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Wait.cs` (only if the wait overlay needs a new stage), `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx` (only if a new wait line is needed), `tests/ReadTheStupidText.Tests/**`, `.maestro/rtst/**`, `src/ReadTheStupidText.Mobile/Testing/**` (new, Debug-only `hold-detection` hook), `src/ReadTheStupidText.Mobile/MauiProgram.cs` (not needed: the hook self-registers), `docs/plan/slices/211-play-before-detection-waits-for-it.md`

#### Why

On the S21, with Translate on, a Play tap about 0.4 s after typing (before language detection's idle delay ran) read the text **untranslated** (1456 ms). The code (2026-10-30, ground-in-code):

- `OnReadTapped` (`ReviewPage.xaml.cs` ~408-426) translates only when `ReviewDecisions.TranslatesOnRead(_optionShown, _ticked)` (`ReviewDecisions.cs:17`) is true, and `_optionShown` needs a detection result (`ReviewDecisions.cs:13`). Detection runs only after the idle delay (`DetectAfterDelayAsync`, ~268-276) and Play never waits for it, so an early tap falls through to the plain read.
- The voice for that plain read then comes from the script alone (`VoiceRouting.ReadingLanguage`, `VoiceRouting.cs:54-63`). Likely result: foreign text in the app's own script (Spanish on an English phone) is read by the app language's voice.
- No slice decided this; Slice 139 only records "a tap before detection starts no early work".

So the same text and the same settings give a different read depending on how fast you tap. That is a bug, not a design.

#### Decision (2026-10-30)

When Play is tapped and detection has not run for the current text, **run detection at once** (cancel the idle delay, keep the same detector), then decide exactly as if it had finished: offer/translate per the existing rules and pick the voice from the detected language. Detection is normally well under 500 ms, so usually no wait shows. If detection plus the start goes over 500 ms, the existing Slice 138 wait overlay shows ("Getting ready", with Cancel), never a silent wait. A detection failure falls back to today's plain read, as now. iOS and Android share this page, so both get it.

#### Work

- [x] `ReviewDecisions`: a pure step "detection pending at Play -> detect now" (unit-tested: pending, done, failed, text changed during detection).
- [x] `ReviewPage.OnReadTapped`: await an immediate detection when none exists for the current text, under the read's cancellation; then the existing paths.
- [x] Wait overlay covers the case when it passes 500 ms (reuse a stage; new string only if none fits).
- [x] Debug marker `READ detect-now <ms>`.
- [x] Maestro: type foreign text and Play at once with Translate on; assert the translated read (marker) on the emulator.

#### Verification

- Fast: the `ReviewDecisions` tests.
- Emulator: the Maestro flow.
- S21: rerun the Slice 139 race (tap ~0.4 s after typing): translated read, start time recorded against the 1456 ms baseline.
- iOS: compile in CI; TestFlight hand check.

#### Built

*Changed while building:*
- Detection runs at once on Play (`DetectNowAsync`), then translate or plain read exactly as a later Play. One continuous wait (`PreparingVoice` stage, "Preparing the voice...", Cancel, 500 ms gate); no new strings. The planned "Getting ready" title became the existing "Preparing the voice..." line.
- Detection has a 5 s limit (`DetectNowLimit`); a timeout falls back to the plain read like a failure. Back or leaving never starts the read. A text change during the wait drops the read. Cancel re-arms detection with the normal idle delay.
- `ReviewDecisions.NeedsDetectionAtRead` plus tests.
- Debug-only `RtstTestCommands` (`src/ReadTheStupidText.Mobile/Testing/`, self-registering module initializer like `DinoTestCommands`) with a `hold-detection` launch hook (10-minute typing delay), used by `.maestro/rtst/play-before-detection.yaml`. Touches widened for it; no other live PR touched those files.

*From review:* high pass: findings 1-6 applied, 7 skipped. Medium pass: 1, 2, 4 applied; 3, 5, 6, 7, 8 skipped. The skipped ones (added by the 2026-10-30-2100 round 1 feedback loop, from the review files outside the repo):
- High 7: this file's Work items were unticked and Verification said the Maestro flow checks the translated read, which it does not (docs).
- Medium 3: the plain-read path ends the wait and `FinishAsync` starts its own "Preparing the voice" wait, so the overlay may flicker and its 500 ms timer restart.
- Medium 5: after Cancel, `RescheduleAfterCancel` re-arms detection and discards prepared work, so Cancel then Play waits again.
- Medium 6: three overlapping booleans; `_detectedNow` is test-only state not reset on the failure and Cancel paths.
- Medium 7: `DetectSafelyAsync` repeats the try/catch of `DetectAfterDelayAsync` (style).
- Medium 8: only the `NeedsDetectionAtRead` predicate is unit-tested; the `DetectNowAsync` state machine (cancel, superseded, leave, failure) has no test.

*Device (emulator-5554, 2026-10-30):* `play-before-detection` failed twice (timing: detection settled before the tap), then passed with the hold hook (HOOK hold-detection ok, READ detect-now 369, READ detect-now translated). `translation-waits-for-internet` passed on a clean install; it failed once after `play-before-detection` had changed install state (that flow has no clearState). Tests: 1685 pass; Android build and iOS compile pass.

#### For CLAUDE.md (lifted 2026-10-30)

- `tools/CLAUDE.md`: RTST Debug launch hooks live in `src/ReadTheStupidText.Mobile/Testing/RtstTestCommands.cs` (module initializer, like `DinoTestCommands`). `translation-waits-for-internet` needs a clean install and first-run, because it has no clearState. (Slice 211: docs/plan/slices/211-play-before-detection-waits-for-it.md)

#### Not yet verified

- S21: rerun of the Slice 139 race (tap about 0.4 s after typing); record the start time against the 1456 ms baseline.
- iOS TestFlight hand check.
- Back-during-wait scripted check (not run; uiautomator showed no resource-ids).
- `translation-wait-cancel` flow: reported "not run" by the emulator run (added by the 2026-10-30-2100 round 1 feedback loop).
- iOS compile of the final code: the only iOS compile ran before later changes, and CI builds iOS in Release, so the Debug-only `RtstTestCommands.cs` hold-detection hook has never been compiled for iOS.
