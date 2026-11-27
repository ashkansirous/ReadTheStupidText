# Dino usage in Ninjalog: session timeline, new-user funnel, where users stop (Slice 419)

**Status: every query below was run in the project's Search on 2026-11-19 and parses** (no rows yet: forwarding went live with the deploy of 21f4925a). The Worker forwards every production `dino_usage` count to Ninjalog (project `ashkan-sirous/dino-book-reader`, index `dino_usage`); no old data is copied (owner, 2026-11-19). Search: https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/- . Still unconfirmed until the first live rows: which document field Ninjalog uses as the event time (the Worker sends ISO-8601 `timestamp`, the batch receive time).

Fields are the named ones of `worker/events.ts` (`name`, `detail`, `book`, `language`, `source`, `version`, `platform`, `day`, `session`, `seq`, `sinceLaunch`, `count`, `deviceCount`, `seconds`, `uiLang`, `installAge`, `launchNo`, `screen`, `country`, `os`, `form`, `arch`, `cores`, `ram`, `sampleRate`, `sampleInterval`). A field a count did not carry is absent. There is no install or device id (Decision 140): a funnel is per launch (`session`).

Engine rules learned on 2026-11-19 (the query language is a Splunk subset, reference at https://app.ninjalog.dev/docs):

- `sort` orders an aggregation's rows only; raw events cannot be sorted, so a timeline is a `stats ... by seq` table.
- `count(eval(...))` works only in the first aggregation; after a `stats`, `eval` a 0/1 column and `sum` it.
- `eventstats` works on events (before a `stats`), not after one.
- `timechart` picks its own bucket (`span=` is automatic only).
- There is no `last()` for text fields: the last event of a session is the row whose `seq` equals the session's `max(seq)`.
- An error shows as one red line under the query box (for example ``unknown command `x` ``).

Test rows are not sent (the Worker forwards only what it wrote to `dino_usage`), but `dino_usage` holds Play testing-track builds too; the last query lists versions so they can be kept apart.

## Session timeline

```
index=dino_usage session=<32 hex> | stats values(sinceLaunch) as sinceLaunch, values(name) as name, values(screen) as screen, values(detail) as detail, values(book) as book, values(language) as language by seq | sort seq
```

## New-user funnel (first launches)

One row per first-launch session with what it reached, then totals by platform (add `version` to both `by` lists to split by version):

```
index=dino_usage installAge=first platform!=sonos platform!=alexa
| stats count(eval(name="intro_ended")) as onb, count(eval(name="book_opened")) as bo, count(eval(name="read_started")) as rs, count(eval(name="book_listened")) as l30 by session, platform
| eval onboarded=if(onb>0,1,0), book_opened=if(bo>0,1,0), read_started=if(rs>0,1,0), listened_30s=if(l30>0,1,0)
| stats count as sessions, sum(onboarded) as onboarded, sum(book_opened) as book_opened, sum(read_started) as read_started, sum(listened_30s) as listened_30s by platform
```

`book_listened` is sent once a reading has played 30 s. Ninjalog gets every event the Worker wrote, without Analytics Engine's own sampling. The app's own sampling still applies: an event sent at `sampleRate` below 1 stands for `sampleInterval` (= 1 / `sampleRate`) events. While every event has `sampleRate` 1 (check with `index=dino_usage | stats count by sampleRate`), plain counts are exact; otherwise weight with `sum(sampleInterval)`. The speaker platforms (`sonos`, `alexa`) are left out of the new-user queries and widgets (they have no first launch and their `seconds` is an estimate; the Slice 288 rule).

## Where first sessions stopped

The screen and event of each first-launch session's last count:

```
index=dino_usage installAge=first platform!=sonos platform!=alexa | eventstats max(seq) as lastseq by session | where seq == lastseq | stats count by screen, name
```

## Likely the same phone

There is no id, so this is a guess, never stored: sessions with equal platform, version, country, os, form, cores and ram. Whether they were minutes apart is read from each session's timeline (`timestamp` is the batch receive time):

```
index=dino_usage platform!=sonos platform!=alexa | stats count as events by session, day, platform, version, country, os, form, cores, ram | stats count as sessions, values(session) as session_ids by day, platform, version, country, os, form, cores, ram | where sessions > 1 | sort -sessions
```

## Churn: listened 30 s or more versus not

`compare` lists the field values and numbers that differ most between the two groups (version, uiLang, screen, detail, os ...); it needs events in both groups:

```
index=dino_usage installAge=first platform!=sonos platform!=alexa | eventstats count(eval(name="book_listened")) as l by session | eval cohort=if(l>0, "listened", "not") | compare by=cohort
```

Fallbacks by reason:

```
index=dino_usage name=fallback | stats count by detail | sort -count
```

Store failures at first run (Slice 460, not run yet: written 2026-11-27 before the release). Builds before Slice 460 send only `store:store_unreachable` or `store:store_failed`; from the Slice 460 build a network failure is one of `store_timeout`, `store_no_network`, `store_dns`, `store_tls`, `store_http_5xx`, `store_http_4xx`, and the others are `store_not_associated`, `store_no_window`, `store_settle_timeout` (`store_failed` stays "other"; from this build a purchase read that fails with no named cause, on Windows, Android and iOS alike, is also `store_failed` where older builds sent `store_unreachable`). So a drop in `store_unreachable` alone proves nothing: count every `store:` detail, and split by version:

```
index=dino_usage name=fallback detail=store:* screen=first_run | stats dc(session) by platform, version, detail
```

## Play testers (versions kept apart)

Play's open and closed testing tracks post to `dino_usage` like production (only TestFlight has its own dataset). List the versions, read the testing-track ones from the Play Console tracks (1.129.0 on 2026-11-19), and add `version!=1.129.*` to the funnel to leave them out:

```
index=dino_usage platform=android | stats count as events, count(eval(installAge="first")) as first_launches by version | sort version
```

## Event flow and journeys (spans, Slice 421)

Each Dino session is also one trace in index `dino_spans` (trace id = the session, span = one count, parent = the previous `seq`). Run these in Ninjalog Search once spans arrive. A lost event cuts the chain, so paths after a gap start a new branch.

Event flow (step to step):

```
index=dino_spans | spantree | stats count by parent.name, span.name | sort -count
```

Where sessions end:

```
index=dino_spans | spantree | where child_count=0 | stats count by span.name | sort -count
```

Screens a session visited (the screen spans only, one row per session; this is the set of screens, not their order, because `values()` returns distinct sorted values; the order is the waterfall below or `span.path` from `spantree`):

```
index=dino_spans name=screen | stats values(span.name) by trace_id
```

One session as a waterfall (replace `<session>` with the 32-character session id):

```
trace_id=<session> | spantree | sort start_ns
```

## Dashboard widgets

**Forced update (Slice 430):** https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/d/forced-update (template
`docs/analytics/ninjalog-forced-update.template.json`, same script). The app itself decides it is below the remote
config's `minVersion` and shows the "please update" page, counted as `screen=update`; the button press is `name=tap
detail="update:update"`. Grouped by platform: sessions that saw the page, presses, both per version, launches by version
per platform (old versions should shrink as the new one grows) and launches per version over the last 24 hours. Builds
older than the screen counts (1.118.0, 1.123.3 seen) send no `screen`, so they show only as an old version in the launch
charts. No id links a press to the next launch on the new version.

**One dashboard per platform (Slice 429).** Ninjalog dashboards have no filters (the format has widgets and a time range
only, read 2026-11-22), so the platform filter is the dashboard you open; in a platform dashboard the first-launch, funnel, launch and device
charts are split by app version (listening seconds, where-stopped, fallbacks and the span tables are not):

- All platforms (split by platform): https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/d/new-users
- Android: https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/d/new-users-android
- iOS: https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/d/new-users-ios
- Windows: https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/d/new-users-windows

A table row's key links to Search narrowed to it (one version, say). Edit only
`docs/analytics/ninjalog-dashboard.template.json` (tokens `{{app}}`, `{{any}}`, `{{split}}`) and run
`node tools/dino/ninjalog-dashboards.mjs --upload` with `NINJALOG_MCP_TOKEN` set (from
`D:\secrets\dinoreader\secrets.env`): it rewrites `docs/analytics/ninjalog-dashboards/`, runs every widget query and
saves nothing if one fails, then replaces the four dashboards by slug; `tools/dino/ninjalog-dashboards.test.mjs` fails when they are out of date. The device and app
widgets count `app_opened` events (launches, not people: there is no id) by `version`, `os`, `form`, `arch`, `ram`,
`cores`, `uiLang` and `country`.

The "New users" dashboards use:

```
index=dino_usage installAge=first name=app_opened platform!=sonos platform!=alexa | timechart count by platform
index=dino_usage name IN (read_paused, read_stopped) seconds>=0 platform!=sonos platform!=alexa | timechart sum(seconds) as seconds
index=dino_usage name=fallback | timechart count by detail
```

plus the funnel and where-stopped queries above, and the "Event flow" and "Where sessions end" tables (the span queries above). Seconds are whole seconds of audio heard since the previous paused/stopped row of that reading; a Sonos or Alexa `book_listened` row holds an estimated track length, so those platforms stay out.

## Test-looking sessions (Slice 462)

What still counts as a real user although it may be a test: first sessions under 2 s and sessions with 10 or more cancelled downloads (the two criteria the queries apply; version strings that look unreleased are not measured, so the number does not include them). The Worker and the app cannot tell these from people, so they are read here, never dropped. Run over 7 days after a release; the share of first sessions (`installAge=first`) that look like tests should stay under 5%. Cancels are `wait_cancelled` plus `audio_pack_download` with `detail=cancelled`.

```
index=dino_usage installAge=first platform!=sonos platform!=alexa | stats dc(session) by country, version
index=dino_usage installAge=first platform!=sonos platform!=alexa | stats max(sinceLaunch) as seconds, count(eval(name="wait_cancelled" OR (name="audio_pack_download" AND detail="cancelled"))) as cancels by session, platform, version | where cancels >= 10 OR NOT seconds>=2
index=dino_usage installAge=first platform!=sonos platform!=alexa | stats max(sinceLaunch) as seconds, count(eval(name="wait_cancelled" OR (name="audio_pack_download" AND detail="cancelled"))) as cancels by session, platform, version | eval test=if(cancels>=10,1,if(seconds>=2,0,1)) | stats count as sessions, sum(test) as testlike | eval pct=100*testlike/sessions
```

One denominator for all three queries and for the 38% baseline: first sessions seen in any event (not only those that logged `app_opened`), phones and desktops only (Sonos and Alexa out). A first session with no `sinceLaunch` (null `seconds`) counts as test-like (`NOT seconds>=2`; Ninjalog has no `isnull`). The third query gives the percentage (`pct`) of those sessions that look like tests. (Parsed and run on 2026-11-27; a 7-day window needs the data of a released build.) Builds from the release that ships Slice 462 skip Android Test Harness Mode devices (Firebase Test Lab has been skipped since Slice 167); the effect is unproven until the next window's count of sessions with 10 or more cancels. A group that still appears is a gap to write up, not data to subtract by hand.
