# CLAUDE.md — ReadTheStupidText

Project-specific guidance for agents working in this repo. This complements
the user's global instructions (which always apply).

## What this is

**ReadTheStupidText** is a Windows 11 tray utility that reads selected/copied text aloud
at 1x–2x speed. It is a **WinUI 3 + Windows App SDK** desktop app, packaged as
**MSIX** and targeted at the **Microsoft Store**. It is *not* a classic UWP app
(the sandbox cannot do tray icons, global hotkeys, or cross-app text reads).

> **Naming:** the user-facing **product display name is "Read The Stupid Text"** (with
> spaces) — shown in the manifest `DisplayName`s, tray tooltip, control-panel header,
> and window titles. The **repo, package id (`ReadTheStupidText`), namespaces, assembly,
> and the StartupTask `TaskId`** are internal identifiers and stay `ReadTheStupidText`
> — do not "rename" those. Store identity (ID `9NGT1BN1H92V`) is wired into
> `Package.appxmanifest`; see Decision 23 in `plan.md` and `STORE.md`.

Read [`plan.md`](plan.md) before starting work — it defines the vertical slices
and their GitHub issue numbers. Tick slices off in `plan.md` as they complete.

## Stack & conventions

- **.NET 10**, **C#**, **WinUI 3 / Windows App SDK 1.8**.
- Solution file is `ReadTheStupidText.slnx` (the XML `slnx` format — not `.sln`).
- Always confirm Windows App SDK / WinUI / `H.NotifyIcon` APIs and versions via
  **context7** before writing code against them — versions move fast.

## Layered structure (one-direction dependencies)

```
App  →  Application  →  Domain
Infrastructure  →  Application / Domain
```

- **Domain** (`net10.0`) — entities, value objects, enums. No framework deps.
  Reading speed is the **`PlaybackRate`** value object — a decimal multiplier
  (0.5–2.0, snapped to 0.05 steps), *not* an enum: the user picks a continuous
  rate, with `SpeedPresets` exposing the common stops (1/1.25/1.5/1.75/2) for the
  native tray menu. Genuinely closed sets (reading state, etc.) stay `enum`s.
- **Application** (`net10.0`) — use cases / orchestration, interfaces.
- **Infrastructure** (`net10.0-windows`) — speech engines (local neural
  **Supertonic-3** via sherpa-onnx, plus a WinRT `SpeechSynthesis` fallback),
  all played through `MediaPlayer`; the neural model ships in the package.
  Clipboard, UI Automation, startup task, OS integration.
- **App** (`net10.0-windows`, WinUI single-project MSIX) — UI, tray, DI wiring.

Keep `App.xaml.cs` thin: provider/DI wiring and window bootstrap only. Real
logic lives in Application/Infrastructure; XAML views stay free of business
logic.

## Build & run

```bash
dotnet build src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64
dotnet run   --project src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64
```

Builds are **per-architecture** — always pass `-p:Platform=x64` (or `arm64`).
Building the **app project** is the quickest way to build the whole graph.

The solution (`ReadTheStupidText.slnx`) also builds directly — e.g.
`dotnet build ReadTheStupidText.slnx -p:Platform=x64` — and opens in Visual
Studio. The class libraries are `AnyCPU` and the app is `x86;x64;ARM64`; the
`.slnx` bridges the gap with explicit `<Configurations>` (platforms only, no
`Any CPU`) and a per-app `<Platform Solution="Debug|x64" Project="x64" />`
mapping, plus a `<Deploy Solution="Debug|x64" />` rule so F5 deploys the
package. Debug through the **(Package)** profile — there is no unpackaged
profile, because running unpackaged fails with `REGDB_E_CLASSNOTREG` (no
package identity).

CI/packaging (Slice 5): `.github/workflows/build.yml` packages the single-project
MSIX (x64 + ARM64, unsigned artifacts — the Store re-signs) and checks out LFS for
the voice model; see `STORE.md` for capability justification, licenses, and the
remaining Partner Center steps. `Release` builds cleanly now — trimming is
disabled (`PublishTrimmed=false`); packaged WinUI apps aren't trimmed, and
`PublishTrimmed` without self-contained was the old `NETSDK1102` failure.

