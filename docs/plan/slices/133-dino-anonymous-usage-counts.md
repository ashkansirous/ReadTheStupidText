# Slice 133 — Dino: anonymous usage counts, first event end to end

*Planned 2026-10-25.*

**Batch:** 28 (Dino usage insights, Decisions 138-142)
**Touches:** `worker/events.ts` (new), `worker/index.ts`, `worker/*.test.ts`, `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `src/DinoReader.Core/UsageEvent.cs` (new), `src/DinoReader.App/UsageCounter.cs` (new), `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/ReadingPage.xaml.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/Platforms/iOS/Resources/PrivacyInfo.xcprivacy`, `src/DinoReader.App/Resources/Strings/*.resx`, `site/dino/privacy.html`, `PRIVACY.md`, `STORE.md`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`, `CLAUDE.md`

## Why

The user wants to know which books families like, in which language, and how parents use the store,
recordings and voices. Dino sends nothing today and its privacy page, `PRIVACY.md` and `STORE.md` say
so. Research and code check: [2026-10-25-dino-usage-insights](../notes/2026-10-25-dino-usage-insights.md).

## Decisions

- **Decision 140: anonymous counts only, no install or device id.** Each event carries only its name,
  context fields (book id for store/starter books, `own` for the parent's own books, language,
  source, has-recording, app version, platform) and a coarse date. No id, no typed text, no own-book
  titles, no timestamps finer than the hour. A per-device profile would be personal information
  about a child under COPPA (needs verifiable parental consent) and profiling under the UK Children's
  code; counts with context answer every question the user asked.
- **Decision 141: on by default, with an off switch for grown-ups** (settings, behind the gate) and one
  line in the first-run screens. Off means nothing is sent.
- **Decision 142: events go to Cloudflare Workers Analytics Engine** (dataset `dino_usage`, 3 months
  retention, free today; 100k points/day on Free, 10M/month on the $5 plan). The app batches a
  session's events into one `POST /api/events` (max 50 per request) at session end or next launch;
  the Worker drops everything when the config's `telemetry.enabled` is false and the app samples by
  `sampleRate` (Slice 132), so both can be turned down without a release.
- The privacy page, `PRIVACY.md`, `STORE.md`, the iOS privacy manifest and the store forms change in
  the same release that first sends events.

## Work

- [ ] Smallest end to end: `book opened` and `book finished` from the reader -> batch -> Worker ->
      Analytics Engine, honouring the config switch and sampling; a dashboard/SQL query shows them.
- [ ] Grown-up off switch and first-run line, strings in all Dino languages.
- [ ] Worker validation: known event names and fields only, length caps, rate cap per request; Dino
      host only; tests.
- [ ] Privacy texts: `site/dino/privacy.html`, `PRIVACY.md`, `STORE.md` (what is counted, no id, the
      off switch, Cloudflare as processor), iOS `PrivacyInfo.xcprivacy` (product interaction, not
      linked, not tracking).
- [ ] **Human:** Play Console Data safety (app activity, not linked to the user, not shared) and App
      Store privacy answers; Claude drives these in the browser where the user is signed in.

## Verification

- Core and Worker tests; emulator: read a book, events arrive in Analytics Engine (SQL API query);
  switch off in settings, nothing is sent; `telemetry.enabled=false` in R2, the Worker writes nothing.
