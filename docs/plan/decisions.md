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

    **Design refresh (post-Slice 34) — unified Reader screen, resynced from the
    same live canvas.** The three-tab Type/Camera/File layout above (Screens 1–3
    plus the File tab) was superseded by a second resync of
    `Option C - Media Card.dc.html`: Paste, Scan and File are now **input
    actions, not modes** — each drops text into the **same** editor, and the one
    voice row + transport card beneath it applies to whatever is in it, rather
    than each path owning its own screen and its own player. Concretely:
    - **Reader (home, replaces Screen 1 + the File tab).** Brand app bar with
      just the title (no sub-line — voice and speed moved into the body). An
      `ADD TEXT` row of three equal buttons — Paste / Scan / File — sits above
      the editor card; Paste reads the clipboard directly, File opens the
      `FilePicker` directly (no more idle/processing/result screen of its own),
      and Scan pushes the Scan screen. Below the editor: a **Voice row** (name +
      chevron, opens the picker — replacing the old header mic button) and the
      same transport + 6-preset speed card as before. No bottom nav — this is
      the Shell's only `ShellContent`.
    - **Scan (replaces Screen 2, "Camera → OCR").** Unchanged capture flow, but
      the result bar's button is now **Use text**, not **Read**: it pops the
      page and hands the OCR result back to Reader via a `scannedText` query
      property instead of speaking it directly — there is only ever one player,
      and it lives on Reader.
    - **Voice picker (Screen 3)** is unchanged in content, now reached from the
      Reader's Voice row instead of a header mic button; dark-theme token fixes
      (secondary text, chip fills) were applied where the picker still used a
      light-only color, matching the resynced doc's explicit "no control ever
      goes black-on-black" callout.
    The local `design_handoff_tray_panel/Option C - Media Card.dc.html` was
    re-pulled from canvas `90615641-f298-4e21-8c50-ca2efbeaaebc` to match; its
    `README.md` still describes the three-tab version and was intentionally left
    alone (per the doc's own precedence rule, "use the HTML purely as the visual
    + behavioral source of truth" — the README is the stale artifact here, not
    the code). No slice renumbering: this is a design-refresh note against the
    already-`[x]`-complete Slices 30–33, the same convention used for the
    Windows panel's post-Slice-13 redesigns.
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
52. **CI produces zero Actions artifacts (Batch 8).** The account has a
    **500 MB** Actions+Packages storage quota, shared by every repo and billed
    as GB-hours across the month (so deleting artifacts stops accrual but never
    refunds it). One full run uploaded ~1.07 GB (MSIX x64 367 MB + ARM64 359 MB
    + `.aab` 173 MB + `.apk` 173 MB), twice the whole quota, which is why
    PR #163's `retention-days` + cleanup action could only soften it. **GitHub
    Release assets don't count toward that quota**, so build outputs go
    straight to a Release, and `actions/upload-artifact` is no longer used for
    build outputs. A workflow with no uploads also keeps running while the
    account is over quota.
53. **PR builds are compile-and-test checks only (Batch 8).** PRs build and
    test every affected platform but upload nothing. Build minutes on standard
    runners are free for this public repo, so the full check costs no storage.
    A reviewer who needs an installable build can use `workflow_dispatch` on the
    branch (which still uploads nothing), or build locally.
54. **On `main`, jobs hand off through a draft Release, not artifacts
    (Batch 8).** A `release-draft` job creates (or reuses, on a re-run) a
    **draft** `v<x.y.z>` release targeting the merge commit. Each build job
    `gh release upload --clobber`s its own assets into it, and a
    `release-publish` job flips it to published only once every
    affected build succeeded. Store/Play submission then read from the
    published Release. Draft releases create no tag, so a failed run leaves no
    half-cut `v*` tag behind (GitVersion's source of truth stays clean). Only
    `ReadTheStupidText.App_*.msix` is uploaded, never the Windows App Runtime
    dependency packages the old `**\*.msix` glob also picked up.
55. **One pipeline for both platforms (Batch 8).** `android-build.yml` folds
    into `build.yml` as a `build-android` job, so one run produces one release
    carrying the MSIX (x64 + ARM64) **and** the signed `.aab` + `.apk`. This
    replaces two workflows racing to create or attach to the same tag, and runs
    the shared `version` and `test` jobs once instead of twice. The Play
    internal-testing upload moves to a post-publish `play-submit` job that
    reads the `.aab` from the Release, mirroring `store-submit`.
56. **Path-filtered per-platform builds (Batch 8).** A `changes` job
    (`dorny/paths-filter`, confirmed via context7 at implementation) classifies
    the diff into three groups:
    - **core:** `Domain`, `Application`, `Documents`, `tests/`, `global.json`,
      `GitVersion.yml`, `*.slnx`, `Directory.*` files, and the pipeline's own
      workflow files.
    - **windows:** `App`, `Infrastructure`, `store-submit.yml`.
    - **android:** `Mobile`.

    On `main`, the diff is taken against the **last published `v*` tag**, not
    the previous push. A platform change whose run failed, or whose pending
    run was dropped by the concurrency queue (GitHub keeps only one pending run
    per group), is therefore still built by the next run.

    Windows jobs (build + `store-submit`) run on **core || windows**, and
    Android jobs (build + `play-submit`) on **core || android**. `test` runs
    whenever either platform would build. A docs-only change builds nothing and
    cuts no release. `workflow_dispatch` forces both platforms.

    This is a job-level `if:` rather than a workflow-level `paths:` trigger
    because one run must still coordinate a single release across whichever
    platforms changed. It is also safe because no required status checks
    exist on `main` (only `deletion` + `non_fast_forward` rules), and a job
    skipped by `if:` reports success anyway.
57. **A release always carries every platform's latest asset (Batch 8).** When
    only one platform changed (or Android changed but the signing secret is
    absent, so no fresh `.aab`/`.apk` exists), `release-draft` **carries forward** the other
    platform's assets from the most recent release that has them (a
    `gh release download` + `upload`, Releases API only, so it costs no Actions
    storage). The README's `releases/latest` MSIX link therefore never lands on
    an Android-only release. Carried-forward assets keep their original
    version in the file name. `store-submit`'s manual-dispatch "latest release"
    lookup is still hardened to pick the newest release that actually has
    **built** MSIX for this version, and submission only fires for a platform
    that was actually rebuilt in this run.
