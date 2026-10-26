# Slice 151 — Dino: three view modes, and no merging of characters

*Planned 2026-10-26.* Dino Reader only.

**Batch:** 31 (tester review, 2026-10-26; Decision 156)
**Touches:** `src/DinoReader.Core/ReadingMode.cs`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryCast.cs`, `src/DinoReader.Core/CastMerges.cs` (deleted), `src/DinoReader.Core/MergeSelection.cs` (deleted), `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/PicturePages.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/CastMergeSheet.cs` (deleted), `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.Core/CLAUDE.md`

## Why

From the tester review (2026-10-26):
- Small children want a picture-only view.
- The per-book "Words over pictures" switch on the cast page (Slice 122, `Story.WordsOverPictures`) is a
  second, hidden way to choose a view.
- The "Merge two characters" button (Slice 65, `CastMergeSheet`) is not something parents need.

## Decisions locked with the user (Decision 156)

- **Three modes** in the reading top bar: **Picture only**, **Text only**, **Picture + text**. Picture + text
  is today's picture layout with the words.
- Picture only shows the picture with no words and reads it aloud as usual.
- Books with pictures open in **Picture + text**. Books without pictures have Text only (the switch is hidden).
- Saved choices carry over: the old `Picture` becomes Picture + text, and `Text` stays Text.
- The **"Words over pictures" switch is removed** (`Story.WordsOverPictures` is ignored when read, and no longer
  written).
- **"Merge two characters" is removed**: the button, the sheet, `CastMerges`/`MergeSelection` and
  `Story.Merges`. Merges already saved in a book are ignored, so those names get their own voices again.
- "Show original text" (translated books, Slice 110) stays.
- The `.dino` schema version does not change: the old fields are read and ignored, so older files still open.

## Work

- [ ] `ReadingMode`: `Picture`, `Text`, `PictureAndText`. Migration in the `story.json` read: an old `Picture`
      (with either `WordsOverPictures` value) becomes `PictureAndText`. Unit tests for old files.
- [ ] Reading top bar: a three-icon switch saved per book, as today. Picture only hides the words panel. The
      highlight and page turns still follow the read.
- [ ] Remove the Words over pictures row from the cast page.
- [ ] Remove the merge button, the sheet, the merge model and `StoryCast.Resolve`'s merge step. Tests updated or deleted.
- [ ] Strings: add the three mode names (14 locales) and remove the merge and words-over strings.
- [ ] CLAUDE.md lines: replace the Slice 98/122 view rule and the Slice 65 merge rule.

## Verification

- `dotnet test tests/DinoReader.Tests`.
- Emulator, with a picture book and a text-only book:
  - All three modes, in both orientations.
  - An installed book saved as Picture opens in Picture + text.
  - A book that had a merge reads with separate voices.
  - The cast page has no merge button and no Words over pictures switch.
