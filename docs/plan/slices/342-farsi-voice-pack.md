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

- [ ] Fix: `fa-ganji_adabi-1` (Ganji Adabi, «حال شما»): the ezafe is missing, it reads "hal shoma", should be "hale shoma". Pattern: the phonemizer adds or drops the ezafe (the linking -e between a noun and what follows) where the written Farsi has no mark for it, so it guesses wrong. Cause to find first (which phonemizer step or pack lexicon decides the ezafe), then fix through the normal review gate, then re-render the clip on the same page for a second listen.

## Ezafe fix design (coordinator, wave 2026-11-13; covers Slice 343's fix items too)

*Fix items in progress (wave 2026-11-13), draft PR, one PR with Slice 343.*

**Touches (fix):** `src/ReadTheStupidText.Application/Reading/FarsiEzafe*.cs` (new), the one shared speech-text step every reader passes chunk text through (`src/ReadTheStupidText.Application/Reading/SpeechTextChunker.cs` or the file the builder finds that feeds `SherpaEngineFactory.Generate`, Android and iOS readers alike), `tests/Shared.Tests/**`, `tools/sound-words-test/**` (probe only), `docs/plan/slices/342-farsi-voice-pack.md`, `docs/plan/slices/343-four-farsi-voices.md`, `docs/plan/unverified.md`.

Cause (premise check, wave 2026-11-13): all four voices are Piper VITS voices in the `piper-fa` pack; the ezafe is decided by espeak-ng inside native sherpa-onnx (`SherpaEngineFactory.cs:112-119`, `DataDir`). Our code has no Farsi speech pre-processing, so the guess cannot be steered today. A general ezafe rule is not realistic; the fix is a small, tested override table applied to the speech text only.

1. **Probe first (at most 10 minutes, before any app code).** With `tools/sound-words-test` (the voice-pick render path) and the newest `piper-fa` pack, read the espeak phonemes (and render WAVs) for the three phrases plain and with candidate marks: kasra U+0650 on the last letter of the first word to force the ezafe («حالِ شما»); sukun U+0652 on the last letter, or a ZWNJ / no-break boundary, to stop it («صبحْ یک», «دفترْ خرید»). The phoneme string decides (the `e` present or absent), not the ear. Write the result into this file.
2. **One pure function** `FarsiEzafe.Apply(string text)` with a word-pair table: force pairs get the kasra, suppress pairs get the mark the probe proved. Only whole-word matches; text with no table pair comes back unchanged (same instance). Unit tests in `Shared.Tests` for every pair, word boundaries, and non-Farsi text untouched.
3. **One call site**: applied to the text sent to the engine, only when the reading language is Farsi, on Windows, Android and iOS through the same shared step (no per-platform copy). The text shown on screen and the highlight offsets do not change.
4. If the probe finds no mark that stops an extra ezafe, ship only the forced pair and keep the two suppress items open with the probe result.
5. Re-render every bad clip and its neighbours as WAVs into `<worktrees>/reviews/s342-ezafe/`; the second-listen page needs the Artifact tool, which a headless night session lacks, so it goes to `docs/plan/unverified.md` as the second listen for the user.

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
