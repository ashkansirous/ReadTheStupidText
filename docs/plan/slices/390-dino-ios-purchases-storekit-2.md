# Slice 390 — Dino iOS purchases: StoreKit 2
**Product:** Dino

*Parked 2026-11-15 (night 2026-11-14-1547, wave 7): waits for the user's choice of route.*

**Decision (user, 2026-11-15): route (a)**, our own small Swift wrapper over StoreKit 2 (products, buy, restore, owned books), built into an XCFramework by a macOS/Xcode CI step and called from `IosPurchases.cs`; no Plugin.InAppBilling. The Worker's purchase check takes StoreKit 2's signed transaction (JWS) for iOS. The sandbox buy and restore on the user's iPhone goes to Not yet verified.

**Park note.** Item 1's check (`library-docs`: context7 and the dotnet/macios wiki `BINDINGS` and `docs/native-library-interop.md`) found no StoreKit 2 binding in .NET for iOS: StoreKit 2 is Swift-only. Two routes: (a) our own Swift wrapper built into an XCFramework (needs a macOS/Xcode build step in CI), or (b) Plugin.InAppBilling 10.x (StoreKit 2 by default through its own Swift shim; its README says only Android and Windows are actively maintained). StoreKit 2 also hands out JWS signed transactions instead of the app receipt that item 2 says is "sent to the server", so the Worker's purchase check changes too. StoreKit 1 still works at runtime on iOS 18 (deprecation warning only, per dotnet/macios `src/storekit.cs`). It waits for the user's choice between a new third-party dependency and our own Swift code, for a real-money path no wave can test on an iPhone.

**Decision (two strikes on unverified entitlements, 2026-11-15):** unverified transactions are ignored, as Apple's docs say. An unverified StoreKit 2 transaction grants nothing and is left out; the verified entitlements are the whole truth of a read, no last-read merge and no keep-the-last-snapshot rule. A read with an unverified entry reports one store fallback event and still returns a Reachable listing of the verified ones. A book whose transaction cannot be verified shows Buy, and the store answers already-owned / Restore.

**Touches:** `src/Dino/DinoReader.App/Platforms/iOS/Purchases/IosPurchases.cs` (and any new file beside it), `tests/Shared/Shared.Tests/FallbackGuardBaseline.txt` only if a catch changes, `docs/plan/slices/390-dino-ios-purchases-storekit-2.md`

## Why

`IosPurchases.cs` uses StoreKit 1, obsoleted from iOS 18. Slice 378 kept one file-scoped `CA1422` suppression there (`#pragma warning disable CA1422`, about 35 sites; see its Decisions) so the warnings gate could land. The Android purchase path is unchanged: this slice is iOS only because the obsoleted API exists only there.

## Work

- [x] 1. Check the StoreKit 2 binding available to .NET for iOS with `library-docs` before writing code. (Done in the park note; context7 `/dotnet/macios` re-checked 2026-11-15: no StoreKit 2 binding, native frameworks load through `NativeReference` + `NativeLibrary`.)
- [x] 2. Move `IosPurchases` to StoreKit 2 with the same behaviour for the shared `IPurchases` contract (products, buy, restore, owned books, receipt sent to the server). Built: `Platforms/iOS/StoreKit2` (Swift file, C functions via `@_cdecl`, built into `DinoStoreKit.xcframework` by `build-xcframework.sh`), `StoreKit2Bridge.cs`, `IosPurchases.cs`, pure `DinoReader.Core/StoreKit2Answers.cs`; the proof is the transaction's JWS and the Worker (`worker/entitlements/apple-jws.ts`, `apple.ts`) verifies it to Apple Root CA - G3, still accepting plain transaction ids from older builds.
- [x] 3. Remove the `CA1422` suppression; the build passes with the gate on. (`net10.0-ios` Debug compiles on Windows with the gate; the macOS jobs still owe the XCFramework and signed build.)
- [ ] 4. The macOS CI jobs `dinoreader-ios.yml` and `dinoreader-ios-sim.yml` pass on the branch (they build the XCFramework first).

## Verification

