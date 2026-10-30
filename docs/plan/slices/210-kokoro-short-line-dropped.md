### Slice 210 — A short Kokoro line is dropped ("gave up")

*Built 2026-10-30, verification criterion not met (see below).* Planned 2026-10-30. From the S21 night runs (docs/plan/notes/2026-10-29-s21-night-runs.md): a 5-character line in `kokoro:am_liam` was not read, with `gave up (voice kokoro:am_liam, 5 chars)`. Investigate first, then fix by what the evidence shows.

**Touches:** `src/ReadTheStupidText.Application/Reading/SilentOutputGuard.cs`, `src/ReadTheStupidText.Application/Reading/SiblingVoice.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs` (log line only), `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader.Synthesis.cs` (log line only), `src/ReadTheStupidText.Infrastructure/Reading/NeuralSentenceSource.cs` (log line only), `tests/ReadTheStupidText.Tests/**`, `docs/plan/slices/210-kokoro-short-line-dropped.md`

#### What the code says (2026-10-30, ground-in-code)

- The message comes from `SilentOutputGuard.cs:151`. The guard wraps every voice on every platform (`AndroidSupertonicSpeechReader.cs:788-790`, `NeuralSentenceSource.cs:107`, `IosSupertonicSpeechReader.Synthesis.cs:98`): 3 takes, then a sibling voice.
- Slice 149's rescue is Farsi-only in practice: `SiblingVoice.DropProne` holds only `fa-mana` and `SiblingVoice.Preferred` only Farsi Piper voices. So `am_liam` gets 3 takes, no sibling, and the line is dropped.
- **Likely** cause (not proven): `IsTooShort` (`SilentOutputGuard.cs:155-167`) is a false positive. A 5-letter line needs at least 150 ms of voiced audio (80 ms floor plus 30 ms per letter, at pace 1.0); Kokoro trims silence hard and may return a short but real word. The other possibility is that Kokoro really returned silence. The log has neither the voiced length nor the text, so it can't tell.

#### Work

