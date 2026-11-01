### Slice 231 — Dino: the Record page shows the page's words (bug)

*Built 2026-11-01 (wave 7, night 2026-11-01-0859), merged as PR #666 (e93daaa); emulator flow passed, rest not yet verified.* From the user's user test on 2026-11-01 (finding 6). Bug fix, no design step; can go first.

**Touches:** `src/DinoReader.App/RecordViews.cs`, `src/DinoReader.App/RecordPage.cs`, `.maestro/dino/record-page-shows-words.yaml`, `docs/plan/slices/231-dino-record-page-shows-words.md`

#### Why

A tester recording their voice could not see the text of the page. Likely cause, read from code (not yet shown on a device): `RecordPage.cs:144` calls `RecordViews.PageView(picture, page.Text, story.Language)`, and `RecordViews.cs:24-28` shows **only the picture** when the page has a picture file; the words appear only on pages without a picture. So every picture book hides them. Scanned books may also show partial text, because their words are in `Lines`, not `Text` (`src/DinoReader.App/CLAUDE.md`).

#### Decisions locked with the user (2026-11-01, RefineScope)

- Show **both**: a smaller picture with the words under it, readable size, in the book's language and direction. The picture helps the reader keep their place.
- Use the page's full text; for scanned pages, the scanned lines.

**User answers (2026-11-01):** iOS gets a compile check in the night run; the VoiceOver / TestFlight / iPhone / iPad hand checks go to this slice's *Not yet verified* for the user to do later.

#### Design (Dino Reader Batch 43 and 44 mocks, frames 106, 116, 129, added 2026-11-01)

The plan says "no design step"; the layout is drawn in three frames: 106 in `docs/design/dino-reader/Dino Reader Batch 43 Grown-ups Corner.dc.html` (file line 267, portrait), 116 and 129 in `docs/design/dino-reader/Dino Reader Batch 44 Landscape.dc.html` (file lines 201 and 350). The mock agrees with the plan.

- **Portrait (106).** The picture shrinks to a fixed band 210 dp tall, radius 14, cropped from the top (`object-position` top). Under it a white card (radius 16, padding 14 16) with the page's full words at 20 px, in the book's language and direction; scanned pages join their `Lines`. No words: the picture fills both. No picture: words only. Header sub-line "The Three Little Pigs · page 5 of 16".
- **Landscape phone (116).** Three columns: the picture fitted whole (not cropped), the words with the recordings line under them, and a rail with Done on top and the mic in the middle.
- **Tablet (129).** Two columns: the page, then header, words, recordings line and the big mic button stacked.
- No new strings.

Settled by the mock: the band is cropped, not fitted, in portrait; landscape fits the whole picture. Example text in the frames is a placeholder; use the book's.

#### Work

- [x] Confirm the cause on the emulator first (a store picture book on the Record page shows no words). Cause confirmed from code only, not shown on a device (see Not yet verified).
- [x] `RecordViews.PageView`: picture plus text card on every page that has text; text only when there is no picture; picture only when the page has no words.
- [x] Scanned pages: build the text from `Lines`.
- [x] Android and iOS built and checked together.

#### Verification

- Scripted emulator flow: open Record on a store picture book, the page's words are visible under the picture; on a scanned book, all lines show. iOS: same on simulator or TestFlight.

#### Changed while building

- Cause confirmed from code: `RecordViews.PageView` showed the words only when the page had no picture.
- `PageView` now takes the `StoryPage`: a picture band (min(210 dp, 40% of height), cropped from the top via `PictureTextBook.CoverFromTop`) over the words card. No picture: words only. No words: picture only.
- The words are `page.Text`, the same text the reader reads. For scanned pages it is built from the included lines (`PageReading.Text`), so the "scanned lines" decision holds and excluded regions never show.
- Touches widened: new flow `.maestro/dino/record-page-shows-words.yaml`.
- Landscape phone and tablet (frames 116 and 129) are not built here: left to Slice 232 (landscape every screen).

#### From review

High (`reviews/s231-high.md`): applied 1-4. Skipped:
- 5 (PageWords has no unit test): the App project is not unit-testable and the pure logic's home, Core, is outside Touches.
- 6 (flow taps plain text "Record", asserts only 20+ characters): skipped.
- 7 (Debug.WriteLine on every Render): skipped.

Medium (`reviews/s231-medium.md`): all skipped.
- 1 (scanned pages not handled): wrong, `Text` is the included lines.
- 2 (portrait-only layout): Slice 232.
- 3 (band height only set from SizeChanged): the band recalculates on every size change.
- 5 (books that print words in pictures now show both): contradicts the user's "show both".
- 4, 6, 7 (log noise, duplicated cover helper, flow strength): style.

#### Verification run

- Emulator-5554, flow `record-page-shows-words`: PASS (113.8 s). uiautomator dump on Good Enough page 1: picture band above `RecordPageWords` with the page text.

#### For CLAUDE.md (lifted 2026-11-01)

None.

#### Not yet verified

- origin/main showing the picture only (the cause was read from code, not shown on a device).
- A scanned book showing its lines on the Record page.
- An RTL/Farsi book on the Record page.
- iPhone, iPad, VoiceOver and TestFlight hand checks (the user's answer: they do these later).
