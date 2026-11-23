---
name: offer-codes-fetch
description: Hand out 1-3 unused Dino Reader offer codes (a free subscription code for a friend, tester or the owner) from the saved batches and record them as given. iOS only for now. Use when the user says "give me a code", "another token for the free year", "I need 2 codes".
---

# Fetch offer codes

Run from the repo root (`export MSYS_NO_PATHCONV=1` in Git Bash):

```
node tools/dino/offer-codes/fetch.mjs --platform ios --count <1-3> [--note "<who it is for>"]
```

- Give **exactly the number asked for** (1 if unsaid). Never add spares.
- The script picks the next unused code from live batches (soonest expiry first), records it in
  `D:\secrets\dinoreader\offer-codes\given.csv`, commits and pushes the secrets repo. One run at a time (a lock in
  `D:\secrets\.git`), so this machine never hands a code out twice. Batches with under 2 days left are skipped.
- "push to the secrets repo failed": the codes printed are still valid and recorded locally; give them to the
  user, and say in one line that the secrets repo needs a push (`git -C D:\secrets push`).
- "Only N unused codes left": run the `offer-codes-create` skill for a new batch, then fetch again.
- `--stock` shows unused codes per batch; `--mark-given <code>` records a code handed out some other way.

Reply to the user with only, per code: the code, its redeem link, the redeem-by date. Optionally one line on how to
redeem (tap the link on the iPhone, or App Store > profile picture > Redeem Gift Card or Code). Nothing else.

Android and Windows: not built yet; say so in one line.
