# Slice 246 — Dino: store books read with pre-made audio from the best voice

*Planned 2026-11-01.* From the user, about Farsi on the iPhone ("it even pronounces the sukun ... isn't there a tehrani
accent?") and the vendors Google lists: "emotions and dedicated Tehrani Standard neural voice option are crucial for this
app which is for young kids!", then: "if generating audio on the servers makes it better, lets do it. a story is 3-5 min,
so it should not take that much space. we can generate it on my machine. create a test page and generate 1 paragraph of
1 of the stories for me via each of these services."

*Tonight's part built 2026-11-02 (night 2026-11-01-2355 wave 2; the user's "Build them all. I will provide the keys tomorrow"), not merged, not yet verified against any live vendor. The slice stays open: work items 2, 4, 5, 6 are untouched and the listening test waits for the user's keys.*

*Changed while building:* `tools/premade-audio` (Python, new): one adapter per service (ElevenLabs v3, Gemini TTS, Azure fa-IR Dilara/Farid, OpenAI gpt-4o-mini-tts, Narakeet, CAMB MARS8). Keys are read from the environment, then `D:/secrets/readthestupidtext/secrets.env`; a missing key gives a `skipped` result, never a failure. `book_text.py` mirrors the `BundledTranslations` speak lookup. `clip_manifest.py` is manifest v2 (text hash plus variant cache key). `render_all.py` renders the Pigs paragraph, the piper-fa mana baseline and a listening-page skeleton. 44 offline tests: `python -I -W error -m unittest discover -s tools/premade-audio`. One run with no keys: every service skipped; the piper-fa baseline skipped too (pack not on disk, `sherpa_onnx` not installed on this PC).

*From review:* files `D:/ReadTheStupidText-worktrees/reviews/s246-review-high.md` (10 findings), `s246-review-medium.md` (8), `s246-review-check-medium.md` (8); and the final check pass `s246-review-check2-medium.md` (8 low/medium findings on the dev tool, no crash or data loss on the main path; all 8 skipped by the coordinator and listed under *Known gaps*: the tool makes no paid call before the user adds keys, and two more fix rounds on the same manifest and splitter code would be patching, not design). All findings read. I did not re-check the code per finding; the brief says the findings were applied except the low ones listed under *Known gaps* in `tools/premade-audio/README.md`, which are skipped (no paid run happens before keys exist):
- Skipped: check-medium 3 (`render_all` has no fatal-answer stop across the voices of one service, so a bad key or 429 is retried per voice).
- Skipped: check-medium 4 (secrets file re-read per lookup).
- Skipped: check-medium 5 (ElevenLabs voice id and Azure region put into the URL unquoted).
- Skipped: check-medium 6 and high 8 / medium 5 (default ElevenLabs, CAMB and Gemini voices unverified for Persian; judge a service by its tuned voice, not the placeholder).
- Skipped: check-medium 8 (`render_piper` rebuilds the model per call and reports a missing import as a quiet "skipped").
- All other findings: applied per the brief, not individually re-verified here.

**Touches, this claim (2026-11-02):** `tools/premade-audio/**` (new), `docs/plan/slices/246-dino-store-books-premade-audio.md`. Read-only: `content/dino/three-little-pigs/**` (story.md, book.json, translations/fa.json, fa.speak.json, fa.marks.json), `tools/sound-words-test/**` (engines.py and friends, reused by import, not edited), `tools/voice-listen/render.py`.

**Tonight's design (coordinator, claim):** Python, like `tools/sound-words-test` and `tools/voice-listen` (reuse `engines.py` for the piper-fa baseline; check the mana voice id there). One adapter module per service in the Research list that has an API: ElevenLabs v3, Gemini TTS (fa-IR preview), Azure (fa-IR Dilara, Farid), OpenAI gpt-4o-mini-tts, Narakeet, CAMB.AI MARS8 (Morphic and Async have no API, so none). Each adapter has one interface (`render(text, voice, out_path)`), reads its key from `D:/secrets` (env var names written in the README; the user adds them with `manage-secrets`), and when the key is missing returns a `skipped: no key` result instead of failing; no paid call runs tonight and no test calls a network. Every vendor request shape comes from current docs (context7 / `library-docs` / `web-research`), cited in the README. A text step builds the listening paragraph: one Farsi paragraph of the Three Little Pigs with the book's `speak:fa` table applied, the same way the app does (`BundledTranslations.SpeakKey`). `render_all` renders the paragraph through every adapter (skipping keyless ones) plus the piper-fa mana baseline (which needs only the downloaded `piper-fa` pack; if the pack is not on disk, say so and skip), and writes a listening-page skeleton (one entry per service, audio or "skipped: no key") that the `voice-pick` agent can publish once keys exist. The per-sentence generator (step 3) gets its manifest shape (sentence text hash, voice, clip path) and the text-hash cache, with unit tests; no app, worker or BookBuilder change tonight (steps 4-5 wait for the pick). Tests: `python -I -m pytest tools/premade-audio` (or `unittest`), offline.

