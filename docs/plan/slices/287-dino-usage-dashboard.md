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

## Decisions (user, 2026-11-07, RefineScope; the mock below supersedes them where they differ)

**The mock is the spec.** The user, 2026-11-07: "the mock always trumps the plan! the plan should always always always be
in a way that implements that fucking mock up." The page is built exactly as frame 200 of
`docs/design/dino-reader/Dino Reader Stats Dashboard.dc.html` shows it; this slice only adds what the mock cannot show
(data, rollup, login, tests).

1. **A listen** is a book played for at least 30 seconds; playing it again later counts again (event from Slice 288,
   Sonos from Slice 289).
2. **Hours** are real played seconds sent by the app, counted from the release that adds them (Slice 288). Speakers:
   played time if Sonos/Alexa report it, else the book's length, shown hatched as an estimate.
3. **Platforms** (from the mock): Android, iPhone, iPad, Windows, Sonos, Alexa, as colour chips. Alexa is shown now, off
   and dimmed with "not live yet", and becomes a normal chip when Slice 282 ships.
4. **Where:** `dinoreader.sirous.uk/stats`, behind Cloudflare Access letting in only `elinsoftuk@gmail.com` (the account
   the mock shows signed in). A daily rollup into permanent storage (D1) so day, week and month views keep their history
   after Analytics Engine drops the raw rows. Filters (from the mock): four fixed periods (Today, Last 7 days, Last 30
   days, This year), bars by day/week/month, book, platform chips. No custom dates.

## Design (planning defaults)

- **Rollup.** A Worker cron (hourly) queries the Analytics Engine SQL API for today and yesterday (UTC) and upserts one row
  per `day × book × platform × metric` into D1 table `daily_stats(day, book, platform, metric, value)`, primary key on the
  first four. Metrics: `downloads` (`store_download`), `listens` (`book_listened`), `seconds` (sum of played seconds),
  `seconds_estimated` (speaker plays with no reported time). Values are weighted with `SUM(double1)` (sampling). Upserting
  the same two days each hour makes it idempotent and today's view at most an hour old. The first run backfills every day
  Analytics Engine still holds. Production dataset `dino_usage` only, never `dino_usage_beta`.
- **Token.** The Worker reads Analytics Engine with the existing read-only `CF_ANALYTICS_READ_TOKEN` (in `D:\secrets`) as a
  Worker secret (`manage-secrets`), plus the account id as a var.
- **Access.** A Cloudflare Access application on `dinoreader.sirous.uk/stats*` and `/api/stats*`, one allow rule:
  `elinsoftuk@gmail.com` (one-time PIN). `/api/stats` returns the `email` claim of the checked token, which the header shows. The Worker also checks the `Cf-Access-Jwt-Assertion` token (signature against the team's
  certs, audience tag) on both paths and answers 403 without a valid one, because the `workers.dev` `/dino/stats` twin is
  not behind Access. Set up through the API if a token with Access edit rights exists; otherwise one user step.
- **Page.** `site/dino/stats.html`: plain HTML with inline script, built to match frame 200 exactly (every value in the
  Design subsection below). No third-party requests: the mock's own caption says "no web font", so the page uses the Dino
  site's font stack (`site/dino/index.html`: Fredoka first, used when installed, else the rounded system font), which is
  how the mock's Fredoka look is reached without loading a font.
- **API.** `GET /api/stats?from=&to=&group=day|week|month&platform=…&book=…` returns series, the top-books table, the
  last rollup time, the first day with listens (with the app version that started counting them) and the signed-in
  email, as data, no copy. Platform values: android, iphone, ipad, windows, sonos, alexa. Weeks start Monday, UTC.

#### Design (Dino Reader Stats Dashboard mock, section Batch 51, frame 200, added 2026-11-07)

