# Slice 418 — Dino: one shelf with a lock, and a five-step onboarding

**Product:** Dino

*In progress (claimed 2026-11-19 on feat/s418-lock).*

Source: user, 2026-11-19 (second round of user testing): "Onboarding was bad. On the listen step, users heard the book but
didn't notice the highlight asking them to tap the second language. Some users didn't want to record their voice and had no
way past that step. The tour of the grown-ups' corner at the end of onboarding was useless. The Child's shelf and Grown-ups
buttons at the top were confusing." The user's decisions below are final ("don't ask about them again").

**Touches:** `src/Dino/DinoReader.App/GalleryPage.xaml`, `src/Dino/DinoReader.App/GalleryPage.xaml.cs`,
`src/Dino/DinoReader.App/ShelfHeader.cs`, `src/Dino/DinoReader.App/CornerShell.cs`, `src/Dino/DinoReader.App/CornerTabs.cs`,
`src/Dino/DinoReader.App/CornerTour.cs` (deleted), `src/Dino/DinoReader.App/GrownUpMode.cs`, `src/Dino/DinoReader.App/GrownUpGate.cs`,
`src/Dino/DinoReader.App/GatePage.xaml`, `src/Dino/DinoReader.App/GatePage.xaml.cs`, `src/Dino/DinoReader.App/BookSheet.cs`,
`src/Dino/DinoReader.App/StorePage.xaml.cs`, `src/Dino/DinoReader.App/SettingsPage.cs`, `src/Dino/DinoReader.App/App.xaml.cs`,
`src/Dino/DinoReader.App/LockBar.cs` (new), `src/Dino/DinoReader.App/LockTimer.cs` (new),
`src/Dino/DinoReader.App/OnboardingSteps.cs`, `src/Dino/DinoReader.App/OnboardingSteps.Route.cs`,
`src/Dino/DinoReader.App/OnboardingSteps.Languages.cs`, `src/Dino/DinoReader.App/OnboardingListenPage.cs`,
`src/Dino/DinoReader.App/OnboardingVoicePage.cs`, `src/Dino/DinoReader.App/FirstRunPage.xaml.cs`,
`src/Dino/DinoReader.App/RecordPage.cs`, `src/Dino/DinoReader.App/RecordingAudio.cs`, `src/Dino/DinoReader.App/Purchases/BookPurchases.cs`,
`src/Dino/DinoReader.App/Platforms/Windows/DinoKeyboard.cs`, `src/Dino/DinoReader.App/Platforms/Windows/GalleryPage.Desktop.cs`,
`src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`,
`src/Dino/DinoReader.App/Testing/DinoTestCommands*.cs`, `src/Dino/DinoReader.Core/LockClock.cs` (new),
`src/Dino/DinoReader.Core/OnboardingDemo.cs`, `src/Dino/DinoReader.Core/CornerLayout.cs`, `src/Dino/DinoReader.Core/BookSheetPlan.cs`,
`tests/Dino/DinoReader.Tests/LockClockTests.cs` (new), `tests/Dino/DinoReader.Tests/OnboardingDemoTests.cs`,
`tests/Dino/DinoReader.Tests/**`, `.maestro/dino/**`, `docs/plan/slices/418-dino-one-shelf-with-a-lock.md`

**Replaces:** the grown-ups' corner as a separate mode (Slice 325 corner header and tabs, Slice 189 "For grown-ups" button,
Slice 228 kids' shelf vs corner, Slice 349 corner status bar) and the onboarding tour (Slice 362 part D). What the old parts did:

- `GrownUpMode.IsGrownUp`, in memory only, false at every start, `Leave()` when the app leaves the foreground
  (App.xaml.cs:68, :402, :500). **Kept:** unlocked = `IsGrownUp`; every start and every trip to the background locks.
