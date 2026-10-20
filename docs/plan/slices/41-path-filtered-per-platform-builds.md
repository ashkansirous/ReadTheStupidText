# Slice 41 — Path-filtered per-platform builds

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 41 — Path-filtered per-platform builds.** (Decisions 56, 57) Add
      the `changes` job with the core/windows/android filters. Gate `test`,
      the Windows build + `store-submit`, and `build-android` + `play-submit`
      on their groups, and make `release-draft`/`release-publish` tolerate a
      skipped platform (`always()` + explicit result checks, not a bare
      `needs` that a skip would cascade-skip). Cut no release when neither
      platform changed, carry forward the unchanged platform's assets from its
      last release, and harden `store-submit`'s dispatch-mode "latest release"
      lookup to the newest release that has MSIX assets. `workflow_dispatch`
      builds both platforms.

## Verification (moved from plan.md)

- **Slice 41:** a PR touching only `src/ReadTheStupidText.Mobile/**` skips
  both Windows legs (and `store-submit` on `main`). One touching only
  `App`/`Infrastructure` skips `build-android`. A `Domain` change builds both,
  and a docs-only change builds nothing and cuts no release. After an
  Android-only merge, `releases/latest` still has both MSIX assets (carried
  forward).
