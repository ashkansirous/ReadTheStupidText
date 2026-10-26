# Slice 129 — Dino voice features out of the shared speech engine

*Built 2026-10-25 (PR #364), not yet verified on a device.*

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

- [x] Map every use of `VoiceFlavour`/`PitchShifter`/flavour fields in the shared projects and the three
      platform readers (`git grep`), and design the hook from that list (write it in this file first).
- [x] Smallest end-to-end: move `PitchShifter` behind the hook on Android, Dino flavours still audible.
- [x] Move `VoiceFlavour` to `DinoReader.Core`; the segment carries an opaque transform; RTST passes none.
- [x] Same on iOS and the Windows head readers.
- [x] Move the related tests from `ReadTheStupidText.Tests` to `DinoReader.Tests`.

## Hook design (built)

Uses found by `git grep`: `VoiceFlavour`/`FlavourShape`/`VoiceFlavours` in Domain; `PitchShifter` in Application;
`VoicedSegment.Flavour`, `SpeechChunkPlan.Flavour`, `SentenceCacheKey.Flavour`; the flavour parameter, shape, pace,
sentence gap and `PitchShifter.Apply` step copied in `NeuralSentenceSource` (Windows), `AndroidSupertonicSpeechReader`
and `IosSupertonicSpeechReader.Synthesis`, plus plumbing in `SupertonicSpeechReader` and the iOS reader/head start.

- `IVoicedAudioTransform` (Application): `CacheTag`, `Pace`, `SentenceGap`, `Apply(samples, sampleRate)`; `VoicedAudio`
  holds the defaults and null-safe helpers (`PaceOf`, `GapOf`, `TagOf`, `Apply`).
- `VoicedSegment.Transform` / `SpeechChunkPlan.Transform` replace `Flavour`; `SentenceCacheKey.VoiceTag` replaces it
  (empty tag keeps the old cache id; a tag adds the same suffix the flavour name did, so existing cached clips still hit).
- `VoiceFlavour`, `VoiceFlavours`, `PitchShifter` moved to `DinoReader.Core` (namespace `DinoReader.Core`);
  `FlavourTransform.For(flavour)` builds the transform (null for Normal). `StoryVoicing` and `VoicePreview` call it.
- Readers: null transform = pace 1, 150 ms gap, no tag, samples untouched (RTST and head start).
- Tests: pitch/flavour tests moved to `DinoReader.Tests`; the plan/segment/cache-key hook tests stay in
  `ReadTheStupidText.Tests` (`VoicedAudioTransformTests`, they need Application internals).

## Verification

- Both test suites green; `ship-targets` on this branch's Dino-only follow-up edits sets no RTST target.
- Emulator device-test: Dino pigs book, each flavoured character sounds as before (Small, Whisper,
  animal sounds); RTST phone reads unchanged.
- Windows head: a flavoured character reads.

*Changed while building:* `VoiceFlavour`, `VoiceFlavours` and `PitchShifter` ended up in `DinoReader.Core`
(namespace `DinoReader.Core`), with `FlavourTransform` caching one instance per flavour (null for Normal).
`VoicedAudio.TagOf` throws on an empty tag of a non-null transform, so a transform can never collide with the
no-flavour cache id. Cache ids are unchanged (tests pin tag == flavour name). Outside the Touches line:
`IosSupertonicSpeechReader.cs` (one line).

*Built and checked:* RTST tests 1085 (2 skipped), Dino tests 1148; clean builds of RTST x64, Dino Android,
Dino Windows head, RTST Mobile Android and the Dino iOS compile.

#### For CLAUDE.md

- The shared speech engine (`NeuralSentenceSource`, `SupertonicSpeechReader`, the Android/iOS readers) must not
  name a Dino flavour, pitch or effect. Dino voice features live in `DinoReader.Core` and reach the engine only
  through `IVoicedAudioTransform` (`CacheTag`, `Pace`, `SentenceGap`, `Apply`); a non-null transform needs a
  non-empty `CacheTag`. RTST passes none. (Slice 129: docs/plan/slices/129-dino-voice-features-out-of-shared-engine.md)
- Cache ids stay as they were: the tag is the flavour name, so existing cached clips still hit. Don't rename a
  flavour without bumping the cache. (Slice 129: docs/plan/slices/129-dino-voice-features-out-of-shared-engine.md)

#### Not yet verified

- Emulator: Dino story with flavoured voices (Small, Whisper, animal sounds) still sounds the same, by ear.
- Windows head: a flavoured character reads.
- RTST phone read unchanged.
