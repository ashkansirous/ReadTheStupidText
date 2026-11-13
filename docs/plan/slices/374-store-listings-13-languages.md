# Slice 374 — Store listings in the other 13 app languages, both apps, every store
**Product:** Shared

*Parked: drafts merged as PR #1096 (2c068e12); the store pushes are not done (the night push was denied by auto mode, [Unrequested Commit in a Connected App]; no Partner Center submission credential exists). Needs an interactive session.*

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

- [ ] 1. Read every live listing first (ASC API, Play API, Partner Center) for both apps and record per store which locales exist. Check each store's supported locale list in its own docs (Apple's App Store Connect localizations, Play's listing languages, Partner Center's): a language a store cannot list (Farsi on the App Store, for one, to be checked) is written down here and skipped there, never mapped to another language.
- [ ] 2. Dino, smallest end-to-end first: French on Play (write `docs/store-listings/dino/fr.md`, push, read back). Then the other 12 languages on Play.
- [ ] 3. Dino App Store: the 13 languages on version 1.81.1 (description, keywords within 100 bytes per locale, promotional text, subtitle within 30). Read back. Do not submit the version.
- [ ] 4. Dino Microsoft Store: the 13 languages into the next Partner Center submission (listing only; never a submission that discards a pending draft), committed, read back from the submission.
- [ ] 5. RTST: complete `ar es fa fr ja pt ru zh` against the current `en.md`, add `da fi sv nb nl`, push to Play, App Store (version 1.76.1, not submitted) and Partner Center the same way, read back.
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
