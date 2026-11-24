# Slice 439 — Dino: a linked Sonos home plays the paid books it owns
**Product:** Dino

*Planned 2026-11-24. Plan only: the owner checks it before it is built (owner, 2026-11-24: "Plan only; check with me
before building").*

**Depends on:** Slice 438 (the link and the per-home row). **Replaces:** Slice 415 item C's paid-track check on Sonos.

**Touches:** `worker/sonos.ts`, `worker/sonos.test.mjs`, `worker/sonos-link.ts`, `worker/sonos-link.test.mjs`,
`worker/entitlements/gate.ts`, `worker/sonos-labels.ts`, `src/Dino/DinoReader.App/SonosLinkPage.xaml.cs`, `PRIVACY.md`,
`site/dino/privacy.html`, this file.

## Why

Owner's brief, 2026-11-24 (verbatim): "Slice C, owned books play on Sonos: a linked home that owns a paid book
(worker/entitlements) can play it; subtitle "Owned". Plan only; check with me before building."

On origin/main (2026-11-24): a store proof is the header `X-Purchase-Proof: <store>:<productId>:<token>`
(`worker/entitlements/proof.ts:5,9-20`); `gate.ts` asks Apple, Google or Microsoft whether the buyer owns a product that
unlocks the book: its own product, a bundle or the subscription (`worker/entitlements/products.ts:36-37`). Answers are
cached in memory only; nothing about the buyer is stored.

## Decisions

- Owner, 2026-11-24 (the mock: the Link screen says Dino keeps "which books you own"; SET-Saved shows "Owned"): a linked
  home plays every paid book its proof unlocks, single books and bundles as well as the subscription. This is newer than
  Slice 415 decision 1 ("subscription only on Sonos", 2026-11-18) and replaces it on Sonos. Named for the owner's check
  before building.
- Paid books the home does not own keep Slice 437's "In the Dino app" row.

## Design

From `docs/design/sonos/SET-Saved.dc.html`: an owned paid book's subtitle ends "Owned" ("The Little Red Hen · 7 min ·
Owned"); free books "Free"; not owned "In the Dino app".

## Work

- [ ] C1. Slice 438's confirm step also stores the app's purchase-proof key for the home (the key only, as `gate.ts`
      caches it; the raw token is kept only as the store checkers need it, decided after reading `gate.ts`).
- [ ] C2. `worker/sonos.ts`: for a linked home, each paid track runs the stored proof through the gate (cached); owned →
      playable, subtitle "Owned"; else Slice 437's row. `getMediaURI` checks again.
- [ ] C3. The Dino app re-sends the proof when the purchase changes (a new book, a renewed subscription), Android and iOS.
- [ ] C4. Tests: owned single book, bundle, active and lapsed subscription, no proof.
- [ ] C5. Privacy text: the per-home row now holds a purchase proof key.

## Verification

- `node --test worker/*.test.mjs`; owner's sandbox with a test purchase on the S21: the book shows "Owned" and plays.
