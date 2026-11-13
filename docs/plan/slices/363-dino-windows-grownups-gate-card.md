### Slice 363 — Dino: the grown-ups' gate as one centred card on Windows and every wide screen

*Planned 2026-11-13 (user, RefineScope).*

**Touches:** `src/DinoReader.App/GatePage.xaml`, `src/DinoReader.App/GatePage.xaml.cs`, `src/DinoReader.App/GrownUpGate.cs`, `src/DinoReader.Core/OnboardingLayout.cs`, `src/DinoReader.Core/GateKeypad.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `.maestro/dino/dino-landscape-gate.yaml`, `.maestro/dino/dino-tablet-upright-cards-gate.yaml`, `tools/desktop-vm/flows/*gate*` (if present), `tests/DinoReader.Tests/**`

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

- [ ] A. Windows: G1 (hold) and G2 (sum) as the centred card (sizes in *Design*), replacing the split; `OnboardingLayout.Gate`
  returns the card shape for 600+; unit tests for the shape choice by size.
- [ ] B. Phone landscape and tablets: G3's short card (ring beside the heading, field and question left, 3 × 2 keys
  right, 470 wide) on short screens; tablets use G1/G2's upright card at 440. Android and iOS share the MAUI code;
  check iOS landscape safe areas.
- [ ] C. Flows: update the landscape and tablet gate flows; a VM flow for the Windows gate (hold with Space, type
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

## Not yet verified

_(filled when built)_
