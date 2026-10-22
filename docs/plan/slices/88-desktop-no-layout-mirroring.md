# Slice 88 — The Windows desktop layout never mirrors; only text flows by its own direction

*Planned.*

**Batch:** 14 (follow-up to Slice 85)
**Depends on:** Slice 85 (`TextDirection.Resolve` in Application)
**Touches:** `src/ReadTheStupidText.App/UiStrings.cs`, `src/ReadTheStupidText.App/ControlPanelWindow.xaml`, `src/ReadTheStupidText.App/ControlPanelWindow.xaml.cs`, `src/ReadTheStupidText.App/ActivityLogWindow.xaml`, `src/ReadTheStupidText.App/ActivityLogWindow.xaml.cs`, `src/ReadTheStupidText.App/LanguageWindow.xaml.cs`, `src/ReadTheStupidText.App/ReadingTextBoxWindow.xaml`, `src/ReadTheStupidText.App/ReadingTextBoxWindow.xaml.cs`, `src/ReadTheStupidText.App/TextFlow.cs` (new), `src/ReadTheStupidText.App/CLAUDE.md`
**Story issue:** —

## Goal

The same rule as Slice 85, on the Windows tray app: in Farsi and Arabic the windows keep the
English layout (control panel, transport row, activity log, language window, reading text box). Only
text takes its own direction: read, OCR or activity text by its first strong letter, and localised
strings by the UI language.

## Why

The user confirmed the follow-up on 2026-10-22 ("slice 85: yes"). Today `UiStrings.CurrentFlowDirection`
(`App/UiStrings.cs:66-69`) sets `RightToLeft` on each window's `RootGrid`, and every child inherits it.

## Decisions locked with the user (2026-10-22)

- No layout mirroring on Windows either; text flows by its own direction (Decision 92, Slice 85).

## Steps

- [ ] Each window's `RootGrid` is fixed `LeftToRight`: ControlPanel (`.xaml.cs:100`), ActivityLog (`:36`),
      Language (`:36`), ReadingTextBox (`:73`). Retire `CurrentFlowDirection` or make it text-only.
- [ ] `App/TextFlow.cs`: the WinUI twin of the Mobile helper (`ForUi`, `ForContent`) on
      `TextDirection.Resolve`.
- [ ] Apply `ForContent` to the reading text box text (and `MeasureProbe`, so the measured layout
      matches) and to the activity-log item text; apply `ForUi` to localised notes such as `TranslateNote`.
- [ ] Remove the now-redundant LTR override on the transport row (`ControlPanelWindow.xaml:373`) if
      nothing else needs it.
- [ ] `App/CLAUDE.md` rule.

## Verification

- `dotnet build src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64`; `dotnet test`.
- Desktop (`device-test`), UI in Farsi then Arabic: the control panel, activity log and language window
  match the English layout. A Farsi read in the text box is right-to-left and an English one is
  left-to-right. Farsi entries in the activity log are right-aligned.
