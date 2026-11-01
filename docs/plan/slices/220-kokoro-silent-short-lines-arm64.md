### Slice 220 — Kokoro returns silent audio for short lines on arm64

*Built (wave 2026-11-01): part 1 PR #638, part 2 PR #685.* Earlier note (history): parked by the 2026-10-31 night run, draft PR #638 not merged: the S21 run rejected the design. The pause-mark padding never rescued a short line (bm_daniel: plain silent 2 of 4, dots-padded 3 of 4, comma-padded 3 of 4, gave up 3 of 4), and the silent takes are full length with NaN samples, not empty or all-zero, so the "short phoneme sequence collapses" hypothesis is wrong. Next: find where Kokoro's NaN output on arm64 comes from (onnxruntime threads or kernels, sherpa-onnx version, the input that triggers it) before any fix; the padding should not ship. Kept on the branch for reuse: the marker-file probe (`cache/rtst-short-line-probe`, confirmed on the phone) and the `short-lines` phone check. Fix the check's summary too: it prints `n/a` for a NaN peak. A new design needs the coordinator or the user, not another patch. Follow-up to Slice 210 (docs/plan/slices/210-kokoro-short-line-dropped.md).

**Touches:** `.github/workflows/packs.yml`, `src/ReadTheStupidText.Application/Reading/PackConstants.cs`, `.github/CLAUDE.md`, `docs/voice-packs.md`, `docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md` (part 2, widened at claim), `src/ReadTheStupidText.Application/Reading/SilentOutputGuard.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader*.cs`, `src/ReadTheStupidText.Infrastructure/Reading/NeuralSentenceSource.cs`, `tests/ReadTheStupidText.Tests/**`, `tools/phone/**`, `docs/plan/slices/220-kokoro-silent-short-lines-arm64.md`

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
- Review (medium, wave 2) fixed: a give-up take has its non-finite samples zeroed before it is handed to the player; the single-thread twin is keyed by the model it was built from (root, folder, language), disposed when the read ends (Stop, or the last owner leaves), and a failed twin build is not retried for the rest of that read (the next NaN take takes the plain retry); `ShortLineProbe.AppliesTo` has boundary and CJK tests. Android and iOS alike.
- Review (medium, wave 2) skipped: NaN-as-failed on every engine (the design: a NaN sample is never valid speech on any engine, and the phone run decides whether the cost shows); preparing the twin once per read (a no-op when the twin is already built: the call only takes the lock and finds it). Finding 5 (last vs best stats): not a bug, a passing take returns at once, so the loop only reaches the end when every take failed.

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

- [x] Count NaN samples per take (`float.IsNaN`), Debug-logged with no text, so it is clear whether every silent take is NaN. Verify: a `short-lines` run prints a NaN count for each silent probe.
- [ ] Find where the NaN comes from on arm64 (onnxruntime threads or kernels, the sherpa-onnx version, the input that triggers it) before any fix. Verify: a written cause with the run that shows it.
- [x] Then design the fix from that cause (for example, treat a NaN take as failed and retry on another session or thread), reusing `tools/phone/checks/short-lines.ps1` from #638.
- [x] `short-lines` summary prints `NaN` for a NaN peak, not `n/a`, so the ZERO SAMPLES / ALL-ZERO / NaN split shows. Verify: its parser test in `tools/phone/test-run-checks.ps1` with a NaN row.

#### Redesign (2026-11-01)


Facts: on the S21 the silent short-line takes have full length and NaN samples, random per take (2026-10-31 run).
Kokoro runs with NumThreads = clamp(cores/2, 2, 4), provider cpu (KokoroEngineConfig.Configure,
AndroidSupertonicSpeechReader.cs ~1248-1255, IosTtsEngine.cs ~182-189); sherpa-onnx 1.13.3, onnxruntime 1.24.3.
Random NaN output from a multi-threaded CPU run points first at a threaded arm64 kernel; not proven.

## Design

1. Drop the pause-mark padding from the branch (ShortLinePadding, padded retries, trim, density threshold,
   ShortLinePaddingTests). Keep the marker-file probe and the `short-lines` phone check.
2. One pure helper in Application/Reading (e.g. `TakeStats.From(float[] samples, int sampleRate)`): sample count,
   NaN count, peak over finite samples, voiced ms. Unit-tested. SilentOutputGuard uses it: a take with any NaN
   sample is a failed take (the existing Slice 210 plain retry then runs), and its Debug log line carries the
   NaN count (never the text).
