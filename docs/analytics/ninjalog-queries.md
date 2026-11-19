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

## Play testers (versions kept apart)

Play's open and closed testing tracks post to `dino_usage` like production (only TestFlight has its own dataset). List the versions, read the testing-track ones from the Play Console tracks (1.129.0 on 2026-11-19), and add `version!=1.129.*` to the funnel to leave them out:

```
index=dino_usage platform=android | stats count as events, count(eval(installAge="first")) as first_launches by version | sort version
```

## Dashboard widgets

The "New users" dashboard (`docs/analytics/ninjalog-dashboard.json`, https://app.ninjalog.dev/t/ashkan-sirous/dino-book-reader/-/d/new-users) uses:

```
index=dino_usage installAge=first name=app_opened platform!=sonos platform!=alexa | timechart count by platform
index=dino_usage name IN (read_paused, read_stopped) seconds>=0 platform!=sonos platform!=alexa | timechart sum(seconds) as seconds
index=dino_usage name=fallback | timechart count by detail
```

plus the funnel and where-stopped queries above. Seconds are whole seconds of audio heard since the previous paused/stopped row of that reading; a Sonos or Alexa `book_listened` row holds an estimated track length, so those platforms stay out.
