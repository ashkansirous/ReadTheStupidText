# Slice 104 — A Norwegian voice pack (Piper nvcc, ten speakers)

*Built 2026-10-24 (PR #291), not yet verified on a device or in a packs.yml run.* Planned 2026-10-24, from the user's request to add Norwegian (with Slice 103).

**Batch:** 21 (five more languages)
**Depends on:** Slice 103 (`nb` exists), Slice 89 (the `piper-fa` pack format and per-platform Piper readers)
**Touches:** `.github/workflows/packs.yml`, `src/ReadTheStupidText.Application/Reading/PackConstants.cs`, `src/ReadTheStupidText.Application/Reading/**` (pack catalog, voice table), `src/ReadTheStupidText.Infrastructure/**` (Windows Piper reader, if it is per-pack), `src/ReadTheStupidText.Mobile.Shared/**` (Piper reader), `src/DinoReader.App/StoryVoicing.cs`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`, `CLAUDE.md`, `src/ReadTheStupidText.Mobile/CLAUDE.md`, `src/ReadTheStupidText.Infrastructure/CLAUDE.md`, `.github/CLAUDE.md`

## Why

Supertonic-3 covers 31 languages but not Norwegian. Piper has `no_NO-nvcc-medium`: ten speakers,
CC0. That gives Dino ten distinct Norwegian character voices, and RTST an offline voice on every platform.

## Decisions (2026-10-24, agreed with the user)

- **Decision 108: a `piper-nb` pack.** It is built and released by `packs.yml` like `piper-fa`. It has one
  model folder (`no_NO-nvcc-medium`, ten speakers, CC0) and the shared `espeak-ng-data` at the pack root.
  Its manifest has `language: "nb"`. The ten speakers are the voices Dino casts from. RTST offers them in
  the voice picker; the default is the clearest speaker, chosen by listening.
- **The GPL exception widens to this pack, with the same scope:** espeak-ng data (GPL-3.0) lives only
  inside the downloaded `piper-fa` and `piper-nb` packs, never in the repo or any app package. The licence
  text ships inside the pack, as for `piper-fa`.
- **Download rules as today:** offered after choosing Norwegian on first run or in the language picker,
  updated only on an unmetered connection. The phone's own Norwegian voice reads until the pack arrives.
- **Out of scope:** `no_NO-talesyntese` (one speaker, adds nothing over nvcc).

## Work

- [x] 1. **Smallest end-to-end: the pack on Android.** The packs.yml job builds and releases `piper-nb`;
  the catalog lists it; choosing Norwegian downloads it and Norwegian text reads with nvcc.
- [x] 2. Speaker names and the default; the voice picker lists the ten.
- [x] 3. Dino casts Norwegian stories across the ten speakers (gender from the speaker metadata where
  known, else balanced).
- [x] 4. Windows and iOS read with the pack (the per-platform Piper readers from Slice 89).
- [x] 5. The GPL-exception wording in the root, Mobile and Infrastructure `CLAUDE.md` and `.github/CLAUDE.md`
  (written at planning; check it still matches).

## Verification

- CI: `packs.yml` publishes `piper-nb` with a manifest that the catalog test accepts; the pack contains
  the GPL text and no app package contains `espeak-ng-data`.
- Emulator (device-test): choose Norsk on first run; the pack downloads; a Norwegian paragraph reads with
  the default speaker; a Norwegian Dino story gives distinct voices to three characters.
- Windows head: the pack downloads and reads.

## Built

*Changed while building:*

- `packs.yml` builds `piper-nb` from rhasspy/piper-voices `no_NO-nvcc-medium`, pinned to revision 5041a7a with sha256s, converted to int8 with sherpa `add_meta_data`. CI checks 10 speakers, a consistent sid map and `nb` in `espeak-ng-data`; the GPL LICENSE ships in the pack; the zip is capped at 100 MB.
- `PackConstants.NorwegianPackId`; `NaturallyReadable` now covers every language; `SupertonicVoiceTable.Serves` keeps Supertonic off `nb`.
- `NorwegianVoices` takes gender from the speaker code's first letter (K/M). Users see "Voice N (woman/man)" in all 14 languages, in the RTST picker and on Dino's cast page. Dino casts `nb` stories from the pack; the narrator falls back to KNN.
- The default voice is KNN (first in the list); nobody has listened to it, so it is not yet the chosen clearest speaker.
- Item 5 (GPL wording in the `CLAUDE.md` files) was not rechecked in this slice.

*From review:* high pass, 6 applied (#1, the `PackConstants` pin bump, is left to the coordinator after the packs run; #7 skipped). Medium pass, 4 applied (#4 and #5 skipped).

#### For CLAUDE.md (lifted 2026-10-24)

- Pack voices are shown as "Voice N (woman/man)" in every language, with gender from the speaker code's first letter via `NorwegianVoices`; `SupertonicVoiceTable.Serves` must keep Supertonic off `nb`. (Slice 104: docs/plan/slices/104-piper-norwegian-pack.md)
- The `piper-nb` pack pins rhasspy/piper-voices revision 5041a7a in `.github/workflows/packs.yml`; bump the `PackConstants` pin after each packs run. (Slice 104: docs/plan/slices/104-piper-norwegian-pack.md)

#### Not yet verified

- The `packs.yml` run: int8 on a multi-speaker model, and the espeak `nb` path.
- The `PackConstants` pin bump after that run.
- Listening to pick the clearest default voice (KNN is a placeholder).
- Emulator: Norsk first run downloads the pack, and a Dino story gets distinct voices.
- Windows head: the pack downloads and reads.

- 2026-10-24 follow-up (fix/packs-nb-espeak): espeak-ng's Norwegian voice `lang/gmq/nb` uses `dictionary no`, so the data file is `no_dict` (there is no `nb_dict`). The packs.yml guard checks `no_dict`; do not "fix" it back.
