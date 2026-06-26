# Read The Stupid Text

A lightweight Windows 11 tray utility that reads selected or copied text aloud
at a user-chosen speed (a continuous 0.5×–2.0× slider, plus quick presets), with
a small control beside the clock to pause/resume and change speed and voice.
Built as a WinUI 3 packaged (MSIX) app for distribution through the Microsoft Store.

> Repo / package id is `ReadTheStupidText`; the product display name is **Read The Stupid Text** (Microsoft Store ID `9NGT1BN1H92V`).

## Download

[![Get it from the Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Read%20The%20Stupid%20Text-0078D4?logo=microsoft-store&logoColor=white)](https://apps.microsoft.com/detail/9NGT1BN1H92V)
[![Latest release](https://img.shields.io/github/v/release/ashkansirous/ReadTheStupidText?sort=semver&label=latest%20release)](https://github.com/ashkansirous/ReadTheStupidText/releases/latest)

**[Microsoft Store →](https://apps.microsoft.com/detail/9NGT1BN1H92V)** is the recommended install (the Store signs and updates it for you). The GitHub release MSIX below is for testing/sideload.

Grab the MSIX from the **[latest release](https://github.com/ashkansirous/ReadTheStupidText/releases/latest)** — `…-x64.msix` for most PCs, `…-ARM64.msix` for Arm devices. Each release is produced by CI from a `v*` tag.

> These packages are **unsigned** (the Microsoft Store signs on publish). To sideload one before the Store listing is live, a signed build / trusted certificate is required — see [`STORE.md`](STORE.md). Every merge to `main` cuts a new `v*` release automatically.

## Status

Early development. See [`plan.md`](plan.md) for the vertical-slice roadmap and
[`scope.md`](scope.md) for the high-level scope. Work is tracked as GitHub
issues (one *story* per slice, *tasks* as sub-issues) on the **ReadTheStupidText**
Projects board.

All planned slices are implemented: hotkey/auto-read at a continuous 0.5–2.0×
speed, a left-click tray control panel, launch-at-startup, local **neural voices**
(sherpa-onnx + Supertonic-3, bundled), and CI packaging. Remaining before a Store
release: Partner Center identity + signing (see [`STORE.md`](STORE.md)).

## Reading from the console & other apps (important)

There are three ways text reaches the reader, and which ones work depends on the
app you're reading from:

| Trigger | When it fires | Works in |
| --- | --- | --- |
| **Auto-read on selection** | You select text (no copy needed) | Apps that expose **UI Automation** text — most browsers (Chrome/Edge), Word, Notepad, many WinUI/WPF apps |
| **Auto-read on copy** | You copy text (Ctrl+C) while auto-read is on | **Any** app, including the console |
| **Global hotkey `Ctrl+Win+R`** | You press the hotkey | **Any** app — always available |

**Console / terminal limitation.** The **console** (Windows Terminal, PowerShell,
`cmd`) and some editors (e.g. Visual Studio's code editor) **do not expose a text
selection to Windows**, so *selecting* text in them triggers nothing — there is no
OS signal to react to. This is a Windows limitation, not a bug. To read from the
console, do one of:

1. **Copy** the text (Ctrl+C, or right-click → Copy) — with auto-read on, the copy
   is read aloud. **Best for reading Claude Code output in Windows Terminal.**
2. Enable **"Automatically copy selection to clipboard"** in Windows Terminal
   (Settings → Interaction, `copyOnSelect`). Then merely *selecting* text copies it,
   so it's read automatically.
3. Press the **global hotkey `Ctrl+Win+R`**, which copies the focused selection and
   reads it regardless of the app.

Notes:
- Auto-read on copy (and on selection) is gated by the **Auto-read** toggle (tray
  menu / control panel). With auto-read **on**, *any* copy is read — including text
  you copied only to paste elsewhere.
- The **activity log** (right-click tray → *Show activity log*) is the diagnostic:
  if a read produces no row, the app gave no signal (use copy or the hotkey).

## Tech stack

- **.NET 10** + **C#**
- **WinUI 3** / **Windows App SDK 1.8** (packaged, single-project MSIX)
- WinRT `Windows.Media.SpeechSynthesis` + `MediaPlayer` for text-to-speech
- `H.NotifyIcon.WinUI` for the notification-area (tray) icon *(added in a later slice)*

## Project structure

```
ReadTheStupidText.slnx
src/
  ReadTheStupidText.App/             WinUI 3 single-project MSIX app (UI, DI wiring)
  ReadTheStupidText.Application/     Use cases / orchestration (net10.0)
  ReadTheStupidText.Domain/          Entities, value objects, enums — no framework deps (net10.0)
  ReadTheStupidText.Infrastructure/  TTS, clipboard, UIA, OS integration (net10.0-windows)
```

Dependency direction is one-way: `App → Application → Domain` and
`Infrastructure → Application/Domain`.

## Prerequisites

- Windows 11
- .NET 10 SDK
- Windows App SDK 1.8 (restored via NuGet)

The WinUI project template comes from the
`Microsoft.WindowsAppSDK.WinUI.CSharp.Templates` pack:

```bash
dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
```

## Build & run

```bash
# Build the app (its project references pull in and build the class libraries)
dotnet build src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64

# Run the app
dotnet run --project src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64
```

> Build the **app project**, not `ReadTheStupidText.slnx`. The class libraries are
> `AnyCPU` while the WinUI app is `x86;x64;ARM64`, so a solution-level
> `-p:Platform=x64` is rejected as an invalid solution configuration. Building
> the app project builds the whole dependency graph with correct per-project
> platform mapping. Always pass `-p:Platform=x64` (or `arm64`).

The neural voice model is large and stored in **Git LFS**, so install
[Git LFS](https://git-lfs.com/) (`git lfs install`) before cloning, or run
`git lfs pull` after clone, so `src/ReadTheStupidText.App/VoiceModel/` holds the
real model files (not pointers).

## Continuous integration

`.github/workflows/build.yml` packages the MSIX (x64 + ARM64) on every push/PR to
`main` and uploads each as an unsigned artifact. See [`STORE.md`](STORE.md) for
packaging, capability justification, third-party licenses, and the Store
submission steps.

## Privacy

Read The Stupid Text collects **no data** — no accounts, no telemetry, no network
calls. Everything runs locally and offline. See [`PRIVACY.md`](PRIVACY.md).

## License

[MIT](LICENSE) © Ashkan Sirous. You may use, modify, and redistribute this
software — including commercially — provided the copyright and permission
notice are kept.
