# Slice 282 — Dino: an Alexa kid skill that reads the store books

*In progress (wave 2026-11-07), draft PR.* *Planned 2026-11-06; item 1 decided 2026-11-07 (see Work).* *Earlier park reason, now settled (night 2026-11-06-1250 wave 4; moved back to planned by the night's final feedback loop, 2026-11-07): item 1 unsettled. Amazon's kid-skill, policy and AudioPlayer pages neither allow nor forbid long-form audio (AudioPlayer) in a child-directed skill. Next step: the user asks Amazon (developer support, their account) or chooses a skill not marked for kids; a wave re-parks it until then.* Server only (Cloudflare Worker) plus the skill definition; the apps do not change.

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
  (`crypto.subtle`); (5) `request.timestamp` within 150 s; (6) when the `ALEXA_SKILL_ID` var is non-empty,
  `session.application.applicationId` / `context.System.application.applicationId` must equal it (empty until the user
  creates the skill: the signature already proves the request is from Alexa). Any failure = HTTP 400, no body detail.
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
- Voice flow: "Alexa, open Dino Book Reader" lists a few books; "read <book>" plays it; built-in pause, resume, next
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
- [ ] Context7 / `library-docs` for the ASK request/response JSON, AudioPlayer directives and the signature-verification
      steps before any code (no SDK assumed: the Worker is plain TypeScript).
- [ ] `alexa-verify.ts`: certificate URL rules, chain check, signature check, 150 s timestamp. Tests with recorded requests.
- [ ] `alexa.ts`: LaunchRequest, ReadBookIntent (book slot filled from the live list), AMAZON built-ins, AudioPlayer events
      (answer with no speech), SessionEnded. One handler per request type.
- [ ] Count plays for the usage dashboard (added 2026-11-07, Slices 287 and 289; the user wants Alexa in its platform
      filter): one `book_listened` row with platform `alexa` per book started, through `worker/speaker-usage.ts` (Slice 289);
      played seconds from the AudioPlayer `PlaybackStopped`/`PlaybackFinished` offset (detail `reported`), else the book's
      length (detail `estimated`). No household or account id. Adds `worker/speaker-usage.ts` to this slice's Touches.
- [ ] `skill-package/`: manifest marked child-directed, Kids category, privacy policy URL, en-GB and en-US models.
- [ ] Privacy text: one paragraph on Alexa (what Amazon sends us: the request, no name or account; what we keep: a count
      of each book played and for how long, nothing about the home).
- [ ] **User steps** (their Amazon login): create the developer account, create the skill from `skill-package/`, set the
      endpoint, enable testing; later, submit for certification. Steps Claude can do with a token (ASK CLI) it does.

## Verification

- Worker tests green, including a rejected forged signature and a rejected stale timestamp.
- On the user's own Echo in development: open, list, read a book, pause, resume after a minute (resumes at the same
  place), next, stop.
- Beta with a few families before certification; certification passed and the skill live in the UK.
