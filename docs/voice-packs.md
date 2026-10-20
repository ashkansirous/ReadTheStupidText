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
| `engine` | the synthesis/recognition engine (`sherpa-onnx`) |
| `files` | every payload file the app must find after extraction (LICENSE excluded) |

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
and Chinese until Slice 51, reads with the phone voice and the app says so).

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
the release number and the pack version. It zips `src/ReadTheStupidText.App/VoiceModel/`
(LFS) into the layout above, computes the sha256 and size, and creates the `packs-<n>`
release with the zip and `packs.json`. After the first run, confirm that `v*` versions and
`releases/latest` are unchanged.

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
