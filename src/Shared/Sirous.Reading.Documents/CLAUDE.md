# CLAUDE.md — Documents

Loaded when you work under `src/Shared/Sirous.Reading.Documents` (`net11.0`, depends on Application).

- `PlainTextExtractor`, `PdfTextExtractor`, `DocxTextExtractor` and `CompositeDocumentTextExtractor` (Slice 33) live here, not in Infrastructure or Application: they need PdfPig and DocumentFormat.OpenXml (so not Application, which stays library-free) but have no Windows-only dependency (so not stuck in Infrastructure).
- Infrastructure (Windows) and Mobile (Android) both reference this project and share the same extractors.
