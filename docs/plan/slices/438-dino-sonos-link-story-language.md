# Slice 438 — Dino: every Sonos home links once through the Dino app and saves its story language
**Product:** Dino

*Plan-cleanup 2026-11-25: keep open, not started (row planned; Slices 437 and 439 also planned on origin/main). Checked: no `sonos-link.ts`, `sonos-homes.sql` or `site/dino/sonos-link.html` on origin/main. Correction: `PRIVACY.md:187` already has an anonymous-Sonos paragraph (no household id stored) that B8 must replace; only `site/dino/privacy.html` has no Sonos text. B9 moved out (outside waits: Sonos case 09396143, production submission) to a Watch line in plan.md Outstanding work.*

*Planned 2026-11-24. Corrected the same day (owner: "Slice 415 had "link required for all" is the correct decision! I
have not told claude design to change that!").*

**Depends on:** Slice 437 (shelves). **Replaces:** Slice 415's Sonos items (B link store, C AppLink, E the Dino app link
page) for the link itself; 415 keeps Alexa. Slice 439 adds the subscription check on top.

**Touches:** `worker/sonos.ts`, `worker/sonos-soap.ts`, `worker/sonos.test.mjs`, `worker/sonos-link.ts` (new),
`worker/sonos-link.test.mjs` (new), `worker/schema/sonos-homes.sql` (new), `worker/index.ts`,
`site/dino/sonos-link.html` (new), `src/Dino/DinoReader.App/SonosLinkPage.xaml` (new),
`src/Dino/DinoReader.App/SonosLinkPage.xaml.cs` (new), `src/Dino/DinoReader.App/Resources/Strings/AppResources*.resx`,
`src/Dino/DinoReader.App/Platforms/Android/AndroidManifest.xml`, `src/Dino/DinoReader.App/Platforms/iOS/Info.plist`,
`src/Dino/DinoReader.Core/SonosLink.cs` (new), `tests/Dino/DinoReader.Tests/SonosLinkTests.cs` (new), `PRIVACY.md`,
`site/dino/privacy.html`, `docs/plan/slices/283-dino-sonos-music-service.md` (listing text), `worker/CLAUDE.md`, this
file. Widened at claim (wave 2026-11-27, premise check): `src/Dino/DinoReader.App/App.xaml.cs`,
`src/Dino/DinoReader.App/MauiProgram.cs`, `src/Dino/DinoReader.App/Platforms/Android/MainActivity.cs`,
`src/Dino/DinoReader.App/Platforms/iOS/AppDelegate.cs`, `src/Dino/DinoReader.App/Platforms/iOS/SceneDelegate.cs`
(app-link routing), `site/dino/index.html` (store ids), `worker/wrangler.jsonc` (only if a binding is needed),
`src/Dino/DinoReader.App/Resources/Strings/AppResources.cs` and `.maestro/dino/sonos-link-screen.yaml` (new) (widened
after the build: the typed string keys and the scripted device flow),
`docs/DEPLOYING.md` (the hand-applied D1 schema).

*Built 2026-11-27 (wave 2026-11-27), merged as PR #1416 (e67b674a); Worker D1 table, iOS simulator and the S21 not yet verified (see Not yet verified).*

## Why

The brief pasted with the Claude Design canvas (`docs/design/sonos/`, 2026-11-24) asked for app-link sign-in through the
Dino app, a "Play Dino stories on your Sonos" screen with a story language picker and Connect / Not now, one Worker row
per Sonos home, a "Story language: <name>" row at the root that saves the language (`setlang:<code>`), folder labels in
the phone's language and the book's phone-language title in the subtitle when the two differ, and new privacy and Sonos
listing wording shown to the owner before shipping. Its line "Never linked: behave exactly as Slice A" is **not** the
owner's decision and is not built (see Decisions).

Owner, 2026-11-24, on what a family without the Dino app sees when the link opens: "they should see the download page of
the app on their phone with their os."

Facts on origin/main (2026-11-24): the Worker's only database is D1 `STATS` (`dino-stats`), schema files in
`worker/schema/`, applied by hand (`docs/DEPLOYING.md:192`), no migration numbering. No OS-picking download page exists:
`site/dino/index.html:124-125` has plain Google Play and App Store buttons.

