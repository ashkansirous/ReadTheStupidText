### Slice 190 — RTST phone: highlight one sentence at a time, not a whole chunk

*Built 2026-10-30 (Android and iOS), draft PR #573; on-screen look, pause/resume, iOS and Farsi not yet verified.* *Planned 2026-10-29.* Read The Stupid Text phone app only (Android and iOS). Windows keeps its chunk highlight (user, 2026-10-29). Gap found in Slice 99.

**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.*.cs` (only where the highlight is set), `src/ReadTheStupidText.Application/Reading/ReadTimingTracker.cs`, `src/ReadTheStupidText.Application/Reading/ISpeechReader.cs`, `src/ReadTheStupidText.Application/Reading/Sentence*.cs`, `src/ReadTheStupidText.Mobile.Shared/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs` (sentence spans inside a chunk, if not already exposed), `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader*.cs`, `tests/ReadTheStupidText.Tests/**`, `.maestro/rtst/**` (the reader flow; `tools/flows/` does not exist), `docs/plan/slices/190-rtst-phone-sentence-highlight.md`.

#### Why

The phone highlight covers a whole `SpeechTextChunker` chunk (up to 3 sentences; the head chunk up to 2), because `ReaderPage.ShowHighlight(ReadChunk)` uses the chunk's `SourceEnd` and `ReadTimingTracker` times chunks only. The phone already synthesises sentence by sentence (the Slice 47 sentence cache), so it knows when each sentence starts.

#### Decisions (user, 2026-10-29)

- Phone only: Android and iOS in this slice. Windows (the reading box, Decision 50) stays chunk-level.

#### Work

- [x] Pure: give each chunk its sentence spans (source start/end) and a tracker that reports the current sentence from the reader's sentence-start events; unit tests for spans and for the switch-over at each boundary, including Farsi/RTL text, a chunk with one sentence, and unpunctuated text (one span).
- [x] Android: the Supertonic reader raises a sentence-started event with the sentence index when its audio starts playing (not when synthesised).
- [x] iOS: the same event from `IosSupertonicSpeechReader`.
- [x] System-voice fallback (no neural pack): keep the chunk highlight unless the engine reports word or range progress.
- [x] `ReaderPage` highlights the current sentence and keeps it scrolled into view; pause, speed change, skip back/forward and a new read reset it cleanly.
- [x] Scripted reader flow asserts the highlight moves to sentence 2 while chunk 1 is still playing (Debug log marker).

#### Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`.
- Emulator flow: a 3-sentence chunk highlights one sentence at a time, in English and Farsi.
- iOS: compile here; a TestFlight run reads a 3-sentence paragraph with one-sentence highlights.

#### Changed while building

- The timeline is built in Application from clip durations plus the gap (`SentenceTimeline.FromClips`), not from per-sentence audio-start events as planned. `SentenceProgress` is a registry under one lock that raises `Changed`; `SentenceHighlightGate` and `SentenceSpans` sit beside it.
- `ISpeechReader` gained a default no-op `SentenceChanged` event; `CompositeSpeechReader` forwards it from the active reader. The system voice keeps the whole-chunk highlight. The early-start head needs no change (it only pre-fills).
- Known behaviour: the highlight can lag a boundary by up to ~200 ms (poll). Chunks that match no sentence timeline keep the chunk highlight.

#### From review

- High pass: 6 of 7 findings applied; the flow-assertion finding was skipped.
- Medium pass: 5 of 8 applied; skipped sub-ms drift, the UI-thread split and a tests note.
- Checks: 1597 tests pass; Android and iOS compile; Windows App build passes. Emulator `.maestro/rtst/sentence-highlight.yaml` PASS (SENTENCE 0 0/0 1/0 2 in order, ~3.4 s apart, no ANR).

#### For CLAUDE.md (lifted 2026-10-30)

- `src/ReadTheStupidText.Mobile/CLAUDE.md`: sentence highlight timing lives in Application `SentenceProgress`. Readers only Register/Select/Begin; never raise sentence events from the page. (Slice 190: docs/plan/slices/190-rtst-phone-sentence-highlight.md)

#### Not yet verified

- The on-screen sentence highlight seen by eye.
- Pause/resume mid-chunk.
- iOS TestFlight read of a 3-sentence paragraph.
- Farsi/RTL on device.
