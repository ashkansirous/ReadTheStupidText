# Slice 246 — Dino: store books read with pre-made audio from the best voice

*Planned 2026-11-01.* From the user, about Farsi on the iPhone ("it even pronounces the sukun ... isn't there a tehrani
accent?") and the vendors Google lists: "emotions and dedicated Tehrani Standard neural voice option are crucial for this
app which is for young kids!", then: "if generating audio on the servers makes it better, lets do it. a story is 3-5 min,
so it should not take that much space. we can generate it on my machine. create a test page and generate 1 paragraph of
1 of the stories for me via each of these services."

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

- [ ] 1. **Listening test.** One Farsi paragraph of the Three Little Pigs (its spoken text with the book's `speak:fa`
  table applied), rendered by every service we have an account for, plus our `piper-fa` mana as the baseline; a private
  listening page (`voice-pick` agent) asking for the pick and notes on accent, emotion and the sukun words. Accounts and
  keys are the user's (none are in `D:\secrets` on 2026-11-01); keys go into the secrets store with `manage-secrets`.
- [ ] 2. Write the user's pick and the licence terms of that vendor for shipping its audio in a paid app into this file.
- [ ] 3. **Generator** (`tools/premade-audio`, runs on the user's PC): per book and language, one clip per sentence (so
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
