# Slice 279 — Dino: recording your own voice, one intro page, then per character or whole page

*In progress (wave 2026-11-06), draft PR.* Dino Reader only, Android, iOS and the Windows head the same in the same slice.

Claim notes (wave 32): the open points under *Design* take their stated defaults (follow the mock). Touches widened at
claim from a code check: `src/DinoReader.Core/{StoryRecordings,Story,RecordLayout,BookSheetPlan,ITextMeasurer}.cs`,
the storage API (`IStoryLibrary`, `FileStoryLibrary`), `StoryPackageWriter`/`StoryBackup*` for the per-piece clips,
`src/DinoReader.App/{GalleryPage.xaml.cs,GalleryPage.Desktop.cs,ReadingPage.xaml.cs,IntroWizardPage.Art.cs}`,
`src/DinoReader.App/Platforms/Windows/{DinoKeyboard,WindowsTextMeasurer}.cs`, a new
`src/DinoReader.App/Testing/DinoTestCommands.Record.cs` (plus one dispatch line in the existing test-command router),
and `.maestro/dino/**`. `src/DinoReader.Core/StoryTranslation.cs` is not this slice's (Slice 280 edits it).

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

#### Design (Dino Reader Batch 48 mock, frames 178-184 and 188-191, added 2026-11-06)

Eleven frames in `docs/design/dino-reader/Dino Reader Batch 48 Store Only and Recording.dc.html` (line numbers in brackets). Recording frames are 387 x 679 (an iPhone 8, 375 x 667, plus frame border); the Windows frame is 1100 x 760. The mock agrees with the Decisions above and adds behaviour for a book that already has recordings (188-191), listed as work items 9-13 below. Icons are inline SVG, not Phosphor. Fonts: Fredoka for UI, Andika for story text. Tokens: brand `#5B57E8`, coral `#FF7A59` (shadow `0 5px 0 #E2583A`), record red `#E5484D`, soft purple `#ECE7FF`, page `#F3EFE6`, ink `#2b2540`, muted `#5a5470` / `#8a8399`.

- **Intro, per character (178, line 56).** Header: back arrow (44px target), title "Record your voice" (20px, 600), book title under it (12px `#8a8399`). Three notes with a small icon: "Read aloud the words on each screen." / "Skip anything you'd rather Dino reads." / "Your voice stays on this phone. It is never uploaded." Two side-by-side mode cards, radius 16: "One character at a time" (selected: white, 2.5px `#5B57E8` ring, radio 20px with 10px dot) and "Whole pages". Heading "Who will you record?", then rows 44px high: 24px checkbox (radius 7, brand fill, white check), name 15px 600, count 12.5px `#8a8399` ("24 lines"). Order: Storyteller, The Wolf, First little pig, Second little pig, Third little pig; all ticked. Pinned button "Start recording": 52px, radius 26, coral with shadow, white 17px 600. The list scrolls under the pinned button; Start is disabled with nobody ticked; choice and ticks are remembered per book; Back returns to the book sheet.
- **Intro, whole pages (179, line 87).** Same header, notes and cards. The checklist is replaced by one quiet line: "Long pages come in parts, so the words always fit on the screen." Switching cards never moves the notes or the button. Existing whole-page recordings open as whole pages.
- **Recording page, one line (180, line 111).** The reading page's layout. Header: back, book title (18px 600), small line "Line 4 of 44", pill "Done" (44px, brand fill, white 15px). Picture cropped from the top (radius 18, 12px side margin). Card on its lower part: 8px inset, radius 16, white at 96 percent, shadow `0 4px 12px rgba(43,37,64,.16)`. Card text: "The Wolf says:" (13.5px 600, brand), then the line in Andika 22px / 1.45 with curly quotes: "Little pig, little pig, let me come in!" Narration reads "Storyteller says:". No instruction, note or page counter. Button row: Skip (56px round, `#ECE7FF`, brand icon), Record (80px, red, 34px white mic), Next (56px); labels 12.5px 600. The header line counts pieces across the book, ticked characters only. Next is dim until the piece is recorded; Skip moves on and leaves the pre-made clip. The card never covers more than half the screen.
- **Recording (181, line 132).** Record becomes a red stop square (white 26px square, radius 6) with a soft pulse (1.4s, scale 1 to 1.45; reduced motion: no pulse, ring alone); its label is the running time ("0:03"). The card gets a 3 dp red ring. Skip and Next wait. Done or Back while recording stops and keeps the clip.
- **Recorded (182, line 153).** Buttons become Re-record, Play (80px, coral), Next (solid brand). Green chip on the label row: "Recorded" (24px high, radius 12, `#DFF5E3`, text `#2E7D32` 12px 700, 13px check), also when the parent comes back. Next goes to the next piece of a ticked character in story order, turning the picture when the page changes; on the last piece it reads "Finish" and returns to the book sheet.
- **Whole page (183, line 174).** Header line "Page 5 of 16 · part 1 of 2". The card holds the page text with no speaker label, Andika 21px / 1.45, padding 14 / 16, max height 332 (half of 667). Cut at a paragraph end; a single too-tall paragraph is cut at a sentence end. A short page is one piece and the header drops "part". No scrolling in the card.
- **Windows (184, line 192).** Card is a centred 640px column; the intro is a centred 560px column. Buttons Skip 56px, Record 76px, Next 56px, labels name the key: "Skip · S", "Record · R", "Next · →". Keys: R records and stops, Space plays a recorded piece, Right arrow Next, S Skip, Esc Back. Record has keyboard focus when a piece opens (focus ring). Key names show on Windows only.
- **Simplified:** status bars ("9:41"), phone outlines, the picture, and the wave bar heights are illustration; the stop-button pulse and wave fill are a small script in the mock.

