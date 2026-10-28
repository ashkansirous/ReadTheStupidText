# Slice 174 — Dino: a cleaner reading screen, and a back button that never surprises

*In progress (wave 2026-10-28c), draft PR (planned 2026-10-28).* Dino Reader only (Android, iOS and the Windows head). The Claude Design mock arrived (Batch 37,
frames 91-94; see Design).

**Batch:** 37 (Dino settings, 2026-10-28; Decision 169). Builds **after Slice 172** (both edit the shelf page).
**Touches:** `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/TransportRow.cs`, `src/DinoReader.App/ReadingTheme.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/TextPages.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/ModeSwitch.cs` (placement only), `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/GrownUpMode.cs`, `src/DinoReader.App/SettingsPage.cs` and its sub-pages (back handling, from Slice 172), `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/Platforms/Android/MainActivity.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.Core/PictureTextBook.cs` (scroll target, if the bug is there), `tests/DinoReader.Tests/**`, `docs/design/dino-reader/**`, `src/DinoReader.App/CLAUDE.md`

## Why

The user, 2026-10-28: the reading screen needs a better look. The play button looks strange, auto-scroll
doesn't work, landscape needs a much better place for the buttons with more room for the story text, and the
parents' button shouldn't be on the reading screen. The back button closes the app from the kids' shelf and from
grown-up mode, which it never should.

What exists (checked 2026-10-28): the play button is a 104 px coral circle with a dark under-edge and a "wandered"
halo, next to 72 px skip buttons (`TransportRow.cs:10-33`, about 105-120). Auto-scroll is built (Slice 171,
`PictureTextPages.cs:142-200`, `ScrollToCurrent`), checked on the emulator only, Picture + text mode only.
Landscape layout applies only to picture + text books (`ReadingPage.xaml.cs:330-360`, Slice 98): picture left,
text right, transport under the text. The parents' button on the reading page is `LockButton`
(`ReadingPage.xaml:25-35`, `OnLockTapped` opens the gate). No page handles back on the shelf (`GalleryPage`);
grown-up mode is a state of the shelf page (`GrownUpMode`), and the shelf is the `NavigationPage` root
(`App.xaml.cs:38-89`), so Android back backgrounds or closes the app.

## Decisions locked with the user (Decision 169)

1. **Design comes from the user's Claude Design mock.** When the link arrives: `import-claude-design` into
   `docs/design/dino-reader/`, then `design-to-plan` writes a **Design** subsection here. Until then the brief
   below is the direction; where the mock differs, the mock wins.
2. **Play button:** a flat round button in the same family as previous/next, a little bigger; no dark 3D under-edge
   and no wander halo.
3. **Landscape (every book that shows text, Picture + text and Text only):** picture on the left about 40%, text on the
   right about 60%; the controls in a slim vertical strip on the far right edge (previous, play, next top to bottom);
   no top bar, only a small back button and the view switch over the picture's corner. Text only: the text takes
   everything except the strip.
4. **No parents' button on the reading screen.** `LockButton` goes; grown-ups use the shelf gear (Slice 172).
5. **Auto-scroll is a bug to fix**, in Picture + text and Text only, then verified on Android, iOS and Farsi (RTL).
   Reproduce first (`superpowers:systematic-debugging`), then fix where it actually breaks: `_lines` not measured,
   the `UserIsScrolling` pause sticking, or the scroll target.
6. **Back button, grown-up side:** in grown-up mode, back leaves grown-up mode to the kids' shelf (same as Done). From
   Settings and its sub-pages back steps out one page at a time and ends on the **kids'** shelf, never the grown-up shelf.
7. **Back button on the kids' shelf (Android):** asks "Close Dino Book Reader?" with **Stay** (default) and **Close**,
   in the main language, no grown-up gate; only Close exits. iOS has no back button and Windows closes from the
   window, so nothing changes there.

#### Design (Dino Reader Batch 37 mock, frames 79-94, added 2026-10-28)

Four frames (91-94). The mock agrees on the flat play button, no parents' button and the back dialog; it differs from the plan on landscape. The wait on the mock is cleared. Header glyphs are inline SVG, not Phosphor.

