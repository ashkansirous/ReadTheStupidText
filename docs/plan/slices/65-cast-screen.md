# Slice 65 — Cast screen

*Built (wave 2026-10-21b), PR #202.*

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

- [x] Cast page (gated by Slice 61's gate): list of characters (name, gender, voice, words
      spoken), narrator picker on top, and a row for "unknown speaker".
- [x] Per row: voice **preview** button (speaks a fixed line in that voice), gender toggle
      (re-runs `VoiceCaster` for that character within the gender), voice dropdown.
- [x] **Merge** two characters: pick both, keep one name; the other's segments are
      re-attributed; the merged character keeps the voice of the name chosen under "Keep the name" (default: the first ticked).
- [x] Persist the edited cast in `story.json` (Slice 61). An edit is a **pin**: re-running the
      caster never overrides a pinned voice.
- [x] Reading uses the pinned cast from the next read on: the gate stops the story before Manage,
      so no read is live while the cast is edited. The mid-read chunk-boundary swap (Decision 29)
      was not built.
- [x] Define the "unknown speaker" voice rule (default: the narrator voice, never a
      character's) and record it here.
- [x] Tests (Core): pinning, merge re-attribution, a gender change re-casts without touching
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
- **Rules in the notes.** Behind the gate. Any edit is **pinned** (EDITED): re-casting never overrides it, and it applies from the next read (the gate stops any read first). The narrator’s voice is never given to a character; main characters’ voices are never reused. Unknown-speaker lines use the narrator voice. After a merge: one entry, one voice (the first kept), every line re-attributed, and the fix is remembered with the story.

**Open point: Unknown-speaker rule.** The plan left it open (default narrator voice). The mock settles it: the narrator’s voice. Record it in that step.
**Open point: MAIN badge.** Slice 59 has main characters (top N by words, default 2) but no UI for them. **Default:** show MAIN on those characters.
**Open point: Gender "?".** Plan says a gender toggle; the mock has three states. **Default:** Boy / Girl / ?, where ? means unknown.
**Open point: Keep-the-name picker.** Plan says "keep one name"; the mock’s choices pair name and voice. **Default:** follow the mock.
**Open point: Shared-voice tag.** The plan has only a note in the cast screen (Slice 66). The mock adds a per-row tag. **Default:** show the tag on every character whose voice another character also uses (all but the first holder), only for packs with fewer voices than characters; the note card shows under the same condition.
**Open point: Main characters share a voice in Farsi.** Slice 59 says main characters’ voices are never reused. Screen 15 gives Peter and Cotton Tail (the two main characters in screen 10) the same voice "ژیرو". **Default:** the narrator exclusion is absolute; the no-share and main-never-reused rules relax when the pack has fewer voices than needed (Decision 75 known limit). Slice 59’s `VoiceCaster` and Slice 60 must allow this; Slice 59 should state it.
**Open point: Note card copy.** The card says "2 natural voices" for Farsi. **Default:** compose the count from the installed pack ("{n}"), not hard-coded, in each locale.
**Open point: Cast before save.** The add screen (Slice 61) opens this page via "Check the cast" for an unsaved story. **Default:** same page, in-memory story.

New strings (both locales): "Who’s speaking", "Narrator", "{n} words", "MAIN", "EDITED", "Boy", "Girl", "Unknown speaker", "{n} lines · uses the narrator’s voice", "Merge two characters", "Merge characters", "Pick the two names that are the same character. Their lines join up under one name and one voice.", "Keep the name", "Merge into {name}", "Farsi only has {n} natural voices, so some characters share a voice. The narrator’s voice is never given to a character." (the note card, per language), "Shared voice" (tag; Farsi "صدای مشترک"). Farsi strings are machine-drafted.

## Decisions taken during build

- **Unknown-speaker rule:** a quote nobody could be attributed to, narration and a character with no voice are read by the narrator's voice (`VoiceCast.VoiceForLine`, unit-tested). The row is always drawn, even at "0 lines".
- **Pin = `CastMember.Edited`**: a grown-up edit (voice, gender, merge keep) sets it, shows EDITED, and makes the stored gender override the guessed one. Every stored voice was already pinned for the caster (Slice 61); `Edited` stays set even if a narrator change forces a new voice. story.json stays version 1 (`Edited` per member, optional `Merges`).
- **Merge** is stored as `Story.Merges` (merged-away id to kept id) and applied to the attribution on every read (`CastMerges`), so lines are re-attributed and words summed. The kept name decides the voice (the sheet's "Keep the name" picker, default the first ticked), not strictly "the first voice". A merge whose side no longer exists after a text fix is ignored.
- **Gender toggle:** *(superseded by "Final shape" below: `CastEditor.SetGender` now prefers a free voice of the target gender, then shares the one with the fewest words, then keeps the current voice and reports it.)* The voice stays if it already fits the new gender ("?" fits any); otherwise a free voice of the new gender is taken, else the least-spoken one is shared. A character never gets an opposite-gender voice; with none of that gender in the pack the voice stays and the page says so.
- **Narrator picker** (action sheet) lives on this page; the caster takes the new narrator's voice off any character who had it.
- **Mid-read swap:** not reachable. The gate stops the story before Manage, so the cast applies from the next read; no chunk-boundary swap was added.
- **Cast before save:** "Check the cast" was added to the add screen's result card; the page works on the in-memory story and writes back into the page's fields. From Edit, edits go into the `StoryDraft` and save with Save.
- **MAIN** = top 2 by words (same rule as the caster). **Shared-voice tag and note** show only when characters outnumber the non-narrator voices; the note is generic English ("This voice pack only has {n} natural voices...").
- Dropdowns are platform action sheets, not the mock's chips-as-menus. RTL sets the page flow direction only; Farsi strings and font are Slice 66.
- **Review fixes.** Narrator change onto a voice an edited character holds: the character is reassigned, their EDITED pin is cleared (it was overridden) and the grown-up sees a notice. Merge takes the better-known gender (known beats unknown, the kept character's wins a conflict; also in `CastMerges.Apply`) and no longer sets EDITED, since merges persist in `Story.Merges`. Merge and voice pickers map action-sheet picks by index. Preview resets on the reader's `Completed`/idle and the page stops the reader on leave only while a preview is live. Add screen: each Save mints a fresh story id (the cast page's id is throwaway). Post-save refresh and the narrator recast are wrapped in try/catch. Add screen has no narrator control; the result card is refreshed by `KeepCast`.
- **For Slice 66:** the preview line (`CastText.PreviewLine`) is English only; localise it per story language.
- Preview speaks a fixed line through `SpeakSegmentsAsync` (or `SpeakAsync` when segments are unsupported) and stops when the page leaves.

## Final shape

- **Core:**
  - `CastMember.Edited` and `Story.Merges` in `story.json` (version still 1).
  - `CastEditor`: `SetVoice`; `SetGender` (prefers a free voice of the target gender, then shares the one with the fewest words, then keeps the current voice and reports it); `Merge` (takes the better-known gender, does not mark the result Edited); `NamesLosingVoice`/`UnpinOverridden` for narrator changes.
  - `CastMerges`, cycle-safe.
  - `CastSheet`, `MergeSelection`, `AvatarPalette`.
  - `VoiceCaster.VoiceForLine`: an unknown speaker uses the narrator.
- **App:**
  - `CastPage` (a grown-up page): narrator picker, character rows with a gender toggle and voice chips, a preview button, the unknown-speaker row, and a merge sheet.
  - Save-or-rollback on failure; guarded refreshes; the preview state follows the reader.
- **Entry points:** the Manage "Cast · n" pill; Edit's "Who's speaking" row (recasts after a text fix, saves with the draft); Add's "Check the cast" (cancels any pending analysis).
- `StoryVoicing` applies merges and gender overrides on every read.

*Changed while building:* see "Decisions taken during build" above; the gender toggle rule there is superseded by `CastEditor.SetGender` as described here.

*From review:* the high pass found 10 issues, all fixed. The medium pass found 8, 7 fixed; skipped: the stale narrator control on the Add screen, because no such control exists. Details are under "Review fixes" above.

*Emulator results (RTST_Test, 2026-10-21):*

- Earlier run, by the implementer: the cast screen renders per the mock; the narrator's voice is excluded from the voice choices; changing Cotton Tail's voice showed EDITED; merging Mum into Peter left one Peter with 8 words; `story.json` persisted `Edited` and `Merges` across a force-stop.
- After the review fixes, re-checked: passing the gate and tapping "Cast · 2" opens "Who's speaking" with the narrator Demiurge; Cotton Tail shows Girl, Lupusregina Beta and EDITED; Peter shows Boy, Momonga and EDITED, with 8 words; the Unknown speaker row shows "0 lines · uses the narrator's voice"; the merge button is present.

## Acceptance

- Change Cotton Tail's voice, then read: her lines use the new voice from then on, also after
  an app restart.
- Merge "the rabbit" into "Peter": one entry, one voice, all lines re-attributed.
- The screen is unreachable without the gate.

## Risks / notes

- If common books need many fixes this screen becomes the main UX (the kill criterion in
  Decision 74): measure on 5 sample kids' books.

#### For CLAUDE.md (lifted 2026-10-21)

- Dino Reader's cast edits pin voices via `CastMember.Edited`. (Slice 65: docs/plan/slices/65-cast-screen.md)
- Merges live in `Story.Merges` and are applied on every read through `CastMerges` (`StoryVoicing`). Never re-attribute in the stored text. (Slice 65: docs/plan/slices/65-cast-screen.md)
- Change the cast only through `CastEditor`, which keeps the voice rules. (Slice 65: docs/plan/slices/65-cast-screen.md)
- An unknown speaker uses the narrator's voice (`VoiceCaster.VoiceForLine`). (Slice 65: docs/plan/slices/65-cast-screen.md)

#### For README.md (lifted 2026-10-21)

Grown-ups can check and fix who's speaking: pick the narrator, choose each character's voice and gender, hear a preview, and merge two names that are the same character.

#### Not yet verified

- The preview audio.
- The Add-screen "Check the cast" path.
- Opening the cast page from Edit after a text fix.
- The narrator-change notice.
- Reading in the edited voices after a restart.
- The shared-voice note.
- RTL (Slice 66).