## Tests

Unit tests live in `tests/ReadTheStupidText.Tests` (xUnit v3, `net10.0-windows`),
covering the **pure** logic only — `PlaybackRate`, `SpeechTextChunker`,
`SupertonicVoiceTable`, `ActivityLog`, and the `LocalSettingsStore` settings
migration (via the pure `ResolveAutoReadFlag`; internals are exposed through
`InternalsVisibleTo`). The WinUI/WinRT/native paths (readers, monitors, windows)
are **not** unit-tested — they need package identity / a UI host; verify those by
running the app (VS **(Package)** profile). Run the suite with:

```bash
dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj
```

A CI `test` job in `build.yml` runs them on every push/PR and **gates** the
`build`/`release` jobs, so a failing test blocks the release.

## Versioning, license & releases (Batch 2 — Slices 11–16)

- **License is MIT** (Decision 16 in `plan.md`). Anyone may use/extend/sell it
  provided the copyright notice is kept. Do **not** add GPL/LGPL components (the
  Supertonic/sherpa stack was chosen to keep this clean — see `STORE.md`).
- **Every merge to `main` ships a version** (Decision 17), derived by
  **GitVersion** from git history — **git tags are the source of truth**. The
  single `build.yml` run does it all: GitVersion computes the next SemVer, the
  MSIX is packaged with that version stamped into `Package.appxmanifest`
  `Version` (`x.y.z.0` — the Store needs revision `0`) **at build time** (not
  committed), then a `v<x.y.z>` tag + GitHub Release are cut at the merge commit.
  No PAT, no commit-back. **`main` defaults to a patch bump.** To bump higher,
  put a token in a commit message (highest since the last tag wins): **`+semver:
  minor`** for a feature, **`+semver: major`** for a breaking change, `+semver:
  none` to skip. Config: `GitVersion.yml`. So: write a normal commit and add
  `+semver: minor`/`major` when the change warrants it; otherwise it's a patch.
- **Signing:** CI stays **unsigned**; the Microsoft Store re-signs on publish
  (Decision 18). A domain (e.g. `sirous.uk`) cannot sign code. **Azure Trusted
  Signing** is the documented upgrade for trusted sideloaded MSIX — see
  `STORE.md`.

## Code quality (project-specific reminders)

- Genuinely closed sets (reading state, etc.) are `enum`s — stringify only at
  the UI boundary. Reading speed is **not** closed: it's the `PlaybackRate`
  value object (decimal, range/step enforced in the type).
- No magic strings for settings keys / manifest names — use named constants.
- The backend/infrastructure returns structured data; the UI composes any
  user-facing copy.
- Every method does one thing; 3–30 lines.

## Out of scope (v1)

Voice *tuning* beyond playback rate (pitch/volume/SSML), a **persistent/dockable**
settings window (tabs, taskbar presence, hotkey-remap UI), and non-Store
distribution as the primary channel. See `scope.md`.

