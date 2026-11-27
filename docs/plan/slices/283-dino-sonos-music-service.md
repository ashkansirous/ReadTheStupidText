# Slice 283 — Dino: a Sonos music service for the store books (anonymous, no login)
**Product:** Dino

*Closed 2026-11-25. The user played it on their own Sonos (user, 2026-11-24 19:58 UTC): "I tested the sonos extension. it works for the free books, but not the paid ones. that fails. also, not all the books are listed. once it listed 5 book, now I see 8 titles. It seems very random. also, in the usage report, I see many hours of usage, even for the ones that I did not listen to ... lastly, the listing is very bad." So the real play is done and must never be asked for again. The bugs and the redesign the user asked for are Slices 436 (real plays, new books quickly), 437 (shelves), 438 (link through the Dino app), 439 (subscription plays paid books) and 440 (search). The link changes the service's login from Anonymous to AppLink, so the Self Test rerun, Sonos case 09396143 and the production submission move to Slice 438 item B9.*

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
artist = `author` else "Dino Book Reader", `duration` = `listenSeconds`, `audio/mp4`); a `recursive` root returns the flat track list.
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
- 2026-11-06 `s283-high.md`: applied 1 (streaming byte cap), 2 (Body action only), 3 (illegal XML characters), 4 (token hashes shown fields), 7 (constants/helpers).
  2026-11-06 9: no change, `/sonos` on the Dino host is not rewritten (`downloads.ts:315-323` `canonicalPath`). Skipped, low: 5 (slice before rendering), 6 (duplicate helpers), 8 (blanket catch maps to ServiceUnavailable).
- 2026-11-06 `s283-medium.md`: 1 no change needed, already cached by `currentEntries`, test added; 4 applied (Body action only). Skipped, low: 2 (comment before action element), 3 (CDATA/char refs in `<id>`), 5 (unsupported actions fault), 6 (count clamped to 500), 7 (primary-language title fallback), 8 (canonical host: the endpoint is registered on the canonical host).
- 2026-11-06 `s283-check1.md`: 1 applied (Body action only), 6 applied (malformed entry skipped). Skipped: 5 (log on the blanket catch; `worker/CLAUDE.md:24` forbids logging); 2, 3, 4 low (comment/CDATA before the action, `\b` match on `id-x`, empty author).
- 2026-11-06 `s283-check2.md`: all five low, skipped (repeats of the above, track-id getMetadata gives ItemNotFound, unauthenticated POST amplification bounded by the 60 s cache).
- 2026-11-19 high (`reviews/s283cold-high.md`): applied 1, 2, 3, 4, 5, 9.
- Skipped #6 (`reviews/s283cold-high.md`): "worker/stories.ts:626 (currentEntries), low. The cold path is not fixed when" - low; a first call with nothing held must build; recorded under *Built*.
- Skipped #7 (`reviews/s283cold-high.md`): "worker/stories.ts:510 (buildEntries), low. F3 starts up to 200 R2 gets" - low; bounded at 24 by medium #2.
- Skipped #8 (`reviews/s283cold-high.md`): "worker/index.ts:110, low. `caches.default` is passed on workers.dev too, and one" - low; the cold-call check runs on the custom host.
- 2026-11-19 medium (`reviews/s283cold-medium.md`): applied 1 (Sonos has its own isolate store `sharedCache`, tested), 2 (meta reads bounded at 24, F3 reworded), 4 (own list older than 5 min is not held).
- Skipped #3 (`reviews/s283cold-medium.md`): "worker/stories.ts:676 (sharedEntries), low. `held === null` goes to rebuild() before" - low; each call must still try R2, else the first Sonos call would fail for 30 s after one blip.
- Skipped #5 (`reviews/s283cold-medium.md`): "worker/index.ts:110, low. On workers.dev `caches.default` stores nothing, so F2 never" - low; Sonos calls the live service on the custom domain, where the edge cache works, and the workers.dev address is only a preview (reason reworded 2026-11-27 for the skip check; meaning unchanged).
- Skipped #6 (`reviews/s283cold-medium.md`): "worker/stories.ts:626, low. First call in a colo with no shared" - low; recorded under *Built* and *Not yet verified*.
- 2026-11-19 check (`reviews/s283cold-check1.md`): 9 low/trivial findings, none applied (doc wording, untested Cache API wiring, non-atomic compare-and-put; none changes behaviour Sonos relies on).

