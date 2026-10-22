# Slice 85 — The layout never mirrors; only text flows by its own direction

*Built 2026-10-22 (wave 2026-10-22), PR #247; verified on the RTST_Test emulator in Farsi and Arabic, see Not yet verified for the rest.*

**Batch:** 14
**Decisions:** 92
**Depends on:** Slices 81 and 82 (all three edit `VoicePickerPage.xaml.cs` and `ReaderPage.xaml.cs`); run last in the batch.
**Touches:** `src/ReadTheStupidText.Mobile/AppShell.xaml.cs`, `src/ReadTheStupidText.Mobile/LanguageChooser.cs`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReaderPage.Translation.cs`, `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml.cs`, `src/ReadTheStupidText.Mobile/*.xaml`, `src/ReadTheStupidText.Mobile/CLAUDE.md`, `src/ReadTheStupidText.Mobile/TextFlow.cs`, `src/ReadTheStupidText.Application/Reading/TextDirection.cs`, `src/ReadTheStupidText.Mobile/TransportGlyphs.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/*`, `src/ReadTheStupidText.Application/Reading/TextDirection.cs`, `tests/ReadTheStupidText.Tests/*`
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

- [x] Pages and the shell set `FlowDirection.LeftToRight` (or drop the override);
      `UiFlowDirection.ForCurrentCulture`/`Mirror` go, or shrink to a text-only helper.
- [x] Text views get direction from their content: the editor and translation label use
      `FlowDirection` from the first strong character of their text (a small pure helper in
      Application, unit-tested), so a Farsi translation is right-aligned and English is not.
- [x] Action sheets (`DisplayActionSheetAsync` with a flow direction) pass `LeftToRight`.
- [x] `src/ReadTheStupidText.Mobile/CLAUDE.md`: replace the "RTL pages set `FlowDirection`"
      rule with "the layout never mirrors; text views follow their content".

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`
- Phone in فارسی: Reader, Voice and Language pages look like the English layout with Farsi
  labels; the Farsi translation box is right-aligned and reads correctly; an English text in
  the editor stays left-aligned. Same check in العربية.

*Changed while building:* `AppShell`, `ReaderPage`, `VoicePickerPage` and `LanguagePage` are fixed `LeftToRight`; action sheets and alerts pass `LeftToRight`. `UiFlowDirection.cs` and the Mirror padding are removed. `TextDirection.Resolve` (Application, first strong character with a UI fallback, tested in `TextDirectionTests.cs`) and `TextFlow.ForUi`/`ForContent` (Mobile) drive the editor, translated text, status/note labels, picker rows and the Language page. Transport glyphs are fixed in `TransportGlyphs.cs`; three mirrored glyph resx keys are deleted. The Mobile CLAUDE.md rules are reconciled on the branch.

*From review:* reviews `85-review-1.md` and `85-review-2.md` (in `D:\ReadTheStupidText-worktrees\reviews\`). Pass 1: fixed 1, 2, 3, 6, 8 (TimerLabel kept); 4 is a device check; 5 and 9 out of scope. Pass 2: all fixed except 2.

*Checks:* RTST tests 798 pass, 2 skipped; Mobile Android build green. Emulator (RTST_Test, Farsi and Arabic): layout not mirrored; Farsi file right-aligned RTL, English left-aligned, flips on content change, caret fine; Farsi/Arabic notes readable and right-aligned; voice picker readable. Static XAML headings stay on the left (acceptable).

#### For CLAUDE.md

- The Mobile layout never mirrors: pages, shell, action sheets and alerts stay `LeftToRight`; only text views take direction from their content via `TextFlow.ForContent` (first strong character, `TextDirection.Resolve`) or `TextFlow.ForUi` for UI strings. Do not reintroduce a page-level RTL `FlowDirection` or `UiFlowDirection`. Transport glyphs live in `TransportGlyphs.cs`, not resx. (Slice 85: docs/plan/slices/85-no-layout-mirroring.md) Place in `src/ReadTheStupidText.Mobile/CLAUDE.md` (already reconciled on the branch).

#### Not yet verified

- Arabic content in the editor on a device.
- Typing Farsi on a real keyboard.
- The first-run Language page in Farsi/Arabic.
- Open question for the user: the Windows desktop app still mirrors for fa/ar; plan a follow-up slice?
