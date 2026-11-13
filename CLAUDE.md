# CLAUDE.md — ReadTheStupidText

Project-specific guidance for agents working in this repo. This complements
the user's global instructions (which always apply). Area rules live in folder-scoped
`CLAUDE.md` files (see **Where the rules live**); they load only when you work in that folder.

## What this is

**ReadTheStupidText** (RTST) is a Windows 11 tray utility that reads selected/copied text aloud; its description, naming and tray build/run are in `src/ReadTheStupidText.App/CLAUDE.md`.

**Two more products share this repo.** The same RTST app ships on **Android** (and iOS) via
**.NET MAUI**, reusing `Domain`/`Application` unchanged. **Dino Reader** is a second MAUI app (a
kids' book reader with a voice per character) sharing the Mobile.Shared platform code. Each product has
its own version, tag and GitHub Release: `rtst-v<x.y.z>` and `dino-v<x.y.z>` (Decision 137; the old shared
`v<x.y.z>` tags are history).

> **Stacked PRs:** open them against `main`, or retarget each one to `main` after its base
> merges; never merge one into another. (Decisions 45-51;
> note: docs/plan/notes/claude-md-history.md)

Read [`plan.md`](plan.md) before starting work — it indexes the open slices (status, PR, slice file); finished rows
live in `docs/plan/notes/slices-done.md`. Tick a slice's work items off in its own slice file; `plan.md` rows change only
in the `update-docs` index pass. The numbered **Decisions** live in `docs/plan/decisions.md` (1-99 archived in `docs/plan/notes/decisions-001-099.md`; "Decision NN" anywhere in these files refers to it); each slice's full record is under `docs/plan/slices/`. Every slice file carries one `**Product:** RTST | Dino | Shared` line under its title, and its status row goes in the matching `plan.md` table (`### Read The Stupid Text`, `### Dino Reader`, `### Shared`). (Slice 366: docs/plan/slices/366-plan-split-by-product.md)

## Where the rules live

Read The Stupid Text:

- `src/ReadTheStupidText.App/CLAUDE.md` — Windows tray, control panel, activity log, read paths, console/UIA limitation, self-contained Release, onnxruntime dedupe.
- `src/ReadTheStupidText.Mobile/CLAUDE.md` — MAUI Android/iOS: namespace and `Application` gotchas, sherpa-onnx native `.so` packaging, voice packs, Scan, emulator and iOS compile notes.

Dino Reader:

- `src/DinoReader.App/CLAUDE.md` — Dino Reader app: icons, the design mock, story, reading and cast rules.
- `src/DinoReader.App/Platforms/Windows/CLAUDE.md` — Dino Reader's Windows head: package identity, key hook, ListViewBase input, Hyper-V VM test.
- `src/DinoReader.App/Testing/CLAUDE.md` — Dino Debug test hooks (`test-store`, `import-test-book`) and Maestro log markers.
- `src/DinoReader.Core/CLAUDE.md` — the pure dialogue library.
- `.claude/skills/create-dino-story/SKILL.md` — making or illustrating a Dino book: repo agents `dino-story-writer` (text, Opus), `dino-story-illustrator` (Gemini pictures, Sonnet, up to four in parallel, JS-only input, one `gemini-lock`) and `dino-story-reviewer` (Opus, after each; its pass is required to ship); the main thread only coordinates.
- `.claude/skills/farsi-review-book/SKILL.md` — the Farsi pronunciation review, one book at a time (Slice 154): run it through the repo agent `farsi-review` (Sonnet); later books prefill the user's earlier answers.
- `.claude/skills/voice-pick/SKILL.md` — a listening page for any choice made by ear (default voices, narrator and character voices, sound words): repo agent `voice-pick` (Sonnet) renders the candidates, waits for the user's picks and writes them into the slice file. Store listing text goes through the global `store-listing` agent (draft, then apply after the user approves).

Shared:

- `src/Sirous.Reading.Application/CLAUDE.md` — shared read path: blank text (`BlankText`), chunking.
- `src/Sirous.Reading.Infrastructure/CLAUDE.md` — Windows speech engines, narrator voice and licence choices, Windows packs and head start, the neural reader.
- `src/Sirous.Reading.Mobile.Shared/CLAUDE.md` — scanner/OCR stack shared by every MAUI app.
- `tools/CLAUDE.md` — device-test tooling: `-App auto`, the build cache, device limits, phone runs.
- `tools/desktop-vm/CLAUDE.md` — the headless Hyper-V VM desktop tests: PowerShell script gotchas.
- `.github/CLAUDE.md` — CI quota rules, `release-rtst.yml`/`release-dino.yml`/`release-shared.yml`/`ship-targets.mjs`, path filters, iOS signing and toolchain, packs releases, APK downloads, actionlint.

## Stack & conventions

- **.NET 10**, **C#**, **WinUI 3 / Windows App SDK 2.x** (the exact version is the `Microsoft.WindowsAppSDK` reference in `src/ReadTheStupidText.App/ReadTheStupidText.App.csproj`).
- Solution file is `ReadTheStupidText.slnx` (the XML `slnx` format — not `.sln`).
- Always confirm Windows App SDK / WinUI / `H.NotifyIcon` APIs and versions via
  **context7** before writing code against them — versions move fast.
- **sherpa-onnx and onnxruntime are one version on Android, iOS and Windows**, written only in
  `src/SherpaOnnxVersion.props`; never pin either anywhere else (`.github/scripts/check-sherpa-pins.mjs`
  and `SherpaVersionPinTests` fail on a stray pin). 2.x stays out until a non-GPL phonemizer exists.
  (Slice 245: docs/plan/slices/245-sherpa-onnx-one-version.md)

## Layered structure (one-direction dependencies)

```
App  →  Application  →  Domain
Infrastructure  →  Application / Domain
Windows.Tray    →  Application / Domain   (referenced only by App)
Mobile  →  Application / Domain / Documents
Mobile.Shared (Windows target only)  →  Infrastructure
Documents  →  Application
DinoReader.Core → Application, Domain
```

- **Domain** (`net10.0`) — entities, value objects, enums. No framework deps.
  Reading speed is the **`PlaybackRate`** value object — a decimal multiplier
  (0.5–2.0, snapped to 0.05 steps), *not* an enum: the user picks a continuous
  rate, with `SpeedPresets` exposing the common stops (1/1.25/1.5/1.75/2) for the
  native tray menu. Genuinely closed sets (reading state, etc.) stay `enum`s.
  Also `SupertonicVoiceTable`/`SupertonicFiles` (Slice 31) — pure voice-catalog
  data, moved here from Infrastructure so Mobile can reuse it too.
- **Application** (`net10.0`) — use cases / orchestration, interfaces. Also
  `LineReflow` (Slice 76, shared by both apps' read paths: `SpeechTextChunker`, Dino
  `StorySentences`/`StorySegmenter`), `SpeechTextChunker`/`ReadTimingTracker` (both `internal`, Slice 31) — pure
  chunking/timing logic shared by every `ISpeechReader` implementation on
  every platform (`InternalsVisibleTo` covers `ReadTheStupidText.Tests`, `Shared.Tests`, `Infrastructure`,
  `Mobile` and `Mobile.Shared`).
- **Documents** (`net10.0`) — `PlainTextExtractor`/`PdfTextExtractor`/
  `DocxTextExtractor`/`CompositeDocumentTextExtractor` (Slice 33). A portable
  library, not Infrastructure or Application: they need PdfPig/
  DocumentFormat.OpenXml (so not Application, which stays library-free) but
  have no Windows-only dependency (so not stuck in Infrastructure) — both
  Infrastructure (Windows) and Mobile (Android) reference this project and
  share the exact same extractors.
- **DinoReader.Core** (`src/DinoReader.Core`) — pure dialogue library, references Application and Domain (details in its own file).
- **Infrastructure** (`net10.0-windows`) — speech engines (local neural
  **Supertonic-3** via sherpa-onnx, plus a WinRT `SpeechSynthesis` fallback),
  all played through `MediaPlayer`; every platform downloads its voice as a pack
  (Slice 50; Windows since Slice 56, no model in the MSIX).
  Clipboard, UI Automation, startup task, OS integration.
- **Windows.Tray** (`src/ReadTheStupidText.Windows.Tray`, `net10.0-windows`, Slice 128, Decision 135) — tray-only
  Win32/UIA code: global hotkey, UIA selection monitor, clipboard listener, selection copier, foreground probe and
  the startup task. Only `App` references it, so a tray edit does not rebuild or ship Dino Reader (which shares
  Infrastructure). Their interfaces (`IHotkeyService`, ...) and the activity log stay in Application/Domain, used by
  `ReadAloudService`. (Slice 128: docs/plan/slices/128-tray-code-out-of-shared-infrastructure.md)
- **App** (`net10.0-windows`, WinUI single-project MSIX) — UI, tray, DI wiring.
- **Mobile** (`src/ReadTheStupidText.Mobile`, .NET MAUI, Decision 37) and **`src/DinoReader.App`** — the
  MAUI apps, sharing `src/Sirous.Reading.Mobile.Shared`; see their own files (Dino Reader's Windows head and its publish command: `src/DinoReader.App/CLAUDE.md`).

## Build & run

Build and run commands: RTST tray in `src/ReadTheStupidText.App/CLAUDE.md`, RTST Android/iOS in `src/ReadTheStupidText.Mobile/CLAUDE.md`, Dino Reader in `src/DinoReader.App/CLAUDE.md`. The solution is `ReadTheStupidText.slnx`.

**Landing site, usage counts, story store, remote config.** The Cloudflare Worker `readthestupidtext` (`wrangler.jsonc`, `worker/`, `site/`) and its rules live in `worker/CLAUDE.md`; read it before touching the Worker, `site/`, usage counts or the Dino story store.

## Tests

There are three test projects (xUnit v3). `tests/ReadTheStupidText.Tests` (RTST-only logic, `net10.0-windows10.0.19041.0`),
`tests/Shared.Tests` (shared engine, Documents, Domain, Application, Mobile.Shared links, and the two repo-wide guards
`FallbackGuardTests` and `SherpaVersionPinTests`; `net10.0-windows10.0.19041.0`) and `tests/DinoReader.Tests`. A test whose
subject only the RTST App, `Windows.Tray` or RTST `Mobile` reaches stays in `ReadTheStupidText.Tests`; anything Dino can
reach goes in `Shared.Tests`. (Slice 368: docs/plan/slices/368-shared-tests-project.md) They cover the **pure** logic only — `PlaybackRate`, `SpeechTextChunker`,
`SupertonicVoiceTable`, `ActivityLog`, and the `LocalSettingsStore` settings
migration (via the pure `ResolveAutoReadFlag`; internals are exposed through
`InternalsVisibleTo`). The WinUI/WinRT/native paths (readers, monitors, windows)
are **not** unit-tested — they need package identity / a UI host; verify those by
running the app (VS **(Package)** profile). Run the suite with:

```bash
dotnet test tests/Shared.Tests/Shared.Tests.csproj
dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj
```

Dino Reader's pure library has its own suite (xUnit v3, plain `net10.0`, runs on any OS):

```bash
dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj
```

The `tests.yml` workflow (called by `release-shared.yml`, once per product run) runs them on every push/PR that
touches app code (docs-only changes skip it) and **gates** the build and release jobs,
so a failing test blocks the release.

## Versioning, license & releases (Batch 2 — Slices 11–16)

- **License is MIT** (Decision 16 in `docs/plan/notes/decisions-001-099.md`). Anyone may use/extend/sell it
  provided the copyright notice is kept. Do **not** add GPL/LGPL components (the
  Supertonic/sherpa stack was chosen to keep this clean — see `STORE.md`). The one
  **scoped exception**: espeak-ng data (GPL-3.0) lives only inside the downloaded `piper-fa`,
  `piper-nb` and `kokoro-en` packs, never in the repo or any package (Decisions 59-71, 108, 119).
  The MIT licence covers the code only: Supertonic-3 weights are BigScience OpenRAIL-M, not
  MIT/Apache-2.0. The `supertonic-3` pack ships `LICENSE-OpenRAIL-M` + `NOTICE` (via `packs.yml`);
  keep both and pass the use restrictions on. Kokoro-82M is Apache-2.0.
  CC-BY-SA is allowed for downloaded packs only (`translation-en-ja`, ElanMT; Decision 171), and the training data's terms are checked as well as the model's label.
  (Slice 114: docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md)
- **Every merge to `main` that touches app code ships a version** (Decision 17;
  docs-only merges cut none, and only the changed platforms are rebuilt and
  submitted — Batch 8), derived by
  **GitVersion** from git history — **git tags are the source of truth**. The
  Each product's release run (`release-rtst.yml`, `release-dino.yml`, both calling `release-shared.yml`; Slice 369) does it all: GitVersion computes the next SemVer, the
  MSIX is packaged with that version stamped into `Package.appxmanifest`
  `Version` (`x.y.z.0` — the Store needs revision `0`) **at build time** (not
  committed), then a `rtst-v<x.y.z>` tag + GitHub Release are cut at the merge commit. **Per
  product (Slice 130, Decision 137):** `GitVersion.yml` (tag prefix `rtst-v`) is Read The Stupid Text's,
  `GitVersion.dino.yml` (`dino-v`) is Dino Reader's; a release carries only its own product's files, so a
  Dino-only change cuts `dino-v…` and no RTST release, and a core change cuts both. Both continue from the
  old shared version (neither number or Android `versionCode` goes backwards). No PAT, no commit-back. **`main` defaults to a patch bump.** To bump higher,
  put a token in a commit message (highest since the last tag wins): **`+semver:
  minor`** for a feature, **`+semver: major`** for a breaking change, `+semver:
  none` to skip. Config: `GitVersion.yml`. So: write a normal commit and add
  `+semver: minor`/`major` when the change warrants it; otherwise it's a patch. A token bumps whichever
  product(s) the run cuts, since both read the same commit messages since their own last tag.
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
- A `catch` in `src/` either reports a fallback (a call with a `FallbackArea.` argument) or has
  `// no-fallback: <reason>` as the first line inside its brace. `FallbackGuardTests` enforces it with a
  per-file baseline, `tests/Shared.Tests/FallbackGuardBaseline.txt`, that may only go down: lower a
  file's number when you mark or instrument a catch. (Slice 273: docs/plan/slices/273-fallback-events-everywhere.md)
