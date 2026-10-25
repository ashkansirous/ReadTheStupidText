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

Open slices only. Finished slices (125 rows) are in [slices-done](docs/plan/notes/slices-done.md); each slice's full record is its own file under `docs/plan/slices/`.

| # | Slice | Status | PR | Story | File |
| --- | --- | --- | --- | --- | --- |
| 121 | Dino: pictures for the ten store stories | planned | | | [121-dino-illustrate-store-stories.md](docs/plan/slices/121-dino-illustrate-store-stories.md) |
| 126 | Dino: keep the screen awake while a book is playing | planned | | | [126-dino-keep-screen-awake.md](docs/plan/slices/126-dino-keep-screen-awake.md) |
| 127 | Release safety: Dino iOS .ipa gap and the Store id fallback | planned | | | [127-release-safety-fixes.md](docs/plan/slices/127-release-safety-fixes.md) |
| 128 | Tray code out of the shared Infrastructure, and tests per product | planned | | | [128-tray-code-out-of-shared-infrastructure.md](docs/plan/slices/128-tray-code-out-of-shared-infrastructure.md) |
| 129 | Dino voice features out of the shared speech engine | planned | | | [129-dino-voice-features-out-of-shared-engine.md](docs/plan/slices/129-dino-voice-features-out-of-shared-engine.md) |
| 130 | A version and a release per product | planned | | | [130-per-product-versions-and-releases.md](docs/plan/slices/130-per-product-versions-and-releases.md) |
| 131 | Mobile.Shared gets its own namespace | planned | | | [131-mobile-shared-namespace.md](docs/plan/slices/131-mobile-shared-namespace.md) |
| 132 | Dino: a remote switchboard and a forced update | planned | | | [132-dino-remote-config-and-force-update.md](docs/plan/slices/132-dino-remote-config-and-force-update.md) |
| 133 | Dino: anonymous usage counts, first event end to end | planned | | | [133-dino-anonymous-usage-counts.md](docs/plan/slices/133-dino-anonymous-usage-counts.md) |
| 134 | Dino: the rest of the usage events, and a report | planned | | | [134-dino-usage-events-and-report.md](docs/plan/slices/134-dino-usage-events-and-report.md) |

Batch 25 (119-123; 119, 120, 122, 123 built, 121 planned; planned 2026-10-25, the stories have no pictures): Claude illustrates each book by driving the Gemini web app in the user's Chrome. 119 first (the pigs, the pilot; the user checks it), then 120 (the skill), then 121 one book per PR. Touches pictures and the store/starter builders only, so it can run beside Batch 24. Decisions 125-129.

Batch 26 (124-125, planned 2026-10-25, the Batch 25 mock and the animal-identity follow-up of Slice 117): 124 first (small; matches the words panel and switch card to the mock), then 125 (both edit `CastRows`/`CastPage` and the strings). 125 also merges after PR #336 (the pigs retell, `StarterBuilder`/`StarterTexts`). Decision 132.

Batch 27 (127-131, planned 2026-10-25, after Dino work kept resubmitting Read The Stupid Text to the Store): keep one repo, stop one product shipping the other. 127 first (small CI fixes), then 128 and 130 one after the other (both edit `release.yml` and `ship-targets.mjs`); 129 (code only) can run beside 127, 128 or 130; 131 runs alone between waves (it touches usings everywhere). Decisions 133-137.

Batch 28 (132-134, planned 2026-10-25, which books families like and how parents use the app): 132 first (the remote config the counts are switched by, and the forced update), then 133 (first events, privacy texts and store forms), then 134. All three edit `worker/` and Dino's `MauiProgram`, so one at a time; they can run beside Batch 27's 127, 128 and 130 (CI) but not beside 129's Dino edits to `CastPage`. Decisions 138-142.

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

Built but not verified (details in each slice file):

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
- Slice 124: emulator look of the panel fade, the bottom alignment and the one switch card (pigs starter upright); native read of the 3 new strings in the 13 non-English languages ([slice](docs/plan/slices/124-dino-words-panel-matches-mock.md)).
- Slice 125: emulator check (pigs starter in English and Farsi, cast page Animal line); store upload of three-little-pigs v5, billy-goats-gruff v4 and peter-rabbit v4 (done after merge); native read of the new strings and roster names in 13 languages; Windows head ([slice](docs/plan/slices/125-dino-character-animals.md)).
- Slice 106: Norwegian on a Windows head via `MarianEngine` (third-party conversion, never run); en to da/fi/sv/nl/nb and back on Windows; the Norwegian packs are about 390 MB each ([slice](docs/plan/slices/106-windows-nordic-dutch-translation-packs.md)).
- Windows has no `en-ja` translation pack (`TranslationPacks.FromEnglish` and `packs.yml` lack it), so English cannot be translated into Japanese offline on Windows; found while planning Slice 106, not yet scheduled.
- Older unverified items (Slices 43-95 and a flaky-test note), moved verbatim: [outstanding-older](docs/plan/notes/2026-10-25-outstanding-older.md). Still open until verified.

## Out of Scope

- **(Batch 28)** Any install, device or advertising id; per-family profiles or parental-consent flows; third-party analytics SDKs; analytics in RTST; remote config for RTST.
- **(Batch 27)** Splitting into two repos or publishing the shared code as NuGet packages; renaming the shared projects or their namespaces beyond Mobile.Shared; a commit flag that skips one product's release.

Earlier batches' out-of-scope lines (still binding unless a later Decision reverses them): [out-of-scope-history](docs/plan/notes/out-of-scope-history.md).
