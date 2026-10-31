# Microsoft Store packaging notes

This document covers what's needed to package and submit **Read The Stupid Text**
(repo/package id `ReadTheStupidText`) to the Microsoft Store. Slice 5 set up the
build/packaging pipeline. The app is **published and live in the Store**
(product `9NGT1BN1H92V`) and its real identity is **wired into
`Package.appxmanifest`** (see below). What remains to fully automate updates is
the four Partner Center secrets (the `STORE_PRODUCT_ID` variable is already set).

> **Google Play (Android/MAUI, Batch 6, Slice 34).** The Android app builds in
> the same release pipeline (`release.yml` orchestrates; `android.yml` builds the
> AAB/APK and `release.yml`'s `play-submit` job ships it, Batch 8), mirroring this
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

- ✅ **Versioning → build → release** runs in `release.yml` (GitVersion →
  `windows.yml` per-arch MSIX → `rtst-v<x.y.z>` tag + GitHub Release). It cut `v0.1.0` … `v0.4.0` as the
  one shared version; since Slice 130 RTST and Dino Reader each have their own tag and release.
- ✅ **Tests gate the release** — a `test` job runs the unit suite and blocks
  `build`/`release` on failure (Slice 15b).
- ✅ **Store identity** wired into the manifest (Slice 16 / Decision 23) and
  cross-checked against the reserved Partner Center product (below).
- ✅ **First submission done — app is live** at
  https://apps.microsoft.com/detail/9NGT1BN1H92V.
- ✅ **`store-submit.yml`** submits **one** update carrying both architectures
  (x64 + ARM64 combined into a single `.msixbundle`). It fails fast until the
  four Partner Center secrets are set. Since Decision 44 it runs
  **automatically** — called from `release.yml`'s `store-submit` job right after
  every new release — with `workflow_dispatch` kept only for a manual re-run.
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

Deploying an update now happens automatically on every push to `main` that
changes Windows-specific code (App, Infrastructure, store-submit.yml, windows.yml; core-only, Android-only or docs-only
merges don't resubmit to the Store), with no manual step. To force a manual re-submit (e.g. retrying a transient upload
failure, or resubmitting an older tag), it's still just:

```bash
gh workflow run store-submit -f tag=rtst-v<x.y.z>
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

`.github/workflows/windows.yml` (called by `release.yml`) builds the single-project MSIX on `windows-latest`
for **x64** and **ARM64**. On `main` it uploads each **unsigned** `.msix` directly
to that merge's draft GitHub Release. PRs build without uploading anything, and
there are no Actions artifacts at all (Batch 8, Decision 52: the account's
500 MB Actions storage quota can't hold even one build).

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
| `internetClient` | standard | Added in Slice 56 (Decision 60). The package no longer carries the ~145 MB voice model: after the first-run language choice the app downloads the matching **voice pack** (an anonymous HTTPS file download from this project's public GitHub Releases, `packs-<n>`, see `docs/voice-packs.md`) and, apart from the optional usage counts (Slice 140: a small config GET and batches of anonymous counts, off with the control-panel switch), checks nothing else on the network. No text, audio, document or identifier is ever sent; once a pack is installed the app makes no network request for it. Partner Center justification: "Downloads the text-to-speech voice for the language the user chooses, on first run, and sends optional anonymous usage counts." |

`internetClient` is the only network capability declared (no `privateNetworkClientServer`,
no `internetClientServer`). Where no network is available the Windows voice keeps reading.

## Store listing (Slice 43, Decision 71)

The listing text is drafted in `docs/store-listings/<lang>.md` for the first nine app
languages (`en`, `fa`, `fr`, `es`, `pt`, `zh`, `ar`, `ru`, `ja`); the five added by Slice 103 are further down. Each file has
the same three blocks: 7 search terms, a short description and a long
description. **The title does not change** (Decision 71): it stays "Read The
Stupid Text". Everything except `en.md` is **machine-drafted and needs a native
read before submission**; `fa` and `ar` are plain UTF-8 text (RTL is applied by
the store, not by markup).

**Search terms (English, Decision 71)** — exactly 7, each <= 30 chars:

| # | Term | Chars |
| --- | --- | --- |
| 1 | read aloud | 10 |
| 2 | text to speech | 14 |
| 3 | tts | 3 |
| 4 | read text aloud | 15 |
| 5 | speak selected text | 19 |
| 6 | pdf reader aloud | 16 |
| 7 | natural voices offline | 22 |

**Short description** (Google Play field, <= 80 chars; on the Microsoft Store
use it as the first line of the description):

> Read any text aloud with natural voices that work offline, at 0.5x to 2x speed.

**Long description** (Microsoft Store <= 10,000 chars, Google Play <= 4,000):
see `docs/store-listings/en.md`. It only claims what ships today: selected or
copied text and the `Ctrl+Win+R` hotkey (Windows); pasted text, `.txt`/`.pdf`/
`.docx` files and camera-scanned pages (Android); on-device natural voices,
0.5x-2x speed, nothing leaves the device. It does **not** mention translation,
languages or voice packs (Batch 9 features not yet shipped).

**Limits to re-check when pasting** (they change): Microsoft Store description
10,000 chars, up to 7 search terms of <= 30 chars each; Google Play title 30,
short description 80, full description 4,000.

**Download disclosure (Play live as of Slice 50; Microsoft Store goes live with Slice 56).**
Android's first launch downloads a ~145 MB voice pack (Decisions 60, 61), and the
store policies require the listing and privacy text to say so. As of Slice 50 the
Google Play note is live: each `docs/store-listings/*.md` carries a "Google Play
only: download note" section to append to the Play full description (the "Works
offline" bullet is already reworded for both stores). **Slice 56 makes the same
true on Windows** (the MSIX no longer bundles the voice and declares `internetClient`),
so paste the same download note into the Microsoft Store description, in the same
release that ships Slice 56. The "works offline" wording stays true for user data (text
never leaves the device) but the app needs a network once per language; the "no
network" statements in this file's *Capabilities* and *Privacy* sections are updated
accordingly.

**Manual step (user pastes the text):**

- Microsoft Store: Partner Center -> Product (Read The Stupid Text) -> Store
  listings -> Add/manage languages, then fill the description and search terms
  per language.
- Google Play: Play Console -> Grow -> Store presence -> Main store listing ->
  Manage translations, then fill the short and full description per language.

### Listing texts for Danish, Finnish, Swedish, Norwegian, Dutch and German (Slice 103, Decision 107; German Slice 168)

Written by Claude from `docs/store-listings/en.md`: the same blocks (7 search terms, short description, long
description) plus the Google Play download note. **Not pasted into any store yet** (the apps are not public)
and, like the other non-English texts, they need a native read before submission. The title stays "Read The
Stupid Text" (Decision 71). Search terms are <= 30 chars each and short descriptions <= 80 chars (checked by
script when written).

#### Danish (`da`)

Search terms: 1. læs højt, 2. tekst til tale, 3. tts, 4. læs tekst højt, 5. læs markeret tekst op, 6. pdf oplæser, 7. naturlige stemmer offline

Short description: Læs al tekst højt med naturlige stemmer, der virker offline, i 0,5-2x fart.

Long description:

Read The Stupid Text læser al tekst højt, så du kan lytte i stedet for at stirre på en skærm.

På Windows ligger den i systembakken. Markér tekst i næsten alle apps, eller kopiér den, og den bliver læst højt med det samme. Tryk på Ctrl+Win+R for at læse det, du har kopieret. (Terminaler melder ikke markeringer, så kopiér teksten dér.)

På Android kan du indsætte eller skrive tekst, åbne en .txt-, .pdf- eller .docx-fil eller scanne en side med kameraet og derefter trykke på afspil.

- Naturlige stemmer, der kører på din enhed
- Læsehastighed fra 0,5x til 2x
- Virker offline (Android henter sin stemme én gang ved første start). Din tekst, dine filer og dine scanninger forlader aldrig din enhed
- Ingen konto og ingen analyse

Google Play, download-note (føjes til den fulde beskrivelse): Første gang du starter appen på Android, henter den en stemmepakke på ca. 145 MB, så den skal bruge internetforbindelse én gang (Wi-Fi anbefales). Indtil overførslen er færdig, læser appen med telefonens indbyggede stemme. Kun stemmepakken hentes. Din tekst, dine filer og dine scanninger bliver på din enhed, og når stemmen er installeret, bruger appen ikke netværket.

#### Finnish (`fi`)

Search terms: 1. lue ääneen, 2. tekstistä puheeksi, 3. tts, 4. lue teksti ääneen, 5. valitun tekstin luku, 6. pdf ääneen, 7. luonnolliset äänet offline

Short description: Lue teksti ääneen luonnollisilla, offline toimivilla äänillä nopeudella 0,5-2x.

Long description:

Read The Stupid Text lukee minkä tahansa tekstin ääneen, joten voit kuunnella näytön tuijottamisen sijaan.

Windowsissa sovellus on ilmaisinalueella. Valitse teksti lähes missä tahansa sovelluksessa tai kopioi se, niin se luetaan heti ääneen. Paina Ctrl+Win+R, niin kopioimasi teksti luetaan. (Päätteet eivät ilmoita valintoja, joten kopioi teksti sinne.)

Androidissa voit liittää tai kirjoittaa tekstin, avata .txt-, .pdf- tai .docx-tiedoston tai skannata sivun kameralla ja painaa sitten toistopainiketta.

- Luonnollisen kuuloiset äänet, jotka toimivat laitteessasi
- Lukunopeus 0,5x-2x
- Toimii offline-tilassa (Android lataa äänensä kerran ensimmäisellä käynnistyskerralla). Tekstisi, tiedostosi ja skannauksesi eivät koskaan poistu laitteestasi
- Ei tiliä eikä analytiikkaa

Google Play, latausmerkintä (lisätään koko kuvaukseen): Kun käynnistät sovelluksen Androidissa ensimmäisen kerran, se lataa noin 145 Mt:n äänipaketin, joten se tarvitsee internetyhteyden kerran (Wi-Fi suositeltava). Kunnes lataus on valmis, sovellus lukee puhelimen oman äänen avulla. Vain äänipaketti ladataan. Tekstisi, tiedostosi ja skannauksesi pysyvät laitteessasi, eikä sovellus käytä verkkoa, kun ääni on asennettu.

#### Swedish (`sv`)

Search terms: 1. läs upp, 2. text till tal, 3. tts, 4. läs text högt, 5. läs upp markerad text, 6. pdf uppläsare, 7. naturliga röster offline

Short description: Läs upp all text med naturliga röster som fungerar offline, i 0,5x-2x hastighet.

Long description:

Read The Stupid Text läser upp all text högt, så att du kan lyssna i stället för att stirra på en skärm.

I Windows ligger appen i systemfältet. Markera text i nästan vilken app som helst, eller kopiera den, så läses den upp direkt. Tryck på Ctrl+Win+R för att läsa upp det du har kopierat. (Terminaler rapporterar inte markeringar, så kopiera texten där.)

I Android kan du klistra in eller skriva text, öppna en .txt-, .pdf- eller .docx-fil eller skanna en sida med kameran och sedan trycka på spela upp.

- Naturligt klingande röster som körs på din enhet
- Läshastighet från 0,5x till 2x
- Fungerar offline (Android hämtar sin röst en gång vid första start). Din text, dina filer och dina skanningar lämnar aldrig din enhet
- Inget konto och ingen analys

Google Play, nedladdningsnotis (läggs till i den fullständiga beskrivningen): Första gången du startar appen i Android hämtar den ett röstpaket på cirka 145 MB, så den behöver internetanslutning en gång (Wi-Fi rekommenderas). Tills nedladdningen är klar läser appen med telefonens inbyggda röst. Bara röstpaketet hämtas. Din text, dina filer och dina skanningar stannar på din enhet, och när rösten är installerad använder appen inte nätverket.

#### Norwegian Bokmål (`nb`)

Search terms: 1. les høyt, 2. tekst til tale, 3. tts, 4. les tekst høyt, 5. les opp valgt tekst, 6. pdf opplesing, 7. naturlige stemmer offline

Short description: Les tekst høyt med naturlige stemmer som fungerer offline, i 0,5-2x hastighet.

Long description:

Read The Stupid Text leser all tekst høyt, slik at du kan lytte i stedet for å stirre på en skjerm.

På Windows ligger appen i systemstatusfeltet. Merk tekst i nesten hvilken som helst app, eller kopier den, så blir den lest høyt med en gang. Trykk Ctrl+Win+R for å lese det du har kopiert. (Terminaler melder ikke fra om markeringer, så kopier teksten der.)

På Android kan du lime inn eller skrive tekst, åpne en .txt-, .pdf- eller .docx-fil eller skanne en side med kameraet, og deretter trykke på spill av.

- Naturlige stemmer som kjører på enheten din
- Lesehastighet fra 0,5x til 2x
- Fungerer offline (Android laster ned stemmen én gang ved første oppstart). Teksten, filene og skanningene dine forlater aldri enheten
- Ingen konto og ingen analyse

Google Play, nedlastingsmerknad (legges til i den fulle beskrivelsen): Første gang du starter appen på Android, laster den ned en stemmepakke på rundt 145 MB, så den trenger internettforbindelse én gang (Wi-Fi anbefales). Til nedlastingen er ferdig, leser appen med telefonens innebygde stemme. Bare stemmepakken lastes ned. Teksten, filene og skanningene dine blir på enheten din, og når stemmen er installert, bruker ikke appen nettverket.

Norwegian has its own natural-voice pack (`piper-nb`, Slice 104), so the "natural voices" claims stand once the pinned catalogue lists it.

#### Dutch (`nl`)

Search terms: 1. voorlezen, 2. tekst naar spraak, 3. tts, 4. tekst hardop lezen, 5. geselecteerde tekst lezen, 6. pdf voorlezen, 7. natuurlijke stemmen offline

Short description: Lees tekst voor met natuurlijke stemmen die offline werken, op 0,5-2x snelheid.

Long description:

Read The Stupid Text leest elke tekst hardop voor, zodat je kunt luisteren in plaats van naar een scherm te staren.

Op Windows staat de app in het systeemvak. Selecteer tekst in bijna elke app, of kopieer die, en ze wordt meteen voorgelezen. Druk op Ctrl+Win+R om voor te lezen wat je hebt gekopieerd. (Terminals melden geen selecties, dus kopieer de tekst daar.)

Op Android kun je tekst plakken of typen, een .txt-, .pdf- of .docx-bestand openen of een pagina met de camera scannen en daarna op afspelen tikken.

- Natuurlijk klinkende stemmen die op je apparaat draaien
- Leessnelheid van 0,5x tot 2x
- Werkt offline (Android downloadt zijn stem één keer bij de eerste start). Je tekst, bestanden en scans verlaten je apparaat nooit
- Geen account en geen analyse

Google Play, downloadnotitie (toevoegen aan de volledige beschrijving): De eerste keer dat je de app op Android start, downloadt hij een stempakket van ongeveer 145 MB, dus er is één keer een internetverbinding nodig (wifi aanbevolen). Tot de download klaar is, leest de app voor met de ingebouwde stem van je telefoon. Alleen het stempakket wordt gedownload. Je tekst, bestanden en scans blijven op je apparaat en zodra de stem is geïnstalleerd, gebruikt de app het netwerk niet.

#### German (`de`)

The complete German listing (title, Play short description, App Store fields) is now `docs/store-listings/de.md`; Dino's is `docs/store-listings/dino/de.md` (Slice 193, drafts, not yet pasted). The short description below is 86 characters, over Play's 80: use the one in `de.md`.

Slice 168. Written by Claude from `docs/store-listings/en.md`, like the other non-English texts: it needs a native read before submission and is **not pasted into any store yet** (the apps are not public).

Search terms: 1. vorlesen, 2. text zu sprache, 3. tts, 4. text laut lesen, 5. markierten text lesen, 6. pdf vorlesen, 7. natürliche stimmen offline

Short description: Liest Text mit natürlichen Offline-Stimmen vor, mit 0,5- bis 2-facher Geschwindigkeit.

Long description:

Read The Stupid Text liest jeden Text laut vor, damit du zuhören kannst, statt auf einen Bildschirm zu starren.

Unter Windows sitzt die App im Infobereich. Markiere Text in fast jeder App oder kopiere ihn, und er wird sofort vorgelesen. Drücke Strg+Win+R, um das Kopierte vorzulesen. (Terminals melden keine Auswahl, kopiere den Text dort also.)

Auf Android kannst du Text einfügen oder tippen, eine .txt-, .pdf- oder .docx-Datei öffnen oder eine Seite mit der Kamera scannen und dann auf Abspielen tippen.

- Natürlich klingende Stimmen, die auf deinem Gerät laufen
- Lesetempo von 0,5x bis 2x
- Funktioniert offline (Android lädt seine Stimme beim ersten Start einmal herunter). Dein Text, deine Dateien und deine Scans verlassen dein Gerät nie
- Kein Konto und keine Analyse

Google Play, Download-Hinweis (zur vollen Beschreibung hinzufügen): Beim ersten Start der App auf Android lädt sie ein Stimmpaket von etwa 145 MB herunter und braucht dafür einmal eine Internetverbindung (WLAN empfohlen). Bis der Download fertig ist, liest die App mit der eingebauten Stimme deines Handys vor. Es wird nur das Stimmpaket heruntergeladen. Dein Text, deine Dateien und deine Scans bleiben auf deinem Gerät, und sobald die Stimme installiert ist, nutzt die App das Netzwerk nicht.

German uses the Supertonic-3 pack (`supertonic-3` lists `de` from pack version 4), so the "natural voices" claims stand once the pinned catalogue (`packs-8`) is published.

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
size (~+50 MB for the runtime). Since Slice 56 the package no longer carries the
~145 MB voice model (it downloads as a pack), so it is that much smaller. The Debug inner loop (`dotnet run` / VS **(Package)** profile)
stays framework-dependent so it remains fast.

## Privacy & diagnostics

The Windows app's only data use is the same anonymous counts as the phones (Slice 140, see
"Phone apps: anonymous usage counts" below), **off until the remote config switches them on** (Decision 145). The read-latency timing diagnostics
added in Slice 19 (time-to-first-audio and synthesis duration per read) live only in
the in-memory activity log — shown in the activity-log window, capped, and cleared on
every restart. There is no third-party analytics SDK and no on-disk telemetry. Since
Slice 56 the network calls on Windows are the voice-pack download (below) and, from Slice 140, the
optional anonymous counts, so the Store privacy questionnaire is answered for the counts
("Microsoft Store (RTST)" in the checklist below) once they are switched on.

**Windows / Microsoft Store (Slice 56, Decision 60).** The Windows app's only network use
is downloading language voice packs from this repo's GitHub Releases (plus, since Slice 140, the optional counts), through the
`internetClient` capability. It is an anonymous file download (the host sees an IP address,
as for any web request; the app does not log it). Nothing about the user, their device or
their content is sent. Packs are stored under the package's `LocalCache\packs` folder, and
cached sentence audio under `LocalCache\sentences` (device only, capped at 100 MB).

**Android / Google Play data safety (Slice 50, Decision 60).** The Android app's only
network use is downloading language voice packs from this repo's GitHub Releases (the
`packs-<n>` releases, `docs/voice-packs.md`). Nothing about the user, their device or
their content is sent: the request is an anonymous file download (the host sees an IP
address, as for any web request, and the app does not log it). So the Play Console data
safety form stays "no data collected" and "no data shared"; the app declares the
`INTERNET` and `ACCESS_NETWORK_STATE` permissions to download packs and detect offline.
Text, images, scans and translations stay on the device. The form needs a **manual update
in Play Console by the owner** before the first release that ships this: the app is no
longer fully offline, so answer the "does your app collect or share data" questions as
above and make sure the privacy policy URL points at the updated `site/privacy.html`.

For **dev-time** performance tuning only, the read pipeline may optionally be
instrumented with **OpenTelemetry** (`Activity`/`Meter`) and observed via a **local
Aspire dashboard** (an OTLP viewer on the developer's own machine). This is **not
part of the shipped MSIX** and exports nothing off the device. Full .NET Aspire was
evaluated and rejected as a shipped mechanism — it orchestrates *distributed* apps at
dev time and is not a redistributable runtime (Decision 26 in `docs/plan/decisions.md`).

## Phone apps: anonymous usage counts (Slice 137, Decisions 143-145)

The Android and iPhone apps send anonymous counts to the Worker (`POST readthestupidtext.sirous.uk/api/events`, `worker/events.ts`, stored in
the Workers Analytics Engine dataset `rtst_usage`, 3 months). Each count is a name plus closed-list values: `app_opened` (platform, OS major
version, app version, app language), `read_started` (detected source language, read language, `original`/`translation`, voice engine
`phone`/`supertonic`/`piper`/`melo`/`kokoro`, whether the voice is the default, text length bucket), `translate_offered` and
`translate_chosen` (language pair), `voice_changed` (language, engine) and `timing` (stage `model_download`/`translate`/`first_audio`, language pair,
outcome `done`/`cancelled`/`failed`, duration bucket <0.5, 0.5-1, 1-2, 2-5, 5-10, 10-20, >20 s), plus app version, platform and the UTC date. No install,
device or advertising id, no read text, no file names, no time of day. The server sees the caller's IP while it answers but reads and stores
none of it with the counts. On by default, with a "Help improve the app" switch in Settings and one line on the first-run language screen; no
consent prompt (Decision 143). The remote config (`readthestupidtext/config.json`, `telemetry.enabled`, `sampleRate`) is **off until every row of
the Store privacy declarations checklist in `plan.md` is ticked** (Decision 145). Windows (Slice 140) sends the same counts through the same pipe, with `platform = windows`, the engine `windows` (the Windows speech voice) and a `source` on each read (`selection`, `clipboard`, `hotkey` or `panel`, never a window or app name); its switch is the last row of the control panel, stored in the app's local data. **Microsoft Store row of the checklist:** answer the Store privacy questionnaire for the counts (anonymous app-activity counts, no identifier, optional) in the same step as the Play and App Store forms; until it is ticked the config keeps counting off, so the Windows build sends nothing.
Store forms (Claude drafts and fills them in the signed-in browser; the user reviews and submits):

- **Google Play Data safety (RTST):** "Data collected: yes", **App activity > App interactions** and **App info and performance > Diagnostics**,
  collected, **not shared**, **processed ephemerally: no**, optional (the user can turn it off), purpose **Analytics**; no personal info, no device
  or other ids. Encrypted in transit: yes. Deletion request: not applicable (nothing ties data to a person). This replaces the "no data
  collected" answers of Slice 50 above once counting is switched on.
- **App Store privacy (RTST):** **Usage Data > Product Interaction** and **Diagnostics > Performance Data**, **not linked to the user**, **not used
  for tracking**, purpose **Analytics**. `PrivacyInfo.xcprivacy` declares the same (`NSPrivacyCollectedDataTypeProductInteraction` and
  `NSPrivacyCollectedDataTypePerformanceData`, linked false, tracking false).
- **Slice 166 additions (both apps, same wording for Dino below):** each batch carries a `device` object (`osMajor`, `formFactor` phone/tablet/desktop/other,
  `arch` arm64/x64/x86/arm/other, `cores` 1-4/5-8/9+, `ram` GB bucket <3/3-4/4-6/6-8/8+ (total rounded up to whole GiB first); never a model, maker, exact chip, exact memory/core number or id) and the
  Worker adds the **country** from `request.cf.country` (`XX`/`T1`/invalid become `none`; the IP is not stored). Form answers to add **before the Worker change
  merges**: **Google Play Data safety:** Location > **Approximate location**, collected, not shared, not ephemeral, optional, Analytics (Diagnostics is already declared);
  **App Store privacy:** **Coarse Location** and **Other Diagnostic Data**, not linked, no tracking, Analytics (`PrivacyInfo.xcprivacy` of both apps declares
  `NSPrivacyCollectedDataTypeCoarseLocation` and `NSPrivacyCollectedDataTypeOtherDiagnosticData`); **Microsoft Store:** nothing beyond the policy URL. Check Google Play's
  Families policy wording on approximate location for Dino before submitting. (Slice 166: docs/plan/slices/166-usage-device-context.md)
- **Microsoft Store (RTST):** no data-collection question beyond the privacy policy URL, which must point at the updated `site/privacy.html`
  (the Windows app sends the same counts once switched on; the switch is the last row of the control panel).
- Cloudflare Analytics Engine is the processor. The `RTST_USAGE` binding (dataset `rtst_usage`) is in both wrangler configs. To switch counting on,
  put `{"telemetry": {"enabled": true, "sampleRate": 1}}` in `readthestupidtext/config.json`
  (`wrangler r2 object put readthestupidtext-downloads/readthestupidtext/config.json --file config.json --remote`).
- Each point's double is the count's weight (1 / the sample rate it was counted at), so query with `SUM(double1)`, never `COUNT(*)`; blobs are
  `name, version, platform, day, language, sourceLanguage, mode, engine, voiceDefault, lengthBucket, osMajor, stage, outcome, durationBucket, source, early, waitStage` (`source` on Windows reads only; `early` on timing counts only, `yes` when the first sentences were prepared while the person decided, Slice 139), then blob18 country, blob19 form factor, blob20 CPU architecture (Slice 166); double2 is the core bucket's lower bound (1/5/9) and double3 the RAM bucket's lower bound in GB (0/3/4/6/8), -1 when a build sent none, and blob11 `osMajor` is filled on every count.
  `tools/rtst-usage-report.ps1` hides groups of fewer than 10 events. That floor counts events, not households (there is no install id), so it
  is a noise filter and not an anonymity guarantee: say "no identifiers, counts only", never "cannot be traced".

## Third-party components and licenses

| Component | Use | License |
| --- | --- | --- |
| Windows App SDK / WinUI 3 | UI, windowing | MIT |
| `H.NotifyIcon.WinUI` | tray icon | MIT |
| `org.k2fsa.sherpa.onnx` | neural TTS runtime | Apache-2.0 |
| Supertonic-3 voice model (`VoiceModel/`) | neural voices | Sample code MIT (`VoiceModel/LICENSE`); the model weights are **BigScience OpenRAIL-M** per the Hugging Face model card and its `LICENSE` file (checked 2026-10-25; this row said Apache-2.0 before). See "Supertonic weights licence" below |
| Kokoro-82M v1.0 voices (downloaded storyteller pack `kokoro-en`, Dino Book Reader only, not in the package; sherpa-onnx int8 build `kokoro-int8-multi-lang-v1_0`) | English storyteller voices | Apache-2.0 (model card, checked 2026-10-25; the licence text is in the pack as `LICENSE-Kokoro-Apache-2.0`) |
| Piper `fa_IR` "ganji_adabi" voice, the default (downloaded Persian pack `piper-fa`, not in the package) | Persian neural voice | Model MIT, voice data CC0 1.0 ([rhasspy/piper-voices](https://huggingface.co/rhasspy/piper-voices/tree/main/fa/fa_IR/ganji_adabi/medium); model card). Fine-tuned from `ganji`, itself fine-tuned from `amir` (dataset `SadeghK/datacula-pertts-amir`, Apache-2.0, so the pack keeps that attribution) |
| Piper `fa_IR` "ganji" voice (same pack) | Persian neural voice | Model MIT, voice data CC0 1.0 ([rhasspy/piper-voices](https://huggingface.co/rhasspy/piper-voices/tree/main/fa/fa_IR/ganji/medium); its own site `tts.datacula.com` did not respond on 2026-10-22, so CC0 rests on the Piper model card) |
| "Mana" Persian Piper voice (same pack; no sherpa-onnx build, so metadata and int8 quantization are added in `packs.yml`) | Persian neural voice | Model MIT ([MahtaFetrat/Mana-Persian-Piper](https://huggingface.co/MahtaFetrat/Mana-Persian-Piper)), data CC0 ([MahtaFetrat/Mana-TTS](https://huggingface.co/datasets/MahtaFetrat/Mana-TTS), released by the Nasl-e-Mana magazine) |
| Piper `fa_IR` "gyro" voice (same pack) | Persian neural voice | Model MIT ([gyroing/Persian-Piper-Model-gyro](https://huggingface.co/gyroing/Persian-Piper-Model-gyro)), data CC0 (Kaggle `magnoliasis/persian-tts-dataset-male`) |
| espeak-ng data (inside the downloaded Persian, Norwegian and Kokoro storyteller packs only) | phonemizer data the Piper voice needs | GPL-3.0-or-later; corresponding source: <https://github.com/espeak-ng/espeak-ng> |
| MeloTTS `zh_en` (downloaded Chinese pack `melo-zh-en`, not in the package) | Chinese + English neural voice | MIT (MyShell.ai; see the `LICENSE` inside the pack) |
| PaddleOCR PP-OCR detection and recognition models (downloaded OCR packs `ocr-latin`, `ocr-cyrillic`, `ocr-arabic`, `ocr-cjk`, not in the package; ONNX conversion by [monkt/paddleocr-onnx](https://huggingface.co/monkt/paddleocr-onnx)) | on-device text recognition for scans (Android and iOS) | Apache-2.0 (PaddlePaddle Authors; see the `LICENSE` inside each pack) |
| Opus-MT translation models (downloaded translation packs `translation-<from>-<to>`, not in the package; Marian encoder/decoder, int8 ONNX conversion by [Xenova](https://huggingface.co/Xenova)) | offline translate-before-read (Windows) | CC-BY-4.0 (Helsinki-NLP / OPUS-MT, https://github.com/Helsinki-NLP/Opus-MT; converted and quantised to int8; see the `LICENSE` inside each pack) |
| ElanMT English-to-Japanese translation model (downloaded pack `translation-en-ja`, not in the package; Marian encoder/decoder, converted to ONNX and int8-quantised by this project's packs workflow) | offline translate-before-read, English to Japanese (Windows) | CC-BY-SA-4.0 (Credit: Mitsua Inc. / ElanMT, https://huggingface.co/Mitsua/elan-mt-bt-en-ja; ShareAlike applies to the model files only, never the app code; see the `LICENSE` inside the pack) |
| `Microsoft.ML.Tokenizers` | reads the SentencePiece model of a translation pack (Windows) | MIT |
| `Microsoft.ML.OnnxRuntime.Managed` | C# binding to the onnxruntime library sherpa-onnx already bundles (Android, and Windows for translation) | MIT |
| `Microsoft.ML.OnnxRuntime` (iOS only) | the same binding plus its own static onnxruntime for PaddleOCR; sherpa-onnx's iOS framework does not export one | MIT |
| `PdfPig` | PDF text extraction (file upload) | Apache-2.0 |
| `DocumentFormat.OpenXml` | DOCX text extraction (file upload) | MIT |
| Fredoka, Andika, Vazirmatn fonts (Dino Book Reader only, bundled in `src/DinoReader.App/Resources/Fonts/`) | Dino Book Reader UI, reading and Farsi/Arabic text | SIL OFL 1.1 (texts in `src/DinoReader.App/Licenses/`) |

No GPL/LGPL components ship in the package. There is one deliberate, scoped
exception (Decision 63): the Persian voice is a Piper voice that needs the
GPL-3.0 espeak-ng data, and that data exists **only inside the separately
downloaded `piper-fa` pack** (built by `.github/workflows/packs.yml`, hosted on the
`packs-<n>` GitHub release, with its licence text and a link to the corresponding
source in the pack's `LICENSE`). It is never in the repository, the MSIX, the AAB
or the `.ipa`, and the app only passes its folder path to the engine (a
data-file use at arm's length), so the Store package itself stays free of GPL
code. Check this by unzipping the app package: there is no `espeak-ng-data`.

The same exception covers the Kokoro storyteller pack `kokoro-en` (Slice 114, Decision 119): Dino Book Reader's
English storyteller voices run on espeak-ng phonemes, so that pack carries the GPL-3.0 espeak-ng data inside its zip
only, with its licence text (`LICENSE`) and the source link (`SOURCE`), and the app only passes its folder path to the
engine. Check this by unzipping the app package: still no `espeak-ng-data`.

**Supertonic weights licence (found 2026-10-25, Slice 114).** The Supertonic-3 model card and the `LICENSE` file in
`Supertone/supertonic-3` on Hugging Face say the weights are BigScience OpenRAIL-M (the repo's own `LICENSE` is MIT
for the sample code only). OpenRAIL-M allows commercial use but requires: a copy of the licence for recipients
(section 4(b)), the use-based restrictions of its Attachment A passed on as an enforceable term to anyone who uses or
receives the model (4(a)), and the notices kept (4(d)). The restrictions that matter here: no use to exploit or harm
minors, to spread false information, to impersonate people, to defame or harass, to give medical advice, or for
automated decisions about legal rights, and machine-generated content must be disclosed as such when it is published.
Reading a child's picture book aloud complies with all of them. **Done (Slice 114):** the `supertonic-3` pack zip now carries the
OpenRAIL-M text (`LICENSE-OpenRAIL-M`, from `.github/licenses/supertonic-3-OpenRAIL-M.txt`), a `NOTICE` with the restrictions and the
source link, and the MIT text (`LICENSE`); `packs.yml` stamps pack version 3 so it is re-released, and its catalogue `license` says
OpenRAIL-M and MIT. The model files inside the app package still carry only the MIT `VoiceModel/LICENSE`: the Store listing
terms are the remaining place to state the restrictions.

Persian is therefore available only on the platforms that download packs (Android
and, since Slice 56, Windows). Where a pack can't be downloaded, Persian reads with the system voice.
The Persian pack offers four voices (Slice 89, Decision 93): `ganji_adabi` (default),
`ganji`, `mana` and `gyro`, all with stated CC0 voice data. Open licence question, to re-check before
a wide launch: `ganji_adabi`, `ganji` and `mana` descend from `amir` (their model cards say fine-tuned
from `ganji`, `amir` and `amir`), which was fine-tuned from the Piper US-English "lessac" voice, whose
dataset terms were never settled (Slice 51 notes). `gyro`'s own card names no lessac parent. `amir` was dropped (the user found it
Afghan-accented, not Farsi); `reza_ibrahim` (dataset page gone, Quran-recitation corpus), Gooya v2
(needs its own G2P front end) and MMS-TTS (CC-BY-NC) were rejected.

## Versioning (GitVersion → tag → release)

Versioning is fully automatic and lives **entirely inside `release.yml`** — one
workflow run does version → build → release, with no PAT and no commit-back
(Decision 17 / Slice 14). **Git tags are the source of truth.**

1. The `version` job runs **GitVersion** once per product (Slice 130, Decision 137):
   `GitVersion.yml` (GitHub Flow preset, tag prefix `rtst-v`) for Read The Stupid Text and
   `GitVersion.dino.yml` (`dino-v`) for Dino Reader. It reads git history and computes the next
   SemVer. `main` defaults to a **Patch** bump over that product's last tag; the old shared `v*`
   tags are ignored.
2. The `build` job stamps that version (`x.y.z.0` — the Store needs revision `0`)
   into the manifest **at build time** and packages the MSIX. Nothing is
   committed back.
3. On a push to `main`, the `release-publish-rtst` job creates the **`rtst-v<x.y.z>` tag** at the
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

Distribution uses **GitHub Releases**. They are also CI's only output store,
because Release assets don't count toward the Actions storage quota. Every push
to `main` that touches app code cuts one (see *Versioning* above). It is created
as a **draft**, each platform build uploads into it, and it is published (which
creates the `rtst-v*` tag) only once every affected build succeeded. When a change
touched only one platform, the other platform's assets are **carried forward**
from the product's last release, so `releases/latest` (always the RTST release; Dino Reader's
`dino-v*` releases are published not-latest) has the MSIX and the APK. The packages get **stable URLs** under
`…/releases/latest`, linked from the README, and serve as the hosted source the
Store-submission step pulls from.

## Deploying to the Store

`/.github/workflows/store-submit.yml` downloads a release's MSIX assets,
**combines x64 + ARM64 into one `.msixbundle`** (`makeappx bundle`), and
submits that single bundle via the **msstore CLI**
(`microsoft/microsoft-store-apppublisher`). The Actions-based msstore flow
does *updates* to an already-published **free** app — which this app now is.
One submission must carry both architectures, so the workflow bundles rather
than calling `msstore publish` once per `.msix` (which would open competing
submissions).

**Automatic since Decision 44.** `release.yml`'s `store-submit` job calls this
workflow (`workflow_call`) right after cutting a release that changed the Windows app
(see line 51), so it reaches the Store with no manual step — this closed a real gap
where the workflow was manual-only and simply wasn't re-run for 15 days
(`v0.7.8` … `v0.14.0` piled up, unshipped). It's called as a job in the *same*
run rather than via an `on: release: published` trigger on this file, because
`release.yml` creates the release with the default `GITHUB_TOKEN`, and GitHub's
recursion guard means a `GITHUB_TOKEN`-authored event never starts another
workflow run — that trigger would have looked correct and never fired.
`workflow_dispatch` still works standalone for a manual re-submit (e.g.
retrying a transient upload failure) — pass `product_id` (required) and a tag, or omit the tag to resubmit
whatever is currently the latest release.

Each call creates a **new** Store submission from the given release,
discarding whatever draft was already pending (see *msstore publish deletes
the pending draft* below) — so if `main` ships several releases faster than
Store certification turns around, only the latest one ends up in review; it
is not "queued update spam."

One detail the workflow gets right and that is easy to get wrong by hand:
`makeappx bundle` is given **`/bv`** (the packages' own `x.y.z.0`, parsed off the
release asset names) — omit it and makeappx stamps the bundle version from the
*current date-time*, which matches neither the release nor a predictable ordering.

⚠️ **The action installs whatever `msstore-cli` is currently `latest` — no
version pin.** It moved from v0.3.9 (proven on `v0.7.7`, 2026-08-16) to v0.4.1
between then and the automation work in Decision 44, silently changing
`publish`'s available flags and defaults underneath this workflow with no
action-version bump to signal it.

⚠️ **`-uploadTimeout`/`-ut` is REQUIRED, not optional, on v0.4.x — omitting it
sets the network timeout to zero.** v0.4.0 added the flag
(`PublishCommand.cs`), intending a 100s default via its `CustomParser`. But
that parser only runs when `-ut` is present with an empty value; when the flag
is **omitted** entirely, `System.CommandLine` binds it to `default(long)` = 0,
and `AzureBlobManager` sets `blobClientOptions.Retry.NetworkTimeout =
TimeSpan.Zero` — every upload attempt then fails **instantly**. This is
exactly what happened submitting `v0.14.0`: three straight failures, each
dying within ~90s of "Uploading Bundle to Azure blob: 0%" with a generic
*"Error while uploading the application package."* on-screen (that string is
hardcoded — `IStorePackagedAPIExtensions.cs` swallows the real exception into
the logger, only visible via `-v`). The `-v` log showed the actual cause:
`System.AggregateException: Retry failed after 6 tries... The operation was
cancelled because it exceeded the configured timeout of 0:00:00.` Fix:
`store-submit.yml` now always passes `-ut 300` explicitly — comfortably above
the ~3s a ~500 MB bundle actually took to upload back when v0.3.9 worked.

⚠️ **Check option flags against the CLI version the action actually installs,
not against the docs or the CLI's `main` branch** — they can and do drift
independently of what's pinned here. `-i/-id/-nc/-f/-prp/-v/-ut` is current
for v0.4.x; passing an option that genuinely doesn't exist on the installed
version fails the run with *"Unrecognized command or argument '‹flag›'"*.

### `msstore publish` deletes the pending draft

Per Microsoft's own docs: *"If the app already has a published submission,
`msstore publish` deletes the pending draft and creates a new one from the
last published submission, discarding any metadata changes already staged in
that draft."* Confirmed in practice: triggering `store-submit` while an
earlier submission was still mid-review created a **new** submission that
superseded it rather than erroring or queuing behind it. This is exactly the
behavior automatic-on-every-release wants — the newest push always wins — but
it means there is no way to have two releases "in flight" to the Store at
once; the older one is simply gone, not paused.

### The CLI supersedes old packages by **file extension**

`msstore publish` clones the last published submission and then decides what the
new package replaces with a single line
(`IStorePackagedAPIExtensions.cs` — confirmed still present on the CLI's `main`
branch as of Decision 44; see the version-pin warning above about not assuming
this stays true on whatever `latest` installs next):

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

## iOS / TestFlight (Slice 48, Decision 70)

The iOS app is the same MAUI project (`net10.0-ios`), built on a macOS runner by
`release.yml`'s `build-ios` job (`ios.yml`). Every merge that touches iOS (or shared Mobile/core
code) lands a build in TestFlight; the App Store release itself stays a manual
step in App Store Connect. No `actions/upload-artifact`: the signed `.ipa` goes to
the GitHub Release as `ReadTheStupidText-<version>.ipa`, and `testflight-submit`
uploads it to App Store Connect with `xcrun altool --upload-app` after the release
publishes. Apple takes up to ~30 minutes to process a build.

**Identity (Lets Call Mom's Apple team, its own App ID):**

| Item | Value |
| --- | --- |
| Bundle ID | `uk.sirous.readthestupidtext` (same as the Android `ApplicationId`) |
| Team | `J4TBWA5379` (shared with Lets Call Mom) |
| App Store Connect app | "Read The Stupid Text", SKU `readthestupidtext` |
| Certificate | the team's Apple Distribution certificate, reused (team-wide) |
| Profile | an *App Store* distribution profile for the bundle ID (per app) |
| API key | a new App Store Connect API key, App Manager role (per app) |

**Secrets (all six; any missing and CI builds unsigned and uploads nothing).** Same
names as Lets Call Mom, stored via `manage-secrets` and set as this repo's Actions
secrets: `IOS_DIST_CERT_P12_BASE64`, `IOS_DIST_CERT_P12_PASSWORD`,
`IOS_PROVISIONING_PROFILE_BASE64`, `ASC_API_KEY_P8_BASE64`, `ASC_API_KEY_ID`,
`ASC_API_ISSUER_ID`.

**One-time manual steps (Apple side):**

1. Developer portal, *Identifiers*: register App ID `uk.sirous.readthestupidtext` (explicit, no extra capabilities).
2. App Store Connect, *Apps*: create the app record (name, bundle ID, SKU above).
3. Developer portal, *Profiles*: create an *App Store* distribution profile for that App ID using the team's Apple Distribution certificate; download the `.mobileprovision`.
4. App Store Connect, *Users and Access, Integrations*: create an API key with the App Manager role; download the `.p8` once, note the key ID and issuer ID.
5. Export the Apple Distribution certificate from Keychain Access as a `.p12` (with a password), or copy Lets Call Mom's.
6. Base64-encode the `.p12`, the profile and the `.p8`, save all six values with `manage-secrets`, and set them as repo secrets.
7. Once a build shows in TestFlight: add it to a tester group in App Store Connect (manual, Decision 70).

**Without the secrets** `build-ios` only compiles for the simulator, nothing is
signed or uploaded, and a change that touches only iOS cuts no release (an unsigned
build cannot ship).

**Batch 9 features on iOS (Slice 55).** The neural voice (sherpa-onnx 1.13.8, Supertonic, Piper
and Melo packs with the sentence head start), language and OCR packs, PaddleOCR with Apple
Vision as the fallback, the fifteen localized UI languages (right-to-left for Farsi and Arabic) and
localized camera and photo permission prompts (`InfoPlist.strings`) are on iOS. Packs download over
plain HTTPS from GitHub Releases, so no App Transport Security exception and no entitlement is
needed (Decision 60); the packs folder is excluded from iCloud backup.
`ITSAppUsesNonExemptEncryption` is `false` (HTTPS only).

**Translation on iOS (Slice 160).** Translation (ML Kit) now runs on Android and iPhone, on device after a one-time model download. Apple's Translation framework was not used (Swift-only, no Farsi). iOS simulator builds have no translator (the MLKitTranslate xcframework has no simulator slice), so the card hides there.

## Dino Book Reader on Windows (Slice 67)

Dino Book Reader is a **separate Store product** from Read The Stupid Text: its own listing, its own
package identity, the same developer account. The package is `src/DinoReader.App` (a MAUI WinUI head,
`net10.0-windows10.0.19041.0`), built by `dinoreader-windows.yml` from `release.yml`'s `build-dinowin` job
as an **unsigned** MSIX per architecture (x64, ARM64; the Store re-signs), uploaded to the draft release as
`DinoReader.App_<x.y.z.0>_<arch>.msix`. Like Read The Stupid Text's, the Release package is built .NET
**self-contained** (Store certification 10.2.4.1); the Windows App SDK stays framework-dependent. There is no
tray, no hotkey and no startup task: it is a normal window app.

**Identity (reserved in Partner Center).** Store ID **`9N9VHRLCZXN5`**, store URL
https://apps.microsoft.com/detail/9N9VHRLCZXN5. `Platforms/Windows/Package.appxmanifest` matches the
reservation: `Name` `AshkanSirous.DinoReader`, `Publisher` `CN=53769961-EF08-4BA5-A1DE-7A51B62A9AA7`,
`PublisherDisplayName` `Ashkan Sirous`. They must keep matching exactly or submission fails.

The Partner Center app name **"Dino Book Reader"** must be reserved under `9N9VHRLCZXN5` before the first Store
submission: the manifest `DisplayName` must match a reserved name.

**Capabilities (Package.appxmanifest):**

| Capability | Type | Justification |
| --- | --- | --- |
| `runFullTrust` | restricted | The standard capability of a packaged WinUI 3 / MAUI desktop app (the MSIX entry point is a full-trust process). Dino Reader uses no Win32 reach beyond that: no tray, no hotkey, no reading of other apps. |
| `internetClient` | standard | Downloads the voice pack for the language the user chooses (an anonymous HTTPS file download from this project's GitHub Releases, `packs-<n>`). No story text, image or identifier is ever sent; once a pack is installed the app needs no network. Partner Center justification: "Downloads the text-to-speech voice for the language the user chooses." |

File and photo picking go through the system pickers (no `broadFileSystemAccess`, no pictures library
capability). Pictures are read on the device with `Windows.Media.Ocr`; PDFs are rendered with
`Windows.Data.Pdf`. Windows has no document scanner, so the Scan tile is shown inert.

**Third-party components and licenses.** Same stack as Read The Stupid Text (see the table above):
`org.k2fsa.sherpa.onnx` (Apache-2.0) with the downloaded Supertonic-3 pack (Apache-2.0), `PdfPig`
(Apache-2.0), `DocumentFormat.OpenXml` (MIT), Windows App SDK and .NET MAUI (MIT), and the bundled
Fredoka, Andika and Vazirmatn fonts (SIL OFL 1.1, texts in `src/DinoReader.App/Licenses/`). No GPL/LGPL
component ships in the package.

**Store submission.** `store-submit-dino` in `release.yml` calls `store-submit.yml` with
`package: DinoReader.App` and the product id from the repository variable **`DINOREADER_STORE_APP_ID`**
(the Store id of the reserved listing). The job runs only when that variable is non-empty, so until the
listing exists a release builds and publishes the MSIX and skips the submission. It uses the same
`AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET` and `SELLER_ID`
secrets as Read The Stupid Text (they belong to the developer account). The very first submission of a
brand-new listing (age rating, pricing, store listing text) is done once by hand in Partner Center; the
CLI then pushes updates. The first submission was made by hand on 2026-10-29 (1.78.1.0) and the app went live on 2026-10-30;
the repository variable `DINOREADER_STORE_APP_ID=9N9VHRLCZXN5` is set, so every Dino release now submits
itself (Slice 203). Never start a Partner Center draft by hand while a release may run: the CLI publish
discards a pending draft.

## Dino Book Reader: anonymous usage counts (Slice 133, Decisions 140-142)

Dino sends anonymous counts to its own Worker (`POST dinoreader.sirous.uk/api/events`, `worker/events.ts`, stored in the Workers
Analytics Engine dataset `dino_usage`, 3 months). Each count is: its name (`book_opened`, `book_finished`, `store_download`, `own_book_added`, `recording_made`, `voice_changed`, `reading_language_changed`, `app_opened`; Slice 134; `intro_started`, `intro_step`, `intro_ended` for the first-run intro, Slice 155: detail `first_run`/`replay`, `store`/`record`/`add`/`languages` + `:tried`/`skipped`, `finished`/`skipped_all`; Slice 173: `story_languages_set` (detail `<1-3>:intro`/`settings`, language = the app language), `story_language_added` (language added), `story_language_switched` (the child's switch; language = the new one, book as for `book_opened`, `none` from the shelf button), `voice_pack_download` (language, detail `done`/`failed`/`cancelled`); from Slice 173 `book_opened`/`book_finished` carry the language the book was read in, not the book's own), the book (a shelf book's
package id for a starter or store story only, or `own` for any other book, never a title), the language, shelf or own, whether a parent recording exists, one closed extra value (`detail`: store filters set or not, own-book kind photo/pdf/scan/docx/txt/mixed/typed, or restore for a book a backup restore put back, plus the book's page-count bucket (1, 2-5, 6-10, 11+; Slice 178), recorded-pages bucket, what voice part changed, or the language changed from), app version, platform and the UTC
date. Counts about no book (voice, language, app opened) name `none` as book and source. No install, device or advertising id, no typed text, no time of day. On by default, with a grown-up's switch ("Help make Dino
better") in Settings and one line on the first-run ready screen; the remote config (`telemetry.enabled`, `sampleRate`) turns it down
without a release. Store forms (the owner or Claude in the signed-in browser, after the release that first sends events ships):

- **Google Play Data safety (Dino):** "Data collected: yes", type **App activity > App interactions**,
  collected, **not shared**, **processed ephemerally: no**, optional (the user can turn it off), purpose **Analytics**; no
  personal info, no device or other ids. Encrypted in transit: yes. Deletion request: not applicable (no way to tie data to a person).
  Families policy: no advertising id, no third-party SDK for the counts (first-party call to our own server).
- **App Store privacy (Dino):** data type **Usage Data > Product Interaction**, **not linked to the user**, **not used for tracking**,
  purpose **Analytics**. `PrivacyInfo.xcprivacy` declares the same (`NSPrivacyCollectedDataTypeProductInteraction`, linked false, tracking false).
- **Microsoft Store (Dino):** no data-collection question beyond the privacy policy URL; it must point at the updated `site/dino/privacy.html`.
- Cloudflare Analytics Engine is the processor. The `DINO_USAGE` binding (dataset `dino_usage`) is in both wrangler configs;
  `telemetry.enabled` in `dinoreader/config.json` switches counting (see "Landing site" in `CLAUDE.md`). Each
  point's double is the count's weight (1 / the sample rate it was counted at), so query with
  `SELECT blob1 AS name, blob2 AS book, SUM(double1) FROM dino_usage GROUP BY name, book`.
- Slice 166 adds blob10 country, blob11 OS major, blob12 form factor, blob13 CPU architecture, blob14 core bucket and blob15 RAM bucket (empty for builds that send no `device` object); the store-form additions are listed under "Slice 166 additions" in the RTST section above.
- `tools/dino-usage-report.ps1` hides groups of fewer than 10 events. That floor counts events, not households (there is no install id), so it is a noise filter and not an anonymity guarantee: say "no identifiers, counts only", never "cannot be traced".
- Switch counting on with `telemetry` in `dinoreader/config.json` (`{"enabled": true, "sampleRate": 1}`); it is off until then.

## Phone apps: crash reports (Slice 227)

Both phone apps (Dino Reader and Read The Stupid Text, Android and iPhone) send a crash or error report to their own Worker (`POST dinoreader.sirous.uk/api/crash`,
`POST readthestupidtext.sirous.uk/api/crash`, `worker/crashes.ts`) the next launch after a crash. Each report is kept as one JSON object in the downloads bucket under
`crashes/<app>/<yyyy-mm-dd>/<random id>.json` and counted in the Analytics Engine dataset `app_crashes` (binding `APP_CRASHES`). A report holds: app, platform, version,
install channel (`store` or `testflight`), kind (`managed`, `native`, `unclean_exit`, `anr`, `low_memory`, `hang`), exception type, message (1 KB, paths removed), stack (16 KB),
the last 300 lines of the app's rolling log (32 KB; technical lines only, audited for typed or read text, book titles and file names), the UTC minute and the same coarse device buckets
as the usage counts. No install, device, account or advertising id (the Worker names the object); the IP is never stored (it is only the rate limiter's key). It follows the existing
"Help improve the app" / "Help make Dino better" switch and the remote config key `crashReports.enabled` (default off, missing = off), which is switched on **only after the store
forms below are saved**. TestFlight installs send reports (the user's exception to the production-only rule, crash reports only); debug builds, unstamped versions, emulators,
Firebase Test Lab, robots and the harnesses' off switch send nothing.

Store forms (Slice 227 item 8; the owner or Claude in the signed-in browser, before `crashReports.enabled` is set to true):

- **Google Play Data safety (both apps):** add **App info and performance > Crash logs**, collected, **not shared**, **processed ephemerally: no**, optional (the user can turn it off), purpose **App functionality** (and Analytics if the form insists on one); no personal info, no ids. Encrypted in transit: yes. Deletion request: not applicable.
- **App Store privacy (both apps):** **Diagnostics > Crash Data**, **not linked to the user**, **not used for tracking**, purpose **App Functionality**. `PrivacyInfo.xcprivacy` of both apps declares the same (`NSPrivacyCollectedDataTypeCrashData`, linked false, tracking false, `NSPrivacyCollectedDataTypePurposeAppFunctionality`).
- **Microsoft Store:** unchanged (no phone crash reports on Windows); the privacy URL stays `site/privacy.html` / `site/dino/privacy.html`, which now have a "Crash reports" section.
- Reading them: `tools/crashes/` (`pull-crashes.mjs` for R2, `pull-apple.py` for App Store Connect and TestFlight, `pull-play.py` for the Play Developer Reporting API); see `worker/CLAUDE.md`.
