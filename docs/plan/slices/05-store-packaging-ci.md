# Slice 5 — Store packaging & CI

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 5 — Store packaging & CI.** ([#8](https://github.com/ashkansirous/ReadTheStupidText/issues/8)) GitHub Actions workflow
      (`.github/workflows/build.yml`) builds + packages the single-project MSIX
      on `windows-latest` for **x64 and ARM64** and uploads each as an **unsigned**
      `.msix` artifact (the Store re-signs on submission, so CI needs no
      certificate). Checkout uses `lfs: true` for the LFS-tracked voice model;
      packaging uses `GenerateAppxPackageOnBuild=true` + `AppxBundle=Never`
      (single-project MSIX can't bundle, so per-arch packages) +
      `UapAppxPackageBuildMode=SideloadOnly` + `AppxPackageSigningEnabled=false`.
      Fixed the **`NETSDK1102`** Release error (`PublishTrimmed` requires
      self-contained — trimming disabled; packaged WinUI apps aren't trimmed) so
      Release now builds (verified locally, x64, 0/0). `STORE.md` documents the
      `runFullTrust` restricted-capability justification, the third-party licenses
      (all MIT/Apache-2.0, no GPL), and the build commands. **CI-only scope (per
      decision):** real Store identity (reserved Name + Publisher ID), signing
      secrets, and the `.msixupload` submission flow are deferred to Partner
      Center and documented as the remaining steps in `STORE.md` — no account was
      wired in. MSIX-packaging msbuild args confirmed via Microsoft Learn first.
      **CI verified green** on PR #41 (run 28160543687) — both arches build and
      upload; needed one fix: pass `RuntimeIdentifier` explicitly because
      `setup-msbuild`'s 32-bit MSBuild made the csproj infer `win-x86` →
      `NETSDK1032`. **Distribution:** on a `v*` tag the `build` workflow's
      `release` job publishes both `.msix` files to a **GitHub Release** (stable
      URLs, linked from the README's Download section) — workflow artifacts alone
      aren't a hosted/linkable source. `store-submit.yml` is a manual
      (`workflow_dispatch`) deploy that submits a release's MSIX via the **msstore
      CLI** (`microsoft/microsoft-store-apppublisher`); scaffolded but inert until
      a Partner Center account + secrets (`AZURE_AD_*`, `SELLER_ID`,
      `STORE_PRODUCT_ID`) and a first manual Store submission exist (the
      Actions msstore flow only *updates* a live free app). msstore CLI workflow
      confirmed via Microsoft Learn first; see `STORE.md`.

## Verification (moved from plan.md)

- **Slice 5:** CI run produces a signed MSIX; install the MSIX on a clean
  Win11 machine and confirm all of the above; Store certification dry-run
  passes capability checks.
