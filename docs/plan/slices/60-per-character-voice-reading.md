# Slice 60 — Reading with a voice per character

*Built (wave 2026-10-21b), PR #193.*

**Batch:** 10
**Decisions:** 76
**Depends on:** Slices 58, 59; Slice 44 (where `CompositeSpeechReader` lives)
**Touches:** `src/ReadTheStupidText.Application/Reading/**`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/ReadTheStupidText.Infrastructure/Reading/**`, `src/DinoReader.App/**`, `tests/ReadTheStupidText.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

Dino Reader reads the example in four voices: narrator, Peter, Cotton Tail, Peter.
On Android `ChunkChanged` fires per chunk with the real source range, as on Windows,
so any highlight can follow the reading.

## Steps

- [x] `ISpeechReader` (Application/Reading): add `SpeakSegmentsAsync(IReadOnlyList<SpeechSegment>
      segments, string? activityId = null)` with `SpeechSegment(string Text, string VoiceId,
      int SourceStart, int SourceEnd)`. A neutral type in Application; Dino Reader maps
      its `Segment` + cast onto it.
- [x] Android: the chunker in `AndroidSupertonicSpeechReader.cs` returns
      `IReadOnlyList<string>` (line 102); make it return text **with source ranges**, as
      Windows does for `ReadChunk` (`SupertonicSpeechReader.cs:253`). Raise `ChunkChanged`
      per chunk with real `SourceStart/SourceEnd` instead of once for the whole text
      (`AndroidSupertonicSpeechReader.cs:107`, `AndroidSpeechReader.cs:93`). Read The
      Stupid Text benefits (Decision 76).
- [x] Android segment read: each segment is chunked separately and each chunk carries
      its speaker into synthesis. Windows `SpeakChunksAsync` already takes the speaker as
      a parameter (`SupertonicSpeechReader.cs:110`, Decision 29); mirror that on Android.
      Generation counter and cancellation behave as for a plain read.
- [x] Windows reader: implement `SpeakSegmentsAsync` the same way, or throw
      `NotSupportedException` with a reason; decide at implementation and record it here.
      The WinRT fallback reader throws `NotSupportedException` explicitly.
- [x] Both `CompositeSpeechReader` copies (Mobile.Shared and `Infrastructure/Reading`) forward
      the new call to the neural reader (or Slice 44's single copy).
- [x] `DinoReader.App`: the page runs `StorySegmenter` -> `SpeakerAttributor` ->
      `VoiceCaster` (Slice 59) on the typed text, has a narrator picker, then reads. The
      cast lives in memory until Slice 61 persists it.
- [x] Unit tests for the pure range bookkeeping (chunk ranges tile the segment, offsets are
      relative to the full text, empty segments dropped) in `tests/ReadTheStupidText.Tests`.

## Acceptance

- The Peter / Cotton Tail example plays Male2 / Male1 / Female1 / Male1 on a device, and
  each character keeps their voice when the text is read again.
- Android `ChunkChanged` fires once per chunk with ranges that map back to the exact text.
- Read The Stupid Text Mobile reads exactly as before (single voice, no new gaps).

## Risks / notes

- Voice switches between chunks must not add audible gaps; Slice 47's cache keys must include the voice.
- Slice 47 edits the same readers: sequence or rebase.

## Decisions taken during build

- **Type name:** `SpeechSegment` collides with `SherpaOnnx.SpeechSegment` in the readers that import
  `SherpaOnnx`, so the Application type is `VoicedSegment(Text, VoiceId, SourceStart, SourceEnd)`.
- **Interface:** `ISpeechReader.SpeakSegmentsAsync(IReadOnlyList<VoicedSegment>, int? activityId = null)`
  (the existing id is `int?`, not `string?`) has a default implementation that throws
  `NotSupportedException`, so the iOS reader and test fakes need no change. Only the Android neural
  reader overrides it.
- **Windows:** the neural reader and the WinRT fallback throw `NotSupportedException` (no Windows
  consumer; Dino Reader is Android only). The Windows `CompositeSpeechReader` just forwards.
- **Mobile composite fallback:** when the neural pack isn't ready (or "Phone voice" is picked) the
  segments are read as one joined text in the phone voice instead of failing; `ChunkChanged` then
  reports the joined text, not story ranges.
- **Android TTS fallback reader** keeps its single whole-text `ChunkChanged` (its progress comes from
  utterance offsets; per-chunk ranges would need a redesign).
- **Ranges:** `SpeechChunkPlanner` (Application, internal) chunks each segment separately. A segment's
  first chunk starts at its `SourceStart` and its last ends at its `SourceEnd` (so a quote's chunks
  cover its quote marks); inner boundaries are the chunker's word positions shifted into the full
  text (+1 when the range is wider than the text, i.e. a quote). The first-chunk short-head bias
  applies per segment.
- **Voices:** a chunk with a voice id uses it; the narrator picker / `SetVoice` no longer restarts a
  segment read mid-way (plain reads behave as before).
- **Dino page:** unattributed quotes are read by the narrator. The cast is cached in memory
  (`StoryVoicing`) for the same text + narrator, so re-reading keeps each voice.
- **CI:** `src/DinoReader.Core/` joins the `dinoapp` group and `dinoreader-android.yml` paths (the app
  now references Core); `ship-targets.test.mjs` expectations updated to match.

## Changed while building (final shape)

Supersedes the earlier build decisions above where they differ.

- `ISpeechReader.SpeakSegmentsAsync(IReadOnlyList<VoicedSegment>, int? activityId)` plus `SupportsSegments`.
  The default returns a faulted `NotSupportedException` task, and `SupportsSegments` is false.
- `VoicedSegment(Text, VoiceId?, SourceStart, SourceEnd, TextStart)`, with `JoinText`.
- `SpeechChunkPlanner` (internal, Application/Reading) turns segments into chunks that each carry a voice
  and a source range. Ranges are clamped and monotonic. The short-head bias applies only to the first
  chunk of the whole plan (not per segment).
- `AndroidSupertonicSpeechReader` synthesizes each chunk in its own voice. `ChunkChanged` fires after
  `player.Start()`, possibly off the UI thread. `SpeakChunksAsync` is split into StartGenerations,
  PlayGeneratedChunkAsync and RestartForVoiceSwapAsync.
- The Mobile.Shared composite always handles segment reads (`SupportsSegments` true). It falls back to the
  joined text in the phone voice when the pack isn't ready, Phone voice is picked, or the neural reader
  can't do segments (e.g. iOS).
- On Windows the composite returns a faulted task and doesn't touch `_active`.
- `DinoReader.Core/SegmentTextLocator` added.
- `StoryVoicing` in DinoReader.App: casting runs off the UI thread, with an immutable atomic cache. The
  narrator picker is disabled while reading.
- `src/DinoReader.Core/**` is in the `dinoapp` ship target.

## From review

- High pass: 9 findings, 8 fixed. Skipped: merging adjacent same-voice segments. It needs the full story
  text passed through the interface (a follow-up), and there may be short gaps between same-voice runs.
- Medium pass: 9 findings, 7 fixed. Skipped: compile-time enforcement of `SupportsSegments`, and `JoinText`
  losing the original separators in the fallback.

## Emulator results (AVD RTST_Test, 2026-10-21)

- Dino Reader: the 3-character test story played as 6 chunks through MediaPlayer, ending idle. Stop works.
  The picker is disabled while reading. No crash on rapid taps. The pack (supertonic-3) was installed.
- Per-voice audio could not be confirmed by ear (emulator audio not audible to the tester). The voice
  mapping is unit-tested.
- Read The Stupid Text Mobile: a plain read still plays.
- Separate bug, not caused by this slice: the timer shows `00:09/00:04` and stays there after the read. It
  reproduces identically on an origin/main build.

#### For CLAUDE.md

- A segment read (`ISpeechReader.SpeakSegmentsAsync`) is the way to read with several voices. Only readers
  with `SupportsSegments` true do it natively; the Mobile composite falls back to one voice (joined text,
  phone voice) otherwise. (Slice 60: docs/plan/slices/60-per-character-voice-reading.md)
- `ChunkChanged` on Android fires when a chunk starts playing and may be off the UI thread, so marshal to
  it. (Slice 60: docs/plan/slices/60-per-character-voice-reading.md)
- Dino Reader's casting lives in `StoryVoicing` (`src/DinoReader.App`) over `DinoReader.Core`.
  (Slice 60: docs/plan/slices/60-per-character-voice-reading.md)

#### For README.md

- Dino Reader reads each character in their own voice, with a narrator picker.

#### Not yet verified

- Each character's voice heard on a real device.
- Gaps between voice switches, judged by ear.
- The pre-existing Android timer bug (`elapsed > total`, not reset on idle) needs its own fix.
