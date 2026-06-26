# Microsoft Store packaging notes

This document covers what's needed to package and submit **Read The Stupid Text**
(repo/package id `ReadTheStupidText`) to the Microsoft Store. Slice 5 set up the
build/packaging pipeline. The app is now **reserved in Partner Center** and its
real identity is **wired into `Package.appxmanifest`** (see below); what remains
is the first manual submission and the CI secrets/variable for automated updates.

## Release pipeline status (Slice 16)

Verified end-to-end and **live**:

- ✅ **Versioning → build → release** runs in the single `build.yml` (GitVersion →
  per-arch MSIX → `v<x.y.z>` tag + GitHub Release). It has cut real releases
  (`v0.1.0` … `v0.4.0`), staying in `0.x` as intended — **not** forced to `v1.0.0`.
- ✅ **Tests gate the release** — a `test` job runs the unit suite and blocks
  `build`/`release` on failure (Slice 15b).
- ✅ **Store identity** wired into the manifest (Slice 16 / Decision 23) and
  cross-checked against the reserved Partner Center product (below).
- ✅ **`store-submit.yml`** is present and valid but **intentionally inert**
  (`workflow_dispatch` only; fails fast without the secrets below) — it's a
  deploy button, not an auto-run.

**Manual remainder (needs a human + Partner Center, not code):** the first Store
submission, then the four secrets + one variable to enable automated updates —
see *Deploying to the Store* below.

## App identity (wired into the manifest)

These are the Partner Center **Product identity** values for the reserved app and
must match it **exactly**, or submission fails with a name/identity error
(confirmed via Microsoft Learn). They are already set in `Package.appxmanifest`:

| Manifest field | Value |
| --- | --- |
| `Package/Identity/Name` | `AshkanSirous.ReadTheStupidText` |
| `Package/Identity/Publisher` | `CN=53769961-EF08-4BA5-A1DE-7A51B62A9AA7` |
| `Package/Properties/PublisherDisplayName` | `Ashkan Sirous` |
| `Package/Properties/DisplayName` | `Read The Stupid Text` (must be a reserved app name) |
| `Package/Identity/Version` | placeholder in the repo (pre-1.0); CI **stamps** the real `x.y.z.0` from GitVersion at build time — see *Versioning* below |

Store listing references:

- **Store ID:** `9NGT1BN1H92V`
- **Listing URL:** https://apps.microsoft.com/detail/9NGT1BN1H92V
- **Store protocol link:** `ms-windows-store://pdp/?productid=9NGT1BN1H92V`
- **MSA / Azure AD app id** (for the submission API): `01fff836-f050-475a-8ee4-13cbcfdc7235`

## Build artifact (CI)

`.github/workflows/build.yml` builds the single-project MSIX on `windows-latest`
for **x64** and **ARM64** and uploads each as an **unsigned** `.msix` artifact.

- The Microsoft Store **re-signs** packages on submission, so CI needs no signing
  certificate (`AppxPackageSigningEnabled=false`).
- Single-project MSIX cannot emit a bundle, so each architecture is built and
  uploaded separately (`AppxBundle=Never`). Submit both `.msix` files to the
  Store, or combine them into an `.msixbundle` with the MSIX Bundler action if a
  single upload is preferred.
- The neural voice model is **Git LFS**-tracked, so checkout uses `lfs: true`.
- The package `Version` is **stamped at build time** from GitVersion (see
  *Versioning* below); the committed manifest value is only a placeholder.

Local equivalent:

```powershell
msbuild src/ReadTheStupidText.App/ReadTheStupidText.App.csproj `
  /restore /p:Configuration=Release /p:Platform=x64 `
  /p:GenerateAppxPackageOnBuild=true /p:AppxPackageDir=AppPackages\ `
  /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxBundle=Never `
  /p:AppxPackageSigningEnabled=false
```

## Capabilities and justification

Declared in `Package.appxmanifest`:

| Capability | Type | Justification |
| --- | --- | --- |
| `runFullTrust` | restricted | The app is a full-trust packaged desktop app. It needs Win32 reach that the UWP sandbox forbids: a notification-area (tray) icon, a system-wide global hotkey, reading the **selection/clipboard of other apps** (UI Automation `TextPattern` + simulated copy), and a packaged `StartupTask`. None of these are possible without `runFullTrust`. This is the standard capability for WinUI 3 / Windows App SDK desktop apps and is the justification given in Partner Center. |

No `internetClient` — the neural voice model ships **inside the package**, so the
app makes no network calls.

## Third-party components and licenses

