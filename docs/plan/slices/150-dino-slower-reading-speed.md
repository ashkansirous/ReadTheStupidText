# Slice 150 — Dino: slower reading by default, and a grown-up speed setting

*Planned 2026-10-26.* Dino Reader only; RTST is not touched.

**Batch:** 31 (tester review, 2026-10-26; Decision 155)
**Touches:** `src/DinoReader.App/SpeechStartup.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/GrownUpMode.cs`, `src/DinoReader.Core/ReadingSpeed.cs` (new), `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

A tester review (2026-10-26) found the reading too fast for small children. Dino always reads at 1.0x:
`SpeechStartup.cs:33` passes the shared `ISettingsStore.Speed` (`PlaybackRate`, default 1.0), and no Dino
screen ever writes it, so parents cannot change it.

## Decisions locked with the user (Decision 155)

- The default is **0.8x** (about 20% slower).
- Grown-ups can set **0.5x to 1.2x in 0.05 steps**, on the grown-up settings screen, behind the gate. Nothing above 1.2x.
- One setting for the whole app, not per book.
- A character's flavour pace (`VoiceFlavour`) multiplies on top of it, as today with 1.0.
- The shared `PlaybackRate` (0.5-2.0) is unchanged; RTST keeps its range. Dino clamps to its own range in a
  pure `ReadingSpeed` (Core).

## Work

- [ ] `ReadingSpeed` (Core): Min 0.5, Max 1.2, Default 0.8, Step 0.05, `Clamp`, built on `PlaybackRate`. Unit tests.
- [ ] `SpeechStartup` uses Dino's own default when nothing is stored. Check `MobilePreferencesSettingsStore.cs:18`'s
      fallback: a stored 1.0 written by nobody must not exist, but a missing value must give 0.8. A stored
      value above 1.2 is clamped.
- [ ] A "Reading speed" row in grown-up settings (`SettingsRows`): a slider with the current value ("0.8x"), plus
      a play button that reads a sample sentence at that speed. The new speed applies to the next sentence read.
- [ ] Strings in all 14 locales.
- [ ] Rule line in `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/DinoReader.Tests` (ReadingSpeed tests).
- Emulator: a fresh install reads at 0.8x. The slider stops at 0.5 and 1.2, and a change is heard on the next sentence.
  The setting survives an app restart.