58. **Default artifact retention drops to 1 day, account-wide safety net
    (Batch 8).** The repo-level "artifact and log retention" setting goes from
    90 → 1 day on this repo **and** `Lets-Call-Mom` (which shares the same
    quota; 68 MB of artifacts there at planning time), so any future stray
    upload (e.g. the automatic GitHub Pages artifact) is short-lived by default.
    PR #163's `cleanup-old-artifacts` composite action and its jobs are
    **deleted**: with nothing uploaded there's nothing to clean, and dropping
    them also drops the `actions: write` permission they needed.
59. **Batch 9 order: Android first, iOS in parallel, Windows last (Batch 9).**
    Two phone scope docs (2026-10-20, *mobile UX / OCR / translation* and *iOS
    + per-platform pipeline*) are folded into one batch. Android gets the new
    features first. iOS starts now, in parallel, on the same MAUI project
    (Decision 37). Windows catches up last. The kids book reader is a separate
    product (Dino Reader) built in this repo on the shared core, planned in its own batch.
60. **User data stays 100% offline; downloading packs is allowed (Batch 9).**
    Text, images, scans and translations never leave the device. The app may
    now download **voice/OCR language packs**, which is its first network
    use: Android gets the `INTERNET` permission, Windows the `internetClient`
    capability, and iOS needs no entitlement. `PRIVACY.md`, `docs/privacy.html`
    and `STORE.md`'s capability justification say exactly this. Still no
    telemetry and no cloud TTS/OCR/translation.
61. **No bundled voice model on any platform; supersedes Decisions 14 and 39
    (Batch 9).** Every voice, Supertonic-3 included, downloads as a pack after
    the first-run language choice (Decision 68). The ~145 MB `VoiceModel/`
    leaves the MSIX, the AAB and the iOS bundle, as does the Mobile
    `MauiAsset` stand-in for Play Asset Delivery. The first launch needs a
    network once. Until the pack lands (or when the download fails), reads
    use the **system voice** (WinRT `SpeechSynthesis` / Android
    `TextToSpeech` / `AVSpeechSynthesizer`), which is promoted from internal
    safety net to a real, labelled first-run voice.
62. **Packs are hosted as GitHub Release assets in this repo (Batch 9).**
    Packs go under dedicated `packs-<n>` releases, separate from the app's
    `v<x.y.z>` releases. Release assets don't count toward the Actions
    storage quota (Decision 52). A `packs-<n>` tag isn't a SemVer `v` tag, so
    GitVersion ignores it (verify in Slice 50). A `packs.json` manifest lists
    each pack's id, language, kind (voice/ocr), version, size, sha256 and
    licence. Clients verify the sha256 before use, and downloads resume and
    retry. Publishing a pack is a manual `workflow_dispatch` (`packs.yml`),
    not part of every app build.
63. **Voices per language (Batch 9).**
    - **Supertonic-3 multilingual** (Apache-2.0) covers en, fr, es, pt, ru, ja
      and ar.
    - **Farsi** uses a **Piper fa_IR** voice through sherpa-onnx.
    - **Chinese** uses **MeloTTS zh_en** through sherpa-onnx (MIT). Kokoro
      stays rejected (Slice 9 note).
    - Which Farsi voice (gyro or amir) is decided in Slice 51 by whichever
      licence is confirmed redistributable. If neither is, Farsi falls back to
      the system voice and the gap is written down.
    - **Scoped GPL exception to the "no GPL" rule (Decision 16):** Piper needs
      espeak-ng data (GPL-3.0). That data lives **only inside the downloaded
      Farsi pack**, never in the repo, the MSIX, the AAB or the iOS bundle,
      and the pack carries the licence text and a source link. `CLAUDE.md`,
      `STORE.md` and the third-party notices describe the exception. The app
      itself stays MIT.
64. **Camera: the system document scanner, text straight into the editor
    (Batch 9).**
    - **Android:** ML Kit **Document Scanner** (`Net.Google.MLKit.DocumentScanner`,
      version confirmed via context7). It opens the camera at once, crops,
      takes multiple pages, reorders them and imports from the gallery, all
      with no camera permission.
    - **iOS:** VisionKit `VNDocumentCameraViewController`.
    - Pages are OCR'd in order and **appended to the editor**. The
      `ScanPage` result screen and `PendingScanResult` go away.
    - The Android scanner needs Google Play services. Without them, the app
      falls back to the existing `MediaPicker` single-photo path and says why.
