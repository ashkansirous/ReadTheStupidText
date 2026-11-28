### Slice 479 — Dino: adding Dino Reader on Sonos opens the Dino app and finishes the link
**Product:** Dino

*Planned 2026-11-28 from the owner's Sonos test, not started.*

**Touches:** Part A: `site/dino/sonos-link.html` (not its Home and Get-the-app links, lines 34 and 41: Slice 475), `site/.well-known/assetlinks.json` (new), `site/.well-known/apple-app-site-association` (new), `wrangler.jsonc` and `wrangler.workers-dev.jsonc` (only if `/.well-known/*` needs a content type or `run_worker_first` entry), `worker/sonos-link.ts` (the `appUrl` only), `src/Dino/DinoReader.App/Platforms/Android/MainActivity.cs`, `src/Dino/DinoReader.App/Platforms/iOS/Entitlements.plist`, `src/Dino/DinoReader.App/Platforms/iOS/Info.plist`, `src/Dino/DinoReader.Core/SonosLink.cs` (the accepted link hosts only), `tests/Dino/DinoReader.Tests/**`, `worker/sonos-link.test.mjs`, `docs/DEPLOYING.md`, this file. Part B: `worker/sonos.ts`, `worker/sonos-link.ts`, `worker/sonos-fault-usage.ts` (new), `worker/sonos-fault-usage.test.mjs` (new), `worker/sonos.test.mjs`, `worker/sonos-link.test.mjs`, this file.

