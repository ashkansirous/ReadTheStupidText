# Slice 286 — Dino: record your voice in each story language, and hear it when reading in that language

*Built 2026-11-07, PR #885.* Dino Reader only. Android, iOS and the Windows head the same, in the same slice.

**Depends on:** Slices 111 and 279 (parent recordings and the recording flow this changes).
**Touches:** `src/DinoReader.Core/{StoryRecordings,Story,LanguagePick,StoryClipKey}.cs`, `src/DinoReader.Core/StoryPackage*.cs`
and `StoryBackup*.cs` (recording model in backups and restore merge), `src/DinoReader.App/{RecordIntroPage,RecordPage,RecordPieces,RecordViews,ReadingPage.xaml,ReadingSession,ReadingClips}.cs`,
`src/DinoReader.App/Resources/**` (strings, every app language), `src/DinoReader.App/Testing/DinoTestCommands.Record.cs`,
`tests/DinoReader.Tests/**`, `tools/emulator/**` (scripted record flow), `src/DinoReader.App/CLAUDE.md`, this file.
Overlap: the uncommitted `fix/dino-language-change-audio` worktree (2026-11-07) also edits `ReadingPage.xaml.cs`; sequence after it merges or keep this slice's edit there to the two `BookSwitchable` calls.

## Why

The user, 2026-11-07, after trying the app: "it does not ask you to select which language of the list you want to record
in. if I have 2 languages for my book, it should allow me recording in each separately. I should also be able to listen
to what I recorded for each language as well. 2- the text that is showing me to read from is only the english one, not
the farsi or the others 3- in the reading mode, it does not show the language picker for any books that I recorded my
audio for. book reader should work as normal, only if there is a recorded voice for a page/sentence in that language, it
should play that instead of playing the original sound".

Code check (origin/main, 2026-11-07): every recording is tied to `story.Language` with no choice.
- The record card and the piece splitter use it (`RecordPage.cs:399`, `RecordPieces.cs:88`), and the saved clip gets it (`RecordPage.cs:819`).
- The clip key is page plus sentence range, with no language (`StoryRecordings.cs:28-35`), so a second language would overwrite the first.
- `StoryRecordings.PlaysClip` (`:125`) plays a clip only when its language equals `story.Language`.
- `LanguagePick.BookSwitchable(hasChoice, recordings) => hasChoice && recordings == 0` (`LanguagePick.cs:9`, called at `ReadingPage.xaml.cs:232,277`) hides the language picker on any book that has a recording.

## Decisions locked with the user (2026-11-07)

1. **The recording language is picked on the first recording screen** (`RecordIntroPage`). A row shows **only the family's story
   languages** (`StoryLanguageSet.All`: the app language first, then the side languages). It defaults to the app language. The picked
   language drives everything after it: the text shown on the card is the book in that language, and so are the pieces it is
   cut into, the existing recordings, Play (listen back), re-record, delete and "Listen and change". Each language shows how much is
   recorded (e.g. "English: 8 of 12 pages, Farsi: none"). With one story language the row is not shown.
2. **Reading works as normal.** The language picker shows exactly as it does for a book with no recordings. When a recording exists for
   the page or sentence **in the language being read**, it plays. Everything else plays the book's own pre-made voice clips as today.
3. **Recording is offered only in the story languages.** A language outside them can't be picked. Story languages always have their
   books' audio downloaded, so there is no "audio missing" case to design.
4. **Recordings that already exist belong to the app's main language** (`StoryLanguageSet.Main`, the language the app is shown in), not
   to the book's stored `Language` field. Plan text and UI never speak of a book's "original language": there is no such product concept.

Each language keeps its own pieces, because sentence splits differ per language. Delete and re-record touch only the picked language.

#### Design (Dino Reader Batch 49 Recording per Language mock, section 192-196, added 2026-11-07)

Five frames on iPhone 8 (375 x 667), English app with Farsi as a side language; they change Batch 48 frames 178, 182, 188 and 189 and match this plan. All five are real screens, none is a labelling illustration. Fonts: Fredoka, Vazirmatn for Farsi. The mock's two review questions take the drawn answers: how you record (one character at a time or whole pages) and the ticks are kept **per language**, and the intro **always opens on the app language**.

