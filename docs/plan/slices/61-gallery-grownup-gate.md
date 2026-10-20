# Slice 61 — Gallery, stories on the device, grown-up gate

*Planned.*

**Batch:** 10
**Decisions:** 77
**Depends on:** Slices 58, 59, 60; input paths reuse Slice 45 (scan) and the existing file import
**Touches:** `src/StoryReader.Core/**`, `src/StoryReader.App/**`, `tests/StoryReader.Tests/**`, `CLAUDE.md`
**Story issue:** —

## Goal

The home screen is a gallery of the child's stories (cover + title). A grown-up, behind a
simple gate, adds stories (type, file, scan) and removes them. Each story remembers its
cast. Reading never needs the gate.

## Steps

- [ ] `Story` model (title, cover path, language, created, pages, cast) and `IStoryLibrary`
      (list, get, add, update, delete) in `StoryReader.Core`. `Cast` = list of (character id,
      name, gender, voice id), serialised with the story.
- [ ] File-backed `FileStoryLibrary` in `StoryReader.App`: one folder per story
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

## Acceptance

- Add a typed story: it appears in the gallery with a generated cover; after a restart it
  is still there with its cast.
- Add/delete/edit controls are unreachable without hold + sum; reading needs no gate.
- Deleting a story removes `stories/<id>/`.

## Risks / notes

- Hold-3-s must work with TalkBack/VoiceOver; decide the accessible alternative at implementation.
- No sync, no export (Out of Scope).
