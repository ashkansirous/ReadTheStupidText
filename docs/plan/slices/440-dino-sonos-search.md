# Slice 440 — Dino: search on Sonos, "Stories" and "All languages"
**Product:** Dino

*Planned 2026-11-24.*

*Plan-cleanup check 2026-11-25, verdict keep open (not started).* Read on origin/main 1a8bf725: no `search` action, no
`worker/sonos-search.ts`, no `site/dino/sonos/` folder (`git grep -i search origin/main -- worker/sonos.ts worker/sonos-soap.ts`
is empty); Slice 437, the dependency, is still `planned` in plan.md. The user has not said anything about search or the
presentation map beyond the 2026-11-24 brief quoted below. D1-D4 are all this slice's own work and all open. The
owner step in D2 (set the presentation map URL and version in the Sonos developer portal) is an owner login step that
comes after D2 is deployed, not a wait inside the work; the portal deep link is not recorded in the repo (only the
support case link in Slice 283), so the builder must look it up before writing that step.

**Starts after:** Slice 437 merged (labels, art, ids).

**Depends on:** Slice 437 (labels, art, ids).

**Touches:** `worker/sonos.ts`, `worker/sonos-soap.ts`, `worker/sonos.test.mjs`, `worker/sonos-search.ts` (new),
`worker/sonos-search.test.mjs` (new), `site/dino/sonos/presentation-map.xml` (new),
`site/dino/sonos/strings.xml` (new), `worker/index.ts`, `worker/sonos-labels.ts`, `worker/sonos-shelves.ts` (both widened 2026-11-27), `docs/plan/slices/283-dino-sonos-music-service.md`, this file.

*Built 2026-11-27, merged as PR #1427 (15e95c82); not yet verified in the Sonos sandbox (owner). Code by cloud run `cse_012UYzYVV36EAj79kYDMG9hQ` (commit 3e068192); D1 (docs read) and the review fixes were done locally. Worker only, no device run.*

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

- [x] D1 (read 2026-11-27, quotes under *D1 result*). Read docs.sonos.com (`web-research`): the presentation map's `SearchCategories` and string table format,
      where its URL and version are set in the portal, the `search` request (id, term, index, count) and the desktop
      search container. Quote the lines here.
- [x] D2. `site/dino/sonos/presentation-map.xml` and `strings.xml` (category names in the 15 languages), served by the
      Worker; owner step: set the presentation map URL and version in the Sonos portal (deep link in this file).
- [x] D3. `worker/sonos-search.ts`: match titles (case and accent folded, `Intl.Collator` base sensitivity) per category;
      `worker/sonos-soap.ts` routes `search`; the search container in `getMetadata`.
- [x] D4. Tests: both categories, a Farsi term, no results, paging.

## Verification

