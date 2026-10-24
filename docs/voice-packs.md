# Voice packs

Language voices are not bundled in the app (Decision 61). The first launch asks for a
language (Decision 68) and downloads the matching pack; until it arrives the phone's own
voice reads. This page is the pack format, the catalog, the hosting and the publish steps
(Slice 50, Decisions 60-62).

Text, images, scans and translations never leave the device. The only network use is
downloading these packs.

## Pack format

A pack is a `.zip` with **one top-level folder** named after the pack id:

```
supertonic-3/
  pack.json        manifest (below)
  LICENSE          the model's licence text
  duration_predictor.int8.onnx ... voice.bin   the payload (the Supertonic file set,
                                               `Domain/Reading/SupertonicFiles.cs`)
```

`pack.json`:

| key | meaning |
| --- | --- |
| `id` | the pack id, equal to the folder name and the catalog id |
| `kind` | `voice`, `ocr` or `translation` |
| `language` | comma-separated language codes the pack serves (`en,fr,es,pt,ru,ja,ar`) |
| `version` | pack version string |
| `engine` | the synthesis/recognition engine (`sherpa-onnx`; `onnxruntime` for `kind: ocr`, Slice 52, and `kind: translation`, Slice 75) |
| `files` | every payload file the app must find after extraction (`pack.json` excluded) |
| `model` | optional (Slice 51): the engine family, Piper/VITS or MeloTTS. Absent means Supertonic |
| `voices` | optional (Slice 51): the voices the pack offers. Supertonic lists none (its ten styles are `SupertonicVoiceTable`); Piper and Melo list theirs |

The app installs a pack only after the whole file list is present, so a killed or partial
download can never be mistaken for a working pack.

## Catalog: `packs.json`

An array of:

```json
{ "id": "supertonic-3", "language": "en,fr,es,pt,ru,ja,ar", "kind": "voice", "version": "1",
  "sizeBytes": 145000000, "sha256": "<hex>", "license": "Apache-2.0", "url": "https://github.com/.../packs-1/supertonic-3-1.zip" }
```

Entries with an unknown `kind` are ignored, so a newer catalog still works with an older
app. Language-to-pack choice is `VoicePackSelector` (a language with no pack reads with the
phone voice and the app says so). The installed pack serving the current language is resolved by
`InstalledVoiceCatalog` (Slice 51). Choice among packs of one kind uses the shared
`PackSelection`, parameterised by kind, with an id tie-break (Slice 52).

### OCR packs (`kind: ocr`, Slice 52)

PaddleOCR models for scanned text, one standalone pack per script, each carrying the shared
detector plus its recognizer and dictionary. Source: `monkt/paddleocr-onnx` (Apache-2.0),
sha256-pinned in `packs.yml`.

| pack | languages | recognizer |
| --- | --- | --- |
| `ocr-latin` | en, fr, es, pt | PP-OCRv5 Latin |
| `ocr-cyrillic` | ru | PP-OCRv5 eslav |
| `ocr-arabic` | ar, fa | PP-OCRv3 Arabic |
| `ocr-cjk` | zh, ja | PP-OCRv5 Chinese server (84 MB) |

When no OCR pack serves the language, or a pack is broken or returns nothing, the app falls
back to ML Kit Latin. Installed by `OcrPackInstaller`.

**Version upgrades.** All four OCR packs share one `ocr_pack_version` workflow input. At every
app start and on a language change, `OcrPackInstaller` (via `PackVersions`) compares the
installed pack with the catalogue: a newer catalogue version is downloaded, and the old pack
is kept until the new one verifies, so a failed upgrade never leaves the language without OCR.
Voice packs also update at start-up (Slice 89): see Client behaviour below.

### Translation packs (`kind: translation`, Slice 75)

Opus-MT (Marian) models for offline translate-before-read on Windows, one standalone pack per
direction with English at one end, so a pair such as French to Russian pivots through two packs
(`translation-fr-en` then `translation-en-ru`). The catalogue `language` is `<from>-<to>`.
Source: the pre-converted int8 ONNX in the `Xenova/opus-mt-*` Hugging Face repos, pinned to one
revision and sha256-checked in `packs.yml`; licence CC-BY-4.0 (Helsinki-NLP / OPUS-MT, with the
notice in each pack's `LICENSE`). Each pack holds `encoder.onnx`, the merged `decoder.onnx` (key/value
cache), `source.spm`, `vocab.json` and `translation.json` (the decoding ids and, for a multi-target
model, the leading `>>xx<<` token). Installed ids are `translation-<from>-<to>`.

| pack | model | size (zip) |
| --- | --- | --- |
| `translation-fr-en`, `translation-en-fr` | `opus-mt-fr-en`, `opus-mt-en-fr` | about 107 MB |
| `translation-es-en`, `translation-en-es` | `opus-mt-es-en`, `opus-mt-en-es` | about 113 MB |
| `translation-zh-en`, `translation-en-zh` | `opus-mt-zh-en`, `opus-mt-en-zh` (`>>cmn_Hans<<`) | about 113 MB |
| `translation-ar-en`, `translation-en-ar` | `opus-mt-ar-en`, `opus-mt-en-ar` (`>>ara<<`) | about 110 MB |
| `translation-ru-en`, `translation-en-ru` | `opus-mt-ru-en`, `opus-mt-en-ru` | about 110 MB |
| `translation-pt-en`, `translation-en-pt` | `opus-mt-ROMANCE-en`, `opus-mt-en-ROMANCE` (`>>pt<<`) | about 113 MB |
| `translation-ja-en` | `opus-mt-ja-en` | about 108 MB |
| `translation-da-en`, `translation-en-da` | `opus-mt-da-en`, `opus-mt-en-da` | about 110 MB |
| `translation-fi-en`, `translation-en-fi` | `opus-mt-fi-en`, `opus-mt-en-fi` | about 110 MB |
| `translation-sv-en`, `translation-en-sv` | `opus-mt-sv-en`, `opus-mt-en-sv` | about 110 MB |
| `translation-nl-en`, `translation-en-nl` | `opus-mt-nl-en`, `opus-mt-en-nl` | about 110 MB |
| `translation-nb-en`, `translation-en-nb` | TigreGotico `opus-mt-gmq-en-onnx`, `opus-mt-en-gmq-onnx` (`>>nob<<`; model card says apache-2.0) | about 390 MB |

