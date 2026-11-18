### Slice 409 — Dino: the store upload stops spending 15 minutes installing ffmpeg
**Product:** Dino

*Planned.*

**Touches:** `.github/workflows/dino-store.yml`

Why: the user, 2026-11-18, about the Love from Seagull Island upload (`dino-store.yml` run 44293402445, 30 minutes in "Build the audio packs the live store
lacks"): "why would it need an hour to pack 15 different zip files?". The log shows where the time went:

- 11:19:57 to 11:35:17, about **15 minutes**, is `sudo apt-get update -qq && sudo apt-get install -y -qq ffmpeg` (silent, then the package unpack lines), run because
  the runner image has no `ffmpeg`/`ffprobe` for the listening tracks (Slice 281). The 09:24 run (44275630376) shows the same: 09:26:05 to about 09:41, 15 minutes.
- The packs themselves are fast: about 3 seconds a language zip and 3 seconds a listening track, so one book's 15 languages take about 1.5 minutes.
- The rest of this run was the Slice 281 listening-track backfill: 9 older books (billy-goats-gruff ... little-red-hen) rebuilt in the same step, about 1.5 minutes
  each; that run reported "Audio packs not reached in this run's time budget: 0", so the backfill looks finished. Check that the next run builds only changed books.

- [ ] Replace the apt install with a fast one: a pinned static ffmpeg build or a setup action (query context7 / the action's docs first), or `apt-get install` without a
      full `apt-get update`, whichever measures fastest; keep the "a failed install never blocks the zips" rule.
- [ ] Confirm from the next run's log that the backfill is done and only behind books are rebuilt; if old books still rebuild every run, find why in
      `publish-store-books.mjs --audio-behind` and fix it in this slice.
- [ ] Report before/after: the step's time on a one-book upload (today about 30 minutes, 15 of them apt).

**Verification:** a `dino-store.yml` run after a one-book change spends under 3 minutes installing ffmpeg and the audio-pack step under 5 minutes, with the listening
tracks built (no "Fail when a listening track could not be built").
