# Slice 447 — Dino story skill: true-size line-up script and the accepted-page save guard
**Product:** Dino
**Touches:** `.claude/skills/create-dino-story/**`, `.claude/agents/dino-story-illustrator.md` (widened by the coordinator after review 2: its save steps pass `-BackupFolder`)

*Built (wave 2026-11-27), merged as PR #1414 (2317355a); the real Gemini save is not yet verified (see Not yet verified). Planned 2026-11-25, moved out of Slice 350.*

**Why.** Two tooling items left open in Slice 350 (moved out 2026-11-25), both for `.claude/skills/create-dino-story/`.

**Work.**
- [x] `true-size-lineup.py` (or a PowerShell twin): cut a Gemini line-up row into connected regions and rescale to one height per character in Carrot units; name it in SKILL.md's line-up step.
- [x] Save guard: `gemini-take-download.ps1` (or a new `save-page.ps1`) refuses to write a page listed in the book's `art.md` `## Accepted by the user` list unless called with `-UserAskedRedo`, and copies any file it replaces to `<working folder>/previous/` first. Verify: save over an accepted page without the flag exits non-zero and leaves the file unchanged.

*Changed while building:*
- `true-size-lineup.py` (Pillow, run with `python -I`): rescales each figure to its height in Carrot units on one baseline. A wrong figure count writes nothing and exits 2; bad `--heights`, `--gap` or `--carrot-px` exit 2.
- New `accepted-pages.ps1` holds the guard. A page listed in `art.md` `## Accepted by the user` (table or bullets, heading matched by prefix) is refused without `-UserAskedRedo`; an unreadable accepted section refuses too. A save into book content needs `-BackupFolder <working folder>/previous` (refused otherwise, nothing touched). Backups never overwrite (timestamp plus counter).
- `gemini-take-download.ps1` moves the download to a temp name beside the target, then replaces, so the original is never deleted first.
- `gemini-step.ps1` `save` and `collect` pass `-UserAskedRedo` and `-BackupFolder`; `collect -UserAskedRedo` replaces an already saved page.
- `dino-story-illustrator.md` save steps pass `-BackupFolder`; the slice's **Touches** was widened to include it.
- Tests: `node --test ".claude/skills/create-dino-story/*.test.mjs"`, 38 pass (`save-guard-and-lineup.test.mjs` is new).

#### From review
- Review files read in full: the high pass (8 findings, 7 fixed), the medium pass (7 findings, 5 fixed), the check pass (6 findings, the two important ones fixed) and the second check (no findings); the skips are listed below.
- Open, low: the `catch` around `Save-Untouched` in `gemini-take-download.ps1` can print a stale "picture is at ..." path when a step after `Move-IntoPlace` throws (`reviews/pr-1414-check2.md`, cosmetic). Follow-up to add as a low item.
- Skipped #7 (`reviews/pr-1414-high.md`): "`find_figures` rebuilds the full-resolution mask per figure, and tightening crops include neighbours." - a precision/performance nit with no failing case shown; heights tested exactly at 400/200/100 px.
- Skipped #3 (`reviews/pr-1414-medium.md`): "The backup is made after the download is found but before the save is known to succeed." - a failed retry only leaves an extra copy in previous/; nothing is lost.
- Skipped #3 (`reviews/pr-1414-check.md`): "An accepted name with no folder (bullet `- page 3: 3.jpg`) becomes stem `3`, and `EndsWith("/3")` matches any folder's `3` (pictures/3, characters/3)." - a bare accepted name blocking a save elsewhere fails closed, the safe direction for accepted pictures.
- Skipped #4 (`reviews/pr-1414-check.md`): "`_tighten` takes the bounding box of all foreground pixels in a region's box and `compose` crops the full-colour image to it." - the same crop precision point as high #7, already skipped; heights are tested exactly.
- Skipped #6 (`reviews/pr-1414-check.md`): "The backup copy is made before the save is known to succeed." - the same as medium #3, already skipped: an extra backup copy, nothing lost.
- Skipped #5 (`reviews/pr-1414-medium.md`): "`scale = image.width // WORK_WIDTH` uses integer division." - the scale only sets the detection mask's working size; output heights are computed from the original pixels and tested exactly.

#### For CLAUDE.md (lifted 2026-11-27)
None.

#### Not yet verified
- `[session]` One real Gemini save of an accepted page is refused, and a `-UserAskedRedo` save backs up to the working folder's `previous/`, in the next illustrator run.
