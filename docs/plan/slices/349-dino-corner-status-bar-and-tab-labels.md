# Slice 349 — Dino: grown-ups corner status bar and clipped tab labels

*Built, merged as PR #1015 (699f8d06) and PR #1021 (a1516498); all items built. Not yet verified on a TestFlight upload.*

**Touches (item 4, wave 2026-11-12):** `src/ReadTheStupidText.Mobile.Shared/ReadTheStupidText.Mobile.Shared.csproj`,
`src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/MobileSharedServices.iOS.cs` (comment only), `src/DinoReader.App/Platforms/iOS/Info.plist`,
`src/DinoReader.App/CLAUDE.md`, `tests/DinoReader.Tests/**`, `docs/plan/slices/349-dino-corner-status-bar-and-tab-labels.md`

**Touches (items 1-3, merged):** `src/DinoReader.App/SystemBarIcons.cs`, `src/DinoReader.App/Platforms/iOS/SystemBarIcons.ios.cs`,
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
- [x] 4. (Built 2026-11-12, PR #1021: scanner code left out of Dino's iOS build, the three keys removed; see review notes.) Remove the camera and photo-library usage texts from `src/DinoReader.App/Platforms/iOS/Info.plist` (about lines
  50-60): nothing in Dino asks for them since Slice 278. The live privacy page (Slice 348, PR #1007) says they are being
  removed, and App Review may ask about declared but unused permissions. Check no other Info.plist key or entitlement
  depends on them.
  **Next step (user, 2026-11-12: "we removed the functionality. Right?"):** don't wait for a TestFlight probe. Stop
  compiling the scanner code (`VisionKitDocumentScanner`, its registration in `MobileSharedServices.iOS.cs`) into
  Dino's iOS build (Dino has used none of it since Slice 278; RTST keeps it), then remove the three keys in the same
  change and confirm on the next TestFlight upload. The `src/DinoReader.App/CLAUDE.md` "keep the keys" rule goes with it.

## Built / review notes (2026-11-12, review pass 1)

- Status bar colour (item 1): the mock shows ink behind the status bar in the corner's bar mode. File `docs/design/dino-reader/Dino reader- 2026-11-08 grownup.dc.html` line 74 ("The ink header runs up behind the Dynamic Island") and line 122 ("the ink draws under the status bar and the punch-hole camera"); frames P1-P5 (lines 30-127) show the Books tab only, and the header is the corner's own on every tab, so the rule is the same on Books, Store and Settings. So the build keeps painting ink there (page `BackgroundColor` = Ink in a bar, `ContentHost` paints the sand) and the icons are light; dark icons only where the painted colour is light (the kids' shelf, a rail).
- Why the old build showed white on light (likely, not shown on a device): the old `InkUnderStatusBar` flag was already true in the corner bar (`GalleryPage.xaml.cs`, `SystemBarIcons.cs:38-39`), so the icons were light by flag, while the page itself was painted Sand (`ApplyModeChrome`, old line 709). Any strip of the status area that the header's own fill did not cover (the Store tab and the RTL/Arabic layout lay the header out differently) therefore showed sand under light icons. The page is now ink under the bar, so the flag and the painted colour agree. The cause is not proven on a device: the device checklist below samples the pixels.
- Tab row (item 2): the mock's tab item is 52 (up to 393) / 56 (412 and up) tall with the bar's padding (4 or 6) outside it (frame P1: padding 4, bar 60). The old code put that padding inside a fixed 52 row, leaving 44 for a 28 pill + 2 + the label: the clip. The tab's least height is now padding + the mock row (60/64/68/68 at default font scale, unit-tested), a larger system font scale grows it.
- Arabic and Farsi tab strings (checked in `Resources/Strings/*.resx`): at most 9 characters (الإعدادات), about 60 dp at 12 pt bold against a third of the width (125 dp at 375); at font scale 1.3 about 80 dp. They fit; the longest language is Portuguese "Configurações" (14 characters), which could ellipsize only at 375 dp and 1.3 scale (tail truncation as before, no mock rule). No ink gap between content and tab row: `GalleryRoot` rows are Auto,*,Auto with no spacing, the content host is sand in the middle row and the tab row is white.
- Item 4 (Info.plist), first pass 2026-11-12: not done. `Mobile.Shared`'s `VisionKitDocumentScanner` compiled into Dino's iOS binary and `DinoReader.App.csproj` does not trim, so App Store Connect's static check might still want the purpose strings. Superseded the same day by the user's "we removed the functionality. Right?": see the next entry.
- Item 4 built (PR #1021): `Platforms\iOS\VisionKitDocumentScanner.cs` is added to the `Compile Remove` list in `Mobile.Shared.csproj` target `DropSpeechEngineSources`, which runs only when Dino sets `IncludeSpeechEngines=false` (RTST keeps the scanner). Its registration in `MobileSharedServices.iOS.cs` was already under `#if !NO_SPEECH_ENGINES`. `NSCameraUsageDescription`, `NSPhotoLibraryUsageDescription` and `NSPhotoLibraryAddUsageDescription` are removed from `src/DinoReader.App/Platforms/iOS/Info.plist`; `NSMicrophoneUsageDescription` stays (Record). New `tests/DinoReader.Tests/IosPrivacyStringsTests.cs` checks the keys are absent, the `Compile Remove` is present and the registration is guarded. Dino iOS and RTST iOS builds from Windows: 0 errors; the lean `Mobile.Shared` dll has no `VisionKitDocumentScanner` reference; 6939 Dino tests pass. No device run: the change is iOS build config only and this machine has no iOS simulator.

*From review (high pass, `reviews/pr-1021-high.md`):* findings read 5/5.

- Applied #2 (test now pins the `Compile Remove` and the `#if` guard) and #3 (target comment updated, stray blank line removed).
- Skipped #1 (`reviews/pr-1021-high.md`): "Keys removed with no check that the built binary lacks camera/photo symbols" - the user decided to remove them (2026-11-12); the check is the next TestFlight upload, listed under Not yet verified.
- Skipped #4 (`reviews/pr-1021-high.md`): "Slice file still says item 4 is not done / the keys stay" - not a code change; fixed in this docs pass.
- Skipped #5 (`reviews/pr-1021-high.md`): "Absence check matches the exact string" - low, test style.

*From review (medium pass, `reviews/pr-1021-medium.md`):* findings read 6/6.

- Applied #3 (assert before indexing) and #6 (method header comment in `MobileSharedServices.iOS.cs` corrected).
- Skipped #1 (`reviews/pr-1021-medium.md`): "purpose strings removed without proof the binary has no camera/photo API references" - same as high #1, the user's decision; TestFlight is the check.
- Skipped #2 (`reviews/pr-1021-medium.md`): "Tests read csproj/source as text only; a reintroduced camera API reference still passes" - a binary check needs an iOS build; the TestFlight upload is the check.
- Skipped #4 (`reviews/pr-1021-medium.md`): "Guard test is fragile string matching" - low.
- Skipped #5 (`reviews/pr-1021-medium.md`): "Scanner exclusion piggybacks on the speech-engines switch" - low; kept on the existing switch.

*From review (medium check pass, `reviews/pr-1021-check1.md`):* findings read 6/6, none applied in code.

- Skipped #1 (`reviews/pr-1021-check1.md`): "scanner registered while source compiled out" - low, test brittleness.
- Skipped #2 (`reviews/pr-1021-check1.md`): "harmless csproj tidy" - low, test brittleness.
- Skipped #3 (`reviews/pr-1021-check1.md`): "Scanner exclusion hung on the speech-engines switch" - deliberate placement, recorded above (medium #5).
- Skipped #4 (`reviews/pr-1021-check1.md`): "No compile-time guard against later camera/photo API use in Dino" - low; TestFlight is the check, listed under Not yet verified.
- Skipped #5 (`reviews/pr-1021-check1.md`): "entry sits mid nested list" - fixed by this edit.
- Skipped #6 (`reviews/pr-1021-check1.md`): "conflicting statements until the index pass" - the index pass sets the merged status line.
- *Changed while building:* the CI selftests needed the 7 corner shots added to `tools/emulator/screenshot-frames.json` (commit f9185754).
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

*From review (third medium check pass, `reviews/pr-1015-check3.md`):* nothing applied.

- Skipped #1 (`reviews/pr-1015-check3.md`): "ApplyBarColours returns early before first layout; page background and bar icons unset until first SizeChanged (possible flash)" - same as check2 #1, deliberate: the first layout pass paints, and the device pixel check passed.
- Skipped #2 (`reviews/pr-1015-check3.md`): "ar/fa and store/settings shots all mapped to single frame P4; contact sheet comparison misleading" - the mock shows only the Books tab, and P4 is its phone frame.
- Skipped #3 (`reviews/pr-1015-check3.md`): "a second font-scale pass overwrites same names" - the font-scale pass is not scripted.
- Skipped #4 (`reviews/pr-1015-check3.md`): "label-whole claim rests on a human reading the sheet" - the tab labels are checked by screenshot in the device run.
- Skipped #5 (`reviews/pr-1015-check3.md`): "makes bar 60pt (was 52); layout consumers assuming 52 may be off" - the mock's bar is 60 (padding + 52 row).
- Skipped #6 (`reviews/pr-1015-check3.md`): "Regular/Medium/Wide bottom padding 0 relies on system inset; three-button nav devices may sit flush" - the mock's bottom padding is the inset (recorded check1 decision, check1 #1).
- Skipped #7 (`reviews/pr-1015-check3.md`): "InkUnderStatusBar treats null chrome as not-a-rail while ApplyBarColours skips on null" - both do nothing before the first layout.
- Skipped #8 (`reviews/pr-1015-check3.md`): "expect-log asserts the exact hex line; brittle to rounding or token change" - Debug-only marker, intended.
- Skipped #9 (`reviews/pr-1015-check3.md`): "comment asserts VisionKit scanner compiled in / not trimmed, unverified in diff" - the claims were read from the csproj and Mobile.Shared in the high fix round.
- Skipped #10 (`reviews/pr-1015-check3.md`): "ink-under-bar rule expressed in three places (property, ApplyBarColours, UseDarkIcons); can drift" - one rule lives in Core, the other two call it.

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

#### For CLAUDE.md (lifted 2026-11-12)

- Grown-up corner bar colours: `GalleryPage.ApplyBarColours` (`src/DinoReader.App/GalleryPage.xaml.cs`) alone sets the page colour (ink only in grown-up bar mode), the content host, the status-bar icons and the Windows title bar; the icon colour follows the colour actually painted behind the bar. Don't set them elsewhere. (Slice 349: docs/plan/slices/349-dino-corner-status-bar-and-tab-labels.md)
- Corner tab bar height comes from `CornerLayout.TabBarMinHeight` (`src/DinoReader.Core/CornerLayout.cs`, the mock's 60/58/62/62 dp plus the home-bar inset); labels use LineHeight 1.4 and grow with the font scale, never a fixed row. (Slice 349: docs/plan/slices/349-dino-corner-status-bar-and-tab-labels.md)
- SUPERSEDED by the rule in `src/DinoReader.App/CLAUDE.md` (item 4, PR #1021: the scanner is out of Dino's iOS build and the three keys are removed). Old text: keep `NSCameraUsageDescription` and the photo-library keys in Dino's `Platforms/iOS/Info.plist` until a TestFlight upload without them proves the binary has no camera/photo API reference. (Slice 349: docs/plan/slices/349-dino-corner-status-bar-and-tab-labels.md)

#### Not yet verified

- The `[StatusBar] ... darkIcons=False` log line was not seen in logcat (the flow passed without it).
- Farsi screenshots were not inspected (the flow passed).
- Tab labels at system font scale 1.3.
- iOS simulator / TestFlight corner screens (status bar and tabs).
- Windows head title bar (VM not run).
- Item 4: the next TestFlight upload of Dino is accepted by App Store Connect with no missing purpose-string warning (ITMS-90683). If flagged, restore the three keys.
