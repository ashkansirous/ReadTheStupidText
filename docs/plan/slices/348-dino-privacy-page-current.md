# Slice 348 — Dino: the privacy page describes today's app

*Planned (2026-11-09).*

**Touches:** `site/dino/privacy.html`, `docs/plan/slices/348-dino-privacy-page-current.md`

## Why

The user, 2026-11-09: "fix the privacy page." Found in Slice 347's review: https://dinoreader.sirous.uk/privacy.html
still describes the app before Slices 278 and 305: stories added by typing, pasting, files, photos and scans; on-device
text recognition; on-device translation models; an offline voice engine; voice packs from GitHub Releases; "Google ML Kit
(Android)" sending Google diagnostics; voices chosen for characters; usage counts for adding own books (by source), voice,
voice style and narrator changes, the phone's own voice as a fallback, and the intro wizard's "adding a book" step.

The app today (`src/DinoReader.App/CLAUDE.md`): store books only (no wizard, editor, scan/photo/file import or machine
translation); no phone voice, books play pre-made clips plus family recordings; no ML Kit, sherpa-onnx or onnxruntime in the
Dino packages (Slice 305); recordings per character and story language; backup and restore; usage counts, crash reports
and Report a problem as already described.

## Work

- [ ] 1. Every paragraph of `site/dino/privacy.html` checked against the code on `main`: remove what the app no longer
  does (own books, photos, scans, OCR, translation models, voice engine, voice packs, ML Kit, voice choices), keep every
  disclosure of what it still does (recordings, backups, story store and audio downloads, usage counts, crash reports,
  Report a problem, server request log, Windows differences if any). The usage-count list is rebuilt from the event names the
  code actually sends (the closed list in the app's usage-count code), not from the old page.
- [ ] 2. Effective date moves to the merge date. Nothing new is promised that the code does not do.
- [ ] 3. Live page read back after deploy.

## Not in this slice

- Slice 346 (paid books) will change this page again when purchases ship.
- Store data-safety and App Privacy forms: the app now collects less, so the published answers stay true (they declare
  usage and diagnostics, not linked, no tracking); a re-check goes with Slice 291's store push.

## Verification

- Live https://dinoreader.sirous.uk/privacy.html has no scan, photo, OCR, ML Kit, translation model, voice engine or voice
  pack; each usage count it names exists in the code.
