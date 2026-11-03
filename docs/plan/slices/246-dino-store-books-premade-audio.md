# Slice 246 — Dino: store books read with pre-made audio from the best voice

*Planned 2026-11-01.* From the user, about Farsi on the iPhone ("it even pronounces the sukun ... isn't there a tehrani
accent?") and the vendors Google lists: "emotions and dedicated Tehrani Standard neural voice option are crucial for this
app which is for young kids!", then: "if generating audio on the servers makes it better, lets do it. a story is 3-5 min,
so it should not take that much space. we can generate it on my machine. create a test page and generate 1 paragraph of
1 of the stories for me via each of these services."

*Tonight's part built 2026-11-02 (night 2026-11-01-2355 wave 2; the user's "Build them all. I will provide the keys tomorrow"), not merged, not yet verified against any live vendor. The slice stays open: work items 2, 4, 5, 6 are untouched and the listening test waits for the user's keys.*

*Park note (2026-11-02, final feedback loop of night 2026-11-01-2355):* the row was `parked: waits for the user's vendor keys (listening test, work item 1); items 2-6 wait for the pick (adapters, generator core, listening-page skeleton merged #729)`. Moved back to `planned` per the user's 2026-11-01 answer (a night's last loop un-parks every parked row so the next night retries it). The next wave should check `D:/secrets/readthestupidtext/secrets.env` for the vendor keys first and re-park if they are still missing.

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

*Re-planned 2026-11-03 (the user: "stick to gemini", "agree. lets plan it"):* Gemini only, plain book text, Farsi first on a free key, then the other languages on the user's GCP credit. The new work list is under **Work (re-planned 2026-11-03)**; it replaces the old one, which stays below for the record. Split into parts A-D with separate Touches so A and C can run in one wave.

**Depends on:** A first (B and D read its manifest); C can start alongside A once A's manifest shape (A2) is merged.
**Touches, part A (generator):** `tools/premade-audio/**`, `content/dino/*/audio-cast.json` (new, one per book), `tools/DinoReader.BookBuilder/**` (a sentences-as-JSON command only).
*Part A in progress 2026-11-03 (branch `feat/s246-a-gemini-generator`).*
*Changed while building (part A, 2026-11-03):* `tools/premade-audio` is now Gemini only: `generate.py` (CLI + `Generator`), `audio_cast.py`, `pacing.py`, `encode.py` (ffmpeg `aac` to `.m4a`, silence trimmed), `clip_manifest.py` v3; BookBuilder `dump-sentences <stem> --lang <code>` prints the app's pieces as JSON (`SentenceDump.cs`, tests in `SentenceDumpTests.cs`); `content/dino/three-little-pigs/audio-cast.json` (Hali narrates; Cleo, Puck, Achird, Elio for Pip, Pop, Pim, Wolf, chosen from descriptors, not heard yet). No live call made. A clip is per app piece (a quote and its "said Pip" tag are two pieces of one highlighted sentence), so the dump carries both `index` and `sentence`.

