# Slice 142 — RTST Windows: early translate and voice in the control panel

*Planned. Starts after Slices 139 and 141.*

**Batch:** 29 (Decisions 143-148)
**Touches:** `src/ReadTheStupidText.Application/ReadAloudService*.cs`, `src/ReadTheStupidText.Application/Translation/ReadTranslator.cs`, `src/ReadTheStupidText.Application/Reading/EarlyStart*`, `src/ReadTheStupidText.Infrastructure/Translation/**`, `src/ReadTheStupidText.App/ControlPanelWindow*.cs`, `tests/ReadTheStupidText.Tests/**`

## Why

Windows already streams: it speaks a translated head block while the rest translates. The idle time
before the user presses Read exists only in the control panel's editor (hotkey and selection reads
start at once), so this applies Decision 147 there.

## Decisions

- Decision 147 applies with Windows pieces: `TranslationBlocks` head block becomes the same
  3-sentence translation span; Marian translates it early when the translate toggle is on and the
  detected language differs; the pack downloads early only on an unmetered network
  (`NetworkInformation` cost), and only when the pack is missing.
- A hotkey or selection read uses whatever early work already matches its text, and none otherwise.

## Work

- [ ] Early translate + voice in `ReadAloudService.PrepareWhenStableAsync`, reusing `EarlyStartPlan`.
- [ ] Cancel on Read, on toggle change and on text change.
- [ ] The full streamed translation reuses the early head.

## Verification

- Unit tests; packaged dev run: type French text, wait 2 s, press Read with translate on, first
  audio arrives within the cached time; toggle translate off before reading: the translation work stops.
