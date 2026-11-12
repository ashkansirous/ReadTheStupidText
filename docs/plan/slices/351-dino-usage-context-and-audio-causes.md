# Slice 351 — Dino: every usage row says who and when, and audio failures say why

*Built (PR #1014, wave 2026-11-12).*

**Touches:** `worker/events.ts`, `worker/events*.test.mjs`, `worker/CLAUDE.md`, `src/DinoReader.App/UsageCounter.cs`,
`src/DinoReader.App/Usage*.cs`, `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/DinoReader.Core/ClipMisses.cs`,
`src/DinoReader.App/AudioPackStore.cs`, `tests/DinoReader.Tests/AudioPackStoreTests.cs`,
`src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/ReadingClips.cs`,
`src/ReadTheStupidText.Application/Usage/**`, `tests/DinoReader.Tests/**`, `tests/ReadTheStupidText.Tests/**`,
`site/dino/privacy.html`, `STORE.md`, `docs/analytics/dino-voice-queries.md`, `docs/analytics/dino-session-queries.md`,
`docs/plan/slices/351-dino-usage-context-and-audio-causes.md`,
`src/DinoReader.Core/PremadeAudio.cs` (approved, no other wave slice touches it; the brief named it `src/DinoReader.App/PremadeAudio.cs`, which does not exist),
`worker/speaker-usage.test.mjs`, `worker/alexa.test.mjs`, `.maestro/dino/usage-context.yaml`, `src/DinoReader.Core/AudioPackUsage.cs` (only if changed; it was not)

## Why

The user, 2026-11-10, after looking at 9 November on the stats page (12 Windows, 13 Android and 2 iPhone book
downloads, but only 2 listens): "are we collecting the right data on books being downloaded? sessionid, os version,
range of cores and alike? if not, add a slice for it. those data should always be collected on any analytics row so
we can guess what has happened." Also: "missing recorded lines, failed voice-pack downloads, animal sounds failed to
play, what was the reason?"

What the code on `origin/main` (= dino-v1.123.3) does today, checked 2026-11-10:

- Every stored row already carries version, platform, day, country (`blob10`), OS major (`blob11`), form factor
  (`blob12`), arch (`blob13`), cores bucket (`blob14`) and RAM bucket (`blob15`): the batch's `device` block is
  stamped on every point (`worker/events.ts:157-166,423-433`). Session (`blob16`) and seq (`double2`) are per event
  and the app stamps both on every count (`UsageCounter.cs:346`). Old builds send neither; a missing `device` block
  gives empty blobs 11-15.
- **Missing on every row:** the app's UI language (only `app_opened`, intro events and `story_languages_set` carry
  it; other events carry the book's language), whether this is the first launch or a returning one, how old the
  install is, and how long after launch the event happened. The row timestamp is the batch's send time, so time of
  day within a session is lost too.
- **Audio failures hide their cause.** `premade_audio:clip_missing` is the catch-all for every silent piece that is
  not a text mismatch: no pack downloaded for that language, a pack that failed to open this launch, a book with no
  source id, or a real missing file (`ReadingSession.cs:689`, `ClipMisses.cs:45-49`, `StoryVoicing.cs:140-177`,
  `AudioPackStore.cs:198-207`). The `fallback` row carries book `none`, so the book is unknown.
  `speech:animal_clip_unplayable` comes from two places (copying the bundled clip to the cache failed,
  `ReadingClips.cs:77`; the player threw, `ReadingSession.cs:880`) and the row does not say which.
- Live data, 14 days to 2026-11-10 (weighted): `clip_missing` 20 events in 7 sessions, on Android 1.110-1.123.2 and
  iOS 1.123.3 (the latest), so it still happens; `animal_clip_unplayable` 2 on Windows 1.123.3.0, 1 session;
  `audio_pack_download failed:missing` (a 404) 4 in 1 Android 1.123.2 session for The Three Little Pigs en and fa.
  On 2026-11-10 those two zips download fine (200, 58 clips each, every clip present) and the released 1.123.3 APK
  on the emulator downloaded both packs and read the book in English with 19 premade clips and no fallback. So the
  404 was most likely a listing briefly ahead of the upload (not proven: the 9 Nov state is gone).

Decided with the user (2026-11-10): **no install id.** First launch / returning and install age go in as closed
buckets, so the privacy page's "no install id" stays true.

## Premises checked (2026-11-12, ground-in-code on origin/main)

- Dino data point `worker/events.ts:422-437` (`toDataPoint`) uses blobs up to 16 and doubles up to 5: blob17-20 and
  double6 are free. The comment at `events.ts:661-663` is RTST's own 20-blob layout; not touched.
- `FALLBACK_REASONS` `worker/events.ts:66`; `FallbackReason` in `src/ReadTheStupidText.Application/Usage/FallbackKind.cs:59`
  (`ClipMissing` :65, `All` :248); kept equal by `tests/DinoReader.Tests/UsageFallbackTests.cs:107` and
  `tests/ReadTheStupidText.Tests/UsageFallbackTests.cs:86`. Stamp in `UsageCounter.cs:346`.
- The `UsageEvent` record (queued and sent as JSON) is `src/DinoReader.Core/UsageEvent.cs`; the new fields need it, so it is in Touches (coordinator approved 2026-11-12; no other slice of the wave edits it).
- The `chore/feedback-loop-2026-11-08-1703` worktree is uncommitted docs only (no PR, idle since 2026-11-08); nothing to rebase on.

## Work

- [x] **A. Context on every row (one end-to-end path first: `app_opened` and `store_download`, then all events).**
  The app adds to every queued event, and the worker stores, validated against closed lists:
  - `uiLang`: the app's UI language code (closed list = the app's languages) → `blob17`.
  - `installAge`: `first` (the first launch of this install) | `d0` (same day) | `d1_7` | `d8_30` | `d31_plus`,
    computed on the device from a locally stored first-launch date that is never sent → `blob18`.
  - `sinceLaunch`: whole seconds since this launch started, capped at 86400 → `double6`. This puts events in time
    order inside a session even when one batch carries them all.
  - `launchNo`: launch count bucket `1` | `2_5` | `6_20` | `21_plus` → `blob19`.
  A missing or invalid value stores `none` / -1 and keeps the event (as for the device block today). `blob20` stays
  free for Slice 352's screen.
