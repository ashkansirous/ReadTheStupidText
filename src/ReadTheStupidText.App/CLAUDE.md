# CLAUDE.md — Windows App (WinUI 3 tray, panel, read orchestration)

Loaded when you work under `src/ReadTheStupidText.App`. Speech engines, voice packs and the
neural reader are in `src/ReadTheStupidText.Infrastructure/CLAUDE.md`. (This folder's `AGENTS.md` is
a generic WinUI template and is separate from these rules.)
The tray's Win32/UIA services (hotkey, selection monitor, clipboard listener, selection copier, foreground probe, startup task) live in `src/ReadTheStupidText.Windows.Tray`, referenced only by this project (Slice 128).

The Batch 29 tray wait flyout and control-panel counts switch (Slices 140-141) are screens W1-W2 of `docs/design/rtst-phone/RTST Batch 29.dc.html`, and the Batch 42 update notices (Slice 217) are W3-W4 of `docs/design/rtst-phone/Batch 42 Updates and Paused Downloads.dc.html` (index in that folder's README): read visual values from it, never guess them, never call `DesignSync` for them.

Keep `App.xaml.cs` thin: provider/DI wiring and window bootstrap only. Real
logic lives in Application/Infrastructure; XAML views stay free of business
logic.

> **Reading text box + disk-backed audio (Batch 7):** a toggleable floating window
> shows the chunk being read (4th icon in the panel's `CONTROLS` row). Synthesized
> audio is a temp WAV per chunk under `TemporaryFolder\audio\<id>\`; the chunk
> map used by Skip (Decision 32) and the read-through timer (Decision 33) carries
> each chunk's file path and source-text range, so extend it, don't duplicate it.
> (Decisions 45-51; note: docs/plan/notes/claude-md-history.md)

## Localization and translation (Slice 57)

UI copy is in `Strings/<tag>/Resources.resw` (en-US, fa, fr, es, pt, zh-Hans, ar, ru, ja, da, fi, sv, nb, nl, de; tags from `UiLanguageTag`, listed in `Package.appxmanifest`). XAML uses `x:Uid` (`Uid.Property` keys; attached properties as `Uid.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip`), code uses `UiStrings.Get/Format`; never hard-code a UI string. A new key goes into all fifteen files (the resource-coverage tests fail otherwise). `UiStrings.ApplyLanguage` runs in `App()` before any window; `x:Uid` text is applied once per window, so the panel's language picker restarts the app (`AppInstance.Restart`). The layout never mirrors (Slice 88): each window's `RootGrid` is fixed `LeftToRight`, and only text takes a direction through `TextFlow` (`ContentDirection` for read/activity text by first strong letter, `ForUi` for localised strings by UI language). Translation runs in `ReadAloudService.ReadEntryAsync` through `ReadTranslator` (time budget, falls back to the original with a `TranslationOutcome`); the engine is `PackTranslator`, which has no inference engine yet. (Slice 57: docs/plan/slices/57-windows-localization-translation.md)

## Anonymous usage counts (Slice 140)

Windows sends the phone's counts (`Application/Usage/`: `UsageEvents`, `UsageBatch`, plus `UsageService` over `IUsageStore`/`IUsageTransport`; `Infrastructure/Usage/` has the file store, HTTP transport and `WindowsUsage.Create`). `ReadAloudService.Usage.cs` records reads, voice changes, the translate switch and the translate, pack-download and first-audio timings through `IUsageRecorder`, which never throws and never blocks the caller. The panel's last row (`ControlPanelWindow.Usage.cs`) is the user's switch: on by default, off clears the queue. Queue and config files live in `LocalFolder/usage/`; sends run on the pool, and the last one at `ProcessExit` waits at most 2 s and uses the held config (no new request), so it can fall short; what it misses stays queued for the next launch. With the switch off the app makes no config request at all. Rules: no id and no text in a count; the queue is cleared only on an explicit off (the switch, or a config really fetched or kept), never while no config has been seen; the Worker's lists (`worker/events.ts`, guarded by `UsageEventsTests`) must grow with `UsageEngine`/`UsageSource`; counting stays off until the remote config says on (Decision 145). (Slice 140: docs/plan/slices/140-rtst-windows-usage-counts.md)

## Windows waits (Slice 141)

- Windows waits go through `WaitFlyoutController` (500 ms `WaitGate`); the flyout (`WaitFlyoutWindow`) must stay no-activate so a hotkey read never steals focus. (Slice 141: docs/plan/slices/141-rtst-windows-wait-indicator.md)

## Release packaging

The **Release** MSIX is
built **.NET self-contained** (`<SelfContained>true</SelfContained>`, non-Debug
only) so the .NET runtime ships inside the package — this fixed Store cert
**10.2.4.1** (undisclosed dependency on .NET); the Windows App SDK runtime stays
framework-dependent (Store-delivered). Debug stays framework-dependent for a fast
inner loop. Trimming stays off (`PublishTrimmed=false`) — packaged WinUI apps
aren't trimmed. Note the build-time `onnxruntime.dll` dedupe in the App `.csproj`
(`RemoveWinMlOnnxRuntimeDuplicate`: WinML and sherpa both ship one; we keep sherpa's).

## Out of scope (v1)

Voice *tuning* beyond playback rate (pitch/volume/SSML), a **persistent/dockable**
settings window (tabs, taskbar presence, hotkey-remap UI), and non-Store
distribution as the primary channel. See `scope.md`.

**Console / non-UIA limitation (by design, not a bug).** The console (Windows
Terminal, PowerShell, `cmd`) and some editors (e.g. Visual Studio's code editor)
expose **no UI Automation text selection**, so *selecting* text in them raises no
OS event — there is nothing to auto-read on. Reading from them therefore requires a
**copy** (auto-read-on-copy via the clipboard listener, gated by the **Auto-read
on copy** toggle) or the **global hotkey** (`Ctrl+Win+R`, copies then reads). Windows
Terminal's `copyOnSelect` makes selecting auto-copy, which then triggers auto-read.
Detecting a bare console *selection* is **out of scope** — Windows provides no API
for it. When a request asks to "read on selection in the terminal," the answer is
copy / `copyOnSelect` / hotkey, not a new selection-detection mechanism.

## Tray, panel, logging and read paths

Tray control panel (Slices 8, 13, 24, 25; docs/plan/slices/08-tray-control-panel-window.md, 13-media-card-control-panel-redesign-media.md, 24-draggable-position-persisted-control-panel.md, 25-read-through-timer.md): a left-click panel, a borderless always-on-top `AppWindow` pinned above all windows (closes only on the X or another tray click, never light-dismisses), sized to its content. It is transient, not a settings window; the right-click `MenuFlyout` stays (Quit lives only there). Rich controls (slider, `ComboBox`) cannot go in H.NotifyIcon's `PopupMenu`, which is why it is a real window. It is the "Media Card" design (`design_handoff_tray_panel/`, the HTML is the visual source of truth): gradient header (waveform, status, play/pause, display-only progress bar from `ISpeechReader.ProgressChanged`, speed pill that reveals the 0.05 slider plus six preset chips kept in sync via `ApplyPresetVisual`/`UpdatePresetHighlight`), a full-width voice `ComboBox` + activity-log button (`ActivityLogRequested`), and three 70px stroked-icon `ToggleButton`s (auto-read selection, auto-read copy, launch at startup; icon colour set in `ApplyToggleVisual`). Seek/scrubbing is out of scope. `TimerText` shows `mm:ss/mm:ss` from `ISpeechReader.TimingChanged` (`ReadTiming`, `ReadTimingFormatter`); elapsed/total use real synthesized chunk durations from `ReadTimingTracker` (pure, shared by both readers); `Total` fires immediately when newly known, and resets on Idle. The header drags the window by raw `GetCursorPos` delta; the position persists as `ISettingsStore.PanelPosition` (`PanelX`/`PanelY`), is clamped to the work area (`ClampToWorkArea`) on open, and defaults to bottom-right. Child controls mark pointer input handled so only empty header areas drag.

**Play loading state.** `ReadAloudService.IsGeneratingAudio` is true from `GeneratingAudio` until the
first `Playing`; `GeneratingChanged` is deduped but not ordered, so handlers re-read it. After
`SpeakAsync` returns a read gets `FirstAudioGrace` (8 s) to reach `Playing` before being marked
Failed (the WinRT fallback returns before playback starts), so any new `ISpeechReader` must await the
whole read or raise `Playing` within that grace. Play does nothing while generating. The control
panel overlays a `ProgressRing`; Mobile `ReaderPage` tracks a per-read token and overlays an
`ActivityIndicator`. (Slice 46: docs/plan/slices/46-play-loading-state.md)

Localization (Slice 57): every user-visible string lives in `Strings/<tag>/Resources.resw` for all 9 languages (`x:Uid` in XAML, `UiStrings.Get/Format` in code); the resw coverage tests fail on a missing key, so never hard-code UI copy. A language change restarts the app (`x:Uid` applies once per window). Never set `FlowDirection` on a container; use `TextFlow` on text elements (Slice 88: docs/plan/slices/88-desktop-no-layout-mirroring.md). Translation UI stays hidden while `ITranslator.IsAvailable` is false (Slice 75 adds the engine). (Slice 57: docs/plan/slices/57-windows-localization-translation.md)

Activity log and diagnostics (Slices 10, 19, 21; docs/plan/slices/10-live-activity-log-auto-read-fix.md, 19-local-only-timing-diagnostics.md, 21-daily-on-disk-logs-open-logs.md): `IActivityLog` (Application, in-memory ring buffer) holds `ActivityEntry` (state pending, generatingAudio, reading, read, ignored, interrupted, failed; `ActivityTrigger`; `ActivityReason` NewSelection/Deselected/Error; `WindowSource` via `IForegroundWindow`), rendered by `ActivityLogWindow`. Two daily files under the package TemporaryFolder `logs\`: `system-YYYYMMDD.log` (Serilog, fixed levels) and `input-YYYYMMDD.log` (TSV, one append-only row per state transition, `ActivityInputLog`/`InputLogRow`). They join on the entry id and hold only redacted text (Slice 20 sanitizer runs first; raw text never touches disk). Abstractions `ISystemLog` + `ILogFolder`; `LogPaths` does a 7-day startup sweep; `ActivityLogWindow` has an Open logs button. `ActivityEntry.TimeToFirstAudio` and `SynthesisDuration` are in-memory only, never transmitted (no SDK, no network, no privacy-policy change). Dev-time OpenTelemetry plus a local Aspire dashboard is optional and never shipped; Aspire was rejected as a redistributable mechanism (see `STORE.md`).

Three read paths (Slices 10, 12; docs/plan/slices/10-live-activity-log-auto-read-fix.md): the UIA selection monitor (`ISelectionMonitor`, toggle Auto-read on selection), the clipboard monitor (`IClipboardMonitor`, `ClipboardFormatListener`/`WM_CLIPBOARDUPDATE`, toggle Auto-read on copy; the path for console and other no-UIA apps) and the always-on hotkey. The two gates are independent `ISettingsStore` flags (`AutoReadOnSelection`/`AutoReadOnCopy`, default on; an old `IsEnabled=false` migrates to both off), shown in the panel and the right-click menu. `ReadAloudService` de-dupes via `_lastTriggeredText` (hotkey clipboard echo, copy-on-select duplicates). The UIA monitor emits `SelectionCleared` only on a genuine empty selection; a transient cross-process failure is `SelectionOutcome.Unavailable` and stays silent. Diagnosing "selection does nothing": no log entry means no UIA event and nothing copied.
- Languages (Slice 103): `AppLanguages.Canonical` is the one place `no`/`nn` map to `nb`; route every language parse and engine code through it. `PackConstants.NaturallyReadable` must stay in step with `packs.yml`; Slice 104 adds `nb` when piper-nb lands. (Slice 103: docs/plan/slices/103-nordic-dutch-languages.md)
- Windows early start lives in `ReadAloudService.EarlyStart.cs`: coordinator calls happen under `_earlyGate`, never await under it, and CTS cancels are deferred until outside the gate. (Slice 142: docs/plan/slices/142-rtst-windows-early-start.md)
- Tray panel and flyout placement goes through `WindowPlacer` with `ResizeClient` (client-area sizing), never by sizing the outer window. (Slice 170: docs/plan/slices/170-tray-panel-controls-row-fits.md)
- Desktop acceptance (tray, hotkey, panel, startup task) runs in the headless Hyper-V test VM, never on the host desktop: `tools/desktop-vm/Invoke-DesktopTest.ps1 -App rtst` restores the `clean` checkpoint, installs the unsigned dev MSIX in the VM, runs `tools/desktop-vm/checks/rtst-*.ps1` and restores `clean`. Never install the dev package on the host or drive its tray, hotkey or clipboard; the VM has no network adapter so nothing reaches analytics. Add a new tray/panel check as `checks/rtst-<name>.ps1` and list it in the script's `$Apps` table. (Slice 163: docs/plan/slices/163-desktop-tests-in-hyperv-vm.md)

## Forced update (Slice 217)

- While an update is required, reads are refused through `IReadGate` inside `ReadAloudService` (hotkey, selection, copy, Play, file, skip); don't add a check in the callers. `READTHESTUPIDTEXT_TEST_APP_CONFIG` (test config for the update check) is read in Debug builds only, so the Release VM package ignores it. (Slice 217: docs/plan/slices/217-rtst-forced-update.md)
