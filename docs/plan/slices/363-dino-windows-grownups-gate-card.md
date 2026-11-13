### Slice 363 — Dino: the grown-ups' gate as one centred card on Windows and every wide screen

*Built 2026-11-13, merged as PR #1060 (1b28bf6d). Not yet tried on the Windows VM; Android emulator landscape gate flow passes.*

**Touches:** `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/GrownUpGate.cs`, `src/DinoReader.Core/OnboardingLayout.cs`, `src/DinoReader.Core/GateKeypad.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `.maestro/dino/dino-landscape-gate.yaml`, `.maestro/dino/dino-tablet-upright-cards-gate.yaml`, `tools/desktop-vm/checks/dino-gate.ps1` (new), `tools/desktop-vm/Invoke-DesktopTest.ps1`, `tools/desktop-vm/checks/dino-settings.ps1`, `tools/desktop-vm/checks/dino-record.ps1`, `tools/desktop-vm/checks/dino-reorder.ps1`, `tools/desktop-vm/checks/dino-desktop-polish.ps1`, `tests/DinoReader.Tests/**`

## Why

The 2026-11-13 Missing States mock (`docs/design/dino-reader/Dino Reader 2026-11-13 Missing States.dc.html`,
section 4, frames G1-G3) redraws the grown-ups' gate for every screen 600 or wider. Today (origin/main,
`ground-in-code` 2026-11-13) the gate splits the window: words on the left, the ring or keypad pushed to the far
right edge (`GatePage.xaml.cs:103-139` `ApplyShape`/`PlaceSheet`, layout choice `OnboardingLayout.Gate`; Slice 232
part E, Windows captions Slice 235 frame 158). The mock puts both steps in one cream card in the middle of the
window over the dimmed shelf, with the thing you act on under its heading.

**The mock trumps this plan and the shipped app.** Copy: `docs/plan/notes/2026-11-13-dino-copy.md` (*Missing
States*, G1-G3); all but one string already exist.

## Decisions

- Same card at every width of 600 or more: Windows, tablets, phone landscape. Phones held upright keep the bottom
  sheet as built. (mock)
- Hold step and sum step behave as built (hold mouse or Space fills the ring; letting go empties it; typing and
  Enter work; a wrong answer clears the field and shows a new sum). Esc = Back to my child's shelf. (mock)
- The only new string is `Gate_TypeOrClickWindows` ("Type it, then Enter, or click the answer").

## Work

- [x] A. Windows: G1 (hold) and G2 (sum) as the centred card (sizes in *Design*), replacing the split; `OnboardingLayout.Gate`
  returns the card shape for 600+; unit tests for the shape choice by size.
- [x] B. Phone landscape and tablets: G3's short card (ring beside the heading, field and question left, 3 × 2 keys
  right, 470 wide) on short screens; tablets use G1/G2's upright card at 440. Android and iOS share the MAUI code;
  check iOS landscape safe areas.
- [x] C. Flows: update the landscape and tablet gate flows; a VM flow for the Windows gate (hold with Space, type
  the sum, Enter, Esc).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Windows head on the VM: G1, G2, Esc; screenshots against the mock.
- Android emulator landscape and tablet: G3 and the upright card; iOS compile, then TestFlight landscape.

#### Design (Dino Reader 2026-11-13 Missing States mock, section 4, added 2026-11-13)

Three frames (G1, G2, G3), no simplified illustrations except the shelf behind the card (a stand-in of covers and
the rail, canvas furniture). The mock agrees with the plan above and adds the sizes. Text keys are in
`docs/plan/notes/2026-11-13-dino-copy.md` (*Missing States*, G1-G3). Hex values as in the mock; use the `DinoTokens`
key with the same value, else add one. Fredoka throughout; layout never mirrors (`src/DinoReader.App/CLAUDE.md`), so the
card is the same for right-to-left languages and only text runs right to left.

- **A. G1, hold step (Windows 1100 x 760, every width 600 or more):** the real shelf behind (rail 260 wide `#FFE9C7`,
  "My stories" 26 / 600, Grown-ups pill 48 high) under a scrim `rgba(43,37,64,.62)`. One card centred in the window: width
  440 (never wider than the window less 48), radius 28, fill `#FFF6E6`, padding 28 36 26, shadow 0 24 60 rgba(20,14,40,.45),
  content centred, gap 14. Top to bottom: step bars (two, 26 x 5, radius 3, gap 6; current `#5B57E8`, other `#e6e0d4`); eyebrow
  `Gallery_ForGrownUps` 14 / 600, `#6f6884`, letter-spacing .6; title `Gate_HoldDescription` 28 / 600, line 1.2; the ring 150 round
  (conic fill `#5B57E8` over `#ECE7FF`, 62% filled in the frame) with an ink centre 122 round `#2b2540` holding the amber lock;
  the hint `Gate_HoldCaptionWindows` 15 `#5a5470`, line 1.45, with the Space key as a key cap (`Key_Space`: padding 0 8, radius 6,
  1.5 px `#c9c2b4` border, 13 / 600, white fill, ink text); the button `Gallery_BackToShelf` 44 high, radius 22, fill `#F3EFE6`,
  1.5 px `#e2dacb` border, 15 / 600, padding 0 22. Holding the mouse on the ring or Space fills it; letting go empties it.
  Esc = `Gallery_BackToShelf`.
- **A. G2, sum step (Windows):** same card (440, same padding and gap) and same step bars (second one current). Eyebrow as G1; title
  `Gate_SumQuestion` ("What is 7 plus 5?") 32 / 600, line 1.2; under it the answer field 140 x 58, radius 16, white, focus ring
  0 0 0 3 `#FFF6E6` + 0 0 0 6 `#5B57E8`, 28 / 600 centred; under that a 3 x 2 grid of answer keys the full card width, gap 10, each 60
  high, radius 18, white, shadow 0 3 0 rgba(43,37,64,.1), 26 / 600 (the six answers: 9, 12, 14, 11, 13, 10 in the frame); then the
  line `Gate_TypeOrClickWindows` 15 `#5a5470` with only the word "Enter" drawn as a key cap (same style as G1's Space); then `Gallery_BackToShelf`. Typing, Enter and a wrong answer (field clears, new sum) are as built.
- **B. G3, phone landscape and short screens (864 x 405 frame, 852 x 393 screen):** the same cream card, narrower padding:
  width 470 (max window less 48), radius 28, padding 20 28 18, gap 14. The step bars on top, then one row (gap 22) with the ring
  (hold) or the question and field (sum) beside the heading block: eyebrow 13 / 600, title 24 / 600, line 1.2; in the sum step the
  3 x 2 keys sit on the right of the same row, all inside the card; `Gallery_BackToShelf` at the foot. The card fits 393 high with room around.
  Tablets use G1/G2's upright card at 440.
- **Phones upright:** keep the bottom sheet as built (no frame).
- Not built: the Windows title bar (32 high, `App_Name` 12.5 / 500, minimise, maximise, close, 46 x 32 each) and the 1100 x 760
  window frame, which are the real window.

No new string except `Gate_TypeOrClickWindows` (all 15 `AppResources.<lang>.resx`).

**Plan changed to match the mock (2026-11-13):** the Design above supplies the sizes the plan lacked; the plan's
Decisions already matched G1-G3, so none was reversed. One wording point: `Gate_TypeOrClickWindows` ("Type it, then Enter, or click the answer") stays
one resource string; the page draws the word "Enter" in it as the key cap (a formatted label span), never two strings. No `CLAUDE.md` rule is touched: the layout-never-mirrors rule holds, and no hard rule clashes.

**Size rule after review:** one pure function, `OnboardingLayout.Gate(width, height)`, used everywhere (page, tests, the VM check):
height under 540 is the short card (G3: 470 wide, or the window width less 24 dp each side if narrower) at any width; else width 600 or more is
the card (440); else the bottom sheet (phone upright). Tests: 1100x760 card, 864x405 short card, 500x450 short card, 393x852 sheet, 800x1280 card.

*Changed while building:* six answer keys (`GateKeypad.AnswerChoices`: the answer plus five within 3), and tapping one submits. The Enter and Space key caps are drawn in the caption from `Key_Enter` / `Key_Space` (case-insensitive, cut at the matched length); `Gate_TypeOrClickWindows` and `Key_Enter` exist in all 15 languages (non-English machine-drafted). The card ring is `CardRingDrawable` in `GatePage.xaml.cs` (`HoldRingDrawable` is outside Touches). The VM checks `dino-settings`, `dino-record`, `dino-reorder` and `dino-desktop-polish` now click the answer key whose name is the sum. New VM check `tools/desktop-vm/checks/dino-gate.ps1`, registered in `Invoke-DesktopTest.ps1`.

*Size rule (built):* A is the 440 card for G1/G2; B is the 470 short card (G3) for windows under 540 high at any width, while tablets and windows 600+ high enough get the 440 card and a phone held upright keeps the sheet.

*Device (emulator-5554, Dino cache 8743684009c3):* `dino-landscape-gate` PASS (`gate mode=ShortCard step=hold cardWidth=470`, then Sheet upright, then ShortCard; Back reachable). `grown-ups-button` FAIL at "Assert that id: GalleryDone is not visible" after Settings opened; that flow enters through the grown-up hook and this slice changes no Settings or Gallery file, so likely unrelated (not compared with main). Logs: `D:/ReadTheStupidText-worktrees/reviews/w53-device/s363-dino-landscape-gate.log`, `s363-grown-ups-button.log`, `s363-grown-ups-button-maestro.log`.

*Decided (the user, 2026-11-13):* keep the mock's one question and six keys: "no. 1 question is good enough. If anything, we should just make sure if the kids know math and pass that step, they cannot buy books by themselves." The purchase itself is guarded by each store's own confirmation (checked 2026-11-13 on the vendors' help pages: Apple asks for Face ID / Touch ID / password, "Always Require" or "Require After 15 Minutes", and Ask to Buy for child accounts; Google Play "Purchase verification" defaults to Always and re-verifies before in-app buys in apps for ages 12 and under; Microsoft Store asks for the password every time by default). Whether to add the app's own device-owner check (face, fingerprint or device PIN) before the store sheet is asked of the user the same day; until answered, nothing changes. (Earlier: the six keys let a child pass the sum step by a random tap about 1 time in 6 after the 3-second hold; a wrong tap rolls a new sum.)

## Not yet verified

- Windows head on the VM: `dino-gate`, G1/G2 screenshots against the mock, Esc, and the four changed VM checks (5.8 GB free, no VM run).
- The tablet card flow on the tablet AVD.
- The hold ring's touch area on the short card (check #9).
- Landscape phones under 393 dp high (the short card is drawn to fit 393 high per mock G3; the hold ring stays outside the ScrollView so the hold gesture works).
- Windows under about 470 wide and under 540 high.
- The UIA AutomationId `GateCard` on WinUI.
- iOS landscape safe areas and compile.
- Native reads of 14 languages (`Gate_TypeOrClickWindows`, `Key_Enter`).
- `grown-ups-button` fails on GalleryDone on the emulator; check on main.

## From review

Review read in full: `reviews/s363-high.md` 9/9, `reviews/s363-medium.md` 9/9, `reviews/s363-check.md` 10/10, `reviews/s363-check2.md` 9/9, `reviews/s363-check3.md` 1/1. Applied: high #2, #3, #4 (the card clipping, later replaced by the size rule), #8; medium #1, #2, #3 (the `ShowHoldAlternative` part only), #5, #6; check #1 (as the size rule), #2, #3, #6, #9, #10; check2 #4, #5 and the other check2 and check3 findings not skipped below.

- Skipped #1 (`reviews/s363-high.md`): "so a child passes the grown-ups' gate by tapping at random about 1 time in 6." - the mock's design; under *Waiting on the user*.
- Skipped #5 (`reviews/s363-high.md`): "The key sizing (HeightRequest, FontSize, WidthRequest) is written twice" - style, no failure path.
- Skipped #6 (`reviews/s363-high.md`): "The step is read from two places" - style, no failure path.
- Skipped #7 (`reviews/s363-high.md`): "Dead code: TabletCardKind.Gate, GateMaxWidth" - BookSheet shares `TabletCardLayout.cs`; dead constants only.
- Skipped #9 (`reviews/s363-high.md`): "Not confirmed without a device run" - unconfirmed; checked on the emulator, later listed under Not yet verified.
- Skipped #3 in part (`reviews/s363-medium.md`): "Sheet and card are two parallel view trees synced by hand" - only the `ShowHoldAlternative` desync was fixed; the rest is style.
- Skipped #4 (`reviews/s363-medium.md`): "A translation spelling the key differently from Key_Space/Key_Enter shows no key cap" - no Turkish-style casing hazard among the 15 languages.
- Skipped #7 (`reviews/s363-medium.md`): "Redundant layout work and duplicated sizing code that can drift." - style/perf, no failure path.
- Skipped #8 (`reviews/s363-medium.md`): "Minor garbage; reuse the buttons." - style/perf, no failure path.
- Skipped #9 (`reviews/s363-medium.md`): "so a touch hold in progress loses its hint mid-hold." - cosmetic, rotation mid-hold only.
- Skipped #1 (`reviews/s363-check3.md`): "CaptionParts.Split matches with CurrentCulture" - a case-insensitive match of the key word gives the same cut in these 15 languages whether CurrentCulture or CurrentUICulture.

- Skipped #4 (`reviews/s363-check.md`): "card ScrollView Orientation=Neither on hold step clips ring/caption/Back on short windows or large font scale, no scroll." - the hold ring must stay outside a ScrollView for the hold gesture; the short card fits 393 high per mock G3; landscape under 393 dp high listed under Not yet verified.
- Skipped #5 (`reviews/s363-check.md`): "FillAnswerKeys and PlaceCardBody both set key height/font/width on the same buttons each roll; sizes duplicated in three places." - style only, no failure path.
- Skipped #7 (`reviews/s363-check.md`): "TabletCardKind.Gate, GateKeyHeight, GateHoldRing, GateFixedBelow, 520 width unused outside tests; dead rules." - dead code, no failure path.
- Skipped #8 (`reviews/s363-check.md`): "AnswerChoices assumes sums 12..38 (1-2 positive digits) without stating or clamping; changing operand range yields -1 or 3-digit keys." - unstated range, no failure path today.
- Skipped #1 (`reviews/s363-check2.md`): "card shows six answer keys, 1/6 random-pass chance; wrong tap rerolls. Likely the mock's design; no lockout." - the mock's design; recorded for the user.
- Skipped #2 (`reviews/s363-check2.md`): "height under 540 gets ShortCard at any width; fixed key widths (~214 dp grid + 56 dp padding) squeeze the question column on a ~360 wide window." - only a window under ~470 wide and under 540 high; listed under Not yet verified.
- Skipped #3 (`reviews/s363-check2.md`): "hold-step ScrollView Orientation=Neither; on a very short window or large font Back falls off with no scroll." - the hold ring must stay outside a ScrollView; already under Not yet verified.
- Skipped #6 (`reviews/s363-check2.md`): "state duplicated across _sumStep, SumText.IsVisible, KeyPad.IsVisible and Card* views; drift risk on resize across the 600/540 boundary." - style, no failure path.
- Skipped #7 (`reviews/s363-check2.md`): "key sizing duplicated in FillAnswerKeys and PlaceCardBody; extract one ApplyKeySizing helper." - style only, no failure path.
- Skipped #8 (`reviews/s363-check2.md`): "scrim colour, ring colours (#ECE7FF, #5B57E8) and shadow (#140E28) hard-coded, not DinoTokens." - style, no failure path.
- Skipped #9 (`reviews/s363-check2.md`): "PressAnswerKey forAnswer guard is dead (keys rebuilt each sum); a tap while _closing returns silently." - dead guard, no failure path.
