# App Store listing: Dino Book Reader (ASC app 6825053435, bundle uk.sirous.dinoreader)

**DRAFT, nothing pushed (2026-10-30).** State read through the ASC API the same day: version 1.81.1 `PREPARE_FOR_SUBMISSION`; en-US localization all null (description, keywords, supportUrl, marketingUrl, promotionalText); appInfo has name and privacyPolicyUrl (`https://dinoreader.sirous.uk/privacy.html`), subtitle and primary category null; copyright null; contentRightsDeclaration null; age rating all null; no review detail; no price; no screenshot sets; `isOrEverWasMadeForKids` false.
Source text: the live Microsoft Store English text (public page read 2026-10-30; ages 3 to 9, fifteen languages). It is newer than the live Play en-GB text, which still says "3 to 8" and "nine languages" (Play needs the same update; not done here). German: `de.md`.

## 1. appInfoLocalization (en-US)

Name (<= 30): Dino Book Reader (already set)
Subtitle (<= 30, 27 chars): A voice for every character

## 2. appStoreVersionLocalization (en-US)

Promotional text (<= 170, 143 chars):
Stories read aloud to children aged 3 to 9, with a different voice for every character. No ads, no accounts, no in-app purchases. 15 languages.

Keywords (<= 100 bytes, 95 bytes):
kids,bedtime,stories,picture books,children,fairy tales,voices,storybook,tales,toddler,narrator

Support URL: https://dinoreader.sirous.uk/
Marketing URL: https://dinoreader.sirous.uk/
What's new: none for the first version.

Description (<= 4,000):

Dino Book Reader reads stories aloud to children aged 3 to 9, with a different voice for every character. The narrator tells the story, and the wolf, the pigs and the gingerbread man each speak in their own voice.

BRING YOUR OWN BOOKS
A grown-up can add any story: scan the pages of a paper book, pick photos or a PDF, or type or paste the text. Dino Book Reader finds who is speaking and gives each character a voice. You can change any voice on the cast screen.

READ TOGETHER
- Each sentence lights up as it is read, so children can follow along.
- Picture pages stay as they are, with the words read over them.
- A night theme for bedtime stories.

STORIES INCLUDED
The app comes with illustrated starter stories, and grown-ups can get more free stories from the built-in story shelf.

FIFTEEN LANGUAGES
English, Arabic, Chinese, Danish, Dutch, Farsi, Finnish, French, German, Japanese, Norwegian, Portuguese, Russian, Spanish and Swedish. Stories can be translated into the grown-up's language right on the iPhone or iPad.

MADE FOR KIDS
- No ads and no in-app purchases.
- No accounts.
- Settings, adding and deleting stories sit behind a grown-ups gate.
- Voices are generated on the device. Once the voice for your language is downloaded, reading works offline.
- Your stories, photos and scans stay on your device.
- Optional anonymous usage counts with no names or identifiers, switched off in Settings (behind the grown-ups gate).

