# Slice 88 — The Windows desktop layout never mirrors; only text flows by its own direction

*Built 2026-10-22 (wave 2026-10-22), PR #254; desktop check in Farsi and Arabic passed; paging back not yet verified.*

**Batch:** 14 (follow-up to Slice 85)
**Depends on:** Slice 85 (`TextDirection.Resolve` in Application)
**Touches:** `src/ReadTheStupidText.App/UiStrings.cs`, `src/ReadTheStupidText.App/ControlPanelWindow.xaml`, `src/ReadTheStupidText.App/ControlPanelWindow.xaml.cs`, `src/ReadTheStupidText.App/ControlPanelWindow.Language.cs`, `src/ReadTheStupidText.App/ActivityLogWindow.xaml`, `src/ReadTheStupidText.App/ActivityLogWindow.xaml.cs`, `src/ReadTheStupidText.App/LanguageWindow.xaml.cs`, `src/ReadTheStupidText.App/ReadingTextBoxWindow.xaml`, `src/ReadTheStupidText.App/ReadingTextBoxWindow.xaml.cs`, `src/ReadTheStupidText.App/TextFlow.cs` (new), `src/ReadTheStupidText.App/CLAUDE.md`
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

- [x] Each window's `RootGrid` is fixed `LeftToRight`: ControlPanel (`.xaml.cs:100`), ActivityLog (`:36`),
      Language (`:36`), ReadingTextBox (`:73`). Retire `CurrentFlowDirection` or make it text-only.
- [x] `App/TextFlow.cs`: the WinUI twin of the Mobile helper (`ForUi`, `ForContent`) on
      `TextDirection.Resolve`.
- [x] Apply `ForContent` to the reading text box text (and `MeasureProbe`, so the measured layout
      matches) and to the activity-log item text; apply `ForUi` to localised notes such as `TranslateNote`.
- [x] Remove the now-redundant LTR override on the transport row (`ControlPanelWindow.xaml:373`) if
      nothing else needs it.
- [x] `App/CLAUDE.md` rule.

## Verification

- `dotnet build src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64`; `dotnet test`.
- Desktop (`device-test`), UI in Farsi then Arabic: the control panel, activity log and language window
  match the English layout. A Farsi read in the text box is right-to-left and an English one is
  left-to-right. Farsi entries in the activity log are right-aligned.

Changed while building:
- `UiStrings.IsRightToLeft` replaces `CurrentFlowDirection`. `TextFlow.ForUi` only (`ForContent` removed): the reading text's direction is set once per Repaginate from the full text via `ApplyContentDirection`, on `ReadingText` and `MeasureProbe`. Activity rows (`ActivityRowVm`) carry UI, content and source directions computed once; `TextAlignment` is Start on RTL content; `ForUi` is re-applied after a language change (`ControlPanelWindow.Language.cs`).
- Static `x:Uid` labels stay LTR, as on mobile.
- The desktop run found multi-line RTL reading text showing only its last line. Fixed by setting the content direction on the `TextScroller` `ScrollViewer` (not the `TextBlock`), with `ReadingText` stretched and horizontal scrolling disabled. Reviewed (`reviews/88-review-3.md`): no findings.

From review (`reviews/88-review-1.md`, `88-review-2.md`): pass 1 fixed 1, 3-9, finding 10 was docs; pass 2 fixed 1, 2, 4, 5, 7, accepted 3, and sent 6 to the desktop check.

Checks: App x64 build 0 warnings; RTST tests 809 pass.

#### For CLAUDE.md (lifted 2026-10-22)

- `src/ReadTheStupidText.App/CLAUDE.md` (already written on the branch): the layout never mirrors; each `RootGrid` is fixed `LeftToRight`; only text takes a direction through `TextFlow` (`ContentDirection`/`ForUi`). Never set `FlowDirection` on a container. (Slice 88: docs/plan/slices/88-desktop-no-layout-mirroring.md)

#### Not yet verified

- Desktop PASS in Farsi and Arabic: layout matches English, activity rows aligned by content, numerals correct, a 910-char Farsi read shows all lines RTL across pages, English stays LTR.
- Paging back (the desktop has no back control).
