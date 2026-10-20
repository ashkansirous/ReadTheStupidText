# Slice 62 — Reading screen: text and picture modes

*Planned.*

**Batch:** 10
**Decisions:** 79, 80
**Depends on:** Slices 60, 61
**Touches:** `src/StoryReader.App/**`, `src/ReadTheStupidText.Application/Reading/**`, `src/StoryReader.Core/**`, `tests/StoryReader.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

The reading screen follows the spoken sentence in two modes the child can switch at any
time: 📖 text and 🖼 picture. In this slice picture mode uses **generated book-style pages**
for every story (Slice 64 adds real page pictures), because the generator's layout gives
the exact line boxes.

## Steps

- [ ] Reading screen with a big two-icon switch (📖 / 🖼) always visible in the top bar; the
      choice is saved per story in `story.json` (Slice 61); default is picture mode when the
      story has pictures, else text.
- [ ] **Text mode:** the story text paginated by the existing `TextBoxPaginator` (Application,
      reusable); the sentence / dialogue line being spoken is highlighted from `ChunkChanged`
      ranges (Slice 60). Highlight rendering is WinUI-only today
      (`ReadingTextBoxWindow.xaml.cs`), so write a MAUI one (`FormattedString` spans or a
      `GraphicsView`; decide at implementation).
- [ ] **Picture mode, generated pages:** a `PageComposer` lays the text onto book-style pages
      (big friendly type, page background, margins) and returns per page the line boxes
      (normalized `Rect`) of each sentence. Pure and unit-tested. The same line-box model is
      what Slices 63/64 fill from real pages.
- [ ] Highlight = translucent rounded box over the lines of the current sentence, animated
      between sentences; the sentence is the unit (Decision 79, no word karaoke).
- [ ] **Auto-follow:** the page turns by itself as reading reaches it (Decision 80). The child
      can swipe to another page; **tap a sentence** reads from there; pressing play resumes
      auto-follow.
- [ ] Sentence list comes from the Slice 59 segments split on sentence ends, so a highlight
      never ends mid-sentence.

## Acceptance

- Start a story: picture mode shows page 1 with the first sentence boxed; the box moves
  sentence by sentence and the page turns at the boundary.
- Switch to text mode mid-read: same position highlighted; the choice survives a restart.
- Swipe away: auto-follow stops; tap a sentence: reads from there and follows again.
- Unit tests for the composer's line boxes (no overlap, all text covered).

## Risks / notes

- Android only raises one chunk per sentence after Slice 60; before it nothing can follow.
- Large type means many pages; compose lazily per page.
