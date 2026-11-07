# Slice 286 — Dino: record your voice in each story language, and hear it when reading in that language

*In progress 2026-11-07.* Dino Reader only. Android, iOS and the Windows head the same, in the same slice.

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

Smaller defaults (not raised by the user; the user may override): each language keeps its own pieces, because sentence splits differ
per language. Delete and re-record touch only the picked language. The "use my recordings" switch stays one switch for the book.

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
