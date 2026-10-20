# Slice 14 — MIT license + Conventional-Commits auto-release

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 14 — MIT license + Conventional-Commits auto-release.**
      ([#57](https://github.com/ashkansirous/ReadTheStupidText/issues/57)) (Decisions 16, 17) Add a `LICENSE` file (MIT, attributed to Ashkan Sirous)
      and replace README's "License: TBD". Add a CI job/workflow that, on merge
      to `main`, computes the next SemVer from Conventional Commits, writes it
      into `Package.appxmanifest` `Version` (`x.y.z.0`), commits the bump, and
      pushes a `v<x.y.z>` tag — which the existing `build.yml` release job turns
      into a GitHub Release. Document the commit convention in `CLAUDE.md`.

## Verification (moved from plan.md)

- **Slice 14:** `LICENSE` (MIT) exists and the README License section reflects
  it. Merge a `feat:` PR → CI bumps the **minor** version in
  `Package.appxmanifest`, tags `v<x.y.z>`, and a GitHub Release appears with the
  MSIX assets; a `fix:`/unconventional PR bumps **patch**; a `!`/`BREAKING
  CHANGE` PR bumps **major**. The manifest version, tag, and release agree.
