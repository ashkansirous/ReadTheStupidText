# Slice 143 — One shared wait overlay; Dino shows it on every wait

*Planned.*

**Batch:** 30 (Dino loading, Farsi voice, kid-friendly language, Decisions 149-153)
**Touches:** `src/ReadTheStupidText.Mobile.Shared/Waiting/**` (new), `src/ReadTheStupidText.Application/Reading/WaitStage.cs` (new, shared with Slice 138), `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/StoryWizardPage*`, `src/DinoReader.App/PageEditorPage*`, `src/DinoReader.App/ScanPages.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/RecordPage.cs`, `src/DinoReader.App/BackupPage*`, `src/DinoReader.App/StorePage*`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/**`, `tests/ReadTheStupidText.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

Dino has no general loading screen. Opening a book sets `SessionState.Loading` (`ReadingPage.xaml.cs`)
but nothing on screen says so; picture/PDF import, scans, own-book translation and saving a recording
show nothing either. Store, backup and pack downloads each have their own bar. The user: "if the user
waits for more than 500 ms they should see a loading screen" (Decision 146, now for Dino too).

## Decisions (locked with the user, 2026-10-26)

- **Decision 149: one shared wait overlay for both MAUI apps** in `Mobile.Shared/Waiting/`, driven by
  the pure `WaitStage` timer model (show after 500 ms, hide on first audio/finish/cancel, Cancel where
  the work can stop). RTST's Slice 138 uses the same component (whichever slice lands first builds it).
- **Dino's look:** a small bouncing dino drawn with MAUI's own animation API (no Lottie, no new
  library), plus one short line for grown-ups ("Getting the voices ready…", "Reading the pictures…",
  "Translating…", "Saving…"). Respects the OS reduce-motion setting (static dino).
- **Every wait** goes through it: book open / first audio, a mid-read stall, picture/PDF import,
  scan/OCR, own-book translation, recording save, backup/restore, store download, pack downloads.
  The existing in-place progress bars (store pill, first-run bar, language pack bar) stay; the overlay
  covers waits that block the screen.

## Work

- [ ] `WaitStage` + timer model (Application), unit-tested; shared overlay control in Mobile.Shared
      with a slot for an app's own animation.
- [ ] Dino bouncing-dino animation and stage strings in all 14 locales.
- [ ] Wire each wait listed above; Cancel stops book loading (back to the gallery) and imports.
- [ ] Usage: `wait_shown` (stage, duration bucket) as a Dino event too (Slice 134 rules: `EVENT_RULES`,
      `UsageDetail`, privacy texts).

## Verification

- Unit tests of the timer model.
- Emulator: open a long book with a fresh voice (the dino shows, then goes when audio starts), a short
  cached book (nothing flashes), import a PDF, translate an own book; TalkBack reads the stage.
