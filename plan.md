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
32. **Skip ±10s is chunk-boundary best-effort, not true seek (Batch 5).** Extends
    Decision 21 rather than replacing it. Skip forward/backward tracks each chunk's
    real synthesized duration (estimated only until that chunk finishes) and jumps
    playback to the nearest chunk boundary at/after the requested offset — accurate
    to within roughly one chunk, not exactly 10.000s. Rejected: sample-accurate seek
    over a fully buffered audio stream — it would require synthesizing the whole text
    up front, which defeats the streaming/first-audio-fast design (Decisions 15, 25)
    and the "starts reading as soon as the first part is ready" requirement for
    uploaded files.
33. **Timer format: `mm:ss`, `--:--` while duration is unknown (Batch 5).** Elapsed
    and total are shown as `elapsed/total`, colon-separated, minutes not zero-padded
    or capped at two digits (grows past 99 minutes, e.g. `125:33`, rather than
    wrapping). While the total is still being synthesized, the total side reads
    `--:--` (not a literal `99:99`) — the more common "unknown duration" convention
    in media players, and unambiguous since `99:99` isn't a real duration.
34. **File upload entry point + supersede semantics (Batch 5).** "Upload file" lives
    both as a button in the control panel and as a "Read file…" item in the
    right-click tray menu, opening the standard Windows `FileOpenPicker`. Picking a
    file immediately supersedes any in-progress read (same generation-counter
    supersede pattern already used across selection/hotkey/clipboard reads) rather
    than queuing multiple files. Drag-and-drop is deferred (see Out of Scope) —
    picker-only for v1.
35. **Document parsers: PdfPig + DocumentFormat.OpenXml, with a soft size cap
    (Batch 5).** PDF text extraction uses **PdfPig** (Apache-2.0); `.docx` uses
    **DocumentFormat.OpenXml** (MIT, official Microsoft SDK); `.txt` needs no
    library. Both fit the project's Apache/MIT-only stance (Decision 16; the
    Piper/Kokoro GPL rejection in `CLAUDE.md` is the same constraint). Exact current
    versions are confirmed via context7 before the NuGet refs are added (Decision 9).
    A soft page/size threshold triggers a warning rather than a silent hang on a huge
    document — text-layer PDFs only, no OCR.
36. **Slice order: timer-on-existing-reads before file upload (Batch 5).** The user's
    own framing led with file upload, but the timer is built and verified against the
    existing selection/hotkey/clipboard read paths first (Slice 25), since that's the
    smaller, already-working surface to validate duration tracking against. File
    upload (Slices 27-29) then reuses the same timer instead of standing up both at
    once.
37. **Cross-platform framework: .NET MAUI, reusing Domain + Application unchanged
    (Batch 6).** The app expands beyond Windows starting with **Android** (a Google
    Play developer account is already in hand; iOS/Mac follow later on the same stack
    once there's an Apple developer account — no timeline set). **.NET MAUI** is
    chosen over **Uno Platform** (also viable, also reuses Domain/Application, but a
    smaller ecosystem and no XAML-porting benefit worth the switch) and over
    **native Kotlin/Swift** (best per-platform fit, but shares zero code — the user
    explicitly asked to reuse "the libraries and code we used so far on Windows").
    `Domain` and `Application` (`PlaybackRate`, `SpeechTextChunker`, `ActivityLog`,
    `ReadAloudService`, the Slice 27 `IDocumentTextExtractor` family, etc.) already
    target plain `net10.0` with zero WinUI/WinRT/Win32 dependencies, so they are
    referenced by the new mobile project **as-is** — only new platform
    implementations are written. `ReadTheStupidText.Infrastructure` and
    `ReadTheStupidText.App` (WinUI) stay Windows-only and untouched. New project:
    `src/ReadTheStupidText.Mobile` (MAUI, `net10.0-android` first, `net10.0-ios` /
    `net10.0-maccatalyst` added later), using MAUI's `Platforms/Android` folder
    convention for platform-specific implementations rather than a separate
    class library. Confirm current .NET MAUI / Android target-framework versions via
    context7 before scaffolding (Decision 9's toolchain rule extends to the mobile
    stack).
38. **No auto-read triggers on Android — text entry, file upload, and camera OCR
    are the only input paths (Batch 6).** The Windows app has three auto-read
    triggers: UIA text-selection monitoring, a global hotkey, and a clipboard-copy
    listener (Decisions 3, 15 follow-up). **None of the three exist on Android**:
    there is no UI-Automation-equivalent cross-app text-selection API (the reason
    the user already ruled out selection support), Android has no concept of a
    system-wide keyboard hotkey for a background app, and background clipboard
    reads are blocked by the OS since Android 10 (only the focused app may read the
    clipboard) — so a clipboard listener modelled on `IClipboardMonitor` cannot
    work cross-app either. The mobile app is therefore **not** a background/tray
    utility like Windows; it is a normal foreground app with three deliberate input
    paths: **type or paste text** directly in-app (reusing `ReadAloudService`
    exactly as the hotkey path does today), **upload a file** (Slice 27's
    `IDocumentTextExtractor` pipeline, reused unchanged), and **take a photo**
    (Slice 32/40, new). `ISelectionMonitor`, the hotkey service, and
    `IClipboardMonitor` are Windows-only abstractions and are not implemented on
    mobile.
39. **Neural voice ported to Android: sherpa-onnx + Supertonic-3 primary, Android
    `TextToSpeech` as safety-net fallback only (Batch 6).** Mirrors Decision 14's
    Windows architecture exactly, for the same reason: consistent voice quality and
    identity across every platform beats relying on whatever OS voice happens to be
    installed. sherpa-onnx ships an Android (JNI) binding, so the same
    Supertonic-3 model (`sherpa-onnx-supertonic-3-tts-int8-2026-05-11`, Apache-2.0,
    ~145 MB) is bundled with the Android build exactly as it is with the Windows
    MSIX — no first-run download, no network, same offline/no-telemetry stance.
    Android's built-in `android.speech.tts.TextToSpeech` is wired only as the
    silent fallback if the packaged model fails to load, matching the WinRT
    fallback's role in `CompositeSpeechReader`. Confirm the sherpa-onnx Android
    binding's current API via context7 before coding. The ~145 MB asset's effect
    on install size is handled via Android App Bundle **Play Asset Delivery**
    (install-time asset pack) rather than baking it into the base APK, kept
    consistent with "ships in the package, no download" while avoiding a
    universal-APK size penalty on devices that don't need it (deferred to
    implementation which delivery mode fits best; confirmed via context7 first).
40. **Camera capture → on-device OCR (Google ML Kit) → existing read pipeline
    (Batch 6).** The core new mobile capability: point the camera at printed or
    handwritten text, capture a photo, extract its text, and read it aloud through
    the same `SpeechTextChunker`/`ReadAloudService` pipeline every other input path
    already uses — OCR is just a new **text source**, not new reading logic.
    **Google ML Kit Text Recognition** (on-device) is chosen over **Cloud Vision
    API** (higher accuracy on hard cases, but costs money per call, needs network,
    and breaks the "we collect nothing" local-only stance this project has held
    since Decision 26) and over **Tesseract** (also on-device and free, but
    generally lower out-of-the-box accuracy and heavier to tune). v1 is
    **single-shot capture** (take one photo → OCR → read) rather than a live
    real-time scanning overlay — simpler, and matches how people actually use a
    document scanner. New Application interface `IImageTextExtractor` (parallel to
    `IDocumentTextExtractor`) implemented by an ML Kit-backed
    `MlKitImageTextExtractor` in the mobile project's Android platform folder.
