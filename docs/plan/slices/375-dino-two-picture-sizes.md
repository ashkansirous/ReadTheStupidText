### Slice 375 — Dino: every picture in two sizes; agents look only at the small one; a high-res pack for tablets and Windows
**Product:** Dino

*Part A merged as PR #1101 (f5692138). Parts B, C, D: in progress (wave 2026-11-14), draft PR.*

**Touches (parts B-D, paths after Slice 371; part A's are history):** `tools/dino/DinoReader.BookBuilder/**`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `worker/stories.ts`, `worker/download-keys.mjs`, `worker/download-keys.d.mts`, `worker/downloads.ts`, `worker/*.test.mjs` (stories, downloads, catalogue), `worker/CLAUDE.md`, `src/Dino/DinoReader.Core/PictureSize*.cs` (new), `src/Dino/DinoReader.Core/LandscapeLayout.cs` (read only, for `TabletShortSide`), `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/BookUpdater.cs`, `src/Dino/DinoReader.App/StagedBookUpdates.cs`, `src/Dino/DinoReader.App/StorePage.xaml.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml.cs`, `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Dino/DinoReader.App/HighResUpgrades*.cs` (new), `src/Dino/DinoReader.App/PictureSize*.cs` (new, platform device class), `src/Dino/DinoReader.App/Platforms/**` (device class only), `src/Dino/DinoReader.App/MauiProgram.cs` (DI), the `StoreItem` type's file, `tests/Dino/DinoReader.Tests/**`, `.maestro/dino/**`, `src/Dino/DinoReader.App/CLAUDE.md`

## Design for parts B-D (coordinator, wave 2026-11-14, before the claim)

Facts read at origin/main 303cd267 (ground-in-code): only `lion-and-mouse` has a complete `high-res/` (15 pages, cover, back cover); Good Enough has no `high-res/` in git; 13 books have only `high-res/back-cover.jpg`. R2 keys are `dinoreader/stories/<id>/{meta.json, cover.jpg, story-v<n>.dino}`; `worker/stories.ts:68` matches only `story-v<n>.dino`. The app takes the package URL straight from the list item (`StoreClient.cs:390,422`). Updates are staged by `StagedBookUpdates` (temp name, then rename) and applied only at a cold start before the gallery loads or from the shelf, never while a book is open.

**B. Packs.** Same version number for both packs. BookBuilder `publish-store` writes `story-v<n>.dino` and, only when `high-res/` holds every page, the cover and the back cover, `story-v<n>.hd.dino` (pictures re-saved at JPEG 85 from the originals, same text and audio). `meta.json` gains `hdBytes` (absent when there is no high-res pack). The content hash that versions a store book also covers `high-res/` when it is complete, so a high-res-only change bumps the version. `check` (or a test) proves the two packs carry the same text, audio and version. Upload order: audio, cover, standard package, high-res package, meta.json last; never overwrite an existing key. `ship-targets.mjs`: `high-res/**` still never enters an app build, but a change there now selects the store upload group. Worker: `/api/stories` items gain `hdDownloadUrl` and `hdBytes` only when meta has `hdBytes`; `GET /api/stories/<id>/download?v=<n>&size=hd` serves `story-v<n>.hd.dino` behind the same entitlement gate; an unknown `size` value is a 400; a missing hd file is a 404.

**C. Choice.** One pure `PictureSizeChoice` in Core: inputs device class (`Phone`, `Tablet`, `Desktop`) and network (`Unmetered`, `Limited`, `None`), plus whether the book has a high-res pack; output `Standard`, `High`, or `StandardThenHigh`. Phone: always Standard. Tablet/Desktop: High on Unmetered, StandardThenHigh on Limited. No high-res pack: Standard. Device class is per device, not per window: Android, the screen's shorter side in dp at least `LandscapeLayout.TabletShortSide` (600); iOS, `DeviceInfo.Idiom == Tablet`; Windows, Desktop. "Slow connection" means `IPackUpdateNetwork` says not unmetered (mobile data, metered, roaming); no speed is measured (obvious choice, written here, not asked).

**D. Swap later: one owner, `HighResUpgrades`.** It owns one small JSON file in app data: book id to the version that is still waiting for its high-res pack. States per book: none; `pending(v)` (installed standard by a StandardThenHigh choice); `staged(v)` (the hd package fully downloaded into `StagedBookUpdates` under a temp name and renamed); applied (StagedBookUpdates swapped it in; entry removed). Triggers: app start and `ConnectivityChanged`; it acts only when `IPackUpdateNetwork` says unmetered, one book at a time, in the background, no loading screen. Interrupts: app killed mid-download, the temp file is discarded at next start and the entry stays `pending`; offline or limited, nothing starts; back on Wi-Fi, it resumes; the book updated to a newer version meanwhile, the entry is replaced by the new install's own choice (pending only if that install was again StandardThenHigh); the book deleted, the entry is removed; the store no longer lists `hdDownloadUrl` for that version, the entry is removed. The swap itself is only `StagedBookUpdates`' existing apply (cold start or shelf), so an open book is never swapped; StagedBookUpdates must accept a same-version package marked high-res as an update over the installed standard one.

## Why

On 2026-11-13 a single session's file reads added up to about 2M tokens, twice its working memory. Much of that was
1792x2400 pictures opened at full size, by the coordinator and by the illustrator and reviewer agents (the rules say
"full size" at create-dino-story `SKILL.md` lines 3, 14, 57 and 203-207, `dino-story-illustrator.md` lines 9, 39, 59,
94 and 102, and `dino-story-reviewer.md` line 43). The user, in their own words: "when the illustrator generates an
image, it should not read it. the images are big and often too big for small screen ... reading them ... use a lot of
token which is useless."

Today two sizes exist but only one ships: `tools/prepare-story-pictures.ps1` makes the 896x1200 app copy (JPEG 85) and
the 288x384 cover thumb with System.Drawing; the 1792x2400 originals sit in `high-res/` (Git LFS) and are kept out of
every build and upload (BookBuilder `Books.cs:83` and `BookHash`, `publish-store-books.mjs`, `ship-targets.mjs:220`).

## Decisions (the user, 2026-11-13)

1. **Agents and Claude never open a picture at full size.** Every picture saved from Gemini gets its 896x1200 copy made
   at once, next to it, and the two always move together. Illustrator, reviewer and coordinator look only at the small
   copy (or a contact sheet); a full-size crop is opened only for one named fault.
2. **Two packs per book.** High-res (1792x2400, JPEG 85: Good Enough's 13 pictures measure 8.8 MB, about 10 MB with one
   language's audio) for tablets (Android, iPad) and Windows; standard (896x1200, about 3 MB a book) for phones. The app
   picks by screen size; nobody chooses.
3. **Books with no high-res originals ship the standard pack only**, until Slice 350 redraws them.
4. **The small copy is made on the user's PC with Windows' built-in imaging** (System.Drawing, as
   `prepare-story-pictures.ps1`, `gemini-take-download.ps1`, `line70.ps1` and `contact-sheet.ps1` already do). No new library.
5. **Slow connection** (the user asked; my recommendation, not objected to): a tablet or Windows PC on mobile data, a
   metered or a slow connection downloads the standard pack, then swaps in the high-res pictures by itself on Wi-Fi.

No book ships inside the app (Slice 261): every book, on every device, comes from the store, so these two packs cover them all.
The Debug-only test books in `Resources/Raw/testbooks/` stay standard.

## Work

### A. Review copy, tooling only (smallest end-to-end; no app change)
- [x] `tools/picture-review-copy.ps1 <original>` (also `-CopyDir <folder>` for a reviewer, who changes nothing in the repo): writes `<name>.small.jpg` (896x1200, centre-cropped to 3:4, never
      upscaled, JPEG 85) beside the original, reusing `prepare-story-pictures.ps1`'s resize code (move it into one shared
      function, not a copy).
- [x] `gemini-take-download.ps1` makes the small copy right after saving a candidate; `line70.ps1` writes its overlay
      from the small copy (no git call: a path with a `\content\dino\` segment goes to `%TEMP%\line70\<hash>\`, any other
      path beside the picture, `-OutDir` overrides both; the small copy is written to a temp name and renamed into place); any move or rename of a candidate moves both.
- [x] Rewrite the "full size" lines above: the illustrator and reviewer judge from the small copy, open a full-size
      crop only for one named fault, and never read the whole original. The coordinator's rule likewise.
- [x] Measure before/after: tokens per picture check on one Good Enough page (full-size read about 4k; small copy target
      about 1.4k), written into this file.
      **Measured (Good Enough page 6, `picture-review-copy.ps1`, 2026-11-13):** original 1792x2400, 2,851,391 bytes,
      estimate 1792*2400/750 = 5,734 tokens; small copy 896x1200, 221,509 bytes (7.8% of the bytes), estimate
      896*1200/750 = 1,434 tokens. So the small copy costs a quarter of the tokens per look (the formula gives 5.7k, not the
      4k guessed above, because it does not apply the viewer's own downscale; the ratio is 4:1 either way).

### B. Two packs built and uploaded
- [ ] BookBuilder builds `<stem>.dino` (standard) and, when `high-res/` holds every page, cover and back cover,
      `<stem>.hd.dino` (pictures re-saved at JPEG 85 from the originals). `check` requires the two to carry the same
      text, audio and version.
- [ ] `publish-store-books.mjs` uploads both; the story catalogue lists the high-res pack and its size when it exists.
      `ship-targets.mjs` keeps `high-res/**` out of the app build but ships it to the store upload.
- [ ] Worker: the download endpoint serves either pack (same entitlement check), with tests.

### C. The app picks the size (Android, iOS and Windows in the same part)
- [ ] One pure `PictureSizeChoice` (screen's shorter side, and connection kind) in Core, unit-tested.
- [ ] Android: tablets (shorter side at least 600 dp) choose high-res. iOS: iPad chooses high-res. Windows: high-res.
- [ ] Phones always get standard.

### D. Slow connection, swap later
- [ ] On mobile data, a metered or a slow connection, a high-res device downloads standard first, marks the book
      "high-res pending", and swaps in the high-res pictures on Wi-Fi in the background (no loading screen; the open book
      is never swapped mid-read). Android and iOS, plus Windows' metered flag.

## Verification
- A: one illustrator run on a test picture shows the small copy saved beside the original and no full-size read in its
  transcript; the token numbers above recorded.
- B: `BookBuilder check` passes; the store lists both packs for Good Enough and one for a book without originals;
  Worker tests pass.
- C/D: Android emulator as phone and as tablet, the iOS simulator as iPhone and iPad, and the Windows VM each get the
  expected pack (log marker); emulator on a metered network gets standard, then high-res after switching to Wi-Fi.

## Part A: what was built (2026-11-13)

*Changed while building:*
- `tools/picture-review-copy.ps1` holds the shared resize function; `tools/prepare-story-pictures.ps1` uses it too (one copy of the code). Small copy: 896x1200, JPEG 85, written to a temp name and renamed into place.
- `gemini-take-download.ps1` makes the small copy after every save. Exit code 2 means "saved, but the small copy failed": never retry on Gemini; run `picture-review-copy.ps1` on the saved file.
- `line70.ps1` draws on the small copy, named `<name>.small.line70.png`. No git call: a path with a `\content\dino\` segment puts the copy and overlay in `%TEMP%\line70\<hash>\`; any other path (scratch candidates) puts them beside the picture; `-OutDir` overrides both. A `.small.jpg` given as input is used as is.
- `.gitignore` keeps `*.small.jpg` and `*.line70.png` out of `content/dino`.
- Every "full size" rule in `create-dino-story` `SKILL.md`, `dino-story-illustrator.md` and `dino-story-reviewer.md` now says: look at the small copy; open a full-size crop only for one named fault; never the whole original.
- Added at the user's request, 2026-11-13 (Good Enough run): every character, background groups included, is drawn first, front and back, with a to-scale line-up, and nobody waits for the user's OK on character sheets.

*From review:* files `reviews/s375a-high.md`, `reviews/s375a-medium.md`, `reviews/s375a-check.md`, `reviews/s375a-check2.md`, `reviews/s375a-check3.md`.
- Applied: high #1, #4, #5, #6, #7 (the `.small.jpg` input part); medium #1, #4; check #1, #2; check2 #1, #2, #5.
- Skipped #2 (`reviews/s375a-high.md`): "Picture and .small.jpg move/rename/delete together is prose only" - no script moves originals either; the move is the agent's own step and the doc says both move.
- Skipped #3 (`reviews/s375a-high.md`): "Reviewer judges high-res originals only from the 896x1200 downscale" - the user's decision 1: agents judge from the small copy, a full-size crop only for a named fault.
- Skipped #7, parameter-scope part (`reviews/s375a-high.md`): "its param names leak into callers' scope" - low; unique names were already chosen to avoid the clash.
- Skipped #2 (`reviews/s375a-medium.md`): "mtime freshness check misses a renamed redraw" - a rename moves both files per the doc, and line70 rebuilds the copy when it is missing; rare.
- Skipped #3 (`reviews/s375a-medium.md`): "Small copy named from base name only" - the take script uses one candidate name per page; png and jpg of the same page do not coexist.
- Skipped #5 (`reviews/s375a-medium.md`): "Review copy centre-crops to 3:4" - Gemini's Dino originals are 3:4 by rule (1792x2400); the slice's high-res pack uses the same crop.
- Skipped #6 (`reviews/s375a-medium.md`): "Unrelated draw every background character first" - part A's scope by the user's own request (2026-11-13); recorded above.
- Skipped #3 (`reviews/s375a-check.md`): "Small copy keyed by base name only" - one candidate per page name, so 5.png and 5.jpg never coexist.
- Skipped #4 (`reviews/s375a-check.md`): "`exit 2` reaches the caller only when run with pwsh" - the "SAVED <path>" line is the documented signal and callers run the script with -File, so exit 2 reaches them.
- Skipped #5 (`reviews/s375a-check.md`): "`-CopyDir X` without -Original does nothing" - low, behaviour correct today (no -Original means nothing to copy).
- Skipped #6 (`reviews/s375a-check.md`): "Dot-sourcing a script with a param block rebinds" - low, behaviour correct today (callers use different variable names).
- Skipped #3 (`reviews/s375a-check2.md`): "Freshness test is LastWriteTime only; a redrawn cand-5.jpg replaced via Copy-Item/Move-Item that keeps an older mtime reuses the stale cand-5.small.jpg" - candidates are new files with new names; a redraw never reuses an older-mtime file.
- Skipped #4 (`reviews/s375a-check2.md`): "%TEMP%\line70\<hash> folders are never removed" - %TEMP% clean-up is the OS's job and the files are small.
- Skipped #1 (`reviews/s375a-check3.md`): "Move-Item -Force onto a .small.jpg that another process has open fails" - one illustrator owns each candidate and runs line70 on it once; two runs on the same picture at once does not happen in the workflow.
- Skipped #2 (`reviews/s375a-check3.md`): "A working folder under a book folder" - working folders live in the scratchpad by rule (illustrator agent), never under content/dino.
- Skipped #3 (`reviews/s375a-check3.md`): "The small copy name drops the extension" - same as check #3: one candidate per page name.
- Skipped #4 (`reviews/s375a-check3.md`): "high-res pictures are Git LFS objects" - the reviewer reads high-res pictures from the worktree where LFS objects are checked out; a pointer file fails loudly anyway.

#### For CLAUDE.md (lifted 2026-11-13: both rules already live in the create-dino-story SKILL.md and the agent files, so no CLAUDE.md edit)
- A picture is never opened at full size by a coordinator or an agent: look at its `<name>.small.jpg` (`tools/picture-review-copy.ps1`, made by `gemini-take-download.ps1` after every save), and open a full-size crop only for one named fault. `gemini-take-download.ps1` exit 2 means saved but the small copy failed: run `picture-review-copy.ps1`, never retry on Gemini. `*.small.jpg` and `*.line70.png` never go into `content/dino` (gitignored). (Slice 375: docs/plan/slices/375-dino-two-picture-sizes.md)
- Dino books: draw every character first, background groups included, front and back, with a to-scale line-up; do not wait for the user's OK on character sheets. (Slice 375: docs/plan/slices/375-dino-two-picture-sizes.md)

#### Not yet verified
- A real illustrator run using the small copy (tonight's Good Enough run): small copy saved beside each candidate and no full-size read in the transcript.
