# Slice 47 — Head start: sentence-level audio cache (Android first)

*Planned.*

**Batch:** 9
**Decisions:** 66 (builds on 25, 49, 50)
**Depends on:** none (Slice 46 improves the wait but is independent). Windows wiring is Slice 56.
**Touches:** `src/ReadTheStupidText.Application/Reading/**`, `src/ReadTheStupidText.Mobile/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `tests/ReadTheStupidText.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

Pressing Play should start sound almost at once. The app synthesizes the first
sentences ahead of time (right after a file lands or a page is scanned, or after
the user stops typing for a second), caches them, and reuses any sentence it has
already spoken in the same voice and language. Editing one sentence only
re-synthesizes that sentence.

## Steps

- [ ] Add a pure `SentenceAudioCache` in `Application/Reading/`: key =
      `hash(normalized sentence) + voiceId + language` (normalize: trim, collapse
      whitespace; SHA-256 hex). Value = chunk file path + duration. Put storage
      behind an interface (`ISentenceStore`: get/put/evict, size cap with LRU)
      so it unit-tests without disk. Language is a constant (`"en"`) until
      Slice 51 gives `ISpeechReader` a language.
- [ ] `SpeechTextChunker` (`Application/Reading/SpeechTextChunker.cs`): the
      first chunk is currently at most **one** sentence and <= `FirstChunkMax =
      120` (line 26; `BiasFirstChunkToShortHead` line 132, `SplitLeadingHead`
      line 153). Change to the first **two** sentences with a raised cap
      (start ~200, tune from the latency log lines) and keep the word-wrap
      fallback for punctuation-sparse text. Update `SpeechTextChunkerTests.cs`.
- [ ] Make the cache unit a sentence: the two-sentence head is split back into
      sentences for caching. `SplitSentences` already exists privately (used at
      line 154); expose what the cache needs.
- [ ] **Where it sits.** Windows keeps chunks as `audio\<id>\chunk-N.wav`
      (`Infrastructure/Reading/AudioChunkPaths.cs`, Decision 49) with a chunk
      map (Decision 50). Android does not: `AndroidSupertonicSpeechReader`
      writes `read-chunk-<guid>.wav` into `FileSystem.CacheDirectory`
      (`WriteWavFileAsync`, line 510) and raises `ChunkChanged` once per
      read (line 107). Give Android a `CacheDirectory/sentences/` folder keyed
      by the hash, replace the guid naming, delete only evicted files.
- [ ] Reader hook: in `GenerateChunkAsync` (lines 234-248) look up the cache
      before `tts.Generate`; on a hit return the path and call
      `RecordChunkDuration` from the stored duration; on a miss generate, store,
      return. Re-check `IsCurrent(generation)` / the cancellation token before
      storing: `Generate` is synchronous and cannot be interrupted (Slice 35
      note in the Windows reader), so a superseded read must not write or play
      stale audio, and `Stop()` (`_generation++`, line ~329) still wins.
- [ ] Add `PrepareAsync(string text)` (head start) on the Android neural reader,
      via a separate small interface (e.g. `IHeadStart`) so the system-voice
      readers need nothing: synthesizes the first two sentences into the cache,
      then keeps synthesizing ahead (bounded by `MaxSynthesisConcurrency`)
      without playing; cancelled by a new `BeginGeneration`-style token.
- [ ] Triggers in `ReaderPage.xaml.cs`: call it immediately after `OnFileTapped`
      fills the editor (line 111), after `OnPasteTapped` (line 78) and after the
      scan (Slice 45); for typing, `TextEditor.TextChanged` with a 1 s debounce
      (reset a `CancellationTokenSource` per keystroke). Cancel when playback
      starts or the voice changes.
- [ ] Edits: because entries are per sentence, editing sentence 3 re-synthesizes
      only sentence 3 on the next prepare or play; confirm via the log lines.
- [ ] Logging: the Android reader has no `ISystemLog` (ctor takes only
      `IVoiceModelService`, line 91). Inject `ISystemLog` (`Application/Logging`)
      with a simple Android implementation (debug log / file under
      `AppDataDirectory`), and write `cache hit` / `cache miss <hash8> (n chars)`
      Debug lines like the Windows reader's per-chunk timings.
- [ ] Tests in `tests/ReadTheStupidText.Tests`: key normalization and hash
      stability, LRU eviction, hit on same sentence + voice + language, miss on
      any change, the new two-sentence head. Run
      `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`.

## Acceptance

- Android: paste or open a file, wait ~2 s, tap Play -> sound starts clearly
  faster than before (compare "first audio" in the log lines).
- Edit sentence 4 of a cached text -> only that sentence misses in the log.
- Switch voice -> misses (key includes voice); switch back -> hits.
- Stop mid-prepare or mid-read leaves no playback from a superseded read.
- Unit tests green in CI.

## Risks / notes

- The key must never leak audio across voices; test it explicitly.
- Bound the disk use (e.g. 100 MB) and purge on startup like `PurgeOrphaned`.
- Touches the chunker shared with Windows, so this slice builds all platforms
  (core filter); expected.
- Windows head-start wiring waits for Slice 56.
