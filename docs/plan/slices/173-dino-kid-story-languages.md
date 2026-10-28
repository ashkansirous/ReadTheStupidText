# Slice 173 — Dino: up to three story languages, switched by the kid

*Planned 2026-10-28.* Dino Reader only (Android, iOS and the Windows head).

**Batch:** 37 (Dino settings, 2026-10-28; Decision 168). Builds **after Slice 172** (it needs the Settings page).
**Touches:** `src/DinoReader.App/StoryLanguages.cs` (new), `src/DinoReader.App/StoryLanguagesPage.cs` (new), `src/DinoReader.App/StoryLanguageButton.cs` (new), `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.App/IntroWizardPage.Art.cs`, `src/DinoReader.App/SettingsPage.cs`, `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.App/ReadingPage.xaml`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/StoryTranslator.cs`, `src/DinoReader.App/GalleryPage.xaml.cs` (titles stay in the main language), `src/DinoReader.App/UsageCounter.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.Core/StoryLanguageSet.cs` (new, pure rules), `worker/events.ts`, `worker/*.test.ts`, `tools/dino-usage-report.ps1`, `site/dino/privacy.html`, `STORE.md`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

The user, 2026-10-28: bilingual families want the kid to read the same books in two or three languages.
The parent picks the app language (theirs) at first run, then at the end of the intro picks up to two more
**story languages** for the child. The kid switches between them while reading. The parents' screens and
the store always stay in the main (app) language.

What exists (checked 2026-10-28): reading follows the app language only (`StoryTranslator.MyLanguage`,
`ReadingPage.xaml.cs:139,266`); every starter and store book carries all languages (`Story.Translations`,
Slice 95); own books are translated on the phone with ML Kit (about 30 MB per language, Android and iOS,
not Windows; Slices 159-160); narrator and cast voices are already saved per language (Slices 108, 109, 172);
there is no in-reader language switch. Usage counts already have `reading_language_changed` and a
`Language` field (`UsageEvent.cs:17,124`).

## Decisions locked with the user (Decision 168)

1. **The main language is the app language** and is always a story language; the parent cannot remove it.
   Up to **two side languages**, so three at most.
2. **Where it's set:** the last step of the intro wizard (Slice 155) asks "Which languages should stories be
   in?", main language ticked and locked; and a **Story languages** row in Settings (Slice 172).
3. **Downloads:** each chosen side language's **voice pack** downloads right after the pick, with progress and
   Cancel (the 500 ms loading rule). The **ML Kit translation pack** for a side language downloads only when
   the parent first saves an own book (the existing `TranslationPackPrompt`, extended to every story language).
4. **The kid's button:** a flag button in the reading page header, shown only when two or three story languages
   are set. One pick for **all books**, remembered across books and restarts. It switches the story text, the
   voice and the book title on the reading page; the reading page's buttons and messages, the shelf, the store
   and every grown-up screen stay in the main language.
5. **Books that can only be read one way hide the button:** a recorded book (the recording always plays), a book
   with "Show original text" on, and own books on Windows (no translator).
6. **Changing the main language later:** the new main language is always in the set. If it was a side language,
   the two swap. If it's new and two sides are already set, the parent is asked which side to drop; otherwise
   the old main stays as a side.
7. **Existing installs** start with the main language only (no button) until a parent adds one.
8. **New telemetry** (closed-list fields only, production builds only; `production-only-analytics` applies):
   - `story_languages_set`: when the parent saves the set; `Language` = main, detail = the count (`1`/`2`/`3`) and
     where it was set (`intro`/`settings`). One extra `story_language_added` per side language (`Language` = it), so
     the report can rank side languages without a list field.
   - `story_language_switched`: the kid used the reading-page button; `Language` = the new language; source/book as
     for `book_opened` (shelf id or `own`).
   - `reading_language_changed` stays for the parent's main-language change.
   - `voice_pack_download`: `Language`, detail `done`/`failed`/`cancelled` (side-language downloads are new waits
     worth seeing).
   - `book_opened`/`book_finished` keep `Language` = the **language it was read in** (the kid's pick), and gain no
     new field. Note this in the report so older rows (book's own language) aren't compared blindly.
   - Worker allow-list, report script, Dino privacy page and the store privacy answers updated in the same slice.

## Work

- [ ] **First end-to-end piece:** pure `StoryLanguageSet` (main + up to two sides, the swap/ask rules) with unit tests;
      a Story languages row in Settings that lets the parent tick side languages (voice pack downloads with progress);
      the reading-page flag button switching text, voice and title for starter/store books. Android and iOS.
- [ ] Intro wizard: a final "Story languages" step before the hand-over, with Skip (Skip = main only).
- [ ] Hide the button for recorded books, "Show original text" books and own books on Windows.
- [ ] Own books: `TranslationPackPrompt` offers every story language's pack; the reader falls back to the book's own
      language (button hidden for the missing language) when a pack isn't there.
- [ ] Main-language change in Settings: swap, or ask which side to drop.
- [ ] Telemetry: the four events above in `UsageEvent.cs`, `UsageCounter`, `worker/events.ts` (+ tests), the report
      script, `site/dino/privacy.html`, `STORE.md` notes for Play Data safety / App Store privacy.
- [ ] Strings (14 languages, 13 drafts): wizard step, Settings row and page, the "which one to drop" question, the
      button's accessibility label.
- [ ] Windows head: the Settings row and the button for starter/store books.
- [ ] `src/DinoReader.App/CLAUDE.md`: story languages vs the app language; grown-up screens and store never follow the
      kid's pick.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (the set rules, event shapes) and the worker tests
  (`npm test`) green.
- Android (headless emulator, `device-test`): first run in English, add Farsi in the wizard (voice downloads with
  progress); open a starter book, the flag switches to Farsi text and voice and back; restart, the pick is kept;
  shelf, store and Settings stay English; a recorded book shows no button; with one language there is no button;
  change the main language to Farsi: English becomes the side language.
- iOS: compile and the same flow on a simulator or TestFlight build.
- Windows head: Settings row, button on a starter book, no button on an own book.
- Telemetry: the new events reach the dev endpoint only from a production build path; tests block the endpoint.
- Native read of the new strings (13 drafts) and the store privacy forms go to Outstanding work.
