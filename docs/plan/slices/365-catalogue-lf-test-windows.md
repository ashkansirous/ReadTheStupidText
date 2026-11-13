# Slice 365 — The catalogue LF test passes on a Windows checkout

*In progress (wave 2026-11-13), draft PR.*

**Touches:** `worker/catalogue.test.mjs`, `.gitattributes`

## Why

`worker/catalogue.test.mjs:139` ("the page file uses LF line endings") fails on a Windows checkout because `site/dino/books.html` is `i/lf w/crlf` there (index LF, working copy CRLF). Found by `run-checks` in the s357 worktree, 2026-11-13. CI on Linux passes, so the test only breaks local runs on Windows.

## Work

- [ ] Make the check test what is committed and pass on both systems: for example a `.gitattributes` `eol=lf` rule for the site files, or reading the git blob in the test instead of the working copy. Pick one; keep the test's intent (the committed page uses LF).
- [ ] Run `node --test worker/catalogue.test.mjs` on Windows and on Linux CI.
