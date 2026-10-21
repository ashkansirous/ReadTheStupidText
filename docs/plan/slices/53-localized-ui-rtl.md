# Slice 53 — Android: localized UI and right-to-left layout

*Built (wave 2026-10-21b), PR #194.* Emulator-checked on RTST_Test on 2026-10-21 (Farsi and Portuguese walked through); see "Verified on the emulator". Not yet verified: native-speaker read, Arabic RTL.

**Batch:** 9
**Decisions:** 68
**Depends on:** Slice 50 (first-run language choice and its `ISettingsStore` property)
**Touches:** `src/ReadTheStupidText.Mobile/**` (RTST app only: all pages, `Resources/Strings/**`, `App.xaml.cs`, `ReadTheStupidText.Mobile.csproj`, `Platforms/Android/AndroidManifest.xml`), `tests/ReadTheStupidText.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

The app speaks the user's language: every label, button and message appears in
the language chosen on first run (English, Farsi, French, Spanish, Portuguese,
Chinese, Arabic, Russian, Japanese), Farsi and Arabic lay out right to left, and
the choice can be changed in settings.

## Steps

- [x] Inventory every hard-coded UI string in `Mobile/`: XAML `Text=`/`Placeholder=`
      in `ReaderPage.xaml` (16 hits), `VoicePickerPage.xaml` (6), `AppShell.xaml`
      (title), and strings in code-behind (`ReaderPage.xaml.cs` alert titles/messages
      at lines 98, 115, 119, `PickerTitle` line 92; `VoicePickerPage.xaml.cs`;
      the Slice 45 scanner messages). Delete `ScanPage` strings only if Slice 45
      has not merged yet (it removes that page).
- [x] Add `Resources/Strings/AppResources.resx` (English, neutral) plus
      `AppResources.<lang>.resx` for `fa`, `fr`, `es`, `pt`, `zh`, `ar`, `ru`, `ja`.
      Key names are descriptive (`Reader_PasteButton`, ...). Translations are
      machine-drafted and listed for a native read before release (same caveat as
      Slice 43).
- [x] Access from XAML through a small `{x:Static}`/markup-extension helper
      (`Translate`-style) and from code through the generated `AppResources`
      class; confirm the idiomatic MAUI approach via context7 at implementation.
      No string literals left in pages (run a grep for `Text="` with letters).
- [x] Apply the language: in `App.xaml.cs` (`App` ctor, line 11) set
      `CultureInfo.DefaultThreadCurrentUICulture` and `CurrentUICulture` from the
      stored language **before** `InitializeComponent`/`CreateWindow`
      (line 23). Changing it in settings restarts navigation (recreate
      `AppShell`) rather than needing an app restart.
- [x] RTL: set `FlowDirection` from the culture's `TextInfo.IsRightToLeft` on the
      root `Window`/`Shell` content so Reader, Voice picker and the settings page
      mirror. `AndroidManifest.xml` already has `android:supportsRtl="true"`
      (line 2 of the `<application>` element). Audit for explicit left/right
      assumptions: the transport row order (back, play, forward) must keep play in
      the middle but glyph arrows should flip; `Padding="18,14,16,15"` header
      paddings; `HorizontalOptions="End"` on `VoiceNameLabel`.
- [x] Settings entry: add a language row (Reader page voice area or a small
      settings page) that lists the 9 languages in their own names; selecting one
      updates `ISettingsStore` (property from Slice 50), re-applies culture, and
      ~~offers to download that language's packs (Slices 50-52)~~ (superseded: the
      "Download voices?" prompt was removed; Slice 51 adds the per-language pack offer).
- [x] Fonts: confirm the bundled OpenSans (`MauiProgram.cs` lines 20-21) falls
      back to system fonts for Arabic, Farsi, Chinese, Japanese and Cyrillic.
      Verified on the emulator (the Language sheet renders every endonym).
- [ ] `CLAUDE.md`: a rule line for the resource layout and "no hard-coded UI
      strings in Mobile". Worded in "For CLAUDE.md" below; lifted by the index pass.

