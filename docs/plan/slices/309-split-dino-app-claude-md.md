# Slice 309 — Split `src/DinoReader.App/CLAUDE.md` (52 KB, budget ~20 KB)

*Built (wave 2026-11-07), merged as PR #907 (a997142b).* Planned 2026-11-07. The Dino app's folder rules file is read whole every time anyone works in `src/DinoReader.App/`. plan.md said 41 KB; it is now 52,793 bytes (read 2026-11-07). The promised split was never planned. Moved from plan.md's *Built but not verified*. Docs only; run it between waves, when no other branch edits the file (`archive-docs` rule).

**Touches:** `src/DinoReader.App/CLAUDE.md`, `src/DinoReader.App/**/CLAUDE.md` (new, one per area), `docs/plan/notes/dino-app-claude-md-history.md` (new), `docs/plan/slices/309-split-dino-app-claude-md.md`. The root `CLAUDE.md` line for item 4 goes in this file's *For CLAUDE.md* block and is lifted by the index pass.

## Work

- [ ] 1. Run `parallel-check`; start only when no open PR or worktree touches `src/DinoReader.App/CLAUDE.md`.
- [x] 2. `archive-docs` on that file (agent `general-purpose`, `model: sonnet`): rules stay (constraint, file to look in, link to the slice file); incident narratives and reversed decisions move to the history note; area rules (reading, store, gallery, record, Windows head) go to a nearer `CLAUDE.md` in that area's folder where the code lives, or stay if the files are all at the top level.
- [x] 3. Apply the "would an agent get this wrong without being told" test to every kept rule; drop what fails it.
- [x] 4. Update the root `CLAUDE.md` *Where the rules live* list if new folder files are created (lines in the *For CLAUDE.md* block below; lifted by the index pass).

## Done (2026-11-07)

`src/DinoReader.App/CLAUDE.md` 55,742 -> 22,415 bytes; old text moved verbatim to `docs/plan/notes/dino-app-claude-md-history.md`; new `Platforms/Windows/CLAUDE.md` and `Testing/CLAUDE.md`. Every old line is in a new file or the history note (script check: 0 missing; no broken slice links).

### For CLAUDE.md (lifted 2026-11-07)

Add to root *Where the rules live*, next to the `src/DinoReader.App/CLAUDE.md` line:

- `src/DinoReader.App/Platforms/Windows/CLAUDE.md` — Dino Reader's Windows head: package identity, key hook, ListViewBase input, Hyper-V VM test.
- `src/DinoReader.App/Testing/CLAUDE.md` — Dino Debug test hooks (`test-store`, `import-test-book`) and Maestro log markers.

## Verification

- `src/DinoReader.App/CLAUDE.md` under ~20 KB; every removed line is found in a moved-to file (`archive_docs.py verify` or a diff of line counts); links resolve.
