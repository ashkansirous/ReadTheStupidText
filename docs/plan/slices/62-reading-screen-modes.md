# Slice 62 — Reading screen: text and picture modes

*Built (wave 2026-10-21b), PR #200.*

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

- [x] Reading screen with a big two-icon switch (📖 / 🖼) always visible in the top bar; the
      choice is saved per story in `story.json` (Slice 61); default is picture mode when the
      story has pictures, else text.
- [x] **Text mode:** the story text paginated by `FlowPager` (Core), which fills pages with whole
      sentences on the same `StorySentences` the highlight uses (the Application `TextBoxPaginator`
      was not used: its sentence splitter differs). The sentence / dialogue line being spoken is
      highlighted from `ChunkChanged` ranges (Slice 60). Highlight rendering is WinUI-only today
      (`ReadingTextBoxWindow.xaml.cs`), so a MAUI one was written (`FormattedString` spans).
- [x] **Picture mode, generated pages:** a `PageComposer` lays the text onto book-style pages
      (big friendly type, page background, margins) and returns per page the line boxes
      (normalized `Rect`) of each sentence. Pure and unit-tested. The same line-box model is
      what Slices 63/64 fill from real pages.
- [x] Highlight = translucent rounded box over the lines of the current sentence, animated
      between sentences; the sentence is the unit (Decision 79, no word karaoke).
      *(Changed while building: the box fades in, it does not slide; see Decisions taken during build.)*
- [x] **Auto-follow:** the page turns by itself as reading reaches it (Decision 80). The child
      can swipe to another page; **tap a sentence** reads from there; pressing play resumes
      auto-follow.
- [x] Sentence list comes from the Slice 59 segments split on sentence ends, so a highlight
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

- The Android neural reader raises one chunk per planned chunk, not per sentence: a short
  paragraph is one chunk. So the reading screen cuts each voiced segment at the sentence starts
  before reading (`SegmentSentenceSplitter`), which gives the planner at least one chunk per
  sentence and makes `ChunkChanged` step the highlight sentence by sentence. A sentence longer than
  a chunk gives several chunks, all inside that sentence.
- The system-voice fallback joins the segments and raises one chunk `(0, length)` for the whole read:
  that range is not a story position, so it is placed at the read's first sentence and the mark does
  not follow the fallback voice sentence by sentence.
- Large type means many pages. Pages are composed for the whole story, on a worker thread, when the
  card gets a size (lazy per-page composition was not done); a resize waits 150 ms for the card to
  settle, and a theme change only re-colours.

## Decisions taken during build

