# Slice 291 — Store listings and screenshots for both apps on Play, App Store and Microsoft Store

*Planned 2026-11-07.* **Park note (2026-11-07, night run wave 5):** parked: the Play "Manage store presence" grant (user: later) and the App Store legal values (user: not tonight), per the User answers 2026-11-07. Blocks pushing the listings. Store text and images, no app code. Moves the listing bullets out of plan.md's *Outstanding work* (the Play "out of date" bullet, the App Store go-live bullet, the stale RTST Microsoft Store English listing from Slice 193's live read, the Slice 194 screenshot retake).

**Touches:** `docs/store-listings/**`, `STORE.md`, `site/dino/privacy.html`, `.github/workflows/store-screenshots.yml` (new: macOS simulator screenshots), `tools/emulator/screenshot-frames.json`, `tools/desktop-vm/checks/rtst-panel-screenshot.ps1`, `docs/plan/slices/291-store-listings-and-screenshots.md`

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
- [ ] 2. `site/dino/privacy.html`: "3 to 8" becomes "3 to 9".
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
