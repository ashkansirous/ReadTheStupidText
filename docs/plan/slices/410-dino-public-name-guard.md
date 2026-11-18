# Slice 410 — Dino: the public name is "Dino Book Reader" everywhere a person sees it

**Product:** Dino

**Touches:** `worker/sonos.ts`, `worker/sonos.test.mjs`, `PRIVACY.md`, `docs/plan/slices/283-dino-sonos-music-service.md`,
`docs/design/dino-reader/sonos/full-logo.svg`, `docs/design/dino-reader/sonos/support-banner.svg`,
`docs/design/dino-reader/sonos/support-banner.png`, `tools/dino/check-public-name.mjs` (new), `tools/dino/public-name.test.mjs` (new),
`.github/workflows/` (one step in the existing worker or docs check job), `src/Dino/DinoReader.App/CLAUDE.md`

## Why

User, 2026-11-18: the app was renamed **Dino Book Reader** right after it was made, but the Sonos sandbox was filled
with "Dino Reader" (service name, description, promo, the full-logo and support-banner wordmarks). The name came from
Slice 283's *Submission text* and the Batch 50 Sonos mock, which both still said "Dino Reader"; nothing checked them
against the app's real name. "Fix it so you wouldn't make the same mistake."

Facts checked on origin/main 2026-11-18:
- The real name is `ApplicationTitle` in `src/Dino/DinoReader.App/DinoReader.App.csproj:24`: `Dino Book Reader`. The
  App Store app is `dino-book-reader/id6825053435`, the Microsoft Store reservation is "Dino Book Reader" (`STORE.md:830`),
  the Alexa skill is "Dino Book Reader" (`alexa/skill-package/skill.json:7`).
- "Dino Reader" is the internal code name (project `DinoReader.App`, folder `src/Dino/`, plan headings, comments); that
  stays.
- Still public and wrong: `worker/sonos.ts:22-23` (`ARTIST_FALLBACK`, `ALBUM_NAME` = "Dino Reader", shown on the speaker),
  `PRIVACY.md:133,136,176,187,189,194,204` ("Dino Reader" as the product name, including the "Dino Reader music
  service in the Sonos app"), `docs/design/dino-reader/sonos/full-logo.svg` and `support-banner.{svg,png}` (wordmark
  "Dino Reader"), Slice 283 *Submission text*.
- The Sonos portal itself was corrected by hand on 2026-11-18 (name, description, promo); its logos still need the new files.

## Decisions

- The public name is taken from `ApplicationTitle` in the csproj, never typed from memory, a slice or a mock.
- A check (`tools/dino/check-public-name.mjs`) reads `ApplicationTitle` and fails on the bare old name "Dino Reader"
  (not part of "Dino Book Reader") in user-facing files: `PRIVACY.md`, `site/dino/**`, `alexa/skill-package/**`,
  string literals in `worker/sonos.ts` and `worker/alexa.ts` (comments ignored), and any slice section headed
  `Submission text` or `Store text`. It runs in CI with the worker tests. Code comments, file names, folders and plan
  headings are out of its scope.
- Wordmarks: regenerate `full-logo.svg` (white on transparent, fixed height 20 px, width at most 180 px: scale the
  "Dino Book Reader" outline to fit) and `support-banner.svg/png` (800 x 200, same layout as now) with the same font
  (Fredoka 600 outlines, from `src/Dino/DinoReader.App/Resources/Fonts/Fredoka.ttf`), plain paths only, as the Batch 50
  mock specified. The mock's two "Dino Reader" labels in `docs/design/` are not changed (design source record).

## Work

- [x] `worker/sonos.ts`: album and artist fallback read "Dino Book Reader"; update `worker/sonos.test.mjs`.
- [x] `PRIVACY.md`: the product name is "Dino Book Reader" wherever it names the app or the Sonos service.
  Check `site/dino/privacy.html` says the same (it is the published copy).
