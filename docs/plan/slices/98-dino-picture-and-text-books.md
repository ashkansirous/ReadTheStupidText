# Slice 98 — Picture + text books: a picture per page, the text beside it

*Planned 2026-10-24*, from the user's request "I want an easier way to add books for kids older than 3.
Those books are usually one side picture and the other side text. Accept a format that is text
separately from image, but show it together. For the Three Little Pigs, you have the text for each page;
I give you a picture for each page and you show them side by side."

**Batch:** 18 (Dino Reader: layout, add-a-book button, picture + text books)
**Depends on:** Slice 91 (`.dino` packages, starters), Slice 94 (picture starter in `StarterBuilder`),
Slice 95 (bundled translations), Slice 78 (`StoryWizardPage`), Slice 96 (the reader no longer mirrors)
**Touches:** `src/DinoReader.Core/PictureTextBook.cs` (new), `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/PictureTextPages.cs` (new), `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/PagesStep.xaml`, `src/DinoReader.App/PagesStep.xaml.cs`, `src/DinoReader.App/StoryWizardPage.xaml.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/CLAUDE.md`, `tests/DinoReader.Tests/PictureTextBookTests.cs` (new), `tests/DinoReader.Tests/StarterBuilder.cs`, `tests/DinoReader.Tests/Starters/<stem>/pictures/**` (when the user sends pictures), `src/DinoReader.App/Resources/Raw/starters/*.dino`

## Why

The only ways to add a book today are photographing pages (OCR) or one `.txt`/`.pdf`/`.docx` file
(`StoryWizardPage`, `PagesStep`). Books for children over three usually have a picture on one side and
the text on the other. A grown-up who has the pictures and the text separately has no way to put them
together, and the reader has no layout that shows a picture next to its text. `StoryPage` can already
hold `Text` and `ImageFile` without `Lines`, but only the scanned-page layout (`ScanPages`, which needs
line boxes) and the fallback `PicturePages` draw pictures.

## Decisions (2026-10-24, agreed with the user)

- **Decision 102: picture + text books.** A page with a picture and its own text (an `ImageFile`, `Text`
  and no `Lines`) is a picture + text page. No new field and no schema bump: `story.json` and `.dino` stay
  `schemaVersion` 1, and an older app shows such a book with its existing fallback.
- **The input format.** Page pictures are picked together and used in natural file-name order (`1, 2, …
  10`, not `1, 10, 2`). The text is a `.txt` file or pasted text, with pages separated by a line holding
  only `---` (surrounding spaces ignored, CRLF or LF). Before saving, the app shows how many pictures and
  how many text pages it found. If they differ, it says so and doesn't save until they match. The pure
  pairing and splitting lives in Core (`PictureTextBook`), the pickers in the app.
- **Where it lives in the app.** A new "Pictures + text" choice in the wizard's Pages step, beside camera,
  gallery and file. After pairing, the pages go through the same Cover and Text steps (casting, speakers,
  editing) as any other story.
- **How a page looks.** Held sideways: the picture on the left half and the text on the right half. Held
  upright: the picture on top and the text below. The text side is the normal reading text with the
  sentence highlight, follows its own direction (Slice 96) and scrolls if it's long. The picture fits
  without cropping. Pages with only a picture or only text look as they do now. Page turns, voices,
  casting and bundled translations (Slice 95: the highlight stays on the source text) work unchanged.
- **Starters get pictures the same way.** `tests/DinoReader.Tests/Starters/<stem>/pictures/1.jpg … N.jpg`
  next to a text starter (Three Little Pigs first) turns it into a picture + text book: the builder pairs
  picture *n* with page *n* of `StarterTexts`, re-encodes to JPEG q85 and fails if the counts differ. If the
  user's pictures don't match the current page breaks, the page breaks in `StarterTexts` are re-split to
  match. The translation tables are then regenerated for any changed pieces (the Slice 95 coverage test
  forces this). A rebuilt starter reaches existing installs only as a new install. Upgrading stored
  starters in place is out of scope, as in Slice 95.
- **Store books** use the same model, so a picture + text `.dino` uploaded to the store needs no worker
  change.
- **Out of scope:** a zip import of pictures plus `text.txt`; editing the pairing (reordering one picture)
  after save, beyond what the existing wizard allows; two-page spreads; pictures for the Gingerbread Man and
  the Tortoise and Hare until the user sends them.

## Work

- [ ] 1. **Smallest end-to-end: a picture + text page reads side by side.** Core `PictureTextBook`
  (natural sort, `---` split, count check) with tests; `ReadingSurface` picks a new `PictureTextPages`
  layout for picture + text pages, sideways and upright. Test it with a hand-built story.
- [ ] 2. **The wizard choice.** "Pictures + text" in `PagesStep`: multi-pick pictures, then a `.txt` file or
  pasted text, the count summary and mismatch message (all nine languages), then into Cover/Text as usual.
- [ ] 3. **Starter pictures.** `StarterBuilder` reads `Starters/<stem>/pictures/` and the coverage tests
  stay green. Three Little Pigs is rebuilt when the user's pictures arrive (re-split pages and
  re-translate if needed).
- [ ] 4. Rules in `src/DinoReader.App/CLAUDE.md` and `src/DinoReader.Core/CLAUDE.md` (written at planning;
  check they still match).

## Verification

- Unit (`tests/DinoReader.Tests`): natural order (`1, 2, 10`), the `---` split with CRLF, spaces, a trailing
  separator and empty pages; the mismatch result; a picture + text story round-trips through
  `StoryPackageWriter`/`Reader`; the builder fails on a count mismatch.
- Emulator (device-test): add a 3-page book with three pictures and pasted text. Sideways it shows the
  picture left and text right with the highlight moving; upright it shows the picture over the text.
  4 pictures with 3 text pages shows the mismatch and doesn't save. With Farsi chosen, an English
  picture + text starter reads in Farsi with the text side unchanged.
- Windows head: the side-by-side layout in a wide window.
