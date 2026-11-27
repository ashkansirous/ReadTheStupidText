# Slice 437 — Dino: Sonos opens in the parent's language with shelves (Bedtime, New, ages, Free, A–Z)
**Product:** Dino

*Plan-cleanup check 2026-11-25 (origin/main 1a8bf725): verdict keep open, nothing built. `worker/` has only sonos.ts, sonos-soap.ts, sonos.test.mjs (no sonos-shelves, sonos-labels, sonos-art, no Accept-Language read); no PR for 437 (only the plan PR #1349). No waits found: A1 is own research, and the paid-track decision is already given in the mock ("In the Dino app", not playable). Known today: the audio route answers 402 for paid books (Slice 436 note) and the user reported "it works for the free books, but not the paid ones" (2026-11-24). A1 still needs the Sonos docs/sandbox result for a non-playable track. The Not yet verified lines stay in unverified.md and do not hold the slice.*

*Planned 2026-11-24. Ships first of 437-440, no sign-in: these are the screens until Slice 438 makes every home link
(owner's "link required for all", Slice 415); after that the same shelves show for the linked home.*

**Depends on:** Slice 283 (the Sonos service). **Replaces:** Slice 436 parts B and D (436 keeps A, real plays, and C, the
5-minute list cache).

**Touches:** `worker/sonos.ts`, `worker/sonos-soap.ts`, `worker/sonos.test.mjs`, `worker/sonos-shelves.ts` (new),
`worker/sonos-shelves.test.mjs` (new), `worker/sonos-labels.ts` (new), `worker/sonos-art.ts` (new),
`worker/sonos-art.test.mjs` (new), `site/dino/sonos-art/` (new), `worker/index.ts`, `worker/stories.ts`,
`worker/CLAUDE.md`, this file.

## Why

Owner, 2026-11-24, after testing: "the listing is very bad. I have to select the language every time, and then I see the
apps. there is no filter, no sorting, no nothing." The owner then brought the Claude Design canvas
(`docs/design/sonos/`, README there) with this brief for this slice (verbatim):

> What's wrong today: the first screen is a list of 15 languages to pick every time, then one flat unsorted list of books.
> The parent does everything on Sonos (the child only listens), so every screen is worded for a parent choosing a story.
>
> Slice A, anonymous browse redesign (ship first, no sign-in):
> - Root opens in the first language from the request's Accept-Language header that has tracks (fa-IR -> fa, follow the
>   q order). No header, or no match: root stays today's language list.
> - Root shows a search box (see Slice D), then these folders, each playable (canPlay, so Play queues all of it) and
>   hidden when empty: Bedtime: 10 minutes or less (listenSeconds <= 600, shortest first) · New stories (store tag New) ·
>   Ages 3–5 (ageMin <= 5) · Ages 6–9 (ageMax >= 6; a 3–9 book is in both) · Free stories (access free) · All stories, A–Z
>   (that language's alphabet via Intl.Collator, ignoring a leading "The"). Then a row for the second language from
>   Accept-Language, if it has tracks, then Other languages (14 rows: own name as title, "<name in the parent's
>   language> · N stories" as subtitle, the parent's other languages first).
> - Every folder title and subtitle is written in the parent's language, with its digits for Farsi and Arabic. Book
>   subtitle: "Ages 3–5 · 6 min · Free" or "In the Dino app" (paid books stay listed but not playable on an anonymous
>   service; confirm with me what Sonos does with a track that can't play).
> - The language lives in every id: lang:<code>, lang:<code>/<shelf>, track:<code>:<storyId> (keep today's track id
>   form, so existing favourites and queues keep working). A speaker sends no Accept-Language, so nothing may depend on
>   the header once an id carries the language.
> - Square album art for folders: generated PNGs served by the Worker, one per shelf. Book covers are portrait, so serve
>   a square crop (absolute URL must stay <= 128 characters, as today).
> - The catalog token in getLastUpdate must change whenever any shelf's contents or labels change.
> - Tests in worker/sonos.test.mjs for each rule above, including Farsi collation and a header with no match.

Today, read on origin/main 2026-11-24: ids are `root`, `lang:<code>`, `track:<code>:<storyId>` (`worker/sonos.ts:14-16`,
126-128); no search or presentation map; art is `${origin}${coverUrl}` (the R2 `cover.jpg` through
`/api/stories/<id>/cover`, no crop, `worker/stories.ts:752-769`), dropped when over `MAX_ART_URI` 128
(`worker/sonos.ts:25,82,90`); getLastUpdate's catalog token is an FNV hash of the offered tracks (`worker/sonos.ts:226`).
Catalogue fields: `audio[lang].listenSeconds`, `ageMin`/`ageMax` (nullable), `tags` (Hot/Popular/Recommended/New),
`access` free/paid (missing = free), `titles[lang]` (`worker/stories.ts:97-156,281-282,368-369`).

## Decisions

- Owner, 2026-11-24: the canvas `docs/design/sonos/` is the design; it wins over Slice 436's part B ("hide paid books")
  and part D. Paid books stay listed with the subtitle "In the Dino app" and do not play on the anonymous service.
- Owner's brief (above) is the rule set; the mock's words are the English and Farsi copy (EN-*, FA-* artboards).
- Decided by this session, from the mock's Main artboard: "New" shelf is newest first; a phone language Dino lacks
  (Italian) moves to the next language in the header, and only no header or no match at all gives the language list
  (the owner's brief; the mock's "else English" note is the narrower reading and is not used).
- Decided by this session: a book with no `ageMin`/`ageMax` is in neither age shelf but stays in All and the other shelves.
- What Sonos does when a parent taps or queues a track that can't play: asked of the owner by the brief; work item A1
  finds out first and the answer goes to the owner before the paid-book rows ship.

## Design

From `docs/design/sonos/` (EN-Home, EN-Bedtime, EN-Languages, FA-Home, FA-Bedtime, FA-All, FA-Languages, Main):

- **Root (EN-Home):** title "Dino: Kids Books & Stories"; search box "Search stories" (Slice 440 builds it; this slice
  leaves room only); rows in order: "Bedtime: 10 minutes or less" / "6 stories · shortest first · press play for all",
  "New stories" / "2 stories", "Ages 3–5" / "N stories", "Ages 6–9", "Free stories", "All stories, A–Z", then the
  second language row (FA-Home: own name "English", subtitle "انگلیسی · ۸ داستان"), then "Other languages" /
  "14 languages".
- **Farsi root (FA-Home):** "قصه‌های شب: ۱۰ دقیقه یا کمتر" / "۶ داستان · کوتاه‌ترها اول · پخش همه", "داستان‌های تازه",
  "۳ تا ۵ سال", "۶ تا ۹ سال", "داستان‌های رایگان", "همه‌ی داستان‌ها، الفبایی", "زبان‌های دیگر" / "۱۴ زبان". Eastern
  Arabic-Persian digits.
- **Shelf (EN-Bedtime):** book rows: square cover, title in the shelf's language, subtitle "Ages 3–5 · 6 min · Free" or
  "Ages 3–5 · 6 min · In the Dino app".
- **Other languages (EN-Languages):** title = the language's own name ("Deutsch"), subtitle "German · 8 stories" in the
  parent's language, art = a tile with the two-letter code. Order: the parent's other header languages first, then by
  the parent-language name.
- **Folder art:** one square tile per shelf: Bedtime moon (#3B3F8F), New sparkle (#9A6412), Ages 3–5 sprout (#2F6B3A),
  Ages 6–9 book (#1F6670), Free gift (#8A3A62), A–Z letters (#4A4E57), Other languages globe (#2C5C9A); white stroke
  icon. The icon paths are in EN-Home.dc.html.

## Work

- [ ] A1. Find out what Sonos does with a listed track that can't play (docs.sonos.com via `web-research`: the
      `isPlayable`/`canPlay` item flags and the fault for `getMediaURI`), then try it on the owner's sandbox (tap, add to
      queue, play a shelf that contains one). Write the result here and take it to the owner before A4 ships paid rows.
- [ ] A2. `worker/sonos-soap.ts`: read `Accept-Language` (q order, region dropped, fa-IR → fa) and pass the ordered list
      to the browse; only `root` uses it.
- [ ] A3. `worker/sonos-shelves.ts`: pure shelf builder per language (the six shelf rules, empty shelf hidden, A–Z with
      `Intl.Collator(lang)` ignoring a leading "The" in English, Bedtime shortest first, New newest first) and the ids
      `lang:<code>`, `lang:<code>/<shelf>` (`bedtime`, `new`, `age-3-5`, `age-6-9`, `free`, `all`), track ids unchanged.
      Every container is `canPlay`.
- [ ] A4. `worker/sonos-labels.ts`: folder titles, subtitles, book subtitle parts and language names in all 15 languages,
      native digits for fa and ar; English and Farsi from the mock, the rest translated. Paid book: "In the Dino app";
      the track is marked not playable per A1's finding.
- [ ] A5. `worker/sonos.ts`: root by header (shelves, second language row, Other languages), language list when no
      match; `lang:<code>` opens that language's shelves; nothing after `root` reads the header.
- [ ] A6. `worker/sonos-art.ts` + `site/dino/sonos-art/`: the seven folder PNGs (static, made once from the mock's
      icons) and a square cover route (`/s/c/<id>.jpg` or as short; a centre crop of the cover, cached), absolute URL
      checked <= 128.
- [ ] A7. getLastUpdate's catalog token hashes every shelf's ids and labels as well as the tracks.
- [ ] A8. Tests (`worker/sonos.test.mjs`, `worker/sonos-shelves.test.mjs`, `worker/sonos-art.test.mjs`): each shelf rule,
      header order and region, a header with no match, Farsi collation, Farsi and Arabic digits, a 3–9 book in both age
      shelves, old `track:` ids still resolve, art URLs <= 128, token changes on a label change.
- [ ] A9. `worker/CLAUDE.md`: one rule line: Sonos ids carry the language; only `root` reads Accept-Language.

## Verification

- `npm ci && npm run typecheck`, `node --test worker/*.test.mjs`.
- Owner's sandbox, from the Sonos app on the S21 (English) and the same with the phone set to Farsi: root opens in the
  phone language with the shelves and art of the mock; Play on Bedtime queues all of it; an old favourite still plays.

## Not yet verified

- [session] Does the Sonos app lay out Farsi right to left? (S21 Sonos app, phone in Farsi.)
- [session] Does Sonos cache the root per household? If so, two phones with different languages could see the same
  first screen (two phones, or the S21 switched between English and Farsi).
- [session] Can a service's folder (not just a track) be saved to My Sonos?
