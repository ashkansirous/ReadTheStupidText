# Slice 89 — Four Farsi voices in one pack (ganji_adabi by default), Amir dropped

*Planned.*

**Batch:** 15
**Decisions:** 93
**Depends on:** — (Slices 81-82 are built: a language change downloads the pack, and the voice picker lists pack voices)
**Touches:** `.github/workflows/packs.yml`, `src/ReadTheStupidText.Application/Reading/PackConstants.cs`, `src/ReadTheStupidText.Application/Reading/PackVoice.cs`, `src/ReadTheStupidText.Application/Reading/PackManifest.cs`, `src/ReadTheStupidText.Domain/Reading/VitsFiles.cs`, `src/ReadTheStupidText.Domain/Reading/VoiceInfo.cs`, `src/ReadTheStupidText.Infrastructure/Reading/PackVoiceCatalog.cs`, `src/ReadTheStupidText.Infrastructure/Reading/PackVoiceModelService.cs`, `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosTtsEngine.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoiceModelService.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/DinoReader.App/CastText.cs`, `STORE.md`, `tests/ReadTheStupidText.Tests/PackVoiceModelServiceTests.cs`, `tests/ReadTheStupidText.Tests/VoiceChoicesTests.cs`, `tests/ReadTheStupidText.Tests/VoiceSelectionTests.cs`, `tests/ReadTheStupidText.Tests/VoicePackSetupTests.cs`, `tests/ReadTheStupidText.Tests/PackVersionsTests.cs`
**Story issue:** —

## Goal

The Farsi pack offers four natural voices: **Ganji Adabi** (the default), **Ganji**, **Mana**
and **Gyro**. All four arrive in one download (about 80 MB), and the voice picker lists them
under the Piper heading. Amir is gone. A phone that already has the Amir pack moves to the new
pack by itself.

## Why

The user, 2026-10-22, on Amir: "Amir seems to be afghan, not farsi and its crap!" Sample WAVs of
every candidate were generated with Piper and pushed to the user's Pixel, then transcribed with
Whisper `medium` (letters wrong, lower is better): ganji_adabi 10%, ganji 14%, reza_ibrahim 21%,
mana 22%, gyro 23%, amir 27%. The user chose ganji_adabi as the voice, then asked for ganji, mana
and gyro alongside it, downloaded together.

Licences (checked 2026-10-22):

| Voice | Model | Training data | Source for the claim |
| --- | --- | --- | --- |
| ganji_adabi | MIT (piper-voices) | CC0 | Piper `MODEL_CARD`; fine-tuned from ganji, from amir (dataset `SadeghK/datacula-pertts-amir`, Apache-2.0) |
| ganji | MIT | CC0 | Piper `MODEL_CARD`; fine-tuned from amir |
| mana | MIT (`MahtaFetrat/Mana-Persian-Piper`) | CC0 (`MahtaFetrat/Mana-TTS`, released by the Nasl-e-Mana magazine) | the dataset card itself |
| gyro | MIT (piper-voices / `gyroing/Persian-Piper-Model-gyro`) | CC0 (Kaggle `magnoliasis/persian-tts-dataset-male`) | the Kaggle dataset page |

Datacula's own site (`tts.datacula.com`) did not respond, so ganji's CC0 rests on the Piper model
card. The user accepted that. Because ganji descends from amir, the pack keeps amir's Apache-2.0
attribution. Rejected: reza_ibrahim (its dataset page is gone, and the corpus is Quran
recitation), Gooya v2 (needs its own Negara G2P front end, which sherpa-onnx can't run), MMS-TTS
(CC-BY-NC).

## Decisions locked with the user (2026-10-22)

- One Farsi pack, four voices, downloaded together; ganji_adabi is the default.
- Amir is removed. A saved `piper:fa-amir` choice resolves to the default.
- No per-voice download and no "(needs downloading)" label: everything comes in the one pack.

## Steps

- [ ] **Pack format: one model per voice.** `PackVoice` gains an optional `Model` (a folder
      relative to the pack, holding `model.onnx` + `tokens.txt`); a voice without it keeps the
      pack-root files (Supertonic, Melo, old packs). `PackManifest` parses it. `VoiceInfo` and
      `VoiceModelPaths` carry the resolved model folder. The shared `espeak-ng-data` stays one
      copy at the pack root.
- [ ] **Readers load the chosen voice's model.** `SherpaEngineFactory` (Windows), the Android
      reader and `IosTtsEngine` build the VITS config from the voice's folder. A switch between
      voices with different models rebuilds the engine at the next chunk (Slice 23's
      mid-read swap rule); a switch within one model just changes the `sid`. The head-start
      sentence cache already keys by voice id.
- [ ] **`packs.yml`:** fetch the sherpa-onnx `vits-piper-fa_IR-{ganji_adabi,ganji,gyro}-medium-int8`
      archives (sha256-pinned). For Mana, which has no sherpa build, download
      `fa_IR-mana-medium.onnx` + `.onnx.json` from `MahtaFetrat/Mana-Persian-Piper` (pinned
      commit), add the sherpa Piper metadata and int8-quantize it the way sherpa's piper
      export script does, and generate its `tokens.txt` from the phoneme map. Layout:
      `voices/<name>/model.onnx`, `voices/<name>/tokens.txt`, root `espeak-ng-data`. The manifest
      voices are `piper:fa-ganji-adabi` (first, the default), `piper:fa-ganji`, `piper:fa-mana`
      and `piper:fa-gyro`. `PERSIAN_VERSION` becomes `2`. `LICENSE` and `SOURCE` cover all four,
      plus amir's Apache-2.0 attribution and the espeak-ng GPL text as today.
- [ ] **Existing installs update.** `VoicePackInstaller` compares an installed pack's version with
      the catalog's (the open Slice 52 follow-up) and downloads the newer one, so phones with the
      Amir pack get the four voices. Unit test with a fake catalog at version 2 over an
      installed version 1.
- [ ] **Saved choice:** `NeuralVoiceMemory`/`VoiceChoices` resolve an unknown id (`piper:fa-amir`)
      to the pack's first voice; test updated.
- [ ] Run `packs.yml` into a new `packs-<n>` release, then flip `PackConstants.CatalogUrl` in
      the same PR (the release must exist before the merge).
- [ ] `STORE.md` third-party table: replace the amir row with the four voices and their sources.
- [ ] Update the existing tests that use `piper:fa-amir` (`PackVoiceModelServiceTests`,
      `VoiceChoicesTests`, `VoiceSelectionTests`, `VoicePackSetupTests`).

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` and
  `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`
- `packs.yml` run: the `piper-fa-2.zip` asset holds four `voices/*/model.onnx`, one
  `espeak-ng-data`, and `LICENSE`/`SOURCE` naming all four; the zip is under 100 MB.
- On the user's Pixel over wireless debugging, app in فارسی and the old Amir pack installed:
  launching downloads `piper-fa` v2 by itself; the voice picker lists Ganji Adabi (selected),
  Ganji, Mana and Gyro; tapping each speaks its name in that voice; a Farsi text reads with each
  of them. Switching voice mid-read changes at the next sentence.
- Windows: the same four voices in the tray panel's voice list after the pack downloads.
