### Slice 122 — Dino: smaller text panel over pictures, and a switch to hide it

*Built 2026-10-25, not yet verified on an emulator or phone. 943 tests green, Android build clean.*

**Touches:** `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/Resources/Strings/*`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/PictureTextBook.cs`, `tests/DinoReader.Tests/PictureTextBookTests.cs`, `tests/DinoReader.Tests/StoryJsonTests.cs`

**Why.** Reported by the user on 2026-10-25 after the illustrated Three Little Pigs (Slice 119). With the phone upright, picture mode draws the story text in a panel over the bottom of the picture. The panel is capped at 58% of the card (`PictureTextPages.PanelHeightShare`, `UprightPage`). Its margin and shadow sit outside the cap, and AspectFit letterboxing makes the card taller than the visible picture, so on the phone the panel hides about 70% of the picture. That is far too much.

**Decisions locked with the user.**
- The panel takes **at most 30%** of the visible picture with the phone upright, margin and shadow included. Longer text scrolls inside the panel, as it already does. The font stays the same.
- A grown-up setting per book, **"Show the words over the pictures"**, sits in the book's grown-up settings, next to the animal-sounds switch (`CastRows`/`CastPage`). When it is off, picture mode shows only the picture, in both orientations. Reading aloud, highlighting and turning pages are unchanged.
- It is **off by default for books whose pictures already contain the words**: books imported from camera photos, gallery photos of pages or PDF pages: the Pages step stores `WordsOverPictures = false` (`StoryDraft.MarkPicturesAsPages`) when it adds them, unless the grown-up already chose. It is **on by default** for every other book, including the store and starter books, whose art has no text in it (Decision 127).
- Scans with placed lines already draw boxes on the page and no panel (`ScanPages`). The setting does not change that.

**Work.**
- [x] Pure Core rule, tested: `PictureTextBook.PanelHeightShare = 0.30` of the *visible picture* height (the AspectFit picture rect, not the card), with the margin inside the share. Add a pure helper that takes the card size and picture aspect and returns the panel's maximum height. Unit tests cover a tall card with letterbox bands, a wide picture and a short text.
- [x] `UprightPage` uses the helper. Remove the 58% constant and update its doc comment.
- [x] `Story.WordsOverPictures` (`bool?`; null means the default from the rule above), stored in `story.json` only when it was set (the `AnimalSoundsOn` pattern, no schema bump). Copy it through `StoryImport` and `StoryActions` `with`-copies.
- [x] `PictureTextBook.ShowsWordsOverPictures(Story)` gives the effective value (the stored value, else true). Tests cover each case.
- [x] `PictureTextPages`: when it is false, draw the picture card only. Upright, the picture fills the card. Sideways, a tall picture keeps the left half at full height and the other half keeps the transport row; a wide picture gets the whole page above the transport row (the first build left a small thumbnail, fixed after the emulator check).
- [x] The grown-up switch row, with strings in every app language. The switch applies the next time the book opens (the reading page closes when the grown-up opens the cast screen).
- [ ] Emulator check, upright and sideways: on the pigs book the panel covers at most 30% of the picture. With the switch off, only the picture shows. A book imported from photos or a PDF has the switch off by default.

**Verification.** `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green, then the emulator check above via `device-test`.

*Changed while building:*
- The cap is `PictureTextBook.PanelMaxHeight` with `PanelHeightShare = 0.30`, of the visible AspectFit picture, margin included. An unknown aspect falls back to 30% of the card. The cap is re-applied on `SizeChanged`.
- No live re-render: the switch applies the next time the book opens, because the reading page closes when the cast screen opens.
- The default is set at import, not derived: camera/gallery page photos and PDF import set `WordsOverPictures = false` (`StoryDraft.MarkPicturesAsPages`, `PagesStep`). Picking art for typed pages leaves it on. The effective value is `?? true`.
- The switch row shows only for picture+text books. When off, pages with a picture show the picture only (both orientations); pages without a picture keep their text card.

*From review:* two passes (high 10 findings, medium 10). Fixed: blank text-only pages, a scan-only switch, a mixed-book default, aspect 0, a 0 cap before layout, a JSON omission test, a package round-trip test. Skipped: live re-render, a size cache.

**Not in scope.** Changing the font size, or moving the text above the picture.

**Art follow-on.** Story pictures keep their faces and action in the **top 70%**, with the bottom 30% calm. The `create-dino-story` skill is updated in Slice 123.

#### For CLAUDE.md

- The words panel over a picture is capped at 30% of the visible AspectFit picture, margin included (`PictureTextBook.PanelMaxHeight` in `src/DinoReader.Core/PictureTextBook.cs`); never size it from the card. `Story.WordsOverPictures` is `bool?`, omitted from `story.json` when null, effective value `?? true`; photo/PDF page imports set it false (`StoryDraft.MarkPicturesAsPages`). (Slice 122: docs/plan/slices/122-dino-text-panel-30-and-switch.md)

#### Not yet verified

- Emulator/phone check, upright and sideways, on the pigs book: the panel covers at most 30% of the picture.
- With the switch off only the picture shows; a new photo or PDF import has the switch off.
- A sideways full-width picture does not overlap the transport row.
