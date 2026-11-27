# LinkedIn post for film B

Written for the user to post from their own LinkedIn account with the rendered film attached. The user posts it
themselves; nothing here is posted by Claude. Every claim traces to the facts list of Slice 426
(`docs/plan/slices/426-dino-linkedin-film-b-bedtime.md`). The film is `dino-film-b-bedtime.mp4` (1080x1350, 44 s); it is
over 15 MB, so it is attached to GitHub Release `promo-dino-linkedin-b`, not committed.

---

One bedtime story, three languages.

Lots of children grow up between two or three languages. Second-generation kids in migrant families often hear one
language from their parents and another everywhere else. I wanted bedtime stories that work in all of them, so I built
Dino: Kids Books & Stories.

What it does:

- Picture books that read themselves aloud, with a different voice for every character. The voices are made with
  Google's Gemini voices.
- The app, the books and the audio come in 15 languages, so the same page can be read in English tonight and in Farsi
  or German tomorrow.
- Grown-ups can record their own voice for any character. The recording stays on the phone.
- Classic tales, retold for ages 3 to 9 so nobody gets hurt.

I built it in 5 weeks with Claude.

It's live now:
- Android, Google Play: https://play.google.com/store/apps/details?id=uk.sirous.dinoreader
- iPhone and iPad, App Store: https://apps.apple.com/app/id6825053435
- Windows, Microsoft Store: https://apps.microsoft.com/detail/9N9VHRLCZXN5

---

## Where each claim comes from

- "Dino: Kids Books & Stories", the three store links: `STORE.md:830`, `site/dino/index.html:123-125`.
- 15 languages for the app, books and audio: `site/dino/books.html:209`.
- A voice per character, Google's Gemini voices: `tools/dino/premade-audio` (`gemini-3.8-flash-tts`).
- Record your voice, stays on the phone: `Record_Title`; the app's "Your voice" screen says "Your voice stays on this
  phone. It is never uploaded."
- Retold for ages 3-9 so nobody is hurt: the slice's facts list.
- 5 weeks: first Dino commit 2026-10-20.
- Why (children between two or three languages, second-generation migrant families): the slice's Storyline, from the
  user.
