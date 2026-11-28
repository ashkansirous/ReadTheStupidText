> Copied verbatim from the Scope Inbox phone doc (Google Drive, https://drive.google.com/file/d/1LXiomH_gcmWVb-r72XvyKnWkcAFY0PRB/view) on 2026-11-24, when it was planned. The slice files that build it win where they differ.

# Dino: new store screenshots and a short preview video on Google Play, the App Store and the Microsoft Store

- project: dino-reader
- status: ready
- goal: parents who see Dino in a store list tap on it — pictures and a short video that show what the app is in a few seconds
- context: read _context/CLAUDE.md (mirrored 2026-11-20), _context/_STATUS.md (2026-11-22), slices 404 and 417 from the plan mirror; plan.main.md, README.md and rules/ not re-read this time
- type: change
- created: 2026-11-23
- updated: 2026-11-23

## Idea
Ashkan: "I need a good video and better pictures." Dino's first week: few people tap on it in the store lists (App Store 66 impressions → 4 page views; Play 48 impressions), while competitors have pretty pictures and trailers. Follows up `_imported/2026-11-18-store-name-and-keywords.md` (the rename); the new name, pictures and video should go out in the same store update.

What went wrong before (Ashkan, 2026-11-23, with a screenshot of the live Play phone set): the captioned shots came out at a strange ratio, with a dark green background, the app screen shrunk inside it, and the phone's status bar (clock, icons) showing. That must not happen again.

## Why
The search-result tile (icon, name, first screenshots or the video) decides whether a parent taps. On the App Store, everyone who opened Dino's page downloaded it; the weak step is the tap from the list. Success: more page views per impression on all three stores, with pictures that are exactly the right size and look like the app.

## Constraints
- **Exact sizes per store — no other size, ratio, stretch, crop or letterbox is allowed.** Every file is checked against the table under "Sizes" before upload; a file that doesn't match exactly is not uploaded.
- Only real app screens and footage. Apple: previews must show only content within the app itself; no hands, fingers or people using a device.
- Status bar: none showing, or Android/iOS demo mode with a clean bar (no notifications, no shield icon). Prefer no status bar at all in the captioned shots.
- No background colour that isn't from the app's own palette (never the dark green again).
- Languages shot: English, Spanish and Japanese (Ashkan, 2026-11-23). **Never Farsi**, and not Chinese, in any shot on a non-local listing. This replaces the store-listing skill's "German or French for the languages shot" rule.
- Captions: no "Free", "#1", "Best", "Download now", no prices, no dates. Play: a caption takes at most 20% of the image. Microsoft Store: no marketing text inside the image (use the separate caption field instead).
- The video must say that some books need a subscription (Apple: disclose in the footage or on the end frame).
- Existing rules from Slice 404 still hold: 12-book full shelf in every shelf or book shot; never build a book package for screenshots; iOS shots from the macOS CI job; Windows shots from the Hyper-V VM.

## Milestones
1. **Phone screenshots, all three platforms' phone/iPhone sets** — six shots, the style below, exact sizes; acceptance: contact sheet looked at, sizes checked, pushed and read back on Play and the App Store.
2. **Tablet, iPad and iPhone Duo sets** — same six screens; Play tablet shots without captions (Play's large-screen guidance), iPad and Duo with captions; Duo after Slice 403; acceptance: sizes checked, pushed, read back.
2b. **Translated captions** — every listing language, after English is live; acceptance: each listing's set read back with its own-language captions.
3. **Microsoft Store desktop set** — landscape 1920 × 1080, no text in the image, captions in Partner Center's caption field; needs the VM store-shot check Slice 404 item 5 found missing; acceptance: pushed and read back.
4. **Preview video** — \~20 s, the timeline below; App Store iPhone and iPad previews, YouTube link for Play, 1920 × 1080 trailer for the Microsoft Store; acceptance: each file passes its store's spec, plays, and is live.
5. **Play feature graphic** 1024 × 500 — Dino plus one book illustration, name centred.

## The hard part
Making the captioned frame look like a designed product page instead of a screenshot pasted on a coloured box: the app screen must fill the frame edge to edge, the caption band must use the app's own colours and font, and every store's exact size must come out right without cropping the app's picture.

## Risks / kill criteria
- If a shot can't be made at the exact size without cropping important content, stop and report — never stretch or letterbox.
- App Store screenshots and previews can only change on an editable version (PREPARE_FOR_SUBMISSION); never cancel a review for it (Slice 404 rule).
- Windows: no Dino store-shot check exists on the VM yet; if it can't reach the 12-book shelf in a Release package, the Windows set waits.

## First step
Make the six English phone shots for Play at 1080 × 1920 in the new style and show the contact sheet in the slice before anything is pushed.

## Spin-offs
- Icon A/B test on Play's store listing experiments, once traffic is high enough for a result.

## Open questions
- [ ] iPhone Duo: Apple's `screenshotDisplayType` value for Duo (read from Apple's API reference or a live call, never guessed), and whether Slice 403 has shipped.
- [ ] Windows: which display scale and window size give a 1920 × 1080 capture where the picture and text fill the frame (try on the VM, check on a contact sheet).
- [ ] Code/repo: which tool made the green frame (`tools/emulator/store-captions.ps1` and `captions.json` per Slice 404) and where its colour, ratio and status bar come from.
- [ ] Code/repo: whether the app's Chinese is Simplified or Traditional (matters later for local listings).
- [ ] Apple's app preview size table was read partly; the 886 × 1920 iPhone and 1600 × 1200 iPad values must be re-checked on Apple's page before encoding.
- [ ] Ashkan to say whether Play's tablet set should also be landscape (Play allows 16:9 landscape, e.g. 2560 × 1440).

---

## Decisions (approved)
- **Language shot** → English, Spanish and Japanese pages, caption "Stories in 15 languages"; never Farsi or Chinese outside their own local listing. (Ashkan, 2026-11-23)
- **Video shots** → no shot longer than about 4 seconds; total about 20 s (Apple needs 15–30 s). (Ashkan, 2026-11-23)
- **Screenshot look** → the app screen fills the picture edge to edge; caption in a band at the top (Play: at most 20% of the image) in the app's own colours and font; no device frame, no status bar, no colour from outside the app's palette. (Ashkan, 2026-11-23)
- **Video orientation** → one storyboard: landscape 1920 × 1080 (YouTube for Play, and the Microsoft Store trailer), recorded from the app's landscape layout; landscape 1600 × 1200 for the iPad preview; portrait for the iPhone and iPhone Duo previews. (Ashkan, 2026-11-23)
- **iPad in landscape** → the iPad screenshots (2752 × 2064) and the iPad preview (1600 × 1200) are taken with the iPad in landscape, showing the app's landscape layout; the live portrait iPad set is replaced. (Ashkan, 2026-11-23: "it should be recorded on landscape mode, not portrait")
- **Caption languages** → every listing language gets translated captions, each in the parents' own search-friendly words, not word-for-word (all 15 app languages, where the store offers that listing; Apple has no Farsi listing). English first, the others in the same slice. Farsi captions only ever on the Farsi listing itself. (Ashkan, 2026-11-23: "all 5 languages should get translated caption", read as all 15)
- **Play tablet shots** → no captions; true 9:16, 1440 × 2560. (Ashkan, 2026-11-23)
- **iPad and iPhone Duo are in scope** → iPad 13" screenshots with captions and an iPad preview; iPhone Duo screenshot sets (outer 1398 × 2034, inner 2007 × 2853, portrait, from Slice 404 Decision 1) and a Duo preview (Apple: Duo uses the standard iPhone video size, 886 × 1920 portrait), taken on Apple's Duo simulator after Slice 403 ships; never faked, stretched or cropped from another size. (Ashkan, 2026-11-23)
- **Windows capture size** → never a full-screen capture of a high-resolution monitor. Size the app window (and the VM's display scale) so the captured window is exactly 1920 × 1080 and the book picture and its text fill it at a comfortable size, with little empty space; capture the app window only, not the desktop. The same window setup for the landscape video. Acceptance: contact sheet shows picture and text filling the frame, no large empty margins. (Ashkan, 2026-11-23)

## Research and options

### Sizes (checked 2026-11-23 on each store's own help pages)
| Store | Asset | Exact size to make | Rules |
|---|---|---|---|
| Google Play | Phone screenshots | **1080 × 1920** portrait (9:16) | 2–8; PNG/JPEG, no alpha; at least 4 at ≥1080 px in 9:16 to be eligible for promotion; caption ≤20% of the image |
| Google Play | 7" and 10" tablet screenshots | **1440 × 2560** portrait (9:16) | Large-screen guidance: 1,080–7,680 px, 16:9 or 9:16, at least 4; leave out text that isn't part of the app (may be cropped). The live tablet set is 1600 × 2560 (10:16), which doesn't match 9:16 |
| Google Play | Feature graphic | **1024 × 500** | JPEG or 24-bit PNG, no alpha; logo/name centred, out of the edges |
| Google Play | Preview video | YouTube link | Portrait or landscape both allowed; ads/monetisation off; public or unlisted; app shown within the first 10 s; only the first 30 s autoplay |
| App Store | iPhone 6.9" screenshots | **1320 × 2868** portrait | 1–10, no alpha; the 6.5" set (1242 × 2688) is needed only when there's no 6.9" set |
| App Store | iPad 13" screenshots | **2752 × 2064** landscape | Required because Dino runs on iPad; captioned; landscape (Ashkan, 2026-11-23); Apple also accepts 2732 × 2048 |
| App Store | iPhone Duo screenshots | outer **1398 × 2034**, inner **2007 × 2853**, portrait | From Slice 404 Decision 1 (Apple's screenshot specifications); after Slice 403 ships; Duo simulator only |
| App Store | iPhone Duo app preview | **886 × 1920** portrait | Apple: Duo uses the standard iPhone video dimensions |
| App Store | iPhone app preview | **886 × 1920** portrait (re-check) | 15–30 s, ≤30 fps, H.264 .mp4/.mov 10–12 Mbps, stereo AAC 256 kbps 44.1/48 kHz, ≤500 MB; autoplays muted; default poster frame at 5 s |
| App Store | iPad app preview | **1600 × 1200** landscape | Same as above; landscape like the iPad screenshots |
| Microsoft Store | Desktop screenshots (capture target) | **1920 × 1080** landscape, captured from a sized app window (never a full-screen grab of a high-res monitor) | PNG; at least 1366 × 768; up to 10, suggest 4+; key content in the top two-thirds; no extra logos or marketing messages in the image; caption field up to 200 characters |
| Microsoft Store | Trailer (capture target) | **1920 × 1080** MP4 (H.264, AAC 48 kHz) + **1920 × 1080** PNG thumbnail | ≤2 GB, 60 s or less recommended |
| Microsoft Store | Desktop screenshots, accepted by `store-size-check.ps1 -Target windows-screenshot` (Slice 445; capture at the target above, this row is only what the checker lets through) | a range, not an exact size: PNG, **1366 × 768 or larger, up to 3840 × 2160**, landscape or portrait (longer side 1366-3840, shorter side 768-2160) | at most 50 MB each; 10 files per listing language (the checker only warns when a folder holds more, it mixes languages); alpha is allowed (Microsoft does not forbid it; the eight RGBA 1920 × 1080 shots pass) |
| Microsoft Store | Trailer, accepted by `-Target windows-trailer` (Slice 445) | exactly **1920 × 1080** MP4 | H.264 High, AAC-LC 48 kHz, under 2 GB; over 60 s only prints a warning (Microsoft recommends 60 s or less); needs ffprobe |
| Microsoft Store | Trailer thumbnail, accepted by `-Target windows-thumbnail` (Slice 445) | exactly **1920 × 1080** PNG | alpha allowed |
| Microsoft Store | 16:9 super hero art (optional) | **1920 × 1080** PNG | No text or product title, no UI or device imagery |

Sources: https://support.google.com/googleplay/android-developer/answer/9866151 , https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications , https://developer.apple.com/help/app-store-connect/reference/app-information/app-preview-specifications , https://developer.apple.com/app-store/app-previews/ , https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images

### The six screenshots (same order on every store)
1. "Picture books, read aloud" — a full-page illustration (e.g. The Three Little Pigs).
2. "A different voice for every character" — reading screen with the highlighted line.
3. "Record your voice. Be the wolf." — the recording screen.
4. "Stories in 15 languages" — the same page in English, Spanish and Japanese.
5. "New books on the shelf" — the 12-book shelf.
6. "For ages 3 to 9. No ads."

### Video timeline (\~20 s, muted-first, captions on screen)
| Time | On screen | Caption |
|---|---|---|
| 0–3 s | A page turns, the wolf speaks | "Stories read aloud" |
| 3–6 s | Two characters in their own voices, words lighting up | "A voice for every character" |
| 6–10 s | The recording screen | "Record Grandma as the wolf" |
| 10–13 s | The same page in English, Spanish, Japanese (about 1 s each) | "Stories in 15 languages" |
| 13–16 s | Swiping along the shelf | "New books on the shelf" |
| 16–20 s | End frame: icon and name | "Some books need a subscription" |
The 5-second poster frame lands on the character-voices shot; it must look good as a still.

### Scope
- In: phone, Play tablet, iPad, iPhone Duo and desktop screenshot sets; the preview video on all three stores (iPhone, Duo and iPad previews on the App Store); Play feature graphic; captions in every listing language, English first.
- Out: icon redesign; Sonos and Alexa images.
- Later: icon test.

## Ashkan's notes
- 2026-11-23: "I need a good video and better pictures."
- 2026-11-23: Language shot: English, Spanish and Japanese. Never Farsi.
- 2026-11-23: Video shots no longer than 4–5 s; people get bored.
- 2026-11-23: The earlier captioned Play shots had a strange ratio and a green background; tell Claude Code the right screen size per platform so it doesn't go wild.
- 2026-11-23: Other stores' videos are landscape, one on YouTube.
- 2026-11-23: The iPad listing is bad: it should be landscape, not portrait.
- 2026-11-23: Approved 1, 2, 4; captions translated for all languages; don't forget iPad and iPhone Duo; on Windows don't record full screen on his high-res screen (lots of empty space) — make it smaller so a good-size picture shows with properly sized text beside it.

## Change log
- 2026-11-23: created (draft), before the RefineScope questions.
- 2026-11-23: RefineScope answers approved; iPad, iPhone Duo and Windows window size added; status ready.
- 2026-11-23: iPad screenshots and preview switched to landscape.
