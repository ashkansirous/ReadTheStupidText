# Slice 27 — File upload: plain text (.txt)

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 27 — File upload: plain text (.txt).** (Decision 34/35, Batch 5) New
      Application interface `IDocumentTextExtractor { bool CanHandle(string
      extension); Task<string> ExtractTextAsync(string filePath); }`; Infrastructure
      `PlainTextExtractor` plus a `CompositeDocumentTextExtractor` that routes by
      extension, mirroring `CompositeSpeechReader`'s composition pattern. New
      `ActivityTrigger.FileUpload` enum value (Domain) — the Activity Log **Source**
      column shows the uploaded file's name for these entries. UI: an "Upload file"
      button in the control panel and a "Read file…" item in the tray right-click
      menu, both opening a `FileOpenPicker` filtered to `.txt` (filter widens in
      Slices 28-29); picking a file calls a new `ReadAloudService.ReadFileAsync(path)`
      that extracts text and feeds the existing read pipeline, superseding any
      in-progress read (Decision 34). Unit-test the composite extractor's routing and
      the plain-text extractor.

## Verification (moved from plan.md)

- **Slice 27:** use the panel's Upload button (or the tray "Read file…" item) on a
  `.txt` file → reading starts almost immediately, supersedes any read in
  progress, and the Activity Log shows `Trigger=FileUpload` with the file name in
  **Source**. `dotnet test` covers the composite extractor's routing.