- **Portrait (91):** page dots gone. The picture fills the space between title and controls, cropped to fit from the top (the art keeps its bottom 30% free). The text card sits on that part, holds five whole lines, reaches just above the controls and scrolls itself to keep the spoken sentence in view. Play is an 84 px flat coral circle, no under-edge, no halo. Previous and next are 64 px, same flat family. Header: back, then the flag button (when shown) left of the view switch (three 44x40 segments, radius 12, in a `#F0E8D8` pill with 3 px padding, radius 16; the active one white with glyph `#5B57E8`). No parents' button. Text only follows the sentence the same way.
- **Back dialog (94):** Android, kids' shelf only. "Close Dino Book Reader?" with "Stay" (big default) and "Close". A second back press or a tap outside counts as Stay. No gate; app language. Elsewhere back steps one page; grown-up mode and Settings pages end on the kids' shelf; reading goes back to the shelf. iOS and Windows unchanged.
- **Landscape, Picture + text (92):** no top bar, title or dots. The left 40% is the picture, cropped from the top. Controls float over the picture's bottom, side by side: previous 52, play 68, next 52. The text takes the whole right side, full height, 23 px, auto-scrolling. Back (44 px circle) and the view switch (40x38 segments) float over the picture's top-left on a `rgba(255,255,255,.92)` chip with a small shadow; the flag joins them. The left inset clears the notch. Same for photo books (a proposal there: switch to fit-whole).
- **Landscape, Text only (93):** the card fills the screen, controls below it side by side as in portrait. Back and view switch sit in the card's top corner with the first line starting under them. No view switch for books with no pictures. Text 26 px, auto-scrolls. In Farsi only the text runs RTL.

**Open point: landscape controls.** Plan decision 3: a slim vertical strip on the far right (previous, play, next), for both modes. Mock 92 floats the controls horizontally over the picture's bottom; mock 93 puts them below the card. **Default:** the mock (decision 1 says the mock wins).
**Open point: play size.** Plan says the new play is "a little bigger" than the current 104 px circle with 72 px skips. Mock shows 84 px play and 64 px previous/next (smaller). **Default:** the mock sizes (84/64 portrait, 68/52 landscape).
**Open point: back from grown-up mode.** Mock 85 note says Android back goes to Settings; this slice and mock 94 say the kids' shelf. **Default:** kids' shelf.

The mock settles what the plan left open: the five-line card, no page dots, picture cropped from the top, the flag's place in the header.

New strings (14 languages): "Close Dino Book Reader?", "Stay", "Close".

## Work

- [ ] **First end-to-end piece (no design needed):** back on the kids' shelf asks Stay/Close; back in grown-up mode returns to
      the kids' shelf; back out of Settings ends on the kids' shelf. Android, with the iOS swipe-back checked on the pushed pages.
- [ ] Remove `LockButton` from the reading page (and its handler, if nothing else uses it).
- [ ] Auto-scroll: reproduce on the emulator and the user's phone (wireless adb), fix, add a unit test for the pure scroll
      target where possible; make Text only follow the sentence too.
- [x] Import the Claude Design mock; Design subsection written (2026-10-28).
- [ ] Transport row restyle (portrait and landscape sizes) from the mock.
- [ ] Landscape layout from the mock for Picture + text and Text only, on Android and iOS (rotation, safe areas, notch).
- [ ] Strings (14 languages, 13 drafts): the close question, Stay, Close.
- [ ] `src/DinoReader.App/CLAUDE.md`: back-button rules (kids' shelf is home; grown-up back always lands there) and the
      reading-screen layout rule.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Android (headless emulator, `device-test`): back on the kids' shelf shows the question; Stay keeps the app, Close exits;
  back in grown-up mode lands on the kids' shelf; back from a Settings sub-page goes up to Settings, then the kids' shelf;
  the reading page has no parents' button; in Picture + text and Text only the text follows the spoken sentence across a
  long page, in English and Farsi; landscape matches the mock, the text takes the larger share, all three controls reachable.
- Real phone (wireless adb): auto-scroll on the book the user saw fail.
- iOS: compile, then rotation, safe areas and auto-scroll on a simulator or TestFlight build.
- Windows head: the reading page builds and lays out wide; nothing changes about closing.
- Native read of the three new strings (13 drafts) goes to Outstanding work.
