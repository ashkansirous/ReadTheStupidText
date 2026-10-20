# Slice 59 — Dialogue parser and voice caster (pure library)

*Built (wave 2026-10-20), PR #175.*

**Batch:** 10
**Decisions:** 74, 75
**Depends on:** none (pure `net10.0`, no UI, no platform code; may start alongside Batch 9)
**Touches:** `src/DinoReader.Core/**`, `tests/DinoReader.Tests/**`, `ReadTheStupidText.slnx`, `.github/workflows/build.yml`, `CLAUDE.md`
**Story issue:** —

## Goal

Given story text, produce segments (narration / quoted line), the speaker of each
quote, a gender per character and a voice per character, all rule-based and on the
device. This is the novel part of Dino Reader; it is proven in unit tests before
any UI exists.

## Steps

- [x] New `src/DinoReader.Core` (`net10.0`, no framework dependencies). A separate
      library on purpose: Application stays product-neutral for Read The Stupid Text.
      The caster takes plain voice records (id, gender) so it needs no reference to
      Domain's voice types.
- [x] Types (records and enums, no strings for closed sets): `Gender { Male, Female,
      Unknown }`, `SegmentKind { Narration, Quote }`, `Segment(Kind, Text,
      SourceStart, SourceEnd, CharacterId?)`, `Character(Id, Name, Gender, WordCount)`.
- [x] `StorySegmenter`: split on quote marks (straight and curly double quotes first)
      into ordered segments with **source ranges** (needed by Slices 60 and 62).
      Handle a quote spanning a paragraph, nested single quotes and an unclosed quote.
- [x] `SpeakerAttributor`: attribute each quote by (1) "X said" / "said X" with a verb
      list (replied, asked, shouted, whispered, ...) in the same sentence or the
      adjacent narration; (2) pronoun tracking ("she said" -> last female character);
      (3) alternating speakers in untagged back-and-forth. An unattributable quote
      keeps `CharacterId = null` (voice rule decided in Slice 65).
- [x] `GenderGuesser`: pronouns near the name, a small embedded first-names list
      (English), titles (Mr, Mrs, Miss, Lady, King, Queen, ...). Unknown stays Unknown.
- [x] `VoiceCaster` (Decision 75): narrator voice excluded; no two characters share a
      voice while enough exist; otherwise the gender-matched voice with the **fewest
      words spoken so far** (running count per voice); main characters' voices (top N
      by words, default 2) are never reused. Deterministic: ties broken by voice order;
      characters processed in order of first appearance.
      **Few-voice packs** (e.g. Farsi, 1-2 voices; mock screen 15): when the pack has
      too few non-narrator voices, the main-never-reused rule relaxes and mains may
      share; the narrator exclusion stays absolute. The cast flags each shared voice
      (Slice 65).
- [x] Rule tables sit behind a `LanguageRules` record (English only here) so Slice 66
      adds languages without touching the algorithm.
- [x] Tests in a new `tests/DinoReader.Tests` (xUnit v3, `net10.0`, like
      `tests/ReadTheStupidText.Tests`); `InternalsVisibleTo("DinoReader.Tests")` from
      `DinoReader.Core` so helpers stay `internal`. Add both projects to
      `ReadTheStupidText.slnx` and a `test` step in `build.yml` (path group
      `dinoreader`, once Slice 58 adds it).

## Acceptance

- Peter / Cotton Tail example attributes **3/3** quotes (Peter, Cotton Tail, Peter);
  Peter is Male, Cotton Tail Female (a multi-word name is also looked up compacted, "cottontail", in the first-name tables).
- Casting with narrator Male2: Peter Male1, Cotton Tail Female1, third quote Peter Male1.
- A story with more than 8 characters: the fewest-words fallback picks the expected
  voice, main characters' voices are never handed out again, narrator never reused.
- `dotnet test tests/DinoReader.Tests` green; the library has no package references.

## Risks / notes

- A names-list gender guess can be wrong; the cast screen (Slice 65) is the fix.
- "Cotton Tail" is two words: tokenise names as capitalised runs, not single words.

## Built

`src/DinoReader.Core` is pure `net10.0` with no package or project references and
`InternalsVisibleTo("DinoReader.Tests")`. `tests/DinoReader.Tests` holds 75 tests.
Both projects are in `ReadTheStupidText.slnx`.