| Component | Use | License |
| --- | --- | --- |
| Windows App SDK / WinUI 3 | UI, windowing | MIT |
| `H.NotifyIcon.WinUI` | tray icon | MIT |
| `org.k2fsa.sherpa.onnx` | neural TTS runtime | Apache-2.0 |
| Supertonic-3 voice model (`VoiceModel/`) | neural voices | Apache-2.0 (see `VoiceModel/LICENSE`) |

No GPL/LGPL components ship in the package (Piper and its espeak-ng phonemizer
were deliberately avoided; Supertonic needs no espeak data). This keeps the
closed-source Store distribution clean.

## Versioning (GitVersion → tag → release)

Versioning is fully automatic and lives **entirely inside `build.yml`** — one
workflow run does version → build → release, with no PAT and no commit-back
(Decision 17 / Slice 14). **Git tags are the source of truth.**

1. The `version` job runs **GitVersion** (`GitVersion.yml`, GitHub Flow preset),
   which reads git history and computes the next SemVer. `main` defaults to a
   **Patch** bump over the last `v*` tag.
2. The `build` job stamps that version (`x.y.z.0` — the Store needs revision `0`)
   into the manifest **at build time** and packages the MSIX. Nothing is
   committed back.
3. On a push to `main`, the `release` job creates the **`v<x.y.z>` tag** at the
   merge commit and a **GitHub Release** with both `.msix` assets — same run, so
   a plain `GITHUB_TOKEN` is enough (no second workflow to trigger).

**Choosing the bump.** Default is patch. To bump higher, add a token to a commit
message since the last tag (highest wins): `+semver: minor` (feature),
`+semver: major` (breaking), `+semver: none` (skip). Agents: write a normal
commit and append `+semver: minor`/`major` when the change warrants it.

> ⚠️ **Footgun — never write the literal token in prose.** GitVersion matches
> `+semver: major`/`minor` **anywhere** in a commit message, including quoted
> examples. A commit that *documents* the tokens (or quotes them in its body)
> will trigger that bump. This actually happened: PR #75's body contained
> `or "+semver: major"`, which forced `0.1.1 → 1.0.0`. When you must mention a
> token in prose, break it (e.g. `+semver:&#8203;major`, or write "the major
> token") so the regex can't match.

> `main`'s branch ruleset only blocks deletion and non-fast-forward, so the tag
> push needs no bypass actor.

## Releases (hosted MSIX)

CI's per-run **workflow artifacts** are only reachable from the Actions run page
(login required, expire after retention) — not a stable download or deploy
source. So distribution uses **GitHub Releases** instead: every push to `main`
cuts one (see *Versioning* above). The packages get **stable URLs** under
`…/releases/latest`, linked from the README, and serve as the hosted source the
Store-submission step pulls from.

## Deploying to the Store

`/.github/workflows/store-submit.yml` is a **manual** (`workflow_dispatch`)
deploy that downloads a release's MSIX and submits it via the **msstore CLI**
(`microsoft/microsoft-store-apppublisher`). It is scaffolded but not yet live —
the Actions-based msstore flow does *updates* to an already-published **free**
app, not the first submission. Remaining steps to turn it on:

1. ~~Reserve the app in Partner Center and wire its **Identity Name + Publisher
   ID** into `Package.appxmanifest`.~~ **Done** — see *App identity* above.
2. Do the **first** submission manually in Partner Center (upload the release
   `.msix` files for x64 + ARM64; the Store signs them) and get it live.
3. Add repo **secrets** `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`,
   `AZURE_AD_APPLICATION_SECRET`, `SELLER_ID`, and a repo **variable**
   `STORE_PRODUCT_ID` = `9NGT1BN1H92V`. (The Azure AD app id above is the
   `AZURE_AD_APPLICATION_CLIENT_ID`; create a client secret for it and find the
   tenant id + seller id in Partner Center → Account settings.)
4. From then on, run **store-submit** (Actions → Run workflow, pick the release
   tag) to push updates.

## Signing

CI produces **unsigned** packages and the Microsoft **Store re-signs** on
publish — the Store is the trusted install channel (SmartScreen trusts Store
apps), so no certificate is needed for the Store path (Decision 18). A domain
(e.g. `sirous.uk`) **cannot** sign code — code-signing certificates validate an
*identity*, not domain control. If trusted **sideloaded** (GitHub-Release) MSIX
is ever wanted, the documented upgrade is **Azure Trusted Signing** (~US$10/mo,
Microsoft-run, GitHub-Actions-native, no hardware token); a traditional OV/EV
cert (cost + hardware token) and self-signed certs (SmartScreen still warns) are
rejected.
