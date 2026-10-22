# Slice 76 — Soft line-break reflow and heading detection

*In progress (wave 2026-10-22), draft PR.*

**Batch:** 13
**Decisions:** 83
**Depends on:** —
**Touches:** `src/ReadTheStupidText.Application/Reading/LineReflow.cs` (new), `src/ReadTheStupidText.Application/Reading/LineClass.cs` (new), `src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs`, `src/ReadTheStupidText.Application/Images/Ocr/OcrReading.cs`, `src/DinoReader.Core/StorySentences.cs`, `src/DinoReader.Core/SentenceBoundaries.cs`, `src/DinoReader.Core/StorySegmenter.cs`, `src/DinoReader.Core/ParagraphBreak.cs`, `tests/ReadTheStupidText.Tests/LineReflowTests.cs` (new), `tests/ReadTheStupidText.Tests/SpeechTextChunkerTests.cs`, `tests/DinoReader.Tests/StorySegmenterTests.cs`, `tests/DinoReader.Tests/LineReflowFixtures/**` (new)
**Story issue:** —

## Goal

A single newline is no longer a sentence end. Text copied from a PDF or a web page, a
scanned page, or a photo of a picture book reads as its author meant it: wrapped lines are
one sentence, hyphenated words are rejoined, blank lines are paragraphs, and **headings and
list items are still read as their own units** even when they have no terminal punctuation.
Both apps, every read path, every existing story, no migration.

## Why

`SpeechTextChunker` (RTST) has a bare `\r?\n` in its sentence-break regex, and Dino's
`SentenceBoundaries` ends a sentence "at a line break". OCR hands back one line per visual
row (`ImportedText.PageText` joins with `\n`), so every wrapped line becomes a sentence of
its own: "Now they have a good view, but" *pause* "Carrot wants to get even closer."
(reported 2026-10-22, image 1). The naive fix, joining every `\n`, glues headings onto the
paragraph below them ("Where to Find and Buy Ebooks Major Retailers: You can buy…"), so the
join needs a line classifier.

## Decisions locked with the user (2026-10-22)

- **Read time, not import time.** Stored text stays as written; reflow runs where text is
  split into sentences. Existing stories and clipboard text are fixed with no migration,
  and Dino's per-line `PageLine` boxes stay valid (the text they index is unchanged).
- **Classifier, not "break on every unpunctuated line".** One pure `LineReflow` in
  Application, used by both apps, taking optional line geometry so the OCR path gets the
  stronger signal and plain text degrades gracefully.
- Bold is not observable from any OCR engine here (ML Kit, Vision, Windows OCR report no
  weight); size and spacing stand in for it. The UIA `FontWeight`/`FontSize` attribute
  signal for the Windows read path is a follow-up, not part of this slice.

## The rule

For a block of lines (a page, a clipboard text, a paragraph between blank lines):

1. **Blank line** (`\n\n`, or whitespace-only line) → paragraph break. Unchanged.
2. **List item:** a line whose trimmed start is a marker — `•`, `·`, `-`, `–`, `—`, `*`,
   `▪`, `○`, `1.`/`12)`/`a)`/`(i)`, `☐`/`☑` — starts a new unit; a break is inserted before
   it. Markers themselves are not spoken (the chunker already strips most; make it one
   shared list).
3. **Heading:** a line with **no terminal punctuation** (`.!?…:;,`) that is **also**
   - *with geometry* (OCR lines carrying `OcrBox`): height ≥ 1.25× the block's median line
     height, **or** the vertical gap to the next line ≥ 1.5× the block's median inter-line
     gap; **or**
   - *without geometry* (plain text): length ≤ 60% of the median length of its neighbouring
     lines in the block, **and** Title Case or ALL CAPS (ignoring stop words: a, an, the,
     of, and, or, to, in, for, on, with…), **and** the next line starts with an upper-case
     letter, a digit or a list marker.
   A heading is its own unit with a paragraph break before and after it (so it gets the
   paragraph pause, not the sentence pause).
4. **Hyphen wrap:** a line ending in `-` or `‐` whose next line starts with a lower-case
   letter → join with no space and drop the hyphen ("foot-" + "ball" → "football"). If the
   next line starts upper-case, it is a compound ("Anglo-" + "Saxon") → join with no space
   and keep the hyphen. A line ending in a spaced dash (` -`, `–`, `—`) is prose → rule 5.
5. **Everything else:** `\n` → a single space.
6. A line ending in `.!?…` ends a sentence regardless (so poems and lists of short
   sentences keep their rhythm).

The median-based thresholds are constants in `LineReflow` (named, not magic numbers), tuned
against the fixtures below.

## Steps

*Built 2026-10-22 (wave 2026-10-22), PR #233, not yet verified on a device.*

