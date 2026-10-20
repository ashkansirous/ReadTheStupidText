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

#### Design (Dino Reader v2 mock, screen 9 language rows, added 2026-10-20)

Source: `docs/design/dino-reader/Dino Reader v2.dc.html`, add-story screen, lines 46-56. The mock draws two language controls and no RTL or per-language frames. Values for the rest of the screen are in Slice 61 Design.

- **Story language row.** White card (radius 18), min height 50, padding 0 14: label "Story language" 14.5; value right-aligned "English · detected" 14.5/600 `#5a5470` plus a 14px chevron. A language list is implied, not drawn.
- **Translate row.** Label "Translate to my language" 14.5; switch 46x28 radius 14, off `#d9d2c4`, knob 24 white. The on state is not drawn.
- Note: who speaks is worked out once, at import, in the story’s own language, before any translation (matches the plan).

**Open point: No RTL or per-language frames.** No mirrored screens, no language list, no Farsi voice-reuse note. **Default:** mirror the Slice 61/62/65 frames per the Slice 53 approach.
**Open point: "detected" tag.** The plan has no language-detection step. **Default:** detect at import and show "· detected" until the grown-up changes it.

## Acceptance

- A short dialogue in each of the eight languages attributes all quotes; Supertonic
  languages cast distinct voices.
- Translating a story keeps each character's voice and the highlight on source ranges.
- Farsi: a 3-character story reads, with the reuse note shown.

## Risks / notes

- zh/ja have no spaces and different quote marks: sentence splitting differs; reuse Batch 9's
  chunker rules where they exist.
