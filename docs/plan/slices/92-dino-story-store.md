# Slice 92 — A free, curated Dino story store behind the grown-up gate

*Planned 2026-10-22.* Source: phone scope "Dino Reader: starter stories, story store, better
onboarding, backup" (Scope Inbox, 2026-10-22).

**Batch:** 16 (Dino Reader content)
**Depends on:** Slice 91 (the `.dino` package and import); the store screen in Claude Design (Decision 99)
**Touches:** `worker/index.ts`, `worker/stories.ts` (new), `worker/downloads.ts`, `worker/download-keys.mjs`, `worker/download-keys.d.mts`, `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `worker/check-configs.mjs`, `worker/*.test.mjs`, `src/DinoReader.App/StorePage.xaml` (new), `src/DinoReader.App/StorePage.xaml.cs` (new), `src/DinoReader.App/StoreClient.cs` (new), `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Platforms/Android/AndroidManifest.xml` (comment only), `src/DinoReader.App/Platforms/iOS/Info.plist` (comment only), `site/dino/privacy.html`, `PRIVACY.md`, `docs/design/dino-reader/**`, `src/DinoReader.App/CLAUDE.md`, `CLAUDE.md` (landing-site paragraph)
**Story issue:** —

## Why

Starter stories run out. A small store of well-cast stories, uploaded by the developer, keeps new
ones coming without the parent scanning a book. No accounts and no tracking keep it fit for a
kids' app (Decision 96).

## Decisions locked with the user (2026-10-22)

- R2 layout `dinoreader/stories/<id>/{meta.json, cover.jpg, story-v<n>.dino}` in the existing
  `readthestupidtext-downloads` bucket, uploaded by hand. `meta.json` goes up last, and a folder
  counts only when its `.dino` and `meta.json` are both there (the APK-manifest convention).
- Worker: `GET /api/stories` (the list, using the existing 60 s listing cache),
  `/api/stories/<id>/cover` and `/api/stories/<id>/download` (versioned, edge-cached, Range).
  Each path gets its `/dino` twin in `run_worker_first`, which `check-configs` enforces.
- `meta.json` carries `schemaVersion`; the app hides a story whose package is newer than it reads.
- The store sits behind the grown-up gate. Each entry shows cover, title, language and length,
  with a Download button or "Downloaded". It has offline and empty states.
- Privacy: the Dino privacy page, `PRIVACY.md` and the manifest/Info.plist comments now say the
  network also fetches the store list and the stories a grown-up picks. Nothing is collected, so
  Play Data safety stays as it is, but gets a re-check.
- Reverses the Batch 10 out-of-scope "A story store or marketplace". "Syncing or sharing stories
  between devices" stays out.

## Work

- [ ] 0. **Design gate.** The user adds the store screen in Claude Design; it is imported into
  `docs/design/dino-reader/` with `import-claude-design`. Item 1 doesn't wait for it; item 2 does.
- [ ] 1. **Worker API, end to end with one real story.** `worker/stories.ts`, the routes and
  twins, key helpers, and tests for "complete folders only" and the versioned download. Upload one
  starter by hand and fetch it from `dinoreader.sirous.uk/api/stories`.
- [ ] 2. **Store screen.** `StoreClient` (list, cover, download into Slice 91's import) and
  `StorePage` per the mock, entered from the grown-up gallery.
- [ ] 3. **Privacy and policy text**, plus the rules into `src/DinoReader.App/CLAUDE.md` and the
  root `CLAUDE.md` landing-site paragraph.

## Verification

- `npm run typecheck` and the Worker tests. `curl` the three endpoints on workers.dev and on the
  custom domain; a half-uploaded folder is not listed.
- Emulator: a grown-up opens the store and downloads a story, which appears in the gallery with
  its cast. Airplane mode shows the offline state.

## Not yet verified

_(filled in when built)_
