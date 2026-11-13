# Slice 318 — Dino: the /stats page on phones and tablets (and the mock's other changes)
**Product:** Dino

*Built 2026-11-08, merged as PR #954 (44dabc57); not yet tried on a real phone or tablet, nor on the live page with real D1 data.* The page (`site/dino/stats.html`) and a small Worker change: `/api/stats` leaves the `own` bucket out of the series and the book totals unless `book=own` is asked for (`worker/stats.ts`, `own` bound as a parameter; no D1 schema change). The page is a website, so the Android/iOS twin rule does not apply (it has to work in phone and tablet browsers, both platforms alike).

## Why

The user refreshed the Claude Design mock on 2026-11-08 ("update the plan and apply it"): `docs/design/dino-reader/Dino Reader Stats Dashboard.dc.html`
(Batch 51, README section "Batch 51 mock"). It adds four frames (201 phone portrait, 202 phone landscape, 203 tablet
portrait, 204 tablet landscape) and changes frame 200, the notes and the script. Slice 287 built the page from the old
frame 200 (it is merged and live); this slice makes the page match the refreshed mock exactly. The mock trumps the shipped page.

**Depends on:** Slice 287 (built, `site/dino/stats.html` exists).
**Touches:** `site/dino/stats.html`, `worker/stats.ts`, `worker/stats.test.mjs`, `tools/stats-page-check/check.mjs`, `docs/plan/slices/318-dino-stats-phone-tablet.md`. `worker/stats.ts` sends the series and book totals the new views read; review (high) made it leave the `own` bucket out of both (mock note: own books are not counted), and the Worker serves the page as a static asset.

## Design (Dino Reader Stats Dashboard mock, sections 200-204 and the notes, added 2026-11-08)

Five frames (200-204) and three notes. The mock disagrees with the shipped page in the points listed under *Plan changed to match the mock*. Frames 201-204 show a phone or tablet shell (rounded black bezel, status bar with "14:05" and a battery, a fake browser bar with "dinoreader.sirous.uk" or ".../stats"): all of that is canvas furniture, not built. The Fredoka Google Fonts link in the mock is canvas-only (the page loads no web font and makes no third-party request; it keeps the font stack it has). Seeded demo numbers, "14:00 UTC", "Dino 1.112" and "10 Nov 2026" are placeholders filled from real data (`updatedAt`, `listensFrom`).

**Which layout, by width and height (the page picks one with CSS media queries).** The mock names the sizes 390 × 844, 844 × 390, 820 × 1180, 1180 × 820 and 1280, and says the sidebar layout is "above about 1100 wide" also the desktop's fallback when narrowed (line 293). So:
phone portrait = width up to 600; phone landscape = height up to 500 with landscape orientation (checked first, so 844 × 390 is not a tablet); tablet portrait = width 601 to 1099; tablet landscape (sidebar) = width 1100 to 1279; desktop (frame 200) = width 1280 and up.

