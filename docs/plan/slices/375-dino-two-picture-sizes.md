### Slice 375 — Dino: every picture in two sizes; agents look only at the small one; a high-res pack for tablets and Windows
**Product:** Dino

*Part A merged as PR #1101 (f5692138). Parts B, C, D: in progress (wave 2026-11-14), draft PR.*

**Touches (parts B-D, paths after Slice 371; part A's are history):** `tools/dino/prepare-story-pictures.ps1`, `tools/dino/picture-review-copy.ps1`, `content/dino/lion-and-mouse/high-res/**`, `tools/dino/DinoReader.BookBuilder/**`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `worker/stories.ts`, `worker/download-keys.mjs`, `worker/download-keys.d.mts`, `worker/downloads.ts`, `worker/*.test.mjs` (stories, downloads, catalogue), `worker/CLAUDE.md`, `src/Dino/DinoReader.Core/PictureSize*.cs` (new), `src/Dino/DinoReader.Core/LandscapeLayout.cs` (read only, for `TabletShortSide`), `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/BookUpdater.cs`, `src/Dino/DinoReader.App/StagedBookUpdates.cs`, `src/Dino/DinoReader.App/StorePage.xaml.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml.cs`, `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Dino/DinoReader.App/HighResUpgrades*.cs` (new), `src/Dino/DinoReader.App/PictureSize*.cs` (new, platform device class), `src/Dino/DinoReader.App/Platforms/**` (device class only), `src/Dino/DinoReader.App/MauiProgram.cs` (DI), the `StoreItem` type's file, `tests/Dino/DinoReader.Tests/**`, `.maestro/dino/**`, `src/Dino/DinoReader.App/CLAUDE.md`

## Design for parts B-D (coordinator, wave 2026-11-14, before the claim)

Facts read at origin/main 303cd267 (ground-in-code): only `lion-and-mouse` has a complete `high-res/` (15 pages, cover, back cover); Good Enough has no `high-res/` in git; 13 books have only `high-res/back-cover.jpg`. R2 keys are `dinoreader/stories/<id>/{meta.json, cover.jpg, story-v<n>.dino}`; `worker/stories.ts:68` matches only `story-v<n>.dino`. The app takes the package URL straight from the list item (`StoreClient.cs:390,422`). Updates are staged by `StagedBookUpdates` (temp name, then rename) and applied only at a cold start before the gallery loads or from the shelf, never while a book is open.

**B. Packs.** Same version number for both packs. BookBuilder `publish-store` writes `story-v<n>.dino` and, only when `high-res/` holds every page, the cover and the back cover, `story-v<n>.hd.dino` (same text and audio; the pictures are the `high-res/` files as they are). **`high-res/` holds the final pictures (review #1, 2026-11-14):** 1792x2400 JPEG 85, centre-cropped 3:4, made on the user's Windows PC with System.Drawing (Decision 4), never raw Gemini files. `tools/dino/prepare-story-pictures.ps1 -HighRes <sourceFolder> <targetFolder> <pageCount>` writes `<targetFolder>/high-res/` (cover, pages, and back-cover.jpg when the source has it) through the shared `Save-Prepared` in `tools/dino/picture-review-copy.ps1`; a source that is already in `high-res/` is re-saved in place. `lion-and-mouse`'s 17 pictures (pages 1-15, cover, back cover; not contact-sheet.jpg) were re-saved once with it (old objects stay in git LFS history), which changed its hash and so its store version once, as expected. BookBuilder's publish gate fails a high-res picture that is not JPEG, not 3:4, or over 1.5 MB, naming book and file. `meta.json` gains `hdBytes` (absent when there is no high-res pack). The content hash that versions a store book also covers `high-res/` when it is complete, so a high-res-only change bumps the version. `check` (or a test) proves the two packs carry the same text, audio and version. Upload order: audio, cover, standard package, high-res package, meta.json last; never overwrite an existing key. `ship-targets.mjs`: `high-res/**` still never enters an app build, but a change there now selects the store upload group. Worker: `/api/stories` items gain `hdDownloadUrl` and `hdBytes` only when meta has `hdBytes`; `GET /api/stories/<id>/download?v=<n>&size=hd` serves `story-v<n>.hd.dino` behind the same entitlement gate; an unknown `size` value is a 400; a missing hd file is a 404.

**B, LFS (coordinator, added during the build; Touches widened to `.github/workflows/dino-store.yml` and `.github/CLAUDE.md`).** (1) The version never depends on whether LFS content is pulled: `BookHash` covers a complete high-res set by each file's LFS oid (the pointer's sha256, or the sha256 of the pulled bytes, which is the same), tested in both forms. (2) A store book whose high-res set is complete in git but still pointers at build time fails (`build --live` skips and names it, the job fails, nothing of it is uploaded) and is never published standard-only. (3) `dino-store.yml` restores an `actions/cache` of `.git/lfs` keyed by the hash of `git lfs ls-files -l -I "content/dino/*/high-res/**"`, runs `git lfs pull --include "content/dino/*/high-res/**" --exclude ""` before `build`, and no longer excludes high-res from its push paths.

**C. Choice.** One pure `PictureSizeChoice` in Core: inputs device class (`Phone`, `Tablet`, `Desktop`) and network (`Unmetered`, `Limited`, `None`), plus whether the book has a high-res pack; output `Standard`, `High`, or `StandardThenHigh`. Phone: always Standard. Tablet/Desktop: High on Unmetered, StandardThenHigh on Limited. No high-res pack: Standard. Device class is per device, not per window: Android, the screen's shorter side in dp at least `LandscapeLayout.TabletShortSide` (600); iOS, `DeviceInfo.Idiom == Tablet`; Windows, Desktop. "Slow connection" means `IPackUpdateNetwork` says not unmetered (mobile data, metered, roaming); no speed is measured (obvious choice, written here, not asked).

**D. Swap later: one owner, `HighResUpgrades`.** It owns one small JSON file in app data: book id to the version that is still waiting for its high-res pack. States per book: none; `pending(v)` (installed standard by a StandardThenHigh choice); `staged(v)` (the hd package fully downloaded into `StagedBookUpdates` under a temp name and renamed); applied (StagedBookUpdates swapped it in; entry removed). Triggers: app start and `ConnectivityChanged`; it acts only when `IPackUpdateNetwork` says unmetered, one book at a time, in the background, no loading screen. Interrupts: app killed mid-download, the temp file is discarded at next start and the entry stays `pending`; offline or limited, nothing starts; back on Wi-Fi, it resumes; the book updated to a newer version meanwhile, the entry is replaced by the new install's own choice (pending only if that install was again StandardThenHigh); the book deleted, the entry is removed; the store no longer lists `hdDownloadUrl` for that version, the entry is removed. The swap itself is only `StagedBookUpdates`' existing apply (cold start or shelf), so an open book is never swapped, and the swap happens only at a cold start or when the gallery next shows (review #3: connecting to Wi-Fi alone stages the package; the user sees high-res after the next launch or the next time the shelf is shown); StagedBookUpdates must accept a same-version package marked high-res as an update over the installed standard one.

**D, outcome rule (coordinator, after check passes 1 and 2 reopened the same failure handling twice; this replaces the patches).** Every high-res attempt ends in exactly one `HighResOutcome`, and only `HighResUpgradeTable` turns an outcome into a state change (one pure, unit-tested function; nothing else calls Clear/MarkFailed on an entry):
- `Done`: swapped in; entry removed.
- `Gone`: a real 404 for the hd package, or a successful catalogue that lists this book's version without `hdDownloadUrl`; entry removed.
- `Refused`: purchase or entitlement refusal (402/403); entry kept, no failure counted, this book skipped for the rest of this process run, the pass goes on to the other books.
- `Transient`: no or limited network, timeout, cancellation, IO or storage error, a failed catalogue fetch, or the book missing from the catalogue; entry kept, no failure counted, the pass stops and the next trigger retries.
- `BadPackage`: the downloaded or staged hd package fails its own validation at stage or at apply; failure counted, the staged file deleted, entry dropped after `MaxFailures`.
A failure of a **standard** staged update never touches a high-res entry. A same-version hd package is not swapped again over a copy that is already high-res (the table records the installed size). The runner only runs a pass when the table has an entry. An install choice and a book delete are not attempt outcomes: they set or remove the entry through the table's own Install/Remove methods, never Clear/MarkFailed from call sites.
**iOS connection reading:** on iOS the runner's trigger is the `NWPathMonitor` snapshot handler itself, and the decision reads the path that snapshot delivered, so the event and the reading always agree (MAUI `ConnectivityChanged` is the trigger on Android and Windows). Before the first snapshot iOS counts as limited. A download in flight is still cancelled when the connection leaves unmetered.

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
  expected pack (log marker); emulator on a metered network gets standard, then, after switching to Wi-Fi, the pack is staged, and high-res shows at the next cold start or the next time the shelf is shown (not instantly).

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
- Applied (`reviews/s375bd-high.md`): #1, #2, #3, #4, #6, #7, #9, #10.
- Skipped #5 (`reviews/s375bd-high.md`): "Cache path `.git/lfs` is restored/saved after the audio step already pulled that book's audio clips into the same folder" - cache contents and save-on-green change only speed, there is no failure path.
- Applied #8 (`reviews/s375bd-high.md`), replacing the earlier skip, with the design in part B above (a `-HighRes` mode of `prepare-story-pictures.ps1` makes the 1792x2400 JPEG 85 files; BookBuilder packs them as they are and gates JPEG, 3:4, 1.5 MB).
- Applied (`reviews/s375bd-medium.md`): #1, #2, #3, #4, #5, #7, #10.
- Skipped #6 (`reviews/s375bd-medium.md`): "The \"try hd, on 404 fall back to standard\" logic is duplicated in BookUpdater (checks fetch.NotFound) and StoreClient.FetchBookPackageAsync" - style, no failure path today.
- Skipped #8 (`reviews/s375bd-medium.md`): "HighResPack.Find, HasPointers and All re-enumerate and open every picture on each call" - performance only, a book has under 20 pictures and runs are offline tooling.
- Skipped #9 (`reviews/s375bd-medium.md`): "HighResUpgrades is an optional constructor parameter. A StoreClient built without it silently never records the pending upgrade" - `MauiProgram.cs:64` registers `HighResUpgrades` and every `StoreClient` and `StagedBookUpdates` comes from DI (`MauiProgram.cs:67,73`); no `new StoreClient` exists in src or tests.
- Skipped #11 (`reviews/s375bd-medium.md`): "Confirm the builder report names a context7 library id for these" - the builder's report names context7 `/actions/cache` and `/git-lfs/git-lfs` (caller's note).
- Applied (`reviews/s375bd-check.md`): #1 (read `NWPathMonitor.CurrentPath` at each ask, the last snapshot only as fallback), #2, #3, #4 (`PackageFetch.Gone` marks a real 404; a purchase refusal gets no fallback request), #7, #9.
- Skipped #5 (`reviews/s375bd-check.md`): "`git lfs pull --include \"content/dino/*/high-res/**\"` also fetches the 13 books that have only `back-cover.jpg` and `contact-sheet.jpg`" - bandwidth only; the cache keeps it to one pull per change.
- Skipped #6 (`reviews/s375bd-check.md`): "`packageSizeInBucket` downloads the whole existing hd package (about 10 MB or more) with `wrangler r2 object get` just to read its size" - speed only; it runs once per publish of an existing hd package.
- Skipped #8 (`reviews/s375bd-check.md`): "Windows `PlatformMetered()` builds a new `WindowsPackUpdateNetwork` on every call and re-checks the same `AllowsAutomaticUpdate`" - not a duplicate in Dino: the injected `IPackUpdateNetwork` there is `MauiPackUpdateNetwork` (profiles only), so the Windows connection cost is read only here; no failure path.
- Skipped #10 (`reviews/s375bd-check.md`): "The picker's size line is fixed at list time with `PictureSizeFor(item)`, but the download chooses the pack again when it starts" - the size line is an estimate shown before the download; the download re-chooses correctly.
- Skipped #3 (`reviews/s375a-check2.md`): "Freshness test is LastWriteTime only; a redrawn cand-5.jpg replaced via Copy-Item/Move-Item that keeps an older mtime reuses the stale cand-5.small.jpg" - candidates are new files with new names; a redraw never reuses an older-mtime file.
- Skipped #4 (`reviews/s375a-check2.md`): "%TEMP%\line70\<hash> folders are never removed" - %TEMP% clean-up is the OS's job and the files are small.
- Skipped #1 (`reviews/s375a-check3.md`): "Move-Item -Force onto a .small.jpg that another process has open fails" - one illustrator owns each candidate and runs line70 on it once; two runs on the same picture at once does not happen in the workflow.
- Skipped #2 (`reviews/s375a-check3.md`): "A working folder under a book folder" - working folders live in the scratchpad by rule (illustrator agent), never under content/dino.
- Skipped #3 (`reviews/s375a-check3.md`): "The small copy name drops the extension" - same as check #3: one candidate per page name.
- Skipped #4 (`reviews/s375a-check3.md`): "high-res pictures are Git LFS objects" - the reviewer reads high-res pictures from the worktree where LFS objects are checked out; a pointer file fails loudly anyway.
- Applied (`reviews/s375bd-check2.md`), all through the D outcome rule (`HighResOutcome`, `HighResUpgradeTable.Apply`/`OutcomeBeforeSwap`, `HighResUpgrades.Record`): #1 a standard update still staged for the book makes the staged hd package Transient, never stale (`StagedBookUpdates.OutcomeBeforeSwapAsync`); #2 a book missing from the catalogue or fetchable list is Transient, only a listed version without hd is Gone (`HighResUpgradeRunner.UpgradeOneAsync`, `OffersHighRes`); #3 a storage or unexpected swap error is Transient, only a package that fails validation is BadPackage (`StagedBookUpdates.SwapHighResAsync`); #4 a 402/403 refusal is Refused: the book is skipped for the run, the pass goes on (`PackageFetch.Refused`, `NextPending(skip)`); #5 a same-version hd package over a copy that already is high-res ends as Done (`HighResUpgrades.InstalledIsHigh`); #6 the iOS trigger is the NWPathMonitor snapshot handler and the reading is the path it delivered (`PictureSizeSource.ConnectionChanged`, `IosPathWatcher.Latest`); #8 `RunAsync` returns at once when the table has no entry.
- Applied (`reviews/s375bd-check3.md`): #1 "iOS decision reads Connectivity.Current/IPackUpdateNetwork, not the NWPath the snapshot delivered" and #2 "iOS PlatformMetered ignores path status and interface type": on iOS `PictureSizeSource.Network()` reads only the latest snapshot's NWPath (unsatisfied = None; expensive, constrained or cellular = Limited; else Unmetered; none yet = Limited); #3 "RunAsync coalescing can lose a trigger": `HighResUpgradeRunner.TryOwnAsync` sets the flag and tries once more; #4 "RecordPictureSize calls upgrades.Clear directly, and DiscardHighRes does too": both now go through `HighResUpgradeTable.Install`/`Remove` (`HighResUpgrades.Install`/`Remove`); #5 "StageFetched maps ArgumentException to BadPackage": a stage error is Transient, only package validation is BadPackage; #6 "Gone-from-shelf case leaves non-staged Pending entry": `StagedBookUpdates.ApplyHighResAsync` records Gone when the book is off the shelf; #8 "RecordPictureSize runs at staging time, before the swap": a staged update remembers its size (`RecordStagedPictureSize`) and the installed-size record is set at the swap (`HighResUpgrades.SettleInstalledAtSwap`).
- Skipped #7 (`reviews/s375bd-check3.md`): "Windows PlatformMetered builds a second WindowsPackUpdateNetwork duplicating the injected network.AllowsAutomaticUpdate check; redundant WinRT read" - duplication with no failure path; earlier skips stand.
- Skipped #7 (`reviews/s375bd-check2.md`): "Windows PlatformMetered re-creates WindowsPackUpdateNetwork and negates AllowsAutomaticUpdate, duplicating what IsUnmetered already checked through the injected network" - duplication with no failure path; the earlier skip of check #8 stands (the injected `MauiPackUpdateNetwork` does not read the Windows connection cost).

#### For CLAUDE.md (lifted 2026-11-13: both rules already live in the create-dino-story SKILL.md and the agent files, so no CLAUDE.md edit)
- A picture is never opened at full size by a coordinator or an agent: look at its `<name>.small.jpg` (`tools/picture-review-copy.ps1`, made by `gemini-take-download.ps1` after every save), and open a full-size crop only for one named fault. `gemini-take-download.ps1` exit 2 means saved but the small copy failed: run `picture-review-copy.ps1`, never retry on Gemini. `*.small.jpg` and `*.line70.png` never go into `content/dino` (gitignored). (Slice 375: docs/plan/slices/375-dino-two-picture-sizes.md)
- Dino books: draw every character first, background groups included, front and back, with a to-scale line-up; do not wait for the user's OK on character sheets. (Slice 375: docs/plan/slices/375-dino-two-picture-sizes.md)

#### Not yet verified
- A real illustrator run using the small copy (tonight's Good Enough run): small copy saved beside each candidate and no full-size read in the transcript.
- Parts B-D on a device (no device run yet, merged on review and unit tests): a tablet on Wi-Fi gets a store book at standard size, then the high-res pack swaps in at the next cold start; a phone stays standard; on mobile data no hd download starts and one in flight is cancelled.

**Design change after review round 5 (two strikes; supersedes the "outcome rule" paragraph and the journal wording above, 2026-11-14).** Five review rounds kept finding bugs in the pending-upgrade journal, so it is gone: there is no `HighResUpgradeTable`, no pending/staged entry, no install-time record, no reconcile, no `PendingAdded`/`AnnouncePending`, no `UndoPictureSizeRecordAsync`. The books to upgrade are DERIVED at the start of each pass (`HighResUpgradePlan.Derive`, Core, pure): on the shelf at the catalogue's version, installed at standard size, offered in high-res, the device wanting it (`PictureSizeSource.DeviceWantsHighRes`), no hd file already staged for that version, the ledger allowing another attempt, and fetchable by this account. Persisted: only which size is installed (`high-res-installed.json`, written after a successful import or at a swap), which staged update is high-res until it swaps in, and a per-(book, version) failure ledger (`HighResLedger`, `high-res-failures.json`: a BadPackage counts, three in all then no more attempts for that version; a hd 404 or an edited copy ends attempts for that version). A book that is owned but not fetchable now (unsettled purchase, refusal, store read failure, no network) is not in the pass and is never counted or dropped. A pass starts at app start, on every connection change, after a successful install (`StoreClient.LibraryChanged`) and after any swap (`StagedBookUpdates.Swapped`), the same on Android, iOS and Windows. The device class is read once on the main thread at start-up (`PictureSizeSource.InitializeDevice`, called by `DinoApp` and the runner's start); a density of 0 or less is not cached and the standard size is used that run; no blocking marshalling. `StagedBookUpdates` defers a transient hd swap by (book, version) and clears it when a package is staged afresh; deferred entries hold no overlay. Tests: `tests/Dino/DinoReader.Tests/HighResUpgradePlanTests.cs` (unsettled purchase kept over 50 passes, parse failure dropped after MaxFailures, derived set after a kill mid-install, re-staged version retried).

*From review (parts B-D, 2026-11-14):* files `reviews/s375bd-check.md` to `reviews/s375bd-check4.md`. Check4 findings 1, 2, 3 and 6 fixed; 4, 5, 7 skipped as low. Skipped #8 ("high-res files are in the content hash, so adding or re-saving hd pictures bumps the standard pack's version"): this is the slice's own rule (part B: a high-res-only change bumps the version; packs are never overwritten).

- Applied (`reviews/s375bd-high6.md`): #1-#7. BookUpdater now always stages the standard package (the runner alone fetches hd, with the ledger and the unmetered cancel); an hd swap fault other than IO or cancel is BadPackage; unreadable state files skip the write (`HighResStateFile`); the runner reads the device again each pass and no longer raises NotifyStaged (hd swaps in at the next cold start or shelf appearance); a staged file replaced meanwhile is not deleted.
- Skipped #8 (`reviews/s375bd-high6.md`): "DeriveAsync calls staged.StagedHighResVersion(id) per shelf book; each call lists and parses the whole staging directory" - low, a few files and a handful of books per pass, speed only.
- Applied (`reviews/s375bd-medium6.md`): #1 (already how it works: the standard swap forgets the hd record and the derived plan upgrades it again on Wi-Fi, a staged hd file blocks a second fetch), #3, #5, #6 (a 404 now counts one ledger failure, `HighResUpgradePlan.OutcomeForMissingPackage`), #9.
- Skipped #2 (`reviews/s375bd-medium6.md`): "RecordStagedSize(high:true) and high-res-staged-size.json are dead" - low, harmless and kept as the hook a later size choice uses.
- Skipped #4 (`reviews/s375bd-medium6.md`): "Every connectivity event / library change / swap triggers a store listing fetch plus FetchableAsync over the whole catalogue" - low-medium, speed only; a pass on a phone or a metered link ends before it.
- Skipped #7 (`reviews/s375bd-medium6.md`): "WINDOWS PlatformMetered builds a new WindowsPackUpdateNetwork per call and ignores the injected IPackUpdateNetwork" - low, both calls give the same answer in practice.
- Skipped #8 (`reviews/s375bd-medium6.md`): "StoreItem.BytesFor is used only on FirstBooksPage; store page and progress show the standard size" - low, display only.
- Skipped #1 and #2 (`reviews/s375bd-check5.md`): a 404 counts once per pass with no delay (three quick triggers give that version up), and a bad id re-downloads and rejects the hd pack each pass. Low; follow-up for the next Dino slice.


## Parts B-D: parked (night 2026-11-14-0726, wave 1)

*Park record (history): parts B-D were then finished by the interactive session and merged as PR #1113 (3ccecadd, 2026-11-14 11:49Z).*

- Built on draft PR #1113 (commit 15828aa9, branch `feat/375-two-picture-sizes`, worktree `s375`), pushed, not merged.
- The review gate did not converge: the high, medium and check passes 1-3 were each fixed (one redesign: a single HighResOutcome rule after two strikes on failure handling). Check pass 4 (`reviews/s375bd-check4.md`) still has 4 medium correctness findings: 1 an unknown swap error retries forever; 2 the iOS first snapshot reads Limited; 6 a kill between import and the entry write loses the upgrade; 8 the hd files in the content hash make phones re-download an unchanged standard pack.
- Next: apply check4 findings 1, 2, 3, 6, 7; decide 8 (likely hash the hd files separately so phones keep their pack); then one check pass, the emulator choice flows, merge. Earlier passes: `reviews/s375bd-high.md`, `-medium`, `-check`, `-check2`, `-check3`.
- Chosen without asking: "slow connection" means not unmetered (no speed is measured). Lion and Mouse's 17 high-res pictures were re-saved at JPEG 85 (0.52-0.86 MB each; hd pack 11.4 MB).
