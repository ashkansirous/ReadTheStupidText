# Slice 390 — Dino iOS purchases: StoreKit 2
**Product:** Dino

*Planned.*

**Touches:** `src/Dino/DinoReader.App/Platforms/iOS/Purchases/IosPurchases.cs` (and any new file beside it), `tests/Shared/Shared.Tests/FallbackGuardBaseline.txt` only if a catch changes, `docs/plan/slices/390-dino-ios-purchases-storekit-2.md`

## Why

`IosPurchases.cs` uses StoreKit 1, obsoleted from iOS 18. Slice 378 kept one file-scoped `CA1422` suppression there (`#pragma warning disable CA1422`, about 35 sites; see its Decisions) so the warnings gate could land. The Android purchase path is unchanged: this slice is iOS only because the obsoleted API exists only there.

## Work

- [ ] 1. Check the StoreKit 2 binding available to .NET for iOS with `library-docs` before writing code.
- [ ] 2. Move `IosPurchases` to StoreKit 2 with the same behaviour for the shared `IPurchases` contract (products, buy, restore, owned books, receipt sent to the server).
- [ ] 3. Remove the `CA1422` suppression; the build passes with the gate on.

## Verification

- `net10.0-ios` Dino builds with the gate on and no suppression.
- A sandbox purchase and a restore on a TestFlight build (a check for the user's iPhone, listed in Not yet verified when built).
