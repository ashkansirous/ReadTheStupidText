# Which sound-word spellings sound right when read aloud (Slice 145)

*2026-10-26, re-run with 5 takes per clip after review. Measured with `tools/sound-words-test/` (Python, sherpa-onnx 1.13.3 = the
version the apps ship, the same packs from the `packs-7` release: Supertonic-3 v3, Kokoro v2, Piper fa v2, Melo zh-en v1).
Slice file: docs/plan/slices/145-sound-words-aloud-test.md.*

**Nobody has listened yet.** Every verdict below is from numbers and a Whisper-small round trip. The user's listen (list at the
end) still decides; treat the winners as a shortlist. **Only en, es, zh and fa were run.** Every other language in the story set
(fr, pt, ru, ja, ar, da, fi, sv, nl, nb) and German (no engine we ship serves it) is untested: no number here applies to them.

## How a spelling was judged

Per voice, 4 sounds (bang, whoosh, knock, animal) x 6-7 spellings, every clip synthesised **5 times** (about 4,000 clips with the
sentence and pace sets), at speed 1.0 unless a pace is named.

- **Spread.** Supertonic, Piper and Melo start from noise, so one clip is one sample (a re-run of the first version showed
  +-25 % between takes of the same text). Cells are **mean±sd ms over 5 takes**. A difference is called real only when the
  ranges (min..max of the 5 takes) of the two spellings do not overlap in that voice; the ratio column says in how many voices
  that held. **Kokoro (en, es, zh) is nearly deterministic** (audio sd under 15 ms, mostly 0-3), so its length differences are exact, but it is one voice
  reading one way. The ASR on those near-identical clips can still flip (`Oink, oink, oiiink!` spelled in 4 of 5 Kokoro takes with audio sd 1-13 ms): the Whisper decode is not fully deterministic, so the pooled k/20 hit counts carry decode noise of a take or two. Chinese and Persian Piper/Melo clips keep their spread.
- **ms** = speech length with leading/trailing silence trimmed (Supertonic pads ~0.5 s each side). **skip n** = n takes under
  120 ms of sound (the engine dropped the word). **gap n** = takes with a silent hole over 250 ms that the spelling does not
  write. **spelled n** = takes the ASR heard as 3+ single letters. The ratio column is against **the first row of each table**
  (the Spanish whoosh baseline is `¡Fiu!`, the Farsi bang baseline the plain `دنگ!`). **A ratio is comparable only for a row that
  is the same word as the baseline.** Rows with a different word (zh `呼` against `嗖`, `汪汪汪！`/`喵喵！` against `哞！`, `叩叩叩！`
  against `咚！`; en `Tok tok tok!` against `Knock!`; es `¡Tocatoc!` against `¡Toc!`, `¡Mu! ¡Mu!` against `¡Muu!`; fa `میو میو!`,
  `بع بع!`, `ویژژژ!` against the baseline of their table) show a different sound, not a different spelling: read their ms, not the ratio.
- **k/5 = takes whose Whisper-small transcript contained the word as an exact token** (lower-cased, accents off, letter runs
  squeezed: `Muuu` = `mu`; no substring or fuzzy match). **A weak signal everywhere**: Whisper is not trained on onomatopoeia, a
  deterministic voice scores 0/5 or 5/5 on the same wrong transcript, and the first version's loose match let "I ain't",
  "Mundo" and "Dying." count as hits (loose 89 / 66 / 35 of 96 against 64 / 32 / 4 exact, en / es / fa). It cannot rank Chinese
  or Persian at all (near-zero hits whatever the spelling). The shortlist uses it only to avoid clear losses.
- **What the app does that the harness now copies.** The text is split into units exactly as `SpeechTextChunker.SplitSentences`
  and `SentenceUnits.Split` do (a sound under 15 characters is glued to its neighbour sentence), one engine call per unit,
  150 ms between units. Melo uses speaker 0, as the app does. **In a sentence:** each sound that is short enough to be glued
  (plus every spelling the skill recommends) was also spoken **before** ("lead") and **after** ("trail") a short sentence
  (`Pip ran away.`, `Pip salió corriendo.`, `小猪跑开了。`, `بچه خوک فرار کرد.`); the table cell is the ms it adds over the
  sentence alone. A sound as its own quote is the "alone" row.
- **Farsi flavours.** The roster flavours reach the engine as a pace (Sleepy .85, Big .92, Small 1.08, Bouncy 1.15, from
  `VoiceFlavours.ShapeOf`); the pitch shift runs on the finished samples, so it cannot make the engine keep or drop a word and is
  **not simulated**. Mana, ganji and gyro were run at those four paces on the baseline and recommended spellings.

