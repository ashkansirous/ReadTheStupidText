### Slice 313 — Dino: a shelf book's text and its sound always match

*Built 2026-11-07, PR pending.*

**Touches:** `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ShelfAudioPacks.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StagedBookUpdates.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/Testing/**`, `src/DinoReader.Core/BookUpdatePlan.cs`, `src/DinoReader.Core/AudioPackDue.cs`, `src/DinoReader.Core/**`, `worker/events.ts` (only if a new event name must be allowed), `.maestro/dino/**`, `tests/DinoReader.Tests/**`, `docs/plan/slices/313-dino-book-text-and-sound-in-step.md`

## Why

User, 2026-11-07, on Dino 1.112.0 (Android): "I tried reading freddy's book, but the text is still the old one and does not have any sounds. I reported it in the morning so I was expecting it to be fixed by now."

Slice 285 fixed only the Farsi half of the morning report (language change never fetched a pack). The English `clip_missing` was left as "no code explanation yet"; that was a mistake.

What the three sources showed (2026-11-07):

- **Store:** Freddy (af402c9974294fcb91a2c8a985c17558) is version 6 since 2026-11-06 21:55Z, packs en v2, fa v3 (made for the v6 text).
- **The app's events (dino_usage):** the user's phone (Android arm64, GB) opened Freddy on 1.110.0 at 07:05Z and on 1.112.0 at 11:58Z and 14:13Z, en and fa; every read logged `premade_audio:clip_missing` from the first piece. No `store_download` or `audio_pack_download` in those sessions. The app logs no book-update check at all, so "never checked" and "checked, nothing" can't be told apart.
- **Emulator, released 1.112.0, fresh install:** Freddy arrives at v6 text with the en v2 pack, 117 of 117 pieces have a clip, premade clips play. On relaunch no book-update line is logged.
- **Code (origin/main):**
  - `BookUpdater` runs only on a cold start (`App.xaml.cs` ~112-125), at most once every 7 days (`BookUpdateSchedule.Interval`, `BookUpdatePlan.cs`), and only stages the new text; the swap waits for the next cold start (`GalleryPage.OnAppearing`, `ApplyOnceAsync`).
  - Packs are fetched independently of the text version: `AudioPackDue.For`, `StoreClient.WantedPacks` and `ShelfAudioPacks.Resume` (every cold start) take whatever pack the store lists now.
  - `PremadeAudio.ClipAt(index, text)` returns null when the manifest's text hash differs from the shown text, so a v6 pack next to v5 text misses on every changed piece.

**Cause (likely, matches all three sources; not observable on the phone because no update event exists):** the phone has v5 text (book update throttled to weekly, plus a second cold start to apply) next to the v6-era packs fetched at launch, so the pieces' hashes don't match.

## Work

- [x] **A. A newer store version reaches the shelf at once.**
  - The store listing the app already fetches at launch / return (for packs) is compared with each shelf book's installed version on every such fetch, not weekly. A shelf book with a higher store version (and not `Edited` by the user) is updated in the same pass.
  - The staged text is applied right away when that book isn't open; when it is open, at the next time the shelf appears (not the next cold start).
  - Keep the existing skip rules (user-edited books, own/shared books, `NotInStore`), and keep the Wi-Fi/mobile-data rule the downloads already follow.
  - Pure decision code in `DinoReader.Core` with unit tests.
- [x] **B. Sound is never installed ahead of its text.**
  - A pack whose store entry belongs to a newer book version than the installed text is not used against the old text: fetch the text first (A), then the pack. If the text can't be fetched, keep the old pack (don't replace a working pack with one that misses).
  - If a pack for the current text is already installed and a mismatched one arrives, the reading page uses the one whose manifest hashes match the shown text.
  - Unit tests for the decision.
- [x] **C. The logs can tell what happened.**
  - Send a usage event for a book-update check per book that is behind: book id, from-version, to-version, result (`updated`, `staged`, `skipped_edited`, `failed`), using the existing event and fallback paths (no new personal data).
  - A pack/text mismatch found at read time is reported with its own reason (`text_mismatch`), not lumped into `clip_missing`.
- [x] **D. A test that would have caught it.**
  - A Debug-only hook to put a store book on the shelf at an older version with the newer pack (e.g. via `StageNowAsync` or a seed hook in `Testing/`).
  - An emulator flow in `.maestro/dino/`: shelf book at old version, relaunch, open, Play: the new text shows, `premade clip plays`, never "no sound".

Android, iOS and Windows share this code; all three get it.

## Verification

- Fast checks: `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`, Android build, `FallbackGuard`.
- Emulator (after both reviews): the new flow, plus `premade-audio-plays` and the two Slice 285 language-change flows.

