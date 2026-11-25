# CLAUDE.md — Tests

Loaded when you work under `tests/`. The three run commands and the placement rule are in the root `CLAUDE.md`.

- Three xUnit v3 projects: `tests/Rtst/ReadTheStupidText.Tests` (RTST-only logic, `net11.0-windows10.0.19041.0`), `tests/Shared/Shared.Tests` (shared engine, Documents, Domain, Application, Mobile.Shared links, and the two repo-wide guards `FallbackGuardTests` and `SherpaVersionPinTests`; `net11.0-windows10.0.19041.0`) and `tests/Dino/DinoReader.Tests` (Dino Reader's pure library, plain `net11.0`, runs on any OS). (Slice 368: docs/plan/slices/368-shared-tests-project.md)
- They cover the **pure** logic only: `PlaybackRate`, `SpeechTextChunker`, `SupertonicVoiceTable`, `ActivityLog`, and the `LocalSettingsStore` settings migration (via the pure `ResolveAutoReadFlag`; internals are exposed through `InternalsVisibleTo`).
- The WinUI/WinRT/native paths (readers, monitors, windows) are **not** unit-tested: they need package identity or a UI host. Verify those by running the app (VS **(Package)** profile).
- The `tests.yml` workflow (called by `release-shared.yml`, once per product run) runs the suites on every push/PR that touches app code (docs-only changes skip it) and **gates** the build and release jobs, so a failing test blocks the release.
- Tests capture `PackDownloadLog` through `PackDownloadLog.CaptureScope`, never by setting the static `PackDownloadLog.Sink`: the sink is process-wide and parallel tests' lines leak into it. A test that must exercise the global `Sink` goes in the non-parallel collection (`tests/Shared/Shared.Tests/PackDownloadLogGlobalSinkTests.cs`). (Slice 388: docs/plan/slices/388-shared-pack-download-log-capture-per-test.md)