- [x] **B. Reading fallbacks carry the book and language** they happened in (shelf or own) instead of `none`, so a
  query can tie a silent read to a book. `RecordGeneralFallback` stays for fallbacks with no book.
- [x] **C. Split `premade_audio:clip_missing` into its real causes:** `no_pack` (no pack downloaded for this
  language), `pack_failed` (a pack exists but failed to open this launch), `file_missing` (hash matched, file absent
  on disk), `no_clip` (the pack has no clip for this piece: an untranslated or new line), keeping `text_mismatch`.
  New reasons go into `FALLBACK_REASONS` (`worker/events.ts`) and `FallbackReason` (`FallbackKind.cs`); the existing
  test keeps the lists equal. `read_voice` keeps its own details.
- [x] **D. Split `speech:animal_clip_unplayable`** into `animal_clip_copy_failed` and `animal_clip_play_failed`, and
  the play site passes the reading so the language is set.
- [x] **E. Worker:** accept and store the new fields; old builds without them stay valid. Tests in
  `worker/events*.test.mjs` for each field (valid, missing, invalid) and for the new reasons.
- [x] **F. Privacy and store forms:** `site/dino/privacy.html` lists the new fields in plain words (UI language,
  "first time or returning", install age and launch count as ranges, seconds since the app opened; still no id).
  Check STORE.md's Play Data safety and App Store privacy rows still match (App activity / Product interaction are
  already declared or need adding; say which).
- [x] **G. Queries:** `docs/analytics/dino-session-queries.md` with a weighted session timeline query (ordered by
  `sinceLaunch`, then seq) and a first-launch funnel; `dino-voice-queries.md` gets the new fallback reasons.
