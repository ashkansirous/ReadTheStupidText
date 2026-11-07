# Slice 302 — Slice 239 leftovers: renamed store package downloaded twice, `dino-store.yml` paths

*Built (wave 2026-11-07, night 2026-11-07-1412), PR #906.* Planned 2026-11-07. Two low items from [Slice 239](239-dino-store-book-without-app-build.md), moved from plan.md's *Outstanding work*.

**Touches:** `.github/workflows/dino-store.yml`, `.github/scripts/ship-targets.mjs`, `.github/scripts/*.test.*`, `.github/CLAUDE.md`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/StarterStories.cs`, `tests/DinoReader.Tests/**`, `docs/plan/slices/302-slice-239-leftovers.md`

## Work

- [x] 1. (Closed in code: no second download path found, no change; not reproduced on a device, see Not yet verified.) Android and iOS (same app code): a renamed store package is downloaded twice on a fresh install. Find where the first download (the shelf book) and the second (the renamed package id) both fire (`StoreClient`, `BookUpdater`) and make one download satisfy both. Test: a fresh install with a renamed package makes one request for it.
  *Where I looked (2026-11-07):* every package fetch in `src/` is `StoreClient.FetchPackageAsync`, called from only three places. (a) `StoreClient.RunDownloadAsync` (line 178): returns `Done` without a request when `InstalledIdsAsync()` already holds the id, and `DownloadAsync` joins an in-flight download keyed by `item.Id`; its callers (`FirstBooksPage`, `StorePage`, test hooks) go through it. (b) `BookUpdater.StageOneAsync` (371, via `FetchPackageFileAsync`): skips when the staged version is current, and runs only for items the update plan marks `Update`, i.e. a book already on the shelf, never a fresh install. (c) `BookUpdater` baseline fetch (320): the older installed version's URL, to record a text hash, a different package from the update. `StarterStories` makes no fetch (it only records versions, `QueueStartersForStoreAsync`). The download and the update key by the store's `item.Id`; there is no separate "renamed package id" path. A request-count test would pin no bug, so none was added.
- [x] 2. `dino-store.yml` `paths` omit `src/ReadTheStupidText.Application` (LineReflow), so an Application-only change waits for the daily run. Add the path, and the matching entry in `ship-targets.mjs` if it lists it separately; extend its test.

## Verification

- Item 1 is closed without a test (nothing found to pin). `ship-targets` tests show an Application-only change selecting the store job (the 'Application core' row and a dedicated `dinostore` assertion). `actionlint .github/workflows/dino-store.yml` ran clean (exit 0, 2026-11-07).

## Not yet verified

- Item 1 was closed from a code read only: the "renamed store package downloaded twice" report was not reproduced on a device run, so a double download on a fresh install is not ruled out.

## Review notes

- High review finding 1 (whole `src/ReadTheStupidText.Application/` folder in the `dinostore` trigger) kept as is: the Dino app and `DinoReader.Core` reference the whole Application project, so a broad trigger only costs an extra run while a narrow one can miss a change.
- High review 2-5 applied (Not yet verified line, Verification text, `.github/CLAUDE.md` dino-store paragraph, dedicated `dinostore` test). Findings: `reviews/s302-high.md`.
- Medium review (`reviews/s302-medium.md`) all skipped: 1, item 1 is already marked closed-without-test with a Not yet verified line; 2, same reason as high 1; 3, the `.github/CLAUDE.md` paragraph documents the trigger; 4, speculative, the existing Dino rows already pin the other targets.

## Claim note (wave 2026-11-07)

Premise check before the claim: item 2 confirmed (`dino-store.yml` push paths and the `dinostore` group in `ship-targets.mjs` both lack `src/ReadTheStupidText.Application`; a node test ties the two lists). Item 1 not confirmed: `StoreClient.DownloadAsync` skips a story already on the shelf and joins an in-flight download keyed by `item.Id`, and `BookUpdater.StageOneAsync` fetches by `item.DownloadUrl` unless staged; no "renamed package id" concept was found and Slice 239 never names one. The builder first traces whether a fresh install can fetch one package twice (StoreClient, BookUpdater, StarterStories); if it can, fix it with a request-count test; if not, it records where it looked and item 1 is closed as not reproducible in code.
