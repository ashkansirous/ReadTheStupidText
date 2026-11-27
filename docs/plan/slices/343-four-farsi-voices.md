# Slice 343: Listening page: Four Farsi voices (Slice 89)
**Product:** Shared

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 89's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Windows:** Slice 89: Windows desktop acceptance of the four voices not run (a stale `desktop-rtl` dev package blocks deploy); iOS on a device; Pixel timing of cold voice preparation (7-8 s on the emulator, 16 s on a cold mid-read swap); a saved Amir choice falling back to Ganji Adabi, on a device; the play button shows idle after Back while chunks still play. ([slice](docs/plan/slices/89-four-farsi-voices.md))

*Park note (night 2026-11-17-2105, wave 1):* **parked: needs an interactive session.** The open build items (a comma at text-prep time for the extra-ezafe clips ganji-2, gyro-2, gyro-3; gyro-1 intermittent «است») need a design the 2026-11-16 answers leave open (whether it can be applied at text-prep time without changing how the app reads other sentences is not decided), and any fix needs a fresh listen on an Artifact listening page, which a headless wave cannot publish.

*Third listen done 2026-11-16 (answers below); 3 extra-ezafe items and `gyro-1` open as build work.* *Built in wave 2026-11-13, merged as PR #1098 (fdf26dc7), one PR with Slice 342 (same cause; the full record is in Slice 342's file). The three extra-ezafe items stay open. Not yet verified on a device.*

*Park note (night 2026-11-14-0726):* parked again by housekeeping 1 for the same reason (the second listen needs an Artifact page; headless wave sessions have no Artifact tool). Moved back to `planned` by the 2026-11-14-0726 feedback loop per the user's 2026-11-01 rule; a night run can only re-park it, so it needs an interactive session.

*Park note (night 2026-11-13-1149):* parked by wave 5 because the 3 extra-ezafe items have no text fix (the piper-fa VITS model adds the ezafe itself) and the second listen needs an interactive session (Artifact page; WAVs in `reviews/s342-ezafe/`). Moved back to `planned` by the 2026-11-13-1149 feedback loop so the next night retries it.

*Park note (2026-11-14, night 2026-11-14-1547 housekeeping 2):* parked: the second listen is an Artifact listening page, which needs an interactive session (headless waves have no Artifact tool).

*Plan-cleanup check 2026-11-25 (verdict: keep open, unparked).* Read at origin/main 1a8bf725: `FarsiEzafe.cs` still has only the force pair «حال شما» (kasra) and no comma or suppress pairs, so the extra-ezafe build is not started (`git grep ezafe origin/main -- src`); `user-says` finds nothing new after 2026-11-16. The park reason ("needs an interactive session, a fix needs a fresh listen") is not a wait: the third listen already chose the candidate (the comma fixed all three clips), and a re-listen is a check of the built fix, not a precondition to build it. Own work left: the extra-ezafe build below. The `gyro-1` item is answered by evidence already in this file (the model, not the renderer; intermittent) and is closed as the model's limit. The re-listen of the built fix moves to `unverified.md` as an `[owner]` ear check, not a wait inside this slice.

**Touches (fix items):** see Slice 342's file, which carries the shared design and code Touches.

*In progress (wave 2026-11-27), draft PR.* The extra-ezafe comma fix only; the re-listen stays an `[owner]` ear check.

