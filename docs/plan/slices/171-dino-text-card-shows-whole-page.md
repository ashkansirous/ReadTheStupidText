# Slice 171 — Dino: the text card shows the whole page

*Planned.* Dino Book Reader, picture + text mode, on Android, iOS and the Windows head (shared MAUI code; each platform verified).

**Touches:** `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.Core/PictureTextBook.cs`,
`tests/DinoReader.Tests/**` (PictureTextBook placement tests), `site/dino/reading-screen.webp`

## Why

Found while taking the website screenshots (2026-10-27): in picture + text mode the upright text card is capped
(`PlacePanel` sets `panel.MaximumHeightRequest = place.MaxHeight`, `PictureTextPages.cs` ~156-160; the cap is
`PictureTextBook.PanelMaxHeight`, Slice 135 / Decision 131). On a 1080x2400 phone it holds about 2.5 lines at
`LineHeight` 1.5, so every page with three or more lines (every Tortoise and the Hare and Three Little Pigs page checked)
shows a half-cut last line and a scrollbar. A child cannot scroll a card while it reads, and the highlight runs into the
hidden line.

## Decisions (user, 2026-10-27: "redesign and fix them"; Claude's design, change here if the user disagrees)

- **The whole page text is always visible, no scrolling, no half lines.** In order, until it fits:
  1. The card grows to fit the text; the picture moves up and scales down to make room (it stays whole, never cropped),
     so the card covers at most a band at the bottom of the picture as today, not the characters.
  2. If the picture would get smaller than about half the card's height, the text steps down in size, to no less than 85%
     of the normal size.
  3. Only then (a very long page on a small phone) does the card scroll, and its height snaps to whole lines so no line is
     ever cut in half; the highlighted sentence scrolls into view as it is read.
- The sizing rules live in `DinoReader.Core` (`PictureTextBook`) as pure functions with unit tests; the MAUI view only
  applies them. Sideways (two-column) mode keeps its layout but gets the same no-half-line rule.
- Same behaviour on Android, iOS and Windows; phone and tablet sizes.

## Work

- [ ] Core: a placement that takes the measured text height and returns picture size/position, card height and text scale
      per the rules above; unit tests for 2-, 3-, 5- and 8-line pages on phone (1080x2400) and tablet (1600x2560) sizes.
- [ ] View: measure the page text at the card width, apply the placement; re-place on size change and on language change.
- [ ] Highlight: when the card scrolls (rule 3), keep the spoken sentence in view.
- [ ] Android task: run on the emulator, check Tortoise and the Hare and Three Little Pigs pages with 3+ lines.
- [ ] iOS task: compile for iOS (`-p:IncludeIos=true -p:IncludeWindows=false -p:IncludeAndroid=false`) and check on the
      simulator/TestFlight build the same pages.
- [ ] Windows head: same pages in a phone-shaped and a wide window.
- [ ] Retake the website picture (`site/dino/reading-screen.webp`) on a 3-line page once it fits.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`
- `device-test` on the Android emulator (1080x2400 and a tablet AVD): every page of Tortoise and the Hare and Three Little Pigs,
  picture + text mode: whole text visible, no half-cut line, picture whole and not covered beyond the bottom band.
- iOS: same check on the simulator or the next TestFlight build (record which); Windows head: same pages.
