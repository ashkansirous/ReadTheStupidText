# Slice 154 — Dino: vowel marks in Farsi and Arabic books

*In progress (wave 2026-10-26b), draft PR.* Dino Reader only. Starts with a listening test, which decides whether the text work goes ahead.

**Batch:** 31 (tester review, 2026-10-26; Decision 159)
**Touches:** `tools/sound-words-test/**`, `tests/DinoReader.Tests/Starters/translations/*.fa.json`, `tests/DinoReader.Tests/Starters/translations/*.ar.json`, `tests/DinoReader.Tests/StarterTexts.cs`, `tests/DinoReader.Tests/StoreTexts.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/LanguageRules.cs`, `src/DinoReader.Core/FarsiRules.cs`, `src/DinoReader.Core/ArabicRules.cs`, `.claude/agents/dino-story-writer.md`, `.claude/skills/create-dino-story/SKILL.md`, `src/DinoReader.App/CLAUDE.md`

## Why

The tester heard the Farsi and Arabic voices misread words many times. Without vowel marks (harakat), a written
word can be several words. Today Arabic has 759 marks in about 24,000 letters (~3%), and Farsi 31 in about
24,400 (~0.1%). Only these two of the 14 languages use the Arabic script. Farsi is read by `piper-fa` (`mana`
default, also `ganji`, `ganji_adabi`, `gyro`) and Arabic by Supertonic-3. Nothing yet shows that these voices
read better with marks. Slice 145 found Farsi `دَنگ!` no shorter than `دنگ!`, but that was a different
question.

## Decisions locked with the user (Decision 159)

- **First a listening test**: 2-3 pages per language, plain and fully vowel-marked, through each Farsi voice and the
  Arabic voice. The user (or a native speaker) judges the result.
- If the marked text reads better: **full vowel marks** in the 15 starter books and every store book, in both
  languages. The marks are **shown on screen** too, as children's books in both languages print them.
- If a voice reads worse with marks: keep the marks on screen and strip them only for that voice before
  speaking (a per-voice switch in its `LanguageRules`).
- Books with open PRs wait until they merge: the illustration PRs #393, #394, #402, #413, #414 and #415, and
  Slice 146's open Farsi rewrites. The marks go on top of Slice 146's spoken-language text, never under it.
- From now on the writer agent writes Farsi and Arabic with full vowel marks.

## Work

- [ ] Listening set (extend `tools/sound-words-test`): pairs of plain and marked sentences, per voice; publish a
      listen page; record the user's verdict here (blocker: user/native listen).
- [ ] If it passes: add vowel marks book by book (one PR per few books, after Slice 146's PR for that book),
      reviewed by `dino-story-reviewer`. A test checks that the marks never change the text once stripped.
- [ ] If a voice reads worse: the per-voice strip before speaking. Test it.
- [ ] Bump each book's version so installed copies update. Store re-uploads after the user's OK.
- [ ] Update the writer agent, the skill and the App CLAUDE.md rule.

## Verification

- The listening verdict recorded here.
- Emulator: a Farsi and an Arabic starter read aloud, with the marks shown and the highlight still lined up.

#### Design (Dino Reader Batch 31 mock, screen 72, added 2026-10-26)

Source: `docs/design/dino-reader/Dino Reader Batch 31.dc.html`, screen 72 (lines 186-212). One frame at 360x760, real UI, headed "only if the listening test passes". The Farsi sample is a draft; the mock says a native speaker needs to check it.

- **Frame.** Picture + text view of The Three Little Pigs, right to left (the whole layout flips). Title "سه بچه‌خوکِ کوچولو" in Vazirmatn 20/600, line-height 1.4. Words panel as in Slice 151 frame 66 but `dir="rtl"`, Vazirmatn 21, **line-height 1.9** (up from 1.5 so marks above and below do not clip); `max-height: calc(30% - 8px)` still applies. Sample text: "گُرگ از دودکِش سُر خورد و شَلَپ! اُفتاد تویِ آبِ سَرد." then the highlighted "«هاپ‌چی!»", then "عَطسه کَرد.". Spoken text `#8a8399`, highlight `#FFE27A`; the highlight covers the whole marked word.
- **Behaviour (note).** Full vowel marks on screen, as children's books in Farsi and Arabic print them. If a voice reads worse with marks, they stay on screen and are removed only before that voice speaks (the plan's per-voice strip).
- **Strings.** None.

**Settled by the mock (the plan left open).** Line height 1.9 and font Vazirmatn for marked text, with the 30% cap kept. **Default:** use 1.9 for Farsi and Arabic books only; other scripts keep 1.5.
