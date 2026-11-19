# Slice 417 — Dino: both web pages point at the live stores, show the new name and current screenshots; testing closed

**Product:** Dino

Source: user, 2026-11-19: "dino reader is apparently live on google play. we can now roll out the new name, screenshots,
tags and all the other stuff that was planned to change. at the same time, make sure you update https://dinoreader.sirous.uk/
and sirous.uk/dinoreader to have the latest updates, with proper screenshots, and link to the proper google play and ios
market. no testing anymore. also, close the google group and apple group that was open for people to join in" and
"the Fifteen languages should be sorted by number of people speeking them".

**Touches:** `site/dino/**`, `site/index.html`, `worker/check-site-eol.mjs`, `src/Dino/DinoReader.App/IosUpdateTarget.cs` (item 7),
`tests/Dino/DinoReader.Tests/IosUpdateTargetTests.cs` (item 7), `docs/plan/slices/417-dino-sites-after-launch.md`;
in the separate repo ashkansirous/sirous.uk: src/data/products.ts, src/assets/products/dino-reader*, astro.config.mjs (redirect), README.md.

## Facts checked 2026-11-19 (desk)

- Google Play production track for `uk.sirous.dinoreader`: release 1.131.1 (versionCode 1131001) status `completed`
  (Play Developer API, read-only edit, deleted without commit).
- App Store: `https://apps.apple.com/gb/app/dino-book-reader/id6825053435` is live (iTunes lookup, GB).
- Play store page `https://play.google.com/store/apps/details?id=uk.sirous.dinoreader` returns 200.
- `site/dino/index.html` and `books.html` still send people to the testers group
  (`https://groups.google.com/g/sirous-app-testers`), the Play testing opt-in (`/apps/testing/uk.sirous.dinoreader`)
  and the public TestFlight link `https://testflight.apple.com/join/WnF7M53D`.
- `https://sirous.uk/dinoreader` is 404; the Dino page is `https://sirous.uk/dino-reader/` (same three tester links).
- Read The Stupid Text's `site/index.html` also links the same Google group; RTST is on Play production too (1.106.0 completed).

## Decisions

- **No testing anywhere on either page.** Every TestFlight, testers-group and Play opt-in link and its "how to join" text
  goes; the buttons are the official store badges/links: Google Play `https://play.google.com/store/apps/details?id=uk.sirous.dinoreader`,
  App Store `https://apps.apple.com/app/id6825053435` (no name slug, so the rename never breaks it), Microsoft Store
  `https://apps.microsoft.com/detail/9N9VHRLCZXN5` (where the page already offers Windows).
- **The public name is "Dino: Kids Books & Stories"** (Slice 412) on both pages, titles and meta tags; the domain and
  URL slugs stay.
- **The fifteen languages are listed by number of speakers, most first** (user, 2026-11-19), total speakers (first plus
  second language), Ethnologue 2025 figures: English, Chinese, Spanish, Arabic, French, Portuguese, Russian, German,
  Japanese, Farsi, Dutch, Swedish, Danish, Finnish, Norwegian. Confirm the order of German/Japanese/Farsi and the four
  Nordic languages from one source before writing it; the same order everywhere the pages list them (including
  `books.html`'s language blocks if they are a visible list).
  **Figures used (2026-11-19, Wikipedia "List of languages by total number of speakers", Ethnologue 2026 table, millions
  L1+L2):** English 1,493; Mandarin 1,183; Spanish 561; Arabic (MSA) 335; French 334; Portuguese 269; Russian 210;
  German 133; Japanese 126; Persian 82. The table stops at 50M, so the five Nordic/Dutch figures come from each language's
  Wikipedia infobox: Dutch 30M total (L1 25M, 2021); Swedish about 13M (L1 10M + L2 3M); Danish 5.5M L1 (2012);
  Finnish 5.0M (infobox) ; Norwegian 4.32M L1 (2012). Order confirmed as listed above. The Danish/Finnish/Norwegian
  gaps are small and those are native-speaker counts, not Ethnologue totals.
- **Screenshots are the current app** (new name, full shelf): take them from `docs/store-listings/screenshots/dino/`
  (Slice 404's retake when it lands; until then the newest phone set there), never older pictures.
- **`sirous.uk/dinoreader` works**: it redirects to `/dino-reader/` (static redirect page or Astro `redirects`).
- **RTST's page drops the Google group step** (the group closes); its Play button goes to the public Play page
  `https://play.google.com/store/apps/details?id=uk.sirous.readthestupidtext`. RTST's own TestFlight link stays (RTST is not on the App Store).

## Work items

1. [x] `site/dino/index.html`, `books.html` (and any other `site/dino` page with tester links): store buttons, new name,
   language order, tester sections removed, hero/screenshots replaced with the current set (webp, sized like today's).
   Built: the five phone screenshots in `docs/store-listings/screenshots/dino/phone/` (shelf, reading, Farsi, record,
   grown-ups corner) as 720x1600 webp (the source is 1080x2400, so 1600 not 1520); the old picture-only, store and night
   shots (no current twin) were removed, so `worker/check-site-eol.mjs`'s required-file list changed (outside Touches).
   `books.html`'s language chooser uses `LANGUAGE_ORDER`. Slice 404's retake is not in the folder yet: swap the five files then.
2. [x] `site/index.html`: the testers-group step removed, Play button to the public page.
3. [ ] sirous.uk repo: the Dino product entry and page (name, description, store links, screenshots, language order),
   `/dinoreader` redirect; `npm run build` passes.
4. [ ] Apple: Dino's external TestFlight group public link switched off (App Store Connect API, `betaGroups`
   `publicLinkEnabled: false`), read back. Desk.
5. [ ] Google group `sirous-app-testers`: no API reaches a consumer Google group; the owner closes or deletes it at
   https://groups.google.com/g/sirous-app-testers/settings (owner step, link given).

6. [ ] Swap the site screenshots (`site/dino/*.webp` and the sirous.uk `src/assets/products/dino-reader*.webp`) for
   Slice 404's retaken English set (German shot 3) once it is in `docs/store-listings/screenshots/dino/` (review s417-high #4).
7. [ ] The in-app iOS update button (`src/Dino/DinoReader.App/IosUpdateTarget.cs:14`) still opens the public TestFlight
   link, now switched off (item 4): point it at the App Store page `https://apps.apple.com/app/id6825053435` for every
   channel (review s417-high #1). App code: its own branch and the full review gate.

## Not yet verified

- [chrome] Both live pages after deploy: store buttons open the right store pages, no tester link left.
