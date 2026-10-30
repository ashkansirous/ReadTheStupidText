# Slice 201: the Norwegian voice pack crashes the app

*Built 2026-10-30 (PR #571), verified on the S21 only; Windows desktop and iOS device not run.*

**Touches:** `.github/workflows/packs.yml`, `src/ReadTheStupidText.Application/Reading/**`, `src/ReadTheStupidText.Domain/Reading/**`, `src/ReadTheStupidText.Mobile/**` (pack/engine loading), `src/ReadTheStupidText.Mobile.Shared/**` (engine loading), `src/ReadTheStupidText.Infrastructure/**` (Windows sherpa engine), `tests/ReadTheStupidText.Tests/**`, pack release constants.

## Problem (found on a real S21, 2026-10-29)

Picking **Norsk** in Read The Stupid Text on Android downloads `piper-nb` (packs-5) and about 2 s later the process dies:
`sherpa-onnx piper-phonemize-lexicon.cc:ReadTokens:106 Error when reading tokens at Line aɪ 161. size: 2`, exit 255.
Because `Language=nb` is saved, every later launch dies too. Logs: `D:\ReadTheStupidText-worktrees\.phone\s21night3-norsk-attempt1.log`, `s21night3-norsk-crashloop.log`.

`files/packs/piper-nb/v1/voices/nvcc/tokens.txt` lines 161-162 are `aɪ 161` and `aʊ 162`: tokens of two code points. sherpa's
Piper reader needs one code point per token and aborts natively (no managed exception to catch). The file is written by
sherpa's `add_meta_data.py` from the `phoneme_id_map` of the nvcc `.onnx.json` in the "Build the Norwegian voice" step of `packs.yml`.
Cause is likely, not yet proven by a fixed pack loading.

## Fix

1. **Pack:** in the Norwegian build step, write a `tokens.txt` sherpa can read. Confirm against sherpa-onnx's source
   (`piper-phonemize-lexicon.cc` `ReadTokens`, via the `library-docs` agent or the pinned sherpa commit) what it accepts, then drop or
   map the multi-code-point entries (espeak-ng emits phonemes one code point at a time, so a diphthong key can never be looked up; check
   that dropping them can't shift other ids, since ids are explicit). Add a build check that fails the packs job when any tokens.txt in
   any Piper/VITS pack (`piper-nb`, `piper-fa`, and the others built there) has a token that isn't exactly one code point. Raise the
   Norwegian pack version and point the apps at the new packs release the way earlier pack fixes did (`.github/CLAUDE.md`, packs releases).
2. **App guard (every platform that loads sherpa engines: Android, iOS, Windows):** before handing a pack to sherpa, validate its
   `tokens.txt` with a pure, unit-tested check in Application/Domain (each non-empty line = one code point, a space, an integer; exact
   rule from step 1's source reading). A pack that fails is treated as broken: not loaded, shown as a failed download with the existing
   retry/redownload path (Slice 182 Failed -> Retry), and the reader falls back the same way it does for a missing pack. Never crash at
   startup because a saved language's pack is bad.
3. **Tests:** unit tests for the validator (good file, two-code-point token, missing id, blank lines, BOM). The scripted device check is
   written but not run by the implementer: switch to Norsk through Settings and read a Norwegian sentence.

## Verification

- Fast checks green; packs workflow change passes actionlint.
- Device (after review): S21 Norsk switch reads Norwegian with the new pack; with the old pack the app shows the failure state instead of dying.

## Built (implementer, 2026-10-30; not yet reviewed or run on a device)

- [x] sherpa rule confirmed in source (v1.13.3 `piper-phonemize-lexicon.cc` `ReadTokens`): one code point per token, `<token> <id>` (an id-only line is the space), no trailing junk, no duplicate token (`SHERPA_ONNX_EXIT(-1)`), `<BLNK>` skipped, and `_ ^ $` looked up unconditionally.
- [x] Pack: the Norwegian build step in `packs.yml` drops multi-code-point keys from `tokens.txt`; a new step "Check every Piper tokens.txt" fails the packs job for any `piper-fa`/`piper-nb` voice with a bad line.
- [x] App guard: `Domain/Reading/PiperTokens`, `PackManifest.BrokenTokenFolders(In)`; a bad pack fails `ValidatePayload` (InvalidPack, the existing failed/retry path), is not "installed" in `LoadInstalled` (so the saved language falls back like a missing pack), and the three engine builders (Windows, Android, iOS) throw `InvalidDataException` before the native load. Debug marker `RTST_PACK_TOKENS_REJECTED`.
- [x] Tests: `PiperTokensTests` (good, two code points, missing/non-numeric id, blank line, BOM, duplicate, missing `_ ^ $`, manifest wiring).
- [x] Scripted check written: `.maestro/rtst/norsk-pack-reads.yaml` (not run).
- [x] Done: packs-10 released with piper-nb v2; `PackConstants.CatalogUrl` points at it. (Original note: run `packs.yml` (new `norwegian_pack_version`, new `release_number`) and bump `PackConstants.CatalogUrl`; the device run. )

## S21 device result (2026-10-30)

Real S21, Android 15. The old piper-nb v1 pack was rejected (`RTST_PACK_TOKENS_REJECTED`) instead of crashing; v2 (packs-10) downloaded and read Norwegian with `piper:nb-knn`; relaunch in Norsk was fine; English and Farsi still read. Night note: [2026-10-29-s21-night-runs.md](../notes/2026-10-29-s21-night-runs.md).

#### Real users (2026-10-30, found in the session audit)

- Every RTST release up to **rtst-v1.73.2** pins a catalog (`packs-9` and older) that still serves the broken `piper-nb-1.zip`, so a user on those versions who picks Norsk crash-loops; store builds have offered Norsk since PR #300. Releases from **rtst-v1.74.0** pin `packs-10` and reject the bad pack. The store builds themselves were never checked on a device.
- Decided by the user 2026-10-30: leave it (no Norwegian users on old versions); Slice 217 adds the forced update for next time. Options that were considered: force an update by setting `minVersion` 1.74.0 for each platform in `readthestupidtext/config.json` (also fixes users who already downloaded v1, since the new app rejects it and fetches v2), or replace the asset in the old packs releases (does not help users who already have v1 installed).

#### Review minors left (2026-10-30)

- Pass 1 left six minors (duplicated code, files read twice). Pass 2 (medium) left these; corrected 2026-10-30: some do change behaviour:
  - A previously installed bad `piper-nb` silently reads as "not installed"; `RTST_PACK_TOKENS_REJECTED` is Debug-only, so release builds leave no trace (`MobileVoicePackService.cs:160`, `WindowsVoicePackService.cs:104`).
  - `packs.yml:399` filter uses `str.splitlines()` (splits on U+2028 and others); the check step and C# split on `
` only.
  - `packs.yml:485` hard-codes the pack shape (`len(files) != 5`), so a new speaker fails the job.
  - `RequirePiperTokens` is copied three times and gates on `Kind==Vits`, not `PiperPackIds`; `PiperPackIds` is a hard-coded allow-list.
  - `LoadInstalled` parses each `tokens.txt` twice; `PiperTokens.cs:85` whitespace rules read from sherpa source, not proven.

#### Not yet verified

- Windows desktop (sherpa engine with the new pack) and iOS device not run.
- The Maestro flow `.maestro/rtst/norsk-pack-reads.yaml` has never run. (Corrected 2026-10-30: Maestro 2.12.0 is installed in `<worktrees>/.tools/maestro`; the night agent looked on the PATH. Run it through `tools/emulator/run-flows.ps1`; planned in Slice 209's checks.)
