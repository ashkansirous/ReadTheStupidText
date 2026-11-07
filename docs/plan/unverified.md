# Not yet tried on a device

Every slice's open Not yet verified checks, newest slice first. A line leaves when the check passes on a device or the user confirms it; plan.md's Outstanding work keeps only the count. The user (2026-11-01 07:31): "I dont have time to check your shit on ios"; the lines stay here until a device run or the user clears them.

### Slice 296: Why a Farsi read matched 5 of 78 pieces to clips ([slice](slices/296-farsi-read-clip-match.md))
- iOS: a Farsi read on an iPhone reports its clip count (no iOS `fa` reads in production in 30 days, so no log to read yet).
- Mid-book read start (`ClipOffsetAsync`) is not modelled or run; only reads from the start are covered.
- Runs 2 and 3 used throwaway flows not in the repo; only run 1's build label (`0ac339e41410`) is proven.

### Slice 311: Crash data for the Windows apps from the Microsoft Store ([slice](slices/311-windows-store-crash-data.md))
- A real pull with the Actions secrets exported for both apps (not readable on this PC): does it print rows or `none` for RTST `9NGT1BN1H92V` and Dino, and are the failure names symbolised?
- Whether either app is under an undocumented Store threshold; if a real pull returns no rows for an app with users, plan the self-reporting Windows slice (item 3, already decided).