**Starts after:** Part A none (it overlaps Slice 466's draft PR #1437 only on `SonosLink.cs` and `worker/sonos-link.ts`; keep its edits there to the host list and the `appUrl` line). Part B after Slice 466 merges (same worker files).

## Why

Owner's test on 2026-11-28 with a real Sonos home: the old Sonos connection said it needs reauthorization and Authorize did nothing. After removing and re-adding the service and pressing "add to Sonos" and Authorize, Sonos showed "something went wrong" and kept loading forever. This happened on three tries, then with the newest Android tester build, then after buying the yearly subscription. **The Dino app never opened on the phone** (owner, 2026-11-28: "no it did not").

Found the same day (read from origin/main and live):
- Live D1 `dino-stats`: `sonos_homes` has **0 rows** and `sonos_pending_links` has 2, so no home ever finished linking. Without a linked home, every browse call faults `Client.AuthTokenExpired` (`worker/sonos-link.ts:372-376`). Sonos shows that as reauthorize or "something went wrong".
- Linking needs the Dino app to post `{op:"confirm"}`. The app is reached only through `site/dino/sonos-link.html`, which opens the custom scheme `dinoreader://sonos-link` (Android `intent:` URL, iPhone scheme then App Store). A verified https app link was a skipped follow-up of Slice 438 (review high #10). Sonos launches `appUrl` (the https page) itself, so the page's automatic jump to a custom scheme is where it stops. Exactly why (Sonos's own browser, no user tap, the intent filter) is item A1's job.
- `SonosLink.cs:16` accepts a pasted link page only on `dinoreader.sirous.uk`, while Sonos now gives `dino.elinsoft.co.uk` (Slice 467).
- The `proof` column is applied live (`PRAGMA table_info(sonos_homes)` lists `proof` and `proof_hash`), so it is not a cause.
- Missing subscribe clips (Slice 439 C3) cannot break the listing: they are read only on the media route.
- Nothing about linking or faults is logged: `handleSonos`'s catch is silent (`worker/sonos.ts:754-756`), and `dino_usage` held no Sonos row in the 48 hours before the test.

## Decisions (owner, 2026-11-28, RefineScope)

1. When linking, the Dino app must open on the phone. It didn't (owner's answer, above).
2. "Yes": a home that was linked before the subscription was bought unlocks by itself once the purchase goes through, with no second link. **This is built by Slice 466 item 1** (renew finds a home linked without a proof). This slice only checks it end to end in its Verification.
3. "ok": an old connection made before linking existed is sent into the new linking steps when Authorize is pressed. Authorize never does nothing.
4. "yes": the server records each Sonos linking step and every fault it returns, with no personal details: step name, fault code and app version only, never a household id, token, proof or link code.

## Work

### Part A: the link opens the Dino app (Android and iOS)

- [ ] A1. Reproduce first on the S21 (`device-test`, released tester build): add Dino Reader in the Sonos app on the S21 and record what opens (Sonos's own browser, Chrome, nothing) plus logcat for `uk.sirous.dinoreader` and the Sonos app. Then open the same link page address in Chrome on the S21. Write what each showed into this file. This decides between A2 alone and A2 plus an intent filter fix.
- [ ] A2. Make `appUrl` a verified https app link to the Dino app: Android App Links (`autoVerify` intent filter on `MainActivity` for `https://dino.elinsoft.co.uk/dino/sonos-link*` and `https://dinoreader.sirous.uk/dino/sonos-link*`, `site/.well-known/assetlinks.json` with the Play app signing and upload certificate SHA-256s) and iOS Universal Links (`applinks:` entries in `Entitlements.plist`, `site/.well-known/apple-app-site-association` served as `application/json`). With the app installed, Sonos opens the app directly. Without it, the same address shows the page, which already sends a family to the store. Confirm the manifest, entitlement and well-known shapes through `library-docs` first. Keep `dinoreader://sonos-link` working for links already handed out.
- [ ] A3. Add a large "Open Dino Reader" button on `sonos-link.html` that opens the app link on a tap, for a browser that blocks the automatic jump. The automatic jump stays.
- [ ] A4. `SonosLink.cs`: accept a link page from both `dinoreader.sirous.uk` and `dino.elinsoft.co.uk` (the app's own calls keep the old host, Slice 467's decision). Add a Core test for each host.
- [ ] A5. `docs/DEPLOYING.md`: where the two certificate fingerprints come from and how to check `https://dino.elinsoft.co.uk/.well-known/assetlinks.json` and `apple-app-site-association` live.

### Part B: old connections relink, and every step and fault is counted

- [ ] B1. Old connections (decision 3): find why Authorize did nothing for a pre-link token (A1's logcat plus the B2 rows). Make the reply to an old or unknown token the one Sonos documents as "authorize again" with AppLink (`Client.AuthTokenExpired`, then Sonos calls `getAppLink`), and check that Sonos calls `getAppLink` next. A worker test covers an old token getting that fault.
- [ ] B2. Counting (decision 4): new `worker/sonos-fault-usage.ts` writes one `dino_usage` row per link step and per fault through the shared `toDataPoint`, with platform `sonos` and event `sonos_link` (detail = `app_link`, `confirm_ok`, `confirm_refused`, `token_retry`, `token_ok`, `token_failed`, `refresh`) or `sonos_fault` (detail = the fault code, plus the SOAP method). It records the app version from the confirm body where there is one and nothing that names a home. It is off when `telemetry.enabled` is false, never throws and never delays the answer. Production only (`production-only-analytics`): Worker tests and the workers.dev config write nothing.
- [ ] B3. Record the new events in `worker/CLAUDE.md` through the `update-docs` index pass (its Sonos lines say nothing is logged today).

## Verification

- `node --test worker/*.test.mjs`; `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj`.
- Live: `curl -sI https://dino.elinsoft.co.uk/.well-known/assetlinks.json` and `.../apple-app-site-association` give 200 with `application/json`. On the S21, `adb shell pm get-app-links uk.sirous.dinoreader` shows both hosts `verified`.
- S21 with the real Sonos app on the owner's home, released build: adding Dino Reader opens the Dino app's Link screen, Connect returns to Sonos, the root lists books, and live D1 shows one `sonos_homes` row with a proof. The owner's yearly subscription unlocks paid books with no second link (decision 2, Slice 466 item 1). An old connection's Authorize leads to the same Link screen.
- iOS: on the simulator, the Universal Link opens the Link screen. The real-iPhone run goes to `docs/plan/unverified.md` tagged `[owner]` with the TestFlight build.
- `dino_usage` shows the `sonos_link` and `sonos_fault` rows from that run within minutes (`analytics-query`).
