# Slice 412 — Dino: renamed "Dino: Kids Books & Stories" everywhere, with the English store listing parents search for

**Product:** Dino

Source: phone scope "Dino: store name, subtitle and keywords that parents search for" (2026-11-18) — https://drive.google.com/file/d/1pSYzLLe3EkJs7rU6VWhTJ6reKgFn_QZ5/view?usp=drivesdk

**Touches:** `src/Dino/DinoReader.App/DinoReader.App.csproj`, `src/Dino/DinoReader.App/Platforms/Windows/Package.appxmanifest`,
`src/Dino/DinoReader.App/Platforms/iOS/Info.plist` (only if the icon label needs it), `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx` (`App_Name` and lines that say the name),
`src/Dino/DinoReader.App/CLAUDE.md`, `tools/dino/check-public-name.mjs`, `tools/dino/public-name.test.mjs`,
`docs/store-listings/dino/appstore.md`, `docs/store-listings/dino/play.md`, `docs/store-listings/dino/microsoft.md`,
`docs/store-listings/dino/*-subscription.md`, `docs/store-listings/dino/subscription-listings.md`,
`.github/scripts/store-listings.mjs`, `.github/scripts/store-listings.test.mjs`, `.github/scripts/store-products.mjs`,
`.github/scripts/store-products.test.mjs`, `.claude/skills/store-listing/SKILL.md`, `worker/sonos.ts`, `worker/sonos.test.mjs`,
`worker/alexa.ts`, `worker/alexa*.test.*`, `alexa/skill-package/**`, `docs/design/dino-reader/sonos/*` (wordmarks),
`site/dino/**`, `PRIVACY.md`, `README.md`, `STORE.md`, `content/dino/*/book.json` (credits), `docs/plan/slices/282-dino-alexa-kid-skill.md`,
`docs/plan/slices/283-dino-sonos-music-service.md`

## Hard constraint (user, 2026-11-18): only text and links change, never the app's identity

"The app's file name uk.sirous.dino… should not change. I don't want to wait another 2 weeks on Google cause it might
think it is a new app, nor on App Store. It should just be updating text and links."

Never change, in this slice or any slice it spawns: the Android `ApplicationId` / package name `uk.sirous.dinoreader`,
the iOS bundle id, the App Store app id `6825053435`, the Play app, the Microsoft Store product id `9N9VHRLCZXN5` and the
Windows package identity (`<Identity Name>` and publisher in `Package.appxmanifest`), signing, version numbering, product
ids of book purchases and subscriptions, the domain. Only display text changes (store name fields, the manifest
`DisplayName`, `ApplicationTitle`, strings) and links that show the old name (e.g. the App Store URL slug
`dino-book-reader` in the site; the id in each link stays). A reviewer flags any diff line that touches one of the
identity fields above as a blocker. Every update ships as an ordinary update of the existing app.

## Hard constraint (user, 2026-11-18): nothing touches Google Play until Dino's first production release is out

"The work is not allowed to touch the android release to production until I have the first version out. I am already
waiting for 3 days and I don't want that to be reset."

Play keeps one pending edit per app, and any `edits.commit` (a listing push or a CI upload) sends everything pending to
review and cancels the submission already waiting (Play Console notes, 2026-11-14). So, until the Play production
track (https://play.google.com/console/u/0/developers/5892509739539100062/app/4974567301865424326/tracks/production)
shows Dino's first production release published:

- No Play listing push (item 6's Play part waits; the App Store and Microsoft parts don't).
- No merge of this slice's app-code changes (items 2 and 4), because a merge runs `release-dino.yml`, which uploads the
  Android build to Play. Those items are built and reviewed on their branch and merged only after the release is out.
- Docs, tooling, Worker, site, Alexa and Sonos items (3, 5, 8, 9, 10) don't make a Play upload and may merge before.
  Check `release-dino.yml`'s path filters before merging any of them; if one would upload Android, it waits too.

Before each Play-touching step, read the production track's state through the Play Developer API (read-only `edits`
insert + `tracks.get`, then delete the edit without committing) and write the result here. "Published" = a production
release with status `completed`.

