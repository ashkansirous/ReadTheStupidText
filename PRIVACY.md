# Privacy Policy — Read The Stupid Text

**Effective date:** 25 June 2026

Read The Stupid Text ("the app") is a Windows utility that reads selected or
copied text aloud. This policy explains how the app handles your data.

## The short version

**Read The Stupid Text does not collect, store, transmit, or share any personal
data.** It has no accounts, no analytics, no telemetry, and makes no network
connections. Everything the app does happens **locally on your device**.

## What the app processes, and where

- **Text you select, copy, or send via the hotkey** is read aloud and held only
  **in memory** for as long as it takes to speak it. It is **not** written to
  disk, logged permanently, or sent anywhere.
- **Speech synthesis is performed entirely on your device** using a voice model
  bundled inside the app. No text or audio is sent to any server or third party.
- The app's **activity log** (an optional diagnostic view) lives only in memory,
  shows recent read activity, and is **cleared when the app closes**. It is never
  saved to disk or transmitted.
- **Settings** you choose (reading speed, selected voice, auto-read and
  startup toggles) are stored **locally** on your device in the app's local
  settings. They never leave your device.

## Network use

**None.** The app works fully offline. It does not connect to the internet,
download anything, or call any web service or API.

## Permissions

The app declares the `runFullTrust` capability. This is required so it can place
a system-tray icon, register a global hotkey, read selected/copied text from
other apps (via UI Automation and the Windows clipboard), and run its on-device
text-to-speech engine. This capability is **not** used to collect or transmit
any data.

## Third-party components

The app bundles open-source components (the sherpa-onnx runtime and the
Supertonic voice model) that run **locally** and do not transmit data.

## Children's privacy

The app collects no personal information from anyone, including children.

## Changes to this policy

If this policy changes, the updated version will be published at this same URL
with a new effective date.

## Contact

Questions about this policy can be raised by opening an issue at
<https://github.com/ashkansirous/ReadTheStupidText/issues>.
