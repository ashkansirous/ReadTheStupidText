# Slice 237 — Dino: a silent back cover (credits page) at the end of every book

*Planned 2026-11-01.* From the user, after "Freddy and the Slipper" shipped: "update the relevant skills to always
generate a back cover page. It should write the credits, basically who wrote the original story, and for the ones
that was not written by me, you should write edited and made suitable for young kids by Ashkan Sirous (use better
wording) and then next line should be the year and month. Illustration: Ashkan Sirous. like September 2026. The back
cover/credits page does not need to be read."

**Depends on:** none (Freddy's store-card credits are fixed separately, branch `fix/dino-freddy-credits`).
**Touches:** `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson*.cs`, `src/DinoReader.Core/ReadingSession.cs`, `src/DinoReader.Core/ScanLayout*.cs`, `src/DinoReader.Core/BackCover*.cs` (new), `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/TextPages.cs`, `src/DinoReader.App/PicturePages.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/Resources/Raw/starters/*.dino`, `tools/DinoReader.BookBuilder/**`, `content/dino/*/book.json`, `tests/DinoReader.Tests/**`, `.claude/skills/create-dino-story/SKILL.md`, `.claude/skills/dino-storytelling/SKILL.md`, `.claude/agents/dino-story-writer.md`, `.claude/agents/dino-story-reviewer.md`

## Why

Books end on their last story page with no credits inside the book. Credits show only on the store card
(`creditsByLanguage`, StorePage). The user wants every book to close with a back cover that says who made it,
shown but never read aloud.

## Decisions (2026-11-01, the user)

- **All 16 books** get the back cover, starters and store books, and the create-dino-story skill makes one for
  every new book from now on.
- **Old app versions** may read the page aloud like a normal page until they update. No `.dino` schema bump
  (a bump would hide every book from old apps).
- **Wording** for books the user did not write: "Retold for young readers by Ashkan Sirous".
- **Content**, one item per line:
  1. Who wrote the story. Own books: "Story by Ashkan Sirous". Freddy: "Story by Elham Shafiei Gol and Ashkan
     Sirous". Classics: the original author, e.g. "Original story by Beatrix Potter" or "A traditional tale",
     then "Retold for young readers by Ashkan Sirous".
  2. "Illustration: Ashkan Sirous".
  3. Year and month, e.g. "September 2026".
- The page is **not read aloud**.

## Decided by me (recommended defaults; the user may change them)

- **Month:** the book's existing `added` field (book.json), shown as the month name and year in the reading
  language ("November 2026", «نوامبر ۲۰۲۶»...). No new date field.
- **Data:** new book.json fields `storyBy` (names, as written) and `retold` (bool); the illustrator is fixed
  ("Ashkan Sirous") unless a book sets `illustration`. The builder generates the page from them. Nobody types
  the page into story.md.
- **Languages:** the labels ("Story by", "Original story by", "A traditional tale", "Retold for young readers
  by", "Illustration:") are app strings in all 14 languages, so they follow the reading language. Names are
  never translated; a book may give transliterations per language if the writer finds the other books do so.
- **Look:** the book's colour as the background, the small cover picture at the top, the credit lines centred
  below, no reading cursor and no word highlighting. It shows in every reading mode (picture-and-text, text-only,
  picture-only).
- **Reading:** auto-play stops on the last story page as now; turning the page by hand shows the back cover.
  The page counts for neither the store's `pageCount` nor `minutes`.

## Work

- [ ] **1. Smallest end to end, one book:** `StoryPage` gets a back-cover kind (no sentences, never voiced);
  the builder appends it from book.json; the reader shows it (Android and iOS share the MAUI code, Windows too);
  `ReadingSession`/segmentation skip it. Freddy and the Slipper first.
- [ ] **2. Strings:** the labels and month names in all 14 languages (resx), the right-to-left layout for fa/ar.
- [ ] **3. All 16 books:** `storyBy`/`retold` in every book.json (own books: Good Enough, X Marks the Treasure,
  Freddy; classics with their original authors: Beatrix Potter, Aesop, Andersen, Grimm and so on, traditional
  tales as "A traditional tale"). The writer checks each original author. Stamp every book (new version, so
  installed copies update), rebuild the starters.
- [ ] **4. Builder and checks:** `check` fails for a book without `storyBy`; the store's `pageCount` and `minutes`
  leave the page out; the hash covers the new fields.
- [ ] **5. Skills:** create-dino-story step 1-2 and the writer agent fill `storyBy`/`retold`; the reviewer checks the
  back cover in full mode; dino-storytelling mentions it.
- [ ] **6. Tests:** builder (page appended, not counted), Core (never in the sentence list, never voiced), all
  books have `storyBy`.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and `BookBuilder check`.
- Android emulator (shared MAUI UI): open Freddy, read to the end, the back cover shows and nothing is spoken;
  same in English and Farsi (right to left), in picture-only and text-only modes.
- iOS: compile check (`-p:IncludeIos=true`); TestFlight build on merge.
- Windows head: Dino on Windows shows the back cover (only if the page code differs per platform).
- Old app: install the current store build, download a stamped book, confirm it reads the credits aloud and
  nothing breaks.
