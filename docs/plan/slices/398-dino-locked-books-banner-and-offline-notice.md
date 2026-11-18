# Slice 398 — Dino: the parents' Books tab after a lapse (locked tiles, "why" banner, offline notice) and the child's shelf
**Product:** Dino

*Planned 2026-11-15 from the 2026-11-15 Subscription mock (`docs/design/dino-reader/Dino Reader 2026-11-15 Subscription.dc.html`, section 2, frames SB6-SB8). Slice 384 built the greyed tile and the hiding; this slice adds the look the mock draws and the two banners.* *In progress (wave 2026-11-18), draft PR.* Runs beside Slice 399 and 394: new resx keys go right after the existing `Sub_` keys (399 adds after `Settings_`); the Store's restore handler (`StorePage.xaml.cs:1091`, 394's file) is called, never edited.

**Touches:** `src/Dino/DinoReader.App/GalleryPage.xaml*`, `src/Dino/DinoReader.App/Platforms/Windows/GalleryPage.Desktop.cs`, `src/Dino/DinoReader.App/StoryTile.cs`, `src/Dino/DinoReader.Core/SubscriptionAccess.cs`, `src/Dino/DinoReader.Core/BookAccess.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `src/Dino/DinoReader.App/Testing/**`, `.maestro/dino/subscription.yaml`, `tests/Dino/DinoReader.Tests/**`, `docs/plan/slices/398-dino-locked-books-banner-and-offline-notice.md`

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

- [ ] 1. Android: the locked tile look (greyscale 55%, lock badge, two pills, no ••• menu, accessible name); the banner with its two links; the offline banner with the lock date.
- [ ] 2. iOS and Windows: the same.
- [ ] 3. Pure rule `SubscriptionAccess.OfflineNotice(now, record)` (offline, past the end date, inside 3 days; returns the lock date) with tests for each edge; the banner count comes from `BookAccess` locked books.
- [ ] 4. Strings above in `AppResources.resx` and every `AppResources.<lang>.resx` (15 languages). `Sub_BuyThis` goes if nothing else uses it; `Sub_Locked` stays as the accessible name.
- [ ] 5. `FakePurchases` for the offline notice; `.maestro/dino/subscription.yaml` checks the banner, a locked tile with its two pills, the offline banner and the child shelf (locked book absent).

## Verification

- Dino tests pass (offline notice rows). Android emulator: the Maestro flow in item 5. iOS simulator and Windows VM: the banner and tile look.
- The offline 3-day rule on a real device stays in Slice 384 *Not yet verified*.
