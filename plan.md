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

| # | Slice | Status | PR | Story | File |
| --- | --- | --- | --- | --- | --- |
| 0 | Project scaffold | built | — | #3 | [00-project-scaffold.md](docs/plan/slices/00-project-scaffold.md) |
| 1 | Clipboard-read on hotkey at chosen speed (smallest E2E) | built | — | #4 | [01-clipboard-read-on-hotkey-at-chosen.md](docs/plan/slices/01-clipboard-read-on-hotkey-at-chosen.md) |
| 2 | Hotkey copies the current selection | built | — | #5 | [02-hotkey-copies-the-current-selection.md](docs/plan/slices/02-hotkey-copies-the-current-selection.md) |
| 3 | Auto-read on selection (UIA) | built | — | #6 | [03-auto-read-on-selection.md](docs/plan/slices/03-auto-read-on-selection.md) |
| 4 | Launch at startup | built | — | #7 | [04-launch-at-startup.md](docs/plan/slices/04-launch-at-startup.md) |
| 5 | Store packaging & CI | built | — | #8 | [05-store-packaging-ci.md](docs/plan/slices/05-store-packaging-ci.md) |
| 6 | Fix speed control | built | — | — | [06-fix-speed-control.md](docs/plan/slices/06-fix-speed-control.md) |
| 7 | Narrator voice selection | built | — | — | [07-narrator-voice-selection.md](docs/plan/slices/07-narrator-voice-selection.md) |
| 8 | Tray control panel window | built | — | — | [08-tray-control-panel-window.md](docs/plan/slices/08-tray-control-panel-window.md) |
| 9 | Local neural voices (sherpa-onnx + Supertonic-3) | built | — | — | [09-local-neural-voices.md](docs/plan/slices/09-local-neural-voices.md) |
| 10 | Live activity log + auto-read fix | built | — | — | [10-live-activity-log-auto-read-fix.md](docs/plan/slices/10-live-activity-log-auto-read-fix.md) |
| 11 | Overlord voice names | built | — | #46 | [11-overlord-voice-names.md](docs/plan/slices/11-overlord-voice-names.md) |
| 12 | Split auto-read into two toggles | built | — | #48 | [12-split-auto-read-into-two-toggles.md](docs/plan/slices/12-split-auto-read-into-two-toggles.md) |
| 13 | "Media Card" control-panel redesign + media-player | built | — | #52 | [13-media-card-control-panel-redesign-media.md](docs/plan/slices/13-media-card-control-panel-redesign-media.md) |
| 14 | MIT license + Conventional-Commits auto-release | built | — | #57 | [14-mit-license-conventional-commits-auto-release.md](docs/plan/slices/14-mit-license-conventional-commits-auto-release.md) |
| 15 | Deep code-review pass + fixes | built | — | #61 | [15-deep-code-review-pass-fixes.md](docs/plan/slices/15-deep-code-review-pass-fixes.md) |
| 16 | Store-pipeline finalize + signing docs, first release | built | — | #64 | [16-store-pipeline-finalize-signing-docs-first.md](docs/plan/slices/16-store-pipeline-finalize-signing-docs-first.md) |
| 17 | Warm the neural engine at startup | built | — | #84 | [17-warm-the-neural-engine-at-startup.md](docs/plan/slices/17-warm-the-neural-engine-at-startup.md) |
| 18 | Adaptive settle delay + smaller first chunk | built | — | #85 | [18-adaptive-settle-delay-smaller-first-chunk.md](docs/plan/slices/18-adaptive-settle-delay-smaller-first-chunk.md) |
| 19 | Local-only timing diagnostics | built | — | #86 | [19-local-only-timing-diagnostics.md](docs/plan/slices/19-local-only-timing-diagnostics.md) |
| 20 | Text sanitizer (redact/simplify noise) | built | — | #102 | [20-text-sanitizer.md](docs/plan/slices/20-text-sanitizer.md) |
| 21 | Daily on-disk logs + open-logs button | built | — | #103 | [21-daily-on-disk-logs-open-logs.md](docs/plan/slices/21-daily-on-disk-logs-open-logs.md) |
| 22 | Read-latency instrumentation + low-risk tuning | verified | #265 | #104 | [22-read-latency-instrumentation-low-risk-tuning.md](docs/plan/slices/22-read-latency-instrumentation-low-risk-tuning.md) |
| 23 | Voice swap continues the current read | built | — | #105 | [23-voice-swap-continues-the-current-read.md](docs/plan/slices/23-voice-swap-continues-the-current-read.md) |
| 24 | Draggable, position-persisted control panel | built | — | #106 | [24-draggable-position-persisted-control-panel.md](docs/plan/slices/24-draggable-position-persisted-control-panel.md) |
| 25 | Read-through timer (elapsed/total) | built | — | — | [25-read-through-timer.md](docs/plan/slices/25-read-through-timer.md) |
| 26 | Skip forward / backward (±10s) | built | — | — | [26-skip-forward-backward.md](docs/plan/slices/26-skip-forward-backward.md) |
| 27 | File upload: plain text (.txt) | built | — | — | [27-file-upload-plain-text.md](docs/plan/slices/27-file-upload-plain-text.md) |
| 28 | File upload: PDF | built | — | — | [28-file-upload-pdf.md](docs/plan/slices/28-file-upload-pdf.md) |
| 29 | File upload: DOCX | built | — | — | [29-file-upload-docx.md](docs/plan/slices/29-file-upload-docx.md) |
| 30 | MAUI Android scaffold + type-to-read (smallest E2E) | built | — | — | [30-maui-android-scaffold-type-to-read.md](docs/plan/slices/30-maui-android-scaffold-type-to-read.md) |
| 31 | Neural voice on Android (sherpa-onnx + Supertonic-3) | built | — | #29270 | [31-neural-voice-on-android.md](docs/plan/slices/31-neural-voice-on-android.md) |
| 32 | Camera capture → OCR → read | built | — | — | [32-camera-capture-ocr-read.md](docs/plan/slices/32-camera-capture-ocr-read.md) |
| 33 | File upload: .txt/.pdf/.docx (reused) | built | — | — | [33-file-upload.md](docs/plan/slices/33-file-upload.md) |
| 34 | Android CI: build + signed AAB, internal testing track | built | — | — | [34-android-ci-build-signed-aab-internal.md](docs/plan/slices/34-android-ci-build-signed-aab-internal.md) |
| 35 | Fix the stuck elapsed/total read-through timer | built | — | — | [35-fix-the-stuck-elapsed-total-read.md](docs/plan/slices/35-fix-the-stuck-elapsed-total-read.md) |
| 36 | Disk-backed audio chunks | built | — | — | [36-disk-backed-audio-chunks.md](docs/plan/slices/36-disk-backed-audio-chunks.md) |
| 37 | Reading text box: window, toggle, whole-chunk highlight | built | — | — | [37-reading-text-box-window-toggle-whole.md](docs/plan/slices/37-reading-text-box-window-toggle-whole.md) |
| 38 | Reading text box: zoom + fit-to-box pagination | built | — | — | [38-reading-text-box-zoom-fit-to.md](docs/plan/slices/38-reading-text-box-zoom-fit-to.md) |
| 39 | Windows: no artifacts, release via draft | built | — | — | [39-windows-no-artifacts-release-via-draft.md](docs/plan/slices/39-windows-no-artifacts-release-via-draft.md) |
| 40 | Android into the same pipeline and release | built | — | — | [40-android-into-the-same-pipeline-and.md](docs/plan/slices/40-android-into-the-same-pipeline-and.md) |
| 41 | Path-filtered per-platform builds | built | — | — | [41-path-filtered-per-platform-builds.md](docs/plan/slices/41-path-filtered-per-platform-builds.md) |
| 42 | Retention safety net | built | — | — | [42-retention-safety-net.md](docs/plan/slices/42-retention-safety-net.md) |
| 43 | Store listing refresh (9 languages) | built | #174 | — | [43-store-listing-refresh.md](docs/plan/slices/43-store-listing-refresh.md) |
| 44 | Pipeline: iOS filter group, platform-neutral Mobile root | built | #183 | — | [44-pipeline-ios-split.md](docs/plan/slices/44-pipeline-ios-split.md) |
| 45 | Android: document scanner into the editor | built | #189 | — | [45-android-document-scanner.md](docs/plan/slices/45-android-document-scanner.md) |
| 46 | Play button loading state | built | #185 | — | [46-play-loading-state.md](docs/plan/slices/46-play-loading-state.md) |
| 47 | Head start: sentence-level audio cache | built | #201 | — | [47-head-start-sentence-cache.md](docs/plan/slices/47-head-start-sentence-cache.md) |
| 48 | iOS target and TestFlight from CI | built | #186 | — | [48-ios-target-testflight.md](docs/plan/slices/48-ios-target-testflight.md) |
| 49 | iOS reaches Android parity | built | #196 | — | [49-ios-parity.md](docs/plan/slices/49-ios-parity.md) |
| 50 | Voice packs, first-run language, slimmer app (Android) | built | #173 | — | [50-voice-packs-first-run-language.md](docs/plan/slices/50-voice-packs-first-run-language.md) |
| 51 | Multilingual voices (Supertonic, Piper fa, MeloTTS zh) | built | #197 | — | [51-multilingual-voices.md](docs/plan/slices/51-multilingual-voices.md) |
| 52 | PaddleOCR per language (Android) | built | #203 | — | [52-paddleocr-per-language.md](docs/plan/slices/52-paddleocr-per-language.md) |
| 53 | Android: localized UI and RTL | built | #194 | — | [53-localized-ui-rtl.md](docs/plan/slices/53-localized-ui-rtl.md) |
| 54 | Android: on-device translation | built | #199 | — | [54-on-device-translation.md](docs/plan/slices/54-on-device-translation.md) |
| 55 | iOS gets the Batch 9 features | built | #207 | — | [55-ios-batch9-features.md](docs/plan/slices/55-ios-batch9-features.md) |
| 56 | Windows: packs, first-run language, head start | built | #204 | — | [56-windows-packs-language-headstart.md](docs/plan/slices/56-windows-packs-language-headstart.md) |
| 57 | Windows: localized UI and translation | built | #217 | — | [57-windows-localization-translation.md](docs/plan/slices/57-windows-localization-translation.md) |
| 58 | Dino Reader skeleton: shared Mobile library, new app, pipeline | built | #191 | — | [58-dino-reader-skeleton.md](docs/plan/slices/58-dino-reader-skeleton.md) |
| 59 | Dialogue parser and voice caster (pure) | built | #175 | — | [59-dialogue-parser-voice-caster.md](docs/plan/slices/59-dialogue-parser-voice-caster.md) |
| 60 | Reading with a voice per character | built | #193 | — | [60-per-character-voice-reading.md](docs/plan/slices/60-per-character-voice-reading.md) |
| 61 | Gallery, stories on device, grown-up gate | built | #198 | — | [61-gallery-grownup-gate.md](docs/plan/slices/61-gallery-grownup-gate.md) |
| 62 | Reading screen: text and picture modes | built | #200 | — | [62-reading-screen-modes.md](docs/plan/slices/62-reading-screen-modes.md) |
| 63 | Positioned text extraction, kept page images | built | #205 | — | [63-positioned-text-extraction.md](docs/plan/slices/63-positioned-text-extraction.md) |
| 64 | Picture mode on real pages | built | #218 | — | [64-picture-mode-real-pages.md](docs/plan/slices/64-picture-mode-real-pages.md) |
| 65 | Cast screen | built | #202 | — | [65-cast-screen.md](docs/plan/slices/65-cast-screen.md) |
| 66 | Dino Reader languages and translation | built | #208 | — | [66-dino-reader-languages-translation.md](docs/plan/slices/66-dino-reader-languages-translation.md) |
| 67 | Dino Reader on iOS and Windows | built | — | #221 | [67-dino-reader-ios-windows.md](docs/plan/slices/67-dino-reader-ios-windows.md) |
| 68 | Landing site served by a Cloudflare Worker | built | #182 | — | [68-landing-site-on-worker.md](docs/plan/slices/68-landing-site-on-worker.md) |
| 69 | Android APK downloads from R2, with a "latest" link | built | #187 | — | [69-apk-downloads-on-r2.md](docs/plan/slices/69-apk-downloads-on-r2.md) |
| 70 | Split the build pipeline into the Lets-Call-Mom shape | built | #190 | — | [70-split-build-pipeline.md](docs/plan/slices/70-split-build-pipeline.md) |
| 71 | Repo hygiene and the Drive mirror | built | #184 | — | [71-repo-hygiene.md](docs/plan/slices/71-repo-hygiene.md) |
| 72 | Android dependencies to latest, R8 shrinking and obfuscation | built | — | #220 | [72-android-deps-r8-obfuscation.md](docs/plan/slices/72-android-deps-r8-obfuscation.md) |
| 73 | Edge-to-edge on Android 15+: safe areas on every screen | built | — | #224 | [73-edge-to-edge-safe-areas.md](docs/plan/slices/73-edge-to-edge-safe-areas.md) |
| 74 | Dino Reader site: landing, privacy and downloads on dinoreader.sirous.uk | built | #219 | — | [74-dino-reader-site.md](docs/plan/slices/74-dino-reader-site.md) |
| 75 | Windows translation engine | built | — | #223 | [75-windows-translation-engine.md](docs/plan/slices/75-windows-translation-engine.md) |
| 76 | Soft line-break reflow and heading detection (both apps) | built | | #233 | [76-soft-line-break-reflow.md](docs/plan/slices/76-soft-line-break-reflow.md) |
| 77 | Dino Reader: covers that survive edits, cover picker, readable inputs | built | | #234 | [77-dino-covers-and-light-theme.md](docs/plan/slices/77-dino-covers-and-light-theme.md) |
| 78 | Dino Reader: page-review wizard (pages → cover → text) | built | | #235 | [78-dino-page-review-wizard.md](docs/plan/slices/78-dino-page-review-wizard.md) |
| 79 | Background text filtering with a manual override | built | #240 | | [79-background-text-filtering.md](docs/plan/slices/79-background-text-filtering.md) |
| 80 | Dino Reader: grown-up mode as a gallery overlay | built | #241 | | [80-grownup-gallery-overlay.md](docs/plan/slices/80-grownup-gallery-overlay.md) |
| 81 | A language change in Settings downloads that language's voice | built | #239 | | [81-language-change-starts-voice-download.md](docs/plan/slices/81-language-change-starts-voice-download.md) |
| 82 | The phone voice reads in the text's language, and is only offered when it can | built | #243 | | [82-phone-voice-reads-the-texts-language.md](docs/plan/slices/82-phone-voice-reads-the-texts-language.md) |
| 83 | Scan replaces the text, like Paste and File | built | #245 | | [83-scan-replaces-the-text.md](docs/plan/slices/83-scan-replaces-the-text.md) |
| 84 | Scan reads the page in the page's script, whatever the app language | built | #246 | | [84-ocr-follows-the-page-not-the-app-language.md](docs/plan/slices/84-ocr-follows-the-page-not-the-app-language.md) |
| 85 | The layout never mirrors; only text flows by its own direction | built | #247 | | [85-no-layout-mirroring.md](docs/plan/slices/85-no-layout-mirroring.md) |
| 86 | RTST scan reads with line geometry | built | #248 | | [86-rtst-scan-line-geometry.md](docs/plan/slices/86-rtst-scan-line-geometry.md) |
| 87 | A scan in a script with no reader asks the page's language and downloads it | built | #253 | | [87-scan-downloads-the-page-language-pack.md](docs/plan/slices/87-scan-downloads-the-page-language-pack.md) |
| 88 | The Windows desktop layout never mirrors; only text flows by its own direction | built | #254 | | [88-desktop-no-layout-mirroring.md](docs/plan/slices/88-desktop-no-layout-mirroring.md) |
| 89 | Four Farsi voices in one pack (ganji_adabi by default), Amir dropped | built | #262 | | [89-four-farsi-voices.md](docs/plan/slices/89-four-farsi-voices.md) |
| 90 | Untranslated text is read in its own language | built | #258 | | [90-untranslated-text-reads-in-its-own-language.md](docs/plan/slices/90-untranslated-text-reads-in-its-own-language.md) |
| 91 | Dino stories as packages, three starter stories, developer export | built | #263 | | [91-dino-story-packages-and-starters.md](docs/plan/slices/91-dino-story-packages-and-starters.md) |
| 92 | A free, curated Dino story store behind the grown-up gate | built | #266 | | [92-dino-story-store.md](docs/plan/slices/92-dino-story-store.md) |
| 93 | Dino backup file and a first run that asks the language | built | #267 | | [93-dino-backup-and-first-run.md](docs/plan/slices/93-dino-backup-and-first-run.md) |
| 94 | "Good Enough" ships as a starter story | built | #271 | | [94-good-enough-starter.md](docs/plan/slices/94-good-enough-starter.md) |
| 95 | Dino translation works: bundled translations for every shipped book, live translation on Android | built | #271 | | [95-dino-translation-works.md](docs/plan/slices/95-dino-translation-works.md) |

