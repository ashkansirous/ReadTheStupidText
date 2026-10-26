# Slice 149 — Never drop a short word: catch silent output from the voices

*In progress (wave 2026-10-26), draft PR.*

**Batch:** 30 (Decisions 150-151; no new decision)
**Touches:** `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Infrastructure/Reading/NeuralSentenceSource.cs`, `src/ReadTheStupidText.Infrastructure/Reading/SupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosTtsEngine.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader.Synthesis.cs`, `src/ReadTheStupidText.Application/Reading/SilentOutputGuard.cs` (new), `tests/ReadTheStupidText.Tests/**`, `src/ReadTheStupidText.Infrastructure/CLAUDE.md`

## Why

Slice 145 measured that the Piper `mana` voice, the Farsi default since Slice 144, returns no audio for
short Farsi words: a lone «تق!», «دنگ!», «ووش!» or «میو میو!» is dropped in 26% of takes alone and
8% inside a sentence (23% at the flavour paces). `ganji` and `ganji_adabi` dropped nothing. A dropped
word is skipped silently, so a child hears the story with words missing, and the same can happen to any
grown-up's own book. (Note: docs/plan/notes/2026-10-26-sound-words-aloud.md.)

## Work

- [ ] A pure `SilentOutputGuard` (Application) decides whether a generated chunk is "silent": no samples,
      or near-silent audio far shorter than the text needs (thresholds from the Slice 145 data). Unit tests
      with sample buffers.
- [ ] Every sherpa-onnx generate path (Windows `SherpaEngineFactory`/`NeuralSentenceSource`, Android,
      iOS) checks each chunk. On a silent chunk: retry once with the text merged into its neighbour, then
      once with a sibling voice of the same pack (`ganji` for `mana`), then give up and log it. Never
      play a gap for real text.
- [ ] Log every rescue (voice, length, which retry worked), so the activity log shows how often it fires.
- [ ] Rule in `src/ReadTheStupidText.Infrastructure/CLAUDE.md`.

## Acceptance

- The Slice 145 harness's Farsi set read through the app path on Windows: no dropped word across 5 takes.
- Emulator: a Farsi Dino page with «تق!» and «دنگ!» is read in full with mana.
