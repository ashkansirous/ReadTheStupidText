# Slice 237 — Dino: a silent back cover (credits page) at the end of every book

*Built 2026-11-01, merged as PR #688 (0a206a23). Android emulator back-cover flow passed; the checks under "Not yet verified" are open.* From the user, after "Freddy and the Slipper" shipped: "update the relevant skills to always
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

- [x] **1. Smallest end to end, one book:** `StoryPage` gets a back-cover kind (no sentences, never voiced);
  the builder appends it from book.json; the reader shows it (Android and iOS share the MAUI code, Windows too);
  `ReadingSession`/segmentation skip it. Freddy and the Slipper first.
- [x] **2. Strings:** the labels and month names in all 14 languages (resx), the right-to-left layout for fa/ar.
- [x] **3. All 16 books:** `storyBy`/`retold` in every book.json (own books: Good Enough, X Marks the Treasure,
  Freddy; classics with their original authors: Beatrix Potter, Aesop, Andersen, Grimm and so on, traditional
  tales as "A traditional tale"). The writer checks each original author. Stamp every book (new version, so
  installed copies update), rebuild the starters.
- [x] **4. Builder and checks:** `check` fails for a book without `storyBy`; the store's `pageCount` and `minutes`
  leave the page out; the hash covers the new fields.
- [x] **5. Skills:** create-dino-story step 1-2 and the writer agent fill `storyBy`/`retold`; the reviewer checks the
  back cover in full mode; dino-storytelling mentions it.
- [x] **6. Tests:** builder (page appended, not counted), Core (never in the sentence list, never voiced), all
  books have `storyBy`.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and `BookBuilder check`.
- Android emulator (shared MAUI UI): open Freddy, read to the end, the back cover shows and nothing is spoken;
  same in English and Farsi (right to left), in picture-only and text-only modes.
- iOS: compile check (`-p:IncludeIos=true`); TestFlight build on merge.
- Windows head: Dino on Windows shows the back cover (only if the page code differs per platform).
- Old app: install the current store build, download a stamped book, confirm it reads the credits aloud and
  nothing breaks.

## Built

Built 2026-11-01. All six work items are done. Android emulator 2026-11-01, back-cover flow: PASS (`BACKCOVER shown lines=4`, nothing spoken), portrait screenshot good.

Changed from plan:
- `storyBy` holds several names separated by `;`, joined with a localized "and" (`BackCover_And`), so the English "and" no longer shows in the other 13 languages.
- The back cover is `Story.BackCover`, kept outside `Pages`. In story.json it is one extra last page with English text, so old apps read it aloud, and it is stripped on load.
- Play on the back cover restarts from sentence 0.
- The credit font is 20 pt and shrinks to a 13 pt minimum; the picture shrinks to fit.
- Goldilocks is credited to Robert Southey and Red Riding Hood to Charles Perrault and the Brothers Grimm (not "traditional").
- `check` fails a book without `storyBy`, and a retold book whose `storyBy` is Ashkan.

From review:
- review read: D:\ReadTheStupidText-worktrees\reviews\s237-high.md, findings 11/11. Applied 1-6, 9, 10, 11. Finding 7 (old installed apps read, translate and count the back cover) is the accepted trade-off of this slice. Finding 8 (`BackCoverEnglish` duplicates English labels and month names from the resx; `Lines()` repeats the traditional branch) was skipped; the brief gave no reason.
- review read: D:\ReadTheStupidText-worktrees\reviews\s237-medium.md, findings 8/8. Applied 1, 2, 4, 6, 7. Skipped: 3 (`SplitBackCover` drops a back cover silently when `TryCreate` fails, and a non-last-page `backCover` is read as a normal page), 5 (`JoinNames` chains "A and B and C" for 3+ names, wrong in list-comma languages; no book has 3 names today), 8 (the date shown is the Added month, so older books show the store-add month; info only, by design). Reasons for 3 and 5 were not given in the brief.

#### Design (Dino Reader Batch 45 mock, frames 147-149, added 2026-11-01)

The back cover was built without a mock; frames 147 (tablet upright), 148 (phone landscape) and 149 (Farsi, phone) now exist in `docs/design/dino-reader/Dino Reader Batch 45 Tablet Upright.dc.html` (README index has the lines). The mock's own review box says the build should be checked against them. They illustrate the built screen; anything new waits for the user's approval of Batch 45. Differences from what was built:

- **Credit size:** the mock uses 22 sp on a tablet upright and 17 sp in phone landscape; the build uses 20 pt shrinking to a 13 pt minimum (17 fits, 22 does not). Default: allow 22 when Slice 234 builds the tablet layout.
- **Landscape (148):** cover on the left, credit lines on the right, left-aligned, in one middle column; chip and rail as in 113. Still unverified in the build ("Landscape layout" below). Default: build to the frame.
- **Farsi (149):** only the credit lines run right to left, in Vazirmatn with Persian digits; chip and controls are not mirrored (Slice 96). The Farsi labels in the frame are drafts for farsi-review.
- **Controls:** Play becomes "Read again" (restart arrow), next is dimmed, no highlight, no view switch. This slice's text says the page shows in every reading mode; the mock hides the switch on this page (no text to switch). Default: follow the mock.

#### For CLAUDE.md (lifted 2026-11-01)

- In `src/DinoReader.App/CLAUDE.md`: every `book.json` needs `storyBy` (and `retold` for classics). The back cover is never voiced or counted (not in `pageCount`, `minutes` or the sentence list). (Slice 237: docs/plan/slices/237-dino-back-cover-credits.md)

#### Not yet verified

Verified 2026-11-01 ~23:45 on emulator-5554 (Dino Debug, origin/main 5d86be52), before night 2026-11-01-2355: the back cover in portrait (back-cover flow) PASS again. Landscape, Farsi digits, picture-only and text-only were not run and stay below.

- Landscape layout of the back cover.
- Farsi/Arabic right-to-left back cover and Persian digits.
- Picture-only and text-only reading modes on a device.
- Old installed app reading the page aloud without breaking.
- iOS and Windows on a device (compile only).
- The ar/ru/ja/zh labels by a native speaker.
- Credit-line wrapping in long languages on a narrow phone (estimated, not measured).
