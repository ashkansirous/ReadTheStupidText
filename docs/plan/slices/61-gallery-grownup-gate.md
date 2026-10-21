# Slice 61 — Gallery, stories on the device, grown-up gate

*In progress (wave 2026-10-21b), draft PR.*

**Batch:** 10
**Decisions:** 77
**Depends on:** Slices 58, 59, 60; input paths reuse Slice 45 (scan) and the existing file import
**Touches:** `src/DinoReader.Core/**`, `src/DinoReader.App/**`, `tests/DinoReader.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

The home screen is a gallery of the child's stories (cover + title). A grown-up, behind a
simple gate, adds stories (type, file, scan) and removes them. Each story remembers its
cast. Reading never needs the gate.

## Steps

- [ ] `Story` model (title, cover path, language, created, pages, cast) and `IStoryLibrary`
      (list, get, add, update, delete) in `DinoReader.Core`. `Cast` = list of (character id,
      name, gender, voice id), serialised with the story.
- [ ] File-backed `FileStoryLibrary` in `DinoReader.App`: one folder per story
      `AppDataDirectory/stories/<id>/` with `story.json` (System.Text.Json, versioned schema)
      and later `page-NN.jpg`. Atomic write (temp file + rename). Mobile persistence is
      Preferences only today (`MobilePreferencesSettingsStore.cs:33-93`), so this is new.
      Nothing leaves the device.
- [ ] Gallery page: grid of cover + title; tap opens the reading screen (Slice 62). Cover is
      the first page image if there is one, else a generated cover (title on a colour).
- [ ] Add-story flow (gated): type/paste, pick a file (txt/pdf/docx via the existing
      `CompositeDocumentTextExtractor`), scan (Slice 45 scanner + OCR). The text runs through
      Slice 59 once at import and the resulting cast is stored with the story.
- [ ] Delete a story (gated) with a confirm; removes the folder.
- [ ] **Grown-up gate** control: press and hold a lock button 3 s, then a simple sum (two
      numbers 6-19, addition, number-pad answer). Pure `GateChallenge` generator/validator in
      Core (seedable RNG), unit-tested. Used by add, delete, edit story and the cast screen.
- [ ] Tests: sum bounds, validation, a wrong answer re-rolls; `story.json` round trip incl.
      unknown-field tolerance.

#### Design (Dino Reader v2 mock, screens 1, 6, 6b, 7, 8, 8b, 9, refreshed 2026-10-20)

Source: `docs/design/dino-reader/Dino Reader v2.dc.html`. Seven frames (gallery lines 24-28, gate hold 38-40, gate sum 40-41, manage mode 41-47, add story 47-57, gate with a screen reader 70-75, edit story 75-82). The mock agrees with the plan on the gate rules and adds scope on the add and edit flows (see Open points). Tokens: Slice 58 Design. Striped boxes tagged "first page photo" are placeholders for a real photo, not UI.

- **Gallery (screen 1).** Page `#FFF6E6`. Top bar: app icon 40x40 radius 11, title "My stories" 22/600 (flex), lock button 44x44 radius 14, fill `rgba(43,37,64,.07)`, line lock icon (20px, stroke 2, `#8a8399`). Grid 2 columns, 12px gap, 14px side padding. Cover: aspect 3/4, radius 16, shadow `0 4px 0 rgba(43,37,64,.08)`. Title under cover 14/600, line-height 1.25, gap 7. Generated cover: flat colour, title centred in Andika 18/700 white, `text-shadow: 0 1px 0 rgba(0,0,0,.12)`; colours seen `#7FD0FF`, `#FFC24D`, `#B39CFF`, `#FF9FB8`. Photo cover: first page photo. No gate for reading. The reading screens carry their own lock (40x40 radius 12, 18px icon; Slice 62 Design) that opens the same gate.
- **Gate step 1 (screen 6).** Bottom sheet over a dimmed page (scrim `rgba(43,37,64,.55)`), white, top radius 28, padding 24/22/28, centred, gap 12. Two step bars 26x5 radius 3 (`#5B57E8` active, `#e6e0d4` idle). Title "For grown-ups" 22/600. Body "Press and hold for 3 seconds." 14.5 `#5a5470`, max width 260. Hold control 120x120: track circle r54 stroke 8 `#ECE7FF`, progress arc `#5B57E8` stroke 8 round caps, inner button 86 circle `#5B57E8` with white lock icon 20px. Hint "Keep holding…" 13/500 `#8a8399`. Cancel pill "Back to the story" height 48, padding 0 26, radius 24, fill `#F3EFE6`, 15/600. The note (line 40) says step 1 is "opened from the lock on the gallery or on any reading screen. The story pauses."
- **Gate step 2 (screen 7).** Same sheet, both step bars `#5B57E8`. Label "What is" 15 `#5a5470`; sum "7 + 12 = 19" 40/700, letter-spacing 1, answer slot min-width 64 with 4px `#5B57E8` underline and `#5B57E8` digits. Number pad 3 columns, 8px gap: keys height 52, radius 14, fill `#F3EFE6`, 22/600; last row "⌫", "0", "OK" (OK fill `#5B57E8`, white). Sum = two numbers 6 to 19; a wrong answer rolls a new sum (matches the plan). The same gate guards adding, deleting and editing a story, and the cast screen.
- **Manage mode (screen 8).** Page `#F3EFE6`. Title "Manage stories" 20/600; "Done" pill height 38, padding 0 14, radius 19, `#5B57E8`, 13.5/600 white. Same grid. First tile "Add a story": 3px dashed `#c9c0f5` border, white, radius 16, plus icon 28 `#5B57E8`, label 14/600 `#5B57E8`. Each story: delete badge 32 circle `#E5484D` at top-right offset -6/-6, white trash icon 16, shadow `0 2px 6px rgba(0,0,0,.2)`; title 13.5/600; two link pills side by side, gap 6: "Edit" (opens the edit screen below) and "Cast · 3" (opens Slice 65), each 12/600 `#5B57E8` on `#ECE7FF`, radius 8, padding 4/9 (no chevron, count only: "Cast · 3", "Cast · 2", "Cast · 4"). Delete confirm: scrim `rgba(43,37,64,.45)`, white card radius 24 padding 22, title "Delete “The Little Lighthouse”?" 19/600, body "Its pages, pictures and voices are removed from this phone." 14 `#5a5470`, buttons height 48 radius 24: "Keep" (`#F3EFE6`) and "Delete" (`#E5484D`, white).
- **Add a story (screen 9).** Back arrow, title "Add a story" 20/600. Four source tiles in a 2x2 grid, gap 10, white, radius 18, padding 14, min height 104, icon chip 42 radius 12 `#ECE7FF`: "Scan pages" / "Keeps the page pictures"; "Photos" / "Pick page photos"; "PDF or file" / ".pdf · .docx · .txt"; "Type or paste" / "Text only". Titles 15/600, subtitles 11.5 `#8a8399`. Settings card (white, radius 18, rows min height 50): "Story language" = "English · detected" with chevron; "Translate to my language" with a switch 46x28 radius 14 (off `#d9d2c4`, knob 24 white). Result card pinned to the bottom: fill `#2b2540`, radius 18, padding 14: green tick + "3 pages read · 3 characters found" 14.5/600; name chips (12.5, `rgba(255,255,255,.12)`, radius 10, padding 4/10) "Peter", "Cotton Tail", "Mum"; two buttons height 44 radius 22: "Check the cast" (`rgba(255,255,255,.14)`) and "Save story" (`#FF7A59`). Who speaks is worked out once, at import, in the story’s own language, before any translation.
- **Gate with a screen reader on (screen 6b).** Same sheet as step 1 with step bar 1 active, title "For grown-ups" 22/600, body 14.5 `#5a5470` max width 270 (the mock's caption text "TalkBack is on, so the hold step becomes a button. The sum is still the real lock." is a labelling note, not shipped copy). The hold ring is replaced by a full-width button "Continue to the question", height 56, radius 28, fill `#5B57E8`, white 16/600, then "Back to the story". Step 2 (the sum) is unchanged and fully labelled and read aloud. The green outline (3px `#2E7D32`) and the green speech bubble "For grown-ups. Continue to the question, button. Double-tap to activate." are annotations for the reader of the mock (focus ring and the spoken label), not UI. Applies when TalkBack, VoiceOver or Switch Access is on; a child with a screen reader still has to solve the sum.
- **Edit a story (screen 8b).** Reached from "Edit" in manage mode, behind the gate. Page `#F3EFE6`. Top row: back chevron 44x44, title "Edit story" 20/600 (flex), "Save" pill height 38, padding 0 14, radius 19, `#5B57E8`, white 13.5/600. Cover row: thumb 96 wide, aspect 3/4, radius 14; label "Cover" 12 `#8a8399`; two chips (12.5/600, radius 9, padding 6/10) "First page" (selected: `#5B57E8` on `#ECE7FF`) and "Colour" (`#5a5470` on white). "Title" card (white, radius 18, padding 10/14): label 12 `#8a8399`, value 16/600. One white card (radius 18, rows min height 50, padding 0 14, 1px `#efe9dd` dividers, values right-aligned 14.5/600 `#5a5470`): "Story language" = "English" + 14px chevron; "Translate to my language" with switch 46x28 (off `#d9d2c4`); "Pages" = "5 · reorder, add, remove" + chevron; "Fix the text" = "Edit OCR text" + chevron; "Who’s speaking" = "3 characters" + chevron. Last: "Delete story" outlined button pinned to the bottom, height 48, radius 24, 2px border `#f3c2c4`, text `#C9363B` 15/600 (same confirm as manage mode). Rule from the caption: a text fix re-runs speaker detection, but never overrides an edited (pinned) voice.
- **Ownership of 8b.** Slice 61 owns the screen and its Cover, Title, Pages and Delete rows. "Story language" and "Translate to my language" are wired by Slice 66. "Who’s speaking" opens Slice 65’s cast page. "Fix the text" saves the corrected text and re-runs Slice 59’s parser and caster, which must skip pinned voices (the pin is Slice 65’s "EDITED" state; Slice 60 reads it).

**Open point: Photos source.** Plan lists type, file, scan. The mock adds "Photos" (pick page photos). **Default:** include it; same pipeline as scan (OCR, keep page images) via the image picker.
**Open point: Review step before save.** Plan stores the cast at import. The mock shows a result card with "Check the cast" and "Save story", so nothing is saved until Save. **Default:** follow the mock; "Check the cast" opens Slice 65’s page on the unsaved story.
**Open point: Language rows on the add screen.** "Story language" and "Translate to my language" belong to Slice 66 but sit on this screen. **Default:** hide them here (English only); Slice 66 wires them.
**Settled by the mock: gate from the reading screen.** The gate opens from the lock on the gallery or on any reading screen (caption, line 40); the story pauses while it is open. Slice 62 adds the lock to the reading bar.
**Settled by the mock: accessible hold alternative.** Screen 6b: with TalkBack, VoiceOver or Switch Access on, step 1 is a plain "Continue to the question" button; the sum stays. Detect via the platform accessibility service (Android `AccessibilityManager.IsTouchExplorationEnabled`, iOS `UIAccessibility.IsVoiceOverRunning`; confirm via context7 at implementation). Closes the plan’s risk note.
**Settled by the mock: edit story.** Edit stays in scope (screen 8b), reversing the earlier default of dropping it.
**Open point: Page editing and OCR text editor.** The mock shows the "Pages" row (reorder, add, remove) and the "Fix the text" row (Edit OCR text) but draws neither sub-screen, and the plan has no page editing. **Default:** build the 8b rows for cover, title, language, translation, cast and delete in this slice; "Pages" and "Fix the text" open simple list and text-edit screens made with the same tokens, pages reorder by up/down buttons, and a saved text fix re-runs detection with pinned voices kept.
**Open point: Cover choice "Colour".** The plan says photo cover or generated cover for typed stories; the mock lets any story switch between "First page" and "Colour". **Default:** follow the mock; "Colour" uses the generated cover colours listed above.

New strings (both locales): "My stories", "For grown-ups", "Press and hold for 3 seconds.", "Keep holding…", "Back to the story", "What is", "OK", "Manage stories", "Done", "Add a story", "Cast · {n}", "Edit", "Edit story", "Save", "Cover", "First page", "Colour", "Title", "Pages", "{n} · reorder, add, remove", "Fix the text", "Edit OCR text", "{n} characters", "Delete story", "Continue to the question", "Delete “{title}”?", "Its pages, pictures and voices are removed from this phone.", "Keep", "Delete", "Scan pages", "Keeps the page pictures", "Photos", "Pick page photos", "PDF or file", ".pdf · .docx · .txt", "Type or paste", "Text only", "Story language", "Translate to my language", "{n} pages read · {m} characters found", "Check the cast", "Save story".

## Acceptance

- Add a typed story: it appears in the gallery with a generated cover; after a restart it
  is still there with its cast.
- Add/delete/edit controls are unreachable without hold + sum; reading needs no gate.
- Deleting a story removes `stories/<id>/`.

## Risks / notes

- Hold-3-s must work with TalkBack/VoiceOver; decide the accessible alternative at implementation.
- No sync, no export (Out of Scope).
