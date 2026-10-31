### Slice 214 — RTST phone: the first read after a cold start takes 7 s

*Built 2026-10-31, merged as PR #647 (9024a2e); iOS timing and a truly cold run not yet verified.* Planned 2026-10-30. From the S21 night runs and the 2026-10-30 leftovers run (docs/plan/notes/2026-10-29-s21-night-runs.md).

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader*.cs`, `src/ReadTheStupidText.Mobile.Shared/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs` (only if the first piece is split), `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs` (only if a warm-up is started there), `src/ReadTheStupidText.Mobile/App.xaml.cs` (only if a warm-up is started at launch), `tests/ReadTheStupidText.Tests/**`, `tools/phone/**` (cold-start measurement), `docs/plan/slices/214-rtst-phone-cold-first-audio.md`

#### Why (measured)

- S21, Android 15, **AOT** Debug build (closest to Release), Supertonic English, fresh install: **first audio 6996 ms**: engine ready 759 ms, chunk 0 synthesis end 5870 ms. Warm: 240 ms. Night Debug JIT build: 8808 ms. Dino's Kokoro: 960 ms cold.
- Same S21, AOT, after a force-stop with the sentence cache cleared (Slice 213 `cold-start`, 2026-10-30, 5 runs): **3.1-4.2 s** first audio (engine 67-85 ms, first synthesis 2.9-4.0 s). Likely the gap to the 7.0 s fresh-install number is the phone's own file cache keeping the model files in memory after a force-stop (not proven). Measure both cases: after a fresh install or a reboot (truly cold) and after a force-stop (the common case).
- Unexplained: one plain Debug (JIT) run after a fresh install read **2.8 s** (engine 22 ms), faster than AOT. Likely the model files were still cached from the run before, which would make it a force-stop case, not a fresh install. Rerun it once with the `cold-start` check before trusting any JIT/AOT comparison.
- So nearly all the cold time is the **first synthesis**, not loading the engine. The Slice 138 wait overlay shows meanwhile, but 7 s before the first word is the app's worst moment.
- Not known yet: whether the cost is the first inference itself (ONNX runtime warm-up, any text length) or the length of the first chunk (up to 2 sentences at the head).

#### Work

1. **Find out (first):**
   - [x] Cold-start timing that is really cold every run (fresh process and cleared sentence cache; today's repeat runs hit a cache and read 240-578 ms): add it to the phone tooling (Slice 213's `cold-start` check).
   - [x] On the S21 (AOT): first synthesis time for a 1-word, 1-sentence and 2-sentence first chunk. If the time barely changes with length, it is warm-up; if it scales, it is length.
2. **Fix by the evidence (Android and iOS, same reader design):**
   - [ ] (Not built, decided 2026-10-31: the length fix alone meets the 2 s target.) Warm-up: as soon as the reader page opens (or the app starts, if cheap enough), synthesise a tiny throwaway text on a background thread at low priority, so the first real read finds a warm engine. Never block the UI; cancel if a real read starts.
   - [x] Length: make the very first piece of a read one short sentence (or the first clause), then continue with normal chunks.
   - [x] Whichever ships, a unit test for the pure part (first-piece split, warm-up cancel rules).

#### Measurement and decision (2026-10-31)

S21, AOT, force-stop cold, 3 runs each, median:

| First read | First audio | First synthesis | Audio produced |
| --- | --- | --- | --- |
| 1 word | 1009 ms | 848 ms | 1.35 s |
| 2 sentences | 2867 ms | 2615 ms | 8.05 s |

Engine load 75-286 ms. So the cost is **length**, not a fixed warm-up: about 0.5 s fixed + 0.26 s per second of audio. Decision: ship the Length item, not the warm-up. First attempt, cut at about 12 words, measured on the S21 (AOT, force-stop cold, 3 runs): first audio 2081/2011/1934 ms (median 2011 ms, target under 2 s), first synthesis about 1.7 s, but chunk 0 audio was 4110 ms each run, longer than the planned 2-3 s. So the first piece is now cut to about 8 spoken words (`FirstPieceMaxCost` 8, about 2 s of audio): at the last clause end (sentence end, comma, semicolon, colon, dash, Arabic comma, CJK marks) that leaves at least 3 words, else at a word boundary; unspaced Chinese/Japanese count 0.5 per character. Built in `SpeechTextChunker` (`HeadPolicy.WithShortFirstPiece`, `FirstPieceCut`), switched on only for text reads on the Android and iOS readers (RTST; Dino's voiced segments keep the old policy) and not on Windows. The early-head preparation and the read use the same policy through one function (`SpeechChunkPlanner.HeadUnits`), so the read starts with exactly the prepared units; the second piece is only the rest of the cut sentence, at most about 16 words. Abbreviations (Mr., Dr., e.g., initials) are not clause ends. Tests: `FirstPieceCutTests`. Result of the 8-word cut (S21, AOT, force-stop cold, 3 runs, 2026-10-31): first audio 1627/1759/2132 ms (median 1759 ms, under the 2 s target; one run was over), first synthesis median 1468 ms, chunk 0 audio 3150 ms, engine 68-132 ms.

#### Target

First audio after a cold start under **2 s** on the S21 with an AOT build: met (median 1759 ms). The "under 500 ms with warm-up" half is dropped with the warm-up.

#### Verification

- Fast: unit tests above.
- S21: the `cold-start` check, 3 truly cold runs, before and after.
- iOS: compile in CI; TestFlight timing by hand.

#### Changed while building

- The first attempt (cut at about 12 words) gave a median 2011 ms, chunk 0 audio 4110 ms; the cut went to about 8 words.
- The warm-up was not built; the Length item alone met the target.
- Text reads on Android and iOS only; Windows and Dino keep the old policy.

#### From review

- High (reviews/s214-high.md) and medium (reviews/s214-medium.md) passes done; fixes applied, including one shared `HeadUnits` for the prepared head and the read.
- Medium finding skipped (1 of 8): finding 5, a clause end at 3 words or more ends the first piece even very early ("Well, I think, that ..." cuts at 4 words), giving a tiny first piece and a long second one (`SpeechTextChunker.cs:224`). Corrected 2026-10-31 by the feedback loop: the earlier line named a finding no review contained. Medium 8 (`-Text` unreachable through `run-checks.ps1`) is still open: Slice 225 item 10. High 3 (no chunk-1-ready timing check) is not applied; the piece-2 gap item under Not yet verified covers it.

#### For CLAUDE.md (lifted 2026-10-31)

- The first piece of an RTST text read is cut short (`FirstPieceMaxCost` in `SpeechTextChunker.cs`); the early-head preparation and the read must both use `SpeechChunkPlanner.HeadUnits`, or the read restarts on different units than were prepared. Windows and Dino segments keep the old policy. (Slice 214: docs/plan/slices/214-rtst-phone-cold-first-audio.md)

#### Not yet verified

- Piece 2 timing against piece 1 at 1x and 2x after the 8-word cut: run `tools/phone/audio-timing.ps1 -App rtst -Aot` on the S21 once it is back (it prints the gap after each chunk at rates 1 and 2 and flags one over 1300 ms). Corrected 2026-10-31 by the feedback loop: an earlier line said no check existed; the device-test agent had only looked in the `run-checks.ps1` catalogue.
- A truly cold run after a fresh install or reboot.
- iOS TestFlight first-audio timing, by hand.
