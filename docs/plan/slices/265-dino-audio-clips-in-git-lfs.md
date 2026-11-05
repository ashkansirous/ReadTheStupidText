# Slice 265 — Dino: pre-made audio clips in Git LFS; book changes only publish to the store

*Built 2026-11-04, merged as PR #810 (61b9d48d). The clips shipped in 8 content-only PRs #815-#822 (chore/s265-clips-1..8; last merge 4e1480a3), so they are in. The clips are not in this PR (see Changed while building); first real runs of `dino-store.yml` and `packs.yml` not yet seen.* *Planned 2026-11-04.* Tooling and CI, no app change. After Slice 261 (the starters leave the app, so no book folder needs an app build any more).

**Touches:** `.gitattributes`, `.lfsconfig` (new), `content/dino/*/audio/**` (new, LFS), `tools/premade-audio/**`, `tools/DinoReader.BookBuilder/**`, `tools/upload-store-story.ps1`, `.github/workflows/dino-store.yml`, `.github/workflows/release.yml`, `.github/workflows/dinoreader.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `.github/scripts/dino-starters.json`, `.github/CLAUDE.md`, `tools/premade-audio/README.md`, `docs/plan/slices/265-dino-audio-clips-in-git-lfs.md`.

## Why

The pre-made audio's single-sentence clips (about 510 MB, 16 books x 15 languages, Slice 246) live only in `D:/ReadTheStupidText-worktrees/s246-audio/`, outside git, with no backup. Losing that folder loses the four starter books' packs (never uploaded) and the cache that lets an edited sentence be re-recorded alone. Slice 246's plan put them outside git with no reason recorded (2026-11-04, the user asked why).

## Decisions (user, 2026-11-04)

- Keep the clip cache in **Git LFS**, under each book's folder (`content/dino/<book>/audio/<lang>/`). The packs (zips, about 467 MB) are rebuilt from the clips and stay out of git; they live in R2 as today.
- A normal checkout does not download the clips: a committed `.lfsconfig` sets `lfs.fetchexclude` for `content/dino/**/audio/**`; only the audio tools and the store upload fetch them (`git lfs pull --include`). This keeps the night run's many worktrees small and the free LFS allowance (10 GiB storage, 10 GiB download a month on GitHub Free, read 2026-11-04) safe.
- **A change to a book's text or audio runs only the store upload**, as store books do today (`dino-store.yml`, Slice 239): no app build, no test run. With the starters gone (Slice 261) that holds for every book.

## Work

- [x] LFS: `.gitattributes` tracks `content/dino/**/audio/**`; `.lfsconfig` excludes them from normal fetches; move the clips from `s246-audio` into the books' folders (manifests too), commit through LFS; the folder outside git stays until the commit is pushed and read back. *(built: 18,251 files, 16 books x 15 languages, 18,011 clips + 240 manifests, about 510 MB; the Persian clips of three-little-pigs come from `pigs-fa/`, those of town-mouse-country-mouse from `books/`; the `s246-audio` folder is untouched. The clips are committed through LFS on the local branch `s265-with-clips` but are NOT in PR #810; see Changed while building.)*
- [x] Tools: `tools/premade-audio` reads and writes the cache in `content/dino/<book>/audio/`; BookBuilder `build-audio` packs from there; README says how to fetch the clips for one book. *(`generate.py` defaults to `content/dino/<book>/audio/<lang>`, `--out` keeps a scratch root; `build-audio --clips` is optional and refuses an LFS pointer file.)*
- [x] CI: remove the starters' special handling (`dino-starters.json`, `STARTER_STEMS`, `STARTER_CONTENT_PATHS`, the starter folders in `release.yml`'s and `dinoreader.yml`'s paths); `ship-targets.mjs` maps every `content/dino/**` path, audio included, to `dinostore` only; tests assert an audio-only and a text-only change start no app build and no test workflow. *(`dino-starters.json` itself stays: `BookBuilderTests` (outside this slice) and `publish-store-books.mjs --check-content` read it until Slice 261 lands and a follow-up removes it.)*
- [x] `dino-store.yml`: for each changed book, `git lfs pull --include content/dino/<book>/audio/**`, build its packs and upload them with the book (today packs go up by hand with `tools/upload-store-story.ps1`). *(`--audio-behind` picks the books/languages whose pack the live store lacks; their clips are fetched alone, packed before `build`, and uploaded before the cover, package and meta.)*
- [x] Upload the four former starter books' packs (built 2026-11-04, PR #792) once they are store books. *(already live per the claim note; nothing built here.)*
- [x] `.github/CLAUDE.md`: the content-only rule and the LFS fetch rule.

## Claim note (coordinator, 2026-11-04, night 2026-11-04-1441 wave 2)

- Built alongside Slice 261 (PR #808, in review), which owns `tools/DinoReader.BookBuilder/StarterBuilder.cs`, the starter command in `tools/DinoReader.BookBuilder/Program.cs` and `src/DinoReader.App/Resources/Raw/starters/` (moved to Debug-only `testbooks/`). This slice does not edit those two files; their starter code goes in a follow-up once both merge, if any is left. Every other part of BookBuilder stays in Touches.
- CI order: removing the starter handling in `ship-targets.mjs` / `release.yml` / `dinoreader.yml` is safe before 261 merges only if a change under `content/dino/<starter>/` still gets the store upload; the builder checks `ship-targets.test.mjs` for the four starter books after the change.
- The four former starter books' packs were uploaded live in night 2026-11-04-1441 wave 1 (`/api/stories` lists 15 audio languages each), so that work item is already done; the builder only confirms it.
- The clips come from `D:/ReadTheStupidText-worktrees/s246-audio/` (978 MB with packs). That folder stayed until the LFS commit was pushed and read back (`git lfs ls-files` on a fresh clone of the branch). Read back 2026-11-04: a fresh worktree of 4e1480a3 lists 18,257 LFS files, all pointers, none downloaded; the folder is safe to delete.

## Verification

- `node --test .github/scripts/*.test.mjs`; a fresh `git worktree add` downloads no clips (`git lfs ls-files` shows them as not fetched); a test PR that changes one clip runs only `dino-store.yml`'s checks.

## Changed while building

- **Clips are not in this PR.** 18,011 clips + 240 manifests (16 books x 15 languages, about 510 MB) are committed through LFS on the local branch `s265-with-clips`. The merge gate reads a PR's files via `gh` and `git` with a 1 MB buffer, and the clip commit lists 1.57 MB of paths. Changing the gate was denied by the auto-mode classifier (reason verbatim "[Self-Modification]"). Plan: after this PR merges, the clips follow in 8 content-only PRs of two books each (`content/dino/**` counts as docs through `contentPaths`). `s246-audio` stays until those are pushed and read back.
- **Built beyond the plan:** `.gitignore` entries for scratch files; `generate.py` and BookBuilder `build-audio` refuse LFS pointer files; `packs.yml` uses `lfs: false` with an explicit include/exclude; `dino-store.yml` builds only the packs the live store lacks (read from `/api/stories`), per book all-or-nothing, and failed pairs fail the job at the end; `publish-store-books.mjs` gained `pruneUnbuiltAudio` and `validPackVersions`.
- The four former starters' packs were already live (wave 1): `/api/stories` showed 16 books, 240 packs, 0 behind.
- `dino-starters.json` and `BookBuilderTests` still reference starters (follow-up after Slice 261).

## From review

Findings files: `D:/ReadTheStupidText-worktrees/reviews/s265-high.md`, `s265-medium.md`, `s265-check.md`, `s265-check2.md` (outside the repo; summarised here).

- **High (9 findings):**
  - 2 (generate.py reused LFS pointers as clips/manifest): applied.
  - 3 (stdin eaten by the `while read` loop in `dino-store.yml`): applied.
  - 4 (stale header comment in `dino-store.yml`): applied.
  - 5 (slow existence check and 30-minute timeout on the first run): applied.
  - 6 (meta listing packs that failed to build): applied.
  - 9 (`packs.yml` `lfs: true` might fetch the clips): applied (`lfs: false` plus explicit include/exclude).
  - 7 (`audio-cast.json` CRLF dirty file): no change.
  - 1 (starter folders no longer start an app build while the bundled starters still exist): skipped; Slice 261 merges first.
  - 8 (`IsLfsPointer` opens each clip more than once): skipped.
- **Medium (8 findings):**
  - 1 (`pruneUnbuiltAudio` should keep live >= meta), 3 (`git lfs pull` failure handling), 4 (packs built for books with other outcomes), 5 (NaN version in `readAudioRecords`), 8 (dry-run differs from the real run): applied.
  - 2 (starter edits no longer run the Dino suite, needs 261 first): skipped.
  - 6 (`lfs_pointer_in` scans every clip): skipped.
  - 7 (`generate.py --format wav` output is gitignored): skipped.
- **Check pass 1 (`s265-check.md`, REVIEW INCOMPLETE, not counted as a gate pass):** 1 (one failed pack fails the step and skips build/upload): applied as per-book staging. 2 (30-minute timeout when clips arrive): skipped, all packs are already live so the first run builds none.
- **Check pass 2 (`s265-check2.md`, complete, nothing blocking):** 1 (starter edits ship only the store upload): handled by merge order (261 first). 2 to 8, all low, skipped: 2 (a failed pair drops the book's new packs for that run, by design), 3 (`pruneUnbuiltAudio` can advertise a lower version than live), 4 (`audioUploadsFor` treats an absent zip as live), 5 (`lfs_pointer_in` opens every clip), 6 (live list fetched more than once), 7 (docs still say a starter edit builds the app: `.claude/skills/create-dino-story/SKILL.md:64`, `src/DinoReader.App/CLAUDE.md:84`; left for the index pass), 8 (wav ignore needs a README note).

Verified locally: a fresh worktree downloads no clips; the audio loop run on real clips for billy-goats-gruff en/da built both zips; a bad language failed the book with nothing copied. 249 script tests, 120 premade-audio tests, actionlint clean.

#### For CLAUDE.md (lifted 2026-11-04)

- `.github/CLAUDE.md`: a change under `content/dino/**`, audio included, starts only `dino-store.yml`, never an app build or test run; this holds only once the starters are out of the app (Slice 261). (Slice 265: docs/plan/slices/265-dino-audio-clips-in-git-lfs.md)
- `.github/CLAUDE.md`: `content/dino/*/audio/**` is in Git LFS and `.lfsconfig` excludes it from normal fetches; a checkout that needs clips runs `git lfs pull --include`, and one that does not uses `lfs: false` (never `lfs: true`). (Slice 265: docs/plan/slices/265-dino-audio-clips-in-git-lfs.md)
- Clips committed under `content/dino/*/audio/**` go in content-only PRs of about two books each, because the merge gate's 1 MB path buffer fails on a huge commit. (Slice 265: docs/plan/slices/265-dino-audio-clips-in-git-lfs.md)

#### Not yet verified

- `dino-store.yml`'s first real run on main.
- `packs.yml`'s first run with `lfs: false`.
- `dino-starters.json` and `BookBuilderTests` still reference starters (follow-up after 261).
