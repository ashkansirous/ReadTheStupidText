# Slice 65 — Cast screen

*In progress (wave 2026-10-21b), draft PR.*

**Batch:** 10
**Decisions:** 74, 75, 77
**Depends on:** Slices 59, 60, 61
**Touches:** `src/DinoReader.App/**`, `src/DinoReader.Core/**`, `tests/DinoReader.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

Behind the grown-up gate, a parent sees the detected characters with gender and voice,
fixes mistakes and merges two names that are one character. Changes apply for the rest of
the book and are remembered.

## Steps

- [ ] Cast page (gated by Slice 61's gate): list of characters (name, gender, voice, words
      spoken), narrator picker on top, and a row for "unknown speaker".
- [ ] Per row: voice **preview** button (speaks a fixed line in that voice), gender toggle
      (re-runs `VoiceCaster` for that character within the gender), voice dropdown.
- [ ] **Merge** two characters: pick both, keep one name; the other's segments are
      re-attributed; the merged character keeps the first voice.
- [ ] Persist the edited cast in `story.json` (Slice 61). An edit is a **pin**: re-running the
      caster never overrides a pinned voice.
- [ ] Reading uses the pinned cast from the next segment on, even when the story is open
      mid-read (the Decision 29 chunk-boundary swap).
- [ ] Define the "unknown speaker" voice rule (default: the narrator voice, never a
      character's) and record it here.
- [ ] Tests (Core): pinning, merge re-attribution, a gender change re-casts without touching
      other pins.

#### Design (Dino Reader v2 mock, screens 10, 11, 15, refreshed 2026-10-20)

Source: `docs/design/dino-reader/Dino Reader v2.dc.html`. Three frames (cast lines 57-64, merge sheet 64-70, Farsi cast with few voices 88-94). Mostly agrees with the plan; adds MAIN and EDITED badges, a boy/girl/? toggle and a "keep the name" picker. Tokens in Slice 58 Design.

- **Cast page (screen 10).** Page `#F3EFE6`. Header: back chevron 44x44, title "Who’s speaking" 20/600, subtitle (story title) 12 `#8a8399`. Cards white, radius 18, gap 10.
- **Narrator row.** Min height 56, padding 0 14. Avatar 34 circle `#2b2540` with a white book icon 18px; "Narrator" 15/600; voice dropdown chip on the right: height 32, padding 0 10, radius 10, fill `#F3EFE6`, 13/600, 12px down-chevron (example "Demiurge").
- **Character card** (one white card, rows split by 1px `#efe9dd`). Row padding 12/14. Top line: avatar 34 circle with the initial (white 700; colours seen `#4FC060`, `#FF7A9C`, `#FFA94D`), name 15/600 with inline badges, "212 words" 12 `#8a8399`, preview button 36 circle `#ECE7FF` with a `#5B57E8` play triangle 16px. Badges 10.5/600, radius 6, padding 1/6: **MAIN** (`#5B57E8` on `#ECE7FF`), **EDITED** (`#1f7a3a` on `#dff7e4`). Second line, indented 44: gender control "Boy" / "Girl" / "?" (track `#F3EFE6`, radius 9, padding 2; cells height 28, padding 0 8, radius 7, 12/600; selected white with shadow `0 1px 2px rgba(43,37,64,.15)` and ink `#2b2540`, others `#8a8399`) and the voice chip on the right (e.g. "Momonga", "Albedo", "Yuri Alpha").
- **Unknown speaker row.** Avatar 34 circle `#e6e0d4`, "?" `#5a5470`; "Unknown speaker"; "2 lines · uses the narrator’s voice".
- **Merge button.** Height 48, radius 24, 2px border `#c9c0f5`, text "Merge two characters" 15/600 `#5B57E8`.
- **Merge sheet (screen 11).** Scrim `rgba(43,37,64,.5)`, white sheet, top radius 28, padding 22/18/26, gap 12. Title "Merge characters" 20/600; body "Pick the two names that are the same character. Their lines join up under one name and one voice." 13.5 `#5a5470`. List with 1px `#efe9dd` border, radius 16; rows padding 10/14: checkbox 24 radius 7 (checked `#5B57E8` with white tick; unchecked 2px `#cfc7b8`), avatar 30 circle (initial 13), name 15 (600 when selected, row fill `#F4F2FF`), voice 12.5 `#8a8399`. Row "Keep the name" (fill `#F3EFE6`, radius 14, padding 10/14, label 14 `#5a5470`, value "Peter · Momonga" 14.5/600, down-chevron). Primary button "Merge into Peter" height 50, radius 25, `#5B57E8`, white 15.5/600.
- **Farsi cast, few voices (screen 15, lines 88-94).** Same layout as screen 10, mirrored (`dir="rtl"`, Vazirmatn; back arrow flipped; header title "چه کسی حرف می‌زند" 20/700, subtitle "پیتر و دُم‌پنبه" 12 `#8a8399`). New **note card** at the top: fill `#FFF1D6`, radius 16, padding 11/13, text 13 / line-height 1.6, colour `#6B4200`: "فارسی فقط ۲ صدای طبیعی دارد، پس بعضی شخصیت‌ها صدای مشترک دارند. صدای راوی هیچ‌وقت به شخصیتی داده نمی‌شود." (Farsi has only 2 natural voices, so some characters share a voice. The narrator’s voice is never given to a character.) Narrator row: "راوی" 15/700, voice chip "امیر". Character rows: initial avatars "پ", "د", "م" (colours `#4FC060`, `#FF7A9C`, then the next), names "پیتر", "دُم‌پنبه", "مامان", word counts "212 کلمه", "148 کلمه", "31 کلمه", gender cells "پسر" / "دختر" / "؟", voice chip "ژیرو" on the left. A character whose voice is already used by another character gets an inline **shared-voice tag** after the name: "صدای مشترک" 10.5/700, `#9A5B00` on `#FFF1D6`, radius 6, padding 1/6 (here on دُم‌پنبه and مامان, not on پیتر, the first holder). No MAIN or EDITED badges are drawn in this frame. Per the caption the voice names are placeholders, and the unknown-speaker row is not drawn.
- **Rules in the notes.** Behind the gate. Any edit is **pinned** (EDITED): re-casting never overrides it, and it applies from the next line even mid-read. The narrator’s voice is never given to a character; main characters’ voices are never reused. Unknown-speaker lines use the narrator voice. After a merge: one entry, one voice (the first kept), every line re-attributed, and the fix is remembered with the story.

