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
   connected to the app").
5. **A second Sonos home replaces the first link** (user, 2026-11-18, answering O1: "replace"). Linking a new household
   with the same subscription removes the old household's link; the old home falls back to free books and the locked
   message.
6. **Alexa has the same limit: one Amazon account per subscription** (user, 2026-11-18, answering O2: "yes"), and by
   the same rule a new Alexa link replaces the old one.

User, 2026-11-18: "you can start doing the research now. dont do the implementation yet". Item A runs; B-G wait for the
user's go after the research is read.

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

## Research results (item A, 2026-11-18)

Read by a web-research agent; the decisive lines re-read from the live pages by the main session (curl, 2026-11-18).

**Sonos.**
- Anonymous is a supported choice with limits: "You can decide not to use any authentication, also known as anonymous
  access." It "limits the features available for your service"; the docs' own example is "anonymous access to offer
  a limited free trial" (https://docs.sonos.com/docs/add-authentication.md).
- With authentication, linking happens when the service is added: "When users add your service to their household,
  they enable Sonos to access their account. Sonos can send the user to your mobile app or to a webpage" (same page).
  No page read says one service can serve some households anonymously and link others, so **decision 2 (unlinked homes
  hear the free books) is not shown to be possible on Sonos**; read as written, an AppLink service makes every home link
  when adding it. Not stated anywhere read: mixing anonymous and linked content in one service, replacing a household's
  link.
