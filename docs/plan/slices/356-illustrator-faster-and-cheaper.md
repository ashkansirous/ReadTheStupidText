# Slice 356 — Dino illustrator: fewer steps per picture and a much smaller context

*Built 2026-11-13, not merged, not yet verified on a real page group (see Not yet verified).*

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

- [x] **Measure tool.** Turn the analysis scripts into `illustrator-usage.mjs <subagent jsonl>`. It prints turns, the
      first, median and last context size, cache reads, image blocks by kind, calls by tool, pictures saved, and per picture
      seconds, context and cache reads. Record the baseline from `agent-a7dbdb44bf000e049` and the two earlier runs in this file.
- [x] **Set-up script.** Add `__setupPicture(prompt, n)` to `gemini-grab.js` for decision 1. It finds menu items by their
      visible label or aria-label, never by screen position. It checks that each step took effect (3:4 shown as selected,
      the prompt length matches) before going on, and it sends through the existing `__sendChecked` path. It returns the
      one-line report.
      Built; dry run, fallback and one real send with a pasted line-up passed live 2026-11-13 (see Live check). The check-pass edit (pictures validation, mark only after all checks) was not live-sent.
- [x] **Paste check.** Add `__attachmentCount()` (or reuse `__markPictures`) so the line-up paste is confirmed by script,
      not by screenshot (decision 3). The lock is held only from Put to the confirmed thumbnail.
      Built; confirmed with a real pasted line-up in the live check 2026-11-13 (count 1).
- [x] **Agent and skill text.** Rewrite steps 1-7 in `dino-story-illustrator.md` and the Gemini section of `SKILL.md` to:
      open or new chat, then health check, then paste sequence, then `__setupPicture`, then `__waitForPicture`, then
      `__grab` plus save, then the full-size check. Remove the screenshot-before-3:4 and look-before-clicking rules
      (decision 2). Keep the click-by-click steps as the named fallback.
- [x] **Fewer, shorter turns.** In the agent file:
      - Load `gemini-grab.js` once per page load. If a new chat keeps the page loaded (no full reload), check with
        `__geminiHealth` instead of injecting again. Never paste the script body into a call more than once per load.
      - Read the skill, slice and art-record files once, and only the sections the mode needs. Never re-read them per page.
      - No `computer` screenshot after a successful script line. Zoom or screenshot only on a reported problem.
      - The full-size check: Read each saved picture once, and never read it again after the verdict.
- [x] **Fresh context per page group.** The caller (`create-dino-story`) splits a book into page groups so no single agent
      runs more than about 5 pictures (the `pages <group>` mode already exists). The agent stops and reports when its group
      is done, rather than continuing into more pages.
- [ ] **After numbers.** Run one real page group with the new steps (in the user's Chrome, the user's lane as for every
      Dino picture run) and record the same measurements next to the baseline: seconds between actions, seconds per
      picture, peak context and cache reads per picture.

## Baseline (measured with `illustrator-usage.mjs`, built 2026-11-13)

A picture is counted at each javascript_tool RESULT that is `__grab`'s blob line (`image/jpeg <bytes>`), never from the call's
script text, so retries of one page count again. The first real send is a `__setupPicture` result starting `ok:` or a
`__sendChecked` result `SENT` (never `dry-run ok`). Context = input + cache read + cache creation per turn.

| Run | Turns | Minutes | Grabs | Context first / median / peak | Cache reads total | Per grab: median s, cache reads | Image blocks |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `agent-a7dbdb44bf000e049` (lion-and-mouse redraw) | 209 | 39.4 | 15 | 65.5k / 226k / 374.7k | 47.2M | 144 s, 2.84M | 57 screenshots, 6 zooms, 27 picture reads |
| `agent-a86480a1a124b3fb3` | 197 | 46.5 | 24 | 65.4k / 233k / 370.8k | 45.9M | 115 s, 1.77M | 10 screenshots, 2 zooms, 23 picture reads |
| `agent-ae3e746cb512714f9` (review only) | 46 | 7.1 | 0 | 30.6k / 128k / 183.2k | 5.5M | none | 42 picture reads |

Re-measured after the review fixes (per-message-id turn dedupe, a real comment/string scan for `__grab` counting, set-up reported
apart): grabs (15, 23, 0), turns, minutes, context and median seconds are unchanged. Only "cache reads per grab" changed
(3.15M to 2.84M and 2.0M to 1.85M) because it now sums the pictures' own turns and leaves the set-up before the first
`__sendChecked`/`__setupPicture` call out (157 s, 29 turns, 3.15M cache reads in the first run; 109 s, 18 turns, 1.93M in the
second). Both baseline runs predate `__setupPicture`, so their "set-up" ends at the first `__sendChecked`.

