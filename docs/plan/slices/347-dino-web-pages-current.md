# Slice 347 — Dino: both web pages tell today's facts, with fresh screenshots

*Planned (2026-11-09).*

**Touches:** `site/dino/index.html`, `site/dino/*.webp` (new screenshots), `docs/plan/slices/347-dino-web-pages-current.md`; in the
`sirous.uk` repo: `src/data/products.ts` (the `dino-reader` entry), `src/assets/products/dino-reader*.{png,webp}`.

## Why

The user, 2026-11-09: "the text on https://dinoreader.sirous.uk/ is out dated and it only contains 1 image.
https://sirous.uk/dino-reader/ is better but is outdated as well. It still says windows is in testing and shows voices to
select per character. fix both pages. take proper screenshots for the app and make sure the text tells the right facts."

Facts checked on 2026-11-09 (ground-in-code agent, origin/main and live fetches):

- Books come only from the free story store. A new install picks at least 2 books on first run (Slice 261). There is no
  scanning, PDF, typing, pasting, editor, OCR or machine translation (Slice 278). No phone voice: books play pre-made clips
  for every character plus the family's own recordings, and nobody picks a voice (Slice 278 part B). No ML Kit or
  sherpa-onnx (Slice 305).
- Reading: three views (picture only, text, picture and text), the sentence lights up as it is read, speed 0.8x to 1.2x,
  animal sounds switch. Grown-ups gate, then Books, Store and Settings tabs. A parent can record each character in each
  story language. Backup and restore. 15 languages (UI, books and audio): en, ar, da, de, es, fa, fi, fr, ja, nb, nl, pt,
  ru, sv, zh. 15 books in `content/dino/`. Ages 3 to 9. Works offline once books are downloaded.
- Availability: Microsoft Store public (`9N9VHRLCZXN5`); Android only in testing (Play page 404, testing link works); iPhone
  and iPad public TestFlight beta (`https://testflight.apple.com/join/WnF7M53D`), not on the App Store.
- Wrong today, page 1 (`site/dino/index.html`): scan/PDF/type, "dialogue parser", cast screen to choose voices,
  neural voices and OCR, "after the voice pack download", German stories "not yet translated", "scanned page or PDF"
  highlighting; one old screenshot.
- Wrong today, page 2 (`sirous.uk` `products.ts`): status "testing" for the whole product, Windows "Store submission
  pending", name "Dino Reader", "ages 3 to 8", scan/PDF/type, cast screen, "Fourteen languages" without German, voice pack,
  ML Kit / PaddleOCR / sherpa-onnx under the hood, gallery caption "change any voice", old images.

## Work

- [ ] 1. Screenshots from the Android emulator (portrait phone; dev install, no debug marks, real store books, no
  analytics): kids' shelf, reading page picture and text with a lit sentence, picture-only page, the grown-ups Store tab, the
  record screen, a right-to-left language shelf. Saved as webp in both repos.
- [ ] 2. `site/dino/index.html`: rewrite every wrong line above from the facts, a screenshot gallery, the three platform
  links with their true state.
- [ ] 3. `sirous.uk` `products.ts` dino-reader entry: same facts, name "Dino Book Reader", per-platform status (Windows
  live, Android and iPhone testing), new gallery images; `npm run build`.
- [ ] 4. Both pages read live after deploy; no wrong claim left.

## Not in this slice

- `site/dino/privacy.html` still mentions photos, scans and ML Kit; Slice 346 also touches it. Recorded, not changed here.
- Paid books (Slice 346): the pages say "free" until that ships.

## Verification

- Live https://dinoreader.sirous.uk/ and https://sirous.uk/dino-reader/ fetched after deploy: no scan, PDF, typing, voice
  picking, OCR, voice pack, "3 to 8", "Fourteen" or "Store submission pending"; Windows shown as on the Microsoft Store.