**Console / non-UIA limitation (by design, not a bug).** The console (Windows
Terminal, PowerShell, `cmd`) and some editors (e.g. Visual Studio's code editor)
expose **no UI Automation text selection**, so *selecting* text in them raises no
OS event — there is nothing to auto-read on. Reading from them therefore requires a
**copy** (auto-read-on-copy via the clipboard listener, gated by the **Auto-read
on copy** toggle) or the **global hotkey** (`Ctrl+Win+R`, copies then reads). Windows
Terminal's `copyOnSelect` makes selecting auto-copy, which then triggers auto-read.
Detecting a bare console *selection* is **out of scope** — Windows provides no API
for it. When a request asks to "read on selection in the terminal," the answer is
copy / `copyOnSelect` / hotkey, not a new selection-detection mechanism.

Note: the narrator voice is a **local neural voice** (Slice 9, Decision 14) — the
**sherpa-onnx** runtime (Apache-2.0) running the **Supertonic-3** model
(Apache-2.0, `sherpa-onnx-supertonic-3-tts-int8-2026-05-11`), via
`org.k2fsa.sherpa.onnx`. The model (~145 MB) **ships inside the package** under
`VoiceModel/` (committed to the repo, read from `AppContext.BaseDirectory`) — no
download, no network, no `internetClient`; the picker offers **only** the
Supertonic voices (`SupertonicVoiceTable`, the fixed 10-style F1–F5/M1–M5 set in
sorted sid order, modelled as `VoiceInfo` records). The built-in WinRT
`SpeechSynthesis` voice is an internal **safety-net fallback only** (if the
packaged files are missing) — never offered for selection. Narrator's
"Natural"/neural voices are unreachable by a Store app, so we bring our own
engine. **Piper is GPL — do not use it; Kokoro was rejected** (Chinese-focused,
no English male voice in the latest, ships GPL-adjacent espeak data); Supertonic
is English-first, Apache-2.0, espeak-free, and Store-safe. Note the build-time
`onnxruntime.dll` dedupe in the App `.csproj` (WinML and sherpa both ship one; we
keep sherpa's).

Note: a **left-click tray control panel** is **in scope** (Slice 8, see
Decision 12 in `plan.md`) — a borderless, always-on-top `AppWindow`, **pinned
above all windows** (it stays open until the ✕ or another tray click; it does
*not* light-dismiss), sized to its content. It is *not* a persistent settings
window: it's transient and every control maps to an existing service. The
right-click `MenuFlyout` stays (Quit lives there only). Rich controls (slider,
`ComboBox`) **cannot** go in H.NotifyIcon's `PopupMenu` — that's why the panel
is a real window, not a flyout (same native-menu limit as Decision 11).

As of **Slice 13 (Decisions 20–21)** the panel is the **"Media Card"** design
(`design_handoff_tray_panel/`): a brand-gradient header (glyph watermark, `NOW
READING` eyebrow, title, a live 5-bar **waveform** + dynamic status text, and a
transport row = 40px play/pause circle + **live progress bar** + speed pill) over
a compact Fluent body and a `Ctrl+Win+R` hotkey footer. **Compact rev (post-Slice
13):** the body no longer stacks full-width labelled rows — the **fine 0.05 speed
slider is hidden in the header** and only revealed when the speed pill (now a
`ToggleButton` carrying a flipping chevron) is tapped (the panel re-fits its height
on reveal). **Design refresh (post-Slice 13):** revealing the speed control now
shows the slider **plus a row of six quick-preset chips** (0.5x / 1x / 1.25x / 1.5x
/ 1.75x / 2x, `SpeedPresetStyle`); tapping a chip drives the slider, and the chip
matching the current rate is highlighted (solid-white fill + blue text) from code
(`ApplyPresetVisual`/`UpdatePresetHighlight`) — slider and chips stay in sync. The
body is a single **voice picker** row (the `ComboBox` now **stretches full width** +
a 34px **activity-log button** that opens `ActivityLogWindow` via the panel's
`ActivityLogRequested` event) above a `CONTROLS` row of **three square icon
toggles** — auto-read on selection, auto-read on copy, launch-at-startup — each a
styled `ToggleButton` (off = card fill + muted icon, on = accent fill + white icon
via its `CheckStates`), now **70px tall (radius 11)** with **larger stroked line
icons**, and a hover tooltip naming the control and its on/off state. The toggle
icons are **stroked `Path` shapes** (the design's source SVGs: a select-frame and
stacked sheets, each enclosing a two-wave sound glyph; launch-at-startup is a
**rocket** — Segoe Fluent has no rocket glyph, and the design deliberately avoids
the power symbol); their stroke colour is the toggle's icon colour, set in code via
`ApplyToggleVisual`. Built with
native WinUI Fluent controls + light/dark `ThemeDictionaries` from the design
tokens (the HTML is the visual source of truth, not shipped code); it **keeps** the
pinned-topmost behavior (no click-away/Esc dismiss). The progress bar is driven by
`ISpeechReader.ProgressChanged` (a 0..1 read-through fraction from `MediaPlayer`
position + chunk index, weighting chunks equally) and is **display-only** — true
scrubbing/seek is out of scope (Decision 21); the status line is composed in the
View from `ReadAloudService.CurrentReadWindow`/`CurrentReadTrigger`.

Note: read activity flows through **`IActivityLog`** (Application — an in-memory,
observable ring buffer; `ActivityEntry`/`ActivityState`/`ActivityTrigger`/
`WindowSource` in Domain — plus **`ActivityReason`** for *why* an entry deviated).
`ReadAloudService` opens an entry per intercepted text and transitions it
(pending→**generatingAudio**→reading→read, or ignored/interrupted/failed), tagging
each deviation with an `ActivityReason` (`NewSelection`/`Deselected`/`Error`) shown
in the log's **Reason** column. `GeneratingAudio` ("Generating audio") covers the
synthesis wait before any audio plays and flips to `reading` on the reader's first
`Playing` transition. Each entry also records its **`ActivityTrigger`** (auto-read /
hotkey / manual / **clipboard**) in the **Trigger** column and the originating
foreground window (`WindowSource` = app + title, via `IForegroundWindow`) in the
**Source** column. `ActivityLogWindow` (a normal resizable window, opened from the
right-click tray menu) renders it live.

Note: alongside the in-memory log, the app writes **two daily diagnostic files to
disk** (Slice 21, Decision 27) under the package **TemporaryFolder** (`logs\`): a
**system log** (`system-YYYYMMDD.log`) via **Serilog** (rolling file, Info/Debug/
Warning/Error, fixed levels — not user-configurable) carrying every id-correlated
action and exception, and an **input log** (`input-YYYYMMDD.log`) — a TSV the
`ActivityInputLog` writer appends **one row per activity-state transition** to
(append-only; it never rewrites a row), columns = the Activity-Log grid plus the
entry **id**, so the two files **join on the id**. Both store **redacted** text only
(the Slice 20 sanitizer runs first); raw text never touches disk. Serilog's rolling
sink stamps the day as `YYYYMMDD` and can't prefix it, so the files are
`system-…`/`input-…` rather than the `yyyy-MM-dd-…` order in Decision 27. The
abstractions are **`ISystemLog`** + **`ILogFolder`** (Application); `LogPaths`
(resolves the folder + a **7-day** startup retention sweep), `SerilogSystemLog`,
`ActivityInputLog` and the pure `InputLogRow` formatter live in Infrastructure.
`ActivityLogWindow` has an **Open logs** button (`Launcher.LaunchFolderAsync`). The
former `UiaSelectionMonitor` `Debug.WriteLine` traces now go through `ISystemLog`.
This is local-only (consistent with the "we collect nothing" stance) — no network,
no telemetry export.

**Three auto-read/read paths feed the log:** the **UIA selection monitor**
(`ISelectionMonitor`, gated by the **Auto-read on selection** toggle), the
**clipboard monitor** (`IClipboardMonitor` = `ClipboardFormatListener`, a Win32
`WM_CLIPBOARDUPDATE` listener on the tray window's handle — the path for the
**console**/Windows Terminal and other apps with no UIA text selection; gated by
the **Auto-read on copy** toggle), and the global **hotkey** (always on). The two
auto-read gates are independent `ISettingsStore` flags (`AutoReadOnSelection` /
`AutoReadOnCopy`, both default on; an old single `IsEnabled=false` migrates to
both off — Decision 22), each surfaced as a `ToggleSwitch` in the control panel
and the right-click menu. `ReadAloudService` de-dupes across them via
`_lastTriggeredText` so the hotkey's own copy (clipboard echo) and a
copy-on-select duplicate of a UIA selection don't read twice. The UIA monitor
emits **`SelectionCleared`** only on a *genuine* empty selection (deselect) so an
in-progress read is interrupted — a **transient cross-process read failure** is
kept distinct (`SelectionOutcome.Unavailable`) and left silent, so a selection
event re-fired mid-selection no longer falsely ignores/interrupts the read. The
log is the diagnostic for "selection does nothing": no entry ⇒ no UIA selection
event *and* nothing copied (with auto-read on, copying in the console now reads;
the hotkey is the always-available fallback). See Slice 10 / Decision 15 in
`plan.md`.

The neural reader (`SupertonicSpeechReader`) **chunks** long text
(`SpeechTextChunker`, paragraph→sentence→word) and synthesizes the chunks
**concurrently** while playing them **in order**, so a long read starts
speaking after the first chunk. The **first** chunk is biased toward a single
sentence (Slice 18, Decision 25) so time-to-first-audio is a short synthesis, not a
whole paragraph; later chunks keep the ~200-char paragraph→sentence→word split. A
superseded/stopped read is torn down via a
**generation counter + `CancellationToken`** (`ISpeechReader.Stop()`), so stale
synthesis can never reach the shared `MediaPlayer`; `read` is marked only on the
reader's **`Completed`** (natural end), never on a stop-induced idle.

**Latency instrumentation + adaptive threading (Slice 22, Decision 30).** The reader
takes `ISystemLog` and writes per-read Debug lines so a slow read is attributable:
`split … into K chunk(s) in X ms (threads T, concurrency C)`, a per-chunk
`chunk i/K (n chars): generate X ms, wav Y ms`, and a one-shot `first audio after X
ms` (on the first chunk's `MediaOpened`). Each is stamped with the activity-log
**id**, threaded in via a new optional `ISpeechReader.SpeakAsync(text, activityId)`
param (the WinRT fallback ignores it), so the lines join the input log. The sherpa-onnx
knobs are **adaptive to `Environment.ProcessorCount`** (no longer fixed at threads 2 /
degree 3): `SynthesisThreads = clamp(cores/2, 2, 4)` (`config.Model.NumThreads`,
confirmed via context7 `/k2-fsa/sherpa-onnx`) and `MaxSynthesisConcurrency =
clamp(cores/threads, 2, 4)` — latency-first (more ONNX threads shorten the single
first-chunk synthesis the user waits on), sized so `threads * concurrency` fits the
cores without oversubscribing.

To kill the cold-start stall on the **first** read (Slice 17, Decision 24), the
engine is **warmed at startup**: once `IVoiceModelService` locates the model,
`ReadAloudService` calls `ISpeechReader.WarmUpAsync()`, which (on the neural
reader) builds the `OfflineTts` and runs one **discarded** tiny synthesis on a
background thread to JIT the ONNX graph. The lazy `EnsureTts()` stays as the
safety net for a read that arrives before warm-up finishes; the build is
double-checked under a lock so the eager warm-up and a first read can't each load
the ~145 MB model. The WinRT fallback's `WarmUpAsync()` is a no-op.

Auto-read uses an **adaptive settle** (Slice 18, Decision 25) rather than a flat
delay: a lone select (click/double-click) reads after a short **150 ms** baseline,
but while events keep arriving within `BurstGapMs` (a live drag) each one extends
the wait to **500 ms**, so a drag still collapses to one read. The
swapped-`_selectionCts` supersede makes any late extra read harmless. Both auto-read
paths (UIA selection + clipboard copy) share this debounce in `ReadAloudService`.

**Local-only timing diagnostics (Slice 19, Decision 26).** Each `ActivityEntry`
carries two nullable diagnostics — **`TimeToFirstAudio`** (entry created → the
reader's first `Playing` transition) and **`SynthesisDuration`** (GeneratingAudio →
first `Playing`, the metric Slice 17's warm-up shrinks). `ReadAloudService` measures
them with monotonic `Stopwatch` timestamps and calls `IActivityLog.RecordTiming` at
the first-Playing transition; `ActivityLogWindow` shows them as the **First audio**
and **Synth** columns (`TimingText` formats ms/s, blank until measured). This stays
**in-memory, cleared on restart, never transmitted** — no third-party SDK, no
network, no privacy-policy change (consistent with the "we collect nothing" stance).
For *dev-time* tuning only, you may optionally instrument the read pipeline with
**OpenTelemetry** `Activity`/`Meter` and attach a **local Aspire dashboard** (an OTLP
viewer run on the dev machine) — **not shipped** in the MSIX; .NET Aspire was
rejected as a redistributable mechanism (it orchestrates *distributed* apps at dev
time and would reintroduce telemetry-export/cost concerns). See `STORE.md`.
