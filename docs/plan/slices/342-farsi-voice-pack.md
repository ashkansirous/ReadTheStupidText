# Slice 342: Listening page: Farsi voice pack (Slice 51)
**Product:** Shared

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 51's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Android:** Slice 51: run `packs.yml` with `release_number=2` (all three versions `1`), then a one-line PR flipping `PackConstants.CatalogUrl` to `packs-2`; until then Farsi and Chinese read with the phone voice. Then hear fa and zh on a device, judge Arabic/Japanese quality and French-as-French by ear, and re-check the `fa_IR-amir-medium` licence before a wide launch. ([slice](docs/plan/slices/51-multilingual-voices.md))

**Touches:** `docs/plan/slices/342-farsi-voice-pack.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [ ] Make the audio exactly as the app plays it: render it with the app's own engine and packs (the `voice-pick` skill's render path), or record it from the dev app on the S21 when rendering cannot reproduce it (speed changes, timing, a Sonos or store track). Note how each clip was made and its build or pack version.
- [ ] Publish one private listening page through the repo's `voice-pick` agent: a play button per clip, the line of text it reads, and for each clip "sounds right" or "something is wrong" with a free-text box for what is wrong (and a timestamp when it helps). English and Farsi only.
- [x] When the user has answered, write every answer into this file, word for word (2026-11-13, see Decisions below).
- [x] Each "something is wrong" becomes a fix item in this slice (written 2026-11-13, see Fix items below). Every "sounds right" closes the check (the "ok" clips below).

## Decisions (user, 2026-11-13)

Listening page https://claude.ai/artifact/Cfes6Q7N1HvgG3HYnmHMjY, user pressed Done 2026-11-13 15:18Z. Answers word for word:

| Clip | Voice | Text | Verdict | Note |
| --- | --- | --- | --- | --- |
| en-supF1-1 | Supertonic F1 | Hello, how are you today? The weather is lovely. | ok |  |
| en-supF1-2 | Supertonic F1 | I drink a cup of tea every morning and read a book. | ok |  |
| en-supF1-3 | Supertonic F1 | Sara went to school at half past seven and bought twenty-five notebooks. | ok |  |
| en-kokoro-1 | Kokoro Heart | Hello, how are you today? The weather is lovely. | ok |  |
| en-kokoro-2 | Kokoro Heart | I drink a cup of tea every morning and read a book. | ok |  |
| en-kokoro-3 | Kokoro Heart | Sara went to school at half past seven and bought twenty-five notebooks. | ok |  |
| fa-ganji_adabi-1 | Ganji Adabi | سلام، حال شما چطور است؟ امروز هوا خیلی خوب است. | bad | it says: "salam, hal shoma chetor ast" instead of saying "salam, hale shoma chetor ast" |
| fa-ganji_adabi-2 | Ganji Adabi | من هر روز صبح یک فنجان چای می‌نوشم و کتاب می‌خوانم. | ok |  |
| fa-ganji_adabi-3 | Ganji Adabi | علی ساعت هفت و نیم به مدرسه رفت و بیست و پنج دفتر خرید. | ok |  |

## Fix items (from the listening answers)

- [x] (built 2026-11-13, second listen owed: `docs/plan/unverified.md`) Fix: `fa-ganji_adabi-1` (Ganji Adabi, «حال شما»): the ezafe is missing, it reads "hal shoma", should be "hale shoma". Pattern: the phonemizer adds or drops the ezafe (the linking -e between a noun and what follows) where the written Farsi has no mark for it, so it guesses wrong. Cause to find first (which phonemizer step or pack lexicon decides the ezafe), then fix through the normal review gate, then re-render the clip on the same page for a second listen.

## Ezafe fix design (coordinator, wave 2026-11-13; covers Slice 343's fix items too)

*Built in wave 2026-11-13, PR #1098 (merge pending), one PR with Slice 343. Not yet verified on a device.*

**Touches (fix):** `src/ReadTheStupidText.Application/Reading/FarsiEzafe*.cs` (new), the one shared speech-text step every reader passes chunk text through (`src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs` or the file the builder finds that feeds `SherpaEngineFactory.Generate`, Android and iOS readers alike), `tests/Shared.Tests/**`, `tools/sound-words-test/**` (probe only), `docs/plan/slices/342-farsi-voice-pack.md`, `docs/plan/slices/343-four-farsi-voices.md`, `docs/plan/unverified.md`.

Cause (premise check, wave 2026-11-13): all four voices are Piper VITS voices in the `piper-fa` pack; the ezafe is decided by espeak-ng inside native sherpa-onnx (`SherpaEngineFactory.cs:112-119`, `DataDir`). Our code has no Farsi speech pre-processing, so the guess cannot be steered today. A general ezafe rule is not realistic; the fix is a small, tested override table applied to the speech text only.

1. **Probe first (at most 10 minutes, before any app code).** With `tools/sound-words-test` (the voice-pick render path) and the newest `piper-fa` pack, read the espeak phonemes (and render WAVs) for the three phrases plain and with candidate marks: kasra U+0650 on the last letter of the first word to force the ezafe («حالِ شما»); sukun U+0652 on the last letter, or a ZWNJ / no-break boundary, to stop it («صبحْ یک», «دفترْ خرید»). The phoneme string decides (the `e` present or absent), not the ear. Write the result into this file.
2. **One pure function** `FarsiEzafe.Apply(string text)` with a word-pair table: force pairs get the kasra, suppress pairs get the mark the probe proved. Only whole-word matches; text with no table pair comes back unchanged (same instance). Unit tests in `Shared.Tests` for every pair, word boundaries, and non-Farsi text untouched.
3. **One call site**: applied to the text sent to the engine, only when the reading language is Farsi, on Windows, Android and iOS through the same shared step (no per-platform copy). The text shown on screen and the highlight offsets do not change.
4. If the probe finds no mark that stops an extra ezafe, ship only the forced pair and keep the two suppress items open with the probe result.
5. Re-render every bad clip and its neighbours as WAVs into `<worktrees>/reviews/s342-ezafe/`; the second-listen page needs the Artifact tool, which a headless night session lacks, so it goes to `docs/plan/unverified.md` as the second listen for the user.

**Probe result (2026-11-13, step 1).** espeak-ng (pack's own `espeak-ng-data`, `fa` voice; libespeak 1.52 via `espeakng-loader`, not sherpa's embedded build, which prints no phonemes) gives: plain `سلام، حال شما` -> `hˈɑl ʃˈomɑ` (no ezafe, as the user heard); kasra `حالِ شما` -> `hˈɑle ʃˈomɑ` (the `e` appears). So the kasra forces the ezafe. For `صبح یک` and `دفتر خرید` the plain text already gives `sˈobh jˈek` and `dˈaftar xˈarid` (no `e`), and the sukun and ZWNJ variants give the identical string, so the extra ezafe the user heard for Ganji and Gyro is not in espeak's phonemes: it is likely the VITS model adding it from context, and no mark that changes the phonemes can stop it. Audio lengths of the sukun/ZWNJ variants differ only inside take noise. Step 4 applies: only the forced pair ships (`FarsiEzafe`, table: `حال` before `شما`); the two suppress items stay open. WAVs of the probe: `reviews/s342-ezafe/probe/`.

**Built (2026-11-13).** `Application/Reading/FarsiEzafe.cs` (`Apply`, `ForModel`: only Vits models with language `fa`), called at the three engine `Generate` points (`SherpaEngineFactory.Generate`, `AndroidSupertonicSpeechReader.GenerateAudio`, `IosTtsEngine.Generate`) because they are the one step common to all readers after chunking and sentence-splitting: the text shown and the highlight offsets stay the original. The persistent sentence cache key is built from the text actually sent to the engine (`FarsiEzafe.ForModel(engine.Model, sentence)` in each `KeyFor`: `NeuralSentenceSource`, `AndroidSupertonicSpeechReader`, `IosSupertonicSpeechReader.Synthesis`), so a sentence the table changes gets a new key (the old "hal shoma" clip is not served) and every unchanged sentence keeps its old key. Any whitespace run between the two words counts (space, no-break space, tab, newline), the separator kept as written. Tests: `tests/Shared.Tests/FarsiEzafeTests.cs`. Re-rendered clips for all four voices and three lines: `reviews/s342-ezafe/*-after.wav`. The Android and iOS edits were not compiled by the builder; the review pass settled this by an Android and an iOS compile (finding 9 below).

**Known limits (review 2026-11-13, `reviews/s342-high.md` #6, #8).** (a) The probe used standalone libespeak 1.52 (`espeakng-loader`), not the espeak embedded in sherpa-onnx, so the phoneme result may differ in the shipped engine; the conclusion "no mark can remove an extra ezafe" and the "after" WAVs are unlistened; the two suppress items stay open. (b) The override works per chunk/sentence: a «حال شما» pair split across two chunks is missed. (c) «حال» meaning "now" before «شما» is forced into «hale» (a pair table cannot tell the senses apart). All accepted.

*Changed while building:* the plan said one call site through a shared step; it is three engine `Generate` points (one per platform engine), and the sentence cache key uses the processed text so the old clip is not served. Only the forced pair ships; the three extra-ezafe items of Slice 343 stay open (espeak gives no `e` for them, the VITS model adds it, no text mark changes it).

*From review:* high pass `reviews/s342-high.md` (10 findings): applied #1 (cache key), #2 (named char constants), #5 (any whitespace between the words), #6 (probe-build note in Known limits), #8 (known limits noted), #10 (more tests); #9 settled by the Android and iOS compile.
- Skipped #3 (`reviews/s342-high.md`): "compute once before the ternary" - style only, no behaviour change.
- Skipped #4 (`reviews/s342-high.md`): "Either accept and amend the design note, or move to a shared step" - accepted: the three engine `Generate` points are the one step common to all readers, and the design note is amended in Built above.
- Skipped #7 (`reviews/s342-high.md`): "add a cheap ordinal Contains pre-check or span compares" - no measured cost.
Medium pass `reviews/s342-medium.md` (4 findings), all skipped:
- Skipped #1 (`reviews/s342-medium.md`): "A pair table cannot tell the senses apart" - already a known limit (Known limits (c)).
- Skipped #2 (`reviews/s342-medium.md`): "Apply runs twice per Farsi sentence" - no measured cost.
- Skipped #3 (`reviews/s342-medium.md`): "compute once before the ternary" - style only.
- Skipped #4 (`reviews/s342-medium.md`): "Add a table-version tag to the key if the table grows" - the cache key is built from the processed text, so a table edit that changes a sentence changes its key.

*Device run (wave 2026-11-13):* RTST build `c5a99c234c14` ok. No RTST Farsi read flow exists. The Dino flows `farsi-speaker-first-join` and `premade-audio-past-roar` FAIL on causes unrelated to this change (stuck on onboarding after skip-first-run lang=fa; after `pm clear` a real fa audio pack replaced `fixture-13.m4a`), and both play premade clips, so the ezafe path was not reached on a device. Log: `D:/ReadTheStupidText-worktrees/.emulator/logcat-i1/dino-farsi-speaker-first-join.log`.

#### For CLAUDE.md

- Farsi speech text goes through `FarsiEzafe.ForModel` (`src/ReadTheStupidText.Application/Reading/FarsiEzafe.cs`) at every engine `Generate` and in every sentence cache `KeyFor`; a new Piper/Farsi path must do both, or the override is skipped or a stale clip is served. The text on screen and highlight offsets stay original. (Slice 342: docs/plan/slices/342-farsi-voice-pack.md)

#### Not yet verified

- The ezafe on a device (Android, iOS, Windows): needs a flow that installs `piper-fa` and synthesises a Farsi page with no audio pack.
- The second listen for the user: needs the Artifact tool; WAVs in `D:/ReadTheStupidText-worktrees/reviews/s342-ezafe/`.
- Follow-up bug: the Dino flows `farsi-speaker-first-join` and `premade-audio-past-roar` fail on main (causes above).

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
