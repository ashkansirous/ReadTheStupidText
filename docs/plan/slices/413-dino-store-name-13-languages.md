# Slice 413 — Dino: the new store name, subtitle and keywords in the other 13 languages, in each language's own search words

**Product:** Dino

Source: phone scope "Dino: store name, subtitle and keywords that parents search for" (2026-11-18), milestone 3 — https://drive.google.com/file/d/1pSYzLLe3EkJs7rU6VWhTJ6reKgFn_QZ5/view?usp=drivesdk

**Touches:** `docs/store-listings/dino/{ar,da,de,es,fa,fi,fr,ja,nb,nl,pt,ru,sv,zh}.md`

Hard constraint (user, 2026-11-18): only listing text changes; package name `uk.sirous.dinoreader`, bundle id and
store app ids never change (Slice 412, *Hard constraint*).

Hard constraint (user, 2026-11-18): no Play push until Dino's first production release is published (Slice 412,
second *Hard constraint*).

Needs Slice 412 merged first (English name, guard and tooling).

## Why

A rename touches every listing: 15 Play locales, 14 App Store locales (no Farsi on the App Store, Slice 374) and the
Microsoft Store. The hard part (phone doc): find the words parents in each language actually search for, not a
word-for-word translation, while keeping "Dino" as the brand and the own-voice idea in the subtitle.

## Decisions

- Brand "Dino" stays first in every name; the descriptor after the colon is the local search phrase for kids' books
  and stories (e.g. German "Gute-Nacht-Geschichten" style words, as Slice 374 already did for keywords).
- Subtitle keeps "bedtime" + "your voice" in local words.
- Each store's limits as in Slice 412 (name 30, subtitle 30, keyword field 100 bytes, Play short 80; Microsoft 7 keywords).
- No word repeated across the iOS name, subtitle and keyword field within one locale.
- Machine-written like Slice 374; the native read goes to `docs/plan/unverified.md`, never a hold (user rule).

## Work items

1. [x] For each of the 14 languages (ar da de es fa fi fr ja nb nl pt ru sv zh; de was in the brief too): title, Play short description, App Store subtitle and keywords, Microsoft
   search terms in `docs/store-listings/dino/<lang>.md`, checked against the limits (own script: name/subtitle 30, keywords 100 bytes, short 80, 7 terms, 21 words, no keyword word repeated from name or subtitle; `store-listings.mjs --app dino` builds 13 listings and its 27 tests pass; `check-public-name.mjs` passes). Written 2026-11-18. The description text only swaps the old name for "Dino".
2. [ ] Push through the `store-listing` agent (apply mode). **App Store done 2026-11-18:** editable version 1.133.1 (`PREPARE_FOR_SUBMISSION`, already existed; no build attached, not submitted): name + subtitle on the editable app info (`ef709cd7-...`) and keywords + description (live text with only "Dino Book Reader" swapped for "Dino", because the live editable text already carries the subscription wording) on 15 locales (ar-SA zh-Hans da nl-NL fi fr-FR ja no pt-BR pt-PT ru es-ES es-MX sv de-DE), all read back equal. **Microsoft: repo only.** No Partner Center submission credential exists on this PC (`MS_STORE_*` is the Store collections app) and submission 1152921505702441682 is in certification; the release's `store-submit` carries the files (`store-listings.mjs`); German (not built by the script) stays by hand. **Google Play: repo only, never called** (Hard constraint, Slice 412). Open until Microsoft and Play are read back.
   *Submitted 2026-11-18:* App Store version 1.133.1 (build 20261118.135720) went to App Store review with every
   locale's name, subtitle, keywords and What's New (`appstore-submit` run 44413771332; the first run 44413222409 failed
   on the missing What's New, now in a "What's new 1.133.1" section of each file). Open finding from that run: the
   in-app purchase `uk.sirous.dinoreader.book.love_from_seagull_island` is MISSING_METADATA in App Store Connect, so it
   did not go with the version; the book cannot be bought on its own on iOS until its metadata is filled.
3. [x] One "Not yet verified" block for the native reads (`docs/plan/unverified.md`, Slice 413).

## Out of scope

- Book text or in-app translations (in-app strings use the English brand name, Slice 412).