- **No `TextBoxPaginator`.** Its sentence splitter differs from the highlight unit, so a highlight could straddle two pages. `FlowPager` (Core) paginates text mode on the same `StorySentences` the highlight uses; `PageComposer` does picture mode. No `Application/Reading` file was touched.
- **Sentence unit** (`StorySentences`): ends at a terminator plus closing quotes, at a line break, and between two adjacent quotes; a quote stays with its attribution (the mock's "Only if you're quick," said Cotton Tail, and off she ran. is one box). After a closing quote the sentence ends unless an attribution follows: a word starting lowercase (or in an uncased script), or a speech verb within the next three words ("Peter said."). A quote that ends with a comma, or that an attribution follows, is never split inside (`"Go away! I'm busy," said Tom.` is one sentence). The Arabic question mark and the guillemets are understood for every language. Rules are English for every story until Slice 66 adds Farsi ones: in an uncased script an attribution cannot be told from a new sentence, so a quote and the words after it stay in one box (the safe side; "never cut mid-sentence").
- **Picture mode:** every sentence starts a new line and is kept whole on one page when it fits; text is vertically centred; boxes are full text width plus 6 (as in the mock), one per line. Highlight moves by fade-in, not slide. Pages are composed for the whole story when the card gets a size (cheap with the Android `Paint` measurer), not lazily per page.
- **Text mode:** per-line labels of word spans; read text dim, the spoken sentence has a square-cornered highlight (a MAUI span cannot round or glow).
- **Story text ignores the system font scale** (`FontAutoScalingEnabled` false): the page geometry is measured, and scaling would overflow lines.
- **Narrator picker removed from the reading screen** (the mock has none; the narrator row belongs to Slice 65's cast page). Reads use the story's stored narrator; nothing to persist yet.
- **Mode saved per story** as `PreferredMode` in `story.json` (null = default: picture if the story has pictures, else text). Version stays 1: an old file reads as null.
- **Skip buttons** move by one sentence; while nothing plays they only move the mark. Play after a finished read starts from the top; after an idle swipe it starts at the shown page's first sentence; while wandering it jumps back and follows.
- **Pager dots** are hidden above 12 pages. The wander pill and Play halo show only while a read is active.
- **Bedtime theme** follows system dark mode only. Bedtime text mode (not drawn in the mock) reuses the card and amber highlight colours.
- **RTL:** FlowDirection and font follow the story language (fa/ar), and the swipe direction mirrors; text-mode taps are hit-tested from the line's right edge for RTL stories (words in logical order; embedded left-to-right runs are approximate). No RTL story can be added before Slice 66, so it is untested on a device. Tablet layout is not built.
- **Relayout keeps a wander:** a new size or mode remaps the page shown through the first sentence it held (`PageFollower.Relaid`); only a follower is moved to the reading page. Reading reaching the page a wanderer is looking at makes them a follower again.
- **Chunk events** are tagged with the active read id when raised and dropped when that read is no longer the active one (not by the reader's state). A finished read is forgotten when the mark moves or the child swipes, so Play starts where they are.
- **Lock:** a read that is still loading is stopped and restarted from its sentence on "Back to the story"; nothing resumes unless this page is on top and the app stayed in the foreground (the gate closes when the app is stopped).
- **Story file:** `PreferredMode` is read as a string and an unknown or numeric value reads as "no choice", so a newer or hand-edited file keeps its story.
- **Title weight:** the mock's 600 cannot be set: Fredoka is a variable font (wght 300-700) and MAUI's `FontAttributes.Bold` selects 700. Left bold.
- **Follow-up:** the loading grace (first-audio wait) is a copy of the RTST reader page's; sharing it needs the state machine moved into `ReadTheStupidText.Mobile.Shared`.
- **Lock** on the reading bar pauses the story, opens the gate, resumes on "Back to the story", and on a pass pops to the gallery and opens Manage.
- **Not done:** the "Page n of m" string (tablet only).
- Review fixes: text mode splits a sentence longer than a page by line (`FlowPager`), and picture mode starts such a sentence on a fresh page; page lookup is range-aware (`PageOf(sentence, position)`, fed by the chunk's `SourceStart` via `ReadingSession.CurrentPosition`). The surface always debounces size changes, lays out one at a time (newest wins) and keeps the previous renderer, or shows an error, when a layout fails. `Completed` only counts once the active read has played. Skip while paused only moves the mark (the paused read is dropped, so Play starts at the mark). One `AndroidTextMeasurer` per font, disposed with the surface.

## Final shape

**DinoReader.Core**

- `StorySentences`: terminators include the Arabic question mark (؟); quotes include «»; attributions are detected by a lowercase start or a speech verb.
- `SegmentSentenceSplitter`: voiced segments are cut at sentence starts, so `ChunkChanged` steps per sentence.
- `SegmentSlicer`: a read can start at a given sentence.
- `LineWrapper` / `LineCounter`: incremental wrapping, each word measured once.
- `FlowPager` (text mode) and `PageComposer` (picture mode, normalised line boxes). Both split overlong sentences across pages; page lookup is range-aware through the spoken position.
- `PageFollower`, with `Relaid` so a wander survives a relayout.
- `SkipPlanner`.
- `PreferredMode` in `story.json` (version still 1); unknown values read as null.

**DinoReader.App**

- `ReadingPage`: top bar (back, lock, text/picture switch), page card, pager dots, wander pill with the mock's shadow, prev/play/next.
- `ReadingSession`: read-id and playing-id guards, the system-voice fallback mapped to the read origin, Stop/start on the gate, resume only in the foreground.
- `ReadingSurface`: composition on a worker thread, debounced, newest request wins, the old renderer is kept on failure.
- One cached, disposable `AndroidTextMeasurer` per font.
- Bedtime theme follows system dark mode; RTL is chosen by primary language subtag.
- The narrator picker was removed from the reading screen, matching the mock. Reads use the story's stored narrator; the narrator becomes changeable again in Slice 65.

*Changed while building:* see "Decisions taken during build" above. Superseded plan text: the `TextBoxPaginator` reuse and the "animated between sentences" slide (now a fade-in), and the narrator picker on this screen.

*From review:*

- High pass: 16 findings, 14 fixed. Skipped: sharing the grace state machine with RTST (needs `ReadTheStupidText.Mobile.Shared`; follow-up) and one-line methods (repo convention).
- Medium pass: 10 findings, all fixed.

## Emulator results (RTST_Test, 2026-10-21)

- Seeded two-story gallery. In picture mode "Peter and Cotton Tail" shows the first sentence boxed, with 2 pager dots.
- Play shows the pause glyph. After about 3 s the box was on sentence 1; after about 9 s on sentence 3 ("Only if you're quick," said Cotton Tail, and off she ran.). The highlight moves per sentence: the core acceptance item passed.
- From the implementer's run: text mode in Andika with dim read text; tapping a sentence reads from it; a swipe turns the page and the dots update; the mode is saved to `story.json` and restored after a restart; the lock opens the gate over the story.
- Startup takes about 5.5 s on the emulator's Debug build, the same as Slice 61, and occasionally trips an ANR focus timeout. Not caused by this slice.

#### For CLAUDE.md

- Dino Reader highlights by `StorySentences`. Segments are split per sentence by `SegmentSentenceSplitter` before `SpeakSegmentsAsync`, so `ChunkChanged` maps 1:1 to sentences. (Slice 62: docs/plan/slices/62-reading-screen-modes.md)
- Dino Reader pages are composed by `FlowPager` (text) and `PageComposer` (picture) in `src/DinoReader.Core`, off the UI thread, with page lookup by spoken position. (Slice 62: docs/plan/slices/62-reading-screen-modes.md)
- Dino Reader story text ignores the system font scale (`FontAutoScalingEnabled` false): page geometry is measured. (Slice 62: docs/plan/slices/62-reading-screen-modes.md)

#### For README.md

Dino Reader's reading screen has a text mode and a picture mode. A highlight follows each sentence as it's read, pages turn on their own, a child can swipe ahead and jump back, and tapping a sentence reads from there.

#### Not yet verified

- The wander pill and Play halo during an active read.
- The gate pass leading to Manage from the reading screen.
- The bedtime theme, and RTL (no fa/ar story can be added until Slice 66).
- A long story's pagination performance on a real phone.
- The tablet layout and the "Page n of m" string, not built.
- Follow-up: share the read-start grace state machine with RTST in `ReadTheStupidText.Mobile.Shared`.
