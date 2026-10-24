# Slice 101 — RTST phone: wizard page 2, check the text, translate, read

*Planned 2026-10-24*, from the user's request: "Wizard page 2 shows the captured text in a text box and
lets people edit it. The text box should be big so they see as much as possible, plus a checkbox, if it
is in a different language, to translate to the app's language, with a button to translate/read. If they
upload a file instead of an image, it should go directly to wizard page 2."

**Batch:** 19 (RTST phone overhaul for first-time users)
**Depends on:** Slice 99 (main page, Edit, Paste), Slice 100 (Done), Slice 54 (translation), Slice 95 (`MlKitTranslator` in Mobile.Shared)
**Touches:** `src/ReadTheStupidText.Mobile/ReviewPage.xaml` (new), `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs` (new), `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Translation.cs`, `src/ReadTheStupidText.Mobile/CapturePage.xaml.cs`, `src/ReadTheStupidText.Mobile/SettingsPage.xaml`, `src/ReadTheStupidText.Mobile/SettingsPage.xaml.cs`, `src/ReadTheStupidText.Mobile/AppShell.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Application/Translation/**`, `src/ReadTheStupidText.Mobile/Resources/Strings/*.resx`, `src/ReadTheStupidText.Mobile/CLAUDE.md`, `tests/ReadTheStupidText.Tests/**`

## Why

There's no step today where a user sees the scanned or imported text before it's read. Translation is a
card with a toggle and a source-language picker that speaks the translation while the screen keeps the
original. Both confused first-time users.

## Decisions (2026-10-24, agreed with the user)

- **Decision 105: one review page before reading, and translation shows what it reads.**
- **Page 2 is almost all text box.** The full text, editable, fills the screen above the keyboard line.
  It's reached from Done (Slice 100), **Text from a file** (straight here after picking a txt, pdf or docx,
  with no page 1), Edit and long-press Paste (Slice 99). A small **Paste** button on page 2 replaces the
  text with the clipboard.
- **The translate checkbox** shows only when the detected language of the text differs from the app's
  language and a translator exists on this device (Android today; on iOS it never shows). It reads
  **"Translate to <app language>"** and is ticked by default. Detection is the existing `ILanguageDetector`
  with its debounce; there's no source-language picker on the page.
- **One big button:** **"Translate and read"** when ticked, **"Read"** when not. Translating replaces the
  text with the translation (a small "Show original" link on page 2 restores it), then returns to the main
  page, which shows and reads the translated text with the highlight. So what is on screen is always what
  is heard. Translation stays fully on-device; a missing ML Kit model is downloaded with a progress line,
  and if that fails the original is read with a short message.
- **Reverses Slice 54's "the translated text is read; the editor keeps the original"** for the phone. The
  `TranslateEnabled` setting and the Settings toggle added by Slice 99 go away; translation is now a
  per-text choice on page 2.
- **Back** from page 2 returns to the main page without changing what's there.

## Work

- [ ] 1. **Smallest end-to-end:** `ReviewPage` with the big editor and **Read**, reached from **Text from a
  file**; Read puts the text on the main page and starts reading.
- [ ] 2. The checkbox and **Translate and read** (detect, translate, replace, "Show original"), the model
  download line and the failure fallback; `TranslateEnabled` and the translate card code removed.
- [ ] 3. Wire Done (Slice 100), Edit and long-press Paste (Slice 99) to page 2; the Paste button on page 2.
- [ ] 4. Strings in nine languages; rule in `src/ReadTheStupidText.Mobile/CLAUDE.md` replacing the Slice 54
  translation line (written at planning; check it still matches).

## Verification

- Unit: the pure decisions (show the checkbox: languages differ and a translator exists; the button
  label; "Show original" restores exactly).
- Emulator (device-test), app in Farsi: a French .txt file goes straight to page 2 with "Translate to
  Farsi" ticked. **Translate and read** shows Farsi on the main page and reads it in Farsi. Unticked, it
  reads the French. An English text with the app in English shows no checkbox. Captured pages from
  Slice 100 arrive here.
- iOS compile check; on iOS the checkbox never shows.
