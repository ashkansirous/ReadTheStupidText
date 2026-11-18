# Slice 398 — Dino: the parents' Books tab after a lapse (locked tiles, "why" banner, offline notice) and the child's shelf
**Product:** Dino

*Planned 2026-11-15 from the 2026-11-15 Subscription mock (`docs/design/dino-reader/Dino Reader 2026-11-15 Subscription.dc.html`, section 2, frames SB6-SB8). Slice 384 built the greyed tile and the hiding; this slice adds the look the mock draws and the two banners.* *Built 2026-11-18 (night 2026-11-17-2105, wave 3), PR #1247, not merged; passed on the Android emulator, not yet verified on iOS or Windows.* Runs beside Slice 399 and 394: new resx keys go right after the existing `Sub_` keys (399 adds after `Settings_`); the Store's restore handler (`StorePage.xaml.cs:1091`, 394's file) is called, never edited.

**Touches:** `src/Dino/DinoReader.App/GalleryPage.xaml*`, `src/Dino/DinoReader.App/Platforms/Windows/GalleryPage.Desktop.cs`, `src/Dino/DinoReader.App/StoryTile.cs`, `src/Dino/DinoReader.App/ShelfReorderTiles.cs` (locked opacity on the reorder cover, review high #9), `src/Dino/DinoReader.App/IconView.cs` (`IconKind.CloudOff` for the offline banner, review high #1; widened by the coordinator: no other wave slice edits these two), `src/Dino/DinoReader.Core/SubscriptionAccess.cs`, `src/Dino/DinoReader.Core/BookAccess.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**`, `.maestro/dino/subscription.yaml`, `tests/Dino/DinoReader.Tests/**`, `docs/plan/slices/398-dino-locked-books-banner-and-offline-notice.md`

## Decisions (the mock, 2026-11-15)

1. Built behaviour stays: a locked book is greyed in the corner Books tab, opens nothing, offers Subscribe again and Buy this book; the child shelf leaves it out (Slice 384 Decision 4).
2. The built caption "Locked: the subscription ended" (`Sub_Locked`) becomes the tile accessible name only (no visible caption).
3. The offline notice follows Slice 384 Decision 6 (3 days after the last end date the store gave); it is a banner, never a lock. Online, it goes and the store answer decides at once.
4. **Copy.** Every string of SB6-SB8, with its key, is in `docs/plan/notes/2026-11-17-dino-subscription-and-foldables-copy.md` (rows "Subscription SB6-SB8"); this slice cites those keys and invents no wording. The banner's "Restore purchases" link is the Slice 396 restore (`Store_RestorePurchases`), the same one that replaces the removed "Bought books before?" card (user, 2026-11-16: the card goes). Yearly first and "Save {0}%" (user, 2026-11-16) apply to Slice 397's card that "See plans in the Store" and "Subscribe again" open.

## Design (2026-11-15 Subscription mock, section 2: SB6-SB8, three phone frames 375 wide, all UI; the book covers in them are placeholders)

- **SB6, Books tab after a lapse.** Header "Grown-ups’ corner · Books" (`Gallery_CornerTitle` + `Corner_TabBooks`), "Child’s shelf" pill (`Corner_ChildsShelf`). Banner at the top (new): title "Your subscription ended" (new `Sub_BannerTitle`, 14.5 / 600, `#3f3a52`); body "3 books are locked. Your child doesn’t see them. Subscribe again, or buy the ones you want to keep." (new `Sub_BannerBody`, 13 `#3a3450`, line 1.4; `_One` for a single book); two links, gap 16, 13.5 / 600 `#4a46d6`: "See plans in the Store" (new `Sub_BannerPlans`; opens the Store tab on Slice 397 SB5) and "Restore purchases" (`Store_RestorePurchases`, runs the Slice 396 restore, for a lost purchase). The banner says it once for all locked books. Three-column grid. Locked tile: the cover in greyscale at 55%, an ink lock badge, the title 12.5 / 600 `#5a5470`, then two full-width pills 32 high, radius 16, 12.5 / 600, centred: "Subscribe again" (`Sub_Again`; brand `#5B57E8`, white; opens the Store tab on SB5) and "Buy · £0.99" (new `Sub_BuyPrice` "Buy · {0}"; tint; the price is the store string; opens the store sheet; replaces the built "Buy this book", `Sub_BuyThis`). No ••• menu on a locked tile. Unlocked tiles are as built.
- **SB7, Offline, end date passed (new).** Calm sand banner at the top of the Books tab, the sand of the Slice 358 strip (`StripSandFill`, `StripSandText`): title "Can’t check your subscription" (new `Sub_OfflineTitle`); body "No internet. Subscription books stay open until 18 Nov, 3 days after the last date the store gave. Connect to keep them open." (new `Sub_OfflineBody` with the lock date). Shown only while offline, past the last end date the store gave and inside the 3 days. Books stay normal (no grey).
- **SB8, Child shelf after a lapse.** Header "My stories" and "Grown-ups" (`Gallery_Title`, `Gallery_GrownUpsPill`). Only unlocked books show (the mock: The Three Little Pigs, The Lion and the Mouse, The Gingerbread Man). No lock, no grey, no offer, nothing the child can tap to buy. The shelf closes up; renewing or buying puts them back in their old places with no download. (Built by Slice 384; this slice adds a check.)

## Work

- [x] 1. Android: the locked tile look (grey veil at 55%, lock badge, two pills, no ••• menu, accessible name); the banner with its two links; the offline banner with the lock date. (The veil approximates greyscale, see *From review*.)
- [x] 2. iOS and Windows: the same (shared XAML and C#, no platform code; not run on either, see *Not yet verified*).
- [x] 3. Pure rule `SubscriptionAccess.OfflineNotice(now, record)` (offline, past the end date, inside 3 days; returns the lock date) with tests for each edge; the banner count comes from `BookAccess` locked books.
- [x] 4. Strings above in `AppResources.resx` and every `AppResources.<lang>.resx` (15 languages). `Sub_BuyThis` goes if nothing else uses it; `Sub_Locked` stays as the accessible name.
- [x] 5. `FakePurchases` for the offline notice; `.maestro/dino/subscription.yaml` checks the banner, a locked tile with its two pills, the offline banner and the child shelf (locked book absent).

## Verification

- Dino tests pass (offline notice rows). Android emulator: the Maestro flow in item 5. iOS simulator and Windows VM: the banner and tile look.
- The offline 3-day rule on a real device stays in Slice 384 *Not yet verified*.

*Emulator run (`reviews/s398-emu.md`, build a3157f886b22):* subscription flow PASS (`[Subscription] banners locked=1 offline=none`, `banners locked=0 offline=shown`, `shelf columns=2` and `=3`), settings-books-and-buying PASS, paid-books PASS, no FATAL.

*Changed while building:* the banner's Restore no longer polls the Store view; it calls `StorePage.RequestRestore()` and selects the Store tab (review high #4, after the merge of main). The offline banner uses `IconKind.CloudOff` (`IconView.cs`) and the reorder layer draws locked books at 55% (`ShelfReorderTiles.cs`); the coordinator added both files to *Touches*.

#### From review

- Read in full: 10 findings in the high pass, 8 in the medium pass, 6 in the first check pass, 3 in the second. High pass: fixed #1, #3, #5, #6, #7, #8, #10 and #4 (below); #2 and #9 in part (below). Medium pass: fixed #1, #2, #4, #5, #7, #8; two left as they are (below).

- Applied #4 (`reviews/pr-1247-high.md`): "Restore purchases link can silently do nothing and shows no loading state" - the banner's Restore now calls `StorePage.RequestRestore()` and selects the Store tab, as Settings' RestoreAsync does; the polling of `TryRestoreFromKeyboard` is gone and the Store tab owns the wait text and the message.
- Follow-up from #2 (`reviews/pr-1247-high.md`): "desaturate the cover for real greyscale (per platform, or a pre-rendered grey copy)" - kept the grey veil (now at 70% and on the cover's own radius, `TileCoverRadius`): a true greyscale needs an image handler per platform (Android ColorMatrix, iOS CIColorControls, Windows) in `Platforms/*` and `StoryCover`, outside this slice's Touches. Needs its own slice.
- Applied #9 (`reviews/pr-1247-high.md`) in part: the reorder layer's locked books draw at the 55% opacity (`ShelfReorderTiles.Build`) but without the veil or lock badge; they are still draggable there.
- Skipped #3 (`reviews/pr-1247-medium.md`): "ApplyShelfBanners can run before ApplyShape sets _shelfGeometry. The banner may show with zero side margin on the first frame until the next layout." - ApplyShape sets the banner margin on the first layout pass before the page is drawn; no failure path shown.
- Skipped #6 (`reviews/pr-1247-medium.md`): "NetworkProbe treats NetworkAccess.Unknown as online. On a Windows head, offline past the end date can show no notice." - Unknown counted as online is the decision taken for high #6: an unknown or captive network must not show the offline notice.
- Follow-up (no code change) #2 (`reviews/pr-1247-check-medium.md`): "ApplyShelfBanners follows visible tiles, not store state, so banner and offline notice can disagree briefly with the shelf at the lock boundary until the rebuild finishes." - `GalleryPage.xaml.cs:1620`; brief and self-correcting at the rebuild.
- Follow-up (no code change) #4 (`reviews/pr-1247-check-medium.md`): "FormatLockDate uses CurrentUICulture "d"; fa/ar may render Persian/Umm al-Qura calendar. Resx text says "3 days" literally while code uses OfflineGrace; no test ties them." - `SubscriptionAccess.cs:77`; add a test tying the resx text to OfflineGrace and pin the calendar.
- Follow-up (no code change) #5 (`reviews/pr-1247-check-medium.md`): "ApplyShelfMargin runs from ApplyShelfBanners before _shelfGeometry is set on first appearing; banner has zero side margin until next layout." - `GalleryPage.xaml.cs:1104`.
- Follow-up (no code change) #6 (`reviews/pr-1247-check-medium.md`): "OnTileCoverSizeChanged runs a Children search on every size/binding change for non-locked tiles; lapsed tile pills are 32 dp (mock-specified) below 44 dp touch target." - `GalleryPage.xaml.cs:1648` / `GalleryPage.xaml:71`; the pills are 32 dp as the mock draws them, kept; the Children search on every size change stays a possible cleanup.
- Follow-up (no code change) #1 (`reviews/pr-1247-check2-medium.md`): "fake-offline mode=off ignores ended-hours and leaves the subscription end moved into the past by an earlier "on"" - `Testing/DinoTestCommands.Offline.cs:26`, Debug test hook only; a later flow step may start from a dirty state.
- Follow-up (no code change) #2 (`reviews/pr-1247-check2-medium.md`): "NetworkProbe.PlatformOffline treats Unknown/ConstrainedInternet (captive portal) as online, so no offline notice there." - `StoryTile.cs:107`; the decision of high #6, see the skipped medium #6 above.
- Follow-up (no code change) #3 (`reviews/pr-1247-check2-medium.md`): "Locked-cover veil is a flat colour fill, not the mock's grayscale; check against the mock." - `GalleryPage.xaml:54`; same as the greyscale follow-up above.
- Applied #1 and #3 (`reviews/pr-1247-check-medium.md`): `CheckLockedShelf` without a `ShelfFreeForSwap()` guard, and `FakeOffline` logging "ok on" after a faulted move.

#### For CLAUDE.md

- Dino locked books (`src/Dino/DinoReader.App/GalleryPage.xaml.cs`, `StoryTile.cs`): the banner's "Restore purchases" calls `StorePage.RequestRestore()` and selects the Store tab; never poll `TryRestoreFromKeyboard` from the Gallery. The offline notice comes only from `SubscriptionAccess.OfflineNotice`; `NetworkAccess.Unknown` and a captive portal count as online on purpose. (Slice 398: docs/plan/slices/398-dino-locked-books-banner-and-offline-notice.md)

#### Not yet verified

- iOS simulator and Windows: the banner, the locked tile look and the offline notice (no Mac; the VM run was skipped at 3.2 GB free memory).
- The 3-day offline rule on a real device.
- True greyscale on the locked cover: the build is a grey veil (follow-up, needs per-platform image handling).
- The reorder layer still lets locked books be dragged (drawn at 55% without veil or badge).
- 14 non-English strings had no native read.
