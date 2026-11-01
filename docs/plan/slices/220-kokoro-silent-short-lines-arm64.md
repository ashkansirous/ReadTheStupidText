### Slice 220 — Kokoro returns silent audio for short lines on arm64

*Planned (moved from parked by the user on 2026-11-01; the wave that claims it writes the new design first, starting from the NaN source).* Parked by the 2026-10-31 night run, draft PR #638 not merged: the S21 run rejected the design. The pause-mark padding never rescued a short line (bm_daniel: plain silent 2 of 4, dots-padded 3 of 4, comma-padded 3 of 4, gave up 3 of 4), and the silent takes are full length with NaN samples, not empty or all-zero, so the "short phoneme sequence collapses" hypothesis is wrong. Next: find where Kokoro's NaN output on arm64 comes from (onnxruntime threads or kernels, sherpa-onnx version, the input that triggers it) before any fix; the padding should not ship. Kept on the branch for reuse: the marker-file probe (`cache/rtst-short-line-probe`, confirmed on the phone) and the `short-lines` phone check. Fix the check's summary too: it prints `n/a` for a NaN peak. A new design needs the coordinator or the user, not another patch. Follow-up to Slice 210 (docs/plan/slices/210-kokoro-short-line-dropped.md).

**Touches:** `src/ReadTheStupidText.Application/Reading/SilentOutputGuard.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader*.cs`, `src/ReadTheStupidText.Infrastructure/Reading/NeuralSentenceSource.cs`, `tests/ReadTheStupidText.Tests/**`, `tools/phone/**`, `docs/plan/slices/220-kokoro-silent-short-lines-arm64.md`

#### Why

On the S21 (arm64) Kokoro returns truly empty audio (voiced ms 0 on every take, and on the sibling voice too) for short lines like "Ann." and "Help!". After Slice 210, 3 of 68 short lines still end in `gave up`. The emulator never shows it.

#### Hypothesis (written 2026-10-30, before any phone run)

Kokoro (sherpa-onnx, espeak-ng phonemizer) gets a phoneme sequence of only a few tokens for a 4-6 character line ("Ann.", "Help!"). On arm64 the model's predicted duration for such a short sequence collapses, and the output is empty or all-zero (S21: voiced ms 0 on every take, the sibling voice too, random per take; the x86_64 emulator is never affected). So a longer input sequence should produce real audio. **Not proven:** the phone run below decides it. The other possibilities it separates: the phonemizer returns nothing for the bare short word (then padding with pause marks will not help either), or the fault is in the arm64 ONNX kernels for any very short sequence (then padding with more *speech* tokens is needed, not pause marks).

#### Work

- [x] Probe: in a Debug build, for every short Kokoro line (at most 8 letters) the guard logs one `short line probe` line (no text) with samples, peak and voiced ms of an independent plain take and of a padded take of each style (dots `... t ...`, commas `, t,`). `tools/phone/checks/short-lines.ps1` (`run-checks.ps1 -App dino -Checks short-lines`, Dino, Debug, kokoro-en) runs the Slice 210 lines on the voices that failed, and prints one row per read plus a summary.
- [x] Fix, behind the existing guard, both platforms (the guard is shared, so Android, iOS and the Windows Dino head all get it): a silent short Kokoro line gets a plain retry (take 2, as in Slice 210) and then a retry (take 3) padded with pause marks (`ShortLinePadding`, default style dots), and the padded take is cut back to its voiced part plus 60 ms. The first take stays plain, so a line that already works is untouched; the `SilentOutputGuard` threshold is unchanged; the log never holds the line's text. Retry lines are Debug-logged (`retry take N (plain|padded, ...)`), the rescue line says `padded take N`.
- [x] Unit tests: `tests/ReadTheStupidText.Tests/ShortLinePaddingTests.cs` (pad text, trim, which lines/voices, padded retry after a silent take, plain first take, no text in the log, probe line) and the log parser in `tools/phone/test-run-checks.ps1`.
- [ ] Phone run (`short-lines` check), then set `ShortLinePadding.DefaultStyle` to the style that works, or move padding to the first take if plain takes fail often enough.

#### What the phone run must show

Run `pwsh -NoProfile -File tools/phone/run-checks.ps1 -App dino -Checks short-lines` on the S21 (Debug arm64 build of this branch, kokoro-en pack). Read the summary line and the rows:

- **Confirmed:** `Plain silent` is above 0 (the failure shows, about one read in five) AND `Dots silent` (or `Commas silent`) is 0 or near 0 on the same reads. Then the retries now rescue, and the run has `gave up 0/N`. Pick the style with fewer silent takes as `DefaultStyle`; if plain silent is above 10%, pad the first take too.
- **Zero samples vs all-zero:** each plain row says `ZERO SAMPLES` (sherpa returned an empty array) or `ALL-ZERO` (samples but under amplitude 0.01). Both are handled the same by the guard; the split only tells where the fault is (zero samples: the model or phonemizer output is empty; all-zero: the vocoder output is flat).
- **Rejected:** `Dots silent` and `Commas silent` are about as often as `Plain silent` (padding with pause marks does not help). Next candidates, not built: pad with speech (repeat the word and keep the first half), or a different `speed`/`length_scale` for short lines; and check the phonemizer output with a native log.
- **Not reproduced:** `Plain silent 0/N` over at least 48 probes. Then the emulator result holds on the S21 for this build and the Slice 210 failures need another look (build key, thermal state).
- Pass for the slice: no `gave up` in the 68-read Slice 210 set (`-Voices` all 17, `-Repeats 1`; about 17 minutes, so run it as three chunks of voices).