One frame (200, line 25 of `docs/design/dino-reader/Dino Reader Stats Dashboard.dc.html`, 1280 wide, inside a browser-window illustration) plus three note paragraphs (lines 142-144) and the behaviour script (lines 149-281). The numbers are made up, drawn as if read on 15 Dec 2026. The mock agrees with the plan on layout, Access, the hourly rollup, no third-party requests, hatched estimates, the 30-second listen and Monday weeks. Where it differed from the first plan, the plan was changed to match it (list under **Plan changed to match the mock** below). The browser chrome (traffic lights, the 480px address pill, the page heading block) and the Tweaks panel for live, loading and error are illustration, not UI. Page ink `#2b2540`, muted `#5a5470` and `#8a8399`, brand `#5B57E8`, page `#F3EFE6`, cards `#FFF6E6` with a 1px `#d9d2c4` border and radius 16. Content padding 20px 32px 40px, gap 20.

- **Header (Work 1).** Dino icon 40px, radius 10; title "Dino Book Reader · Usage" (19px, 600); second line "Counts of events, never people. Updated hourly · last at 14:00 UTC." (13px, `#8a8399`). Right: a lock glyph (16px stroke icon) and the signed-in email. The "last at" time is the last rollup run, so `/api/stats` must return it (a one-row `meta` table or the max `updated_at`); the page does not work it out.
- **Filters card (Work 3).** Labels 12px, 600, `#8a8399`, letter-spacing .5, upper case: PERIOD, BARS BY, BOOK, PLATFORMS (PLATFORMS on its own full-width row). PERIOD and BARS BY are segmented pills: track padding 3, radius 999, `#F3EFE6`, 1px `#d9d2c4`; buttons 32px high, padding 0 13px, 14px, 500; selected `#fff` / `#2b2540` with shadow `0 1px 3px rgba(43,37,64,.16)`, others transparent / `#5a5470`. PERIOD: "Today", "Last 7 days", "Last 30 days", "This year". BARS BY: "Day", "Week", "Month". BOOK: a select, 38px high, min 220px, radius 10, white; first option "All books", then the store titles from `/api/stories`, last "Own books".
- **Period and grouping rules (Work 3).** Defaults: Last 30 days, bars by Day, All books, all platforms but Alexa, tile Book downloads, table sorted by Downloads. Ranges end today (UTC): 7 days = today minus 6; 30 days = today minus 29; This year = 1 Jan. "Today" forces Day and dims Week and Month to opacity .4 (not clickable). Picking "This year" while on Day switches to Week; other presets keep the grouping. Buckets cover every day of the range, empty ones included. Week key = that Monday (UTC), month key = the 1st; the label is the key's date. Bar labels "d MMM" (en-GB), month bars "MMM"; every ceil(n/14)th label is drawn.
- **Platform chips (Work 1, 3).** Fixed order and colours: Android `#4FB860`, iPhone `#5B57E8`, iPad `#A9A6F7`, Windows `#F2994A`, Sonos `#2b2540`, Alexa `#4BB3D9`. Chip 34px high, padding 0 14px 0 10px, radius 999, 14px, 500, a 12px square dot (radius 4) in its colour. On: white, 2px border in its colour. Off: transparent, 2px `#d9d2c4`, text `#8a8399`. At least one chip stays on (a click on the last one is ignored). Alexa: off, opacity .55, not clickable, note "not live yet" (12px, `#8a8399`) and tooltip "Counts start when Slice 282 ships"; it becomes a normal chip when Slice 282 ships.
- **Tiles as the chart switch (Work 1, 2).** Three tiles in a grid, `repeat(auto-fit, minmax(240px, 1fr))`, gap 14, radius 16, padding 16px 18px. The chosen tile is `#fff` with a 2px `#5B57E8` border and a "IN THE CHART" tag (11.5px, 600, `#5B57E8`); the others `#FFF6E6`, 1px `#d9d2c4`. Label 13px 600; number 34px 600 (`#c9c2b4` while loading); sub 12.5px `#8a8399`. "Book downloads": sub "Store books, from the apps. Speakers stream and don’t download." "Listens": sub "Played for 30 s or more", or "Counted from 10 Nov 2026 · played for 30 s or more" when the range starts before listens were counted. "Hours listened": the value is real plus estimated hours; sub "Real played time from the app", or "incl. 3.4 h estimated for Sonos" when estimated hours exist. Numbers use thousands separators (en-GB); hours get one decimal under 10, whole above.
- **One stacked chart (Work 1, 2).** Card title "{Book downloads|Listens|Hours listened} per {day|week|month}" (17px, 600); sub "{book} · 16 Nov – 15 Dec 2026" (13px, `#8a8399`; the exact days, start without year, end with year); legend at the right (12px squares, radius 3, 12.5px). Plot 240px high, y axis 44px wide with five ticks, 0 to 4 steps; the step is the first of 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 5000, 10000 whose four steps cover the tallest bar; grid line every 60px `#ebe4d6`, baseline `#cfc7b6`. Bars fill the width with a gap of 8px (up to 20 bars), 3px (up to 60), 1px (more). Segments stack bottom up in platform order, at least 1px high, the top one rounded `4px 4px 0 0`. Hover: brightness 1.06 and a tooltip "{label}: {total}" then one "{platform} {n}" line each. Estimated hours: segment `repeating-linear-gradient(135deg, <platform colour> 0 3px, #b8b2c8 3px 6px)`; the Sonos legend entry reads "Sonos (estimate)" on the hours chart. When the range starts before the listens date and the tile is Listens or Hours, a note sits above the plot (`#ECE7FF`, radius 10, padding 6px 10px, 12.5px): "Listens and hours count from Dino 1.112, 10 Nov 2026 (Slice 288)." (the mock's version and date are filled with Slice 288's real release; same wording). Earlier bars are empty.
- **iPhone and iPad (Work 1).** The rollup keys iOS rows by form factor: `blob12` `tablet` is `ipad`; anything else (`phone`, `other`, empty for builds before Slice 166) is `iphone`, and the hover says so for the unknown ones. `blob12` is written at `worker/events.ts:397`; the values are `phone`, `tablet`, `desktop`, `other` (`events.ts:108`). Android and Windows stay one chip each.
- **Top books table (Work 3).** Card title "Top books" (17px, 600) and "in this period and these platforms · click a book to filter, a heading to sort" (13px, `#8a8399`). Grid `minmax(0,2.2fr) repeat(3, minmax(0,1fr))`, header 12.5px 600: "Book", "Downloads", "Listens", "Hours" (numeric headings are right-aligned buttons; the sorted one reads "Downloads ↓" in `#5B57E8`; sort is descending only). Row: rank (22px wide, 12.5px), cover 30x40 radius 6 (the store cover from `/api/stories/<id>/cover`, else a `#e6dccb` tile with the title's first letter after "The ", "✎" for Own books), title 14.5px 500 with ellipsis. Each number cell: 14.5px tabular figures plus a 72x4 bar (`#ebe4d6` track) scaled to the column maximum; the sorted column's bar is `#5B57E8` and its number `#2b2540`, the others `#c9c2b4` and `#5a5470`. Own books show "—" for downloads. Clicking a row sets the book filter (again clears it); the chosen row is `#ECE7FF`, hover `#fbefd9`. The table follows the period and platforms but ignores the book filter, and lists every book (no top-N cut).
- **Footer note (Work 1).** "Downloads are store-book downloads from the apps. A listen is a book played for 30 seconds or more. Hours are real played time from the app; for speakers without reported time, the book’s length (hatched, estimate). Weeks start Monday, UTC. Data from {first day held}; kept daily in D1 after Analytics Engine drops raw rows." 12.5px, `#8a8399`.
- **States (Work 1).** A wait over 500 ms greys the plot with an overlay (`rgba(255,246,230,.82)`, centred "Loading…", 14px, `#5a5470`), keeps the old numbers and greys the tile numbers. An error shows a banner above the tiles (`#FFF0EA`, 1px `#f2c4b4`, radius 16, padding 18px 20px): "Couldn’t load the numbers. Analytics Engine or the database didn’t answer." and a "Try again" button (38px, radius 999, `#5B57E8`, white, 14px, 600). No empty-table or "no data" frame exists.
- **Phone (Work 3).** A note only, no frame: the tiles stack and the table keeps Book and the sorted column. The filter card already wraps.
- **Mock only, not UI.** The page heading block, the fake browser bar, the seeded random data, and the unused `setFrom`/`setTo` and `custom` branch (lines 191, 217-218; no date inputs are drawn).

**Plan changed to match the mock (2026-11-07; the mock wins every difference):**

- **Dates:** four fixed periods only (line 142, presets at line 206); the custom range is gone from the page. `/api/stats`
  still takes `from` and `to`, because the page sends the preset's days.
- **iPhone and iPad** are two platforms (lines 143, 197-202): the rollup writes `platform` as `iphone` or `ipad` from
  `blob12` as above. `/api/events` platforms stay android/ios/windows (that is the apps' wire format, not the page's).
- **One chart** switched by the three tiles (line 143), not a chart per metric.
- **Signed-in email** in the header (line 35): the `email` claim of the checked Access token, returned by `/api/stats`.
  The Access allow rule is `elinsoftuk@gmail.com`, the account the mock shows.
- **Font:** the look of Fredoka with no font download, through the Dino site's font stack (the mock's caption at line 20
  says "no web font"; its Google Fonts link at lines 12-13 only renders the canvas in Claude Design).
