# Slice 133 — Dino: anonymous usage counts, first event end to end

*Built 2026-10-26 (PR #365), not yet verified: the Analytics Engine binding is not switched on and nothing has run on a device.*

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

#### Design (Dino Reader Batch 26-28 mock, section 26-28, added 2026-10-25)

Frames 62 (counts switch) and 63 (first-run line), lines 76 and 96 of `docs/design/dino-reader/Dino Reader Batch 26-28.dc.html`. The mock agrees with the plan. Frame 62's first card (App language, Narrator) is filler.

- **Counts switch (frame 62, `SettingsRows`).** Own white card (radius 18, min 64px), title "Help make Dino better" (15px, 600), helper "Sends counts like "a store book was finished". No names, no ids, nothing you typed." (12.5px, `#5a5470`). Switch 52x32, on state `#5B57E8` with a 26px white knob on the right. On by default (Decision 141); off sends nothing. Placement is a proposal, pending a grown-up settings mock.
- **First-run line (frame 63, `FirstRunPage`).** Frame 18 "Ready to read!" plus one non-tappable line above "Set up the stories": "Dino counts which books get read, with no names or ids. You can turn this off in Settings." (12.5px, `#5a5470`, centred, max 290px, pushed to the bottom by `margin-top:auto`).

**New strings (all Dino locales):** the switch title, its helper, the first-run line.

**Open point: First-run line wording.** The plan says "one line in the first-run screens" (plural). The mock puts it only on the final "ready" screen. **Default:** only the ready screen.


## Work

- [x] Smallest end to end: `book opened` and `book finished` from the reader -> batch -> Worker ->
      Analytics Engine, honouring the config switch and sampling; a dashboard/SQL query shows them.
- [x] Grown-up off switch and first-run line, strings in all Dino languages.
- [x] Worker validation: known event names and fields only, length caps, rate cap per request; Dino
      host only; tests.
- [x] Privacy texts: `site/dino/privacy.html`, `PRIVACY.md`, `STORE.md` (what is counted, no id, the
      off switch, Cloudflare as processor), iOS `PrivacyInfo.xcprivacy` (product interaction, not
      linked, not tracking).
- [ ] **Human:** Play Console Data safety (app activity, not linked to the user, not shared) and App
      Store privacy answers; Claude drives these in the browser where the user is signed in.

## Verification

- Core and Worker tests; emulator: read a book, events arrive in Analytics Engine (SQL API query);
  switch off in settings, nothing is sent; `telemetry.enabled=false` in R2, the Worker writes nothing.

*Changed while building:*

- The Analytics Engine `USAGE` binding is deliberately not in `wrangler.jsonc` / `wrangler.workers-dev.jsonc`: AE could not be confirmed enabled and a failed deploy would take the site down. Until switched on, telemetry is off and `POST /api/events` returns 204 and writes nothing.
- `UsageCountsCard` sits in `LanguagePage` above the Version card (outside Touches, approved); the first-run line is only on the ready screen.
- Each event carries `sampleRate` (0.001-1) and is written with `double1 = 1/rate`, so counts are `SUM(double1)` (query in `STORE.md`).

*Built:*

- Worker `POST /api/events` (Dino host plus `/dino` twin): checks the remote telemetry switch (60 s isolate cache; R2 error -> 503) and the `USAGE` binding before reading the body; byte-capped stream <= 32 KB, <= 50 events, closed-list validation.
- App `UsageCounter`: in-memory queue under lock, persisted; one sampling roll per launch; batches of 50 on window stop, on leaving the reader and on launch; permanent 4xx drops a batch, 408/429/5xx back off 5 min to 1 h; remote switch off discards the queue.
- Events `book_opened` (first `OnAppearing`) and `book_finished` (also on natural end; reset on swipe back). Source is `shelf` plus id only for starter/store catalogue books, else `own`.
- Privacy texts: `site/dino/privacy.html`, `STORE.md`, `PrivacyInfo.xcprivacy`. Strings `Usage_Title`, `Usage_Helper`, `FirstRun_Usage` in 14 resx (machine drafts).
- Tests: worker 70, Dino 1139.

#### For CLAUDE.md (lifted 2026-10-26)

- Worker `/api/events` stays inert until switched on: enable Analytics Engine in the dashboard, add the `USAGE` binding to both wrangler configs, deploy, then set `telemetry.enabled` true. Never add the binding before AE is enabled (a failed deploy takes the site down). (Slice 133: docs/plan/slices/133-dino-anonymous-usage-counts.md)
- Usage events carry only closed-list fields, never an id, typed text or own-book title; source is `shelf`+id only for starter/store books, else `own`. Count with `SUM(double1)`, not `COUNT(*)`, because of sampling. (Slice 133: docs/plan/slices/133-dino-anonymous-usage-counts.md)

#### Not yet verified

- Switching on (human: enable Analytics Engine), then emulator read: rows appear in `dino_usage`; settings off sends nothing.
- Play Data safety and App Store privacy answers once the sending release ships (human).
- No rate limit or origin check on `/api/events`: a hostile client can inflate counts (accepted for now).
- Native read of the new strings (machine drafts).
