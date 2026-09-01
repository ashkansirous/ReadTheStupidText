# Microsoft Store packaging notes

This document covers what's needed to package and submit **Read The Stupid Text**
(repo/package id `ReadTheStupidText`) to the Microsoft Store. Slice 5 set up the
build/packaging pipeline. The app is **published and live in the Store**
(product `9NGT1BN1H92V`) and its real identity is **wired into
`Package.appxmanifest`** (see below). What remains to fully automate updates is
the four Partner Center secrets (the `STORE_PRODUCT_ID` variable is already set).

> **Google Play (Android/MAUI, Batch 6, Slice 34).** The Android app has its own
> parallel pipeline, `.github/workflows/android-build.yml`, mirroring this
> document's trust model (CI-held upload key; Google Play App Signing re-signs
> for distribution — same shape as this file's [Signing](#signing) section).
> App id `uk.sirous.readthestupidtext`. The signed `.aab` build already works
> (`ANDROID_SIGNING_*` secrets are set); the Play Store **upload** step is a
> documented no-op until `PLAY_SERVICE_ACCOUNT_JSON` is created in Play
> Console (Setup → API access → service account) — a credential only the
> account owner can create, the same category as this file's Partner Center
> secrets below. See `plan.md`'s Slice 34 note for the full rationale.

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
- ✅ **Credentials wired and proven.** The tenant was created from Partner Center
  (individual account — see below), an Entra app registered and given the
  **Manager** role, and all four secrets set. `msstore reconfigure` authenticates.
- ✅ **Automated update proven end-to-end** on **v0.7.7** (run `31974833565`):
  download both release `.msix` → `makeappx bundle /bv 0.7.7.0` → authenticate →
  `msstore publish` → *Submission commit success*, status **Certification**. The
  ~492 MB upload took ~3 s. So the whole chain — GitVersion → per-arch MSIX →
  GitHub Release → bundle → submit — now runs from one dispatch.

Deploying an update is therefore just:

```bash
gh workflow run store-submit -f tag=v<x.y.z>
```

**One-time Partner Center setup — done, kept because it is the non-obvious part.**
This is an **individual** Partner Center account, which starts with no Microsoft
Entra tenant. A tenant was created (free, from Partner Center), an Entra app
registered and given the **Manager** role, and the four secrets set. Full
walkthrough in *Deploying to the Store → An individual Partner Center account has
no tenant* — needed again only if the account or app registration is rebuilt.

⏰ **The client secret expires** (24 months max, and it was created 2026-08-16).
When it lapses, `store-submit` starts failing at *Configure Store credentials*
with an auth error. The fix is a new secret in the same app registration and a
`gh secret set AZURE_AD_APPLICATION_SECRET` — nothing else changes.

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
- **MSA / Azure AD app id** shown on the product's identity page:
  `01fff836-f050-475a-8ee4-13cbcfdc7235`. This is the product's own identity value
  — **not** the submission-API client id. `AZURE_AD_APPLICATION_CLIENT_ID` is the
  Application (client) id of an Entra **app registration** you create yourself (see
  *Deploying to the Store*).

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
| `PdfPig` | PDF text extraction (file upload) | Apache-2.0 |
| `DocumentFormat.OpenXml` | DOCX text extraction (file upload) | MIT |

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

One detail the workflow gets right and that is easy to get wrong by hand:
`makeappx bundle` is given **`/bv`** (the packages' own `x.y.z.0`, parsed off the
release asset names) — omit it and makeappx stamps the bundle version from the
*current date-time*, which matches neither the release nor a predictable ordering.

⚠️ **Check option flags against the CLI version the action actually installs**
(`latest` = **v0.3.9**), not against the docs or the CLI's `main` branch — they
have drifted. `msstore publish --uploadTimeout/-ut` is documented and present on
`main` but does **not** exist in v0.3.9, where `publish` accepts only
`-i/-id/-nc/-f/-prp/-v`; passing it fails the run with *"Unrecognized command or
argument '-ut'"*. It also isn't needed: v0.3.9 uploads via the Azure Storage SDK
(`BlobClient.UploadAsync`), which chunks and retries internally, so the ~500 MB
bundle is not racing one fixed HTTP timeout.

### The CLI supersedes old packages by **file extension**

`msstore publish` clones the last published submission and then decides what the
new package replaces with a single line
(`IStorePackagedAPIExtensions.cs`, v0.3.9):

```csharp
var applicationPackage = packages?.FirstOrDefault(p => Path.GetExtension(p.FileName) == file.Extension);
if (applicationPackage != null) { /* ... */ applicationPackage.FileStatus = FileStatus.PendingDelete; }
```

The match is on **extension only**. That matters because this repo switched the
submission from per-arch `.msix` to a single `.msixbundle`: on the **first**
bundle submission (v0.7.7) nothing matched `.msixbundle`, so the previously
published `ReadTheStupidText.App_0.5.0.0_{x64,arm64}.msix` packages were **not**
superseded — the bundle was simply appended next to them, and they would have
been inherited by every later submission forever. They were deleted by hand in
Partner Center, once.

