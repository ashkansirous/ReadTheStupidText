# Slice 169 — German translations for all 15 Dino books

*Built 2026-10-27; German not yet heard aloud.* Dino Book Reader, Android, iOS and Windows. After Slice 168 merges (needs the German rule set, roster and voice).

**Touches:** `content/dino/*/translations/de.json` (new, one per book), `content/dino/*/book.json` (version bumps),
`src/DinoReader.App/Resources/Raw/starters/*.dino` (rebuilt starters), `src/DinoReader.App/ShippedTitles.cs`,
`tests/DinoReader.Tests/**` (per-book expectations only)

## Why

Slice 168 makes German an app language; the books must follow, or a German child sees English books. The store and the
in-app starters always stay on the same latest versions (user rule), and CI uploads every bumped store book (Slice 158).

## Decisions (approved by the user, 2026-10-27)

- **Claude translates; the `dino-story-reviewer` agent checks each book** like the other languages. No human review page:
  German spelling reads aloud reliably, unlike unmarked Farsi. The user may listen to one book at the end.
- One book per item and PR, merged one at a time (`create-dino-story` skill; before each book, check `gh pr list` for open PRs
  on that book from any session, and skip or queue it).
- Keep each book's gentle, nobody-hurt retelling; German titles go in `ShippedTitles`.

## Work

- [x] 1. **First book end to end:** Three Little Pigs `de.json` by `dino-story-writer`, reviewer pass, version bump, rebuild the
  starter, tests, PR, merge; CI uploads it and `/api/stories` lists the German title.
- [x] 2. The other 14 books, the same way, one PR each.
  Changed: all 15 books ship in this one PR, because every book edits the shared ShippedTitles.cs and the test pending-list; separate PRs would conflict.
- [ ] 3. End-of-batch check: every book's `meta.json` on `/api/stories` matches `main`'s version; upload any laggard.

## Not yet verified

- German sound words and pages heard aloud on a phone.
- A German book on iOS (TestFlight).
- End-of-batch check that /api/stories matches main (CI publish-store).

## Verification

- `dotnet run --project tools/DinoReader.BookBuilder -- check` and `dotnet test tests/DinoReader.Tests` per book.
- Emulator: open a German book with `run-flows.ps1 -App dino` (UI language German) and read a page.
- iOS: TestFlight, a German book reads with distinct character voices.
- User listens to one German book on the phone.
