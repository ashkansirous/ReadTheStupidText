# Dino on Sonos: browse screens (Claude Design copy)

- **Source:** https://claude.ai/artifact/PTSoGuZ6cNNseQXtwNsXPd (a Design canvas, "Dino on Sonos: browse screens"),
  version 1795561197-1582.
- **Copied:** 2026-11-24, with the Artifact tool's `read` of every `project/*` file (byte-for-byte, sha256 checked by the
  tool). The canvas index is `canvas.json`; one `.dc.html` file per artboard. The `/_blob/...` cover images are uploads on
  the canvas and are not copied (they are the store covers of the sample books).
- **Slices built from it:** 437 (anonymous browse), 438 (sign in once, story language), 439 (owned books), 440 (search).

## Artboards

| Title | File | Size |
| --- | --- | --- |
| Browse tree and rules | Main.dc.html | 1600x980 |
| English 1 · First screen | EN-Home.dc.html | 390x844 |
| English 2 · Bedtime shelf | EN-Bedtime.dc.html | 390x844 |
| English 3 · Other languages | EN-Languages.dc.html | 390x1100 |
| English 4 · Search | EN-Search.dc.html | 390x844 |
| Farsi 1 · First screen (RTL) | FA-Home.dc.html | 390x844 |
| Farsi 2 · Bedtime shelf | FA-Bedtime.dc.html | 390x844 |
| Farsi 3 · All stories, Persian A–Z | FA-All.dc.html | 390x844 |
| Farsi 4 · Other languages | FA-Languages.dc.html | 390x1100 |
| Setting 1 · Once: Sonos opens the Dino app | Link.dc.html | 390x844 |
| Setting 2 · First screen, English | SET-Home.dc.html | 390x844 |
| Setting 3 · Pick the story language | SET-Pick.dc.html | 390x1220 |
| Setting 4 · Saved: Bedtime now plays Farsi | SET-Saved.dc.html | 390x844 |
| Setting 5 · Farsi phone: same row | SET-FA-Home.dc.html | 390x844 |

The canvas note beside the Setting row ("How it is saved") is in `canvas.json` under `notes.setnote`.

## Reading and refreshing

Read the words, order and subtitles from these files (strip the tags; every row is a title `div` then a subtitle `div`).
Sonos draws its own screens, so colours and fonts here are only the mock's stand-in for the Sonos app; the Worker
controls titles, subtitles, art and order. To refresh, re-read every `project/*` file of the canvas with the Artifact
tool into this folder; never hand-edit a copy.

## The owner's import prompt (2026-11-24, verbatim)

> Redesign Dino Reader's Sonos music service (Slice 283, worker/sonos.ts) from the design on this canvas:
> https://claude.ai/artifact/PTSoGuZ6cNNseQXtwNsXPd
>
> Read plan.md, worker/CLAUDE.md, docs/plan/slices/283-dino-sonos-music-service.md and worker/sonos.ts first.
> Plan this as new Dino slices (one slice file each, **Product:** Dino, rows in plan.md's Dino Reader table)
> before writing any code, and show me the slice list.
>
> (Slices A-D and the sandbox tests follow; they are copied in full into the Why section of slices 437-440.)
