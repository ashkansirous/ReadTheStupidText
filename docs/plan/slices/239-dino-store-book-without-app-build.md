# Slice 239 — Dino: adding, changing or deleting a store story runs no pipeline but the upload

*Planned 2026-11-01, widened the same day.* From the user, after the Freddy and back-cover releases each built the
Dino iOS app: "why there is an ios build? I just changed the text and image of the stories in the store!", then:
"change the fucking code! Adding/updating/deleting a new story should never cause the build pipelines to run. No
tests should be executed! no other shit like that either!"

**Depends on:** none.
**Touches:** `.github/workflows/**`, `.github/scripts/ship-targets.mjs`, `.github/scripts/*.test.*`, `.github/CLAUDE.md`, `src/DinoReader.App/ShippedTitles.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.Core/Story*.cs`, `src/DinoReader.Core/StoryJson*.cs`, `tools/DinoReader.BookBuilder/**`, `tools/upload-store-story.ps1`, `tests/DinoReader.Tests/**`, `content/dino/*/book.json`, `content/dino/*/titles.json`, `.claude/skills/create-dino-story/SKILL.md`, `.claude/agents/dino-story-*.md`, `src/DinoReader.App/CLAUDE.md`, `worker/CLAUDE.md`

## Why (checked on origin/main, 2026-11-01)

- A change only under `content/dino/<book>/` runs `publish-store` **and the Dino test suite** (`ship-targets.mjs`,
  groups `dinostore`/`dinoreader`), and PR checks run on it.
- Every new store book also had to add its title in 13 languages to `src/DinoReader.App/ShippedTitles.cs` (app code),
  so the release built the Dino iOS, Android and Windows apps (Freddy, PR #680). The test
  `EveryShippedBook_HasATitleInEveryOtherLanguage` (`tests/DinoReader.Tests/StoreStoryTests.cs:92`) forces that row,
  and `BookBuilderTests`/`StoreStoryTests` hard-code the number of books, so a new book also edits tests.
- The store list already shows translated titles from the listing's own `titles`. Only the shelf and reading screen
  of a downloaded book read `ShippedTitles.ForBook` (`StoryTranslator.cs:162`, `:175`).
- A book whose content changed but whose `version` was not stamped is caught today only by `check` in the tests.

## Decisions (the user, 2026-11-01)

- A store story add, update or delete runs **only** the store upload: no app build, no test suite, no PR test checks,
  no code-review gate. (Supersedes the earlier "ship with the next app change" timing: build it now.)
- Starter books are bundled in the app, so changing a starter still builds the app (stated to the user).

## Work

- [x] **1. Titles travel with the book.** Each store book's 13 titles live in its content folder; the builder writes
  them into the package (optional story.json field, old apps ignore it) and into meta.json. The shelf and reading
  screen read the book's own titles first, then `ShippedTitles` (fallback for older downloads and starters). Store
  rows leave `ShippedTitles.cs`.
- [x] **2. No test edits per book.** Tests derive book counts and the title list from `content/dino/`, never a
  hard-coded number or the app table, so adding or deleting a book edits no test file.
- [x] **3. No version stamping needed.** `publish-store` compares each book's content hash with the live store and
  uploads a changed book as live version + 1 (never overwriting a version). `stamp`/`check` stop being required for
  store books; the `check` step leaves the test suite.
- [x] **4. Delete.** `publish-store` removes a store book from the listing when its content folder is gone (meta.json
  first, packages kept for installed copies), and logs which ids it removed.
- [x] **5. Pipelines.** `content/dino/<store-book>/**` triggers only the `dinostore` target: no `dinoreader` tests, no
  app builds, no PR test workflows (path filters on every PR/push workflow, incl. `tests.yml`). Starter folders keep
  triggering the app. Unit-test `ship-targets.mjs` for both cases.
- [x] **6. Rules.** create-dino-story, the writer/illustrator/reviewer agents and `src/DinoReader.App/CLAUDE.md`: a
  store book never edits `src/`, `tests/` or app files, and needs no stamp. The ClaudeSkills `merge-gate.mjs` treats
  `content/dino/**` store-book files as content, so no review files are demanded (separate ClaudeSkills PR).

**Built 2026-11-01 (deviations).** Item 1 needed no app code: `StoryTranslator.ShelfTitle`/`TitleAsync` already look a title up in the installed book's own language table first, so `titles.json` is merged into each language's table under the English title (works in already-installed apps, no app release). The 12 existing store books got a `titles.json` (from `ShippedTitles`); their old rows stay in `ShippedTitles.cs` as the fallback for copies installed before (removing them would blank those titles). Item 3: `check` still runs for the starters (hash + committed package) and for "every store book builds and has titles"; store books are no longer hash-checked or stamped. Known: re-adding a deleted store book needs a `book.json` version above its old live one.

## Verification

- Dino tests and the `ship-targets.mjs` unit tests.
- A content-only PR (one store book's text) shows no PR checks and, after merge, a release run with only
  `publish-store`; the live store shows the new version.
- Android emulator: a store book downloaded in another app language shows its translated title on the shelf and the
  reading screen.
