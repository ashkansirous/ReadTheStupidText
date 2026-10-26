# Slice 142 — RTST Windows: early translate and voice in the control panel

*Built 2026-10-26 (wave PR #408), not yet verified on a desktop.*

**Batch:** 29 (Decisions 143-148)
**Touches:** `src/ReadTheStupidText.Application/Reading/ReadAloudService*.cs`, `src/ReadTheStupidText.Application/Translation/ReadTranslator.cs`, `src/ReadTheStupidText.Application/Translation/TranslationBlocks.cs`, `src/ReadTheStupidText.Application/Reading/EarlyStart*`, `src/ReadTheStupidText.Infrastructure/Translation/**`, `src/ReadTheStupidText.Infrastructure/Reading/SupertonicSpeechReader.cs`, `src/ReadTheStupidText.App/App.xaml.cs`, `src/ReadTheStupidText.App/ControlPanelWindow*.cs`, `src/ReadTheStupidText.App/WaitFlyoutController.cs`, `tests/ReadTheStupidText.Tests/**`

## Why

Windows already streams: it speaks a translated head block while the rest translates. The Windows
control panel has no editable text box, so the quiet moment is the auto-read debounce (a selection
or copy settles before it reads); early work runs there, and hotkey, Play and file reads reuse
early work that matches their text.

## Decisions

- Decision 147 applies with Windows pieces: `TranslationBlocks` uses the same 3-sentence
  translation span as its first block only when early work for that text exists (other reads keep
  the shorter first block); Marian translates it early when the translate toggle is on and the
  detected language differs; the pack downloads early only on an unmetered network
  (`NetworkInformation` cost), and only when the pack is missing.
- A hotkey or selection read uses whatever early work already matches its text, and none otherwise.

#### Design (RTST Batch 29 mock, added 2026-10-26)

No screens. The Batch 29 mock intro says the early start has no screen of its own. Nothing to build from the mock.

## Work

- [x] Early translate + voice in `ReadAloudService.PrepareWhenStableAsync`, reusing `EarlyStartPlan`.
- [x] Cancel on Read, on toggle change and on text change.
- [x] The full streamed translation reuses the early head.

## Verification

- Unit tests; packaged dev run: auto-read on, translate on, select French text: first audio comes
  from the early head; toggle translate off before reading: the translation work stops.

Changed while building:

- Early work runs in the auto-read debounce, in `ReadAloudService.EarlyStart.cs`, reusing Slice 139's `EarlyStartCoordinator`; hotkey, Play and file reads reuse matching early work.
- The 3-sentence first block is used only when early work exists.
- `FirstChunkHints` in `SupertonicSpeechReader` is confirmed against the full-text plan.
- `WindowsPackUpdateNetwork`: the early pack download runs on unmetered networks only, with a per-pair 5-minute retry gate.
- A blank remainder translation falls back to the full text.
- Spec corrected: the Windows control panel has no editable text box, so no panel editor work exists.

From review: 142-high (14 applied), 142-medium (9 applied).

#### For CLAUDE.md

- Windows early start lives in `ReadAloudService.EarlyStart.cs`: coordinator calls happen under `_earlyGate`, never await under it, and CTS cancels are deferred until outside the gate. (`src/ReadTheStupidText.App/CLAUDE.md`; Slice 142: docs/plan/slices/142-rtst-windows-early-start.md)

#### Not yet verified

- Packaged desktop run (x64 dev build, 2026-10-26, CPU-saturated host so timings rough): early work runs in the debounce and the read reuses its chunks; toggling translate off stops the translation at once; the Slice 141 flyout still shows after ~0.8 s and hides on first audio; no crash. Two gaps found and fixed after the run (early original voicing now prepares only the head; a short translated block now starts with the early head): re-check first-audio timing on a quiet machine. Prompt tray exit not verified.
