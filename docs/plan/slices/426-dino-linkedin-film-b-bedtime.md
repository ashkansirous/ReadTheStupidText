# Slice 426 — Dino: LinkedIn film B, "One bedtime, three languages" (what Dino is, why, what it does)
**Product:** Dino

*Planned 2026-11-20. First of three LinkedIn films (426 B, 427 C, 428 A), built and shipped in that order; each ships before the next is built.*

**Touches:** `tools/dino/promo/linkedin/README.md` (new), `tools/dino/promo/linkedin/b-bedtime/` (new), `.gitignore`, this slice file.

## Why

The user wants LinkedIn posts with video that promote Dino Reader and, most of all, themselves. An earlier app video made by Claude alone was poor ("the video of the app was crap"); the user asked for HeyGen's HyperFrames (HTML-to-video, plugin installed) instead. The user's order (2026-11-20): "First we have to tell what it is, why did I build it and what does it do. Then C explains how and last A, that explains that I deleted features."

## Decisions (user, 2026-11-20, RefineScope)

- Three films, in the order B, C, A. "Start from B, tell the user story, then we build and ship the next ones."
- Voices: "use app's voices. 3 little pigs page that I use for on boarding", meaning the pre-made Gemini clips in `content/dino/three-little-pigs/audio/<lang>/` for the onboarding demo page (`OnboardingDemoPage`, `src/Dino/DinoReader.App/OnboardingListenPage.cs`). No HeyGen voice and no avatar.
- Footage: "lets try what hyperframe can do. record video only for 3 seconds that the story is being read." Real app screenshots, the book's own pictures and kinetic type are built in HyperFrames. One real screen recording of at most about 3 s shows a story being read in the app.
- Defaults the user accepted:
  - 1080×1350 (LinkedIn 4:5 feed), 40–55 s, text on screen so it works muted.
  - Free music.
  - The post text is drafted by Claude in the user's voice.
  - Preview first, then render the final file.

## Facts the film and post may state (checked on origin/main 2026-11-20)

- Live on Google Play (`uk.sirous.dinoreader`), the App Store (`id6825053435`) and the Microsoft Store (`9N9VHRLCZXN5`); store name "Dino: Kids Books & Stories" (`STORE.md:830`, `site/dino/index.html:123-125`).
- 15 languages for the app, books and audio: en, zh, es, ar, fr, pt, ru, de, ja, fa, nl, sv, da, fi, nb (`site/dino/books.html:209`).
- Voices: a distinct pre-made voice per character, made with Google's Gemini voices (`gemini-3.8-flash-tts`, `tools/dino/premade-audio`). Say "Google's Gemini voices", not "Google TTS". "With feelings" is not claimed (not found in the code or docs).
- Classic tales retold for ages 3–9 so nobody is hurt (the 1900s versions were frightening).
- "Record your voice" (`Record_Title`): a parent or child picks a character, speaks its lines, and the recording stays on the phone.
- First Dino commit 2026-10-20: "5 weeks" holds.

## Storyline (to be written out as SPEC.md)

One real page of The Three Little Pigs (the onboarding page). The same line plays in English, then Farsi, then German, with the text on the page changing language with the voice. A short "why" beat follows: children growing up between two or three languages, for example second-generation migrant families. Then a parent taps "Record your voice" and reads a character's line. It ends on "Built in 5 weeks with Claude" and the three store badges.

## Work

- [ ] HyperFrames project in `tools/dino/promo/linkedin/b-bedtime/`, made with `npx hyperframes init`. `STORYLINES.md` keeps all three storylines, so 427 and 428 start from it. `SPEC.md` is written from `references/story.md` and `spec.md` of the hyperframes skill.
- [ ] Assets: the page picture(s) from `content/dino/three-little-pigs/`, the en/fa/de clips for that page, real app screenshots (emulator, scripted), and one recording of at most 3 s of the page being read (emulator `screenrecord`, scripted).
- [ ] Scenes built. `npx hyperframes check --json` is clean, `snapshot --review` has been run and `REVIEW.md` written. The Studio preview is opened for the user.
- [ ] After the user's OK on the preview: render the final MP4 at 1080×1350 and commit it under the film folder if it is under 15 MB (else attach it to a GitHub Release named `promo-dino-linkedin-b`; decided by this session).
- [ ] LinkedIn post text in `b-bedtime/POST.md`, in the user's voice, covering:
  - what Dino is and why the user built it;
  - the four features in plain words;
  - "Google's Gemini voices";
  - the three store links.
  The user posts it themselves from their own LinkedIn account. Nothing is posted by Claude.

## Verification

- `npx hyperframes check --json` shows no errors. Every frame reviewed with `snapshot --review` shows no text outside the title-safe area.
- The rendered file plays at 1080×1350 with sound. Each voice is the app's own clip for that page and language.
- Every claim in the film and in POST.md traces to the facts list above.
