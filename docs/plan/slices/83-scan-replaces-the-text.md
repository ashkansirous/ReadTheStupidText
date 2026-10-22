# Slice 83 — Scan replaces the text, like Paste and File

*Built 2026-10-22 (wave 2026-10-22), PR #245. Scan itself not yet verified on a device.*

**Batch:** 14
**Decisions:** 90
**Depends on:** —
**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Application/Images/ScannedText.cs`, `tests/ReadTheStupidText.Tests/ScannedTextTests.cs`, `src/ReadTheStupidText.Mobile/CLAUDE.md`, `src/ReadTheStupidText.Mobile/Resources/Strings/**`
**Story issue:** —

## Goal

A new scan puts only the scanned pages in the text box. Whatever was there before (an earlier
scan, pasted text, a file) is replaced, exactly as Paste and File already do.

## Why

Seen on a Pixel 10 Pro, 1.16.0, 2026-10-22: tapping Scan added the new page after the text
already in the box. `ReaderPage.ApplyScanResultAsync` calls
`ScannedText.AppendPages(TextEditor.Text, texts)` (Decision 64: "appended to the editor"),
while `OnPasteTapped` and `OnFileTapped` both call `ReplaceEditorText`.

## Decisions locked with the user (2026-10-22)

- Paste, Scan and File all **replace** the editor's text. The pages of one multi-page scan
  are still joined in order. Supersedes the "appended" part of Decision 64.

## Steps

- [x] `ScannedText`: a `JoinPages(texts)` that builds the text from the pages alone (blank
      pages skipped, same separators as today); `AppendPages` goes if nothing else uses it.
- [x] `ApplyScanResultAsync` replaces the editor with `JoinPages(texts)`; the "no text found"
      alert fires when the joined text is empty (not when it equals the old text).
- [x] `ScannedTextTests` updated: a scan into a box with text yields only the scanned pages.
- [x] `src/ReadTheStupidText.Mobile/CLAUDE.md` Scan rule: "replaces the editor" instead of
      "appends via `ScannedText.AppendPages`".

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`
- Phone: paste some text, then Scan one page: the box holds only that page. Scan two pages:
  both, in order. A scan of a blank page leaves the old text and shows "No text found".

## Changed while building

- `ScannedText.JoinPages` trims each page, skips blanks and joins with a blank line; `AppendPages` and the dead `PageSeparator` are removed.
- A scan goes through `ReplaceEditorText`, like Paste and File. `ReplaceEditorText` now stops playback and pending/translation reads first.
- The editor is read-only while busy (Scan, Paste, File) and restored in a `finally`.
- The partial-scan alert is reworded in the 9 locales (hence the `Resources/Strings/**` Touches entry).

## From review

Findings: `D:\ReadTheStupidText-worktrees\reviews\83-review-1.md` and `83-review-2.md`.
- Review 1: fixed 1-4; 5 accepted (the spec locks replace).
- Review 2: fixed 1; 2-3 low, skipped.

## Verified

- `dotnet test`: 727 pass, 2 skipped. Mobile Android build passes.
- Emulator (RTST_Test): Paste during playback stops it and replaces the text; a cancelled scan keeps the old text.
- Scan itself is not testable on the emulator (the ML Kit scanner errors in Play services).

#### For CLAUDE.md

- Scan, Paste and File all replace the editor text; Scan uses `ScannedText.JoinPages`. `ReplaceEditorText` stops playback and pending/translation reads, and the editor is read-only while busy, restored in a `finally` (`ReaderPage.xaml.cs`). Already applied to `src/ReadTheStupidText.Mobile/CLAUDE.md` on this branch. (Slice 83: docs/plan/slices/83-scan-replaces-the-text.md)

#### Not yet verified

- One-page, two-page and blank-page scans on a Google-signed-in device.
- Editor is read-only during OCR.
