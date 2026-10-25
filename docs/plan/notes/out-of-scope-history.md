# Out of scope, earlier batches

Moved verbatim from `plan.md` (2026-10-25). They still hold unless a later Decision reverses them; note that the Batch 8 and Batch 9 "one `v<x.y.z>` per merge" lines are reversed by Decision 137 (Slice 130).

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

