### Slice 231 — Dino: the Record page shows the page's words (bug)

*Planned 2026-11-01.* From the user's user test on 2026-11-01 (finding 6). Bug fix, no design step; can go first.

**Touches:** `src/DinoReader.App/RecordViews.cs`, `src/DinoReader.App/RecordPage.cs`, `docs/plan/slices/231-dino-record-page-shows-words.md`

#### Why

A tester recording their voice could not see the text of the page. Likely cause, read from code (not yet shown on a device): `RecordPage.cs:144` calls `RecordViews.PageView(picture, page.Text, story.Language)`, and `RecordViews.cs:24-28` shows **only the picture** when the page has a picture file; the words appear only on pages without a picture. So every picture book hides them. Scanned books may also show partial text, because their words are in `Lines`, not `Text` (`src/DinoReader.App/CLAUDE.md`).

#### Decisions locked with the user (2026-11-01, RefineScope)

- Show **both**: a smaller picture with the words under it, readable size, in the book's language and direction. The picture helps the reader keep their place.
- Use the page's full text; for scanned pages, the scanned lines.

#### Work

- [ ] Confirm the cause on the emulator first (a store picture book on the Record page shows no words).
- [ ] `RecordViews.PageView`: picture plus text card on every page that has text; text only when there is no picture; picture only when the page has no words.
- [ ] Scanned pages: build the text from `Lines`.
- [ ] Android and iOS built and checked together.

#### Verification

- Scripted emulator flow: open Record on a store picture book, the page's words are visible under the picture; on a scanned book, all lines show. iOS: same on simulator or TestFlight.