- `net10.0-ios` Dino builds with the gate on and no suppression.
- A sandbox purchase and a restore on a TestFlight build (a check for the user's iPhone, listed in Not yet verified when built).

#### Changed while building

- CI run 43711519466 failed: `xcodebuild archive` of the Swift package put its product in usr/local/lib, not in a framework. Packaging is now a static library: `swiftc -emit-library -static` per SDK into `DinoStoreKit.xcframework` (`-library`), `Package.swift` removed, the csproj links it as a `Static` NativeReference (ForceLoad, `LinkWithSwiftSystemLibraries`), and `StoreKit2Bridge.cs` calls the exports through `DllImport("__Internal")`.

#### Not yet verified

- The Swift wrapper and the XCFramework have never been compiled (Windows has no Xcode): `dinoreader-ios.yml` and `dinoreader-ios-sim.yml` on the branch are the first proof.
- Sandbox buy and restore on the user's iPhone with a TestFlight build (a paid book and a subscription plan; Restore on a second install).
- The Worker's JWS check against a real Apple StoreKit 2 transaction (tests use a throwaway chain; the pin is the SHA-256 of Apple Root CA - G3).

## From review

Files: `reviews/s390-high.md` (pass 1) and `reviews/s390-medium.md` (pass 2).

- High #1 (hasOid searched the whole TBS): applied; the marker OID is read from the certificate's extensions only.
- High #2 (redeem timeout opens the web page over the sheet): applied; no timeout, and medium #5 narrows the web fallback further.
- High #3 (revoked subscription transaction): applied; revoked transactions are dropped in `StoreKit2Answers.Entitlements`.
- High #4 (transaction update only clears pending): applied; the update raises `PurchaseUpdates.StoreChanged` and BookPurchases re-reads.
- High #5 (exception in the native callback): applied; `OnAnswer` catches everything and hands updates to the thread pool.
- High #6 (price query and entitlements read in sequence): skipped: low, an offline wait only, no wrong result.
- High #7 (AskAsync timer and waiting entry): applied; the timer is cancelled on every path and an abandoned request leaves the waiting list.
- High #8 (Sandbox JWS refused in production): superseded by medium #1. A verified Sandbox JWS is accepted only where `APPLE_IAP_ALLOW_SANDBOX` is set, so the TestFlight Sandbox check in Not yet verified must target a Worker with that switch (staging); the production Worker refuses it by design.
- High #9 (duplicate JWS test, mutable trusted roots): applied; `isJws` is shared and the roots are a `createAppleChecker(roots)` argument.
- High #10 (IBookPurchases summary): applied; the paragraph now says iOS reads Apple's entitlements silently.
- Medium #1 (Sandbox JWS accepted on production): applied; the same rule as a plain transaction id (slice 346), tests changed to the opposite.
- Medium #2 (non-array `transactions` read as "owns nothing"): applied; it is a failed read (null).
- Medium #3 (unverified entitlement skipped silently): applied; the Swift answer carries `incomplete`, C# treats it as a failed read, so BookPurchases keeps the last snapshot and reports `StoreUnreachable` when online.
- Medium #4 (BuyAsync cancel): applied differently: no `Abandon` (it would discard the late answer); a caller cancel hands the request to `DeliverLateAnswerAsync`, which raises `PurchaseUpdates` when the purchase completes, so the book unlocks; the bridge removes the waiting entry when the answer arrives.
- Medium #5 (redeem opens the web page on any failure): applied; the page opens only when the bridge is missing or the answer is `unsupported`.
- Medium #6 (updates listener starts late): applied; `AppDelegate.FinishedLaunching` loads the bridge, which starts the listener.
- Medium #7 (static handler never unsubscribed, `_lastListed` unlocked): applied; `BookPurchases` is `IDisposable` and unsubscribes, `_lastListed` is read and written under a lock.
- Medium #8 (failed JWS cached as not owned): applied; a JWS that fails verification is `CANNOT_VERIFY` for books too; a verified one that is not ours stays "not owned".

Third pass (`reviews/s390-check.md`):

- #1 (one unverified entitlement fails the whole read): applied; the read keeps the verified ones, `StoreKitEntitlements.KeepingKnown` adds the last read's books for products it cannot see, and `BookPurchases` reports one `StoreFailed` per change. This replaces the "failed read" rule of medium #3 above.
- #2 (Sandbox JWS refused is cached as not owned): applied; it is `CANNOT_VERIFY` (not cached). The production Worker has `APPLE_IAP_ALLOW_SANDBOX` set (`wrangler secret list`, 2026-11-15); the switch rule stays.
- #3 (updates before the first read): applied; `PurchaseUpdates` notes an update raised with no listener and `BookPurchases` marks its read stale when it subscribes.
- #4 (static `ProductUpdated` never unsubscribed): applied; `IosPurchases` and `SwitchablePurchases` are `IDisposable`.
- #6 (dead catch in `TryCreate`): applied; comment corrected, the updates start is inside the same guard, the no-bridge case is still handled by every caller.
- #7 (late-answer task never ends): applied; it times out after 15 minutes or on dispose and drops its waiting entry.
- Skipped #5 (`reviews/s390-check.md`): "every JWS verification failure (forgery included) is now CANNOT_VERIFY, so forged proofs are never negatively cached; repeated full chain verification costs CPU only" - CPU only, no wrong answer.
- Skipped #8 (`reviews/s390-check.md`): "arm64 device and simulator slices only; an x64 simulator build fails to link (workflows pin arm64; same limit as before)" - arm64-only as before.

Fourth pass (`reviews/s390-check2.md`):

- #1 (`KeepKnown` merges the last read without checking the Apple ID): fixed by removal; `KeepKnown`, `_lastRead` and `KeepingKnown` are gone (the Decision above).
- #2 (`_lastRead` memory-only, cold start returns a snapshot missing the unverified book): fixed by the same removal; the verified entries are the whole read.
- #3 (late answer gives up after 15 minutes, no re-read): applied; a late answer, and the timeout, both raise `PurchaseUpdates` so the store is read again (`IosPurchases.DeliverLateAnswerAsync`).
- #4 (`TryCreate` catches only two exception types): applied; it catches every exception, so `Load()` on the launch path never throws.
- #5 (`TakeMissed` only marks stale): applied; a missed update marks stale and starts the re-read (`BookPurchases.ListenForStoreUpdates`); the launch listener's updates reach it through `PurchaseUpdates.Raise`.
- Skipped #6 (`reviews/s390-check2.md`): "verified Sandbox JWS without switch returns CANNOT_VERIFY, never cached; repeated full chain verification (CPU only, no entitlement granted)" - CPU only, no wrong answer.

**Park note (moved back to planned by the 2026-11-14-1547 feedback loop, 2026-11-15):** the row was `parked: waits for the user's choice of StoreKit 2 route (own Swift XCFramework or Plugin.InAppBilling 10.x)`.