## Built / review notes

- Give-up simplified after the second review round (313-medium 3, 4, 7): a book is given up for a store version only when the store has no usable package for it (404 / missing package only; a corrupt staged package is deleted and downloaded again, never given up); that releases its pack gate and resets when the store version changes (`BookTextGiveUp.Note`, stored in `dino.book.textgaveup.<id>`). Every other failure (network, baseline fetch, storage, unexpected exception at apply) is retryable: the staged entry stays, nothing is counted, the next store-listing pass retries, and the old pack stays in use. The 3-strike counter is gone.
- Also from 313-medium: an edited book records its seen version when staging skips it (1); the Wi-Fi wait runs only when the plan needs a download (2); a swap plans only the languages that have audio or a live run (5); staged-update refreshes on the gallery run one at a time (6).

*Changed while building:* work item B's second bullet (the reading page picks the pack whose hashes match the shown text) and the two-packs idea are not built: the store's pack entry carries no book version. Instead a pack is never installed ahead of its text (the text sync runs before the pack plan).

*From review:* high (`reviews/313-high.md`): findings 1-10 applied.
- Skipped #11 (`reviews/313-high.md`): "the reading page uses the one whose manifest hashes match the shown text" (the two-packs bullet of B): not built because the store's pack entry carries no book version; a pack is never installed ahead of its text instead.

*From review:* medium (`reviews/313-medium.md`): 1, 2, 5, 6, 8 applied.
- Skipped #3 (`reviews/313-medium.md`): "A failed baseline fetch (decisions null) counts toward the 3-strike give-up": replaced by the simplified give-up above (the counter is gone, a failed baseline is retried at the next listing pass).
- Skipped #4 (`reviews/313-medium.md`): "records a permanent \"store has no package\" failure whenever StageOneAsync returned true": replaced by the simplified give-up (only a 404 / not-in-store gives up for that store version).
- Skipped #7 (`reviews/313-medium.md`): "records a permanent failure and deletes the staged entry; transient faults": replaced by the simplified give-up (an unexpected exception is retryable, the staged entry stays).

*From review:* check pass (`reviews/313-check.md`): 4, 6, 9 applied.
- #4: the staged-update refresh handler catches every exception and reports a fallback (`GalleryPage.OnBookUpdateStaged`), coalescing kept.
- #6: `StagedBookUpdates` remembers a swap nobody heard (`TakeUnheardSwap`); `ShelfAudioPacks.Resume` plans the packs again for it, past the throttle.
- #9: `BookUpdater.StageOneAsync` (weekly check) gives up on a 404 like the listing pass.
- Skipped 1, 2, 3, 5, 7, 8 (low or by design: retries are throttled to listing passes; a 404 give-up resets on the next store version).

*From review:* check pass 2 (`reviews/313-check2.md`): 1-4 applied.
- #1 and #2: the `_swapUnheard` / `TakeUnheardSwap` flag of check #6 is removed (it raced and skipped `TextChanged`). `DinoApp`'s constructor resolves `ShelfAudioPacks` first, so its `StagedBookUpdates.Swapped` subscription exists before any apply; every swap reaches `TextChanged()` (live run: PlanAgain; otherwise a quiet run past the throttle).
- #3: the weekly check's 404 give-up (`BookUpdater.StageOneAsync`) also reports `book_update:failed` and an `UpdateCheckFailed` fallback.
- #4: `GalleryPage.OnBookUpdateStaged` lets `OperationCanceledException` pass silently (no pass after it); any other failure is reported per pass, so a queued `_stagedRefreshAgain` pass still runs.

*Fast checks:* `DinoReader.Tests` 3860 passed (1 skipped), Dino Android build, `FallbackGuard` 47/47, worker events tests 79/79.

*Emulator-5554, 2026-11-07:* `book-text-and-sound-in-step` PASS (`book_update staged:13:14`, `book_update updated:13:14`, `78 of 78 pieces have a clip`, `premade clip plays`). The first run failed on a flow bug (expect-log markers ending in a colon), fixed. `premade-audio-plays`, `language-change-fetches-audio`, `language-change-back-key-fetches-audio` and `silent-piece-beat` PASS. The flow can't show changed text on screen: the test store serves the same text.

**Not yet verified:**
- The user's phone: Freddy in English and Farsi after updating to the release with this slice.
- iPhone: same path.
- Windows head: same path.
- Worker deploy needed before `book_update` events are stored.

#### For CLAUDE.md

- A book's sound pack is never installed ahead of its text: the store listing pass stages and applies newer book text, and `book_update` events record each result. Look in `src/DinoReader.App/CLAUDE.md`. (Slice 313: docs/plan/slices/313-dino-book-text-and-sound-in-step.md)