- AppLink calls: `getAppLink` (request: householdId, hardware, osVersion, sonosAppName, callbackPath; result has a
  required `authorizeAccount`), `getDeviceAuthToken` (request: householdId, linkCode, linkDeviceId; returns `authToken`,
  "The authorization token for the household") (https://docs.sonos.com/docs/getapplink.md,
  https://docs.sonos.com/docs/getdeviceauthtoken.md).
- Faults for an unlinked or expired link: `NOT_LINKED_RETRY`, `AuthTokenExpired`, `LoginUnauthorized`
  (https://docs.sonos.com/docs/error-handling.md); no documented "play a message instead" for a locked track.

**Alexa.**
- "Child-directed skills – Account linking isn't supported for skills directed toward children."
  (https://developer.amazon.com/en-US/docs/alexa/account-linking/add-account-linking.html). Our skill is
  child-directed (`alexa/skill-package/skill.json`: `isChildDirected: true`), so **linking from the Dino app (decision 3)
  is not allowed on Alexa** while it stays a kid skill.
- The same page: "Non-child-directed skills that add account linking don't appear in the" parent dashboard.
- The only paid route Amazon documents for kid skills is its own In-Skill Purchasing: "At this time, ISP in kid skills is
  available only in the US", and Amazon "can't offer ISPs for a price higher than $9.99 USD"
  (https://developer.amazon.com/en-US/docs/alexa/in-skill-purchase/isp-kid-skills.html). That is a separate Amazon
  subscription, not the Dino app's.

**Questions these raise for the user (asked 2026-11-18, not defaulted):**
- **Q-A (Sonos):** decision 2 may not be possible. Choose: link required for every home (free books too, through the Dino
  app); or two Sonos services (an anonymous free one, a linked one for subscribers); or ask Sonos in case 09396143
  whether one service can mix the two, and decide after their answer.
- **Q-B (Alexa):** keep the kid skill with free books and locked paid ones (no subscription on Alexa); or make the skill
  not child-directed so it may link (it leaves the kids' category and the parent dashboard); or Amazon's own kid-skill
  purchase (US only, at most $9.99, sold by Amazon, separate from the Dino subscription).

**Answers (user, 2026-11-18):**
- **Q-A Sonos: "Link required for all".** Every Sonos home links through the Dino app when it adds the service, free
  books included. This replaces decision 2 on Sonos: there is no unlinked Sonos home any more. A linked home without an
  active subscription (lapsed, or a free-only Dino app) hears the free books and the locked message on paid ones.
  Consequence to settle in item C: linking needs the Dino app, so a family without the app cannot add the Sonos service.
- **Q-B Alexa: "Amazon's own purchase".** Alexa sells its own subscription through Alexa In-Skill Purchasing for kid
  skills; the skill stays child-directed and has no account linking. This replaces decisions 3 and 6 on Alexa (no Dino
  app link, so no Alexa link limit). Amazon's limits, from the page above: kid-skill ISP is "available only in the US",
  at most $9.99, purchases need a parent's approval.
- **Alexa outside the US (user, 2026-11-18): "we build for the fucking us and test on the fucking uk".** Amazon's
  page only describes the US; it does not say other countries are refused. Build the purchase for the US, then test it
  on the UK skill. Nothing is decided for the UK until that test shows what Amazon does there. (An earlier version of
  this answer said "Outside the US the skill keeps free books and the locked message"; that was the session's own
  inference, never the user's decision, and is withdrawn.)
  The Alexa price and plan are not yet chosen by the user (asked when item D starts).

## Work

**Sonos moved out (owner's Claude Design canvas `docs/design/sonos/`, 2026-11-24):** items B, C and E's Sonos link are
now Slice 438 (link once through the Dino app, optional, a story language per home; never-linked homes keep the
anonymous browse, which replaces "Link required for all") and Slice 439 (a linked home plays the paid books it owns,
single books included, which replaces decision 1 on Sonos; the owner checks 439 before it is built). This slice keeps
Alexa (D) and the Alexa parts of F and G.

- [x] **A. Research** the two facts above; write the answers and source lines into this file; ask any
      result that changes a decision as one question set. (Done 2026-11-18; answers above.)
- [ ] **B. Server link store** (`worker/speaker-link.ts`, D1 table in STATS), **Sonos only**: a link row holds the
      Sonos household id (hashed), the store proof's key (`proofKey`, never the raw token), created and last-checked
      times. One row per proof key; a new household with the same proof key replaces the old row (decisions 4, 5).
      Endpoints: start a link (from `getAppLink`, a short code), confirm a link (the Dino app sends the code plus its
      subscription proof), remove a link.
- [ ] **C. Sonos**: authentication changes from Anonymous to AppLink in the portal and the server (`getAppLink`,
      `getDeviceAuthToken`, `refreshAuthToken`); `getAppLink`'s `authorizeAccount` opens the Dino app; every paid track
      checks the household's link and runs the stored proof through the existing subscription gate (cached as `gate.ts`
      does); no active subscription: the locked spoken message. Single-book purchase proofs are ignored (decision 1).
      Settled (owner, 2026-11-24): a family without the Dino app gets the app's download page for their phone's
      system ("they should see the download page of the app on their phone with their os").
- [ ] **D. Alexa (built for the US, tested on the UK)**: an Alexa In-Skill Purchasing subscription product for the kid skill (price and period
      chosen by the user, at most $9.99); `worker/alexa.ts` asks Amazon whether the customer is entitled and plays paid
      books only then, else the locked message; the purchase offer follows Amazon's kid-skill rules (parent approval).
      The product is also offered in the en-GB locale, and the UK test (below) shows whether Amazon sells it there;
      the result is written here and taken to the user. Skill manifest (`allowsPurchases`) and testing instructions
      updated to match.
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
- Sonos sandbox: adding the service asks to link through the Dino app; a linked home without a subscription plays a
  free book and hears the locked message on a paid one; with an active subscription, linked from the Dino app on
  Android and on iOS, a paid book plays; a second household replaces the first link (the first then hears the locked
  message).
- Alexa (US): with no purchase, a paid book gives the locked message and the purchase offer; after a test purchase in
  the Alexa developer console, it plays.
- Alexa (UK): the same steps on the en-GB skill (developer console simulator, then a UK Echo): does Amazon offer the
  purchase, does a test purchase go through, does the paid book then play. Record exactly what Amazon shows.
- Android and iOS: the Link a speaker page on the emulator and the S21, and on an iOS simulator.
