# Slice 374 — Store listings in the other 13 app languages, both apps, every store
**Product:** Shared

*In progress (wave 2026-11-13), draft PR.*

**Touches:** `docs/store-listings/**` (new: one file per language for Dino under `docs/store-listings/dino/<lang>.md`; RTST's existing `docs/store-listings/<lang>.md` files completed, new `da.md`, `fi.md`, `sv.md`, `nb.md`, `nl.md`), `docs/plan/slices/374-store-listings-13-languages.md`, `docs/plan/unverified.md`

## Why

Both apps speak 15 languages (`src/ReadTheStupidText.Application/Reading/AppLanguage.cs`: English, Persian, French, Spanish, Portuguese, Chinese, Arabic, Russian, Japanese, Danish, Finnish, Swedish, Norwegian, Dutch, German), but the stores list them in far fewer. Read live on 2026-11-13:

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

## Verification

- Each store's listing for each app, read back through its API (Partner Center from the submission), equals the repo text in all 15 languages the store supports.
- A language a store does not support is named in this file with the store's own doc as evidence.
