# App Store listing: Read The Stupid Text (ASC app 6825052929, bundle uk.sirous.readthestupidtext)

**Rewritten What / Why / Who / Features 2026-11-15 (Slice 291) from the current app (15 voice languages: supertonic-3 en fr es pt ru ja ar da fi sv nl de, plus piper-fa, piper-nb and melo-zh-en; on-device translation; voice and speed choice; .txt/.pdf/.docx; camera scan) and pushed to the version in `PREPARE_FOR_SUBMISSION`; what went live is in the Slice 291 file.** Original draft written 2026-10-30 for the failed submit in Actions run 41078343356; state read then through the ASC API: version 1.76.1 `PREPARE_FOR_SUBMISSION` (editable); every field below null; no review detail, price schedule or screenshot sets.
Source text: Play en-US (live, read through the Play API) and `en.md`. Not reused as is: Play still says "No account and no analytics" (stale since the usage counts, PRIVACY.md 26 Oct 2026), and the Microsoft Store text says the voice ships inside the app (a downloaded pack since Slice 56).

## 1. appInfoLocalization (en-US)

Name (<= 30): Read The Stupid Text (already set)

Subtitle (<= 30, 19 chars): Read any text aloud

## 2. appStoreVersionLocalization (en-US)

Promotional text (<= 170, 152 chars; editable any time):
Listen instead of staring at a screen. Natural voices in 15 languages read pasted text, PDF and Word files or a scanned page aloud, offline.

Keywords (<= 100 bytes, 87 bytes; words of the name and subtitle left out on purpose):
tts,speech,voice,pdf,docx,scan,ocr,listen,speak,offline,narrator,translate,audio,reader

Support URL: https://readthestupidtext.sirous.uk/
Marketing URL: https://readthestupidtext.sirous.uk/
What's new: none for the first version.

Description (<= 4,000):

Read The Stupid Text reads any text out loud, so you can listen instead of staring at a screen.

The voices sound natural and run on your device, so your text never leaves it.

It is for anyone who would rather listen than read: a long article, a PDF, study notes, a page of a book, or a text in a language you read slowly.

Paste or type text, open a .txt, .pdf or .docx file, or scan a page with the camera, then tap play. Each sentence is highlighted as it is read.

- Natural-sounding voices in 15 languages (English, Persian, French, Spanish, Portuguese, Chinese, Arabic, Russian, Japanese, Danish, Finnish, Swedish, Norwegian, Dutch and German) that run on your device
- Choose a voice and a reading speed from 0.5x to 2x
- Works offline once your voice is downloaded; your text, files and scans never leave your device
- Optional translation on the device, when a text is in another language (not available for every language)
- No account and no ads
- Optional anonymous usage counts, with no text, names or identifiers; switch them off any time in Settings

The first time you open the app it downloads a voice for your language, about 145 MB, so it needs an internet connection once (Wi-Fi recommended). Until the download finishes, the app reads with your device's built-in voice. Scanning also downloads a small text-recognition pack for your language. The first time you translate to or from a language, a translation model of about 30 MB is also downloaded once from Google's servers; none of your text is sent. Only these packs are downloaded: your text, files and scans stay on your device, and once the voice is installed, reading does not use the network. Voices read in the language you choose in Settings; a text in another language is read with your iPhone's own voice.

Read The Stupid Text also exists for Windows (a tray utility that reads whatever you select or copy) and Android.

(Claims come from PRIVACY.md and `en.md`; "each sentence is highlighted" from `.maestro/rtst/sentence-highlight.yaml`; the iPad wording from UIDeviceFamily 1+2 in Info.plist. Not re-run on a device today.)

## 3. Version and app fields

- copyright (version): `2026 Ashkan Sirous` (LICENSE: "Copyright (c) 2026 Ashkan Sirous").
- primaryCategory: UTILITIES. secondaryCategory: PRODUCTIVITY. (Judgment call; BOOKS and REFERENCE are valid ids too.)
- Price: free. USA price point id `eyJzIjoiNjgyNTA1MjkyOSIsInQiOiJVU0EiLCJwIjoiMTAwMDAifQ` (customerPrice 0.0, from `/v1/apps/6825052929/appPricePoints?filter[territory]=USA`); set it as the base-territory manual price in `POST /v1/appPriceSchedules`.
- Export compliance: Info.plist already has `ITSAppUsesNonExemptEncryption` false (untouched).

