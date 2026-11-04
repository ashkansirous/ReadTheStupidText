# Slice 257 — Dino: a quote after "said Mouse." is the Mouse's, not the Lion's (Lion and the Mouse)

*Planned 2026-11-03.* Dino Reader only (Core), so Android, iOS and the Windows head alike. Found while casting the store
books for Slice 246: in Lion and the Mouse the Mouse's line "I didn't mean to wake you up. If you're kind to me today,
I'll be kind to you one day, too!" (the part after "said Mouse.", in the paragraph where she says "Mister Lion") is given
to the Lion by the app's own speaker attribution, in both English (dump index 14) and Farsi (13). So the on-device voice
reads it in the Lion's voice today, and the Gemini clip rendered on 2026-11-03 uses the Lion's voice (Algenib).

*Built 2026-11-04 (night 2026-11-03-2349 wave 1), merged as PR #775 (95330968); no device run (pure Core logic, covered by the unit tests), see Not yet verified.*

**Touches:** `src/DinoReader.Core/SpeakerAttributor.cs`, `src/DinoReader.Core/AttributionRun.cs`,
`src/DinoReader.Core/SpeakerTagParser.cs`, `src/DinoReader.Core/SentenceBoundaries.cs`, `src/DinoReader.Core/*Rules.cs`,
`tests/DinoReader.Tests/SpeakerAttributor*Tests.cs`, `docs/plan/slices/257-lion-and-mouse-speaker.md`.

**Claim note (coordinator, night 2026-11-03-2349 wave 1):** likely cause, read from code, not yet run: after `said Mouse.` the
narration ends a sentence, so `TagBefore` is null (`SentenceBoundaries.UnfinishedLast`), and `FromTag` then takes `TagAfter` from
the next narration "The Lion laughed ... said Lion." (`AttributionRun.cs:49-59`, `laughed` is a speech verb in `EnglishRules.cs`).
The builder proves the cause with the failing test first. Fix direction: a quote that directly follows a finished tag sentence
in the same paragraph ("..." said Mouse. "...") continues that speaker before any tag after it is tried; it must hold in Farsi
too (fa index 13). Every other book's attribution is compared before and after with the BookBuilder dump; each change is
listed here. Item 2's re-render needs the Gemini key and the s246-audio folder; if either is missing, item 2 goes to *Not yet
verified*.

## Work

- [x] 1. Find why the continuation quote after a mid-paragraph tag ("...," said Mouse. "I didn't mean ...") is attributed to the
  wrong speaker; fix it in Core with a test built from this paragraph, and run the whole Dino suite (other books' attributions
  must not change, or each change is listed and checked).
- [x] 2. Re-dump Lion and the Mouse (fa and en) and re-render the changed line(s) in Farsi with `generate.py --batch` into
  `D:/ReadTheStupidText-worktrees/s246-audio/books-lion-and-mouse/` (the cache re-renders only pieces whose voice changed).

## Built (2026-11-03)

