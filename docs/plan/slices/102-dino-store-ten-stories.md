# Slice 102 — Ten open stories in the Dino story store

*Built 2026-10-24 (PR #280), not yet verified: the R2 upload, the live list and the emulator check are outstanding.* Planned 2026-10-24, from the user's request "add 10 open source stories to the store, so they can
download them from my server".

**Batch:** 20 (Dino Reader store content)
**Depends on:** Slice 92 (store), Slice 95 (bundled translations), Slice 91 (`StarterBuilder`); illustrations later depend on Slice 98
**Touches:** `tests/DinoReader.Tests/StoreTexts.cs` (new), `tests/DinoReader.Tests/StoreBuilder.cs` (new), `tests/DinoReader.Tests/StarterBuilder.cs`, `tests/DinoReader.Tests/StarterStoryTests.cs`, `tests/DinoReader.Tests/StoreStoryTests.cs` (new), `tests/DinoReader.Tests/Starters/translations/**`, `tools/upload-store-story.ps1` (new), `worker/stories.ts`, `worker/downloads.test.mjs`, `src/DinoReader.Core/StoreCatalog.cs`, `tests/DinoReader.Tests/StoreCatalogTests.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`

## Why

The store (Slice 92) is live at `dinoreader.sirous.uk/api/stories`, but empty: it lists no stories.
There's no tool that builds a store story's `meta.json` and `cover.jpg`, and no upload script.
`StarterBuilder` makes only the bundled starters, and the all-languages coverage test checks only
those.

## Decisions (2026-10-24, agreed with the user)

- **Decision 106: ten open stories in the store, retold by us, in all nine languages.**
- **The ten stories:**
  1. The Tale of Peter Rabbit (Beatrix Potter, 1902, her own words)
  2. Goldilocks and the Three Bears
  3. Little Red Riding Hood (the gentle ending: the woodcutter arrives in time and nobody is eaten)
  4. The Ugly Duckling (Andersen)
  5. The Lion and the Mouse (Aesop)
  6. The Little Red Hen
  7. The Three Billy Goats Gruff
  8. Jack and the Beanstalk
  9. The Elves and the Shoemaker (Grimm)
  10. The Town Mouse and the Country Mouse (Aesop)

  None repeats a built-in book.
- **Short retellings Claude writes,** based on the public-domain tales: 8-15 pages, simple sentences for
  ages 3-8. Every line of speech is tagged `…, said Hen` / `said Name` with a bare capitalised name (the
  starter rule), so each character gets a voice. The retold text is original to this repo (MIT). Peter
  Rabbit keeps Potter's public-domain text, with a Gutenberg header stripped if one was used.
- **All nine languages:** Claude writes the translations (fa, fr, es, pt, zh, ar, ru, ja) into
  `tests/DinoReader.Tests/Starters/translations/<stem>.<lang>.json`, as in Slice 95. The coverage test
  runs over the store stories as well as the starters, so a store story with a missing piece can't be
  built. `TranslateToMyLanguage` is on.
- **Text books with a coloured cover now.** The story uses the generated colour cover
  (`CoverStyle.Colour`). `cover.jpg` for the store list is rendered from the same colour and title. Once
  Slice 98 is built, public-domain illustrations (Potter's own, Walter Crane, L. Leslie Brooke) are added
  as picture + text pages and uploaded as `story-v2.dino` with `version: 2`. That is a follow-up, not this
  slice.
- **A credit line.** `meta.json` gets an optional `credits` string, e.g. "Retold from Aesop · free to share"
  or "Beatrix Potter, 1902". `worker/stories.ts` passes it through (still optional, so older entries
  stay valid), and the store card shows it as one small muted line under the title, in place of the language name that
  line shows today (changed to match the mock, 2026-10-24). The length line stays below it. No change to
  `story.json` or the `.dino` schema.
- **Fixed ids.** Each store story gets a fixed GUID package id (recorded in `StoreTexts`), so a rebuild
  uploads as a new `story-v<n>` of the same story and "Downloaded" keeps working.
- **Build and upload.**
  - `DINO_BUILD_STORE=1 dotnet test tests/DinoReader.Tests` writes
    `artifacts/store/<id>/{meta.json, cover.jpg, story-v<n>.dino}` (gitignored). `minutes` and
    `pageCount` come from the text, `added` is the build date, and `language` is `en`.
  - `tools/upload-store-story.ps1 <id>` puts the cover and the package with
    `wrangler r2 object put … --remote`, and `meta.json` last, so a half-upload never lists.
  - Claude runs the upload for all ten (wrangler is already logged in) and checks the live list.

#### Design (Dino Reader Batch 17-18 mock, screen 36, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 17-18.dc.html`, screen 36 (lines 175-196). One frame at 360x760: the store frame from Batch 16 (screen 21) with ten real stories (four shown). Real UI. The plan was changed to match the mock on the dropped language line.

- **Page and header.** Page `#F3EFE6`. Back arrow 44x44, title "Free stories" 20/600, subtitle "Picked and cast for Dino" 12 `#8a8399`. List padding 0/14/14, gap 10.
- **Store card.** White, radius 18, padding 12, gap 12. Cover 66 wide, aspect 3/4, radius 10, generated colour with the title in Andika 9.5/700 white, line-height 1.15, padding 5, centred. Colours seen: `#FFC24D`, `#7FD0FF`, `#FF7A9C`, `#8BD38A`, `#B8A6FF`. Text column, gap 3: title 15.5/600, line-height 1.25; **credit line** 12 `#8a8399`, line-height 1.3; size line 12.5 `#5a5470` ("8 pages · 4 min").
- **Credit examples.** "Retold from Aesop · free to share", "Beatrix Potter, 1902", "Retold traditional tale · free to share", "Retold from Andersen · free to share".
- **Card states (right edge).** Get: height 44, padding 0/14, radius 22, fill `#5B57E8`, white 14/600, download icon and "Get". Downloading: pill 88x44, radius 22, fill `#ECE7FF`, progress fill `#c9c0f5` at 42% width, label "42%" 14/600 `#3f3bc4`. Downloaded: no fill, green check and "Downloaded" 13.5/600 `#2E7D32`.
- **Behaviour (note).** The credit comes from `meta.json`'s optional `credits`; a card without credits shows the language name as its sub-line, as before. With credits, the credit line replaces the language name (the mock), and the length line stays. Covers are the generated colour covers until the illustrated `story-v2`.

Changed to match the mock (2026-10-24): the card's sub-line becomes the credit line, replacing the language name; the length line stays. Code today: `StorePage.xaml.cs:40,101-104` builds the card's sub-line as the language name plus the length (`Store_Length`); the mock keeps only the length line and puts the credit above it.

New strings: none new for the card (credit text is data from `meta.json`, not a resource).

## Work

- [x] 1. **Smallest end-to-end: one story live.** `StoreTexts` with The Lion and the Mouse, `StoreBuilder`
  (package, cover, meta), the upload script, the `credits` field in the Worker and on the store card
  (credit line replaces the language name; length kept). Upload
  it and see it in the live list and download it on the emulator. (Code built; the upload and emulator check are under Not yet verified.)
- [x] 2. **The other nine retellings,** with speech tags the caster resolves (a test asserts each story
  finds its expected characters).
- [x] 3. **Translations** for all ten into the eight other languages. The coverage test is extended to store
  stories.
- [ ] 4. **Upload all ten** and check them in `GET /api/stories`; download two on the emulator and read one in
  Farsi.
- [x] 5. Rules: the root `CLAUDE.md` store paragraph (`credits`, the builder and the upload script) and
  `src/DinoReader.App/CLAUDE.md` (written at planning; check they still match).

## Verification

- Unit: the store builder writes a valid `meta.json` (worker schema), a cover and a package that
  `StoryPackageReader` opens. Every store story has all eight translations for every piece, and each
  story's cast includes its named characters. The worker test covers `credits` passing through and its
  absence.
- Live: `curl https://dinoreader.sirous.uk/api/stories` lists ten stories with titles, credits, page counts
  and minutes.
- Emulator (device-test): the store shows ten stories with their credit lines. Downloading one installs it,
  and it shows "Downloaded". It reads with a voice per character, and with Farsi picked it reads in Farsi.

## Follow-up (not this slice)

- Illustrations as `story-v2` once Slice 98 is built.

## Built

*Changed while building:*

- Ten retellings live in `StoreTexts` with fixed GUID ids; each has its own `Version` (1) and `Added` (2026-10-24).
- Peter Rabbit is Potter's text shortened to 13 pages, so its credit reads "Beatrix Potter, 1902 (shortened)". Its cast is Mrs. Rabbit and Mr. McGregor; Peter has no spoken line.
- 80 translations plus 10 pieces files are written. A Claude-written check confirms counts and scripts; translation quality is unreviewed.
- `StoreBuilder` writes `artifacts/store/<id>/` using its own baseline JPEG encoder, with a structural test.
- The upload script resolves `$Root` from the script folder, uploads `meta.json` last, and checks the version.
- Credits: the Worker passes them through trimmed, max 200 characters. Core `StoreItem.Credits` is tested. The card shows credits, or the native language name when a story has none.

*From review:* high pass: 7 findings applied, #6 left for docs (this note). Medium pass: 3 applied, #2 accepted, #5 not a bug.

#### For CLAUDE.md

- Store `meta.json` takes an optional `credits` string; `worker/stories.ts` trims it and caps it at 200 characters. The store card shows credits, or the native language name when there are none (`src/DinoReader.App/StorePage.xaml.cs`). (Slice 102: docs/plan/slices/102-dino-store-ten-stories.md)
- Store stories are built from `tests/DinoReader.Tests/StoreTexts.cs` with fixed GUID ids; `DINO_BUILD_STORE=1 dotnet test tests/DinoReader.Tests` writes `artifacts/store/<id>/`. Bump a story's `Version` and upload a new `story-v<n+1>.dino` rather than overwriting. `tools/upload-store-story.ps1 <id>` uploads `meta.json` last. (Slice 102: docs/plan/slices/102-dino-store-ten-stories.md)

#### Not yet verified

- The R2 upload of all ten and the live `GET /api/stories` listing (the coordinator does this after merge).
- Emulator: the store shows ten cards with credits; download; read with voices; Farsi read.
- A native-speaker read of the translations.
