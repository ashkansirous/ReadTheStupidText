# Slice 472: Dino and shared: route the two unrouted response-body reads through OpenBodyAsync

**Product:** Shared

*Planned 2026-11-28 by plan-cleanup, not started.*

Moved out of Slice 400 (plan-cleanup 2026-11-28). Slice 400's stream-close fix (PR #1240, e5ac6c92) routes download bodies through `OpenBodyAsync`; two small direct reads were left (review `reviews/s400-check3-medium.md` #5), still on origin/main 2026-11-28:

- `src/Shared/Sirous.Reading.Mobile.Shared/Usage/RemoteConfigClient.cs:287` (`response.Content.ReadAsStreamAsync(timeout.Token)`)
- `src/Dino/DinoReader.App/Purchases/SubscriptionEndClient.cs:48` (`response.Content.ReadAsStreamAsync(cancel)`)

## Work
- [ ] 1. Route both through `OpenBodyAsync` (same hazard class: closing an unread okhttp body on the UI thread). Small bodies, so low risk; no crash report names either (crash store 2026-11-28: no `FixedLengthSource` report since 1.123.1/1.131.1).
- [ ] 2. A unit test per call site where pure logic allows; fast checks only.

**Touches:** the two files above.
