### Slice 210 — A short Kokoro line is dropped ("gave up")

*In progress (wave 2026-10-30), draft PR.* Planned 2026-10-30. From the S21 night runs (docs/plan/notes/2026-10-29-s21-night-runs.md): a 5-character line in `kokoro:am_liam` was not read, with `gave up (voice kokoro:am_liam, 5 chars)`. Investigate first, then fix by what the evidence shows.

**Touches:** `src/ReadTheStupidText.Application/Reading/SilentOutputGuard.cs`, `src/ReadTheStupidText.Application/Reading/SiblingVoice.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs` (log line only), `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader.Synthesis.cs` (log line only), `src/ReadTheStupidText.Infrastructure/Reading/NeuralSentenceSource.cs` (log line only), `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/210-kokoro-short-line-dropped.md`

#### What the code says (2026-10-30, ground-in-code)

- The message comes from `SilentOutputGuard.cs:151`. The guard wraps every voice on every platform (`AndroidSupertonicSpeechReader.cs:788-790`, `NeuralSentenceSource.cs:107`, `IosSupertonicSpeechReader.Synthesis.cs:98`): 3 takes, then a sibling voice.
- Slice 149's rescue is Farsi-only in practice: `SiblingVoice.DropProne` holds only `fa-mana` and `SiblingVoice.Preferred` only Farsi Piper voices. So `am_liam` gets 3 takes, no sibling, and the line is dropped.
- **Likely** cause (not proven): `IsTooShort` (`SilentOutputGuard.cs:155-167`) is a false positive. A 5-letter line needs at least 150 ms of voiced audio (80 ms floor plus 30 ms per letter, at pace 1.0); Kokoro trims silence hard and may return a short but real word. The other possibility is that Kokoro really returned silence. The log has neither the voiced length nor the text, so it can't tell.

#### Work

1. **Find out (first sub-slice, its own PR is fine):**
   - [ ] The gave-up and retry log lines add the voiced milliseconds of each take and the threshold it was held to (no text: the text can be the user's).
   - [ ] Reproduce: read short lines (1-8 letters: "Yes.", "Oh no!", "Help!", names) with each Kokoro voice the apps ship, on the emulator first (Dino `open-book` / RTST `read` hooks), then on the S21. Record which voices and lengths fail and the voiced ms. Write it into this file.
2. **Fix by the evidence (both apps, every platform, since the guard is shared):**
   - [ ] If the takes had real audio under the threshold: make the threshold fit the engine (Kokoro gets its own floor and per-letter rate, measured from step 1), never a blanket lowering that lets Supertonic's real silences through.
   - [ ] If Kokoro truly returned silence: give Kokoro voices a sibling map (same language and gender, e.g. `am_liam` -> `am_michael`, chosen from the shipped voices) and, if a pattern shows, a short-line padding the way Slice 149 pads Farsi phrases.
   - [ ] Unit tests in `SilentOutputGuard`/`SiblingVoice` tests for the chosen fix.

#### Verification

- Fast: the guard and sibling tests.
- Emulator and S21: the step 1 short-line set reads every line with each Kokoro voice, no `gave up`.
- A listen to the rescued lines (by ear; stays in *Not yet verified* until the user hears them).
- iOS: compile in CI (shared guard).
