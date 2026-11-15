# Slice 343: Listening page: Four Farsi voices (Slice 89)
**Product:** Shared

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 89's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Windows:** Slice 89: Windows desktop acceptance of the four voices not run (a stale `desktop-rtl` dev package blocks deploy); iOS on a device; Pixel timing of cold voice preparation (7-8 s on the emulator, 16 s on a cold mid-read swap); a saved Amir choice falling back to Ganji Adabi, on a device; the play button shows idle after Back while chunks still play. ([slice](docs/plan/slices/89-four-farsi-voices.md))

*Parked: second listen needs an Artifact listening page (interactive session); 3 extra-ezafe items.* *Built in wave 2026-11-13, merged as PR #1098 (fdf26dc7), one PR with Slice 342 (same cause; the full record is in Slice 342's file). The three extra-ezafe items stay open. Not yet verified on a device.*

*Park note (night 2026-11-14-0726):* parked again by housekeeping 1 for the same reason (the second listen needs an Artifact page; headless wave sessions have no Artifact tool). Moved back to `planned` by the 2026-11-14-0726 feedback loop per the user's 2026-11-01 rule; a night run can only re-park it, so it needs an interactive session.

*Park note (night 2026-11-13-1149):* parked by wave 5 because the 3 extra-ezafe items have no text fix (the piper-fa VITS model adds the ezafe itself) and the second listen needs an interactive session (Artifact page; WAVs in `reviews/s342-ezafe/`). Moved back to `planned` by the 2026-11-13-1149 feedback loop so the next night retries it.

*Park note (2026-11-14, night 2026-11-14-1547 housekeeping 2):* parked: the second listen is an Artifact listening page, which needs an interactive session (headless waves have no Artifact tool).

**Touches (fix items):** see Slice 342's file, which carries the shared design and code Touches.

