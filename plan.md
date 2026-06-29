# Plan: ReadTheStupidText — Windows 11 read-aloud tray app

## Context

The user wants a lightweight Windows 11 utility ("ReadTheStupidText") that reads text
aloud at a user-chosen speed (1x / 1.25x / 1.5x / 1.75x / 2x), with a tiny
control beside the clock for pause/resume and speed, that auto-reads selected
text where possible and falls back to a global hotkey for apps that don't
expose their selection (terminals, CLI, Claude Code). It must run at startup
and ship through the Microsoft Store. The shape was settled in `scope.md`;
this plan turns it into ordered, shippable vertical slices.

## Decisions

1. **Framework:** C#/.NET + WinUI 3 (Windows App SDK), packaged as MSIX —
   keeps full Win32 capabilities (tray, global hotkey, cross-app read) while
   staying Store-installable. Pure UWP rejected (sandbox blocks all three).
2. **Speech:** WinRT `Windows.Media.SpeechSynthesis` rendered through
   `MediaPlayer` with `PlaybackRate` for live, pitch-corrected speed; system
   default Win11 voice in v1.
3. **Trigger:** UI Automation `TextPattern` selection monitoring as the
   primary auto-read path, **plus** a global hotkey (default `Ctrl+Win+R`)
   that simulates copy + reads the clipboard as the fallback.
4. **Control surface:** notification-area icon via `H.NotifyIcon.WinUI` —
   left-click flyout (Play/Pause + five speed buttons), right-click context
   menu (Pause, Enable/Disable, Settings, Quit). Global hotkey also toggles
   pause/resume.
5. **Startup:** packaged `StartupTask` (Windows App SDK), user-toggleable,
   starts minimized to tray.
6. **Persistence:** last speed + enabled state in
   `ApplicationData.Current.LocalSettings`; default speed 1x.
7. **Distribution:** Microsoft Store; design within Store/restricted-capability
   rules from day one.
8. **Naming:** repo stays `ReadTheStupidText`; product display name "ReadTheStupidText".
9. **Toolchain:** before writing any Windows App SDK / WinUI / H.NotifyIcon
   code, confirm current stable versions and APIs via context7
   (`/microsoft/windowsappsdk`, `/microsoft/winui`, `H.NotifyIcon`).
10. **Voice selection (scope change):** narrator voice is now **in scope** —
    chosen from installed Windows voices (`SpeechSynthesizer.AllVoices`),
    surfaced as a tray **Voice** submenu, persisted by `VoiceInformation.Id`,
    applied to the next read. Voices are an open, machine-dependent set, so
    they are modelled as a `VoiceInfo` record (not an enum). Voice *tuning*
    (pitch/volume/SSML) and in-app voice installation stay out of scope.
11. **Tray menu invocation (speed-defect root cause):** H.NotifyIcon's default
    `PopupMenu` context-menu mode renders a *native Win32 menu* built from the
    `MenuFlyout`. It invokes each item's **`Command`** only — the WinUI
    **`Click`** event never fires — and it renders a checkmark only for
    `ToggleMenuFlyoutItem`, not `RadioMenuFlyoutItem` (which falls through to the
    plain item case). That is why selecting a speed did nothing *and* never
    showed as selected. The fix drives every tray item through an `ICommand` and
    models the five speeds as mutually-exclusive `ToggleMenuFlyoutItem`s
    (single-selection managed in code). The same Command pattern applies to the
    Voice submenu in Slice 7. (The earlier "window never activated / XAML root"
    theory was wrong — confirmed against the H.NotifyIcon source.)
12. **Tray control panel (Slice 8):** left-clicking the tray icon opens a
    **borderless always-on-top `AppWindow`** (`OverlappedPresenter`,
    `IsAlwaysOnTop=true`, no system title bar) — not a `Flyout`/`Popup` and not
    a native `PopupMenu`. Reasons: the rich controls this panel needs (a speed
    **slider**, a voice **`ComboBox`**) cannot live in H.NotifyIcon's
    `PopupMenu` (the same native-menu limitation behind Decision 11), and a
    WinUI `Flyout` has no usable anchor on the zero-size hidden tray window. A
    real window positions reliably above the taskbar and is **pinned topmost**
    ("hovers over all windows"). It is **not** light-dismissed: it stays open
    until the user clicks its **✕** or left-clicks the tray icon again (an
    earlier light-dismiss design was rejected in testing — the panel vanished as
    soon as you clicked into the app you were reading from). Closing only
    **hides** the window — the app keeps running in the tray; **Quit stays in the
    right-click menu only**. A **single** window instance is reused (left-click
    toggles show/hide), sized to its content and re-reading live state on open. The
    right-click context menu is **kept unchanged** — left-click → panel,
    right-click → menu (auto-read, launch at startup, quit). The panel is a
    View in the App project binding to the existing `ReadAloudService`,
    `IVoiceCatalog`, and `IStartupService`; no new Application/Infrastructure
    layers. Auto-read and launch-at-startup appear in **both** the panel and the
    right-click menu, so both surfaces read/write the same services to stay in
    sync.
