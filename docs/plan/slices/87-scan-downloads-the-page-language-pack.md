# Slice 87 — A scan in a script with no reader asks the page's language and downloads it

*Planned.*

**Batch:** 14 (follow-up to Slice 84)
**Depends on:** Slice 84 (`OcrPackChoice`, `OcrSource.MlKitPackPending`), Slice 83 (Scan replaces the text)
**Touches:** `src/ReadTheStupidText.Mobile/OcrPackInstaller.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile.Shared/Ocr/LanguageImageTextExtractor.cs`, `src/ReadTheStupidText.Application/Images/Ocr/PageLanguagePrompt.cs` (new), `src/ReadTheStupidText.Mobile/Resources/Strings/**`, `tests/ReadTheStupidText.Tests/PageLanguagePromptTests.cs` (new), `src/ReadTheStupidText.Mobile/CLAUDE.md`
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

- [ ] `LanguageImageTextExtractor` / `ImageText`: make sure a scan's result carries
      `OcrSource.MlKitPackPending` (and the page image path) up to `ReaderPage`.
- [ ] Pure `PageLanguagePrompt` in Application: given the scan result and the installed and catalog
      OCR packs, decide whether to ask, and which languages to offer (packs that read a non-Latin
      script, minus the installed ones). Tests.
- [ ] `OcrPackInstaller.EnsureForLanguageAsync(language, IProgress<double>, ct)`: the public
      per-language install, reusing the private `InstallAsync` with progress passed through to
      `IVoicePackService.DownloadAsync`.
- [ ] `ReaderPage` scan flow: on a pending result, show the language choice (reuse
      `LanguageChoices` / the `LanguageChooser` action-sheet style, LTR per Slice 85), then a
      "Downloading the reader for <language>… n%" status with Cancel, then re-run OCR on the same
      page image(s) and replace the text (Slice 83). Offline or failed: a message with Retry.
- [ ] Strings in all 9 locales; the `Mobile/CLAUDE.md` rule.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests` (`PageLanguagePromptTests`).
- Device, app in English: scan a Farsi page → asked for the language → pick فارسی → download
  progress shown → the page reads as Farsi. Scan the next Farsi page → no prompt.
- Device: cancel at the prompt → the old text stays; airplane mode during the download → message
  with Retry.
