# Dino usage in Ninjalog: session timeline, new-user funnel, where users stop (Slice 419)

**Status: written, NOT YET RUN.** The Worker forwards every production `dino_usage` count to Ninjalog (project `ashkan-sirous/dino-book-reader`, index `dino_usage`) from the first deploy after the `NINJALOG_TOKEN` secret is set; no old data is copied (owner, 2026-11-19). These queries are written from the query reference read on 2026-11-19 (`stats ... by`, `count(eval(...))`, `eventstats`, `compare by=`, `sort`, `timechart`); each must be run once against live data after the first rows arrive, and any line the engine refuses fixed here. Also unconfirmed until the first live send: which document field Ninjalog uses as the event time (the Worker sends ISO-8601 `timestamp`).

Fields are the named ones of `worker/events.ts` (`name`, `detail`, `book`, `language`, `source`, `version`, `platform`, `day`, `session`, `seq`, `sinceLaunch`, `count`, `deviceCount`, `seconds`, `uiLang`, `installAge`, `launchNo`, `screen`, `country`, `os`, `form`, `arch`, `cores`, `ram`, `sampleRate`, `sampleInterval`). A field a count did not carry is absent. There is no install or device id (Decision 140): a funnel is per launch (`session`).

Test rows are not sent (the Worker forwards only what it wrote to `dino_usage`), but `dino_usage` may still hold Play testing-track builds; the last query lists those apart.

## Session timeline

Order by `sinceLaunch`, then `seq`, never by time (counts arrive in batches):

```
index=dino_usage session=<32 hex> | sort sinceLaunch, seq | table sinceLaunch, seq, name, screen, detail, book, language
```

## New-user funnel (first launches)

One row per first-launch session with what it reached, then totals by platform and version:

```
index=dino_usage platform!=sonos platform!=alexa installAge=first
| stats count(eval(name="app_opened")) as opened, count(eval(name="intro_ended")) as onboarded, count(eval(name="book_opened")) as book_opened, count(eval(name="read_started")) as read_started, count(eval(name="book_listened")) as listened_30s by session, platform, version
| stats count as sessions, count(eval(onboarded>0)) as onboarded, count(eval(book_opened>0)) as book_opened, count(eval(read_started>0)) as read_started, count(eval(listened_30s>0)) as listened_30s by platform, version
```

`book_listened` is sent once a reading has played 30 s. Counts are per session; a session sampled out by the app (rate below 1) is absent whole, so multiply by `sampleInterval` only if a rate other than 1 is in force (check `stats avg(sampleInterval)`). Ninjalog gets every written event unsampled (Analytics Engine's sampling is not applied there), so plain counts and sums need no `sampleInterval` weighting. The speaker platforms (`sonos`, `alexa`) are left out of the new-user queries and widgets (they have no first launch and their `seconds` is an estimate; the Slice 288 rule).

## Where first sessions stopped

The last screen and last event of each first-launch session:

```
index=dino_usage installAge=first
| sort session, sinceLaunch, seq
| stats last(screen) as last_screen, last(name) as last_event, last(sinceLaunch) as seconds_in by session, platform
| stats count by last_screen, last_event
| sort -count
```

## Likely the same phone

There is no id, so this is a guess from the device fields and time, never stored: sessions on one day with equal platform, version, country, os, form, cores and ram. Within 10 minutes of each other is judged by eye from the first and last `sinceLaunch` plus the day's order; Ninjalog has no per-session start time other than the batch `timestamp`:

```
index=dino_usage
| stats min(timestamp) as first_seen, max(timestamp) as last_seen, count as events by session, day, platform, version, country, os, form, cores, ram
| eventstats count as sessions_like_this by day, platform, version, country, os, form, cores, ram
| where sessions_like_this > 1
| sort day, platform, version, country, first_seen
```

## Churn: listened 30 s or more versus none

```
index=dino_usage
| stats count(eval(name="book_listened")) as listened by session, version, uiLang, platform
| eval group=if(listened>0, "listened", "none")
| compare by=group version
```

Repeat with `uiLang` and `platform` in place of `version`. Fallbacks in the same two groups:

```
index=dino_usage name=fallback | stats count by detail | sort -count
```

## Play testers (versions kept apart)

Play's open and closed testing tracks post to `dino_usage` like production (only TestFlight has its own dataset). List the versions and read the testing-track ones from the Play Console tracks (the 1.129.x builds on 2026-11-19), then exclude them from the funnel with `version!=1.129.*`:

```
index=dino_usage platform=android | stats count, count(eval(installAge="first")) as first_launches by version | sort version
```

## Fallbacks and listening, per day (dashboard widgets)

```
index=dino_usage platform!=sonos platform!=alexa installAge=first name=app_opened | timechart span=1d count by platform
index=dino_usage name=fallback | timechart span=1d count
index=dino_usage platform!=sonos platform!=alexa name IN ("read_paused","read_stopped") seconds>=0 | timechart span=1d sum(seconds)
```

Seconds are whole seconds of audio heard since the previous paused/stopped row of that reading; a Sonos or Alexa `book_listened` row holds an estimated track length in `seconds`, so keep `name IN ("read_paused","read_stopped")` and leave speaker rows out.
