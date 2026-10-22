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

## Design (Batch 16 mock, screens 21-23, added 2026-10-22)

Source: `docs/design/dino-reader/Dino Reader Batch 16.dc.html`, screens 21 (line 152), 22 (line 184), 23 (line 199), group "Grown-up · Free stories · Slice 92" at line 147. 3 frames. The mock agrees with the plan, with the open points below. Titles and covers in the mock are placeholders.

- **Entry (screen 19, line 104).** In the grown-up gallery a "Free stories" button sits left of "Backup" in a row under the header: `flex:1`, 48 px high, 16 px radius, white fill, 2 px inset `#c9c0f5` border, `#5B57E8` text 15 px/600, 20 px open-book icon (stroke 2.2).
- **Store header (screen 21).** 44 px back chevron, then "Free stories" (20 px/600) over "Picked and cast for Dino" (12 px, `#8a8399`).
- **Entry row.** Cover, then title (15.5 px/600), then language in its own name ("English", "فارسی") and "12 pages · 6 min" (12.5 px, `#8a8399`), then the action on the right, 44 px high:
  - "Get": `#5B57E8` pill, 22 px radius, white 14 px/600 text, 16 px download icon.
  - Downloading: 88 px wide `#ECE7FF` pill, `#c9c0f5` fill growing left to right, percentage ("42%") in `#3f3bc4`.
  - "Downloaded": no fill, `#2E7D32` text 13.5 px/600 with a 16 px check.
- **Footer line.** "Free, with no account. Nothing about you or your child is sent."
- **Rules shown (notes).** Stories in every language are listed. A story packed in a newer format is hidden, not greyed out. A dropped download puts "Get" back; nothing half-saved reaches the shelf. A finished download is on the shelf with its cast.
- **Offline (screen 22).** "You're offline" (21 px/600), "The free stories need the internet. Stories already on the shelf still read without it.", button "Try again" (48 px, 24 px radius, `#5B57E8`, 15 px/600). Also used when the server can't be reached.
- **Empty (screen 23).** "No new stories yet", "New ones are added from time to time. Check back soon.", button "Back to my stories". Shown when the list is empty or every entry is hidden for being too new. A normal state, not an error.
- New strings (both locales): "Free stories", "Picked and cast for Dino", "Get", "Downloaded", "{n} pages · {m} min", the footer line, "You're offline", the offline body, "Try again", "No new stories yet", the empty body, "Back to my stories".

**Settled (2026-10-22, from the mock): story length in the list.** The mock shows "12 pages · 6 min", so `pageCount` and `minutes` to `meta.json` (written when the developer uploads; minutes from word count).

**Settled (2026-10-22, from the mock): store lists a story already on the shelf.** Screen 21 shows "The Three Little Pigs" as "Downloaded". Import still gives every story a new id (Decision 94), so the imported `story.json` records the package's own id as `sourceId` (Slice 91's importer writes it; the starters carry the same ids as their store copies). The store shows "Downloaded" when any installed story has that `sourceId`, and Download is not offered for it. A deleted story can be downloaded again.

## Work

- [x] 0. **Design gate.** Done 2026-10-22: the mock (`Dino Reader Batch 16.dc.html`, screens 21-23) is
  in `docs/design/dino-reader/`, see Design above. Item 1 doesn't wait for it; item 2 does.
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
