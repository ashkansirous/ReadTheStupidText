### Slice 220 — Kokoro returns silent audio for short lines on arm64

*Planned 2026-10-30.* Follow-up to Slice 210 (docs/plan/slices/210-kokoro-short-line-dropped.md).

**Touches:** `src/ReadTheStupidText.Application/Reading/SilentOutputGuard.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader*.cs`, `src/ReadTheStupidText.Infrastructure/Reading/NeuralSentenceSource.cs`, `tests/ReadTheStupidText.Tests/**`, `tools/phone/**`, `docs/plan/slices/220-kokoro-silent-short-lines-arm64.md`

#### Why

On the S21 (arm64) Kokoro returns truly empty audio (voiced ms 0 on every take, and on the sibling voice too) for short lines like "Ann." and "Help!". After Slice 210, 3 of 68 short lines still end in `gave up`. The emulator never shows it.

#### Work

- [ ] Find out what sherpa-onnx returns on the S21 for these lines: zero samples or all-zero samples, and whether the phonemizer output is empty.
- [ ] Fix by construction (for example, pad the short input) so no short line is dropped, without lowering the `SilentOutputGuard` threshold.
- [ ] Unit test for the fix; keep the log free of the line's text.

#### Verification

- The Slice 210 S21 short-line set (68 lines) runs with no `gave up`.

#### Not yet verified

- Everything: planned only.
