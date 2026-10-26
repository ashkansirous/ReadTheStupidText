# Slice 140 — RTST Windows: the same usage counts and timings

*Built 2026-10-26 (wave PR #391); packaged desktop run passed 2026-10-26 (queue file needs telemetry on).*

**Batch:** 29 (Decisions 143-148)
**Touches:** `src/ReadTheStupidText.Infrastructure/Usage/**` (new), `src/ReadTheStupidText.App/App.xaml.cs`, `src/ReadTheStupidText.App/ControlPanelWindow*.cs`, `src/ReadTheStupidText.App/ControlPanelWindow.xaml`, `src/ReadTheStupidText.App/Strings/**`, `src/ReadTheStupidText.Application/ReadAloudService*.cs`, `src/ReadTheStupidText.Application/Usage/**`, `worker/events.ts`, `PRIVACY.md`, `site/privacy.html`, `STORE.md`, `README.md`, `site/index.html`, `tools/rtst-usage-report.ps1`, `tests/ReadTheStupidText.Tests/**`, `src/ReadTheStupidText.App/CLAUDE.md`

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

- [x] Windows sender, queue persistence, flush on exit and next launch.
- [x] Events from `ReadAloudService` (read source, mode, voice, timings: pack download, translate,
      first audio) and the control panel (translate toggle, voice change).
- [x] Switch and one line in the control panel; strings.
- [x] Privacy texts and `STORE.md` gain the Windows sources; Microsoft Store row of the checklist.

## Verification

- Unit tests; packaged dev run (`device-test`): a hotkey read and a translated read send one batch;
  the switch off sends nothing.

*Changed while building:* the sender and queue are not in local settings: `Infrastructure/Usage/` keeps a file queue and the config under `LocalFolder/usage/` with an HTTP transport, over `UsageService` (with `IUsageRecorder`/`IUsageStore`/`IUsageTransport`) in `Application/Usage/`, mirroring Slice 137 semantics. Events come from `ReadAloudService.Usage.cs`; `app_opened` from `App.xaml.cs`. The last flush runs on `ProcessExit`, waits at most 2 s and uses the held config; a batch is dequeued before the POST (loss over double count). With the switch off the app fetches no config; counts are held unstamped until a config arrives. Worker: `source` is blob 15, a `windows` engine was added, and `translate_chosen` `sourceLanguage` is optional. `tools/rtst-usage-report.ps1` updated; `PRIVACY.md`, `site/privacy.html`, `STORE.md`, `README.md` and `site/index.html` now describe counts on every platform. No first-run window (the open point's default).

*From review:* 140-high: 13 findings applied, 6 left by design. 140-medium: 6 applied, 5 not a bug, 8 skipped.

#### For CLAUDE.md (lifted 2026-10-26)

Already added by the build as "Anonymous usage counts (Slice 140)" in `src/ReadTheStupidText.App/CLAUDE.md` (queue cleared only on an explicit off, never while no config has been seen; Worker lists must grow with `UsageEngine`/`UsageSource`; counting stays off until the remote config says on). Index pass: do not lift it again.

#### Not yet verified

- Packaged desktop run (x64 dev build, 2026-10-26): PASS for the switch (on by default, persists), clipboard/hotkey/translated reads, no crash with telemetry off, tray Quit in 0.6 s. Not exercised: the `LocalFolder/usage` queue file (needs `telemetry.enabled` on; covered by `UsageServiceTests`).
- Microsoft Store data-collection answers (the `plan.md` checklist row).
- Rows appear in `rtst_usage` once Analytics Engine is switched on.
