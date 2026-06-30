# Microsoft Store packaging notes

This document covers what's needed to package and submit **Read The Stupid Text**
(repo/package id `ReadTheStupidText`) to the Microsoft Store. Slice 5 set up the
build/packaging pipeline. The app is **published and live in the Store**
(product `9NGT1BN1H92V`) and its real identity is **wired into
`Package.appxmanifest`** (see below). What remains to fully automate updates is
the four Partner Center secrets (the `STORE_PRODUCT_ID` variable is already set).

## Release pipeline status (Slice 16)

Verified end-to-end and **live**:

- ✅ **Versioning → build → release** runs in the single `build.yml` (GitVersion →
  per-arch MSIX → `v<x.y.z>` tag + GitHub Release). It has cut real releases
  (`v0.1.0` … `v0.4.0`), staying in `0.x` as intended — **not** forced to `v1.0.0`.
- ✅ **Tests gate the release** — a `test` job runs the unit suite and blocks
  `build`/`release` on failure (Slice 15b).
- ✅ **Store identity** wired into the manifest (Slice 16 / Decision 23) and
  cross-checked against the reserved Partner Center product (below).
- ✅ **First submission done — app is live** at
  https://apps.microsoft.com/detail/9NGT1BN1H92V.
- ✅ **`store-submit.yml`** is `workflow_dispatch`-only and submits **one** update
  carrying both architectures (x64 + ARM64 combined into a single `.msixbundle`).
  It fails fast until the four Partner Center secrets are set.
- ✅ **`STORE_PRODUCT_ID` variable** set to `9NGT1BN1H92V` in repo Actions
  variables.

**Manual remainder (needs a human + Partner Center, not code):** add the four
secrets below to enable the automated-update button — see *Deploying to the
Store*.

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
  uploaded separately (`AppxBundle=Never`). A Store submission must carry both
  architectures, so `store-submit.yml` combines the two release `.msix` assets
  into one `.msixbundle` (`makeappx bundle`) and submits that single bundle (see
  *Deploying to the Store*).
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

## Runtime deployment (.NET self-contained)

The shipped (**Release**) MSIX is built **.NET self-contained**
(`<SelfContained>true</SelfContained>`, scoped to non-Debug in
`ReadTheStupidText.App.csproj`), so the **.NET 10 runtime is bundled inside the
package**. The **Windows App SDK** runtime stays framework-dependent
(`Microsoft.WindowsAppRuntime.2`, auto-installed by the Store) —
`WindowsAppSDKSelfContained` is deliberately **not** set.

Rationale: a framework-dependent Store MSIX gets only the Windows App SDK runtime
delivered by the Store; the **.NET runtime is not**, and it is not present on a
clean Windows 11. The first submission failed Store certification **10.2.4.1
(Security — Software Dependencies: undisclosed dependency on non-integrated
software: .NET)**. Bundling .NET removes the external dependency entirely — no
description disclosure is required and users install nothing. The cost is package
size (~+50 MB for the runtime; the package is dominated by the ~145 MB voice
model regardless). The Debug inner loop (`dotnet run` / VS **(Package)** profile)
stays framework-dependent so it remains fast.

## Privacy & diagnostics

The app **collects and transmits nothing**. The read-latency timing diagnostics
added in Slice 19 (time-to-first-audio and synthesis duration per read) live only in
the in-memory activity log — shown in the activity-log window, capped, and cleared on
every restart. There is no third-party analytics SDK, no network call, and no
on-disk telemetry, so the Store privacy questionnaire stays "no data collected".

For **dev-time** performance tuning only, the read pipeline may optionally be
instrumented with **OpenTelemetry** (`Activity`/`Meter`) and observed via a **local
Aspire dashboard** (an OTLP viewer on the developer's own machine). This is **not
part of the shipped MSIX** and exports nothing off the device. Full .NET Aspire was
evaluated and rejected as a shipped mechanism — it orchestrates *distributed* apps at
dev time and is not a redistributable runtime (Decision 26 in `plan.md`).

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
deploy that downloads a release's MSIX assets, **combines x64 + ARM64 into one
`.msixbundle`** (`makeappx bundle`), and submits that single bundle via the
**msstore CLI** (`microsoft/microsoft-store-apppublisher`). The Actions-based
msstore flow does *updates* to an already-published **free** app — which this app
now is. One submission must carry both architectures, so the workflow bundles
rather than calling `msstore publish` once per `.msix` (which would open
competing submissions).

Setup status:

1. ~~Reserve the app in Partner Center and wire its **Identity Name + Publisher
   ID** into `Package.appxmanifest`.~~ **Done** — see *App identity* above.
2. ~~Do the **first** submission manually in Partner Center and get it live.~~
   **Done** — the app is live at https://apps.microsoft.com/detail/9NGT1BN1H92V.
3. ~~Add repo **variable** `STORE_PRODUCT_ID` = `9NGT1BN1H92V`.~~ **Done.**
4. **Remaining:** add four repo **secrets** so the credentials step can
   authenticate. Settings → Secrets and variables → Actions → *New repository
   secret*, or `gh secret set <NAME>`:
   - `AZURE_AD_TENANT_ID` — Entra tenant id (Partner Center → Account settings;
     or entra.microsoft.com → Overview).
   - `AZURE_AD_APPLICATION_CLIENT_ID` — `01fff836-f050-475a-8ee4-13cbcfdc7235`
     (the Entra app registration's Application id).
   - `AZURE_AD_APPLICATION_SECRET` — a **client secret** created for that app
     registration (Entra → App registrations → your app → Certificates &
     secrets; copy the value immediately, it's shown once).
   - `SELLER_ID` — your Partner Center publisher/seller id (Account settings →
     Identifiers).
5. From then on, run **store-submit** (Actions → Run workflow, pick the release
   tag) to push an update.

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
