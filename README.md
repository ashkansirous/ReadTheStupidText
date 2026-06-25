# ReadTheStupidText

A lightweight Windows 11 tray utility that reads selected or copied text aloud
at a user-chosen speed (a continuous 0.5×–2.0× slider, plus quick presets), with
a small control beside the clock to pause/resume and change speed and voice.
Built as a WinUI 3 packaged (MSIX) app for distribution through the Microsoft Store.

> Repo name is `ReadTheStupidText`; the product display name is **ReadTheStupidText**.

## Download

[![Latest release](https://img.shields.io/github/v/release/ashkansirous/ReadTheStupidText?sort=semver&label=latest%20release)](https://github.com/ashkansirous/ReadTheStupidText/releases/latest)

Grab the MSIX from the **[latest release](https://github.com/ashkansirous/ReadTheStupidText/releases/latest)** — `…-x64.msix` for most PCs, `…-ARM64.msix` for Arm devices. Each release is produced by CI from a `v*` tag.

> These packages are **unsigned** (the Microsoft Store signs on publish). To sideload one before the Store listing exists, a signed build / trusted certificate is required — see [`STORE.md`](STORE.md). Releases appear once the first `v*` tag is pushed.

## Status

Early development. See [`plan.md`](plan.md) for the vertical-slice roadmap and
[`scope.md`](scope.md) for the high-level scope. Work is tracked as GitHub
issues (one *story* per slice, *tasks* as sub-issues) on the **ReadTheStupidText**
Projects board.

All planned slices are implemented: hotkey/auto-read at a continuous 0.5–2.0×
speed, a left-click tray control panel, launch-at-startup, local **neural voices**
(sherpa-onnx + Supertonic-3, bundled), and CI packaging. Remaining before a Store
release: Partner Center identity + signing (see [`STORE.md`](STORE.md)).

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

## License

TBD.
