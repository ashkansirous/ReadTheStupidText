# Slice 77 — Dino Reader: covers that survive edits, a cover picker, and readable inputs

*In progress (wave 2026-10-22), draft PR.*

**Batch:** 13
**Decisions:** 84
**Depends on:** Slice 63 (page images kept)
**Touches:** `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/StoryDraft.cs`, `src/DinoReader.Core/ImportedText.cs`, `src/DinoReader.Core/FileStoryLibrary.cs`, `src/DinoReader.App/AddStoryPage.xaml`, `src/DinoReader.App/AddStoryPage.xaml.cs`, `src/DinoReader.App/EditStoryPage.xaml`, `src/DinoReader.App/EditStoryPage.xaml.cs`, `src/DinoReader.App/StoryCover.xaml`, `src/DinoReader.App/StoryCover.xaml.cs`, `src/DinoReader.App/StoryTile.cs`, `src/DinoReader.App/CoverPicker.cs` (new), `src/DinoReader.App/App.xaml`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/**/*.xaml` (TextColor audit only), `tests/DinoReader.Tests/GalleryTests.cs`, `tests/DinoReader.Tests/LibraryHardeningTests.cs`
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

- [ ] `Story`: `CoverStyle` gains `Picture` (a chosen file) alongside `FirstPage` and
      `Colour`; new `CoverFile` (nullable) and a `CoverPageIndex` (nullable) so "page 3 as
      cover" needs no copy. `CoverImageFile` resolves all three. `StoryJson` round-trips the
      new fields; old JSON without them still loads (test in `LibraryHardeningTests`).
- [ ] `StoryPage` gets `TextMatchesLines` (computed: the page text equals the joined `Lines`
      text, whitespace-insensitive). `ImportedText.Unchanged` no longer decides whether
      images are kept; `TryKeepPictureAsync` runs for every page with a source picture. The
      reading screen uses `TextMatchesLines` to decide whether to draw boxes for that page.
- [ ] `StoryDraft` / `AddStoryPage.Save`: default `CoverStyle = FirstPage` when any page has
      an image.
- [ ] `FileStoryLibrary`: delete-story removes `cover.jpg` too; saving a cover photo copies it
      into the story folder via the existing temp-file-and-rename path.
- [ ] `CoverPicker` (a bottom sheet / page): a horizontal strip of the story's page
      thumbnails, a "Pick a photo" tile (`MediaPicker.PickPhotoAsync`), and the colour
      swatches from `CoverPalette`. Opened from `EditStoryPage` (and later from the Slice 78
      wizard's cover step — build it as a reusable view now).
- [ ] `StoryCover` shows `CoverImageFile` for all three styles, keeps the title label beneath
      the tile (gallery) — no behaviour change for colour covers.
- [ ] `App.xaml.cs`: `UserAppTheme = AppTheme.Light`. `App.xaml`: implicit `Entry` and
      `Editor` styles with `TextColor` = ink, `PlaceholderColor` = muted, `BackgroundColor`
      transparent. Remove the per-control `PlaceholderColor` now covered by the style.
- [ ] RTST Mobile: audit `Entry`/`Editor` in `src/ReadTheStupidText.Mobile` for the same
      missing `TextColor` under dark mode; fix only if reproduced (RTST Mobile does use
      `AppThemeBinding`, so it may be fine).
- [ ] Tests: `StoryJson` round-trip with each `CoverStyle`; `CoverImageFile` resolution;
      `TextMatchesLines` true for unchanged, false after an edit; gallery tile order
      unchanged (`GalleryTests`).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Emulator (`device-test`, system dark mode on): Add a story → Type or paste → typed text is
  dark on the white card. Photos import → save → the gallery tile shows page 1. Edit the
  text, save → tile still shows page 1; reading the edited page shows the picture without
  boxes, the unedited pages still highlight. Edit → cover → pick page 2, then a gallery
  photo, then a colour — tile follows each time, survives a restart.
