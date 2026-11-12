# Slice 349 — Dino: grown-ups corner status bar and clipped tab labels

*In progress (wave 2026-11-12), draft PR.*

**Touches:** `src/DinoReader.App/SystemBarIcons.cs`, `src/DinoReader.App/Platforms/iOS/SystemBarIcons.ios.cs`,
`src/DinoReader.App/Platforms/Android/MainActivity.cs`, `src/DinoReader.App/CornerShell.cs`, `src/DinoReader.App/CornerTabs.cs`,
`src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.Core/StatusBarIcons.cs`, `src/DinoReader.Core/CornerLayout.cs`,
`src/DinoReader.App/Platforms/iOS/StatusBarPageViewController.cs`, `src/DinoReader.App/Platforms/iOS/Info.plist`,
`tests/DinoReader.Tests/**`,
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

- [ ] 1. Find why the corner sets light status-bar icons over a light background; pick the icon colour from the bar
  colour behind it on every corner tab, Android and iOS (same app, same fix on both).
- [ ] 2. Give the corner tab labels room for descenders and for Arabic (and Farsi) script: no clipping at the default
  font scale and at the largest system font scale.
- [ ] 3. Unit test for the icon-colour decision if it is pure logic.
- [ ] 4. Remove the camera and photo-library usage texts from `src/DinoReader.App/Platforms/iOS/Info.plist` (about lines
  50-60): nothing in Dino asks for them since Slice 278. The live privacy page (Slice 348, PR #1007) says they are being
  removed, and App Review may ask about declared but unused permissions. Check no other Info.plist key or entitlement
  depends on them.

## Verification

- Android emulator, scripted: corner Books, Store and Settings tabs in English, Arabic and Farsi: status-bar clock readable,
  all tab labels whole. Same screens on the iOS simulator or a TestFlight build.
