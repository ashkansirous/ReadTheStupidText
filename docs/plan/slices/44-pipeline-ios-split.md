# Slice 44 — Pipeline: iOS filter group and platform-neutral Mobile root

*Planned.*

**Batch:** 9
**Decisions:** 69 (also 52-58 stand)
**Depends on:** **PR #165 (Batch 8 implementation, branch `feat/ci-release-assets`) merging first.** The `changes`, `release-draft` and `release-publish` jobs edited here do not exist on `main` until then.
**Touches:** `src/ReadTheStupidText.Mobile/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/Platforms/Android/**`, `src/ReadTheStupidText.Application/Reading/**`, `.github/workflows/build.yml`, `.github/scripts/**`, `CLAUDE.md`
**Story issue:** —

## Goal

One shared version still ships per merge, but CI can tell an Android-only
change from an iOS-only change from a shared-MAUI change. Today every file under
`Mobile/**` is "android"; once iOS exists that would rebuild Android on every
iOS tweak and vice versa. No user-visible change yet; this unblocks Slice 48.

## Steps

- [ ] **Prerequisite refactor: make non-`Platforms/` Mobile files
      platform-neutral.** Today `Mobile/CompositeSpeechReader.cs` (line 15-20)
      names `AndroidSupertonicSpeechReader` and `AndroidSpeechReader` directly,
      and `MauiProgram.cs` (lines 44-62) builds them inside `#if ANDROID`.
      Change the composite's constructor to take two `ISpeechReader`s (neural,
      fallback) plus `IVoiceModelService`; the event wiring (lines 27-36) only
      needs `ISpeechReader`. Keep `IDisposable` handling via `as IDisposable`.
- [ ] Move the platform choice behind one seam: add
      `IPlatformServices`-style factory in `Mobile/` (interface only) with
      `CreateNeuralReader`, `CreateFallbackReader`, `CreateImageTextExtractor`;
      the Android implementation lives in `Platforms/Android/` and is
      registered from `Platforms/Android/` code, so `MauiProgram.cs` loses its
      `#if ANDROID` blocks (lines 46-52, 57-61). A `Platforms/iOS/` registration
      is added by Slice 48.
- [ ] Grep the rest of `Mobile/` (non-`Platforms`) for `Android.` / `Xamarin.`
      usings and move any hit behind an interface (known clean: ReaderPage,
      VoicePickerPage, MobileVoiceModelService use only MAUI + Application).
- [ ] `build.yml` `changes` job: add an `ios` output next to `windows` and
      `android`; split the `android` filter group into
      `android` (`src/ReadTheStupidText.Mobile/Platforms/Android/**`,
      Android-only scripts), `ios` (`.../Platforms/iOS/**`), and `mobile-shared`
      (`src/ReadTheStupidText.Mobile/**` not under `Platforms/`, using the
      `!`-negation form `dorny/paths-filter` supports) that sets both. Core
      (Domain/Application/Documents/tests/pipeline) sets all three.
- [ ] Wire `ios` into `version`/`test` `if:` conditions and `release-publish`
      (`needs.changes.outputs.ios != 'true' || needs.build-ios.result == ...`),
      leaving `build-ios` itself to Slice 48. Until then the `ios` output is
      computed but unused by any build job.
- [ ] Extend `release-draft`'s *Carry forward assets* step: add an `IOS` env and
      `carry_forward` for the iOS asset pattern (the `.ipa`, name fixed in Slice
      48; use a placeholder constant here).
- [ ] Consider Lets Call Mom's tested path-to-target mapping
      (`D:\Lets-Call-Mom\.github\scripts\ship-targets.mjs` + `.test.mjs`): if the
      filter grows past what `dorny/paths-filter` expresses cleanly, copy that
      pattern into `.github/scripts/ship-targets.mjs` with a node test; else
      keep the YAML filters and note why.
- [ ] Update `CLAUDE.md`'s Batch 8 CI note (lines 37-48): add the `ios` group
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
