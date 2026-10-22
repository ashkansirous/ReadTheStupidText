# Slice 77 — Dino Reader: covers that survive edits, a cover picker, and readable inputs

*Built 2026-10-22 (wave 2026-10-22), PR #234; emulator-checked on RTST_Test in dark mode, not yet verified on every path (see Not yet verified).*

**Batch:** 13
**Decisions:** 84
**Depends on:** Slice 63 (page images kept)
**Touches:** `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/StoryDraft.cs`, `src/DinoReader.Core/ImportedText.cs`, `src/DinoReader.Core/FileStoryLibrary.cs`, `src/DinoReader.App/AddStoryPage.xaml`, `src/DinoReader.App/AddStoryPage.xaml.cs`, `src/DinoReader.App/EditStoryPage.xaml`, `src/DinoReader.App/EditStoryPage.xaml.cs`, `src/DinoReader.App/StoryCover.xaml`, `src/DinoReader.App/StoryCover.xaml.cs`, `src/DinoReader.App/StoryTile.cs`, `src/DinoReader.App/CoverPicker.cs` (new), `src/DinoReader.App/App.xaml`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/**/*.xaml` (TextColor audit only), `tests/DinoReader.Tests/GalleryTests.cs`, `tests/DinoReader.Tests/LibraryHardeningTests.cs`, `src/DinoReader.Core/ScanLayout.cs`, `src/DinoReader.App/ScanPages.cs`, `src/DinoReader.Core/IStoryLibrary.cs`, `src/DinoReader.App/Resources/Strings/**`, `tests/DinoReader.Tests/StoryJsonTests.cs`, `tests/DinoReader.Tests/ScanLayoutTests.cs`, `src/DinoReader.App/Platforms/Android/MainActivity.cs`, `src/DinoReader.App/Platforms/Windows/App.xaml.cs`, `src/DinoReader.App/SystemTheme.cs` (new), `src/DinoReader.App/PickedPhotoCache.cs` (new), `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/PagesPage.cs`
**Story issue:** —

## Goal

Every story made from pictures shows its first page as the cover (image 3 today shows flat
colour tiles), the cover survives editing the text, and a grown-up can pick any page, a
gallery photo, or a colour as the cover. Also the two quick fixes nobody should wait a
wizard for: text typed into *Add a story* is readable in system dark mode (image 5), and
the kids' palette is pinned to light so no other control goes dark-on-dark.

## Why

`Story.CoverStyle` is `FirstPage | Colour` and `page-N.jpg` is written to the story folder
— but only when the editor text is unchanged from the import (`ImportedText.Unchanged`),
and `StoryDraft` falls back to `Colour`. One typo fixed → images gone → colour tile. Dino
never sets `UserAppTheme`, and `AddStoryPage`'s `Entry`/`Editor` have no `TextColor`, so in
system dark mode the platform's default light text lands on the white card.

## Decisions locked with the user (2026-10-22)

- **Images always survive edits.** Editing text never deletes page images. Word
  highlighting (Slice 64) only works on pages whose text still matches the OCR lines;
  an edited page shows its picture and reads its text without boxes. (Fuzzy re-mapping of
  edited text onto boxes is out of scope.)
- **Cover sources:** any page image, a photo from the gallery (copied into the story folder
  as `cover.jpg`), or one of the flat colours. Default is the first page image when the
  story has one, else a colour.
- **Light theme pinned.** `UserAppTheme = AppTheme.Light` at Dino startup; night colours
  stay confined to `ReadingTheme` on the reading screen. Explicit `TextColor` on every
  `Entry`/`Editor` style anyway.

## Steps

- [x] `Story`: `CoverStyle` gains `Picture` (a chosen file) alongside `FirstPage` and
      `Colour`; new `CoverFile` (nullable) and a `CoverPageIndex` (nullable) so "page 3 as
      cover" needs no copy. `CoverImageFile` resolves all three. `StoryJson` round-trips the
      new fields; old JSON without them still loads (test in `LibraryHardeningTests`).
- [x] `StoryPage` gets `TextMatchesLines` (computed: the page text equals the joined `Lines`
      text, whitespace-insensitive). `ImportedText.Unchanged` no longer decides whether
      images are kept; `TryKeepPictureAsync` runs for every page with a source picture. The
      reading screen uses `TextMatchesLines` to decide whether to draw boxes for that page.
- [x] `StoryDraft` / `AddStoryPage.Save`: default `CoverStyle = FirstPage` when any page has
      an image.
- [x] `FileStoryLibrary`: delete-story removes `cover.jpg` too; saving a cover photo copies it
      into the story folder via the existing temp-file-and-rename path.
- [x] `CoverPicker` (a bottom sheet / page): a horizontal strip of the story's page
      thumbnails, a "Pick a photo" tile (`MediaPicker.PickPhotoAsync`), and the colour
      swatches from `CoverPalette`. Opened from `EditStoryPage` (and later from the Slice 78
      wizard's cover step — build it as a reusable view now).
- [x] `StoryCover` shows `CoverImageFile` for all three styles, keeps the title label beneath
      the tile (gallery) — no behaviour change for colour covers.
- [x] `App.xaml.cs`: `UserAppTheme = AppTheme.Light`. `App.xaml`: implicit `Entry` and
      `Editor` styles with `TextColor` = ink, `PlaceholderColor` = muted, `BackgroundColor`
      transparent. Remove the per-control `PlaceholderColor` now covered by the style.
- [x] RTST Mobile: audit `Entry`/`Editor` in `src/ReadTheStupidText.Mobile` for the same
      missing `TextColor` under dark mode; fix only if reproduced (RTST Mobile does use
      `AppThemeBinding`, so it may be fine).
- [x] Tests: `StoryJson` round-trip with each `CoverStyle`; `CoverImageFile` resolution;
      `TextMatchesLines` true for unchanged, false after an edit; gallery tile order
      unchanged (`GalleryTests`).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Emulator (`device-test`, system dark mode on): Add a story → Type or paste → typed text is
  dark on the white card. Photos import → save → the gallery tile shows page 1. Edit the
  text, save → tile still shows page 1; reading the edited page shows the picture without
  boxes, the unedited pages still highlight. Edit → cover → pick page 2, then a gallery
  photo, then a colour — tile follows each time, survives a restart.

*Changed while building:*

- Edited text keeps page pictures via `ImportedText.TextsForEditedText`; unsplittable edited text goes on page 1. An edited page keeps boxes on lines still found verbatim (page-level gating dropped because it would kill boxes on PDF pages), so `TextMatchesLines` was removed.
- `StoryDraft` `ChooseCover`/`RemoveAt`/`Move` keep the cover index. The cover photo is stored at Save as `cover-<id>.jpg` via `IStoryLibrary.StoreFileAsync`; the old one is deleted, and only `cover-*.jpg` files are deletable.
- `CoverPicker` Photo chip label is `Edit_CoverPhoto` in 9 locales.
- The reading screen follows `PlatformAppTheme` through a new `SystemTheme` signal (Android `OnConfigurationChanged`, Windows `UISettings.ColorValuesChanged`), because `UserAppTheme` is pinned Light.
- RTST Mobile `Entry`/`Editor` unchanged: no dark-mode bug reproduced.
- Known gap: the picked-photo cache copy leaks on a system back press.

*From review:* `reviews/77-review-1.md` and `77-review-2.md` (in the worktrees' `reviews` folder). Pass 1: all fixed except finding 8 (deliberate). Pass 2: findings 1-7 fixed.

*Checks:* Dino 369 and RTST 655 tests pass; Dino Android and Windows x64, and RTST Mobile, build. Emulator (RTST_Test, dark mode): typed text readable, page-1 tile after import and after edit, photo cover, colour cover survives restart, live night toggle on the reading screen, cancel keeps the cover: pass.

#### For CLAUDE.md

In `src/DinoReader.App/CLAUDE.md`:

- Dino pins `UserAppTheme = Light` (`App.xaml.cs`). The reading screen's night theme must read `PlatformAppTheme` through `SystemTheme.cs`, never `RequestedTheme`. (Slice 77: docs/plan/slices/77-dino-covers-and-light-theme.md)
- Cover photos are `cover-<id>.jpg` in the story folder; only those names may be deleted via `IStoryLibrary.DeleteFileAsync`. (Slice 77: docs/plan/slices/77-dino-covers-and-light-theme.md)

#### Not yet verified

- Page-2 cover pick on a multi-page story.
- Photo cover survives an app restart.
- Windows live theme switch (`UISettings.ColorValuesChanged`).
