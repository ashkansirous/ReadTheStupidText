# Slice 289 — Dino: count Sonos plays on the server

*Planned 2026-11-07.* Server only (Cloudflare Worker). The apps do not change.

**Depends on:** Slice 283 (the Sonos music service). Feeds Slice 287 (the dashboard).
**Touches:** `worker/sonos.ts`, `worker/sonos-soap.ts`, `worker/speaker-usage.ts` (new: writes a speaker play to
`DINO_USAGE`), `worker/sonos.test.mjs`, `worker/speaker-usage.test.mjs` (new), `site/dino/privacy.html`, `PRIVACY.md`,
`worker/CLAUDE.md`, `docs/plan/slices/283-dino-sonos-music-service.md` (submission text, if reporting is declared), this file.

## Why

The user, 2026-11-07, wants the dashboard's platform filter to include Sonos and Alexa. This lifts "counting speaker plays"
out of the Slices 281-283 out-of-scope line. Code check (origin/main, 2026-11-07): `worker/sonos.ts` and `sonos-soap.ts`
record nothing, and the `/api/stories/<id>/listen/<lang>` route writes nothing; a speaker can fetch one track in several
Range requests, so counting fetches would over-count.

## Decisions (user, 2026-11-07)

- One listen per book play. Hours: the played time if Sonos reports it to our service, else the book's length
  (`listenSeconds`), stored as an estimate and shown hatched on the dashboard.
- Alexa is counted the same way inside Slice 282 when it is built.

## Design (planning defaults)

- Count in the `getMediaURI` handler (one call per track play), not on the audio route, so Range requests never count.
- `speaker-usage.ts` writes a `book_listened` row with platform `sonos`, the book id, the language, source `shelf`,
  `double1` = 1 (no sampling), `double5` = seconds, and detail (`blob9`) `estimated` or `reported`. No household id, no IP, no
  account; country from Cloudflare like app rows. It never goes through `/api/events`, whose platform list stays
  android/ios/windows. Switched off when `telemetry.enabled` is false in `dinoreader/config.json`, like app counts.
- **First item: check Sonos's docs** (`web-research`) for play reporting (e.g. `reportPlaySeconds` / `reportPlayStatus`
  and the manifest capability it needs). If Sonos sends played time: implement it and write a seconds row per report
  (not estimated). If not, or it needs a review we don't want now: write `listenSeconds` as an estimate at `getMediaURI`.
  Write the answer into this file before building.

#### Design (Dino Reader Stats Dashboard mock, frame 200, added 2026-11-07)

No UI here. The dashboard shows Sonos as a chip (`#2b2540`, between Windows and Alexa), hatches Sonos hours as estimates, labels the legend "Sonos (estimate)", and says "Speakers stream and don’t download" under the downloads tile, so Sonos rows carry no downloads. The mock has no Sonos rows for Own books and starts Sonos on 1 Dec 2026 (made-up data).

**Open point: reported versus estimated.** The mock treats every Sonos hour as an estimate; this slice may store `reported` seconds. **Default:** the dashboard hatches only the `estimated` rows (Slice 287 open point), so keep the `estimated` / `reported` detail in `blob9` as planned.

## Work

- [ ] 1. `web-research`: Sonos play reporting; answer written here.
- [ ] 2. `speaker-usage.ts` + tests; `getMediaURI` writes one row per call.
- [ ] 3. Play reporting handlers if item 1 says yes (tests with recorded SOAP bodies); else the estimate path.
- [ ] 4. Privacy page and `PRIVACY.md`: one line on counting speaker plays (book, language, length; nothing about the home).
- [ ] 5. `worker/CLAUDE.md`: Sonos plays are counted at `getMediaURI`, never on the audio route.

## Verification

- `npm ci && npm run typecheck`, `node --test worker/sonos.test.mjs worker/speaker-usage.test.mjs`: one row per
  `getMediaURI`, none for Range fetches, none with telemetry off.
- Live (once the user's Sonos sandbox from Slice 283 works): play one book; an `analytics-query` count shows one `sonos`
  `book_listened` row for it.
