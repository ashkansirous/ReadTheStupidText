### Slice 230 — Dino: say plainly that recordings stay on the phone, and restore brings them back

*In progress (wave 2026-11-01), draft PR.* From the user's user test on 2026-11-01 (findings 4 and 5). No design step (copy and logic).

**Touches:** `src/DinoReader.Core/StoryPackageReader.cs`, `src/DinoReader.Core/StoryBackupReader.cs`, `src/DinoReader.Core/StoryBackup.cs`, `src/DinoReader.Core/StoryRecordings.cs`, `src/DinoReader.Core/IStoryLibrary.cs`, `src/DinoReader.App/FileStoryLibrary.cs` (wherever it lives), `src/DinoReader.App/BackupPage.xaml`, `src/DinoReader.App/BackupPage.xaml.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/RecordViews.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.App/Testing/DinoTestCommands.Backup.cs` (new), `src/DinoReader.App/Testing/DinoTestCommands.cs` (key registration only), `tools/emulator/flows/dino-backup-restore-recordings.yaml` (new), `tests/DinoReader.Tests/StoryBackupTests.cs`, `tests/DinoReader.Tests/StoryPackageTests.cs`, `tests/DinoReader.Tests/` (new test files), `docs/plan/slices/230-dino-recordings-copy-and-restore-merge.md` (widened at the claim, 2026-11-01, from a premise check)

#### Coordinator's design notes (claim, 2026-11-01)

Checked on origin/main before the claim:

- **The skip.** It is in `StoryBackupReader.cs:125-127` (`known.Contains(packaged.Id) || known.Contains(source)` returns `Outcome.Skipped`) and in `ImportKeepingIdAsync` (:131-133), not in `StoryPackageReader`. `StoryPackageReader.cs:36` keeps recordings when `keepId` is set. Recordings are `Story.Recordings` (`IReadOnlyDictionary<int, PageRecording>?`, `Story.cs:100-101`). Their audio files sit in the story folder, named by `StoryRecordings.FileNameOf(page)` (`FileStoryLibrary.cs:169-176`). `StarterUpdate.cs:240-248` (`WithMovedRecordings`, `TryAdd`) is a merge pattern to reuse.
- **Merge order (interrupt-safe).** Write every audio file from the backup into the existing story's folder first, each through a temp name and then a move. Only after that, save the story once with the merged `Recordings` map. A kill in between leaves at most extra audio files and no entry that points at a missing file.
  - Pages, pictures, cast, `UseRecordings` and everything else on the phone stay as they are.
  - A backup recording whose page number doesn't exist in the phone's story is dropped.
- **Outcome.** Add a `Merged` restore outcome. Count it in `StoryRestoreResult`, add a `Backup_Merged` string, and show "N books got their recordings back" on the Backup page. A restore that finds nothing new to merge stays `Skipped`.
- **Copy.** There are 16 resx files: the base plus 15 locales (ar, da, de, es, fa, fi, fr, ja, nb, nl, pt, ru, sv, zh). Every new or changed string goes into all 16; the "13 languages" above is stale. `Backup_RestoreBody` (:87, "Stories already here stay exactly as they are") becomes the mock's 124 restore text. The Record line has a phone and a tablet string, picked by `DeviceInfo.Idiom`.
- **Device check.** The microphone can't be driven by a script, so add a Debug-only test hook in a new partial file `Testing/DinoTestCommands.Backup.cs`:
  - It writes a short known audio file as page 1's recording of a starter book, backs up to a file, deletes the recordings and restores, logging each step.
  - One new emulator flow drives it. The flow's `appId` must match whatever `tools/emulator` uses on main when this merges (Slice 221 moves Dino dev installs to `uk.sirous.dinoreader.dbg`).
  - The check is: after the restore the page's recording entry exists and its file is present and non-empty. Hearing it play goes to *Not yet verified*.

#### Why

