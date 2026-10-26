# Plan: ReadTheStupidText — Windows 11 read-aloud tray app

Index of the plan. Every slice has its own file under `docs/plan/slices/`; the numbered Decisions live in [`docs/plan/decisions.md`](docs/plan/decisions.md); investigations and batch write-ups live in `docs/plan/notes/`. Slices 0-42 were moved here verbatim from the old long-form `plan.md` by `archive-docs` (2026-10-20).

## Context

The user wants a lightweight Windows 11 utility ("ReadTheStupidText") that reads text
aloud at a user-chosen speed (1x / 1.25x / 1.5x / 1.75x / 2x), with a tiny
control beside the clock for pause/resume and speed, that auto-reads selected
text where possible and falls back to a global hotkey for apps that don't
expose their selection (terminals, CLI, Claude Code). It must run at startup
and ship through the Microsoft Store. The shape was settled in `scope.md`;
this plan turns it into ordered, shippable vertical slices.

## Status

Open slices only. Finished slices (143 rows) are in [slices-done](docs/plan/notes/slices-done.md); each slice's full record is its own file under `docs/plan/slices/`.

| # | Slice | Status | PR | Story | File |
| --- | --- | --- | --- | --- | --- |
| 146 | Rewrite every Dino book in spoken, kid-friendly language | in progress | | | [146-dino-books-kid-friendly-language.md](docs/plan/slices/146-dino-books-kid-friendly-language.md) |
| 150 | Dino: slower reading by default (0.8x), grown-up speed 0.5-1.2x | planned | | | [150-dino-slower-reading-speed.md](docs/plan/slices/150-dino-slower-reading-speed.md) |
| 151 | Dino: three view modes, no merging of characters | planned | | | [151-dino-three-view-modes.md](docs/plan/slices/151-dino-three-view-modes.md) |
| 152 | Dino: a character's animal comes from the book, never its voice | planned | | | [152-dino-animal-from-the-book.md](docs/plan/slices/152-dino-animal-from-the-book.md) |
| 153 | Dino: no robotic-sounding character voices | planned | | | [153-dino-no-robotic-voices.md](docs/plan/slices/153-dino-no-robotic-voices.md) |
| 154 | Dino: vowel marks in Farsi and Arabic books | planned | | | [154-dino-farsi-arabic-vowel-marks.md](docs/plan/slices/154-dino-farsi-arabic-vowel-marks.md) |
| 155 | Dino: a first-run intro wizard for the four things Dino does | planned | | | [155-dino-first-run-intro-wizard.md](docs/plan/slices/155-dino-first-run-intro-wizard.md) |

Batch 25 (119-123; 119, 120, 122, 123 built, 121 built; planned 2026-10-25, the stories have no pictures): Claude illustrates each book by driving the Gemini web app in the user's Chrome. 119 first (the pigs, the pilot; the user checks it), then 120 (the skill), then 121 one book per PR. Touches pictures and the store/starter builders only, so it can run beside Batch 24. Decisions 125-129.

Batch 26 (124-125, planned 2026-10-25, the Batch 25 mock and the animal-identity follow-up of Slice 117): 124 first (small; matches the words panel and switch card to the mock), then 125 (both edit `CastRows`/`CastPage` and the strings). 125 also merges after PR #336 (the pigs retell, `StarterBuilder`/`StarterTexts`). Decision 132. Slice 136 (the cast page drops the Animal line; Decision 132 changed) is built.