**Open point: Unknown-speaker rule.** The plan left it open (default narrator voice). The mock settles it: the narrator’s voice. Record it in that step.
**Open point: MAIN badge.** Slice 59 has main characters (top N by words, default 2) but no UI for them. **Default:** show MAIN on those characters.
**Open point: Gender "?".** Plan says a gender toggle; the mock has three states. **Default:** Boy / Girl / ?, where ? means unknown.
**Open point: Keep-the-name picker.** Plan says "keep one name"; the mock’s choices pair name and voice. **Default:** follow the mock.
**Open point: Shared-voice tag.** The plan has only a note in the cast screen (Slice 66). The mock adds a per-row tag. **Default:** show the tag on every character whose voice another character also uses (all but the first holder), only for packs with fewer voices than characters; the note card shows under the same condition.
**Open point: Main characters share a voice in Farsi.** Slice 59 says main characters’ voices are never reused. Screen 15 gives Peter and Cotton Tail (the two main characters in screen 10) the same voice "ژیرو". **Default:** the narrator exclusion is absolute; the no-share and main-never-reused rules relax when the pack has fewer voices than needed (Decision 75 known limit). Slice 59’s `VoiceCaster` and Slice 60 must allow this; Slice 59 should state it.
**Open point: Note card copy.** The card says "2 natural voices" for Farsi. **Default:** compose the count from the installed pack ("{n}"), not hard-coded, in each locale.
**Open point: Cast before save.** The add screen (Slice 61) opens this page via "Check the cast" for an unsaved story. **Default:** same page, in-memory story.

New strings (both locales): "Who’s speaking", "Narrator", "{n} words", "MAIN", "EDITED", "Boy", "Girl", "Unknown speaker", "{n} lines · uses the narrator’s voice", "Merge two characters", "Merge characters", "Pick the two names that are the same character. Their lines join up under one name and one voice.", "Keep the name", "Merge into {name}", "Farsi only has {n} natural voices, so some characters share a voice. The narrator’s voice is never given to a character." (the note card, per language), "Shared voice" (tag; Farsi "صدای مشترک"). Farsi strings are machine-drafted.

## Acceptance

- Change Cotton Tail's voice, then read: her lines use the new voice from then on, also after
  an app restart.
- Merge "the rabbit" into "Peter": one entry, one voice, all lines re-attributed.
- The screen is unreachable without the gate.

## Risks / notes

- If common books need many fixes this screen becomes the main UX (the kill criterion in
  Decision 74): measure on 5 sample kids' books.
