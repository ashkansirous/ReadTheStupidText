# Slice 283 — Dino: a Sonos music service for the store books (anonymous, no login)

*Planned 2026-11-06.* Server only (Cloudflare Worker); the apps do not change.

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
      submission, so the review clock starts. Claude writes the submission text (description, anonymous auth, endpoints).
- [ ] Context7 / `library-docs` / Sonos docs for the exact SMAPI WSDL shapes before any code.
- [ ] `sonos-soap.ts`: read the SOAP action and body, write envelopes and SOAP faults. Tests with recorded requests.
- [ ] `sonos.ts`: the five calls over `/api/stories` data; paging (`index`, `count`) for long lists.
- [ ] Sandbox integration in the portal pointed at `https://dinoreader.sirous.uk/sonos` (user adds their Sonos ID if it
      needs their login), then Self Test until it passes.
- [ ] Privacy text: one paragraph on Sonos, in the same shape as Alexa's.

## Verification

- Worker tests green.
- Self Test passes in the portal.
- On the user's own Sonos (sandbox): browse languages, open English, see covers and titles, play a book end to end, skip
  forward and back inside it.
- Sonos review, validation and preview passed; the service is live in the Sonos app.
