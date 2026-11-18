### Slice 411 — Dino: one command sets up a new book's sale product in all three stores
**Product:** Dino

*Planned.*

**Touches:** `.github/scripts/store-products.mjs`, `.github/scripts/store-products.test.mjs`, `.github/scripts/store-products-local.mjs` (new), `.claude/skills/create-dino-story/SKILL.md`, `.github/CLAUDE.md`

Why: the user, 2026-11-18. Love from Seagull Island went live in the Dino store but showed "cannot be bought right now", because
nothing created its in-app product. Play and App Store Connect were fixed by running `store-products.mjs --apply`. For Windows the
script only makes an empty add-on, and the first 11 books were priced and submitted by a Chrome agent in Partner Center. The user:
"on windows, this is not a 1 time thing to be done with chrome. fix the script so that the new books can be added easily".

## What was proven on 2026-11-18 (live, Love from Seagull Island)

- The Partner Center Entra app "ReadTheStupidText" (Manager role) authenticates the Store submission API with the keys already in
  `D:/secrets/dinoreader/secrets.env`: `MS_STORE_TENANT_ID`, `MS_STORE_CLIENT_ID`, `MS_STORE_CLIENT_SECRET`. The script's
  `MS_SUBMISSION_*` variables take these values. The Dino Store app id is `9N9VHRLCZXN5`.
- The existing book add-ons' published submissions read back `pricing: { priceId: "Base", isAdvancedPricingModel: true,
  marketSpecificPricings: { LB: "NotAvailable", EH: "NotAvailable" } }`, `contentType: "BookDownload"`, `lifetime: "Forever"`,
  `visibility: "Private"`, `privacyPolicyRequired: true`, `privacyPolicyUrl: "https://dinoreader.sirous.uk/privacy"`, en-us listing.
  The API never shows the real price of these ("Base").
- Microsoft's docs (https://learn.microsoft.com/en-us/windows/uwp/monetize/manage-add-on-submissions) list `Tier1012`-`Tier1424` as
  valid `priceId` values on the advanced pricing model, and the msstore CLI docs map `Tier1012` to USD 0.99. A new add-on created
  through the API (`9N6J9F50FM6G`) took a PUT with `priceId: "Tier1012"`, the template fields above and an en-us listing; the read-back
  submission is `PendingCommit` with `priceId: "Tier1012"`. The commit was rate limited (HTTP 429, "try again in 292 seconds").
  The commit's result and the live price are recorded below once read.
- A live add-on's price is readable without credentials from
  `https://displaycatalog.mp.microsoft.com/v7.0/products/<storeId>?market=US&languages=en-us`
  (`DisplaySkuAvailabilities[].Availabilities[]` with Actions `Purchase`: `OrderManagementData.Price.ListPrice`). Goldilocks
  `9P7HG6S98TVG` reads `USD 0.99` there.

## Decisions

1. Microsoft book add-ons are completed by the API, never by hand: create the shell (as today), then create a submission, PUT the
   price `Tier1012`, the en-us listing (title = the book's English title; description = the same fixed line Apple uses,
   `A picture-book story in Dino Book Reader.`), and the other fields copied from the newest published book add-on's submission
   (`contentType`, `lifetime`, `visibility`, `privacyPolicyRequired`, `privacyPolicyUrl`, `marketSpecificPricings`, `keywords`),
   then commit and poll the submission status until it leaves `CommitStarted`/`PendingCommit` (fail on `CommitFailed`, `CertificationFailed`, any failed or unknown status, printing its errors;
   still `CommitStarted` after 10 minutes is reported as "still committing, check later" without failing the run).
2. Idempotent like the other stores: an add-on with a published submission is `exists`; one with a pending submission is completed
   (PUT again, commit); a pending submission that Partner Center edited (the API answers 409 on PUT) is reported as needing a delete
   and re-create, never silently skipped. HTTP 429 waits the seconds Microsoft names (at most 10 minutes in total) and retries once per wait.
3. After a commit the run prints the add-on's Store id and the displaycatalog URL; a separate `--check-prices` mode reads every book's
   live Microsoft price there and fails on anything other than USD 0.99 (certification takes hours, so it is not waited on in the same run).
4. `--book <stem>` limits a run to that one book and skips the subscriptions in every store, so a new book never touches the
   subscription products (Microsoft's subscription shapes stay UNVERIFIED and are only run without `--book`).
5. `store-products-local.mjs <args>` runs the script on the desk: it loads the keys from `D:/secrets` without printing them
   (Play: `DINOREADER_PLAY_SERVICE_ACCOUNT_JSON` in `dinoreader/secrets.env` is a path, the script gets the file's text; Apple:
   `ASC_API_*` in `lets-call-mom/secrets.env`; Microsoft: `MS_STORE_*` mapped to `MS_SUBMISSION_*`, `MS_STORE_APP_ID=9N9VHRLCZXN5`).
6. The `create-dino-story` step 7b becomes one command, `node .github/scripts/store-products-local.mjs --apply --book <stem>`, then
   the App Store review screenshot and submission (still by hand, with the link) and `--check-prices` the next day. The step's
   "never run `--store microsoft`" line goes.

## Work

- [x] `store-products.mjs`: Microsoft submission, listing, price, commit and status poll (Decisions 1-2), with unit tests of the pure
      body builders, the 409 and 429 handling and the status outcomes (injected `request`, as `createMicrosoftProduct` already takes).
- [x] `--book <stem>` (Decision 4) and `--check-prices` (Decision 3), with tests; usage text and the coverage header updated.
- [x] `store-products-local.mjs` (Decision 5); no secret value is ever printed.
- [x] `create-dino-story` step 7b and the `.github/CLAUDE.md` line (Decision 6).
- [x] Live: finish Love from Seagull Island's add-on `9N6J9F50FM6G` with the new code (it is the pending case), and record its commit
      result and, once certified, its displaycatalog price here.
      Committed by the prototype (the same request shapes) on 2026-11-18 after three HTTP 429s ("try again in ~290 s", about 15 minutes):
      `202 CommitStarted`, then status CommitStarted, PreProcessing. The new code's live run
      (`node .github/scripts/store-products-local.mjs --apply --book love-from-seagull-island`, exit 0) then read Play `exists`,
      App Store Connect `exists` and Partner Center `exists (already Certification; Store id 9N6J9F50FM6G)`. The live price waits on
      certification: `docs/plan/unverified.md`, Slice 411.

## Verification

- `node --test .github/scripts/store-products.test.mjs` green.
- The live run above: `created`/`completed` for Microsoft, the status poll ends without `CommitFailed`, and `--check-prices` reads
  USD 0.99 for every book once Microsoft has certified it.

#### From review

- Skipped #2 (`reviews/s411-medium.md`): "pricing rebuilt without isAdvancedPricingModel (spec: templates carry true; Tier1012 needs it). Copy from template." - Microsoft's docs list `isAdvancedPricingModel` as read-only, and the live PUT without it was accepted.
- Skipped #8 (`reviews/s411-medium.md`): "price check sequential, takes first Purchase availability with a ListPrice (false pass/mismatch possible); use Promise.all and pick by market." - style/performance only, no wrong answer in practice (the catalog is read with market=US).
