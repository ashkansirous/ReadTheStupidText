# Slice 302 — Slice 239 leftovers: renamed store package downloaded twice, `dino-store.yml` paths

*In progress (wave 2026-11-07), draft PR.* Planned 2026-11-07. Two low items from [Slice 239](239-dino-store-book-without-app-build.md), moved from plan.md's *Outstanding work*.

**Touches:** `.github/workflows/dino-store.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/*.test.*`, `.github/CLAUDE.md`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/StarterStories.cs`, `tests/DinoReader.Tests/**`, `docs/plan/slices/302-slice-239-leftovers.md`

## Work

- [ ] 1. Android and iOS (same app code): a renamed store package is downloaded twice on a fresh install. Find where the first download (the shelf book) and the second (the renamed package id) both fire (`StoreClient`, `BookUpdater`) and make one download satisfy both. Test: a fresh install with a renamed package makes one request for it.
- [ ] 2. `dino-store.yml` `paths` omit `src/ReadTheStupidText.Application` (LineReflow), so an Application-only change waits for the daily run. Add the path, and the matching entry in `ship-targets.mjs` if it lists it separately; extend its test.

## Verification

- A request-count test for item 1; `ship-targets` tests show an Application-only change selecting the store job; `actionlint` on the workflow is clean.

## Claim note (wave 2026-11-07)

Premise check before the claim: item 2 confirmed (`dino-store.yml` push paths and the `dinostore` group in `ship-targets.mjs` both lack `src/ReadTheStupidText.Application`; a node test ties the two lists). Item 1 not confirmed: `StoreClient.DownloadAsync` skips a story already on the shelf and joins an in-flight download keyed by `item.Id`, and `BookUpdater.StageOneAsync` fetches by `item.DownloadUrl` unless staged; no "renamed package id" concept was found and Slice 239 never names one. The builder first traces whether a fresh install can fetch one package twice (StoreClient, BookUpdater, StarterStories); if it can, fix it with a request-count test; if not, it records where it looked and item 1 is closed as not reproducible in code.
