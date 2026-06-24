# CLAUDE.md — ReadTheStupidText

Project-specific guidance for agents working in this repo. This complements
the user's global instructions (which always apply).

## What this is

**ReadTheStupidText** is a Windows 11 tray utility that reads selected/copied text aloud
at 1x–2x speed. It is a **WinUI 3 + Windows App SDK** desktop app, packaged as
**MSIX** and targeted at the **Microsoft Store**. It is *not* a classic UWP app
(the sandbox cannot do tray icons, global hotkeys, or cross-app text reads).

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
  **Kokoro** via sherpa-onnx, plus a WinRT `SpeechSynthesis` fallback), all
  played through `MediaPlayer`; the neural model downloads on first run.
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
package identity). Note: `Release` builds currently
fail with `NETSDK1102` (`PublishTrimmed` without self-contained) — a scaffold
default to revisit at Store-packaging time (Slice 5); `Debug` is unaffected.

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
settings window (tabs, taskbar presence, hotkey-remap UI), non-Store distribution
as the primary channel, and reading from non-UIA apps without the hotkey
fallback. See `scope.md`.

Note: the narrator voice is a **local neural voice** (Slice 9, Decision 14) — the
**sherpa-onnx** runtime (Apache-2.0) running the **Kokoro** model (Apache-2.0),
via `org.k2fsa.sherpa.onnx`. The model **downloads on first run** from the
sherpa-onnx GitHub release into app-local storage (so `internetClient` is
declared); the picker offers **only** the Kokoro voices (`KokoroVoiceTable`,
modelled as `VoiceInfo` records). The built-in WinRT `SpeechSynthesis` voice is
an internal **fallback only**, used while the model downloads — never offered for
selection. Narrator's "Natural"/neural voices are unreachable by a Store app, so
we bring our own engine. **Piper is GPL — do not use it**; Kokoro/sherpa-onnx are
Apache-2.0 and Store-safe. Note the build-time `onnxruntime.dll` dedupe in the
App `.csproj` (WinML and sherpa both ship one; we keep sherpa's).

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
