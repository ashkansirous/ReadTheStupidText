# ReadTheStupidText — Store / MSIX Visual Assets

These replace the default placeholder tile images that triggered Microsoft Store
certification failure **10.1.1.11 (On Device Tiles)**. Every asset uniquely represents the
product: the glasses mark on the brand blue→indigo gradient for tiles, and a transparent blue
mark for taskbar/Start icons.

## How to use
1. Copy everything in `store_assets/` into your project's image folder (in a default WinUI/UWP
   project this is the `Images/` folder; rename to match your manifest's existing path).
2. Make sure `Package.appxmanifest` points at these. The default mapping:

```xml
<uap:VisualElements
    DisplayName="ReadTheStupidText"
    Description="Reads selected or copied text aloud."
    BackgroundColor="#4B63EE"
    Square150x150Logo="Images\Square150x150Logo.png"
    Square44x44Logo="Images\Square44x44Logo.png">
  <uap:DefaultTile
      Square71x71Logo="Images\Square71x71Logo.png"
      Square310x310Logo="Images\Square310x310Logo.png"
      Wide310x150Logo="Images\Wide310x150Logo.png">
    <uap:ShowNameOnTiles>
      <uap:ShowOn Tile="square150x150Logo"/>
      <uap:ShowOn Tile="wide310x150Logo"/>
      <uap:ShowOn Tile="square310x310Logo"/>
    </uap:ShowNameOnTiles>
  </uap:DefaultTile>
  <uap:SplashScreen Image="Images\SplashScreen.png" BackgroundColor="#4B63EE"/>
</uap:VisualElements>
```
The manifest references the base filename (e.g. `Square150x150Logo.png`); Windows automatically
picks the matching `.scale-*` / `.targetsize-*` file at build time. You do **not** rename the
scale suffixes.

3. Rebuild the MSIX and resubmit. If you previously had a placeholder still referenced, delete
   the old files first so the package doesn't carry both.

> Tip: in Visual Studio you can also open `Package.appxmanifest` → **Visual Assets**, set the
> source to the 256px or splash asset, and let it regenerate — but these hand-tuned files give
> tighter, more legible padding than the auto-generator, so prefer dropping them in directly.

## What's included (all official scale factors: 100 / 125 / 150 / 200 / 400)
- **Square44x44Logo** — app list / Start / taskbar icon.
  - `scale-*` : plated, gradient background.
  - `targetsize-{16,24,32,48,256}_altform-unplated` : transparent background for the taskbar &
    Start list (required for a crisp unplated icon).
- **Square71x71Logo** — small tile.
- **Square150x150Logo** — medium tile *(required)*.
- **Wide310x150Logo** — wide tile.
- **Square310x310Logo** — large tile.
- **StoreLogo** — shown in the Store listing & Settings.
- **SplashScreen** (620×300) — launch splash.

## Design notes
- **Tile gradient:** `linear-gradient(135deg, #5B57E8 → #3B82F6)`. Manifest `BackgroundColor`
  is set to `#4B63EE` (the gradient midpoint) so any system-drawn plate matches.
- **Mark:** white glasses glyph, centered, with safe padding so it never crowds tile edges or
  gets clipped when Windows rounds corners.
- **Unplated icons** use the blue mark on transparency so it reads on both light and dark
  taskbars.
- Source mark: `assets/glyph.png`. Regenerate at new sizes from that file if needed.

`store_assets_preview.html` (in the parent folder) shows the full set rendered for a quick
visual check.
