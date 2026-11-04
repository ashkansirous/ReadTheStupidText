# Slice 268 — Dino: remove "Show original text" from grown-up mode

*In progress (wave 2026-11-04), draft PR.* Dino Reader, Android and iOS alike (and the Windows head, which shares the pages). User, 2026-11-04: "there is a part in the parents mode that allows reading a book in the original language. I dont need that. remove it". Fits the rule in Slice 267: a book has no language of its own; it is read in the family's languages.

**Touches:** `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.Core/BookSheetPlan.cs`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingText.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.Core/ReadingTranslation.cs`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/StoryPackageWriter.cs`, `src/DinoReader.Core/StarterUpdate.cs`, `src/DinoReader.Core/LanguagePick*.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `.maestro/dino/**`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/268-dino-remove-show-original-text.md`.

## What it is today (read on origin/main 2026-11-04)

The book's ⋯ sheet in grown-up mode has a "Show original text" switch with a hint (`BookSheet.cs:294-299, 626-639`, shown when `BookSheetPlan.ShowOriginalText`), saved per book as `Story.ShowOriginalText` (`GalleryPage.xaml.cs:67, 725`). When it is on, the reading page shows the book's own words instead of the family's language (`ReadingPage.xaml.cs:404`) and the language switch treats the book differently (`LanguagePick.BookSwitchable`, `ReadingPage.xaml.cs:230, 275`). Strings `Book_ShowOriginal` and `Book_ShowOriginalHint` in all 15 resx files.

## Work

- [ ] Remove the row and its hint from the sheet and from `BookSheetPlan`; remove the save handler.
- [ ] Reading page: always read in the family's languages; drop the `ShowOriginalText` branch and the parameter in `LanguagePick.BookSwitchable` (with its tests).
- [ ] Books saved with `ShowOriginalText = true` (existing installs): read as if it were false; the field is still read from an older `story.json` and dropped, so nothing breaks (same pattern as `Merges`, Slice 151).
- [ ] Remove the two strings from all 15 resx files and `AppResources.cs`; update any flow or test that taps or asserts "Show original text".
- [ ] `src/DinoReader.App/CLAUDE.md`: remove "Show original text" from the `BookSheet` rule lines.
- [ ] Android task (emulator: the sheet has no such row; a book that had it on reads in the family's language). iOS task (same shared code; iOS compile in CI).

## Verification

- `dotnet test tests/DinoReader.Tests`; emulator flow: open a book's sheet in grown-up mode, no "Show original text"; a book saved with the old flag on reads in the app language.
