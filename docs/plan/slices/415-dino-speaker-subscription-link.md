# Slice 415 — Dino: paid books on Sonos and Alexa through the subscription, linked from the Dino app
**Product:** Dino

*Planned 2026-11-18.*

**Touches:** `worker/sonos.ts`, `worker/sonos-soap.ts`, `worker/sonos.test.mjs`, `worker/alexa.ts`, `worker/alexa*.test.mjs`,
`worker/speaker-link.ts` (new), `worker/speaker-link.test.mjs` (new), `worker/index.ts`, `worker/schema/stats.sql` (the
link table), `alexa/skill-package/skill.json`,
`src/Dino/DinoReader.App/SpeakerLinkPage.xaml` (new), `src/Dino/DinoReader.App/SpeakerLinkPage.xaml.cs` (new),
`src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`, `src/Dino/DinoReader.App/Platforms/Android/AndroidManifest.xml`,
`src/Dino/DinoReader.App/Platforms/iOS/Info.plist`, `src/Dino/DinoReader.Core/SpeakerLink.cs` (new),
`tests/Dino/DinoReader.Tests/SpeakerLinkTests.cs` (new), `PRIVACY.md`, `site/dino/privacy.html`,
`docs/plan/slices/415-dino-speaker-subscription-link.md`

## Why

User, 2026-11-18 (after the Sonos sandbox add failed and the question of how a speaker listener pays): "plan a slice for
the paid account on sonos. I will not have buy book. just subscriptions on sonos and alexa. save my decision".

Today both speaker services are anonymous (Slice 283 Sonos, Slice 282 Alexa kid skill): they send no identity we can
tie to a store purchase. Slice 346 left Sonos and Alexa out of paid books on purpose (user, 2026-11-09: "we'll figure
sonos and alexa later"); its known limit is that the speaker catalogues list paid books while the listening-track route
refuses them. The subscription (Slice 384, £1.99 a month or £14.99 a year) is bought in the Dino app through Google
Play, the App Store or the Microsoft Store and proved to the server by a store proof (`worker/entitlements/proof.ts`,
checked by `worker/entitlements/gate.ts`).

## Decisions

User, 2026-11-18 (this session, Decision 175):
1. **Subscription only on Sonos and Alexa.** Buying a single book is not offered or honoured on a speaker; only an
   active Dino subscription opens paid books there.
2. **Unlinked homes get the free books and see the paid ones locked** (answer "Free books + locked paid"): every book
   is listed; a paid book played without a linked active subscription plays a short spoken "subscribe in the Dino
   app" message instead of the story.
3. **Linking goes through the Dino app**, no email or password: the speaker side shows a link or short code, the Dino
   app on the phone (which holds the store subscription) confirms it.
4. **At most one Sonos account per subscription** (user: "they should not be able to get more than 1 sonos account
   connected to the app"). Linking a second Sonos household replaces or is refused (see open point O1).

Not decided by the user (open points, answered before the claim, not defaulted):
- **O1.** A second Sonos household: refuse with a message, or replace the first link? The user said "not more than
  1"; which of the two is theirs to pick.
- **O2.** Does the one-link limit also apply to Alexa (one Amazon account per subscription)? The user named Sonos only.

## Facts to establish first (research items, written here before any design)

- Sonos AppLink for a content service: whether one service can mix anonymous free browsing with AppLink, or whether
  AppLink makes every household link before anything plays (decides how decision 2 is built on Sonos). Sources:
  docs.sonos.com `add-authentication`, `getappLink`, `getdeviceauthtoken`, `refreshauthtoken`.
- Alexa: whether a child-directed custom skill (`isChildDirected: true`, `alexa/skill-package/skill.json`) may use
  account linking at all, and what Amazon requires (kid-skill policy, parental consent). If it may not, Alexa keeps the
  free books only and paid books stay locked there; that result goes back to the user as one question.
- Sonos support case 09396143 (filed by the user 2026-11-18, see Slice 283): the sandbox add fails before any call to
  our endpoint. Its answer may change the authentication choice; this slice's server work waits on it only if Sonos
  says the sandbox needs AppLink to add at all.

## Work

- [ ] **A. Research** the two facts above; write the answers and source lines into this file; ask O1, O2 and any
      result that changes a decision as one question set.
- [ ] **B. Server link store** (`worker/speaker-link.ts`, D1 table in STATS): a link row holds the speaker kind
      (sonos | alexa), the speaker-side id (Sonos household id / Alexa user id, hashed), the store proof's key
      (`proofKey`, never the raw token), created and last-checked times. One Sonos row per proof key (decision 4).
      Endpoints: start a link (speaker side gets a short code), confirm a link (app sends the code plus its
      subscription proof), remove a link.
- [ ] **C. Sonos**: the AppLink calls the research picks (`getAppLink`, `getDeviceAuthToken`, `refreshAuthToken`
      as needed); every paid track checks the household's link and runs the stored proof through the existing
      subscription gate (cached as `gate.ts` does); unlinked or lapsed: the locked spoken message (decision 2).
      Single-book purchase proofs are ignored on Sonos (decision 1).
- [ ] **D. Alexa**: the same check in `worker/alexa.ts` through Alexa account linking if the research allows it; the
      locked message otherwise. Skill manifest and testing instructions updated to match.
- [ ] **E. Dino app, Android and iOS (same slice, both)**: a "Link a speaker" page in the grown-up area (Settings,
      "Books and buying" group of Slice 399) that opens from the speaker's link, shows which speaker it links, and
      confirms with the subscription proof; shows the linked Sonos home and an Unlink button. Only offered while a
      subscription is active. Windows: not in scope unless the user asks (the subscription is sold there too; noted
      for the user).
- [ ] **F. Privacy text**: `PRIVACY.md` and `site/dino/privacy.html` say what a link stores (a hashed speaker id and
      the subscription proof key, nothing about the listener) and how to remove it.
- [ ] **G. Store texts**: the Alexa skill description and the Sonos service text stop saying "nothing to buy" once
      paid books are reachable there.

## Verification

- Worker tests: link start/confirm/remove; one Sonos link per proof key; paid track with no link, a lapsed
  subscription and a single-book proof all give the locked message; an active subscription plays.
- Sonos sandbox: an unlinked household plays a free book and hears the locked message on a paid one; after linking
  from the Dino app on Android and on iOS, a paid book plays; a second household gets the O1 behaviour.
- Alexa (if linking is allowed): the same three checks on an Echo or the Alexa simulator.
- Android and iOS: the Link a speaker page on the emulator and the S21, and on an iOS simulator.
