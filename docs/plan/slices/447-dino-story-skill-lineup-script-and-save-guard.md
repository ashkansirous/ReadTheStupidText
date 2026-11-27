# Slice 447 — Dino story skill: true-size line-up script and the accepted-page save guard
**Product:** Dino
**Touches:** `.claude/skills/create-dino-story/**`

*In progress (wave 2026-11-27), draft PR.*

*Planned 2026-11-25 (plan-cleanup of that day; moved out of Slice 350; not built).*

**Why.** Two tooling items left open in Slice 350 (moved out 2026-11-25), both for `.claude/skills/create-dino-story/`.

**Work.**
- [ ] `true-size-lineup.py` (or a PowerShell twin): cut a Gemini line-up row into connected regions and rescale to one height per character in Carrot units; name it in SKILL.md's line-up step.
- [ ] Save guard: `gemini-take-download.ps1` (or a new `save-page.ps1`) refuses to write a page listed in the book's `art.md` `## Accepted by the user` list unless called with `-UserAskedRedo`, and copies any file it replaces to `<working folder>/previous/` first. Verify: save over an accepted page without the flag exits non-zero and leaves the file unchanged.
