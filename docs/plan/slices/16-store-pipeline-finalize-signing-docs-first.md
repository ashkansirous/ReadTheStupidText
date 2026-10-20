# Slice 16 — Store-pipeline finalize + signing docs, first release

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 16 — Store-pipeline finalize + signing docs, first release.**
      ([#64](https://github.com/ashkansirous/ReadTheStupidText/issues/64)) (Decisions 18, and Slice 5's deferred Partner Center work) Verify
      `store-submit.yml` is correct (kept **inert** — no account), refresh
      `STORE.md` with the remaining Partner Center steps and the **Azure Trusted
      Signing** upgrade path, and confirm the Conventional-Commits versioning
      feeds the release/Store flow. Cut the **first auto-versioned release** —
      the tag is whatever the versioning produces (**stays `0.x`**; **not**
      `v1.0.0` until the user declares the app stable).

- [x] **Store launch — live + automated-update wiring.** App **published and
      live** at https://apps.microsoft.com/detail/9NGT1BN1H92V (product
      `9NGT1BN1H92V`). `store-submit.yml` made functional (downloads a release,
      combines x64 + ARM64 into one `.msixbundle`, submits via the msstore CLI);
      `STORE_PRODUCT_ID` repo variable set. First submission failed Store
      cert **10.2.4.1** (undisclosed dependency on .NET) — fixed by building the
      Release MSIX **.NET self-contained** (`SelfContained=true`), bundling the
      .NET runtime so there's no external dependency. Windows App SDK stays
      framework-dependent (Store-delivered).
      **Automation now complete and proven** (v0.7.7, run `31974833565`): the
      individual Partner Center account had no Entra tenant, so one was created
      free from Partner Center, an Entra app registered with the **Manager** role,
      and all four secrets set. Three real failures were found and fixed on the way
      — `makeappx bundle` needs **`/bv`** (else the bundle version comes from the
      current date-time); `msstore publish -ut` **does not exist** in the CLI
      version the action installs (v0.3.9) despite being documented; and
      **`SELLER_ID` must be the numeric Seller ID** from *Organization profile →
      Legal info*, not the GUID Publisher ID on the Identifiers page. Updates now
      ship with `gh workflow run store-submit -f tag=v<x.y.z>`. One transition
      artifact, handled manually once: the CLI supersedes packages by **file
      extension**, so the first `.msixbundle` did not replace the old `.msix`
      packages — see `STORE.md`.

## Verification (moved from plan.md)

- **Slice 16:** `STORE.md` lists the remaining Partner Center steps and the
  Azure Trusted Signing upgrade; `store-submit.yml` is valid but inert (no
  secrets). Pushing the **first auto-versioned tag** (a `0.x` version — not
  `v1.0.0`) produces the first Store-ready GitHub Release with both arch MSIX
  packages.
