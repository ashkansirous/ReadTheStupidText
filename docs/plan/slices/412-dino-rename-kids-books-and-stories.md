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

## Work items

1. [ ] **Popularity table (first step, milestone 1).** Apple Search Ads popularity (or a free checker such as
   appdrift.co keyword difficulty) for: kids books, books for kids, bedtime stories, stories for kids, kids stories,
   children's stories, read aloud, picture books, storybook. Storefronts GB and US. One table in this file. Try it through
   a tool first (`web-research`, then the signed-in Chrome only if the user says so in that turn); Chrome hung on
   Google Trends and appdrift.co on 2026-11-18. **If "bedtime stories" is far ahead of "kids books" in GB, raise it with
   the user before item 6 ships** (the phone doc's acceptance line). Swap the weakest keyword-field words if the scores say so.
2. [ ] **App name.** One MSBuild property for the public name (e.g. `DinoPublicName` = "Dino: Kids Books & Stories" in
   `DinoReader.App.csproj`), `ApplicationTitle` = "Dino" (icon label). Windows `Package.appxmanifest` `DisplayName`
   (package and application) = the full name, since the Start menu shows it and it must equal the reserved Partner Center
   name. Confirm with context7 / `library-docs` how MAUI maps `ApplicationTitle` to the Android label, iOS
   `CFBundleDisplayName` and the Windows manifest before editing, so the Windows head doesn't fall back to "Dino".
3. [ ] **Name guard.** `tools/dino/check-public-name.mjs` reads the public name from the new property, not `ApplicationTitle`;
   "Dino Book Reader" joins "Dino Reader" as an old name that must not appear in public files; scan the store-listing
   files too. Tests in `tools/dino/public-name.test.mjs`. Update the rule line in `src/Dino/DinoReader.App/CLAUDE.md`.
4. [ ] **In-app strings.** `App_Name` and every string that names the app, in all 15 `AppResources*.resx`, say the new
   name (in-app text uses the English brand name in every language, as today).
5. [ ] **Store tooling.** `store-listings.test.mjs` fixtures, `store-products.mjs` constants (`APPLE_DESCRIPTION`,
   `APPLE_GROUP_NAME`, `SUBSCRIPTION_TITLE`, the subscription group localization, Play book descriptions) and their
   tests, the subscription listing files, `.claude/skills/store-listing/SKILL.md:66`, the Microsoft keyword limit.
6. [ ] **English listing on every store (milestone 2).** `appstore.md` (name, subtitle, keywords, description opening),
   `play.md` (title, short description, full description opening with the search phrases, "15 languages"),
   `microsoft.md` (title, 7 keywords). Push through the `store-listing` agent in apply mode and read back equal. Play:
   check the Publishing overview for unrelated pending changes before committing the edit (one pending draft per app).
   iOS name and subtitle go with the next version submission.
7. [ ] **Microsoft Store rename.** User step: reserve "Dino: Kids Books & Stories" (Partner Center, *Manage app names*
   for 9N9VHRLCZXN5: https://partner.microsoft.com/en-us/dashboard/products/9N9VHRLCZXN5/overview). If the name is
   taken, stop and ask. Then the manifest change ships in the next Windows submission; the old name can be deleted once
   the new one is published.
8. [ ] **Sonos.** `worker/sonos.ts` `ALBUM_NAME`/`ARTIST_FALLBACK` and its test, Slice 283's *Submission text*, the
   logo and banner wordmarks (`make-wordmarks.mjs`). Portal service name (user step, Sonos portal, as in Slice 283):
   the full name, or "Dino Kids Stories" if the portal refuses its length; write the limit found into this file.
9. [ ] **Alexa.** `skill.json` name (en-GB, en-US) "Dino Bedtime Stories", both `interactionModels` `invocationName`
   "dino bedtime stories", `worker/alexa.ts:72` welcome line, tests, Slice 282's flow lines ("Alexa, open Dino Bedtime Stories").
10. [ ] **Website, privacy, docs, credits.** `site/dino/*` (all 15 language blocks), `PRIVACY.md` and
    `site/dino/privacy.html`, `README.md`, `STORE.md` (name lines; keep engineering prose about the code name), the
    credits in `content/dino/*/book.json` (book version bumps and store upload per the Dino store rules, since the
    credits are book content).

## Verification

- `node tools/dino/check-public-name.mjs` passes and fails on a planted "Dino Book Reader".
- Store listings read back equal on Play, App Store (after the version submission) and Microsoft Store.
- Android emulator: the launcher label reads "Dino"; the back dialog and version card show the new name.
- Windows (VM, only if the manifest change needs it): Start menu and title bar show "Dino: Kids Books & Stories".

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
