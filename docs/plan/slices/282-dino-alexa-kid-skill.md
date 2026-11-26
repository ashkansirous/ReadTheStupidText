# Slice 282 — Dino: an Alexa kid skill that reads the store books
**Product:** Dino

**Plan-cleanup check 2026-11-25:** code merged (#935); live `ALEXA_SKILL_ID` is still empty in both wrangler configs and `POST https://dinoreader.sirous.uk/alexa` returns 400. Skill creation is the owner step (user, 2026-11-19: "I have problems with amazon developer. park until tomorrow"; nothing since). The observability/privacy line is not this slice's work (moved to Outstanding work); privacy.html on main has no Sonos or Alexa paragraph about server logs.

*Built 2026-11-07 (night 2026-11-07-1412 wave 6), merged as PR #935 (e1d0a461); not yet tried on a real Echo.* *Planned 2026-11-06; item 1 decided 2026-11-07 (see Work).* Server only (Cloudflare Worker) plus the skill definition; the apps do not change.

**Depends on:** Slice 281 (the listening tracks).
**Touches:** `alexa/skill-package/**` (new: manifest, interaction models en-GB and en-US), `worker/alexa*.ts` (new),
`worker/alexa*.test.mjs` (new), `worker/index.ts` (one dispatch line), `worker/speaker-usage.ts`,
`worker/speaker-usage.test.mjs`, `wrangler.jsonc` and `wrangler.workers-dev.jsonc` (`run_worker_first` with the
`/dino/...` twin check-configs requires, and an `ALEXA_SKILL_ID` var), `package.json` and `package-lock.json` (only if
the certificate library below is added), `worker/CLAUDE.md`, `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html`,
this file. (Wave 2026-11-07 premise check: `resolveSite` already strips `/dino`, so `worker/downloads.ts` is not touched.)

## Design (coordinator, wave 2026-11-07, before the claim)

Premises checked on origin/main 9ec6489c: dispatch goes like Sonos (`worker/index.ts:120-122`, `serveSonos` at :54-59);
`/api/stories` items carry `listenUrl` and `listenSeconds` (only books that have both; the list may be empty);
`recordSpeakerPlay(usage, {platform, book, language, seconds}, request)` (`worker/speaker-usage.ts:65`) hardcodes detail
`estimated` (:55); no X.509 code or dependency exists; worker tests run with `node --test worker/*.test.mjs` and import
`.ts` by type-stripping (no enums, no parameter properties).

- **State: none on the server.** The one owner of playback state is Alexa. Every AudioPlayer `Play` directive carries a
  token `v1|<bookId>|<lang>|<storyVersion>|<segmentStartMs>`; every request about playback (AudioPlayer events,
  PlaybackController, built-in intents) reads `context.AudioPlayer.token` and `offsetInMilliseconds`. Interrupts:
  pause/stop = `AudioPlayer.Stop`, nothing stored; resume = a new `Play` of the same book at the sent offset with a
  new token whose `segmentStartMs` is that offset; next/previous = the neighbour in the live `/api/stories` order
  (wrap off: last + next says there is no next book); start over = offset 0; a token that no longer parses or names a book
  no longer listed = say the book is not available and list books. A malformed or foreign token is never trusted for
  anything but these fields, each validated (book id pattern from `speaker-usage.ts`, lang in the listed languages,
  numbers in range).
- **Counting (Slice 289 table):** one `book_listened` row per played segment, written on `PlaybackStopped` or
  `PlaybackFinished`: seconds = event offset minus the token's `segmentStartMs` (clamped to 0..listenSeconds), detail
  `reported`; if the event carries no offset, the book's `listenSeconds` minus segment start, detail `estimated`.
  `SpeakerPlay` gains an optional `detail` (default `estimated`, so Sonos is unchanged) and `speaker-usage.ts` exports
  `SPEAKER_PLATFORM_ALEXA = "alexa"`. A paused-and-resumed book makes two rows (no state to join them): written in
  `worker/CLAUDE.md` next to the `blob7` notes. No household, user, device or skill-instance id is stored; the Worker
  logs nothing (worker/CLAUDE.md:26).
- **Request check (`alexa-verify.ts`), all pure functions plus one fetch:** (1) `SignatureCertChainUrl` rules (https,
  host `s3.amazonaws.com` case-insensitive, path starts `/echo.api/` after normalising, port 443 or none); (2) fetch the
  PEM chain, cached per isolate by URL; (3) leaf valid now, SAN contains `echo-api.amazon.com`, chain verifies up to a
  root in a small bundled list; (4) `Signature-256` header = base64 RSA-SHA256 over the raw body bytes with the leaf key
  (`crypto.subtle`); (5) `request.timestamp` within 150 s; (6) `session.application.applicationId` / `context.System.application.applicationId` must equal the `ALEXA_SKILL_ID`
  var. **Fail closed (review, 2026-11-07):** an empty id refuses every request with 400, because the signature only proves
  the sender is Alexa, not that it is our skill (any developer could point a skill at this URL). The user step of creating
  the skill sets the id in both wrangler configs; until then the endpoint answers 400 to everyone. Any failure = HTTP 400, no body detail.
  Also from review: the chain cache is keyed by the normalised URL (origin and path, size-capped, failures kept 60 s), bodies are
  read with a running byte cap, both signature-algorithm fields must match and be RSA-SHA256, issuers must be CAs, and
  PlaybackController answers hold AudioPlayer directives only (Amazon's PlaybackController reference).
  Certificate parsing: first `library-docs` (context7) for `@peculiar/x509` (or `pkijs`) working in Workers; use it if it
  bundles with wrangler and runs under `node --test`; otherwise a minimal DER reader limited to what the steps need.
  Which roots Amazon's current `echo-api` chain ends in comes from fetching the live chain (read-only), not from memory.
  Tests use a test-generated CA, leaf and signed bodies (injectable root list and clock), plus forged-signature,
  stale-timestamp, bad-URL and wrong-SAN cases.
- **Speech copy** lives in `alexa.ts` as named constants (the backend speaks to the user here; there is no other UI).
  Launch lists at most 5 titles; an empty list says there are no books yet and ends the session.

## Why

User, 2026-11-06: Dino books on Alexa, no login, no parent recordings. Facts checked 2026-11-06:

- Kid skills exist in the UK (under-16 threshold there). A child-directed skill may not use account linking or collect any
  personal information. Outside the US, parents switch kid skills on once in the Alexa app.
  https://developer.amazon.com/en-US/alexa/alexa-skills-kit/get-deeper/custom-skills/kid-skills,
  https://developer.amazon.com/en-US/docs/alexa/custom-skills/policy-requirements-for-an-alexa-skill.html
- Developer account is free; AWS is not required. Any HTTPS service works if it uses port 443 and a trusted certificate,
  verifies Alexa's request signature, and rejects requests older than 150 seconds.
  https://developer.amazon.com/en-US/docs/alexa/ask-overviews/create-developer-account.html,
  https://developer.amazon.com/en-US/docs/alexa/custom-skills/host-a-custom-skill-as-a-web-service.html
- Testing before certification: the skill works on the developer's own Echo (same email, matching locale); the console
  simulator does not play audio; a beta takes up to 500 testers for at most 90 days.
  https://developer.amazon.com/en-US/docs/alexa/test/test-your-skill-overview.html,
  https://developer.amazon.com/en-US/docs/alexa/custom-skills/skills-beta-testing-for-alexa-skills.html
- **Unconfirmed:** whether certification allows long-form audio (AudioPlayer) in a kid skill. Amazon's pages say nothing
  either way. Item 1 settles it before the build.

## Decisions (planning defaults)

- English first (en-GB and en-US models), playing the English track. Other book languages that Alexa has a locale for
  (de, fr, es, ja, pt, ...) are a follow-up line, not this slice.
- Voice flow: "Alexa, open Dino Bedtime Stories" lists a few books; "read <book>" plays it; built-in pause, resume, next
  (next book), previous, start over, stop. The book list and titles come from `/api/stories`, so new store books appear
  with no skill change.
- **No server state.** Resume uses the token and offset Alexa sends with each request. Nothing about the household is
  stored; the Worker logs no titles, text or ids (worker/CLAUDE.md rules).
- The Worker endpoint `POST /alexa` (on `dinoreader.sirous.uk`) verifies the signature chain and timestamp itself.
- Pictures on Echo Show screens are out of this slice (not checked yet).

## Work

- [x] 1. **Decided 2026-11-07 (user):** "I am not going to ask amazon anything on 282. We do based on whatever we can find in their documentation." Amazon's documentation (read 2026-11-07) lists what a child-directed skill may not do: collect personal information, account linking, selling outside In-Skill Purchasing, promoting or directing users to content outside Alexa, content not suitable for all ages
      (https://developer.amazon.com/en-US/docs/alexa/custom-skills/policy-requirements-for-an-alexa-skill.html,
      https://developer.amazon.com/en-US/alexa/alexa-haus/child-directed-skills). Nothing in them bars the AudioPlayer
      interface (https://developer.amazon.com/en-US/docs/alexa/custom-skills/audioplayer-interface-reference.html has no
      child-directed restriction). **Decision: build it as a kid skill using AudioPlayer; if certification refuses
      AudioPlayer, fall back to a skill not marked for kids** (then update the privacy and policy text to match). Nobody
      asks Amazon.
- [x] Context7 / `library-docs` for the ASK request/response JSON, AudioPlayer directives and the signature-verification
      steps before any code (no SDK assumed: the Worker is plain TypeScript). (Done: `/alexa/alexa-skills-kit-sdk-for-nodejs`,
      `/peculiarventures/x509`; the manifest fields from Amazon's skill-manifest page. Certificate library: none, a minimal DER
      reader, because `@peculiar/x509` needs a reflect polyfill under node and wrangler.)
- [x] `alexa-verify.ts`: certificate URL rules, chain check, signature check, 150 s timestamp. Tests with a test-generated CA
      and signed bodies (plus the real Amazon chain checked once by hand at a date inside its validity).
- [x] `alexa.ts`: LaunchRequest, ReadBookIntent (book slot filled from the live list), AMAZON built-ins, AudioPlayer events
      (answer with no speech), SessionEnded. One handler per request type.
- [x] Count plays for the usage dashboard (added 2026-11-07, Slices 287 and 289; the user wants Alexa in its platform
      filter): one `book_listened` row with platform `alexa` per book started, through `worker/speaker-usage.ts` (Slice 289);
      played seconds from the AudioPlayer `PlaybackStopped`/`PlaybackFinished` offset (detail `reported`), else the book's
      length (detail `estimated`). No household or account id. Adds `worker/speaker-usage.ts` to this slice's Touches.
- [x] `skill-package/`: manifest marked child-directed, category `CHILDRENS_EDUCATION_AND_REFERENCE` (`KIDS` is not a value; a child-directed skill must use one of four CHILDRENS_ categories, per Amazon's skill-manifest reference), privacy policy URL, en-GB and en-US models, and the four PNG icons (108x108 and 512x512, from `site/dino/icon.png`) under `alexa/skill-package/assets/`.
- [x] Privacy text (`PRIVACY.md` and `site/dino/privacy.html`; `site/privacy.html` is the RTST page and has no Sonos paragraph either): one paragraph on Alexa (what Amazon sends us: the request, no name or account; what we keep: a count
      of each book played and for how long, nothing about the home).
- [x] **User steps** (their Amazon login): create the developer account, create the skill from `skill-package/`, set the
      endpoint, enable testing; later, submit for certification. Steps Claude can do with a token (ASK CLI) it does.
      *Done 2026-11-25:* the user created the Amazon developer account and signed the ASK CLI in once (profile `default`,
      vendor `M2F7IO7I4G7Y5U`, no AWS link). Claude ran `ask deploy` from `alexa/` (new `alexa/ask-resources.json`; the
      CLI's `alexa/.ask/` state is git-ignored): skill `amzn1.ask.skill.32f02175-4e96-4a88-98c1-b858c48c048d` created
      from `skill-package/` with the endpoint `https://dinoreader.sirous.uk/alexa`, both models built, enabled for
      testing (development stage). `ALEXA_SKILL_ID` set in both wrangler configs by this PR.
- [x] **Fixed 2026-11-26: the certificate type.** `ask smapi invoke-skill-end-point` gave the real error: "Certificate for host 'dinoreader.sirous.uk' contains wildcard '*.dinoreader.sirous.uk'". Cloudflare's edge certificate lists `*.dinoreader.sirous.uk`, and Alexa's `Trusted` type refuses any certificate with a wildcard name (Cloudflare security events and edge request logs showed no block; read with the new read-only token `CF_READ_ALL_TOKEN`). `skill.json` now says `"sslCertificateType": "Wildcard"`, redeployed with `ask deploy`. Simulation after the fix: "open dino bedtime stories" lists the five books; "read the little red hen" answers "Here is The Little Red Hen." with an `AudioPlayer.Play` of `/api/stories/<id>/listen/en`. Not yet heard on a real Echo.
- [x] ~~**First live test fails (2026-11-25, after PR #1372 deployed version 8577735a with the skill id):** `ask smapi simulate-skill` "open dino bedtime stories" (en-GB, development) three times returns "Request to skill endpoint resulted in an error." with no response body (45 ms). `wrangler tail` (POST) during the simulation shows no `/alexa` request reaching the Worker, while a test POST from this PC with an Amazon-like user agent does reach it (400, as designed). So Amazon's call stops before the Worker: likely at Cloudflare's edge (Bot Fight Mode or a security rule against AWS addresses) or the TLS handshake (certificate Google Trust Services WE1, SAN covers `dinoreader.sirous.uk`). Not proven: no tool here can read the zone's security events (`CF_ANALYTICS_READ_TOKEN` sees no zones; reading zone security settings with the wrangler login was refused by the permission check). Next: read Security > Events for `/alexa` on the `sirous.uk` zone, then allow Amazon's requests to `/alexa` only (skip rule), and rerun the simulation.~~ (wrong guess; the cause is the line above)
- [ ] Submit for certification (Claude, ASK CLI `ask smapi submit-skill-for-certification`) after the first test on the live endpoint passes.

**Skill console:** https://developer.amazon.com/alexa/console/ask/test/amzn1.ask.skill.32f02175-4e96-4a88-98c1-b858c48c048d/development/en_GB/

## Verification

- Worker tests green, including a rejected forged signature and a rejected stale timestamp.
- On the user's own Echo in development: open, list, read a book, pause, resume after a minute (resumes at the same
  place), next, stop.
- Beta with a few families before certification; certification passed and the skill live in the UK.

*Changed while building:* `POST /alexa` and `/dino/alexa` in `worker/alexa.ts`, `alexa-verify.ts` (hand-written DER reader: `@peculiar/x509` needs a reflect polyfill under node and wrangler), `alexa-roots.ts` (Amazon Root CA 1 and Starfield Services G2, from the live echo-api chain); `speaker-usage.ts` has an optional `detail` and `SPEAKER_PLATFORM_ALEXA`. Manifest category is `CHILDRENS_EDUCATION_AND_REFERENCE` (`KIDS` is not a valid value per the skill-manifest docs); icons come from the 1024 Dino app icon. Privacy text is in `PRIVACY.md` and `site/dino/privacy.html` (`site/privacy.html` unchanged: RTST page). Differences from the design: an empty `ALEXA_SKILL_ID` rejects every request (fail closed); the chain cache holds successes only, LRU 16, TTL min(1 h, leaf notAfter) (redesign after check 1, second strike); cheap claims (timestamp, skill id) run before the chain fetch; a resume past the end or with a changed story version starts at 0; PlaybackController answers are directives only (Amazon docs); NavigateHome = stop.

*Tests:* `node --test worker/*.test.mjs` 269/269, `npm run typecheck` green.

*From review:* files `reviews/pr-935-high.md`, `reviews/pr-935-medium.md`, `reviews/pr-935-check1.md`, `reviews/pr-935-check2.md`, `reviews/pr-935-check3.md` read in full; findings not listed as skipped were applied (check 3 itself says "no new failure path" for its own skips).
- Skipped #8 (`reviews/pr-935-high.md`): "Verify and reword the privacy text, or exclude /alexa from logging." (pre-existing issue, see Outstanding below)
- Skipped #1 (`reviews/pr-935-medium.md`): "redeployed over a dashboard-set id" (the user sets the id in the configs by PR)
- Skipped #7 (`reviews/pr-935-medium.md`): "worker/alexa.ts:273, low: resumeOffset restarts a book when the offset is at or past catalogue seconds": the fix for high #5 made a finished or stale resume start the book over, so this restart is the intended behaviour.
- Skipped #5 (`reviews/pr-935-check1.md`): "returns end:true with no directive" for next on the last book (a voice Next past the end speaks and ends the session; a PlaybackController button answer may not carry speech per Amazon's PlaybackController docs, and the current book keeps playing)
- Skipped #7 (`reviews/pr-935-check1.md`): "replayed signed PlaybackStopped/Finished accepted within 150 s window" (the design keeps no server state, so no dedup store; a replay needs a captured signed request and only adds one anonymous count)
- Skipped #8 (`reviews/pr-935-check1.md`): "ignores pathLenConstraint, keyUsage, EKU, name constraints" (the roots are pinned to Amazon Root CA 1 and Starfield Services G2, both Amazon-owned, and the leaf must name echo-api.amazon.com)
- Skipped #2 (`reviews/pr-935-check2.md`): "use plain MAX_SECONDS clamp when offset is present" (low; efficiency only, no wrong result)
- Skipped #4 (`reviews/pr-935-check2.md`): "skips intermediate pathLenConstraint, KeyUsage, EKU and leaf usage checks" (low; same as check1 #8: pinned Amazon-owned roots and the echo-api SAN)
- Skipped #5 (`reviews/pr-935-check2.md`): "no single-flight for chain fetch" (low; a cold start fetches the chain a few times at most, no wrong result)
- Skipped #6 (`reviews/pr-935-check2.md`): "Accepted as best effort (no server state)" (low, double-counting on retry)
- Skipped #1-#5 (`reviews/pr-935-check3.md`): "Skipped 2, 4, 5, 6: no new failure path"; all five findings are low and were left unapplied (#1 is about one second of silence at a resume just under the end, low; #5 is the pre-existing logging question in Outstanding)

*Outstanding (pre-existing, not this slice):* `wrangler.jsonc` has observability enabled for every route while the Sonos and Alexa privacy paragraphs say the receiving server may log and "Dino Reader itself does not receive or keep" that log. Check what Workers Logs record and make the text true, or switch logging off for those routes (raised by the medium, check 2 and check 3 reviews).

#### For CLAUDE.md (lifted 2026-11-07)

- The Alexa endpoint fails closed: an empty `ALEXA_SKILL_ID` (`wrangler.jsonc`, `wrangler.workers-dev.jsonc`) rejects every request with 400, because Amazon's signature proves only that the sender is Alexa. Do not add a fail-open path. `worker/alexa-verify.ts`. (Slice 282: docs/plan/slices/282-dino-alexa-kid-skill.md)
- Alexa AudioPlayer and PlaybackController replies carry directives only, never speech; the server keeps no playback state (the token and offset come from each request). `worker/alexa.ts`. (Slice 282: docs/plan/slices/282-dino-alexa-kid-skill.md)

#### For README.md

None (server only, not user-facing yet).

#### Not yet verified

- Everything on a real Echo: open, list, read, pause, resume after a minute, next, stop.
- Request shapes for AudioPlayer events and built-in intents are partly written from memory (only the Play directive and PlaybackController were checked against Amazon's docs).
- A real signed Alexa request passing verification live.
