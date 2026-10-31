### Slice 214 — RTST phone: the first read after a cold start takes 7 s

*In progress (wave 2026-10-31), draft PR.* Planned 2026-10-30. From the S21 night runs and the 2026-10-30 leftovers run (docs/plan/notes/2026-10-29-s21-night-runs.md).

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader*.cs`, `src/ReadTheStupidText.Mobile.Shared/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs` (only if the first piece is split), `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs` (only if a warm-up is started there), `src/ReadTheStupidText.Mobile/App.xaml.cs` (only if a warm-up is started at launch), `tests/ReadTheStupidText.Tests/**`, `tools/phone/**` (cold-start measurement), `docs/plan/slices/214-rtst-phone-cold-first-audio.md`

#### Why (measured)

- S21, Android 15, **AOT** Debug build (closest to Release), Supertonic English, fresh install: **first audio 6996 ms**: engine ready 759 ms, chunk 0 synthesis end 5870 ms. Warm: 240 ms. Night Debug JIT build: 8808 ms. Dino's Kokoro: 960 ms cold.
- Same S21, AOT, after a force-stop with the sentence cache cleared (Slice 213 `cold-start`, 2026-10-30, 5 runs): **3.1-4.2 s** first audio (engine 67-85 ms, first synthesis 2.9-4.0 s). Likely the gap to the 7.0 s fresh-install number is the phone's own file cache keeping the model files in memory after a force-stop (not proven). Measure both cases: after a fresh install or a reboot (truly cold) and after a force-stop (the common case).
- Unexplained: one plain Debug (JIT) run after a fresh install read **2.8 s** (engine 22 ms), faster than AOT. Likely the model files were still cached from the run before, which would make it a force-stop case, not a fresh install. Rerun it once with the `cold-start` check before trusting any JIT/AOT comparison.
- So nearly all the cold time is the **first synthesis**, not loading the engine. The Slice 138 wait overlay shows meanwhile, but 7 s before the first word is the app's worst moment.
- Not known yet: whether the cost is the first inference itself (ONNX runtime warm-up, any text length) or the length of the first chunk (up to 2 sentences at the head).

#### Work

1. **Find out (first):**
   - [ ] Cold-start timing that is really cold every run (fresh process and cleared sentence cache; today's repeat runs hit a cache and read 240-578 ms): add it to the phone tooling (Slice 213's `cold-start` check).
   - [ ] On the S21 (AOT): first synthesis time for a 1-word, 1-sentence and 2-sentence first chunk. If the time barely changes with length, it is warm-up; if it scales, it is length.
2. **Fix by the evidence (Android and iOS, same reader design):**
   - [ ] Warm-up: as soon as the reader page opens (or the app starts, if cheap enough), synthesise a tiny throwaway text on a background thread at low priority, so the first real read finds a warm engine. Never block the UI; cancel if a real read starts.
   - [ ] Length: make the very first piece of a read one short sentence (or the first clause), then continue with normal chunks.
   - [ ] Whichever ships, a unit test for the pure part (first-piece split, warm-up cancel rules).

#### Target

First audio after a cold start under **2 s** on the S21 with an AOT build, and under 500 ms when the warm-up had time to finish before Play.

#### Verification

- Fast: unit tests above.
- S21: the `cold-start` check, 3 truly cold runs, before and after.
- iOS: compile in CI; TestFlight timing by hand.
