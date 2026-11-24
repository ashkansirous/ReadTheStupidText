# Slice 440 — Dino: search on Sonos, "Stories" and "All languages"
**Product:** Dino

*Planned 2026-11-24.*

**Depends on:** Slice 437 (labels, art, ids).

**Touches:** `worker/sonos.ts`, `worker/sonos-soap.ts`, `worker/sonos.test.mjs`, `worker/sonos-search.ts` (new),
`worker/sonos-search.test.mjs` (new), `site/dino/sonos/presentation-map.xml` (new),
`site/dino/sonos/strings.xml` (new), `worker/index.ts`, `docs/plan/slices/283-dino-sonos-music-service.md`, this file.

## Why

Owner's brief, 2026-11-24 (verbatim): "Slice D, search: SMAPI search with two categories, "Stories" (titles in the
current language) and "All languages" (every title, language in the subtitle). Needs the presentation map Slice 283 left
out. Check the docs for how categories are declared."

On origin/main: no `search` action, no search category and no presentation map (`worker/sonos.ts`, unsupported actions
fault at :243-244). Slice 436's reading of https://docs.sonos.com/docs/search: categories are declared in the
presentation map (mobile) and through `getMetadata` on the search container (desktop).

## Decisions

- Owner's brief: two categories, "Stories" (titles in the current language: the saved story language when linked, else
  the root's language) and "All languages" (every title in every language, the language name in the subtitle).

## Design

From `docs/design/sonos/EN-Search.dc.html`: box "Search stories"; chips "Stories" (selected) and "All languages"; results
are book rows as in Slice 437 ("The Three Billy Goats Gruff" / "Ages 3–5 · 6 min · In the Dino app").

## Work

- [ ] D1. Read docs.sonos.com (`web-research`): the presentation map's `SearchCategories` and string table format,
      where its URL and version are set in the portal, the `search` request (id, term, index, count) and the desktop
      search container. Quote the lines here.
- [ ] D2. `site/dino/sonos/presentation-map.xml` and `strings.xml` (category names in the 15 languages), served by the
      Worker; owner step: set the presentation map URL and version in the Sonos portal (deep link in this file).
- [ ] D3. `worker/sonos-search.ts`: match titles (case and accent folded, `Intl.Collator` base sensitivity) per category;
      `worker/sonos-soap.ts` routes `search`; the search container in `getMetadata`.
- [ ] D4. Tests: both categories, a Farsi term, no results, paging.

## Verification

- `node --test worker/*.test.mjs`; owner's sandbox: searching "goat" in the S21 Sonos app gives the two English rows of
  the mock under Stories, and every language's match under All languages.
