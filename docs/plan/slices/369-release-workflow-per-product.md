# Slice 369 — One release workflow per product, sharing a reusable part
**Product:** Shared

*Built 2026-11-13 (wave 55), merged as PR #1087 (3ba64b04); not yet run on GitHub.* Planned 2026-11-13. Part of the tidy-by-product slices (366-371). User's choice, 2026-11-13 (item 3 of 5).

**Touches:** `.github/workflows/release.yml`, `.github/workflows/release-rtst.yml` (new), `.github/workflows/release-dino.yml` (new), `.github/workflows/release-shared.yml` (new, `workflow_call`), `.github/workflows/dino-store.yml`, `.github/workflows/store-submit.yml`, `.github/workflows/testflight-external.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `.github/scripts/find-release.sh`, `.github/scripts/check-called-workflow-permissions.mjs`, `.github/CLAUDE.md`, `.github/scripts/check-called-workflow-permissions.test.mjs`, `.github/scripts/site-checks-needed.mjs`, and the path filters / `workflow_call` callers in `.github/workflows/android.yml`, `ios.yml`, `windows.yml`, `dinoreader.yml`, `dinoreader-android.yml`, `dinoreader-ios.yml`, `dinoreader-windows.yml`, `tests.yml`, `site-checks.yml`, `release-scripts.yml`, `tools-selftests.yml`, `packs.yml`, and the docs that name `release.yml`: `STORE.md`, `docs/voice-packs.md` (premise check 2026-11-13: no file under `tools/` or `.claude/` names `release.yml`)

## Why

`release.yml` is 992 lines and mentions Dino 189 times; one run computes both versions, builds both apps and submits both to the stores. A Dino-only fix and an RTST-only fix read, run and fail in the same file and the same run (e.g. run 42410230793: an RTST test failure skipped every RTST build while Dino shipped). Per product tags (`rtst-v`, `dino-v`) already exist (Slice 130, Decision 137).

## Rules this must keep (read `.github/CLAUDE.md` before starting)

- Only changed platforms are rebuilt and submitted (`ship-targets.mjs`); docs-only merges cut nothing.
- Actions storage and minutes quota: no build-output artifacts, Release assets only; path filters per platform.
- Wave merges carry the skip-CI marker and release in one batched `workflow_dispatch`; that batch must still release every product whose files changed (one dispatch that starts both, or one per product, never a lost product). `batch-release.mjs` (ClaudeSkills `skills/run-wave/`, lines ~233-245) dispatches each workflow whose `push` filters match the batch's changed files and errors on one with no `workflow_dispatch`; it names no workflow file, so it needs no change as long as both product workflows carry `push` paths and `workflow_dispatch`. With an unknown batch base (line ~318) it dispatches every matching workflow.
- Versions never go backwards; Android `versionCode` keeps rising.
- iOS signing, Apple and Play secrets stay repo-level and are shared.
- A merge cancelling an in-flight release is fine; the two product workflows get separate concurrency groups so one product never cancels the other's run.

## Work

- [x] 1. Map every job and step of `release.yml` to RTST, Dino or Shared (version calc, tests, packs check, each platform build, each store submit, GitHub Release, tag). Write the map into this file before changing anything.
- [x] 2. `release-shared.yml` (`workflow_call`): the steps both need (checkout, GitVersion with the product's config, tests, `ship-targets` decision, GitHub Release creation with the product's tag prefix).
- [x] 3. `release-rtst.yml` and `release-dino.yml`: push to `main` with their own path filters, plus `workflow_dispatch`; each calls the shared part and then only its own build and submit jobs. Shared code paths trigger both.
- [x] 4. Retire `release.yml` (removed, not kept as a copy). Update every caller and reader: `dino-store.yml`, `store-submit.yml`, `testflight-external.yml`, `find-release.sh`, `check-called-workflow-permissions.mjs` and its test, `site-checks-needed.mjs`, and every build/test workflow whose `paths` filter or comments name `release.yml` (list in Touches): their `- '.github/workflows/release.yml'` path lines are functional and must name the new files. `tests.yml` is called twice by `release.yml` (shared-test and RTST test jobs); keep both calls. Rewrite `release.yml`'s own `gh run list --workflow release.yml` line (~195) to the new names. The `run-wave` batched release (ClaudeSkills `batch-release.mjs`) names no workflow file: verify, no change expected.
- [x] 5. `ship-targets.mjs` returns targets per product; `ship-targets.test.mjs` covers: Dino-only change runs only the Dino workflow's builds; tray-only change only RTST; shared engine change both; docs-only none.
- [x] 6. `.github/CLAUDE.md`: replace the `release.yml` rules with the new file names.

## Job map of the old `release.yml` (item 1, written before the split; built 2026-11-13)

Old job -> where it lives now. S = `release-shared.yml` (both products, one `product` input), R = `release-rtst.yml`, D = `release-dino.yml`.

| Old job / step | Owner | Now |
| --- | --- | --- |
| `version`: refuse off main, wait for older runs of the other event type | Shared | S `version` (lists runs of the caller's `workflow_file`, so a product never waits for the other) |
| `version`: full checkout, bootstrap baseline tags | Shared | S `version` (only the product's prefix) |
| `version`: GitVersion `GitVersion.yml` / `GitVersion.dino.yml`, Android versionCode | Both, one each | S `version` runs one GitVersion step with the product's config; one versionCode |
| `version`: `ship-targets.mjs` (decides what ships) | Shared | S `version`, `PRODUCT=rtst|dino`, prints only that product's targets |
| `version`: `site-checks-needed.mjs` | RTST | S `version` step `site` (RTST only, `continue-on-error`, output `site_checks`); R `site-checks` runs on `!cancelled()` so advisory stays advisory (review #4: it was a separate `site-plan` job) |
| `shared-test` (tests.yml suite shared) | Shared | S |
| `rtst-test` (tests.yml suite rtst) | RTST | S (its `if` is false for Dino); both `tests.yml` calls kept |
| `dinoreader` (Dino's suite) | Dino | S (false for RTST) |
| `site-checks` | RTST | R |
| `release-draft-rtst` / `release-draft-dino` (secrets check, draft release, carry-forward) | Shared, product-specific values | S `release-draft`: one job, signing secrets and asset names chosen by `product`; outputs `proceed`, `android_signed`, `ios_signed` |
| `build-windows`, `build-android`, `build-ios` | RTST | R (same `if`, `needs.shared.*`) |
| `build-dinoapp`, `build-dinoios`, `build-dinowin` | Dino | D |
| `release-publish-rtst` / `release-publish-dino` (stale-draft guard, asset checks, publish = creates the tag) | one per product | R / D |
| `store-submit` (Microsoft Store), `store-submit-mark` (`rtst-store-v` marker) | RTST | R |
| `store-submit-dino` | Dino | D |
| `play-submit` / `play-submit-dino` | RTST / Dino | R / D |
| `testflight-submit` / `testflight-submit-dino` | RTST / Dino | R / D |
| workflow-level `concurrency` (dispatch, fix merge, push groups) | per product | R: `release-rtst-dispatch-<ref>`, `release-rtst-fix-<sha>`, `release-rtst-<ref>`; D: the same with `release-dino-`; S has none |
| `on: push` paths (`**` minus `content/dino/**`) | per product | R: `**` minus Dino-only files and `content/dino/**`; D: derived from `GROUPS` |

Behaviour that changes, by design of the split: a Dino-only push no longer starts an RTST run, so a pending Microsoft Store submission (Slice 218) waits for the next RTST release instead of riding a Dino-only one; `force_all` forces one product per workflow instead of both.
Also changes (review #1, accepted): a push that starts both products (a core change) runs the `shared` suite twice on `windows-latest`, once per product run; the two runs are independent.
Preconditions of the first run on main: none to create (no tag, secret or setting; the baseline tags already exist and `bootstrap-baseline-tags.sh` still creates a missing one). Group names are new, so a still-running old `release` run is not queued behind (it is gone with this merge).

*From review:* three passes read in full (`reviews/s369-high.md` 5/5, `reviews/s369-medium.md` 3/3, `reviews/s369-check1-medium.md` 5/5).
- High #1 accepted as a behaviour change (shared suite runs once per product workflow, see above); #2, #3, #4 applied.
- Skipped #5 (`reviews/s369-high.md`): "~40 lines each of the same script" (the duplicate `release-publish-dino` and `release-publish-rtst` jobs) — a refactor for its own sake; the two copies work and differ in product constants.
- Medium #2 applied (a drift test compares both push path lists with `GROUPS`). Medium #1 answered: origin/main's `release.yml` push filter was `**` plus `!content/dino/**` only, so docs-only merges also started a run that cut nothing; `release-rtst.yml` mirrors it.
- Skipped #3 (`reviews/s369-medium.md`): "plansFromEnv plans both products when PRODUCT is unset" — over-builds when unset but never loses a product; `release-shared.yml` always sets it.
- Skipped #1 (`reviews/s369-check1-medium.md`): "a failed shared test in one product's run leaves the two products' runs for one commit disagreeing" — a rerun of the failed product's workflow recovers it.
- Skipped #2 (`reviews/s369-check1-medium.md`): "bootstrap-baseline-tags.sh is not in CORE_PATHS/GROUPS, so editing it starts no Dino run" — the script only matters before the first tags exist, and they do.
- Skipped #3 (`reviews/s369-check1-medium.md`): "build-dinoapp/build-dinoios run on a duplicate-version re-run (proceed=false), wasting minutes" — same as the old behaviour, not a regression.
- Skipped #4 (`reviews/s369-check1-medium.md`): "store-submit-mark does not check release-publish-rtst released" — origin/main's `store-submit-mark` had the same `needs: [version, store-submit]` and `if: needs.store-submit.result == 'success'` (release.yml:820-821).
- Skipped #5 (`reviews/s369-check1-medium.md`): "still names release.yml's publish-store job" — queued for the index pass (see below).

Checks run: `node --test .github/scripts` 356/356, `actionlint` clean, `check-called-workflow-permissions` OK.

## Verification

- `actionlint` and the `.github/scripts` tests pass.
- First merge after this slice: a Dino-only PR starts only `release-dino.yml` and cuts only a `dino-v` tag; the following RTST-only PR only `release-rtst.yml` and an `rtst-v` tag (read with `gh run list` and `gh release list`). Record both run ids here.
- One batched `workflow_dispatch` from a wave releases both products.
- No version goes backwards (compare the new tags with the last `rtst-v` and `dino-v`).

## For CLAUDE.md (lifted 2026-11-13)

Not edited on this branch (the root file changed on main in another slice); the index pass applies these replacements:

- line 54: `- `.github/CLAUDE.md` — CI quota rules, `release-rtst.yml`/`release-dino.yml`/`release-shared.yml`/`ship-targets.mjs`, path filters, iOS signing and toolchain, packs releases, APK downloads, actionlint.`
- line 170: `The `tests.yml` workflow (called by `release-shared.yml`, once per product run) runs them on every push/PR that`
- line 190: replace "The single `release.yml` run does it all:" with "Each product's release run (`release-rtst.yml`, `release-dino.yml`, both calling `release-shared.yml`; Slice 369) does it all:".

## For README.md (lifted 2026-11-13)

Line 136, replace "`release.yml` runs on every push to `main` and calls a reusable workflow per platform (`windows.yml`, `android.yml`, `ios.yml`)" with "`release-rtst.yml` (Read The Stupid Text) and `release-dino.yml` (Dino Reader) run on every push to `main` that touches their files; each calls `release-shared.yml` and then a reusable workflow per platform (`windows.yml`, `android.yml`, `ios.yml`, and Dino's `dinoreader-*.yml`)".

## For the index pass (applied 2026-11-13)

- `docs/plan/unverified.md` line ~31 (Slice 368 block): "the new `release.yml` gates (`shared-test`, `rtst-test`)" now live in `release-shared.yml` (called by both product workflows); reword it and add the Slice 369 first-run check (the run ids named under Verification).
- `worker/CLAUDE.md` line 26 (not edited on this branch): "uploaded **automatically on every merge** by `release.yml`'s `publish-store` job" is stale; replace with "uploaded **automatically on every merge** by `dino-store.yml`'s `publish-store` job" (the store upload lives in `dino-store.yml`, Slices 239 and 369; check that file for the job's current name before writing it).
- Add the Slice 369 lines below to `docs/plan/unverified.md`.

#### Not yet verified

- First Dino-only merge runs only `release-dino.yml` and cuts only a `dino-v` tag.
- First RTST-only merge runs only `release-rtst.yml` and cuts only an `rtst-v` tag.
- One batched `workflow_dispatch` releases each product whose files changed.
- No version goes backwards (new tags compared with the last `rtst-v` and `dino-v`).
