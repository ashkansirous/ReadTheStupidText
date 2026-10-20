# Slice 29 — File upload: DOCX

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 29 — File upload: DOCX.** (Decision 35, Batch 5) Added
      **DocumentFormat.OpenXml** 3.5.1 (MIT, version confirmed via context7/NuGet,
      Decision 9) to Infrastructure; `DocxTextExtractor` walks the document body's
      paragraphs (`Descendants<Paragraph>().InnerText`) into plain text. Widened the
      `FileOpenPicker` filter to include `.docx`. Soft cap mirrors Slice 28's intent
      but reads the *cached* page count DOCX actually carries — Word's `app.xml`
      `ExtendedFilePropertiesPart.Properties.Pages` (a `.docx` has no fixed
      pagination of its own, unlike a PDF) — reusing `DocumentTooLargeException`
      when it's present and over 200; a document with no cached page count (never
      opened/saved in Word) skips the cap rather than being rejected on a number we
      don't have. Unit-tested (`DocxTextExtractorTests`) against fixture `.docx`
      files built with the Open XML SDK itself (unlike PdfPig, it can author as
      well as read), covering routing, paragraph order, the over-cap throw, and the
      no-cached-count skip.

## Verification (moved from plan.md)

- **Slice 29:** upload a `.docx` → text extracts and reads correctly.