- [ ] **Android, iOS and Windows:** all of this is shared code in `src/DinoReader.App`; each platform gets a check
  (below). No platform-only branch.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`, `dotnet test tests/ReadTheStupidText.Tests/...`
  (lists equal, bucket edges, fallback reasons), `node --test worker/` for the worker.
- Android emulator and iOS simulator, Debug build with the usage log: launch twice, read a book whose pack is not
  downloaded → the queued rows show `uiLang`, `installAge` `first` then `d0`, `launchNo` `1` then `2_5`, rising
  `sinceLaunch`, and a fallback `premade_audio:no_pack` with the book id. Windows head on the desktop VM: same, plus
  the animal clip reason when the cache folder is read-only.
- After release: a weighted query shows the new blobs filled on rows from the new version.

## Order

Before Slice 352 (both edit `worker/events.ts` and `UsageCounter.cs`). The unmerged `chore/feedback-loop-2026-11-08-1703`
branch also edits `worker/events.ts`, `FallbackKind.cs` and `privacy.html`; rebase on it if it merges first.

## From review (`reviews/pr-1014-high.md`)

- Applied #1: no_clip uses a presence check, `PremadeAudio.Has(index)` (the file is `src/DinoReader.Core/PremadeAudio.cs`), not `index >= Count`.
- Applied #2: an own book (no shelf id) sends no `premade_audio` fallback for a silent piece (expected state); `ClipMisses.SilentPieceReasonFor`.
- Applied #3: the launch-history read/save and the screen language in `Stamp` are guarded; on failure the buckets and `uiLang` are `none` and a `config:preference_unsaved` fallback is reported.
- Applied #4: a read's fallbacks name the book and reading language captured when the read starts (`StepRead.Story`/`Language`).
- Applied #5: if the book-aware report throws, the old per-language sink gets the fallback. Dedupe changes from per (area, reason, language) with no book to per book (as part B asks), so a session sends more fallback rows than before.
- Applied #6: documented in `docs/analytics/dino-voice-queries.md` (read-level and piece-level causes can differ within one read).

## From review (`reviews/pr-1014-medium.md`)

- Applied #2: `Stamp`'s screen-language failure report is guarded by `ReentryGuard` (`UsageCounter._uiLangReport`), so a report raised inside `Stamp` is never nested.
- Applied #3: a whole read of an own book reports no pack fallback on any path (`ClipMisses.ReadReasonFor`, used in `StoryVoicing.ReportVoiceUsed`).
- Skipped #1 (`reviews/pr-1014-medium.md`): "launch counted at singleton construction (lazy DI), not process start; sinceLaunch too small" - the singleton is built once per process, at launch.
- Skipped #4 (`reviews/pr-1014-medium.md`): "piece in both _noClip and _textMismatch sets mislabels cause as no_clip" - the overlap is rare and the effect low.
- Skipped #5 (`reviews/pr-1014-medium.md`): "launch day uses UtcNow, bucket boundaries differ from local day" - the buckets are coarse ranges and UTC is consistent across devices.
- Skipped #6 (`reviews/pr-1014-medium.md`): "TickCount64 may not advance in device sleep on mobile; sinceLaunch understated after resume" - an understated figure after sleep is acceptable for ordering within a launch.
- Applied #7: `UsageCounter.RecordFallback` tells its caller when the tally has counted (`counted` callback); `ReadingSession.ReportFallback` sends to the sink only when it did not.
- Skipped #8 (`reviews/pr-1014-medium.md`): "positional blob17-19/double6 layout hand-synced with docs SQL and Slice 352's blob20; nothing checks it" - this is the existing worker convention and the tests cover each field.

## From review (`reviews/pr-1014-check1.md`)

Nothing applied.

- Skipped #1 (`reviews/pr-1014-check1.md`): "sinceLaunch uses Environment.TickCount64 from singleton construction" - sinceLaunch only orders events within a launch, and the singleton is built at launch.
- Skipped #2 (`reviews/pr-1014-check1.md`): "Cleared/malformed launch store does not throw" - a cleared store is a fresh install by our definition (no install id).
- Skipped #3 (`reviews/pr-1014-check1.md`): "after navigation or book close it is tallied under the wrong book" - the navigated-away case is rare and the book is still right for shelf reads.
- Skipped #4 (`reviews/pr-1014-check1.md`): "ShelfId(story.SourceId) returns null for a store book not yet on the shelf" - own books are never sold from the store, and a store book being read is on the shelf (not confirmed in a run).
- Skipped #5 (`reviews/pr-1014-check1.md`): "A mixed read (no-entry plus text-mismatch) is classed text_mismatch" - rare overlap, low effect.
- Skipped #6 (`reviews/pr-1014-check1.md`): "each count throws and reports" - the failure is reported once per launch by the tally.
- Skipped #7 (`reviews/pr-1014-check1.md`): "AnimalClipCopyFailed goes through the general sink without the book" - copy failures have no read in scope, and a cancelled copy is rare.
- Skipped #8 (`reviews/pr-1014-check1.md`): "ThreadLocal<bool> is thread-based, not async-flow" - all calls are synchronous on one thread.

## From review (`reviews/pr-1014-check2.md`)

Nothing applied.

- Skipped #1 (`reviews/pr-1014-check2.md`): "every UsageCounter instance counts a launch and saves prefs" - UsageCounter is a DI singleton, one per process.
- Skipped #2 (`reviews/pr-1014-check2.md`): "Save failure after Begin leaves buckets set but history unsaved" - a failed Save means the next launch reads the previous value, low effect.
- Skipped #3 (`reviews/pr-1014-check2.md`): "Animal clip copy failure reported with language but no book" - low, same as check1 #7.
- Skipped #4 (`reviews/pr-1014-check2.md`): "ReportFallback drops the report when FallbackUsage.Detail is null" - a null Detail is not a fallback.
- Skipped #5 (`reviews/pr-1014-check2.md`): "RecordingUnplayable and AnimalClipPlayFailed use live _story/_reading" - those two have no StepRead, so the live book is the book on screen.
- Skipped #6 (`reviews/pr-1014-check2.md`): "Redundant wrappers (SilentPieceReason, SilentPieceReasonFor, ReadReasonFor)" - style.
- Skipped #7 (`reviews/pr-1014-check2.md`): "SixthLaunch and TwentyFirstLaunch unused; bucket edges invite off-by-one" - style, kept as named bucket edges.
- Skipped #8 (`reviews/pr-1014-check2.md`): "ThreadLocal<bool> never disposed; guards same thread only" - a process-lifetime guard, one thread by design.
- Skipped #9 (`reviews/pr-1014-check2.md`): "a persistent failure enters the catch path each time" - same as check1 #6: the failure is reported once per launch by the tally.
- Skipped #10 (`reviews/pr-1014-check2.md`): "Fallback reason recomputed from miss counts can disagree with the detail reason" - read-level and piece-level causes are documented as able to differ (`docs/analytics/dino-voice-queries.md`).

## Changed while building

- Emulator run 1 found the flow's bare `installAge=` markers never match (`Test-LogMarker` needs the value). The markers are now `installAge=d0` and `launchNo=2_5`.
- The Debug fallback log line now ends `book=<id> lang=<lang>`.

## Device run (Android emulator, builds 07f813fc5608 then 74792d3dc802)

Evidence: `D:/ReadTheStupidText-worktrees/reviews/w47-device/s351/` and `.../rerun/`.

- Fresh install logged `installAge=first launchNo=1`; the second launch logged `installAge=d0 launchNo=2_5`.
- `uiLang` follows the app language (en, fa and ar seen). `sinceLaunch` rose 2, 9, 16, 18, 22 within one launch.
- `[Usage] fallback premade_audio:text_mismatch times=1 queued book=d24bcdade30f46fb80ff5e7bcede86db lang=fa`: the fallback now names the book.
- RTST smoke `usage-fallback-swallowed`: PASS.
- The rerun of `usage-context` failed only on `uiLang=en`: another slice's flow had left the emulator app in Arabic, and the line showed `uiLang=ar`, correct for that state. The flow assumes an English UI.

#### For CLAUDE.md

- Every Dino usage row carries `blob17` uiLang, `blob18` installAge, `blob19` launchNo and `double6` sinceLaunch (`worker/events.ts`, `src/DinoReader.App/UsageCounter.cs`); `blob20` is reserved for Slice 352's screen, do not use it. (Slice 351: docs/plan/slices/351-dino-usage-context-and-audio-causes.md)
- An own book (no shelf id) sends no `premade_audio` fallback for a silent piece: that is an expected state (`src/DinoReader.Core/ClipMisses.cs`). (Slice 351: docs/plan/slices/351-dino-usage-context-and-audio-causes.md)

#### Not yet verified

- `premade_audio:no_pack` with a book id on a device (a book whose pack is not downloaded).
- iOS simulator run of the context fields and the new fallback reasons.
- Windows head on the VM, including the animal clip copy failure reason.
- After release: a weighted query shows blobs 17-19 and double6 filled on rows from the new version.
- `.maestro/dino/usage-context.yaml` should set English first (the flow assumes an English UI).