## Findings (numbers: mean over the takes; full tables below)

1. **Stretching letters is a small gain and costs recognition.** `Daaang!` x1.13, `Daaannng!` x1.16, `Whooosh!` x1.14,
   `Woooooshhhh!` x1.38, `Oiiink!` x1.23, `Ooooink!` x1.20, `Knoooock!` x1.10 (English); `¡Puuum!` x1.27, `¡Puuuuum!` x1.56,
   `¡Fiuuu!` x1.27, `¡Fiiiiuuuu!` x1.63, `¡Muuu!` x1.16, `¡Muuuuuu!` x1.54 (Spanish): each beyond the spread in 2-4 of 4 voices.
   Against the plain word the ASR hit rate (pooled over 4 voices x 5 takes) fell: `Knock!` 19/20 to `Knoooock!` 0/20, `Dang!` 19/20
   to `Daaang!` 13/20 and `Daaannng!` 6/20, `Whoosh!` 18/20 to `Whooosh!` 11/20 and `Woooooshhhh!` 3/20; `Oink!` 10/20 to
   `Oiiink!` 9/20 (no loss). No engine read a vowel run letter by letter on its own.
2. **Repetition lengthens more and the voices read it.** The triples and `...` forms gave x2.0-2.6 in English and Spanish (`Dang!
   Dang! Dang!` x2.02, `Knock! Knock! Knock!` x2.17, `Oink, oink, oink!` x2.17, `¡Pum! ¡Pum! ¡Pum!` x2.30, `¡Toc! ¡Toc!
   ¡Toc!` x2.56; `Dang... dang... dang!` x2.09, `¡Toc... toc... toc!` x2.37), each beyond the spread in 4 of 4 voices. The two-word
   forms are lower and less even: `Dang! Dang!` x1.39, `Knock knock!` x1.36, `Whoosh! Whoosh!` x1.66, `¡Toc, toc!` x1.68,
   `¡Fiu! ¡Fiu!` x1.37, `Oink... oink!` x1.54, `Whoosh... whoosh!` x1.64; `¡Fiu... fiu!` stops at x1.44.
3. **A doubled short word can add nothing on Supertonic.** Spanish `¡Pum! ¡Pum!` 360±38 / 364±90 ms against single `¡Pum!` 361±45 / 327±20
   (F1 / M1), `¡Mu! ¡Mu!` 420±93 / 391±66 against `¡Muu!` 422±46 / 374±42; Kokoro reads both repeats (`¡Pum! ¡Pum!` x1.53 / x1.68; `¡Mu! ¡Mu!` only x1.26 / x1.29 against `¡Muu!`). English `Dang! Dang!`
   did **not** collapse in the re-run (511±107 / 428±56 against 398±21 / 316±20: longer in 3 of 4 voices, the F1 ranges touch).
   After a sentence the same Spanish double adds 960-1120 ms on Supertonic (441-847 ms on Kokoro), so on Supertonic the weakness is seen only when the double stands alone. Repeated plain words as a triple were never short in English or Spanish; the exceptions are
   Farsi `دَنگ! دَنگ! دَنگ!` on mana (2±2 ms, skipped 5/5) and Chinese `砰……砰……砰！` on Melo (285±349, skipped 3/5).
4. **Stretch inside a repeat can be spelled out by Kokoro:** `Oink, oink, oiiink!` was flagged spelled in 4 of 5 takes on both
   Kokoro voices (and Kokoro Alex `¡Fiu... fiu!` 4/5). Keep stretching and repeating apart.
5. **Chinese.** Kokoro reads every spelling (210-850 ms). **Melo drops a lone character**: `砰！` 32±22 ms (skipped 5/5 takes),
   `嘭！` 55 (5/5), `嗖！` 17 (5/5), `咚！` 76 (4/5), `哞！` 76±62 (2/5); `呼——` 0 (5/5), `砰——` 47 (5/5), `哞——` 12 (5/5). `砰——` and `哞——` are shorter than the plain
   character on Kokoro (x0.67 / x0.68, shorter in 2 and 1 of 3 voices) and dropped by Melo; `呼——` (x0.52) is a different character from the `嗖！` baseline and was not compared with `呼！`. The dash **between** two characters
   is fine (`嗖——嗖！` x2.30, `咚——咚！` x2.05, not dropped). `……`: `砰……砰……砰！` is unstable on Melo (285±349, skipped 3/5) but
   `咚……咚……咚！` (782±104) and `呼……呼！` (501±65) were never dropped. **Repeated characters** (`砰砰砰！`, `咚咚咚！`, `嗖嗖嗖！`,
   `汪汪汪！`, `哞哞！`, `喵喵！`) were never skipped by any voice (x1.6-3.3). Glued to a sentence Melo adds 110-430 ms for a lone character (more than alone, so
   it is not dropped there; not verified by ear), and Kokoro adds only 90-190 ms for a lone character after a sentence. The ASR cannot judge Chinese; nobody has listened.
