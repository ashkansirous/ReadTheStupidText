# CLAUDE.md — ReadTheStupidText

Project-specific guidance for agents working in this repo. This complements
the user's global instructions (which always apply). Area rules live in folder-scoped
`CLAUDE.md` files (see **Where the rules live**); they load only when you work in that folder.

## What this is

**ReadTheStupidText** is a Windows 11 tray utility that reads selected/copied text aloud
at 1x–2x speed. It is a **WinUI 3 + Windows App SDK** desktop app, packaged as
**MSIX** and targeted at the **Microsoft Store**. It is *not* a classic UWP app
(the sandbox cannot do tray icons, global hotkeys, or cross-app text reads).

**Two more products share this repo.** The same RTST app ships on **Android** (and iOS) via
**.NET MAUI**, reusing `Domain`/`Application` unchanged. **Dino Reader** is a second MAUI app (a
kids' book reader with a voice per character) sharing the Mobile.Shared platform code. Each product has
its own version, tag and GitHub Release: `rtst-v<x.y.z>` and `dino-v<x.y.z>` (Decision 137; the old shared
`v<x.y.z>` tags are history).

> **Stacked PRs:** open them against `main`, or retarget each one after its base
> merges (PRs #154-#156 once merged into each other instead). (Decisions 45-51;
> note: docs/plan/notes/claude-md-history.md)

> **Naming:** the user-facing **product display name is "Read The Stupid Text"** (with
> spaces) — shown in the manifest `DisplayName`s, tray tooltip, control-panel header,
> and window titles. The **repo, package id (`ReadTheStupidText`), namespaces, assembly,
> and the StartupTask `TaskId`** are internal identifiers and stay `ReadTheStupidText`
> — do not "rename" those. Store identity (ID `9NGT1BN1H92V`) is wired into
> `Package.appxmanifest`; see Decision 23 in `docs/plan/decisions.md` and `STORE.md`.

> **Naming:** the user-facing product display name is **"Dino Book Reader"** (manifest, store listing,
> app title, UI strings, site). Package ids (`uk.sirous.dinoreader`, `AshkanSirous.DinoReader`), namespaces,
> projects, the `.dino` format and the domain `dinoreader.sirous.uk` stay "DinoReader": do not rename those.

Read [`plan.md`](plan.md) before starting work — it defines the vertical slices
and their GitHub issue numbers. Tick slices off in `plan.md` as they complete. The numbered **Decisions** live in `docs/plan/decisions.md` ("Decision NN" anywhere in these files refers to it); each slice's full record is under `docs/plan/slices/`.

## Where the rules live

- `src/ReadTheStupidText.App/CLAUDE.md` — Windows tray, control panel, activity log, read paths, console/UIA limitation, self-contained Release, onnxruntime dedupe.
- `src/ReadTheStupidText.Infrastructure/CLAUDE.md` — Windows speech engines, narrator voice and licence choices, Windows packs and head start, the neural reader.
- `src/ReadTheStupidText.Mobile/CLAUDE.md` — MAUI Android/iOS: namespace and `Application` gotchas, sherpa-onnx native `.so` packaging, voice packs, Scan, emulator and iOS compile notes.
- `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md` — scanner/OCR stack shared by every MAUI app.
- `src/DinoReader.App/CLAUDE.md` — Dino Reader app: icons, the design mock, story, reading and cast rules.
- `src/DinoReader.Core/CLAUDE.md` — the pure dialogue library.
- `.claude/skills/create-dino-story/SKILL.md` — making or illustrating a Dino book: repo agents `dino-story-writer` (text, Opus), `dino-story-illustrator` (Gemini pictures, Sonnet, up to four in parallel, JS-only input, one `gemini-lock`) and `dino-story-reviewer` (Opus, after each; its pass is required to ship); the main thread only coordinates.
- `.github/CLAUDE.md` — CI quota rules, `release.yml`/`ship-targets.mjs`, path filters, iOS signing and toolchain, packs releases, APK downloads, actionlint.

## Stack & conventions

- **.NET 10**, **C#**, **WinUI 3 / Windows App SDK 1.8**.
- Solution file is `ReadTheStupidText.slnx` (the XML `slnx` format — not `.sln`).
- Always confirm Windows App SDK / WinUI / `H.NotifyIcon` APIs and versions via
  **context7** before writing code against them — versions move fast.

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
  every platform (`InternalsVisibleTo` covers `Tests`, `Infrastructure`, and
  `Mobile`).
- **Documents** (`net10.0`) — `PlainTextExtractor`/`PdfTextExtractor`/
  `DocxTextExtractor`/`CompositeDocumentTextExtractor` (Slice 33). A portable
  library, not Infrastructure or Application: they need PdfPig/
  DocumentFormat.OpenXml (so not Application, which stays library-free) but
  have no Windows-only dependency (so not stuck in Infrastructure) — both
  Infrastructure (Windows) and Mobile (Android) reference this project and
  share the exact same extractors.
- **DinoReader.Core** (`src/DinoReader.Core`) — pure dialogue library, references Application only for `LineReflow` (details in its own file).
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
  MAUI apps, sharing `src/ReadTheStupidText.Mobile.Shared`; see their own files. Dino Reader also has a
  **Windows head** (Slice 67, MSIX, no tray or hotkey): Mobile.Shared's Windows target references
  `Infrastructure` for the Windows speech engines. Package it with
  `dotnet publish src/DinoReader.App -f net10.0-windows10.0.19041.0 -c Release -p:IncludeAndroid=false -p:Platform=x64`
  (never a global `RuntimeIdentifier`; rules in `src/DinoReader.App/CLAUDE.md` and `.github/CLAUDE.md`).

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

Mobile (Android): `dotnet build src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj -f net10.0-android`.
On a Windows host Mobile.Shared also restores its Windows target (needs the `maui-windows` workload);
where only `maui-android` is installed add `-p:IncludeWindows=false` (CI's Android and iOS jobs run on
Linux/macOS, which have no Windows target). iOS from Windows: `-p:IncludeIos=true -p:IncludeWindows=false
-p:IncludeAndroid=false` (emulator and iOS notes in the Mobile file). CI and packaging: `.github/CLAUDE.md`; Store details: `STORE.md`.

**Landing site.** `readthestupidtext.sirous.uk` is the Cloudflare Worker `readthestupidtext`
(`wrangler.jsonc`, `worker/index.ts`) serving `site/` (not `docs/`); GitHub Pages is off. Deploy with
`npm run deploy` (refuses CRLF site files; `npm run typecheck` keeps `wrangler.jsonc` and
`wrangler.workers-dev.jsonc` in sync) manually when needed. Workers Builds deploys `main` only (preview-branch builds are off); deploy
command `npm run deploy`, watch paths `worker/*`, `site/*`, `wrangler*.jsonc`, `package*.json`. A Worker custom domain can't replace an existing DNS
record: delete it first (wrangler has no DNS scope). Privacy policy edits touch both `PRIVACY.md` and
`site/privacy.html`. Don't add `docs/CNAME` or a GitHub Actions deploy. See `docs/DEPLOYING.md`.
The Worker also serves `dinoreader.sirous.uk` -> `site/dino/` (workers.dev: `/dino/` prefix); host routing is in `worker/downloads.ts` (`resolveSite`), every `run_worker_first` path needs its `/dino` twin (`check-configs` enforces), and the Dino site makes no third-party requests (kids' app).
Usage counts (`POST /api/events`, `worker/events.ts`, Slice 133) write to Analytics Engine through a `USAGE` binding that is **not** in the wrangler configs, because a deploy fails if Analytics Engine is not enabled on the account. Without it the endpoint answers 204 and writes nothing while telemetry is off, 503 while it is on. To switch counting on: (1) enable Analytics Engine in the Cloudflare dashboard (Workers & Pages > Analytics Engine); (2) add this block to both `wrangler.jsonc` and `wrangler.workers-dev.jsonc` (next to `r2_buckets`): `"analytics_engine_datasets": [{ "binding": "USAGE", "dataset": "dino_usage" }],`; (3) deploy (merge to `main`); (4) set `telemetry.enabled` (and `sampleRate`) in `dinoreader/config.json`.
Usage events carry only closed-list fields, never an id, typed text or own-book title; `source` is `shelf`+id only for starter/store books, else `own`. Count with `SUM(double1)`, not `COUNT(*)`, because of sampling. (Slice 133: docs/plan/slices/133-dino-anonymous-usage-counts.md)
The Dino host also serves the free story store (Slice 92): `GET /api/stories`, `/api/stories/<id>/cover` and `/api/stories/<id>/download[?v=n]` (`worker/stories.ts`, Dino only; `/api/stories` and `/api/stories/*` plus their `/dino` twins are in `run_worker_first`). Every Dino book (starter or store) is a folder `content/dino/<stem>/` (`story.md`, `book.json` with the id, version, ages, author, credits, colour, voices and content hash, `pictures/`, `cover.jpg`, `cover-thumb.jpg`, `translations/`, `art.md`); `dotnet run --project tools/DinoReader.BookBuilder -- build` writes `artifacts/store/<id>/` and the committed starters, `check` fails when a book changed without `stamp` (which raises its version) and runs in CI (Slice 157, Decision 162: docs/plan/slices/157-dino-book-content-out-of-tests.md). Store stories are built with `build` (to `artifacts/store/<id>/`) and uploaded **automatically on every merge** by `release.yml`'s `publish-store` job (the `dinostore` target; every book whose built version is above the live one; Slice 158, Decision 163); `tools/upload-store-story.ps1 <id>` is the manual fallback. Either way the files go to `dinoreader/stories/<id>/{meta.json, cover.jpg, story-v<n>.dino}` in the downloads bucket, `meta.json` last: a folder is listed only with `meta.json` and a package, and `meta.json` carries `schemaVersion` (the `.dino` format), `title`, `language`, `pageCount`, `minutes`, optional `version`/`added`/`credits`, `ageMin`/`ageMax` (both or neither; integers 0-18, min <= max, else both dropped) and `author` (trimmed, max 100; Slice 112) (`worker/stories.ts` trims `credits` and caps it at 200 characters; Slice 102). The story id is the package id; upload a new `story-v<n+1>.dino` rather than overwriting a version (`?v=` is cached for a day).
The Dino host also serves the remote config (Slice 132): `GET /api/app-config` (`worker/appconfig.ts`, Dino only, `/api/app-config` and its `/dino` twin in `run_worker_first`, read from R2 on every request so a changed file takes effect at once) from the R2 object `dinoreader/config.json` in the downloads bucket: `{"android":{"minVersion":"1.2.0","latestVersion":"1.3.0"},"ios":{...},"windows":{...},"telemetry":{"enabled":false,"sampleRate":0}}`. Change it with `wrangler r2 object put readthestupidtext-downloads/dinoreader/config.json --file config.json --remote`; a missing or bad file (or field) means no forced update and telemetry off. To force an update set that platform's `minVersion` above the oldest build you still allow.
The RTST host also serves `/api/events` and `/api/app-config` from `readthestupidtext/config.json` (Slice 137); its `RTST_USAGE` binding (dataset `rtst_usage`) is added to both wrangler configs only when switching on, with the same four steps as Dino. (Slice 137: docs/plan/slices/137-rtst-phone-usage-counts.md)
(Slice 68: docs/plan/slices/68-landing-site-on-worker.md; Slice 74: docs/plan/slices/74-dino-reader-site.md; Slice 92: docs/plan/slices/92-dino-story-store.md)

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

Dino Reader's pure library has its own suite (xUnit v3, plain `net10.0`, runs on any OS):

```bash
dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj
```

The `tests.yml` workflow (called by `release.yml`) runs them on every push/PR that
touches app code (docs-only changes skip it) and **gates** the build and release jobs,
so a failing test blocks the release.

## Versioning, license & releases (Batch 2 — Slices 11–16)

- **License is MIT** (Decision 16 in `docs/plan/decisions.md`). Anyone may use/extend/sell it
  provided the copyright notice is kept. Do **not** add GPL/LGPL components (the
  Supertonic/sherpa stack was chosen to keep this clean — see `STORE.md`). The one
  **scoped exception**: espeak-ng data (GPL-3.0) lives only inside the downloaded `piper-fa`,
  `piper-nb` and `kokoro-en` packs, never in the repo or any package (Decisions 59-71, 108, 119).
  The MIT licence covers the code only: Supertonic-3 weights are BigScience OpenRAIL-M, not
  MIT/Apache-2.0. The `supertonic-3` pack ships `LICENSE-OpenRAIL-M` + `NOTICE` (via `packs.yml`);
  keep both and pass the use restrictions on. Kokoro-82M is Apache-2.0.
  (Slice 114: docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md)
- **Every merge to `main` that touches app code ships a version** (Decision 17;
  docs-only merges cut none, and only the changed platforms are rebuilt and
  submitted — Batch 8), derived by
  **GitVersion** from git history — **git tags are the source of truth**. The
  single `release.yml` run does it all: GitVersion computes the next SemVer, the
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
