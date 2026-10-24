# Slice 101 — RTST phone: wizard page 2, check the text, translate, read

*Built 2026-10-24 (PR #297), not yet verified on a device.* Planned 2026-10-24, from the user's request: "Wizard page 2 shows the captured text in a text box and
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
  `TranslateEnabled` setting goes away (Slice 99 already removed the switch and stopped main-page translation, so translation is unavailable until this slice); translation is now a
  per-text choice on page 2.
- **Back** from page 2 returns to the main page without changing what's there.

#### Design (RTST Phone Batch 19 mock, screens R6 and R7, added 2026-10-24)

Source: `docs/design/rtst-phone/RTST Phone Batch 19.dc.html`. Two frames at 360x760: another language with translate ticked (R6, lines 152-165) and translating while the model downloads (R7, lines 167-186). The mock agrees with the plan and adds the failed-page banner, the detected-language caption and the download card.

- **Page chrome.** Header padding 8/8/8/4: back arrow 48x48, title "Check the text" 18/600 (flex), **Paste** pill at the right: height 40, padding 0/14, radius 20, fill `rgba(255,255,255,.18)`, white 14/600, clipboard icon (rect 13x15 radius 2) left of the label, gap 6.
- **Text box (R6).** Fills the page: white, radius 16, padding 14/16, **2px `#5B57E8` border** (focused), 18px, line-height 1.6, `#1f2027`, editable, caret 2x20 `#5B57E8`. Sample is French (mock data).
- **Failed-page banner (R6).** Fill `#FFF6E0`, radius 12, padding 10/12, gap 8, 14px `#6b4e00`, line-height 1.4, warning triangle icon: "Page 2 couldn’t be read. Pages 1 and 3 are here."
- **Translate row (R6).** White, 1px `rgba(0,0,0,.11)`, radius 14, min height 56, padding 0/14, gap 12. Ticked box 28x28 radius 8 fill `#5B57E8` with a white check; title "Translate to English" 16.5/600; caption "This text looks like French" 13 `rgba(0,0,0,.58)`.
- **Big button (R6).** Height 62, radius 16, header gradient, white 18/600, play triangle icon left (gap 10), shadow `0 4px 14px rgba(91,87,232,.32)`: "Translate and read". Unticked: "Read".
- **Translating (R7).** Text box loses the border and goes grey (`rgba(0,0,0,.45)` text, 1px border). Translate row at opacity .55, title only. A progress card replaces the button: white, radius 16, padding 14/16, gap 10; "Getting the French translator…" 15/600 with "62%" right-aligned `#4A46D6` tabular; bar 8 high radius 4, track `rgba(0,0,0,.10)`, fill `#5B57E8`; "Only the first time. Translation happens on this phone." 13 `rgba(0,0,0,.58)`.
- **Behaviour (notes).** Reached from Done (R4), straight from Text from a file, from Edit and from long-press Paste. Paste in the header replaces the text with the clipboard. The checkbox shows only when the text's language differs from the app's and the phone can translate (Android; never iOS); ticked by default, no source-language picker. The R7 card shows only when the translator is not on the phone yet. After translating, the text box is replaced with the translation and the main page (R1) shows and reads it. If the download fails, the original is read with one short line saying why. Coming back through **Edit**, page 2 shows the translation with a small **Show original** link under the box.

**Settled by the mock: where "Show original" appears.** Only when page 2 is re-entered through Edit, under the text box; the plan said "on page 2" without saying when.

New strings (nine locales): "Check the text", "Paste", "Page {n} couldn’t be read. Pages {list} are here.", "Translate to {language}", "This text looks like {language}", "Translate and read", "Read", "Getting the {language} translator…", "Only the first time. Translation happens on this phone.", "Show original".

## Work

- [x] 1. **Smallest end-to-end:** `ReviewPage` with the big editor and **Read**, reached from **Text from a
  file**; Read puts the text on the main page and starts reading.
- [x] 2. The checkbox and **Translate and read** (detect, translate, replace, "Show original"), the model
  download line and the failure fallback; `TranslateEnabled` and the translate card code removed.
- [x] 3. Wire Done (Slice 100), Edit and long-press Paste (Slice 99) to page 2; the Paste button on page 2.
- [x] 4. Strings in nine languages; rule in `src/ReadTheStupidText.Mobile/CLAUDE.md` replacing the Slice 54
  translation line (written at planning; check it still matches).

## Verification

- Unit: the pure decisions (show the checkbox: languages differ and a translator exists; the button
  label; "Show original" restores exactly).
- Emulator (device-test), app in Farsi: a French .txt file goes straight to page 2 with "Translate to
  Farsi" ticked. **Translate and read** shows Farsi on the main page and reads it in Farsi. Unticked, it
  reads the French. An English text with the app in English shows no checkbox. Captured pages from
  Slice 100 arrive here.
- iOS compile check; on iOS the checkbox never shows.

## Built 2026-10-24 (PR #297)

*Changed while building:*

- `ReviewPage` (`AppShell.ReviewRoute`) implements mock R6-R7: big editable box, Paste pill, failed-pages banner (singular/plural safe, blank pages not listed). The translate row reads "Translate to <UI language>" and "This text looks like <language>", names in the UI language, ticked by default, shown only when the languages differ and a translator exists (never on iOS).
- The button reads "Translate and read" or "Read". Translating replaces the text; "Show original" restores it and survives a language rebuild via `EditorDraft`. The model-download card shows a sweep, not a percentage, because ML Kit gives no %. On failure: one alert, then the original is read.
- Entry points: Done (capture), Text from a file, Edit, long-press Paste. `ReaderPage` has a re-entrancy guard and pauses reading; `ReviewHandOff` values clear on read.
- Detection always routes the voice; the translator only gates the checkbox. The old translate-on-speak path is gone from the phone. `TranslateEnabled` stays in `ISettingsStore` for Windows.
- Soft input switches to `AdjustResize` on this page and back to `Pan` on leaving.

*From review:* high pass, 8 findings applied; medium pass, 9 findings applied.

#### For CLAUDE.md (lifted 2026-10-24)

- `ReviewPage` hand-off: `ReviewHandOff` values clear on read, and `ReaderPage` guards against re-entry and pauses reading when it opens the page. Don't read the hand-off twice. (Slice 101: docs/plan/slices/101-rtst-mobile-review-and-translate.md)
- Phone translation is a per-text choice on `ReviewPage`, never translate-on-speak. Language detection always routes the voice; the translator only gates the checkbox. `TranslateEnabled` stays in `ISettingsStore` for Windows only. (Slice 101: docs/plan/slices/101-rtst-mobile-review-and-translate.md)
- `ReviewPage` sets soft input to `AdjustResize` and must restore `Pan` on leaving. (Slice 101: docs/plan/slices/101-rtst-mobile-review-and-translate.md)

#### Verified on the emulator (2026-10-24, RTST_Test, app in English)

- A French .txt opens "Check the text" with "Translate to English" ticked and "This text looks like French" shown. The ML Kit model downloaded (download card with sweep), and the main page showed and read the English translation.
- Unticked, the button says Read, and the French is read as-is. An English .txt shows no checkbox.
- Edit opens page 2 and Back changes nothing. Long-press Paste opens page 2 (clipboard replacement checked only weakly). Choose from photos, then Done, lands on page 2.
- With the keyboard open, the button sits just above it.

#### Not yet verified

- Emulator device-test of the page (result to follow from the caller).
- A real ML Kit model download and translation on a phone.
- iOS (compile and that the checkbox never shows).
- A native-speaker read of the new strings in the nine locales.