- `node --test worker/*.test.mjs`; owner's sandbox: searching "goat" in the S21 Sonos app gives the two English rows of
  the mock under Stories, and under All languages every book whose OWN-language title contains "goat" (decided 2026-11-27: D3 matches each book's title in its own language only, so "goat" finds the English book, not the German "Die drei Ziegenböcke"; a book has one title per language and no cross-language title index exists, and a Sonos listener searching in one alphabet expects titles in it; searching "ziegenbock" finds the German one). A cross-language title index would be a new slice.

## Not yet verified

- `[owner]` Portal versus hosted presentation map: the current docs.sonos.com add-search page sets the search categories and their strings in the portal (Search Capabilities, Localization Resources) and does not mention a hosted presentation map, so `site/dino/sonos/presentation-map.xml` may be unused. The owner does the portal steps in D2 and says whether mobile search shows both categories; if it does not, the hosted map is not enough on its own.
- `[owner]` The owner step in D2 (Search Capabilities categories, Localization Resources and, if the portal has the field, the presentation map URL and version in the Sonos Developer Portal).
- `[owner]` The sandbox check ("goat" in the S21 Sonos app: two English rows under Stories, the own-language matches under All languages).
- `[session]` After deploy, `curl` that https://dinoreader.sirous.uk/dino/sonos/presentation-map.xml and https://dinoreader.sirous.uk/dino/sonos/strings.xml both load (HTTP 200, the XML bodies).

## For CLAUDE.md (lifted 2026-11-27)

- `worker/CLAUDE.md`, in the Slice 437 paragraph "Sonos ids carry the language; only `root` reads Accept-Language", after "never read the header below `root`.": **The one exception is search (Slice 440):** the `search` action and the `search` container use the fixed ids `stories` and `all-languages` (the presentation map's, so no language can ride in them), so they read Accept-Language for the words (`labelOfRequest`, English with no header) and, for an unlinked caller, the Stories language (the first accepted language with books, else the catalogue's first); a linked home's Stories language is its saved one. Tests: `worker/sonos-search.test.mjs`.

## D1 result (2026-11-27, `web-research`, raw `.md` of each docs page)

Quotes. https://docs.sonos.com/docs/search: params `id` string(128) "The ID of the search category.", `term` "The search term.", `index` "The zero-based index at which to start retrieving search results.", `count` "The number of search results requested."; "The response is identical to the getMetadata message."; sample map `<PresentationMap type="Search"><Match><SearchCategories><Category mappedId="artist" id="artists"/>...`; "Implement search for mobile devices by sending a list of searchable categories from the unified search configuration in the presentation map. Implement it for Windows and Mac OS by sending a list of categories in a response to a `getMetadata` request on the search container."; "The list of IDs ... should be identical."; "Sonos uses the ID for a given category as the category (ID) parameter to the search message." Desktop container: `getMetadata` with `<id>search</id>`, each entry `<mediaCollection><id>artist</id><title>Artist</title><itemType>search</itemType>`. https://docs.sonos.com/docs/getmetadata: "The `itemType` of each element in this list should be `search`." https://docs.sonos.com/docs/add-search (updated 2026-04-16): `CustomCategory` attributes `mappedId` ("The SMAPI identifier for the category passed to search. This is required.") and `stringId` ("The string entry from strings.xml used for the UI for the custom search category. This is required."); built-in `Category` takes `id` from a fixed list (All, artists, albums, ... tracks) plus `mappedId`; "Add text for search categories to the **Localization Resources** page"; categories are added "From the Search Capabilities page in your Integration Submission Form: Click the Add Custom Category button ... enter a Mapped ID".

What the docs changed:
- Kept (matches the docs): `CustomCategory stringId/mappedId` in `PresentationMap type="Search"` > `Match` > `SearchCategories`; `search` fields id/term/index/count; desktop container id `search` with `itemType` `search`, same ids as the map.
- Changed: `strings.xml` was a guessed `stringtable/stringtext language=` shape with bare language codes. docs.sonos.com has no string-table page; the only XML example is a Sonos community post (legacy format): `stringtables` > `stringtable rev xml:lang` > `string stringId`. Rewritten to that, with the region codes docs.sonos.com/docs/localization accepts (en-US, da-DK, de-DE, es-ES, fr-FR, ja-JP, nb-NO, nl-NL, pt-BR, sv-SE, zh-CN). ar, fa, fi, ru are not Sonos locales and left out of the file (the Worker still names the categories in them for the desktop `getMetadata`). The test now checks the new shape.
- Worker search code: no change needed; the ids (`stories`, `all-languages`) are the same in the map and `getMetadata`.
- Open conflict, not settled by the docs: the current add-search page says the categories and their strings are set in the portal (Search Capabilities page, and Localization Resources as an i18next JSON with `common`/`integration` sections per locale), and does not mention a hosted presentation map for search; the older `search` page still shows the map XML. So the hosted XML files may be unused by the portal.
- No docs page names a presentation map URL/version field, and no deep link with the integration id is documented (NOT READ; the portal needs a login).

Owner step (D2), after Slice 440 is deployed: open the sandbox integration in the Sonos developer portal, https://developer.sonos.com/s/ (the saved sandbox integration, applicationId `a6BPs000000qWNBMA2`, Slice 283; no exact page link is documented), go to the integration's Search Capabilities page, click Add Custom Category twice: title "Stories" with Mapped ID `stories`, and "All languages" with Mapped ID `all-languages`, then add the two strings to Localization Resources (`integration` section of each locale, text from `site/dino/sonos/strings.xml`), and if the portal has a presentation map URL/version field, enter `https://dinoreader.sirous.uk/dino/sonos/presentation-map.xml` (the site folder `site/dino/sonos/` as a static path; confirm it loads after deploy) and version 1. Then Refresh and Send.

#### Changed while building

- All languages matches each book's own-language title (decision already under *Verification*).
- Folding in `worker/sonos-search.ts`: case, accents, ß/æ/œ, ø/đ/ł and similar, Arabic tatweel; kana dakuten kept; collator `en`, accent-sensitive.
- Search ids `stories` and `all-languages` are language-free, so the `search` action and container read Accept-Language (see *For CLAUDE.md*).
- `strings.xml` covers the 11 Sonos locales; ar, fa, fi, ru are desktop-only (named by the Worker in `getMetadata`).

#### From review

Four findings files read in full (high 7/7, medium 4/4, check 3/3, check2 5/5).

- Applied (`reviews/s440/pr-1427-high.md`) #1 ("Search misses titles whose letters fold to a different length"), #2 ("The `search` action and the `search` container read the Accept-Language header below `root`": exception recorded in `worker/CLAUDE.md`, no-header test added), #3 (linked-home test), #4 (per-locale strings.xml test), #6 (term cut on code points), #7 (stale D1 text removed).
- Skipped #5 (`reviews/s440/pr-1427-high.md`): "`search()` builds a row for every hit before paging" - no Outcome line for it; the catalogue holds a few dozen books, so the cost is not measurable (same reason as medium #1).
- Applied (`reviews/s440/pr-1427-medium.md`) #3 (kana dakuten kept) and #4 (strings.xml locale guard).
- Applied (`reviews/s440/pr-1427-check.md`) #1 (ø and similar folded), #2 (tatweel removed), #3 (explicit collator locale).
- `reviews/s440/pr-1427-check2.md` has five nits after convergence, no code change; they are the follow-up lines below.
- Skipped #1 (`reviews/s440/pr-1427-medium.md`): "worker/sonos.ts:462, nit (CPU)." - the catalogue holds a few dozen books, so building rows before paging costs nothing measurable (same reason as high-pass #5).
- Skipped #2 (`reviews/s440/pr-1427-medium.md`): "worker/sonos-search.ts:90, nit (CPU)." - folding one short term per track over a few dozen tracks costs microseconds.

#### Follow-up nits (`reviews/s440/pr-1427-check2.md`, no code change)

- [ ] check2 #1: the collator treats hiragana/katakana and small/large kana as equal (`matchesTerm('カ','か')` true); use variant sensitivity or fold deliberately with a test.
- [ ] check2 #2: `foldText` is not idempotent for kana plus soft hyphen/ZWSP plus dakuten (NFC runs before deletion); run NFC after the expanding folds.
- [ ] check2 #3: curly apostrophes and quotes (U+2019 etc.) do not match ASCII ("goldilocks'" misses "Goldilocks’ Tale"); add folds.
- [ ] check2 #4: `matchesTerm` re-folds the term per title; fold once and pass the folded term.
- [ ] check2 #5: a standalone spacing dakuten (U+309B/C) folds to a lone combining mark U+3099; map it before NFKD.
