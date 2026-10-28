# Slice 177 — Dino: slow speech for the first reads after the app starts

*Built 2026-10-28 on the emulator; Pixel and iOS timing not yet verified.* Batch 38 (test gaps found 2026-10-28), Dino Reader (Android and iOS). After Slice 175 (it needs the
`ENGINE_READY` and `CHUNK_SYNTH` markers).

**Touches:** `src/ReadTheStupidText.Mobile.Shared/**` except `Testing/**` (the shared neural readers and pack start-up), `src/DinoReader.App/**` except the Slice 174 files (`ReadingPage.*`, `TransportRow.cs`, `ReadingTheme.cs`, `PictureTextPages.cs`, `TextPages.cs`, `ReadingSurface.cs`, `ModeSwitch.cs`, `GalleryPage.*`, `GrownUpMode.cs`, `SettingsPage.cs`, `App.xaml.cs`, `CloseQuestionPage.cs`, `Platforms/Android/MainActivity.cs`, `Resources/Strings/**`), `tools/phone/**`, `tools/emulator/**`, `tests/DinoReader.Tests/**` (new test files only), `docs/plan/slices/177-dino-cold-start-synthesis.md`

## Why

On the user's Pixel 10 Pro (2026-10-28, Debug build), the first Dino reads after a fresh install are slow on every
chunk, not only the first: at 2x, first audio 5.0-5.9 s and chunk 2 ready after about 5.4 s, against 0.9-1.2 s
first audio and 40-60 ms for chunk 2 once warm. A 4.9 s silence between chunks was flagged in one cold run. RTST on
the same phone starts in about 0.25 s. A child pressing Play waits five seconds, then hears a pause mid-sentence.

Slice 17 warmed the Windows neural engine at start-up; check whether the phone readers do the same.
The cause is unknown: it may be the Debug build (no AOT, interpreter), the model being loaded per chunk, the engine
warming its threads, or the pack still unpacking after `PACK_READY`.

## Work

- [x] **Measure first, change nothing:** `audio-timing.ps1 -Aot` builds an AOT-compiled Dino with the `.dbg`
  package id (Debug configuration: the test hooks and Slice 175 markers exist only in Debug, so `-Release` was dropped), so timing matches Release speed. Three cold runs and three warm runs at
  1x and 2x. If Release is fast cold, the slice ends here with the numbers recorded (Debug-only, no user impact).
- [x] If Release is slow cold: find the phase from the markers (engine creation, first synthesis, every synthesis)
  and fix it by construction, e.g. create the engine once when the pack is ready and keep it, and synthesise the
  next chunk while the current one plays. Android and iOS share the reader, so both get the fix.
- [x] Set the `audio-timing.ps1` cold-start limit from the fixed numbers so a regression is flagged. *Done 2026-10-28 on the Pixel 10 Pro: cold first audio 3.2-3.3 s; limits set: first audio <= 5 s on a fresh install, chunk gap limit 1.7 s at 2x cold (1.3 s trips).*

## Verification

- Phone, Release build: cold first audio and chunk gaps within about 1.5x the warm numbers, at 1x and 2x, three
  runs each; the numbers recorded here.
- iOS: compiles; timing can't be run (no Mac), so it waits on TestFlight with the user's ear.
</content>
</invoke>

## Built and measured (2026-10-28, emulator x86_64 Debug, 2x, three-little-pigs)

