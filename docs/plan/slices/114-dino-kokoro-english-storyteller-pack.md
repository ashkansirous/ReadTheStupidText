# Slice 114 — Dino: Kokoro storyteller voices for English

*Planned 2026-10-24*, from the user's note that Dino's voices are "too grown up", asking for better
options "at least for English and alike".

**Batch:** 23
**Depends on:** Slice 113 (flavours apply on top of these voices too)
**Touches:** `.github/workflows/packs.yml`, `src/ReadTheStupidText.Domain/Reading/KokoroFiles.cs` (new), `src/ReadTheStupidText.Domain/Reading/KokoroVoiceTable.cs` (new), `src/ReadTheStupidText.Application/Reading/PackManifest.cs`, `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Mobile.Shared/**`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `tests/ReadTheStupidText.Tests/**`, `STORE.md`, `site/dino/privacy.html` (only if pack hosting changes), `src/ReadTheStupidText.Infrastructure/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `.github/CLAUDE.md`

## Why

Kokoro-82M (Apache-2.0) is the most expressive permissive voice model that runs on a phone, and
sherpa-onnx supports it (https://k2-fsa.github.io/sherpa/onnx/tts/pretrained_models/kokoro.html). Its
English voices include warm storytellers (`af_heart`, `bm_fable`) and a playful one (`am_santa`). It also
covers Spanish, French, Italian and Portuguese; not German, Dutch, the Nordic languages or Farsi. It was
rejected for RTST (Decision 63: v1.1 has no English male voice, v1.0 is larger and ships espeak-ng data).
For Dino, expressiveness matters more than RTST's reasons.

## Decisions (2026-10-24, agreed with the user)

- **Decision 118: Dino gets a downloadable `kokoro-en` voice pack.** English Dino books use Kokoro
  voices when the pack is installed (offered on the cast screen as "Storyteller voices", downloaded like
  any other pack); without it they keep Supertonic. Narrator default: a warm storyteller voice; characters
  are cast from Kokoro's English voices by gender, with Slice 113's flavours on top. RTST is unchanged.
  Spanish/French/Italian/Portuguese Kokoro voices are a follow-up if English proves out.
- **Decision 119: the espeak-ng GPL exception covers `kokoro-en`**, with the same scope as `piper-fa`
  and `piper-nb`: espeak-ng data only inside the downloaded pack, never in the repo, MSIX, AAB or IPA,
  with the licence text and a source link in the pack.
- Size target: the int8 model so the pack stays under the 100 MB CI cap if it fits, otherwise a raised
  cap for this pack only (record the real size). Kill criteria: real-time factor on a mid-range Android
  phone worse than Supertonic's by more than 2x, or the user prefers Supertonic plus flavours in the
  listening check.

## Work

- [ ] 1. `library-docs`: sherpa-onnx 1.13.3 `OfflineTtsKokoroModelConfig` in the C# binding, the int8
  model's files and size, voice ids and multi-language `lang` handling.
- [ ] 2. Licence check, both models: confirm Kokoro's weights are Apache-2.0, and re-check Supertonic-3's
  weights licence (research on 2026-10-24 reported OpenRAIL-M, while the repo records Apache-2.0). Record
  the outcome in `STORE.md` and the Decisions; if Supertonic's weights are OpenRAIL-M, list its use
  restrictions and confirm Dino and RTST comply.
- [ ] 3. `packs.yml`: build and release `kokoro-en` (model, voices, tokens, espeak-ng data, LICENSE and
  source link), size check.
- [ ] 4. `KokoroFiles`/`KokoroVoiceTable`, `PackManifest` and `SherpaEngineFactory` Kokoro config;
  unit tests for the table and manifest.
- [ ] 5. Dino: pack offer on the cast screen, voices from Kokoro for English when installed, narrator
  default.
- [ ] 6. Rules in `src/ReadTheStupidText.Infrastructure/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md` and
  `.github/CLAUDE.md` (the new pack and its GPL scope).

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`; `packs.yml` run builds the
  pack within its size cap.
- Android emulator: install the pack from the cast screen, read Peter Rabbit in English: Kokoro narrator
  and characters, flavours still apply; uninstall: Supertonic again.
- The user listens to one page read with Supertonic plus flavours and with Kokoro, and keeps the pack
  only if Kokoro wins.
