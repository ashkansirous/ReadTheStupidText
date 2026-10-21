# Slice 73 — Edge-to-edge on Android 15+: safe areas on every screen

*Built 2026-10-21 (PR #224, merged 46e5165); not yet verified, see below.*

**Batch:** 12 (Play Console health)
**Depends on:** Slice 72 (the MAUI update changes safe-area behaviour, so check screens on the new version)
**Touches:** `src/ReadTheStupidText.Mobile/*.xaml`, `src/ReadTheStupidText.Mobile/AppShell.xaml`, `src/DinoReader.App/*.xaml`, `src/DinoReader.App/AppShell.xaml`, `src/DinoReader.App/*Page.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/SystemBarIcons.cs`, `src/DinoReader.App/Platforms/Android/MainActivity.cs`, `src/ReadTheStupidText.Mobile.Shared/**/*.xaml` (if any page lives there)
**Story issue:** —

## Goal

Play flagged release 1.0.9 with two advisory warnings (no deadline):

1. **"Edge-to-edge may not display for all users"**: apps targeting SDK 35+ draw
   edge-to-edge on Android 15+ and must handle insets.
2. **"Your app uses deprecated APIs or parameters for edge-to-edge"**: APIs deprecated
   in Android 15 (`setStatusBarColor`, `setNavigationBarColor`,
   `setDecorFitsSystemWindows`, …).

Every screen of both apps must look right on Android 15/16: nothing under the status
bar, navigation bar or display cutout, and the keyboard never covering the field being
typed in.

## Findings (2026-10-21)

- None of our code calls the deprecated APIs or touches window flags, bar colours or
  insets. Both `MainActivity`s use `@style/Maui.SplashTheme` on `MauiAppCompatActivity`,
  and there is no `styles.xml`.
- MAUI 10 already enables edge-to-edge itself (`MauiAppCompatActivity` calls
  `WindowCompat.SetDecorFitsSystemWindows(Window, false)`), and MAUI/AndroidX/Material
  reference the deprecated calls. AndroidX gates them by Android version at runtime (new
  API on 15+, old on 14 and below), but Play scans the DEX statically and flags the
  references anyway. So warning 2 comes from framework code. Slice 72's update to
  latest MAUI/AndroidX/Material is the only lever we have on it; whatever remains
  afterwards is accepted and noted here.
- MAUI 10's tool for insets is `SafeAreaEdges` (`None`, `SoftInput`, `Container`,
  `Default`, `All`) on `ContentPage`, layouts, `ContentView`, `Border` and `ScrollView`.
  Source: learn.microsoft.com/dotnet/maui/whats-new/dotnet-10 (Safe area).
- Pages: Read The Stupid Text: `ReaderPage`, `LanguagePage`, `VoicePickerPage`.
  Dino Reader: `GalleryPage`, `ReadingPage`, `AddStoryPage`, `EditStoryPage`,
  `GatePage`, `ManagePage` (plus the cast screen from Slice 65).

## Scope

- [ ] On the Slice 72 build, screenshot every page of both apps on an Android 15/16
      emulator (`RTST_Test`, API 36) via the `device-test` agent, portrait and
      landscape, with the keyboard up on pages that take text. Note each overlap.
- [x] Fix each overlap with `SafeAreaEdges` (`Container` on pages whose content must
      avoid the bars; `SoftInput` where the keyboard must push content up; `None` only
      where a background is meant to run under the bars, like Dino Reader's full-bleed
      picture mode). No `Window.SetStatusBarColor`/`SetNavigationBarColor` calls of our own
      (deprecated). Dino Reader's `MainActivity` does set the bar *icon* appearance through
      `WindowInsetsControllerCompat` (not deprecated): MAUI picks it from the system night
      mode, but only the reading screen follows night mode, so `SystemBarIcons` picks it
      from the page background actually shown.
- [x] Bar colours: let the page background draw under the bars, matching the Dino Reader
      design (`docs/design/dino-reader/`), never by setting bar colours.
- [x] Keyboard: Dino Reader's window uses `AdjustResize` (the default `AdjustPan` slid the
      whole page, header included, under the status bar) and the pages that take text
      (`AddStoryPage`, `EditStoryPage`, `PageTextPage`) use `Container, SoftInput`. Checked on
      API 36: header stays, caret and footer sit above the keyboard. RTST's `ReaderPage` needs
      nothing (its editor sits in the top half), so it keeps the default.
- [ ] Check Android 14 (API 34 image, or the oldest AVD available) still looks right,
      since the inset path differs below 15.
- [ ] After the next Play release, record which deprecated-API call sites Play still
      lists (its **Read more** page names the classes), and whether each is MAUI,
      AndroidX or Material.

## Done when

- Before/after screenshots of every page on API 36 show no content under the status
  bar, navigation bar or cutout, and no input hidden by the keyboard.
- Android 14 looks unchanged or better.
- Play's "Edge-to-edge may not display for all users" warning is gone on the next
  release. The deprecated-API warning is either gone or its remaining call sites are
  listed here as framework code.

## Version

Patch, unless the screens visibly change for users (content moved out from under the
bars counts): then `+semver: minor` on the commit that carries it.

## Built (2026-10-21)

Both apps draw edge to edge with per-page `SafeAreaEdges`: `None` on the page/root, `Container`
on the header, sheet or body that must avoid the bars (`Container, SoftInput` on the Dino pages
that take text). Dino Reader's `MainActivity` uses `AdjustResize` and sets bar icon colour
through `WindowInsetsControllerCompat` via `SystemBarIcons`; no bar colours are set. Touched
`LanguagePage`, `ReaderPage`, `VoicePickerPage` (RTST) and the Dino pages plus `App.xaml.cs`
and `MainActivity.cs`.

*From review:* two review passes ran and their findings were fixed. Findings 4 and 7 of pass 2
were not bugs and were left as they are.

*Checked on the emulator* (RTST_Test, API 36): every screen of both apps, the Dino bar icons in
day and night, and the Dino add page with the keyboard up.

*Follow-up seen on the emulator:* after toggling night mode, the Dino gallery and Manage tiles
re-lay out with odd sizes. It is not known whether this predates the slice.

#### For CLAUDE.md (lifted 2026-10-21)

- `src/ReadTheStupidText.Mobile/CLAUDE.md`: Pages draw edge to edge on Android 15+. Set
  `SafeAreaEdges="None"` on the page/root and `Container` on the header, sheet or body that must
  clear the bars; never set status/navigation bar colours in code (deprecated). (Slice 73:
  docs/plan/slices/73-edge-to-edge-safe-areas.md)
- `src/DinoReader.App/CLAUDE.md`: Same `SafeAreaEdges` pattern (`None` on page/root, `Container`
  on header, sheet or body; `Container, SoftInput` on text-entry pages). Bar icon colour comes
  from `SystemBarIcons`, which picks it from the page background shown, because MAUI follows
  system night mode but only the reading screen does. (Slice 73:
  docs/plan/slices/73-edge-to-edge-safe-areas.md)

#### Not yet verified

- Android 14 (API 34): the inset path differs below 15.
- Landscape on both apps.
- RTST `LanguagePage` on first run.
- Dino Type/paste page (`PageTextPage`) with the keyboard up.
- Play's deprecated-API warning after the next release.