Re-measured again after switching to result-based counting: the second run now counts 24 grabs, not 23 (24 real blob results
about 2 minutes apart; the old text scan missed one call), so its median is 115 s and cache reads per grab 1.77M. The first run's
median is 144 s (was 145; the set-up boundary moved 1 s); all else is unchanged.

Context grows about 10-16k per picture in both drawing runs (first run: 140k at picture 1, 363k at picture 15), so cache reads
per picture rise as the run goes on. The first run's per-picture turn count was 9-32; the second's 7 after the first few.

Set-up script dry run (2026-11-13, own tab, cold page, nothing sent): `dry-run ok: create-image, 3:4, 0 attachment, prompt 31
chars, not sent`, 917 ms for the whole set-up. Real labels read from the page: **Upload & tools** button, item *Create image*
(chip then reads `Deselect Images`), button *Aspect ratio* (reads `Aspect ratio, 3:4` once set), menu items `role=menuitemradio`
with aria-label `3:4`. The send, the paste with an attachment and the fallback were exercised later the same day (Live check below).

## Verification

- [x] Passed 2026-11-13 (Live check below, on the version before the check-pass edit): one live `__setupPicture` send with a pasted line-up (attachment count 1 or more, `ok: ..., sent`).
- [x] Passed 2026-11-13 (Live check below, label broken in the page by a MutationObserver): the missing-menu-item fallback.

**Live check 2026-11-13** (own tab, 1 full page load plus 1 in-app New chat, lock taken only around paste and download):

