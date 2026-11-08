# Slice 324: Dino reads at 1x by default

*Built 2026-11-08.* User, 2026-11-08: "set the default speed for the app to 1x". Read The Stupid Text already defaults to 1x (`PlaybackRate.Default`, `src/ReadTheStupidText.Domain/Reading/PlaybackRate.cs`), so the change is Dino Reader's 0.8x default (Decision 155) becoming 1x (Decision 172).

**Touches:** `src/DinoReader.Core/ReadingSpeed.cs`, `src/DinoReader.App/ReadingSpeedPreference.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/CLAUDE.md`, `tests/DinoReader.Tests/ReadingSpeedTests.cs`, `.maestro/dino/settings-reading-speed-row.yaml`, `docs/plan/decisions.md`, `docs/plan/slices/150-dino-slower-reading-speed.md`

## Items

1. `ReadingSpeed.Default` goes from 0.8 to 1.0; the 0.5x-1.2x range and 0.05 steps stay. Android, iOS and Windows share it, so all three heads change together.
2. Who it reaches: every install with no stored `dino.reading.speed`. The value is saved only when a grown-up moves the slider (`SettingsRows.Commit`), so a speed someone already picked is kept.
3. Unit test `ReadingSpeedTests.The_default_is_normal_speed` asserts 1.0.
4. The Batch 31-46 design mocks still show 0.8x; the user's later instruction wins over them. The mocks are not redrawn.

5. `.maestro/dino/settings-reading-speed-row.yaml` asserts "1x" on the Reading speed page after its `clearState` launch (a fresh install).

## Verified

- Android emulator (RTST_Test, 2026-11-08): `settings-reading-speed-row.yaml` passed with the 1x assertion, 49.5 s.

## Reviews

- High: `reviews/speed1x-high.md`, 7 low findings; 2, 3, 5 and 7 fixed; 1 and 6 covered by Decision 172's note; 4 (no test of `Load()`, which needs MAUI `Preferences`) left.
- Medium: `reviews/speed1x-medium.md`, 3 low findings; 1 fixed (flow assertion); 2 is intended (a speed someone stored stays); 3 as high 1.

## Not yet verified

- iOS: a fresh Dino install shows 1x on the Reading speed page and a pre-made clip plays at normal speed.
