# Slice 60 — Reading with a voice per character

*Planned.*

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

- [ ] `ISpeechReader` (Application/Reading): add `SpeakSegmentsAsync(IReadOnlyList<SpeechSegment>
      segments, string? activityId = null)` with `SpeechSegment(string Text, string VoiceId,
      int SourceStart, int SourceEnd)`. A neutral type in Application; Dino Reader maps
      its `Segment` + cast onto it.
- [ ] Android: the chunker in `AndroidSupertonicSpeechReader.cs` returns
      `IReadOnlyList<string>` (line 102); make it return text **with source ranges**, as
      Windows does for `ReadChunk` (`SupertonicSpeechReader.cs:253`). Raise `ChunkChanged`
      per chunk with real `SourceStart/SourceEnd` instead of once for the whole text
      (`AndroidSupertonicSpeechReader.cs:107`, `AndroidSpeechReader.cs:93`). Read The
      Stupid Text benefits (Decision 76).
- [ ] Android segment read: each segment is chunked separately and each chunk carries
      its speaker into synthesis. Windows `SpeakChunksAsync` already takes the speaker as
      a parameter (`SupertonicSpeechReader.cs:110`, Decision 29); mirror that on Android.
      Generation counter and cancellation behave as for a plain read.
- [ ] Windows reader: implement `SpeakSegmentsAsync` the same way, or throw
      `NotSupportedException` with a reason; decide at implementation and record it here.
      The WinRT fallback reader throws `NotSupportedException` explicitly.
- [ ] Both `CompositeSpeechReader` copies (Mobile.Shared and `Infrastructure/Reading`) forward
      the new call to the neural reader (or Slice 44's single copy).
- [ ] `DinoReader.App`: the page runs `StorySegmenter` -> `SpeakerAttributor` ->
      `VoiceCaster` (Slice 59) on the typed text, has a narrator picker, then reads. The
      cast lives in memory until Slice 61 persists it.
- [ ] Unit tests for the pure range bookkeeping (chunk ranges tile the segment, offsets are
      relative to the full text, empty segments dropped) in `tests/ReadTheStupidText.Tests`.

## Acceptance

- The Peter / Cotton Tail example plays Male2 / Male1 / Female1 / Male1 on a device, and
  each character keeps their voice when the text is read again.
- Android `ChunkChanged` fires once per chunk with ranges that map back to the exact text.
- Read The Stupid Text Mobile reads exactly as before (single voice, no new gaps).

## Risks / notes

- Voice switches between chunks must not add audible gaps; Slice 47's cache keys must include the voice.
- Slice 47 edits the same readers: sequence or rebase.
