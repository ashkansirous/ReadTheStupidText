# Slice 277 — Dino: a store book is published only when every sentence in every language has pre-made audio
**Product:** Dino

*Built 2026-11-06 (wave 2026-11-06), merged as PR #858 (0f922e2d); verified on the first `dino-store.yml` run on main (run 42229431973, success).* Dino Reader only. Read The Stupid Text is not touched.

**Claim notes (wave 30, night 2026-11-06-1250):** a `check-audio [<stem>...]` command already exists (Slice 272,
`tools/DinoReader.BookBuilder/AudioAlignment.cs`, `Program.cs:22`); A1 extends it (missing packs, stale clips,
zero-length clips, every offered language) instead of adding a second command. This slice also takes Slice 275
item 6 (commit the live-store alignment scan as a `check-audio --live` mode; `reviews/lion-fa/live_scan.py`) and
item 8 (Goldilocks: live v12 vs repo v11; make store and repo match). Clip generation (`tools/premade-audio`,
Gemini TTS, `GEMINI_API_KEY`) is limited by its daily quota: gaps it cannot fill tonight are recorded here, not
faked.

**Depends on:** none. **Blocks:** Slice 278 part B (the voice engine leaves the app, so nothing may be missing).
**Touches:** `tools/DinoReader.BookBuilder/**` (`AudioAlignment.cs`, a new `AudioCoverage.cs` if useful, `StoreValidation.cs`, `BookCommands.cs`, `Program.cs`), `content/dino/goldilocks/**` (275 item 8), `content/dino/*/audio/**` (only to fill gaps A5 finds), `reviews/lion-fa/live_scan.py` (read only, 275 item 6), `tools/upload-store-story.ps1`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/ship-targets.test.mjs`, `.github/scripts/publish-store-books.test.mjs`, `.github/workflows/dino-store.yml`, `tests/DinoReader.Tests/**` (BookBuilder tests, new files), `tools/premade-audio/**` (only to fill gaps the first report finds), `.claude/skills/create-dino-story/SKILL.md`, `src/DinoReader.App/CLAUDE.md`, this file.

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

- [x] A1. BookBuilder `check-audio <stem> [--lang <code>]`: split the book exactly as the app does (the same code
  `dump-sentences` uses), and for each language the listing offers, compare every sentence's hash with that
  language's clip manifest. Report missing packs, missing clips, clips for text that no longer exists (stale), and
  zero-length or unreadable clips. Exit non-zero on any gap.
- [x] A2. `StoreValidation` runs the same check for every book it builds, so a gap fails the build of the store
  upload, not just a separate command.
- [x] A3. Both upload paths refuse a book with a gap: `tools/upload-store-story.ps1` (by hand) and
  `publish-store-books.mjs` in `dino-store.yml` (CI). The error names the book, language and first missing
  sentences. A failed check uploads nothing for that book (meta.json stays as it is), and the other books still go.
- [x] A4. Tests: a book with a complete pack passes; one missing sentence, a stale clip, a missing language pack
  and an empty clip each fail with the right message.
- [x] A5 (check run, nothing to generate; see Results). Run `check-audio` over every store book today (12 store books plus the 4 former starter books, 15
  languages each). Write the result into this file. Any gap: generate the missing clips with `tools/premade-audio`,
  upload them (standing approval: store and app must match), and re-run until the whole store is clean.
- [x] A6. `create-dino-story` and `src/DinoReader.App/CLAUDE.md` say that a book reaches the store only through
  this check.

## Verification

- `dotnet test tests/DinoReader.Tests` passes with the new tests.
- `check-audio` over the whole store prints no gap (A5's result, recorded here).
- A deliberately broken copy of one book (one clip removed) is refused by `upload-store-story.ps1 -WhatIf` (or its
  dry-run mode) and by the `publish-store-books.mjs` test.

## Results (builder, 2026-11-06)

Built: A1 (`check-audio` now fails on a language with no pack, a sentence with no clip, a clip for text that is gone, a missing or empty clip file; an LFS-pointer clip counts by the size its pointer records),
A2 (`build --live` skips a book with a gap via `StoreValidation.ProblemsByBook`), A3 (`publish-store-books.mjs --audio-verdict <file>, required, fail closed` refuses the book, including its meta.json, the other books go; `dino-store.yml` writes the file
with `check-audio --verdict` and fails the job at the end; `upload-store-story.ps1` runs the check first, `-CheckOnly` runs only it), A4 (`AudioCoverageTests`, 4 new `publish-store-books.test.mjs` tests), A6 (SKILL.md, App CLAUDE.md),
Slice 275 item 6 (`check-audio --live`, replaces `live_scan.py`, read only) and the repo side of item 8.

**The rule (one behaviour, every path; check1 and the coordinator's design):** a book whose id has no complete audio verdict changes NOTHING on R2, whatever its outcome (new upload, up-to-date
meta.json refresh, audio pack): no meta.json, no pack, no cover; its live copy stays as it is. `mayChangeOnR2` in `publish-store-books.mjs` decides it from the verdict, and `refuseAudioGaps`
(uploads, meta refresh) and `onlyCompleteBooks` (`--audio-behind`) both go through it. The other books go on; the job fails at the end naming each refused book, saying whether its audio is incomplete or
the verdict has no entry for its id. Accepted cost: a gap book's metadata change waits until its audio is fixed. `upload-store-story.ps1` also checks that the folder's audio zips have the sizes the book records.

**A5, read only:** `check-audio` over all 16 content books x 15 languages (12 store + 4 starters): 240 of 240 complete, no gap (clips counted from the LFS pointers' recorded sizes; manifests pulled).
`check-audio --live` (downloads the 240 live packs, checks every clip inside the zips): 240 of 240 complete, no gap. Nothing to generate or upload.

**Item 8 (Goldilocks), finding:** the live v12 against the repo's v11 is not a drift. A store book's live version is derived at publish time (Slice 239: live + 1 when the content hash changed), so the live version is normally
above `book.json`'s. The live Goldilocks audio lines up with the repo text in all 15 languages, so the live and repo content match. `--live` now reports only a live version BELOW book.json's (none today).

*Changed while building:* the publish rule above (one behaviour on every path: a book without a complete verdict changes nothing on R2; the job fails naming it; an up-to-date book with a gap fails the daily job until fixed) comes from the design file `D:/ReadTheStupidText-worktrees/reviews/s277-design.md` (outside the repo), written after two failed strikes at patching the publish refusal.

*From review* (files in `D:/ReadTheStupidText-worktrees/reviews/`):
- `s277-high.md`: all 10 findings applied.
- `s277-medium.md`: 3, 6, 7, 8 applied. Skipped: 1 (`AudioCoverage.cs`/`AudioPacks.cs:136` `RejectLfsPointer` plus the `lfs pull && build-audio` chain already guards a pointer clip at pack build); 2 (the starter books are published to the store too, `BookCommands.cs:38`, so checking them is right); 4 and 5 (low, tooling only: live-pack cache and `--live <stem>` parsing).
- `s277-check1.md`: 1 (the design), 4, 5, 7, 8 applied. Skipped: 2 (clip names come from trusted repo manifests), 3 (low, live-pack cache), 6 (low, the double check costs time only).
- `s277-check2.md`: 1, 2, 4 applied. Skipped: 3 (same as medium 1, an LFS pointer counts by its recorded size).
- `s277-check3.md`: all skipped. 1 intended (a PR that leaves a gap should fail); 2 wording only; 3 dead branch; 4 no two `content/dino` books share an id today (checked 2026-11-06); 5 and 7 low tooling; 6 a malformed live list aborts the read-only check, which fails closed.
- CI fix after the first push: the `test` job checks out without Git LFS, so `TheValidationByBook_IsEmptyForTheRepoAndNamesNoValidBook` saw every audio manifest as a pointer. `StoreValidation.ProblemsByBook(includeAudio)`: the repo-wide validation is text-only; `build --live` (after `dino-store.yml` pulls the manifests) and `check-audio` (the `audio-alignment` job pulls them) run the audio check.
- `s277-check4.md` (after the CI fix): all 5 skipped. 1 the default only affects the text-only repo test; publishing stays fail-closed through the verdict in `publish-store-books.mjs`; 3 a failed LFS pull writes no verdict, so nothing is published and the job prints the infrastructure message; 5 an exception in the audio check skips that book (fail closed); 2 and 4 low.

#### For CLAUDE.md (lifted 2026-11-06)

- A Dino store book reaches R2 only through `check-audio`'s verdict: `.github/scripts/publish-store-books.mjs` needs `--audio-verdict <file>` (written by `check-audio --verdict`), and a book with no complete verdict changes nothing on R2 (no meta.json, pack or cover); the job fails naming it. Do not add an upload path that skips the verdict. (Slice 277: docs/plan/slices/277-dino-store-audio-coverage-gate.md)

#### Not yet verified

- None left. Verified 2026-11-06: the first `dino-store.yml` run on main with the new steps (run 42229431973, push of 0f922e2d) ended `success`: "Check that every book's audio is complete", "Build the audio packs the live store lacks", "Build the store books" and the upload step succeeded; both "Fail when ..." steps were skipped.
