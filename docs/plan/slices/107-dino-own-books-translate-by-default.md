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

#### Design (Dino Reader Batch 22-23 mock, frame 37, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 22-23.dc.html`, frame 37 (lines 27-48). One real-UI frame (360x760 phone frame, 6px `#2b2540` border, radius 34.) plus a small side illustration of the disabled state. The mock agrees with the plan on the default and the greyed switch, and adds the second line under the switch.

- **Text step (37).** Page `#F3EFE6`. Header: back arrow 44x44, title "Text" 20/600, subtitle "The Tale of Peter Rabbit · scanned · 4 pages" 12 `#8a8399`. Content padding 0/14, gap 10.
- **Language card.** White, radius 18, padding 14, gap 12. Row 1: "Book language" 14 `#5a5470` and a picker pill (36 high, radius 10, `#F3EFE6`, "English" 14/600, chevron). Hairline `#efe9dd`. Row 2: the switch.
- **Translate switch (107-2).** Label "Translate to my language" 15/600; **second line** "Reads and shows it in فارسی" 12.5 `#8a8399` (the language name in Vazirmatn, `dir=rtl`). Switch 52x32, radius 16, **on** = `#5B57E8` with a 26 white knob at the right. "Reads and shows" is the Slice 110 wording: the screen shows the translation too.
- **Disabled state (side illustration, not a frame of the app).** Same card, label `#a39cb3`, line "Translation works on Android for now" 12.5 `#5a5470`, switch off and greyed (`#e6e0d4`, knob left). Shown on Windows and iOS for a book with no bundled translation; never hidden. Shipped and store books still translate there.
- **Page cards.** White, radius 16, padding 12/14: "PAGE 1" 12/700 `#8a8399`, text Andika 14, line-height 1.45. Primary button "Save story" 52 high, radius 26, `#FF7A59`, shadow `0 5px 0 #E2583A`.

Changed to match the mock (2026-10-24): work item 2 now also covers the new second line on the enabled switch (it names the target language, so "my language" is clear). Icons in the mock are inline SVG strokes (2.2 wide, `#5B57E8`), not Phosphor names; the app uses its own `IconKind` set.

New strings (all 14 locales, in this slice's block): the second line "Reads and shows it in {0}" (with the language name); "Translation works on Android for now".

## Work

- [ ] 1. `NewStoryAsync` creates the story with `TranslateToMyLanguage = true`; `StoryDraft` for a new
  story keeps it. Existing stories are not changed. Core unit test: a new draft has the flag set.
- [ ] 2. `TextStep`/`LanguageCard`: the enabled switch gets a second line naming the target language ("Reads and shows it in {language}", frame 37; new string). On a platform with no live `ITranslator` and a story with no bundled
  translation for my language, show the switch disabled with the note (new resx string, all 14 locales,
  inside this slice's block).
- [ ] 3. Rule in `src/DinoReader.App/CLAUDE.md`: amend the Slice 95 bullet (new books default on; the
  no-engine note).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: app in Farsi, scan or type a short English page as a new book, open it: it reads in
  Farsi with no extra switch.
- Windows head: the same new book shows the greyed switch and the note, and reads English.
