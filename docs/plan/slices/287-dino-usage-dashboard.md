# Slice 287 — Dino: a public usage dashboard at dinoreader.sirous.uk/stats

*Built 2026-11-08 (wave 43, night 2026-11-08-0106), PR #940, not merged. Code for Work 1-6 is built and passes the local checks; nothing is live (no D1 database, binding, secret or deploy), see Waiting on the user.* Server only (Cloudflare Worker, D1, the Dino site). The apps do not change here.

**User answer (2026-11-08): the dashboard is public.** "its not private! I want everyone to see that dashboard". So there is
no Cloudflare Access, no Zero Trust, no sign-in, no JWT check and no signed-in email; anyone can open `/stats`. This
overrides the mock's lock glyph and email in the header (the user's own words beat the mock here) and the earlier
"behind Access" decision. Zero Trust was not activated (the checkout was left at the card-authorisation step).

**No Access step any more.** The wrangler OAuth login on this machine has `d1 (write)` and `workers_scripts (write)` (checked with `wrangler whoami`, 2026-11-08), but the night run's live steps were still refused (see below).

**Waiting on the user (2026-11-08):** the user creates the D1 database, applies the schema and adds the binding in both configs, sets the `CF_ANALYTICS_READ_TOKEN` secret and the `ANALYTICS_ACCOUNT_ID` var, then deploys; or adds an `autoMode` allow line so a night run can do it. The permission check denied `wrangler secret put` as "[Secret-Store Writes]" and `wrangler d1 create` with no reason given. The deploy also waits on the Slice 282 observability/privacy bullet. The code for Work items 1-6 is built, but these live steps are not done: `wrangler d1 create dino-stats`, applying `worker/schema/stats.sql` remotely, the D1 `STATS` binding block in both wrangler configs (left out on purpose: an invented `database_id` would break the next Workers Builds deploy), `wrangler secret put CF_ANALYTICS_READ_TOKEN`, the `ANALYTICS_ACCOUNT_ID` var, and the deploy. The exact steps are in `docs/DEPLOYING.md`, section "The Dino usage dashboard". Until then `/api/stats` answers 503 `data_unavailable` and the cron (already in both configs) logs and returns. The "counts from" note's version and date are fixed constants in `worker/stats.ts` (`LISTENS_SINCE_VERSION` 1.115, `LISTENS_SINCE_DAY` 2026-11-07): Slice 288 (PR #901, merge 41419276) first shipped in tag `dino-v1.115.0`, tagged 2026-11-07 (the mock's "1.112, 10 Nov 2026" was made up).

