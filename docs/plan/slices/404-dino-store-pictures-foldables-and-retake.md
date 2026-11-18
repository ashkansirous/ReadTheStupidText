# Slice 404 — Dino: a full-shelf test hook, then every live store picture retaken (Google Play, App Store, Microsoft Store) and the App Store iPhone Duo set
**Product:** Dino

*In progress (wave 2026-11-18), draft PR: items 1-2 only (night 2026-11-17-2105 wave 4).* *Planned 2026-11-17; the four decisions below were agreed with the user the same day.*

**Touches:** `src/Dino/DinoReader.App/Testing/**` (a new Debug hook), `src/Dino/DinoReader.App/Testing/CLAUDE.md`, `.maestro/dino/needs-file/store-shot-phone.yaml`, `.maestro/dino/needs-file/store-shot-tablet.yaml`, `.maestro/dino/store-shot-ios-1-reading.yaml`, `.maestro/dino/store-shot-ios-2-record.yaml`, `.maestro/dino/store-shot-ios-3-language.yaml`, `.maestro/dino/store-shot-ios-4-grownups.yaml`, `.maestro/dino/store-shot-ios-5-shelf.yaml`, `.maestro/dino/store-shot-ios-duo-*.yaml` (new), `.github/workflows/store-screenshots.yml`, `tools/emulator/store-screenshots.ps1`, `tools/emulator/test-store-screenshots.ps1`, `tools/emulator/screenshot-frames.json`, `docs/store-listings/screenshots/dino/**`, `docs/store-listings/dino/**`, `tests/Dino/DinoReader.Tests/**`, `docs/plan/slices/404-dino-store-pictures-foldables-and-retake.md`. It records what went live in Slice 291's file, `docs/plan/slices/291-store-listings-and-screenshots.md` (read, one record block added when a store goes live; the slice's own work stays 291's).

## Why

Three things are wrong with the live pictures, and one is new:

- Every shelf or book shot shows four lonely covers, because `skip-first-run` puts only the four test books on the shelf (`src/Dino/DinoReader.App/Testing/CLAUDE.md`). The store-listing skill (`.claude/skills/store-listing/SKILL.md`, section 2, *A full shelf in every shot*, user 2026-11-16) says no shelf or book shot is taken until a Debug hook puts the whole live catalogue on the shelf.
- The live "other languages" shot (shot 3) shows Farsi. The skill's *Screen language* rule (user 2026-11-16) says English everywhere, German or French for the languages shot, and Farsi, Arabic or Chinese only in their own local listing.
- Slice 393 item 14 already asks for a retake of the Android shots (status-bar demo mode; a download notification and a shield icon showed); it is folded into this retake.
- The iPhone Duo (Slice 403) is a new App Store display size.

## Decisions (user, agreed 2026-11-17; settled, never re-ask)

1. **App Store iPhone Duo set** (optional until April 2027; sizes from Apple's screenshot specifications, <https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/>: outer 1398 x 2034, inner 2007 x 2853, portrait). Taken **after Slice 403 ships**, from the app on Apple's iPhone Duo simulator. If Xcode has no Duo simulator yet, the item waits and goes to *Not yet verified*: never faked, stretched or cropped from another size. Before the upload, check the App Store Connect API's `screenshotDisplayType` list for a Duo value (read it from Apple's API reference or a live call, never guess the name).
2. **Retake every live set** on Google Play, the App Store and the Microsoft Store under `.claude/skills/store-listing/SKILL.md` section 2: English screens on the global and European listings; German or French for the languages shot (the live shot 3 shows Farsi and is replaced); Farsi, Arabic and Chinese screens only in their own local listing; a full shelf in every shelf or book shot.
3. **A new Debug test hook** puts the whole live store catalogue on the shelf (all 15 books, real covers, no purchase). Today `skip-first-run` gives only the four test books. Used by `.maestro/dino/needs-file/store-shot-*.yaml` and `.maestro/dino/store-shot-ios-*.yaml`.
4. **No separate Galaxy Fold pictures on Google Play** (Play has no foldable slot). Play's tablet set is retaken under the new rules.

## Work

- [ ] 1. The Debug hook (name `full-shelf`, Android and iOS twins by the shared test code): puts every book of the live store catalogue on the shelf with its real cover and no purchase record; a Debug build only (`Testing/**`), documented in `src/Dino/DinoReader.App/Testing/CLAUDE.md`. Unit test where the catalogue-to-shelf mapping is pure. Smallest end-to-end first: this item, with one phone run showing a full shelf.
- [ ] 2. Flows: `store-shot-phone.yaml`, `store-shot-tablet.yaml` and the five `store-shot-ios-*.yaml` call the hook before every shelf or book shot (shots 4 and 5 and the Books and Store pages); shot 3 uses German or French (`set-language lang=de`, falling back to `fr` if the German pack is missing), the other shots stay English. The flows keep Slice 395's recording rule and the skill's captions. The check on each shot: no empty rows, no "no stories" state, only the last row part-filled; no price on the grown-ups shot.
- [ ] 3. Android shots with status-bar demo mode on (the 393 item 14 fix: no download notification, no shield icon, a fixed clock). Google Play phone set and tablet set retaken, English captions on the global and European listings; the Farsi, Arabic and Chinese listings keep their own screens in their own language.
- [ ] 4. App Store iPhone 6.9", iPhone 6.5" and iPad 13" sets retaken through `store-screenshots.yml` (the macOS simulator job per device); pushed through the global `store-listing` agent in apply mode, no approval round; read back.
- [ ] 5. Microsoft Store desktop shots retaken from the Hyper-V VM with the full shelf (Windows has the hook too); pushed and read back.
- [ ] 6. **iPhone Duo set** (Decision 1): wait for Slice 403 and an Xcode with a Duo simulator; then new flows `store-shot-ios-duo-*.yaml` take the outer (1398 x 2034) and inner (2007 x 2853) screens in portrait; check `screenshotDisplayType` for a Duo value; upload and read back. If the simulator is missing, this item is the only open one and sits in *Not yet verified*.
- [ ] 7. Record what went live per store in Slice 291's file (one block with the run ids, the sets and the read-back result) and tick it here.

## Verification

- The hook: a phone run shows 15 books on the shelf, no purchase made; contact sheet looked at by the coordinator (small copy, never full size).
- Every pushed set read back from its store (Play API, App Store Connect API, Partner Center): count and sizes match, shot 3 is not Farsi on any non-Farsi listing, no shot shows four lonely covers.
- iPhone Duo: size and display-type check before upload.

## Not yet verified

- The iPhone Duo set, until Xcode has a Duo simulator and Slice 403 has shipped.

## Out of scope

- Separate Galaxy Fold pictures on Google Play (Decision 4). The App Store header and search-results pictures are Slice 402.
