---
name: farsi-review-book
description: Run the Farsi pronunciation review for one Dino Reader book (Slice 154) - build its review page with every word the user already answered in earlier books prefilled, offer spoken alternatives for sound words the piper-fa voice drops, re-record on request, and when the user says done, write the book's hidden "how to read" table, bump, build and merge. Use when the user says "next Farsi book", "review <book> in Farsi", "done" on a Farsi review page, or works on Slice 154. Run it through the `farsi-review` agent, never from the main thread.
---

# Farsi pronunciation review, one book at a time

The user types how each Farsi word sounds, in Finglish, on a claude.ai review page. Their answers become the book's hidden
"how to read" table: kids see plain Farsi, the voice reads the marked text (Slice 154,
`docs/plan/slices/154-dino-farsi-arabic-vowel-marks.md`; `speak:<lang>` rule in `src/DinoReader.Core/CLAUDE.md`).
Arabic is out of scope (user, 2026-10-27).

## Rules the user set (do not relax)

- **Prefill.** Copy the user's answers from every earlier book's page, matched by the Farsi word, into the new page as done.
  Only words never answered before show under "To check". "95% of the time they're pronounced the same; don't waste my time."
- **Never accept a misaligned spelling.** A Finglish answer counts as checked only when `finglish_to_marks` lines it up with
  the word's letters. A misaligned one stays under "To check" with the problem shown.
- **No guesses you can't stand behind.** Leave `guess` empty rather than offer a literary or wrong reading
  (e.g. informal درو = "dar ro", not "dero").
- **Sound words the voice drops** (short repeated exclamations; `mana` drops short words) get 2-3 spoken-only options with
  audio. Kids still see the original text. The page must show unanswered option boxes under "To check".
- **Quiet.** Report only the page link, a question you cannot settle, and "merged". No status lines.

## Pages done so far

| Book | Page | State |
| --- | --- | --- |
| three-little-pigs | https://claude.ai/artifact/VAMNwsWLKHMNjMd7r1dbwi | merged (#470, #481) |
| gingerbread-man | https://claude.ai/artifact/MzzGdgir8c5iUC2mK5TXeM | review open |

Add a row when you publish a page and update it when its book merges (same PR as the book).

## Steps

1. **Worktree** from `origin/main`: `feat/dino-farsi-marks-<stem>` in `D:\ReadTheStupidText-worktrees\fa-marks-<stem>`.
   Book text: `content/dino/<stem>/translations/fa.json`. Order: starters first, then store books.
2. **Page.** Copy the newest page above (read it with the Artifact tool), change only the title, publish it as a new artifact
   with `capabilities {"assets":{}, "db":{"rules":[{"path":"words","write":"admin"},{"path":"pages","write":"admin"}]}, "user":{}}`.
   DB: `words` (word, page, order, count, guess), `pages` (p<n>: page, text, audioUrl, version), `answers`, `alternatives`
   (page, group, order, replaces, text, audioUrl), `choices` (doc `p<page>-<group>`), `requests/regen`, `requests/status`.
3. **Seed** words, pages, prefilled answers (step "Prefill" above; note "from <book>"), page audio and alternatives.
   Audio: `tools/sound-words-test` (`engines.build_piper`, `regen_review_audio.render_webm`), voice `mana`, webm only,
   uploaded as artifact assets in one publish. Find dropped words with `regen_review_audio.py --probe`-style loudness checks.
4. **Hand the link** to the caller with the count of new words to check.
5. **Re-record on request.** While the user reviews, check `requests/regen` vs `requests/status` every few minutes
   (cheap reads only). When `regen.at > status.at`: pull `answers` and `choices` with `out_dir`, run
   `build_marks_list.py` then `regen_review_audio.py --all`, upload the clips, update each `pages/p<n>` and `requests/status`
   pinned with `if_version`. Stop polling when the user says done or after 2 hours idle.
6. **Done.** Pull answers and choices, run `build_marks_list.py` and `apply_marks.py` to write
   `content/dino/<stem>/translations/fa.marks.json` and `fa.speak.json` (`fa.json` never changes), then
   `dotnet run --project tools/DinoReader.BookBuilder --disable-build-servers -- stamp <stem>`, `-- build <stem>`, `-- check`,
   `dotnet test tests/DinoReader.Tests` (FarsiMarksTests) and the Android build (`-p:IncludeWindows=false`). One line in the
   Slice 154 file. Commit, PR (`--base main`, Summary + Test plan), merge when green with a merge commit. Store books then
   upload themselves (`publish-store` job); check its result.

## Local paths (this machine)

- Python venv with numpy and the engines: `D:\ReadTheStupidText-worktrees\s154b\tmp\venv\Scripts\python`
  (else `python -m venv` + `pip install -r tools/sound-words-test/requirements.txt`). Run scripts with `-I` and the tool dir on `sys.path`.
- piper-fa pack: `D:/ReadTheStupidText-worktrees/s145-tmp/packs/x/piper-fa-2/piper-fa` (or `RTST_PIPER_FA_ROOT`).
- Review data and audio go in the book's worktree `tmp/` (gitignored), never in a temp folder that can be cleaned.
