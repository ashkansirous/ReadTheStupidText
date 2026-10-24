# Slice 110 — Dino: translated text on screen, with "Show original text"

*Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
reading language, one narrator, parent recordings, store filters" (2026-10-24) —
https://drive.google.com/file/d/1b2XPenCW2D4Kx1yx98js_LK4CRBSnnLf/view?usp=drivesdk (part 1, milestone 2).

**Batch:** 22
**Depends on:** Slice 107; runs after 108 and 109 (all three edit `StoryVoicing` and `ReadingSession`)
**Touches:** `src/DinoReader.Core/**` (voiced segment and display-span mapping types), `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/ReadingSurface.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/PictureTextPages.cs`, `src/DinoReader.App/ScanPages.cs`, `src/DinoReader.App/TextPages.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

When a book reads in the grown-up's language, only the audio is translated; the screen shows the source
text, so what the child sees doesn't match what they hear. Slice 95 put on-screen translation out of
scope; this slice reverses that. It is more than a text swap: the highlight is driven by
`VoicedSegment.SourceStart/SourceEnd/TextStart` (`StoryVoicing.cs:220`), which index the source text, and
scanned pages highlight source-language OCR line boxes (`ScanPages`/`ScanLayout`).

## Decisions (2026-10-24, agreed with the user)

- **Decision 112: the screen shows the translation, with a per-story "Show original text" switch.** When
  `StoryTranslator.Applies`, every page shows the translated text and the highlight follows the
  translated sentence. Farsi and Arabic text is RTL; the layout never mirrors (Slice 96). Scanned pages
  with line boxes render as picture plus a translated text panel (Slice 98's `PictureTextPages` layout)
  while translated. A grown-up per-story switch "Show original text" (default off, saved as an optional
  `story.json` field) returns the screen to the source text and OCR boxes; the audio stays translated.
- On a page with a parent recording (Slice 111), the text shown is in the recording's language.

## Work

- [ ] 1. Core: translated page text plus a piece-to-display-span map beside the source offsets, so a
  voiced segment can highlight either text; unit tests including RTL and multi-piece sentences.
- [ ] 2. `ReadingSession`/`ReadingSurface`/`TextPages`: render and highlight the translated text.
- [ ] 3. Scanned picture pages switch to the picture-plus-text layout while translated.
- [ ] 4. The "Show original text" switch (grown-up only, on the reading page's grown-up controls) and its
  persistence.
- [ ] 5. Rule in `src/DinoReader.App/CLAUDE.md` (amend the Slice 95 "speakers in the source language"
  bullet: the screen shows the translation).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator, app in Farsi: a starter and a scanned English book show Farsi RTL text, highlight
  follows each spoken sentence, the scanned book shows picture plus text; "Show original text" brings back
  English and the OCR boxes.
