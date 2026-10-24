# Slice 99 — RTST phone: a minimal main page and a Settings page

*Planned 2026-10-24*, from the user's report: "I gave Read The Stupid Text to my dad and my wife's dad,
and they did not know what to do with it. The page is very crowded, and they didn't even see the add
more images button. The main page should keep the minimal requirements: the text being read, two
buttons that start a wizard for adding a new text, and a settings button for the language and the
voice."

**Batch:** 19 (RTST phone overhaul for first-time users)
**Depends on:** Slices 45 (scan), 53 (localised UI), 54 (translation), 85 (layout never mirrors)
**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Translation.cs`, `src/ReadTheStupidText.Mobile/SettingsPage.xaml` (new), `src/ReadTheStupidText.Mobile/SettingsPage.xaml.cs` (new), `src/ReadTheStupidText.Mobile/AppShell.xaml`, `src/ReadTheStupidText.Mobile/AppShell.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile/EditorDraft.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/*.resx`, `src/ReadTheStupidText.Mobile/CLAUDE.md`

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
  freed space goes to the text area.
- **Settings is one page** (pushed route `settings`): the app language (today's `LanguageChooser`), the
  voice (opens today's `VoicePickerPage`) and the reading speed (the same presets). Speed persists as it
  does today.
- **Read-only main text.** Touching the text doesn't open the keyboard. Long-press offers Paste, which
  opens wizard page 2 (Slice 101) with the pasted text. **Edit** opens page 2 with the current text.
  Until Slice 101 lands, Edit and Paste use today's in-place editor so the slice ships on its own.
- **An empty start.** With no text, the text area shows one friendly line ("Tap a button below to add
  something to read") and Play is disabled.
- **Scope:** Android and iOS. The Windows tray app is unchanged. The layout is built in the current
  style, with no Claude Design mock first. The layout never mirrors (Decision 92).

## Work

- [ ] 1. **Smallest end-to-end:** `SettingsPage` with language, voice and speed. The main page loses those
  rows and the speed presets and gains the gear button; reading still works the same.
- [ ] 2. **The reading area:** the text area becomes read-only with the sentence highlight and the Edit
  link; the empty-state line; long-press Paste.
- [ ] 3. **The two big buttons:** **Text from pictures** (today's scan until Slice 100) and **Text from a
  file** (today's file import until Slice 101); the translate card leaves the main page (Slice 101 brings
  translation back on page 2; until then the translate toggle sits in Settings so nothing is lost).
- [ ] 4. Strings in all nine UI languages; the rule in `src/ReadTheStupidText.Mobile/CLAUDE.md` (written
  at planning; check it still matches).

## Verification

- Emulator (device-test), first launch in English and in Farsi: the main page shows only the header gear,
  the text area, the two buttons and the player, with no scrolling needed on a 6-inch phone. Settings
  changes the language, voice and speed, and the main page reads with them. Edit and long-press Paste
  work, and Play is disabled with no text.
- iOS compile check (`-p:IncludeIos=true`).
