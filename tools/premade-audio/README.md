# premade-audio (Slice 246)

Renders a Dino store book's Farsi text through several cloud TTS services (and the on-device `piper-fa` mana baseline) so the user can pick the best voice,
and holds the core of the per-sentence clip generator (manifest and text-hash cache). Python, stdlib only for the adapters. Runs on the user's PC.
No app, worker or BookBuilder change yet: those steps wait for the pick.

## Keys (the user adds them; none exist yet)

An adapter with a missing key returns `skipped: no key (<ENV VAR>)` and sends nothing. The key is read from the environment, then from
`D:/secrets/readthestupidtext/secrets.env` (the repo's folder in the secrets store; `KEY=VALUE` lines; override the path with `PREMADE_AUDIO_SECRETS`).
Environment variables win over the file. Add them with the `manage-secrets` skill (store `ashkansirous/secrets`, folder `readthestupidtext`). A skipped
result names the file it searched.

| Service | Module | Env vars |
| --- | --- | --- |
| ElevenLabs v3 | `adapter_elevenlabs.py` | `ELEVENLABS_API_KEY` |
| Gemini TTS | `adapter_gemini.py` | `GEMINI_API_KEY` |
| Azure Speech fa-IR (Dilara, Farid) | `adapter_azure.py` | `AZURE_SPEECH_KEY`, `AZURE_SPEECH_REGION` |
| OpenAI gpt-4o-mini-tts | `adapter_openai.py` | `OPENAI_API_KEY` |
| Narakeet (reza, shohreh) | `adapter_narakeet.py` | `NARAKEET_API_KEY` |
| CAMB.AI MARS8 | `adapter_camb.py` | `CAMB_API_KEY` |

Morphic and Async have no API: no adapter.

## Run

```bash
python -I -m pytest tools/premade-audio          # offline tests (pytest absent here: python -I -m unittest discover -s tools/premade-audio)
python tools/premade-audio/render_all.py --out <dir> [--paragraph 0] [--piper-fa-root <unzipped piper-fa folder>]
```

`render_all` takes paragraph N of `content/dino/three-little-pigs` (default: the first, two sentences), applies the `speak:fa` table, renders it through
every adapter that has a key plus the piper-fa mana baseline, and writes `results.json` and a listening-page skeleton `index.html` into `<dir>`
(one entry per service and voice: audio, or why it was skipped). Run it with plain `python` (not `-I`): the baseline imports
`tools/sound-words-test/engines.py` (needs `sherpa-onnx==1.13.8` and numpy). The piper baseline needs the `piper-fa-2.zip` of release `packs-7` unzipped
(see `tools/sound-words-test/README.md`); pass `<unzipped>/piper-fa` or set `RTST_PIPER_FA_ROOT`. Without it the baseline is `skipped`.
Then hand `<dir>` to the `voice-pick` agent to publish.

## How the text is built

`book_text.py` mirrors `src/DinoReader.Core/BundledTranslations.cs`: `SpeakKey(language)` (line 126) = `"speak:" + language`, and `LookupSpoken`: the book's
`translations/fa.speak.json` ("how to read", vowel marks) first, then `fa.json`, else the source text. Pieces come from `translations/pieces.json`;
a paragraph is the pieces found in order in one `story.md` paragraph.

## Clip generator core

`clip_manifest.py`: `manifest.json` = `{"version": 2, "entries": [{"hash", "voice", "variant", "clip", "text"}]}`. `hash` = SHA-256 of the NFC sentence text;
`variant` = `<service>-<settings tag>` (hash of the request built for a probe text: URL, model, style, settings). Same sentence + voice + variant with the clip
file present is a cache hit; an edited sentence, another service or a changed model/style re-renders; the clip name carries the sanitised voice and variant.
Saved after every clip, so a run resumes. The loop stops at the first skipped result (no key) or a 401/402/403/429 answer, and after 3 errors in a row.
A vendor HTTP error comes back with the first 500 characters of the response body. Gemini audio without a RIFF header is wrapped as 24 kHz mono 16-bit WAV.
Narakeet text over 1024 UTF-8 bytes is `unsupported` (not an error).
`render_sentences(sentences, voice, adapter, folder)` is the loop; character casting and Opus/AAC encoding come with the pick.

## Known gaps (low severity, no paid run happens before keys exist)

- `render_all` has no fatal-answer stop across voices (a bad key or 429 is retried per voice of a service).
- The secrets file is re-read per lookup; ElevenLabs voice id and Azure region go into the URL unquoted.
- Default voices for ElevenLabs, CAMB and Gemini are unverified for Persian; judge a service by its tuned voice, not the placeholder.
- `render_piper` rebuilds the model per call and reports a missing import as a quiet "skipped".
- Manifest load does not check each entry has a `clip` or compare `version`; a second unreadable manifest overwrites the earlier `.bak`.
- `pieces_in` does not handle a newline inside a sentence, guillemets or single curly quotes (raises ValueError, never drops text).
- The error-body check misses plain-text or BOM-led bodies; a disk-full write can leave a truncated clip (no temp-file write).
- `--paragraph` out of range gives a raw IndexError.

## Request shapes (docs read 2026-11-02)

- ElevenLabs: https://elevenlabs.io/docs/api-reference/text-to-speech/convert , https://elevenlabs.io/docs/overview/models . Audio-tag support on `eleven_v3` not confirmed; voice id is a placeholder.
- Gemini: https://ai.google.dev/gemini-api/docs/speech-generation (Interactions API; Persian auto-detected; older generateContent shape not used).
- Azure: https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech , https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts
- OpenAI: https://developers.openai.com/api/reference/resources/audio/subresources/speech/methods/create (model shuts down 2027-01-06; Persian not stated).
- Narakeet: https://www.narakeet.com/docs/automating/text-to-speech-api/ , https://www.narakeet.com/languages/persian-text-to-speech/ (voice-name casing unconfirmed; 1 KB short API).
- CAMB.AI: https://docs.camb.ai/api-reference/endpoint/create-tts-stream.md (`fa-ir` on every model not confirmed).

Every shape is unproven against the live service until the first keyed run; a 4xx there comes back as an `error` result with the message.
