# Slice 360 — Dino: Play on mobile data sits silent on "Waiting for Wi-Fi…"
**Product:** Dino

*Built 2026-11-13, not yet verified on a device; merged as PR #1063 (4427e6a1).*

**Touches:** `src/DinoReader.App/ShelfAudioPacks.cs`, `src/DinoReader.Core/ShelfAudioNetwork.cs`, `src/DinoReader.Core/ShelfAudioPlan.cs`, `src/DinoReader.Core/PackRepair*.cs` (new), `src/DinoReader.App/PackRepair*.cs` (new), `src/Sirous.Reading.Application/Usage/FallbackKind.cs`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.App/Testing/*`, `.maestro/dino/*` (new reading-sound flows), `src/DinoReader.App/ShelfAudioAsk.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/AudioPackStore.cs`, `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/App.xaml.cs`, `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.App/StoryLanguagesPage.cs`, `src/DinoReader.App/IntroWizardPage.cs`, `src/DinoReader.Core/AudioPackDue.cs`, `.github/scripts/publish-store-books.mjs`, `worker/stories.ts`, `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/DinoReader.Core/UsageEvent.cs`, `worker/events.ts`, `worker/events.test.mjs`, `src/DinoReader.App/Platforms/Android/AndroidManifest.xml`, `tests/DinoReader.Tests/*`, `worker/stories-audio.test.mjs`, `src/DinoReader.App/ShelfAudioPacksCard.cs`, `src/DinoReader.App/SoundStatusPill.cs`, `src/DinoReader.App/SoundStatusPillState.cs`, `src/DinoReader.Core/InstalledShelfAudio.cs`

## Why

On Monday 2026-11-09 the user, away from Wi-Fi in a restaurant on mobile data, opened a book already on the shelf
to show a friend. Play showed a Wi-Fi message and nothing was read. On the restaurant Wi-Fi it played at once.

What three sources show (2026-11-13):
- **Code** (`ground-in-code`, origin/main): no error says "connect to Wi-Fi". The reading page shows the status pill
  `ShelfAudio_WaitingWifi` "Waiting for Wi-Fi…" (`ReadingPage.xaml.cs:1173`) while the book's audio pack for the
  current language is not on the phone. Launch and app-return pack runs are quiet and never allow mobile data
  (`ShelfAudioPacks.cs:82, 241, 424, 620-647`, `ShelfAudioNetwork.cs` `NeedsWifi`); only the grown-up's "Download now"
  answer in the add-a-language dialog (`ShelfAudioAsk.cs:23-25`) allows it. The wait reports **no** usage or fallback
  event, only a local log line. There is no setting to allow mobile data.
- **Emulator** (`device-test`, released `dino-v1.124.1`, mobile data only, 2026-11-13): a book played before and a book
  never played both read at once (their audio came with the book). After switching the app language to French, the same
  book showed `En attente du Wi-Fi…` and played silent (`[AudioPack] silent piece shown ... no_pack`). **Reproduced.**
- **Logs** (`analytics-query`, dino_usage): 2026-11-09 20:25 UTC, Android 1.123.2, GB, three launches within a minute:
  `book_opened`, `audio_pack_download failed:missing` x2 and `premade_audio:clip_missing` x5. Likely the user (time and
  place fit, repeated tries), not provable: no device id. If so, the pack the app wanted was missing on the server, and
  the app still showed a Wi-Fi wait. Four Dino releases were cut that day (1.123.0-1.123.3).

**Why the rule didn't save it** (`ground-in-code`, origin/main, 2026-11-13). The user's rule is Slice 267 line 5: every
book downloads its audio in all family languages at install, for books on the shelf, when a language is added, and in the
update check. Getting a book does that at once on any network (`StoreClient.cs:247-256`). The gaps come later:
- A failed pack is retried only by the weekly book update check (`BookUpdater.cs:426-443`, Slice 181's "at most once every
  7 days"), and a failed pack stays failed for the rest of the launch after two tries (`AudioPackStore.cs:565-569`).
  Slice 246's review flagged "failed pack ignored until the weekly check" as High and it was skipped as "by design" by the
  building session, without the user.
- A 404 (`failed:missing`, `StoreClient.cs:427-431`, Worker `stories.ts:553-558`: the listing doesn't offer that version)
  is not counted as a failure in the weekly sync (`StoreClient.cs:372-376`), so it is never retried.
- Launch and app-return runs never use mobile data (`ShelfAudioPacks.cs:241, 620-647`). That Wi-Fi rule came from Decision
  120 (the 100+ MB storyteller voice pack) and was carried over to the small per-book packs in Slices 278/285 without the user.
- Likely trigger on 11-09: commit `b60cca23` (08:54 UTC) re-rendered The Three Little Pigs in all 15 languages, so its pack
  versions changed; a phone with an older listing asked for a version the Worker no longer offers (404). Not proven: the
  book id in the 11-09 rows settles it (first work item below).

## Decisions (user, 2026-11-13)

1. **A missing or failed pack is retried every hour** while the app runs (and at the next launch), never weekly. Any
   failure counts, a 404 included: a 404 first refreshes the store listing, then retries with the listed version.
2. **Opening a book whose audio is missing** (in any family language) retries its packs at once.
3. **Mobile data is fine for every book audio download**: no Wi-Fi wait for book packs anywhere (launch, app return,
   language added, update, retry). The "Download on mobile data?" question for book audio goes. Decision 120's Wi-Fi rule
   stays only for anything large that is not a book pack, if any remains.

## Design: pack repair state (coordinator, wave 2026-11-13)

Stateful work, so the design is fixed before the build:
- **One owner.** A new App service `PackRepair` (singleton) owns the retry state; `AudioPackStore` keeps downloading and reports each
  outcome to it. The weekly `BookUpdater` check no longer decides retries for book packs.
- **State.** Per pack key (book id, language): `LastAttemptUtc` and `LastOutcome` (`Ok`, `Failed`, `Missing404`, `Offline`), in memory only.
  A pure Core function `PackRepairDue.IsDue(entry, now)` decides: never tried this launch -> due; failed under 60 min ago -> not due; 60+ min -> due.
  Unit tests: never tried, failed 59 min ago, failed 61 min ago, 404 then a new listed version (a new version is a new attempt, due at once).
- **Triggers.** App launch (every missing pack is due), an hourly tick while the app is in the foreground, app return (due check only), opening a
  book (that book's missing packs are due at once, ignoring the hour), and the network coming back (due check only).
- **Interrupts.** Killed: nothing persisted, the next launch tries everything missing. Offline: the attempt ends as `Offline`, the reading page shows
  SS2/SS4, and the network-back trigger retries. A 404: refresh the store listing once, then retry with the listed version; still 404 -> `Missing404`,
  the page shows SS3, retried at the next hour. No cancel: runs are quiet. Back-off inside the hour: one attempt per pack per trigger, never a loop.
- The per-launch two-failure cap in `AudioPackStore.cs:565-569` goes for book packs (the hourly due check replaces it).
- Mobile data allowed for every book pack (Decision 3); `NeedsWifi` keeps answering only for the large storyteller pack, if still used.

**Revision after review (second round, 2026-11-13):**
- `PackRepairLog` holds entries only for packs a run actually tried (book, language, listed version) with `LastAttemptUtc` + outcome. At each tick (and each network-back event)
  entries whose pack is now installed, or that no longer need a pack (no listing yet, no pack by design, language removed or cancelled), are removed. `AnyDue` = an entry with outcome
  Failed/Missing404/Offline whose last attempt is 60 min or more ago, so nothing re-requests forever.
- Tick every 5 min: `AnyDue` -> `requestRun` (ordinary scope; the due check per pack stays `PackRepairDue`). Connectivity: only a change TO `NetworkAccess.Internet` (Local and
  ConstrainedInternet are ignored), and only when some entry's last outcome is Offline -> a run with `NetworkBack` (Offline entries due at once, the hour ignored); the 20 s debounce is
  stamped when the run is actually requested. A run cancelled while waiting offline records no Offline outcome: the entry keeps (or regains) its earlier outcome.
- Every timer and connectivity callback wraps `requestRun` in a try/catch that reports `FallbackArea.Pack` / `PackSyncFailed`. `StartTicking` runs even when `shelfAudio.Resume` throws.
- No listing read yet is not "missing": opening a book does not force a network run for it. The reading page checks the installed pack first (no pack warning, no
  `pack_missing_on_play`, when the pack is on the phone), and reports `pack_missing_on_play` only for the NoInternet and Failed states.

**Expectation rule after review (third round, 2026-11-13; supersedes every "no pack by design / no listing" branch above):** "is a pack expected" is one pure Core function, `PackExpectation.For(knownListedItem, liveListingRead, isStoreBook, language, bookOwnLanguage, mainLanguage)`, used by the reading page's view, `ShelfAudioPacks.MissingExpectedLanguages` (the open-book trigger), `StillNeedsPack` (the repair log's prune) and nothing else.
- Known listing = this launch's when a live listing was read this launch (a book it leaves out has no known listing, whatever the saved copy says); the last good store listing saved on the phone (`store-listing.json` in `FileSystem.AppDataDirectory`) is used only when no live listing was read. It is written off the UI thread by `StoreClient.ListAsync` (unique temp file, then moved over), loaded once per launch; a file that fails to load, parse or validate is no saved listing (reported as a fallback, and deleted when it does not parse). `KnownListing` in Core.
- The book's own language is never Expected. Known listing -> Expected iff it lists a pack for the language. A live listing read this launch that does not list the book -> NotExpected (nothing is coming; `StoreClient.LiveListingRead`). A live listing is saved over the last good one only if it reads back with at least one readable book (`KnownListing.IsSavable`). No listing at all known (none read this launch, none saved) -> Expected iff it is a store book and the language is not the book's own language; with the own language unknown (taken from the book on the phone, else its listing entry), the child's main family language is not Expected. Languages compare by `BundledTranslations.Key`. `StillNeedsPack` asks the same `ExpectationFor`.
- Page (`PackSoundView.ForPack`): installed or NotExpected -> nothing; Expected and missing -> SS1 while a try runs, SS2/SS4 offline, SS3 failed/404, and with no outcome yet SS1 at 0% while the open-book run starts.
- Opening a book: Expected and missing in any family language -> `OnBookOpened` always, even with no listing; offline it records an Offline entry (so the network-back trigger retries), a run that cannot read the listing while online records Failed.
- Also: Prune removes an entry only if unchanged since its snapshot; a listing refresh that fails while offline records Offline, not Missing404; an Ok entry is never counted by the tick and, if the pack is wanted again, is a first try.

## Work

- [x] **Hourly retry and retry on open** (Decisions 1-2): a pack-repair run separate from the weekly book update check;
  remove the per-launch two-failure cap for it (keep a back-off inside the hour); a 404 refreshes the listing and retries.
  Due-check logic in Core with tests (never tried, failed 59 min ago, failed 61 min ago, 404 then new version).
- [x] **Mobile data everywhere for book packs** (Decision 3): drop the Wi-Fi wait in `ShelfAudioPacks`/`ShelfAudioNetwork`
  for book packs and the `ShelfAudioAsk` question on `LanguagePage`, `StoryLanguagesPage`, `IntroWizardPage`; remove the
  `ShelfAudio_WaitingWifi` pill (the mock supersedes "if nothing else uses it": frame 198's pill goes). Update the rule in `src/DinoReader.App/CLAUDE.md`.
- [x] **Keep old packs on the server** until phones have moved: `publish-store-books.mjs` leaves the previous pack version
  in R2 and the Worker serves a version one behind the listing, so a phone with a slightly old listing never gets a 404.

- [x] **Play never waits silently.** When the reader opens a book whose pack for the reading language is missing, it
  downloads at once on any network (Decision 2) and shows the download's progress, not a Wi-Fi wait.
- [x] **Say what is really wrong** (the mock, Design SS1-SS4, supersedes the earlier wording). Four states under the words
  card, each a pill or card, never a pop-up, and the words keep showing and highlighting: downloading `Reading_GettingBookSound`
  "Getting {0} sound for this book · {1}%"; no internet `Reading_NoInternetSound` "No internet, so this book's {0} sound can't
  download yet." plus the button `Reading_ReadIn` "Read in {0}" when another family language's sound is on the phone; pack missing
  on the server or failed `Reading_SoundFailed` "Couldn't get the {0} sound. Dino will try again soon." (shortened for a child's
  page; not Slice 357's per-status text) plus the same button; nothing to switch to `Reading_NoInternetWordsShow` "No internet. The words
  still show, and the sound comes once you're back online." with no button (also used for a server problem when there is no other
  language). The `ShelfAudio_WaitingWifi` "Waiting for Wi-Fi..." pill (frame 198) goes, not "if nothing else uses it". Strings in every
  `AppResources.<lang>.resx`; `{0}` is the language's name in its own script.
- [x] **Log it.** A fallback `premade_audio:waiting_wifi` and `premade_audio:pack_missing_on_play` (closed reasons,
  `FallbackReason` + `worker/events.ts` `FALLBACK_REASONS` + test), so this case shows in the data next time.
- [x] **Find the missing pack.** From the 11-09 rows (book id in `blob` fields) list which book and language returned
  `failed:missing`, check that pack exists in R2 now, and record the answer here. If a release or store upload can leave
  a listing pointing at a pack that isn't uploaded yet, fix that order in `publish-store`.
  Answer (`analytics-query`, 2026-11-13; findings in `reviews/s360-missing-pack.md`): book The Three Little Pigs
  (`966400017c494fb5b045786f53852c9e`), en and fa each `audio_pack_download failed:missing` x2, Android 1.123.2, GB,
  2026-11-09 20:15-20:40 UTC. The 5 `premade_audio:clip_missing` rows carry book `none`. The version asked is not recorded
  (blob2 book, blob3 lang, blob9 outcome have no version column). Both packs are live now: en v2 (1514197 bytes), fa v3
  (1619564 bytes), book v15; `GET /api/stories/<id>/audio/en?v=2` and `/audio/fa?v=3` both return 200. Likely, not proven:
  the 11-09 listing pointed at a pack version not served, which fits commit `b60cca23` re-rendering the book that morning.
  `publish-store-books.mjs` already never deletes old packs, and the Worker now serves one version behind the listing.
- [ ] **iOS twin:** the same code is shared (`ShelfAudioPacks`, `ReadingPage`); verify the iOS cellular check
  (`ShelfAudioNetwork`) on a TestFlight build. Code shared, not compiled or run on iOS after the last fixes (see Not yet verified).
- [x] **Side crash found on the run:** holding the grown-ups' 3-second press crashed 1.124.1 once with
  `PermissionException ... android.permission.VIBRATE` (log `D:\ReadTheStupidText-worktrees\reviews\dino-1.124.1-cellular-crash.txt`).
  Add the permission to `AndroidManifest.xml` or drop the haptic; check iOS needs nothing.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`; `node --test worker/events.test.mjs`.
- Emulator, mobile data only, released build: change the language, open a book, it downloads and reads (the pill shows
  "Getting {0} sound for this book · n%" and leaves with the first clip); airplane mode shows `Reading_NoInternetSound` with
  `Reading_ReadIn` "Read in English"; a Debug 404 shows `Reading_SoundFailed`; one family language shows `Reading_NoInternetWordsShow`
  with no button.
- Emulator: the grown-ups' hold no longer crashes.
- Emulator, mobile data only: add a language with no question asked, every shelf book's pack for it downloads; a
  Debug hook forcing a 404 or network failure retries within the hour and again on opening the book.
- After release: `premade_audio:waiting_wifi` and `pack_missing_on_play` counts in dino_usage.

#### Design (Dino Reader 2026-11-13 Missing States mock, section 3, added 2026-11-13)

Four frames (SS1 getting the sound, SS2 no internet, SS3 problem on the server, SS4 nothing to switch to), all the same Farsi reading
page of The Three Little Pigs in picture-and-text view on a phone (405 x 864; bezel and status bar are canvas furniture), so only
the strip under the words card is new. The mock agrees with Decisions 1-3 and removes frame 198's Wi-Fi pill. The picture in the
frames is a stand-in. Text keys: `docs/plan/notes/2026-11-13-dino-copy.md` (*Missing States*, SS1-SS4). Hex as in the mock (use the
`DinoTokens` key with the same value, else add one). Fredoka (Vazirmatn for the Farsi words). Layout never mirrors
(`src/DinoReader.App/CLAUDE.md`): the strip and the button sit the same in right-to-left languages; only text runs right to left.
- **Page above the strip (existing reading page):** title row (back 44 x 44, flag pill 46 high radius 16 `#F0E8D8`, view switch: three
  segments 44 x 40, radius 13, the current one white with shadow 0 1 4 rgba(43,37,64,.16) and `#5B57E8`), book title 20 / 600 (Vazirmatn 1.4),
  stage radius 18 `#F0E8D8` with the words card (left/right/bottom 8, `rgba(255,255,255,.95)`, radius 16, padding 10 0, Vazirmatn
  20 / 1.75; the read sentence on `#FFE27A`). Controls row (previous 64 round `#ECE7FF`, Play 84 round `#FF7A59`, next 64 round,
  gap 28, padding 12 0 30).
- **SS1, getting the sound:** the strip row is centred, padding 12 14 0. A pill: min-height 36, padding 7 14 7 9, radius 18, fill
  `#ECE7FF`, text `#3f3bc4`, 14 / 500, line 1.3, gap 8, the filling ring (Slice 290's `197` pill, now per book) then
  `Reading_GettingBookSound` ("Getting فارسی sound for this book · 40%"). Opening a book whose sound is missing starts the download at once, Wi-Fi or
  mobile data. The pill leaves when the first clip plays.
- **SS2, no internet:** padding 10 14 0 (centred). A sand card: radius 18, fill `#F0E8D8`, text `#3f3a52`, padding 10 14, gap 8, column. Line: a
  cloud-off glyph then `Reading_NoInternetSound` 14 / 500, line 1.35. Under it, centred, the button `Reading_ReadIn`: height 40, padding 0 14 0 6, radius 20,
  white, ink `#2b2540`, 14.5 / 600, gap 7, shadow 0 2 0 rgba(43,37,64,.08), a 28 round flag (ring 1.5 rgba(43,37,64,.12)) of the other language. It switches this
  book's reading language for this reading only and starts from this page. Dino tries the missing sound again by itself when the phone is back online.
- **SS3, problem on the server:** the same card shape and button with a warm fill `#FFF0EA`, text `#A4462A`, an alert glyph, text `Reading_SoundFailed`. The hourly retry and
  the retry on opening run quietly behind it.
- **SS4, nothing to switch to:** the sand card of SS2 with the single line `Reading_NoInternetWordsShow` and **no button** (one family
  language, or no other language's sound on the phone). On a server problem with nothing to switch to the line is SS3's, also without the button.

New strings (all 15 `AppResources.<lang>.resx`): `Reading_GettingBookSound`, `Reading_NoInternetSound`, `Reading_ReadIn`, `Reading_SoundFailed`,
`Reading_NoInternetWordsShow`. `ShelfAudio_WaitingWifi` and its translations go.

**Plan changed to match the mock (2026-11-13):**
- "Say what is really wrong" now names the five keys and the four states; the server-problem text is `Reading_SoundFailed`, not Slice 357's per-status message.
- Frame 198's Wi-Fi pill (`ShelfAudio_WaitingWifi`) is removed outright instead of "if nothing else uses it".
- The download progress is shown in a tint pill with the percent (SS1), per book.
- "Read in {0}" is a flag button on the card, per reading only (not a stored choice).
- **Rules in `src/DinoReader.App/CLAUDE.md` this changes (past design choices, in Touches):** "Nothing on the child's side starts or offers a
  download" and "the reading page starts no fetch" (Slice 173, 285): opening a book now starts the book's missing pack (Decision 2), and the
  language-change rule's `IntroWizardPage` wording goes with Slice 362. The honoured-only-if-installed rule for the child's pick stays: "Read in {0}" appears only when
  that audio is on the phone. No hard rule (privacy, store, parity) clashes: iOS and Android share the code.

*Changed while building:* the `premade_audio:waiting_wifi` fallback exists but has no emitter (the Wi-Fi wait is gone), so its post-release count will read zero; `pack_missing_on_play` is the live one. Debug hooks added: `force-pack-failure`, `pack-repair`, `remove-audio-pack`. The saved store listing (`store-listing.json`, `KnownListing`, `PackExpectation`) came from review rounds 3-5, see the Expectation rule above. Device flows (emulator-5554, logs in `D:/ReadTheStupidText-worktrees/reviews/w53-device/s360-run1/`, `s360-run2/`, `s360-run3/`): `dino-landscape-gate` PASS (hold, no VIBRATE `PermissionException`); `reading-sound-getting` and `reading-sound-retry-on-open` PASS (run 2, cache ae67c7099028); `reading-sound-no-internet` and `reading-sound-failed-404` PASS (run 3, cache af6412669b6c). Earlier failures were flow setup (pack already installed at launch, a forced network failure blocking the real install, a log marker the app never wrote), not app bugs.

*From review:* `reviews/s360-check6.md` (check pass 6 on the remove-audio-pack hook, the OnTick log lines and the four reading-sound flows): no findings.
- Skipped #9 (`reviews/s360-high.md`): "Convention and dead code - LOW" (long `ShowAudioStatus`, magic strings in `PackRepairHook`, `SoundStatusPillState.From`, Preparing/WaitingForInternet pill kinds, `RunIncludes`, `Reading_GettingSound`, `FallbackReason.WaitingWifi`) - style only; `waiting_wifi` is kept because the Log item names it, and it has no emitter now (the Wi-Fi wait is gone), so its Verification count will read zero.

- Skipped #8 (`reviews/s360-medium.md`): "Unrelated VIBRATE permission (grown-ups' 3-second press haptic) added; not in the slice's Decisions or SS1-SS4" - it is this slice's own work item "Side crash found on the run"; iOS needs no permission for haptics.
- Skipped #9 (`reviews/s360-medium.md`): "list folders synchronously (InstalledVersion per family language) on the UI thread in ReadingPage.OnAppearing" - small folder listings, no measured stall; recorded under Not yet verified.
- Skipped #10 (`reviews/s360-medium.md`): "servesAudioVersion serves only the single highest older held version" - the slice decides one version behind ("the Worker serves a version one behind the listing").

- Applied (`reviews/s360-high.md`): #1-8. Applied (`reviews/s360-medium.md`): #1-7. Applied (`reviews/s360-check.md`): #1-9. Applied (`reviews/s360-check2.md`): #1-8. (Per the caller's triage, not re-verified one by one by the third-round fixer.)
- Applied (`reviews/s360-check3.md`): #2, #3, #4, #5, #6 (a saved file that does not parse or validate is no saved listing and is deleted), #8, #9.
- Applied (`reviews/s360-check4.md`): #1 (a live listing read this launch that does not list the book -> NotExpected; the "store book, no listing" branch applies only when no listing at all is known), #3 (a live listing with zero books, or one that fails `KnownListing.ParseSaved`'s validation, is never saved over the saved one).
- Skipped #2 (`reviews/s360-check4.md`): "by-id ExpectationFor reads own language from _ownLanguages (filled only by the Story overload); with no listing and a store book written in a non-main family language, StillNeedsPack/repair can run before the Story overload and treat the own language as Expected (download attempted, logged failed). Needs offline-first-launch plus no listing, so mostly self-limiting." - a repair-log entry exists only after a run planned from a listing, so the offline-first-launch case has no entry for the by-id check to keep.
- Skipped #1 (`reviews/s360-check3.md`): "stale saved listing naming no pack for a language gives NotExpected, no card, no repair" - offline with a saved listing older than a newly published pack: the next online launch refreshes the listing; recorded under Not yet verified.
- Skipped #7 (`reviews/s360-check3.md`): "possible hitch on slow storage" - small folder scans and one small file; already under Not yet verified for jank.
- Skipped #1 (`reviews/s360-check5.md`): "a book pulled from the store after the first live read stays Expected; its card stays getting/failed until relaunch" - a book pulled mid-launch keeps its card only until the next launch, and the listing is read once per launch by design; recorded under Not yet verified.
- Skipped #2 (`reviews/s360-check5.md`): "the skipped book becomes NotExpected (no card, no fetch)" - skipping a malformed book entry is how the listing parser already behaved before this slice (`StoreClient` parse), and a malformed entry gets no fetch either way; recorded under Not yet verified.

## Not yet verified

- Follow-up: book pulled mid-launch keeps its card until relaunch (check5 #1, `reviews/s360-check5.md`).
- Follow-up: a malformed listing entry gets no card (check5 #2, `reviews/s360-check5.md`).

- Offline launch with a saved listing older than a newly published pack: the child opens the book in that language, sees no card until the next online launch refreshes the listing (check3 #1).

- Check no jank opening a book on a slow phone (the pack-folder listing on the UI thread in `ReadingPage.OnAppearing`, review #9).
- iOS compile after the last fixes, and a TestFlight run of the cellular pack download and the reading-page states.
- Real airplane mode on a phone (the flows force offline by a Debug hook).
- Add a language on mobile data only: every shelf book's pack for it downloads with no question.
- After release: `premade_audio:waiting_wifi` (no emitter now, expect zero) and `pack_missing_on_play` counts in dino_usage.
- Worker serving one version behind, live, after the batched release deploys.

#### For CLAUDE.md (lifted 2026-11-13)

- Book audio packs download on any network, never wait for Wi-Fi, and `ShelfAudioAsk` is gone; `ShelfAudioNetwork.NeedsWifi` is for the large storyteller pack only. Failed or 404 packs are retried hourly and on opening the book by `PackRepair` (due check `PackRepairDue` in Core), never by the weekly `BookUpdater` check. "Is a pack expected" is only `PackExpectation.For` (Core). (Slice 360: docs/plan/slices/360-dino-play-waits-for-wifi.md)
- `publish-store-books.mjs` never deletes old pack versions; the Worker (`worker/stories.ts`) serves one version behind the listing. (Slice 360: docs/plan/slices/360-dino-play-waits-for-wifi.md)
- The grown-ups' hold haptic needs `android.permission.VIBRATE` in `AndroidManifest.xml`; iOS needs none. (Slice 360: docs/plan/slices/360-dino-play-waits-for-wifi.md)

#### For README.md (lifted 2026-11-13)

Nothing user-facing to add.
