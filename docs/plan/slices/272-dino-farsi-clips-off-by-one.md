# Slice 272 — Dino: Farsi pre-made audio falls back to the device voice after the first animal sound

*In progress 2026-11-05 (user: "fix the farsi books").* Bug fix, Dino Reader, content and tooling (the app code is unchanged). Reported by the user on 2026-11-05: in Lion and Mouse in Farsi, from page 5 ("but you are too small", where the lion lifts his paw) the read falls back to the device voice and stays on it; English was fine.

**Touches:** `content/dino/{goldilocks,lion-and-mouse,little-red-hen,red-riding-hood,three-little-pigs,tortoise-and-hare,town-mouse-country-mouse,ugly-duckling}/audio/fa/**`, the same eight books' `audio-cast.json` (`audioPacks.fa`), `tools/premade-audio/**`, `tools/DinoReader.BookBuilder/**`, `.github/workflows/dino-store.yml`, `.github/workflows/tools-selftests.yml`, `.github/CLAUDE.md`, `tools/CLAUDE.md`, `docs/plan/slices/272-dino-farsi-clips-off-by-one.md`.

## Cause (shown by a scan, 2026-11-05)

- The app finds a clip by the piece's position and checks the manifest's text hash at that position (`src/DinoReader.Core/PremadeAudio.cs` `ClipAt`). A wrong hash means the device voice reads that piece.
- The Farsi clips were recorded on 2026-11-03 with the animal sound words (e.g. `غرررر!`, the lion's roar) counted as spoken pieces. Slice 255 (ffd09cc0, merged 2026-11-04 02:40Z) made the app play a real recording for them and leave them out of the count (`StoryVoicing.cs` `WithAnimalSounds`, `SentenceDump.cs`). From the first sound word on, every Farsi piece's position is one (or more) off, its hash misses, and the rest of the book is read by the device voice.
- The other 14 languages were recorded after 04:15Z that day (Slice 246 D3), with sound words already left out, so they line up. (Timing read from the wave ledger and PR merge times, not from the clip files: likely, not proven.)
- Scan of origin/main 07d00d4b: 16 books x 15 languages = 240 manifests, 232 line up, 8 do not, all Farsi. Extra entries / first bad position: goldilocks 5/51, lion-and-mouse 6/19, little-red-hen 14/16, red-riding-hood 3/21, three-little-pigs 7/24, tortoise-and-hare 1/33, town-mouse-country-mouse 8/20, ugly-duckling 6/12. The only difference in all 8 is the sound-word entries; no text was edited or re-split. Record: `D:/ReadTheStupidText-worktrees/reviews/lion-fa/scan.md` (copy the table into this file when building).
- The live store's packs are checked against the same dumps (`reviews/lion-fa/live-scan.md`); add its result here.

**Why no check caught it:** nothing compares a pack's manifest with the sentences the current app reads. Slice 255 changed the count and every pack built before it silently stopped fitting.

**What analytics showed** (`dino_usage`, 2026-11-03 to 2026-11-05): `read_voice` for lion-and-mouse fa was `premade` twice and `device:clip_missing` once (23:34Z, Android 1.106.0). A read counts as `premade` when any piece had a clip, so a read that falls back from page 5 on looks like a good one; the fallback reasons are Slice 273's. A lion-and-mouse fa `audio_pack_download` has `started` with no `done` or `failed` today: find out why (item 5).

## Work

- [ ] 1. **Re-align the 8 Farsi manifests** with the current sentence dump (`dump-sentences <book> --lang fa`): drop the sound-word entries and renumber, reusing every existing clip by hash (no Gemini request; `generate.py` on the current dump with all clips present must send 0 requests, `--dry-run` first). Fetch the clips with `git lfs pull --include "content/dino/<book>/audio/fa/**" --exclude ""` first.
- [ ] 2. **Rebuild and publish the 8 Farsi packs** (`build-audio`, `audio-cast.json` `audioPacks.fa` updated) so `dino-store.yml` uploads them on merge, and make sure a phone that already holds the old Farsi pack fetches the new one (read how BookUpdater/AudioPackStore decide; bump the pack version if the hash alone does not trigger it).
- [ ] 3. **A check that every pack lines up**: a BookBuilder command (or a step in an existing one) that, for every book x language with an `audio/<lang>/manifest.json`, compares each index's hash with the current dump and fails naming the book, language and first bad position. Run it in CI on every change to `src/DinoReader.Core/**`, `src/DinoReader.App/StoryVoicing.cs`, `tools/DinoReader.BookBuilder/**` or `content/dino/**`, so an app change that alters the count (like Slice 255) fails until the packs are re-aligned. Manifests are LFS: the CI step fetches only the `manifest.json` files (`git lfs pull --include "content/dino/*/audio/*/manifest.json" --exclude ""`). Verify: it fails on origin/main before item 1 with the 8 Farsi books, and passes after.
- [ ] 4. **Re-check the live store** after the merge's `dino-store.yml` run: every live pack's manifest lines up with the dump (the `live-scan` script in `reviews/lion-fa/`; commit it under `tools/` if item 3 does not already cover live packs).
- [ ] 5. **The cut-off lion-and-mouse fa download** (`started`, no `done`/`failed`, 2026-11-05): read `AudioPackStore`/`BookUpdater` for a path that starts a download and ends without counting either. Fix it here if it is a missing count; else write the cause in this file.

## Verification

- Item 3's check fails on the old manifests and passes on the new ones; `dotnet test tests/DinoReader.Tests`.
- Emulator: Lion and Mouse in Farsi with the new pack reads past page 5 with pre-made clips (logcat: clip at piece 19 onward, no device-voice run after the roar), and the lion's roar recording still plays. Android and iOS read the same pack through the same Core code; iOS is checked by the user on the next TestFlight build (`unverified.md`).
