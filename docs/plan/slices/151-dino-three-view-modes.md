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

#### Design (Dino Reader Batch 31 mock, screens 66-69, added 2026-10-26)

Source: `docs/design/dino-reader/Dino Reader Batch 31.dc.html`, screens 66 (lines 28-52), 67 (54-75), 68 (77-98) and the cast page, screen 69 (lines 105-120). Four frames at 360x760, real UI. The mock agrees with Decision 156 on the three modes, the Picture-to-Picture + text migration and the removals. It adds the segment control and a few settled details; the segment order and icons are marked "a proposal" in the mock.

- **Top bar (all reading frames).** Back arrow 44x44. The title sits on its own row (`order:3`, full width), 20/600, line-height 1.25, padding 0/8. The switch sits at the right, in the old mode toggle's place. A flex row, gap 8, padding 8/12/4/8.
- **Segmented switch.** Container padding 3, gap 2, radius 16, fill `#F0E8D8`. Each segment 44x40 (44 px touch width), radius 13, icon 22 px, stroke 2.2, round caps. Selected: fill `#fff`, shadow `0 1px 4px rgba(43,37,64,.16)`, icon `#5B57E8`. Unselected: no fill, icon `#5a5470`. Order, left to right: **Picture only** (framed picture with a sun and hills), **Picture + text** (small framed picture over two text lines), **Text only** (text lines). TalkBack and VoiceOver read each segment by its name.
- **Frame 66, Picture + text (default for picture books).** Today's 30% panel (frame 56): picture 3:4, radius 18, `object-fit: contain`, page padding 6/14/0. Words panel inset 8 from left, right and bottom, `max-height: calc(30% - 8px)`, fill `rgba(255,255,255,.92)`, radius 16, padding 10/14, shadow `0 4px 12px rgba(43,37,64,.16)`, Andika 21 / line-height 1.5. Spoken text `#8a8399`, current sentence default colour, current word `#FFE27A` with radius 8 and a 4 px spread of the same colour. Page dots 9 px (current one 26x9, `#5B57E8`, others `#d9d2c4`), play button 72 px round, fill `#ECE7FF`, icon `#5B57E8`.
- **Frame 67, Picture only.** Picture, dots and play button as above, no words panel. Pages still turn with the reading. The highlight has no place to show but keeps tracking, so switching view mid-sentence lands on the right word. A page with no picture falls back to its text card. Same layout sideways.
- **Frame 68, Text only.** Today's text mode, unchanged. Books without pictures (typed, text files) have only this view: the switch is hidden and the top bar shows just the back button and the title. "Show original text" on translated books (Slice 110) stays in the book options sheet.
- **Saved choice.** Saved per book. A book saved as `Picture` opens in Picture + text, whatever its old words-over value (the plan's migration).
- **Cast page, frame 69 (the 151 part).** Supersedes frame 59 and the bottom of frame 58. No "Merge two characters" button and no words-over switch. Header: back 44x44, "Who’s speaking" 20/600, sub-line "The Three Billy Goats Gruff · English" 12 `#8a8399`. Narrator card: white, radius 18, min-height 60, a 34 px dark round badge, "Narrator · Story Granny" 15/600, link "Change for all books" 13/600 `#5B57E8` (24 px min height). Character list: one white card, radius 18, rows padding 12/14 with a 1 px `#efe9dd` divider; 34 px round letter badge (`#8FA66B`, `#C9A24A`, `#6B5B8A`), name 15/600, "MAIN" chip 10.5/600 `#5B57E8` on `#ECE7FF` radius 6, "62 words" 12 `#8a8399`; under the name, indented 44, the voice pick ("Brave Knight") with two icon buttons. Merges saved in older books are ignored, so those names get their own rows again.
- **New strings (14 locales).** The three mode names, used as the segments' accessibility names: "Picture only", "Picture + text", "Text only". The design shows no visible labels. Remove the merge and words-over strings.

**Open point: Segment order and icons.** The plan lists the modes as "Picture only, Text only, Picture + text". The mock orders them Picture only, Picture + text, Text only, and calls the order and icons a proposal. **Default:** use the mock's order and icons.

**Open point: Cast page frame is a draft.** The mock marks the book (Billy Goats Gruff) and the voice picks as drafts. **Default:** take only the layout and the removals from it; the real rows come from `CastRows`.
