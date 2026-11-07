# Slice 287 — Dino: a private usage dashboard at dinoreader.sirous.uk/stats

*Planned 2026-11-07.* Server only (Cloudflare Worker, D1, the Dino site). The apps do not change here.

**Depends on:** nothing to start. Slice 288 (the app's listens and listening time) and Slice 289 (Sonos plays)
fill the listen and hour numbers; until they ship those columns show zero with a note saying from which version
they count.
**Touches:** `worker/stats.ts` (new: page data API), `worker/stats-rollup.ts` (new: cron rollup), `worker/stats-access.ts`
(new: Cloudflare Access token check), `worker/stats*.test.mjs` (new), `worker/index.ts` (routes + `scheduled`),
`worker/schema/stats.sql` (new D1 table), `wrangler.jsonc`, `wrangler.workers-dev.jsonc` (D1 binding, cron trigger,
`run_worker_first` for `/stats`, `/api/stats` and their `/dino` twins), `site/dino/stats.html` (new), `worker/CLAUDE.md`,
`docs/analytics/dino-dashboard-queries.md` (new), this file.

## Why

The user, 2026-11-07: "I want to have a dashboard showing the usage on the dinoreader.sirous.uk website. For that I want
to show: 1- number of books being downloaded per period (day, week, month) and top books 2- how many times a book was
listened and how many times in general all the books were listened to 3- how many hour for all books and for a specific
book." Then: "platforms should be a part of the filter. like android, ios, windows, alexa, sonos".

Code check (origin/main, 2026-11-07): there is no dashboard, stats endpoint or login anywhere. Every `dino_usage` row
carries the platform in `blob7` (`worker/events.ts:392-398`), the book in `blob2` (a store id for shelf books, `own` for
a parent's own book) and the sampling weight in `double1`. Downloads are the app's `store_download` events; the Worker's
own `/api/stories/<id>/download` writes nothing. No install or device id exists, so the dashboard counts events, never
people. Nothing in the repo keeps data beyond Analytics Engine's own retention (the repo assumes 90 days; not checked
against Cloudflare's docs).

## Decisions (user, 2026-11-07, RefineScope)

1. **A listen** is a book played for at least 30 seconds; playing it again later counts again (event from Slice 288,
   Sonos from Slice 289).
2. **Hours** are real played seconds sent by the app, counted from the release that adds them (Slice 288). Speakers:
   played time if Sonos/Alexa report it, else the book's length, shown as an estimate.
3. **Platforms** in the filter: Android, iOS, Windows, Sonos, Alexa. Alexa appears once Slice 282 ships.
4. **Where:** `dinoreader.sirous.uk/stats`, behind Cloudflare Access letting in only the user's email. A daily rollup into
   permanent storage (D1) so day, week and month views keep their history after Analytics Engine drops the raw rows.
   Filters: date range, book, platform.

## Design (planning defaults)

- **Rollup.** A Worker cron (hourly) queries the Analytics Engine SQL API for today and yesterday (UTC) and upserts one row
  per `day × book × platform × metric` into D1 table `daily_stats(day, book, platform, metric, value)`, primary key on the
  first four. Metrics: `downloads` (`store_download`), `listens` (`book_listened`), `seconds` (sum of played seconds),
  `seconds_estimated` (speaker plays with no reported time). Values are weighted with `SUM(double1)` (sampling). Upserting
  the same two days each hour makes it idempotent and today's view at most an hour old. The first run backfills every day
  Analytics Engine still holds. Production dataset `dino_usage` only, never `dino_usage_beta`.
- **Token.** The Worker reads Analytics Engine with the existing read-only `CF_ANALYTICS_READ_TOKEN` (in `D:\secrets`) as a
  Worker secret (`manage-secrets`), plus the account id as a var.
- **Access.** A Cloudflare Access application on `dinoreader.sirous.uk/stats*` and `/api/stats*`, one allow rule: the
  user's email (one-time PIN). The Worker also checks the `Cf-Access-Jwt-Assertion` token (signature against the team's
  certs, audience tag) on both paths and answers 403 without a valid one, because the `workers.dev` `/dino/stats` twin is
  not behind Access. Set up through the API if a token with Access edit rights exists; otherwise one user step.
- **Page.** Plain HTML with inline script and inline SVG charts, no third-party requests (the Dino site rule). Top: date
  range (presets Today, Last 7 days, Last 30 days, This year, custom) and grouping (day, week, month); platform checkboxes;
  book picker (store titles from `/api/stories`, plus "Own books" as one line). Three tiles (downloads, listens, hours),
  a bar chart per metric over the chosen grouping, and a "Top books" table (downloads, listens, hours, sortable).
  Estimated speaker hours are drawn hatched and labelled "estimate". A wait over 500 ms shows a loading state.
- **API.** `GET /api/stats?from=&to=&group=day|week|month&platform=…&book=…` returns series and the top-books table as
  data, no copy. Weeks start Monday, UTC.

#### Design (Dino Reader Stats Dashboard mock, section Batch 51, frame 200, added 2026-11-07)

One frame (200, line 25 of `docs/design/dino-reader/Dino Reader Stats Dashboard.dc.html`, 1280 wide, inside a browser-window illustration) plus three note paragraphs (lines 142-144) and the behaviour script (lines 149-281). The numbers are made up, drawn as if read on 15 Dec 2026. The mock agrees with the plan on layout, Access, the hourly rollup, no third-party requests, hatched estimates, the 30-second listen and Monday weeks. It differs on the points under **Open point** below. The browser chrome (traffic lights, the 480px address pill, the page heading block) and the Tweaks panel for live, loading and error are illustration, not UI. Page ink `#2b2540`, muted `#5a5470` and `#8a8399`, brand `#5B57E8`, page `#F3EFE6`, cards `#FFF6E6` with a 1px `#d9d2c4` border and radius 16. Content padding 20px 32px 40px, gap 20.

- **Header (Work 1).** Dino icon 40px, radius 10; title "Dino Book Reader · Usage" (19px, 600); second line "Counts of events, never people. Updated hourly · last at 14:00 UTC." (13px, `#8a8399`). Right: a lock glyph (16px stroke icon) and the signed-in email. The "last at" time is the last rollup run, so `/api/stats` must return it (a one-row `meta` table or the max `updated_at`); the page does not work it out.
- **Filters card (Work 3).** Labels 12px, 600, `#8a8399`, letter-spacing .5, upper case: PERIOD, BARS BY, BOOK, PLATFORMS (PLATFORMS on its own full-width row). PERIOD and BARS BY are segmented pills: track padding 3, radius 999, `#F3EFE6`, 1px `#d9d2c4`; buttons 32px high, padding 0 13px, 14px, 500; selected `#fff` / `#2b2540` with shadow `0 1px 3px rgba(43,37,64,.16)`, others transparent / `#5a5470`. PERIOD: "Today", "Last 7 days", "Last 30 days", "This year". BARS BY: "Day", "Week", "Month". BOOK: a select, 38px high, min 220px, radius 10, white; first option "All books", then the store titles from `/api/stories`, last "Own books".
- **Period and grouping rules (Work 3).** Defaults: Last 30 days, bars by Day, All books, all platforms but Alexa, tile Book downloads, table sorted by Downloads. Ranges end today (UTC): 7 days = today minus 6; 30 days = today minus 29; This year = 1 Jan. "Today" forces Day and dims Week and Month to opacity .4 (not clickable). Picking "This year" while on Day switches to Week; other presets keep the grouping. Buckets cover every day of the range, empty ones included. Week key = that Monday (UTC), month key = the 1st; the label is the key's date. Bar labels "d MMM" (en-GB), month bars "MMM"; every ceil(n/14)th label is drawn.
- **Platform chips (Work 1, 3).** Fixed order and colours: Android `#4FB860`, iPhone `#5B57E8`, iPad `#A9A6F7`, Windows `#F2994A`, Sonos `#2b2540`, Alexa `#4BB3D9`. Chip 34px high, padding 0 14px 0 10px, radius 999, 14px, 500, a 12px square dot (radius 4) in its colour. On: white, 2px border in its colour. Off: transparent, 2px `#d9d2c4`, text `#8a8399`. At least one chip stays on (a click on the last one is ignored). Alexa: off, opacity .55, not clickable, note "not live yet" (12px, `#8a8399`) and tooltip "Counts start when Slice 282 ships"; it becomes a normal chip when Slice 282 ships.
- **Tiles as the chart switch (Work 1, 2).** Three tiles in a grid, `repeat(auto-fit, minmax(240px, 1fr))`, gap 14, radius 16, padding 16px 18px. The chosen tile is `#fff` with a 2px `#5B57E8` border and a "IN THE CHART" tag (11.5px, 600, `#5B57E8`); the others `#FFF6E6`, 1px `#d9d2c4`. Label 13px 600; number 34px 600 (`#c9c2b4` while loading); sub 12.5px `#8a8399`. "Book downloads": sub "Store books, from the apps. Speakers stream and don’t download." "Listens": sub "Played for 30 s or more", or "Counted from 10 Nov 2026 · played for 30 s or more" when the range starts before listens were counted. "Hours listened": the value is real plus estimated hours; sub "Real played time from the app", or "incl. 3.4 h estimated for Sonos" when estimated hours exist. Numbers use thousands separators (en-GB); hours get one decimal under 10, whole above.
- **One stacked chart (Work 1, 2).** Card title "{Book downloads|Listens|Hours listened} per {day|week|month}" (17px, 600); sub "{book} · 16 Nov – 15 Dec 2026" (13px, `#8a8399`; the exact days, start without year, end with year); legend at the right (12px squares, radius 3, 12.5px). Plot 240px high, y axis 44px wide with five ticks, 0 to 4 steps; the step is the first of 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 5000, 10000 whose four steps cover the tallest bar; grid line every 60px `#ebe4d6`, baseline `#cfc7b6`. Bars fill the width with a gap of 8px (up to 20 bars), 3px (up to 60), 1px (more). Segments stack bottom up in platform order, at least 1px high, the top one rounded `4px 4px 0 0`. Hover: brightness 1.06 and a tooltip "{label}: {total}" then one "{platform} {n}" line each. Estimated hours: segment `repeating-linear-gradient(135deg, <platform colour> 0 3px, #b8b2c8 3px 6px)`; the Sonos legend entry reads "Sonos (estimate)" on the hours chart. When the range starts before the listens date and the tile is Listens or Hours, a note sits above the plot (`#ECE7FF`, radius 10, padding 6px 10px, 12.5px): "Listens and hours count from Dino 1.112, 10 Nov 2026 (Slice 288)." (version and date are placeholders for Slice 288's real release). Earlier bars are empty.
- **iPhone and iPad (Work 1).** The rollup keys iOS rows by form factor: `blob12` `tablet` is `ipad`; anything else (`phone`, `other`, empty for builds before Slice 166) is `iphone`, and the hover says so for the unknown ones. `blob12` is written at `worker/events.ts:397`; the values are `phone`, `tablet`, `desktop`, `other` (`events.ts:108`). Android and Windows stay one chip each.
- **Top books table (Work 3).** Card title "Top books" (17px, 600) and "in this period and these platforms · click a book to filter, a heading to sort" (13px, `#8a8399`). Grid `minmax(0,2.2fr) repeat(3, minmax(0,1fr))`, header 12.5px 600: "Book", "Downloads", "Listens", "Hours" (numeric headings are right-aligned buttons; the sorted one reads "Downloads ↓" in `#5B57E8`; sort is descending only). Row: rank (22px wide, 12.5px), cover 30x40 radius 6 (the store cover from `/api/stories/<id>/cover`, else a `#e6dccb` tile with the title's first letter after "The ", "✎" for Own books), title 14.5px 500 with ellipsis. Each number cell: 14.5px tabular figures plus a 72x4 bar (`#ebe4d6` track) scaled to the column maximum; the sorted column's bar is `#5B57E8` and its number `#2b2540`, the others `#c9c2b4` and `#5a5470`. Own books show "—" for downloads. Clicking a row sets the book filter (again clears it); the chosen row is `#ECE7FF`, hover `#fbefd9`. The table follows the period and platforms but ignores the book filter, and lists every book (no top-N cut).
- **Footer note (Work 1).** "Downloads are store-book downloads from the apps. A listen is a book played for 30 seconds or more. Hours are real played time from the app; for speakers without reported time, the book’s length (hatched, estimate). Weeks start Monday, UTC. Data from {first day held}; kept daily in D1 after Analytics Engine drops raw rows." 12.5px, `#8a8399`.
- **States (Work 1).** A wait over 500 ms greys the plot with an overlay (`rgba(255,246,230,.82)`, centred "Loading…", 14px, `#5a5470`), keeps the old numbers and greys the tile numbers. An error shows a banner above the tiles (`#FFF0EA`, 1px `#f2c4b4`, radius 16, padding 18px 20px): "Couldn’t load the numbers. Analytics Engine or the database didn’t answer." and a "Try again" button (38px, radius 999, `#5B57E8`, white, 14px, 600). No empty-table or "no data" frame exists.
- **Phone (Work 3).** A note only, no frame: the tiles stack and the table keeps Book and the sorted column. The filter card already wraps.
- **Mock only, not UI.** The page heading block, the fake browser bar, the seeded random data, and the unused `setFrom`/`setTo` and `custom` branch (lines 191, 217-218; no date inputs are drawn).

**Open point: custom date range.** The slice (Design, Page; Work 3) has a custom range; the mock has four fixed periods and no date inputs (note at line 142, presets at line 206). **Default:** drop the custom range from the page (follow the mock); `/api/stats` keeps `from` and `to`, so it can return later.

**Open point: iPhone and iPad.** Decision 3 and the slice name platforms android, ios, windows, sonos, alexa. The mock has iPhone and iPad as two chips and stack colours (note at line 143, colours at lines 197-202). **Default:** follow the mock; the rollup writes `platform` as `iphone` or `ipad` from `blob12` as above, and the API's `platform=` takes android, iphone, ipad, windows, sonos, alexa. `/api/events` platforms stay android/ios/windows.

**Open point: one chart, not three.** The slice says "a bar chart per metric"; the mock has one chart switched by the three tiles (line 143). **Default:** one chart, as the mock.

**Open point: signed-in email.** The mock header shows `elinsoftuk@gmail.com` (line 35); the slice does not show it. **Default:** show the Access identity, read from the `email` claim of the already-checked `Cf-Access-Jwt-Assertion` token and returned by `/api/stats`, never typed into the HTML; the page is owner-only. If the user prefers it hidden, drop the line.

**Open point: web font.** The mock loads Fredoka from Google Fonts (lines 12-13, 16), for the canvas only; the Dino site makes no third-party requests and the mock's own intro says so (line 20). **Default:** do not copy it; use the Dino site's own font stack (system-ui fallback) in `site/dino/stats.html`.

**Open point: platform control.** The slice says platform checkboxes and an Alexa that "appears once Slice 282 ships"; the mock uses colour chips and shows Alexa now, off and dimmed (line 142). **Default:** chips, Alexa shown dimmed as in the mock.

**Open point: "from when" note.** The slice says the zero columns carry a note with the app version. The mock shows one note, for listens and hours only, with a hard-coded Dino 1.112 and 10 Nov 2026 (lines 167, 229); it draws no note for Sonos, which starts 1 Dec in the mock. **Default:** the date comes from the first day D1 holds a `listens` row (the version from Slice 288's release), no Sonos note.

**Open point: Sonos hatching.** The mock treats all Sonos hours as estimated (hatched, "Sonos (estimate)", lines 176-177, 264). Slice 289 may also store `reported` rows. **Default:** hatch only the estimated part (`seconds_estimated`); the legend says "(estimate)" whenever Sonos is on in the hours chart.

Settled by the mock, left open by the plan: grouping rules, y-axis steps, bar gaps, tile copy, the table's all-books list, the "last at" time from the API, Own books showing "—" for downloads, and that the page has no per-locale copy (English, owner only; the mock draws no language switch). New strings: none for the apps; no locale files change.

## Work

- [ ] 1. Smallest end to end: D1 database and table, the rollup for `downloads` only, `/api/stats`, and the page showing
      downloads by day with the platform filter (chips in the mock's colours) and top books, behind Access with the JWT check. Deploy and see real numbers.
- [ ] 2. Add `listens`, `seconds` and `seconds_estimated` to the rollup and the page (zero until Slices 288/289 ship; the
      tile says from which app version listens count).
- [ ] 3. Week and month grouping, the book picker, the sortable top-books table, the iPhone/iPad split and the Alexa chip
      (custom date range: see the Design open point; the mock has none).
- [ ] 4. Backfill on the first run; a test that re-running a day gives the same rows.
- [ ] 5. `docs/analytics/dino-dashboard-queries.md`: the rollup SQL, for the `analytics-query` agent to cross-check.
- [ ] 6. `worker/CLAUDE.md`: the stats routes, the Access + JWT rule, the cron and D1 binding, and that the page makes no
      third-party requests.

## Verification

- `npm ci && npm run typecheck` and `node --test worker/stats*.test.mjs`: rollup SQL per metric, idempotent upsert, week and
  month grouping, JWT accepted/rejected, `/dino/stats` on workers.dev refused without a token.
- Live: the page behind Access shows yesterday's downloads equal to an `analytics-query` count of `store_download` for the
  same day (weighted), per platform.
- Opening `/stats` signed out ends at the Access login; another email is refused.
