# Slice 66 — Dino Reader languages and translation

*Planned.*

**Batch:** 10
**Decisions:** 74, 81 (also Batch 9 Decisions 63, 67, 68)
**Depends on:** Slices 50-54 (packs, voices, OCR, localized UI, translation); Slices 59-61
**Touches:** `src/DinoReader.Core/**`, `src/DinoReader.App/**`, `tests/DinoReader.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

Stories in the other eight languages are attributed correctly in their own language, can be
translated per segment after attribution, and Dino Reader's UI is localized with RTL.

## Steps

- [ ] `LanguageRules` tables (the Slice 59 hook) for fa, ar, zh, ja, ru, fr, es, pt: quote marks
      (guillemets, corner brackets, dash dialogue, curly forms), speech verbs, pronouns, titles,
      small names lists. The segmenter picks rules by the story's language.
- [ ] Attribution runs in the **source** language; only then is each segment translated
      (Slice 54 translator), keeping `CharacterId` and source ranges (ranges refer to the source
      text for highlight; the spoken text is the translation).
- [ ] Localize Dino Reader's UI strings and RTL layout with the Slice 53 approach: gallery,
      reading screen, gate, cast screen all mirror in RTL.
- [ ] Voice packs per language: the caster sees only the installed pack's voices. Supertonic
      languages have 5+5; Farsi (Piper) has 1-2, so heavy reuse is a **known limit**
      (Decision 75); show a note in the cast screen.
- [ ] Gender guessing per language (grammatical cues in fr/es/pt/ru/ar, small names lists);
      Unknown falls back to the cast-screen fix.
- [ ] Tests per language with a 3-line dialogue sample (attribution + casting).

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