3. Diagnosis probe (Debug and marker file only, as today): for each short Kokoro line, 3 plain takes on the normal
   engine and 3 plain takes on a single-thread Kokoro engine (same config, NumThreads = 1), created lazily only in
   probe mode and disposed with the reader. One `short line probe` log line per arm with samples, NaN count, peak,
   voiced ms. Android and iOS both (iOS compile only).
4. Rescue, behind a named constant `KokoroNanRescue` (enum: Off, SingleThreadRetry; default SingleThreadRetry):
   when a Kokoro take has NaN samples, its next retry runs on a lazily created single-thread engine (created on the
   first NaN take, kept while the Kokoro pack is the active engine, disposed with the main engine; never created
   when no NaN take happens). Both platforms through the shared guard; the engine factory lives in each platform
   reader. The coordinator sets the default from the phone run: if single-thread takes show NaN as often as
   multi-thread ones, the default becomes Off and the slice records the next candidate (sherpa-onnx / onnxruntime
   version change) instead.
5. `short-lines.ps1`: print NaN (not n/a) for a NaN peak, a NaN count column, and a summary split by arm
   (multi-thread vs single-thread) and outcome (rescued by single-thread retry, gave up). Parser test in
   tools/phone/test-run-checks.ps1 with a NaN row.

## What the phone run decides (coordinator runs it after both reviews)

- Confirmed: multi-thread probe NaN takes > 0 and single-thread NaN takes 0 (or near) on the same lines; `gave up 0/N`.
- Rejected: single-thread NaN about as often. Then KokoroNanRescue default Off, record it, next candidate is the
  runtime version.
- Not reproduced: no NaN over >= 24 multi-thread probes. Record, keep NaN-as-failed (harmless), no rescue claim.

#### Redesign built (wave 2026-11-01, not yet run on a device)

- Dropped: `ShortLinePadding` (pad, trim, density, padded retries, sibling padding), `ShortLinePaddingTests`. Kept: the marker-file probe (now `ShortLineProbe`) and the `short-lines` check.
- `TakeStats.From` (samples, NaN count, finite peak, voiced ms) and `KokoroNanRescue` (Off, SingleThreadRetry) live in `SilentOutputGuard.cs`; the default is `KokoroEngineConfig.NanRescue = SingleThreadRetry`. A take with any NaN is a failed take; the retry after it runs on a lazily built single-thread Kokoro engine (`RefCountedTts.SingleThread`, Android and iOS), disposed with its engine. Giving up prefers a take without NaN.
- Debug log lines (no text): `take has NaN samples (... nan N of M)`, `retry take N (plain|single-thread, ...)`, `rescued by single-thread take N`, and per short Kokoro line two `short line probe multi-thread|single-thread` lines of 3 takes each. Windows (no single-thread engine) probes the multi-thread arm only.
- `short-lines.ps1`: NaN count column, `NaN` for a NaN peak, summary per arm and per outcome; parser tests in `tools/phone/test-run-checks.ps1` (pwsh and powershell).
- Still open: the phone run that decides the default (see *What the phone run decides*); the cause item above stays open until it. iOS: compile only.

#### Part 1 merged (PR #638, 2026-11-01)

Built and merged: `TakeStats` (non-finite count, peak, voiced ms); a non-finite take counts as failed (plain retry); give-up takes are zeroed so NaN never reaches the player; the probe logs per arm; the `short-lines` summary prints NaN and splits per arm; the single-thread twin sits behind `KokoroNanRescue`, whose default is now Off (`KokoroEngineConfig.cs`).

S21 run 2026-11-01 (debug 127010c518a7):

- bm_daniel: multi-thread NaN 9/9, single-thread NaN 7/9; rescued 0/4, gave up 2/4.
- bf_alice: multi-thread NaN 9/12, single-thread NaN 11/12; rescued 0/4, gave up 3/4.
- NaN takes are full length and all-NaN.
- Verdict: **Rejected** for the thread-count cause (second design rejected; padding was the first). The single-thread rescue stays Off.

Lead (web research, not tested): k2-fsa/sherpa-onnx issue #4086 and PR #4087 (merged 2026-10-26). The int8 Kokoro model returns all-NaN for short texts, from unguarded STFT convs (0/0 at DC/Nyquist); fp32 is unaffected. The fix re-exports the int8 model with `/decoder/generator/Conv` and `Conv_1` kept float32, and the models were updated in the sherpa-onnx `tts-models` release. Android report #3754 (Pixel, int8) was closed as fixed by #4087.

