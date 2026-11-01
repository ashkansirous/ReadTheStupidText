# Not yet tried on a device

Every slice's open Not yet verified checks, newest slice first. A line leaves when the check passes on a device or the user confirms it; plan.md's Outstanding work keeps only the count.

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

### Slice 220: Kokoro silent short lines on arm64, part 1 merged ([slice](slices/220-kokoro-silent-short-lines-arm64.md))

- iOS: compile only.
- The 68-read Slice 210 set is still not run.
- The Windows Dino head has no single-thread arm.

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
