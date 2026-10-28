# Privacy Policy — Read The Stupid Text

**Effective date:** 26 October 2026

Read The Stupid Text ("the app") reads text aloud. It ships as a Windows tray
utility, an Android app and an iPhone app. This policy explains how the app
handles your data on **all** platforms.

## The short version

**Read The Stupid Text does not collect, store, transmit, or share any personal
data.** It has no accounts and no advertising, and what you read, scan or type
never leaves your device: everything the app does with it happens **locally**.
The Windows, Android and iPhone apps send only small **anonymous counts** (no
names, no identifiers, nothing you read) that you can switch off (in the control
panel on Windows, in Settings on the phones); see *Anonymous usage counts* below.

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
- **Translation** (Android and iPhone, off by default): when you turn on "Translate to my
  language", the app identifies the language of your text with Google's
  on-device ML Kit language identification and translates it with ML Kit's
  on-device translation, then reads the result. Both run entirely on your
  phone; your text is never sent to Google or anyone else. The translation is
  casual quality.
  On **Windows** (off by default) translation runs on your PC with offline Opus-MT models from
  downloaded translation packs (Persian is not covered); your text is never sent anywhere.
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

**Language packs, and the optional counts.** The Android, iPhone and Windows apps download the natural-voice
language pack for the language you choose on first launch (about 145 MB, so a
Wi-Fi connection is advisable). The Android and iPhone apps also download the text-recognition
pack for that language (up to about 85 MB, depending on the language). The download is a plain file request to this
project's public GitHub Releases page: no account, no identifier and none of
your text, images or scans is sent. (The apps' separate, optional usage counts,
which you can switch off, are described in *Anonymous usage counts*.)
Once a pack is installed the app works fully offline for reading and makes no network requests for the pack (it does not even re-check the catalog); the only other requests are the usage counts and the small settings file they ask for, and none if you switch them off. Until it arrives, and if
you are offline, your device's own built-in voice reads instead (the Windows voice on
Windows, the phone's voice on Android and iPhone).

**Translation packs (Windows).** If you turn on translation, the app downloads one translation
pack per language direction (about 110 MB each; English is the go-between, so a pair such as
French to Russian needs two) from the same public GitHub Releases page, as a plain file request
that carries none of your text. Once installed, translating works fully offline. If a pack is
missing the app reads the original text.

**Translation models (Android and iPhone).** The first time you translate to or from a
language, Google's ML Kit downloads that language's translation model (about
30 MB each; English is used as a go-between for most pairs) from Google's
servers, using ML Kit's own on-device model download (on Android it may use Google Play
services on Android). This is a request for a model file, not for your text: none of your
text, images or scans is sent. Once a model is on the phone, translating works
offline. If a model cannot be downloaded the app reads the original text.

Your text, images, scans and documents are never transmitted anywhere; reading
and text recognition happen on the device.

## Anonymous usage counts (Windows, Android and iPhone)

The Windows, Android and iPhone apps send small anonymous counts so the project can see which
languages are read, in which countries, whether translation is noticed and used, whether voices are changed,
and how long waits such as the first translation take. This is **on by default**, and you
can turn it off at any time with **Settings > Help improve the app** on the phones, or the
**Help improve the app** switch at the bottom of the control panel on Windows: when it is off,
nothing is queued or sent, and anything waiting is deleted. The phones' first-run language screen
says so once; an app updated from an older version says so once on its first start. On Windows
the switch's own text says what is sent.

A count is a name plus a few values from fixed lists:

- `app_opened`: the platform (Windows, Android or iPhone), the operating system's major version,
  the app version and the language of the app's words.
- `read_started`: the language detected in the text (when the app could tell), the language
  it was read in, whether it was read as written or as a translation, which kind of voice
  read it (the phone's own voice, the Windows voice or one of the app's voice engines), whether you still use
  the default voice, and the text's length as a coarse bucket (under 200, 200 to 1000,
  1000 to 5000, or over 5000 characters). On Windows it also says what started the read: a
  selection, a copy, the hotkey or the control panel (never the name of the app or window).
