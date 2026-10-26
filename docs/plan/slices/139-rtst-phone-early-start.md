# Slice 139 — RTST phone: start translating and voicing before the tap

*Planned.*

**Batch:** 29 (Decisions 143-148)
**Touches:** `src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs`, `src/ReadTheStupidText.Application/Translation/**`, `src/ReadTheStupidText.Application/Reading/EarlyStart*` (new), `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile.Shared/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MlKitTranslator.cs`, `src/ReadTheStupidText.Mobile.Shared/MauiPackUpdateNetwork.cs`, `tests/ReadTheStupidText.Tests/**`, `docs/plan/decisions.md`

## Why

Most of the wait after the tap is the first translation and the first audio. The text is on screen
before the user taps Read or Translate and read (ReaderPage and ReviewPage), so that idle time can
do the first part of both choices.

## What exists

- The head start (Decision 66, Slice 47) already voices the first sentences of the original into a
  sentence-level audio cache, after a file or scan lands and after 1 s idle while typing.
- Cancellation is already plumbed through `ReadTranslator`, `MlKitTranslator` and `CancelPrepare`.
- `SpeechTextChunker.HeadPolicy.Default` says "one sentence, at most 120 chars", while Decision 66
  says two sentences. This slice settles it at two (below) and fixes whichever is out of date.

## Decisions (locked with the user, 2026-10-26)

- **Decision 147:**
  - Original: voice the first 2 sentences; with no sentence ending, the first ~120 characters
    (about 20 words) cut at a word boundary.
  - Translation, only when the detected source language differs from the user's language: translate
    the first 3 sentences (~300 characters, for context), keep and voice the first 2 translated sentences.
  - The tap decides: the other side's work is cancelled at once, and the chosen side's cached audio
    and translation are used.
  - If the ML Kit model for the pair is not installed, start its download early only on an unmetered
    network; on mobile data the download waits for the tap (and the Slice 138 indicator shows it).
  - New text (edit, paste, scan) cancels all early work and restarts it after the existing 1 s idle.

## Work

- [ ] Pure `EarlyStartPlan` in Application: head sentences for the original, the translation span
      and the voiced part of it, the no-punctuation fallback; unit-tested for Latin, CJK and Farsi
      punctuation.
- [ ] Fix `HeadPolicy.Default` / Decision 66 to two sentences.
- [ ] A small coordinator on the phone that runs both early jobs, cancels the loser on the tap,
      hands over the translated head to the read, and restarts on text change.
- [ ] The translated head must be reused by the full translation (no translating it twice) and its
      audio must hit the existing sentence cache.
- [ ] Usage (Slice 137): `timing` gains `early` (bool) so the report shows the gain.

## Verification

- Unit tests of the plan and the cancel-on-choice coordinator.
- Emulator: paste French text with an English UI, wait 2 s, tap Translate and read: first audio
  within the cached time, logcat shows no second translation of the head; tap Read instead: the
  translation job is cancelled; mobile data: no model download before the tap.
