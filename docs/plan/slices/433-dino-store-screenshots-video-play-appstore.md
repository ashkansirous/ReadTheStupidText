### Slice 433 — Dino: store screenshots in a new look, the App Store preview videos and the Play feature graphic (Google Play and App Store)
**Product:** Dino

*Planned 2026-11-24 from the phone doc [2026-11-23-dino-store-screenshots-and-video.md](../notes/2026-11-23-dino-store-screenshots-and-video.md) (Scope Inbox, status ready). Follow every line of that note's Constraints, Decisions (approved), Sizes, The six screenshots and Video timeline; this file wins where they differ.*

**Touches:** `tools/emulator/store-captions.ps1`, `docs/store-listings/screenshots/captions.json`, `tools/emulator/test-store-captions.ps1`, `tools/emulator/store-screenshots.ps1`, `tools/emulator/test-store-screenshots.ps1`, `tools/emulator/screenshot-frames.json`, `tools/emulator/store-size-check.ps1` (new), `.maestro/dino/needs-file/store-shot-phone.yaml`, `.maestro/dino/needs-file/store-shot-tablet.yaml`, `.maestro/dino/store-shot-ios-*.yaml`, `.maestro/dino/store-shot-ios-duo-*.yaml` (new), `.maestro/dino/store-video-*.yaml` (new), `.github/scripts/ios-sim-store-shots.sh`, `.github/workflows/store-screenshots.yml`, `tools/dino/promo/store/**` (new), `docs/store-listings/screenshots/dino/**`, `docs/store-listings/dino/**`, `.claude/skills/store-listing/SKILL.md`, `docs/plan/slices/433-dino-store-screenshots-video-play-appstore.md`. It records what went live in `docs/plan/slices/291-store-listings-and-screenshots.md` and ticks `docs/plan/slices/404-dino-store-pictures-foldables-and-retake.md` items 4, 6 and 7 (record lines only).

**Replaces:** Slice 404 items 4 (the App Store retake, taken 2026-11-19 in the old green-frame look and never pushed) and 6 (the iPhone Duo set); 404's Decisions 1, 3, 5 (Duo sizes and simulator only, the 12-book `full-shelf` hook, never build a book package for screenshots) still hold here. The old caption tool's behaviour that stays: captions read from `captions.json`, the 45-character limit and its test, the `-Form tablet` switch; what goes on purpose: the `#2F5D50` green fill (`store-captions.ps1:28`, `:97`), the 15% band (`:26`) and the shrink-to-fit of the app screen (`Get-ShotRect`, `:51-56`). Windows (404 item 5) moves to Slice 434.

## Why

Dino's first week: few people tap Dino in the store lists (App Store 66 impressions to 4 page views; Play 48 impressions). The live Play shots came out at a strange ratio, on a dark green box, with the app screen shrunk inside it and the phone's status bar showing (user, 2026-11-23). The user asked for "a good video and better pictures".

## Decisions (settled, never re-ask)

- Everything in the note's *Decisions (approved)* (user, 2026-11-23): the language shot in English, Spanish and Japanese (never Farsi or Chinese outside their own local listing); the screen fills the picture edge to edge with a caption band at the top in the app's own colours and font (Play: at most 20% of the image), no device frame, no status bar; iPad in landscape (2752 x 2064 shots, 1600 x 1200 preview), replacing the live portrait iPad set; Play tablet shots without captions at 1440 x 2560; captions translated into every listing language, English first; video shots at most about 4 s, about 20 s in all; iPhone Duo sets after Slice 403.
- **This plan replaces the unfinished screenshot work of Slice 404** (user, 2026-11-24, approving the suggested answer): its retaken App Store sets are not pushed; the new look goes to every store.
- **Windows desktop shots and the 1920 x 1080 landscape video are Slice 434** (user, 2026-11-24), so phone, tablet and iPad do not wait for the VM work.
- The note's open question "Play tablet landscape" is closed by its own decision (portrait 1440 x 2560, decided by this planning session from the note).
- The green frame came from `tools/emulator/store-captions.ps1` (answered from the code, 2026-11-24).

## Work

- [ ] 1. **New caption frame.** Rewrite `store-captions.ps1` so the app screen fills the output edge to edge at the exact store size with a top caption band in the app's palette and font (colours and font read from the Dino app's resources, named in the slice when built), no other colour, no letterbox, no stretch; crop the status bar off the raw shot (or demo mode, the note's Constraints). New `store-size-check.ps1`: fails any file whose pixel size, ratio or alpha does not match the note's *Sizes* row; every upload step runs it first. `test-store-captions.ps1` asserts the exact output sizes and that no pixel outside the band and the screen comes from outside the palette.
- [ ] 2. **First step: six English Play phone shots at 1080 x 1920** (the note's six screens and captions, in order), contact sheet written into this slice, then pushed through the `store-listing` agent and read back.
- [ ] 3. Play tablet set: the same six screens at 1440 x 2560, no captions; pushed and read back.
- [ ] 4. App Store sets from `store-screenshots.yml`: iPhone 6.9" 1320 x 2868 and iPad 13" landscape 2752 x 2064 (new landscape iPad flows), captioned; pushed when a Dino version is editable (PREPARE_FOR_SUBMISSION; never cancel a review, 404 rule), read back.
- [ ] 5. Translated captions for every listing language (parents' own search words, not word for word; Farsi only on the Farsi listing; Apple has no Farsi listing), each set pushed and read back. Update `.claude/skills/store-listing/SKILL.md`'s screen-language rule to English, Spanish and Japanese for the languages shot (the note replaces "German or French").
- [ ] 6. **App Store preview videos** with the note's video timeline: real app footage only (simulator screen recordings from the macOS job, no hands or devices), composed and captioned with HyperFrames in `tools/dino/promo/store/` (reuse `tools/dino/promo/linkedin/`'s setup), the end frame saying some books need a subscription, the 5 s poster frame on the character-voices shot. iPhone 886 x 1920 portrait and iPad 1600 x 1200 landscape; re-read Apple's app preview specifications page before encoding (the note read them partly). Encoded to Apple's spec (15-30 s, at most 30 fps, H.264 10-12 Mbps, AAC 256 kbps stereo), pushed, read back.
- [ ] 7. Play feature graphic 1024 x 500 (Dino plus one book picture, name centred, no alpha); the user asked for a new one, which lifts the skill's "stays unless the user asks" line. Pushed and read back.
- [ ] 8. iPhone Duo screenshot sets (outer 1398 x 2034, inner 2007 x 2853) and a Duo preview (886 x 1920) on Apple's Duo simulator after Slice 403 ships; read the Duo `screenshotDisplayType` from Apple's API reference or a live call, never guessed. Until 403 ships this item alone sits in *Not yet verified*.
- [ ] 9. Record what went live per store in Slice 291's file and tick Slice 404 items 4, 6 and 7 as replaced by this slice.

Rules on the way: `.github/CLAUDE.md` (no Actions artifacts: shots and videos go to the `store-shots-*` draft release; iOS shots only from `store-screenshots.yml`), `tools/CLAUDE.md` (status-bar demo mode cannot be read back: the proof is the shot), the 12-book `full-shelf` hook and never building a book package (404 Decisions 3 and 5). Android and iOS both get the new look in this slice.

## Verification

- `tools/emulator/test-store-captions.ps1` and `store-size-check.ps1` pass on every file before upload; `tools-selftests.yml` green.
- Each store's set and video read back through its API (Play edits, App Store Connect) with the expected sizes and order; contact sheets in this file.
- The Play page and App Store page show the new set (checked in Chrome on the live pages after release, by the run that pushed them).
