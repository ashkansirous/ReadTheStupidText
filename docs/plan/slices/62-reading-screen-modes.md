# Slice 62 — Reading screen: text and picture modes

*In progress (wave 2026-10-21b), draft PR.*

**Batch:** 10
**Decisions:** 79, 80
**Depends on:** Slices 60, 61
**Touches:** `src/DinoReader.App/**`, `src/ReadTheStupidText.Application/Reading/**`, `src/DinoReader.Core/**`, `tests/DinoReader.Tests/**`, `CLAUDE.md`
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

#### Design (Dino Reader v2 mock, screens 3, 4, 5, 12, 13, added 2026-10-20)

Source: `docs/design/dino-reader/Dino Reader v2.dc.html`. Five frames (generated page lines 30-31, text mode 31-32, wandered 32-35, bedtime 35-36, tablet 96-107; a sixth, Farsi, is in the RTL bullet). Screen 2 (real photo page) is Slice 64. The mock agrees with the plan on the two modes and the highlight unit; it adds pager dots, skip buttons, a wander pill, a bedtime theme and a tablet layout. Tokens: Slice 58 Design. Striped "scanned illustration" boxes are placeholders, not UI.

- **Top bar (all reading screens).** Wrapping row, gap 8 (row gap 2), padding 8/12/4/8. Back chevron 44x44 (22px icon, stroke 2.6), then a flexible gap, then the **grown-up lock** and the mode switch. Story title sits on its own full-width row below them (order 3), padding 0 8, 20/600, line-height 1.25, balanced wrap, so a long title wraps instead of truncating. **Lock** (new in the refreshed mock): 40x40, radius 12, fill `rgba(43,37,64,.07)` (bedtime `rgba(255,255,255,.07)`), 18px line padlock (stroke 2, `#8a8399`; bedtime `#8e86a8`); it opens the Slice 61 gate. **Mode switch**: a segmented pair, track `rgba(43,37,64,.07)`, radius 16, padding 4, gap 4; each cell 52x44 radius 12 with a 24px line icon (stroke 2.2). Left cell = open book (text mode), right cell = picture frame (picture mode). Active cell: fill `#5B57E8`, under-edge `0 2px 0 #3f3bc4`, white icon; inactive icon `#8a8399`.
- **Picture mode, generated page (screen 3).** Page card margin 6/14/0, radius 24, fill `#FFE9B0` (the wandered page uses `#DDEBFF`, so pages carry different pastel backgrounds), padding 24/22, text vertically centred. Text Andika 25, line-height 1.55, `#2b2540`, one block per laid-out line, gap 3. Highlight: each line of the current sentence gets fill `rgba(255,214,0,.42)`, inset 2px outline `#F5B800`, radius 9, extended 6px sideways. A two-line sentence is two stacked rounded boxes.
- **Text mode (screen 4).** White card radius 26, padding 22, Andika 25 / line-height 1.6, `#2b2540`. Already-read text `#b3abc2`. Current sentence or dialogue line: fill `#FFE27A`, radius 8, 5px glow `0 0 0 5px #FFE27A`. Unit = sentence or dialogue line, never a word, never cut mid-sentence. Tapping any sentence reads from there.
- **Pager dots.** Below the page, centred, gap 8, padding 12/0/2: idle dot 9x9 `#d9d2c4`; current 26x9 radius 5 `#5B57E8`. One dot per page.
- **Transport row.** Centred, gap 26, padding 12/0/26: previous 72 circle `#ECE7FF` with double-left chevron (30px, stroke 2.6, `#5B57E8`); play/pause 104 circle `#FF7A59`, under-edge `0 8px 0 #E2583A`, white pause bars (40px glyph, two 3.4x11 rects radius 1.4); next 72 circle, double-right chevron. The mock shows only the pause glyph.
- **Wandered state (screen 5).** The child swiped ahead while reading goes on. A pill floats centred at top 150: fill `#2b2540`, white 13.5/600, radius 20, padding 8/14, shadow `0 6px 16px rgba(43,37,64,.3)`, a `#FFE27A` marker icon 16px, text "Reading page 2". Play gets an extra halo `0 0 0 10px rgba(255,122,89,.22)`. Rule: auto-follow pauses; tap any sentence to read from there, or press Play to jump back and follow again.
- **Right-to-left (screen 14, Farsi; lines 84-88).** The whole screen mirrors for fa and ar (`dir="rtl"`, font Vazirmatn): back arrow flipped (`scaleX(-1)`, points right) at the right edge, lock and mode switch at the left, top bar padding 8/8/4/12. Title "پیتر و دُم‌پنبه" 20/700, line-height 1.3. Page card margin 6/14/0, radius 24, fill `#DFF5E3`, padding 24/22, text Vazirmatn 24 / line-height 1.8, `#2b2540`, lines right-aligned, gap 3. Quotes use «guillemets»: "«من هم بیام؟» پیتر پرسید." Spoken line boxed per laid-out line (fill `rgba(255,214,0,.42)`, radius 9, extended 6px sideways), in logical order: "دُم‌پنبه گفت: «اگه تند" then "بیای!» و دوید و رفت." Pager dots run right to left (current 26x9 `#5B57E8`). Pages run right to left: swipe left for the next page. The status bar stays left-to-right. Per the caption, Farsi copy is machine-drafted.
- **Bedtime theme (screen 12).** Palette in Slice 58 Design; same layout. "It follows the phone’s dark mode, or a grown-up can switch it on."
- **Tablet (screen 13, 1000x640).** Header title 22/600 plus "Page 2 of 5" 15/600 `#8a8399` before the mode switch. Body: a two-page spread (left picture, right text on `#f7f1e3`; radius 8; shadow `0 8px 24px rgba(43,37,64,.18)`) and a 140px side column of controls (skip 80, play 120 with a 46px glyph, skip 80, gap 24). No pager dots.

