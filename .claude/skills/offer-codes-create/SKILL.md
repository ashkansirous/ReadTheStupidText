---
name: offer-codes-create
description: Create a new batch of one-time-use offer codes (free subscription codes for friends, testers or the owner) for Dino Reader and save every code in the secrets store. iOS only for now. Use when the user asks for "a new batch of codes", "more free codes", or when offer-codes-fetch says no unused codes are left.
---

# Create an offer code batch

Scripts: `tools/dino/offer-codes/` (run from the repo root of any checkout; `export MSYS_NO_PATHCONV=1` in Git Bash).
Codes are saved in `D:\secrets\dinoreader\offer-codes\` (`batches.json`, one `<platform>-<batchId>.csv` per batch,
`given.csv`); the scripts pull and push that repo themselves.

## iOS (App Store)

1. List the offers on Dino's yearly subscription: `node tools/dino/offer-codes/create-ios.mjs --list-offers`.
   The one-year-free offer is `c15fd602-0504-4fac-a0a7-20e90b5c650d` ("Gift Bahar", 1 year free).
   Use it unless the user names another offer; never create a new offer unless asked.
2. Create the batch:
   `node tools/dino/offer-codes/create-ios.mjs --offer <offerId> --count 500 --expires <YYYY-MM-DD>`
   - Apple makes one-time-use codes in batches of at least 500. That is normal: unused codes cost nothing and
     expire. Never ask the user about the batch size.
   - Expiry: the date the user gives; if none, one month from today. Apple allows at most about 6 months.
3. If the script says Apple returned no codes yet, wait a few minutes and run `--import <batchId>`.
4. Report one line: batch id, number of codes, offer name, expiry. Never print the codes themselves.

`--import <batchId>` also saves a batch made by hand in App Store Connect.

## Android, Windows

Not built yet. Say so in one line; do not improvise a route.
