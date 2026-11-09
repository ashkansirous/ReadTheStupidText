# Slice 291 — Store listings and screenshots for both apps on Play, App Store and Microsoft Store

*In progress (wave 2026-11-08), PR #941 merged (08e1d63a): item 2 and the repo listing text of items 1 and 3 only (no store push, no screenshots); the rest stays open.* **Wave scope (2026-11-08):** this wave's PR touches only `docs/store-listings/**`, `site/dino/privacy.html`, `site/dino/index.html` (same age range) and this file. **Park note (2026-11-07, night run wave 5):** parked: the Play "Manage store presence" grant (user: later) and the App Store legal values (user: not tonight), per the User answers 2026-11-07. Blocks pushing the listings. Store text and images, no app code. Moves the listing bullets out of plan.md's *Outstanding work* (the Play "out of date" bullet, the App Store go-live bullet, the stale RTST Microsoft Store English listing from Slice 193's live read, the Slice 194 screenshot retake).

**Touches:** `docs/store-listings/**`, `STORE.md`, `site/dino/privacy.html`, `site/dino/index.html`, `.github/workflows/store-screenshots.yml` (new: macOS simulator screenshots), `tools/emulator/screenshot-frames.json`, `tools/desktop-vm/checks/rtst-panel-screenshot.ps1`, `docs/plan/slices/291-store-listings-and-screenshots.md`

## Built this wave (2026-11-08)

- Item 2 done: `site/dino/privacy.html` and `site/dino/index.html` (meta description and eyebrow) say "3 to 9"; `index.html` was added to Touches.
- Item 1 repo text: already correct before this wave (`docs/store-listings/dino/play.md`: 3 to 9, fifteen languages, counted from the 15 `AppResources.*.resx` locales; RTST text in `en.md` and `appstore-rtst.md` states the optional anonymous usage counts). No edit needed. The Play push stays open (needs the "Manage store presence" grant).
- Item 3 repo text: `docs/store-listings/en.md` made current: the offline bullet now says Windows and Android download the voice pack once, and the stale "don't add until Slice 56 ships" line became a Microsoft Store download note (about 145 MB, built-in Windows voice until the pack lands). The Partner Center push stays open; compare with the live text first.
- Still open: items 1 (push) and 3 (push), 4, 5, 6.

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

## From review (`reviews/pr-941-check1-medium.md`)

- Applied #1: `de.md` now has its own Microsoft Store download note (translation of `en.md`) and its stale "left out" drafting note is rewritten.
- Applied #2: the "no analytics" bullet in `es ar fa fr ja pt ru zh` is replaced by "no account" plus the optional anonymous usage counts bullet from `en.md` (my own translation).
- Skipped #3 (`reviews/pr-941-check1-medium.md`): "effective date bumped to 8 November 2026 though only the age range changed" - the age range is policy text, so the effective date moves with it
- Skipped #4 (`reviews/pr-941-check1-medium.md`): "not checked against real pack sizes" (the 150 MB claim) - packs.yml caps voice packs at 100 MB and Kokoro at 150 MB, an upper bound
- Skipped #5 (`reviews/pr-941-check1-medium.md`): "new Microsoft Store notes are unreviewed machine translations meant for direct paste" - native review of the translated notes goes to Not yet verified below

## Not yet verified

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

- [ ] 1. Play, both apps: correct the RTST analytics sentence, the Dino age range (3 to 9) and language count (read from the app), through the `store-listing` agent (draft, diff against live, apply, read back). **Needs the service account `auto-deploy-to-google-play@read-the-stupid-text.iam.gserviceaccount.com` to have "Manage store presence" on both apps** (Play Console, Users and permissions). Granting it from Claude's Chrome was refused by the auto-mode permission check on 2026-11-07, so it is the user's one click, or an allow from the user. Until then `edits.commit` returns 403 (Slice 193).
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
- **Review contact email:** elinsoftuk@gmail.com. The phone number Apple asks for is still missing.
- **Kids Category: off for now** (user). Claude's reasons were wrong: Dino has no ML Kit (Slice 305), collects
  nothing personal, and everything (settings, links, update page) is already behind the grown-ups gate. Turning it on
  later is the user's call; the only real open point is that Apple's age bands do not match "3 to 9".
- **Cartoon violence: "infrequent or mild"** (user agreed), still rated 4+.
- **The App Store draft `docs/store-listings/dino/appstore.md` is stale** (found 2026-11-09): it still describes
  scanning and adding books, translation, the voice download and the camera, all removed by Slice 278 (store books
  only, pre-made clips) and Slice 305. Rewrite the description, keywords and review notes from the current app before
  any push.
- **Price:** free, **but the user wants in-app purchases**. That is new scope: what is sold is not decided, and
  nothing is planned or built for it (StoreKit on iOS and Play Billing on Android, the Paid Apps agreement in App
  Store Connect, a gate in front of purchases). Every "no in-app purchases" line in `docs/store-listings/dino/*`,
  `site/dino/privacy.html` and the Play/Microsoft listings would have to change when purchases ship.
