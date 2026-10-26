# Slice 149 — Never drop a short word: catch silent output from the voices

*Built 2026-10-26 (wave PR #401); emulator Farsi read passed 2026-10-26 (logs only, not heard).*

**Batch:** 30 (Decisions 150-151; no new decision)
**Touches:** `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Infrastructure/Reading/NeuralSentenceSource.cs`, `src/ReadTheStupidText.Infrastructure/Reading/SupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosTtsEngine.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader.Synthesis.cs`, `src/ReadTheStupidText.Application/Reading/SilentOutputGuard.cs` (new), `tests/ReadTheStupidText.Tests/**`, `src/ReadTheStupidText.Infrastructure/CLAUDE.md`

## Why

Slice 145 measured that the Piper `mana` voice, the Farsi default since Slice 144, returns no audio for
short Farsi words: a lone «تق!», «دنگ!», «ووش!» or «میو میو!» is dropped in 26% of takes alone and
8% inside a sentence (23% at the flavour paces). `ganji` and `ganji_adabi` dropped nothing. A dropped
word is skipped silently, so a child hears the story with words missing, and the same can happen to any
grown-up's own book. (Note: docs/plan/notes/2026-10-26-sound-words-aloud.md.)

## Work

- [x] A pure `SilentOutputGuard` (Application) decides whether a generated chunk is "silent": no samples,
      or near-silent audio far shorter than the text needs (thresholds from the Slice 145 data). Unit tests
      with sample buffers.
- [x] Every sherpa-onnx generate path (Windows `SherpaEngineFactory`/`NeuralSentenceSource`, Android,
      iOS) checks each chunk. On a silent chunk: retry once with the text merged into its neighbour, then
      once with a sibling voice of the same pack (`ganji` for `mana`), then give up and log it. Never
      play a gap for real text.
      *Built:* the neighbour-merge retry is skipped on purpose: audio is cached per sentence unit, so merged audio
      cannot be split back into two clips. Retries are 2 more takes of the same voice, then the pack's next voice
      (`SiblingVoice`, engine built lazily by the engine cache).
- [x] Log every rescue (voice, length, which retry worked), so the activity log shows how often it fires.
- [x] Rule in `src/ReadTheStupidText.Infrastructure/CLAUDE.md`.

*Review fixes (high pass):* a give-up take is not cached; the sibling is an explicit table (mana, gyro -> ganji; ganji <-> ganji-adabi;
no entry, no sibling); a sibling that will not load is logged and skipped; the retry loop takes the token; the slot is handed back while
a second model loads; the best take is kept; the floor is 80 ms / 30 ms per letter divided by the pace (measured on the Slice 145 run5
clips: 0 false rescues in 972 non-mana clips of ganji, ganji-adabi, gyro, Supertonic, Kokoro; Melo zh `哞——`-type one-character clips fall
under it and are out of scope); log lines name the voice.

**Glued sound words (closed here):** a drop-prone voice (`SiblingVoice.IsDropProne`: mana) speaks each unit phrase by phrase
(`PhraseSplitter`, at `. ! ? … , ،`; `SilentOutputGuard.RescueUnitAsync`), each phrase guarded and rescued alone, joined with 150 ms
(90 ms after a comma) and cached as one unit. Acceptance, 155 takes (lone and glued, 5 each): 0 dropped after the guard (raw 16-18 lone, 2-3
glued). The mid-read wait while a second model loads is Slice 148's stall overlay (deferred); units with no letters are never retried.

*Review fixes (medium pass):* commas split only around a phrase of 6 letters or fewer (normal clauses keep their prosody); a "." splits unless it sits between two digits; mana clips carry cache tag `pg2` so old clips regenerate; the sibling take is one shared helper (`SiblingRescue`) used by the three readers, checks the token before the load; a take from another sample rate is resampled when joined; a comma-cut fragment gets the per-letter floor only. A sibling-model load is a wait over 500 ms with no loading state of its own: left to Slice 148's stall overlay (deliberate, not fixed here).

## Acceptance

- The Slice 145 harness's Farsi set read through the app path on Windows: no dropped word across 5 takes.
- Emulator: a Farsi Dino page with «تق!» and «دنگ!» is read in full with mana.

*From review:* high pass (`149-high`): 9 applied, 8 deferred. Medium pass (`149-medium`): 9 applied, 10 deferred to Slice 148.

#### For CLAUDE.md

Already added by the build in `src/ReadTheStupidText.Infrastructure/CLAUDE.md` (silent output guard rule, with the Slice 149 link).

#### Not yet verified

- Emulator (Dino, Farsi, mana, 2026-10-26): all 6 units of a two-page own book with «تق!», «دنگ!», «ووش!», «میو میو!», «هاپ!» were made; 3 came back silent and were rescued (take 2, take 3, sibling voice); no unit dropped; no crash. Not heard by ear. First audio 12.6 s on the slow emulator (cold load); judge on a phone.
- iOS compile of `IosSupertonicSpeechReader.Synthesis.cs` and `SiblingRescue` use.
- A listen to joined mana phrases (150/90 ms joins) for natural pacing.