- **Cause confirmed** by a failing test first (`SpeakerAttributorTests.Narration_after_a_page_break_does_not_tag_the_earlier_pages_quote`, Mouse/Mouse/Lion expected, Lion got for the 2nd): after `said Mouse.` `TagBefore` is null, so `FromTag` took `TagAfter` from "The Lion laughed so hard that his tummy wobbled." (`laughed` is a speech verb). The attribution runs on the English source text, so Farsi (fa 13) follows it.
- **Fix** (`AttributionRun.TagAfter` / `SameParagraphNarration`; the first rule, `AfterFinishedTagSentence` / `IsOnlyTag`, was dropped after the high review because it broke ordinary alternation): a tag after a quote is taken only from narration in the same paragraph as the quote. Narration after a blank line, which is also the page break between pages (`Story.Text` joins pages with a blank line), never tags an earlier paragraph's quote, and a tag is read only up to the first blank line inside the narration. In Lion and the Mouse the Mouse quote (page 3) and "The Lion laughed ... said Lion." (page 4) are on different pages, so the quote gets no tag after and falls to alternation (Mouse). Tests: the Lion paragraph in English and Farsi, the review's alternation shapes (`"Hi," said Peter. "Hello!" Lucy replied. She smiled. "Fine."` and the blank-line shape), and a bare next tag in the same paragraph still tagging.
- **Attribution dump** (`dump-sentences`, every store book in `en` and `fa`, before vs after; `good-enough` cannot be dumped, "1 pages but 10 pictures", before and after alike): only `lion-and-mouse` changed. One attribution: the quote "I didn't mean to wake you up. If you're kind to me today, I'll be kind to you one day, too!" (en index 14, fa 13) Lion -> Mouse. Knock-on: the app's inserted animal sounds are counted over each character's own lines, so the Mouse's one more line moves where "Squeak!" and "Roar!" fall (en: a Squeak now after index 14, several Roar/Squeak moved one place later or earlier; fa: one Roar moved, 3 positions). No other book changed.
- Dump redone with the new rule (BookBuilder `dump-sentences`, origin/main code vs this branch, 15 books x en, fa): the same single change as above (en 14 and fa 13 Lion -> Mouse, same animal-sound knock-on), no other book differs. So item 2's Farsi re-render (1 changed line, Leda) is unaffected.
- After the medium review: `SameParagraphNarration` scans `narration.Text` only (one coordinate system, no source-offset slice that could run out of range); the Farsi test pins the Mouse as speaker of quote 2; the blank-line test now fails on the old rule; new tests for a single newline, a tag before a break inside one narration, CRLF and whitespace-only blank lines, and a quote ending a page with its tag on the next page (no tag after). Same results as the dump above (the scan is the same, only its coordinates changed), so the dump was not redone.
- Whole `tests/DinoReader.Tests` suite: 2330 passed, 0 failed.

## Verification

- The new Core test; the full `tests/DinoReader.Tests` suite; the re-render ending with 0 left.

Item 2 done (2026-11-04): `generate.py --batch` into `s246-audio/books-lion-and-mouse/` rendered the 1 changed Farsi line (Mouse voice Leda), 80 cached, 0 left.

**Deliberate trade-off:** a tag in the next paragraph (or on the next page) never attributes an earlier quote; that quote falls to alternation. Pinned by a test (`A_quote_ending_a_page...`).

**Changed while building:** the first rule (`AfterFinishedTagSentence`: a quote right after a finished tag sentence continues that speaker) was dropped after the high review and replaced by `SameParagraphNarration` (see Built).

**From review** (files in `D:/ReadTheStupidText-worktrees/reviews/`):

- `s257-high.md` (6 findings): 1 (new rule changed ordinary alternation), 2 (no paragraph-break check), 4 (only the positive case tested) and 6 (duplicated lookahead) applied through the redesign above. Skipped: 3 (narrow special case; the review's own text says a tag-before reading is correct here, and the redesign fixes the root in `TagAfter`), 5 (`IsOnlyTag` on an unterminated narration; the rule using it was dropped).
- `s257-medium.md` (8 findings): 3 (mixed coordinates), 6 (Farsi test did not pin the speaker), 7 (a test that guarded nothing) and 8 (boundary tests) applied. Skipped: 1 (a tag on the next page no longer tags the quote; the accepted trade-off above), 2 (`IsLeadInToNextQuote` still measures the whole narration segment; low), 4 (double scan, constant factor only), 5 (diff noise in `FromTag`).
- `s257-check1-medium.md` (5 low/trivial findings): none applied. 1 is the trade-off above; 2 unconfirmed, no failing case found; 3 and 5 are noise/comment only; 4 (format of the Not-yet-verified line) is done in the block below.

#### For CLAUDE.md (lifted 2026-11-04)

- Dino speaker attribution (`src/DinoReader.Core/AttributionRun.cs`): a tag after a quote is read only from narration in the same paragraph. Pages join with a blank line, so a page break also ends the search. Do not add a rule that makes a quote after a finished tag sentence continue that speaker: it changes ordinary alternation. (Slice 257: docs/plan/slices/257-lion-and-mouse-speaker.md)

#### Not yet verified

- Listen to the new Farsi clip in `D:/ReadTheStupidText-worktrees/s246-audio/books-lion-and-mouse/`.
- On a device, Lion and the Mouse reads the Mouse's line ("I didn't mean to wake you up. ...") in the Mouse's voice (no device run: pure Core logic, covered by the unit tests the app uses).