41. **Settings persistence and app identity on Android (Batch 6).** `ISettingsStore`
    gets a new Android implementation over MAUI's `Preferences` API (the mobile
    analogue of `ApplicationData.Current.LocalSettings`) — same interface, same
    keys where they apply (voice id, playback rate), with the Windows-only keys
    (`PanelPosition`, `AutoReadOnSelection`, `AutoReadOnCopy`) simply unused on
    mobile per Decision 38. Product display name stays **"Read The Stupid Text"**
    (Decision 23) on the Play Store listing; the Android application ID follows the
    existing internal-identifier convention (`uk.sirous.readthestupidtext` or
    equivalent — exact value confirmed against the Google Play Console listing
    during Slice 35, not guessed here).
42. **Android distribution: Google Play internal testing track, Play App Signing
    (Batch 6).** Mirrors the Windows Store re-signing trust model (Decision 18): CI
    builds and signs an Android App Bundle (`.aab`) with an **upload key**, and
    **Play App Signing** re-signs it with Google's managed release key for actual
    distribution — no long-lived release-signing secret lives in CI. A new GitHub
    Actions workflow (parallel to `build.yml`/`store-submit.yml`) builds the AAB and
    (once Play Console credentials exist) uploads it to the **internal testing**
    track first — the mobile equivalent of the Windows app's original
    GitHub-Release "testing/sideload" stage before the first real Store submission.
    Public production release on Play is a later, explicit step, not automatic on
    every merge (unlike Windows' Decision 17 auto-versioned release — the same
    GitVersion-driven SemVer is reused for the version *number*, but publishing to
    production track stays manual until the mobile app is deliberately declared
    ready, matching how the Windows Store submission itself was manual).
43. **Android UI design: three concrete screens, specified in the same design
    project as the Windows panel (Batch 6, not yet implemented).**
    `design_handoff_tray_panel/Option C - Media Card.dc.html` and its
    `README.md` were re-imported via the Claude Design MCP (`/design-login`) and
    now specify the Android UI directly — not just a conceptual adaptation of the
    Windows "Media Card" panel (superseding the earlier draft of this decision,
    which guessed at the mapping before the design existed). Both the local
    `design_handoff_tray_panel/` bundle and this decision were resynced from the
    live design project (canvas `90615641-f298-4e21-8c50-ca2efbeaaebc`) to match.
    Brand identity (gradient `linear-gradient(135deg,#5B57E8,#3B82F6)`, glyph
    watermark, the bundled Supertonic voice set, the `PlaybackRate` model) stays
    consistent across platforms, but Android is a **genuinely different layout**
    from the Windows flyout, not a resize of it — full detail in
    `design_handoff_tray_panel/README.md` under "Android app (Batch 6 · MAUI)";
    summary:
    - **Screen 1 — Type or paste (home, Slice 30/31).** Brand-gradient app bar
      (title + `Momonga · 1.25×` sub-line + a mic button that opens Screen 3) over
      a full-height editor card (the paste/type target) and a transport card:
      skip-back-10s / 52px gradient play-pause / skip-forward-10s, a progress bar
      with the `00:03 / --:--` elapsed/total timer beneath it, and a 6-up row of
      speed-preset pills (**no hidden slider on mobile** — a 0.05-step slider is a
      poor touch target, so the presets *are* the speed control, unlike Windows).
      A white bottom nav with three tabs — **Type · Camera · File** — is the app's
      only navigation; this **replaces** the Windows "Controls" icon-toggle row
      entirely (there is no Android equivalent of a settings-row within the
      screen — the three tabs *are* the three input paths from Decision 38).
    - **Screen 2 — Camera → OCR (Slice 32).** Full-bleed dark viewfinder with a
      detection frame + `TEXT FOUND` chip (ML Kit sees text) and a hint pill; a
      capture bar (gallery / 68px shutter / flash); a result bar with a **Read**
      button that feeds the extracted text into the same chunked pipeline as
      typed text. Single-shot only — no live-scan overlay (Decision 40).
    - **Screen 3 — Voice picker (Slice 31).** Brand app bar with a back chevron;
      two grouped cards (`MALE`/`FEMALE`) listing the ten Overlord voices with a
      preview-play affordance per row; the selected voice is checked and tinted.
      Footer note matches Slice 23's Windows behavior: a change applies at the
      next chunk, not instantly.
    - **File tab (Slice 33)** is a nav destination, not yet given its own
      "Screen N" mock — reuse the same card/list visual language as the other
      screens when it's built.
    **Confirmed still shared with Windows** (so Slice 30's transport card is not
    a cut-down v1 — it includes skip ±10s and the timer from the start, once the
    underlying Batch 5 Windows work — Decisions 32/33 — lands, since that logic
    lives in the already-reused `Domain`/`Application`): the ten Supertonic
    voices, the 0.5–2.0× `PlaybackRate` model and its six presets, and chunked
    synthesis with ±10s skip and the `mm:ss`/`--:--` timer. **Confirmed absent on
    Android, including for this first release** (Decision 38, now made explicit
    in the design doc too): the two auto-read toggles, launch-at-startup, the
    global hotkey, the tray icon, always-on-top/drag behavior, and — new —
    **any activity-log or on-disk-diagnostics screen** (deferred; already
    reflected in this plan's Out of Scope). Mobile-specific tokens (page bg
    `#f4f5f9`, white 14px-radius cards, `#0b0d12` camera chrome, platform font
    instead of Segoe UI Variable) are in the README's "Mobile tokens" table.
    Exact native-control mapping (MAUI XAML controls, precise layout) is still
    decided when each slice is actually implemented — this decision fixes the
    design source and the concrete screen contract so that work doesn't start
    from a blank page.
