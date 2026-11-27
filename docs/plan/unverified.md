# Not yet tried on a device

Every slice's open Not yet verified checks, newest slice first. A line leaves when the check passes on a device or the user confirms it; plan.md's Outstanding work keeps only the count. The user (2026-11-01 07:31): "I dont have time to check your shit on ios"; the lines stay here until a device run or the user clears them.

**Only what can be tested stays here** (the user, 2026-11-08: "keep only whatever I can test"). Each line starts with who tests it: **You** (the user: the iPhone, the S21 by hand, the Echo, the Sonos, an English or Farsi read, a decision or account only you hold) or **Device** (Claude, on the Android emulator or tablet AVD, the S21 by script, or the Windows VM). Checks nobody here can test were removed; behaviours only real use shows are watched in production data instead; checks that need the user's ear are listening-page slices 330-343. All three lists are in the [record](notes/2026-11-08-unverified-triage.md). Earlier on 2026-11-08: triaged 2026-11-08: 106 lines removed as replaced, already done, proven in production or not a check; the real gaps moved to [Slice 316](slices/316-unverified-triage-gaps.md); the Slice 43-95 lines that were still open moved here from [outstanding-older](notes/2026-10-25-outstanding-older.md) ([record](notes/2026-11-08-unverified-triage.md)). The 2026-11-08 device run (emulator, S21, VM) passed 49 more and the user's iPhone session 2 more; same record.

## Read The Stupid Text

