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
- [x] ~~A request whose `credentials` header holds a `loginToken` or `sessionId` gets HTTP 500 with a SOAP 1.1 Fault
  `Client.LoginUnauthorized`~~ **Reverted** (see *Self Test second run* below): Sonos's own tester sends those values on
  normal browse requests, so the server ignores the credentials header entirely.
- [x] Tests for all three (worker/sonos.test.mjs).
- [ ] After merge and deploy, rerun Self Test (SOAP Compliance, Browse, Sonos Cloud) from the portal.

*Main was red when this PR ran (2026-11-18):* `StoreFilterTests.EveryBookJsonRange_FitsExactlyOneBand` (Slice 394) and
Love from Seagull Island (PR merged the same day, ages 3-6) landed in parallel, so main's Dino tests fail. This PR sets
that book to 3-5 (the "up to 5" band, as every other 3+ book in `content/dino/`); it ships with the same store upload.

#### From review

- Review fixes (`reviews/s410-high.md`): `site/privacy.html` now says "Dino Book Reader" and all of `site/` is scanned; the credits in `content/dino/{good-enough,love-from-seagull-island,x-marks-the-treasure}/book.json` (English and the 14 translations) say "Dino Book Reader" and `content/dino/*/book.json` is scanned (good-enough is a starter: stamped to v12 and its bundled `.dino` rebuilt; the two store books are stamped by `publish-store`, so they need a store upload after merge; no page text changed, so no `EarlierPageTextHashes`); the check reads `ApplicationTitle` and asserts the Sonos album constants and PRIVACY.md carry it; matching is case-insensitive across spaces and line breaks; the scanner handles regex and nested template literals and covers every `worker/*.ts`; empty `<sessionId/>`, comments and CDATA are not credentials; the two wordmark SVGs no longer carry the old C2PA metadata.
- STORE.md is not scanned: its six "Dino Reader" lines are engineering prose (tags, crash endpoints), not store-listing text, and the listing blocks hold none.
- Second review (`reviews/s410-medium.md`) #2 applied: the check moved out of site-checks (which ignores `content/dino/**`) into its own node-only workflow `.github/workflows/public-name.yml`, with a `paths` filter listing every scanned path (books' `book.json` included) on PRs and pushes to main; actionlint clean.
- Second review #6 applied: `hasLoginCredentials` (`worker/sonos.ts`) reads only the SOAP header's `credentials` element (non-empty `loginToken`/`sessionId`); the Body and the rest of the envelope are never scanned, and a test puts both names in the Body and gets 200.
- Second review #3 (`reviews/s410-medium.md`) became the store-upload work item above.
- Skipped #7 (`reviews/s410-high.md`): "tools/dino/check-public-name.mjs:11, low. Only Markdown headings" - the slices that hold copy for people use those two headings; widening it would flag the internal code name across every plan file.
- Skipped #10 (`reviews/s410-high.md`): ".github/workflows/site-checks.yml:54, low. Quoted glob" - the step moved to public-name.yml, where node's own glob works on the job's Node and the tests need no packages.
- Skipped #1 (`reviews/s410-medium.md`): "CODE_NAME misses hyphenated, suffixed and transliterated forms" - no such form exists in the repo today, and the translations keep the Latin name as the app's resx strings do.
- Skipped #4 (`reviews/s410-medium.md`): "slice 410 itself is scanned" - same reason as the high pass's #7 above: widening the headings flags the code name in every plan file.
- Skipped #5 (`reviews/s410-medium.md`): "tokenizer decides regex vs division from the previous token only" - no file the check scans has a regex that desyncs it; a desync would show as a false failure, never a silent pass of user text.
- Skipped #7 (`reviews/s410-medium.md`): "replaceText not rerun-safe" - the wordmark script is run by hand once per rename and its output was checked by eye at small size.
- Skipped #8 (`reviews/s410-medium.md`): "a reformat gives a false" - a reformat would fail loudly in CI, which is the safe direction; no reformat is planned.
- Skipped #9 (`reviews/s410-medium.md`): "the check is chained with && in one step" - the step was moved out of site-checks into its own workflow in this PR.
- Skipped #1 (`reviews/s410-check.md`): "credential written as CDATA content is stripped, guard misses it" - Sonos sends tokens as plain text; an anonymous service issues none, so a CDATA token never arrives.
- Skipped #2 (`reviews/s410-check.md`): "CREDENTIALS_ELEMENT matches first" - no SMAPI request puts a credentials element in the Body; the next Self Test run checks the real shapes.
- Skipped #3 (`reviews/s410-check.md`): "over-matches names like" - Sonos uses only the two exact element names; a lookalike name is not a real request.
- Skipped #4 (`reviews/s410-check.md`): "possible quadratic CPU on an unauthenticated endpoint" - the body is capped at 16 KiB before this runs, so the worst case stays small.
- Skipped #5 (`reviews/s410-check.md`): "silently no-ops if envelope shape changes" - the envelope fixture lives in the same test file and changes only with the test; a silent no-op would fail the 500 assertions.
- Skipped #6 (`reviews/s410-check.md`): "trackMetadata field order pinned to the code's own order" - the order was checked against the WSDL by the first review pass, which says so in its report.
- Skipped #7 (`reviews/s410-check.md`): "check runs before parseSonosRequest" - a non-SOAP body with a session id is not a real Sonos request, and either fault is a refusal.
- Skipped #8 (`reviews/s410-check.md`): "path-filtered workflow" - public-name is not a required check, so a PR that touches no scanned path needs no report from it.
- Check pass #9 (`reviews/s410-check.md`) applied after CI's actionlint failed on it ("paths section must be sequence
  node but got alias node"): both `paths` lists are written out. The skip had rested on the fixer's local actionlint
  run, which I did not check.

