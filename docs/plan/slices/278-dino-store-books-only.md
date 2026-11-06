# Slice 278 — Dino: store books only (no own books, no scanning, no translation, no voice engine, no voice pickers)

*Part A in progress (wave 2026-11-06), draft PR; parts B and C planned.* Dino Reader only (Android, iOS and the Windows head, the same in the same parts). **Read The
Stupid Text is not touched**, and neither is any shared code it uses (`src/ReadTheStupidText.Mobile.Shared`,
`src/ReadTheStupidText.Infrastructure`, `src/ReadTheStupidText.Documents`, `src/ReadTheStupidText.Mobile`). Dino
simply stops calling the shared pieces it no longer needs.

**Depends on:** Slice 277 for part B (the whole store must have complete pre-made audio before the voice engine goes).
**Touches, part A (own books out):** `src/DinoReader.App/{StoryWizardPage,PagesStep,TextStep,CoverStep,PageEditorPage}.xaml*`,
`src/DinoReader.App/{PageTextPage,RegionPicker,RegionOverlay,CoverPicker,StoryImport,OwnBookTranslation,PickedPhotoCache,ScanPages,ZoomablePage,TranslationPackPrompt,TranslationPackStore,StoryTranslator,LocalizedPackStatus,BookOrigins}.cs`,
`src/DinoReader.App/{IntroWizardPage,IntroWizardPage.Art,IntroState,StoryActions,BookSheet,GalleryPage.xaml,MauiProgram,UsageEvent}.cs*`,
`src/DinoReader.App/Resources/**` (strings), `src/DinoReader.Core/ScanLayout*.cs` (only if no store book uses it),
`tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.Core/CLAUDE.md`,
`src/DinoReader.App/StarterStories.cs`, `src/DinoReader.App/BookSheetPlan*.cs`, `src/DinoReader.App/OwnBooksCleanup.cs` (new, A3),
`.maestro/dino/**` (only flows that drive the removed wizard, Add pill or scan; not `premade-audio-past-roar.yaml` or
`usage-fallback-swallowed.yaml`), `tools/desktop-vm/checks/**` (only Dino checks that drive removed screens).
Must not edit `src/DinoReader.App/ReadingSession.cs`, `PlayerHandOff.cs` or `AudioPackStore.cs` (Slices 271 and 275 edit them in the same wave).

**Design for A3 (coordinator, claim 2026-11-06).** One owner: a new `OwnBooksCleanup` run once from app start-up,
after the library loads and before the shelf shows. The rule for "own book" is the existing one: `BookOrigins.Of`
(`BookOrigins.cs:9-14`, from `story.SourceId` via `StarterStories.IsCatalogueId`); store books and the four former
starters count as store books. The decision (which books go) is a pure, unit-tested function over the loaded
stories. States: not done / done, kept in one preference key (`OwnBooksCleanupDone`, a named constant). Interrupts:
killed or crashed mid-delete, the key is not set yet, so the next start runs it again over whatever is left
(deleting an already-deleted book is a no-op, so it is idempotent); a failure on one book is reported with a
`FallbackArea.` event and the rest still go, and the key is set only when no book failed. Offline does not matter
(local files only). Logs one line with the count deleted. Nothing to cancel (no UI).
**Touches, part B (voice engine and voice pickers out):** `src/DinoReader.App/{StoryVoicing,SpeechStartup,StorytellerDefault,StorytellerConsent,StorytellerViews,VoicePreview,NarratorPage,NarratorChoice,NarratorRecast,CastPage,CastRows,CastViews,CastText,CastFlavourMenu,ReadingClips,ReadingSession,SpokenLanguage,FirstRunPage.xaml,FirstBooksPage*,SettingsPage,SettingsRows,BookSheet,StoryActions,MauiProgram}.cs*`,
`src/DinoReader.App/DinoReader.App.csproj`, `src/DinoReader.App/Platforms/**`, `src/DinoReader.App/Resources/**`,
`src/DinoReader.Core/**` (cast and voice-choice types only), `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`.
**Touches, part C (design copy):** `docs/design/dino-reader/**`.
Parts A and B share hub files (`MauiProgram.cs`, `StoryActions.cs`, `BookSheet.cs`, resources): build them one after
the other, not in the same wave.

