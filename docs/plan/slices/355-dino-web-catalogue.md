# Slice 355 — Dino: a public web page listing every store book, free or paid, and the price

*Planned.*

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

- [ ] `site/dino/books.html`: a static page in the site's existing style that fetches `/api/stories` (same origin, no
      third-party requests: no fonts, scripts or analytics from outside, `worker/CLAUDE.md`) and shows, per book: the
      cover (`/api/stories/<id>/cover`), title, ages, page count, minutes, and "Free" or the price.
- [ ] Price and switch reach the page: add a `catalogue.price` text field to the remote config in
      `worker/appconfig.ts` (default "$0.99 / €0.99 / £0.99") and expose it, together with `paidBooks.enabled`, to the
      page (in the `/api/stories` response or a small same-origin JSON the page reads; pick the one that keeps the apps'
      parsing unchanged). Tests in `worker/appconfig.test.mjs`.
- [ ] A loading state while the list loads (over 500 ms), and an error state with a Retry button if it fails.
- [ ] "Get it in the app" links: Microsoft Store `https://apps.microsoft.com/detail/9N9VHRLCZXN5`; Android and iPhone
      use the links `site/dino/index.html` uses today (Play testing opt-in and TestFlight, lines 184 and 190) until
      public listings exist, then the public links.
- [ ] Route `/books` to `/books.html` in `worker/downloads.ts` (like `/stats` at ~line 259), with its `/dino` twin in
      both `wrangler.jsonc` and `wrangler.workers-dev.jsonc` if it needs `run_worker_first` (`check-configs` enforces
      the twin). Covers under the workers.dev host need the `/dino` prefix.
- [ ] Link the page from `site/dino/index.html`.
- [ ] Tests: `worker/downloads.test.mjs` (route) and `worker/catalogue.test.mjs` (free/paid/price shown right with the
      switch off and on). Site files stay LF (CRLF is refused at deploy).

### Part B — every language

- [ ] Translate the page text into every language the books already carry, picked from the browser's language with a
      language chooser on the page; book titles and credits use the translated `titles` and `creditsByLanguage` from
      `/api/stories`. Right-to-left languages (Farsi, Arabic) lay out right to left.
- [ ] Tests for language choice and fallback to English.

### Part C — docs

- [ ] `worker/CLAUDE.md`: one line saying the page exists, where the price comes from, and that it follows
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