**Open point: Mode icons.** Plan says the emoji book and picture. The mock uses line icons. **Default:** use the mock’s icons, not emoji.
**Open point: Skip buttons.** The plan has no previous/next controls; the mock has two double-chevron buttons around Play. **Default:** previous/next sentence (the highlight unit).
**Open point: Pager dots.** Plan has swipe only; the mock adds page dots. **Default:** add them, display-only.
**Open point: Wander pill.** Plan: auto-follow stops. The mock adds the "Reading page N" pill. **Default:** add it, inert.
**Open point: Lock on the reading bar.** Settled by the mock (Slice 61 caption): the gate opens from any reading screen and pauses the story. **Default:** add the lock as above; pause on open, resume on "Back to the story".
**Open point: Title row.** The plan and the earlier mock had a one-line ellipsised title. The refreshed mock wraps it on its own row at 20/600. **Default:** follow the mock.
**Open point: Bedtime theme.** Not in the plan. The mock does not show where the manual grown-up switch lives. **Default:** follow system dark mode only; no manual switch yet.
**Open point: Tablet layout.** Not in the plan. **Default:** later layout pass; phone first.
**Open point: RTL reading.** Plan: Slice 53 mirrors the layout for fa and ar. Screen 14 now draws it (flipped arrow, pages right to left). **Default:** follow the mock; swipe direction follows the layout direction.
**Open point: Default mode.** Plan: picture if the story has pictures, else text. Screen 3 shows a typed story in picture mode. **Default:** keep the plan’s default; the screen shows a child’s choice.

New strings (both locales): "Reading page {n}", "Page {n} of {m}"; the lock needs an accessibility label (use "For grown-ups").

## Acceptance

- Start a story: picture mode shows page 1 with the first sentence boxed; the box moves
  sentence by sentence and the page turns at the boundary.
- Switch to text mode mid-read: same position highlighted; the choice survives a restart.
- Swipe away: auto-follow stops; tap a sentence: reads from there and follows again.
- Unit tests for the composer's line boxes (no overlap, all text covered).

## Risks / notes

- Android only raises one chunk per sentence after Slice 60; before it nothing can follow.
- Large type means many pages; compose lazily per page.
