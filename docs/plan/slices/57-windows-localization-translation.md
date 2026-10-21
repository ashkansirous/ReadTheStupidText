# Slice 57 — Windows: localized UI and on-device translation

*Built 2026-10-21 (PR #217, merge 03a3cd1); not yet verified, see below.*

**Batch:** 9
**Decisions:** 67, 68
**Depends on:** Slices 54 (translation interfaces and `TranslationFlow`), 56 (first-run language on Windows)
**Touches:** `src/ReadTheStupidText.App/**` (`MainWindow.xaml.cs`, `ControlPanelWindow.*`, `ActivityLogWindow.*`, `ReadingTextBoxWindow.*`, `Strings/**`, `Package.appxmanifest`), `src/ReadTheStupidText.Application/Reading/ReadAloudService.cs`, `src/ReadTheStupidText.Application/Translation/**`, `src/ReadTheStupidText.Infrastructure/Translation/**`, `src/ReadTheStupidText.Infrastructure/Settings/LocalSettingsStore.cs`, `tests/ReadTheStupidText.Tests/**`, `STORE.md`, `PRIVACY.md`, `CLAUDE.md`
**Story issue:** —

## Scope split (coordinator decision)

This slice ships localization plus an inert translate-before-read flow. The translation engine moves to Slice 75, "Windows translation engine": the Opus-MT vs Bergamot research, translation packs in `packs.yml`/`packs.json`, ONNX inference with SentencePiece, and "translate first sentences first". Those steps stay unticked below, marked "-> Slice 75". `ITranslator.IsAvailable` is a default member (true); the Windows `PackTranslator` returns false, so the translate toggle, source picker, note and tray item are hidden and `TranslateEnabled` reads false until Slice 75.

## Goal

The tray panel, menus and activity log speak the user's language, mirror right to
left for Farsi and Arabic, and a "Translate to my language" toggle translates
whatever gets read, including auto-read on selection and copy and the hotkey.

## Steps

- [x] **Localize.** There are no `.resw`/`.resx` resources in `src/` today.
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
- [x] Apply the stored language at startup (Slice 56's setting) via
      `ApplicationLanguages.PrimaryLanguageOverride`, and add a language picker
      to the panel (a row near the voice picker) that re-applies it.
- [x] **RTL.** For fa and ar set `FlowDirection = RightToLeft` on each window's
      root `Grid` (`ControlPanelWindow`, `ActivityLogWindow`,
      `ReadingTextBoxWindow`) and check the borderless panel's hand-placed
      layout (speed pill, waveform, hotkey footer) and the header drag region. The
      tray `MenuFlyout` mirrors automatically in RTL; verify.
- [ ] **-> Slice 75.** **Translation research (before code).** Compare Opus-MT ONNX models
      (Helsinki-NLP/OPUS-MT, via onnxruntime, with pivot through English) with
      Bergamot (Firefox translation models, own native runtime): licence of
      engine and each pair's model, whether `fa` is covered in both directions,
      per-pair size (target < ~100 MB), CPU speed, and tokenizer needs
      (SentencePiece). Recommended default per Decision 67 is Opus-MT ONNX; record
      the decision and numbers here and in `plan.md`'s Decisions if it changes.
- [ ] **-> Slice 75.** **Translation packs.** Ship translation models as packs (kind `translation`)
      through the Slice 50/56 pack service and `packs.yml`; `packs.json` lists
      `xx -> en` and `en -> xx` pairs for the user's language.
- [x] **Implement `ITranslator` and `ILanguageDetector` on Windows** in
      `Infrastructure/Translation/` (onnxruntime already ships with sherpa-onnx;
      decide as in Slice 52 whether to add the managed onnxruntime package). A
      light detector (character-script heuristics plus a small n-gram model, or
      the Windows `Windows.Globalization` language APIs) is enough; confirm.
- [x] **Every read path.** All reads funnel through `ReadAloudService.ReadEntryAsync`
      (line 549; callers at lines 304 file read, 420 clipboard, 496 selection
      after debounce, and `PlayPauseOrReadAsync`, line 256): apply the
      `TranslationFlow` there, before `_reader.SpeakAsync` (line 560), when the
      toggle is on. Decide how the reading text box (`ReadTextChanged`,
      line 559) and the `ReadChunk` source ranges behave: they must refer to
      the text actually spoken (the translation), so raise `ReadTextChanged`
      with the translated text and label it in the box.
- [x] Toggle and source dropdown in the control panel (compact: a small icon
      toggle in the `CONTROLS` row plus a source-language picker revealed like
      the speed control), persisted through `LocalSettingsStore`; add a tray menu
      item.
- [x] Activity log: record the translation step (new `ActivityReason`/log line)
      and keep input-log rows redacted as before (Slice 20/21 rules apply to the
      translated text too).
- [x] Tests: translation flow and any pure tokenizer/pre-post processing; the
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
  cover it, so translate the first sentences first (-> Slice 75).
- Machine-translated UI strings need a native read before release.

## Built on the branch

*Desktop-accepted 2026-10-21 (dev package); not yet on the Store.*

- **Localization:** `Strings/<tag>/Resources.resw` in 9 languages (en-US, fa, fr, es, pt, zh-Hans, ar, ru, ja), with `x:Uid` in XAML and `UiStrings.Get/Format` in code. A missing key falls back to en-US. Coverage tests check every key and placeholder in all 9 languages. The manifest lists the 9 languages. The localized "Windows voice" name is display-only.
- **Language change:** a Language row in the panel and the first-run `LanguageWindow` both start voice-pack setup, then restart via `AppInstance.Restart` (a failure is logged and shown as a tray notice).
- **RTL:** applies only when the effective resource language is fa or ar (`UiLanguageTag.Effective`/`Match`), with the transport row forced LTR.
- **Translate-before-read (inert):** `ReadTranslator` in `ReadAloudService.ReadEntryAsync` (20 s budget, original on timeout/failure/missing pack), `ScriptLanguageDetector`, `PackTranslator` (pack readiness only; `IsAvailable` is false, so the toggle, source picker, note and tray item are hidden). `ReadTextChanged` carries `ReadText(Text, Translated)`. The source-language pick is sticky; set it back to Auto-detect.
- **Branding:** `ProductInfo.Name` is the brand constant and window titles are branded.
- **Changed beyond Touches (accepted):** `LanguageWindow.*`, `PackSetupText.cs`, `App.xaml.cs`, `Application/Reading/ReadText.cs`, `App/ProductInfo.cs`.
- **Review:** two passes (10 + 10 findings, 1 skipped as not a bug). Checks: x64 build, 571 tests.
- **Desktop acceptance, 2026-10-21 (dev package), all 7 passed:** English, French and Farsi switch with a restart; Farsi is RTL while the transport row stays LTR; auto-read on copy works; no WRN/ERR in the system log. Observed: the native tray menu does not mirror in RTL (an H.NotifyIcon/Win32 limitation; the text is localized). `TimerText` moves to the left in RTL.
- **Left undone:** everything marked "-> Slice 75" (research, packs, inference engine, translate-first-sentences-first; `PackTranslator.TranslateAsync` always raises `TranslationModelUnavailableException`); a dedicated `ActivityReason` for the translation step (Domain is outside Touches; it is a system-log line `translated xx -> yy`); `STORE.md`, `PRIVACY.md` (no model ships yet) and root `CLAUDE.md`.

#### For CLAUDE.md (lifted 2026-10-21)

Target: `src/ReadTheStupidText.App/CLAUDE.md`.

- Every user-visible string lives in `Strings/<tag>/Resources.resw` for all 9 languages (`x:Uid` in XAML, `UiStrings.Get/Format` in code); the resw coverage tests fail on a missing key. Never hard-code UI copy. A language change restarts the app (`x:Uid` applies once per window). RTL only for fa/ar via `UiLanguageTag`; keep the transport row LTR. Translation UI stays hidden while `ITranslator.IsAvailable` is false (Slice 75 adds the engine). (Slice 57: docs/plan/slices/57-windows-localization-translation.md)

#### For README.md (lifted 2026-10-21)

- The Windows app's UI is available in 9 languages.

#### Not yet verified

- Native-speaker review of the 9 machine-translated resw files.
- The Store listing shows the 9 languages after the next submission.
