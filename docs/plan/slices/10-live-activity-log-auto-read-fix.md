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
