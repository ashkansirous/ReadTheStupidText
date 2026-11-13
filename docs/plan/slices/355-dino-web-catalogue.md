# Slice 355 — Dino: a public web page listing every store book, free or paid, and the price
**Product:** Dino

*Built 2026-11-13, merged as PR #1053 (31dc4c42), not yet verified on the live site (see Not yet verified).*

*Changed while building:* the price and switch reach the page through the existing `/api/app-config` (new `catalogue.price` field, apps ignore it), so `/api/stories` and the apps' parsing are unchanged. Review added: when the config is unknown the page shows no price label (never "Free"); Retry aborts the previous load; Cancel shows a "cancelled" message; the price is validated in the Worker including bidi and zero-width characters; the Android button goes to `./#platforms` like `site/dino/index.html`. Slice 346 is referenced by name only.

**Touches:** `site/dino/books.html` (new), `site/dino/index.html`, `worker/downloads.ts`, `worker/downloads.test.mjs`, `worker/appconfig.ts`, `worker/appconfig.test.mjs`, `worker/catalogue.test.mjs` (new), `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `worker/CLAUDE.md`

## Why

The user asked (2026-11-13, RefineScope) why the Dino store is built in every app, and whether it could be one web page
the apps open. The answer: the store is already one shared screen used by Android, iOS and Windows (about 1,450 lines:
`src/DinoReader.App/StorePage.xaml*`, `StoreClient.cs`); only purchases differ per platform. A web page inside the app
would still need the app for payment (Apple and Google require their own in-app purchase), downloading and importing
books, background updates and offline. It would also break the App Store answers "no WebView or browser" and
"unrestrictedWebAccess false" (`docs/store-listings/dino/appstore.md:61`). No earlier decision recorded why it is native;
it came from the design mock and the grown-up gate.

So the user chose to leave the apps' store screen as it is and add a separate public web page where anyone can see which
books exist, which are free, which are paid and how much.

## Decisions (user, 2026-11-13, RefineScope)

1. **The apps don't change.** No WebView, no change to `StorePage` or `StoreClient`.
2. **A public, read-only page on dinoreader.sirous.uk.** It shows every book in the store. Nobody can buy on the web.
   Each book shows "Get it in the app" links to the app's store pages.
3. **Price: one price written once in the server settings** (remote config, `worker/appconfig.ts`), e.g.
   "$0.99 / €0.99 / £0.99", shown next to every paid book. The real store prices don't exist yet (Slice 346 Part D) and
   the Worker cannot read them without each store's API, so this is a fixed display value the user changes in one place.
   Slice 346's rule "never a '$0.99' written in the app or the Worker" is about the in-app price, which stays the
   store's own localised string; this setting is only for the website.
4. **Free or paid follows the paid-books switch.** While `paidBooks.enabled` is off (today), the page shows every book as
   free, matching what the apps do. Once it is on, books with `access: "paid"` show the price; the four free books
   (Slice 346 decision 2) show "Free".
5. **English first, then all languages in the same slice.** The site is English only today (`site/dino/index.html`,
   `<html lang="en">`). Book titles and credits already come translated from `/api/stories`; the page's own text
   (headings, "Free", "Get it in the app") is translated as the second part.

## Work

### Part A — the English page, end to end

- [x] `site/dino/books.html`: a static page in the site's existing style that fetches `/api/stories` (same origin, no
      third-party requests: no fonts, scripts or analytics from outside, `worker/CLAUDE.md`) and shows, per book: the
      cover (`/api/stories/<id>/cover`), title, ages, page count, minutes, and "Free" or the price.
- [x] Price and switch reach the page: add a `catalogue.price` text field to the remote config in
      `worker/appconfig.ts` (default "$0.99 / €0.99 / £0.99") and expose it, together with `paidBooks.enabled`, to the
      page (in the `/api/stories` response or a small same-origin JSON the page reads; pick the one that keeps the apps'
      parsing unchanged). Tests in `worker/appconfig.test.mjs`.
- [x] A loading state while the list loads (over 500 ms), and an error state with a Retry button if it fails.
- [x] "Get it in the app" links: Microsoft Store `https://apps.microsoft.com/detail/9N9VHRLCZXN5`; Android and iPhone
      use the links `site/dino/index.html` uses today (Play testing opt-in and TestFlight, lines 184 and 190) until
      public listings exist, then the public links.
- [x] Route `/books` to `/books.html` in `worker/downloads.ts` (like `/stats` at ~line 259), with its `/dino` twin in
      both `wrangler.jsonc` and `wrangler.workers-dev.jsonc` if it needs `run_worker_first` (`check-configs` enforces
      the twin). Covers under the workers.dev host need the `/dino` prefix.
- [x] Link the page from `site/dino/index.html`.
- [x] Tests: `worker/downloads.test.mjs` (route) and `worker/catalogue.test.mjs` (free/paid/price shown right with the
      switch off and on). Site files stay LF (CRLF is refused at deploy).

### Part B — every language

