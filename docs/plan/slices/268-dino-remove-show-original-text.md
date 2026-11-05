# Slice 268 — Dino: remove "Show original text" from grown-up mode

*Built 2026-11-04, merged as PR #825 (a52b1dac). Checked on the Android emulator (emulator-5554, build cfd10992f4e6); iOS is compiled by CI only.* Dino Reader, Android and iOS alike (and the Windows head, which shares the pages). User, 2026-11-04: "there is a part in the parents mode that allows reading a book in the original language. I dont need that. remove it". Fits the rule in Slice 267: a book has no language of its own; it is read in the family's languages.

**Touches:** `src/DinoReader.App/BookSheet.cs`, `src/DinoReader.Core/BookSheetPlan.cs`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/ReadingText.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.Core/ReadingTranslation.cs`, `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/StoryPackageWriter.cs`, `src/DinoReader.Core/StarterUpdate.cs`, `src/DinoReader.Core/LanguagePick*.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `.maestro/dino/**`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/Resources/Raw/testbooks/{three-little-pigs,gingerbread-man,tortoise-and-hare,good-enough}.dino`, `docs/plan/slices/268-dino-remove-show-original-text.md`.

## What it is today (read on origin/main 2026-11-04)

The book's ⋯ sheet in grown-up mode has a "Show original text" switch with a hint (`BookSheet.cs:294-299, 626-639`, shown when `BookSheetPlan.ShowOriginalText`), saved per book as `Story.ShowOriginalText` (`GalleryPage.xaml.cs:67, 725`). When it is on, the reading page shows the book's own words instead of the family's language (`ReadingPage.xaml.cs:404`) and the language switch treats the book differently (`LanguagePick.BookSwitchable`, `ReadingPage.xaml.cs:230, 275`). Strings `Book_ShowOriginal` and `Book_ShowOriginalHint` in all 15 resx files.

## Work

- [x] Remove the row and its hint from the sheet and from `BookSheetPlan`; remove the save handler.
- [x] Reading page: always read in the family's languages; drop the `ShowOriginalText` branch and the parameter in `LanguagePick.BookSwitchable` (with its tests).
- [x] Books saved with `ShowOriginalText = true` (existing installs): read as if it were false; the field is still read from an older `story.json` and dropped, so nothing breaks (same pattern as `Merges`, Slice 151).
- [x] Remove the two strings from all 15 resx files and `AppResources.cs`; update any flow or test that taps or asserts "Show original text".
- [x] `src/DinoReader.App/CLAUDE.md`: remove "Show original text" from the `BookSheet` rule lines.
- [x] Android task (emulator: the sheet has no such row). iOS task: same shared code, compiled by CI only (no iOS runtime check, see Not yet verified).

## Verification

- `dotnet test tests/DinoReader.Tests`; emulator flow: open a book's sheet in grown-up mode, no "Show original text"; a book saved with the old flag on reads in the app language.
- Emulator (emulator-5554, build cfd10992f4e6): `grown-ups-button`, `settings-done-button`, `dino-landscape-gate` and the new `book-sheet-no-original.yaml` PASS.

*Changed while building:*
- Removed `Story.ShowOriginalText` and every branch that read it: `StoryTranslator`, `StoryVoicing`, `ReadingTranslation`, `ReadingPage`, `StarterUpdate`, `StoryPackageWriter`; also the `translated` parameter of `BookSheetPlan` and the `BookSwitchable` parameter of `LanguagePick`.
- Strings removed from 16 resx files (not 15) plus `AppResources.cs` and `ReadingText.cs`.
- An old `story.json` with `ShowOriginal: true` still loads and the field is dropped (`TranslatedStoryTests`).
- The four Debug-only test books in `src/DinoReader.App/Resources/Raw/testbooks/` were rebuilt, since the package no longer carries the flag. No version bump: `BookHash` covers the source files, which are unchanged, and no store-only book changes version.

*From review:* (`reviews/s268-high.md` findings 6/6 read, `reviews/s268-medium.md` findings 2/2 read)
- High 1 (`StoryJson.cs:66`, an old file's flag is dropped silently and the old-file test passes only because unmapped members are ignored; `UnmappedMemberHandling.Disallow` later would make old files throw): skipped, the switch to the translation is by design.
- High 2 (`src/DinoReader.App/CLAUDE.md:161` stale Slice 110 sentence on when the sheet opens): applied.
- High 3 (`BookSheet.cs` stale comments, double blank line): applied.
- High 4 (rename the test `A_book_is_switchable_only_with_a_choice_a_translation_and_no_recordings`): skipped, naming only.
- High 5 (`StoryTranslator.cs:152` ungrammatical `ShelfTitle` comment): applied.
- High 6 (`ReadingPage.xaml.cs:374` comment merged into one overlong line): skipped, style.
- Medium 1 (`ReadingTranslation.cs:13` comment says "with the switch on"): skipped, comment nit.
- Medium 2 (`ReadingPage.xaml.cs:374`, same as high 6): skipped, comment nit.

#### For CLAUDE.md (lifted 2026-11-05)

None beyond the `src/DinoReader.App/CLAUDE.md` edit already made in this slice.

#### Not yet verified

- The new flow `book-sheet-no-original.yaml` was not shown failing on the old build.
- A book saved with the old flag on, read on a device.
- Windows head.
- iOS runtime (CI compiles iOS).
