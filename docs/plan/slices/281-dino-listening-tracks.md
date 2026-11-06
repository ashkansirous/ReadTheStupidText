# Slice 281 — Dino: one listening track per store book and language, served for smart speakers

*Built 2026-11-06 (wave 2026-11-06, ledger wave 33), merged as PR #876 (e74bb206: BookBuilder `build-listen`, `AudioPacks`, Worker route and tests) and PR #878 (edec1d8e) with the publish side (`dino-store.yml`, `publish-store-books.mjs` and tests, `ship-targets.test.mjs`, `.github/CLAUDE.md`, this file), which the coordinator's commit brief for #876 left out by mistake (it named only tools/, tests/ and worker/ for staging). Not yet verified live (first `dino-store` run after merge, a live track).* Server and build tooling only; the Android, iOS and Windows apps do not change.

**Depends on:** nothing. Slices 282 (Alexa) and 283 (Sonos) both depend on this one.
**Touches:** `tools/DinoReader.BookBuilder/ListenTrack*.cs` (new), `tools/DinoReader.BookBuilder/Program.cs` (one new command),
`tools/DinoReader.BookBuilder/StoreBuilder.cs` and `tools/DinoReader.BookBuilder/AudioPacks.cs` (the `meta.json` `audio.<lang>` fields),
`tests/DinoReader.Tests/ListenTrack*.cs` (new), `tests/DinoReader.Tests/**` (meta.json audio tests only),
`.github/workflows/dino-store.yml` (the `publish-store` job only), `.github/scripts/publish-store-books.mjs`,
`.github/scripts/publish-store-books.test.mjs`, `.github/CLAUDE.md`,
`worker/stories.ts`, `worker/stories-listen.test.mjs` (new), `worker/CLAUDE.md`, this file. (`PRIVACY.md` and `site/dino/privacy.html` were listed at planning and moved to Slices 282/283; `.github/scripts/ship-targets.test.mjs` was also edited, a regex in the test only, outside Touches.)

*Claim notes (ground-in-code, 2026-11-06):* `publish-store` lives in `.github/workflows/dino-store.yml:34`, not `release.yml`;
the upload order is in `.github/scripts/publish-store-books.mjs` (zip, cover, package, `meta.json` last) and its `--audio-verdict`
gate applies to the listen tracks too (a track is uploaded only where its pack is). `meta.json` `audio.<lang>` is written by
`StoreBuilder.cs:121` / `AudioPacks.cs:73` and parsed by `worker/stories.ts` (`AUDIO_FILE`). There is **no clip-joining code**
in the repo: `JoinAudio.cs` is Slice 280's fa/ar manifest rewriter. The join is new, done with ffmpeg (as
`tools/premade-audio/encode.py` already uses: native `aac`, mono 48 kb/s, `+faststart`); the job installs ffmpeg if the runner
lacks it. The Worker's zip route (`serveAudio` / `serveObject` in `downloads.ts`) may already handle Range: reuse it, and
the router needs a `listen` path segment beside `audio`. `/api/stories/*` is already run-worker-first, so no wrangler change.

## Why

The user wants the Dino store books playable on Alexa and Sonos (2026-11-06). Scope settled with the user in this
conversation: **no login, no parent recordings, store books only, both Alexa and Sonos.**

Both platforms fetch audio themselves from a public HTTPS address; neither can play a file that only exists on a phone
(Alexa: "internet-accessible HTTPS endpoint on port 443",
https://developer.amazon.com/en-US/docs/alexa/custom-skills/use-long-form-audio.html; Sonos music services are server-hosted,
https://docs.sonos.com/docs/key-requirements). Today a book's audio sits on R2 only as one zip per language
(`dinoreader/stories/<id>/audio-<lang>-v<n>.zip`, per-sentence AAC `.m4a` clips, a `manifest.json` with sentence index
but no page numbers or timings; `tools/DinoReader.BookBuilder/AudioPacks.cs`, `tools/premade-audio/README.md`). Neither
speaker can play a zip.

## Decisions (planning defaults, change them here if the user disagrees)

- **One track per book per language**, `listen-<lang>-v<n>.m4a` (AAC, which Alexa and Sonos both accept), made by
  joining the pack's clips in reading order with the app's own gap between sentences and a longer gap between pages.
  The book's version `v<n>` is the audio pack's version, so a new pack makes a new track.
  A per-page track list was considered: it gives Sonos "pages as tracks" but makes Alexa queue dozens of streams.
  One track per book is simpler for both and Alexa resumes it from the offset it reports itself (no server state).
- Built and uploaded by the existing `publish-store` job, next to the zip, under the same rule as the zip: never overwrite
  a version, `meta.json` written last (Decision 163). `meta.json`'s `audio.<lang>` gains `listenSeconds` (the Worker adds `listenUrl`).
  Publishing is book by book with a time budget (900 s of the build loop per run; the rest waits for the next run), a track is
  live only when the book's `meta.json` in the bucket lists it (never the Worker's list), and a track failure never blocks the book's zip (review fixes, see Built).
