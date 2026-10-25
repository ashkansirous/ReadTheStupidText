# Slice 114 — Dino: Kokoro storyteller voices for English

*In progress (wave 2026-10-24), draft PR.* *Planned 2026-10-24*, from the user's note that Dino's voices are "too grown up", asking for better
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

#### Design (Dino Reader Batch 22-23 mock, frames 48 and 49, added 2026-10-24)

Source: `docs/design/dino-reader/Dino Reader Batch 22-23.dc.html`, frames 48 (lines 259-283, the offer) and 49 (285-301, installed). Two real-UI frames (360x760 phone frame, 6px `#2b2540` border, radius 34.). Icons in the mock are inline SVG strokes (2.2 wide, `#5B57E8`), not Phosphor names; the app uses its own `IconKind` set. The mock agrees with the plan; Kokoro voice names ("Heart", "Puck", "Santa", "Bella") are drafts, and "90 MB" is a placeholder until the pack is built.

- **Offer card (48, 114-5).** At the top of the cast screen, only for English books without the pack: fill `#ECE7FF`, radius 18. Title "Storyteller voices" 15/600; text "Warmer, livelier English voices for stories. 90 MB download." 12.5 `#5a5470`, line-height 1.4. Two buttons side by side, 44 high, radius 22: **Download** (fill `#5B57E8`, white 14/600, download icon 16) and **Hear one** (white, `#5B57E8` text, play icon 14). "Hear one" plays a sample before the download.
- **Installed (49).** The card is replaced by a green line (13 `#2E7D32`, check icon) "Storyteller voices are on for English books". The narrator row reads "Narrator · Heart". Rows are collapsed (tap to open the full row from 48) with the flavour in the subtitle: "Boy · Puck · Small", "Boy · Santa · Big", "Girl · Bella · Normal". An outlined pill "Use Dino's first voices" (48 high, radius 24, 2px `#c9c0f5`, `#5B57E8` 15/600) removes the pack and goes back to Supertonic.
- **Behaviour (note).** After the download English books re-cast from the Kokoro voices by gender, with a warm storyteller narrator by default and the flavours kept on top. Other languages are unchanged.

Settled by the mock: "Hear one" is a sample before the download; the way back is "Use Dino's first voices" on the cast screen (the plan said only "uninstall").

New strings (all 14 locales): "Storyteller voices", "Warmer, livelier English voices for stories. {0} MB download.", "Download", "Hear one", "Storyteller voices are on for English books", "Use Dino's first voices".

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
- [ ] 5. Dino: pack offer on the cast screen ("Storyteller voices" card with Download and "Hear one", frame 48; the green line and "Use Dino's first voices" once installed, frame 49), voices from Kokoro for English when installed, narrator
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