Batch 27 (127-131, planned 2026-10-25, after Dino work kept resubmitting Read The Stupid Text to the Store): keep one repo, stop one product shipping the other. 127 (small CI fixes), 128, 129 and 130 (a version and release per product, PR #367) are built; 131 (PR #371) is built; it ran alone between waves because it touches usings everywhere. Decisions 133-137.

Batch 28 (132-134, planned 2026-10-25, which books families like and how parents use the app): 132 (the remote config the counts are switched by, and the forced update) and 133 (first events, privacy texts and store forms) and 134 (the rest of the events and the report, PR #368) are built, so the batch is done. All three edit `worker/` and Dino's `MauiProgram`, so one at a time. Decisions 138-142.

Batch 30 (143-146 and 148, planned 2026-10-26, Dino loading, Farsi voice and kid-friendly language): 143 builds the shared wait overlay (RTST 138 reuses it), 144 is small and independent, 145 measures sound-word spellings before 146 rewrites every book (one PR per book: Farsi, then English, then the rest). 143, 144, 145 and 148 (the stall and wait-count follow-ups, PR #416) are built; 146 is in progress: Farsi wave A (8 books) is merged, 6 more Farsi books wait for the other tab's PRs, and English and the other languages are to come. Decisions 149-153.

Batch 31 (150-154, planned 2026-10-26, from a tester review of Dino Reader): 150 (speed) and 151 (view modes, no merge) go first and can run together (150 edits settings, 151 the reading page and cast page). 152 follows 151 (both edit `CastPage`/`CastRows`/`StoryVoicing`). 153 starts with a listening page and waits on the user's flags. 154 starts with a listening test, then adds vowel marks book by book after each book's Slice 146 rewrite and open illustration PR merge. Decisions 155-159 (157 reverses 132).

Batch 32 (155, planned 2026-10-26, Dino onboarding): one slice, mock first, then step 1 end to end, then steps 2-4, resume, the replay row, strings and privacy texts. It edits the first-run, gallery and usage-event code, which Batch 31 doesn't, so it can run alongside 150-154. Decision 160.

### Notes

- [Finished slices](docs/plan/notes/slices-done.md): the status rows of every built or verified slice, and the finished batches' lane-order paragraphs.
- [Out of scope, earlier batches](docs/plan/notes/out-of-scope-history.md): every earlier batch's out-of-scope lines, moved verbatim.
- [Decisions 1-99](docs/plan/decisions.md): the full numbered decision list, moved verbatim.
- [2026-10-25 Dino usage insights](docs/plan/notes/2026-10-25-dino-usage-insights.md): what Play, Apple, COPPA and the UK Children's code allow for analytics in a kids' app, and the Cloudflare cost (Batch 28).
- [2026-10-25 Product separation](docs/plan/notes/2026-10-25-product-separation.md): how much RTST and Dino share, and which changes ship the other product (Batch 27).
- [2026-10-20 Android minSdk 28](docs/plan/notes/2026-10-20-android-min-sdk-28.md): why the Android floor is 28 (Decision 82).
- [Batch introductions, Batches 2-8](docs/plan/notes/batch-intros-1-8.md): the framing paragraph that opened each batch (2026-10-20 move).
- [Batch 9 and 10 indexes](docs/plan/notes/plan-batch-9-10-index.md): the former in-plan tables and lane order.
- [General verification lines](docs/plan/notes/verification-general.md): the verification lines that belong to no single slice.
- [Older outstanding work](docs/plan/notes/2026-10-25-outstanding-older.md): Slice 43-95 unverified items moved out of plan.md (2026-10-25).
- [CLAUDE.md history](docs/plan/notes/claude-md-history.md): the Batch 6-10 narrative blockquotes moved out of `CLAUDE.md` (2026-10-20).

## Outstanding work

**Store privacy declarations (legal; gates all usage counts, Decision 145).** No `telemetry.enabled` is set to `true` for an app until its rows are ticked. Claude drafts the answers and fills the forms in the user's signed-in Chrome; the user reviews and presses Submit/Publish.

- [x] RTST repo texts: `PRIVACY.md`, `site/privacy.html` (deployed), `STORE.md`, iOS `PrivacyInfo.xcprivacy` (Slice 137; Windows sources in Slice 140; checked 2026-10-26: phone and Windows covered). Claude.
- [ ] RTST Google Play Console: Data safety (app activity and app info/performance, not linked to the user, not shared, optional to turn off). User submits.
- [ ] RTST Apple App Store Connect: App Privacy (usage data and diagnostics, not linked, no tracking). User publishes.
- [ ] RTST Microsoft Partner Center: privacy policy URL and the data-collection answers. User submits.
- [ ] Dino Google Play Data safety and Apple App Privacy for its usage counts (Slices 133-134, still pending). User submits.
- [ ] Then: enable Analytics Engine, add the bindings, deploy, set `telemetry.enabled` per app.

Built but not verified (details in each slice file):

- Slice 137: rows in `rtst_usage` once Analytics Engine is on and the binding is added; iOS compile; emulator pass done 2026-10-26 (unselected speed-button labels low-contrast in dark mode, pre-existing) ([slice](docs/plan/slices/137-rtst-phone-usage-counts.md)).
- Slice 138: emulator PASS 2026-10-26; not seen: a sub-500 ms play start, an ML Kit progress %. Native-speaker read of the 4 `Wait_` strings in 14 locales; iOS. Mid-read stall wait and `wait_shown`/`wait_cancelled` events deferred. Pre-existing: a 36 MB text file crashes ReviewPage with OutOfMemoryError ([slice](docs/plan/slices/138-rtst-phone-wait-indicator.md)).
- Slice 140: queue file under `LocalFolder/usage` not exercised (needs `telemetry.enabled` on); Microsoft Store data-collection answers; rows in `rtst_usage` once Analytics Engine is on ([slice](docs/plan/slices/140-rtst-windows-usage-counts.md)).
- Slice 143: not exercised on emulator: photos, scan, own-book translate; native read of the new strings (14 locales, Claude drafts); Windows head overlay; reduce-motion static dino ([slice](docs/plan/slices/143-shared-wait-overlay-dino-loading.md)).
- Slice 144: listen to every moved Farsi character and the five new flavours (distinct enough?); Farsi read with `mana` narrating on emulator/phone; RTST Farsi default on phone and Windows ([slice](docs/plan/slices/144-farsi-default-female-voice.md)).
- Slice 141: the panel ring (Play becomes the ring with Cancel) re-check on the desktop; native-speaker read of the 5 `Wait_` strings in 14 locales; one unconfirmed `CoreMessagingXP` `0xc000027b` crash on a hotkey read 0.7 s after an auto-read ([slice](docs/plan/slices/141-rtst-windows-wait-indicator.md)).
- Slice 145: a human listen of the `sound-words-listen` set (Farsi and Chinese especially; Farsi whoosh and cat picks provisional); German, French and Portuguese untested ([slice](docs/plan/slices/145-sound-words-aloud-test.md)).
- Slice 149: emulator Farsi read passed by logs only, not heard; iOS compile of `IosSupertonicSpeechReader.Synthesis.cs` and `SiblingRescue`; a listen to joined `mana` phrases (150/90 ms joins) ([slice](docs/plan/slices/149-never-drop-a-short-word.md)).
- Slice 139: emulator 2026-10-26 (swapping AVD): early work finished gives first audio in 396-749 ms with a cache hit on sentence 1, but in-flight races (a Read tapped ~0.3 s after unticking Translate, or Translate-and-read before the early voice is stored) still read cold; re-test on a real phone and follow up in the read path (`OnReadTapped`/`EarlyStartCoordinator` settle after a restart). Known limits: no early work before language detection + 0.4 s idle; unpunctuated text misses the sentence cache; iOS ([slice](docs/plan/slices/139-rtst-phone-early-start.md)).
- Slice 142: packaged desktop run done 2026-10-26 on a CPU-saturated host; re-check first-audio timing on a quiet machine; prompt tray exit not verified ([slice](docs/plan/slices/142-rtst-windows-early-start.md)).
- Slice 148: emulator PASS 2026-10-26 (timings not representative). Not done: Windows stall overlay on a packaged run; native read of `Wait_Stalled` and `Review_TextCut`; re-run the 300k-character repeated-text read on a 2 GB AVD to confirm the 8-chunk `GenerationWindow`; iOS and Windows neural readers not checked ([slice](docs/plan/slices/148-phone-wait-followups.md)).
- Slice 121: check the illustrated books on a phone (pictures, text panel, voices) ([slice](docs/plan/slices/121-dino-illustrate-store-stories.md)).
- Slice 146 (in progress): upload the six rewritten store books (`tools/upload-store-story.ps1`; user OK needed) ([slice](docs/plan/slices/146-dino-books-kid-friendly-language.md)).
- Slice 96: emulator in Farsi and Arabic (gallery, gate, wizard, store, backup, reader, swipe direction, highlight on RTL lines); Windows head gallery and reader ([slice](docs/plan/slices/96-dino-layout-never-mirrors.md)).
- Slice 97: Windows head: the AddBook icon rendering and the gate opening; native-speaker read of the `Gallery_AddBook` strings ([slice](docs/plan/slices/97-dino-add-book-button.md)).
- Slice 99: the RTST phone highlight covers a whole 3-sentence `SpeechTextChunker` chunk, not one sentence; full TalkBack walk-through; iOS device run; Settings voice name does not refresh after a pack installs ([slice](docs/plan/slices/99-rtst-mobile-minimal-main-page.md)).
- Slice 102: emulator: ten cards with credits, download, read with voices, Farsi read; a native-speaker read of the translations ([slice](docs/plan/slices/102-dino-store-ten-stories.md)).
- Slice 98: emulator result for the picture + text flow was recorded in the slice; Windows head side-by-side layout in a wide window; Three Little Pigs pictures from the user (starter not rebuilt); a native read of the new `Pictures_*` strings, non-English ones are Claude drafts ([slice](docs/plan/slices/98-dino-picture-and-text-books.md)).
- Slice 103: emulator check of each new UI language; Windows head in Dutch; a native read of all translations; Dino's iOS app has no localisation plist entries (pre-existing) ([slice](docs/plan/slices/103-nordic-dutch-languages.md)).
- Done 2026-10-24: packs-5 (packs run 40081509906; fixes #295 Python 3.12, #296 `no_dict` guard) carries Supertonic v2 with da/fi/sv/nl, piper-nb and the ten Windows translation packs. The apps point at it (#300). Still to check on a real phone: Norsk downloads piper-nb, and Swedish/Danish read with Supertonic.
- Slice 100: a real ML Kit scan on a signed-in phone; iOS capture (compile and behaviour); a native read of the new strings ([slice](docs/plan/slices/100-rtst-mobile-page-capture-wizard.md)).
- Slice 101: emulator device-test of the page; a real ML Kit model download and translation on a phone; iOS (compile, the checkbox never shows); a native read of the new strings in the nine locales ([slice](docs/plan/slices/101-rtst-mobile-review-and-translate.md)).
- Slice 104: listen to pick the clearest default voice (KNN is a placeholder); emulator: Norsk first run downloads the pack and a Dino story gets distinct voices; Windows head: the pack downloads and reads. Follow-ups merged 2026-10-24: #295 (`packs.yml` pins Python 3.12), #296 (the nb espeak guard checks `no_dict`) ([slice](docs/plan/slices/104-piper-norwegian-pack.md)).
- Slice 105: Swedish and Danish reading after the packs-5 release on a real phone, and that the phone-voice fallback doesn't hang silently with no voice for the language; the upgrade from 1.33.0 with a release-signed build; a native read of the 70 translation tables. Story-v2 is uploaded (10 live at version 2). Follow-up merged 2026-10-24: #289 (`Pictures_*` strings in the 5 new Dino languages) ([slice](docs/plan/slices/105-dino-nordic-dutch-books-and-rules.md)).
- Slice 107: Windows head greyed switch and note; native read of the 3 new strings ([slice](docs/plan/slices/107-dino-own-books-translate-by-default.md)).
- Slice 108: native check of `Cast_VoicesFor` ([slice](docs/plan/slices/108-dino-cast-per-reading-language.md)).
- Slice 109: older build opens new package, old backup re-cast; native check of 14 locales ([slice](docs/plan/slices/109-dino-one-narrator-per-language.md)).
- Slice 110: scanned page picture plus translation; native read of `Book_*` in 14 locales ([slice](docs/plan/slices/110-dino-translated-text-on-screen.md)).
- Slice 111: **human:** Play Console Data safety "audio recordings, on-device only" answer; record/replay on a real phone (Android 9-11), iOS, Windows head; mic permission denial; backup round trip; native read of `Rec_*` ([slice](docs/plan/slices/111-dino-parent-recordings.md)).
- Slice 112: native read of new strings ([slice](docs/plan/slices/112-dino-store-age-author-filters.md)).
- Slice 115: the emulator checks listed under Verification; native read of the `Storyteller_*` strings (12 are machine drafts); a "Download now" tap right after the mobile-data check can still be dropped; real-phone Kokoro first-sentence latency (emulator 74 s vs Supertonic 8 s; Slice 114 kill criterion) ([slice](docs/plan/slices/115-dino-storyteller-voices-by-default.md)).
- Dino first-run "Ready" screen says "Three stories" but four ship; a fix is in progress. (fix PR open)
- Slice 113: pitch by ear (drop it if robotic); bug: the first Sounds-row play tap right after changing a flavour is silent (second tap works); system-voice fallback plays Normal; iOS reader build; native read of the strings ([slice](docs/plan/slices/113-dino-character-flavours.md)).
- Slice 114: listening to Kokoro English with flavours on a real phone; Windows head storyteller; native check of the strings ([slice](docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md)).
- Slice 116: native read of roster names and hints, especially Farsi; Farsi bee=Small and princess=Whisper flavours by ear; Chinese has only one voice (confirm acceptable) ([slice](docs/plan/slices/116-dino-storyteller-characters.md)).
- Slice 117: native read of all animal words and sounds (Persian pig and cow are guesses); `BleatWobble` depth by ear; iOS reader build. ([slice](docs/plan/slices/117-dino-animal-sounds.md)).
- Slice 118: the listening check of the es, pt and zh WAVs; real-phone Kokoro speed (about 9x slower than Supertonic on the emulator); a Chinese sentence with Latin words on a device; the Wi-Fi plus cellular pack-update path on a device ([slice](docs/plan/slices/118-dino-storyteller-more-languages.md)).
- Slice 119: on-device starter auto-update and recording copy (unchanged pages keep recordings, edited text is skipped); the pigs pictures in both orientations and the store download of pigs v3 ([slice](docs/plan/slices/119-dino-illustrated-three-little-pigs.md)).
- Slice 122: on the user's phone, pigs upright and sideways (panel at most 30%); photo/PDF import defaults the switch off; a wide picture sideways with the words off ([slice](docs/plan/slices/122-dino-text-panel-30-and-switch.md)).
- Slice 123: on the phone, the pigs show 16 pages with pictures; an installed v3 copy updates to v4 and keeps recordings on unchanged pages ([slice](docs/plan/slices/123-dino-pigs-one-action-per-page.md)).
- Slice 124: emulator 2026-10-25 PASS for the switch card and sub-line, the fade while scrolled and the panel inside the picture (bottom alignment of a short page not seen: every pigs page is long); native read of the 3 new strings in the 13 non-English languages ([slice](docs/plan/slices/124-dino-words-panel-matches-mock.md)).
- Slice 125: emulator 2026-10-25 PASS for the Animal line, "Not an animal" persisting and the Farsi card (its bugs were fixed in Slice 135); store copies live (billy-goats v4, peter-rabbit v4); "Oink!" cadence by ear; native read of the new strings and roster names in 13 languages; Windows head ([slice](docs/plan/slices/125-dino-character-animals.md)).
- Slice 135 (and the Farsi follow-up, PR #355): emulator 2026-10-25 PASS on an upgraded install: three distinct piglets and the Wolf voice in English and Farsi, Animal sounds on, upright panel inside the picture; store copies live (pigs v7, gingerbread v3, tortoise v3). Still open: listening by ear; native read of the new roster names ([slice](docs/plan/slices/135-dino-animal-voices-fixes.md)).
- Slice 126: emulator PASS (screen stays on while playing, sleeps after pause and at the end); real Android phone whole book; iPhone Auto-Lock 30 s; Windows head not built or run ([slice](docs/plan/slices/126-dino-keep-screen-awake.md)).
- Slice 127: Dino Microsoft Store submit with the `DinoReader.App` package (store-submit-dino was skipped in run 40253931851; RTST store-submit, both TestFlight jobs and actionlint passed) ([slice](docs/plan/slices/127-release-safety-fixes.md)).
- Slice 129: emulator Dino story with flavoured voices (Small, Whisper, animal sounds) sounds the same, by ear; Windows head flavoured character reads; RTST phone read unchanged ([slice](docs/plan/slices/129-dino-voice-features-out-of-shared-engine.md)).
- Slice 133: human enables Analytics Engine, then emulator read shows rows in `dino_usage` and settings off sends nothing (usage switch present on the emulator 2026-10-26); Play Data safety and App Store privacy answers once the sending release ships; no rate limit or origin check on `/api/events` (accepted); native read of the new strings ([slice](docs/plan/slices/133-dino-anonymous-usage-counts.md)).
- Slice 132: seed R2 `config.json` after merge; emulator: Version card and "A new version is ready" row PASS, and the resume fetch (follow-up #366) PASS with no NetworkOnMainThread in logcat 2026-10-26; forced-update `UpdatePage`, the gate and Play page fallback not run on a device (needs a temporary `minVersion` change in the live config); Windows head and iOS builds; Play in-app update needs a Play-installed build with a higher versionCode; native read of the 5 new strings ([slice](docs/plan/slices/132-dino-remote-config-and-force-update.md)).
- Slice 131: macOS CI iOS build after merge; emulator launch of both apps PASS 2026-10-26 ([slice](docs/plan/slices/131-mobile-shared-namespace.md)).
- Slice 134: everything live waits on switching telemetry on (human: enable Analytics Engine, add the binding, set `telemetry.enabled`), then `tools/dino-usage-report.ps1`; emulator smoke of each event; Play Data safety and App Store privacy answers for the new events; `own_book_added` not wired for backup restore or `.dino` import ([slice](docs/plan/slices/134-dino-usage-events-and-report.md)).
- Slice 136: emulator: cast page PASS in English and Farsi (matches frame 59, no Animal line); sounds (oink, Wolf growls) and recasting Pip to a non-animal going silent need a listen, not verifiable by adb ([slice](docs/plan/slices/136-dino-animal-from-character.md)).
- Slice 106: Norwegian on a Windows head via `MarianEngine` (third-party conversion, never run); en to da/fi/sv/nl/nb and back on Windows; the Norwegian packs are about 390 MB each ([slice](docs/plan/slices/106-windows-nordic-dutch-translation-packs.md)).
- Windows has no `en-ja` translation pack (`TranslationPacks.FromEnglish` and `packs.yml` lack it), so English cannot be translated into Japanese offline on Windows; found while planning Slice 106, not yet scheduled.
- Older unverified items (Slices 43-95 and a flaky-test note), moved verbatim: [outstanding-older](docs/plan/notes/2026-10-25-outstanding-older.md). Still open until verified.

## Out of Scope

- **(Batch 31)** Animals and sounds for parent-provided books (photos, PDFs, typed: unchanged); robotic-voice checks beyond English unless English shows flavours are the cause; reading speeds above 1.2x in Dino; vowel marks for languages other than Farsi and Arabic.
- **(Batch 28)** Any install, device or advertising id; per-family profiles or parental-consent flows; third-party analytics SDKs; analytics in RTST; remote config for RTST.
- **(Batch 27)** Splitting into two repos or publishing the shared code as NuGet packages; renaming the shared projects or their namespaces beyond Mobile.Shared; a commit flag that skips one product's release.

Earlier batches' out-of-scope lines (still binding unless a later Decision reverses them): [out-of-scope-history](docs/plan/notes/out-of-scope-history.md).
