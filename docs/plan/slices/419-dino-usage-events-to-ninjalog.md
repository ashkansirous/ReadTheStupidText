### Slice 419 — Dino: every usage event also goes to Ninjalog, with a funnel and a "where new users stop" dashboard
**Product:** Dino

*Built 2026-11-19 (not merged), live checks and queries not yet run; item 9 (alert) open.*

*Changed while building:* item 1's "sending stays off" result was overtaken by the owner's decision (Decisions, last line): Ninjalog has no terms or DPA, the owner said send anyway, so `NINJALOG_ENABLED` is "true" in both wrangler configs. Item 6 (backfill) dropped by the owner. Project retention set to 90 days (Project settings → Retention) on 2026-11-19 to match the privacy page; the project's storage limit reads 10 GiB (not changed). `workers-dev` shares the one Worker, so the same switch covers it.

*From review:* pass 1 high (`reviews/s419-high.md`, folder `D:/ReadTheStupidText-worktrees/reviews`): findings 1, 2, 4, 5, 6, 7 fixed; 3 handled live (retention set, see above); 8 left (Play "share" wording in item 5 is from knowledge, not a read source); 9 left (event time is checked at the first live send). Pass 2 medium (`reviews/s419-medium.md`): 1 is an owner decision (send without a DPA, quoted in Decisions); 2 and 7 fixed in `site/dino/privacy.html` (wording "no advertising or tracking companies", speaker counts named); 3 noted for the queries: the document timestamp is the batch receive time, so order within a session by `seq` / `sinceLaunch`; 4, 5, 6 accepted as nits. Findings read: see the report (counts given there).

*Checks:* `node --test worker/*.test.mjs` 439 pass, 0 fail; `npm run typecheck` green; `check-site-eol` OK.

**Touches:** `worker/ninjalog.ts` (new), `worker/ninjalog.test.mjs` (new), `worker/events.ts`, `worker/speaker-usage.ts`, `worker/index.ts`, `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `worker/CLAUDE.md`, `site/dino/privacy.html`, `PRIVACY.md`, `STORE.md`, `docs/analytics/ninjalog-queries.md` (new), `docs/analytics/ninjalog-dashboard.json` (new)

## Why

On 2026-11-19 the owner asked where the new users of the last three days left the app. Answering took a dozen agent
queries against Analytics Engine: positional columns (`blob1`..`blob20`), batch-send timestamps, sampling
(`_sample_interval` up to 4 on a two-minute session) and no funnel or timeline view. Findings that day (from
`dino_usage`, 2026-11-16..19):

- India, Android 1.131.1 (production), 07:11-07:20 UTC: four sessions, most likely one phone (same OS 14, cores 5-8,
  RAM 8+, back to back; first launch twice, so a reinstall or data clear). Onboarding finished twice, story languages
  skipped, then en+es+nb added in settings; 16 reads of one book, 14 cut off by the next start after 0 s; about 3 s
  raw (9 s weighted) listened.
- Germany, iOS 1.131.1, 2026-11-18 16:03-16:05: whole onboarding including a recording, then backgrounded on the
  shelf; no `read_started` at all; `store_unreachable` and `prices_missing` fallbacks at the start.
- Owner's wife, iOS 1.131.1 GB: onboarding finished, about 15 s read in Farsi.
- Play testers on 1.129.0 (open and closed testing tracks) post to `dino_usage`, not the beta dataset (only TestFlight
  has its own route, Slice 251), so a Play tester looked like a new user.

The owner has a Ninjalog account (project `ashkan-sirous/dino-book-reader`,
https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-, empty). Read 2026-11-19 in the owner's signed-in Chrome:
Ninjalog is an EU-hosted log search service with a Splunk-subset query language (`stats ... by`, `count(eval(...))`,
`eventstats`, `compare by=`), dashboards, alerts and an MCP connection; ingest is
`POST https://app.ninjalog.dev/v1/ingest/json` with an ingest token (Bearer, or Basic username), `#index` on the
document or `?index=` on the URL (docs: `/docs/ingest`, query reference `/docs`, dashboards `/docs/dashboards`). It has
**no funnel, retention or cohort feature**: a funnel is a `stats ... by session` query. No pricing, retention, terms or
DPA page was found on ninjalog.dev (all 404).

## Decisions (owner, 2026-11-19)

- **Plan it:** owner's "yes" to "plan that as a slice" (Worker forwards every event to Ninjalog, a funnel query and a
  "where new users stop" dashboard).
- **No install id: Decision 140 stands.** Owner picked "Keep the promise": no install or device id. Funnels and
  timelines are per launch (`session`); "likely the same phone" is a query over the device fields and time
  (`docs/analytics/ninjalog-queries.md`), never a stored or sent id. `site/dino/privacy.html` keeps "no install
  identifier".