**Shared by every layout (notes lines 423-426, script lines 431-592).**
- Readout line above the chart (frames 201-204, not frame 200). Idle text `Tap a bar to see its numbers.` in `#8a8399`. A tapped bar (the whole bar column is the tap target) writes `<name> · <total> — <Platform amount>, <Platform amount>` in `#2b2540`, only platforms above zero. Name: by day `Wed 5 Nov` (en-GB, weekday short, day numeric, month short, UTC), by week `Week of 3 Nov`, by month `November 2026`. The other bars drop to opacity 0.4; tapping the same bar again clears it; changing the period or bars-by clears it. Frame 200 keeps the hover tooltip only (no readout line).
- Bars are heights in percent of the plot (`height: <value/top*100>%`, minimum 0.4%, `flex-shrink:0`, line 390), so one plot works at 120, 170, 200 and 240 px. Segments keep the platform colours; the top segment keeps its 4 px top radius; the Sonos estimate keeps the hatched fill.
- Time is never rounded to "0.0" (note "Short listening times", line 425): `0` for zero seconds; under 60 s `under 1 min`; under 1 hour whole minutes, `4 min`; under 10 hours one decimal, `2.4 h`; above that whole hours with thousands separator, `12 h`. Used for the Hours tile value, the Sonos line `incl. <time> estimated for Sonos` (the old text appended " h" after the number; the formatted time carries its own unit), the Hours table cells, bar tooltips, the readout and the phone rows' second line.
- If no bar in the period reaches one hour and the metric is hours, the chart switches to minutes: values times 60, the y axis in minutes, chart title `Minutes listened per <group>` (the tile stays `Hours listened`), tooltips in the same time format.
- The book picker lists the store titles from `/api/stories` only. "Own books" is removed (note "Filters", line 423: "own books are not counted"): no `Own books` option, no `✎` cover initial, no `—` in a Downloads cell. A book id the counts name but the store list lacks still gets a row (`Book <first 8 chars>`), as today.
- Filter summary text (phone): `<book title or All books> · <all platforms | N platforms> · by <day|week|month>`, where N counts the five live platforms switched on; `all platforms` when all five are on.
- Short period labels on phones and the tablet period pills: `Today`, `7 days`, `30 days`, `Year` (desktop keeps `Today`, `Last 7 days`, `Last 30 days`, `This year`).

**Frame 201, phone portrait, 390 × 844 (lines 26-101).** Page: padding `6px 16px 24px`, gap 14, background `#F3EFE6`.
- Header: icon 36 × 36 radius 9; `Usage` 19px/600, line-height 1.2; under it `Updated 14:00 UTC` 12.5px `#8a8399` (the real update time, not the long "Counts of events, never people…" line). No lock glyph, no email (public page).
- Period: one pill group, padding 3, border 1px `#d9d2c4`, background `#FFF6E6`, radius 999, gap 2; four buttons flex 1, height 40, 14.5px/500; the on button is white with `0 1px 3px rgba(43,37,64,.16)`.
- Filters button: min-height 44, border 1px `#d9d2c4`, radius 12, background `#FFF6E6`, padding 0 12, gap 10, slider icon 18px stroke `#5B57E8` (2.2), summary 14px (one line, ellipsis), then `Filters` 14px/600 `#5B57E8`. It opens the sheet.
- Error: box `#FFF0EA`, border `#f2c4b4`, radius 14, padding `12px 14px`, `Couldn’t load the numbers.` 14px `#3a3450`, button `Try again` 44 tall, `#5B57E8`, white 15px/600.
- Tiles: 3 columns, gap 8; each radius 14, padding `10px 11px`, min-height 72; name 12.5px/600 `#5a5470` (`Downloads`, `Listens`, `Hours`); value 22px/600 line-height 1.2. The sub-lines are gone from the tiles (they moved to the footnote). The selected tile is white with a 2px `#5B57E8` border; the others `#FFF6E6` with a 1px border. Value colour `#c9c2b4` while loading or blank, as today.
- Chart card: radius 16, padding 12, gap 10. Title 16px/600, subtitle 12.5px `#8a8399`. The "counts from" note 12px `#5a5470` on `#ECE7FF`, radius 8, padding `6px 9px`. Readout 12.5px, line-height 1.4, min-height 35. Plot: y axis 32 wide, 170 tall, 3 labels (`step×4`, `step×2`, `0`, 11px); grid 170 tall with one line at 50%; bar gap 0 above 60 bars, 2 above 20, else 6; at most 5 date labels (every `ceil(n/5)`th), 10.5px; loading overlay `rgba(255,246,230,.82)` with `Loading…` 14px. Legend 10px dots, 12px text, gap `6px 12px`.
- Top books card (radius 16, `#FFF6E6`): title `Top books` 16px/600; sub `by <downloads|listens|hours>, like the chart · tap to filter` 12.5px. No column headings and no sorting: rows follow the chart's metric. Row: min-height 56, padding `8px 12px`, gap 10, 1px top border `#efe9dd`; rank 14 wide 12px; cover 30 × 40 radius 6; title 14.5px/500 with ellipsis; second line 12px `#8a8399` with the other two numbers in words, `<n> downloads · <n> listens` (hours as `<time>`); a zero is left out; right side the chart's number 15px/600 over a 52 × 4 bar (`#ebe4d6` track, `#5B57E8` fill, width relative to the largest). Tapping a row filters to that book (again clears); the picked row is `#ECE7FF`.
- Footnote 12px `#8a8399`, line-height 1.5: `A listen is 30 s or more. Sonos hours are estimates (hatched). Weeks start Monday, UTC.` The mock's "Signed in as elinsoftuk@gmail.com" is not built (public page, user 2026-11-08).
- Filters sheet (opens from the Filters button): scrim `rgba(43,37,64,.38)` (tap closes), bottom sheet `#FFF6E6`, radius `24px 24px 0 0`, padding `8px 16px 30px`, gap 16; grabber 40 × 5 `#d9d2c4`; header `Filters` 18px/600 and `Done` (44 tall, padding 0 20, `#5B57E8`, white 15px/600). Fields: `BARS BY` (12px/600 `#8a8399`, letter-spacing .5px) with a three-button pill group, buttons 40 tall, 14.5px, Week and Month dimmed to 0.4 when the period is Today; `BOOK` select 46 tall, full width, radius 12, 16px; `PLATFORMS` chips 44 tall, 14.5px, padding `0 14px 0 11px`, with the note (`not live yet` on Alexa). Changes apply at once; Done only closes.

