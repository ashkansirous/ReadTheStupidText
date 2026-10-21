# Slice 10 — Live activity log + auto-read fix

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 10 — Live activity log + auto-read fix.** (see Decision 15) Adds a
      separate, resizable **activity-log window** opened from the right-click tray
      menu ("Show activity log"), showing read activity **live**. New
      `IActivityLog`/`ActivityLog` (Application, in-memory observable ring buffer,
      ~200, `EntryAdded`/`EntryChanged`) + Domain `ActivityEntry` /
      `ActivityState` (pending/reading/read/ignored/interrupted/failed) /
      `ActivitySource` (auto-read/hotkey/manual). `ReadAloudService` now opens an
      entry per intercepted text and drives its state: a new selection or deselect
      **supersedes** the active entry (pending→`ignored`; reading→`interrupted`,
      pausing the reader), the debounce elapsing flips it to `reading`, the reader
      returning to `Idle` marks `read`, and a synth/playback exception marks
      `failed`. The UIA monitor (`ISelectionMonitor`) gained a `SelectionCleared`
      event (emitted once on the transition to an empty selection) so a deselect
      interrupts an in-progress read. `ActivityLogWindow` (normal resizable
      window, single-instance, in switchers) renders rows via `ActivityRowVm`
      (state updates in place); seeds from existing entries on open. *Diagnostic
      for the "selecting text does nothing" bug:* the path is sound and builds
      clean; the log is the lens — if selecting in a UIA app (Notepad) produces an
      entry that reaches `read`, auto-read works and the originally-tested app
      simply exposes no UIA text (hotkey is the fallback); if Notepad shows
      nothing, the monitor isn't firing. **Needs a runtime check to confirm the
      root cause** (can't run the UI/UIA here). Logs all read sources, tagged.

## Verification (moved from plan.md)

- **Slice 10:** right-click tray → **Show activity log** opens the log window.
  Select text in a UIA app (Notepad): an entry appears `pending`, flips to
  `reading`, then `read`. Quickly select "this is" then "this is a new text":
  the first row → `ignored`, the second runs `pending`→`reading`→`read`. While a
  read is playing, select something else or **deselect** → the row → `interrupted`
  and audio stops. Trigger a synth error (or pull the model) → `failed`. Hotkey
  and manual Play reads also appear, tagged by source. **Bug:** selecting text in
  the app you reported now either shows an entry (and reads) or shows nothing —
  if nothing, the log confirms that app exposes no UIA text (use the hotkey),
  which we verify against Notepad where it must work.

## Notes moved from CLAUDE.md (2026-10-21)

Note: read activity flows through **`IActivityLog`** (Application — an in-memory,
observable ring buffer; `ActivityEntry`/`ActivityState`/`ActivityTrigger`/
`WindowSource` in Domain — plus **`ActivityReason`** for *why* an entry deviated).
`ReadAloudService` opens an entry per intercepted text and transitions it
(pending→**generatingAudio**→reading→read, or ignored/interrupted/failed), tagging
each deviation with an `ActivityReason` (`NewSelection`/`Deselected`/`Error`) shown
in the log's **Reason** column. `GeneratingAudio` ("Generating audio") covers the
synthesis wait before any audio plays and flips to `reading` on the reader's first
`Playing` transition. Each entry also records its **`ActivityTrigger`** (auto-read /
hotkey / manual / **clipboard**) in the **Trigger** column and the originating
foreground window (`WindowSource` = app + title, via `IForegroundWindow`) in the
**Source** column. `ActivityLogWindow` (a normal resizable window, opened from the
right-click tray menu) renders it live.


**Three auto-read/read paths feed the log:** the **UIA selection monitor**
(`ISelectionMonitor`, gated by the **Auto-read on selection** toggle), the
**clipboard monitor** (`IClipboardMonitor` = `ClipboardFormatListener`, a Win32
`WM_CLIPBOARDUPDATE` listener on the tray window's handle — the path for the
**console**/Windows Terminal and other apps with no UIA text selection; gated by
the **Auto-read on copy** toggle), and the global **hotkey** (always on). The two
auto-read gates are independent `ISettingsStore` flags (`AutoReadOnSelection` /
`AutoReadOnCopy`, both default on; an old single `IsEnabled=false` migrates to
both off — Decision 22), each surfaced as a `ToggleSwitch` in the control panel
and the right-click menu. `ReadAloudService` de-dupes across them via
`_lastTriggeredText` so the hotkey's own copy (clipboard echo) and a
copy-on-select duplicate of a UIA selection don't read twice. The UIA monitor
emits **`SelectionCleared`** only on a *genuine* empty selection (deselect) so an
in-progress read is interrupted — a **transient cross-process read failure** is
kept distinct (`SelectionOutcome.Unavailable`) and left silent, so a selection
event re-fired mid-selection no longer falsely ignores/interrupts the read. The
log is the diagnostic for "selection does nothing": no entry ⇒ no UIA selection
event *and* nothing copied (with auto-read on, copying in the console now reads;
the hotkey is the always-available fallback). See Slice 10 / Decision 15 in
`docs/plan/decisions.md`.