- **C, Record intro, language row (frames 192, 193).** A `Record in` label (12.5px, 600, `#8a8399`), then one card per story language in a two-column grid, 8px gap. Card: 56px high, radius 16, white, picked card ringed 2.5px `#5B57E8`. Inside, a 30px round flag (ring 2px white plus 1px hairline) and two lines: the name in its own script (15px, 600; Vazirmatn for Farsi) and that language's count (12px: "12 of 44" in `#5a5470`, or "Nothing yet" in `#8a8399`). Counts are in the unit of that language's pieces (lines for "One character at a time"). Three languages make three narrower cards in one row; names never wrap ("Svenska" is the longest). With one story language the row is not shown and the page is frame 189. Tapping a card swaps the page below the row, never the row.
- **C, per-language page below the row (192, 193).** Frame 192 (recordings in English) is frame 189: "12 of 44 lines recorded" (15px, 600) with "One character at a time" (12.5px) on the right, an 8px bar (`#ECE7FF` track, `#E5484D` fill), "The rest is read by Dino. Your voice stays on this phone.", then `Who will you record?` (12.5px, 600, `#8a8399`) over rows of 42px with a 24px box (radius 7), name, count ("0 of 24", green `#2E7D32` "9 of 9"). Buttons "Listen and change" (outlined) and "Record the rest" (`#FF7A59`), 52px, two columns. Frame 193 (Farsi, nothing yet) is frame 178: the three notes, the two mode cards, the checklist with "N lines" counts, and a full-width "Start recording" (17px). Line counts are Farsi's own (Storyteller 21, not 24).
- **C, record line page (frame 194, from 180).** Header: title "The Three Little Pigs" (18px, 600), small line (12px, `#8a8399`) is a 14px round flag, the language name and the position: "Farsi · Line 4 of 41". Card text is the book's Farsi text, right to left (Vazirmatn 22px, line height 1.6, `dir=rtl`). The label "The Wolf says:" (13.5px, 600, `#5B57E8`), the Skip / Record / Next buttons and the layout stay in the app language and do not mirror. Recording, recorded, Listen and change (190) and New take (191) touch only that language's clips; whole pages are cut by that language's own lengths.
- **C, book sheet (frame 195, from 188).** The "Your voice" row's small line (12.5px, `#5a5470`) lists each language that has recordings, in story-language order, language names in the app language: "English 12 of 44 · Farsi 6 of 41". A language with nothing recorded is left out, so a book recorded in English only still reads "12 of 44 lines recorded". It fits one line with three languages and truncates past that. The "You" chip on the cover shows when any language has a recording.
- **D, reading (frame 196, exactly 89).** The language button is back on a book with recordings (a ring in the mock only points at it). The recorded line is highlighted as usual; sentences not recorded in Farsi play Dino's Farsi clips with no gap or change on screen. Switching to English plays the English takes; a piece recorded only in English plays Dino's Farsi clip when reading in Farsi. The child sees no sign of whose voice it is. Frame 88's note loses "Hidden for recorded books".

New strings (both locales, every app language): the `Record in` label already exists as `Record_Language`; the mock adds none beyond the book-sheet line template "{0} {1} of {2}" joined by " · " and the header line "{language} · Line {n} of {m}" / page variants. Language names come from `LanguageNames`.

**The mock decides (it always wins over this plan):**
- Speaker names in the checklist and the "The Wolf says:" label stay in the **app language**; only the words on the card are in the picked language.
- The record page's small header line is a 14px round flag, the language name and the position, shown when the family has more than one story language.
- How you record (one character at a time or whole pages) and the ticks are kept **per language**. The "use my recordings" switch stays one switch for the book.

Details the mock settles that the plan left open: the language row is a two-column card grid with flags, not a list with radios; counts on the cards are in the unit of that language's mode (lines or pages), not always pages; the intro opens on the app language; the book-sheet line lists every language with recordings.

## Work

- [x] **A. Core model, smallest end to end first.** The recording key includes the language (page + first/last sentence + language). Do
      the same for whole-page clips and the file name, e.g. `rec-<lang>-<page>-<first>-<last>-<hex>.m4a`. Old names and old keys still load.
      `PlaysClip(story, key, readingLanguage)` compares with the reading language. Add `StoryRecordings.Count(story, language)` and use
      it for the progress label.
- [x] **B. Migration.** On load, a recording with no language key, or one whose language came from the old `story.Language`, is filed
      under the app's main language at the moment of migration (decision 4). The migration runs once per book and is saved. Backups and restore
      merge (`StoryPackageReader`, Slice 230 order: stage, `UpdateAsync`, `.bak` rollback) carry the language per clip. A pre-286 backup
      restores into the main language.
- [x] **C. Recording screen.** Add a language row to `RecordIntroPage` (story languages only, app language picked by default, per-language
      progress). `RecordPage` and `RecordPieces` take the picked language: the card text comes from the book's translation in that language (the same
      source the reader shows, `StoryTranslator`), and pieces are cut and measured in that language. Listen back, re-record, delete and Undo act on
      that language's clips only. Right-to-left languages (Farsi, Arabic) lay out the card text right to left, following the existing RTL rules
      in `src/DinoReader.App/CLAUDE.md`.
- [x] **D. Reading.** Drop the recordings condition from `LanguagePick.BookSwitchable`, so the picker shows whenever the book has a choice.
      `ReadingSession` and `ReadingClips` pass the reading language to `PlaysClip`. Switching language mid-book switches which recordings play. The
      page shows that language's text as usual.
- [x] **E. Strings** for the language row and progress label, in every app language.
- [x] **F. Tests.** Core unit tests cover:
      - keys with language, and two languages on the same piece not overwriting each other;
      - old keys and file names migrating to the main language;
      - `PlaysClip` by reading language, and the `BookSwitchable` change;
      - restore of old and new backups.

      Extend the scripted emulator record flow with two story languages: record one piece in each, listen back, read in each and hear the right clip.
