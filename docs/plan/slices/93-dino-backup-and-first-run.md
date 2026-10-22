# Slice 93 — Dino backup file and a first run that asks the language

*Planned 2026-10-22.* Source: phone scope "Dino Reader: starter stories, story store, better
onboarding, backup" (Scope Inbox, 2026-10-22).

**Batch:** 16 (Dino Reader content)
**Depends on:** Slice 91 (the `.dino` package); the backup and first-run screens in Claude Design (Decision 99)
**Touches:** `src/DinoReader.Core/StoryBackup*.cs` (new), `src/DinoReader.App/BackupPage.xaml` (new), `src/DinoReader.App/BackupPage.xaml.cs` (new), `src/DinoReader.App/FirstRunPage.xaml` (new), `src/DinoReader.App/FirstRunPage.xaml.cs` (new), `src/DinoReader.App/SpeechStartup.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `docs/design/dino-reader/**`, `src/DinoReader.App/CLAUDE.md`
**Story issue:** —

## Why

A scanned story with a hand-fixed cast is real work, and losing it with the phone must not
happen. Dino's first run also picks English silently and downloads the voice in the background
(`SpeechStartup`: `_settings.Language ??= English`). A non-English family is never asked, and
nobody can tell when the voice is ready.

## Decisions locked with the user (2026-10-22)

- Backup: one file (a zip of `.dino` packages with its own `schemaVersion`), made from behind the
  grown-up gate and saved through the share sheet or save dialog. Restore imports every story
  whose id isn't already present and skips the rest. Android `allowBackup="false"` (Slice 58)
  stays as it is (Decision 97).
- iOS device backup already includes `AppDataDirectory/stories`, so leave it.
- First run: the user picks the language, then sees the voice pack download with its progress
  and a "ready" moment. No tutorial (Decision 98).

## Work

- [ ] 0. **Design gate.** The backup and first-run screens go into `docs/design/dino-reader/` first.
- [ ] 1. **Backup round-trip (Core, first).** `StoryBackupWriter` and `StoryBackupReader` on top of
  Slice 91's package. Tests: restore skips existing ids, refuses a newer schema, and survives a
  corrupt entry.
- [ ] 2. **Backup screen** behind the gate: "Save a backup" and "Restore from a backup", reporting
  how many stories were restored and skipped.
- [ ] 3. **First-run page.** Language choice (the languages with a Dino voice pack), then the pack
  download progress and a ready state. `SpeechStartup` stops defaulting to English on its own.
  Shown once; the Slice 91 starters install alongside it.
- [ ] 4. Rules into `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Emulator: a fresh install asks the language and shows the download and ready state. Make a
  backup, wipe the app data and restore: stories and casts come back. Restoring twice adds nothing.

## Not yet verified

_(filled in when built)_