**Frame 202, phone landscape, 844 × 390 (lines 105-168).** Page padding `10px 50px 20px` (clear of the notch), gap 10.
- Title row: icon 30 × 30 radius 8, `Usage` 17px/600, spacer, the four period buttons (40 tall, padding 0 14, 14px) and a `Filters` pill (44 tall, padding 0 14, border 1px `#d9d2c4`, `#FFF6E6`, 14px/600 `#5B57E8`, icon 16).
- Summary line 12.5px `#8a8399`, margin-top -4: `<filter summary> · updated 14:00 UTC`.
- Grid `180px | 1fr`, gap 10. Left: the three tiles stacked, gap 8, each flex 1, min-height 56, padding `6px 12px`, content centred (name and value as in 201). Right: chart card radius 16, padding `10px 12px`, gap 6; title 15px/600 and the period range (`5 Nov – 4 Dec`, 12px `#8a8399`) instead of the book name; readout one line (12px, nowrap, ellipsis, min-height 16); y axis 32 × 120, grid 120 tall, gridline at 50%, 8 date labels; legend 11.5px.
- Under the grid: the table card (radius 16, `overflow:hidden`) with all three columns: header grid `2.2fr 1fr 1fr 1fr`, padding `10px 14px`, `Top books` 12.5px/600 plus three sort buttons (min-height 28, right aligned, 12.5px/600; the sorted one `#5B57E8`); rows min-height 48, padding `6px 14px`, cover 26 × 34 radius 5, rank 16 wide, numbers 14px right aligned (no bars).
- Filters sheet = a right side panel, 360 wide, `#FFF6E6`, padding `14px 50px 16px 18px`, gap 12, scrolls; `Filters` 17px/600; `Done` 44 tall padding 0 18; pill group buttons 38 tall 14px; select 44 tall; chips 44 tall, 14px, padding `0 12px 0 10px`, without the `not live yet` note text. Scrim `rgba(43,37,64,.38)` on the left, tap closes.

