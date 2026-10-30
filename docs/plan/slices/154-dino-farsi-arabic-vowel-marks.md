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

## Verdict and approach (2026-10-27)

- **User verdict from the review page:** correct marks help the voice, wrong marks hurt. So marks come only from words the user approved by ear, never from a guess.
- **Review page approach:** each distinct word of the Pigs Farsi text got a clip with the voice's own guess and the user's Finglish; the user chose "mine" or "guess" per word (`tmp/answers-final`). `tools/sound-words-test/finglish_to_marks.py` aligns the Finglish to the letters and adds the marks (a silent word-final ع/ء and an ezafe on a final ی are handled); `build_marks_list.py` turns the answers into the list; `apply_marks.py` builds the "how to read" table. One word, کوچولوم (user typo `kuchulm`), stays unmarked.
- **Approved list (data):** `tests/DinoReader.Tests/Starters/translations/three-little-pigs.fa.marks.json` (`{marks: {plain word -> marked word}, ezafe: [{page, sentence, word index, plain, marked}], spoken: [{page, sentence, replaces, text}]}`: 182 words, plus the user's spoken replacements for phrases the voice leaves silent, e.g. "آخ جون!"; they apply to the spoken text only, before the marks; `build_marks_list.py` reads them from `tmp/choices`).
- **Design change from the user:** marks are NOT shown to kids. `three-little-pigs.fa.json` stays plain; `three-little-pigs.fa.speak.json` holds the marked "how to read" text per piece. It rides in the story's `Translations` under the key `speak:<lang>` (`BundledTranslations.SpeakKey`), so the `.dino` format (schemaVersion 1) is unchanged and older apps ignore it. `StoryVoicing.VoiceAsync` speaks it (`StoryTranslator.TranslateAsync(forSpeech: true)`); the on-screen text and highlight use the normal translation and source ranges, so they stay aligned.
- Tests: `FarsiMarksTests` (shown text has no marks, spoken text stripped equals shown for every piece, every marked word is in the list, plain text hash), `BundledTranslationsTests.LookupSpoken`. Pigs starter bumped to v11.
- **Ezafe is per occurrence (review fix):** the user wrote each word's Finglish looking at ONE sentence, so a trailing e / ye the plain word does not spell belongs to that occurrence only. `marks` holds the word without it (applies everywhere); `ezafe` holds the with-ezafe form for that sentence and word position (10 of them). "گرگ گفت" is spoken without ezafe. `Adopt` gives an already-adopted or edited copy the speak table only for pieces whose shown translation still equals the bundled one (adopted flag key bumped to v3).

## Scope change (user, 2026-10-27, Decision 166)

- **Arabic vowel marks are out of scope:** leave Arabic as it is. The Arabic diacritizer and its listen test below are dropped.
- **The marks are a hidden "how to read" text** kids never see (`speak:<lang>`), not shown on screen. This supersedes the earlier "shown on screen" decision (159).

## Built, Three Little Pigs Farsi part (2026-10-27)

*Built 2026-10-27; emulator-checked, not fully verified (see below).* Supersedes Decision 159's "marks shown on screen" (user, 2026-10-27).

- **Hidden marks.** Kids see plain Farsi; the marked text is a `speak:fa` table in `Story.Translations` (`BundledTranslations.SpeakKey`/`LookupSpoken`/`ComposeSpoken`). `StoryTranslator.TranslateAsync(forSpeech)` and `StoryVoicing` (`forSpeech: true`) speak it if present, else the plain text. Highlight follows the source ranges. `.dino` `schemaVersion` unchanged. `ArabicMarks.Strip`; `AnimalSounds.SaysIt` strips marks.
- **Review tooling.** The user checked every word on a review page (Finglish input to `tools/sound-words-test/finglish_to_marks.py`, 28 tests); `build_marks_list.py`, `apply_marks.py`, `regen_review_audio.py`. Data: `tests/DinoReader.Tests/Starters/translations/three-little-pigs.fa.marks.json` and `.fa.speak.json`. Ezafe applied per occurrence only. Exclamations `piper-fa` mana drops get spoken-only replacements, e.g. p2 «آخ جون آخ جون آخ جون!».
- **Versions.** Pigs starter v10 to v11; adopted flag key v3.
- **Sound-word picks (2026-10-27, pigs v12):** the user chose spoken-only replacements on the review page: p5 «تَقْ تَقْ تَقْ!», p14 «شلپ شلپ شلپ!» and «آپچی، آپچی، آپچی!», p15 «چک چک چک!», p2 unchanged; shown text unchanged.
- **From review:** both passes done, fixes applied. `DinoReader.Tests` 1527/1527; Android build clean.
- **Device (emulator, headless):** shown text has no marks; spoken text marked on p1, p2 and ezafe lines; highlight follows; English unchanged; no crash; v9 to v11 upgrade kept Created/SourceId.

#### For CLAUDE.md (lifted 2026-10-27)

- The `speak:<lang>` convention is already in `src/DinoReader.Core/CLAUDE.md` (Slice 154); nothing further to add.

#### Not yet verified

- Recordings surviving the starter refresh (none existed in the test; the story dir id changes on refresh).
- p5 تق تق تق, p14 شالاپ/آپچی and p15 چیک replacements await the user's pick.
- Farsi review of the other 14 books.
- Arabic diacritizer and its listen test.

- **Gingerbread Man reviewed (v9):** 223 words checked on the review page, 3 spoken replacements (tap, tagh, oum), ezafe per occurrence; `gingerbread-man/translations/fa.marks.json` and `fa.speak.json`. Page 13 "you can catch me" now reads نمی‌تونی on the user's request.
