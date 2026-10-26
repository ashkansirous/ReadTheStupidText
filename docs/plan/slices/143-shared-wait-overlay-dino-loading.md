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

#### Design (Dino Reader Batch 30 mock, frames 64 and 65, added 2026-10-26)

Two frames in `docs/design/dino-reader/Dino Reader Batch 30.dc.html`, plus the `dinoHop`/`dinoShadow` keyframes (in its style block) and the `dino()` function (in its script block). The mock agrees with the plan except for the points below. The book page, the pager dots and the player in 64, and the picture grid in 65, are the real screens behind the overlay.

- **Dino animation (both frames).** A 130x130 box, content bottom-aligned. Inside: a 96x120 column with the app icon (`dino_reader_icons/ios/AppIcon-1024-light.png`, here `icons/ios/AppIcon-1024-light.png`) at 84x84, radius 22, transform origin 50% 100%, and under it a shadow ellipse 64x10 (`#2b2540`, 6px gap). Both loop every 1.1 s with `cubic-bezier(.45,0,.55,1)`.
  - `dinoHop`: 0% and 100% rest; 10% squash `scale(1.08,.9)`; 45% apex `translateY(-34px) scale(.96,1.05)`; 80% landing squash `scale(1.06,.92)`.
  - `dinoShadow`: rest `scaleX(1)` opacity .28; at 45% `scaleX(.6)` opacity .12.
  - With reduce motion on, the dino stands still. TalkBack and VoiceOver read the line.
- **Frame 64, opening a book (first audio not ready).** The overlay covers only the picture (the 3:4 picture box, radius 18), so the back button and title ("The Three Little Pigs", 20px, 600) stay. Fill `rgba(255,246,230,.86)`, centred column, gap 14, padding 20. Line "Getting the voices ready…" (16px, 600, `#2b2540`). Cancel: 44px high, radius 22, padding 0x22, fill `#fff`, 2px `#e3dccd` border, `#5a5470`, 15px, 600. The player row (back 72px, play 104px `#FF7A59`, forward 72px) is dimmed to opacity .4 until audio starts. Cancel goes back to the shelf. A mid-read stall looks the same with no Cancel.
- **Frame 65, own book import (whole-screen wait).** Covers the full page (grown-up "New book" screen, bg `#F3EFE6`) with fill `rgba(243,239,230,.9)`, padding 24. Line "Reading the pictures…" (16px, 600), then a second line "Page 3 of 8" (13px, `#5a5470`, pulled up 8px), then the same Cancel. The second line shows only when the work can count. Cancel stops the import and keeps nothing.
- **Timing.** Shows 500 ms after the wait begins and goes away the moment the first sound plays. A short cached book never shows it.
- **Other lines.** "Translating…", "Saving…" (recording), "Getting the book…" (backup and restore), "Getting the voices ready…". Saving a recording has no Cancel. The store pill, the first-run bar and the language pack bar keep their own progress and do not use this overlay. The copy is a draft.

**Open point: Dino is the app icon, not a drawn dino.** The plan says a dino "drawn with MAUI's own animation API". The mock animates the app-icon image (squash, hop, shadow). **Default:** animate the icon image with MAUI's `TranslateTo`/`ScaleTo` using the keyframe values above; no new art.

**Open point: Cancel on a stall and on a recording save.** The plan says "Cancel where the work can stop" without listing. The mock removes Cancel for a mid-read stall and for saving a recording. **Default:** follow the mock. Backup and restore are not settled by the mock; **Default:** no Cancel (a half-restored library is worse than waiting).

**Open point: Store and pack downloads.** The plan lists "store download, pack downloads" among the waits that use the overlay, and also says the store pill, first-run bar and language pack bar stay. The mock says those three keep their own progress. **Default:** follow the mock: the overlay is not used for them.

**Open point: RTST look vs Dino look.** Dino's overlay differs from RTST's (cream fill, white outlined Cancel, hopping dino instead of a spinner). **Default:** one shared control with a slot for the app's animation, fill and Cancel style, as Decision 149 says.

**New strings (all 14 Dino locales):** "Getting the voices ready…", "Reading the pictures…", "Page N of M", "Translating…", "Saving…", "Getting the book…", Cancel (if not present).

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