- **Ninjalog is named and used:** owner picked "Yes, name it and send": the privacy page names Ninjalog (EU-hosted)
  as a second processor next to Cloudflare, after the terms check in item 1. If item 1 finds no terms, retention or
  DPA, the sending stays off (the flag in item 2) and the gap goes to *Outstanding work*; it never ships without them.
- Decided by this session: the Worker forwards; the app is not changed and makes no new request (no third-party
  request from a kids' app). Only what is written to `dino_usage` is forwarded, never `dino_usage_beta`
  (production-only analytics). Forwarding is fire-and-forget (`ctx.waitUntil`); a Ninjalog failure never changes the
  app's 204 or the Analytics Engine write.
- Owner 2026-11-19: Ninjalog is a friend's product with no terms or DPA; owner said send anyway ("yes! end of story"). Item 1's hold is lifted. `NINJALOG_ENABLED` is "true" in both wrangler configs (`check-configs` requires them equal); sending still needs the `NINJALOG_TOKEN` secret. Privacy page says 90 days; the project's retention is set to 90 days to match.

## Work

- [x] 1. **Terms check before any data is sent.** Read Ninjalog's terms, privacy, retention and DPA (in-app pages,
      the project settings, or ask on its Discord linked from ninjalog.dev). Record in this file: where data is
      stored, how long it is kept, whether a DPA exists, whether it may hold data from a children's app. No DPA or no
      retention statement: stop at item 2 with the flag off and add one line to *Outstanding work*.
      **Done 2026-11-19 (owner's signed-in Chrome):** hosted in the EU (ninjalog.dev home page); retention is set per
      project by us (Project settings → Retention: default "No time limit" and "No size limit"; per-index retention
      possible, `/docs/indices`). Not found anywhere (ninjalog.dev, the app, its docs): terms of service, privacy
      policy, DPA, children's data, pricing. **Result: sending stays off** (`NINJALOG_ENABLED` unset) until the owner
      confirms a DPA or terms. The privacy page change (item 5) ships with the switch, not before, so the page never
      names a processor that gets nothing; items 3, 6 (the run), 8 and 9 wait on the same answer.
      Ingest facts read the same day: `POST https://app.ninjalog.dev/v1/ingest/json?index=<name>`, body
      newline-delimited JSON (its Vector example: `framing: newline_delimited`, optional `compression: gzip`),
      `Authorization: Bearer <token>`; a document's `#index` overrides `?index=`. Dashboards: one JSON object
      `{slug, name, defaultRange?, widgets:[{id, title, x, y, w, h, query, range, viz, options}]}` on a 12-column
      grid; viz `timechart` (query ends in `timechart`), `stat` (ends in `stats count`), `table` (`stats count by f`
      or `chart ... over f`); upload on the dashboards page, `POST /api/dashboards`, or the MCP `save_dashboard`
      tool. Not stated: which document field it takes as the event time (checked at the first live send).
- [x] 2. **Forwarding in the Worker** (`worker/ninjalog.ts`): a tee around the `DINO_USAGE` dataset so every
      `writeDataPoint` (app events in `events.ts`, speaker rows in `speaker-usage.ts`) also queues one named JSON
      document: every field by its name from `events.ts` (`name`, `detail`, `book`, `language`, `version`,
      `platform`, `country`, `os`, `form`, `arch`, `cores`, `ram`, `session`, `seq`, `sinceLaunch`, `uiLang`,
      `installAge`, `launchNo`, `screen`, `seconds` ...), never `blobN`, `#index: "dino_usage"`, and the batch's
      receive time. One POST per incoming batch (NDJSON or an array if `/docs/ingest` allows, else one per event),
      sent through `defer`. Controlled by a `NINJALOG_ENABLED` var (default off) plus the `NINJALOG_TOKEN` Worker
      secret; missing either = nothing sent. Never forwards the IP, the raw body or anything `EVENT_RULES` dropped.
      Robot-filtered rows (`isRobotCount`) are not forwarded either.
- [x] 3. **Token (done 2026-11-19):** ingest token "dino-worker-production" created in the project, stored as `NINJALOG_TOKEN` in `D:\secrets\dinoreader\secrets.env`, set as the Worker secret on `readthestupidtext`. Original text: create an ingest token in the project (Project settings → Ingest tokens,
      https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/settings), store it with `manage-secrets` in
      `D:\secrets`, set it as the Worker secret. Creating the token is an account action: ask the owner once in chat
      before creating it in their signed-in browser.
- [x] 4. **Tests** (`worker/ninjalog.test.mjs`): a batch writes to Analytics Engine and posts the same named fields;
      beta path posts nothing; flag off or no token posts nothing; a Ninjalog 500 or timeout leaves the 204 and the
      AE write; dropped and robot rows are not posted; no IP in the posted body.
- [x] 5. **Privacy and store text:** `site/dino/privacy.html` names Ninjalog (EU-hosted) as a processor for the same
      anonymous counts, with its retention from item 1, and still says no install or device identifier. `STORE.md`
      and the Play Data safety / App Store privacy answers: check whether "shared" changes (a processor acting for us
      is not "shared" under Play's definition; record what was checked and the source). Line in `worker/CLAUDE.md`.
- [x] 6. **Dropped by the owner 2026-11-19: "dont copy the past data from cloudflare! the app was released just today! dont need old data".** (Was: Backfill, `tools/dino/ninjalog-backfill.mjs`): read `dino_usage` (production only, x64 and `1.0*`
      dropped as in `docs/analytics/dino-session-queries.md`) day by day over the 90 days Analytics Engine keeps,
      with the same named fields plus `sampleInterval`, and post them with their original timestamp. Idempotent per
      day (a marker field `backfill: <day>` and a check before re-posting). Run it once after item 2 is live.
- [x] 7. **Queries** (`docs/analytics/ninjalog-queries.md`) (file written; not yet run against live data, see *Not yet verified*):
      - session timeline: `index=dino_usage session=<id> | sort seq`;
      - new-user funnel per first launch (`installAge=first`): opened → onboarding finished (`intro_ended`) → book
        opened → `read_started` → 30 s listened, as `stats count(eval(...)) ... by session | stats sum(...)`, by
        platform and version;
      - "where they stopped": the last screen and last event of each first-launch session;
      - "likely the same phone": sessions on one day with equal platform, version, country, os, form, cores and ram,
        within 10 minutes of each other;
      - churn: `compare by=` between sessions with 30 s+ listened and sessions with none (version, uiLang, screen,
        fallbacks);
      - Play testers: 1.129.x and other testing-track versions listed apart (versions read from the Play tracks).
- [x] 8. **Dashboard** (file written; not yet uploaded) (`docs/analytics/ninjalog-dashboard.json`, format `/docs/dashboards`): "New users": first
      launches per day by platform, the funnel table, where first sessions stopped, fallbacks per day, listening
      seconds per day. Created in the project and kept in the repo.
- [x] 9. **Alert:** fallback rows over a threshold in an hour (threshold from the last 30 days' data), notify the
      owner by email if Ninjalog offers it. **Done 2026-11-19:** the busiest hour in `dino_usage` over 30 days had 11
      fallbacks (2026-11-18 14:00 UTC), so the alert "Dino fallbacks spike (over 20 in the window)" is
      `index=dino_usage name=fallback | stats count | where count > 20` over 1 hour, group `default` (email to all
      project members). https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/alerts

**After the merge (2026-11-19, this session):** deploy of 21f4925a: Workers Builds `completed success`. Token
checked with one test document to index `smoke` (HTTP 200, found in Search). No production count arrived in
`dino_usage` between the deploy (21:29 UTC) and 21:45 UTC (Analytics Engine query), so the first forwarded
event is not seen yet. Every query was run in Search and fixed where the engine refused it (rules in
`docs/analytics/ninjalog-queries.md`: no `sort` on raw events, `count(eval())` only in the first aggregation,
`span=` automatic, no `last()`); the dashboard was uploaded and opens at
https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/d/new-users (five widgets, "no data" until events arrive).

## Verification

- `node --test worker/ninjalog.test.mjs worker/events.test.mjs worker/speaker-usage.test.mjs`.
- After the merge deploys (Workers Builds): an event from the production app (or a curl to `/api/events` with a
  valid batch, labelled as a test and deleted from Ninjalog after) appears in `index=dino_usage` within a minute with
  named fields, and the same count appears in `dino_usage`.
- The funnel query reproduces this file's 2026-11-19 findings (India: onboarding finished, 16 reads, about 3 s raw).
- The dashboard opens in the project and shows the last 7 days.
- No app change, so no Android or iOS build; both platforms' events are checked in the funnel query.

#### For CLAUDE.md

- Every `dino_usage` write is also forwarded to Ninjalog by the Worker (`worker/ninjalog.ts`), never `dino_usage_beta`, never the IP, raw body, dropped or robot rows; fire-and-forget via `ctx.waitUntil`. Needs `NINJALOG_ENABLED` "true" (equal in both wrangler configs) and the `NINJALOG_TOKEN` secret. The app makes no request to Ninjalog. (Slice 419: docs/plan/slices/419-dino-usage-events-to-ninjalog.md)

#### For README.md

Not user-facing (privacy page text is in `site/dino/privacy.html`).

#### Not yet verified

- [session] After the merge deploys: first live event appears in `index=dino_usage` in Ninjalog with named fields and the same count as `dino_usage`.
- [session] Which document field Ninjalog uses as event time (the timestamp is the batch receive time; order within a session by `seq`/`sinceLaunch`).
- [session] The funnel query returns rows for real first launches and the dashboard widgets show data (both parse; no rows yet).
