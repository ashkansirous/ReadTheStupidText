# Slice 174 — Dino: a cleaner reading screen, and a back button that never surprises

*Planned 2026-10-28.* Dino Reader only (Android, iOS and the Windows head). **Waits on the user's Claude Design mock**
for the reading screen (portrait and landscape); the back-button part can start before it.

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

## Work

- [ ] **First end-to-end piece (no design needed):** back on the kids' shelf asks Stay/Close; back in grown-up mode returns to
      the kids' shelf; back out of Settings ends on the kids' shelf. Android, with the iOS swipe-back checked on the pushed pages.
- [ ] Remove `LockButton` from the reading page (and its handler, if nothing else uses it).
- [ ] Auto-scroll: reproduce on the emulator and the user's phone (wireless adb), fix, add a unit test for the pure scroll
      target where possible; make Text only follow the sentence too.
- [ ] Import the Claude Design mock; `design-to-plan` writes the Design subsection here.
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