13. **Speed is a decimal, not an enum (scope change, Slice 8):** reading speed
    moved from the five-value `ReadingSpeed` enum to a **`PlaybackRate`** value
    object — a decimal multiplier the user picks continuously from **0.5× to
    2.0× in 0.05 steps** (YouTube-style). The type clamps and snaps on
    construction so an out-of-range/off-step rate can't exist, persisted as a
    `double`. The control panel exposes the full range via a slider; the native
    tray menu (which can't host a slider) keeps **five quick presets**
    (`SpeedPresets`: 1/1.25/1.5/1.75/2), each just setting the rate to that
    value, with a checkmark only when the current rate exactly equals a preset.
    This supersedes the earlier "five speeds are an enum / never doubles"
    convention — speed is no longer a closed set.
14. **Neural voices via sherpa-onnx + Supertonic-3 (scope change, Slice 9):** the
    built-in Windows (OneCore) voices sound robotic and the high-quality
    Narrator **"Natural"/neural** voices are gated to Narrator — unusable by a
    Store MSIX app through any supported API (confirmed via Microsoft Learn +
    Microsoft Q&A; the only "unlock" is an unsupported HKLM registry hack a
    sandboxed MSIX can't do). So the app brings its own **local neural engine**:
    the **sherpa-onnx** runtime (Apache-2.0, .NET bindings) running the
    **Supertonic-3** voice model (`sherpa-onnx-supertonic-3-tts-int8-2026-05-11`,
    int8, ~145 MB, 10 voices — F1–F5 sid 0–4, M1–M5 sid 5–9, default M1).
    **Kokoro was evaluated and rejected**: its English voices live in a
    Chinese-focused multilingual bundle (v1.1 has *no* English male voice; v1.0
    is larger and ships GPL-adjacent `espeak-ng-data`). **Piper was rejected** as
    GPL (`espeak-ng` / `piper1-gpl`). Supertonic is English-first (31 languages,
    no Chinese baggage), comparable quality, ~half the size, and crucially uses a
    bundled `unicode_indexer` instead of espeak — **so there is no espeak GPL
    data concern**. The model (~145 MB) is **shipped inside the package** (under
    `VoiceModel/`, committed to the repo) — no first-run download, no network, so
    it works fully offline from install and needs no `internetClient` capability.
    The picker shows **only** the neural voices; a silent WinRT fallback remains
    only as a safety net should the packaged files ever be missing. sherpa-onnx
    generates PCM that we wrap as a stream and play
    through the existing `MediaPlayer`, so the 0.5–2.0× speed slider keeps
    working. Supersedes Decision 10's "voices come from installed Windows
    voices".
15. **Live activity log (Slice 10):** a separate, resizable **log window**
    (opened from the right-click tray menu, single-instance, normal taskbar
    window — *not* the pinned control-panel style) shows read activity **live**.
    A new **`IActivityLog`** (Application) is an in-memory, observable store the
    read paths write to; the window subscribes and renders. Each intercepted
    text is **one entry** whose state mutates in place through:
    **pending** (waiting the 0.5 s debounce) → **reading** → **read**;
    **ignored** (superseded during the wait, never read); **interrupted**
    (a new selection or a **deselect** stopped a read in progress); **failed**
    (synthesis/audio error). Entries carry timestamp, source (auto-read / hotkey
    / manual Play), and truncated text; the store is a ring buffer (~200, cleared
    on restart, no disk). Supporting the **deselect→interrupted** rule requires
    the UIA monitor — which today swallows empty selections — to emit a
    "selection cleared" signal so the service can stop the reader. The log is
    also the **diagnostic** for the "selecting text does nothing" bug: no entry
    on selection ⇒ the app exposes no UIA text (hotkey is the fallback) or
    auto-read is off; an entry that stalls before `reading` ⇒ a downstream
    reader issue.

    *Follow-up (post-Slice-10 fixes):*
    1. **Console / clipboard auto-read.** Reading the console (Windows Terminal /
       PowerShell — the **primary use case**, e.g. Claude Code's responses) is now
       supported: a Win32 clipboard-format listener (`IClipboardMonitor` =
       `ClipboardFormatListener`, `WM_CLIPBOARDUPDATE` on the tray window) auto-reads
       on **copy**, the only signal a console exposes (no UIA text selection). It's
       gated by the auto-read toggle and tagged with the new `Clipboard`
       **`ActivityTrigger`**. `ReadAloudService` de-dupes across the UIA, clipboard,
       and hotkey paths (`_lastTriggeredText`, plus a `_copyingForRead` guard so the
       hotkey's own synthesized Ctrl+C echo isn't re-read). Clipboard reads use the
       **Win32** API (`ClipboardReader` → `OpenClipboard`/`GetClipboardData`), not the
       WinRT clipboard, which is documented as readable only while focused — and this
       tray window is never activated. *Console caveat:* a bare **selection** still
       produces no signal; the user must **copy** (or enable Windows Terminal's
       `copyOnSelect`, which makes selecting auto-copy → auto-read). The hotkey
       remains the universal fallback.
    2. **Reason column.** Each non-pending/non-read entry carries an
       **`ActivityReason`** (`NewSelection` / `Deselected` / `Error`) surfaced as a
       **Reason** column so a read that was ignored or interrupted says *why*. The
       UIA monitor now distinguishes a genuine **empty selection** (deselect →
       `SelectionCleared`) from a **transient cross-process read failure** (left
       silent): previously a re-fired `TextSelectionChangedEvent` whose read threw
       mid-selection looked like a deselect and **falsely ignored/interrupted** the
       read the user wanted.
    3. **Source (window) column.** Each entry records the foreground window it came
       from (`WindowSource` = app + title, via `IForegroundWindow` /
       `ForegroundWindowProbe` — `GetForegroundWindow` + process name + title), shown
       as e.g. "Chrome — Inbox - Gmail". The old "Source" column (the trigger) was
       renamed **Trigger** (`ActivitySource` → **`ActivityTrigger`**).
    4. **Interrupt actually stops the audio (concurrency fix).** Previously
       `SpeakAsync` synthesized off-thread then *unconditionally* swapped its audio
       into the single shared `MediaPlayer`, and supersede only called `Pause()` — so
       a slow long read finishing late would play *after/over* the short read that
       replaced it (the "it still had them in a stack" bug; long reads also often
       seemed to never play). The reader now carries a **generation counter +
       synthesis `CancellationToken`**: a superseded synthesis is cancelled and can
       never reach the player. `ISpeechReader` gained **`Stop()`** (supersede now
       stops instead of pausing) and a **`Completed`** event raised only on natural
       end — so an entry is marked `read` on genuine completion, never on a
       stop-induced idle. Applied to both engines via `CompositeSpeechReader`.
    5. **Chunked, concurrent synthesis (faster long reads).** The neural engine
       synthesized the whole text in one slow `Generate` call. Now `SpeechTextChunker`
       splits text over 200 chars on paragraph → sentence → word boundaries; chunks
       synthesize **concurrently (degree 3, `SemaphoreSlim`)** but play **strictly in
       order** via an ordered await + per-chunk `MediaEnded` hand-off, so playback
       starts after the first chunk instead of after the whole text.
    6. **`GeneratingAudio` activity state.** While a read is synthesizing (nothing
       playing yet) the entry shows **`GeneratingAudio`** ("Generating audio"); it
       flips to `reading` on the reader's first `Playing` transition. So the log now
       surfaces the synthesis wait as its own state rather than appearing stuck.

16. **License — MIT (Batch 2).** The project ships under the **MIT** license:
    anyone may use, modify, and redistribute it — including commercially —
    provided they keep the copyright/permission notice (the "must credit the
    author" requirement the user asked for). MIT is chosen over **Apache-2.0**
    (its patent clause is overkill for a tray app) and over **GPL** (copyleft
    forces derivatives open — the opposite of "anyone can extend, even
    commercially"). The bundled dependencies are already MIT/Apache-2.0 with no
    GPL (see `STORE.md`), so MIT at the repo root is compatible. A `LICENSE`
    file is added and README's "License: TBD" is replaced.
17. **Versioning — GitVersion → automatic release (Batch 2).** Every merge to
    `main` ships a new version, derived by **GitVersion** from git history —
    **git tags are the source of truth** (not the manifest). A single `build.yml`
    run does version → build → release: GitVersion computes the next SemVer
    (`main` defaults to a **patch** bump; override per commit with
    `+semver: minor` / `+semver: major`, highest since the last tag wins), the
    MSIX is packaged with that version stamped into `Package.appxmanifest`
    `Version` (4-part `x.y.z.0`; the Store requires revision `0`) **at build
    time** (never committed), then a `v<x.y.z>` tag + GitHub Release are cut at
    the merge commit. Because it's one run, a plain `GITHUB_TOKEN` suffices — no
    PAT, no commit-back, no second workflow to trigger. Config lives in
    `GitVersion.yml`. (Earlier draft pushed a manifest-bump commit + tag from a
    separate workflow and needed a PAT to trigger the release; GitVersion removes
    all of that.)
18. **Signing — Microsoft Store re-signing only for now (Batch 2).** The Store
    re-signs the package on publish and is the trusted install channel
    (SmartScreen trusts Store apps), so the package keeps shipping **unsigned** from CI
    (`AppxPackageSigningEnabled=false` unchanged). A domain (`sirous.uk`)
    **cannot sign code** — code-signing certificates validate an *identity*, not
    domain control — so it plays no part. **Azure Trusted Signing** (~US$10/mo,
    Microsoft-run, GitHub-Actions-native, no hardware token) is documented in
    `STORE.md` as the one-step upgrade *if/when* a trusted **sideloaded**
    (GitHub-Release) MSIX is wanted; traditional OV/EV certs (cost + hardware
    token) and self-signed certs (SmartScreen still warns) are rejected. The
    GitHub-Release MSIX stays labelled "testing/sideload".
19. **Voice display names → Overlord characters (Batch 2).** The ten Supertonic
    styles are renamed to *Overlord* characters in `SupertonicVoiceTable` —
    **only** the `DisplayName` changes; the persisted `supertonic:F1…M5` ids and
    the sid order are untouched, so saved choices keep resolving. Mapping (sid
    order F1–F5 then M1–M5): F1 **Albedo**, F2 **Shalltear Bloodfallen**, F3
    **Yuri Alpha**, F4 **Lupusregina Beta**, F5 **Narberal Gamma**; M1
    **Momonga**, M2 **Demiurge**, M3 **Cocytus**, M4 **Sebas Tian**, M5
    **Pandora's Actor**. Default stays M1 = **Momonga** (the protagonist).
20. **Control panel redesign — "Media Card" (Batch 2, Slice 13).** The
    left-click control panel is rebuilt to the high-fidelity spec in
    `design_handoff_tray_panel/` ("Option C — Media Card"): a brand-gradient
    header (`linear-gradient(135deg,#5B57E8,#3B82F6)`) with the glyph watermark,
    a `NOW READING` eyebrow + app title, a live 5-bar **waveform** + dynamic
    status text, a **transport row** (40px play/pause circle + progress bar +
    speed pill), over a Fluent **settings list** (voice row, the two auto-read
    toggles, launch-at-startup) and a `Ctrl+Win+R` hotkey footer. Rebuilt with
    **native WinUI Fluent controls + theme resources** (not the HTML), light/dark
    following the system theme, acrylic/mica surface, 376 px wide, sized to
    content. It **keeps the existing pinned-topmost `AppWindow`** behavior
    (Decision 12) — the design's "dismiss on click-away / Esc" is **not** adopted
    (pinning was chosen deliberately in Slice 8 testing). The HTML + design
    tokens are the visual source of truth only, not code to ship.
21. **Media-player progress (Batch 2, folded into Slice 13).** The transport
    row's progress bar shows live read-through of the current utterance, driven
    by `MediaPlayer` position + chunk completion (the reader already
    chunks/streams, Decision 15). **Seeking is best-effort only** — at most a
    resync to a chunk boundary — because each chunk is independently synthesized
    PCM and a true scrub would require re-synthesis (out of scope). When
    idle/paused the bar is empty and the status reads `Ready`/`Paused`; the
    waveform animates only while reading.
22. **Auto-read split into two toggles (Batch 2, Slice 12).** The single
    "Auto-read" gate becomes two independent settings — **Auto-read on
    selection** (gates the UIA `ISelectionMonitor`) and **Auto-read on copy**
    (gates the `IClipboardMonitor`) — surfaced as two `ToggleSwitch`es in **both**
    the control panel and the right-click menu, persisted as two `ISettingsStore`
    flags, **both default on** so today's behavior is preserved. The global
    hotkey is unaffected (always on). `ReadAloudService` checks the relevant flag
    per path; the old single `IsEnabled` is migrated (an existing `false` maps
    both new flags off).
23. **Store identity wired + product display name "Read The Stupid Text" (Batch 2,
    Slice 16 / Slice 5 #24).** The app is reserved in Partner Center (Store ID
    `9NGT1BN1H92V`), so the real **Product identity** is wired into
    `Package.appxmanifest` and must match Partner Center exactly (confirmed via
    Microsoft Learn): `Identity/Name` = `AshkanSirous.ReadTheStupidText`,
    `Identity/Publisher` = `CN=53769961-EF08-4BA5-A1DE-7A51B62A9AA7`,
    `Properties/PublisherDisplayName` = `Ashkan Sirous`. The user-facing **product
    display name becomes "Read The Stupid Text"** (with spaces) — manifest
    `DisplayName`/`VisualElements DisplayName`/`Description`/StartupTask
    `DisplayName`, the tray tooltip, the control-panel header, and the activity-log
    window title. The **repo, package id (`ReadTheStupidText`), namespaces,
    assembly, and the StartupTask `TaskId`** stay unchanged (internal identifiers).
    The manifest `Version` is reset to **`0.1.0.0`** — pre-1.0 (the app isn't
    "v1" yet); Conventional-Commits versioning (Decision 17) takes it from there.
    Store **signing stays Store-only** (Decision 18). This wires the deferred
    Slice 5 manifest-identity task and advances Slice 16; the first Partner Center
    submission + CI secrets are still manual (see `STORE.md`).

24. **Warm the neural engine at startup (Batch 3, Slice 17).** The single biggest
    contributor to the "selecting does nothing, then suddenly reads" delay is the
    **cold engine build**: `SupertonicSpeechReader.EnsureTts()` builds the
    sherpa-onnx `OfflineTts` (loading the ~145 MB model) **lazily on the first
    `SpeakAsync`**, so the very first read after launch pays the entire model-load
    cost (seconds) with no audio and no feedback. `IVoiceModelService.InitializeAsync()`
    today only *locates* the model files; it does not build the engine. Fix: after
    the model is located, **eagerly build the `OfflineTts` on a background thread**
    and run **one tiny throwaway synthesis** (a short token, result discarded) to
    JIT/warm the ONNX graph, so the first *real* read is near-instant. Warm-up runs
    off the UI thread and is idempotent (the lazy `EnsureTts()` stays as the
    safety net if warm-up hasn't finished when the first read arrives). Cost is
    ~145 MB resident while the tray app idles and a few seconds of background CPU
    at launch — accepted (the app's whole job is reading on demand; a multi-second
    first-read stall is the worse trade). Warm-at-startup was chosen over
    warm-on-first-tray-interaction (the first read can bypass the tray via the
    hotkey) and over keeping it lazy (the status quo being fixed).
25. **Adaptive settle delay (Batch 3, Slice 18).** The fixed
    `ReadAloudService.SelectionDebounceMs = 500` adds a flat half-second to **every**
    auto-read before synthesis even begins. It exists only to collapse a
    drag-select (one UIA event per character grown) into a single read. Replace the
    flat 500 ms with an **adaptive settle**: a short baseline (~150 ms) that is
    **extended only while selection/clipboard events are still actively arriving**
    (a live drag), so a quick click/double-click select fires almost immediately
    while a drag still collapses to one read. The existing
    swapped-`CancellationTokenSource` supersede already makes a late extra read
    harmless (the newer read cancels the older). Chosen over a flat 150 ms (a fast
    drag could occasionally double-fire) and over keeping 500 ms (the latency being
    fixed). The same debounce path serves both the UIA-selection and clipboard-copy
    triggers. **Folded in:** make the **first chunk smaller** —
    `SpeechTextChunker` should bias the *first* chunk toward a single sentence (or
    less) so audio starts after a short first synthesis instead of after a whole
    first paragraph; later chunks keep the existing ~200-char paragraph→sentence→word
    splitting. This shortens time-to-first-audio without changing the
    concurrent-synthesis/ordered-playback model (Decision 15).
26. **Local-only timing diagnostics — no remote analytics; Aspire is dev-only
    (Batch 3, Slice 19).** The app's privacy stance is "we collect nothing," so
    analytics must measure latency **without anything leaving the device**. Decision:
    record **timing diagnostics in the existing in-memory `IActivityLog`** — at
    minimum **time-to-first-audio** (entry created → reader's first `Playing`
    transition) and **synthesis duration** per read — surfaced in the
    `ActivityLogWindow`. Nothing is transmitted, no third-party SDK, no cost, no
    privacy-policy change; it stays a live, in-memory, cleared-on-restart ring
    buffer (consistent with Decision 15 and the existing out-of-scope "no disk
    persistence/export"). **.NET Aspire was evaluated and rejected as a shipped
    mechanism** (confirmed via Microsoft Learn): Aspire is an opinionated stack for
    orchestrating and observing **distributed** apps via a **dev-time** AppHost +
    dashboard — it has nothing to orchestrate in a single-process WinUI 3 tray app
    and is not a redistributable runtime you bundle into an MSIX, and it would
    reintroduce the very telemetry-export/cost concern being avoided. Aspire's
    underlying tech is plain **OpenTelemetry**; the *optional* dev-only convenience
    is to instrument the read pipeline with OpenTelemetry `Activity`/`Meter` so a
    developer can attach a **local Aspire dashboard** (an OTLP viewer run on the dev
    machine) while tuning — shipping nothing and costing nothing. Remote/opt-in
    telemetry was rejected (needs a consent UI + policy change + ongoing cost for a
    local-first Store utility).
27. **On-disk daily diagnostic logs — two files, redacted, local-only (Batch 4,
    Slice 21).** To debug field problems (and the Slice 22 latency analysis) the app
    writes **two files per day** under the package **TemporaryFolder** (`logs\`):
    `yyyy-MM-dd-input.log` and `yyyy-MM-dd-system.log`. The **input log** is
    **append-only, one row per activity-log state transition** (it never rewrites a
    row — a new line is added with the new state), TSV with the Activity-Log columns
    plus the row **id**: `timestamp  id  trigger  state  reason  source  first-audio-ms
    synth-ms  redacted-text`. The **system log** is the diagnostic stream (every
    action, exception, and info/debug detail) written via **Serilog** (rolling file),
    each line carrying the same **id** so the two files join. The **Activity-Log
    window gets a top button** that opens the `logs\` folder in Explorer
    (`Launcher.LaunchFolderAsync`). **Logs store redacted text only** (Decision 28
    runs first) — consistent with the "we collect nothing / stays local" stance; raw
    text never touches disk. Retention: files older than **7 days** are deleted on
    startup. This **supersedes** the Batch-1 "no disk persistence / no log-level
    config" out-of-scope line for the *file* logs (the in-memory `IActivityLog` is
    unchanged); levels are fixed (Info/Debug/Warning/Error), not user-configurable.
28. **Text sanitizer — strip "noise" before reading & logging (Batch 4, Slice 20).**
    A new **`ITextSanitizer`** (Application; rules in Infrastructure) cleans
    intercepted text **before** `SpeakAsync` **and** before any logging, replacing
    each match with a short spoken-friendly summary rather than deleting it. Default
    categories, each an independently toggleable setting and **all default-on**:
    **URLs** (`www.google.com/sub/x?q=1` → `"x on google.com"` — last path segment +
    host), **email addresses** (→ `"an email address"`), **passwords / API keys /
    high-entropy tokens** (`key=`/`token=`/`password=` + long mixed runs → `"a
    password"` / `"a secret token"`), **long digit runs** (card / phone / account →
    `"a card number"` / `"a phone number"`), **file paths** (→ the file name),
    **GUIDs / hashes / commit SHAs** (→ `"an identifier"`), and **markdown/HTML
    noise** (`[text](url)` → `text`; strip `**`, backticks, raw tags, emoji, control
    chars). Pure, regex-driven, unit-testable — the litmus is that no secret is ever
    spoken or written to disk.
29. **Voice swap mid-read → continue from current point (Batch 4, Slice 23).** Today
    `SetVoice` only updates `_speakerId` and applies "to the next read"; changing the
    voice actor during a read does nothing audible. Decision: on a voice change while
    a read is active, **keep the already-played chunks, cancel pending synthesis, and
    re-synthesize the remaining chunks with the new speaker**, resuming at the current
    chunk index — using `SupertonicSpeechReader`'s existing generation-counter /
    `_currentChunkIndex` machinery. No repeat of what's already been heard; the voice
    switches mid-stream. (Restart-from-beginning was rejected — it replays audio the
    user already heard.)
30. **Read-latency analysis + low-risk tuning (Batch 4, Slice 22).** The user reports
    a paragraph can take up to ~7 s. Decision: first **instrument** — log per-chunk
    **synthesis** and **playback** timings (split, generate, wav-encode, first-audio)
    into the Slice 21 system log so the 7 s is attributable; then apply **low-risk
    wins** measured against those logs — raise sherpa-onnx `NumThreads` /
    `MaxSynthesisConcurrency` to match available cores, tighten first-chunk biasing
    (Decision 25). **Confirm sherpa-onnx threading knobs via context7 before
    changing them.** Deep model/runtime/provider changes (GPU/DirectML) stay out of
    scope this round.
31. **Draggable, position-persisted control panel (Batch 4, Slice 24).** The
    borderless "Media Card" panel is fixed in place. Decision: make it draggable by
    pointer-drag on the header (the panel is a real `AppWindow`, so drag via
    `AppWindow.Move` from pointer deltas, or a draggable title-bar region), and
    **persist the last position** in settings so it reopens where the user left it.
    Keeps the pinned-topmost / no-light-dismiss behavior (Decision 20).

## Changes

Ordered as vertical slices — each is end-to-end and independently runnable.

- [x] **Slice 0 — Project scaffold.** ([#3](https://github.com/ashkansirous/ReadTheStupidText/issues/3)) Create the WinUI 3 packaged (single-project
      MSIX) app via `dotnet new` / template (version confirmed through
      context7). Add `.gitignore` (VisualStudio + OS noise), `README.md`,
      `CLAUDE.md` (project conventions), and `AGENTS.md` (`@CLAUDE.md`). App
      boots to an empty window. Establish the layered folder structure
      (`Domain` / `Application` / `Infrastructure` / `App`/UI).
- [x] **Slice 1 — Clipboard-read on hotkey at chosen speed (smallest E2E).**
      ([#4](https://github.com/ashkansirous/ReadTheStupidText/issues/4)) Register the global hotkey; on press, read current clipboard text aloud
      via `SpeechSynthesis` + `MediaPlayer`. Tray icon present with a flyout
      exposing Play/Pause and the five speed buttons; speed changes apply live
      via `PlaybackRate`. This single slice proves TTS + speed + tray + hotkey.
- [x] **Slice 2 — Hotkey copies the current selection.** ([#5](https://github.com/ashkansirous/ReadTheStupidText/issues/5)) Extend the hotkey to
      simulate copy (send `Ctrl+C`) before reading, so the user can select in
      any app (incl. terminals/CLI/Claude Code) and have it read aloud. Persist
      last-used speed + enabled state to `LocalSettings`.
- [x] **Slice 3 — Auto-read on selection (UIA).** ([#6](https://github.com/ashkansirous/ReadTheStupidText/issues/6)) Add UI Automation
      `TextPattern` monitoring so selecting text in supporting apps
      (Notepad, modern apps, most browsers) auto-reads without the hotkey.
      Enable/Disable toggle in the tray menu gates this behavior. Hotkey
      remains the fallback for non-UIA apps.
- [x] **Slice 4 — Launch at startup.** ([#7](https://github.com/ashkansirous/ReadTheStupidText/issues/7)) Declared a packaged
      `windows.startupTask` extension (`desktop:Extension`, `Enabled="false"` so
      the user opts in) in `Package.appxmanifest`. `IStartupService` (Application)
      with `StartupTaskService` (Infrastructure) over `Windows.ApplicationModel.StartupTask`
      (`GetAsync`/`RequestEnableAsync`/`Disable`); the tray gains a **Launch at
      startup** toggle that reflects the *actual* OS state (enabling can be
      refused by the user/policy). The app already starts minimized to tray (its
      window is never shown), so startup launch needs no extra UI handling.
- [x] **Slice 5 — Store packaging & CI.** ([#8](https://github.com/ashkansirous/ReadTheStupidText/issues/8)) GitHub Actions workflow
      (`.github/workflows/build.yml`) builds + packages the single-project MSIX
      on `windows-latest` for **x64 and ARM64** and uploads each as an **unsigned**
      `.msix` artifact (the Store re-signs on submission, so CI needs no
      certificate). Checkout uses `lfs: true` for the LFS-tracked voice model;
      packaging uses `GenerateAppxPackageOnBuild=true` + `AppxBundle=Never`
      (single-project MSIX can't bundle, so per-arch packages) +
      `UapAppxPackageBuildMode=SideloadOnly` + `AppxPackageSigningEnabled=false`.
      Fixed the **`NETSDK1102`** Release error (`PublishTrimmed` requires
      self-contained — trimming disabled; packaged WinUI apps aren't trimmed) so
      Release now builds (verified locally, x64, 0/0). `STORE.md` documents the
      `runFullTrust` restricted-capability justification, the third-party licenses
      (all MIT/Apache-2.0, no GPL), and the build commands. **CI-only scope (per
      decision):** real Store identity (reserved Name + Publisher ID), signing
      secrets, and the `.msixupload` submission flow are deferred to Partner
      Center and documented as the remaining steps in `STORE.md` — no account was
      wired in. MSIX-packaging msbuild args confirmed via Microsoft Learn first.
      **CI verified green** on PR #41 (run 28160543687) — both arches build and
      upload; needed one fix: pass `RuntimeIdentifier` explicitly because
      `setup-msbuild`'s 32-bit MSBuild made the csproj infer `win-x86` →
      `NETSDK1032`. **Distribution:** on a `v*` tag the `build` workflow's
      `release` job publishes both `.msix` files to a **GitHub Release** (stable
      URLs, linked from the README's Download section) — workflow artifacts alone
      aren't a hosted/linkable source. `store-submit.yml` is a manual
      (`workflow_dispatch`) deploy that submits a release's MSIX via the **msstore
      CLI** (`microsoft/microsoft-store-apppublisher`); scaffolded but inert until
      a Partner Center account + secrets (`AZURE_AD_*`, `SELLER_ID`,
      `STORE_PRODUCT_ID`) and a first manual Store submission exist (the
      Actions msstore flow only *updates* a live free app). msstore CLI workflow
      confirmed via Microsoft Learn first; see `STORE.md`.

Added after the initial plan — **tackled next, before Slice 4 (startup) and
Slice 5 (store):**

- [x] **Slice 6 — Fix speed control.** The five speed items didn't commit
      selection or change the rate (defect from Slices 1/3). Root cause: in
      H.NotifyIcon's default `PopupMenu` mode the native menu invokes each
      item's `Command` (not the WinUI `Click` event) and only `ToggleMenuFlyoutItem`
      renders a checkmark. Fix: drive all tray items through an `ICommand`
      (`RelayCommand`) and model the speeds as mutually-exclusive
      `ToggleMenuFlyoutItem`s with selection managed in code. `SetSpeed` already
      drives `MediaPlaybackSession.PlaybackRate` live and on the next read
      (verified in `SpeechReader`). Bug fix — no new layer, just App wiring.
- [x] **Slice 7 — Narrator voice selection.** Modelled a `VoiceInfo` record
      (Id, DisplayName, Language) in Domain; added `IVoiceCatalog` (installed
      voices + default) and `ISpeechReader.SetVoice(id)` in Application, with
      `WinRtVoiceCatalog` over `SpeechSynthesizer.AllVoices` and
      `SpeechReader.SetVoice` (sets `SpeechSynthesizer.Voice`) in Infrastructure.
      Tray flyout gains a **Voice** submenu (`MenuFlyoutSubItem` of
      `ToggleMenuFlyoutItem`s, checkmark on the current voice, Command-driven
      like the speeds — `RadioMenuFlyoutItem`/`Click` don't work in PopupMenu
      mode, see Decision 11). `ISettingsStore.VoiceId` persists the choice;
      `ReadAloudService` restores it on startup and `CurrentVoiceId` falls back
      to the system default when unset or no longer installed. A voice change
      applies to the next read (can't swap mid-utterance). WinRT voice APIs
      confirmed via Microsoft Learn docs first.
- [x] **Slice 8 — Tray control panel window.** Left-clicking the tray icon
      opens a borderless, always-on-top control panel (see Decision 12) holding
      every interactive control in one place: a **Play/Pause** toggle bound to
      `ReadAloudService.StateChanged`, a **YouTube-style speed slider** spanning
      the full `PlaybackRate` range (0.5×–2.0× in 0.05 steps, see Decision 13)
      with the current value shown beside it, a **Voice `ComboBox`** over
      `IVoiceCatalog.InstalledVoices` (current voice preselected), and
      **Auto-read** + **Launch at startup** `ToggleSwitch`es. A custom **✕**
      button sits at the top; the panel is **pinned** above all windows and
      closes only via the ✕ or a second tray left-click — both only hide it,
      never exit. Positioned bottom-right above the taskbar (work-area- and
      DPI-aware), sized to its content. New `ControlPanelWindow` (View) in the App
      project; the existing right-click `MenuFlyout` is left intact (Quit lives
      there, plus the five preset speeds). Confirm H.NotifyIcon `LeftClickCommand`
      and WinUI 3 `AppWindow`/`OverlappedPresenter` (borderless + always-on-top +
      positioning) via context7/Microsoft Learn before coding.
      *As built:* the panel is a `ControlPanelWindow` with a Mica backdrop, an
      `OverlappedPresenter` (`SetBorderAndTitleBar(true, false)`, `IsAlwaysOnTop`,
      non-resizable, hidden from switchers), sized/positioned in device pixels
      via `GetDpiForWindow` + `DisplayArea.WorkArea` (height measured from the
      content after first layout, fixing an initial overflow). It is pinned (no
      light-dismiss — that was tried and rejected in testing). Speed moved from
      the `ReadingSpeed` enum to the `PlaybackRate` value object (Decision 13);
      the panel slider sets any 0.05 step, the tray menu keeps the five
      `SpeedPresets`. Cross-surface sync is event-driven: `ReadAloudService`
      raises `SpeedChanged` / `VoiceChanged` / `EnabledChanged`, which
      `MainWindow` uses to keep the menu's checkmarks current when the change
      originates in the panel; the panel re-reads live state each time it opens,
      and raises `StartupStateChanged` so the menu's startup toggle follows.
      `LeftClickCommand` + `NoLeftClickDelay` open the panel without a
      double-click wait. *Refinements after testing:* the slider no longer
      clobbers the persisted default (its initial coercion to the 0.5 minimum is
      suppressed, so a fresh state opens at 1×); **Play** now starts a read of
      the selection/clipboard when idle (via `PlayPauseOrReadAsync`) instead of
      being a no-op, shared by the tray Play item; and when the active rate isn't
      a preset (e.g. 1.05×) the tray menu surfaces it as a checked item at the
      top of the speed group. *Voice quality:* the built-in WinRT
      `SpeechSynthesizer.AllVoices` voices sounded robotic and Narrator's neural
      voices are unreachable by a Store app — addressed by Slice 9, which replaces
      them with a bundled local neural engine.
- [x] **Slice 9 — Local neural voices (sherpa-onnx + Supertonic-3).** Replaces the
      built-in voices with a local neural engine (see Decision 14). Added the
      **sherpa-onnx** runtime (Apache-2.0) + **Supertonic-3** model (Apache-2.0)
      via the `org.k2fsa.sherpa.onnx` NuGet package; Kokoro (Chinese-focused,
      no English male voice in the latest) and Piper (GPL) were rejected.
      `IVoiceModelService` (Application) + `SupertonicModelService` (Infrastructure)
      locate `sherpa-onnx-supertonic-3-tts-int8-2026-05-11`, which is **bundled in
      the package** under `VoiceModel/` (committed to the repo, ~145 MB) and read
      from `AppContext.BaseDirectory` — so it's ready offline at first launch, no
      download. `VoiceModelPaths` is just the model root dir, `SupertonicFiles`
      holds the layout. `SupertonicSpeechReader` builds an `OfflineTts` lazily
      (Supertonic config: duration_predictor / text_encoder / vector_estimator /
      vocoder + tts.json + unicode_indexer + voice.bin — no espeak, no lexicon),
      synthesizes PCM at 1×, wraps it as an in-memory WAV stream, and plays it
      through the existing `MediaPlayer` so the 0.5–2.0× slider stays live and
      pitch-corrected. `CompositeSpeechReader` routes to Supertonic (with the WinRT
      voice as a safety-net fallback only if the bundled files are missing);
      `NeuralVoiceCatalog` exposes **only** the Supertonic voices
      (`SupertonicVoiceTable`, the fixed 10-style set F1–F5/M1–M5 in sorted sid
      order, default *Male 1*); the panel/menu rebuild on
      `ReadAloudService.VoicesChanged`. A build-time target drops the duplicate
      `onnxruntime.dll` that WinML (`Microsoft.Windows.AI.MachineLearning`) and
      sherpa both ship, keeping sherpa's version-matched copy; the unused
      `systemAIModels` capability was removed (no `internetClient` is needed since
      the model ships in the package). sherpa-onnx C# API, model id, voice sid
      order (`generate_voices_bin.py` uses `sorted(*.json)`), and licensing all
      confirmed via context7 + NuGet + HF + the sherpa docs before coding.
      *Auto-read debounce (fix):* UIA fires a `SelectionChanged` per character
      while the user drag-selects, which previously triggered a burst of
      overlapping reads (the play/pause state bounced). `ReadAloudService` now
      debounces selections (500 ms quiet period, superseded reads cancelled via a
      swapped `CancellationTokenSource`), so a drag collapses into one read.
      *Unverified at build time (needs a real run):* neural audio output and the
      native runtime loading under package identity.
- [x] **Slice 10 — Live activity log + auto-read fix.** (see Decision 15) Adds a
      separate, resizable **activity-log window** opened from the right-click tray
      menu ("Show activity log"), showing read activity **live**. New
      `IActivityLog`/`ActivityLog` (Application, in-memory observable ring buffer,
      ~200, `EntryAdded`/`EntryChanged`) + Domain `ActivityEntry` /
      `ActivityState` (pending/reading/read/ignored/interrupted/failed) /
      `ActivitySource` (auto-read/hotkey/manual). `ReadAloudService` now opens an
      entry per intercepted text and drives its state: a new selection or deselect
      **supersedes** the active entry (pending→`ignored`; reading→`interrupted`,
      pausing the reader), the debounce elapsing flips it to `reading`, the reader
      returning to `Idle` marks `read`, and a synth/playback exception marks
      `failed`. The UIA monitor (`ISelectionMonitor`) gained a `SelectionCleared`
      event (emitted once on the transition to an empty selection) so a deselect
      interrupts an in-progress read. `ActivityLogWindow` (normal resizable
      window, single-instance, in switchers) renders rows via `ActivityRowVm`
      (state updates in place); seeds from existing entries on open. *Diagnostic
      for the "selecting text does nothing" bug:* the path is sound and builds
      clean; the log is the lens — if selecting in a UIA app (Notepad) produces an
      entry that reaches `read`, auto-read works and the originally-tested app
      simply exposes no UIA text (hotkey is the fallback); if Notepad shows
      nothing, the monitor isn't firing. **Needs a runtime check to confirm the
      root cause** (can't run the UI/UIA here). Logs all read sources, tagged.

**Batch 2 — release-readiness (Slices 11–16).** With the feature set complete,
this batch polishes voices + UI, then makes the project releasable: license,
automatic versioning, a code-review pass, and the Store-pipeline wiring, ending
in the **first auto-versioned release**. The version follows Conventional
Commits (default patch) and **stays `0.x`** — it is **not** forced to `v1.0.0`;
the app reaches `1.0.0` only when the user declares it stable. Ordered
smallest-first; each is independently shippable.

- [ ] **Slice 11 — Overlord voice names.** ([#46](https://github.com/ashkansirous/ReadTheStupidText/issues/46)) (Decision 19) Rename the ten
      `DisplayName`s in `SupertonicVoiceTable` to the Overlord mapping (default
      **Momonga** = M1); leave the `supertonic:` ids and sid order untouched.
      Smallest end-to-end change — the picker/menu show the new names with no
      engine change.
- [x] **Slice 12 — Split auto-read into two toggles.** ([#48](https://github.com/ashkansirous/ReadTheStupidText/issues/48)) (Decision 22) Add
      `AutoReadOnSelection` + `AutoReadOnCopy` to `ISettingsStore` and its impl
      (both default on; migrate an old `IsEnabled=false` to both off). Gate the
      UIA `ISelectionMonitor` and the `IClipboardMonitor` independently in
      `ReadAloudService`. Surface two `ToggleSwitch`es in the right-click menu
      **and** the control panel, kept in sync via the existing event pattern.
- [x] **Slice 13 — "Media Card" control-panel redesign + media-player
      progress.** ([#52](https://github.com/ashkansirous/ReadTheStupidText/issues/52)) (Decisions 20, 21) Rebuild `ControlPanelWindow` to the
      `design_handoff_tray_panel/` spec with native WinUI Fluent controls and
      light/dark theme resources: gradient header + glyph watermark + eyebrow/
      title, animated waveform + dynamic status text, transport row (play/pause
      circle + **live progress bar** + speed pill), Fluent settings list (voice
      row, the two auto-read toggles, launch-at-startup), hotkey footer. Keep the
      pinned-topmost `AppWindow` (Decision 12 — no click-away dismiss). Wire the
      progress bar to `MediaPlayer` position + chunk completion; seek is
      best-effort (chunk-boundary resync) only.
- [x] **Slice 14 — MIT license + Conventional-Commits auto-release.**
      ([#57](https://github.com/ashkansirous/ReadTheStupidText/issues/57)) (Decisions 16, 17) Add a `LICENSE` file (MIT, attributed to Ashkan Sirous)
      and replace README's "License: TBD". Add a CI job/workflow that, on merge
      to `main`, computes the next SemVer from Conventional Commits, writes it
      into `Package.appxmanifest` `Version` (`x.y.z.0`), commits the bump, and
      pushes a `v<x.y.z>` tag — which the existing `build.yml` release job turns
      into a GitHub Release. Document the commit convention in `CLAUDE.md`.
- [x] **Slice 15 — Deep code-review pass + fixes.** ([#61](https://github.com/ashkansirous/ReadTheStupidText/issues/61)) (Item 5) Run
      `/code-review-in-detail` over the full app, triage the findings, and fix
      the confirmed real bugs (each non-trivial fix referenced in the PR). The
      generated `summary-code-review.md` / `detailed-code-review.md` are the
      record. Gates the first release tag.
- [x] **Slice 15b — Unit test suite (from the Slice 15 review).** The review's
      top finding was zero automated tests. Added `tests/ReadTheStupidText.Tests`
      (xUnit v3, net10.0-windows) with 43 tests covering the pure logic:
      `PlaybackRate` (clamp/snap), `SpeechTextChunker` (paragraph→sentence→word
      splitting), `SupertonicVoiceTable` (sid mapping/default), `ActivityLog`
      (ring buffer + events), and the `LocalSettingsStore` legacy-`IsEnabled`
      migration (extracted to a pure `ResolveAutoReadFlag` so it needs no package
      identity). A CI `test` job gates `build`/`release` so a failing test blocks
      the release. Run locally with `dotnet test`.
- [x] **Slice 16 — Store-pipeline finalize + signing docs, first release.**
      ([#64](https://github.com/ashkansirous/ReadTheStupidText/issues/64)) (Decisions 18, and Slice 5's deferred Partner Center work) Verify
      `store-submit.yml` is correct (kept **inert** — no account), refresh
      `STORE.md` with the remaining Partner Center steps and the **Azure Trusted
      Signing** upgrade path, and confirm the Conventional-Commits versioning
      feeds the release/Store flow. Cut the **first auto-versioned release** —
      the tag is whatever the versioning produces (**stays `0.x`**; **not**
      `v1.0.0` until the user declares the app stable).

**Batch 3 — read-latency reduction + local diagnostics.** Addresses the user
report that "from selecting to reading takes a lot, and sometimes it feels like
it isn't picking up the text, then suddenly reads." Root-caused to three delays
in the read pipeline (Decisions 24–26): a cold neural-engine build on first
read, a flat 500 ms settle delay on every read, and a whole-first-paragraph
first chunk — plus a way to *measure* the improvement that keeps the "we collect
nothing" policy literally true. Ordered smallest-first; each is independently
shippable. (No GitHub issues yet — create via `plan-to-issues` if wanted.)

- [x] **Slice 17 — Warm the neural engine at startup.** ([#84](https://github.com/ashkansirous/ReadTheStupidText/issues/84)) (Decision 24) The biggest
      single win and the smallest change. After `IVoiceModelService` locates the
      model, eagerly build the `OfflineTts` on a background thread and run one tiny
      throwaway synthesis to warm the ONNX graph, so the first real read no longer
      pays the cold-start cost. Keep the lazy `EnsureTts()` as the fallback if a
      read arrives before warm-up finishes. No UI-thread blocking; idempotent.
- [x] **Slice 18 — Adaptive settle delay + smaller first chunk.** ([#85](https://github.com/ashkansirous/ReadTheStupidText/issues/85)) (Decision 25)
      Replace `ReadAloudService`'s flat `SelectionDebounceMs = 500` with a short
      (~150 ms) baseline that extends only while events keep arriving (a live
      drag), so click-selects fire fast and drags still collapse to one read. Bias
      `SpeechTextChunker`'s **first** chunk toward a single sentence so audio
      starts sooner; later chunks unchanged. Both shorten per-read
      time-to-first-audio without changing the concurrent-synthesis model.
- [x] **Slice 19 — Local-only timing diagnostics.** ([#86](https://github.com/ashkansirous/ReadTheStupidText/issues/86)) (Decision 26) Record
      time-to-first-audio (entry → first `Playing`) and synthesis duration per read
      into the existing in-memory `IActivityLog`/`ActivityEntry`, and surface them
      as column(s) in `ActivityLogWindow` — nothing transmitted, no third-party, no
      policy change. Document the optional dev-only OpenTelemetry + local Aspire
      dashboard path in `CLAUDE.md`/`STORE.md` (not shipped). Lets the Slice 17/18
      gains be measured on the user's machine instead of guessed.

**Batch 4 — diagnostic logs, text sanitizing, and read-control fixes.** Five
user requests: (1) two daily on-disk log files + an "open logs" button, (2)
redact/simplify noise (URLs, passwords, …) before reading, (3) a draggable
control panel, (4) make the new logs explain the ~7 s paragraph latency (plus
low-risk tuning), and (5) make a mid-read voice change actually take effect.
Ordered smallest/most-foundational first — the sanitizer (Slice 20) ships
listening value on its own *and* is the prerequisite for "logs store redacted
text," so it leads; logging (Slice 21) then unblocks the latency analysis (Slice
22). (No GitHub issues yet — create via `plan-to-issues` if wanted.)

- [x] **Slice 20 — Text sanitizer (redact/simplify noise).** ([#102](https://github.com/ashkansirous/ReadTheStupidText/issues/102)) (Decision 28) Add
      `ITextSanitizer` (Application) + a regex rule set in Infrastructure that
      rewrites URLs → `"page on host"`, passwords/tokens → `"a password"`, emails,
      long digit runs, file paths, GUIDs/hashes, and markdown/HTML noise to short
      spoken summaries. Each category an independent **default-on** setting
      (`ISettingsStore`); wire it into `ReadAloudService` so the sanitized text is
      what gets spoken (and, later, logged). Unit-test the rules (pure logic, fits
      the existing test story). End-to-end value: selecting a URL/password reads a
      clean summary instead of gibberish.
- [x] **Slice 21 — Daily on-disk logs + open-logs button.** ([#103](https://github.com/ashkansirous/ReadTheStupidText/issues/103)) (Decision 27) Add
      Serilog (rolling file) for the system log and a small thread-safe
      append-writer for the input log (one TSV row per activity-state
      transition, id-keyed, **redacted** text from Slice 20), both under the package
      `TemporaryFolder\logs`. Subscribe the input writer to `IActivityLog`
      events; thread the same id through the system log. Add the **open-logs**
      button to the top of `ActivityLogWindow` (`Launcher.LaunchFolderAsync`).
      Delete logs older than 7 days on startup. Promote the existing `Debug.WriteLine`
      UIA traces to the system log. **Built:** files are `system-YYYYMMDD.log` /
      `input-YYYYMMDD.log` (Serilog's rolling sink stamps the day as `YYYYMMDD` and
      can't prefix it, so both writers use that form for consistency, not the literal
      `yyyy-MM-dd-…` order). `ISystemLog` (Serilog) + `ILogFolder` in Application;
      `LogPaths`/`SerilogSystemLog`/`ActivityInputLog`/`InputLogRow` in Infrastructure;
      `ReadAloudService` logs each id-correlated action + exceptions.
- [~] **Slice 22 — Read-latency instrumentation + low-risk tuning.** ([#104](https://github.com/ashkansirous/ReadTheStupidText/issues/104)) (Decision 30)
      Log per-chunk split/generate/wav/first-audio timings into the system log so the
      ~7 s is attributable, then (context7-confirm sherpa threading first) raise
      `NumThreads`/`MaxSynthesisConcurrency` to fit the machine and tighten first-chunk
      biasing — measured against the new logs. No model/runtime swap this round.
      **Built:** `SupertonicSpeechReader` now takes `ISystemLog` and emits Debug lines —
      `split N chars into K chunk(s) in X ms (threads T, concurrency C)`, per chunk
      `chunk i/K (n chars): generate X ms, wav Y ms`, and once `first audio after X ms`
      (on the first chunk's `MediaOpened`) — all stamped with the activity-log **id**
      (threaded through a new optional `ISpeechReader.SpeakAsync(text, activityId)`
      param) so they join the input log. Tuning is **adaptive to `Environment.ProcessorCount`**:
      `SynthesisThreads = clamp(cores/2, 2, 4)` and `MaxSynthesisConcurrency =
      clamp(cores/threads, 2, 4)` — latency-first (more ONNX threads shorten the single
      first-chunk synthesis the user feels), sized so `threads * concurrency` fits the
      cores without oversubscribing. sherpa-onnx `config.Model.NumThreads` confirmed via
      context7 (`/k2-fsa/sherpa-onnx`) before the change. First-chunk biasing was already
      in place from Slice 18 (`SpeechTextChunker.BiasFirstChunkToOneSentence`) and is kept
      at one sentence — cutting *below* a sentence trades prosody for latency, a call to
      make against the new logs. **Deferred (#117, story #104 stays open):** verifying the
      latency win and any further biasing-tightening is data-driven and needs a real run
      under the (Package) profile, which can't be measured headlessly. #115 (per-chunk
      logs) and #116 (threading) are done.
- [x] **Slice 23 — Voice swap continues the current read.** ([#105](https://github.com/ashkansirous/ReadTheStupidText/issues/105)) (Decision 29) On
      `SetVoice` during an active read, cancel pending synthesis and re-synthesize the
      remaining chunks with the new speaker from the current `_currentChunkIndex`
      (reuse the generation-counter machinery); already-played audio is not repeated.
      Drive it from `ReadAloudService.SetVoice`.
      **Built:** the playback loop was extracted into a shared `SpeakChunksAsync(chunks,
      startIndex, speakerId, …)` that takes the **speaker as a parameter** (not the mutable
      field) so a change can't half-apply to queued chunks. `SetVoice` just records the
      selection (`_speakerId`); the loop switches **at the next chunk boundary** — when the
      current chunk finishes it notices `_speakerId` changed and restarts at the *next*
      chunk in the new voice (`BeginGeneration` cancels in-flight old-voice synthesis). So
      the current chunk finishes in the old voice: nothing already heard is repeated **and
      no unheard text is skipped**, and earlier chunks are never re-synthesized. (Reviewed
      with the user: chosen over resume-at-current-chunk, which replayed the current
      sentence.) A single-chunk read or a change during the last chunk applies to the next
      read. The native-reader logic isn't unit-tested (no engine without package identity),
      per the project's test story; runtime check under the (Package) profile remains.
- [ ] **Slice 24 — Draggable, position-persisted control panel.** ([#106](https://github.com/ashkansirous/ReadTheStupidText/issues/106)) (Decision 31) Make
      the borderless control panel draggable by its header (pointer-drag → `AppWindow`
      move) and persist the last position in `ISettingsStore` so it reopens in place;
      keep pinned-topmost / no light-dismiss.

## Out of Scope

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
- **(Batch 2)** Going live on Partner Center (real identity, secrets, first
  submission) — `store-submit.yml` stays inert and documented (Decision 18).
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

## Verification

- **Slice 0:** `dotnet build` succeeds; app launches to an empty window;
  scaffolding files present.
- **Slice 1:** copy text manually, press the hotkey → text is spoken; click
  each speed button → speech speed changes live; Play/Pause works.
- **Slice 2:** select text in Notepad, a terminal, a browser, and Claude Code,
  press the hotkey → each is read aloud; restart app → last speed/enabled
  state restored.
- **Slice 3:** with auto-read enabled, select text in Notepad/browser →
  reads automatically; select in a terminal → does not auto-read but hotkey
  still works; Disable toggle stops auto-read.
- **Slice 4:** enable startup, reboot → app is running in the tray; disable →
  it is not.
- **Slice 5:** CI run produces a signed MSIX; install the MSIX on a clean
  Win11 machine and confirm all of the above; Store certification dry-run
  passes capability checks.
- **Slice 6:** open the tray flyout, click each speed → the item shows as
  selected (radio check) and stays selected on reopen; trigger a read → speech
  plays at that rate; change speed mid-playback → rate changes live.
- **Slice 7:** the Voice submenu lists the installed Windows voices with the
  current one checked; pick a different voice → the next read uses it; restart
  the app → the chosen voice is restored; uninstall that voice → falls back to
  the system default without error.
- **Slice 8:** left-click the tray icon → the control panel opens above the
  taskbar, on top of all other windows, with no content clipped; click into
  another app → the panel **stays** on top (pinned, no light-dismiss); drag the
  speed slider → it moves in 0.05 steps across 0.5×–2.0× and the next/active read
  uses that rate; set a preset in the right-click menu → the slider reflects it
  on next open and the matching menu preset is checked; pick a voice in the
  `ComboBox` → the next read uses it; toggle Auto-read / Launch at startup →
  state matches the right-click menu (open the menu to confirm both surfaces
  agree); click ✕ or left-click the tray again → the panel hides but the app
  stays in the tray; Quit is reachable only from the right-click menu.
- **Slice 9:** on first launch (offline is fine — the model ships in the
  package) → the picker immediately lists the Supertonic neural voices (default
  Male 1; 5 male + 5 female) and the tray Voice submenu is present; a read uses
  the selected neural voice and sounds natural; the speed slider still changes
  the rate live; pick a different voice → the next read uses it; restart → the
  chosen voice is restored. **Auto-read debounce:** drag-select a sentence → it
  is read **once** after the selection settles (no burst of play/pause and no
  overlapping reads). Confirm the packaged build runs under package identity and
  that audio plays (the build-time ORT dedupe didn't break the native engine).
- **Slice 10:** right-click tray → **Show activity log** opens the log window.
  Select text in a UIA app (Notepad): an entry appears `pending`, flips to
  `reading`, then `read`. Quickly select "this is" then "this is a new text":
  the first row → `ignored`, the second runs `pending`→`reading`→`read`. While a
  read is playing, select something else or **deselect** → the row → `interrupted`
  and audio stops. Trigger a synth error (or pull the model) → `failed`. Hotkey
  and manual Play reads also appear, tagged by source. **Bug:** selecting text in
  the app you reported now either shows an entry (and reads) or shows nothing —
  if nothing, the log confirms that app exposes no UIA text (use the hotkey),
  which we verify against Notepad where it must work.
- **Slice 11:** open the Voice picker (control panel `ComboBox` + tray submenu)
  → the ten voices read **Momonga / Demiurge / Cocytus / Sebas Tian / Pandora's
  Actor** and **Albedo / Shalltear Bloodfallen / Yuri Alpha / Lupusregina Beta /
  Narberal Gamma**, default **Momonga**; a profile that had a saved voice id
  still resolves to the same style (id unchanged); a read uses the picked voice.
- **Slice 12:** with both toggles on, selecting in Notepad reads and copying in
  the console reads. Turn **Auto-read on selection** off → selecting no longer
  reads but copying still does; turn **Auto-read on copy** off instead →
  copying no longer reads but selecting does; the hotkey reads in all
  combinations. The two switches match between the panel and the right-click
  menu. A profile upgraded from the old single toggle (`IsEnabled=false`) opens
  with both new toggles off.
- **Slice 13:** left-click the tray → the panel matches the "Media Card" design
  (gradient header, glyph watermark, waveform, transport row, settings list,
  hotkey footer) in both light and dark system themes, ~376 px wide, no clipped
  content. Start a read → the **progress bar advances** and status text shows the
  source ("Reading selection from Notepad…"); pause → bar/waveform stop, status
  shows `Paused`; idle → `Ready`. The panel stays pinned when you click into
  another app (no click-away dismiss). Speed pill + slider, voice row, the two
  auto-read toggles, and startup all still drive their services.
- **Slice 14:** `LICENSE` (MIT) exists and the README License section reflects
  it. Merge a `feat:` PR → CI bumps the **minor** version in
  `Package.appxmanifest`, tags `v<x.y.z>`, and a GitHub Release appears with the
  MSIX assets; a `fix:`/unconventional PR bumps **patch**; a `!`/`BREAKING
  CHANGE` PR bumps **major**. The manifest version, tag, and release agree.
- **Slice 15:** `/code-review-in-detail` produces `summary-code-review.md` +
  `detailed-code-review.md`; confirmed real bugs are fixed (re-review or
  targeted tests pass) and referenced in the PR; the app still builds and runs.
- **Slice 16:** `STORE.md` lists the remaining Partner Center steps and the
  Azure Trusted Signing upgrade; `store-submit.yml` is valid but inert (no
  secrets). Pushing the **first auto-versioned tag** (a `0.x` version — not
  `v1.0.0`) produces the first Store-ready GitHub Release with both arch MSIX
  packages.
- **Slice 17:** launch the app, wait a moment, then immediately select text in a
  UIA app (or press the hotkey) for the **first** read of the session → speech
  starts promptly with no multi-second "is it broken?" stall (compare against the
  pre-warm build, where the first read lagged). Subsequent reads are unaffected.
  Confirm the UI is responsive during the startup warm-up (no freeze) and that a
  read fired *before* warm-up completes still works (falls through to lazy
  `EnsureTts()`), and that audio still plays under package identity.
- **Slice 18:** a quick click/double-click select reads with only a brief pause
  (no half-second wait); a slow drag-select of a sentence still reads **once**
  after the drag settles (no burst). A long multi-paragraph selection starts
  speaking after a short first synthesis (first sentence), not after the whole
  first paragraph. The clipboard-copy path behaves the same.
- **Slice 19:** trigger a read → the activity-log row shows a
  **time-to-first-audio** (and synthesis-duration) value; values are plausible and
  drop noticeably after Slice 17's warm-up vs a cold first read. Nothing leaves
  the device (no network call, no third-party SDK referenced). The optional dev
  OpenTelemetry/Aspire-dashboard path is documented but not part of the shipped
  package.
- **Slice 20:** `dotnet test` — sanitizer unit tests cover each category
  (URL→`page on host`, `password=…`→`a password`, email, digit runs, file path,
  GUID/hash, markdown link/`**`). Run the app: select a URL or a `token=…` string →
  it reads the clean summary, and the activity-log row text is the redacted form.
- **Slice 21:** trigger a few reads → `…\TemporaryFolder\logs\<date>-input.log` has
  **one row per state transition** (pending→generatingAudio→reading→read each a new
  line) with the id and **redacted** text; `<date>-system.log` has the matching
  id-keyed diagnostic lines (and any exceptions). The Activity-Log window's new
  top button opens the `logs` folder in Explorer. Restart with a >7-day-old file
  present → it's deleted. Confirm **no** raw secret appears in either file.
- **Slice 22:** read a multi-sentence paragraph → the system log shows per-chunk
  split/generate/wav/first-audio timings that sum toward the observed latency; after
  the thread/concurrency tuning, the logged synthesis time per chunk drops on a
  multi-core machine. context7 query for sherpa-onnx threading is logged before the
  knob change.
- **Slice 23:** start a long read, then change the voice actor mid-read → already-
  spoken audio is **not** repeated; the remaining text continues in the **new**
  voice. Changing voice while idle still applies to the next read.
- **Slice 24:** open the control panel, drag it by the header to a new spot → it
  moves; close and reopen → it reappears in the moved position. It still stays
  pinned-topmost and does not light-dismiss.
- Manual UI checks driven through the running app; no browser E2E harness
  applies to a native tray app.
