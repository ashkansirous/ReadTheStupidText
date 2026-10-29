---
name: voice-pick
description: Put a listening choice in front of the user and bring back their pick - render the same lines in each candidate voice (or spelling, flavour, pace) with the apps' own packs, publish one private listening page with a play button and a pick per question, wait for the answers, and return them. Use for "pick the default <language> voice", "which voice for the narrator", "listen to these and choose", Slice 195 and any slice whose work starts with the user's ear.
---

# Voice pick

The user's ear decides; this skill only makes listening fast and brings the answer back. It never
changes app code. The caller applies the pick (usually through `implement-slice`).

## 1. Render

- Engines and packs as in `tools/sound-words-test/README.md` (`engines.py` mirrors `SherpaEngineFactory.cs`:
  Supertonic, Kokoro, Melo, Piper). Packs from the newest `packs-<n>` release; venv, packs and WAVs live
  outside the repo or in the ignored `tools/voice-listen/out/` folder.
- Use the caller's lines; else two everyday sentences in the language plus one with a number and a name.
  The same lines for every candidate, the app's default pace, no flavour unless the question is about flavours.
- Label each clip with the voice id the code uses (e.g. `piper:nb-knn`) and a plain hint (woman/man, from
  the pack's speaker table). Convert to MP3 or OGG at 64-96 kbps to keep the page small.
- Skip a candidate whose clip is silent or cut (check length against the text, as `run.py` does) and say so.

## 2. Page

- Publish one private Artifact (load `artifact-design` and `artifact-capabilities` first). Audio files go up as
  artifact assets in one publish. Capabilities: `{"assets":{}, "db":{"rules":[{"path":"answers","write":"user"}]}, "user":{}}`.
- One card per question ("Default Norwegian voice", "Dino narrator, woman", ...): every candidate in a row with
  play, and one choice (radio) plus an optional note. A "Done" button writes `answers/<question>` and `answers/done`.
- Order the candidates randomly but the same for every question, so the first one isn't favoured.
- Earlier picks for the same question (from the slice file) are shown preselected and labelled "your earlier pick".

## 3. Wait and return

- Poll `answers` with `ArtifactData` every 5 minutes; stop at `done`, or after 2 hours idle (report "no answer yet",
  the page link, and keep nothing running).
- Write the picks and the page link into the slice file the caller names (under *Decisions (user, <date>)*).
- Report: page link, each question -> picked voice id, notes, skipped candidates. At most 8 lines.
