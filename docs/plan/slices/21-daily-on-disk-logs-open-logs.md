# Slice 21 — Daily on-disk logs + open-logs button

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 21 — Daily on-disk logs + open-logs button.** ([#103](https://github.com/ashkansirous/ReadTheStupidText/issues/103)) (Decision 27) Add
      Serilog (rolling file) for the system log and a small thread-safe
      append-writer for the input log (one TSV row per activity-state
      transition, id-keyed, **redacted** text from Slice 20), both under the package
      `TemporaryFolder\logs`. Subscribe the input writer to `IActivityLog`
      events; thread the same id through the system log. Add the **open-logs**
      button to the top of `ActivityLogWindow` (`Launcher.LaunchFolderAsync`).
      Delete logs older than 7 days on startup. Promote the existing `Debug.WriteLine`
      UIA traces to the system log. **Built:** files are `system-YYYYMMDD.log` /
      `input-YYYYMMDD.log` (Serilog's rolling sink stamps the day as `YYYYMMDD` and
      can't prefix it, so both writers use that form for consistency, not the literal
      `yyyy-MM-dd-…` order). `ISystemLog` (Serilog) + `ILogFolder` in Application;
      `LogPaths`/`SerilogSystemLog`/`ActivityInputLog`/`InputLogRow` in Infrastructure;
      `ReadAloudService` logs each id-correlated action + exceptions.

## Verification (moved from plan.md)

- **Slice 21:** trigger a few reads → `…\TemporaryFolder\logs\<date>-input.log` has
  **one row per state transition** (pending→generatingAudio→reading→read each a new
  line) with the id and **redacted** text; `<date>-system.log` has the matching
  id-keyed diagnostic lines (and any exceptions). The Activity-Log window's new
  top button opens the `logs` folder in Explorer. Restart with a >7-day-old file
  present → it's deleted. Confirm **no** raw secret appears in either file.

## Notes moved from CLAUDE.md (2026-10-21)

Note: alongside the in-memory log, the app writes **two daily diagnostic files to
disk** (Slice 21, Decision 27) under the package **TemporaryFolder** (`logs\`): a
**system log** (`system-YYYYMMDD.log`) via **Serilog** (rolling file, Info/Debug/
Warning/Error, fixed levels — not user-configurable) carrying every id-correlated
action and exception, and an **input log** (`input-YYYYMMDD.log`) — a TSV the
`ActivityInputLog` writer appends **one row per activity-state transition** to
(append-only; it never rewrites a row), columns = the Activity-Log grid plus the
entry **id**, so the two files **join on the id**. Both store **redacted** text only
(the Slice 20 sanitizer runs first); raw text never touches disk. Serilog's rolling
sink stamps the day as `YYYYMMDD` and can't prefix it, so the files are
`system-…`/`input-…` rather than the `yyyy-MM-dd-…` order in Decision 27. The
abstractions are **`ISystemLog`** + **`ILogFolder`** (Application); `LogPaths`
(resolves the folder + a **7-day** startup retention sweep), `SerilogSystemLog`,
`ActivityInputLog` and the pure `InputLogRow` formatter live in Infrastructure.
`ActivityLogWindow` has an **Open logs** button (`Launcher.LaunchFolderAsync`). The
former `UiaSelectionMonitor` `Debug.WriteLine` traces now go through `ISystemLog`.
This is local-only (consistent with the "we collect nothing" stance) — no network,
no telemetry export.

