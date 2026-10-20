# Slice 33 — File upload: .txt/.pdf/.docx (reused)

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 33 — File upload: .txt/.pdf/.docx (reused).** (Decisions 34, 35,
      41, 43) Wire a MAUI file/document picker — reached from the bottom nav's
      **File** tab (Decision 43; no dedicated screen mock yet, reuse the other
      screens' card/list visual language) — to the *existing*
      `CompositeDocumentTextExtractor`/`PlainTextExtractor`/`PdfTextExtractor`/
      `DocxTextExtractor` from Batch 5 (Application interface, Infrastructure
      implementations — referenced from the mobile project's platform folder or
      promoted to a portable location if any Windows-only dependency is found
      during implementation). No new extraction logic; only new platform picker
      UI.
      **Built:** No Windows-only dependency was found — as the plan text
      anticipated, all four extractors are plain C# over PdfPig/
      DocumentFormat.OpenXml (both portable managed libraries), stuck in the
      `net10.0-windows` Infrastructure project only because that's where they
      happened to land in Batch 5. Rather than duplicating them into Mobile's
      own platform folder (which would drift from the Windows copy over time —
      the exact failure the Slice 28/31 layering fixes exist to prevent), they
      were **promoted to a new portable library**, `ReadTheStupidText.Documents`
      (`net10.0`, referencing only `Application` + PdfPig/OpenXml), added to
      the `.slnx`. Infrastructure now references it instead of containing the
      files directly (its own `PdfPig`/`DocumentFormat.OpenXml`
      `PackageReference`s moved with them); Mobile references it too — both
      platforms now share the literal same `CompositeDocumentTextExtractor`
      instance-shape, not a look-alike. `FilePage` follows the same DI pattern
      Windows' `App.xaml.cs` already used (register the three concrete
      extractors + `IDocumentTextExtractor → CompositeDocumentTextExtractor`),
      and reads through the same shared `ISpeechReader` singleton
      `TypePage`/`CameraPage` use. `FilePicker.Default.PickAsync` with a custom
      `FilePickerFileType` (Android MIME types for `.txt`/`.pdf`/`.docx`) —
      no native-packaging risk here (unlike Slices 31/32) since it just opens
      the system document picker, no bundled native library or model asset of
      its own. Verified: `dotnet build` (Mobile alone and the full `.slnx`,
      0 errors) and the 108-test suite (the three extractor-test files just
      needed their `using` updated for the new namespace — same tests,
      unmoved logic, still passing). **Not yet verified: running on a
      device/emulator** — same environment gap as Slices 30-32.

## Verification (moved from plan.md)

- **Slice 33:** use the file picker to open a `.txt`, `.pdf`, and `.docx` file in
  turn → each reads correctly, reusing the same extractors verified in Slices
  27-29.
