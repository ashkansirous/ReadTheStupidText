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
| `kind` | `voice` or `ocr` |
| `language` | comma-separated language codes the pack serves (`en,fr,es,pt,ru,ja,ar`) |
| `version` | pack version string |
| `engine` | the synthesis/recognition engine (`sherpa-onnx`; `onnxruntime` for `kind: ocr`, Slice 52) |
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
app. Language-to-pack choice is `VoicePackSelector` (a language with no pack, today Persian
and Chinese until `packs-2` is published and pinned, reads with the phone voice and the app
says so). The installed pack serving the current language is resolved by
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
The voice installer does not yet check for newer voice-pack versions at start-up.

## Hosting

Packs are assets of a dedicated GitHub release tagged `packs-<n>`, separate from the app's
`v<x.y.z>` releases (Decision 62). Release assets don't count toward the Actions storage
quota (Decision 52). The app reads the catalog from the **pinned** release named in
`PackConstants.CatalogUrl` (currently `packs-1`), so publishing `packs-2` changes nothing
until a new app version points at it.

`packs-<n>` cannot disturb app versioning: GitVersion has no `tag-prefix`, so only
`[vV]?<semver>` tags count; `release.yml` (via `ship-targets.mjs` and `find-release.sh`) only looks at strict `v<x.y.z>` tags; and the release is
created `--latest=false`, so `releases/latest` stays the app.

## Publishing

Manual: run the **packs** workflow (`.github/workflows/packs.yml`, `workflow_dispatch`) with
the release number and the pack versions (`pack_version` for Supertonic,
`piper_pack_version`, `melo_pack_version`, and `ocr_pack_version` for the four OCR packs,
Slice 52). One run builds the packs into the one
`packs-<n>` release, computes each sha256 and size, and publishes the zips with `packs.json`.
The voice packs:

| pack | languages | engine | licence |
| --- | --- | --- | --- |
| `supertonic-3` | en, fr, es, pt, ru, ja, ar | Supertonic (zips `src/ReadTheStupidText.App/VoiceModel/`, LFS) | Apache-2.0 |
| `piper-fa` | fa | Piper/VITS (`fa_IR-amir-medium`) | espeak-ng data is GPL-3.0, inside this zip only, with its `LICENSE` and `SOURCE` |
| `melo-zh-en` | zh | MeloTTS | MIT |

The espeak-ng data never enters the repo or an app package. After a run, confirm that `v*`
versions and `releases/latest` are unchanged, then flip `PackConstants.CatalogUrl` to the new
release in a one-line PR.

## Client behaviour (Android, `MobileVoicePackService`)

- Files live in `AppDataDirectory/packs/<id>/`.
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