**Touches:** `src/Shared/Sirous.Reading.Application/Reading/FarsiEzafe.cs`, `tests/Shared/Shared.Tests/FarsiEzafeTests.cs`, `docs/plan/slices/343-four-farsi-voices.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

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

**User answer 2026-11-15:** third listen "later". The page stays up with its `v3-*` answers empty; no wave renders or asks again until the user opens it.

### Third listen (2026-11-16, same page, `v3-*` answers; user pressed Done 2026-11-16 21:03Z)

Answers word for word (candidate, verdict, note). The "unchanged" clips carry no verdict, only a note; the page stored an empty verdict for them.

- ganji-2 unchanged: (no verdict) "it says "sobhe" instead of "sobh""
- ganji-2 comma («صبح، یک»): ok
- ganji-2 zero-width non-joiner («صبح‌ یک»): ok
- ganji-2 slower (length scale 1.15): ok
- gyro-1 take 1 (as the app plays it): bad, "say "have kheili khoob oost""
- gyro-1 take 2 (as the app plays it): ok
- gyro-1 slower (length scale 1.15): ok
- gyro-2 unchanged: (no verdict) "says "har rooze" instead of "har rooz""
- gyro-2 comma («روز، صبح»): ok
- gyro-2 zero-width non-joiner: bad, "says "har rooze" instead of "har rooz""
- gyro-2 slower (length scale 1.15): bad, "says "har rooze" instead of "har rooz""
- gyro-3 unchanged: bad, "say "25 daftare kharid" instead of "25 daftar""
- gyro-3 comma («دفتر، خرید»): ok
- gyro-3 zero-width non-joiner: ok
- gyro-3 slower (length scale 1.15): ok

What this decides (by the fix item above: cheapest model-side options first, otherwise record the model's limit):

- Extra ezafe, `ganji-2`: comma, zero-width non-joiner and slower pace each fixed it.
- Extra ezafe, `gyro-3`: comma, zero-width non-joiner and slower pace each fixed it.
- Extra ezafe, `gyro-2` («روز»): only the comma fixed it; zero-width non-joiner and slower pace did not.
- `gyro-1` («است» not spoken): no text candidate was offered. The two takes of the unchanged render differed (take 1 bad, take 2 ok), and slower pace was ok. The note on take 1 reads differently from the earlier "misses ast"; the user wrote "say "have kheili khoob oost"".
- reading: the comma is the one candidate that fixed all three extra-ezafe clips, so it is the likely render-time fix for all three; whether it can be applied at text-prep time without changing how the app reads other sentences is not decided by the answers and stays a build item. The `gyro-1` fault looks intermittent (one take bad, one ok), so one good take is not proof; slower pace is a candidate, not a settled fix.
- The model's-limit fallback in the fix item does not apply to the extra-ezafe clips, since a candidate worked for each.

The user's listening is done; no further listen is asked for unless a build item says so. The two fix items above stay unticked: they are build work (Claude's) and not started.

- [x] (closed 2026-11-25 as the model's limit: the app returns the samples untrimmed and the phonemes hold the word, see the evidence above; one take bad, one ok, slower pace ok; no build work) Fix: `gyro-1` (Gyro, «... خیلی خوب است.»): the final «است» ("ast") is not spoken (second listen, 2026-11-14). Find whether the sentence-final word is cut by the renderer (trailing audio trimmed, end-of-sentence handling) or by the model; fix through the review gate; re-render for a third listen.
- [ ] **Own work left, build it (no listen first):** in `FarsiEzafe.Apply` insert a comma at text-prep time for exactly the pairs the user heard fixed by it: «صبح یک» (ganji-2), «روز صبح» (gyro-2, comma after «روز»), «دفتر خرید» (gyro-3), as a pair table like `ForcePairs`, so no other sentence changes; tests in `FarsiEzafeTests`; review gate. (Decided 2026-11-25 from the third-listen answers.) Original wording follows. Fix: the extra-ezafe items (`ganji-2`, `gyro-2` now on «روز» too, `gyro-3`): no text mark stops them (espeak's phonemes are already right). Try the cheapest model-side options first and render each on one page for a third listen: a short break between the two words (comma or zero-width non-joiner at render time), a different noise/length scale for these two voices, or the same sentence on the other pack voices. If none works, record it as the model's limit for Ganji and Gyro (the default voice, Ganji Adabi, and Mana are right).

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen. Second listen done 2026-11-14 (above): the default voice is right; Ganji's and Gyro's open items need a third listen after their fixes.

**Park note (moved back to planned by the 2026-11-14-1547 feedback loop, 2026-11-15):** the row was `parked: the second listen is an Artifact listening page, which needs an interactive session (headless waves have no Artifact tool) (3 extra-ezafe items; park reason in the slice file; the second listen of 342, built in #1098, is the same page: [342](docs/plan/slices/342-farsi-voice-pack.md))`.
