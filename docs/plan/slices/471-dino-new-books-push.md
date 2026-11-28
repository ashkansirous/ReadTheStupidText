# Slice 471: Dino: a push notification when new books are out, with opt-out

**Product:** Dino

*Planned 2026-11-28 from the owner's RefineScope answers (this session). **Starts after:** Slice 470, which provides the banner the notification opens.*

**Touches:** `src/Dino/DinoReader.App/DinoReader.App.csproj`, `src/Dino/DinoReader.App/Platforms/Android/` (manifest, `MainActivity.cs`, `google-services.json` (new)), `src/Dino/DinoReader.App/Platforms/iOS/` (`Info.plist`, `Entitlements.plist`, `AppDelegate.cs`, `GoogleService-Info.plist` (new)), `src/Dino/DinoReader.App/NewBooks/` (the push parts), `src/Dino/DinoReader.App/StorePage.xaml.cs`, `src/Dino/DinoReader.App/SettingsPage.cs`, `src/Dino/DinoReader.App/SettingsRows.cs`, `src/Dino/DinoReader.App/Resources/`, `.github/workflows/dino-new-books-push.yml` (new), `tools/dino/new-books-push/` (new), `PRIVACY.md`, `site/dino/privacy.html`, `tests/Dino/DinoReader.Tests/`.

## Why

Families should hear about new books even when they don't open the app. The owner wants to send the notification and was worried about cost. Firebase Cloud Messaging is free with no message limit, and it also carries iPhone notifications through Apple's service.

## Decisions (owner, 2026-11-28, this session)

- **Firebase push** (owner: "firebase push"). This was chosen over a free daily check inside the app after being told that the privacy page and the store privacy forms would need updating.
- **Ask only inside the grown-up area** (owner: "agreed"). There is one gentle offer the first time a grown-up opens the Store, and a "Tell me about new books" switch in Settings that turns it off at any time. The phone's own permission pop-up appears only after the grown-up says yes, so a child never sees it.
- **Opening the notification always shows the banner** of the books it announced: from a cold start, from a warm start, and even if that banner was already seen (owner: "clicking on push notification should still open the banner even if visited once").
- Android and iOS. The Windows head gets the banner only (owner: agreed in RefineScope Q1).

Decided by this session (not the owner's words):

- Phones join a **topic**, so the server keeps no list of phone tokens and Decision 140's "no per-device profile on our side" holds. There is one topic per app language, `dino-new-books-<lang>`, and the app moves to the new topic when the app language changes. That way the notification is in the app's own language.
- The owner sends a notification by running a GitHub workflow by hand, with the release time of the batch as its input. The words come from a 15-language strings file in the repo.
- The notification carries that release time, and tapping it opens Slice 470's "banner for books since <time>".

## Work

- [ ] 1. **Policy check before code.** Run `web-research` on two questions: (a) Google Play Families policy for Firebase Cloud Messaging in a children's app (is FCM on the self-certified Families SDK list, and what does the Data safety form need), and (b) Apple's rules for push in an app for children. Write the answers here. If either one forbids it, stop and ask the owner one plain question.
- [ ] 2. **Firebase project.** Create the Firebase project and the Android and iOS apps by tool (`firebase` or `gcloud` CLI), and commit the two config files. Upload the existing Apple push key (the Lets-Call-Mom Apple setup, `reference-apple-and-asc`) to Firebase. Store the service-account key used for sending through `manage-secrets`, and add it as a GitHub secret. Any step no tool can do goes to the owner as one step with its deep link.
- [ ] 3. **App side, Android.** Add the Firebase Messaging binding for .NET 11 MAUI (check with context7 first). Subscribe to the topic only after a yes. Add the notification channel "New books". A tap opens `MainActivity` with the release time, cold or warm, and shows the banner.
- [ ] 4. **App side, iOS.** The same as Android: APNs entitlement, permission asked only after the yes, topic subscription, and the tap handled in `AppDelegate`, cold or warm, showing the banner.
- [ ] 5. **The ask and the switch.** On the first Store open, show a gentle one-time offer (words from the app, in the app language; "no" is final until the switch is used). The Settings switch "Tell me about new books" subscribes or unsubscribes and deletes the Firebase token when turned off. If the phone permission is denied, the switch explains how to turn notifications on in the phone settings.
- [ ] 6. **Sender.** `dino-new-books-push.yml` (`workflow_dispatch`, input: the batch release time, default the newest `addedAt` in the live `/api/stories`). It sends one message per language topic through the FCM HTTP v1 API, with a dry-run option. Add a `tools/dino/new-books-push/` script plus the strings file.
- [ ] 7. **Privacy.** Update `PRIVACY.md` and `site/dino/privacy.html`: what Firebase receives (a device token held by Google, the topic) and nothing else. Update the Play Data safety form and the App Store privacy answers by API where one exists, else name each console page with its deep link. Keep the AndroidManifest and Info.plist comments in step with this.
- [ ] 8. **Tests and flows.** Unit tests for the tap-to-banner hand-off and the switch state. Add a Debug hook that fakes a notification tap, and a scripted flow `new-books-push-tap` (cold and warm, the banner shows even after it was seen).

## Verification

- Unit tests, plus `new-books-push-tap` on the emulator and an iOS simulator.
- A real dry run, then a real send to a test-only topic, received on the S21 and an iPhone.
- Turning the switch off and sending again: no notification arrives.
