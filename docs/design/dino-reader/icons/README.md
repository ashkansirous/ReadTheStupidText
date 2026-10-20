# Dino Reader app icons (4b · Book hug)

Master artwork: 108dp adaptive grid, with the glyph inside the 66dp safe zone. Background gradient #FFD9BF → #FF9FB8.

- **ios/**: `AppIcon-1024-light/dark/tinted.png` + `Contents.json`. Drop into `AppIcon.appiconset`; uses iOS 18 appearances. The dark version is the glyph on a transparent background; tinted is a white silhouette on black. `legacy/` has the pre-iOS-18 sizes.
- **android/**: `mipmap-*/ic_launcher(_round).png` (legacy), plus the adaptive `ic_launcher_background/foreground/monochrome.png` at 108dp for each density, `mipmap-anydpi-v26/ic_launcher.xml`, and `play-store-512.png`.
- **windows/**: MSIX visual assets at scale 100/125/150/200/400 (Square44 unplated, Square71/150/310, Wide310x150, StoreLogo, SplashScreen), plus `Square44x44Logo.targetsize-*_altform-unplated.png`.
- **tray/**: `TrayIconLight.ico` (dark glyph, for light taskbars) and `TrayIconDark.ico` (white glyph, for dark taskbars), each at 16/20/24/32/48 px, plus the PNGs.
