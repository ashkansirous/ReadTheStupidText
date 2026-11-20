# LinkedIn post for film C

Written for the user to post from their own LinkedIn account with the rendered film attached. The user posts it
themselves; nothing here is posted by Claude. Every claim traces to the Findings of Slice 427
(`docs/plan/slices/427-dino-linkedin-film-c-five-weeks.md`), the facts list of Slice 426
(`docs/plan/slices/426-dino-linkedin-film-b-bedtime.md`) or the user's own words. The film is
`dino-film-c-five-weeks.mp4` (1080x1350, 44 s); it is over 15 MB (15.9 MB), so it is attached to GitHub Release
`promo-dino-linkedin-c`, not committed.

---

How I built Dino in 5 weeks, with Claude.

Dino: Kids Books & Stories is a picture-book app for children. The film is the same app, week by week, from 26 October
to 20 November. Every screen in it is a real capture of that week's release code.

I and Claude wrote it in 5 weeks. Major UI, onboarding and feature changes followed user feedback. AI coding makes a
new round with users almost every week possible. There were over 150 releases along the way.

What changed along the way:

- Week 1: the first build, a voice for every character.
- Week 2: fixes from the first user test.
- Week 3: a redesign, with a kids shelf and a grown-ups corner.
- Week 4: onboarding that shows what Dino can do.
- Week 5: one shelf with a lock, and a five-step onboarding.

It's live now:
- Android, Google Play: https://play.google.com/store/apps/details?id=uk.sirous.dinoreader
- iPhone and iPad, App Store: https://apps.apple.com/app/id6825053435
- Windows, Microsoft Store: https://apps.microsoft.com/detail/9N9VHRLCZXN5

---

## Where each claim comes from

- Built in 5 weeks: first Dino commit 2026-10-21 (95eaf534, Slice 58); the film's weeks run 26 Oct to 20 Nov.
- Over 150 releases: 155 `dino-v*` releases in all (the first on 2026-10-26), plus 65 earlier shared `v*` releases
  that also carried Dino (Slice 427 Findings).
- "Major UI, onboarding and feature changes followed user feedback" and "AI coding makes a new round with users almost
  every week possible": the user's own words (the user, 2026-11-20). No round count is claimed; git holds one dated
  round, the user test of 2026-11-01 (Slices 228-232).
- "I and Claude wrote it in 5 weeks": the user's words (the slice's Why).
- Real captures: weeks 1-3 are captures of the release builds themselves; weeks 4-5 are builds of the release's own code
  (Debug x86_64); week 2's shelf and reading are from its Windows version (`raw/captures/captures.md`).
- Week by week changes: Slice 427 Findings, "Changes per week, from slices" (58, 60; 155 and 228-232; 261, 263, 325; 362,
  399; 418). Weekly releases shown: v1.53.0, dino-v1.96.1, dino-v1.123.3, dino-v1.131.3, dino-v1.136.3.
- Store name and the three store links: `STORE.md:830`, `site/dino/index.html:123-125` (film B's facts list).