Batch 9 (43-57) and Batch 10 (58-67) lane order: see [plan-batch-9-10-index.md](docs/plan/notes/plan-batch-9-10-index.md).

Batch 13 (76-80, planned 2026-10-22): 76 and 77 are independent and go first (Touches do not overlap); 78 after 77; 79 and 80 after 78 (both edit the wizard's files). Decisions 83-87.

Batch 14 (81-85, planned 2026-10-22, Android bugs found on a Pixel 10 Pro running 1.16.0): 81 and 83 are independent and go first; 82 after 81; 84 after 83; 85 last (it edits the pages 81 and 82 touch). Decisions 88-92.

Batch 15 (89, planned 2026-10-22): the Farsi voice swap; touches packs.yml, the pack format and every platform reader, so nothing else in those files runs alongside it. Decision 93.

Batch 16 (91-93, planned 2026-10-22, Dino Reader content from the phone scope): 91 first (the `.dino` package the other two use); 92 and 93 after it, one at a time (both edit `GalleryPage` and `MauiProgram`). The store, backup and first-run screens wait for a Claude Design pass. Decisions 94-99.

Batch 17 (94-95, planned 2026-10-23): 94 first (the user's own book as a starter), then 95 (it translates all four starters, so it rebuilds 94's package). Both edit `StarterBuilder` and the starter packages, so they run one after the other.

### Notes

- [Decisions 1-99](docs/plan/decisions.md): the full numbered decision list, moved verbatim.
- [2026-10-20 Android minSdk 28](docs/plan/notes/2026-10-20-android-min-sdk-28.md): why the Android floor is 28 (Decision 82).
- [Batch introductions, Batches 2-8](docs/plan/notes/batch-intros-1-8.md): the framing paragraph that opened each batch (2026-10-20 move).
- [Batch 9 and 10 indexes](docs/plan/notes/plan-batch-9-10-index.md): the former in-plan tables and lane order.
- [General verification lines](docs/plan/notes/verification-general.md): the verification lines that belong to no single slice.
- [CLAUDE.md history](docs/plan/notes/claude-md-history.md): the Batch 6-10 narrative blockquotes moved out of `CLAUDE.md` (2026-10-20).

## Outstanding work

Built but not verified (details in each slice file):

- Slice 43: the 8 translated Play listings are only worth adding when the app goes to Play production (it is closed testing now); get a native read first. The Microsoft Store keeps its existing Windows-specific listing (decided 2026-10-20). ([slice](docs/plan/slices/43-store-listing-refresh.md))
- Slice 59: attribution quality on a real children's book text (only unit-test fixtures so far); check with the first Slice 60 read ([slice](docs/plan/slices/59-dialogue-parser-voice-caster.md)).
- Slice 58: decide whether Dino Reader project additions to `ReadTheStupidText.slnx` / the workflows should keep re-releasing the RTST apps (today `*.slnx` and `release.yml` are core paths in `ship-targets.mjs`); seen when Slice 59's merge shipped an unchanged RTST release ([slice](docs/plan/slices/58-dino-reader-skeleton.md)).
- Slice 58: Play Console app creation and the first manual internal-track upload for `uk.sirous.dinoreader` ([slice](docs/plan/slices/58-dino-reader-skeleton.md)).
- Slice 58: the Dino upload key and Play service-account secrets (`DINOREADER_*`) generated and stored ([slice](docs/plan/slices/58-dino-reader-skeleton.md)).
- Slice 58: a signed Dino release cut by CI ([slice](docs/plan/slices/58-dino-reader-skeleton.md)).
- Slice 46: the Windows tray panel's progress ring in the running app (auto-read, copy, hotkey; clears on supersede/failure/completion); follow-up: a reader-level generating signal on `ISpeechReader` for Android ([slice](docs/plan/slices/46-play-loading-state.md)).
- Slice 69: real-R2 Range behaviour of `/download/android/latest` not separately tested ([slice](docs/plan/slices/69-apk-downloads-on-r2.md)).
- Slice 45: a real multi-page scan on a Google-signed-in device (module download, edge detection, page order, gallery import, text appended and read aloud) ([slice](docs/plan/slices/45-android-document-scanner.md)).
- Slice 45: the no-Play-services `MediaPicker` fallback on a device without Play services ([slice](docs/plan/slices/45-android-document-scanner.md)).
- Slice 48: installing from TestFlight on an iPhone and adding testers (manual); the iOS app's runtime behaviour (AVSpeech reading, file picker); no Mac or iPhone here ([slice](docs/plan/slices/48-ios-target-testflight.md)).
- Slice 70: a PR touching only one platform runs only that platform's workflow ([slice](docs/plan/slices/70-split-build-pipeline.md)).
- Slice 47: a voice switch (miss, then hit on switching back), head-start benefit and the 100 MB cap on a real phone; Slice 56 wires the cache on Windows. ([slice](docs/plan/slices/47-head-start-sentence-cache.md))
- Slice 49: a real-device or TestFlight pass (neural voice after the pack, skip, interruptions, lock screen, scanning, Vision OCR, txt/pdf/docx open); the `.ipa` contains a correctly signed `SherpaOnnxC.framework`; App Review acceptance of background audio. ([slice](docs/plan/slices/49-ios-parity.md))
- Slice 51: run `packs.yml` with `release_number=2` (all three versions `1`), then a one-line PR flipping `PackConstants.CatalogUrl` to `packs-2`; until then Farsi and Chinese read with the phone voice. Then hear fa and zh on a device, judge Arabic/Japanese quality and French-as-French by ear, and re-check the `fa_IR-amir-medium` licence before a wide launch. ([slice](docs/plan/slices/51-multilingual-voices.md))
- Slice 52: run `packs.yml` with a new release number and `ocr_pack_version=1`, then flip `PackConstants.CatalogUrl`; until then OCR packs are not live. Real scans in ar/fa/ru/zh/ja/fr (accuracy, reading order, RTL numbers, time per page), memory with the 84 MB CJK pack; follow-up: the voice installer does not check for newer voice-pack versions at start-up. ([slice](docs/plan/slices/52-paddleocr-per-language.md))
- Slice 53: a native-speaker read of the eight translations; Arabic RTL on the emulator (only Farsi walked through); follow-ups: translate neural voice names, fold `LocalizedPackStatus` into `PackStatusText`. ([slice](docs/plan/slices/53-localized-ui-rtl.md))
- Slice 54: offline translation with a cached model, offline first use (alert, original read), a 5,000-word text, the translate card in RTL, a native-speaker read; follow-up: streaming translation. iOS has no translator registered, so the card stays hidden. ([slice](docs/plan/slices/54-on-device-translation.md))
- Slice 60: each character's voice and the gaps between voice switches heard on a real device; the pre-existing Android timer bug (`elapsed > total`, not reset on idle) needs its own fix. ([slice](docs/plan/slices/60-per-character-voice-reading.md))
- Slice 61: scan and Photos import (blocked on a shared scan/OCR project, likely with Slice 63); the TalkBack path of the gate; edit, page reorder and delete on the emulator; strings are English-only until Slice 66. ([slice](docs/plan/slices/61-gallery-grownup-gate.md))
- Slice 62: the wander pill and Play halo during a read, the gate pass to Manage, the bedtime theme and RTL (waits on Slice 66), long-story pagination on a phone, the tablet layout and "Page n of m"; follow-up: share the read-start grace state machine with RTST. ([slice](docs/plan/slices/62-reading-screen-modes.md))
- Slice 65: preview audio, the Add-screen "Check the cast" path, opening the cast page from Edit, the narrator-change notice, reading in edited voices after a restart, the shared-voice note, RTL (Slice 66). ([slice](docs/plan/slices/65-cast-screen.md))
- Slice 56: system voice then neural handover on a real first run (download too fast to see; no log line names the engine); offline first launch; kill and resume mid-download; MSIX size and no `VoiceModel\`; the `internetClient` Store certification dry run; first-audio comparison for prepared text; follow-ups: pack/engine log lines, deduplicate with `Mobile.Shared` ([slice](docs/plan/slices/56-windows-packs-language-headstart.md)).
- Slice 63: draw boxes over a scanned page, an EXIF-rotated photo and PDFs with and without a text layer (ML Kit and PaddleOCR); RTL line order on a real Arabic/Farsi page; the RTST scan flow unchanged on a Google-signed-in device; follow-up: wire Dino's Scan, Photos and PDF import to the shared services ([slice](docs/plan/slices/63-positioned-text-extraction.md)).
- Slice 66: Farsi/Arabic UI mirroring and Russian/Chinese stories on a device; a native read of the 116 UI strings; follow-up: register the ML Kit translator for Dino ([slice](docs/plan/slices/66-dino-reader-languages-translation.md)).
- Slice 57: native-speaker review of the 9 machine-translated resw files; the Store listing showing the 9 languages after the next submission ([slice](docs/plan/slices/57-windows-localization-translation.md)).
- Slice 64: a real camera scan of a picture book on a device; an Arabic or Farsi scan highlighting the right lines; the denied-permission alert on Photos; whether the 1.1 zoom snap threshold feels right on a phone ([slice](docs/plan/slices/64-picture-mode-real-pages.md)).
- Slice 74: after the deploy, `https://dinoreader.sirous.uk` and `/privacy` load with the security headers; `/download/android` serves the Dino APK once a Dino release publishes; paste the privacy URL into Play Console's Dino Reader field; Play Families may want an email contact instead of the GitHub issues link ([slice](docs/plan/slices/74-dino-reader-site.md)).
- Slice 75: desktop acceptance of toggle, pack download and streamed translated read (session was locked); `packs-3` contents after the workflow run; Android/iOS still find their packs in `packs-3` ([slice](docs/plan/slices/75-windows-translation-engine.md)).
- Slice 94: the upgrade path for an install that already had the three starters gaining Good Enough ([slice](docs/plan/slices/94-good-enough-starter.md)).
- Slice 95: distinct Farsi voices per character; the upgrade path from a real older install (adopt logic unit-tested only); Windows and iOS reading bundled translations; a native-speaker read of the translations; Spanish/Portuguese/Russian translations make the Hare or Fox feminine while the voice may be male ([slice](docs/plan/slices/95-dino-translation-works.md)).
- Slice 72: Play Console "DEX obfuscation" metric after the next release (target >= 25%); iOS build on MAUI 10.0.120 / workload 10.0.401 in CI; a real-device scan with the GMS document scanner module ([slice](docs/plan/slices/72-android-deps-r8-obfuscation.md)).
- Slice 67: TestFlight build installs and reads in four voices with picture-mode boxes; Windows desktop flows (typed story, voices, PDF picture mode, Windows OCR); real Partner Center identity and `DINOREADER_STORE_APP_ID`, then a Store submission ([slice](docs/plan/slices/67-dino-reader-ios-windows.md)).
- Slice 73: Android 14 (API 34) inset path; landscape on both apps; RTST `LanguagePage` on first run; Dino `PageTextPage` with the keyboard up; Play's deprecated-API warning after the next release; follow-up: Dino gallery/Manage tiles re-lay out oddly after toggling night mode ([slice](docs/plan/slices/73-edge-to-edge-safe-areas.md)).
- Slice 76: on a device, a pasted hard-wrapped email and a scanned book page read with no pause mid-sentence (RTST Android, RTST Windows, Dino) ([slice](docs/plan/slices/76-soft-line-break-reflow.md)).
- Slice 77: page-2 cover pick on a multi-page story; a photo cover surviving an app restart; the Windows live theme switch (`UISettings.ColorValuesChanged`) ([slice](docs/plan/slices/77-dino-covers-and-light-theme.md)).
- Slice 78: scan 2 pages + 1 photo with the ML Kit scanner (needs a Google-signed device); Windows rotate/crop and drag-reorder; iOS on a device; only the changed page re-read; whether the Photos picker should multi-select; no discard confirmation on system back ([slice](docs/plan/slices/78-dino-page-review-wizard.md))
- ~~Slice 76 follow-up: geometry takeover~~ done: Dino in Slice 79, RTST scan in Slice 86.
- Slice 79: a real picture-book scan on a device; Windows overlay taps ([slice](docs/plan/slices/79-background-text-filtering.md)).
- Slice 80: Windows Dino nested taps and window recreate; TalkBack reading "Edit <title>" and "Delete <title>"; the 10-min `AwayTracker.ExternalActivityCap` timer (no test; lives in `DinoApp`) ([slice](docs/plan/slices/80-grownup-gallery-overlay.md)).
- Slice 86: a scanned page with a heading reads the heading as its own unit on a Google-signed-in Android device and in the Windows Mobile build (the emulator's ML Kit scanner fails) ([slice](docs/plan/slices/86-rtst-scan-line-geometry.md)).
- Slice 81: header "tap to retry" label (state flashes Resolving then Offline, no code defect found); a reinstalled pack restoring the chosen voice; all of it on a real phone ([slice](docs/plan/slices/81-language-change-starts-voice-download.md)).
- Slice 82: iOS voice selection on a device (CI compile only); Windows does not switch the reading language (by design for now). User decided 2026-10-22: untranslated text is read in its own language (Slice 90). ([slice](docs/plan/slices/82-phone-voice-reads-the-texts-language.md))
- Slice 83: one-page, two-page and blank-page scans on a Google-signed-in device; the editor read-only during OCR ([slice](docs/plan/slices/83-scan-replaces-the-text.md)).
- Slice 84: the Nutkin page with the app in Farsi, a Farsi page, and an English app with a Farsi page, on a Google-signed-in device. Resolved: the Farsi OCR pack is offered and downloaded (Slice 87). ([slice](docs/plan/slices/84-ocr-follows-the-page-not-the-app-language.md))
- Slice 85: Arabic content in the editor and typing Farsi on a real keyboard; the first-run Language page in Farsi/Arabic. Resolved: the Windows desktop no longer mirrors (Slice 88). ([slice](docs/plan/slices/85-no-layout-mirroring.md))
- Slice 87: on a Google-signed-in device with the app in English, scan a Farsi page, get asked, pick Farsi, see progress, and the page reads as Farsi; a second Farsi page has no prompt; Cancel keeps the text; airplane mode shows the message with Retry; native-speaker check of the 5 new strings ([slice](docs/plan/slices/87-scan-downloads-the-page-language-pack.md)).
- Slice 88: paging back on the desktop (it has no back control); the Farsi/Arabic desktop check itself passed ([slice](docs/plan/slices/88-desktop-no-layout-mirroring.md)).
- Flaky test `ReadAloudServiceGeneratingTests.Auto_read_prepares_the_head_start_before_the_settle_delay_ends` (timing; failed once in CI on 2026-10-21).
- Slice 90: iOS on a device (a different-language read uses the phone voice in that language); Spanish text in an English app; the "no voice" message for a language with neither a natural pack nor a phone voice. ([slice](docs/plan/slices/90-untranslated-text-reads-in-its-own-language.md))
- Slice 91: re-importing a `.dino` on a device (unit round-trip tests cover it) ([slice](docs/plan/slices/91-dino-story-packages-and-starters.md)).
- Slice 92: "Try again" after going offline and the gallery refresh on `StoreClient.LibraryChanged`, on a device; the production `/api/stories*` routes after deploy; the store is empty until curated stories are put in `dinoreader/stories/` in the bucket ([slice](docs/plan/slices/92-dino-story-store.md)).
- Slice 93: the language rebuild after first run (`DinoApp.RebuildRoot`) on a device; Windows backup save and restore; iOS; native-speaker review of the new strings in the 9 locales ([slice](docs/plan/slices/93-dino-backup-and-first-run.md)).
- Slice 89: Windows desktop acceptance of the four voices not run (a stale `desktop-rtl` dev package blocks deploy); iOS on a device; Pixel timing of cold voice preparation (7-8 s on the emulator, 16 s on a cold mid-read swap); a saved Amir choice falling back to Ganji Adabi, on a device; the play button shows idle after Back while chunks still play. ([slice](docs/plan/slices/89-four-farsi-voices.md))

## Out of Scope

- **(Batch 13)** Fuzzy re-mapping of edited story text onto OCR boxes (an edited page reads
  without highlight boxes, Slice 77); an ML layout model for background text (Slice 79 is
  size/position heuristics plus a manual override); any RTST UI for region filtering (the
  classifier is shared, the toggle is Dino-only); UIA font-weight/size as a heading signal
  on the Windows read path (a follow-up to Slice 76); perspective correction in the in-app
  page editor (the scanner already does it; Slice 78 is rotate + rectangular crop); iOS
  device verification of Batch 13 (CI build only).
- Voice *tuning* beyond playback rate (pitch, volume, SSML prosody).
- Selecting from the Windows-installed voices — the picker offers **only** the
  bundled neural (Supertonic) voices (Slice 9). The neural model ships in the
  package; the app does not install or expose Windows/Narrator voices for
  selection (the WinRT voice is only an internal safety-net fallback if the
  packaged model files are ever missing).
- ~~Reading from non-UIA apps *without* the hotkey fallback.~~ **Now in scope**
  (Slice 10 follow-up): auto-read on **clipboard copy** covers the console / other
  non-UIA apps, with the hotkey still the always-on fallback.
- Non-Store / sideload as a primary distribution channel (MSIX may be
  sideloaded for testing, but Store is the target).
- A persistent/dockable settings window with its own taskbar presence, tabs,
  or hotkey remapping UI. The Slice 8 control panel is a transient, tray-toggled
  surface (pinned topmost while open, hidden otherwise) — every control still
  maps to one of the existing services; no new configurable settings are
  introduced. The Slice 10 activity-log window is a separate diagnostic window
  (read-only, in-memory, cleared on restart) — not a settings surface.
- ~~Persisting the activity log to disk, exporting it, or log-level configuration
  (Slice 10 is in-memory and live-only).~~ **Partly superseded (Batch 4, Slice
  21):** the app now writes **daily diagnostic files** to the package
  TemporaryFolder. The in-memory `IActivityLog` is still live-only/unpersisted;
  log **levels remain fixed** (no user-facing level config).
- Pure UWP packaging.
- **(Batch 2)** A purchased OV/EV code-signing certificate and signing the
  sideload MSIX in this batch — the domain `sirous.uk` cannot sign code; **Azure
  Trusted Signing** is the documented later upgrade (Decision 18).
- **(Batch 2)** True audio scrubbing/seek in the progress bar — best-effort
  chunk-boundary resync only, because synthesis is chunked/streamed
  (Decision 21).
- ~~**(Batch 2)** Going live on Partner Center~~ — **Done.** The app is live in
  the Store; `store-submit.yml` is functional and the `STORE_PRODUCT_ID` variable
  is set (only the four Azure AD/seller secrets remain). See the *Store launch*
  item under Batch 2 and `STORE.md`.
- **(Batch 2)** The design's click-away / Esc dismiss of the control panel — it
  stays pinned-topmost (Decision 20 keeps Decision 12).
- **(Batch 2)** Renaming voice **ids** or adding/removing voices — only the
  `DisplayName`s change (Decision 19).
- **(Batch 2)** Apache-2.0/GPL licensing or a CLA — the repo is plain MIT
  (Decision 16).
- **(Batch 4)** Storing **raw** (unredacted) text anywhere on disk — the file logs
  hold redacted text only (Decision 27/28); raw text stays in memory for the
  current read.
- **(Batch 4)** Remote/uploaded logs, log-level UI, or a configurable log
  location — files are local, fixed-level, in the package TemporaryFolder.
- **(Batch 4)** Deep latency rework — GPU/DirectML provider, a different TTS
  model/runtime, or audio caching. Slice 22 is instrumentation + thread/concurrency
  tuning only (Decision 30).
- **(Batch 4)** Restart-from-beginning on voice change, or live voice morphing of
  already-synthesized audio — the swap continues from the current chunk
  (Decision 29).
- **(Batch 4)** A full move/resize chrome (min/max buttons, snap layouts) on the
  control panel — only header-drag + remembered position (Decision 31).
- **(Batch 5)** True sample-accurate seek — skip stays best-effort to the nearest
  chunk boundary, not exactly 10.000s (Decision 32, extends Decision 21).
- **(Batch 5)** Drag-and-drop file upload onto the panel/tray icon — picker-only
  for v1 (Decision 34).
- **(Batch 5)** A multi-file queue/playlist — picking a new file supersedes the
  current read rather than queuing (Decision 34).
- **(Batch 5)** OCR of scanned/image-only PDFs — text-layer PDFs only
  (Decision 35).
- **(Batch 5)** Document formats other than `.txt`/`.pdf`/`.docx` (e.g. `.rtf`,
  `.epub`, `.odt`).
- **(Batch 5)** Resuming an uploaded file's read position across app restarts.
- **(Batch 6)** iOS and macOS builds — deferred to a later batch on the same
  MAUI codebase once there's an Apple developer account (Decision 37).
- **(Batch 6)** Any form of auto-read trigger on Android (text-selection
  monitoring, a global hotkey, or cross-app clipboard reads) — none are
  possible on the platform; text entry, file upload, and camera capture are
  the only input paths (Decision 38).
- **(Batch 6)** A background/tray-style presence, a floating always-on-top
  panel, or a draggable/position-persisted window — Android has no tray or
  always-on-top-window equivalent; the mobile app is a normal foreground app
  (Decision 38).
- **(Batch 6)** Live/real-time OCR scanning (a continuous camera overlay) —
  v1 is single-shot capture only (Decision 40).
- **(Batch 6)** OCR of handwriting beyond what ML Kit handles out of the box,
  or any cloud-based OCR fallback for hard cases (Decision 40).
- **(Batch 6)** A mobile equivalent of the activity-log window or the on-disk
  diagnostic logs (Decisions 15, 27) — deferred to a later batch; not needed
  for the first Android release to be usable.
- **(Batch 6)** Public/production Google Play release — CI publishes to the
  **internal testing** track only; production release is a later, deliberate
  step (Decision 42).
- **(Batch 7)** Sentence/word-level highlight precision — the text box
  highlights the whole playing chunk only (Decision 46).
- **(Batch 7)** Manual page navigation in the text box — v1 auto-follows
  playback only, no next/prev controls (Decision 47).
- **(Batch 7)** A size/duration threshold that keeps small reads in memory —
  every read is disk-backed uniformly (Decision 49).
- **(Batch 7)** A mobile equivalent of the reading text box — Windows-only for
  now, same deferral pattern as the activity log (Decision 38).
- **(Batch 8)** Downloadable per-PR builds — PRs are compile/test checks only
  (Decision 53).
- **(Batch 8)** A GitHub Actions spending limit / paid storage — a billing
  choice for the user, not a pipeline change. Not needed once nothing is
  uploaded.
- **(Batch 8)** Caching Git LFS objects (the ~145 MB voice model is fetched by
  every Windows/Android build). LFS **bandwidth** is a separate allowance from
  Actions storage. Path filtering already cuts how often it's fetched; revisit
  only if the billing page shows LFS bandwidth running short.
- **(Batch 8)** Splitting releases into per-platform tags (`win-v*` /
  `android-v*`). One `v<x.y.z>` per merge stays (Decision 17), with the
  unchanged platform's assets carried forward (Decision 57).
- **(Batch 9)** Cloud TTS, OCR or translation: everything user-facing stays
  on the device (Decision 60).
- **(Batch 9)** A live camera text overlay (point-and-read); scanning is the
  document scanner only (Decision 64).
- **(Batch 9)** Handwriting recognition beyond what PaddleOCR gives (Decision 65).
- **(Batch 9)** The kids book reader: a separate product, "Dino Reader", built in
  this repo on the shared core and planned in its own batch, not Batch 9 (Decision 59).
- **(Batch 9)** Mac Catalyst / macOS: a later spin-off on the same MAUI stack.
- **(Batch 9)** App Store public-release automation: CI stops at TestFlight, the
  App Store submission is by hand (Decision 70).
- **(Batch 9)** A store title change; only search terms and descriptions change
  (Decision 71).
- **(Batch 9)** Per-platform version tags: one `v<x.y.z>` per merge stays
  (Decision 69).
- **(Batch 10)** Word-by-word karaoke highlight: the engine has no word timestamps (Decision 79).
- **(Batch 10)** Cloud or LLM speaker attribution (a small on-device model is the only fallback, Decision 74).
- **(Batch 10)** Syncing or sharing stories between devices.
- **(Batch 10)** ~~A story store or marketplace.~~ Reversed by Decision 96 (Slice 92).
- **(Batch 10)** Illustrations generated by AI.
- **(Batch 10)** Dino Reader on the Windows tray: it is a normal app, no tray or hotkey.
