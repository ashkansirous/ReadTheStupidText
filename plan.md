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
    real window positions reliably above the taskbar and is **pinned topmost**
    ("hovers over all windows"). It is **not** light-dismissed: it stays open
    until the user clicks its **✕** or left-clicks the tray icon again (an
    earlier light-dismiss design was rejected in testing — the panel vanished as
    soon as you clicked into the app you were reading from). Closing only
    **hides** the window — the app keeps running in the tray; **Quit stays in the
    right-click menu only**. A **single** window instance is reused (left-click
    toggles show/hide), sized to its content and re-reading live state on open. The
    right-click context menu is **kept unchanged** — left-click → panel,
    right-click → menu (auto-read, launch at startup, quit). The panel is a
    View in the App project binding to the existing `ReadAloudService`,
    `IVoiceCatalog`, and `IStartupService`; no new Application/Infrastructure
    layers. Auto-read and launch-at-startup appear in **both** the panel and the
    right-click menu, so both surfaces read/write the same services to stay in
    sync.
13. **Speed is a decimal, not an enum (scope change, Slice 8):** reading speed
    moved from the five-value `ReadingSpeed` enum to a **`PlaybackRate`** value
    object — a decimal multiplier the user picks continuously from **0.5× to
    2.0× in 0.05 steps** (YouTube-style). The type clamps and snaps on
    construction so an out-of-range/off-step rate can't exist, persisted as a
    `double`. The control panel exposes the full range via a slider; the native
    tray menu (which can't host a slider) keeps **five quick presets**
    (`SpeedPresets`: 1/1.25/1.5/1.75/2), each just setting the rate to that
    value, with a checkmark only when the current rate exactly equals a preset.
    This supersedes the earlier "five speeds are an enum / never doubles"
    convention — speed is no longer a closed set.
14. **Neural voices via sherpa-onnx + Supertonic-3 (scope change, Slice 9):** the
    built-in Windows (OneCore) voices sound robotic and the high-quality
    Narrator **"Natural"/neural** voices are gated to Narrator — unusable by a
    Store MSIX app through any supported API (confirmed via Microsoft Learn +
    Microsoft Q&A; the only "unlock" is an unsupported HKLM registry hack a
    sandboxed MSIX can't do). So the app brings its own **local neural engine**:
    the **sherpa-onnx** runtime (Apache-2.0, .NET bindings) running the
    **Supertonic-3** voice model (`sherpa-onnx-supertonic-3-tts-int8-2026-05-11`,
    int8, ~145 MB, 10 voices — F1–F5 sid 0–4, M1–M5 sid 5–9, default M1).
    **Kokoro was evaluated and rejected**: its English voices live in a
    Chinese-focused multilingual bundle (v1.1 has *no* English male voice; v1.0
    is larger and ships GPL-adjacent `espeak-ng-data`). **Piper was rejected** as
    GPL (`espeak-ng` / `piper1-gpl`). Supertonic is English-first (31 languages,
    no Chinese baggage), comparable quality, ~half the size, and crucially uses a
    bundled `unicode_indexer` instead of espeak — **so there is no espeak GPL
    data concern**. The model (~145 MB) is **shipped inside the package** (under
    `VoiceModel/`, committed to the repo) — no first-run download, no network, so
    it works fully offline from install and needs no `internetClient` capability.
    The picker shows **only** the neural voices; a silent WinRT fallback remains
    only as a safety net should the packaged files ever be missing. sherpa-onnx
    generates PCM that we wrap as a stream and play
    through the existing `MediaPlayer`, so the 0.5–2.0× speed slider keeps
    working. Supersedes Decision 10's "voices come from installed Windows
    voices".

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
- [x] **Slice 9 — Local neural voices (sherpa-onnx + Supertonic-3).** Replaces the
      built-in voices with a local neural engine (see Decision 14). Added the
      **sherpa-onnx** runtime (Apache-2.0) + **Supertonic-3** model (Apache-2.0)
      via the `org.k2fsa.sherpa.onnx` NuGet package; Kokoro (Chinese-focused,
      no English male voice in the latest) and Piper (GPL) were rejected.
      `IVoiceModelService` (Application) + `SupertonicModelService` (Infrastructure)
      locate `sherpa-onnx-supertonic-3-tts-int8-2026-05-11`, which is **bundled in
      the package** under `VoiceModel/` (committed to the repo, ~145 MB) and read
      from `AppContext.BaseDirectory` — so it's ready offline at first launch, no
      download. `VoiceModelPaths` is just the model root dir, `SupertonicFiles`
      holds the layout. `SupertonicSpeechReader` builds an `OfflineTts` lazily
      (Supertonic config: duration_predictor / text_encoder / vector_estimator /
      vocoder + tts.json + unicode_indexer + voice.bin — no espeak, no lexicon),
      synthesizes PCM at 1×, wraps it as an in-memory WAV stream, and plays it
      through the existing `MediaPlayer` so the 0.5–2.0× slider stays live and
      pitch-corrected. `CompositeSpeechReader` routes to Supertonic (with the WinRT
      voice as a safety-net fallback only if the bundled files are missing);
      `NeuralVoiceCatalog` exposes **only** the Supertonic voices
      (`SupertonicVoiceTable`, the fixed 10-style set F1–F5/M1–M5 in sorted sid
      order, default *Male 1*); the panel/menu rebuild on
      `ReadAloudService.VoicesChanged`. A build-time target drops the duplicate
      `onnxruntime.dll` that WinML (`Microsoft.Windows.AI.MachineLearning`) and
      sherpa both ship, keeping sherpa's version-matched copy; the unused
      `systemAIModels` capability was removed (no `internetClient` is needed since
      the model ships in the package). sherpa-onnx C# API, model id, voice sid
      order (`generate_voices_bin.py` uses `sorted(*.json)`), and licensing all
      confirmed via context7 + NuGet + HF + the sherpa docs before coding.
      *Auto-read debounce (fix):* UIA fires a `SelectionChanged` per character
      while the user drag-selects, which previously triggered a burst of
      overlapping reads (the play/pause state bounced). `ReadAloudService` now
      debounces selections (500 ms quiet period, superseded reads cancelled via a
      swapped `CancellationTokenSource`), so a drag collapses into one read.
      *Unverified at build time (needs a real run):* neural audio output and the
      native runtime loading under package identity.

## Out of Scope

- Voice *tuning* beyond playback rate (pitch, volume, SSML prosody).
- Selecting from the Windows-installed voices — the picker offers **only** the
  bundled neural (Supertonic) voices (Slice 9). The neural model ships in the
  package; the app does not install or expose Windows/Narrator voices for
  selection (the WinRT voice is only an internal safety-net fallback if the
  packaged model files are ever missing).
- Reading from non-UIA apps *without* the hotkey fallback.
- Non-Store / sideload as a primary distribution channel (MSIX may be
  sideloaded for testing, but Store is the target).
- A persistent/dockable settings window with its own taskbar presence, tabs,
  or hotkey remapping UI. The Slice 8 control panel is a transient, tray-toggled
  surface (pinned topmost while open, hidden otherwise) — every control still
  maps to one of the existing services; no new configurable settings are
  introduced.
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
  taskbar, on top of all other windows, with no content clipped; click into
  another app → the panel **stays** on top (pinned, no light-dismiss); drag the
  speed slider → it moves in 0.05 steps across 0.5×–2.0× and the next/active read
  uses that rate; set a preset in the right-click menu → the slider reflects it
  on next open and the matching menu preset is checked; pick a voice in the
  `ComboBox` → the next read uses it; toggle Auto-read / Launch at startup →
  state matches the right-click menu (open the menu to confirm both surfaces
  agree); click ✕ or left-click the tray again → the panel hides but the app
  stays in the tray; Quit is reachable only from the right-click menu.
- **Slice 9:** on first launch (offline is fine — the model ships in the
  package) → the picker immediately lists the Supertonic neural voices (default
  Male 1; 5 male + 5 female) and the tray Voice submenu is present; a read uses
  the selected neural voice and sounds natural; the speed slider still changes
  the rate live; pick a different voice → the next read uses it; restart → the
  chosen voice is restored. **Auto-read debounce:** drag-select a sentence → it
  is read **once** after the selection settles (no burst of play/pause and no
  overlapping reads). Confirm the packaged build runs under package identity and
  that audio plays (the build-time ORT dedupe didn't break the native engine).
- Manual UI checks driven through the running app; no browser E2E harness
  applies to a native tray app.