**Touches:** `docs/plan/slices/343-four-farsi-voices.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [ ] Make the audio exactly as the app plays it: render it with the app's own engine and packs (the `voice-pick` skill's render path), or record it from the dev app on the S21 when rendering cannot reproduce it (speed changes, timing, a Sonos or store track). Note how each clip was made and its build or pack version.
- [ ] Publish one private listening page through the repo's `voice-pick` agent: a play button per clip, the line of text it reads, and for each clip "sounds right" or "something is wrong" with a free-text box for what is wrong (and a timestamp when it helps). English and Farsi only.
- [x] When the user has answered, write every answer into this file, word for word (2026-11-13, see Decisions below).
- [x] Each "something is wrong" becomes a fix item in this slice (written 2026-11-13, see Fix items below). Every "sounds right" closes the check (the "ok" clips below).

## Decisions (user, 2026-11-13)

Listening page https://claude.ai/artifact/Cfes6Q7N1HvgG3HYnmHMjY, user pressed Done 2026-11-13 15:18Z. Answers word for word:

| Clip | Voice | Text | Verdict | Note |
| --- | --- | --- | --- | --- |
| fa-ganji_adabi-1 | Ganji Adabi (default) | سلام، حال شما چطور است؟ امروز هوا خیلی خوب است. | bad | it says: "salam, hal shoma chetor ast" instead of saying "salam, hale shoma chetor ast" |
| fa-ganji_adabi-2 | Ganji Adabi (default) | من هر روز صبح یک فنجان چای می‌نوشم و کتاب می‌خوانم. | ok |  |
| fa-ganji_adabi-3 | Ganji Adabi (default) | علی ساعت هفت و نیم به مدرسه رفت و بیست و پنج دفتر خرید. | ok |  |
| fa-ganji-1 | Ganji | سلام، حال شما چطور است؟ امروز هوا خیلی خوب است. | ok |  |
| fa-ganji-2 | Ganji | من هر روز صبح یک فنجان چای می‌نوشم و کتاب می‌خوانم. | bad | it says "man har rooz sobhe yek fenjan", but it should say "man har rooz sobh yek fenjan" |
| fa-ganji-3 | Ganji | علی ساعت هفت و نیم به مدرسه رفت و بیست و پنج دفتر خرید. | ok |  |
| fa-mana-1 | Mana | سلام، حال شما چطور است؟ امروز هوا خیلی خوب است. | ok |  |
| fa-mana-2 | Mana | من هر روز صبح یک فنجان چای می‌نوشم و کتاب می‌خوانم. | ok |  |
| fa-mana-3 | Mana | علی ساعت هفت و نیم به مدرسه رفت و بیست و پنج دفتر خرید. | ok |  |
| fa-gyro-1 | Gyro | سلام، حال شما چطور است؟ امروز هوا خیلی خوب است. | ok |  |
| fa-gyro-2 | Gyro | من هر روز صبح یک فنجان چای می‌نوشم و کتاب می‌خوانم. | bad | it says "man har rooz sobhe yek fenjan", but it should say "man har rooz sobh yek fenjan" |
| fa-gyro-3 | Gyro | علی ساعت هفت و نیم به مدرسه رفت و بیست و پنج دفتر خرید. | bad | it says "bisto panj daftare kharid" instead of "bisto panj daftar kharid" |

## Fix items (from the listening answers)

- [x] (built 2026-11-13 in Slice 342's `FarsiEzafe`; second listen owed) Fix: `fa-ganji_adabi-1` (Ganji Adabi default, «حال شما»): the ezafe is missing, it reads "hal shoma", should be "hale shoma" (same fault as Slice 342's clip). Pattern: the phonemizer adds or drops the ezafe (the linking -e between a noun and what follows) where the written Farsi has no mark for it, so it guesses wrong. Find the cause, fix through the review gate, re-render for a second listen.
- [ ] Fix: `fa-ganji-2` (Ganji, «صبح یک»): an extra ezafe, it reads "sobhe yek", should be "sobh yek". Pattern: the phonemizer adds or drops the ezafe (the linking -e between a noun and what follows) where the written Farsi has no mark for it, so it guesses wrong.
- [ ] Fix: `fa-gyro-2` (Gyro, «صبح یک»): the same extra ezafe, "sobhe yek" instead of "sobh yek". Pattern: the phonemizer adds or drops the ezafe (the linking -e between a noun and what follows) where the written Farsi has no mark for it, so it guesses wrong.
- [ ] Fix: `fa-gyro-3` (Gyro, «دفتر خرید»): an extra ezafe, "daftare kharid" instead of "daftar kharid". Pattern: the phonemizer adds or drops the ezafe (the linking -e between a noun and what follows) where the written Farsi has no mark for it, so it guesses wrong.
- [x] Find one cause for the whole ezafe pattern across 342 and 343 (all four voices share the Farsi phonemizer), fix it once, and re-render every bad clip plus its neighbours on one page for a second listen. Result (probe in Slice 342's file): the missing ezafe is espeak's phonemes (a kasra fixes it, shipped); the three extra-ezafe items are not in espeak's phonemes (`sobh jek`, `daftar kharid` already), so no text mark stops them; they stay open, likely the VITS model's own habit, needs a different fix (for example another pack voice or model retrain). WAVs: `reviews/s342-ezafe/`, no Artifact page (headless).

### Second listen (2026-11-14 21:33Z, same page, `v2-*` answers)

The user pressed Done at 21:33:50Z. Answers word for word (Mana was not re-rendered: all three clips were right the first time):

| Clip | Voice | Verdict | Note |
| --- | --- | --- | --- |
| ganji_adabi-1 | Ganji Adabi (default) | ok | (earlier wrong: "hal shoma"; the kasra fix works) |
| ganji_adabi-2 | Ganji Adabi (default) | ok | |
| ganji_adabi-3 | Ganji Adabi (default) | ok | |
| ganji-1 | Ganji | ok | |
| ganji-2 | Ganji | bad | says "har rooz sobhe" , should say "har rooz sob" |
| ganji-3 | Ganji | ok | |
| gyro-1 | Gyro | bad | misses the "ast" at the end |
| gyro-2 | Gyro | bad | says "har rooze sob" , should say "har rooz sob" |
| gyro-3 | Gyro | bad | says "daftare kharid" should say "daftar kharid" |

Result: the missing-ezafe fix (Ganji Adabi, the default voice, and Slice 342's clip) is confirmed by ear. The three extra-ezafe items are still wrong, as expected (no text fix exists; see the cause line above). Gyro now also puts the extra ezafe on «روز» ("rooze") and drops the final «است» in clip 1: both new.

Third listen page (2026-11-14, same page, `v3-*` answers, 16 clips): https://claude.ai/artifact/Cfes6Q7N1HvgG3HYnmHMjY . Gyro `gyro-1` evidence: the app's `SherpaEngineFactory.Generate` returns the engine samples untrimmed (no trim or silence step in `src/`); rendered as the app does, the WAV ends with 0.00-0.04 s of silence and the phonemizer output has the word (`xˈuːb ˈast`). Gyro simply says it very short: the unit with «است» is only about 0.11 s longer than the same unit without it (Ganji +0.37 s, Mana +0.20 s). So it is the model, not the renderer. The page also has comma / ZWNJ / length-scale 1.15 variants for `ganji-2`, `gyro-2`, `gyro-3`.

- [ ] Fix: `gyro-1` (Gyro, «... خیلی خوب است.»): the final «است» ("ast") is not spoken (second listen, 2026-11-14). Find whether the sentence-final word is cut by the renderer (trailing audio trimmed, end-of-sentence handling) or by the model; fix through the review gate; re-render for a third listen.
- [ ] Fix: the extra-ezafe items (`ganji-2`, `gyro-2` now on «روز» too, `gyro-3`): no text mark stops them (espeak's phonemes are already right). Try the cheapest model-side options first and render each on one page for a third listen: a short break between the two words (comma or zero-width non-joiner at render time), a different noise/length scale for these two voices, or the same sentence on the other pack voices. If none works, record it as the model's limit for Ganji and Gyro (the default voice, Ganji Adabi, and Mana are right).

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen. Second listen done 2026-11-14 (above): the default voice is right; Ganji's and Gyro's open items need a third listen after their fixes.

**Park note (moved back to planned by the 2026-11-14-1547 feedback loop, 2026-11-15):** the row was `parked: the second listen is an Artifact listening page, which needs an interactive session (headless waves have no Artifact tool) (3 extra-ezafe items; park reason in the slice file; the second listen of 342, built in #1098, is the same page: [342](docs/plan/slices/342-farsi-voice-pack.md))`.
