# Slice 102 — Ten open stories in the Dino story store

*Planned 2026-10-24*, from the user's request "add 10 open source stories to the store, so they can
download them from my server".

**Batch:** 20 (Dino Reader store content)
**Depends on:** Slice 92 (store), Slice 95 (bundled translations), Slice 91 (`StarterBuilder`); illustrations later depend on Slice 98
**Touches:** `tests/DinoReader.Tests/StoreTexts.cs` (new), `tests/DinoReader.Tests/StoreBuilder.cs` (new), `tests/DinoReader.Tests/StarterBuilder.cs`, `tests/DinoReader.Tests/StarterStoryTests.cs`, `tests/DinoReader.Tests/StoreStoryTests.cs` (new), `tests/DinoReader.Tests/Starters/translations/**`, `tools/upload-store-story.ps1` (new), `worker/stories.ts`, `worker/*.test.ts`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`

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
  stay valid), and the store card shows it as one small muted line under the title. No change to
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

## Work

- [ ] 1. **Smallest end-to-end: one story live.** `StoreTexts` with The Lion and the Mouse, `StoreBuilder`
  (package, cover, meta), the upload script, the `credits` field in the Worker and on the store card. Upload
  it and see it in the live list and download it on the emulator.
- [ ] 2. **The other nine retellings,** with speech tags the caster resolves (a test asserts each story
  finds its expected characters).
- [ ] 3. **Translations** for all ten into the eight other languages. The coverage test is extended to store
  stories.
- [ ] 4. **Upload all ten** and check them in `GET /api/stories`; download two on the emulator and read one in
  Farsi.
- [ ] 5. Rules: the root `CLAUDE.md` store paragraph (`credits`, the builder and the upload script) and
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
