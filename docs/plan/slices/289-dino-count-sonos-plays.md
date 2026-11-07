# Slice 289 — Dino: count Sonos plays on the server

*Built 2026-11-07 (wave 2026-11-07, night 2026-11-07-1412 wave 5), PR #929, not merged; not yet verified live.* *Planned 2026-11-07.* Server only (Cloudflare Worker). The apps do not change.

**Depends on:** Slice 283 (the Sonos music service). Feeds Slice 287 (the dashboard).
**Touches:** `worker/events.ts` (shared row layout), `worker/appconfig.ts` (telemetry switch read), `worker/index.ts` (env plumbing), `worker/sonos.ts`, `worker/sonos-soap.ts`, `worker/speaker-usage.ts` (new: writes a speaker play to
`DINO_USAGE`), `worker/sonos.test.mjs`, `worker/speaker-usage.test.mjs` (new), `site/dino/privacy.html`, `PRIVACY.md`,
`worker/CLAUDE.md`, `docs/plan/slices/283-dino-sonos-music-service.md` (submission text, if reporting is declared), this file.

## Why

The user, 2026-11-07, wants the dashboard's platform filter to include Sonos and Alexa. This lifts "counting speaker plays"
out of the Slices 281-283 out-of-scope line. Code check (origin/main, 2026-11-07): `worker/sonos.ts` and `sonos-soap.ts`
record nothing, and the `/api/stories/<id>/listen/<lang>` route writes nothing; a speaker can fetch one track in several
Range requests, so counting fetches would over-count.

## Decisions (user, 2026-11-07)

- One listen per book play. Hours: the book's length (`listenSeconds`), stored as an estimate and shown hatched on the
  dashboard. (The first plan also allowed Sonos-reported played time; the mock, which wins, draws all Sonos hours as estimates.)
- Alexa is counted the same way inside Slice 282 when it is built.

## Design (planning defaults)

- Count in the `getMediaURI` handler (one call per track play), not on the audio route, so Range requests never count.
- `speaker-usage.ts` writes a `book_listened` row with platform `sonos`, the book id, the language, source `shelf`,
  `double1` = 1 (no sampling), `double5` = the book's `listenSeconds`, and detail (`blob9`) `estimated`. No household id,
  no IP, no account; country from Cloudflare like app rows. It never goes through `/api/events`, whose platform list stays
  android/ios/windows. Switched off when `telemetry.enabled` is false in `dinoreader/config.json`, like app counts.

#### Design (Dino Reader Stats Dashboard mock, frame 200, added 2026-11-07)

No UI here. The dashboard shows Sonos as a chip (`#2b2540`, between Windows and Alexa), hatches Sonos hours as estimates, labels the legend "Sonos (estimate)", and says "Speakers stream and don’t download" under the downloads tile, so Sonos rows carry no downloads. The mock has no Sonos rows for Own books and starts Sonos on 1 Dec 2026 (made-up data).

