# Slice 277 — Dino: a store book is published only when every sentence in every language has pre-made audio

*Planned 2026-11-06.* Dino Reader only. Read The Stupid Text is not touched.

**Depends on:** none. **Blocks:** Slice 278 part B (the voice engine leaves the app, so nothing may be missing).
**Touches:** `tools/DinoReader.BookBuilder/**` (a new `AudioCoverage.cs`, `StoreValidation.cs`, `BookCommands.cs`, `Program.cs`), `tools/upload-store-story.ps1`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `.github/workflows/dino-store.yml`, `tests/DinoReader.Tests/**` (BookBuilder tests, new files), `tools/premade-audio/**` (only to fill gaps the first report finds), `.claude/skills/create-dino-story/SKILL.md`, `src/DinoReader.App/CLAUDE.md`, this file.

## Why

The user, 2026-11-06, when told that a sentence without a pre-made clip is read by the phone's voice engine:
"I dont need that fucking fail over! that is my own fucking store! your fucking job is to make sure the books will
have the proper audio for everything! if you already dont have it add a slice to run tests before publishing a book
to store."

Today (Slice 246, checked on origin/main 2026-11-06): pre-made audio comes as one pack per book and language. The app
finds each clip by the hash of the sentence text; "no match, a missing file, or no pack" falls back to the device voice.
Nothing stops a book reaching the store with a missing pack, a stale pack (the text changed after the audio was made)
or a pack missing some sentences. Slice 278 removes the device voice, so such a gap would become silence.

## Decisions (the user, 2026-11-06)

- The store must be complete: every book, every language it is offered in, every sentence has its clip.
- There is no fallback in the app. The check before publishing is what guarantees it.

## Work

- [ ] A1. BookBuilder `check-audio <stem> [--lang <code>]`: split the book exactly as the app does (the same code
  `dump-sentences` uses), and for each language the listing offers, compare every sentence's hash with that
  language's clip manifest. Report missing packs, missing clips, clips for text that no longer exists (stale), and
  zero-length or unreadable clips. Exit non-zero on any gap.
- [ ] A2. `StoreValidation` runs the same check for every book it builds, so a gap fails the build of the store
  upload, not just a separate command.
- [ ] A3. Both upload paths refuse a book with a gap: `tools/upload-store-story.ps1` (by hand) and
  `publish-store-books.mjs` in `dino-store.yml` (CI). The error names the book, language and first missing
  sentences. A failed check uploads nothing for that book (meta.json stays as it is), and the other books still go.
- [ ] A4. Tests: a book with a complete pack passes; one missing sentence, a stale clip, a missing language pack
  and an empty clip each fail with the right message.
- [ ] A5. Run `check-audio` over every store book today (12 store books plus the 4 former starter books, 15
  languages each). Write the result into this file. Any gap: generate the missing clips with `tools/premade-audio`,
  upload them (standing approval: store and app must match), and re-run until the whole store is clean.
- [ ] A6. `create-dino-story` and `src/DinoReader.App/CLAUDE.md` say that a book reaches the store only through
  this check.

## Verification

- `dotnet test tests/DinoReader.Tests` passes with the new tests.
- `check-audio` over the whole store prints no gap (A5's result, recorded here).
- A deliberately broken copy of one book (one clip removed) is refused by `upload-store-story.ps1 -WhatIf` (or its
  dry-run mode) and by the `publish-store-books.mjs` test.
