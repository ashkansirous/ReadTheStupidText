# Slice 109 — Dino: one narrator per language, for every book

*In progress (wave 2026-10-24), draft PR.* *Planned 2026-10-24*, from the phone scope "Dino Reader - translated text on screen, voices in the
reading language, one narrator, parent recordings, store filters" (2026-10-24) —
https://drive.google.com/file/d/1b2XPenCW2D4Kx1yx98js_LK4CRBSnnLf/view?usp=drivesdk (part 4, milestone 4).

**Batch:** 22
**Depends on:** Slice 108 (both rewrite the cast and narrator code; run after it)
**Touches:** `src/DinoReader.Core/Story.cs`, `src/DinoReader.Core/StoryJson.cs`, `src/DinoReader.Core/StoryDraft.cs`, `src/DinoReader.Core/CastEditor.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.App/StoryImport.cs`, `src/DinoReader.App/StoryActions.cs`, `src/DinoReader.App/TextStep.xaml.cs`, `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/NarratorPage.cs` (new), `src/DinoReader.App/NarratorChoice.cs` (new), `src/DinoReader.App/NarratorRecast.cs` (new), `src/DinoReader.Core/NarratorSetting.cs` (new), `src/DinoReader.App/CastRows.cs`, `src/DinoReader.App/CastText.cs`, `src/DinoReader.App/IconView.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

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

#### Design (Dino Reader Batch 22-23 mock, frames 38 and 39, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 22-23.dc.html`, frames 38 (lines 50-80) and 39 (lines 82-104); the cast-page link is in frame 40 (see Slice 108). Two real-UI frames (360x760 phone frame, 6px `#2b2540` border, radius 34.). Icons in the mock are inline SVG strokes (2.2 wide, `#5B57E8`), not Phosphor names; the app uses its own `IconKind` set. The mock differs from the plan in how the grown-up row looks and in who gets a card on the Narrator page.

- **Grown-up gallery (38, 109-3).** Page `#FFF6E6`. Header: app icon 40 (radius 11), "My stories" 22/600, "Grown-up mode" 12.5/600 `#5B57E8`, and the **Language** pill (44 high, radius 22, white, 2px `#c9c0f5` ring, globe icon, "English") as the header's right side. Below, **three equal buttons** in a 3-column grid, gap 8: "Free stories", "Narrator" (new), "Backup". Each is 64 high, radius 16, white, 2px `#c9c0f5` ring, icon (20) above the label 14/600 `#5B57E8`. Narrator's icon is a person with sound waves; the dashed outline in the frame only marks it as new.
- **Tile record button (Slice 111, shown here).** Each story tile's action bar gains a red microphone (44x44, white, radius 12) beside Edit and Cast; a 36 circle `#E5484D` with a mic is also drawn on the tile. Screen reader: "Record Good Enough in your voice". Done button 56 high, radius 28, `#FF7A59`.
- **Narrator page (39).** Page `#F3EFE6`. Header: back arrow, "Narrator" 20/600, subtitle "The storyteller voice for every book" 12 `#8a8399`. One white card per language (radius 18): language name 16/600, a voice picker (44 high, radius 12, `#F3EFE6`, 14.5, chevron) and a 44 sample circle `#ECE7FF` with a play icon. **My language is first and ringed** (2px `#c9c0f5`) with a badge "MY LANGUAGE" (11/700 `#5B57E8` on `#ECE7FF`, radius 6). Cards shown: English (Demiurge), فارسی (Ganji Adabi, RTL name), Español (Momonga). Voice names are the placeholders of today's `NarratorFor` picks.
- **Footnote under the cards.** "If a character in a book has the narrator's voice, Dino gives that character another one." 13 `#5a5470`, line-height 1.45.
- **Behaviour (note).** Opened from Narrator (38) or the cast screen's "Change for all books" link (40). **One card per language with an installed voice pack**, my language first. A change applies to starter, store and own books at once; clashing characters are re-cast quietly. The sample button speaks a short line in that voice.

Changed to match the mock (2026-10-24): the Narrator page lists only languages with an installed voice pack (the plan said "each language"); the gallery's grown-up row is three equal icon-above-label buttons with Language kept as the header pill (the plan said the button sits "beside Free stories, Backup and Language"); a new `IconKind` is needed unless one already fits (a person with sound waves).

New strings (all 14 locales): "Narrator" (button and page title), "The storyteller voice for every book", "MY LANGUAGE", the footnote, "Change for all books", and the screen-reader label for the sample button.