## Why

The user, 2026-11-06: "remove the path to add your own books. I dont care about it. that's not the main product
anymore. we dont need any of the shit that comes with that. no ml kit, not scanning no translation no nothing. even
remove it from the design. no tts engine. nothing. as a result, the wizard does not need it either and there is no
choice to change charactures voices. all should be cleaned up from the app." And: "I said everything but I was
explicit about dino reader. Do not fucking touch the RTST!!!" The user plans to move Dino Reader to its own
repository later.

## Decisions (the user, 2026-11-06)

- Books are only store books. A book plays its pre-made audio, or the family's own recordings (Slice 279). No phone
  voice ever reads anything, and there is no fallback when a clip is missing (Slice 277 guarantees there is none).
- Books a family added themselves are **deleted on update** (testing phase; "I dont fucking care"). The user asked
  how many people did this. Live usage counts (`dino_usage`, last 90 days, test rows left out, read 2026-11-06):
  own books were added in **1 app session** (about 9 books, all typed in the story wizard; no scans or file
  imports), and own-voice recordings were made in 2 sessions. The data has no person or install id, only a random
  id per app launch, so this is launches, not people; TestFlight counts (`dino_usage_beta`) were not read.
- The setup wizard loses its "add your own book" step. Nobody can choose a character's or the storyteller's voice.
- The Windows head loses the voice engine too, like Android and iOS.
- Removed from the design copy as well.

## Facts from the code (origin/main, 2026-11-06)

- Own books start at `StoryActions.EditAsync` (the story wizard), the intro wizard's "add" step and the Add pill.
- Machine translation: `StoryTranslator` uses each store book's bundled translations first and ML Kit only when one
  is missing. Store books carry their own translations, so only bundled ones stay.
- Voices: Kokoro (`kokoro-en`), Supertonic and `piper-fa` packs, system voice readers, `StorytellerDefault`
  downloads the English voice on Wi-Fi; the first-run flow waits for a voice download.
- The intro step names `Add` and `Store` are kept for old saved states and usage-event names (`FromLegacy`,
  `FromSixSteps`); the Worker's allow-list is left alone.

## Work

**Part A — own books out**

- [ ] A1. Remove the story wizard and every page and service only it uses (list in Touches), the Add pill, the
  intro wizard's "add" step (remap saved states that point at it to the next step), and their strings.
- [ ] A2. Remove machine translation: `StoryTranslator` keeps only bundled translations; ML Kit, translation packs
  and their prompts go. A book is offered only in languages it has a bundled translation for.
- [ ] A3. On the first start after the update, delete every book that did not come from the store (with its
  pictures and recordings) and log one line with the count. Store books and their recordings stay.
- [ ] A4. Dino no longer registers or calls the scanner, OCR, PDF/Word extractors or translator from the shared
  projects. Do not change those projects. Check whether the ML Kit packages still land in the Dino Android and iOS
  packages through `Mobile.Shared`; if they do, write it into *Outstanding work* for the planned repo split, without
  editing shared code.
- [ ] A5. Tests updated (wizard, import and translation tests removed; a test for A3's cleanup and A1's remap).

**Part B — voice engine and voice pickers out**

- [ ] B1. Reading plays only pre-made clips and recordings: remove the device-voice path from `StoryVoicing` /
  `ReadingClips` / `ReadingSession`, and every speech engine, voice pack download and voice consent from Dino
  (Android, iOS, Windows head).
- [ ] B2. Remove the cast page, narrator page, voice preview and their entries in the book sheet and settings; drop
  narrator recasting. A stored cast in a saved book is ignored on load (no crash on old files).
- [ ] B3. First run no longer waits for a voice download: language, first books, then the shelf (the books' audio
  packs still download with the books, as today).
- [ ] B4. Remove the sherpa-onnx and voice-pack references from the Dino project only (`DinoReader.App.csproj`);
  never from shared projects (same check and *Outstanding work* note as A4 if they still come in through them).
- [ ] B5. Tests updated; a test that reading a page with no clip and no recording never asks for a speech engine.

