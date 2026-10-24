# Slice 109 — Dino: one narrator per language, for every book

*Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
reading language, one narrator, parent recordings, store filters" (2026-10-24) —
https://drive.google.com/file/d/1b2XPenCW2D4Kx1yx98js_LK4CRBSnnLf/view?usp=drivesdk (part 4, milestone 4).

**Batch:** 22
**Depends on:** Slice 108 (both rewrite the cast and narrator code; run after it)
**Touches:** `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/StoryDraft.cs`, `src/DinoReader.Core/CastEditor.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/TextStep.xaml.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/NarratorPage.cs` (new), `src/DinoReader.App/NarratorChoice.cs` (new), `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

The user wants the narrator chosen once for all books, not per book. Today `Story.NarratorVoiceId` is
persisted in `story.json` and is required for a story to load (`StoryJson.cs:221`). It is read in
`StoryVoicing`, `ReadingSession.cs:181`, `CastPage.cs:166-301`, `StoryActions.cs:65`, `StoryImport.cs:222,229`,
`TextStep.xaml.cs` and `StoryDraft.cs`. Dino has no Settings screen.

## Decisions (2026-10-24, agreed with the user)

- **Decision 114: one narrator per language, chosen on the parent's main page.** A stored narrator voice
  per language (default: today's `NarratorFor` pick) is used by every book: starter, store and own.
  It is chosen from a **Narrator** button in the gallery's grown-up mode (the parent's main page, beside
  Free stories, Backup and Language), which opens a grown-up `NarratorPage` (`IGrownUpPage`) listing each
  language with a voice picker and a sample. The cast screen shows the narrator as a read-only row with a
  "Change for all books" link to that page; the per-book narrator picker and its notice are removed.
- `story.json` still reads `narratorVoiceId` from old stories and packages (and no longer requires it to
  load), but ignores it; new stories don't write it. `schemaVersion` stays 1. Any character whose voice
  equals the new narrator's is re-cast through `CastEditor.NamesLosingVoice`/`UnpinOverridden`, per
  language's cast (Slice 108).

## Work

- [ ] 1. `NarratorChoice`: per-language setting (named key constant), default from `NarratorFor`; unit-
  testable resolution in Core if it holds logic.
- [ ] 2. Replace every `story.NarratorVoiceId` read with `NarratorChoice` for the reading language;
  `StoryJson` stays backward compatible and no longer treats the field as required.
- [ ] 3. `NarratorPage` and the grown-up gallery's Narrator button (a new `IconKind` only if none fits).
- [ ] 4. Cast page: read-only narrator row with the "Change for all books" link; remove the per-book picker.
  Changing the narrator re-casts clashing characters in every story's cast for that language.
- [ ] 5. Upgrade tests: an old story, an old `.dino` and an old backup still open and read.
- [ ] 6. Rule in `src/DinoReader.App/CLAUDE.md` (replaces the Slice 65 per-story narrator line).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: grown-up mode, Narrator, change the English narrator; open a starter, a store book and
  an own book: all three use it; the cast page shows it with the link and no picker.
