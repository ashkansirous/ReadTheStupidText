# Slice 436 — Dino: Sonos counts only real plays and shows new books quickly
**Product:** Dino

*Plan-cleanup check 2026-11-25 (origin/main 1a8bf725): keep open, nothing built. Re-read live: `worker/sonos.ts:220` still calls `recordSpeakerPlay` in `getMediaUri`; no `sonos-reporting.ts`, no `timePlayed` anywhere in `worker/`; `SHARED_HOLD_SECONDS` is still 1 h in `worker/stories.ts`. Slice 283 is closed (user played it 2026-11-24), so the Depends-on line is satisfied. Parts B and D live in Slices 437-440, all still planned. No `user-says` hit for the Reporting Endpoint step, so A4 is still the owner's. A4 owner step: the portal integration id is `a6BPs000000qWNBMA2` (Slice 283 *Progress 2026-11-17/18*); the exact page URL is not in the repo, find it before handing the step over. The two *Not yet verified* lines stay in unverified.md and do not hold the slice; whether an anonymous service gets reports is known only after A4 is done.*

*Planned 2026-11-24 (user, 2026-11-24: "open a slice and write it there. dont do it yet"; later set to planned).* *In progress (wave 2026-11-27), draft PR.*

**Depends on:** Slice 283 (Sonos service), Slice 289 (Sonos play counting). **Replaced in part by:** Slice 437 (parts B
and D: the owner's Claude Design canvas `docs/design/sonos/`, 2026-11-24, lists paid books as "In the Dino app" instead of
hiding them and gives the browse tree), Slices 438-440 (link, subscription, search).

**Touches:** `worker/sonos.ts`, `worker/sonos-soap.ts`, `worker/sonos.test.mjs`, `worker/speaker-usage.ts`,
`worker/speaker-usage.test.mjs`, `worker/sonos-reporting.ts` (new), `worker/sonos-reporting.test.mjs` (new),
`worker/index.ts`, `worker/stories.ts`, `worker/stories-catalogue.test.mjs`, `worker/stats-rollup.ts`,
`worker/stats.test.mjs`, `worker/CLAUDE.md`, `PRIVACY.md`, `site/dino/privacy.html`, this file.

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
- [ ] A1. `getMediaURI` stops writing a usage row (`worker/sonos.ts`); tests prove no row on getMediaURI.
- [ ] A2. New `worker/sonos-reporting.ts`: `POST /api/sonos/timePlayed` (path final at build, under the Worker's Sonos
      routes in `worker/index.ts`) reads each report of type `final`, maps the item id to book and language, and writes one
      `book_listened` row through `recordSpeakerPlay` when `duration` is 30 s or more, seconds = min(duration, book length).
      `update` reports are ignored. Payload shape checked against https://docs.sonos.com/docs/add-reporting and its
      `timePlayed` reference before coding (`library-docs`/`web-research`), validated, unknown items rejected with 400.
      Telemetry switch honoured. No household id, IP or account stored (same as Slice 289).
- [ ] A3. Sonos fault visibility: an unplayable request (unknown item, paid book reached by id) writes a fallback event
      (`FallbackArea.` pattern of Slice 273 on the Worker side, or the Worker's existing fallback row), so a failure shows
      up in `dino_usage`.
- [ ] A4. Owner step: fill the **Reporting Endpoint** field of the Dino sandbox integration (463879) with the A2 URL in the
      Sonos Dev Portal. The direct page id of that integration is not in the repo yet; find it (the case page
      https://developer.sonos.com/s/case/500Ps00001uDPr5IAG/sandbox-content-integration-463879-dino-book-reader-wasnt-added-on-add-to-sonos
      links the integration) and write the deep link here before handing the step over.
- [ ] A5. `worker/stats-rollup.ts`: leave out `sonos` `book_listened` rows whose detail is `estimated` and that were
      written before A1 shipped (cutover timestamp constant), so the dashboard drops the 4.4 h test hours; test in
      `worker/stats.test.mjs`. Re-run the rollup for the affected days.
- [ ] A6. `PRIVACY.md` and `site/dino/privacy.html`: speaker plays are counted from the speaker's play report (book,
      language, seconds played; nothing about the home). `worker/CLAUDE.md`: Sonos plays are counted at `/timePlayed`,
      never at `getMediaURI` or the audio route.

### B. Moved to Slice 437
Paid books stay listed with "In the Dino app" and don't play (owner's mock, 2026-11-24); built in Slice 437.

### C. New books show up quickly
- [ ] C1. Cut the Sonos list's colo Cache API lifetime (`worker/stories.ts:69-71`) from about 1 hour to 5 minutes, and keep
      the stale-while-rebuild path; test in `worker/stories-catalogue.test.mjs`.
- [ ] C2. Return the same order every call (Slice 437's shelf order once it ships) and a stable `total`, so a
      reload never shows a different count for the same catalogue.

### D. Moved to Slices 437-440
The owner made the mock (`docs/design/sonos/`, 2026-11-24); the browse tree is Slice 437, the link and story language
Slice 438, the subscription on Sonos Slice 439, search Slice 440. The brief written here on 2026-11-24 is in git history.

## Verification

- `npm ci && npm run typecheck`, `node --test worker/*.test.mjs`: no row on getMediaURI; one row per `final` report of
  30 s or more with the capped seconds; none for `update`, short or unknown reports; Sonos list cache 5 minutes; rollup drops pre-cutover estimated Sonos rows.
- Live, after the owner's A4 step: browse the service twice in the Sonos app without playing; an `analytics-query`
  shows no new `sonos` row. Play one free book for 2 minutes and stop; one row with about 120 s appears after Sonos's
  report. The dashboard's Sonos hours for 18-24 Nov drop to 0.
- Live: a new listening track published appears within about 5 minutes.

## Not yet verified

- [session] Whether Sonos sends `/timePlayed` reports to an anonymous (no-login) service: the docs are silent. If none
  arrive, Sonos plays show 0 rather than invented hours; that result goes back to the owner as one question.
- [session] Whether the 5-then-8 count came from the hour-long cache (not reproduced 2026-11-24).
