# App Store listing: Dino Book Reader - subscription version (en-US changes only)

**Draft 2026-11-15 (Slice 384 work item H). NOT pushed. Push only when Slice 384 is live on the App Store (the subscription group and both products approved and the app version that carries the subscription released); until then `appstore.md` is the live text.** Written from `play-subscription.md`. Plans from Slice 384 Decisions 2 and 5: $ prices are the store's own (UK 1.99 GBP a month, 14.99 GBP a year, set in App Store Connect), 7-day free trial once per Apple ID, single books still sold and kept, free and bought books never affected.
Apple requires, for an auto-renewing subscription: the price, the length, the trial and the renewal terms next to the buy button (the app's subscription card shows them), and a working link to the **Terms of Use (EULA)** and the **privacy policy** in the App Store description (or the EULA field) and in the app. The links are below; set them with the API (`appInfoLocalizations.privacyPolicyUrl`, and the description lines here), never by hand.

## Promotional text (replaces the line in `appstore.md` section 2; <= 170)

Stories read aloud to children aged 3 to 9, with a voice for every character and room for your own. No ads. Subscribe for every book, or buy only the ones you like.

## Description: the paragraph and the bullet that change

Replace the whole "STORIES AND PRICES" paragraph with:

STORIES AND PRICES
Four illustrated stories are free: The Lion and the Mouse, The Gingerbread Man, The Three Little Pigs and X Marks the Treasure. Subscribe for every other story, or buy only the stories you want and keep them. The subscription is monthly or yearly, starts with a 7-day free trial for new subscribers (once per Apple ID), and renews automatically at the price shown in the app until you cancel it in your Apple ID settings at least 24 hours before the end of the period. It opens every paid story while it runs; when it ends, stories you only had through it leave the child's shelf, and subscribing again or buying one brings it straight back. Stories you buy are yours to keep, with or without a subscription. A grown-up subscribes or buys behind the grown-ups gate, and can restore purchases at any time. Stories you already have stay on your device.

Replace the "Stories are bought through the App Store ..." bullet under MADE FOR KIDS with:

- Subscriptions and stories are bought through the App Store, by a grown-up behind the grown-ups gate. There is no account to create, and we store nothing about you or your purchase; the store's proof is checked and forgotten.

Add these two lines at the very end of the description (Apple's own check looks for them):

Terms of Use: https://www.apple.com/legal/internet-services/itunes/dev/stdeula/
Privacy Policy: https://dinoreader.sirous.uk/privacy

## In-app purchase metadata (made by `store-products.mjs`, not by this file)

- One subscription group "Dino Book Reader subscription" with `uk.sirous.dinoreader.sub.yearly` (level 1, ONE_YEAR) and `uk.sirous.dinoreader.sub.monthly` (level 2, ONE_MONTH), each with a ONE_WEEK free introductory offer; localisation name "Every book", description "Opens every book in Dino Book Reader while you subscribe." (`store-products.mjs`, dry run first; its Apple request shapes are unverified until a real run).
- The review screenshot of each subscription and the submission with an app version are the user's steps (no API).
