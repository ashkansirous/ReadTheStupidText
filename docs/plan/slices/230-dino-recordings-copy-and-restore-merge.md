### Slice 230 — Dino: say plainly that recordings stay on the phone, and restore brings them back

*Planned 2026-11-01.* From the user's user test on 2026-11-01 (findings 4 and 5). No design step (copy and logic).

**Touches:** `src/DinoReader.Core/StoryPackageReader.cs`, `src/DinoReader.Core/StoryBackupReader.cs`, `src/DinoReader.Core/StoryBackup.cs`, `src/DinoReader.App/BackupPage.xaml`, `src/DinoReader.App/BackupPage.xaml.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/RecordViews.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/StoryBackupTests.cs`, `tests/DinoReader.Tests/StoryPackageTests.cs`, `docs/plan/slices/230-dino-recordings-copy-and-restore-merge.md`

#### Why

- **Finding 4:** testers thought their recorded voice was saved for them somewhere. It is not: recordings live in the story's folder on the phone and are never uploaded (`AndroidManifest.xml` microphone comment). The Backup page says "Dino keeps no copy online" (`Backup_*`, `AppResources.resx:80-88`) but never mentions recordings, and the Record page says nothing about where they go.
- **Finding 5 (checked in code, not on a device):** a backup includes recordings (`StoryBackupWriter.cs:59`, `includeRecordings: true`) and restore keeps the story id and the page links (`StoryPackageReader.cs:36,197`). But restore **skips a story whose id is already on the phone**, so restoring onto a phone that still has the story (e.g. recordings deleted, or a fresh re-download from the store) brings no recordings back.
- Open: the user's finding said "on save your voice"; no screen has that name. The copy goes on the three places below until the user names the screen they meant.

#### Decisions locked with the user (2026-11-01, RefineScope)

- **Warm, short copy, inline, no popup**, on the Record page, the intro wizard's "Read it in your voice" step and the Backup page. Roughly: "Your recordings stay on this phone, just for your family. Dino never uploads them or backs them up, so keep a copy yourself: Settings → Backup." All 13 languages.
- **Restore merges recordings** into a story that is already on the phone: add every recorded page the phone lacks; where both have one for the same page, the backup's wins.
- Backup page copy says the file includes your recordings.

#### Work

- [ ] Core: restore of an existing story id merges `Recordings` (copy missing audio files, backup wins on the same page), leaves pages, pictures and cast as they are on the phone. Unit tests: fresh restore, merge into a story with none, conflict on one page, a recording whose audio file is missing from the zip (dropped, as today).
- [ ] Round-trip test in Core: write a backup with recordings, read it into an empty library, every recording entry points at an existing file.
- [ ] Copy on Record page, wizard step and Backup page, 13 languages.
- [ ] Android and iOS: device check of back up, delete the recordings, restore, the recorded voice plays (both platforms).

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Scripted emulator flow: record one page, back up, delete the story's recordings, restore, "Read with my voice" plays the page. iOS: same on simulator or TestFlight.
