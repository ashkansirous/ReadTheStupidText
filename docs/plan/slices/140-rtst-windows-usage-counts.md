# Slice 140 — RTST Windows: the same usage counts and timings

*Planned. Starts after Slices 137-139.*

**Batch:** 29 (Decisions 143-148)
**Touches:** `src/ReadTheStupidText.Infrastructure/Usage/**` (new), `src/ReadTheStupidText.App/App.xaml.cs`, `src/ReadTheStupidText.App/ControlPanelWindow*.cs`, `src/ReadTheStupidText.App/ControlPanelWindow.xaml`, `src/ReadTheStupidText.App/Strings/**`, `src/ReadTheStupidText.Application/ReadAloudService*.cs`, `src/ReadTheStupidText.Application/Usage/**`, `worker/events.ts`, `PRIVACY.md`, `site/privacy.html`, `STORE.md`, `tests/ReadTheStupidText.Tests/**`, `src/ReadTheStupidText.App/CLAUDE.md`

## Why

The tray app is where most reads happen on Windows; the same questions apply (languages, translate
toggle use, voice changes, translation and first-audio times, including translation-pack downloads).

## Decisions

- Decisions 143-145 apply unchanged: the same events (with `platform = windows` and Windows sources:
  `selection`, `clipboard`, `hotkey`, `panel`), no id, on by default with an off switch in the
  control panel, nothing sent until the Microsoft Store declarations match.
- Windows sends through a small `HttpClient` sender over the shared pure queue from
  `Application/Usage/` (Slice 137), persisted in the app's local settings.

## Work

- [ ] Windows sender, queue persistence, flush on exit and next launch.
- [ ] Events from `ReadAloudService` (read source, mode, voice, timings: pack download, translate,
      first audio) and the control panel (translate toggle, voice change).
- [ ] Switch and one line in the control panel; strings.
- [ ] Privacy texts and `STORE.md` gain the Windows sources; Microsoft Store row of the checklist.

## Verification

- Unit tests; packaged dev run (`device-test`): a hotkey read and a translated read send one batch;
  the switch off sends nothing.