## Acceptance

- Switch to each of the 9 languages: Reader, Voice picker, settings and all
  alerts are translated; no English string remains except the product name.
- In Farsi and Arabic the whole UI mirrors, text aligns right, and the transport
  controls still work.
- The first-run language choice is applied on first launch without a restart.

## Risks / notes

- `MauiXamlInflator` is `SourceGen` (csproj line 21): check that localized
  bindings and `x:Static` work with it.
- Long German-style strings are not a target, but Russian/Portuguese are longer
  than English; check the speed-preset chips and the 44-px action buttons.
- Windows has its own localization in Slice 57.

## Decisions taken during build

- **Resources:** `Resources/Strings/AppResources.resx` (English) plus eight language files; a hand-written `AppResources` class (same folder) exposes each key as a static property named after the key (`[CallerMemberName]`), so XAML uses `{x:Static res:AppResources.Key}` (checked at compile time, works with the `SourceGen` inflator) and code uses the property. No `Translate` markup extension. `ResourceManager` falls back to English for a missing key. Strings are read once when a page is created, so a language change rebuilds the shell (`App.ShowMain()`).
- **Culture:** `UiCulture` (pure BCL, unit-tested through a linked `Compile` item) resolves stored language, then device language, then English. `App` ctor applies it before `InitializeComponent`. `Apply` sets only `DefaultThreadCurrentUICulture` (*superseded: it first also set `DefaultThreadCurrentCulture`, which would have changed number formats; see "Culture formatting" below*), never the per-flow `CurrentUICulture` (an assignment inside an async continuation is undone when it returns, and an explicit value would shadow the defaults on a later change). Before a language is stored, the first-run page already speaks the device language if it is one of the nine; picking a row re-localizes that page at once (`LanguagePage.ApplyTexts`).
- **RTL:** `FlowDirection` is set on `AppShell`, `ReaderPage`, `VoicePickerPage` and `LanguagePage`. `Thickness` is physical, so the three header paddings and the voice-picker title margin are mirrored by hand (`UiFlowDirection.Mirror`). Back arrow, and the skip-back/skip-forward glyphs, are resource keys so Farsi and Arabic carry the mirrored glyph. The play glyph is not mirrored (media convention).
- **Language row:** a row under the Voice row on the Reader page (no separate settings page). Tapping opens an action sheet of the nine endonyms (`LanguageChooser`). The sheet opens in the current flow direction. A pick stores the language, applies the culture, calls `ReaderPage.PrepareForRebuild()` (see the next item) and rebuilds the shell. The "Download voices?" prompt on a language change was removed. It does **not** offer a voice pack: `VoicePackInstaller.StartAsync` is a no-op once supertonic-3 is installed, so Slice 51 (per-language packs) adds the pack offer when the language changes.
- **Language change keeps state:** before the shell is rebuilt, `ReaderPage.PrepareForRebuild` stops the read, stores the editor text in the singleton `EditorDraft` (the new page takes it once in its constructor), detaches the singleton handlers and marks the page retired, so an in-flight scan or file read neither writes to the editor nor shows an alert. The flag is set only on this path, not in `DetachHandlers`, because `OnDisappearing` also detaches (e.g. when the voice picker opens) and a scan then must still finish.
- **Culture formatting:** `UiCulture.Apply` sets only `DefaultThreadCurrentUICulture`, so number and date formatting follow the device, not the app language.
- **A language change does not switch the voice pack or the saved voice.** Slice 51 (per-language packs) handles that.
- **Traditional Chinese devices get the Simplified UI:** only `zh` is supported, so `zh-TW`/`zh-HK` resolve to the same strings.
- **Follow-up:** the neural voice display names (`SupertonicVoiceTable`) stay untranslated. `VoicePickerPage.PhoneVoiceName` is a property read on use (the page is transient), so it is never cached in an old culture.
- **`PackStatusText` lives in `Mobile.Shared`**, which this slice may not edit. `LocalizedPackStatus` (Mobile) mirrors it with resource strings and the Reader and language pages use it. The Shared class is now unused by this app; Dino Reader still uses it. Fold the two together when Shared is free (or give Shared an injected string source).
- Endonyms (`LanguageChoices`) are data and stay untranslated; the product name `Read The Stupid Text` stays English in every language (`App_Name`, neutral file only).
- `ActionButtonLabelStyle` got `LineBreakMode=TailTruncation` so a longer Russian/Portuguese label cannot wrap the 44 px buttons.