### Slice 289: Dino: count Sonos plays on the server ([slice](slices/289-dino-count-sonos-plays.md))
- Whether Sonos's `getMediaURI` request comes from the player or from Sonos's cloud (decides what the row's country means).
- That Sonos calls `getMediaURI` once per play (prefetch, resume, retry or reconnect may over-count plays and hours).
- Speaker rows carry no app version, so test or sandbox plays cannot be filtered by version: reports filter `blob7 = 'sonos'`, and Slice 287 must handle platform `sonos` explicitly.
- Country may be empty if the Request rebuilt by `withPath` in `worker/index.ts` drops `cf` (review check1 #2).
- A live Sonos play counts exactly one row, once the user's Sonos sandbox (Slice 283) works.

### Slice 314: Restore the stall overlay when the person returns to the reader ([slice](slices/314-reader-stall-overlay-after-return.md))
- The stall-return case itself on Android and iOS (start a stalled read, open Settings, come back: overlay and Cancel shown); no Debug hook forces a stall.
- The iOS stall watcher on device.

### Slice 312: Read the iPhone's crash logs from this PC ([slice](slices/312-read-iphone-crash-logs-from-this-pc.md))
- Item 2: a real pair and pull on the iPhone (needs the user's one-time Apple Devices/driver and libimobiledevice install, and one cable pairing).
- The real `.ips` field names and header layout: the parser was written from the known iOS crash format and checked only against a synthetic fixture, so the first real file may need a tweak.

### Slice 310: Dino Debug hook to force a voice-pack download failure ([slice](slices/310-dino-debug-hook-force-pack-failure.md))
- iOS: the same hook and three flows on an iOS Debug build (no Mac or iPhone in the 2026-11-07 wave).

### Slice 297: Slice 267 follow-ups ([slice](slices/297-slice-267-follow-ups.md))
- iOS wizard layout (no Mac or iPhone tonight).
- `read_voice` counted at the first premade clip on iOS.
- Dino Windows head not built.

### Slice 305: Dino package cleanup ([slice](slices/305-dino-package-cleanup-ml-kit-and-sherpa.md))
- iOS TestFlight look of Dino and its IPA size (iOS only compiled).
- Windows VM run of the Dino Windows head (unchanged by design; 6.5 GB free).
- A parallel build of both apps in one msbuild invocation.

### Slice 303: Slice 201 review leftovers ([slice](slices/303-slice-201-review-leftovers.md))
- Windows read path with a bad pack (VM not run: free memory 6.5 GB, under the 8 GB needed).
- iOS device: bad Piper pack reports `pack_failed` and the guard stops the engine build.
- A real `packs.yml` run (next packs release): the new tokens check and fixture self-test.

### Slice 301: Slice 247 follow-ups, emulator runner ([slice](slices/301-slice-247-follow-ups-emulator-runner.md))
- A real crash-kill on a Mono SIGSEGV: the retry and the two attempt logs (only self-tests with stubs and fake processes, and a passing happy-path run).
- A real stuck Maestro tree (`RunnerStuck`, rest NOTRUN).

### Slice 300: Kept problem report with the crash switch off ([slice](slices/300-user-report-kept-while-crash-switch-off.md))
- Emulator: with the usage switch off, a failed user-report send shows the not-kept text and logs `not kept, usage switch off`. Needs a Debug hook that makes the report endpoint refuse; none exists.
- iOS device: the same shared code, not run on a device.

### Slice 307: ClaudeSkills hooks and guards leftovers ([slice](slices/307-claudeskills-hooks-and-guards-leftovers.md))
- Whether Claude Code shows `additionalContext` from a PreToolUse hook that returns no `permissionDecision` (the SDK type marks the field optional; not seen live).
- Item 10 (open, not built): `phone-network-guard.mjs` still wrongly denies a read-only grep with a quoted `\|adb` pattern.

### Slice 308: Slice 225 run-checks -Serial and failed-build cause ([slice](slices/308-slice-225-run-checks-serial-and-build-cause.md))
- Slice 225 item 2 cause (entries 249/494/630): no failed Debug build log exists on disk; the next failed build leaves `<log>.failed.log` to read.

### Slice 299: Slice 261 leftovers ([slice](slices/299-slice-261-leftovers.md))
- First-run picker and Cancel during the list load on emulator and iOS (item 2 follow-up: a cancelled load shows the "Connect to the internet" copy; a distinct copy needs new strings in every language).

### Slice 284: Final loop leftovers, night 2026-11-06-1250 ([slice](slices/284-final-loop-leftovers-night-2026-11-06-1250.md))
- Windows VM `dino-record` key-R stage (Stop button or the microphone alert, alert texts read from the host resx, dismiss): never run. Judge check3 findings 1-4 on that run.

### Slice 304: Slice 202 re-review and the InitWait order test ([slice](slices/304-slice-202-rereview-and-initwait-test.md))
- Review-picked language holds after Review's Read, and an edit clears it (device).
- Scan or import of the same text keeps the language (device).
- Language-change rebuild shows a loading state when it takes over 500 ms (review 9; caller in Settings/AppShell, not read by this slice).
- Stalled-read overlay is not restored after leaving and returning (review 6): `ISpeechReader` exposes no stalled state; follow-up is [Slice 314](slices/314-reader-stall-overlay-after-return.md).
- No scripted flow yet for Review-pick-then-Read.

### Slice 302: Slice 239 leftovers ([slice](slices/302-slice-239-leftovers.md))
- Item 1 was closed from a code read only: the "renamed store package downloaded twice" report was not reproduced on a device run, so a double download on a fresh install is not ruled out.

### Slice 298: Slice 264 gallery reorder follow-ups ([slice](slices/298-slice-264-gallery-reorder-follow-ups.md))
- Real drag in both drop event orders (Drop then DropCompleted, and the reverse) on Android, iOS and the Windows head.
- A 1 s hold, and a scroll that cancels the hold, on Android, iOS and the Windows head.
- Hold release after a relayout (rotation) on Android, iOS and the Windows head.
- Windows keyboard reorder on the Windows head.
- iOS compile of the changes not run.
- Tablet-upright flow not run.

### Slice 288: Dino: the app counts listens (30 s or more) and real listening time ([slice](slices/288-dino-listens-and-listening-time.md))
- usage-listen-seconds flow (`.maestro/dino/usage-listen-seconds.yaml`) on the Android emulator: the 2026-11-07 run was blocked at install (C: full, `INSTALL_FAILED_INTERNAL_ERROR`, res=-110).
- The same flow on the iOS simulator.
- The same flow on the Windows head.
- The release version and date of this slice, for Slice 287's dashboard note (version line not filled until it ships).

### Slice 294: The `-NoBuild` cache-key miss in the emulator runner ([slice](slices/294-emulator-nobuild-cache-key-miss.md))
- Item 3, the `first-run` flake: no failing log was kept, so no cause and no fix.
- `tools/phone/run-checks.ps1` `Stop-CacheMissed` miss printout does not print the inputs (outside this slice's Touches).

### Slice 292: Fix the flaky `TranslationModelDownloadSessionTests.Progress_is_reported_while_downloading` ([slice](slices/292-fix-flaky-translation-download-progress-test.md))
- The 200-run loop of `Progress_is_reported_while_downloading` (only 25 runs were done).

### Slice 286: Dino: record your voice in each story language, and hear it when reading in that language ([slice](slices/286-dino-recordings-per-story-language.md))
- iOS device or simulator: the record-per-language flow was not run.
- Windows head on a real screen: centring of the record intro on a wide window.
- The 14 hand-written language-name strings (`StoryLanguages_NameMap`) and the `Record_LanguageUnavailable` / `Record_LanguageRetry` translations have not been checked by native speakers.
- Emulator store audio had to be seeded because the store was unreachable in the test setup, so the real download path was not covered.

### Slice 290: the reading page's sound line as a pill ([slice](slices/290-dino-reading-sound-pill.md))
- Every pill state on a device against frames 197-199 (lilac Getting pill with the language name in Vazirmatn and "· N of M", Wi-Fi, Preparing, no sound), the medium weight and the new translations. No flow stops on the reading page while a pill shows (part B).
- The pill's accessibility text: no `ReadingAudioStatus` node appeared in uiautomator dumps during the language flow (part B).
- A Farsi or Arabic pill whose text wraps (label right-aligned, icon still left).
- iPhone and the Windows head (incl. the UIA name): not built here; shared code with no platform branches.

### Slice 285: Dino: changing the app language fetches the books' sound in that language ([slice](slices/285-dino-language-change-fetches-book-audio.md))
- iPhone: switch the app language, open a store book, press Play. Sound should play in the new language with no relaunch.
- Windows head: the same.
- The user's phone: Freddy in English after the v6 text update. The English `clip_missing` events of 2026-11-07 07:05Z have no code explanation yet.

### Slice 283: Dino: a Sonos music service for the store books ([slice](slices/283-dino-sonos-music-service.md))
- Live `POST /sonos` after the Worker deploy: getMetadata root lists the languages, getMediaURI returns the track URL.
- Sonos Self Test passes.
- A real play on a Sonos speaker.

### Slice 281: Dino: one listening track per store book and language ([slice](slices/281-dino-listening-tracks.md))
- The first `dino-store` run after merge (the backfill takes several runs; watch it).
- `curl -I` and a ranged `curl -r 0-1023` on a live track answer 200 and 206 with `audio/mp4`.
- Play one track end to end (page gaps audible).
- Every live store book has a track for every language with a pack.

### Slice 279: Dino: recording your own voice, one intro page, then per character or whole page ([slice](slices/279-dino-recording-flow.md))
- iPhone 8 / iOS simulator layout and recording (no Mac to run it).
- A real microphone recording on Windows (the VM has none; R opens Windows' microphone consent prompt).
- Backup merge with a whole-page clip and a piece clip on the same page, and orphan files after a same-key restore.
- Intro freeze on a long book: not measured.
- The 13 translations are machine-drafted.

### Slice 280: Dino: Farsi speaker first, joined at translation time ([slice](slices/280-dino-farsi-speaker-first-at-translation.md))
- iOS beyond compile.
- The Windows head.
- On a real phone: the live fa/ar packs after the upload.

### Slice 278: Dino: store books only, parts A and B ([slice](slices/278-dino-store-books-only.md))
- Part B: iOS device or simulator run.
- Part B: the mobile-data prompt (`ShelfAudioAsk`) on a real cellular-only phone.
- Part B: the beat pausing on screen lock (no scripted flow).
- Part B: flows not run this round: `dino-landscape-settings`, `premade-audio-past-roar`, `usage-playback-events`, `dino-landscape-store` (the first emulator run's results were lost: the agent piped `run-flows` through `Out-String`).
- Part B: a Release build downloads no voice pack.
- Part B: the Windows MSIX still packages (`APPX1101`) and the iOS app still links: project settings unchanged, not built beyond a compile.
- Android emulator flow `dino-landscape-store` was not reached in the 10-minute run.
- No flow asserts the "Get more books" text or that the Add pill is gone.
- The A3 cleanup has no device check: no Debug hook seeds an own book, so deleting an own book on a real install is untried.
- No iOS device run is recorded for part A.

### Slice 275: Leftovers from night 2026-11-06-0042, parts 1 to 3 ([slice](slices/275-night-2026-11-06-0042-leftovers.md))
- Item 7: no emulator flow cancels an audio pack download and checks that `cancelled` is sent (there is no test hook to stall a pack fetch); only unit and Worker tests cover it.
- Item 1 part 2: the new `store_failed`, `preference_unsaved` and `shelf_order_unreadable` rows were not seen on a device (no flow forces them); iOS and the Windows head were not run (shared code).
- Item 1 part 3: `layout_failed` and the new `FileStoryLibrary` order-file reports were not seen on a device (no flow forces them); iOS and the Windows head were not run (shared code); no test for a failed order-file write (OS-dependent).
- Item 4: the cause of the earlier `-NoBuild` cache-key miss is unknown; `-NoBuild` hit the cache for both apps on 2026-11-06 only.

### Slice 271: Leftovers from night 2026-11-05-1823 ([slice](slices/271-night-2026-11-05-1823-leftovers.md))
- A Windows VM check that plays a pre-made-audio book past two clip hand-offs (item 2, VM half): the VM package is a Release build with no Debug test hooks, so no fixture audio pack can be installed there; it needs Debug test hooks (or an install path) in the VM package first.

### Slice 274: Dino iPhone crash after leaving the grown-ups pages ([slice](slices/274-dino-ios-crash-after-leaving-parent-page.md))
- Android emulator: drag and release the reading-speed slider, and open the Language page; both behave as before (the builder moved the slider's Unloaded handler onto a wrapper and made `LanguagePage.WhenLoadedAsync` wait on RootPage; added by the feedback loop of 2026-11-06).
- The next morning, `pull-crashes.mjs` and TestFlight crash feedback show no new report for that build.
- The iOS cause is only likely (stack matches dotnet/maui PR #38662; the one symbolicated app frame is `_xamarin_release_managed_ref+0x64`). CI keeps no dSYM, so the stack cannot be symbolicated further.

### Slice 273: Every fallback sends an analytics event, parts A-E ([slice](slices/273-fallback-events-everywhere.md))
- After release, `analytics-query` shows `fallback` rows from production builds and none from tests or CI.
- A store book with a published pack that is not downloaded sends no `fallback` (part C).
- Part B, Windows rows not run on the VM: Infrastructure composite `neural_not_ready`/`neural_unavailable`/`stream_unavailable`, `engine_build_failed`, and the `UsageService.BeforeSend` flush.
- Part B, iPhone rows compiled only: `AvSpeechReader` `phone_language_missing`, `IosSupertonic` `engine_build_failed`, iOS ML Kit `translation_pack_failed`.
- Part B production rows: check with `analytics-query` after release.
- Part D, Windows rows not forced on the VM: `saved_position_moved`, `displays_unreadable`, `dpi_defaulted`, `window_frame_failed`, LanguageWindow `pack_failed`.
- Part D, iOS rows compiled only: `font_fallback`, `storyteller_pack_missing`, clip rows.
- Part D production rows: check with `analytics-query` after release.
- Part C, Windows rows not run on the VM: voice pack `pack_failed`, `WindowsOcrTextExtractor` `ocr_platform_fallback`.
- Part C, iPhone rows compiled only: scanner, OCR, config hold.
- Part C, `config_cached`/`config_default` and `ui_language` rows on a real first launch.
- Part C production rows: check with `analytics-query` after release.

### Slice 270: Dino iPhone crash, player disposed in its finished callback ([slice](slices/270-dino-ios-player-disposed-in-finished-callback.md))
- Windows clip hand-off: no VM check plays audio clips (a harness gap).
- `IDispatcher.Dispatch` always queues on Windows (seen only as a string match in Microsoft.Maui.dll 10.0.120, not confirmed from source).
- A parent's recording that ends while paused waits for Resume (`_recordingEndedWhilePaused` in `ReadingSession.cs`): the logic is unit-tested now (Slice 271, `ReadingPauseStateTests`); the device flow is still owed.
- Review check2 #4 (stale id's early return in `ApplyRecordingEnded` may leave the recording Playing with nothing playing) and #6 (iOS audio-session Deactivate only in the queued step on a stale play): both unconfirmed.
- Review check2 #1 kept because "origin/main had the same window" (Windows Pause then Resume in the hop replays the clip): premise not checked.

### Slice 269: Leftovers from night 2026-11-04-1441 ([slice](slices/269-feedback-loop-2026-11-04-1441-leftovers.md))
- Emulator cold-boot retry path of the preflight (not triggered in the run; kill the Maestro driver after a snapshot boot).
- `Test-LogMarker` regex: hand-checked and self-test only, not seen on a real flow with a `,` or `:` marker.
- Apple Vision per-language support for Farsi, Arabic, Russian, Japanese and Chinese (research only; call `supportedRecognitionLanguages` on the minimum iOS).
- The weekly pack sync skipping cancelled languages through the static lock in `ShelfAudioPacks`: unit-tested only.

### Slice 235: Dino on Windows, part C ([slice](slices/235-dino-windows-window.md))
- Down right after opening Settings does nothing until Tab (focus starts on the window's Back button).
- Reorder mode on Windows keeps phone padding (review high 6).
- Focus return on a virtualized container is a no-op.
- Hover lift and shadow are not built (no native property).
- Real mouse hover on a real PC.

### Slice 268: Dino: remove "Show original text" from grown-up mode ([slice](slices/268-dino-remove-show-original-text.md))
- The new flow `book-sheet-no-original.yaml` was not shown failing on the old build.
- A book saved with the old flag on, read on a device.
- Windows head.
- iOS runtime (CI compiles iOS).

### Slice 267: Dino bugs: books on the shelf keep the old voice; the first-run buttons overlap ([slice](slices/267-dino-old-voice-and-wizard-buttons.md))
- The user's phone check (no phone tonight), including the "check first on the user's phone" item (app version, `dino.books.lastupdatecheck`, whether `audio-packs/<book>/` exists): the cause is from code and the emulator reproduction, not from the phone.
- The upgrade-from-previous-release path (previous build, then install over it) is not scripted.
- The layout check at font 1.0 and in landscape on every wizard and first-run screen: only the Record step at font 1.3 has a flow. On an emulator without a microphone the Record step may hide Try it.
- The contact sheet of flow screenshots (`reviews/<slice>-shots/`) was not built.
- Bug E on main: the wait step has run (read 2026-11-05 by the feedback loop: the `fix/` merge #826, release run 41950767114, success; #799's run 41951758486 ran 01:55-03:47 UTC, about 112 min against a normal 62-68, and succeeded). Still unchecked: whether that run held behind the fix run on purpose (read its version-job log), and the lookup-failed path. Cost: a `fix/` merge no longer coalesces with later merges (one full release per fix merge, against the Actions quota rules in `.github/CLAUDE.md`).
- iOS runtime and the XCUITest twins (`net10.0-ios` compiles; the XCUITest layout check does not exist).
- The `read_voice` and failure-path log lines carry no messages or URLs: only the success lines were read.

### Slice 264: Dino: grown-ups reorder the shelf ([slice](slices/264-dino-gallery-reorder-mode.md))
- Esc after a drag on Windows. (The drag itself, the order saved and kept after a restart, and Cancel restoring the order passed on the VM on 2026-11-05 in Slice 269's `dino-reorder` check with real mouse moves; the pre-night run's "drag moved, order not saved" at 16:28 used the old untracked script, so it likely came from that script's synthetic moves, not checked.)
- A real scroll during a press cancelling the hold (the 4-book test shelf does not scroll).
- iOS gestures (`PointerGestureRecognizer` touch on iOS).
- Windows keyboard reorder (Space/arrows).
- The 6-book 1,2,5,3,4,6 example on a device (unit-tested only).

### Slice 262: Dino: adding a second language downloads the shelf books' audio in it ([slice](slices/262-dino-side-language-downloads-book-audio.md))
- iOS run of the shelf-audio card and runner (compiles in CI only).
- Kill-and-resume and offline mid-download on a device.
- The mobile-data rule ("Download now" only) on a real phone.
- The intro languages step path on a device.

### Slice 266: Waves: stop wasting the minutes between fix attempts ([slice](slices/266-wave-fix-attempt-speed.md))
- Item 5: a timed Windows fix attempt on a real slice (target under 6 minutes, against the measured 11.5-12.4).
- A VM run reusing a package a fixer built in a fix round.

### Slice 265: Dino: pre-made audio clips in Git LFS; book changes only publish to the store ([slice](slices/265-dino-audio-clips-in-git-lfs.md))

- `git lfs pull --include` of one clip from a fresh clone of main downloads the real file (only pointers were read back, 2026-11-04). Keep `s246-audio` until it does.
- `dino-store.yml`'s first real run on main.
- `packs.yml`'s first run with `lfs: false`.

### Slice 261: Dino: no starter books in the app; a first-run book picker ([slice](slices/261-dino-no-starters-first-run-book-picker.md))
- Upgrade from the current release APK keeps the four books (the release signature differs from Debug).
- iOS simulator run of the picker and first run.
- The one-off Mono SIGSEGV on first-books-cancel (not reproduced in two later runs with the crash buffer captured).
- The roughly 45 older flows were not all rerun (open-book, back-cover, premade-audio-plays, store-tags and grown-ups-button passed).
- The covers row on the download wait and the intro caption were not checked by eye.

### Slice 260: Dino: tags on store books (Hot, Popular, Recommended, New) ([slice](slices/260-dino-store-tags.md))
- Tablet store flow `dino-landscape-store-tablet` on the tablet AVD (it failed on the phone AVD).
- iOS run (compile only so far).
- Badge look at 130-200% system font size and the slim (sideways) store card with a tagged book (extra badge row vs title and cover centre).
- The 15 locale labels (`Store_Tag_*`) need a native read.
- Badge text after an app-language switch with the store page open (review high 3, not confirmed).

### Slice 259: Leftovers from night 2026-11-03-2349 ([slice](slices/259-night-2026-11-03-2349-leftovers.md))
- Item 1: the mid-book wait on a slow real phone (overlay when above 500 ms); the emulator measured 540 ms to first sound with no overlay.
- Item 1: why no overlay at 540 ms (about 40 ms over the 500 ms rule).
- Item 4: the WER copy on a real crash (the scripted kill left `events.txt` and the trace tail only).
- Item 4: `tools-selftests.yml`'s new crash-evidence step has not had its first CI run.
- Follow-up, not done: document the new `exit_code_for` exit rule in `tools/premade-audio/README.md`.
- Item 5: the keychain-lifetime cause is read from the log, not reproduced; confirmed only by the next signed Dino build passing after the `ios-signing.sh` change.

### Slice 258: Dino: pre-made audio for the four starter books ([slice](slices/258-dino-starter-books-premade-audio.md))
- iOS run (compile only so far).
- Windows head: the starter books' pre-made audio playing (launch, open book and page turn passed on the VM, 2026-11-05 16:28 pre-night run).

### Slice 250: Dino usage: playback events and a session id new on every cold start ([slice](slices/250-dino-playback-events-and-session-id.md))

- `seq` on a device (the Debug line has no `seq`; unit-tested only).
- The `usage-playback-events` flow shown failing on origin/main (needs a rebuild).
- iOS store build's first rows showing session ids and the new events.
- Live Worker rows after the deploy.

### Slice 249: Dino: "Report a problem" in the grown-ups' corner ([slice](slices/249-dino-report-a-problem.md))

- Known gap, to fix in a follow-up: with the remote crash switch off, a report the page called "kept and sent later" can be wiped at the next app start (`CrashReporter.UserReport.cs:135`).
- Sending a report from a TestFlight build and `pull-crashes.mjs --code <code>` on that real report.
- The iOS log lines (page, reading, memory warning) on a device.
- The rolling file on a device (the device run read the lines from logcat).
- RTST logs no Back line on Android (Shell has no pop hook); not tried further.

### Slice 256: Dino: `dump-sentences` works for picture books (Good Enough) ([slice](slices/256-good-enough-sentence-dump.md))

- The dump index matches the app's sentence index on a device during premade-audio playback (Slice 246 C).
- Item 3: Good Enough Farsi batch render (paid Gemini, needs `GEMINI_API_KEY`) not run; command in the slice file.

### Slice 255: Dino: real animal recordings instead of voiced animal sound words ([slice](slices/255-dino-real-animal-sounds.md))

- User listening check (2026-11-06, "I need to think about it"): a line whose own text already says the animal's sound word (e.g. "moo", `SaysIt`) is read by the voice with no clip, per the slice's recorded decision; the user's quote says animal noises should not be AI-voiced, so the user listens and confirms or changes it.
- Farsi clip on a device (the device run's `lang=fa` launch may not have been applied to the running app).
- Clip playback on iOS and on the Windows head (compiled only).
- A possible double sound when a quote already says the animal's word in a language without a sound table (check2 #1).
- Old premade dumps (Pigs, Wolf) predate the sound-entry skip and need regenerating before Slice 246 C uses them.
- A fully cached run with no `Playing` state would not advance (same as origin/main).

### Slice 257: Dino: a quote after "said Mouse." is the Mouse's, not the Lion's ([slice](slices/257-lion-and-mouse-speaker.md))

- Listen to the new Farsi clip in `D:/ReadTheStupidText-worktrees/s246-audio/books-lion-and-mouse/`.
- On a device, Lion and the Mouse reads the Mouse's line ("I didn't mean to wake you up. ...") in the Mouse's voice (no device run: pure Core logic, covered by the unit tests).

### Slice 252: Release CI: a hung signed iOS build can't hold the release queue ([slice](slices/252-release-ios-build-timeout-and-speed.md))

- Item 5 (forced short-timeout run) not run: a dispatched run shares the release concurrency group and a later merge would cancel it.
- The first `main` run's job summary (timing block) and its numbers: not yet seen or written into item 3.
- The upload retry on a real network blip: not seen; the step logs "Upload succeeded on attempt N" on success.
- shellcheck of the new step scripts runs only in CI.

### Slice 251: Dino usage from TestFlight goes to its own beta dataset ([slice](slices/251-dino-beta-usage-dataset.md))

- A TestFlight build of Dino sends rows that land in `dino_usage_beta` and none in `dino_usage` (needs a real TestFlight install; no device run was made).
- The Workers Builds deploy after the merge carries the `DINO_USAGE_BETA` binding.

### Slice 254: Dino: speaker first in Farsi and Arabic, and animal sounds next to the animal's own words ([slice](slices/254-dino-speaker-first-and-sound-placement.md))

- On a device: Three Little Pigs in Farsi reads «پیم گفت:» then the whole quote and «گرگ گفت:» before the wolf's words; English: the wolf's sound before "said Wolf", "Achoo!" in the wolf's voice.

### Slice 253: Final-loop leftovers, night 2026-11-01-2355 ([slice](slices/253-final-loop-leftovers-night-2026-11-01-2355.md))

- Freddy and the Slipper: the Arabic, Russian, Japanese and Chinese spellings of Elham's name, by a native speaker (until now only in `docs/plan/notes/2026-11-01-freddy-and-the-slipper.md`).
- The parallel illustration workflow (`dino-story-illustrator` modes setup / pages / finish, 3-4 at once, PR #687) has never been run: the first book drawn that way proves one lock, one stamp and matching place sheets.
- Item 1: a forced "System UI isn't responding" dialog on the tablet AVD with the dialog guard, a flow passing with no brief text (not run).
- Item 1: the dialog guard's restore after a lease loss (a killed run leaves `hide_error_dialogs=1`, cleared by the next `run-flows.ps1`; not exercised on a device).
- Item 6: before/after seconds per slow flow (command in the slice file's Built block, item 6; not run).

### Slice 248: Dino iOS: leaving a book stops its read, and a new book never drives the old one's voice ([slice](slices/248-dino-stop-read-on-leave-ios.md))

- iOS (TestFlight build after the merge): play a book, tap Back: the voice stops. Open another book: its button shows Play; Play reads that book. Swipe back from a playing book: the voice stops.

### Slice 247: Leftovers from the night's final feedback loop ([slice](slices/247-final-loop-leftovers-night-2026-11-01-0859.md))

- Mono SIGSEGV retry on a real tombstone: the fixture is copied from the note, and no SIGSEGV occurred in the 2026-11-02 runs.
- Cause of the `first-run` flow failure (assertCondition, 24.4 s) on the 2026-11-02 emulator run.
- Item a's change: the waits are not shortened yet, so "same 5 flows finish faster" is unproven.
- Item b: the S21 kokoro-en stall cause, until a real stall prints the PACKDL tail.

### Slice 235: Dino on Windows, parts A and B ([slice](slices/235-dino-windows-window.md))

- Part B on a real PC and by eye: whether `PreviewKeyDown` gets keys when nothing has focus yet (the check focuses the window first), the Space hold with real key repeats, title bar colours and height, tooltips on a hovering mouse.
- Why Right takes about 2 s after a read starts on the VM (speech start busy on the UI thread?), and whether a real PC has the same delay.
- Esc on the language page / cast picker through the new back path; held Esc on Settings/Language/Cast/Record pops exactly one page; the gate hold ending on Alt-Tab; title bar colour after the intro and the gate close.
- Typing the sum on AZERTY / AltGr layouts (on-screen pad and numpad only); Tab then Enter on "Back to story".
- The title bar back to cream after Esc from the book is shown by the check's pass but not read from the trace.
- Opening at 1100 x 760 on a screen larger than 1024 x 768 (the VM clamps it).
- A maximised window closed and reopened stays maximised (high finding 6).
- 150% scaling and the unit of `PreferredMinimumWidth/Height`.
- Two monitors, and an unplugged display (saved place on a missing monitor).
- Title bar height and colour by eye.
- A window clamped to a small screen and saved as maximised reopens maximised (seen on the VM; whether that is by design is not decided).

### Slice 234: Dino tablet held upright, parts A-C ([slice](slices/234-dino-tablet-portrait.md))

- Part C: Record, Cast with the picker, the editor (keyboard down and up), Store and Backup against frames 142-146 by eye (no screenshots; no script reaches those screens with a screenshot step).
- Part C: `RecordFixedRows` / `EditorFixedRows` on a real tablet.
- Part A (from the final feedback loop, 2026-11-02): after the store flow rotated back to upright, the shelf logged `layout=rail ... tabletUpright=False` (234A emulator run). Likely moot after part B's `TabletUpright` flag; check the shelf log line on an upright tablet after a rotate-back.
- Part C: Cast picker on a short window or with the keyboard.
- Part B: cards, corner and Settings against frames 136-141 by eye (the flows take no screenshots).
- Part B: dino-tablet-upright-shelf and dino-landscape-reading-tablet regression not rerun after part B.
- Part B: a drifting finger on the gate hold.
- Part B: a short tablet window (cards cap and scroll).
- Part A: stacked reading page against frames 134, 135 and 150 by eye (no screenshot taken).
- Part A: shelf spacing and header against frame 133.
- Part A: picture only, upright.
- iPad (compile only), parts A-C.
- TalkBack order, parts A-C.

### Slice 246: Dino store books read with pre-made audio ([slice](slices/246-dino-store-books-premade-audio.md))

- Every vendor request shape is unproven until the first keyed run (Gemini model id, ElevenLabs voice id placeholder, Narakeet voice-name casing, CAMB `fa-ir`).
- The piper-fa baseline render, on a PC that has the `piper-fa` pack and `sherpa_onnx` installed.
- `build-audio` was run on real folders only by the pack run; the zips were not opened.
- Part C: iOS rate and playback on a device; a real store download fetching live packs; the `premade-audio-plays` flow shown failing on the old build; Windows head playback; the mid-book re-translation against the 500 ms loading-state rule (not measured).
- Pigs Farsi: the two `say` lines (گُنده‌ی, «شالاپ، شالاپ، شالاپ!») were re-rendered in the full book on 2026-11-03 but nobody has listened to them in the book; the user heard only the spelling-page variants. The same holds for the two other lines with the same word re-rendered after #766 (Pim's tub line and the narrator's "big smile" line): four lines in all.

### Slice 242: Round 4 leftovers, night 2026-11-01-0859 ([slice](slices/242-round-4-leftovers-night-2026-11-01-0859.md))

- `run-flows.ps1 -Install -NoBuild` may still build (check pass, low finding 1: the explicit `-Install` path lacks the no-build exit).
- `dino-landscape-reading` does not assert the `[Landscape] reading controls=row` line; the row branch was read from logcat by hand.
- Item 6: the 34/26 dp phone insets (no log or tree value; screenshots only).
- Item 6: the tablet screenshot against frame 127, by eye only.
- Item 6: the phone flow does not assert the tablet title stays hidden.

### Slice 244: Dino iOS crashes ([slice](slices/244-dino-ios-crashes-2026-11-01.md))

- The iPhone 8 run (the user's): read Farsi Three Little Pigs, Back mid-sentence, re-read, background, 3 times.
- iOS simulator flow (no CI workflow runs one).
- No new NSDictionary or `0xdead10cc` crash for a week after release (`tools/crashes`).
- The window size on a real phone.
- The 3-window on a sub-3 GB device.
- The iOS twin of the Maestro flow.

### Slice 238: packs.yml defaults match the catalog ([slice](slices/238-packs-workflow-defaults-match-catalog.md))

- The check's first run in CI on the PR.
- What a lower version does on phones: Slice 220's record says a re-download is decided by `PackVersions.IsNewer` only, so a phone holding piper-nb 2 would most likely keep it and a fresh install would get 1; not checked on a device.

### Slice 236: phone runner gaps, night 2026-11-01-0859 ([slice](slices/236-phone-runner-gaps-night-2026-11-01-0859.md))

- Item 3 on a device: cancel a pack download, `reset-dev-app.ps1 -Level auto`, the pack starts again.
- Item 6 with no lease held: `preflight.ps1 -Fix` then `-Restore` back to 0.
- A mid-run `CHANGED:` stop with no record.
- The `dumpsys platform_compat` override-line wording (`Get-CompatOverrideLines` matching).
- The 30 s device-side restore cancel timeout warning ("did not finish in 30 s").

### Slice 245: sherpa-onnx one version ([slice](slices/245-sherpa-onnx-one-version.md))

- Dino `supertonic-3`, `piper-fa`, `piper-nb` and `melo` reads on a device; RTST Android Supertonic read.
- Scan/OCR (PaddleOCR) and translation with managed onnxruntime 1.28.0 against native 1.28.2 on Android, iOS and Windows.
- Windows VM reads: the VM did not start (host had 6.9 GB free, gate is 8 GB, because the emulator ran at the same time).
- iOS everything (needs the TestFlight build).
- The same Farsi sukun sentence sounding the same on Android and iOS.

### Slice 241: Faster, quieter device tests ([slice](slices/241-device-test-speed.md))

- Item 1, crash fail-fast: a flow list with a launch crash prints one FAIL with the crash lines and NOTRUN (naming the crash) for the rest in under one flow's time.
- Item 3, timing: a cache-miss run's total is build time, not build plus boot (the `Cache miss` line itself passed on 2026-11-01; the time was not recorded).
- `PACKDL progress` lines every ~5 s on a normal download (the runner shows only the first).
- The iOS stall watcher on a device.
- The `-Split` two-emulator path.

### Slice 240: Dino Windows CI setup cache ([slice](slices/240-dino-windows-ci-setup-cache.md))

- A cache hit on the next run after main saved its Dino Windows caches (22:32-22:35 on 2026-11-01, release run 41356358486); PR #708's pull-request run missed the cache before that.
- `gh cache delete` of a cache entry created on another ref may be refused (only warns; not run).
- A release run on `main` with the Store MSIX build pinned to SDK 10.0.402 (was floating `10.0.x`) packages both architectures and uploads them to the draft release (only PR runs have used the pin).

### Slice 239: Dino store book without an app build ([slice](slices/239-dino-store-book-without-app-build.md))

- A real story-only PR shows no PR checks and, after merge, runs only `dino-store.yml` (no `release.yml`).
- Delisting a deleted book on a real run.
- A translated title for a newly added store book on an installed app, with no app update.

### Slice 232 parts A-E: Dino landscape reading screen, shelf, Record, Cast, page text editor, intro, gate and first run ([slice](slices/232-dino-landscape-every-screen.md))

- Part E: screenshots against frames 121, 122, 123, 130 (the flows take none), including check 3 (gate keys uneven in tall rows) and check 4 (long language names in 3 columns).
- Part E: `first-run.yaml` on this build against main (the SIGSEGV; not proven unrelated).
- Part E: the gate sum step sideways (the hold is not scriptable).
- Part E: tablet intro, gate and first run.
- Part E: iPhone and iPad (iOS compile only).
- Part E: TalkBack order.
- Part D: screenshots against frames 117, 118, 124, 128 by eye.
- Part D: tablet Store with 3 columns, and tablet Backup.
- Part D: TalkBack order in two panes.
- Part D: the hosted pane's right edge inset on a notch phone.
- Part B: the same book stays in view after rotating a shelf with more than 4 books (the emulator shelf has 4, all on screen).
- Part B: tablet landscape and portrait (no tablet AVD); iPhone and iPad (iOS compile only).
- Part B: notch inset doubling with `SafeAreaEdges` (high 10).
- Part B: a 360 dp-tall phone, the card and Remove fit.
- Part B: rotating with the sheet open during a Remove confirmation.
- Part B: VoiceOver / TalkBack order in the rail.
- Part C: screenshots of each landscape state against frames 116, 119, 120, 129 (the flows take none; the user's eye).
- Part C: tablet frames (129) on the RTST_Tablet emulator (Slice 242 adds it).
- Part C: the editor thumbnail with a real keyboard on a scanned page (the flow reaches only a typed page).
- Part C: a tablet with the keyboard up counts as a phone (short side under 600) and shows the thumbnail.
- Part C: iOS compile and iPhone/iPad.
- Part C: skipped lows: Cast rotation stops a playing sample, layout jump on focus, 3:4 picture column.
- Tablet landscape (no tablet AVD): the controls row under the card and its left edge.
- iPhone and iPad landscape (iOS compile only).
- VoiceOver / TalkBack order in landscape.
- Picture-only chip placement against frame 114 (the user's eye).
- A window 600-700 dp on the short side choosing rail versus row.
- Windows head in a wide window.
- Portrait reading unchanged on iOS, Windows and tablets after `TransportRow`'s base class changed to `StackLayout` (shown only by two Android emulator flows).
- Phone side margins (about 14 dp, mock 34/26) and tablet frame 127's title against the mock (the user's eye).

### Slice 225: Device-test tooling leftovers, night 2026-10-31-1301 ([slice](slices/225-device-tooling-leftovers-night-2026-10-31-1301.md))

- Paused-notification checks on the S21 (needs `run-checks.ps1 -Build -App rtst` first; cache miss).
- `stay_on` restore when the user changed the value (self-tested only).
- Heal `trap` and `kill -0` under Android sh.
- `Clear-PageCache` on a rooted device.
- `uiautomator dump /dev/tty` returned no XML on the emulator (2026-11-01): check whether run-flows' ANR dump is affected.
- Item 16 in the Debug app: the `[BookUpdate] ... forced entry dropped, recorded version put back` and `forced-apply mark expired` lines.
- Item 3 (run-checks emulator path) not built.
- Item 6: failed-build cause unknown (the log is now kept as `.failed.log`).
- Item 21: a flow reading a TalkBack focus event (TalkBack on and off passed; the focus event was not read).

### Slice 230: Dino: say plainly that recordings stay on the phone, and restore brings them back ([slice](slices/230-dino-recordings-copy-and-restore-merge.md))

- Hearing the restored recording play ("Read with my voice").
- iOS simulator or TestFlight: back up, restore, recordings come back.
- VoiceOver on the new Record/Backup lines.
- iPhone and iPad layouts of the new lines.
- Windows head wording ("this computer").
- The info icon (not drawn).
- The failing-once check against the old APK (no old APK available), so the flow is not shown to fail before the change.
- Native-speaker check of the new strings in the 14 machine-drafted locales (Farsi and Arabic right to left).

### Slice 221: Device-test tooling leftovers from the 2026-10-30-2100 night, round 1 ([slice](slices/221-device-test-tooling-leftovers-night-1.md))

- Item 6 on the real phone: two timed runs with the same owner, the second exits 4.
- Item 4 on GitHub: a force_all dispatch followed by a merge still runs its jobs; the wait step's `jq`/`gh` output on a real run.
- `run-flows.ps1` exits 137 after printing PASS (not investigated).
- Item 3 seen in a device-test run log.
- `adb uninstall` stderr under Windows PowerShell 5.1 with `ErrorActionPreference` Stop (`Remove-StaleDevPackages`, `tools/emulator/common.ps1:151`) could abort the install instead of warning.

### Slice 229: Dino Done/Continue button on every grown-up page ([slice](slices/229-dino-subpage-done-button.md))

- Page editor header over the crop stage (44 dp pill vs 38).
- Intro-wizard Continue flow (no hook to start the intro at that step).
- Farsi/Arabic: Done stays top right.
- Windows head not built.
- iOS VoiceOver, iPhone and iPad hand checks.

### Slice 224: Phone runner build-cache gaps ([slice](slices/224-phone-runner-build-cache-gaps.md))

- A real phone run of the runner with a dead-builder status file, and with `-NoBuild` on a warm cache (no device run this wave; host tooling only).

### Slice 237: Dino back cover (credits page) ([slice](slices/237-dino-back-cover-credits.md))

- Landscape layout of the back cover.
- Farsi/Arabic right-to-left back cover and Persian digits.
- Picture-only and text-only reading modes on a device.
- Old installed app reading the page aloud without breaking.
- iOS and Windows on a device (compile only).
- The ar/ru/ja/zh labels by a native speaker.
- Credit-line wrapping in long languages on a narrow phone (estimated, not measured).

### Slice 233: paused notice same on Android and iOS ([slice](slices/233-paused-notice-same-on-android-and-ios.md))

- iOS on a device: the VoiceOver, TestFlight, iPhone and iPad hand checks (the user's answer: they do these later). Only the iOS compile was checked.
- The silent re-post on a repeated failed automatic resume: no scripted check asserts the alert/silent difference.
- A real network loss: the device run used the Debug-only `drop-pack-network` hook, not a real loss.
- The medium-pass skip reasons (slice line 46) state code facts not read in the code at triage, e.g. "Android's Notify reports no failure": check each against the code.

### Slice 228: Dino grown-ups' corner clarity ([slice](slices/228-dino-grownups-corner-clarity.md))

- Windows head and VM: shared UI, not run.
- iOS: compile only.
- Corner tiles and title truncation on a small phone and on a tablet.
- Read step with no starter book.
- The translated strings are machine drafts.
- The intro read's return being cancelled by Play/pause, skip or a page turn (the emulator recheck tapped the page surface, which is not one of those).
- The 48 dp close button on a device (changed after the last emulator run; check pass only).
- Check-pass skip reasons 2, 3 and 7 (slice line 103) state code facts not read at triage (no modal over the reader; Dismiss refresh before `_mode.Leave`; Idle means no download): check each in the code.
- The skip reason "the app reaches users days later through the stores" (Worker before app) was not checked against the release workflow: Dino merges reach internal tracks the same day, so `intro_step` events may be dropped for minutes after a merge.

### Slice 220: Kokoro silent short lines on arm64 ([slice](slices/220-kokoro-silent-short-lines-arm64.md))

- iOS: compile only.
- The Windows Dino head on kokoro-en 3.
- The 68-read Slice 210 set.
- A fresh install's rebuilt non-kokoro packs (rebuilt in packs-11 at the same versions, no device run).

### Slice 231: Dino Record page shows the page's words ([slice](slices/231-dino-record-page-shows-words.md))

- origin/main showing the picture only (the cause was read from code, not shown on a device).
- A scanned book showing its lines on the Record page.
- An RTL/Farsi book on the Record page.
- iPhone, iPad, VoiceOver and TestFlight hand checks (the user's answer: they do these later) ([slice](slices/231-dino-record-page-shows-words.md)).

### Slice 226: Dino Settings rows and page text ([slice](slices/226-dino-settings-rows-review-leftovers.md))

- TalkBack / VoiceOver hearing each reading page once.
- iPhone / iPad / VoiceOver / TestFlight hand checks (the user's answer: they do these later).
- A Switch-end row still toggling (no current caller) ([slice](slices/226-dino-settings-rows-review-leftovers.md)).
- iOS: a settings row with the button text left null keeps its height and look, and `settings-reading-speed-row.yaml` on iOS needs an in-app back step instead of `- back` ([slice](slices/226-dino-settings-rows-review-leftovers.md)).
- The row-button width fix on Android: re-read the Settings row button bounds in a `uiautomator` dump (seen only on the Windows VM), and confirm the `Grid.Add` span cause its comment states ([slice](slices/226-dino-settings-rows-review-leftovers.md)).

### Slice 223: usage version rule ([slice](slices/223-usage-version-rule-cleanup.md))

- Live: a read-only `rtst_usage` / `dino_usage` query shows event counts per real version unchanged after the 2026-11-01 Worker deploy, no real version newly counted as unstamped ([slice](slices/223-usage-version-rule-cleanup.md)).

### Slice 222: Microsoft Store marker hardening ([slice](slices/222-store-marker-hardening.md))

- The marker guards' behaviour on the next real store-submit run (the first on main after this merges) ([slice](slices/222-store-marker-hardening.md)).

### Slice 227: crash reports ([slice](slices/227-crash-reports.md))

- Slice 227 not yet verified: iOS on a real TestFlight build (report tagged `testflight`, receipt name), MetricKit `native` report 24 h later, `crash-test kind=kill` gives one `unclean_exit`, Android exit reasons, an R2 object and `app_crashes` row once on, `dino_usage` TestFlight leak (27 rows stored), `pull-crashes.mjs`/`pull-apple.py`/`pull-play.py` against the real services ([slice](slices/227-crash-reports.md)).
- Slice 227: reports come only from rtst-v1.80.0 / dino-v1.85.0 onward; RTST's Microsoft Store submission rtst-store-v1.81.0 (2026-11-01) carries the code once certified; which version each store serves today could not be read from the public pages.

### Slice 219: usage drops dev versions ([slice](slices/219-usage-drops-dev-versions.md))

- Slice 219 (PR #632): after deploy, a read-only `rtst_usage` / `dino_usage` query shows no new `0.x` rows; a local Windows run adds no new row ([slice](slices/219-usage-drops-dev-versions.md)).

### Slice 217: rtst forced update ([slice](slices/217-rtst-forced-update.md))

- Slice 217 (PR #640): Windows forced and soft update on the VM (`rtst-update-forced` / `rtst-update-soft`); store button tap on the phone; iOS compile in CI and TestFlight hand check ([slice](slices/217-rtst-forced-update.md)).

### Slice 216: phone tooling leftovers ([slice](slices/216-phone-tooling-leftovers.md))

- Slice 216 (PR #637): item 11 cold-start rerun and the numbers in Slices 185 and 214; path-length estimates unmeasured; `Get-PhoneStamp` not device-run ([slice](slices/216-phone-tooling-leftovers.md)).

### Slice 215: device guard gaps ([slice](slices/215-device-guard-gaps.md))

- Slice 215 (PR #639): site image retake (VM sample read never leaves GeneratingAudio with the copied pack; `site/img/control-panel.webp` not replaced; watermark crop); `sentence-highlight` first-run failure after a fresh install; `test-activity.ps1 -DeviceKillCheck` script path not run ([slice](slices/215-device-guard-gaps.md)).

### Slice 214: rtst phone cold first audio ([slice](slices/214-rtst-phone-cold-first-audio.md))

- Slice 214 (PR #647): piece 2 timing against piece 1 at 1x and 2x (run `tools/phone/audio-timing.ps1 -App rtst -Aot` on the S21; it flags gaps); a truly cold run after a fresh install or reboot; iOS TestFlight first-audio timing by hand ([slice](slices/214-rtst-phone-cold-first-audio.md)).

- Dino: the first-run hello line (and the voice preview on a reader without segment support) now gets the short first piece; check on a phone that it still sounds whole ([slice](slices/214-rtst-phone-cold-first-audio.md)).

### Slice 213: phone check runner ([slice](slices/213-phone-check-runner.md))

- Slice 213: the `notification-text` check has never been seen failing (not run with a broken pack).
- Slice 213 (PR #610): a cold-cache run where the AOT build fails or stalls (the FAIL path); a run while another session holds the lease; the ... [full text](notes/2026-10-30-outstanding-detail.md#o017) ([slice](slices/213-phone-check-runner.md))

### Slice 211: play before detection waits for it ([slice](slices/211-play-before-detection-waits-for-it.md))

- Slice 211 (PR #633): S21 rerun of the Slice 139 race (tap ~0.4 s after typing, against the 1456 ms baseline); iOS TestFlight hand check; back-during-wait scripted check not run; `translation-wait-cancel` not run; Debug iOS compile of the final code (incl. the hold-detection hook) not done ([slice](slices/211-play-before-detection-waits-for-it.md)).

### Slice 210: kokoro short line dropped ([slice](slices/210-kokoro-short-line-dropped.md))

- Slice 210 (PR #631): listening to the rescued lines (the user's ear); Spanish, Portuguese and Chinese Kokoro voices (`ef_dora`, `pf_dora` have no sibling); iOS compile only ([slice](slices/210-kokoro-short-line-dropped.md)).

### Slice 209: settings and book update test hooks ([slice](slices/209-settings-and-book-update-test-hooks.md))

- Slice 209 (PR #644): the iOS URL-scheme hooks on a device (compile-checked only) ([slice](slices/209-settings-and-book-update-test-hooks.md)).

### Slice 208: paused pack downloads tell you and finish ([slice](slices/208-paused-pack-downloads-tell-you-and-finish.md))

- Slice 208 (PR #646) not yet verified: the S21 Verification list (paused notice within ~35 s and tap resumes; notification text; stale partial resumes or is deleted), runnable now that the phone is back; Dino storyteller first download shows no ask sheet and grown-up Settings shows it once (emulator); iOS TestFlight hand check; stale-partial-launch first cold run marker not seen within 40 s (not diagnosed); the `paused-notification` check itself was reworked by Slice 233 (Debug hook `drop-pack-network`, no netpolicy) and passed on the S21 on 2026-11-01, so its earlier netpolicy and detached-restore doubts no longer apply. [208-paused-pack-downloads-tell-you-and-finish.md](slices/208-paused-pack-downloads-tell-you-and-finish.md)

### Slice 206: phone run scripts ([slice](slices/206-phone-run-scripts.md))

- Slice 206 (PR #594): `preflight.ps1` reading a PIN or pattern lock as NO-GO; `reset-dev-app.ps1 -Level reinstall`; Dino resets (`-App dino`); ... [full text](notes/2026-10-30-outstanding-detail.md#o001) ([slice](slices/206-phone-run-scripts.md))

### Slice 205: dino windows reading speed and page turn checks ([slice](slices/205-dino-windows-reading-speed-and-page-turn-checks.md))

- Slice 205 (PR #645): Android Settings row width vs screen (the uiautomator dump came back empty after a permission dialog; no Maestro flow targets the Reading speed row); the row opening with a tap and with TalkBack / VoiceOver on Android and iOS was never run ([slice](slices/205-dino-windows-reading-speed-and-page-turn-checks.md)).

### Slice 204: cancelled voice pack stays cancelled ([slice](slices/204-cancelled-voice-pack-stays-cancelled.md))

- Slice 204 (PR #588): Dino on device; iOS TestFlight; native read of the Stopped strings (machine drafts)([slice](slices/204-cancelled-voice-pack-stays-cancelled.md)).

### Slice 202: reader language change crash ([slice](slices/202-reader-language-change-crash.md))

- Slice 202: iOS device not run (same shared MAUI page fix) ([slice](slices/202-reader-language-change-crash.md)).

### Slice 201: norwegian pack crash ([slice](slices/201-norwegian-pack-crash.md))

- Slice 201 (PR #571): Windows desktop read with the new Norwegian pack, iOS device, and the Maestro flow `.maestro/rtst/norsk-pack-reads.yaml` never run ([slice](slices/201-norwegian-pack-crash.md)).

### Slice 197: dino download now tap dropped ([slice](slices/197-dino-download-now-tap-dropped.md))

- Slice 197 (PR #583): no stale "Downloading" status after the download ends; notification Cancel mid-download then nothing restarts; iOS TestFlight ... [full text](notes/2026-10-30-outstanding-detail.md#o016) ([slice](slices/197-dino-download-now-tap-dropped.md))

### Slice 196: dino default view per book ([slice](slices/196-dino-default-view-per-book.md))

- Slice 196 (PR #575): the flow `default-view-per-book.yaml` not yet shown failing on old main; Good Enough pages 8 and 10 by eye; landscape layout; ... [full text](notes/2026-10-30-outstanding-detail.md#o008) ([slice](slices/196-dino-default-view-per-book.md))

### Slice 195: norwegian default voice ([slice](slices/195-norwegian-default-voice.md))

- Slice 195 (PR #592): hearing the picks (default `nb-knv` is measured, not heard); iOS TestFlight run of the new order; Windows head; Dino narrator on a device ([slice](slices/195-norwegian-default-voice.md)).

### Slice 192: dino store credits in every language ([slice](slices/192-dino-store-credits-in-every-language.md))

- Slice 192 (PR #582): live `meta.json` refreshed (all 15 books in `/api/stories` carry `creditsByLanguage`, checked 2026-10-30); ... [full text](notes/2026-10-30-outstanding-detail.md#o012) ([slice](slices/192-dino-store-credits-in-every-language.md))

### Slice 191: dino count restored and imported books ([slice](slices/191-dino-count-restored-and-imported-books.md))

- Slice 191 (PR #581): emulator run inconclusive (no `own_book_added` line seen at add or restore; whether the own book came back not seen; another ... [full text](notes/2026-10-30-outstanding-detail.md#o011) ([slice](slices/191-dino-count-restored-and-imported-books.md))

### Slice 190: rtst phone sentence highlight ([slice](slices/190-rtst-phone-sentence-highlight.md))

- Slice 190 (PR #573): on-screen sentence highlight PASS on the S21 (2026-10-30); pause/resume mid-chunk; iOS TestFlight read of a 3-sentence ... [full text](notes/2026-10-30-outstanding-detail.md#o007) ([slice](slices/190-rtst-phone-sentence-highlight.md))

### Slice 189: dino grown ups button and book sheet ([slice](slices/189-dino-grown-ups-button-and-book-sheet.md))

- Slice 189 (PR #560): own-book sheet ("Your book", Edit pages); a switch flipped just before Remove or Edit pages; Settings sub-page -> Settings ... [full text](notes/2026-10-30-outstanding-detail.md#o076) ([slice](slices/189-dino-grown-ups-button-and-book-sheet.md))

### Slice 188: dino settings vm check ([slice](slices/188-dino-settings-vm-check.md))

- Slice 188 (PR #585): reading speed row navigation on the Windows head by UIA (a Grid with a tap gesture; neither a label click nor a pane-centre ... [full text](notes/2026-10-30-outstanding-detail.md#o010) ([slice](slices/188-dino-settings-vm-check.md))

### Slice 187: translation download waits for internet ([slice](slices/187-translation-download-waits-for-internet.md))

- Slice 187 (PR #576): the waiting look by eye (title, grey bar); Dino's translation wait on a device; other wait overlays after the `Center` -> ... [full text](notes/2026-10-30-outstanding-detail.md#o009) ([slice](slices/187-translation-download-waits-for-internet.md))

### Slice 186: no ocr download after voice cancel ([slice](slices/186-no-ocr-download-after-voice-cancel.md))

- Slice 186 (PR #574): iOS TestFlight: the notification Cancel holds the scan pack ([slice](slices/186-no-ocr-download-after-voice-cancel.md)).

### Slice 185: close three old gaps ([slice](slices/185-close-three-old-gaps.md))

- Slice 185 (PR #545): AOT cold start measured on a phone (first audio 624 ms, but taken with `audio-timing.ps1`, which labels warm-cache repeats as ... [full text](notes/2026-10-30-outstanding-detail.md#o020) ([slice](slices/185-close-three-old-gaps.md))

### Slice 184: windows english to japanese pack ([slice](slices/184-windows-english-to-japanese-pack.md))

- Slice 184: a real English-to-Japanese translation in the Windows app (download the pack, translate in the control panel); the C# tokenizer path has ... [full text](notes/2026-10-30-outstanding-detail.md#o071) ([slice](slices/184-windows-english-to-japanese-pack.md))

### Slice 183: pack downloads keep going when locked ([slice](slices/183-pack-downloads-keep-going-when-locked.md))

- Slice 183 (PR #549): iOS background session on a device (TestFlight); Dino storyteller first-download ask deferral; the dataSync timeout's silent ... [full text](notes/2026-10-30-outstanding-detail.md#o018) ([slice](slices/183-pack-downloads-keep-going-when-locked.md))

### Slice 182: pack download resumes by itself ([slice](slices/182-pack-download-resumes-by-itself.md))

- Slice 182 (PR #544): OCR resume after the final fix round; "Waiting for Wi-Fi" on a real cellular-only phone; a real phone and the iOS runtime; ... [full text](notes/2026-10-30-outstanding-detail.md#o006) ([slice](slices/182-pack-download-resumes-by-itself.md))

### Slice 181: dino books update from store ([slice](slices/181-dino-books-update-from-store.md))

- Slice 181 (PR #543): a recording carried over a swap; a store-downloaded book with edited text getting an update; a crash during the apply itself ... [full text](notes/2026-10-30-outstanding-detail.md#o005) ([slice](slices/181-dino-books-update-from-store.md))

### Slice 180: faster device tests ([slice](slices/180-faster-device-tests.md))

- Slice 180 (PR #536, #538): the System UI "isn't responding" dismissal in `run-flows.ps1` never triggered (read only); one first-run flow flake with ... [full text](notes/2026-10-30-outstanding-detail.md#o022) ([slice](slices/180-faster-device-tests.md))

### Slice 179: no robot or own machine counts ([slice](slices/179-no-robot-or-own-machine-counts.md))

- Slice 179 (PR #542): Windows VM run with a CI MSIX; iOS `sandboxReceipt` on TestFlight; Windows Store-signature check on a Store build; Release ... [full text](notes/2026-10-30-outstanding-detail.md#o019) ([slice](slices/179-no-robot-or-own-machine-counts.md))

### Slice 178: dino own book usage detail ([slice](slices/178-dino-own-book-usage-detail.md))

- Slice 178 (PR #547): pdf, docx, photo and scan kinds on a device; iOS and Windows runs; `tools/dino-usage-report.ps1` against the live API after ... [full text](notes/2026-10-30-outstanding-detail.md#o004) ([slice](slices/178-dino-own-book-usage-detail.md))

### Slice 177: dino cold start synthesis ([slice](slices/177-dino-cold-start-synthesis.md))

- Slice 177 (PR #522): iOS timing (TestFlight); a first read in a non-default voice folder (e.g. Farsi multi-folder pack) still starts cold; a Farsi ... [full text](notes/2026-10-30-outstanding-detail.md#o023) ([slice](slices/177-dino-cold-start-synthesis.md))

### Slice 176: whitespace read crash ([slice](slices/176-whitespace-read-crash.md))

- Slice 176 (PR #518): an iOS run (the whitespace read passes on the phone: blank text returns before the engine) ([slice](slices/176-whitespace-read-crash.md)).

### Slice 175: test gaps permanent checks ([slice](slices/175-test-gaps-permanent-checks.md))

- Slice 175 (PR #517): iOS hooks on a device (no Mac); no unit tests for `LaunchKeys`, `BookMatch` or the timing parser (Slice 199). The VM items ... [full text](notes/2026-10-30-outstanding-detail.md#o077) ([slice](slices/175-test-gaps-permanent-checks.md))

### Slice 174: dino reading screen and back button ([slice](slices/174-dino-reading-screen-and-back-button.md))

- Slice 174 (PR #521): shelf tile title after Show original text on the emulator (fix made after the run); iOS run (rotation, safe areas, ... [full text](notes/2026-10-30-outstanding-detail.md#o075) ([slice](slices/174-dino-reading-screen-and-back-button.md))

### Slice 173: dino kid story languages ([slice](slices/173-dino-kid-story-languages.md))

- Slice 173 (PR #525): mobile-data "Wait for Wi-Fi / Download now" prompt not seen (emulator has Wi-Fi); a persisted no-pack language ... [full text](notes/2026-10-30-outstanding-detail.md#o021) ([slice](slices/173-dino-kid-story-languages.md))

### Slice 172: dino settings gear ([slice](slices/172-dino-settings-gear.md))

- Slice 172 (PR #519): narrator sample Cancel overlay (sample returned under 500 ms on the emulator); cast narrator language on a device; iOS run (no ... [full text](notes/2026-10-30-outstanding-detail.md#o074) ([slice](slices/172-dino-settings-gear.md))

### Slice 171: dino text card shows whole page ([slice](slices/171-dino-text-card-shows-whole-page.md))

- Slice 171 (PR #506): iOS line reader (`IosTextLineSource`, TextKit rebuilt to match UILabel) and the card's scroll on a real iPhone: Latin, Farsi ... [full text](notes/2026-10-30-outstanding-detail.md#o033) ([slice](slices/171-dino-text-card-shows-whole-page.md))

### Slice 170: tray panel controls row fits ([slice](slices/170-tray-panel-controls-row-fits.md))

- Slice 170 (PR #505): device check at 100% and 150% scale, idle and reading, English and Finnish (all five toggles and the wait flyout fully ... [full text](notes/2026-10-30-outstanding-detail.md#o032) ([slice](slices/170-tray-panel-controls-row-fits.md))

### Slice 169: german dino books ([slice](slices/169-german-dino-books.md))

- Slice 169 (PR #503): German sound words and pages heard aloud on a phone; a German book on iOS (TestFlight); end-of-batch check that /api/stories ... [full text](notes/2026-10-30-outstanding-detail.md#o031) ([slice](slices/169-german-dino-books.md))

### Slice 168: german in both apps ([slice](slices/168-german-in-both-apps.md))

- Slice 168 (PR #495): German heard on iOS (TestFlight) and Windows (waits on packs-8); Android German read done by log (1795 ms, [S21 night ... [full text](notes/2026-10-30-outstanding-detail.md#o030) ([slice](slices/168-german-in-both-apps.md))

### Slice 167: no test device counts ([slice](slices/167-no-test-device-counts.md))

- Slice 167 (PR #497): Release build on an emulator (crashed at startup in MAUI before any usage code, x64 only); emulator and Test Lab rules on a ... [full text](notes/2026-10-30-outstanding-detail.md#o029) ([slice](slices/167-no-test-device-counts.md))

### Slice 166: usage device context ([slice](slices/166-usage-device-context.md))

- Slice 166 (PR #492): Windows heads only compiled (no desktop run of the RTST tray or Dino Windows head); iOS compiled only (no simulator); first ... [full text](notes/2026-10-30-outstanding-detail.md#o028) ([slice](slices/166-usage-device-context.md))

### Slice 165: real phone audio timing checks ([slice](slices/165-real-phone-audio-timing-checks.md))

- Slice 165: the user's listening check on the phone (Slice 163 and the Slice 175/176 gaps are closed) ([slice](slices/165-real-phone-audio-timing-checks.md))

### Slice 162: scripted device flows ([slice](slices/162-scripted-device-flows.md))

- Slice 162 (PR #490): iOS hooks and the `rtst-test://` / `dino-test://` URL scheme have never run (no Mac), compile-checked only; Dino ... [full text](notes/2026-10-30-outstanding-detail.md#o026) ([slice](slices/162-scripted-device-flows.md))

### Slice 161: warm headless x64 emulator ([slice](slices/161-warm-headless-x64-emulator.md))

- Slice 161 (PR #485): arm64-vs-x64 launch-and-read timing not compared; version-mismatch guard, bump guard and concurrent-build paths reviewed only, ... [full text](notes/2026-10-30-outstanding-detail.md#o025) ([slice](slices/161-warm-headless-x64-emulator.md))

### Slice 160: ios translation parity ([slice](slices/160-ios-translation-parity.md))

- Slice 160 (PR #486): TestFlight translate English to Farsi on iOS, model download progress and Cancel, offline translate after download, Dino typed ... [full text](notes/2026-10-30-outstanding-detail.md#o024) ([slice](slices/160-ios-translation-parity.md))

### Slice 159: dino everything in my language ([slice](slices/159-dino-everything-in-my-language.md))

- Slice 159: native-speaker review of `ShippedTitles` and the 7 new resx keys in 13 languages; store credit line still English (Slice 192); live ... [full text](notes/2026-10-30-outstanding-detail.md#o047) ([slice](slices/159-dino-everything-in-my-language.md))

### Slice 156: dino authored character voices ([slice](slices/156-dino-authored-character-voices.md))

- Slice 156: device listen of each book's cast in en/fa/es/pt/zh; fa/es/pt roster limits (few voices per gender, narrator voice shared; see each art ... [full text](notes/2026-10-30-outstanding-detail.md#o044) ([slice](slices/156-dino-authored-character-voices.md))

### Slice 155: dino first run intro wizard ([slice](slices/155-dino-first-run-intro-wizard.md))

- Slice 155: offline store step; microphone prompt on the Record step; Windows variant (title, Picture badge); iOS modal presentation; large-font ... [full text](notes/2026-10-30-outstanding-detail.md#o073) ([slice](slices/155-dino-first-run-intro-wizard.md))

### Slice 152: dino animal from the book ([slice](slices/152-dino-animal-from-the-book.md))

- Slice 152: Billy Goats store book on device; hearing a recast animal by ear; Windows head and iOS; native read of the `Settings_AnimalSounds_*` ... [full text](notes/2026-10-30-outstanding-detail.md#o043) ([slice](slices/152-dino-animal-from-the-book.md))

### Slice 151: dino three view modes ([slice](slices/151-dino-three-view-modes.md))

- Slice 151: photo import opening in Picture only (needs a camera); Windows head and iOS; native read of the new strings (13 drafts); night-theme segment colours unchanged ([slice](slices/151-dino-three-view-modes.md)).

### Slice 150: dino slower reading speed ([slice](slices/150-dino-slower-reading-speed.md))

- Slice 150: a mid-book speed change heard on a real phone; Windows head and iOS readers apply `SetSpeed` on the next sentence; native-speaker read ... [full text](notes/2026-10-30-outstanding-detail.md#o072) ([slice](slices/150-dino-slower-reading-speed.md))

### Slice 149: never drop a short word ([slice](slices/149-never-drop-a-short-word.md))

- Slice 149: emulator Farsi read passed by logs only, not heard; iOS compile of `IosSupertonicSpeechReader.Synthesis.cs` and `SiblingRescue`; a ... [full text](notes/2026-10-30-outstanding-detail.md#o039) ([slice](slices/149-never-drop-a-short-word.md))

### Slice 148: phone wait followups ([slice](slices/148-phone-wait-followups.md))

- Slice 148: emulator PASS 2026-10-26 (timings not representative). Not done: Windows stall overlay on a packaged run; native read of `Wait_Stalled` ... [full text](notes/2026-10-30-outstanding-detail.md#o041) ([slice](slices/148-phone-wait-followups.md))

### Slice 146: dino books kid friendly language ([slice](slices/146-dino-books-kid-friendly-language.md))

- Slice 146: native read of all 13 translations per book; a listen to the new sound-word triples in ar, da, fi, fr, ja, nb, nl, pt, ru, sv; upload of ... [full text](notes/2026-10-30-outstanding-detail.md#o042) ([slice](slices/146-dino-books-kid-friendly-language.md))

### Slice 145: sound words aloud test ([slice](slices/145-sound-words-aloud-test.md))

- Slice 145: a human listen of the `sound-words-listen` set (Farsi and Chinese especially; Farsi whoosh and cat picks provisional); German, French and Portuguese untested ([slice](slices/145-sound-words-aloud-test.md)).

### Slice 144: farsi default female voice ([slice](slices/144-farsi-default-female-voice.md))

- Slice 144: listen to every moved Farsi character and the five new flavours (distinct enough?); Farsi read with `mana` narrating on emulator/phone; ... [full text](notes/2026-10-30-outstanding-detail.md#o037) ([slice](slices/144-farsi-default-female-voice.md))

### Slice 143: shared wait overlay dino loading ([slice](slices/143-shared-wait-overlay-dino-loading.md))

- Slice 143: not exercised on emulator: photos, scan, own-book translate; native read of the new strings (14 locales, Claude drafts); Windows head ... [full text](notes/2026-10-30-outstanding-detail.md#o036) ([slice](slices/143-shared-wait-overlay-dino-loading.md))

### Slice 142: rtst windows early start ([slice](slices/142-rtst-windows-early-start.md))

- Slice 142: packaged desktop run done 2026-10-26 on a CPU-saturated host; re-check first-audio timing on a quiet machine; prompt tray exit not verified ([slice](slices/142-rtst-windows-early-start.md)).

### Slice 141: rtst windows wait indicator ([slice](slices/141-rtst-windows-wait-indicator.md))

- Slice 141: the panel ring (Play becomes the ring with Cancel) re-check on the desktop; native-speaker read of the 5 `Wait_` strings in 14 locales; ... [full text](notes/2026-10-30-outstanding-detail.md#o038) ([slice](slices/141-rtst-windows-wait-indicator.md))

### Slice 140: rtst windows usage counts ([slice](slices/140-rtst-windows-usage-counts.md))

- Slice 140: queue file under `LocalFolder/usage` not exercised (needs `telemetry.enabled` on); no Windows rows in `rtst_usage` yet (telemetry on since 2026-10-27) ([slice](slices/140-rtst-windows-usage-counts.md)).

### Slice 139: rtst phone early start ([slice](slices/139-rtst-phone-early-start.md))

- Slice 139: emulator 2026-10-26 (swapping AVD): early work finished gives first audio in 396-749 ms with a cache hit on sentence 1, but in-flight ... [full text](notes/2026-10-30-outstanding-detail.md#o040) ([slice](slices/139-rtst-phone-early-start.md))

### Slice 138: rtst phone wait indicator ([slice](slices/138-rtst-phone-wait-indicator.md))

- Slice 138: emulator PASS 2026-10-26; sub-500 ms play start PASS on the S21 (61-140 ms); not seen: an ML Kit progress % (Slice 139 downloads the ... [full text](notes/2026-10-30-outstanding-detail.md#o035) ([slice](slices/138-rtst-phone-wait-indicator.md))

### Slice 137: rtst phone usage counts ([slice](slices/137-rtst-phone-usage-counts.md))

- Slice 137: telemetry on since 2026-10-27 and rows arrive in `rtst_usage` (so far test devices only); iOS compile; emulator pass done 2026-10-26 ... [full text](notes/2026-10-30-outstanding-detail.md#o034) ([slice](slices/137-rtst-phone-usage-counts.md))

### Slice 136: dino animal from character ([slice](slices/136-dino-animal-from-character.md))

- Slice 136: emulator: cast page PASS in English and Farsi (matches frame 59, no Animal line); sounds (oink, Wolf growls) and recasting Pip to a ... [full text](notes/2026-10-30-outstanding-detail.md#o069) ([slice](slices/136-dino-animal-from-character.md))

### Slice 135: dino animal voices fixes ([slice](slices/135-dino-animal-voices-fixes.md))

- Slice 135 (and the Farsi follow-up, PR #355): emulator 2026-10-25 PASS on an upgraded install: three distinct piglets and the Wolf voice in English ... [full text](notes/2026-10-30-outstanding-detail.md#o062) ([slice](slices/135-dino-animal-voices-fixes.md))

### Slice 134: dino usage events and report ([slice](slices/134-dino-usage-events-and-report.md))

- Slice 134: telemetry on since 2026-10-27, `tools/dino-usage-report.ps1` runs against live data; emulator smoke of each event; `own_book_added` not ... [full text](notes/2026-10-30-outstanding-detail.md#o068) ([slice](slices/134-dino-usage-events-and-report.md))

### Slice 133: dino anonymous usage counts ([slice](slices/133-dino-anonymous-usage-counts.md))

- Slice 133: telemetry on and rows arrive in `dino_usage` since 2026-10-27; settings off sends nothing still to check on a device; no rate limit or ... [full text](notes/2026-10-30-outstanding-detail.md#o066) ([slice](slices/133-dino-anonymous-usage-counts.md))

### Slice 131: mobile shared namespace ([slice](slices/131-mobile-shared-namespace.md))

- Slice 131: macOS CI iOS build after merge; emulator launch of both apps PASS 2026-10-26 ([slice](slices/131-mobile-shared-namespace.md)).

### Slice 129: dino voice features out of shared engine ([slice](slices/129-dino-voice-features-out-of-shared-engine.md))

- Slice 129: emulator Dino story with flavoured voices (Small, Whisper, animal sounds) sounds the same, by ear; Windows head flavoured character ... [full text](notes/2026-10-30-outstanding-detail.md#o065) ([slice](slices/129-dino-voice-features-out-of-shared-engine.md))

### Slice 126: dino keep screen awake ([slice](slices/126-dino-keep-screen-awake.md))

- Slice 126: emulator PASS (screen stays on while playing, sleeps after pause and at the end); screen awake on a real phone PASS while playing (end ... [full text](notes/2026-10-30-outstanding-detail.md#o063) ([slice](slices/126-dino-keep-screen-awake.md))

### Slice 125: dino character animals ([slice](slices/125-dino-character-animals.md))

- Slice 125: emulator 2026-10-25 PASS for the Animal line, "Not an animal" persisting and the Farsi card (its bugs were fixed in Slice 135); store ... [full text](notes/2026-10-30-outstanding-detail.md#o061) ([slice](slices/125-dino-character-animals.md))

### Slice 124: dino words panel matches mock ([slice](slices/124-dino-words-panel-matches-mock.md))

- Slice 124: emulator 2026-10-25 PASS for the switch card and sub-line, the fade while scrolled and the panel inside the picture (bottom alignment of ... [full text](notes/2026-10-30-outstanding-detail.md#o060) ([slice](slices/124-dino-words-panel-matches-mock.md))

### Slice 123: dino pigs one action per page ([slice](slices/123-dino-pigs-one-action-per-page.md))

- Slice 123: on the phone, the pigs show 16 pages with pictures; an installed v3 copy updates to v4 and keeps recordings on unchanged pages ([slice](slices/123-dino-pigs-one-action-per-page.md)).

### Slice 122: dino text panel 30 and switch ([slice](slices/122-dino-text-panel-30-and-switch.md))

- Slice 122: on the user's phone, pigs upright and sideways (panel at most 30%); photo/PDF import defaults the switch off; a wide picture sideways ... [full text](notes/2026-10-30-outstanding-detail.md#o059) ([slice](slices/122-dino-text-panel-30-and-switch.md))

### Slice 121: dino illustrate store stories ([slice](slices/121-dino-illustrate-store-stories.md))

- Slice 121: check the illustrated books on a phone (pictures, text panel, voices) ([slice](slices/121-dino-illustrate-store-stories.md)).

### Slice 119: dino illustrated three little pigs ([slice](slices/119-dino-illustrated-three-little-pigs.md))

- Slice 119: on-device starter auto-update and recording copy (unchanged pages keep recordings, edited text is skipped); the pigs pictures in both ... [full text](notes/2026-10-30-outstanding-detail.md#o058) ([slice](slices/119-dino-illustrated-three-little-pigs.md))

### Slice 118: dino storyteller more languages ([slice](slices/118-dino-storyteller-more-languages.md))

- Slice 118: the listening check of the es, pt and zh WAVs; real-phone Kokoro speed is done ([S21 night ... [full text](notes/2026-10-30-outstanding-detail.md#o057) ([slice](slices/118-dino-storyteller-more-languages.md))

### Slice 117: dino animal sounds ([slice](slices/117-dino-animal-sounds.md))

- Slice 117: native read of all animal words and sounds (Persian pig and cow are guesses); `BleatWobble` depth by ear; iOS reader build. ([slice](slices/117-dino-animal-sounds.md)).

### Slice 116: dino storyteller characters ([slice](slices/116-dino-storyteller-characters.md))

- Slice 116: native read of roster names and hints, especially Farsi; Farsi bee=Small and princess=Whisper flavours by ear; Chinese has only one ... [full text](notes/2026-10-30-outstanding-detail.md#o056) ([slice](slices/116-dino-storyteller-characters.md))

### Slice 115: dino storyteller voices by default ([slice](slices/115-dino-storyteller-voices-by-default.md))

- Slice 115: the emulator checks listed under Verification; native read of the `Storyteller_*` strings (12 are machine drafts); a "Download now" tap ... [full text](notes/2026-10-30-outstanding-detail.md#o054) ([slice](slices/115-dino-storyteller-voices-by-default.md))

### Slice 114: dino kokoro english storyteller pack ([slice](slices/114-dino-kokoro-english-storyteller-pack.md))

- Slice 114: listening to Kokoro English with flavours on a real phone; Windows head storyteller; native check of the strings ([slice](slices/114-dino-kokoro-english-storyteller-pack.md)).

### Slice 113: dino character flavours ([slice](slices/113-dino-character-flavours.md))

- Slice 113: pitch by ear (drop it if robotic); first Sounds-row tap after a flavour change fixed (PR #556: waits for the save, one sample at a time, ... [full text](notes/2026-10-30-outstanding-detail.md#o055) ([slice](slices/113-dino-character-flavours.md))

### Slice 112: dino store age author filters ([slice](slices/112-dino-store-age-author-filters.md))

- Slice 112: native read of new strings ([slice](slices/112-dino-store-age-author-filters.md)).

### Slice 111: dino parent recordings ([slice](slices/111-dino-parent-recordings.md))

- Slice 111: record/replay on a real phone (Android 9-11), iOS, Windows head; mic permission denial; backup round trip; native read of `Rec_*` ([slice](slices/111-dino-parent-recordings.md)).

### Slice 110: dino translated text on screen ([slice](slices/110-dino-translated-text-on-screen.md))

- Slice 110: scanned page picture plus translation; native read of `Book_*` in 14 locales ([slice](slices/110-dino-translated-text-on-screen.md)).

### Slice 109: dino one narrator per language ([slice](slices/109-dino-one-narrator-per-language.md))

- Slice 109: older build opens new package, old backup re-cast; native check of 14 locales ([slice](slices/109-dino-one-narrator-per-language.md)).

### Slice 108: dino cast per reading language ([slice](slices/108-dino-cast-per-reading-language.md))

- Slice 108: native check of `Cast_VoicesFor` ([slice](slices/108-dino-cast-per-reading-language.md)).

### Slice 107: dino own books translate by default ([slice](slices/107-dino-own-books-translate-by-default.md))

- Slice 107: Windows head greyed switch and note; native read of the 3 new strings ([slice](slices/107-dino-own-books-translate-by-default.md)).

### Slice 106: windows nordic dutch translation packs ([slice](slices/106-windows-nordic-dutch-translation-packs.md))

- Slice 106: Norwegian on a Windows head via `MarianEngine` (third-party conversion, never run); en to da/fi/sv/nl/nb and back on Windows; the ... [full text](notes/2026-10-30-outstanding-detail.md#o070) ([slice](slices/106-windows-nordic-dutch-translation-packs.md))

### Slice 105: dino nordic dutch books and rules ([slice](slices/105-dino-nordic-dutch-books-and-rules.md))

- Slice 105: Swedish and Danish reading PASS on the S21; the no-voice phone-voice fallback PASS but weak (every app language has a voice, so no true ... [full text](notes/2026-10-30-outstanding-detail.md#o053) ([slice](slices/105-dino-nordic-dutch-books-and-rules.md))

### Slice 104: piper norwegian pack ([slice](slices/104-piper-norwegian-pack.md))

- Slice 104: listen to pick the clearest default voice (KNN is a placeholder; Slice 195); Norsk download and read on the S21 PASS via Slice 201; ... [full text](notes/2026-10-30-outstanding-detail.md#o052) ([slice](slices/104-piper-norwegian-pack.md))

### Slice 103: nordic dutch languages ([slice](slices/103-nordic-dutch-languages.md))

- Slice 103: emulator check of each new UI language; Windows head in Dutch; a native read of all translations; Dino's iOS app has no localisation ... [full text](notes/2026-10-30-outstanding-detail.md#o049) ([slice](slices/103-nordic-dutch-languages.md))

### Slice 102: dino store ten stories ([slice](slices/102-dino-store-ten-stories.md))

- Slice 102: emulator: ten cards with credits, download, read with voices, Farsi read; a native-speaker read of the translations ([slice](slices/102-dino-store-ten-stories.md)).

### Slice 101: rtst mobile review and translate ([slice](slices/101-rtst-mobile-review-and-translate.md))

- Slice 101: emulator device-test of the page; real ML Kit download and translation PASS on the S21 (fr/es/de, [S21 night ... [full text](notes/2026-10-30-outstanding-detail.md#o051) ([slice](slices/101-rtst-mobile-review-and-translate.md))

### Slice 100: rtst mobile page capture wizard ([slice](slices/100-rtst-mobile-page-capture-wizard.md))

- Slice 100: a real ML Kit scan on a signed-in phone; iOS capture (compile and behaviour); a native read of the new strings ([slice](slices/100-rtst-mobile-page-capture-wizard.md)).

### Slice 99: rtst mobile minimal main page ([slice](slices/99-rtst-mobile-minimal-main-page.md))

- Slice 99: full TalkBack walk-through; iOS device run; Settings voice name refresh PASS on the S21 ([S21 night runs](notes/2026-10-29-s21-night-runs.md)) ([slice](slices/99-rtst-mobile-minimal-main-page.md)).

### Slice 98: dino picture and text books ([slice](slices/98-dino-picture-and-text-books.md))

- Slice 98: emulator result for the picture + text flow was recorded in the slice; Windows head side-by-side layout in a wide window; Three Little ... [full text](notes/2026-10-30-outstanding-detail.md#o048) ([slice](slices/98-dino-picture-and-text-books.md))

### Slice 97: dino add book button ([slice](slices/97-dino-add-book-button.md))

- Slice 97: Windows head: the AddBook icon rendering and the gate opening; native-speaker read of the `Gallery_AddBook` strings ([slice](slices/97-dino-add-book-button.md)).

### Slice 96: dino layout never mirrors ([slice](slices/96-dino-layout-never-mirrors.md))

- Slice 96: emulator in Farsi and Arabic (gallery, gate, wizard, store, backup, reader, swipe direction, highlight on RTL lines); Windows head gallery and reader ([slice](slices/96-dino-layout-never-mirrors.md)).

### No slice

- Older unverified items (Slices 43-95 and a flaky-test note), moved verbatim: [outstanding-older](notes/2026-10-25-outstanding-older.md). Still open until verified.
