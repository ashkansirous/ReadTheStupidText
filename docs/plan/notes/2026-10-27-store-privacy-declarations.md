# Store privacy declarations (2026-10-27)

Moved verbatim from `plan.md` *Outstanding work* by the plan cleanup of 2026-11-25. Every row is ticked; done 2026-10-26/27 (PR #487).

**Store privacy declarations (legal; gates all usage counts, Decision 145).** No `telemetry.enabled` is set to `true` for an app until its rows are ticked. Claude drafts the answers and fills the forms in the user's signed-in Chrome; the user reviews and presses Submit/Publish.

- [x] RTST repo texts: `PRIVACY.md`, `site/privacy.html` (deployed), `STORE.md`, iOS `PrivacyInfo.xcprivacy` (Slice 137; Windows sources in Slice 140; checked 2026-10-26: phone and Windows covered). Claude.
- [x] RTST Google Play Console: Data safety (app activity and app info/performance, not linked to the user, not shared, optional to turn off). User submits. Submitted/published 2026-10-27.
- [x] RTST Apple App Store Connect: App Privacy (usage data and diagnostics, not linked, no tracking). User publishes. Submitted/published 2026-10-27.
- [x] RTST Microsoft Partner Center: privacy policy URL and the data-collection answers. User submits. Privacy URL already correct.
- [x] Dino Google Play Data safety and Apple App Privacy for its usage counts (Slices 133-134, still pending). User submits. Submitted/published 2026-10-27.
- [x] Then: enable Analytics Engine, add the bindings, deploy, set `telemetry.enabled` per app. Done 2026-10-27 (PR #487).
