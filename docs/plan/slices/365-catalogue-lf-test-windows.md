# Slice 365 — The catalogue LF test passes on a Windows checkout

*Built 2026-11-13, merged as PR #1061 (e338e0c5).*

**Touches:** `.gitattributes` (`worker/catalogue.test.mjs` untouched: it already checks the right thing)

## Why

`worker/catalogue.test.mjs:139` ("the page file uses LF line endings") fails on a Windows checkout because `site/dino/books.html` is `i/lf w/crlf` there (index LF, working copy CRLF). Found by `run-checks` in the s357 worktree, 2026-11-13. CI on Linux passes, so the test only breaks local runs on Windows.

## Work

- [x] Make the check test what is committed and pass on both systems: for example a `.gitattributes` `eol=lf` rule for the site files, or reading the git blob in the test instead of the working copy. Pick one; keep the test's intent (the committed page uses LF).
- [x] Run `node --test worker/catalogue.test.mjs` on Windows and on Linux CI. (Windows done; Linux CI runs on the PR.)

*Changed while building:* chose the `.gitattributes` route: line `site/** text=auto eol=lf`. `auto` keeps the 11 png/webp/mp4 binaries as `-text` (checked with `git ls-files --eol`); a plain `text` would have let git convert them. The test is unchanged. The rule also makes `worker/check-site-eol.mjs` pass on Windows. Before: `catalogue.test.mjs` failed here (`books.html` `i/lf w/crlf`). After re-checking out the `site/` files: `node --test worker/*.test.mjs` 367/367, `check-site-eol` OK.

Existing Windows checkouts keep CRLF copies until those `site/` files are re-checked out (delete, then `git checkout -- <file>`); fresh clones are fine.

*From review:*
- High (`reviews/s365-high.md`) #1 applied: `text` changed to `text=auto` so binaries are not converted. #2 applied as the recorded re-checkout step above. #3 recorded here (status, ticks, Touches).
- Medium (`reviews/s365-medium.md`) #1 is the same re-checkout note, applied. #2 applied by this docs pass.
- Skipped #3 (`reviews/s365-medium.md`): "relies on git sniffing; siblings use explicit" - `text=auto` was verified to detect all the binaries as `-text`, and a per-extension list adds upkeep with no failing case.

#### For CLAUDE.md (lifted 2026-11-13)

- `worker/CLAUDE.md`: `.gitattributes` forces LF for `site/**` (`text=auto eol=lf`); an existing Windows checkout keeps CRLF copies until those files are re-checked out (delete, `git checkout -- <file>`), and only then do `catalogue.test.mjs` and `check-site-eol.mjs` pass. (Slice 365: docs/plan/slices/365-catalogue-lf-test-windows.md)

#### Not yet verified

- The test run on Linux CI (the PR's checks).
