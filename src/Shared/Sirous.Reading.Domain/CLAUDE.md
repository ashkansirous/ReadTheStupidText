# CLAUDE.md — Domain

Loaded when you work under `src/Shared/Sirous.Reading.Domain` (`net11.0`): entities, value objects, enums. No framework deps.

- Reading speed is the **`PlaybackRate`** value object: a decimal multiplier (0.5-2.0, snapped to 0.05 steps), *not* an enum, because the user picks a continuous rate. `SpeedPresets` exposes the common stops (1/1.25/1.5/1.75/2) for the native tray menu. The range and step are enforced in the type.
- Genuinely closed sets (reading state, etc.) stay `enum`s; stringify only at the UI boundary.
- `SupertonicVoiceTable`/`SupertonicFiles` (Slice 31) are pure voice-catalog data, moved here from Infrastructure so Mobile can reuse them.
