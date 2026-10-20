# Slice 17 — Warm the neural engine at startup

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 17 — Warm the neural engine at startup.** ([#84](https://github.com/ashkansirous/ReadTheStupidText/issues/84)) (Decision 24) The biggest
      single win and the smallest change. After `IVoiceModelService` locates the
      model, eagerly build the `OfflineTts` on a background thread and run one tiny
      throwaway synthesis to warm the ONNX graph, so the first real read no longer
      pays the cold-start cost. Keep the lazy `EnsureTts()` as the fallback if a
      read arrives before warm-up finishes. No UI-thread blocking; idempotent.

## Verification (moved from plan.md)

- **Slice 17:** launch the app, wait a moment, then immediately select text in a
  UIA app (or press the hotkey) for the **first** read of the session → speech
  starts promptly with no multi-second "is it broken?" stall (compare against the
  pre-warm build, where the first read lagged). Subsequent reads are unaffected.
  Confirm the UI is responsive during the startup warm-up (no freeze) and that a
  read fired *before* warm-up completes still works (falls through to lazy
  `EnsureTts()`), and that audio still plays under package identity.
