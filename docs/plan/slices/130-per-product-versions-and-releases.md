# Slice 130 — A version and a release per product

*Built 2026-10-26 (PR #367), not yet verified: actionlint and the first release run after merge.*

**Batch:** 27 (product separation, Decisions 133-137)
**Touches:** `GitVersion.yml`, `.github/workflows/release.yml`, `.github/scripts/ship-targets.mjs`, `.github/workflows/release-scripts.yml`, `.github/workflows/windows.yml`, `.github/workflows/android.yml`, `.github/workflows/ios.yml`, `.github/workflows/dinoreader-*.yml`, `worker/downloads.ts`, `worker/**` tests, `site/**` (download links only), `.github/CLAUDE.md`, `CLAUDE.md`, `STORE.md`

## Why

One GitVersion and one `v<x.y.z>` tag cover both apps. A Dino-only change bumps the shared version and
cuts a GitHub Release that copies in the latest RTST files (`release.yml` 283-315, `carry_forward`), so
RTST's release notes and download page change when nothing in RTST did. Dino's unsigned Windows build
always runs, so every Dino change cuts a release.

## Decisions

- **Decision 137:** each product has its own version, tag and GitHub Release: `rtst-v<x.y.z>` and
  `dino-v<x.y.z>`. A release carries only its own product's files; no carry-forward across products.
- Both continue from the current shared version (no number goes backwards: Android `versionCode` and
  Store versions must keep rising). The old `v*` tags stay as history.
- `+semver:` tokens still work; a token bumps whichever product the release run is cutting.

## Work

- [ ] Confirm via `library-docs` (context7) how GitVersion 6 computes a version from a tag prefix
      (`tag-prefix`) and whether two configs or an override per run is the standard way.
- [x] `release.yml`: from the `ship-targets` outputs, run one release job per product that has a target;
      each computes its own version and drafts its own release. Carry-forward only within a product
      (e.g. RTST Android unsigned carries the earlier RTST `.aab`).
- [x] Baseline tags `rtst-v<current>` and `dino-v<current>` at the last `v*` tag commit.
- [ ] `worker/downloads.ts` (and site links): resolve "latest" per product from its own tag prefix.
- [x] Update `.github/CLAUDE.md`, the root `CLAUDE.md` versioning section and `STORE.md`.

## Verification

- `release-scripts` tests cover the per-product decision; `actionlint` clean.
- After merge: a Dino-only commit cuts `dino-v…` with only `DinoReader*` assets and no RTST release; an
  RTST-only commit the reverse; a core change cuts both.
- Both landing sites' download buttons fetch the right latest file.

*Changed while building:* `GitVersion.yml` now uses tag prefix `rtst-v` and a new `GitVersion.dino.yml` uses `dino-v`; `release.yml` runs two GitVersion steps and a draft/publish/submit chain per product (RTST gated by `test`, Dino by `dinoreader`). Only the RTST release is marked latest; the README badge is filtered to `rtst-v`. Baseline tags are made by `.github/scripts/bootstrap-baseline-tags.sh` on the first run (not by hand): it tags the newest legacy `v*` commit as `rtst-v` and `dino-v` of the same version and pushes them (the version job has `contents: write`); it fails only if no legacy tag exists. `ship-targets.mjs` measures changes per product from its own newest tag, and a major token bumps only the product(s) whose paths the commit touches (core = both). Carry-forward stays inside a product (`find-release.sh` takes a tag regex; the first prefixed release falls back to legacy `v*`). `windows`, `android`, `ios` and `dinoreader-*` workflows upload to the product-prefixed tag; `store-submit.yml` takes a per-product tag regex. Files outside Touches that were needed: `ship-targets.test.mjs`, `windows.yml`, `dinoreader-windows.yml`, `find-release.sh`, `GitVersion.dino.yml`, the bootstrap script and its test, `tests.yml`/`dinoreader.yml` paths, `README.md`, `STORE.md`, comments in `packs.yml`/`ios.yml`. The context7 check and the `worker/downloads.ts` item were not reported done in the brief and stay unticked.

*Tests:* ship-targets 86, bootstrap 4, publish-downloads 15.

#### For CLAUDE.md (lifted 2026-10-26)

- Versions and tags are per product: `rtst-v<x.y.z>` (`GitVersion.yml`) and `dino-v<x.y.z>` (`GitVersion.dino.yml`); the old `v*` tags are history only. A release carries only its own product's files, and carry-forward (`find-release.sh` with a tag regex) never crosses products. Only the RTST release is marked latest. A major token bumps only the product(s) whose paths the commit touches (core = both). (Slice 130: docs/plan/slices/130-per-product-versions-and-releases.md)
- `.github/scripts/bootstrap-baseline-tags.sh` seeds `rtst-v`/`dino-v` from the newest legacy `v*` tag on the first run; do not hand-create baseline tags. (Slice 130: docs/plan/slices/130-per-product-versions-and-releases.md)

#### For README.md (lifted 2026-10-26)

- The release badge is filtered to `rtst-v` tags (already edited on the branch).

#### Not yet verified

- `actionlint` on the changed workflows (runs in CI).
- First release run after merge: bootstrap tags are created, both versions continue from the last shared version, the right targets build, the RTST release is latest and the Dino release is not.
- The `gittools` `configFilePath` input on the real runner.
