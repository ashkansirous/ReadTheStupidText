# Slice 158 — Dino: publish store books on every merge

*In progress (wave 2026-10-27c), draft PR.* Dino Reader only. Small, CI only; can run beside other work (it touches no book content).

**Batch:** 34 (store plumbing, 2026-10-27; Decision 163)
**Touches:** `.github/workflows/release.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`,
`.github/scripts/publish-store-books.mjs` (new), `.github/scripts/publish-store-books.test.mjs` (new),
`tools/upload-store-story.ps1`, `.github/CLAUDE.md`, `CLAUDE.md`

## Why

Nothing publishes store books. `release.yml` ships the apps, `packs.yml` the voice packs and Workers Builds the site, but a
store book only reaches `dinoreader.sirous.uk/api/stories` when someone runs `tools/upload-store-story.ps1` by hand, and
that upload is blocked for agents as a production deploy. So every rewrite bumped a version on `main` and none was
uploaded: on 2026-10-27 all 15 store books were behind (X Marks the Treasure v1 live, v6 on `main`, still spelled
"Kaatrin"). The user: "store and in app stories should always be on the same latest versions."

Grounding (2026-10-27): `release.yml` is the only workflow on push to `main` (`.github/CLAUDE.md`), so this is a job in
it, not a new workflow. Its `publish-downloads` job already writes the same bucket (`readthestupidtext-downloads`,
`dinoreader/` prefix) with `CLOUDFLARE_API_TOKEN`/`CLOUDFLARE_ACCOUNT_ID` (`publish-downloads.mjs:44`), so no new token
is needed. The store builder runs on Linux (`dinoreader.yml` already runs `tests/DinoReader.Tests` on ubuntu).

## Decisions (Decision 163)

1. **A merged version bump is the approval.** A `publish-store` job in `release.yml` uploads every store book whose built
   version is higher than the live one. No per-upload OK.
2. **It compares with the live store every run**, not with the diff, so a cancelled run (a later merge cancels the
   in-flight `release.yml`) or a missed book is caught by the next run. A run with nothing behind uploads nothing.
3. **Triggered by a new `dinostore` ship target**: store/starter texts, voices, pictures, translations, the builders,
   `src/DinoReader.Core/` (the package format) and the upload script. `dinostore` alone never cuts an app release.
   `workflow_dispatch` with `force_all` also runs it (a manual catch-up).
4. **Same rules as the hand upload:** cover and package first, `meta.json` last; never overwrite an existing
   `story-v<n>.dino`; never upload a lower version than live. Reuse `CLOUDFLARE_API_TOKEN`; skip with a step-summary
   warning when it is unset. No Actions artifacts (quota rule).
5. Slice 157 later only repoints the job at `content/dino/` and its `BookBuilder build`.

## Work

- [ ] `publish-store-books.mjs`: reads `artifacts/store/*/meta.json`, fetches `/api/stories`, and returns the ids whose
      built version is higher than live (pure function, plus a small runner that calls `wrangler r2 object put` in the
      upload order). A `--dry-run` that prints the plan. Unit tests for the comparison (new book, behind, equal, live
      newer, bad meta).
- [ ] `ship-targets.mjs`: `dinostore` target and its paths; tests in `ship-targets.test.mjs`.
- [ ] `release.yml`: `publish-store` job on ubuntu, `needs: [version, dinoreader]`, runs on `main` when `dinostore` (or
      `force_all`); builds with `DINO_BUILD_STORE=1 dotnet test tests/DinoReader.Tests --filter BuildStore`, installs
      wrangler (`npm ci --ignore-scripts`), runs the script, then re-reads `/api/stories` and fails if any built book is
      still behind. Writes the uploaded ids and versions to the step summary. Its own `concurrency` group, so two runs
      never upload at once. actionlint clean.
- [ ] `tools/upload-store-story.ps1` stays for manual use; note in its header that CI does this on merge.
- [ ] Docs: `.github/CLAUDE.md` (the job, its trigger, the order rule), root `CLAUDE.md` store paragraph (uploads are
      automatic on merge; the manual script is a fallback).
- [ ] First run: after the merge, `release.yml` (or a `workflow_dispatch`) uploads all 15 lagging books; compare
      `/api/stories` with `StoreTexts.cs`/`StarterVersions` and confirm none lag; X Marks the Treasure reads "Kathryn".

## Verification

- `node --test .github/scripts/*.test.mjs` green; `actionlint` clean.
- On the merge run: the job lists the lagging books, uploads them, and the final check passes. A second run (dispatch)
  uploads nothing.
- `/api/stories` shows every book at its `main` version; the downloaded X Marks the Treasure package says "Kathryn".
- A docs-only merge does not start the job.

## Out of scope

- Moving book content out of the test project (Slice 157).
- A content-hash guard against a missed version bump (Slice 157).
- Deleting old `story-v<n>.dino` versions from R2.