- [x] Slice 283 *Submission text* and the progress block: "Dino Book Reader".
- [x] Regenerate `full-logo.svg`, `support-banner.svg`, `support-banner.png` with the new wordmark (`tools/dino/wordmark/make-wordmarks.mjs`; fontkit + resvg-js, `npm ci` in that folder first).
- [x] `tools/dino/check-public-name.mjs` + test, wired into CI.
- [x] Rule line in `src/Dino/DinoReader.App/CLAUDE.md`: the public name comes from `ApplicationTitle`; the check enforces it.
- [ ] After merge: upload the new full logo and support banner to the Sonos sandbox, Send again (main thread, user's Chrome).
- [ ] After merge: publish the two changed store books (`love-from-seagull-island`, `x-marks-the-treasure`, credit now
      "Dino Book Reader") through the repo's store upload route, then compare `meta.json` with `/api/stories`.

## Sonos Self Test fixes (first run, 2026-11-18)

Self Test on the sandbox (service id 463879) after the first Send: SOAP Compliance, Browse and Sonos Cloud failed,
MIME Type not applicable. The failing lines, read from the portal's Test Results:
- Browse `TestBrowse::test_browse_to_leaf`: "Leaf node should be playable". Our tracks' `trackMetadata` has no `canPlay`.
- Sonos Cloud `test_root_containers_are_enumerable`: "Root containers with canEnumerate=false": every `lang:*` collection.
  The other Sonos Cloud failures were HTTP 422 "Unknown service. Service id: 463879" (the service was sent minutes
  before; rerun after the fixes, expected to clear).
- SOAP Compliance `TestSOAPFaultCompliance::test_fault_is_direct_child_of_body` "Expected a non-200 response for invalid
  credentials" (and the two faultcode/faultstring checks that follow from it): an anonymous service never issues a
  `loginToken` or `sessionId`, so a request whose credentials header carries one is invalid.

- [x] `mediaCollection` for each language: `<canEnumerate>true</canEnumerate>` (keep `canPlay` false), in the WSDL's element order.
- [x] `trackMetadata` for each track: `<canPlay>true</canPlay>` and `<canSkip>true</canSkip>`, in the WSDL's element order.
- [x] A request whose `credentials` header holds a `loginToken` or `sessionId` gets HTTP 500 with a SOAP 1.1 Fault
  `Client.LoginUnauthorized` (faultcode and faultstring present); a header with only `deviceId`/`deviceProvider`, or none,
  is still answered as now.
- [x] Tests for all three (worker/sonos.test.mjs).
- [ ] After merge and deploy, rerun Self Test (SOAP Compliance, Browse, Sonos Cloud) from the portal.

#### From review

- Review fixes (`reviews/s410-high.md`): `site/privacy.html` now says "Dino Book Reader" and all of `site/` is scanned; the credits in `content/dino/{good-enough,love-from-seagull-island,x-marks-the-treasure}/book.json` (English and the 14 translations) say "Dino Book Reader" and `content/dino/*/book.json` is scanned (good-enough is a starter: stamped to v12 and its bundled `.dino` rebuilt; the two store books are stamped by `publish-store`, so they need a store upload after merge; no page text changed, so no `EarlierPageTextHashes`); the check reads `ApplicationTitle` and asserts the Sonos album constants and PRIVACY.md carry it; matching is case-insensitive across spaces and line breaks; the scanner handles regex and nested template literals and covers every `worker/*.ts`; empty `<sessionId/>`, comments and CDATA are not credentials; the two wordmark SVGs no longer carry the old C2PA metadata.
- STORE.md is not scanned: its six "Dino Reader" lines are engineering prose (tags, crash endpoints), not store-listing text, and the listing blocks hold none.
- Second review (`reviews/s410-medium.md`) #2 applied: the check moved out of site-checks (which ignores `content/dino/**`) into its own node-only workflow `.github/workflows/public-name.yml`, with a `paths` filter listing every scanned path (books' `book.json` included) on PRs and pushes to main; actionlint clean.
- Second review #6 applied: `hasLoginCredentials` (`worker/sonos.ts`) reads only the SOAP header's `credentials` element (non-empty `loginToken`/`sessionId`); the Body and the rest of the envelope are never scanned, and a test puts both names in the Body and gets 200.
- Skipped #7 (`reviews/s410-high.md`): "Only Markdown headings "Submission text"/"Store text" are scanned in slices; bold labels or other names ("## Listing", "## Sonos copy") pass. ... Also the check scans slice 410 itself, so a quoted example under a "Store text" heading would fail it." - not applied by the caller's triage.
- Skipped #10 (`reviews/s410-high.md`): "Quoted glob `node --test "tools/dino/*.test.mjs"` relies on node's own glob (fine on Node 26 in this job); any later test in tools/dino needing its own deps ... would break the job." - not applied by the caller's triage; the glob works on the job's Node.
- Skipped from the second review (`reviews/s410-medium.md`), low or speculative: #1 (hyphenated/transliterated forms),
  #4, #5, #7 (wordmark script rerun safety), #8 (one-line constant assumption), #9 (CI step layout). #3 became the
  store-upload work item above.
- Check pass (`reviews/s410-check.md`): nothing applied. #1 (credential inside CDATA) and #2 (a `credentials` element
  inside the Body) are shapes Sonos does not send; #3-#9 low. Revisit only if a later Self Test or real request shows one.

## Verification

- `node tools/dino/check-public-name.mjs` passes on the branch and fails when "Dino Reader" is put back into `PRIVACY.md`.
- Worker tests green; a live `getMetadata` after deploy shows `<album>Dino Book Reader</album>`.
- The new wordmark PNG read at small size spells "Dino Book Reader".