*Part A built 2026-11-03, after review:* the dump also lists the app's animal-sound segments (87 entries for Pigs in Farsi, not 78); per-line directions are guarded by speaker and English source text; stale manifest entries are dropped; exit codes per cause; a lock per out folder; `--max-requests` defaults to 200; a daily 429 ends the run at once; silent clips stop a run after 3 in a row or 5 in total. 51 Python tests, 2,276 Dino tests. Billing (user, 2026-11-03): £4 prepaid ($5), auto-reload off, **monthly spend cap £5** (the user's choice; may stop the full library halfway through a month, not checked whether credit-paid usage counts toward it). First live run (Pigs, Farsi) started 18:10 with `--rpm 10`.
*From review:* `D:/ReadTheStupidText-worktrees/reviews/s246a-review-high.md` (14; 11 applied, 6/8/9 skipped as style or proven in AI Studio), `s246a-review-medium.md` (9; 1-6 applied, 7-9 skipped low), `s246a-review-check-medium.md` (4 new; 1, 2, 4 applied, 3 low skipped), `s246a-review-check2-medium.md` (no findings). Skipped and still open: re-render of every clip when the request format changes (medium 7); `pending_count` double-counts duplicate stems (check 3); non-atomic WAV write and `.bak` overwrite (older gaps).
**Touches, part B (package and store):** `tools/DinoReader.BookBuilder/**`, `tools/upload-store-story.ps1`, `worker/**`, `worker/CLAUDE.md`, `STORE.md`.
**Touches, part C (app playback):** `src/DinoReader.Core/PremadeAudio*.cs` (new), `src/DinoReader.App/Reading*.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/Platforms/**`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`.
**Touches, part D (generation runs):** generated audio outside git (store uploads only), this slice file.

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

## Decisions (the user, 2026-11-03, listening round 1, free routes)

Page: https://claude.ai/artifact/MkAx2edzi4WAiCWMJKfL9h (Three Little Pigs paragraph 0).
- Gemini 3.8 Flash TTS, voice Hali (female), via AI Studio, style prompt "warm, gentle, expressive storyteller voice for young children, standard Tehrani accent": rating 5, accent Tehrani yes, warmth 5, no wrong words.
- piper-fa mana (app today): rating 2, accent Tehrani no, warmth 2, no wrong words noted.
- Overall: worth trying more? Gemini only. Note: "we can do it without paying. why pay?" (no paid services; Elio, Finn, Cleo are the next voices to hear).

## Decisions (the user, 2026-11-03, after round 1)

- **Gemini only.** "stick to gemini". The other vendor adapters leave scope (A1 removes them).
- **No paid services; free first.** "we can do it without paying. why pay?" The Gemini API free tier first; the user's
  billing account "AlphaAgent" (Paid tier 1, £15.04 eligible GCP credits, 0 projects linked on 2026-11-03) only if the free
  limits are too tight, and then only with a monthly spend cap at or under the credit set first.
- **Plain text for Gemini.** The user asked why we still render with vowel marks: Gemini reads the book's plain text (the
  `fa.json` lookup without `fa.speak.json`). Vowel marks and the "how to read" tables stay for the on-device voice only.
  If a listening round shows Gemini misreading a word, that word gets a Gemini-only fix, never the whole marked text.
- **Order:** Farsi first for every store book (16 on main, ~1,200 sentences), on the free key; then the other 14 languages
  (~16,800 sentences) on the free key or the capped credit, whichever the free limit allows. Estimate given to the user:
  about 16 hours of audio in total, about £10 at the 2026 price (it doubles from 2027-01-01).
- **Per-sentence clips** (so the reading highlight follows), not per page.

## Decisions (the user, 2026-11-03, evening)

- Slice 154 (Farsi vowel marks) dropped: "Gemini is good and I dont need to do anything manually."
- **Farsi first on the free plan, then decide on paying:** "I think £15 should be more than enough for most of the work.
  lets do the farsi voices before deciding."
- **Each sentence saved as its own clip:** "save the voices separately. I might want to change the text of the books here
  and there and for that, we should just generate a new voice for the changed text." So a changed sentence re-renders
  alone (one request), nothing else.

## Part A design (coordinator, 2026-11-03)

- **One request per sentence, one clip per sentence.** The user chose the $5 prepay (2026-11-03: "ok, do $5 prepay"), so
  the paid tier's limits apply and no page-sized requests or cutting at pauses are needed (that idea, for the 10-a-day
  free tier, is dropped). The project gets a spend cap at or under the £15.04 credit and auto-reload stays off.
- **Edits re-render one sentence.** The manifest is keyed by each sentence's text hash (plus voice and direction); an
  edited sentence misses the cache and gets one single-sentence request; unchanged sentences keep their clips.
- **The app's sentence split, not a copy.** A BookBuilder command prints each book's sentences for a language (index,
  text, speaker) as JSON from the same `StorySentences`/segmenter code the app uses; the Python generator reads that,
  so clip N is always highlight N.
- **Limits and budget:** reads the requests-per-minute and per-day limits from flags (defaults: the paid Tier 1 values
  read off the Rate Limit page once billing is on; the free 3/min, 10/day with `--free`), paces itself, saves state after
  every clip and resumes. A `--max-requests` budget per run. 402 (no balance) or 403 ends the run cleanly; 429 waits for
  the retry-after once, then ends.
- **First run: Three Little Pigs in Farsi** (D1), narrator Hali; character voices from the defaults below, shown to the
  user before the rest of Farsi.

## Recommended defaults (not yet confirmed by the user; change any before part A starts)

- Narrator voice: the user's top pick from listening round 2 (Hali rated 5/5 in round 1).
- Characters: each book's cast gets Gemini voices matched to its existing cast (children young and bouncy, per the Dino
  voice rule), written into `content/dino/<book>/audio-cast.json` and shown to the user on a listening page per book
  before the full render (`voice-pick`).
- Emotion: one short direction per dialogue line (e.g. "the wolf, sly and sweet-talking"), written by the
  `dino-story-writer` agent into `audio-cast.json`; narration uses the one storyteller prompt.
- Format: AAC in `.m4a` (plays on Android, iOS and Windows without a codec pack), checked against each platform's player
  docs in C1 before coding.
- Licence: the Gemini API terms for shipping generated audio in a paid app are read and written here (A5) before any
  clip ships (part B), not before rendering.

## Decisions (the user, listening D1, Three Little Pigs fa, 2026-11-03)

Page: https://claude.ai/artifact/5oh43T7JWpijaprFi62wHD. Rating **5/5**; "Good enough for all 16 Farsi books?" **yes**;
every voice OK (Hali narrator; Cleo, Puck, Achird as Pip, Pop, Pim; Elio as the wolf). Note: "other than a couple of
small problems all good. It was wonderfull". Flagged lines:
- 16 (Pim, «خرخر!», voice): "if it is the sound a pig makes, it is wrong. Also, it should not be read by ai. for the
  animal noises, lets use the real pig oink oink and real cow saying mo" → Slice 255 (real recordings).
- 17 (narrator, word): "گنده‌ی is read gondeye not gandeye" → A6.
- 22 (narrator, «گرگ گفت.», voice): "It says Gorg goft.... not Gorge goft! which makes the audiance think the wolf just
  want to say something." → Slice 254 (the tag moves before the wolf's words in Farsi), re-listen after the re-render.
- 69 (narrator, «شالاپ! شالاپ! شالاپ!», voice): "shaalaap shaalaap shaalaap! it reads it as shalaap shalaap shalaap" → A6.

## Decisions (the user, spelling page and Pigs v2 re-listen, 2026-11-03 20:33-20:35 UTC)

Spelling page: https://claude.ai/artifact/Vj3oSgh8LGBTkwW8sNcnoc. Its first version asked line 17 the wrong way round (as if
the user wanted "gandeye") and offered no "gondeye" spelling; it was republished at about 20:25 with three گُنده spellings
before the user answered. Line numbers on the page are the old dump's (17 and 69); after Slice 254 they are 16 and 68.
- "One evening, a big grey Wolf came walking down the road." (old 17): option 1, `say` «یه روز غروب، یه گرگ گُنده‌ی خاکستری داشت از جاده میومد.»
- "Splash, splash, splash!" (old 69): option 3, `say` «شالاپ، شالاپ، شالاپ!» (the book text has «!» after each word; the
  picked spelling uses commas, which changes the rhythm, and is what the user chose by ear).
Both are in `content/dino/three-little-pigs/audio-cast.json` and rendered (2 requests).

The other two گنده lines (page https://claude.ai/artifact/MsDdy7erGGjAemhcRg4nLA, 2026-11-03 21:07 UTC; option 1 was the
clip then in the book): Pim's "I've put a big tub of cold, bubbly bath water right under the chimney." option 2, `say`
«من یه وان گُنده پر از آب سرد و کف‌کفی گذاشتم زیر دودکش.»; the narrator's "said Pim with a big smile." option 2, `say`
«پیم با یه لبخند گُنده گفت:». Both in the cast and rendered (2 requests), so all three گنده lines now carry the ُ mark.

Pigs Farsi v2 page (https://claude.ai/artifact/GKVR5pS3QgdKXPi8ZjsNHa): «X گفت:» before the line sounds right: **yes**;
overall OK: **yes**. No lines flagged.

## Work (re-planned 2026-11-03)

**Part A — generator (`tools/premade-audio`, runs on the user's PC)**
- [ ] A0. *(main thread, needs Chrome)* Create a Gemini API key in an AI Studio project **not linked to billing**, save it
  as `GEMINI_API_KEY` with `manage-secrets`, read the free-tier limits for `gemini-3.8-flash-tts` off the Rate Limit
  page and write them here.
  *Done 2026-11-03:* key "Default Gemini API Key" in project "Default Gemini Project" (Free tier), saved as
  `GEMINI_API_KEY`; one live sentence OK through `adapter_gemini.py` (Interactions API, voice Hali). Free limits for every
  TTS model: **3 RPM, 10K TPM, 10 requests per day** (2.5-pro-tts: none). At one sentence per request that is months for
  Farsi, so the route needs the user's choice: a $5 prepay (the minimum; then the £15.04 GCP credit is used first,
  auto-reload off, spend cap set) or page-sized free requests cut at pauses. Billing account unchanged (0 projects linked).
- [x] A1. Remove the ElevenLabs, Azure, OpenAI, Narakeet and CAMB adapters and their tests; keep the Gemini adapter and
  prove its request shape with one keyed sentence (the first live call; fix what it shows). *Done 2026-11-03: adapters, `render_all.py`, `book_text.py`
  (the BookBuilder dump replaced it) and the old tests removed; the live shape was already proven by A0, so no further call was made.*
- [x] A2. Per-book run: every sentence of one book in one language, plain text, narrator vs character from the app's own
  segmenter output (same sentence split as `StorySentences`, so clip N is highlight N), the voice and direction from
  `audio-cast.json`; manifest v3 (book, language, sentence index, text hash, voice, direction, clip). Resumable and
  cached by text hash.
- [x] A3. Rate-limit aware: honours the free tier's per-minute and per-day limits, stops cleanly at the day's limit and
  resumes the next day where it left off; a 402/403 never retries; a daily budget flag for the paid route.
- [x] A4. Encode WAV to AAC `.m4a`; drop leading and trailing silence so sentence clips join without gaps.
- [x] A5. Write the Gemini API output licence terms (commercial use, attribution, AI-voice disclosure) into this file.
  *Read 2026-11-03* at https://aistudio.google.com/docs/terms (ai.google.dev/gemini-api/terms redirects there; effective date March 23, 2026, no version number).
  - Ownership: "Google won't claim ownership over that content." Google "may generate the same or similar content for others"; "You're responsible for your use of generated content, and for the use of that content by anyone you share it with."
  - Commercial use: the terms are for "professional or business purposes, not for consumer use" (the API client is ours, the audio ships inside our app); no ban on shipping generated output in a paid product was found.
  - Attribution: the terms say compliance with applicable law "may require the provision of" attribution "to your users when returned as part of an API call" (no fixed wording given; our clips are not returned live to users, they are pre-made).
  - AI disclosure / watermark: the page does not mention SynthID, watermarking or an AI-voice disclosure rule. (Not covered by the terms: any store or law requirement to say the voices are AI; decide that in part B.)
  - Restrictions: no models that compete with the Services, no reverse engineering/replicating the Services, no clinical or medical-advice use (none apply to storybook audio).
  - Data: on the unpaid tier Google uses submitted content and responses to improve products and "human reviewers may read" them ("Do not submit sensitive, confidential, or personal information to the Unpaid Services"); the paid tier does not. Book text is public story text, so the free key is fine. Paid Services are required for API clients offered to users in the EEA; we offer no API client, only pre-made audio.
  - Summary line: output is ours to ship commercially; Google claims no ownership; no attribution or AI-voice label is required by the terms for pre-made clips, but nothing here settles any store-policy or EU-law AI-label question (check before part B ships). Primary source read through a page summariser, not the raw page: re-read the quoted clauses before relying on them.
- [x] A6. (done 2026-11-03: the user's spelling picks are the Pigs `say` entries, see the decisions above) **Gemini-only pronunciation fix per line** (from the D1 answers): `audio-cast.json` takes a per-line `say` (the text
  Gemini reads, e.g. one word with a vowel mark), guarded by speaker + English source like the directions; the clip is
  keyed by the `say` text; the book text and the on-device voice are untouched. Try 2-3 spellings for each flagged word
  (a few pence) on a listening page, keep the user's pick.
- [x] A7. Per-line `direction`/`say` entries in `audio-cast.json` (`lines`) are keyed by the English `source` + `speaker` (+ 1-based `occurrence` for repeats), never by dump index, so one cast serves every language; a `say` carries its `lang`; every entry must match a dump piece or the run fails before any request (the stopgap `audio-cast.fa.json` is gone).

**Part B — package and store**
- [ ] B1. Decide inside `.dino` vs a separate download by measured size (one Farsi book's clips), write the decision here.
- [ ] B2. BookBuilder / `upload-store-story.ps1` carry the clips and manifest; the worker serves them; a book without
  clips for a language works as today.

**Part C — app playback (Android, iOS, Windows head alike)**
- [ ] C1. `PremadeAudio` in `DinoReader.Core`: parse the manifest, match a sentence by text hash, else fall back.
- [ ] C2. Reading plays the clip when the hash matches, else the on-device voice as today; speed, pause, Back and the
  highlight behave the same on both paths. Android and iOS tasks both, plus the Windows head.

**Part D — generation runs**
- [x] D1. Farsi, one book (Three Little Pigs): render, listening page for the user, fix what they hear. *Done 2026-11-03 (PRs #763, #766).*
- [ ] D2. Farsi, the other 15 books on the free key (as many days as the limit needs). *14 rendered 2026-11-03 in batch mode (all 0 left); Good Enough blocked: `dump-sentences good-enough` fails ("1 pages but 10 pictures"). Clips outside git in `D:/ReadTheStupidText-worktrees/s246-audio/books-<book>/` (Town Mouse in `books/`). Voices listening page: https://claude.ai/artifact/9AZaLgD9Vvt1LjfpAaEwnG, waiting on the user. Casts merged in #767.*

### Decisions and findings, 2026-11-03 evening (Gemini limits and batch mode)

- **Paid Tier 1 limits** (the user's screenshot of the AI Studio rate-limit page, 2026-11-03): every TTS model has 10 requests per minute and 100 per day (2.5 Pro TTS: 50 per day); gemini-3.8-flash-tts reached 104/100 that day. The plan's assumption that the paid tier lifts the per-sentence limit was never checked before the run (A0 read only the free limits); it was wrong.
- **Other models** (one Farsi sentence each, 2026-11-03): only gemini-3.8-flash-tts and gemini-3.8-flash-lite-tts accept the extended voices (Hali); gemini-3.1-flash-tts-preview and the 2.5 models offer only the 30 standard voices, and 3.1 refuses `speech_metadata` ("Speech metadata is not supported for this model").
- **The user: "dont want light. I checked 2 phrases, the normal one is better."** Lite is out; gemini-3.8-flash-tts stays.
- **Batch API works and is not counted against the 100 a day:** a 2-request batch on gemini-3.8-flash-tts succeeded after that day's 100 were used; a 20-request Lite batch came back in 2 min 9 s; 14 books (1,138 requests in 14 batches, 13 at once) all succeeded within minutes. Batch is half the normal price (docs). Batch limits for the 3.8 TTS models are not listed in the docs (enqueued tokens are per model); not hit so far.
- **Batch mode** (`generate.py --batch`, `batch.py`): one batch per book-language, resumable state file, fail-closed lookup of a lost submit by display name (live listing shape `operations[].metadata.displayName`), a 2-attempt cap per failing key.
- **Found while casting** (not fixed here): in Lion and the Mouse the Mouse's line "I didn't mean to wake you up. If you're kind to me today, I'll be kind to you one day, too!" is attributed to the Lion by the app's own segmenter (en index 14, fa 13), so both the device voice and the Gemini clip use the Lion's voice; the frog's "Ribbit!" lines in Tortoise and the Hare are spoken lines, not sound pieces.
- [ ] D3. The other 14 languages, in the order the user picks; switch to the capped credit only with the user's yes.
- [ ] D4. Upload each finished book-language (store and app must match: the store-upload rule).

## Verification (re-planned)

- A: offline unit tests (`python -I -W error -m unittest discover -s tools/premade-audio`) for sentence alignment with
  the app's split, manifest v3, cache hits, the daily-limit stop and resume; one live keyed sentence (A1).
- C: `tests/DinoReader.Tests` for hash match and fallback; Android emulator and iOS: a store book plays clips with the
  highlight in step, an edited sentence falls back to the device voice.
- D: the user's listening answers for D1 written here.

## Work (old list, kept for the record; replaced by the re-planned list above)

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

- ~~The vendor keys~~ (superseded 2026-11-03: Gemini only, key created in A0 from the user's signed-in AI Studio).
- Listening round 2 answers (more Gemini voices, plain text, emotions) pick the narrator voice for A2.
- Confirm or change the *Recommended defaults* above.

#### For CLAUDE.md

(none from tonight's part)

#### Not yet verified

- Every vendor request shape is unproven until the first keyed run (Gemini model id, ElevenLabs voice id placeholder, Narakeet voice-name casing, CAMB `fa-ir`).
- The piper-fa baseline render, on a PC that has the `piper-fa` pack and `sherpa_onnx` installed.
- Pigs Farsi: the two `say` lines (گُنده‌ی, «شالاپ، شالاپ، شالاپ!») were re-rendered in the full book on 2026-11-03 but nobody has listened to them in the book; the user heard only the spelling-page variants.
