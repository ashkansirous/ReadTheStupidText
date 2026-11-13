# Slice 356 — Dino illustrator: fewer steps per picture and a much smaller context

*Planned.*

**Touches:** `.claude/agents/dino-story-illustrator.md`, `.claude/skills/create-dino-story/SKILL.md`, `.claude/skills/create-dino-story/gemini-grab.js`, `.claude/skills/create-dino-story/illustrator-usage.mjs` (new)

## Why

The user watched the `dino-story-illustrator` agent (2026-11-13, RefineScope) and saw 2-5 seconds between each action:
every menu click, mode pick and text entry. Only two waits should be real: the page loading and Gemini drawing. The same
day a run showed **354.5k tokens after 36 minutes**. The user said "that should not be the case".

What the code shows (read 2026-11-13 from the local checkout, which may lag `main` a little):
- Sending, waiting for the picture and downloading are already one `javascript_tool` call each (`gemini-grab.js`:
  `__sendChecked`, `__waitForPicture`, `__grab`).
- Each of these is still its own `find` plus click call: the **+** menu, *Create image*, *Aspect ratio* and *3:4*. On top
  of that, `SKILL.md` says to take a screenshot before 3:4, and the agent file says "take menu steps one at a time and look
  before clicking". Each call costs one model round trip, and those round trips are the 2-5 s gaps.
- The character line-up goes in by a real clipboard paste (`gemini-clipboard.ps1` Put + Ctrl+V). Gemini ignored a fake
  upload or drop when it was tried on 2026-10-26 (`SKILL.md`), so the paste stays.

What the token count is (an analysis of run `agent-a7dbdb44bf000e049`, the lion-and-mouse redraw, on 2026-11-13):
- The 354.5k is most likely the agent's **current context size**, meaning everything it re-reads on each turn. It matches
  the per-turn input at minute 36-37 (347k-352k). It grew from 65.5k on turn 1 to 368k by turn 208. Total cache reads
  across the run were 46.9M. Earlier runs show the same shape: 197 turns ending at 371k, and 46 turns ending at 183k.
- Causes, largest first:
  1. **Turn count.** 208 turns, about 1.45k tokens added per turn and never dropped. It made 168 `computer` calls,
     95 `javascript_tool` calls and 98 Bash calls.
  2. **Pictures in the context.** 90 image blocks: 57 screenshots plus 6 zooms (about 25-40k tokens), and 27 full-size
     picture reads (about 40-55k tokens). These per-image numbers are an estimate, not read from the usage records.
  3. **Text pasted into calls.** The grab and inject scripts are pasted into tool calls (about 30k tokens of input in
     total). Tool results add about 43k, and the skill, slice and art-record files are read in full (10-18k chars each).
- About 14 pictures came out of the run, so roughly 25k context and about 3.3M cache reads per picture. This is rough.

## Decisions (user, 2026-11-13, RefineScope)

1. **One script call per picture set-up.** One `javascript_tool` call opens **+**, picks *Create image*, sets 3:4, fills in
   the prompt and sends. It returns one line, for example `ok: create-image, 3:4, 1 attachment, prompt 812 chars, sent`. If it
   can't find a menu item, it says which one, and the agent falls back to today's click-by-click steps for that picture only.
2. **Drop the screenshot before 3:4 and the "look before every click" rule.** The agent screenshots only when the script's
   line reports a problem.
3. **Keep the real clipboard paste of the line-up**, as one tight sequence: take the lock, Put, one Ctrl+V, a script check that
   the thumbnail is there, then release the lock.
4. **Stay on Sonnet.** The gain comes from fewer steps, and the full-size picture check needs the stronger model.
5. **Token use comes down, measured per picture before and after** (user, same day, on seeing 354.5k).

The full-size check of every picture stays (`feedback-dino-images-quality-and-no-harm`: every Gemini picture is checked at
full size). This slice cuts the tokens around that check, not the check itself.

## Work

- [ ] **Measure tool.** Turn the analysis scripts into `illustrator-usage.mjs <subagent jsonl>`. It prints turns, the
      first, median and last context size, cache reads, image blocks by kind, calls by tool, pictures saved, and per picture
      seconds, context and cache reads. Record the baseline from `agent-a7dbdb44bf000e049` and the two earlier runs in this file.
- [ ] **Set-up script.** Add `__setupPicture(prompt, n)` to `gemini-grab.js` for decision 1. It finds menu items by their
      visible label or aria-label, never by screen position. It checks that each step took effect (3:4 shown as selected,
      the prompt length matches) before going on, and it sends through the existing `__sendChecked` path. It returns the
      one-line report.
- [ ] **Paste check.** Add `__attachmentCount()` (or reuse `__markPictures`) so the line-up paste is confirmed by script,
      not by screenshot (decision 3). The lock is held only from Put to the confirmed thumbnail.
- [ ] **Agent and skill text.** Rewrite steps 1-7 in `dino-story-illustrator.md` and the Gemini section of `SKILL.md` to:
      open or new chat, then health check, then paste sequence, then `__setupPicture`, then `__waitForPicture`, then
      `__grab` plus save, then the full-size check. Remove the screenshot-before-3:4 and look-before-clicking rules
      (decision 2). Keep the click-by-click steps as the named fallback.
- [ ] **Fewer, shorter turns.** In the agent file:
      - Load `gemini-grab.js` once per page load. If a new chat keeps the page loaded (no full reload), check with
        `__geminiHealth` instead of injecting again. Never paste the script body into a call more than once per load.
      - Read the skill, slice and art-record files once, and only the sections the mode needs. Never re-read them per page.
      - No `computer` screenshot after a successful script line. Zoom or screenshot only on a reported problem.
      - The full-size check: Read each saved picture once, and never read it again after the verdict.
- [ ] **Fresh context per page group.** The caller (`create-dino-story`) splits a book into page groups so no single agent
      runs more than about 5 pictures (the `pages <group>` mode already exists). The agent stops and reports when its group
      is done, rather than continuing into more pages.
- [ ] **After numbers.** Run one real page group with the new steps (in the user's Chrome, the user's lane as for every
      Dino picture run) and record the same measurements next to the baseline: seconds between actions, seconds per
      picture, peak context and cache reads per picture.

## Verification

- `node .claude/skills/create-dino-story/illustrator-usage.mjs <before>` and `<after>`, both written into this file.
- Target: no gap between the paste and the send other than the script's own run time. Peak context per agent under
  about 150k (from 368k), and cache reads per picture down by at least half. If a target is missed, record why here.
- The fallback is exercised once: rename a menu label in the script's lookup, check that it reports the missing item and
  that the agent finishes that picture click-by-click.
