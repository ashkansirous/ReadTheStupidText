### Slice 235 — Dino on Windows: a real desktop window (size, keyboard, mouse)

*Part B built 2026-11-02 (night 2026-11-01-2355 wave 6), merged as PR #745 (d8ba0b6a), all six Dino VM checks pass; part C (WindowLayout wiring into pages, hover, wheel, focus-ring styling and focus return, Settings arrow keys, right-click, shelf column counts, speed hint string) still planned.* *Part A built 2026-11-02 (window and pure rules; five Dino VM checks pass), merged as PR #740 (0d135658).* *Planned 2026-11-01.* The user asked for "better design on windows". **Mock drawn 2026-11-01 (Batch 46), added by the user in Claude Design, so it counts as approved**. Start after Slice 232 merges (a wide window uses its layouts) and alongside or after Slice 234 (a mid-size window uses the tablet rules).

**Touches:** `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/Platforms/Windows/**`, `src/DinoReader.Core/*Layout*.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/GrownUpHeader.cs`, `src/DinoReader.App/*Page*.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `tools/desktop-vm/**`, `docs/design/dino-reader/**`, `docs/plan/slices/235-dino-windows-window.md`

#### Parts (coordinator, claim 2026-11-02, night 2026-11-01-2355 wave 5)

Slice 234 parts B and C edit most page files tonight, so this slice is split. **A (this claim):** the window itself and the pure width rule, nothing in the pages. **B (later, after Slice 234 is built):** wire `WindowLayout` into the pages, keyboard map, Tab order and focus ring, hover states, wheel, tooltips, per-page title-bar colours, the gate's Space hold and typed sum, the sheet's close button and "click" copy, strings in all 13 languages.

**Touches, part A:** `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/Platforms/Windows/**`, `src/DinoReader.Core/WindowLayout.cs` (new), `src/DinoReader.Core/WindowPlacement.cs` (new), `tests/DinoReader.Tests/WindowLayoutTests.cs` (new), `tests/DinoReader.Tests/WindowPlacementTests.cs` (new), `tools/desktop-vm/**` (only a new Dino window check, if the harness needs one), `docs/plan/slices/235-dino-windows-window.md`. Read-only: every page file and every other `src/DinoReader.Core/*Layout*.cs` (Slice 234 parts B and C run in parallel).

**Part A design note (coordinator, claim):** Windows head only; Android and iOS get nothing but the unused pure rules. Defaults from the open points: minimum 800 x 560, the mock's `WindowLayout`. (1) Pure Core `WindowLayout.For(w, h)` on the content area (window minus the 32 px title bar) returns a closed enum: tablet upright (h > w and w >= 600), phone landscape with a grid shelf (w >= h and w < 960), tablet landscape (w >= 960), phone portrait (narrower than 600 and taller than wide); plus `ContentWidth(w)` = min(w, 1600) for centring. Unit-test the mock's cases (1100 x 760, the minimum, 1920 x 1080 maximised, the boundaries). Not called by any page yet. (2) Pure Core `WindowPlacement`: a saved rect (x, y, w, h, maximised) plus the displays' work areas gives the rect to open; no saved value gives 1100 x 760 centred on the main display; a saved rect off every display gives the saved size (clamped to the minimum and the main display) centred on the main one. Unit-tested. (3) Windows head (`Platforms/Windows`, wired from `App.xaml.cs`'s `CreateWindow` under a Windows-only guard): open with that rect, enforce the 800 x 560 minimum (the WinUI `AppWindow`/`OverlappedPresenter` way; query context7 first), save size, place and maximised state to `Preferences` under named-constant keys when the window closes or moves/resizes (debounced, never on every pixel), restore maximised state. Title bar: 32 px, app icon and name, cream (the kids' page colour) for now; per-page colours come in part B. Device: the Hyper-V VM (`tools/desktop-vm`, `-App auto`): open, check the size and minimum, resize, close and reopen to see the size and place kept.

#### Part B claim (coordinator, 2026-11-02, night 2026-11-01-2355 wave 6)

Part B as first written (wire `WindowLayout` into every page, plus all input work) is too big for one claim: the premise check found no single layout entry point (each page calls its own rule: `GalleryPage.xaml.cs:393`, `GatePage.xaml.cs:76`, `IntroWizardPage.cs:170`, `FirstRunPage.xaml.cs:185`, `BookSheet.cs:121`, `BackupPage.xaml.cs:68`, `CastPage.cs:205`, `PageTextPage.cs:97`, `ReadingLayoutRules`), and changing those Core rules would touch phones and tablets. So part B is split again:

- **B (this claim): keyboard, gate, tooltips, title-bar colours, strings.** Windows head only.
- **C (later, stays planned):** `WindowLayout` wired into the pages (a shape seam in Core), hover states, wheel and scroll-bar behaviour, the visible focus ring styling (159), Settings up/down/Enter/right-arrow pane navigation (157), right-click on a book, the shelf's column counts.

**Touches, part B:** `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/Platforms/Windows/**`, `src/DinoReader.Core/DinoKeyMap.cs` (new), `src/DinoReader.Core/TitleBarTone.cs` (new), `src/DinoReader.Core/BackInFlight.cs` (new, added at medium review), `src/DinoReader.App/DinoKeyTarget.cs` (new), `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.App/GrownUpHeader.cs`, `src/DinoReader.App/*Page*.cs`, `src/DinoReader.App/*Page.xaml.cs`, `src/DinoReader.App/StoryLanguageButton.cs`, `src/DinoReader.App/TransportRow.cs`, `src/DinoReader.App/ModeSwitch.cs`, `src/DinoReader.App/TileOverlay.xaml`, `src/DinoReader.App/TileOverlay.xaml.cs` (last four added at build, 2026-11-02: icon-button tooltips; no other open PR touches them), `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `tools/desktop-vm/**`, `docs/plan/slices/235-dino-windows-window.md`. Read-only: every `src/DinoReader.Core/*Layout*.cs`.

**Part B design note (coordinator, claim):**

1. **Key map, pure Core, unit-tested:** `DinoKeyMap.Resolve(key, screen, textFieldFocused, keyDown)` returns a closed `DinoKeyAction` enum (None, PlayPause, PreviousPage, NextPage, Back, CloseSheet, ReadAgain, GateHoldStart, GateHoldEnd, GateDigit, GateErase, GateSubmit). `screen` is a closed enum (Shelf, Reading, ReadingBackCover, Gate, Sheet, Other). Rules from frame 160: Space play/pause (back cover: read again); Left/Right previous/next; Esc back or close a sheet, nothing on the shelf; on the gate Space down/up starts/ends the hold, digits type, Backspace erases, Enter submits. Space and arrows do nothing while a text field has focus. Keys never mirror (no right-to-left swap) in any language. Key identity is a Core enum (`DinoKey`), mapped from `Windows.System.VirtualKey` only in `Platforms/Windows`.
2. **One hook, Windows only:** `Platforms/Windows` attaches `KeyDown`/`KeyUp` (or `PreviewKeyDown`) to the window's root content once, from the same place part A attaches `DinoWindowFrame`. It maps the key, asks the current page (the top of the navigation stack or the open sheet) for its `DinoKeyTarget` screen, calls `DinoKeyMap.Resolve`, and hands the action to that page. `DinoKeyTarget` (App, shared code, no platform types) is a small interface: `Screen` and `bool Handle(DinoKeyAction)`. Android and iOS never call it (no hook there), so phones and tablets behave as today. Key repeat on a held Space must not restart the gate hold (ignore repeats).
3. **Gate:** the hold ring gets the same start/end calls as the mouse (`OnHoldStart`/`OnHoldEnd`), so letting go empties it. The sum step keeps the on-screen number pad (premise check: answered by Buttons into `_answer`, `GatePage.xaml.cs:55`); typed digits feed the same `_answer` as a pad press, Backspace removes one, Enter checks. A wrong answer behaves as a wrong pad answer does today. This is the open point's "type it, then Enter" without a separate text field (decided here: the pad already shows the typed answer; a second field would show it twice). Captions on Windows only: "With the mouse, or hold the Space bar." and "Type it, then Enter".
4. **Book sheet:** it already has a labelled close button (`BookSheet.cs:37, 91`; the slice's "new close button" was wrong). Part B adds only Esc to close and focus on Read now when it opens, and returns focus to the book on close where MAUI allows it.
5. **Tooltips:** `ToolTipProperties.Text` on every icon-only button of shelf, reading, sheet and grown-ups header, set only on Windows (a Windows-only guard), with the key in brackets where there is one ("Next page (Right arrow)"). Reuse existing strings (`Common_Back`, `Common_Close`, `Gallery_BookOptions`, `Story_Language`, `Reading_Previous`, `Reading_Next`, `Reading_Play`, `Reading_Mode_*`, `Settings_Title`); add only the missing ones (Read again, the gate captions, the key names, the speed hint) in all 13 languages.
6. **Title bar per page:** pure Core `TitleBarTone.For(screen kind)` returns Cream (kids' pages), Paper (grown-up pages) or Ink (corner pages); the Windows head reapplies `AppWindowTitleBar` colours on each navigation (one handler on the window's page changes) and, on Reading, sets the window title to "<book title> - Dino Reader". Colours from the existing `DinoTokens`.
7. **"click" copy:** the sheet and any "Tap ..." hint read "Click ..." on Windows only, from new strings, never by string replacement.

Device: Hyper-V VM (`tools/desktop-vm`, `-App auto`): open a book and read with Space and arrows only, Esc back to the shelf; pass the gate by holding Space and typing the sum then Enter; open the book sheet and close it with Esc; title bar colour on shelf vs a grown-ups page (read the title bar colour from `AppWindow` via the trace file if UI Automation can't). Unit tests for `DinoKeyMap` and `TitleBarTone`.

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

#### Part B: built (2026-11-02, night 2026-11-01-2355 wave 6)

PR #745. All six Dino VM checks pass (run 5, package `c0458ed2efe2`).

- **Core (pure, unit-tested):** `DinoKeyMap.Resolve` (+ `DinoKey`, `DinoScreen`, `DinoKeyAction`, `DigitOf`), `TitleBarTones` (+ `TitleBarTone`, `TitleBarPage`, `WindowTitle`), `BackInFlight` (one Esc-back slot; added at medium review).
- **App (shared, no platform types):** `DinoKeyTarget.cs` holds `IDinoKeyTarget` (`Screen`, `Handle(action, key)`), `DinoKeyTargets` (finds the top page, modal first, routes a key, owns the `BackInFlight` slot) and `DesktopTips` (tooltips with the key in brackets, a no-op off Windows). Implemented by `GatePage` (Space hold start/end, digits, Backspace, Enter, Esc closes), `ReadingPage` (Space, Left, Right, Esc; Space on the silent back cover reads from the top, tooltip "Read again") and `GalleryPage` (Esc closes the book sheet; nothing on the shelf). Esc and the Back button share `ReadingPage.LeaveBookAsync`. Tooltips also on the transport previous/play/next buttons (play follows Play/Pause), the three mode cells (no key) and the "Book options" dots (`TransportRow.cs`, `ModeSwitch.cs`, `TileOverlay.xaml(.cs)`, added to Touches at build).
- **Windows head:** `DinoKeyboard.cs` (PreviewKeyDown/Up on the window content; a press is one action, used until its key-up; ignores Ctrl/Alt combos and Shift on digits; text field focus detected through `FocusManager`; writes `Dino keyboard: <Key> used|not used (<page>, <screen>)` to the trace), `DinoTitleBar.cs` (tone per page kind, modals included via `Window.ModalPushed/ModalPopped`; the reading screen's window title), `DinoWindowFrame.ApplyTone` replaces the fixed cream.
- **Strings (14 language files):** gate captions, `Key_*` names, `Reading_ReadAgain`, and seven "...Click" twins (corner line, six pack-status hints) that `AppResources.Get` returns when `UseClickWording` is on (set only by the Windows head).
- **Checks:** new `tools/desktop-vm/checks/dino-keyboard.ps1` (registered in `Invoke-DesktopTest.ps1` before `dino-window`). It holds Space 1.2 s (the sum must not appear), then 3.6 s with repeats (it must), types a wrong sum then Enter (gate stays), then the right one (grown-up mode), checks the ink title bar tone, opens a book sheet and closes it with Esc, opens it again and presses Enter on Read now (the book opens, window title contains " - "), presses Space, Right and Left on the reading page, Esc back to the kids' shelf, cream tone again, and Esc on Settings. After each key it polls `window-trace.log` (up to 10 s) for that key's "used" line; Left and Right are sent as extended keys. Debug-only log lines: `[Keyboard] gate hold started/ended by the Space bar`, `[Keyboard] reading <action>`.
- **Work boxes (none ticked fully):** *Keyboard shortcuts and Tab order*: done are the key map, the gate Space hold, the reading keys, Esc everywhere, the sheet's Enter/Space and the gate's tab stops; left for part C are Tab order and focus-ring styling on every page, Settings arrow keys and focus return after the sheet closes. *Hover/tooltips/title bar*: tooltips on every icon button and title-bar colours are done; hover states, wheel scrolling and right-click are part C. *Strings*: the tooltip, gate, key-name and click strings are in all languages; the speed hint string is part C.

Behaviour chosen: Esc on the gate closes it (like its Back button). Esc on a page that is not a key target (Settings and the other grown-up pages) calls `SendBackButtonPressed()` first and pops only when nothing handled it (`DinoKeyTargets.PopOne`); the shelf does nothing. The grown-up header's back tooltip shows no key. On the open book sheet Enter or Space opens Read now unless a button, switch or checkbox inside the sheet has keyboard focus (`DinoKeyAction.SheetActivate`, `BookSheet.ActivateFocused`). On Windows the gate's pad keys are not tab stops and a pad click unfocuses, so Enter and Backspace act on the sum unless Back to story or Continue has focus. The "3 seconds" hold instruction stays on every platform; on Windows a Space-bar caption is added.

Changed while building:

- Designs taken by construction after repeat findings: Esc is one press, one action (used until key-up) plus the Core `BackInFlight` slot; gate pad keys are not tab stops; the title-bar tone follows the top page, modals included; sheet Enter/Space open Read now unless a sheet button has focus; Esc and Back share `ReadingPage.LeaveBookAsync`.
- `Find-UiaByName` (`tools/desktop-vm/checks/_lib.ps1`): my first harness fix skipped only the ToolTip element, not its child `Text` of the same name (VM run 2). It now skips a ToolTip and anything whose UIA parent is one, prefers an exact name over a partial one, and ignores an element that vanished mid-read. The tooltip hit was the cause of the `dino-page-turn` failure in runs 1-2 (the taps hit the popup).
- Sheet Enter: "Read now" is a tapped Border WinUI cannot focus, so the first design (focus the row, activate while it has focus) never held; replaced by the rule above.
- My "Esc dropped while busy" guess was wrong: Read now leaves the corner by design (`GalleryPage.LeaveAndReadAsync` calls `_mode.Leave()`), so Esc popped to the kids' shelf and the check expected the corner. The check was fixed, not the app.
- `dino-keyboard` waited 500 ms per key, but Right took 2.2-2.6 s after Space on the VM; the check now polls the trace. Run 3 gave no cause, so the only app change was one trace line per mapped key.

VM runs (logs `D:/ReadTheStupidText-worktrees/reviews/s235b-dino-vm-run*.log`, deleted later):

- Run 1 (`1e5092bff5fd`): `dino-keyboard` failed at the sheet Enter; `dino-page-turn` failed (tooltip hit). Run 2 (`4a96ac0cabef`): sheet Enter worked; `dino-page-turn` failed again (tooltip child); `dino-keyboard` failed at Esc on the reading page (wrong expected page, above).
- Run 3 (`bf0e4023e8df`): 5 of 6; `dino-keyboard` failed at Right (the 500 ms wait). Run 4 (`c0458ed2efe2`): the new trace showed Right used 2.6 s after Space; the check was changed to poll.
- Run 5 (`c0458ed2efe2`, cached package): all 6 Dino checks PASS (launch, open-book, page-turn, settings, keyboard, window), about 3 min. Key waits: gate Space 2644 ms, reading Space 448, Right 2166, Left 443. The delay follows the first key after a read starts.

From review (files in `D:/ReadTheStupidText-worktrees/reviews/`, deleted later):

- `s235b-high.md` (8): applied 1 (Esc via `SendBackButtonPressed`, held Esc swallowed), 2 (gate hold ends on deactivation or focus loss), 3 (Ctrl/Alt/Shift-digit not mapped), 4 (Esc works while the book loads), 5 (Read again stops a running read first). Skipped 6 (a tile is an immutable record; tooltip and screen-reader text refresh on the same binding change), 7 (focus return after the sheet closes: part C), 8 (the per-key trace write stays; the VM check reads it).
- `s235b-medium.md` (10): applied 1 and 3 (a press is one action; `BackInFlight`), 4 and 5 (`DinoTitleBar.Watch`, modal push/pop), 8 (`StartKeyHold` false once closing). Not a bug: 9 (Read again follows the Play path; `ReadingSession.StartAtAsync` stops a running read). Skipped 2 (one window for the app's life; its "any held key" half is fixed with 1), 6 (as high 6), 7 (the test guards the set), 10 (focus return: part C).
- `s235b-check-medium.md` (10): applied 4 (gate Enter/Backspace act on the sum only when no button has focus) and 6 (back-cover tooltip "Read again"). Skipped 1 (the 1 s back slot is the guard against double pops; a second Esc within 1 s on a self-handling page is acceptable), 2 (the Windows build compiles it, so it is public), 3, 5 (on-screen pad and numpad still work on AZERTY), 7, 8, 9, 10 (low).
- `s235b-check2-medium.md` (3): applied 1 (by construction: pad keys not tab stops, a pad click unfocuses) and 2 ("3 seconds" instruction kept). 3: no bug found.
- `s235b-check3-medium.md`: no findings. `s235b-check4-medium.md` (5): none applied; 1, 2, 4, 5 low; 3 (`ActivateFocused` runs `ReadNowAsync` with `_ =`, so an exception goes unobserved but cannot crash the app) is a part C follow-up.
- `s235b-check5-medium.md` (8): applied 3 (key actions do nothing while `LeaveBookAsync` holds the busy guard) and 6 (`Find-UiaByName` skips a vanished element). Skipped 1, 2, 7, 8 (low); 4, 5 (callers re-read: none relied on a partial match beating an exact one).
- `s235b-check6-medium.md`, `s235b-check7-medium.md`: no new findings.

#### Not yet verified

- Part B on a real PC and by eye: whether `PreviewKeyDown` gets keys when nothing has focus yet (the check focuses the window first), the Space hold with real key repeats, title bar colours and height, tooltips on a hovering mouse.
- Why Right takes about 2 s after a read starts on the VM (speech start busy on the UI thread?), and whether a real PC has the same delay.
- Esc on the language page / cast picker through the new back path; held Esc on Settings/Language/Cast/Record pops exactly one page; the gate hold ending on Alt-Tab; title bar colour after the intro and the gate close.
- Typing the sum on AZERTY / AltGr layouts (on-screen pad and numpad only); Tab then Enter on "Back to story".
- The title bar back to cream after Esc from the book is shown by the check's pass but not read from the trace.
- Opening at 1100 x 760 on a screen larger than 1024 x 768 (the VM clamps it).
- A maximised window closed and reopened stays maximised (part A high finding 6).
- 150% scaling and the unit of `PreferredMinimumWidth/Height`.
- Two monitors, and an unplugged display (saved place on a missing monitor).
- A window clamped to a small screen and saved as maximised reopens maximised (seen on the VM; whether that is by design is not decided).

#### For tools/desktop-vm/CLAUDE.md (lifted 2026-11-02)

- The VM screen is 1024 x 768, so window-size checks clamp their expectations to its work area.
- The Release VM build shows no `Debug.WriteLine`, so a Windows-head check needs a file trace it can read back (`window-trace.log`). (Slice 235: docs/plan/slices/235-dino-windows-window.md)

#### For tools/desktop-vm/CLAUDE.md (part B) (lifted 2026-11-02)

- `Find-UiaByName` prefers an exact name and skips tooltips and their children: WinUI tooltips sit in the UIA tree ahead of the control, so a partial match taps the popup.
- A keyboard check polls `window-trace.log` for each key's line instead of sleeping a fixed time (Right took 2.2-2.6 s after Space on the VM), and sends arrow keys as extended keys. (Slice 235: docs/plan/slices/235-dino-windows-window.md)

#### For src/DinoReader.App/CLAUDE.md (part B) (lifted 2026-11-02)

- Windows keys go through one hook (`Platforms/Windows/DinoKeyboard.cs`), then Core `DinoKeyMap`, then the top page's `IDinoKeyTarget`; pages handle no keys directly.
- Tooltips and the "click" wording come only from `DesktopTips` (Windows only). (Slice 235: docs/plan/slices/235-dino-windows-window.md)


Part B VM run 6 (after merging main): PASS 6/6 (dino-launch, open-book, page-turn, settings, keyboard, window) on b7a29744, package fa3f0e388903; log reviews/s235b-dino-vm-run6.log.
Part B emulator run (after merging main): PASS 3/3 (leave-book-stops-read, read-back-reread-background, open-book-voice-id) on b7a29744, build fa3f0e388903, 220 s; log reviews/s235b-emulator-run.log.

#### Part C: parked (night 2026-11-03-2349 wave 1, two device strikes)

*Likely cause, 2026-11-04 (from Slice 264's proven diagnosis, not yet checked for part C):* Slice 264's identical `LayoutCycleException` on the Windows shelf was traced with WinUI's `LayoutCycleTracingLevel = High` to `AspectRatioView.ArrangeOverride` re-invalidating Measure whenever the arranged width differs from the measured one by more than 0.5 px, which layout rounding does on fractional tile widths. Part C's crash on the switch to the kids' shelf is very likely the same loop (its width-driven layout gives fractional widths). Next: apply Slice 264's `AspectRatioView` fix (or run with the same tracing) before any redesign; see `docs/plan/slices/264-dino-gallery-reorder-mode.md`, *Cause found*.

*Moved back to planned by the night's final feedback loop (2026-11-04, user answer 2026-11-01). Park reason kept: two VM strikes on the Windows shelf; the next attempt starts with a design pass of the shelf layout, not a third patch.*

*Parked 2026-11-04: VM checks fail after two fixes in the same area (the Windows shelf). Work in progress is on draft PR #776 (branch `feat/s235c-windows-polish`), unmerged and not reviewed in its last form.* Built: `WindowShapeSeam` (Core, unit-tested; off Windows it returns main's values), Windows shelf grid and column counts, hover and focus rings, Tab stops, right-click sheet, focus return, Settings Up/Down/Right (`DinoKeyMap`), the speed hint string, `ActivateFocused` observed, `WinUiCrashTrace` (logs WinUI `UnhandledException` to window-trace.log), new VM check `dino-desktop-polish.ps1`. Reviews: `s235c-high.md`, `s235c-medium.md`, `s235c-check1-medium.md`, `s235c-check2-medium.md` under `D:/ReadTheStupidText-worktrees/reviews/` (the code changed after check 2, so the gate must run again).

- **Strike 1 (VM run 1-2):** the app died 90 ms after the shelf switched from the grown-ups corner to the kids' shelf: `Microsoft.UI.Xaml.LayoutCycleException` "Layout cycle detected" (HResult 0x802B0014, read by `WinUiCrashTrace`; evidence `reviews/s235c-vm-crash2/`). Which element looped was not proven; suspects were the list cap (`MaximumWidthRequest` 1600 + `HorizontalOptions Center` on the CollectionView), the margin change in the same pass, and ring Borders added to the tile template in its `Loaded` handler.
- **Fix:** list stays Fill with a side margin from the page width only (`WindowShapeSeam.ShelfSideMargin`), layout values set only when they change, ring Borders moved into the tile template (hidden, input-transparent).
- **Strike 2 (VM run 3, package ad725caf328d):** no crash and no `WinUI UNHANDLED` line, but 5 of 7 checks fail: dino-launch and dino-settings PASS; dino-open-book (reading page never opens), dino-page-turn (follows), dino-keyboard (book sheet does not open from a corner tile), dino-desktop-polish (still in the grown-ups corner, never the kids' shelf), dino-window (800x560 seen, likely because dino-desktop-polish resized the window before it; not confirmed). So tiles no longer open their book or sheet on Windows. Not yet diagnosed; first places to look: the hidden ring Borders in the template sitting over the cover in the UIA tree (the checks find `StoryTile` and invoke it), and `MakeTabStop` setting `IsTabStop`/`GotFocus` on the native tile. Log `reviews/s235c-vm-run3/run.log`.
- **Android (emulator, build ad725caf328d):** dino-landscape-shelf, open-book, back-cover, grown-ups-button, leave-book-stops-read PASS (open-book and back-cover after one rerun: the known Debug x86_64 Mono SIGSEGV at start, Slice 247).
- **Next attempt:** start from the design, not a patch: diagnose why a tile does not open (one VM probe with the UIA tree of a tile dumped), then decide whether rings can live outside the tile (one overlay ring positioned on the focused/hovered tile) so the template is untouched.
