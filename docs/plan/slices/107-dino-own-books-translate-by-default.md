# Slice 107 — Dino: own books translate by default

*Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
reading language, one narrator, parent recordings, store filters" (2026-10-24) —
https://drive.google.com/file/d/1b2XPenCW2D4Kx1yx98js_LK4CRBSnnLf/view?usp=drivesdk (part 2, milestone 1).

**Batch:** 22 (Dino reads, shows and voices the grown-up's language)
**Depends on:** Slice 95 (translation)
**Touches:** `src/DinoReader.App/StoryWizardPage.xaml.cs`, `src/DinoReader.App/TextStep.xaml.cs`, `src/DinoReader.App/LanguageCard.cs`, `src/DinoReader.Core/StoryDraft.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

The user scanned *The Tale of Peter Rabbit* (English) on Android with the app in Farsi, and Dino read it
in English. Grounding found the cause, with no device needed: `StoryWizardPage.NewStoryAsync`
(`StoryWizardPage.xaml.cs:202-209`) builds the new `Story` without `TranslateToMyLanguage`, so it takes the
record default `false` (`Story.cs:64`). `StoryDraft` copies it, `StoryImport` saves it, and `TextStep`
seeds the switch from it. Only `BundledTranslations.Adopt` sets it true, and that runs only for shipped
books. So `StoryTranslator.Applies` is false for every own book and `StoryVoicing.VoiceAsync` returns
early (`StoryVoicing.cs:58-61`). The Android ML Kit engine is registered (`MobileSharedServices.Android.cs:18`)
and works. The fix is the default flag, not the engine.

## Decisions (2026-10-24, agreed with the user)

- **Decision 111: every new book translates to my language by default.** "Translate to my language" is
  on for every new story (own scans, typed or imported text), the same as shipped books. The per-story
  switch stays. On Windows and iOS, where no live engine translates an own book, the switch shows greyed
  out with one line, "Translation works on Android for now", instead of being hidden.

## Work

- [ ] 1. `NewStoryAsync` creates the story with `TranslateToMyLanguage = true`; `StoryDraft` for a new
  story keeps it. Existing stories are not changed. Core unit test: a new draft has the flag set.
- [ ] 2. `TextStep`/`LanguageCard`: on a platform with no live `ITranslator` and a story with no bundled
  translation for my language, show the switch disabled with the note (new resx string, all 14 locales,
  inside this slice's block).
- [ ] 3. Rule in `src/DinoReader.App/CLAUDE.md`: amend the Slice 95 bullet (new books default on; the
  no-engine note).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: app in Farsi, scan or type a short English page as a new book, open it: it reads in
  Farsi with no extra switch.
- Windows head: the same new book shows the greyed switch and the note, and reads English.