- New public route `GET|HEAD /api/stories/<id>/listen/<lang>?v=<n>`: `audio/mp4`, inline (not attachment), **HTTP Range
  support** (both speakers seek), immutable cache, 404 unless `meta.json` lists that language and version (same rule as the
  zip route). No auth: the books and zips are already public.
- No new telemetry. Speaker plays are not counted (closed-list event rule in `worker/CLAUDE.md`); a later slice may add a
  counted, id-free event if the user wants numbers.
- Privacy text: **not in this slice** (moved out after the high review: the services do not exist yet and the site deploys on merge).
  Slices 282 and 283 each add it with the wording: Amazon and Sonos fetch the book audio when a family plays it there, we
  receive no account, name or id from them, and their own privacy terms apply on their devices. The "no accounts" promise stays true.

## Built (builder notes, 2026-11-06)

- **Gaps, from the app code.** Between sentences: `VoicedAudio.DefaultSentenceGap` = 150 ms (`src/ReadTheStupidText.Application/Reading/IVoicedAudioTransform.cs:30`; `PremadeAudio.SentenceGap` and `PhraseSplitter.SentenceGapMs` use the same value). **The app has no page gap**: pages are one text joined by `Story.PageSeparator` (`src/DinoReader.Core/Story.cs:106`) and reading runs on at the 150 ms sentence gap while the picture changes (`PlaybackSteps.BeatMilliseconds` = 1200 ms is the step-through beat, not a page pause). The track's page-turn gap is therefore new: `ListenTrackPlan.PageGapMs` = 900 ms, for a listener with no picture. Change the constant if the user wants another.
- **Build:** new `build-listen <stem> --lang <code>[,<code>...]` (`ListenTrack.cs`, `ListenTrackPlan.cs`): order and pages from `SentenceDump.Build` plus the page of each sentence span (the story is built once per call, for all the languages); ONE ffmpeg process per track (review fix, design 2 after the medium review 4, 9, 10): the **concat demuxer**, not a filter graph. An ffconcat list file (`ffconcat version 1.0`, one `file '<abs path>'` line per clip or silence, forward slashes, `'` written `'\''`; read with `-f concat -safe 0`, per the ffmpeg docs checked with context7 `/websites/ffmpeg_documentation`) names the clips and one small silence `.m4a` per distinct gap (150 ms and 900 ms, made first with `anullsrc` at the clips' sample rate, because the demuxer needs one format throughout; `RequireOneFormat` fails a track whose clips differ in rate or channels, reading the format by a box walk to `mp4a` with an ffprobe fallback. Measured on the real clips: the en, fa and ar packs of freddy-and-the-slipper and billy-goats-gruff are all AAC 24 kHz mono), and one encode writes the track (native `aac`, mono, 48 kb/s, `-ar 48000`, `+faststart`). No command-line argument grows with the book (a 3000-clip book is the same command as a 10-clip one, with a 6000-line list) and only one file is open at a time; a clip used twice is just two list lines. The length is `ffprobe -show_entries format=duration` (`-of default=noprint_wrappers=1:nokey=1`) on the finished file: `N/A`, empty, non-numeric or non-positive fails that track (the old last-`out_time_us` read would have rejected a good track). ffprobe is `$FFPROBE`, else beside `$FFMPEG`, else the PATH. Timed here: 300 clips of 2 s joined in under 7 s (a whole throwaway test, tone creation included), the track's length asserted between 600 and 700 s (300 x 2 s plus the gaps). A piece without a manifest clip, a missing clip file, a missing ffmpeg/ffprobe, a full disk or any other exception fails that language only (`TryBuildOne` catches everything but a cancel and names the language). The manifest is read by `AudioPacks.ClipsByIndex` (the pack's own version, language, LFS-pointer and plain-file-name checks), not a second reader. The track length is recorded as `listenSeconds` in the pack's `audio-cast.json` record and listed in `meta.json` `audio.<lang>`.
- **Deviation from the Decisions text:** `meta.json` carries `listenSeconds` only, not `listenUrl`: the Worker builds the link (the list adds `listenUrl`), as it does for the pack's `url`, because the link depends on the host prefix (`/dino` on workers.dev).
- **Publish (review fixes, high review 1, 2, 3, 5, 6):** `dino-store.yml` runs `build-listen` once per book after that book's `build-audio` runs. The backfill is incremental: the build loop starts no new book after `LISTEN_BUDGET_SECONDS` (900 s; the first book always runs; the job's `timeout-minutes` is now 60: 15 min of builds + ~10 min of the normal run + ~10 min of live-state reads + ~10 min of uploads + margin, estimates, untimed), the step summary counts the packs not reached, and the next push or daily cron continues; the publish step uploads book by book (zip and track, then that book's `meta.json`), so a cancel keeps every finished book. A track failure never blocks the zip: the book goes up without the track's length and a final step fails the job naming it; a missing ffmpeg/ffprobe (the apt install is non-fatal) skips the track builds, publishes everything else and fails the job at the end ("ffmpeg missing").
- **Liveness rule (design 2, medium review 1, 2, 6, 7):** a track is live only when the book's `meta.json` in the bucket lists `listenSeconds` for that language at that pack version (the Worker serves nothing else). Unlisted = not served = may be built and (over)written; listed = neither built nor uploaded (Decision 163 holds by construction). Meta is written last with the length of the file just uploaded, so meta and bucket always describe the same bytes (`reconcileListen`: a listed track keeps the bucket's length, an unlisted one keeps the built length only when its file is in the folder). **One meta read per story per job:** `--live-state` (step "Read the live store state", which already read every live story's `meta.json`, twice per story before; now once) writes each story's `audio` (`audioEntry`: version and `listenSeconds`) into `live-store.json`, and `--audio-behind` and the publish step read it with `--bucket-state <file>` (required, no bucket request, no per-track `get`: `packageInBucket` is no longer used for tracks). **Reading errors:** `metaInBucket` uses `publishedStateFrom` in `.github/scripts/r2-wrangler.mjs`, which returns "absent" only for a missing key (404/NoSuchKey/10007 text) and throws for anything else, including a missing bucket, auth and network (read in that file; the same helper the package probe and `isManagedStory` use). That throw fails the `--live-state` step, before anything is built or uploaded, so an error is never read as "absent", never delists, and never skips a track as unlisted (this is stricter than design 2's "skip that book": the whole job stops at its first step on a read error, one wrangler failure would be an auth or network failure for every book anyway). The old `bucketAudioOf` that swallowed every error is gone.
- **Privacy text removed from this slice (high review 4):** the Alexa/Sonos paragraphs in `PRIVACY.md` and `site/dino/privacy.html` are taken out: those services do not exist yet, and the site deploys on merge, so the text would state an untrue data flow. Slices 282 (Alexa) and 283 (Sonos) each carry their own privacy work item, with the same wording as the text removed here (the free stories are fetched by Amazon or Sonos as a plain file request; no account, name or identifier reaches us; their own terms apply).

- **Worker route:** `GET|HEAD /api/stories/<id>/listen/<lang>?v=<n>` in `worker/stories.ts`, `audio/mp4` inline, Range through `serveObject`, 404 with no-cache (a not-yet-listed track is never cached).
- **Known limit:** a track stays at its pack version, so a change that only moves page breaks (or the gap constants) without a pack bump keeps the old track until the pack version moves.

## Changed while building

- Page gap 900 ms chosen by the builders (the app has none); sentence gap 150 ms = `VoicedAudio.DefaultSentenceGap`.
- `meta.json` carries `listenSeconds` only; the Worker builds `listenUrl`.
- Privacy text moved to Slices 282/283 (no speaker uses the tracks yet).
- Publish became incremental and book by book (900 s build budget, `timeout-minutes: 60`), liveness read once from the bucket's `meta.json` in `--live-state`, and a track or ffmpeg failure fails the job at the end without blocking zips (details under Built).
- `ship-targets.test.mjs` regex updated (test only, outside Touches).

## From review

Files in `D:/ReadTheStupidText-worktrees/reviews/` (outside the repo):

- `s281-high.md`: 1, 2, 3, 5, 6 applied via design 1 (`s281-design-1.md`); 7, 8 applied; 4 (privacy text claims a feature that does not exist yet) applied by moving the text to Slices 282/283; 9 (info, `check-site-eol.mjs`, no change needed) skipped.
- `s281-medium.md`: all 10 applied via design 2 (`s281-design-2.md`).
- `s281-check1.md`: 1, 2, 4, 6, 9 applied; 8 already handled (the listen 404 is no-cache); skipped: 7 (live state read once at job start, a concurrent run could list a track meanwhile: skipped because of the `dino-store` concurrency group), 3 (HE-AAC/SBR rate: `encode.py` makes native AAC-LC only, and the guard compares clips with each other), 5 (900 s budget checked only between books: one book joins in seconds, 300 clips in under 7 s), 10 (build-audio re-zips a pack when only the track is behind: efficiency, low).
- `s281-check2.md`: all 5 skipped: 1 (rate above 65535 Hz reads 0: all clips are 24 kHz), 2 (stale track when only page breaks change: the known limit above), 3 (unguarded temp-folder delete: low severity), 4 (header walk covers 64 KB only: the clips are faststart), 5 (~21 ms drift per boundary from AAC priming: accepted).

## Work

- [x] Context7 / `library-docs` first for anything new used in the join (ffmpeg flags or the .NET AAC path #867 uses).
- [x] `ListenTrack.cs`: from a `.dino` story plus its audio pack, list clips in page and sentence order (page mapping from
      the story, since the manifest has none), join them with the gaps, write `listen-<lang>-v<n>.m4a`. One method one job.
- [x] Unit tests: ordering by page then sentence, gap lengths, a missing clip fails the build (never a silent hole).
- [x] `publish-store`: build and upload the track for every language that has a pack, before `meta.json`.
- [x] Worker route with Range (206 + `Content-Range`, 416 for bad ranges), HEAD, the version check; tests for each.
- [x] ~~Privacy text in `PRIVACY.md` and `site/dino/privacy.html`~~ removed from this slice (see Decisions); Slices 282 and 283 carry it.
- [x] `worker/CLAUDE.md`: the listen route and its rules, one short paragraph.

## Verification

- Unit tests and Worker tests green.
- After merge: `curl -I` and a ranged `curl -r 0-1023` on one live English track answer 200 and 206 with `audio/mp4`;
  the full file plays in a desktop player from start to end with page gaps audible.
- Every live store book has a track for every language with a pack (compare `/api/stories` `audio` with the new fields).

#### For CLAUDE.md (lifted 2026-11-06)

- `worker/CLAUDE.md` (already added by the slice): the listen route, `GET|HEAD /api/stories/<id>/listen/<lang>?v=<n>`, serves a track only when `meta.json` lists `listenSeconds` for that language and version; 404 is no-cache. (Slice 281: docs/plan/slices/281-dino-listening-tracks.md)
- `.github/CLAUDE.md` (already added by the slice): a listening track is live only when the bucket's `meta.json` lists `listenSeconds` at that pack version; read once in `--live-state`, any read error but not-found fails the job before uploads; never overwrite a listed track; meta last. (Slice 281: docs/plan/slices/281-dino-listening-tracks.md)

#### Not yet verified

- The first `dino-store` run after merge (the backfill takes several runs; watch it).
- `curl -I` and a ranged `curl -r 0-1023` on a live track answer 200 and 206 with `audio/mp4`.
- Play one track end to end (page gaps audible).
- Every live store book has a track for every language with a pack.
