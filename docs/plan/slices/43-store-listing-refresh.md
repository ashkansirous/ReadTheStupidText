# Slice 43 — Store listing refresh (Microsoft Store + Google Play, 9 languages)

*Planned.*

**Batch:** 9
**Decisions:** 71 (also 60, 61 for the download disclosure)
**Depends on:** none (docs-only; can run any time)
**Touches:** `STORE.md`, `docs/store-listings/**`
**Story issue:** —

## Goal

The Store and Play pages currently describe a Windows tray reader in English
only. Replace them with a description and search terms that match how people
actually look for the app ("read aloud", "text to speech"), in all nine
languages the app will support (en, fa, fr, es, pt, zh, ar, ru, ja). The user
pastes the text into Partner Center and Play Console.

## Steps

- [ ] Add a **Store listing** section to `STORE.md` (after *Capabilities and
      justification*, ~line 121-131): the 7 search terms from Decision 71
      (each <= 30 chars, count them), the new long description, and the short
      description (Play: <= 80 chars; Store: description first line).
- [ ] Write the English description to cover both platforms: reads selected or
      copied text (Windows), pasted text, files (.txt/.pdf/.docx) and scanned
      pages (Android), natural offline voices, 0.5x-2x speed, nothing leaves the
      device. Keep claims true to what ships today; do not promise Batch 9
      features (translation, languages) until their slices land.
- [ ] Draft the nine translations as `docs/store-listings/<lang>.md`
      (`en`, `fa`, `fr`, `es`, `pt`, `zh`, `ar`, `ru`, `ja`): same three blocks
      each (search terms, short, long). Search terms are re-chosen per language
      (not literal translations). Mark each file "machine-drafted, needs a
      native read before submission".
- [ ] Add a **disclosure paragraph** to the English draft and a TODO marker in
      each translation: once Slice 50 (Android) / 56 (Windows) land, the first
      launch downloads a ~145 MB voice pack. Store policies need this stated in
      the listing and the privacy text. Do not publish this paragraph before
      those slices ship.
- [ ] Document the manual step in `STORE.md`: Partner Center -> Product ->
      Store listings -> Add/manage languages; Play Console -> Grow -> Store
      presence -> Main store listing -> Manage translations. No title change
      (Decision 71).
- [ ] Note in the section that Slice 50/56 must re-check the listing text
      against what is then true (offline claim, download disclosure).

## Acceptance

- `STORE.md` has the Store listing section with exactly 7 terms, each <= 30
  chars.
- `docs/store-listings/` holds 9 files, each with search terms, short and long
  description; the RTL ones (`fa`, `ar`) are stored as plain UTF-8 text.
- The user confirms the text was pasted into both consoles (manual).

## Risks / notes

- Docs-only: commit as a patch (no version-marker token).
- Translations are drafts; a wrong store description in a language the user
  cannot read is a real risk, so flag it rather than ship silently.
- The Store caps description and search-term lengths; check the current limits
  in Partner Center when pasting.
