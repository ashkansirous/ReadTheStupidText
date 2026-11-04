# Slice 258 — Dino: pre-made audio for the four starter books

*In progress (wave 2026-11-04), draft PR.* Follow-up to Slice 246 (parts A-D merged as #729, #768, #787, #790). Dino Reader only, Android and iOS alike (and the Windows head, which shares the code).

**Touches:** `tools/DinoReader.BookBuilder/**`, `src/DinoReader.App/StarterStories.cs`, `src/DinoReader.App/AudioPack*.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/BookUpdater.cs`, `content/dino/{gingerbread-man,good-enough,three-little-pigs,tortoise-and-hare}/audio-cast.json`, `tests/DinoReader.Tests/**`, `tools/upload-store-story.ps1`, `.github/scripts/publish-store-books.mjs`, `.maestro/dino/premade-audio-*.yaml`, `src/DinoReader.App/Testing/DinoTestCommands.cs`, `src/DinoReader.App/Reading*.cs`, `STORE.md`, `worker/CLAUDE.md` (doc lines only), `docs/plan/slices/258-dino-starter-books-premade-audio.md`. Widened at claim (2026-11-04) after a code check; the Worker code is not touched.

## Why

The four starter books (gingerbread-man, good-enough, three-little-pigs, tortoise-and-hare) are bundled in the app (`StarterStories`, `BookKind.Starter`). Slice 246's `build-audio` refuses non-store books, and part C's playback gives them no audio (Slice 246, *Known behaviours (part C)*). Their clips for all 15 languages are already recorded under `D:/ReadTheStupidText-worktrees/s246-audio/`, so only packing, delivery and playback are missing.

## Decide first (work item 1)

How the packs reach the app. Write the pick and the reason here before building:

- **Option 1: serve beside the store books** through the same Worker route (`GET /api/stories/<id>/audio/<lang>?v=<n>`), if `/api/stories` can list the starter ids; the app fetches them through the part C pack path. Keeps the app download small; needs the starter ids in the catalogue and `BookUpdater` / `StoreClient` to ask for packs of bundled books.
- **Option 2: ship in the app.** Bigger install on both platforms.

**Decided 2026-11-04 (wave session, obvious choice, user away): Option 1.** Checked against origin/main:
- The live `/api/stories` already lists the four starters (16 titles): `StoreBuilder.StarterEntries` (`tools/DinoReader.BookBuilder/StoreBuilder.cs:32`) gives them the bundled ids, so no catalogue change and no duplicate risk.
- The Worker serves a pack for any id with a valid `meta.json`, a package and a listed, uploaded pack (`worker/stories.ts:353-358`, `:203-205`, `:454-458`): no Worker change.
- `BookUpdater.SyncPacksAsync` (`BookUpdater.cs:239-255`) already fetches packs for every listed id on the shelf, and starters sit on the shelf with the package id as `SourceId` (`StarterStories.cs:235,269`). The builder checks the part C playback lookup for any starter exclusion and removes it if there is one.
- Option 2 would grow the install on Android, iOS and Windows for audio most readers never use, and needs a second load path.
- Delivery is the D4 route: `audioPacks` records in each starter's `audio-cast.json` merge first, then `tools/upload-store-story.ps1` uploads the zips and the meta (standing upload approval, memory *Store upload: always keep live*).

## Work

- [x] 1. Decide the delivery (above) and write it here. Option 1.
- [ ] 2. `build-audio` accepts the four starter books; their `audio-cast.json` files exist and match the recorded clips.
- [ ] 3. Build the packs from the recorded clips for all 15 languages.
- [ ] 4. Delivery per the decision, Android and iOS (and the Windows head) the same.
- [ ] 5. A starter book reads with the pre-made clip when the text hash matches, else the on-device voice (the part C rule).
- [ ] 6. Unit tests; a device flow modeled on `premade-audio-plays`.

## Not yet verified

(none yet)
