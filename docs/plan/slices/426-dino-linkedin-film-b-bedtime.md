# Slice 426 — Dino: LinkedIn film B, "One bedtime, three languages" (what Dino is, why, what it does)
**Product:** Dino

*Built 2026-11-20, merged as PR #1326 (24d5247). Planned 2026-11-20. First of three LinkedIn films (426 B, 427 C, 428 A), built and shipped in that order; each ships before the next is built.*

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

## Decisions (user, 2026-11-20, after watching the 49 s preview)

- The 3 s app recording is dropped from film B, with its scene and its sound: "the part from 0-18s is already showing that it is saying it with different voices, you just dont point it out." The film is now 44 s. `raw/s21-page13-en.mp4` stays in `raw/` for films C and A.
- The line "A voice for every character" / "made with Google's Gemini voices" goes over the opening page scene from 0 s to about 5 s (built: 0–5.1 s, a paper card below the language chips and above the pigs' heads, clear of the text card). The music stays as it was ("music is ok"), re-timed: quiet under the voices, rising where nobody speaks (from 16.8 s), faded on the end card.
- Music replaced later the same day: "the music is not good". From a listening page of 11 free HeyGen catalog tracks the user picked **11** ("Astral Generated Music: 3602ebd4", cheerful upbeat indie kids, 43 s). It keeps the same levels and fades and ends at 43 s, inside the end card's fade.
- The "Your voice" screen is centred, not zoomed: "The text is already taking the whole width of the page. you can only bring it to the center of the page to take people's attention." Built: the screen slides down until the privacy line sits on the frame's vertical centre; its caption moves to the top of title-safe.

## Facts the film and post may state (checked on origin/main 2026-11-20)

- Live on Google Play (`uk.sirous.dinoreader`), the App Store (`id6825053435`) and the Microsoft Store (`9N9VHRLCZXN5`); store name "Dino: Kids Books & Stories" (`STORE.md:830`, `site/dino/index.html:123-125`).
- 15 languages for the app, books and audio: en, zh, es, ar, fr, pt, ru, de, ja, fa, nl, sv, da, fi, nb (`site/dino/books.html:209`).
- Voices: a distinct pre-made voice per character, made with Google's Gemini voices (`gemini-3.8-flash-tts`, `tools/dino/premade-audio`). Say "Google's Gemini voices", not "Google TTS". "With feelings" is not claimed (not found in the code or docs).
- Classic tales retold for ages 3–9 so nobody is hurt (the 1900s versions were frightening).
- "Record your voice" (`Record_Title`): a parent or child picks a character, speaks its lines, and the recording stays on the phone.
- First Dino commit 2026-10-20: "5 weeks" holds.

## Storyline (to be written out as SPEC.md)

One real page of The Three Little Pigs (the onboarding page). The same line plays in English, then Farsi, then German, with the text on the page changing language with the voice. A short "why" beat follows: children growing up between two or three languages, for example second-generation migrant families. Then a parent taps "Record your voice" and reads a character's line. It ends on "Built in 5 weeks with Claude" and the three store badges.

As built and accepted (2026-11-20): the record beat shows the "Your voice" setup screen (pick a character, the recording stays on this phone), centred, not a parent speaking a line; the 3 s app recording was dropped (see Decisions).

## Work

- [x] HyperFrames project in `tools/dino/promo/linkedin/b-bedtime/`, made with `npx hyperframes init`. `STORYLINES.md` keeps all three storylines, so 427 and 428 start from it. `SPEC.md` is written from `references/story.md` and `spec.md` of the hyperframes skill.
- [x] Assets: the page picture(s) from `content/dino/three-little-pigs/`, the en/fa/de clips for that page, real app screenshots (emulator, scripted), and one recording of at most 3 s of the page being read (emulator `screenrecord`, scripted).
  - Built 2026-11-20: pages 13 and 15 from `high-res/`; clips en/de 56 + 58 and fa 57 + 59 (Puck = Pop, Achird = Pim); the "Your voice" screen from the scripted store screenshots of 2026-11-19 (`store-screenshots.ps1`); a 3.1 s cut of the store app (1.133.1) reading page 13 in English on the S21 (`screenrecord`), with the app's clips en 57 and 58 under it at the highlight's measured frames (the release APK, arm64 only, crashed on the x86_64 emulator, and the emulator's Debug take had no audio pack); that cut was dropped from the film on 2026-11-20 (see Decisions) and the take is kept in `raw/` for films C and A. Music: HeyGen catalog "Astral Generated Music: e1e41141", ducked to 0.14 under the voices (`b-bedtime/REVIEW.md`); replaced the same day by the user's pick, "Astral Generated Music: 3602ebd4" (see Decisions).
- [x] Scenes built. `npx hyperframes check --json` is clean, `snapshot --review` has been run and `REVIEW.md` written. The Studio preview is opened for the user.
- [x] After the user's OK on the preview: render the final MP4 at 1080×1350 and commit it under the film folder if it is under 15 MB (else attach it to a GitHub Release named `promo-dino-linkedin-b`; decided by this session). Built 2026-11-20: 19.3 MB, so it stays in `renders/` and is attached to the Release.
- [x] LinkedIn post text in `b-bedtime/POST.md`, in the user's voice, covering:
  - what Dino is and why the user built it;
  - the four features in plain words;
  - "Google's Gemini voices";
  - the three store links.
  The user posts it themselves from their own LinkedIn account. Nothing is posted by Claude.

## Verification

- `npx hyperframes check --json` shows no errors. Every frame reviewed with `snapshot --review` shows no text outside the title-safe area.
- The rendered file plays at 1080×1350 with sound. Each voice is the app's own clip for that page and language.
- Every claim in the film and in POST.md traces to the facts list above.

## For films C and A (from review, 2026-11-20)

- The language chips' CJK text uses "Noto Sans JP", which is not bundled in `assets/fonts/`: film B rendered correctly on this PC (checked frame at 21 s), but a render on a PC without that font would show empty boxes. Films C and A bundle a CJK font (with its OFL text) if they show Chinese or Japanese.
- Review findings left as they are: the page-13 text card shows only the lines spoken in the film, and the Farsi card follows the film's layout, not `fa.json`'s quotes (accepted by the user in the preview); `record.html` keeps Studio attributes and fixed offsets (no effect on the render).

## Where it is

The film is on Release https://github.com/ashkansirous/ReadTheStupidText/releases/tag/promo-dino-linkedin-b; the post text is in `tools/dino/promo/linkedin/b-bedtime/POST.md`.