- `StorySegmenter`: straight and curly quotes, nested single quotes kept inside the
  quote. A quote ends at any paragraph break (blank line) and is marked unclosed
  unless the next paragraph reopens with a quote mark; a quote can also be unclosed
  at the end of the text. `Segment(Kind, Text, SourceStart, SourceEnd, CharacterId,
  IsClosed)`.
- `SpeakerAttributor`: a tag before a quote is preferred over a tag after. A
  lowercase-start tag after a quote belongs to it even when it leads into the next
  quote; a capitalised new-sentence lead-in binds only to the next quote. Pronoun tags
  resolve to the most recent name of that gender. Same-paragraph untagged quotes
  inherit the previous speaker unless the narration between has a different
  character as subject. Alternation applies only across a paragraph turn and within
  `DialogueGapChars`. Unclosed-quote continuation is handled.
- Name corroboration (`StoryNames`): a sentence-initial single word is a name only if
  it is in the name/title tables, was seen capitalised mid-sentence, or is the
  subject/object of a speech-verb tag. A single line feed is not a sentence start.
  `IndefinitePronouns` never become characters. The title regex is `\b`-anchored.
- `CharacterAliases`: "Peter" and "Peter Rabbit" merge when unambiguous.
- `GenderGuesser`: titles, then first-name tables (including compacted multi-word
  names), then pronouns after the name. The pronoun window is cut only at
  corroborated names; positions are cached per story.
- `VoiceCaster` (Decision 75): mains are cast first and never share while voices
  allow, then the others in appearance order, then overflow tiers. `IsShared` flag;
  duplicate voice ids are deduped.
- All language data is in `LanguageRules` (`LanguageRules.English`).
- `build.yml`: new `dinoreader` path filter and `changes` output; the `core` filter
  narrowed from `tests/**` to `tests/ReadTheStupidText.Tests/**`; new
  `test-dinoreader` job on ubuntu-latest that is in no release job's `needs`. A
  Dino-only push runs only that job (no version, release or store submission).

*Changed while building:* the coordinator first named the project `StoryReader.Core`
by mistake (the product was renamed Dino Reader on `main`); renamed to
`DinoReader.Core` / `DinoReader.Tests` in the fix pass. The Dino paths were first put
in the `core` filter, which would have built and submitted both RTST platforms for a
Dino-only change; replaced by the separate `dinoreader` group.

*From review:* the high-effort pass found 8 findings plus 2 low, all fixed (tag-before
precedence, mains sharing voices, alternation within a paragraph, uncorroborated
sentence-initial words as names, an unclosed quote inverting the rest of the story,
the gender window cut, `Segment.IsClosed`, compacted-name lookup, duplicate voice ids,
a shared name extractor). The medium pass found 10, 9 fixed; compound-name gender
tables were skipped (the tables are the design; Slice 66 extends them per language).

#### For CLAUDE.md (lifted 2026-10-20)

Lift after merge (the index pass marks the block lifted).

- Layer diagram gets `DinoReader.Core (standalone, no deps)`.
- **DinoReader.Core** (`net10.0`, `src/DinoReader.Core`): the pure dialogue library
  for Dino Reader. `StorySegmenter`, `SpeakerAttributor`, `GenderGuesser`,
  `VoiceCaster` (Decision 75). No package or project references (not Domain, not
  Application); voices come in as plain `VoiceOption(id, gender)`. Every language
  table lives in `LanguageRules` (`LanguageRules.English` today): a new language adds
  a rules instance and never touches the algorithms. Returns data only, no
  user-facing copy. Paragraphs are blank-line separated; a single newline is never a
  sentence or paragraph boundary (wrapped PDF/OCR text).
  (Slice 59: docs/plan/slices/59-dialogue-parser-voice-caster.md)
- Tests: `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` (xUnit v3, plain
  `net10.0`, runs on any OS).
- CI: the `dinoreader` path group runs only the `test-dinoreader` job (ubuntu), never
  the version, release or store jobs. Keep Dino paths out of the `core` group.

#### For README.md (lifted 2026-10-20)

None.

#### Not yet verified

- Attribution quality on a real children's book text (only unit-test fixtures so far); check with the first Slice 60 read.
- Slice 58 still has to add the Dino Reader app build group to `build.yml`.
