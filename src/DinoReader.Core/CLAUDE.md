# CLAUDE.md — DinoReader.Core

- **DinoReader.Core** (`net10.0`, `src/DinoReader.Core`) — the pure dialogue library
  for Dino Reader: `StorySegmenter`, `SpeakerAttributor`, `GenderGuesser`,
  `VoiceCaster` (Decision 75). No package references; it references Application only for `LineReflow` (not Domain); voices come in as plain `VoiceOption(id, gender)`. Every language
  table lives in `LanguageRules` (`LanguageRules.English` today): a new language adds
  a rules instance and never touches the algorithms. Returns data only, no
  user-facing copy. Paragraphs are blank-line separated; a single newline is a
  sentence/segment boundary only where `LineReflow` (Application) says it is a real break, never on a bare newline (wrapped PDF/OCR text). (Slice 59: docs/plan/slices/59-dialogue-parser-voice-caster.md; Slice 76: docs/plan/slices/76-soft-line-break-reflow.md)
- `Story.BreakMap` uses geometry only when the included lines match the text. (Slice 79: docs/plan/slices/79-background-text-filtering.md)
- A story travels as a `.dino` zip (`StoryPackageWriter`/`StoryPackageReader`): `package.json` (`schemaVersion`), `story.json`, pictures, all top-level plain names. Import gives a new id and records the package's id as `Story.SourceId` (backup restore passes `keepId`); it refuses a newer schema, any entry name with a path part (zip-slip), and oversized entries, and `IStoryLibrary.ImportAsync` writes `story.json` last and removes the folder on failure. (Slice 91: docs/plan/slices/91-dino-story-packages-and-starters.md)

Tests: `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (xUnit v3, plain `net10.0`, any OS).
