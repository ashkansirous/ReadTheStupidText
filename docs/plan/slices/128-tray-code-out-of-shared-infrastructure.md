# Slice 128 — Tray code out of the shared Infrastructure, and tests per product

*Planned 2026-10-25.*

**Batch:** 27 (product separation, Decisions 133-137)
**Touches:** `src/ReadTheStupidText.Infrastructure/Input/**`, `src/ReadTheStupidText.Infrastructure/Startup/**`, `src/ReadTheStupidText.Application/Input/**`, `src/ReadTheStupidText.Application/Startup/**`, `src/ReadTheStupidText.Application/Activity/**`, `src/ReadTheStupidText.Domain/Activity/**`, `src/ReadTheStupidText.Windows.Tray/**` (new), `src/ReadTheStupidText.App/**`, `ReadTheStupidText.slnx`, `tests/ReadTheStupidText.Tests/**`, `.github/scripts/ship-targets.mjs`, `.github/workflows/release.yml`, `.github/CLAUDE.md`, `src/ReadTheStupidText.Infrastructure/CLAUDE.md`, `src/ReadTheStupidText.App/CLAUDE.md`, `CLAUDE.md`

## Why

`Infrastructure` is shared: Dino's Windows head references it through Mobile.Shared
(`Mobile.Shared.csproj` ~112). But it also holds code only the tray uses: the global hotkey, UI
Automation selection monitor, clipboard listener, selection copier, foreground probe and startup task
(`Infrastructure/Input`, `Infrastructure/Startup`, about 0.5k lines). So a tray-only edit rebuilds and
republishes Dino Windows, and Dino ships dead tray code. Separately, the `test` job runs RTST's tests
whenever `dinowin` is true (`release.yml` 163-167), so every Dino change runs RTST's suite.

## Decisions

- **Decision 135:** tray-only code (Input, Startup, and Activity if only the tray uses it) lives in a new
  `ReadTheStupidText.Windows.Tray` class library (`net10.0-windows`) referenced only by
  `ReadTheStupidText.App`. Shared Infrastructure keeps speech, translation, settings, logging,
  sanitizing. A separate project (not the App project) keeps the tray logic out of the WinUI shell.
- Tests run per product: RTST tests when an RTST target is set, Dino tests when a Dino target is set.

## Work

- [ ] Confirm with `git grep` which of Application `Input`/`Startup`/`Activity` and Domain `Activity` are
      tray-only; move only those (interfaces may stay in Application if Mobile uses them).
- [ ] Create `ReadTheStupidText.Windows.Tray`, move the files, fix namespaces and DI in `App`.
- [ ] `ship-targets.mjs`: new `src/ReadTheStupidText.Windows.Tray/**` maps to `windows` + `windowsstore`
      only; Infrastructure stays `windows` + `windowsstore` + `dinowin`.
- [ ] `release.yml`: split the test gate so RTST tests run on `windows|android|ios` and the Dino suite on
      `dinoapp|dinoios|dinowin` (core changes set both).
- [ ] Update the folder CLAUDE.md files (where tray code lives) and the layered-structure block in root
      `CLAUDE.md`.

## Verification

- `dotnet build src/ReadTheStupidText.App/ReadTheStupidText.App.csproj -p:Platform=x64` and the Dino
  Windows publish command both build; both test suites green.
- `node .github/scripts/ship-targets.mjs` unit cases (or the release-scripts tests): a Tray-only path
  sets no `dino*` target; a `DinoReader.App` path does not run RTST tests.
- Desktop device-test: hotkey read, auto-read on selection, start-at-login toggle still work.
