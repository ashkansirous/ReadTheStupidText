### Slice 375 — Dino: every picture in two sizes; agents look only at the small one; a high-res pack for tablets and Windows
**Product:** Dino

*Planned.*

**Touches:** `tools/prepare-story-pictures.ps1`, `tools/picture-review-copy.ps1` (new), `.claude/skills/create-dino-story/**`, `.claude/agents/dino-story-illustrator.md`, `.claude/agents/dino-story-reviewer.md`, `tools/DinoReader.BookBuilder/**`, `tools/publish-store-books.mjs`, `tools/publish-store-books.test.mjs`, `.github/scripts/ship-targets.mjs`, `worker/**` (story catalogue and downloads), `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/PictureSize*.cs` (new), `tests/DinoReader.Tests/**`, `.maestro/dino/**`

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

Planner's choice, flagged for the user: starter books bundled in the app stay standard inside the app package (keeps the
download from the stores small); tablets and Windows fetch their high-res pictures on Wi-Fi like decision 5.

## Work

### A. Review copy, tooling only (smallest end-to-end; no app change)
- [ ] `tools/picture-review-copy.ps1 <original>`: writes `<name>.small.jpg` (896x1200, centre-cropped to 3:4, never
      upscaled, JPEG 85) beside the original, reusing `prepare-story-pictures.ps1`'s resize code (move it into one shared
      function, not a copy).
- [ ] `gemini-take-download.ps1` makes the small copy right after saving a candidate; `line70.ps1` writes its overlay
      from the small copy; any move or rename of a candidate moves both.
- [ ] Rewrite the "full size" lines above: the illustrator and reviewer judge from the small copy, open a full-size
      crop only for one named fault, and never read the whole original. The coordinator's rule likewise.
- [ ] Measure before/after: tokens per picture check on one Good Enough page (full-size read about 4k; small copy target
      about 1.4k), written into this file.

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
- [ ] Starter books on tablets and Windows fetch their high-res pictures the same way.

## Verification
- A: one illustrator run on a test picture shows the small copy saved beside the original and no full-size read in its
  transcript; the token numbers above recorded.
- B: `BookBuilder check` passes; the store lists both packs for Good Enough and one for a book without originals;
  Worker tests pass.
- C/D: Android emulator as phone and as tablet, the iOS simulator as iPhone and iPad, and the Windows VM each get the
  expected pack (log marker); emulator on a metered network gets standard, then high-res after switching to Wi-Fi.