- [x] **G. Docs.** Update the parent-recordings rule in `src/DinoReader.App/CLAUDE.md`: drop "matches the story language", and say clips are per story
      language, keyed by language, and played in the reading language.
- [x] **H. Design alignment (Batch 49 frames 192-196).** "Record in" label and two/three-column language cards with the shared `FlagRing` flag and each language's own count
      ("12 of 44", `Record_LanguagePages` now "{0} of {1}" in all 15 languages; "Nothing yet"); the record mode and ticks kept per language (`RecordChoices` key carries the language, the old
      per-book choice carries over to the app's main language); speaker labels and checklist names stay in the app language (`WithSpeakerNames` removed); record header small line with a 14 px flag
      and "<language> · Line 4 of 41" (`GrownUpHeader.SetSubtitleFlag`); book sheet "Your voice" line lists every language with recordings ("English 12 of 44 · Farsi 6 of 41", names from the new
      `StoryLanguages_NameMap` string in all 15 languages), one line truncated; "Who will you record?" as the 12.5 px muted heading.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Android emulator, scripted, with story languages English plus Farsi:
  - the intro shows both languages;
  - the Farsi card shows Farsi text;
  - a Farsi take plays back;
  - the English recordings are still there;
  - the reading screen shows the language picker;
  - reading in Farsi plays the Farsi take on that piece and pre-made clips elsewhere;
  - reading in English plays the English takes.
- Upgrade check: a book recorded before this slice still plays its recordings in the app's main language after updating.
- iOS: the same flow compiles and runs on the simulator scripted flow if one exists, otherwise it goes to *Not yet verified*. Windows head: covered by
  shared code; the VM runs only if Windows-specific record code changes.

## Result

*Changed while building:*
- Recordings are keyed by story language (file names `rec-<lang>-...`). Old clips migrate to the app's main language, and only once the main language is known.
- Record intro, record header, book sheet and reading follow design Batch 49 frames 192-196 (item H): language cards with each language's own counts, record mode and ticks per language, header flag plus language line, sheet line "English n of m · Farsi n of m", and the reading language picker kept, with a recording played only in the reading language.
- Proven bug fixed: a header flag added after the layout was measured 0x0 (evidence `tmp/diag/flag-measure.txt`). The header now keeps one `FlagRing` and changes its country.
- Android, iOS and Windows heads build.
- Emulator (Android, emulator-5554): 2026-11-07 10:47 record-per-character and record-page-shows-words pass; 2026-11-07 11:01 (bcf9fec8) record-per-language and wizard-layout-record-large-font pass. Logcat shows reading=en and reading=fa clips; screenshots match frames 192-195.

*From review:* files `D:\ReadTheStupidText-worktrees\reviews\s286-{high,medium,check1..check6}.md`, outside the repo (outcomes below are from the coordinator's notes, not from the files):
- high: fixed 1, 3, 4, 5, 6, 7, 9, 10 (plus a speaker-name change, later reverted to match the design). Skipped 2 (clips of a language removed from the set stay on disk and return if it is re-added; no data lost) and 8 (style).
- medium: fixed 2, 4, 5, 6, 7, 9, 10. Skipped 1 (decision 4: old clips go to the app's main language), 3 (same as high 2), 8 (intro opens in the app language and one "use recordings" switch: agreed, matches the design).
- check1: fixed 1-7 and 12. Skipped 8 (as high 2), 9 (harmless legacy key), 10 and 11 (match design frames 194/195).
- check2: fixed 1, 2, 4, 7. Skipped 3, 5, 6 (small speed points, nothing visible).
- check3: its 3 low findings accepted.
- check4: fixed 1, 2, 3, 6, 7; 4 resolved by the margin change in 3; lows 5, 8, 9, 10 accepted.
- check5: fixed 1, 2, 3, 4, 5, 10. Skipped 6 (follows 1), 7 (Dino never mirrors), 8 (alert titles keep Record_Title), 9 (one small view per header).
- check6: its 2 low findings accepted.

#### For CLAUDE.md

- In `src/DinoReader.App/CLAUDE.md`: parent recordings are per story language (key and file name carry the language) and play only when the reading language matches. Old clips file under the app's main language, only once it is known. (Slice 286: docs/plan/slices/286-dino-recordings-per-story-language.md)
- In `src/DinoReader.App/CLAUDE.md`: a view added to a row that is already laid out may never be measured (0x0). Keep one view and change its content, as `GrownUpHeader` does with its `FlagRing`. (Slice 286: docs/plan/slices/286-dino-recordings-per-story-language.md)

#### Not yet verified

- iOS device or simulator: the record-per-language flow was not run.
- Windows head on a real screen: centring of the record intro on a wide window.
- The 14 hand-written language-name strings (`StoryLanguages_NameMap`) and the `Record_LanguageUnavailable` / `Record_LanguageRetry` translations have not been checked by native speakers.
- Emulator store audio had to be seeded because the store was unreachable in the test setup, so the real download path was not covered.
