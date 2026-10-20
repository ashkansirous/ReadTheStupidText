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
