# Slice 283 — Dino: a Sonos music service for the store books (anonymous, no login)

*Code built 2026-11-06 (wave 2026-11-06, ledger wave 33), merged as PR #879 (a0687a11); user steps open (Sonos partner account, submission, sandbox, Self Test, a play on their Sonos).* Server only (Cloudflare Worker); the apps do not change.

*Claim notes (2026-11-06, unattended night):* the code is built now, as the Decisions say (build during Sonos's review weeks).
The user step (Sonos Company partner account, service submission, sandbox integration, Self Test, a play on their own
Sonos) needs the user's login and stays open under *Waiting on the user*; the submission text is written into this file.
Slice 281 is merged (#876 plus its publish-side follow-up); a book's track is listed in `/api/stories` as `audio.<lang>`
`listenSeconds` and served at `listenUrl` (`/api/stories/<id>/listen/<lang>?v=<n>`). Slice 281 removed its Alexa/Sonos
privacy paragraphs: this slice adds the Sonos one. `worker/index.ts:109-110` dispatches by path after `resolveSite`, which
needs no change for `/sonos`; `worker/check-configs.mjs` requires each `run_worker_first` entry to have its `/dino/...` twin
in both wrangler files, no entry covered by another glob (`worker/jsonc-run-first.mjs`), and both files identical apart from `routes`.

**Depends on:** Slice 281 (the listening tracks). Can be built alongside Slice 282: the Touches overlap only on the
one-line hub edits (`worker/index.ts`, `resolveSite`, both wrangler files) and the privacy files.
**Touches:** `worker/sonos.ts` (new), `worker/sonos-soap.ts` (new), `worker/sonos*.test.mjs` (new), `worker/index.ts`
(one dispatch line), `worker/downloads.ts` (`resolveSite`), `wrangler.jsonc` and `wrangler.workers-dev.jsonc`
(`run_worker_first`, with the `/dino/...` twin), `worker/CLAUDE.md`, `PRIVACY.md`, `site/dino/privacy.html`, this file.

## Why

User, 2026-11-06: Dino books on Sonos too, no login, no parent recordings. Facts checked 2026-11-06:

- A service appears in the Sonos app only through Sonos's partner programme: register a Company partner account, then
  review (about 6 weeks), validation (about 6 weeks), preview (6-8 weeks), general availability. New services are
  S2-only. A sign-up page is required unless the service allows anonymous use. https://docs.sonos.com/docs/submit-your-service
- Required calls: getMetadata, getExtendedMetadata, getMediaURI, getMediaMetadata, getLastUpdate; TLS; Sonos cloud
  addresses allowed. https://docs.sonos.com/docs/key-requirements
- Before approval: a "Content Integration (Sandbox)" in the developer portal shows the service only in the developer's own
  Sonos app (up to 10 minutes after Refresh and Send); the portal's Self Test checks it like Sonos's review would.
  https://docs.sonos.com/docs/test-your-service, https://docs.sonos.com/docs/use-self-test-to-validate-your-service
- The old customSD page on the speaker seems not to work on S2 (Sonos community posts only, not official).
- **Unknown:** whether a Sonos partner account costs anything (the pages read give no price).

## Decisions (planning defaults)

- **Anonymous** service: no sign-up page, no account, nothing stored per household.
- Browse tree: root lists the book languages that have tracks (shown in their own names), each opens the list of books
  (cover as album art, title in that language), each book is one playable track (Slice 281's file).
- getLastUpdate returns a value derived from the newest `meta.json` version, so Sonos refreshes when a book is added.
- SOAP by hand in the Worker (`sonos-soap.ts`: parse the few requests, write the few responses); no SOAP library unless
  Context7 shows a small Workers-compatible one.
- Apply to Sonos early, because the waiting time is the longest part; build during the review weeks.

## Work

- [ ] **User step first** (their login, company details): create the Sonos Company partner account and start the service
      submission, so the review clock starts. The submission text is written below (*Submission text*); open under *Waiting on the user*.
- [x] Sonos docs for the exact SMAPI shapes before any code (*Sources* below).
- [x] `sonos-soap.ts`: read the SOAP action and body, write envelopes and SOAP faults. Tests with recorded-shape requests.
- [x] `sonos.ts`: the five calls over `/api/stories` data; paging (`index`, `count`) for long lists; `getLastUpdate`.
- [ ] Sandbox integration in the portal pointed at `https://dinoreader.sirous.uk/sonos` (user adds their Sonos ID if it
      needs their login), then Self Test until it passes.
- [x] Privacy text: one paragraph on Sonos in `PRIVACY.md` and `site/dino/privacy.html`, the same wording.

## Built (2026-11-06)

`worker/sonos.ts` (+ `sonos-soap.ts`, `sonos.test.mjs`, 15 tests): `POST /sonos` and `/dino/sonos`, both twins in both
wrangler files. Ids: `root`, `lang:<code>`, `track:<code>:<storyId>`. The catalogue is the `/api/stories` list read through
`handleStories`, so only a book whose language has `listenUrl` appears. Root lists languages in their own names; a language
lists its books (title in that language from `titles`, cover as `albumArtURI` when the absolute URL fits 128 characters,
artist = `author` else "Dino Reader", `duration` = `listenSeconds`, `audio/mp4`); a `recursive` root returns the flat track list.
Paging: `index` default 0, `count` default 100, capped at 500. `getLastUpdate`: `catalog` = track count and an FNV-1a hash of
the sorted lines of every shown field (see *Changed while building*), `favorites` "0", `pollInterval` 600. `getMediaURI` returns the https
`listenUrl`. Failures: HTTP 500 and a Fault only; unknown id `Client.ItemNotFound`, no bucket `Server.ServiceUnavailable`,
anything else `Server.ServiceUnknownError`. The credentials header is ignored (anonymous). Nothing is logged.

Choices to confirm: the book is one track per language (a book offered in two languages shows once in each folder); the
catalogue is cached by `/api/stories`' 60 s per-isolate cache (a new book shows within about a minute, then Sonos at its next poll).
Not built, on purpose: getAppLink / getDeviceAuthToken (anonymous), search, presentation map, `positionInformation` (resume).
`index.ts` has three small edits (import, `serveSonos`, one dispatch line), not one line.

*Changed while building:* the catalogue now comes from `stories.ts` `currentEntries` (60 s cache) instead of a call through
`handleStories`; the catalog token hashes every shown field (title, author, cover, per-language titles, version, seconds), because a
`meta.json` text edit needs no version bump; the action comes only from the Body's action element (the SOAPAction header is ignored);
the request body is capped at 16 KiB while streaming; XML-illegal characters are dropped from output; a malformed catalogue entry is
skipped, not fatal. Nothing is logged (`worker/CLAUDE.md:24`). Tests: worker suite 202 green, typecheck and `check-configs` clean;
`check-site-eol` flags CRLF only from local autocrlf (the index is LF).

*From review* (files in `D:/ReadTheStupidText-worktrees/reviews/`, deleted later):
- `s283-high.md`: applied 1 (streaming byte cap), 2 (Body action only), 3 (illegal XML characters), 4 (token hashes shown fields), 7 (constants/helpers).
  9: no change, `/sonos` on the Dino host is not rewritten (`downloads.ts:315-323` `canonicalPath`). Skipped, low: 5 (slice before rendering), 6 (duplicate helpers), 8 (blanket catch maps to ServiceUnavailable).
- `s283-medium.md`: 1 no change needed, already cached by `currentEntries`, test added; 4 applied (Body action only). Skipped, low: 2 (comment before action element), 3 (CDATA/char refs in `<id>`), 5 (unsupported actions fault), 6 (count clamped to 500), 7 (primary-language title fallback), 8 (canonical host: the endpoint is registered on the canonical host).
- `s283-check1.md`: 1 applied (Body action only), 6 applied (malformed entry skipped). Skipped: 5 (log on the blanket catch; `worker/CLAUDE.md:24` forbids logging); 2, 3, 4 low (comment/CDATA before the action, `\b` match on `id-x`, empty author).
- `s283-check2.md`: all five low, skipped (repeats of the above, track-id getMetadata gives ItemNotFound, unauthenticated POST amplification bounded by the 60 s cache).

## Waiting on the user (their login)

Sonos Company partner account and service submission (text above); company details; uploading the ready brand images (*Brand Assets* below); Sonos ID for the sandbox; localisation
strings; then the sandbox integration at `https://dinoreader.sirous.uk/sonos`, Self Test, and a play on their own Sonos.
Unknown: whether the partner account costs anything; whether Self Test skips authentication tests for an anonymous service.

## Not yet verified

- Live `POST /sonos` after the Worker deploy: getMetadata root lists the languages, getMediaURI returns the track URL.
- Sonos Self Test passes.
- A real play on a Sonos speaker.

#### For CLAUDE.md (lifted 2026-11-07; already in worker/CLAUDE.md from the slice PR)

- `worker/sonos.ts`: the SMAPI action is read only from the Body's action element, never the SOAPAction header. The catalog token in
  getLastUpdate must hash every field shown in browse results, so a new shown field goes into the hash. Register the endpoint on the
  canonical host (`/sonos`): `canonicalRedirect` 302s a non-canonical POST and SOAP clients do not follow it. (Slice 283: docs/plan/slices/283-dino-sonos-music-service.md)

## Sources (Sonos docs, read 2026-11-06)

All under https://docs.sonos.com/docs/ (the raw page is `<slug>.md`): `soap-requests-and-responses` (namespace
`http://www.sonos.com/Services/1.1`, SOAPAction `"<ns>#<method>"`, `[method]Response`/`[method]Result`, optional
`credentials` header), `getmetadata`, `getextendedmetadata`, `getmediametadata`, `getmediauri`, `getlastupdate`
(`catalog`, `favorites`, `pollInterval` 30-3600; the WSDL makes `catalog` and `favorites` required, so both are always sent),
`smapi-object-types` (mediaCollection / mediaMetadata / trackMetadata fields, title max 64, albumArtURI max 128),
`error-handling` (every failure HTTP 500, `faultcode` `Client.*`/`Server.*`, `Client.ItemNotFound`),
`streaming-basics` (Range 206, HEAD, Content-Length: served by Slice 281's route), `supported-audio-formats` (`audio/mp4`),
`add-authentication` and `test-your-service` (anonymous = Sonos uses no authentication method), `use-self-test-to-validate-your-service`,
`localization` (i18next file with `service-description` and `service-promo`). WSDL:
https://sonos-partner-documentation.s3.amazonaws.com/ReadMe-External/content-services/soap-requests-and-responses/Sonoswsdl-1.19.6-20231024.wsdl.
Not stated in any page read: what an anonymous service does with an absent credentials header (we ignore it), and whether
Self Test skips its Authentication tests for anonymous (found out at the first Self Test).

## Submission text (for the Sonos developer portal; the user pastes it)

- **Service name:** Dino Reader
- **Short description (service-description):** Gentle picture-book stories for children aged 3 to 9, read aloud with a voice for every character.
- **Promo (service-promo):** Free bedtime stories from Dino Reader, in English and more languages. No account needed.
- **Longer description:** Dino Reader is a free children's reading app. This service plays its free story-store books as listening tracks: pick a language, pick a book, press play. Books are read by a narrator and character voices. No sign-up, no account, no advertising, no tracking; the service keeps no information about households, speakers or listeners. (Slice 289: each play adds one anonymous count to the project's own usage counts: book, language, length, day and country; nothing about the home, speaker or account. If the portal asks what is reported, say that.)
- **Authentication method:** Anonymous (no account linking, no sign-up page needed; no personalisation, favourites or playlists).
- **SMAPI endpoint:** `https://dinoreader.sirous.uk/sonos` (SOAP 1.1, HTTPS only, POST). Polling interval: 10 minutes (`getLastUpdate` answers `pollInterval` 600).
- **Implemented calls:** getMetadata, getExtendedMetadata, getMediaMetadata, getMediaURI, getLastUpdate. Media: `audio/mp4` (AAC in .m4a) served over HTTPS with Range and HEAD support.
- **Privacy policy:** https://dinoreader.sirous.uk/privacy (section "Dino Reader on Sonos (optional)").
- **Territory / availability:** worldwide (the user confirms).
- **Brand Assets (ready, 2026-11-07):** upload from `docs/design/dino-reader/sonos/`: service logo `service-logo-400.png` (other sizes, or `service-logo-400.svg`, if the portal asks), full logo `full-logo.svg` (129 x 20, white on transparent), badge `badge-40.svg`, support banner `support-banner.png` (800 x 200; `support-banner.svg` too). See *Design* below.
- **Needed from the user:** company details, the Sonos ID for the sandbox, the localization file or the two strings above.

## Design

Batch 50 mock, `docs/design/dino-reader/Dino Reader Batch 50 Sound States and Sonos.dc.html` lines 123-170 (index in
`docs/design/dino-reader/README.md`); the files are in `docs/design/dino-reader/sonos/`. Built to Sonos's *Add logos*
page (read by the designer 2026-11-07). Service logo: the full-colour Book hug on flat `#FFBDB4` (no gradient, no
transparency, no rounded corners), PNG 20/40/80/112/200/400 and SVG 40/400. Full logo and badge: one-colour white on
transparent, cut-out shapes, plain paths only (wordmark is Fredoka 600 as outlines). Support banner: white background,
colour character, ink wordmark. Nothing built differs: the Worker serves no service logo (only each book's cover as `albumArtURI`, `worker/sonos.ts`), so the images only go into the
developer portal with the submission.

## Verification

- Worker tests green.
- Self Test passes in the portal.
- On the user's own Sonos (sandbox): browse languages, open English, see covers and titles, play a book end to end, skip
  forward and back inside it.
- Sonos review, validation and preview passed; the service is live in the Sonos app.