44. **Store submission is now automatic on every release, not manual
    (`store-submit.yml` bug fix).** Root cause: the workflow was
    `workflow_dispatch`-only and simply never got re-triggered — it had run
    exactly 3 times total (all on 2026-08-16, submitting `v0.7.7`), while
    `main` went on to ship `v0.7.8` … `v0.14.0` (15 days, 7 releases) with
    nothing reaching the Store. Fix: `build.yml`'s `release` job now has a
    sibling `store-submit` job that calls `store-submit.yml` as a **reusable
    workflow** (`workflow_call`) immediately after a new release is cut, only
    when `release`'s own `released` output says a release actually happened
    (guards the idempotent-skip path where the version didn't bump). This
    could **not** be done as an `on: release: types: [published]` trigger on
    `store-submit.yml` itself — `build.yml` creates the release with the
    default `GITHUB_TOKEN`, and GitHub's recursion guard means a
    `GITHUB_TOKEN`-authored event never starts another workflow run, so that
    trigger would parse fine and simply never fire (a real gotcha, verified
    against current GitHub Actions docs, not assumed). `workflow_dispatch`
    stays on `store-submit.yml` for manual re-submits. Also confirmed (from
    Microsoft's own CLI docs): `msstore publish` deletes any still-pending
    draft submission and creates a fresh one from the latest package — exactly
    what automatic-on-every-release wants (newest always wins), but it means
    two releases can never both be "in flight" to the Store at once. See
    `STORE.md` → *Deploying to the Store*.
45. **Reading text box is a separate floating window, not an expansion of the
    control panel (Batch 7).** The panel (Decision 12/20) stays its current
    fixed compact size. A new toggleable window shows the text currently being
    read; it opens/closes via a **fourth** square icon `ToggleButton` added to
    the panel's existing `CONTROLS` row, matching the visual pattern of the
    other three toggles (auto-read-on-selection, auto-read-on-copy,
    launch-at-startup). Rejected: expanding the panel downward (would make the
    already-compact Media Card unwieldy) and a docked pop-out glued to the
    panel (adds positioning complexity for no benefit over a plain independent
    window, since the panel itself is already freely draggable per Decision 31).
46. **Highlighting is whole-chunk only, not sentence/word-level (Batch 7).**
    The text box highlights the entire `SpeechTextChunker` chunk currently
    playing — no interpolation to a sub-chunk sentence or word position.
    Simpler, needs no new timing precision, and is what was actually asked
    for (an earlier sentence-level-interpolation idea was rejected as
    unneeded complexity).
47. **Text-box pagination is independent of synthesis chunk boundaries, and
    auto-follows playback (Batch 7).** A new greedy, sentence-boundary-respecting
    fill algorithm sizes each "page" to whatever currently fits the box (its own
    segmentation, separate from `SpeechTextChunker`, which is sized for
    synthesis latency, not display). The box auto-advances to the page
    containing the currently-playing chunk; v1 has no manual next/prev page
    controls — it is a read-along display, not a separate navigable reader.
48. **Zoom lives in the text-box window itself, with a dynamic minimum instead
    of a fixed pixel floor (Batch 7).** +/- zoom controls sit in the text box's
    own header (not on the main control panel). The zoom-in limit is enforced
    live against the box's current width: zooming stops the moment a 30-word
    sentence would no longer fit, rather than a fixed minimum font size — this
    tracks window resizing automatically. A generous fixed pixel ceiling (~32px)
    caps zoom-out-of-control on the large end, since no equivalent constraint
    was specified for that direction.
49. **Audio chunks are always written to a temp WAV file per chunk, for every
    read (Batch 7).** Supersedes the "no audio caching" exclusion under Batch 4
    (which only scoped Slice 22's latency work, not a permanent exclusion) — the
    motivation here is memory footprint on large files, not latency. Every
    read's chunks land at
    `…\TemporaryFolder\audio\<activity-id>\chunk-<index>.wav` (a sibling of the
    existing `logs\` folder, Decision 27) and `MediaPlayer` plays from the file
    instead of an in-memory buffer. One code path for every read (not a
    size/duration-gated dual path) — simpler, and the per-chunk disk round-trip
    is not expected to be perceptible. Cleanup: a read's chunk files are deleted
    when it reaches a terminal state (read/interrupted/failed) or is superseded
    (the existing generation-counter teardown already used for synthesis
    cancellation), plus a startup sweep removes any orphaned `audio\<id>\`
    folder older than 1 day (mirrors the 7-day `logs\` sweep, Decision 27, at
    shorter retention since these are pure transient playback buffers, never a
    diagnostic record).
50. **One shared chunk map, extended rather than duplicated (Batch 7).** The
    existing chunk index used by Skip (`SkipTarget`, Decision 32) and the
    read-through timer (`ReadTimingTracker`, Decision 33) gains two more fields
    per chunk — its on-disk WAV path (Decision 49) and its source-text character
    range (for the text box, Decision 46) — instead of introducing a second,
    parallel index that could drift out of sync with the first.
51. **The stuck elapsed/total timer is root-caused during implementation, not
    pre-diagnosed here (Batch 7).** No fix is assumed in this plan; use the
    systematic-debugging skill when the slice is picked up.

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
- [x] **Store launch — live + automated-update wiring.** App **published and
      live** at https://apps.microsoft.com/detail/9NGT1BN1H92V (product
      `9NGT1BN1H92V`). `store-submit.yml` made functional (downloads a release,
      combines x64 + ARM64 into one `.msixbundle`, submits via the msstore CLI);
      `STORE_PRODUCT_ID` repo variable set. First submission failed Store
      cert **10.2.4.1** (undisclosed dependency on .NET) — fixed by building the
      Release MSIX **.NET self-contained** (`SelfContained=true`), bundling the
      .NET runtime so there's no external dependency. Windows App SDK stays
      framework-dependent (Store-delivered).
      **Automation now complete and proven** (v0.7.7, run `31974833565`): the
      individual Partner Center account had no Entra tenant, so one was created
      free from Partner Center, an Entra app registered with the **Manager** role,
      and all four secrets set. Three real failures were found and fixed on the way
      — `makeappx bundle` needs **`/bv`** (else the bundle version comes from the
      current date-time); `msstore publish -ut` **does not exist** in the CLI
      version the action installs (v0.3.9) despite being documented; and
      **`SELLER_ID` must be the numeric Seller ID** from *Organization profile →
      Legal info*, not the GUID Publisher ID on the Identifiers page. Updates now
      ship with `gh workflow run store-submit -f tag=v<x.y.z>`. One transition
      artifact, handled manually once: the CLI supersedes packages by **file
      extension**, so the first `.msixbundle` did not replace the old `.msix`
      packages — see `STORE.md`.

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
- [x] **Slice 24 — Draggable, position-persisted control panel.** ([#106](https://github.com/ashkansirous/ReadTheStupidText/issues/106)) (Decision 31) Make
      the borderless control panel draggable by its header (pointer-drag → `AppWindow`
      move) and persist the last position in `ISettingsStore` so it reopens in place;
      keep pinned-topmost / no light-dismiss.
      **Built:** pointer handlers on `HeaderBorder` move the `AppWindow` by the raw
      screen-cursor delta (`GetCursorPos`, not element-relative coords — those jitter as
      the window moves); on capture-lost the final position is saved. New
      `PanelPosition(X, Y)` record + `ISettingsStore.PanelPosition` (device pixels, two
      `PanelX`/`PanelY` keys in `LocalSettingsStore`); `ISettingsStore` threaded into the
      panel via `MainWindow`. `PositionPanel` restores the saved spot **clamped to the
      work area** (`ClampToWorkArea` + `DisplayArea.GetFromPoint`, so an offscreen/moved-
      monitor point is pulled back on-screen), falling back to the default bottom-right
      pin only when never moved. Child controls mark their pointer input handled, so a
      drag only starts on the header's empty areas; pinned-topmost / no-light-dismiss
      kept. UI/native code isn't unit-tested per the project's test story; runtime check
      under the (Package) profile remains.

- [x] **Slice 25 — Read-through timer (elapsed/total).** (Decision 33, Batch 5) Show
      `elapsed/total` in the transport row. Track each chunk's real synthesized
      duration as it completes; once every chunk for the current read has finished,
      sum them into the total. Before that, the total is unknown.
      New Application record (e.g. `ReadTiming(TimeSpan Elapsed, TimeSpan? Total)`)
      exposed via a new `ISpeechReader.TimingChanged` event (parallel to the existing
      `ProgressChanged`), raised roughly once per second while reading and whenever
      `Total` changes. A pure formatter renders `mm:ss` / `--:--` (Decision 33). Wire
      into `ControlPanelWindow`'s transport row next to the existing progress bar, the
      same way progress is wired today. Unit-test the formatter and the
      duration-accumulation sequencing against the user's own worked example
      (`00:00/--:--` → ticks → `00:03/02:23` the instant the last chunk lands).
      **Built:** `ReadTiming(TimeSpan Elapsed, TimeSpan? Total)` +
      `ReadTimingFormatter` (mm:ss/mm:ss, `--:--` for unknown) live in
      `Application.Reading`, unit-tested directly. The duration-accumulation logic
      is a new pure `Infrastructure.Reading.ReadTimingTracker` (`Start`/
      `RecordChunkDuration`/`AdvancePastChunk`/`CurrentTiming`/`Reset`) shared by both
      `ISpeechReader` implementations, so it's unit-testable without the WinRT engine
      — `ReadTimingTrackerTests` reproduces the worked example exactly. In
      `SupertonicSpeechReader`, each chunk's real duration is computed from its PCM
      sample count/rate the instant `Generate` returns (not when it plays), recorded
      into the tracker, and — the instant that completes the total — `TimingChanged`
      fires immediately, bypassing the ~1s throttle used for ordinary ticks
      (`OnPositionChanged`); `AdvancePastChunk` folds a finished chunk's duration into
      the elapsed baseline in the ordered consume loop. The WinRT fallback
      (`SpeechReader`) has one "chunk"; its total is learned from `NaturalDuration`
      the first tick it's populated (mirroring the existing progress-bar code), also
      firing immediately that tick. `CompositeSpeechReader` forwards `TimingChanged`
      from whichever engine is active, same as `ProgressChanged`.
      `ReadAloudService.TimingChanged` passes it through; `ControlPanelWindow` adds a
      `TimerText` label under the transport row, updated via `ReadTimingFormatter` and
      reset to `00:00/--:--` on the same Idle transition that zeroes the progress bar.
      `SpeechSynthesisStream.Duration` was considered for the fallback but dropped —
      context7 (`/websites/learn_microsoft_en-us_uwp_api`) couldn't confirm the
      property exists, so `NaturalDuration` (already relied on elsewhere in this file)
      was used instead. UI/native code isn't unit-tested per the project's test story;
      runtime check under the (Package) profile remains.
- [x] **Slice 26 — Skip forward / backward (±10s).** (Decision 32, Batch 5)
      `ISpeechReader.SkipForward()` / `SkipBackward()`: using the per-chunk duration
      index from Slice 25, compute the chunk whose cumulative start time is nearest
      at/after `elapsed ± 10s`, tear down current playback via the existing
      generation-counter mechanism, and resume `MediaPlayer` at that chunk (waiting on
      it if still synthesizing). Clamp backward at 0; clamp forward at the furthest
      point reached so far (can't skip into audio not yet synthesized). Two new icon
      buttons flank the play/pause circle in the transport row, wired to
      `ReadAloudService.SkipForward/SkipBackward`. Unit-test the pure
      target-chunk-selection and clamping logic.
      **Built:** a new pure `SkipTarget(int ChunkIndex, TimeSpan ChunkStart)` record
      (`Application.Reading`) and `ReadTimingTracker.ComputeSkipTarget(elapsed, delta)`
      (Infrastructure, unit-tested — `ReadTimingTrackerTests`) do the target-chunk
      selection: forward rounds up to the nearest known chunk start at/after the
      target, clamped to the furthest reachable boundary (the start of the next chunk
      is knowable, and selectable, even before that chunk itself has finished
      synthesizing); backward rounds down to the nearest known chunk start at/before
      the target, clamped to zero. A new `ReadTimingTracker.SeekTo(SkipTarget)`
      repositions the elapsed baseline without touching already-recorded chunk
      durations, so replaying a chunk after a skip folds its duration back in exactly
      like a first playthrough. `SupertonicSpeechReader.SkipForward/SkipBackward` keep
      the read's chunk list (`_chunks`, set in `SpeakAsync`) so a skip can restart the
      ordered playback loop (`SpeakChunksAsync`) at the target chunk — reusing the same
      generation-counter teardown as `Stop()`/the Slice 23 voice-swap. The WinRT
      fallback (`SpeechReader`) is a single fully-synthesized stream, so instead of
      chunk-snapping it seeks `MediaPlaybackSession.Position` directly (exact, not
      best-effort — confirmed via context7 against
      `/websites/learn_microsoft_en-us_windows_apps`), clamped to
      `[0, NaturalDuration]`. `CompositeSpeechReader` and `ReadAloudService.
      SkipForwardAsync/SkipBackwardAsync` just forward to the active engine. UI: two
      28px ghost icon buttons (`SkipBackButton`/`SkipForwardButton`, Segoe Fluent
      Icons `Rewind`/`FastForward` glyphs `EB9E`/`EB9D` — the font's only true ±10s
      pair is `SkipBack10`/`SkipForward30`, mismatched at 10s/30s, so the plain
      direction glyphs were used instead) flank the play/pause circle in the transport
      row. Both engines gate the skip on `State != Idle` rather than on `_chunkCount`/
      `NaturalDuration` (a code-review catch): neither reader clears those on natural
      completion — only `Stop()` does — so gating on them let a skip after a read
      finished silently resurrect/replay it; `State` is the field completion always
      updates first, on both engines. UI/native code isn't unit-tested per the
      project's test story; runtime check under the (Package) profile remains.
- [x] **Slice 27 — File upload: plain text (.txt).** (Decision 34/35, Batch 5) New
      Application interface `IDocumentTextExtractor { bool CanHandle(string
      extension); Task<string> ExtractTextAsync(string filePath); }`; Infrastructure
      `PlainTextExtractor` plus a `CompositeDocumentTextExtractor` that routes by
      extension, mirroring `CompositeSpeechReader`'s composition pattern. New
      `ActivityTrigger.FileUpload` enum value (Domain) — the Activity Log **Source**
      column shows the uploaded file's name for these entries. UI: an "Upload file"
      button in the control panel and a "Read file…" item in the tray right-click
      menu, both opening a `FileOpenPicker` filtered to `.txt` (filter widens in
      Slices 28-29); picking a file calls a new `ReadAloudService.ReadFileAsync(path)`
      that extracts text and feeds the existing read pipeline, superseding any
      in-progress read (Decision 34). Unit-test the composite extractor's routing and
      the plain-text extractor.
- [x] **Slice 28 — File upload: PDF.** (Decision 35, Batch 5) Added **PdfPig**
      0.1.16 (Apache-2.0, version confirmed via context7/NuGet, Decision 9) to
      Infrastructure; `PdfTextExtractor` joins each page's `page.Text` in order.
      Widened the `FileOpenPicker` filter to include `.pdf`. Threshold: a soft cap
      of **200 pages** — above it `PdfTextExtractor` throws a new
      `DocumentTooLargeException` (Application/Documents, carrying `Actual`/`Limit`)
      before extracting any text, rather than synthesizing a huge document;
      `ReadAloudService.ReadFileAsync` catches it ahead of the generic extraction
      failure and logs it at `Warning` (vs. `Error` for a genuinely corrupt file) —
      text-layer PDFs only, no OCR (a scanned/image-only PDF just yields empty
      pages). *Layering fix:* `CompositeDocumentTextExtractor` **and**
      `PlainTextExtractor` moved from Application to **Infrastructure/Documents**
      (mirroring `CompositeSpeechReader`/`SpeechReader`/`SupertonicSpeechReader`'s
      placement, and matching what Slice 27's own plan entry already said —
      Application landed them there by mistake) — Application must not reference
      Infrastructure, and `PdfTextExtractor` (depends on the PdfPig package) can
      only be composed by a class allowed to see both. Unit-tested
      (`PdfTextExtractorTests`) against a small
      hand-built fixture PDF (PdfPig is read-only, so the fixture is generated with
      a minimal hand-rolled PDF writer in the test file) covering routing, page
      order, and the 200-page cap.
- [x] **Slice 29 — File upload: DOCX.** (Decision 35, Batch 5) Added
      **DocumentFormat.OpenXml** 3.5.1 (MIT, version confirmed via context7/NuGet,
      Decision 9) to Infrastructure; `DocxTextExtractor` walks the document body's
      paragraphs (`Descendants<Paragraph>().InnerText`) into plain text. Widened the
      `FileOpenPicker` filter to include `.docx`. Soft cap mirrors Slice 28's intent
      but reads the *cached* page count DOCX actually carries — Word's `app.xml`
      `ExtendedFilePropertiesPart.Properties.Pages` (a `.docx` has no fixed
      pagination of its own, unlike a PDF) — reusing `DocumentTooLargeException`
      when it's present and over 200; a document with no cached page count (never
      opened/saved in Word) skips the cap rather than being rejected on a number we
      don't have. Unit-tested (`DocxTextExtractorTests`) against fixture `.docx`
      files built with the Open XML SDK itself (unlike PdfPig, it can author as
      well as read), covering routing, paragraph order, the over-cap throw, and the
      no-cached-count skip.

**Batch 6 — Android app (MAUI): type-to-read, camera OCR, file upload, neural
voice.** Expands the app beyond Windows, starting with Android (Decisions
37-42). Domain and Application are reused unchanged; only new platform
implementations are written. Ordered smallest-first, each independently
runnable/installable — the same vertical-slice principle as every earlier
batch. iOS/Mac are deliberately not slices here (Decision 37) — they're future
batches on the same MAUI codebase once there's an Apple developer account.

- [x] **Slice 30 — MAUI Android scaffold + type-to-read (smallest E2E).**
      (Decision 37, 38, 43 — Screen 1) Scaffold `src/ReadTheStupidText.Mobile`
      (MAUI, `net10.0-android`), referencing the existing
      `ReadTheStupidText.Domain` and `ReadTheStupidText.Application` projects
      unchanged. Builds Screen 1 ("Type or paste"): brand app-bar, editor card,
      and a transport card (play/pause, skip ±10s, progress bar + elapsed/total
      timer, six speed presets — no hidden slider on mobile), plus the bottom nav
      shell (Type · Camera · File tabs, Camera/File as stubs until Slices 32/33).
      TTS engine for this slice is Android's built-in `TextToSpeech` (the neural
      voice is ported in Slice 31) via an `ISpeechReader` implementation in
      `Platforms/Android`. Add the project to `ReadTheStupidText.slnx`. This
      proves the whole DI/Domain/Application reuse story end-to-end before any
      mobile-specific complexity (OCR, file parsing, neural voice) is added — a
      plain/native-default visual treatment is an acceptable stand-in for this
      first pass if that's faster to prove the wiring; the branded Screen 1 look
      (Decision 43) lands whenever this slice's UI is actually built.
      **Built:** Android-only for now (`TargetFrameworks` trimmed to
      `net10.0-android`; iOS/MacCatalyst/Windows heads removed from the
      template scaffold — Decision 37). `ApplicationId` `uk.sirous.readthestupidtext`,
      display name "Read The Stupid Text" per the naming convention. No
      network permissions in the manifest (on-device TTS only, matching the
      "we collect nothing" stance). Rather than reusing `ReadAloudService`
      wholesale (it's wired for Windows-only triggers — hotkey, UIA selection,
      clipboard — none of which exist on Android per Decision 38, and the
      plan text itself only asked to reuse `PlaybackRate`/`SpeedPresets`),
      `TypePage` wires `ISpeechReader` directly as its own thin use case — the
      simpler, correctly-scoped design once `ReadAloudService`'s dependency
      list was inspected. `AndroidSpeechReader` (`Platforms/Android`, in
      namespace `ReadTheStupidText.Mobile` — deliberately *not*
      `...Platforms.Android`, since a namespace segment literally named
      `Android` shadows the top-level `Android.*` binding namespace for
      unqualified lookups; the same reason `App : Application` had to be
      qualified as `Microsoft.Maui.Controls.Application`, since the project
      also references a library literally named `ReadTheStupidText.Application`)
      wraps `Android.Speech.Tts.TextToSpeech`. That engine has no native
      pause/resume/seek, so `Pause`/`Resume`/`SkipForward`/`SkipBackward` all
      stop and re-`Speak()` from a character offset tracked via
      `UtteranceProgressListener.OnRangeStart` (word-boundary granularity),
      snapped to the nearest space — best-effort, matching the "not
      sample-accurate" contract the Windows chunked engines already promise,
      just via a different mechanism (word boundaries instead of chunk
      boundaries). `ReadTimingFormatter` (Application, Slice 25) is reused
      as-is for the `mm:ss/--:--` timer label. `AppShell` is a `TabBar` with
      `Type`/`Camera`/`File` tabs (`FlyoutBehavior="Disabled"` — bottom nav is
      the only navigation, Decision 43); `CameraPage`/`FilePage` are stub
      pages pointing at Slices 32/33. `MauiProgram` registers `ISpeechReader`
      behind an `#if ANDROID` factory and `TypePage` for constructor
      injection through Shell's `ContentTemplate`. Colors.xaml gained the
      brand tokens (`BrandStart`/`BrandEnd`/`PageBackground`/`CardBorder`/
      `TextSecondaryLight`) and `Primary` now matches the brand purple, so
      default control theming (buttons, progress bars, the tab bar) tracks
      the brand color for free. Verified: `dotnet build` (both the Mobile
      project alone and the full `.slnx`) and the existing 102-test suite all
      pass. **Not yet verified: running on a device/emulator** — none was
      available in the dev environment this slice was built in (no AVD
      configured, no physical device attached); do that manually before
      relying on this slice.
- [x] **Slice 31 — Neural voice on Android (sherpa-onnx + Supertonic-3).**
      (Decision 39, 43 — Screen 3) Bundle the same Supertonic-3 model used on
      Windows via Play Asset Delivery; port `SupertonicSpeechReader`'s synthesis
      logic to the sherpa-onnx Android binding; `CompositeSpeechReader`-style
      routing with Android `TextToSpeech` as the safety-net fallback only. Builds
      Screen 3 (voice picker): grouped `MALE`/`FEMALE` cards listing the same ten
      Overlord-named voices (Decision 19, `SupertonicVoiceTable` reused unchanged
      from Domain) with a preview-play affordance; a change applies at the next
      chunk, matching Slice 23's Windows behavior. Settings persistence (voice
      id, playback rate) via a MAUI `Preferences`-backed `ISettingsStore`
      implementation (Decision 41).
      **Built:** *Layering fix (same pattern as Slice 28's), enabling this whole
      slice:* `SupertonicVoiceTable`, `SupertonicFiles`, `ReadTimingTracker`, and
      `SpeechTextChunker` were misplaced in Infrastructure (net10.0-windows-only,
      unreachable from Mobile) despite having zero framework dependencies —
      Domain and Application weren't actually "unchanged," they were made
      correct so the reuse the plan promises is real. `SupertonicVoiceTable`
      (now `public`, plus a new `IsFemale(voiceId)` for the MALE/FEMALE
      grouping, unit-tested) and `SupertonicFiles` moved to
      `Domain.Reading`; `ReadTimingTracker` and `SpeechTextChunker` moved to
      `Application.Reading` (both still `internal` — `SpeechTextChunker` needed
      a new `InternalsVisibleTo` for `ReadTheStupidText.Mobile` alongside the
      existing `Tests`/now-added-back `Infrastructure` ones). All four now have
      exactly one implementation shared by both platforms instead of a second
      copy waiting to drift.
      *Native packaging (the real blocker):* `org.k2fsa.sherpa.onnx`'s nuspec
      unconditionally depends on all 8 platform `runtime.*` sub-packages; a
      plain `PackageReference` on Android silently bundled the **linux-x64**
      `libonnxruntime.so`/`libsherpa-onnx-c-api.so` into `lib/arm64-v8a/`
      instead of the real Android ones — same failure as
      [microsoft/onnxruntime#29270](https://github.com/microsoft/onnxruntime/issues/29270),
      confirmed by inspecting the built APK, not just trusting the build log.
      Fix: `ExcludeAssets="all"` on the seven non-Android `runtime.*` packages
      (kept as pinned-but-inert references) plus `GeneratePathProperty="true"`
      on `org.k2fsa.sherpa.onnx.runtime.android-arm64`, then two explicit
      `AndroidNativeLibrary` items (`Abi="arm64-v8a"`) pointing at its
      `$(Pkgorg_k2fsa_sherpa_onnx_runtime_android-arm64)\runtimes\...\native\*.so`
      — confirmed by unzipping the built APK a second time. No android-x64
      native package exists upstream, so the neural engine is unreachable on a
      plain x86_64 emulator; `CompositeSpeechReader`-style fallback to
      `AndroidSpeechReader` (Slice 30) covers that by design, the same as a
      missing/broken model on Windows. `SupportedOSPlatformVersion` bumped
      21→23 for `MediaPlayer.PlaybackParams` (pitch-preserving speed change) —
      negligible reach cost.
      *`AndroidSupertonicSpeechReader`* (`Platforms/Android`) ports the Windows
      reader's chunking/ordered-playback/generation-counter/mid-read-voice-swap
      design essentially unchanged (it's the same pure logic, now shared via
      the layering fix above); only the playback primitive differs —
      `Android.Media.MediaPlayer` has no in-memory-stream play path, so each
      synthesized chunk is written to a temp WAV file
      (`FileSystem.Current.CacheDirectory`) and played via `SetDataSource`,
      and there's no position-changed event, so progress/timing are driven by
      a 200ms poll loop instead of a push event. `MobileVoiceModelService`
      extracts the model (packaged as a `MauiAsset`, referencing the *same*
      committed `App/VoiceModel/*` files — no second 139 MB copy in the repo)
      from the package to app-local storage once on first launch, since
      sherpa-onnx's native loader needs real file paths, not an
      asset-package stream; this is a **plain-bundle stand-in for the real
      Play Asset Delivery** — untestable here without a live Play Console
      listing to deliver from (Slice 34 revisits it once one exists), fine for
      local sideload testing meanwhile. `VoicePickerPage` (Screen 3, pushed via
      a registered Shell route from `TypePage`'s mic button) builds its
      MALE/FEMALE rows in code rather than a `CollectionView` (10 static
      items); tapping a row selects **and** previews it by speaking its own
      display name — hearing the voice *is* the preview, so there's no
      separate preview affordance to wire up (a disclosed simplification of
      the design mock's separate per-row play icon). `App.xaml.cs` now takes
      `ISpeechReader`/`ISettingsStore`/`IVoiceModelService` via constructor
      injection (`UseMauiApp<App>` resolves `App` from the DI container) and
      applies the persisted speed/voice immediately, then awaits model
      location + engine warm-up. Verified: `dotnet build` (Mobile alone and
      the full `.slnx`), the 108-test suite (6 new `IsFemale` cases), and
      unzipping the built debug APK to confirm both the native libraries and
      `assets/VoiceModel/*` land where expected. **Not yet verified: running
      on a device/emulator** — same environment gap as Slice 30.
- [x] **Slice 32 — Camera capture → OCR → read.** (Decision 40, 43 — Screen 2)
      Add the camera screen (MAUI `MediaPicker`/`CameraView`-equivalent, confirm
      current API via context7): dark viewfinder with a detection frame + "TEXT
      FOUND" chip, a capture bar (gallery / shutter / flash), and a result bar
      with a **Read** button. `MlKitImageTextExtractor` implements the new
      `IImageTextExtractor` (Application); a captured photo's extracted text
      flows through the existing `SpeechTextChunker`/`ReadAloudService` pipeline
      exactly like typed text. Single-shot only, per Decision 40. This is the
      headline new capability the user asked for: "take a picture of the stuff,
      then read it for them."
      **Built:** Confirmed via context7 that MAUI has no embedded live-preview
      camera control (`CameraView` is a Community Toolkit/third-party thing, not
      MAUI itself) — `MediaPicker.Default.CapturePhotoAsync()` launches the
      **system** camera app instead, so Screen 2 is a plain-native-default
      capture flow (idle "take photo" card → processing spinner → extracted-text
      result card with Retake/Read) rather than the design mock's in-app dark
      viewfinder/detection-frame/shutter-bar chrome, same disclosed-simplification
      allowance Slice 30 used for Screen 1's first pass. `IImageTextExtractor`
      (Application/Images, mirroring `IDocumentTextExtractor`'s shape but with
      no `CanHandle` — a capture is always an image, nothing to route) is
      implemented by `MlKitImageTextExtractor` over **Google ML Kit's on-device
      Latin text recognizer** (`Xamarin.Google.MLKit.TextRecognition`), per
      Decision 40. Unlike sherpa-onnx's packaging bug, ML Kit's native `.so` and
      model assets are correctly per-ABI out of the box — confirmed the same
      way, by unzipping the built APK: `lib/arm64-v8a/` +
      `lib/x86_64/libmlkit_google_ocr_pipeline.so` (~11 MB each, both real,
      no wrong-OS collision) and the recognizer's `.tflite`/`.binarypb` model
      files under `assets/mlkit-google-ocr-models/` — meaning recognition is
      **fully on-device from first launch, zero network**, stronger than the
      Play-services-download story originally assumed for Decision 40's "we
      collect nothing" framing. Awaiting the Java `Task<Text>` from
      `ITextRecognizer.Process()` needed the `Android.Gms.Extensions` package
      (`Xamarin.GooglePlayServices.Tasks`) for its `GetAwaiter()`/`AsAsync<T>()`
      extensions — not obvious from the compiler's first error (a bare
      "namespace not found"); found by loading the referenced DLL into a scratch
      console project and enumerating its public strings for the real namespace,
      the same "trust the built artifact, not the guess" instinct that caught
      the sherpa-onnx bug. `CameraPage` reads through the exact same
      `ISpeechReader` **singleton** `TypePage` uses (registered once in
      `MauiProgram`) — tapping **Read** calls `SpeakAsync` on that shared
      instance directly, so it is genuinely "the same pipeline," not a
      look-alike second one; switching to the Type tab mid-read shows the same
      in-progress read on its transport controls, for free. `CAMERA` permission
      + a `<queries>` package-visibility entry for `IMAGE_CAPTURE` added to
      `AndroidManifest.xml`; `Permissions.Camera` itself is requested
      internally by `MediaPicker`, not requested by app code. Verified:
      `dotnet build` (Mobile alone and the full `.slnx`, 0 errors), the
      108-test suite (no regressions — this slice added no new pure logic to
      test), and unzipping the built debug APK to confirm the OCR native
      libraries/model assets as above. **Not yet verified: running on a
      device/emulator** (camera capture doubly so, since most emulators have
      no usable camera) — same environment gap as Slices 30-31.
- [x] **Slice 33 — File upload: .txt/.pdf/.docx (reused).** (Decisions 34, 35,
      41, 43) Wire a MAUI file/document picker — reached from the bottom nav's
      **File** tab (Decision 43; no dedicated screen mock yet, reuse the other
      screens' card/list visual language) — to the *existing*
      `CompositeDocumentTextExtractor`/`PlainTextExtractor`/`PdfTextExtractor`/
      `DocxTextExtractor` from Batch 5 (Application interface, Infrastructure
      implementations — referenced from the mobile project's platform folder or
      promoted to a portable location if any Windows-only dependency is found
      during implementation). No new extraction logic; only new platform picker
      UI.
      **Built:** No Windows-only dependency was found — as the plan text
      anticipated, all four extractors are plain C# over PdfPig/
      DocumentFormat.OpenXml (both portable managed libraries), stuck in the
      `net10.0-windows` Infrastructure project only because that's where they
      happened to land in Batch 5. Rather than duplicating them into Mobile's
      own platform folder (which would drift from the Windows copy over time —
      the exact failure the Slice 28/31 layering fixes exist to prevent), they
      were **promoted to a new portable library**, `ReadTheStupidText.Documents`
      (`net10.0`, referencing only `Application` + PdfPig/OpenXml), added to
      the `.slnx`. Infrastructure now references it instead of containing the
      files directly (its own `PdfPig`/`DocumentFormat.OpenXml`
      `PackageReference`s moved with them); Mobile references it too — both
      platforms now share the literal same `CompositeDocumentTextExtractor`
      instance-shape, not a look-alike. `FilePage` follows the same DI pattern
      Windows' `App.xaml.cs` already used (register the three concrete
      extractors + `IDocumentTextExtractor → CompositeDocumentTextExtractor`),
      and reads through the same shared `ISpeechReader` singleton
      `TypePage`/`CameraPage` use. `FilePicker.Default.PickAsync` with a custom
      `FilePickerFileType` (Android MIME types for `.txt`/`.pdf`/`.docx`) —
      no native-packaging risk here (unlike Slices 31/32) since it just opens
      the system document picker, no bundled native library or model asset of
      its own. Verified: `dotnet build` (Mobile alone and the full `.slnx`,
      0 errors) and the 108-test suite (the three extractor-test files just
      needed their `using` updated for the new namespace — same tests,
      unmoved logic, still passing). **Not yet verified: running on a
      device/emulator** — same environment gap as Slices 30-32.
- [x] **Slice 34 — Android CI: build + signed AAB, internal testing track.**
      (Decision 42) New GitHub Actions workflow builds the MAUI Android project
      and packages a signed `.aab` (CI-held upload key; Play App Signing re-signs
      for distribution). Once Play Console credentials exist, upload to the
      **internal testing** track — mirrors the Windows Store pipeline's original
      "testing" stage before any public submission. GitVersion continues to supply
      the SemVer for the Android `versionName`/`versionCode`.
      **Built:** `.github/workflows/android-build.yml`, structured like the
      sibling Lets-Call-Mom project's own `android.yml` (same problem, same
      fix, found by reading that repo's actual workflow rather than
      reinventing it): a `HAS_SIGNING`/`HAS_PLAY_PUBLISHING` pair of env
      booleans (`secrets.X != ''`, since `secrets` isn't usable directly
      inside a step `if:`) gate the signed-build and Play-upload steps
      independently, so the workflow is safe to merge before either secret
      exists — an always-on unsigned Debug build (`dotnet build -f
      net10.0-android`) is the real "does this still compile" check, with no
      secrets required. `version`/`test` jobs mirror `build.yml` exactly
      (same GitVersion config, same unit-test gate); the Android
      `versionCode` is derived as `major*10000 + minor*100 + patch` from
      GitVersion's own numeric outputs (monotonically increasing as long as
      main only bumps forward, which `GitVersion.yml` already guarantees) —
      `versionName` is just `majorMinorPatch` passed straight through.
      Release signing uses `-p:AndroidPackageFormat=aab` +
      `-p:AndroidKeyStore=True` + `-p:AndroidSigningKeyStore/KeyAlias/
      KeyPass/StorePass` (confirmed via context7 that `AndroidSigningKeyPass`'s
      `env:`-prefix indirection is documented as **unsupported once
      `AndroidPackageFormat=aab`** — passed as plain `-p:` values instead,
      still never logged since GitHub Actions masks any output matching a
      registered secret's value); Play upload via `r0adkll/upload-google-play@v1`
      (same action the sibling project already uses), main-only, gated on
      both secrets. **Generated the actual CI-held upload keystore** (RSA
      2048, 10000-day validity, alias `upload`) via `keytool` and stored it
      through the `manage-secrets` skill — durable copy + registry row in the
      private `ashkansirous/secrets` store, then pushed as four GitHub Actions
      repo secrets (`ANDROID_SIGNING_KEYSTORE_BASE64/KEY_ALIAS/KEY_PASSWORD/
      STORE_PASSWORD`) via `gh secret set`. `PLAY_SERVICE_ACCOUNT_JSON` is
      **not** set — that credential can only come from the user's own Google
      Play Console (Setup → API access → service account), the same
      never-fabricate-a-third-party-credential line `store-submit.yml`'s
      Azure AD secrets already draw; `HAS_PLAY_PUBLISHING` stays false and
      the publish step stays a documented no-op until it's added, exactly
      like `AZURE_AD_TENANT_ID` for the Windows Store pipeline. Also note:
      Play Console requires the very first release on any track to be
      uploaded by hand once through its UI regardless — a one-time API
      limitation this workflow can't route around, same caveat the sibling
      project's own workflow documents.
      Verified for real, not just read: ran the exact signing command
      locally against a throwaway 1-day test keystore
      (`-p:AndroidKeyStore=True -p:AndroidSigningKeyStore=... -p:
      AndroidSigningKeyAlias=testkey ...`) — produced
      `uk.sirous.readthestupidtext-Signed.aab`, then `jarsigner -verify`
      confirmed "jar verified" signed by the test cert's own DN, proving the
      MSBuild property wiring is genuinely correct before it ever needed a
      GitHub Actions run to find out. Also verified: `dotnet build`
      (full `.slnx`, 0 errors) and the 108-test suite, unaffected by this
      slice (no application code changed). **Not yet verified: the workflow
      actually running in GitHub Actions** — needs a push/PR to trigger; not
      something triggerable from this environment. **Not yet verified:
      Play Store upload** — blocked on `PLAY_SERVICE_ACCOUNT_JSON`, which only
      the user can create.

**Batch 7 — Read-along text box, disk-backed audio, timer bug fix (Windows).**
Addresses three user-reported problems: (1) no visual read-along display, (2)
synthesized audio held fully in memory instead of spilling to disk on large
files, (3) the elapsed/total read-through timer getting stuck. Ordered
smallest-first: the standalone bug fix, then the (also standalone) memory
fix, then the text box in two passes — a minimal window that proves the
highlight/toggle wiring, then zoom + real pagination on top of it.

- [ ] **Slice 35 — Fix the stuck elapsed/total read-through timer.** (Decision
      51) Root-cause why `Total` (or the whole timer) stops updating during a
      read, via the systematic-debugging skill — no fix assumed up front.
      Regression-test the underlying formatter/accumulation logic
      (`ReadTimingFormatter`/`ReadTimingTracker`) for whatever the root cause
      turns out to be.
- [ ] **Slice 36 — Disk-backed audio chunks.** (Decisions 49, 50) Replace the
      in-memory per-chunk buffer `SupertonicSpeechReader`/`CompositeSpeechReader`
      feed to `MediaPlayer` with a temp WAV file per chunk under
      `…\TemporaryFolder\audio\<activity-id>\`; extend the existing chunk map
      (Decision 50) with each chunk's file path. Delete a read's folder on
      terminal state or supersede (reusing the generation-counter teardown) and
      sweep orphaned folders older than 1 day at startup (mirrors Decision 27's
      log sweep). Unit-test the pure cleanup/sweep-eligibility logic.
- [ ] **Slice 37 — Reading text box: window, toggle, whole-chunk highlight.**
      (Decisions 45, 46, 50) New floating `AppWindow`, opened/closed by a new
      4th icon `ToggleButton` in the control panel's `CONTROLS` row. Extend the
      chunk map (Decision 50) with each chunk's source-text character range.
      While a read plays, the window shows the current chunk's text with it
      highlighted (plain scrollable text for this first pass — no pagination or
      zoom yet, those land in Slice 38). Closing the window doesn't stop
      playback; toggling it back open re-syncs to the current chunk.
- [ ] **Slice 38 — Reading text box: zoom + fit-to-box pagination.** (Decisions
      47, 48) Add +/- zoom controls to the text box's own header, enforcing the
      dynamic 30-word-sentence zoom-in floor and a fixed ~32px ceiling. Replace
      the Slice 37 scrollable view with the greedy fit-to-box pagination
      algorithm (own segmentation, independent of `SpeechTextChunker`); the box
      auto-advances pages to keep the currently-highlighted chunk in view.
      Unit-test the pure pagination-fill and zoom-floor logic.

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
- **Slice 25:** trigger any existing read path → timer shows `00:00/--:--`, ticks
  once per second, then flips to the real total the instant synthesis of every
  chunk finishes, matching the worked example (`00:00/--:--` → … → `00:03/02:23`).
  `dotnet test` covers the formatter and the duration-accumulation sequencing.
- **Slice 26:** during a read, click skip-forward → elapsed jumps ~10s to the
  nearest chunk boundary and audio resumes there; skip-backward similarly; clamps
  at 0 and at the furthest synthesized point. `dotnet test` covers the pure
  target-selection and clamping logic.
- **Slice 27:** use the panel's Upload button (or the tray "Read file…" item) on a
  `.txt` file → reading starts almost immediately, supersedes any read in
  progress, and the Activity Log shows `Trigger=FileUpload` with the file name in
  **Source**. `dotnet test` covers the composite extractor's routing.
- **Slice 28:** upload a multi-page PDF → text extracts and reads correctly; an
  oversized PDF triggers the soft-cap warning instead of hanging.
- **Slice 29:** upload a `.docx` → text extracts and reads correctly.
- **Slice 30:** install the Android app on a device/emulator, type or paste text,
  tap Play → it reads aloud at the selected speed via Android's built-in TTS;
  Pause/Play and the speed control work exactly as their Windows counterparts.
- **Slice 31:** open the voice picker → the same ten Overlord-named voices appear;
  pick one → the next read uses it and sounds like the Windows Supertonic voice,
  not the Android system voice; restart the app → the chosen voice and speed are
  restored (`Preferences`-backed settings). Force the model to fail to load (test
  hook) → falls back to Android `TextToSpeech` without crashing.
- **Slice 32:** tap the camera button, photograph a printed page → the extracted
  text begins reading aloud shortly after capture, through the same chunked
  pipeline as every other input path.
- **Slice 33:** use the file picker to open a `.txt`, `.pdf`, and `.docx` file in
  turn → each reads correctly, reusing the same extractors verified in Slices
  27-29.
- **Slice 34:** a CI run on `main` produces a signed `.aab` artifact; once Play
  Console credentials are configured, the same run uploads it to the internal
  testing track and it's installable via the Play Store's internal-testing link.
- **Slice 35:** trigger a read that used to leave the timer stuck → `Total`
  now resolves and the `mm:ss/mm:ss` display keeps ticking for the whole read.
  `dotnet test` covers the regression case for whatever the root cause was.
- **Slice 36:** read a large file, watching Task Manager → memory no longer
  grows unbounded with file size; `…\TemporaryFolder\audio\<id>\` contains a
  WAV per chunk during the read and is gone after it finishes; skip
  forward/backward still works unchanged. Restart with a >1-day-old orphaned
  `audio\` folder present → it's swept on startup.
- **Slice 37:** toggle the new 4th `CONTROLS` icon on → the text box opens;
  during a read, it shows the current chunk's text with it highlighted, and
  updates as playback moves to the next chunk. Close the window → playback is
  unaffected; reopen it → it re-syncs to whatever chunk is currently playing.
- **Slice 38:** with the text box open, click +/- → text zooms in/out live;
  zooming in stops right where a 30-word sentence would no longer fit at the
  current box width; a long paragraph is shown one fit-to-box page at a time,
  auto-advancing as playback crosses into the next page's chunk.
- Manual UI checks driven through the running app; no browser E2E harness
  applies to a native tray app or a MAUI mobile app.