- Gate (hold 3 s via `HoldTracker.GateHold`, then the sum from `GateChallenge`, keypad; centred card from 600 wide,
  Slice 363; keyboard, Slice 235 B). **Kept as is**, opened from the lock; its back button becomes **Cancel** (`Common_Cancel`).
- Corner tabs Books / Store / Settings with their content (`StorePage`, `SettingsTab`, Books in GalleryPage). **Kept**, moved
  into the one tab bar; content unchanged (Slice 384, 396, 397, 398, 399 as built).
- Windows keys: Ctrl+1/2/3 pick a tab, Ctrl+R restores on the Store tab, Esc leaves the corner. **Kept:** Ctrl+1/2/3 pick
  Books/Store/Settings (Store/Settings while locked act like a tap: hint and pulse), Ctrl+R only while unlocked on Store.
  **Dropped on purpose:** Esc leaving the corner (there is no corner). **New:** Ctrl+L locks, or opens the popup while locked.
- The coral "Child's shelf" pill (`GalleryDone`) and the shelf's "Grown-ups" button (`ShelfHeader.cs:88`): **dropped on purpose**.
- The corner tour (`CornerTour.cs`, `CornerTourFlow`, `TourStop`): **dropped on purpose**, with every `Tour_*` string except
  `Tour_StartReading` (still used on NO7).
- Onboarding's second-language listen (`ListenDemo`, `Onboarding_ListenNext`, `Onboarding_TapLanguage`, `Onboarding_ListenOther`,
  `Onboarding_ListenWide`): **dropped on purpose**.
- Onboarding's try recording deleted at voice-step end and on abandon (`OnboardingTryStore.DeleteAsync`). **Kept**, and also
  run by Skip this step.

## Design

