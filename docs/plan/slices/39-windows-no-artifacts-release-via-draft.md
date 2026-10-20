# Slice 39 — Windows: no artifacts, release via draft

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 39 — Windows: no artifacts, release via draft.** (Decisions 52,
      53, 54, 58) In `build.yml`, remove `upload-artifact` from the MSIX matrix
      job. PRs build only. On `main`, add `release-draft` (create/reuse a draft
      `v<x.y.z>` at the merge commit, skipping everything if that version is
      already *published*, matching today's idempotency). Each matrix leg
      `gh release upload --clobber`s only `ReadTheStupidText.App_*.msix`, and
      `release-publish` (needs both legs) publishes it and sets
      `released=true` for `store-submit`. Delete the `cleanup-artifacts` job,
      the `.github/actions/cleanup-old-artifacts/` action and the
      `actions: write` grant.

## Verification (moved from plan.md)

- **Slice 39:** a PR run builds both MSIX legs and the repo's artifact list
  gains nothing
  (`gh api repos/{owner}/{repo}/actions/artifacts --jq .total_count`). A
  `main` run publishes `v<x.y.z>` with exactly the two
  `ReadTheStupidText.App_*.msix` assets and `store-submit` still succeeds.
  A deliberately failed leg leaves only a draft, with no `v*` tag.