**Depends on:** none for step 1 (the listening test). The build steps wait for the user's pick.
**Touches:** `tools/premade-audio/**` (new), `tools/DinoReader.BookBuilder/**`, `tools/upload-store-story.ps1`,
`src/DinoReader.Core/**`, `src/DinoReader.App/Reading*.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`,
`src/DinoReader.App/StoryVoicing.cs`, `worker/**`, `content/dino/**`, `tests/DinoReader.Tests/**`,
`src/DinoReader.App/CLAUDE.md`, `worker/CLAUDE.md`, `STORE.md`, `.claude/skills/create-dino-story/SKILL.md`

## Why

- On-device voices are what we have today (`piper-fa` mana/ganji/gyro). No offline Persian voice found states a Tehrani
  accent or has emotion control (research 2026-11-01).
- Store books are made by us, so their audio can be made once on the user's PC with the best voice and shipped with the
  book; the phone plays it offline. A 3-5 minute story is a few MB at most per voice cast.
- It also removes espeak from the store-book path (see Slice 245, 2.0.x deferred).

## Research (2026-11-01, docs only, nothing listened to)

- ElevenLabs v3: Persian supported (v3 only), expressive audio tags, multi-speaker; commercial use of output on paid
  plans; about $80 per million characters.
- Gemini TTS: fa-IR in Preview; style, tone and emotion steered by prompt (not confirmed for Persian).
- Azure: fa-IR DilaraNeural and FaridNeural, no styles.
- OpenAI gpt-4o-mini-tts: Persian listed, `instructions` for tone; AI-voice disclosure required; that model shuts down
  2027-01-06.
- Narakeet: Persian voices Reza and Shohreh; commercial use on paid accounts; API on commercial accounts.
- CAMB.AI MARS8: Persian with emotion tags; no public price.
- Morphic and Async: the only ones labelling a Tehrani accent; no API or licence found.
- Not found: any vendor evidence on sukun/harakat/ezafe handling. Google Chirp 3 HD and Amazon Polly: no Persian.

## Decisions (the user, 2026-11-01)

- Pre-made audio for store books if it sounds better; generated on the user's machine.
- First a listening page: one paragraph of one story through each service.

## Work

- [ ] 1. *(partly: code ready 2026-11-02, the listening test waits for the user's keys)* **Listening test.** One Farsi paragraph of the Three Little Pigs (its spoken text with the book's `speak:fa`
  table applied), rendered by every service we have an account for, plus our `piper-fa` mana as the baseline; a private
  listening page (`voice-pick` agent) asking for the pick and notes on accent, emotion and the sukun words. Accounts and
  keys are the user's (none are in `D:\secrets` on 2026-11-01); keys go into the secrets store with `manage-secrets`.
- [ ] 2. Write the user's pick and the licence terms of that vendor for shipping its audio in a paid app into this file.
- [ ] 3. *(partly: manifest and text-hash cache built 2026-11-02; no Opus/AAC encoding or cast routing yet)* **Generator** (`tools/premade-audio`, runs on the user's PC): per book and language, one clip per sentence (so
  the reading highlight still follows), each character in its cast voice, narrator in the narrator voice; Opus/AAC
  output; a manifest with the text hash of each sentence. Resumable, cached by text hash so a small edit re-renders only
  changed sentences.
- [ ] 4. **Package and store:** the audio goes with the store book (inside `.dino` or as a separate download beside it,
  decided by size), uploaded by `upload-store-story.ps1`.
- [ ] 5. **App (Android, iOS, Windows head alike):** when a book's sentence text hash matches a pre-made clip, play the
  clip; otherwise (edited text, a user's own book, a language without audio) read with the on-device voice as today.
  The reader controls (speed, pause, Back) work the same on both paths.
- [ ] 6. Generate for the Farsi store books first, then other languages the user picks.

## Verification

- Step 1: the listening page with every rendered service, and the user's answers written here.
- Unit tests: hash matching, fallback to the device voice for an edited sentence, manifest parsing.
- Android emulator and iOS: a store book plays pre-made clips with the highlight in step; an edited sentence falls back.

## User answers before the night run (2026-11-01, evening)

- Asked what the night should do with the paid listening test: "Build them all. I will provide the keys tomorrow". So tonight builds the render and generator code for every service in the Research list (one adapter per service, each reading its key from the secrets store and skipping cleanly when the key is missing), the piper-fa baseline render and the listening-page skeleton. No paid call runs tonight (there are no keys); the listening test and the pick wait for the keys.

## Waiting on the user

- The vendor keys (env var names are in `tools/premade-audio/README.md`), added with `manage-secrets`. Blocks the listening test (work item 1).

#### For CLAUDE.md

(none from tonight's part)

#### Not yet verified

- Every vendor request shape is unproven until the first keyed run (Gemini model id, ElevenLabs voice id placeholder, Narakeet voice-name casing, CAMB `fa-ir`).
- The piper-fa baseline render, on a PC that has the `piper-fa` pack and `sherpa_onnx` installed.