- **Finding 4:** testers thought their recorded voice was saved for them somewhere. It is not: recordings live in the story's folder on the phone and are never uploaded (`AndroidManifest.xml` microphone comment). The Backup page says "Dino keeps no copy online" (`Backup_*`, `AppResources.resx:80-88`) but never mentions recordings, and the Record page says nothing about where they go.
- **Finding 5 (checked in code, not on a device):** a backup includes recordings (`StoryBackupWriter.cs:59`, `includeRecordings: true`) and restore keeps the story id and the page links (`StoryPackageReader.cs:36,197`). But restore **skips a story whose id is already on the phone**, so restoring onto a phone that still has the story (e.g. recordings deleted, or a fresh re-download from the store) brings no recordings back.
- Open: the user's finding said "on save your voice"; no screen has that name. The copy goes on the three places below until the user names the screen they meant.

#### Decisions locked with the user (2026-11-01, RefineScope)

- **Warm, short copy, inline, no popup**, on the Record page, the intro wizard's "Read it in your voice" step and the Backup page. Roughly: "Your recordings stay on this phone, just for your family. Dino never uploads them or backs them up, so keep a copy yourself: Settings → Backup." All 13 languages.
- **Restore merges recordings** into a story that is already on the phone: add every recorded page the phone lacks; where both have one for the same page, the backup's wins.
- Backup page copy says the file includes your recordings.

**User answers (2026-11-01):** the user accepted the defaults: one restore string everywhere, allowed to wrap in landscape, shown on Record, the wizard step and Backup. iOS gets a compile check in the night run; the VoiceOver / TestFlight / iPhone / iPad hand checks go to this slice's *Not yet verified* for the user to do later.

#### Design (Dino Reader Batch 43 and 44 mocks, frames 106, 116, 124, 129, added 2026-11-01)

The plan says "no design step"; the copy is drawn anyway, in four frames: 106 in `docs/design/dino-reader/Dino Reader Batch 43 Grown-ups Corner.dc.html` (file line 267, Record, portrait), and 116, 124, 129 in `docs/design/dino-reader/Dino Reader Batch 44 Landscape.dc.html` (file lines 201, 274, 350). The mock agrees with the plan.

- **Record page line (106).** Inline under the words, no popup: small info icon, gap 8, 12.5 px, `#5a5470`, padding 0 4: "Your recordings stay on this phone, just for your family. Dino never uploads them, so keep a copy yourself: Settings → Backup." On a tablet "phone" becomes "tablet" by device idiom (129).
- **Same line** goes on the intro's record step and the Backup page; the mock draws it only on Record.
- **Backup page (124, landscape, two cards side by side).** "Back up" / "Saves every book, picture and recording into one file you keep. Dino keeps no copy online." button "Save a backup". "Restore" / "Brings books and recordings back from a file. Books already here keep their pages and get the recordings back." button "Choose a file". The mock calls these drafts for the 13 languages.
- **New strings (both locales, 13 languages).** The Record line (phone and tablet wording), the two Backup card bodies, the buttons if new.

**Open point: shorter landscape wording.** The phone landscape frame (116) shows "Your recordings stay on this phone. Keep a copy: Settings → Backup." **Default:** one string everywhere (the 106 wording), allowed to wrap.

Not drawn: the intro's record step itself.

#### Work

- [ ] Core: restore of an existing story id merges `Recordings` (copy missing audio files, backup wins on the same page), leaves pages, pictures and cast as they are on the phone. Unit tests: fresh restore, merge into a story with none, conflict on one page, a recording whose audio file is missing from the zip (dropped, as today).
- [ ] Round-trip test in Core: write a backup with recordings, read it into an empty library, every recording entry points at an existing file.
- [ ] Copy on Record page, wizard step and Backup page, 13 languages.
- [ ] Android and iOS: device check of back up, delete the recordings, restore, the recorded voice plays (both platforms).

#### Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Scripted emulator flow: record one page, back up, delete the story's recordings, restore, "Read with my voice" plays the page. iOS: same on simulator or TestFlight.
