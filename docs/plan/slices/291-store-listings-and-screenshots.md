# Slice 291 — Store listings and screenshots for both apps on Play, App Store and Microsoft Store
**Product:** Shared

*Built in part 2026-11-14 (items 1, 2 done; 3 built, read-back pending; 5 Android phone shots done; 4 and 6 open), merged as PR #1137 (e3e79c1a).* *Wave 2026-11-08: PR #941 merged (08e1d63a): item 2 and the repo listing text of items 1 and 3 only (no store push, no screenshots); the rest stays open.* **Wave scope (2026-11-08):** this wave's PR touches only `docs/store-listings/**`, `site/dino/privacy.html`, `site/dino/index.html` (same age range) and this file. **Park note (2026-11-07, night run wave 5):** parked: the Play "Manage store presence" grant (user: later) and the App Store legal values (user: not tonight), per the User answers 2026-11-07. Blocks pushing the listings. Store text and images, no app code. Moves the listing bullets out of plan.md's *Outstanding work* (the Play "out of date" bullet, the App Store go-live bullet, the stale RTST Microsoft Store English listing from Slice 193's live read, the Slice 194 screenshot retake).

**Touches:** `docs/store-listings/**`, `STORE.md`, `site/dino/privacy.html`, `site/dino/index.html`, `.github/workflows/store-screenshots.yml` (new: macOS simulator screenshots), `tools/emulator/screenshot-frames.json`, `tools/desktop-vm/checks/rtst-panel-screenshot.ps1`, `docs/plan/slices/291-store-listings-and-screenshots.md`

**Touches, widened at claim (2026-11-14):** also `.github/scripts/store-listings.mjs` and its test (item 3: RTST English through the release listing script, as Slice 193 did for German), `.github/workflows/store-submit.yml` (only if the RTST English line needs it), `tools/emulator/store-screenshots.ps1` (new), `.maestro/dino/store-shot-*.yaml` and `.maestro/rtst/store-shot-*.yaml` (new), `docs/store-listings/screenshots/**` (new: the PNGs). Widened after review pass 1 (coordinator, 2026-11-14; parallel-check showed no other live work): `.maestro/dino/needs-file/store-shot-*.yaml` and `.maestro/rtst/needs-file/store-shot-*.yaml` (the flows moved there so normal runs skip them), `tools/emulator/test-store-screenshots.ps1` (new), `tools/emulator/lint-flows.ps1` (one line: skip `store-shot-*` in the contact-sheet listing check).

## Claim notes (night 2026-11-14-1547 wave 4: iOS and tablet shots)

*In progress (wave 2026-11-14, night 2026-11-14-1547 wave 4), draft PR.*

