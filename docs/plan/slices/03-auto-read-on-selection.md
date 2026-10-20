# Slice 3 — Auto-read on selection (UIA)

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 3 — Auto-read on selection (UIA).** ([#6](https://github.com/ashkansirous/ReadTheStupidText/issues/6)) Add UI Automation
      `TextPattern` monitoring so selecting text in supporting apps
      (Notepad, modern apps, most browsers) auto-reads without the hotkey.
      Enable/Disable toggle in the tray menu gates this behavior. Hotkey
      remains the fallback for non-UIA apps.

## Verification (moved from plan.md)

- **Slice 3:** with auto-read enabled, select text in Notepad/browser →
  reads automatically; select in a terminal → does not auto-read but hotkey
  still works; Disable toggle stops auto-read.
