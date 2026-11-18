# Premade audio: one language at a time vs three at once (2026-11-18)

**Product:** Dino

Measured while rendering Love from Seagull Island (113 pieces per language, 95 batch requests each, `gemini-3.8-flash-tts`, `generate.py --batch`).
The user asked for the comparison and for the skill to follow the faster way.

## Numbers

One at a time (one `generate.py --batch` per language, each waiting for the last to finish), from the last clip's save time:

| Language | Last clip | Gap to previous |
| --- | --- | --- |
| en | 08:38:09 | (first run, includes setup) |
| ar | 08:46:41 | 8:32 (includes the switch to the next language's dump) |
| da | 08:50:09 | 3:28 |
| de | 08:54:11 | 4:02 |

Steady state: **about 3:45 a language**.

Three at once (each process with its own `--out` folder, so its own lock; `xargs -P3`), start and end per language:

| Language | Start | End | Took |
| --- | --- | --- | --- |
| fr (resumed job) | 10:22:05 | 10:25:12 | 3:07 |
| nb | 10:22:05 | 10:25:37 | 3:32 |
| ja | 10:22:05 | 10:28:42 | 6:37 |
| nl | 10:25:13 | 10:28:46 | 3:33 |
| pt | 10:25:38 | 10:29:11 | 3:33 |
| ru | 10:28:42 | 10:32:07 | 3:25 |
| sv | 10:28:46 | 10:31:44 | 2:58 |
| zh | 10:29:11 | 10:35:49 | 6:38 |

8 languages in 13:44: **about 1:43 a language, 2.2 times faster.** A single job took about the same as when run alone (3-3.5 min), with two slow ones (6.5 min).
An earlier three-at-once round (es, fa, fi) finished all three within about 3.5 minutes of de.

## What went wrong

At 08:57 eight languages were submitted at once (more than the three asked for). Seven were refused with HTTP 429 `RESOURCE_EXHAUSTED`,
"Your project has exceeded its monthly spending cap", and the eighth (fr) sat in `BATCH_STATE_RUNNING` with all 95 requests pending until the user raised the cap at
https://ai.studio/spend. That was the project's monthly spend cap, not a concurrency limit, so eight-at-once was not measured. A 429 whose message names the spend cap
needs the user; a rerun only resumes after they raise it.

## Decision

Render a book's languages **three at a time**, each with its own `--out` folder, then copy each finished language's clips and `manifest.json` into
`content/dino/<book>/audio/<lang>/`. Not more than three until a wider run has been measured. Written into `tools/dino/premade-audio/README.md` (*Batch mode*) and the
create-dino-story skill.
