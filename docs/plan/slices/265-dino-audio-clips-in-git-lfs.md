# Slice 265 — Dino: pre-made audio clips in Git LFS; book changes only publish to the store

*In progress (wave 2026-11-04), draft PR.* *Planned 2026-11-04.* Tooling and CI, no app change. After Slice 261 (the starters leave the app, so no book folder needs an app build any more).

**Touches:** `.gitattributes`, `.lfsconfig` (new), `content/dino/*/audio/**` (new, LFS), `tools/premade-audio/**`, `tools/DinoReader.BookBuilder/**`, `tools/upload-store-story.ps1`, `.github/workflows/dino-store.yml`, `.github/workflows/release.yml`, `.github/workflows/dinoreader.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `.github/scripts/dino-starters.json`, `.github/CLAUDE.md`, `tools/premade-audio/README.md`, `docs/plan/slices/265-dino-audio-clips-in-git-lfs.md`.

## Why

The pre-made audio's single-sentence clips (about 510 MB, 16 books x 15 languages, Slice 246) live only in `D:/ReadTheStupidText-worktrees/s246-audio/`, outside git, with no backup. Losing that folder loses the four starter books' packs (never uploaded) and the cache that lets an edited sentence be re-recorded alone. Slice 246's plan put them outside git with no reason recorded (2026-11-04, the user asked why).

## Decisions (user, 2026-11-04)

- Keep the clip cache in **Git LFS**, under each book's folder (`content/dino/<book>/audio/<lang>/`). The packs (zips, about 467 MB) are rebuilt from the clips and stay out of git; they live in R2 as today.
- A normal checkout does not download the clips: a committed `.lfsconfig` sets `lfs.fetchexclude` for `content/dino/**/audio/**`; only the audio tools and the store upload fetch them (`git lfs pull --include`). This keeps the night run's many worktrees small and the free LFS allowance (10 GiB storage, 10 GiB download a month on GitHub Free, read 2026-11-04) safe.
- **A change to a book's text or audio runs only the store upload**, as store books do today (`dino-store.yml`, Slice 239): no app build, no test run. With the starters gone (Slice 261) that holds for every book.

## Work

- [x] LFS: `.gitattributes` tracks `content/dino/**/audio/**`; `.lfsconfig` excludes them from normal fetches; move the clips from `s246-audio` into the books' folders (manifests too), commit through LFS; the folder outside git stays until the commit is pushed and read back. *(built: 18,251 files, 16 books x 15 languages, 18,011 clips + 240 manifests, about 510 MB; the Persian clips of three-little-pigs come from `pigs-fa/`, those of town-mouse-country-mouse from `books/`; the `s246-audio` folder is untouched.)*
- [x] Tools: `tools/premade-audio` reads and writes the cache in `content/dino/<book>/audio/`; BookBuilder `build-audio` packs from there; README says how to fetch the clips for one book. *(`generate.py` defaults to `content/dino/<book>/audio/<lang>`, `--out` keeps a scratch root; `build-audio --clips` is optional and refuses an LFS pointer file.)*
- [x] CI: remove the starters' special handling (`dino-starters.json`, `STARTER_STEMS`, `STARTER_CONTENT_PATHS`, the starter folders in `release.yml`'s and `dinoreader.yml`'s paths); `ship-targets.mjs` maps every `content/dino/**` path, audio included, to `dinostore` only; tests assert an audio-only and a text-only change start no app build and no test workflow. *(`dino-starters.json` itself stays: `BookBuilderTests` (outside this slice) and `publish-store-books.mjs --check-content` read it until Slice 261 lands and a follow-up removes it.)*
- [x] `dino-store.yml`: for each changed book, `git lfs pull --include content/dino/<book>/audio/**`, build its packs and upload them with the book (today packs go up by hand with `tools/upload-store-story.ps1`). *(`--audio-behind` picks the books/languages whose pack the live store lacks; their clips are fetched alone, packed before `build`, and uploaded before the cover, package and meta.)*
- [x] Upload the four former starter books' packs (built 2026-11-04, PR #792) once they are store books. *(already live per the claim note; nothing built here.)*
- [x] `.github/CLAUDE.md`: the content-only rule and the LFS fetch rule.

## Claim note (coordinator, 2026-11-04, night 2026-11-04-1441 wave 2)

- Built alongside Slice 261 (PR #808, in review), which owns `tools/DinoReader.BookBuilder/StarterBuilder.cs`, the starter command in `tools/DinoReader.BookBuilder/Program.cs` and `src/DinoReader.App/Resources/Raw/starters/` (moved to Debug-only `testbooks/`). This slice does not edit those two files; their starter code goes in a follow-up once both merge, if any is left. Every other part of BookBuilder stays in Touches.
- CI order: removing the starter handling in `ship-targets.mjs` / `release.yml` / `dinoreader.yml` is safe before 261 merges only if a change under `content/dino/<starter>/` still gets the store upload; the builder checks `ship-targets.test.mjs` for the four starter books after the change.
- The four former starter books' packs were uploaded live in night 2026-11-04-1441 wave 1 (`/api/stories` lists 15 audio languages each), so that work item is already done; the builder only confirms it.
- The clips come from `D:/ReadTheStupidText-worktrees/s246-audio/` (978 MB with packs). That folder stays until the LFS commit is pushed and read back (`git lfs ls-files` on a fresh clone of the branch).

## Verification

- `node --test .github/scripts/*.test.mjs`; a fresh `git worktree add` downloads no clips (`git lfs ls-files` shows them as not fetched); a test PR that changes one clip runs only `dino-store.yml`'s checks.