#### Next design (wave to come)

- [x] Check whether the kokoro-en pack ships the int8 model (`packs.yml`).
- [x] Rebuild the pack from the re-exported int8 model in `tts-models` (or fp32 if the size allows), publish a new packs release (standing approval), and bump the pack version so phones update.
- [x] Verify: the `short-lines` S21 run on bm_daniel and bf_alice shows NaN 0 and gave up 0. No app code change is expected.

#### Part 2 design (written by the wave coordinator, 2026-11-01)

Checked 2026-11-01: `packs.yml` pins `KOKORO_URL` = `kokoro-int8-multi-lang-v1_0.tar.bz2` with `KOKORO_SHA256` 4c3052ab...; the upstream archive today (re-uploaded 2026-10-26 by sherpa-onnx PR #4087, STFT convs kept float32) is 132,353,410 bytes, sha256 `72c347230107ef8a31098ec4d468a605244beef5f1ed00db97efa13518800653`. So the live `kokoro-en-2` pack (packs-10, carried over) is the pre-fix int8 model. fp32 (349,886,476 bytes) is over the 150 MB cap and is not used.

- Pin the new sha256 in `packs.yml` (same URL, cap unchanged, comments updated to say why).
- Dispatch `packs.yml` from this branch with `kokoro_pack_version` 3, `kokoro_carry_over_from` empty, and every other pack kept at its current version/bytes (read `docs/voice-packs.md` and the inputs for how), publishing `packs-11` (standing approval; nothing live changes until the app points at it).
- Bump `PackConstants.CatalogUrl` to `packs-11`, so phones see kokoro-en 3 and download it. No other app code change.
- Verify on the S21: the `short-lines` check (defaults: bm_daniel and bf_alice, the 4 Slice 210 lines x 3 reads = 24 reads) shows NaN 0 and gave up 0 on every take, and a probe line on every line's first read. The check clears the sentence cache before every read (see "Check redesign" below), so repeats count and the default is 3. NaN is random per take, so a small sample proves nothing. The check reads the active kokoro-en folder from the app's `active` file, logs it and fails unless it is `v3` or newer (`-ExpectedPackVersion`): the update to 3 is offered, not forced, so a run on a phone still on v2 does not count. If NaN persists, the swap is rejected: do not merge the CatalogUrl bump; park.
- Merge order: this PR merges only after `packs-11` is published and the S21 check above passed. The `CatalogUrl` bump points at `packs-11`; merged earlier, release.yml would ship an app whose catalogue URL 404s and every pack download would fail.
- Same-version rebuilds: only Kokoro can be carried over, so the other 34 packs are rebuilt at the version numbers of packs-10 and their zips will differ in bytes and sha256. This is harmless to installed phones: the apps offer a pack only when the catalogue version is newer (`PackVersions.IsNewer`: `PackVersions.cs:19`, `VoicePackInstaller.cs:622`, `MobileVoicePackService.cs:468`, `WindowsVoicePackService.cs:138`), and the sha256 is checked only against the catalogue entry of the zip being downloaded (`MobileVoicePackService.cs:552,571`), never against an installed pack. Fresh installs get the packs-11 rebuild, which has no device run of its own.
- Docs: the int8 notes in `.github/CLAUDE.md` and Slice 114 updated (size, sha, why).

Check redesign (2026-11-01, after two S21 runs with missing probe lines): `short-lines.ps1` now deletes `cache/sentences` (`run-as <pkg> rm -rf`, never `files/packs`) before every read, so every read, every repeat, is synthesised and must carry a probe line; `-Repeats` default is 3 (the 4 lines x 3 x 2 voices = 24 reads). The cache is `FileSystem.CacheDirectory/sentences` (`AndroidSupertonicSpeechReader.cs:45,185-188`), indexed in memory once per process but a vanished file is a miss (`SentenceAudioCache.cs:54-68`). The "AOT build has no probe" factor is not supported by the code: the dev AOT build is a Debug-configuration build (README: `-Aot` is an AOT-compiled Debug build; `read` hook and debug sink are `#if DEBUG`), so the check only refuses a non-debuggable install (`dumpsys package` flags). The pack guard accepts v3 or newer, and an adb/run-as failure reading the version is reported as that failure.

Runner gap: `run-checks.ps1` has no `-Voices`; `short-lines.ps1` had to be run directly for a second voice (belongs with the Slice 224/225 tooling).

#### Part 2 built (PR #685, wave 2026-11-01): the model swap fixed it

- `packs.yml` pins the re-uploaded sherpa-onnx int8 archive (sha256 `72c347230107ef8a31098ec4d468a605244beef5f1ed00db97efa13518800653`, 132,353,410 bytes; STFT convs kept float32, sherpa-onnx PR #4087); `kokoro_pack_version` default is 3. `PackConstants.CatalogUrl` points at `packs-11`.
- packs-11 published from this branch (run 41323377117): 35 packs, only kokoro-en changed (v3, 131,415,128 bytes). The other packs were rebuilt at the same versions (phones decide re-download by version only, `PackVersions.IsNewer`); fresh installs get the rebuilt bytes, which have no device run of their own.
- `tools/phone/checks/short-lines.ps1`: clears `cache/sentences` before each read, refuses non-debuggable installs, guards the active kokoro-en pack version (at least v3), 3 repeats x 4 lines, a probe line required on every read.
- S21 run 3 (2026-11-01 about 16:35), kokoro-en v3: bm_daniel 12 reads, bf_alice 12 reads; multi-thread NaN 0/72 takes, single-thread NaN 0/72, gave up 0/24, PASS. Log: `reviews/s220c-short-lines.txt`. Baseline on v2: bm_daniel NaN 9/9, bf_alice 9/12. Cause confirmed: the int8 export's STFT convs.
- Changed while building: run 1 used an older dev APK (packs-10), reinstalled; repeats served from the sentence cache gave no probe line (runs 1 and 2), fixed by clearing the cache per read. Odd, not a failure: the single-thread arm gives identical samples across repeats.
- The single-thread rescue stays Off; no further app code change.

From review (findings files in `reviews/`, outside the repo):

- `s220-p2-high.md` (8 findings, read 8/8): 1 (nothing pushed before dispatch; process, pushed before dispatching); 2 (merge-order line added to the slice); 3 (no change needed); 4, 6, 7, 8 applied; 5 (self-check ignores NaN) skipped, low.
- `s220-p2-medium.md` (7 findings, read 7/7): 1 and 7 are the merge-order gate, done (publish and verify first); 2, 3, 4, 5, 6 low, skipped.
- `s220-p2-medium-check.md` (6 findings, read 6/6): 3 and 4 applied; 1 and 2 covered by the check redesign (3 repeats, cache cleared per read); 5 skipped (low); 6 is the merge-order gate.
- `s220-p2-medium-check2.md` (8 findings, read 8/8), all low, none applied (dev-tool script `short-lines.ps1`; the S21 PASS run hit no failure path): 1 local `Clear-SentenceCache` shadows the common.ps1 helper; 2 adb error read as "not debuggable"; 3 debuggable assert runs after the pack-version read; 4 `Set-ProbeMarker` output not verified, cleanup throw can mask the first error; 5 probe log read with no settle wait, a half-logged probe passes; 6 `-Repeats` and `-ExpectedPackVersion` not validated; 7 default 24 reads (48 with `-AllVoices`) can pass the 10-minute cap; 8 dead pack-version term in the final `$ok`.

#### For CLAUDE.md

- Kokoro pack: `.github/workflows/packs.yml` pins the kokoro int8 archive by sha256; kokoro-en 3 and later must be built from the float32-STFT re-upload (the older int8 export returns all-NaN audio for short lines on arm64). Pack version defaults must change with the model. (Slice 220: docs/plan/slices/220-kokoro-silent-short-lines-arm64.md)
- Same-version pack rebuilds are safe for installed phones (only `PackVersions.IsNewer` triggers a download), but only Kokoro can be carried over; the rest are rebuilt. Merge a `PackConstants.CatalogUrl` bump only after that packs release exists. (Slice 220: docs/plan/slices/220-kokoro-silent-short-lines-arm64.md)

#### Not yet verified

- iOS: compile only.
- The Windows Dino head on kokoro-en 3.
- The 68-read Slice 210 set.
- A fresh install's rebuilt non-kokoro packs (rebuilt in packs-11 at the same versions, no device run).

#### Review notes (check pass on the final code, 2026-11-01)

Findings file `reviews/s220-w2-medium-check.md`. None applied. 1: the slice stays open (planned) for the model swap. 2 and 3: the single-thread code stays as a switch and the probe's second arm; with `NanRescue` Off only the Debug probe (marker file) builds the twin. 4: one no-op task per read end, negligible. 5, 6, 7: low; a NaN take failing on any engine is the design. 8: the marker is only created by the phone check in the dev app. 9: tool output only, belongs with the Slice 224/225 tooling.