- **Platform chips** in the mock's colours, Alexa shown off and dimmed with "not live yet" (line 142).
- **"Counts from" note:** exactly the mock's wording and place (lines 167, 229): "Listens and hours count from Dino
  {version}, {d MMM yyyy} (Slice 288).", for the Listens and Hours tiles only, no Sonos note. Version and date are Slice
  288's real release, returned by `/api/stats` (the first D1 day with a `listens` row, and the version Slice 288 shipped in).
- **Sonos on the hours chart** reads "Sonos (estimate)" in the legend and its estimated hours are hatched (lines 176-177,
  264). Slice 289 therefore counts every Sonos hour as the book's length (no Sonos play reporting is built). Alexa
  hours follow the same footer rule ("for speakers without reported time, the book’s length (hatched, estimate)").
- **Try again** on the error banner reloads the numbers (the mock draws the button; it has no handler in the canvas).

Settled by the mock, left open by the plan: grouping rules, y-axis steps, bar gaps, tile copy, the table's all-books list, the "last at" time from the API, Own books showing "—" for downloads, and that the page has no per-locale copy (English, owner only; the mock draws no language switch). New strings: none for the apps; no locale files change.

## Work

- [ ] 1. Smallest end to end: D1 database and table, the rollup for `downloads` only, `/api/stats`, and the page as frame
      200 (header with the signed-in email, filters card, tiles, chart, table, footer, loading and error states) showing
      downloads, behind Access (`elinsoftuk@gmail.com`) with the JWT check. Deploy and see real numbers.