6. **Farsi: the voices differ, and mana (the default since Slice 144) is the weak one.** See the drop table below.
   `دنگ! دنگ! دنگ!` was synthesised this time: 1638±76 / 1390±61 / 906±24 / 984±214 ms on ganji / ganji_adabi / gyro / mana
   (x4.19 against `دنگ!`, longer in 4 of 4 voices, no take dropped on any voice alone or in a sentence, 1 of 20 at the flavour paces
   on mana). The diacritic forms are **not** shorter on ganji / adabi / gyro (`دَنگ!` 400±48 / 422±45 / 243±9 against 421±43 / 359±46 /
   231±31: ranges overlap) but **mana drops them** (`دَنگ!` 2±3 ms, skipped 5/5; `دَنگ! دَنگ! دَنگ!` 2±2, skipped 5/5). `هاااپ هاپ!` is x1.09
   (no loss, no gain). Stretched Farsi consonants measured x1.15-2.93 (`ویژژژ!` x2.93, longer in 4/4, but a different word from the `ووش!` baseline; `تققق!` x2.30, `دنگگگ!` x1.86, `ووووش!` x1.65, `بوووم!` x1.15). The ASR scores nearly every Farsi cell 0-1/5 whatever the spelling, so it says nothing here. Mana: `ویژژژ!` 926±564 (wide), `بوووم!` skipped 3/5. Left to the listen.
7. **Spanish** words are heard less often than English (Kokoro es hears the plain words 0-2 of 5), so the ASR gives little; the lengths
   above are what stands. Tripled bangs and `¡Toc... toc... toc!` were read by every voice.

### Does Piper mana drop short Farsi sound words, even in a sentence?

**Yes.** Share of takes that added under 120 ms (in a sentence: under 120 ms over the sentence alone):

| Voice | alone (all) | alone (recommended) | in context (lead/trail) | at flavour paces |
|---|---|---|---|---|
| piper ganji | 0/125 (0 %) | 0/30 (0 %) | 0/240 (0 %) | 0/180 (0 %) |
| piper ganji_adabi | 0/125 (0 %) | 0/30 (0 %) | 0/240 (0 %) | - |
| piper gyro | 1/125 (0 %) | 0/30 (0 %) | 0/240 (0 %) | 9/180 (5 %) |
| piper mana | 33/125 (26 %) | 2/30 (6 %) | 21/240 (8 %) | 42/180 (23 %) |

Mana drops, per 5 takes alone: `تق!` 4, `دنگ!` 3, `بوووم!` 3, `ووش!` 2, `ووش! ووش!` 2, `تق! تق!` 3, `میو میو!` 2, `میوووو!` 4. In a sentence it
still drops `دنگ!` (trail 2/5), `ووش!` (trail 2/5), `تق!` (trail 3/5), `تق! تق!` (lead 3/5). At the flavour paces: `تق!` 16 of 20, `ووش!` 9 of 20,
`دنگ!` 6 of 20, `ووش… ووش!` 6 of 20, `میو میو!` 4 of 20; `دنگ! دنگ! دنگ!` 1 of 20. **Never dropped** (alone, in a sentence or at any pace): `تق تق تق!`,
`هاپ هاپ!` (but 604±246 ms with a wide spread, one gap), `وووووش… ووش!`, `دنگ… دنگ… دنگ!`. Gyro drops only `ووش!` (alone 1/5, paces 9/20). Ganji and ganji_adabi
dropped nothing. The flavour paces (.85 to 1.15) do not rescue a dropped word (they only shift the odds) and do not change the others.
`ووش… ووش!` is short on mana and gyro (190±31, 335±22 ms against 841 on ganji), and `میو میو!` is unreliable on mana (208±132).

## Provisional winners (until the listen)

