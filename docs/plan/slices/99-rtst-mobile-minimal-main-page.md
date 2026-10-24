# Slice 99 — RTST phone: a minimal main page and a Settings page

*Built 2026-10-24 (PR #279), emulator-checked on RTST_Test the same day.* Planned 2026-10-24, from the user's report: "I gave Read The Stupid Text to my dad and my wife's dad,
and they did not know what to do with it. The page is very crowded, and they didn't even see the add
more images button. The main page should keep the minimal requirements: the text being read, two
buttons that start a wizard for adding a new text, and a settings button for the language and the
voice."

**Batch:** 19 (RTST phone overhaul for first-time users)
**Depends on:** Slices 45 (scan), 53 (localised UI), 54 (translation), 85 (layout never mirrors)
**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Translation.cs`, `src/ReadTheStupidText.Mobile/SettingsPage.xaml` (new), `src/ReadTheStupidText.Mobile/SettingsPage.xaml.cs` (new), `src/ReadTheStupidText.Mobile/AppShell.xaml`, `src/ReadTheStupidText.Mobile/AppShell.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile/EditorDraft.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/*.resx`, `src/ReadTheStupidText.Mobile/CLAUDE.md`, `src/ReadTheStupidText.Mobile/Platforms/**`, `src/ReadTheStupidText.Mobile/LongPressBehavior.cs`, `src/ReadTheStupidText.Mobile/TranslateSourceMigration.cs`, `src/ReadTheStupidText.Mobile/Resources/Images/header_glyph.png`, `src/ReadTheStupidText.Mobile/AppResources.cs`

## Why

`ReaderPage` holds everything on one screen: the Paste/Scan/File row, the editor, the translate card
with its source picker, the voice row, the language row, the transport and six speed presets. First-time
users (two grandparents) couldn't find where to start. Scanning more than one page happens inside
Google's scanner UI, where they never saw its "add page" button.

## Decisions (2026-10-24, agreed with the user)

- **Decision 103: the RTST phone main page shows only reading.** Top to bottom:
  - a small header with a **Settings** (gear) button
  - a large read-only text area showing what is being read, with the current sentence highlighted, and a
    small **Edit** link under it
  - two big buttons, **Text from pictures** and **Text from a file**
  - a simple player: one large Play/Pause, back and forward, and the progress bar

  The speed presets, voice row, language row, translate card and Paste button leave the main page. The
  freed space goes to the text area. Settings has no translate toggle at
  any point (changed to match the mock, 2026-10-24).
- **Settings is one page** (pushed route `settings`): the app language (today's `LanguageChooser`), the
  voice (opens today's `VoicePickerPage`) and the reading speed (the same presets). Speed persists as it
  does today.
- **Read-only main text.** Touching the text doesn't open the keyboard. Long-press offers Paste, which
  opens wizard page 2 (Slice 101) with the pasted text. **Edit** opens page 2 with the current text.
  Until Slice 101 lands, Edit and Paste use today's in-place editor so the slice ships on its own.
- **An empty start.** With no text, the text area shows one friendly line ("Tap a button below to add
  something to read") and Play is disabled.
- **Scope:** Android and iOS. The Windows tray app is unchanged. The layout is built in the current
  style. The layout never mirrors (Decision 92).

#### Design (RTST Phone Batch 19 mock, screens R1, R2, R3, added 2026-10-24)

Source: `docs/design/rtst-phone/RTST Phone Batch 19.dc.html`. Three frames at 360x760: main page reading (R1, lines 25-50), main page empty (R2, lines 52-74), Settings (R3, lines 76-100). Real UI throughout (the status bar "9:41 / 100%" is a device frame). The plan was changed to match the mock on the translate toggle (see the end of this subsection). Type is larger than Batch 9.

- **Page chrome (all).** Page `#f4f5f9`; status bar 30 high `#5B57E8`. Header: `linear-gradient(135deg,#5B57E8,#3B82F6)`, white, padding 8/8/8/18, title "Read The Stupid Text" 17/600, the app glyph `design_handoff_tray_panel/assets/glyph.png` as a watermark (100x100, right 40, top -18, white, opacity .12, rotated -8deg). Gear button 48x48 (gear outline with circle r 3, white).
- **Reading area (R1).** Body padding 14/14/12, gap 12. Text card: white, 1px `rgba(0,0,0,.11)`, radius 16, padding 16, 19px, line-height 1.6, `#1f2027`, read-only. Read text `rgba(0,0,0,.45)`, spoken sentence fill `rgba(91,87,232,.16)`, radius 6, halo 3px of the same colour, then the unread text in `#1f2027`. Under it, right-aligned, **Edit** (pencil icon, 15/600, `#4A46D6`, height 40).
- **Two big buttons (R1).** 2 columns, gap 10, each 92 high, white, 1px border, radius 16, icon over label, gap 8, 15.5/600, centred, line-height 1.2: camera icon with "Text from pictures", file-page icon with "Text from a file".
- **Player (R1).** White card, radius 16, padding 14/16, gap 12. Progress bar 6 high radius 3, track `rgba(0,0,0,.10)`, fill `#5B57E8`; times "0:09" and "0:24" 12.5 tabular, `rgba(0,0,0,.58)`. Controls centred, gap 28: previous (double chevron left) 52 circle fill `rgba(91,87,232,.10)` icon `#4A46D6`; pause 72 circle in the header gradient, shadow `0 4px 14px rgba(91,87,232,.38)`, white bars; next 52 circle (double chevron right).
- **Empty state (R2).** Text card has a 1px dashed `rgba(0,0,0,.18)` border, a down arrow icon and "Tap a button below to add something to read" 19/1.45 `rgba(0,0,0,.6)`, max width 240, centred. The two big buttons get a 2px `#5B57E8` border in this state only. Player at opacity .5: times "0:00" and "--:--", prev/next fill `rgba(0,0,0,.06)` icon `rgba(0,0,0,.4)`, play 72 circle `#c9cbd6` with a white triangle.
- **Settings (R3).** Header padding 8/18/8/4: back arrow 48x48, title "Settings" 18/600. Body padding 16/14, gap 10. Card 1 (white, radius 16, rows min height 64, padding 0/16, divider `rgba(0,0,0,.08)`): globe icon, "Language" 16.5/600 with caption "The app’s words, and what text is translated into" 13 `rgba(0,0,0,.58)`, value "English" 15/600 `rgba(0,0,0,.6)`, chevron; speaker icon, "Voice" with value "Amy" and chevron. Card 2 (padding 14/16/16): speedometer icon with "Reading speed" 16.5/600, then a 3x2 grid (gap 8) of 48-high radius-12 chips: "0.5×", "1×", "1.25×", "1.5×", "1.75×", "2×". Selected `#5B57E8` white 15/700; others `rgba(91,87,232,.10)` with `rgba(0,0,0,.6)` 15/600.
- **Behaviour (notes).** Text is read-only; touching it never opens the keyboard. **Edit** opens page 2 (R6) with this text; long-press for **Paste** also opens page 2. Fits a 6-inch phone without scrolling. Language opens today's chooser, Voice opens today's voice picker for that language; speed keeps its saved value.

Changed to match the mock (2026-10-24): the plan kept a translate toggle in Settings until Slice 101. Mock R3 has three settings only and says "There is no translate toggle". Translation is unavailable between Slices 99 and 101. That is the simpler choice: `ReaderPage.Translation.cs` and `TranslationActive()` (`ReaderPage.xaml.cs:1046`) read `TranslateEnabled`, and a main page with no card has no place to ask the source language, so the main page stops translating (reads the text as it is) and the switch UI is removed. Slice 101 brings translation back on page 2 and deletes the setting.

New strings (nine locales): "Settings", "Edit", "Text from pictures", "Text from a file", "Tap a button below to add something to read", "Language", "The app’s words, and what text is translated into", "Voice", "Reading speed".

## Work

- [x] 1. **Smallest end-to-end:** `SettingsPage` with language, voice and speed. The main page loses those
  rows and the speed presets and gains the gear button; reading still works the same.
- [x] 2. **The reading area:** the text area becomes read-only with the sentence highlight and the Edit
  link; the empty-state line; long-press Paste.
- [x] 3. **The two big buttons:** **Text from pictures** (today's scan until Slice 100) and **Text from a
  file** (today's file import until Slice 101); the translate card leaves the main page. Translation is
  unavailable until Slice 101 brings it back on page 2: the main page stops calling the translator and the
  switch UI is removed. Settings gets no toggle.
- [x] 4. Strings in all nine UI languages; the rule in `src/ReadTheStupidText.Mobile/CLAUDE.md` (written
  at planning; check it still matches).

## Verification

- Emulator (device-test), first launch in English and in Farsi: the main page shows only the header gear,
  the text area, the two buttons and the player, with no scrolling needed on a 6-inch phone. Settings
  changes the language, voice and speed, and the main page reads with them. Edit and long-press Paste
  work, and Play is disabled with no text.
- iOS compile check (`-p:IncludeIos=true`).

*Changed while building:* the main page's icons are the mock's SVG paths drawn as MAUI `Path`, plus the header watermark (`header_glyph.png`). Long-press Paste works through `LongPressBehavior` with Android and iOS partials. Translation is off on the main page until Slice 101; the language detector still routes the voice, and `TranslateSourceMigration` clears the stored source once. Unused strings were removed and five accessibility strings added, in all nine languages. `ReadTimingFormatter.FormatClock` is tested. Known limit: a language change lands on the main page, not Settings.

*From review:* high pass, 9 applied (#6 perf and #11 by-design skipped). Medium pass, 8 applied (#6 voice-name refresh and #8 approximate scroll skipped).

#### For CLAUDE.md (lifted 2026-10-24)

- `src/ReadTheStupidText.Mobile/`: the main page is read-only and shows only reading; language, voice and speed live in `SettingsPage` (route `settings`). Long-press Paste goes through `LongPressBehavior` (Android and iOS partials). The main page does not translate until Slice 101; `TranslateSourceMigration` clears the stored source once. (Slice 99: docs/plan/slices/99-rtst-mobile-minimal-main-page.md)

#### Verified on the emulator (2026-10-24, RTST_Test, fresh install)

- Empty state (dashed card, outlined buttons, dimmed player), no scrolling at 1080x2400, icons sized correctly.
- Long-press Paste, Play/Pause/skip with progress, Edit pauses the read, typing, OK back to read-only.
- Settings: 1.5x persists, the voice picker opens, a Farsi switch lands on the main page with Farsi labels and an LTR layout.
- Text from a file opens the picker; Text from pictures reaches the ML Kit scanner. The icon buttons have content descriptions.

#### Not yet verified

- The highlight covers the whole `SpeechTextChunker` chunk (up to three sentences), not one sentence as the mock shows. Probably old behaviour; needs a follow-up.
- A full TalkBack walk-through (only the labels were checked).

- Emulator result pending (the user will pass it separately).
- iOS device run.
- The voice name in Settings does not refresh after a voice pack installs.
