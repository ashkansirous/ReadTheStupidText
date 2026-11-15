# Slice 390 — Dino iOS purchases: StoreKit 2
**Product:** Dino

*Parked 2026-11-15 (night 2026-11-14-1547, wave 7): waits for the user's choice of route.*

**Park note.** Item 1's check (`library-docs`: context7 and the dotnet/macios wiki `BINDINGS` and `docs/native-library-interop.md`) found no StoreKit 2 binding in .NET for iOS: StoreKit 2 is Swift-only. Two routes: (a) our own Swift wrapper built into an XCFramework (needs a macOS/Xcode build step in CI), or (b) Plugin.InAppBilling 10.x (StoreKit 2 by default through its own Swift shim; its README says only Android and Windows are actively maintained). StoreKit 2 also hands out JWS signed transactions instead of the app receipt that item 2 says is "sent to the server", so the Worker's purchase check changes too. StoreKit 1 still works at runtime on iOS 18 (deprecation warning only, per dotnet/macios `src/storekit.cs`). It waits for the user's choice between a new third-party dependency and our own Swift code, for a real-money path no wave can test on an iPhone.

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
