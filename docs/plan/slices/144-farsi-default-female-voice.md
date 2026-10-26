# Slice 144 — Farsi: the female voice is the default, the literary voice leaves the cast

*Built 2026-10-26 (wave PR #384), not yet verified by ear or on a device.*

**Batch:** 30 (Decisions 149-153)
**Touches:** `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.Core/CharacterRosterPackVoices.cs`, `src/DinoReader.Core/VoiceFlavour.cs`, `src/DinoReader.App/NarratorRecast.cs`, `src/DinoReader.App/ReadingSession.cs`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/ReadTheStupidText.Infrastructure/Speech/**`, `src/ReadTheStupidText.Application/Reading/**`, `tests/DinoReader.Tests/**`, `tests/ReadTheStupidText.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

The Farsi default is `ganji_adabi` ("adabi" = literary), which sounds too formal for stories. The
`piper-fa` pack has four voices (sid 0 each): `ganji_adabi`, `ganji`, `gyro` (male) and `mana` (female,
already the "night-storyteller" and the mum/fairy/princess voices; `CharacterRosterPackVoices.cs`).

## Decisions (locked with the user, 2026-10-26)

- **Decision 150:** in both apps `mana` is the default Farsi voice (Dino narrator, RTST Farsi reading).
  Roster characters that use `ganji_adabi`, the "storyteller" included, move to `ganji` or `gyro`
  (pick by ear against the character). `ganji_adabi` stays selectable. A narrator or character a
  grown-up picked themselves is kept (`NarratorChoice`, `CastEditor.StaysPicked`).
- Change the default in code (`StoryVoicing.NarratorFor`, the roster, RTST's Farsi default), not by
  reordering `packs.yml` (that needs a new packs release).

#### Design (Dino Reader Batch 30 mock, added 2026-10-26)

No screens. The Batch 30 mock intro says 144 changes no screens (it makes **mana** the default Farsi voice). Nothing to build from the mock.

## Work

- [x] `NarratorFor("fa")` returns `mana` (`StoryVoicing`); RTST's Farsi default likewise: `VoiceModelPaths.DefaultVoice` prefers `VoiceChoices.PersianDefaultVoiceId` for a Piper model, which both apps use (no Mobile edit needed). Tests in `VoiceChoicesTests`, `VoiceSelectionTests`.
- [x] Roster (no ganji_adabi left; see below; `night-storyteller` is now the first narrator): replace `FaGanjiAdabi` uses with `ganji`/`gyro`.
- [x] Recast once (`ReadingSession.RecastClashesAsync` before each voiced read; idempotent, saves only when something changed; tests in `FarsiDefaultVoiceTests`): books whose cast was the default (not picked) and used `ganji_adabi` move to the new
      voices on the next open, through `NarratorRecast`'s clash rules (a character on `mana` that now
      clashes with the narrator moves). Unit-test it.

## Verification

- Unit tests (default, roster, recast keeps picks).
- Emulator: fresh install in Farsi reads the pigs with `mana` narrating and the wolf on a plain male
  voice; an install with a picked narrator keeps it. Listening by the user.

## Voice choices for a later listen (applied)

- storyteller: ganji_adabi -> gyro (the one male narrator left besides deep-storyteller on ganji; keeps three narrators on three voices).
- New flavours (Core `VoiceFlavour`, Farsi roster only): Chirpy (+2.0 st, pace 1.22), Perky (+2.5, 1.08), Hushed (+1.5, 0.90, soft gap), Gentle (+1.0, 0.97) and Mellow (-1.0, 1.05). Chirpy, Perky count as young for the animal, family and reuse logic; Hushed, Gentle do not; Mellow is grown-sounding like Sleepy (family 3, not young, avoided for reused young animals). Picks by name and hint: piglet gyro+Chirpy, lamb ganji+Chirpy, kitten ganji+Perky, piglet3 (calm, simple) gyro+Gentle, duckling2 (calm, whispery) gyro+Hushed, kitten2 (soft, sleepy) gyro+Mellow; every voice+flavour pair stays unique. To listen for: are these distinct enough?
- A grown-up's explicit `ganji_adabi` narrator pick stays `ganji_adabi` (`StoryVoicing.OnNarratorRoster` exempts it, Farsi only). A cast only clashes with the narrator when a character has its voice with a flavour the roster does not play on that voice (`CastEditor.ClashesWithNarrator`), so a Farsi cast with the mum on mana is left alone. Books whose unpicked characters sit on `ganji_adabi` are recast once per session (`CastEditor.UsesRetiredVoice`, Farsi only), and a book is checked once per session once its language has voices (`ReadingSession.RecastClashesAsync`).

*Changed while building:* the default is `VoiceChoices.PersianDefaultVoiceId` (`mana`, Farsi only), used by the Dino narrator and RTST's `VoiceModelPaths.DefaultVoice`. `ganji_adabi` is retired from the Farsi roster but stays selectable, and explicit picks are kept. Five new Core flavours (Chirpy, Mellow, Perky, Hushed, Gentle) are Assignable, not Pickable. `ReadingSession.RecastClashesAsync` recasts once per book per session; the clash rule is `CastEditor.ClashesWithNarrator`.

*From review:* 144-high: 9 findings applied, 6 skipped. 144-medium: 8 applied, findings 3 and 7 skipped by design.

#### For CLAUDE.md (lifted 2026-10-26)

Target `src/DinoReader.App/CLAUDE.md`:

- The Farsi default voice is `mana` (`VoiceChoices.PersianDefaultVoiceId`, used by `StoryVoicing.NarratorFor("fa")` and RTST). `ganji_adabi` is retired from the Farsi roster but stays selectable; a grown-up's explicit pick is kept. (Slice 144: docs/plan/slices/144-farsi-default-female-voice.md)
- The flavours Chirpy, Mellow, Perky, Hushed and Gentle are Farsi-roster-only (Assignable, not Pickable); keep every voice+flavour pair unique. (Slice 144: docs/plan/slices/144-farsi-default-female-voice.md)

#### Not yet verified

- Listen to every moved Farsi character and the five new flavours (Chirpy, Mellow, Perky, Hushed, Gentle): are they distinct enough?
- Farsi read on emulator/phone with `mana` narrating.
- RTST on phone and Windows: Farsi default is `mana`.