From bundle-to-bundle this is **self-correcting**: the previous submission now
contains a `.msixbundle`, the extensions match, and the old bundle is marked
`PendingDelete` automatically. So this is a transition artifact, not a standing
bug — but if the packaging format ever changes again (e.g. to `.msixupload`),
expect the same stale-package leftover and clear it manually.

⚠️ Leftovers are not cosmetic: the stale 0.5.0.0 packages were the
**framework-dependent** builds that failed cert **10.2.4.1**, so leaving them in a
submission re-exposes the reviewer to the exact package that failed before.

### Reading the submission result

A green workflow means **submitted**, not **live** — certification runs on
Microsoft's side afterwards. The publish step's own output is the thing to read:
it ends with `Submission commit success!` and lists the packages the submission
actually contains, which is how the stale-package problem above was spotted.
Benign noise to expect: `PackageValidationWarning` about `windows.startupTask`
"not supported on Xbox" — this app is Desktop-only.

Setup status:

1. ~~Reserve the app in Partner Center and wire its **Identity Name + Publisher
   ID** into `Package.appxmanifest`.~~ **Done** — see *App identity* above.
2. ~~Do the **first** submission manually in Partner Center and get it live.~~
   **Done** — the app is live at https://apps.microsoft.com/detail/9NGT1BN1H92V.
3. ~~Add repo **variable** `STORE_PRODUCT_ID` = `9NGT1BN1H92V`.~~ **Done.**
4. **Remaining:** create a tenant + app registration (below), then add four repo
   **secrets** so the credentials step can authenticate. Settings → Secrets and
   variables → Actions → *New repository secret*, or `gh secret set <NAME>`:
   - `AZURE_AD_TENANT_ID` — Entra tenant id (entra.microsoft.com → Overview).
   - `AZURE_AD_APPLICATION_CLIENT_ID` — the Entra app registration's
     Application (client) id.
   - `AZURE_AD_APPLICATION_SECRET` — a **client secret** created for that app
     registration (Entra → App registrations → your app → Certificates &
     secrets; copy the value immediately, it's shown once).
   - `SELLER_ID` — the **numeric Seller ID** (Account settings → Identifiers →
     "Seller ID"). ⚠️ **Digits only.** The `msstore` CLI runs `Convert.ToInt32`
     on this value, so the GUID-shaped **Publisher ID** on the same page — and
     anything with `CN=`, braces or quotes — fails with an opaque
     `FormatException: The input string was not in a correct format` that names
     no credential. The workflow now pre-checks the shape and says so plainly.
5. From then on, run **store-submit** (Actions → Run workflow, pick the release
   tag) to push an update.

### An **individual** Partner Center account has no tenant — create one

This account is an **individual** (personal-MSA) Windows developer account, so it
starts with **no Microsoft Entra tenant attached** — and `msstore reconfigure`
cannot authenticate without a `tenantId`. This is not a blocker and does **not**
require converting to a company account: an individual account can attach a tenant
after the fact, and Partner Center will **create a brand-new Entra tenant for free**
if you don't already have one.

Do this once, in the browser (nothing here can be scripted from CI):

1. **Create the tenant.** [Partner Center](https://partner.microsoft.com/dashboard)
   → gear icon → **Account settings** → **Tenants** → **Create Microsoft Entra ID**.
   Fill in the directory name, initial domain (`<something>.onmicrosoft.com`) and
   the global-admin user it creates. *(If you already have an Entra tenant — e.g.
   from a Microsoft 365 subscription — use **Associate Microsoft Entra ID with your
   Partner Center account** instead and sign in with that tenant's credentials.)*
2. **Register an app** in that tenant: [entra.microsoft.com](https://entra.microsoft.com)
   → Identity → Applications → **App registrations** → *New registration*
   (single-tenant, no redirect URI needed). Copy its **Application (client) ID**.
3. **Add a client secret**: same app → **Certificates & secrets** → *New client
   secret*. Copy the **value** immediately — it is shown once. Note the expiry
   (max 24 months); the workflow starts failing auth the day it lapses.
4. **Give the app access to submissions.** Back in Partner Center → **Account
   settings** → **User management** → **Microsoft Entra applications** → add the
   app registration and assign it the **Manager** role. Skipping this is the usual
   cause of a 403 from `msstore` even with valid credentials.
5. **Collect the four values** and set them as the repo secrets in step 4 above:
   tenant id (Entra → Overview), client id (step 2), client secret (step 3),
   seller id (Partner Center → Account settings → Identifiers).

Constraints worth knowing before the first dispatch: the Actions/msstore update
path supports **free products only** (this app is free), and the product must
already be **published and live** (it is) — the API cannot create an app or make a
*first* submission.

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
