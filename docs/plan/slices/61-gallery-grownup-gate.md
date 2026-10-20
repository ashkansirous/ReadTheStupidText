# Slice 61 — Gallery, stories on the device, grown-up gate

*Planned.*

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

#### Design (Dino Reader v2 mock, screens 1, 6, 7, 8, 9, added 2026-10-20)

Source: `docs/design/dino-reader/Dino Reader v2.dc.html`. Five frames (gallery line 23-27, gate hold 37-39, gate sum 39-40, manage mode 40-46, add story 46-56). The mock agrees with the plan on the gate rules and adds scope on the add flow (see Open points). Tokens: Slice 58 Design. Striped boxes tagged "first page photo" are placeholders for a real photo, not UI.

- **Gallery (screen 1).** Page `#FFF6E6`. Top bar: app icon 40x40 radius 11, title "My stories" 22/600 (flex), lock button 44x44 radius 14, fill `rgba(43,37,64,.07)`, line lock icon (20px, stroke 2, `#8a8399`). Grid 2 columns, 12px gap, 14px side padding. Cover: aspect 3/4, radius 16, shadow `0 4px 0 rgba(43,37,64,.08)`. Title under cover 14/600, line-height 1.25, gap 7. Generated cover: flat colour, title centred in Andika 18/700 white, `text-shadow: 0 1px 0 rgba(0,0,0,.12)`; colours seen `#7FD0FF`, `#FFC24D`, `#B39CFF`, `#FF9FB8`. Photo cover: first page photo. No gate for reading.
- **Gate step 1 (screen 6).** Bottom sheet over a dimmed page (scrim `rgba(43,37,64,.55)`), white, top radius 28, padding 24/22/28, centred, gap 12. Two step bars 26x5 radius 3 (`#5B57E8` active, `#e6e0d4` idle). Title "For grown-ups" 22/600. Body "Press and hold for 3 seconds." 14.5 `#5a5470`, max width 260. Hold control 120x120: track circle r54 stroke 8 `#ECE7FF`, progress arc `#5B57E8` stroke 8 round caps, inner button 86 circle `#5B57E8` with white lock icon 20px. Hint "Keep holding…" 13/500 `#8a8399`. Cancel pill "Back to the story" height 48, padding 0 26, radius 24, fill `#F3EFE6`, 15/600. The note says the story pauses.
- **Gate step 2 (screen 7).** Same sheet, both step bars `#5B57E8`. Label "What is" 15 `#5a5470`; sum "7 + 12 = 19" 40/700, letter-spacing 1, answer slot min-width 64 with 4px `#5B57E8` underline and `#5B57E8` digits. Number pad 3 columns, 8px gap: keys height 52, radius 14, fill `#F3EFE6`, 22/600; last row "⌫", "0", "OK" (OK fill `#5B57E8`, white). Sum = two numbers 6 to 19; a wrong answer rolls a new sum (matches the plan). The same gate guards adding, deleting and editing a story, and the cast screen.
- **Manage mode (screen 8).** Page `#F3EFE6`. Title "Manage stories" 20/600; "Done" pill height 38, padding 0 14, radius 19, `#5B57E8`, 13.5/600 white. Same grid. First tile "Add a story": 3px dashed `#c9c0f5` border, white, radius 16, plus icon 28 `#5B57E8`, label 14/600 `#5B57E8`. Each story: delete badge 32 circle `#E5484D` at top-right offset -6/-6, white trash icon 16, shadow `0 2px 6px rgba(0,0,0,.2)`; title 13.5/600; link "Cast · 3 characters" 12/600 `#5B57E8` with a 14px chevron (opens Slice 65). Delete confirm: scrim `rgba(43,37,64,.45)`, white card radius 24 padding 22, title "Delete “The Little Lighthouse”?" 19/600, body "Its pages, pictures and voices are removed from this phone." 14 `#5a5470`, buttons height 48 radius 24: "Keep" (`#F3EFE6`) and "Delete" (`#E5484D`, white).
- **Add a story (screen 9).** Back arrow, title "Add a story" 20/600. Four source tiles in a 2x2 grid, gap 10, white, radius 18, padding 14, min height 104, icon chip 42 radius 12 `#ECE7FF`: "Scan pages" / "Keeps the page pictures"; "Photos" / "Pick page photos"; "PDF or file" / ".pdf · .docx · .txt"; "Type or paste" / "Text only". Titles 15/600, subtitles 11.5 `#8a8399`. Settings card (white, radius 18, rows min height 50): "Story language" = "English · detected" with chevron; "Translate to my language" with a switch 46x28 radius 14 (off `#d9d2c4`, knob 24 white). Result card pinned to the bottom: fill `#2b2540`, radius 18, padding 14: green tick + "3 pages read · 3 characters found" 14.5/600; name chips (12.5, `rgba(255,255,255,.12)`, radius 10, padding 4/10) "Peter", "Cotton Tail", "Mum"; two buttons height 44 radius 22: "Check the cast" (`rgba(255,255,255,.14)`) and "Save story" (`#FF7A59`). Who speaks is worked out once, at import, in the story’s own language, before any translation.

**Open point: Photos source.** Plan lists type, file, scan. The mock adds "Photos" (pick page photos). **Default:** include it; same pipeline as scan (OCR, keep page images) via the image picker.
**Open point: Review step before save.** Plan stores the cast at import. The mock shows a result card with "Check the cast" and "Save story", so nothing is saved until Save. **Default:** follow the mock; "Check the cast" opens Slice 65’s page on the unsaved story.
**Open point: Language rows on the add screen.** "Story language" and "Translate to my language" belong to Slice 66 but sit on this screen. **Default:** hide them here (English only); Slice 66 wires them.
**Open point: Gate from the reading screen.** The hold-step backdrop shows a paused story, so the gate seems to open from reading. The reading top bar in the mock has no lock. **Default:** the gate opens from the gallery lock only; the paused backdrop is illustrative.
**Open point: Edit story.** Plan gates "edit story"; the mock has no edit action beyond the Cast link. **Default:** drop edit from this slice.
**Open point: Accessible hold alternative.** Still open from the plan’s risks; the mock shows only the hold ring. **Default:** decide at implementation.

New strings (both locales): "My stories", "For grown-ups", "Press and hold for 3 seconds.", "Keep holding…", "Back to the story", "What is", "OK", "Manage stories", "Done", "Add a story", "Cast · {n} characters", "Delete “{title}”?", "Its pages, pictures and voices are removed from this phone.", "Keep", "Delete", "Scan pages", "Keeps the page pictures", "Photos", "Pick page photos", "PDF or file", ".pdf · .docx · .txt", "Type or paste", "Text only", "Story language", "Translate to my language", "{n} pages read · {m} characters found", "Check the cast", "Save story".

## Acceptance

- Add a typed story: it appears in the gallery with a generated cover; after a restart it
  is still there with its cast.
- Add/delete/edit controls are unreachable without hold + sum; reading needs no gate.
- Deleting a story removes `stories/<id>/`.

## Risks / notes

- Hold-3-s must work with TalkBack/VoiceOver; decide the accessible alternative at implementation.
- No sync, no export (Out of Scope).