## Self Test second run (2026-11-18, 13:57, after the merge and deploy)

SOAP Compliance: 22 passed, 20 failed; every failure is `assert 500 == 200`. Browse: all six tests fail with "The request
for metadata failed with error: HTTP 500". Sonos's own tester sends a non-empty `loginToken`/`sessionId` with its normal
browse requests, so the `Client.LoginUnauthorized` rule (item 3 of the first-run fixes) refuses them. The three fault-format
checks it was added for now pass, but the cost is every browse call. Decision: an anonymous service ignores the
credentials header entirely, as it did before this slice; the "invalid credentials" checks stay failing (they test an
authenticated service).

- [x] Remove the `Client.LoginUnauthorized` path (`hasLoginCredentials` and its call in `worker/sonos.ts`, the
      `FaultCode.LoginUnauthorized` constant in `worker/sonos-soap.ts` if nothing else uses it); any request whose body
      parses is answered whatever its credentials header holds. Replace the 500 tests with: a header carrying a
      non-empty `loginToken` and `sessionId` (and deviceId, deviceProvider, zonePlayerId) is answered 200 like any other.
- [x] After deploy: rerun SOAP Compliance, Browse and Sonos Cloud; record the results here.
  Third run, 2026-11-18 14:12-14:13 (PR #1271 live; a credentialed `getMetadata` answered 200 before the run):
  - **Browse: Passed** (was failed twice).
  - **SOAP Compliance: 47 passed, 3 failed.** The three are `TestSOAPFaultCompliance` "Expected a non-200 response for
    invalid credentials" and the faultcode/faultstring checks that follow from it: checks for a service with sign-in,
    failing by decision above.
  - **Sonos Cloud: 5 passed, 7 failed.** `test_root_containers_are_enumerable` now passes. All 7 failures are Sonos's
    cloud answering HTTP 422 `ERROR_SERVICE_NOT_CONFIGURED` "Unknown service. Service id: 463879. Account id: 0", the
    same as the first run 50 minutes earlier: Sonos's cloud side does not know the sandbox service. Nothing in our
    server is involved (the request never reaches it). Open: whether this clears on its own or needs the production
    conversion or a Sonos support question; recheck at the next Self Test.
  - MIME Type: "Not Applicable" (first run).
  - Store: Love from Seagull Island v2 and X Marks the Treasure! v14 are live on `/api/stories` with "A Dino Book Reader
    picture book", both ages 3-5. Live `getMetadata` returns `<album>Dino Book Reader</album>`, `canPlay`/`canSkip` true.
  - Not yet verified: Dino Book Reader showing in the user's Sonos app (Add Services) and a play on a speaker (user's
    device; Slice 283 *Not yet verified*).

#### From review (second run fix)

- Applied #1 (`reviews/s410b-high.md`) and #1 (`reviews/s410b-medium.md`): the first-run item now reads as reverted.
- Skipped #2 (`reviews/s410b-high.md`): "The one regression test sends the credentials header only on getLastUpdate" - the credentials header is no longer read on any path, so a getMetadata case would test the same nothing; the Self Test rerun checks the real browse calls.
- Skipped #3 (`reviews/s410b-high.md`): "all on getLastUpdate. Maintenance" - overlap in tests is harmless; trimming them is not worth a re-review while Sonos browsing is broken live.
- Skipped #4 (`reviews/s410b-high.md`): "checks are left failing by decision" - the Self Test rerun result goes in this section; a certification follow-up is decided once Sonos reviews the production submission.
- Skipped #5 (`reviews/s410b-high.md`): "is attributed wholly to the removed rule" - the rerun after deploy shows which failures remain, which settles it better than reasoning now.
- Skipped #2 (`reviews/s410b-medium.md`): "new test sends credentials only with getLastUpdate" - same reason as the high pass's #2.
- Skipped #3 (`reviews/s410b-medium.md`): "does not compare the body to the anonymous response" - nothing reads the header any more, so there is no code path that could make the bodies differ.
- Skipped #4 (`reviews/s410b-medium.md`): "post-deploy rerun of SOAP Compliance" - it is the open work item just above, done after the merge.
- Skipped #5 (`reviews/s410b-medium.md`): "no inline comment in respond" - the slice file and this section record why; a comment would repeat it.

## Verification

- `node tools/dino/check-public-name.mjs` passes on the branch and fails when "Dino Reader" is put back into `PRIVACY.md`.
- Worker tests green; a live `getMetadata` after deploy shows `<album>Dino Book Reader</album>`.
- The new wordmark PNG read at small size spells "Dino Book Reader".
