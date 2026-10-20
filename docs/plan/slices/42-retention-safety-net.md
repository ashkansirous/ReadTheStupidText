# Slice 42 — Retention safety net

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 42 — Retention safety net.** (Decision 58) Set repo-level
      artifact/log retention to 1 day on `ReadTheStupidText` and `Lets-Call-Mom`
      (`gh api -X PUT repos/<repo>/actions/permissions/artifact-and-log-retention
      -F days=1`). This is a settings change and needs no PR. **Retention part
      done 2026-09-27** (both repos report `days: 1`); only the docs remain. Update `CLAUDE.md`'s
      CI paragraph and `README.md`'s "uploads each as an unsigned artifact" line
      to describe the Release-only, path-filtered pipeline.

## Verification (moved from plan.md)

- **Slice 42:** `gh api repos/<repo>/actions/permissions/artifact-and-log-retention`
  returns `days: 1` for both repos. After a few days of normal merges, the
  billing page shows Actions storage near zero.
