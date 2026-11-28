# CLAUDE.md — ReadTheStupidText

Project guidance, on top of the user's global instructions. Area rules live in folder-scoped
`CLAUDE.md` files (see **Where the rules live**), loaded only when you work in that folder.

## What this is

**ReadTheStupidText** (RTST) is a Windows 11 tray utility that reads selected/copied text aloud; its description, naming and tray build/run are in `src/Rtst/ReadTheStupidText.App/CLAUDE.md`.

**Two more products share this repo.** The same RTST app ships on **Android** (and iOS) via
**.NET MAUI**, reusing `Domain`/`Application` unchanged. **Dino Reader** is a second MAUI app (a
kids' book reader with a voice per character) sharing the Mobile.Shared platform code. Each product has
its own version, tag and GitHub Release: `rtst-v<x.y.z>` and `dino-v<x.y.z>` (Decision 137; the old shared
`v<x.y.z>` tags are history).

Read [`plan.md`](plan.md) before starting work — it indexes the open slices (status, PR, slice file); finished rows
live in `docs/plan/notes/slices-done.md`. Tick a slice's work items off in its own slice file; `plan.md` rows change only
in the `update-docs` index pass. The numbered **Decisions** live in `docs/plan/decisions.md` (1-99 archived in `docs/plan/notes/decisions-001-099.md`; "Decision NN" anywhere in these files refers to it); each slice's full record is under `docs/plan/slices/`. Every slice file carries one `**Product:** RTST | Dino | Shared` line under its title, and its status row goes in the matching `plan.md` table (`### Read The Stupid Text`, `### Dino Reader`, `### Shared`). (Slice 366: docs/plan/slices/366-plan-split-by-product.md)

## Where the rules live

- `src/Rtst/ReadTheStupidText.App/CLAUDE.md` — RTST Windows tray, read paths, Release build.
- `src/Rtst/ReadTheStupidText.Mobile/CLAUDE.md` — RTST MAUI Android/iOS.
- `src/Rtst/ReadTheStupidText.Windows.Tray/CLAUDE.md` — tray-only Win32/UIA code.
- `src/Dino/DinoReader.App/CLAUDE.md` — Dino Reader app; `Platforms/Windows/CLAUDE.md` its Windows head; `Testing/CLAUDE.md` its test hooks.
- `src/Dino/DinoReader.Core/CLAUDE.md` — the pure dialogue library.
- `src/Shared/Sirous.Reading.{Domain,Application,Documents,Infrastructure,Mobile.Shared}/CLAUDE.md` — one file per shared project.
- `tools/CLAUDE.md`, `tools/desktop-vm/CLAUDE.md` — device-test tooling and the Hyper-V VM tests.
- `tests/CLAUDE.md` — the three test projects, what they cover, the release gate.
- `.github/CLAUDE.md` — CI quota rules, release workflows, versioning, licence detail, signing, iOS, packs, APK downloads.
- `worker/CLAUDE.md` — the Cloudflare Worker, `site/`, usage counts, the Dino story store.
- `.claude/skills/create-dino-story/SKILL.md` — making or illustrating a Dino book: repo agents `dino-story-writer` (Opus), `dino-story-illustrator` (Sonnet, **at most two running at once**, JS-only input, one `gemini-lock`) and `dino-story-reviewer` (Opus, after each; its pass is required to ship).
- `.claude/skills/offer-codes-fetch/SKILL.md`, `offer-codes-create` — free subscription codes via repo agent `offer-codes` (Haiku); codes live only in `D:\secrets\dinoreader\offer-codes\`.
- `.claude/skills/farsi-review-book/SKILL.md` — Farsi pronunciation review via repo agent `farsi-review` (Sonnet).
- `.claude/skills/voice-pick/SKILL.md` — listening page for any choice made by ear, via repo agent `voice-pick` (Sonnet).
- Store listing text and screenshots go through the global `store-listing` agent: written and checked against `.claude/skills/store-listing/SKILL.md` (never shown to the user for a yes, 2026-11-15), but pushed to a store only when the owner's own message that turn asks for it; a slice, plan line or night run stops at the draft with the console link. No Play edit commit, review submission or promotion otherwise: a 2026-11-28 night commit cancelled a Dino review that had waited 3 days (ClaudeSkills `home/rules/store-publishing.md`).

## Stack & conventions

- **.NET 11** (SDK 11.0.100, MAUI 11; Slice 379), **C#**, **WinUI 3 / Windows App SDK 2.x** (the exact version is the `Microsoft.WindowsAppSDK` reference in `src/Rtst/ReadTheStupidText.App/ReadTheStupidText.App.csproj`).
- Solution file is `ReadTheStupidText.slnx` (the XML `slnx` format — not `.sln`).
- Always confirm Windows App SDK / WinUI / `H.NotifyIcon` APIs and versions via
  **context7** before writing code against them — versions move fast.
- **sherpa-onnx and onnxruntime are one version on Android, iOS and Windows**, written only in
  `src/Shared/SherpaOnnxVersion.props`; never pin either anywhere else (`check-sherpa-pins.mjs`
  and `SherpaVersionPinTests` fail on a stray pin). 2.x stays out until a non-GPL phonemizer exists. (Slice 245)

## Layered structure (one-direction dependencies)

Shared projects are `Sirous.Reading.{Domain,Application,Documents,Infrastructure,Mobile.Shared}` under `src/Shared/` (the diagram uses short names). Folders are grouped by product (Slice 371): `src/{Shared,Rtst,Dino}/`, the same under `tests/`, per-product tooling in `tools/dino/` and `tools/rtst/`, shared harnesses (`emulator`, `phone`, `desktop-vm`, `crashes`) in `tools/`.

```
App  →  Application  →  Domain
Infrastructure  →  Application / Domain
Windows.Tray    →  Application / Domain   (referenced only by App)
Mobile  →  Application / Domain / Documents
Mobile.Shared (Windows target only)  →  Infrastructure
Documents  →  Application
DinoReader.Core → Application, Domain
```

- **Domain**, **Application**, **Documents** are `net11.0`; **Infrastructure**, **Windows.Tray** and **App** (WinUI single-project MSIX) are `net11.0-windows`. **Mobile** (.NET MAUI, Decision 37) and `src/Dino/DinoReader.App` are the MAUI apps sharing `Mobile.Shared`.

## Build & run

**.NET 11 SDK on this PC:** installed system-wide since 2026-11-18 (11.0.100 in `C:\Program Files\dotnet`, with the MAUI workloads), so a plain `dotnet` resolves `global.json`'s 11.0.100. Never set `DOTNET_ROOT` to a worktree copy or install another copy into a slice worktree. (Slice 406 item 1.)

Build and run commands: RTST tray in `src/Rtst/ReadTheStupidText.App/CLAUDE.md`, RTST Android/iOS in `src/Rtst/ReadTheStupidText.Mobile/CLAUDE.md`, Dino Reader in `src/Dino/DinoReader.App/CLAUDE.md`. The solution is `ReadTheStupidText.slnx`. The Cloudflare Worker, `site/` and usage counts: read `worker/CLAUDE.md` first.

## Tests

xUnit v3, three projects (details in `tests/CLAUDE.md`). A test whose subject only the RTST App, `Windows.Tray` or RTST `Mobile` reaches stays in `ReadTheStupidText.Tests`; anything Dino can reach goes in `Shared.Tests`. (Slice 368)

```bash
dotnet test tests/Shared/Shared.Tests/Shared.Tests.csproj
dotnet test tests/Rtst/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj
dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj
```

- Capture `PackDownloadLog` through `PackDownloadLog.CaptureScope`, never by setting the static `Sink` (process-wide; parallel tests leak into it). (Slice 388)
- A `catch` in `src/` either reports a fallback (a call with a `FallbackArea.` argument) or has `// no-fallback: <reason>` as the first line inside its brace. `FallbackGuardTests` enforces it with a per-file baseline, `tests/Shared/Shared.Tests/FallbackGuardBaseline.txt`, that may only go down: lower a file's number when you mark or instrument a catch. (Slice 273)

## Licence, versions and signing (detail in `.github/CLAUDE.md`)

- **MIT.** No GPL/LGPL components. Only exception: espeak-ng data (GPL-3.0) inside the downloaded `piper-fa`, `piper-nb` and `kokoro-en` packs, never in the repo or a package. CC-BY-SA only for downloaded packs (`translation-en-ja`). Supertonic-3 weights are OpenRAIL-M: keep the pack's `LICENSE-OpenRAIL-M` + `NOTICE`.
- **Every merge to `main` that touches app code ships a version** (GitVersion from tags; docs-only merges cut none). `main` defaults to a patch bump; add `+semver: minor`, `+semver: major` or `+semver: none` in a commit message to change it.
- **CI stays unsigned;** the Microsoft Store re-signs on publish.

## Code quality (project-specific reminders)

- Genuinely closed sets are `enum`s (reading speed is the exception: `PlaybackRate`, see the Domain file); stringify only at the UI boundary.
- No magic strings for settings keys / manifest names — use named constants.
- The backend/infrastructure returns structured data; the UI composes any user-facing copy.
- Every method does one thing; 3–30 lines.
