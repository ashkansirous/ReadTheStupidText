# Slice 112 — Dino: store filters by age and author

*Built 2026-10-24, not yet verified on the emulator or live (see "Not yet verified / after merge").* *Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
reading language, one narrator, parent recordings, store filters" (2026-10-24) —
https://drive.google.com/file/d/1b2XPenCW2D4Kx1yx98js_LK4CRBSnnLf/view?usp=drivesdk (part 6, milestone 6).

**Batch:** 22
**Depends on:** Slice 102 (the ten store stories and their `meta.json`)
**Touches:** `worker/stories.ts`, `worker/downloads.test.mjs`, `src/DinoReader.Core/StoreCatalog.cs`, `tests/DinoReader.Tests/StoreCatalogTests.cs`, `tests/DinoReader.Tests/StoreTexts.cs`, `tests/DinoReader.Tests/StoreBuilder.cs`, `tools/upload-store-story.ps1`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `CLAUDE.md` (store paragraph), `src/DinoReader.App/CLAUDE.md`

## Why

The store needs to be browsable by age and author once it grows past the ten launch stories. Today
`worker/stories.ts` `StoryMeta`/`ListItem` carry `credits` only; `parseMeta` ignores unknown fields and
`StoreCatalog.cs` mirrors the list. The metadata lives in `meta.json`, not in the `.dino`, so no story
needs repackaging.

## Decisions (2026-10-24, agreed with the user)

- **Decision 116: store stories carry an age range and an author; the store filters by Google Play's age
  bands.** `meta.json` gains `ageMin`, `ageMax` and `author` (optional; a story without them shows under
  every band). The store shows chips "5 & under", "6-8", "9-12" (a chip matches any story whose range
  overlaps it) and an author filter; the card says "Ages 3-6". Filtering is client-side on the fetched
  list and works offline on the cached list. Other filters (search, language, length, has pictures) are
  later.
- Claude assigns the ten stories' age ranges from their content (e.g. Peter Rabbit 3-6, The Ugly Duckling
  5-8) in `StoreTexts.cs`; the user can change them in review. Their `meta.json` files are re-uploaded with
  `tools/upload-store-story.ps1` (no new `story-v<n>.dino`).

#### Design (Dino Reader Batch 22-23 mock, frame 47, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 22-23.dc.html`, frame 47 (lines 234-252). One real-UI frame (360x760 phone frame, 6px `#2b2540` border, radius 34.); it builds on the store frame of Batch 17-18 (screen 36, see Slice 102). Icons in the mock are inline SVG strokes (2.2 wide, `#5B57E8`), not Phosphor names; the app uses its own `IconKind` set. The mock agrees with the plan and adds an "All ages" chip.

- **Age chips (112-3).** A row of four chips under the header, each 40 high, radius 20: "All ages" (default), "5 & under", "6–8", "9–12". Selected: fill `#5B57E8`, white 14/600. Unselected: white fill, `#5a5470` 14. One chip at a time. Bands are Google Play's; a story shows under every band its range overlaps (The Lion and the Mouse, 4–7, shows under "5 & under" and "6–8").
- **Author filter.** A row under the chips: a white field, 44 high, radius 14, label "Author" 14 `#8a8399`, value "Anyone" 14, chevron. It opens a picker of the authors in the list.
- **Card.** As in Slice 102 (white, radius 18, padding 12, cover 66 wide, Get button 44 high `#5B57E8`), with the detail line now "Ages 5–8 · 14 pages · 7 min" (12.5 `#5a5470`): the age range leads the length line. The credit line above it stays ("Retold from Andersen · free to share"). Cards shown: The Ugly Duckling (Ages 5–8, 14 pages, 7 min), The Lion and the Mouse (Ages 4–7, 8 pages, 4 min), The Three Billy Goats Gruff (Ages 4–7, 10 pages, 5 min).
- **Behaviour (note).** Filtering is on the phone, works offline on the cached list. The ages are Claude's drafts for review.

Settled by the mock: the chip labels use an en dash ("6–8", "9–12"); "All ages" is a chip and the default; the age sits on the detail line, not the credit line; the author control is a labelled picker with "Anyone" as no filter.

New strings (all 14 locales): "All ages", "5 & under", "6–8", "9–12", "Author", "Anyone", "Ages {0}–{1}" (card line).

## Work

