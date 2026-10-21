# Slice 56 — Windows: packs, first-run language, head start

*Built 2026-10-21 (PR #204); desktop acceptance partly done, rest in Not yet verified.*

**Batch:** 9
**Decisions:** 61, 62, 66, 68 (also 60, 63)
**Depends on:** Slices 47, 50, 51 (the head-start cache, the pack service and the multilingual engine selector it reuses). Pipeline edits (LFS removal in `build.yml`) depend on **PR #165** merging.
**Touches:** `src/ReadTheStupidText.App/**` (`ReadTheStupidText.App.csproj`, `Package.appxmanifest`, `App.xaml.cs`, new first-run window, `ControlPanelWindow.*`), `src/ReadTheStupidText.Infrastructure/**` (`Reading/SupertonicModelService.cs`, `Reading/SupertonicSpeechReader.cs`, `Reading/CompositeSpeechReader.cs`, `Settings/LocalSettingsStore.cs`), `src/ReadTheStupidText.Application/**`, `.github/workflows/build.yml`, `STORE.md`, `PRIVACY.md`, `site/privacy.html`, `CLAUDE.md`
**Story issue:** —

## Goal

The Microsoft Store package stops carrying the ~145 MB voice. On first launch
the user picks a language, the voice downloads in the background and the
built-in Windows voice reads until it arrives; once it does, the natural voice
starts with the same instant head start as Android.

## Steps

- [x] **Remove the bundle.** `ReadTheStupidText.App.csproj` lines 31-38: delete the
      `<Content Include="VoiceModel\**\*">` group and its comment; move or delete
      `src/ReadTheStupidText.App/VoiceModel/` (the files now feed `packs.yml`,
      Slice 50, so keep them in the repo or move them to a packs source folder and
      update that workflow's path). Update `.github/workflows/build.yml`: the
      Windows build's LFS checkout (PR #165 lines ~252-255) is no longer needed if
      nothing else is LFS; keep the `changes` filter consistent (no
      `src/ReadTheStupidText.App/VoiceModel/**` entry).
- [x] `Package.appxmanifest` (`<Capabilities>`, lines 73-75: only `runFullTrust`
      today): add `<Capability Name="internetClient" />`. `STORE.md` *Capabilities
      and justification* gets the matching sentence (Decision 60), as do
      `PRIVACY.md` and `site/privacy.html`.
- [x] **Pack service.** Implement `IVoicePackService` (Slice 50 interface) for
      Windows in Infrastructure, storing under `ApplicationData.Current.LocalFolder`
      `packs\<id>`; same resume/retry/sha256 behaviour. Rewrite
      `SupertonicModelService.cs` (`AppContext.BaseDirectory` + `VoiceModel`,
      lines 14, 33) to look in the pack folder.
- [x] **First-run language window** in `App/` (9 languages, same set and
      persistence as Slice 50 via `ISettingsStore`; `LocalSettingsStore`
      gains the `Language` key). Shown from `App.xaml.cs` startup when none is stored.
      Shows download progress, then closes; the tray app stays usable meanwhile.
- [x] **WinRT voice as the first-run voice.** `Infrastructure/Reading/CompositeSpeechReader.cs`
      already routes to `SpeechReader` (WinRT `SpeechSynthesis`) while the model
      is not ready (the CLAUDE.md "internal safety net" language changes: it is
      now a labelled, selectable first-run voice). Add it to the picker as
      "Windows voice", and keep the Decision-14 note updated.
- [x] **Head start.** Port the Slice 47 cache into `SupertonicSpeechReader`:
      reuse `SentenceAudioCache`, backed by `AudioChunkPaths` (Decision 49,
      `Infrastructure/Reading/AudioChunkPaths.cs`) so cached chunk files persist
      instead of being deleted per read (`DeleteRead`); keep the startup sweep
      for orphans and add the size cap. Lookup before `tts.Generate` (the
      generate-then-write section near lines 335-355), respecting
      `IsCurrent`/token. Triggers: the Windows read paths already funnel through
      `ReadAloudService.ReadEntryAsync` (line 549); add head-start prepare when
      text arrives (auto-read waits for the settle delay, so prepare at the
      moment text is captured, not after).
- [x] **Engine selector.** Reuse the Slice 51 language-aware selector with the
      Windows `ISpeechReader` implementations; replace the `NeuralVoiceCatalog`
      single-table listing with the per-language catalog.
- [x] **Keep the onnxruntime dedupe target** (`RemoveWinMlOnnxRuntimeDuplicate`,
      csproj lines ~103-118): it resolves WinML vs sherpa `onnxruntime.dll`, which
      is unrelated to where the model lives; leave it and note why.
- [x] Store listing: the first-run download disclosure from Slice 43 goes live;
      re-check the offline claim wording.
- [ ] `CLAUDE.md`: replace "ships inside the package" on Windows, the
      `Infrastructure` bullet, and the Decision 24 warm-up note (warm-up now
      follows pack readiness). *Left for `update-docs` (the implementer does not edit CLAUDE.md); the For CLAUDE.md block below has the lines.*

## Acceptance

- Unpack the built MSIX: no `VoiceModel\` folder; the package is ~145 MB smaller
  (record before/after).
- Fresh install: language window appears; reading works at once with the Windows
  voice; the pack downloads; the next read uses the natural voice with no restart.
- Offline first launch: clear message, retry works, Windows voice keeps reading.
- Select text with auto-read after the pack is installed: first audio is faster
  than before for text already prepared (compare "first audio" log lines).
- Store certification dry run passes the `internetClient` capability check.

## Risks / notes

- First network use in the Windows app: the Store listing, `PRIVACY.md` and the
  capability justification must ship together with this slice.
- Pack resume over a flaky connection needs a real test (kill network mid-download).
- Core-code changes rebuild all platforms.

## Decisions taken during build

- **Pack location.** Packs live in `ApplicationData.Current.LocalCacheFolder\packs\<id>` and the sentence cache in `LocalCacheFolder\sentences`, not `LocalFolder`. Context7 (Windows App SDK docs) says LocalFolder is backed up to the cloud and LocalCache is not; a 145 MB re-downloadable pack belongs in the cache. `AudioChunkPaths.SentenceFolder` and `PackFolders.PacksRoot()` expose both.
- **Cache, not per-read chunk files.** Instead of making `audio\<id>\chunk-N.wav` persistent, the Android design is mirrored: a persistent `SentenceAudioCache` (100 MB LRU, `FileSentenceStore`) holds one clip per sentence, and each chunk is joined from its clips into the existing per-read `audio\<id>\chunk-N.wav` play file. `DeleteRead` and the 1-day startup sweep therefore still only ever touch play files; cached clips are never opened by the player.
- **Reader rewrite.** `SupertonicSpeechReader` is now language-aware: it builds Supertonic, Piper or Melo from `IVoiceModelService.Paths` (ref-counted `BuiltEngine`, `SherpaEngineFactory`, structs passed by ref), uses `HeadPolicy.Cached`, and implements `IHeadStart`. The engine/sentence helpers are Infrastructure files of their own (`BuiltEngine.cs`, `SherpaEngineFactory.cs`, `NeuralSentenceSource.cs`) to keep methods under 30 lines. Windows still does not read per-voice segments (`SupportsSegments` stays false).
- **Duplication instead of moving shared code.** `PackVoiceModelService` and `WindowsVoicePackService` are Windows ports of `MobileVoiceModelService` and `MobileVoicePackService` (not editable here), and `VoicePackSetup` (Application) is the MAUI-free twin of `VoicePackInstaller`. Follow-up: move the model service and the pack pipeline into Application/Mobile.Shared-neutral code and delete the copies.
- **Windows voice in the picker.** `PackVoiceCatalog` lists the pack's voices, then "Windows voice" (`SystemVoice.Id`). Before a pack is ready the control panel shows the download status line in place of the picker (it would hold one entry). Choosing "Windows voice" persists as `VoiceId = "system"` and routes reads to the WinRT reader even after a pack is installed.
- **Head-start trigger.** `ReadAloudService` takes an optional `IHeadStart` and prepares in `BeginAutoRead`, right after the text is captured and before the settle delay (selection and clipboard paths). Deselect cancels it. Hotkey and Play-button reads do not prepare: they read at once and share any in-flight synthesis anyway.
- **Head-start timing (review fix).** `PrepareHeadStart` no longer prepares on every event. Each auto-read event restarts an 80 ms wait (`HeadStartStableMs`), and the prepare starts only when it elapses. 80 ms is below the 150 ms baseline settle, so a lone click-select still has its opening sentences synthesizing before the read starts, and below the 250 ms burst gap, so each event of a drag-select restarts the wait and only the text the drag ends on is synthesized. Deselect, a newer event and Dispose cancel a pending wait; the synthesis slot also re-checks its token once acquired, so text superseded before its synthesis began is never generated or cached.
- **Other review fixes.** `VoicePackSetup.StartAsync` loops until the stored language is the one it served. `WindowsVoicePackService.DownloadAsync` downloads a newer catalogue version of an installed pack (`PackVersions` numeric dotted compare) and swaps it in by renaming the old folder aside, so a failed update keeps the old pack. A resumed 206 reports its starting fraction at once, and a `Content-Range` that does not start at the requested offset restarts from zero. `SupertonicSpeechReader` remembers a failed engine build per model (folder, kind, pack version) and goes straight to the Windows voice until the model changes. `LanguageWindow` closes only when a run it started ends Ready. `PackVoiceModelService.SameModel` also compares pack version and full voice entries.
- **Language window.** First run (no stored language) opens it from `App.OnLaunched`; every later launch runs `VoicePackSetup.StartAsync()` quietly (activate an installed pack, or resume the download). A tray item "Voice language..." reopens the window to change language or retry. Changing language while a download is in flight is blocked (the list is disabled). Existing users upgrading from a bundled-model version see the window once and read with the Windows voice until the pack lands.
- **Offline detection.** `NetworkInterface.GetIsNetworkAvailable()` before a download and as the tie-breaker when classifying a failure; `PackErrorClassifier` does the rest.
- **Pipeline.** `windows.yml` drops `lfs: true`. `ship-targets.mjs` is unchanged: `src/ReadTheStupidText.App/` already covers `VoiceModel/`, and excluding it would also need `tests.yml` (outside Touches); a model change still rebuilds Windows, which is harmless. `.gitattributes` and `VoiceModel/` stay for `packs.yml`. The `RemoveWinMlOnnxRuntimeDuplicate` target is kept untouched (WinML vs sherpa `onnxruntime.dll`, unrelated to where the model lives).
- **Store listing files.** `docs/store-listings/*.md` are outside Touches and unchanged; `STORE.md` now says to paste the existing "download note" into the Microsoft Store description in the release that ships this slice.

## Review follow-ups

- **Deduplicate with `Mobile.Shared` (review finding 6, skipped).** `PackVoiceModelService`, `WindowsVoicePackService` and `VoicePackSetup` duplicate their Mobile.Shared twins, and the reader's read-ownership fix below exists only on Windows. Move the model service, pack pipeline and the reader's session logic into shared code and delete the copies.
- **Read ownership (finding 1).** `SupertonicSpeechReader` tracks the active read as `ActiveRead(engine, ReadOwnership)`: the original `SpeakAsync` and every Skip that continues playback are owners, only the last one out ends the read (clears `ReadInProgress`), and Stop clears it at once. This also lets a head-start prepare for the next text run while the old read's native synthesis winds down. The Android reader (Slice 47) still has the Skip bug.
- **Head policy (finding 4).** The two-sentence head is used only when its sentences are already in the sentence cache at read start; otherwise the one-sentence default. A prepare plans with the two-sentence head so a prepared text qualifies.
- **Engine build failures (finding 3).** Any failure to build the engine surfaces as `NeuralEngineUnavailableException` (composite falls back to the Windows voice) and triggers a model refresh.
- **Follow-up from desktop acceptance (2026-10-21).** Log `[INF]` lines in `WindowsVoicePackService` / `VoicePackSetup` for pack download start, finish and failure, and for which engine read. Today no log line names the engine, so the system-to-neural handover can't be confirmed from logs.
- **Offline first launch (finding 7).** `App` listens to `NetworkChange.NetworkAvailabilityChanged` and re-runs `VoicePackSetup.StartAsync` when the setup is Offline and the network returns.

## Built (verification)

- `dotnet build` App x64, Mobile net10.0-android: succeeded. `dotnet test`: 336 passed. `node --test .github/scripts/*.test.mjs`: 69 passed. actionlint: clean.
- New tests: `WindowsVoicePackServiceTests` (install, Range resume, server ignoring Range, hash mismatch, invalid pack, offline, transient retry, installed pack makes no request), `VoicePackSetupTests`, `PackVoiceModelServiceTests` (model service and `PackVoiceCatalog`), and three head-start cases in `ReadAloudServiceGeneratingTests`.

#### Desktop acceptance (2026-10-21)

Run on the real Windows machine.

- PASS: the language window shows on a true first run (clean package state). The earlier missing window was stale LocalSettings `Language` (gate at `App.xaml.cs:125`, read at `LocalSettingsStore.cs:83`), not a code bug.
- PASS: picking English downloads supertonic-3 into `LocalCache\packs\supertonic-3` (9 files, about 145 MB) and the window closes.
- PASS: Persian shows "No natural voice for this language yet. The Windows voice reads."
- PASS: French closes as Ready with no new download.
- PASS: restart does not re-show the window or re-download.
- PASS: the tray item "Voice language..." reopens the window.

#### Not yet verified (needs the real Windows machine, VS **(Package)** profile)

- The system voice reading before the pack arrives, and the neural voice taking over after (the download finished in about 2 s, too fast to see; no log line names the engine).
- Offline first launch.
- Killing mid-download and resuming.
- MSIX size before/after, and no `VoiceModel\` in the unpacked MSIX.
- The `internetClient` Store certification dry run.
- The first-audio comparison (faster "first audio" log lines for already-prepared text).

#### For CLAUDE.md (lifted 2026-10-21)

- Windows packs: no voice model in the MSIX, `internetClient` declared, packs in `LocalCacheFolder\packs\<id>`; Windows twins of the Mobile pack services (change both together); `windows.yml` has no LFS.
- Windows head start: `SupertonicSpeechReader` implements `IHeadStart` with `SentenceAudioCache` in `LocalCacheFolder\sentences`; prepare after an 80 ms stable wait in `BeginAutoRead`; reads owned by `ActiveRead`/`ReadOwnership`.
