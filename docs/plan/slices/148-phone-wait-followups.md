# Slice 148 — Phone apps: mid-read stalls, wait counts and the leftovers of Slices 137, 138 and 143

*Built 2026-10-26 (wave PR #416), not yet verified on a device.*

**Batch:** 30 (Decisions 146, 149; no new decision)
**Touches:** `src/ReadTheStupidText.Application/Reading/ISpeechReader.cs`, `src/ReadTheStupidText.Application/Reading/StallReporter.cs`, `src/ReadTheStupidText.Application/Reading/EditorTextLimit.cs`, `src/ReadTheStupidText.Application/Usage/**`, `src/ReadTheStupidText.Infrastructure/Reading/SupertonicSpeechReader.cs`, `src/ReadTheStupidText.Infrastructure/Reading/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/CompositeSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/*/*SupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Windows/WindowsNeuralReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Usage/**`, `src/ReadTheStupidText.Mobile.Shared/Waiting/WaitController.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/**`, `src/ReadTheStupidText.App/WaitFlyoutController.cs`, `src/ReadTheStupidText.App/Strings/*/Resources.resw`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/UsageCounter.cs`, `src/DinoReader.App/Resources/Strings/**`, `src/DinoReader.Core/UsageEvent.cs`, `worker/events.ts`, `worker/events.test.mjs`, `tools/dino-usage-report.ps1`, `tools/rtst-usage-report.ps1`, `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`

## Why

Slices 137, 138 and 143 each left work out because another slice in the same wave owned the files.
This slice picks it all up in one place, since the pieces share `ISpeechReader`, the usage pipe and the
overlay.

## Work

- [x] **Mid-read stall (both phone apps, and Windows).** `ISpeechReader` gains a signal for "the next chunk isn't
      ready yet" (e.g. a `Buffering` state or event), raised by every reader (Supertonic, Kokoro, Piper,
      the phone voice). Dino's `ReadingSession` and RTST's `ReaderPage` show the shared overlay through
      `WaitController` with the `Stalled` stage after 500 ms ("Still getting the voice ready…"), and hide
      it on the next sound. Cancel stops the read and stays on the page.
- [x] **Wait counts (phones and Windows).** `wait_shown` and `wait_cancelled` (closed fields: app, stage, duration bucket) for
      Dino and RTST phone, through `UsageQueueSender`; the Worker's closed event lists, the two report
      scripts and the privacy texts gain them. Nothing is counted while telemetry is off.
- [x] **RTST phone: no config fetch while the switch is off.** `RtstUsage.StartAsync`
      (`Mobile.Shared/Usage/RtstUsage.cs`) fetches `/api/app-config` at every launch; skip it while
      "Help improve the app" is off, as Windows does since Slice 140.
- [x] **Huge text files.** A 36 MB text file crashes `ReviewPage` with `OutOfMemoryError` in
      `EditText.setText` (found on the emulator in Slice 138). Cap what goes into the editor (read the
      first N characters, say so on the page) so a big file never crashes the app.
- [ ] **PDF import Cancel inside a stage (Dino): left undone.** The extractors take no `CancellationToken`, so Cancel
      waits for the current stage. Pass a token through the extractor interfaces if it stays small;
      otherwise leave it and say so here.

## Acceptance

- On the emulator, a read that stalls mid-book (throttle the CPU or use a long first chunk) shows the
  overlay after 500 ms and hides it on the next sound, in both apps.
- The Worker tests cover the two new events; the phone makes no network call at launch with the switch off.
- A 36 MB text file opens without a crash.

## Built notes

- Stall: `ISpeechReader.StallChanged` (default no-op event) is raised by `StallReporter.WatchAsync` around the wait for each chunk's audio in the Windows, Android and iOS Supertonic readers (Kokoro, Piper and Melo run inside them); both composites forward it. The phone's own voice never raises it (the OS buffers inside). RTST `ReaderPage.Wait.cs` and Dino `ReadingSession.Stalled` / `ReadingPage.ApplyWait` show the `Stalled` stage ("Still getting the voice ready...", `Wait_Stalled` in all three apps, drafts for 13 languages); the preparing wait keeps the overlay when it owns it. Cancel stops the read and stays on the page.
- Windows: `WaitFlyoutController` listens to the reader's `StallChanged` itself, so `ReadAloudService*`, `ControlPanelWindow*` and `Application/Reading/EarlyStart*` (Slice 142) are untouched. Its Cancel, when stalled, also stops the reader.
- Slice 149's rescue (a sibling model loads inside one chunk) is covered by the same signal: the chunk's audio is late, so the overlay shows "Still getting the voice ready" (the first chunk of a read is covered by the preparing wait).
- Counts: `WaitController.Finished` (static, raised only for a wait whose overlay showed) feeds `RtstUsage.CountWaits` and Dino's `UsageCounter`; `WaitFlyoutController` counts Windows through `IUsageRecorder`. RTST events `wait_shown` / `wait_cancelled` carry the new `waitStage` field (blob 17) and `durationBucket`; Dino's carry about-no-book `detail` = `stage:bucket`. Worker, both report scripts and the three privacy texts have them; telemetry stays off in the remote config.
- `RtstUsage.StartAsync` returns before any fetch while the switch is off; turning the switch on fetches the config and flushes.
- Huge text: `EditorTextLimit.Cap` (300,000 characters) is applied to a review request and to Paste; the page says so in its note (`Review_TextCut`). Not run against the 36 MB file on the emulator.
- PDF Cancel inside a stage: not done. A token would have to go through `IPositionedTextExtractor`, `IPdfPageRenderer` (three platform renderers), `RenderedPdfTextExtractor`, `PdfPositionedTextExtractor`, `StoryImport.CopyIntoAsync` and `PagesStep`, which is not small and overlaps Slice 144's files.
- Not verified on a device: the emulator acceptance items (overlay on a stalled read in both apps, the 36 MB file) and the Windows packaged run.

From review: s148-high (7 applied), s148-medium (7 applied, finding 8 not a bug).

#### Design (Dino Reader Batch 31 mock, screen 71, added 2026-10-26)

Source: `docs/design/dino-reader/Dino Reader Batch 31.dc.html`, screen 71 (lines 156-183). One frame at 360x760, real UI. The mock agrees with the built slice (Cancel stops the read and stays on the page) and corrects the note on Batch 30 frame 65, which said a mid-read stall has "no Cancel".

- **Frame.** The reading page (Picture + text, page 3 of 6) with the Batch 30 overlay laid over the picture only, so the title and back button stay put. Overlay fill `rgba(255,246,230,.86)`, radius 18, padding 20, content centred, gap 14. The hopping dino in a 130x130 box (96x120 dino, 84 px icon, radius 22, `dinoHop` 1.1 s). Line "Still getting the voice ready…" 16/600. **Cancel** pill: height 44, padding 0/22, radius 22, fill `#fff`, 2 px border `#e3dccd`, text 15/600 `#5a5470`. The play controls are dimmed to opacity .4.
- **Timing and behaviour.** Shows 500 ms after the next part fails to arrive, hides on the next sound. It also covers a Farsi rescue while the second voice loads (Slice 149). **Cancel stops the read and stays on the page.** Read The Stupid Text shows the same line in its own overlay (R8, Batch 29).
- **Strings.** "Still getting the voice ready…" is the existing `Wait_Stalled`; "Cancel" exists. No new strings.

**Open point: Batch 30's "no Cancel" note.** `docs/design/dino-reader/Dino Reader Batch 30.dc.html` (note under screen 65, line 49) says a mid-read stall looks the same "but with no Cancel". The slice text and the built code (Built notes) give the stall a Cancel that stops the read. **Default:** the slice and the Batch 31 mock win; treat the Batch 30 sentence as superseded. No code change.

#### For CLAUDE.md (lifted 2026-10-26)

- Readers raise `ISpeechReader.StallChanged` through `StallReporter` (per-watch tickets, overlapping watches counted); wait usage is counted only through `WaitController.Finished`. (`src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`; Slice 148: docs/plan/slices/148-phone-wait-followups.md)

#### Not yet verified

- Emulator (AVD RTST_Test, 2026-10-26, after the fixes): Dino "Getting the voices ready…" hides on first audio and on Cancel, the "Still getting the voice ready…" stall overlay shows and hides; RTST stall overlay, 36 MB file (cut note, no crash), 300k-char read for 90 s with flat memory (~750 MB RSS, no kill), no config request while the switch is off; no unhandled exception. Timings not representative (arm64 under translation).
- Windows stall overlay on a packaged run.
- Native read of the `Wait_Stalled` and `Review_TextCut` strings.
- A 300k-character repeated-text read on a 2 GB AVD was killed by lmkd after racing about 1,290 cached chunks: Android started every remaining chunk at once. Now at most 8 are in flight (`GenerationWindow`); re-run that read to confirm. Finished clips still land on disk ahead of playback (not bounded by playback), and the iOS and Windows neural readers were not checked.
