# premade-audio (Slice 246)

Renders a Dino book's spoken sentences with Gemini TTS, one request and one clip per sentence, on the user's PC. Python, stdlib only (plus `ffmpeg` for AAC).
The sentences come from the app's own code through the BookBuilder, so clip N is the app's piece N and its `sentence` is the highlight.

## Key

`GEMINI_API_KEY`, read from the environment, then from `D:/secrets/readthestupidtext/secrets.env` (`KEY=VALUE` lines; override the path with
`PREMADE_AUDIO_SECRETS`). With no key the run stops at once with `skipped: no key` and sends nothing. Add it with the `manage-secrets` skill.

## Run

```bash
# 1. the sentences the app reads aloud (store or starter book, one language): index, highlight sentence, spoken text, source text, speaker, narrator flag
dotnet run --project tools/DinoReader.BookBuilder -- dump-sentences three-little-pigs --lang fa --out tmp/pigs-fa.json
# 2. one clip per sentence (add --dry-run first to see how many requests it needs; sends nothing)
python tools/premade-audio/generate.py --sentences tmp/pigs-fa.json --out artifacts/premade-audio --free --max-requests 10
# offline tests
python -I -W error -m unittest discover -s tools/premade-audio
```

The text is the displayed translation (`fa.json`), plain: the `fa.speak.json` "how to read" table is for the on-device voice only (user, 2026-11-03). Pieces with no
letter or digit are left out. The animal sounds the app adds after some lines (pigs, wolf; `"sound": true` in the dump, `StoryVoicing.WithAnimalSounds`) are listed in the app's order with their own index and clip,
and the highlight `sentence` is the one holding the segment's `SourceStart`, as in the app. Clips go to `<out>/<book>/<language>/<hash>-<voice>-<variant>.m4a` with `manifest.json`.

## Cast: `content/dino/<book>/audio-cast.json`

Narrator voice and style, a voice and style per speaker name (as in the dump), an optional `direction` per character, optional `directions` by text hash (SHA-256 of the NFC
text; a hash follows the text if lines move) and optional `lines` for single lines: `{"speaker", "source", "occurrence"?, "direction"?, "say"?, "lang"?}`. A line is found by
the dump piece's `speaker` (`null` for the narrator) and English `source`, which are the same in every language, never by index (each language's dump orders its pieces
differently), so one cast serves every language. `occurrence` (1-based, in dump order) is needed only when that speaker says that source more than once. An entry that matches no
piece, matches several without an `occurrence`, or sets a field of one piece twice fails the run before any request. `direction` applies in every language. The delivery sent to Gemini is `<style>; <direction>`
(`speech_metadata.style`). A speaker with no entry fails the run before any request. An entry cannot target an animal-sound piece (`"sound": true`): its source is localized and the count differs by language, so only narration and spoken lines can be keyed. `check(sentences, language)` must run before any delivery (the language is a lower-case code such as `fa`). Format and rules: `audio_cast.py`. Fixture dumps for the Pigs binding test: `tools/premade-audio/testdata/` (regenerate with `dump-sentences three-little-pigs --lang en|fa --out ...`).

### `say`: a Gemini-only pronunciation fix per line (A6)

A line entry takes `say` next to (or instead of) `direction`: the text Gemini reads for that line in place of the book text, e.g. one word with a vowel mark.
`{"say": "...", "lang": "fa", "speaker": null, "source": "One evening, a big grey Wolf came walking down the road."}`. A `say` is language-specific, so it needs `lang` and applies
only to that language's run (other languages still match it against the dump so a stale entry fails, but do not apply it; its direction, if any, applies everywhere). The clip name follows the spoken text (the manifest `variant` stays the service and settings tag), so
changing a `say` re-renders that line only, and lines with the same `say`, voice and delivery share one clip. The manifest entry keeps the book `text` and `hash` (the app
matches clips by them) and records the `say`. The book text and the on-device voice are untouched.

Listening test for 2-3 spellings of one line (sends one request per candidate, counted in the budget like any other; writes no manifest entry):

