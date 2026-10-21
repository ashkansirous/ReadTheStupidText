# Slice 47 — Head start: sentence-level audio cache (Android first)

*Built (wave 2026-10-21b), PR #201.*

**Batch:** 9
**Decisions:** 66 (builds on 25, 49, 50)
**Depends on:** none (Slice 46 improves the wait but is independent). Windows wiring is Slice 56.
**Touches:** `src/ReadTheStupidText.Application/Reading/**`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/**` (reader, `AndroidSystemLog.cs`), `src/ReadTheStupidText.Mobile.Shared/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `tests/ReadTheStupidText.Tests/**`, `src/ReadTheStupidText.Infrastructure/Reading/SupertonicSpeechReader.cs`, `PRIVACY.md`, `site/privacy.html`, `CLAUDE.md`
**Story issue:** —

## Goal

Pressing Play should start sound almost at once. The app synthesizes the first
sentences ahead of time (right after a file lands or a page is scanned, or after
the user stops typing for a second), caches them, and reuses any sentence it has
already spoken in the same voice and language. Editing one sentence only
re-synthesizes that sentence.

## Steps

- [x] Add a pure `SentenceAudioCache` in `Application/Reading/`: key =
      `hash(normalized sentence) + voiceId + language` (normalize: trim, collapse
      whitespace; SHA-256 hex). Value = chunk file path + duration. Put storage
      behind an interface (`ISentenceStore`: get/put/evict, size cap with LRU)
      so it unit-tests without disk. Language is a constant (`"en"`) until
      Slice 51 gives `ISpeechReader` a language.
- [x] `SpeechTextChunker` (`Application/Reading/SpeechTextChunker.cs`): the
      first chunk is currently at most **one** sentence and <= `FirstChunkMax =
      120` (line 26; `BiasFirstChunkToShortHead` line 132, `SplitLeadingHead`
      line 153). Change to the first **two** sentences with a raised cap
      (start ~200, tune from the latency log lines) and keep the word-wrap
      fallback for punctuation-sparse text. Update `SpeechTextChunkerTests.cs`.
- [x] Make the cache unit a sentence: the two-sentence head is split back into
      sentences for caching. `SplitSentences` already exists privately (used at
      line 154); expose what the cache needs.
- [x] **Where it sits.** Windows keeps chunks as `audio\<id>\chunk-N.wav`
      (`Infrastructure/Reading/AudioChunkPaths.cs`, Decision 49) with a chunk
      map (Decision 50). Android does not: `AndroidSupertonicSpeechReader`
      writes `read-chunk-<guid>.wav` into `FileSystem.CacheDirectory`
      (`WriteWavFileAsync`, line 510) and raises `ChunkChanged` once per
      read (line 107). Give Android a `CacheDirectory/sentences/` folder keyed
      by the hash, replace the guid naming, delete only evicted files.
- [x] Reader hook: in `GenerateChunkAsync` (lines 234-248) look up the cache
      before `tts.Generate`; on a hit return the path and call
      `RecordChunkDuration` from the stored duration; on a miss generate, store,
      return. Re-check `IsCurrent(generation)` / the cancellation token before
      storing: `Generate` is synchronous and cannot be interrupted (Slice 35
      note in the Windows reader), so a superseded read must not write or play
      stale audio, and `Stop()` (`_generation++`, line ~329) still wins.
- [x] Add `PrepareAsync(string text)` (head start) on the Android neural reader,
      via a separate small interface (e.g. `IHeadStart`) so the system-voice
      readers need nothing: synthesizes the first two sentences into the cache,
      then keeps synthesizing ahead (bounded by `MaxSynthesisConcurrency`)
      without playing; cancelled by a new `BeginGeneration`-style token.
- [x] Triggers in `ReaderPage.xaml.cs`: call it immediately after `OnFileTapped`
      fills the editor (line 111), after `OnPasteTapped` (line 78) and after the
      scan (Slice 45); for typing, `TextEditor.TextChanged` with a 1 s debounce
      (reset a `CancellationTokenSource` per keystroke). Cancel when playback
      starts or the voice changes.
- [x] Edits: because entries are per sentence, editing sentence 3 re-synthesizes
      only sentence 3 on the next prepare or play; confirm via the log lines.
- [x] Logging: the Android reader has no `ISystemLog` (ctor takes only
      `IVoiceModelService`, line 91). Inject `ISystemLog` (`Application/Logging`)
      with a simple Android implementation (debug log / file under
      `AppDataDirectory`), and write `cache hit` / `cache miss <hash8> (n chars)`
      Debug lines like the Windows reader's per-chunk timings.
- [x] Tests in `tests/ReadTheStupidText.Tests`: key normalization and hash
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

## Extra item: natural-end timer bug (found 2026-10-21, fixed in this slice)

- [x] When a read ended naturally the Android reader never stopped its poll loop or reset
      the timing tracker (only `Stop()` did), so the poll kept adding the finished player's
      position to a baseline that already included that chunk: the timer showed elapsed > total
      (e.g. 00:09/00:04) and never reset. `SpeakChunksAsync` now calls `FinishNaturalEnd()`
      under `IsCurrent(generation)` (StopPolling, `_timing.Reset()`, `RaiseTiming(0)`,
      progress 0, Idle), and `CurrentChunkPosition` returns zero unless the player is ready
      and the state is not Idle.

## Decisions taken during build

- **Cache unit and playback.** Playback chunks are unchanged. Each chunk is split into
  sentence units (`SentenceUnits`), every unit is fetched from the cache or synthesized, and
  the units are joined (`WavFile.Concat`, 150 ms silence between) into one temp play file.
  Cached files are never opened by the player or deleted by playback. A unit shorter than 15
  characters ("Mr.", "Yes.") is joined to the next one so no stub is synthesized alone.
- **Key** = SHA-256 of engine kind, language, voice id, model directory and normalized text
  (`SentenceCacheKey`). Playback speed is not in the key (applied by the player).
- **Storage.** `Application/Reading`: `ISentenceStore`, `FileSentenceStore` (temp name then
  move, partials purged on startup), `SentenceAudioCache` (100 MB LRU, index rebuilt from the
  folder), `SentenceAudioSource` (in-flight sharing, hit/miss log, store failures never fail a
  read). Android folder: `CacheDirectory/sentences`; orphaned `read-chunk-*.wav` purged at startup.
- **Head start preparation** covers the first 4 chunks only, not the whole text (battery
  on long files). Cancelled by a newer prepare, Play, a voice change re-prepares.
  *Refined in the final shape:* prepare is also limited to the first 2,000 characters.
- **Cancelled callers** never store or play; a native `Generate` already running finishes
  and goes to whoever still waits (another caller may have joined it).
- **Logging.** `AndroidSystemLog` writes to logcat (tag `RTST`), no file. The reader takes
  `ISystemLog? log = null` and defaults to it, so no DI change was needed for either app.
- **Chunker:** head = up to two sentences within 200 characters (was one sentence, 120).
  *Superseded by the final shape below:* this is now `SpeechTextChunker.HeadPolicy.Cached`,
  used by Android only. Windows keeps `HeadPolicy.Default` (1 sentence, 120 characters) until
  Slice 56 wires the cache. A short first chunk is left untouched.
- **Files outside the Touches line:** `CompositeSpeechReader.cs` (implements `IHeadStart` by
  forwarding to the neural reader when it would be the one to read); `Touches` also names the
  old `Mobile/Platforms/Android` path, the reader now lives in `Mobile.Shared`.

*Verified (implementer's earlier run):* 290 RTST tests, 80 DinoReader tests, Mobile and
DinoReader Android builds, App x64 build. Emulator (RTST_Test, arm64 embedded): typing prepared
the sentences after 1 s; Play with a prepared text had the chunk ready in 41-87 ms and first
audio 111 ms after the tap (the very first play after launch took about 5.6 s, which is
MediaPlayer initialisation on the emulator, not synthesis); a sentence edit re-prepared only the
changed sentence (hits for the rest); after a natural end the timer showed 00:00/--:-- and
progress was 0.

## Changed while building (final shape)

- **Application/Reading:**
  - `WavFile`: strict PCM 16-bit mono header validation, plus `IsComplete`.
  - `SentenceCacheKey`: SHA-256 over engine kind, language, voice id, model dir, pack version and normalised text.
  - `ISentenceStore` / `FileSentenceStore`: atomic writes; invalid or truncated clips purged at startup.
  - `SentenceAudioCache`: 100 MB LRU that invalidates bad clips.
  - `SentenceAudioSource`: shared in-flight synthesis that stores from the shared task, re-checks the cache, and surfaces cancellation only for the caller's own token.
  - `SentenceUnits`: merges units under 15 characters.
  - `IHeadStart` with `ReadInProgress`.
  - `SpeechTextChunker.HeadPolicy`: Default (1 sentence, 120 characters) for Windows, Cached (2 sentences, 200 characters) for Android.
- **Android reader:**
  - Each chunk is built from cached or synthesized sentence clips, joined with 150 ms of silence into a temp play file.
  - A corrupt join invalidates the clip and resynthesizes once.
  - One engine-level synthesis semaphore is shared by prepare and read.
  - Prepare is ordered by a synchronous `BeginPrepare`, runs on the thread pool, and is limited to the first 2,000 characters and 4 chunks.
  - `AndroidSystemLog` logs to logcat under the tag `RTST`.
  - Orphaned play files are purged at startup.
- **Natural-end timer fix:** `FinishNaturalEnd` stops polling and resets timing and progress before Idle, and `CurrentChunkPosition` is guarded. The bug existed on `main`.
- **ReaderPage:** prepares immediately on paste, file or scan, after a 1 s debounce when typing, and again when the cache key changes. `NoHeadStart` is the fallback.
- **Windows reader:** uses `WavFile.Encode` (byte-identical output).
- **Privacy:** `PRIVACY.md` and `site/privacy.html` state that sentence audio is cached on the device only.

## From review

- High pass: 10 findings, 9 fixed (one-line methods skipped as the repo convention).
- Medium pass: 10 findings, 9 fixed (no unit test for the natural end, because the reader needs a device).

## Emulator results (RTST_Test, 2026-10-21)

- Typing three sentences logged three cache misses during prepare.
- Play then logged three cache hits, `chunk 0 (74 chars, 3 sentence(s)) ready in 74 ms` and `first audio after 1040 ms`.
- After the read ended, the timer showed `00:00/--:--`.

#### For CLAUDE.md (lifted 2026-10-21)

- Android sentence audio is cached in `CacheDirectory/sentences` by `SentenceAudioCache`, keyed by engine, language, voice, model, pack version and text. Don't play cached clips directly; join them into a temp play file. (Slice 47: docs/plan/slices/47-head-start-sentence-cache.md)
- The shared chunker's head rule is a `SpeechTextChunker.HeadPolicy`. Windows keeps Default until Slice 56 wires the cache. (Slice 47: docs/plan/slices/47-head-start-sentence-cache.md)
- `IHeadStart` prepares the first chunks while the user is still on the page. (Slice 47: docs/plan/slices/47-head-start-sentence-cache.md)

#### For README.md (lifted 2026-10-21)

- On Android, reading starts almost instantly for text you've just pasted or opened: the first sentences are prepared while you look at it, and sentences already heard are cached.

#### Not yet verified

- A voice switch (miss, then hit when switching back) on the device.
- Head-start benefit on a real phone, not the emulator.
- Cache behaviour at the 100 MB cap.
- Slice 56 wires the same cache on Windows.

## Translation interaction (merge with Slice 54)

With the translate toggle on, the read speaks the translated text, so preparing the source text would cache the wrong sentences. The head start therefore skips preparing while translation is active (`TranslationActive()` in `ReaderPage.xaml.cs`: a translator exists and `TranslateEnabled`). Flipping the toggle restarts the prepare, so turning it off prepares the source text at once and turning it on cancels any prepare in flight. Paste, scan and file import share one helper, `ReplaceEditorText` (clear the translation source override, set the text, start the prepare).
