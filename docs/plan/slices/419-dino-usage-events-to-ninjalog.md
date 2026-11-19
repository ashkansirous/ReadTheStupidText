### Slice 419 — Dino: every usage event also goes to Ninjalog, with a funnel and a "where new users stop" dashboard
**Product:** Dino

*Planned.*

**Touches:** `worker/ninjalog.ts` (new), `worker/ninjalog.test.mjs` (new), `worker/events.ts`, `worker/speaker-usage.ts`, `worker/index.ts`, `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `worker/CLAUDE.md`, `site/dino/privacy.html`, `STORE.md`, `tools/dino/ninjalog-backfill.mjs` (new), `docs/analytics/ninjalog-queries.md` (new), `docs/analytics/ninjalog-dashboard.json` (new)

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

## Work

- [ ] 1. **Terms check before any data is sent.** Read Ninjalog's terms, privacy, retention and DPA (in-app pages,
      the project settings, or ask on its Discord linked from ninjalog.dev). Record in this file: where data is
      stored, how long it is kept, whether a DPA exists, whether it may hold data from a children's app. No DPA or no
      retention statement: stop at item 2 with the flag off and add one line to *Outstanding work*.
- [ ] 2. **Forwarding in the Worker** (`worker/ninjalog.ts`): a tee around the `DINO_USAGE` dataset so every
      `writeDataPoint` (app events in `events.ts`, speaker rows in `speaker-usage.ts`) also queues one named JSON
      document: every field by its name from `events.ts` (`name`, `detail`, `book`, `language`, `version`,
      `platform`, `country`, `os`, `form`, `arch`, `cores`, `ram`, `session`, `seq`, `sinceLaunch`, `uiLang`,
      `installAge`, `launchNo`, `screen`, `seconds` ...), never `blobN`, `#index: "dino_usage"`, and the batch's
      receive time. One POST per incoming batch (NDJSON or an array if `/docs/ingest` allows, else one per event),
      sent through `defer`. Controlled by a `NINJALOG_ENABLED` var (default off) plus the `NINJALOG_TOKEN` Worker
      secret; missing either = nothing sent. Never forwards the IP, the raw body or anything `EVENT_RULES` dropped.
      Robot-filtered rows (`isRobotCount`) are not forwarded either.
- [ ] 3. **Token:** create an ingest token in the project (Project settings → Ingest tokens,
      https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/settings), store it with `manage-secrets` in
      `D:\secrets`, set it as the Worker secret. Creating the token is an account action: ask the owner once in chat
      before creating it in their signed-in browser.
- [ ] 4. **Tests** (`worker/ninjalog.test.mjs`): a batch writes to Analytics Engine and posts the same named fields;
      beta path posts nothing; flag off or no token posts nothing; a Ninjalog 500 or timeout leaves the 204 and the
      AE write; dropped and robot rows are not posted; no IP in the posted body.
- [ ] 5. **Privacy and store text:** `site/dino/privacy.html` names Ninjalog (EU-hosted) as a processor for the same
      anonymous counts, with its retention from item 1, and still says no install or device identifier. `STORE.md`
      and the Play Data safety / App Store privacy answers: check whether "shared" changes (a processor acting for us
      is not "shared" under Play's definition; record what was checked and the source). Line in `worker/CLAUDE.md`.
- [ ] 6. **Backfill** (`tools/dino/ninjalog-backfill.mjs`): read `dino_usage` (production only, x64 and `1.0*`
      dropped as in `docs/analytics/dino-session-queries.md`) day by day over the 90 days Analytics Engine keeps,
      with the same named fields plus `sampleInterval`, and post them with their original timestamp. Idempotent per
      day (a marker field `backfill: <day>` and a check before re-posting). Run it once after item 2 is live.
- [ ] 7. **Queries** (`docs/analytics/ninjalog-queries.md`): written and run against the live data:
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
- [ ] 8. **Dashboard** (`docs/analytics/ninjalog-dashboard.json`, format `/docs/dashboards`): "New users": first
      launches per day by platform, the funnel table, where first sessions stopped, fallbacks per day, listening
      seconds per day. Created in the project and kept in the repo.
- [ ] 9. **Alert:** fallback rows over a threshold in an hour (threshold from the last 30 days' data), notify the
      owner by email if Ninjalog offers it.

## Verification

- `node --test worker/ninjalog.test.mjs worker/events.test.mjs worker/speaker-usage.test.mjs`.
- After the merge deploys (Workers Builds): an event from the production app (or a curl to `/api/events` with a
  valid batch, labelled as a test and deleted from Ninjalog after) appears in `index=dino_usage` within a minute with
  named fields, and the same count appears in `dino_usage`.
- Backfill: per day, Ninjalog's `stats sum(sampleInterval)` equals Analytics Engine's `SUM(_sample_interval)` for
  that day.
- The funnel query reproduces this file's 2026-11-19 findings (India: onboarding finished, 16 reads, about 3 s raw).
- The dashboard opens in the project and shows the last 7 days.
- No app change, so no Android or iOS build; both platforms' events are checked in the funnel query.
