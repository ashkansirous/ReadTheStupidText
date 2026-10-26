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

#### Design (RTST Batch 29 mock, frame W2, added 2026-10-26)

One frame, W2 (line 172 of `docs/design/rtst-phone/RTST Batch 29.dc.html`), shared with Slice 141. Only the counts card is this slice's.

- **Counts switch (W2, control panel).** Last row of the panel, after the existing toggles, below a 1px `rgba(0,0,0,.08)` divider, padding-top 12, gap 12. Title "Help improve the app" (13px, `rgba(0,0,0,.89)`). Helper "Sends counts like "a French text was read". No names, no ids, nothing you read." (11.5px, `rgba(0,0,0,.56)`, line 1.35). Switch 40x20, radius 10, on state `#3B6FE3`, 12px white knob 4px from the right. On by default.
- The panel is 344px wide, radius 8, fill `rgba(251,251,253,.94)`, padding 16, gap 14.

**New strings (Windows locales):** the same three as Slice 137 (title, helper, first-run line), reused if the string files can share them.

**Open point: Windows first-run window.** The W2 note says "the first-run window gets the same one line as R11". The plan says one line in the control panel, and the App project has no first-run window today. **Default:** put the line in the control panel's counts card only (the helper already says it) and add no first-run window.

## Work

- [ ] Windows sender, queue persistence, flush on exit and next launch.
- [ ] Events from `ReadAloudService` (read source, mode, voice, timings: pack download, translate,
      first audio) and the control panel (translate toggle, voice change).
- [ ] Switch and one line in the control panel; strings.
- [ ] Privacy texts and `STORE.md` gain the Windows sources; Microsoft Store row of the checklist.

## Verification

- Unit tests; packaged dev run (`device-test`): a hotkey read and a translated read send one batch;
  the switch off sends nothing.
