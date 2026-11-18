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

1. [ ] For each of the 13 languages: title, Play short description, App Store subtitle and keywords, Microsoft
   search terms in `docs/store-listings/dino/<lang>.md`, checked against the limits by `store-listings.mjs`.
2. [ ] Push through the `store-listing` agent (apply mode) to Play, App Store (14 locales; iOS fields with the next
   version submission) and Microsoft, and read every one back equal (milestone 3 acceptance).
3. [ ] One "Not yet verified" block for the native reads.

## Out of scope

- Book text or in-app translations (in-app strings use the English brand name, Slice 412).
