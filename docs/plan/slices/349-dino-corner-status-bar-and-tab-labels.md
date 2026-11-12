# Slice 349 — Dino: grown-ups corner status bar and clipped tab labels

*Built (PR #1015, wave 2026-11-12); item 4 (remove camera/photo texts) waits for proof Dino's iOS binary has no camera/photo API reference.*

**Touches:** `src/DinoReader.App/SystemBarIcons.cs`, `src/DinoReader.App/Platforms/iOS/SystemBarIcons.ios.cs`,
`src/DinoReader.App/Platforms/Android/MainActivity.cs`, `src/DinoReader.App/CornerShell.cs`, `src/DinoReader.App/CornerTabs.cs`,
`src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.Core/StatusBarIcons.cs`, `src/DinoReader.Core/CornerLayout.cs`,
`src/DinoReader.App/Platforms/iOS/StatusBarPageViewController.cs`, `src/DinoReader.App/Platforms/iOS/Info.plist`,
`tests/DinoReader.Tests/**`, `tools/emulator/screenshot-frames.json`,
`docs/plan/slices/349-dino-corner-status-bar-and-tab-labels.md`

## Why

Seen while taking the website screenshots for Slice 347 (2026-11-09, Android emulator, Dino dev build from `eb780755`,
1080x2400 portrait). The user, 2026-11-09: "add the bugs to the plan."

1. In the grown-ups corner, the status-bar icons are white on the light bar, so the clock and battery are nearly
   invisible (Store tab, and the Arabic corner). The kids' shelf and reading page show dark icons correctly.
2. The English corner tab label "Settings" renders like "Settinas": the descender of the "g" looks clipped.
3. In Arabic, the corner's bottom tab labels are clipped.

Causes not checked yet; the files in Touches are where the status-bar colour and the corner tabs are set (found by
name, not read).

## Premises checked (2026-11-12, ground-in-code on origin/main; likely, not proven on a device)

- Icon colour: `SystemBarIcons.cs:38-39` picks dark icons only when `!inkUnderBar` and the page is light
  (`DinoReader.Core/StatusBarIcons.cs:7`); the corner is `GalleryPage`, whose `IInkUnderStatusBar`
  (`GalleryPage.xaml.cs:19`) is true whenever grown-up mode and not the rail, which forces light icons.
- Tab labels: `CornerTabs.cs:196` label `MaxLines=1`, tail truncation, inside a fixed `HeightRequest = TabRow`
  (`:175`); sizes from `DinoReader.Core/CornerLayout.cs:177,227,230`. No LineHeight.
- Info.plist: `NSCameraUsageDescription` :52, `NSPhotoLibraryUsageDescription` :58, `NSPhotoLibraryAddUsageDescription` :60;
  `NSMicrophoneUsageDescription` (:66) stays (Record). Touches narrowed: `StorePage.xaml.cs` and `SettingsPage.cs` dropped
  (not where the colour or tabs are set), so the slice runs beside Slice 346.

## Work

- [x] 1. Find why the corner sets light status-bar icons over a light background; pick the icon colour from the bar
  colour behind it on every corner tab, Android and iOS (same app, same fix on both).
- [x] 2. Give the corner tab labels room for descenders and for Arabic (and Farsi) script: no clipping at the default
  font scale and at the largest system font scale.
- [x] 3. Unit test for the icon-colour decision if it is pure logic.
- [ ] 4. (Not done 2026-11-12: the keys stay, see review notes.) Remove the camera and photo-library usage texts from `src/DinoReader.App/Platforms/iOS/Info.plist` (about lines
  50-60): nothing in Dino asks for them since Slice 278. The live privacy page (Slice 348, PR #1007) says they are being
  removed, and App Review may ask about declared but unused permissions. Check no other Info.plist key or entitlement
  depends on them.

## Built / review notes (2026-11-12, review pass 1)

- Status bar colour (item 1): the mock shows ink behind the status bar in the corner's bar mode. File `docs/design/dino-reader/Dino reader- 2026-11-08 grownup.dc.html` line 74 ("The ink header runs up behind the Dynamic Island") and line 122 ("the ink draws under the status bar and the punch-hole camera"); frames P1-P5 (lines 30-127) show the Books tab only, and the header is the corner's own on every tab, so the rule is the same on Books, Store and Settings. So the build keeps painting ink there (page `BackgroundColor` = Ink in a bar, `ContentHost` paints the sand) and the icons are light; dark icons only where the painted colour is light (the kids' shelf, a rail).
- Why the old build showed white on light (likely, not shown on a device): the old `InkUnderStatusBar` flag was already true in the corner bar (`GalleryPage.xaml.cs`, `SystemBarIcons.cs:38-39`), so the icons were light by flag, while the page itself was painted Sand (`ApplyModeChrome`, old line 709). Any strip of the status area that the header's own fill did not cover (the Store tab and the RTL/Arabic layout lay the header out differently) therefore showed sand under light icons. The page is now ink under the bar, so the flag and the painted colour agree. The cause is not proven on a device: the device checklist below samples the pixels.
- Tab row (item 2): the mock's tab item is 52 (up to 393) / 56 (412 and up) tall with the bar's padding (4 or 6) outside it (frame P1: padding 4, bar 60). The old code put that padding inside a fixed 52 row, leaving 44 for a 28 pill + 2 + the label: the clip. The tab's least height is now padding + the mock row (60/64/68/68 at default font scale, unit-tested), a larger system font scale grows it.
- Arabic and Farsi tab strings (checked in `Resources/Strings/*.resx`): at most 9 characters (الإعدادات), about 60 dp at 12 pt bold against a third of the width (125 dp at 375); at font scale 1.3 about 80 dp. They fit; the longest language is Portuguese "Configurações" (14 characters), which could ellipsize only at 375 dp and 1.3 scale (tail truncation as before, no mock rule). No ink gap between content and tab row: `GalleryRoot` rows are Auto,*,Auto with no spacing, the content host is sand in the middle row and the tab row is white.
- Item 4 (Info.plist): NOT done. `Mobile.Shared`'s `VisionKitDocumentScanner` and `MobileSharedServices.iOS.cs` compile into Dino's iOS binary and `DinoReader.App.csproj` does not trim, so App Store Connect's static check may still want the purpose strings. The three keys stay; removal waits for proof the binary has no camera/photo API reference (a TestFlight upload with the keys removed). Dated 2026-11-12.
- Skipped #6 (`reviews/pr-1015-high.md`): "Two overlapping ways to say 'bar is ink' ... the new test ... is near-tautological" - low, test style; caller skipped it.

*From review (medium pass, `reviews/pr-1015-medium.md`):*

- Applied #1-#4. One method, `GalleryPage.ApplyBarColours`, owns the page colour (ink only in grown-up bar mode), the content host colour (the page's old colour for the mode, set explicitly), the bar icons and the Windows title bar refresh; it runs on every mode change, after every chrome pass and on first appearance, not behind the size gate. #4: the mock's bar is 60 (375), 58 (393) and 62 (412, 440) plus the home-bar inset (frames P1 to P4: bottom padding is the inset itself), so `TabBar.Bottom` is 0 from 393 and the tab-height test says so; this corrects the "60/64/68/68" above.
- Skipped #5 (`reviews/pr-1015-medium.md`): "HeightRequest and MinimumHeightRequest are reset in two separate branches; a helper setting both would avoid stale heights when a tab switches between rail and bar" - style; both are set on every build, so no height goes stale.
- Skipped #6 (`reviews/pr-1015-medium.md`): "new HexOf duplicates colour-to-hex formatting, and the debug log line prints on every ForPage apply" - the log is Debug-only and the duplication is style.
- Skipped #7 (`reviews/pr-1015-medium.md`): "asserting darkIcons=False plus bg=#2B2540 in the log does not check the pixels behind the bar" - the pixel check is on the device checklist below.

*From review (medium check pass, `reviews/pr-1015-check1.md`):*

- Applied #2, #7. #2: `ApplyBarColours` paints nothing until a layout pass has built a chrome (no guessed 393x852). #7: the layout and `IInkUnderStatusBar` both read `CurrentChrome()`, the last built chrome, never `_windowSize`.
- Skipped #1 (`reviews/pr-1015-check1.md`): "Bar tab bottom padding is 0 at width >= 393, relying on safe-area inset. With inset 0 (3-button nav, non-edge-to-edge, narrow Windows bar window) label sits flush at edge/clipped. Mock's 34/24 pt is a device inset, not a constant. Use max(inset, 4) or a fixed minimum." - the mock wins: its bottom padding at 393 and wider is the home-bar inset itself (bar 58/62/62 plus the inset), so a phone with no inset matches the mock's own layout; `TabBar.Bottom` stays 0 from 393 (4 at 375).
- Skipped #3 (`reviews/pr-1015-check1.md`): "Page is ink in grown-up bar mode with ContentHost sand over it; if the tab bar is transparent, ink shows through home-bar inset; bounce/keyboard can reveal ink. Opaque tab-bar background not verified in diff." - the bar tab row is opaque white (medium #2 check); the device pixel check below covers it.
- Skipped #4 (`reviews/pr-1015-check1.md`): "Bar tab HeightRequest=-1 with only MinimumHeightRequest; large font scale or Arabic/Farsi labels can make tabs differ in height/baseline and the bar height change at runtime." - a larger font scale may grow a tab by design (the font-scale 1.3 device check covers it).
- Skipped #5 (`reviews/pr-1015-check1.md`): "LineHeight 1.4 with MaxLines=1 may offset the label on Android/iOS so min-height maths no longer matches; no device check in diff." - the device run covers it.
- Skipped #6 (`reviews/pr-1015-check1.md`): "ApplyBarColours runs three times per cycle (OnAppearing, ApplyMode, ApplyShapeOnce); repeated native writes/log lines, possible Windows title bar flicker. Skip when unchanged." - the writes are idempotent; no flicker is visible in the code.
- Skipped #8 (`reviews/pr-1015-check1.md`): "Debug-only TestHookLog line in shared static helper used by every page; Maestro flow asserts on the whole line, so wording changes break it silently." - Debug-only log.

*From review (second medium check pass, `reviews/pr-1015-check2.md`):* no blocking finding.

- Skipped #1 (`reviews/pr-1015-check2.md`): "ApplyBarColours returns early until the first layout pass, so on first appearance the bar icons and ink page colour are not applied until the first SizeChanged/ApplyShapeOnce" - deliberate (no guessed window size), the first layout pass paints; the device pixel check passed.

## Device results (emulator-5554, build 8ef1086e1ff1, 2026-11-12)

- Flow `dino-corner-status-bar-and-tabs`: PASS in English, Arabic and Farsi.
- Status-bar pixels are ink `#2B2540` with white icons on Books, Store and Settings in English and Arabic; the Arabic tab labels are whole.
- Evidence: `D:/ReadTheStupidText-worktrees/reviews/w47-device/s349/` (outside the repo).

## Device checklist (added by review pass 1)

- Corner Books, Store and Settings tabs, English, Arabic, Farsi, portrait phone: `adb shell screencap` and sample pixels in the status area (top 24 dp): they must be ink `#2B2540`, and the clock/battery light.
- Tab row height at font scale 1.0 (excluding the home-bar inset) is 60 (375) / 58 (393) / 62 (412 and 440) dp, the mock's bar before the home-bar inset is added; at `font_scale 1.3` the labels are whole (restore 1.0 after).

## Verification

- Android emulator, scripted: corner Books, Store and Settings tabs in English, Arabic and Farsi: status-bar clock readable,
  all tab labels whole. Same screens on the iOS simulator or a TestFlight build.

#### For CLAUDE.md

- Grown-up corner bar colours: `GalleryPage.ApplyBarColours` (`src/DinoReader.App/GalleryPage.xaml.cs`) alone sets the page colour (ink only in grown-up bar mode), the content host, the status-bar icons and the Windows title bar; the icon colour follows the colour actually painted behind the bar. Don't set them elsewhere. (Slice 349: docs/plan/slices/349-dino-corner-status-bar-and-tab-labels.md)
- Corner tab bar height comes from `CornerLayout.TabBarMinHeight` (`src/DinoReader.Core/CornerLayout.cs`, the mock's 60/58/62/62 dp plus the home-bar inset); labels use LineHeight 1.4 and grow with the font scale, never a fixed row. (Slice 349: docs/plan/slices/349-dino-corner-status-bar-and-tab-labels.md)
- Keep `NSCameraUsageDescription` and the photo-library keys in Dino's `Platforms/iOS/Info.plist` until a TestFlight upload without them proves the binary has no camera/photo API reference (`Mobile.Shared` scanner code compiles in). (Slice 349: docs/plan/slices/349-dino-corner-status-bar-and-tab-labels.md)

#### Not yet verified

- The `[StatusBar] ... darkIcons=False` log line was not seen in logcat (the flow passed without it).
- Farsi screenshots were not inspected (the flow passed).
- Tab labels at system font scale 1.3.
- iOS simulator / TestFlight corner screens (status bar and tabs).
- Windows head title bar (VM not run).
- Item 4: Dino's iOS binary has no camera/photo API reference, so the three Info.plist keys can go.