**Part C — design**

- [ ] C1. Remove the add-book, scan, page-editor, translation, voice download, cast and narrator frames from the
  design copy in `docs/design/dino-reader/`, and the wizard's "add" step. (The new recording frames come with Slice 279.)

#### Design (Dino Reader Batch 48 mock, frames 177, 185, 186, added 2026-11-06)

Three frames in `docs/design/dino-reader/Dino Reader Batch 48 Store Only and Recording.dc.html` (lines 34, 325, 349). The mock mostly agrees with the plan; three Open points are at the end.

- **A1 / B2, book sheet (177, line 34, 387 x 679).** Frame 172 with Cast gone. Rows: header (44 x 58 cover, "The Three Little Pigs", "Traditional · 16 pages"); "Read now" (60px); "Record your voice" (48px, outlined: 2px `#c9c0f5` border, `#5B57E8` text, red `#E5484D` mic icon 18px, full row); "Reorder" (48px); "Remove from the shelf" (46px). "From the Store" is gone: the line under the title is credit and length, because every book is a store book. Record opens the Slice 279 intro every time (its frame 178).
- **A1, Grown-ups' corner (185, line 325, 360 x 760).** Frame 175 with "Add a book" gone. One full-row button: 52px, radius 18, fill `#5B57E8`, white 15px 600, shop icon + "Get more books". Below it the book list (Good Enough, The Three Little Pigs, The Gingerbread Man, The Tortoise and the Hare, 30px covers) and "Back to my child's shelf" (56px). Intro line "Only grown-ups see this. Your child's shelf is one tap away." The page title still says "Store".
- **A3 (185 caption).** Own books are deleted on the first start after the update with no message to the user (the plan's one log line stays).
- **C1, what leaves the design copy (186, line 349, a list, not a frame).** Own books: v2 add-story, Batch 17-18 frames 31-35, Batch 47 frames 170-171, corner "Add a book" (98, 175), the intro add step. Translation: Batch 22-23 frames 37 and 41; Batch 37 story-languages copy that mentions machine translation. Voices: Batch 16 frame 17, Batch 22-23 frames 48-49, Batch 24 frames 50-51, frame 169's reading-voice line and "Dino's reading voice will read it". Cast and Narrator: Batch 22-23 frames 38-40 and 42-43, Batch 24 frames 52-55, Batch 31 frame 69, Cast in frames 100 and 172. Old Record: Batch 22-23 frames 44-46 and 106's words and note (replaced by 177-184, Slice 279).
- **B3, first run (186).** Language, first books (163-166), "Ready to read" (167). No voice download screen.
- **A1, intro wizard (186).** Two places, read, record, languages: counters read "of 4" (record is step 3 of 4). Saved add steps map to languages.
- **A2, story languages (frame 169, 186).** The list shows only languages every shelf book has; the audio card stays.
- **Simplified:** 186 is a text list; status bars and phone outlines are illustration.
- **Strings (13 languages):** new "Get more books"; remove every add-book, scan, translation, voice-download, cast and narrator string.

**Open point: remap target of the intro's add step.** Plan A1: saved states map "to the next step". Mock 186: they map to languages. Default: languages.
**Open point: translation languages.** Plan A2: a book is offered only in languages it has a bundled translation for. Mock 186 (frame 169): the list shows languages every shelf book has. Default: per book when reading, and the story-languages list as the mock says.
**Open point: removing frames.** Plan C1 removes frames from the design copy. Mock 186: the older batch files keep their frames and get a "retired" line in the screen map; only the repo copy drops them. Default: leave the older `.dc.html` files verbatim and add a "retired by Slice 278" line per frame range to the README screen map.

## Verification

- `dotnet test tests/DinoReader.Tests` passes; `dotnet test tests/ReadTheStupidText.Tests` passes unchanged and
  `git diff origin/main --stat` shows no file outside Dino's folders, tests and docs.
- Android emulator and iOS: a store book plays start to end with pre-made audio; no "add", "cast", "narrator" or
  "voice" control appears anywhere; an install that had an own book shows it gone after the update.
- Windows head: a store book plays with pre-made audio.
