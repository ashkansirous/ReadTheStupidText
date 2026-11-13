# Slice 342: Listening page: Farsi voice pack (Slice 51)

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

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