| Sound | en | es | zh | fa |
|---|---|---|---|---|
| bang | `Dang! Dang! Dang!` | `¡Pum! ¡Pum! ¡Pum!` | `砰砰砰！` | `دنگ! دنگ! دنگ!` (measured) |
| whoosh | `Whoosh... whoosh!` or `Whoosh! Whoosh!` | `¡Fiu! ¡Fiu!` (x1.37 but not shown to help on Supertonic: ranges overlap; no triple was run; listen) | `嗖嗖嗖！` | `وووووش… ووش!` (mana 782, gyro 462, ganji 1063 ms); `ووش… ووش!` is short on mana and gyro |
| knock | `Knock! Knock! Knock!` or `Knock knock!` | `¡Toc... toc... toc!` or `¡Toc, toc!` | `咚咚咚！` | `تق تق تق!` (measured 800-1030 ms on all four, never dropped; ASR heard it 0 of 20 alone) |
| animal | `Oink, oink, oink!` (`Oiiink!` is neutral for the ASR but only x1.23) | `¡Muuu!` x1.16 (inside the noise on Supertonic; ASR on M1 5/5 to 3/5) or `¡Muu... muuu!` x1.59 | `汪汪汪！`, `哞哞！`, `喵喵！` | `هاپ هاپ!` (all voices); `میو میو!` is dropped by mana |

## Results (ms of speech, mean±sd over 5 takes, then skip / spelled / gap, then ASR exact-token hits)

### en

**bang** alone (ms, mean±sd over the takes)

| Spelling | supertonic F1 | supertonic M1 | kokoro heart (f) | kokoro michael (m) | vs `Dang!` |
|---|---|---|---|---|---|
| `Dang!` | 398±21 4/5 | 316±20 5/5 | 446±0 5/5 | 517±11 5/5 | baseline |
| `Daaang!` | 411±42 4/5 | 440±51 5/5 | 477±0 4/5 | 531±9 0/5 | x1.13 (longer in 3/4, shorter in 0) |
| `Daaannng!` | 425±22 3/5 | 409±26 3/5 | 497±0 0/5 | 596±2 0/5 | x1.16 (longer in 3/4, shorter in 0) |
| `Dang! Dang!` | 511±107 5/5 | 428±56 5/5 | 658±0 5/5 | 746±2 4/5 | x1.39 (longer in 3/4, shorter in 0) |
| `Dang... dang... dang!` | 878±75 4/5 | 919±86 5/5 | 723±1 5/5 | 829±0 5/5 | x2.09 (longer in 4/4, shorter in 0) |
| `Dang! Dang! Dang!` | 791±73 5/5 | 737±59 5/5 | 857±0 5/5 | 952±0 5/5 | x2.02 (longer in 4/4, shorter in 0) | 

**whoosh** alone (ms, mean±sd over the takes)

| Spelling | supertonic F1 | supertonic M1 | kokoro heart (f) | kokoro michael (m) | vs `Whoosh!` |
|---|---|---|---|---|---|
| `Whoosh!` | 458±34 5/5 | 381±48 5/5 | 479±3 3/5 | 545±4 5/5 | baseline |
| `Whooosh!` | 476±71 4/5 | 474±40 5/5 | 550±0 0/5 | 624±0 2/5 | x1.14 (longer in 2/4, shorter in 0) |
| `Woooooshhhh!` | 639±54 1/5 | 608±28 0/5 | 606±0 0/5 | 693±0 2/5 | x1.38 (longer in 4/4, shorter in 0) |
| `Whoosh! Whoosh!` | 736±70 5/5 | 749±56 5/5 | 768±2 3/5 | 804±3 5/5 | x1.66 (longer in 4/4, shorter in 0) |
| `Whoosh... whoosh!` | 792±61 5/5 | 775±26 5/5 | 651±1 2/5 | 777±2 5/5 | x1.64 (longer in 4/4, shorter in 0) |
| `Whooooosh... whoosh!` | 1093±46 5/5 | 1000±46 5/5 | 782±0 0/5 | 898±3 4/5 | x2.07 (longer in 4/4, shorter in 0) | 

**knock** alone (ms, mean±sd over the takes)

| Spelling | supertonic F1 | supertonic M1 | kokoro heart (f) | kokoro michael (m) | vs `Knock!` |
|---|---|---|---|---|---|
| `Knock!` | 471±59 4/5 | 474±45 5/5 | 470±2 5/5 | 584±5 5/5 | baseline |
| `Knock knock!` | 580±52 5/5 | 652±31 5/5 | 681±2 5/5 | 799±2 5/5 | x1.36 (longer in 3/4, shorter in 0) |
| `Knock! Knock! Knock!` | 1142±85 5/5 | 1096±65 5/5 | 962±3 5/5 | 1117±1 5/5 | x2.17 (longer in 4/4, shorter in 0) |
| `Knoooock!` | 513±60 0/5 | 510±54 0/5 | 537±0 0/5 | 634±9 0/5 | x1.10 (longer in 2/4, shorter in 0) |
| `Knock... knock... knock!` | 1368±92 5/5 | 1368±90 5/5 | 880±2 5/5 | 1016±3 5/5 | x2.35 (longer in 4/4, shorter in 0) |
| `Tok tok tok!` | 667±88 0/5 | 689±36 0/5 | 923±4 0/5 | 1033±7 0/5 | x1.65 (longer in 3/4, shorter in 0) | 

