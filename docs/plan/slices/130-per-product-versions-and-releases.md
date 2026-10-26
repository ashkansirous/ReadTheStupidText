# Slice 130 — A version and a release per product

*In progress (wave 2026-10-26 C), draft PR.*

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
- [ ] `release.yml`: from the `ship-targets` outputs, run one release job per product that has a target;
      each computes its own version and drafts its own release. Carry-forward only within a product
      (e.g. RTST Android unsigned carries the earlier RTST `.aab`).
- [ ] Baseline tags `rtst-v<current>` and `dino-v<current>` at the last `v*` tag commit.
- [ ] `worker/downloads.ts` (and site links): resolve "latest" per product from its own tag prefix.
- [ ] Update `.github/CLAUDE.md`, the root `CLAUDE.md` versioning section and `STORE.md`.

## Verification

- `release-scripts` tests cover the per-product decision; `actionlint` clean.
- After merge: a Dino-only commit cuts `dino-v…` with only `DinoReader*` assets and no RTST release; an
  RTST-only commit the reverse; a core change cuts both.
- Both landing sites' download buttons fetch the right latest file.
