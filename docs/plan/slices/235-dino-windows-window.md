### Slice 235 — Dino on Windows: a real desktop window (size, keyboard, mouse)

*Planned 2026-11-01.* The user asked for "better design on windows". **Mock drawn 2026-11-01 (Batch 46), added by the user in Claude Design, so it counts as approved**. Start after Slice 232 merges (a wide window uses its layouts) and alongside or after Slice 234 (a mid-size window uses the tablet rules).

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

**User answers (2026-11-01):** skip this slice tonight. It is parked until the user sees and approves its mock (Batch 46); status `parked: waits for the user's approval of the mock`. Parked until 2026-11-01 by the user: skipped tonight, the user added the mock on 2026-11-01, so the slice is ready to build after 232.

#### Design (Dino Reader Batch 46 mock, frames 151-160, added 2026-11-01)

Eleven frames (158 has parts a and b) in `docs/design/dino-reader/Dino Reader Batch 46 Windows.dc.html` (README index has the line of each), drawn at 1 dp = 1 px (100% scaling) with a Windows title bar. The user added the mock on 2026-11-01; it counts as approved. The mock agrees with the plan except for the open points below. Layouts are Batch 44 and 45 picked by window size, so there are no Windows-only layouts. Frames 159 (states) and 160 (keyboard map) are reference sheets, not screens. The file's intro says "Frames 151-162"; only 151-160 exist.

- **Window to layout rule (Core, unit-tested):** `WindowLayout(w, h)` on the content area (window minus the 32 px title bar): h > w and w >= 600 is tablet upright (Batch 45); w >= h and w < 960 is phone landscape rules (Batch 44) but the shelf is a grid, not a sideways row; w >= 960 is tablet landscape. Above 1600 the content is capped at 1600 and centred; the cream runs edge to edge. Opening 1100 x 760 gives tablet landscape; minimum gives phone landscape; maximised 1920 x 1080 gives tablet landscape, capped.
- **Size and memory:** opens 1100 x 760. Remembers size, place and maximised state; a saved place off every screen opens centred on the main one.
- **Shelf (151, 154):** five columns at 1100, six (the maximum) maximised, covers about 245 dp. Hover lifts a book 4 dp with shadow `0 14px 26px rgba(43,37,64,.22)` (rest `0 5px 0 rgba(43,37,64,.08)`); the pointer becomes a hand. The wheel scrolls the grid; a thin scroll bar shows only while the mouse is over the window.
- **Title bar:** 32 px, app icon and name; cream on kids' pages, paper on grown-up pages, ink on corner pages (155); on Reading it names the book ("The Tortoise and the Hare - Dino Reader"). Caption buttons are Windows' own (the red close hover is Windows').
- **Reading (152, 153):** 1100 x 728 uses the tablet landscape reading layout (127). Icon-only buttons darken one step on hover (`#ECE7FF` to `#DCD5FF`) and show a tooltip after 500 ms with the key. The wheel over the text card scrolls it; the auto-scroll resumes on the next sentence. Minimum, content 800 x 528: picture capped at 40% of the width, rail on the right, card at 22 sp, about 30 letters a line; the shelf there is four columns.
- **Corner (155) and book sheet (156):** corner at 1100 dp is Batch 44's rail with four book columns; gear hover shows a fill step and a tooltip; a hovered book gets an indigo ring (`0 0 0 3px #5B57E8`); right-click on a book opens the same sheet as the dots button. Copy "Tap" becomes "click" on Windows. The sheet is the centred card (112) with keyboard focus on Read now, a new close button (a mouse has no swipe-down), Tab order Read now, voice switch, Record, Cast, Remove, close; Esc or an outside click closes it and returns focus to the book.
- **Settings (157):** up/down move through rows with the focus ring; Enter or right arrow opens a row in the right pane; Tab goes into that pane; hover tints a row lighter than the selected one; the speed pane shows "Slower is easier for little ones - left and right arrows to change".
- **Gate (158a, 158b):** hold: a mouse press-and-hold or holding Space fills the 3 s ring, letting go empties it; caption "With the mouse, or hold the Space bar." Sum: the field has focus, number keys type, Enter checks, the six answer buttons still work; a wrong answer clears the field and shows a new sum. Shown at 700 x 520 to save room.
- **States (159):** rest, hover, pressed, keyboard focus for book, main button, icon button, icon on ink. Hover steps one shade (`#FF7A59` to `#FF8E70`, `#ECE7FF` to `#DCD5FF`); pressed takes the shadow colour; focus ring 3 dp indigo with a 3 dp white gap on paper pages, shelf yellow `#FFC24D` on ink pages, shown only after a key press. Tooltip: ink on paper, white on ink pages, 500 ms delay, key shown when there is one.
- **Keyboard map (160):** Space play/pause (on the back cover: read again); left/right previous/next page; Esc back or close a sheet (nothing on the shelf); Tab / Shift+Tab; Enter; up/down in lists; hold Space on the gate; type digits then Enter on the gate; wheel. Keys never mirror, in any language. Space and arrows do nothing while a text field has focus.
- **New strings, all 13 languages:** tooltips Back, Picture only, Picture and words, Words only, Story language, Previous page, Play / pause, Next page, Read again, Book options, Settings, Close; the gate hold caption "With the mouse, or hold the Space bar."; "Type it, then Enter"; the speed hint with arrow keys; "click" wording for "tap" on Windows. Check which already exist first.

**Open point: minimum size.** The plan says about 720 x 540; the mock says 800 x 560 (at 720 the reading card is about 240 dp wide, 18 letters a line). **Default:** 800 x 560.

**Open point: gate sum by keyboard.** The plan says "number keys"; the mock types the answer in a field and presses Enter, so mashing keys is no easier than tapping. **Default:** the mock's field plus Enter.

**Open point: width rule.** The plan says narrow = phone portrait, mid = tablet, wide = landscape. The mock splits by shape: h > w and w >= 600 is tablet upright, otherwise below 960 is phone landscape, 960 and up is tablet landscape. **Default:** the mock's `WindowLayout`; a window under 600 wide and taller than wide falls back to phone portrait.

**Open point: close button and "click" copy.** The mock adds a close button to the book sheet and changes "Tap" to "click" on Windows; the plan mentions neither. **Default:** build both on the Windows head only.

#### Work

- [x] **Design, added by the user in Claude Design 2026-11-01 (counts as approved):** Windows mock (`Dino Reader Batch 46 Windows.dc.html` in `docs/design/dino-reader/`): the window at its opening size, at minimum size and maximised, for shelf, reading, corner, Settings, gate and a sheet; hover and focus states; the title bar; a keyboard map.
- [ ] Window size, minimum and remembered position (Windows head only, `Platforms/Windows`).
- [ ] Width-driven layout picks the phone, tablet or landscape rule; unit-test the width → layout mapping in Core.
- [ ] Keyboard shortcuts and Tab order on every page; space-bar hold on the gate.
- [ ] Hover states, wheel scrolling, tooltips on every icon button, title-bar colours.
- [ ] Strings for the new tooltips in all 13 languages.

This is a desktop head, so the Android/iOS parity rule does not apply; phones and tablets get nothing new from this slice beyond the shared width rules.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (width → layout mapping).
- Scripted run in the Hyper-V VM (`tools/desktop-vm`): open the window, check its size and minimum, resize narrow → wide and screenshot each layout, read a book with space and arrows only, pass the gate with the space bar, Tab through Settings.
