---
name: voice-pick
description: Put a listening choice in front of the user and bring back their pick - render the same lines in each candidate voice (or spelling, flavour, pace) with the apps' own packs, publish one private listening page with a play button and a pick per question, wait for the answers, and return them. Use for "pick the default <language> voice", "which voice for the narrator", "listen to these and choose", Slice 195 and any slice whose work starts with the user's ear.
---

# Voice pick

The user's ear decides; this skill only makes listening fast and brings the answer back. It never
changes app code. The caller applies the pick (usually through `implement-slice`).

## 0. Measure or reuse before asking the ear

- A check a tool can measure (silence, clipping, length against the text, loudness, speed: ffmpeg, `run.py`'s length check)
  is measured and decided by the tool; it never goes on a page (Slice 330 was closed by measuring with ffmpeg, #1064).
- A question already answered (the slice file's *Decisions (user, <date>)*, an earlier page's answers) is reused, shown only
  as "your earlier pick" when the candidates are the same, and never asked again.
- Only a choice that is purely about how it sounds goes on the page. If nothing is left, publish no page and say so.

## 1. Render

- Engines and packs as in `tools/dino/sound-words-test/README.md` (`engines.py` mirrors `SherpaEngineFactory.cs`:
  Supertonic, Kokoro, Melo, Piper). Packs from the newest `packs-<n>` release; venv, packs and WAVs live
  outside the repo or in the ignored `tools/dino/voice-listen/out/` folder.
- Dino reads only pre-made clips, rendered with Gemini TTS: render Dino candidates with `tools/dino/premade-audio`
  (`generate.py --batch`, see its README), never the normal per-request route.
- Use the caller's lines; else two everyday sentences in the language plus one with a number and a name.
  The same lines for every candidate, the app's default pace, no flavour unless the question is about flavours.
- Label each clip with the voice id the code uses (e.g. `piper:nb-knv`) and a plain hint (woman/man, from
  the pack's speaker table). Convert to MP3 or OGG at 64-96 kbps to keep the page small.
- To make a pick the default, change the order in code, not in `packs.yml` (no packs release): Norwegian is
  `NorwegianVoices.Ids` (first Id = default, picker order, "Voice N" label), plus the roster in
  `CharacterRosterTenVoice.cs` for Dino. A stored pick that is still installed is kept, never remapped.
- Skip a candidate whose clip is silent or cut (check length against the text, as `run.py` does) and say so.

## 2. Page

- Publish one private Artifact (load `artifact-design` and `artifact-capabilities` first). Audio files go up as
  artifact assets in one publish. Capabilities: `{"assets":{}, "db":{"rules":[{"path":"answers","write":"user"}]}, "user":{}}`.
- One card per question ("Default Norwegian voice", "Dino narrator, woman", ...): every candidate in a row with
  play, and one choice (radio) plus an optional note. A "Done" button writes `answers/<question>` and `answers/done`.
- Order the candidates randomly but the same for every question, so the first one isn't favoured.
- Earlier picks for the same question (from the slice file) are shown preselected and labelled "your earlier pick".

## 3. Wait and return

- Give the caller the page link before the first wait, never only with the final report (a subagent has no message tool, so write
  "page: <link>" into the slice file's decisions the moment the page is published); the link is never held back until the answers arrive.
- Poll `answers` with `ArtifactData` every 5 minutes; stop at `done`, or after 2 hours idle (report "no answer yet",
  the page link, and keep nothing running).
- Write the picks and the page link into the slice file the caller names (under *Decisions (user, <date>)*).
- Report: page link, each question -> picked voice id, notes, skipped candidates. At most 8 lines.
