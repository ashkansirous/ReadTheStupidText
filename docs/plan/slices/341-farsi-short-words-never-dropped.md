# Slice 341: Listening page: Farsi short words never dropped (Slice 149)

*Guard built; merged as PR #993 (c91bf35). CI built Android for it (the `android` run on e33d4767 passed). **No listen needed (the user, 2026-11-09):** "why do I need to listen to 341? those sounds are for dino reader where I have gemeni generated sounds not the internal enginer". The fix is Read The Stupid Text's Farsi engine path only; Dino plays premade Gemini clips and is not affected. The sound words were inherited from Slice 149's Dino-era check; the RTST case is any short Farsi line («سلام.» and «آه.» also came out as hiss), and the peak measurements prove the guard catches it. The page https://claude.ai/artifact/VqSDxX7G8k3TYzGdkAg63e is withdrawn.*

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 149's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **iOS:** Slice 149: emulator Farsi read passed by logs only, not heard; iOS compile of `IosSupertonicSpeechReader.Synthesis.cs` and `SiblingRescue`; a ... [full text](notes/2026-10-30-outstanding-detail.md#o039) ([slice](slices/149-never-drop-a-short-word.md))

**Touches:** `docs/plan/slices/341-farsi-short-words-never-dropped.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it. This is Read The Stupid Text's Farsi path (Dino has had no speech engine since Slice 278). A first render on 2026-11-08 used a Python port of the chunking; the user said none of the clips "say anything proper". It was redone with the app's real C# reading code on Windows (retry and ganji sibling rescue included), piper-fa pack v2 from catalog packs-11, main 9c82f37f, through a throwaway harness that was never committed.
- [x] Publish one private listening page: https://claude.ai/artifact/SMcmfN7etCqhYC7KN9TDii (answers under doc ids `r2-<clip>` and `r2-done`; the older ids belong to the Python-port clips and do not count).
- [x] **Fix 1 (guard built and merged as PR #993; the clips never went on a page, the listen was dropped 2026-11-09; the device run on short Farsi lines is the Device line in `docs/plan/unverified.md`; measured on 2026-11-08 before any answer, from the real C# render):** lone short Farsi sound words come out silent or nearly silent in mana, and the guard does not always rescue them. «میو میو!» renders silent (peak 3% of full scale, 0 s audible) and no rescue fires, so the app would play silence. «تق!» is 0.1 s audible even after the ganji rescue. «ووش!» needs a retake. «هاپ!» is rescued by ganji. ganji alone renders «ووش!» silent, and its retake fails too. Lines of two or more words are audible (27-32% peak). Find why `SilentOutputGuard` lets «میو میو!» through and why the rescues are this weak, fix it in the shared reader for Android, iOS and Windows, and put the new clips on the same page. The Python-port render failed on the same lines, so the port was not the cause.
- Fix 1 built (guard), reworked after review and a measurement of every shipped voice (tables at the end of this file): mana returns a hiss that passes the old span rule (12 of 12 takes of «میو میو!» peaked 0.018-0.059 and ran 192-528 ms; 9 of 12 «سلام.», 10 of 12 «آه.»), so `SilentOutputGuard` now also calls a take quiet when its peak is under `MinPeak` 0.09, or when it holds under `MinSoundMs` 1 ms of samples at or over `SoundAmplitude` 0.05 (a click or one pop has almost none; speech has 5 ms and up). **No clean gap exists in peak alone**: mana's hiss tops out at 0.059 and its sure speech starts at 0.101, with a continuum of unclear takes between (0.045-0.09), so the rule errs towards one more retake. **It is switched on only for drop-prone voices (mana, the `splitPhrases` flag every reader already passes), not globally**: Piper nb voice `mnn` (sid 4) speaks at peak 0.027-0.040 and a global floor would retake every one of its lines for no gain. The other voices measured far above 0.09 (ganji, ganji-adabi, gyro 0.115+, Supertonic 0.167+, Kokoro 0.196+), so they lose nothing by not being checked either. The quiet outcome has its own fallback reasons `quiet_retake`, `quiet_sibling`, `quiet_gave_up` (also in `worker/events.ts`), used only when the quiet check alone caught the first take; the rescue log lines carry each take's peak and "caught by the peak check", and a near-miss debug line is written when a first take passes under 1.25 times `MinPeak`. Choosing the best take at give-up now ranks "has real sound" (`TakeStats.IsQuiet`, one rule shared with the check) above voiced span, so a long hiss or a hiss with one pop never beats short real speech. Not heard: whether the retakes and ganji sound right; the final WAVs are in `tmp/341-clips/` (worktree) for the listening page. The local Android build hung at `_ResolveXamarinAndroidTools` (log `tmp/android-build.log`) and was not run; CI builds Android.
- Re-render through the guard (the app's `NeuralSentenceSource`, mana with the ganji sibling, 10 runs per word, 2026-11-09; final peak is measured on the joined WAV). «میو میو!»: 10 of 10 rescued by the sibling (8 caught by the peak check alone), final peak 0.104-0.172, 480-716 ms voiced, none gave up. «تق!»: 10 of 10 rescued by the sibling (4 by the peak check alone), final peak 0.191-0.389, 247-377 ms voiced. «ووش!»: 7 sibling (4 by the peak check alone, final peak 0.114-0.160), 1 retake (peak 0.104), 2 gave up (final peaks 0.081 and 0.088, 374-438 ms: ganji also renders this word weakly, as the first measurement said; the best take plays). «هاپ!» (control): 10 of 10 sibling, final peak 0.170-0.318. No run passed on the first mana take. Cost: a short mana word now almost always loads the sibling, the price of mana dropping them.
- [x] ~~When the user has answered, write every answer into this file~~ (dropped 2026-11-09: no listen needed, see the status line).
- [x] ~~Each "something is wrong" becomes a fix item~~ (dropped 2026-11-09: Fix 1 above is the measured fix).

## From review

- Skipped #1 (`reviews/s341-medium.md`): "The 0.09 peak floor overlaps the measured speech range" - accepted: a few real quiet mana takes (peak ~0.095 and below) will retake and then go to the ganji sibling; mana takes in 0.045-0.09 are a continuum with no clean gap and ganji is a full voice. Listed for a device-data review through the new quiet_* events.
- Skipped #4 (`reviews/s341-medium.md`): "MinSoundMs 1 ms at 0.05 (just under the top hiss peak 0.059) is a weak click filter" - low; the peak floor is the main check and no measured hiss take passed it.
- Skipped #5 (`reviews/s341-medium.md`): "Near-miss debug log sees only the first take's peak and only when the first take passed" - low; debug-only logging, and the rescue lines already carry every take's peak.
- Skipped #6 (`reviews/s341-medium.md`): "A given-up quiet take is played as a hiss (loudest of the takes)" - low; the existing give-up behaviour for any silent take, unchanged by this slice.
- Skipped #7 (`reviews/s341-medium.md`): "quietCheck is derived from splitPhrases implicitly in RescueUnitAsync" - low; only mana is drop-prone today, and the coupling is documented on RescueUnitAsync.

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.

## Measurements (2026-11-09, the app's own engines and packs-11: piper-fa 2, piper-nb 2, kokoro-en 3, supertonic-3 4)

Raw engine output at pace 1.0 (what the guard sees), `SherpaEngineFactory.Generate`, same `TakeStats` as the guard. "peak" is the loudest finite sample (1.0 = full scale), "voiced ms" the span over 0.01. Raw data: `tmp/341-peaks-fa.tsv`, `tmp/341-peaks-rest.tsv`, `tmp/341-mana.tsv` in the worktree (not committed).

### Piper fa voices (4 takes per unit; min / median peak, min / median voiced ms)

| voice | unit | peak min | peak med | voiced ms min | voiced ms med |
|---|---|---|---|---|---|
| fa mana | میو میو! | 0.016 | 0.028 | 93 | 145 |
| fa mana | تق! | 0.004 | 0.020 | 0 | 70 |
| fa mana | ووش! | 0.005 | 0.047 | 0 | 224 |
| fa mana | هاپ! | 0.000 | 0.004 | 0 | 0 |
| fa mana | دنگ! | 0.138 | 0.148 | 646 | 2013 |
| fa mana | بع بع! | 0.133 | 0.162 | 581 | 871 |
| fa mana | هاپ هاپ! | 0.239 | 0.285 | 492 | 818 |
| fa mana | سلام. | 0.001 | 0.019 | 0 | 44 |
| fa mana | بله. | 0.011 | 0.027 | 12 | 180 |
| fa mana | نه! | 0.114 | 0.124 | 1709 | 4072 |
| fa mana | آه. | 0.036 | 0.080 | 155 | 238 |
| fa mana | بچه خوک فرار کرد. | 0.171 | 0.193 | 1150 | 1309 |
| fa mana | گرگ بزرگ و بد آمد. | 0.153 | 0.197 | 1471 | 1534 |
| fa ganji | میو میو! | 0.115 | 0.122 | 544 | 633 |
| fa ganji | تق! | 0.260 | 0.273 | 252 | 336 |
| fa ganji | ووش! | 0.129 | 0.131 | 414 | 483 |
| fa ganji | هاپ! | 0.216 | 0.303 | 411 | 486 |
| fa ganji | دنگ! | 0.297 | 0.363 | 329 | 379 |
| fa ganji | بع بع! | 0.275 | 0.309 | 414 | 461 |
| fa ganji | هاپ هاپ! | 0.270 | 0.306 | 395 | 628 |
| fa ganji | سلام. | 0.221 | 0.284 | 399 | 477 |
| fa ganji | بله. | 0.142 | 0.173 | 259 | 336 |
| fa ganji | نه! | 0.144 | 0.157 | 272 | 277 |
| fa ganji | آه. | 0.204 | 0.283 | 277 | 313 |
| fa ganji | بچه خوک فرار کرد. | 0.169 | 0.191 | 1207 | 1298 |
| fa ganji | گرگ بزرگ و بد آمد. | 0.182 | 0.231 | 1394 | 1520 |
| fa ganji-adabi | میو میو! | 0.262 | 0.331 | 720 | 809 |
| fa ganji-adabi | تق! | 0.286 | 0.316 | 357 | 370 |
| fa ganji-adabi | ووش! | 0.240 | 0.342 | 479 | 518 |
| fa ganji-adabi | هاپ! | 0.342 | 0.562 | 381 | 440 |
| fa ganji-adabi | دنگ! | 0.478 | 0.505 | 311 | 399 |
| fa ganji-adabi | بع بع! | 0.351 | 0.376 | 410 | 448 |
| fa ganji-adabi | هاپ هاپ! | 0.400 | 0.428 | 822 | 956 |
| fa ganji-adabi | سلام. | 0.303 | 0.402 | 546 | 594 |
| fa ganji-adabi | بله. | 0.384 | 0.470 | 303 | 391 |
| fa ganji-adabi | نه! | 0.428 | 0.576 | 255 | 283 |
| fa ganji-adabi | آه. | 0.357 | 0.372 | 405 | 419 |
| fa ganji-adabi | بچه خوک فرار کرد. | 0.590 | 0.621 | 1299 | 1377 |
| fa ganji-adabi | گرگ بزرگ و بد آمد. | 0.478 | 0.542 | 1587 | 1682 |
| fa gyro | میو میو! | 0.354 | 0.366 | 487 | 594 |
| fa gyro | تق! | 0.424 | 0.470 | 269 | 289 |
| fa gyro | ووش! | 0.198 | 0.226 | 127 | 180 |
| fa gyro | هاپ! | 0.361 | 0.420 | 143 | 212 |
| fa gyro | دنگ! | 0.242 | 0.275 | 194 | 238 |
| fa gyro | بع بع! | 0.498 | 0.550 | 389 | 427 |
| fa gyro | هاپ هاپ! | 0.343 | 0.424 | 266 | 335 |
| fa gyro | سلام. | 0.418 | 0.457 | 360 | 385 |
| fa gyro | بله. | 0.448 | 0.490 | 371 | 383 |
| fa gyro | نه! | 0.216 | 0.272 | 197 | 210 |
| fa gyro | آه. | 0.211 | 0.251 | 106 | 128 |
| fa gyro | بچه خوک فرار کرد. | 0.408 | 0.495 | 991 | 1066 |
| fa gyro | گرگ بزرگ و بد آمد. | 0.399 | 0.429 | 1285 | 1340 |

### Piper fa mana, 12 takes per unit (the drop-prone voice; sorted peaks)

| unit | lowest peaks | highest peaks | takes passing the old span rule with peak under 0.09 |
|---|---|---|---|
| میو میو! | 0.018 0.024 0.024 0.025 | 0.045 0.045 0.059 | 12 of 12 |
| تق! | 0.000 0.001 0.002 0.004 | 0.029 0.031 0.088 | 3 of 12 |
| ووش! | 0.012 0.013 0.014 0.020 | 0.076 0.095 0.111 | 6 of 12 |
| هاپ! | 0.000 0.000 0.001 0.001 | 0.002 0.004 0.016 | 0 of 12 |
| دنگ! | 0.109 0.126 0.139 0.168 | 0.217 0.218 0.221 | 0 of 12 |
| بع بع! | 0.106 0.111 0.113 0.131 | 0.157 0.161 0.208 | 0 of 12 |
| هاپ هاپ! | 0.173 0.187 0.197 0.202 | 0.374 0.398 0.412 | 0 of 12 |
| بوووم! | 0.018 0.023 0.026 0.027 | 0.047 0.048 0.048 | 11 of 12 |
| سلام. | 0.001 0.019 0.024 0.024 | 0.054 0.069 0.101 | 9 of 12 |
| بله. | 0.001 0.001 0.017 0.018 | 0.066 0.081 0.084 | 7 of 12 |
| نه! | 0.101 0.101 0.102 0.111 | 0.144 0.154 0.162 | 0 of 12 |
| آه. | 0.024 0.025 0.035 0.042 | 0.084 0.107 0.118 | 10 of 12 |
| بچه خوک فرار کرد. | 0.140 0.140 0.141 0.146 | 0.199 0.211 0.223 | 0 of 12 |
| گرگ بزرگ و بد آمد. | 0.156 0.158 0.171 0.175 | 0.237 0.283 0.297 | 0 of 12 |
| پیش از آن، | 0.030 0.035 0.035 0.036 | 0.064 0.071 0.074 | 12 of 12 |

### Other packs: lowest peak of any voice per unit (Supertonic en: 6 voices x 2 takes; Kokoro en: 7 voices x 2 takes; Piper nb: 4 voices x 3 takes)

| pack | unit | lowest peak (voice) | voiced ms at that take |
|---|---|---|---|
| supertonic-en | hm. | 0.212 (sid5) | 314 |
| supertonic-en | shh. | 0.180 (sid4) | 371 |
| supertonic-en | Hi! | 0.207 (sid2) | 433 |
| supertonic-en | OK. | 0.195 (sid2) | 565 |
| supertonic-en | No. | 0.207 (sid2) | 417 |
| supertonic-en | Oh. | 0.167 (sid2) | 379 |
| supertonic-en | Boom! | 0.217 (sid2) | 399 |
| supertonic-en | Meow meow! | 0.205 (sid2) | 731 |
| supertonic-en | Woosh! | 0.245 (sid4) | 478 |
| supertonic-en | The little pig ran away. | 0.205 (sid2) | 1255 |
| supertonic-en | Once upon a time there was a dinosaur. | 0.256 (sid0) | 1792 |
| kokoro-en | hm. | 0.316 (sid3) | 627 |
| kokoro-en | shh. | 0.324 (sid3) | 768 |
| kokoro-en | Hi! | 0.196 (sid2) | 382 |
| kokoro-en | OK. | 0.340 (sid15) | 692 |
| kokoro-en | No. | 0.273 (sid3) | 415 |
| kokoro-en | Oh. | 0.266 (sid3) | 386 |
| kokoro-en | Boom! | 0.336 (sid10) | 596 |
| kokoro-en | Meow meow! | 0.258 (sid3) | 703 |
| kokoro-en | Woosh! | 0.341 (sid15) | 544 |
| kokoro-en | The little pig ran away. | 0.280 (sid3) | 1192 |
| kokoro-en | Once upon a time there was a dinosaur. | 0.315 (sid15) | 2159 |
| piper-nb | hm. | 0.032 (sid4) | 241 |
| piper-nb | Hei! | 0.029 (sid4) | 141 |
| piper-nb | Nei. | 0.027 (sid4) | 151 |
| piper-nb | Mjau mjau! | 0.038 (sid4) | 558 |
| piper-nb | Grisen løp bort. | 0.034 (sid4) | 598 |

### Piper nb, every voice measured

| voice | unit | peak min | peak med | voiced ms min |
|---|---|---|---|---|
| piper-nb sid0 | hm. | 0.073 | 0.081 | 318 |
| piper-nb sid0 | Hei! | 0.067 | 0.083 | 170 |
| piper-nb sid0 | Nei. | 0.060 | 0.067 | 244 |
| piper-nb sid0 | Mjau mjau! | 0.080 | 0.086 | 646 |
| piper-nb sid0 | Grisen løp bort. | 0.082 | 0.085 | 683 |
| piper-nb sid2 | hm. | 0.065 | 0.065 | 252 |
| piper-nb sid2 | Hei! | 0.063 | 0.069 | 197 |
| piper-nb sid2 | Nei. | 0.058 | 0.059 | 199 |
| piper-nb sid2 | Mjau mjau! | 0.074 | 0.088 | 587 |
| piper-nb sid2 | Grisen løp bort. | 0.071 | 0.071 | 808 |
| piper-nb sid4 | hm. | 0.032 | 0.033 | 241 |
| piper-nb sid4 | Hei! | 0.029 | 0.030 | 141 |
| piper-nb sid4 | Nei. | 0.027 | 0.028 | 151 |
| piper-nb sid4 | Mjau mjau! | 0.038 | 0.040 | 558 |
| piper-nb sid4 | Grisen løp bort. | 0.034 | 0.038 | 598 |
| piper-nb sid7 | hm. | 0.090 | 0.098 | 232 |
| piper-nb sid7 | Hei! | 0.106 | 0.122 | 152 |
| piper-nb sid7 | Nei. | 0.082 | 0.089 | 185 |
| piper-nb sid7 | Mjau mjau! | 0.102 | 0.107 | 447 |
| piper-nb sid7 | Grisen løp bort. | 0.081 | 0.095 | 816 |
