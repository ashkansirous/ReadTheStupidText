# Slice 436 — Dino: Sonos counts only real plays and shows new books quickly
**Product:** Dino

*Plan-cleanup check 2026-11-25 (origin/main 1a8bf725): keep open, nothing built. Re-read live: `worker/sonos.ts:220` still calls `recordSpeakerPlay` in `getMediaUri`; no `sonos-reporting.ts`, no `timePlayed` anywhere in `worker/`; `SHARED_HOLD_SECONDS` is still 1 h in `worker/stories.ts`. Slice 283 is closed (user played it 2026-11-24), so the Depends-on line is satisfied. Parts B and D live in Slices 437-440, all still planned. No `user-says` hit for the Reporting Endpoint step, so A4 is still the owner's. A4 owner step: the portal integration id is `a6BPs000000qWNBMA2` (Slice 283 *Progress 2026-11-17/18*); the exact page URL is not in the repo, find it before handing the step over. The two *Not yet verified* lines stay in unverified.md and do not hold the slice; whether an anonymous service gets reports is known only after A4 is done.*

*Planned 2026-11-24 (user, 2026-11-24: "open a slice and write it there. dont do it yet"; later set to planned).* *Built 2026-11-27 (wave 2026-11-27, night 2026-11-27-0154 wave 1), merged as PR #1391 (f58a108f); worker only, no device run. A4 (Sonos portal field) and A7 (secret path token) are open owner steps, and the live checks are under "Not yet verified". The code was first written by a cloud session (run cse_012UYzYS99g3gGchFRn43e4t, commit 477e2704), then fixed locally.*

*Changed while building:* getMediaURI writes no play row (A1); `POST /sonos/v2.0/timePlayed` and `/dino/sonos/v2.0/timePlayed` count one `book_listened` row (detail `reported`) per final report with `durationPlayedMillis` of 30 s or more, capped at the book length, at most 5 per request, `reportId` de-duplicated about 2 days via the Cache API after a successful write (A2); an unknown item on getMediaURI writes one fallback row (A3); the rollup replaces a stale day's `sonos` rows from the Analytics Engine (A5, *Design*); privacy texts (A6); the Sonos list is fresh for 5 minutes and held 30 minutes as the fallback (C1). Detail is under *Build notes*. Tests: `npm run typecheck` exit 0, `node --test worker/*.test.mjs` 551+ pass and 0 fail, `node worker/check-configs.mjs` exit 0.

**Depends on:** Slice 283 (Sonos service), Slice 289 (Sonos play counting). **Replaced in part by:** Slice 437 (parts B
and D: the owner's Claude Design canvas `docs/design/sonos/`, 2026-11-24, lists paid books as "In the Dino app" instead of
hiding them and gives the browse tree), Slices 438-440 (link, subscription, search).

**Touches:** `worker/sonos.ts`, `worker/sonos-soap.ts`, `worker/sonos.test.mjs`, `worker/speaker-usage.ts`,
`worker/speaker-usage.test.mjs`, `worker/sonos-reporting.ts` (new), `worker/sonos-reporting.test.mjs` (new),
`worker/index.ts`, `worker/stories.ts`, `worker/stories-catalogue.test.mjs`, `worker/stats-rollup.ts`,
`worker/stats.test.mjs`, `worker/CLAUDE.md`, `PRIVACY.md`, `site/dino/privacy.html`, `wrangler.jsonc`,
`wrangler.workers-dev.jsonc`, this file.

## Why

The user tested the Sonos service on 2026-11-24: "it works for the free books, but not the paid ones. that fails. also,
not all the books are listed. once it listed 5 book, now I see 8 titles ... in the usage report, I see many hours of
usage, even for the ones that I did not listen to ... lastly, the listing is very bad. I have to select the language
every time, and then I see the apps. there is no filter, no sorting, no nothing."

What the code, live calls and analytics showed (2026-11-24, origin/main):

1. **Paid books** (confirmed by live call): the browse lists all 8 books with a listening track; `getMediaURI`
   (`worker/sonos.ts:218-223`) hands out the listen URL for any of them, and the audio route answers 402 for the 6 paid
   ones (`worker/stories.ts:858-860`, `paidDenial`). Only the 2 free books play. Slice 415's locked-book design is not built.