- **Warm-up already existed** (Slice 17's shape: `WarmUpAsync` on pack ready and on model ready). **Cause found:** a once-only `_warmedUp` flag. Dino installs Supertonic first (the flag was set by warming it) and the Kokoro storyteller pack later; the read uses Kokoro, so its engine was never warmed and the first read built it (4.9-5.6 s).
- **Fix:** both readers (`AndroidSupertonicSpeechReader`, `IosSupertonicSpeechReader`) keep the warm state on the engine itself (`RefCountedTts` / `IosRefCountedTts`: a `_synthesized` flag under a lock). `WarmUp` acquires the engine the read would use (same `AcquireEngine`, so the same voice folder) and, inside that lock, synthesises one short sentence only if nothing has synthesised on it yet; a read's synthesis (`BeginSynthesis`/`EndSynthesis`) waits for a warm-up in progress and marks the engine warm only when it completes (not at acquire), and a warm-up skips an engine a read is synthesising on; a warm-up never evicts an engine in use (`EngineCache.Acquire(forWarmUp)` returns null, the next trigger retries); it warms the default voice's model folder and the folder of the latest read's voice. So a read and a warm-up never use the engine at once, a read that got there first has already warmed it, and an evicted engine comes back as a cold entry that the next trigger warms. Triggers (pack ready, model ready on app start or language change, the installer's await) just call `WarmUp`; duplicates find the engine warm. No ledger, no Idle check, nothing to retry.
- **Numbers, fresh install, first read** (engine ready / first audio): before 5.6, 5.5, 4.9 s / 17.5, 17.0, 17.2 s; after (read 30 s after the pack was ready) 1.2, 1.2, 1.3 s / 13.4, 13.4, 12.5 s. Read pressed the instant the pack is ready, before the fix: 19.2 s.
- **Cold app start, pack installed:** unchanged, about 1.2 s to engine ready and 1.6-1.8 s to first audio when the sentences are cached (the engine acquire itself is about 2 ms once warmed; the rest is the screen opening the book). An uncached page: 8.8 s before, 9.1 s after.
- **What is left is the emulator's synthesis speed, not a cold start:** an uncached first chunk (4.4 s of audio) takes about 11 s on the emulator (real-time factor about 2.5) whether the process is fresh or warm, and a chunk already in the sentence cache takes 6-150 ms. The earlier "cold start 1.8-2.7 s" runs were cache hits. The real-time factor on a phone is for the phone run to show.
- `audio-timing.ps1`: `-Emulator` (`-Serial` picks the device, default emulator-5554), `-Aot` (the way to measure Release speed), `-FreshInstall` (wipes the dev app's data every run), `-Warm`, `-SettleSeconds n` (wait after the pack is ready, as a child would). `-Warm` now works: the `open-book` hook reuses an open reading screen of the same book, and goes back to the shelf to open another book.
- Pixel 10 Pro run 2026-10-28: cold start first audio 3.2-3.3 s. Limits set from it: first audio <= 5 s on a fresh install, gap limit 1.7 s at 2x cold (1.3 s trips). Owed: iOS timing on TestFlight.

*Changed while building:* fresh-install first read measured 19.2 s to first audio (engine ready 6.1 s + first chunk 12.6 s); later cold starts with cached sentences 1.6-2.7 s. Debug JIT and AOT were the same, so it was not a Debug artefact. After the fix, engine ready is 1.16-1.39 s (was 4.9-5.6 s) for a read 30 s after the pack is ready; a read right after pack ready waits inside the first chunk (ReadingSession Loading UI), no overlap. Emulator synthesis runs at a real-time factor of about 2.5 (a 4.4 s chunk takes about 11 s), so absolute speed needs the phone.

*From review (three rounds):* the design settled after two rounds: warm state on the engine, not in a flag; `EngineCache.Acquire(paths, forWarmUp)` never evicts an engine in use; a read marks the engine warm only on completion; warm-up covers the default and last-read voice folders; Android and iOS identical. A `WarmUpLedger` was tried and removed. Reviews: `D:\ReadTheStupidText-worktreeseviews-review1.md`, `177-review2.md`, `177-review3.md`.

Tests: RTST 1482 + 2 skipped (2 new EngineCache tests), Dino 1651; Android builds, iOS compiles. `tools/phone/audio-timing.ps1` gained `-Emulator`/`-Serial`, `-Aot` (Release-speed proxy; a real Release build strips the hooks), `-Warm`, `-FreshInstall`, `-SettleSeconds`, `-AppLanguage`; the Debug open-book hook reuses only a top ReadingPage for the same story (`LoadedStoryId`).

#### For CLAUDE.md (lifted 2026-10-28)

- Warm-up state lives on the engine (`RefCountedTts` / `IosRefCountedTts`), never in a once-only reader flag: a flag spent on the Supertonic pack left the later Kokoro story voice cold. `WarmUp` must acquire through the same `AcquireEngine` a read uses; `EngineCache.Acquire(paths, forWarmUp)` must never evict an engine in use. Keep Android and iOS identical. (Slice 177: docs/plan/slices/177-dino-cold-start-synthesis.md) Folder: `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`.

#### Not yet verified

- iOS timing (TestFlight).
- A first read in a non-default voice folder (e.g. Farsi multi-folder pack) still starts cold.
- A Farsi book warmed on open is not proven on the emulator.
