### Slice 235 — Dino on Windows: a real desktop window (size, keyboard, mouse)

*Planned 2026-11-01.* The user asked for "better design on windows". **Needs design by Claude Code** (an HTML mock, approved by the user, before any code). Start after Slice 232 merges (a wide window uses its layouts) and alongside or after Slice 234 (a mid-size window uses the tablet rules).

**Touches:** `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/Platforms/Windows/**`, `src/DinoReader.Core/*Layout*.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/GrownUpHeader.cs`, `src/DinoReader.App/*Page*.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `tools/desktop-vm/**`, `docs/design/dino-reader/**`, `docs/plan/slices/235-dino-windows-window.md`

#### Why (read from code on origin/main, 2026-11-01; not run in the VM)

The Windows head (Slice 67) opens a default WinUI window: `new Window(navigation)` in `App.xaml.cs:51-56` sets no size or minimum, so it can be resized to anything. Only the reading screen reacts to width, through `ReadingLayoutRules` (width vs height). There are no keyboard shortcuts (no space or arrows), no hover states, no scroll-wheel or right-click handling (no `PointerGestureRecognizer`, `KeyboardAccelerator`, `ContextFlyout` or `PointerEntered` in `src/DinoReader.App` or Mobile.Shared), and no title-bar styling (`Platforms/Windows/App.xaml.cs` only reads `UISettings` colours). Tooltips exist only on a few shelf buttons (`GalleryPage.xaml:74, 85`; `StoryLanguageButton.cs:234`). The gate's 3-second hold (`GatePage.xaml:39-40`) works with a mouse press-and-hold. Slice 205 had to fix keyboard access to one Settings row, so keyboard reach is a known gap.

#### Decisions locked with the user (2026-11-01, RefineScope)

- **Dino Reader only** (the RTST tray app is not in this slice).
- **Window:** opens at about 1100 × 760, minimum about 720 × 540 (exact values from the mock); remembers its last size and place.
- **Layout follows the window width** with the same rules as phones and tablets (Slices 232 and 234): narrow = phone portrait, mid = tablet, wide = landscape.
- **Keyboard:** space plays or pauses, left/right arrows turn pages, Esc goes back, Tab reaches every button in a sensible order with a visible focus ring.
- **Mouse:** hover states on books and buttons; the mouse wheel scrolls the shelf and lists.
- **Tooltips** on every icon-only button. **Title bar** in the app's colours.
- **Gate:** keep the 3-second hold with the mouse, and holding the space bar also counts. The sum step works with the keyboard (number keys).
- **Light only, never mirrored** in any language (user, 2026-11-01; Slice 96).

#### Work

- [ ] **Design (Claude Code):** Windows mock (e.g. `Dino Reader Batch 46 Windows.dc.html` in `docs/design/dino-reader/`): the window at its opening size, at minimum size and maximised, for shelf, reading, corner, Settings, gate and a sheet; hover and focus states; the title bar; a keyboard map. Publish as a private page; record the user's answer here.
- [ ] Window size, minimum and remembered position (Windows head only, `Platforms/Windows`).
- [ ] Width-driven layout picks the phone, tablet or landscape rule; unit-test the width → layout mapping in Core.
- [ ] Keyboard shortcuts and Tab order on every page; space-bar hold on the gate.
- [ ] Hover states, wheel scrolling, tooltips on every icon button, title-bar colours.
- [ ] Strings for the new tooltips in all 13 languages.

This is a desktop head, so the Android/iOS parity rule does not apply; phones and tablets get nothing new from this slice beyond the shared width rules.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (width → layout mapping).
- Scripted run in the Hyper-V VM (`tools/desktop-vm`): open the window, check its size and minimum, resize narrow → wide and screenshot each layout, read a book with space and arrows only, pass the gate with the space bar, Tab through Settings.
