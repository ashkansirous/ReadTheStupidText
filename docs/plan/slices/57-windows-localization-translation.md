# Slice 57 — Windows: localized UI and on-device translation

*In progress (wave 2026-10-21), draft PR.*

**Batch:** 9
**Decisions:** 67, 68
**Depends on:** Slices 54 (translation interfaces and `TranslationFlow`), 56 (first-run language on Windows)
**Touches:** `src/ReadTheStupidText.App/**` (`MainWindow.xaml.cs`, `ControlPanelWindow.*`, `ActivityLogWindow.*`, `ReadingTextBoxWindow.*`, `Strings/**`, `Package.appxmanifest`), `src/ReadTheStupidText.Application/Reading/ReadAloudService.cs`, `src/ReadTheStupidText.Application/Translation/**`, `src/ReadTheStupidText.Infrastructure/Translation/**`, `src/ReadTheStupidText.Infrastructure/Settings/LocalSettingsStore.cs`, `tests/ReadTheStupidText.Tests/**`, `STORE.md`, `PRIVACY.md`, `CLAUDE.md`
**Story issue:** —

## Goal

The tray panel, menus and activity log speak the user's language, mirror right to
left for Farsi and Arabic, and a "Translate to my language" toggle translates
whatever gets read, including auto-read on selection and copy and the hotkey.

## Steps

- [ ] **Localize.** There are no `.resw`/`.resx` resources in `src/` today.
      Add `Strings/<lang>/Resources.resw` (WinUI resource loader, en default, then
      fa, fr, es, pt, zh, ar, ru, ja; confirm the packaged-app approach via
      context7 at implementation). Move every UI string out of
      `ControlPanelWindow.xaml` (18 `Text=` hits), `ActivityLogWindow.xaml` (18),
      `ReadingTextBoxWindow.xaml` (2), `MainWindow.xaml` (1) and the tray menu built in
      `MainWindow.xaml.cs` `BuildTrayMenu()` (line 185) plus code-behind strings
      (`ControlPanelWindow.xaml.cs` `DescribeStatus`, line 563, the status line
      composition, tooltips). Use `x:Uid` in XAML and `ResourceLoader` in code.
      Enum text helpers (`ActivityStateText`, `ActivityReasonText`,
      `WindowSourceText`) read from resources too. Add the supported languages
      to the manifest (`<Resources>`/`Languages`) so the Store lists them.
- [ ] Apply the stored language at startup (Slice 56's setting) via
      `ApplicationLanguages.PrimaryLanguageOverride`, and add a language picker
      to the panel (a row near the voice picker) that re-applies it.
- [ ] **RTL.** For fa and ar set `FlowDirection = RightToLeft` on each window's
      root `Grid` (`ControlPanelWindow`, `ActivityLogWindow`,
      `ReadingTextBoxWindow`) and check the borderless panel's hand-placed
      layout (speed pill, waveform, hotkey footer) and the header drag region. The
      tray `MenuFlyout` mirrors automatically in RTL; verify.
- [ ] **Translation research (before code).** Compare Opus-MT ONNX models
      (Helsinki-NLP/OPUS-MT, via onnxruntime, with pivot through English) with
      Bergamot (Firefox translation models, own native runtime): licence of
      engine and each pair's model, whether `fa` is covered in both directions,
      per-pair size (target < ~100 MB), CPU speed, and tokenizer needs
      (SentencePiece). Recommended default per Decision 67 is Opus-MT ONNX; record
      the decision and numbers here and in `plan.md`'s Decisions if it changes.
- [ ] **Translation packs.** Ship translation models as packs (kind `translation`)
      through the Slice 50/56 pack service and `packs.yml`; `packs.json` lists
      `xx -> en` and `en -> xx` pairs for the user's language.
- [ ] **Implement `ITranslator` and `ILanguageDetector` on Windows** in
      `Infrastructure/Translation/` (onnxruntime already ships with sherpa-onnx;
      decide as in Slice 52 whether to add the managed onnxruntime package). A
      light detector (character-script heuristics plus a small n-gram model, or
      the Windows `Windows.Globalization` language APIs) is enough; confirm.
- [ ] **Every read path.** All reads funnel through `ReadAloudService.ReadEntryAsync`
      (line 549; callers at lines 304 file read, 420 clipboard, 496 selection
      after debounce, and `PlayPauseOrReadAsync`, line 256): apply the
      `TranslationFlow` there, before `_reader.SpeakAsync` (line 560), when the
      toggle is on. Decide how the reading text box (`ReadTextChanged`,
      line 559) and the `ReadChunk` source ranges behave: they must refer to
      the text actually spoken (the translation), so raise `ReadTextChanged`
      with the translated text and label it in the box.
- [ ] Toggle and source dropdown in the control panel (compact: a small icon
      toggle in the `CONTROLS` row plus a source-language picker revealed like
      the speed control), persisted through `LocalSettingsStore`; add a tray menu
      item.
- [ ] Activity log: record the translation step (new `ActivityReason`/log line)
      and keep input-log rows redacted as before (Slice 20/21 rules apply to the
      translated text too).
- [ ] Tests: translation flow and any pure tokenizer/pre-post processing; the
      resource-key coverage check (every `x:Uid` has an entry in every language).
- [ ] `CLAUDE.md`, `STORE.md` (third-party notices for the chosen models),
      `PRIVACY.md` updated.

## Acceptance

- Switch the panel through all 9 languages: menus, panel, activity log are
  translated; Farsi and Arabic mirror correctly.
- Copy French text with auto-read on copy and the toggle on (target English):
  English is spoken; with it off, French. Same through the hotkey and a UIA
  selection.
- Translation works offline after the pack is installed; before it, the original
  is read and a message says why.
- Farsi source and target are either working or recorded as a gap here.

## Risks / notes

- Auto-read on selection must not stall: translate with a time budget, fall back
  to the original on timeout, and respect the generation counter so superseded reads
  never speak stale translations.
- Translation adds latency before the first word; Slice 47's head start does not
  cover it, so translate the first sentences first.
- Machine-translated UI strings need a native read before release.
