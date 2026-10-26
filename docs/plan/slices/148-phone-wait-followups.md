# Slice 148 — Phone apps: mid-read stalls, wait counts and the leftovers of Slices 137, 138 and 143

*In progress (wave 2026-10-26), draft PR.*

**Batch:** 30 (Decisions 146, 149; no new decision)
**Touches:** `src/ReadTheStupidText.Application/Reading/ISpeechReader.cs`, `src/ReadTheStupidText.Application/Reading/WaitStage.cs`, `src/ReadTheStupidText.Application/Usage/**`, `src/ReadTheStupidText.Infrastructure/Speech/**`, `src/ReadTheStupidText.Mobile.Shared/Speech/**`, `src/ReadTheStupidText.Mobile.Shared/Usage/**`, `src/ReadTheStupidText.Mobile.Shared/Waiting/**`, `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/UsageCounter.cs`, `src/DinoReader.Core/UsageEventName.cs`, `worker/events.ts`, `worker/*.test.mjs`, `tools/dino-usage-report.ps1`, `tools/rtst-usage-report.ps1`, `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`

## Why

Slices 137, 138 and 143 each left work out because another slice in the same wave owned the files.
This slice picks it all up in one place, since the pieces share `ISpeechReader`, the usage pipe and the
overlay.

## Work

- [ ] **Mid-read stall (both phone apps).** `ISpeechReader` gains a signal for "the next chunk isn't
      ready yet" (e.g. a `Buffering` state or event), raised by every reader (Supertonic, Kokoro, Piper,
      the phone voice). Dino's `ReadingSession` and RTST's `ReaderPage` show the shared overlay through
      `WaitController` with the `Stalled` stage after 500 ms ("Still getting the voice ready…"), and hide
      it on the next sound. Cancel stops the read and stays on the page.
- [ ] **Wait counts.** `wait_shown` and `wait_cancelled` (closed fields: app, stage, duration bucket) for
      Dino and RTST phone, through `UsageQueueSender`; the Worker's closed event lists, the two report
      scripts and the privacy texts gain them. Nothing is counted while telemetry is off.
- [ ] **RTST phone: no config fetch while the switch is off.** `RtstUsage.StartAsync`
      (`Mobile.Shared/Usage/RtstUsage.cs`) fetches `/api/app-config` at every launch; skip it while
      "Help improve the app" is off, as Windows does since Slice 140.
- [ ] **Huge text files.** A 36 MB text file crashes `ReviewPage` with `OutOfMemoryError` in
      `EditText.setText` (found on the emulator in Slice 138). Cap what goes into the editor (read the
      first N characters, say so on the page) so a big file never crashes the app.
- [ ] **PDF import Cancel inside a stage (Dino).** The extractors take no `CancellationToken`, so Cancel
      waits for the current stage. Pass a token through the extractor interfaces if it stays small;
      otherwise leave it and say so here.

## Acceptance

- On the emulator, a read that stalls mid-book (throttle the CPU or use a long first chunk) shows the
  overlay after 500 ms and hides it on the next sound, in both apps.
- The Worker tests cover the two new events; the phone makes no network call at launch with the switch off.
- A 36 MB text file opens without a crash.
