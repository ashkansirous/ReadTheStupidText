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
**copy** (auto-read-on-copy via the clipboard listener, gated by the auto-read
toggle) or the **global hotkey** (`Ctrl+Win+R`, copies then reads). Windows
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

Note: a **left-click tray control panel** is now **in scope** (Slice 8, see
Decision 12 in `plan.md`) — a borderless, always-on-top `AppWindow`, **pinned
above all windows** (it stays open until the ✕ or another tray click; it does
*not* light-dismiss), sized to its content, holding the fine 0.05 speed slider,
voice picker, play/pause, and the auto-read/startup toggles. It is *not* a
persistent settings window: it's transient and every control maps to an
existing service. The
right-click `MenuFlyout` stays (Quit lives there only). Rich controls (slider,
`ComboBox`) **cannot** go in H.NotifyIcon's `PopupMenu` — that's why the panel
is a real window, not a flyout (same native-menu limit as Decision 11).

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

**Three auto-read/read paths feed the log:** the **UIA selection monitor**
(`ISelectionMonitor`, gated by auto-read), the **clipboard monitor**
(`IClipboardMonitor` = `ClipboardFormatListener`, a Win32 `WM_CLIPBOARDUPDATE`
listener on the tray window's handle — the path for the **console**/Windows
Terminal and other apps with no UIA text selection; gated by auto-read), and the
global **hotkey** (always on). `ReadAloudService` de-dupes across them via
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
**concurrently (degree 3)** while playing them **in order**, so a long read starts
speaking after the first chunk. A superseded/stopped read is torn down via a
**generation counter + `CancellationToken`** (`ISpeechReader.Stop()`), so stale
synthesis can never reach the shared `MediaPlayer`; `read` is marked only on the
reader's **`Completed`** (natural end), never on a stop-induced idle.
