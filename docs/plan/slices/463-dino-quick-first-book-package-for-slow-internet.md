# Slice 463 — Dino: a small "quick" first-book package for slow internet, replaced by the full one in the background
**Product:** Dino

*Built 2026-11-27 (night wave 9) in PR #1444, not merged. Open: item 2 (the owner's side-by-side page, an Artifact, needs an interactive session). No quick packages exist yet (no `content/dino/*/quick` folders), so nothing ships a quick copy until the quick pictures and clips are made after the owner's pick. The slow-link switch was not reached on a device (see Not yet verified).*

*Planned 2026-11-27 from the owner's answer on the 2026-11-26 usage analysis.*

**Design (coordinator, wave 2026-11-27, before the build; the builder builds this):**
- **One owner of the state:** the installed book's variant (`quick` | `standard` | `hd`), stored with the high-res state the way `HighResUpgrades` keeps its ledger (`HighResStateFile.WriteAtomic`). Nothing else decides which copy is installed.
- **States:** standard downloading -> (slow: after about 3 s the measured speed predicts more than about 10 s left) cancelled -> quick downloading -> installed `quick` -> background standard download staged (`StagedBookUpdates`) -> verified -> swapped, installed `standard` (then high-res where `PictureSizeChoice` says so, as today).
- **The switch decision is one pure, unit-tested function** (bytes so far, elapsed, total bytes -> keep or switch).
- **Interrupts:** app killed during the quick download: the first-books step starts over as it does today (Slice 459). Killed, offline or failed during the background standard download: the staged copy is dropped, the book stays `quick`, and the upgrade is tried again at the next start (the `HighResUpgradeRunner` pattern, metered links included). Cancel on the first-books page: cancels both, as today. A swap that fails verification keeps the quick copy. The reading position and progress are never touched by a swap: only the picture and audio files change. A book never stays `quick` once a standard download has completed and verified.
- **Design after check pass 1 (coordinator, 2026-11-27 19:40Z; third strike in the swap and record area, so one design, not patches).** The root cause of `reviews/pr-1444-check-1.md` #2, #4 and #5 is one fault: the recovery and the orphan drop run outside the lock the installs hold, so they can see a half-done swap or import. The rules:
  1. **One lock per store.** `AudioPackStore.RecoverInterruptedSwaps` runs under the same `_gate` as `InstallAsync`, so it can never meet a swap between its two renames. The `.old` copy is deleted only under that gate, after the new manifest is confirmed in `final` (#4, #5).
  2. **Quick records name their launch.** A quick record (book or pack) written before an import carries the launch id that wrote it. The orphan drop removes only a record from an EARLIER launch whose book is not on the shelf. A record of the current launch is never dropped, so an import in flight is safe (#2). The same intent-then-confirm order applies to the quick pack record: write it before the install, confirm it after (#6).
  3. **A record goes only with its book.** Cancel, a failed fetch or a page leave never forgets a quick record while the quick book is on the shelf; it goes only when a verified standard copy is swapped in, or the book leaves the shelf (#3). `SwitchedToQuick` is set only when the quick fetch succeeded (#7).
  4. **Only a bad file counts.** The failure ledger counts only a verification failure (wrong size, hash or a broken zip, a 404). Network, timeout, offline, disk-full and IO errors count nothing and the pass simply retries later (#1).
- **No default until the owner's pick:** the quick sizes are the named defaults (448x600, AAC 24 kb/s mono), built and listed; item 2's pick may change them before release.

**Starts after:** [Slice 459](459-dino-first-book-playable-in-seconds.md) (merged as PR #1395; it edits the same first-books download path).

**Touches:** `tools/dino/prepare-story-pictures.ps1`, `tools/dino/premade-audio/encode.py`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `worker/stories.ts`, `worker/**` tests, `src/Dino/DinoReader.App/StoreClient.cs`, `src/Dino/DinoReader.App/HighResUpgradeRunner.cs`, `src/Dino/DinoReader.Core/PictureSizeChoice.cs`, `tests/Dino/DinoReader.Tests/PictureSizeChoiceTests.cs`, `tests/Dino/DinoReader.Tests/**` (first books), `src/Dino/DinoReader.App/FirstBooksPage.cs`, `worker/events.ts`, `src/Dino/DinoReader.App/HighResUpgrades.cs`, `src/Dino/DinoReader.App/StagedBookUpdates.cs`, `src/Dino/DinoReader.App/PictureSizeSource.cs`, `src/Dino/DinoReader.App/UsageCounter.cs`, `src/Dino/DinoReader.App/MauiProgram.cs`, `src/Dino/DinoReader.Core/HighResStateFile.cs`, `src/Dino/DinoReader.Core/HighResUpgradePlan.cs`, `src/Dino/DinoReader.Core/StoreCatalog.cs`, `src/Dino/DinoReader.Core/UsageEvent.cs`, `src/Dino/DinoReader.Core/FirstBookSwitch.cs` (new), `src/Shared/Sirous.Reading.Application/Reading/WaitStage.cs`, `src/Shared/Sirous.Reading.Application/Usage/**`, `tests/Shared/Shared.Tests/**` (usage wait stages), `src/Dino/DinoReader.App/FirstBookPrefetch.cs`, `src/Dino/DinoReader.App/AudioPackStore.cs`, `tests/Dino/DinoReader.Tests/AudioPackStoreTests.cs`, `tools/dino/DinoReader.BookBuilder/**` (quick package and quick audio zips), `tests/Dino/DinoReader.Tests/BookBuilderTests.cs`, `tools/dino/premade-audio/test_premade_audio.py`, `src/Dino/DinoReader.Core/UiUsage.cs`

**Why:** the owner's words (2026-11-27): "we probably need an even lower quality package for people with slow internet. when we start downloading the first book, if it is too slow, we drop it and switch to the even smaller package. then we download a proper package in the background. when it is done, it would replace the low quality package."

**Measured 2026-11-27** (live list `https://dinoreader.sirous.uk/api/stories` and a real download of The Lion and the Mouse; MB = 10^6 bytes):

| Variant | Pictures | Audio | Lion and the Mouse (book + en) | 1 Mbit/s | 5 Mbit/s |
| --- | --- | --- | --- | --- | --- |
| High-res (tablets on unmetered links; `PictureSizeChoice.cs:42-49`) | 1792x2400 JPEG q85 | AAC 48 kb/s | 11.43 + 1.66 = 13.1 | 105 s | 21 s |
| Standard (today's default) | 896x1200 JPEG q85 | AAC-LC mono 24 kHz 48 kb/s | 2.48 + 1.66 = 4.14 | 33 s | 6.6 s |
| Quick, AAC (measured in scratch) | 448x600 JPEG (ffmpeg q 8) | AAC 24 kb/s mono | 0.33 + 0.85 = 1.18 | 9.4 s | 1.9 s |
| Quick, opus (measured in scratch) | 448x600 JPEG | opus 16 kb/s mono | 0.33 + 0.54 = 0.87 | 7 s | 1.4 s |

- Across all 16 books, the standard package is 2.5-4.0 MB, median 3.2 MB. The en pack is 0.8-2.7 MB, median 1.9 MB. High-res is 9.0-16.6 MB.
- Pictures are about 60% of a standard download and audio about 40%.
- Audio has no variants today. Pictures do: standard and high-res already exist, and `HighResUpgradeRunner` already swaps standard for high-res in the background. The quick-to-standard swap can reuse that pattern.

**Decisions (decided by this planning session, 2026-11-27, from the owner's answer):**
- A third, smaller variant, "quick", is built for every store book and its packs. It is listed beside the standard and high-res variants, the way `hdDownloadUrl`/`hdBytes` are.
- The switch rule works on the first book in the first-books step only:
  - Start the standard download.
  - If after about 3 s the measured speed predicts more than about 10 s left, cancel it and fetch the quick variant.
  - When the first book is playable, download the standard package in the background. Swap it in when it is complete and verified. The reading position is kept and nothing shows a gap.
- Quick audio stays AAC, at 24 kb/s mono. Opus would save another 0.3 MB per book but changes the codec, the decoders and the publish pipeline. That's not worth it for the first version.
- **Decision (Start pass 2026-11-27, obvious choice):** build the named defaults (448x600 pictures, AAC 24 kb/s mono) behind the quick variant; publish nothing as the default until the owner's pick (the `**You:**` line for Slice 463 in `docs/plan/unverified.md`).

**Open for the owner:**
- After item 2, the quick pictures (448x600) and audio (24 kb/s) need a look and a listen on a phone. Are they good enough for a child's first book? The slice publishes a private side-by-side page (standard against quick) for that look and listen (`voice-pick` pattern). The owner's pick sets the final picture size and audio bitrate before anything ships.

**Work:**
- [x] 1. Build the quick variant in `prepare-story-pictures.ps1` and `premade-audio/encode.py`. Publish it with `publish-store-books.mjs` (with tests), and list it in `worker/stories.ts` as `quickDownloadUrl`/`quickBytes`, per pack too.
- [ ] 2. Owner check: the side-by-side page for the four free books in English and German. Record the pick in this file.
- [x] 3. App: the switch rule in the first-books download (`StoreClient`), and a variant flag on the installed book. A background upgrade from quick to standard (and on to high-res where `PictureSizeChoice` says so) reuses `HighResUpgradeRunner`, including on a metered link. A quick book never stays quick once a standard download has completed.
- [x] 4. Count it: `wait_shown` detail gains `quick` when the switch fires, and the upgrade's result is a closed-list `fallback` or `ui` detail. No new blobs.
- [x] 5. Android, iOS and Windows all get it, through the shared MAUI code.

**Verification:**
- Publish-script and Worker tests list all three variants.
- App tests cover the switch threshold, the cancel-and-switch, the background swap, and a swap failure that keeps the quick copy.
- Device runs on Android, iOS and the Windows VM throttled to 256 kbit/s: the first book plays within about 15 s, and it is replaced by standard later.
- Ninjalog, after release: `index=dino_usage event=wait_shown screen=first_books | stats dc(session) by detail` shows `quick` firing for slow links, and `getting_file:>20` near zero.

#### Built

- Quick tooling: `prepare-story-pictures.ps1 -Quick` (448x600, q75), `encode.py` quick (AAC 24 kb/s mono), `BookBuilder` `QuickPack` and quick audio zips, `publish-store-books.mjs` uploads and records `quickBytes`, Worker `?size=quick`.
- App: `FirstBookSwitch.Decide` and `SlowLinkWatch` (`FirstBookSwitch.cs`), the quick fetch with a 404 fallback to standard, the background quick-to-standard swap through `StagedBookUpdates` and `HighResUpgradeRunner`, and an in-place `AudioPackStore` replace. Shared MAUI code, no `#if`.
- Counting: wait stage `getting_quick_file`, fallback `store:quick_upgrade_failed`, ui screen `shelf:quick_upgraded`.

*Changed while building:*
- Item 4 said the `wait_shown` detail gains `quick`. It is the stage name `getting_quick_file`, so the detail is `getting_quick_file:<bucket>` and contains `quick` (decided by the coordinator); the Verification's Ninjalog query reads that.
- Design after check pass 1 (rules 1-4 above) replaced the first swap and record code.
- Known behaviours: an own-edited quick book is never swapped; the progress bar restarts once after the switch; a quick audio zip is built only when `<clips>/quick/` is complete.

*Device run 2026-11-27* (emulator-5554; logs in `D:/ReadTheStupidText-worktrees/reviews/s463-device/`): normal link PASS (the book landed, no switch, no crash). Slow link NOT REACHED: with `umts` on mobile data the picker's store list timed out twice (`[Store] list attempt=2 source=picker cause=Timeout`, then `[FirstBooks] offline listed=0`), so the switch was never judged. Likely a list-timeout under a slow link; not shown to be this slice's (follow-up below). The draft flow is `reviews/s463-device/s463-quick-wait.yaml`.

#### From review

Findings files read to the last finding: high 9 of 9, medium 7 of 7, check 1 9 of 9, check 2 8 of 8, check 3 6 of 6. Every other finding in them was fixed (check 3 has only follow-ups, listed below).

- Skipped #6 (`reviews/pr-1444-high.md`): "Slice item 4 says the" `wait_shown` detail gains `quick` - kept the stage `getting_quick_file`, whose detail is `getting_quick_file:<bucket>` and so contains `quick`, which the Ninjalog check above reads; a new stage keeps the closed stage list clean. Only the second half (reset the switched flag per download) was applied.
- Skipped #7 (`reviews/pr-1444-medium.md`): "During SwapInPlace the installed pack folder does not exist between the two renames, and InstalledVersion/Open take no lock against the install gate, though the comment says the pack is never missing." - the two renames leave the pack folder missing for microseconds; the `.old` recovery copy restores it after a crash, so no data is lost. A clip looked up at that instant is not retried: its slot stays silent and reports `clip_missing` (`ReadingClips.cs:11`, `AudioPackStore.cs:229`), the same as any missing clip. (Coordinator correction: the first skip line wrongly said the player retries.)
- Skipped #8 (`reviews/pr-1444-check-1.md`): "SlowLinkWatch.Observe judges on the final tick (done == total); a slow average can cancel a download that just finished, then the quick package is fetched needlessly." - a cancel on the final tick only switches a download that was about to finish to the small package, which is then upgraded in the background; follow-up (below).
- Skipped #9 (`reviews/pr-1444-check-1.md`): "DiscardAside/RestoreAside log only via Debug.WriteLine, not _log; the try/catch in DiscardAside is dead (DeleteFolder swallows IOException)." - logging style and a dead try/catch, no behaviour change; follow-up (below).
- Check pass 1 (`reviews/pr-1444-check-1.md`) #1-#7 were built as the one **Design after check pass 1**, rules 1-4. Two readings: rule 1 takes a dedicated swap lock in `AudioPackStore` (held by both renames and by the recovery), not the install `_gate`, because the gate is held across a whole download and a lookup (`InstalledVersion`) would then block the caller's thread for it; the `.old` copy is settled by the recovery decision before any swap. Rule 2's "launch id" is the process: `HighResUpgrades` remembers the ids whose quick record this process wrote, and the orphan drop skips them. #5's rate limit on retrying a failed recovery pass was not added (the existing test requires the next call to try again; the cost is one folder listing per lookup while a fault lasts).
- Follow-up (from #8): `SlowLinkWatch.Observe` should not judge at `done >= total`, and the watch should be marked complete once the fetch returns (`FirstBookSwitch.cs`).
- Follow-up (from #9): `AudioPackStore.DiscardAside`/`RestoreAside` become instance methods writing to `_log`; drop the dead try/catch in `DiscardAside`.
- Check pass 2 (`reviews/pr-1444-check-2.md`) #1-#5 were applied under the same rules 1-4 (every write to a pack's final folder takes `_swapLock`; a failed `RestoreAside` clears `_recovered` so the next lookup recovers; the quick pack record gets the complete-shelf read and the this-launch guard; only a confirmed bad file counts, everything else is `TryLater`/`Transient`).
- Skipped #6 (`reviews/pr-1444-check-2.md`): "UpgradeQuickPackAsync installs with CancellationToken.None, so a runner cancel (metered link, app leaving) is ignored once the zip is fetched; unpack and swap keep the install gate." - marked follow-up by the reviewer: a cancel during the background pack install only finishes a download; follow-up (below).
- Skipped #7 (`reviews/pr-1444-check-2.md`): "Pack record forgotten whenever result != Installed, including AlreadyInstalled; a concurrent install that left the quick copy loses its record and ledger entry, so it is never replaced." - marked follow-up by the reviewer: AlreadyInstalled re-records on the next pass; follow-up (below).
- Skipped #8 (`reviews/pr-1444-check-2.md`): "RemoveFromShelfAsync does not call upgrades.Remove or discard the staged .std.dino; stale quick records stay until next launch and a later same-launch refetch may download redundant standard packs." - marked follow-up by the reviewer: a book removed from the shelf has its records dropped by the next orphan pass; follow-up (below).
- Follow-up (from check 2 #6): `StoreClient.UpgradeQuickPackAsync` passes `cancel` to the pack install (the swap is journaled).
- Follow-up (from check 2 #7): forget the pack record only on Failed, NotFound or Cancelled, not AlreadyInstalled (`StoreClient.cs`).
- Follow-up (from check 2 #8): `RemoveFromShelfAsync` calls `upgrades.Remove` and discards the staged `.std.dino`.
- Check pass 3 (`reviews/pr-1444-check-3.md`): no reopening findings; its six findings are follow-ups, not fixed (below).

**Follow-ups (not built):**
- [ ] F1 (`reviews/pr-1444-check-3.md` #1): `AudioPackStore.InstalledVersion` takes `_swapLock` only inside `RecoverInterruptedSwaps()` and lists the folder after release; between the two renames of `SwapInPlace` a reader can see no pack or an older version (a few ms). Enumerate under `_swapLock` or fall back to the `.old` copy.
- [ ] F2 (check-3 #2): the quick-to-standard upgrade runs on any connection, metered data included, with no opt-out (`HighResUpgradeRunner.cs`). Confirm that is intended, else gate on `PictureNetwork.Unmetered`.
- [ ] F3 (check-3 #3): `StoreClient.UpgradeQuickPackAsync` calls `packs.InstallAsync` with `CancellationToken.None`, so a cancelled pass can sit behind `_gate` held by another pack download. Pass `cancel` once the zip is local (same area as the check 2 #6 follow-up).
- [ ] F4 (check-3 #4): `SlowLinkWatch` is built with the standard size (`item.Bytes`) even when the high-size package (`HdBytes`) is fetched; past the standard size `Decide` returns Keep, so a slow high-res fetch may never switch to quick. Pass `item.BytesFor(size)`.
- [ ] F5 (check-3 #5): `worker/stories.ts` `serveAudio` handles `size=quick` but treats `size=hd` as the standard pack (200 instead of 400). Accept only standard and quick for audio.
- [ ] F6 (check-3 #6): `AudioPacks.BuildQuick` returns null on an incomplete `quick/` set without deleting a stale `.quick.zip` from an earlier build; the publisher can upload it unlisted, or list it if meta.json later lists `quickBytes`. Delete the old zip when returning null.
- [ ] F7: `StoreClient.SwapHighResAsync`'s catch (already on `main`) counts any non-IO exception as a bad package, the same fault check pass 2 #5 fixed in the standard-over-quick swap.
- [ ] F8 (device run): the first-books store list timed out twice under an `umts` slow link, so the switch was never judged. Check whether the list timeout is too short for a slow link; plan a slice if confirmed.
- [ ] F9: commit the draft flow `reviews/s463-device/s463-quick-wait.yaml` as `.maestro/dino/first-book-slow-link.yaml`.

#### For CLAUDE.md

- Every write to an audio pack's final folder (`AudioPackStore.cs`: the swap, the plain publish, the interrupted-swap recovery) takes the store's `_swapLock`; the `.old` copy is deleted only after the new manifest is confirmed in `final`. Never run recovery outside that lock. (Slice 463: docs/plan/slices/463-dino-quick-first-book-package-for-slow-internet.md)
- A quick record (book or pack, `quick-installed.json` via `HighResUpgrades`) is written before the import, names its launch, and goes only when a verified standard copy is swapped in or the book leaves the shelf; the orphan drop skips records of the current launch. The failure ledger counts only a confirmed bad file (wrong size, hash, broken zip, 404), never a network, timeout, disk or IO error. (Slice 463: docs/plan/slices/463-dino-quick-first-book-package-for-slow-internet.md)
- The first-book wait stage for the quick fetch is `getting_quick_file`; it is listed in `WaitStage.cs`, `UsageWaitStage.cs` and `worker/events.ts` together, and the Ninjalog query reads `getting_quick_file:*`. (Slice 463: docs/plan/slices/463-dino-quick-first-book-package-for-slow-internet.md)

#### Not yet verified

- [phone] The slow-link switch judged after about 3 s on a real slow link, and the 404 fallback to standard (try `emu network speed edge`; the 2026-11-27 `umts` run never got past the store list).
- [owner] Item 2's pick: the quick picture size and audio bitrate after the side-by-side look and listen.
- [session] The real quick-then-standard swap, once quick packages are published.
- [cloud] The iOS simulator first-books step on a slow link.
- [session] The Windows VM first-books step throttled to 256 kbit/s.
- [session] Ninjalog after release: `wait_shown` detail `getting_quick_file:*` appears.
