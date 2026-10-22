# Slice 85 — The layout never mirrors; only text flows by its own direction

*Planned.*

**Batch:** 14
**Decisions:** 92
**Depends on:** Slices 81 and 82 (all three edit `VoicePickerPage.xaml.cs` and `ReaderPage.xaml.cs`); run last in the batch.
**Touches:** `src/ReadTheStupidText.Mobile/UiFlowDirection.cs`, `src/ReadTheStupidText.Mobile/AppShell.xaml.cs`, `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Translation.cs`, `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml.cs`, `src/ReadTheStupidText.Mobile/*.xaml`, `src/ReadTheStupidText.Mobile/CLAUDE.md`
**Story issue:** —

## Goal

Choosing Farsi or Arabic translates the labels but leaves the layout as it is: buttons,
rows, the player and the speed chips stay left-to-right. Text inside the editor, the
translation box and labels still flows in its own direction (Farsi right-to-left).

## Why

Seen on a Pixel 10 Pro, 1.16.0, 2026-10-22: with the app in فارسی the whole Reader mirrors
(Paste/Scan/File reversed, player controls and speed chips flipped). This was built on purpose
(Decision 68: "full RTL layout for fa and ar"; `UiFlowDirection.ForCurrentCulture` on every
page). The user: "The language of the app … should not even change the direction of the
layout and buttons."

## Decisions locked with the user (2026-10-22)

- **Never mirror.** Pages, shells and control rows are always `LeftToRight`. Text views
  (editor, translation label, status labels with RTL text) align by the text's own direction.
  Supersedes the RTL-layout part of Decision 68.

## Steps

- [ ] Pages and the shell set `FlowDirection.LeftToRight` (or drop the override);
      `UiFlowDirection.ForCurrentCulture`/`Mirror` go, or shrink to a text-only helper.
- [ ] Text views get direction from their content: the editor and translation label use
      `FlowDirection` from the first strong character of their text (a small pure helper in
      Application, unit-tested), so a Farsi translation is right-aligned and English is not.
- [ ] Action sheets (`DisplayActionSheetAsync` with a flow direction) pass `LeftToRight`.
- [ ] `src/ReadTheStupidText.Mobile/CLAUDE.md`: replace the "RTL pages set `FlowDirection`"
      rule with "the layout never mirrors; text views follow their content".

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`
- Phone in فارسی: Reader, Voice and Language pages look like the English layout with Farsi
  labels; the Farsi translation box is right-aligned and reads correctly; an English text in
  the editor stays left-aligned. Same check in العربية.
