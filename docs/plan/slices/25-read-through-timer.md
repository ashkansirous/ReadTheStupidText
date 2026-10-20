# Slice 25 — Read-through timer (elapsed/total)

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 25 — Read-through timer (elapsed/total).** (Decision 33, Batch 5) Show
      `elapsed/total` in the transport row. Track each chunk's real synthesized
      duration as it completes; once every chunk for the current read has finished,
      sum them into the total. Before that, the total is unknown.
      New Application record (e.g. `ReadTiming(TimeSpan Elapsed, TimeSpan? Total)`)
      exposed via a new `ISpeechReader.TimingChanged` event (parallel to the existing
      `ProgressChanged`), raised roughly once per second while reading and whenever
      `Total` changes. A pure formatter renders `mm:ss` / `--:--` (Decision 33). Wire
      into `ControlPanelWindow`'s transport row next to the existing progress bar, the
      same way progress is wired today. Unit-test the formatter and the
      duration-accumulation sequencing against the user's own worked example
      (`00:00/--:--` → ticks → `00:03/02:23` the instant the last chunk lands).
      **Built:** `ReadTiming(TimeSpan Elapsed, TimeSpan? Total)` +
      `ReadTimingFormatter` (mm:ss/mm:ss, `--:--` for unknown) live in
      `Application.Reading`, unit-tested directly. The duration-accumulation logic
      is a new pure `Infrastructure.Reading.ReadTimingTracker` (`Start`/
      `RecordChunkDuration`/`AdvancePastChunk`/`CurrentTiming`/`Reset`) shared by both
      `ISpeechReader` implementations, so it's unit-testable without the WinRT engine
      — `ReadTimingTrackerTests` reproduces the worked example exactly. In
      `SupertonicSpeechReader`, each chunk's real duration is computed from its PCM
      sample count/rate the instant `Generate` returns (not when it plays), recorded
      into the tracker, and — the instant that completes the total — `TimingChanged`
      fires immediately, bypassing the ~1s throttle used for ordinary ticks
      (`OnPositionChanged`); `AdvancePastChunk` folds a finished chunk's duration into
      the elapsed baseline in the ordered consume loop. The WinRT fallback
      (`SpeechReader`) has one "chunk"; its total is learned from `NaturalDuration`
      the first tick it's populated (mirroring the existing progress-bar code), also
      firing immediately that tick. `CompositeSpeechReader` forwards `TimingChanged`
      from whichever engine is active, same as `ProgressChanged`.
      `ReadAloudService.TimingChanged` passes it through; `ControlPanelWindow` adds a
      `TimerText` label under the transport row, updated via `ReadTimingFormatter` and
      reset to `00:00/--:--` on the same Idle transition that zeroes the progress bar.
      `SpeechSynthesisStream.Duration` was considered for the fallback but dropped —
      context7 (`/websites/learn_microsoft_en-us_uwp_api`) couldn't confirm the
      property exists, so `NaturalDuration` (already relied on elsewhere in this file)
      was used instead. UI/native code isn't unit-tested per the project's test story;
      runtime check under the (Package) profile remains.

## Verification (moved from plan.md)

- **Slice 25:** trigger any existing read path → timer shows `00:00/--:--`, ticks
  once per second, then flips to the real total the instant synthesis of every
  chunk finishes, matching the worked example (`00:00/--:--` → … → `00:03/02:23`).
  `dotnet test` covers the formatter and the duration-accumulation sequencing.
