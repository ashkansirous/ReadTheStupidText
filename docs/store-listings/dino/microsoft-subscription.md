# Microsoft Store listing: Dino Book Reader - subscription version (English changes only)

**Draft 2026-11-15 (Slice 384 work item H). NOT pushed. Push only when Slice 384 is live in the Microsoft Store (the two subscription add-ons published); until then `microsoft.md` is the live text.** Written from `play-subscription.md`. Plans from Slice 384 Decisions 2 and 5: 1.99 GBP a month, 14.99 GBP a year (the Store converts other markets), 7-day free trial, single books still sold and kept, free and bought books never affected. The German text follows this one by the same edit when the user approves the English.

The "No ads, no in-app purchases" bullets of the live listing are already replaced by `microsoft.md`; these replace that file's version.

## MADE FOR KIDS bullets

    - No ads.
    - Subscriptions and stories are bought through the Microsoft Store, by a grown-up behind the grown-ups gate. There is no account to create, and we store nothing about you or your purchase; the store's proof is checked and forgotten.

## STORIES AND PRICES paragraph

    STORIES AND PRICES
    Four illustrated stories are free: The Lion and the Mouse, The Gingerbread Man, The Three Little Pigs and X Marks the Treasure. Subscribe for every other story on the built-in story shelf, or buy only the stories you want and keep them. The subscription is monthly or yearly, starts with a 7-day free trial, and renews automatically at the price shown in the app until you cancel it in your Microsoft account (https://account.microsoft.com/services). It opens every paid story while it runs; when it ends, stories you only had through it leave the child's shelf, and subscribing again or buying one brings it straight back. Stories you buy are yours to keep, with or without a subscription. A grown-up subscribes or buys behind the grown-ups gate and can restore purchases at any time. Stories you already have stay on your device.

Feature line "No ads, no account, buy a story only if you want it" becomes "No ads, no account, subscribe or buy a story only if you want it". Feature line "Four free stories, the rest $0.99 each" becomes "Four free stories, a subscription for the rest".

Privacy policy link (Partner Center > Properties): https://dinoreader.sirous.uk/privacy

## Add-ons (made by `store-products.mjs`, then finished in Partner Center by hand)

Two subscription add-ons `uk.sirous.dinoreader.sub.monthly` and `uk.sirous.dinoreader.sub.yearly`: the submission API cannot set a subscription's period, price or trial, so those, the add-on listing and its submission are the user's steps in Partner Center (the script's dry run lists them).
