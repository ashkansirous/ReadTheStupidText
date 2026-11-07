# Slice 289 — Dino: count Sonos plays on the server

*In progress (wave 2026-11-07), draft PR.* *Planned 2026-11-07.* Server only (Cloudflare Worker). The apps do not change.

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

## Work

- [ ] 1. `speaker-usage.ts` + tests; `getMediaURI` writes one row per call with the book's length as estimated seconds.
- [ ] 2. Privacy page and `PRIVACY.md`: one line on counting speaker plays (book, language, length; nothing about the home).
- [ ] 3. `worker/CLAUDE.md`: Sonos plays are counted at `getMediaURI`, never on the audio route.

## Verification

- `npm ci && npm run typecheck`, `node --test worker/sonos.test.mjs worker/speaker-usage.test.mjs`: one row per
  `getMediaURI`, none for Range fetches, none with telemetry off.
- Live (once the user's Sonos sandbox from Slice 283 works): play one book; an `analytics-query` count shows one `sonos`
  `book_listened` row for it.
