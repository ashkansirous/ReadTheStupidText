# Slice 56 — Windows: packs, first-run language, head start

*In progress (wave 2026-10-21b), draft PR.*

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

- [ ] **Remove the bundle.** `ReadTheStupidText.App.csproj` lines 31-38: delete the
      `<Content Include="VoiceModel\**\*">` group and its comment; move or delete
      `src/ReadTheStupidText.App/VoiceModel/` (the files now feed `packs.yml`,
      Slice 50, so keep them in the repo or move them to a packs source folder and
      update that workflow's path). Update `.github/workflows/build.yml`: the
      Windows build's LFS checkout (PR #165 lines ~252-255) is no longer needed if
      nothing else is LFS; keep the `changes` filter consistent (no
      `src/ReadTheStupidText.App/VoiceModel/**` entry).
- [ ] `Package.appxmanifest` (`<Capabilities>`, lines 73-75: only `runFullTrust`
      today): add `<Capability Name="internetClient" />`. `STORE.md` *Capabilities
      and justification* gets the matching sentence (Decision 60), as do
      `PRIVACY.md` and `site/privacy.html`.
- [ ] **Pack service.** Implement `IVoicePackService` (Slice 50 interface) for
      Windows in Infrastructure, storing under `ApplicationData.Current.LocalFolder`
      `packs\<id>`; same resume/retry/sha256 behaviour. Rewrite
      `SupertonicModelService.cs` (`AppContext.BaseDirectory` + `VoiceModel`,
      lines 14, 33) to look in the pack folder.
- [ ] **First-run language window** in `App/` (9 languages, same set and
      persistence as Slice 50 via `ISettingsStore`; `LocalSettingsStore`
      gains the `Language` key). Shown from `App.xaml.cs` startup when none is stored.
      Shows download progress, then closes; the tray app stays usable meanwhile.
- [ ] **WinRT voice as the first-run voice.** `Infrastructure/Reading/CompositeSpeechReader.cs`
      already routes to `SpeechReader` (WinRT `SpeechSynthesis`) while the model
      is not ready (the CLAUDE.md "internal safety net" language changes: it is
      now a labelled, selectable first-run voice). Add it to the picker as
      "Windows voice", and keep the Decision-14 note updated.
- [ ] **Head start.** Port the Slice 47 cache into `SupertonicSpeechReader`:
      reuse `SentenceAudioCache`, backed by `AudioChunkPaths` (Decision 49,
      `Infrastructure/Reading/AudioChunkPaths.cs`) so cached chunk files persist
      instead of being deleted per read (`DeleteRead`); keep the startup sweep
      for orphans and add the size cap. Lookup before `tts.Generate` (the
      generate-then-write section near lines 335-355), respecting
      `IsCurrent`/token. Triggers: the Windows read paths already funnel through
      `ReadAloudService.ReadEntryAsync` (line 549); add head-start prepare when
      text arrives (auto-read waits for the settle delay, so prepare at the
      moment text is captured, not after).
- [ ] **Engine selector.** Reuse the Slice 51 language-aware selector with the
      Windows `ISpeechReader` implementations; replace the `NeuralVoiceCatalog`
      single-table listing with the per-language catalog.
- [ ] **Keep the onnxruntime dedupe target** (`RemoveWinMlOnnxRuntimeDuplicate`,
      csproj lines ~103-118): it resolves WinML vs sherpa `onnxruntime.dll`, which
      is unrelated to where the model lives; leave it and note why.
- [ ] Store listing: the first-run download disclosure from Slice 43 goes live;
      re-check the offline claim wording.
- [ ] `CLAUDE.md`: replace "ships inside the package" on Windows, the
      `Infrastructure` bullet, and the Decision 24 warm-up note (warm-up now
      follows pack readiness).

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
