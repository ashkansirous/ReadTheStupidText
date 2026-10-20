# Slice 40 — Android into the same pipeline and release

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 40 — Android into the same pipeline and release.** (Decisions
      52, 55) Move the `android-build.yml` build into `build.yml` as
      `build-android` (needs `version`, `test`, `release-draft`), keeping the
      unsigned Debug compile check and the `HAS_SIGNING` gate. On `main` it
      uploads the signed `.aab` + `.apk` to the draft, with no
      `upload-artifact`. `release-publish` also needs `build-android`. A new
      `play-submit` job (after publish, `HAS_PLAY_PUBLISHING`-gated) downloads
      the `.aab` from the Release and runs `r0adkll/upload-google-play`
      unchanged. Delete `android-build.yml` and its cleanup step.

## Verification (moved from plan.md)

- **Slice 40:** a `main` run's release carries MSIX x64 + ARM64 + `.aab` +
  `.apk`, still with zero artifacts. `play-submit` uploads to internal testing
  once the Play secrets exist, and `android-build.yml` is gone.