```bash
python tools/premade-audio/generate.py --sentences tmp/pigs-fa.json --out artifacts/premade-audio --variants 17 --say "<spelling 1>" --say "<spelling 2>" --free
```

The files land in `<out>/<book>/<language>/variants/<index>/1.m4a`, `2.m4a`, ... in the line's own voice and delivery (its `say` is ignored); a rerun replaces them (each only when its new clip is saved; after a complete run, files numbered beyond the candidates are removed).
Put the chosen spelling into `audio-cast.json` as that line's `say` and run a normal render.

## Manifest (version 3)

`manifest.json` = `{"version": 3, "book", "language", "entries": [{"index", "sentence", "hash", "voice", "direction", "variant", "clip", "text"}]}`.
`variant` = `gemini-tts-<tag>`, the tag a hash of the request (model, voice, style incl. direction) built for a probe text. The clip file name is derived from text hash, voice and
variant, so a cache hit is "that file exists": an edited sentence renders one new clip, a changed voice, style or direction re-renders just the lines it touches, everything
else is reused. Saved after every clip; a rerun resumes. A manifest of another version is kept as `manifest.json.bak` and the list starts empty.

## Limits and budget

| Flag | Meaning |
| --- | --- |
| `--free` | free tier preset: 3 requests/min, 10/day (every Gemini TTS model, read off the AI Studio Rate Limit page on 2026-11-03) |
| `--rpm N`, `--rpd N` | override; `--rpm 0` no pacing, `--rpd 0` no daily cap. Without `--free` the default is 10/min and no daily cap: a **placeholder**, read the paid values off the Rate Limit page once billing is on |
| `--max-requests N` | most requests in this run (default 200, so a typo cannot run a long book unbounded on a paid key; 0 = no limit) |

One run per output folder: `<out>/.run.lock` is an OS lock (dropped if the process dies); a second run exits 7. Exit codes: 0 done or stopped at a budget/daily limit, 1 sentences failed
(or three errors in a row), 2 no ffmpeg, 3 no key, 4 401/402/403, 5 encode failure (the paid WAV is kept and encoded on the next run with no new request), 6 a second 429, 7 folder locked.
A clip that trims to under 0.1 s (silent) counts as an error and is requested again on a rerun. Manifest entries that no longer match the dump (text, voice or variant) are dropped at the start of a run.

Requests are spaced to the per-minute limit; the daily count is a rolling 24 hours kept in `<out>/usage.json` (never looser than the vendor's calendar reset), so a later run
carries on where the day stopped and prints when the next slot opens. The run ends cleanly (exit 0, reason printed) at the budget, the daily limit, a 401/402/403, or a second 429;
the first 429 waits the vendor's retry-after once (header, else the body's `retryDelay`, default 60 s; over 300 s ends the run) and retries that sentence. Three errors in a row end it too.

## Encoding

`ffmpeg` (on PATH or `$FFMPEG`), native `aac` encoder, mono 48 kb/s, `-movflags +faststart`, leading and trailing silence cut at -50 dB with a 0.05 s pad kept
(`silenceremove` forward and on the reversed audio). Without ffmpeg the run exits with a message; `--format wav` keeps the vendor's WAV (24 kHz mono 16-bit) instead.

## Request shape (docs read 2026-11-03)

Gemini Interactions API, single speaker: https://aistudio.google.com/docs/speech-generation (the ai.google.dev URL redirects there). `POST /v1beta/interactions`,
model `gemini-3.8-flash-tts`, text part with a `speech_metadata` annotation (`style`), `generation_config.speech_config: [{"voice": ...}]`, WAV in
`steps[].content[]` (`type: "audio"`). Proven live for one sentence (voice Hali) in A0. Voices Hali, Elio, Cleo are not in the doc's 30-voice list (the extended voice library).

## Known gaps

- Voices in `audio-cast.json` other than Hali are chosen from the documented descriptors, not yet heard.
- The day count is rolling, not Pacific-calendar; it can wait a little longer than needed, never go over.
