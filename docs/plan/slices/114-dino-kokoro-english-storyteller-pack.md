# Slice 114 — Dino: Kokoro storyteller voices for English

*Built 2026-10-25, not yet verified (packs-6 run and device listening open).* *Planned 2026-10-24*, from the user's note that Dino's voices are "too grown up", asking for better
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

- [x] 1. `library-docs`: sherpa-onnx 1.13.3 `OfflineTtsKokoroModelConfig` in the C# binding, the int8
  model's files and size, voice ids and multi-language `lang` handling.
- [x] 2. Licence check, both models: confirm Kokoro's weights are Apache-2.0, and re-check Supertonic-3's
  weights licence (research on 2026-10-24 reported OpenRAIL-M, while the repo records Apache-2.0). Record
  the outcome in `STORE.md` and the Decisions; if Supertonic's weights are OpenRAIL-M, list its use
  restrictions and confirm Dino and RTST comply.
- [x] 3. `packs.yml`: build and release `kokoro-en` (model, voices, tokens, espeak-ng data, LICENSE and
  source link), size check.
- [x] 4. `KokoroFiles`/`KokoroVoiceTable`, `PackManifest` and `SherpaEngineFactory` Kokoro config;
  unit tests for the table and manifest.
- [x] 5. Dino: pack offer on the cast screen ("Storyteller voices" card with Download and "Hear one", frame 48; the green line and "Use Dino's first voices" once installed, frame 49), voices from Kokoro for English when installed, narrator
  default.