- [ ] 2. Add `listens`, `seconds` and `seconds_estimated` to the rollup and the page (zero until Slices 288/289 ship; the
      "counts from" note as in the mock).
- [ ] 3. Week and month grouping with the mock's rules, the book picker, the sortable top-books table, the iPhone/iPad
      split, the dimmed Alexa chip, and the phone layout from the mock's States note.
- [ ] 3b. Side-by-side check: the built page next to frame 200 at 1280 wide (screenshot of each), every difference fixed
      before review.
- [ ] 4. Backfill on the first run; a test that re-running a day gives the same rows.
- [ ] 5. `docs/analytics/dino-dashboard-queries.md`: the rollup SQL, for the `analytics-query` agent to cross-check.
- [ ] 6. `worker/CLAUDE.md`: the stats routes, the Access + JWT rule, the cron and D1 binding, and that the page makes no
      third-party requests.

## Verification

- `npm ci && npm run typecheck` and `node --test worker/stats*.test.mjs`: rollup SQL per metric, idempotent upsert, week and
  month grouping, JWT accepted/rejected, `/dino/stats` on workers.dev refused without a token.
- Live: the page behind Access shows yesterday's downloads equal to an `analytics-query` count of `store_download` for the
  same day (weighted), per platform.
- Opening `/stats` signed out ends at the Access login; `elinsoftuk@gmail.com` gets in, another email is refused.
- The page at 1280 wide matches frame 200 (layout, colours, copy, chip order, states); screenshots in the PR.