**Changed to match the mock (2026-11-07, the mock wins):** the mock draws every Sonos hour as an estimate ("Sonos hours
are hatched because they are estimates (Slice 289)", line 143; legend "Sonos (estimate)"). So this slice writes the
book's length (`listenSeconds`) as the seconds of each play, detail `estimated`, and does not build Sonos play reporting.
The detail blob stays so a later mock can change it.

*Changed while building:* `getMediaURI` writes one `book_listened` row (platform `sonos`, detail `estimated`, seconds = book length, `hasRecording` no, country from Cloudflare, no version, session or device) through the new `worker/speaker-usage.ts` (`recordSpeakerPlay`, platform-neutral so Slice 282 Alexa can reuse it). The row is built from `events.ts` `toDataPoint` with `NO_EXTRA_FIELDS`, so every field is typed. Counting is deferred with `ctx.waitUntil`, so the play never waits or fails on it. The `telemetry` switch in `dinoreader/config.json` is honoured. `PRIVACY.md` and `site/dino/privacy.html` now say one anonymous row per play, kept about 3 months. The `events.ts` comment, `worker/CLAUDE.md` and the Slice 288 file now say hours queries must exclude `blob7 = 'sonos'`. Tests: `npm run typecheck && node --test worker/*.test.mjs` 220/220.

*From review:* (`reviews/s289-high.md` 10/10 read, `reviews/s289-medium.md` 6/6 read, `reviews/s289-check1.md` 2/2 read)
- High #1, 3, 5, 7, 8, 9 and medium #1 applied.
- Skipped #2 (`reviews/s289-high.md`): "SMAPI calls come from Sonos's cloud servers" (country may be Sonos's cloud, not the listener's; kept, recorded under Not yet verified).
- Skipped #4 (`reviews/s289-high.md`): "assumes Sonos calls getMediaURI exactly once per play" (no dedupe without a live check; recorded under Not yet verified).
- Skipped #6 (`reviews/s289-high.md`): "`request.cf.country` may not survive" (needs a live run; no test covers the index.ts wiring).
- Skipped #10 (`reviews/s289-high.md`): "speaker rows carry an empty version" (speaker rows carry no version; reports filter the sonos platform, recorded under Not yet verified).
- Skipped #2 (`reviews/s289-medium.md`): "Every getMediaURI call counts with no dedup" (known limit, recorded under Not yet verified).
- Skipped #3 (`reviews/s289-medium.md`): "Telemetry check closure is built on every Sonos request" (low; not changed).
- Skipped #4 (`reviews/s289-medium.md`): "country read from request.cf after withPath rebuilds" (unconfirmed; recorded under Not yet verified).
- Skipped #5 (`reviews/s289-medium.md`): "Rows use UTC day and empty version" (existing queries not changed here).
- Skipped #6 (`reviews/s289-medium.md`): "Seven internal constants newly exported" (not changed; rows go through the typed `toDataPoint` path).
- check1 #1 not applied (`reviews/s289-check1.md`): "may over-count on Sonos retries/prefetch" (note only, unconfirmed whether Sonos calls once per play).
- check1 #2 not applied (`reviews/s289-check1.md`): "worst case empty country" (note only; recorded under Not yet verified).

## Work

- [x] 1. `speaker-usage.ts` + tests; `getMediaURI` writes one row per call with the book's length as estimated seconds.
- [x] 2. Privacy page and `PRIVACY.md`: one line on counting speaker plays (book, language, length; nothing about the home).
- [x] 3. `worker/CLAUDE.md`: Sonos plays are counted at `getMediaURI`, never on the audio route.

## Verification

- `npm ci && npm run typecheck`, `node --test worker/sonos.test.mjs worker/speaker-usage.test.mjs`: one row per
  `getMediaURI`, none for Range fetches, none with telemetry off.
- Live (once the user's Sonos sandbox from Slice 283 works): play one book; an `analytics-query` count shows one `sonos`
  `book_listened` row for it.

## Not yet verified

- Whether Sonos's `getMediaURI` request comes from the player or from Sonos's cloud: that decides what the row's country
  means (the listener's, or Sonos's server country). Until known, the per-country view of Sonos rows may be wrong.
- That Sonos calls `getMediaURI` once per play: queue prefetch, resume after pause, retry or reconnect may call it again and
  over-count plays and hours (3 resumes = 3 x the book's length).
- Speaker rows carry no app version, so test or sandbox plays cannot be filtered out by version: reports must filter
  `blob7 = 'sonos'` plays made during testing, and Slice 287 must handle platform `sonos` explicitly.
- Country may be empty if the Request rebuilt by `withPath` in `worker/index.ts` drops `cf` (review check1 #2).
- A live Sonos play counts exactly one row, once the user's Sonos sandbox (Slice 283) works.

#### For CLAUDE.md

- Worker: speaker plays (Sonos, later Alexa) are counted through `worker/speaker-usage.ts` (`recordSpeakerPlay`), never `/api/events`; hours queries exclude `blob7 = 'sonos'`. (Slice 289: docs/plan/slices/289-dino-count-sonos-plays.md)
