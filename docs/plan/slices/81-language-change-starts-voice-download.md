# Slice 81 — A language change in Settings downloads that language's voice

*Built 2026-10-22 (wave 2026-10-22), PR #239. Verified on the RTST_Test emulator, not yet on a real phone.*

**Batch:** 14
**Decisions:** 88
**Depends on:** —
**Touches:** `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml.cs`, `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/**`, `tests/ReadTheStupidText.Tests/VoicePackSetupTests.cs`, `src/DinoReader.App/SpokenLanguage.cs`
**Story issue:** —

## Goal

Picking a new language from the Reader's **Language** row starts that language's natural
voice download right away, with the usual progress in the header. The voice picker never
leaves the user with nothing to do: with no pack yet it shows the download state and a
Retry button when one failed.

## Why

Seen on a Pixel 10 Pro, 1.16.0, 2026-10-22: switching Farsi → English left the voice picker on
"Phone voice" only, with "The natural voice for your language isn't installed yet" and no
download. `LanguageChooser.Apply` stores the language and rebuilds the shell but never calls
`VoicePackInstaller.StartAsync()`; only `App.InitializeSpeechAsync` (launch) and the first-run
`LanguagePage` do. Restarting the app started the download at once, which confirms it.

## Decisions locked with the user (2026-10-22)

- A language change behaves like first run: the pack for the new language downloads
  without asking (the phone voice reads meanwhile, as today).
- `OcrPackInstaller` already listens to `LanguageChanged`; the voice installer follows the
  same event, so any future place that changes the language gets the download too.

## Steps

- [x] `VoicePackInstaller` subscribes to `ILanguageChangeNotifier.LanguageChanged` and calls
      `StartAsync()`; a run already in flight for the old language finishes, then a re-run
      for the new language starts (no dropped request, same pattern as
      `MobileVoiceModelService.RefreshInBackground`).
- [x] Unit test (fake pack service + notifier): a language change while idle starts a
      resolve for the new language; a change during a run re-runs once after it.
- [x] `VoicePickerPage`: when no pack serves the language, show the installer's state
      (Resolving / Downloading n% / failed) and a **Retry** button when `CanRetry`, instead
      of the static "isn't installed yet" line; strings in all nine `.resx` files.
- [x] Remove the stale "Offering that language's voice pack is Slice 51" remark in
      `LanguageChooser`'s summary.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`
- On a phone over wireless debugging: with the app in English and the English pack installed,
  switch to فارسی in the Language row. The header shows "Downloading natural voices…" without
  an app restart; when it finishes, the voice picker lists the Piper Farsi voice. Switch back
  to English: no download (pack already installed), the Supertonic voices are listed.
- Airplane mode during a switch: the picker shows the offline state and Retry; Retry after
  reconnecting completes the download.

*Changed while building:* `VoicePackInstaller` is single-flight with a run generation (stale publishes and progress ticks are dropped) and cancels the in-flight run when the language changes, instead of finishing it and re-running. Publish happens outside the lock, a failure publishes Failed, a null language publishes Idle, and waiters are released in a `finally`. The voice picker shows localised status with Retry on Idle and failure; a progress tick updates only the status label. Tests use event waits with a 30 s cap (no polling). `src/DinoReader.App/SpokenLanguage.cs` got comment-only edits.

*From review:* `D:\ReadTheStupidText-worktrees\reviews\81-review-{1,2}.md`: all findings fixed in both passes.

*Checks:* RTST tests 693 pass / 2 skipped; Mobile and Dino Android builds. Emulator (RTST_Test): en to fa lists Piper Amir without a restart; back to en is instant; a change to zh downloads unprompted with progress, then Ready; zh to es to zh mid-download resumes and ends Ready; airplane mode shows offline plus Retry, and Retry works; app launches fine: PASS.

#### For CLAUDE.md

In `src/ReadTheStupidText.Mobile/CLAUDE.md`: Debug builds installed with `adb` (emulator) need `-p:EmbedAssembliesIntoApk=true -p:RuntimeIdentifier=android-arm64` (plus `-p:IncludeWindows=false`). A plain Debug build uses Fast Deployment and dies on launch with "No assemblies found" after `adb install`. (Slice 81: docs/plan/slices/81-language-change-starts-voice-download.md)

#### Not yet verified

- The header "tap to retry" label: no defect found in code, but the state flashes Resolving then Offline.
- A reinstalled pack restores the previously chosen voice (the code says yes).
- Everything above on a real phone.