- [x] 6. Rules in `src/ReadTheStupidText.Infrastructure/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md` and
  `.github/CLAUDE.md` (the new pack and its GPL scope): written below as the For CLAUDE.md block; `update-docs` lifts them.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj`; `packs.yml` run builds the
  pack within its size cap.
- Android emulator: install the pack from the cast screen, read Peter Rabbit in English: Kokoro narrator
  and characters, flavours still apply; uninstall: Supertonic again.
- The user listens to one page read with Supertonic plus flavours and with Kokoro, and keeps the pack
  only if Kokoro wins.

## Built notes (2026-10-25, not yet verified)

- **Licences (item 2).** Kokoro-82M: model card says `apache-2.0` (v1.0, "Apache-licensed weights"), so the build went ahead. Supertonic-3: the model
  card and the `LICENSE` file of `Supertone/supertonic-3` say the **weights are BigScience OpenRAIL-M** (the repo's MIT `LICENSE` is the sample code only), so the
  repo's "Apache-2.0" record was wrong. Recorded in `STORE.md` with the use restrictions (Attachment A: no exploiting minors, false information,
  impersonation, medical advice, automated legal decisions; machine-generated content disclosed) and the obligations (licence copy, restrictions passed on
  as an enforceable term, notices kept). Dino and RTST comply with the restrictions; the `supertonic-3` pack zip lacked the OpenRAIL-M text and a notice
  of the restrictions. **Fixed in this slice:** `packs.yml` now ships `LICENSE-OpenRAIL-M` and a `NOTICE` in the `supertonic-3` pack (`pack_version` 3); `STORE.md` updated.
- **Pack.** `kokoro-en` is sherpa-onnx's `kokoro-int8-multi-lang-v1_0` (sha256 `4c3052ab...`, 132 MB archive; **superseded by Slice 220 part 2**: pack version 3 pins the 2026-10-26 re-upload, 132,353,410 bytes, sha256 `72c34723...`, STFT convs kept float32, because the first int8 build gave NaN/silent short lines on arm64): `model.int8.onnx` (114 MB) stored as `model.onnx`, `voices.bin`
  (53 voices, 28 MB), `tokens.txt`, `lexicon-us-en.txt`, the espeak-ng data, the Apache licence. Built locally to measure: **the zip is 125,582,024 bytes (about
  126 MB)**, over the 100 MB cap of the other packs, so `packs.yml` gives this pack its own cap, `KOKORO_MAX_BYTES` 157286400 (150 MB). The card names the size from
  the catalog entry, so the mock's "90 MB" placeholder is the real size once the pack is published (about "126 MB").
- **Catalog kind `storyteller`.** A new `PackKind.Storyteller`, not `voice`: `VoicePackSelector`/`InstalledVoiceCatalog` never pick it for the text reader (RTST
  stays Supertonic for English), and an older Dino build skips the unknown kind in `packs.json`. Dino registers `StorytellerVoices` (a marker) so
  `MobileVoiceModelService` and `StoryVoicing` also scan `IVoicePackService.GetInstalledStorytellerPacks()`; `InstalledVoiceCatalog` puts a storyteller pack
  ahead of the language's voice pack. Voices are not listed in `pack.json`: the fourteen English ones (Heart, Bella, Nicole, Sky, Nova, Emma, Lily, Alice,
  Puck, Santa, Fable, Liam, Michael, Daniel) are `KokoroVoiceTable` with the speaker ids of the v1.0 `voices.bin`; Heart is the narrator. `VoiceEngineKind.Kokoro`
  is configured in the three engine builders (Infrastructure `SherpaEngineFactory`, Android reader, `IosTtsEngine`); config fields are `Model`, `Voices`,
  `Tokens`, `Lexicon`, `DataDir` (checked against the 1.13.3 binding by reflection; `Lang` exists and is left unset).
- **Cast screen.** `StorytellerCard` (+ `StorytellerController`): offer card with Download (percent while downloading) and "Hear one", the green line once installed, and
  "Use Dino's first voices" pinned above the foot. The card appears only when the catalog lists a storyteller pack for the book's reading language, so nothing shows
  until `packs.yml` is run and `PackConstants.CatalogUrl` points at that release. "Hear one" fetches `<id>-<version>-sample.wav` from beside the pack zip (made by
  `packs.yml` with the pack's own files; 4 MB cap in the app) into the cache and plays it with `IRecordingPlayer`. After an install or removal `NarratorRecast` also
  re-casts any cast whose voices are not in the language's pool (`CastEditor.HasVoiceOutside`), so English books re-cast by gender from Kokoro and back to Supertonic on
  removal; flavours and edited genders stay. `MobileVoicePackService.Uninstall` deletes the pack folder (false when a file is locked).
- **Deviations from the mock.** (1) (Superseded by review: frame 49 collapsed rows were built.) Character rows stayed expanded (Slice 113's rows) instead of collapsing to "Boy · Puck · Small": the mock marks the collapse as "for space".
  (2) "Use Dino's first voices" sits above the foot with the merge button, not in the scroll body. (3) One extra string, `Cast_Storyteller_Failed`, for a failed download (the existing
  pack-status strings talk about the phone voice). The 13 translations are machine drafts.
- **Not done / open.** No `packs.yml` run, no release, no pin bump (post-merge: `packs.yml` with `release_number=6 pack_version=3 piper_pack_version=2 norwegian_pack_version=1 melo_pack_version=1 kokoro_pack_version=1`, then `PackConstants.CatalogUrl` to packs-6). The Python sample step and the `sherpa-onnx==1.13.3` pip wheel are untested until the run;
  actionlint is not installed here, so the workflow was only YAML-parsed. The multi-language Kokoro front end may take the lexicon or the espeak path for English: listen on a device.
  The RTST `VoicePickerPage` engine-name switch has no `Kokoro` case (RTST never gets the pack). The windows `WindowsVoicePackService` (RTST) has no storyteller support (defaults).
  `docs/voice-packs.md` does not describe the `storyteller` kind yet.

- **Changed while building.** `kokoro-en` is 126 MB int8, under its own 150 MB cap. Kept Supertonic picks map to a same-gender Kokoro voice (`KokoroVoiceTable.MapFromSupertonic`); the name shown matches for English casts only. Lexicon and espeak data are required. `KokoroEngineConfig` is a shared helper in Mobile.Shared (Infrastructure keeps its own copy); sherpa 1.13.3 Python keywords verified. Frame 49 collapsed rows are now built; `SupportsStorytellerPacks` added.
- **From review.** High 1-10 applied: the download outlives the page, refresh is queued, uninstall is atomic, Edited picks are kept on install/remove, `SupportsStorytellerPacks`. Medium 1-10 applied as above; #4 accepted (stored casts are recast on the next cast visit).
- **Not yet verified.** The `packs.yml` run incl. the Kokoro sample step; device listening (Kokoro English front end, flavours on top); Windows head storyteller; native check of the Slice 114 strings.

### For CLAUDE.md (lifted 2026-10-25)

- `src/ReadTheStupidText.Infrastructure/CLAUDE.md`: Kokoro (`VoiceEngineKind.Kokoro`, Slice 114) is Dino-only English storyteller voices: one model, speaker id picks the voice, config via `ConfigureKokoro` (model, voices, tokens, lexicon, espeak data dir) in `SherpaEngineFactory`, the Android reader and `IosTtsEngine` (keep the three in step). Its pack `kokoro-en` has catalog kind `storyteller`; the text reader never resolves it. (Slice 114: docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md)
- `src/DinoReader.App/CLAUDE.md`: Storyteller voices (Slice 114): Dino alone registers `StorytellerVoices`; English books read with Kokoro while `kokoro-en` is installed, else Supertonic. Pools come from `StoryVoicing` (storyteller packs scanned), a pool change re-casts through `NarratorRecast` (`CastEditor.HasVoiceOutside`), the card is `StorytellerCard`/`StorytellerController` and shows only when the catalog lists the pack. Never add `kokoro` voices to RTST. (Slice 114: docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md)
- `.github/CLAUDE.md`: `kokoro-en` is built by `packs.yml` from the sha256-pinned sherpa-onnx `kokoro-int8-multi-lang-v1_0` archive, catalog kind `storyteller`, with its own 150 MB cap (`KOKORO_MAX_BYTES`; the zip is about 126 MB) and a `<id>-<version>-sample.wav` beside the zip (the "Hear one" clip). It carries GPL espeak-ng data inside the zip only (Decision 119, same scope as `piper-fa`/`piper-nb`). Run `packs.yml`, then bump `PackConstants.CatalogUrl`; until then the card is hidden. (Slice 114: docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md)
- Root `CLAUDE.md` (licence section) and `STORE.md`: Supertonic-3 weights are BigScience OpenRAIL-M, not MIT/Apache-2.0 (the MIT `LICENSE` is sample code only). The `supertonic-3` pack ships `LICENSE-OpenRAIL-M` + `NOTICE` (via `packs.yml`); keep both and pass the use restrictions on. Kokoro-82M is Apache-2.0. (Slice 114: docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md)

*Emulator check 2026-10-25 (Android emulator, Debug arm64, main 7846b1c): PASS. Offer shows 126 MB, Hear one plays, download installs in about 35s, installed state and "Use Dino's first voices" work.*