## Work

- [x] 1. `NarratorChoice`: per-language setting (named key constant), default from `NarratorFor`; unit-
  testable resolution in Core if it holds logic.
- [x] 2. Replace every `story.NarratorVoiceId` read with `NarratorChoice` for the reading language;
  `StoryJson` stays backward compatible and no longer treats the field as required.
- [x] 3. `NarratorPage` (one card per language with an installed voice pack, my language first with the "MY LANGUAGE" badge, sample button, footnote; frame 39) and the grown-up gallery's three equal buttons Free stories / Narrator / Backup with Language staying as the header pill (frame 38). New `IconKind` for Narrator (a person with sound waves) unless one fits.
- [x] 4. Cast page: read-only narrator row with the "Change for all books" link; remove the per-book picker.
  Changing the narrator re-casts clashing characters in every story's cast for that language.
- [x] 5. Upgrade tests: an old story, an old `.dino` and an old backup still open and read.
- [x] 6. Rule written below under *For CLAUDE.md* (`update-docs` lifts it into `src/DinoReader.App/CLAUDE.md`; replaces the Slice 65 per-story narrator line).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: grown-up mode, Narrator, change the English narrator; open a starter, a store book and
  an own book: all three use it; the cast page shows it with the link and no picker.

## Built (2026-10-24)

`NarratorSetting.Resolve` (Core, tested) picks the stored voice while the pack has it, else the default. App: `NarratorChoice` (preference `dino.narrator.<lang>`), `StoryVoicing.NarratorOf/SetNarrator/InstalledLanguages` (the old `NarratorIn`, `NarratorChoicesIn`, `StoryNarratorFor` and every `narratorVoiceId` parameter are gone), `NarratorRecast` (re-casts clashing characters in every language of a story), `NarratorPage` (frame 39) and the gallery's three equal buttons with the globe on the Language pill (frame 38). The cast page's narrator row is read-only with "Change for all books"; coming back from the Narrator page, or opening a cast, re-casts a clash quietly. `Story.NarratorVoiceId` stays in the record for old files only and is never read for voicing; `StoryDraft` lost its narrator and `Story.NewOwnBook` its parameter. For compatibility `story.json` still writes `NarratorVoiceId` as the story-language narrator via `StoryJson.LegacyNarratorOf`, so older app builds can read new packages and backups. A `VoicePreview` helper plays the sample.

*Built 2026-10-24, not yet verified on the emulator.*

*Changed while building:* Touches grew by `NarratorRecast.cs`, `NarratorSetting.cs` (Core), `CastRows.cs`, `CastText.cs`, `IconView.cs` (`IconKind.Narrator`). My language always gets a card even before its pack is installed (its voices are the Supertonic styles then). The Narrator page's badge now matches the mock's 11 (review). The old "N characters changed voice" notice after a narrator change is gone (the mock says "quietly"), and its now-dead `VoiceChanged` strings were removed. The plan said new stories don't write `narratorVoiceId`; they do, as a legacy value for older builds (review).

*From review:* high 1-10 applied: the legacy `NarratorVoiceId` write above; Narrator page lists only installed-pack languages per the mock; badge size 11; `VoicePreview` helper; 44 tap target; dead strings removed. Medium 1-5, 7, 8 applied; 6 declined (not a bug); 9 out of scope.

#### Not yet verified

- Emulator: Narrator page matches frame 39 (installed-pack languages only); grown-up mode, change the English narrator; a starter, a store book and an own book all use it; the cast page shows it with the link and no picker.
- Emulator: changing the Farsi narrator quietly re-casts all Farsi books; an old backup restores.
- An older app build opens a package made by this build.
- Native check of the Slice 109 strings in all 14 locales (machine-drafted).

## For CLAUDE.md

- App: the narrator is one voice per language for every book (`NarratorChoice` preference, resolved by `StoryVoicing.NarratorOf(language)` against the language's pack, default `NarratorFor`); never read `Story.NarratorVoiceId` for voicing (`StoryJson.LegacyNarratorOf` still writes it only so older builds can open new packages and backups). The cast screen shows it read-only; it is chosen on `NarratorPage` (grown-up gallery, Narrator button), and a change re-casts clashing characters in every saved book through `NarratorRecast`. (Slice 109: docs/plan/slices/109-dino-one-narrator-per-language.md)
