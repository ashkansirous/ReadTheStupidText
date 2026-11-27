# Slice 453 — Dino first-books picker: a cancelled or failed list load must not say "Connect to the internet"
**Product:** Dino
**Touches:** `src/Dino/DinoReader.App/FirstBooksPage.cs`, `src/Dino/DinoReader.App/FirstBooksPage.Views.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/Dino/DinoReader.Tests/FirstBooks*Tests.cs`

*In progress (wave 2026-11-27), draft PR.* (Wave decision: the English copy is written by the builder in the style of the existing FirstBooks_Offline* strings, and the 14 translations are filled in the same resx files as every other FirstBooks string.)

*Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 299 item 2; not built).*

Moved out of Slice 299 item 2 by the plan cleanup of 2026-11-25. Read live on origin/main that day: `FirstBooksPage.LoadAsync` (`src/Dino/DinoReader.App/FirstBooksPage.cs:226-236`) answers a Cancel on the list wait, and any other load failure, with `ShowLoadFailed()`, which shows `_offlineLayer` (`:330-335`), whose copy is `FirstBooks_OfflineTitle` "Connect to the internet to choose your first books" / `FirstBooks_OfflineBody` (`AppResources.resx:115-116`). A grown-up who taps Cancel, or a server error with a working network, is told the phone is offline.

## Work

- [ ] 1. Two new strings (cancelled, failed) with their own title and body, in all 15 app languages (`Resources/Strings/AppResources.*.resx`), shown by `ShowLoadFailed` for the cancelled and failed cases; the real offline case (`ShowOffline`) keeps the current copy. Try again stays.
- [ ] 2. A test with a fake store client whose list call is cancelled and one whose list call throws: the page shows the new copy, not the offline copy.
- [ ] 3. Android and iOS the same (shared MAUI page).

#### Not yet verified

- [phone] Emulator: open the first-run picker, Cancel during the list load: the cancelled copy shows with Try again.
