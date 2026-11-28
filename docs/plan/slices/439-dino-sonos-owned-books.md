# Slice 439 — Dino: a linked Sonos home with an active Dino subscription plays the paid books
**Product:** Dino

*Planned 2026-11-24. Corrected the same day (owner: "no fucking owned books! sonos only works with subscriptions!").*

*Plan-cleanup 2026-11-28 (origin/main d3cb55e4): C1, C2, C4, C5, C6 built (PR #1421 merged). Only C3 is own work left: no render script exists in `tools/` (`git ls-tree origin/main` finds none), and `curl https://dinoreader.app/sonos/subscribe/en.m4a` answers 404, so no clip is uploaded. Verdict: keep open on C3 only. The *Low follow-ups* and *From review* items below are moved to Slice 466 (planned, PR #1437); the D1 `ALTER TABLE` is a `[session]` line in `unverified.md`, not slice work; the S21/Sonos, iOS and native-speaker checks stay in `unverified.md`.*

*Plan-cleanup 2026-11-25 (origin/main 1a8bf725): not started. `worker/sonos-link.ts` does not exist, `worker/sonos.ts` has no subscription check, no open Sonos PR, Slice 438 is still `planned`. The Why claims hold (`proof.ts`, `subscription.ts` exist). Verdict: keep open, C1-C6 all remain, nothing to move out.*

**Starts after:** Slice 438 merges (a start condition, not a wait inside the work).

**Depends on:** Slice 438 (the link and the per-home row). **Replaces:** Slice 415 item C's paid-track check on Sonos.

**Touches:** `worker/sonos.ts`, `worker/sonos.test.mjs`, `worker/sonos-link.ts`, `worker/sonos-link.test.mjs`,
`worker/entitlements/gate.ts`, `worker/sonos-labels.ts`, `src/Dino/DinoReader.App/SonosLinkPage.xaml.cs`, `PRIVACY.md`,
`site/dino/privacy.html`, `src/Dino/DinoReader.App/Purchases/**`, `src/Dino/DinoReader.App/Platforms/Android/Purchases/**`,
`src/Dino/DinoReader.App/Platforms/iOS/Purchases/**` (C4: re-send on renew or change), `tests/Dino/DinoReader.Tests/**`,
`worker/sonos-shelves.ts` (a subscribed home's paid books stay off the free shelf), `worker/schema/sonos-homes.sql` (a `proof` column), `worker/index.ts` (hands the subscription source to `handleSonos`),
`docs/DEPLOYING.md` (the column step), `src/Dino/DinoReader.Core/SonosLink.cs` (`ProofStoredOf`),
`src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Resources/Strings/AppResources.cs`
(the Link screen's busy and Try again strings; widened by the wave 2026-11-27 coordinator after the check passes, no other open PR), this file.

*Built in part 2026-11-27, merged as PR #1421 (150e676b). Left: the C3 render and upload, and the live checks.*

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
- Decided by the wave 2026-11-27 coordinator (the builder found Slice 438's row keeps only `proof_hash`, and C2 must ask
  Apple or Google at play time, which needs the proof itself):
  - The per-home row (and the pending-link row) gets a `proof TEXT` column holding the proof as the app sends it
    (`<store>:<productId>:<token>`); `proof_hash` stays for the one-home-per-subscription index and unlink. Why plain and
    not encrypted: C6 already says the row holds the proof key, the database is reachable only by the Worker, and
    encrypting needs a new Worker secret that a night run cannot set. Encrypting it with a Worker secret is a possible later change.
  - The column goes into the `CREATE TABLE` in `worker/schema/sonos-homes.sql`, and `docs/DEPLOYING.md` gets the
    `ALTER TABLE ... ADD COLUMN proof TEXT` line for a database where the table already exists.
  - The subscription source built in `worker/index.ts` (`paidGateFor`) is handed to `handleSonos` through a new optional
    field on `SonosLinks`, never rebuilt in `sonos.ts`.
  - C4 uses a new link op `renew {oldProof, newProof}` that replaces the stored proof (and its hash) of the home holding the old one.
- Decided by the wave 2026-11-27 coordinator, after review 1 (`reviews/s439/pr-1421-high.md`):
  - **The proof never leaves the Worker (finding 2).** `getMediaURI` for a paid book returns the Worker's media URL `/sonos/media/<language>/<storyId>.m4a`, and its `httpHeaders` carry `Authorization: Bearer <the home's own Sonos auth token>` (Sonos already holds it), never the proof. The media route finds the home by that token's hash, runs the subscription check with the stored proof server-side and streams the file (an active or grace subscription) or the subscribe clip. The plain `proof` column at rest stays as decided (Worker-only database). `getMediaURI` itself never calls the store.
  - **Renew (finding 1):** it replaces the stored proof only when the old proof matches a home AND the new proof passes the store's subscription check (active or grace, a subscription product); otherwise it refuses (403 inactive, 429 over the limit, 503 store unavailable, 409 when another home holds the new proof) and changes nothing. It never deletes another home.
  - **Confirm (finding 3):** the store confirms the proof (active or grace) before it is stored; the store-call limit is keyed as `gate.ts` keys its own (the connection's `CF-Connecting-IP`, else `unknown`), never on the home hash.
  - **The `proof` column not yet added (finding 4):** reading a home from a database without the column treats the home as having no proof and logs `fallback=no-proof-column` (name only); it never faults the home.
  - **Texts (findings 5, 6):** the privacy texts and the schema and file comments say the proof is kept for the Worker's own store check only and is never sent to Sonos.
  - **Findings 7, 8:** the app sets the linked proof to what the link sent (removes it when none), clears it only on the Worker's `unknown_home` answer, drops the cached subscription proof when a store read finds the subscription lapsed, always re-reads the store before linking, and traces a failed renew task.
  - **Paid books are `canPlay=true` for a linked home (finding 9)** so the subscribe clip is reachable from browse; an anonymous browse keeps them unplayable.
  - **A store error or rate limit keeps the home's last real answer for 10 minutes; `getLastUpdate` never calls the store (finding 10)**, it uses the last real answer (false when none).
  - **A subscribed home's paid books are not on the "Free stories" shelf (coordinator item 11; `worker/sonos-shelves.ts`).**

## Work

- [x] C1. Slice 438's confirm step stores the subscription proof's key for the home (only a subscription product's
      proof is accepted; single-book proofs are refused with a clear reason to the app).
- [x] C2. `worker/sonos.ts`: for each paid track, the home's proof runs through the subscription check (cached as
      `gate.ts` does); active → playable; else `getMediaURI` returns the spoken subscribe message. `getMediaURI` checks
      again at play time.
- [~] C3 (code and texts built; the clips are not rendered or uploaded, see Not yet verified). The spoken "subscribe in the Dino app" message in all 15 languages (rendered once with the apps' narrator
      voices, served from R2).
- [x] C4. The Dino app re-sends the proof when the subscription renews or changes, Android and iOS.
- [x] C5. Tests: active subscription plays; lapsed, none and a single-book proof give the subscribe message.
- [x] C6. Privacy text: the per-home row holds a subscription proof key.

## Verification

- `node --test worker/*.test.mjs`; owner's sandbox with a test subscription on the S21: a paid book plays; after the
  test subscription ends, the same book plays the subscribe message.

## Not yet verified

- `[session]` C3 clips (a render run with the narrator voices, then an upload to R2 `dinoreader/sonos/subscribe-<lang>.m4a`; nothing was rendered or uploaded; C3 stays open on this wait): the 15 texts are `SUBSCRIBE_MESSAGES` in `worker/sonos-labels.ts`; each clip is an AAC `.m4a` uploaded to R2 as
  `dinoreader/sonos/subscribe-<language>.m4a` (steps in `docs/DEPLOYING.md`, *The subscription proof column and the spoken message*). The render script itself (under `tools/dino/`) is outside this slice's Touches and is not written.
- `[session]` D1: apply the `ALTER TABLE ... ADD COLUMN proof TEXT` lines and the index lines in `docs/DEPLOYING.md` to the live STATS database before the batched release serves Sonos (not applied yet; this adds to the `sonos-homes.sql` step from Slice 438).
- `[owner]` Device and Sonos sandbox (the Verification line above: the Sonos sandbox and S21 test-subscription run): that Sonos sends the `Authorization: Bearer <token>` from `getMediaURI`'s `httpHeaders` to the media route and plays the story (the S21 with a test subscription; a refreshed token is picked up by the next `getMediaURI`, a track already queued with the old token gets 401), that the message clip plays for a lapsed home, and that a renewal on the S21 reaches the Worker (Debug log `[SonosLink] proof renew Renewed`).
- `[cloud]` iOS: the shared `BookPurchases` code compiled for Windows and Android only; an iOS compile and a TestFlight run are owed (no Mac here).
- `[owner]` A native-speaker read of the 15 subscribe texts (`SUBSCRIBE_MESSAGES` in `worker/sonos-labels.ts`).

Checked this wave: worker tests 646, Dino 5199, Shared 1745, all pass; the Dino Windows head builds. Emulator flows `sonos-link-screen` PASS (31.0 s) and `first-run` PASS (28.9 s), no crash (log: `D:/ReadTheStupidText-worktrees/reviews/s439-device/flow-dino.log`).

## Built (wave 2026-11-27)

- A home with an active (or grace) subscription sees paid books as playable, subtitle without the access word, and every linked home's paid book answers `getMediaURI` with the Worker's media URL plus the home's own Sonos token as `httpHeaders` (see Decisions, after review 1); the media route streams the story for an active subscription and the spoken message (`/sonos/subscribe/<language>.m4a`'s file) otherwise.
- Known: a home linked without a subscription needs a relink after the grown-up subscribes (the renew needs the old proof to find the home).

#### Low follow-ups (moved to Slice 466, 2026-11-28; they never keep this slice open)

- A home linked without a subscription needs a relink after the grown-up subscribes.
- Accept the previous token on `/sonos/media` during a token refresh.
- Move the app's stored proof to SecureStorage.
- The rest of the low follow-ups written in *From review* below.

#### From review

- Skipped #6 (`reviews/s439/pr-1421-medium.md`): "`isSubscribed` answers getLastUpdate from per-isolate `subscriptionKnown` (else false) while browse uses the real store answer; the catalogue token includes `unlocked` and `subscribePrompt`." - an isolate-local flip only makes Sonos refetch the catalogue, with no wrong answer; low follow-up (derive the poll token without per-home flags).
- Skipped #2 (`reviews/s439/pr-1421-check-medium.md`): "getLastUpdate builds its token from `subscriptionKnown` (per-isolate in-memory map, false on a cold isolate) while browse used `subscriptionActive` (asks the store), so the token differs between calls with nothing changed." - repeats medium #6 above, already skipped: an isolate-local flip only makes Sonos refetch the catalogue.
- Skipped #8 (`reviews/s439/pr-1421-medium.md`): "The full subscription purchase token is now stored in D1 in plain text (previously only hashes)." - plain storage at rest is the slice's Decision ("Decided by the wave 2026-11-27 coordinator": the plain `proof` column); PRIVACY.md says so.
- Low follow-up (#3, `reviews/s439/pr-1421-medium.md`, not built): Slice 438's `refreshTokens` keeps no previous token hash or time (it overwrites `token_hash`), so `/sonos/media` cannot accept the previous token without a new column. `getMediaURI` is called right before play, so the exposed window is a token refresh between the two calls (a track queued before a refresh gets 401 until Sonos asks `getMediaURI` again).
- Review 2 behaviour to know: a store that cannot be asked, with no kept answer, now answers the media route 503 instead of the message. Review 3 changed the switch-off case: with the paid-books switch off a linked home's paid books play like everyone else's (the media route and the browse unlock them), with or without a proof; confirm answers `proofStored` and the app remembers the proof only when it is true; a Range or HEAD request uses the kept answer (10 minutes) and does not ask the store.
- Skipped #3 (`reviews/s439/pr-1421-check2-medium.md`): "src/Dino/DinoReader.App/SonosLinkPage.xaml.cs:98, nit (misleading message)." - a wording follow-up (low; no crash, data-loss, security, privacy, parity or loading-state fault).
- Skipped #4 (`reviews/s439/pr-1421-check2-medium.md`): "withHttpHeader string-replaces `</getMediaURIResponse>` and does not check the replace matched." - low follow-up: the replace has a test path (the getMediaURI tests read the `httpHeaders` entry); asserting the match is not built.
- Skipped #3 (`reviews/s439/pr-1421-check3-medium.md`): "src/Dino/DinoReader.App/Purchases/SonosLinkedProof.cs:21, nit (privacy/security)." - low follow-up: the token sits in the app's private Preferences on the device; moving it to SecureStorage (Keystore/Keychain) is not built.
- Low follow-up (a, `reviews/s439/pr-1421-check3-medium.md`, not built): "worker/sonos-link.ts:119 with the paid switch Off, subscriptionKnown stays false so the poll token never moves when the switch flips".
- Low follow-up (b, `reviews/s439/pr-1421-check3-medium.md`, not built): "src/Dino/DinoReader.App/SonosLinkPage.xaml.cs:121 a 403 subscription_inactive on the first confirm retries with no proof silently and links free-only with no notice".
- Skipped check4 #1 (`reviews/s439/pr-1421-check4-medium.md`): "Android returns SubscriptionStatus.None; not confirmed that iOS owned.Subscription(now) returns non-null None rather than null." - low follow-up after the fourth check pass: unconfirmed, and keeping the proof is the safe side; the Worker re-checks with the store anyway.
- Skipped check4 #2 (`reviews/s439/pr-1421-check4-medium.md`): "an older overlapping read can overwrite a newer proof until the next read." - low follow-up: a narrow race between two store reads, fixed by the next read.
- Low follow-up (check4, `reviews/s439/pr-1421-check4-medium.md`, not built): "one 403 on renew settles the proof for 6 hours (SettledFor), so a home with a lapsed proof stays on free books up to 6 hours after a brief store hiccup".
- Review 4 (check 3) behaviour to know: calls from Sonos (browse, getMediaURI, the media route) key the store-call limiter on the linked home (`home:<homeHash>`), the app's confirm and renew stay on the client IP; a store read with no subscription answer keeps the remembered proof, only a reachable "not entitled" read drops it.
