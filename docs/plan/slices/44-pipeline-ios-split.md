# Slice 44 — Pipeline: iOS filter group and platform-neutral Mobile root

*Built (wave 2026-10-20b), PR #183.*

**Batch:** 9
**Decisions:** 69 (also 52-58 stand)
**Depends on:** PR #165 (merged 2026-10-20).
**Touches:** `src/ReadTheStupidText.Mobile/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/**`, `src/ReadTheStupidText.Application/Reading/**`, `.github/workflows/build.yml`, `.github/scripts/**`, `CLAUDE.md`
**Story issue:** —

## Goal

One shared version still ships per merge, but CI can tell an Android-only
change from an iOS-only change from a shared-MAUI change. Today every file under
`Mobile/**` is "android"; once iOS exists that would rebuild Android on every
iOS tweak and vice versa. No user-visible change yet; this unblocks Slice 48.

## Steps

- [x] **Prerequisite refactor: make non-`Platforms/` Mobile files
      platform-neutral.** Today `Mobile/CompositeSpeechReader.cs` (line 15-20)
      names `AndroidSupertonicSpeechReader` and `AndroidSpeechReader` directly,
      and `MauiProgram.cs` (lines 44-62) builds them inside `#if ANDROID`.
      Change the composite's constructor to take two `ISpeechReader`s (neural,
      fallback) plus `IVoiceModelService`; the event wiring (lines 27-36) only
      needs `ISpeechReader`. Keep `IDisposable` handling via `as IDisposable`.
- [x] Move the platform choice behind one seam: add
      `IPlatformServices`-style factory in `Mobile/` (interface only) with
      `CreateNeuralReader`, `CreateFallbackReader`, `CreateImageTextExtractor`;
      the Android implementation lives in `Platforms/Android/` and is
      registered from `Platforms/Android/` code, so `MauiProgram.cs` loses its
      `#if ANDROID` blocks (lines 46-52, 57-61). A `Platforms/iOS/` registration
      is added by Slice 48.
- [x] Grep the rest of `Mobile/` (non-`Platforms`) for `Android.` / `Xamarin.`
      usings and move any hit behind an interface (known clean: ReaderPage,
      VoicePickerPage, MobileVoiceModelService use only MAUI + Application).
- [x] `build.yml` `changes` job: add an `ios` output next to `windows` and
      `android`; split the `android` filter group into
      `android` (`src/ReadTheStupidText.Mobile/Platforms/Android/**`,
      Android-only scripts), `ios` (`.../Platforms/iOS/**`), and `mobile-shared`
      (`src/ReadTheStupidText.Mobile/**` not under `Platforms/`, using the
      `!`-negation form `dorny/paths-filter` supports) that sets both. Core
      (Domain/Application/Documents/tests/pipeline) sets all three.
- [ ] *Deferred to Slice 48.* Wire `ios` into `version`/`test` `if:` conditions and `release-publish`
      (`needs.changes.outputs.ios != 'true' || needs.build-ios.result == ...`),
      leaving `build-ios` itself to Slice 48. Until then the `ios` output is
      computed but unused by any build job.
- [ ] *Deferred to Slice 48.* Extend `release-draft`'s *Carry forward assets* step: add an `IOS` env and
      `carry_forward` for the iOS asset pattern (the `.ipa`, name fixed in Slice
      48; use a placeholder constant here).
- [x] Consider Lets Call Mom's tested path-to-target mapping
      (`D:\Lets-Call-Mom\.github\scripts\ship-targets.mjs` + `.test.mjs`): if the
      filter grows past what `dorny/paths-filter` expresses cleanly, copy that
      pattern into `.github/scripts/ship-targets.mjs` with a node test; else
      keep the YAML filters and note why.
- [x] *(Lifted by the index pass.)* Update `CLAUDE.md`'s Batch 8 CI note (lines 37-48): add the `ios` group
      and the Mobile split rule; also the *Mobile* bullet in "Layered structure".

## Acceptance

- On a PR/push: touching only `Platforms/Android/**` -> `android` true,
  `ios` false; touching `Mobile/ReaderPage.xaml.cs` -> both; touching
  `src/ReadTheStupidText.Domain/**` -> all three; docs-only -> none.
  Check by pushing throwaway PRs or by running the mapping script's test.
- `dotnet build src/ReadTheStupidText.Mobile -f net10.0-android` still
  succeeds and Android still reads aloud on a device (refactor is behaviour-neutral).
- A normal Android release still builds, signs and publishes.

## Risks / notes

