# Slice 137 — RTST phone: anonymous usage counts and timings

*Planned.*

**Batch:** 29 (RTST usage, waiting and early start, Decisions 143-148)
**Touches:** `worker/events.ts`, `worker/appconfig.ts`, `worker/index.ts`, `worker/downloads.ts`, `worker/*.test.mjs`, `wrangler.jsonc`, `wrangler.workers-dev.jsonc`, `tools/rtst-usage-report.ps1` (new), `src/ReadTheStupidText.Mobile.Shared/Usage/**` (new), `src/DinoReader.App/AppConfigClient.cs`, `src/DinoReader.App/UsageCounter.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/ReadTheStupidText.Application/Usage/**` (new), `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Mobile/VoicePickerPage.xaml.cs`, `src/ReadTheStupidText.Mobile/SettingsPage.xaml`, `src/ReadTheStupidText.Mobile/SettingsPage.xaml.cs`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml*`, `src/ReadTheStupidText.Mobile/Resources/Strings/*.resx`, `src/ReadTheStupidText.Mobile/Platforms/iOS/Resources/PrivacyInfo.xcprivacy`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/MlKitTranslator.cs`, `PRIVACY.md`, `site/privacy.html`, `STORE.md`, `tests/ReadTheStupidText.Tests/**`, `src/ReadTheStupidText.Mobile/CLAUDE.md`

## Why

The user wants to know which languages people use, whether they know they can translate or read
the original, whether they change the voice, and how long translation and the first sound take
(a user reported the app "hangs" on Translate; the ML Kit model downloads on first use and is the
likely cause). Today Read The Stupid Text sends nothing and `PRIVACY.md`, `site/privacy.html`,
`STORE.md` and the store forms all say so.

## Decisions (locked with the user, 2026-10-26)

- **Same pipe as Dino (Decision 144).** Reuse Dino's Batch 28 work (Slices 132-134): `POST /api/events`
  on the Worker, Workers Analytics Engine, a remote config with `telemetry.enabled` and `sampleRate`.
  RTST gets its own dataset `rtst_usage` (binding `RTST_USAGE`) and its own config object
  `readthestupidtext/config.json`, served on the `readthestupidtext.sirous.uk` host.
- **Anonymous counts, no id (Decision 143).** No install, device or advertising id; no read text, no
  file names; nothing finer than the hour. Durations go in buckets. On by default, an off switch in
  Settings, one line on the first-run language screen. No consent prompt (the user's call: "anything
  minimum that does not need their approval").
- **Nothing is sent until the store declarations match (Decision 145).** The app code and privacy
  texts ship in this slice, but `telemetry.enabled` in `readthestupidtext/config.json` stays `false`
  until every row of the *Store privacy declarations* checklist in `plan.md` is ticked.

## Work

- [ ] Shared client: move the queue/batch/flush/sample logic out of `DinoReader.App/UsageCounter.cs`
      and the fetch/cache logic out of `DinoReader.App/AppConfigClient.cs` into
      `Mobile.Shared/Usage/`, parameterised by host and preference keys. Dino keeps its own
      `Record*` methods on top; its behaviour and keys do not change (its tests stay green).
- [ ] Worker: serve `/api/app-config` and `/api/events` on the RTST host too (today Dino only),
      reading `readthestupidtext/config.json` and writing to `RTST_USAGE`; a closed RTST `EVENT_RULES`
      set. Like Slice 133 the binding is **not** added to the wrangler configs until Analytics Engine
      is confirmed enabled (a failed deploy takes the site down); until then the route returns 204.
- [ ] Events (closed set, `Application/Usage/`, pure and unit-tested):
  - `app_opened`: platform, OS major version, app version, UI language.
  - `read_started`: source language (detected), read language, mode (`original`/`translation`),
    voice engine (closed list), voice is default (bool), text length bucket.
  - `translate_offered` / `translate_chosen`: language pair (ReviewPage checkbox shown, ticked).
  - `voice_changed`: language, engine.
  - `timing`: stage (`model_download`, `translate`, `first_audio`), language pair, duration bucket
    (<0.5, 0.5-1, 1-2, 2-5, 5-10, 10-20, >20 s), outcome (`done`, `cancelled`, `failed`).
- [ ] Settings switch "Help improve the app" with helper "Sends counts like "a French text was read".
      No names, no ids, nothing you read."; one line on the first-run screen; strings in every RTST locale.
- [ ] Privacy texts: `PRIVACY.md`, `site/privacy.html`, `STORE.md` name every event and field, the off
      switch, Cloudflare as processor, that the IP is seen by the server but not stored with counts;
      iOS `PrivacyInfo.xcprivacy` (product interaction and performance data, not linked, no tracking).
- [ ] `tools/rtst-usage-report.ps1`: languages, translate offered vs chosen, original vs translation,
      voice changes, timing distributions per stage and platform; suppress groups under 10.
- [ ] Seed `readthestupidtext/config.json` with telemetry **off** after merge.

## Verification

- Worker and Application unit tests; Dino tests unchanged and green.
- Emulator (once Analytics Engine is on and the switch is flipped for a test): read, translate,
  change voice; rows appear in `rtst_usage`; Settings off sends nothing; config off, Worker writes nothing.
- Human gate: the *Store privacy declarations* checklist in `plan.md` before `telemetry.enabled` is set.