1. Dry run on a cold page: FAIL on the first call, PASS on the second. First call (`dryRun`, 0 attachments) returned `MISSING: composer area (could not count attached pictures; ...)` after 1152 ms: right after the prompt fill the Send button was not yet in the composer, so the area lookup was null. Same call repeated about a minute later: `dry-run ok: create-image, 3:4, 0 attachment, prompt 81 chars, not sent` in 3233 ms. Likely a render race after the fill; the area lookup should wait (`waitFor`) for the Send button before returning UNKNOWN. Not fixed here.
2. Fallback: PASS. A MutationObserver renamed every `Aspect ratio...` aria-label to a nonsense label in the page; `__setupPicture(..., {dryRun:true})` returned `MISSING: Aspect ratio 3:4` in 1565 ms and sent nothing. (My first attempt renamed the label once, the script's Create image re-pick re-rendered it, and the call really sent a text prompt with 0 attachments: one stray picture request, my mistake, in a chat that was then left.)
3. Real send with pasted line-up (billy-goats-gruff/characters/lineup.jpg): PASS. `__attachmentCount()` was 1 (paste to confirmed about 2.7 s, including one tool round trip); `__setupPicture(..., 1)` returned `ok: create-image, 3:4, 1 attachment, prompt 81 chars, sent` in 1396 ms; `__waitForPicture` returned `READY 1` after 9006 ms; `__grab(1)` returned `image/jpeg 2654604` in 11565 ms; `gemini-take-download.ps1 -Bytes 2654604` saved 1792x2400 in 1638 ms. Lock held about 5 s for the paste and about 14 s for grab and save. Picture is in the scratchpad `s356-live\cand-1.jpg`, not in any book.
4. Re-run of check 1 after the 3 s composer-area wait (own tab, one cold page load, health `OK`, then `__setupPicture("A friendly green dinosaur waving", 0, {dryRun:true})` at once): PASS. Re-done with the WHOLE worktree `gemini-grab.js` injected as one `javascript_tool` call (full text, as the illustrator does), in a new own tab with one cold load, health `OK` first. Returned `dry-run ok: create-image, 3:4, 0 attachment, prompt 32 chars, not sent` in 1262 ms (an earlier run with a trimmed copy gave 1179 ms). Cleared afterwards (prompt box empty, Create image chip deselected); nothing sent.

- `node .claude/skills/create-dino-story/illustrator-usage.mjs <before>` and `<after>`, both written into this file.
- Target: no gap between the paste and the send other than the script's own run time. Peak context per agent under
  about 150k (from 368k), and cache reads per picture down by at least half. If a target is missed, record why here.
- The fallback is exercised once: rename a menu label in the script's lookup, check that it reports the missing item and
  that the agent finishes that picture click-by-click.

## From review

Three findings files, all read to their last finding: `reviews/s356i-high.md` 9/9, `reviews/s356i-medium.md` 10/10, `reviews/s356i-check.md` 9/9.

- `reviews/s356i-high.md` (9 findings): all 9 fixed, none skipped.
- `reviews/s356i-medium.md` (10 findings): 9 fixed, #3 skipped (below).
- `reviews/s356i-check.md` (9 findings): #1-#4 fixed, #9 fixed in passing, #5-#8 skipped (below).
- Skipped #5 (`reviews/s356i-check.md`): "chooseCreateImage always deselects then re-picks a correct chip, adding round trips and a MISSING risk on a slow deselect" - deliberate: a stale chip is never trusted (medium #10).
- Skipped #6 (`reviews/s356i-check.md`): "Create image item matched by exact innerText equality; an icon ligature or badge in the text makes it MISSING every time" - the live check on 2026-11-13 matched the real labels.
- Skipped #7 (`reviews/s356i-check.md`): "composer().area now needs a Send button with no fallback; a missing Send (mic or stop state) stalls 3 s and returns MISSING: composer area" - MISSING sends the agent to its click-by-click fallback, which is the intended behaviour.
- Skipped #8 (`reviews/s356i-check.md`): "attachedPictures counts any image of 48 px or more in the whole area ... so unrelated images could satisfy `attached >= pictures`" - the live check on 2026-11-13 counted the pasted line-up as 1.
- Fixed in passing #9 (`reviews/s356i-check.md`): the entry-point check in `illustrator-usage.mjs` now compares the full path.
- Fixed #1-#3 (`reviews/s356i-check.md`): `illustrator-usage.mjs` no longer scans script text; it counts a picture from the javascript_tool result (`image/jpeg <bytes>`) and the first send from an `ok:` or `SENT` result (`codeOnly` removed).
- Fixed #4 (`reviews/s356i-check.md`): `__setupPicture` validates `pictures` and marks the picture count only after every check passed, right before sending.
- Skipped #3 (`reviews/s356i-medium.md`): "hard-coded English aria-labels ("Deselect Images", "Upload & tools", "3:4"); the 3:4 lookup is page-global" - a non-English UI gives `MISSING: <item>` and the agent falls back click by click, so the failure is reported, not silent.
- Fixed #1 (`reviews/s356i-medium.md`): an unfindable composer area is now UNKNOWN (`__attachmentCount()` returns `'UNKNOWN'`, `__setupPicture`/`__sendChecked` return `MISSING: composer area`), never 0 or n; the page-wide fallback stays removed.
- Fixed #10 (`reviews/s356i-medium.md`): a Create image chip already showing is deselected and re-picked through the menu in the same call (costs one extra click when the chip persists in a chat).
- Fixed #2, #4-#9 (`reviews/s356i-medium.md`): ratio and chip re-checked just before sending, dry run documented as leaving the prompt filled, usage-tool turn and comment handling, group cap and step 1 wording reconciled between agent file and `SKILL.md` (3 per redraw group, at most 5 otherwise).
- Fixed #1-#9 (`reviews/s356i-high.md`): exact `Deselect Images` chip match, composer-only attachment count, expected-count agreement with `__sendChecked`, focus check and one retry in the prompt fill, usage-tool header-comment and empty-run guards, step 1 reading wording, group-size wording, and the Work ticks made honest (finding 9).
- `reviews/s356i-check2.md` (10 findings, second check pass over the check-pass fixes): all 10 skipped, no code changed after it.
  - #1, #4: no Send button means `__attachmentCount` gives UNKNOWN and the area-based lookups fall back to the page. `__setupPicture`/`__sendChecked` still return `MISSING: composer area` before any send, so nothing is sent on a wrong match; the live paste check counted 1 with Send present.
  - #2: `MISSING` instead of `REFUSED` for text-only with no Send: both stop the send and the agent goes click by click.
  - #3: a throw from `__sendChecked` leaves the mark advanced; the agent treats a thrown call as a failed picture and starts a new chat (one prompt per chat), so the mark is not reused.
  - #5: `ratioIs34` reads only the aria-label: the live check saw `Aspect ratio, 3:4` there.
  - #6, #7: any `image/<type> <bytes>` result or `ok:` result counts. Line-up, sheets and retries are real Gemini pictures and count as such; per-picture numbers are a before/after comparison measured the same way on both sides.
  - #8: zero or negative per-picture seconds only when a save comes before the first send (a sheet saved before the first page). It affects one row at most, and the median is reported.
  - #9: test temp dirs and loose assertions: test hygiene only.
  - #10: extra stale attachments: the agent text already says to remove a stray thumbnail before sending.

#### For CLAUDE.md

None expected: the rules live in `.claude/skills/create-dino-story/SKILL.md` and `.claude/agents/dino-story-illustrator.md`.

#### For README.md

None (tooling only, nothing user-facing).

#### Not yet verified

- After-numbers on a real page group of a book (seconds per picture, peak context, cache reads per picture against the Baseline table), to be taken on the next real illustrator run with `illustrator-usage.mjs`.
- A real send after the check-pass edit to `__setupPicture` (pictures validation, mark only after all checks, mark restored on REFUSED/MISSING); only the earlier version was live-sent on 2026-11-13.
