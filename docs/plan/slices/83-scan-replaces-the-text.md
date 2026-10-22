# Slice 83 — Scan replaces the text, like Paste and File

*In progress (wave 2026-10-22), draft PR.*

**Batch:** 14
**Decisions:** 90
**Depends on:** —
**Touches:** `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Application/Images/ScannedText.cs`, `tests/ReadTheStupidText.Tests/ScannedTextTests.cs`, `src/ReadTheStupidText.Mobile/CLAUDE.md`
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

- [ ] `ScannedText`: a `JoinPages(texts)` that builds the text from the pages alone (blank
      pages skipped, same separators as today); `AppendPages` goes if nothing else uses it.
- [ ] `ApplyScanResultAsync` replaces the editor with `JoinPages(texts)`; the "no text found"
      alert fires when the joined text is empty (not when it equals the old text).
- [ ] `ScannedTextTests` updated: a scan into a box with text yields only the scanned pages.
- [ ] `src/ReadTheStupidText.Mobile/CLAUDE.md` Scan rule: "replaces the editor" instead of
      "appends via `ScannedText.AppendPages`".

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`
- Phone: paste some text, then Scan one page: the box holds only that page. Scan two pages:
  both, in order. A scan of a blank page leaves the old text and shows "No text found".
