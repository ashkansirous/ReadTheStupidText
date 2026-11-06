# Slice 282 — Dino: an Alexa kid skill that reads the store books

*Planned 2026-11-06.* Server only (Cloudflare Worker) plus the skill definition; the apps do not change.

**Depends on:** Slice 281 (the listening tracks).
**Touches:** `alexa/skill-package/**` (new: manifest, interaction models en-GB and en-US), `worker/alexa.ts` (new),
`worker/alexa-verify.ts` (new), `worker/alexa*.test.mjs` (new), `worker/index.ts` (one dispatch line), `worker/downloads.ts`
(`resolveSite`), `wrangler.jsonc` and `wrangler.workers-dev.jsonc` (`run_worker_first`, with the `/dino/...` twin
check-configs requires), `worker/CLAUDE.md`, `PRIVACY.md`, `site/dino/privacy.html`, this file.

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

- [ ] 1. Before building: ask Amazon (developer support / forum) or read the certification checklist for kid skills and
      AudioPlayer; write the answer here. If refused, stop and bring the choice back to the user (a skill not marked for
      kids changes the privacy and policy picture).
- [ ] Context7 / `library-docs` for the ASK request/response JSON, AudioPlayer directives and the signature-verification
      steps before any code (no SDK assumed: the Worker is plain TypeScript).
- [ ] `alexa-verify.ts`: certificate URL rules, chain check, signature check, 150 s timestamp. Tests with recorded requests.
- [ ] `alexa.ts`: LaunchRequest, ReadBookIntent (book slot filled from the live list), AMAZON built-ins, AudioPlayer events
      (answer with no speech), SessionEnded. One handler per request type.
- [ ] `skill-package/`: manifest marked child-directed, Kids category, privacy policy URL, en-GB and en-US models.
- [ ] Privacy text: one paragraph on Alexa (what Amazon sends us: the request, no name or account; what we keep: nothing).
- [ ] **User steps** (their Amazon login): create the developer account, create the skill from `skill-package/`, set the
      endpoint, enable testing; later, submit for certification. Steps Claude can do with a token (ASK CLI) it does.

## Verification

- Worker tests green, including a rejected forged signature and a rejected stale timestamp.
- On the user's own Echo in development: open, list, read a book, pause, resume after a minute (resumes at the same
  place), next, stop.
- Beta with a few families before certification; certification passed and the skill live in the UK.