## Why

The store name "Dino Book Reader" and the iOS subtitle "A voice for every character" hold none of the words parents
type into the stores, so Dino can't rank for its own category, and "book reader" reads as an e-reader (ReadEra, FBReader,
an existing "DinoReader" ebook app). Success: Dino shows up for "kids books", "bedtime stories", "stories for kids" and
"read aloud" in the UK and US storefronts, and a parent who reads the name and subtitle can say what the app does.

## Decisions

From the phone doc's *Decisions (approved)* (user, 2026-11-18), unchanged:

- **Store name** (App Store name, Google Play title, Microsoft Store display name): **"Dino: Kids Books & Stories"** (26 chars).
- **iOS subtitle:** **"Bedtime Tales in Your Voice"** (27 chars).
- **Play short description:** "Narrated bedtime stories & picture books for kids. Record your own voice too." (77 chars).
- **iOS keyword field:** `storybook,storytime,read,aloud,audiobook,picture,narrated,toddler,preschool,children,fairy,bilingual`
  (100 chars; no word repeated from the name or subtitle). Claude may swap the weakest words once the popularity scores are in.
- **Microsoft Store:** reserve the new name in Partner Center, change the package manifest display name, new submission.
  Keywords: `bedtime stories`, `kids books`, `stories for kids`, `read aloud`, `picture books`, `children's stories`, `storybook`.
- **Sonos service name:** "Dino: Kids Books & Stories"; "Dino Kids Stories" if the portal's length limit is shorter.
- **Alexa skill name and invocation name:** "dino bedtime stories" ("Alexa, open Dino Bedtime Stories"); second choice
  "dino story time". The store name breaks Alexa's invocation rules (no "and", lowercase letters, spaces and possessive
  apostrophes only), and an invocation name can't change after certification.
- **Play full description** opens with "bedtime stories for kids", "picture books", "read aloud" and "children's stories"
  and repeats them naturally (no keyword stuffing; Play suspends for it).

Made at the desk (user, 2026-11-18, this pickup):

- **The app itself is renamed too, not only the listings.** The phone doc kept the in-app name; the user overruled that:
  "the suggestion is to change the store name. why wouldn't we change the app name?" So "Dino: Kids Books & Stories" is
  the one public name everywhere a person sees it: stores, Windows Start menu and title bar, in-app strings, website,
  privacy policy, book credits, Sonos. The phone doc's "in-app naming out of scope" line no longer applies.
- **The label under the phone icon is "Dino"** (user picked it over the full name cut to "Dino: Kids B…" and over
  "Dino Stories", the name of another App Store app). Home-screen / launcher label only; everything else uses the full name.
- **Stays as is:** the code name "Dino Reader" in project names, namespaces, folders, bundle ids (`uk.sirous.dinoreader`),
  the domain `dinoreader.sirous.uk`, plan headings and tags (`dino-v…`). These are not seen by parents.
- **Story languages: 15.** The phone doc's "the app has 14" is wrong: the app ships fifteen languages
  (`DinoReader.App.csproj:84`, `play.md`, `appstore.md:41`); 14 is the App Store locale count (Farsi isn't an App Store
  locale, Slice 374). The Play description says 15.
- **Supersedes** the Batch 9 out-of-scope line "A store title change; only search terms and descriptions change"
  (`docs/plan/notes/out-of-scope-history.md:127`).

## Facts checked on origin/main (2026-11-18)

- Name sources today: `ApplicationTitle` "Dino Book Reader" (`DinoReader.App.csproj:24`, feeds the Android label and the
  iOS home-screen name); `Package.appxmanifest:20,55` `DisplayName`; `App_Name` in `AppResources*.resx` line 7 (Windows
  title bar `DinoTitleBar.cs:26`, welcome icon description `FirstRunPage.xaml.cs:162`, the "Close Dino Book Reader?" dialog,
  the version card).
