# Not yet tried on a device

Every slice's open Not yet verified checks, newest slice first. A line leaves when the check passes on a device or the user confirms it; plan.md's Outstanding work keeps only the count. The user (2026-11-01 07:31): "I dont have time to check your shit on ios"; the lines stay here until a device run or the user clears them.

**Only what can be tested stays here** (the user, 2026-11-08: "keep only whatever I can test"). Each line starts with who tests it: **You** (the user: the iPhone, the S21 by hand, the Echo, the Sonos, an English or Farsi read, a decision or account only you hold) or **Device** (Claude, on the Android emulator or tablet AVD, the S21 by script, or the Windows VM). Checks nobody here can test were removed; behaviours only real use shows are watched in production data instead; checks that need the user's ear are listening-page slices 330-343. All three lists are in the [record](notes/2026-11-08-unverified-triage.md). Earlier on 2026-11-08: triaged 2026-11-08: 106 lines removed as replaced, already done, proven in production or not a check; the real gaps moved to [Slice 316](slices/316-unverified-triage-gaps.md); the Slice 43-95 lines that were still open moved here from [outstanding-older](notes/2026-10-25-outstanding-older.md) ([record](notes/2026-11-08-unverified-triage.md)). The 2026-11-08 device run (emulator, S21, VM) passed 49 more and the user's iPhone session 2 more; same record.

### Slice 372: S21 check fixes ([slice](slices/372-s21-check-fixes-2026-11-13.md))

