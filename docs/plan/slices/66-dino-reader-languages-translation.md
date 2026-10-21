# Slice 66 — Dino Reader languages and translation

*Built (wave 2026-10-21b), PR #208.*

**Batch:** 10
**Decisions:** 74, 81 (also Batch 9 Decisions 63, 67, 68)
**Depends on:** Slices 50-54 (packs, voices, OCR, localized UI, translation); Slices 59-61
**Touches:** `src/DinoReader.Core/**`, `src/DinoReader.App/**`, `tests/DinoReader.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

Stories in the other eight languages are attributed correctly in their own language, can be
translated per segment after attribution, and Dino Reader's UI is localized with RTL.

## Steps

- [x] `LanguageRules` tables (the Slice 59 hook) for fa, ar, zh, ja, ru, fr, es, pt: quote marks
      (guillemets, corner brackets, dash dialogue, curly forms), speech verbs, pronouns, titles,
      small names lists. The segmenter picks rules by the story's language.
- [x] Attribution runs in the **source** language; only then is each segment translated
      (Slice 54 translator), keeping `CharacterId` and source ranges (ranges refer to the source
      text for highlight; the spoken text is the translation).
- [x] Localize Dino Reader's UI strings and RTL layout with the Slice 53 approach: gallery,
      reading screen, gate, cast screen all mirror in RTL.
- [x] Voice packs per language: the caster sees only the installed pack's voices. Supertonic
      languages have 5+5; Farsi (Piper) has 1-2, so heavy reuse is a **known limit**
      (Decision 75); show a note in the cast screen.
- [x] Gender guessing per language (grammatical cues in fr/es/pt/ru/ar, small names lists);
      Unknown falls back to the cast-screen fix.
- [x] Tests per language with a 3-line dialogue sample (attribution + casting).

#### Design (Dino Reader v2 mock, screens 9, 8b language rows, 14, 15, refreshed 2026-10-20)

Source: `docs/design/dino-reader/Dino Reader v2.dc.html`, add-story screen (lines 47-57), edit-story screen 8b (lines 75-82), and two Farsi frames (14 reading, lines 84-88; 15 cast, lines 88-94). The mock draws the language controls twice and one RTL language (Farsi); no language list and no other languages. Values for the rest of the add and edit screens are in Slice 61 Design; the Farsi reading and cast values are in Slice 62 and Slice 65 Design.

- **Story language row.** White card (radius 18), min height 50, padding 0 14: label "Story language" 14.5; value right-aligned "English · detected" 14.5/600 `#5a5470` plus a 14px chevron. A language list is implied, not drawn.
- **Translate row.** Label "Translate to my language" 14.5; switch 46x28 radius 14, off `#d9d2c4`, knob 24 white. The on state is not drawn.
- **Edit screen (8b).** The same two rows repeat as the middle rows of the white card: "Story language" = "English" (no "· detected" on edit) and "Translate to my language" (switch off). Slice 61 owns the screen; this slice wires the rows.
- **Right-to-left (screens 14, 15).** The mock draws Farsi only. Whole screen mirrors; back arrow points right; lock and mode switch move left; pages run right to left (swipe left for next); quotes in «guillemets»; boxed spoken line in logical order; font Vazirmatn (Slice 58). Farsi copy is machine-drafted. Details and strings: Slice 62 (reading) and Slice 65 (cast, note card, "صدای مشترک" tag). The row switch in the language card uses `margin-inline-start:auto` and `inset-inline-start` for the knob, so logical properties are used.
- Note: who speaks is worked out once, at import, in the story’s own language, before any translation (matches the plan).

**Settled by the mock: RTL and Farsi voice-reuse note.** Screens 14 and 15 draw the mirrored reading and cast screens and the voice-reuse note. Arabic is expected to mirror the same way (not drawn).
**Open point: No language list or other languages.** The language picker, the on state of the translate switch, and zh/ja/ar frames are not drawn. **Default:** reuse the Slice 53 language picker and the Slice 61 tokens; Chinese and Japanese use the system font.
**Open point: "detected" tag.** The plan has no language-detection step. **Default:** detect at import and show "· detected" until the grown-up changes it.

## Acceptance

- A short dialogue in each of the eight languages attributes all quotes; Supertonic
  languages cast distinct voices.
- Translating a story keeps each character's voice and the highlight on source ranges.
- Farsi: a 3-character story reads, with the reuse note shown.

## Risks / notes

- zh/ja have no spaces and different quote marks: sentence splitting differs; reuse Batch 9's
  chunker rules where they exist.

## Decisions taken during build

