# Slice 293 — Slice 290 check-pass leftovers (sound pill ring, story null checks)
**Product:** Dino

*Done 2026-11-07 by Slice 290 part B (PR #895): the ring and icon colours refresh on every `Show`, and `ShowAudioStatus` checks the story once and passes it down. Nothing left to build here.* Two small findings from Slice 290's check pass, both low, not applied then ([Slice 290](290-dino-reading-sound-pill.md), built #889). Moved from plan.md's *Outstanding work*. Android and iOS are the same MAUI code here; one change serves both.

**Touches:** `src/DinoReader.App/SoundStatusPill.cs`, `src/DinoReader.App/SoundStatusPillRing.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `tests/DinoReader.Tests/SoundStatusPillStateTests.cs`, `docs/plan/slices/293-slice-290-check-pass-leftovers.md`

## Why

- A light/bedtime colour switch while the Getting pill shows with unchanged text leaves the ring on the old colours until the next count: `SoundStatusPill.cs:59` returns early ("nothing else changed: skip the icon rebuild and the ring redraw") before `_ringView.Invalidate()` (line 93).
- Two null checks on the story near `ReadingPage.xaml.cs:1107-1109` (`_story is { } book` then `_story is { } shownBook`) could drift apart (cleanup).

## Work

- [ ] 1. Android and iOS: make the early return in `SoundStatusPill.Show` also compare the colour set (or call the ring's `Invalidate` before returning) so a theme switch redraws the ring. Add a state/theme test where one fits.
- [ ] 2. Android and iOS (same code): read `_story` once into a local in the status block near `ReadingPage.xaml.cs:1107` and use it for both the language and the status.

## Verification

- Unit test: a theme change with identical pill text changes the ring colours.
- Emulator flow (Android) and an iOS simulator or TestFlight look: switch light to bedtime while the pill shows; the ring recolours at once.
