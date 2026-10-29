### Slice 190 — RTST phone: highlight one sentence at a time, not a whole chunk

*Planned 2026-10-29.* Read The Stupid Text phone app only (Android and iOS). Windows keeps its chunk highlight (user, 2026-10-29). Gap found in Slice 99.

**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.*.cs` (only where the highlight is set), `src/ReadTheStupidText.Application/Reading/ReadTimingTracker.cs`, `src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs` (sentence spans inside a chunk, if not already exposed), `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader*.cs`, `tests/ReadTheStupidText.Tests/**`, `tools/flows/**` (the reader flow), `docs/plan/slices/190-rtst-phone-sentence-highlight.md`.

#### Why

The phone highlight covers a whole `SpeechTextChunker` chunk (up to 3 sentences; the head chunk up to 2), because `ReaderPage.ShowHighlight(ReadChunk)` uses the chunk's `SourceEnd` and `ReadTimingTracker` times chunks only. The phone already synthesises sentence by sentence (the Slice 47 sentence cache), so it knows when each sentence starts.

#### Decisions (user, 2026-10-29)

- Phone only: Android and iOS in this slice. Windows (the reading box, Decision 50) stays chunk-level.

#### Work

- [ ] Pure: give each chunk its sentence spans (source start/end) and a tracker that reports the current sentence from the reader's sentence-start events; unit tests for spans and for the switch-over at each boundary, including Farsi/RTL text, a chunk with one sentence, and unpunctuated text (one span).
- [ ] Android: the Supertonic reader raises a sentence-started event with the sentence index when its audio starts playing (not when synthesised).
- [ ] iOS: the same event from `IosSupertonicSpeechReader`.
- [ ] System-voice fallback (no neural pack): keep the chunk highlight unless the engine reports word or range progress.
- [ ] `ReaderPage` highlights the current sentence and keeps it scrolled into view; pause, speed change, skip back/forward and a new read reset it cleanly.
- [ ] Scripted reader flow asserts the highlight moves to sentence 2 while chunk 1 is still playing (Debug log marker).

#### Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`.
- Emulator flow: a 3-sentence chunk highlights one sentence at a time, in English and Farsi.
- iOS: compile here; a TestFlight run reads a 3-sentence paragraph with one-sentence highlights.