### Slice 342: Farsi ezafe override ([slice](slices/342-farsi-voice-pack.md), [343](slices/343-four-farsi-voices.md))
- [owner] **You:** second listen to `D:\ReadTheStupidText-worktrees\reviews\s342-ezafe\fa-ganji_adabi-1-after.wav`: it should say "hale shoma" (and the other 11 WAVs there, four voices x three lines, should sound as before).
- [owner] **You (re-listen, comma fix built in Slice 343, PR #1422):** an Arabic comma is now put at text-prep time after «صبح» before «یک» (Ganji, Gyro), after «روز» before «صبح» (Gyro) and after «دفتر» before «خرید» when «خرید» ends the clause (Gyro). Owner-reported in the third listen, one voice per pair: «صبح، یک» heard ok on Ganji only, «روز، صبح» on Gyro only, «دفتر، خرید» on Gyro only (the zero-width non-joiner also fixed the first and third; Ganji Adabi and Mana were right without a comma); the pairs are not heard on the other voices. Listen that "sobh yek", "rooz sobh" and "daftar kharid" no longer get an extra ezafe, and that other sentences read as before. The chained «روز صبح یک» (two commas, in no book yet) is unheard: say if it sounds choppy.
- [phone] **Device:** the ezafe on a device (Android, iOS, Windows) needs a flow that installs `piper-fa` and synthesises a Farsi page with no audio pack; the 2026-11-13 device run did not reach it (no RTST Farsi read flow; Dino flows play premade clips).
- [phone] **Device (follow-up bug):** the Dino flows `farsi-speaker-first-join` and `premade-audio-past-roar` fail on main, unrelated to the ezafe (stuck on onboarding after skip-first-run lang=fa; a real fa audio pack replaced `fixture-13.m4a` after `pm clear`).

### Slice 373: RTST voice rows RT1 and RT2 ([slice](slices/373-rtst-voice-rows-rt1-rt2.md))
- [phone] VM check `rtst-language-rows` not run: free memory 7 GB was under the 8 GB the VM needs, so the VM was not started.
- [phone] The Windows failed row (not downloaded line, error line, Retry): the VM has no forced-failure hook on Windows.
- [phone] Layout check: no `layout-check ok` line was produced.
- [owner] iOS on a device (iOS only compiled).
- [phone] A real failed download on the S21.

### Slice 341: Listening page: Farsi short words never dropped ([slice](slices/341-farsi-short-words-never-dropped.md))
- [phone] **Device:** a phone run of short Farsi lines in Read The Stupid Text (mana with the ganji rescue, e.g. «سلام.», «آه.») is not done.

### Slice 345: RTST: the Debug stall hook must block the reader's first-chunk wait ([slice](slices/345-rtst-stall-hook-blocks-reader.md))
- [phone] **Device:** `stall-return-overlay` on iOS (**You**, no Mac; the iOS reader edit is not compiled or run) and on the Windows head (VM); only the Android emulator ran, twice, 2026-11-09.

### Slice 314: Restore the stall overlay when the person returns to the reader ([slice](slices/314-reader-stall-overlay-after-return.md))
- [phone] **Device:** Start a stalled read on Android, open Settings, come back: overlay and Cancel shown. (live check 2026-11-27: failed, follow-up in Slice 345)
- [owner] **You:** The iOS stall watcher on device.

### Slice 304: Slice 202 re-review and the InitWait order test ([slice](slices/304-slice-202-rereview-and-initwait-test.md))
- [phone] **Device:** Importing the same text keeps the language (device).
- [phone] **Device:** Language-change rebuild shows a loading state when it takes over 500 ms (review 9; caller in Settings/AppShell, not read by this slice).

### Slice 217: rtst forced update ([slice](slices/217-rtst-forced-update.md))
- [phone] **Device:** Slice 217 (PR #640): Windows forced and soft update on the VM (`rtst-update-forced` / `rtst-update-soft`); store button tap on the phone; iOS compile in CI and TestFlight hand check ([slice](slices/217-rtst-forced-update.md)). Harness gap (2026-11-13 VM run): the `rtst-update-forced` / `rtst-update-soft` VM checks do not exist and the Release MSIX has no test hooks.

### Slice 214: rtst phone cold first audio ([slice](slices/214-rtst-phone-cold-first-audio.md))
- [phone] **Device:** Slice 214 (PR #647): piece 2 timing against piece 1 at 1x and 2x (run `tools/phone/audio-timing.ps1 -App rtst -Aot` on the S21; it flags gaps); a truly cold run after a fresh install or reboot; iOS TestFlight first-audio timing by hand ([slice](slices/214-rtst-phone-cold-first-audio.md)).

### Slice 211: play before detection waits for it ([slice](slices/211-play-before-detection-waits-for-it.md))
- [phone] **Device:** Slice 211 (PR #633): S21 rerun of the Slice 139 race (tap ~0.4 s after typing, against the 1456 ms baseline); iOS TestFlight hand check; back-during-wait scripted check not run; `translation-wait-cancel` not run; Debug iOS compile of the final code (incl. the hold-detection hook) not done ([slice](slices/211-play-before-detection-waits-for-it.md)).

### Slice 202: reader language change crash ([slice](slices/202-reader-language-change-crash.md))
- [owner] **You:** iPhone: change the reader language; no crash.

### Slice 201: norwegian pack crash ([slice](slices/201-norwegian-pack-crash.md))

### Slice 190: rtst phone sentence highlight ([slice](slices/190-rtst-phone-sentence-highlight.md))
- [phone] **Device:** Slice 190 (PR #573): on-screen sentence highlight PASS on the S21 (2026-10-30); pause/resume mid-chunk; iOS TestFlight read of a 3-sentence ... [full text](notes/2026-10-30-outstanding-detail.md#o007) ([slice](slices/190-rtst-phone-sentence-highlight.md))

### Slice 176: whitespace read crash ([slice](slices/176-whitespace-read-crash.md))
- [owner] **You:** iPhone: reading whitespace-only text does not crash.

### Slice 170: tray panel controls row fits ([slice](slices/170-tray-panel-controls-row-fits.md))
- [phone] **Device:** Device check at 100% and 150% scale, idle and reading, in English (all five toggles and the wait flyout fully visible).

### Slice 160: ios translation parity ([slice](slices/160-ios-translation-parity.md))
- [owner] **You:** Slice 160 (PR #486): TestFlight translate English to Farsi on iOS, model download progress and Cancel, offline translate after download, Dino typed ... [full text](notes/2026-10-30-outstanding-detail.md#o024) ([slice](slices/160-ios-translation-parity.md))

### Slice 142: rtst windows early start ([slice](slices/142-rtst-windows-early-start.md))
- [phone] **Device:** Slice 142: packaged desktop run done 2026-10-26 on a CPU-saturated host; re-check first-audio timing on a quiet machine; prompt tray exit not verified ([slice](slices/142-rtst-windows-early-start.md)).

### Slice 141: rtst windows wait indicator ([slice](slices/141-rtst-windows-wait-indicator.md))
- [phone] **Device:** The panel ring (Play becomes the ring with Cancel) re-check on the desktop (native read dropped).

### Slice 139: rtst phone early start ([slice](slices/139-rtst-phone-early-start.md))
- [phone] **Device:** Slice 139: emulator 2026-10-26 (swapping AVD): early work finished gives first audio in 396-749 ms with a cache hit on sentence 1, but in-flight ... [full text](notes/2026-10-30-outstanding-detail.md#o040) ([slice](slices/139-rtst-phone-early-start.md))

### Slice 138: rtst phone wait indicator ([slice](slices/138-rtst-phone-wait-indicator.md))
- [phone] **Device:** Slice 138: emulator PASS 2026-10-26; sub-500 ms play start PASS on the S21 (61-140 ms); not seen: an ML Kit progress % (Slice 139 downloads the ... [full text](notes/2026-10-30-outstanding-detail.md#o035) ([slice](slices/138-rtst-phone-wait-indicator.md))

### Slice 99: rtst mobile minimal main page ([slice](slices/99-rtst-mobile-minimal-main-page.md))
- [phone] **Device:** Slice 99: full TalkBack walk-through; iOS device run; Settings voice name refresh PASS on the S21 ([S21 night runs](notes/2026-10-29-s21-night-runs.md)) ([slice](slices/99-rtst-mobile-minimal-main-page.md)).

## Dino Reader

### Slice 458: Dino: store server notifications ([slice](slices/458-dino-store-server-notifications.md))
- [cloud] Apple trial-to-paid detection (`isTrialConversion`: previous period at most 8 days) and the Google trial/conversion heuristic were built on synthetic fixtures; confirm on the first real notifications after item 4 (console wiring).

### Slice 424: Dino: the Android 1.136.0 unclean exit ([slice](slices/424-dino-android-1136-unclean-exit.md))
- [phone] Item 1: the walk on the S21 with the Play release build (first-run pack downloads, then Settings); the flow `first-run-to-settings` itself was written and run on the emulator, but the real pack downloads are not scripted. (live check 2026-11-27: failed, follow-up in Slice 424)
- [cloud] Item 3 iOS: the MetricKit exit-count path and the iOS simulator kill check on the CI Mac (`dinoreader-ios-sim.yml`); both heads compile, nothing ran on iOS. (live check 2026-11-27: unreachable: no Claude Code cloud session and no iOS simulator run in this unattended run)
- [phone] Item 6: a second distinct death on the same tail gets its own report.
- [session] After release: read a real `unclean_exit` with `tools/crashes/report.mjs -App dino` (the exit reason, session and last page on a real report). (live check 2026-11-27: unreachable: the release went live at 22:32 UTC tonight, so no real traffic from it exists yet)

### Slice 460: Dino Windows: the store works at first run, or recovers on its own ([slice](slices/460-dino-windows-first-run-store-recovery.md))
- [session] Item 1, the Windows VM `AppTrace` read at first run: not run, the VM needs 8 GB free and this wave had 6.8, 4.5 and 7.9 GB. Compare the cached origin/main package `8bbbca166301` with the new one `9fc20fa691d1`, and write down which call fails, its error and whether the window or Store-context ordering is the trigger.
- [session] Windows VM fresh-install first run with the network cut for 5 s and restored: first run finishes with no manual Try again.
- [cloud] iOS simulator run of the same first-run and offline checks in `dinoreader-ios-sim.yml`. (live check 2026-11-27: unreachable: no Claude Code cloud session and no iOS simulator run in this unattended run)
- [phone] On the S21 a first run shows no `store_failed` row from the purchase read (`BookPurchases.ReportReadProblems`); the emulator's rows came from having no Play Billing, which is likely but not proven. (live check 2026-11-27: unreachable: no scripted S21 first run with real store reads, follow-up in Slice 460)
- [session] Windows VM run: `OpenContext` is now reached from a `Task.Run` pool thread (`WaitForWindowAsync` -> `ContextWithRetryAsync`) and touches `Application.Current.Windows`, `Handler.PlatformView`, `InitializeWithWindow` and writes `_context` unlocked; a wrong-thread COMException would be caught as Failure=null, end the poll early and report `store_failed` with no recovery (`src/Dino/DinoReader.App/Platforms/Windows/Purchases/WindowsPurchases.cs:149`; `reviews/s460/pr-1428-check5.md` #3, unconfirmed, not run on a device).

### Slice 439: Dino: a linked Sonos home with an active Dino subscription plays the paid books ([slice](slices/439-dino-sonos-owned-books.md))
- [session] C3 clips (a render run with the narrator voices, then an upload to R2 `dinoreader/sonos/subscribe-<lang>.m4a`; nothing was rendered or uploaded; C3 stays open on this wait): the 15 texts are `SUBSCRIBE_MESSAGES` in `worker/sonos-labels.ts`; steps in `docs/DEPLOYING.md`, *The subscription proof column and the spoken message*. The render script is outside Slice 439's Touches and is not written (planned row: C3 in Slice 439).
- [owner] **You:** Device and Sonos sandbox: that Sonos sends the `Authorization: Bearer <token>` from `getMediaURI`'s `httpHeaders` to the media route and plays the story (the S21 with a test subscription; a refreshed token is picked up by the next `getMediaURI`, a track already queued with the old token gets 401), that the message clip plays for a lapsed home, and that a renewal on the S21 reaches the Worker (Debug log `[SonosLink] proof renew Renewed`).
- [cloud] iOS: the shared `BookPurchases` code compiled for Windows and Android only; an iOS compile and a TestFlight run are owed (no Mac here). (live check 2026-11-27: unreachable: the iOS compile now passes, dinoreader-ios run 46072316030; the TestFlight run needs an iPhone)
- [owner] **You (native read):** the 15 subscribe texts (`SUBSCRIBE_MESSAGES` in `worker/sonos-labels.ts`).

### Slice 435: Dino: YouTube channel with the four free books as read-aloud videos in 15 languages ([slice](slices/435-dino-youtube-read-aloud-channel.md))
- [owner] **You:** watch the first unlisted video on a TV and say yes or what to change: https://www.youtube.com/watch?v=Fjo-TUF3EoU . Items 6-7 wait on that yes.
- [owner] **You:** verify the YouTube channel's phone number at https://www.youtube.com/verify so the thumbnail and playlist can be set (see the first-upload line below).
- [owner] **You (native read):** the 14 non-English card lines, titles and descriptions in `lib/strings.mjs` were written by Claude, not a native speaker; read the first non-English video you open.
- [session] First upload DONE 2026-11-27 (item 4), video `Fjo-TUF3EoU` (unlisted; API read-back: title, language en, madeForKids true, privacy unlisted). Not done: the custom thumbnail (`thumbnails.set` answered 403, likely an unverified channel; not confirmed) and the playlist "Dino Reader stories in English". After the owner's phone verification, rerun `upload.mjs`: it resumes from `uploaded.json` and finishes both; the check is the read-back of the playlist membership. Item 4 stays unticked until then. (live check 2026-11-27: unreachable: waits on the owner's YouTube channel phone verification, which no tool can read)
- [session] Item 5 spoken clips: 2 of 75 exist (`voice/en/grown-ups.m4a`, `voice/en/title-lion-and-mouse.m4a`); the other 73 wait on the Gemini daily cap. Render them with `make_voice.py` (`node voice-lines.mjs --book all --lang all > ../../../../tmp/lines.json && python make_voice.py --lines ../../../../tmp/lines.json`, about 75 requests, may take two days), then rerun the render; item 5's spoken half stays open until then.

### Slice 454: Dino subscription: Microsoft Store add-ons, 14 other App Store languages, script gaps ([slice](slices/454-dino-subscription-microsoft-addons-and-app-store-languages.md))
- [session] Live App Store apply, denied at night by auto mode (needs an interactive session): `node .github/scripts/store-products-local.mjs --apply --store apple --locales fr-FR`, read back, then all locales, then `--submit`. (live check 2026-11-27: unreachable: auto mode denies the live App Store apply in an unattended session; needs an interactive one)
- [owner] **You:** create the two subscription add-ons in Partner Center: monthly `uk.sirous.dinoreader.sub.monthly` 1.99 GBP and yearly `uk.sirous.dinoreader.sub.yearly` 14.99 GBP, 7-day trial, listing texts from `docs/store-listings/dino/subscription-listings.md`, at https://partner.microsoft.com/en-us/dashboard/products/9N9VHRLCZXN5/addons
- [session] The Windows app's subscription card reads the two add-ons, after that owner step.
- [owner] Native read of the machine-written subscription texts (already in unverified.md under Slice 384).

### Slice 447: Dino story skill: true-size line-up script and accepted-page save guard ([slice](slices/447-dino-story-skill-lineup-script-and-save-guard.md))
- [session] One real Gemini save of an accepted page is refused, and a `-UserAskedRedo` save backs up to the working folder's `previous/`, in the next illustrator run. (live check 2026-11-27: unreachable: no real illustrator book run happened tonight)

### Slice 455: Dino: buy, restore and subscription outcome events ([slice](slices/455-dino-purchase-outcome-events.md))
- [phone] S21: the Store card shows `sub_monthly.shown.trial`, and a cancelled buy sends `book.started` then `book.cancelled` with the book GUID (read in `dino_usage_beta` via `analytics-query`).
- [owner] A real buy, restore and code redemption on iOS TestFlight.
- [owner] Windows sandbox buy: trial versus paid is recorded correctly.

### Slice 452: Dino: sound pill device check hook and Part B leftovers ([slice](slices/452-dino-sound-pill-device-check-hook.md))
- [session] Compare the four shots (`reading-sound-pill-states`) with mock frames 197-199 when `docs/design/dino-reader/frames` exists (only the HTML mock is in the repo today). (live check 2026-11-27: unreachable: docs/design/dino-reader/frames still does not exist)
- [session] iOS simulator run of the same flow (iOS is not compiled on this PC). (live check 2026-11-27: unreachable: needs an iPhone or an iOS simulator run, none in this unattended run)

### Slice 431: Dino: the story skill reports its steps to the agent board ([slice](slices/431-dino-story-board-steps.md))
- [owner] Item C: when the owner next makes a new book, read the book card back with `get_item` on the board (steps, process keys, the owner-read wait).

### Slice 441: Dino: iPad reading page hides Back and the view switch from VoiceOver ([slice](slices/441-dino-ipad-reading-header-voiceover.md))
- [session] The iPad and iPhone simulator hierarchy (the CI iOS simulator, Maestro) shows `ReadingBack` and `ReadingModes` on the reading page (item 1 and item 3 for iOS). (live check 2026-11-27: unreachable: needs an iPhone or an iOS simulator run, none in this unattended run)
- [owner] **You:** VoiceOver on an iPad reads Back, the view switch and the book title as a heading, and does not read the title twice on the sideways tablet layout (`SideTitle` plus heading).
- [phone] Android TalkBack on a tablet reads Back, the view switch and the title as a heading.
- [owner] Windows Narrator reads Back, the view switch and the title as a heading.

### Slice 464: Dino: an older part-take file never plays after a new take is saved ([slice](slices/464-dino-reused-part-take-file-orphan.md))
- [phone] The Verification walk-through by hand on the S21 or emulator (record a part, re-record an overlapping part, delete the new take, and the older recording does not play). The emulator flows `record-only-the-words`, `record-per-character` and `record-per-language` pass but log `replacedFiles=0`, and a scripted flow cannot make a stale orphan, so the orphan path is covered by unit tests only. (live check 2026-11-27: unreachable: a hand walk-through, since no scripted flow can make a stale orphan; the three record flows pass on the emulator)

### Slice 462: Dino: drop the test and review traffic that still reaches dino_usage and Ninjalog ([slice](slices/462-dino-drop-leaking-test-traffic.md))
- [owner] **You:** iOS parity (blocks nothing): Android now drops Test Harness Mode devices, iOS has no such signal, so iOS keeps today's behaviour (TestFlight and App Review already go to `dino_usage_beta`; the iOS Groups A and C stay in `dino_usage`). Exempt iOS or name a signal and the iOS usage gate would change to match; otherwise nothing changes. [Slice file](https://github.com/ashkansirous/ReadTheStupidText/blob/main/docs/plan/slices/462-dino-drop-leaking-test-traffic.md).
- [cloud] After the release that ships this slice, the next analytics window (7 days of Ninjalog): the share of test-looking first sessions (third query in `docs/analytics/ninjalog-queries.md`, "Test-looking sessions") is under 5%, and Group B (10 or more cancels) is gone. Unproven that Play's pre-launch devices set Test Harness Mode. No device run is planned for the Test Harness read (a Debug build skips usage before the harness check, and `adb shell cmd testharness enable` wipes the device); the 7-day query is the proof. (live check 2026-11-27: unreachable: the release went live at 22:32 UTC tonight, so no real traffic from it exists yet)

### Slice 459: Dino first books: the first book is playable within seconds of Continue ([slice](slices/459-dino-first-book-playable-in-seconds.md))
- [phone] The S21 first run with the network throttled to 1 Mbit/s: the first book opens before the rest finish (`tools/emulator` has no throttle flag).
- [cloud] iOS simulator first run. (live check 2026-11-27: unreachable: no Claude Code cloud session and no iOS simulator run in this unattended run)
- [session] Windows VM first run (VM not started: 7.8 GB free memory, under the 8 GB the VM needs).
- [session] The Ninjalog `wait_shown` query after release (the Verification line of the slice). (live check 2026-11-27: unreachable: the release went live at 22:32 UTC tonight, so no real traffic from it exists yet)

### Slice 443: Dino: a parent's part take plays in place of only its words ([slice](slices/443-dino-parent-part-take-plays-its-words-only.md))
- [phone] Hear "said Mummy" in the book's voice between two part takes on the S21 (the parent's ear).
- [cloud] iOS simulator: record a part take and read it back. (live check 2026-11-27: unreachable: no Claude Code cloud session and no iOS simulator run in this unattended run)
- [session] Windows VM: record a part take and read it back (the VM was not started, for memory).

### Slice 437: Dino: Sonos opens in the parent's language with shelves (Bedtime, New, ages, Free, A–Z) ([slice](slices/437-dino-sonos-browse-shelves.md))
- [owner] **You:** Sonos sandbox run in your Sonos app: the root opens in the phone's language, the shelf order, the paid row ("In the Dino app"), where the subtitle shows, and the element order.
- [owner] **You:** A1 on your sandbox (S21 Sonos app): what a tap on a paid book, adding it to the queue and pressing Play on a shelf that holds one do with `canPlay` false and `Client.ItemNotFound`; whether the app's wording for it reads well.
- [session] **Device:** the Sonos app accepts the new element order: a folder row `id, itemType, title, artist, albumArtURI, canPlay, canEnumerate` and a book's subtitle in `album`, and shows each where the mock shows its subtitle. (live check 2026-11-27: unreachable: needs a Sonos speaker on the home network, none was driven in this unattended run)
- [owner] **You:** the square cover crop needs `"images": { "binding": "IMAGES" }` in `wrangler.jsonc` and `wrangler.workers-dev.jsonc` and the Cloudflare Images binding on the account (until then covers are served uncropped).
- [session] **Device:** the seven folder PNGs and the 15 language tiles on a real Sonos screen (`site/dino/sonos-art/`). (live check 2026-11-27: unreachable: needs a Sonos speaker on the home network, none was driven in this unattended run)
- [owner] **You:** the 13 translated label sets (`worker/sonos-labels.ts`; English and Farsi are the mock's words).
- [session] **Device:** Sonos sends `Accept-Language` on SOAP calls (no live call read). (live check 2026-11-27: unreachable: needs a Sonos speaker on the home network, none was driven in this unattended run)
- [session] **Device:** does the Sonos app lay out Farsi right to left? (S21 Sonos app, phone in Farsi.) (live check 2026-11-27: unreachable: needs a Sonos speaker on the home network, none was driven in this unattended run)
- [session] **Device:** does Sonos cache the root per household? If so, two phones with different languages could see the same first screen. (live check 2026-11-27: unreachable: needs a Sonos speaker on the home network, none was driven in this unattended run)
- [session] **Device:** can a service's folder (not just a track) be saved to My Sonos? (live check 2026-11-27: unreachable: needs a Sonos speaker on the home network, none was driven in this unattended run)

### Slice 417: Dino: both web pages point at the live stores, show the new name and current screenshots; testing closed ([slice](slices/417-dino-sites-after-launch.md))
- [chrome] Both live pages after deploy: store buttons open the right store pages, no tester link left.

### Slice 410: Dino: the public name is "Dino Book Reader" everywhere a person sees it ([slice](slices/410-dino-public-name-guard.md))
- [owner] Upload the new full logo and support banner to the Sonos sandbox and Send again (Slice 283 is closed; Slice 438 B9 holds the Self Test and case).
- [owner] Rerun the Sonos Self Test (SOAP Compliance, Browse, Sonos Cloud) from the portal after the deploy.

### Slice 395: Dino: recording a character shows and replaces only that character's quoted words ([slice](slices/395-dino-record-only-the-characters-words.md))
- [session] Windows head: record a page on the Dino Windows head with the switch off (`PartTakes.Enabled = false`) behaves as before (not run).

### Slice 436: Dino: Sonos counts only real plays and shows new books within 5 minutes ([slice](slices/436-dino-sonos-real-plays-and-browse.md))
- [owner] **You:** Sonos Reporting Endpoint (item A4) | Sonos Dev Portal > Dashboard > Dino Book Reader integration (a6BPs000000qWNBMA2) > Sonos Music API > Reporting Endpoint = `https://dinoreader.sirous.uk/sonos/v2.0`, Save. Link: https://developer.sonos.com/s/case/500Ps00001uDPr5IAG/sandbox-content-integration-463879-dino-book-reader-wasnt-added-on-add-to-sonos (the integration's case; the Dashboard link is not recorded).
- [owner] Whether Sonos sends `/timePlayed` reports to an anonymous (no-login) service: the docs are silent. After A4, a real play should show a `reported` row (`analytics-query`); if none arrive, Sonos plays show 0 rather than invented hours, and that result goes back to the owner as one question.
- [cloud] After the release, a new store book shows in the Sonos browse within 5 minutes.
- [session] Whether the 5-then-8 count came from the hour-long cache (not reproduced 2026-11-24).

### Slice 432: Dino: drop the unused PDF and Word libraries (PdfPig, OpenXml) from the Android and iOS apps ([slice](slices/432-dino-drop-pdf-word-libraries.md))
- [phone] The Dino TestFlight build launches and opens a book. (live check 2026-11-27: unreachable: needs an iPhone or TestFlight, none reachable here)
- [session] RTST Android opens a PDF on the emulator (no flow exists; a flow that pushes a small PDF is a follow-up). (live check 2026-11-27: unreachable: no flow exists, follow-up in Slice 432)

### Slice 453: Dino first-books picker: a cancelled or failed list load must not say "Connect to the internet" ([slice](slices/453-dino-first-books-cancelled-load-copy.md))
- [phone] Emulator: open the first-run picker, Cancel during the list load: the cancelled copy shows with Try again, and the device log carries the `cancelled-load` marker (not `failed-load`). (live check 2026-11-27: unreachable: no scripted flow stalls the list load, follow-up in Slice 453)
- [phone] With the network back after a failed load (offline or a server error), the first-books list reloads by itself. (live check 2026-11-27: unreachable: no flow asserts the reload without a tap, follow-up in Slice 453)
- [phone] The same two checks on the iPhone (shared page, no iOS-only code). (live check 2026-11-27: unreachable: needs an iPhone or TestFlight, none reachable here)

### Slice 431: Dino: the story skill reports its steps to the agent board, with process keys ([slice](slices/431-dino-story-board-steps.md))
- [session] Item C: one real book run, read back with `get_item` on the board (steps, process keys, the owner-read wait). (live check 2026-11-27: unreachable: no real illustrator book run happened tonight)

### Slice 463: Dino: a small "quick" first-book package for slow internet, replaced by the full one in the background ([slice](slices/463-dino-quick-first-book-package-for-slow-internet.md))
- [owner] **You:** pick the quick package's picture size and audio bitrate (quick 448x600 / AAC 24 kb/s mono vs standard, four free books, en and de) | the side-by-side page is made by an interactive session; no link yet
- [phone] The slow-link switch judged after about 3 s on a real slow link, and the 404 fallback to standard (try `emu network speed edge`; the 2026-11-27 `umts` run never got past the store list).
- [session] The real quick-then-standard swap, once quick packages are published.
- [cloud] The iOS simulator first-books step on a slow link. (live check 2026-11-27: unreachable: no Claude Code cloud session and no iOS simulator run in this unattended run)
- [session] The Windows VM first-books step throttled to 256 kbit/s.
- [session] Ninjalog after release: `wait_shown` detail `getting_quick_file:*` appears. (live check 2026-11-27: unreachable: the release went live at 22:32 UTC tonight, so no real traffic from it exists yet)
- [session] `.maestro/dino/first-book-slow-link.yaml` (expects `[FirstBooks] landed-quick`) was not run: it needs quick packages in the store, none exist until item 2.
- [session] `high-res-swap-on-wifi` asserted nothing in the 2026-11-27 run: run `high-res-limited-then-wifi` first, with a live high-res pack.

### Slice 457: Dino Alexa: finds a book by any part of its name, lists every book, plays only what it can, and keeps favourites ([slice](slices/457-dino-alexa-find-list-favourites.md))
- [owner] **You:** on your Echo: "Alexa, open Dino Books", then "yes", "no", "search", "goldylock", "the lion", "add this to my favourites", "play my favourites" | no link (a real Echo)
- [owner] **You:** on your Echo: the end-of-story clip pacing by ear | no link (a real Echo)
- [owner] **You:** on your Echo: saving a favourite mid-story ("Alexa, ask Dino Books to save this story") and the story carries on | no link (a real Echo)
- [owner] **You:** on your Echo: "Alexa, repeat" and "Alexa, next" (a random other story, decision 17) during and after the clip; whether "Alexa, next story" without the skill name reaches the skill while audio plays (Amazon routes it, not the model) | no link (a real Echo)

### Slice 440: Dino: search on Sonos, "Stories" and "All languages" ([slice](slices/440-dino-sonos-search.md))
- [owner] Portal versus hosted presentation map: the current docs.sonos.com add-search page sets the search categories and their strings in the portal (Search Capabilities, Localization Resources) and does not mention a hosted presentation map, so `site/dino/sonos/presentation-map.xml` may be unused. The owner does the portal steps in D2 and says whether mobile search shows both categories; if it does not, the hosted map is not enough on its own.
- [owner] **You:** the owner step in D2 (Search Capabilities categories "Stories" `stories` and "All languages" `all-languages`, Localization Resources and, if the portal has the field, the presentation map URL and version) in the Sonos Developer Portal, integration a6BPs000000qWNBMA2 (same case link as 436: https://developer.sonos.com/s/case/500Ps00001uDPr5IAG/sandbox-content-integration-463879-dino-book-reader-wasnt-added-on-add-to-sonos).
- [owner] The sandbox check ("goat" in the S21 Sonos app: two English rows under Stories, the own-language matches under All languages).

### Slice 438: Dino: every Sonos home links once through the Dino app and saves its story language ([slice](slices/438-dino-sonos-link-story-language.md))
- [session] iOS simulator: the link opens `SonosLinkPage` and the Debug test-hook plist merge keeps the `dinoreader` scheme (review high #8). (live check 2026-11-27: unreachable: needs an iPhone or an iOS simulator run, none in this unattended run)
- [phone] The S21 opens the link from the Sonos page and hands back.
- [owner] **You:** switch the Sonos integration's auth to AppLink once 438 merges | Sonos Developer Portal, integration a6BPs000000qWNBMA2 (same case link as 436: https://developer.sonos.com/s/case/500Ps00001uDPr5IAG/sandbox-content-integration-463879-dino-book-reader-wasnt-added-on-add-to-sonos)

### Slice 436: Dino: Sonos counts only real plays and shows new books quickly ([slice](slices/436-dino-sonos-real-plays-and-browse.md))
- [owner] **You:** set the Sonos Reporting Endpoint to the A2 URL (item A4) | Sonos Developer Portal, integration a6BPs000000qWNBMA2 (the integration page link is not recorded yet; the open case is https://developer.sonos.com/s/case/500Ps00001uDPr5IAG/sandbox-content-integration-463879-dino-book-reader-wasnt-added-on-add-to-sonos)

### Slice 453: Dino first-books picker: a cancelled or failed list load must not say "Connect to the internet" ([slice](slices/453-dino-first-books-cancelled-load-copy.md))
- [phone] Emulator: open the first-run picker, Cancel during the list load: the cancelled copy shows with Try again. (live check 2026-11-27: unreachable: no scripted flow stalls the list load, follow-up in Slice 453)

### Slice 445: Dino: Microsoft Store trailer, Partner Center push and Play video link ([slice](slices/445-dino-microsoft-store-trailer-and-play-video-link.md))
- [session] Render the 1920 x 1080 landscape cut and push the Microsoft Store screenshots, captions and trailer, then read them back (Slice 433 is closed; its storyboard in `tools/dino/promo/store/`).
- [session] The first real upload in Slice 435 is done (unlisted, video `Fjo-TUF3EoU`, 2026-11-27; script `tools/dino/promo/youtube/upload.mjs`); the Play video URL and the YouTube upload of the landscape cut can go ahead.
- [session] Run `store-size-check.ps1` on the eight Windows PNGs (Slice 433 is closed). (live check 2026-11-27: unreachable: store-size-check.ps1 has no Windows target, follow-up in Slice 445)

### Slice 433: Dino: new-look store screenshots, App Store previews, Play feature graphic ([slice](slices/433-dino-store-screenshots-video-play-appstore.md))
- [owner] **You:** a native read of the machine-drafted screenshot captions (14 languages other than English, `docs/store-listings/screenshots/captions.json`), live on Play since 2026-11-25.
- [owner] **You:** a native read of the machine-drafted App Store preview video captions (13 languages other than English, `tools/dino/promo/store/preview-captions.json`; the new lines are cap3 "Record Grandma as the wolf", cap6 "Some books need a subscription" and endSub, taken from each listing title; zh cap1 is its own line). Uploaded 2026-11-25 to the unsubmitted App Store draft 1.136.5 (item 10), not live on the store until that version ships.
- [chrome] The public Play page (https://play.google.com/store/apps/details?id=uk.sirous.dinoreader) shows the new phone set and feature graphic (pushed and read back through the API 2026-11-25; the public page can lag by hours).
- [chrome] The live App Store page shows the new sets and previews once Apple approves 1.136.5 (WAITING_FOR_REVIEW, ASC API 2026-11-25).

### Slice 444: Dino: iPhone Duo store screenshot set and Duo preview ([slice](slices/444-dino-iphone-duo-store-screenshots-and-preview.md))
- [owner] **Device:** the iPhone Duo set, until Xcode has a Duo simulator and Slice 403 has shipped.

### Slice 434: Dino: Microsoft Store 1920 x 1080 shots and the landscape video ([slice](slices/434-dino-windows-store-shots-and-landscape-video.md))
- [session] The dino-onboarding mic step failed once on the VM 2026-11-24 (run2.log: "FAIL R on the voice step started no recording and showed no microphone-off state (waited 15 s)"); recheck on main. The store-shots check follows `dino-onboarding-skip` and no longer runs that step.

### Slice 420: Dino Windows checks after one shelf ([slice](slices/420-dino-windows-checks-after-one-shelf.md))
- [session] `needs-file/store-shot-phone` and `needs-file/store-shot-tablet` Maestro flows (tap on a book's dots by title) were not run.
- [cloud] iOS: a cover tap reads the book on the unlocked shelf. (live check 2026-11-27: unreachable: no Claude Code cloud session and no iOS simulator run in this unattended run)

### Slice 418: Dino one shelf with a lock ([slice](slices/418-dino-one-shelf-with-a-lock.md))
- [cloud] iOS on a simulator or device: the lock, the buying sheet and recording hold the clock. (live check 2026-11-27: unreachable: no Claude Code cloud session and no iOS simulator run in this unattended run)
- [owner] Native reads of the new and changed strings in 14 languages: `Lock_*`, `Onboarding_SkipStep`, `Onboarding_VoiceBody`, `Onboarding_ThatsYouBody`, `FirstRun_HelloBody`, `Gallery_ReorderHint`.

### Slice 413: Dino store name, subtitle and keywords in 14 languages ([slice](slices/413-dino-store-name-13-languages.md))
- [owner] **You:** native read of the new Dino name, subtitle, keyword field, Play short description and Microsoft search terms in `docs/store-listings/dino/<lang>.md`, one language at a time: ar, da, de, es, fa, fi, fr, ja, nb, nl, pt, ru, sv, zh (all machine drafts; Farsi has no App Store entry). The same texts are on the editable App Store version 1.133.1 (not submitted); Microsoft and Play are repo-only.

### Slice 403: Dino: foldable layouts ([slice](slices/403-dino-foldable-layouts.md))
- [owner] **Device:** the iPhone Duo on a simulator or device (no Duo in Xcode yet; Apple has published no pixel sizes, points or safe areas, 2026-11-16). The Fold8 and Ultra on a real device.
- [owner] **You:** the 14 non-English translations of `Reading_TheEnd` and `Onboarding_LanguagesBodyShort` are machine-written, not checked by a native reader.

### Slice 398: Dino: the parents' Books tab after a lapse ([slice](slices/398-dino-locked-books-banner-and-offline-notice.md))
- [phone] **Device:** iOS simulator and Windows: the banner, the locked tile look and the offline notice (no Mac; the VM run was skipped at 3.2 GB free memory).
- [phone] **Device:** the 3-day offline rule on a real device.
- [phone] **Device:** true greyscale on the locked cover: the build is a grey veil (follow-up, needs per-platform image handling).
- [phone] **Device:** the reorder layer still lets locked books be dragged (drawn at 55% without veil or badge).
- [owner] **You:** the 14 non-English strings of this slice have had no native read.

### Slice 399: Dino: a "Books and buying" group in Settings ([slice](slices/399-dino-settings-books-and-buying.md))
- [phone] **Device:** iOS and Windows (rail Settings tab): the Books and buying group and its rows were not run (no Mac; the Windows VM was skipped at 3.2 GB free memory).
- [phone] **Device:** Restore from Settings on a store account that owns books (the emulator account owned none: `NothingFound`), and the request reaching a Store tab that was never opened or is already loaded on a real account.
- [phone] **Device:** iOS and Windows: a subscription-only read with no listing (`BookPurchases.RefreshLastListedAsync`) cannot overwrite a stored subscription; read only on Android.
- [owner] **You:** the 14 non-English strings of this slice have had no native read.

### Slice 394: Dino: the store age chips ([slice](slices/394-dino-store-age-filter-crash-and-bands.md))
- [phone] **Device:** item 1, the crash fix with old-range data: tap the oldest chip on a build whose store still has a "9-12" chip, or check the crash store after the release (`Java.Lang.IllegalStateException` "already has a parent", Dino Android 1.129.2); the emulator run did not exercise that path.
- [phone] **Device:** iOS and Windows store chip taps.
- [chrome] **Device:** the live store list after the batch release publishes the re-ranged books (read `/api/stories` back; every book's `ageMin`/`ageMax`, Tortoise and the Hare included).
- [phone] **Device:** the Love from Seagull Island book (draft PR #1244) needs its band when it lands.

### Slice 400: Dino: the crashes first reported 2026-11-14/15 ([slice](slices/400-dino-crashes-2026-11-14.md))
- [phone] **Device:** iOS compile of the shared `StallGuardedRead.cs` change, and a Cancel during a first-book download on an iOS device or TestFlight build (same flow as `.maestro/dino/first-books-cancel-real-store.yaml`; only an Android flow exists): no Mac here.
- [session] **Device:** other direct `ReadAsStreamAsync` calls bypass the guard (`RemoteConfigClient.cs:287`, `SubscriptionEndClient:48`; small bodies, same hazard class, outside the slice's Touches): not routed through `OpenBodyAsync`. (live check 2026-11-27: failed, follow-up in Slice 400)

### Slice 397: Dino: the Store's subscription card in every state ([slice](slices/397-dino-subscription-card-states.md))
- [phone] **Device:** Windows head build of the Slice 397 changes (wide layout, Ctrl+R hint).
- [phone] **Device:** layout of the card states on the emulator: no Maestro flow carries a `# layout-check` header, so only titles and log lines were checked.
- [owner] **You:** real Play, App Store and Microsoft Store purchases: a second trial purchase on one account, the Grace and Fix payment paths.
- [owner] **You:** the 14 non-English trial strings read by a native speaker.

### Slice 405: Dino back cover names per language ([slice](slices/405-dino-back-cover-names-per-language.md))
- [owner] **Device:** iOS simulator back cover in Farsi and Japanese (no Mac here; `.github/workflows/dinoreader-ios-sim.yml` is workflow_dispatch only and has no back-cover flow).
- [owner] **You:** native read of the spellings chosen without a store-card source: ar «الأخوان غريم»; Perrault, Southey and Andersen in ar, fa, ja, ru, zh; ru «Эзоп».

### Slice 396: Dino Restore and Redeem out of the Store foot ([slice](slices/396-dino-restore-and-redeem-out-of-the-store-foot.md))
- [owner] **Device:** iOS compile and iOS run of the Store's first-line Restore link and the result message.
- [phone] **Device:** Windows head run on the VM: Ctrl+R and the first-line layout.
- [owner] **You:** real-store Restore and Redeem on Play, the App Store and the Microsoft Store (sandbox, owner).
- [owner] **You:** native read of the 14 machine-translated strings.
- [phone] **Device:** screen reader announcement of the restore result (TalkBack, VoiceOver, Narrator).

### Slice 405: Dino back cover names per language ([slice](slices/405-dino-back-cover-names-per-language.md))
- [owner] **Device:** iOS simulator back cover in Farsi and Japanese (no Mac here; `dinoreader-ios-sim.yml` is workflow_dispatch only and has no back-cover flow).
- [owner] **You:** native read of the spellings chosen without a store-card source: ar «الأخوان غريم»; Perrault, Southey and Andersen in ar, fa, ja, ru, zh; ru «Эзоп».

### Slice 402: Dino App Store header and search-results pictures ([slice](slices/402-dino-app-store-creative-assets.md))
- [owner] **Device:** how the pictures look in App Store search on an iOS 27 device.

### Slice 401: Dino iOS app languages ([slice](slices/401-dino-ios-app-languages.md))
- [chrome] **Device:** the App Store "Languages" line lists the 15 languages (needs the next version reviewed and released).
- [owner] **You:** the German microphone prompt on a real iPhone.

### Slice 390: Dino iOS purchases on StoreKit 2 ([slice](slices/390-dino-ios-purchases-storekit-2.md))
- [owner] **Not a user to-do (the sandbox Buy was done by the user, 2026-11-15: "I tried the sandbox buy!"; never ask again):** not stated by the user and so still unverified: a subscription plan showing its real end date, and Restore purchases (also on a second install), on an iPhone with a TestFlight build.
- [session] **Device:** the macOS jobs prove the Swift XCFramework builds and the signed Release build links it (`dinoreader-ios.yml`, `dinoreader-ios-sim.yml`); the Worker's check of a real Apple JWS (the pinned Apple Root CA - G3 chain) has only run against a test chain. (live check 2026-11-27: unreachable: dinoreader-ios.yml passes on main-bound PRs (run 46072316030), but no real Apple JWS has reached the Worker check)

### Slice 384: Dino subscription ([slice](slices/384-dino-subscription.md))
- [session] **Device:** iOS simulator subscription flow (`.maestro/dino/subscription.yaml`). (live check 2026-11-27: unreachable: needs an iPhone or an iOS simulator run, none in this unattended run)
- [phone] **Device:** Windows Hyper-V VM subscription flow.
- [owner] **You:** real store sandbox (Play licence tester, Apple sandbox) after the store setup.
- [chrome] **Device:** Worker route `GET /api/stories/subscription` live, after the batched release deploy.
- [phone] **Device:** `store-products.mjs` request shapes: the Apple and Play runs went through live on 2026-11-15; the Microsoft shapes are still unverified.
- [phone] **Device:** offline 3-day lock on a real device.
- [owner] **You:** restore on iOS.
- [phone] **Device:** the app's subscription card shows each plan's name, length and price before buying (Apple requires it in the app; not checked on a screen).
- [owner] **Device:** the plans showing in the iOS app (sandbox/TestFlight) now that the products exist.

### Slice 383: Dino: a book keeps reading when the phone is locked ([slice](slices/383-dino-reading-keeps-going-when-locked.md))
- [phone] **Device:** A long locked read (1 minute or more, three-little-pigs with its real Farsi pack on the S21 dev install; `KEYCODE_WAKEUP` before `open-book`): a silent beat's delay is not stalled by Doze.
- [owner] **You:** iOS TestFlight: a book plays to the end with the iPhone locked, the silent beat's delay runs, and `OnDisappearing` does not pause it on lock.
- [phone] **Device:** Another app in front with the screen on: the read goes on (no device run covers it).

### Slice 391: Dino Windows: Enter on the first-run story-languages step opens no book picker ([slice](slices/391-dino-windows-first-run-enter-on-story-languages.md))
- [phone] **Device:** `dino-reorder` on the VM (item 3; Slice 382's open Windows check) was not reached: `dino-onboarding-keys` passed on the final build `50c83d7c6389`, but the prerequisite `dino-launch` then failed ("the shelf did not show", app left on onboarding step 5 of 6), so `dino-reorder` stopped with "SETUP FAILURE: need two books on the shelf, found 0". Likely a harness gap in `tools/desktop-vm` (finish or skip onboarding before `dino-launch`), not app code.

### Slice 381: Dino: first-run tour placement under a tall status bar ([slice](slices/381-dino-tour-shifted-under-tall-status-bar.md))
- [phone] **Device:** The tall-cutout overlay was enabled for the Pixel-size run but not read back, so the 149 px status bar of the repro may not have been active.
- [phone] **Device:** The flow was not shown failing on the old build (without the fix).
- [phone] **Device:** Farsi (RTL) placement: whether the layer mirrors `Margin.Left` under RTL.
- [phone] **Device:** S21: `onboarding-tour-position` not run.
- [owner] **Device:** iOS notch / Dynamic Island: the copies on their targets (simulator workflow or TestFlight).
- [phone] **Device:** Small screenshots of the Child's shelf and Grown-ups stops with the copy over the real control.

### Slice 382: Dino reorder: held book wiggles, plain drag ([slice](slices/382-dino-reorder-held-book-wiggles-plain-drag.md))
- [phone] **Device:** Windows VM `dino-reorder` (mouse drag, keyboard path, the new hint): blocked by the `dino-onboarding-keys` first-run Enter failure (Slice 391).
- [owner] **Device:** iOS pan (simulator or TestFlight): the scroll view waits for the cover's pan, no scroll before the drag, a tap on a cover still lifts.
- [phone] **Device:** The drag's drop slot: the Maestro flow only proves that a drag happens, not where the book lands.
- [phone] **Device:** Tap-lift a cover, then drag another cover: the new drag takes over the lift; look at it on a device.

### Slice 386: Dino: the default-view-per-book flow fails on main ([slice](slices/386-dino-default-view-per-book-flow-fails.md))
- [phone] **Device:** no test-store book has carried `textInPictures` since Slice 350, so the device path of the Picture-only default (story.json `TextInPictures` -> `Story.Mode` -> `PictureTextPages.IsPictureOnly` -> `ModeSwitch`) is covered only by the `Story.Mode` unit tests (`TextInPicturesTests.cs`), not by any flow.

### Slice 387: Dino: the shelf title cut by the Grown-ups button ([slice](slices/387-dino-shelf-title-cut-by-grownups-button.md))
- [phone] **Device:** The title's seven taps open the developer switch while the short label sits above the full one at Opacity 0 (InputTransparent), on iOS and Windows; and on Windows the VM checks (`Find-UiaById 'GalleryTitle'`) find only the full label, so they miss a shelf whose title is Short or Hidden.
- [phone] **Device:** Farsi strings on the shelf header (title whole or shortened, never cut by the pill).
- [phone] **Device:** The iPhone shots (run 43657945195, 2026-11-15): 6.5" FAILS the check (title "My s…" with clear space before the pill; item 3); 6.9" produced no shots (Maestro `IOSDriverTimeoutException`, twice); iPad 13" failed at `ReadingBack` not found, the same step as wave 4 (Slice 291's open iPad frame). (live check 2026-11-27: unreachable: needs an iPhone or an iOS simulator run, none in this unattended run)

### Slice 380: Dino Android: product details through the listener only ([slice](slices/380-dino-android-listener-only-details.md))
- [phone] **Device:** S21 with a Play internal-track build shows a price for every product and one `product details:` line per refresh (needs the next batched release).

### Slice 375: Dino: every picture in two sizes ([slice](slices/375-dino-two-picture-sizes.md))

- [session] **Device:** a real illustrator run using the small copy (the Good Enough run): small copy saved beside each candidate and no full-size read in the transcript. (live check 2026-11-27: unreachable: no real illustrator book run happened tonight)
- [phone] **Device:** parts B-D (merged #1113 on review and unit tests, no device run): a tablet on Wi-Fi gets a store book at standard size, then the high-res pack swaps in at the next cold start; a phone stays standard; on mobile data no hd download starts and one in flight is cancelled.

### Slice 362: Dino onboarding shows what Dino can do ([slice](slices/362-dino-onboarding-shows-what-dino-can-do.md))

- [phone] **Device:** `onboarding-tablet`: no tablet instance.
- [phone] **Device:** VM checks `dino-onboarding` and `dino-launch`: free memory 6.5 GB, below the 8 GB VM rule.
- [owner] **You:** iOS is compile only; the microphone prompt needs a TestFlight run.
- [phone] **Device:** WN2 and WN4 wide layouts, and right-to-left (Farsi N4c), on a device.
- [phone] **Device:** Lion and X Marks demo pages: no such books in the test store.
- [owner] **You:** the 55 new strings are machine drafts and need a native read.
- [phone] **Device:** layout on the emulator flows: no `layout-check ok` line was printed.

### Slice 358: Dino Android no Buy button ([slice](slices/358-dino-android-no-buy-button.md))

- [phone] **Device:** iOS and Windows strips are only compiled, not run.
- [owner] **You:** the translations of `Store_NotForSaleNow` are machine drafts.

### Slice 360: Dino play waits for Wi-Fi ([slice](slices/360-dino-play-waits-for-wifi.md))

- [phone] **Device:** book pulled mid-launch keeps its card until relaunch (check5 #1); a malformed listing entry gets no card (check5 #2).
- [phone] **Device:** offline launch with a saved listing older than a newly published pack: no card until the next online launch refreshes the listing (check3 #1).
- [phone] **Device:** no jank opening a book on a slow phone (the pack-folder listing on the UI thread in `ReadingPage.OnAppearing`, review #9).
- [owner] **You:** iOS compile after the last fixes, and a TestFlight run of the cellular pack download and the reading-page states.
- [phone] **Device:** real airplane mode on a phone (the flows force offline by a Debug hook).
- [phone] **Device:** add a language on mobile data only: every shelf book's pack for it downloads with no question.
- [chrome] **Device:** Worker serving one version behind, live, after the batched release deploys.

### Slice 364: Slice 357 design pass ([slice](slices/364-server-errors-design-pass.md))

- [phone] **Device:** Windows head store page (Dino) on the VM (5.8 GB free, VM not started).
- [phone] **Device:** RTST App x64 voice error text (`Error_Server` / `Error_VoiceGone`) on the VM.
- [phone] **Device:** RTST Android pack-failure text on a device.
- [owner] **Device:** iOS compile and TestFlight.
- [owner] **You:** native reads of the 14 non-English languages (machine-drafted).
- [phone] **Device:** a list-load 402/404 shows a book-specific text (`ShowOffline` reuses `StoreText.ErrorMessage`): check whether the list endpoint can answer those.

### Slice 363: Dino grown-ups' gate card ([slice](slices/363-dino-windows-grownups-gate-card.md))

- [phone] **Device:** Windows head on the VM: `dino-gate`, G1/G2 screenshots against the mock, Esc, and the four changed VM checks (5.8 GB free, no VM run).
- [phone] **Device:** the tablet card flow on the tablet AVD.
- [phone] **Device:** the hold ring's touch area on the short card (check #9).
- [phone] **Device:** landscape phones under 393 dp high.
- [phone] **Device:** Windows under about 470 wide and under 540 high.
- [phone] **Device:** the UIA AutomationId `GateCard` on WinUI.

### Slice 359: Dino: a drawn back-cover picture for every book ([slice](slices/359-dino-back-cover-picture.md))

- [owner] **Device:** iOS on a device (compile only).
- [phone] **Device:** Windows head run (compiled only).
- [phone] **Device:** tablet layout and the Batch 45 22/17 sp credit sizes (wait for the tablet slice, 234).
- [phone] **Device:** long-language credits in the grown band on a narrow phone beyond fa/ar.
- [phone] **Device:** older installed apps showing the picture page.
- [phone] **Device:** Good Enough's back cover (after its text-free redraw, Slice 350 part E, and its text rewrite).
- [phone] **Device:** item 6: `layout-check` (the four back-cover flows carry no layout-check header).
- [phone] **Device:** item 6: the Read again glyph on the back cover (screens show the play triangle; no flow asserts it).
- [phone] **Device:** item 6: the tablet and Windows 360 card.
- [phone] **Device:** item 6: Windows head build.
- [owner] **Device:** item 6: iOS on a device.
- [phone] **Device:** item 6: the text-only view screenshot was not reviewed.
- [phone] **Device:** item 6: the Farsi/Arabic author line wraps "Ashkan / Sirous" in the 300 card (check against BC3).

### Slice 355: Dino: a public web page listing every store book, free or paid, and the price ([slice](slices/355-dino-web-catalogue.md))
- [chrome] **Device:** Live `/books` and the workers.dev `/dino/books` after the batched release deploys: covers load, no third-party request, loading/Cancel/Retry work, and with the switch on the price shows only on paid books.
- [owner] **You:** Native read of the 14 non-English translations of the page text.
- [owner] **You:** Switched-on check after the paid books store setup (Slice 346 Part D).

### Slice 356: Dino illustrator: faster and cheaper ([slice](slices/356-illustrator-faster-and-cheaper.md))
- [session] **Device:** after-numbers on a real page group of a book (seconds per picture, peak context, cache reads per picture against the Baseline table), taken on the next real illustrator run with `illustrator-usage.mjs`. (live check 2026-11-27: unreachable: no real illustrator book run happened tonight)
- [session] **Device:** a real send after the check-pass edit to `__setupPicture` (pictures validation, mark only after all checks, mark restored on REFUSED/MISSING); only the earlier version was live-sent on 2026-11-13. (live check 2026-11-27: unreachable: no real illustrator book run happened tonight)

### Slice 352: Dino: every screen, wizard step and button press is counted ([slice](slices/352-dino-screen-and-tap-events.md))
- [phone] **Device:** corner screen row and a speed `setting_changed` on a device (the flows do not open them).
- [phone] **Device:** the Windows VM: window-close flush and `app_backgrounded` (the `Destroying` path); the package builds since Slice 353. Harness gap (2026-11-13 VM run): no VM check closes the window and reads the usage events.
- [owner] **You:** the iOS simulator (none on this machine) and the `DidEnterBackground` flush.
- [session] **Device:** `tools/usage-sessions` against live data after release. (live check 2026-11-27: failed, follow-up in Slice 352)

### Slice 351: Dino: every usage row says who and when, and audio failures say why ([slice](slices/351-dino-usage-context-and-audio-causes.md))
- [phone] **Device:** `premade_audio:no_pack` with a book id, on a book whose pack is not downloaded.
- [owner] **You:** the iOS run of the context fields and the new fallback reasons (iPhone or TestFlight).
- [phone] **Device:** Windows head on the VM, including the animal clip copy failure reason.
- [phone] **Device:** `.maestro/dino/usage-context.yaml` should set English first (the flow assumes an English UI).

### Slice 350: Dino: high-resolution pictures for a printed Amazon book ([slice](slices/350-dino-high-res-pictures.md))
- [owner] **You:** the first real high-res push on `main` starts no `dino-store` run (needs part D, which needs your Chrome and Gemini account).

### Slice 349: Dino: grown-ups corner status bar and clipped tab labels ([slice](slices/349-dino-corner-status-bar-and-tab-labels.md))
- [phone] **Device:** the `[StatusBar] ... darkIcons=False` log line was not seen in logcat (the flow passed without it).
- [phone] **Device:** Farsi screenshots were not inspected (the flow passed).
- [phone] **Device:** tab labels at system font scale 1.3.
- [owner] **You:** iOS simulator or TestFlight corner screens (status bar and tabs).
- [phone] **Device:** Windows head title bar (VM not run).
- [owner] **You:** item 4: the next TestFlight upload of Dino is accepted by App Store Connect with no missing purpose-string warning (ITMS-90683); if flagged, restore the three keys.

### Slice 346: Dino: four free books, the rest $0.99 each ([slice](slices/346-dino-paid-books.md))
- [owner] **You:** a real Play test-account purchase and restore.
- [owner] **You:** an iOS StoreKit sandbox or TestFlight purchase and restore.
- [phone] **Device:** the Windows Store add-on on the VM.
- [phone] **Device:** the switch-on store page against the real Worker.
- [owner] **You:** the Microsoft Store ID flow against a live store.
- [phone] **Device:** Part E on a device (merged #1120 on review and unit tests): on a Windows, Android and iOS paid row, the store row's wait and Cancel hold until the run ends, and a 403 `purchase-not-valid` after a real purchase is retried on the 3-30 s schedule (about 84 s) instead of showing "Try again" at once; a refused paid download reaches analytics as `download_refused` / `purchase_not_valid`.

### Slice 340: Listening page: Mid-book speed change on a phone ([slice](slices/340-mid-book-speed-change-on-a-phone.md))
- [phone] **Device:** Fix 1 on a device: Settings > Reading speed slider runs 0.8x-1.2x with the left label "0.8x", on Android, iOS and the Windows head, and a book reads at 0.8x at the low end. Android phone emulator, 2026-11-09: `settings-reading-speed-row.yaml` passed and shows "0.8x · slow"; iOS (**You**, no Mac), Windows and the 0.8x reading are still open.

### Slice 322: Dino's silent beats pause when the screen locks ([slice](slices/322-dino-beat-pauses-on-screen-lock.md))
- [owner] **You:** iOS (no Mac): the same `Window.Stopped`/`Resumed` handlers, not run.
- [phone] **Device:** Edge cases reverted after the second strike: a window re-subscribe while an event fact is newer than the platform read, and a null `Platform.CurrentActivity`.
- [phone] **Device:** Harness flake risk (`reviews/s322-check-final3.md` #1-#3): the screen cycle could sleep the display during the flow's final launch/assert or setup waits.

### Slice 321: Reorder layer back to the mock on Windows ([slice](slices/321-reorder-layer-per-mock.md))
- [phone] **Device:** The tablet AVD (`dino-tablet-upright-shelf`): the layer uses the shelf's grid, hint aligned with its left edge, also beside the grown-ups' rail.
- [phone] **Device:** Phone portrait: 3 across, 14 dp sides, 27 dp row gap (no flow asserts the measurements).
- [phone] **Device:** Windows VM with 6 or more books (only 2 books were used; 2026-11-13 VM run: still 2 books, `dino-reorder` passed, no 6-book + resize check exists), and a window resize while the layer is open (columns change, no layout cycle).
- [phone] **Device:** A narrow Windows window after recheck fix 1.
- [phone] **Device:** The 30 px versus 40 px inset on Windows (an assertion that agrees with the screenshot). 2026-11-13 VM run: `dino-reorder` logged 30 px against the mock's 40, with 0 layout-cycle lines, but no assertion.
- [owner] **You:** iOS phone/tablet: the same shared MAUI code; no Mac, not run.

### Slice 325: Dino every page in the 2026-11-08 design, parts A-F ([slice](slices/325-dino-2026-11-08-redesign.md))
- [phone] **Device:** No `layout-check ok` line was printed for any onboarding or intro flow.
- [owner] **You:** iOS on a device (34 pt home bar, 59 pt island inset) for the first run and intro.
- [phone] **Device:** Windows intro Left from the languages step may drop unsaved ticks (check6 #4, unconfirmed).
- [owner] **You:** 14 machine-drafted translations of the new first-run strings need native review (parts A-C add `Store_OnShelf` and `Settings_GroupComputer`).
- [owner] **You:** iOS on a real iPhone 8 and an iPhone 17 Pro Max (Dynamic Island, home bar, landscape flip) for the corner tabs and kids' shelf (parts A-C).
- [owner] **You:** The Samsung three-button bar on the S21 (frame P5 is an assumed size); a real tablet in both orientations.
- [phone] **Device:** Arabic and Persian first-run and corner layouts on a device (parts A-C).
- [phone] **Device:** Contact sheet for the new shots: the onboarding flow names shot-OL1-language and shot-OTL1-language but takes neither (no manifest row); O1 / O1-8 and O6 / O6-8 may be the same image if the 375 x 667 resize does not take effect between captures; the intro flow header says O9 where the frame is OT9 (comment only).

### Slice 323: Dino shelf titles follow the story language picked on the shelf ([slice](slices/323-dino-shelf-titles-follow-story-language.md))
- [owner] **You:** iOS has no scripted run: the shelf title font for a Farsi title (Vazirmatn) on a tile and in the book sheet.
- [owner] **You:** On the iPhone (TestFlight): picking فارسی on the shelf retitles "Good Enough" and "The Three Little Pigs".

### Slice 324: Dino reads at 1x by default ([slice](slices/324-dino-default-speed-1x.md))
- [owner] **You:** iOS: a fresh Dino install shows 1x on the Reading speed page and a pre-made clip plays at normal speed.

### Slice 318: Dino: the /stats page on phones and tablets ([slice](slices/318-dino-stats-phone-tablet.md))
- [phone] **Device:** Both orientations on the live /stats page on the S21 and the tablet AVD (touch, notch spacing, Filters sheet focus, the side panel in landscape).
- [owner] **You:** The live page against real D1 data (the Worker deployed 2026-11-08 and serves the new layouts; the real-data look is not checked).
- [phone] **Device:** Screenshots of the page side by side with the mock frames 200-204 (not compared; `tools/stats-page-check/check.mjs` checks the values only).

### Slice 313: Dino: a shelf book's text and its sound always match ([slice](slices/313-dino-book-text-and-sound-in-step.md))
- [owner] **You:** The user's phone: Freddy in English and Farsi after updating to the release with this slice (Dino 1.116.0 or later): new text, and sound plays.
- [owner] **You:** iPhone: the same path.
- [phone] **Device:** Windows head: the same path.

### Slice 287: Dino: a public usage dashboard at dinoreader.sirous.uk/stats ([slice](slices/287-dino-usage-dashboard.md))
- [owner] **You:** A private-window open of the public page shows it.
- [owner] **You:** The page side by side with mock frame 200 at 1280 (the mock's `support.js` is not in the repo, so it could not be rendered).

### Slice 282: Dino: an Alexa kid skill that reads the store books ([slice](slices/282-dino-alexa-kid-skill.md))
- [owner] **You:** Everything on a real Echo: open, list, read, pause, resume after a minute, next, stop.
- [owner] **You:** A real signed Alexa request passing verification live.

### Slice 289: Dino: count Sonos plays on the server ([slice](slices/289-dino-count-sonos-plays.md))
- [owner] **You:** A live Sonos play counts exactly one row, once the user's Sonos sandbox (Slice 283) works.

### Slice 305: Dino package cleanup ([slice](slices/305-dino-package-cleanup-ml-kit-and-sherpa.md))
- [owner] **You:** TestFlight look of Dino.

### Slice 299: Slice 261 leftovers ([slice](slices/299-slice-261-leftovers.md))
- [phone] **Device:** First-run picker and Cancel during the list load on emulator and iOS (item 2 follow-up: a cancelled load shows the "Connect to the internet" copy; a distinct copy needs new strings in every language). (live check 2026-11-27: unreachable: no scripted flow stalls the list load, follow-up in Slice 453)

### Slice 298: Slice 264 gallery reorder follow-ups ([slice](slices/298-slice-264-gallery-reorder-follow-ups.md))
- [phone] **Device:** Real drag in both drop event orders (Drop then DropCompleted, and the reverse) on Android, iOS and the Windows head.
- [phone] **Device:** A 1 s hold, and a scroll that cancels the hold, on Android, iOS and the Windows head.
- [phone] **Device:** Hold release after a relayout (rotation) on Android, iOS and the Windows head.

### Slice 288: Dino: the app counts listens (30 s or more) and real listening time ([slice](slices/288-dino-listens-and-listening-time.md))
- [phone] **Device:** The same flow on the Windows head.

### Slice 286: Dino: record your voice in each story language, and hear it when reading in that language ([slice](slices/286-dino-recordings-per-story-language.md))
- [owner] **You:** iPhone: the record-per-language flow.
- [phone] **Device:** Windows head on a real screen: centring of the record intro on a wide window.
- [owner] **You:** The Farsi and English language-name strings and the Record_LanguageUnavailable / Record_LanguageRetry translations, read by the user.
- [phone] **Device:** Emulator store audio had to be seeded because the store was unreachable in the test setup, so the real download path was not covered.

### Slice 290: the reading page's sound line as a pill ([slice](slices/290-dino-reading-sound-pill.md))
- [phone] **Device:** Every pill state on a device against frames 197-199 (lilac Getting pill with the language name, Wi-Fi, Preparing, no sound) and the medium weight.
- [phone] **Device:** The pill's accessibility text: no `ReadingAudioStatus` node appeared in uiautomator dumps during the language flow (part B).
- [phone] **Device:** A Farsi pill whose text wraps (label right-aligned, icon still left).
- [phone] **Device:** The pill's UIA name on the Windows head (VM).

### Slice 285: Dino: changing the app language fetches the books' sound in that language ([slice](slices/285-dino-language-change-fetches-book-audio.md))
- [owner] **You:** iPhone: switch the app language, open a store book, press Play. Sound should play in the new language with no relaunch.
- [phone] **Device:** Windows head: the same.

### Slice 283: Dino: a Sonos music service for the store books ([slice](slices/283-dino-sonos-music-service.md))
- [chrome] **Device:** Live `POST /sonos` after the Worker deploy: getMetadata root lists the languages, getMediaURI returns the track URL.
- [chrome] **Device:** A cold `/sonos` call from a far colo under 1.5 s (EWR first calls 1.6-2.1 s on 2026-11-19).

### Slice 281: Dino: one listening track per store book and language ([slice](slices/281-dino-listening-tracks.md))
- [chrome] **Device:** `curl -I` and a ranged `curl -r 0-1023` on a live track answer 200 and 206 with `audio/mp4`.
- [chrome] **Device:** Every live store book has a track for every language with a pack.

### Slice 279: Dino: recording your own voice, one intro page, then per character or whole page ([slice](slices/279-dino-recording-flow.md))
- [owner] **You:** iPhone: layout and recording.
- [owner] **You:** A real microphone recording on Windows (the VM has none; R opens Windows' microphone consent prompt).
- [phone] **Device:** Backup merge with a whole-page clip and a piece clip on the same page, and orphan files after a same-key restore.
- [phone] **Device:** Intro freeze on a long book: not measured.

### Slice 280: Dino: Farsi speaker first, joined at translation time ([slice](slices/280-dino-farsi-speaker-first-at-translation.md))
- [owner] **You:** On a real phone: the live fa pack after the upload.

### Slice 278: Dino: store books only, parts A and B ([slice](slices/278-dino-store-books-only.md))
- [owner] **You:** Part B: the mobile-data prompt (`ShelfAudioAsk`) on a real cellular-only phone.
- [phone] **Device:** Part B: the beat pausing on screen lock (no scripted flow).
- [phone] **Device:** Part B: a Release build downloads no voice pack.
- [phone] **Device:** No flow asserts the "Get more books" text or that the Add pill is gone.
- [phone] **Device:** The A3 cleanup has no device check: no Debug hook seeds an own book, so deleting an own book on a real install is untried.

### Slice 274: Dino iPhone crash after leaving the grown-ups pages ([slice](slices/274-dino-ios-crash-after-leaving-parent-page.md))
- [phone] **Device:** Android emulator: drag and release the reading-speed slider, and open the Language page; both behave as before (the builder moved the slider's Unloaded handler onto a wrapper and made `LanguagePage.WhenLoadedAsync` wait on RootPage; added by the feedback loop of 2026-11-06).

### Slice 270: Dino iPhone crash, player disposed in its finished callback ([slice](slices/270-dino-ios-player-disposed-in-finished-callback.md))
- [phone] **Device:** Windows clip hand-off: no VM check plays audio clips (a harness gap).
- [phone] **Device:** A parent's recording that ends while paused waits for Resume (`_recordingEndedWhilePaused` in `ReadingSession.cs`): the logic is unit-tested now (Slice 271, `ReadingPauseStateTests`); the device flow is still owed.

### Slice 235: Dino on Windows, part C ([slice](slices/235-dino-windows-window.md))
- [phone] **Device:** Reorder mode on Windows keeps phone padding (review high 6).
- [owner] **You:** Real mouse hover on a real PC.

### Slice 268: Dino: remove "Show original text" from grown-up mode ([slice](slices/268-dino-remove-show-original-text.md))
- [phone] **Device:** The new flow `book-sheet-no-original.yaml` was not shown failing on the old build.
- [phone] **Device:** A book saved with the old flag on, read on a device.

### Slice 267: Dino bugs: books on the shelf keep the old voice; the first-run buttons overlap ([slice](slices/267-dino-old-voice-and-wizard-buttons.md))
- [owner] **You:** The user's phone check (no phone tonight), including the "check first on the user's phone" item (app version, `dino.books.lastupdatecheck`, whether `audio-packs/<book>/` exists): the cause is from code and the emulator reproduction, not from the phone.
- [phone] **Device:** The upgrade-from-previous-release path (previous build, then install over it) is not scripted.
- [phone] **Device:** The layout check at font 1.0 and in landscape on every wizard and first-run screen: only the Record step at font 1.3 has a flow. On an emulator without a microphone the Record step may hide Try it.

### Slice 264: Dino: grown-ups reorder the shelf ([slice](slices/264-dino-gallery-reorder-mode.md))
- [phone] **Device:** The 6-book 1,2,5,3,4,6 example on a device (unit-tested only).

### Slice 262: Dino: adding a second language downloads the shelf books' audio in it ([slice](slices/262-dino-side-language-downloads-book-audio.md))
- [owner] **You:** iOS run of the shelf-audio card and runner (compiles in CI only).
- [phone] **Device:** Kill-and-resume and offline mid-download on a device.
- [owner] **You:** The mobile-data rule ("Download now" only) on a real phone.
- [phone] **Device:** The intro languages step path on a device.

### Slice 261: Dino: no starter books in the app; a first-run book picker ([slice](slices/261-dino-no-starters-first-run-book-picker.md))
- [phone] **Device:** Upgrade from the current release APK keeps the four books (the release signature differs from Debug).
- [owner] **You:** iPhone: the picker and first run.
- [phone] **Device:** The roughly 45 older flows were not all rerun (open-book, back-cover, premade-audio-plays, store-tags and grown-ups-button passed).
- [phone] **Device:** The covers row on the download wait and the intro caption were not checked by eye.

### Slice 260: Dino: tags on store books (Hot, Popular, Recommended, New) ([slice](slices/260-dino-store-tags.md))
- [phone] **Device:** Badge look at 130-200% system font size and the slim (sideways) store card with a tagged book (extra badge row vs title and cover centre).
- [owner] **You:** The Store_Tag_* labels in English and Farsi read by the user.
- [phone] **Device:** Badge text after an app-language switch with the store page open (review high 3, not confirmed).

### Slice 250: Dino usage: playback events and a session id new on every cold start ([slice](slices/250-dino-playback-events-and-session-id.md))
- [phone] **Device:** `seq` on a device (the Debug line has no `seq`; unit-tested only).

### Slice 249: Dino: "Report a problem" in the grown-ups' corner ([slice](slices/249-dino-report-a-problem.md))
- [phone] **Device:** The rolling file on a device (the device run read the lines from logcat).

### Slice 256: Dino: `dump-sentences` works for picture books (Good Enough) ([slice](slices/256-good-enough-sentence-dump.md))
- [phone] **Device:** The dump index matches the app's sentence index on a device during premade-audio playback (Slice 246 C).

### Slice 255: Dino: real animal recordings instead of voiced animal sound words ([slice](slices/255-dino-real-animal-sounds.md))
- [phone] **Device:** Farsi clip on a device (the device run's `lang=fa` launch may not have been applied to the running app).
- [owner] **You:** iPhone: animal clip playback.

### Slice 257: Dino: a quote after "said Mouse." is the Mouse's, not the Lion's ([slice](slices/257-lion-and-mouse-speaker.md))
- [phone] **Device:** On a device, Lion and the Mouse reads the Mouse's line ("I didn't mean to wake you up. ...") in the Mouse's voice (no device run: pure Core logic, covered by the unit tests).

### Slice 248: Dino iOS: leaving a book stops its read, and a new book never drives the old one's voice ([slice](slices/248-dino-stop-read-on-leave-ios.md))
- [owner] **You:** iOS (TestFlight build after the merge): play a book, tap Back: the voice stops. Open another book: its button shows Play; Play reads that book. Swipe back from a playing book: the voice stops.

### Slice 235: Dino on Windows, parts A and B ([slice](slices/235-dino-windows-window.md))
- [owner] **You:** Part B on a real PC: whether PreviewKeyDown gets keys with nothing focused, the Space hold with real key repeats, title bar colours and height, tooltips on a hovering mouse.
- [phone] **Device:** Why Right takes about 2 s after a read starts on the VM (speech start busy on the UI thread?), and whether a real PC has the same delay. 2026-11-13 VM run: did not reproduce (Right 446 ms, Space 460 ms); a real PC is still unchecked.
- [phone] **Device:** Esc on the language page / cast picker through the new back path; held Esc on Settings/Language/Cast/Record pops exactly one page; the gate hold ending on Alt-Tab; title bar colour after the intro and the gate close. 2026-11-13 VM run: Record Esc popped exactly one page; the language page, cast picker and Settings are not covered; title bar ink in the corner and back to cream confirmed, tone after the intro and after the gate close unchecked.
- [phone] **Device:** Tab then Enter on "Back to story".
- [owner] **You:** Opening at 1100 x 760 on a screen larger than 1024 x 768 (the VM clamps it; 2026-11-13: the VM screen is 1024 x 768, cannot run there).
- [phone] **Device:** 150% scaling and the unit of `PreferredMinimumWidth/Height`.
- [owner] **You:** Title bar height and colour by eye.

### Slice 234: Dino tablet held upright, parts A-C ([slice](slices/234-dino-tablet-portrait.md))
- [phone] **Device:** Part C: Record, Cast with the picker, the editor (keyboard down and up), Store and Backup against frames 142-146 by eye (no screenshots; no script reaches those screens with a screenshot step).
- [phone] **Device:** RecordFixedRows / EditorFixedRows on the tablet AVD.
- [phone] **Device:** Part C: Cast picker on a short window or with the keyboard.
- [phone] **Device:** Part B: cards, corner and Settings against frames 136-141 by eye (the flows take no screenshots).
- [owner] **You:** Part B: a drifting finger on the gate hold.
- [phone] **Device:** Part B: a short tablet window (cards cap and scroll).
- [phone] **Device:** Part A: stacked reading page against frames 134, 135 and 150 by eye (no screenshot taken).
- [phone] **Device:** Part A: shelf spacing and header against frame 133.
- [phone] **Device:** Part A: picture only, upright.
- [phone] **Device:** TalkBack order, parts A-C.

### Slice 246: Dino store books read with pre-made audio ([slice](slices/246-dino-store-books-premade-audio.md))
- [session] **Device:** The piper-fa baseline render, on a PC that has the `piper-fa` pack and `sherpa_onnx` installed.
- [session] **Device:** `build-audio` was run on real folders only by the pack run; the zips were not opened.
- [owner] **You:** iPhone: pre-made audio rate and playback, and a real store download fetching live packs.

### Slice 244: Dino iOS crashes ([slice](slices/244-dino-ios-crashes-2026-11-01.md))
- [phone] **Device:** The audio-backed flow `ios-read-audio.yaml` passes reliably on main: two runs on main failed for harness reasons, no app crash (43649148563 StoryTile 60 s wait timed out after skip-first-run; 43650858536 Maestro `XCUITestServerError` Unreachable); the branch run 43646782384 passed. (live check 2026-11-27: unreachable: needs an iPhone or an iOS simulator run, none in this unattended run)
- [owner] **You:** The iPhone 8 run (the user's): read Farsi Three Little Pigs, Back mid-sentence, re-read, background, 3 times.
- [phone] **Device:** The window size on a real phone.
- [phone] **Device:** The first `dinoreader-ios-sim` run: MAUI `AutomationId` to `accessibilityIdentifier`, the "Open in" prompt for dino-test:// links is tapped (prompt text, timing, locale), the `.ips` file naming, the Jetsam `largestProcess` field. (live check 2026-11-27: unreachable: needs an iPhone or an iOS simulator run, none in this unattended run)

### Slice 239: Dino store book without an app build ([slice](slices/239-dino-store-book-without-app-build.md))
- [phone] **Device:** A translated title for a newly added store book on an installed app, with no app update.

### Slice 232 parts A-E: Dino landscape reading screen, shelf, Record, Cast, page text editor, intro, gate and first run ([slice](slices/232-dino-landscape-every-screen.md))
- [phone] **Device:** Part E: screenshots against frames 121, 122, 123, 130 (the flows take none), including check 3 (gate keys uneven in tall rows) and check 4 (long language names in 3 columns).
- [owner] **You:** Part E: the gate sum step sideways (the hold is not scriptable).
- [owner] **You:** iPhone: Part E layouts.
- [phone] **Device:** Part E: TalkBack order.
- [phone] **Device:** Part D: screenshots against frames 117, 118, 124, 128 by eye.
- [phone] **Device:** Part D: TalkBack order in two panes.
- [phone] **Device:** Part D: the hosted pane's right edge inset on a notch phone.
- [phone] **Device:** Part B: the same book stays in view after rotating a shelf with more than 4 books (the emulator shelf has 4, all on screen).
- [phone] **Device:** Part B: tablet landscape and portrait on the tablet AVD.
- [phone] **Device:** Part B: notch inset doubling with `SafeAreaEdges` (high 10).
- [phone] **Device:** Part B: a 360 dp-tall phone, the card and Remove fit.
- [phone] **Device:** Part B: rotating with the sheet open during a Remove confirmation.
- [phone] **Device:** Part B: TalkBack order in the rail.
- [owner] **Device:** Part C: screenshots of each landscape state against frames 116, 119, 120, 129 (the flows take none; the user's eye).
- [phone] **Device:** Part C: a tablet with the keyboard up counts as a phone (short side under 600) and shows the thumbnail.
- [owner] **You:** iPhone: Part C landscape.
- [phone] **Device:** Part C: skipped lows: Cast rotation stops a playing sample, layout jump on focus, 3:4 picture column.
- [phone] **Device:** Tablet landscape (no tablet AVD): the controls row under the card and its left edge.
- [owner] **You:** iPhone landscape.
- [phone] **Device:** VoiceOver / TalkBack order in landscape.
- [owner] **You:** Picture-only chip placement against frame 114 (the user's eye).
- [phone] **Device:** A window 600-700 dp on the short side choosing rail versus row.
- [phone] **Device:** Portrait reading unchanged on iOS, Windows and tablets after `TransportRow`'s base class changed to `StackLayout` (shown only by two Android emulator flows).
- [owner] **You:** Phone side margins (about 14 dp, mock 34/26) and tablet frame 127's title against the mock (the user's eye).

### Slice 230: Dino: say plainly that recordings stay on the phone, and restore brings them back ([slice](slices/230-dino-recordings-copy-and-restore-merge.md))
- [owner] **You:** iOS simulator or TestFlight: back up, restore, recordings come back.
- [owner] **You:** VoiceOver on the new Record/Backup lines.
- [owner] **You:** iPhone: layouts of the new Record/Backup lines.
- [owner] **You:** Read the new Record/Backup strings in Farsi (right to left).

### Slice 229: Dino Done/Continue button on every grown-up page ([slice](slices/229-dino-subpage-done-button.md))
- [phone] **Device:** Page editor header over the crop stage (44 dp pill vs 38).

### Slice 237: Dino back cover (credits page) ([slice](slices/237-dino-back-cover-credits.md))
- [phone] **Device:** Landscape layout of the back cover.
- [phone] **Device:** Farsi right-to-left back cover and Persian digits.
- [phone] **Device:** Old installed app reading the page aloud without breaking.
- [owner] **You:** iPhone: the back cover page.
- [phone] **Device:** Credit-line wrapping in long languages on a narrow phone (estimated, not measured).

### Slice 228: Dino grown-ups' corner clarity ([slice](slices/228-dino-grownups-corner-clarity.md))
- [phone] **Device:** Corner tiles and title truncation on a small phone and on a tablet.
- [owner] **You:** The Farsi strings of the grown-ups' corner, read by the user.
- [phone] **Device:** The intro read's return being cancelled by Play/pause, skip or a page turn (the emulator recheck tapped the page surface, which is not one of those).
- [phone] **Device:** The 48 dp close button on a device (changed after the last emulator run; check pass only).

### Slice 231: Dino Record page shows the page's words ([slice](slices/231-dino-record-page-shows-words.md))
- [phone] **Device:** An RTL/Farsi book on the Record page.

### Slice 226: Dino Settings rows and page text ([slice](slices/226-dino-settings-rows-review-leftovers.md))
- [phone] **Device:** TalkBack / VoiceOver hearing each reading page once.
- [owner] **You:** iOS: a settings row with the button text left null keeps its height and look, and `settings-reading-speed-row.yaml` on iOS needs an in-app back step instead of `- back` ([slice](slices/226-dino-settings-rows-review-leftovers.md)).

### Slice 203: Dino on the Microsoft Store: turn on automatic submissions and link it ([slice](slices/203-dino-microsoft-store-live.md))

### Slice 197: dino download now tap dropped ([slice](slices/197-dino-download-now-tap-dropped.md))
- [phone] **Device:** Slice 197 (PR #583): no stale "Downloading" status after the download ends; notification Cancel mid-download then nothing restarts; iOS TestFlight ... [full text](notes/2026-10-30-outstanding-detail.md#o016) ([slice](slices/197-dino-download-now-tap-dropped.md))

### Slice 196: dino default view per book ([slice](slices/196-dino-default-view-per-book.md))
- [phone] **Device:** Slice 196 (PR #575): Good Enough pages 8 and 10 by eye; landscape layout; ... [full text](notes/2026-10-30-outstanding-detail.md#o008) ([slice](slices/196-dino-default-view-per-book.md))

### Slice 192: dino store credits in every language ([slice](slices/192-dino-store-credits-in-every-language.md))
- [session] **Device:** Slice 192 (PR #582): live `meta.json` refreshed (all 15 books in `/api/stories` carry `creditsByLanguage`, checked 2026-10-30); ... [full text](notes/2026-10-30-outstanding-detail.md#o012) ([slice](slices/192-dino-store-credits-in-every-language.md))

### Slice 181: dino books update from store ([slice](slices/181-dino-books-update-from-store.md))
- [phone] **Device:** Slice 181 (PR #543): a recording carried over a swap; a store-downloaded book with edited text getting an update; a crash during the apply itself ... [full text](notes/2026-10-30-outstanding-detail.md#o005) ([slice](slices/181-dino-books-update-from-store.md))

### Slice 173: dino kid story languages ([slice](slices/173-dino-kid-story-languages.md))
- [owner] **You:** Slice 173 (PR #525): mobile-data "Wait for Wi-Fi / Download now" prompt not seen (emulator has Wi-Fi); a persisted no-pack language ... [full text](notes/2026-10-30-outstanding-detail.md#o021) ([slice](slices/173-dino-kid-story-languages.md))

### Slice 171: dino text card shows whole page ([slice](slices/171-dino-text-card-shows-whole-page.md))
- [owner] **You:** iPhone: the line reader and the card's scroll for English and Farsi text.

### Slice 159: dino everything in my language ([slice](slices/159-dino-everything-in-my-language.md))
- [owner] **You:** Read the ShippedTitles and the 7 new resx keys in English and Farsi.

### Slice 157: Dino: book content and the book builder out of the test project ([slice](slices/157-dino-book-content-out-of-tests.md))
- [session] **Device:** Slice 157 (book content out of the tests, PR #475): first main run of `publish-store` with the new builder; `ShippedTitles` edits do not reach live ... [full text](notes/2026-10-30-outstanding-detail.md#o046)

### Slice 155: dino first run intro wizard ([slice](slices/155-dino-first-run-intro-wizard.md))
- [phone] **Device:** Slice 155: offline store step; microphone prompt on the Record step; Windows variant (title, Picture badge); iOS modal presentation; large-font ... [full text](notes/2026-10-30-outstanding-detail.md#o073) ([slice](slices/155-dino-first-run-intro-wizard.md))

### Slice 151: dino three view modes ([slice](slices/151-dino-three-view-modes.md))
- [phone] **Device:** Night-theme segment colours unchanged on the Windows head and iPhone (photo import and native read dropped).

### Slice 133: dino anonymous usage counts ([slice](slices/133-dino-anonymous-usage-counts.md))
- [session] **Device:** Slice 133: telemetry on and rows arrive in `dino_usage` since 2026-10-27; settings off sends nothing still to check on a device; no rate limit or ... [full text](notes/2026-10-30-outstanding-detail.md#o066) ([slice](slices/133-dino-anonymous-usage-counts.md))

### Slice 126: dino keep screen awake ([slice](slices/126-dino-keep-screen-awake.md))
- [phone] **Device:** Slice 126: emulator PASS (screen stays on while playing, sleeps after pause and at the end); screen awake on a real phone PASS while playing (end ... [full text](notes/2026-10-30-outstanding-detail.md#o063) ([slice](slices/126-dino-keep-screen-awake.md))

### Slice 122: dino text panel 30 and switch ([slice](slices/122-dino-text-panel-30-and-switch.md))
- [owner] **You:** On the user's phone: pigs upright and sideways (panel at most 30%); PDF import defaults the switch off; a wide picture sideways.

### Slice 121: dino illustrate store stories ([slice](slices/121-dino-illustrate-store-stories.md))
- [owner] **You:** Slice 121: check the illustrated books on a phone (pictures, text panel, voices) ([slice](slices/121-dino-illustrate-store-stories.md)).

### Slice 112: dino store age author filters ([slice](slices/112-dino-store-age-author-filters.md))
- [owner] **You:** Read the new store age/author filter strings in English and Farsi.

### Slice 111: dino parent recordings ([slice](slices/111-dino-parent-recordings.md))
- [phone] **Device:** Record/replay on the S21, iPhone and Windows head; mic permission denial; backup round trip.

### Slice 96: dino layout never mirrors ([slice](slices/96-dino-layout-never-mirrors.md))
- [phone] **Device:** Emulator in Farsi (gallery, gate, wizard, store, backup, reader, swipe direction, highlight on RTL lines); Windows head gallery and reader.

## Shared

### Slice 451: A Debug hook that makes the user-report endpoint refuse ([slice](slices/451-debug-hook-user-report-endpoint-refuses.md))
- [session] The flow `.maestro/dino/user-report-not-kept-usage-off.yaml` has not been run against the old behaviour (without the not-kept text) to show it fails there; that needs a second build and D: was under 2 GB.
- [session] No unit tests exist for `UserReportRefusePlan` (they need a `Compile` link in `DinoReader.Tests.csproj`, which parked draft PR #1437's worktree edits); add them once that PR is settled.

### Slice 406: Feedback loop 2026-11-17-2105 leftovers ([slice](slices/406-feedback-loop-2026-11-17-2105-leftovers.md))
- [cloud] The `flow-lint` job starts on the first src-only PR after PR #1443 merges and the full self-test set is skipped (the job gating has no self-test). (live check 2026-11-27: unreachable: no src-only PR since #1443; #1448 and #1450 also changed .maestro/, so the full set rightly ran)

### Slice 442: Phone, emulator and iOS harness follow-ups ([slice](slices/442-phone-emulator-ios-harness-followups.md))
- [cloud] The `ios-sim-*.sh` and `dinoreader-ios-sim.yml` changes (budget guard, `kill_driver`, settle seconds, 1080 s step budget) run on the CI Mac: check the next iOS simulator workflow run. (live check 2026-11-27: unreachable: dinoreader-ios-sim.yml is manual and has no run since the change)
- [session] `reset-dino-language.ps1` exit 2 path (Dino dev app not installed) has not been run.
- [session] `store-screenshots.ps1` demo-mode early stop and exit 8 on an emulator that will not start have not been run.

### Slice 448: tools/phone runs the .maestro/dino flows on a real phone ([slice](slices/448-tools-phone-runs-maestro-dino-flows.md))
- [phone] The three failing store flows (`store-restore`, `paid-books`, `subscription`) pass on the S21 after Slice 465 (S21 run 4: 1 of 4 passed). (live check 2026-11-27: failed, follow-up in Slice 448)

### Slice 343: Listening page: four Farsi voices ([slice](slices/343-four-farsi-voices.md))
- [owner] **You:** ear check of the built comma fix (ganji-2, gyro-2, gyro-3) on a listening page after it ships.

### Slice 393: Feedback loop 2026-11-14-1547 leftovers ([slice](slices/393-feedback-loop-2026-11-14-1547-leftovers.md))
- [phone] **Device:** `tools/phone/test-phone-lib.ps1` and a real-phone preflight after #1242's lease changes (a foreign lease gives `PREFLIGHT WARN` exit 3, `Invoke-LeaseFileOnly`, owner compare `-ceq`): never run, the lease was held by another session (feedback loop 2026-11-17-2105).
- [phone] **Device:** item 13: the VM run of `-Check dino-reorder` through its `# after:` chain (the VM needs 8 GB free; 3.2 GB was free).
- [phone] **Device:** item 14: the first shot's status bar (10:00, no icons) after `install-debug.ps1 -BuildOnly` for Dino (the 2026-11-18 run stopped at a run-flows cache miss), then the retake of the RTST and Dino phone and tablet shots.
- [session] **Device:** item 23: a macOS run of `store-screenshots.yml` and `dinoreader-ios-sim.yml` (whether the warm-up leaves the driver warm for `maestro test`). (live check 2026-11-27: unreachable: needs an iPhone or an iOS simulator run, none in this unattended run)

### Slice 379: Everything to the latest stable, .NET 11 ([slice](slices/379-everything-to-latest-stable.md))
- [owner] **Device:** RTST cold start on iOS under the scene lifecycle (MT7199 fix; RTST only compiled on CI, Dino's cold start is shown), on an iOS 27 and an older iOS device or simulator; the Debug test hooks firing through `SceneOpenUrl`, `SceneWillConnect` and `SceneOnActivated` in RTST; file "Open in" / share hand-off (none is declared today).
- [owner] **You:** background audio continuing with the screen locked (Slice 49 / Dino story) on an iPhone; the pack-download paused notification and the downloader's active/inactive stall clock under scenes; a Dino StoreKit purchase sheet presenting; `VisionKitDocumentScanner` presenting from `Platform.GetCurrentUIViewController()` in RTST (`reviews/pr-1176-check2.md` #1).
- [session] **Device:** `store-screenshots.yml` and the iPad 13" / iPhone 6.5" types on the `xcode-27` image: the 2026-11-16 simulator run booted an iOS 27.2 iPhone only. (live check 2026-11-27: unreachable: store-screenshots.yml has no run since the 2026-11-16 one)
- [phone] **Device:** capture a CoreCLR native crash on the emulator and write its signature into `tools/emulator/speed.ps1`'s launch-crash retry, which still matches only Mono (`TODO(379B device run)`).
- [phone] **Device:** phone `-Variant aot` (`tools/phone`): `PublishReadyToRun` and `PublishTrimmed` on a Debug `dotnet build` may not ReadyToRun-compile, and trimming may strip the Debug test hooks; one `-Build -Variant aot` phone run settles it.
- [session] **Device:** `src/Shared/AndroidRelease.props` R8 guard still accepts the Android SDK pack floor 36.1.118: raise it to the 37.x pack once a release build shows the exact version. (live check 2026-11-27: failed, follow-up in Slice 379)
- [phone] **Device:** Dino store list under CoreCLR: the 2026-11-16 emulator run logged `[Usage] fallback store:store_unreachable` on the shelf and ran no store flow; run a store flow with its stub (`store-tags`). Slice 393 lists `store_unreachable` as a tooling item.
- [phone] **Device:** start-time and speed under CoreCLR against the Mono numbers (`tools/emulator/speed.ps1`).
- [owner] **Device:** Dino's per-page iOS status-bar icon colour (`StatusBarPageViewController`, Slice 325 A4) on the handler-based NavigationPage: the icons still follow each page.
- [owner] **Device:** iOS NavigationPage/TabbedPage handlers (Dino); Windows CollectionView2 (opted out for Dino, so unchanged).
- [session] **Device:** Dependabot nuget/npm PRs, including versions held in `.props` files, after the first weekly cycle. (live check 2026-11-27: unreachable: no Dependabot PR has bumped a version held in a .props file yet; #1332-#1334 bumped others)
- [phone] **Device:** Windows tray read in the Hyper-V VM.
- [phone] **Device:** RTST Scan flow.

### Slice 389: Tooling: the emulator preflight picks an installed JDK ([slice](slices/389-tooling-emulator-preflight-picks-an-installed-jdk.md))
- [phone] **Device:** `run-flows.ps1` end to end with a removed `JAVA_HOME` (cached build, then `-NoBuild`): the `Confirm-EmulatorPreflight` wiring prints the chosen JDK, and with no JDK at all it exits 7. The pick and the Maestro preflight themselves passed on emulator-5554.

### Slice 388: Shared tests: `PackDownloadLogTests` capture leaks another test's line ([slice](slices/388-shared-pack-download-log-capture-per-test.md))

### Slice 378: Build warnings gate ([slice](slices/378-build-warnings-gate.md))
- [phone] **Device:** The scan-crop fix on an API 28-30 emulator image: no Android 9-11 system image is installed, and no flow scans a real page, so a scan flow is needed as well.
- [session] **Device:** The first release run after merge: no Node 20 warnings, `tracks:` accepted by upload-google-play v1.1.5, GitTools v4.7.0 `configFilePath` (including `windows.yml`'s restored config), setup-node v7 caching. (live check 2026-11-27: failed, follow-up in Slice 378)
- [owner] **Device:** A TestFlight build with the gate on.
- [phone] **Device:** Android 36 `ToggleSwitch` `CheckedState` (`ToggleSwitchAccessibility.cs`).

### Slice 317: Feedback loop 2026-11-08-0106 leftovers ([slice](slices/317-feedback-loop-2026-11-08-0106-leftovers.md))
- [phone] Item 3: `pack-failure-network` passes twice in a row on emulator-5554 (the Kotlin error is undiagnosed; the 60 s timeout is untested). (live check 2026-11-27: failed, follow-up in Slice 317)
- [session] The commit retry in `store-submit.yml` (a failed `msstore submission publish` is run once more after 60 s) has not run on a runner; what `submission publish` does when the draft is already committed or missing is undocumented and unverified. (live check 2026-11-27: unreachable: store-submit.yml has not run since 2026-09-01)

### Slice 376: Feedback loop 2026-11-13-1149 leftovers ([slice](slices/376-feedback-loop-2026-11-13-1149-leftovers.md))
- [phone] Item 2: a run with a real stale onboarding flag (the run saw `pending=none`, so the cause stays likely).
- [phone] Item 3: that `farsi-ezafe-reads` fails on a build before #1098.
- [phone] Item 5: one emulator download row showing the pack version.
- [phone] `premade-audio-past-roar` fixture clash (a real fa pack replaces `fixture-13.m4a` after `pm clear`): untouched, still open as item 2's second half.

### Slice 354: Feedback loop 2026-11-12-1337 leftovers ([slice](slices/354-feedback-loop-2026-11-12-1337-leftovers.md))
- [session] `rerun-no-runner.yml` on a real release run with a no-runner cancel. (live check 2026-11-27: unreachable: no release run has had a no-runner cancel; all 5 runs skipped)
- [session] The TestFlight verdict on a real 500-then-success upload. (live check 2026-11-27: unreachable: every TestFlight upload so far succeeded on the first try)
- [owner] `gemini-grab.js` on a non-English Gemini page.
- [phone] Item 9: the Mono SIGSEGV at cold start; no crash-store match, needs a device run (10 cold starts).

### Slice 377: Feedback loop 2026-11-14-0726 leftovers ([slice](slices/377-feedback-loop-2026-11-14-0726-leftovers.md))
- [session] **Device:** item 2: a live read-back of one non-English Microsoft Store listing after certification (the store name in the text; the `pt-br` listing created beside any `pt-pt`; the `submission update` payload shape). (live check 2026-11-27: unreachable: tonight's release went out at 22:32 UTC, and Store certification hasn't finished)
- [phone] **Device:** item 16: first install on an unmetered link, turn the link metered mid-download: the standard pack arrives and the log line shows; a brief offline gap must not stop the high-res fetch.

### Slice 193: German store listings ([slice](slices/193-german-store-listings.md))
- [owner] **You:** native German read of both files (machine-written).
- [session] **Device:** after the next RTST release's certification, the Partner Center submission shows the German listing with the `docs/store-listings/de.md` text (it replaces the existing `de` listing). (live check 2026-11-27: unreachable: tonight's release went out at 22:32 UTC, and Store certification hasn't finished)

### Slice 243: Crash reports: everything missing to explain a crash ([slice](slices/243-crash-report-gaps.md))
- [session] **Device:** a Release iOS build in CI attaches the dSYM zip to its GitHub Release; `tools/crashes/symbolicate` resolves a frame of a test crash (`crash-test`, Slice 227) of that build to a C# method (items 1-2, merged #1122 on unit tests and review only). (live check 2026-11-27: unreachable: the dSYM zips are on dino-v1.136.8, but no crash-test crash of a Release iOS build exists to symbolicate)
- [phone] **Device:** the rolling log of a test crash on the Android emulator and an iPhone shows the page, Back and lifecycle lines, and the report carries the new build, UUID, memory and foreground fields (items 5-7).

### Slice 371: Folders grouped by product ([slice](slices/371-folders-by-product.md))
- [phone] **Device:** Windows VM run (`tools/desktop-vm`) from the new paths.

### Slice 370: Give the shared code a neutral name ([slice](slices/370-rename-shared-code.md))

- [owner] iOS simulator upgrade test (old build, setting, pack, install the renamed build over it): no Mac available.

### Slice 374: Store listings in 13 more languages ([slice](slices/374-store-listings-13-languages.md))

Machine drafts; none of these holds a push. Each line covers the Dino file (`docs/store-listings/dino/<lang>.md`) and the RTST file (`docs/store-listings/<lang>.md`).

- [session] **Release:** Microsoft Store listings in 13 languages reach both apps' Store pages after the next release (read back with `msstore submission get <id>` after the first release run that calls `store-submit.yml`; check the keys, `pt-br`, no duplicate `fr`/`fr-fr`, and that the run's log shows no `::warning::` from the listing step). Built, not yet run live. (live check 2026-11-27: unreachable: tonight's release went out at 22:32 UTC, and Store certification hasn't finished)
- [owner] **You:** native read of the Arabic store listings (`ar`).
- [owner] **You:** native read of the Chinese (Simplified) store listings (`zh`).
- [owner] **You:** native read of the Danish store listings (`da`).
- [owner] **You:** native read of the Dutch store listings (`nl`).
- [owner] **You:** native read of the Farsi store listings (`fa`; Google Play and Microsoft Store only, the App Store has no Persian).
- [owner] **You:** native read of the Finnish store listings (`fi`).
- [owner] **You:** native read of the French store listings (`fr`).
- [owner] **You:** native read of the Japanese store listings (`ja`).
- [owner] **You:** native read of the Norwegian store listings (`nb`).
- [owner] **You:** native read of the Portuguese store listings (`pt`, Brazilian).
- [owner] **You:** native read of the Russian store listings (`ru`).
- [owner] **You:** native read of the Spanish store listings (`es`).
- [owner] **You:** native read of the Swedish store listings (`sv`).

### Slice 369: One release workflow per product ([slice](slices/369-release-workflow-per-product.md))

- [session] First Dino-only merge runs only `release-dino.yml` and cuts only a `dino-v` tag. (live check 2026-11-27: unreachable: no Dino-only merge has cut a release yet; every release run so far was a workflow_dispatch)
- [session] First RTST-only merge runs only `release-rtst.yml` and cuts only an `rtst-v` tag. (live check 2026-11-27: unreachable: no RTST-only merge has cut a release yet; every release run so far was a workflow_dispatch)

### Slice 366: Plan split by product ([slice](slices/366-plan-split-by-product.md))


### Slice 372: S21 check fixes ([slice](slices/372-s21-check-fixes-2026-11-13.md))

- [phone] **Device:** left on the phone by the runner: `/sdcard/Download/rtst-packs-aside/uk.sirous.readthestupidtext.dbg/packs-20261113172728.tar` (the dev app's own pack backup).
- [phone] **Device:** why the heal survived TERM is unknown (now harmless: the pid-file guard).
- [phone] **Device:** voiced book-end timing measured once (159 s; timeout 240 s).

### Slice 368: Shared tests in their own project ([slice](slices/368-shared-tests-project.md))


### Slice 365: `worker/catalogue.test.mjs` LF test on a Windows checkout ([slice](slices/365-catalogue-lf-test-windows.md))


### Slice 357: Server errors the user can see: standard messages per status ([slice](slices/357-server-error-messages.md))
- [phone] **Device:** RTST pack-failure text on a device: no scripted hook for a pack server error.
- [phone] **Device:** Dino shop list-failure panel text on a device: no list-error hook.
- [phone] **Device:** Windows VM: a refused Get on the Dino Windows head.
- [owner] **You:** iOS TestFlight: one refused Get.
- [owner] **You:** Native read of the new strings in the 14 non-English languages.
- [phone] **Device:** Dino shop: a failed Get while a background book update downloads the same book at the same moment (`StoreClient._downloadErrors` is keyed by book id; check3 #2).
- [owner] **You:** Part D: the `minVersion` change to `1.124.0` is open until paid books are switched on for everyone.

### Slice 295: Bump MauiVersion for dotnet/maui PR 38662 ([slice](slices/295-bump-maui-version-for-pr-38662.md))
- [phone] **Device:** RTST emulator flows on MAUI 10.0.130: not reached in the one emulator run (Dino only).
- [owner] **You:** TestFlight iPhone check of the earlier Slice 274 crash path on a release build: open a parent page, leave it (language page), background, return; no crash. Needs a TestFlight build after merge; Slice 295 work item 5 (remove the Slice 274 workaround) waits on it.

### Slice 353: CI: keep the .NET 10 SDK on runners with .NET 11, fix the Dino Windows VM test package ([slice](slices/353-ci-sdk-pin-and-vm-test-package.md))
- [phone] **Device:** Dino VM checks of Slice 352 (desktop), now that `Build-TestPackage.ps1 -App dino` builds.

### Slice 339: RTST real-phone audio timing ([slice](slices/339-real-phone-audio-timing.md))
- [phone] **Device:** S21: run `tools/phone/audio-timing.ps1 -App rtst` and check the 1x gaps between chunks against the script's own limit (a scrcpy recording on 2026-11-08 showed 1.1-1.5 s gaps).

### Slice 320: Device-check fixes and missing test hooks from the 2026-11-08 run ([slice](slices/320-device-check-tooling-from-2026-11-08-run.md))
- [phone] **Device:** RTST `stall-return-overlay` failed twice (the hook does not block the reader); moved to [Slice 345](slices/345-rtst-stall-hook-blocks-reader.md).
- [owner] **You:** iOS: the new hooks are untried (no Mac, no iOS compile was possible).

### Slice 147: iOS review cadence: one review at a time, the newest build, never cancel ([slice](slices/147-ios-review-cadence.md))
- [owner] **You:** the first successful `appstore-submit.yml` run (manual; its only two runs, 2026-10-30, concluded failure): it should report the listing items Apple still needs (screenshots, privacy, age rating). The first scheduled `testflight-external.yml` run already succeeded.

### Slice 319: Fixes from the 2026-11-08 device run ([slice](slices/319-device-run-2026-11-08-fixes.md))
- [phone] **Device:** Tablet emulator look of the Reorder layer after the geometry redesign (shared code; not run).

### Slice 316: Gaps found by the unverified-checks triage ([slice](slices/316-unverified-triage-gaps.md))
- [phone] **Device:** That a later edit clears the Review pick on the reader page: no flow step yet.

### Slice 291: Store listings and screenshots for both apps ([slice](slices/291-store-listings-and-screenshots.md))
- [owner] **You:** A native read (Farsi) of the translated Microsoft Store notes and usage-counts bullets before the Partner Center push.
- [session] **Device:** Partner Center RTST English read-back after the next RTST release (item 3). (live check 2026-11-27: unreachable: tonight's release went out at 22:32 UTC, and Store certification hasn't finished)
- [owner] **You:** A native read of the 14 machine-translated RTST listings (ar, da, de, es, fi, fr, ja, nb, nl, pt, ru, sv, zh, fa), already live on Play and the App Store (fa on Play only).
- [session] **Device:** The RTST iOS store shots: the RTST flows on all three devices failed in the 2026-11-14 run (`PackStatusLabel is not visible... FAILED`) and have not been retaken. (Dino's iPhone and iPad sets are done, run 43748250465, 2026-11-15.) (live check 2026-11-27: unreachable: needs an iPhone or an iOS simulator run, none in this unattended run)
- [chrome] **Device:** Whether App Store localizations without screenshots show the en-US set (Apple's docs do not say); read it on the App Store page once Dino is live.
- [session] **Device:** The resume path of `store-screenshots.yml` (no job failed in run 43761667169), its `resume_run` input, and the RTST single-flow unit.
- [owner] **You:** A native read of the 15 machine-translated App Store subscription texts (Dino; also Slice 384).

### Slice 297: Slice 267 follow-ups ([slice](slices/297-slice-267-follow-ups.md))
- [owner] **You:** iOS wizard layout (no Mac or iPhone tonight).

### Slice 300: Kept problem report with the crash switch off ([slice](slices/300-user-report-kept-while-crash-switch-off.md))
- [owner] **You:** iOS device: the same shared code, not run on a device.

### Slice 308: Slice 225 run-checks -Serial and failed-build cause ([slice](slices/308-slice-225-run-checks-serial-and-build-cause.md))
- [phone] **Device:** Phone `paused-notification` before the `Get-PackageUid` fix: the shadowing left the dev app's uid unread, so earlier phone PASS results may never have blocked the network; re-run it on the phone once (feedback loop 2026-11-07-1412, [Slice 315](slices/315-feedback-loop-2026-11-07-1412-leftovers.md) item 11).

### Slice 284: Final loop leftovers, night 2026-11-06-1250 ([slice](slices/284-final-loop-leftovers-night-2026-11-06-1250.md))

### Slice 302: Slice 239 leftovers ([slice](slices/302-slice-239-leftovers.md))
- [phone] **Device:** Item 1 was closed from a code read only: the "renamed store package downloaded twice" report was not reproduced on a device run, so a double download on a fresh install is not ruled out.

### Slice 275: Leftovers from night 2026-11-06-0042, parts 1 to 3 ([slice](slices/275-night-2026-11-06-0042-leftovers.md))
- [phone] **Device:** Item 7: no emulator flow cancels an audio pack download and checks that `cancelled` is sent (there is no test hook to stall a pack fetch); only unit and Worker tests cover it.

### Slice 271: Leftovers from night 2026-11-05-1823 ([slice](slices/271-night-2026-11-05-1823-leftovers.md))
- [phone] **Device:** A Windows VM check that plays a pre-made-audio book past two clip hand-offs (item 2, VM half): the VM package is a Release build with no Debug test hooks, so no fixture audio pack can be installed there; it needs Debug test hooks (or an install path) in the VM package first.

### Slice 269: Leftovers from night 2026-11-04-1441 ([slice](slices/269-feedback-loop-2026-11-04-1441-leftovers.md))
- [phone] **Device:** Emulator cold-boot retry path of the preflight (not triggered in the run; kill the Maestro driver after a snapshot boot).

### Slice 259: Leftovers from night 2026-11-03-2349 ([slice](slices/259-night-2026-11-03-2349-leftovers.md))
- [phone] **Device:** Item 1: the mid-book wait on a slow real phone (overlay when above 500 ms); the emulator measured 540 ms to first sound with no overlay.

### Slice 253: Final-loop leftovers, night 2026-11-01-2355 ([slice](slices/253-final-loop-leftovers-night-2026-11-01-2355.md))
- [phone] **Device:** Item 1: a forced "System UI isn't responding" dialog on the tablet AVD with the dialog guard, a flow passing with no brief text (not run).
- [phone] **Device:** Item 1: the dialog guard's restore after a lease loss (a killed run leaves `hide_error_dialogs=1`, cleared by the next `run-flows.ps1`; not exercised on a device).
- [phone] **Device:** Item 6: before/after seconds per slow flow (command in the slice file's Built block, item 6; not run).

### Slice 242: Round 4 leftovers, night 2026-11-01-0859 ([slice](slices/242-round-4-leftovers-night-2026-11-01-0859.md))
- [phone] **Device:** Item 6: the 34/26 dp phone insets (no log or tree value; screenshots only).
- [phone] **Device:** Item 6: the tablet screenshot against frame 127, by eye only.

### Slice 238: packs.yml defaults match the catalog ([slice](slices/238-packs-workflow-defaults-match-catalog.md))
- [phone] **Device:** What a lower version does on phones: Slice 220's record says a re-download is decided by `PackVersions.IsNewer` only, so a phone holding piper-nb 2 would most likely keep it and a fresh install would get 1; not checked on a device.

### Slice 236: phone runner gaps, night 2026-11-01-0859 ([slice](slices/236-phone-runner-gaps-night-2026-11-01-0859.md))
- [phone] **Device:** Item 6 with no lease held: `preflight.ps1 -Fix` then `-Restore` back to 0.
- [phone] **Device:** A mid-run `CHANGED:` stop with no record.
- [phone] **Device:** The `dumpsys platform_compat` override-line wording (`Get-CompatOverrideLines` matching).
- [phone] **Device:** The 30 s device-side restore cancel timeout warning ("did not finish in 30 s").

### Slice 245: sherpa-onnx one version ([slice](slices/245-sherpa-onnx-one-version.md))
- [phone] **Device:** Translation with managed onnxruntime 1.28.0 against native 1.28.2 on Android and Windows (scan and OCR dropped).

### Slice 241: Faster, quieter device tests ([slice](slices/241-device-test-speed.md))
- [phone] **Device:** Item 1, crash fail-fast: a flow list with a launch crash prints one FAIL with the crash lines and NOTRUN (naming the crash) for the rest in under one flow's time.
- [phone] **Device:** Item 3, timing: a cache-miss run's total is build time, not build plus boot (the `Cache miss` line itself passed on 2026-11-01; the time was not recorded).
- [owner] **You:** The iOS stall watcher on a device.
- [phone] **Device:** The `-Split` two-emulator path.

### Slice 225: Device-test tooling leftovers, night 2026-10-31-1301 ([slice](slices/225-device-tooling-leftovers-night-2026-10-31-1301.md))
- [phone] **Device:** Paused-notification checks on the S21 (needs `run-checks.ps1 -Build -App rtst` first; cache miss). Passed 2026-11-13: `paused-notification-network` in 57 s (title "Download paused. Tap to finish.", text "English voice (124 MB) · 0% done"; tap resumed `PACKDL start pack=supertonic-3 offset=990290`; notice cleared). Still open: the other text variants, the repost flags check (Slice 372).
- [phone] **Device:** `stay_on` restore when the user changed the value (self-tested only).
- [phone] **Device:** Item 16 in the Debug app: the `[BookUpdate] ... forced entry dropped, recorded version put back` and `forced-apply mark expired` lines.
- [phone] **Device:** Item 21: a flow reading a TalkBack focus event (TalkBack on and off passed; the focus event was not read).

### Slice 221: Device-test tooling leftovers from the 2026-10-30-2100 night, round 1 ([slice](slices/221-device-test-tooling-leftovers-night-1.md))
- [phone] **Device:** Item 6 on the real phone: two timed runs with the same owner, the second exits 4.
- [phone] **Device:** Item 3 seen in a device-test run log.
- [phone] **Device:** `adb uninstall` stderr under Windows PowerShell 5.1 with `ErrorActionPreference` Stop (`Remove-StaleDevPackages`, `tools/emulator/common.ps1:151`) could abort the install instead of warning.

### Slice 224: Phone runner build-cache gaps ([slice](slices/224-phone-runner-build-cache-gaps.md))
- [phone] **Device:** A real phone run of the runner with a dead-builder status file, and with `-NoBuild` on a warm cache (no device run this wave; host tooling only).

### Slice 233: paused notice same on Android and iOS ([slice](slices/233-paused-notice-same-on-android-and-ios.md))
- [phone] **Device:** A real network loss: the device run used the Debug-only `drop-pack-network` hook, not a real loss.

### Slice 220: Kokoro silent short lines on arm64 ([slice](slices/220-kokoro-silent-short-lines-arm64.md))
- [phone] **Device:** The 68-read Slice 210 set.
- [phone] **Device:** A fresh install's rebuilt non-kokoro packs (rebuilt in packs-11 at the same versions, no device run).

### Slice 216: phone tooling leftovers ([slice](slices/216-phone-tooling-leftovers.md))
- [phone] **Device:** Slice 216 (PR #637): item 11 cold-start rerun and the numbers in Slices 185 and 214; path-length estimates unmeasured; `Get-PhoneStamp` not device-run ([slice](slices/216-phone-tooling-leftovers.md)).

### Slice 215: device guard gaps ([slice](slices/215-device-guard-gaps.md))
- [phone] **Device:** Slice 215 (PR #639): site image retake (VM sample read never leaves GeneratingAudio with the copied pack; `site/img/control-panel.webp` not replaced; watermark crop); `sentence-highlight` first-run failure after a fresh install; `test-activity.ps1 -DeviceKillCheck` script path not run ([slice](slices/215-device-guard-gaps.md)).

### Slice 213: phone check runner ([slice](slices/213-phone-check-runner.md))
- [phone] **Device:** Slice 213 (PR #610): a cold-cache run where the AOT build fails or stalls (the FAIL path); a run while another session holds the lease; the ... [full text](notes/2026-10-30-outstanding-detail.md#o017) ([slice](slices/213-phone-check-runner.md))

### Slice 208: paused pack downloads tell you and finish ([slice](slices/208-paused-pack-downloads-tell-you-and-finish.md))
- [phone] **Device:** Slice 208 (PR #646) partly verified on the S21 on 2026-11-13 (`paused-notification-network` passed in 57 s: title "Download paused. Tap to finish.", text "English voice (124 MB) · 0% done", tap resumed `PACKDL start pack=supertonic-3 offset=990290`, notice cleared). Still open: the other notification text variants, the stale partial resume/delete, the cold-run marker, Dino first download, iOS and the `device_config` time-limit variant (never passed on an emulator, not run). Earlier list: the S21 Verification list (paused notice within ~35 s and tap resumes; notification text; stale partial resumes or is deleted); Dino storyteller first download shows no ask sheet and grown-up Settings shows it once (emulator); iOS TestFlight hand check; stale-partial-launch first cold run marker not seen within 40 s (not diagnosed); the `paused-notification` check itself was reworked by Slice 233 (Debug hook `drop-pack-network`, no netpolicy) and passed on the S21 on 2026-11-01, so its earlier netpolicy and detached-restore doubts no longer apply. [208-paused-pack-downloads-tell-you-and-finish.md](slices/208-paused-pack-downloads-tell-you-and-finish.md)

### Slice 206: phone run scripts ([slice](slices/206-phone-run-scripts.md))
- [phone] **Device:** Slice 206 (PR #594): `preflight.ps1` reading a PIN or pattern lock as NO-GO; `reset-dev-app.ps1 -Level reinstall`; Dino resets (`-App dino`); ... [full text](notes/2026-10-30-outstanding-detail.md#o001) ([slice](slices/206-phone-run-scripts.md))

### Slice 204: cancelled voice pack stays cancelled ([slice](slices/204-cancelled-voice-pack-stays-cancelled.md))
- [owner] **You:** iPhone: Dino cancelled voice pack stays cancelled; read the Stopped strings in English and Farsi.

### Slice 200: Check the live usage data after the store rollouts ([slice](slices/200-live-usage-data-checks.md))
- [session] **Device:** Slice 200 (live data checks): re-run the report after more real traffic (under 10 events per group everywhere on 2026-10-30); no rows yet for voice ... [full text](notes/2026-10-30-outstanding-detail.md#o013)

### Slice 183: pack downloads keep going when locked ([slice](slices/183-pack-downloads-keep-going-when-locked.md))
- [owner] **You:** Slice 183 (PR #549): iOS background session on a device (TestFlight); Dino storyteller first-download ask deferral; the dataSync timeout's silent ... [full text](notes/2026-10-30-outstanding-detail.md#o018) ([slice](slices/183-pack-downloads-keep-going-when-locked.md))
- [owner] **You:** Slice 183: native review of the new strings (13 machine drafts per app).

### Slice 182: pack download resumes by itself ([slice](slices/182-pack-download-resumes-by-itself.md))
- [owner] **You:** "Waiting for Wi-Fi" on a real cellular-only phone (OCR resume dropped).

### Slice 180: faster device tests ([slice](slices/180-faster-device-tests.md))
- [phone] **Device:** Slice 180 (PR #536, #538): the System UI "isn't responding" dismissal in `run-flows.ps1` never triggered (read only); one first-run flow flake with ... [full text](notes/2026-10-30-outstanding-detail.md#o022) ([slice](slices/180-faster-device-tests.md))

### Slice 179: no robot or own machine counts ([slice](slices/179-no-robot-or-own-machine-counts.md))
- [phone] **Device:** Slice 179 (PR #542): Windows VM run with a CI MSIX; iOS `sandboxReceipt` on TestFlight; Windows Store-signature check on a Store build; Release ... [full text](notes/2026-10-30-outstanding-detail.md#o019) ([slice](slices/179-no-robot-or-own-machine-counts.md))

### Slice 167: no test device counts ([slice](slices/167-no-test-device-counts.md))
- [phone] **Device:** Slice 167 (PR #497): Release build on an emulator (crashed at startup in MAUI before any usage code, x64 only); emulator and Test Lab rules on a ... [full text](notes/2026-10-30-outstanding-detail.md#o029) ([slice](slices/167-no-test-device-counts.md))

### Slice 166: usage device context ([slice](slices/166-usage-device-context.md))
- [phone] **Device:** Slice 166 (PR #492): Windows heads only compiled (no desktop run of the RTST tray or Dino Windows head); iOS compiled only (no simulator); first ... [full text](notes/2026-10-30-outstanding-detail.md#o028) ([slice](slices/166-usage-device-context.md))

### Slice 161: warm headless x64 emulator ([slice](slices/161-warm-headless-x64-emulator.md))
- [phone] **Device:** Slice 161 (PR #485): arm64-vs-x64 launch-and-read timing not compared; version-mismatch guard, bump guard and concurrent-build paths reviewed only, ... [full text](notes/2026-10-30-outstanding-detail.md#o025) ([slice](slices/161-warm-headless-x64-emulator.md))

### Slice 148: phone wait followups ([slice](slices/148-phone-wait-followups.md))
- [phone] **Device:** Windows stall overlay on a packaged run (native read dropped).

### Slices 43-95 ([moved from notes/2026-10-25-outstanding-older.md](notes/2026-10-25-outstanding-older.md))
- [phone] **Device:** Slice 46: the Windows tray panel's progress ring in the running app (auto-read, copy, hotkey; clears on supersede/failure/completion); follow-up: a reader-level generating signal on `ISpeechReader` for Android ([slice](docs/plan/slices/46-play-loading-state.md)).
- [owner] **You:** iPhone from TestFlight: the iOS app's runtime (AVSpeech reading, file picker).
- [phone] **Device:** Slice 47: a voice switch (miss, then hit on switching back), head-start benefit and the 100 MB cap on a real phone; Slice 56 wires the cache on Windows. ([slice](docs/plan/slices/47-head-start-sentence-cache.md))
- [owner] **You:** iPhone TestFlight pass: neural voice after the pack, skip, interruptions, lock screen, txt/pdf/docx open.
- [phone] **Device:** Offline translation with a cached model, offline first use (alert, original read), a 5,000-word text, the translate card in RTL.
- [phone] **Device:** The TalkBack path of the gate; edit, page reorder and delete on the emulator.
- [phone] **Device:** Slice 62: the wander pill and Play halo during a read, the gate pass to Manage, the bedtime theme and RTL (waits on Slice 66), long-story pagination on a phone, the tablet layout and "Page n of m"; follow-up: share the read-start grace state machine with RTST. ([slice](docs/plan/slices/62-reading-screen-modes.md))
- [phone] **Device:** Slice 56: system voice then neural handover on a real first run (download too fast to see; no log line names the engine); offline first launch; kill and resume mid-download; MSIX size and no `VoiceModel\`; the `internetClient` Store certification dry run; first-audio comparison for prepared text; follow-ups: pack/engine log lines, deduplicate with `Mobile.Shared` ([slice](docs/plan/slices/56-windows-packs-language-headstart.md)).
- [phone] **Device:** Farsi UI mirroring on a device.
- [owner] **You:** Slice 74: after the deploy, `https://dinoreader.sirous.uk` and `/privacy` load with the security headers; `/download/android` serves the Dino APK once a Dino release publishes; paste the privacy URL into Play Console's Dino Reader field; Play Families may want an email contact instead of the GitHub issues link ([slice](docs/plan/slices/74-dino-reader-site.md)).
- [phone] **Device:** Slice 75: desktop acceptance of toggle, pack download and streamed translated read (session was locked); `packs-3` contents after the workflow run; Android/iOS still find their packs in `packs-3` ([slice](docs/plan/slices/75-windows-translation-engine.md)).
- [owner] **You:** Play Console "DEX obfuscation" metric after the next release (target >= 25%).
- [phone] **Device:** Slice 73: Android 14 (API 34) inset path; landscape on both apps; RTST `LanguagePage` on first run; Dino `PageTextPage` with the keyboard up; Play's deprecated-API warning after the next release; follow-up: Dino gallery/Manage tiles re-lay out oddly after toggling night mode ([slice](docs/plan/slices/73-edge-to-edge-safe-areas.md)).
- [phone] **Device:** Slice 77: page-2 cover pick on a multi-page story; a photo cover surviving an app restart; the Windows live theme switch (`UISettings.ColorValuesChanged`) ([slice](docs/plan/slices/77-dino-covers-and-light-theme.md)).
- [phone] **Device:** Slice 80: Windows Dino nested taps and window recreate; TalkBack reading "Edit <title>" and "Delete <title>"; the 10-min `AwayTracker.ExternalActivityCap` timer (no test; lives in `DinoApp`) ([slice](docs/plan/slices/80-grownup-gallery-overlay.md)).
- [phone] **Device:** Slice 81: header "tap to retry" label (state flashes Resolving then Offline, no code defect found); a reinstalled pack restoring the chosen voice; all of it on a real phone ([slice](docs/plan/slices/81-language-change-starts-voice-download.md)).
- [owner] **You:** Slice 82: iOS voice selection on a device (CI compile only); Windows does not switch the reading language (by design for now). User decided 2026-10-22: untranslated text is read in its own language (Slice 90). ([slice](docs/plan/slices/82-phone-voice-reads-the-texts-language.md))
- [owner] **You:** Typing Farsi on a real keyboard; Arabic content in the editor; the first-run Language page in Farsi.
- [owner] **You:** iPhone: a different-language read uses the phone voice in that language; the "no voice" message for a language with neither a natural pack nor a phone voice.
- [phone] **Device:** Slice 92: "Try again" after going offline and the gallery refresh on `StoreClient.LibraryChanged`, on a device; the production `/api/stories*` routes after deploy; the store is empty until curated stories are put in `dinoreader/stories/` in the bucket ([slice](docs/plan/slices/92-dino-story-store.md)).
- [phone] **Device:** The language rebuild after first run (DinoApp.RebuildRoot) on a device; Windows backup save and restore; iPhone.