- **Device:** left on the phone by the runner: `/sdcard/Download/rtst-packs-aside/uk.sirous.readthestupidtext.dbg/packs-20261113172728.tar` (the dev app's own pack backup).
- **Device:** why the heal survived TERM is unknown (now harmless: the pid-file guard).
- **Device:** voiced book-end timing measured once (159 s; timeout 240 s).

### Slice 368: Shared tests in their own project ([slice](slices/368-shared-tests-project.md))

- **Device:** the new `release.yml` gates (`shared-test`, `rtst-test`) and `tests.yml` jobs (`select`, `shared`, `rtst`) have not run on GitHub; the first PR run and the first `main` push show them.

### Slice 360: Dino play waits for Wi-Fi ([slice](slices/360-dino-play-waits-for-wifi.md))

- **Device:** book pulled mid-launch keeps its card until relaunch (check5 #1); a malformed listing entry gets no card (check5 #2).
- **Device:** offline launch with a saved listing older than a newly published pack: no card until the next online launch refreshes the listing (check3 #1).
- **Device:** no jank opening a book on a slow phone (the pack-folder listing on the UI thread in `ReadingPage.OnAppearing`, review #9).
- **You:** iOS compile after the last fixes, and a TestFlight run of the cellular pack download and the reading-page states.
- **Device:** real airplane mode on a phone (the flows force offline by a Debug hook).
- **Device:** add a language on mobile data only: every shelf book's pack for it downloads with no question.
- **Device:** after release: `premade_audio:waiting_wifi` (no emitter now, expect zero) and `pack_missing_on_play` counts in dino_usage.
- **Device:** Worker serving one version behind, live, after the batched release deploys.

### Slice 365: `worker/catalogue.test.mjs` LF test on a Windows checkout ([slice](slices/365-catalogue-lf-test-windows.md))

- **Device:** the test run on Linux CI (the PR's checks).

### Slice 364: Slice 357 design pass ([slice](slices/364-server-errors-design-pass.md))

- **Device:** Windows head store page (Dino) on the VM (5.8 GB free, VM not started).
- **Device:** RTST App x64 voice error text (`Error_Server` / `Error_VoiceGone`) on the VM.
- **Device:** RTST Android pack-failure text on a device.
- **Device:** iOS compile and TestFlight.
- **You:** native reads of the 14 non-English languages (machine-drafted).
- **Device:** a list-load 402/404 shows a book-specific text (`ShowOffline` reuses `StoreText.ErrorMessage`): check whether the list endpoint can answer those.

### Slice 363: Dino grown-ups' gate card ([slice](slices/363-dino-windows-grownups-gate-card.md))

- **Device:** Windows head on the VM: `dino-gate`, G1/G2 screenshots against the mock, Esc, and the four changed VM checks (5.8 GB free, no VM run).
- **Device:** the tablet card flow on the tablet AVD.
- **Device:** the hold ring's touch area on the short card (check #9).
- **Device:** landscape phones under 393 dp high.
- **Device:** Windows under about 470 wide and under 540 high.
- **Device:** the UIA AutomationId `GateCard` on WinUI.

### Slice 359: Dino: a drawn back-cover picture for every book ([slice](slices/359-dino-back-cover-picture.md))

- **Device:** iOS on a device (compile only).
- **Device:** Windows head run (compiled only).
- **Device:** tablet layout and the Batch 45 22/17 sp credit sizes (wait for the tablet slice, 234).
- **Device:** long-language credits in the grown band on a narrow phone beyond fa/ar.
- **Device:** older installed apps showing the picture page.
- **Device:** Good Enough's back cover (after its text-free redraw, Slice 350 part E, and its text rewrite).
- **Device:** the store upload of the 11 store books and their new versions on the live store.

### Slice 357: Server errors the user can see: standard messages per status ([slice](slices/357-server-error-messages.md))
- **Device:** RTST pack-failure text on a device: no scripted hook for a pack server error.
- **Device:** Dino shop list-failure panel text on a device: no list-error hook.
- **Device:** Windows VM: a refused Get on the Dino Windows head.
- **You:** iOS TestFlight: one refused Get.
- **You:** Native read of the new strings in the 14 non-English languages.
- **Device:** Dino shop: a failed Get while a background book update downloads the same book at the same moment (`StoreClient._downloadErrors` is keyed by book id; check3 #2).
- **You:** Part D: the `minVersion` change to `1.124.0` is open until paid books are switched on for everyone.

### Slice 355: Dino: a public web page listing every store book, free or paid, and the price ([slice](slices/355-dino-web-catalogue.md))
- **Device:** Live `/books` and the workers.dev `/dino/books` after the batched release deploys: covers load, no third-party request, loading/Cancel/Retry work, and with the switch on the price shows only on paid books.
- **You:** Native read of the 14 non-English translations of the page text.
- **You:** Switched-on check after the paid books store setup (Slice 346 Part D).

### Slice 295: Bump MauiVersion for dotnet/maui PR 38662 ([slice](slices/295-bump-maui-version-for-pr-38662.md))
- **Device:** RTST emulator flows on MAUI 10.0.130: not reached in the one emulator run (Dino only).
- **You:** TestFlight iPhone check of the earlier Slice 274 crash path on a release build: open a parent page, leave it (language page), background, return; no crash. Needs a TestFlight build after merge; Slice 295 work item 5 (remove the Slice 274 workaround) waits on it.

### Slice 356: Dino illustrator: faster and cheaper ([slice](slices/356-illustrator-faster-and-cheaper.md))
- **Device:** after-numbers on a real page group of a book (seconds per picture, peak context, cache reads per picture against the Baseline table), taken on the next real illustrator run with `illustrator-usage.mjs`.
- **Device:** a real send after the check-pass edit to `__setupPicture` (pictures validation, mark only after all checks, mark restored on REFUSED/MISSING); only the earlier version was live-sent on 2026-11-13.

### Slice 352: Dino: every screen, wizard step and button press is counted ([slice](slices/352-dino-screen-and-tap-events.md))
- **Device:** corner screen row and a speed `setting_changed` on a device (the flows do not open them).
- **Device:** the Windows VM: window-close flush and `app_backgrounded` (the `Destroying` path); the package builds since Slice 353. Harness gap (2026-11-13 VM run): no VM check closes the window and reads the usage events.
- **You:** the iOS simulator (none on this machine) and the `DidEnterBackground` flush.
- **Device:** `tools/usage-sessions` against live data after release.

### Slice 353: CI: keep the .NET 10 SDK on runners with .NET 11, fix the Dino Windows VM test package ([slice](slices/353-ci-sdk-pin-and-vm-test-package.md))
- **Device:** `android.yml` and one other workload workflow green on a run whose "Set up job" shows runner image 20261111 or later (the android jobs of PR #1024 and PR #1022 both ran on the old image 20261101.186.1, so the pin is not yet proven there).
- **Device:** Dino VM checks of Slice 352 (desktop), now that `Build-TestPackage.ps1 -App dino` builds.

### Slice 351: Dino: every usage row says who and when, and audio failures say why ([slice](slices/351-dino-usage-context-and-audio-causes.md))
- **Device:** `premade_audio:no_pack` with a book id, on a book whose pack is not downloaded.
- **You:** the iOS run of the context fields and the new fallback reasons (iPhone or TestFlight).
- **Device:** Windows head on the VM, including the animal clip copy failure reason.
- **Device:** after release, a weighted query shows blobs 17-19 and double6 filled on rows from the new version.
- **Device:** `.maestro/dino/usage-context.yaml` should set English first (the flow assumes an English UI).

### Slice 350: Dino: high-resolution pictures for a printed Amazon book ([slice](slices/350-dino-high-res-pictures.md))
- **You:** the first real high-res push on `main` starts no `dino-store` run (needs part D, which needs your Chrome and Gemini account).

### Slice 349: Dino: grown-ups corner status bar and clipped tab labels ([slice](slices/349-dino-corner-status-bar-and-tab-labels.md))
- **Device:** the `[StatusBar] ... darkIcons=False` log line was not seen in logcat (the flow passed without it).
- **Device:** Farsi screenshots were not inspected (the flow passed).
- **Device:** tab labels at system font scale 1.3.
- **You:** iOS simulator or TestFlight corner screens (status bar and tabs).
- **Device:** Windows head title bar (VM not run).
- **You:** item 4: the next TestFlight upload of Dino is accepted by App Store Connect with no missing purpose-string warning (ITMS-90683); if flagged, restore the three keys.

### Slice 346: Dino: four free books, the rest $0.99 each ([slice](slices/346-dino-paid-books.md))
- **You:** a real Play test-account purchase and restore.
- **You:** an iOS StoreKit sandbox or TestFlight purchase and restore.
- **Device:** the Windows Store add-on on the VM.
- **Device:** the switch-on store page against the real Worker.
- **You:** the Microsoft Store ID flow against a live store.
- **Device:** E1's log lines appear in Workers Logs on the next real paid download (read with `CF_OBSERVABILITY_READ_TOKEN`).

### Slice 341: Listening page: Farsi short words never dropped ([slice](slices/341-farsi-short-words-never-dropped.md))
- **Device:** a phone run of short Farsi lines in Read The Stupid Text (mana with the ganji rescue, e.g. «سلام.», «آه.») is not done.

### Slice 345: RTST: the Debug stall hook must block the reader's first-chunk wait ([slice](slices/345-rtst-stall-hook-blocks-reader.md))
- **Device:** `stall-return-overlay` on iOS (**You**, no Mac; the iOS reader edit is not compiled or run) and on the Windows head (VM); only the Android emulator ran, twice, 2026-11-09.

### Slice 340: Listening page: Mid-book speed change on a phone ([slice](slices/340-mid-book-speed-change-on-a-phone.md))
- **Device:** Fix 1 on a device: Settings > Reading speed slider runs 0.8x-1.2x with the left label "0.8x", on Android, iOS and the Windows head, and a book reads at 0.8x at the low end. Android phone emulator, 2026-11-09: `settings-reading-speed-row.yaml` passed and shows "0.8x · slow"; iOS (**You**, no Mac), Windows and the 0.8x reading are still open.

### Slice 339: RTST real-phone audio timing ([slice](slices/339-real-phone-audio-timing.md))
- **Device:** S21: run `tools/phone/audio-timing.ps1 -App rtst` and check the 1x gaps between chunks against the script's own limit (a scrcpy recording on 2026-11-08 showed 1.1-1.5 s gaps).

### Slice 322: Dino's silent beats pause when the screen locks ([slice](slices/322-dino-beat-pauses-on-screen-lock.md))
- **You:** iOS (no Mac): the same `Window.Stopped`/`Resumed` handlers, not run.
- **Device:** Windows head: `WindowForeground` returns unknown and `Deactivated` is unhandled; not run on the VM.
- **Device:** Edge cases reverted after the second strike: a window re-subscribe while an event fact is newer than the platform read, and a null `Platform.CurrentActivity`.
- **Device:** Harness flake risk (`reviews/s322-check-final3.md` #1-#3): the screen cycle could sleep the display during the flow's final launch/assert or setup waits.

### Slice 321: Reorder layer back to the mock on Windows ([slice](slices/321-reorder-layer-per-mock.md))
- **Device:** The tablet AVD (`dino-tablet-upright-shelf`): the layer uses the shelf's grid, hint aligned with its left edge, also beside the grown-ups' rail.
- **Device:** Phone portrait: 3 across, 14 dp sides, 27 dp row gap (no flow asserts the measurements).
- **Device:** Windows VM with 6 or more books (only 2 books were used; 2026-11-13 VM run: still 2 books, `dino-reorder` passed, no 6-book + resize check exists), and a window resize while the layer is open (columns change, no layout cycle).
- **Device:** A narrow Windows window after recheck fix 1.
- **Device:** The 30 px versus 40 px inset on Windows (an assertion that agrees with the screenshot). 2026-11-13 VM run: `dino-reorder` logged 30 px against the mock's 40, with 0 layout-cycle lines, but no assertion.
- **Device:** The `shelf-reorder` first-launch markers (`HOOK skip-first-run ok`, `HOOK shelf-order-reset ok`).
- **You:** iOS phone/tablet: the same shared MAUI code; no Mac, not run.

### Slice 320: Device-check fixes and missing test hooks from the 2026-11-08 run ([slice](slices/320-device-check-tooling-from-2026-11-08-run.md))
- **Device:** RTST `stall-return-overlay` failed twice (the hook does not block the reader); moved to [Slice 345](slices/345-rtst-stall-hook-blocks-reader.md).
- **Device:** On the S21: `book-end` on Dino with the fixture pack (run 2026-11-13: not finished, still playing at 521 s of its 900 s window when the 590 s agent timeout ended the run; the 420 s catalogue estimate is too low for the S21), `run-checks -Prepare -App dino`, `paused-notification-repost` flag reading (run 2026-11-13 FAILED as a check bug: parsed flags 0x800 lack FLAG_AUTO_CANCEL 0x10, so `Get-NotificationFlagsFromText` reads the wrong field of Samsung's record; fix and rerun in Slice 372).
- **You:** iOS: the new hooks are untried (no Mac, no iOS compile was possible).

### Slice 147: iOS review cadence: one review at a time, the newest build, never cancel ([slice](slices/147-ios-review-cadence.md))
- **You:** the first successful `appstore-submit.yml` run (manual; its only two runs, 2026-10-30, concluded failure): it should report the listing items Apple still needs (screenshots, privacy, age rating). The first scheduled `testflight-external.yml` run already succeeded.

### Slice 325: Dino every page in the 2026-11-08 design, parts A-F ([slice](slices/325-dino-2026-11-08-redesign.md))
- **Device:** No `layout-check ok` line was printed for any onboarding or intro flow.
- **You:** iOS on a device (34 pt home bar, 59 pt island inset) for the first run and intro.
- **Device:** Windows intro Left from the languages step may drop unsaved ticks (check6 #4, unconfirmed).
- **You:** 14 machine-drafted translations of the new first-run strings need native review (parts A-C add `Store_OnShelf` and `Settings_GroupComputer`).
- **You:** iOS on a real iPhone 8 and an iPhone 17 Pro Max (Dynamic Island, home bar, landscape flip) for the corner tabs and kids' shelf (parts A-C).
- **You:** The Samsung three-button bar on the S21 (frame P5 is an assumed size); a real tablet in both orientations.
- **Device:** Arabic and Persian first-run and corner layouts on a device (parts A-C).
- **Device:** Contact sheet for the new shots: the onboarding flow names shot-OL1-language and shot-OTL1-language but takes neither (no manifest row); O1 / O1-8 and O6 / O6-8 may be the same image if the 375 x 667 resize does not take effect between captures; the intro flow header says O9 where the frame is OT9 (comment only).

### Slice 323: Dino shelf titles follow the story language picked on the shelf ([slice](slices/323-dino-shelf-titles-follow-story-language.md))
- **You:** iOS has no scripted run: the shelf title font for a Farsi title (Vazirmatn) on a tile and in the book sheet.
- **You:** On the iPhone (TestFlight): picking فارسی on the shelf retitles "Good Enough" and "The Three Little Pigs".

### Slice 324: Dino reads at 1x by default ([slice](slices/324-dino-default-speed-1x.md))
- **You:** iOS: a fresh Dino install shows 1x on the Reading speed page and a pre-made clip plays at normal speed.

### Slice 319: Fixes from the 2026-11-08 device run ([slice](slices/319-device-run-2026-11-08-fixes.md))
- **Device:** Maestro flow `.maestro/rtst/sentence-highlight.yaml` changed to the same text, not run. iOS shares `ReaderPage`; no iOS change.
- **Device:** Tablet emulator look of the Reorder layer after the geometry redesign (shared code; not run).

### Slice 318: Dino: the /stats page on phones and tablets ([slice](slices/318-dino-stats-phone-tablet.md))
- **Device:** Both orientations on the live /stats page on the S21 and the tablet AVD (touch, notch spacing, Filters sheet focus, the side panel in landscape).
- **You:** The live page against real D1 data (the Worker deployed 2026-11-08 and serves the new layouts; the real-data look is not checked).
- **Device:** Screenshots of the page side by side with the mock frames 200-204 (not compared; `tools/stats-page-check/check.mjs` checks the values only).

### Slice 313: Dino: a shelf book's text and its sound always match ([slice](slices/313-dino-book-text-and-sound-in-step.md))
- **You:** The user's phone: Freddy in English and Farsi after updating to the release with this slice (Dino 1.116.0 or later): new text, and sound plays.
- **You:** iPhone: the same path.
- **Device:** Windows head: the same path.

### Slice 316: Gaps found by the unverified-checks triage ([slice](slices/316-unverified-triage-gaps.md))
- **Device:** `paused-notification-repost` has never run on a phone: the `flags=0x` parsing of dumpsys is unproven.
- **Device:** That a later edit clears the Review pick on the reader page: no flow step yet.

### Slice 291: Store listings and screenshots for both apps ([slice](slices/291-store-listings-and-screenshots.md))
- **You:** A native read (Farsi) of the translated Microsoft Store notes and usage-counts bullets before the Partner Center push.

### Slice 287: Dino: a public usage dashboard at dinoreader.sirous.uk/stats ([slice](slices/287-dino-usage-dashboard.md))
- **You:** A private-window open of the public page shows it.
- **You:** The page side by side with mock frame 200 at 1280 (the mock's `support.js` is not in the repo, so it could not be rendered).

### Slice 282: Dino: an Alexa kid skill that reads the store books ([slice](slices/282-dino-alexa-kid-skill.md))
- **You:** Everything on a real Echo: open, list, read, pause, resume after a minute, next, stop.
- **You:** A real signed Alexa request passing verification live.

### Slice 289: Dino: count Sonos plays on the server ([slice](slices/289-dino-count-sonos-plays.md))
- **You:** A live Sonos play counts exactly one row, once the user's Sonos sandbox (Slice 283) works.

### Slice 314: Restore the stall overlay when the person returns to the reader ([slice](slices/314-reader-stall-overlay-after-return.md))
- **Device:** Start a stalled read on Android, open Settings, come back: overlay and Cancel shown.
- **You:** The iOS stall watcher on device.

### Slice 312: Read the iPhone's crash logs from this PC ([slice](slices/312-read-iphone-crash-logs-from-this-pc.md))
- **You:** Item 2: a real pair and pull on the iPhone (needs the user's one-time Apple Devices/driver and libimobiledevice install, and one cable pairing).
- **You:** The real `.ips` field names and header layout: the parser was written from the known iOS crash format and checked only against a synthetic fixture, so the first real file may need a tweak.

### Slice 297: Slice 267 follow-ups ([slice](slices/297-slice-267-follow-ups.md))
- **You:** iOS wizard layout (no Mac or iPhone tonight).

### Slice 305: Dino package cleanup ([slice](slices/305-dino-package-cleanup-ml-kit-and-sherpa.md))
- **You:** TestFlight look of Dino.

### Slice 300: Kept problem report with the crash switch off ([slice](slices/300-user-report-kept-while-crash-switch-off.md))
- **Device:** Emulator: with the usage switch off, a failed user-report send shows the not-kept text and logs `not kept, usage switch off`. Needs a Debug hook that makes the report endpoint refuse; none exists.
- **You:** iOS device: the same shared code, not run on a device.

### Slice 308: Slice 225 run-checks -Serial and failed-build cause ([slice](slices/308-slice-225-run-checks-serial-and-build-cause.md))
- **Device:** Phone `paused-notification` before the `Get-PackageUid` fix: the shadowing left the dev app's uid unread, so earlier phone PASS results may never have blocked the network; re-run it on the phone once (feedback loop 2026-11-07-1412, [Slice 315](slices/315-feedback-loop-2026-11-07-1412-leftovers.md) item 11).

### Slice 299: Slice 261 leftovers ([slice](slices/299-slice-261-leftovers.md))
- **Device:** First-run picker and Cancel during the list load on emulator and iOS (item 2 follow-up: a cancelled load shows the "Connect to the internet" copy; a distinct copy needs new strings in every language).

### Slice 284: Final loop leftovers, night 2026-11-06-1250 ([slice](slices/284-final-loop-leftovers-night-2026-11-06-1250.md))

### Slice 304: Slice 202 re-review and the InitWait order test ([slice](slices/304-slice-202-rereview-and-initwait-test.md))
- **Device:** Importing the same text keeps the language (device).
- **Device:** Language-change rebuild shows a loading state when it takes over 500 ms (review 9; caller in Settings/AppShell, not read by this slice).

### Slice 302: Slice 239 leftovers ([slice](slices/302-slice-239-leftovers.md))
- **Device:** Item 1 was closed from a code read only: the "renamed store package downloaded twice" report was not reproduced on a device run, so a double download on a fresh install is not ruled out.

### Slice 298: Slice 264 gallery reorder follow-ups ([slice](slices/298-slice-264-gallery-reorder-follow-ups.md))
- **Device:** Real drag in both drop event orders (Drop then DropCompleted, and the reverse) on Android, iOS and the Windows head.
- **Device:** A 1 s hold, and a scroll that cancels the hold, on Android, iOS and the Windows head.
- **Device:** Hold release after a relayout (rotation) on Android, iOS and the Windows head.

### Slice 288: Dino: the app counts listens (30 s or more) and real listening time ([slice](slices/288-dino-listens-and-listening-time.md))
- **Device:** The same flow on the Windows head.

### Slice 286: Dino: record your voice in each story language, and hear it when reading in that language ([slice](slices/286-dino-recordings-per-story-language.md))
- **You:** iPhone: the record-per-language flow.
- **Device:** Windows head on a real screen: centring of the record intro on a wide window.
- **You:** The Farsi and English language-name strings and the Record_LanguageUnavailable / Record_LanguageRetry translations, read by the user.
- **Device:** Emulator store audio had to be seeded because the store was unreachable in the test setup, so the real download path was not covered.

### Slice 290: the reading page's sound line as a pill ([slice](slices/290-dino-reading-sound-pill.md))
- **Device:** Every pill state on a device against frames 197-199 (lilac Getting pill with the language name, Wi-Fi, Preparing, no sound) and the medium weight.
- **Device:** The pill's accessibility text: no `ReadingAudioStatus` node appeared in uiautomator dumps during the language flow (part B).
- **Device:** A Farsi pill whose text wraps (label right-aligned, icon still left).
- **Device:** The pill's UIA name on the Windows head (VM).

### Slice 285: Dino: changing the app language fetches the books' sound in that language ([slice](slices/285-dino-language-change-fetches-book-audio.md))
- **You:** iPhone: switch the app language, open a store book, press Play. Sound should play in the new language with no relaunch.
- **Device:** Windows head: the same.

### Slice 283: Dino: a Sonos music service for the store books ([slice](slices/283-dino-sonos-music-service.md))
- **Device:** Live `POST /sonos` after the Worker deploy: getMetadata root lists the languages, getMediaURI returns the track URL.
- **You:** Sonos Self Test passes.
- **You:** A real play on a Sonos speaker.

### Slice 281: Dino: one listening track per store book and language ([slice](slices/281-dino-listening-tracks.md))
- **Device:** `curl -I` and a ranged `curl -r 0-1023` on a live track answer 200 and 206 with `audio/mp4`.
- **Device:** Every live store book has a track for every language with a pack.

### Slice 279: Dino: recording your own voice, one intro page, then per character or whole page ([slice](slices/279-dino-recording-flow.md))
- **You:** iPhone: layout and recording.
- **You:** A real microphone recording on Windows (the VM has none; R opens Windows' microphone consent prompt).
- **Device:** Backup merge with a whole-page clip and a piece clip on the same page, and orphan files after a same-key restore.
- **Device:** Intro freeze on a long book: not measured.

### Slice 280: Dino: Farsi speaker first, joined at translation time ([slice](slices/280-dino-farsi-speaker-first-at-translation.md))
- **You:** On a real phone: the live fa pack after the upload.

### Slice 278: Dino: store books only, parts A and B ([slice](slices/278-dino-store-books-only.md))
- **You:** Part B: the mobile-data prompt (`ShelfAudioAsk`) on a real cellular-only phone.
- **Device:** Part B: the beat pausing on screen lock (no scripted flow).
- **Device:** Part B: a Release build downloads no voice pack.
- **Device:** No flow asserts the "Get more books" text or that the Add pill is gone.
- **Device:** The A3 cleanup has no device check: no Debug hook seeds an own book, so deleting an own book on a real install is untried.

### Slice 275: Leftovers from night 2026-11-06-0042, parts 1 to 3 ([slice](slices/275-night-2026-11-06-0042-leftovers.md))
- **Device:** Item 7: no emulator flow cancels an audio pack download and checks that `cancelled` is sent (there is no test hook to stall a pack fetch); only unit and Worker tests cover it.

### Slice 271: Leftovers from night 2026-11-05-1823 ([slice](slices/271-night-2026-11-05-1823-leftovers.md))
- **Device:** A Windows VM check that plays a pre-made-audio book past two clip hand-offs (item 2, VM half): the VM package is a Release build with no Debug test hooks, so no fixture audio pack can be installed there; it needs Debug test hooks (or an install path) in the VM package first.

### Slice 274: Dino iPhone crash after leaving the grown-ups pages ([slice](slices/274-dino-ios-crash-after-leaving-parent-page.md))
- **Device:** Android emulator: drag and release the reading-speed slider, and open the Language page; both behave as before (the builder moved the slider's Unloaded handler onto a wrapper and made `LanguagePage.WhenLoadedAsync` wait on RootPage; added by the feedback loop of 2026-11-06).

### Slice 270: Dino iPhone crash, player disposed in its finished callback ([slice](slices/270-dino-ios-player-disposed-in-finished-callback.md))
- **Device:** Windows clip hand-off: no VM check plays audio clips (a harness gap).
- **Device:** A parent's recording that ends while paused waits for Resume (`_recordingEndedWhilePaused` in `ReadingSession.cs`): the logic is unit-tested now (Slice 271, `ReadingPauseStateTests`); the device flow is still owed.

### Slice 269: Leftovers from night 2026-11-04-1441 ([slice](slices/269-feedback-loop-2026-11-04-1441-leftovers.md))
- **Device:** Emulator cold-boot retry path of the preflight (not triggered in the run; kill the Maestro driver after a snapshot boot).

### Slice 235: Dino on Windows, part C ([slice](slices/235-dino-windows-window.md))
- **Device:** Reorder mode on Windows keeps phone padding (review high 6).
- **You:** Real mouse hover on a real PC.

### Slice 268: Dino: remove "Show original text" from grown-up mode ([slice](slices/268-dino-remove-show-original-text.md))
- **Device:** The new flow `book-sheet-no-original.yaml` was not shown failing on the old build.
- **Device:** A book saved with the old flag on, read on a device.

### Slice 267: Dino bugs: books on the shelf keep the old voice; the first-run buttons overlap ([slice](slices/267-dino-old-voice-and-wizard-buttons.md))
- **You:** The user's phone check (no phone tonight), including the "check first on the user's phone" item (app version, `dino.books.lastupdatecheck`, whether `audio-packs/<book>/` exists): the cause is from code and the emulator reproduction, not from the phone.
- **Device:** The upgrade-from-previous-release path (previous build, then install over it) is not scripted.
- **Device:** The layout check at font 1.0 and in landscape on every wizard and first-run screen: only the Record step at font 1.3 has a flow. On an emulator without a microphone the Record step may hide Try it.

### Slice 264: Dino: grown-ups reorder the shelf ([slice](slices/264-dino-gallery-reorder-mode.md))
- **Device:** The 6-book 1,2,5,3,4,6 example on a device (unit-tested only).

### Slice 262: Dino: adding a second language downloads the shelf books' audio in it ([slice](slices/262-dino-side-language-downloads-book-audio.md))
- **You:** iOS run of the shelf-audio card and runner (compiles in CI only).
- **Device:** Kill-and-resume and offline mid-download on a device.
- **You:** The mobile-data rule ("Download now" only) on a real phone.
- **Device:** The intro languages step path on a device.

### Slice 265: Dino: pre-made audio clips in Git LFS; book changes only publish to the store ([slice](slices/265-dino-audio-clips-in-git-lfs.md))
- **Device:** `git lfs pull --include` of one clip from a fresh clone of main downloads the real file (only pointers were read back, 2026-11-04). Keep `s246-audio` until it does.

### Slice 261: Dino: no starter books in the app; a first-run book picker ([slice](slices/261-dino-no-starters-first-run-book-picker.md))
- **Device:** Upgrade from the current release APK keeps the four books (the release signature differs from Debug).
- **You:** iPhone: the picker and first run.
- **Device:** The roughly 45 older flows were not all rerun (open-book, back-cover, premade-audio-plays, store-tags and grown-ups-button passed).
- **Device:** The covers row on the download wait and the intro caption were not checked by eye.

### Slice 260: Dino: tags on store books (Hot, Popular, Recommended, New) ([slice](slices/260-dino-store-tags.md))
- **Device:** Badge look at 130-200% system font size and the slim (sideways) store card with a tagged book (extra badge row vs title and cover centre).
- **You:** The Store_Tag_* labels in English and Farsi read by the user.
- **Device:** Badge text after an app-language switch with the store page open (review high 3, not confirmed).

### Slice 259: Leftovers from night 2026-11-03-2349 ([slice](slices/259-night-2026-11-03-2349-leftovers.md))
- **Device:** Item 1: the mid-book wait on a slow real phone (overlay when above 500 ms); the emulator measured 540 ms to first sound with no overlay.

### Slice 250: Dino usage: playback events and a session id new on every cold start ([slice](slices/250-dino-playback-events-and-session-id.md))
- **Device:** `seq` on a device (the Debug line has no `seq`; unit-tested only).

### Slice 249: Dino: "Report a problem" in the grown-ups' corner ([slice](slices/249-dino-report-a-problem.md))
- **Device:** The rolling file on a device (the device run read the lines from logcat).

### Slice 256: Dino: `dump-sentences` works for picture books (Good Enough) ([slice](slices/256-good-enough-sentence-dump.md))
- **Device:** The dump index matches the app's sentence index on a device during premade-audio playback (Slice 246 C).

### Slice 255: Dino: real animal recordings instead of voiced animal sound words ([slice](slices/255-dino-real-animal-sounds.md))
- **Device:** Farsi clip on a device (the device run's `lang=fa` launch may not have been applied to the running app).
- **You:** iPhone: animal clip playback.

### Slice 257: Dino: a quote after "said Mouse." is the Mouse's, not the Lion's ([slice](slices/257-lion-and-mouse-speaker.md))
- **Device:** On a device, Lion and the Mouse reads the Mouse's line ("I didn't mean to wake you up. ...") in the Mouse's voice (no device run: pure Core logic, covered by the unit tests).

### Slice 253: Final-loop leftovers, night 2026-11-01-2355 ([slice](slices/253-final-loop-leftovers-night-2026-11-01-2355.md))
- **Device:** Item 1: a forced "System UI isn't responding" dialog on the tablet AVD with the dialog guard, a flow passing with no brief text (not run).
- **Device:** Item 1: the dialog guard's restore after a lease loss (a killed run leaves `hide_error_dialogs=1`, cleared by the next `run-flows.ps1`; not exercised on a device).
- **Device:** Item 6: before/after seconds per slow flow (command in the slice file's Built block, item 6; not run).

### Slice 248: Dino iOS: leaving a book stops its read, and a new book never drives the old one's voice ([slice](slices/248-dino-stop-read-on-leave-ios.md))
- **You:** iOS (TestFlight build after the merge): play a book, tap Back: the voice stops. Open another book: its button shows Play; Play reads that book. Swipe back from a playing book: the voice stops.

### Slice 235: Dino on Windows, parts A and B ([slice](slices/235-dino-windows-window.md))
- **You:** Part B on a real PC: whether PreviewKeyDown gets keys with nothing focused, the Space hold with real key repeats, title bar colours and height, tooltips on a hovering mouse.
- **Device:** Why Right takes about 2 s after a read starts on the VM (speech start busy on the UI thread?), and whether a real PC has the same delay. 2026-11-13 VM run: did not reproduce (Right 446 ms, Space 460 ms); a real PC is still unchecked.
- **Device:** Esc on the language page / cast picker through the new back path; held Esc on Settings/Language/Cast/Record pops exactly one page; the gate hold ending on Alt-Tab; title bar colour after the intro and the gate close. 2026-11-13 VM run: Record Esc popped exactly one page; the language page, cast picker and Settings are not covered; title bar ink in the corner and back to cream confirmed, tone after the intro and after the gate close unchecked.
- **Device:** Tab then Enter on "Back to story".
- **You:** Opening at 1100 x 760 on a screen larger than 1024 x 768 (the VM clamps it; 2026-11-13: the VM screen is 1024 x 768, cannot run there).
- **Device:** 150% scaling and the unit of `PreferredMinimumWidth/Height`.
- **You:** Title bar height and colour by eye.

### Slice 234: Dino tablet held upright, parts A-C ([slice](slices/234-dino-tablet-portrait.md))
- **Device:** Part C: Record, Cast with the picker, the editor (keyboard down and up), Store and Backup against frames 142-146 by eye (no screenshots; no script reaches those screens with a screenshot step).
- **Device:** RecordFixedRows / EditorFixedRows on the tablet AVD.
- **Device:** Part C: Cast picker on a short window or with the keyboard.
- **Device:** Part B: cards, corner and Settings against frames 136-141 by eye (the flows take no screenshots).
- **You:** Part B: a drifting finger on the gate hold.
- **Device:** Part B: a short tablet window (cards cap and scroll).
- **Device:** Part A: stacked reading page against frames 134, 135 and 150 by eye (no screenshot taken).
- **Device:** Part A: shelf spacing and header against frame 133.
- **Device:** Part A: picture only, upright.
- **Device:** TalkBack order, parts A-C.

### Slice 246: Dino store books read with pre-made audio ([slice](slices/246-dino-store-books-premade-audio.md))
- **Device:** The piper-fa baseline render, on a PC that has the `piper-fa` pack and `sherpa_onnx` installed.
- **Device:** `build-audio` was run on real folders only by the pack run; the zips were not opened.
- **You:** iPhone: pre-made audio rate and playback, and a real store download fetching live packs.

### Slice 242: Round 4 leftovers, night 2026-11-01-0859 ([slice](slices/242-round-4-leftovers-night-2026-11-01-0859.md))
- **Device:** Item 6: the 34/26 dp phone insets (no log or tree value; screenshots only).
- **Device:** Item 6: the tablet screenshot against frame 127, by eye only.

### Slice 244: Dino iOS crashes ([slice](slices/244-dino-ios-crashes-2026-11-01.md))
- **You:** The iPhone 8 run (the user's): read Farsi Three Little Pigs, Back mid-sentence, re-read, background, 3 times.
- **Device:** The window size on a real phone.

### Slice 238: packs.yml defaults match the catalog ([slice](slices/238-packs-workflow-defaults-match-catalog.md))
- **Device:** What a lower version does on phones: Slice 220's record says a re-download is decided by `PackVersions.IsNewer` only, so a phone holding piper-nb 2 would most likely keep it and a fresh install would get 1; not checked on a device.

### Slice 236: phone runner gaps, night 2026-11-01-0859 ([slice](slices/236-phone-runner-gaps-night-2026-11-01-0859.md))
- **Device:** Item 6 with no lease held: `preflight.ps1 -Fix` then `-Restore` back to 0.
- **Device:** A mid-run `CHANGED:` stop with no record.
- **Device:** The `dumpsys platform_compat` override-line wording (`Get-CompatOverrideLines` matching).
- **Device:** The 30 s device-side restore cancel timeout warning ("did not finish in 30 s").

### Slice 245: sherpa-onnx one version ([slice](slices/245-sherpa-onnx-one-version.md))
- **Device:** Translation with managed onnxruntime 1.28.0 against native 1.28.2 on Android and Windows (scan and OCR dropped).

### Slice 241: Faster, quieter device tests ([slice](slices/241-device-test-speed.md))
- **Device:** Item 1, crash fail-fast: a flow list with a launch crash prints one FAIL with the crash lines and NOTRUN (naming the crash) for the rest in under one flow's time.
- **Device:** Item 3, timing: a cache-miss run's total is build time, not build plus boot (the `Cache miss` line itself passed on 2026-11-01; the time was not recorded).
- **You:** The iOS stall watcher on a device.
- **Device:** The `-Split` two-emulator path.

### Slice 239: Dino store book without an app build ([slice](slices/239-dino-store-book-without-app-build.md))
- **Device:** A translated title for a newly added store book on an installed app, with no app update.

### Slice 232 parts A-E: Dino landscape reading screen, shelf, Record, Cast, page text editor, intro, gate and first run ([slice](slices/232-dino-landscape-every-screen.md))
- **Device:** Part E: screenshots against frames 121, 122, 123, 130 (the flows take none), including check 3 (gate keys uneven in tall rows) and check 4 (long language names in 3 columns).
- **Device:** Part E: `first-run.yaml` on this build against main (the SIGSEGV; not proven unrelated).
- **You:** Part E: the gate sum step sideways (the hold is not scriptable).
- **You:** iPhone: Part E layouts.
- **Device:** Part E: TalkBack order.
- **Device:** Part D: screenshots against frames 117, 118, 124, 128 by eye.
- **Device:** Part D: TalkBack order in two panes.
- **Device:** Part D: the hosted pane's right edge inset on a notch phone.
- **Device:** Part B: the same book stays in view after rotating a shelf with more than 4 books (the emulator shelf has 4, all on screen).
- **Device:** Part B: tablet landscape and portrait on the tablet AVD.
- **Device:** Part B: notch inset doubling with `SafeAreaEdges` (high 10).
- **Device:** Part B: a 360 dp-tall phone, the card and Remove fit.
- **Device:** Part B: rotating with the sheet open during a Remove confirmation.
- **Device:** Part B: TalkBack order in the rail.
- **Device:** Part C: screenshots of each landscape state against frames 116, 119, 120, 129 (the flows take none; the user's eye).
- **Device:** Part C: a tablet with the keyboard up counts as a phone (short side under 600) and shows the thumbnail.
- **You:** iPhone: Part C landscape.
- **Device:** Part C: skipped lows: Cast rotation stops a playing sample, layout jump on focus, 3:4 picture column.
- **Device:** Tablet landscape (no tablet AVD): the controls row under the card and its left edge.
- **You:** iPhone landscape.
- **Device:** VoiceOver / TalkBack order in landscape.
- **You:** Picture-only chip placement against frame 114 (the user's eye).
- **Device:** A window 600-700 dp on the short side choosing rail versus row.
- **Device:** Portrait reading unchanged on iOS, Windows and tablets after `TransportRow`'s base class changed to `StackLayout` (shown only by two Android emulator flows).
- **You:** Phone side margins (about 14 dp, mock 34/26) and tablet frame 127's title against the mock (the user's eye).

### Slice 225: Device-test tooling leftovers, night 2026-10-31-1301 ([slice](slices/225-device-tooling-leftovers-night-2026-10-31-1301.md))
- **Device:** Paused-notification checks on the S21 (needs `run-checks.ps1 -Build -App rtst` first; cache miss). Passed 2026-11-13: `paused-notification-network` in 57 s (title "Download paused. Tap to finish.", text "English voice (124 MB) · 0% done"; tap resumed `PACKDL start pack=supertonic-3 offset=990290`; notice cleared). Still open: the other text variants, the repost flags check (Slice 372).
- **Device:** `stay_on` restore when the user changed the value (self-tested only).
- **Device:** Item 16 in the Debug app: the `[BookUpdate] ... forced entry dropped, recorded version put back` and `forced-apply mark expired` lines.
- **Device:** Item 21: a flow reading a TalkBack focus event (TalkBack on and off passed; the focus event was not read).

### Slice 230: Dino: say plainly that recordings stay on the phone, and restore brings them back ([slice](slices/230-dino-recordings-copy-and-restore-merge.md))
- **You:** iOS simulator or TestFlight: back up, restore, recordings come back.
- **You:** VoiceOver on the new Record/Backup lines.
- **You:** iPhone: layouts of the new Record/Backup lines.
- **You:** Read the new Record/Backup strings in Farsi (right to left).

### Slice 221: Device-test tooling leftovers from the 2026-10-30-2100 night, round 1 ([slice](slices/221-device-test-tooling-leftovers-night-1.md))
- **Device:** Item 6 on the real phone: two timed runs with the same owner, the second exits 4.
- **Device:** Item 3 seen in a device-test run log.
- **Device:** `adb uninstall` stderr under Windows PowerShell 5.1 with `ErrorActionPreference` Stop (`Remove-StaleDevPackages`, `tools/emulator/common.ps1:151`) could abort the install instead of warning.

### Slice 229: Dino Done/Continue button on every grown-up page ([slice](slices/229-dino-subpage-done-button.md))
- **Device:** Page editor header over the crop stage (44 dp pill vs 38).

### Slice 224: Phone runner build-cache gaps ([slice](slices/224-phone-runner-build-cache-gaps.md))
- **Device:** A real phone run of the runner with a dead-builder status file, and with `-NoBuild` on a warm cache (no device run this wave; host tooling only).

### Slice 237: Dino back cover (credits page) ([slice](slices/237-dino-back-cover-credits.md))
- **Device:** Landscape layout of the back cover.
- **Device:** Farsi right-to-left back cover and Persian digits.
- **Device:** Old installed app reading the page aloud without breaking.
- **You:** iPhone: the back cover page.
- **Device:** Credit-line wrapping in long languages on a narrow phone (estimated, not measured).

### Slice 233: paused notice same on Android and iOS ([slice](slices/233-paused-notice-same-on-android-and-ios.md))
- **Device:** A real network loss: the device run used the Debug-only `drop-pack-network` hook, not a real loss.

### Slice 228: Dino grown-ups' corner clarity ([slice](slices/228-dino-grownups-corner-clarity.md))
- **Device:** Corner tiles and title truncation on a small phone and on a tablet.
- **You:** The Farsi strings of the grown-ups' corner, read by the user.
- **Device:** The intro read's return being cancelled by Play/pause, skip or a page turn (the emulator recheck tapped the page surface, which is not one of those).
- **Device:** The 48 dp close button on a device (changed after the last emulator run; check pass only).

### Slice 220: Kokoro silent short lines on arm64 ([slice](slices/220-kokoro-silent-short-lines-arm64.md))
- **Device:** The 68-read Slice 210 set.
- **Device:** A fresh install's rebuilt non-kokoro packs (rebuilt in packs-11 at the same versions, no device run).

### Slice 231: Dino Record page shows the page's words ([slice](slices/231-dino-record-page-shows-words.md))
- **Device:** An RTL/Farsi book on the Record page.

### Slice 226: Dino Settings rows and page text ([slice](slices/226-dino-settings-rows-review-leftovers.md))
- **Device:** TalkBack / VoiceOver hearing each reading page once.
- **You:** iOS: a settings row with the button text left null keeps its height and look, and `settings-reading-speed-row.yaml` on iOS needs an in-app back step instead of `- back` ([slice](slices/226-dino-settings-rows-review-leftovers.md)).

### Slice 217: rtst forced update ([slice](slices/217-rtst-forced-update.md))
- **Device:** Slice 217 (PR #640): Windows forced and soft update on the VM (`rtst-update-forced` / `rtst-update-soft`); store button tap on the phone; iOS compile in CI and TestFlight hand check ([slice](slices/217-rtst-forced-update.md)). Harness gap (2026-11-13 VM run): the `rtst-update-forced` / `rtst-update-soft` VM checks do not exist and the Release MSIX has no test hooks.

### Slice 216: phone tooling leftovers ([slice](slices/216-phone-tooling-leftovers.md))
- **Device:** Slice 216 (PR #637): item 11 cold-start rerun and the numbers in Slices 185 and 214; path-length estimates unmeasured; `Get-PhoneStamp` not device-run ([slice](slices/216-phone-tooling-leftovers.md)).

### Slice 215: device guard gaps ([slice](slices/215-device-guard-gaps.md))
- **Device:** Slice 215 (PR #639): site image retake (VM sample read never leaves GeneratingAudio with the copied pack; `site/img/control-panel.webp` not replaced; watermark crop); `sentence-highlight` first-run failure after a fresh install; `test-activity.ps1 -DeviceKillCheck` script path not run ([slice](slices/215-device-guard-gaps.md)).

### Slice 214: rtst phone cold first audio ([slice](slices/214-rtst-phone-cold-first-audio.md))
- **Device:** Slice 214 (PR #647): piece 2 timing against piece 1 at 1x and 2x (run `tools/phone/audio-timing.ps1 -App rtst -Aot` on the S21; it flags gaps); a truly cold run after a fresh install or reboot; iOS TestFlight first-audio timing by hand ([slice](slices/214-rtst-phone-cold-first-audio.md)).

### Slice 213: phone check runner ([slice](slices/213-phone-check-runner.md))
- **Device:** Slice 213 (PR #610): a cold-cache run where the AOT build fails or stalls (the FAIL path); a run while another session holds the lease; the ... [full text](notes/2026-10-30-outstanding-detail.md#o017) ([slice](slices/213-phone-check-runner.md))

### Slice 211: play before detection waits for it ([slice](slices/211-play-before-detection-waits-for-it.md))
- **Device:** Slice 211 (PR #633): S21 rerun of the Slice 139 race (tap ~0.4 s after typing, against the 1456 ms baseline); iOS TestFlight hand check; back-during-wait scripted check not run; `translation-wait-cancel` not run; Debug iOS compile of the final code (incl. the hold-detection hook) not done ([slice](slices/211-play-before-detection-waits-for-it.md)).

### Slice 208: paused pack downloads tell you and finish ([slice](slices/208-paused-pack-downloads-tell-you-and-finish.md))
- **Device:** Slice 208 (PR #646) partly verified on the S21 on 2026-11-13 (`paused-notification-network` passed in 57 s: title "Download paused. Tap to finish.", text "English voice (124 MB) · 0% done", tap resumed `PACKDL start pack=supertonic-3 offset=990290`, notice cleared). Still open: the other notification text variants, the stale partial resume/delete, the cold-run marker, Dino first download, iOS and the `device_config` time-limit variant (never passed on an emulator, not run). Earlier list: the S21 Verification list (paused notice within ~35 s and tap resumes; notification text; stale partial resumes or is deleted); Dino storyteller first download shows no ask sheet and grown-up Settings shows it once (emulator); iOS TestFlight hand check; stale-partial-launch first cold run marker not seen within 40 s (not diagnosed); the `paused-notification` check itself was reworked by Slice 233 (Debug hook `drop-pack-network`, no netpolicy) and passed on the S21 on 2026-11-01, so its earlier netpolicy and detached-restore doubts no longer apply. [208-paused-pack-downloads-tell-you-and-finish.md](slices/208-paused-pack-downloads-tell-you-and-finish.md)

### Slice 206: phone run scripts ([slice](slices/206-phone-run-scripts.md))
- **Device:** Slice 206 (PR #594): `preflight.ps1` reading a PIN or pattern lock as NO-GO; `reset-dev-app.ps1 -Level reinstall`; Dino resets (`-App dino`); ... [full text](notes/2026-10-30-outstanding-detail.md#o001) ([slice](slices/206-phone-run-scripts.md))

### Slice 204: cancelled voice pack stays cancelled ([slice](slices/204-cancelled-voice-pack-stays-cancelled.md))
- **You:** iPhone: Dino cancelled voice pack stays cancelled; read the Stopped strings in English and Farsi.

### Slice 203: Dino on the Microsoft Store: turn on automatic submissions and link it ([slice](slices/203-dino-microsoft-store-live.md))
- **Device:** Slice 203 (PR #578): the first automatic Dino Store submission (needs the next Dino release on `main`).

### Slice 202: reader language change crash ([slice](slices/202-reader-language-change-crash.md))
- **You:** iPhone: change the reader language; no crash.

### Slice 201: norwegian pack crash ([slice](slices/201-norwegian-pack-crash.md))
- **Device:** Run the Maestro flow .maestro/rtst/norsk-pack-reads.yaml.

### Slice 200: Check the live usage data after the store rollouts ([slice](slices/200-live-usage-data-checks.md))
- **Device:** Slice 200 (live data checks): re-run the report after more real traffic (under 10 events per group everywhere on 2026-10-30); no rows yet for voice ... [full text](notes/2026-10-30-outstanding-detail.md#o013)

### Slice 197: dino download now tap dropped ([slice](slices/197-dino-download-now-tap-dropped.md))
- **Device:** Slice 197 (PR #583): no stale "Downloading" status after the download ends; notification Cancel mid-download then nothing restarts; iOS TestFlight ... [full text](notes/2026-10-30-outstanding-detail.md#o016) ([slice](slices/197-dino-download-now-tap-dropped.md))

### Slice 196: dino default view per book ([slice](slices/196-dino-default-view-per-book.md))
- **Device:** Slice 196 (PR #575): the flow `default-view-per-book.yaml` not yet shown failing on old main; Good Enough pages 8 and 10 by eye; landscape layout; ... [full text](notes/2026-10-30-outstanding-detail.md#o008) ([slice](slices/196-dino-default-view-per-book.md))

### Slice 192: dino store credits in every language ([slice](slices/192-dino-store-credits-in-every-language.md))
- **Device:** Slice 192 (PR #582): live `meta.json` refreshed (all 15 books in `/api/stories` carry `creditsByLanguage`, checked 2026-10-30); ... [full text](notes/2026-10-30-outstanding-detail.md#o012) ([slice](slices/192-dino-store-credits-in-every-language.md))

### Slice 190: rtst phone sentence highlight ([slice](slices/190-rtst-phone-sentence-highlight.md))
- **Device:** Slice 190 (PR #573): on-screen sentence highlight PASS on the S21 (2026-10-30); pause/resume mid-chunk; iOS TestFlight read of a 3-sentence ... [full text](notes/2026-10-30-outstanding-detail.md#o007) ([slice](slices/190-rtst-phone-sentence-highlight.md))

### Slice 183: pack downloads keep going when locked ([slice](slices/183-pack-downloads-keep-going-when-locked.md))
- **You:** Slice 183 (PR #549): iOS background session on a device (TestFlight); Dino storyteller first-download ask deferral; the dataSync timeout's silent ... [full text](notes/2026-10-30-outstanding-detail.md#o018) ([slice](slices/183-pack-downloads-keep-going-when-locked.md))
- **You:** Slice 183: native review of the new strings (13 machine drafts per app).

### Slice 182: pack download resumes by itself ([slice](slices/182-pack-download-resumes-by-itself.md))
- **You:** "Waiting for Wi-Fi" on a real cellular-only phone (OCR resume dropped).

### Slice 181: dino books update from store ([slice](slices/181-dino-books-update-from-store.md))
- **Device:** Slice 181 (PR #543): a recording carried over a swap; a store-downloaded book with edited text getting an update; a crash during the apply itself ... [full text](notes/2026-10-30-outstanding-detail.md#o005) ([slice](slices/181-dino-books-update-from-store.md))

### Slice 180: faster device tests ([slice](slices/180-faster-device-tests.md))
- **Device:** Slice 180 (PR #536, #538): the System UI "isn't responding" dismissal in `run-flows.ps1` never triggered (read only); one first-run flow flake with ... [full text](notes/2026-10-30-outstanding-detail.md#o022) ([slice](slices/180-faster-device-tests.md))

### Slice 179: no robot or own machine counts ([slice](slices/179-no-robot-or-own-machine-counts.md))
- **Device:** Slice 179 (PR #542): Windows VM run with a CI MSIX; iOS `sandboxReceipt` on TestFlight; Windows Store-signature check on a Store build; Release ... [full text](notes/2026-10-30-outstanding-detail.md#o019) ([slice](slices/179-no-robot-or-own-machine-counts.md))

### Slice 176: whitespace read crash ([slice](slices/176-whitespace-read-crash.md))
- **You:** iPhone: reading whitespace-only text does not crash.

### Slice 173: dino kid story languages ([slice](slices/173-dino-kid-story-languages.md))
- **You:** Slice 173 (PR #525): mobile-data "Wait for Wi-Fi / Download now" prompt not seen (emulator has Wi-Fi); a persisted no-pack language ... [full text](notes/2026-10-30-outstanding-detail.md#o021) ([slice](slices/173-dino-kid-story-languages.md))

### Slice 171: dino text card shows whole page ([slice](slices/171-dino-text-card-shows-whole-page.md))
- **You:** iPhone: the line reader and the card's scroll for English and Farsi text.

### Slice 170: tray panel controls row fits ([slice](slices/170-tray-panel-controls-row-fits.md))
- **Device:** Device check at 100% and 150% scale, idle and reading, in English (all five toggles and the wait flyout fully visible).

### Slice 167: no test device counts ([slice](slices/167-no-test-device-counts.md))
- **Device:** Slice 167 (PR #497): Release build on an emulator (crashed at startup in MAUI before any usage code, x64 only); emulator and Test Lab rules on a ... [full text](notes/2026-10-30-outstanding-detail.md#o029) ([slice](slices/167-no-test-device-counts.md))

### Slice 166: usage device context ([slice](slices/166-usage-device-context.md))
- **Device:** Slice 166 (PR #492): Windows heads only compiled (no desktop run of the RTST tray or Dino Windows head); iOS compiled only (no simulator); first ... [full text](notes/2026-10-30-outstanding-detail.md#o028) ([slice](slices/166-usage-device-context.md))

### Slice 161: warm headless x64 emulator ([slice](slices/161-warm-headless-x64-emulator.md))
- **Device:** Slice 161 (PR #485): arm64-vs-x64 launch-and-read timing not compared; version-mismatch guard, bump guard and concurrent-build paths reviewed only, ... [full text](notes/2026-10-30-outstanding-detail.md#o025) ([slice](slices/161-warm-headless-x64-emulator.md))

### Slice 160: ios translation parity ([slice](slices/160-ios-translation-parity.md))
- **You:** Slice 160 (PR #486): TestFlight translate English to Farsi on iOS, model download progress and Cancel, offline translate after download, Dino typed ... [full text](notes/2026-10-30-outstanding-detail.md#o024) ([slice](slices/160-ios-translation-parity.md))

### Slice 159: dino everything in my language ([slice](slices/159-dino-everything-in-my-language.md))
- **You:** Read the ShippedTitles and the 7 new resx keys in English and Farsi.

### Slice 157: Dino: book content and the book builder out of the test project ([slice](slices/157-dino-book-content-out-of-tests.md))
- **Device:** Slice 157 (book content out of the tests, PR #475): first main run of `publish-store` with the new builder; `ShippedTitles` edits do not reach live ... [full text](notes/2026-10-30-outstanding-detail.md#o046)

### Slice 155: dino first run intro wizard ([slice](slices/155-dino-first-run-intro-wizard.md))
- **Device:** Slice 155: offline store step; microphone prompt on the Record step; Windows variant (title, Picture badge); iOS modal presentation; large-font ... [full text](notes/2026-10-30-outstanding-detail.md#o073) ([slice](slices/155-dino-first-run-intro-wizard.md))

### Slice 151: dino three view modes ([slice](slices/151-dino-three-view-modes.md))
- **Device:** Night-theme segment colours unchanged on the Windows head and iPhone (photo import and native read dropped).

### Slice 148: phone wait followups ([slice](slices/148-phone-wait-followups.md))
- **Device:** Windows stall overlay on a packaged run (native read dropped).

### Slice 142: rtst windows early start ([slice](slices/142-rtst-windows-early-start.md))
- **Device:** Slice 142: packaged desktop run done 2026-10-26 on a CPU-saturated host; re-check first-audio timing on a quiet machine; prompt tray exit not verified ([slice](slices/142-rtst-windows-early-start.md)).

### Slice 141: rtst windows wait indicator ([slice](slices/141-rtst-windows-wait-indicator.md))
- **Device:** The panel ring (Play becomes the ring with Cancel) re-check on the desktop (native read dropped).

### Slice 139: rtst phone early start ([slice](slices/139-rtst-phone-early-start.md))
- **Device:** Slice 139: emulator 2026-10-26 (swapping AVD): early work finished gives first audio in 396-749 ms with a cache hit on sentence 1, but in-flight ... [full text](notes/2026-10-30-outstanding-detail.md#o040) ([slice](slices/139-rtst-phone-early-start.md))

### Slice 138: rtst phone wait indicator ([slice](slices/138-rtst-phone-wait-indicator.md))
- **Device:** Slice 138: emulator PASS 2026-10-26; sub-500 ms play start PASS on the S21 (61-140 ms); not seen: an ML Kit progress % (Slice 139 downloads the ... [full text](notes/2026-10-30-outstanding-detail.md#o035) ([slice](slices/138-rtst-phone-wait-indicator.md))

### Slice 133: dino anonymous usage counts ([slice](slices/133-dino-anonymous-usage-counts.md))
- **Device:** Slice 133: telemetry on and rows arrive in `dino_usage` since 2026-10-27; settings off sends nothing still to check on a device; no rate limit or ... [full text](notes/2026-10-30-outstanding-detail.md#o066) ([slice](slices/133-dino-anonymous-usage-counts.md))

### Slice 126: dino keep screen awake ([slice](slices/126-dino-keep-screen-awake.md))
- **Device:** Slice 126: emulator PASS (screen stays on while playing, sleeps after pause and at the end); screen awake on a real phone PASS while playing (end ... [full text](notes/2026-10-30-outstanding-detail.md#o063) ([slice](slices/126-dino-keep-screen-awake.md))

### Slice 122: dino text panel 30 and switch ([slice](slices/122-dino-text-panel-30-and-switch.md))
- **You:** On the user's phone: pigs upright and sideways (panel at most 30%); PDF import defaults the switch off; a wide picture sideways.

### Slice 121: dino illustrate store stories ([slice](slices/121-dino-illustrate-store-stories.md))
- **You:** Slice 121: check the illustrated books on a phone (pictures, text panel, voices) ([slice](slices/121-dino-illustrate-store-stories.md)).

### Slice 112: dino store age author filters ([slice](slices/112-dino-store-age-author-filters.md))
- **You:** Read the new store age/author filter strings in English and Farsi.

### Slice 111: dino parent recordings ([slice](slices/111-dino-parent-recordings.md))
- **Device:** Record/replay on the S21, iPhone and Windows head; mic permission denial; backup round trip.

### Slice 99: rtst mobile minimal main page ([slice](slices/99-rtst-mobile-minimal-main-page.md))
- **Device:** Slice 99: full TalkBack walk-through; iOS device run; Settings voice name refresh PASS on the S21 ([S21 night runs](notes/2026-10-29-s21-night-runs.md)) ([slice](slices/99-rtst-mobile-minimal-main-page.md)).

### Slice 96: dino layout never mirrors ([slice](slices/96-dino-layout-never-mirrors.md))
- **Device:** Emulator in Farsi (gallery, gate, wizard, store, backup, reader, swipe direction, highlight on RTL lines); Windows head gallery and reader.

### Slices 43-95 ([moved from notes/2026-10-25-outstanding-older.md](notes/2026-10-25-outstanding-older.md))
- **Device:** Slice 46: the Windows tray panel's progress ring in the running app (auto-read, copy, hotkey; clears on supersede/failure/completion); follow-up: a reader-level generating signal on `ISpeechReader` for Android ([slice](docs/plan/slices/46-play-loading-state.md)).
- **You:** iPhone from TestFlight: the iOS app's runtime (AVSpeech reading, file picker).
- **Device:** Slice 47: a voice switch (miss, then hit on switching back), head-start benefit and the 100 MB cap on a real phone; Slice 56 wires the cache on Windows. ([slice](docs/plan/slices/47-head-start-sentence-cache.md))
- **You:** iPhone TestFlight pass: neural voice after the pack, skip, interruptions, lock screen, txt/pdf/docx open.
- **Device:** Offline translation with a cached model, offline first use (alert, original read), a 5,000-word text, the translate card in RTL.
- **Device:** The TalkBack path of the gate; edit, page reorder and delete on the emulator.
- **Device:** Slice 62: the wander pill and Play halo during a read, the gate pass to Manage, the bedtime theme and RTL (waits on Slice 66), long-story pagination on a phone, the tablet layout and "Page n of m"; follow-up: share the read-start grace state machine with RTST. ([slice](docs/plan/slices/62-reading-screen-modes.md))
- **Device:** Slice 56: system voice then neural handover on a real first run (download too fast to see; no log line names the engine); offline first launch; kill and resume mid-download; MSIX size and no `VoiceModel\`; the `internetClient` Store certification dry run; first-audio comparison for prepared text; follow-ups: pack/engine log lines, deduplicate with `Mobile.Shared` ([slice](docs/plan/slices/56-windows-packs-language-headstart.md)).
- **Device:** Farsi UI mirroring on a device.
- **You:** Slice 74: after the deploy, `https://dinoreader.sirous.uk` and `/privacy` load with the security headers; `/download/android` serves the Dino APK once a Dino release publishes; paste the privacy URL into Play Console's Dino Reader field; Play Families may want an email contact instead of the GitHub issues link ([slice](docs/plan/slices/74-dino-reader-site.md)).
- **Device:** Slice 75: desktop acceptance of toggle, pack download and streamed translated read (session was locked); `packs-3` contents after the workflow run; Android/iOS still find their packs in `packs-3` ([slice](docs/plan/slices/75-windows-translation-engine.md)).
- **You:** Play Console "DEX obfuscation" metric after the next release (target >= 25%).
- **Device:** Slice 73: Android 14 (API 34) inset path; landscape on both apps; RTST `LanguagePage` on first run; Dino `PageTextPage` with the keyboard up; Play's deprecated-API warning after the next release; follow-up: Dino gallery/Manage tiles re-lay out oddly after toggling night mode ([slice](docs/plan/slices/73-edge-to-edge-safe-areas.md)).
- **Device:** Slice 77: page-2 cover pick on a multi-page story; a photo cover surviving an app restart; the Windows live theme switch (`UISettings.ColorValuesChanged`) ([slice](docs/plan/slices/77-dino-covers-and-light-theme.md)).
- **Device:** Slice 80: Windows Dino nested taps and window recreate; TalkBack reading "Edit <title>" and "Delete <title>"; the 10-min `AwayTracker.ExternalActivityCap` timer (no test; lives in `DinoApp`) ([slice](docs/plan/slices/80-grownup-gallery-overlay.md)).
- **Device:** Slice 81: header "tap to retry" label (state flashes Resolving then Offline, no code defect found); a reinstalled pack restoring the chosen voice; all of it on a real phone ([slice](docs/plan/slices/81-language-change-starts-voice-download.md)).
- **You:** Slice 82: iOS voice selection on a device (CI compile only); Windows does not switch the reading language (by design for now). User decided 2026-10-22: untranslated text is read in its own language (Slice 90). ([slice](docs/plan/slices/82-phone-voice-reads-the-texts-language.md))
- **You:** Typing Farsi on a real keyboard; Arabic content in the editor; the first-run Language page in Farsi.
- **You:** iPhone: a different-language read uses the phone voice in that language; the "no voice" message for a language with neither a natural pack nor a phone voice.
- **Device:** Slice 92: "Try again" after going offline and the gallery refresh on `StoreClient.LibraryChanged`, on a device; the production `/api/stories*` routes after deploy; the store is empty until curated stories are put in `dinoreader/stories/` in the bucket ([slice](docs/plan/slices/92-dino-story-store.md)).
- **Device:** The language rebuild after first run (DinoApp.RebuildRoot) on a device; Windows backup save and restore; iPhone.
