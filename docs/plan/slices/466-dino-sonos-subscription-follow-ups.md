# Slice 466 — Dino: Sonos subscription follow-ups from Slice 439's review
**Product:** Dino

*Planned 2026-11-27 in the docs index pass, from [Slice 439](439-dino-sonos-owned-books.md)'s *Low follow-ups* list and its *From review* follow-ups.*

*In progress (wave 2026-11-27), draft PR.*

**Depends on:** Slice 439 (merged as PR #1421). Its C3 clips are not part of this slice.

**Touches:** `worker/sonos.ts`, `worker/sonos.test.mjs`, `worker/sonos-link.ts`, `worker/sonos-link.test.mjs`,
`worker/sonos-labels.ts`, `worker/schema/sonos-homes.sql` (a previous-token column), `docs/DEPLOYING.md` (the column step),
`src/Dino/DinoReader.App/SonosLinkPage.xaml.cs`, `src/Dino/DinoReader.App/Purchases/**`,
`src/Dino/DinoReader.App/Platforms/Android/Purchases/**`, `src/Dino/DinoReader.App/Platforms/iOS/Purchases/**`,
`src/Dino/DinoReader.Core/SonosLink.cs`, `tests/Dino/DinoReader.Tests/**`, `PRIVACY.md` and `site/dino/privacy.html` (only if where the proof is kept changes), this file.

## Why

Slice 439 shipped with review findings it judged low (no crash, data loss, security, privacy, parity or loading-state fault) and left unbuilt. They are real rough edges for a grown-up using Sonos with a subscription, so each is written here once, with the review file that holds the finding.

## Work

- [ ] 1. Link a proof to a home that was linked without one: when the grown-up subscribes later, the app's renew path finds the home without needing the old proof, so no relink is needed (439 *Built*, "Known"; today the renew needs the old proof to find the home).
- [ ] 2. Accept the previous token on `/sonos/media` during a token refresh: keep the previous token hash and the time of the refresh (Slice 438's `refreshTokens` overwrites `token_hash` today), so a track queued before a refresh does not get 401 (439 *From review*, low follow-up #3, `reviews/s439/pr-1421-medium.md`).
- [ ] 3. Move the app's stored proof from Preferences to SecureStorage (Keystore on Android, Keychain on iOS), both apps, with the existing stored value migrated (`src/Dino/DinoReader.App/Purchases/SonosLinkedProof.cs:21`, `reviews/s439/pr-1421-check3-medium.md` #3).
- [ ] 4. Derive the catalogue poll token without per-home flags: `getLastUpdate` uses the per-isolate `subscriptionKnown` (false on a cold isolate) while browse asks the store, so the token can flip with nothing changed (`reviews/s439/pr-1421-medium.md` #6, repeated in `pr-1421-check-medium.md` #2).
- [ ] 5. With the paid-books switch Off the poll token never moves when the switch flips (`worker/sonos-link.ts:119`, `reviews/s439/pr-1421-check3-medium.md`, low follow-up a).
- [ ] 6. A 403 `subscription_inactive` on the first confirm retries with no proof silently and links the home free-only with no notice: tell the grown-up (`src/Dino/DinoReader.App/SonosLinkPage.xaml.cs:121`, same file, low follow-up b).
- [ ] 7. One 403 on renew settles the proof for 6 hours (`SettledFor`), so a lapsed-looking proof keeps a home on free books up to 6 hours after a brief store hiccup: shorten or retry (`reviews/s439/pr-1421-check4-medium.md`, low follow-up).
- [ ] 8. `SonosLinkPage.xaml.cs:98` shows a misleading message: reword it (`reviews/s439/pr-1421-check2-medium.md` #3).
- [ ] 9. `withHttpHeader` string-replaces `</getMediaURIResponse>` and does not check the replace matched: assert the match (`reviews/s439/pr-1421-check2-medium.md` #4).
- [ ] 10. Confirm that iOS `owned.Subscription(now)` returns a non-null `None` rather than null when nothing is owned (Android returns `SubscriptionStatus.None`); fix the iOS side if it returns null (`reviews/s439/pr-1421-check4-medium.md` #1).
- [ ] 11. An older overlapping store read can overwrite a newer proof until the next read: order the reads so the newest wins (`reviews/s439/pr-1421-check4-medium.md` #2).

## Verification

- `node --test worker/*.test.mjs` and the Dino test project cover items 1, 2, 4 to 9 and 11; item 3 has a migration test.
- Items 3, 6 and 10 on a device (S21 and an iPhone) are listed under *Not yet verified* when the build run does not reach them.

## Not yet verified

(Nothing built yet.)