- [x] Translate the page text into every language the books already carry, picked from the browser's language with a
      language chooser on the page; book titles and credits use the translated `titles` and `creditsByLanguage` from
      `/api/stories`. Right-to-left languages (Farsi, Arabic) lay out right to left.
- [x] Tests for language choice and fallback to English.

### Part C — docs

- [x] `worker/CLAUDE.md`: one line saying the page exists, where the price comes from, and that it follows
      `paidBooks.enabled`.

## Verification

- `npm ci && npm run typecheck` and the worker tests pass (`worker/*.test.mjs`).
- After merge (Workers Builds deploys): open `https://dinoreader.sirous.uk/books` and the workers.dev `/dino/books`
  copy; every book from `/api/stories` is listed with its cover; with the switch off all show "Free"; the browser's
  network panel shows no request to any other host.
- With the switch on (once Slice 346 Part D is done): the four free books show "Free", the rest show the configured price.
- No app changes, so no device runs.

## Out of scope

- Buying on the web, accounts, wish lists.
- Fetching real per-country prices from the three stores (possible later, after Slice 346 Part D, with store API keys).
- Any change to the apps' store screen.

## From review

- Skipped #3 (`reviews/s355-medium.md`): "chooseLanguage's history.replaceState drops url.hash; every language change rebuilds all cards and covers." - cosmetic.
- Skipped #4 (`reviews/s355-medium.md`): "Relative links ("./", "privacy", "./#platforms") break if the page is served at a trailing-slash path." - the Worker already redirects `/books/` and `/books.html` to `/books` on the Dino host and `/dino/books/` to `/dino/books` on workers.dev (`worker/downloads.ts` lines 336 and 340), so the page is never served at a trailing-slash path.
- Skipped #5 (`reviews/s355-medium.md`): "Each Dino-only page is hard-coded in PAGE_FOR_PATH, canonical redirects and both wrangler run_worker_first lists." - follows the existing /stats pattern; deduping is a refactor for its own sake.

- Skipped #3, test-coverage part (`reviews/s355-high.md`): "load(), cancel, retry, slow spinner, 15 s timeout and paint code are outside the PURE block, so no test runs them" - only the Cancel message was applied; the DOM glue stays untested (no DOM test harness in the repo, checked on the live page after merge).
- Skipped #4 (`reviews/s355-high.md`): "Default price and price validation are written twice (worker/appconfig.ts and the page's readConfig)" - refactor for its own sake; the Worker default stays the source and the page copy only matters when the served config has no usable price.

*Review tally:* `reviews/s355-high.md` (7 findings): applied 1, 2, 5, 6, 7 and the Cancel part of 3; 3 (tests) and 4 not applied, lines above. `reviews/s355-medium.md` (5 findings): applied 1 and 2; 3, 4, 5 not applied, lines above. `reviews/s355-check.md` (7 low findings): none applied, lines below.

- Skipped #1 (`reviews/s355-check.md`): "readConfig lacks the Worker's length cap and control-character check." - the Worker already validates the price at the source.
- Skipped #2 (`reviews/s355-check.md`): "DEFAULT_PRICE duplicates DEFAULT_CATALOGUE_PRICE in worker/appconfig.ts." - refactor for its own sake (same as high #4).
- Skipped #3 (`reviews/s355-check.md`): "footer store links hard-coded; Android button goes to ./#platforms, English only." - the Android link to #platforms is the earlier applied fix; the store links reuse `site/dino/index.html`'s.
- Skipped #4 (`reviews/s355-check.md`): "language change via replaceState drops the URL hash." - cosmetic, same as medium #3.
- Skipped #5 (`reviews/s355-check.md`): "PURE-START/END marker slicing in tests is fragile; DOM code untested." - no DOM test harness in the repo.
- Skipped #6 (`reviews/s355-check.md`): "each Dino-only page adds per-page special cases in canonicalPath." - refactor for its own sake (same as medium #5).
- Skipped #7 (`reviews/s355-check.md`): "price cap counts UTF-16 units; Cf regex rejects ZWJ emoji prices." - a ZWJ emoji price is not a real input.

*Verification run:* worker tests 362/362, typecheck pass. No device runs (no app changes).

#### For CLAUDE.md (lifted 2026-11-13)

- `site/dino/books.html`: the page's pure script block (price/switch labels, language choice, titles) sits between `PURE-START` and `PURE-END` markers; `worker/catalogue.test.mjs` runs it by slicing the page between them, so keep new logic you want tested inside the markers and do not remove them. The page's DOM glue (load, Cancel, Retry) is not tested. (Slice 355: docs/plan/slices/355-dino-web-catalogue.md)
- The page's price is only `catalogue.price` from `/api/app-config`, validated in `worker/appconfig.ts`; when the config cannot be read the page shows no price label, never "Free". (Slice 355: docs/plan/slices/355-dino-web-catalogue.md)

#### Not yet verified

- Live `/books` and the workers.dev `/dino/books` after the batched release deploys: covers load, no third-party request, loading/Cancel/Retry work, and with the switch on the price shows only on paid books.
- Native read of the 14 non-English translations of the page text.
- Switched-on check after the paid books store setup (Slice 346 Part D).