- **Rules:** `LanguageRules.For(code)` picks fr, es, pt, ru, fa, ar, zh, ja tables (English otherwise). Records gained optional members: extra quote marks, dialogue dashes (ru/es/pt/fr: a line opening with a dash is a quote, a second spaced dash closes it, so `- Hi, - said Peter.` splits into quote, tag, quote), `ScriptStyle` (Cased, CaselessSpaced for fa/ar, Unspaced for zh/ja), gendered verb forms (ru past tense, ar), name endings (es/pt `-a`/`-o`, ru), and regex fragments for tags. fa/ar names come from the name list or a speech tag (no capitals); zh/ja names come from the list plus 小/阿/老+1 and katakana runs. Tables are small starter lists.
- **Known limits:** an unlisted Chinese/Japanese name in kanji is not found (cast-screen fix); Farsi/Arabic adverbs before a verb can be read as a name; CJK sentences next to a quote stay in one highlight box. Chinese/Japanese words count two characters to a word.
- **Detection:** no ML Kit detector in Dino, so `ScriptLanguageGuesser` (script plus common words) pre-fills the story language with "· detected"; the grown-up can change it from an action sheet of endonyms.
- **Translation:** `StoryTranslation` (Core) translates each sentence piece after attribution, keeping kind, character and source range; a translation over 200 characters is cut into pieces sharing the source range. `VoiceRemap` keeps a character's voice (or the same-gender voice at the same place) in another language's pack. **No translator in this build:** the ML Kit `ITranslator`/`ILanguageDetector` live in the RTST Mobile project. `StoryTranslator` takes an optional `ITranslator` from DI; without one the "Translate to my language" rows are hidden and stories read in their own language. Follow-up: once Slice 63 settles `Mobile.Shared`, move/reference the ML Kit translator and detector and register them in Dino's `MauiProgram`.
- **Voices:** the caster pool is the installed pack's voices for the story's language, else the Supertonic styles. Opening a story or its cast sets `ISettingsStore.Language` to the spoken language (`SpokenLanguage`) and starts that pack. Few voices show the existing shared-voice note.
- **UI language:** the device language when it is one of the nine, else English; no in-app UI picker. Strings in `Resources/Strings/AppResources*.resx` (116 keys, machine-drafted, need a native read), typed `AppResources`; `LocalizedUiTests` enforces keys, placeholders and no literals in XAML. Page chrome mirrors by UI language (`FlowDirection`, `UiFlow.Mirror` for header paddings, arrows flipped); the story text surface mirrors by story language. Farsi/Arabic UI uses Vazirmatn.
- Pack status copy is a local `LocalizedPackStatus` (Mobile.Shared is not edited).

## Verified on the emulator (RTST_Test, uk.sirous.dinoreader only)

- A Farsi story opens right to left with sentence boxes, switches the voice language to fa and shows the localized pack-status line.
- Not verified: Farsi/Arabic UI mirroring (per-app locale does not change the .NET culture; the device language was not changed), the gate, add and edit screens in other languages.

## Review fixes

- `SpokenLanguage` is a single serialized worker (`LatestWinsWorker` in Core): each `Use` records the wanted language, the worker waits until the installer is idle, stores the language, starts the installer and waits for it to settle, then serves the latest wanted language once more if it changed. Exceptions are logged and the loop survives; a Failed state is retried once per `Use`, never in a loop. A repeat `Use` of the same code starts the pack unless it is Ready. Only `ReadingPage` switches the spoken language: `CastPage.Load` no longer touches `ISettingsStore.Language`, and its preview line is in the current spoken language.
- Farsi/Arabic names: every caseless run counts as sentence-initial, so only the name/title tables and speech tags accept a name. `GenderGuesser.VerbBefore` stops at a terminator or quote.
- Dialogue dashes are em, en and horizontal bar only; an ASCII hyphen opens a quote only at the start of a line before a space (`LanguageRules.IsDialogueDash(text, pos)`), never inside words ("grand-mere", "dit-elle").
- Rules are cached once per language (`RuleSets.For`); `StoryVoicing` keeps one segmenter and attributor per language. Language code lists, RTL set and native names derive from `AppLanguage`/`AppLanguages` (Core now references the library-free Application project); `DinoFonts.IsRightToLeft` calls `UiLanguage.IsRightToLeft`.
- Translation runs up to 4 pieces at a time (`StoryTranslation.DefaultParallelism`) and takes the read's `CancellationToken`: `ReadingSession` cancels it on Stop or when a new read supersedes. Fully lazy translation (speak the first pieces while the rest translate) needs the reader to accept a growing segment list; not done.
- Removed unused `StoryDraft.LanguageChanged` and `UiFlow.ForStory`.
- Not changed, on purpose: `UiFlow.Mirror` is not double-mirroring. RTST Slice 53 showed on the emulator that manual Thickness mirroring under RightToLeft lands correctly on Android, with no double flip.
- The hyphen in `TagPatterns.VerbSubjectGap` is French only ("dit-il"); other languages keep whitespace.
- `StoryTranslation` links a `CancellationTokenSource` and cancels the remaining pieces on the first fault.
- Accepted, not changed: `DinoReader.Core` references `ReadTheStupidText.Application` (library-free, for `AppLanguage`); one more finding judged harmless.
- When translation is skipped (model missing) the story is read in its own language and voices, and the spoken pack goes back to the story's language (`ReadingSession.TranslationSkipped`). Voice remap is done as a set (`VoiceRemap.MapSet`, narrator first, Unknown-gender aware). The cast preview speaks the story's language when its pack is installed, else the current one, and never switches the global language. `StoryVoicing` clears its voice pools when a pack finishes installing. Add-story cancels the in-flight analysis before recasting and clears a stale "detected" label. The UI culture is applied in `MauiProgram` before anything reads it. `Add_FileSub` and `Cast_UnknownGender` are in all eight language files; `App_Name` stays neutral-only (as in RTST).
- Not done, follow-up: lazy translation (speak the first pieces while the rest translate); no translator is registered yet.

## Review notes

- High pass: 10 findings, 9 fixed. The UI-mirror finding was skipped: the emulator evidence above (RTST Slice 53) shows no double flip.
- Medium pass: 8 findings, 6 fixed.

#### For CLAUDE.md (lifted 2026-10-21)

- Dino rules per language live in `RuleSets` (cached per `AppLanguage`); `LanguageRules.For(code)` picks them.
- Only `ReadingPage` switches the spoken language (`SpokenLanguage`); the cast screen never does.
- Translation is built but hidden until an `ITranslator` is registered in Dino's DI.

#### For README.md (lifted 2026-10-21)

- Dino Reader works in nine languages, with right-to-left layout for Farsi and Arabic.

#### Not yet verified

- Farsi/Arabic UI mirroring on a device.
- Russian and Chinese stories read on a device.
- A native read of the 116 UI strings.
- Registering the ML Kit translator for Dino (follow-up).