65. **OCR: PaddleOCR PP-OCRv5 (ONNX, Apache-2.0), one model per language, in
    the language pack (Batch 9).** ML Kit v2 text recognition has no Arabic,
    Farsi or Cyrillic. PaddleOCR runs on the onnxruntime already shipped with
    sherpa-onnx, so one engine serves Android, iOS and Windows. ML Kit Latin
    OCR stays only as the fallback before the OCR pack is downloaded.
66. **Head start: a sentence-level audio cache (Batch 9).**
    - **Cache key:** `hash(normalized sentence) + voiceId + language`, sitting
      on top of the disk-backed chunk files and the shared chunk map
      (Decisions 49-50).
    - **First chunk:** the first **two** sentences, raised from Decision 25's
      one sentence.
    - **When synthesis starts:** at once after a file or scan lands, and after
      **1 s idle** while typing. Synthesis then keeps running ahead of
      playback.
    - **Edits:** editing sentence 3 or later re-synthesizes only the changed
      sentences.
    - **Cancellation:** cache hits respect the generation counter (Decision
      15/25), so `Stop()` and supersede still win.
    - **Play button:** shows a loading animation from tap until the first
      `Playing` transition.
67. **Translation: on-device only, opt-in, with a visible source dropdown
    (Batch 9).**
    - **Android:** ML Kit Translation plus ML Kit Language ID. Auto-detect
      sets the source dropdown, and the user can always override it.
    - **iOS:** the ML Kit iOS SDK if a usable .NET binding exists (checked in
      Slice 55). Otherwise Apple's Translation framework for every language
      but Farsi, and Farsi translation on iOS is listed as a gap.
    - **Windows:** **Opus-MT ONNX** models on the shared onnxruntime
      (recommended; Bergamot is the alternative, decided in Slice 57).
    - Translation pivots through English and is casual quality, as the
      reader is told.
    - A **"Translate to <my language>"** toggle controls it, default off.
      When on, it applies to **every** read path, Windows auto-read on
      selection and copy included.
68. **The first-run language drives UI, voice, OCR and translation target
    (Batch 9).**
    - **Languages:** en, fa, fr, es, pt, zh, ar, ru, ja.
    - **First run:** the user picks a language, which sets the UI language,
      downloads the matching voice and OCR packs, and becomes the translation
      target. It's changeable later in settings.
    - **UI strings:** `.resx` resources, with full RTL layout for fa and ar.
