# CLAUDE.md history

The batch narratives that used to sit in `CLAUDE.md` as blockquotes, moved verbatim on 2026-10-20 (Slice 71). The rule each one implies stays in `CLAUDE.md`, with a pointer here or to the slice. The Batch 8 text is the version before Slice 44 changed its `Mobile` -> Android line.

## Batch 6: multi-platform direction (planning text)

> **Multi-platform direction (Batch 6, planning):** the app is expanding to
> **Android first** (via **.NET MAUI**, a Google Play developer account is
> already in hand), with **iOS/macOS to follow later on the same MAUI stack**.
> `Domain` and `Application` are already framework-agnostic and are reused
> **unchanged** by the mobile project — only new platform implementations are
> written (Android has none of Windows' auto-read triggers; text entry, file
> upload, and a new **camera → on-device OCR → read** path are the mobile
> input surfaces instead). See Decisions 37-42 (docs/plan/decisions.md) and the Batch 6 slices in
> `plan.md` for the full rationale.

## Batch 7: reading text box and disk-backed audio

> **Reading text box + disk-backed audio (Batch 7, built):** a toggleable
> floating window shows the chunk currently being read (whole-chunk
> highlight, its own fit-to-box pagination independent of `SpeechTextChunker`,
> zoom bounded by a live 30-word-sentence floor) — opened via a 4th icon in the
> control panel's `CONTROLS` row. Synthesized audio moved from an in-memory
> buffer to a temp WAV file per chunk under `TemporaryFolder\audio\<id>\`
> (sibling of the existing `logs\` folder), fixing memory growth on large
> files; the existing chunk map used by Skip (Decision 32) and the read-through
> timer (Decision 33) is extended with each chunk's file path and source-text
> range rather than duplicated. See Decisions 45-51 (docs/plan/decisions.md) and the Batch 7 slices
> (35-38) in `plan.md`. Slices 36-38 were stacked PRs (#154-#156) that merged
> into each other instead of `main`; they landed on `main` later — open stacked
> PRs against `main`, or retarget each one after its base merges.

## Batch 8: CI within the Actions storage quota

> **CI within the Actions storage quota (Batch 8):** the account's
> Actions+Packages storage is **500 MB, shared across all repos, billed as
> monthly GB-hours** (deleting artifacts doesn't refund it), and one run's
> MSIX + AAB/APK is ~1 GB. So CI uses **zero `upload-artifact`**. PRs are
> compile/test checks only. On `main`, build jobs upload straight to a **draft
> GitHub Release** (Release assets don't count toward the quota), which
> `release-publish` publishes (creating the tag) once every affected build
> succeeds. Android lives in `build.yml` too (`android-build.yml` is gone). A
> `changes` job (`dorny/paths-filter`) **path-filters** per platform: core
> (Domain/Application/Documents/tests/pipeline) → both, `App`/`Infrastructure`
> → Windows, and `Mobile` + `App/VoiceModel` → Android. An untouched platform
> is neither built nor submitted; its assets are carried forward from its last
> release. **When adding a project or shared build file, add its path to the
> right filter group. Don't add `actions/upload-artifact` for build outputs.**
> Repo artifact retention is 1 day as a safety net. See Decisions 52-58 (docs/plan/decisions.md) and
> Slices 39-42 in `plan.md`.

## Batch 9: language packs, iOS, translation (planning text)

> **Language packs, iOS, translation (Batch 9, planning):** once Slices 50/56
> land, **no platform bundles a voice model** (supersedes Decisions 14 and 39):
> every voice downloads as a pack after the first-run language choice, from
> GitHub Releases tagged `packs-*` (separate from the `v<x.y.z>` app releases),
> and the system voice reads until it arrives. User data (text, images, scans,
> translations) stays **100% offline**; only packs are downloaded. One **scoped
> GPL exception**: the Farsi Piper pack carries espeak-ng data (GPL-3.0) inside
> the downloaded pack only, never in the repo or any package. **iOS** is the
> same MAUI project (`net10.0-ios`) shipped to TestFlight from CI, with an `ios`
> path-filter group and the Mobile split rule: `Platforms/Android/**` builds
> Android, `Platforms/iOS/**` builds iOS, the rest of `Mobile/**` builds both.
> See Decisions 59-71 and `docs/plan/slices/43-57`. (Other rules above change
> inside those slices, not before.)

## Batch 10: Dino Reader (planning text)

> **Dino Reader (Batch 10, planning):** a second product in this repo, a kids'
> book reader with a voice per character. It is a second MAUI app,
> `src/DinoReader.App`, sharing the platform code in
> `src/ReadTheStupidText.Mobile.Shared` (moved out of the RTST Mobile app) plus
> Domain/Application/Documents. A pure `src/DinoReader.Core` holds the dialogue
> segmenter, speaker attributor and voice caster (no framework deps). One shared
> version (`v<x.y.z>`) covers both products; each has its own path-filter group.
> See Decisions 72-82 and `docs/plan/slices/58-67`.
> Its icons (all platforms, "Book hug" design) live in
> `docs/design/dino-reader/icons/`: copy from there, never redraw or regenerate them.
> Its screens are the mock `docs/design/dino-reader/Dino Reader v2.dc.html` (index in
> that folder's README): read visual values (colours, sizes, copy, layout) from it,
> never guess them, and never call `DesignSync` for them.
