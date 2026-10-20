# Slice 12 — Split auto-read into two toggles

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 12 — Split auto-read into two toggles.** ([#48](https://github.com/ashkansirous/ReadTheStupidText/issues/48)) (Decision 22) Add
      `AutoReadOnSelection` + `AutoReadOnCopy` to `ISettingsStore` and its impl
      (both default on; migrate an old `IsEnabled=false` to both off). Gate the
      UIA `ISelectionMonitor` and the `IClipboardMonitor` independently in
      `ReadAloudService`. Surface two `ToggleSwitch`es in the right-click menu
      **and** the control panel, kept in sync via the existing event pattern.

## Verification (moved from plan.md)

- **Slice 12:** with both toggles on, selecting in Notepad reads and copying in
  the console reads. Turn **Auto-read on selection** off → selecting no longer
  reads but copying still does; turn **Auto-read on copy** off instead →
  copying no longer reads but selecting does; the hotkey reads in all
  combinations. The two switches match between the panel and the right-click
  menu. A profile upgraded from the old single toggle (`IsEnabled=false`) opens
  with both new toggles off.
