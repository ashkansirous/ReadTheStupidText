### Slice 435 — Dino: a YouTube channel with the four free books as read-aloud videos in 15 languages, rendered from the book files
**Product:** Dino

*Plan-cleanup check 2026-11-25 (origin/main 1a8bf725): verdict keep open, not started. Free list re-read live from dinoreader.sirous.uk/api/stories: the same four books. `tools/dino/promo/youtube/` does not exist yet (only `promo/linkedin/`); `ListenTrack.cs` and `StorySentences.cs` exist as cited. Source note present. No waits, no side items to move: all seven work items are own work. The user sign-in for the YouTube grant (item 4) and the "yes" on the first video are the owner steps the slice already names.*

*Planned 2026-11-24 from the phone doc [2026-11-23-dino-youtube-story-channel.md](../notes/2026-11-23-dino-youtube-story-channel.md) (Scope Inbox, status ready). Follow every line of that note's Constraints (including the token budget), Decisions (approved) and Opening and closing cards; this file wins where they differ.*

**Touches:** `tools/dino/promo/youtube/**` (new), `docs/plan/notes/2026-11-24-youtube-kids-policy.md` (new), `docs/plan/slices/435-dino-youtube-read-aloud-channel.md`. Reads `content/dino/<stem>/` only (book.json, story.md, translations, high-res pictures, covers, `audio/<lang>/manifest.json` and clips through Git LFS), never edits it.

## Why

Families should find Dino through YouTube (user, 2026-11-23). One made-for-kids channel, one video per free book per language (4 x 15 = 60), each showing the book's text highlighted as it's read, like the app.

## Decisions (settled, never re-ask)

- Everything in the note's *Decisions (approved)* and the token-budget rules (user, 2026-11-23): one video per book per language; rendered from the book files, not a screen recording; 4K 3840 x 2160; YouTube marked made for kids; opening card (front cover, title said aloud, small QR code to dinoreader.sirous.uk) and closing card (back cover, credits, QR code, the grown-ups line); no buy or download prompts; one script built once, one book approved first, then batch runs with a one-line pass/fail per video and one contact sheet per book; no agent per video; a failing step is retried once at most, then reported.
- **Highlight sentence by sentence**, the way the app does it (user, 2026-11-24): the narration manifests have one clip per sentence and no word timings (`src/Dino/DinoReader.Core/StorySentences.cs`, `PlaybackSteps.cs`).
- **Upload by script through the YouTube Data API** (user, 2026-11-24): the user signs in once to grant access; the token is saved in the secrets store (`manage-secrets`), so later uploads need nothing from them. The first video goes up unlisted; the other 59 follow after the user's yes on it.
- **The four free books, read from the live store on 2026-11-24** (`/api/stories`, `access: "free"`): The Lion and the Mouse, X Marks the Treasure!, The Gingerbread Man, The Three Little Pigs (the same four as Slice 346). The render script reads the free list from `/api/stories` at run time, never a fixed list.
- **The channel exists** (user, 2026-11-25): Google account `elinsoftuk@gmail.com`, channel "Dino: Kids Books & Stories" (name and an "E" icon set in YouTube Studio). The one-time grant (item 4) signs in with that account and picks this channel. API access uses the Desktop OAuth client already in the secrets store (`claude-skills/google-oauth-desktop-client.json`, Google Cloud project `lets-call-mom`), with the YouTube Data API v3 switched on in that project. Unverified API projects may upload public or unlisted videos within the default quota of 100 uploads a day (YouTube Data API `videos.insert` docs, read 2026-11-25); kids status is set with `status.selfDeclaredMadeForKids`.
- Kidoodle.TV is left out (the note marks its status uncertain; a later spin-off at most).

## Work

- [ ] 1. Read YouTube's own help pages (through `web-research`) for: AI-made content disclosure, made-for-kids rules, and whether a QR code or link to a site that sells a subscription is allowed in made-for-kids videos; write the answers with links to `docs/plan/notes/2026-11-24-youtube-kids-policy.md`. If the QR code or closing line breaks a rule, change the card to fit (site address only, no QR) and record it; never upload against a rule.
- [ ] 2. Check whether each free book's front and back covers carry English text; if they do, the cards for other languages use the cover picture without the text plus the title in that language, rendered by the script.
- [ ] 3. **The renderer** in `tools/dino/promo/youtube/` (HyperFrames, like `tools/dino/promo/linkedin/`, which also shows reading LFS clips with `git lfs smudge`): page picture, the page's text in a TV-readable size with the spoken sentence highlighted, the narration clips in read order with the gaps `BookBuilder build-listen` uses (`tools/dino/DinoReader.BookBuilder/ListenTrack.cs`), clip lengths from ffprobe, gentle page turns, the opening and closing cards with the QR code. An automatic check per video: every highlighted sentence's on-screen text equals its manifest entry's text and clip; a run prints one pass/fail line per video and one contact sheet per book.
- [ ] 4. **First step: The Lion and the Mouse in English at 3840 x 2160**, opening and closing cards included; the upload script (YouTube Data API, made for kids, unlisted) puts it up; the user watches it on a TV and says yes or what to change.
- [ ] 5. The closing grown-ups line in all 15 languages (the note's proposed English: "Grown-ups: more stories wait in the Dino app, where you can record your own voice for any character."), spoken in each track's voice and shown on screen; titles, descriptions and thumbnail text written in each language.
- [ ] 6. After the user's yes: all 15 languages of that book, then the other three books, by running the script (locally or in CI, no agent per video); one playlist per language; each video's title, description and thumbnail in its own language.
- [ ] 7. Channel set-up: name, icon, banner, links to dinoreader.sirous.uk and the store pages in About, made for kids at channel level; by API where the API allows, else one step for the user with its direct YouTube Studio link.

Rules on the way: never Farsi in any picture meant for everyone (Farsi text appears only in the Farsi video); no Actions artifacts for the videos (`.github/CLAUDE.md`); a marketing tool, so no app change and no Android/iOS work.

## Verification

- The per-video check passes for all 60 videos (one summary line each); one contact sheet per book in this file.
- The YouTube Data API reads back each video's title, language, made-for-kids flag and playlist; the user's yes on the first video recorded here with its date.
