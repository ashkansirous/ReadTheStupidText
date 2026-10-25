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
| 96 | Dino's layout never mirrors when the language changes | built | #278 | | [96-dino-layout-never-mirrors.md](docs/plan/slices/96-dino-layout-never-mirrors.md) |
| 97 | The grown-ups button says "add a book", not a lock | built | #284 | | [97-dino-add-book-button.md](docs/plan/slices/97-dino-add-book-button.md) |
| 98 | Picture + text books: a picture per page, the text beside it | built | #285 | | [98-dino-picture-and-text-books.md](docs/plan/slices/98-dino-picture-and-text-books.md) |
| 99 | RTST phone: a minimal main page and a Settings page | built | #279 | | [99-rtst-mobile-minimal-main-page.md](docs/plan/slices/99-rtst-mobile-minimal-main-page.md) |
| 100 | RTST phone: "Take a photo of each page", wizard page 1 | built | #290 | | [100-rtst-mobile-page-capture-wizard.md](docs/plan/slices/100-rtst-mobile-page-capture-wizard.md) |
| 101 | RTST phone: wizard page 2, check the text, translate, read | built | #297 | | [101-rtst-mobile-review-and-translate.md](docs/plan/slices/101-rtst-mobile-review-and-translate.md) |
| 102 | Ten open stories in the Dino story store | built | #280 | | [102-dino-store-ten-stories.md](docs/plan/slices/102-dino-store-ten-stories.md) |
| 103 | Danish, Finnish, Swedish, Norwegian and Dutch in every app | built | #286 | | [103-nordic-dutch-languages.md](docs/plan/slices/103-nordic-dutch-languages.md) |
| 104 | A Norwegian voice pack (Piper nvcc, ten speakers) | built | #291 | | [104-piper-norwegian-pack.md](docs/plan/slices/104-piper-norwegian-pack.md) |
| 105 | Dino: story rules and book translations for the five new languages | built | #292 | | [105-dino-nordic-dutch-books-and-rules.md](docs/plan/slices/105-dino-nordic-dutch-books-and-rules.md) |
| 106 | Windows: offline translation packs for the five new languages | built | #281 | | [106-windows-nordic-dutch-translation-packs.md](docs/plan/slices/106-windows-nordic-dutch-translation-packs.md) |
| 107 | Dino: own books translate by default | built | #305 | | [107-dino-own-books-translate-by-default.md](docs/plan/slices/107-dino-own-books-translate-by-default.md) |
| 108 | Dino: a cast per reading language | built | #307 | | [108-dino-cast-per-reading-language.md](docs/plan/slices/108-dino-cast-per-reading-language.md) |
| 109 | Dino: one narrator per language, for every book | built | #309 | | [109-dino-one-narrator-per-language.md](docs/plan/slices/109-dino-one-narrator-per-language.md) |
| 110 | Dino: translated text on screen, with "Show original text" | built | #311 | | [110-dino-translated-text-on-screen.md](docs/plan/slices/110-dino-translated-text-on-screen.md) |
| 111 | Dino: parents read in their own voice | built | #313 | | [111-dino-parent-recordings.md](docs/plan/slices/111-dino-parent-recordings.md) |
| 112 | Dino: store filters by age and author | built | #306 | | [112-dino-store-age-author-filters.md](docs/plan/slices/112-dino-store-age-author-filters.md) |
| 113 | Dino: playful character voices (pitch and pace per character) | built | #314 | | [113-dino-character-flavours.md](docs/plan/slices/113-dino-character-flavours.md) |
| 114 | Dino: Kokoro storyteller voices for English | built | #316 | | [114-dino-kokoro-english-storyteller-pack.md](docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md) |
| 115 | Dino: storyteller voices by default | built | #323 | | [115-dino-storyteller-voices-by-default.md](docs/plan/slices/115-dino-storyteller-voices-by-default.md) |
| 116 | Dino: storyteller characters in every language | built | #325 | | [116-dino-storyteller-characters.md](docs/plan/slices/116-dino-storyteller-characters.md) |
| 117 | Dino: animal characters make their sounds | built | #327 | | [117-dino-animal-sounds.md](docs/plan/slices/117-dino-animal-sounds.md) |
| 118 | Dino: storyteller voices for Spanish, French and Portuguese | built | #330 | | [118-dino-storyteller-more-languages.md](docs/plan/slices/118-dino-storyteller-more-languages.md) |
| 119 | Dino: illustrated Three Little Pigs, and the starters in the store | built | #332, #334 | | [119-dino-illustrated-three-little-pigs.md](docs/plan/slices/119-dino-illustrated-three-little-pigs.md) |
| 120 | Dino: a repo skill for making a story | built | #332 | | [120-dino-create-story-skill.md](docs/plan/slices/120-dino-create-story-skill.md) |
| 121 | Dino: pictures for the ten store stories | planned | | | [121-dino-illustrate-store-stories.md](docs/plan/slices/121-dino-illustrate-store-stories.md) |
| 122 | Dino: words panel at most 30% of the picture, and a words switch | built | #335, #340 | | [122-dino-text-panel-30-and-switch.md](docs/plan/slices/122-dino-text-panel-30-and-switch.md) |
| 123 | Dino: the pigs retold as 16 pages, one action each | built | #336 | | [123-dino-pigs-one-action-per-page.md](docs/plan/slices/123-dino-pigs-one-action-per-page.md) |
| 124 | Dino: the words panel and its switch match the Batch 25 mock | planned | | | [124-dino-words-panel-matches-mock.md](docs/plan/slices/124-dino-words-panel-matches-mock.md) |
| 125 | Dino: each character knows its animal, and animal voices to match | planned | | | [125-dino-character-animals.md](docs/plan/slices/125-dino-character-animals.md) |
| 126 | Dino: keep the screen awake while a book is playing | planned | | | [126-dino-keep-screen-awake.md](docs/plan/slices/126-dino-keep-screen-awake.md) |

