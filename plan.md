# Plan: ReadTheStupidText — Windows 11 read-aloud tray app

## Context

The user wants a lightweight Windows 11 utility ("ReadTheStupidText") that reads text
aloud at a user-chosen speed (1x / 1.25x / 1.5x / 1.75x / 2x), with a tiny
control beside the clock for pause/resume and speed, that auto-reads selected
text where possible and falls back to a global hotkey for apps that don't
expose their selection (terminals, CLI, Claude Code). It must run at startup
and ship through the Microsoft Store. The shape was settled in `scope.md`;
this plan turns it into ordered, shippable vertical slices.

## Decisions

1. **Framework:** C#/.NET + WinUI 3 (Windows App SDK), packaged as MSIX —
   keeps full Win32 capabilities (tray, global hotkey, cross-app read) while
   staying Store-installable. Pure UWP rejected (sandbox blocks all three).
2. **Speech:** WinRT `Windows.Media.SpeechSynthesis` rendered through
   `MediaPlayer` with `PlaybackRate` for live, pitch-corrected speed; system
   default Win11 voice in v1.
3. **Trigger:** UI Automation `TextPattern` selection monitoring as the
   primary auto-read path, **plus** a global hotkey (default `Ctrl+Win+R`)
   that simulates copy + reads the clipboard as the fallback.
4. **Control surface:** notification-area icon via `H.NotifyIcon.WinUI` —
   left-click flyout (Play/Pause + five speed buttons), right-click context
   menu (Pause, Enable/Disable, Settings, Quit). Global hotkey also toggles
   pause/resume.
5. **Startup:** packaged `StartupTask` (Windows App SDK), user-toggleable,
   starts minimized to tray.
6. **Persistence:** last speed + enabled state in
   `ApplicationData.Current.LocalSettings`; default speed 1x.
7. **Distribution:** Microsoft Store; design within Store/restricted-capability
   rules from day one.
8. **Naming:** repo stays `ReadTheStupidText`; product display name "ReadTheStupidText".
9. **Toolchain:** before writing any Windows App SDK / WinUI / H.NotifyIcon
   code, confirm current stable versions and APIs via context7
   (`/microsoft/windowsappsdk`, `/microsoft/winui`, `H.NotifyIcon`).
10. **Voice selection (scope change):** narrator voice is now **in scope** —
    chosen from installed Windows voices (`SpeechSynthesizer.AllVoices`),
    surfaced as a tray **Voice** submenu, persisted by `VoiceInformation.Id`,
    applied to the next read. Voices are an open, machine-dependent set, so
    they are modelled as a `VoiceInfo` record (not an enum). Voice *tuning*
    (pitch/volume/SSML) and in-app voice installation stay out of scope.
11. **Tray menu invocation (speed-defect root cause):** H.NotifyIcon's default
    `PopupMenu` context-menu mode renders a *native Win32 menu* built from the
    `MenuFlyout`. It invokes each item's **`Command`** only — the WinUI
    **`Click`** event never fires — and it renders a checkmark only for
    `ToggleMenuFlyoutItem`, not `RadioMenuFlyoutItem` (which falls through to the
    plain item case). That is why selecting a speed did nothing *and* never
    showed as selected. The fix drives every tray item through an `ICommand` and
    models the five speeds as mutually-exclusive `ToggleMenuFlyoutItem`s
    (single-selection managed in code). The same Command pattern applies to the
    Voice submenu in Slice 7. (The earlier "window never activated / XAML root"
    theory was wrong — confirmed against the H.NotifyIcon source.)
12. **Tray control panel (Slice 8):** left-clicking the tray icon opens a
    **borderless always-on-top `AppWindow`** (`OverlappedPresenter`,
    `IsAlwaysOnTop=true`, no system title bar) — not a `Flyout`/`Popup` and not
    a native `PopupMenu`. Reasons: the rich controls this panel needs (a speed
    **slider**, a voice **`ComboBox`**) cannot live in H.NotifyIcon's
    `PopupMenu` (the same native-menu limitation behind Decision 11), and a
    WinUI `Flyout` has no usable anchor on the zero-size hidden tray window. A
    real window positions reliably above the taskbar, is naturally topmost
    ("hovers over all windows"), and **light-dismisses** by closing on its own
    `Deactivated` event (click-away). It also carries a custom **✕** button at
    the top. The ✕ and click-away both only **hide** the window — the app keeps
    running in the tray; **Quit stays in the right-click menu only** (avoids an
    accidental quit from a light-dismiss surface). A **single** window instance
    is reused (left-click toggles show/hide), re-reading live state on open. The
    right-click context menu is **kept unchanged** — left-click → panel,
    right-click → menu (auto-read, launch at startup, quit). The panel is a
    View in the App project binding to the existing `ReadAloudService`,
    `IVoiceCatalog`, and `IStartupService`; no new Application/Infrastructure
    layers. Auto-read and launch-at-startup appear in **both** the panel and the
    right-click menu, so both surfaces read/write the same services to stay in
    sync.

