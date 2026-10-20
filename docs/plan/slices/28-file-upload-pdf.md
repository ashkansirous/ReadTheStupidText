# Slice 28 — File upload: PDF

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 28 — File upload: PDF.** (Decision 35, Batch 5) Added **PdfPig**
      0.1.16 (Apache-2.0, version confirmed via context7/NuGet, Decision 9) to
      Infrastructure; `PdfTextExtractor` joins each page's `page.Text` in order.
      Widened the `FileOpenPicker` filter to include `.pdf`. Threshold: a soft cap
      of **200 pages** — above it `PdfTextExtractor` throws a new
      `DocumentTooLargeException` (Application/Documents, carrying `Actual`/`Limit`)
      before extracting any text, rather than synthesizing a huge document;
      `ReadAloudService.ReadFileAsync` catches it ahead of the generic extraction
      failure and logs it at `Warning` (vs. `Error` for a genuinely corrupt file) —
      text-layer PDFs only, no OCR (a scanned/image-only PDF just yields empty
      pages). *Layering fix:* `CompositeDocumentTextExtractor` **and**
      `PlainTextExtractor` moved from Application to **Infrastructure/Documents**
      (mirroring `CompositeSpeechReader`/`SpeechReader`/`SupertonicSpeechReader`'s
      placement, and matching what Slice 27's own plan entry already said —
      Application landed them there by mistake) — Application must not reference
      Infrastructure, and `PdfTextExtractor` (depends on the PdfPig package) can
      only be composed by a class allowed to see both. Unit-tested
      (`PdfTextExtractorTests`) against a small
      hand-built fixture PDF (PdfPig is read-only, so the fixture is generated with
      a minimal hand-rolled PDF writer in the test file) covering routing, page
      order, and the 200-page cap.

## Verification (moved from plan.md)

- **Slice 28:** upload a multi-page PDF → text extracts and reads correctly; an
  oversized PDF triggers the soft-cap warning instead of hanging.
