# CLAUDE.md — Binders

Project-specific guidance for agents working in this repo. This complements
the user's global instructions (which always apply).

## What this is

**Binders** is a Windows 11 tray utility that reads selected/copied text aloud
at 1x–2x speed. It is a **WinUI 3 + Windows App SDK** desktop app, packaged as
**MSIX** and targeted at the **Microsoft Store**. It is *not* a classic UWP app
(the sandbox cannot do tray icons, global hotkeys, or cross-app text reads).

Read [`plan.md`](plan.md) before starting work — it defines the vertical slices
and their GitHub issue numbers. Tick slices off in `plan.md` as they complete.

## Stack & conventions

- **.NET 10**, **C#**, **WinUI 3 / Windows App SDK 1.8**.
- Solution file is `Binders.slnx` (the XML `slnx` format — not `.sln`).
- Always confirm Windows App SDK / WinUI / `H.NotifyIcon` APIs and versions via
  **context7** before writing code against them — versions move fast.

## Layered structure (one-direction dependencies)

```
App  →  Application  →  Domain
Infrastructure  →  Application / Domain
```

- **Domain** (`net10.0`) — entities, value objects, enums. No framework deps.
  The five speeds are an `enum` (a closed set), never loose strings/doubles.
- **Application** (`net10.0`) — use cases / orchestration, interfaces.
- **Infrastructure** (`net10.0-windows`) — WinRT TTS (`SpeechSynthesis` +
  `MediaPlayer`), clipboard, UI Automation, startup task, OS integration.
- **App** (`net10.0-windows`, WinUI single-project MSIX) — UI, tray, DI wiring.

Keep `App.xaml.cs` thin: provider/DI wiring and window bootstrap only. Real
logic lives in Application/Infrastructure; XAML views stay free of business
logic.

## Build & run

```bash
dotnet build src/Binders.App/Binders.App.csproj -p:Platform=x64
dotnet run   --project src/Binders.App/Binders.App.csproj -p:Platform=x64
```

Builds are **per-architecture** — always pass `-p:Platform=x64` (or `arm64`).
Build the **app project**, not `Binders.slnx`: the class libs are `AnyCPU` and
the app is `x86;x64;ARM64`, so a solution-level `-p:Platform=x64` is an invalid
solution configuration. The app project builds the whole graph.

## Code quality (project-specific reminders)

- Closed sets are `enum`s (speed band, reading state, etc.) — stringify only at
  the UI boundary.
- No magic strings for settings keys / manifest names — use named constants.
- The backend/infrastructure returns structured data; the UI composes any
  user-facing copy.
- Every method does one thing; 3–30 lines.

## Out of scope (v1)

Voice picker / multilingual selection, a full settings window, non-Store
distribution as the primary channel, and reading from non-UIA apps without the
hotkey fallback. See `scope.md`.
