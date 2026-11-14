# Slice 374 — Store listings in the other 13 app languages, both apps, every store
**Product:** Shared

*Partly built (wave 2026-11-14, merged as PR #1110 (7e709c31); parked, see plan.md): Google Play (both apps, 15 locales) and App Store (Dino 1.81.1, RTST 1.76.1, 14 locales, Farsi not offered by Apple; versions not submitted) pushed and read back equal. Parked: the Microsoft Store part (item 4 and Partner Center in item 5) waits for a Partner Center submission credential (an Entra app with `MS_SUBMISSION_TENANT_ID`, `MS_SUBMISSION_CLIENT_ID`, `MS_SUBMISSION_CLIENT_SECRET`), which only the user can create, or an interactive session with the user's signed-in Chrome.*

*Moved back to `planned` by the 2026-11-14-0726 feedback loop (park reason above kept). Since then PR #1115 (merged 2026-11-14 11:17Z) puts the listings into each release's own Store submission with the existing `AZURE_AD_*` credentials, so no `MS_SUBMISSION_*` app is needed; the first such run, release-dino.yml 43589278474, ended `store-submit-dino / submit: success` with no listing warning (read 2026-11-14 by the loop). Items 4 and 5 close when a submission is read back with the 13 languages.*

*Earlier (history): drafts merged as PR #1096 (2c068e12); the store pushes are not done (the night push was denied by auto mode, [Unrequested Commit in a Connected App]; no Partner Center submission credential exists). Needs an interactive session.* Moved back to `planned` by the 2026-11-13-1149 feedback loop (park reason kept here); a night run is denied again unless the user adds an autoMode allow line for store listing pushes (see that note's *Waiting on the user*).

**Touches:** `docs/store-listings/**` (new: one file per language for Dino under `docs/store-listings/dino/<lang>.md`; RTST's existing `docs/store-listings/<lang>.md` files completed, new `da.md`, `fi.md`, `sv.md`, `nb.md`, `nl.md`), `docs/plan/slices/374-store-listings-13-languages.md`, `docs/plan/unverified.md`

## Why

Both apps speak 15 languages (`src/Sirous.Reading.Application/Reading/AppLanguage.cs`: English, Persian, French, Spanish, Portuguese, Chinese, Arabic, Russian, Japanese, Danish, Finnish, Swedish, Norwegian, Dutch, German), but the stores list them in far fewer. Read live on 2026-11-13:

- **App Store:** Dino 1.81.1 and RTST 1.76.1 have en-US and de-DE only (ASC API).
- **Google Play:** Dino en-GB and de-DE; RTST en-US only (Play API).
- **Microsoft Store:** Dino English and German (Slice 346 Part D). RTST: the repo holds `ar es fa fr ja pt ru zh` texts (Slice 291 lists them as needing a native read before the Partner Center push); which of them are live was not read.

A French or Japanese parent searching the store sees an English page for an app that speaks their language.

## Decisions (user, 2026-11-13)

- Both apps, all three stores, the 13 languages beyond English and German: Arabic, Chinese, Danish, Dutch, Farsi, Finnish, French, Japanese, Norwegian, Portuguese, Russian, Spanish, Swedish.
- Machine-translated from the current English listing (Dino: `docs/store-listings/dino/appstore.md` and `play.md` as rewritten 2026-11-13; RTST: `docs/store-listings/en.md` and `appstore-rtst.md`), then pushed at once. One go covers every language and store: no second approval round per store or language.
- No native read holds a push: each language gets a "native read" line in `docs/plan/unverified.md` instead.

## Work

- [x] 1. Read every live listing first (ASC API, Play API, Partner Center) for both apps and record per store which locales exist. Check each store's supported locale list in its own docs (Apple's App Store Connect localizations, Play's listing languages, Partner Center's): a language a store cannot list (Farsi on the App Store, for one, to be checked) is written down here and skipped there, never mapped to another language.
- [x] 2. Dino, smallest end-to-end first: French on Play (write `docs/store-listings/dino/fr.md`, push, read back). Then the other 12 languages on Play.
- [x] 3. Dino App Store: the 13 languages on version 1.81.1 (description, keywords within 100 bytes per locale, promotional text, subtitle within 30). Read back. Do not submit the version.
- [ ] 4. (BUILT into the release pipeline, not yet run live; stays open until a release run's `submission get` shows the 13 languages, see Microsoft Store build) Dino Microsoft Store: the 13 languages into the next Partner Center submission (listing only; never a submission that discards a pending draft), committed, read back from the submission.
- [ ] 5. (Google Play and App Store done; Partner Center built into the release pipeline, not yet run live, so open until the read-back, see Microsoft Store build) RTST: complete `ar es fa fr ja pt ru zh` against the current `en.md`, add `da fi sv nb nl`, push to Play, App Store (version 1.76.1, not submitted) and Partner Center the same way, read back.
- [ ] 6. Play's and Apple's store keywords and short texts are written for each language, not translated word for word (store search terms people actually type, e.g. "Gute-Nacht-Geschichten", not a literal "bedtime stories").
- [ ] 7. Each language gets one line in `docs/plan/unverified.md`: native read of the Dino and RTST listings in that language.

The work goes through the global `store-listing` agent (apply mode; the user's approval above is the go).

Pushes not done: the night session's store push was denied by the auto mode classifier (2026-11-13), reason verbatim: [Unrequested Commit in a Connected App]. The texts are in the repo; the push runs in an interactive session.

## Draft state (2026-11-13, draft run, nothing pushed)

**Texts written (items 2-7 text part).** Dino: `docs/store-listings/dino/<lang>.md` for ar zh da nl fa fi fr ja nb pt ru es sv (title, Play short, App Store subtitle/keywords/promotional text, Microsoft search terms, one description: Play text, with "Google Play" replaced by "App Store" or "Microsoft Store" for the other two). RTST: `docs/store-listings/<lang>.md` completed for ar es fa fr ja pt ru zh (brought to the current `en.md`: "no ads", usage-count wording, Android scan pack, Microsoft fallback reports) and new da fi sv nb nl; each now also has the App Store subtitle, keywords, promotional text and description. Item 6: every language has its own store search words (not word for word), checked by script against Apple keywords 100 bytes, subtitle 30, promotional 170, Play short 80, Microsoft 7 terms of at most 30 (RTST) or 40 (Dino) characters and 21 words; all within. Play has no keyword field: its search words are listed per file ("Play search phrases") and sit in the short and full text. Item 7: 13 native-read lines in `docs/plan/unverified.md` under Shared. All are machine drafts.

**Supported locales per store (item 1, from each store's own docs).**
- App Store: Apple's App Store Connect localizations page (https://developer.apple.com/help/app-store-connect/reference/app-store-localizations, read 2026-11-13) lists 50 languages including Arabic, Chinese (Simplified and Traditional), Danish, Dutch, Finnish, French, Japanese, Norwegian, Portuguese (Brazil, Portugal), Russian, Spanish (Spain, Mexico), Swedish. **Persian (Farsi) is not on the list: no Farsi on the App Store, for either app; skipped, not mapped to another language.** The page gives names, not codes; the codes in the files (`ar-SA`, `zh-Hans`, `da`, `nl-NL`, `fi`, `fr-FR`, `ja`, `no`, `pt-BR`/`pt-PT`, `ru`, `es-ES`/`es-MX`, `sv`) are from memory and must be checked against the API's accepted values at the push.
- Google Play: Play Console help "Translate and localize your app" (https://support.google.com/googleplay/android-developer/answer/9844778, read 2026-11-13) lists all 13, Persian as `fa` (also fa-AE, fa-AF, fa-IR), Arabic `ar`, Chinese `zh-CN`/`zh-TW`/`zh-HK`, Danish `da-DK`, Dutch `nl-NL`, Finnish `fi-FI`, French `fr-FR`, Japanese `ja-JP`, Norwegian `no-NO`, Portuguese `pt-BR`/`pt-PT`, Russian `ru-RU`, Spanish `es-ES`/`es-419`, Swedish `sv-SE`.
- Microsoft Store: not confirmed. The Microsoft Learn pages I tried for the Partner Center language list returned 404, so Farsi and the Norwegian and Chinese codes are unconfirmed; check in Partner Center at the push (the existing RTST `fa.md`, `ar.md` etc. were written for it in Slice 291).

**Live locales per store and app.** Not re-read in this run: this draft run had no permission to read the credentials in `D:/secrets` (the one read was denied by the auto mode classifier, reason [Credential Materialization]). The 2026-11-13 reads in "Why" above stand (App Store en-US, de-DE; Play Dino en-GB, de-DE, RTST en-US; Microsoft Dino en, de; which RTST Microsoft locales are live was never read). Re-read them at the push, before overwriting.

**Credentials (names only).** `D:/secrets/dinoreader/secrets.env` holds `DINOREADER_PLAY_SERVICE_ACCOUNT_JSON`, `APPLE_IAP_*` and `MS_STORE_*` (the Worker's Store collections app, not a submission API); `D:/secrets/readthestupidtext/secrets.env` holds `PLAY_SERVICE_ACCOUNT_JSON`; the App Store Connect key names (`ASC_API_KEY_*`) are in the `lets-call-mom` store. **No Partner Center submission credential exists for RTST or Dino**: no `MS_SUBMISSION_TENANT_ID`, `MS_SUBMISSION_CLIENT_ID` or `MS_SUBMISSION_CLIENT_SECRET` in any `D:/secrets` file, and the repo's GitHub secrets list shows no `MS_SUBMISSION_*`. So the Microsoft Store push (items 4 and the Partner Center part of 5) needs that Entra app created first, or the user's signed-in Chrome up to the Submit button.

## Verification

- Each store's listing for each app, read back through its API (Partner Center from the submission), equals the repo text in all 15 languages the store supports.
- A language a store does not support is named in this file with the store's own doc as evidence.

## Push results (2026-11-14, apply run)

Live read first (2026-11-14): Play Dino en-GB, de-DE; Play RTST en-US. ASC Dino 1.81.1 en-US, de-DE; ASC RTST 1.76.1 en-US (description empty, left as it was), de-DE. Both versions `PREPARE_FOR_SUBMISSION`, still not submitted. No denial by any permission check.

- **Google Play, Dino:** pushed and read back equal (title, short, full description): fr-FR first (own edit), then ar, zh-CN, da-DK, nl-NL, fa, fi-FI, ja-JP, no-NO, pt-BR, pt-PT, ru-RU, es-ES, es-419, sv-SE (second edit). All committed.
- **Google Play, RTST:** the same 15 locales (fr-FR, ar, zh-CN, da-DK, nl-NL, fa, fi-FI, ja-JP, no-NO, pt-BR, pt-PT, ru-RU, es-ES, es-419, sv-SE) pushed and read back equal.
- **App Store, Dino 1.81.1:** description, keywords, promotional text, subtitle, name pushed and read back equal for ar-SA, zh-Hans, da, nl-NL, fi, fr-FR, ja, no, pt-BR, pt-PT, ru, es-ES, es-MX, sv. The API accepted every code. Farsi skipped (not an App Store language). Not submitted.
- **App Store, RTST 1.76.1:** the same 14 locales pushed and read back equal. Farsi skipped. Not submitted. Support/marketing/privacy URLs were copied from the existing localization (not new text).
- **Microsoft Store, both apps:** WAITING. No Partner Center submission credential exists (`MS_STORE_*` is the Worker's collections app); the headless session did not try Chrome. Item 4 and the Partner Center part of item 5 stay open. Texts are in the repo.
- Machine drafts, native read still open (`docs/plan/unverified.md`). Apple created the appInfo localization together with the version localization; the second pass patched it.

**Microsoft Store build (2026-11-14, no Partner Center credential needed).** `store-submit.yml` (called by release-rtst.yml and release-dino.yml, which already hold `AZURE_AD_*` and `SELLER_ID`) now does: `msstore publish <bundle> -id <id> -ut 300 -v --noCommit` (uploads the package into the new draft, no commit), then a step that runs `msstore submission get <id>`, merges the listings with `.github/scripts/store-listings.mjs --app rtst|dino --submission draft.json` (title, short description, description and search terms of the 13 languages only; pricing, packages, images and the en/de listings are kept; refuses a submission without `Pricing.PriceId`), `msstore submission update <id> --payload merged.json` and `msstore submission publish <id>`. One submission per release, no discard beyond what `publish` already did, no price change. After review: the listings are validated offline in a step before `publish`; get/merge/update are best-effort (a `::warning::`) and `submission publish` always runs; msstore-cli is pinned to v0.4.4 and `get` passes `--output-stream stderr`; Brazilian Portuguese goes to `pt-br`; existing keys match case-insensitively and by language (`fr` finds `fr-fr`), a new language copies the English listing's images or is skipped with a warning; a concurrency group per product id. The STORE_PRODUCT_ID/DINOREADER_STORE_APP_ID pairing check is unchanged. Sources: learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/commands (publish deletes the pending draft and clones the last published submission; `--noCommit`; `submission update` replaces the whole submission), msstore-cli `UpdateCommand.cs`, learn.microsoft.com/en-us/windows/uwp/monetize/manage-app-submissions (listing resource, lower-case language keys).
Not confirmed (check on the first release run, `msstore submission get` after it): Partner Center's character limits (script uses 10,000 description, 1,000 short, 200 title, 7 search terms of 30 chars, 21 words) and the language codes `zh-cn`, `nb`, `fa`, `ar`; whether `submission get` stdout is pure JSON (the step fails loudly if not). Listing text changes ship with the next app release (docs-only merges cut none); read the live listings back after that run to close the read-back item.
