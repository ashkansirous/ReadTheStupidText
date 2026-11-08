# Unverified checks triage, 2026-11-08

The user asked why `docs/plan/unverified.md` held hundreds of checks and approved a triage ("yes. do it"). Four Sonnet agents classified all 469 lines there plus the 54 older lines in [outstanding-older](2026-10-25-outstanding-older.md) against origin/main (slice files and code); an analytics-query agent checked the log-provable ones against 30 days of production rows (test rows left out); the main thread reviewed every removal class and corrected a few (Slice 254 kept, Slice 69 removed, Slice 60 moved to the gaps).

Removed: 106 lines. Still open: 416 (Live 130, iOS 70, Android 152, Windows 52, Logs 12), now tagged by what each needs.

The analytics pass over the Android/iOS/Windows/Live lines found almost nothing provable: most are layout, look, test-tooling or listening checks that no event records. One contradiction: English and Farsi `clip_missing` rows on Android up to v1.114.0, all before the Slice 313 fix (dino-v1.116.0); the Slice 285 line stays open until rows on 1.116+ are clean.

## The user's iPhone session, 2026-11-08

The user used Dino 1.117.0 from TestFlight on an iPhone (iOS 16, session `26b3cf3b…`, 08:10-08:22 UTC), "clicking on as many routes as possible": 5 books, 12 reads (Farsi, then Spanish), a recording, the store with a download, Settings (reading speed, story languages with Spanish added and all 9 Spanish packs fetched, 0 failed), animal sounds, About, the intro replay, the grown-ups' gate and Report a problem (code T7RS-G327, read with `pull-crashes.mjs --code`). No fallback, crash or unclean exit on 1.117.0. The 1.109.0 session just before it (08:06-08:09) had 5 `clip_missing` and 2 `text_mismatch` fallbacks on book `6eff8d77…` in Farsi; on 1.117.0 the same book played "119 of 119 pieces have a clip", consistent with the Slice 313 fix.

Closed by it:
- **Slice 296:** iOS: a Farsi read on an iPhone reports its clip count. *Why:* report log `read voice premade (108 pieces by clip, 0 with no clip)` and 119/107/19 on other books; `read_voice` rows with counts in `dino_usage_beta`.
- **Slice 249:** Sending a report from a TestFlight build and `pull-crashes.mjs --code <code>` on that real report. *Why:* T7RS-G327 sent from 1.117.0 `testflight` and read back whole.
- **Slice 249**, narrowed: "The iOS log lines (page, reading, memory warning)": the page and reading lines are in the report; only the memory-warning line stays open.

