### Slice 409 — Dino: the store upload only uploads (listening tracks built on the PC, no backfill in unrelated runs)
**Product:** Dino

*Built in the worktree 2026-11-25, not yet committed or reviewed.* Every Work item is done except the two that can only be measured on the merge's own release run (items 2's `meta.json` confirmation and 6's "after"), marked pending below. Design as built:
- `build-listen-all [<book>...]` (BookBuilder, new, `ListenTrackFiles.BuildAll`) runs `build-audio` then `build-listen` per language on the PC; `build-listen` now writes `content/dino/<book>/audio/<lang>/listen-<lang>-v<n>.m4a` beside the clips (LFS already covers `content/dino/*/audio/**`; `git check-attr filter` says `lfs`), refuses clips that no longer hash to the `audioPacks` record, deletes the language's older track, and records `listenSeconds` in `audio-cast.json`.
- `check-audio` (`ListenTrackFiles.Problems`, called from `AudioAlignment.RecordProblem`) fails a language whose record has no length, whose `listen-<lang>-v<record version>.m4a` is missing or empty (an LFS pointer counts by its recorded size, so CI, which pulls no clips, checks it too), or, where the clips are real files, whose clips no longer hash to the record (track older than its clips). Shown failing: a track renamed away gave `x-marks-the-treasure fa: the listening track audio/fa/listen-fa-v2.m4a is missing`, exit 1.
- `dino-store.yml`: no ffmpeg install, no `build-listen`, no `LISTEN_BUDGET_SECONDS`, no "not reached" count, no "Fail when a listening track could not be built" step. `--audio-behind` now prints `<book> <language>` (pack behind) or `<book> <language> track` (live pack, track not live); a `track` pair fetches no clips and re-zips nothing (Slice 281 check-pass finding 10); every pair's committed track is pulled (`git lfs pull --include`) and copied next to the zips by the new `stage-listen`. A guard fails a book whose CI-built pack record differs from the committed one (`git diff --quiet` on its `audio-cast.json`), because the track's file name carries the pack version.
- The job's `timeout-minutes` stays 60 (not asked to change it).

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

- [x] `build-listen` (or a premade-audio step) writes each language's track into `content/dino/<book>/audio/<lang>/` (LFS) on the PC, after the clips; `check-audio`
      fails a book whose track is missing or older than its clips. (`ListenTrack.cs`, `ListenTrackFiles.cs`, `AudioAlignment.cs`; tests `ListenTrackFilesTests`, 8 cases.)
- [ ] Build and commit the tracks for every live book now, in this slice (decision 1), and confirm in this slice's own release run that every book's `meta.json` lists them.
      Built 2026-11-25 with this PC's ffmpeg 9.0.2 (`build-listen-all`, exit 0, no error): **240 tracks (16 books x 15 languages), 551,314,728 bytes (about 526 MiB)**, then `check-audio` for all 16 books: "All 240 audio manifests line up". The
      commit is the caller's. **Pending (release run after merge):** that every book's `meta.json` in the bucket lists `listenSeconds` for all 15 languages.
      Finding while building: `good-enough`'s committed record was behind the live store (committed v2/v3, live v3/v4 in 15 languages: CI bumped the version in the runner's workspace without committing it); `build-listen-all` printed 15 "pack version moved" warnings and
      the record is now v3/v4, equal to what is live, so no pack is re-uploaded for it.
- [x] `dino-store.yml`: drop the ffmpeg install and the `build-listen` call; upload the committed track with the pack. Remove `LISTEN_BUDGET_SECONDS` and the
      "not reached" backfill; skip the re-zip when only a track is behind (Slice 281 check-pass finding 10). (`publish-store-books.mjs`: `audioBehindLines`, `listenLanguages` + 2 tests; actionlint exit 0.)
- [x] Find why the Slice 281 backfill was still running on 2026-11-18 (the `--audio-behind` verdict for the 9 books in run 44293402445), and say it in this file. See *Cause* below ("likely": run 44293402445 and every dino-store run before 2026-11-24 are gone from GitHub).
- [x] Rules: replace the *Listening tracks* backfill sentence in `.github/CLAUDE.md` with decision 1, add decision 1 to the create-dino-story skill, and the track step to
      the premade-audio README.
- [ ] Report before/after: the audio-pack step on a one-book upload (today about 30 minutes, 15 of them apt). Before: below. **After: pending (measured on the release run after merge).**

**Verification:** after a one-book change, `dino-store.yml` builds only that book, installs nothing, and the audio-pack step takes under 3 minutes; the book's
`meta.json` lists `listenSeconds` for all 15 languages.

#### Cause: why the Slice 281 backfill was still running on 2026-11-18 (item 4; "likely", not proved)

