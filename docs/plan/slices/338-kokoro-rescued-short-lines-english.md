# Slice 338: Listening page: Kokoro rescued short lines (English)
**Product:** Shared

*Dropped 2026-11-08 (see the end of this file); planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 210's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Live:** Slice 210 (PR #631): listening to the rescued lines (the user's ear); Spanish, Portuguese and Chinese Kokoro voices (`ef_dora`, `pf_dora` have no sibling); iOS compile only ([slice](slices/210-kokoro-short-line-dropped.md)).

**Touches:** `docs/plan/slices/338-kokoro-rescued-short-lines-english.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [ ] Make the audio exactly as the app plays it: render it with the app's own engine and packs (the `voice-pick` skill's render path), or record it from the dev app on the S21 when rendering cannot reproduce it (speed changes, timing, a Sonos or store track). Note how each clip was made and its build or pack version.
- [ ] Publish one private listening page through the repo's `voice-pick` agent: a play button per clip, the line of text it reads, and for each clip "sounds right" or "something is wrong" with a free-text box for what is wrong (and a timestamp when it helps). English and Farsi only.
- [ ] When the user has answered, write every answer into this file, word for word.
- [ ] Each "something is wrong" becomes a fix item in this slice (cause, then fix, through the normal review gate). Every "sounds right" closes the check.

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.

## Dropped (2026-11-08)

No shipped app speaks with Kokoro, so there is nothing for the user to hear. Dino Reader has no speech engine since Slice 278 part B (`src/DinoReader.App/CLAUDE.md`, *No phone voice*; `NoSpeechEngineTests`); its roster files only name voices. Read The Stupid Text never reaches a Kokoro voice: `kokoro-en` is a storyteller pack registered only by Dino (`PackConstants.cs`, `MobileVoiceModelService.cs`), Windows lists no Kokoro pack (`WindowsVoicePackService.cs`), and `SiblingVoice` moves a Kokoro voice only to another Kokoro voice. A listening page built on 2026-11-08 (17 English Kokoro voices rendered on the PC) tested that dead path; it was withdrawn before the user answered. The user had pointed out that a sibling page (Slice 332) "seems to be using the old tts engine".
