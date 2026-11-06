# Slice 281 — Dino: one listening track per store book and language, served for smart speakers

*Planned 2026-11-06.* Server and build tooling only; the Android, iOS and Windows apps do not change.

**Depends on:** nothing. Slices 282 (Alexa) and 283 (Sonos) both depend on this one.
**Touches:** `tools/DinoReader.BookBuilder/ListenTrack.cs` (new), `tools/DinoReader.BookBuilder/Program.cs` (one new command),
`tests/DinoReader.Tests/ListenTrack*.cs` (new), `.github/workflows/release.yml` (the `publish-store` job only),
`worker/stories.ts`, `worker/stories-listen.test.mjs` (new), `worker/CLAUDE.md`, `PRIVACY.md`, `site/dino/privacy.html`, this file.
Program.cs is also changed by Slice 279 (draft PR #867, `JoinAudio`): start this after #867 merges, and reuse its join code instead of writing a second one.

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
  a version, `meta.json` written last (Decision 163). `meta.json`'s `audio.<lang>` gains `listenUrl` and `listenSeconds`.
- New public route `GET|HEAD /api/stories/<id>/listen/<lang>?v=<n>`: `audio/mp4`, inline (not attachment), **HTTP Range
  support** (both speakers seek), immutable cache, 404 unless `meta.json` lists that language and version (same rule as the
  zip route). No auth: the books and zips are already public.
- No new telemetry. Speaker plays are not counted (closed-list event rule in `worker/CLAUDE.md`); a later slice may add a
  counted, id-free event if the user wants numbers.
- Privacy text: say that Amazon and Sonos fetch the book audio when a family plays it there, that we receive no account,
  name or id from them, and that their own privacy terms apply on their devices. The "no accounts" promise stays true.

## Work

- [ ] Context7 / `library-docs` first for anything new used in the join (ffmpeg flags or the .NET AAC path #867 uses).
- [ ] `ListenTrack.cs`: from a `.dino` story plus its audio pack, list clips in page and sentence order (page mapping from
      the story, since the manifest has none), join them with the gaps, write `listen-<lang>-v<n>.m4a`. One method one job.
- [ ] Unit tests: ordering by page then sentence, gap lengths, a missing clip fails the build (never a silent hole).
- [ ] `publish-store`: build and upload the track for every language that has a pack, before `meta.json`.
- [ ] Worker route with Range (206 + `Content-Range`, 416 for bad ranges), HEAD, the version check; tests for each.
- [ ] Privacy text in `PRIVACY.md` and `site/dino/privacy.html`, same wording in both.
- [ ] `worker/CLAUDE.md`: the listen route and its rules, one short paragraph.

## Verification

- Unit tests and Worker tests green.
- After merge: `curl -I` and a ranged `curl -r 0-1023` on one live English track answer 200 and 206 with `audio/mp4`;
  the full file plays in a desktop player from start to end with page gaps audible.
- Every live store book has a track for every language with a pack (compare `/api/stories` `audio` with the new fields).