69. **One shared version; the path filter grows an `ios` group (Batch 9).**
    - Decisions 17 and 57 stand: one `v<x.y.z>` per merge, and untouched
      platforms are not rebuilt or resubmitted. Their assets carry forward
      (per-platform tags rejected again).
    - **Mobile split:**
      - `src/ReadTheStupidText.Mobile/Platforms/Android/**` builds Android.
      - `.../Platforms/iOS/**` builds iOS.
      - The rest of `Mobile/**` (shared MAUI) builds Android and iOS.
      - Core builds all three.
    - For this to be true, Android-bound code outside `Platforms/Android` (for
      example `CompositeSpeechReader`'s Android constructor arguments) moves
      behind interfaces first.
70. **iOS: TestFlight from CI first, App Store by hand later; signing reuses
    the Lets Call Mom Apple team (Batch 9).**
    - **App identity:** RTST gets its own App ID `uk.sirous.readthestupidtext`
      (same as the Android `ApplicationId`), its own distribution profile and
      its own App Store Connect API key, all stored via the `manage-secrets`
      store and set as this repo's secrets.
    - **Borrowed from Lets Call Mom:** the keychain/profile steps and the
      TestFlight distribute script, adapted from Lets Call Mom's `ios.yml`
      and `testflight-distribute.mjs`.
    - **Build:** `dotnet publish -f net10.0-ios` on a macOS runner, not
      `xcodebuild`.
    - **Native packaging:** sherpa-onnx's iOS native packaging is checked by
      unpacking the built `.ipa`, the same lesson as the Android `.so`
      gotcha.
71. **Store listings: new search terms and description, in all 9 languages
    (Batch 9).**
    - **Search terms (7, each ≤30 chars):** "read aloud", "text to speech",
      "tts", "read text aloud", "speak selected text", "pdf reader aloud",
      "natural voices offline".
    - **Description:** rewritten and drafted in `STORE.md`.
    - **Languages:** listings are added for all 9 languages on the Microsoft
      Store and Google Play. No title change for now.
    - The Partner Center and Play Console edits are a manual step for the
      user.
72. **Two products, one repo, one version (Batch 9).** Dino Reader is a
    second app in this repo, a new UI on the shared core (Domain,
    Application, Documents, voice engines, packs, OCR), not a new repo.
    - **One shared version:** a single GitVersion `v<x.y.z>` per merge covers
      both products, exactly as for platforms (Decisions 17, 57, 69).
      Per-product tag prefixes are rejected.
    - **Per-product path groups:** each product gets its own filter group.
      A product whose code didn't change is not rebuilt or resubmitted, and
      its release assets carry forward. Shared core rebuilds both products.
    - **Shared mobile code:** the Android/iOS readers, OCR and scanner move
      out of the Mobile app project into a shared MAUI class library that
      both apps reference. That is Dino Reader's first slice, planned in
      its own batch.

73. **Dino Reader: a kids book reader with a voice per character (Batch 10).**
    It is the second product in this repo (Decision 72), a MAUI app at
    `src/DinoReader.App`. The display name is **"Dino Reader"** and the app
    id is `uk.sirous.dinoreader`. Instead of one voice, it works out who is
    speaking and gives each character the same distinct voice for the whole
    book, with a narrator voice the user picks. It inherits every Read The
    Stupid Text rule: 100% offline for user data, the same voice/OCR packs,
    the 9 languages, translation, licences and pipeline. The source is the
    phone doc `2026-10-20-dino-reader.md` plus a desk refine on 2026-10-20.
74. **Speaker attribution is rule-based, per language, on the device (Batch
    10).**
    - **Segments:** quote marks split the text into narration and quoted
      lines.
    - **Who speaks:** a quote is attributed by "X said" / "said X", by
      pronoun tracking, and by alternating speakers in untagged back-and-forth.
    - **Gender:** comes from pronouns, a names list and titles.
    - **Language:** attribution runs in the **source** language, and only
      then is each segment translated.
    - **Fixes:** a grown-up corrects mistakes in the cast screen. If the
      rules get common kids' books badly wrong, a small on-device model is
      the fallback to consider, never a cloud one.
75. **Voice casting (Ashkan's algorithm, Batch 10).**
    - The narrator's voice is never reused for a character.
    - While there are enough voices, no two characters share one.
    - When characters outnumber voices, an unassigned character gets the
      gender-matched voice that has spoken the **fewest words so far**,
      counted per voice. Main characters' voices are never handed out again.
    - Farsi (Piper, 1-2 voices) will reuse voices heavily, which is a known
      limit.
76. **Speech by segments: one read, many voices, real source ranges (Batch
    10).**
    - **New call:** `ISpeechReader` gains a segment-based read: a list of
      `(text, voiceId, sourceStart, sourceEnd)`. Each chunk carries its own
      speaker into synthesis. Decision 29's chunk-boundary swap already
      threads the speaker through the playback loop as a parameter.
    - **Highlight events:** the Android reader raises `ChunkChanged` **per
      chunk with the real source range**, as Windows already does (Decision
      50). Today it raises it once, covering the whole text.
    - **Read The Stupid Text benefits too:** the ranges are what any
      mobile highlight needs.
77. **The gallery is a local library behind a grown-up gate (Batch 10).**
    - **What a story is:** title, cover, language, pages (each page's
      picture if any, its text and line positions), and its **cast**
      (character, gender, voice), remembered per book.
    - **Storage:** stories are stored only on the device, under
      `AppDataDirectory/stories/<id>/` (`story.json` + page images). There
      is no sync and no upload.
    - **Grown-up gate:** adding, removing or editing a story or its cast
      sits behind **press-and-hold 3 s on a lock button, then a simple sum**.
      Reading never needs the gate.
78. **Text comes with its position on the page (Batch 10).**
    - **New extraction call:** extraction gains a positioned variant that
      returns lines with bounding boxes in normalized page coordinates. The
      existing text-only `IImageTextExtractor.ExtractTextAsync` stays for
      Read The Stupid Text.
    - **Where positions come from:** ML Kit and PaddleOCR return their line
      boxes (Slice 52 must keep them, not discard them). PDFs use PdfPig word
      positions grouped into lines, falling back to OCR when the PDF has no
      text layer.
    - **Page images:** scanned page images are **kept**. PDF pages are
      rendered to images by the platform renderer (Android `PdfRenderer`,
      iOS PDFKit, `Windows.Data.Pdf`).
    - **Sources:** stories come from camera scan (multi-page), photo import,
      PDF, and typed / .txt / .docx text.
79. **Reading modes: text and picture, always switchable by the child (Batch
    10).**
    - **The switch:** a big two-icon switch (📖 / 🖼) in the reading screen's
      top bar is **always** shown, remembered per story, and defaults to
      picture mode when the story has pictures.
    - **Picture mode:** highlights the **sentence / dialogue line** being
      spoken as boxes over the page picture. A story with no pictures gets
      **generated book-style pages** (big friendly type on a page
      background), so picture mode works for every story.
    - **Highlight unit:** the sentence/line. The engine has no word
      timestamps, so word-by-word karaoke is out of scope for now.
80. **Pages follow the reading; the child can wander (Batch 10).** The page
    turns by itself as the reading reaches it. A child can swipe to another
    page and tap a sentence to read from there, and pressing play resumes
    auto-follow.
81. **Dino Reader platforms and order (Batch 10).**
    - **Platforms:** Android and iOS first, Windows (MAUI WinUI head) last.
    - **Shared code:** the Android/iOS readers, OCR, scanner and other
      platform code move from the Read The Stupid Text Mobile app project
      into a shared MAUI class library, `src/ReadTheStupidText.Mobile.Shared`,
      that both apps reference.
    - **Pipeline:** the pipeline gets a `dinoreader` path group and the
      same shared version (Decisions 69, 72). Distribution is Play internal
      testing and TestFlight, as for Read The Stupid Text.
    - **Order:** the pure slices (dialogue parser, voice caster) may start
      alongside Batch 9. The picture-mode slices wait for the scanner and
      PaddleOCR (Slices 45, 52).
82. **Android minimum is API 28 (Android 9, 2018) for every Android app in
    this repo** (Read The Stupid Text Mobile now; Dino Reader and
    `Mobile.Shared` when they land). Supersedes Slice 31's 23. Play automatic
    protection needs at least 24, and 28 reaches about 95% of active devices.
    The target SDK is not pinned; it follows the .NET Android workload.
    Background: `docs/plan/notes/2026-10-20-android-min-sdk-28.md`.
83. **A single newline is whitespace, classified at read time (Batch 13).** Both
    apps split sentences through one pure `LineReflow` in Application: `\n` joins
    with a space, `-\n` + lower-case rejoins the word, a blank line is a paragraph,
    and a line is a heading or list item (its own unit, paragraph pause) by a
    classifier that uses OCR line height and spacing when boxes exist and
    length/case shape otherwise. Stored text is never rewritten, so existing
    stories and clipboard reads are fixed with no migration and Dino's
    per-line boxes stay valid. Chosen over import-time reflow (breaks box
    mapping, does nothing for RTST) and over "break on every unpunctuated line"
    (undoes the reflow for ordinary wrapped lines). (Slice 76)
84. **Dino page images always survive text edits (Batch 13).** Editing a story
    never deletes `page-N.jpg`; a page whose text no longer matches its OCR lines
    shows its picture and reads without highlight boxes. Covers are the first
    page by default, or any page, a gallery photo (`cover.jpg`) or a flat
    colour. Dino pins `UserAppTheme = Light`; night colours live only in
    `ReadingTheme`. Fuzzy re-mapping of edited text onto boxes is out of
    scope. (Slice 77)
85. **One add/edit wizard with an in-app page review on every platform (Batch
    13).** Pages (drag to reorder via `CollectionView.CanReorderItems`, rotate by
    quarter turns, rectangular crop, delete, add) → Cover → Text. OCR runs when
    Pages is confirmed and re-runs only for changed pages. The transform code is
    one `ImageTransform` in `Mobile.Shared`; the ML Kit scanner stays the Android
    capture path only. Replaces `AddStoryPage`, `EditStoryPage` and `PagesPage`.
    Chosen over ML Kit gallery import (Android-only, no re-edit of saved
    stories). (Slice 78)
86. **Background text is dropped by a size/position heuristic with a manual
    override (Batch 13).** `TextRegions` in Application groups OCR lines into
    regions, keeps the dominant block and marks regions ≥ 1.5× or ≤ 0.6× its
    line height, or small and far from it, as background; a page is never
    emptied. The choice is stored per page; Dino's wizard shows the regions on
    the page and a tap toggles any of them. RTST gets the classifier, no UI.
    Chosen over manual-only (every page needs a check) and heuristic-only (a
    wrong guess is unfixable). (Slice 79)
87. **Grown-up mode is a state of the kids' gallery, not a page (Batch 13).**
    After the gate the same tiles (covers + titles) show an edit overlay (Edit,
    Cast, Delete) and the Add tile; Edit opens the wizard. `ManagePage` is
    removed. The gate and the leave-the-app reset are unchanged and the mode is
    never persisted. (Slice 80)

88. **The voice installer follows every language change (Batch 14).**
    `VoicePackInstaller` listens to `LanguageChanged` like `OcrPackInstaller`, so
    changing the language anywhere downloads that language's pack at once; the
    voice picker shows the download state and Retry. (Slice 81)
89. **No voice reads a language it can't speak (Batch 14).** The phone voice is
    set to the language being read. A language is readable when the phone TTS has
    a voice for it or a natural pack is installed or in the catalog; the Translate
    toggle and the Phone voice row show only for readable languages. (Slice 82)
90. **Scan replaces the editor text (Batch 14).** Paste, Scan and File all
    replace; one multi-page scan still joins its pages in order. Supersedes the
    "appended to the editor" part of Decision 64. (Slice 83)
91. **OCR follows the page's script, never the app language (Batch 14).** The
    built-in recognizer reads first; installed packs are tried only for mostly
    non-Latin pages, and RTL ordering applies only to an RTL pack's result.
    (Slice 84)
92. **The layout never mirrors (Batch 14).** Pages and control rows stay
    left-to-right in every language; text views follow their own content's
    direction. Supersedes the "full RTL layout for fa and ar" part of Decision 68.
    (Slice 85)
93. **Four Farsi voices in one pack, Amir dropped (Batch 15).** The `piper-fa`
    pack (v2) carries ganji_adabi (default), ganji, mana and gyro, one model
    folder per voice with one shared espeak-ng-data; Amir scored worst on a
    Whisper round-trip and was rejected by ear. A pack voice may name its own
    model folder, and an installed pack older than the catalog's is replaced.
    (Slice 89)
94. **Dino stories travel as `.dino` packages (Batch 16).** A `.dino` file is a
    zip of `package.json` (its own `schemaVersion`), `story.json` and the
    story's cover and page images, all by plain file name. Import gives the
    story a new id and refuses a newer schema, an unsafe path or an oversized
    entry. Exporting is a developer-only action. Reverses Slice 61's "no export"
    (export only; sync stays out). (Slice 91)
95. **Three bundled starter stories (Batch 16).** The Three Little Pigs, The
    Gingerbread Man and The Tortoise and the Hare: public-domain text only,
    generated colour covers, installed once on first run, deletable. (Slice 91)
96. **A free, curated story store behind the grown-up gate (Batch 16).** The
    developer uploads `.dino` packages to R2 by hand under
    `dinoreader/stories/<id>/`; the Worker lists only complete folders and
    serves cover and download. No accounts, no tracking, no user uploads.
    Reverses the Batch 10 out-of-scope "A story store or marketplace".
    (Slice 92)
97. **Backup is one manual file; phone backup stays off (Batch 16).** A
    grown-up saves a single backup file from behind the gate and restores it,
    skipping stories already present. `allowBackup="false"` (Slice 58) stays.
    (Slice 93)
98. **Dino's first run asks the language and shows the voice getting ready
    (Batch 16).** Replaces the silent English default in `SpeechStartup`. No
    tutorial. (Slice 93)
99. **The store, backup and first-run screens are designed in Claude Design
    first (Batch 16).** Slices 92 and 93 don't start their UI until the mock
    has those screens. (Slices 92, 93)
100. **Dino's layout never mirrors (Batch 18).** Every Dino screen, the reader
    included, stays left-to-right in every language, and page turns go the same
    way for every book; text elements follow their own content's direction.
    Extends Decision 92 to Dino. (Slice 96)
101. **The grown-ups button is a book with a plus, everywhere (Batch 18).** It
    replaces the lock in the gallery header and on the reading page; the gate
    and what it opens are unchanged. The `BackupPage` lock glyph stays.
    (Slice 97)
102. **Picture + text books (Batch 18).** A page with a picture and its own text
    (no line boxes) shows the picture beside the text (sideways) or above it
    (upright, as a text panel over the picture). Grown-ups add pictures per page in the
    Pages step: each page of a text-only file gets an optional picture slot,
    and "Pick pictures for all pages" fills them in file-name order; starters
    get pictures the same way. No schema bump. (Slice 98)
103. **The RTST phone main page shows only reading (Batch 19).** A gear for
    Settings (language, voice, speed; no translate toggle), a large read-only text area with the
    highlight and an Edit link, two big buttons (Text from pictures, Text from
    a file) and a simple player. (Slice 99)
104. **Multi-page capture is our own screen (Batch 19).** "Take a photo of
    each page": numbered thumbnails, "+ Add another page" and "Done"; each Add
    captures one page; OCR runs in the background. (Slice 100)
105. **One review page before reading; translation shows what it reads
    (Batch 19).** A big editable text box, a "Translate to <app language>"
    checkbox only when the languages differ, and one Read / Translate and read
    button. Translating replaces the text. Files go straight here. Reverses
    Slice 54's "the editor keeps the original" on the phone. (Slice 101)
106. **Ten open stories in the store (Batch 20).** Short retellings Claude
    writes from public-domain tales (Peter Rabbit in Potter's own words), all
    nine languages, coloured covers now and public-domain illustrations later
    as `story-v2`. `meta.json` gains an optional `credits` line shown on the
    store card. (Slice 102)
107. **Fourteen app languages (Batch 21).** `AppLanguage` adds Danish, Finnish,
    Swedish, Norwegian Bokmål (`nb`) and Dutch to all three apps; Claude writes
    the UI translations; da/fi/sv/nl read with Supertonic-3. Extends Decision 68.
    (Slice 103)
108. **A `piper-nb` voice pack (Batch 21).** Piper `no_NO-nvcc-medium` (ten
    speakers, CC0) for Norwegian on every platform; the espeak-ng GPL exception
    covers this pack with the same scope as `piper-fa`. (Slice 104)
109. **Dino speaks the five new languages fully (Batch 21).** A story rule set
    per language and every shipped book translated into all five. (Slice 105)
110. **Ten more Windows translation packs (Batch 21).** Opus-MT to and from
    English for da, fi, sv, nb and nl. (Slice 106)
111. **Every new Dino book translates to my language by default (Batch 22).** The per-story switch
    stays; on Windows and iOS the switch is greyed with "Translation works on Android for now". (Slice 107)
112. **Dino shows the translated text (Batch 22).** Highlight follows the translated sentence; scanned
    pages show picture plus text while translated; a per-story grown-up "Show original text" switch. (Slice 110)
113. **A Dino cast per reading language (Batch 22).** Auto-cast on first read in a language, pins kept
    per language, merges shared; the cast screen lists that language's voices. (Slice 108)
114. **One Dino narrator per language (Batch 22).** Chosen from a Narrator button on the grown-up
    gallery, used by every book; the per-book narrator is dropped. (Slice 109)
115. **Parent recordings in Dino (Batch 22).** AAC per page, on device, in backups, never in store
    uploads; mic only behind the grown-up gate; a recorded page always plays the recording. (Slice 111)
116. **Dino store age range and author (Batch 22).** `ageMin`/`ageMax`/`author` in `meta.json`,
    chips for Play's bands 5 & under, 6-8, 9-12, filtered on the device. (Slice 112)
117. **Dino voice flavours per character (Batch 23).** Small, Big, Bouncy, Sleepy or Normal (pitch
    and pace), guessed from the character, editable on the cast screen; narrator at a storyteller pace. (Slice 113)
118. **A downloadable `kokoro-en` pack for Dino English (Batch 23).** Kokoro storyteller voices when
    installed, Supertonic otherwise; RTST unchanged. (Slice 114)
119. **The espeak-ng GPL exception covers `kokoro-en` (Batch 23)**, same scope as `piper-fa`. (Slice 114)
120. **The storyteller pack is Dino's default voice (Batch 24).** Downloaded by itself over Wi-Fi on
    launch when missing ("Download now" for mobile data); the offer card, "Hear one" and "Use Dino's
    first voices" are removed. (Slice 115)
121. **One re-cast of every Dino book when the storyteller pack lands (Batch 24)**, hand-picked voices
    included (genders and flavours kept), replacing Slice 114's keep-Edited rule. (Slice 115)
122. **Dino's cast is a list of storyteller characters in every language (Batch 24).** About 16-20 per
    language, each a voice plus a flavour plus a local kid-friendly name, on the language's best engine;
    engine and Supertonic (Overlord) names are never shown; Dino casts, a grown-up swaps. (Slice 116)
123. **Dino animal characters say their sound about every 2-3 of their lines (Batch 24)**, in their own
    voice, per-language word lists, never when the line already has it, not on narration; a per-story
    switch, on by default. (Slice 117)
124. **The storyteller pack serves Spanish, French and Portuguese if it speaks them well (Batch 24)**;
    Japanese and Chinese only if the same check passes. (Slice 118)
125. **Claude illustrates Dino books by driving the Gemini web app in the user's Chrome (Batch 25).** One character
    chat per book (sheets, line-up), then fresh page chats with the line-up pasted in and outfits spelled out, at most 3
    pictures per chat and one per chat once a chat repeats a scene (a single long chat mixed scenes and outfits in the
    pilot); pictures are saved and committed without asking each time; stop at the free tier's daily cap and resume. Reverses Batch 10's "no AI illustrations" and
    supersedes Decision 106's public-domain follow-up. (Slice 119)
126. **One house style for every Dino book (Batch 25)**: soft watercolour and coloured pencil, warm gentle colours,
    simple rounded shapes, no text; style line and cast per book in `docs/design/story-art/<stem>.md`. (Slice 119)
127. **Dino pictures are 3:4 portrait, 896x1200 (Gemini's native output; never upscaled), text-free, characters and action in the top 70% (the words panel takes at most 30%, Slice 122; was top 40% before it) (Batch 25)**; the title on a
    cover comes from our code, never from Gemini. The store list gets a 288x384 `cover-thumb.jpg`; the full cover stays inside the package. (Slice 119)
128. **The bundled starters are also store stories with the same package id (Batch 25)**, so a deleted starter can be
    downloaded again. (Slice 119)
129. **Making a Dino story is the project skill `create-dino-story` (Batch 25).** (Slice 120)
130. **No Dino book shows harm or pain (Batch 25)**, in text or pictures, whatever the classic tale says;
    conflicts end gently; all 14 books audited and rewritten, translations updated, and a test scans every starter and store text for banned harm words. Bundled starters carry a content version (package manifest `version`, the same number in their store entry): an installed starter at a lower version is replaced on launch, keeping the cast and switches; a deleted one stays deleted. (Slice 119)
131. **In Dino picture mode the words take at most 30% of the visible picture with the phone upright, and each book has a grown-up switch to hide them (Batch 25).**
    The switch is off by default for books whose pictures already show the words (scans, photos of pages, PDFs) and on for every other book. (Slice 122)
132. *Reversed by Decision 157 (Slice 152, 2026-10-26): the animal comes from the book, not the cast character.* **A Dino cast character's animal comes from the storyteller character it is cast as (Batch 26; changed 2026-10-25).** Each roster character carries an animal kind or none (Cheeky Piglet = pig, Big Bad Wolf = wolf, Brave Knight = none), and animal sounds use the cast character's animal; a pig cast as a non-animal character makes no sound, by design. The stored `animal` field in `story.json`, the build-time animals and the guess from the name and story text only pick the first character when a book is cast or recast; they do not drive sounds afterwards. The Animal sounds card follows the current cast. There is no separate Animal control on the cast page (Batch 26-28 mock, frame 59). *Originally:* a stored per-character animal the grown-up could change on the cast page (Slice 125). (Slices 125, 136)

133. **Read The Stupid Text and Dino Reader stay in one repo (Batch 27).** About a third of the app code is shared and it is the engine (speech readers, voice packs, chunking, OCR, translation); splitting would mean versioned NuGet packages for every engine fix. The fix is in how changes ship. (note: docs/plan/notes/2026-10-25-product-separation.md)

134. **Only Mobile.Shared's namespace is renamed, to `ReadTheStupidText.Mobile.Shared` (Batch 27).** No wider rename of the shared projects for now. (Slice 131)

135. **Tray-only code lives in `ReadTheStupidText.Windows.Tray`, referenced only by the RTST Windows app (Batch 27).** Shared Infrastructure keeps speech, translation, settings, logging and sanitizing; tests run per product. (Slice 128)

136. **The shared speech engine carries no Dino-only voice features (Batch 27).** Flavours, pitch and animal effects live in `DinoReader.Core` and reach the engine through one small hook, so a shared-engine change is a real engine change and shipping both apps for it is correct. (Slice 129)

137. **Each product has its own version, tag and GitHub Release: `rtst-v<x.y.z>` and `dino-v<x.y.z>` (Batch 27).** Both continue from the current shared version; a release carries only its own product's files. (Slice 130)

138. **Dino fetches one small config from the Worker (Batch 28).** `GET /api/app-config` serves per-platform `minVersion`/`latestVersion` and the telemetry switch and sample rate from the R2 object `dinoreader/config.json`, so they change without a deploy or an app release; the app falls back to its cached copy, then to safe defaults. (Slice 132)

139. **Below `minVersion` Dino shows only a "Please update" page (Batch 28).** Its button passes the grown-up gate, then opens the Play in-app immediate update, the App Store page or the Windows Store update. Below `latestVersion` there is only a quiet line in grown-up settings. (Slice 132)

140. **Dino usage data is anonymous counts with context, never an install, device or advertising id (Batch 28).** No typed text, no own-book titles, nothing finer than the hour. A per-device profile would be a child's personal information under COPPA and profiling under the UK Children's code. (Slice 133; note: docs/plan/notes/2026-10-25-dino-usage-insights.md)

141. **Usage counts are on by default with a grown-up off switch and a line in the first-run screens (Batch 28).** (Slice 133)

142. **Usage events go to Workers Analytics Engine, batched per session, switched and sampled by the remote config (Batch 28).** Reports suppress counts under 10. (Slices 133-134)

143. **Read The Stupid Text usage data is anonymous counts with context, never an install, device or advertising id (Batch 29).** No read text, no file names, nothing finer than the hour; durations in buckets. On by default with an off switch in Settings (control panel on Windows) and one line on first run; no consent prompt. (Slices 137, 140)

144. **RTST reuses Dino's usage pipe (Batch 29).** The same Worker `POST /api/events` and `GET /api/app-config`, served on the RTST host, with RTST's own Analytics Engine dataset `rtst_usage` and config object `readthestupidtext/config.json`; the phone client is shared from `Mobile.Shared`. (Slice 137)

145. **No usage event is sent for an app until every store privacy declaration for it matches (Batch 29).** The remote `telemetry.enabled` stays `false` until the *Store privacy declarations* checklist in `plan.md` is ticked for that app; this also covers Dino's pending Play and App Store answers. (Slices 133, 137, 140)

146. **Any wait over 500 ms shows a stage indicator with Cancel (Batch 29).** An overlay over the reading area (a tray flyout for windowless Windows reads) naming the stage and progress; nothing shows under 500 ms; no Lottie for now. (Slices 138, 141)

147. **Early start translates and voices the head before the tap (Batch 29).** Original: first 2 sentences (else ~120 characters at a word boundary). Translation, only when the detected language differs from the user's: translate the first 3 sentences, voice the first 2. The tap cancels the other side; a missing translation model downloads early only on an unmetered network. Supersedes the one-vs-two-sentence mismatch of Decision 66 and `HeadPolicy.Default`: both now say two sentences (`Default` is two sentences, at most 120 characters; `Cached` stays two, at most 200). The phone's early start (Slice 139) keeps the translated head and translates only the rest, serializes the two voice preparations (the reader holds one at a time) and counts nothing itself. (Slices 139, 142)

148. **Batch 29 order: phone first, then Windows (Batch 29).** 137 (counts) -> 138 (wait indicator) -> 139 (early start) on Android/iOS, then 140-142 on Windows; iOS has no translation, so it gets counts and the indicator only. (Slices 137-142)

149. **One shared wait overlay for both MAUI apps; Dino shows it on every wait over 500 ms (Batch 30).** `Mobile.Shared/Waiting/`, driven by the `WaitStage` timer model; Dino's look is a bouncing dino drawn with MAUI animation (static under reduce-motion) and one grown-up line; no Lottie. (Slices 138, 143)

150. **Farsi's default voice is `mana` (female) in both apps; `ganji_adabi` leaves the cast (Batch 30).** Characters on it move to `ganji`/`gyro`; it stays selectable; grown-ups' picks are kept; the default changes in code, not in `packs.yml`. (Slice 144)

151. **Sound-word spellings are measured per voice before any book is rewritten (Batch 30).** Until the list exists, sound words use repetition with punctuation. (Slice 145)

152. **Dino books are written in spoken, kid-friendly language in every language (Batch 30).** The way a parent talks to a 4-6-year-old: colloquial forms, diminutives, exclamations; never literary or written-only forms. (Slice 146)

153. **All existing Dino books are rewritten to Decision 152, one PR per book: Farsi first, then English, then the other languages (Batch 30).** (Slice 146)

154. **iOS reviews follow one cadence for every app (Pipeline).** Never cancel a waiting review or expire a build; at most one review waiting per app; always the newest processed (`VALID`, unexpired) build. External TestFlight testers (group "Rofagha"): daily and automatic (`testflight-external.yml`). App Store: a manual button (`appstore-submit.yml`, release type after approval; the first listing is filled in App Store Connect by hand). Internal "Family" testers are unchanged (every merge, no review). Supersedes Decision 70's manual tester-group step. (Slice 147: docs/plan/slices/147-ios-review-cadence.md)

155. **Dino reads at 0.8x by default; grown-ups set 0.5x-1.2x in 0.05 steps, app-wide (Batch 31).** Behind the gate in grown-up settings; flavour pace multiplies on top; RTST's `PlaybackRate` range is unchanged. (Slice 150: docs/plan/slices/150-dino-slower-reading-speed.md)

156. **Dino has three view modes: Picture only, Text only, Picture + text (Batch 31).** Picture books open in Picture + text; an old saved `Picture` becomes Picture + text. The "Words over pictures" switch (Slice 122) and "Merge two characters" (Slice 65) are removed; stored merges are ignored. (Slice 151: docs/plan/slices/151-dino-three-view-modes.md)

157. **A store or starter book character's animal comes from the book's data only, never from its voice or cast character (Batch 31; reverses Decision 132).** Every such character carries an explicit `Animal` (a kind or `none`); recasting never changes the animal or its sound, which is still spoken in the character's voice. The animal is not shown in the UI. Parent-provided books are out of scope and unchanged. (Slice 152: docs/plan/slices/152-dino-animal-from-the-book.md)

158. **Robotic-sounding voices are found by ear (Batch 31).** Claude renders every English roster character to a listening page, the user flags the robotic ones, and the flagged flavours are toned down or the characters moved; un-edited casts are recast. (Slice 153: docs/plan/slices/153-dino-no-robotic-voices.md)

159. **Farsi and Arabic Dino books carry full vowel marks, shown on screen, if a listening test shows the voices read better with them (Batch 31).** A voice that reads worse gets the marks stripped before speaking only. Books are marked after their Slice 146 rewrite and any open PR. (Slice 154: docs/plan/slices/154-dino-farsi-arabic-vowel-marks.md)
