# Slice 138 — RTST phone: a wait indicator after 500 ms

*Planned.*

**Batch:** 29 (Decisions 143-148)
**Touches:** `src/ReadTheStupidText.Mobile.Shared/Waiting/**` (the shared overlay, Decision 149; built here or by Slice 143, whichever goes first), `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Application/Reading/WaitStage.cs` (new), `src/ReadTheStupidText.Application/Usage/**`, `src/ReadTheStupidText.Mobile/Resources/Strings/*.resx`, `tests/ReadTheStupidText.Tests/**`

## Why

A user thought the app hung after tapping Translate. Whenever the user waits on the phone (model
download, translating, preparing the voice, or playback stalled because synthesis is behind), they
must see that it is working and on what.

## Decisions (locked with the user, 2026-10-26)

- **Decision 146:** after 500 ms of waiting, show an overlay over the reading area (not a separate
  page) with a native spinner, the stage ("Downloading French… 40%", "Translating…", "Preparing the
  voice…") and a Cancel button. Hide it the moment audio starts. Under 500 ms nothing shows (no flash).
  No Lottie for now: it adds an animation library per platform and the stage text is what tells the
  user it has not frozen. A Lottie animation can come later as polish.

## Work

- [ ] `WaitStage` enum and a pure "show after 500 ms, hide on first audio or cancel" timer model in
      Application, unit-tested.
- [ ] Wire the stages: ML Kit model download (with progress where ML Kit gives it, else
      indeterminate), translate, synthesis of the first chunk, and mid-read stalls when the next chunk
      is not ready. Cancel stops the work and returns to the text.
- [ ] Check the ReviewPage "Translate and read" path never blocks the UI thread (the grounding pass
      did not read it); fix it if it does.
- [ ] Replace the separate `BusyIndicator` / `PlayLoadingIndicator` uses that this overlay covers.
- [ ] Usage: `wait_shown` (stage, duration bucket) and `wait_cancelled` (stage), added to the
      Slice 137 closed set, the Worker `EVENT_RULES` and the privacy texts.
- [ ] Strings in every RTST locale.

## Verification

- Unit tests of the timer model.
- Emulator: first translation to a new language shows "Downloading…" then "Translating…", Cancel
  works; a short text that starts within 500 ms shows nothing; TalkBack announces the stage.
