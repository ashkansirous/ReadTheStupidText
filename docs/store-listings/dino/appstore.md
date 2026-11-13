# App Store listing: Dino Book Reader (ASC app 6825053435, bundle uk.sirous.dinoreader)

**Rewritten 2026-11-13 (Slice 291 work item 4) from the current app, and pushed the same day through the App Store Connect API (en-US and de-DE read back equal; the version is NOT submitted). Version 1.81.1 is `PREPARE_FOR_SUBMISSION`, not submitted.** The text pushed earlier the same day (read back from the ASC API 2026-11-13) was stale: it still sold "bring your own books" (scan, photos, PDF, typing), a cast screen, on-device voices with a voice download, on-device translation and "scans stay on your device". Slice 278 (store books only, pre-made clips, no scanning or translation) and Slice 305 (no ML Kit, no sherpa-onnx in the package) removed all of that. What the app does now: a fresh install picks a language, then at least two books from the story store; every character has a pre-made voice (downloaded audio, one pack per language); a family can record its own voice for any character (microphone only on the Record screen); three reading modes; reading speed 0.8x to 1.2x; animal sounds; backup and restore; four books free, the rest $0.99 behind the grown-ups gate; 15 books, 15 languages. German: `de.md`.

## 1. appInfoLocalization (en-US)

Name (<= 30): Dino Book Reader (already set)
Subtitle (<= 30, 27 chars): A voice for every character (already set, unchanged)

## 2. appStoreVersionLocalization (en-US)

Promotional text (<= 170, 161 chars; editable any time):
Stories read aloud to children aged 3 to 9, with a voice for every character and room for your own. No ads. Four stories free, the rest $0.99 each. 15 languages.

Keywords (<= 100 bytes, 97 bytes, no spaces after commas):
kids,bedtime,stories,picture books,children,fairy tales,storybook,tales,toddler,read aloud,record

Support URL: https://dinoreader.sirous.uk/
Marketing URL: https://dinoreader.sirous.uk/
What's new: none for the first version.

Description (<= 4,000):

Dino Book Reader reads picture-book stories aloud to children aged 3 to 9, with a different voice for every character. The narrator tells the story, and the wolf, the pigs and the gingerbread man each speak in their own voice.

PICK YOUR STORIES
A grown-up chooses books from the story shelf: classic tales retold gently, with a picture on every page. Four stories are free. Every book comes with its own pre-made voices, in the language you pick.

HEAR FAMILY IN THE STORY
A parent or grandparent can record their own voice for any character, on any page, in any story language. Your child hears you read, and the recording stays on your device.

READ TOGETHER
- Each sentence lights up as it is read, so children can follow along.
- Choose picture only, text only, or picture and text.
- Reading speed from 0.8x to 1.2x, and animal sounds you can switch on or off.
- A night theme for bedtime stories.

STORIES AND PRICES
Four illustrated stories are free: The Lion and the Mouse, The Gingerbread Man, The Three Little Pigs and X Marks the Treasure. Every other story on the story shelf is $0.99 each (the App Store shows the price in your own currency). A grown-up buys a story once, behind the grown-ups gate, with in-app purchase, and can restore past purchases at any time. Stories you already have stay on your device.

FIFTEEN LANGUAGES
The app, the stories and the voices come in English, Arabic, Chinese, Danish, Dutch, Farsi, Finnish, French, German, Japanese, Norwegian, Portuguese, Russian, Spanish and Swedish.

MADE FOR KIDS
- No ads.
- Stories are bought through the App Store, by a grown-up behind the grown-ups gate. There is no account to create, and we store nothing about you or your purchase; the store's proof is checked and forgotten.
- Settings, buying and deleting stories, recording and backups sit behind a grown-ups gate.
- Once a story and its language are downloaded, it reads offline.
- Your recordings stay on your device. Back them up and restore them from the grown-ups area.
- Optional anonymous usage counts with no names or identifiers, switched off in Settings (behind the grown-ups gate).

(Basis: `site/dino/index.html` and `site/dino/privacy.html` as of 2026-11-13, and the app rules in `src/DinoReader.App/CLAUDE.md`. "Made for kids" is a heading only, not the App Store Kids Category.)

## 3. Version and app fields

- copyright: `2026 Ashkan Sirous`.
- primaryCategory: BOOKS. secondaryCategory: EDUCATION. (GAMES_FAMILY exists but this is not a game.)
- Price: free to download, with in-app purchases (one non-consumable product per paid story, $0.99 price point; made by `store-products.mjs`, not by this file; all 11 read READY_TO_SUBMIT on 2026-11-13).
- Price (app): free. USA price point id `eyJzIjoiNjgyNTA1MzQzNSIsInQiOiJVU0EiLCJwIjoiMTAwMDAifQ` (customerPrice 0.0).

## 4. Legal declarations and age rating (the user's answers of 2026-11-09, Slice 291)

- contentRightsDeclaration: DOES_NOT_USE_THIRD_PARTY_CONTENT (user: "sure").
- Age rating, the attributes `appInfos/{id}/ageRatingDeclaration` takes: all content descriptors NONE; `violenceCartoonOrFantasy` NONE (user, 2026-11-13: INFREQUENT_OR_MILD made Apple rate it 9+; the books are rewritten so nobody is hurt); `parentalControls` true (the grown-ups gate counts, user); gambling, lootBox, healthOrWellnessTopics, messagingAndChat, socialMedia, userGeneratedContent, unrestrictedWebAccess, advertising, ageAssurance all false. Expected 4+. Evidence: no advertising; no accounts or chat; no WebView or browser; purchases only behind the grown-ups gate; the one link out (the update page on the App Store) sits behind the gate. userGeneratedContent stays false: a recording is the family's own, kept on the device and in their own backup, never published to other users. Enum spellings are checked against Apple's docs on the first API write (work item 4).
- Kids Category: OFF for now (user, 2026-11-09). `isOrEverWasMadeForKids` stays false. Apple's age bands (5 and under, 6-8, 9-11) do not match "3 to 9"; turning it on later is the user's call.

## 5. appStoreReviewDetail

Name Ashkan Sirous; email elinsoftuk@gmail.com; phone: from the private secrets store (`D:\secrets\dinoreader\secrets.env`, `APP_REVIEW_CONTACT_PHONE`), filled at apply time, never written in this repo; no demo account.
Notes: "A children's picture-book reader with no sign-in and no ads. First launch: pick a language, then pick at least two books from the story store (internet needed; the books and their pre-made voices download from this project's own server, dinoreader.sirous.uk). Tap a book on the shelf to hear it read, with a different voice for every character. Settings, the story store, recording and backups are behind a grown-ups gate (hold a button, then answer a sum). A grown-up can buy extra stories at $0.99 each with in-app purchase (four stories are free); Restore Purchases is in the story store. A paid story is handed out only after our server has asked Apple that it was bought. The in-app purchase products are submitted for review with this version; use a sandbox Apple ID to try a purchase. The microphone is used only on the Record screen, when a grown-up records their own voice for a character; the recording stays on the device. The app does not use the camera or the photo library. Privacy policy: https://dinoreader.sirous.uk/privacy.html"

## 6. Screenshots (not generated; applies to both apps)

Missing for both: one 6.9"/6.7"/6.5" iPhone class and a 13" iPad (UIDeviceFamily is 1+2 in both Info.plists, hence iPad is required). Slice 291 work item 5 owns them (macOS CI simulator job `store-screenshots.yml`); pixel sizes to be confirmed from Apple's docs.