**Depends on:** nothing to start. Slice 288 (the app's listens and listening time) and Slice 289 (Sonos plays)
fill the listen and hour numbers; until they ship those columns show zero with a note saying from which version
they count.
**Touches:** `worker/stats.ts` (new: page data API), `worker/stats-rollup.ts` (new: cron rollup),
`worker/stats*.test.mjs` (new), `worker/index.ts` (routes + `scheduled`),
`worker/schema/stats.sql` (new D1 table), `wrangler.jsonc`, `wrangler.workers-dev.jsonc` (D1 binding, cron trigger,
`run_worker_first` for `/stats`, `/api/stats` and their `/dino` twins), `site/dino/stats.html` (new), `worker/CLAUDE.md`,
`docs/analytics/dino-dashboard-queries.md` (new), `worker/check-configs.mjs`, `worker/downloads.ts` (`resolveSite`, `/dino/` prefix), `docs/DEPLOYING.md` (D1 creation, the secret), `package.json`/`package-lock.json` (only if a dependency is added), this file.

**Deploy (claim note, 2026-11-08):** the Worker deploy waits on plan.md's *Worker not deployed* / Slice 282 observability bullet (deploying publishes privacy text that may be untrue), so this slice builds, creates the D1 database (its id goes into both wrangler configs) and sets the secret, but does not deploy; the live checks go to *Not yet verified* until the next deploy.

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
4. **Where:** `dinoreader.sirous.uk/stats`, public, no sign-in (user, 2026-11-08; replaces "behind Cloudflare Access"). A daily rollup into permanent storage (D1) so day, week and month views keep their history
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
- **Public, read-only.** No login. `/stats`, `/api/stats` and their `workers.dev` `/dino` twins are open to everyone and
  only read D1 (never Analytics Engine per request, so a visitor cannot spend the token's quota). `/api/stats` answers
  with `Cache-Control: public, max-age=300` (the data changes hourly), takes only the documented query parameters
  (anything else is a 400) and returns aggregate counts only: no per-event rows, no country, no version below the
  "counts from" line.
- **Page.** `site/dino/stats.html`: plain HTML with inline script, built to match frame 200 exactly (every value in the
  Design subsection below). No third-party requests: the mock's own caption says "no web font", so the page uses the Dino
  site's font stack (`site/dino/index.html`: Fredoka first, used when installed, else the rounded system font), which is
  how the mock's Fredoka look is reached without loading a font.
- **API.** `GET /api/stats?from=&to=&group=day|week|month&platform=…&book=…` returns series, the top-books table, the
  last rollup time and the first day with listens (with the app version that started counting them), as data, no copy. Platform values: android, iphone, ipad, windows, sonos, alexa. Weeks start Monday, UTC.

#### Design (Dino Reader Stats Dashboard mock, section Batch 51, frame 200, added 2026-11-07)

One frame (200, line 25 of `docs/design/dino-reader/Dino Reader Stats Dashboard.dc.html`, 1280 wide, inside a browser-window illustration) plus three note paragraphs (lines 142-144) and the behaviour script (lines 149-281). The numbers are made up, drawn as if read on 15 Dec 2026. The mock agrees with the plan on layout, Access, the hourly rollup, no third-party requests, hatched estimates, the 30-second listen and Monday weeks. Where it differed from the first plan, the plan was changed to match it (list under **Plan changed to match the mock** below). The browser chrome (traffic lights, the 480px address pill, the page heading block) and the Tweaks panel for live, loading and error are illustration, not UI. Page ink `#2b2540`, muted `#5a5470` and `#8a8399`, brand `#5B57E8`, page `#F3EFE6`, cards `#FFF6E6` with a 1px `#d9d2c4` border and radius 16. Content padding 20px 32px 40px, gap 20.

- **Header (Work 1).** Dino icon 40px, radius 10; title "Dino Book Reader · Usage" (19px, 600); second line "Counts of events, never people. Updated hourly · last at 14:00 UTC." (13px, `#8a8399`). The mock's lock glyph and signed-in email at the right are not built: the page is public (user, 2026-11-08). The "last at" time is the last rollup run, so `/api/stats` must return it (a one-row `meta` table or the max `updated_at`); the page does not work it out.
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
- **Signed-in email** in the header (line 35): dropped by the user's 2026-11-08 answer (public page, no sign-in).
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

### Rollup state (design, 2026-11-08)

One design replaces the earlier backfill cursor (`last_rollup_at`, dropped):

- State: D1 table `rollup_days(day TEXT PRIMARY KEY, rolled_at TEXT)` (`worker/schema/stats.sql`), one row per day whose rollup was written successfully. No cursor.
- Each run's days: today and yesterday (UTC) always, plus at most 10 of the oldest days in [today-89, today-2] that have no `rollup_days` row (a first backfill of 88 days takes 9 hourly runs). Never a day older than today-89 (Analytics Engine keeps about 90 days), so old D1 rows are never read, rewritten or deleted.
- Per run (check 2, 2026-11-08): one AE query per metric covering all the run's target days, `GROUP BY day` (4 AE fetches a run, not per day); then per-day decisions. If any of the four answers non-200 or does not parse to `{ data: [...] }`, the whole run is skipped (logged as an Analytics Engine skip, nothing written, retried next hour).
- Guard: AE only appends inside its retention, so if the new total of any metric for a day is lower than D1's total for that day (one D1 totals read for all days), D1's rows stay and `stats-rollup: lower total from AE` is logged. The day is marked in `rollup_days` (D1 already holds the higher, correct data), so it takes no more backfill slot.
- Write: one D1 batch per day = delete the day's `daily_stats` rows, insert the new rows, upsert `rollup_days(day, now)`. Data and mark land together or not at all; a batch failure is logged on its own (`D1 batch failed for <day>`, not as an AE skip), leaves the day unmarked and goes on with the next day.
- AE queries (check 3, #3): each filters on exactly the run's target days (`formatDateTime(timestamp, '%Y-%m-%d') IN (...)`), never a min-max span, so a stuck old day cannot make a run read the days between it and yesterday.
- Day batch (check 3, #1): exactly 3 D1 statements (delete the day, ONE multi-row insert of all its rows, upsert `rollup_days`). The insert is `INSERT ... SELECT ... FROM json_each(?)` with ONE bound parameter (the day's rows as a JSON array), so D1's per-statement bound-parameter limit never needs chunking (the docs lookup did not state the limit).
- Subrequests, counting every D1 statement as one (the docs do not say whether a batch is one): 4 AE + day list read + totals read + 12 days x 3 statements + meta write = 43 at most, under 50 (Free); a test counts the fetch calls and every D1 statement of a full catch-up run.
- The page's "last at" time is the separate meta key `last_run_at`, set at the end of a run where today's day was written or kept (a day kept by the guard counts as success); a run where today's day was not written is `failed` even if older days were written. `/api/stats` returns it as `updatedAt`.

## Work

- [x] 1. (Code built; the live D1 database, deploy and real numbers are not done, see Waiting on the user.) Smallest end to end: D1 database and table, the rollup for `downloads` only, `/api/stats`, and the page as frame
      200 (header without the lock and email, filters card, tiles, chart, table, footer, loading and error states) showing
      downloads, public. Deploy and see real numbers.
- [x] 2. Add `listens`, `seconds` and `seconds_estimated` to the rollup and the page (zero until Slices 288/289 ship; the
      "counts from" note as in the mock).
- [x] 3. Week and month grouping with the mock's rules, the book picker, the sortable top-books table, the iPhone/iPad
      split, the dimmed Alexa chip, and the phone layout from the mock's States note.
- [ ] 3b. Side-by-side check: the built page next to frame 200 at 1280 wide (screenshot of each), every difference fixed
      before review. (Not done: the mock's `support.js` is not in the repo, so frame 200 could not be rendered; the page was checked in six states at 1280 instead.)
- [x] 4. Backfill on the first run; a test that re-running a day gives the same rows.
- [x] 5. `docs/analytics/dino-dashboard-queries.md`: the rollup SQL, for the `analytics-query` agent to cross-check.
- [x] 6. `worker/CLAUDE.md`: the stats routes (public, read-only from D1, cached 5 minutes), the cron and D1 binding, and that the page makes no
      third-party requests.

## Verification

- `npm ci && npm run typecheck` and `node --test worker/stats*.test.mjs`: rollup SQL per metric, idempotent upsert, week and
  month grouping, unknown query parameters refused, the response carries no per-event rows.
- Live: the public page shows yesterday's downloads equal to an `analytics-query` count of `store_download` for the
  same day (weighted), per platform.
- Opening `/stats` in a private window, signed in to nothing, shows the page.
- The page at 1280 wide matches frame 200 (layout, colours, copy, chip order, states); screenshots in the PR.

## Checked before the night run (2026-11-07)

- (Superseded 2026-11-08.) This said Access was the one user step. The page is now public, so there is no Access step;
  the D1 database, the Worker secret and the deploy go through the wrangler OAuth login (`d1 (write)`, `workers_scripts (write)`).

## Review notes

**From review:** pass 1 (high), `reviews/pr-940-high.md`.

- Applied #1-#9: the rollup re-reads from the day before the last run (at most 92 days), weights every sum by `_sample_interval` as the repo's usage reports do, `/api/stats` answers from the Workers Cache API for 300 s keyed by the normalised query, the Top books table lists books the counts name but the store list lacks, the page repaints a control only when its markup changes, a failed reload clears the numbers, the cover URL goes in through the DOM, Alexa becomes a normal chip once `platforms` in the answer holds `alexa` (dimmed until then), and `/dino/stats` and `/stats/` redirect to `/stats` on the Dino host with tests for the routing helpers.
**From review:** pass 2 (medium), `reviews/pr-940-medium.md`.

- Applied #1, #2 and #6 (`reviews/pr-940-medium.md`): "Rollup only upserts returned rows" (#1: each re-read day is replaced in one D1 batch: delete the day, then insert, so vanished keys go), #2 (at most 31 days per run, oldest first, progress in `last_rollup_at`; a day whose write fails is logged and skipped), #6 (`/stats` is mapped only for the Dino site, `DINO_ONLY_PAGE_FOR_PATH` in `worker/downloads.ts`).
- Not a bug #3 (`reviews/pr-940-medium.md`): "seconds sums read_paused + read_stopped" - the pause and stop rows carry per-segment seconds (`src/DinoReader.App/ListenTally.cs:113-116`), the app's `book_listened` carries none; recorded in `docs/analytics/dino-dashboard-queries.md`.
- Skipped #4 (`reviews/pr-940-medium.md`): "Cache key per normalised query, never invalidated; Cache.put no-op on workers.dev" - the page only sends the documented parameter set and production is the custom domain, where Cache.put works
- Skipped #5 (`reviews/pr-940-medium.md`): "SELECT DISTINCT platform and MIN(day) scan whole table on every cache miss" - daily_stats holds one row per day x book x platform x metric (thousands of rows), and the 300 s cache fronts it

- Skipped #10 (`reviews/pr-940-high.md`): "has no Cancel; stack-conventions asks the loading state to name what it waits for" - the mock (frame 200 States note) specifies the overlay text "Loading…" with no Cancel; the mock wins.

**From review:** check pass 1 (medium), `reviews/pr-940-check1-medium.md`.

- Applied #1-#5 with one rollup design ("Rollup state (design, 2026-11-08)" above: `rollup_days`, today and yesterday every run, at most 29 oldest undone days of [today-89, today-2], lower-total guard, one batch per day, `last_run_at`), #7 (the Alexa auto re-load carries `auto`: a user click supersedes it by token, a failed one keeps the shown data and the chip), #9 (a book missing from the store list is added to the select, `bookList()` in `site/dino/stats.html`).
- Skipped #6 (`reviews/pr-940-check1-medium.md`): "Every uncached /api/stats runs whole-table MIN(day) and DISTINCT platform; cache separately or store in meta." - daily_stats holds thousands of rows and the 300 s cache fronts it
- Skipped #8 (`reviews/pr-940-check1-medium.md`): "First weekly/monthly bucket of a range is partial but labelled as the whole bucket." - the mock's rule labels a bucket by its key date (week = that Monday, month = the 1st); the mock wins

**From review:** check pass 2 (medium), `reviews/pr-940-check2-medium.md`.

- Applied #1-#6: #2-#5 within the "Rollup state (design, 2026-11-08)" subsection above, which now says so (one AE query per metric for the whole run, kept days marked, run fails unless today was written or kept, D1 batch errors logged apart from AE skips, under 50 subrequests); #1 (the failed auto re-load restores the chips from before the Alexa switch-on) and #6 (`data.books` checked with `Array.isArray`) in `site/dino/stats.html`.

**From the device run:** `reviews/s287-device/results.md`.

- Fixed in `site/dino/stats.html`: (a) with no rollup time known (error or never run) the header's second line ends after "Updated hourly." with no "last at" part; (b) in the error state with no loaded data the tile numbers show "—" in the loading grey `#c9c2b4` instead of 0 / 0.0, and with earlier data they keep the old numbers greyed, as the mock's States note says for loading.

**From review:** check pass 3 (medium), `reviews/pr-940-check3-medium.md`.

- Applied #1, #3 and #4: #1 and #3 within the "Rollup state (design, 2026-11-08)" subsection above (at most 10 old days a run, a day is exactly 3 statements with one `json_each` insert, subrequests counted per statement: 43 at most; the AE queries name exactly the run's days), #4 (a failed auto re-load also resets `alexaSeen`, so the switch-on is retried on the next successful load) in `site/dino/stats.html`.
- Skipped #2 (`reviews/pr-940-check3-medium.md`): "a 200 from AE with an empty or partial `data` for a day with no D1 rows is written and marked done; days older than today-2 are never re-read" - Analytics Engine is append-only inside its retention, so an old day that answers empty had no events; today and yesterday are re-read every run, and the lower-total guard covers a short answer for a day D1 already holds

**From review:** check pass 4 (medium), `reviews/pr-940-check4-medium.md` (3 findings read).

- #3 (the `json_each` insert only ran against mocks): covered by the local D1 run, which passed (`reviews/s287-device/results.md`: the SQL on real D1 semantics on local D1). The first run on real D1 stays in *Not yet verified*.
- #1 is the same point as check3 #2 (an empty AE answer for an old day), whose skip is recorded under check pass 3.
- Skipped #2 (`reviews/pr-940-check4-medium.md`): "partial-answer check uses a fixed 1e-6 epsilon on float sums" - weighted sums here are at most tens of thousands; float reordering error at that size is around 1e-9, far under the 1e-6 margin

**From review:** check pass 11 (medium), `reviews/pr-940-check11-medium.md` (3 findings read, none applied).

- Skipped #1 (`reviews/pr-940-check11-medium.md`): "workers.dev `/dino/stats/` is not canonicalised" - workers.dev is the test host; a second URL there harms nothing
- Skipped #2 (`reviews/pr-940-check11-medium.md`): "no case for workers.dev `/dino/stats/`, workers.dev query string, or RTST `/stats.html`; loop test too loose" - the routing test already walks every `run_worker_first` stats path on both hosts
- Skipped #3 (`reviews/pr-940-check11-medium.md`): "RTST `/stats.html` falls to ASSETS (404 body differs from `/stats`)" - the RTST site has no `/stats.html` file, so the asset layer answers 404 as for any unknown page

**From review:** check pass 5 (medium), `reviews/pr-940-check5-medium.md`.

- Rule (by construction, `site/dino/stats.html`): the page renders from one state value, `loading` | `ready(data)` | `error(data|null)`, and every paint goes through `paint()`. `error(null)` shows the banner, tiles "—" in `#c9c2b4`, the chart plot empty (axes and gridlines, no bars, no numbers) and an empty table body, and clears the `loading` class; `error(data)` keeps the previous numbers greyed.
- Applied #1, #2 (only a fetch, HTTP or JSON failure is the error state; an exception while painting or handling the response is logged with `console.error` and also shows the banner), #3 (tiles repaint only when their markup changes, so focus stays) and the `loading` class part of #4.
- Skipped #3 (`reviews/pr-940-check5-medium.md`): "header always says "Updated hourly" with no failure indicator and drops last-updated time when data is null" - the header without a time is the device-run fix for an unknown time (see above), and the banner is the failure indicator
- Skipped #4 (`reviews/pr-940-check5-medium.md`): "in error state tile subtitles still say "Counted from Dino 1.115" (hard-coded fallback) beside dash values" - the rest of #4 is not changed: the subtitles are the mock's own wording and the mock wins
- Skipped #6 (`reviews/pr-940-check5-medium.md`): "error banner always blames" - the banner copy and the (Slice 288) note are the mock's own wording, and the mock wins

**From review:** check pass 6 (medium), `reviews/pr-940-check6-medium.md`.

- Design that replaces the Alexa auto re-load (it caused check3 #4 and check6 #2 and #3, so the mechanism is gone: no `auto`, no `alexaSeen`): the Alexa chip is dimmed and not clickable ("not live yet") while the API's `platforms` list lacks `alexa`; once the list has it, it is a normal clickable chip that starts OFF (the mock's default is every platform but Alexa) and the page never switches it on. Earlier notes above about the auto re-load (check1 #7, check2 #1, check3 #4) describe the old design.
- Applied #1 (`error(data)` greys the chart plot and the table rows with the same `stale` treatment as the tiles), #2 and #3 (both vanish with the auto re-load) in `site/dino/stats.html`.

**From the device rerun:** `reviews/s287-device/results.md`.

- Fixed in `site/dino/stats.html`: (a) in `error(null)` the "—" tile values are `#c9c2b4` (body class `blank`); (b) in `error(data)` the chart title, subtitle, day labels, bars and legend use the range, grouping, book and platforms the shown data was fetched for, not the new filter's.

**From review:** check pass 7 (medium), `reviews/pr-940-check7-medium.md`.

- One rule by construction, replacing the `view`/`data` handling (third round on "old data painted with new filters", #2 and #3): the page keeps `shown = { data, query }`, the data with the exact query (range, grouping, book, platforms) it was fetched for, set only when a response succeeds. Tiles, chart (title, sub, labels, bars, legend) and table always paint from `shown.query` and `shown.data`, in every state: loading and error grey them; `shown` null is the blank state. Only the filter controls show the user's pending choice in `st`. There is no `view`, `vw()` or global `data` any more.
- Applied #1 (one `AbortController` per request; a newer request aborts the older, which is ignored, not an error; a 20 s timeout aborts into the error state), #4 (store titles that are not strings become "Book <id>" in `loadStories`), #5 (`hrs` rounds to one decimal first, then picks the format, so 9.97 shows "10").

**From review:** check pass 8 (medium), `reviews/pr-940-check8-medium.md`.

- Applied #1 (the `loading` body class, which greys the tile numbers, is set only once the overlay is on, so it starts together with the overlay after 500 ms and never at once).
- Skipped #2 (`reviews/pr-940-check8-medium.md`): "public page shows internal slice numbers" - the Slice 288 note and the Alexa tooltip are the mock's own copy, and the mock wins
- Skipped #3 (`reviews/pr-940-check8-medium.md`): "a book id not matching" - store book ids are 32 lowercase hex (live /api/stories read 2026-11-08, e.g. 67695905c10a46bd81aa498beab9609e) and own books are `own`, so BOOK_PATTERN accepts every id the page can send

**From review:** check pass 9 (medium), `reviews/pr-940-check9-medium.md`.

- Applied #1 (`/stats/`, `/stats.html` and their `/dino` twins added to `run_worker_first` in both wrangler configs; test in `worker/stats.test.mjs`) and #3 (`keepInCache` in `worker/stats.ts`: a failed `Cache.put` is logged, the D1 data is still returned).
- Skipped #2 (`reviews/pr-940-check9-medium.md`): "the cache key includes arbitrary valid from/to pairs, so a caller can make a distinct key per request" - from/to are validated dates and a cache miss is one indexed read on a table of thousands of rows
- Skipped #4 (`reviews/pr-940-check9-medium.md`): "tiles keep full colour for up to 500 ms on first paint" - under 500 ms nothing changes by rule, the dash shows plain for that moment
- Skipped #5 (`reviews/pr-940-check9-medium.md`): "a lagging AE answer for day -2 or older freezes a wrong value" - today and yesterday are re-read every run, and an older day cannot still be arriving in Analytics Engine
- Skipped #6 (`reviews/pr-940-check9-medium.md`): "missing ANALYTICS_ACCOUNT_ID/token makes the hourly cron a silent no-op logged only with console.log" - the log line lands in Workers Logs, which is where the deploy check looks
- Skipped #7 (`reviews/pr-940-check9-medium.md`): "HEAD gets 405" - the page and API only use GET

**From review:** check pass 10 (medium), `reviews/pr-940-check10-medium.md`.

- Applied #4 (`/stats.html` redirects to `/stats` on the Dino host and `/dino/stats.html` to `/dino/stats` on workers.dev, `canonicalPath` in `worker/downloads.ts`; test in `worker/stats.test.mjs` walks every stats path in `run_worker_first`).
- Skipped #1 (`reviews/pr-940-check10-medium.md`): "edge cache is a no-op on workers.dev hosts, so every stats request there runs the five D1 statements" - production is the custom domain, where the cache works; workers.dev is the test host
- Skipped #2 (`reviews/pr-940-check10-medium.md`): "`await keepInCache` blocks the first uncached 200 on a slow Cache.put" - one uncached response per 5 minutes per query waits for one Cache.put
- Skipped #3 (`reviews/pr-940-check10-medium.md`): "declares the cron trigger but no D1 STATS binding; when the binding is added to wrangler.jsonc only, a workers-dev deploy drops it" - worker/check-configs.mjs (run by npm run typecheck) deep-compares the two configs and fails when they differ, and the D1 binding is added to both at deploy time per docs/DEPLOYING.md

**Local checks (night 2026-11-08-0106), `reviews/s287-device/results.md`: PASS.** The SQL on real D1 semantics on local D1; `/api/stats` codes (400 unknown parameter, 503 no binding); the page in six states at 1280 wide.

**Built facts:** listens count from Dino 1.115, 7 Nov 2026 (PR #901, tag `dino-v1.115.0`). The page renders from one state with `shown = { data, query }`, aborts a superseded request and times out at 20 s, and has no auto re-load; the Alexa chip is dimmed until Alexa rows exist, then normal and off.

#### For CLAUDE.md

- Nothing new to lift: `worker/CLAUDE.md` already holds the stats rule (built in Touches, Work 6).

#### Not yet verified

- Live yesterday's downloads equal an `analytics-query` count per platform.
- A private-window open of the public page shows it.
- The first real cron run: the `json_each` insert on real D1, and the 9 hourly runs that backfill.
- The page side by side with mock frame 200 at 1280 (the mock's `support.js` is not in the repo, so it could not be rendered).