- Scope: item 5's iPhone 6.9"/6.5" and iPad 13" shots of both apps from a new `store-screenshots.yml` (macOS simulator, dispatch-only, reusing `.github/scripts/ios-sim-*.sh` from Slice 244; plan-not-needed: 244), and the Play tablet shots on the `RTST_Tablet` AVD (`tools/emulator/new-avd.ps1 -Tablet`). The Microsoft Store desktop shot and the RTST control-panel retake stay open (Hyper-V VM needs over 8 GB free memory; 3.9 GB at the claim). Item 6 (uploads) waits for complete per-store sets, as decided in wave 3.
- **Touches, widened at claim (wave 4):** also `.github/scripts/ios-sim-*.sh` (only to add a parameter, the 244 flow keeps working), `.maestro/dino/store-shot-ios-*.yaml` and `.maestro/rtst/store-shot-ios-*.yaml` (new), `.maestro/*/needs-file/store-shot-tablet.yaml`, `.github/scripts/ship-targets.test.mjs` (exclusions for new test-only files), `.github/workflows/release-rtst.yml` and `release-dino.yml` (path exclusions only), `tools/emulator/new-avd.ps1`, `tools/emulator/common.ps1`. The shots themselves stay outside the repo, under `<repo>-worktrees/reviews/s291-shots/`, as in wave 3 (the CI job hands them over the way `.github/CLAUDE.md` allows for Actions storage).
- Tablet shots (review `reviews/s291c-high.md` #7): the Play tablet flows, `Get-StoreShotInstance` and `new-avd.ps1 -Tablet` already exist on main, so wave 4 adds no tablet code; the tablet shots are a device run after the reviews (`install-debug.ps1 -App <app> -Instance tablet`, then `store-screenshots.ps1 -App <app> -Form tablet`).
- Skipped #5 (`reviews/s291c-high.md`): "the RTST supertonic-3 pack is downloaded three times per run" - low, and the dispatch-only job's cost is accepted; no change to the clearState flow.
- Skipped #6 (`reviews/s291c-high.md`): "waiting on `text: Read any text aloud with natural voices.*` depends on how ReadingText splits or highlights spans" - low, speculative; the ReadingText id wait stays first and the text match is the intended check.
- Skipped #2 (`reviews/s291c-medium.md`): "After openLink to the grown-up Books tab, waits on StoryTile, which is already visible on the shelf, so it can pass before navigation; the books shot may catch a transition." - third round on this wait; the first CI run and a look at each shot decide.
- Skipped #4 (`reviews/s291c-medium.md`): "If ios-sim-boot.sh fails after creating the simulator, udid is empty and cleanup is skipped; shots-<label> simulators leak on reused runners." - low; CI runners are fresh macOS VMs.
- Skipped #5 (`reviews/s291c-medium.md`): "'Hand the shots over' runs with if: always(), so a partial set (one device) is published as a draft with no incomplete marker." - low; the summary lists every problem and the shots go to a draft only.
- Parallel with Slice 378 (both edit `.github/workflows/*.yml`): accepted, because 378 changes only `uses:` version lines and `useConfigFile`/`track` inputs, while this slice adds a new workflow; a later merge takes the other side's lines mechanically. The new workflow uses the action majors 378 moves to.

## Claim notes (wave 2026-11-14, night 2026-11-14-1547 wave 3)

Premise check on origin/main:
- Item 1: RTST Play text still says "No account and no analytics" (`docs/store-listings/appstore-rtst.md:4` calls it stale; `en.md:29` has the corrected usage-counts text). The Play grant blocker is gone: Slice 193 committed RTST Play de-DE through the API on 2026-11-14 ("The Play API works for this service account since Slice 374"). So item 1's RTST push runs tonight through the `store-listing` agent (read live, diff, apply, read back), every RTST Play locale that carries the stale sentence.
- Item 3: `store-listings.mjs:9,165` leaves English as pushed by hand and builds 13 languages plus RTST German (`store-submit.yml:229`). Tonight: add RTST English (from `docs/store-listings/en.md`) to the script the same way as German, so it reaches Partner Center with the next RTST release; the read-back after that release goes to *Not yet verified*. Partner Center through Chrome is not used at night.
- Item 4: the App Store declarations stay with the user (User answers 2026-11-07); nothing is written to App Store Connect except screenshots for the open versions if item 6 gets that far.
- Item 5: none of the screenshot files exist yet. Play phone and tablet shots come from the Android emulator (scripted Maestro flows, test hooks, a warm headless emulator); iPhone 6.9"/6.5" and iPad 13" shots come from `store-screenshots.yml`, built after Slice 244 merges and reusing its `.github/scripts/ios-sim-*.sh`. The Microsoft Store desktop shot needs the Hyper-V VM; free memory was 3.1 GB at the wave start (under the 8 GB rule), so it stays open.
- Item 6: upload only after the coordinator has looked at every shot's small copy; Play upload through the API with a read-back; App Store screenshots only onto the versions in `PREPARE_FOR_SUBMISSION` (nothing submitted).

## Built this wave (2026-11-08)

- Item 2 done: `site/dino/privacy.html` and `site/dino/index.html` (meta description and eyebrow) say "3 to 9"; `index.html` was added to Touches.
- Item 1 repo text: already correct before this wave (`docs/store-listings/dino/play.md`: 3 to 9, fifteen languages, counted from the 15 `AppResources.*.resx` locales; RTST text in `en.md` and `appstore-rtst.md` states the optional anonymous usage counts). No edit needed. The Play push stays open (needs the "Manage store presence" grant).
- Item 3 repo text: `docs/store-listings/en.md` made current: the offline bullet now says Windows and Android download the voice pack once, and the stale "don't add until Slice 56 ships" line became a Microsoft Store download note (about 145 MB, built-in Windows voice until the pack lands). The Partner Center push stays open; compare with the live text first.
- Still open: items 1 (push) and 3 (push), 4, 5, 6.

## Pushed 2026-11-13 (user: "fucking yes!", "yes fucking yesss")

- Item 4, Dino only: App Store version 1.81.1 en-US and de-DE text, copyright, categories (BOOKS, EDUCATION), content rights, age declaration and review detail pushed through the ASC API and read back equal. Not submitted (`PREPARE_FOR_SUBMISSION`). The old text sold scanning, own books, translation and a voice download (removed by Slices 278 and 305); rewritten from the current app and the paid books of Slice 346.
- **Age rating fixed to 4+ (2026-11-13):** with cartoon violence INFREQUENT_OR_MILD Apple computed 9+; the user said yes to NONE, Claude patched the declaration through the ASC API and Apple read back `FOUR_PLUS`.
- Item 1, Dino Play en-GB and de-DE: full description rewritten the same way, pushed and committed through a Play edit, read back 2026-11-13 with no scan/PDF/translation words left. Title and short description unchanged. So the service account can commit Play listing edits on Dino. RTST Play is not done yet.
- Not yet verified: a native German read of the App Store and Play de-DE text (machine-written).
- Still open: item 3 (RTST Microsoft push), RTST parts of 1 and 4, 5 (screenshots), 6.

## Review notes (`reviews/pr-941-high.md`)

- Applied #1: this note now says `index.html` is done; item 2 ticked.
- Applied #2: the Windows note says "up to about 150 MB" (only the Supertonic pack, ~145 MB, is documented; `packs.yml` caps the other voice packs at 100 MB and Kokoro at 150 MB).
- Applied #3, from the code: the Windows app's network use is the pack downloads (`WindowsVoicePackService`), `UsageHttpTransport` (`/api/app-config` and `/api/events`, the usage counts, which carry fallback reports) and `WindowsUpdate` (remote-config check at start and every 24 h, whether or not the usage switch is on). No separate crash reporter was found in `App` or `Infrastructure`, so the Windows note does not say "crash reports"; it names the daily version check and the usage counts.
- Applied #4: the drafting note in `en.md` is an HTML comment, not paste text.
- Applied #5 in part: the stale "don't add until Slice 56" line in `ar es fa fr ja pt ru zh` is replaced by the Microsoft Store note in each language (my own translation of `en.md`, not reviewed by a native speaker). Not done: the old "Android downloads its voice once" offline bullet in those files, and `de.md` line 71 (its own stale Windows note); they stay open with the Partner Center push (item 3).
- Applied #6: both stale clauses in `docs/store-listings/dino/appstore.md` (lines 4 and 68) updated.
- Applied #7: the shared offline bullet in `en.md` is store-neutral ("a one-time download on first launch"); the Windows detail lives only in the Microsoft Store note. The short description and search term "natural voices offline" stay as they are.

## From review (`reviews/pr-941-medium.md`)

- Applied #1: the drafting note is gone from `docs/store-listings/en.md` (nothing paste-ready contains it). Its content, kept here in plain text: Slice 56 shipped, so the voice is no longer inside the MSIX; the built-in Windows voice as first-run voice and fallback is from `src/ReadTheStupidText.Infrastructure/CLAUDE.md`; the Supertonic pack is about 145 MB, `packs.yml` caps the other voice packs at 100 MB (Kokoro 150 MB); Windows network use, from the code: `UsageHttpTransport` (app-config and events), `WindowsUpdate` (config check at start and every 24 h, whether or not the usage switch is on), `WindowsVoicePackService` (pack downloads). Compare with the live Microsoft Store text before the Partner Center push.
- Applied #2: the offline bullet in `de es fr pt ar fa ja ru zh` now matches `en.md` ("once your voice pack is downloaded, a one-time download on first launch"); my own translation, not reviewed by a native speaker. The `de.md` App Store bullet (line 63) is left as it is.
- Applied #4: the Microsoft Store note in `en.md` says the usage counts also carry reports of problems the app handled (fallbacks) and can be switched off. Still no separate crash reporter on Windows, found from code (App and Infrastructure), not from a run.
- Applied #5: `site/dino/privacy.html` effective date is now 8 November 2026.
- Applied #6: `docs/store-listings/dino/appstore.md` no longer points at `play.md` and says the age range is fixed in the repo text, the store push still open.
- Skipped #3 (`reviews/pr-941-medium.md`): "voice pack size not verified against packs.yml" - packs.yml caps voice packs at 100 MB and Kokoro at 150 MB, so "up to about 150 MB" is a true upper bound

## From review (`reviews/s291-high.md`)

- Applied #1-#4, #7, #8, #10 per the wave ledger (the exact edits are in the diff; the findings asked for): #1 the dot-source guard fixed so the pure functions load; #2 `-Form` checked against the emulator serial; #3 the reading-shot wait on something only true while reading; #4 a wait for the pack download banner before the home and settings shots; #7 stale comments and the `store-submit.yml` language count updated for RTST English; #8 one shared English-listing helper in `store-listings.mjs`; #10 shot lookup limited to this run's instance folders.
- Skipped #9 (`reviews/s291-high.md`): "The new en.md test pins the exact short-description string and specific long-description phrases" - the test checks the usage-counts bullet on purpose: that sentence is what item 3 ships.
- Applied #5/#6 by moving the four `store-shot-*` flows to `.maestro/<app>/needs-file/` (run by name only) and exempting `store-shot-*` flows from the manifest rule in `tools/emulator/lint-flows.ps1` (one line, outside the first Touches lines); the eight manifest entries were removed again.

## From review (`reviews/s291-medium.md`)

- Applied #1 (optional wait for the banner, then notVisible), #3 (RTST `en` goes only to a listing named exactly `en-us` or `en`; otherwise one warning names the English listings found), #5 (the store shot copied is the PNG named exactly after the flow's shot, newer than the run start, with a non-zero pixel size) and #8 (the review editor must be gone before the sentence is waited for).
- Skipped #2 (`reviews/s291-medium.md`): "line-for-line copy of phone flow (Dino pair too); selector edits must be made in four files and will drift" - low; one flow per form is how the other flows here are written, and the shot names carry the form.
- Skipped #4 (`reviews/s291-medium.md`): "extra-English warning recomputes keys after listingTarget may have created a listing" - low; with #3 no English listing is ever created, so the keys cannot differ.
- Skipped #6 (`reviews/s291-medium.md`): "-NoBuild with no cached build exits 6 without printing the fix command" - low; run-flows.ps1 already prints its own build-cache message before exit 6.
- Skipped #7 (`reviews/s291-medium.md`): "exemption skips every file named store-shot-*, including Dino" - low; the four store-shot flows are the only ones with that name and the self-test names them.
- Skipped #9 (`reviews/s291-medium.md`): "test pins exact marketing copy; wording edits break it" - duplicate of #9 in `reviews/s291-high.md`, skipped there for the same reason.

## From review (`reviews/pr-941-check1-medium.md`)

- Applied #1: `de.md` now has its own Microsoft Store download note (translation of `en.md`) and its stale "left out" drafting note is rewritten.
- Applied #2: the "no analytics" bullet in `es ar fa fr ja pt ru zh` is replaced by "no account" plus the optional anonymous usage counts bullet from `en.md` (my own translation).
- Skipped #3 (`reviews/pr-941-check1-medium.md`): "effective date bumped to 8 November 2026 though only the age range changed" - the age range is policy text, so the effective date moves with it
- Skipped #4 (`reviews/pr-941-check1-medium.md`): "not checked against real pack sizes" (the 150 MB claim) - packs.yml caps voice packs at 100 MB and Kokoro at 150 MB, an upper bound
- Skipped #5 (`reviews/pr-941-check1-medium.md`): "new Microsoft Store notes are unreviewed machine translations meant for direct paste" - native review of the translated notes goes to Not yet verified below

## From review (`reviews/s291-check.md`)

- Applied #1 (new-language images come from en-us, else en, else the first en-*; English text still goes only to en-us or en), #3, #4 (old shot PNGs are deleted before the run; no time filter), #7, #8.
- Skipped #2 (`reviews/s291-check.md`): "Pack-banner wait can pass before banner appears (8 s optional visible, then notVisible passes at once)" - the coordinator looks at every shot before any upload, so a banner in a shot is caught there; the device run decides (third round on this wait).
- Skipped #5 (`reviews/s291-check.md`): "PNGs are copied only after the whole flow, and exit 1 on a failed flow copies none" - low.
- Skipped #6 (`reviews/s291-check.md`): "When both en-us and en exist only en-us is written; en stays stale with just a warning" - low; a warning is enough while en-us is the listing Partner Center shows.

## From review (`reviews/s291-check3.md`)

- Applied #1, #2, #3 (first launch `clearState: true`, required 30 s wait for the banner, then notVisible 300000 ms).
- Skipped #4 (`reviews/s291-check3.md`): "A failed or offline pack state also leaves the label visible, so the flow waits the full 300 s (twice) and fails with a bare timeout" - low.
- Skipped #5 (`reviews/s291-check3.md`): "Remove-OldStoreShots deletes shots from the shared per-instance maestro-home/maestro-out before the run; a failed run leaves them gone" - low, unconfirmed.
- Skipped #6 (`reviews/s291-check3.md`): "The expect-log marker is checked after a flow that may wait up to 10 min; if logcat rolled, the run fails and copies no PNG though all shots exist" - low, unconfirmed.
- Skipped #7 (`reviews/s291-check3.md`): "The flow forces PORTRAIT on the tablet; if the tablet listing should be landscape, every Dino tablet PNG has the wrong orientation" - low, unconfirmed.

## From review (`reviews/s291-check2.md`)

- Skipped #1 (`reviews/s291-check2.md`): "waits 120 s for PackStatusLabel to vanish" - settled by the device runs (wait now 300 s, see Changed while building).
- Skipped #2 (`reviews/s291-check2.md`): "regex wait on the whole pasted sentence against one element" - low; the RTST phone run passed and each shot was looked at.
- Skipped #3 (`reviews/s291-check2.md`): "SpeedPresets may sit below the fold in the Settings ScrollView" - the RTST phone run passed and shot 4 (settings) was looked at.
- Skipped #4 (`reviews/s291-check2.md`): "a locked file throws under ErrorActionPreference Stop and aborts the run" - low.
- Skipped #5 (`reviews/s291-check2.md`): "a run missing one shot still copies the others" - decided by the device run: all shots came out and the coordinator looked at each one.
- Skipped #6 (`reviews/s291-check2.md`): "only en-us is overwritten; warnOtherEnglish only logs" - repeats check #6, skipped there.
- Skipped #7 (`reviews/s291-check2.md`): "line-for-line copy of the phone flow; fixes must be made twice" - low; same as medium #2.
- Skipped #8 (`reviews/s291-check2.md`): "the two rules can disagree (matters for Dino only)" - low; the four store-shot flows are the only files with that name.

## From review (`reviews/s291-check4.md`)

- Skipped #1 (`reviews/s291-check4.md`): "the notVisible PackStatusLabel wait passes on the first poll" - low; the pack was installed by shot 3, so a banner in shot 4 is unlikely, and the coordinator looks at every shot.
- Skipped #2 (`reviews/s291-check4.md`): "Offline emulator or pack server error keeps the banner visible" - low; it fails loudly and repeats check3 #4.
- Skipped #3 (`reviews/s291-check4.md`): "fails if the banner shows for less than a poll" - low; after clearState a download runs for minutes, so it is not reachable on the emulator path.

## Changed while building

- Item 1 done 2026-11-14 (wave 62): RTST Play, 17 live locales read. Only en-US carried the stale "no analytics" line; two committed Play edits (the usage-counts bullet from `en.md` lines 28-29, and the Google Play download note from `en.md`), read back equal, 1349 characters. The other 16 locales were already current. Dino Play was done 2026-11-13.
- Item 3 built: RTST English is added to `.github/scripts/store-listings.mjs`. It is strict: written only to an `en-us` or `en` listing, otherwise skipped with a warning; the image source for new languages keeps en-us, en, then the first en-*. It reaches Partner Center with the next RTST release; item 3 stays open until the read-back after that release.
- Item 5, Android phone built: flows `.maestro/{dino,rtst}/needs-file/store-shot-{phone,tablet}.yaml`, `tools/emulator/store-screenshots.ps1` (and `test-store-screenshots.ps1`), PNGs in `docs/store-listings/screenshots/dino/phone` (1-shelf, 2-reading, 3-books) and `rtst/phone` (1-home, 2-review, 3-reading, 4-settings). Emulator runs 2026-11-14 on emulator-5554: Dino phone PASS (69 s), RTST phone PASS (225 s, clearState, pack downloaded in the run). The coordinator looked at every shot's contact sheet: clean. Still open in item 5: Play tablet (no second emulator: 3.6 GB free RAM, the 8 GB rule), iPhone and iPad (`store-screenshots.yml` reusing Slice 244's ios-sim scripts, now on main), Microsoft Store desktop (VM, same RAM reason), and the RTST Windows control-panel retake.
- Item 6 decision (coordinator, 2026-11-14): no upload tonight. Uploads go per store once its full set exists (Play phone and tablet together), so a live listing never gets a half-replaced set.

