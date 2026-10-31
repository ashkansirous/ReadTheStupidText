### Slice 209 — Test hooks for language, voice, Translate and a staged Dino book update

*Built 2026-10-31 (not merged); verified on the emulator and the S21 (2026-10-31), iOS hooks compile-checked only.* Planned 2026-10-30. From the S21 night runs (docs/plan/notes/2026-10-29-s21-night-runs.md) and Slice 206. Tooling: Debug builds only, no Release behaviour change.

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookDispatcher.cs`, `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookCommand.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/Testing/TestHooks.Android.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/Testing/TestHooks.iOS.cs`, `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile/Testing/**` (new, if RTST needs app-level commands), `src/DinoReader.App/Testing/DinoTestCommands.cs`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/StagedBookUpdates.cs`, `tests/ReadTheStupidText.Tests/**`, `.maestro/**`, `tools/phone/**`, `tools/CLAUDE.md`, `docs/plan/slices/209-settings-and-book-update-test-hooks.md`, `src/DinoReader.App/SpokenLanguage.cs`, `src/ReadTheStupidText.Application/Reading/ReadAloudService.cs (only if the hook needs an existing method made reachable)`, `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml.cs (only to share the voice-set path)`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs` (set-translate hook entry only; added in the wave 2026-10-31, mobile Translate is the Review page tick, not a stored setting), `src/DinoReader.Core/BookUpdatePlan.cs` (force lowers the recorded version), `src/DinoReader.App/StarterStories.cs` (launch refresh must not undo a forced stage), `tests/DinoReader.Tests/BookUpdatePlanTests.cs` (added while building)

#### Why

- No hook switches the UI language, the voice or Translate after first run (only `skip-first-run lang=`; `TestHookDispatcher.cs:111-134`). The S21 night skipped every language check until they were driven by uiautomator taps, and taps break when a label or translation changes.
- Slice 181's apply time on a real phone was never measured: nothing stages a store update. `BookUpdater` checks at most once every 7 days and only against the live store.

#### Decisions (2026-10-30)

- New shared hooks, parsed by `TestHookCommand` and added to the iOS launch-key list (`TestHookDispatcher.cs:51`, or iOS drops them):
  - `set-language lang=<code>`: runs the same sequence as the app's own picker, on the main thread. RTST: `LanguageChooser.Apply` (`LanguageChooser.cs:35-44`: settings, `UiCulture.Apply`, the page's before-rebuild, `App.ShowMain()`), so the Slice 202 path is exercised. Dino: its UI-language path (`SpokenLanguage.Use`, `SpokenLanguage.cs:105`). Logs `HOOK set-language ok <code>`.
  - `set-voice id=<voice id>` (RTST): the picker's path (`VoicePickerPage.xaml.cs:331` / `ReadAloudService.cs:458`), so the voice name refreshes as in Slice 99.
  - `set-translate on|off [from=<code>]` (RTST): the toggle's path (`ReadAloudService.cs:287`).
  - `stage-book-update id=<story id>` (Dino): downloads the next store version of that book into `book-updates/<id>/v<n+1>.dino` (the same staging `StagedBookUpdates` uses) and logs `HOOK stage-book-update ok <id> v<n+1>`; the apply then runs at the next cold start and the `[BookUpdate]` log already times it. Fails loudly if the book is edited (edited books are skipped by design) or the store has no newer version.
- A wrong value fails loudly (`HOOK set-language failed unknown <code>`), never silently.
- Android and iOS get every hook in this slice (shared dispatcher; iOS through the URL scheme).

#### Work

- [x] `TestHookCommand` parses the four commands (unit-tested), launch keys updated.
- [x] RTST `set-language`, `set-voice` through the real UI paths. `set-translate mode=on|off` sets the Review page's Translate tick (mobile has no stored toggle; no `from=`, the source is detected).
- [x] Dino `set-language` and `stage-book-update`.
- [x] `tools/phone/README.md` and `tools/CLAUDE.md`: settings switch by hook first, `tap.ps1` only when no hook covers it (replaces "no hook exists yet").
- [x] A phone helper or `audio-timing.ps1` option that measures the Slice 181 apply (`stage-book-update`, force-stop, cold start, read `[BookUpdate]` apply time).
- [x] Maestro: one flow per hook on the emulator.

- [x] Runner checks (Slice 213, `tools/phone/checks/`): `language` (switch through each shipped UI language with `set-language`, read a line, assert the voice and the `HOOK set-language ok`; covers the night's sv/da/de/fa/nb/zh checks), `voice` (`set-voice`, voice name refresh as Slice 99), `translate` (`set-translate on`, read foreign text, assert the translated read; the Slice 139 race), and `book-update` (`stage-book-update`, cold start, apply time). Each added to the runner catalogue so a brief only names it.

#### Verification

- Fast: `TestHookCommand` parse tests.
- Emulator: Maestro flows (`set-language` en -> fa -> sv -> en with text in the reader, as the Slice 202 check; `set-voice`; `set-translate`), plus the Norwegian pack flow from Slice 201 (`.maestro/rtst/norsk-pack-reads.yaml`, never run yet; Maestro 2.12.0 is in `<worktrees>/.tools/maestro`).
- S21: the Slice 181 apply time on a real phone with `stage-book-update`.
- iOS: compile in CI; the URL-scheme hooks stay compile-checked only (no Mac, as Slice 162).

#### Changed while building

- `set-translate mode=on|off` sets the Review page's Translate tick, not a stored setting (mobile has none); no `from=`, the source is detected.
- Dino `set-language voice=1` answers `ok` only once the language is stored, after any pack wait (the first S21 run raced the store). `voice=0` skips the voice start, which crashes the Debug x86_64 emulator build.
- `stage-book-update force=true` (Debug only) leaves a mark so the apply lowers the recorded version, letting an up-to-date book be staged. The first forced apply on the S21 was undone because `StarterStories.UpdateIfOlderAsync` ran before `ApplyOnceAsync`; the mark is now honoured after the launch starter refresh.
- The first S21 run failed 4 checks on check-script bugs (hard-coded F3 voice, a sentence containing '.', an UpToDate book, the Dino stored-language race); `set-voice id=auto` now picks an installed-pack voice not in use.

#### From review

- High (reviews/s209-high.md, 7 of 8 applied) and medium (reviews/s209-medium.md, 3 of 5 applied); the review files live outside the repo.
- Not applied: high 3 and medium 5 (`DinoTestCommands.StageBookUpdateAsync` builds its own `BookUpdater` instead of sharing `App.xaml.cs:97`'s: a drift risk only, no failure today) and medium 3 (the emulator `language` check sends `voice=0` on purpose: the emulator has no pack downloads to wait for; the S21 run covers `voice=1`). Medium 2 was applied: `PlanOneBookAsync` records a missing baseline for the named book only.

#### Verification results

- Emulator, Maestro: `set-language`, `set-voice`, `set-translate` (rtst) and `set-language`, `stage-book-update` (dino) all PASS.
- S21 (2026-10-31): rtst language PASS (sv, da, de, fa, nb, zh, en), voice PASS (`supertonic:F1`), translate PASS (45 s); dino language PASS, book-update PASS, apply 940 ms with swap.

#### For CLAUDE.md

- Settings (UI language, voice, Translate) and a Dino book update are switched by test hook (`set-language`, `set-voice`, `set-translate`, `stage-book-update`), not by `tap.ps1`; use the phone runner checks `language`, `voice`, `translate`, `book-update`. Dino `set-language` sends `voice=0` on an emulator (the voice start crashes its Debug x86_64 build). (tools/CLAUDE.md; Slice 209: docs/plan/slices/209-settings-and-book-update-test-hooks.md)

#### Not yet verified

- The iOS URL-scheme hooks on a device (compile-checked only).
