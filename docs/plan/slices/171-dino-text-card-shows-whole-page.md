# Slice 171 — Dino: the text card scrolls with the reading

*Built 2026-10-27; Android checked on the emulator (English, Chinese, rotation, hand scroll), iOS and Farsi RTL not yet verified.* Dino Book Reader, picture + text mode, on Android, iOS and the Windows head (shared MAUI code; each platform verified).

**Touches:** `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.Core/PictureTextBook.cs`,
`tests/DinoReader.Tests/**` (PictureTextBook placement tests), `site/dino/reading-screen.webp`

## Why

Found while taking the website screenshots (2026-10-27): in picture + text mode the upright text card is capped
(`PlacePanel` sets `panel.MaximumHeightRequest = place.MaxHeight`, `PictureTextPages.cs` ~156-160; the cap is
`PictureTextBook.PanelMaxHeight`, Slice 135 / Decision 131). On a 1080x2400 phone it holds about 2.5 lines at
`LineHeight` 1.5, so every page with three or more lines (every Tortoise and the Hare and Three Little Pigs page checked)
shows a half-cut last line and a scrollbar. A child cannot scroll a card while it reads, and the highlight runs into the
hidden line.

## Decisions (user, 2026-10-27)

- **The text never shrinks** (user: "I dont want the text to shrink. It should be auto scrolling as the reader reads it").
- **The card auto-scrolls with the reading:** it keeps today's place and size over the picture, and as each sentence is
  spoken the card scrolls smoothly so that sentence is fully in view (scroll to the start of the sentence; a long sentence
  keeps its current line in view as it goes). Turning to a page starts at the top. The picture is not resized or moved.
- **No half lines at rest:** the card's height snaps to a whole number of lines, so the last visible line is never cut in half.
- A child or grown-up can still scroll the card by hand; reading carries on and the next sentence scrolls it back into view.
- The line-height snapping and the "which offset shows this sentence" maths live in `DinoReader.Core` (`PictureTextBook`) as
  pure functions with unit tests; the MAUI view only applies them. Sideways (two-column) mode gets the same auto-scroll.
- Same behaviour on Android, iOS and Windows; phone and tablet sizes.

## Work

- [ ] Core: whole-line card height from the cap and the line height; the scroll offset that shows a given sentence's line range;
      unit tests on phone (1080x2400) and tablet (1600x2560) sizes for 2-, 3-, 5- and 8-line pages.
- [ ] View: snap the card height; on each highlighted sentence, smooth-scroll the card's ScrollView to the Core offset;
      reset to the top on page turn; re-place on size and language change.
- [ ] Android task: run on the emulator, read Tortoise and the Hare and Three Little Pigs pages with 3+ lines and watch the card follow.
- [ ] iOS task: compile for iOS (`-p:IncludeIos=true -p:IncludeWindows=false -p:IncludeAndroid=false`) and check the same pages on
      the simulator or the next TestFlight build.
- [ ] Windows head: same pages in a phone-shaped and a wide window.
- [ ] Retake the website picture (`site/dino/reading-screen.webp`) on a 3-line page mid-read.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`
- `device-test` on the Android emulator (1080x2400 and a tablet AVD): every page of Tortoise and the Hare and Three Little Pigs,
  picture + text mode: every spoken sentence scrolls fully into view, no half-cut line at rest, text size unchanged.
- iOS: same check on the simulator or the next TestFlight build (record which); Windows head: same pages.

## Not yet verified

- iOS line reader (`IosTextLineSource`, TextKit rebuilt to match UILabel) and the card's scroll on a real iPhone: Latin, Farsi (RTL) and a CJK page. Compile-checked only.

#### For CLAUDE.md (lifted 2026-10-27)

- Dino text card (Slice 171): line positions come from the label's real layout through `ITextLineSource` (per-platform), never from `Mark`/measured guesses; the card scrolls to the spoken sentence from those lines. (Slice 171: docs/plan/slices/171-dino-text-card-shows-whole-page.md)
