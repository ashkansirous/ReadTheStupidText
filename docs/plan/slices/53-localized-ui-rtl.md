# Slice 53 — Android: localized UI and right-to-left layout

*In progress (wave 2026-10-21b), draft PR.*

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

- [ ] Inventory every hard-coded UI string in `Mobile/`: XAML `Text=`/`Placeholder=`
      in `ReaderPage.xaml` (16 hits), `VoicePickerPage.xaml` (6), `AppShell.xaml`
      (title), and strings in code-behind (`ReaderPage.xaml.cs` alert titles/messages
      at lines 98, 115, 119, `PickerTitle` line 92; `VoicePickerPage.xaml.cs`;
      the Slice 45 scanner messages). Delete `ScanPage` strings only if Slice 45
      has not merged yet (it removes that page).
- [ ] Add `Resources/Strings/AppResources.resx` (English, neutral) plus
      `AppResources.<lang>.resx` for `fa`, `fr`, `es`, `pt`, `zh`, `ar`, `ru`, `ja`.
      Key names are descriptive (`Reader_PasteButton`, ...). Translations are
      machine-drafted and listed for a native read before release (same caveat as
      Slice 43).
- [ ] Access from XAML through a small `{x:Static}`/markup-extension helper
      (`Translate`-style) and from code through the generated `AppResources`
      class; confirm the idiomatic MAUI approach via context7 at implementation.
      No string literals left in pages (run a grep for `Text="` with letters).
- [ ] Apply the language: in `App.xaml.cs` (`App` ctor, line 11) set
      `CultureInfo.DefaultThreadCurrentUICulture` and `CurrentUICulture` from the
      stored language **before** `InitializeComponent`/`CreateWindow`
      (line 23). Changing it in settings restarts navigation (recreate
      `AppShell`) rather than needing an app restart.
- [ ] RTL: set `FlowDirection` from the culture's `TextInfo.IsRightToLeft` on the
      root `Window`/`Shell` content so Reader, Voice picker and the settings page
      mirror. `AndroidManifest.xml` already has `android:supportsRtl="true"`
      (line 2 of the `<application>` element). Audit for explicit left/right
      assumptions: the transport row order (back, play, forward) must keep play in
      the middle but glyph arrows should flip; `Padding="18,14,16,15"` header
      paddings; `HorizontalOptions="End"` on `VoiceNameLabel`.
- [ ] Settings entry: add a language row (Reader page voice area or a small
      settings page) that lists the 9 languages in their own names; selecting one
      updates `ISettingsStore` (property from Slice 50), re-applies culture, and
      offers to download that language's packs (Slices 50-52).
- [ ] Fonts: confirm the bundled OpenSans (`MauiProgram.cs` lines 20-21) falls
      back to system fonts for Arabic, Farsi, Chinese, Japanese and Cyrillic.
- [ ] `CLAUDE.md`: a rule line for the resource layout and "no hard-coded UI
      strings in Mobile".

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