Not closed: Freddy (`af402c99…`, Slice 313's iPhone line) was not read in this session; Slice 285's line needs the *app* language switched, and the session switched the story language.

## Replaced or removed by later code (51)

- **Slice 304:** Stalled-read overlay is not restored after leaving and returning (review 6): `ISpeechReader` exposes no stalled state; follow-up is [Slice 314](slices/314-reader-stall-overlay-after-return.md).
  - *Why:* Slice 314 carries stall overlay restore
- **Slice 264:** A real scroll during a press cancelling the hold (the 4-book test shelf does not scroll).
  - *Why:* Slice 298 lists real scroll cancelling hold
- **Slice 264:** iOS gestures (`PointerGestureRecognizer` touch on iOS).
  - *Why:* Slice 298 lists iOS gestures
- **Slice 264:** Windows keyboard reorder (Space/arrows).
  - *Why:* Slice 298 lists Windows keyboard reorder
- **Slice 258:** iOS run (compile only so far).
  - *Why:* Slice 261 removed the starter books from the app (StarterStories/Resources), premade audio for them moot
- **Slice 258:** iOS run (compile only so far).
  - *Why:* Slice 261 removed the starter books from the app (StarterStories/Resources), premade audio for them moot
- **Slice 258:** Windows head: the starter books' pre-made audio playing (launch, open book and page turn passed on the VM, 2026-11-05 16:28 pre-night run).
  - *Why:* Slice 261 removed the starter books from the app; Windows starter audio check moot
- **Slice 249:** Known gap, to fix in a follow-up: with the remote crash switch off, a report the page called "kept and sent later" can be wiped at the next app start (`CrashReporter.UserReport.cs:135`).
  - *Why:* Slice 300 (user report kept only while crash switch on; otherwise not saved and page shows ReportProblem_FailedNotKept; merged)
- **Slice 255:** Old premade dumps (Pigs, Wolf) predate the sound-entry skip and need regenerating before Slice 246 C uses them.
  - *Why:* Slice 258 line 43 rebuilt stale Pigs/Tortoise fa manifests; Slice 272 item 2 rebuilt broken fa packs
- **Slice 247:** Item a's change: the waits are not shortened yet, so "same 5 flows finish faster" is unproven.
  - *Why:* Slice 301 item 3: 3 of the 5 flows deleted by Slice 278 part B; before/after premise gone
- **Slice 242:** `dino-landscape-reading` does not assert the `[Landscape] reading controls=row` line; the row branch was read from logcat by hand.
  - *Why:* .maestro/dino/dino-landscape-reading-tablet.yaml:5 expect-log controls=row; Slice 242 device run saw it on RTST_Tablet
- **Slice 225:** Item 3 (run-checks emulator path) not built.
  - *Why:* Slice 308 built run-checks.ps1 -Serial emulator-NNNN (emulator path)
- **Slice 229:** Windows head not built.
  - *Why:* Dino Windows head is built by release.yml dinowin target (dinoreader-windows.yml) on every release; Slice 235 head also builds
- **Slice 191:** Slice 191 (PR #581): emulator run inconclusive (no `own_book_added` line seen at add or restore; whether the own book came back not seen; another ... [full text](notes/2026-10-30-outstanding-detail.md#o011) ([slice](slices/191-dino-count-restored-and-imported-books.md))
  - *Why:* Slice 278 Part A removed own books (A1-A3); own_book_added no longer fires
- **Slice 189:** Slice 189 (PR #560): own-book sheet ("Your book", Edit pages); a switch flipped just before Remove or Edit pages; Settings sub-page -> Settings ... [full text](notes/2026-10-30-outstanding-detail.md#o076) ([slice](slices/189-dino-grown-ups-button-and-book-sheet.md))
  - *Why:* Slice 278 Part A: own books deleted, no Edit pages row (278:162)
- **Slice 178:** Slice 178 (PR #547): pdf, docx, photo and scan kinds on a device; iOS and Windows runs; `tools/dino-usage-report.ps1` against the live API after ... [full text](notes/2026-10-30-outstanding-detail.md#o004) ([slice](slices/178-dino-own-book-usage-detail.md))
  - *Why:* Slice 278 Part A removed own books (pdf, docx, photo, scan kinds)
- **Slice 177:** Slice 177 (PR #522): iOS timing (TestFlight); a first read in a non-default voice folder (e.g. Farsi multi-folder pack) still starts cold; a Farsi ... [full text](notes/2026-10-30-outstanding-detail.md#o023) ([slice](slices/177-dino-cold-start-synthesis.md))
  - *Why:* Slice 278 Part B removed Dino speech engine (278:186,194); no synthesis cold start in Dino
- **Slice 174:** Slice 174 (PR #521): shelf tile title after Show original text on the emulator (fix made after the run); iOS run (rotation, safe areas, ... [full text](notes/2026-10-30-outstanding-detail.md#o075) ([slice](slices/174-dino-reading-screen-and-back-button.md))
  - *Why:* Slice 268 removed Show original text (PR #825)
- **Slice 172:** Slice 172 (PR #519): narrator sample Cancel overlay (sample returned under 500 ms on the emulator); cast narrator language on a device; iOS run (no ... [full text](notes/2026-10-30-outstanding-detail.md#o074) ([slice](slices/172-dino-settings-gear.md))
  - *Why:* Slice 278 Part B removed narrator and cast pages (278:186)
- **Slice 156:** Slice 156: device listen of each book's cast in en/fa/es/pt/zh; fa/es/pt roster limits (few voices per gender, narrator voice shared; see each art ... [full text](notes/2026-10-30-outstanding-detail.md#o044) ([slice](slices/156-dino-authored-character-voices.md))
  - *Why:* Slice 278 Part B removed voice engine and cast; clips are pre-made (278:186)
- **Slice 152:** Slice 152: Billy Goats store book on device; hearing a recast animal by ear; Windows head and iOS; native read of the `Settings_AnimalSounds_*` ... [full text](notes/2026-10-30-outstanding-detail.md#o043) ([slice](slices/152-dino-animal-from-the-book.md))
  - *Why:* Slice 255 real animal recordings and Slice 278 cast removal replace recast animals
- **Slice 145:** Slice 145: a human listen of the `sound-words-listen` set (Farsi and Chinese especially; Farsi whoosh and cat picks provisional); German, French and Portuguese untested ([slice](slices/145-sound-words-aloud-test.md)).
  - *Why:* Slice 255 replaced voiced sound words with real animal recordings
- **Slice 144:** Slice 144: listen to every moved Farsi character and the five new flavours (distinct enough?); Farsi read with `mana` narrating on emulator/phone; ... [full text](notes/2026-10-30-outstanding-detail.md#o037) ([slice](slices/144-farsi-default-female-voice.md))
  - *Why:* Slice 278 Part B removed Dino voice engine and cast (278:186)
- **Slice 143:** Slice 143: not exercised on emulator: photos, scan, own-book translate; native read of the new strings (14 locales, Claude drafts); Windows head ... [full text](notes/2026-10-30-outstanding-detail.md#o036) ([slice](slices/143-shared-wait-overlay-dino-loading.md))
  - *Why:* Slice 278 Part A removed photos, scan, own-book translate
- **Slice 136:** Slice 136: emulator: cast page PASS in English and Farsi (matches frame 59, no Animal line); sounds (oink, Wolf growls) and recasting Pip to a ... [full text](notes/2026-10-30-outstanding-detail.md#o069) ([slice](slices/136-dino-animal-from-character.md))
  - *Why:* Slice 278 Part B removed cast page and animals (278:186); Slice 255 real sounds
- **Slice 135:** Slice 135 (and the Farsi follow-up, PR #355): emulator 2026-10-25 PASS on an upgraded install: three distinct piglets and the Wolf voice in English ... [full text](notes/2026-10-30-outstanding-detail.md#o062) ([slice](slices/135-dino-animal-voices-fixes.md))
  - *Why:* Slice 255 real animal recordings and Slice 278 replace animal voices
- **Slice 129:** Slice 129: emulator Dino story with flavoured voices (Small, Whisper, animal sounds) sounds the same, by ear; Windows head flavoured character ... [full text](notes/2026-10-30-outstanding-detail.md#o065) ([slice](slices/129-dino-voice-features-out-of-shared-engine.md))
  - *Why:* Slice 278 Part B removed flavoured voices and the Dino voice engine
- **Slice 125:** Slice 125: emulator 2026-10-25 PASS for the Animal line, "Not an animal" persisting and the Farsi card (its bugs were fixed in Slice 135); store ... [full text](notes/2026-10-30-outstanding-detail.md#o061) ([slice](slices/125-dino-character-animals.md))
  - *Why:* Slice 255 and Slice 278 removed character animals and cast
- **Slice 119:** Slice 119: on-device starter auto-update and recording copy (unchanged pages keep recordings, edited text is skipped); the pigs pictures in both ... [full text](notes/2026-10-30-outstanding-detail.md#o058) ([slice](slices/119-dino-illustrated-three-little-pigs.md))
  - *Why:* Slice 261 removed starter books from the app; Slice 278 removed own books
- **Slice 118:** Slice 118: the listening check of the es, pt and zh WAVs; real-phone Kokoro speed is done ([S21 night ... [full text](notes/2026-10-30-outstanding-detail.md#o057) ([slice](slices/118-dino-storyteller-more-languages.md))
  - *Why:* Slice 278 Part B removed Kokoro storyteller packs (278:109-110)
- **Slice 117:** Slice 117: native read of all animal words and sounds (Persian pig and cow are guesses); `BleatWobble` depth by ear; iOS reader build. ([slice](slices/117-dino-animal-sounds.md)).
  - *Why:* Slice 255 real animal recordings replace sound words and BleatWobble
- **Slice 116:** Slice 116: native read of roster names and hints, especially Farsi; Farsi bee=Small and princess=Whisper flavours by ear; Chinese has only one ... [full text](notes/2026-10-30-outstanding-detail.md#o056) ([slice](slices/116-dino-storyteller-characters.md))
  - *Why:* Slice 278 Part B removed storyteller roster (278:186)
- **Slice 115:** Slice 115: the emulator checks listed under Verification; native read of the `Storyteller_*` strings (12 are machine drafts); a "Download now" tap ... [full text](notes/2026-10-30-outstanding-detail.md#o054) ([slice](slices/115-dino-storyteller-voices-by-default.md))
  - *Why:* Slice 278 Part B removed storyteller voices and downloads (278:186)
- **Slice 114:** Slice 114: listening to Kokoro English with flavours on a real phone; Windows head storyteller; native check of the strings ([slice](slices/114-dino-kokoro-english-storyteller-pack.md)).
  - *Why:* Slice 278 Part B removed Dino Kokoro English pack
- **Slice 113:** Slice 113: pitch by ear (drop it if robotic); first Sounds-row tap after a flavour change fixed (PR #556: waits for the save, one sample at a time, ... [full text](notes/2026-10-30-outstanding-detail.md#o055) ([slice](slices/113-dino-character-flavours.md))
  - *Why:* Slice 278 Part B removed flavours with the cast page
- **Slice 110:** Slice 110: scanned page picture plus translation; native read of `Book_*` in 14 locales ([slice](slices/110-dino-translated-text-on-screen.md)).
  - *Why:* Slice 278 Part A removed scan, own books and translation (A1-A2)
- **Slice 109:** Slice 109: older build opens new package, old backup re-cast; native check of 14 locales ([slice](slices/109-dino-one-narrator-per-language.md)).
  - *Why:* Slice 278 Part B removed narrator per language
- **Slice 108:** Slice 108: native check of `Cast_VoicesFor` ([slice](slices/108-dino-cast-per-reading-language.md)).
  - *Why:* Slice 278 Part B removed Cast_VoicesFor strings (278:194)
- **Slice 107:** Slice 107: Windows head greyed switch and note; native read of the 3 new strings ([slice](slices/107-dino-own-books-translate-by-default.md)).
  - *Why:* Slice 278 Part A removed own books translate switch
- **Slice 105:** Slice 105: Swedish and Danish reading PASS on the S21; the no-voice phone-voice fallback PASS but weak (every app language has a voice, so no true ... [full text](notes/2026-10-30-outstanding-detail.md#o053) ([slice](slices/105-dino-nordic-dutch-books-and-rules.md))
  - *Why:* Slice 278 Part B removed Dino voices; fallback no longer exists
- **Slice 97:** Slice 97: Windows head: the AddBook icon rendering and the gate opening; native-speaker read of the `Gallery_AddBook` strings ([slice](slices/97-dino-add-book-button.md)).
  - *Why:* Slice 278 Part A removed the Add pill and own books (A1)
- **Slice 59:** Slice 59: attribution quality on a real children's book text (only unit-test fixtures so far); check with the first Slice 60 read ([slice](docs/plan/slices/59-dialogue-parser-voice-caster.md)).
  - *Why:* Slice 278 removed Dino cast/voice engine/own books; store books use pre-made audio (Slices 246/277) and speaker-first (254); no Slice 60 read exists any more
- **Slice 69:** Slice 69: real-R2 Range behaviour of `/download/android/latest` not separately tested ([slice](docs/plan/slices/69-apk-downloads-on-r2.md)).
  - *Why:* worker/downloads.ts:1-3: direct APK downloads removed 2026-10-27; /download redirects to #platforms (curl 2026-11-08)
- **Slice 65:** Slice 65: preview audio, the Add-screen "Check the cast" path, opening the cast page from Edit, the narrator-change notice, reading in edited voices after a restart, the shared-voice note, RTL (Slice 66). ([slice](docs/plan/slices/65-cast-screen.md))
  - *Why:* Slice 278 (Gone: CastPage/Rows/Views/Text/FlavourMenu, NarratorPage, VoicePreview; cast pill removed from the book sheet)
- **Slice 64:** Slice 64: a real camera scan of a picture book on a device; an Arabic or Farsi scan highlighting the right lines; the denied-permission alert on Photos; whether the 1.1 zoom snap threshold feels right on a phone ([slice](docs/plan/slices/64-picture-mode-real-pages.md)).
  - *Why:* Slice 278 removed Dino scanning and picture-book capture (ScanPages/RegionPicker/PageTextPage/StoryWizardPage, picture-mode import)
- **Slice 94:** Slice 94: the upgrade path for an install that already had the three starters gaining Good Enough ([slice](docs/plan/slices/94-good-enough-starter.md)).
  - *Why:* Slice 261 removed starter books (no starters, first-run book picker); Slice 278 deletes own books on update
- **Slice 95:** Slice 95: distinct Farsi voices per character; the upgrade path from a real older install (adopt logic unit-tested only); Windows and iOS reading bundled translations; a native-speaker read of the translations; Spanish/Portuguese/Russian translations make the Hare or Fox feminine while the voice may be male ([slice](docs/plan/slices/95-dino-translation-works.md)).
  - *Why:* Slice 278 removed own-book translation and Dino voices/cast (bundled translations, per-character Farsi voices); Slice 261 removed starters
- **Slice 67:** Slice 67: TestFlight build installs and reads in four voices with picture-mode boxes; Windows desktop flows (typed story, voices, PDF picture mode, Windows OCR); real Partner Center identity and `DINOREADER_STORE_APP_ID`, then a Store submission ([slice](docs/plan/slices/67-dino-reader-ios-windows.md)).
  - *Why:* four voices, picture-mode boxes and Windows OCR removed by Slice 278; Dino Microsoft Store live per Slice 203 (dino-microsoft-store-live)
- **Slice 78:** Slice 78: scan 2 pages + 1 photo with the ML Kit scanner (needs a Google-signed device); Windows rotate/crop and drag-reorder; iOS on a device; only the changed page re-read; whether the Photos picker should multi-select; no discard confirmation on system back ([slice](docs/plan/slices/78-dino-page-review-wizard.md))
  - *Why:* Slice 278 removed the Dino page review wizard (PagesStep, PageEditorPage, StoryWizardPage, scan import)
- **Slice 79:** Slice 79: a real picture-book scan on a device; Windows overlay taps ([slice](docs/plan/slices/79-background-text-filtering.md)).
  - *Why:* Slice 278 removed Dino scanning, picture-mode overlays (RegionOverlay, RegionPicker, ScanPages)
- **Slice 91:** Slice 91: re-importing a `.dino` on a device (unit round-trip tests cover it) ([slice](docs/plan/slices/91-dino-story-packages-and-starters.md)).
  - *Why:* Slice 278 removed Dino own-book import (StoryImport) and books family added; store books are the only source

## Already passed on record (6)

- **Slice 251:** The Workers Builds deploy after the merge carries the `DINO_USAGE_BETA` binding.
  - *Why:* Slice 259 item 7: wrangler versions view lists env.DINO_USAGE_BETA (dino_usage_beta) Analytics Engine Dataset, deployed 2026-11-04T00:40:58Z
- **Slice 240:** A cache hit on the next run after main saved its Dino Windows caches (22:32-22:35 on 2026-11-01, release run 41356358486); PR #708's pull-request run missed the cache before that.
  - *Why:* Slice 247 item d: release run 41406476389 on main restored the Dino Windows cache in 8 s/6 s, install steps skipped
- **Slice 58:** Slice 58: Play Console app creation and the first manual internal-track upload for `uk.sirous.dinoreader` ([slice](docs/plan/slices/58-dino-reader-skeleton.md)).
  - *Why:* Dino is on Play: Slice 291 line 11 reads the live Play Dino listing ("3 to 8, nine languages"); Slice 267 reports the user on the latest Android build
- **Slice 58:** Slice 58: the Dino upload key and Play service-account secrets (`DINOREADER_*`) generated and stored ([slice](docs/plan/slices/58-dino-reader-skeleton.md)).
  - *Why:* .github/workflows/release.yml:456 (HAS_DINO_SIGNING from DINOREADER_ANDROID_SIGNING_*) and :897 (DINOREADER_PLAY_SERVICE_ACCOUNT_JSON) use the stored secrets
- **Slice 58:** Slice 58: a signed Dino release cut by CI ([slice](docs/plan/slices/58-dino-reader-skeleton.md)).
  - *Why:* dino-v releases cut by CI (Slice 130:56 dino-v1.54.0; Slice 227 dino-v1.85.0; Slice 261 measured dino-v1.100.0 builds)
- **Slice 76:** ~~Slice 76 follow-up: geometry takeover~~ done: Dino in Slice 79, RTST scan in Slice 86.
  - *Why:* the line itself says done: Dino in Slice 79, RTST scan in Slice 86 (struck-through)

## Proven by production analytics (7)

- **Slice 297:** `read_voice` counted at the first premade clip on iOS.
  - *Why:* dino_usage read_voice ios, 30d, arm64, no 1.0: 3 rows (1.112.0, 1.114.0; 2 premade, 1 partial ja). 1 ios fallback premade_audio:text_mismatch ja (the partial one).
- **Slice 273:** After release, `analytics-query` shows `fallback` rows from production builds and none from tests or CI.
  - *Why:* dino_usage fallback 30d, filtered (no 1.0, no x64/x86): 21 rows (android 18, ios 3), first 2026-11-06. rtst_usage fallback: 0 rows. "none from tests" is inference (filters only).
- **Slice 273:** Part B production rows: check with `analytics-query` after release.
  - *Why:* dino_usage fallback production rows 21 in 30d from 2026-11-06 via the sending path; read_voice rows carry counts.
- **Slice 273:** Part C production rows: check with `analytics-query` after release.
  - *Why:* dino_usage fallback store:store_unreachable 4 rows (android 2, ios 2), config:config_cached 1 row android, 2026-11-06 to 11-07.
- **Slice 250:** iOS store build's first rows showing session ids and the new events.
  - *Why:* dino_usage ios blob16<>'' 30d: 153 rows, 9 sessions, first 2026-11-04; read_started/read_stopped/book_left ios rows on v1.112-1.114.
- **Slice 250:** Live Worker rows after the deploy.
  - *Why:* dino_usage 30d: read_started android 194 + ios 7, read_stopped same, book_left 44+2, from 2026-11-04 19:28Z.
- **Slice 251:** A TestFlight build of Dino sends rows that land in `dino_usage_beta` and none in `dino_usage` (needs a real TestFlight install; no device run was made).
  - *Why:* dino_usage_beta ios 30d: read_started 60, read_stopped 56, audio_pack_download 40, app_opened 9, last 2026-11-06 19:29Z; crash store has testflight ios 1.106.x. "none in dino_usage" is by Worker design, not separately proven.

## Not a check (informational note) (27)

- **Slice 282:** Request shapes for AudioPlayer events and built-in intents are partly written from memory (only the Play directive and PlaybackController were checked against Amazon's docs).
  - *Why:* request shapes from memory; verified only by a real Echo
- **Slice 296:** Runs 2 and 3 used throwaway flows not in the repo; only run 1's build label (`0ac339e41410`) is proven.
  - *Why:* run provenance note
- **Slice 305:** A parallel build of both apps in one msbuild invocation.
  - *Why:* CI build combination note
- **Slice 288:** The release version and date of this slice, for Slice 287's dashboard note (version line not filled until it ships).
  - *Why:* filled in once the slice ships
- **Slice 294:** Item 3, the `first-run` flake: no failing log was kept, so no cause and no fix.
  - *Why:* no failing log kept; no cause
- **Slice 275:** Item 4: the cause of the earlier `-NoBuild` cache-key miss is unknown; `-NoBuild` hit the cache for both apps on 2026-11-06 only.
  - *Why:* cause unknown; Slice 294 follows up
- **Slice 274:** The iOS cause is only likely (stack matches dotnet/maui PR #38662; the one symbolicated app frame is `_xamarin_release_managed_ref+0x64`). CI keeps no dSYM, so the stack cannot be symbolicated further.
  - *Why:* cause only likely; no dSYM kept; Slice 295 bumps MAUI
- **Slice 270:** `IDispatcher.Dispatch` always queues on Windows (seen only as a string match in Microsoft.Maui.dll 10.0.120, not confirmed from source).
  - *Why:* unconfirmed platform fact about IDispatcher on Windows
- **Slice 270:** Review check2 #4 (stale id's early return in `ApplyRecordingEnded` may leave the recording Playing with nothing playing) and #6 (iOS audio-session Deactivate only in the queued step on a stale play): both unconfirmed.
  - *Why:* unconfirmed review notes check2 #4 and #6
- **Slice 270:** Review check2 #1 kept because "origin/main had the same window" (Windows Pause then Resume in the hop replays the clip): premise not checked.
  - *Why:* review premise unchecked
- **Slice 269:** `Test-LogMarker` regex: hand-checked and self-test only, not seen on a real flow with a `,` or `:` marker.
  - *Why:* Test-LogMarker regex hand-checked, self-test only
- **Slice 269:** The weekly pack sync skipping cancelled languages through the static lock in `ShelfAudioPacks`: unit-tested only.
  - *Why:* unit-tested only static lock in ShelfAudioPacks
- **Slice 235:** Focus return on a virtualized container is a no-op.
  - *Why:* known no-op on virtualized container
- **Slice 235:** Hover lift and shadow are not built (no native property).
  - *Why:* hover lift not built (no native property)
- **Slice 267:** The contact sheet of flow screenshots (`reviews/<slice>-shots/`) was not built.
  - *Why:* contact sheet not built
- **Slice 261:** The one-off Mono SIGSEGV on first-books-cancel (not reproduced in two later runs with the crash buffer captured).
  - *Why:* one-off Mono SIGSEGV not reproduced
- **Slice 259:** Item 1: why no overlay at 540 ms (about 40 ms over the 500 ms rule).
  - *Why:* 40 ms over threshold explanatory
- **Slice 250:** The `usage-playback-events` flow shown failing on origin/main (needs a rebuild).
  - *Why:* old-build failing demo, no value on current main
- **Slice 249:** RTST logs no Back line on Android (Shell has no pop hook); not tried further.
  - *Why:* RTST Shell has no pop hook; not pursued
- **Slice 255:** A fully cached run with no `Playing` state would not advance (same as origin/main).
  - *Why:* same as origin/main
- **Slice 225:** Item 6: failed-build cause unknown (the log is now kept as `.failed.log`).
  - *Why:* cause unknown, log kept as .failed.log
- **Slice 230:** The info icon (not drawn).
  - *Why:* info icon not drawn
- **Slice 230:** The failing-once check against the old APK (no old APK available), so the flow is not shown to fail before the change.
  - *Why:* no old APK available
- **Slice 231:** origin/main showing the picture only (the cause was read from code, not shown on a device).
  - *Why:* cause read from code only
- **Slice 226:** A Switch-end row still toggling (no current caller) ([slice](slices/226-dino-settings-rows-review-leftovers.md)).
  - *Why:* no current caller
- **Slice 227:** Slice 227: reports come only from rtst-v1.80.0 / dino-v1.85.0 onward; RTST's Microsoft Store submission rtst-store-v1.81.0 (2026-11-01) carries the code once certified; which version each store serves today could not be read from the public pages.
  - *Why:* version availability note
- **Slice 88:** Slice 88: paging back on the desktop (it has no back control); the Farsi/Arabic desktop check itself passed ([slice](docs/plan/slices/88-desktop-no-layout-mirroring.md)).
  - *Why:* the desktop has no back control, so paging back cannot be driven; the rest of Slice 88 passed (its own text)

## Not a check: a real gap, moved to Slice 316 (15)

- **Slice 296:** Mid-book read start (`ClipOffsetAsync`) is not modelled or run; only reads from the start are covered.
  - *Why:* ClipOffsetAsync mid-book start not modelled
- **Slice 307:** Item 10 (open, not built): `phone-network-guard.mjs` still wrongly denies a read-only grep with a quoted `\|adb` pattern.
  - *Why:* item 10 open, not built (phone-network-guard.mjs)
- **Slice 304:** No scripted flow yet for Review-pick-then-Read.
  - *Why:* no scripted flow for Review-pick-then-Read
- **Slice 294:** `tools/phone/run-checks.ps1` `Stop-CacheMissed` miss printout does not print the inputs (outside this slice's Touches).
  - *Why:* Stop-CacheMissed printout, outside Touches
- **Slice 267:** The `read_voice` and failure-path log lines carry no messages or URLs: only the success lines were read.
  - *Why:* failure-path log lines carry no messages (only success lines read)
- **Slice 259:** Follow-up, not done: document the new `exit_code_for` exit rule in `tools/premade-audio/README.md`.
  - *Why:* document exit_code_for in tools/premade-audio/README.md
- **Slice 235:** A window clamped to a small screen and saved as maximised reopens maximised (seen on the VM; whether that is by design is not decided).
  - *Why:* design decision not made on maximised reopen
- **Slice 242:** Item 6: the phone flow does not assert the tablet title stays hidden.
  - *Why:* phone flow lacks tablet-title assert
- **Slice 233:** The silent re-post on a repeated failed automatic resume: no scripted check asserts the alert/silent difference.
  - *Why:* no scripted check for alert vs silent
- **Slice 233:** The medium-pass skip reasons (slice line 46) state code facts not read in the code at triage, e.g. "Android's Notify reports no failure": check each against the code.
  - *Why:* verify skip reasons against code (slice line 46)
- **Slice 228:** Check-pass skip reasons 2, 3 and 7 (slice line 103) state code facts not read at triage (no modal over the reader; Dismiss refresh before `_mode.Leave`; Idle means no download): check each in the code.
  - *Why:* check skip reasons 2,3,7 in code (slice line 103)
- **Slice 228:** The skip reason "the app reaches users days later through the stores" (Worker before app) was not checked against the release workflow: Dino merges reach internal tracks the same day, so `intro_step` events may be dropped for minutes after a merge.
  - *Why:* Worker-before-app skip reason; intro_step events dropped after merge
- **Slice 58:** Slice 58: decide whether Dino Reader project additions to `ReadTheStupidText.slnx` / the workflows should keep re-releasing the RTST apps (today `*.slnx` and `release.yml` are core paths in `ship-targets.mjs`); seen when Slice 59's merge shipped an unchanged RTST release ([slice](docs/plan/slices/58-dino-reader-skeleton.md)).
  - *Why:* decision still open: .github/scripts/ship-targets.mjs:106 still lists *.slnx in CORE_PATHS; Slice 130 split releases per product but did not change it
- **Slice 60:** Slice 60: each character's voice and the gaps between voice switches heard on a real device; the pre-existing Android timer bug (`elapsed > total`, not reset on idle) needs its own fix. ([slice](docs/plan/slices/60-per-character-voice-reading.md))
  - *Why:* Dino voice part removed by Slice 278; the RTST Android timer bug (ReaderPage.xaml.cs ElapsedLabel) is still open
- **Slice flaky-test:** Flaky test `ReadAloudServiceGeneratingTests.Auto_read_prepares_the_head_start_before_the_settle_delay_ends` (timing; failed once in CI on 2026-10-21).
  - *Why:* tests/ReadTheStupidText.Tests/ReadAloudServiceGeneratingTests.cs:445 test still exists, timing-based; no later doc mentions a fix
