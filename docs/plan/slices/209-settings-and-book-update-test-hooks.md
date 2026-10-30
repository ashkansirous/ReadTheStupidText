### Slice 209 — Test hooks for language, voice, Translate and a staged Dino book update

*Planned 2026-10-30.* From the S21 night runs (docs/plan/notes/2026-10-29-s21-night-runs.md) and Slice 206. Tooling: Debug builds only, no Release behaviour change.

**Touches:** `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookDispatcher.cs`, `src/ReadTheStupidText.Mobile.Shared/Testing/TestHookCommand.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/Testing/TestHooks.Android.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/Testing/TestHooks.iOS.cs`, `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile/Testing/**` (new, if RTST needs app-level commands), `src/DinoReader.App/Testing/DinoTestCommands.cs`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/StagedBookUpdates.cs`, `tests/ReadTheStupidText.Tests/**`, `.maestro/**`, `tools/phone/**`, `tools/CLAUDE.md`, `docs/plan/slices/209-settings-and-book-update-test-hooks.md`

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

- [ ] `TestHookCommand` parses the four commands (unit-tested), launch keys updated.
- [ ] RTST `set-language`, `set-voice`, `set-translate` through the real UI paths.
- [ ] Dino `set-language` and `stage-book-update`.
- [ ] `tools/phone/README.md` and `tools/CLAUDE.md`: settings switch by hook first, `tap.ps1` only when no hook covers it (replaces "no hook exists yet").
- [ ] A phone helper or `audio-timing.ps1` option that measures the Slice 181 apply (`stage-book-update`, force-stop, cold start, read `[BookUpdate]` apply time).
- [ ] Maestro: one flow per hook on the emulator.

- [ ] Runner checks (Slice 213, `tools/phone/checks/`): `language` (switch through each shipped UI language with `set-language`, read a line, assert the voice and the `HOOK set-language ok`; covers the night's sv/da/de/fa/nb/zh checks), `voice` (`set-voice`, voice name refresh as Slice 99), `translate` (`set-translate on`, read foreign text, assert the translated read; the Slice 139 race), and `book-update` (`stage-book-update`, cold start, apply time). Each added to the runner catalogue so a brief only names it.

#### Verification

- Fast: `TestHookCommand` parse tests.
- Emulator: Maestro flows (`set-language` en -> fa -> sv -> en with text in the reader, as the Slice 202 check; `set-voice`; `set-translate`), plus the Norwegian pack flow from Slice 201 (`.maestro/rtst/norsk-pack-reads.yaml`, never run yet; Maestro 2.12.0 is in `<worktrees>/.tools/maestro`).
- S21: the Slice 181 apply time on a real phone with `stage-book-update`.
- iOS: compile in CI; the URL-scheme hooks stay compile-checked only (no Mac, as Slice 162).
