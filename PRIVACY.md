# Privacy Policy — Read The Stupid Text

**Effective date:** 3 September 2026

Read The Stupid Text ("the app") reads text aloud. It ships as a Windows tray
utility, an Android app and an iPhone app. This policy explains how the app
handles your data on **all** platforms.

## The short version

**Read The Stupid Text does not collect, store, transmit, or share any personal
data.** It has no accounts, no analytics, no advertising, and no telemetry
that leaves your device. Everything the app does happens **locally**.

## What the app processes, and where

- **Text you select, copy, type, paste, or send via the hotkey** is read aloud
  and held only **in memory** for as long as it takes to speak it. It is not
  permanently logged or sent anywhere.
- **Uploaded files** (`.txt`, `.pdf`, `.docx`) are opened and parsed locally to
  extract their text. The file itself is never transmitted or copied anywhere
  by the app.
- **Camera scans** (Android and iPhone): a photo you take is read by on-device
  text recognition, which runs entirely on your phone: a downloaded language
  recognition pack, or Google's ML Kit on Android and Apple's Vision framework
  on iPhone while that pack is not installed. The photo is processed to extract
  text and is not uploaded, saved permanently, or shared — it exists only to be
  read aloud.
- **Translation** (Android only for now, off by default): when you turn on "Translate to my
  language", the app identifies the language of your text with Google's
  on-device ML Kit language identification and translates it with ML Kit's
  on-device translation, then reads the result. Both run entirely on your
  phone; your text is never sent to Google or anyone else. The translation is
  casual quality.
- **Speech synthesis is performed entirely on your device**, using a neural
  voice model that the app downloads once as a language pack. No text or audio is
  sent to any server or third party, on any platform.
- The app's **activity log** (an optional diagnostic view) lives in memory,
  shows recent read activity, and is cleared when the app closes.
- **Cached audio of read sentences (Android, iPhone and Windows):** to start reading sooner, the app keeps the
  audio it generated for sentences it has read on your device only, in the app's private
  storage, capped at 100 MB. It is removed when you clear the app's cache or storage (on
  Windows, when you uninstall the app), and it is never sent anywhere.
- **Settings** you choose (reading speed, selected voice, and platform toggles
  such as auto-read and launch-at-startup) are stored **locally** on your
  device. They never leave it.
- **Windows only — on-disk diagnostic logs:** the Windows app additionally
  writes two small local log files per day (system events and a per-read
  history) to help diagnose problems, kept for 7 days and then deleted
  automatically. Anything resembling a URL, email address, password, card
  number, or file path is automatically replaced with a short description
  (e.g. "an email address") before it's written. These files stay on your
  machine and are never transmitted anywhere.

## Network use

**Language packs only.** The Android, iPhone and Windows apps download the natural-voice
language pack for the language you choose on first launch (about 145 MB, so a
Wi-Fi connection is advisable). The Android and iPhone apps also download the text-recognition
pack for that language (up to about 85 MB, depending on the language). The download is a plain file request to this
project's public GitHub Releases page: no account, no identifier and none of
your text, images or scans is sent, and the app has no analytics or telemetry.
Once a pack is installed the app works fully offline and makes no network requests at all (it does not even re-check the catalog). Until it arrives, and if
you are offline, your device's own built-in voice reads instead (the Windows voice on
Windows, the phone's voice on Android and iPhone).

**Translation models (Android).** The first time you translate to or from a
language, Google's ML Kit downloads that language's translation model (about
30 MB each; English is used as a go-between for most pairs) from Google's
servers, using ML Kit's own on-device model download (it may use Google Play
services). This is a request for a model file, not for your text: none of your
text, images or scans is sent. Once a model is on the phone, translating works
offline. If a model cannot be downloaded the app reads the original text.

Your text, images, scans and documents are never transmitted anywhere; reading
and text recognition happen on the device.

## Permissions

**Windows** declares the `runFullTrust` capability, required so it can place a
system-tray icon, register a global hotkey, read selected/copied text from
other apps (via UI Automation and the Windows clipboard), and run its
on-device text-to-speech engine. It also declares the standard **Internet** (`internetClient`)
capability, used only to download language voice packs (see *Network use*). It is not used to
collect or transmit data.

**Android** declares the **Internet** and **Network state** permissions, used
only to download language voice packs and to tell "offline" from a failed
download (see *Network use*). It also requests the **Camera** permission, used only when you tap the
scan action, so the app can point your camera at text to read it — nothing is
captured otherwise.

**iPhone** needs no network permission beyond the standard HTTPS access every app has
(used for the same pack downloads). It asks for **Camera** access when you tap the scan
action, and for **Photo library** access only when you choose a photo to read (the app never
writes to your library). Downloaded language packs are marked as excluded from iCloud and
device backups.

All platforms use the standard OS file picker to let you choose a document to read;
the app only opens the file you explicitly select.

## Third-party components

The app uses a small set of open-source, on-device components that never
transmit data (the Supertonic voice model is downloaded as a
language pack on every platform rather than bundled):

- **sherpa-onnx** and the **Supertonic** voice model — the offline neural
  text-to-speech engine, used on every platform.
- **Piper** (Persian voice) with **espeak-ng** data, and **MeloTTS** (Chinese
  voice) — more offline voices, each downloaded once as a language pack when you
  choose that language. They run entirely on your device; the only network use is
  the one-time download of the pack, and your text is never sent anywhere.
- **Google ML Kit Text Recognition** (Android only) and **Apple Vision** (iPhone) —
  the on-device models used to read text out of a scanned photo while a language's
  recognition pack is not installed. They run locally; nothing is sent to Google,
  Apple or anyone else.
- **PaddleOCR** text-recognition models and **ONNX Runtime** (Android and iPhone) —
  downloaded once per language as a recognition pack, run locally, and nothing you
  scan is sent anywhere.

- **Google ML Kit Language Identification and Translation** (Android only) —
  the on-device models used to detect the language of your text and translate
  it. They run locally; nothing you type, paste or scan is sent to Google or
  anyone else. Only the translation model files are downloaded (see *Network
  use*).

## Children's privacy

The app collects no personal information from anyone, including children.

## Changes to this policy

If this policy changes, the updated version will be published at this same
URL with a new effective date.

## Contact

Questions about this policy can be raised by opening an issue at
<https://github.com/ashkansirous/ReadTheStupidText/issues>.
