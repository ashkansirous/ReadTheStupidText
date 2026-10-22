# Slice 78 — Dino Reader: the page-review wizard (pages → cover → text)

*Built 2026-10-22 (wave 2026-10-22), PR #235. Emulator-tested on RTST_Test; not yet verified on the items below.*

**Batch:** 13
**Decisions:** 85
**Depends on:** Slice 77 (cover model and `CoverPicker`), Slice 63 (positioned OCR), Slice 45 (document scanner)
**Touches:** `src/DinoReader.App/StoryWizardPage.xaml` (new), `src/DinoReader.App/StoryWizardPage.xaml.cs` (new), `src/DinoReader.App/PagesStep.xaml` (new), `src/DinoReader.App/PagesStep.xaml.cs` (new), `src/DinoReader.App/CoverStep.xaml` (new), `src/DinoReader.App/TextStep.xaml` (new), `src/DinoReader.App/TextStep.xaml.cs` (new), `src/DinoReader.App/PageEditorPage.xaml` (new), `src/DinoReader.App/PageEditorPage.xaml.cs` (new), `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/ScanPages.cs`, `src/DinoReader.App/AddStoryPage.xaml` (removed), `src/DinoReader.App/AddStoryPage.xaml.cs` (removed), `src/DinoReader.App/EditStoryPage.xaml` (removed), `src/DinoReader.App/EditStoryPage.xaml.cs` (removed), `src/DinoReader.App/PagesPage.cs` (removed), `src/DinoReader.App/PageTextPage.cs`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ManagePage.xaml.cs`, `src/DinoReader.App/Resources/Strings/**`, `src/DinoReader.Core/StoryDraft.cs`, `src/DinoReader.Core/PageDraft.cs` (new), `src/DinoReader.Core/PageOrder.cs` (new), `src/ReadTheStupidText.Mobile.Shared/Images/ImageTransform.cs` (new), `src/ReadTheStupidText.Mobile.Shared/Images/CropRect.cs` (new), `src/ReadTheStupidText.Mobile.Shared/Platforms/**/ImageTransform*.cs` (new), `src/ReadTheStupidText.Mobile.Shared/ScannedImageFiles.cs`, `tests/DinoReader.Tests/PageOrderTests.cs` (new), `tests/DinoReader.Tests/StoryDraftTests.cs` (new), `tests/ReadTheStupidText.Tests/CropRectTests.cs` (new); edited outside the original list: `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/CoverStep.xaml.cs` (new), `src/DinoReader.App/LanguageCard.cs`, `src/DinoReader.Core/ImportedText.cs`, `tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` (CropRect link), `tests/DinoReader.Tests/GalleryTests.cs`, `tests/DinoReader.Tests/ScanLayoutTests.cs`
**Story issue:** —

## Goal

Adding or editing a story is one wizard with three steps. **Pages:** every picked photo,
scanned page and PDF page as a thumbnail grid you can drag to reorder, rotate, crop, delete,
or add to. **Cover:** pick a page, a photo or a colour (Slice 77's picker). **Text:** title,
per-page text, language, cast check, save. OCR runs when the Pages step is confirmed — after
crop and rotate, which change its result — with per-page progress, and re-runs only for
pages that changed. The same wizard opens for an existing story from grown-up mode.

## Why

Today `MediaPicker.PickPhotosAsync` hands the pictures to OCR in picker order with no
preview; there is no crop or rotate for Photos (the ML Kit scanner has its own, Android
only); `PagesPage` offers move up/down on text pages only; `AddStoryPage` runs OCR before
you have seen the pages (image 5 shows 0 characters found with no way to tell why). The
user asked for: show all pictures and scans, crop/rotate/reorder each, pick one cover, then
title and text (2026-10-22).

## Decisions locked with the user (2026-10-22)

- **In-app review on all three platforms** (Android, iOS, Windows), not the ML Kit
  gallery-import mode: one `PagesStep` in Dino, one `ImageTransform` (rotate by 90° steps,
  rectangular crop) in `Mobile.Shared` with a platform implementation each
  (`SkiaSharp`/`Microsoft.Maui.Graphics` on all three if it covers rotate+crop; otherwise
  Android `Bitmap`, iOS `UIImage`, Windows `BitmapDecoder`). Confirm the API via context7
  before coding.
- **Drag to reorder** via `CollectionView.CanReorderItems` (MAUI supports it on all three
  platforms); page numbers on every thumbnail; no up/down arrows.
- **OCR after the review**, not during picking. Changing a page (rotate/crop/replace)
  invalidates only that page's OCR.
- The wizard **replaces** `AddStoryPage`, `EditStoryPage` and `PagesPage`. `PageTextPage`
  survives as the per-page text editor the Text step opens.
- Scan (ML Kit document scanner) stays the capture path on Android; its output pages land
  in the Pages step like any other picture. Windows/iOS keep the dimmed Scan tile.

## Steps

Smallest end-to-end first: the Pages step with reorder and delete, wired into Add, then
the rest.

- [x] `PageDraft(string SourceFile, int RotationQuarterTurns, CropRect? Crop, OcrReading? Ocr,
      string? EditedText)` and `StoryDraft.Pages: List<PageDraft>`; `PageOrder` (pure: move,
      remove, insert-at, renumber) in `DinoReader.Core` with tests.
- [x] `StoryWizardPage`: a step header (1 Pages · 2 Cover · 3 Text), Back/Next, and a single
      `StoryDraft` shared by the steps. Opens from the gallery's Add tile and from grown-up
      Edit (Slice 80) with an existing story loaded into the draft.
- [x] `PagesStep`: 3-column thumbnail `CollectionView` (`CanReorderItems="True"`), page
      number badge, per-tile `⋯` → Rotate / Crop / Delete, "Add pages" tile offering Scan
      (Android), Photos, PDF or file. PDF pages render to images via the existing
      `RenderedPdf` path so they behave like photos. Empty state explains the four sources.
- [x] `ImageTransform` in `Mobile.Shared`: `RotateAsync(file, quarterTurns)` and
      `CropAsync(file, CropRect)` writing a new file next to the source (never in place, so
      Undo is a pointer change). `CropRect` is normalised (0–1) like `NormalizedRect`.
      EXIF orientation is applied before any transform (Slice 63 found EXIF-rotated photos).
- [x] `PageEditorPage`: full-screen page with rotate-left/right buttons and a draggable
      rectangular crop frame with corner handles; Done writes the transform into the
      `PageDraft`. No perspective correction (the scanner already does that for scans).
- [x] Confirming Pages → `StoryImport.ReadPicturesAsync` over the drafts in order, skipping
      pages whose `Ocr` is still valid; a per-page progress row ("Reading page 3 of 7")
      replaces the current "1 page read · 0 characters found" chip. Pages with no text are
      kept (a picture-only page) and flagged, not dropped — the grown-up decides.
- [x] `CoverStep`: Slice 77's `CoverPicker` over the draft's pages.
- [x] `TextStep`: title (pre-filled from the first heading `LineReflow` finds, Slice 76),
      language, one card per page showing its thumbnail and text (tap → `PageTextPage`),
      "Check the cast", Save. Save writes pages, images, cover and transforms via
      `FileStoryLibrary` (transformed files are the ones kept; sources are deleted as today).
- [x] Edit mode: the draft loads from an existing `Story` (pages with their images, text,
      cover); Save replaces the story atomically (temp folder + rename, as today).
- [x] Remove `AddStoryPage`, `EditStoryPage`, `PagesPage`; move their strings to the new
      pages' keys inside this slice's own block of each `Strings/*.resx`.
- [x] Tests: `PageOrder`; `StoryDraft` → `Story` mapping keeps order, transforms and
      cover; `CropRect` normalisation and rotation of a crop rect by 90° (pure).

## Verification

- Both test suites green.
- Emulator (`device-test`): pick 4 photos out of order → drag page 4 to the front → rotate
  page 2 → crop page 3 → Next → progress shows 4 pages → cover step shows the reordered
  page 1 → Text step shows the four texts in the new order → Save → gallery tile is the new
  page 1 → the story reads in the new order with boxes on the uncropped pages. Back from
  Text to Pages and rotate one page → only that page is re-read. Scan 2 pages, add 1 photo →
  3 pages in one grid. Edit the story from grown-up mode → remove a page → Save → read.
- Windows Dino build: Photos and PDF into the same wizard; rotate/crop work.

## Built

*Changed while building:*

- `StoryDraft.Pages` is kept as a synced projection of `PageDrafts`; the old `RemoveAt`/`Move` API is removed.
- PDF pages are rendered eagerly at pick time, behind the progress overlay.
- Clearing a text-only page removes it.
- "Delete story" lives only on the manage grid.
- Add and Edit open from `ManagePage`; there is no gallery Add tile.
- OCR runs on Next with a progress overlay, only for pages without a valid reading. Rotate/crop keep the edited text and re-OCR for boxes only.
- `StorySaver` uses fresh page names; in edit mode it deletes unused pictures only after a successful save.
- `ImageTransform` has Android, Windows and iOS implementations. 24 strings in 9 locales. Title is prefilled from the first `LineReflow` heading.

*From review* (`reviews/78-review-1.md`, `78-review-2.md`): pass 1, all 11 fixed (6 partly). Pass 2, findings 2-8 fixed; 1 skipped (boxes are only drawn for verbatim lines).

*Checks:* Dino 388 and RTST 686 tests pass; Dino Android and Windows x64 build. iOS is compiled only by the PR's `dinoreader-ios` check. Emulator (RTST_Test): add 3 photos, reorder, rotate, crop, OCR overlay, cover order; edited text survives rotate; edit story, delete a page, save, reads; system back leaves no story: PASS.

#### For CLAUDE.md (lifted 2026-10-22)

In `src/DinoReader.App/CLAUDE.md`:

- The story editor is `StoryWizardPage` (Pages, Cover, Text); `AddStoryPage`/`EditStoryPage`/`PagesPage` are gone. (Slice 78: docs/plan/slices/78-dino-page-review-wizard.md)
- Never delete a story picture before the new `story.json` is saved (`StorySaver`). (Slice 78: docs/plan/slices/78-dino-page-review-wizard.md)
- Rotate/crop invalidate only that page's reading; edited text is kept. (Slice 78: docs/plan/slices/78-dino-page-review-wizard.md)

#### Not yet verified

- Scan 2 pages + 1 photo with the ML Kit scanner (needs a Google-signed device).
- Windows rotate/crop and drag-reorder.
- iOS on a device.
- "Only the changed page is re-read" counted.
- Photos picker adds one image per pick; check whether multi-select is expected.
- No discard confirmation on system back.
