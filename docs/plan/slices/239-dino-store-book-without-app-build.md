# Slice 239 — Dino: a new or changed store book never touches app code

*Planned 2026-11-01.* From the user, after the Freddy and back-cover releases each built the Dino iOS app:
"why there is an ios build? I just changed the text and image of the stories in the store!"

**Depends on:** none.
**Touches:** `src/DinoReader.App/ShippedTitles.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.Core/Story*.cs`, `src/DinoReader.Core/StoryJson*.cs`, `tools/DinoReader.BookBuilder/**`, `tests/DinoReader.Tests/StoreStoryTests.cs`, `tests/DinoReader.Tests/BookBuilderTests.cs`, `.claude/skills/create-dino-story/SKILL.md`, `.claude/agents/dino-story-writer.md`, `src/DinoReader.App/CLAUDE.md`

## Why (checked on origin/main, 2026-11-01)

- A change only under `content/dino/<book>/` runs `publish-store` and the Dino tests and builds no app
  (`.github/scripts/ship-targets.mjs`, groups `dinostore`/`dinoreader`).
- But every new store book also had to add its title in 13 languages to `src/DinoReader.App/ShippedTitles.cs`, which
  is app code, so the release built the Dino iOS, Android and Windows apps (Freddy, PR #680). The test
  `EveryShippedBook_HasATitleInEveryOtherLanguage` (`tests/DinoReader.Tests/StoreStoryTests.cs:92`) forces that row.
- The store list already shows translated titles from the listing's own `titles` (`ShippedTitles.ShownInStore`,
  `Core/StoreCatalog.cs:12`). Only the shelf and the reading screen of a downloaded book read `ShippedTitles.ForBook`
  (`StoryTranslator.cs:162`, `:175`), so they need the table today.
- The back cover (Slice 237, PR #688) was real app code and needed one app release regardless.

## Work

- [ ] **1.** The book carries its own titles: the builder writes the per-language titles into the `.dino` package
  (story.json, optional field, so old apps ignore it), taken from a `titles` block in the book's content folder.
- [ ] **2.** The shelf and reading screen read the installed book's own titles first, then `ShippedTitles` (kept as
  the fallback for books downloaded before this change and for the starters).
- [ ] **3.** Move the store books' rows out of `ShippedTitles.cs` into each book's content folder; the test checks the
  content folder instead of the app table for store books. Starters stay in the table (they ship with the app anyway).
- [ ] **4.** Skill and writer agent: a new store book's titles go in its content folder, never in app code. A check
  (BookBuilder `check` or a test) fails a store-book change that edits `src/` without saying why.
- [ ] **5.** Restamp store books so installed copies get packages with titles.

This needs **one** more Dino app release (Android, iOS, Windows); after it, adding or editing a store book builds no app.

## Verification

- Dino tests and BookBuilder `check`.
- Android emulator: download a store book in another app language, its translated title shows on the shelf and the
  reading screen.
- A test PR touching only `content/dino/<store-book>/` shows `publish-store` running and no Dino app build.
