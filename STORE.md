# Microsoft Store packaging notes

This document covers what's needed to package and submit **ReadTheStupidText** to
the Microsoft Store. Slice 5 set up the build/packaging pipeline; the account-
dependent steps (identity, signing, submission) are listed at the end and are
done in Partner Center when an account is available.

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

## Releases (hosted MSIX)

CI's per-run **workflow artifacts** are only reachable from the Actions run page
(login required, expire after retention) — not a stable download or deploy
source. So distribution uses **GitHub Releases** instead:

- Push a version tag (`git tag v1.0.0 && git push origin v1.0.0`). The `build`
  workflow then runs the `release` job, which attaches both `.msix` files to a
  GitHub Release for that tag.
- The packages get **stable URLs** under `…/releases/latest`, linked from the
  README, and serve as the hosted source the Store-submission step pulls from.

## Deploying to the Store

`/.github/workflows/store-submit.yml` is a **manual** (`workflow_dispatch`)
deploy that downloads a release's MSIX and submits it via the **msstore CLI**
(`microsoft/microsoft-store-apppublisher`). It is scaffolded but not yet live —
the Actions-based msstore flow does *updates* to an already-published **free**
app, not the first submission. To turn it on:

1. Reserve the app in Partner Center; wire its **Identity Name + Publisher ID**
   into `Package.appxmanifest` (currently a placeholder GUID + `CN=Ashkan Sirous`).
2. Do the **first** submission manually in Partner Center (upload the release
   `.msix` files; the Store signs them) and get it live.
3. Add repo **secrets** `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`,
   `AZURE_AD_APPLICATION_SECRET`, `SELLER_ID`, and a repo **variable**
   `STORE_PRODUCT_ID`.
4. From then on, run **store-submit** (Actions → Run workflow, pick the release
   tag) to push updates.
