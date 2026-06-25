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

## Remaining Partner Center steps (account-dependent — not in this slice)

1. Reserve the app name in Partner Center and note the assigned **Package/Identity
   Name** and **Publisher ID**.
2. Update `Package.appxmanifest` `<Identity Name=... Publisher=...>` and
   `<PublisherDisplayName>` to match the reservation (currently a placeholder
   GUID + `CN=Ashkan Sirous`).
3. Submit the CI `.msix` artifacts (x64 + ARM64) to the Store; it signs them.
4. Optionally automate submission with the Microsoft Store CLI GitHub Action
   (needs `AZURE_AD_*` + `SELLER_ID` secrets).
