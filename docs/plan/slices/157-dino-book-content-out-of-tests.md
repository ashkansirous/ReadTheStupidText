# Slice 157 — Dino: book content and the book builder out of the test project

*Planned 2026-10-27.* Dino Reader only. Runs **alone**, between waves: it moves every book's files.

**Batch:** 34 (store plumbing, 2026-10-27; Decision 162)
**Touches:** `content/dino/**` (new), `tools/DinoReader.BookBuilder/**` (new), `ReadTheStupidText.slnx`,
`tests/DinoReader.Tests/**`, `src/DinoReader.App/Resources/Raw/starters/*.dino`, `docs/design/story-art/**`,
`tools/upload-store-story.ps1`, `tools/prepare-story-pictures.ps1`, `tools/ocr-starter-pages.ps1`,
`.github/workflows/dinoreader.yml`, `.github/workflows/release.yml`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/ship-targets.mjs`,
`.github/CLAUDE.md`, `.claude/agents/dino-story-*.md`, `.claude/skills/create-dino-story/**`,
`.claude/skills/dino-storytelling/**`, `src/DinoReader.App/CLAUDE.md`, `CLAUDE.md`, `docs/design/dino-reader/README.md`

## Why

Every store and starter book is built by an xUnit "test" that only works when `DINO_BUILD_STORE=1` or
`DINO_BUILD_STARTERS=1` is set (`tests/DinoReader.Tests/StoreStoryTests.cs:24`, `StarterStoryTests.cs:21`). The book
text is a C# constant (`StoreTexts.cs`), the pictures live under `tests/DinoReader.Tests/Store/<book>/`, and the upload is
a hand-run script. Nothing connects a merge to the store, so the store falls behind `main`: on 2026-10-27
"X Marks the Treasure!" was v1 in the store and v5 on `main`. No decision ever chose this layout; it came in with
the starter builder (`596469a`, 2026-10-23) and Slice 102 copied it.

Grounding (2026-10-27): the builders use no xUnit or test-only helper and reference only `DinoReader.Core` (no
Core internals found by name; add `InternalsVisibleTo` for the new project if the compile needs it). They run on
Linux (no Windows APIs, fixed Supertonic voice lists, hand-written JPEG writer). The four starters are committed
`.dino` files shipped as `MauiAsset` (`DinoReader.App.csproj:95`). `release.yml` already holds
`CLOUDFLARE_API_TOKEN`/`CLOUDFLARE_ACCOUNT_ID`. About 48 MB in 458 files. No check catches content changed
without a version bump.

## Decisions locked with the user (Decision 162)

1. **One folder per book**, `content/dino/<stem>/`: `story.md` (the text, pages split by `---`, exactly today's
   constant), `book.json` (id, title, version, ages, author, credits, colour, added date, cast voices, content
   hash), `pictures/1..N.jpg`, `cover.jpg`, `cover-thumb.jpg`, `translations/<lang>.json`, `art.md` plus
   `characters/` (moved from `docs/design/story-art/`). Starters and store books use the same shape; `book.json`
   says which (`"kind": "starter" | "store"`).
2. **A console builder**, `tools/DinoReader.BookBuilder` (net10.0, references `DinoReader.Core`), with commands
   `build [<stem>…]` (to `artifacts/store/<id>/`, and for starters also the committed `.dino` in
   `Resources/Raw/starters/`), `check`, `stamp <stem>` and `dump-pieces`. The env-var tests go away.
3. **Starters stay committed** in `src/DinoReader.App/Resources/Raw/starters/`; `check` fails when a committed
   starter differs from what its content folder builds.
4. **Forgetting a version bump fails CI.** `book.json` stores a hash of the book's content (text, pictures, covers,
   translations, cast). `check` fails with "raise the version of <stem>" when the content hash differs from the
   stored one; `stamp` raises the version and refreshes the hash.
5. **Merging to `main` uploads bumped books to the live store.** Built first by Slice 158 (Decision 163): the
   `publish-store` job in `release.yml`. This slice only repoints it at `content/dino/` and `BookBuilder build`.
6. **One PR** moves everything and updates every reference. The validation tests stay in `tests/DinoReader.Tests`
   and read `content/dino/` through a shared loader.

## Work

Not vertical by design: a one-off move of content and tooling, with no user-visible surface except the store
catching up. The first sub-slice still ends in something that works end to end (one book built from its folder).

- [ ] **Sequencing.** Before starting: the open per-book PRs that edit `StoreTexts.cs`, `StarterTexts.cs`,
      `StarterVoices.cs`, `Store/` or `Starters/` merge or are parked (check with `parallel-check`; on 2026-10-27
      PR #431 (Slice 153) edited `StoreTexts.cs`, and the good-enough/billy-goats worktrees had uncommitted work).
      Claim with a draft PR.
- [ ] **Builder project.** Create `tools/DinoReader.BookBuilder`, move `StoreBuilder`, `StarterBuilder`,
      `StarterAnimals`, `StarterVoices`, `StoreCover`/`JpegWriter` into it (internal to the tool), add it to
      `ReadTheStupidText.slnx`. Add a `BookFolder` loader that reads `book.json` + `story.md` + files.
- [ ] **One book end to end.** Migrate `x-marks-the-treasure` to `content/dino/x-marks-the-treasure/`;
      `build x-marks-the-treasure` produces a `.dino` byte-identical in content to today's builder output
      (same `story.json`, same pictures). Keep this as a test.
- [ ] **Migration script** (`tools/move-book-content.ps1`, one-off, deleted after the PR): writes every book's
      `story.md` from its C# constant and `book.json` from its `StoreTexts`/`StarterTexts`/`StarterVoices` entry,
      `git mv`s pictures, covers, translations and art records. Run it for all books; delete the constants.
- [ ] **Starters.** `build` writes the four starter `.dino` files; `check` compares them with the committed ones.
- [ ] **Hash guard.** Content hash in `book.json`; `check` and `stamp`; seed every book's hash at its current version.
- [ ] **Tests.** Point every validation test (`GentleTextTests`, `AuthoredVoiceTests`, `PictureTextBookTests`,
      `StarterUpdateTests`, `StoreStoryTests`, `CastFallbackFitTests`, the animal and per-book tests, …) at the
      loader. Remove the two env-var build tests and `StarterVoicesDump` if the tool replaces it. Same test count
      minus those, all green.
- [ ] **CI.** `dinoreader.yml` runs `BookBuilder check`. `ship-targets.mjs` treats `content/dino/**` as a Dino
      trigger (starter changes ship the app; store-only changes ship nothing but the store job).
      Repoint Slice 158's `publish-store` job and the `dinostore` ship target at `content/dino/**` and
      `BookBuilder build`. `tools/upload-store-story.ps1` reads from the new paths.
- [ ] **References.** Update the Dino agents (`dino-story-writer`, `-illustrator`, `-reviewer`), the
      `create-dino-story` and `dino-storytelling` skills, `tools/prepare-story-pictures.ps1`,
      `tools/ocr-starter-pages.ps1`, `src/DinoReader.App/CLAUDE.md`, root `CLAUDE.md` (store paragraph),
      `.github/CLAUDE.md`, `docs/design/dino-reader/README.md`. Old slice files keep their historical paths.
- [ ] **Catch the store up.** After the first green `publish-store` run after this merge on `main`, compare every `book.json`
      version with `/api/stories` and confirm none lag (X Marks the Treasure v5 among them).

## Verification

- `dotnet run --project tools/DinoReader.BookBuilder -- check` passes on `main` after the move; changing one word
  in a `story.md` without `stamp` makes it fail with the book's name.
- `dotnet test tests/DinoReader.Tests` green; no test reads `Store/`, `Starters/`, `StoreTexts` or `StarterTexts`.
- The four committed starters rebuild unchanged; the app's starter install (Slice 91/`StarterUpdate`) still sees
  the same ids and versions.
- `publish-store` on the merge: uploads only bumped books, `meta.json` last; `/api/stories` then lists every
  store book at its `book.json` version; a re-run uploads nothing.
- `git grep -n "tests/DinoReader.Tests/Store\|DINO_BUILD_STORE\|DINO_BUILD_STARTERS"` finds only old slice and
  note files.

## Out of scope

- Changing the `.dino` format or `schemaVersion`.
- Building the starters during the app build.
- Raising versions automatically in CI (would need commits back to `main`).
- The untracked `src/DinoReader.BookSample/` pictures.
