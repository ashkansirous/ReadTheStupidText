# Slice 438 — Dino: every Sonos home links once through the Dino app and saves its story language
**Product:** Dino

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
file.

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

- [ ] B1. Research (`web-research`, docs.sonos.com): the AppLink flow (`getAppLink`, `getDeviceAuthToken`,
      `refreshAuthToken`), where the household id arrives (`credentials.loginToken.householdId` or other), token
      lifetime and refresh. Quote the lines here.
- [ ] B2. `worker/schema/sonos-homes.sql` (D1 `STATS`): one row per hashed household id: story language, link token hash,
      subscription proof key (Slice 439), created and last-seen times. Nothing about the child or listener.
- [ ] B3. `worker/sonos-link.ts`: `getAppLink` (returns the app link and the web fallback `site/dino/sonos-link.html`
      with the link code), confirm endpoint called by the Dino app (code + story language + subscription proof),
      `getDeviceAuthToken`, `refreshAuthToken`, unlink; a request without a valid token gets the Sonos not-linked fault.
      Sonos portal: authentication changes to AppLink (owner step with the integration's deep link, as in Slice 436 A4).
- [ ] B4. `site/dino/sonos-link.html`: tries the Dino app's link; without the app, sends the phone to its store page by
      system (Android Play, iOS App Store, Windows Microsoft Store, else the Dino home page); real store ids from the
      repo's store config.
- [ ] B5. Browse (`worker/sonos.ts`): "Story language" row, `setlang:<code>` saves and shows that root, shelves in the
      saved language, labels in the header language, subtitle title in the phone language when they differ.
- [ ] B6. Dino app, **Android and iOS** (same slice): `SonosLinkPage` opened by the app link with the code, language
      picker defaulting to the reading language, Connect, the strings in every app language (`AppResources*.resx`), the
      link handler in `AndroidManifest.xml` and `Info.plist`; pure link logic in `DinoReader.Core/SonosLink.cs` with
      tests.
- [ ] B7. Tests: link start/confirm/refresh/unlink, one row per home, a second home replaces the first, setlang saves,
      unlinked request faults, fallback page picks the store per user agent.
- [ ] B8. Wording: `PRIVACY.md`, `site/dino/privacy.html` (no Sonos text today) and Slice 283's promo and description say
      what a linked home stores (story language, a hashed home id, the subscription proof key) and how to unlink. Show
      the owner the new wording before shipping.

## Verification

- `npm ci && npm run typecheck`, `node --test worker/*.test.mjs`, `dotnet test tests/Dino/DinoReader.Tests/DinoReader.Tests.csproj`.
- Owner's sandbox: adding the service from the S21 Sonos app opens the Dino app's Link screen; Connect saves Farsi; root
  shows "Story language: فارسی" and Farsi shelves; on an emulator without the Dino app the link opens the Play page.
  iOS: the Link screen on the simulator through the app link.