#### Review notes

- `DefaultStyle = Dots` and the 8-letter limit are guesses shipped before the phone run; Dots stays the default until the run picks the style. Short means letters/digits only, and any Chinese, Japanese or Korean character makes a line not short. A padded take is no longer discarded for low voiced density (the 20% threshold was never measured): it is cut back and judged by the normal voiced floor, so a lone click still fails it; the density of every padded take is Debug-logged (`padded take voiced density`, and `density` in the probe line) so the phone run can set a threshold. The sibling voice also speaks a short line padded.
- Accepted cost: the probe delays the first audio of a short Kokoro line by 3 extra syntheses, but only while the marker file `rtst-short-line-probe` is in the app's temp (Android cache) folder; the `short-lines` check creates it and removes it at the end, normal runs never probe. The check defaults to one voice (about 200 s); `-AllVoices` runs the four that failed.
- Probe stops between takes on cancel and a probe failure is logged instead of failing the read. The check fails when a read never ended or has no probe line.
- Review (medium) skipped: ProbeInThisBuild as compile-time #if DEBUG (store builds compile Release libraries; the probe is now a runtime marker file anyway); VoicedSpan helper duplication in Trim (style); padding on the first attempt or earlier for sibling (design choice, no measured failure; the phone run decides).

#### Verification

- The Slice 210 S21 short-line set (68 lines) runs with no `gave up`.

#### Not yet verified

- Everything on a device: the fix and the probe are built and unit tested; no phone run yet (see *What the phone run must show*). iOS: compile only (it builds with no errors; no Mac run).
- *Superseded 2026-10-31:* the S21 run below happened and rejected the padding. Still not verified: the 68-read Slice 210 set, the other voices, and any redesign.

#### Phone run 2026-10-31 (wave1-s220, S21, Debug arm64, voice bm_daniel, 4 short lines, 1 repeat)

- Two runner snags first: the dev app was not installed by run-checks, so `-Reinstall` was needed once; and the first run had no probe lines because the lines were served from the app's sentence cache (`cache/sentences`, 128 files) and never synthesised. After emptying that cache folder (dev app only) the probe ran. The marker file in `cache/` works (Path.GetTempPath() is the cache folder).
- Result (4 reads): plain take silent 2/4 (reads of 4 chars); dots-padded silent 3/4; comma-padded silent 3/4; **gave up 3/4**. The padded retry (take 3) never rescued: `padded take voiced density 0.00`, voiced ms 0/0/0, sibling voice also silent.
- Finding: the "silent" takes are not empty and not all-zero. They have the full length (26400, 27000, 25200 samples) and **peak NaN**: the model returns NaN samples on arm64 (a NaN is never above the voiced amplitude, so the guard counts it as silent). One 5-char read had a voiced plain probe (608 ms) but silent real takes, so it is random per take, as in Slice 210. A 6-char line was voiced on all three probes.
- Verdict: **Rejected** for the pause-mark padding (padded silent as often as plain, 3/4 vs 2/4). Not reproduced as a "short phoneme sequence" effect either, since the output has normal length. Next: find where NaN comes from (an arm64 ONNX kernel or the vocoder on a short input), treat NaN as a failed take and retry plain or on another thread/session, or run short Kokoro lines through a different path (e.g. a CPU fallback or a different speed); log `float.IsNaN` count per take. Sample is small (4 reads, 1 voice, one short run inside the 10-minute cap); the 68-read set was not run.
- Slice 210 set (68 reads): not reached. Other voices: not reached. Runner gap: `short-lines` summary prints `n/a` for a silent probe (NaN peak), so the split ZERO SAMPLES / ALL-ZERO is not shown; its parser should print NaN.

#### Redesign work (parked, feedback loop 2026-10-31; [note](../notes/2026-10-31-1000-feedback-loop-w1.md))

The `[x]` items above describe padding code that is only on draft #638 and should not ship. The open `Phone run ... set DefaultStyle` item is moot. Next, in this order:

- [ ] Count NaN samples per take (`float.IsNaN`), Debug-logged with no text, so it is clear whether every silent take is NaN. Verify: a `short-lines` run prints a NaN count for each silent probe.
- [ ] Find where the NaN comes from on arm64 (onnxruntime threads or kernels, the sherpa-onnx version, the input that triggers it) before any fix. Verify: a written cause with the run that shows it.
- [ ] Then design the fix from that cause (for example, treat a NaN take as failed and retry on another session or thread), reusing `tools/phone/checks/short-lines.ps1` from #638.
- [ ] `short-lines` summary prints `NaN` for a NaN peak, not `n/a`, so the ZERO SAMPLES / ALL-ZERO / NaN split shows. Verify: its parser test in `tools/phone/test-run-checks.ps1` with a NaN row.
