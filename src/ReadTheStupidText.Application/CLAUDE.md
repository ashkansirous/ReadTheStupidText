# CLAUDE.md — Application

- Reading (`Reading/`): blank text is `BlankText.IsBlank` (whitespace plus format/control characters), never `string.IsNullOrWhiteSpace`; a blank speak on a `CompositeSpeechReader` still raises `Completed`. (Slice 176: docs/plan/slices/176-whitespace-read-crash.md)
- Reading (`Reading/`): Kokoro on arm64 can return truly silent audio for short lines; `SilentOutputGuard.cs` rescues with `SiblingVoice.cs`, which must map every shipped Kokoro voice to a same-language, same-gender sibling (a test enforces it). Add a sibling there when adding a Kokoro voice; don't lower the threshold. Never log the line's text. (Slice 210: docs/plan/slices/210-kokoro-short-line-dropped.md)