- `translate_offered` and `translate_chosen`: the language pair, when the app offered
  to translate a text and when you chose to. On Windows, `translate_chosen` is sent when you
  turn translation on, with the language you picked.
- `voice_changed`: the language and the kind of voice, when you pick a voice.
- `timing`: which wait it was (getting a translation model, translating, or the time until
  the first sound), the language pair when known, how it ended (finished, cancelled or failed)
  and how long it took as a coarse bucket (under 0.5 s, 0.5 to 1, 1 to 2, 2 to 5, 5 to 10,
  10 to 20, or over 20 seconds).
- `wait_shown` and `wait_cancelled`: when a "please wait" panel appeared (for example "Preparing
  the voice" or "Still getting the voice ready"), and when you tapped Cancel on it: which wait it
  was (from a fixed list) and how long the panel showed as a coarse bucket, with no text and no
  language.

Every count also carries the app version, the platform and the UTC date (no time of day).
Counts never contain the text you read, file names, scanned images, names, or any install,
device, account or advertising identifier, so they cannot be put together into a history of
one person. Only a share of launches may count at all, as set by the project.

Each batch of counts also says a few coarse things about the device, as fixed lists and ranges: the form
factor (phone, tablet, desktop or other), the operating system's major version, the processor type (arm64, x64,
x86, arm or other), the number of processor cores as a range (1 to 4, 5 to 8, or 9 or more) and the memory as a
range in GB (under 3, 3 to 4, 4 to 6, 6 to 8, or 8 or more). Never the device's model or maker, the exact chip,
exact memory or core numbers, the full system build, or any identifier. The server also records the **country** of
the request as a two-letter code (such as GB), which Cloudflare works out from the connection. It is approximate,
not your GPS position: the app never asks for location permission, never sends a location, and the IP address
itself is not stored. These are coarse ranges, not exact values, and nothing here is a name, an account, a device model or an identifier. They are not a guarantee of anonymity on their own, though: in a small country a rare combination of ranges could stand out, so they are used only to see the overall picture.

Counts are sent in small batches to this project's own server
(`readthestupidtext.sirous.uk`) and stored in Cloudflare's Workers Analytics Engine
(Cloudflare is the processor) for about three months, then deleted. As with any web request,
the server sees your IP address while the request is handled; it is not stored with the
counts and is not used for anything (only the country it maps to, as described above). The project can also switch counting off for everyone
from the server. Counts are not shared with anyone else and are not used for advertising or
tracking. Test and development builds, and the test devices store review tools use, send no counts.

## Permissions

**Windows** declares the `runFullTrust` capability, required so it can place a
system-tray icon, register a global hotkey, read selected/copied text from
other apps (via UI Automation and the Windows clipboard), and run its
on-device text-to-speech engine. It also declares the standard **Internet** (`internetClient`)
capability, used to download language voice packs and to send the anonymous usage counts you can switch off
(see *Network use* and *Anonymous usage counts*). It is not used to send anything else.

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

- **Cloudflare** (Workers and Analytics Engine) — hosts this project's website and
  receives the anonymous usage counts described above on the phone apps; it is a
  processor for the project, and no count holds anything that identifies you.
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

- **Google ML Kit Language Identification and Translation** (Android; on iPhone ML Kit Translation only, with Apple's on-device language recognition) —
  the on-device models used to detect the language of your text and translate
  it. They run locally; nothing you type, paste or scan is sent to Google or
  anyone else. Only the translation model files are downloaded (see *Network
  use*).

## Children's privacy

The app collects no personal information from anyone, including children. The
anonymous counts hold no name, identifier or text, and can be switched off in Settings.

## Changes to this policy

If this policy changes, the updated version will be published at this same
URL with a new effective date.

## Contact

Questions about this policy can be raised by opening an issue at
<https://github.com/ashkansirous/ReadTheStupidText/issues>.
