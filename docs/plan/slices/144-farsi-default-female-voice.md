# Slice 144 — Farsi: the female voice is the default, the literary voice leaves the cast

*Planned.*

**Batch:** 30 (Decisions 149-153)
**Touches:** `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CharacterRoster*.cs`, `src/DinoReader.App/NarratorRecast.cs`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/ReadTheStupidText.Infrastructure/Speech/**`, `src/ReadTheStupidText.Application/Reading/**`, `tests/DinoReader.Tests/**`, `tests/ReadTheStupidText.Tests/**`, `src/DinoReader.App/CLAUDE.md`

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

## Work

- [ ] `NarratorFor("fa")` returns `mana`; RTST's Farsi default likewise (find where RTST picks it).
- [ ] Roster: replace `FaGanjiAdabi` uses with `ganji`/`gyro`.
- [ ] Recast once: books whose cast was the default (not picked) and used `ganji_adabi` move to the new
      voices on the next open, through `NarratorRecast`'s clash rules (a character on `mana` that now
      clashes with the narrator moves). Unit-test it.

## Verification

- Unit tests (default, roster, recast keeps picks).
- Emulator: fresh install in Farsi reads the pigs with `mana` narrating and the wolf on a plain male
  voice; an install with a picked narrator keeps it. Listening by the user.