1. **Find out (first sub-slice, its own PR is fine):**
   - [x] The gave-up and retry log lines add the voiced milliseconds of each take and the threshold it was held to (no text: the text can be the user's).
   - [x] Reproduce: read short lines (1-8 letters: "Yes.", "Oh no!", "Help!", names) with each Kokoro voice the apps ship, on the emulator first (Dino `open-book` / RTST `read` hooks), then on the S21. Record which voices and lengths fail and the voiced ms. Write it into this file.
2. **Fix by the evidence (both apps, every platform, since the guard is shared):**
   - [x] (Not the cause: not needed.) If the takes had real audio under the threshold: make the threshold fit the engine (Kokoro gets its own floor and per-letter rate, measured from step 1), never a blanket lowering that lets Supertonic's real silences through.
   - [x] If Kokoro truly returned silence: give Kokoro voices a sibling map (same language and gender, e.g. `am_liam` -> `am_michael`, chosen from the shipped voices) and, if a pattern shows, a short-line padding the way Slice 149 pads Farsi phrases.
   - [x] Unit tests in `SilentOutputGuard`/`SiblingVoice` tests for the chosen fix.

#### Verification

- Fast: the guard and sibling tests.
- Emulator and S21: the step 1 short-line set reads every line with each Kokoro voice, no `gave up`.
- A listen to the rescued lines (by ear; stays in *Not yet verified* until the user hears them).
- iOS: compile in CI (shared guard).

#### Device evidence (emulator, 2026-10-30)

Build key 4d820f2a936f (the guard's new measure lines), Dino x86_64 Debug on the emulator, `kokoro-en` v2 pack, script `tmp/s210-shortlines.sh` (sets `LastNeuralVoiceId.Kokoro`, then the `read` hook; by app pid).

- Lines: "Yes.", "Oh no!", "Help!", "Ann.", "No." (2-5 letters; floor 80-150 ms). Voices: all 17 English Kokoro voices (af_heart, af_bella, af_nicole, af_sky, af_nova, af_sarah, af_kore, af_aoede, bf_emma, bf_lily, bf_alice, am_puck, am_santa, bm_fable, am_liam, am_michael, bm_daniel). 85 reads, each logged `first unit (read, voice kokoro:<id>)` with the right voice.
- Guard lines: **0** `rescued`, **0** `gave up`, **0** `passed near the floor` (the near-floor line fires when the first take is under 1.25 times the floor). So every first take held more than 1.25 times its floor (at least 100-188 ms per line); the exact voiced ms of a clean pass is not logged, so no per-line figure exists for these. Only rescue, gave-up and near-floor lines carry ms.
- Caveats: the reads were 4 s apart, so a read could be cut by the next one (51 `READ_END` of 85); each still reached `first unit`, which is logged before playback. Not reached: Spanish, Portuguese and Chinese Kokoro voices, "Hi, Tom!" and "Wow!" (dropped to fit 10 minutes), the S21, and the original failure (the 2026-10-29 `am_liam` 5-character line did not reproduce on the emulator; the S21 may differ).

#### Device evidence (S21, 2026-10-30)

Build key 4d820f2a936f (Debug arm64, `uk.sirous.dinoreader.dbg`, installed over wireless adb; the store install was not touched), `kokoro-en` v2 pack, script `tmp/s210-shortlines-s21.sh` (voice set in the dev app's prefs, Dino broadcast `read` hook, each read waited for `READ_END`; by app pid). Lines "Yes." (4 chars), "Oh no!" (6), "Help!" (5), "Ann." (4); all 17 English Kokoro voices; 68 reads, all reached `READ_END` (a gave-up line still ends the read, silently).

- **The original failure reproduces on the S21 and is true silence, not a threshold false positive.** Every guard line shows `voiced ms 0/0/0`: Kokoro returned 0 ms of voiced audio on all three takes, against a floor of 90-120 ms. The emulator (x86_64) never did this (0 guard lines in 85 reads).
- **14 of 68 reads gave up** (a 4-6 char line, nothing spoken): bm_daniel 3, bf_alice 2, bf_emma 2, bm_fable 2, am_puck, am_santa, af_nova, af_kore, af_aoede 1 each. Not seen with af_heart, af_bella, af_nicole, am_michael, bf_lily, af_sarah (these may still fail on another run: the failures are random per take).
- Rescues: 8 by take 2 (voiced 431-776 ms), 4 by take 3 (457-601 ms), 2 by the sibling voice (am_liam; this build has a sibling for am_liam). 0 `passed near the floor` lines. Takes that did return audio were well over the floor, so the floor is not the problem and raising or lowering it will not help.
- Reading: the first take of a short line is often silent and each retry is an independent chance (retry 2 and 3 rescue about half); the sibling map covers only some voices (`sibling 0` appears in the gave-up lines of am_puck). Fix direction: a sibling map for every shipped Kokoro voice, and/or a fourth take, and/or a short-line padding like Slice 149's.
- Not reached: Spanish, Portuguese, Chinese Kokoro voices; "Hi, Tom!" and "Wow!"; a listen to the rescued lines.

#### Device evidence (S21 recheck, 2026-10-30)

Build key ff0bfda456c0 (Debug arm64, `uk.sirous.dinoreader.dbg`, a sibling for every Kokoro voice), same script (`tmp/s210-shortlines-s21b.sh`, output `tmp/s210-s21b-out.txt`), same 4 lines ("Yes.", "Oh no!", "Help!", "Ann.") x 17 English Kokoro voices, 68 reads, every read waited to `READ_END` (68 of 68 reached it). One clean run (17 pids in sequence).

- **Not passed: 3 of 68 reads still gave up** (silent, `voiced ms 0/0/0` on all three takes and the sibling returned `sibling 0`): bm_daniel "Ann." (4 chars), bf_alice "Help!" (5) and "Ann." (4). Before: 14 of 68.
- 9 reads had a silent first take: 6 rescued (1 by take 3: af_kore, voiced 452 ms; 5 by the sibling voice: bm_daniel 479, af_nova 430, af_aoede 579, bf_emma 776 and 660 ms), 0 by take 2, 3 gave up. 0 `passed near the floor`.
- Silence still comes in 0 ms takes (not a threshold problem). Both give-ups are a voice whose sibling also returned 0 ms; failures are random per take, so a rerun may differ.
- Not reached: Spanish, Portuguese, Chinese voices; "Hi, Tom!" and "Wow!"; a listen to the rescued lines.

#### Result (2026-10-30)

*Changed while building:* the cause is proven on the S21 (arm64): Kokoro returns truly silent output (voiced ms 0) for short lines; it is not a threshold false positive, and the emulator never shows it (85 reads clean). So the fix is the sibling map, not a Kokoro threshold. `SilentOutputGuard` now logs voiced ms, needed ms, pace and sibling on rescue and gave-up (never the text); a near-floor line (under 1.25x needed) goes to the debug log only. `NeededMs` is `internal`. `SiblingVoice` maps every shipped Kokoro voice to a same-language, same-gender sibling; `ef_dora` and `pf_dora` have none (the only woman in their language). A test checks that every shipped Kokoro voice has a sibling.

*From review:* high pass: findings 4, 5, 6 applied; 1, 2, 3, 7, 8 skipped. Medium pass: 1, 2 applied; 3, 4, 5 skipped.

*Outcome:* S21 drops went from 14/68 to 3/68 (`bm_daniel` "Ann.", `bf_alice` "Help!" and "Ann.": all takes and the sibling gave 0 ms). The criterion "no `gave up` on the S21" is **not met**. Two strikes on device in this area, so no more patching: follow-up Slice 220 (to be planned in the index pass) finds why Kokoro synthesis returns empty audio for short lines on arm64 (sherpa-onnx output length or zeros, padding, phonemizer) and fixes it by construction.

#### For CLAUDE.md

- Kokoro on arm64 can return truly silent audio for short lines; `SilentOutputGuard.cs` rescues with `SiblingVoice.cs`, which must map every shipped Kokoro voice to a same-language, same-gender sibling (a test enforces it). Add a sibling there when adding a Kokoro voice; don't lower the threshold. Never log the line's text. (Slice 210: docs/plan/slices/210-kokoro-short-line-dropped.md)

#### Not yet verified

- Listening to the rescued lines (the user's ear).
- Spanish, Portuguese and Chinese Kokoro voices (`ef_dora` and `pf_dora` have no sibling).
- iOS: the guard is shared; CI compile only.
- No `gave up` on the S21: 3/68 still drop; moved to Slice 220.
