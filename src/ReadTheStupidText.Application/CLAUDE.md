# CLAUDE.md — Application

- Reading (`Reading/`): blank text is `BlankText.IsBlank` (whitespace plus format/control characters), never `string.IsNullOrWhiteSpace`; a blank speak on a `CompositeSpeechReader` still raises `Completed`. (Slice 176: docs/plan/slices/176-whitespace-read-crash.md)
