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

## Work

- [ ] 1. Smallest end to end: D1 database and table, the rollup for `downloads` only, `/api/stats`, and the page showing
      downloads by day with the platform filter and top books, behind Access with the JWT check. Deploy and see real numbers.
- [ ] 2. Add `listens`, `seconds` and `seconds_estimated` to the rollup and the page (zero until Slices 288/289 ship; the
      tile says from which app version listens count).
- [ ] 3. Week and month grouping, custom date range, the book picker, the sortable top-books table.
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