What could be read: run 44293402445 (and 44275630376) answer 404 from the GitHub API (`gh run view 44293402445`, `gh api .../actions/jobs/44293402445`, `gh api .../actions/runs?created=2026-11-18` returns 0), and `gh run list --workflow dino-store.yml` holds one run, 45410724759 (2026-11-24, `schedule`). So the `--audio-behind` verdict of 2026-11-18 cannot be read. The 2026-11-24 run's log (`gh run view 45410724759 --log`) shows what the same code did six days later:
- `--audio-behind` listed 8 books (good-enough, love-from-seagull-island, red-riding-hood, three-little-pigs, tortoise-and-hare, town-mouse-country-mouse, ugly-duckling, x-marks-the-treasure), 15 languages each, and "not reached: 0". Those 8 are exactly the books whose public list now shows `listenSeconds`; the other 8 (billy-goats-gruff, elves-and-shoemaker, freddy-and-the-slipper, gingerbread-man, goldilocks, jack-and-the-beanstalk, lion-and-mouse, little-red-hen) were not behind by the bucket's `meta.json` that day (the public list, and a paid book's own routes answering 402, cannot show their tracks).
- `good-enough` was behind again although the 2026-11-18 run (per the user's note) already rebuilt it among the "9 older books": its tracks were built and its packs were `v3`/`v4` on 2026-11-24, while its committed record said `v2`/`v3`. So the work of an earlier run had not stuck.

Likely cause, from the code (`.github/workflows/dino-store.yml` before this slice): the backfill's work was done in the "Build the audio packs" step but only **staged on the runner**; it went live only in the publish step after `build --live`, and a run that ended before it (a failed or cancelled job, the 60-minute timeout: 2026-11-18's step alone took 30 minutes, 15 of them apt; a newer push) published nothing, so the next run found the same books behind and built them again. On top of that the budget (900 s, about 93 s a book on 2026-11-24, 150 s on 2026-11-18) fits 6 to 10 of the 16 books a run, so the backfill needed at least two complete runs, and runs happen only on a push to a store path and the daily cron; and any pack whose clips changed in between (the Persian and Arabic re-joins of Slice 280, the premade-audio re-renders of 2026-11-17/18) became behind again and needed its track again. This slice removes the mechanism: no backfill, no budget, no install; the tracks are committed and `check-audio` gates them.

#### Before and after (item 6)

Before (read from logs): the step "Build the audio packs the live store lacks" took about **30 minutes** on 2026-11-18 (run 44293402445, per the user: 15 minutes `apt-get update && install ffmpeg`, about 13 minutes for 9 older books; the 09:24 run lost the same 15 minutes). On 2026-11-24 (run 45410724759, ffmpeg 8.0.1 already on the runner image, so no apt) the step took **12 min 24 s** (14:10:34 to 14:22:58) for 8 books, about 93 s a book (15 `build-audio` at about 2 s each plus `build-listen`), the whole job 20 min 47 s (14:08:08 to 14:28:55), the publish step 5 min 36 s.
After: **pending**, measured on the release run after merge (a one-book change: the step zips that book's behind packs only, stages its committed tracks, installs nothing; target under 3 minutes).

#### Not done, listed (scope)

- The release run after merge must confirm that every book's `meta.json` lists `listenSeconds` for all 15 languages, and give the "after" time above. Both are marked pending, not measured.
- The 551 MB of tracks are new Git LFS objects: about 526 MiB added to the repo's 10 GiB LFS storage allowance (on top of the ~510 MB of clips). The first release run after merge pulls all 240 of them (one `git lfs pull --include` per pair, about 526 MiB of the 10 GiB monthly download allowance); later runs pull only the tracks of the pairs they upload.
- *Watch* (post-merge release run of `dino-store.yml`, items 2 and 6): every book's `meta.json` lists `listenSeconds` for all 15 languages, and the "after" time of the audio-pack step is read from that run's log and written into *Before and after*.
- The job's `timeout-minutes: 60` was raised for the backfill (Slice 281) and was left as is.

#### From review

- Skipped #7 (`reviews/s409-high.md`): "The track-older-than-its-clips check needs real clips, so it cannot run in the pull-request check (clips are LFS pointers there)" - by design, a known limit: a PR that changes clips without running `build-listen-all` passes the gate and fails after merge in `dino-store.yml` as `<book>/record` (that book uploads nothing until a fix PR); stated in `.github/CLAUDE.md`.
- Applied (`reviews/s409-medium.md`) #1-#6: a pull-request check `.github/scripts/check-audio-record.mjs` (in `tools-selftests.yml`'s `audio-alignment` job, file names only) fails a PR that changes a language's clips without its `listen-<lang>-v*.m4a` and the book's `audio-cast.json` (this supersedes the "known limit" of #7 above); `<book>/record` names `build-listen-all`; `build-listen` rejects `--clips`/`--out`; `build-listen-all` names each failing `stem/language` and ends with `check-audio`; `stage-listen` names the expected file. #2 (pointer clips give an unknown hash, not "clips changed") already held because `PackFiles` rejects a pointer clip and `HashOrNull` maps that to null; a test now pins it for a partly pulled pack.
- Applied #1-#6: a held-back book restores its committed `audio-cast.json`; `build-listen-all` skips a current language (no new LFS object on a rerun) and has `--force`, skips pointer manifests and counts a failed language instead of ending the run; CI pulls only the tracks of the pairs it uploads (`git lfs pull --include` per pair, never all); tests for staging and the skip/failure handling.
