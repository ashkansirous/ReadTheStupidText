### Slice 409 — Dino: the store upload only uploads (listening tracks built on the PC, no backfill in unrelated runs)
**Product:** Dino

*Planned.*

**Touches:** `.github/workflows/dino-store.yml`, `.github/scripts/publish-store-books.mjs`, `.github/CLAUDE.md`, `tools/dino/DinoReader.BookBuilder/**`, `tools/dino/premade-audio/README.md`, `.claude/skills/create-dino-story/SKILL.md`, `content/dino/*/audio/*/listen-*.m4a` (new)

Why: the user, 2026-11-18, about the Love from Seagull Island upload (`dino-store.yml` run 44293402445, 30 minutes in "Build the audio packs the live store
lacks"): "why would it need an hour to pack 15 different zip files?", then: "why are we reinstalling ffmpeg? why dont we do it on my local?" and "why ... did we need
to rebuild other [books]?! if there was a task or slice changing the old ones, that one is responsible to ship them!"

What the log shows:

- 11:19:57 to 11:35:17, about **15 minutes**, is `sudo apt-get update -qq && sudo apt-get install -y -qq ffmpeg`. A GitHub runner is a fresh machine every run, so this
  happens on every run that builds a pack. The 09:24 run (44275630376) lost the same 15 minutes.
- About 13 minutes went on 9 older books (billy-goats-gruff ... little-red-hen; the 09:24 run did 6 others), rebuilt because their listening tracks were not live yet.
  That is Slice 281's design: it left its own backfill to later pushes and the daily cron, 900 s a run (`.github/CLAUDE.md`, *Listening tracks*), so other books'
  uploads carried its work 12 days after it merged. Why it was still not finished after 12 days is not yet known.
- The packs themselves take about 3 s a zip and 3 s a track: one book's 15 languages in about 1.5 minutes.

Decisions (user, 2026-11-18):

1. **The work that changes old books ships them.** A slice or task that makes existing books' store files stale (a new track, a pack format, a picture size)
   rebuilds and uploads every affected book in its own PR and release run. A later, unrelated upload never carries its backfill. This replaces the
   "backfills over several runs" rule in `.github/CLAUDE.md`.
2. **ffmpeg work runs on the user's PC, where premade-audio already uses ffmpeg.** The listening track is built locally next to the clips and committed (LFS) like them;
   CI only zips, checks and uploads, so it needs no ffmpeg.

Work:

- [ ] `build-listen` (or a premade-audio step) writes each language's track into `content/dino/<book>/audio/<lang>/` (LFS) on the PC, after the clips; `check-audio`
      fails a book whose track is missing or older than its clips.
- [ ] Build and commit the tracks for every live book now, in this slice (decision 1), and confirm in this slice's own release run that every book's `meta.json` lists them.
- [ ] `dino-store.yml`: drop the ffmpeg install and the `build-listen` call; upload the committed track with the pack. Remove `LISTEN_BUDGET_SECONDS` and the
      "not reached" backfill; skip the re-zip when only a track is behind (Slice 281 check-pass finding 10).
- [ ] Find why the Slice 281 backfill was still running on 2026-11-18 (the `--audio-behind` verdict for the 9 books in run 44293402445), and say it in this file.
- [ ] Rules: replace the *Listening tracks* backfill sentence in `.github/CLAUDE.md` with decision 1, add decision 1 to the create-dino-story skill, and the track step to
      the premade-audio README.
- [ ] Report before/after: the audio-pack step on a one-book upload (today about 30 minutes, 15 of them apt).

**Verification:** after a one-book change, `dino-store.yml` builds only that book, installs nothing, and the audio-pack step takes under 3 minutes; the book's
`meta.json` lists `listenSeconds` for all 15 languages.