## Waiting on the user (their login)

Sonos Company partner account and service submission (text above); company details; uploading the ready brand images (*Brand Assets* below); Sonos ID for the sandbox; localisation
strings; then the sandbox integration at `https://dinoreader.sirous.uk/sonos`, Self Test, and a play on their own Sonos.
Unknown: whether the partner account costs anything; whether Self Test skips authentication tests for an anonymous service.

**Progress 2026-11-17/18 (user + Claude in the user's Chrome):** partner account made by the user, integration type
**Content Partner** only (Control Partner is for apps that drive speakers; none planned). The user accepted the Sonos
Content Integration Agreement. Sandbox integration created (portal applicationId `a6BPs000000qWNBMA2`) and saved:
name Dino Book Reader (corrected by hand 2026-11-18, Slice 410), availability Global (switch to a region list without mainland China before production, see
`project-dino-no-china-mainland`), home page `https://dinoreader.sirous.uk`; Integration ID `uk.sirous.dinoreader`
(fixed once submitted), label "Version 1.0", SMAPI endpoint `https://dinoreader.sirous.uk/sonos` (SOAP 1.1),
Anonymous, page size 100, poll 600 s; brand assets as SVG (service logo 400, full logo, support banner, badge);
en-US summary, description and promo from *Submission text* (description says each play adds one anonymous count);
Extended Metadata on. User's Sonos ID for the sandbox: `126815808`. Still to do: add that ID under Service Deployment
Settings, Send on Service Configuration, Self Test, a play on the user's Sonos. The portal signed the session out on
2026-11-18, so the next step waits on the user signing in again.

**Adding the service fails (2026-11-18, 15:54, reproduced by Claude on the S21):** the sandbox shows in the Sonos app
(Add content service lists "Dino Book Reader" with our logo and description). "Add to Sonos" → Continue →
"Authorising Dino Book Reader account… Adding…" → "Dino Book Reader wasn't added. Something went wrong. Please try
again." Same on the user's phone, and again after the user power-cycled the speakers.
- `wrangler tail` (POST, all paths) during both attempts: no request from Sonos reached the Worker; it caught a test
  `curl` at the same time, so the tail was live.
- S21 logcat (`com.sonos.acr2`): the add runs in a WebView from `passport-music-account-management.ws.sonos.com`
  (only its favicon and locale file return 403); the WebView is not debuggable, so its own error is not visible. At app
  start Sonos's account server answers 401 ("Account refresh requires a valid token").
- Sonos ID in the app (About your system) is 126815808, the same as in the portal. System: one IKEA table lamp, S2,
  build 86.11, Connected.
- Self Test Sonos Cloud fails with 422 `ERROR_SERVICE_NOT_CONFIGURED` "Unknown service. Service id: 463879. Account
  id: 0" (Slice 410).
- Sonos docs (test-your-service, add-authentication, use-self-test) name no other setting for Anonymous; the portal
  note says a hard reset of the players "may be required" (done, no change).
Conclusion (likely, from these sources): Sonos's cloud does not know sandbox service 463879, so its authorising step
fails before any call to us. Next: a Sonos developer support case (the user's account; draft in the session).
**Support case 09396143** opened by the user 2026-11-18 16:20 in the Sonos developer portal (Content Integration →
Technical Issue, status New), with the draft text; checked by Claude the same day. Open at:
https://developer.sonos.com/s/case/500Ps00001uDPr5IAG/sandbox-content-integration-463879-dino-book-reader-wasnt-added-on-add-to-sonos
Paid access on speakers (subscription only, linked from the Dino app) is planned in Slice 415
(docs/plan/slices/415-dino-speaker-subscription-link.md).

**Self Test fourth run (2026-11-18, 16:09-16:10):** Browse passed; SOAP Compliance 36 passed, 3 failed (the three
invalid-credential checks); MIME Type and Item Type "Not Applicable"; Authentication produced no result row. Sonos Cloud
0 of 12: besides the same 422 "Unknown service 463879", its direct `getMetadata('root')` calls to our endpoint got a
10 s timeout ("Operation timed out after 10001 milliseconds with 0 bytes received") and then HTTP 500. Five live
`getMetadata root` calls a few minutes later each answered 200 in under 1 s; SOAP Compliance in the same minute
passed its getMetadata checks. Open (not yet explained): what made our endpoint time out and answer 500 at 16:10.
A cold `/api/stories` catalogue read is the first suspect (`currentEntries`, 60 s cache); check Workers Logs for
`/sonos` at 16:10 and time a cold call before Sonos review, since a real player has a timeout too.

**Cause, likely (Workers Observability read 2026-11-19, token `CF_OBSERVABILITY_READ_TOKEN`):** Sonos's Self Test
(UA "Sonos/1.0 (SMAPI Test Framework)", AWS Ashburn) reached colo NRT. Every cold `/sonos` call there took 3.3-4.2 s
(16:10:01, :15, :19, :25, :30 UTC, all 200); cache hits 1-4 ms; 16:10:33.365 a cold call returned 500 after 2716 ms with
no exception text; 16:09:14 one call was cancelled by the client after 3467 ms. The 16 fast 500s at 16:09:15-17 and
16:10:03-06 are the invalid-credential faults (`sonos.ts:261` sends every fault as 500). From LHR a cold call is 0.55-0.7 s.
Cold path: `sonos.ts` `loadTracks` → `handleStories` → `stories.ts` `currentEntries` → `buildEntries`: a full
`bucket.list` then `meta.json` reads in sequential batches of 6 (`META_READ_BATCH`), cached per isolate for 60 s only, no
stale fallback, and `refreshEntries` swallows the error unlogged. The 10 s timeout itself is not in the logs.

**Fix (2026-11-19, the user: "why waiting for anyone?!"):**

- [x] F1. Keep the last good catalogue and serve it when a rebuild fails or runs past about 3 s (stale-while-revalidate).
- [x] F2. Keep the built catalogue in the Cache API (`caches.default`, about 5 min) with a `ctx.waitUntil` refresh, so a new isolate or colo does not rebuild on the request path.
- [x] F3. `buildEntries` reads the `meta.json` files at a fixed concurrency of 24 (`META_READ_CONCURRENCY`, a sliding window), not all at once and not in sequential batches of 6; it applies to every caller (apps, Alexa, Sonos).
- [x] F4. A failed catalogue rebuild is reported as one `console.warn` line with the error's name only (no ids or titles; the Worker has no fallback-event mechanism, and `worker/CLAUDE.md`'s Sonos paragraph now names this exception), so the next 500 shows its cause.
- [x] F5. Tests for F1-F4 in the Worker's test suite; the stories API answer is unchanged.
- Verify after the deploy: a cold `/sonos` call from a far colo stays under 1.5 s; the next Self Test's Sonos Cloud rows show no timeout.

*Built (2026-11-19):* `worker/stories.ts` `currentEntries(bucket, runtime)`: an isolate list is fresh 60 s; with a `CatalogueRuntime` (`defer` = `ctx.waitUntil`, `shared` = `caches.default`) a list up to 5 min old (own or from the Cache API) is served at once while a rebuild runs after the answer; an older one is served when the rebuild fails or runs past 3 s (`REBUILD_BUDGET_MS`), and after a failure R2 is not tried again for 30 s (`REBUILD_BACKOFF_MS`). The Cache API copy has key `https://catalogue.internal/dino/stories-entries/v1` (`SHARED_SCHEMA_VERSION`, also in the body and checked entry by entry on read; a mismatch is ignored), is held at most 1 h, and an older build never overwrites a newer one. **Staleness bound:** isolate list fresh 60 s, up to 5 min served while refreshing, the shared copy at most 1 h; a delisted or newly paid book can show on Sonos for up to that long while the apps see it within 60 s. `buildEntries` reads `meta.json` 24 at a time (`META_READ_CONCURRENCY`; `META_READ_BATCH` removed). The Sonos path keeps its own isolate list (`sharedCache`), never the apps' `cache`, and ignores its own list once it is older than 5 min (the Cache API copy still serves up to 1 h on failure). A failed rebuild logs one line `catalogue rebuild failed: <ErrorName>` (error name only); Cache API failures log `shared read/write failed: <ErrorName>`. Only `/sonos` passes the runtime (`handleSonos` 6th argument, `index.ts` `serveSonos`); `/api/stories` for the apps and Alexa run `appEntries`, origin/main's code (60 s isolate cache, 503 on failure, no stale list, no shared copy). Tests: `worker/stories-catalogue.test.mjs`. Known gap: the first Sonos call in a colo with no shared copy and no isolate list still waits for a full build (no budget); the cold-call check below passes only after a colo is warmed once. Edited `worker/stories.ts` although the Touches line lists only the Sonos files: the brief named it.

**Portal, 2026-11-19 00:48:** Sonos ID 126815808 already in Service Deployment Settings (PROD); Service Configuration
(ID 2bdd04fd-f44d-424b-84a3-02c235c84823, Sandbox, DEVELOPMENT) sent again: "ICS has successfully received your
integration". Self Test needs a Secret token even for Anonymous; "anonymous" typed as a dummy. Full Test Suite
7ce77187-abd6-4395-9104-3c2236c9b126 Failed (per-test texts in its Download results). Case 09396143: still New, no reply.

**Progress 2026-11-19:** fixes F1-F5 merged #1290, deployed 2026-11-19 01:23:39 UTC. Add to Sonos now works on the S21
(~01:00 UTC): "Setup complete", browse shows 8 languages, English 6 books, after the Service Configuration was re-sent at
00:48. Case 09396143 updated 00:52 with the test JSON attached. A Self Test rerun queued 01:32 UTC stayed "In Progress" on
Sonos's side (our Worker answered every call, logs 01:32:08-01:33:26). Left: a real play on the speaker (Living Room was
playing the user's music), the Self Test result, Sonos's answer on the auth tests, the production submission.

## Not yet verified

- Live `POST /sonos` after the Worker deploy: getMetadata root lists the languages, getMediaURI returns the track URL.
- Sonos Self Test passes.
- A real play on a Sonos speaker.
- A cold `/sonos` call from a far colo under 1.5 s (EWR first calls 1.6-2.1 s on 2026-11-19).

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

- **Service name:** Dino: Kids Books & Stories (Slice 412; "Dino Kids Stories" if the portal refuses its length: the portal's limit is not found yet, write it here when the user pastes the name)
- **Short description (service-description):** Gentle picture-book stories for children aged 3 to 9, read aloud with a voice for every character.
- **Promo (service-promo):** Bedtime stories from Dino: Kids Books & Stories, in English and more languages. Connect once through the Dino app.
- **Longer description:** Dino: Kids Books & Stories is a children's story app. This service plays its story-store books as listening tracks: connect your Sonos home once through the Dino app, choose the language your stories play in, pick a book, press play. Books are read by a narrator and character voices. No sign-up, no account, no advertising, no tracking. For each connected home Dino keeps only the story language you chose, a one-way hash of the home id and of the link's secret codes, a one-way hash of your subscription proof and the times it was connected and last used; nothing about your child, a listener or a speaker. To disconnect, remove Dino in the Sonos app. (Slice 289: each play adds one anonymous count to the project's own usage counts: book, language, length, day and country; nothing about the home, speaker or account. If the portal asks what is reported, say that.) (Slice 438, B8: wording replaced; the privacy policy says the same in "Dino: Kids Books & Stories on Sonos (optional)".)
- **Authentication method:** AppLink (Slice 438: the Sonos app opens the Dino app, where a grown-up picks the story language and presses Connect; no sign-up page, no personalisation, favourites or playlists). Until the owner switches the sandbox integration to AppLink and applies the D1 schema (docs/DEPLOYING.md, "The Sonos home link"), the Slice 283 service is anonymous.
- **SMAPI endpoint:** `https://dinoreader.sirous.uk/sonos` (SOAP 1.1, HTTPS only, POST). Polling interval: 10 minutes (`getLastUpdate` answers `pollInterval` 600).
- **Implemented calls:** getMetadata, getExtendedMetadata, getMediaMetadata, getMediaURI, getLastUpdate. Media: `audio/mp4` (AAC in .m4a) served over HTTPS with Range and HEAD support.
- **Privacy policy:** https://dinoreader.sirous.uk/privacy (section "Dino: Kids Books & Stories on Sonos (optional)").
- **Territory / availability:** worldwide (the user confirms).
- **Brand Assets (ready, 2026-11-07):** upload from `docs/design/dino-reader/sonos/`: service logo `service-logo-400.png` (other sizes, or `service-logo-400.svg`, if the portal asks), full logo `full-logo.svg` (180 x 20, white on transparent; regenerated with the new name 2026-11-18, Slice 412), badge `badge-40.svg`, support banner `support-banner.png` (800 x 200; `support-banner.svg` too). See *Design* below.
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