**animal** alone (ms, mean±sd over the takes)

| Spelling | supertonic F1 | supertonic M1 | kokoro heart (f) | kokoro michael (m) | vs `Oink!` |
|---|---|---|---|---|---|
| `Oink!` | 406±16 5/5 | 380±18 5/5 | 458±1 0/5 | 524±1 0/5 | baseline |
| `Oiiink!` | 512±50 5/5 | 462±68 4/5 | 550±0 0/5 | 653±1 0/5 | x1.23 (longer in 2/4, shorter in 0) |
| `Oink, oink, oink!` | 890±64 4/5 | 865±35 4/5 | 996±3 3/5 | 1068±8 5/5 | x2.17 (longer in 4/4, shorter in 0) |
| `Oink, oink, oiiink!` | 1036±55 5/5 | 952±64 5/5 | 1179±1 spelled 4 0/5 | 1257±13 spelled 4 0/5 | x2.51 (longer in 4/4, shorter in 0) |
| `Ooooink!` | 457±39 2/5 | 480±68 0/5 | 541±3 0/5 | 654±1 1/5 | x1.20 (longer in 2/4, shorter in 0) |
| `Oink... oink!` | 715±44 5/5 | 582±100 5/5 | 678±0 0/5 | 718±0 0/5 | x1.54 (longer in 3/4, shorter in 0) | 

**bang** alone (ms, mean±sd over the takes)

| Spelling | supertonic F1 | supertonic M1 | kokoro dora (f) | kokoro alex (m) | vs `¡Pum!` |
|---|---|---|---|---|---|
| `¡Pum!` | 361±45 5/5 | 327±20 4/5 | 404±2 0/5 | 397±1 0/5 | baseline |
| `¡Puuum!` | 411±39 5/5 | 462±18 3/5 | 503±0 0/5 | 506±0 0/5 | x1.27 (longer in 3/4, shorter in 0) |
| `¡Puuuuum!` | 534±24 4/5 | 508±70 2/5 | 625±0 0/5 | 651±0 0/5 | x1.56 (longer in 4/4, shorter in 0) |
| `¡Pum! ¡Pum!` | 360±38 5/5 | 364±90 4/5 | 619±0 0/5 | 666±1 0/5 | x1.33 (longer in 2/4, shorter in 0) |
| `¡Pum... pum... pum!` | 771±43 5/5 | 822±69 5/5 | 775±0 5/5 | 822±0 5/5 | x2.16 (longer in 4/4, shorter in 0) |
| `¡Pum! ¡Pum! ¡Pum!` | 781±122 5/5 | 797±74 4/5 | 902±0 5/5 | 943±0 2/5 | x2.30 (longer in 4/4, shorter in 0) | 

**whoosh** alone (ms, mean±sd over the takes)

| Spelling | supertonic F1 | supertonic M1 | kokoro dora (f) | kokoro alex (m) | vs `¡Fiu!` |
|---|---|---|---|---|---|
| `¡Fiu!` | 414±62 1/5 | 328±43 2/5 | 408±0 2/5 | 466±1 0/5 | baseline |
| `¡Fium!` | 401±63 1/5 | 336±26 2/5 | 471±10 0/5 | 434±1 0/5 | x1.02 (longer in 1/4, shorter in 1) |
| `¡Fiuuu!` | 508±33 4/5 | 427±35 4/5 | 540±4 0/5 | 574±0 0/5 | x1.27 (longer in 2/4, shorter in 0) |
| `¡Fiiiiuuuu!` | 639±24 5/5 | 574±18 5/5 | 676±9 1/5 | 736±2 0/5 | x1.63 (longer in 4/4, shorter in 0) |
| `¡Fiu! ¡Fiu!` | 496±80 0/5 | 491±115 1/5 | 567±0 3/5 | 646±3 0/5 | x1.37 (longer in 2/4, shorter in 0) |
| `¡Fiu... fiu!` | 626±31 0/5 | 562±40 0/5 | 523±0 0/5 | 587±1 spelled 4 0/5 | x1.44 (longer in 4/4, shorter in 0) |
| `¡Fiuuu... fiu!` | 799±52 2/5 | 715±53 3/5 | 668±0 0/5 | 707±5 0/5 | x1.82 (longer in 4/4, shorter in 0) | 

**knock** alone (ms, mean±sd over the takes)

