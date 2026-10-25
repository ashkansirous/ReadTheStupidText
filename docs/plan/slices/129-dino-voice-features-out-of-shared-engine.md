# Slice 129 — Dino voice features out of the shared speech engine

*Planned 2026-10-25.*

**Batch:** 27 (product separation, Decisions 133-137)
**Touches:** `src/ReadTheStupidText.Domain/Reading/VoiceFlavour.cs`, `src/ReadTheStupidText.Application/Reading/PitchShifter.cs`, `src/ReadTheStupidText.Application/Reading/VoicedSegment.cs`, `src/ReadTheStupidText.Application/Reading/SentenceCacheKey.cs`, `src/ReadTheStupidText.Application/Reading/SpeechChunkPlan.cs`, `src/ReadTheStupidText.Application/Reading/**` (new hook interface), `src/ReadTheStupidText.Infrastructure/Reading/NeuralSentenceSource.cs`, `src/ReadTheStupidText.Infrastructure/Reading/SupertonicSpeechReader.cs`, `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader.*.cs`, `src/DinoReader.Core/**`, `src/DinoReader.App/**`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`, `src/ReadTheStupidText.Infrastructure/CLAUDE.md`, `src/DinoReader.Core/CLAUDE.md`

## Why

The release audit of 2026-10-25 found that every Read The Stupid Text Microsoft Store resubmission in the
last 40 release runs (4 of them, and 2 more RTST Play/TestFlight uploads) came from Dino work editing the
shared engine: character flavours (Slice 113), pitch shifting, animal sounds and storyteller voices
touched `VoiceFlavour`, `PitchShifter`, `NeuralSentenceSource`, `SherpaEngineFactory` and
`SupertonicSpeechReader`. RTST never uses flavours (Batch 23 out of scope: "flavours in RTST"), yet each
change re-ships it and triggers a Store review.

## Decisions

- **Decision 136:** the shared speech engine only knows "synthesize this segment with this voice id and
  speed". Dino-only voice features (flavours, pitch, animal effects) live in `DinoReader.Core` and
  reach the engine through one small hook in Application (for example an `IVoicedAudioTransform`
  applied to the PCM, plus an opaque per-segment tag in the cache key). A shared-engine change then
  means a real engine change, and shipping both apps for it is correct.

## Work

- [ ] Map every use of `VoiceFlavour`/`PitchShifter`/flavour fields in the shared projects and the three
      platform readers (`git grep`), and design the hook from that list (write it in this file first).
- [ ] Smallest end-to-end: move `PitchShifter` behind the hook on Android, Dino flavours still audible.
- [ ] Move `VoiceFlavour` to `DinoReader.Core`; the segment carries an opaque transform; RTST passes none.
- [ ] Same on iOS and the Windows head readers.
- [ ] Move the related tests from `ReadTheStupidText.Tests` to `DinoReader.Tests`.

## Verification

- Both test suites green; `ship-targets` on this branch's Dino-only follow-up edits sets no RTST target.
- Emulator device-test: Dino pigs book, each flavoured character sounds as before (Small, Whisper,
  animal sounds); RTST phone reads unchanged.
- Windows head: a flavoured character reads.
