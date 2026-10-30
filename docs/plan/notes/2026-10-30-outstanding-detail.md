# Outstanding work, full text (moved from plan.md 2026-10-30)

Verbatim items; plan.md keeps a short line and a link to each.

<a id="o001"></a>
- Slice 206 (PR #594): `preflight.ps1` reading a PIN or pattern lock as NO-GO; `reset-dev-app.ps1 -Level reinstall`; Dino resets (`-App dino`); `tap.ps1` on Dino Reader; the lease used by two real sessions at once ([slice](docs/plan/slices/206-phone-run-scripts.md)).

<a id="o002"></a>
- Slice 194 (PR #591): the panel shot is not good enough for the site (guest offline, no voice pack, watermark); retake it with the voice pack copied into the offline VM (Slice 215), crop the watermark, convert to WebP at 362x525 and replace `site/img/control-panel.webp` ([slice](docs/plan/slices/194-control-panel-screenshot-script.md)).

<a id="o003"></a>
- Slice 193 (PR #589, drafts only): the user reads `docs/store-listings/de.md` and `dino/de.md` and approves, then the `store-listing` agent applies; Partner Center needs the user's Submit; the RTST Windows text needs the pack size ([slice](docs/plan/slices/193-german-store-listings.md)).

<a id="o004"></a>
- Slice 178 (PR #547): pdf, docx, photo and scan kinds on a device; iOS and Windows runs; `tools/dino-usage-report.ps1` against the live API after release; `analytics-query` notes for the new blob9 shape (Slice 166 file) after release ([slice](docs/plan/slices/178-dino-own-book-usage-detail.md)).

<a id="o005"></a>
- Slice 181 (PR #543): a recording carried over a swap; a store-downloaded book with edited text getting an update; a crash during the apply itself (kill mid-swap); iOS simulator and Windows VM runs; apply time on a real phone still owed (no hook stages a store update; 1.2-1.5 s on emulator Debug; [S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)) ([slice](docs/plan/slices/181-dino-books-update-from-store.md)).

<a id="o006"></a>
- Slice 182 (PR #544): OCR resume after the final fix round; "Waiting for Wi-Fi" on a real cellular-only phone; a real phone and the iOS runtime; Failed -> Retry done on the S21 2026-10-30 ([S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)) ([slice](docs/plan/slices/182-pack-download-resumes-by-itself.md)).

<a id="o007"></a>
- Slice 190 (PR #573): on-screen sentence highlight PASS on the S21 (2026-10-30); pause/resume mid-chunk; iOS TestFlight read of a 3-sentence paragraph; Farsi/RTL on device ([slice](docs/plan/slices/190-rtst-phone-sentence-highlight.md)).

<a id="o008"></a>
- Slice 196 (PR #575): the flow `default-view-per-book.yaml` not yet shown failing on old main; Good Enough pages 8 and 10 by eye; landscape layout; Windows head; iOS TestFlight open of Good Enough; a native speaker's read of the machine-drafted `Reading_Mode_Selected` string in 14 languages ([slice](docs/plan/slices/196-dino-default-view-per-book.md)).

<a id="o009"></a>
- Slice 187 (PR #576): the waiting look by eye (title, grey bar); Dino's translation wait on a device; other wait overlays after the `Center` -> `Fill` change; iOS TestFlight; native read of the new strings ([slice](docs/plan/slices/187-translation-download-waits-for-internet.md)).

<a id="o010"></a>
- Slice 188 (PR #585): reading speed row navigation on the Windows head by UIA (a Grid with a tap gesture; neither a label click nor a pane-centre click opened it on the VM); `dino-page-turn` fails in the VM (no StoryText element with a name), both now Slice 205 ([slice](docs/plan/slices/188-dino-settings-vm-check.md)).

<a id="o011"></a>
- Slice 191 (PR #581): emulator run inconclusive (no `own_book_added` line seen at add or restore; whether the own book came back not seen; another check wiped the app data on the same emulator); restore of a backup with 2 own books should log two `restore:` entries and none for starter/store books ([slice](docs/plan/slices/191-dino-count-restored-and-imported-books.md)).

<a id="o012"></a>
- Slice 192 (PR #582): live `meta.json` refreshed (all 15 books in `/api/stories` carry `creditsByLanguage`, checked 2026-10-30); `.maestro/dino/store-credits-language.yaml` (German store shows "frei zum Teilen"); native read of the credit lines; iOS TestFlight ([slice](docs/plan/slices/192-dino-store-credits-in-every-language.md)).

<a id="o013"></a>
- Slice 200 (live data checks): re-run the report after more real traffic (under 10 events per group everywhere on 2026-10-30); no rows yet for voice pack download events or the Windows `clipboard`/`hotkey` read sources; the stores' own live versions were not read. Own-machine Windows rows moved to Slice 219 ([slice](docs/plan/slices/200-live-usage-data-checks.md)).

<a id="o014"></a>
- RTST Microsoft Store English listing is stale (neutral English text from before the voice packs; found in Slice 193's live read, which covers German only): draft an update with the `store-listing` agent; applying it waits for the user's approval ([slice](docs/plan/slices/193-german-store-listings.md)).

<a id="o015"></a>
- PR #435 (Slice 154, Dino Farsi and Arabic vowel marks, open): Slice 192 bumped all 15 store books after it branched (Three Little Pigs is v13 on `main`), so its book versions must be re-stamped (`BookBuilder stamp`) when it is next merged with `main`.

<a id="o016"></a>
- Slice 197 (PR #583): no stale "Downloading" status after the download ends; notification Cancel mid-download then nothing restarts; iOS TestFlight on a cellular-only iPhone ([slice](docs/plan/slices/197-dino-download-now-tap-dropped.md)).

<a id="o017"></a>
- Slice 213 (PR #610): a cold-cache run where the AOT build fails or stalls (the FAIL path); a run while another session holds the lease; the Debug-to-AOT swap mid-run; iOS (no Mac) ([slice](docs/plan/slices/213-phone-check-runner.md)).

<a id="o018"></a>
- Slice 183 (PR #549): iOS background session on a device (TestFlight); Dino storyteller first-download ask deferral; the dataSync timeout's silent stop is planned as Slice 208 (a "Download paused. Tap to finish." notification). PIN-locked network and Android 15 `OnTimeout` done on the S21 ([S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)); notification text read on the S21 2026-10-30: "Downloading natural voices… 3%", no language or size (Slice 208) ([slice](docs/plan/slices/183-pack-downloads-keep-going-when-locked.md))

<a id="o019"></a>
- Slice 179 (PR #542): Windows VM run with a CI MSIX; iOS `sandboxReceipt` on TestFlight; Windows Store-signature check on a Store build; Release build on an emulator without the marker (gate reason); Worker drop live after deploy; iOS simulator harness off switch (`SIMCTL_CHILD_READTHESTUPIDTEXT_NO_USAGE=1`) when a harness is added ([slice](docs/plan/slices/179-no-robot-or-own-machine-counts.md)).

<a id="o020"></a>
- Slice 185 (PR #545): AOT cold start measured on a phone (first audio 624 ms, but taken with `audio-timing.ps1`, which labels warm-cache repeats as cold (Slice 216 item 2), so recheck it with the runner's `cold-start`; [S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)); the Release x64 first-run pack start is not run ([slice](docs/plan/slices/185-close-three-old-gaps.md)).

<a id="o021"></a>
- Slice 173 (PR #525): mobile-data "Wait for Wi-Fi / Download now" prompt not seen (emulator has Wi-Fi); a persisted no-pack language (`dino.story.nopack`) after restart (Farsi had a pack); translation-pack size is a fixed "30 MB" (about right for one ML Kit model; no change); iOS run (no Mac) and the real phone; native read of the new strings in 13 languages; store privacy forms for the new events (user's lane). Windows VM run 2026-10-28 passed (Story languages page behind the gate, offline download fails cleanly, no flag buttons with one language or an own book); Cancel during a download not tried (offline fails within 1 s). Failed download now unticks with "Couldn't get the voice. Tap to try again." and offline keeps waiting (PR #555); the untick regression it caused is fixed in PR #558; emulator 2026-10-29 passed all five checks; iOS not run ([slice](docs/plan/slices/173-dino-kid-story-languages.md)).

<a id="o022"></a>
- Slice 180 (PR #536, #538): the System UI "isn't responding" dismissal in `run-flows.ps1` never triggered (read only); one first-run flow flake with two emulators at once (watch for repeats) ([slice](docs/plan/slices/180-faster-device-tests.md)).

<a id="o023"></a>
- Slice 177 (PR #522): iOS timing (TestFlight); a first read in a non-default voice folder (e.g. Farsi multi-folder pack) still starts cold; a Farsi book warmed on open not proven on the emulator ([slice](docs/plan/slices/177-dino-cold-start-synthesis.md)).

<a id="o024"></a>
- Slice 160 (PR #486): TestFlight translate English to Farsi on iOS, model download progress and Cancel, offline translate after download, Dino typed own book into Farsi, VoiceOver switch role/state (iOS below 17 and 17+); CI iOS job links the MLKitTranslate xcframework; simulator builds have no translator by design ([slice](docs/plan/slices/160-ios-translation-parity.md)).

<a id="o025"></a>
- Slice 161 (PR #485): arm64-vs-x64 launch-and-read timing not compared; version-mismatch guard, bump guard and concurrent-build paths reviewed only, not run. Follow-up done: device-test now uses `tools/emulator`, ClaudeSkills #59 ([slice](docs/plan/slices/161-warm-headless-x64-emulator.md)). The snapshot warm start was dropped in Slice 162: the emulator cold-boots (~45 s) and is reused while running.

<a id="o026"></a>
- Slice 162 (PR #490): iOS hooks and the `rtst-test://` / `dino-test://` URL scheme have never run (no Mac), compile-checked only; Dino `skip-first-run` leaves out the voice pack start (Debug x86_64 SIGSEGV in Reflection.Emit via `SpokenLanguage.Use`), and the grown-up hand-over (the Dino x64 first-run crash check is Slice 185). (device-test now runs the repo harnesses first, ClaudeSkills #59, done) ([slice](docs/plan/slices/162-scripted-device-flows.md)).

<a id="o027"></a>
- Slice 164 (PR #491): pure moves, unit-tested, no device run needed. Follow-up done (PR #557: SpeedPreset, EmptyPlayer, Dino LanguagePick, ReadingLayoutRules, WizardSteps); ApplyScanAvailability, PagesStep and the tray wiring hold no decision worth moving ([slice](docs/plan/slices/164-more-logic-in-unit-tests.md)).

<a id="o028"></a>
- Slice 166 (PR #492): Windows heads only compiled (no desktop run of the RTST tray or Dino Windows head); iOS compiled only (no simulator); first live rows from a store build with the new fields (query via the API, `analytics-query`) ([slice](docs/plan/slices/166-usage-device-context.md)).

<a id="o029"></a>
- Slice 167 (PR #497): Release build on an emulator (crashed at startup in MAUI before any usage code, x64 only); emulator and Test Lab rules on a device (unit tests only); RTST tray on the desktop; iOS simulator run; first store rollout showing no test rows, via the API ([slice](docs/plan/slices/167-no-test-device-counts.md)).

<a id="o030"></a>
- Slice 168 (PR #495): German heard on iOS (TestFlight) and Windows (waits on packs-8); Android German read done by log (1795 ms, [S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)); Windows `de-en`/`en-de` translation; Dino German own book with distinct character voices; native German read of all German text (machine-written); Dino German store listing is Slice 193 ([slice](docs/plan/slices/168-german-in-both-apps.md))

<a id="o031"></a>
- Slice 169 (PR #503): German sound words and pages heard aloud on a phone; a German book on iOS (TestFlight); end-of-batch check that /api/stories matches main (CI publish-store). German translations show English curly quotes instead of „…“ (fixed in `TranslatedStory.cs`; re-check in the app). ([slice](docs/plan/slices/169-german-dino-books.md))

<a id="o032"></a>
- Slice 170 (PR #505): device check at 100% and 150% scale, idle and reading, English and Finnish (all five toggles and the wait flyout fully visible); retake `site/img/control-panel.webp` (Slice 194); the redesign rule at 200% scale with the Translate column hidden. Follow-up: "Controls row fits by design" (fits by layout, never measured on screen). ([slice](docs/plan/slices/170-tray-panel-controls-row-fits.md))

<a id="o033"></a>
- Slice 171 (PR #506): iOS line reader (`IosTextLineSource`, TextKit rebuilt to match UILabel) and the card's scroll on a real iPhone: Latin, Farsi (RTL), a CJK page; Windows head pages not checked. ([slice](docs/plan/slices/171-dino-text-card-shows-whole-page.md))

<a id="o034"></a>
- Slice 137: telemetry on since 2026-10-27 and rows arrive in `rtst_usage` (so far test devices only); iOS compile; emulator pass done 2026-10-26 (unselected speed-button labels low-contrast in dark mode, pre-existing) ([slice](docs/plan/slices/137-rtst-phone-usage-counts.md)).

<a id="o035"></a>
- Slice 138: emulator PASS 2026-10-26; sub-500 ms play start PASS on the S21 (61-140 ms); not seen: an ML Kit progress % (Slice 139 downloads the model during the idle; needs a metered network or recording). Native-speaker read of the 4 `Wait_` strings in 14 locales; iOS. Mid-read stall wait and `wait_shown`/`wait_cancelled` events deferred. ([slice](docs/plan/slices/138-rtst-phone-wait-indicator.md)).

<a id="o036"></a>
- Slice 143: not exercised on emulator: photos, scan, own-book translate; native read of the new strings (14 locales, Claude drafts); Windows head overlay; reduce-motion static dino ([slice](docs/plan/slices/143-shared-wait-overlay-dino-loading.md)).

<a id="o037"></a>
- Slice 144: listen to every moved Farsi character and the five new flavours (distinct enough?); Farsi read with `mana` narrating on emulator/phone; RTST Farsi default on Windows (phone PASS: `piper:fa-mana`, 558 ms, [S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)) ([slice](docs/plan/slices/144-farsi-default-female-voice.md)).

<a id="o038"></a>
- Slice 141: the panel ring (Play becomes the ring with Cancel) re-check on the desktop; native-speaker read of the 5 `Wait_` strings in 14 locales; one unconfirmed `CoreMessagingXP` `0xc000027b` crash on a hotkey read 0.7 s after an auto-read ([slice](docs/plan/slices/141-rtst-windows-wait-indicator.md)).

<a id="o039"></a>
- Slice 149: emulator Farsi read passed by logs only, not heard; iOS compile of `IosSupertonicSpeechReader.Synthesis.cs` and `SiblingRescue`; a listen to joined `mana` phrases (150/90 ms joins) ([slice](docs/plan/slices/149-never-drop-a-short-word.md)).

<a id="o040"></a>
- Slice 139: emulator 2026-10-26 (swapping AVD): early work finished gives first audio in 396-749 ms with a cache hit on sentence 1, but in-flight races (a Read tapped ~0.3 s after unticking Translate, or Translate-and-read before the early voice is stored) still read cold; real phone 2026-10-28 (Pixel 10 Pro): first audio 2.7 s, no gaps; the in-flight races PASS on the S21 2026-10-30 ([S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md); a tap before language detection reads untranslated, a bug planned as [Slice 211](docs/plan/slices/211-play-before-detection-waits-for-it.md)). Known limits: no early work before language detection + 0.4 s idle; unpunctuated text misses the sentence cache; iOS. Slice 198 closed with no change needed ([slice](docs/plan/slices/139-rtst-phone-early-start.md)).

<a id="o041"></a>
- Slice 148: emulator PASS 2026-10-26 (timings not representative). Not done: Windows stall overlay on a packaged run; native read of `Wait_Stalled` and `Review_TextCut`; re-run the 300k-character repeated-text read on a 2 GB AVD to confirm the 8-chunk `GenerationWindow`; iOS and Windows neural readers not checked ([slice](docs/plan/slices/148-phone-wait-followups.md)).

<a id="o042"></a>
- Slice 146: native read of all 13 translations per book; a listen to the new sound-word triples in ar, da, fi, fr, ja, nb, nl, pt, ru, sv; upload of every rewritten store book (`tools/upload-store-story.ps1`; needs the user's OK) then compare `/api/stories` ([slice](docs/plan/slices/146-dino-books-kid-friendly-language.md)).

<a id="o043"></a>
- Slice 152: Billy Goats store book on device; hearing a recast animal by ear; Windows head and iOS; native read of the `Settings_AnimalSounds_*` strings; upload and live check of the 11 bumped store books (needs the user's OK) ([slice](docs/plan/slices/152-dino-animal-from-the-book.md)).

<a id="o044"></a>
- Slice 156: device listen of each book's cast in en/fa/es/pt/zh; fa/es/pt roster limits (few voices per gender, narrator voice shared; see each art record); Chinese Treasure on device; Windows head and iOS; store upload (user's OK) ([slice](docs/plan/slices/156-dino-authored-character-voices.md)).

<a id="o045"></a>
- Slice 154 (Three Little Pigs Farsi part, PR #470; its review tooling data now lives under `content/dino/three-little-pigs/translations`, Slice 157): recordings surviving the starter refresh (none existed in the test); p5 تق تق تق, p14 شالاپ/آپچی and p15 چیک replacements await the user's pick; Farsi review of the other 14 books; Arabic diacritizer and its listen test ([slice](docs/plan/slices/154-dino-farsi-arabic-vowel-marks.md)).

<a id="o046"></a>
- Slice 157 (book content out of the tests, PR #475): first main run of `publish-store` with the new builder; `ShippedTitles` edits do not reach live `meta.json` until a version bump ([slice](docs/plan/slices/157-dino-book-content-out-of-tests.md)).

<a id="o047"></a>
- Slice 159: native-speaker review of `ShippedTitles` and the 7 new resx keys in 13 languages; store credit line still English (Slice 192); live `meta.json` gets `titles` only at each book's next version bump; iOS ML Kit binding not wired (iOS/Windows have no translation engine); airplane mode (no pack: book stays English, no download) untested; Debug `-t:Install` crash-loops on a missing sherpa-onnx native lib (use the embedded arm64 APK) ([slice](docs/plan/slices/159-dino-everything-in-my-language.md)).

<a id="o048"></a>
- Slice 98: emulator result for the picture + text flow was recorded in the slice; Windows head side-by-side layout in a wide window; Three Little Pigs pictures from the user (starter not rebuilt); a native read of the new `Pictures_*` strings, non-English ones are Claude drafts ([slice](docs/plan/slices/98-dino-picture-and-text-books.md)).

<a id="o049"></a>
- Slice 103: emulator check of each new UI language; Windows head in Dutch; a native read of all translations; Dino's iOS app has no localisation plist entries (pre-existing) ([slice](docs/plan/slices/103-nordic-dutch-languages.md)).

<a id="o050"></a>
- Done 2026-10-24: packs-5 (packs run 40081509906; fixes #295 Python 3.12, #296 `no_dict` guard) carries Supertonic v2 with da/fi/sv/nl, piper-nb and the ten Windows translation packs. The apps point at it (#300). Still to check on a real phone: Norsk downloads piper-nb, and Swedish/Danish read with Supertonic.

<a id="o051"></a>
- Slice 101: emulator device-test of the page; real ML Kit download and translation PASS on the S21 (fr/es/de, [S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)); iOS (compile, the checkbox never shows); a native read of the new strings in the nine locales ([slice](docs/plan/slices/101-rtst-mobile-review-and-translate.md)).

<a id="o052"></a>
- Slice 104: listen to pick the clearest default voice (KNN is a placeholder; Slice 195); Norsk download and read on the S21 PASS via Slice 201; emulator: a Dino story gets distinct voices; Windows head: the pack downloads and reads. Follow-ups merged 2026-10-24: #295 (`packs.yml` pins Python 3.12), #296 (the nb espeak guard checks `no_dict`) ([slice](docs/plan/slices/104-piper-norwegian-pack.md)).

<a id="o053"></a>
- Slice 105: Swedish and Danish reading PASS on the S21; the no-voice phone-voice fallback PASS but weak (every app language has a voice, so no true no-voice language exists; zh only stood in while its pack downloaded) ([S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)); the upgrade from 1.33.0 with a release-signed build; a native read of the 70 translation tables. Story-v2 is uploaded (10 live at version 2). Follow-up merged 2026-10-24: #289 (`Pictures_*` strings in the 5 new Dino languages) ([slice](docs/plan/slices/105-dino-nordic-dutch-books-and-rules.md)).

<a id="o054"></a>
- Slice 115: the emulator checks listed under Verification; native read of the `Storyteller_*` strings (12 are machine drafts); a "Download now" tap right after the mobile-data check can still be dropped (Slice 197); real-phone Kokoro speed done (S21 cold 960 ms, [S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)) ([slice](docs/plan/slices/115-dino-storyteller-voices-by-default.md)).

<a id="o055"></a>
- Slice 113: pitch by ear (drop it if robotic); first Sounds-row tap after a flavour change fixed (PR #556: waits for the save, one sample at a time, wait overlay with Cancel after 500 ms); not yet heard on a device or run after the review fixes; system-voice fallback plays Normal; iOS reader build; native read of the strings ([slice](docs/plan/slices/113-dino-character-flavours.md)).

<a id="o056"></a>
- Slice 116: native read of roster names and hints, especially Farsi; Farsi bee=Small and princess=Whisper flavours by ear; Chinese has only one voice (confirm acceptable) ([slice](docs/plan/slices/116-dino-storyteller-characters.md)).

<a id="o057"></a>
- Slice 118: the listening check of the es, pt and zh WAVs; real-phone Kokoro speed is done ([S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)); a Chinese sentence with Latin words on a device; the Wi-Fi plus cellular pack-update path on a device ([slice](docs/plan/slices/118-dino-storyteller-more-languages.md)).

<a id="o058"></a>
- Slice 119: on-device starter auto-update and recording copy (unchanged pages keep recordings, edited text is skipped); the pigs pictures in both orientations and the store download of pigs v3 ([slice](docs/plan/slices/119-dino-illustrated-three-little-pigs.md)).

<a id="o059"></a>
- Slice 122: on the user's phone, pigs upright and sideways (panel at most 30%); photo/PDF import defaults the switch off; a wide picture sideways with the words off ([slice](docs/plan/slices/122-dino-text-panel-30-and-switch.md)).

<a id="o060"></a>
- Slice 124: emulator 2026-10-25 PASS for the switch card and sub-line, the fade while scrolled and the panel inside the picture (bottom alignment of a short page not seen: every pigs page is long); native read of the 3 new strings in the 13 non-English languages ([slice](docs/plan/slices/124-dino-words-panel-matches-mock.md)).

<a id="o061"></a>
- Slice 125: emulator 2026-10-25 PASS for the Animal line, "Not an animal" persisting and the Farsi card (its bugs were fixed in Slice 135); store copies live (billy-goats v4, peter-rabbit v4); "Oink!" cadence by ear; native read of the new strings and roster names in 13 languages; Windows head ([slice](docs/plan/slices/125-dino-character-animals.md)).

<a id="o062"></a>
- Slice 135 (and the Farsi follow-up, PR #355): emulator 2026-10-25 PASS on an upgraded install: three distinct piglets and the Wolf voice in English and Farsi, Animal sounds on, upright panel inside the picture; store copies live (pigs v7, gingerbread v3, tortoise v3). Still open: listening by ear; native read of the new roster names ([slice](docs/plan/slices/135-dino-animal-voices-fixes.md)).

<a id="o063"></a>
- Slice 126: emulator PASS (screen stays on while playing, sleeps after pause and at the end); screen awake on a real phone PASS while playing (end of book PASS on the S21 2026-10-30: screen hold released 0 s after the end, [S21 night runs](docs/plan/notes/2026-10-29-s21-night-runs.md)); iPhone Auto-Lock 30 s; Windows head not built or run ([slice](docs/plan/slices/126-dino-keep-screen-awake.md)).

<a id="o064"></a>
- Slice 127: Dino's first Microsoft Store submission went in by hand on 2026-10-29 (1.78.1.0, x64 and ARM64, 15 listing languages, machine drafts except English; name "Dino Book Reader" reserved). The repository variable `DINOREADER_STORE_APP_ID` is set (2026-10-30, Slice 203) ([slice](docs/plan/slices/127-release-safety-fixes.md)).

<a id="o065"></a>
- Slice 129: emulator Dino story with flavoured voices (Small, Whisper, animal sounds) sounds the same, by ear; Windows head flavoured character reads; RTST phone read unchanged ([slice](docs/plan/slices/129-dino-voice-features-out-of-shared-engine.md)).

<a id="o066"></a>
- Slice 133: telemetry on and rows arrive in `dino_usage` since 2026-10-27; settings off sends nothing still to check on a device; no rate limit or origin check on `/api/events` (accepted); native read of the new strings ([slice](docs/plan/slices/133-dino-anonymous-usage-counts.md)).

<a id="o067"></a>
- Slice 132: seed R2 `config.json` after merge; emulator: Version card and "A new version is ready" row PASS, and the resume fetch (follow-up #366) PASS with no NetworkOnMainThread in logcat 2026-10-26; forced-update `UpdatePage`, the gate and Play page fallback not run on a device (needs a temporary `minVersion` change in the live config); Windows head and iOS builds; Play in-app update needs a Play-installed build with a higher versionCode; native read of the 5 new strings ([slice](docs/plan/slices/132-dino-remote-config-and-force-update.md)).

<a id="o068"></a>
- Slice 134: telemetry on since 2026-10-27, `tools/dino-usage-report.ps1` runs against live data; emulator smoke of each event; `own_book_added` not wired for backup restore or `.dino` import (Slice 191) ([slice](docs/plan/slices/134-dino-usage-events-and-report.md)).

<a id="o069"></a>
- Slice 136: emulator: cast page PASS in English and Farsi (matches frame 59, no Animal line); sounds (oink, Wolf growls) and recasting Pip to a non-animal going silent need a listen, not verifiable by adb ([slice](docs/plan/slices/136-dino-animal-from-character.md)).

<a id="o070"></a>
- Slice 106: Norwegian on a Windows head via `MarianEngine` (third-party conversion, never run); en to da/fi/sv/nl/nb and back on Windows; the Norwegian packs are about 390 MB each ([slice](docs/plan/slices/106-windows-nordic-dutch-translation-packs.md)).

<a id="o071"></a>
- Slice 184: a real English-to-Japanese translation in the Windows app (download the pack, translate in the control panel); the C# tokenizer path has not run against ElanMT ([slice](docs/plan/slices/184-windows-english-to-japanese-pack.md)).

<a id="o072"></a>
- Slice 150: a mid-book speed change heard on a real phone; Windows head and iOS readers apply `SetSpeed` on the next sentence; native-speaker read of the `Speed_*` strings (13 drafts) ([slice](docs/plan/slices/150-dino-slower-reading-speed.md)).

<a id="o073"></a>
- Slice 155: offline store step; microphone prompt on the Record step; Windows variant (title, Picture badge); iOS modal presentation; large-font layout; native read of the 15 Intro strings (13 drafts); Play Data safety and App Store privacy answers cover the 3 new events ([slice](docs/plan/slices/155-dino-first-run-intro-wizard.md)).

<a id="o074"></a>
- Slice 172 (PR #519): narrator sample Cancel overlay (sample returned under 500 ms on the emulator); cast narrator language on a device; iOS run (no Mac); native read of the 13 draft translations. The Windows Settings flow passed in the VM 2026-10-28 (touch injection and Narrator). the permanent VM check is planned as Slice 188 (the script lives only in `D:\ReadTheStupidText-worktrees\_desktop-vmdhoc-s172\checks\`) ([slice](docs/plan/slices/172-dino-settings-gear.md)).

<a id="o075"></a>
- Slice 174 (PR #521): shelf tile title after Show original text on the emulator (fix made after the run); iOS run (rotation, safe areas, swipe-back); real phone; native read of 3 new strings x 14 languages (13 drafts); the reported auto-scroll bug was never reproduced. The `dino-page-turn` VM check now passes portrait and both landscapes (PR #527) ([slice](docs/plan/slices/174-dino-reading-screen-and-back-button.md)).

<a id="o076"></a>
- Slice 189 (PR #560): own-book sheet ("Your book", Edit pages); a switch flipped just before Remove or Edit pages; Settings sub-page -> Settings back step; the 30% card on Windows (only the inner scroll area measured, 24.7%); iOS run (no Mac); real phone; native read of the new strings (13 drafts). Slice 188's VM check must use the new path (shelf -> grown-ups button -> gate -> gear) ([slice](docs/plan/slices/189-dino-grown-ups-button-and-book-sheet.md)).

<a id="o077"></a>
- Slice 175 (PR #517): iOS hooks on a device (no Mac); no unit tests for `LaunchKeys`, `BookMatch` or the timing parser (Slice 199). The VM items (activation, ISO handling, Windows Update off; PR #523) are done and the selection check passes 6/6 ([slice](docs/plan/slices/175-test-gaps-permanent-checks.md)).
