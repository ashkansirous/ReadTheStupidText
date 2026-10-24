# Slice 98 — Picture + text books: a picture per page, the text beside it

*Built 2026-10-24 (PR #285), not yet verified on the emulator, the Windows head or by a native read of the strings.*

*Changed while building:* the Pages step in slot mode has no drag-reorder (delete and edit-text kept). Starter pictures are copied as-is, so they must be supplied already sized (no re-encode). Rotate/crop keeps a picture + text page's text and status (`KeepPictureText`). A mixed book stays in the picture + text layout: a picture-only page shows the picture full, a text-only page shows its text. Thumbnails are decoded at display size.

*From review:* high pass, 10 findings applied. Medium pass, 7 applied; #5 skipped per Decision 100, #9 skipped per Decision 102.

Planned 2026-10-24, from the user's request "I want an easier way to add books for kids older than 3.
Those books are usually one side picture and the other side text. Accept a format that is text
separately from image, but show it together. For the Three Little Pigs, you have the text for each page;
I give you a picture for each page and you show them side by side."

**Batch:** 18 (Dino Reader: layout, add-a-book button, picture + text books)
**Depends on:** Slice 91 (`.dino` packages, starters), Slice 94 (picture starter in `StarterBuilder`),
Slice 95 (bundled translations), Slice 78 (`StoryWizardPage`), Slice 96 (the reader no longer mirrors)
**Touches:** `src/DinoReader.Core/PictureTextBook.cs` (new), `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/CLAUDE.md`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/PictureTextPages.cs` (new), `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/PagesStep.xaml`, `src/DinoReader.App/PagesStep.xaml.cs`, `src/DinoReader.App/StoryWizardPage.xaml`, `src/DinoReader.App/StoryWizardPage.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/CLAUDE.md`, `tests/DinoReader.Tests/PictureTextBookTests.cs` (new), `tests/DinoReader.Tests/StarterBuilder.cs`, `tests/DinoReader.Tests/Starters/<stem>/pictures/**` (when the user sends pictures), `src/DinoReader.App/Resources/Raw/starters/*.dino`

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
- **The input format** (changed to match the mock, 2026-10-24; it was "Pictures + text" with a `---` text
  file and a count check). There is no separate mode. A text-only `.pdf`, `.docx` or `.txt` (or typed
  text) opens the Pages step with **one optional picture slot per page**. The pages come from the file.
  Today's `.txt` import (`IDocumentTextExtractor`, `PagesStep.xaml.cs:304`) returns one flowing string
  with no page split, so for a `.txt` (and typed or pasted text) a line holding only `---` (surrounding
  spaces ignored, CRLF or LF) starts a new page, and without one the text is one page. Tap a slot to pick
  that page's picture; **Pick pictures for all pages** fills the empty slots in natural file-name order
  (`1, 2, ... 10`, not `1, 10, 2`). Pictures are optional, so **Next** is never blocked and a page with no
  picture reads as text. A file that already has pictures follows screen 32: it uses them, shows no slots,
  and on a file with pictures on only some pages shows slots only on the others. Core's `PictureTextBook`
  holds the pure parts (natural sort, the `---` split, filling empty slots in order); there is no mismatch
  result. The pickers are in the app.
- **Where it lives in the app.** In the wizard's Pages step, after PDF or file (or Type it). From there
  the pages go through the same Cover and Text steps (casting, speakers, editing) as any other story.
- **How a page looks.** Held sideways: the picture on the left half and the text on the right half, with no
  page dots. Held upright (changed to match the mock): the picture fills the page, fitted without
  cropping, and the text sits on a 92% white panel over its lower part, up to 58% of the page height. The
  text is the normal reading text with the sentence highlight, follows its own direction (Slice 96) and
  scrolls inside the panel if it's long. Pages with only a picture or only text look as they do now. Page turns, voices,
  casting and bundled translations (Slice 95: the highlight stays on the source text) work unchanged.
- **Starters get pictures the same way.** `tests/DinoReader.Tests/Starters/<stem>/pictures/1.jpg … N.jpg`
  next to a text starter (Three Little Pigs first) turns it into a picture + text book: the builder pairs
  picture *n* with page *n* of `StarterTexts`, re-encodes to JPEG q85 and fails if the counts differ
  (unchanged by the mock). If the
  user's pictures don't match the current page breaks, the page breaks in `StarterTexts` are re-split to
  match. The translation tables are then regenerated for any changed pieces (the Slice 95 coverage test
  forces this). A rebuilt starter reaches existing installs only as a new install. Upgrading stored
  starters in place is out of scope, as in Slice 95.
- **Store books** use the same model, so a picture + text `.dino` uploaded to the store needs no worker
  change.
- **Out of scope:** a zip import of pictures plus `text.txt`; a count-mismatch check; editing the pairing (reordering one picture)
  after save, beyond what the existing wizard allows; two-page spreads; pictures for the Gingerbread Man and
  the Tortoise and Hare until the user sends them.

#### Design (Dino Reader Batch 17-18 mock, screens 31, 32, 34, 35, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 17-18.dc.html`. Four frames: Pages step with no pictures (31, lines 100-116), Pages step with pictures in the file (32, lines 118-134), reading upright (34, lines 140-155), reading sideways (35, lines 157-170). The plan was changed to match the mock on how pictures are added and on the reading layouts (see the end of this subsection). Screen number 33 is not drawn. The striped boxes labelled "1.jpg", "in file", "page 3 picture (from you)" are placeholders for real pictures, not UI.

- **Pages step, text-only file (31).** Page `#F3EFE6`. Header: back arrow 44x44, title "Pages" 20/600, subtitle "pigs.docx · 10 pages" 12 `#8a8399` (file name and page count). List padding 0/14, gap 8, bottom padding 96 for the footer. Info card: fill `#ECE7FF`, radius 16, padding 12/14, gap 8. Title "This file has words but no pictures" 14/600; body "Add one picture to each page if you like. Pages without one read as text." 12.5/1.4 `#5a5470`; button "Pick pictures for all pages" height 44, radius 22, fill `#5B57E8`, white 14/600, with an 18px picture icon (rect 17x15 radius 2.5, circle r 1.8, mountain line, stroke 2) left of the label.
- **Page row (31, 32).** White card, radius 16, padding 10, gap 10. Thumbnail 58 wide, aspect 3/4, radius 9. Filled slot: the picture. Empty slot: 2px dashed `#c9c0f5` border, plus icon 18px `#5B57E8` (stroke 2.6). Label "PAGE n" 12/700 `#8a8399`; text preview Andika 13.5, line-height 1.35, 2 lines max. Right action, 13.5/600 `#5B57E8`, height 44: "Change" on a filled page, "Add picture" on an empty one.
- **Footer button (31, 32).** Over a fade (`linear-gradient(rgba(243,239,230,0), #F3EFE6 32%)`, padding 30/14/22): "Next: cover" height 52, radius 26, fill `#FF7A59`, shadow `0 5px 0 #E2583A`, white 16/600.
- **File with its own pictures (32).** Subtitle "bo-and-the-shell.pdf · 6 pages". A line instead of the info card: green check (16px, stroke 3, `#2E7D32`) and "Pictures found on every page" 13/600 `#2E7D32`. Each thumbnail is a placeholder tagged "in file", and rows have no action. Partial pictures: the picture slot shows only on pages without one (note).
- **Reading upright (34).** Page `#FFF6E6`, same header as other reading frames (back 44x44, add-book 40x40, title 20/600 on its own row). Picture area margin 6/14/0, radius 18, fills the area (fitted, not cropped; the picture sits in the top 55%). Text panel: left/right/bottom 10, `max-height:58%`, fill `rgba(255,255,255,.92)`, radius 18, padding 14/16, Andika 21, line-height 1.55, `#2b2540`, shadow `0 6px 18px rgba(43,37,64,.18)`, scrolls inside. Unspoken text `#8a8399`; spoken sentence fill `#FFE27A`, radius 8, 4px halo of the same colour. Dots and transport as in screen 30 (Slice 96).
- **Reading sideways (35, 760x360).** Left half: back 44x44 and title 17/600 (one line, ellipsis) on top, then the picture filling the rest (radius 18, margin-top 6), padding 12/10/12/16. Right half, padding 12/16/12/10, gap 10: add-book 40x40 right-aligned, a white text card (radius 22, padding 14/18, Andika 21, line-height 1.55, unspoken `#b3abc2`, spoken `#FFE27A` as above) that fills the rest, then controls 60 / 80 / 60 circles, gap 22 (play `#FF7A59`, shadow `0 6px 0 #E2583A`). No page dots here, to save height.
- **Behaviour (notes).** No separate mode: after PDF or file (or Type it) the app already knows if the file holds pictures. Tap a slot to pick that page's picture; "Pick pictures for all pages" fills the empty slots in file-name order (1, 2 ... 10). Pictures are optional, nothing blocks **Next**; it goes on to Cover and Text. The layout stays this way round in every language (Slice 96).

Changed to match the mock (2026-10-24): (1) pictures are added per page in the Pages step (optional slots, "Pick pictures for all pages"), not through a "Pictures + text" choice with a `---` text file and a count check; (2) upright, the text panel sits over the picture instead of below it; (3) no page dots sideways.

New strings (nine locales): "Pages", "{file} · {n} pages", "This file has words but no pictures", "Add one picture to each page if you like. Pages without one read as text.", "Pick pictures for all pages", "PAGE {n}", "Change", "Add picture", "Pictures found on every page", "Next: cover".

## Work

- [x] 1. **Smallest end-to-end: a picture + text page reads side by side.** Core `PictureTextBook`
  (natural sort, `---` split, filling empty slots in order) with tests; `ReadingSurface` picks a new
  `PictureTextPages` layout for picture + text pages: sideways (picture left, text right, no dots) and
  upright (picture filling the page, text panel over it). Test it with a hand-built story.
- [x] 2. **The picture slots.** `PagesStep`: one optional slot per page for a text-only file or typed text
  (a `---` line splits a `.txt`), tap-to-pick, "Pick pictures for all pages", the "Pictures found on every
  page" state (screen 32), then into Cover/Text as usual. Strings in all nine languages.
- [x] 3. **Starter pictures.** `StarterBuilder` reads `Starters/<stem>/pictures/*.jpg` (fails on a count
  mismatch) and the coverage tests stay green. Three Little Pigs is rebuilt when the user's pictures
  arrive (re-split pages and re-translate if needed): not done, waiting on the pictures.
- [x] 4. Rules in `src/DinoReader.App/CLAUDE.md` and `src/DinoReader.Core/CLAUDE.md` (written at planning;
  check they still match).

## Verification

- Unit (`tests/DinoReader.Tests`): natural order (`1, 2, 10`), the `---` split with CRLF, spaces, a trailing
  separator and empty pages; filling empty slots in order and leaving filled ones alone; a picture + text
  story round-trips through `StoryPackageWriter`/`Reader`; the builder fails on a count mismatch.
- Emulator (device-test): add a 3-page book from pasted text with two `---` lines, then "Pick pictures
  for all pages" with three pictures (a page left without one reads as text). Sideways it shows the
  picture left and text right with the highlight moving; upright it shows the text panel over the
  picture. A PDF that has pictures shows no slots. With Farsi chosen, an English
  picture + text starter reads in Farsi with the text side unchanged.
- Windows head: the side-by-side layout in a wide window.

Build notes: starter pictures must be supplied already sized (copied as-is, no re-encode); slot mode has no drag-reorder (delete and edit go through the text editor).

#### For CLAUDE.md

- `PictureTextBook` (`src/DinoReader.Core`) holds the pure parts: natural file-name sort, `ImportPages` (splits on a `---` line once, only for `.txt` and typed text, never PDF or DOCX) and `FillEmptySlots`. (Slice 98: docs/plan/slices/98-dino-picture-and-text-books.md)
- A page with an `ImageFile` and `Text` but no `Lines` is a picture + text page; no schema change. `PictureTextPages` draws it (sideways: picture left, text right, no dots; upright: full picture with a 92% panel of up to 58% height; each sentence a tappable span); a mixed book stays in this layout. (Slice 98: docs/plan/slices/98-dino-picture-and-text-books.md)
- Rotate/crop must keep a picture + text page's text and status (`KeepPictureText`). (Slice 98: docs/plan/slices/98-dino-picture-and-text-books.md)
- `StarterBuilder` reads `Starters/<stem>/pictures/*.jpg` and copies them as-is, so supply them pre-sized; the build fails if the count differs from the pages. (Slice 98: docs/plan/slices/98-dino-picture-and-text-books.md)

#### Verified on the emulator (2026-10-24, RTST_Test)

- A 3-page .txt shows 3 picture slots. Picking 10, 2, 1 fills them as 1, 2, 10. Next is never blocked, including with an empty slot.
- Sideways: picture on the left, text on the right, no dots, title in the bar, the highlight moves, and tapping a sentence reads from it.
- Upright: picture fitted with the white text panel over it, the highlight and page turns work, and tapping a sentence works.
- Rotating a picture keeps its text and the picture + text layout.
- After the run, fixed in 34dc109 (not re-run on the emulator): Rotate and Crop added to the slot menu, and the sideways controls moved under the text half.

#### Not yet verified

- Emulator result for the picture + text flow (result to follow from the caller).
- Windows head: the side-by-side layout in a wide window.
- Three Little Pigs pictures from the user; the starter is not rebuilt.
- A native read of the new `Pictures_*` strings (the non-English ones are Claude drafts).
