# Slice 461 — Dino: after onboarding a new parent sees a book to play, and the store doesn't look empty
**Product:** Dino

*Planned 2026-11-27 from the 2026-11-26 usage analysis (Ninjalog `ashkan-sirous/dino-book-reader`, index `dino_usage`, 19-26 Nov).*

**Starts after:** [Slice 453](453-dino-first-books-cancelled-load-copy.md) (both edit `AppResources*.resx`).

**Touches:** `src/Dino/DinoReader.App/StorePage.xaml.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml.cs`, `src/Dino/DinoReader.App/OnboardingSteps.Route.cs`, `src/Dino/DinoReader.Core/BookAccess.cs`, `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/Dino/DinoReader.Tests/**`, `docs/analytics/dino-session-queries.md`

**Why:**
- Of the real finishers on 1.136, 12 went through the parent gate and 9 reached `corner_store`. 4 kept using the app. 5 left shortly after (3 Germany, 1 Belgium, 1 UK).
- Two German users tapped `corner_store:restore` on a fresh install, and one also tried filter and download. The sessions are `53522bc72ec26a72b415c962ba04b8d5`, `1f6aa8fea0499cf2f5aaf255b99217dd`, `e9886e1de9a8a4c2cb83955cabf2aabd`, `7c1ca4452a8226e41d912b56754da9bd` and `97bef124343b4c07ff13f65e2bdc5106`.
- 6 of 21 finishers left within about a minute of the intro ending without opening a book.
- This is inferred from tap sequences, not proven.
- From the code (origin/main, 2026-11-27):
  - Four books are free: The Lion and the Mouse, X Marks the Treasure!, The Gingerbread Man and The Three Little Pigs.
  - The first-run picker offers only those, needs 2 ticked, and starts with The Three Little Pigs ticked (`BookAccess.cs:249`). So a new shelf holds 2 to 4 books, and 12 of the 16 books are sold.
  - Restore with nothing to restore shows a "nothing bought" line naming the store account, with a redeem action (`StorePage.xaml.cs:1230-1258`).
  - Whether every book has German text and audio isn't verified: the `de` keys exist, but that alone isn't proof.

**Decisions (owner, 2026-11-27: "3- ok"):**
- After the intro the app lands on the shelf with the first downloaded book highlighted and a clear Play button.
- The store lists free books first and shows a loading state for prices instead of empty slots.
- Prices and the free list stay as they are. Changing either is the owner's call, asked first.

**Open for the owner:** after item 1, if the German shelf turns out thin, should one more German-language book be made free? Ask then; no change until answered.

**Work:**
- [ ] 1. Check a fresh install in German, then English, on Android and Windows, with scripted flows and screenshots. Look at the shelf after the intro, the corner store (prices loading, prices failed, offline) and Restore with nothing bought. Count which books have German text and German audio. Write the findings into this file.
- [ ] 2. End of the intro: land on the shelf with the first downloaded book highlighted and a large Play button. Log it as a `ui` event so it can be counted.
- [ ] 3. Corner store: free books first, a loading state while prices load (over 500 ms), and the existing offline notice when prices fail. Nothing shows as an empty slot.
- [ ] 4. Restore on a fresh install: keep the current "nothing bought" message, and add a line pointing back to the free books on the shelf. The text is composed in the UI. The backend returns only the state.
- [ ] 5. iOS gets the same changes as Android (both are the MAUI app). Windows too.

**Verification:**
- Unit tests for the store ordering and for the loading and failed states.
- Scripted device flows on Android, iOS and the Windows VM show the shelf highlight and the store order on a fresh install.
- Ninjalog, after release, for first-launch real sessions that finished the intro: the share that opens a book within 2 minutes (`index=dino_usage event=book_opened` within 120 s of `onboarding_done`, by session), against the 19-26 Nov baseline of 15 of 21. Also the share that leaves right after `corner_store`, against 5 of 9.
