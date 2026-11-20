# Slice 423 — Dino: short links `/book/<book-id>` that printed books point at

**Product:** Dino

*Built 2026-11-20, merged as PR #1321 (1fb9678); verified live 2026-11-20.*

*Planned 2026-11-20 with the owner, from print-the-damn-book's KDP robot planning (RefineScope answer 4, "yes";
print-the-damn-book Slice 40: docs/plan/slices/40-kdp-series-and-cross-links.md in that repo).*

**Touches:** `worker/index.ts`, `worker/book-links.ts` (new), `worker/book-links.test.mjs` (new), `wrangler.jsonc`,
`wrangler.workers-dev.jsonc`, `worker-configuration.d.ts`, `docs/plan/slices/423-dino-book-short-links.md`

Claim-time premise check (2026-11-20, decided by the coordinator; these replace the catalogue lookup in Decisions):
- **No catalogue lookup** (no exported list exists, and an R2 read on every scan of a printed code costs for nothing).
  The id is checked by its characters only: lowercase `a-z`, digits and `-`, 1-64 characters. A valid id forwards to
  `<BOOK_LINK_TARGET>?book=<id>`; any other id forwards to `<BOOK_LINK_TARGET>` with no `?book=`. Never a 404.
- `/book/*` goes into `run_worker_first` in `wrangler.jsonc`, and into `wrangler.workers-dev.jsonc` with its
  `/dino/book/*` twin (`worker/check-configs.mjs` enforces both); `BOOK_LINK_TARGET` goes into both `vars` blocks, and
  `worker-configuration.d.ts` is regenerated with `npm run types`.
- The handler sits in `serveAsset` (`worker/index.ts`) after `resolveSite` and before `env.ASSETS.fetch`; a test proves
  `canonicalRedirect` leaves `/book/...` alone on the Dino host. Checks: `npm ci && npm run typecheck` and
  `node --test worker/*.test.mjs` (worker/CLAUDE.md, `.github/workflows/site-checks.yml`).

**Migrations:** none

#### Why

The owner's paperbacks on Amazon print QR codes and an "Also by" page that lead readers to the Dino app. A printed code
can never change, and the owner may buy a new domain for the app later. So books print a short link on this site,
`https://dinoreader.sirous.uk/book/<book-id>`, and only the place it forwards to ever changes.

#### Decisions

- Approved by the owner, 2026-11-20: books print `https://dinoreader.sirous.uk/book/<book-id>`, forwarding to the Dino
  app page; a future domain changes only the forward.
- Decided by this planning session, 2026-11-20: the forward is a `302` (temporary, so browsers and Amazon's link checks
  never cache it for good). The target is one setting in `wrangler.jsonc` `vars` (`BOOK_LINK_TARGET`, today
  `https://dinoreader.sirous.uk/`); `<book-id>` is the `content/dino/<id>` folder name and is checked by its characters
  only, with no catalogue lookup (claim-time decision above; an unknown id still forwards, never a 404, because a printed
  book can outlive a catalogue entry). The book id is passed on as `?book=<id>` so a later app page can open that book. No cookies, no logging beyond
  what the Worker already records.

#### Work

- [x] A. `worker/book-links.ts`: build the forward from the path and `BOOK_LINK_TARGET`; route `/book/*` on the Dino host
  in `worker/index.ts` before the site assets.
- [x] B. Tests: known id, unknown id, odd characters in the id (never echoed unescaped), trailing slash, the
  readthestupidtext host is not affected.

#### Verification

- Unit tests above.
- Live `[session]` after the release: `curl -sI https://dinoreader.sirous.uk/book/love-from-seagull-island` shows `302`
  and `location: https://dinoreader.sirous.uk/?book=love-from-seagull-island`.

#### Changed while building

- `worker/book-links.ts`: the character check runs after one guarded decode. An invalid or missing `BOOK_LINK_TARGET`
  falls back to the request origin root. `serveAsset` in `worker/index.ts` calls it on the Dino host after
  `canonicalRedirect`/`resolveSite`, before the story routes and `ASSETS`. `/book/*` and `/dino/book/*` are in
  `run_worker_first` of both wrangler files; `worker-configuration.d.ts` was regenerated with `npm run types` (main's
  copy was stale, so the generated diff is large).
- Checks: 462 tests pass (`node --test worker/*.test.mjs`), typecheck green.

*From review:* (`reviews/pr-1321-high.md` 5/5 read, `reviews/pr-1321-medium.md` 4/4 read)
- High: applied 1, 2, 3 and 5.
- Skipped #4 (`reviews/pr-1321-high.md`): "Slice file contradicts itself" - the slice-file record is the work done in this pass.
- Skipped #1 (`reviews/pr-1321-medium.md`): "Percent-encoded slash decoded before trailing-slash strip" - an id with an encoded slash still forwards to the app page, harmless.
- Skipped #2 (`reviews/pr-1321-medium.md`): "Invalid-target fallback to request origin" - only when the var is invalid and on workers.dev, which no printed code uses.
- Skipped #3 (`reviews/pr-1321-medium.md`): "bare forms fall to assets and give a 404" - bare `/book` is never printed.
- Skipped #4 (`reviews/pr-1321-medium.md`): "Routing-order test reads worker/index.ts as text" - a runtime test needs `index.ts` refactored for node; the live curl check covers it.

#### For CLAUDE.md (lifted 2026-11-20)

- (`worker/CLAUDE.md`) `/book/<id>` short links are printed in books and must never 404 or change their path; only `BOOK_LINK_TARGET` changes. (Slice 423: docs/plan/slices/423-dino-book-short-links.md)

#### Not yet verified

_(none: the live check passed 2026-11-20 after the release; `curl -sI https://dinoreader.sirous.uk/book/love-from-seagull-island`
returned `HTTP/1.1 302 Found` and `Location: https://dinoreader.sirous.uk/?book=love-from-seagull-island`.)_
