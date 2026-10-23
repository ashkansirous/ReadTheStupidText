# Slice 89 — Four Farsi voices in one pack (ganji_adabi by default), Amir dropped

*Built 2026-10-23. Android emulator acceptance passes; Windows desktop, iOS and Pixel timing not yet verified (see the end of this file).*

**Batch:** 15
**Decisions:** 93
**Depends on:** — (Slices 81-82 are built: a language change downloads the pack, and the voice picker lists pack voices)
**Touches:** `.github/workflows/packs.yml`, `src/ReadTheStupidText.Application/Reading/PackConstants.cs`, `src/ReadTheStupidText.Application/Reading/PackVoice.cs`, `src/ReadTheStupidText.Application/Reading/PackManifest.cs`, `src/ReadTheStupidText.Domain/Reading/VitsFiles.cs`, `src/ReadTheStupidText.Domain/Reading/VoiceInfo.cs`, `src/ReadTheStupidText.Infrastructure/Reading/PackVoiceCatalog.cs`, `src/ReadTheStupidText.Infrastructure/Reading/PackVoiceModelService.cs`, `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosTtsEngine.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoiceModelService.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `src/ReadTheStupidText.Mobile.Shared/VoicePackInstaller.cs`, `src/DinoReader.App/CastText.cs`, `STORE.md`, `tests/ReadTheStupidText.Tests/PackVoiceModelServiceTests.cs`, `tests/ReadTheStupidText.Tests/VoiceChoicesTests.cs`, `tests/ReadTheStupidText.Tests/VoiceSelectionTests.cs`, `tests/ReadTheStupidText.Tests/VoicePackSetupTests.cs`, `tests/ReadTheStupidText.Tests/PackVersionsTests.cs`, `src/ReadTheStupidText.Application/Reading/PackJsonKeys.cs`, `src/ReadTheStupidText.Application/Reading/InstalledVoiceCatalog.cs`, `src/ReadTheStupidText.Application/Reading/IVoiceModelService.cs`, `src/ReadTheStupidText.Application/Reading/VoicePackSetup.cs`, `src/ReadTheStupidText.Infrastructure/Reading/SupertonicSpeechReader.cs`, `src/ReadTheStupidText.Infrastructure/Reading/BuiltEngine.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/iOS/IosSupertonicSpeechReader.Synthesis.cs`
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

- [x] **Pack format: one model per voice.** `PackVoice` gains an optional `Model` (a folder
      relative to the pack, holding `model.onnx` + `tokens.txt`); a voice without it keeps the
      pack-root files (Supertonic, Melo, old packs). `PackManifest` parses it. `VoiceInfo` and
      `VoiceModelPaths` carry the resolved model folder. The shared `espeak-ng-data` stays one
      copy at the pack root.
- [x] **Readers load the chosen voice's model.** `SherpaEngineFactory` (Windows), the Android
      reader and `IosTtsEngine` build the VITS config from the voice's folder. A switch between
      voices with different models rebuilds the engine at the next chunk (Slice 23's
      mid-read swap rule); a switch within one model just changes the `sid`. The head-start
      sentence cache already keys by voice id.
- [x] **`packs.yml`:** fetch the sherpa-onnx `vits-piper-fa_IR-{ganji_adabi,ganji,gyro}-medium-int8`
      archives (sha256-pinned). For Mana, which has no sherpa build, download
      `fa_IR-mana-medium.onnx` + `.onnx.json` from `MahtaFetrat/Mana-Persian-Piper` (pinned
      commit), add the sherpa Piper metadata and int8-quantize it the way sherpa's piper
      export script does, and generate its `tokens.txt` from the phoneme map. Layout:
      `voices/<name>/model.onnx`, `voices/<name>/tokens.txt`, root `espeak-ng-data`. The manifest
      voices are `piper:fa-ganji-adabi` (first, the default), `piper:fa-ganji`, `piper:fa-mana`
      and `piper:fa-gyro`. `PERSIAN_VERSION` becomes `2`. `LICENSE` and `SOURCE` cover all four,
      plus amir's Apache-2.0 attribution and the espeak-ng GPL text as today.
- [x] **Existing installs update.** `VoicePackInstaller` compares an installed pack's version with
      the catalog's (the open Slice 52 follow-up) and downloads the newer one, so phones with the
      Amir pack get the four voices. Unit test with a fake catalog at version 2 over an
      installed version 1.
- [x] **Saved choice:** `NeuralVoiceMemory`/`VoiceChoices` resolve an unknown id (`piper:fa-amir`)
      to the pack's first voice; test updated.
- [x] Run `packs.yml` into a new `packs-<n>` release, then flip `PackConstants.CatalogUrl` in
      the same PR (the release must exist before the merge).
- [x] `STORE.md` third-party table: replace the amir row with the four voices and their sources.
- [x] Update the existing tests that use `piper:fa-amir` (`PackVoiceModelServiceTests`,
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

## Changed while building

- **Pack folders are versioned.** A pack is no longer replaced in place: each version installs into
  its own `v<version>/` folder inside the pack's folder, and a small `active` file naming it is swapped
  atomically (`PackVersionFolders`). A live engine keeps its old files; old folders and the loose files of
  the unversioned layout are deleted at the next app start. This replaced the in-place update first built
  (review 3 found no safe engine release or retry on the Dino Windows head).
- **`EngineCache` with capacity 4** (keyed by model folder and pack version, least recently used out first)
  replaced the single shared engine and `ExtraEngineSet`, because a cast read alternates between the four
  model folders of one pack and a pack update is a new folder.
- **Updates only on an unmetered connection.** Phones update an installed pack only on Wi-Fi or Ethernet
  (`PackUpdatePolicy`, `MauiPackUpdateNetwork`); otherwise the pack stays Ready and the check repeats next launch.
- **ANR fix:** engine acquisition moved off the UI thread (first selection of a not-yet-loaded voice froze it).
- **Voice preview plays only when idle** (`VoicePreviewPolicy`); it used to cancel the running read.
- `packs-4` was published from the branch: `piper-fa-2.zip`, 61 MB, with the `ganji_adabi`, `ganji`, `mana`
  and `gyro` int8 models of about 18 MB each. `PackConstants.CatalogUrl` points at `packs-4`.

## From review

Four passes (`reviews/89-review-high.md`, `-medium.md`, `-3.md`, `-4.md`). The first two (10 findings each)
and the third (engine-cache concurrency, update serialisation, memoised load without cancellation, double
enumeration of connection profiles) drove the versioned folders and `EngineCache` rework above. The fourth
(medium, own read of the rework) covered the update gate, the same-version install replacing a live folder,
`active` pointer flush, cache eviction of a mid-load slot, and a metered hotspot passing the Wi-Fi check.
Finding 7 of the medium pass (`packs-4` published before merge) was accepted.

## Verified

Android emulator (`reviews/89-android.md`, fresh AVD, arm64 embedded Debug): all items pass. Fresh install
downloads `piper-fa-2.zip`, extracts `v2/`, sets `active`; the picker lists Ganji Adabi (default), Ganji, Mana
and Gyro; each voice reads from its own `voices/<id>/` folder; an Amir v1 install upgrades to v2 and loses the
old loose files; the first selection of a cold voice no longer causes an ANR; a mid-read Gyro to Mana switch
resumes and finishes; idle selection speaks its name.

## Follow-up

- The play button shows idle after Back while chunks are still playing (seen on the emulator).

#### For CLAUDE.md

- **Pack layout.** An installed pack lives in `packs/<id>/v<version>/` with an `active` file naming the
  folder; always resolve it through `PackVersionFolders.Resolve` (`src/ReadTheStupidText.Application/Reading/PackVersionFolders.cs`),
  never `packs/<id>/` directly, and never replace or delete the active folder (a live engine holds its files).
  A multi-model voice pack puts each model in `voices/<name>/` (`model.onnx` + `tokens.txt`), with one shared
  `espeak-ng-data` at the pack root. Phones update an installed pack only on an unmetered connection
  (`PackUpdatePolicy`). (Slice 89: docs/plan/slices/89-four-farsi-voices.md)
- **Engines are cached per model folder.** Readers get engines through `EngineCache`
  (`src/ReadTheStupidText.Application/Reading/EngineCache.cs`, capacity 4, keyed by model folder and pack
  version, reference counted). Acquire on the thread pool, never on the UI thread (it caused an ANR); the voice
  preview plays only when the reader is idle. (Slice 89: docs/plan/slices/89-four-farsi-voices.md)

#### For README.md

The Farsi voice pack now offers four voices (Ganji Adabi by default, Ganji, Mana, Gyro) in one download of
about 61 MB; Amir is removed, and a phone with the old pack updates itself on Wi-Fi.

#### Not yet verified

- Windows desktop acceptance of the four voices: blocked by a stale dev install, waits on the user.
- iOS on a device.
- Pixel timing of cold voice preparation (7-8 s on the emulator; a 16 s gap on a cold mid-read swap).
- A saved Amir choice falling back to Ganji Adabi, on a device.
