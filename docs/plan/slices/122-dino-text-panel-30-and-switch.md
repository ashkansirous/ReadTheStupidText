### Slice 122 — Dino: smaller text panel over pictures, and a switch to hide it

*In progress.*

**Touches:** `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/Resources/Strings/*`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/PictureTextBook.cs`, `tests/DinoReader.Tests/PictureTextBookTests.cs`, `tests/DinoReader.Tests/StoryJsonTests.cs`

**Why.** Reported by the user on 2026-10-25 after the illustrated Three Little Pigs (Slice 119). With the phone upright, picture mode draws the story text in a panel over the bottom of the picture. The panel is capped at 58% of the card (`PictureTextPages.PanelHeightShare`, `UprightPage`). Its margin and shadow sit outside the cap, and AspectFit letterboxing makes the card taller than the visible picture, so on the phone the panel hides about 70% of the picture. That is far too much.

**Decisions locked with the user.**
- The panel takes **at most 30%** of the visible picture with the phone upright, margin and shadow included. Longer text scrolls inside the panel, as it already does. The font stays the same.
- A grown-up setting per book, **"Show the words over the pictures"**, sits in the book's grown-up settings, next to the animal-sounds switch (`CastRows`/`CastPage`). When it is off, picture mode shows only the picture, in both orientations. Reading aloud, highlighting and turning pages are unchanged.
- It is **off by default for books whose pictures already contain the words**: books made from scans, photos of pages or a PDF (`ScanLayout.HasScannedPages`, or a picture-text page imported from a PDF or a camera page). It is **on by default** for every other book, including the store and starter books, whose art has no text in it (Decision 127).
- Scans with placed lines already draw boxes on the page and no panel (`ScanPages`). The setting does not change that.

**Work.**
- [ ] Pure Core rule, tested: `PictureTextBook.PanelHeightShare = 0.30` of the *visible picture* height (the AspectFit picture rect, not the card), with the margin inside the share. Add a pure helper that takes the card size and picture aspect and returns the panel's maximum height. Unit tests cover a tall card with letterbox bands, a wide picture and a short text.
- [ ] `UprightPage` uses the helper. Remove the 58% constant and update its doc comment.
- [ ] `Story.WordsOverPictures` (`bool?`; null means the default from the rule above), stored in `story.json` only when it was set (the `AnimalSoundsOn` pattern, no schema bump). Copy it through `StoryImport` and `StoryActions` `with`-copies.
- [ ] `PictureTextBook.ShowsWordsOverPictures(Story)` gives the effective value (stored value, else false for scanned, photo or PDF books, else true). Tests cover each case.
- [ ] `PictureTextPages`: when it is false, draw the picture card only. Upright, the picture fills the card. Sideways, the picture takes the full width and the transport row stays.
- [ ] The grown-up switch row, with strings in every app language. Toggling it re-renders an open book.
- [ ] Emulator check, upright and sideways: on the pigs book the panel covers at most 30% of the picture. With the switch off, only the picture shows. A scanned book has the switch off by default.

**Verification.** `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green, then the emulator check above via `device-test`.

**Not in scope.** Changing the font size, or moving the text above the picture.

**Art follow-on.** Story pictures keep their faces and action in the **top 70%**, with the bottom 30% calm. The `create-dino-story` skill is updated in Slice 123.