- Do not start before PR #165 merges; rebase onto it.
- A bad filter silently skips a platform. Keep the mapping unit-tested if moved to a script.
- `.github/workflows/build.yml` is a core-filter path itself, so editing it builds everything.

## Built

*Changed while building:*

- `IPlatformServices` (`CreateNeuralReader` / `CreateFallbackReader` / `CreateImageTextExtractor`); each returns a new caller-owned instance and is called once at startup. `MauiProgram` is partial with a required `private static partial void RegisterPlatformServices(IServiceCollection)`, implemented in `Platforms/Android/MauiProgram.Android.cs` (a target framework listed without an implementation fails to compile). `AndroidPlatformServices` is the Android implementation. No Android/Xamarin type remains outside `Platforms/`.
- `CompositeSpeechReader` takes `(ISpeechReader neural, ISpeechReader fallback, IVoiceModelService)` and disposes both. `AndroidSpeechReader` now stops and shuts down its `TextToSpeech` on `Dispose`.
- CI: `dorny/paths-filter` pinned `@v4.0.3` with `predicate-quantifier: some-with-excludes` (needs 4.0.3 or later). Groups: `android` = `Mobile/Platforms/Android/**` + `stage-android-package.sh`; `ios` = `Mobile/Platforms/iOS/**`; `mobileshared` = `Mobile/**` minus `Mobile/Platforms/**`. Outputs: `android` = core || android || mobileshared; `ios` = core || ios || mobileshared (computed, not consumed until Slice 48). Every other job's graph is unchanged.
- `.github/scripts/path-filters.test.mjs` reads the filters and the `changes` output expressions straight from `build.yml` and asserts the mapping: Android-only -> android; iOS-only -> ios; shared Mobile -> both; Domain/`build.yml` -> all; Windows App -> windows; docs -> none; DinoReader -> none. It runs in its own `test-filters` job (ubuntu, picomatch 2.3.1) that no other job needs. The YAML filters were kept rather than copying Lets Call Mom's `ship-targets.mjs`.
- Deliberately deferred to Slice 48: wiring `ios` into `version`/`test`/`release-publish` (with no `build-ios` job it would run jobs for nothing, and an iOS-only push would keep re-triggering until a release) and extending `release-draft`'s carry-forward for iOS (a placeholder `.ipa` pattern would be untested dead code).

*From review:*

- High review: a comment inside `release-publish`'s folded `if:` would have invalidated the whole workflow (fixed); the filter test wasn't run in CI (now its own job); the placeholder iOS carry-forward looped over 50 releases for nothing (removed); the partial method was made required; the fallback reader is now disposed; the test derives outputs from `build.yml`; the test script was removed from `core`.
- The coordinator caught `fetch-depth: ${{ ... && 0 || 1 }}` (0 is falsy, so always 1); superseded when the test moved to its own job and `changes` went back to its original checkout.
- Medium review: ios gating and dead carry-forward removed (deferred to 48); the test parser fails on unrecognised lines; Android TTS shutdown; file split. The reviewer doubted `v4.0.3`/`some-with-excludes`; the coordinator verified the tag (`ceb8a2b`) and the mode in the action's source.

*Verified:* 174 unit tests, DinoReader tests, Mobile Android and App x64 builds, actionlint (0 issues), filter test pass. On the RTST_Test emulator (arm64 embedded build, pack already installed) the app launches, a read plays through the app's MediaPlayer (neural voice), and with "Phone voice" selected the read is dispatched to Google TTS.

#### For CLAUDE.md

- Batch 8 CI note: replace "`Mobile` -> Android" with: Mobile splits by folder (Decision 69): `Mobile/Platforms/Android/**` -> Android only, `Mobile/Platforms/iOS/**` -> iOS only, the rest of `Mobile/**` (shared MAUI root incl. csproj/Resources) -> both. `dorny/paths-filter@v4.0.3` with `predicate-quantifier: some-with-excludes` (keep the pin; the mode needs 4.0.3 or later). `.github/scripts/path-filters.test.mjs` (job `test-filters`) checks the mapping; change it together with the `filters:` block. The `ios` output is computed but unused until Slice 48 adds `build-ios`, so an iOS-only change runs nothing. (Slice 44: docs/plan/slices/44-pipeline-ios-split.md)
- Mobile bullet: nothing outside `Platforms/` names an Android or iOS type; engines come from `IPlatformServices` via a required `MauiProgram.RegisterPlatformServices` partial in each `Platforms/<OS>/`; Slice 48 adds `Platforms/iOS`. (Slice 44: docs/plan/slices/44-pipeline-ios-split.md)

#### Not yet verified

- A real Android release built through the new filter (happens on this PR's merge): check build-android and play-submit go green.
