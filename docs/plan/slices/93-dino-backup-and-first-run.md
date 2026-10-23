# Slice 93 — Dino backup file and a first run that asks the language

*Built 2026-10-23, not yet fully verified (Windows and iOS not run; see Not yet verified).* Planned 2026-10-22. Source: phone scope "Dino Reader: starter stories, story store, better
onboarding, backup" (Scope Inbox, 2026-10-22).

**Batch:** 16 (Dino Reader content)
**Depends on:** Slice 91 (the `.dino` package); the backup and first-run screens in Claude Design (Decision 99)
**Touches:** `src/DinoReader.Core/StoryBackup*.cs` (new), `src/DinoReader.App/BackupPage.xaml` (new), `src/DinoReader.App/BackupPage.xaml.cs` (new), `src/DinoReader.App/FirstRunPage.xaml` (new), `src/DinoReader.App/FirstRunPage.xaml.cs` (new), `src/DinoReader.App/LanguagePage.xaml` (new), `src/DinoReader.App/LanguagePage.xaml.cs` (new), `src/DinoReader.App/SpeechStartup.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `docs/design/dino-reader/**`, `src/DinoReader.App/CLAUDE.md`
**Story issue:** —

## Why

A scanned story with a hand-fixed cast is real work, and losing it with the phone must not
happen. Dino's first run also picks English silently and downloads the voice in the background
(`SpeechStartup`: `_settings.Language ??= English`). A non-English family is never asked, and
nobody can tell when the voice is ready.

## Decisions locked with the user (2026-10-22)

- Backup: one file (a zip of `.dino` packages with its own `schemaVersion`), made from behind the
  grown-up gate and saved through the share sheet or save dialog. Restore imports every story
  whose id isn't already present and skips the rest; unlike a store import, restore keeps each
  story's backed-up id, so restoring twice adds nothing. Android `allowBackup="false"` (Slice 58)
  stays as it is (Decision 97).
- iOS device backup already includes `AppDataDirectory/stories`, so leave it.
- First run: the user picks the language, then sees the voice pack download with its progress
  and a "ready" moment. No tutorial (Decision 98).

## Design (Batch 16 mock, screens 16-19 and 24-26, added 2026-10-22)

Source: `docs/design/dino-reader/Dino Reader Batch 16.dc.html`, screens 16 (line 28), 17 (55), 18 (81), 19 (100), 24 (244), 25 (268), 26 (217). 7 frames. The mock agrees with the plan, apart from the open points below. All phone frames are 360x760, 34 px radius, page background `#FFF6E6`.

- **First run, language (16).** App icon 92 px (24 px radius), "Hello! Which language do we read in?" (24 px/600), "Dino downloads a voice for it. You can change it later behind the lock." (14 px, `#5a5470`). Two-column grid of 52 px buttons, 16 px radius, 16 px/600: English (selected: `#5B57E8` fill, white text, 18 px check), Español, Français, Português, Русский, 中文, 日本語, العربية (`dir="rtl"`, 700), فارسی. Each shows its own name. The list holds only languages with a Dino voice pack, the phone's language selected first. "Continue": 56 px, 28 px radius, `#FF7A59` with a 5 px `#E2583A` under-shadow, 17 px/600, at the bottom. Shown once, before the gallery. `SpeechStartup` stops defaulting to English.
- **First run, download (17).** "Getting Dino's voice ready", "English voices · this happens only once", a progress bar with "Downloading…" and the live percentage ("64%", `#5B57E8`), "Waiting on the shelf" with three 56 px 3:4 starter covers, and a "Pick another language" link. Real pack progress. Going back to change the language cancels the download. The starters install alongside.
- **First run, ready (18).** "Ready to read!", "Every character gets their own voice. Three stories are on the shelf.", a lavender `#ECE7FF` pill "Hear Dino say hello" (48 px, 34 px round play badge) that plays the hello line in the new voice, and the coral button "Set up the stories". The button goes to grown-up mode (19), not the kids' shelf. After the hand-over the app always opens on the kids' gallery.
- **First grown-up (19).** The Slice 80 grown-up gallery: header "My stories" over "Grown-up mode" (12.5 px, `#5B57E8`), an "English" language pill on the right (44 px, 22 px radius, white, inset `#c9c0f5` border, `#5B57E8` text 14.5 px/600), a button row "Free stories" and "Backup" (48 px, see Slice 92), then the starters. The bottom button is "Ready for my child" (56 px, coral, open-book icon). It switches to the kids' view (screen 20). On later visits the header shows "Done" instead.
- **Backup (24).** Header "Backup". Card 1: "Save a backup", "All 5 stories in one file, with their pages, pictures and fixed-up casts. Save it to Drive or Files, or send it to yourself." (the count is live), filled button "Save a backup" (50 px, 25 px radius, `#5B57E8`, 15.5 px/600). Card 2: "Restore from a backup", "Adds the stories this phone doesn't have. Stories already here stay exactly as they are.", outlined button "Choose a backup file" (2 px `#c9c0f5` border). Footer: "The file only goes where you send it. Dino keeps no copy online." One `.zip` of `.dino` packages, saved through the share sheet (Android, iOS) or a save dialog (Windows).
- **Restore result (25).** "4 stories restored", rows with a coloured dot: "Added to the shelf" 4 (`#4FC060`), "Already here, skipped" 2, "Couldn't be read" 1 (this row only when a story is damaged; the rest still come back), button "See my stories". A backup from a newer app is refused before anything is added: "This backup is from a newer Dino Reader. Update the app, then try again."
- **Language (26).** Grown-up page opened from the language pill. Header "Language" over "Dino reads and translates into this". The nine languages, one per row (54 px min height): the current one on `#F4F2FF` with a filled `#5B57E8` 26 px check circle; "Ready" in `#2E7D32` for each installed language, "Download" for the rest; a downloading row has an outlined circle, "Downloading 38%" and an 8 px progress bar. Footer: "A story in another language is still read in its own language, unless Translate to my language is on for that story." Picking a language without a voice switches to it at once and downloads in that row (Slice 81); the old language keeps reading until the new one is ready. It also sets the app's words and the translation language.
- New strings (both locales): everything quoted above, including "Continue", "Pick another language", "Hear Dino say hello", "Set up the stories", "Ready for my child", "Done", "Language", "Ready", "Download", "Downloading {n}%", "{n} stories restored", the three result rows, and the newer-backup refusal. The nine language names are own-name and are not translated.

**Settled (2026-10-22, from the mock): the grown-up Language page.** No slice had it, and Dino's `GalleryPage.xaml` has no language pill (the only language rule is `src/DinoReader.App/CLAUDE.md:23`). Added here as work item 5, with `LanguagePage.xaml` in Touches.

**Settled (2026-10-22, from the mock): where first run ends.** First run ends in grown-up mode (screen 19), not the kids' shelf. Set the first-run flag at "Set up the stories", so a kill mid-way does not repeat the picker.

**Settled (2026-10-22, from the mock): "Done" vs "Ready for my child".** Same gallery, two bottom buttons: "Ready for my child" only until the first hand-over, then "Done" as in Slice 80.

## Work

- [x] 0. **Design gate.** Done 2026-10-22: the mock (`Dino Reader Batch 16.dc.html`, screens 16-19 and 24-26) is
  in `docs/design/dino-reader/`, see Design above.
- [x] 1. **Backup round-trip (Core, first).** `StoryBackupWriter` and `StoryBackupReader` on top of
  Slice 91's package. Tests: restore skips existing ids, refuses a newer schema, and survives a
  corrupt entry.
- [x] 2. **Backup screen** behind the gate: "Save a backup" and "Restore from a backup", reporting
  how many stories were restored and skipped.
- [x] 3. **First-run page.** Language choice (the languages with a Dino voice pack), then the pack
  download progress and a ready state. `SpeechStartup` stops defaulting to English on its own.
  Shown once; the Slice 91 starters install alongside it.
- [x] 4. Rules into `src/DinoReader.App/CLAUDE.md`.
- [x] 5. **Grown-up Language page** (mock screen 26, added with the design). A language pill in the
  grown-up gallery header opens `LanguagePage`: the nine voice-pack languages with Ready /
  Download / Downloading n%, the same choice as first run, starting that language's pack on pick
  (the Slice 81 behaviour, via `ILanguageChangeNotifier`). Also the "Ready for my child" button on
  the first grown-up view.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Emulator: a fresh install asks the language and shows the download and ready state. Make a
  backup, wipe the app data and restore: stories and casts come back. Restoring twice adds nothing.

## Changed while building

- First-run detection is "no stored language" (an old install always has one, so it never sees the picker).
- A handover flag (`FirstRunState.HandoverPending`) drives "Ready for my child"; the language pill shows in grown-up mode on every visit.
- "Pick another language" stops awaiting the download but does not cancel the installer, which is left untouched.
- The Windows save dialog sits under `#if WINDOWS` in `BackupPage`. All nine languages are listed.
- The backup is a `.zip` of `.dino` files. Restore dedupes by id or `SourceId` and has a 2 GiB budget; overflow stops the restore and reports "Too large".
- Strings are machine-drafted in 9 locales. The Dino UI follows `UiFlow` (RTL for Farsi), like the existing Dino pages. The Persian UI font stays until restart after switching back from Farsi.

## From review

- Review 1 (high): findings 1-9 fixed; the upgrade path was confirmed (old installs always store a Language).
- Review 2 (medium): findings 1-9 fixed. #6 only partly: Android and iOS cannot tell whether the share sheet saved the file.
- Device (`93-device.md`): 6/6 pass. The gallery language bug found there was fixed afterwards with `DinoApp.RebuildRoot`; that fix was not re-run on a device.

## Not yet verified

- The language rebuild after the first run (`DinoApp.RebuildRoot`), on a device.
- Windows backup save and restore.
- iOS.
- Native-speaker review of the new strings in the 9 locales.

#### For CLAUDE.md (lifted 2026-10-23)

- Backup is one zip (`backup.json` + a `<id>.dino` per story, Core `StoryBackupWriter`/`StoryBackupReader`); restore keeps each story's id, dedupes by id or `SourceId`, counts a damaged package without stopping, stops at a 2 GiB budget ("Too large") and refuses a newer `backup.json` before adding anything. (Slice 93: docs/plan/slices/93-dino-backup-and-first-run.md)
- A fresh install has no stored language and that is what shows `FirstRunPage`; never store a default language at launch (`SpeechStartup`). `LanguageChoice.Apply` is the one place a language is picked. (Slice 93: docs/plan/slices/93-dino-backup-and-first-run.md)
- `BackupPage` shares via `Share` (Android, iOS) or a `FileSavePicker` (Windows, `#if WINDOWS`), inside `ExternalActivityGuard`; the mobile share sheet cannot report whether the file was saved. (Slice 93: docs/plan/slices/93-dino-backup-and-first-run.md)
- Changing language after first run rebuilds the root with `DinoApp.RebuildRoot`; `FirstRunState.HandoverPending` swaps "Done" for "Ready for my child" until the first hand-over. (Slice 93: docs/plan/slices/93-dino-backup-and-first-run.md)

(Already added to `src/DinoReader.App/CLAUDE.md` on this branch by the implementer as one bullet; the index pass should not lift it twice.)

#### For README.md (lifted 2026-10-23)

Not user-facing beyond the app itself; nothing to add.
