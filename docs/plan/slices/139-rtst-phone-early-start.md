# Slice 139 — RTST phone: start translating and voicing before the tap

*Built 2026-10-26 (wave PR #398), not yet verified on the emulator or iOS.*

**Batch:** 29 (Decisions 143-148)
**Touches:** `src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs`, `src/ReadTheStupidText.Application/Translation/**`, `src/ReadTheStupidText.Application/Reading/EarlyStart*` (new), `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile.Shared/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MlKitTranslator.cs`, `src/ReadTheStupidText.Mobile.Shared/MauiPackUpdateNetwork.cs`, `tests/ReadTheStupidText.Tests/**`, `docs/plan/decisions.md`, `src/ReadTheStupidText.Application/Usage/**`, `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `worker/events.ts`, `worker/events.test.mjs`

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

#### Design (RTST Batch 29 mock, added 2026-10-26)

No screens. The Batch 29 mock intro says the early start "has no screen of its own: it just makes these waits shorter or skips them". Nothing to build from the mock.

## Work

- [x] Pure `EarlyStartPlan` in Application: head sentences for the original, the translation span
      and the voiced part of it, the no-punctuation fallback; unit-tested for Latin, CJK and Farsi
      punctuation.
- [x] Fix `HeadPolicy.Default` / Decision 66 to two sentences.
- [x] A small coordinator on the phone that runs both early jobs, cancels the loser on the tap,
      hands over the translated head to the read, and restarts on text change.
- [x] The translated head must be reused by the full translation (no translating it twice) and its
      audio must hit the existing sentence cache.
- [x] Usage (Slice 137): `timing` gains `early` (bool) so the report shows the gain.

## Verification

- Unit tests of the plan and the cancel-on-choice coordinator.
- Emulator: paste French text with an English UI, wait 2 s, tap Translate and read: first audio
  within the cached time, logcat shows no second translation of the head; tap Read instead: the
  translation job is cancelled; mobile data: no model download before the tap.

## Known gap

- Text with no sentence ending is voiced early as its first ~120 characters, but the Android reader's `HeadPolicy.Cached` peels about 200 characters for the real read, so that one case misses the sentence cache (punctuated text matches). `HeadPolicy.Cached` is left alone: its file overlaps Slice 149's readers.

## Review notes (medium pass)

- `HeadPolicy.Default` is now two sentences for every reader, so the Windows and Dino readers (no sentence cache) also get a first chunk of up to two short sentences. Decision 147 intends the shared head; time-to-first-audio there was not measured.
- Not done: a cache of early work keyed by the head text, so an edit past the head would keep the finished translation and voicing instead of restarting. Later improvement.

## Changed while building

- `EarlyStartPlan`, `EarlyStartCoordinator` and `EarlyStartVoice` live in `Application/Reading`; the phone side is `ReaderPage.EarlyStart.cs`.
- ReviewPage translates early too: after 600 + 400 ms idle when the user types, never for paste, file or scan. The call is tracked in `_earlyCall`, and the full translation reuses the translated head.
- `HeadPolicy.Default` is now 2 sentences / 120 chars (Decision 147); the Windows and Dino readers use it too.
- Usage `early` field (blob16) added in `worker/`, the report and `STORE.md`.
- `UsageService.FlushAsync` now waits behind a running send, which fixed the flaky batches test.
- The early-voice handler survives ReviewPage on top and is cleared only on retire.
- `SettleChosenAsync` waits for the chosen side (10 s cap) behind an overlay with Cancel; Back and Cancel hand nothing over, and the editor is read-only meanwhile.
- `HeadStartRun` (Application): a head start completes normally only when clips are stored. The Windows, Android and iOS readers use it.
- First-chunk hint: the early voice's exact head becomes the read's first chunk, through an optional `firstChunkHint` on `SpeechTextChunker.Split` / `SpeechChunkPlanner.ForText`, owned per reader by the Android and iOS `FirstChunkHints`. Windows and Dino are unchanged (golden test).
- Merged origin/main with Slice 149 cleanly.

## From review

- 139-high: 10 findings applied.
- 139-medium: 8 findings applied; 5 and 8 skipped (see the medium-pass notes above).
- Late review 139-late: 10 findings applied.
- 139-redesign: 11 findings applied.

#### For CLAUDE.md (lifted 2026-10-26)

In `src/ReadTheStupidText.Mobile/CLAUDE.md`:

- Early start (`ReaderPage.EarlyStart.cs`, `ReviewPage.xaml.cs`) records no usage of its own and shows no overlay. It may only cancel preparations it started itself. (Slice 139: docs/plan/slices/139-rtst-phone-early-start.md)
- A head start completes normally only when its clips are stored (`HeadStartRun`); the read's first chunk follows the early head through `FirstChunkHints`, never global state. (Slice 139: docs/plan/slices/139-rtst-phone-early-start.md)

#### Not yet verified

- Emulator on 5c90243/6a58a43 (2026-10-26, heavily swapping AVD): early work finished -> first audio 396-749 ms with a cache hit on sentence 1 (fac0a49: 254 / 185 ms). In-flight races are NOT reliably reused yet: a Read tapped ~0.3 s after unticking Translate (restart), or a Translate-and-read before the early voice is stored, still reads cold (12-24 s on that emulator) — no worse than without early start, but no gain. Re-test on a real phone; if it reproduces, follow up in the read path (OnReadTapped / EarlyStartCoordinator settle after a restart).
- Known limit: a tap before language detection + 0.4 s idle starts no early work; a tap during a cold engine load waits up to the 10 s settle cap.
- Unpunctuated text misses the sentence cache (`HeadPolicy.Cached` peels about 200 characters, early voices about 120).
- iOS.