| Spelling | supertonic F1 | supertonic M1 | kokoro dora (f) | kokoro alex (m) | vs `¡Toc!` |
|---|---|---|---|---|---|
| `¡Toc!` | 331±45 5/5 | 289±18 4/5 | 356±0 0/5 | 405±0 0/5 | baseline |
| `¡Toc, toc!` | 546±75 5/5 | 491±55 5/5 | 643±1 5/5 | 644±10 0/5 | x1.68 (longer in 4/4, shorter in 0) |
| `¡Toc! ¡Toc! ¡Toc!` | 848±41 5/5 | 793±93 5/5 | 913±0 0/5 | 958±0 0/5 | x2.56 (longer in 4/4, shorter in 0) |
| `¡Toooc!` | 355±42 1/5 | 389±53 2/5 | 507±0 0/5 | 514±0 0/5 | x1.28 (longer in 3/4, shorter in 0) |
| `¡Toc... toc... toc!` | 785±68 5/5 | 824±32 5/5 | 776±0 5/5 | 845±0 3/5 | x2.37 (longer in 4/4, shorter in 0) |
| `¡Tocatoc!` | 546±47 0/5 | 507±33 0/5 | 690±0 0/5 | 740±15 0/5 | x1.79 (longer in 4/4, shorter in 0) | 

**animal** alone (ms, mean±sd over the takes)

| Spelling | supertonic F1 | supertonic M1 | kokoro dora (f) | kokoro alex (m) | vs `¡Muu!` |
|---|---|---|---|---|---|
| `¡Muu!` | 422±46 5/5 | 374±42 5/5 | 453±0 0/5 | 480±6 0/5 | baseline |
| `¡Muuu!` | 498±58 5/5 | 466±60 3/5 | 492±1 0/5 | 535±0 0/5 | x1.16 (longer in 2/4, shorter in 0) |
| `¡Muuuuuu!` | 605±63 5/5 | 637±51 4/5 | 696±0 0/5 | 721±0 0/5 | x1.54 (longer in 4/4, shorter in 0) |
| `¡Muu, muu, muuu!` | 825±101 1/5 | 825±39 3/5 | 908±0 0/5 | 962±18 0/5 | x2.04 (longer in 4/4, shorter in 0) |
| `¡Mu! ¡Mu!` | 420±93 5/5 | 391±66 5/5 | 573±0 0/5 | 620±1 0/5 | x1.15 (longer in 2/4, shorter in 0) |
| `¡Muu... muuu!` | 693±21 2/5 | 676±31 3/5 | 656±3 0/5 | 696±0 0/5 | x1.59 (longer in 4/4, shorter in 0) | 

**bang** alone (ms, mean±sd over the takes)

| Spelling | kokoro xiaoxiao (f) | kokoro yunxi (m) | melo | vs `砰！` |
|---|---|---|---|---|
| `砰！` | 375±2 0/5 | 262±1 0/5 | 32±22 skip 5 0/5 | baseline |
| `砰砰砰！` | 788±2 0/5 | 622±0 0/5 | 531±6 0/5 | x2.97 (longer in 3/3, shorter in 0) |
| `砰——` | 305±0 0/5 | 211±3 0/5 | 47±12 skip 5 0/5 | x0.67 (longer in 0/3, shorter in 2) |
| `嘭！` | 376±1 0/5 | 262±0 0/5 | 55±24 skip 5 0/5 | x0.82 (longer in 0/3, shorter in 0) |
| `砰！砰！` | 541±0 0/5 | 431±0 0/5 | 351±151 skip 1 0/5 | x2.00 (longer in 2/3, shorter in 0) |
| `砰……砰……砰！` | 788±0 0/5 | 621±2 0/5 | 285±349 skip 3 0/5 | x2.28 (longer in 2/3, shorter in 0) | 

**whoosh** alone (ms, mean±sd over the takes)

| Spelling | kokoro xiaoxiao (f) | kokoro yunxi (m) | melo | vs `嗖！` |
|---|---|---|---|---|
| `嗖！` | 399±0 0/5 | 296±1 0/5 | 17±34 skip 5 0/5 | baseline |
| `嗖嗖嗖！` | 811±1 2/5 | 696±1 4/5 | 645±8 1/5 | x3.25 (longer in 3/3, shorter in 0) |
| `呼——` | 282±1 0/5 | 250±2 1/5 | 0±0 skip 5 0/5 | x0.52 (longer in 0/3, shorter in 2) |
| `呼呼呼！` | 740±3 1/5 | 614±1 0/5 | 464±5 1/5 | x2.60 (longer in 3/3, shorter in 0) |
| `嗖——嗖！` | 616±1 0/5 | 533±4 1/5 | 427±10 0/5 | x2.30 (longer in 3/3, shorter in 0) |
| `呼……呼！` | 526±2 1/5 | 470±0 0/5 | 501±65 0/5 | x2.36 (longer in 3/3, shorter in 0) | 

