# Slice 81 — A language change in Settings downloads that language's voice

*In progress (wave 2026-10-22), draft PR.*

**Batch:** 14
**Decisions:** 88
**Depends on:** —
**Touches:** `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml.cs`, `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/**`, `tests/ReadTheStupidText.Tests/VoicePackSetupTests.cs`
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

- [ ] `VoicePackInstaller` subscribes to `ILanguageChangeNotifier.LanguageChanged` and calls
      `StartAsync()`; a run already in flight for the old language finishes, then a re-run
      for the new language starts (no dropped request, same pattern as
      `MobileVoiceModelService.RefreshInBackground`).
- [ ] Unit test (fake pack service + notifier): a language change while idle starts a
      resolve for the new language; a change during a run re-runs once after it.
- [ ] `VoicePickerPage`: when no pack serves the language, show the installer's state
      (Resolving / Downloading n% / failed) and a **Retry** button when `CanRetry`, instead
      of the static "isn't installed yet" line; strings in all nine `.resx` files.
- [ ] Remove the stale "Offering that language's voice pack is Slice 51" remark in
      `LanguageChooser`'s summary.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`
- On a phone over wireless debugging: with the app in English and the English pack installed,
  switch to فارسی in the Language row. The header shows "Downloading natural voices…" without
  an app restart; when it finishes, the voice picker lists the Piper Farsi voice. Switch back
  to English: no download (pack already installed), the Supertonic voices are listed.
- Airplane mode during a switch: the picker shows the offline state and Retry; Retry after
  reconnecting completes the download.
