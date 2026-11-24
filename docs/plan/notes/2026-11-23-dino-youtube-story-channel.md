> Copied verbatim from the Scope Inbox phone doc (Google Drive, https://drive.google.com/file/d/1AqcV6K78MUvYL_XJ7JdwhunL6mezSM7i/view) on 2026-11-24, when it was planned. The slice files that build it win where they differ.

# Dino: a YouTube channel with the four free stories as read-aloud videos in 15 languages

- project: dino-reader
- status: ready
- goal: families find Dino through free story videos on YouTube (and later other platforms), and come to the app from there
- context: read _context/CLAUDE.md (mirrored 2026-11-20), _context/_STATUS.md (2026-11-22), slices 404 and 417; plan.main.md, README.md and rules/ not re-read this time
- type: new-project feature (marketing channel, no app change expected)
- created: 2026-11-23
- updated: 2026-11-23

## Idea
Ashkan (2026-11-23): "I am showing 4 books for free. Why not make the best out of it? Let's create a YouTube channel for kids. Record each of those 4 stories in all 15 languages on Windows, in a size that suits TV with high res, even 4K? At the beginning, show the name of the book and maybe say its name, with a QR code to my page. At the end, use the back cover and show credit with a QR code to my page. Only thing to care: on my screen the text would become small, so pick the right size or increase the text size on Windows." Questions he asked: how to tell languages and countries apart on YouTube Kids, whether to also use normal YouTube, and which other platforms.

## Why
The stores alone bring very little traffic in the first weeks. Read-aloud story videos are a known format parents search for on YouTube, and each video can point families to Dino (QR code and the channel's links). Success: the videos get watched on TVs and tablets, and visits to dinoreader.sirous.uk and installs go up.

## Constraints
- Only the four books that are free in the app.
- Made for kids (COPPA): on YouTube, videos marked made for kids lose cards, end screens, comments, notifications and personalised ads, among other features (YouTube Help 9632097). So the QR code inside the video is the main way to point to the page.
- YouTube's quality principles for kids content treat "heavily promotional" content as low quality, which affects recommendations, YouTube Kids inclusion and monetisation (YouTube Help 10774223). The QR codes stay small and only at the start and end; the story itself carries no promotion.
- The video shows the book's text, highlighted as it's read, exactly like the app (Ashkan, 2026-11-23). So every language needs its own video: the words on screen must match the voice.
- YouTube's monetisation rules ("generic or repetitive content", clarified 2025–2026) name channels that upload narrated stories or slideshows with only superficial differences between them. Sixty uploads (4 books × 15 languages) may not be monetised. The goal here is reach, not ad income, so this is accepted; each language's video gets its own title, description and thumbnail text in that language.
- Never Farsi in any visual meant for everyone (Ashkan, 2026-11-23); a Farsi audio track is fine because each viewer picks their own language.
- Picture quality: every book has high-resolution pictures (Slice 350 Part D done for all 15 books on 2026-11-19, plan mirror 2026-11-20), so all four videos can be made in 4K (3840 × 2160).
- On Windows, text and pictures must fill the frame at a readable size; never a full-screen grab of the high-resolution monitor (same rule as the store-pictures scope, 2026-11-23-store-screenshots-and-video).

- **Token budget (Ashkan, 2026-11-23: "very concerned about my token count").** Claude Code's work is one script built once, then batch runs with no agent watching. Rules: build and approve one book in one language first; the other 59 videos are made by running the script (locally or in CI), not by an agent per video; checks are automatic (each line's on-screen text and narration come from the same book data and timings; a run prints a one-line pass/fail per video and one contact sheet per book); the agent reads that summary, never the videos or long logs; no review rounds beyond the first book; stop and report instead of retrying a failing step more than once.

## Milestones
1. **One book, one language, end to end** — The Lion and the Mouse in English, at the chosen resolution, with the opening and closing cards; uploaded unlisted for Ashkan to watch on a TV; acceptance: he approves the look.
2. **All 15 languages for that book** — one video per language, with that language's text on screen and its own title, description and thumbnail; one playlist per language; acceptance: each video's on-screen text matches its narration.
3. **The other three free books** the same way.
4. **Channel set-up** — name, icon, banner, links to dinoreader.sirous.uk and the store pages in the channel's About section; made-for-kids set at channel level.
5. **Later:** other platforms (see Research).

## The hard part
Producing 60 videos (4 books × 15 languages) that each show the right text, highlighted in time with the narration, at a size that reads well on a TV, without any of them going out with a mismatch between words and voice.

## Risks / kill criteria
- If YouTube Kids doesn't pick the videos up after a few weeks, the channel still works on normal YouTube.
- Stop if the QR code or channel links turn out to break YouTube's kids policies (check before upload).

## First step
Make the English The Lion and the Mouse video (opening card, story, closing card) at 3840 × 2160, and upload it unlisted for Ashkan to watch on a TV.

## Spin-offs
- An audio-only "Dino Bedtime Stories" podcast from the same narration (Spotify, Apple Podcasts).
- Short clips aimed at parents on Instagram, TikTok or YouTube Shorts (those apps are for 13+, so the clips speak to parents, not children).
- The same videos embedded on dinoreader.sirous.uk.

## Open questions
- [ ] Code/repo: which four books are free right now (the store's free list).
- [ ] Code/repo: whether a direct render from the book files (pictures, text, narration clips and their timings) or a recording of the Windows reading screen gives the cleanest video.
- [ ] Check: YouTube's rules for disclosing AI-made content (the pictures and narration are AI-made) — read YouTube's own help page before the first upload; never guess.
- [ ] Check: whether QR codes or links pointing to a page that sells a subscription are allowed in made-for-kids videos (YouTube's ads and kids policies).
- [ ] Book covers: whether the front and back covers carry English text (they would then show English to every language).
- [ ] Kidoodle.TV (a curated kids' streaming service that takes creator submissions) — Wikipedia mentions a liquidation linked to its sister platform; check its status before spending time on it.

---

## Decisions (approved)
- **Videos per language** → one video per book per language (60 in all), the book's text on screen highlighted as it's read and matching the voice; each with its own title, description and thumbnail in that language; one playlist per language on one channel; ad income is not a goal. (Ashkan, 2026-11-23)
- **How the videos are made** → rendered straight from the book files (pictures, text with the highlighted line, the pre-made narration clips and their timings), styled like the app's reading screen; not a screen recording. Under the token budget rules in Constraints. (Ashkan, 2026-11-23)
- **Resolution** → 4K (3840 × 2160) for all four books; every book has high-resolution pictures. (Ashkan, 2026-11-23)
- **Where first** → YouTube, marked made for kids (which is where YouTube Kids picks from); the podcast and parent-facing clips come later. (Ashkan, 2026-11-23)
- **Opening and closing cards** → front cover with the title said aloud and a small QR code to dinoreader.sirous.uk; back cover with credits, the QR code and the grown-ups line about more stories and recording your own voice; no buy or download prompts. (Ashkan, 2026-11-23)

## Research and options

### YouTube vs YouTube Kids
You upload once, to YouTube, and mark the video (or the whole channel) as made for kids. YouTube Kids picks from that content, guided by YouTube's quality principles for kids and family content. So "normal YouTube or YouTube Kids" is one upload, not two.

### Languages and countries
- **One video per language**, because the on-screen text is in that language. Each video's title, description and thumbnail are written in its own language, so YouTube's search matches it for speakers of that language. One playlist per language on one channel.
- **Countries** need no separate handling: language is what changes. Mainland China can't reach YouTube anyway.
- **Multi-language audio** (several audio tracks on one video) doesn't fit, because the text on screen would only match one track. Not used.

### Options for making the video
1. **Render straight from the book files** (pictures, the book's text with the line highlighted as it's read, the pre-made narration clips and their timings, gentle page turns), styled like the app's reading screen but not a screen capture. Exact 1080p or 4K, no UI chrome, text size chosen for TV. Recommended.
2. **Record the Windows app** reading screen with text, window sized so the capture is exactly 1920 × 1080 (or 3840 × 2160) with comfortable text. Closer to "the real app", but tied to the app's layout and the monitor's scaling.
3. **Record the phone app** — portrait, wrong for TVs. Not recommended.

### Opening and closing cards
- Opening (about 3–4 s): the front cover, the narrator says the book's name in the track's language, a small QR code and the address dinoreader.sirous.uk.
- Closing (about 5–8 s): the back cover, credits (story, pictures, voices, made with AI if YouTube requires it), the QR code and address.
- No subscribe or download prompts inside the story.
- **Closing line (Ashkan, 2026-11-23: "say get more books and speak on behalf of the characters in our app"):** one short spoken line, plus the same words on screen, addressed to grown-ups, in each track's language. Proposed: "Grown-ups: more stories wait in the Dino app, where you can record your own voice for any character." No "buy", "download now" or prices, so it stays clear of YouTube's "heavily promotional" and "encourages excessive buying" low-quality signals. Claude Code checks YouTube's kids policies before the first upload (see Open questions).

### Other platforms (not verified in depth)
- Audio podcast (Spotify, Apple Podcasts): the narration as episodes, linking to the app.
- Short parent-facing clips: Instagram Reels, TikTok, YouTube Shorts (13+ apps).
- Kidoodle.TV: curated, takes submissions; status uncertain (see Open questions).

Sources: https://support.google.com/youtube/answer/9632097 , https://support.google.com/youtube/answer/10774223 , https://support.google.com/youtube/answer/13338784 , https://support.google.com/youtubekids/answer/15934993 , https://blog.youtube/news-and-events/multi-language-audio/ , https://www.tubefilter.com/2026/07/13/youtube-inauthentic-content-monetization-policy-update/ , https://en.wikipedia.org/wiki/Kidoodle.TV

## Ashkan's notes
- 2026-11-23: The idea as quoted above; worries the text gets small on his high-res screen.
- 2026-11-23: Maybe also say "get more books" and that you can speak on behalf of the characters in the app.
- 2026-11-23: The app shows the text too, so the videos must show it: no language-neutral single video.
- 2026-11-23: Very concerned about token count for option 2.
- 2026-11-23: All books have high-resolution pictures (correcting the draft).
- 2026-11-23: "yes" — approved the remaining points.

## Change log
- 2026-11-23: created (draft), before the RefineScope questions.
- 2026-11-23: closing line about more books and recording character voices added.
- 2026-11-23: corrected: the videos show the text, so one video per language (multi-language audio dropped).
- 2026-11-23: token budget rules added.
- 2026-11-23: corrected: high-res pictures exist for all books, so 4K for all four.
- 2026-11-23: all decisions approved; status ready.
