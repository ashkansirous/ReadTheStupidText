# Slice 258 — Dino: pre-made audio for the four starter books

*Built 2026-11-04 (wave 2026-11-04), PR #792, not merged. Upload of the pack zips is a post-merge step; the device flow `premade-audio-starter` is not yet verified.* Follow-up to Slice 246 (parts A-D merged as #729, #768, #787, #790). Dino Reader only, Android and iOS alike (and the Windows head, which shares the code).

**Touches:** `tools/DinoReader.BookBuilder/**`, `src/DinoReader.App/StarterStories.cs`, `src/DinoReader.App/AudioPack*.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/BookUpdater.cs`, `content/dino/{gingerbread-man,good-enough,three-little-pigs,tortoise-and-hare}/audio-cast.json`, `tests/DinoReader.Tests/**`, `tools/upload-store-story.ps1`, `.github/scripts/publish-store-books.mjs`, `.maestro/dino/premade-audio-*.yaml`, `src/DinoReader.App/Testing/DinoTestCommands.cs`, `src/DinoReader.App/Reading*.cs`, `src/DinoReader.App/StoryVoicing.cs` (comments only), `STORE.md`, `worker/CLAUDE.md` (doc lines only), `docs/plan/slices/258-dino-starter-books-premade-audio.md`. Widened at claim (2026-11-04) after a code check; the Worker code is not touched.

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
- [x] 2. `build-audio` accepts the four starter books; their `audio-cast.json` files exist and match the recorded clips.
- [x] 3. Build the packs from the recorded clips for all 15 languages: 60 packs v1 (4 starters x 15 languages), in `D:/ReadTheStupidText-worktrees/s246-audio/packs/<book>/<id>/`.
- [ ] 4. Delivery per the decision, Android and iOS (and the Windows head) the same. The `audioPacks` records are in the four `audio-cast.json` files; the zips are uploaded by `tools/upload-store-story.ps1` right after the merge (post-merge step, not yet done).
- [x] 5. A starter book reads with the pre-made clip when the text hash matches, else the on-device voice (the part C rule). No starter exclusion existed in the app playback path.
- [x] 6. Unit tests; a device flow `.maestro/dino/premade-audio-starter.yaml` modeled on `premade-audio-plays`.

Changed while building:
- `StoreBuilder.MetaOf` fix: a starter spec has no stem, so its meta lost the audio field.
- `build-audio` accepts starters (the guard in `BookCommands.BuildAudio` removed).
- Stale manifests: three-little-pigs `fa` and tortoise-and-hare `fa` had stale indexes (Slice 255 removed sound pieces); rebuilt from the generator cache with 0 renders, kept in `s246-audio/regen-s258/`.
- New Debug-only hook `fetch-audio-pack` in `DinoTestCommands.cs`; `open-book` waits up to 120 s for in-flight fetches.
- Touches widened to `src/DinoReader.App/StoryVoicing.cs` (comments only).

Known behaviour: an edited starter falls back to the device voice for the changed pieces (clips match per piece by text hash).

From review (files `D:/ReadTheStupidText-worktrees/reviews/s258-high.md`, `s258-medium.md`, `s258-check.md`, all findings read):
- High 1, 2, 5, 6: applied.
- High 3 (test checks record shape only, not that zips are uploaded): skipped; upload is a post-merge step.
- High 4 (packs not tied to the starter's text version, `check` does not flag it): skipped; this is the Slice 246 design for store books too, playback falls back per piece. See Follow-up.
- Medium 1, 2, 3: applied.
- Medium 4 (records only, not that uploaded zips match hashes): skipped; upload is a post-merge step.
- Medium 5 (pack records go stale when text changes): skipped; same as high 4, see Follow-up.
- Check pass, 5 findings, none applied, none blocking: 1 (test forces a pack per UI language), 2 (records not checked against upload; repeats high 3), 3 (120 s wait in the test-only `open-book`), 4 (case-insensitive id vs case-sensitive Worker folder; not hit by the four starters), 5 (stale pack after a text bump; repeats high 4).

**Parked 2026-11-04 (night 2026-11-03-2349, wave 4):** PR #792 passed both reviews, a check pass and every CI check (green, mergeable), but `gh pr merge 792 --merge` was denied by the auto-mode classifier ("CI Bypass") in the headless session, so it is not merged and the 60 pack zips are not uploaded. Next: merge #792 (the merge needs an allow for `gh pr merge` in the night sessions), then run `pwsh tools/upload-store-story.ps1 <id>` for each starter after `BookBuilder build` copies the packs into `artifacts/store/<id>/`, then run `premade-audio-starter` on the emulator.

Follow-up: make BookBuilder `check` flag a pack whose clip hashes no longer match the text.

## Not yet verified

- `premade-audio-starter` PASS after the pack upload. Emulator run (cache 9f62c6983293): `premade-audio-plays` PASS, `usage-playback-events` PASS, `premade-audio-starter` FAIL as expected before upload (`HOOK fetch-audio-pack failed no listed pack for 966400017c494fb5b045786f53852c9e in language 'fa'`).
- iOS run (compile only so far).
- Windows head run.

#### For CLAUDE.md

(none: no new rule that an agent would get wrong without being told)