Page **"Dino Reader 2026-11-19 One Shelf with a Lock"** of the design canvas
<https://claude.ai/code/artifact/16e8b2ab-0a01-432b-973a-f35f61f629b1#page-Dino-Reader-2026-11-19-One-Shelf-with-a-Lock>,
copied verbatim to `docs/design/dino-reader/Dino Reader 2026-11-19 One Shelf with a Lock.dc.html` (frames L1-L8, WL1-WL2,
NO1-NO8; section 1 holds the rules). Build exactly what it shows. It replaces the "Dino Reader 2026-11-13 Onboarding" page
(marked replaced on the canvas) and the dark corner header (ink bar with the coral Child's shelf pill) on every earlier page;
the content of those pages (Store states, subscription, Settings rows) still applies.

## Slices this changes

- **362** (onboarding, `docs/plan/slices/362-dino-onboarding-shows-what-dino-can-do.md`): six steps become five; part B's
  second-language step (N4b-N4d) and all of part D (tour, N8a-N10) are removed; part C gets Skip this step.
- **384** (subscription, `docs/plan/slices/384-dino-subscription.md`) and **396, 397, 398, 399**: the Store and Settings screens
  sit under the shelf's header with the countdown chip instead of the ink corner header; their content is unchanged. 399's tour
  Store tip goes with the tour.
- **325** (corner header and rail), **349** (corner status bar and tab labels), **189** (For grown-ups button), **228**
  (kids' shelf vs corner), **392** (grown-ups button flow): replaced by the one tab bar and the lock.
- **61, 363, 235 part B** (the gate): unchanged, except it opens from the lock and its button says Cancel.
- **403** (foldables): builds on the new rail (lock row at the rail's foot) instead of the corner rail.
- **404** (store pictures): shots that show the corner or the Grown-ups button are out of date once this ships; this slice
  updates the shot flows, the retake belongs to 404.

## Decisions (user, 2026-11-19, final)

Onboarding
1. The listen step loses its second-language part completely. The page plays once; Continue works when it ends.
2. The recording step can be skipped. "Skip this step" sits at the bottom of both the voice intro (NO5) and the recording page
   (NO6), same link, same text. Anything recorded so far is deleted.
3. The corner tour is removed completely (welcome card, tab tour, "Child's shelf ready?", "This is your child's shelf").
   Onboarding is five steps: language, story languages, first books, listen, your voice. It ends straight on the locked shelf.

One shelf with a lock
4. No Child's shelf button and no Grown-ups button at the top. The kids' shelf and the grown-ups' corner are one screen.
5. Bottom menu: Books · Store · Settings · Lock, lock bottom right. From 600 wide the same items sit in the rail, the lock a
   plain row at its foot (Ctrl+L on Windows).
6. Books shows the grown-up view; tapping a book still reads it.
7. Lock icons plain, no background. Locked: a simple closed lock in ink. Unlocked: a bigger, clearly open lock in brand
   purple. Never black or red backgrounds.
8. Locked by default, including after every app start. While locked only choosing a book to read and changing the reading
   language work.
9. Store and Settings are faded while locked. Tapping one shows "Store and Settings are for grown-ups. Tap the lock to
   unlock." and the lock pulses.
10. Locked books have no ••• menu.
11. Tapping the lock while locked opens today's unlock popup (hold 3 s, then the sum). Its button says Cancel.
12. Tapping the lock while unlocked locks at once, no question.
13. Auto-lock after 30 seconds without a grown-up tap. Grown-up taps reset the timer: Store, Settings, a book's ••• menu,
    anything inside them. Reading, turning pages, changing the reading language and leaving the phone untouched do not.
14. When it locks: any open sheet or sub-page closes, the app goes back to Books, and "Locked again" shows.
15. While unlocked an "Unlocked · 24s" countdown chip (light purple) sits next to the title.
16. The store's own buying sheet and a recording in progress pause the 30-second timer.

Decided by the desk (2026-11-19, this session), from the mock and the code:
- The lock message is "Locked again: no grown-up taps for 30 seconds." after the timer and "Locked." after a tap (mock L7).
- The pulse is amber for 2 seconds; with reduce motion there is no pulse (mock L2).
- Going to the background still locks (existing `GrownUpMode.Leave()` behaviour). The store's buying sheet is the one
  exception already handled by the purchase code; item 3 checks it is not treated as leaving the app.

## Copy (refined against `AppResources.resx`)

Existing keys are reused where the text means the same, so their 14 translations stay. New and changed keys get all 14
languages machine-drafted (the resx doc comment's rule) and a native read in `docs/plan/unverified.md`.

| Screen | Key | English | State |
| --- | --- | --- | --- |
| L1-L8 header | `Gallery_Title` | My stories | existing |
| Tab bar / rail | `Corner_TabBooks` | Books | existing |
| Tab bar / rail | `Store_Title` | Store | existing |
| Tab bar / rail | `Settings_Title` | Settings | existing |
| Lock, locked | `Lock_Locked` | Locked | new |
| Lock, unlocked | `Lock_Unlocked` | Unlocked | new |
| Lock, screen reader (locked) | `Lock_LockedA11y` | Locked. Tap to unlock. | new |
| Lock, screen reader (unlocked) | `Lock_UnlockedA11y` | Unlocked. Tap to lock. | new |
| L4-L6 chip | `Lock_Countdown` | Unlocked · {0}s | new |
| L2 hint | `Lock_GrownUpsHint` | Store and Settings are for grown-ups. Tap the lock to unlock. | new |
| L3 popup title | `Gallery_ForGrownUps` | For grown-ups | existing |
| L3 popup hold | `Gate_HoldDescription` | Press and hold for 3 seconds | existing (canvas changed from "Hold for 3 seconds") |
| L3 popup button | `Common_Cancel` | Cancel | existing (was `Gate_BackToStory`) |
| L7 after the timer | `Lock_LockedAgain` | Locked again: no grown-up taps for 30 seconds. | new |
| L7 after a tap | `Lock_LockedByHand` | Locked. | new |
| WL2 rail line | `Lock_TapToLock` | Tap to lock. Locks itself in {0}s without a grown-up tap. | new (canvas "22 s" made "22s", like the chip) |
| WL1-WL2 rail | (constant, not translated) | Ctrl+L | new constant |
| L8 sheet | `Book_ReadNow` | Read now | existing |
| L8 sheet | `Record_Title` | Record your voice | existing (canvas changed from "Record voices", as shipped) |
| L8 sheet | `Gallery_Reorder` | Reorder | existing (canvas changed from "Move on the shelf", as shipped) |
| L8 sheet | `Book_Remove` | Remove from the shelf | existing |
| Reorder hint | `Gallery_ReorderHint` | Drag a book to a new place on the shelf. | changed (was "…Your child's shelf shows the same order.") |
| NO1-NO7 bar | `Onboarding_StepOf` | Step {0} of {1} · {2} | existing (count becomes 5) |
| NO1 | `FirstRun_Hello` | Hello! Which language should Dino use? | existing |
| NO1 | `FirstRun_HelloBody` | For the app's buttons and words. You can change it later in Settings. | changed (was "…in the grown-ups' corner.") |
| NO4 | `Onboarding_ListenTitle` | Listen to Dino read | existing |
| NO4 | `Onboarding_ListenBody` | Every character has its own voice, and Dino reads with feeling. | existing |
| NO4 | `Common_Continue` | Continue | existing |
| NO5 | `Onboarding_VoiceTitle` | Be the voice of a character | existing |
| NO5 | `Onboarding_VoiceBody` | Record {0}'s lines on this page, then hear the page with **you** as {0}. You can skip this. | changed |
| NO5 | `Onboarding_StaysTitle_Phone` | Your voice stays on this phone | existing (tablet/device variants too) |
| NO5 | `Onboarding_StaysBody` | It is never uploaded, not to us and not to the cloud. This try is deleted when setup ends. | existing |
| NO5 | `Onboarding_MicAsk` | Dino will ask for the microphone. | existing |
| NO5 | `Onboarding_RecordLines` | Record {0}'s lines | existing |
| NO5, NO6 | `Onboarding_SkipStep` | Skip this step | new |
| NO7 | `Onboarding_ThatsYou` | That's you! | existing |
| NO7 | `Onboarding_ThatsYouBody` | That's {0} in your voice. This was just a try, so it's deleted when setup ends. You can record any character later from a book's ••• menu. | changed (was "…in the grown-ups' corner.") |
| NO7 | `Tour_StartReading` | Start reading | existing |

Removed keys (in the neutral resx, all 14 language files and `AppResources.cs`): `Onboarding_ListenNext`, `Onboarding_TapLanguage`,
`Onboarding_ListenOther`, `Onboarding_ListenWide`, every `Tour_*` except `Tour_StartReading`, `Corner_ChildsShelf`,
`Gallery_GrownUpsPill`, `Gallery_GrownUpsButton`, `Gallery_GrownUpsButtonLocked`, `Gallery_BackToShelf`, `Gallery_ReadyForChild`,
`Gallery_CornerTitle`, `Gallery_CornerLine`, `Gallery_CornerLineShort`, `Gallery_CornerLineClick`, `Gate_BackToStory` — each only once
a grep shows no remaining use.

## Work

- [ ] 1. **Lock clock (Core, pure).** `LockClock` in `DinoReader.Core`: `Locked` by default; `Unlock()`, `Lock(LockReason)`
  (`Timer` or `ByHand`), `GrownUpTap()` resets to 30 s, `Hold()`/`Release()` (counted, for the buying sheet and recording),
  `Tick(elapsed)` locks at 0 with reason `Timer`, `SecondsLeft`. Unit tests cover reset, hold, release, both reasons, and that
  ticks while held do not count.
- [ ] 2. **One tab bar.** Phone (< 600): bottom bar Books · Store · Settings · Lock (lock last, right). From 600: the cream rail
  with the same items and the lock as a plain row at the rail's foot (WL1/WL2), on Android, iOS and Windows. Remove the ink
  corner header, the coral Child's shelf pill and the shelf's Grown-ups button; Store and Settings show under the shelf's
  header (icon, title, reading-language button). Lock icons per decision 7 (locked 24 ink; unlocked 30 brand purple, shackle
  lifted; rail 26), no background.
- [ ] 3. **Locked behaviour.** Locked at start and after the background. Store and Settings at 38 % opacity; a tap shows the
  dark hint `Lock_GrownUpsHint` over the lock and pulses it amber 2 s (no pulse with reduce motion). Covers have no •••; a tap
  reads. Reading-language button works on the shelf and the reading page. Book options, reordering, recording, buying,
  restoring and every setting are unreachable.
- [ ] 4. **Unlock and lock.** Lock tap while locked opens the gate (bottom sheet on phones, centred card from 600), button
  Cancel. Success unlocks: covers get •••, Store and Settings open, chip `Lock_Countdown` (light purple) next to the title on
  Books, Store and Settings, counting down. Lock tap while unlocked locks at once with `Lock_LockedByHand`. Windows Ctrl+L does
  the same as a lock tap; Ctrl+1/2/3 and Ctrl+R as in **Replaces**.
- [ ] 5. **Auto-lock.** A 1-second tick drives `LockClock`. Grown-up taps (Store, Settings, a ••• menu, anything inside them,
  sub-pages included) call `GrownUpTap()`; reading, page turns, the reading-language button and no touch do not. The store's
  own buying sheet (`BookPurchases.BuyAsync` and the subscription buy) and `RecordingAudio.IsRecording` hold the clock until
  they finish. On lock: close any open sheet and sub-page, go back to Books, show `Lock_LockedAgain` (timer) or
  `Lock_LockedByHand` (tap) as a short dark message.
- [ ] 6. **Onboarding, five steps.** Drop the `Corner` step and `CornerTour` (and its start in GalleryPage). Listen (NO4): one
  page in Dino's language, speaker chip, no language bar or bubble; Continue dim while it plays, wakes when the page ends.
  Your voice: "Skip this step" (brand text button) at the bottom of NO5 and NO6; it deletes the try
  (`OnboardingTryStore.DeleteAsync`) and finishes setup. NO7's button "Start reading" finishes setup. Setup ends on the locked
  shelf (NO8) with no card. Saved step values from an older install that point at the removed steps resume at the next
  existing step.
- [ ] 7. **Copy.** Every key in the Copy table in the neutral resx, the 14 language files (machine-drafted) and
  `AppResources.cs`; removed keys removed once unused.
- [ ] 8. **Test hooks and flows.** `enter-grown-up` becomes `unlock` (keep the old name as an alias so older flows still run),
  add a Debug-only `lock-seconds <n>` hook to shorten the 30 s for flows. Update every `.maestro/dino` flow that drives the
  corner, the Grown-ups button, the gate or the tour (the onboarding, store, settings, subscription, paid-books, corner and
  store-shot flows); add `lock.yaml` (L1, L2, L3 Cancel, L4 chip, L7 timer lock with `lock-seconds 3`, tap lock) and update
  the onboarding flows to five steps with a Skip path.
- [ ] 9. **Android and iOS.** All of the above is shared MAUI code; check the iOS build compiles (`net11.0-ios`) and that the
  iOS buying sheet and recording hold the clock like Android's.

## Verification

- `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj` (LockClock, OnboardingFlow count 5, step resume).
- Android emulator: `lock.yaml`, the onboarding flows (with and without Skip), a store and a settings flow, phone and
  600-wide landscape.
- Windows VM: WL1/WL2 rail, Ctrl+L, Ctrl+1/2/3.
- iOS: compile in CI; device checks go to *Not yet verified* tagged `[cloud]`/`[owner]` as the harness allows.
