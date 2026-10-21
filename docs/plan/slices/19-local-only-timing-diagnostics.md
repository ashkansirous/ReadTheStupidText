# Slice 19 — Local-only timing diagnostics

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 19 — Local-only timing diagnostics.** ([#86](https://github.com/ashkansirous/ReadTheStupidText/issues/86)) (Decision 26) Record
      time-to-first-audio (entry → first `Playing`) and synthesis duration per read
      into the existing in-memory `IActivityLog`/`ActivityEntry`, and surface them
      as column(s) in `ActivityLogWindow` — nothing transmitted, no third-party, no
      policy change. Document the optional dev-only OpenTelemetry + local Aspire
      dashboard path in `CLAUDE.md`/`STORE.md` (not shipped). Lets the Slice 17/18
      gains be measured on the user's machine instead of guessed.

## Verification (moved from plan.md)

- **Slice 19:** trigger a read → the activity-log row shows a
  **time-to-first-audio** (and synthesis-duration) value; values are plausible and
  drop noticeably after Slice 17's warm-up vs a cold first read. Nothing leaves
  the device (no network call, no third-party SDK referenced). The optional dev
  OpenTelemetry/Aspire-dashboard path is documented but not part of the shipped
  package.

## Notes moved from CLAUDE.md (2026-10-21)

**Local-only timing diagnostics (Slice 19, Decision 26).** Each `ActivityEntry`
carries two nullable diagnostics — **`TimeToFirstAudio`** (entry created → the
reader's first `Playing` transition) and **`SynthesisDuration`** (GeneratingAudio →
first `Playing`, the metric Slice 17's warm-up shrinks). `ReadAloudService` measures
them with monotonic `Stopwatch` timestamps and calls `IActivityLog.RecordTiming` at
the first-Playing transition; `ActivityLogWindow` shows them as the **First audio**
and **Synth** columns (`TimingText` formats ms/s, blank until measured). This stays
**in-memory, cleared on restart, never transmitted** — no third-party SDK, no
network, no privacy-policy change (consistent with the "we collect nothing" stance).
For *dev-time* tuning only, you may optionally instrument the read pipeline with
**OpenTelemetry** `Activity`/`Meter` and attach a **local Aspire dashboard** (an OTLP
viewer run on the dev machine) — **not shipped** in the MSIX; .NET Aspire was
rejected as a redistributable mechanism (it orchestrates *distributed* apps at dev
time and would reintroduce telemetry-export/cost concerns). See `STORE.md`.