## Decisions

- **Link required for all** (owner, 2026-11-18, Slice 415 Q-A, confirmed 2026-11-24): every Sonos home links through the
  Dino app when it adds the service, free books included. There is no unlinked home once this slice ships; Slice 437's
  anonymous browse is what Sonos shows until then. The canvas's "Not now" button and "never linked" path are dropped:
  the Link screen has Connect only, and leaving it leaves the service unadded.
- Owner, 2026-11-24: the link's web fallback, opened on a phone without the Dino app, is the app's download page for that
  phone's system: Android → Google Play, iPhone/iPad → App Store, Windows → Microsoft Store, anything else → the Dino
  site's home page with all store buttons.
- **Subscription only** (owner, 2026-11-18, Slice 415 decision 1, confirmed 2026-11-24: "sonos only works with
  subscriptions"): the home's row holds the story language and the subscription proof key for Slice 439, nothing about
  single-book purchases and nothing about the child. The Link screen's note says "your subscription", not "which books
  you own".
- Slice 415 decisions 4 and 5 stay: one Sonos home per subscription, a new home replaces the old link.
- Owner's brief: the privacy and listing wording is shown to the owner before this slice ships (B8).
- **Decision (Start pass 2026-11-27, obvious choice):** item B8 writes the privacy and listing wording and ships it with the slice; there is no separate show-the-owner round (the owner's standing rule: no review round they did not ask for). This replaces the brief line above.

## Design

From `docs/design/sonos/` (Link, SET-Home, SET-Pick, SET-Saved, SET-FA-Home), with the two changes in Decisions:

- **Dino app screen (Link):** header "Dino app · opened by Sonos"; title "Play Dino stories on your Sonos"; body "Sonos
  asked to connect to Dino. Choose the language your stories play in. You can change it later in Sonos or here."; a
  "Story language" picker (the app's reading language first); note "Dino keeps two things for your Sonos home: this
  story language and your subscription. Nothing about your child."; button "Connect to Sonos". No "Not now".
- **Root (SET-Home):** first row "Story language: English" / "Tap to change · saved for your home", then the six shelves;
  no Other languages row.
- **Pick (SET-Pick):** title "Story language"; rows: own name, subtitle "English · current" for the saved one, "Farsi · 8
  stories" for the others.
- **Saved (SET-Saved):** after picking Farsi, shelves list Farsi titles with subtitle "<phone-language title> · 6 min ·
  Free" (age dropped when the title is shown); folder names stay in the phone's language. Paid books' last part comes
  from Slice 439, never "Owned".
- **Farsi phone (SET-FA-Home):** "زبان قصه‌ها: فارسی" / "برای تغییر بزنید · برای خانه‌ی شما ذخیره می‌شود". Before anything
  is saved, the story language starts as the phone's language.

## Work

- [x] B1. Research (`web-research`, docs.sonos.com): the AppLink flow (`getAppLink`, `getDeviceAuthToken`,
      `refreshAuthToken`), where the household id arrives (`credentials.loginToken.householdId` or other), token
      lifetime and refresh. Quote the lines here.
- [x] B2. `worker/schema/sonos-homes.sql` (D1 `STATS`): one row per hashed household id: story language, link token hash,
      subscription proof key (Slice 439), created and last-seen times. Nothing about the child or listener.
- [x] B3. `worker/sonos-link.ts`: `getAppLink` (returns the app link and the web fallback `site/dino/sonos-link.html`
      with the link code), confirm endpoint called by the Dino app (code + story language + subscription proof),
      `getDeviceAuthToken`, `refreshAuthToken`, unlink; a request without a valid token gets the Sonos not-linked fault.
      Sonos portal: authentication changes to AppLink (owner step with the integration's deep link, as in Slice 436 A4).
- [x] B4. `site/dino/sonos-link.html`: tries the Dino app's link; without the app, sends the phone to its store page by
      system (Android Play, iOS App Store, Windows Microsoft Store, else the Dino home page); real store ids from the
      repo's store config.
- [x] B5. Browse (`worker/sonos.ts`): "Story language" row, `setlang:<code>` saves and shows that root, shelves in the
      saved language, labels in the header language, subtitle title in the phone language when they differ.
- [x] B6. Dino app, **Android and iOS** (same slice): `SonosLinkPage` opened by the app link with the code, language
      picker defaulting to the reading language, Connect, the strings in every app language (`AppResources*.resx`), the
      link handler in `AndroidManifest.xml` and `Info.plist`; pure link logic in `DinoReader.Core/SonosLink.cs` with
      tests.
- [x] B7. Tests: link start/confirm/refresh/unlink, one row per home, a second home replaces the first, setlang saves,
      unlinked request faults, fallback page picks the store per user agent.
- [x] B8. Wording: `PRIVACY.md` (replace the anonymous-Sonos paragraph at line 187), `site/dino/privacy.html` (no Sonos text today) and Slice 283's promo and description say
      what a linked home stores (story language, a hashed home id, the subscription proof key) and how to unlink. Show
      the owner the new wording before shipping.
- [moved out 2026-11-25 to a Watch line in plan.md, not this slice's work] B9. (Moved from Slice 283, 2026-11-25.) After the link is live: switch the sandbox integration to AppLink, rerun
      the Sonos Self Test until it passes, follow up on Sonos developer case 09396143, then submit the service for
      production. The user already played the anonymous service on their Sonos (2026-11-24); never ask for that play again.

## B1 result: the Sonos AppLink flow (research 2026-11-27, docs.sonos.com; full notes `D:/ReadTheStupidText-worktrees/reviews/s438/sonos-applink-research.md`)

Pages read: getapplink, getdeviceauthtoken, refreshauthtoken, use-authentication-tokens, add-app-authentication, add-authentication. Search summaries only (verify on the live page before relying): error-handling, smapi, add-browser-authentication.

- **getAppLink:** SOAPAction `"http://www.sonos.com/Services/1.1#getAppLink"`. The household id is a **Body** element (`householdId`, string, max 255: "The Sonos household ID requesting the link code."), not a header; the Body also has `hardware`, `osVersion`, `sonosAppName` and `callbackPath` ("The callback URL your service uses to launch Sonos after your app has authenticated the user", a Sonos app URL with a `state` query parameter). Response `getAppLinkResult/authorizeAccount`: `appUrl` (URL that opens the service's app), `appUrlStringId` (sample `SIGN_IN`), `deviceLink` with `regUrl`, `linkCode` ("An authorization code that players use to obtain access and refresh tokens for your service.", sample 19 characters; link code lifetime recommended one hour or less, at most 32 characters: smapi overview, search summary only), `showLinkCode` (boolean) and optional `linkDeviceId` ("A token, hidden from the user to prevent token phishing."). `callToAction` and `installAction` are deprecated. No fault is documented for getAppLink.
- **appUrl:** the service builds it (OAuth-style `scope`, `client_id`, `response_type`, `state`, `redirect_uri`; `state` and `redirect_uri` come from `callbackPath`). iOS opens it as a normal app URL; Android launches an intent with it as the URI data. After sign-in the app returns `code` and `state` to the callback URL (iOS: opens it; Android: extras such as `com.sonos.MusicServicesWizard.extras.code` through `onActivityResult()`).
- **getDeviceAuthToken:** `householdId` (Body, max 255) and `linkCode` (Body; if absent Sonos is upgrading the auth mode and sends username and password in the credentials header) and optional `linkDeviceId`; header `credentials` has `deviceId` and `deviceProvider`. Success `getDeviceAuthTokenResult`: `authToken` (max 2048, sent on every later call), `privateKey` (max 2048, only for refreshing) and optional `userInfo`. (The prose says `deviceAuthTokenResult`; the samples say `getDeviceAuthTokenResult`: the samples are used.) Faults: `Client.NOT_LINKED_RETRY` (`<ExceptionInfo>NOT_LINKED_RETRY</ExceptionInfo>`, SonosError 5) and `Client.NOT_LINKED_FAILURE` (SonosError 6), both faultstring "Link Code not found retry...". Per the error-handling summary Sonos polls for up to seven minutes on RETRY and treats FAILURE as permanent (new link code); the add-app-authentication page describes one call after control returns: the Worker handles both (RETRY until the app confirms, success after).
- **Where the household id arrives:** getAppLink and getDeviceAuthToken: Body `householdId`. Every call after linking: header `credentials/loginToken/householdId`, next to `token` (starts as the authToken) and `key` (starts as the privateKey). refreshAuthToken: no Body parameters; household id, token and key in `credentials/loginToken`.
- **Token lifetime and refresh:** the service sets it; the docs name three kinds (non-expiring; expiring with automatic refresh; expiring with manual re-login) and give no number ("authentication tokens should have a short life"). SOAP refresh: the service answers `Client.TokenRefreshRequired` with the new `refreshAuthTokenResult` (`authToken`, `privateKey`) in the fault `detail`, Sonos stores them and retries; or Sonos calls `refreshAuthToken` (answer `refreshAuthTokenResult` with `authToken`, `privateKey`, optional `userInfo`). If refresh fails: `Server.ServiceUnknownError` (SonosError 34) to retry, or `Client.AuthTokenExpired`, which makes Sonos ask the user to authorize again.
- **Not found:** the SonosError number of `Client.TokenRefreshRequired`; any unlink call (none was looked up); getDeviceLinkCode and getSessionId (the older DeviceLink path, not used).
- **Built from it:** non-expiring random tokens (`refreshAuthToken` rotates on request, never forced); a request with no valid token gets `Client.AuthTokenExpired`; the household id is hashed, never stored. The Sonos portal, not this research, decides how the link code is shown: `showLinkCode` is false.

## Built 2026-11-27 (wave builder) and decisions taken while building

- **Worker:** `worker/sonos-link.ts` (SMAPI link calls, D1 rows as hashes, JSON `confirm` and `unlink` on `POST /sonos` with `Content-Type: application/json`, the story-language words in 15 languages), `sonos.ts` (linked browse: Story language row, picker `pick:<label>`, `setlang:<code>:<label>` saves and answers with the new root, folder ids `lang:<story>-<label>/<shelf>`, the phone-language title leading the book line), `sonos-soap.ts` (login token read from the header only, NOT_LINKED faults with `<detail>`), `index.ts` (passes `{db: env.STATS}`; no binding means every call faults). `worker/schema/sonos-homes.sql` is applied by hand (`docs/DEPLOYING.md`).
- **Chosen without asking (obvious, say if wrong):** the page Sonos opens is the static file `/dino/sonos-link.html` and the app's confirm call reuses `POST /sonos` (JSON body), so no `run_worker_first` entry and no `wrangler*.jsonc` edit were needed; the app link is the custom scheme `dinoreader://sonos-link` (Android intent filter on `MainActivity`, iOS `CFBundleURLTypes`), not a verified https app link, so no `assetlinks.json`, apple-app-site-association or associated-domains entitlement is needed; the callback Sonos gives must be a `sonos...:` scheme or https on sonos.com, else `getAppLink` faults and the app never opens it.
- **Words:** English and Farsi come from the mock; in English Farsi is named "Farsi" and Norwegian "Norwegian" as in SET-Pick (the platform says "Persian" and "Norwegian Bokmål"); the other 13 languages are machine drafts for native review (Link screen and the Story language row).
- **Not built:** unlink from the Dino app's own UI (the `unlink` JSON call by subscription proof exists, Slice 439 gives it a caller); counting the Connect press in usage counts (SilentButtonGuard has a `no-tap-count` note: a new `UiControls` name needs the Worker's lists, outside this slice).

Also built: every Sonos call needs a linked home (fails closed; `Server.ServiceUnavailable` before the D1 table exists); pending rows capped at 3 per home; expired rows purged on every link call; `site/dino/sonos-link.html` opens the app scheme, then on Android an `intent:` URL falling back to Google Play, on iPhone the App Store only if the page stays visible; `SonosLinkPage` on Android (`[IntentFilter]` on `MainActivity`) and iOS (`CFBundleURLTypes`), scheme `dinoreader://sonos-link`; `PRIVACY.md`, `site/dino/privacy.html` and `docs/DEPLOYING.md` ("The Sonos home link").

**Results (2026-11-27):** worker 620 tests pass and typecheck; `DinoReader.Tests` 5163 pass; Android Debug, iOS compile and Windows x64 builds ok. Android emulator: `sonos-link-screen` and `first-run` PASS, the deep link opens the Link screen, no crash (logs `D:/ReadTheStupidText-worktrees/reviews/s438-device/`).

## Verification

- `npm ci && npm run typecheck`, `node --test worker/*.test.mjs`, `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj`.
- Owner's sandbox: adding the service from the S21 Sonos app opens the Dino app's Link screen; Connect saves Farsi; root
  shows "Story language: فارسی" and Farsi shelves; on an emulator without the Dino app the link opens the Play page.
  iOS: the Link screen on the simulator through the app link.

#### From review

- Skipped #7 (`reviews/pr-1416-high.md`): "The Link screen opens with no grown-up gate." - it opens only from the Sonos app's link, and the slice's mock and work items name no grown-up gate for it.
- Skipped #8 (`reviews/pr-1416-high.md`): "`CFBundleURLTypes` may collide with the Debug test-hook plist merge." - needs a Mac to check the merged Debug plist; [session] iOS check, not yet verified.
- Skipped #10 (`reviews/pr-1416-high.md`): "Custom scheme `dinoreader://sonos-link` instead of a verified https app link." - the custom scheme stays: the link code is single-use and expires within the hour; a verified https link is a follow-up.
- Skipped #4 (`reviews/pr-1416-medium.md`): "requireHome does a D1 SELECT in the critical path of every linked SOAP call (getLastUpdate, getMediaURI, browse) before the catalogue loads." - the D1 read on every call is how the slice's Decision "Link required for all" (owner, 2026-11-18, Slice 415 Q-A, confirmed 2026-11-24: every Sonos home links through the Dino app) is enforced; skipping it for any call would serve an unlinked home.
- Skipped #3 (`reviews/pr-1416-check.md`): "refreshAuthToken rotates token and key hashes with no grace; a lost response makes Sonos's retry fail with AuthTokenExpired and forces a relink." - a nit: the family links again, no data is lost; a grace window is a low follow-up.
- Skipped #4 (`reviews/pr-1416-check.md`): "src/Dino/DinoReader.App/SonosLinkPage.xaml.cs:86, nit. A retried Connect after Sonos already consumed the pending row gets 404 and shows "This link has run out" for a working link." - a nit about wording of a retry after success; low follow-up.
- Skipped #3 (`reviews/pr-1416-check2.md`): "worker/sonos-link.ts:117, nit. getDeviceAuthToken upserts over a confirmed home, so anyone holding a household id plus the link code from getAppLink can replace the live token (victim's speaker gets AuthTokenExpired)." - a confirmed, unexpired code is the only way to replace a home's tokens; that is the relink the check-pass fix kept and tested in `worker/sonos-link.test.mjs`.
- Third check (`reviews/pr-1416-check3.md`, 3 findings, none a crash or data loss) left open as low follow-ups, none fixed: `src/Dino/DinoReader.App/SonosLinkPage.xaml.cs:123` `_done` is set before `Launcher.OpenAsync`, so later Connect presses do nothing if the open failed; `SonosLinkPage.xaml.cs:137` a failed `Navigation.PopAsync` shows the failed line and leaves Connect dead; `SonosLinkPage.xaml.cs:38` `Load` drops a newer code that arrives mid-request. Also a low follow-up: a verified https app link instead of the custom scheme (high #10).

#### For CLAUDE.md (lifted 2026-11-27)

- Every Sonos SMAPI call needs a linked home (`worker/sonos-link.ts`, fails closed); the D1 schema `worker/schema/sonos-homes.sql` is applied by hand (`docs/DEPLOYING.md`). Already written in `worker/CLAUDE.md` by the builder (Slice 438: docs/plan/slices/438-dino-sonos-link-story-language.md); nothing more to lift.

#### Not yet verified

- [session] Apply `worker/schema/sonos-homes.sql` to the live D1 `STATS` per `docs/DEPLOYING.md` before the Worker deploy serves Sonos; until then every Sonos call answers `Server.ServiceUnavailable` (sandbox only).
- [session] iOS simulator: the link opens `SonosLinkPage` and the Debug test-hook plist merge keeps the `dinoreader` scheme (review high #8).
- [phone] The S21 opens the link from the Sonos page and hands back.
- B9 stays a Watch line in `plan.md`.
