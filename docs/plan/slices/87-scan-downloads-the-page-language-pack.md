# Slice 87 — A scan in a script with no reader asks the page's language and downloads it

*Built 2026-10-22 (wave 2026-10-22), PR #253. Not yet verified on a device.*

**Batch:** 14 (follow-up to Slice 84)
**Depends on:** Slice 84 (`OcrPackChoice`, `OcrSource.MlKitPackPending`), Slice 83 (Scan replaces the text)
**Touches:** `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile.Shared/Ocr/LanguageImageTextExtractor.cs`, `src/ReadTheStupidText.Application/Images/Ocr/PageLanguagePrompt.cs` (new), `src/ReadTheStupidText.Mobile/Resources/Strings/**`, `tests/ReadTheStupidText.Tests/PageLanguagePromptTests.cs` (new), `src/ReadTheStupidText.Mobile/CLAUDE.md`, `src/ReadTheStupidText.Mobile/ScanPackFlow.cs` (new), `src/ReadTheStupidText.Application/Images/Ocr/PagePackRetry.cs` (new), `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/AppResources.cs`, `tests/ReadTheStupidText.Tests/` (ScriptDetector table test)
**Story issue:** —

## Goal

Someone using the app in English scans a Farsi page. Today they get junk Latin text or nothing,
because only the app language's OCR pack is ever downloaded and nothing reads the "pack pending"
signal. After this slice the app notices the page isn't in a script it can read yet, asks which
language the page is in, downloads that language's OCR pack with visible progress, and reads the
page with it.

## Why

Slice 84 made OCR follow the page's script, but only among packs already installed. The user
(2026-10-22): "if they want to read it in farsi, then yes, we'll need to download the farsi pack and
need to show that we are downloading the package."

## Decisions locked with the user (2026-10-22)

- A page in a script with no installed reader leads to downloading that language's pack, with the
  download shown to the user.
- **Coordinator's call (based on "if they want to read it in Farsi"):** the user is asked which
  language the page is in, because the built-in recognizer can't tell Farsi from Arabic or Urdu
  before a pack exists. The choice lists the OCR-pack languages from the catalog, in their native
  names. Cancel keeps the existing text, as a cancelled scan does (Slice 83).
- App-language pack downloads (`EnsureForCurrentLanguageAsync`) stay as they are.

## Steps

- [x] `LanguageImageTextExtractor` / `ImageText`: make sure a scan's result carries
      `OcrSource.MlKitPackPending` (and the page image path) up to `ReaderPage`.
- [x] Pure `PageLanguagePrompt` in Application: given the scan result and the installed and catalog
      OCR packs, decide whether to ask, and which languages to offer (packs that read a non-Latin
      script, minus the installed ones). Tests.
- [x] `OcrPackInstaller.EnsureForLanguageAsync(language, IProgress<double>, ct)`: the public
      per-language install, reusing the private `InstallAsync` with progress passed through to
      `IVoicePackService.DownloadAsync`.
- [x] `ReaderPage` scan flow: on a pending result, show the language choice (reuse
      `LanguageChoices` / the `LanguageChooser` action-sheet style, LTR per Slice 85), then a
      "Downloading the reader for <language>… n%" status with Cancel, then re-run OCR on the same
      page image(s) and replace the text (Slice 83). Offline or failed: a message with Retry.
- [x] Strings in all 9 locales; the `Mobile/CLAUDE.md` rule.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests` (`PageLanguagePromptTests`).
- Device, app in English: scan a Farsi page → asked for the language → pick فارسی → download
  progress shown → the page reads as Farsi. Scan the next Farsi page → no prompt.
- Device: cancel at the prompt → the old text stays; airplane mode during the download → message
  with Retry.

## Built

*Changed while building:* the scan flow lives in a new `ScanPackFlow` (Mobile) behind `IScanPackUi`, so `ReaderPage` only supplies the UI. Pure `PageLanguagePrompt` and `PagePackRetry` (Application, tested) decide what to ask and whether to retry. A pending page asks the page's language (non-Latin catalog OCR languages without an installed pack, the matching script first), downloads it with progress and a Cancel button (one token covers the catalog fetch and the download), then re-reads only the pending pages. Offline or no catalog keeps the text and says the page needs a download, with Retry (max 3); a missing pack is not retryable; a page still unreadable keeps the text and says so. `OcrPackInstaller.EnsureForLanguageAsync` shares one in-flight download per pack (join and take-over, progress replay). 5 new strings in 9 locales.

*From review* (`reviews/87-review-1.md`, `87-review-2.md`): all findings fixed.

*Also fixed:* `VoicePackInstaller.OnLanguageChanged` no longer queues a second `StartAsync` while a run is in flight (an extra-run bug from Slice 81, found as the flaky test `A_language_change_during_a_run_re_runs_once_after_it`).

*Checks:* RTST 819 pass / 2 skipped, 12 consecutive clean runs; Mobile and Dino Android builds.

#### For CLAUDE.md

In `src/ReadTheStupidText.Mobile/CLAUDE.md`: A scan whose page the built-in recognizer can't read (an `OcrSource.MlKitPackPending` result) runs `ScanPackFlow` (the pure decisions are `PageLanguagePrompt` and `PagePackRetry` in Application): it asks which language the page is in (the page's detected script first), downloads that pack via `OcrPackInstaller.EnsureForLanguageAsync` (joins an in-flight download of the same pack) with progress and a Cancel button, then re-reads only the pending pages. Offline, cancel, a capped Retry, or a page still pending afterwards keep the editor text. (Slice 87: docs/plan/slices/87-scan-downloads-the-page-language-pack.md)

#### Not yet verified

- On a Google-signed-in device with the app in English: scan a Farsi page, get asked, pick Farsi, see progress, and the page reads as Farsi.
- A second Farsi page has no prompt.
- Cancel at the prompt keeps the text.
- Airplane mode shows the message with Retry.
- Native-speaker check of the 5 new strings.