The `nb` packs (Slice 106) are about four times the others: the "int8" merged decoder in the TigreGotico
repos holds fp32 weights (~337 MB), and no smaller Norwegian export exists. Sizes in the other rows are
approximate (the Xenova int8 pairs); the table's repo layout column in `packs.yml` selects the source paths.

No pack exists for Persian (either way) or English to Japanese: no usable Opus-MT model (Slice 75
research). Installed by `TranslationPackInstaller` through the pack service; the translate toggle
fetches the user's `en` to language pack, and a read whose source needs another leg downloads it in
the background while the original is read.

## Hosting

Packs are assets of a dedicated GitHub release tagged `packs-<n>`, separate from the app's
`v<x.y.z>` releases (Decision 62). Release assets don't count toward the Actions storage
quota (Decision 52). The app reads the catalog from the **pinned** release named in
`PackConstants.CatalogUrl` (currently `packs-4`), so publishing `packs-4` changes nothing
until a new app version points at it. Every `packs-<n>` release carries every pack (voice, OCR and
translation), because the app pins one catalogue: a run with a pack missing would leave the app
pointing at a catalogue without it.

`packs-<n>` cannot disturb app versioning: GitVersion has no `tag-prefix`, so only
`[vV]?<semver>` tags count; `release.yml` (via `ship-targets.mjs` and `find-release.sh`) only looks at strict `v<x.y.z>` tags; and the release is
created `--latest=false`, so `releases/latest` stays the app.

## Publishing

Manual: run the **packs** workflow (`.github/workflows/packs.yml`, `workflow_dispatch`) with
the release number and the pack versions (`pack_version` for Supertonic,
`piper_pack_version`, `norwegian_pack_version`, `melo_pack_version`, and `ocr_pack_version` for the four OCR packs,
Slice 52, and `translation_pack_version` for the twenty-three translation packs, Slice 75). One run builds the packs into the one
`packs-<n>` release, computes each sha256 and size, and publishes the zips with `packs.json`.
The voice packs:

| pack | languages | engine | licence |
| --- | --- | --- | --- |
| `supertonic-3` | en, fr, es, pt, ru, ja, ar | Supertonic (zips `src/ReadTheStupidText.App/VoiceModel/`, LFS) | Apache-2.0 |
| `piper-fa` | fa | Piper/VITS, four voices (below) | espeak-ng data is GPL-3.0, inside this zip only, with its `LICENSE` and `SOURCE` (also the amir Apache-2.0 attribution, Slice 89) |
| `piper-nb` | nb | Piper/VITS, one model with ten speakers (`piper:nb-knn` default, codes from the upstream `speaker_id_map`; Slice 104) | espeak-ng data is GPL-3.0, inside this zip only, with its `LICENSE` and `SOURCE`; voice data CC0 |
| `melo-zh-en` | zh | MeloTTS | MIT |

`piper-fa` (Slice 89, version 2) holds four voices, `piper:fa-ganji-adabi` (the default, listed first),
`piper:fa-ganji`, `piper:fa-mana` and `piper:fa-gyro`, one int8 model each (about 18 MB, zip about 61 MB). Amir is
gone; a saved `piper:fa-amir` choice resolves to the default. Layout:

```
piper-fa/
  pack.json   LICENSE   SOURCE
  espeak-ng-data/              one shared copy
  voices/<name>/model.onnx     one folder per voice, with tokens.txt
```

A `voices` entry in `pack.json` names its model folder (`model`); a voice without one uses the pack-root files.

The espeak-ng data never enters the repo or an app package. After a run, confirm that `v*`
versions and `releases/latest` are unchanged, then flip `PackConstants.CatalogUrl` to the new
release in a one-line PR.

## Client behaviour (Android, `MobileVoicePackService`)

- Files live in `AppDataDirectory/packs/<id>/v<version>/`, and `packs/<id>/active` names the folder in use
  (`PackVersionFolders`). A new version installs into its own folder and the `active` file is replaced
  atomically, so a reader holding the old model keeps working; old version folders and loose files from the
  first (unversioned) layout are deleted at the next app start. A pack with no `active` file is the old layout.
- An installed pack is updated when the catalogue has a newer version, only on an unmetered connection
  (Wi-Fi or Ethernet, `PackUpdatePolicy`); otherwise it stays Ready and the check repeats next launch.
- Readers keep up to four engines (`EngineCache`, one per model folder and pack version).
- Download streams into `<id>.zip.partial` and resumes with an HTTP `Range` request
  (`PackRetryPolicy.ResumeOffset`); a server that ignores Range restarts the file.
- Failures retry with exponential backoff (2 s, 4 s, 8 s, capped at 30 s, four attempts).
  Offline stops at once and the UI offers Retry.
- The sha256 is verified before extraction. A mismatch deletes the file and re-downloads
  once, then reports `HashMismatch`.
- Extraction goes to a scratch folder, is validated against `pack.json`, then moved into
  place atomically.
- Results are the typed `PackDownloadResult` (`Ok`, `Offline`, `HashMismatch`, `Failed`);
  the UI composes the message.