*Changed while building:* scan paths use the plain-text rule only (`OcrReading.ReflowedText`); the geometry wiring (RTST scan text, Dino per-page `Lines` to `LineBreakMap`) moved to Slice 79 together with review-2 #2 (a geometry heading needs a text-shape check). Fixtures are inline strings in the tests, not files. Bullets stay in unit text. Hyphen rule per spec ("well-
known" becomes "wellknown", `DroppedHyphenBreaks` keeps `LocateChunk` aligned). Adjacent headings are separate units. A heading inside a quote is ignored by the segmenter but used by sentences (accepted). Mismatched boxes fall back to plain text. `StoryVoicing` builds the `LineBreakMap` once; headings are their own narration segment. Added `LineReflow.ReflowText`, `BreakMap`, `JoinUnits` + `UnitSeparator`.

*From review:* reviews in `<worktrees>/reviews/76-review-{1,2}.md`. Pass 1: fixed 1, 2, 3, 5, 6, 7, 8, 9; 4 partly; 10 went to docs. Pass 2: fixed 1, 3, 4, 5, 6, 7, 8; 2 moved to Slice 79. Tests: RTST 683 pass / 2 skipped, Dino 360 pass; App x64, Mobile and Dino Android build.

- [x] `LineReflow.Reflow(IReadOnlyList<ReflowLine> lines) → IReadOnlyList<ReflowUnit>` in
      `Application/Reading`, where `ReflowLine(string Text, OcrBox? Box)` and
      `ReflowUnit(string Text, UnitKind Kind)` with `UnitKind { Paragraph, Heading, ListItem }`.
      Pure, no I/O. A `LineReflow.FromPlainText(string)` helper splits on `\n` and calls it
      with `Box = null`.
- [x] `LineClass` — the classifier (`IsHeading`, `IsListItem`, `IsHyphenWrap`), one method
      each, 3–30 lines, with the constants named.
- [x] RTST: `SpeechTextChunker` calls `LineReflow.FromPlainText` before its sentence regex and
      removes the bare `\r?\n` alternative from `SentenceBreak`. Paragraph pause after a
      `Heading` unit; existing `\n\n` paragraph tests still pass.
- [ ] RTST Windows and Mobile OCR/scan paths (moved to Slice 79): where the scan result is turned into text to
      read (RTST Mobile `ScanPages`-equivalent and the Windows OCR read), pass the `OcrLine`
      boxes through `LineReflow.Reflow` so the geometry rule applies. The *stored/displayed*
      text keeps its `\n`s.
- [x] Dino: `StorySentences` / `SentenceBoundaries` stop treating `\n` as a sentence end and
      take their units from `LineReflow` — with boxes when the page has `Lines` (Slice 63),
      plain otherwise. `StorySegmenter` keeps blank-line paragraphs via `ParagraphBreak`; a
      `Heading` unit becomes its own `Segment` with `SegmentKind.Narration`.
- [ ] Dino `RangeToBoxMapper`: sentence spans that now cross a `\n` must still map to the
      right `PageLine`s — the mapper already handles multi-line sentences (Slice 64); add a
      test where a sentence spans two OCR lines joined by reflow.
- [x] Fixtures in `tests/DinoReader.Tests/LineReflowFixtures/` (text files + expected unit
      lists), shared with the RTST tests via a linked file or a copy: (a) the ebooks
      screenshot text (heading without punctuation + bullets with bold labels and colons,
      plain-text path), (b) the Bright Stadium page as `OcrLine`s with boxes (geometry path;
      the heading rule must flag "BRIGHT STADIUM" as a heading — Slice 79 later drops it as
      background, but here it must at least not glue onto the story), (c) a hyphen-wrapped
      paragraph, (d) a four-line poem with terminal punctuation (no false joins), (e) a short
      last line of a paragraph (must *not* be read as a heading because it ends in `.`).
- [x] `SpeechTextChunkerTests`: a single-`\n` wrapped paragraph becomes one sentence; a
      heading followed by a paragraph becomes two units with a paragraph pause.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` and
  `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green, with the five fixtures.
- Windows (Package profile): copy the ebooks page from a browser → the heading is read, a
  pause, then each bullet; a PDF paragraph with hard wraps reads as flowing sentences.
- Android emulator (`device-test`): the Slice 64 Carrot-and-Hazelnut story reads "Now they
  have a good view, but Carrot wants to get even closer." as one sentence with the highlight
  box covering both lines.

#### For CLAUDE.md (lifted 2026-10-22)

- `src/DinoReader.Core/CLAUDE.md`: a single newline is a sentence/segment boundary only where `LineReflow` (Application) says it is a real break; never split on bare `
`. Core does reference Application, so the "no project references" line is wrong. (Slice 76: docs/plan/slices/76-soft-line-break-reflow.md)
- Root `CLAUDE.md`, Application line: `LineReflow` is shared by both apps' read paths (`SpeechTextChunker`, Dino `StorySentences`/`StorySegmenter`). (Slice 76: docs/plan/slices/76-soft-line-break-reflow.md)

#### Not yet verified

- On a device, a pasted hard-wrapped email and a scanned book page read with no pause mid-sentence (RTST Android, RTST Windows, Dino).