2. **5 then 8 titles** (not reproduced; 9 live `getMetadata` calls in en, de, fa all gave 8 of 8). Only 8 of 16 stories
   have a listening track. The Sonos list is held up to about 1 hour in the colo's Cache API (`worker/stories.ts:69-71`,
   689-704), so a list read before new tracks were published can be served for up to an hour, per colo. Likely cause,
   from code only.
3. **Hours never listened** (confirmed in `dino_usage`): every `getMediaURI` call writes one `book_listened` row with the
   whole book's length as estimated seconds (`worker/sonos.ts:220`, `worker/speaker-usage.ts:73-86`). 18-24 Nov: 38 raw
   rows (43 weighted), about 15 books, mostly in bursts no one could listen to (8 books in 49 s on 21 Nov, 3 in one second
   on 22 Nov). The dashboard's 4.4 h is the sum of those books' lengths (`worker/stats-rollup.ts:80,87`,
   `site/dino/stats.html:682`). Slice 289 had listed exactly this as unverified ("That Sonos calls `getMediaURI` once per play").
4. **Browsing**: the root is 15 language containers, each a flat list of books in catalogue order
   (`worker/sonos.ts:118-124`, 177-191); no sort, search or grouping; the `Accept-Language` header is ignored
   (`worker/sonos-soap.ts:107-119`).

Sonos documentation read 2026-11-24 (web-research):
- getMediaURI is sent "when playing content, such as after a long pause" and "most often when the player plays the next
  song in the queue" (https://docs.sonos.com/docs/getmediauri.md). It is not a play count.
- Playback reporting: "Players send final playback reports by sending a POST /timePlayed request to your reporting
  endpoint"; `duration` is "the total duration of playback, in milliseconds"; "By default, players send one report per
  track, at track completion." Turned on by filling the **Reporting Endpoint** field of the integration in the Sonos
  Dev Portal (https://docs.sonos.com/docs/add-reporting). The old SMAPI calls (`reportPlaySeconds`, `setPlayedSeconds`,
  `reportPlayStatus`) are deprecated. The pages are silent on whether an anonymous (no-login) service gets reports.
- "Sonos apps send an Accept-Language HTTP header in their SMAPI requests" ... "Sonos players do not send
  Accept-Language headers" (https://docs.sonos.com/docs/soap-requests-and-responses).
