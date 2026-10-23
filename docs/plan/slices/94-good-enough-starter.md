# Slice 94 — "Good Enough" ships as a starter story

*Planned.* 2026-10-23, from the user's request "I want my book to be available by default".

**Batch:** 17 (Dino Reader content, part 2)
**Depends on:** Slice 91 (starters, `.dino` package), Slice 64 (picture mode line boxes)
**Touches:** `tests/DinoReader.Tests/StarterBuilder.cs`, `tests/DinoReader.Tests/StarterTexts.cs`, `tests/DinoReader.Tests/StarterStoryTests.cs`, `tests/DinoReader.Tests/Starters/good-enough/**` (new), `tools/ocr-starter-pages.ps1` (new), `src/DinoReader.App/StarterStories.cs`, `src/DinoReader.App/Resources/Raw/starters/good-enough.dino` (new), `src/DinoReader.BookSample/**` (removed), `src/DinoReader.App/CLAUDE.md`
**Story issue:** —

## Why

The user wrote and illustrated a picture book, "Good Enough" (Carrot the rabbit and Hazelnut the
squirrel at Bright Stadium, 10 pages plus a cover). It should be on every shelf out of the box, as
the first story, read in picture mode with the sentence boxed on the page.

## Decisions (2026-10-23)

- The source pictures move from the untracked `src/DinoReader.BookSample/` to
  `tests/DinoReader.Tests/Starters/good-enough/` (builder input, not app code). `Cover.jpg` becomes the
  story's cover photo (`CoverStyle.Picture`, `cover-<id>.jpg`); pages `1.jpg`-`10.jpg` are the pages.
  **The back cover is not shipped:** it is a blurb with a placeholder "PUBLISHER" mark, not story text.
- The text is printed in the pictures. Line text and boxes come from Windows OCR
  (`Windows.Media.Ocr`, run once by `tools/ocr-starter-pages.ps1`), the text is proofread by hand, and
  only the caption lines are kept: words painted into the scene ("BRIGHT STADIUM", banners) are not
  page text. The result is checked in as `lines.json` next to the pictures, so the build is repeatable
  without OCR.
- Pictures are re-encoded to JPEG quality 85 at their own size (896x1200), so the asset stays a few MB.
- New package id; it is first in `StarterStories.AssetNames`, so it is the newest tile. Existing installs
  get it on update too: starters are recorded per package id (Slice 91), and a deleted one still never
  returns.
- Language `en`, cast by the builder like the other starters. It ships with all eight translations
  (Slice 95).

## Work

- [ ] 1. **OCR the pages once.** `tools/ocr-starter-pages.ps1 <folder>` writes `lines.json` (per page:
  lines with text and a 0..1 box). Proofread the text against the pictures, drop the scene words.
- [ ] 2. **Builder.** `StarterBuilder` gains a picture starter: pages with `ImageFile`, `Lines` and the
  text made from those lines (so `Story.BreakMap` uses the geometry), cover photo, cast. Writes
  `good-enough.dino`.
- [ ] 3. **Install.** Add `good-enough.dino` first in `StarterStories.AssetNames`. Delete
  `src/DinoReader.BookSample/`.
- [ ] 4. Tests: the shipped package opens, every page has a picture and boxed lines, the cover is the
  cover photo, Carrot and Hazelnut each get their own voice.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Emulator: a fresh install shows "Good Enough" first; picture mode boxes each caption sentence as it
  is spoken; an install that already had the three starters gains it after an update.
