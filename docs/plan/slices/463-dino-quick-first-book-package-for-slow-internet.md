# Slice 463 — Dino: a small "quick" first-book package for slow internet, replaced by the full one in the background
**Product:** Dino

*Planned 2026-11-27 from the owner's answer on the 2026-11-26 usage analysis.*

**Starts after:** [Slice 459](459-dino-first-book-playable-in-seconds.md) (it edits the same first-books download path).

**Touches:** `tools/dino/prepare-story-pictures.ps1`, `tools/dino/premade-audio/encode.py`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `worker/stories.ts`, `worker/**` tests, `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/HighResUpgradeRunner.cs`, `src/Dino/DinoReader.Core/PictureSizeChoice.cs`, `tests/Dino/DinoReader.Tests/PictureSizeChoiceTests.cs`, `tests/Dino/DinoReader.Tests/**` (first books), `src/Dino/DinoReader.App/FirstBooksPage.cs`

**Why:** the owner's words (2026-11-27): "we probably need an even lower quality package for people with slow internet. when we start downloading the first book, if it is too slow, we drop it and switch to the even smaller package. then we download a proper package in the background. when it is done, it would replace the low quality package."

**Measured 2026-11-27** (live list `https://dinoreader.sirous.uk/api/stories` and a real download of The Lion and the Mouse; MB = 10^6 bytes):

| Variant | Pictures | Audio | Lion and the Mouse (book + en) | 1 Mbit/s | 5 Mbit/s |
| --- | --- | --- | --- | --- | --- |
| High-res (tablets on unmetered links; `PictureSizeChoice.cs:42-49`) | 1792x2400 JPEG q85 | AAC 48 kb/s | 11.43 + 1.66 = 13.1 | 105 s | 21 s |
| Standard (today's default) | 896x1200 JPEG q85 | AAC-LC mono 24 kHz 48 kb/s | 2.48 + 1.66 = 4.14 | 33 s | 6.6 s |
| Quick, AAC (measured in scratch) | 448x600 JPEG (ffmpeg q 8) | AAC 24 kb/s mono | 0.33 + 0.85 = 1.18 | 9.4 s | 1.9 s |
| Quick, opus (measured in scratch) | 448x600 JPEG | opus 16 kb/s mono | 0.33 + 0.54 = 0.87 | 7 s | 1.4 s |

- Across all 16 books, the standard package is 2.5-4.0 MB, median 3.2 MB. The en pack is 0.8-2.7 MB, median 1.9 MB. High-res is 9.0-16.6 MB.
- Pictures are about 60% of a standard download and audio about 40%.
- Audio has no variants today. Pictures do: standard and high-res already exist, and `HighResUpgradeRunner` already swaps standard for high-res in the background. The quick-to-standard swap can reuse that pattern.

**Decisions (decided by this planning session, 2026-11-27, from the owner's answer):**
- A third, smaller variant, "quick", is built for every store book and its packs. It is listed beside the standard and high-res variants, the way `hdDownloadUrl`/`hdBytes` are.
- The switch rule works on the first book in the first-books step only:
  - Start the standard download.
  - If after about 3 s the measured speed predicts more than about 10 s left, cancel it and fetch the quick variant.
  - When the first book is playable, download the standard package in the background. Swap it in when it is complete and verified. The reading position is kept and nothing shows a gap.
- Quick audio stays AAC, at 24 kb/s mono. Opus would save another 0.3 MB per book but changes the codec, the decoders and the publish pipeline. That's not worth it for the first version.

**Open for the owner:**
- After item 2, the quick pictures (448x600) and audio (24 kb/s) need a look and a listen on a phone. Are they good enough for a child's first book? The slice publishes a private side-by-side page (standard against quick) for that look and listen (`voice-pick` pattern). The owner's pick sets the final picture size and audio bitrate before anything ships.

**Work:**
- [ ] 1. Build the quick variant in `prepare-story-pictures.ps1` and `premade-audio/encode.py`. Publish it with `publish-store-books.mjs` (with tests), and list it in `worker/stories.ts` as `quickDownloadUrl`/`quickBytes`, per pack too.
- [ ] 2. Owner check: the side-by-side page for the four free books in English and German. Record the pick in this file.
- [ ] 3. App: the switch rule in the first-books download (`StoreClient`), and a variant flag on the installed book. A background upgrade from quick to standard (and on to high-res where `PictureSizeChoice` says so) reuses `HighResUpgradeRunner`, including on a metered link. A quick book never stays quick once a standard download has completed.
- [ ] 4. Count it: `wait_shown` detail gains `quick` when the switch fires, and the upgrade's result is a closed-list `fallback` or `ui` detail. No new blobs.
- [ ] 5. Android, iOS and Windows all get it, through the shared MAUI code.

**Verification:**
- Publish-script and Worker tests list all three variants.
- App tests cover the switch threshold, the cancel-and-switch, the background swap, and a swap failure that keeps the quick copy.
- Device runs on Android, iOS and the Windows VM throttled to 256 kbit/s: the first book plays within about 15 s, and it is replaced by standard later.
- Ninjalog, after release: `index=dino_usage event=wait_shown screen=first_books | stats dc(session) by detail` shows `quick` firing for slow links, and `getting_file:>20` near zero.