**knock** alone (ms, mean±sd over the takes)

| Spelling | kokoro xiaoxiao (f) | kokoro yunxi (m) | melo | vs `咚！` |
|---|---|---|---|---|
| `咚！` | 357±0 0/5 | 278±0 0/5 | 76±41 skip 4 0/5 | baseline |
| `咚咚咚！` | 790±0 0/5 | 616±0 0/5 | 544±6 0/5 | x2.99 (longer in 3/3, shorter in 0) |
| `咚——咚！` | 593±1 0/5 | 451±1 0/5 | 342±7 1/5 | x2.05 (longer in 3/3, shorter in 0) |
| `咚咚！` | 593±1 0/5 | 451±1 0/5 | 332±5 0/5 | x2.02 (longer in 3/3, shorter in 0) |
| `咚……咚……咚！` | 790±0 0/5 | 615±0 0/5 | 782±104 0/5 | x3.65 (longer in 3/3, shorter in 0) |
| `叩叩叩！` | 851±1 0/5 | 680±1 0/5 | 684±15 0/5 | x3.51 (longer in 3/3, shorter in 0) | 

**animal** alone (ms, mean±sd over the takes)

| Spelling | kokoro xiaoxiao (f) | kokoro yunxi (m) | melo | vs `哞！` |
|---|---|---|---|---|
| `哞！` | 495±0 0/5 | 306±0 0/5 | 76±62 skip 2 0/5 | baseline |
| `哞哞！` | 583±0 0/5 | 415±2 0/5 | 266±5 0/5 | x1.58 (longer in 3/3, shorter in 0) |
| `哞——` | 526±1 0/5 | 267±0 0/5 | 12±20 skip 5 0/5 | x0.68 (longer in 1/3, shorter in 1) |
| `汪汪汪！` | 694±2 1/5 | 546±2 0/5 | 419±10 0/5 | x2.23 (longer in 3/3, shorter in 0) |
| `喵喵！` | 598±0 0/5 | 480±0 0/5 | 366±12 0/5 | x1.94 (longer in 3/3, shorter in 0) |
| `喵——喵！` | 597±2 0/5 | 479±0 0/5 | 372±4 0/5 | x1.96 (longer in 3/3, shorter in 0) | 

**bang** alone (ms, mean±sd over the takes)

| Spelling | piper ganji | piper ganji_adabi | piper gyro | piper mana | vs `دنگ!` |
|---|---|---|---|---|---|
| `دنگ!` | 421±43 0/5 | 359±46 0/5 | 231±31 0/5 | 195±161 skip 3 0/5 | baseline |
| `دَنگ!` | 400±48 2/5 | 422±45 1/5 | 243±9 0/5 | 2±3 skip 5 0/5 | x0.80 (longer in 0/4, shorter in 1) |
| `دنگگگ!` | 721±51 0/5 | 704±22 1/5 | 277±53 0/5 | 503±81 0/5 | x1.86 (longer in 2/4, shorter in 0) |
| `دنگ! دنگ! دنگ!` | 1638±76 1/5 | 1390±61 0/5 | 906±24 0/5 | 984±214 0/5 | x4.19 (longer in 4/4, shorter in 0) |
| `دَنگ! دَنگ! دَنگ!` | 1484±63 1/5 | 1592±118 0/5 | 919±67 2/5 | 2±2 skip 5 0/5 | x2.99 (longer in 3/4, shorter in 1) |
| `دنگ… دنگ… دنگ!` | 855±55 1/5 | 758±20 0/5 | 576±41 0/5 | 943±137 0/5 | x2.87 (longer in 4/4, shorter in 0) |
| `بوووم!` | 419±54 0/5 | 444±35 0/5 | 344±42 0/5 | 173±143 skip 3 0/5 | x1.15 (longer in 0/4, shorter in 0) | 

**whoosh** alone (ms, mean±sd over the takes)

| Spelling | piper ganji | piper ganji_adabi | piper gyro | piper mana | vs `ووش!` |
|---|---|---|---|---|---|
| `ووش!` | 431±67 1/5 | 523±47 0/5 | 163±48 skip 1 0/5 | 144±171 skip 2 0/5 | baseline |
| `ووووش!` | 667±78 0/5 | 555±65 0/5 | 331±39 0/5 | 283±64 0/5 | x1.65 (longer in 2/4, shorter in 0) |
| `ووش! ووش!` | 1045±95 1/5 | 1208±73 0/5 | 297±46 0/5 | 385±291 skip 2 0/5 | x2.31 (longer in 3/4, shorter in 0) |
| `ووش… ووش!` | 841±92 0/5 | 968±82 0/5 | 335±22 0/5 | 190±31 0/5 | x1.79 (longer in 3/4, shorter in 0) |
| `وووووش… ووش!` | 1063±93 0/5 | 771±81 0/5 | 462±46 0/5 | 782±76 1/5 | x3.05 (longer in 4/4, shorter in 0) |
| `ویژژژ!` | 619±48 0/5 | 660±47 0/5 | 420±57 0/5 | 926±564 0/5 | x2.93 (longer in 4/4, shorter in 0) | 