**Frame 203, tablet portrait, 820 × 1180 (lines 172-233).** Page padding `20px 24px 32px`, gap 16.
- Header: icon 40 × 40 radius 10, title `Dino Book Reader · Usage`, sub `Counts of events, never people. Updated hourly · last at 14:00 UTC.` (as today). The mock's lock-glyph circle (44 px) with the email as its title is not built (public page).
- Filters card (radius 16, padding `14px 16px`, gap 14, `#FFF6E6`): row 1 grid `1.5fr 1fr`, gap 14: `PERIOD` (short labels, 40 tall, 14.5px) and `BARS BY`; row 2 `BOOK` select 44 tall, 16px; row 3 `PLATFORMS` chips 44 tall, 14.5px. Every control is 44 tall (the mock's "44 pt" line, 233).
- Error: box with `Couldn’t load the numbers.` 15px and `Try again` 44 tall.
- Tiles: 3 columns, gap 12; radius 16, padding `14px 16px`, gap 4; name 13px/600, value 30px/600 (line-height 1.15), sub 12.5px `#8a8399` (the full sub-lines stay on this layout).
- Chart card: radius 16, padding `14px 16px`, gap 10; title row as desktop; readout line; y axis 40 wide, 5 labels, plot 240 tall, gridlines at 25%; 8 date labels.
- Top books card: heading `Top books` 17px/600 with `tap a book to filter, a heading to sort` 13px; header row padding `4px 16px` (`Book` + three sort buttons, min-height 36); rows min-height 56, padding `8px 16px`, rank 18 wide, cover 30 × 40, numbers 14.5px with a 60 × 4 bar (`#c9c2b4` fill, `#5B57E8` on the sorted column).
- Footnote 12.5px: `A listen is a book played for 30 s or more. Sonos hours are the book’s length (hatched, estimate). Weeks start Monday, UTC. Data from <first rollup day>.`

**Frame 204, tablet landscape, 1180 × 820 (lines 237-294).** Two panes; the page fills the viewport height and each pane scrolls on its own.
- Left column 290 wide, `#FFF6E6`, 1px right border `#d9d2c4`, padding `18px 18px 20px`, gap 18. Top: icon 38 × 38 radius 10, `Usage` 18px/600, `Updated 14:00 UTC` 12.5px. Then `PERIOD` (short labels, 40 tall, 14px, padding 0 2), `BARS BY`, `BOOK` (44 tall select), `PLATFORMS` chips (44 tall, 14px, hover title). A spacer, then at the bottom the footnote 12px: `A listen is 30 s or more. Sonos hours are estimates (hatched). Weeks start Monday, UTC.` (The mock's "Signed in as elinsoftuk@gmail.com" line is not built.)
- Right pane: padding `18px 22px 28px`, gap 14. Error box; tiles 3 columns gap 12, radius 16, padding `12px 16px`, gap 3, with name, value and sub; chart card radius 16, padding `12px 16px`, gap 8, with the legend on the title row's right (11px dots, 12px text), then the "counts from" note (12.5px, radius 10), the readout (13px, min-height 18), the plot with a 40-wide y axis (5 labels) and a 200 tall plot (gridlines at 25%), date labels as on desktop; then the table card with the header row `Top books` (16px `#2b2540`) plus sort buttons (min-height 40), rows min-height 52, padding `6px 16px`, cover 28 × 38, bar 64 × 4.

**Frame 200, desktop, 1280 (line 305 on).** Changes versus the old copy: bars as percent heights; "Own books" removed from the book list and table; the time format and the minutes chart (all above). Nothing else in the frame changed, so the layout, colours and copy from Slice 287 stay.

New strings (the Dino site page is English only and has no locale files, so nothing for two locales): `Tap a bar to see its numbers.`, `Filters`, `Done`, `Updated <HH:MM> UTC`, `by <downloads|listens|hours>, like the chart · tap to filter`, `<n> downloads`, `<n> listens`, `under 1 min`, `<n> min`, `Minutes listened per <group>`, `Week of <date>`, `all platforms`, `<N> platforms`, `7 days`, `30 days`, `Year`, and the two short footnotes above.

**Plan changed to match the mock (2026-11-08):**
- Slice 287 built the phone layout from the old States note ("tiles stack and the table keeps Book and the sorted column": `@media (max-width: 640px)` with `.hide-phone`). The mock replaces it with frames 201-204 (the mock supersedes it); that media rule goes.
- 287 built "Own books" (`bookList` adds `own`, the `✎` initial and the `—` downloads cell). The mock now says own books are not counted; all three go.
- 287 shows hours with `hrs()` (one decimal, "0.0" for short times). The mock's `under 1 min` / `N min` / `N.N h` / `N h` rules replace it everywhere, and the chart switches to minutes when no bar reaches an hour.
- 287 drew bars in pixels (`H = 240`). The mock uses percent so the plot height can be 120, 170, 200 or 240.
- 287 had hover tooltips only. The mock adds the tap-a-bar readout and bar dimming on frames 201-204.
- Not built (canvas furniture): the device shells, status bars, fake browser bars, the lock glyph and "Signed in as elinsoftuk@gmail.com" (the user said on 2026-11-08 the dashboard is public, "I want everyone to see that dashboard", Slice 287), the mock's Fredoka link, and the `sample`/`quiet` tweak (a design-tool control; its numbers are demo data).

## Work

- [x] 1. Time format: replace `hrs()` with the mock's `fdur(seconds)` and use it in the Hours tile, its Sonos line, table cells and tooltips; add the minutes chart switch (title `Minutes listened per <group>`).
- [x] 2. Remove "Own books" (`bookList`, the `✎` initial, the `—` cell).
- [x] 3. Bars in percent of the plot, with the plot height from CSS per layout (240, 200, 170, 120); y axis 3 or 5 labels per layout; date label counts 14 / 14 / 8 / 5 / 8 (desktop, sidebar, tablet portrait, phone portrait, phone landscape).
- [x] 4. Tap-a-bar: `picked` state, readout text and colours, bar dimming, cleared on period or bars-by change; hidden on desktop.
- [x] 5. Tablet portrait (601-1099 wide) and tablet landscape sidebar (1100-1279 wide) layouts as frames 203 and 204.
- [x] 6. Phone portrait (up to 600 wide): header, period pills, Filters summary button, 3-up tiles, `Top books` list by the chart's metric, footnote, and the bottom Filters sheet (open, scrim close, Done).
- [x] 7. Phone landscape (height up to 500, landscape): title row with period and Filters, summary line, tiles-beside-chart grid, full table, side Filters panel.
- [x] 8. Remove the old `max-width: 640px` rule; keep desktop (1280 and up) as it is.
- [x] 9. Check at the five sizes against the values in this file and fix every difference (the mock's `support.js` is not in the repo, so the frames cannot be rendered next to the page). Done by `tools/stats-page-check/check.mjs` against the values in this file; screenshots were not compared side by side with the mock frames.

## Verification

Scripted only. Serve `site/dino/` as static files and run a headless browser (Playwright, or headless Chrome from the repo's tooling) with `/api/stats` and `/api/stories` answered by fixtures (a busy 30-day set, a quiet set of four listens of 22 s to 1 min 40 s, and an error response). Take a screenshot at 390 × 844, 844 × 390, 820 × 1180, 1180 × 820 and 1280 × 800, and check in the DOM:
- the right layout per size (sidebar only at 1180 wide; Filters button only at the two phone sizes; table columns: one number at 390, three at 844, 820, 1180 and 1280);
- tapping a bar writes the readout and dims the other bars; tapping again clears it; no readout line at 1280;
- the Filters sheet opens, a platform chip inside it changes the summary, Done closes it;
- the quiet fixture shows `under 1 min` / `N min` and the chart titled `Minutes listened per day`; the busy fixture shows `N.N h`;
- no `Own books` option anywhere; no request to a host other than the page's own (no web font).
`node --test worker/stats*.test.mjs` still passes (routing is unchanged; the only test that touches the page is the routing test). Screenshots go in the PR.

#### Built and checked

Changed while building: the Worker also drops the `own` bucket (`worker/stats.ts`, bound as a parameter, only `book=own` returns it), so tiles, chart and rows agree; new `tools/stats-page-check/check.mjs` (Playwright, fixtures, 118 checks). Run: `npm install --prefix tmp/pw playwright`, `npx --prefix tmp/pw playwright install chromium`, `PLAYWRIGHT_DIR=tmp/pw node tools/stats-page-check/check.mjs`. Result: 118 passed, 0 failed, headless at 390x844, 844x390, 820x1180, 1180x820 and 1280. `node --test worker/stats*.test.mjs`: 31/31. Typecheck clean. Browser run `reviews/s318-browser/results.md`: one check bug (the gridline regex) was fixed and the rerun was green.

#### From review

Review (high), `reviews/s318-high.md`, findings 1-10 applied (findings read 10/10):
- #1 `worker/stats.ts` leaves the `own` bucket out of the series and the book totals unless `book=own` is asked for (test in `worker/stats.test.mjs`), so tiles, chart and rows agree.
- #2 check.mjs finds Playwright from `PLAYWRIGHT_DIR` (documented install and run commands in its header).
- #3 phone-landscape preset labels `nowrap`. #4 tablet portrait and side layouts use the mock's top-anchored gridlines. #5 the filter summary says "all platforms" when the five non-Alexa platforms are on.
- #6 `/api/stories` counts as loading (overlay after 500 ms, 20 s timeout, book rows held until it settles; the footer says when titles failed to load).
- #7 check.mjs asserts: rows per store book (none for own), own cover set and a foreign-host cover not, preset labels on one line, gridlines, summary wording, overlay absent before and present after 500 ms on desktop and both phone sizes and for slow titles. Not added: a tiles-vs-rows totals assertion (the fixture's series and book totals are independent numbers).
- #8 `paintChart` split into `paintBars`, `paintLegend`, `paintChartTitles`. #9 Filters sheet is `role="dialog"` with `aria-modal` on phones, `aria-expanded` on the button, Tab trapped, focus kept when chips or bars rebuild; bars are keyboard buttons. #10 covers only from this origin's `/api/stories/<id>/cover`; the book list is built once per paint.

Review (medium), `reviews/s318-medium.md`, findings 1-5, 7, 8, 9 and 11 applied (findings read 11/11; check.mjs has since been run, see above):
- #1 the gridline check runs only on tablet and phone sizes. #2 `own` is bound as a parameter (the API keeps dropping it, decided in review high #1). #11 with `book=own` the book totals read the own bucket too, so rows match the chart.
- #3 `fdur` rounds to whole seconds and switches to hours when the minutes round to 60 (3599 s is `1.0 h`). #9 check.mjs has Hours tile fixtures at 59, 60, 3569, 3570, 3599, 3600, 35999 and 36000 s.
- #4 bars are `role=button` with a roving tab stop and arrow keys only on the touch layouts; on desktop they are plain hover tooltips. #5 the picked bar is stored as its bucket key and cleared when that bucket is not in the new data.
- #7 public copy matches the mock's literal strings (the mock wins): "Listens and hours count from Dino <version>, <date> (Slice 288)." and the dimmed Alexa chip note "not live yet" with title "Counts start when Slice 282 ships". #8 slow book titles put "Loading books…" in the book rows only; the chart overlay is for the numbers.
- Skipped #6 (`reviews/s318-medium.md`): "Every bar tap calls the full `paint()`, which rebuilds the chart, both book lists (via innerHTML) and every cover." - low, left as is by the caller. Skipped #10: "`bookList(id)` caches one full list per id ... each paint builds 2-3 copies" - minor cost, left as is.

#### Not yet verified

- The page on a real phone and a real tablet in both orientations (touch, notch spacing, Filters sheet focus, the side panel in landscape).
- The live page against real D1 data after deploy.
- Screenshots were not compared side by side with the mock frames 200-204.

#### For CLAUDE.md (lifted 2026-11-08)

- `site/dino/stats.html`: the `/stats` layouts come from frames 200-204 of the mock (`docs/design/dino-reader/Dino Reader Stats Dashboard.dc.html`); run `tools/stats-page-check/check.mjs` after a change. `worker/stats.ts` leaves the `own` bucket out of the series and book totals unless `book=own`. Goes in `worker/CLAUDE.md`. (Slice 318: docs/plan/slices/318-dino-stats-phone-tablet.md)
