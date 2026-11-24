# Slice 436 — Dino: Sonos counts only real plays, hides books it can't play, shows new books quickly, and browses by a Claude Design mock
**Product:** Dino

*Planned 2026-11-24. Not built (user, 2026-11-24: "open a slice and write it there. dont do it yet").*

**Depends on:** Slice 283 (Sonos service), Slice 289 (Sonos play counting). **Replaced in part by:** Slice 415 (paid books
through the subscription), which brings the hidden paid books back as locked books.

**Touches:** `worker/sonos.ts`, `worker/sonos-soap.ts`, `worker/sonos.test.mjs`, `worker/speaker-usage.ts`,
`worker/speaker-usage.test.mjs`, `worker/sonos-reporting.ts` (new), `worker/sonos-reporting.test.mjs` (new),
`worker/index.ts`, `worker/stories.ts`, `worker/stories-catalogue.test.mjs`, `worker/stats-rollup.ts`,
`worker/stats.test.mjs`, `worker/CLAUDE.md`, `PRIVACY.md`, `site/dino/privacy.html`, `docs/design/` (part D, the mock),
this file.

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
- Decided by this session: paid books are left out of the Sonos browse until Slice 415 is built; Slice 415's decision 2
  (every book listed, paid ones locked) replaces this when it ships.
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

### B. Don't list books Sonos can't play
- [ ] B1. `worker/sonos.ts` `tracksOf`/`loadTracks`: skip books whose `access` is paid; `getMediaURI`,
      `getMediaMetadata` and `getExtendedMetadata` for a paid book's id return the SMAPI "item not found" fault, not a URL.
      Tests for each.
- [ ] B2. Note in `worker/CLAUDE.md` and in Slice 415's file that 415 reverses B1 (list every book, play the
      subscribe message on a locked one).

### C. New books show up quickly
- [ ] C1. Cut the Sonos list's colo Cache API lifetime (`worker/stories.ts:69-71`) from about 1 hour to 5 minutes, and keep
      the stale-while-rebuild path; test in `worker/stories-catalogue.test.mjs`.
- [ ] C2. Return the same order every call (catalogue order today; part D may change it) and a stable `total`, so a
      reload never shows a different count for the same catalogue.

### D. Browsing layout from a Claude Design mock (waits on the mock)
- [ ] D1. The owner makes the Sonos browse mock in Claude Design from the brief below; `import-claude-design` copies it
      into `docs/design/` and `design-to-plan` rewrites this part to build exactly what it shows.
- [ ] D2. Build the mock's tree in `worker/sonos.ts` (root containers, language handling from `Accept-Language`, order,
      grouping, search through the presentation map and the search container if the mock has search).

**Claude Design brief (paste into Claude Design):**

> Design the browse screens of "Dino" (kids' picture books read aloud) inside the Sonos app, as a Sonos music service.
> Constraints: Sonos draws its own UI. A service only controls a tree of containers and items, each with a title, an
> optional subtitle and square artwork, plus an optional search box with categories. No custom colours, fonts or
> buttons. Content: 8 books with audio today (16 in the catalogue), each in 15 languages (English, Arabic, Danish,
> German, Spanish, Farsi, Finnish, French, Japanese, Norwegian, Dutch, Portuguese, Russian, Swedish, Chinese), each with
> a cover, an age range (within 3-9), a length in minutes and a free or paid flag. The Sonos phone app tells us the
> listener's language (Accept-Language); speakers themselves do not. Problems to solve: today the first screen is a list
> of 15 languages to pick every time, then a flat unsorted list. Design the first screen and each level below it for a
> parent choosing a story for a child, in their own language without picking it each time, with a way to reach other
> languages, plus sorting or grouping that helps (for example new, by age, short ones for bedtime). Show the phone
> screens for an English-speaking parent and a Farsi-speaking parent.

## Verification

- `npm ci && npm run typecheck`, `node --test worker/*.test.mjs`: no row on getMediaURI; one row per `final` report of
  30 s or more with the capped seconds; none for `update`, short or unknown reports; paid books absent from browse and
  faulted by id; Sonos list cache 5 minutes; rollup drops pre-cutover estimated Sonos rows.
- Live, after the owner's A4 step: browse the service twice in the Sonos app without playing; an `analytics-query`
  shows no new `sonos` row. Play one free book for 2 minutes and stop; one row with about 120 s appears after Sonos's
  report. The dashboard's Sonos hours for 18-24 Nov drop to 0.
- Live: browse shows only the 2 free books in each language; a new listening track published appears within about 5
  minutes.

## Not yet verified

- [session] Whether Sonos sends `/timePlayed` reports to an anonymous (no-login) service: the docs are silent. If none
  arrive, Sonos plays show 0 rather than invented hours; that result goes back to the owner as one question.
- [session] Whether the 5-then-8 count came from the hour-long cache (not reproduced 2026-11-24).