## Needs a native-speaker read before release

All eight translations (fa, fr, es, pt, zh, ar, ru, ja) are machine-written, as in Slice 43. Check especially: the Farsi and Arabic alert and status copy, "Scan" (`Reader_ScanButton`), the "MALE"/"FEMALE" section labels for each language's grammatical gender, and Russian `Reader_FileTooLargeMessage` ("стр." abbreviation).

## Not done / not verified

- [x] Fonts: *superseded, verified on the emulator 2026-10-21: the Language sheet renders every endonym (Farsi, Chinese, Arabic, Russian, Japanese), so the system font fallback works.*
- [x] `CLAUDE.md` rule line: *superseded, worded in "For CLAUDE.md" below for the index pass.*
- [ ] Emulator pass over all nine languages: *superseded by the Farsi and Portuguese walk-through below; Arabic was not walked through.*

## Changed while building

- `LocalizedPackStatus` has a test pinning that it and Mobile.Shared's `PackStatusText` handle every `PackInstallState` identically.
- The timer label is forced left-to-right: in RTL it showed `--:--/00:00`.
- `UiFlowDirection` gained `Mirror(Thickness)` for the header paddings.
- Retired pages (after `PrepareForRebuild`) ignore late async continuations and alerts.

## From review

- **High pass:** 9 findings, 6 fixed. Mirroring (finding 4) was checked on the emulator instead of changed. The `PackStatusText` duplication (finding 7) is a follow-up, because Mobile.Shared was owned by Slice 60.
- **Medium pass:** 10 findings, 5 fixed. Skipped: a language change does not switch the voice pack (Slice 51); the culture is applied on pick, before Continue; Traditional Chinese gets the Simplified UI; the language label does not refresh without a rebuild.

## Verified on the emulator (RTST_Test, 2026-10-21)

- The Language sheet renders every endonym, including Farsi, Chinese, Arabic, Russian and Japanese, so the font step is verified.
- Switching to Farsi: the whole Reader is translated and right-to-left; the typed draft survived the rebuild; the transport row and speed chips are mirrored; header padding is correct, so the manual `Mirror` does not double-flip; the timer reads correctly after the fix.
- The voice picker in Farsi is right-to-left, with the back arrow on the right and translated section labels.
- Switching to Portuguese: labels do not wrap.

#### For CLAUDE.md (lifted 2026-10-21)

- Mobile UI strings live in `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources*.resx`, accessed through the typed `AppResources` class, never as literals in pages. A new key goes in the neutral file, in all eight language files, and in `AppResources.cs`; `LocalizedUiTests` enforces matching keys and placeholders. Set the culture with `UiCulture.Apply` (it sets the default thread UI culture only). RTL pages set `FlowDirection`, and physical paddings are mirrored with `UiFlowDirection.Mirror`. (Slice 53: docs/plan/slices/53-localized-ui-rtl.md)

#### For README.md (lifted 2026-10-21)

- The Android app's interface is available in nine languages (en, fa, fr, es, pt, zh, ar, ru, ja), right-to-left for Farsi and Arabic, and switchable from the Language row.

#### Not yet verified

- A native-speaker read of all eight translations.
- Arabic RTL on the emulator: only Farsi was walked through; Arabic shares the code path.
- Neural voice display names are not translated (follow-up).
- Fold `LocalizedPackStatus` into Mobile.Shared's `PackStatusText` (follow-up).