Batch 9 (43-57) and Batch 10 (58-67) lane order: see [plan-batch-9-10-index.md](docs/plan/notes/plan-batch-9-10-index.md).

Batch 13 (76-80, planned 2026-10-22): 76 and 77 are independent and go first (Touches do not overlap); 78 after 77; 79 and 80 after 78 (both edit the wizard's files). Decisions 83-87.

Batch 14 (81-85, planned 2026-10-22, Android bugs found on a Pixel 10 Pro running 1.16.0): 81 and 83 are independent and go first; 82 after 81; 84 after 83; 85 last (it edits the pages 81 and 82 touch). Decisions 88-92.

Batch 15 (89, planned 2026-10-22): the Farsi voice swap; touches packs.yml, the pack format and every platform reader, so nothing else in those files runs alongside it. Decision 93.

Batch 16 (91-93, planned 2026-10-22, Dino Reader content from the phone scope): 91 first (the `.dino` package the other two use); 92 and 93 after it, one at a time (both edit `GalleryPage` and `MauiProgram`). The store, backup and first-run screens wait for a Claude Design pass. Decisions 94-99.

Batch 17 (94-95, planned 2026-10-23): 94 first (the user's own book as a starter), then 95 (it translates all four starters, so it rebuilds 94's package). Both edit `StarterBuilder` and the starter packages, so they run one after the other.

Batch 18 (96-98, planned 2026-10-24): 96 first (it changes how every Dino screen and the reader set direction), then 97 (a small gallery-header change, can run beside 96's tail), then 98 (its new reader layout builds on 96's non-mirroring reader). Decisions 100-102.

Batch 19 (99-101, planned 2026-10-24, after two first-time users couldn't find their way around the RTST phone app): 99, then 100, then 101, one at a time; all three edit `ReaderPage` and `AppShell`. Each ships on its own with a stand-in for the next one. Android and iOS only; the Windows tray app is unchanged. Decisions 103-105.

Batch 20 (102, planned 2026-10-24): ten open stories for the empty store. It touches only the test builders, translations, the upload script, `worker/stories.ts` and the store card, so it can run beside 96, 97 and 99-101. Illustrations follow as `story-v2` after Slice 98. Decision 106.

Batch 21 (103-106, planned 2026-10-24): Danish, Finnish, Swedish, Norwegian (Bokmål) and Dutch. 103 first (it adds the languages every other slice uses); then 104 (`packs.yml`, Piper readers), 105 (Dino rules and books) and 106 (`packs.yml`, Windows translation) can run alongside each other except that 104 and 106 both edit `packs.yml`, so they merge one after the other. Decisions 107-110.

Batch 22 (107-112, planned 2026-10-24, from the phone scope "translated text on screen, voices in the reading language, one narrator, parent recordings, store filters"): 107 first (own books translate by default; small). Then 108, 109, 110 and 111 one at a time in that order: all four edit `StoryVoicing`, `ReadingSession`, `Story`/`StoryJson` or the cast page. 112 (store filters) touches only the store and the Worker, so it runs beside any of them. Decisions 111-116.

Batch 23 (113-114, planned 2026-10-24, the voices sound too grown up for children's stories): 113 after 108 (flavours live on each language's cast), then 114 (the Kokoro English pack; flavours apply on top). 113 edits `StoryVoicing` and the cast page, so it does not run beside 109-111. Decisions 117-119.

Batch 24 (115-118, planned 2026-10-25, Dino stuck on the old voices; storyteller characters for every language and animal sounds): one at a time in order 115, 116, 117, 118 (all built). All four edit `StoryVoicing`, the cast page or the voice tables. Dino only; RTST unchanged. Decisions 120-124.

Batch 25 (119-123; 119, 120, 122, 123 built, 121 planned; planned 2026-10-25, the stories have no pictures): Claude illustrates each book by driving the Gemini web app in the user's Chrome. 119 first (the pigs, the pilot; the user checks it), then 120 (the skill), then 121 one book per PR. Touches pictures and the store/starter builders only, so it can run beside Batch 24. Decisions 125-129.

Batch 26 (124-125, planned 2026-10-25, the Batch 25 mock and the animal-identity follow-up of Slice 117): 124 first (small; matches the words panel and switch card to the mock), then 125 (both edit `CastRows`/`CastPage` and the strings). 125 also merges after PR #336 (the pigs retell, `StarterBuilder`/`StarterTexts`). Decision 132.

### Notes

- [Decisions 1-99](docs/plan/decisions.md): the full numbered decision list, moved verbatim.
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
- Slice 117: native read of all animal words and sounds (Persian pig and cow are guesses); `BleatWobble` depth by ear; iOS reader build. Known limit: sounds come from the character's name only, so in The Three Little Pigs only the Wolf sounds; follow-up planned as Slice 125 ([slice](docs/plan/slices/117-dino-animal-sounds.md)).
- Slice 118: the listening check of the es, pt and zh WAVs; real-phone Kokoro speed (about 9x slower than Supertonic on the emulator); a Chinese sentence with Latin words on a device; the Wi-Fi plus cellular pack-update path on a device ([slice](docs/plan/slices/118-dino-storyteller-more-languages.md)).
- Slice 119: on-device starter auto-update and recording copy (unchanged pages keep recordings, edited text is skipped); the pigs pictures in both orientations and the store download of pigs v3 ([slice](docs/plan/slices/119-dino-illustrated-three-little-pigs.md)).
- Slice 122: on the user's phone, pigs upright and sideways (panel at most 30%); photo/PDF import defaults the switch off; a wide picture sideways with the words off ([slice](docs/plan/slices/122-dino-text-panel-30-and-switch.md)).
- Slice 123: on the phone, the pigs show 16 pages with pictures; an installed v3 copy updates to v4 and keeps recordings on unchanged pages ([slice](docs/plan/slices/123-dino-pigs-one-action-per-page.md)).
- Slice 106: Norwegian on a Windows head via `MarianEngine` (third-party conversion, never run); en to da/fi/sv/nl/nb and back on Windows; the Norwegian packs are about 390 MB each ([slice](docs/plan/slices/106-windows-nordic-dutch-translation-packs.md)).
- Windows has no `en-ja` translation pack (`TranslationPacks.FromEnglish` and `packs.yml` lack it), so English cannot be translated into Japanese offline on Windows; found while planning Slice 106, not yet scheduled.
- Older unverified items (Slices 43-95 and a flaky-test note), moved verbatim: [outstanding-older](docs/plan/notes/2026-10-25-outstanding-older.md). Still open until verified.

## Out of Scope

- **(Batch 23)** Children's or emotive voice models (none permissive and phone-sized; Chatterbox, Orpheus too big); an expressive Farsi voice (none exists; parent recordings, Slice 111, are the Farsi answer); Kokoro for Spanish/French/Italian/Portuguese until English proves out; flavours in RTST.
- **(Batch 22)** Cloud translation or TTS; sharing recordings between devices; voice cloning from a parent's recordings; several recorded voices per book (Mum, Dad, Grandma); iOS live translation of own books (Apple Translation framework, not researched); store search, language, length and "has pictures" filters.
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
- **(Batch 10)** ~~Illustrations generated by AI.~~ Reversed by Decision 125 (Slice 119).
- **(Batch 10)** Dino Reader on the Windows tray: it is a normal app, no tray or hotkey.
