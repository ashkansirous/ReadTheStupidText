# Slice 8 — Tray control panel window

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 8 — Tray control panel window.** Left-clicking the tray icon
      opens a borderless, always-on-top control panel (see Decision 12) holding
      every interactive control in one place: a **Play/Pause** toggle bound to
      `ReadAloudService.StateChanged`, a **YouTube-style speed slider** spanning
      the full `PlaybackRate` range (0.5×–2.0× in 0.05 steps, see Decision 13)
      with the current value shown beside it, a **Voice `ComboBox`** over
      `IVoiceCatalog.InstalledVoices` (current voice preselected), and
      **Auto-read** + **Launch at startup** `ToggleSwitch`es. A custom **✕**
      button sits at the top; the panel is **pinned** above all windows and
      closes only via the ✕ or a second tray left-click — both only hide it,
      never exit. Positioned bottom-right above the taskbar (work-area- and
      DPI-aware), sized to its content. New `ControlPanelWindow` (View) in the App
      project; the existing right-click `MenuFlyout` is left intact (Quit lives
      there, plus the five preset speeds). Confirm H.NotifyIcon `LeftClickCommand`
      and WinUI 3 `AppWindow`/`OverlappedPresenter` (borderless + always-on-top +
      positioning) via context7/Microsoft Learn before coding.
      *As built:* the panel is a `ControlPanelWindow` with a Mica backdrop, an
      `OverlappedPresenter` (`SetBorderAndTitleBar(true, false)`, `IsAlwaysOnTop`,
      non-resizable, hidden from switchers), sized/positioned in device pixels
      via `GetDpiForWindow` + `DisplayArea.WorkArea` (height measured from the
      content after first layout, fixing an initial overflow). It is pinned (no
      light-dismiss — that was tried and rejected in testing). Speed moved from
      the `ReadingSpeed` enum to the `PlaybackRate` value object (Decision 13);
      the panel slider sets any 0.05 step, the tray menu keeps the five
      `SpeedPresets`. Cross-surface sync is event-driven: `ReadAloudService`
      raises `SpeedChanged` / `VoiceChanged` / `EnabledChanged`, which
      `MainWindow` uses to keep the menu's checkmarks current when the change
      originates in the panel; the panel re-reads live state each time it opens,
      and raises `StartupStateChanged` so the menu's startup toggle follows.
      `LeftClickCommand` + `NoLeftClickDelay` open the panel without a
      double-click wait. *Refinements after testing:* the slider no longer
      clobbers the persisted default (its initial coercion to the 0.5 minimum is
      suppressed, so a fresh state opens at 1×); **Play** now starts a read of
      the selection/clipboard when idle (via `PlayPauseOrReadAsync`) instead of
      being a no-op, shared by the tray Play item; and when the active rate isn't
      a preset (e.g. 1.05×) the tray menu surfaces it as a checked item at the
      top of the speed group. *Voice quality:* the built-in WinRT
      `SpeechSynthesizer.AllVoices` voices sounded robotic and Narrator's neural
      voices are unreachable by a Store app — addressed by Slice 9, which replaces
      them with a bundled local neural engine.

## Verification (moved from plan.md)

- **Slice 8:** left-click the tray icon → the control panel opens above the
  taskbar, on top of all other windows, with no content clipped; click into
  another app → the panel **stays** on top (pinned, no light-dismiss); drag the
  speed slider → it moves in 0.05 steps across 0.5×–2.0× and the next/active read
  uses that rate; set a preset in the right-click menu → the slider reflects it
  on next open and the matching menu preset is checked; pick a voice in the
  `ComboBox` → the next read uses it; toggle Auto-read / Launch at startup →
  state matches the right-click menu (open the menu to confirm both surfaces
  agree); click ✕ or left-click the tray again → the panel hides but the app
  stays in the tray; Quit is reachable only from the right-click menu.
