# Slice 43 — Store listing refresh (Microsoft Store + Google Play, 9 languages)

*Built (wave 2026-10-20), PR #174.*

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

- [x] Add a **Store listing** section to `STORE.md` (after *Capabilities and
      justification*, ~line 121-131): the 7 search terms from Decision 71
      (each <= 30 chars, count them), the new long description, and the short
      description (Play: <= 80 chars; Store: description first line).
- [x] Write the English description to cover both platforms: reads selected or
      copied text (Windows), pasted text, files (.txt/.pdf/.docx) and scanned
      pages (Android), natural offline voices, 0.5x-2x speed, nothing leaves the
      device. Keep claims true to what ships today; do not promise Batch 9
      features (translation, languages) until their slices land.
- [x] Draft the nine translations as `docs/store-listings/<lang>.md`
      (`en`, `fa`, `fr`, `es`, `pt`, `zh`, `ar`, `ru`, `ja`): same three blocks
      each (search terms, short, long). Search terms are re-chosen per language
      (not literal translations). Mark each file "machine-drafted, needs a
      native read before submission".
- [x] Add a **disclosure paragraph** to the English draft and a TODO marker in
      each translation: once Slice 50 (Android) / 56 (Windows) land, the first
      launch downloads a ~145 MB voice pack. Store policies need this stated in
      the listing and the privacy text. Do not publish this paragraph before
      those slices ship.
- [x] Document the manual step in `STORE.md`: Partner Center -> Product ->
      Store listings -> Add/manage languages; Play Console -> Grow -> Store
      presence -> Main store listing -> Manage translations. No title change
      (Decision 71).
- [x] Note in the section that Slice 50/56 must re-check the listing text
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

## Built

New **Store listing** section in `STORE.md`: 7 English search terms, the short
description, the field limits to re-check when pasting, the manual Partner
Center / Play Console steps, and a note that the title is unchanged.
`docs/store-listings/{en,fa,fr,es,pt,zh,ar,ru,ja}.md` each hold search terms,
short and long description. All fields are within limits (longest Play short
description is 79/80). The eight non-English files carry a "machine-drafted,
needs a native read" banner. The first-run download disclosure (~145 MB pack,
system voice until it lands) is a separate "ADD WHEN SLICE 50/56 SHIPS" block in
`en.md` and a TODO in the others; no live text promises a download.

*Changed while building / from review:*

- Microsoft Store limits were taken from this slice, not checked live.
- "No ads" is left out of the copy (unconfirmed).
- Camera scan is described as shipping (`ScanPage` and `MlKitImageTextExtractor`
  are on `main`).

#### For CLAUDE.md

None.

#### For README.md

None.

#### Not yet verified

- Native-speaker read of the 8 translations.
- Text pasted into Partner Center (9 languages, description + 7 search terms) and Play Console (short + full description).
- Download disclosure to be added once Slices 50 and 56 ship; re-check the "works offline" wording then.
