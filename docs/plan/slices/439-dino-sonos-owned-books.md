# Slice 439 — Dino: a linked Sonos home with an active Dino subscription plays the paid books
**Product:** Dino

*Planned 2026-11-24. Corrected the same day (owner: "no fucking owned books! sonos only works with subscriptions!").*

*Plan-cleanup 2026-11-25 (origin/main 1a8bf725): not started. `worker/sonos-link.ts` does not exist, `worker/sonos.ts` has no subscription check, no open Sonos PR, Slice 438 is still `planned`. The Why claims hold (`proof.ts`, `subscription.ts` exist). Verdict: keep open, C1-C6 all remain, nothing to move out.*

**Starts after:** Slice 438 merges (a start condition, not a wait inside the work).

**Depends on:** Slice 438 (the link and the per-home row). **Replaces:** Slice 415 item C's paid-track check on Sonos.

**Touches:** `worker/sonos.ts`, `worker/sonos.test.mjs`, `worker/sonos-link.ts`, `worker/sonos-link.test.mjs`,
`worker/entitlements/gate.ts`, `worker/sonos-labels.ts`, `src/Dino/DinoReader.App/SonosLinkPage.xaml.cs`, `PRIVACY.md`,
`site/dino/privacy.html`, `src/Dino/DinoReader.App/Purchases/**`, `src/Dino/DinoReader.App/Platforms/Android/Purchases/**`,
`src/Dino/DinoReader.App/Platforms/iOS/Purchases/**` (C4: re-send on renew or change), `tests/Dino/DinoReader.Tests/**`, this file.

*In progress (wave 2026-11-27), draft PR.*

## Why

Paid books fail on Sonos today (Slice 436, finding 1). Slice 415 settled how a speaker listener pays (owner,
2026-11-18): only the Dino subscription, bought in the Dino app, opens paid books on Sonos. The brief pasted with the
Claude Design canvas asked for "owned" books and an "Owned" subtitle; that was not the owner's decision and is not built.

On origin/main (2026-11-24): a store proof is the header `X-Purchase-Proof: <store>:<productId>:<token>`
(`worker/entitlements/proof.ts:5,9-20`); `gate.ts` checks it with Apple, Google or Microsoft;
`worker/entitlements/subscription.ts` returns the subscription's state and end date for the same proof.

## Decisions

- **Subscription only** (owner, 2026-11-18, Slice 415 decision 1; 2026-11-24: "sonos only works with subscriptions"):
  a single-book or bundle purchase opens nothing on Sonos; only an active Dino subscription does.
- Slice 415 Q-A answer (owner, 2026-11-18): a linked home without an active subscription (lapsed, or never bought)
  hears the free books, and a paid book plays the short spoken "subscribe in the Dino app" message instead of the story.
- Decided by this session: with an active subscription a paid book's subtitle drops the access word ("Ages 3–5 ·
  7 min"); without one it keeps Slice 437's "In the Dino app". Never "Owned".

## Work

- [ ] C1. Slice 438's confirm step stores the subscription proof's key for the home (only a subscription product's
      proof is accepted; single-book proofs are refused with a clear reason to the app).
- [ ] C2. `worker/sonos.ts`: for each paid track, the home's proof runs through the subscription check (cached as
      `gate.ts` does); active → playable; else `getMediaURI` returns the spoken subscribe message. `getMediaURI` checks
      again at play time.
- [ ] C3. The spoken "subscribe in the Dino app" message in all 15 languages (rendered once with the apps' narrator
      voices, served from R2).
- [ ] C4. The Dino app re-sends the proof when the subscription renews or changes, Android and iOS.
- [ ] C5. Tests: active subscription plays; lapsed, none and a single-book proof give the subscribe message.
- [ ] C6. Privacy text: the per-home row holds a subscription proof key.

## Verification

- `node --test worker/*.test.mjs`; owner's sandbox with a test subscription on the S21: a paid book plays; after the
  test subscription ends, the same book plays the subscribe message.