**From the Batch 48 mock** (added work items):

- [ ] 9. (from the Batch 48 mock) "Your voice" shown (188, line 218). Shelf cover gets a white chip "You" with a red mic (26px high, radius 13, bottom-left, 5px in; the top-right keeps the more dot). In the book sheet the Record row becomes "Your voice" / "12 of 44 lines recorded" (min 56px, red mic 20px, chevron; whole-page books say "5 of 16 pages recorded"). The kids' shelf shows nothing. Removing a book that has recordings asks first: "Your recordings of this book will be deleted too."
- [ ] 10. (from the Batch 48 mock) Intro when recordings exist (189, line 244). Title "Your voice". Leads with "12 of 44 lines recorded", the mode name on the right, an 8px bar (`#ECE7FF`, red fill), and "The rest is read by Dino. Your voice stays on this phone." The three notes and mode cards hide behind "Change how you record" (40px, brand link). The checklist shows progress per name ("0 of 24"; "9 of 9" in green when done); ticks remembered. Buttons: "Listen and change" (52px) and coral "Record the rest" (starts at the first ticked piece with no recording). When everything ticked is done the coral button reads "Record again from the start".
- [ ] 11. (from the Batch 48 mock) Listen and change (190, line 273). The recording page walks only recorded pieces; header line "Your recordings · 2 of 12". Each piece plays by itself on open; a 28px wave fills as it plays; Play shows "Playing" and replays on tap. "Use Dino's voice" (13px 600, `#C8402A`) deletes that piece's recording so the pre-made audio plays again, with a 4 s "Undo" toast, no dialog. Swipe right or Back goes to the previous recorded piece. Whole-page books work piece by piece.
- [ ] 12. (from the Batch 48 mock) New take (191, line 295). After Re-record and stop, the chip reads "New take" (amber `#FFF0C2`, text `#8A5A00`) and the card adds two quiet buttons (44px, radius 22, `#F3EFE6`, text `#5a5470`): "Old take" (small play triangle, plays the previous take) and "Keep the old one" (drops the new take, back to 190). Next, Done or Back keeps the new take and deletes the old one. One take back only. The same applies in 182 for a piece recorded in the same session.
- [ ] 13. (from the Batch 48 mock) The intro wizard's record step is step 3 of 4 and opens the same intro page with "Try it" (177 caption).

**New strings (13 languages):** "Record your voice", "Your voice", the three notes above, "One character at a time", "Whole pages", "Who will you record?", "Storyteller", "<n> lines", "<n> of <m>", "Start recording", "Long pages come in parts, so the words always fit on the screen.", "<name> says:", "Line <n> of <m>", "Page <n> of <m> · part <k> of <j>", "Done", "Skip", "Record", "Next", "Finish", "Play", "Playing", "Re-record", "Recorded", "New take", "Old take", "Keep the old one", "Use Dino's voice", "Undo", "Change how you record", "Listen and change", "Record the rest", "Record again from the start", "Your recordings · <n> of <m>", "<n> of <m> lines recorded", "<n> of <m> pages recorded", "The rest is read by Dino. Your voice stays on this phone.", "You", "Your recordings of this book will be deleted too.", "Try it", and the Windows labels "Skip · S", "Record · R", "Next · →".

**Open point: buttons on a recorded piece.** Plan (Work 4, Decided in planning): Record, Skip, Next; a recorded piece adds Play and Re-record. Mock 182: exactly three buttons, Re-record, Play, Next (Re-record takes Skip's place). Default: follow the mock.
**Open point: Storyteller pieces.** Plan Work 1: "the lines of the ticked speakers". Mock 178 and its question 1: each run of narration between two spoken lines is one piece ("24 lines" for Storyteller counts those runs); the alternative is one per sentence. Default: one piece per run, as drawn.
**Open point: Done half-way.** Plan is silent. Mock question 3: Done keeps every piece recorded so far; the intro reopens on the same choice; Start goes to the first piece not yet recorded. Default: as drawn.
**Open point: counts on the checklist.** Mock question 2 (178): names show line counts. Default: keep them.
**Open point: pages vs parts in the sheet count.** Mock 188 says "5 of 16 pages recorded" for whole-page books, but a page can be two pieces. Default: a page counts as recorded when all its parts are.

## Verification

- `dotnet test tests/DinoReader.Tests`: split tests (character order, unticked speakers left out, paragraph cut,
  sentence cut, a short page as one piece) and model tests (old per-page clips still load).
- Android emulator at a small phone size and the iOS simulator on iPhone 8: the intro page shows once per Record tap;
  the recording page shows the whole piece with no instruction text; a long page splits at a paragraph end; reading
  back mixes recorded and pre-made pieces.
- Windows head: the same flow records and plays back.