- Slice 410's guard `tools/dino/check-public-name.mjs` takes the public name from `ApplicationTitle` and requires
  `worker/sonos.ts` `ALBUM_NAME`/`ARTIST_FALLBACK` and `PRIVACY.md` to carry it. With the icon label "Dino", `ApplicationTitle`
  can no longer be the public name, so the guard needs its own source (item 3).
- Old name also in: `.github/scripts/store-listings.test.mjs:35-83`, `store-products.mjs:76,314,864-866,1141` (+ tests),
  `.claude/skills/store-listing/SKILL.md:66`, `site/dino/index.html`, `books.html` (15 language blocks), `privacy.html`,
  `PRIVACY.md:133-204`, `README.md:29,152`, `STORE.md:815-830,862`, `content/dino/*/book.json` credits (14 languages),
  `worker/sonos.ts:22-23`, `worker/alexa.ts:72` ("Welcome to Dino Book Reader."), `alexa/skill-package/skill.json`
  and `interactionModels/custom/en-GB.json:4`, `en-US.json:4` (invocation "dino book reader").
- The phone doc's "Sonos and `worker/sonos.ts` still say Dino Reader" is out of date: Slice 410 already moved them to
  "Dino Book Reader", and the Sonos portal text was fixed by hand on 2026-11-18. The Sonos logo and banner wordmarks say
  "Dino Book Reader" and are regenerated with `tools/dino/wordmark/make-wordmarks.mjs`.
- Alexa: the skill package is in the repo, but the skill is not created in the developer console yet (Slice 282,
  *Waiting on the user*), so the invocation name is free to change.
- Microsoft keyword length: `store-listings.mjs:39` allows 30 chars per keyword; Microsoft's docs (phone doc, 2026-11-18)
  say 40. Check the docs (`web-research`) and fix the limit before pushing.
- iOS name and subtitle change only with a new version submission; App Store version 1.81.1 is not submitted yet
  (`appstore.md`), so the new name can ride on the next submission.

## Build split (desk, 2026-11-18, user: "implement the slices for android, windows and ios")

Play production for Dino was still not live on 2026-11-18 (mirror read, 1.131.1 not on production), so the slice ships in two parts:

- **Part A, `feat/412a-rename-text`, merges when green:** items 1, 3, 5, 6 (App Store and Microsoft only, no Play push), 8, 9, 10.
  None of its files is in `release-dino.yml`'s push `paths` (checked 2026-11-18; `content/dino/**` starts only `dino-store.yml`,
  which never talks to Play). The guard's public name moves to `tools/dino/public-name.json` (`{"publicName": ..., "iconLabel": "Dino"}`)
  instead of a csproj property, because a csproj edit starts `release-dino.yml` and so a Play upload; part B adds the csproj
  property and the guard then also checks that it equals the json.
- **Part B, `feat/412b-app-name`, built and reviewed, merged only after Play production shows `completed`:** items 2 and 4
  (csproj `DinoPublicName` + `ApplicationTitle` "Dino", Windows manifest `DisplayName`, the 15 resx files), and the guard check
  that the csproj property equals `tools/dino/public-name.json`. The Windows `DisplayName` also needs item 7 (the name reserved
  in Partner Center) before it can be submitted.
- Until Play production is live, run `store-products` (and `store-products-local`) only with `--store apple --store microsoft`: do not run its Play steps, because `SUBSCRIPTION_TITLE` feeds both stores.
- Slice 413 (13 languages) needs this slice's English listing and the Play hold, Slice 414 runs 4–6 weeks after go-live: neither is built now.

## Work items

