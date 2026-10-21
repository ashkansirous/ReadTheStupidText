# CLAUDE.md — DinoReader.Core

- **DinoReader.Core** (`net10.0`, `src/DinoReader.Core`) — the pure dialogue library
  for Dino Reader: `StorySegmenter`, `SpeakerAttributor`, `GenderGuesser`,
  `VoiceCaster` (Decision 75). No package or project references (not Domain, not
  Application); voices come in as plain `VoiceOption(id, gender)`. Every language
  table lives in `LanguageRules` (`LanguageRules.English` today): a new language adds
  a rules instance and never touches the algorithms. Returns data only, no
  user-facing copy. Paragraphs are blank-line separated; a single newline is never a
  sentence or paragraph boundary (wrapped PDF/OCR text). (Slice 59: docs/plan/slices/59-dialogue-parser-voice-caster.md)

Tests: `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (xUnit v3, plain `net10.0`, any OS).
