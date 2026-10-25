# Slice 118 — Dino: storyteller voices for Spanish, French and Portuguese

*In progress (wave 2026-10-25), draft PR.* *Planned 2026-10-25.* Dino Reader only; RTST is not touched.

**Batch:** 24 (storyteller characters)
**Depends on:** Slice 116 (rosters per language; this slice moves three rosters onto Kokoro)
**Touches:** `src/ReadTheStupidText.Domain/Reading/KokoroVoiceTable.cs`, `src/ReadTheStupidText.Domain/Reading/KokoroFiles.cs`, `src/ReadTheStupidText.Application/Reading/PackManifest.cs`, `src/ReadTheStupidText.Application/Reading/PackConstants.cs`, `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Mobile.Shared/**/KokoroEngineConfig*.cs`, `src/ReadTheStupidText.Mobile.Shared/**/*Reader*.cs`, `.github/workflows/packs.yml`, `src/DinoReader.Core/CharacterRoster*.cs`, `src/DinoReader.App/StoryVoicing.cs`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`, `src/ReadTheStupidText.Infrastructure/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `.github/CLAUDE.md`

## Why

The `kokoro-en` pack is sherpa-onnx's `kokoro-int8-multi-lang-v1_0` with 53 voices, but Dino uses only
the 14 English ones (`KokoroVoiceTable.Serves`, line 38). Kokoro-82M v1.0 also has Spanish, French and
Portuguese voices (and Japanese and Chinese ones). The same pack may already cover them.

## Decisions (2026-10-25, agreed with the user)

- **Decision 124: the storyteller pack also serves Spanish, French and Portuguese, if the pack
  speaks them well.** Work item 1 checks whether the sherpa-onnx multi-lang front end pronounces
  these through the pack's espeak-ng data (the `lang` config field). If it does, those three
  rosters move from Supertonic-3 to Kokoro voices. If not, they stay on Supertonic and the slice
  records why. Japanese and Chinese move too only if the same check passes; otherwise they keep
  Supertonic-3 and `melo-zh-en`.
- The pack id may be renamed from `kokoro-en` to a language-neutral id (e.g. `kokoro`) if it serves
  more than English; installed `kokoro-en` folders keep working.
- Farsi, Arabic, Russian, Danish, Finnish, Swedish, Norwegian and Dutch are not covered by Kokoro and
  keep their current engines under the Slice 116 characters.

#### Design (Dino Reader Batch 24 mock, no frames, added 2026-10-25)

Source: `docs/design/dino-reader/Dino Reader Batch 24.dc.html`. The mock has no frame for this slice; its header says Slice 118 "changes no screens". The only mention is under frame 55: Spanish moves to Kokoro underneath only if this slice's listening check passes, and the narrator screen is the same either way. The mock agrees with the plan. No new strings, no open points.

## Work

- [ ] 1. `library-docs`: sherpa-onnx 1.13.3 Kokoro multi-lang `lang` handling and which languages the
  v1.0 multi-lang archive's front end supports; list the es/fr/pt/ja/zh voice ids in `voices.bin`.
  Generate one sample sentence per language and the user listens.
- [ ] 2. `KokoroVoiceTable` voices and `Serves` for the languages that pass; engine config sets
  `lang` per segment if needed, in all three builders (Infrastructure, Android, iOS).
- [ ] 3. Move those rosters (Slice 116) to Kokoro voices; Slice 115's re-cast covers existing books.
- [ ] 4. `packs.yml` catalog entry lists the extra languages (and the id rename if done); rules in the
  Infrastructure, App and `.github` `CLAUDE.md` files (For CLAUDE.md block).

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` and
  `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: a Spanish, French and Portuguese starter read with Kokoro characters.
- Kill criterion: a language whose Kokoro reading is worse than Supertonic-3 in the listening check
  stays on Supertonic.