1. [x] **Popularity table (first step, milestone 1).** Apple Search Ads popularity (or a free checker such as
   appdrift.co keyword difficulty) for: kids books, books for kids, bedtime stories, stories for kids, kids stories,
   children's stories, read aloud, picture books, storybook. Storefronts GB and US. One table in this file. Try it through
   a tool first (`web-research`, then the signed-in Chrome only if the user says so in that turn); Chrome hung on
   Google Trends and appdrift.co on 2026-11-18. **If "bedtime stories" is far ahead of "kids books" in GB, raise it with
   the user before item 6 ships** (the phone doc's acceptance line). Swap the weakest keyword-field words if the scores say so.
   *Tried 2026-11-18 (`web-research`): no GB or US score reachable without signing in.* Apple's popularity score needs an
   Apple Ads account, and appalize.com, appdrift.co, peekaso.app and AppTweak give numbers only through live JavaScript forms.
   The one stray figure (peekaso.app "bedtime stories" 76/100, storefront and date not stated) has nothing to compare against.
   So nothing shows "bedtime stories" far ahead of "kids books", and the approved name and keyword field ship unchanged. The
   table can be filled from the user's signed-in Chrome or Apple Ads account if they ask; Slice 414 re-tunes keywords from
   App Store Connect impressions anyway.
   *Closed 2026-11-24 (user: "do the other 2!"; `web-research` second pass plus one direct retry):* Google Trends answered
   HTTP 429 to both the agent and a direct `trends/api/explore` call for GB; AppGoblin answered 401; Astro and MobileAction
   have no public keyword pages. The only comparable figure: Appfigures keyword teardown #98 (US App Store, 2023-03-20,
   https://appfigures.com/resources/keyword-teardowns/98-books-for-kids) gives "books for kids" popularity 32 and says
   "bedtime stories" is "on par with" it.

   | Phrase | GB | US |
   | --- | --- | --- |
   | books for kids | not reachable | 32 (Appfigures, 2023) |
   | bedtime stories | not reachable | on par with "books for kids" (Appfigures, 2023, no number) |
   | kids books, stories for kids, kids stories, children's stories, read aloud, picture books, storybook | not reachable | not reachable |

   Nothing shows "bedtime stories" far ahead of "kids books", so the approved name and keyword field stay; real
   impressions decide the keyword swaps in Slice 414.
2. [x] **App name.** (Built: `DinoPublicName` added, `ApplicationTitle` = "Dino"; Android build label "Dino", Windows AppxManifest DisplayName is the full name; Windows manifest is a literal, MAUI does not overwrite it; iOS takes `ApplicationTitle` as CFBundleDisplayName, read from the iOS SDK targets, not built here.) One MSBuild property for the public name (e.g. `DinoPublicName` = "Dino: Kids Books & Stories" in
   `DinoReader.App.csproj`), `ApplicationTitle` = "Dino" (icon label). Windows `Package.appxmanifest` `DisplayName`
   (package and application) = the full name, since the Start menu shows it and it must equal the reserved Partner Center
   name. Confirm with context7 / `library-docs` how MAUI maps `ApplicationTitle` to the Android label, iOS
   `CFBundleDisplayName` and the Windows manifest before editing, so the Windows head doesn't fall back to "Dino".
3. [x] *Built (Part A, 2026-11-18): `tools/dino/public-name.json`, old names "Dino Reader" and "Dino Book Reader", English `docs/store-listings/dino/*.md` scanned (language files wait for Slice 413), tests, `public-name.yml` paths, CLAUDE.md rule lines. good-enough's book is skipped by a temporary `PENDING_PART_B_BOOKS` entry; Part B removes it.* **Name guard.** `tools/dino/check-public-name.mjs` reads the public name from the new property, not `ApplicationTitle`;
   "Dino Book Reader" joins "Dino Reader" as an old name that must not appear in public files; scan the store-listing
   files too. Tests in `tools/dino/public-name.test.mjs`. Update the rule line in `src/Dino/DinoReader.App/CLAUDE.md`.
4. [x] **In-app strings.** (Built: all 15 `AppResources*.resx`, 62 occurrences of "Dino Book Reader" replaced; none left.) `App_Name` and every string that names the app, in all 15 `AppResources*.resx`, say the new
   name (in-app text uses the English brand name in every language, as today).
5. [x] *Built (Part A, 2026-11-18): fixtures, constants, subscription title "Every book" and description (Apple's 30/45 limits), group display name, `SKILL.md`. `APPLE_GROUP_NAME` keeps its value: it is the live group's internal lookup name, never shown, and changing it would create a second group. Microsoft keyword limit left at 30: Microsoft's docs could not be found to confirm 40.* **Store tooling.** `store-listings.test.mjs` fixtures, `store-products.mjs` constants (`APPLE_DESCRIPTION`,
   `APPLE_GROUP_NAME`, `SUBSCRIPTION_TITLE`, the subscription group localization, Play book descriptions) and their
   tests, the subscription listing files, `.claude/skills/store-listing/SKILL.md:66`, the Microsoft keyword limit.
   Note (review #5): store-products' new subscription title and description apply only when a subscription is created, so the live subscriptions' text is pushed by the `store-listing` agent with item 6.
6. [ ] *Repo text half built (Part A, 2026-11-18): `appstore.md`, `play.md`, `microsoft.md` and the subscription files; nothing pushed.* **English listing on every store (milestone 2).** `appstore.md` (name, subtitle, keywords, description opening),
   `play.md` (title, short description, full description opening with the search phrases, "15 languages"),
   `microsoft.md` (title, 7 keywords). Push through the `store-listing` agent in apply mode and read back equal. Play:
   check the Publishing overview for unrelated pending changes before committing the edit (one pending draft per app).
   iOS name and subtitle go with the next version submission.
   *Pushed 2026-11-18 (`store-listing` agent, no Play call):* Microsoft en-us description, 8 features and the 7 search
   terms read back equal and were committed as submission 1152921505702441682 (same packages, pricing and visibility
   as the last published one; certification pending). Still open: (a) App Store name, subtitle, keywords and description
   wait for the next App Store version (only 1.131.1 READY_FOR_SALE exists; none was created). Before that version, merge
   the subscription paragraph and bullets of `appstore-subscription.md` into `appstore.md`, whose promo and description
   still carry the old "$0.99 each" text and would regress the live text. (b) Apple refused the subscription group
   display name and both subscription localizations with 409 "Cannot edit ... when it is in ACTIVE state"; retry with
   the next version. (c) Microsoft title: a new submission after the one in certification finishes. (d) Play: after the
   production release is out.
   *(d) done 2026-11-19 (`store-listing` agent; user: "dino reader is apparently live on google play. we can now roll
   out the new name"; Play API: production 1.131.1 `completed`):* Play edit 12117885717090361155 committed: title, short
   and full description on all 17 locales, read back equal in a fresh edit, no "Dino Book Reader" left; the subscription
   `uk.sirous.dinoreader.sub` (17 listings, "Every book") and the 12 book products ("in Dino: Kids Books & Stories")
   read back equal. Every full list of the 15 languages in `docs/store-listings/dino/` now runs by number of speakers
   (user, 2026-11-19: "the Fifteen languages should be sorted by number of people speeking them"); App Store and
   Microsoft carry that order with their next submission. (c) still open: Microsoft title with the next `store-submit`.
   *(a) done 2026-11-18 evening (`store-listing` agent; user: "isnt the task explained to you as change the name and
   keywords?"):* App Store version 1.133.1 (id 3afbd46d-d592-4fdb-a403-62eb93aaaff5) created from build 20261118.135720;
   en-US name, subtitle, keywords, promo and description read back equal. `appstore.md` now carries the live
   subscription wording. Submitted by `appstore-submit` after Slice 413's locales are on the same version.
   (b) retried: still 409 `ENTITY_ERROR.ATTRIBUTE.INVALID.UNMODIFIABLE` (group and both subscriptions APPROVED/ACTIVE);
   Apple allows no rename of an active subscription's display name here, so the old "Dino Book Reader" stays on them.
   **(b) closed 2026-11-24 (user: "I dont care about fucking apple subscription", "close the fucking apple subscription
   task"):** the old name stays on Apple's subscription group and subscriptions; never retried or raised again.
   The Dino ASC key in `D:\secrets\dinoreader` returns 401; the shared key in `D:\secrets\lets-call-mom` works.
   *(c) tried 2026-11-24 (`store-listing` agent, Partner Center submission API; `MS_STORE_*` in `D:\secrets\dinoreader\secrets.env` works for it):* last published submission 1152921505702472995 (1.136.5 packages, no other pending one) still has en-us and zh-hans title "Dino Book Reader" and no title field on the other 13 listings; thirteen descriptions still name "Dino Book Reader" or "Dino Reader". Created draft 1152921505702495765 (PendingCommit, NOT committed; deleted 2026-11-24 with a 204, no pending submission left, published one unchanged). A `PUT` with title "Dino: Kids Books & Stories" answers 200 and echoes it, but a `GET` of the draft still reads "Dino Book Reader" (same with the reserved name "Dino Reader", so the API does not persist titles); a localized title (zh-hans "Dino: ...") is refused 400, only the reserved names "Dino Reader", "Dino Book Reader", "Dino: Kids Books & Stories" pass. Title stays open: set it in Partner Center (Dino, Store listings, each language, Product name = the reserved name), then submit. The draft's `isAdvancedPricingModel` reads false after the PUT (published: true), priceId stays Free: check Pricing before committing.
   **Tooling gap (2026-11-24):** `.github/scripts/store-listings.mjs` leaves Dino's `en` and `de` listings unwritten and maps `zh`/`pt` to `zh-cn`/`pt-br` while the live submission names them `zh-hans`/`pt`, so a name change in the repo files never reaches the live Microsoft listings.
7. [ ] *Name reserved 2026-11-18 (desk, in the user's Chrome, user: "use the fucking chrome I approve it"): Partner
   Center lists "Dino: Kids Books & Stories" as "Reserved for this app" next to "Dino Book Reader" (in use) and "Dino
   Reader". Microsoft drops a reservation unless the product is submitted with it within three months (by 2027-02-18).
   Next: the Microsoft listing title, then the manifest `DisplayName` with part B.* **Microsoft Store rename.** User step: reserve "Dino: Kids Books & Stories" (Partner Center, *Manage app names*
   for 9N9VHRLCZXN5: https://partner.microsoft.com/en-us/dashboard/products/9N9VHRLCZXN5/overview). If the name is
   taken, stop and ask. Then the manifest change ships in the next Windows submission; the old name can be deleted once
   the new one is published.
8. [x] *Built (Part A, 2026-11-18): `sonos.ts`, tests, Slice 283 submission text, wordmarks regenerated from `public-name.json`. Portal service name and its length limit remain the user's step.* **Sonos.** `worker/sonos.ts` `ALBUM_NAME`/`ARTIST_FALLBACK` and its test, Slice 283's *Submission text*, the
   logo and banner wordmarks (`make-wordmarks.mjs`). Portal service name (user step, Sonos portal, as in Slice 283):
   the full name, or "Dino Kids Stories" if the portal refuses its length; write the limit found into this file.
9. [x] *Built (Part A, 2026-11-18): skill.json, both interaction models, welcome line, Slice 282 flow line; "dino bedtime stories" follows the Decisions' invocation rules (context7 holds no invocation-name rules).* **Alexa.** `skill.json` name (en-GB, en-US) "Dino Bedtime Stories", both `interactionModels` `invocationName`
   "dino bedtime stories", `worker/alexa.ts:72` welcome line, tests, Slice 282's flow lines ("Alexa, open Dino Bedtime Stories").
10. [x] *Built (Part A, 2026-11-18): site (incl. 15 language blocks), PRIVACY.md, README, STORE.md, credits of the two store books (Store books are not stamped; `dino-store.yml` uploads them). NOT done: good-enough's credits, because it is a starter: its version bump and rebuilt `good-enough.dino` live under `src/Dino/DinoReader.App` (release path); moved to Part B.* **Website, privacy, docs, credits.** `site/dino/*` (all 15 language blocks), `PRIVACY.md` and
    `site/dino/privacy.html`, `README.md`, `STORE.md` (name lines; keep engineering prose about the code name), the
    credits in `content/dino/*/book.json` (book version bumps and store upload per the Dino store rules, since the
    credits are book content).
    *Built (Part B, 2026-11-18, `feat/412b-app-name`): good-enough credits (English + 14 languages) changed, `stamp` v12 to v13, `build` rewrote
    `good-enough.dino`, `PENDING_PART_B_BOOKS` removed; the guard now also fails when csproj `DinoPublicName` differs from `publicName` or
    `ApplicationTitle` from `iconLabel` (3 new tests); the "Close ...?" code comments in `CloseQuestionPage.cs` and `GalleryPage.xaml.cs` and the
    naming note in `DinoReader.App/CLAUDE.md` updated. Identity untouched. Merges only after Play production shows `completed`.*

## Verification

- `node tools/dino/check-public-name.mjs` passes and fails on a planted "Dino Book Reader".
- Store listings read back equal on Play, App Store (after the version submission) and Microsoft Store.
- Android emulator: the launcher label reads "Dino"; the back dialog shows the new name. (The version card shows only
  "Version" and the number, `AboutPage.cs:46`, `UpdatePage.cs:158`: it never named the app, so it has nothing to check.)
- Windows (VM, only if the manifest change needs it): Start menu and title bar show "Dino: Kids Books & Stories".

*Part B device run, 2026-11-18 (emulator-5554, debug build of `feat/412b-app-name`):* launcher label PASS
(`aapt2 dump badging`: `application-label:'Dino'` in every locale); close dialog PASS (new Maestro flow
`.maestro/dino/app-name-close-dialog.yaml` waits for "Close Dino: Kids Books & Stories?", taps Stay, the shelf is back).

## Not yet verified

- iOS home-screen label reads "Dino" (read from the iOS SDK targets only; no Mac build here).
- Android launcher label as drawn on a real launcher (the APK label was read, not the home screen).
- Arabic and Farsi in-app name renders in the right order next to RTL text.
- Windows Start menu and title bar show the full name (VM run not done; the built AppxManifest DisplayName was read).

## Review (part B, `feat/412b-app-name`)

*From review:* files `reviews/s412b-high.md`, `reviews/s412b-medium.md`, `reviews/s412b-check.md` read in full; every
finding without a line below was applied (high #4 resolved itself: the name was reserved in Partner Center on 2026-11-18;
high #7 and #8 became Not yet verified lines).

- Skipped #5 (`reviews/s412b-medium.md`): "also feeds Android label and iOS CFBundleDisplayName" — that is the user's pick for the icon label; Android checked on the emulator, iOS is a Not yet verified line.
- Skipped #7 (`reviews/s412b-medium.md`): "full name h" — the exception messages name the app on purpose, now with the new name.
- Skipped #8 (`reviews/s412b-medium.md`): "VM checks match dialog and window text containing" — no VM run in this pass; a Not yet verified line covers Windows.
- Skipped #2 (`reviews/s412b-check.md`): "only the English AppResources.resx App_Name is checked" — every language file uses the English brand name; Slice 413 can widen the guard.
- Skipped #3 (`reviews/s412b-check.md`): "csprojProperty takes the first match" — the csproj has no conditional override of either property.
- Skipped #4 (`reviews/s412b-check.md`): "csproj tests hard-code live name strings" — low; a rename updates them with the json.
- Skipped #5 (`reviews/s412b-check.md`): "errors from findHits/csprojProblems are not caught" — low; a stack trace still fails the check.
- Skipped #6 (`reviews/s412b-check.md`): "comments quote the public name and can go stale" — cosmetic.
- Skipped #7 (`reviews/s412b-check.md`): "namespaced DisplayName is not matched" — the manifest uses no namespace prefix on DisplayName.

## Review (part A, PR #1278)

*From review:* files `reviews/s412a-high.md`, `reviews/s412a-medium.md`, `reviews/s412a-check.md`, `reviews/s412a-check2.md`, `reviews/s412a-check3.md`
read in full; every finding without a line below was applied (check2 #4: the listing page size went from 200 to 50).

- Skipped #3 (`reviews/s412a-high.md`): "the guard skips it and nothing fails when the skip should go" — part B fixes the good-enough credits and removes the skip in the same change.
- Skipped #4 (`reviews/s412a-high.md`): "until Part B nothing ties publicName to csproj ApplicationTitle" — part B adds that check; the json is committed.
- Skipped #6 (`reviews/s412a-high.md`): "SUBSCRIPTION_TITLE (store-products.mjs:865) feeds both Play and Apple bodies" — the Build split limits store-products to `--store apple --store microsoft` until Play production is live.
- Skipped #7 (`reviews/s412a-high.md`): "alexa/skill-package/skill.json:7 and docs/store-listings/dino/appstore.md:8, low" — the Alexa name is in this slice's Decisions (user, 2026-11-18: "dino bedtime stories"), and the 100-character keyword field was counted by the reviewer.
- Skipped #3 (`reviews/s412a-medium.md`): "PENDING_PART_B_BOOKS skips good-enough entirely" — part B removes it.
- Skipped #4 (`reviews/s412a-medium.md`): "exempts any future two-letter .md" — low; Slice 413 scans the language files.
- Skipped #5 (`reviews/s412a-medium.md`): "ApplicationTitle still old name (Part B)" — part B.
- Skipped #6 (`reviews/s412a-medium.md`): "shortened SUBSCRIPTION_TITLE/DESCRIPTION also feed Play" — as high #6.
- Skipped #2 (`reviews/s412a-check.md`): "PATCH uses the plain client, no retry on 429/5xx" — a throttled rename only warns and a rerun retries.
- Skipped #4 (`reviews/s412a-check.md`): "group matched only by exact referenceName and unpaginated" — pre-existing lookup, unchanged here.
- Skipped #7 (`reviews/s412a-check.md`): "fallback duplicated (also line 1325)" — nit.
- Skipped #1 (`reviews/s412a-check2.md`): "a failed read of the group's listing list" — pre-existing; a rerun recovers.
- Skipped #2 (`reviews/s412a-check2.md`): "a 200 non-JSON PATCH response counts as renamed" — cosmetic; a rerun retries.
- Skipped #3 (`reviews/s412a-check2.md`): "retryingAsc has no patch; works today" — the plain client is passed on purpose.
- Skipped #5 (`reviews/s412a-check2.md`): "two POST paths for the missing listing" — nit.
- Skipped #6 (`reviews/s412a-check2.md`): "live subscriptions' own listings keep the old title" — item 6's store-listing push sets them.
- Skipped #7 (`reviews/s412a-check2.md`): "new tests pass via the 404 on the missing POST" — low; the warning and en-US paths are asserted before that point.
- Skipped #8 (`reviews/s412a-check2.md`): "a rejected rename warns on every run" — cosmetic.
- Skipped #1 (`reviews/s412a-check3.md`): "Only matters past 50 locales" — Dino has about 15.
- Skipped #2 (`reviews/s412a-check3.md`): "rename PATCH is not retried" — a failed rename only warns; a rerun retries.
- Skipped #3 (`reviews/s412a-check3.md`): "appleRenameGroupListing branches have no direct unit test" — low; the throw, rejection and missing en-US paths are tested through ensureAppleGroup.
- Skipped #4 (`reviews/s412a-check3.md`): "duplicated POST path, log fallback, constant comment wording" — style.

## Out of scope

- The other 13 languages: Slice 413. Keyword tuning after 4–6 weeks: Slice 414.
- Spin-off: a first screenshot caption that says plainly what the app is (e.g. "Picture books read aloud – in your
  voice or ours"). Belongs to Slice 404's store-picture retake; that slice takes it up from this line.
- Spin-off: a 5–10 UK parent test of icon, name and subtitle ("what does this app do?"). The user's own, no slice.
- Renaming the repo, projects, namespaces, bundle ids or the domain.

## Risks

- "Bedtime stories" results are full of sleep apps and AI story generators; that's why "Bedtime" is in the subtitle,
  not first in the name.
- "Dino Stories" is an existing App Store app (Oliver Denman) and "DinoReader" an FBReader-based ebook app: the name
  avoids both.
- "Dino" alone can suggest dinosaur-only books: the descriptor and screenshots must show a range of stories.
- "Read aloud" sits in the keyword field, where it counts for less (accepted: no search numbers found for it).