- Search: categories via the presentation map (mobile) and `getMetadata` on the search container (desktop)
  (https://docs.sonos.com/docs/search). Sort order and a default first container: docs silent.

## Decisions

- User, 2026-11-24: write this slice for parts A-C; do not build it yet.
- User, 2026-11-24, on the browsing layout: "lets claude desing do it". Part D's browse tree comes from a Claude Design
  mock, which wins over everything below once it exists (`~/.claude/CLAUDE.md`, *Claude Design*).
- Slice 289's user rule (2026-11-07) stays: "One listen per book play" and Sonos hours drawn hatched as estimates on the
  dashboard. This slice changes what counts as a play (a real playback report, not an audio-address request).
- Decided by this session: each counted play stores the reported played seconds, capped at the book's length, in place
  of the whole book's length; the dashboard keeps the Sonos hatch and "Sonos (estimate)" label (the Slice 287 mock)
  until a mock says otherwise. A play under 30 s is not a listen, the same bar as the apps ("Played for 30 s or more").
- ~~Decided by this session: paid books are left out of the Sonos browse until Slice 415 is built.~~ Replaced by the
  owner's mock (2026-11-24): paid books stay listed as "In the Dino app" (Slice 437).
- Decided by this session: the 18-24 Nov Sonos rows (and every earlier `sonos` `book_listened` row with detail
  `estimated`) were the owner's tests; Analytics Engine rows cannot be deleted, so the rollup leaves them out.

## Work

### A. Count only real plays
- [x] A1. `getMediaURI` stops writing a usage row (`worker/sonos.ts`); tests prove no row on getMediaURI.
- [x] A2. New `worker/sonos-reporting.ts`: `POST /sonos/v2.0/timePlayed` (path final at build, under the Worker's Sonos
      routes in `worker/index.ts`) reads each report of type `final`, maps the item id to book and language, and writes one
      `book_listened` row through `recordSpeakerPlay` when `duration` is 30 s or more, seconds = min(duration, book length).
      `update` reports are ignored. Payload shape checked against https://docs.sonos.com/docs/add-reporting and its
      `timePlayed` reference before coding (`library-docs`/`web-research`), validated, unknown items rejected with 400.
      Telemetry switch honoured. No household id, IP or account stored (same as Slice 289).
- [x] A3. Sonos fault visibility: an unplayable request (unknown item, paid book reached by id) writes a fallback event
      (`FallbackArea.` pattern of Slice 273 on the Worker side, or the Worker's existing fallback row), so a failure shows
      up in `dino_usage`.
- [x] A4. (Done through Slice 467: the address is now `https://dino.elinsoft.co.uk/sonos/v2`, set in the Sonos portal 2026-11-28 in the owner's signed-in Chrome and read back after a reload: SMAPI Endpoint `https://dino.elinsoft.co.uk/sonos` (was `https://dinoreader.sirous.uk/sonos`; both hosts answer the same SOAP fault to a bad getAppLink), Reporting Endpoint `https://dino.elinsoft.co.uk/sonos/v2` (version 2.3; was empty). Brand Assets, Integration Capabilities, Image Replacement and General Information hold no Dino URL; Service Configuration is a request tester, not a stored value.) Owner step: fill the **Reporting Endpoint** field of the Dino sandbox integration (463879) with exactly
      `https://dinoreader.sirous.uk/sonos/v2.0` (Sonos requires the version number in the URL and players POST `/timePlayed` under it, so
      the Worker serves `https://dinoreader.sirous.uk/sonos/v2.0/timePlayed`; host read from the `routes` custom domain `dinoreader.sirous.uk`
      in `wrangler.jsonc` and `worker/CLAUDE.md` line 12; version rule from `reviews/pr-1391-add-reporting-raw.md` lines 31-34) in the
      Sonos Dev Portal. The direct page id of that integration is not in the repo yet; find it (the case page
      https://developer.sonos.com/s/case/500Ps00001uDPr5IAG/sandbox-content-integration-463879-dino-book-reader-wasnt-added-on-add-to-sonos
      links the integration) and write the deep link here before handing the step over.
- [x] A5. `worker/stats-rollup.ts`: leave out `sonos` `book_listened` rows whose detail is `estimated` and that were
      written before A1 shipped (cutover timestamp constant), so the dashboard drops the 4.4 h test hours; test in
      `worker/stats.test.mjs`. Re-run the rollup for the affected days.
- [x] A6. `PRIVACY.md` and `site/dino/privacy.html`: speaker plays are counted from the speaker's play report (book,
      language, seconds played; nothing about the home). `worker/CLAUDE.md`: Sonos plays are counted at `/timePlayed`,
      never at `getMediaURI` or the audio route.

- [ ] A7. Follow-up (owner step, nothing can be set tonight): authenticate `/timePlayed` with a secret path token set as a
      Worker secret (the token goes in the Reporting Endpoint URL). Until then the endpoint is anonymous, so a request writes
      at most `MAX_COUNTED_PER_REQUEST` rows and `/timePlayed` never writes a fallback row.

### B. Moved to Slice 437
Paid books stay listed with "In the Dino app" and don't play (owner's mock, 2026-11-24); built in Slice 437.

### C. New books show up quickly
- [x] C1. Cut the Sonos list's colo Cache API lifetime (`worker/stories.ts:69-71`) from about 1 hour to 5 minutes, and keep
      the stale-while-rebuild path; test in `worker/stories-catalogue.test.mjs`.
- [x] C2. Return the same order every call (Slice 437's shelf order once it ships) and a stable `total`, so a
      reload never shows a different count for the same catalogue.

### D. Moved to Slices 437-440
The owner made the mock (`docs/design/sonos/`, 2026-11-24); the browse tree is Slice 437, the link and story language
Slice 438, the subscription on Sonos Slice 439, search Slice 440. The brief written here on 2026-11-24 is in git history.

## Verification

- `npm ci && npm run typecheck`, `node --test worker/*.test.mjs`: no row on getMediaURI; one row per `final` report of
  30 s or more with the capped seconds; none for `update`, short or unknown reports; Sonos list rebuilt once it is over 5 minutes old (held 30 min as the fallback); rollup drops estimated Sonos rows.
- Live, after the owner's A4 step: browse the service twice in the Sonos app without playing; an `analytics-query`
  shows no new `sonos` row. Play one free book for 2 minutes and stop; one row with about 120 s appears after Sonos's
  report. The dashboard's Sonos hours for 18-24 Nov drop to 0.
- Live: a new listening track published appears within about 5 minutes.

## Build notes (2026-11-27)

- A2 payload (fix pass 1, from the saved pages https://docs.sonos.com/docs/add-reporting and https://docs.sonos.com/reference/timeplayed): the body is `{ "items": [...] }`; each item has `type` (`final`/`update`), the item id (`objectId` in the SMAPI examples, `id` in the OpenAPI's cloud queue item; also `reportId`, `mediaUrl`, `positionMillis`) and `durationPlayedMillis` ("the total duration of playback ... cumulative"). Only `durationPlayedMillis` is counted; `durationMillis`, `duration` and `positionMillis` are never read (a track's length would count as a full play). A final report without an id or `durationPlayedMillis`, an unknown item or a non-object entry is skipped (the rest of the batch counts, the answer is 200); a body that is not an `items` list is 400. The parser takes no bare array and no `reports` key.
- A2 anonymous endpoint: no secret can be set in this pass, so a request counts at most `MAX_COUNTED_PER_REQUEST` (5) rows and `/timePlayed` writes no fallback row; getMediaURI keeps A3's fault row. The secret path token is A7.
- A5 (fix pass 1): the filter has no date bound (no code writes a `sonos` `estimated` row after this slice), so the deploy day does not matter. The rollup also leaves the `sonos` platform out of its lower-total guard and treats a day that still stores a `sonos` `seconds_estimated` row as not done, so the next hourly runs (10 old days a run) replace the stored test hours by themselves; no manual re-run is needed.
- A6: `site/dino/privacy.html` has no Sonos paragraph, so only `PRIVACY.md` and `worker/CLAUDE.md` changed; `PRIVACY.md` now also names the failure row (fix pass 1).
- `wrangler.jsonc` and `wrangler.workers-dev.jsonc` now list `/sonos/v2.0/timePlayed` and `/dino/sonos/v2.0/timePlayed` in `run_worker_first` (fix pass 1; a test in `worker/sonos-reporting.test.mjs` reads both configs), or Sonos's report would never reach the Worker.
- C1 (fix pass 1): a held list older than 5 min (`SHARED_FRESH_MS`) is rebuilt before the answer, which bounds a new track's delay to about 5 min; the Cache API hold (`SHARED_HOLD_SECONDS`) is 30 min so an older list still serves when a rebuild fails or runs past 3 s. C2: order is unchanged (list order), Slice 437's shelf order comes later; a test checks two reads match.

## Design (wave 2026-11-27)

The rollup's stale Sonos days (third round, one design instead of patches). A day is stale while D1 holds any `sonos` `seconds_estimated` row for it (only the old getMediaURI code wrote those). For a stale day the `sonos` platform's rows are replaced as a whole: in one D1 batch of 3 statements, every `sonos` row of that day (all metrics) is deleted, the `sonos` rows the Analytics Engine re-roll gives with the `NOT_SONOS_ESTIMATE` filter are written (so `reported` plays come back from AE, not lost), and the day is marked. The lower-total guard does not apply to the `sonos` platform on a stale day; it still applies to every other platform, and to `sonos` on non-stale days. When the guard keeps the other platforms, only the `sonos` rows are replaced (`replaceSonosOnly`); otherwise the whole day is replaced as always. So a stale day is never stale after one run, whatever the other platforms' guard decides. The earlier partial delete (`seconds_estimated` only) is removed. Subrequests per run are unchanged at 44 at most (4 AE + 1 done-days + 1 totals + 1 stale-days + 12 days x 3 + 1 meta), under the Worker's 50. Tests in `worker/stats.test.mjs`: stale day with old listens and seconds rows plus a reported row in AE; a non-stale day with a lower AE sonos total keeps D1; another platform's lower total on a stale day keeps that platform's rows.

## Not yet verified

- [owner] Whether Sonos sends `/timePlayed` reports to an anonymous (no-login) service: the docs are silent. After A4, a real play should show a `reported` row (`analytics-query`); if none arrive, Sonos plays show 0 rather than invented hours, and that result goes back to the owner as one question.
- [cloud] After the release, a new store book shows in the Sonos browse within 5 minutes.
- [session] Whether the 5-then-8 count came from the hour-long cache (not reproduced 2026-11-24).

#### From review

Six findings files, each read to its last finding: pr-1391-high (9 findings, all fixed), pr-1391-medium (4 fixed, 1 left out), pr-1391-check1-medium (3 fixed), pr-1391-check2-medium (2 fixed, 2 left out; finding 4 has no Outcome line, its best-effort wording is the next bullet), pr-1391-check3-medium (3 left out; the coordinator's C1 fixed: reports served at the `/sonos/v2.0` paths), pr-1391-check4-medium (one nit, follow-up).

- Follow-up (`reviews/pr-1391-check4-medium.md`, 1 nit, no Outcome block): `replaceSonosOnly` in `worker/stats-rollup.ts` replaces a stale day's `sonos` rows without the lower-total guard, so a partial Analytics Engine answer could drop real reported rows; real reported rows on stale days exist only on the deploy day, so it is left as a follow-up.

- Skipped #2 (`reviews/pr-1391-medium.md`): "After MAX_COUNTED_PER_REQUEST (5) counted reports the loop breaks and answers 200, so Sonos never resends: real plays are lost for good." - the final-report example in `reviews/pr-1391-add-reporting-raw.md` carries one item with one reportId, so a cap of 5 counted reports per request drops no real play; the cap answers high #5's inflation finding.
- Report de-duplication (medium #3) uses the Cache API, which is per data centre: a retry that lands in another data centre can count again.
- Skipped #2 (`reviews/pr-1391-check2-medium.md`): "Stale-day detection keys only on a Sonos `seconds_estimated` row; an old estimated play with seconds -1 leaves only a `listens` row, so the day is not stale and the invented listen stays." - after the second check pass only data-loss-class findings reopen code. Follow-up: an old estimated play with no seconds row stays counted; detect such days too.
- Skipped #3 (`reviews/pr-1391-check2-medium.md`): "Dedup keyed on reportId alone; a reportId reused across items would drop later plays silently." - Sonos makes reportId unique per report (saved spec `reviews/pr-1391-timeplayed-raw.md`). Follow-up: key on reportId plus item id and log skips if Sonos ever reuses one.
- Skipped #1 (`reviews/pr-1391-check3-medium.md`): "Dedupe memory is written only in deferred `countAll`, after the row write; a retry arriving while the first write runs finds no id and writes a second `book_listened` row." - a retry faster than the row write is rare and only double-counts one play. Follow-up: remember a pending marker before the write.
- Skipped #2 (`reviews/pr-1391-check3-medium.md`): "`countedSeconds` caps at `track.seconds` after the 30 s bar; a book with `listenSeconds` under 30, 0 or negative writes a row with that length (0 or negative) which the Listens metric counts." - every store book with a track has a positive listenSeconds. Follow-up: guard tracks whose length is under the bar or not positive.
- Skipped #3 (`reviews/pr-1391-check3-medium.md`): "Cache API memory does nothing on the workers.dev twin (`caches.default.put` not honoured), so retries there are counted again silently." - the workers.dev twin is not a Sonos endpoint. Follow-up: note it in worker/CLAUDE.md if the twin ever serves Sonos.