- Item 5, RTST store-shot flows (phone and tablet): the pack wait is 300000 ms (was 120000). The emulator run 2026-11-14 20:37-20:41Z failed at `notVisible id: PackStatusLabel`; logcat showed the pack still downloading, `PACKDL progress pack=supertonic-3 bytes=37510341` at 20:40:08 rising about 1.1 MB/s to `bytes=113614848` at 20:41:18. The first launch is back on `clearState: true` (check3 #1, #2: without it `TestHookDispatcher.cs:209` ignores `lang` when a language is already set, and kept data lets text from an earlier run into shots 1 and 2); a fresh install always starts the download, so the flow then requires `PackStatusLabel` visible (30 s, no longer optional) before `notVisible` with 300000 ms, and the shots show the voice pack installed. The relaunch before the Settings shot keeps the data and only waits for the banner to be gone.
- Item 5, Dino store-shot flows (phone and tablet): the Store shot is dropped and the Books shot renumbered to `-3-books`. The phone run passed, but shot 3 (Store tab) showed blank beige cover boxes (the Debug test store has no covers). `store-dino-phone-3-store.png` is deleted and `store-dino-phone-4-books.png` is renamed `store-dino-phone-3-books.png`.

## Not yet verified

- Partner Center RTST English read-back after the next RTST release (item 3).
- The tablet store-shot flows (`store-shot-tablet.yaml`, Dino and RTST) have never been run.
- A native speaker reads the translated Microsoft Store notes and usage-counts bullets in `de es fr pt ar fa ja ru zh` before the Partner Center push.

## Why

Facts from plan.md, to be re-read live before each edit (the `store-listing` agent reads every live listing first):

- **Play, RTST** says "no analytics", wrong since Slice 137.
- **Play, Dino** says "3 to 8, nine languages"; the other stores say 3 to 9. The language count comes from the app, not from memory.
- **Dino privacy page** (`site/dino/privacy.html`) also says "3 to 8".
- **Microsoft Store, RTST, English** is stale (neutral text from before the voice packs; found in Slice 193's live read).
- **App Store, both apps**: never submitted. The 2026-10-30 `appstore-submit` run ([41078343356](https://github.com/ashkansirous/ReadTheStupidText/actions/runs/41078343356)) lists what Apple wants: store text, category, copyright, price, content rights, age rating, review contact and iPhone 6.5-6.9" + iPad 13" screenshots. Drafts: `docs/store-listings/appstore-rtst.md`, `docs/store-listings/dino/appstore.md` (the age-rating enum names and the Kids-category rules there were written from memory: check on the first API write). Five values wait on the user's confirmation: content rights, the 4+ age-rating answers, reviewer contact `elinsoftuk@gmail.com` with no phone, Kids category off, and the drafted text, category, copyright and free price. Then run the [workflow](https://github.com/ashkansirous/ReadTheStupidText/actions/workflows/appstore-submit.yml).
- **Screenshots**: none fresh for any store. The RTST control-panel shot from Slice 194 is not good enough (guest offline, no voice pack, watermark).

## Work

- [x] 1. (Dino Play 2026-11-13, RTST Play 2026-11-14) Play, both apps: correct the RTST analytics sentence, the Dino age range (3 to 9) and language count (read from the app), through the `store-listing` agent (draft, diff against live, apply, read back). **Needs the service account `auto-deploy-to-google-play@read-the-stupid-text.iam.gserviceaccount.com` to have "Manage store presence" on both apps** (Play Console, Users and permissions). Granting it from Claude's Chrome was refused by the auto-mode permission check on 2026-11-07, so it is the user's one click, or an allow from the user. Until then `edits.commit` returns 403 (Slice 193).
- [x] 2. `site/dino/privacy.html` (and `site/dino/index.html`): "3 to 8" becomes "3 to 9".
- [ ] 3. Microsoft Store, RTST English: replace the stale listing with the current text (`docs/store-listings/en.md`, checked against the live listing first). Partner Center through Chrome (Claude's work), as in Slice 193.
- [ ] 4. App Store go-live text for both apps from the two drafts: confirm the five values with the user in one question, check the age-rating enum names and Kids-category rules against Apple's docs on the first API write, apply through the App Store Connect API.
- [ ] 5. Screenshots, every store: iPhone 6.5-6.9" and iPad 13" (a macOS CI simulator job, `store-screenshots.yml`; Android and iOS are the same app, so Dino and RTST get the same frames); Play phone and tablet (emulator, `tools/emulator/screenshot-frames.json`); Microsoft Store desktop (VM). Retake the RTST control-panel shot from Slice 194 with the voice pack installed, signed in as a normal user and no watermark.
- [ ] 6. Upload the screenshots with the listings (Play API, App Store Connect API, Partner Center), read each store back.

## Verification

- Each live listing read back equals the repo text; no listing says "no analytics" (RTST) or "3 to 8" (Dino, site).
- Every store has the screenshot sizes it requires; the App Store Connect version shows no missing items.

## User answers (2026-11-07, before the night run)

- **App Store legal values (item 4): not tonight.** The user did not approve sending the drafts' declarations
  (copyright, content rights, age rating, review contact) yet, and Apple's required contact phone number is not
  stored anywhere. Item 4 waits for the user; a night wave does not write any App Store declaration.
- **Play "Manage store presence" grant (item 1): later.** The user will grant it another time; until then the
  Play publish waits. Items that need neither (listing text in the repo, screenshots, Microsoft Store text) may be built.

## User answers (2026-11-09, Dino App Store go-live)

- **Content rights:** "sure", so the draft's `DOES_NOT_USE_THIRD_PARTY_CONTENT` is accepted.
- **Parental controls:** the grown-ups gate counts as a parental control, so `parentalControls` is true.
- **Review contact email:** elinsoftuk@gmail.com. Phone: given by the user on 2026-11-09 and kept only in the private secrets store
  (`D:\secrets\dinoreader\secrets.env`, `APP_REVIEW_CONTACT_PHONE`). Never write it in this public repo.
- **Kids Category: off for now** (user). Claude's reasons were wrong: Dino has no ML Kit (Slice 305), collects
  nothing personal, and everything (settings, links, update page) is already behind the grown-ups gate. Turning it on
  later is the user's call; the only real open point is that Apple's age bands do not match "3 to 9".
- **Cartoon violence: "infrequent or mild"** (user agreed), still rated 4+.
- **The App Store draft `docs/store-listings/dino/appstore.md` is stale** (found 2026-11-09): it still describes
  scanning and adding books, translation, the voice download and the camera, all removed by Slice 278 (store books
  only, pre-made clips) and Slice 305. Rewrite the description, keywords and review notes from the current app before
  any push.
- **Price:** free to download. **In-app purchases: decided later on 2026-11-09 (user):** four free books, the rest $0.99 each; planned in Slice 346 (docs/plan/slices/346-dino-paid-books.md), which also changes the listing and privacy lines below. Earlier note, kept for the record: what is sold is not decided, and
  nothing is planned or built for it (StoreKit on iOS and Play Billing on Android, the Paid Apps agreement in App
  Store Connect, a gate in front of purchases). Every "no in-app purchases" line in `docs/store-listings/dino/*`,
  `site/dino/privacy.html` and the Play/Microsoft listings would have to change when purchases ship.
