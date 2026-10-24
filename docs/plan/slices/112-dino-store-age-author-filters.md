# Slice 112 — Dino: store filters by age and author

*Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
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

## Work

- [ ] 1. Worker: parse and list `ageMin`/`ageMax`/`author` (validated: integers 0-18, min <= max), tests in
  `worker/downloads.test.mjs`.
- [ ] 2. Core: `StoreItem` fields, catalog parse, and a pure band/author filter helper with tests.
- [ ] 3. `StorePage`: chips, author filter, card age line; resx strings in all 14 locales.
- [ ] 4. `StoreTexts.cs` age ranges and authors for the ten stories; the builder and upload script write
  them to `meta.json`; re-upload the ten `meta.json` files.
- [ ] 5. Docs: the root `CLAUDE.md` store paragraph lists the new `meta.json` fields; the store rule in
  `src/DinoReader.App/CLAUDE.md`.

## Verification

- `npm test` (worker) and `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- `GET https://dinoreader.sirous.uk/api/stories` lists the age and author fields after the upload.
- Android emulator: the "6-8" chip and an author filter narrow the list; airplane mode, reopen the store:
  filtering still works on the cached list.
