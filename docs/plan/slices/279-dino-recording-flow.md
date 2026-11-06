# Slice 279 — Dino: recording your own voice, one intro page, then per character or whole page

*Planned 2026-11-06.* Dino Reader only, Android, iOS and the Windows head the same in the same slice.

**Depends on:** Slice 278 (the cast and voice code it removes is the code this slice would otherwise have to work around).
**Touches:** `src/DinoReader.App/{RecordPage,RecordViews,RecordingAudio,StoryActions,BookSheet,IntroWizardPage,ReadingClips,ReadingSession,ReadingSurface,PictureTextPages,TextPages,MauiProgram}.cs`,
`src/DinoReader.App/RecordIntroPage.cs` (new), `src/DinoReader.App/RecordPieces.cs` (new),
`src/DinoReader.App/Platforms/*/RecordingAudio.*.cs`, `src/DinoReader.App/Resources/**` (strings, 13 languages),
`src/DinoReader.Core/Story*.cs` (recording model), `src/DinoReader.Core/RecordSplit*.cs` (new),
`tests/DinoReader.Tests/**`, `tools/emulator/**` (the scripted record flow), `docs/design/dino-reader/**`,
`src/DinoReader.App/CLAUDE.md`, this file.

## Why

The user, 2026-11-06: "the path for adding your own voice is not good. all the text is polluting the page when
recording the story leaving only 1 line to read that no one can find. specially on a small device like iphone 8."

Today (origin/main): `RecordPage` records one clip per page and shows on every page the instruction line, the
"recordings stay on this phone" note, a subtitle and "N of M pages recorded", around the page's words.

## Decisions (the user, 2026-11-06)

- **Intro page, every time Record is tapped.** It holds all the text that applies to every recording: read the text
  shown, skip any part you don't want to read yourself, and your voice stays on this phone and is never uploaded to
  any server. None of this is repeated on the recording pages.
- Two choices on it: **record per character** or **record the whole page**.
- Per character shows a checkbox list of every character in the book, with the **Storyteller** (the narration) first;
  all are ticked by default.
- **Recording page:** the same picture-and-text layout as the reading page.
  - Per character: one line at a time, shown exactly as in the book, with who says it ("Rex says:"). Record that one
    line, then go on to the next line of a ticked character, in story order.
  - Whole page: all of the page's text, so it can be read. If the text would take more than half the screen, cut it
    at the end of a paragraph and record it in pieces.
- The Windows head gets the same flow.

## Decided in planning (not asked; the user can change them)

- The intro page remembers the last choice and the ticked characters for that book.
- Each piece has Record, Skip and Next; a recorded piece shows Play and Re-record.
- If a single paragraph alone is more than half the screen, it is cut at the end of a sentence.
- The iPhone 8 screen (375 x 667 points) is the size the layout is checked against.
- Playback: a piece plays the family's recording where one exists and the pre-made audio everywhere else, so a
  book can mix both. Existing whole-page recordings keep working as whole-page recordings.
- Recordings stay in backups only, never in shared books or uploads (Slice 230's rule stays).

## Work

- [ ] 1. Core: a pure split of a page into recording pieces. Per character: the lines of the ticked speakers in
  story order. Whole page: paragraphs packed up to a height limit, then sentences if one paragraph is too tall
  (the height comes from the app's text measurer, so the split itself is testable with fake heights).
- [ ] 2. Core: the recording model holds a clip per piece (keyed by page and the piece's sentence range), next to
  today's per-page clip, which loads unchanged.
- [ ] 3. App: `RecordIntroPage` with the shared text, the two choices and the character checklist (Storyteller first,
  all ticked); entered from the book sheet's Record pill and the intro wizard's record step.
- [ ] 4. App: the recording page in the reading page's picture-and-text layout, one piece at a time, with only
  Record / Skip / Next (and Play / Re-record after recording), "<name> says:" above a character's line. Android, iOS
  and Windows head.
- [ ] 5. Playback: reading picks the family's clip for a piece if there is one, else the pre-made clip.
- [ ] 6. Strings in all 13 languages; the old per-page instruction, note and counter strings are removed.
- [ ] 7. Design: add the intro page and both recording page modes to the design copy in `docs/design/dino-reader/`.
- [ ] 8. A scripted emulator flow: tap Record, pick per character with one character unticked, record two lines,
  skip one, then read the page back.

## Verification

- `dotnet test tests/DinoReader.Tests`: split tests (character order, unticked speakers left out, paragraph cut,
  sentence cut, a short page as one piece) and model tests (old per-page clips still load).
- Android emulator at a small phone size and the iOS simulator on iPhone 8: the intro page shows once per Record tap;
  the recording page shows the whole piece with no instruction text; a long page splits at a paragraph end; reading
  back mixes recorded and pre-made pieces.
- Windows head: the same flow records and plays back.