(Basis: the Microsoft Store text plus the Play text's scan and translation lines, as in `de.md`; the last bullet is from site/dino/privacy.html. "Made for kids" is a heading only, not the App Store Kids Category; reword if the user does not want it to imply that.)

## 3. Version and app fields

- copyright: `2026 Ashkan Sirous`.
- primaryCategory: BOOKS. secondaryCategory: EDUCATION. (GAMES_FAMILY exists but this is not a game.)
- Price: free. USA price point id `eyJzIjoiNjgyNTA1MzQzNSIsInQiOiJVU0EiLCJwIjoiMTAwMDAifQ` (customerPrice 0.0).

## 4. LEGAL DECLARATIONS IN THE USER'S NAME (proposed answers; the user decides)

contentRightsDeclaration: DOES_NOT_USE_THIRD_PARTY_CONTENT. Evidence: the 15 books in `content/dino/*/book.json` are credited as retold traditional tales, Aesop, Brothers Grimm, Hans Christian Andersen, Beatrix Potter (1902) and two by Ashkan Sirous: public-domain sources retold by the project, "free to share". Caveats: the pictures were drawn with Gemini; books grown-ups add are their own private content. If the user wants certainty on Peter Rabbit (1902) or the AI pictures, answer USES_THIRD_PARTY_CONTENT and confirm rights.

Age rating declaration (same attributes as RTST). Proposed: all content descriptors NONE; gambling, lootBox, healthOrWellnessTopics, messagingAndChat, socialMedia, userGeneratedContent, unrestrictedWebAccess, advertising, parentalControls, ageAssurance all false. Expected 4+. Evidence: privacy page says no advertising, no in-app purchases, "does not link children to other apps or websites"; no accounts or chat; no WebView or browser (grep: the only `Launcher` use opens this app's own App Store page, in `StoreUpdater.ios.cs`, after a parental gate per the comment in `UpdatePage.cs`; I did not run it). Judgment points:
- violenceCartoonOrFantasy NONE, because the retold tales are written so nobody is hurt (project rule for ages 3-9). If a wolf or giant counts as mild cartoon peril, use INFREQUENT_OR_MILD (still 4+).
- parentalControls false. The grown-ups gate (hold, then a sum: `GrownUpGate.cs`) hides settings and story management from the child; it does not let a parent restrict content or time. If Apple's wording counts a gate, answer true.
- userGeneratedContent false. Grown-ups add their own books and can record their voice, and can share a book file out through the system share sheet (`StoryActions.cs`), but nothing is published to other users.
- ageAssurance false: no age check.
- Enum spellings from memory, as in `appstore-rtst.md`.

Kids Category (NOT chosen, flagged): it fits the product (ages 3-9, no ads, no accounts, no purchases, a gate). What it would require, from my memory of Apple's rules (guidelines 1.3 and 5.1.4; Apple's page would not load today, so verify): set `isOrEverWasMadeForKids` true (I believe it cannot be undone) and pick one `kidsAgeBand` (5 and under, 6-8, 9-11), so 3-9 spans two bands and a narrower claim must be chosen; the privacy page says "3 to 8" while the stores say 3 to 9, fix before submitting; no third-party analytics or ads; any link out or purchase behind a parental gate; a privacy policy (exists). Check first: the anonymous usage counts go to our own Cloudflare Worker (first-party, but still analytics); translation models download from Google's servers (ML Kit); the update prompt opening the App Store page must sit behind the gate. Whether to go Kids is the user's call.

## 5. appStoreReviewDetail

Same contact rules as `appstore-rtst.md` section 5: name Ashkan Sirous; email elinsoftuk@gmail.com (Play developer contact) or ashkan.sirous@gmail.com, user to pick; phone NOT FILLED (none exists anywhere I looked); no demo account.
Notes: "A children's story reader with no sign-in. On first launch it asks for a language and downloads an on-device voice (about 145 MB) from this project's public GitHub Releases page; it needs internet once. Tap a starter book on the shelf to hear it read with a different voice per character. Settings and adding books are behind a grown-ups gate (hold a button, then answer a sum). The free story shelf (in the grown-up area) downloads stories from our own server. The camera is used only to scan a printed book, on the device. No ads, no purchases, no accounts. Privacy policy: https://dinoreader.sirous.uk/privacy.html"

## 6. Screenshots (not generated; applies to both apps)

Missing for both: one 6.9"/6.7"/6.5" iPhone class and a 13" iPad (UIDeviceFamily is 1+2 in both Info.plists, hence iPad is required). In the repo: no iOS screenshots, no fastlane or store-assets folder; only `site/dino/reading-screen.webp` (site image) and RTST's `site/img/control-panel.webp` (Windows). Play: RTST 4 phone screenshots, Dino 5, each with a feature graphic and icon, no tablet shots.
Capturing would take: a macOS CI job (both apps already build for `iossimulator-arm64` on macOS runners, `dinoreader-ios.yml`) that boots a 6.9" iPhone and a 13" iPad simulator, installs the Debug simulator build, drives it and runs `xcrun simctl io <device> screenshot`, then uploads through the ASC API (`appScreenshotSets` + `appScreenshots`). Not checked: whether Maestro (`.maestro/`, used for Android) or the Debug hooks work on iOS simulators, and the voice-pack download needs the network or a pre-seeded pack on the runner. Exact pixel sizes to be confirmed from Apple's docs.
