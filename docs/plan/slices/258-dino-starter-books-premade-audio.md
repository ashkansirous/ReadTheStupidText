# Slice 258 — Dino: pre-made audio for the four starter books

*Planned 2026-11-04.* Follow-up to Slice 246 (parts A-D merged as #729, #768, #787, #790). Dino Reader only, Android and iOS alike (and the Windows head, which shares the code).

*Night 2026-11-03-2349 wave 4: built and reviewed on PR #792; merge denied by the auto-mode classifier; parked.*

**Touches:** `tools/DinoReader.BookBuilder/**`, `src/DinoReader.App/StarterStories.cs`, `src/DinoReader.App/AudioPack*.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/BookUpdater.cs`, `content/dino/{gingerbread-man,good-enough,three-little-pigs,tortoise-and-hare}/audio-cast.json`, `tests/DinoReader.Tests/**`, `docs/plan/slices/258-dino-starter-books-premade-audio.md`.

## Why

The four starter books (gingerbread-man, good-enough, three-little-pigs, tortoise-and-hare) are bundled in the app (`StarterStories`, `BookKind.Starter`). Slice 246's `build-audio` refuses non-store books, and part C's playback gives them no audio (Slice 246, *Known behaviours (part C)*). Their clips for all 15 languages are already recorded under `D:/ReadTheStupidText-worktrees/s246-audio/`, so only packing, delivery and playback are missing.

## Decide first (work item 1)

How the packs reach the app. Write the pick and the reason here before building:

- **Option 1: serve beside the store books** through the same Worker route (`GET /api/stories/<id>/audio/<lang>?v=<n>`), if `/api/stories` can list the starter ids; the app fetches them through the part C pack path. Keeps the app download small; needs the starter ids in the catalogue and `BookUpdater` / `StoreClient` to ask for packs of bundled books.
- **Option 2: ship in the app.** Bigger install on both platforms.

Not decided in this file yet; neither option is checked against the code.

## Work

- [ ] 1. Decide the delivery (above) and write it here.
- [ ] 2. `build-audio` accepts the four starter books; their `audio-cast.json` files exist and match the recorded clips.
- [ ] 3. Build the packs from the recorded clips for all 15 languages.
- [ ] 4. Delivery per the decision, Android and iOS (and the Windows head) the same.
- [ ] 5. A starter book reads with the pre-made clip when the text hash matches, else the on-device voice (the part C rule).
- [ ] 6. Unit tests; a device flow modeled on `premade-audio-plays`.

## Not yet verified

(none yet)
