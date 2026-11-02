### Slice 235 — Dino on Windows: a real desktop window (size, keyboard, mouse)

*Part A built 2026-11-02 (window and pure rules; five Dino VM checks pass), merged as PR #740 (0d135658); part B still planned.* *Planned 2026-11-01.* The user asked for "better design on windows". **Mock drawn 2026-11-01 (Batch 46), added by the user in Claude Design, so it counts as approved**. Start after Slice 232 merges (a wide window uses its layouts) and alongside or after Slice 234 (a mid-size window uses the tablet rules).

**Touches:** `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/Platforms/Windows/**`, `src/DinoReader.Core/*Layout*.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/GrownUpHeader.cs`, `src/DinoReader.App/*Page*.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `tools/desktop-vm/**`, `docs/design/dino-reader/**`, `docs/plan/slices/235-dino-windows-window.md`

#### Parts (coordinator, claim 2026-11-02, night 2026-11-01-2355 wave 5)

Slice 234 parts B and C edit most page files tonight, so this slice is split. **A (this claim):** the window itself and the pure width rule, nothing in the pages. **B (later, after Slice 234 is built):** wire `WindowLayout` into the pages, keyboard map, Tab order and focus ring, hover states, wheel, tooltips, per-page title-bar colours, the gate's Space hold and typed sum, the sheet's close button and "click" copy, strings in all 13 languages.

**Touches, part A:** `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/Platforms/Windows/**`, `src/DinoReader.Core/WindowLayout.cs` (new), `src/DinoReader.Core/WindowPlacement.cs` (new), `tests/DinoReader.Tests/WindowLayoutTests.cs` (new), `tests/DinoReader.Tests/WindowPlacementTests.cs` (new), `tools/desktop-vm/**` (only a new Dino window check, if the harness needs one), `docs/plan/slices/235-dino-windows-window.md`. Read-only: every page file and every other `src/DinoReader.Core/*Layout*.cs` (Slice 234 parts B and C run in parallel).

**Part A design note (coordinator, claim):** Windows head only; Android and iOS get nothing but the unused pure rules. Defaults from the open points: minimum 800 x 560, the mock's `WindowLayout`. (1) Pure Core `WindowLayout.For(w, h)` on the content area (window minus the 32 px title bar) returns a closed enum: tablet upright (h > w and w >= 600), phone landscape with a grid shelf (w >= h and w < 960), tablet landscape (w >= 960), phone portrait (narrower than 600 and taller than wide); plus `ContentWidth(w)` = min(w, 1600) for centring. Unit-test the mock's cases (1100 x 760, the minimum, 1920 x 1080 maximised, the boundaries). Not called by any page yet. (2) Pure Core `WindowPlacement`: a saved rect (x, y, w, h, maximised) plus the displays' work areas gives the rect to open; no saved value gives 1100 x 760 centred on the main display; a saved rect off every display gives the saved size (clamped to the minimum and the main display) centred on the main one. Unit-tested. (3) Windows head (`Platforms/Windows`, wired from `App.xaml.cs`'s `CreateWindow` under a Windows-only guard): open with that rect, enforce the 800 x 560 minimum (the WinUI `AppWindow`/`OverlappedPresenter` way; query context7 first), save size, place and maximised state to `Preferences` under named-constant keys when the window closes or moves/resizes (debounced, never on every pixel), restore maximised state. Title bar: 32 px, app icon and name, cream (the kids' page colour) for now; per-page colours come in part B. Device: the Hyper-V VM (`tools/desktop-vm`, `-App auto`): open, check the size and minimum, resize, close and reopen to see the size and place kept.

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
- [x] Window size, minimum and remembered position (Windows head only, `Platforms/Windows`). Part A, built 2026-11-02.
- [ ] Width-driven layout picks the phone, tablet or landscape rule; unit-test the width → layout mapping in Core.
- [ ] Keyboard shortcuts and Tab order on every page; space-bar hold on the gate.
- [ ] Hover states, wheel scrolling, tooltips on every icon button, title-bar colours.
- [ ] Strings for the new tooltips in all 13 languages.

This is a desktop head, so the Android/iOS parity rule does not apply; phones and tablets get nothing new from this slice beyond the shared width rules.

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (width → layout mapping).
- Scripted run in the Hyper-V VM (`tools/desktop-vm`): open the window, check its size and minimum, resize narrow → wide and screenshot each layout, read a book with space and arrows only, pass the gate with the space bar, Tab through Settings.

#### Part A: built (2026-11-02)

- **Core (pure, unit-tested, not wired into any page; that is part B):** `WindowLayout` (`WindowShape` enum, `For(w, h)`, `ContentWidth` capped at 1600, minimum 800 x 560, default 1100 x 760, 32 px title bar). `WindowPlacement` (`SavedWindow` text round-trip in invariant culture; `Resolve`: no saved value gives the default centred, a saved rect is clamped onto the display holding most of its title bar, a rect off every display is centred on the main one, values beyond +-100000 are rejected). `WindowMinimum.Enforce`.
- **Windows head:** `Platforms/Windows/DinoWindowFrame.cs`, attached from `App.xaml.cs` under `#if WINDOWS` on `Window.Created`, with `HandlerChanged` as fallback, applied once on success. Opens at the resolved rect. The 800 x 560 minimum is scaled by the target display's DPI (the unit of `PreferredMinimum*` is undocumented) and also enforced by construction on `Changed`, because a UI Automation resize went below the presenter minimum on the VM. Saves size, place and maximised (the last non-minimised state) to Preferences key `dino.window.placement`, debounced 500 ms and on closing. Cream title bar (Windows' own bar recoloured; per-page colours are part B). `WindowTrace.cs` appends to `window-trace.log` in the app data folder, trimmed to 200 lines once per run, and the VM check reads it back on failure.
- **Checks:** new `tools/desktop-vm/checks/dino-window.ps1` (expectations clamped to the VM's 1024 x 768 work area); `dino-settings.ps1` now handles Settings' two panes on a landscape window and retries the Done click.
- Tests: 2146 pass; Windows head and Android builds pass.

Changed while building:

- **Bug found only by the VM:** iterating `DisplayArea.FindAll()` threw `InvalidCastException` (microsoft-ui-xaml #6454), so the frame never applied. Fixed with an index loop plus a `Primary` / `GetFromWindowId` fallback. It was found only after adding the Release trace; the first two VM runs could not tell whether the frame ran (the Release VM build shows no `Debug.WriteLine`).
- VM runs (`s235a-dino-vm-run*.log`): runs 1-2 failed the window minimum (frame not applied); run 4's trace showed the cast exception; run 5 applied the frame but the check expected 1100 x 760 on a 1024 x 768 screen; run 6 passed all five Dino checks (package ad4b9ca41578): the minimum held 800 x 560 after asking for 400 x 300, and the window reopened at 944 x 650 at 60,40 as closed.

From review (files in `D:/ReadTheStupidText-worktrees/reviews/`, deleted later):

- `s235a-high.md` (10 findings): applied 1-5 and 7-9. 6 (maximise runs before first activation; may flash or be re-shown normal) left to the VM, not run. 10 (check gaps: maximised round trip, 150% scaling, minimised close, off-screen saved place) not fixed; listed under Not yet verified.
- `s235a-review-medium.md` (7): applied 1-3. Skipped 4 (needs the VM; a maximised window's restored bounds). Skipped 5 (a resize within 500 ms of a kill is lost; acceptable). Skipped 6 (an exactly square window is phone landscape by the mock's rule). Skipped 7 (nit: rebuild of the work-area list).
- `s235a-review-check-medium.md` (5): applied 1 (trace write cost), 2 (`applied` set only after success), 5. Skipped 3 (unconfirmed, not reproduced; VM run 5 shows no sign). Skipped 4 (low; one visible jump after a DPI change).
- `s235a-review-check2-medium.md` (5): none applied. 2 cannot happen (the VM starts clean every run). 1, 3, 4, 5 skipped as low (minimum scale refreshed only on a position change; check units at non-100% scale; half-framed window if `Apply` throws; trim lost on a transient IO failure).

#### Not yet verified

- Opening at 1100 x 760 on a screen larger than 1024 x 768 (the VM clamps it).
- A maximised window closed and reopened stays maximised (high finding 6).
- 150% scaling and the unit of `PreferredMinimumWidth/Height`.
- Two monitors, and an unplugged display (saved place on a missing monitor).
- Title bar height and colour by eye.
- A window clamped to a small screen and saved as maximised reopens maximised (seen on the VM; whether that is by design is not decided).

#### For tools/desktop-vm/CLAUDE.md (lifted 2026-11-02)

- The VM screen is 1024 x 768, so window-size checks clamp their expectations to its work area.
- The Release VM build shows no `Debug.WriteLine`, so a Windows-head check needs a file trace it can read back (`window-trace.log`). (Slice 235: docs/plan/slices/235-dino-windows-window.md)