- [x] 1. Worker: parse and list `ageMin`/`ageMax`/`author` (validated: integers 0-18, min <= max), tests in
  `worker/downloads.test.mjs`.
- [x] 2. Core: `StoreItem` fields, catalog parse, and a pure band/author filter helper with tests.
- [x] 3. `StorePage`: four chips ("All ages" default, "5 & under", "6–8", "9–12"), the "Author" picker with "Anyone", the card line "Ages {0}–{1} · {pages} pages · {min} min" (frame 47); resx strings in all 14 locales.
- [x] 4. `StoreTexts.cs` age ranges and authors for the ten stories; the builder and upload script write
  them to `meta.json`. The re-upload of the ten `meta.json` files is outstanding (see "Not yet verified / after merge").
- [ ] 5. (tracked under "For CLAUDE.md" below, done by the index pass) Docs: the root `CLAUDE.md` store paragraph lists the new `meta.json` fields; the store rule in
  `src/DinoReader.App/CLAUDE.md`.

## Built (implementer notes)

- `meta.json` fields: `ageMin`/`ageMax` (both or neither; integers 0-18, min <= max, else both dropped) and `author` (trimmed, max 100). Worker `parseMeta` and `StoreCatalog` apply the same rule.
- Filtering: `StoreFilter` (Core, pure: `InBand`, `Apply`, `Authors`); `StorePage` keeps every row and shows the filtered subset, so a row's download state survives a filter change.
- Ages and authors drafted in `StoreTexts.cs` (traditional tales: author "Traditional"). Re-upload needed: `pwsh tools/upload-store-story.ps1 <id> -MetaOnly` for each of the ten ids after `DINO_BUILD_STORE=1 dotnet test tests/DinoReader.Tests` (version stays 2, no new package).
- Not in the mock, added in review: a muted line "No stories here yet. Try another age." (`Store_NoMatch`) when a filter empties the list; the author picker lists only authors in the chosen band and a band change clears an author it excludes. Review also added `Store_AgeSingle` ("Age 5" when min = max) and `Store_ChipSelected` (chip accessibility description), authors compared ignoring case, and the client dropping only a wrong-typed age range or author (not the story), as the Worker does.

*From review:* high pass, findings 1-9 applied: empty state "No stories here yet. Try another age."; `StorePage` filters through `StoreFilter.Apply`; "Age 5" when the range is a single age; the author picker shows only authors in the chosen band; the client drops only the bad field (as the Worker does), not the story; authors compared case-insensitively. Medium pass, findings 1-6 applied: the "9–12" chip is open-ended, so it also matches stories for 13+; `5.0` is accepted as an integer age; the chosen author is cleared on reload; the store refreshes in `OnAppearing`; duplicate author labels show a visible "(2)".

## For CLAUDE.md (lifted 2026-10-24; index pass; the CLAUDE.md files are not edited on the branch)

- Root `CLAUDE.md` store paragraph: `meta.json` also carries optional `ageMin`/`ageMax` (both or neither; integers 0-18, min <= max) and `author` (trimmed, max 100); the list response repeats them (Slice 112: docs/plan/slices/112-dino-store-age-author-filters.md).
- `src/DinoReader.App/CLAUDE.md`, "Store stories (Slice 102)": the card shows the age range (then pages and minutes) above `credits`; the store filters on the phone by Google Play age band and author (`StoreFilter` in Core), also on the cached list offline.

## Not yet verified / after merge

- Done 2026-10-24, verified via `/api/stories`: re-upload of the ten stories' `meta.json` (`DINO_BUILD_STORE=1 dotnet test tests/DinoReader.Tests`, then `pwsh tools/upload-store-story.ps1 <id> -MetaOnly` for each id). Until then live `/api/stories` has no ages or authors: the author row hides, cards show no ages and the chips filter nothing.
- Emulator check of the age chips, the author picker and the empty state.
- Native-speaker check of the new strings in the 13 non-English locales.

## Verification

- `npm test` (worker) and `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- `GET https://dinoreader.sirous.uk/api/stories` lists the age and author fields after the upload.
- Android emulator: the "6-8" chip and an author filter narrow the list; airplane mode, reopen the store:
  filtering still works on the cached list.

*Emulator check 2026-10-25 (Android emulator, Debug arm64, main 7846b1c): PASS. Chips (5 & under / 6-8 / 9-12), author picker and empty state work.*