## 4. LEGAL DECLARATIONS (the answers below were accepted by the user on 2026-11-15, "rtst should be updated", same as Dino's, and applied through the API; enum spellings confirmed by Apple's 200 responses)

contentRightsDeclaration: DOES_NOT_USE_THIRD_PARTY_CONTENT. Evidence: the app shows and reads only text the user supplies; it bundles no articles, books, images or media (voice models are downloaded engine data under their own licences, see the STORE.md third-party table). If the user counts the OpenRAIL-M voice model as content, answer USES and confirm rights; I judge it software, not content.

Age rating declaration (every attribute Apple listed in the failed run). Values that went live 2026-11-15 (Apple computes FOUR_PLUS):
- Content descriptors, all NONE: alcoholTobaccoOrDrugUseOrReferences, contests, gamblingSimulated, gunsOrOtherWeapons, horrorOrFearThemes, matureOrSuggestiveThemes, medicalOrTreatmentInformation, profanityOrCrudeHumor, sexualContentGraphicAndNudity, sexualContentOrNudity, violenceCartoonOrFantasy, violenceRealistic, violenceRealisticProlongedGraphicOrSadistic.
- Yes/no, all false: gambling, lootBox, healthOrWellnessTopics, messagingAndChat, socialMedia, userGeneratedContent, unrestrictedWebAccess, advertising, parentalControls, ageAssurance.
- Evidence: no ad SDK (grep of `src` for ad libraries found none; PRIVACY.md "no advertising"); no WebView or in-app browser (grep for WebView/Browser/Launcher found none in RTST code); no accounts, chat or sharing between users; no purchases. Caveat: the app reads whatever text the user pastes or opens, which could be anything, but it is the user's own and is not shared; I answered NONE on that basis (Apple's user-generated-content question is about content shared between users).
- Enum spellings (NONE / INFREQUENT_OR_MILD / FREQUENT_OR_INTENSE, booleans) and field names were accepted by Apple's 200 responses on 2026-11-15; these values are live.

## 5. appStoreReviewDetail

- Contact name: Ashkan Sirous. Contact email: elinsoftuk@gmail.com is the developer contact already public on both Play listings (Play API `details`); applied 2026-11-15.
- Contact phone: set 2026-11-15 from `D:\secrets\dinoreader\secrets.env` (`APP_REVIEW_CONTACT_PHONE`); never written in this repo. Contact email elinsoftuk@gmail.com, name Ashkan Sirous (user, 2026-11-15: same as Dino).
- Demo account: not needed (`demoAccountRequired` false).
- Notes: "No sign-in. On first launch the app asks for a language and downloads an on-device voice pack (about 145 MB) from this project's public GitHub Releases page; it needs internet once, and until it finishes the iPhone's built-in voice reads. To test: type or paste any text on the main screen and tap play; speed and voice are in Settings. The Scan button uses the camera and an on-device recognizer; nothing is uploaded. Optional anonymous usage counts can be switched off in Settings > Help improve the app. Privacy policy: https://readthestupidtext.sirous.uk/privacy.html"

## 6. Screenshots (not generated)

Apple's error needs one of IPHONE_DYNAMIC_ISLAND_LARGE / FACE_ID_LARGE / DYNAMIC_ISLAND_MEDIUM (6.9" / 6.7" / 6.5" class) and IPAD_13_PROFILE. The repo has no iOS screenshots, no fastlane or store-assets folder: only `site/img/control-panel.webp` (Windows panel) and `tools/desktop-vm/checks/rtst-panel-screenshot.ps1` (Windows VM). Play has 4 phone screenshots, a feature graphic and an icon for this app (counted through the Play API), 0 seven- or ten-inch. They are the wrong size and show Android chrome. Capture plan: `dino/appstore.md` section 6.
