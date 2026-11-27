# LinkedIn post for film A

Written for the user to post from their own LinkedIn account with the rendered film attached. The user posts it
themselves; nothing here is posted by Claude. Every claim traces to the Findings and Wording of Slice 428
(`docs/plan/slices/428-dino-linkedin-film-a-deleted-feature.md`), the Findings of Slice 427, the facts list of Slice 426
or the user's own words. The film is `dino-film-a-deleted-feature.mp4` (1080x1350, 50 s), attached to GitHub Release
`promo-dino-linkedin-a`, not committed.

---

The feature I deleted.

My first idea for Dino was simple: scan your own paper book and let the phone read it aloud. I built it in the first
week: scan the pages, pick photos, add a PDF or type the text, then the app's built-in voice reads it.

Almost nobody used it. So on 6 November I deleted it, together with the voice engine behind it.

Dino became something else: illustrated picture books for children,
retold so nobody gets hurt, with a different voice for every character, in 15 languages.

That's the beauty of user testing and coding with AI: a complete new round with users almost every week. My first idea
lived for 16 days.

Dino: Kids Books & Stories is live now:
- Android, Google Play: https://play.google.com/store/apps/details?id=uk.sirous.dinoreader
- iPhone and iPad, App Store: https://apps.apple.com/app/id6825053435
- Windows, Microsoft Store: https://apps.microsoft.com/detail/9N9VHRLCZXN5

---

## Where each claim comes from

- First idea, the four ways to add pages, built in the first week: the first Dino build `v1.53.0` (2026-10-26) has
  "Scan pages", "Photos", "PDF or file" and "Type or paste" (`a-deleted-feature/raw/captures/captures.md`); Dino used the
  scanner from Slice 64 (2026-10-21) and Slice 78 (2026-10-22). The user's words: "scanning your own paper book and using
  the internal voice engine to read the books".
- "The app's built-in voice reads it": the user's words ("using the internal voice engine to read the books"); Slice 278
  part B (PR #863) removed that voice engine.
- "Almost nobody used it": Slice 278 records live usage: own books were added in one app session (about 9 books, all
  typed; no scans, no file imports).
- Deleted on 6 November with the voice engine: Slice 278, PRs #859 (scanning, own books) and #863 (voice engine).
- What Dino became: picture books, nobody hurt, a voice per character, 15 languages: Slice 426 facts list.
- "The beauty of user testing and coding with AI", "a complete new round with users almost every week": the user's own
  words (2026-11-20, "thats the beauty of user testing and coding with AI making it possible to have a complete new
  round with users almost every week"). "16 days": scanning reached Dino on 21 October (Slice 64) and left on
  6 November (Slice 278).
- "So I deleted it": the user's words (2026-11-20): "I ended up removing the first feature/idea ... given the user
  feedback"; the user's reason in Slice 278: "that's not the main product anymore".
- Store name and the three store links: `STORE.md:830`, `site/dino/index.html:123-125` (film B's facts list).