## Changes

Ordered as vertical slices — each is end-to-end and independently runnable.

- [x] **Slice 0 — Project scaffold.** ([#3](https://github.com/ashkansirous/ReadTheStupidText/issues/3)) Create the WinUI 3 packaged (single-project
      MSIX) app via `dotnet new` / template (version confirmed through
      context7). Add `.gitignore` (VisualStudio + OS noise), `README.md`,
      `CLAUDE.md` (project conventions), and `AGENTS.md` (`@CLAUDE.md`). App
      boots to an empty window. Establish the layered folder structure
      (`Domain` / `Application` / `Infrastructure` / `App`/UI).
- [x] **Slice 1 — Clipboard-read on hotkey at chosen speed (smallest E2E).**
      ([#4](https://github.com/ashkansirous/ReadTheStupidText/issues/4)) Register the global hotkey; on press, read current clipboard text aloud
      via `SpeechSynthesis` + `MediaPlayer`. Tray icon present with a flyout
      exposing Play/Pause and the five speed buttons; speed changes apply live
      via `PlaybackRate`. This single slice proves TTS + speed + tray + hotkey.
- [x] **Slice 2 — Hotkey copies the current selection.** ([#5](https://github.com/ashkansirous/ReadTheStupidText/issues/5)) Extend the hotkey to
      simulate copy (send `Ctrl+C`) before reading, so the user can select in
      any app (incl. terminals/CLI/Claude Code) and have it read aloud. Persist
      last-used speed + enabled state to `LocalSettings`.
- [x] **Slice 3 — Auto-read on selection (UIA).** ([#6](https://github.com/ashkansirous/ReadTheStupidText/issues/6)) Add UI Automation
      `TextPattern` monitoring so selecting text in supporting apps
      (Notepad, modern apps, most browsers) auto-reads without the hotkey.
      Enable/Disable toggle in the tray menu gates this behavior. Hotkey
      remains the fallback for non-UIA apps.
- [x] **Slice 4 — Launch at startup.** ([#7](https://github.com/ashkansirous/ReadTheStupidText/issues/7)) Declared a packaged
      `windows.startupTask` extension (`desktop:Extension`, `Enabled="false"` so
      the user opts in) in `Package.appxmanifest`. `IStartupService` (Application)
      with `StartupTaskService` (Infrastructure) over `Windows.ApplicationModel.StartupTask`
      (`GetAsync`/`RequestEnableAsync`/`Disable`); the tray gains a **Launch at
      startup** toggle that reflects the *actual* OS state (enabling can be
      refused by the user/policy). The app already starts minimized to tray (its
      window is never shown), so startup launch needs no extra UI handling.
- [ ] **Slice 5 — Store packaging & CI.** ([#8](https://github.com/ashkansirous/ReadTheStupidText/issues/8)) Finalize the MSIX manifest
      (identity, capabilities + restricted-capability justification), signing,
      and a GitHub Actions workflow that builds + packages the MSIX artifact.
      Prepare Store submission assets.

Added after the initial plan — **tackled next, before Slice 4 (startup) and
Slice 5 (store):**

- [x] **Slice 6 — Fix speed control.** The five speed items didn't commit
      selection or change the rate (defect from Slices 1/3). Root cause: in
      H.NotifyIcon's default `PopupMenu` mode the native menu invokes each
      item's `Command` (not the WinUI `Click` event) and only `ToggleMenuFlyoutItem`
      renders a checkmark. Fix: drive all tray items through an `ICommand`
      (`RelayCommand`) and model the speeds as mutually-exclusive
      `ToggleMenuFlyoutItem`s with selection managed in code. `SetSpeed` already
      drives `MediaPlaybackSession.PlaybackRate` live and on the next read
      (verified in `SpeechReader`). Bug fix — no new layer, just App wiring.
- [x] **Slice 7 — Narrator voice selection.** Modelled a `VoiceInfo` record
      (Id, DisplayName, Language) in Domain; added `IVoiceCatalog` (installed
      voices + default) and `ISpeechReader.SetVoice(id)` in Application, with
      `WinRtVoiceCatalog` over `SpeechSynthesizer.AllVoices` and
      `SpeechReader.SetVoice` (sets `SpeechSynthesizer.Voice`) in Infrastructure.
      Tray flyout gains a **Voice** submenu (`MenuFlyoutSubItem` of
      `ToggleMenuFlyoutItem`s, checkmark on the current voice, Command-driven
      like the speeds — `RadioMenuFlyoutItem`/`Click` don't work in PopupMenu
      mode, see Decision 11). `ISettingsStore.VoiceId` persists the choice;
      `ReadAloudService` restores it on startup and `CurrentVoiceId` falls back
      to the system default when unset or no longer installed. A voice change
      applies to the next read (can't swap mid-utterance). WinRT voice APIs
      confirmed via Microsoft Learn docs first.
- [x] **Slice 8 — Tray control panel window.** Left-clicking the tray icon
      opens a borderless, always-on-top control panel (see Decision 12) holding
      every interactive control in one place: a **Play/Pause** toggle bound to
      `ReadAloudService.StateChanged`, a **YouTube-style speed slider** that
      snaps to the five `ReadingSpeed` stops (1x / 1.25x / 1.5x / 1.75x / 2x)
      with the current value shown beside it, a **Voice `ComboBox`** over
      `IVoiceCatalog.InstalledVoices` (current voice preselected), and
      **Auto-read** + **Launch at startup** `ToggleSwitch`es. A custom **✕**
      button sits at the top; the window light-dismisses on `Deactivated`
      (click-away) and a second left-click toggles it shut — both only hide it,
      never exit. Positioned bottom-right above the taskbar (work-area- and
      DPI-aware). New `ControlPanelWindow` (View) + thin view-model in the App
      project; the existing right-click `MenuFlyout` is left intact (Quit lives
      there). Confirm H.NotifyIcon `LeftClickCommand` and WinUI 3
      `AppWindow`/`OverlappedPresenter` (borderless + always-on-top +
      positioning + `Deactivated`) via context7/Microsoft Learn before coding.
      *As built:* the panel is a `ControlPanelWindow` with a Mica backdrop, an
      `OverlappedPresenter` (`SetBorderAndTitleBar(true, false)`, `IsAlwaysOnTop`,
      non-resizable, hidden from switchers), sized/positioned in device pixels
      via `GetDpiForWindow` + `DisplayArea.WorkArea`, light-dismissed on
      `Window.Activated`→`Deactivated`, with a short reopen guard so the tray
      click that dismissed it doesn't immediately reopen it. Cross-surface sync
      is event-driven: `ReadAloudService` now raises `SpeedChanged` /
      `VoiceChanged` / `EnabledChanged`, which `MainWindow` uses to keep the
      menu's checkmarks current when the change originates in the panel; the
      panel re-reads live state each time it opens, and raises
      `StartupStateChanged` so the menu's startup toggle follows. `LeftClickCommand`
      + `NoLeftClickDelay` open the panel without a double-click wait.

## Out of Scope

- Voice *tuning* beyond playback rate (pitch, volume, SSML prosody).
- Installing/downloading new voices or languages from within the app — the
  picker lists only voices already installed in Windows.
- Reading from non-UIA apps *without* the hotkey fallback.
- Non-Store / sideload as a primary distribution channel (MSIX may be
  sideloaded for testing, but Store is the target).
- A persistent/dockable settings window with its own taskbar presence, tabs,
  or hotkey remapping UI. The Slice 8 control panel is a transient,
  light-dismiss surface — every control still maps to one of the existing
  services; no new configurable settings are introduced.
- Pure UWP packaging.

## Verification

- **Slice 0:** `dotnet build` succeeds; app launches to an empty window;
  scaffolding files present.
- **Slice 1:** copy text manually, press the hotkey → text is spoken; click
  each speed button → speech speed changes live; Play/Pause works.
- **Slice 2:** select text in Notepad, a terminal, a browser, and Claude Code,
  press the hotkey → each is read aloud; restart app → last speed/enabled
  state restored.
- **Slice 3:** with auto-read enabled, select text in Notepad/browser →
  reads automatically; select in a terminal → does not auto-read but hotkey
  still works; Disable toggle stops auto-read.
- **Slice 4:** enable startup, reboot → app is running in the tray; disable →
  it is not.
- **Slice 5:** CI run produces a signed MSIX; install the MSIX on a clean
  Win11 machine and confirm all of the above; Store certification dry-run
  passes capability checks.
- **Slice 6:** open the tray flyout, click each speed → the item shows as
  selected (radio check) and stays selected on reopen; trigger a read → speech
  plays at that rate; change speed mid-playback → rate changes live.
- **Slice 7:** the Voice submenu lists the installed Windows voices with the
  current one checked; pick a different voice → the next read uses it; restart
  the app → the chosen voice is restored; uninstall that voice → falls back to
  the system default without error.
- **Slice 8:** left-click the tray icon → the control panel opens above the
  taskbar, on top of all other windows; drag the speed slider → it snaps to the
  five stops and the next/active read uses that rate; pick a voice in the
  `ComboBox` → the next read uses it; toggle Auto-read / Launch at startup →
  state matches the right-click menu (open the menu to confirm both surfaces
  agree); click ✕ or click another app → the panel hides but the app stays in
  the tray; left-click again → it reopens with current state; Quit is reachable
  only from the right-click menu.
- Manual UI checks driven through the running app; no browser E2E harness
  applies to a native tray app.