**knock** alone (ms, mean±sd over the takes)

| Spelling | piper ganji | piper ganji_adabi | piper gyro | piper mana | vs `تق!` |
|---|---|---|---|---|---|
| `تق!` | 311±44 0/5 | 319±40 2/5 | 194±26 0/5 | 31±51 skip 4 0/5 | baseline |
| `تق تق تق!` | 1026±44 0/5 | 1023±42 0/5 | 817±65 0/5 | 803±73 0/5 | x4.35 (longer in 4/4, shorter in 0) |
| `تَق… تَق… تَق!` | 702±22 2/5 | 582±14 2/5 | 469±30 1/5 | 775±81 0/5 | x3.24 (longer in 4/4, shorter in 0) |
| `تققق!` | 477±29 1/5 | 372±58 0/5 | 384±32 0/5 | 544±21 0/5 | x2.30 (longer in 3/4, shorter in 0) |
| `تق! تق!` | 704±56 0/5 | 816±83 0/5 | 506±48 0/5 | 210±179 skip 3 0/5 | x2.29 (longer in 3/4, shorter in 0) |
| `تَقْ تَقْ!` | 670±46 0/5 | 638±56 0/5 | 522±4 0/5 | 849±97 1/5 | x3.48 (longer in 4/4, shorter in 0) | 

**animal** alone (ms, mean±sd over the takes)

| Spelling | piper ganji | piper ganji_adabi | piper gyro | piper mana | vs `هاپ هاپ!` |
|---|---|---|---|---|---|
| `هاپ هاپ!` | 607±81 0/5 | 846±63 0/5 | 389±43 1/5 | 604±246 0/5 | baseline |
| `هاااپ هاپ!` | 612±81 1/5 | 911±63 0/5 | 512±26 0/5 | 567±164 0/5 | x1.09 (longer in 1/4, shorter in 0) |
| `هاپ… هاپ… هاپ!` | 673±35 0/5 | 1024±28 0/5 | 566±20 0/5 | 954±205 0/5 | x1.34 (longer in 2/4, shorter in 0) |
| `میو میو!` | 522±43 0/5 | 623±88 0/5 | 509±53 0/5 | 208±132 skip 2 0/5 | x0.81 (longer in 0/4, shorter in 1) |
| `میوووو!` | 491±20 0/5 | 374±48 0/5 | 382±27 3/5 | 143±213 skip 4 0/5 | x0.62 (longer in 0/4, shorter in 1) |
| `بع بع!` | 415±39 0/5 | 431±49 0/5 | 347±26 0/5 | 491±122 0/5 | x0.72 (longer in 0/4, shorter in 1) |

## Still needs a human listen

The WAVs are **not committed**. The listen shortlist (first take of each clip: the baseline, every spelling the skill recommends,
up to three more per sound that no take dropped, and the recommended spellings before a sentence; 293 WAVs, 25 MB, folders
`<lang>/<voice>/<kind>/`) is copied to **`D:\ReadTheStupidText-worktrees\sound-words-listen\`**, outside any worktree so it survives the clean-up of this one,
together with `report.md` (the full tables, including the in-a-sentence and flavour-pace tables). The engines are stochastic, so a
winner is picked from those files, not from a rebuild; the rebuild is in `tools/sound-words-test/README.md` (about an hour of CPU).

1. **All Chinese and Persian** picks: the ASR could not judge them. Listen to the tripled-character and `…` forms, and to Persian
   `mana` and `gyro` against `ganji` (is a missing word audible? mana is now the default).
2. **English/Spanish**: does `Daaang!` / `Whooosh!` sound drawn out or like a stutter? Is `¡Pum! ¡Pum!` on Supertonic really one word?
3. Whether a 2-3x longer sound is *wanted* (a 1 s `Knock! Knock! Knock!` can feel slow in a read-aloud) and which one reads as the
   best-sounding to a child; the numbers cannot say.
4. The user's picks then replace the provisional table in `dino-storytelling` section 3 (Slice 145's last task stays open until then).
5. Other languages (fr, pt, ru, ja, ar, da, fi, sv, nl, nb) were never run; Slice 146's rewrite should use the repeat form there and
   flag each for a listen.
