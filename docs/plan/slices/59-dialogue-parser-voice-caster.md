# Slice 59 — Dialogue parser and voice caster (pure library)

*Planned.*

**Batch:** 10
**Decisions:** 74, 75
**Depends on:** none (pure `net10.0`, no UI, no platform code; may start alongside Batch 9)
**Touches:** `src/StoryReader.Core/**`, `tests/StoryReader.Tests/**`, `ReadTheStupidText.slnx`, `.github/workflows/build.yml`, `CLAUDE.md`
**Story issue:** —

## Goal

Given story text, produce segments (narration / quoted line), the speaker of each
quote, a gender per character and a voice per character, all rule-based and on the
device. This is the novel part of Story Reader; it is proven in unit tests before
any UI exists.

## Steps

- [ ] New `src/StoryReader.Core` (`net10.0`, no framework dependencies). A separate
      library on purpose: Application stays product-neutral for Read The Stupid Text.
      The caster takes plain voice records (id, gender) so it needs no reference to
      Domain's voice types.
- [ ] Types (records and enums, no strings for closed sets): `Gender { Male, Female,
      Unknown }`, `SegmentKind { Narration, Quote }`, `Segment(Kind, Text,
      SourceStart, SourceEnd, CharacterId?)`, `Character(Id, Name, Gender, WordCount)`.
- [ ] `StorySegmenter`: split on quote marks (straight and curly double quotes first)
      into ordered segments with **source ranges** (needed by Slices 60 and 62).
      Handle a quote spanning a paragraph, nested single quotes and an unclosed quote.
- [ ] `SpeakerAttributor`: attribute each quote by (1) "X said" / "said X" with a verb
      list (replied, asked, shouted, whispered, ...) in the same sentence or the
      adjacent narration; (2) pronoun tracking ("she said" -> last female character);
      (3) alternating speakers in untagged back-and-forth. An unattributable quote
      keeps `CharacterId = null` (voice rule decided in Slice 65).
- [ ] `GenderGuesser`: pronouns near the name, a small embedded first-names list
      (English), titles (Mr, Mrs, Miss, Lady, King, Queen, ...). Unknown stays Unknown.
- [ ] `VoiceCaster` (Decision 75): narrator voice excluded; no two characters share a
      voice while enough exist; otherwise the gender-matched voice with the **fewest
      words spoken so far** (running count per voice); main characters' voices (top N
      by words, default 2) are never reused. Deterministic: ties broken by voice order;
      characters processed in order of first appearance.
- [ ] Rule tables sit behind a `LanguageRules` record (English only here) so Slice 66
      adds languages without touching the algorithm.
- [ ] Tests in a new `tests/StoryReader.Tests` (xUnit v3, `net10.0`, like
      `tests/ReadTheStupidText.Tests`); `InternalsVisibleTo("StoryReader.Tests")` from
      `StoryReader.Core` so helpers stay `internal`. Add both projects to
      `ReadTheStupidText.slnx` and a `test` step in `build.yml` (path group
      `storyreader`, once Slice 58 adds it).

## Acceptance

- Peter / Cotton Tail example attributes **3/3** quotes (Peter, Cotton Tail, Peter);
  Peter is Male, Cotton Tail Female or Unknown with the rule recorded here.
- Casting with narrator Male2: Peter Male1, Cotton Tail Female1, third quote Peter Male1.
- A story with more than 8 characters: the fewest-words fallback picks the expected
  voice, main characters' voices are never handed out again, narrator never reused.
- `dotnet test tests/StoryReader.Tests` green; the library has no package references.

## Risks / notes

- A names-list gender guess can be wrong; the cast screen (Slice 65) is the fix.
- "Cotton Tail" is two words: tokenise names as capitalised runs, not single words.
